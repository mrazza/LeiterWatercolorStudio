using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Pipeline;

public class StageVisualizersAndInspectorTests
{
    public static IEnumerable<object[]> AllViewAndModeCombinations()
    {
        foreach (StageViewDescriptor view in PipelineStageRegistry.Views)
        {
            foreach (VisualizationMode mode in view.ValidModes)
            {
                yield return [view.ViewId, mode];
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllViewAndModeCombinations))]
    public void RenderView_All20ViewsAndAllValidModes_WorkNormalAndBypassed(StageViewId view, VisualizationMode mode)
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(36, 28);
        WatercolorOptions normalOpts = WatercolorOptions.ForImage(img);
        var normalSession = new WatercolorPipelineSession(img);
        WatercolorResult normalResult = normalSession.Render(normalOpts);

        Matrix<Rgb8> renderedNormal = StageVisualizers.RenderView(view, mode, img, normalResult, normalOpts, randomSeed: 42);
        Assert.Equal(img.Size, renderedNormal.Size);

        WatercolorOptions bypassedOpts = normalOpts with
        {
            EnableColorTransfer = false,
            EgbiBlurSigma = 0.0,
            EnableLodAbstraction = false,
            EnableWetInWet = false,
            EnableHandTremor = false,
            EnableEdgeDarkening = false,
            EnableTurbulenceFlow = false,
            EnableToneCurve = false,
            EnablePaperGranulation = false,
            EnableFxaa = false,
        };
        var bypassedSession = new WatercolorPipelineSession(img);
        WatercolorResult bypassedResult = bypassedSession.Render(bypassedOpts);

