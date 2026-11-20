using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Parameters;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Parameters;

public class PipelineParametersViewModelTests
{
    private static WatercolorOptions CreateAllNonDefaultOptions(ColorTransferProfile? targetPalette = null)
    {
        return new WatercolorOptions(
            TargetPalette: targetPalette ?? new ColorTransferProfile(
                "Autumn Studio Wash",
                new LabColorStatistics(58.5, 11.2, 24.8, 14.3, 9.7, 13.4),
                Illuminant.D65),
            ColorTransferStrength: 0.72,
            Egbi: new EgbiOptions(
                KFactor: 145.5,
                Epsilon: 2.35,
                MinRegionSize: 85,
                Connectivity: GridConnectivity.FourConnected),
            RegionalContrastSaliency: new RegionalContrastSaliencyOptions(
                EnableSmoothing: false,
                ComputeBorderRegions: false,
                CenterPriorWeight: 4.25),
            SaliencyCut: new SaliencyCutOptions(
                MaxOuterIterations: 6,
                TrimapBandRadius: 14,
                GrabCut: new GrabCutOptions(
                    Iterations: 4,
                    MixtureComponentCount: 7,
                    SmoothnessWeight: 37.5),
                Mask: new SaliencyMaskOptions(
                    Method: SaliencyThresholdMethod.AdaptiveMean,
                    FixedThreshold: 0.42,
                    RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
                    MinAreaFraction: 0.035,
                    Connectivity: GridConnectivity.FourConnected),
                MaxWorkingDimension: 640),
            SaliencyDistanceField: new SaliencyDistanceFieldOptions(
                SmoothingSigma: 7.5,
                DefaultSigmaScale: 0.045),
            MeanShift: new MeanShiftOptions(
                SpatialRadius: 12,
                RangeRadius: 7.25,
                MinRegionSize: 120,
                MaxIterations: 28,
                ConvergenceEpsilon: 0.08,
                Connectivity: GridConnectivity.FourConnected),
            LodAbstraction: new LodAbstractionOptions(
                SalientKernelSize: 7,
                MinNonSalientKernelSize: 3,
                MaxNonSalientKernelSize: 13,
                DistanceKernelScale: 14.5,
                DistanceKernelBias: -0.15,
                CrossSegmentColorScale: 0.42,
                SalientOverlapThreshold: 0.65),
            WetInWet: new WetInWetOptions(
                Mode: WetInWetMode.ExcludeSalientBoundaries,
                ColorGradientThreshold: 0.18,
                SalientHueThresholdDegrees: 32.0,
                NonSalientHueThresholdDegrees: 115.0,
                MinIntensityDifference: 0.05,
                SalientOverlapThreshold: 0.6,
                ScatterRadius: 7,
                SeedProbability: 0.62,
                EllipticalFilter: new OrientedEllipticalFilterOptions(
                    MajorRadius: 8.5,
                    MinorRadius: 3.25,
                    SigmaScale: 0.72),
                Seed: 4242),
            HandTremor: new HandTremorOptions(
                Amplitude: 4.5,
                SalientAmplitudeScale: 0.45,
                SimilarHueThresholdDegrees: 42.0,
                OverlapDarkening: 0.35,
                PaperColor: new Rgb8(241, 234, 221),
                PrimaryNoise: new PerlinNoiseOptions(Seed: 1111, Frequency: 0.06, Octaves: 5, Lacunarity: 2.3, Persistence: 0.4),
                SecondaryNoise: new PerlinNoiseOptions(Seed: 2222, Frequency: 0.09, Octaves: 2, Lacunarity: 1.8, Persistence: 0.65),
                MinContrastingChroma: 0.12,
                PaperGapBias: -0.25,
                PaperGapOpacity: 0.82),
            EdgeDarkening: new EdgeDarkeningOptions(
                Intensity: 2.4,
                BlurSigma: 1.25,
                GradientOperator: GradientOperator.Scharr),
            TurbulenceFlow: new TurbulenceFlowOptions(
                Intensity: 0.55,
                Noise: new PerlinNoiseOptions(Seed: 3333, Frequency: 0.025, Octaves: 6, Lacunarity: 2.1, Persistence: 0.58)),
            ToneCurve: new PiecewiseToneCurveOptions(
                MinTone: 0.08,
                MaxTone: 0.91,
                ShadowKnee: 0.22,
                HighlightKnee: 0.78),
            PaperGranulation: new PaperGranulationOptions(
                DensityStrength: 0.48,
                WarpAmplitude: 0.95,
                GrainNoise: new PerlinNoiseOptions(Seed: 4444, Frequency: 0.24, Octaves: 3, Lacunarity: 2.4, Persistence: 0.62)),
            Fxaa: new FxaaOptions(
                RelativeContrastThreshold: 0.166,
                AbsoluteContrastThreshold: 0.045,
                SubpixelBlendStrength: 0.6,
                MaxSpanSearchSteps: 12),
            EnableFxaa: false,
            EgbiBlurSigma: 1.35,
            EnableColorTransfer: false,
            EnableLodAbstraction: false,
            EnableWetInWet: false,
            EnableHandTremor: false,
            EnableEdgeDarkening: false,
            EnableTurbulenceFlow: false,
            EnableToneCurve: false,
            EnablePaperGranulation: false);
    }

