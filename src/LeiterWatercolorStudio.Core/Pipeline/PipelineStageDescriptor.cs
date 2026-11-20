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
