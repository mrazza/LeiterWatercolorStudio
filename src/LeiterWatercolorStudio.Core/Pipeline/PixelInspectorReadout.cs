using System.Globalization;
using System.Text;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Captures per-pixel colorimetric, segmentation, saliency, wet-in-wet, and finishing diagnostics
/// for interactive viewport inspection.
/// </summary>
public readonly record struct PixelInspectorReadout(
    int X,
    int Y,
    StageViewId ActiveView,
    Rgb8 Rgb,
    Rgb8 InputRgb,
    Lab32 Lab,
    Lab32 InputLab,
    double DeltaEFromInput,
    int? RegionId,
    int? RegionArea,
    double? RegionSalientFraction,
    double? Saliency,
    bool? IsSalient,
    double? DistanceSmoothed,
    double? DistanceRaw,
    bool? IsWetArea,
    int? RegionQualifyingBoundaryCount,
    int? RegionTotalBoundaryCount,
    int? QualifyingBoundaryCount,
    int? TotalBoundaryCount,
    double? EdgeDensity,
    double? TurbulenceOffset,
    double? PaperHeight,
    Rgb8? PreToneRgb,
    Rgb8? PostToneRgb)
{
    /// <summary>
    /// Samples full diagnostic pixel data at (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public static PixelInspectorReadout Sample(
        int x,
        int y,
        StageViewId activeView,
        IReadOnlyMatrix<Rgb8> source,
        WatercolorResult? result,
        IReadOnlyMatrix<Rgb8>? displayedImage = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(activeView))
        {
            throw new ArgumentOutOfRangeException(nameof(activeView), activeView, "Unknown stage view identifier.");
        }

        if (x < 0 || x >= source.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, $"X coordinate {x} is outside [0, {source.Width}).");
        }

        if (y < 0 || y >= source.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Y coordinate {y} is outside [0, {source.Height}).");
        }

        Rgb8 inputRgb = source[x, y];
        Rgb8 sampledRgb = ResolveSampledRgb(x, y, activeView, source, result, displayedImage);

        Lab32 inputLab = inputRgb.ToLab32(RgbColorSpace.SRgb);
        Lab32 sampledLab = sampledRgb.ToLab32(RgbColorSpace.SRgb);
        double deltaE = sampledRgb == inputRgb ? 0.0 : sampledLab.DeltaE2000(inputLab);

        if (result is null)
        {
            return new PixelInspectorReadout(
                X: x,
                Y: y,
                ActiveView: activeView,
                Rgb: sampledRgb,
                InputRgb: inputRgb,
                Lab: sampledLab,
                InputLab: inputLab,
                DeltaEFromInput: deltaE,
                RegionId: null,
                RegionArea: null,
                RegionSalientFraction: null,
                Saliency: null,
                IsSalient: null,
                DistanceSmoothed: null,
                DistanceRaw: null,
                IsWetArea: null,
                RegionQualifyingBoundaryCount: null,
                RegionTotalBoundaryCount: null,
                QualifyingBoundaryCount: null,
                TotalBoundaryCount: null,
                EdgeDensity: null,
                TurbulenceOffset: null,
                PaperHeight: null,
                PreToneRgb: null,
                PostToneRgb: null);
        }

        RegionGraph activeGraph = activeView == StageViewId.SaliencyEgbiRegions
            ? result.EgbiGraph
            : result.SegmentationGraph;

        int? regionId = null;
        int? regionArea = null;
        double? regionSalientFraction = null;

        if (activeGraph.Width == source.Width && activeGraph.Height == source.Height && activeGraph.RegionCount > 0)
        {
            int rid = activeGraph.Labels[x, y];
            Region<Coord> region = activeGraph.Regions[rid];
            regionId = rid;
            regionArea = region.Pixels.Count;
            if (region.Pixels.Count > 0 && result.SalientMask.Size == activeGraph.Size)
            {
                int salientCount = 0;
                foreach (Coord pixel in region.Pixels)
                {
                    if (result.SalientMask[pixel])
                    {
                        salientCount++;
                    }
                }

                regionSalientFraction = (double)salientCount / region.Pixels.Count;
            }
            else
            {
                regionSalientFraction = 0.0;
            }
        }

        int? regionQualifying = null;
        int? regionTotal = null;
        int? globalQualifying = null;
        int? globalTotal = null;

        if (result.SegmentationGraph.Size == source.Size)
        {
            IReadOnlyList<bool> qualifyingFlags = result.WetInWet.QualifyingBoundaries;
            int totalB = result.SegmentationGraph.Boundaries.Count;
            int qualB = 0;
            for (int i = 0; i < totalB && i < qualifyingFlags.Count; i++)
            {
                if (qualifyingFlags[i])
                {
                    qualB++;
                }
            }

            globalTotal = totalB;
            globalQualifying = qualB;

            if (result.SegmentationGraph.RegionCount > 0)
            {
                int segRegionId = result.SegmentationGraph.Labels[x, y];
                ReadOnlySpan<int> incident = result.SegmentationGraph.GetBoundaryIndices(segRegionId);
                int rTotal = incident.Length;
                int rQual = 0;
                for (int i = 0; i < incident.Length; i++)
                {
                    int bIdx = incident[i];
                    if (bIdx < qualifyingFlags.Count && qualifyingFlags[bIdx])
                    {
                        rQual++;
                    }
                }

                regionTotal = rTotal;
                regionQualifying = rQual;
            }
        }

        return new PixelInspectorReadout(
            X: x,
            Y: y,
            ActiveView: activeView,
            Rgb: sampledRgb,
            InputRgb: inputRgb,
            Lab: sampledLab,
            InputLab: inputLab,
            DeltaEFromInput: deltaE,
            RegionId: regionId,
            RegionArea: regionArea,
            RegionSalientFraction: regionSalientFraction,
            Saliency: result.SaliencyMap[x, y].Value,
            IsSalient: result.SalientMask[x, y],
            DistanceSmoothed: result.SaliencyDistanceField[x, y].Value,
            DistanceRaw: result.RawSaliencyDistanceField[x, y].Value,
            IsWetArea: result.WetInWet.WetAreaMask[x, y],
            RegionQualifyingBoundaryCount: regionQualifying,
            RegionTotalBoundaryCount: regionTotal,
            QualifyingBoundaryCount: globalQualifying,
            TotalBoundaryCount: globalTotal,
            EdgeDensity: result.EdgeDensityField?[x, y].Value,
            TurbulenceOffset: result.TurbulenceDensityField?[x, y].Value,
            PaperHeight: result.PaperHeightMap?[x, y].Value,
            PreToneRgb: result.DensityAdjusted[x, y],
            PostToneRgb: result.ToneAdjusted[x, y]);
    }

    /// <summary>
    /// Attempts to sample pixel diagnostics at (<paramref name="x"/>, <paramref name="y"/>), returning <see langword="false"/> if out of bounds.
    /// </summary>
    public static bool TrySample(
        int x,
        int y,
        StageViewId activeView,
        IReadOnlyMatrix<Rgb8> source,
        WatercolorResult? result,
        out PixelInspectorReadout readout,
        IReadOnlyMatrix<Rgb8>? displayedImage = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (x < 0 || x >= source.Width || y < 0 || y >= source.Height)
        {
            readout = default;
            return false;
        }

        readout = Sample(x, y, activeView, source, result, displayedImage);
        return true;
    }

    /// <summary>
    /// Formats a concise multi-line HUD text readout suitable for status bars and inspector overlays.
    /// </summary>
    public string FormatHudText()
    {
        var sb = new StringBuilder(256);
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "({0}, {1}) RGB({2}, {3}, {4}) Lab({5:F1}, {6:F1}, {7:F1}) ΔE00:{8:F2}",
            X,
            Y,
            Rgb.R,
            Rgb.G,
            Rgb.B,
            Lab.L,
            Lab.A,
            Lab.B,
            DeltaEFromInput);

        if (RegionId is int rid)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "\nRegion #{0} Area:{1}px Salient:{2:P0}",
                rid,
                RegionArea ?? 0,
                RegionSalientFraction ?? 0.0);
        }

        if (Saliency is double sal && IsSalient is bool isSal)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "\nSaliency:{0:F3} ({1}) Dist:{2:F3} (Raw:{3:F3})",
                sal,
                isSal ? "Salient" : "Background",
                DistanceSmoothed ?? 0.0,
                DistanceRaw ?? 0.0);
        }

        if (IsWetArea is bool wet)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "\nWet:{0} Boundaries:{1}/{2} region ({3}/{4} total)",
                wet ? "Yes" : "No",
                RegionQualifyingBoundaryCount ?? 0,
                RegionTotalBoundaryCount ?? 0,
                QualifyingBoundaryCount ?? 0,
                TotalBoundaryCount ?? 0);
        }

        if (PreToneRgb is Rgb8 preTone && PostToneRgb is Rgb8 postTone)
        {
            sb.AppendFormat(
                CultureInfo.InvariantCulture,
                "\nEdge:{0} Turb:{1} Paper:{2} Tone:({3},{4},{5})->({6},{7},{8})",
                EdgeDensity is double ed ? ed.ToString("F3", CultureInfo.InvariantCulture) : "off",
                TurbulenceOffset is double td ? td.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) : "off",
                PaperHeight is double ph ? ph.ToString("F3", CultureInfo.InvariantCulture) : "off",
                preTone.R,
                preTone.G,
                preTone.B,
                postTone.R,
                postTone.G,
                postTone.B);
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public override string ToString() => FormatHudText();

    private static Rgb8 ResolveSampledRgb(
        int x,
        int y,
        StageViewId activeView,
        IReadOnlyMatrix<Rgb8> source,
        WatercolorResult? result,
        IReadOnlyMatrix<Rgb8>? displayedImage)
    {
        if (displayedImage is not null && x < displayedImage.Width && y < displayedImage.Height)
        {
            return displayedImage[x, y];
        }

        if (result is null || activeView == StageViewId.Input)
        {
            return source[x, y];
        }

        return activeView switch
        {
            StageViewId.ColorTransfer => result.ColorTransferred[x, y],
            StageViewId.SaliencyPreBlur => result.EgbiPreBlurred[x, y],
            StageViewId.SaliencyEgbiRegions => result.ColorTransferred[x, y],
            StageViewId.SaliencyRcMap => result.ColorTransferred[x, y],
            StageViewId.SaliencyCutMask => result.ColorTransferred[x, y],
            StageViewId.SaliencyDistanceField => result.ColorTransferred[x, y],
            StageViewId.SegmentationMeanShiftFiltered => result.MeanShiftFiltered[x, y],
            StageViewId.SegmentationMeanShiftRegions => result.MeanShiftFiltered[x, y],
            StageViewId.LodAbstraction => result.LodAbstracted[x, y],
            StageViewId.WetInWetImage => result.WetInWet.Image[x, y],
            StageViewId.WetInWetAreaMask => result.LodAbstracted[x, y],
            StageViewId.WetInWetQualifyingBoundaries => result.LodAbstracted[x, y],
            StageViewId.HandTremor => result.HandTremor[x, y],
            StageViewId.FinishingEdgeDarkening => result.HandTremor[x, y],
            StageViewId.FinishingTurbulenceFlow => result.HandTremor[x, y],
            StageViewId.FinishingCombinedPigment => result.DensityAdjusted[x, y],
            StageViewId.FinishingToneCurve => result.ToneAdjusted[x, y],
            StageViewId.FinishingPaperGranulation => result.Granulated[x, y],
            StageViewId.FinishingFinalFxaa => result.Image[x, y],
            _ => source[x, y],
        };
    }
}
