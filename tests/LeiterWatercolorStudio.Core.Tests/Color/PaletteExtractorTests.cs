namespace LeiterWatercolorStudio.Core.Tests.Color;

using System;
using System.IO;
using Leiter.Algorithms.Color;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using LeiterWatercolorStudio.Core.Color;
using Xunit;

public class PaletteExtractorTests
{
    [Fact]
    public void ExtractFromMatrix_ComputesValidatedProfileMatchingColorTransfer()
    {
        var matrix = new SequentialMatrix<Rgb8>(8, 8);
        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                matrix[x, y] = new Rgb8((byte)(x * 32), (byte)(y * 32), (byte)((x + y) * 16));
            }
        }

        ColorTransferProfile profile = PaletteExtractor.ExtractFromMatrix(matrix, "Gradient Test");
        profile.Validate();

        LabColorStatistics expectedStats = ColorTransfer.ComputeStatistics(matrix, RgbColorSpace.SRgb, Illuminant.E);
        Assert.Equal("Gradient Test", profile.Name);
        Assert.Equal(Illuminant.E, profile.Illuminant);
        Assert.Equal(expectedStats, profile.Statistics);
    }

    [Fact]
    public void ExtractFromMatrix_RejectsInvalidArguments()
    {
        var matrix = new SequentialMatrix<Rgb8>(2, 2);
        Assert.Throws<ArgumentNullException>(() => PaletteExtractor.ExtractFromMatrix(null!, "Valid"));
        Assert.Throws<ArgumentException>(() => PaletteExtractor.ExtractFromMatrix(matrix, "   "));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(7)]
    public void SynthesizeSwatches_ReturnsExpectedCountAndSpansLightness(int count)
    {
        var profile = new ColorTransferProfile(
            "Coastal Study",
            new LabColorStatistics(60.0, -4.0, -12.0, 20.0, 10.0, 15.0),
            Illuminant.E);

        Rgb8[] swatches = PaletteExtractor.SynthesizeSwatches(profile, count);

        Assert.Equal(count, swatches.Length);

        if (count == 1)
        {
            Rgb8 expectedMean = new Lab32(60f, -4f, -12f).ToRgb8(RgbColorSpace.SRgb, Illuminant.E);
            Assert.Equal(expectedMean, swatches[0]);
        }
        else
        {
            Lab32 darkLab = swatches[0].ToLab32(RgbColorSpace.SRgb, Illuminant.E);
            Lab32 lightLab = swatches[^1].ToLab32(RgbColorSpace.SRgb, Illuminant.E);
            Assert.True(lightLab.L > darkLab.L, $"Expected lightest swatch L* ({lightLab.L}) > darkest swatch L* ({darkLab.L}).");

            Rgb8 expectedCenter = new Lab32(60f, -4f, -12f).ToRgb8(RgbColorSpace.SRgb, Illuminant.E);
            Assert.Equal(expectedCenter, swatches[count / 2]);
        }
    }

    [Fact]
    public void SynthesizeSwatches_SupportsD65AndValidatesCount()
    {
        var d65Profile = new ColorTransferProfile(
            "D65 Wash",
            new LabColorStatistics(72.0, 6.0, 14.0, 12.0, 8.0, 9.0),
            Illuminant.D65);

        Rgb8[] swatches = PaletteExtractor.SynthesizeSwatches(d65Profile, 5);
        Assert.Equal(5, swatches.Length);
        Assert.Equal(new Lab32(72f, 6f, 14f).ToRgb8(RgbColorSpace.SRgb, Illuminant.D65), swatches[2]);

        Assert.Throws<ArgumentOutOfRangeException>(() => PaletteExtractor.SynthesizeSwatches(d65Profile, 0));
    }

    [Fact]
    public void ExtractFromFile_LoadsExampleInputImage()
    {
        string inputPath = FindExampleInputPath();
        ColorTransferProfile defaultNamed = PaletteExtractor.ExtractFromFile(inputPath);
        ColorTransferProfile customNamed = PaletteExtractor.ExtractFromFile(inputPath, "Example Reference");

        defaultNamed.Validate();
        customNamed.Validate();

        Assert.Equal("input", defaultNamed.Name);
        Assert.Equal("Example Reference", customNamed.Name);
        Assert.Equal(Illuminant.E, defaultNamed.Illuminant);
        Assert.Equal(defaultNamed.Statistics, customNamed.Statistics);
        Assert.True(defaultNamed.Statistics.MeanL > 0.0);
        Assert.True(defaultNamed.Statistics.StdDevL > 0.0);

        Rgb8[] swatches = PaletteExtractor.SynthesizeSwatches(defaultNamed, 5);
        Assert.Equal(5, swatches.Length);
    }

    private static string FindExampleInputPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "extern", "Leiter", "examples", "input.png");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate extern/Leiter/examples/input.png from test base directory.");
    }
}
