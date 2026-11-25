using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Cli;
using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Presets;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Cli;

public class HeadlessExportRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_FinalDefaultOutput_WritesFullResPngAndNoSidecar()
    {
        string tempDir = CreateTempDirectory();
        try
        {
            string inputPath = WriteSyntheticInput(tempDir, "scene.png", 48, 36);

            HeadlessExportResult result = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
            });

            string expectedOutput = Path.Combine(tempDir, "scene_watercolor.png");
            Assert.Equal(expectedOutput, Assert.Single(result.OutputFiles));
            Assert.Null(result.OutputDirectory);
            Assert.Equal(48, result.NativeWidth);
            Assert.Equal(36, result.NativeHeight);
            Assert.Equal(48, result.RenderWidth);
            Assert.Equal(36, result.RenderHeight);
            Assert.Equal(BaselineMode.ForImage, result.BaselineMode);
            Assert.True(File.Exists(expectedOutput));
            Assert.False(File.Exists(Path.Combine(tempDir, "preset.json")));

            SequentialMatrix<Rgb8> loaded = ImageExtensions.LoadRgb8(expectedOutput);
            Assert.Equal(new Size(48, 36), loaded.Size);
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DownscaledResolutionAndWebpOutput_ResizesAndOverwrites()
    {
        string tempDir = CreateTempDirectory();
        try
        {
            string inputPath = WriteSyntheticInput(tempDir, "wide.png", 64, 32);
            string outputPath = Path.Combine(tempDir, "out.webp");
            await File.WriteAllTextAsync(outputPath, "existing-placeholder");

            HeadlessExportResult result = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = outputPath,
                Baseline = BaselineMode.ForSize,
                MaxDimension = 32,
            });

            Assert.Equal(outputPath, Assert.Single(result.OutputFiles));
            Assert.Equal(64, result.NativeWidth);
            Assert.Equal(32, result.NativeHeight);
            Assert.Equal(32, result.RenderWidth);
            Assert.Equal(16, result.RenderHeight);
            Assert.Equal(BaselineMode.ForSize, result.BaselineMode);

            SequentialMatrix<Rgb8> loaded = ImageExtensions.LoadRgb8(outputPath);
            Assert.Equal(new Size(32, 16), loaded.Size);
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AllStagesMode_Writes24PngsWithoutSidecar()
    {
        string tempDir = CreateTempDirectory();
        try
        {
            string inputPath = WriteSyntheticInput(tempDir, "photo.png", 40, 40);
            string stagesDir = Path.Combine(tempDir, "all_out");

            HeadlessExportResult result = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = stagesDir,
                Mode = CliExportMode.AllStages,
                Baseline = BaselineMode.Default,
            });

            Assert.Equal(stagesDir, result.OutputDirectory);
            Assert.Equal(24, result.OutputFiles.Count);
            Assert.False(File.Exists(Path.Combine(stagesDir, "preset.json")));

            foreach (StageExportEntry entry in ExportService.StageManifest)
            {
                string stageFile = Path.Combine(stagesDir, entry.FileName);
                Assert.True(File.Exists(stageFile), $"Missing {entry.FileName}");
            }
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SingleStageWithFileExtensionVsDirectory_RespectsOutputShape()
    {
        string tempDir = CreateTempDirectory();
        try
        {
            string inputPath = WriteSyntheticInput(tempDir, "sample.png", 36, 36);
            Assert.True(StageSelectorResolver.TryResolve(["05_hand_tremor"], out var tremorOnly, out _));

            string directFile = Path.Combine(tempDir, "custom_tremor.png");
            HeadlessExportResult fileResult = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = directFile,
                Mode = CliExportMode.Stages,
                SelectedStages = tremorOnly,
                Baseline = BaselineMode.ForSize,
            });

            Assert.Null(fileResult.OutputDirectory);
            Assert.Equal(directFile, Assert.Single(fileResult.OutputFiles));
            Assert.True(File.Exists(directFile));

            string stageDir = Path.Combine(tempDir, "dir_out");
            HeadlessExportResult dirResult = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = stageDir,
                Mode = CliExportMode.Stages,
                SelectedStages = tremorOnly,
                Baseline = BaselineMode.ForSize,
            });

            Assert.Equal(stageDir, dirResult.OutputDirectory);
            string expectedInDir = Path.Combine(stageDir, "05_hand_tremor.png");
            Assert.Equal(expectedInDir, Assert.Single(dirResult.OutputFiles));
            Assert.True(File.Exists(expectedInDir));
            Assert.False(File.Exists(Path.Combine(stageDir, "preset.json")));

            Assert.True(StageSelectorResolver.TryResolve(["wet-in-wet"], out var wetGroup, out _));
            string wetDir = Path.Combine(tempDir, "wet_out");
            HeadlessExportResult wetResult = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = wetDir,
                Mode = CliExportMode.Stages,
                SelectedStages = wetGroup,
                Baseline = BaselineMode.ForSize,
                MaxDimension = 512, // larger than 36x36: never upscales
            });

            Assert.Equal(36, wetResult.RenderWidth);
            Assert.Equal(36, wetResult.RenderHeight);
            Assert.Equal(3, wetResult.OutputFiles.Count);
            Assert.False(File.Exists(Path.Combine(wetDir, "preset.json")));
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PresetInput_RespectsThresholdAndExplicitScalingOverrides()
    {
        string tempDir = CreateTempDirectory();
        try
        {
            string inputPath = WriteSyntheticInput(tempDir, "target.png", 40, 40);
            string presetPath = Path.Combine(tempDir, "warm.json");

            var preset = new PresetDocument
            {
                Name = "Warm Test",
                CalibrationReferenceDimension = 400.0, // > 5% difference from 40
                PaletteMode = PaletteMode.BuiltIn,
                Options = WatercolorOptions.Default with
                {
                    TargetPalette = WatercolorPalettes.WarmOchreEarth,
                    EnableHandTremor = false,
                },
            };
            await PresetSerializer.SaveToFileAsync(preset, presetPath);

            HeadlessExportResult autoScaled = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = Path.Combine(tempDir, "auto.png"),
                PresetPath = presetPath,
                PresetScaling = CliPresetScalingMode.Auto,
            });

            Assert.True(autoScaled.PresetScaled);
            Assert.Equal("Warm Test", autoScaled.PresetName);
            Assert.Null(autoScaled.BaselineMode);

            HeadlessExportResult exactResult = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = Path.Combine(tempDir, "exact.png"),
                PresetPath = presetPath,
                PresetScaling = CliPresetScalingMode.Exact,
            });

            Assert.False(exactResult.PresetScaled);

            // Preset calibrated within <= 5% does not auto-scale unless forced.
            string closePresetPath = Path.Combine(tempDir, "close.json");
            await PresetSerializer.SaveToFileAsync(preset with { CalibrationReferenceDimension = 41.0 }, closePresetPath);

            HeadlessExportResult closeAuto = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = Path.Combine(tempDir, "close_auto.png"),
                PresetPath = closePresetPath,
                PresetScaling = CliPresetScalingMode.Auto,
            });
            Assert.False(closeAuto.PresetScaled);

            HeadlessExportResult forcedScale = await HeadlessExportRunner.ExecuteAsync(new CliExportOptions
            {
                InputPath = inputPath,
                OutputPath = Path.Combine(tempDir, "forced.png"),
                PresetPath = closePresetPath,
                PresetScaling = CliPresetScalingMode.Scale,
            });
            Assert.True(forcedScale.PresetScaled);
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    [Fact]
    public async Task ExecuteAsync_InvalidInputsOrExtensions_FailFast()
    {
        string tempDir = CreateTempDirectory();
        try
        {
            string inputPath = WriteSyntheticInput(tempDir, "in.png", 32, 32);

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                HeadlessExportRunner.ExecuteAsync(new CliExportOptions
                {
                    InputPath = Path.Combine(tempDir, "missing.png"),
                }));

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                HeadlessExportRunner.ExecuteAsync(new CliExportOptions
                {
                    InputPath = inputPath,
                    PresetPath = Path.Combine(tempDir, "missing.json"),
                }));

            await Assert.ThrowsAsync<NotSupportedException>(() =>
                HeadlessExportRunner.ExecuteAsync(new CliExportOptions
                {
                    InputPath = inputPath,
                    OutputPath = Path.Combine(tempDir, "bad.xyz"),
                }));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                HeadlessExportRunner.ExecuteAsync(new CliExportOptions
                {
                    InputPath = inputPath,
                    Mode = CliExportMode.AllStages,
                    OutputPath = Path.Combine(tempDir, "bundle.png"),
                }));

            Assert.True(StageSelectorResolver.TryResolve(["saliency"], out var saliencyGroup, out _));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                HeadlessExportRunner.ExecuteAsync(new CliExportOptions
                {
                    InputPath = inputPath,
                    Mode = CliExportMode.Stages,
                    SelectedStages = saliencyGroup,
                    OutputPath = Path.Combine(tempDir, "bundle.jpg"),
                }));
        }
        finally
        {
            CleanupDirectory(tempDir);
        }
    }

    private static string WriteSyntheticInput(string dir, string fileName, int width, int height)
    {
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool center = x >= width / 4 && x < (3 * width) / 4 && y >= height / 4 && y < (3 * height) / 4;
                matrix[x, y] = center
                    ? new Rgb8((byte)(180 + (40 * x / width)), (byte)(50 + (50 * y / height)), 60)
                    : new Rgb8((byte)(40 + (70 * x / width)), (byte)(100 + (90 * y / height)), 190);
            }
        }

        string path = Path.Combine(dir, fileName);
        matrix.SaveImage(path);
        return path;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"leiter-cli-runner-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CleanupDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
