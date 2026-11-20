using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Leiter.Algorithms.Color;
using Leiter.Pixels;
using LeiterWatercolorStudio.Core.Color;

namespace LeiterWatercolorStudio.App.Converters;

/// <summary>
/// Converts a <see cref="ColorTransferProfile"/> into five representative sRGB swatches via <see cref="PaletteExtractor.SynthesizeSwatches"/>.
/// </summary>
public sealed class ProfileToSwatchesConverter : IValueConverter
{
    /// <summary>
    /// Gets a shared singleton instance of <see cref="ProfileToSwatchesConverter"/>.
    /// </summary>
    public static ProfileToSwatchesConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ColorTransferProfile profile)
        {
            return PaletteExtractor.SynthesizeSwatches(profile, 5);
        }

        return Array.Empty<Rgb8>();
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
