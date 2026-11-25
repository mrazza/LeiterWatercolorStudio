namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Represents the parsed command-line invocation and any usage validation error.
/// </summary>
public sealed record CliParseResult
{
    public required CliCommandKind Command { get; init; }
    public CliExportOptions? ExportOptions { get; init; }
    public string? HelpTopic { get; init; }
    public bool JsonOutput { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsError => !string.IsNullOrEmpty(ErrorMessage);
}
