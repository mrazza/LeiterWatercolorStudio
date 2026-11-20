namespace LeiterWatercolorStudio.Core.Models;

/// <summary>
/// Caps the maximum dimension used for interactive preview rendering.
/// </summary>
public enum PreviewResolutionCap
{
    Px512 = 512,
    Px768 = 768,
    Px1024 = 1024,
    Px1536 = 1536,
    Native = 0,
}
