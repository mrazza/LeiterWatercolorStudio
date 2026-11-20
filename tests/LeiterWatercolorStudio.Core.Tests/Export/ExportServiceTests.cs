namespace LeiterWatercolorStudio.Core.Tests.Export;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Presets;
using Xunit;

public class ExportServiceTests
{
    private static readonly string[] ExpectedStageFiles =
    [
        "00_input.png",
        "01_color_transfer.png",
        "02a_egbi_preblur.png",
        "02b_egbi_regions_avg.png",
        "02b_egbi_regions_random.png",
        "02c_saliency_rc.png",
        "02d_saliency_cut_mask.png",
        "02e_distance_field_raw.png",
        "02f_distance_field_smoothed.png",
        "03a_meanshift_filtered.png",
        "03a_meanshift_regions_avg.png",
        "03a_meanshift_regions_random.png",
        "03b_lod_abstraction.png",
        "04_wet_in_wet.png",
        "04_wet_area_mask.png",
        "04_wet_qualifying_boundaries.png",
        "05_hand_tremor.png",
        "06a_edge_darkening_density.png",
        "06b_turbulence_density.png",
        "06c_pigment_combined.png",
        "06d_tone_curve.png",
        "06e_paper_height_map.png",
        "06e_paper_granulation.png",
        "06f_final_watercolor.png",
    ];