    [Fact]
    public void DefaultInitialization_MatchesWatercolorOptionsDefault()
    {
        var vm = new PipelineParametersViewModel();

        Assert.Equal(BaselineMode.Default, vm.ActiveBaselineMode);
        Assert.Equal(WatercolorOptions.Default, vm.BaselineOptions);
        Assert.Equal(WatercolorOptions.Default, vm.BuildOptions());
        Assert.False(vm.CanUndo);
        Assert.False(vm.CanRedo);
    }

    [Fact]
    public void FullLeafRoundTrip_AcrossAll13OptionRecords_MatchesExactlyAndValidates()
    {
        WatercolorOptions custom = CreateAllNonDefaultOptions();
        custom.Validate();

        var vm = new PipelineParametersViewModel();
        vm.LoadFromOptions(custom);

        WatercolorOptions roundTripped = vm.BuildOptions();
        roundTripped.Validate();

        Assert.Equal(PaletteMode.Custom, vm.ColorTransfer.PaletteMode);
        Assert.Equal(custom, roundTripped);
    }

    [Fact]
    public void FullLeafRoundTrip_BuiltInAndAutoPaletteAndZeroExplicitSigma_PreserveValues()
    {
        WatercolorOptions builtInOpts = CreateAllNonDefaultOptions(WatercolorPalettes.BotanicalFoliage) with
        {
            SaliencyCut = CreateAllNonDefaultOptions().SaliencyCut with { MaxWorkingDimension = null },
            SaliencyDistanceField = new SaliencyDistanceFieldOptions(SmoothingSigma: 0.0, DefaultSigmaScale: 0.02)
        };

        var vm = new PipelineParametersViewModel();
        vm.LoadFromOptions(builtInOpts);

        Assert.Equal(PaletteMode.BuiltIn, vm.ColorTransfer.PaletteMode);
        Assert.Equal(WatercolorPalettes.BotanicalFoliage, vm.ColorTransfer.SelectedBuiltInPalette);
        Assert.False(vm.Saliency.SaliencyCut.UseMaxWorkingDimension);
        Assert.True(vm.Saliency.SaliencyDistanceField.UseExplicitSmoothingSigma);
        Assert.Equal(0.0, vm.Saliency.SaliencyDistanceField.SmoothingSigma);
        Assert.Equal(builtInOpts, vm.BuildOptions());

        WatercolorOptions autoOpts = WatercolorOptions.Default with { ColorTransferStrength = 0.65 };
        vm.LoadFromOptions(autoOpts);
        Assert.Equal(PaletteMode.Auto, vm.ColorTransfer.PaletteMode);
        Assert.Equal(autoOpts, vm.BuildOptions());
    }

    [Fact]
    public void BaselineSwitching_AndSetBaselineOnly_BehaveAsSpecified()
    {
        WatercolorOptions forSize = WatercolorOptions.ForSize(new Size(1600, 1200));
        WatercolorOptions forImage = CreateAllNonDefaultOptions(WatercolorPalettes.PastoralSunlight);

        var vm = new PipelineParametersViewModel();
        var notifications = new List<OptionsChangedEventArgs>();
        vm.OptionsChanged += (_, e) => notifications.Add(e);

        // SetBaselineOnly updates mode, baseline, and noise baselines without altering current values or notifying.
        vm.SetBaselineOnly(BaselineMode.ForSize, forSize);
        Assert.Equal(BaselineMode.ForSize, vm.ActiveBaselineMode);
        Assert.Equal(forSize, vm.BaselineOptions);
        Assert.Equal(forSize.PaperGranulation.GrainNoise, vm.Finishing.GrainNoise.Baseline);
        Assert.Equal(WatercolorOptions.Default, vm.BuildOptions());
        Assert.Empty(notifications);

        // SwitchBaseline updates mode, baseline, stage values, records undo, and fires immediate OptionsChanged.
        vm.SwitchBaseline(BaselineMode.ForImage, forImage);
        Assert.Equal(BaselineMode.ForImage, vm.ActiveBaselineMode);
        Assert.Equal(forImage, vm.BaselineOptions);
        Assert.Equal(forImage, vm.BuildOptions());
        Assert.True(vm.CanUndo);
        Assert.Single(notifications);
        Assert.True(notifications[0].IsImmediate);

        // Undo reverts SwitchBaseline changes.
        vm.Undo();
        Assert.Equal(WatercolorOptions.Default, vm.BuildOptions());
    }

