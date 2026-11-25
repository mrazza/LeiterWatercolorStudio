namespace LeiterWatercolorStudio.Core.Presets;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using LeiterWatercolorStudio.Core.Presets.Converters;

/// <summary>
/// Serializes, deserializes, validates, and resolution-adapts <see cref="PresetDocument"/> files.
/// </summary>
public static class PresetSerializer
{
    /// <summary>
    /// The default relative linear-dimension difference threshold (<c>5%</c>) above which a preset is auto-scaled to match a target image.
    /// </summary>
    public const double DefaultScalePromptThreshold = 0.05;

    /// <summary>
    /// Gets the shared <see cref="JsonSerializerOptions"/> configured for preset serialization.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    /// <summary>
    /// Serializes <paramref name="preset"/> to formatted camelCase JSON after validating schema version and option invariants.
    /// </summary>
    /// <param name="preset">The preset document to serialize.</param>
    /// <returns>A formatted JSON representation of <paramref name="preset"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="preset"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when <paramref name="preset"/> has an unsupported schema version or missing options.</exception>
    /// <exception cref="ArgumentException">Thrown when any preset option or profile invariant fails validation.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any numeric or enum value is outside its valid domain.</exception>
    public static string Serialize(PresetDocument preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ValidateDocument(preset);
        return JsonSerializer.Serialize(preset, SerializerOptions);
    }

    /// <summary>
    /// Deserializes and validates a <see cref="PresetDocument"/> from JSON.
    /// </summary>
    /// <param name="json">The preset JSON text.</param>
    /// <returns>The validated <see cref="PresetDocument"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is empty/whitespace or any option invariant is invalid.</exception>
    /// <exception cref="JsonException">Thrown when the JSON syntax or converter representation is malformed.</exception>
    /// <exception cref="InvalidDataException">Thrown when schema version is unsupported or required document sections are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any numeric or enum value is outside its valid domain.</exception>
    public static PresetDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        PresetDocument? document = JsonSerializer.Deserialize<PresetDocument>(json, SerializerOptions);
        if (document is null)
        {
            throw new InvalidDataException("Preset JSON deserialized to null.");
        }

