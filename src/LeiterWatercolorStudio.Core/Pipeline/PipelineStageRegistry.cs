using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Provides canonical metadata, DAG mappings, visualization modes, and predecessor lookups
/// for all 8 primary pipeline stages and 20 inspectable stage views.
/// </summary>
public static class PipelineStageRegistry
{
    private static readonly IReadOnlyDictionary<PrimaryStageId, PipelineStageDescriptor> StageById;
    private static readonly IReadOnlyDictionary<StageViewId, StageViewDescriptor> ViewById;
    private static readonly IReadOnlyDictionary<WatercolorStep, PrimaryStageId> StageByStep;
    private static readonly IReadOnlyDictionary<StageViewId, StageViewId> PreviousViewById;

    static PipelineStageRegistry()
    {
        PipelineStageDescriptor[] stages =
        [
            new(
                PrimaryStageId.Input,
                "Input",
                '1',
                [StageViewId.Input],
                StageViewId.Input,
                [],
                HasSubStages: false),
            new(
                PrimaryStageId.ColorTransfer,
                "Color Transfer",
                '2',
                [StageViewId.ColorTransfer],
                StageViewId.ColorTransfer,
                [WatercolorStep.ColorTransfer],
                HasSubStages: false),
            new(
                PrimaryStageId.Saliency,
                "Saliency",
                '3',
                [
                    StageViewId.SaliencyPreBlur,
                    StageViewId.SaliencyEgbiRegions,
                    StageViewId.SaliencyRcMap,
                    StageViewId.SaliencyCutMask,
                    StageViewId.SaliencyDistanceField,
                ],
                StageViewId.SaliencyRcMap,
                [
                    WatercolorStep.EgbiPreBlur,
                    WatercolorStep.EgbiSegmentation,
                    WatercolorStep.RegionalContrastSaliency,
                    WatercolorStep.SaliencyCut,
                    WatercolorStep.SaliencyDistanceField,
                ],
                HasSubStages: true),
            new(
                PrimaryStageId.Segmentation,
                "Segmentation",
                '4',
                [
                    StageViewId.SegmentationMeanShiftFiltered,
                    StageViewId.SegmentationMeanShiftRegions,
                ],
                StageViewId.SegmentationMeanShiftRegions,
                [
                    WatercolorStep.MeanShiftFiltering,
                    WatercolorStep.MeanShiftClustering,
                ],
                HasSubStages: true),
            new(
                PrimaryStageId.LodAbstraction,
                "LOD Abstraction",
                '5',
                [StageViewId.LodAbstraction],
                StageViewId.LodAbstraction,
                [WatercolorStep.LodAbstraction],
                HasSubStages: false),
            new(
                PrimaryStageId.WetInWet,
                "Wet-in-Wet",
                '6',
                [
                    StageViewId.WetInWetImage,
                    StageViewId.WetInWetAreaMask,
                    StageViewId.WetInWetQualifyingBoundaries,
                ],
                StageViewId.WetInWetImage,
                [WatercolorStep.WetInWet],
                HasSubStages: true),
            new(
                PrimaryStageId.HandTremor,
                "Hand Tremor",
                '7',
                [StageViewId.HandTremor],
                StageViewId.HandTremor,
                [WatercolorStep.HandTremor],
                HasSubStages: false),
            new(
                PrimaryStageId.Finishing,
                "Finishing",
                '8',
                [
                    StageViewId.FinishingEdgeDarkening,
                    StageViewId.FinishingTurbulenceFlow,
                    StageViewId.FinishingCombinedPigment,
                    StageViewId.FinishingToneCurve,
                    StageViewId.FinishingPaperGranulation,
                    StageViewId.FinishingFinalFxaa,
                ],
                StageViewId.FinishingFinalFxaa,
                [
                    WatercolorStep.EdgeDarkening,
                    WatercolorStep.TurbulenceFlow,
                    WatercolorStep.PigmentCombine,
                    WatercolorStep.ToneCurve,
                    WatercolorStep.PaperHeightMap,
                    WatercolorStep.PaperGranulation,
                    WatercolorStep.Fxaa,
                ],
                HasSubStages: true),
        ];

        VisualizationMode[] rgbOnly = [VisualizationMode.Rgb];
        VisualizationMode[] regionModes =
        [
            VisualizationMode.RegionAverageColor,
            VisualizationMode.RegionRandomColor,
            VisualizationMode.RegionBoundariesOverlay,
            VisualizationMode.SalientRegionClassification,
        ];
        VisualizationMode[] rcMapModes =
        [
            VisualizationMode.HeatmapTurbo,
            VisualizationMode.HeatmapViridis,
            VisualizationMode.Grayscale,
            VisualizationMode.MaskedColorOverlay,
        ];
        VisualizationMode[] cutMaskModes =
        [
            VisualizationMode.BinaryMask,
            VisualizationMode.TranslucentTintOverlay,
            VisualizationMode.ContourOverlay,
        ];
        VisualizationMode[] distanceModes =
        [
            VisualizationMode.DistanceSmoothedHeatmap,
            VisualizationMode.DistanceSmoothedGrayscale,
            VisualizationMode.DistanceSmoothedContours,
            VisualizationMode.DistanceRawHeatmap,
            VisualizationMode.DistanceRawGrayscale,
            VisualizationMode.DistanceRawContours,
        ];
        VisualizationMode[] densityModes =
        [
            VisualizationMode.IsolatedPigmentPreview,
            VisualizationMode.DensityHeatmap,
            VisualizationMode.DensityGrayscale,
        ];

        StageViewDescriptor[] views =
        [
            new(StageViewId.Input, "Input", PrimaryStageId.Input, null, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.ColorTransfer, "Color Transfer", PrimaryStageId.ColorTransfer, WatercolorStep.ColorTransfer, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.SaliencyPreBlur, "EGBI Pre-Blur", PrimaryStageId.Saliency, WatercolorStep.EgbiPreBlur, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.SaliencyEgbiRegions, "EGBI Regions", PrimaryStageId.Saliency, WatercolorStep.EgbiSegmentation, regionModes, VisualizationMode.RegionAverageColor),
            new(StageViewId.SaliencyRcMap, "RC Saliency Map", PrimaryStageId.Saliency, WatercolorStep.RegionalContrastSaliency, rcMapModes, VisualizationMode.HeatmapTurbo),
            new(StageViewId.SaliencyCutMask, "SaliencyCut Mask", PrimaryStageId.Saliency, WatercolorStep.SaliencyCut, cutMaskModes, VisualizationMode.BinaryMask),
            new(StageViewId.SaliencyDistanceField, "Distance Field", PrimaryStageId.Saliency, WatercolorStep.SaliencyDistanceField, distanceModes, VisualizationMode.DistanceSmoothedHeatmap),
            new(StageViewId.SegmentationMeanShiftFiltered, "Mean-Shift Filtered", PrimaryStageId.Segmentation, WatercolorStep.MeanShiftFiltering, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.SegmentationMeanShiftRegions, "Mean-Shift Regions", PrimaryStageId.Segmentation, WatercolorStep.MeanShiftClustering, regionModes, VisualizationMode.RegionAverageColor),
            new(StageViewId.LodAbstraction, "LOD Abstraction", PrimaryStageId.LodAbstraction, WatercolorStep.LodAbstraction, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.WetInWetImage, "Wet-in-Wet Image", PrimaryStageId.WetInWet, WatercolorStep.WetInWet, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.WetInWetAreaMask, "Wet Area Mask", PrimaryStageId.WetInWet, WatercolorStep.WetInWet, [VisualizationMode.WetMaskOverlay, VisualizationMode.WetMaskBinary], VisualizationMode.WetMaskOverlay),
            new(StageViewId.WetInWetQualifyingBoundaries, "Qualifying Boundaries", PrimaryStageId.WetInWet, WatercolorStep.WetInWet, [VisualizationMode.QualifyingBoundariesOverlay], VisualizationMode.QualifyingBoundariesOverlay),
            new(StageViewId.HandTremor, "Hand Tremor", PrimaryStageId.HandTremor, WatercolorStep.HandTremor, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.FinishingEdgeDarkening, "Edge Darkening", PrimaryStageId.Finishing, WatercolorStep.EdgeDarkening, densityModes, VisualizationMode.IsolatedPigmentPreview),
            new(StageViewId.FinishingTurbulenceFlow, "Turbulence Flow", PrimaryStageId.Finishing, WatercolorStep.TurbulenceFlow, densityModes, VisualizationMode.IsolatedPigmentPreview),
            new(StageViewId.FinishingCombinedPigment, "Combined Pigment", PrimaryStageId.Finishing, WatercolorStep.PigmentCombine, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.FinishingToneCurve, "Tone Curve", PrimaryStageId.Finishing, WatercolorStep.ToneCurve, rgbOnly, VisualizationMode.Rgb),
            new(StageViewId.FinishingPaperGranulation, "Paper Granulation", PrimaryStageId.Finishing, WatercolorStep.PaperGranulation, [VisualizationMode.Rgb, VisualizationMode.PaperHeightGrayscale, VisualizationMode.PaperHeightRelief], VisualizationMode.Rgb),
            new(StageViewId.FinishingFinalFxaa, "Final (FXAA)", PrimaryStageId.Finishing, WatercolorStep.Fxaa, [VisualizationMode.Rgb, VisualizationMode.FxaaDiffMagnifier], VisualizationMode.Rgb),
        ];

        Stages = Array.AsReadOnly(stages);
        Views = Array.AsReadOnly(views);

        StageById = stages.ToDictionary(static s => s.StageId);
        ViewById = views.ToDictionary(static v => v.ViewId);

        var stepMap = new Dictionary<WatercolorStep, PrimaryStageId>();
        foreach (PipelineStageDescriptor stage in stages)
        {
            foreach (WatercolorStep step in stage.MappedSteps)
            {
                stepMap.Add(step, stage.StageId);
            }
        }

        StageByStep = stepMap;

        PreviousViewById = new Dictionary<StageViewId, StageViewId>
        {
            [StageViewId.Input] = StageViewId.Input,
            [StageViewId.ColorTransfer] = StageViewId.Input,
            [StageViewId.SaliencyPreBlur] = StageViewId.ColorTransfer,
            [StageViewId.SaliencyEgbiRegions] = StageViewId.SaliencyPreBlur,
            [StageViewId.SaliencyRcMap] = StageViewId.SaliencyEgbiRegions,
            [StageViewId.SaliencyCutMask] = StageViewId.SaliencyRcMap,
            [StageViewId.SaliencyDistanceField] = StageViewId.SaliencyCutMask,
            [StageViewId.SegmentationMeanShiftFiltered] = StageViewId.ColorTransfer,
            [StageViewId.SegmentationMeanShiftRegions] = StageViewId.SegmentationMeanShiftFiltered,
            [StageViewId.LodAbstraction] = StageViewId.SegmentationMeanShiftFiltered,
            [StageViewId.WetInWetImage] = StageViewId.LodAbstraction,
            [StageViewId.WetInWetAreaMask] = StageViewId.LodAbstraction,
            [StageViewId.WetInWetQualifyingBoundaries] = StageViewId.LodAbstraction,
            [StageViewId.HandTremor] = StageViewId.WetInWetImage,
            [StageViewId.FinishingEdgeDarkening] = StageViewId.HandTremor,
            [StageViewId.FinishingTurbulenceFlow] = StageViewId.HandTremor,
            [StageViewId.FinishingCombinedPigment] = StageViewId.HandTremor,
            [StageViewId.FinishingToneCurve] = StageViewId.FinishingCombinedPigment,
            [StageViewId.FinishingPaperGranulation] = StageViewId.FinishingToneCurve,
            [StageViewId.FinishingFinalFxaa] = StageViewId.FinishingPaperGranulation,
        };
    }