    [Fact]
    public void SwitchBaseline_WithAutoPaletteModeHint_PreservesAutoAcrossResets()
    {
        WatercolorOptions forImageLike = WatercolorOptions.Default with
        {
            TargetPalette = WatercolorPalettes.WarmOchreEarth,
            ColorTransferStrength = 0.85,
        };

        var vm = new PipelineParametersViewModel();
        vm.SwitchBaseline(BaselineMode.ForImage, forImageLike, PaletteMode.Auto);

        Assert.Equal(PaletteMode.Auto, vm.ColorTransfer.PaletteMode);
        Assert.Equal(WatercolorPalettes.WarmOchreEarth, vm.ColorTransfer.ActiveMatchedPalette);
        Assert.Null(vm.BuildOptions().TargetPalette);

        vm.ColorTransfer.SetCustomProfile(new ColorTransferProfile("Temp", new(60, 5, 5, 10, 5, 5)));
        Assert.Equal(PaletteMode.Custom, vm.ColorTransfer.PaletteMode);

        vm.ResetStage(PrimaryStageId.ColorTransfer);
        Assert.Equal(PaletteMode.Auto, vm.ColorTransfer.PaletteMode);
        Assert.Equal(0.85, vm.ColorTransfer.ColorTransferStrength);
    }

    [Fact]
    public void ResetStage_ResetsOnlyTargetStageAndLeavesOtherStagesIntact()
    {
        var vm = new PipelineParametersViewModel();

        vm.ColorTransfer.ColorTransferStrength = 0.4;
        vm.Saliency.Egbi.KFactor = 250.0;
        vm.Saliency.SaliencyCut.TrimapBandRadius = 18;
        vm.Segmentation.SpatialRadius = 15;
        vm.LodAbstraction.SalientKernelSize = 9;
        vm.WetInWet.ScatterRadius = 10;
        vm.HandTremor.Amplitude = 7.5;
        vm.Finishing.EdgeIntensity = 3.2;

        // Input stage reset is a no-op.
        int undoCountBefore = vm.UndoRedo.UndoCount;
        vm.ResetStage(PrimaryStageId.Input);
        Assert.Equal(undoCountBefore, vm.UndoRedo.UndoCount);

        // Reset Saliency only.
        vm.ResetStage(PrimaryStageId.Saliency);

        Assert.Equal(WatercolorOptions.Default.Egbi.KFactor, vm.Saliency.Egbi.KFactor);
        Assert.Equal(WatercolorOptions.Default.SaliencyCut.TrimapBandRadius, vm.Saliency.SaliencyCut.TrimapBandRadius);

        // Other stages remain modified.
        Assert.Equal(0.4, vm.ColorTransfer.ColorTransferStrength);
        Assert.Equal(15, vm.Segmentation.SpatialRadius);
        Assert.Equal(9, vm.LodAbstraction.SalientKernelSize);
        Assert.Equal(10, vm.WetInWet.ScatterRadius);
        Assert.Equal(7.5, vm.HandTremor.Amplitude);
        Assert.Equal(3.2, vm.Finishing.EdgeIntensity);

        // Resetting all restores everything to BaselineOptions.
        vm.ResetAllToBaseline();
        Assert.Equal(WatercolorOptions.Default, vm.BuildOptions());
    }

