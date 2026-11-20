using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Pipeline.Events;

/// <summary>
/// Event payload raised when a preview pipeline execution completes successfully.
/// </summary>
public sealed record RenderCompletedEventArgs(
    long RequestId,
    WatercolorResult Result,
    WatercolorOptions CanonicalOptions,
    WatercolorOptions EffectiveOptions,
    TimeSpan TotalElapsed);
