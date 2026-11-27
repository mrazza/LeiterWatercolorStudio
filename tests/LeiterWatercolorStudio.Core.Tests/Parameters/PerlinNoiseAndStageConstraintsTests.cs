using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Parameters;
using LeiterWatercolorStudio.Core.Parameters.Stages;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Parameters;

public class PerlinNoiseAndStageConstraintsTests
{
    [Fact]
    public void PerlinNoiseViewModel_ClampsInvalidInputsAndSupportsPerParamReset()
    {
        var baseline = new PerlinNoiseOptions(Seed: 42, Frequency: 0.12, Octaves: 5, Lacunarity: 2.2, Persistence: 0.45);
        var vm = new PerlinNoiseViewModel(baseline);

        vm.Frequency = 0.0;
        Assert.True(vm.Frequency > 0.0);
        vm.Frequency = double.NaN;
        Assert.True(double.IsFinite(vm.Frequency) && vm.Frequency > 0.0);

        vm.Octaves = 0;
        Assert.Equal(1, vm.Octaves);
        vm.Octaves = 99;
        Assert.Equal(16, vm.Octaves);

        vm.Lacunarity = -2.0;
        Assert.True(vm.Lacunarity > 0.0);

        vm.Persistence = -0.5;
        Assert.Equal(0.0, vm.Persistence);

        vm.Seed = 999;
        vm.ResetFrequency();
        Assert.Equal(baseline.Frequency, vm.Frequency);
        Assert.Equal(999, vm.Seed);

        vm.ResetOctaves();
        vm.ResetLacunarity();
        vm.ResetPersistence();
        vm.ResetSeed();
        Assert.Equal(baseline, vm.ToOptions());

        int beforeSeed = vm.Seed;
        bool? immediateFlag = null;
        vm.ParameterChanged += immediate => immediateFlag = immediate;
        vm.RandomizeSeed();
        Assert.NotEqual(beforeSeed, vm.Seed);
        Assert.True(immediateFlag);

        vm.ResetToBaseline();
        Assert.Equal(baseline, vm.ToOptions());
    }

    [Fact]
    public void LodAbstraction_BidirectionalCoupling_AlwaysPreservesMinLessOrEqualMax()
    {
        var vm = new LodAbstractionStageViewModel();
        var events = new List<bool>();
        vm.ParameterChanged += events.Add;

        vm.MinNonSalientKernelSize = 14;
        Assert.Equal(14, vm.MinNonSalientKernelSize);
        Assert.Equal(14, vm.MaxNonSalientKernelSize);
        Assert.Single(events);
        Assert.False(events[0]);
        vm.ToOptions().Validate();

        vm.MaxNonSalientKernelSize = 6;
        Assert.Equal(6, vm.MaxNonSalientKernelSize);
        Assert.Equal(6, vm.MinNonSalientKernelSize);
        vm.ToOptions().Validate();

        vm.MinNonSalientKernelSize = 0;
        vm.MaxNonSalientKernelSize = -5;
        Assert.Equal(1, vm.MinNonSalientKernelSize);
        Assert.Equal(1, vm.MaxNonSalientKernelSize);
        vm.ToOptions().Validate();
    }

    [Fact]
    public void FinishingToneCurve_RealTimeClamping_PreservesStrictInvariants()
    {
        var vm = new FinishingStageViewModel();

        // Try pushing MinTone past MaxTone (0.96) and above 1.0.
        vm.MinTone = 0.99;
        Assert.InRange(vm.MinTone, 0.0, vm.MaxTone);
        Assert.True(vm.MinTone < vm.MaxTone);

        vm.MinTone = -0.3;
        Assert.Equal(0.0, vm.MinTone);

        // Try pushing MaxTone below MinTone or above 1.0.
        vm.MinTone = 0.4;
        vm.MaxTone = 0.2;
        Assert.True(vm.MinTone < vm.MaxTone);
        Assert.True(vm.MaxTone <= 1.0);

        vm.MaxTone = 1.5;
        Assert.Equal(1.0, vm.MaxTone);

        // Try ShadowKnee / HighlightKnee boundary clamping.
        vm.ShadowKnee = 0.0;
        Assert.True(vm.ShadowKnee > 0.0);

        vm.ShadowKnee = 0.95;
        Assert.True(vm.ShadowKnee < vm.HighlightKnee);

        vm.HighlightKnee = 1.0;
        Assert.True(vm.HighlightKnee < 1.0);

        vm.HighlightKnee = 0.01;
        Assert.True(vm.ShadowKnee < vm.HighlightKnee);

        // Non-finite inputs must be ignored without corrupting invariants.
        vm.MinTone = double.NaN;
        vm.MaxTone = double.PositiveInfinity;
        vm.ShadowKnee = double.NegativeInfinity;
        vm.HighlightKnee = double.NaN;

        vm.ToToneCurveOptions().Validate();
    }

