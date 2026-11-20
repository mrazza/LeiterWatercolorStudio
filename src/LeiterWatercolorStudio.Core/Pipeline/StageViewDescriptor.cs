using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Describes an inspectable intermediate or final stage view in the watercolor pipeline.
/// </summary>
/// <param name="ViewId">The stage view identifier.</param>
/// <param name="DisplayName">The human-readable display name.</param>
/// <param name="ParentStage">The owning primary stage identifier.</param>
/// <param name="MappedStep">The underlying <see cref="WatercolorStep"/> producing this view's data, or <see langword="null"/> for the input view.</param>
/// <param name="ValidModes">The supported visualization modes for this view.</param>
/// <param name="DefaultMode">The default visualization mode for this view.</param>
public sealed record StageViewDescriptor(
    StageViewId ViewId,
    string DisplayName,
    PrimaryStageId ParentStage,
    WatercolorStep? MappedStep,
    IReadOnlyList<VisualizationMode> ValidModes,
    VisualizationMode DefaultMode);
