namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Identifies the top-level action requested by command-line arguments.
/// </summary>
public enum CliCommandKind
{
    LaunchGui,
    Export,
    ListStages,
    Help,
    Version,
}
