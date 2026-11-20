namespace LeiterWatercolorStudio.Core.Presets;

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Models;

/// <summary>
/// Versioned JSON preset document capturing a complete watercolor pipeline configuration and optional calibration metadata.
/// </summary>
public sealed record PresetDocument
{
    /// <summary>
    /// The supported preset JSON schema version.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Gets the schema version of this preset document.
    /// </summary>
    [JsonRequired]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>
    /// Gets the human-readable preset name.
    /// </summary>
    public string Name { get; init; } = "Untitled Preset";

    /// <summary>
    /// Gets the geometric reference dimension (<c>sqrt(Width * Height)</c>) at which <see cref="Options"/> were calibrated,
    /// or <c>0</c> if uncalibrated.
    /// </summary>
    public double CalibrationReferenceDimension { get; init; } = WatercolorOptions.DefaultReferenceDimension;

    /// <summary>
    /// Gets the palette selection mode associated with this preset.
    /// </summary>
    public PaletteMode PaletteMode { get; init; } = PaletteMode.Auto;

    /// <summary>
    /// Gets the canonical watercolor pipeline configuration.
    /// </summary>
    public WatercolorOptions Options { get; init; } = WatercolorOptions.Default;

    /// <summary>
    /// Gets optional per-step execution durations in milliseconds, typically recorded when exporting stage diagnostics.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<WatercolorStep, double>? StepTimingsMs { get; init; }
}
