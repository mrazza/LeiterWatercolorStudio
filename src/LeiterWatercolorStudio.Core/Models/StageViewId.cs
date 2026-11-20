namespace LeiterWatercolorStudio.Core.Models;

/// <summary>
/// Identifies a specific inspectable intermediate or final stage view in the watercolor pipeline.
/// </summary>
public enum StageViewId
{
    Input,
    ColorTransfer,
    SaliencyPreBlur,
    SaliencyEgbiRegions,
    SaliencyRcMap,
    SaliencyCutMask,
    SaliencyDistanceField,
    SegmentationMeanShiftFiltered,
    SegmentationMeanShiftRegions,
    LodAbstraction,
    WetInWetImage,
    WetInWetAreaMask,
    WetInWetQualifyingBoundaries,
    HandTremor,
    FinishingEdgeDarkening,
    FinishingTurbulenceFlow,
    FinishingCombinedPigment,
    FinishingToneCurve,
    FinishingPaperGranulation,
    FinishingFinalFxaa,
}