    [Fact]
    public async Task ExportFinalImageAsync_SavesPngJpgAndWebp()
    {
        SequentialMatrix<Rgb8> image = CreateSyntheticScene(32, 24);
        string tempDir = CreateTempDirectory();

        try
        {
            foreach (string ext in new[] { ".png", ".jpg", ".webp" })
            {
                string outputPath = Path.Combine(tempDir, "nested", $"final{ext}");
                await ExportService.ExportFinalImageAsync(image, outputPath);

                var fileInfo = new FileInfo(outputPath);
                Assert.True(fileInfo.Exists, $"Missing {outputPath}");
                Assert.True(fileInfo.Length > 0, $"Empty {outputPath}");

                SequentialMatrix<Rgb8> reloaded = ImageExtensions.LoadRgb8(outputPath);
                Assert.Equal(image.Size, reloaded.Size);

                if (ext == ".png")
                {
                    Assert.Equal(image[0, 0], reloaded[0, 0]);
                    Assert.Equal(image[16, 12], reloaded[16, 12]);
                }
            }

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                ExportService.ExportFinalImageAsync(image, Path.Combine(tempDir, "final.xyz")));
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExportAllStagesAsync_NormalRun_WritesAll24PngsAndPresetSidecar()
    {
        SequentialMatrix<Rgb8> source = CreateSyntheticScene(48, 48);
        WatercolorOptions options = WatercolorOptions.ForImage(source);
        WatercolorResult result = WatercolorPipeline.RenderDetailed(source, options: options);
        string tempDir = CreateTempDirectory();

        try
        {
            var reported = new List<(int Completed, int Total, string FileName)>();
            var progress = new SyncProgress<(int Completed, int Total, string FileName)>(reported.Add);

            await ExportService.ExportAllStagesAsync(
                source,
                result,
                options,
                tempDir,
                includePresetSidecar: true,
                calibrationReferenceDimension: 48.0,
                paletteMode: PaletteMode.Auto,
                progress: progress);

            Assert.Equal(25, reported.Count);
            Assert.Equal((1, 25, ExpectedStageFiles[0]), reported[0]);
            Assert.Equal((25, 25, "preset.json"), reported[^1]);

            foreach (string fileName in ExpectedStageFiles)
            {
                string path = Path.Combine(tempDir, fileName);
                var info = new FileInfo(path);
                Assert.True(info.Exists, $"Missing stage PNG: {fileName}");
                Assert.True(info.Length > 0, $"Empty stage PNG: {fileName}");

                SequentialMatrix<Rgb8> loaded = ImageExtensions.LoadRgb8(path);
                Assert.Equal(source.Size, loaded.Size);
            }

            string sidecarPath = Path.Combine(tempDir, "preset.json");
            Assert.True(File.Exists(sidecarPath));
            PresetDocument sidecar = await PresetSerializer.LoadFromFileAsync(sidecarPath);
            Assert.Equal(PresetDocument.CurrentSchemaVersion, sidecar.SchemaVersion);
            Assert.Equal(48.0, sidecar.CalibrationReferenceDimension);
            Assert.Equal(PaletteMode.Auto, sidecar.PaletteMode);
            Assert.Equal(options, sidecar.Options);
            Assert.NotNull(sidecar.StepTimingsMs);
            Assert.Equal(result.StepTimings.Count, sidecar.StepTimingsMs!.Count);
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExportAllStagesAsync_BypassedStages_HandlesNullFieldsAndWritesAll24Pngs()
    {
        SequentialMatrix<Rgb8> source = CreateSyntheticScene(40, 40);
        WatercolorOptions options = WatercolorOptions.ForSize(source.Size) with
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

        WatercolorResult result = WatercolorPipeline.RenderDetailed(source, options: options);
        Assert.Null(result.EdgeDensityField);
        Assert.Null(result.TurbulenceDensityField);
        Assert.Null(result.PaperHeightMap);

        string tempDir = CreateTempDirectory();
        try
        {
            await ExportService.ExportAllStagesAsync(
                source,
                result,
                options,
                tempDir,
                includePresetSidecar: true,
                paletteMode: PaletteMode.BuiltIn);

            foreach (string fileName in ExpectedStageFiles)
            {
                string path = Path.Combine(tempDir, fileName);
                var info = new FileInfo(path);
                Assert.True(info.Exists, $"Missing bypassed stage PNG: {fileName}");
                Assert.True(info.Length > 0, $"Empty bypassed stage PNG: {fileName}");

                SequentialMatrix<Rgb8> loaded = ImageExtensions.LoadRgb8(path);
                Assert.Equal(source.Size, loaded.Size);
            }

            PresetDocument sidecar = await PresetSerializer.LoadFromFileAsync(Path.Combine(tempDir, "preset.json"));
            Assert.Equal(PaletteMode.BuiltIn, sidecar.PaletteMode);
            Assert.Equal(options, sidecar.Options);
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExportAllStagesAsync_CustomVisualizerAndNoSidecar_InvokesCustomDelegate()
    {
        SequentialMatrix<Rgb8> source = CreateSyntheticScene(24, 24);
        WatercolorOptions options = WatercolorOptions.ForSize(source.Size);
        WatercolorResult result = WatercolorPipeline.RenderDetailed(source, options: options);
        string tempDir = CreateTempDirectory();

        try
        {
            var calls = new List<(StageViewId View, VisualizationMode Mode)>();
            var customMatrix = new SequentialMatrix<Rgb8>(source.Size);
            customMatrix.AsSpan().Fill(new Rgb8(12, 128, 240));

            await ExportService.ExportAllStagesAsync(
                source,
                result,
                options,
                tempDir,
                customVisualizer: (view, mode) =>
                {
                    calls.Add((view, mode));
                    return customMatrix;
                },
                includePresetSidecar: false);

            Assert.Equal(24, calls.Count);
            Assert.False(File.Exists(Path.Combine(tempDir, "preset.json")));

            SequentialMatrix<Rgb8> loadedInput = ImageExtensions.LoadRgb8(Path.Combine(tempDir, "00_input.png"));
            Assert.Equal(new Rgb8(12, 128, 240), loadedInput[0, 0]);
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    private static SequentialMatrix<Rgb8> CreateSyntheticScene(int width, int height)
    {
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inCenterBlock = x >= width / 4 && x < (3 * width) / 4 && y >= height / 4 && y < (3 * height) / 4;
                if (inCenterBlock)
                {
                    byte r = (byte)(180 + (50 * x / width));
                    byte g = (byte)(40 + (60 * y / height));
                    byte b = 45;
                    matrix[x, y] = new Rgb8(r, g, b);
                }
                else
                {
                    byte r = (byte)(30 + (80 * x / width));
                    byte g = (byte)(90 + (110 * y / height));
                    byte b = (byte)(170 + (60 * (x + y) / (width + height)));
                    matrix[x, y] = new Rgb8(r, g, b);
                }
            }
        }

        return matrix;
    }

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), $"leiter-export-test-{Guid.NewGuid():N}");

    private static void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