    /// <summary>
    /// Gets descriptors for all 8 primary pipeline stages in execution order.
    /// </summary>
    public static IReadOnlyList<PipelineStageDescriptor> Stages { get; }

    /// <summary>
    /// Gets descriptors for all 20 inspectable stage views in pipeline order.
    /// </summary>
    public static IReadOnlyList<StageViewDescriptor> Views { get; }

    /// <summary>
    /// Returns the descriptor for <paramref name="stageId"/>.
    /// </summary>
    public static PipelineStageDescriptor GetStage(PrimaryStageId stageId) =>
        StageById.TryGetValue(stageId, out PipelineStageDescriptor? descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(stageId), stageId, "Unknown primary stage identifier.");

    /// <summary>
    /// Returns the descriptor for <paramref name="viewId"/>.
    /// </summary>
    public static StageViewDescriptor GetView(StageViewId viewId) =>
        ViewById.TryGetValue(viewId, out StageViewDescriptor? descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(viewId), viewId, "Unknown stage view identifier.");

    /// <summary>
    /// Returns the <see cref="PrimaryStageId"/> owning <paramref name="step"/>.
    /// </summary>
    public static PrimaryStageId GetStageForStep(WatercolorStep step) =>
        StageByStep.TryGetValue(step, out PrimaryStageId stageId)
            ? stageId
            : throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown watercolor step.");

