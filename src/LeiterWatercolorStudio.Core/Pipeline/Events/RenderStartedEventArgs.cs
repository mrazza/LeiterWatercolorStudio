using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;

namespace LeiterWatercolorStudio.Core.Pipeline.Events;

/// <summary>
/// Event payload raised when a preview pipeline execution starts.
/// </summary>
public sealed record RenderStartedEventArgs(
    long RequestId,
    WatercolorOptions CanonicalOptions,
    WatercolorOptions EffectiveOptions,
    Size PreviewSize);
