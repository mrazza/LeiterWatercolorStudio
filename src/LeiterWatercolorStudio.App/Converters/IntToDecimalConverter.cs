using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts between <see cref="int"/> view-model properties and <see cref="decimal"/> numeric input values for compiled bindings.
/// </summary>
public sealed class IntToDecimalConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="IntToDecimalConverter"/>.
    /// </summary>
    public static IntToDecimalConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return (decimal?)intValue;
        }

        if (value is decimal decimalValue)
        {
            return (decimal?)decimalValue;
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            decimal rounded = decimal.Round(decimalValue, 0, MidpointRounding.AwayFromZero);
            return (int)Math.Clamp(rounded, int.MinValue, int.MaxValue);
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return BindingOperations.DoNothing;
    }
}