    [Fact]
    public void ImmediateVsDebouncedClassification_DistinguishesTogglesAndEnumsFromSliders()
    {
        var vm = new PipelineParametersViewModel();
        var events = new List<OptionsChangedEventArgs>();
        vm.OptionsChanged += (_, e) => events.Add(e);

        // Continuous edits -> IsImmediate = false.
        vm.ColorTransfer.ColorTransferStrength = 0.5;
        vm.Saliency.EgbiBlurSigma = 1.2;
        vm.Saliency.Egbi.KFactor = 80.0;
        vm.Saliency.RegionalContrastSaliency.CenterPriorWeight = 5.0;
        vm.Saliency.GrabCut.SmoothnessWeight = 30.0;
        vm.Saliency.SaliencyMask.FixedThreshold = 0.4;
        vm.Saliency.SaliencyDistanceField.DefaultSigmaScale = 0.05;
        vm.Segmentation.RangeRadius = 8.0;
        vm.LodAbstraction.DistanceKernelScale = 12.0;
        vm.WetInWet.SeedProbability = 0.7;
        vm.HandTremor.Amplitude = 5.0;
        vm.HandTremor.PaperColorHex = "#E0D8C8";
        vm.HandTremor.PrimaryNoise.Frequency = 0.07;
        vm.Finishing.EdgeIntensity = 2.1;
        vm.Finishing.TurbulenceNoise.Persistence = 0.6;
        vm.Finishing.MinTone = 0.1;
        vm.Finishing.GrainNoise.Octaves = 6;
        vm.Finishing.SubpixelBlendStrength = 0.5;

        Assert.Equal(18, events.Count);
        Assert.All(events, e => Assert.False(e.IsImmediate));

        events.Clear();

        // Discrete toggles, enum selectors, and discrete actions -> IsImmediate = true.
        vm.ColorTransfer.EnableColorTransfer = false;
        vm.ColorTransfer.PaletteMode = PaletteMode.BuiltIn;
        vm.ColorTransfer.SelectedBuiltInPaletteIndex = 3;
        vm.ColorTransfer.PaletteMode = PaletteMode.Custom;
        vm.ColorTransfer.CustomIlluminant = Illuminant.D65;
        vm.Saliency.Egbi.Connectivity = GridConnectivity.FourConnected;
        vm.Saliency.RegionalContrastSaliency.EnableSmoothing = false;
        vm.Saliency.RegionalContrastSaliency.ComputeBorderRegions = false;
        vm.Saliency.SaliencyCut.UseMaxWorkingDimension = true;
        vm.Saliency.SaliencyMask.Method = SaliencyThresholdMethod.Otsu;
        vm.Saliency.SaliencyMask.RetentionPolicy = ComponentRetentionPolicy.KeepAll;
        vm.Saliency.SaliencyMask.Connectivity = GridConnectivity.FourConnected;
        vm.Saliency.SaliencyDistanceField.UseExplicitSmoothingSigma = true;
        vm.Segmentation.Connectivity = GridConnectivity.FourConnected;
        vm.LodAbstraction.EnableLodAbstraction = false;
        vm.WetInWet.EnableWetInWet = false;
        vm.WetInWet.Mode = WetInWetMode.ExcludeSalientRegions;
        vm.WetInWet.RandomizeSeed();
        vm.HandTremor.EnableHandTremor = false;
        vm.HandTremor.PrimaryNoise.RandomizeSeed();
        vm.Finishing.EnableEdgeDarkening = false;
        vm.Finishing.EdgeGradientOperator = GradientOperator.Scharr;
        vm.Finishing.EnableTurbulenceFlow = false;
        vm.Finishing.EnableToneCurve = false;
        vm.Finishing.EnablePaperGranulation = false;
        vm.Finishing.EnableFxaa = false;

        Assert.Equal(26, events.Count);
        Assert.All(events, e => Assert.True(e.IsImmediate));
    }

    [Fact]
    public void DragCoalescingAndUndoRedo_IntegrateAcrossStages()
    {
        var vm = new PipelineParametersViewModel();
        var canUndoChanged = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PipelineParametersViewModel.CanUndo))
            {
                canUndoChanged.Add(vm.CanUndo);
            }
        };

        vm.BeginDrag();
        vm.Finishing.EdgeIntensity = 2.0;
        vm.Finishing.EdgeIntensity = 2.5;
        vm.Finishing.EdgeIntensity = 3.1;
        Assert.False(vm.CanUndo);

        vm.EndDrag();
        Assert.True(vm.CanUndo);
        Assert.Equal(1, vm.UndoRedo.UndoCount);
        Assert.Contains(true, canUndoChanged);

        vm.Undo();
        Assert.Equal(WatercolorOptions.Default.EdgeDarkening.Intensity, vm.Finishing.EdgeIntensity);
        Assert.False(vm.CanUndo);
        Assert.True(vm.CanRedo);

        vm.Redo();
        Assert.Equal(3.1, vm.Finishing.EdgeIntensity);
        Assert.True(vm.CanUndo);
        Assert.False(vm.CanRedo);
    }
}
