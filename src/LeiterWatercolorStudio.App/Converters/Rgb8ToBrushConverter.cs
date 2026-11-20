using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Leiter.Pixels;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts between Leiter <see cref="Rgb8"/> values and Avalonia <see cref="IBrush"/> or <see cref="Color"/> values.
/// </summary>
public sealed class Rgb8ToBrushConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="Rgb8ToBrushConverter"/>.
    /// </summary>
    public static Rgb8ToBrushConverter Instance { get; } = new();

    /// <summary>
    /// Converts an <see cref="Rgb8"/> to an Avalonia <see cref="Color"/>.
    /// </summary>
    public static Color ToAvaloniaColor(Rgb8 rgb) => Color.FromRgb(rgb.R, rgb.G, rgb.B);

    /// <summary>
    /// Converts an Avalonia <see cref="Color"/> to an <see cref="Rgb8"/>.
    /// </summary>
    public static Rgb8 ToRgb8(Color color) => new(color.R, color.G, color.B);

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || ReferenceEquals(value, AvaloniaProperty.UnsetValue))
        {
            return null;
        }

        if (value is Rgb8 rgb)
        {
            Color color = ToAvaloniaColor(rgb);
            return IsColorTarget(targetType) ? color : new SolidColorBrush(color);
        }

        if (value is Color avaloniaColor)
        {
            if (IsRgb8Target(targetType))
            {
                return ToRgb8(avaloniaColor);
            }

            return IsColorTarget(targetType) ? avaloniaColor : new SolidColorBrush(avaloniaColor);
        }

        if (value is ISolidColorBrush brush)
        {
            if (IsRgb8Target(targetType))
            {
                return ToRgb8(brush.Color);
            }

            return IsColorTarget(targetType) ? brush.Color : brush;
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ISolidColorBrush brush)
        {
            return IsColorTarget(targetType) ? brush.Color : ToRgb8(brush.Color);
        }

        if (value is Color color)
        {
            return IsBrushTarget(targetType) ? new SolidColorBrush(color) : ToRgb8(color);
        }

        if (value is Rgb8 rgb)
        {
            Color avaloniaColor = ToAvaloniaColor(rgb);
            if (IsColorTarget(targetType))
            {
                return avaloniaColor;
            }

            return IsBrushTarget(targetType) ? new SolidColorBrush(avaloniaColor) : rgb;
        }

        return BindingOperations.DoNothing;
    }

    private static bool IsColorTarget(Type targetType) =>
        targetType == typeof(Color) || targetType == typeof(Color?);

    private static bool IsRgb8Target(Type targetType) =>
        targetType == typeof(Rgb8) || targetType == typeof(Rgb8?);

    private static bool IsBrushTarget(Type targetType) =>
        typeof(IBrush).IsAssignableFrom(targetType);
}
