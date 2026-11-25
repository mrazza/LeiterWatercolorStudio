using LeiterWatercolorStudio.Core.Export;

namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Resolves CLI stage selector names, short aliases, and primary stage group aliases
/// into canonical <see cref="StageExportEntry"/> items from <see cref="ExportService.StageManifest"/>.
/// </summary>
public static class StageSelectorResolver
{
    private static readonly IReadOnlyDictionary<string, int[]> LookupByToken;

    static StageSelectorResolver()
    {
        IReadOnlyList<StageExportEntry> manifest = ExportService.StageManifest;
        var metadataByFile = new Dictionary<string, (string ShortAlias, string GroupAlias, string[] ExtraAliases)>(StringComparer.Ordinal)
        {
            ["00_input.png"] = ("input", "input", []),
            ["01_color_transfer.png"] = ("color_transfer", "color_transfer", []),
            ["02a_egbi_preblur.png"] = ("egbi_preblur", "saliency", []),
            ["02b_egbi_regions_avg.png"] = ("egbi_regions_avg", "saliency", []),
            ["02b_egbi_regions_random.png"] = ("egbi_regions_random", "saliency", []),
            ["02c_saliency_rc.png"] = ("saliency_rc", "saliency", []),
            ["02d_saliency_cut_mask.png"] = ("saliency_cut_mask", "saliency", []),
            ["02e_distance_field_raw.png"] = ("distance_field_raw", "saliency", []),
            ["02f_distance_field_smoothed.png"] = ("distance_field_smoothed", "saliency", []),
            ["03a_meanshift_filtered.png"] = ("meanshift_filtered", "segmentation", []),
            ["03a_meanshift_regions_avg.png"] = ("meanshift_regions_avg", "segmentation", []),
            ["03a_meanshift_regions_random.png"] = ("meanshift_regions_random", "segmentation", []),
            ["03b_lod_abstraction.png"] = ("lod_abstraction", "lod", []),
            ["04_wet_in_wet.png"] = ("wet_in_wet_image", "wet_in_wet", []),
            ["04_wet_area_mask.png"] = ("wet_area_mask", "wet_in_wet", []),
            ["04_wet_qualifying_boundaries.png"] = ("wet_qualifying_boundaries", "wet_in_wet", []),
            ["05_hand_tremor.png"] = ("hand_tremor", "hand_tremor", []),
            ["06a_edge_darkening_density.png"] = ("edge_darkening_density", "finishing", []),
            ["06b_turbulence_density.png"] = ("turbulence_density", "finishing", []),
            ["06c_pigment_combined.png"] = ("pigment_combined", "finishing", []),
            ["06d_tone_curve.png"] = ("tone_curve", "finishing", []),
            ["06e_paper_height_map.png"] = ("paper_height_map", "finishing", []),
            ["06e_paper_granulation.png"] = ("paper_granulation", "finishing", []),
            ["06f_final_watercolor.png"] = ("final_watercolor", "finishing", ["final"]),
        };

        var descriptors = new StageSelectorDescriptor[manifest.Count];
        var lookup = new Dictionary<string, int[]>(StringComparer.Ordinal);
        var groupIndices = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (int i = 0; i < manifest.Count; i++)
        {
            StageExportEntry entry = manifest[i];
            if (!metadataByFile.TryGetValue(entry.FileName, out var meta))
            {
                throw new InvalidOperationException($"Stage manifest entry '{entry.FileName}' has no CLI selector mapping.");
            }

            string key = Path.GetFileNameWithoutExtension(entry.FileName).ToLowerInvariant();
            descriptors[i] = new StageSelectorDescriptor(key, meta.ShortAlias, meta.GroupAlias, entry);

            lookup[key] = [i];
            lookup[meta.ShortAlias] = [i];
            foreach (string extra in meta.ExtraAliases)
            {
                lookup[extra] = [i];
            }

            if (!groupIndices.TryGetValue(meta.GroupAlias, out List<int>? list))
            {
                list = [];
                groupIndices[meta.GroupAlias] = list;
            }

            list.Add(i);
        }

        (string Alias, string DisplayName)[] groupOrder =
        [
            ("input", "Input"),
            ("color_transfer", "Color Transfer"),
            ("saliency", "Saliency"),
            ("segmentation", "Segmentation"),
            ("lod", "LOD Abstraction"),
            ("wet_in_wet", "Wet-in-Wet"),
            ("hand_tremor", "Hand Tremor"),
            ("finishing", "Finishing"),
        ];

        var groups = new StageGroupDescriptor[groupOrder.Length];
        for (int i = 0; i < groupOrder.Length; i++)
        {
            (string alias, string displayName) = groupOrder[i];
            List<int> indices = groupIndices[alias];
            string[] stageKeys = indices.Select(idx => descriptors[idx].Key).ToArray();
            groups[i] = new StageGroupDescriptor(alias, displayName, Array.AsReadOnly(stageKeys));
            lookup[alias] = indices.ToArray();
        }

        Stages = Array.AsReadOnly(descriptors);
        Groups = Array.AsReadOnly(groups);
        LookupByToken = lookup;
    }

    /// <summary>
    /// Gets descriptors for all 24 exportable diagnostic stages in canonical manifest order.
    /// </summary>
    public static IReadOnlyList<StageSelectorDescriptor> Stages { get; }

    /// <summary>
    /// Gets descriptors for all 8 primary stage group aliases.
    /// </summary>
    public static IReadOnlyList<StageGroupDescriptor> Groups { get; }

    /// <summary>
    /// Resolves one or more raw <c>--stages</c> argument strings (supporting comma-separated tokens,
    /// manifest stems, short aliases, and stage-group aliases) into deduplicated manifest entries in canonical order.
    /// </summary>
    public static bool TryResolve(
        IEnumerable<string> rawArguments,
        out IReadOnlyList<StageExportEntry> resolved,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(rawArguments);

        bool[] selected = new bool[ExportService.StageManifest.Count];
        bool sawAny = false;

        foreach (string rawArg in rawArguments)
        {
            if (string.IsNullOrWhiteSpace(rawArg))
            {
                resolved = [];
                error = "Stage selector in '--stages' cannot be empty.";
                return false;
            }

            string[] parts = rawArg.Split(',');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                {
                    resolved = [];
                    error = $"Empty stage selector in '--stages' value '{rawArg}'.";
                    return false;
                }

                string normalized = NormalizeToken(trimmed);
                if (!LookupByToken.TryGetValue(normalized, out int[]? indices))
                {
                    resolved = [];
                    error = $"Unknown stage selector '{trimmed}'. Run 'LeiterWatercolorStudio stages' to list valid stage names and aliases.";
                    return false;
                }

                sawAny = true;
                foreach (int idx in indices)
                {
                    selected[idx] = true;
                }
            }
        }

        if (!sawAny)
        {
            resolved = [];
            error = "Option '--stages' requires at least one stage name or group alias.";
            return false;
        }

        var result = new List<StageExportEntry>();
        IReadOnlyList<StageExportEntry> manifest = ExportService.StageManifest;
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i])
            {
                result.Add(manifest[i]);
            }
        }

        resolved = result;
        error = null;
        return true;
    }

    internal static string NormalizeToken(string token)
    {
        string trimmed = token.Trim();
        if (trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        return trimmed.ToLowerInvariant().Replace('-', '_');
    }
}
