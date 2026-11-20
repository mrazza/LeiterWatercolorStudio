namespace LeiterWatercolorStudio.Core.Models;

/// <summary>
/// Identifies how a stage output or diagnostic artifact is rendered for inspection.
/// </summary>
public enum VisualizationMode
{
    Rgb,
    RegionAverageColor,
    RegionRandomColor,
    RegionBoundariesOverlay,
    SalientRegionClassification,
    Grayscale,
    HeatmapTurbo,
    HeatmapViridis,
    MaskedColorOverlay,
    BinaryMask,
    TranslucentTintOverlay,
    ContourOverlay,
    DistanceSmoothedGrayscale,
    DistanceSmoothedHeatmap,
    DistanceSmoothedContours,
    DistanceRawGrayscale,
    DistanceRawHeatmap,
    DistanceRawContours,
    WetMaskBinary,
    WetMaskOverlay,
    QualifyingBoundariesOverlay,
    IsolatedPigmentPreview,
    DensityHeatmap,
    DensityGrayscale,
    PaperHeightGrayscale,
    PaperHeightRelief,
    FxaaDiffMagnifier,
}
