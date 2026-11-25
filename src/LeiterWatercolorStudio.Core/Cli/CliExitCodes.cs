namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Standard process exit codes returned by the headless CLI.
/// </summary>
public static class CliExitCodes
{
    public const int Success = 0;
    public const int RuntimeError = 1;
    public const int UsageError = 2;
    public const int Cancelled = 130;
    public const int Terminated = 143;
}
