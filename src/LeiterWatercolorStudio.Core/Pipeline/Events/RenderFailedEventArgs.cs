using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Pipeline.Events;

/// <summary>
/// Event payload raised when a preview pipeline execution fails with an unhandled exception.
/// </summary>
public sealed record RenderFailedEventArgs(
    long RequestId,
    WatercolorOptions CanonicalOptions,
    Exception Exception);