        ValidateDocument(document);
        return document;
    }

    /// <summary>
    /// Validates and writes <paramref name="preset"/> asynchronously to <paramref name="filePath"/> as UTF-8 JSON.
    /// </summary>
    /// <param name="preset">The preset document to save.</param>
    /// <param name="filePath">The destination JSON file path.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public static async Task SaveToFileAsync(
        PresetDocument preset,
        string filePath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ct.ThrowIfCancellationRequested();

        string json = Serialize(preset);
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads, deserializes, and validates a <see cref="PresetDocument"/> asynchronously from <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">The source JSON file path.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>The validated <see cref="PresetDocument"/>.</returns>
    public static async Task<PresetDocument> LoadFromFileAsync(
        string filePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct).ConfigureAwait(false);
        return Deserialize(json);
    }

    /// <summary>
    /// Adapts a preset's <see cref="WatercolorOptions"/> to <paramref name="targetSize"/>, optionally scaling geometric parameters
    /// relative to <see cref="PresetDocument.CalibrationReferenceDimension"/>.
    /// </summary>
    /// <param name="preset">The preset document containing canonical options and calibration dimension.</param>
    /// <param name="targetSize">The target image dimensions.</param>
    /// <param name="scaleToSize">Whether to scale spatial/frequency parameters when <see cref="PresetDocument.CalibrationReferenceDimension"/> is positive.</param>
    /// <returns>
    /// Geometrically scaled <see cref="WatercolorOptions"/> when <paramref name="scaleToSize"/> is <see langword="true"/> and
    /// <see cref="PresetDocument.CalibrationReferenceDimension"/> is finite and positive; otherwise <paramref name="preset"/>.<see cref="PresetDocument.Options"/>.
    /// </returns>
    public static WatercolorOptions AdaptForImageSize(
        PresetDocument preset,
        Size targetSize,
        bool scaleToSize)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentOutOfRangeException.ThrowIfNegative(targetSize.Width, nameof(targetSize));
        ArgumentOutOfRangeException.ThrowIfNegative(targetSize.Height, nameof(targetSize));

        if (preset.Options is null)
        {
            throw new InvalidDataException("Preset 'options' cannot be null.");
        }

        preset.Options.Validate();

        if (scaleToSize &&
            double.IsFinite(preset.CalibrationReferenceDimension) &&
            preset.CalibrationReferenceDimension > 0.0)
        {
            return WatercolorOptions.ForSize(targetSize, preset.Options, preset.CalibrationReferenceDimension);
        }

        return preset.Options;
    }

    /// <summary>
    /// Determines whether a preset's calibration reference dimension differs enough from <paramref name="targetSize"/>
    /// to warrant resolution-adaptive scaling.
    /// </summary>
    /// <param name="preset">The preset document containing calibration metadata.</param>
    /// <param name="targetSize">The target image dimensions.</param>
    /// <param name="relativeThreshold">Relative linear-dimension difference threshold (defaults to <see cref="DefaultScalePromptThreshold"/>).</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="targetSize"/> is non-empty,
    /// <see cref="PresetDocument.CalibrationReferenceDimension"/> is finite and positive, and the relative linear-dimension difference
    /// exceeds <paramref name="relativeThreshold"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool ShouldAutoScaleForImageSize(
        PresetDocument preset,
        Size targetSize,
        double relativeThreshold = DefaultScalePromptThreshold)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentOutOfRangeException.ThrowIfNegative(targetSize.Width, nameof(targetSize));
        ArgumentOutOfRangeException.ThrowIfNegative(targetSize.Height, nameof(targetSize));

        if (!double.IsFinite(relativeThreshold) || relativeThreshold < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(relativeThreshold),
                relativeThreshold,
                "Relative threshold must be finite and non-negative.");
        }

        if (targetSize.Width == 0 ||
            targetSize.Height == 0 ||
            !double.IsFinite(preset.CalibrationReferenceDimension) ||
            preset.CalibrationReferenceDimension <= 0.0)
        {
            return false;
        }

        double currentDim = targetSize.LinearDimension;
        double relativeDiff = Math.Abs(currentDim - preset.CalibrationReferenceDimension) /
                              Math.Max(1.0, preset.CalibrationReferenceDimension);

        return relativeDiff > relativeThreshold;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new Rgb8JsonConverter());
        options.Converters.Add(new ColorTransferProfileJsonConverter());
        options.Converters.Add(new HandTremorOptionsJsonConverter());
        return options;
    }

    private static void ValidateDocument(PresetDocument document)
    {
        if (document.SchemaVersion != PresetDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported preset schema version {document.SchemaVersion}. Expected {PresetDocument.CurrentSchemaVersion}.");
        }

        if (document.Name is null)
        {
            throw new InvalidDataException("Preset 'name' cannot be null.");
        }

        if (!double.IsFinite(document.CalibrationReferenceDimension) || document.CalibrationReferenceDimension < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(document.CalibrationReferenceDimension),
                document.CalibrationReferenceDimension,
                "CalibrationReferenceDimension must be finite and non-negative.");
        }

        if (!Enum.IsDefined(document.PaletteMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(document.PaletteMode),
                document.PaletteMode,
                $"Unsupported {nameof(document.PaletteMode)} value.");
        }

        if (document.Options is null)
        {
            throw new InvalidDataException("Preset 'options' cannot be null.");
        }

        document.Options.Validate();

        if (document.StepTimingsMs is not null)
        {
            foreach (var (step, elapsedMs) in document.StepTimingsMs)
            {
                if (!Enum.IsDefined(step))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(document.StepTimingsMs),
                        step,
                        $"Unsupported {nameof(WatercolorStep)} key.");
                }

                if (!double.IsFinite(elapsedMs) || elapsedMs < 0.0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(document.StepTimingsMs),
                        elapsedMs,
                        $"Step timing for {step} must be finite and non-negative.");
                }
            }
        }
    }
}
