namespace LeiterWatercolorStudio.Core.Cli;

/// <summary>
/// Describes a primary stage group alias and the ordered stage keys it expands to.
/// </summary>
/// <param name="Alias">Normalized group alias (e.g. <c>saliency</c>, <c>wet_in_wet</c>).</param>
/// <param name="DisplayName">Human-readable primary stage display name.</param>
/// <param name="StageKeys">Ordered manifest stage keys belonging to this group.</param>
public sealed record StageGroupDescriptor(
    string Alias,
    string DisplayName,
    IReadOnlyList<string> StageKeys);
