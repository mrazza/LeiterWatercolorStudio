using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Describes a user-facing primary stage in the watercolor pipeline.
/// </summary>
/// <param name="StageId">The primary stage identifier.</param>
/// <param name="DisplayName">The human-readable display name.</param>
/// <param name="ShortcutKey">The keyboard shortcut character (<c>'1'</c>–<c>'8'</c>).</param>
/// <param name="ChildViews">The ordered inspectable stage views belonging to this stage.</param>
/// <param name="DefaultView">The default view selected when activating this stage.</param>
/// <param name="MappedSteps">The underlying <see cref="WatercolorStep"/> DAG nodes belonging to this stage.</param>
/// <param name="HasSubStages">Whether this stage exposes multiple inspectable sub-stage views.</param>
public sealed record PipelineStageDescriptor(
    PrimaryStageId StageId,
    string DisplayName,
    char ShortcutKey,
    IReadOnlyList<StageViewId> ChildViews,
    StageViewId DefaultView,
    IReadOnlyList<WatercolorStep> MappedSteps,
    bool HasSubStages);

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
