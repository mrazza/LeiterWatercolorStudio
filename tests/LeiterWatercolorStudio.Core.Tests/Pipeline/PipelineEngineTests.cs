using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Pipeline;

public class PipelineEngineTests
{
    [Fact]
    public void LoadImage_AndSetPreviewCap_RetainsAnalysisAndNativeSource()
    {
        SequentialMatrix<Rgb8> large = PipelineTestImages.CreateSyntheticScene(64, 48);
        using var engine = new PipelineEngine(
            debounceDelay: TimeSpan.FromMilliseconds(5),
            initialPreviewCap: PreviewResolutionCap.Px1024);

        engine.LoadImage(large, "test.png");

        Assert.True(engine.HasImage);
        Assert.Equal("test.png", engine.SourcePath);
        Assert.Same(large, engine.NativeSource);
        Assert.NotNull(engine.Analysis);
        Assert.Equal(new Size(64, 48), engine.PreviewSize);

        var analysisRef = engine.Analysis;

        // Also verify downscaling via a larger synthetic image
        SequentialMatrix<Rgb8> wide = PipelineTestImages.CreateSyntheticScene(640, 320);
        engine.LoadImage(wide);
        analysisRef = engine.Analysis;
        engine.SetPreviewCap(PreviewResolutionCap.Px512);

        Assert.Same(wide, engine.NativeSource);
        Assert.Same(analysisRef, engine.Analysis);
        Assert.Equal(new Size(512, 256), engine.PreviewSize);

        WatercolorOptions forImage = engine.ComputeBaseline(BaselineMode.ForImage);
        WatercolorOptions forSize = engine.ComputeBaseline(BaselineMode.ForSize);
        WatercolorOptions def = engine.ComputeBaseline(BaselineMode.Default);

        Assert.NotNull(forImage.TargetPalette);
        Assert.Equal(WatercolorOptions.Default, def);
        forSize.Validate();
        forImage.Validate();
    }

    [Fact]
    public async Task RapidEditStress_20ConcurrentRequests_HasZeroInvalidOperationExceptions()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(32, 24);
        using var engine = new PipelineEngine(
            debounceDelay: TimeSpan.FromMilliseconds(8),
            initialPreviewCap: PreviewResolutionCap.Native);
        engine.LoadImage(img);

        int failedCount = 0;
        int completedCount = 0;
        engine.RenderFailed += (_, _) => Interlocked.Increment(ref failedCount);
        engine.RenderCompleted += (_, _) => Interlocked.Increment(ref completedCount);

        WatercolorOptions baseline = engine.ComputeBaseline(BaselineMode.ForImage);
        var tasks = new List<Task<WatercolorResult?>>(20);
        WatercolorOptions? lastRequested = null;

        for (int i = 0; i < 20; i++)
        {
            WatercolorOptions next = baseline with
            {
                ColorTransferStrength = 0.5 + (i * 0.02),
                EdgeDarkening = baseline.EdgeDarkening with { Intensity = 1.0 + (i * 0.05) }
            };
            lastRequested = next;
            tasks.Add(engine.RequestRender(next));
            await Task.Delay(2);
        }

        WatercolorResult?[] results = await Task.WhenAll(tasks);

