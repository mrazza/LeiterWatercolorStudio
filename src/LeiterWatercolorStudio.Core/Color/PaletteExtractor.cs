namespace LeiterWatercolorStudio.Core.Color;

using System;
using System.IO;
using Leiter.Algorithms.Color;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Platform.ImageSharp;

/// <summary>
/// Extracts CIELAB <see cref="ColorTransferProfile"/> definitions from reference images and synthesizes representative RGB swatches.
/// </summary>
public static class PaletteExtractor
{
    /// <summary>
    /// Loads an image from <paramref name="imagePath"/> and computes its CIELAB <see cref="ColorTransferProfile"/> under <see cref="Illuminant.E"/>.
    /// </summary>
    /// <param name="imagePath">Path to the reference image file.</param>
    /// <param name="profileName">Optional palette name; defaults to the file stem when omitted or whitespace.</param>
    /// <returns>A validated <see cref="ColorTransferProfile"/> describing the image's CIELAB color distribution.</returns>
    public static ColorTransferProfile ExtractFromFile(string imagePath, string? profileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        SequentialMatrix<Rgb8> image = ImageExtensions.LoadRgb8(imagePath);
        string resolvedName = string.IsNullOrWhiteSpace(profileName)
            ? Path.GetFileNameWithoutExtension(imagePath)
            : profileName;

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            resolvedName = "Custom Palette";
        }

        return ExtractFromMatrix(image, resolvedName);
    }

    /// <summary>
    /// Computes a validated CIELAB <see cref="ColorTransferProfile"/> under <see cref="Illuminant.E"/> from an RGB image matrix.
    /// </summary>
    /// <param name="image">The source sRGB image matrix.</param>
    /// <param name="profileName">The name assigned to the extracted profile.</param>
    /// <returns>A validated <see cref="ColorTransferProfile"/>.</returns>
    public static ColorTransferProfile ExtractFromMatrix(IReadOnlyMatrix<Rgb8> image, string profileName)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        LabColorStatistics statistics = ColorTransfer.ComputeStatistics(image, RgbColorSpace.SRgb, Illuminant.E);
        var profile = new ColorTransferProfile(profileName, statistics, Illuminant.E);
        profile.Validate();
        return profile;
    }

    /// <summary>
    /// Synthesizes <paramref name="count"/> representative sRGB color swatches spanning the profile's CIELAB mean and standard-deviation spread.
    /// </summary>
    /// <param name="profile">The CIELAB color transfer profile to sample.</param>
    /// <param name="count">The number of swatches to synthesize (default 5; must be at least 1).</param>
    /// <returns>An array of <paramref name="count"/> <see cref="Rgb8"/> swatches ordered from darker to lighter.</returns>
    public static Rgb8[] SynthesizeSwatches(ColorTransferProfile profile, int count = 5)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        LabColorStatistics stats = profile.Statistics;
        var swatches = new Rgb8[count];

        if (count == 1)
        {
            double clampedL = Math.Clamp(stats.MeanL, 0.0, 100.0);
            swatches[0] = new Lab32((float)clampedL, (float)stats.MeanA, (float)stats.MeanB)
                .ToRgb8(RgbColorSpace.SRgb, profile.Illuminant);
            return swatches;
        }

        for (int i = 0; i < count; i++)
        {
            double t = -1.0 + (2.0 * i / (count - 1));
            double l = Math.Clamp(stats.MeanL + (t * stats.StdDevL), 0.0, 100.0);
            // Endpoints cover +/-StdDev along both chromatic axes, while interior samples modulate a* orthogonally to reveal hue spread.
            double aScale = (i == 0 || i == count - 1) ? t : Math.Sin(t * Math.PI);
            double a = stats.MeanA + (aScale * stats.StdDevA);
            double b = stats.MeanB + (t * stats.StdDevB);

            swatches[i] = new Lab32((float)l, (float)a, (float)b)
                .ToRgb8(RgbColorSpace.SRgb, profile.Illuminant);
        }

        return swatches;
    }
}
