using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Pipeline.Events;

/// <summary>
/// Event payload raised when a pending or active preview pipeline execution is cancelled.
/// </summary>
public sealed record RenderCancelledEventArgs(
    long RequestId,
    WatercolorOptions CanonicalOptions,
    bool WasStarted);
