using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Pipeline;

public class PipelineStalenessTrackerTests
{
    [Fact]
    public void ComputeStale_NullLastRendered_MarksAllStepsAndProcessingStagesStale()
    {
        var (steps, stages) = PipelineStalenessTracker.ComputeStale(null, WatercolorOptions.Default);

        Assert.Equal(Enum.GetValues<WatercolorStep>().Length, steps.Count);
        Assert.Equal(7, stages.Count);
        Assert.DoesNotContain(PrimaryStageId.Input, stages);
    }

    [Fact]
    public void ComputeStale_IdenticalOptions_IsEmpty()
    {
        var (steps, stages) = PipelineStalenessTracker.ComputeStale(WatercolorOptions.Default, WatercolorOptions.Default);

        Assert.Empty(steps);
        Assert.Empty(stages);
    }

    [Fact]
    public void ComputeStale_MatchesWatercolorPipelineSessionCacheBehavior()
    {
        SequentialMatrix<Rgb8> img = PipelineTestImages.CreateSyntheticScene(24, 24);
        var session = new WatercolorPipelineSession(img);
        WatercolorOptions baseline = WatercolorOptions.ForImage(img);
        session.Render(baseline);

        // 1. Saliency change leaves Stage 3a MeanShift cached
        WatercolorOptions saliencyEdited = baseline with
        {
            Egbi = baseline.Egbi with { KFactor = baseline.Egbi.KFactor + 25.0 }
        };
        AssertMatchesSessionCache(session, baseline, saliencyEdited, img.Size, expectedNotStale:
        [
            WatercolorStep.ColorTransfer,
            WatercolorStep.EgbiPreBlur,
            WatercolorStep.MeanShiftFiltering,
            WatercolorStep.MeanShiftClustering,
            WatercolorStep.TurbulenceFlow,
            WatercolorStep.PaperHeightMap,
        ]);

        // 2. MeanShift MinRegionSize leaves MeanShiftFiltering & Stage 2 Saliency cached
        WatercolorOptions clusteringEdited = saliencyEdited with
        {
            MeanShift = saliencyEdited.MeanShift with { MinRegionSize = saliencyEdited.MeanShift.MinRegionSize + 10 }
        };
        AssertMatchesSessionCache(session, saliencyEdited, clusteringEdited, img.Size, expectedNotStale:
        [
            WatercolorStep.ColorTransfer,
            WatercolorStep.EgbiPreBlur,
            WatercolorStep.EgbiSegmentation,
            WatercolorStep.RegionalContrastSaliency,
            WatercolorStep.SaliencyCut,
            WatercolorStep.SaliencyDistanceField,
            WatercolorStep.MeanShiftFiltering,
            WatercolorStep.TurbulenceFlow,
            WatercolorStep.PaperHeightMap,
        ]);

        // 3. Stage 6 TurbulenceFlow leaves Stages 1-5, EdgeDarkening, and PaperHeightMap cached
        WatercolorOptions turbEdited = clusteringEdited with
        {
            TurbulenceFlow = clusteringEdited.TurbulenceFlow with { Intensity = clusteringEdited.TurbulenceFlow.Intensity + 0.15 }
        };
        AssertMatchesSessionCache(session, clusteringEdited, turbEdited, img.Size, expectedNotStale:
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
            WatercolorStep.EdgeDarkening,
            WatercolorStep.PaperHeightMap,
        ]);

        // 4. PaperGranulation DensityStrength leaves PaperHeightMap & ToneCurve cached
        WatercolorOptions granStrengthEdited = turbEdited with
        {
            PaperGranulation = turbEdited.PaperGranulation with { DensityStrength = turbEdited.PaperGranulation.DensityStrength + 0.1 }
        };
        AssertMatchesSessionCache(session, turbEdited, granStrengthEdited, img.Size, expectedNotStale:
        [
            WatercolorStep.ToneCurve,
            WatercolorStep.PaperHeightMap,
        ]);

