namespace LeiterWatercolorStudio.Core.Export;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Presets;

/// <summary>
/// Exports final watercolor paintings and full multi-stage diagnostic PNG bundles with optional preset sidecars.
/// </summary>
public static class ExportService
{
    /// <summary>
    /// Describes a single diagnostic stage image file in a full-stage export bundle.
    /// </summary>
    /// <param name="FileName">The target PNG filename relative to the export directory.</param>
    /// <param name="View">The logical stage view identifier.</param>
    /// <param name="Mode">The visualization mode applied when rendering the stage view.</param>
    public readonly record struct StageExportEntry(
        string FileName,
        StageViewId View,
        VisualizationMode Mode);

    private static readonly Rgb8 QualifiedBoundaryColor = new(0, 255, 255);
    private static readonly Rgb8 RejectedBoundaryColor = new(160, 48, 48);
    private static readonly Rgb8 NeutralSignedDensityColor = new(128, 128, 128);

    /// <summary>
    /// Gets the ordered list of all 24 diagnostic stage PNG entries produced by <see cref="ExportAllStagesAsync"/>.
    /// </summary>
    public static IReadOnlyList<StageExportEntry> StageManifest { get; } =
    [
        new("00_input.png", StageViewId.Input, VisualizationMode.Rgb),
        new("01_color_transfer.png", StageViewId.ColorTransfer, VisualizationMode.Rgb),
        new("02a_egbi_preblur.png", StageViewId.SaliencyPreBlur, VisualizationMode.Rgb),
        new("02b_egbi_regions_avg.png", StageViewId.SaliencyEgbiRegions, VisualizationMode.RegionAverageColor),
        new("02b_egbi_regions_random.png", StageViewId.SaliencyEgbiRegions, VisualizationMode.RegionRandomColor),
        new("02c_saliency_rc.png", StageViewId.SaliencyRcMap, VisualizationMode.Grayscale),
        new("02d_saliency_cut_mask.png", StageViewId.SaliencyCutMask, VisualizationMode.BinaryMask),
        new("02e_distance_field_raw.png", StageViewId.SaliencyDistanceField, VisualizationMode.DistanceRawGrayscale),
        new("02f_distance_field_smoothed.png", StageViewId.SaliencyDistanceField, VisualizationMode.DistanceSmoothedGrayscale),
        new("03a_meanshift_filtered.png", StageViewId.SegmentationMeanShiftFiltered, VisualizationMode.Rgb),
        new("03a_meanshift_regions_avg.png", StageViewId.SegmentationMeanShiftRegions, VisualizationMode.RegionAverageColor),
        new("03a_meanshift_regions_random.png", StageViewId.SegmentationMeanShiftRegions, VisualizationMode.RegionRandomColor),
        new("03b_lod_abstraction.png", StageViewId.LodAbstraction, VisualizationMode.Rgb),
        new("04_wet_in_wet.png", StageViewId.WetInWetImage, VisualizationMode.Rgb),
        new("04_wet_area_mask.png", StageViewId.WetInWetAreaMask, VisualizationMode.WetMaskBinary),
        new("04_wet_qualifying_boundaries.png", StageViewId.WetInWetQualifyingBoundaries, VisualizationMode.QualifyingBoundariesOverlay),
        new("05_hand_tremor.png", StageViewId.HandTremor, VisualizationMode.Rgb),
        new("06a_edge_darkening_density.png", StageViewId.FinishingEdgeDarkening, VisualizationMode.DensityGrayscale),
        new("06b_turbulence_density.png", StageViewId.FinishingTurbulenceFlow, VisualizationMode.DensityGrayscale),
        new("06c_pigment_combined.png", StageViewId.FinishingCombinedPigment, VisualizationMode.Rgb),
        new("06d_tone_curve.png", StageViewId.FinishingToneCurve, VisualizationMode.Rgb),
        new("06e_paper_height_map.png", StageViewId.FinishingPaperGranulation, VisualizationMode.PaperHeightGrayscale),
        new("06e_paper_granulation.png", StageViewId.FinishingPaperGranulation, VisualizationMode.Rgb),
        new("06f_final_watercolor.png", StageViewId.FinishingFinalFxaa, VisualizationMode.Rgb),
    ];

