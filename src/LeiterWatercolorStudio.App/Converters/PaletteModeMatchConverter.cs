using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts a <see cref="PaletteMode"/> value into a boolean matching a configured target mode.
/// </summary>
public sealed class PaletteModeMatchConverter : IValueConverter
{
    /// <summary>
    /// Matches <see cref="PaletteMode.BuiltIn"/>.
    /// </summary>
    public static PaletteModeMatchConverter BuiltIn { get; } = new(PaletteMode.BuiltIn, invert: false);

    /// <summary>
    /// Matches <see cref="PaletteMode.Custom"/>.
    /// </summary>
    public static PaletteModeMatchConverter Custom { get; } = new(PaletteMode.Custom, invert: false);

    /// <summary>
    /// Matches any mode other than <see cref="PaletteMode.Custom"/>.
    /// </summary>
    public static PaletteModeMatchConverter NotCustom { get; } = new(PaletteMode.Custom, invert: true);

    private readonly PaletteMode _target;
    private readonly bool _invert;

    private PaletteModeMatchConverter(PaletteMode target, bool invert)
    {
        _target = target;
        _invert = invert;
    }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PaletteMode mode)
        {
            bool match = mode == _target;
            return _invert ? !match : match;
        }

        return AvaloniaProperty.UnsetValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
