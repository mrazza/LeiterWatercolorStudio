using System.Diagnostics;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using LeiterWatercolorStudio.Core.Presets;

namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Executes headless watercolor pipeline renders and exports without requiring a UI display server.
/// </summary>
public static class HeadlessExportRunner
{
    /// <summary>
    /// Validates inputs, loads the source image and optional preset, renders the watercolor pipeline,
    /// and writes the requested final or diagnostic stage exports.
    /// </summary>
    public static async Task<HeadlessExportResult> ExecuteAsync(
        CliExportOptions options,
        Action<WatercolorStepProgress>? onRenderStep = null,
        IProgress<(int Completed, int Total, string FileName)>? onExportProgress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.InputPath);
        ct.ThrowIfCancellationRequested();

        if (options.Mode == CliExportMode.Stages && options.SelectedStages.Count == 0)
        {
            throw new ArgumentException("At least one stage must be selected for 'stages' export mode.", nameof(options));
        }

        if (options.MaxDimension is int maxDim && maxDim <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), maxDim, "Maximum resolution dimension must be positive.");
        }

        string fullInputPath = Path.GetFullPath(options.InputPath);
        if (!File.Exists(fullInputPath))
        {
            throw new FileNotFoundException($"Input image file not found: '{fullInputPath}'.", fullInputPath);
        }

        string? fullPresetPath = null;
        PresetDocument? preset = null;
        if (!string.IsNullOrWhiteSpace(options.PresetPath))
        {
            fullPresetPath = Path.GetFullPath(options.PresetPath);
            if (!File.Exists(fullPresetPath))
            {
                throw new FileNotFoundException($"Preset JSON file not found: '{fullPresetPath}'.", fullPresetPath);
            }

            preset = await PresetSerializer.LoadFromFileAsync(fullPresetPath, ct).ConfigureAwait(false);
        }

        OutputPlan outputPlan = ResolveOutputPlan(options, fullInputPath);

        long totalStart = Stopwatch.GetTimestamp();
        SequentialMatrix<Rgb8> nativeSource = await Task.Run(
            () => ImageExtensions.LoadRgb8(fullInputPath),
            ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        Size nativeSize = nativeSource.Size;
        WatercolorOptions canonical;
        bool presetScaled = false;
        BaselineMode? baselineUsed = null;

        if (preset is not null)
        {
            bool shouldScale = options.PresetScaling switch
            {
                CliPresetScalingMode.Scale => true,
                CliPresetScalingMode.Exact => false,
                CliPresetScalingMode.Auto => PresetSerializer.ShouldAutoScaleForImageSize(preset, nativeSize),
                _ => throw new ArgumentOutOfRangeException(nameof(options), options.PresetScaling, "Unsupported preset scaling mode."),
            };

            canonical = PresetSerializer.AdaptForImageSize(preset, nativeSize, shouldScale);
            presetScaled = shouldScale &&
                           double.IsFinite(preset.CalibrationReferenceDimension) &&
                           preset.CalibrationReferenceDimension > 0.0;
        }
        else
        {
            baselineUsed = options.Baseline;
            canonical = options.Baseline switch
            {
                BaselineMode.Default => WatercolorOptions.Default,
                BaselineMode.ForSize => WatercolorOptions.ForSize(nativeSize),
                BaselineMode.ForImage => await Task.Run(
                    () => WatercolorOptions.ForAnalysis(ImageAnalysis.Analyze(nativeSource)),
                    ct).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(options), options.Baseline, "Unsupported baseline mode."),
            };
        }

        Size renderSize = options.MaxDimension is int requestedCap
            ? PreviewScaling.ComputePreviewSize(nativeSize, requestedCap)
            : nativeSize;

        IReadOnlyMatrix<Rgb8> renderSource = renderSize == nativeSize
            ? nativeSource
            : await Task.Run(() => nativeSource.ResizeBox(renderSize), ct).ConfigureAwait(false);

        WatercolorOptions effectiveOptions = PreviewScaling.ComputeEffectivePreviewOptions(
            canonical,
            nativeSize,
            renderSize);

        long renderStart = Stopwatch.GetTimestamp();
        WatercolorResult result = await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var session = new WatercolorPipelineSession(renderSource);
            return session.Render(effectiveOptions, onRenderStep, ct);
        }, ct).ConfigureAwait(false);
        double renderElapsedMs = Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;

        long exportStart = Stopwatch.GetTimestamp();
        var outputFiles = new List<string>();

        if (outputPlan.SingleFilePath is string targetFile)
        {
            if (options.Mode == CliExportMode.Final)
            {
                await ExportService.ExportFinalImageAsync(result.Image, targetFile, ct).ConfigureAwait(false);
            }
            else
            {
                StageExportEntry entry = options.SelectedStages[0];
                IReadOnlyMatrix<Rgb8> rendered = await Task.Run(
                    () => StageVisualizers.RenderView(entry.View, entry.Mode, renderSource, result, effectiveOptions),
                    ct).ConfigureAwait(false);

                await ExportService.ExportFinalImageAsync(rendered, targetFile, ct).ConfigureAwait(false);
            }

            outputFiles.Add(targetFile);
            onExportProgress?.Report((1, 1, Path.GetFileName(targetFile)));
        }
        else
        {
            string targetDir = outputPlan.DirectoryPath!;
            IReadOnlyList<StageExportEntry> entries = options.Mode == CliExportMode.AllStages
                ? ExportService.StageManifest
                : options.SelectedStages;

            await ExportService.ExportStagesAsync(
                renderSource,
                result,
                effectiveOptions,
                entries,
                targetDir,
                customVisualizer: null,
                includePresetSidecar: false,
                calibrationReferenceDimension: renderSize.LinearDimension,
                paletteMode: preset?.PaletteMode ?? PaletteMode.Auto,
                progress: onExportProgress,
                ct: ct).ConfigureAwait(false);

            foreach (StageExportEntry entry in entries)
            {
                outputFiles.Add(Path.Combine(targetDir, entry.FileName));
            }
        }

        double exportElapsedMs = Stopwatch.GetElapsedTime(exportStart).TotalMilliseconds;
        double totalElapsedMs = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;

        var stepTimingsMs = new Dictionary<WatercolorStep, double>(result.StepTimings.Count);
        foreach (var (step, timing) in result.StepTimings)
        {
            stepTimingsMs[step] = timing.Elapsed.TotalMilliseconds;
        }

        return new HeadlessExportResult
        {
            InputPath = fullInputPath,
            Mode = options.Mode,
            NativeWidth = nativeSize.Width,
            NativeHeight = nativeSize.Height,
            RenderWidth = renderSize.Width,
            RenderHeight = renderSize.Height,
            PresetPath = fullPresetPath,
            PresetName = preset?.Name,
            PresetScaled = presetScaled,
            BaselineMode = baselineUsed,
            OutputDirectory = outputPlan.DirectoryPath,
            OutputFiles = outputFiles,
            RenderElapsedMs = renderElapsedMs,
            ExportElapsedMs = exportElapsedMs,
            TotalElapsedMs = totalElapsedMs,
            StepTimingsMs = stepTimingsMs,
        };
    }

    private static OutputPlan ResolveOutputPlan(CliExportOptions options, string fullInputPath)
    {
        string inputDir = Path.GetDirectoryName(fullInputPath) ?? Directory.GetCurrentDirectory();
        string inputStem = Path.GetFileNameWithoutExtension(fullInputPath);

        if (options.Mode == CliExportMode.Final)
        {
            if (!string.IsNullOrWhiteSpace(options.OutputPath) && EndsWithDirectorySeparator(options.OutputPath))
            {
                throw new ArgumentException(
                    $"Output path '{options.OutputPath}' ends with a directory separator; '--mode final' requires a file path.",
                    nameof(options));
            }

            string targetFile = string.IsNullOrWhiteSpace(options.OutputPath)
                ? Path.Combine(inputDir, $"{inputStem}_watercolor.png")
                : Path.GetFullPath(options.OutputPath);

            if (Directory.Exists(targetFile))
            {
                throw new IOException(
                    $"Output path '{targetFile}' is an existing directory; '--mode final' requires a file path.");
            }

            if (!ExportService.IsSupportedImageExtension(targetFile))
            {
                string ext = Path.GetExtension(targetFile);
                throw new NotSupportedException(
                    $"Unsupported image file extension '{ext}' for '{targetFile}'. Supported extensions include .png, .jpg, .webp, .bmp, .tga, and .tiff.");
            }

            return new OutputPlan(SingleFilePath: targetFile, DirectoryPath: null);
        }

        if (options.Mode == CliExportMode.Stages &&
            options.SelectedStages.Count == 1 &&
            !string.IsNullOrWhiteSpace(options.OutputPath) &&
            !EndsWithDirectorySeparator(options.OutputPath))
        {
            string candidatePath = Path.GetFullPath(options.OutputPath);
            if (!Directory.Exists(candidatePath) && ExportService.IsSupportedImageExtension(candidatePath))
            {
                return new OutputPlan(SingleFilePath: candidatePath, DirectoryPath: null);
            }
        }

        if (IsDisallowedMultiStageOutputPath(options.OutputPath))
        {
            throw new ArgumentException(
                "--output cannot specify an image file extension when exporting multiple stages; provide a directory path instead.",
                nameof(options));
        }

        string targetDir = string.IsNullOrWhiteSpace(options.OutputPath)
            ? Path.Combine(inputDir, $"{inputStem}_stages")
            : Path.GetFullPath(options.OutputPath);

        if (File.Exists(targetDir))
        {
            throw new IOException($"Output directory path '{targetDir}' already exists as a regular file.");
        }

        return new OutputPlan(SingleFilePath: null, DirectoryPath: targetDir);
    }

    internal static bool IsDisallowedMultiStageOutputPath(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || EndsWithDirectorySeparator(outputPath))
        {
            return false;
        }

        return !Directory.Exists(outputPath) && ExportService.IsSupportedImageExtension(outputPath);
    }

    private static bool EndsWithDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ||
        path.EndsWith(Path.AltDirectorySeparatorChar);

    private readonly record struct OutputPlan(string? SingleFilePath, string? DirectoryPath);
}