    [Fact]
    public void FinishingFxaaAndOtherBounds_RejectInvalidValues()
    {
        var vm = new FinishingStageViewModel();

        vm.RelativeContrastThreshold = 0.0;
        Assert.True(vm.RelativeContrastThreshold > 0.0 && vm.RelativeContrastThreshold <= 1.0);

        vm.RelativeContrastThreshold = 2.0;
        Assert.Equal(1.0, vm.RelativeContrastThreshold);

        vm.SubpixelBlendStrength = -1.0;
        Assert.Equal(0.0, vm.SubpixelBlendStrength);
        vm.SubpixelBlendStrength = 2.0;
        Assert.Equal(1.0, vm.SubpixelBlendStrength);

        vm.AbsoluteContrastThreshold = -0.5;
        Assert.Equal(0.0, vm.AbsoluteContrastThreshold);

        vm.MaxSpanSearchSteps = 0;
        Assert.Equal(1, vm.MaxSpanSearchSteps);

        vm.EdgeIntensity = -1.0;
        vm.EdgeBlurSigma = -1.0;
        vm.TurbulenceIntensity = -1.0;
        vm.DensityStrength = -1.0;
        vm.WarpAmplitude = -1.0;

        vm.ToEdgeDarkeningOptions().Validate();
        vm.ToTurbulenceFlowOptions().Validate();
        vm.ToPaperGranulationOptions().Validate();
        vm.ToFxaaOptions().Validate();
    }

    [Fact]
    public void HandTremorPaperColor_HexRgbAndStructStaySynchronized()
    {
        var vm = new HandTremorStageViewModel();
        var changedProperties = new HashSet<string?>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        vm.PaperColorHex = "#1a2b3c";
        Assert.Equal(new Rgb8(0x1A, 0x2B, 0x3C), vm.PaperColor);
        Assert.Equal(0x1A, vm.PaperColorR);
        Assert.Equal(0x2B, vm.PaperColorG);
        Assert.Equal(0x3C, vm.PaperColorB);
        Assert.Equal("#1A2B3C", vm.PaperColorHex);
        Assert.Contains(nameof(HandTremorStageViewModel.PaperColor), changedProperties);
        Assert.Contains(nameof(HandTremorStageViewModel.PaperColorR), changedProperties);

        vm.PaperColorR = 200;
        vm.PaperColorG = 100;
        vm.PaperColorB = 50;
        Assert.Equal(new Rgb8(200, 100, 50), vm.PaperColor);
        Assert.Equal("#C86432", vm.PaperColorHex);

        vm.PaperColor = new Rgb8(10, 20, 30);
        Assert.Equal(10, vm.PaperColorR);
        Assert.Equal(20, vm.PaperColorG);
        Assert.Equal(30, vm.PaperColorB);
        Assert.Equal("#0A141E", vm.PaperColorHex);

        // Invalid hex strings must not corrupt synced color state.
        vm.PaperColorHex = "not-hex";
        Assert.Equal(new Rgb8(10, 20, 30), vm.PaperColor);
        Assert.Equal("#0A141E", vm.PaperColorHex);

        // Support hex without leading '#'.
        vm.PaperColorHex = "ABCDEF";
        Assert.Equal(new Rgb8(0xAB, 0xCD, 0xEF), vm.PaperColor);
        Assert.Equal("#ABCDEF", vm.PaperColorHex);
    }

