using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Summarizes a completed headless watercolor export execution.
/// </summary>
public sealed record HeadlessExportResult
{
    public required string InputPath { get; init; }
    public required CliExportMode Mode { get; init; }
    public required int NativeWidth { get; init; }
    public required int NativeHeight { get; init; }
    public required int RenderWidth { get; init; }
    public required int RenderHeight { get; init; }
    public string? PresetPath { get; init; }
    public string? PresetName { get; init; }
    public bool PresetScaled { get; init; }
    public BaselineMode? BaselineMode { get; init; }
    public string? OutputDirectory { get; init; }
    public required IReadOnlyList<string> OutputFiles { get; init; }
    public required double RenderElapsedMs { get; init; }
    public required double ExportElapsedMs { get; init; }
    public required double TotalElapsedMs { get; init; }
    public required IReadOnlyDictionary<WatercolorStep, double> StepTimingsMs { get; init; }
}
