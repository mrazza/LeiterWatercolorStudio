namespace LeiterWatercolorStudio.Core.Models;

/// <summary>
/// Identifies the primary user-facing stages in the watercolor pipeline.
/// </summary>
public enum PrimaryStageId
{
    Input,
    ColorTransfer,
    Saliency,
    Segmentation,
    LodAbstraction,
    WetInWet,
    HandTremor,
    Finishing,
}
