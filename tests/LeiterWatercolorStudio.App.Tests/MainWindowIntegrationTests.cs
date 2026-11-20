using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.App.Services;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.App.Views;
using LeiterWatercolorStudio.App.Views.Inspectors;
using LeiterWatercolorStudio.Core.Export;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Presets;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class MainWindowIntegrationTests
{
    [AvaloniaFact]
    public async Task MainWindow_FullStudioWorkflow_ThemesStagesPresetsExportsAndFocusAwareShortcuts()
    {
        string inputPath = TestAppHelpers.FindExampleInputPath();
        string tempDir = Path.Combine(Path.GetTempPath(), $"leiter-app-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        string savedPresetPath = Path.Combine(tempDir, "saved_preset.json");
        string scaledPresetPath = Path.Combine(tempDir, "scaled_preset.json");
        string exportedFinalPath = Path.Combine(tempDir, "exported_final.png");
        string exportAllDir = Path.Combine(tempDir, "all_stages");

        var dialogs = new MockFileDialogService
        {
            SavePresetPath = savedPresetPath,
            OpenPresetPath = scaledPresetPath,
            SaveFinalImagePath = exportedFinalPath,
            ExportFolderPath = exportAllDir,
            ConfirmResult = true,
        };

        var vm = new MainWindowViewModel(
            dialogs,
            uiDispatcher: action => Dispatcher.UIThread.Post(action),
            debounceInterval: TimeSpan.FromMilliseconds(10));

        var window = new MainWindow
        {
            DataContext = vm,
        };

        try
        {
            // 1. Instantiate MainWindow + VM and test Dark <-> Light themes
            window.Show();
            TestAppHelpers.PumpUi();

            Assert.True(vm.IsDarkTheme);
            Assert.Equal(ThemeVariant.Dark, Application.Current?.RequestedThemeVariant);

            vm.ToggleThemeCommand.Execute(null);
            TestAppHelpers.PumpUi();
            Assert.False(vm.IsDarkTheme);
            Assert.Equal(ThemeVariant.Light, Application.Current?.RequestedThemeVariant);

            vm.ToggleThemeCommand.Execute(null);
            TestAppHelpers.PumpUi();
            Assert.True(vm.IsDarkTheme);
            Assert.Equal(ThemeVariant.Dark, Application.Current?.RequestedThemeVariant);

            // 2. Load real photo and await pipeline completion
            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.LoadImageFromPathAsync(inputPath));

            Assert.True(vm.HasImage);
            Assert.Equal(8, vm.Stages.Count);

            foreach (StageCardViewModel stage in vm.Stages)
            {
                Assert.NotNull(stage.Thumbnail);
                Assert.NotNull(stage.GridBitmap);
                Assert.False(string.IsNullOrWhiteSpace(stage.TimingText));
                Assert.NotEqual("—", stage.TimingText);
            }

            // 3. Check SubStages drawer population for Saliency, Segmentation, WetInWet, Finishing
            AssertSubStageDrawer(vm, PrimaryStageId.Input, expectedVisible: false, expectedCount: 0);
            AssertSubStageDrawer(vm, PrimaryStageId.ColorTransfer, expectedVisible: false, expectedCount: 0);
            AssertSubStageDrawer(vm, PrimaryStageId.Saliency, expectedVisible: true, expectedCount: 5);
            AssertSubStageDrawer(vm, PrimaryStageId.Segmentation, expectedVisible: true, expectedCount: 2);
            AssertSubStageDrawer(vm, PrimaryStageId.LodAbstraction, expectedVisible: false, expectedCount: 0);
            AssertSubStageDrawer(vm, PrimaryStageId.WetInWet, expectedVisible: true, expectedCount: 3);
            AssertSubStageDrawer(vm, PrimaryStageId.HandTremor, expectedVisible: false, expectedCount: 0);
            AssertSubStageDrawer(vm, PrimaryStageId.Finishing, expectedVisible: true, expectedCount: 6);

            // 4. Switch through all 8 primary stages and multiple VisualizationModes
            foreach (StageCardViewModel stage in vm.Stages)
            {
                vm.SelectPrimaryStageCommand.Execute(stage.StageId);
                TestAppHelpers.PumpUi();
                Assert.Equal(stage.StageId, vm.ActiveStageId);
                Assert.NotNull(vm.PrimaryBitmap);
                Assert.NotEmpty(vm.AvailableVisualizationModes);
            }

            vm.SelectSubStageCommand.Execute(StageViewId.SaliencyRcMap);
            TestAppHelpers.PumpUi();
            foreach (VisualizationMode mode in new[]
                     {
                         VisualizationMode.HeatmapTurbo,
                         VisualizationMode.HeatmapViridis,
                         VisualizationMode.Grayscale,
                         VisualizationMode.MaskedColorOverlay,
                     })
            {
                vm.SelectedVisualizationMode = mode;
                TestAppHelpers.PumpUi();
                Assert.Equal(mode, vm.SelectedVisualizationMode);
                Assert.NotNull(vm.PrimaryBitmap);
            }

            vm.SelectSubStageCommand.Execute(StageViewId.SegmentationMeanShiftRegions);
            TestAppHelpers.PumpUi();
            vm.SelectedVisualizationMode = VisualizationMode.RegionRandomColor;
            TestAppHelpers.PumpUi();
            Assert.True(vm.ShowReseedButton);
            vm.ReseedRandomColorsCommand.Execute(null);
            TestAppHelpers.PumpUi();

            vm.SelectedVisualizationMode = VisualizationMode.RegionBoundariesOverlay;
            TestAppHelpers.PumpUi();
            Assert.False(vm.ShowReseedButton);

            vm.SelectSubStageCommand.Execute(StageViewId.FinishingFinalFxaa);
            TestAppHelpers.PumpUi();
            vm.SelectedVisualizationMode = VisualizationMode.FxaaDiffMagnifier;
            TestAppHelpers.PumpUi();
            vm.SelectedVisualizationMode = VisualizationMode.Rgb;
            TestAppHelpers.PumpUi();

            // 5. Toggle Enable* flags across stages and test UndoCommand / RedoCommand
            Assert.True(vm.Parameters.ColorTransfer.EnableColorTransfer);
            Assert.True(vm.Parameters.LodAbstraction.EnableLodAbstraction);
            Assert.True(vm.Parameters.WetInWet.EnableWetInWet);
            Assert.True(vm.Parameters.HandTremor.EnableHandTremor);

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.Parameters.ColorTransfer.EnableColorTransfer = false);
            Assert.True(vm.Stages[(int)PrimaryStageId.ColorTransfer].IsBypassed);

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.Parameters.LodAbstraction.EnableLodAbstraction = false);
            Assert.True(vm.Stages[(int)PrimaryStageId.LodAbstraction].IsBypassed);

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.Parameters.WetInWet.EnableWetInWet = false);
            Assert.True(vm.Stages[(int)PrimaryStageId.WetInWet].IsBypassed);

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.Parameters.HandTremor.EnableHandTremor = false);
            Assert.True(vm.Stages[(int)PrimaryStageId.HandTremor].IsBypassed);
            Assert.Equal("Bypassed", vm.Stages[(int)PrimaryStageId.HandTremor].TimingText);
            Assert.True(vm.UndoCommand.CanExecute(null));

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.UndoCommand.Execute(null));
            Assert.True(vm.Parameters.HandTremor.EnableHandTremor);
            Assert.False(vm.Stages[(int)PrimaryStageId.HandTremor].IsBypassed);
            Assert.True(vm.RedoCommand.CanExecute(null));

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.RedoCommand.Execute(null));
            Assert.False(vm.Parameters.HandTremor.EnableHandTremor);

            // Undo all 4 toggles back so full pipeline is active before preset/export verification
            for (int i = 0; i < 4; i++)
            {
                await TestAppHelpers.WaitForRenderAsync(vm, () => vm.UndoCommand.Execute(null));
            }

            Assert.True(vm.Parameters.ColorTransfer.EnableColorTransfer);
            Assert.True(vm.Parameters.LodAbstraction.EnableLodAbstraction);
            Assert.True(vm.Parameters.WetInWet.EnableWetInWet);
            Assert.True(vm.Parameters.HandTremor.EnableHandTremor);

            // 6. SavePresetCommand & LoadPresetCommand (triggering ConfirmAsync scaling prompt)
            await vm.SavePresetCommand.ExecuteAsync(null);
            TestAppHelpers.PumpUi();
            Assert.True(File.Exists(savedPresetPath));
            Assert.True(new FileInfo(savedPresetPath).Length > 0);

            PresetDocument savedDoc = await PresetSerializer.LoadFromFileAsync(savedPresetPath);
            var scaledDoc = new PresetDocument
            {
                SchemaVersion = savedDoc.SchemaVersion,
                Name = "ScaledTestPreset",
                CalibrationReferenceDimension = 400.0, // >5% diff from 1408x768 linear dimension (~1039.8)
                PaletteMode = PaletteMode.Auto,
                Options = savedDoc.Options with { ColorTransferStrength = 0.72 },
                StepTimingsMs = savedDoc.StepTimingsMs,
            };
            await PresetSerializer.SaveToFileAsync(scaledDoc, scaledPresetPath);

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.LoadPresetCommand.ExecuteAsync(null));
            Assert.Single(dialogs.ConfirmCalls);
            Assert.Equal(0.72, vm.Parameters.ColorTransfer.ColorTransferStrength, 4);

            // 7. ExportFinalFullResCommand & ExportAllStagesCommand
            await vm.ExportFinalFullResCommand.ExecuteAsync(null);
            TestAppHelpers.PumpUi();
            Assert.True(File.Exists(exportedFinalPath));
            Assert.True(new FileInfo(exportedFinalPath).Length > 0);
            SequentialMatrix<Rgb8> exportedFinal = ImageExtensions.LoadRgb8(exportedFinalPath);
            Assert.Equal(vm.Engine.NativeSize, exportedFinal.Size);

            await vm.ExportAllStagesCommand.ExecuteAsync(null);
            TestAppHelpers.PumpUi();
            Assert.Equal(24, ExportService.StageManifest.Count);
            foreach (ExportService.StageExportEntry entry in ExportService.StageManifest)
            {
                string stageFile = Path.Combine(exportAllDir, entry.FileName);
                Assert.True(File.Exists(stageFile), $"Missing exported stage PNG: {entry.FileName}");
                Assert.True(new FileInfo(stageFile).Length > 0, $"Empty exported stage PNG: {entry.FileName}");
            }

            string exportedPresetSidecar = Path.Combine(exportAllDir, "preset.json");
            Assert.True(File.Exists(exportedPresetSidecar));
            Assert.True(new FileInfo(exportedPresetSidecar).Length > 0);

            // 8. Focus-aware keyboard shortcuts (1-8, G, Ctrl+Z, Ctrl+Y, Ctrl+Enter)
            ZoomPanViewport viewport = window.FindControl<ZoomPanViewport>("Viewport")!;
            viewport.Focus();
            TestAppHelpers.PumpUi();

            (PhysicalKey Key, PrimaryStageId ExpectedStage)[] stageKeys =
            [
                (PhysicalKey.Digit1, PrimaryStageId.Input),
                (PhysicalKey.Digit2, PrimaryStageId.ColorTransfer),
                (PhysicalKey.Digit3, PrimaryStageId.Saliency),
                (PhysicalKey.Digit4, PrimaryStageId.Segmentation),
                (PhysicalKey.Digit5, PrimaryStageId.LodAbstraction),
                (PhysicalKey.Digit6, PrimaryStageId.WetInWet),
                (PhysicalKey.Digit7, PrimaryStageId.HandTremor),
                (PhysicalKey.Digit8, PrimaryStageId.Finishing),
            ];

            foreach ((PhysicalKey key, PrimaryStageId expectedStage) in stageKeys)
            {
                window.KeyPressQwerty(key, RawInputModifiers.None);
                TestAppHelpers.PumpUi();
                Assert.Equal(expectedStage, vm.ActiveStageId);
            }

            // G toggles Grid view
            Assert.False(vm.IsGridView);
            window.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.True(vm.IsGridView);

            window.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.False(vm.IsGridView);

            // Ctrl+Z and Ctrl+Y undo/redo
            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.Parameters.Finishing.EnableFxaa = false);
            viewport.Focus();
            TestAppHelpers.PumpUi();

            await TestAppHelpers.WaitForRenderAsync(
                vm,
                () => window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control));
            Assert.True(vm.Parameters.Finishing.EnableFxaa);

            await TestAppHelpers.WaitForRenderAsync(
                vm,
                () => window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control));
            Assert.False(vm.Parameters.Finishing.EnableFxaa);

            // Ctrl+Enter executes RunNow when Auto-Run is paused
            vm.IsAutoRunEnabled = false;
            vm.Parameters.Finishing.EnableFxaa = true;
            Assert.NotEmpty(vm.Engine.StaleStages);

            viewport.Focus();
            TestAppHelpers.PumpUi();

            await TestAppHelpers.WaitForRenderAsync(
                vm,
                () => window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control));
            Assert.Empty(vm.Engine.StaleStages);
            vm.IsAutoRunEnabled = true;

            // 9. Focus a TextBox and verify pressing '1' or 'G' does NOT change stage or toggle Grid view
            viewport.Focus();
            window.KeyPressQwerty(PhysicalKey.Digit7, RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.Equal(PrimaryStageId.HandTremor, vm.ActiveStageId);
            Assert.False(vm.IsGridView);
            Assert.Empty(dialogs.ErrorCalls);
            vm.IsInspectorOpen = true;
            TestAppHelpers.PumpUi();

            HandTremorInspectorView handTremorInspector = window.FindControl<HandTremorInspectorView>("HandTremorInspector")!;
            TextBox focusedTextBox = handTremorInspector
                .GetVisualDescendants()
                .OfType<TextBox>()
                .First(tb => tb.IsVisible && tb.IsEffectivelyVisible && tb.Focusable);

            focusedTextBox.Focus();
            TestAppHelpers.PumpUi();
            Assert.Same(focusedTextBox, window.FocusManager?.GetFocusedElement());

            window.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.G, RawInputModifiers.None);
            TestAppHelpers.PumpUi();

            Assert.Equal(PrimaryStageId.HandTremor, vm.ActiveStageId);
            Assert.False(vm.IsGridView);
            Assert.Empty(dialogs.ErrorCalls);
        }
        finally
        {
            if (Application.Current is Application app)
            {
                app.RequestedThemeVariant = ThemeVariant.Dark;
            }

            window.Close();
            vm.Dispose();

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static void AssertSubStageDrawer(
        MainWindowViewModel vm,
        PrimaryStageId stageId,
        bool expectedVisible,
        int expectedCount)
    {
        vm.SelectPrimaryStageCommand.Execute(stageId);
        TestAppHelpers.PumpUi();
        Assert.Equal(expectedVisible, vm.ShowSubStageDrawer);
        Assert.Equal(expectedCount, vm.SubStages.Count);
    }

    private sealed class MockFileDialogService : IFileDialogService
    {
        public string? OpenImagePath { get; set; }
        public string? SaveFinalImagePath { get; set; }
        public string? ExportFolderPath { get; set; }
        public string? SavePresetPath { get; set; }
        public string? OpenPresetPath { get; set; }
        public bool ConfirmResult { get; set; }
        public List<(string Title, string Message)> ConfirmCalls { get; } = [];
        public List<(string Title, string Message)> ErrorCalls { get; } = [];

        public Task<string?> PickOpenImageAsync() => Task.FromResult(OpenImagePath);
        public Task<string?> PickSaveFinalImageAsync(string suggestedFileName) => Task.FromResult(SaveFinalImagePath);
        public Task<string?> PickExportFolderAsync() => Task.FromResult(ExportFolderPath);
        public Task<string?> PickSavePresetAsync(string suggestedFileName) => Task.FromResult(SavePresetPath);
        public Task<string?> PickOpenPresetAsync() => Task.FromResult(OpenPresetPath);

        public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
        {
            ConfirmCalls.Add((title, message));
            return Task.FromResult(ConfirmResult);
        }

        public Task ShowErrorAsync(string title, string message)
        {
            ErrorCalls.Add((title, message));
            return Task.CompletedTask;
        }
    }
}
