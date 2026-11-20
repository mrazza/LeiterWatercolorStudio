namespace LeiterWatercolorStudio.Core.Tests;

using System;
using System.IO;
using System.Threading.Tasks;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using LeiterWatercolorStudio.Core.Presets;
using Xunit;

public class RealImageIntegrationTests
{
    [Fact]
    public async Task RealExampleImage_PipelineEngineStageVisualizersAndExportService_IntegrateEndToEnd()
    {
        string inputPath = FindExampleInputPath();
        SequentialMatrix<Rgb8> nativeSource = ImageExtensions.LoadRgb8(inputPath);

        using var engine = new PipelineEngine(
            debounceDelay: TimeSpan.Zero,
            initialPreviewCap: PreviewResolutionCap.Px512);

        engine.LoadImage(nativeSource, inputPath);
        Assert.NotNull(engine.PreviewSource);
        Assert.True(engine.PreviewSize.Width <= 512 && engine.PreviewSize.Height <= 512);

        WatercolorOptions baseline = engine.ComputeBaseline(BaselineMode.ForImage);
        WatercolorResult? result = await engine.RequestRender(baseline, immediate: true);

        Assert.NotNull(result);
        Assert.Equal(engine.PreviewSize, result!.Image.Size);
        Assert.NotNull(engine.LastRenderedEffectiveOptions);

        Matrix<Rgb8> egbiOverlay = StageVisualizers.RenderView(
            StageViewId.SaliencyEgbiRegions,
            VisualizationMode.RegionBoundariesOverlay,
            engine.PreviewSource!,
            result,
            engine.LastRenderedEffectiveOptions);
        Assert.Equal(engine.PreviewSize, egbiOverlay.Size);

        string tempDir = Path.Combine(Path.GetTempPath(), $"leiter-real-integration-{Guid.NewGuid():N}");
        try
        {
            await ExportService.ExportAllStagesAsync(
                engine.PreviewSource!,
                result,
                engine.LastRenderedEffectiveOptions!,
                tempDir,
                customVisualizer: null,
                includePresetSidecar: true,
                calibrationReferenceDimension: nativeSource.Size.LinearDimension,
                paletteMode: PaletteMode.Auto);

            Assert.Equal(24, ExportService.StageManifest.Count);
            foreach (StageExportEntry entry in ExportService.StageManifest)
            {
                string filePath = Path.Combine(tempDir, entry.FileName);
                Assert.True(File.Exists(filePath), $"Missing exported stage image: {entry.FileName}");
                SequentialMatrix<Rgb8> loaded = ImageExtensions.LoadRgb8(filePath);
                Assert.Equal(engine.PreviewSize, loaded.Size);
            }

            string presetPath = Path.Combine(tempDir, "preset.json");
            Assert.True(File.Exists(presetPath));
            PresetDocument sidecar = await PresetSerializer.LoadFromFileAsync(presetPath);
            Assert.Equal(nativeSource.Size.LinearDimension, sidecar.CalibrationReferenceDimension);
            Assert.Equal(engine.LastRenderedEffectiveOptions, sidecar.Options);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static string FindExampleInputPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "extern", "Leiter", "examples", "input.png");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate extern/Leiter/examples/input.png from test base directory.");
    }
}