        Matrix<Rgb8> renderedBypassed = StageVisualizers.RenderView(view, mode, img, bypassedResult, bypassedOpts, randomSeed: 42);
        Assert.Equal(img.Size, renderedBypassed.Size);
    }

    [Fact]
    public void RenderView_SpecialModesProduceExpectedDiagnostics()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(40, 32);
        WatercolorOptions opts = WatercolorOptions.ForImage(img);
        WatercolorResult result = new WatercolorPipelineSession(img).Render(opts);

        // Random color differs across seeds
        Matrix<Rgb8> rand1 = StageVisualizers.RenderView(StageViewId.SaliencyEgbiRegions, VisualizationMode.RegionRandomColor, img, result, opts, randomSeed: 1);
        Matrix<Rgb8> rand2 = StageVisualizers.RenderView(StageViewId.SaliencyEgbiRegions, VisualizationMode.RegionRandomColor, img, result, opts, randomSeed: 999);
        Assert.NotEqual(rand1[5, 5], rand2[5, 5]);

        // IsolatedPigmentPreview matches PigmentBlend.ApplyDensityField
        if (result.EdgeDensityField is not null)
        {
            Matrix<Rgb8> isolated = StageVisualizers.RenderView(StageViewId.FinishingEdgeDarkening, VisualizationMode.IsolatedPigmentPreview, img, result, opts);
            Matrix<Rgb8> direct = PigmentBlend.ApplyDensityField(result.HandTremor, result.EdgeDensityField);
            Assert.Equal(direct[10, 10], isolated[10, 10]);
        }

        // FxaaDiffMagnifier is zero when FXAA disabled
        WatercolorOptions noFxaaOpts = opts with { EnableFxaa = false };
        WatercolorResult noFxaaResult = new WatercolorPipelineSession(img).Render(noFxaaOpts);
        Matrix<Rgb8> diffBypassed = StageVisualizers.RenderView(StageViewId.FinishingFinalFxaa, VisualizationMode.FxaaDiffMagnifier, img, noFxaaResult, noFxaaOpts);
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                Assert.Equal(Rgb8.Zero, diffBypassed[x, y]);
            }
        }

        // Invalid mode throws
        Assert.Throws<ArgumentException>(() =>
            StageVisualizers.RenderView(StageViewId.Input, VisualizationMode.HeatmapTurbo, img, result));

        // Empty source returns empty matrix
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        Matrix<Rgb8> emptyRendered = StageVisualizers.RenderView(StageViewId.Input, VisualizationMode.Rgb, empty, null);
        Assert.Equal(new Size(0, 0), emptyRendered.Size);
    }

    [Fact]
    public void PixelInspectorReadout_SamplesAllMetricsAndHandlesNullAndBounds()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(32, 24);
        WatercolorOptions opts = WatercolorOptions.ForImage(img);
        WatercolorResult result = new WatercolorPipelineSession(img).Render(opts);

        PixelInspectorReadout readout = PixelInspectorReadout.Sample(12, 10, StageViewId.FinishingFinalFxaa, img, result);
        Assert.Equal(12, readout.X);
        Assert.Equal(10, readout.Y);
        Assert.Equal(img[12, 10], readout.InputRgb);
        Assert.Equal(result.Image[12, 10], readout.Rgb);
        Assert.True(readout.DeltaEFromInput >= 0.0);
        Assert.NotNull(readout.RegionId);
        Assert.True(readout.RegionArea > 0);
        Assert.InRange(readout.RegionSalientFraction!.Value, 0.0, 1.0);
        Assert.NotNull(readout.Saliency);
        Assert.NotNull(readout.IsSalient);
        Assert.NotNull(readout.DistanceSmoothed);
        Assert.NotNull(readout.DistanceRaw);
        Assert.NotNull(readout.IsWetArea);
        Assert.NotNull(readout.RegionQualifyingBoundaryCount);
        Assert.NotNull(readout.RegionTotalBoundaryCount);
        Assert.NotNull(readout.QualifyingBoundaryCount);
        Assert.NotNull(readout.TotalBoundaryCount);
        Assert.NotNull(readout.EdgeDensity);
        Assert.NotNull(readout.TurbulenceOffset);
        Assert.NotNull(readout.PaperHeight);
        Assert.Equal(result.DensityAdjusted[12, 10], readout.PreToneRgb);
        Assert.Equal(result.ToneAdjusted[12, 10], readout.PostToneRgb);

        string hud = readout.FormatHudText();
        Assert.Contains("(12, 10)", hud);
        Assert.Contains("RGB(", hud);
        Assert.Contains("Lab(", hud);
        Assert.Contains("Region #", hud);
        Assert.Contains("Saliency:", hud);
        Assert.Contains("Wet:", hud);

        // Input view without result
        PixelInspectorReadout inputOnly = PixelInspectorReadout.Sample(4, 5, StageViewId.Input, img, result: null);
        Assert.Equal(0.0, inputOnly.DeltaEFromInput);
        Assert.Null(inputOnly.RegionId);
        Assert.Null(inputOnly.EdgeDensity);

        // Bypassed stage 6 fields
        WatercolorOptions bypassedOpts = opts with
        {
            EnableEdgeDarkening = false,
            EnableTurbulenceFlow = false,
            EnablePaperGranulation = false,
        };
        WatercolorResult bypassed = new WatercolorPipelineSession(img).Render(bypassedOpts);
        PixelInspectorReadout bypassedReadout = PixelInspectorReadout.Sample(8, 8, StageViewId.FinishingFinalFxaa, img, bypassed);
        Assert.Null(bypassedReadout.EdgeDensity);
        Assert.Null(bypassedReadout.TurbulenceOffset);
        Assert.Null(bypassedReadout.PaperHeight);
        Assert.Contains("Edge:off", bypassedReadout.FormatHudText());

        // Bounds checks
        Assert.False(PixelInspectorReadout.TrySample(-1, 0, StageViewId.Input, img, null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => PixelInspectorReadout.Sample(img.Width, 0, StageViewId.Input, img, null));
    }
}

internal static class PipelineTestImages
{
    public static SequentialMatrix<Rgb8> CreateSyntheticScene(int width, int height)
    {
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inCenterBlock = x >= width / 4 && x < (3 * width) / 4 && y >= height / 4 && y < (3 * height) / 4;
                if (inCenterBlock)
                {
                    byte r = (byte)Math.Clamp(190 + ((x * 40) / Math.Max(1, width)), 0, 255);
                    byte g = (byte)Math.Clamp(60 + ((y * 50) / Math.Max(1, height)), 0, 255);
                    byte b = 45;
                    matrix[x, y] = new Rgb8(r, g, b);
                }
                else
                {
                    byte r = (byte)Math.Clamp(40 + ((x * 30) / Math.Max(1, width)), 0, 255);
                    byte g = (byte)Math.Clamp(95 + ((y * 60) / Math.Max(1, height)), 0, 255);
                    byte b = (byte)Math.Clamp(170 + (((x + y) * 30) / Math.Max(1, width + height)), 0, 255);
                    matrix[x, y] = new Rgb8(r, g, b);
                }
            }
        }

        return matrix;
    }
}
