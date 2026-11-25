using System.Text.Json;
using System.Text.Json.Serialization;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Orchestrates command parsing, headless execution, formatted console/JSON output, and exit code mapping.
/// </summary>
public static class CliApplication
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>
    /// Runs the CLI command described by <paramref name="args"/>, writing output to <paramref name="stdout"/>
    /// and errors to <paramref name="stderr"/>.
    /// </summary>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        string? version = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        CliParseResult parsed = CliArgumentParser.Parse(args);
        if (parsed.IsError)
        {
            await stderr.WriteLineAsync($"Error: {parsed.ErrorMessage}").ConfigureAwait(false);
            return CliExitCodes.UsageError;
        }

        switch (parsed.Command)
        {
            case CliCommandKind.Help:
                await WriteHelpAsync(stdout, parsed.HelpTopic).ConfigureAwait(false);
                return CliExitCodes.Success;

            case CliCommandKind.Version:
                string displayVersion = string.IsNullOrWhiteSpace(version)
                    ? typeof(CliApplication).Assembly.GetName().Version?.ToString() ?? "0.0.0"
                    : version;
                await stdout.WriteLineAsync($"LeiterWatercolorStudio {displayVersion}").ConfigureAwait(false);
                return CliExitCodes.Success;

            case CliCommandKind.ListStages:
                await WriteStagesListAsync(stdout, parsed.JsonOutput).ConfigureAwait(false);
                return CliExitCodes.Success;

            case CliCommandKind.Export:
                return await RunExportAsync(parsed.ExportOptions!, stdout, stderr, ct).ConfigureAwait(false);

            case CliCommandKind.LaunchGui:
                await stderr.WriteLineAsync("Error: No CLI command specified.").ConfigureAwait(false);
                return CliExitCodes.UsageError;

            default:
                await stderr.WriteLineAsync($"Error: Unsupported CLI command '{parsed.Command}'.").ConfigureAwait(false);
                return CliExitCodes.UsageError;
        }
    }

    private static async Task<int> RunExportAsync(
        CliExportOptions options,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken ct)
    {
        bool emitHuman = !options.Quiet && !options.JsonOutput;
        object printLock = new();
        int completedSteps = 0;
        int totalSteps = Enum.GetValues<WatercolorStep>().Length;

        Action<WatercolorStepProgress>? onStep = emitHuman
            ? step =>
            {
                lock (printLock)
                {
                    completedSteps++;
                    string status = step.Bypassed
                        ? "bypassed"
                        : $"{step.Elapsed.TotalMilliseconds:0} ms";
                    stdout.WriteLine($"  [{completedSteps}/{totalSteps}] {step.Step} ({status})");
                }
            }
            : null;

        IProgress<(int Completed, int Total, string FileName)>? onExport = emitHuman
            ? new SynchronousProgress<(int Completed, int Total, string FileName)>(report =>
            {
                lock (printLock)
                {
                    stdout.WriteLine($"  Saved ({report.Completed}/{report.Total}): {report.FileName}");
                }
            })
            : null;

        if (emitHuman)
        {
            await stdout.WriteLineAsync($"Processing '{options.InputPath}' (mode: {FormatMode(options.Mode)})...").ConfigureAwait(false);
        }

        try
        {
            HeadlessExportResult result = await HeadlessExportRunner.ExecuteAsync(
                options,
                onStep,
                onExport,
                ct).ConfigureAwait(false);

            if (options.JsonOutput)
            {
                string json = JsonSerializer.Serialize(CreateJsonSummary(result), JsonOptions);
                await stdout.WriteLineAsync(json).ConfigureAwait(false);
            }
            else if (emitHuman)
            {
                await WriteHumanSummaryAsync(stdout, result).ConfigureAwait(false);
            }

            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            await stderr.WriteLineAsync("Cancelled.").ConfigureAwait(false);
            return CliExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            return CliExitCodes.RuntimeError;
        }
    }

    private static async Task WriteHumanSummaryAsync(TextWriter stdout, HeadlessExportResult result)
    {
        string dims = result.RenderWidth == result.NativeWidth && result.RenderHeight == result.NativeHeight
            ? $"{result.NativeWidth}x{result.NativeHeight} (full)"
            : $"{result.RenderWidth}x{result.RenderHeight} (downscaled from {result.NativeWidth}x{result.NativeHeight})";

        string configDesc = result.PresetPath is not null
            ? $"preset '{result.PresetName}' ({(result.PresetScaled ? "scaled" : "exact")})"
            : $"baseline {FormatBaseline(result.BaselineMode!.Value)}";

        await stdout.WriteLineAsync(
            $"Done in {result.TotalElapsedMs:0} ms (render {result.RenderElapsedMs:0} ms, export {result.ExportElapsedMs:0} ms) — {dims}, {configDesc}.")
            .ConfigureAwait(false);

        if (result.OutputDirectory is not null)
        {
            await stdout.WriteLineAsync($"Wrote {result.OutputFiles.Count} file(s) to '{result.OutputDirectory}'.")
                .ConfigureAwait(false);
        }
        else if (result.OutputFiles.Count == 1)
        {
            await stdout.WriteLineAsync($"Output: {result.OutputFiles[0]}").ConfigureAwait(false);
        }
    }

    private static async Task WriteStagesListAsync(TextWriter stdout, bool jsonOutput)
    {
        if (jsonOutput)
        {
            var payload = new
            {
                stages = StageSelectorResolver.Stages.Select(static s => new
                {
                    key = s.Key,
                    shortAlias = s.ShortAlias,
                    group = s.GroupAlias,
                    fileName = s.Entry.FileName,
                    view = s.Entry.View.ToString(),
                    visualizationMode = s.Entry.Mode.ToString(),
                }),
                groups = StageSelectorResolver.Groups.Select(static g => new
                {
                    alias = g.Alias,
                    displayName = g.DisplayName,
                    stages = g.StageKeys,
                }),
            };

            await stdout.WriteLineAsync(JsonSerializer.Serialize(payload, JsonOptions)).ConfigureAwait(false);
            return;
        }

        await stdout.WriteLineAsync("Exportable diagnostic stages (24):").ConfigureAwait(false);
        foreach (StageSelectorDescriptor stage in StageSelectorResolver.Stages)
        {
            await stdout.WriteLineAsync(
                $"  {stage.Key,-30} alias: {stage.ShortAlias,-27} group: {stage.GroupAlias}")
                .ConfigureAwait(false);
        }

        await stdout.WriteLineAsync().ConfigureAwait(false);
        await stdout.WriteLineAsync("Stage group aliases:").ConfigureAwait(false);
        foreach (StageGroupDescriptor group in StageSelectorResolver.Groups)
        {
            await stdout.WriteLineAsync(
                $"  {group.Alias,-16} {group.DisplayName,-18} ({group.StageKeys.Count} stage{(group.StageKeys.Count == 1 ? string.Empty : "s")})")
                .ConfigureAwait(false);
        }

        await stdout.WriteLineAsync().ConfigureAwait(false);
        await stdout.WriteLineAsync("Convenience alias: 'final' -> '06f_final_watercolor' (hyphens/underscores and optional '.png' are accepted).")
            .ConfigureAwait(false);
    }

    private static async Task WriteHelpAsync(TextWriter stdout, string? topic)
    {
        string text = topic switch
        {
            "export" => ExportHelpText,
            "stages" => StagesHelpText,
            _ => RootHelpText,
        };

        await stdout.WriteAsync(text).ConfigureAwait(false);
    }

    private static object CreateJsonSummary(HeadlessExportResult result) =>
        new
        {
            inputPath = result.InputPath,
            mode = FormatMode(result.Mode),
            nativeWidth = result.NativeWidth,
            nativeHeight = result.NativeHeight,
            renderWidth = result.RenderWidth,
            renderHeight = result.RenderHeight,
            presetPath = result.PresetPath,
            presetName = result.PresetName,
            presetScaled = result.PresetScaled,
            baselineMode = result.BaselineMode is { } b ? FormatBaseline(b) : null,
            outputDirectory = result.OutputDirectory,
            outputFiles = result.OutputFiles,
            renderElapsedMs = Math.Round(result.RenderElapsedMs, 2),
            exportElapsedMs = Math.Round(result.ExportElapsedMs, 2),
            totalElapsedMs = Math.Round(result.TotalElapsedMs, 2),
            stepTimingsMs = result.StepTimingsMs.ToDictionary(
                static kvp => kvp.Key.ToString(),
                static kvp => Math.Round(kvp.Value, 2)),
        };

    private static string FormatMode(CliExportMode mode) => mode switch
    {
        CliExportMode.Final => "final",
        CliExportMode.AllStages => "all-stages",
        CliExportMode.Stages => "stages",
        _ => mode.ToString(),
    };

    private static string FormatBaseline(Models.BaselineMode mode) => mode switch
    {
        Models.BaselineMode.ForImage => "for-image",
        Models.BaselineMode.ForSize => "for-size",
        Models.BaselineMode.Default => "default",
        _ => mode.ToString(),
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    private const string RootHelpText = """
LeiterWatercolorStudio — Interactive desktop studio and headless watercolor pipeline CLI.

Usage:
  LeiterWatercolorStudio                               Launch desktop GUI
  LeiterWatercolorStudio export <input> [options]      Render and export headlessly
  LeiterWatercolorStudio stages [--json]               List exportable pipeline stages & aliases
  LeiterWatercolorStudio --help [export|stages]        Show help
  LeiterWatercolorStudio --version                     Show version

Commands:
  export    Render an input image headlessly and export final or diagnostic stage images.
  stages    Print all 24 diagnostic stage keys, short aliases, and stage-group aliases.

Run 'LeiterWatercolorStudio export --help' for detailed export options and examples.
""";

    private const string ExportHelpText = """
Usage:
  LeiterWatercolorStudio export <input> [options]
  LeiterWatercolorStudio export -i <input> [options]

Arguments:
  <input>                         Path to the source image (PNG, JPEG, WebP, BMP, etc.).

Options:
  -i, --input <path>              Source image path (alternative to positional <input>).
  -o, --output <path>             Output image file (for 'final' or single-stage file export)
                                  or output directory (for 'all-stages' / multi-stage 'stages').
                                  Defaults to '<stem>_watercolor.png' or '<stem>_stages/'.
  -p, --preset <path.json>        Optional preset JSON file to configure pipeline parameters.
      --scale-preset              Always scale preset spatial parameters to match input size.
      --no-scale-preset           Keep exact preset parameter values without resolution scaling.
                                  (Default with --preset: auto-scale when > 5% size difference.)
  -b, --baseline <mode>           Baseline when --preset is omitted: for-image (default),
                                  for-size, or default. Cannot be combined with --preset.
  -m, --mode <mode>               Export mode: final (default), all-stages, or stages
                                  (automatically set to 'stages' when --stages is passed).
  -s, --stages <list>             Comma-separated and/or repeatable stage names, short aliases,
                                  or group aliases (implies --mode stages).
  -r, --resolution <cap>          Max output dimension: full (default), native, or positive px
                                  (e.g. 512, 768, 1024, 1536). Preserves aspect ratio; never upscales.
  -q, --quiet                     Suppress standard output (mutually exclusive with --json).
      --json                      Emit a single JSON summary object to stdout.
  -h, --help                      Show this help text.

Examples:
  LeiterWatercolorStudio export photo.jpg
  LeiterWatercolorStudio export photo.jpg -p warm.json -o watercolor.webp
  LeiterWatercolorStudio export photo.jpg -r 1024 -o preview.png
  LeiterWatercolorStudio export photo.jpg -m all-stages -o ./stages_out
  LeiterWatercolorStudio export photo.jpg -s saliency,05_hand_tremor -o ./partial
  LeiterWatercolorStudio export photo.jpg -s hand-tremor -o tremor.png
""";

    private const string StagesHelpText = """
Usage:
  LeiterWatercolorStudio stages [--json]

Lists all 24 exportable diagnostic stage keys, short aliases, and 8 primary stage group aliases
accepted by 'LeiterWatercolorStudio export --stages <list>'.

Options:
      --json    Emit machine-readable JSON instead of a formatted table.
  -h, --help    Show this help text.
""";
}