    [Fact]
    public void ColorTransfer_ModeSwitchingCustomAndCopyMatched_WorksCleanly()
    {
        var vm = new ColorTransferStageViewModel();
        Assert.Equal(PaletteMode.Auto, vm.PaletteMode);
        Assert.Null(vm.ToTargetPalette());

        // CopyMatchedToCustom is a safe no-op before a matched palette exists.
        vm.CopyMatchedToCustom();
        Assert.Equal(PaletteMode.Auto, vm.PaletteMode);

        bool? immediate = null;
        vm.ParameterChanged += flag => immediate = flag;

        // Setting ActiveMatchedPalette is informational only and must not trigger ParameterChanged.
        vm.ActiveMatchedPalette = WatercolorPalettes.CoastalCerulean;
        Assert.Null(immediate);

        vm.CopyMatchedToCustom();
        Assert.True(immediate);
        Assert.Equal(PaletteMode.Custom, vm.PaletteMode);
        Assert.Equal(WatercolorPalettes.CoastalCerulean.Name, vm.CustomPaletteName);
        Assert.Equal(WatercolorPalettes.CoastalCerulean, vm.ToTargetPalette());

        // Blank custom palette name still yields a valid ColorTransferProfile.
        vm.CustomPaletteName = "   ";
        vm.StdDevL = -5.0;
        vm.StdDevA = -1.0;
        vm.StdDevB = -2.0;
        var built = vm.ToTargetPalette();
        Assert.NotNull(built);
        built!.Validate();

        vm.PaletteMode = PaletteMode.BuiltIn;
        vm.SelectedBuiltInPalette = WatercolorPalettes.CrimsonFloral;
        Assert.Equal(WatercolorPalettes.CrimsonFloral, vm.ToTargetPalette());
    }

    [Fact]
    public void SaliencySegmentationAndWetInWet_BoundsClampToValidDomains()
    {
        var saliency = new SaliencyStageViewModel();
        saliency.EgbiBlurSigma = -1.0;
        saliency.Egbi.KFactor = -10.0;
        saliency.Egbi.Epsilon = -0.5;
        saliency.Egbi.MinRegionSize = -5;
        saliency.RegionalContrastSaliency.CenterPriorWeight = -2.0;
        saliency.SaliencyCut.MaxOuterIterations = 0;
        saliency.SaliencyCut.TrimapBandRadius = 0;
        saliency.SaliencyCut.UseMaxWorkingDimension = true;
        saliency.SaliencyCut.MaxWorkingDimension = 1;
        saliency.GrabCut.Iterations = 0;
        saliency.GrabCut.MixtureComponentCount = 0;
        saliency.GrabCut.SmoothnessWeight = -1.0;
        saliency.SaliencyMask.FixedThreshold = 1.5;
        saliency.SaliencyMask.MinAreaFraction = -0.2;
        saliency.SaliencyDistanceField.UseExplicitSmoothingSigma = true;
        saliency.SaliencyDistanceField.SmoothingSigma = -3.0;
        saliency.SaliencyDistanceField.DefaultSigmaScale = -0.1;

        Assert.Equal(2, saliency.SaliencyCut.MaxWorkingDimension);
        Assert.Equal(1.0, saliency.SaliencyMask.FixedThreshold);
        saliency.Egbi.ToOptions().Validate();
        saliency.RegionalContrastSaliency.ToOptions().Validate();
        saliency.ToSaliencyCutOptions().Validate();
        saliency.SaliencyDistanceField.ToOptions().Validate();

        var seg = new SegmentationStageViewModel();
        seg.SpatialRadius = 0;
        seg.RangeRadius = 0.0;
        seg.MinRegionSize = -4;
        seg.MaxIterations = 0;
        seg.ConvergenceEpsilon = -1.0;
        Assert.Equal(1, seg.SpatialRadius);
        Assert.True(seg.RangeRadius > 0.0);
        Assert.True(seg.ConvergenceEpsilon > 0.0);
        seg.ToOptions().Validate();

        var wet = new WetInWetStageViewModel();
        wet.ColorGradientThreshold = -1.0;
        wet.SalientHueThresholdDegrees = 200.0;
        wet.NonSalientHueThresholdDegrees = -10.0;
        wet.MinIntensityDifference = -0.5;
        wet.SalientOverlapThreshold = 1.5;
        wet.ScatterRadius = 0;
        wet.SeedProbability = -0.2;
        wet.EllipticalMajorRadius = 0.0;
        wet.EllipticalMinorRadius = -1.0;
        wet.EllipticalSigmaScale = 0.0;
        Assert.Equal(180.0, wet.SalientHueThresholdDegrees);
        Assert.Equal(0.0, wet.NonSalientHueThresholdDegrees);
        Assert.Equal(1, wet.ScatterRadius);
        Assert.True(wet.EllipticalMajorRadius > 0.0 && wet.EllipticalMinorRadius > 0.0 && wet.EllipticalSigmaScale > 0.0);
        wet.ToOptions().Validate();
    }

