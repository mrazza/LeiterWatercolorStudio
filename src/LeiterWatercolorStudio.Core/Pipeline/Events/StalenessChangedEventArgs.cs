using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline.Events;

/// <summary>
/// Event payload raised when DAG staleness changes relative to the last rendered options.
/// </summary>
public sealed record StalenessChangedEventArgs(
    IReadOnlySet<WatercolorStep> StaleSteps,
    IReadOnlySet<PrimaryStageId> StaleStages);
