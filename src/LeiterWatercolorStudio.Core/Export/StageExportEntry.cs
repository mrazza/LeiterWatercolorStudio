using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Export;

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
