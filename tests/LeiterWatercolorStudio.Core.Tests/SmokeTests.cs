using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Models;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void LeiterAndCoreModels_LinkCleanly()
    {
        var matrix = new SequentialMatrix<Rgb8>(4, 4);
        matrix[1, 2] = new Rgb8(12, 34, 56);

        using var image = matrix.ToImage();
        SequentialMatrix<Rgb8> roundTrip = image.ToSequentialMatrix();

        Assert.Equal(new Rgb8(12, 34, 56), roundTrip[1, 2]);
        Assert.NotNull(WatercolorOptions.Default);
        Assert.Equal(PrimaryStageId.Input, default);
        Assert.Equal(StageViewId.Input, default);
        Assert.Equal(VisualizationMode.Rgb, default);
        Assert.Equal(1024, (int)PreviewResolutionCap.Px1024);
        Assert.Equal(0, (int)PreviewResolutionCap.Native);
    }
}
