using LeiterWatercolorStudio.Core.Cli;
using LeiterWatercolorStudio.Core.Export;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Cli;

public class StageSelectorResolverTests
{
    [Fact]
    public void Resolver_CoversEveryManifestEntryAndGroup()
    {
        Assert.Equal(ExportService.StageManifest.Count, StageSelectorResolver.Stages.Count);
        Assert.Equal(8, StageSelectorResolver.Groups.Count);

        for (int i = 0; i < ExportService.StageManifest.Count; i++)
        {
            StageExportEntry manifestEntry = ExportService.StageManifest[i];
            StageSelectorDescriptor desc = StageSelectorResolver.Stages[i];
            Assert.Equal(manifestEntry, desc.Entry);
            Assert.Equal(Path.GetFileNameWithoutExtension(manifestEntry.FileName), desc.Key);
            Assert.True(StageSelectorResolver.TryResolve([desc.Key], out var byKey, out _));
            Assert.Equal([manifestEntry], byKey);

            Assert.True(StageSelectorResolver.TryResolve([manifestEntry.FileName], out var byPng, out _));
            Assert.Equal([manifestEntry], byPng);

            Assert.True(StageSelectorResolver.TryResolve([desc.ShortAlias.Replace('_', '-')], out var byKebab, out _));
            Assert.Equal([manifestEntry], byKebab);
        }
    }

    [Fact]
    public void TryResolve_WetInWetGroupVsSingleImage_AreUnambiguous()
    {
        Assert.True(StageSelectorResolver.TryResolve(["wet-in-wet"], out var groupEntries, out _));
        Assert.Equal(
            ["04_wet_in_wet.png", "04_wet_area_mask.png", "04_wet_qualifying_boundaries.png"],
            groupEntries.Select(static e => e.FileName).ToArray());

        Assert.True(StageSelectorResolver.TryResolve(["04_wet_in_wet"], out var byStem, out _));
        Assert.Equal(["04_wet_in_wet.png"], byStem.Select(static e => e.FileName).ToArray());

        Assert.True(StageSelectorResolver.TryResolve(["wet-in-wet-image"], out var byAlias, out _));
        Assert.Equal(["04_wet_in_wet.png"], byAlias.Select(static e => e.FileName).ToArray());
    }

    [Fact]
    public void TryResolve_DeduplicatesAndSortsInCanonicalManifestOrder()
    {
        Assert.True(StageSelectorResolver.TryResolve(
            ["final,05-hand-tremor.png", "saliency,02c_saliency_rc", "INPUT"],
            out var resolved,
            out _));

        string[] expected =
        [
            "00_input.png",
            "02a_egbi_preblur.png",
            "02b_egbi_regions_avg.png",
            "02b_egbi_regions_random.png",
            "02c_saliency_rc.png",
            "02d_saliency_cut_mask.png",
            "02e_distance_field_raw.png",
            "02f_distance_field_smoothed.png",
            "05_hand_tremor.png",
            "06f_final_watercolor.png",
        ];

        Assert.Equal(expected, resolved.Select(static e => e.FileName).ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("00_input,,05_hand_tremor")]
    [InlineData("nonexistent_stage")]
    public void TryResolve_InvalidTokens_ReturnError(string token)
    {
        Assert.False(StageSelectorResolver.TryResolve([token], out var resolved, out string? error));
        Assert.Empty(resolved);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
