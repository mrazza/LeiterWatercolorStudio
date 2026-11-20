using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts between <see cref="byte"/> color channel properties and <see cref="double"/> slider values for compiled bindings.
/// </summary>
public sealed class ByteToDoubleConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="ByteToDoubleConverter"/>.
    /// </summary>
    public static ByteToDoubleConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is byte byteValue)
        {
            return (double)byteValue;
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
            return (byte)Math.Clamp(rounded, byte.MinValue, byte.MaxValue);
        }

        if (value is byte byteValue)
        {
            return byteValue;
        }

        return BindingOperations.DoNothing;
    }
}
