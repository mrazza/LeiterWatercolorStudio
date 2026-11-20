using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.App.Rendering;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using Xunit;
using LeiterSize = Leiter.Core.Size;

namespace LeiterWatercolorStudio.App.Tests;

public class LargeImageProfileTests
{
    private static readonly WatercolorStep[] Stage1To5Steps =
    [
        WatercolorStep.ColorTransfer,
        WatercolorStep.EgbiPreBlur,
        WatercolorStep.EgbiSegmentation,
        WatercolorStep.RegionalContrastSaliency,
        WatercolorStep.SaliencyCut,
        WatercolorStep.SaliencyDistanceField,
        WatercolorStep.MeanShiftFiltering,
        WatercolorStep.MeanShiftClustering,
        WatercolorStep.LodAbstraction,
        WatercolorStep.WetInWet,
        WatercolorStep.HandTremor,
    ];

    [AvaloniaFact]
    public async Task LargeImage2400x1600_PreviewCap1024_CachesUpstreamStagesAndReusesWriteableBitmapWithBoundedHeap()
    {
        string inputPath = TestAppHelpers.FindExampleInputPath();
        SequentialMatrix<Rgb8> source = ImageExtensions.LoadRgb8(inputPath);
        var upscaled = (SequentialMatrix<Rgb8>)source.ResizeBilinear(new LeiterSize(2400, 1600));

        using var engine = new PipelineEngine(
            debounceDelay: TimeSpan.Zero,
            initialPreviewCap: PreviewResolutionCap.Px1024);

        engine.LoadImage(upscaled, inputPath);

        Assert.Equal(new LeiterSize(2400, 1600), engine.NativeSize);
        Assert.Equal(1024, Math.Max(engine.PreviewSize.Width, engine.PreviewSize.Height));
        Assert.Equal(new LeiterSize(1024, 683), engine.PreviewSize);

        WatercolorOptions baseline = engine.ComputeBaseline(BaselineMode.ForImage);
        WatercolorResult? initialResult = await engine.RequestRender(baseline, immediate: true);
        Assert.NotNull(initialResult);
        Assert.Equal(engine.PreviewSize, initialResult!.Image.Size);

        WriteableBitmap? bitmapSlot = MatrixBitmapConverter.Blit(initialResult.Image);
        WriteableBitmap initialSlotRef = bitmapSlot;

        try
        {
            // Verify a Stage 6 edit hits cache for Stages 1-5
            WatercolorOptions stage6Edit = baseline with
            {
                ToneCurve = new PiecewiseToneCurveOptions(0.08, 0.94, 0.16, 0.84),
            };
            WatercolorResult? editedResult = await engine.RequestRender(stage6Edit, immediate: true);
            Assert.NotNull(editedResult);

            foreach (WatercolorStep step in Stage1To5Steps)
            {
                Assert.True(editedResult!.StepTimings.TryGetValue(step, out WatercolorStepTiming timing));
                Assert.False(timing.Bypassed);
                Assert.True(timing.FromCache, $"Expected upstream step {step} to hit cache after Stage 6 edit.");
            }

            bitmapSlot = MatrixBitmapConverter.Blit(editedResult!.Image, bitmapSlot);
            Assert.Same(initialSlotRef, bitmapSlot);

            // Stabilize heap baseline after warmup
            ForceFullGc();
            long baselineHeapBytes = GC.GetTotalMemory(forceFullCollection: true);

            // Run 10 consecutive Stage 6 parameter edits + Blit calls reusing the same WriteableBitmap slot
            for (int i = 0; i < 10; i++)
            {
                double minTone = 0.02 + (i * 0.01);
                double maxTone = 0.98 - (i * 0.005);
                WatercolorOptions iterationOptions = baseline with
                {
                    ToneCurve = new PiecewiseToneCurveOptions(minTone, maxTone, 0.15, 0.85),
                };

                WatercolorResult? result = await engine.RequestRender(iterationOptions, immediate: true);
                Assert.NotNull(result);

                foreach (WatercolorStep step in Stage1To5Steps)
                {
                    Assert.True(result!.StepTimings[step].FromCache, $"Iteration {i}: expected {step} to hit cache.");
                }

                WriteableBitmap next = MatrixBitmapConverter.Blit(result!.Image, bitmapSlot);
                Assert.Same(initialSlotRef, next);
                bitmapSlot = next;
            }

            ForceFullGc();
            long finalHeapBytes = GC.GetTotalMemory(forceFullCollection: true);
            long heapDeltaBytes = finalHeapBytes - baselineHeapBytes;

            // With WriteableBitmap reuse and Stage 1-5 cache hits, retained managed heap growth must remain bounded
            const long maxAllowedHeapDeltaBytes = 16L * 1024 * 1024;
            Assert.True(
                heapDeltaBytes < maxAllowedHeapDeltaBytes,
                $"Managed heap grew by {heapDeltaBytes / (1024.0 * 1024.0):F2} MB after 10 edits (limit {maxAllowedHeapDeltaBytes / (1024.0 * 1024.0):F2} MB).");
        }
        finally
        {
            bitmapSlot?.Dispose();
        }
    }

    private static void ForceFullGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