        Assert.Equal(0, failedCount);
        Assert.True(completedCount >= 1);
        Assert.NotNull(engine.LastResult);
        Assert.Equal(lastRequested, engine.LastRenderedCanonicalOptions);
        Assert.Contains(results, static r => r is not null);
    }

    [Fact]
    public async Task PauseAutoRun_MarksStaleUntilRunNowAsync()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(24, 24);
        using var engine = new PipelineEngine(debounceDelay: TimeSpan.FromMilliseconds(5));
        engine.LoadImage(img);

        WatercolorOptions initial = engine.ComputeBaseline(BaselineMode.ForImage);
        WatercolorResult? firstResult = await engine.RequestRender(initial, immediate: true);
        Assert.NotNull(firstResult);
        Assert.Empty(engine.StaleSteps);

        engine.IsAutoRunEnabled = false;
        WatercolorOptions edited = initial with { EnableFxaa = false };
        WatercolorResult? skipped = await engine.RequestRender(edited);

        Assert.Null(skipped);
        Assert.Equal(initial, engine.LastRenderedCanonicalOptions);
        Assert.Equal(edited, engine.PendingCanonicalOptions);
        Assert.Contains(WatercolorStep.Fxaa, engine.StaleSteps);
        Assert.Contains(PrimaryStageId.Finishing, engine.StaleStages);

        WatercolorResult? manualResult = await engine.RunNowAsync();
        Assert.NotNull(manualResult);
        Assert.Equal(edited, engine.LastRenderedCanonicalOptions);
        Assert.Empty(engine.StaleSteps);
        Assert.Empty(engine.StaleStages);
    }

    [Fact]
    public async Task CancelActiveRender_EmitsRenderCancelled()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(32, 32);
        using var engine = new PipelineEngine(debounceDelay: TimeSpan.FromMilliseconds(150));
        engine.LoadImage(img);

        int cancelledCount = 0;
        engine.RenderCancelled += (_, _) => Interlocked.Increment(ref cancelledCount);

        Task<WatercolorResult?> pending = engine.RequestRender(engine.ComputeBaseline(BaselineMode.ForImage));
        engine.CancelActiveRender();

        WatercolorResult? result = await pending;
        Assert.Null(result);
        Assert.Equal(1, cancelledCount);
    }

    [Fact]
    public async Task RenderFullResolutionAsync_PreservesPreviewSessionCache()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(64, 48);
        using var engine = new PipelineEngine(
            debounceDelay: TimeSpan.FromMilliseconds(5),
            initialPreviewCap: PreviewResolutionCap.Px512);
        engine.LoadImage(img);

        // Make a larger image so preview is downscaled relative to native
        SequentialMatrix<Rgb8> native = PipelineTestImages.CreateSyntheticScene(600, 400);
        engine.LoadImage(native);
        Assert.Equal(new Size(512, 341), engine.PreviewSize);

        WatercolorOptions opts = engine.ComputeBaseline(BaselineMode.ForImage) with
        {
            EnableColorTransfer = false,
            EnableLodAbstraction = false,
            EnableWetInWet = false,
            EnableHandTremor = false,
            EnableEdgeDarkening = false,
            EnableTurbulenceFlow = false,
            EnableToneCurve = false,
            EnablePaperGranulation = false,
            EnableFxaa = false,
        };

        WatercolorResult? previewFirst = await engine.RequestRender(opts, immediate: true);
        Assert.NotNull(previewFirst);
        Assert.Equal(new Size(512, 341), previewFirst.Image.Size);

        int fullSteps = 0;
        WatercolorResult fullResult = await engine.RenderFullResolutionAsync(opts, _ => Interlocked.Increment(ref fullSteps));
        Assert.Equal(new Size(600, 400), fullResult.Image.Size);
        Assert.Equal(18, fullSteps);
        Assert.Same(previewFirst, engine.LastResult);

        // Preview session cache should still be warm when requesting slightly changed final step
        WatercolorOptions fxaaToggled = opts with { EnableFxaa = true };
        WatercolorResult? previewSecond = await engine.RequestRender(fxaaToggled, immediate: true);
        Assert.NotNull(previewSecond);
        Assert.True(previewSecond.StepTimings[WatercolorStep.MeanShiftFiltering].FromCache);
        Assert.True(previewSecond.StepTimings[WatercolorStep.SaliencyCut].FromCache);
    }

    [Fact]
    public async Task RequestRender_InvalidOptions_EmitsRenderFailed()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(24, 24);
        using var engine = new PipelineEngine(debounceDelay: TimeSpan.FromMilliseconds(5));
        engine.LoadImage(img);

        RenderFailedEventArgs? failed = null;
        engine.RenderFailed += (_, args) => failed = args;

        WatercolorOptions invalid = WatercolorOptions.Default with { ColorTransferStrength = 2.5 };
        WatercolorResult? result = await engine.RequestRender(invalid, immediate: true);

        Assert.Null(result);
        Assert.NotNull(failed);
        Assert.IsType<ArgumentOutOfRangeException>(failed.Exception);
        Assert.False(engine.IsRendering);
    }
}
