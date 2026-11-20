using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts between <see cref="int"/> view-model properties and <see cref="double"/> slider values for compiled bindings.
/// </summary>
public sealed class IntToDoubleConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="IntToDoubleConverter"/>.
    /// </summary>
    public static IntToDoubleConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return (double)intValue;
        }

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue && double.IsFinite(doubleValue))
        {
            double rounded = Math.Round(doubleValue, MidpointRounding.AwayFromZero);
            return (int)Math.Clamp(rounded, int.MinValue, int.MaxValue);
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return BindingOperations.DoNothing;
    }
}
