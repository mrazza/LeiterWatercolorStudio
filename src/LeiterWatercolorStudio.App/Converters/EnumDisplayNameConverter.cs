using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts enum values into friendly, human-readable display strings with proper spacing.
/// </summary>
public sealed class EnumDisplayNameConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="EnumDisplayNameConverter"/>.
    /// </summary>
    public static EnumDisplayNameConverter Instance { get; } = new();

    /// <summary>
    /// Formats an <see cref="Enum"/> value into a human-readable label.
    /// </summary>
    public static string FormatEnum(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            VisualizationMode vm when Enum.IsDefined(vm) =>
                PipelineStageRegistry.GetVisualizationModeDisplayName(vm),
            PrimaryStageId stage when Enum.IsDefined(stage) =>
                PipelineStageRegistry.GetStage(stage).DisplayName,
            StageViewId view when Enum.IsDefined(view) =>
                PipelineStageRegistry.GetView(view).DisplayName,
            PreviewResolutionCap cap => cap switch
            {
                PreviewResolutionCap.Native => "Native",
                PreviewResolutionCap.Px512 => "512 px",
                PreviewResolutionCap.Px768 => "768 px",
                PreviewResolutionCap.Px1024 => "1024 px",
                PreviewResolutionCap.Px1536 => "1536 px",
                _ => SplitPascalCase(cap.ToString()),
            },
            PaletteMode.BuiltIn => "Built-In",
            CompareMode.SideBySide => "Side by Side",
            _ => SplitPascalCase(value.ToString()),
        };
    }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || ReferenceEquals(value, AvaloniaProperty.UnsetValue))
        {
            return string.Empty;
        }

        if (value is Enum enumValue)
        {
            return FormatEnum(enumValue);
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;

    private static string SplitPascalCase(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length + 6);
        for (int i = 0; i < raw.Length; i++)
        {
            char current = raw[i];
            if (current == '_')
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            if (i > 0 && raw[i - 1] != '_')
            {
                char prev = raw[i - 1];
                bool prevIsLower = char.IsLower(prev);
                bool prevIsDigit = char.IsDigit(prev);
                bool currIsUpper = char.IsUpper(current);
                bool currIsDigit = char.IsDigit(current);
                bool nextIsLower = i + 1 < raw.Length && char.IsLower(raw[i + 1]);

                bool isBoundary =
                    (currIsUpper && (prevIsLower || prevIsDigit || (char.IsUpper(prev) && nextIsLower))) ||
                    (currIsDigit && !prevIsDigit);

                if (isBoundary && builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
