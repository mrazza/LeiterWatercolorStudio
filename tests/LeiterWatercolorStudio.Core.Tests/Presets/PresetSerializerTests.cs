namespace LeiterWatercolorStudio.Core.Tests.Presets;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
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
using LeiterWatercolorStudio.Core.Presets;
using Xunit;

public class PresetSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_FullNonDefaultRoundTrip_PreservesAllFields()
    {
        var customPalette = new ColorTransferProfile(
            "Sunset Wash",
            new LabColorStatistics(68.5, 11.2, 18.4, 15.1, 9.3, 12.7),
            Illuminant.D65);

        var fullOptions = new WatercolorOptions(
            TargetPalette: customPalette,
            ColorTransferStrength: 0.72,
            Egbi: new EgbiOptions(
                KFactor: 145.0,
                Epsilon: 1.75,
                MinRegionSize: 280,
                Connectivity: GridConnectivity.FourConnected),
            RegionalContrastSaliency: new RegionalContrastSaliencyOptions(
                EnableSmoothing: false,
                ComputeBorderRegions: false,
                CenterPriorWeight: 4.5),
            SaliencyCut: new SaliencyCutOptions(
                MaxOuterIterations: 6,
                TrimapBandRadius: 14,
                GrabCut: new GrabCutOptions(
                    Iterations: 3,
                    MixtureComponentCount: 4,
                    SmoothnessWeight: 62.5),
                Mask: new SaliencyMaskOptions(
                    Method: SaliencyThresholdMethod.Otsu,
                    FixedThreshold: 0.35,
                    RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
                    MinAreaFraction: 0.04,
                    Connectivity: GridConnectivity.FourConnected),
                MaxWorkingDimension: 512),
            SaliencyDistanceField: new SaliencyDistanceFieldOptions(
                SmoothingSigma: 12.5,
                DefaultSigmaScale: 0.045),
            MeanShift: new MeanShiftOptions(
                SpatialRadius: 11,
                RangeRadius: 7.2,
                MinRegionSize: 90,
                MaxIterations: 25,
                ConvergenceEpsilon: 0.08,
                Connectivity: GridConnectivity.FourConnected),
            LodAbstraction: new LodAbstractionOptions(
                SalientKernelSize: 3,
                MinNonSalientKernelSize: 5,
                MaxNonSalientKernelSize: 13,
                DistanceKernelScale: 12.0,
                DistanceKernelBias: 0.45,
                CrossSegmentColorScale: 0.25,
                SalientOverlapThreshold: 0.6),
            WetInWet: new WetInWetOptions(
                Mode: WetInWetMode.ExcludeSalientBoundaries,
                ColorGradientThreshold: 0.14,
                SalientHueThresholdDegrees: 25.0,
                NonSalientHueThresholdDegrees: 75.0,
                MinIntensityDifference: 0.05,
                SalientOverlapThreshold: 0.55,
                ScatterRadius: 6,
                SeedProbability: 0.6,
                EllipticalFilter: new OrientedEllipticalFilterOptions(8.0, 3.0, 0.7),
                Seed: 4242),
            HandTremor: new HandTremorOptions(
                Amplitude: 4.2,
                SalientAmplitudeScale: 0.4,
                SimilarHueThresholdDegrees: 35.0,
                OverlapDarkening: 0.65,
                PaperColor: new Rgb8(244, 236, 221),
                PrimaryNoise: new PerlinNoiseOptions(Seed: 111, Frequency: 0.06, Octaves: 5, Lacunarity: 2.1, Persistence: 0.45),
                SecondaryNoise: new PerlinNoiseOptions(Seed: 222, Frequency: 0.08, Octaves: 2, Lacunarity: 1.8, Persistence: 0.6),
                MinContrastingChroma: 0.12,
                PaperGapBias: -0.25,
                PaperGapOpacity: 0.8),
            EdgeDarkening: new EdgeDarkeningOptions(
                Intensity: 2.4,
                BlurSigma: 1.1,
                GradientOperator: GradientOperator.Scharr),
            TurbulenceFlow: new TurbulenceFlowOptions(
                Intensity: 0.55,
                Noise: new PerlinNoiseOptions(Seed: 505, Frequency: 0.02, Octaves: 6, Lacunarity: 2.2, Persistence: 0.52)),
            ToneCurve: new PiecewiseToneCurveOptions(
                MinTone: 0.08,
                MaxTone: 0.92,
                ShadowKnee: 0.2,
                HighlightKnee: 0.8),
            PaperGranulation: new PaperGranulationOptions(
                DensityStrength: 0.42,
                WarpAmplitude: 0.85,
                GrainNoise: new PerlinNoiseOptions(Seed: 808, Frequency: 0.22, Octaves: 5, Lacunarity: 2.05, Persistence: 0.6)),
            Fxaa: new FxaaOptions(
                RelativeContrastThreshold: 0.1,
                AbsoluteContrastThreshold: 0.05,
                SubpixelBlendStrength: 0.9,
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

        var timings = new Dictionary<WatercolorStep, double>
        {
            [WatercolorStep.ColorTransfer] = 12.5,
            [WatercolorStep.Fxaa] = 3.25,
        };

        var document = new PresetDocument
        {
            SchemaVersion = PresetDocument.CurrentSchemaVersion,
            Name = "Custom Sunset Study",
            CalibrationReferenceDimension = 1024.0,
            PaletteMode = PaletteMode.Custom,
            Options = fullOptions,
            StepTimingsMs = timings,
        };

        string json = PresetSerializer.Serialize(document);

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"#F4ECDD\"", json);
        Assert.Contains("\"paletteMode\": \"Custom\"", json);
        Assert.Contains("\"illuminant\": \"D65\"", json);

        PresetDocument roundTripped = PresetSerializer.Deserialize(json);

        Assert.Equal(document.SchemaVersion, roundTripped.SchemaVersion);
        Assert.Equal(document.Name, roundTripped.Name);
        Assert.Equal(document.CalibrationReferenceDimension, roundTripped.CalibrationReferenceDimension);
        Assert.Equal(document.PaletteMode, roundTripped.PaletteMode);
        Assert.Equal(document.Options, roundTripped.Options);
        Assert.NotNull(roundTripped.StepTimingsMs);
        Assert.Equal(12.5, roundTripped.StepTimingsMs![WatercolorStep.ColorTransfer]);
        Assert.Equal(3.25, roundTripped.StepTimingsMs[WatercolorStep.Fxaa]);
        roundTripped.Options.Validate();
    }

    [Fact]
    public void Deserialize_ReadsRgb8AsHexOrObject()
    {
        var baseDoc = new PresetDocument
        {
            Name = "Hex Test",
            Options = WatercolorOptions.Default with
            {
                HandTremor = HandTremorOptions.Default with { PaperColor = new Rgb8(0x1A, 0x2B, 0x3C) }
            }
        };

        string hexJson = PresetSerializer.Serialize(baseDoc).Replace("#1A2B3C", "#1a2b3c");
        PresetDocument fromLowerHex = PresetSerializer.Deserialize(hexJson);
        Assert.Equal(new Rgb8(0x1A, 0x2B, 0x3C), fromLowerHex.Options.HandTremor.PaperColor);

        string objectJson = hexJson.Replace("\"#1a2b3c\"", "{\"r\": 26, \"g\": 43, \"b\": 60}");
        PresetDocument fromObject = PresetSerializer.Deserialize(objectJson);
        Assert.Equal(new Rgb8(26, 43, 60), fromObject.Options.HandTremor.PaperColor);
    }

    [Theory]
    [InlineData("{\"schemaVersion\": 2, \"name\": \"Future\"}")]
    [InlineData("{\"schemaVersion\": 0, \"name\": \"Zero\"}")]
    [InlineData("{\"name\": \"MissingVersion\"}")]
    [InlineData("{\"schemaVersion\": 1, \"options\": null}")]
    [InlineData("{\"schemaVersion\": 1, \"calibrationReferenceDimension\": -5.0}")]
    [InlineData("{\"schemaVersion\": 1, \"paletteMode\": 999}")]
    public void Deserialize_InvalidSchemaOrDocumentMetadata_Throws(string invalidJson)
    {
        Assert.ThrowsAny<Exception>(() => PresetSerializer.Deserialize(invalidJson));
    }

    [Fact]
    public void Deserialize_InvalidNestedOptionsOrConverters_Throws()
    {
        string baselineJson = PresetSerializer.Serialize(new PresetDocument
        {
            Options = WatercolorOptions.Default with
            {
                TargetPalette = WatercolorPalettes.BalancedWash
            }
        });

        string badStrength = baselineJson.Replace("\"colorTransferStrength\": 1", "\"colorTransferStrength\": 1.5");
        Assert.Throws<ArgumentOutOfRangeException>(() => PresetSerializer.Deserialize(badStrength));

        string badToneCurve = baselineJson
            .Replace("\"minTone\": 0.05", "\"minTone\": 0.95")
            .Replace("\"maxTone\": 0.96", "\"maxTone\": 0.50");
        Assert.Throws<ArgumentOutOfRangeException>(() => PresetSerializer.Deserialize(badToneCurve));

        string badPaletteName = baselineJson.Replace("\"Balanced Wash\"", "\"   \"");
        Assert.Throws<ArgumentException>(() => PresetSerializer.Deserialize(badPaletteName));

        string badPaletteStdDev = baselineJson.Replace("\"stdDevL\": 17.36", "\"stdDevL\": -2.0");
        Assert.Throws<ArgumentOutOfRangeException>(() => PresetSerializer.Deserialize(badPaletteStdDev));

        string badHexColor = baselineJson.Replace("#FAF8F2", "#ZZ1122");
        Assert.Throws<JsonException>(() => PresetSerializer.Deserialize(badHexColor));

        string badRgbObject = baselineJson.Replace("\"#FAF8F2\"", "{\"r\": 256, \"g\": 10, \"b\": 10}");
        Assert.Throws<JsonException>(() => PresetSerializer.Deserialize(badRgbObject));

        string incompleteRgbObject = baselineJson.Replace("\"#FAF8F2\"", "{\"r\": 10, \"g\": 20}");
        Assert.Throws<JsonException>(() => PresetSerializer.Deserialize(incompleteRgbObject));
    }

    [Fact]
    public async Task SaveToFileAsyncAndLoadFromFileAsync_RoundTripsFile()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"leiter-preset-test-{Guid.NewGuid():N}");
        string filePath = Path.Combine(tempRoot, "nested", "my_preset.json");

        try
        {
            var preset = new PresetDocument
            {
                Name = "Disk Preset",
                CalibrationReferenceDimension = 640.0,
                PaletteMode = PaletteMode.BuiltIn,
                Options = WatercolorOptions.Default with
                {
                    TargetPalette = WatercolorPalettes.CoastalCerulean,
                    EnableFxaa = false
                }
            };

            await PresetSerializer.SaveToFileAsync(preset, filePath);
            Assert.True(File.Exists(filePath));

            PresetDocument loaded = await PresetSerializer.LoadFromFileAsync(filePath);
            Assert.Equal(preset.Name, loaded.Name);
            Assert.Equal(preset.CalibrationReferenceDimension, loaded.CalibrationReferenceDimension);
            Assert.Equal(preset.PaletteMode, loaded.PaletteMode);
            Assert.Equal(preset.Options, loaded.Options);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void AdaptForImageSize_RespectsScaleFlagAndCalibrationReference()
    {
        var preset = new PresetDocument
        {
            CalibrationReferenceDimension = 800.0,
            Options = WatercolorOptions.Default with
            {
                TargetPalette = WatercolorPalettes.WarmOchreEarth
            }
        };

        var targetSize = new Size(1600, 1600);

        WatercolorOptions unscaled = PresetSerializer.AdaptForImageSize(preset, targetSize, scaleToSize: false);
        Assert.Equal(preset.Options, unscaled);

        WatercolorOptions scaled = PresetSerializer.AdaptForImageSize(preset, targetSize, scaleToSize: true);
        WatercolorOptions expected = WatercolorOptions.ForSize(targetSize, preset.Options, 800.0);
        Assert.Equal(expected, scaled);
        Assert.NotEqual(preset.Options.MeanShift.SpatialRadius, scaled.MeanShift.SpatialRadius);

        PresetDocument uncalibrated = preset with { CalibrationReferenceDimension = 0.0 };
        WatercolorOptions uncalibratedResult = PresetSerializer.AdaptForImageSize(uncalibrated, targetSize, scaleToSize: true);
        Assert.Equal(preset.Options, uncalibratedResult);
    }

    [Fact]
    public void Deserialize_MinimalJsonAndNullHandTremorPaperColor_FallsBackToDefaults()
    {
        const string minimalJson = """
            {
              "schemaVersion": 1,
              "options": {
                "handTremor": {
                  "paperColor": null
                }
              }
            }
            """;

        PresetDocument document = PresetSerializer.Deserialize(minimalJson);
        Assert.Equal(WatercolorOptions.Default, document.Options);
        Assert.Equal(HandTremorOptions.DefaultPaperColor, document.Options.HandTremor.PaperColor);
    }
}
