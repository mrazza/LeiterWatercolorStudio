using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Parsed options for the headless <c>export</c> command.
/// </summary>
public sealed record CliExportOptions
{
    public required string InputPath { get; init; }
    public string? OutputPath { get; init; }
    public string? PresetPath { get; init; }
    public CliPresetScalingMode PresetScaling { get; init; } = CliPresetScalingMode.Auto;
    public BaselineMode Baseline { get; init; } = BaselineMode.ForImage;
    public CliExportMode Mode { get; init; } = CliExportMode.Final;
    public IReadOnlyList<StageExportEntry> SelectedStages { get; init; } = [];
    public int? MaxDimension { get; init; }
    public bool Quiet { get; init; }
    public bool JsonOutput { get; init; }
}
