using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts <see cref="VisualizationMode"/> values into clean user-facing labels.
/// </summary>
public sealed class VisualizationModeNameConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="VisualizationModeNameConverter"/>.
    /// </summary>
    public static VisualizationModeNameConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || ReferenceEquals(value, AvaloniaProperty.UnsetValue))
        {
            return string.Empty;
        }

        if (value is VisualizationMode mode)
        {
            return Enum.IsDefined(mode)
                ? PipelineStageRegistry.GetVisualizationModeDisplayName(mode)
                : EnumDisplayNameConverter.FormatEnum(mode);
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