        // 5. PaperGranulation GrainNoise changes PaperHeightMap
        WatercolorOptions granNoiseEdited = granStrengthEdited with
        {
            PaperGranulation = granStrengthEdited.PaperGranulation with
            {
                GrainNoise = granStrengthEdited.PaperGranulation.GrainNoise with { Seed = 999 }
            }
        };
        var (grainSteps, grainStages) = PipelineStalenessTracker.ComputeStale(granStrengthEdited, granNoiseEdited, img.Size);
        Assert.Contains(WatercolorStep.PaperHeightMap, grainSteps);
        Assert.Contains(WatercolorStep.PaperGranulation, grainSteps);
        Assert.Contains(WatercolorStep.Fxaa, grainSteps);
        Assert.DoesNotContain(WatercolorStep.ToneCurve, grainSteps);
        Assert.Equal([PrimaryStageId.Finishing], grainStages);
        WatercolorResult granResult = session.Render(granNoiseEdited);
        Assert.False(granResult.StepTimings[WatercolorStep.PaperHeightMap].FromCache);
        Assert.True(granResult.StepTimings[WatercolorStep.ToneCurve].FromCache);
    }

    [Fact]
    public void ComputeStale_DisabledAndZeroIntensityNoOps_DoNotInvalidate()
    {
        WatercolorOptions allBypassed = WatercolorOptions.Default with
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

        WatercolorOptions mutatedWhileBypassed = allBypassed with
        {
            ColorTransferStrength = 0.25,
            LodAbstraction = new LodAbstractionOptions(SalientKernelSize: 7),
            WetInWet = new WetInWetOptions(ScatterRadius: 8),
            HandTremor = new HandTremorOptions(OverlapDarkening: 0.9),
            EdgeDarkening = new EdgeDarkeningOptions(BlurSigma: 2.0),
            TurbulenceFlow = new TurbulenceFlowOptions(Noise: new PerlinNoiseOptions(Seed: 999)),
            ToneCurve = new PiecewiseToneCurveOptions(MinTone: 0.1),
            PaperGranulation = new PaperGranulationOptions(GrainNoise: new PerlinNoiseOptions(Seed: 888)),
            Fxaa = new FxaaOptions(MaxSpanSearchSteps: 16),
        };

        var (steps, stages) = PipelineStalenessTracker.ComputeStale(allBypassed, mutatedWhileBypassed);
        Assert.Empty(steps);
        Assert.Empty(stages);

        // Zero intensity / Disabled mode while Enable* is true
        WatercolorOptions zeroIntensity = WatercolorOptions.Default with
        {
            ColorTransferStrength = 0.0,
            WetInWet = WatercolorOptions.Default.WetInWet with { Mode = WetInWetMode.Disabled },
            HandTremor = WatercolorOptions.Default.HandTremor with { Amplitude = 0.0 },
            EdgeDarkening = WatercolorOptions.Default.EdgeDarkening with { Intensity = 0.0 },
            TurbulenceFlow = WatercolorOptions.Default.TurbulenceFlow with { Intensity = 0.0 },
            PaperGranulation = WatercolorOptions.Default.PaperGranulation with { DensityStrength = 0.0, WarpAmplitude = 0.0 },
        };

        WatercolorOptions zeroIntensityToggle = zeroIntensity with
        {
            EnableColorTransfer = false,
            EnableWetInWet = false,
            EnableHandTremor = false,
            EnableEdgeDarkening = false,
            EnableTurbulenceFlow = false,
            EnablePaperGranulation = false,
        };

        var (toggleSteps, toggleStages) = PipelineStalenessTracker.ComputeStale(zeroIntensity, zeroIntensityToggle);
        Assert.Empty(toggleSteps);
        Assert.Empty(toggleStages);
    }

    private static void AssertMatchesSessionCache(
        WatercolorPipelineSession session,
        WatercolorOptions previous,
        WatercolorOptions next,
        Size size,
        WatercolorStep[] expectedNotStale)
    {
        var (staleSteps, _) = PipelineStalenessTracker.ComputeStale(previous, next, size);
        WatercolorResult result = session.Render(next);

        foreach (WatercolorStep step in expectedNotStale)
        {
            Assert.DoesNotContain(step, staleSteps);
        }

        foreach (var (step, timing) in result.StepTimings)
        {
            Assert.Equal(!timing.FromCache, staleSteps.Contains(step));
        }
    }
}
