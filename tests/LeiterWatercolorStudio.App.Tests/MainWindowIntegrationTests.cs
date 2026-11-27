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
using LeiterWatercolorStudio.App.Tests.TestDoubles;
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
        vm.PreviewCap = PreviewResolutionCap.Px512;

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
                CalibrationReferenceDimension = 1200.0, // >5% diff (~13.3%) from 1408x768 linear dimension (~1039.8)
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
            foreach (StageExportEntry entry in ExportService.StageManifest)
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

    [AvaloniaFact]
    public async Task MainWindow_SamplePhoto_IndependentGridZoom_HoldCompare_AndFullResPreviewToggle()
    {
        var vm = new MainWindowViewModel(
            fileDialogs: null,
            uiDispatcher: action => Dispatcher.UIThread.Post(action),
            debounceInterval: TimeSpan.FromMilliseconds(10));
        vm.PreviewCap = PreviewResolutionCap.Px512;

        var window = new MainWindow
        {
            Width = 1440,
            Height = 900,
            DataContext = vm,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            // Empty launch defaults to 1. Input, and toolbar fits on 1 single row at 1440x900
            Assert.False(vm.HasImage);
            Assert.Equal(PrimaryStageId.Input, vm.ActiveStageId);
            Assert.Equal("1. Input", vm.ActiveStageHeaderTitle);

            Border toolbar = window
                .GetVisualDescendants()
                .OfType<Border>()
                .First(b => b.Classes.Contains("studio-toolbar"));
            Assert.True(toolbar.Bounds.Height <= 46.0, $"Expected single-row toolbar <= 46px at 1440px wide, got {toolbar.Bounds.Height:F1}px.");

            // Try Sample Photo loads procedural 768x512 landscape and renders pipeline
            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.LoadSampleImageCommand.Execute(null));
            Assert.True(vm.HasImage);
            Assert.Equal(PrimaryStageId.Finishing, vm.ActiveStageId);
            Assert.Equal("8. Finishing", vm.ActiveStageHeaderTitle);
            Assert.NotNull(vm.ToneCurveHistogram);
            Assert.NotEmpty(vm.ToneCurveHistogram!);

            // Independent GridZoom vs Single Zoom
            vm.Zoom = 2.4;
            vm.PanX = 45.0;
            Assert.Equal(1.0, vm.GridZoom, 4);
            Assert.Equal(0.0, vm.GridPanX, 4);

            vm.IsGridView = true;
            TestAppHelpers.PumpUi();
            StageGridView stageGrid = window.FindControl<StageGridView>("StageGrid")!;
            Assert.Equal(1.0, stageGrid.Zoom, 4);

            stageGrid.ZoomAroundCenter(1.5);
            TestAppHelpers.PumpUi();
            Assert.Equal(1.5, vm.GridZoom, 4);
            Assert.Equal(2.4, vm.Zoom, 4);

            vm.IsGridView = false;
            TestAppHelpers.PumpUi();

            // Toggle Compare via C key, verify toolbar stays single-row in SideBySide + PinnedStage, and test Hold-to-Compare
            ZoomPanViewport viewport = window.FindControl<ZoomPanViewport>("Viewport")!;
            viewport.Focus();
            TestAppHelpers.PumpUi();

            Assert.Equal(CompareMode.Off, vm.CompareMode);
            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.Equal(CompareMode.SplitWipe, vm.CompareMode);
            Assert.True(toolbar.Bounds.Height <= 46.0, $"Expected single-row toolbar <= 46px in SplitWipe, got {toolbar.Bounds.Height:F1}px.");

            vm.CompareMode = CompareMode.SideBySide;
            vm.CompareReferenceKind = CompareReferenceKind.PinnedStage;
            TestAppHelpers.PumpUi();
            Assert.True(toolbar.Bounds.Height <= 46.0, $"Expected single-row toolbar <= 46px in SideBySide+PinnedStage, got {toolbar.Bounds.Height:F1}px.");

            vm.CompareMode = CompareMode.SplitWipe;
            vm.CompareReferenceKind = CompareReferenceKind.Input;
            TestAppHelpers.PumpUi();

            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.None);
            TestAppHelpers.PumpUi();
            Assert.Equal(CompareMode.Off, vm.CompareMode);

            vm.IsHoldComparing = true;
            TestAppHelpers.PumpUi();
            Assert.NotNull(vm.CompareBitmap);
            vm.IsHoldComparing = false;
            TestAppHelpers.PumpUi();

            // Artist Look Shortcut jumps to requested stage/section
            vm.ApplyLookShortcutCommand.Execute("paper-tooth");
            TestAppHelpers.PumpUi();
            Assert.Equal(PrimaryStageId.Finishing, vm.ActiveStageId);
            Assert.Equal(StageViewId.FinishingPaperGranulation, vm.SelectedSubStage?.ViewId);

            // Full-Resolution preview toggle & cache invalidation on edit
            Assert.False(vm.HasCachedFullRes);
            await vm.RenderFullResolutionCommand.ExecuteAsync(null);
            TestAppHelpers.PumpUi();

            Assert.True(vm.HasCachedFullRes);
            Assert.False(vm.IsViewingFullRes);
            Assert.Equal("Preview Full-Res", vm.FullResToggleLabel);

            vm.ToggleFullResPreviewCommand.Execute(null);
            TestAppHelpers.PumpUi();
            Assert.True(vm.IsViewingFullRes);
            Assert.Equal("Full Res (cached)", vm.FullResToggleLabel);

            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.Parameters.HandTremor.EnableHandTremor = false);
            Assert.False(vm.HasCachedFullRes);
            Assert.False(vm.IsViewingFullRes);
        }
        finally
        {
            window.Close();
            vm.Dispose();
        }
    }
}
