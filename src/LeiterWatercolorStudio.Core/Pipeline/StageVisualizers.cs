using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Renders diagnostic and artistic visualization images for every <see cref="StageViewId"/>
/// and <see cref="VisualizationMode"/> combination.
/// </summary>
public static class StageVisualizers
{
    private static readonly Rgb8[] TurboStops =
    [
        new(48, 18, 59),
        new(69, 91, 205),
        new(62, 155, 254),
        new(24, 215, 203),
        new(70, 248, 132),
        new(164, 252, 60),
        new(238, 207, 58),
        new(251, 128, 34),
        new(122, 4, 3),
    ];

    private static readonly Rgb8[] ViridisStops =
    [
        new(68, 1, 84),
        new(59, 82, 139),
        new(33, 145, 140),
        new(94, 201, 98),
        new(253, 231, 37),
    ];

    private static readonly Rgb8[] DivergingStops =
    [
        new(33, 102, 172),
        new(146, 197, 222),
        new(245, 245, 245),
        new(244, 165, 130),
        new(178, 24, 43),
    ];

    /// <summary>
    /// Renders <paramref name="view"/> under <paramref name="mode"/> from <paramref name="source"/> and <paramref name="result"/>.
    /// </summary>
    public static Matrix<Rgb8> RenderView(
        StageViewId view,
        VisualizationMode mode,
        IReadOnlyMatrix<Rgb8> source,
        WatercolorResult? result,
        WatercolorOptions? options = null,
        int randomSeed = 42)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!PipelineStageRegistry.IsValidMode(view, mode))
        {
            throw new ArgumentException($"Visualization mode '{mode}' is not valid for stage view '{view}'.", nameof(mode));
        }

        if (source.Count == 0)
        {
            return new SequentialMatrix<Rgb8>(source.Size);
        }

        if (view == StageViewId.Input)
        {
            return source.Clone();
        }

        ArgumentNullException.ThrowIfNull(result);
        WatercolorOptions resolvedOptions = options ?? WatercolorOptions.Default;

        return view switch
        {
            StageViewId.ColorTransfer => result.ColorTransferred.Clone(),
            StageViewId.SaliencyPreBlur => result.EgbiPreBlurred.Clone(),
            StageViewId.SaliencyEgbiRegions => RenderRegions(
                result.ColorTransferred,
                result.EgbiGraph,
                result.SalientMask,
                resolvedOptions.LodAbstraction.SalientOverlapThreshold,
                mode,
                randomSeed),
            StageViewId.SaliencyRcMap => RenderRcSaliencyMap(result.SaliencyMap, result.ColorTransferred, mode),
            StageViewId.SaliencyCutMask => RenderCutMask(result.SalientMask, result.ColorTransferred, mode),
            StageViewId.SaliencyDistanceField => RenderDistanceField(result.SaliencyDistanceField, result.RawSaliencyDistanceField, mode),
            StageViewId.SegmentationMeanShiftFiltered => result.MeanShiftFiltered.Clone(),
            StageViewId.SegmentationMeanShiftRegions => RenderRegions(
                result.MeanShiftFiltered,
                result.SegmentationGraph,
                result.SalientMask,
                resolvedOptions.LodAbstraction.SalientOverlapThreshold,
                mode,
                randomSeed),
            StageViewId.LodAbstraction => result.LodAbstracted.Clone(),
            StageViewId.WetInWetImage => result.WetInWet.Image.Clone(),
            StageViewId.WetInWetAreaMask => RenderWetAreaMask(result.WetInWet.WetAreaMask, result.LodAbstracted, mode),
            StageViewId.WetInWetQualifyingBoundaries => RenderQualifyingBoundaries(result.LodAbstracted, result.SegmentationGraph, result.WetInWet.QualifyingBoundaries),
            StageViewId.HandTremor => result.HandTremor.Clone(),
            StageViewId.FinishingEdgeDarkening => RenderDensityField(result.HandTremor, result.EdgeDensityField, mode, isSigned: false),
            StageViewId.FinishingTurbulenceFlow => RenderDensityField(result.HandTremor, result.TurbulenceDensityField, mode, isSigned: true),
            StageViewId.FinishingCombinedPigment => result.DensityAdjusted.Clone(),
            StageViewId.FinishingToneCurve => result.ToneAdjusted.Clone(),
            StageViewId.FinishingPaperGranulation => RenderPaperGranulation(result.Granulated, result.PaperHeightMap, mode),
            StageViewId.FinishingFinalFxaa => RenderFinalFxaa(result.Image, result.Granulated, mode),
            _ => throw new ArgumentOutOfRangeException(nameof(view), view, "Unknown stage view identifier."),
        };
    }

    private static Matrix<Rgb8> RenderRegions(
        IReadOnlyMatrix<Rgb8> colorBasis,
        RegionGraph graph,
        MaskMatrix salientMask,
        double salientOverlapThreshold,
        VisualizationMode mode,
        int randomSeed)
    {
        switch (mode)
        {
            case VisualizationMode.RegionAverageColor:
                return SegmentationVisualizer.ColorRegions(colorBasis, graph, SegmentationVisualizer.AverageColorAssigner());

            case VisualizationMode.RegionRandomColor:
                return SegmentationVisualizer.ColorRegions(colorBasis, graph, randomSeed);

            case VisualizationMode.RegionBoundariesOverlay:
            {
                Matrix<Rgb8> output = colorBasis.Clone();
                var boundaryColor = new Rgb8(255, 220, 40);
                for (int i = 0; i < graph.Boundaries.Count; i++)
                {
                    RegionBoundary boundary = graph.Boundaries[i];
                    PaintBoundaryPixels(output, colorBasis, boundary.FirstBorderPixels.Span, boundaryColor, 0.85);
                    PaintBoundaryPixels(output, colorBasis, boundary.SecondBorderPixels.Span, boundaryColor, 0.85);
                }

                return output;
            }

            case VisualizationMode.SalientRegionClassification:
            {
                double[] fractions = graph.ComputeRegionFractions(salientMask);
                var isSalient = new bool[graph.RegionCount];
                for (int i = 0; i < isSalient.Length; i++)
                {
                    isSalient[i] = fractions[i] >= salientOverlapThreshold;
                }

                var salientTint = new Rgb8(30, 230, 120);
                var nonSalientTint = new Rgb8(50, 70, 160);

                var output = new SequentialMatrix<Rgb8>(colorBasis.Size);
                for (int y = 0; y < colorBasis.Height; y++)
                {
                    for (int x = 0; x < colorBasis.Width; x++)
                    {
                        int regionId = graph.Labels[x, y];
                        Rgb8 baseColor = colorBasis[x, y];
                        output[x, y] = isSalient[regionId]
                            ? Rgb8.Lerp(baseColor, salientTint, 0.45)
                            : Rgb8.Lerp(baseColor, nonSalientTint, 0.50);
                    }
                }

                return output;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported region visualization mode.");
        }
    }

    private static Matrix<Rgb8> RenderRcSaliencyMap(
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        IReadOnlyMatrix<Rgb8> colorTransferred,
        VisualizationMode mode) =>
        mode switch
        {
            VisualizationMode.Grayscale => saliencyMap.ToGrayscaleRgb8(),
            VisualizationMode.HeatmapTurbo => MapScalarField(saliencyMap, static v => SampleStops(TurboStops, ScalarMath.ClampUnitOrDefault(v))),
            VisualizationMode.HeatmapViridis => MapScalarField(saliencyMap, static v => SampleStops(ViridisStops, ScalarMath.ClampUnitOrDefault(v))),
            VisualizationMode.MaskedColorOverlay => RenderMaskedColorOverlay(colorTransferred, saliencyMap),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported RC saliency visualization mode."),
        };

    private static Matrix<Rgb8> RenderMaskedColorOverlay(
        IReadOnlyMatrix<Rgb8> baseImage,
        IReadOnlyMatrix<DoublePixel> saliencyMap)
    {
        var darkBackdrop = new Rgb8(18, 22, 30);
        var output = new SequentialMatrix<Rgb8>(baseImage.Size);
        for (int y = 0; y < baseImage.Height; y++)
        {
            for (int x = 0; x < baseImage.Width; x++)
            {
                double s = ScalarMath.ClampUnitOrDefault(saliencyMap[x, y].Value);
                output[x, y] = Rgb8.Lerp(darkBackdrop, baseImage[x, y], 0.15 + (0.85 * s));
            }
        }

        return output;
    }

    private static Matrix<Rgb8> RenderCutMask(
        MaskMatrix salientMask,
        IReadOnlyMatrix<Rgb8> colorTransferred,
        VisualizationMode mode)
    {
        switch (mode)
        {
            case VisualizationMode.BinaryMask:
                return salientMask.ToGrayscaleRgb8();

            case VisualizationMode.TranslucentTintOverlay:
            {
                var fgTint = new Rgb8(0, 240, 150);
                var bgTint = new Rgb8(170, 28, 48);
                var output = new SequentialMatrix<Rgb8>(colorTransferred.Size);
                for (int y = 0; y < colorTransferred.Height; y++)
                {
                    for (int x = 0; x < colorTransferred.Width; x++)
                    {
                        Rgb8 px = colorTransferred[x, y];
                        output[x, y] = salientMask[x, y]
                            ? Rgb8.Lerp(px, fgTint, 0.25)
                            : Rgb8.Lerp(px, bgTint, 0.55);
                    }
                }

                return output;
            }

            case VisualizationMode.ContourOverlay:
            {
                Matrix<Rgb8> output = colorTransferred.Clone();
                var contourColor = new Rgb8(0, 255, 180);
                int width = salientMask.Width;
                int height = salientMask.Height;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!salientMask[x, y])
                        {
                            continue;
                        }

                        bool isBorder =
                            x == 0 || x == width - 1 || y == 0 || y == height - 1 ||
                            !salientMask[x - 1, y] || !salientMask[x + 1, y] ||
                            !salientMask[x, y - 1] || !salientMask[x, y + 1];

                        if (isBorder)
                        {
                            output[x, y] = contourColor;
                        }
                    }
                }

                return output;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported SaliencyCut visualization mode.");
        }
    }

    private static Matrix<Rgb8> RenderDistanceField(
        IReadOnlyMatrix<DoublePixel> smoothed,
        IReadOnlyMatrix<DoublePixel> raw,
        VisualizationMode mode) =>
        mode switch
        {
            VisualizationMode.DistanceSmoothedGrayscale => smoothed.ToGrayscaleRgb8(),
            VisualizationMode.DistanceSmoothedHeatmap => MapScalarField(smoothed, static v => SampleStops(TurboStops, ScalarMath.ClampUnitOrDefault(v))),
            VisualizationMode.DistanceSmoothedContours => RenderDistanceContours(smoothed),
            VisualizationMode.DistanceRawGrayscale => raw.ToGrayscaleRgb8(),
            VisualizationMode.DistanceRawHeatmap => MapScalarField(raw, static v => SampleStops(TurboStops, ScalarMath.ClampUnitOrDefault(v))),
            VisualizationMode.DistanceRawContours => RenderDistanceContours(raw),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported distance field visualization mode."),
        };

    private static Matrix<Rgb8> RenderDistanceContours(IReadOnlyMatrix<DoublePixel> field)
    {
        const double contourStep = 0.1;
        int width = field.Width;
        int height = field.Height;
        var output = new SequentialMatrix<Rgb8>(field.Size);
        var contourLineColor = new Rgb8(250, 250, 250);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double val = ScalarMath.ClampUnitOrDefault(field[x, y].Value);
                int band = (int)Math.Floor(val / contourStep);

                bool isContour = false;
                if (x + 1 < width)
                {
                    int rightBand = (int)Math.Floor(ScalarMath.ClampUnitOrDefault(field[x + 1, y].Value) / contourStep);
                    if (rightBand != band)
                    {
                        isContour = true;
                    }
                }

                if (!isContour && y + 1 < height)
                {
                    int bottomBand = (int)Math.Floor(ScalarMath.ClampUnitOrDefault(field[x, y + 1].Value) / contourStep);
                    if (bottomBand != band)
                    {
                        isContour = true;
                    }
                }

                output[x, y] = isContour
                    ? contourLineColor
                    : SampleStops(TurboStops, val);
            }
        }

        return output;
    }

    private static Matrix<Rgb8> RenderWetAreaMask(
        MaskMatrix wetAreaMask,
        IReadOnlyMatrix<Rgb8> lodAbstracted,
        VisualizationMode mode)
    {
        switch (mode)
        {
            case VisualizationMode.WetMaskBinary:
                return wetAreaMask.ToGrayscaleRgb8();

            case VisualizationMode.WetMaskOverlay:
            {
                var wetTint = new Rgb8(0, 210, 255);
                var dryBackdrop = new Rgb8(20, 22, 30);
                var output = new SequentialMatrix<Rgb8>(lodAbstracted.Size);
                for (int y = 0; y < lodAbstracted.Height; y++)
                {
                    for (int x = 0; x < lodAbstracted.Width; x++)
                    {
                        Rgb8 px = lodAbstracted[x, y];
                        output[x, y] = wetAreaMask[x, y]
                            ? Rgb8.Lerp(px, wetTint, 0.50)
                            : Rgb8.Lerp(px, dryBackdrop, 0.35);
                    }
                }

                return output;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported wet mask visualization mode.");
        }
    }

    private static Matrix<Rgb8> RenderQualifyingBoundaries(
        IReadOnlyMatrix<Rgb8> lodAbstracted,
        RegionGraph segmentationGraph,
        IReadOnlyList<bool> qualifyingBoundaries)
    {
        Matrix<Rgb8> output = lodAbstracted.Clone();
        var qualifiedColor = new Rgb8(0, 255, 255);
        var rejectedColor = new Rgb8(170, 48, 48);

        for (int pass = 0; pass < 2; pass++)
        {
            bool targetQualified = pass == 1;
            Rgb8 color = targetQualified ? qualifiedColor : rejectedColor;

            for (int i = 0; i < segmentationGraph.Boundaries.Count; i++)
            {
                bool isQualified = i < qualifyingBoundaries.Count && qualifyingBoundaries[i];
                if (isQualified != targetQualified)
                {
                    continue;
                }

                RegionBoundary boundary = segmentationGraph.Boundaries[i];
                PaintSolidPixels(output, boundary.FirstBorderPixels.Span, color);
                PaintSolidPixels(output, boundary.SecondBorderPixels.Span, color);
            }
        }

        return output;
    }

    private static Matrix<Rgb8> RenderDensityField(
        IReadOnlyMatrix<Rgb8> handTremor,
        IReadOnlyMatrix<DoublePixel>? densityField,
        VisualizationMode mode,
        bool isSigned)
    {
        switch (mode)
        {
            case VisualizationMode.IsolatedPigmentPreview:
                return densityField is null
                    ? handTremor.Clone()
                    : PigmentBlend.ApplyDensityField(handTremor, densityField);

            case VisualizationMode.DensityHeatmap:
                if (densityField is null)
                {
                    Rgb8 neutralColor = isSigned ? SampleStops(DivergingStops, 0.5) : TurboStops[0];
                    return CreateSolidImage(handTremor.Size, neutralColor);
                }

                if (isSigned)
                {
                    double maxAbs = ComputeMaxAbsolute(densityField);
                    if (maxAbs <= 1e-12)
                    {
                        return CreateSolidImage(handTremor.Size, SampleStops(DivergingStops, 0.5));
                    }

                    return MapScalarField(
                        densityField,
                        v => SampleStops(DivergingStops, Math.Clamp(0.5 + (0.5 * (v / maxAbs)), 0.0, 1.0)));
                }
                else
                {
                    double maxVal = ComputeMaxNonNegative(densityField);
                    if (maxVal <= 1e-12)
                    {
                        return CreateSolidImage(handTremor.Size, TurboStops[0]);
                    }

                    return MapScalarField(
                        densityField,
                        v => SampleStops(TurboStops, ScalarMath.ClampUnitOrDefault(v / maxVal)));
                }

            case VisualizationMode.DensityGrayscale:
                if (densityField is null)
                {
                    Rgb8 neutralGray = isSigned ? new Rgb8(128, 128, 128) : new Rgb8(0, 0, 0);
                    return CreateSolidImage(handTremor.Size, neutralGray);
                }

                if (isSigned)
                {
                    double maxAbs = ComputeMaxAbsolute(densityField);
                    if (maxAbs <= 1e-12)
                    {
                        return CreateSolidImage(handTremor.Size, new Rgb8(128, 128, 128));
                    }

                    return MapScalarField(densityField, v =>
                    {
                        double norm = Math.Clamp(0.5 + (0.5 * (v / maxAbs)), 0.0, 1.0);
                        byte g = Rgb8.ClampRoundedByte(norm * 255.0);
                        return new Rgb8(g, g, g);
                    });
                }
                else
                {
                    double maxVal = ComputeMaxNonNegative(densityField);
                    if (maxVal <= 1e-12)
                    {
                        return CreateSolidImage(handTremor.Size, Rgb8.Zero);
                    }

                    return MapScalarField(densityField, v =>
                    {
                        double norm = ScalarMath.ClampUnitOrDefault(v / maxVal);
                        byte g = Rgb8.ClampRoundedByte(norm * 255.0);
                        return new Rgb8(g, g, g);
                    });
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported density visualization mode.");
        }
    }

    private static Matrix<Rgb8> RenderPaperGranulation(
        IReadOnlyMatrix<Rgb8> granulated,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap,
        VisualizationMode mode)
    {
        switch (mode)
        {
            case VisualizationMode.Rgb:
                return granulated.Clone();

            case VisualizationMode.PaperHeightGrayscale:
                return paperHeightMap is null
                    ? CreateSolidImage(granulated.Size, new Rgb8(128, 128, 128))
                    : paperHeightMap.ToGrayscaleRgb8();

            case VisualizationMode.PaperHeightRelief:
                if (paperHeightMap is null)
                {
                    return CreateSolidImage(granulated.Size, new Rgb8(180, 180, 180));
                }

                return RenderPaperHillshade(paperHeightMap);

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported paper granulation visualization mode.");
        }
    }

    private static Matrix<Rgb8> RenderPaperHillshade(IReadOnlyMatrix<DoublePixel> heightMap)
    {
        const double zFactor = 6.0;
        const double invNorm = 1.0 / 2.0615528128088303; // sqrt(1 + 1 + 1.5^2)
        const double lx = -1.0 * invNorm;
        const double ly = -1.0 * invNorm;
        const double lz = 1.5 * invNorm;

        int width = heightMap.Width;
        int height = heightMap.Height;
        var output = new SequentialMatrix<Rgb8>(heightMap.Size);

        for (int y = 0; y < height; y++)
        {
            int ym = Math.Max(0, y - 1);
            int yp = Math.Min(height - 1, y + 1);

            for (int x = 0; x < width; x++)
            {
                int xm = Math.Max(0, x - 1);
                int xp = Math.Min(width - 1, x + 1);

                double dhdx = (heightMap[xp, y].Value - heightMap[xm, y].Value) * 0.5 * zFactor;
                double dhdy = (heightMap[x, yp].Value - heightMap[x, ym].Value) * 0.5 * zFactor;

                double nx = -dhdx;
                double ny = -dhdy;
                double nz = 1.0;
                double nLen = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));

                double dot = Math.Clamp(((nx * lx) + (ny * ly) + (nz * lz)) / nLen, 0.0, 1.0);
                double shade = 0.25 + (0.75 * dot);
                output[x, y] = new Rgb8(
                    Rgb8.ClampRoundedByte(shade * 248.0),
                    Rgb8.ClampRoundedByte(shade * 245.0),
                    Rgb8.ClampRoundedByte(shade * 238.0));
            }
        }

        return output;
    }

    private static Matrix<Rgb8> RenderFinalFxaa(
        IReadOnlyMatrix<Rgb8> finalImage,
        IReadOnlyMatrix<Rgb8> granulated,
        VisualizationMode mode)
    {
        if (mode == VisualizationMode.Rgb)
        {
            return finalImage.Clone();
        }

        if (mode != VisualizationMode.FxaaDiffMagnifier)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported FXAA visualization mode.");
        }

        var output = new SequentialMatrix<Rgb8>(finalImage.Size);
        for (int y = 0; y < finalImage.Height; y++)
        {
            for (int x = 0; x < finalImage.Width; x++)
            {
                Rgb8 a = finalImage[x, y];
                Rgb8 b = granulated[x, y];
                output[x, y] = new Rgb8(
                    (byte)Math.Min(255, Math.Abs(a.R - b.R) * 16),
                    (byte)Math.Min(255, Math.Abs(a.G - b.G) * 16),
                    (byte)Math.Min(255, Math.Abs(a.B - b.B) * 16));
            }
        }

        return output;
    }

    private static Matrix<Rgb8> MapScalarField(
        IReadOnlyMatrix<DoublePixel> field,
        Func<double, Rgb8> mapper)
    {
        var output = new SequentialMatrix<Rgb8>(field.Size);
        for (int y = 0; y < field.Height; y++)
        {
            for (int x = 0; x < field.Width; x++)
            {
                output[x, y] = mapper(field[x, y].Value);
            }
        }

        return output;
    }

    private static Matrix<Rgb8> CreateSolidImage(Size size, Rgb8 color)
    {
        var output = new SequentialMatrix<Rgb8>(size);
        output.AsSpan().Fill(color);
        return output;
    }

    private static void PaintBoundaryPixels(
        Matrix<Rgb8> output,
        IReadOnlyMatrix<Rgb8> background,
        ReadOnlySpan<Coord> pixels,
        Rgb8 color,
        double alpha)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            Coord c = pixels[i];
            output[c.X, c.Y] = Rgb8.Lerp(background[c.X, c.Y], color, alpha);
        }
    }

    private static void PaintSolidPixels(
        Matrix<Rgb8> output,
        ReadOnlySpan<Coord> pixels,
        Rgb8 color)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            Coord c = pixels[i];
            output[c.X, c.Y] = color;
        }
    }

    private static double ComputeMaxNonNegative(IReadOnlyMatrix<DoublePixel> field)
    {
        double max = 0.0;
        for (int y = 0; y < field.Height; y++)
        {
            for (int x = 0; x < field.Width; x++)
            {
                double v = field[x, y].Value;
                if (double.IsFinite(v) && v > max)
                {
                    max = v;
                }
            }
        }

        return max;
    }

    private static double ComputeMaxAbsolute(IReadOnlyMatrix<DoublePixel> field)
    {
        double maxAbs = 0.0;
        for (int y = 0; y < field.Height; y++)
        {
            for (int x = 0; x < field.Width; x++)
            {
                double v = field[x, y].Value;
                if (double.IsFinite(v))
                {
                    double abs = Math.Abs(v);
                    if (abs > maxAbs)
                    {
                        maxAbs = abs;
                    }
                }
            }
        }

        return maxAbs;
    }

    private static Rgb8 SampleStops(Rgb8[] stops, double t)
    {
        double clamped = Math.Clamp(t, 0.0, 1.0);
        double scaled = clamped * (stops.Length - 1);
        int idx = (int)scaled;
        if (idx >= stops.Length - 1)
        {
            return stops[^1];
        }

        double frac = scaled - idx;
        return Rgb8.Lerp(stops[idx], stops[idx + 1], frac);
    }
}
