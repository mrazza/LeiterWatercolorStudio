using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts a boolean value into a configurable opacity value (defaults to <c>1.0</c> when true and <c>0.45</c> when false).
/// </summary>
public sealed class BoolToOpacityConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="BoolToOpacityConverter"/>.
    /// </summary>
    public static BoolToOpacityConverter Instance { get; } = new();

    /// <summary>
    /// Gets or sets the opacity returned when the input evaluates to <see langword="true"/>.
    /// </summary>
    public double TrueOpacity { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the opacity returned when the input evaluates to <see langword="false"/>.
    /// </summary>
    public double FalseOpacity { get; set; } = 0.45;

    /// <summary>
    /// Gets or sets whether the boolean input is inverted before mapping to opacity.
    /// </summary>
    public bool Invert { get; set; }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (ReferenceEquals(value, AvaloniaProperty.UnsetValue))
        {
            return AvaloniaProperty.UnsetValue;
        }

        if (value is null)
        {
            return Math.Clamp(Invert ? TrueOpacity : FalseOpacity, 0.0, 1.0);
        }

        if (value is not bool flag)
        {
            return AvaloniaProperty.UnsetValue;
        }

        bool invert = Invert;
        double falseOpacity = FalseOpacity;

        if (parameter is string paramStr && !string.IsNullOrWhiteSpace(paramStr))
        {
            if (string.Equals(paramStr.Trim(), "invert", StringComparison.OrdinalIgnoreCase))
            {
                invert = !invert;
            }
            else if (double.TryParse(paramStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double customFalse)
                     && double.IsFinite(customFalse))
            {
                falseOpacity = customFalse;
            }
        }
        else if (parameter is double numericParam && double.IsFinite(numericParam))
        {
            falseOpacity = numericParam;
        }

        bool effective = invert ? !flag : flag;
        double result = effective ? TrueOpacity : falseOpacity;
        return Math.Clamp(result, 0.0, 1.0);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double opacity || !double.IsFinite(opacity))
        {
            return BindingOperations.DoNothing;
        }

        double midpoint = (TrueOpacity + FalseOpacity) * 0.5;
        bool isTrue = TrueOpacity >= FalseOpacity
            ? opacity >= midpoint
            : opacity <= midpoint;

        bool invert = Invert;
        if (parameter is string paramStr && string.Equals(paramStr.Trim(), "invert", StringComparison.OrdinalIgnoreCase))
        {
            invert = !invert;
        }

        return invert ? !isTrue : isTrue;
    }
}
