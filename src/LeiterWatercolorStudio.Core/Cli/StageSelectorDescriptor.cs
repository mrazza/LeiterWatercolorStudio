using LeiterWatercolorStudio.Core.Export;

namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Describes a selectable diagnostic stage export entry and its CLI aliases.
/// </summary>
/// <param name="Key">Canonical manifest filename stem (e.g. <c>05_hand_tremor</c>).</param>
/// <param name="ShortAlias">Canonical short alias (e.g. <c>hand_tremor</c>).</param>
/// <param name="GroupAlias">Primary stage group alias (e.g. <c>finishing</c>).</param>
/// <param name="Entry">The underlying <see cref="StageExportEntry"/>.</param>
public sealed record StageSelectorDescriptor(
    string Key,
    string ShortAlias,
    string GroupAlias,
    StageExportEntry Entry);