    [Fact]
    public void PerlinNoiseViewModel_IsSeedModified_TracksSeedAndBaselineChanges()
    {
        var baseline = new PerlinNoiseOptions(Seed: 42, Frequency: 0.12, Octaves: 5, Lacunarity: 2.2, Persistence: 0.45);
        var vm = new PerlinNoiseViewModel(baseline);
        var seedModifiedNotifications = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PerlinNoiseViewModel.IsSeedModified))
            {
                seedModifiedNotifications.Add(vm.IsSeedModified);
            }
        };

        Assert.False(vm.IsSeedModified);

        vm.Seed = 123;
        Assert.True(vm.IsSeedModified);
        Assert.Equal([true], seedModifiedNotifications);

        vm.ResetSeed();
        Assert.False(vm.IsSeedModified);
        Assert.Equal(baseline.Seed, vm.Seed);
        Assert.Equal([true, false], seedModifiedNotifications);

        vm.RandomizeSeed();
        Assert.True(vm.IsSeedModified);

        vm.ResetToBaseline();
        Assert.False(vm.IsSeedModified);

        // SetBaseline updates IsSeedModified when baseline seed diverges from current seed.
        vm.SetBaseline(baseline with { Seed = 777 });
        Assert.True(vm.IsSeedModified);

        // LoadFrom matching baseline seed clears IsSeedModified.
        vm.LoadFrom(baseline with { Seed = 777 });
        Assert.False(vm.IsSeedModified);
    }

    [Fact]
    public void WetInWetStageViewModel_SelectableFilteringModes_ExcludesDisabledAndPreservesBackCompat()
    {
        Assert.Equal(
            [WetInWetMode.Default, WetInWetMode.ExcludeSalientBoundaries, WetInWetMode.ExcludeSalientRegions],
            WetInWetStageViewModel.SelectableFilteringModes);
        Assert.DoesNotContain(WetInWetMode.Disabled, WetInWetStageViewModel.SelectableFilteringModes);

        var vm = new WetInWetStageViewModel();
        Assert.True(vm.IsWetInWetActive);

        // Toggling EnableWetInWet off preserves the selected filtering mode.
        vm.Mode = WetInWetMode.ExcludeSalientBoundaries;
        vm.EnableWetInWet = false;
        Assert.False(vm.IsWetInWetActive);
        Assert.Equal(WetInWetMode.ExcludeSalientBoundaries, vm.Mode);

        // Legacy preset with WetInWetMode.Disabled remains round-trippable.
        WetInWetOptions disabledOpts = WatercolorOptions.Default.WetInWet with { Mode = WetInWetMode.Disabled };
        vm.LoadFrom(enableWetInWet: true, disabledOpts);
        Assert.Equal(WetInWetMode.Disabled, vm.Mode);
        Assert.False(vm.IsWetInWetActive);
        Assert.Equal(WetInWetMode.Disabled, vm.ToOptions().Mode);

        // Turning EnableWetInWet on when Mode was Disabled restores a usable filtering mode.
        vm.LoadFrom(enableWetInWet: false, disabledOpts);
        vm.EnableWetInWet = true;
        Assert.True(vm.EnableWetInWet);
        Assert.NotEqual(WetInWetMode.Disabled, vm.Mode);
        Assert.True(vm.IsWetInWetActive);
    }
}
