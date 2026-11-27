using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Leiter.Core;
using Leiter.Pixels;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.App.Rendering;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.App.Views;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class ViewportAndGridTests
{
    [AvaloniaFact]
    public void ZoomPanViewport_MapsScreenToImagePixel_ClampsProperties_AndSwitchesCompareModes()
    {
        var primaryMatrix = new SequentialMatrix<Rgb8>(100, 80);
        var compareMatrix = new SequentialMatrix<Rgb8>(100, 80);
        WriteableBitmap primaryBitmap = MatrixBitmapConverter.Blit(primaryMatrix);
        WriteableBitmap compareBitmap = MatrixBitmapConverter.Blit(compareMatrix);

        var viewport = new ZoomPanViewport
        {
            Width = 400,
            Height = 300,
            PrimaryBitmap = primaryBitmap,
            CompareBitmap = compareBitmap,
            PrimaryLabel = "Final",
            CompareLabel = "Input",
            IsEmpty = false,
            CompareMode = CompareMode.Off,
        };

        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = viewport,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            // Center of 400x300 viewport -> center pixel (50, 40) of 100x80 image at Zoom=1, Pan=(0,0)
            Assert.Equal(new PixelPoint(50, 40), viewport.ScreenToImagePixel(new Point(200, 150)));
            Assert.Equal(new PixelPoint(0, 0), viewport.ScreenToImagePixel(new Point(150, 110)));
            Assert.Equal(new PixelPoint(99, 79), viewport.ScreenToImagePixel(new Point(249.5, 189.5)));
            Assert.Null(viewport.ScreenToImagePixel(new Point(149, 110)));
            Assert.Null(viewport.ScreenToImagePixel(new Point(250.5, 150)));

            // Apply Zoom and Pan offsets and check ScreenToImagePixel
            viewport.Zoom = 2.0;
            viewport.PanX = 20.0;
            viewport.PanY = -10.0;
            Assert.Equal(new PixelPoint(50, 40), viewport.ScreenToImagePixel(new Point(220, 140)));
            Assert.Equal(new PixelPoint(51, 41), viewport.ScreenToImagePixel(new Point(222, 142)));

            // Verify Zoom / PanX / PanY / WipePosition clamping
            viewport.Zoom = 0.01;
            Assert.Equal(ZoomPanViewport.MinZoom, viewport.Zoom);

            viewport.Zoom = 999.0;
            Assert.Equal(ZoomPanViewport.MaxZoom, viewport.Zoom);

            viewport.Zoom = double.NaN;
            Assert.Equal(1.0, viewport.Zoom);

            viewport.PanX = double.PositiveInfinity;
            viewport.PanY = double.NaN;
            Assert.Equal(0.0, viewport.PanX);
            Assert.Equal(0.0, viewport.PanY);

            viewport.WipePosition = -0.5;
            Assert.Equal(0.0, viewport.WipePosition);

            viewport.WipePosition = 2.2;
            Assert.Equal(1.0, viewport.WipePosition);

            viewport.WipePosition = double.NaN;
            Assert.Equal(0.5, viewport.WipePosition);

            // Empty viewport returns null for ScreenToImagePixel
            viewport.IsEmpty = true;
            Assert.Null(viewport.ScreenToImagePixel(new Point(200, 150)));
            viewport.IsEmpty = false;

            // Pointer pan & wheel zoom updates
            viewport.ZoomOneToOne();
            Point dragStart = new(200, 150);
            Point dragEnd = new(235, 125);
            window.MouseDown(dragStart, MouseButton.Left);
            window.MouseMove(dragEnd);
            window.MouseUp(dragEnd, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(35.0, viewport.PanX, 2);
            Assert.Equal(-25.0, viewport.PanY, 2);

            double zoomBeforeWheel = viewport.Zoom;
            window.MouseWheel(new Point(200, 150), new Vector(0, 2));
            TestAppHelpers.PumpUi();
            Assert.True(viewport.Zoom > zoomBeforeWheel);

            // CompareMode switching: Off -> SplitWipe -> SideBySide
            Canvas wipeOverlay = viewport.FindControl<Canvas>("PART_WipeOverlay")!;
            Grid wipeLabels = viewport.FindControl<Grid>("PART_WipeLabels")!;
            Grid sideBySideLabels = viewport.FindControl<Grid>("PART_SideBySideLabels")!;

            Assert.False(wipeOverlay.IsVisible);
            Assert.False(wipeLabels.IsVisible);
            Assert.False(sideBySideLabels.IsVisible);

            viewport.CompareMode = CompareMode.SplitWipe;
            viewport.WipePosition = 0.5;
            TestAppHelpers.PumpUi();

            Assert.True(wipeOverlay.IsVisible);
            Assert.True(wipeLabels.IsVisible);
            Assert.False(sideBySideLabels.IsVisible);

            // Dragging the split-wipe handle updates WipePosition in [0, 1] without changing Pan
            double panXBeforeWipe = viewport.PanX;
            double panYBeforeWipe = viewport.PanY;
            window.MouseDown(new Point(200, 150), MouseButton.Left);
            window.MouseMove(new Point(300, 150));
            window.MouseUp(new Point(300, 150), MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.InRange(viewport.WipePosition, 0.70, 0.80);
            Assert.Equal(panXBeforeWipe, viewport.PanX, 2);
            Assert.Equal(panYBeforeWipe, viewport.PanY, 2);

            viewport.CompareMode = CompareMode.SideBySide;
            viewport.ZoomOneToOne();
            TestAppHelpers.PumpUi();

            Assert.False(wipeOverlay.IsVisible);
            Assert.False(wipeLabels.IsVisible);
            Assert.True(sideBySideLabels.IsVisible);

            // Both panes map center to image center pixel; divider gap returns null
            Assert.Equal(new PixelPoint(50, 40), viewport.ScreenToImagePixel(new Point(99.5, 150)));
            Assert.Equal(new PixelPoint(50, 40), viewport.ScreenToImagePixel(new Point(300.5, 150)));
            Assert.Null(viewport.ScreenToImagePixel(new Point(200.0, 150)));

            // High zoom renders pixel grid surface cleanly
            viewport.Zoom = 8.0;
            TestAppHelpers.PumpUi();
            using WriteableBitmap? highZoomFrame = window.CaptureRenderedFrame();
            Assert.NotNull(highZoomFrame);
        }
        finally
        {
            window.Close();
            primaryBitmap.Dispose();
            compareBitmap.Dispose();
        }
    }

    [AvaloniaFact]
    public void StageGridView_RendersEightTiles_SharesZoomPan_AndActivatesOnDoubleClick()
    {
        var stages = new ObservableCollection<StageCardViewModel>();
        var sampleMatrix = new SequentialMatrix<Rgb8>(64, 48);

        foreach (PipelineStageDescriptor descriptor in PipelineStageRegistry.Stages)
        {
            var card = new StageCardViewModel(descriptor)
            {
                Thumbnail = MatrixBitmapConverter.BlitThumbnail(sampleMatrix),
                GridBitmap = MatrixBitmapConverter.Blit(sampleMatrix),
                TimingText = descriptor.StageId == PrimaryStageId.Input ? "Input" : "12 ms",
            };
            stages.Add(card);
        }

        PrimaryStageId? eventActivatedStage = null;
        StageCardViewModel? commandActivatedCard = null;

        var gridView = new StageGridView
        {
            Stages = stages,
            SelectedStage = stages[0],
            ActivateStageCommand = new RelayCommand<StageCardViewModel>(card => commandActivatedCard = card),
        };
        gridView.StageDoubleClicked += (_, stageId) => eventActivatedStage = stageId;

        var window = new Window
        {
            Width = 1000,
            Height = 640,
            Content = gridView,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            ItemsControl itemsControl = gridView.FindControl<ItemsControl>("GridItemsControl")!;
            List<Border> tiles = itemsControl
                .GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Classes.Contains("grid-tile"))
                .ToList();

            Assert.Equal(8, tiles.Count);

            // Mouse-wheel zoom over tile 2 updates shared Zoom on StageGridView
            Border targetTile = tiles[2];
            Point tileCenter = targetTile.TranslatePoint(
                new Point(targetTile.Bounds.Width * 0.5, targetTile.Bounds.Height * 0.5),
                window)!.Value;

            double initialZoom = gridView.Zoom;
            window.MouseWheel(tileCenter, new Vector(0, 2));
            TestAppHelpers.PumpUi();

            Assert.True(gridView.Zoom > initialZoom);
            Assert.NotEqual("100%", gridView.ZoomText);

            // Pointer drag on tile pans shared PanX/PanY and selects that tile
            double panXBefore = gridView.PanX;
            double panYBefore = gridView.PanY;

            window.MouseDown(tileCenter, MouseButton.Left);
            window.MouseMove(tileCenter + new Point(25, -18));
            window.MouseUp(tileCenter + new Point(25, -18), MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(stages[2], gridView.SelectedStage);
            Assert.Equal(panXBefore + 25.0, gridView.PanX, 2);
            Assert.Equal(panYBefore - 18.0, gridView.PanY, 2);

            // Verify every tile's image shares the synchronized RenderTransform Zoom/Pan values
            List<Image> tileImages = itemsControl
                .GetVisualDescendants()
                .OfType<Image>()
                .Where(img => img.Classes.Contains("tile-image"))
                .ToList();
            Assert.Equal(8, tileImages.Count);
            foreach (Image tileImage in tileImages)
            {
                var group = Assert.IsType<TransformGroup>(tileImage.RenderTransform);
                var scale = Assert.IsType<ScaleTransform>(group.Children[0]);
                var translate = Assert.IsType<TranslateTransform>(group.Children[1]);
                Assert.Equal(gridView.Zoom, scale.ScaleX, 4);
                Assert.Equal(gridView.Zoom, scale.ScaleY, 4);
                Assert.Equal(gridView.PanX, translate.X, 4);
                Assert.Equal(gridView.PanY, translate.Y, 4);
            }

            // Double-click tile 5 activates StageDoubleClicked and ActivateStageCommand
            Border tile5 = tiles[5];
            Point tile5Center = tile5.TranslatePoint(
                new Point(tile5.Bounds.Width * 0.5, tile5.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(tile5Center, MouseButton.Left);
            window.MouseUp(tile5Center, MouseButton.Left);
            window.MouseDown(tile5Center, MouseButton.Left);
            window.MouseUp(tile5Center, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(stages[5].StageId, eventActivatedStage);
            Assert.Equal(stages[5], commandActivatedCard);
            Assert.Equal(stages[5], gridView.SelectedStage);

            // ResetView restores defaults
            gridView.ResetView();
            Assert.Equal(1.0, gridView.Zoom);
            Assert.Equal(0.0, gridView.PanX);
            Assert.Equal(0.0, gridView.PanY);
        }
        finally
        {
            window.Close();
            foreach (StageCardViewModel card in stages)
            {
                card.Dispose();
            }
        }
    }

    [AvaloniaFact]
    public void ZoomPanViewport_HidesLoneRgbModeBar_ShowsMultipleModesOrReseed_AndSupportsHoldToCompare()
    {
        var primaryMatrix = new SequentialMatrix<Rgb8>(80, 60);
        var compareMatrix = new SequentialMatrix<Rgb8>(80, 60);
        WriteableBitmap primaryBitmap = MatrixBitmapConverter.Blit(primaryMatrix);
        WriteableBitmap compareBitmap = MatrixBitmapConverter.Blit(compareMatrix);

        var viewport = new ZoomPanViewport
        {
            Width = 500,
            Height = 320,
            PrimaryBitmap = primaryBitmap,
            CompareBitmap = compareBitmap,
            PrimaryLabel = "Final",
            CompareLabel = "Input",
            IsEmpty = false,
            AvailableVisualizationModes = [VisualizationMode.Rgb],
            SelectedVisualizationMode = VisualizationMode.Rgb,
        };

        bool? lastHoverCompare = null;
        viewport.HoverPixelChanged += (_, args) => lastHoverCompare = args.IsCompareSide;

        var window = new Window
        {
            Width = 500,
            Height = 320,
            Content = viewport,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Border modeBar = viewport.FindControl<Border>("PART_ModeBar")!;
            Border holdBadge = viewport.FindControl<Border>("PART_HoldCompareBadge")!;
            Canvas wipeOverlay = viewport.FindControl<Canvas>("PART_WipeOverlay")!;

            // Lone RGB chip with no Reseed button keeps PART_ModeBar hidden
            Assert.False(modeBar.IsVisible);

            // ShowReseedButton or >= 2 modes shows PART_ModeBar
            viewport.ShowReseedButton = true;
            TestAppHelpers.PumpUi();
            Assert.True(modeBar.IsVisible);

            viewport.ShowReseedButton = false;
            TestAppHelpers.PumpUi();
            Assert.False(modeBar.IsVisible);

            viewport.AvailableVisualizationModes = [VisualizationMode.Rgb, VisualizationMode.FxaaDiffMagnifier];
            TestAppHelpers.PumpUi();
            Assert.True(modeBar.IsVisible);

            // SideBySide anchors PART_ModeBar into the right (Primary) pane
            viewport.CompareMode = CompareMode.SideBySide;
            TestAppHelpers.PumpUi();
            Assert.True(modeBar.Margin.Left > 200.0);

            // Hold-to-Compare overrides SplitWipe/SideBySide visually without mutating CompareMode
            viewport.CompareMode = CompareMode.SplitWipe;
            viewport.IsHoldComparing = true;
            TestAppHelpers.PumpUi();
            Assert.True(holdBadge.IsVisible);
            Assert.False(wipeOverlay.IsVisible);
            Assert.Equal(CompareMode.SplitWipe, viewport.CompareMode);

            window.MouseMove(new Point(250, 160));
            TestAppHelpers.PumpUi();
            Assert.True(lastHoverCompare);

            viewport.IsHoldComparing = false;
            TestAppHelpers.PumpUi();
            Assert.False(holdBadge.IsVisible);
            Assert.True(wipeOverlay.IsVisible);
        }
        finally
        {
            window.Close();
            primaryBitmap.Dispose();
            compareBitmap.Dispose();
        }
    }

    [AvaloniaFact]
    public void StageGridView_ClickPinButton_DispatchesTogglePinStageCommand()
    {
        var stages = new ObservableCollection<StageCardViewModel>();
        var sampleMatrix = new SequentialMatrix<Rgb8>(32, 24);
        foreach (PipelineStageDescriptor descriptor in PipelineStageRegistry.Stages)
        {
            stages.Add(new StageCardViewModel(descriptor)
            {
                Thumbnail = MatrixBitmapConverter.BlitThumbnail(sampleMatrix),
                GridBitmap = MatrixBitmapConverter.Blit(sampleMatrix),
            });
        }

        StageCardViewModel? toggledCard = null;
        var gridView = new StageGridView
        {
            Stages = stages,
            SelectedStage = stages[0],
            TogglePinStageCommand = new RelayCommand<StageCardViewModel>(card => toggledCard = card),
        };

        var window = new Window
        {
            Width = 1000,
            Height = 640,
            Content = gridView,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            ItemsControl itemsControl = gridView.FindControl<ItemsControl>("GridItemsControl")!;
            Button pinButton = itemsControl
                .GetVisualDescendants()
                .OfType<Button>()
                .First(b => b.Classes.Contains("grid-pin-btn") &&
                            b.DataContext is StageCardViewModel card &&
                            card.StageId == PrimaryStageId.Saliency);

            Point pinPoint = pinButton.TranslatePoint(
                new Point(pinButton.Bounds.Width * 0.5, pinButton.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(pinPoint, MouseButton.Left);
            window.MouseUp(pinPoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(stages[(int)PrimaryStageId.Saliency], toggledCard);
        }
        finally
        {
            window.Close();
            foreach (StageCardViewModel card in stages)
            {
                card.Dispose();
            }
        }
    }

    [AvaloniaFact]
    public void ZoomPanViewport_HoverDiagnosticsToggle_ControlsDetailsVisibility()
    {
        var primaryMatrix = new SequentialMatrix<Rgb8>(64, 48);
        WriteableBitmap primaryBitmap = MatrixBitmapConverter.Blit(primaryMatrix);

        var viewport = new ZoomPanViewport
        {
            PrimaryBitmap = primaryBitmap,
            IsEmpty = false,
            HoverHudText = "(10, 12) RGB(180, 120, 90) Lab(55.0, 20.0, 25.0)",
            HoverHudDetailsText = "Region #42 Area:512px Salient:100%\nSaliency:0.850 Wet:No",
            HoverSwatchColor = Color.FromRgb(180, 120, 90),
        };

        var window = new Window
        {
            Width = 640,
            Height = 400,
            Content = viewport,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            Border hoverHud = viewport.FindControl<Border>("PART_HoverHud")!;
            TextBlock detailsText = viewport.FindControl<TextBlock>("PART_HoverHudDetailsText")!;
            ToggleButton diagnosticsToggle = viewport.FindControl<ToggleButton>("PART_DiagnosticsToggle")!;

            Assert.False(viewport.ShowHoverDiagnostics);
            Assert.False(diagnosticsToggle.IsChecked == true);
            Assert.True(hoverHud.IsVisible);
            Assert.False(detailsText.IsVisible);

            Point togglePoint = diagnosticsToggle.TranslatePoint(
                new Point(diagnosticsToggle.Bounds.Width * 0.5, diagnosticsToggle.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(togglePoint, MouseButton.Left);
            window.MouseUp(togglePoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.True(viewport.ShowHoverDiagnostics);
            Assert.True(diagnosticsToggle.IsChecked == true);
            Assert.True(detailsText.IsVisible);
            Assert.Contains("Region #42", detailsText.Text);

            window.MouseDown(togglePoint, MouseButton.Left);
            window.MouseUp(togglePoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.False(viewport.ShowHoverDiagnostics);
            Assert.False(detailsText.IsVisible);
        }
        finally
        {
            window.Close();
            primaryBitmap.Dispose();
        }
    }
}