    /// <summary>
    /// Encodes and saves <paramref name="image"/> asynchronously to <paramref name="outputPath"/> (PNG, JPEG, WebP, or any ImageSharp-supported format).
    /// </summary>
    /// <param name="image">The image matrix to save.</param>
    /// <param name="outputPath">The destination file path whose extension determines the image encoder.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public static async Task ExportFinalImageAsync(
        IReadOnlyMatrix<Rgb8> image,
        string outputPath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(image.Width, 0, nameof(image));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(image.Height, 0, nameof(image));
        ct.ThrowIfCancellationRequested();

        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            image.SaveImage(outputPath);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports all 24 intermediate and final stage PNGs (plus an optional <c>preset.json</c> sidecar) into <paramref name="outputDir"/>.
    /// </summary>
    /// <param name="source">The source RGB image passed into the pipeline.</param>
    /// <param name="result">The completed pipeline result containing all stage intermediates.</param>
    /// <param name="options">The canonical pipeline options used for the render.</param>
    /// <param name="outputDir">The directory into which stage PNGs and optional sidecar are written.</param>
    /// <param name="customVisualizer">Optional custom renderer mapping each <see cref="StageViewId"/> and <see cref="VisualizationMode"/> to an RGB matrix.</param>
    /// <param name="includePresetSidecar">Whether to also write <c>preset.json</c> alongside the 24 PNGs.</param>
    /// <param name="calibrationReferenceDimension">Optional calibration reference dimension recorded in <c>preset.json</c>; defaults to <see cref="Size.LinearDimension"/> of <paramref name="source"/>.</param>
    /// <param name="paletteMode">The palette mode recorded in <c>preset.json</c>.</param>
    /// <param name="progress">Optional progress callback receiving <c>(Completed, Total, FileName)</c> after each exported file.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public static async Task ExportAllStagesAsync(
        IReadOnlyMatrix<Rgb8> source,
        WatercolorResult result,
        WatercolorOptions options,
        string outputDir,
        Func<StageViewId, VisualizationMode, Matrix<Rgb8>>? customVisualizer = null,
        bool includePresetSidecar = true,
        double? calibrationReferenceDimension = null,
        PaletteMode paletteMode = PaletteMode.Auto,
        IProgress<(int Completed, int Total, string FileName)>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDir);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(source.Width, 0, nameof(source));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(source.Height, 0, nameof(source));
        if (source.Size != result.Image.Size)
        {
            throw new ArgumentException(
                $"Result image dimensions {result.Image.Size} must match source dimensions {source.Size}.",
                nameof(result));
        }

        options.Validate();

        double resolvedReferenceDimension = calibrationReferenceDimension ?? source.Size.LinearDimension;
        if (!double.IsFinite(resolvedReferenceDimension) || resolvedReferenceDimension < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(calibrationReferenceDimension),
                resolvedReferenceDimension,
                "Calibration reference dimension must be finite and non-negative.");
        }

        if (!Enum.IsDefined(paletteMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(paletteMode),
                paletteMode,
                $"Unsupported {nameof(PaletteMode)} value.");
        }

        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(outputDir);

        int totalFiles = includePresetSidecar ? StageManifest.Count + 1 : StageManifest.Count;
        int completed = 0;

        await Task.Run(() =>
        {
            foreach (StageExportEntry entry in StageManifest)
            {
                ct.ThrowIfCancellationRequested();

                IReadOnlyMatrix<Rgb8> rendered = customVisualizer is not null
                    ? customVisualizer(entry.View, entry.Mode)
                    : RenderFallback(entry.View, entry.Mode, source, result);

                string filePath = Path.Combine(outputDir, entry.FileName);
                rendered.SaveImage(filePath);

                completed++;
                progress?.Report((completed, totalFiles, entry.FileName));
            }
        }, ct).ConfigureAwait(false);

        if (includePresetSidecar)
        {
            ct.ThrowIfCancellationRequested();
            PresetDocument sidecar = CreatePresetSidecar(
                outputDir,
                result,
                options,
                resolvedReferenceDimension,
                paletteMode);

            string sidecarPath = Path.Combine(outputDir, "preset.json");
            await PresetSerializer.SaveToFileAsync(sidecar, sidecarPath, ct).ConfigureAwait(false);

            completed++;
            progress?.Report((completed, totalFiles, "preset.json"));
        }
    }

    private static PresetDocument CreatePresetSidecar(
        string outputDir,
        WatercolorResult result,
        WatercolorOptions options,
        double calibrationReferenceDimension,
        PaletteMode paletteMode)
    {
        string trimmedDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDir));
        string dirName = Path.GetFileName(trimmedDir);
        string presetName = string.IsNullOrWhiteSpace(dirName) ? "Exported Watercolor Preset" : dirName;

        Dictionary<WatercolorStep, double>? stepTimingsMs = null;
        if (result.StepTimings.Count > 0)
        {
            stepTimingsMs = new Dictionary<WatercolorStep, double>(result.StepTimings.Count);
            foreach (var (step, timing) in result.StepTimings)
            {
                stepTimingsMs[step] = timing.Elapsed.TotalMilliseconds;
            }
        }

        return new PresetDocument
        {
            SchemaVersion = PresetDocument.CurrentSchemaVersion,
            Name = presetName,
            CalibrationReferenceDimension = calibrationReferenceDimension,
            PaletteMode = paletteMode,
            Options = options,
            StepTimingsMs = stepTimingsMs,
        };
    }

    private static IReadOnlyMatrix<Rgb8> RenderFallback(
        StageViewId view,
        VisualizationMode mode,
        IReadOnlyMatrix<Rgb8> source,
        WatercolorResult result) =>
        (view, mode) switch
        {
            (StageViewId.Input, _) => source,
            (StageViewId.ColorTransfer, _) => result.ColorTransferred,
            (StageViewId.SaliencyPreBlur, _) => result.EgbiPreBlurred,
            (StageViewId.SaliencyEgbiRegions, VisualizationMode.RegionAverageColor) =>
                SegmentationVisualizer.ColorRegions(
                    result.EgbiPreBlurred,
                    result.EgbiGraph,
                    SegmentationVisualizer.AverageColorAssigner()),
            (StageViewId.SaliencyEgbiRegions, VisualizationMode.RegionRandomColor) =>
                SegmentationVisualizer.ColorRegions(result.EgbiPreBlurred, result.EgbiGraph, seed: 42),
            (StageViewId.SaliencyRcMap, _) => result.SaliencyMap.ToGrayscaleRgb8(),
            (StageViewId.SaliencyCutMask, _) => result.SalientMask.ToGrayscaleRgb8(),
            (StageViewId.SaliencyDistanceField, VisualizationMode.DistanceRawGrayscale) =>
                result.RawSaliencyDistanceField.ToGrayscaleRgb8(),
            (StageViewId.SaliencyDistanceField, VisualizationMode.DistanceSmoothedGrayscale) =>
                result.SaliencyDistanceField.ToGrayscaleRgb8(),
            (StageViewId.SegmentationMeanShiftFiltered, _) => result.MeanShiftFiltered,
            (StageViewId.SegmentationMeanShiftRegions, VisualizationMode.RegionAverageColor) =>
                SegmentationVisualizer.ColorRegions(
                    result.MeanShiftFiltered,
                    result.SegmentationGraph,
                    SegmentationVisualizer.AverageColorAssigner()),
            (StageViewId.SegmentationMeanShiftRegions, VisualizationMode.RegionRandomColor) =>
                SegmentationVisualizer.ColorRegions(result.MeanShiftFiltered, result.SegmentationGraph, seed: 42),
            (StageViewId.LodAbstraction, _) => result.LodAbstracted,
            (StageViewId.WetInWetImage, _) => result.WetInWet.Image,
            (StageViewId.WetInWetAreaMask, _) => result.WetInWet.WetAreaMask.ToGrayscaleRgb8(),
            (StageViewId.WetInWetQualifyingBoundaries, _) => RenderQualifyingBoundaries(result),
            (StageViewId.HandTremor, _) => result.HandTremor,
            (StageViewId.FinishingEdgeDarkening, _) =>
                RenderNonNegativeDensityField(result.EdgeDensityField, result.Image.Size),
            (StageViewId.FinishingTurbulenceFlow, _) =>
                RenderSignedDensityField(result.TurbulenceDensityField, result.Image.Size),
            (StageViewId.FinishingCombinedPigment, _) => result.DensityAdjusted,
            (StageViewId.FinishingToneCurve, _) => result.ToneAdjusted,
            (StageViewId.FinishingPaperGranulation, VisualizationMode.PaperHeightGrayscale) =>
                result.PaperHeightMap is not null
                    ? result.PaperHeightMap.ToGrayscaleRgb8()
                    : new SequentialMatrix<Rgb8>(result.Image.Size),
            (StageViewId.FinishingPaperGranulation, _) => result.Granulated,
            (StageViewId.FinishingFinalFxaa, _) => result.Image,
            _ => throw new ArgumentOutOfRangeException(
                nameof(view),
                (view, mode),
                $"Unsupported stage export combination ({view}, {mode}).")
        };

    private static Matrix<Rgb8> RenderQualifyingBoundaries(WatercolorResult result)
    {
        Matrix<Rgb8> canvas = result.LodAbstracted.Clone();
        IReadOnlyList<RegionBoundary> boundaries = result.SegmentationGraph.Boundaries;
        IReadOnlyList<bool> qualifying = result.WetInWet.QualifyingBoundaries;
        int count = Math.Min(boundaries.Count, qualifying.Count);

        // Paint rejected boundaries first so shared border pixels prioritize qualified cyan highlights.
        for (int pass = 0; pass < 2; pass++)
        {
            bool targetQualified = pass == 1;
            Rgb8 color = targetQualified ? QualifiedBoundaryColor : RejectedBoundaryColor;

            for (int i = 0; i < count; i++)
            {
                if (qualifying[i] != targetQualified)
                {
                    continue;
                }

                RegionBoundary boundary = boundaries[i];
                PaintCoords(canvas, boundary.FirstBorderPixels.Span, color);
                PaintCoords(canvas, boundary.SecondBorderPixels.Span, color);
            }
        }

        return canvas;
    }

    private static void PaintCoords(Matrix<Rgb8> target, ReadOnlySpan<Coord> coords, Rgb8 color)
    {
        for (int i = 0; i < coords.Length; i++)
        {
            Coord coord = coords[i];
            target[coord.X, coord.Y] = color;
        }
    }

    private static Matrix<Rgb8> RenderNonNegativeDensityField(
        IReadOnlyMatrix<DoublePixel>? field,
        Size size)
    {
        var output = new SequentialMatrix<Rgb8>(size);
        if (field is null || field.Count == 0)
        {
            return output;
        }

        double maxValue = 0.0;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                double val = field[x, y].Value;
                if (double.IsFinite(val) && val > maxValue)
                {
                    maxValue = val;
                }
            }
        }

        if (maxValue <= 0.0)
        {
            return output;
        }

        double invMax = 1.0 / maxValue;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                double val = field[x, y].Value;
                double normalized = double.IsFinite(val) && val > 0.0 ? val * invMax : 0.0;
                byte channel = Rgb8.ClampRoundedByte(normalized * 255.0);
                output[x, y] = new Rgb8(channel, channel, channel);
            }
        }

        return output;
    }

    private static Matrix<Rgb8> RenderSignedDensityField(
        IReadOnlyMatrix<DoublePixel>? field,
        Size size)
    {
        var output = new SequentialMatrix<Rgb8>(size);
        if (field is null || field.Count == 0)
        {
            output.AsSpan().Fill(NeutralSignedDensityColor);
            return output;
        }

        double maxAbs = 0.0;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                double absVal = Math.Abs(field[x, y].Value);
                if (double.IsFinite(absVal) && absVal > maxAbs)
                {
                    maxAbs = absVal;
                }
            }
        }

        if (maxAbs <= 0.0)
        {
            output.AsSpan().Fill(NeutralSignedDensityColor);
            return output;
        }

        double invScale = 0.5 / maxAbs;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                double val = field[x, y].Value;
                double normalized = double.IsFinite(val) ? 0.5 + (val * invScale) : 0.5;
                byte channel = Rgb8.ClampRoundedByte(normalized * 255.0);
                output[x, y] = new Rgb8(channel, channel, channel);
            }
        }

        return output;
    }
}