    /// <summary>
    /// Returns the immediate upstream predecessor view for A/B PreviousStage comparison.
    /// </summary>
    public static StageViewId GetPreviousView(StageViewId viewId) =>
        PreviousViewById.TryGetValue(viewId, out StageViewId previousView)
            ? previousView
            : throw new ArgumentOutOfRangeException(nameof(viewId), viewId, "Unknown stage view identifier.");

    /// <summary>
    /// Returns whether <paramref name="mode"/> is valid for <paramref name="viewId"/>.
    /// </summary>
    public static bool IsValidMode(StageViewId viewId, VisualizationMode mode)
    {
        StageViewDescriptor descriptor = GetView(viewId);
        for (int i = 0; i < descriptor.ValidModes.Count; i++)
        {
            if (descriptor.ValidModes[i] == mode)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns a human-readable display label for <paramref name="mode"/>.
    /// </summary>
    public static string GetVisualizationModeDisplayName(VisualizationMode mode) => mode switch
    {
        VisualizationMode.Rgb => "RGB",
        VisualizationMode.RegionAverageColor => "Region Avg Color",
        VisualizationMode.RegionRandomColor => "Region Random Color",
        VisualizationMode.RegionBoundariesOverlay => "Region Boundaries",
        VisualizationMode.SalientRegionClassification => "Salient Classification",
        VisualizationMode.Grayscale => "Grayscale",
        VisualizationMode.HeatmapTurbo => "Heatmap (Turbo)",
        VisualizationMode.HeatmapViridis => "Heatmap (Viridis)",
        VisualizationMode.MaskedColorOverlay => "Masked Color Overlay",
        VisualizationMode.BinaryMask => "Binary Mask",
        VisualizationMode.TranslucentTintOverlay => "Tint Overlay",
        VisualizationMode.ContourOverlay => "Contour Overlay",
        VisualizationMode.DistanceSmoothedGrayscale => "Smoothed Grayscale",
        VisualizationMode.DistanceSmoothedHeatmap => "Smoothed Heatmap",
        VisualizationMode.DistanceSmoothedContours => "Smoothed Contours",
        VisualizationMode.DistanceRawGrayscale => "Raw Grayscale",
        VisualizationMode.DistanceRawHeatmap => "Raw Heatmap",
        VisualizationMode.DistanceRawContours => "Raw Contours",
        VisualizationMode.WetMaskBinary => "Wet Mask Binary",
        VisualizationMode.WetMaskOverlay => "Wet Mask Overlay",
        VisualizationMode.QualifyingBoundariesOverlay => "Qualifying Boundaries",
        VisualizationMode.IsolatedPigmentPreview => "Isolated Pigment",
        VisualizationMode.DensityHeatmap => "Density Heatmap",
        VisualizationMode.DensityGrayscale => "Density Grayscale",
        VisualizationMode.PaperHeightGrayscale => "Paper Height",
        VisualizationMode.PaperHeightRelief => "Paper Relief",
        VisualizationMode.FxaaDiffMagnifier => "FXAA Diff (16x)",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown visualization mode."),
    };
}
