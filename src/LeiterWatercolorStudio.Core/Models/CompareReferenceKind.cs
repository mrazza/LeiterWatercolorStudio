namespace LeiterWatercolorStudio.Core.Models;

/// <summary>
/// Selects which reference image is displayed when comparing stage outputs.
/// </summary>
public enum CompareReferenceKind
{
    Input,
    PreviousStage,
    PinnedStage,
}
