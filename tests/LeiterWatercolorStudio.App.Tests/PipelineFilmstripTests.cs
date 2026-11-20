using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
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

public class PipelineFilmstripTests
{
    [AvaloniaFact]
    public async Task PipelineFilmstrip_InsideMainWindow_ClickStageCardAndSubStagePill_DispatchesTypedRelayCommands()
    {
        var vm = new MainWindowViewModel(
            fileDialogs: null,
            uiDispatcher: action => Dispatcher.UIThread.Post(action),
            debounceInterval: TimeSpan.FromMilliseconds(10));

        var window = new MainWindow
        {
            Width = 1440,
            Height = 860,
            DataContext = vm,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            var sampleMatrix = CreateSampleMatrix(48, 36);
            await TestAppHelpers.WaitForRenderAsync(vm, () => vm.LoadImageFromMatrix(sampleMatrix, "sample.png"));

            PipelineFilmstrip filmstrip = window.FindControl<PipelineFilmstrip>("Filmstrip")!;
            ItemsControl primaryItems = filmstrip.FindControl<ItemsControl>("PrimaryStagesItemsControl")!;

            // Click Stage 2 (Saliency) card body via real pointer input -> RelayCommand<PrimaryStageId>
            Border saliencyCard = primaryItems
                .GetVisualDescendants()
                .OfType<Border>()
                .First(b => b.Classes.Contains("stage-card")
                    && b.DataContext is StageCardViewModel card
                    && card.StageId == PrimaryStageId.Saliency);

            Point cardPoint = saliencyCard.TranslatePoint(
                new Point(saliencyCard.Bounds.Width * 0.5, saliencyCard.Bounds.Height * 0.65),
                window)!.Value;

            window.MouseDown(cardPoint, MouseButton.Left);
            window.MouseUp(cardPoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(PrimaryStageId.Saliency, vm.ActiveStageId);
            Assert.True(vm.ShowSubStageDrawer);

            // Click SaliencyCut sub-stage pill -> RelayCommand<StageViewId>
            ItemsControl subItems = filmstrip.FindControl<ItemsControl>("SubStagesItemsControl")!;
            Button cutMaskPill = subItems
                .GetVisualDescendants()
                .OfType<Button>()
                .First(btn => btn.Classes.Contains("substage-pill")
                    && btn.DataContext is SubStageItemViewModel sub
                    && sub.ViewId == StageViewId.SaliencyCutMask);

            Point pillPoint = cutMaskPill.TranslatePoint(
                new Point(cutMaskPill.Bounds.Width * 0.5, cutMaskPill.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(pillPoint, MouseButton.Left);
            window.MouseUp(pillPoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.NotNull(vm.SelectedSubStage);
            Assert.Equal(StageViewId.SaliencyCutMask, vm.SelectedSubStage!.ViewId);
            Assert.Equal(StageViewId.SaliencyCutMask, vm.SelectedStage!.ActiveViewId);

            // Click Stage 1 (ColorTransfer) card body to confirm switching back works without exception
            Border colorCard = primaryItems
                .GetVisualDescendants()
                .OfType<Border>()
                .First(b => b.Classes.Contains("stage-card")
                    && b.DataContext is StageCardViewModel card
                    && card.StageId == PrimaryStageId.ColorTransfer);

            Point colorPoint = colorCard.TranslatePoint(
                new Point(colorCard.Bounds.Width * 0.5, colorCard.Bounds.Height * 0.65),
                window)!.Value;

            window.MouseDown(colorPoint, MouseButton.Left);
            window.MouseUp(colorPoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(PrimaryStageId.ColorTransfer, vm.ActiveStageId);
            Assert.False(vm.ShowSubStageDrawer);
        }
        finally
        {
            window.Close();
            TestAppHelpers.PumpUi();
        }
    }

    [AvaloniaFact]
    public void PipelineFilmstrip_ClickPinButton_SupportsBothStageCardViewModelAndPrimaryStageIdRelayCommands()
    {
        List<StageCardViewModel> stages = CreateStageCards();
        StageCardViewModel? pinnedByCard = null;
        PrimaryStageId? pinnedById = null;

        var filmstrip = new PipelineFilmstrip
        {
            Width = 1400,
            Height = 200,
            Stages = stages,
            SelectedStage = stages[0],
            TogglePinStageCommand = new RelayCommand<StageCardViewModel>(card => pinnedByCard = card),
        };

        var window = new Window
        {
            Width = 1400,
            Height = 200,
            Content = filmstrip,
        };

        try
        {
            window.Show();
            TestAppHelpers.PumpUi();

            ItemsControl primaryItems = filmstrip.FindControl<ItemsControl>("PrimaryStagesItemsControl")!;
            Button pinButton = primaryItems
                .GetVisualDescendants()
                .OfType<Button>()
                .First(b => b.Classes.Contains("pin-btn")
                    && b.DataContext is StageCardViewModel card
                    && card.StageId == PrimaryStageId.ColorTransfer);

            Point pinPoint = pinButton.TranslatePoint(
                new Point(pinButton.Bounds.Width * 0.5, pinButton.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(pinPoint, MouseButton.Left);
            window.MouseUp(pinPoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(stages[1], pinnedByCard);

            // Switch TogglePinStageCommand to RelayCommand<PrimaryStageId> and click again
            filmstrip.TogglePinStageCommand = new RelayCommand<PrimaryStageId>(id => pinnedById = id);
            window.MouseDown(pinPoint, MouseButton.Left);
            window.MouseUp(pinPoint, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(PrimaryStageId.ColorTransfer, pinnedById);
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
    public void StageGridView_DoubleClickTile_DispatchesBothPrimaryStageIdAndStageCardViewModelRelayCommands()
    {
        List<StageCardViewModel> stages = CreateStageCards();
        PrimaryStageId? activatedById = null;
        StageCardViewModel? activatedByCard = null;

        var gridView = new StageGridView
        {
            Stages = stages,
            SelectedStage = stages[0],
            ActivateStageCommand = new RelayCommand<PrimaryStageId>(id => activatedById = id),
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
            Border tile4 = itemsControl
                .GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Classes.Contains("grid-tile"))
                .ElementAt(4);

            Point tile4Center = tile4.TranslatePoint(
                new Point(tile4.Bounds.Width * 0.5, tile4.Bounds.Height * 0.5),
                window)!.Value;

            window.MouseDown(tile4Center, MouseButton.Left);
            window.MouseUp(tile4Center, MouseButton.Left);
            window.MouseDown(tile4Center, MouseButton.Left);
            window.MouseUp(tile4Center, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(stages[4].StageId, activatedById);

            gridView.ActivateStageCommand = new RelayCommand<StageCardViewModel>(card => activatedByCard = card);

            window.MouseDown(tile4Center, MouseButton.Left);
            window.MouseUp(tile4Center, MouseButton.Left);
            window.MouseDown(tile4Center, MouseButton.Left);
            window.MouseUp(tile4Center, MouseButton.Left);
            TestAppHelpers.PumpUi();

            Assert.Equal(stages[4], activatedByCard);
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

    private static List<StageCardViewModel> CreateStageCards()
    {
        var sampleMatrix = CreateSampleMatrix(24, 16);
        var stages = new List<StageCardViewModel>();
        foreach (PipelineStageDescriptor descriptor in PipelineStageRegistry.Stages)
        {
            var card = new StageCardViewModel(descriptor)
            {
                Thumbnail = MatrixBitmapConverter.BlitThumbnail(sampleMatrix),
                GridBitmap = MatrixBitmapConverter.Blit(sampleMatrix),
                TimingText = descriptor.StageId == PrimaryStageId.Input ? "Input" : "5 ms",
            };
            stages.Add(card);
        }

        return stages;
    }

    private static SequentialMatrix<Rgb8> CreateSampleMatrix(int width, int height)
    {
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                matrix[x, y] = new Rgb8(
                    (byte)((x * 255) / Math.Max(1, width - 1)),
                    (byte)((y * 255) / Math.Max(1, height - 1)),
                    140);
            }
        }

        return matrix;
    }
}
