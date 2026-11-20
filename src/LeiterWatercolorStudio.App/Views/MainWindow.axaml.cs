using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.App.Views;

/// <summary>
/// Primary desktop studio shell window coordinating the toolbar, viewport/grid workspace,
/// collapsible inspector sidebar, pipeline filmstrip, drag-and-drop, and keyboard shortcuts.
/// </summary>
public partial class MainWindow : Window
{
    private const double ShortcutZoomStep = 1.25;
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor DefaultCursor = new(StandardCursorType.Arrow);

    private MainWindowViewModel? _attachedViewModel;
    private bool _isSpacePanMode;

    /// <summary>
    /// Initializes a new instance of <see cref="MainWindow"/>.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        // Subscribe ONLY to NumericSlider and ToneCurveEditor drag events (PerlinNoiseEditor
        // wraps NumericSlider and re-emits its own drag events, so subscribing to both would double-trigger).
        InspectorHost.AddHandler(NumericSlider.DragStartedEvent, OnInspectorDragStarted);
        InspectorHost.AddHandler(NumericSlider.DragCompletedEvent, OnInspectorDragCompleted);
        InspectorHost.AddHandler(ToneCurveEditor.DragStartedEvent, OnInspectorDragStarted);
        InspectorHost.AddHandler(ToneCurveEditor.DragCompletedEvent, OnInspectorDragCompleted);

        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);
        AddHandler(DragDrop.DropEvent, OnWindowDrop);
    }

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        DetachViewModel();
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachViewModel();

        if (DataContext is MainWindowViewModel vm)
        {
            _attachedViewModel = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.ViewportRefreshRequested += OnViewportRefreshRequested;
            vm.ImageLoaded += OnViewModelImageLoaded;
            ApplyThemeVariant(vm.IsDarkTheme);
        }
    }

    private void DetachViewModel()
    {
        if (_attachedViewModel is null)
        {
            return;
        }

        _attachedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _attachedViewModel.ViewportRefreshRequested -= OnViewportRefreshRequested;
        _attachedViewModel.ImageLoaded -= OnViewModelImageLoaded;
        _attachedViewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MainWindowViewModel vm && e.PropertyName == nameof(MainWindowViewModel.IsDarkTheme))
        {
            ApplyThemeVariant(vm.IsDarkTheme);
        }
    }

    private void OnViewportRefreshRequested(object? sender, EventArgs e)
    {
        Viewport.RefreshImage();
    }

    private void OnViewModelImageLoaded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => Viewport.FitToView(), DispatcherPriority.Loaded);
    }

    private static void ApplyThemeVariant(bool isDarkTheme)
    {
        if (Application.Current is Application app)
        {
            app.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    private void OnSingleViewClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsGridView = false;
        }
    }

    private void OnGridViewClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsGridView = true;
        }
    }

    private void OnViewportHoverPixelChanged(object? sender, PixelHoverEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.UpdateHoverPixel(e.X, e.Y);
        }
    }

    private void OnViewportHoverExited(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ClearHoverPixel();
        }
    }

    private async void OnViewportFileDropped(object? sender, FileDroppedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && !string.IsNullOrWhiteSpace(e.Path))
        {
            await vm.LoadImageFromPathAsync(e.Path);
        }
    }

    private void OnStageGridDoubleClicked(object? sender, PrimaryStageId stageId)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.IsGridView = false;
            if (vm.SelectPrimaryStageCommand.CanExecute(stageId))
            {
                vm.SelectPrimaryStageCommand.Execute(stageId);
            }
        }
    }

    private void OnInspectorDragStarted(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.BeginParameterDrag();
        }
    }

    private void OnInspectorDragCompleted(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.EndParameterDrag();
        }
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (e.Handled || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        IStorageItem[]? files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length == 0)
        {
            return;
        }

        foreach (IStorageItem item in files)
        {
            string? localPath = item.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                e.Handled = true;
                await vm.LoadImageFromPathAsync(localPath);
                break;
            }
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        KeyModifiers modifiers = e.KeyModifiers;
        bool hasCtrl = modifiers.HasFlag(KeyModifiers.Control);
        bool hasShift = modifiers.HasFlag(KeyModifiers.Shift);
        bool hasAlt = modifiers.HasFlag(KeyModifiers.Alt);

        if (hasCtrl && !hasAlt)
        {
            if (TryHandleControlShortcut(vm, e.Key, hasShift))
            {
                e.Handled = true;
                return;
            }
        }

        if (modifiers == KeyModifiers.None && e.Key == Key.Escape)
        {
            if (vm.IsBusy && vm.CancelRenderCommand.CanExecute(null))
            {
                vm.CancelRenderCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (vm.IsGridView)
            {
                vm.IsGridView = false;
                e.Handled = true;
                return;
            }
        }

        if (IsTextInputFocused(e))
        {
            return;
        }

        if (modifiers == KeyModifiers.None)
        {
            if (TryMapStageShortcut(e.Key, out PrimaryStageId stageId))
            {
                if (vm.SelectPrimaryStageCommand.CanExecute(stageId))
                {
                    vm.SelectPrimaryStageCommand.Execute(stageId);
                    e.Handled = true;
                }

                return;
            }

            if (e.Key == Key.G)
            {
                if (vm.ToggleGridViewCommand.CanExecute(null))
                {
                    vm.ToggleGridViewCommand.Execute(null);
                    e.Handled = true;
                }

                return;
            }

            if (e.Key == Key.Space && !vm.IsGridView && vm.HasImage)
            {
                _isSpacePanMode = true;
                Viewport.Cursor = HandCursor;
                if (!Viewport.IsFocused)
                {
                    Viewport.Focus();
                    e.Handled = true;
                }
            }
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && _isSpacePanMode)
        {
            _isSpacePanMode = false;
            Viewport.Cursor = DefaultCursor;
        }
    }

    private bool TryHandleControlShortcut(MainWindowViewModel vm, Key key, bool hasShift)
    {
        switch (key)
        {
            case Key.O when !hasShift:
                return ExecuteIfReady(vm.OpenImageCommand);
            case Key.O when hasShift:
                return ExecuteIfReady(vm.LoadPresetCommand);
            case Key.S when !hasShift:
                return ExecuteIfReady(vm.SavePresetCommand);
            case Key.E when !hasShift:
                return ExecuteIfReady(vm.ExportFinalFullResCommand);
            case Key.E when hasShift:
                return ExecuteIfReady(vm.ExportAllStagesCommand);
            case Key.Z when !hasShift:
                return ExecuteIfReady(vm.UndoCommand);
            case Key.Z when hasShift:
            case Key.Y when !hasShift:
                return ExecuteIfReady(vm.RedoCommand);
            case Key.Enter when !hasShift:
                return ExecuteIfReady(vm.RunNowCommand);
            case Key.D0 or Key.NumPad0 when !hasShift:
                if (vm.IsGridView)
                {
                    StageGrid.ResetView();
                }
                else
                {
                    Viewport.FitToView();
                }

                return true;
            case Key.D1 or Key.NumPad1 when !hasShift:
                if (vm.IsGridView)
                {
                    vm.Zoom = 1.0;
                    vm.PanX = 0.0;
                    vm.PanY = 0.0;
                }
                else
                {
                    Viewport.ZoomOneToOne();
                }

                return true;
            case Key.OemPlus or Key.Add when !hasShift:
                vm.Zoom = Math.Clamp(vm.Zoom * ShortcutZoomStep, ZoomPanViewport.MinZoom, ZoomPanViewport.MaxZoom);
                return true;
            case Key.OemMinus or Key.Subtract when !hasShift:
                vm.Zoom = Math.Clamp(vm.Zoom / ShortcutZoomStep, ZoomPanViewport.MinZoom, ZoomPanViewport.MaxZoom);
                return true;
            default:
                return false;
        }
    }

    private bool IsTextInputFocused(KeyEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return true;
        }

        if (e.Source is Visual sourceVisual && sourceVisual.FindAncestorOfType<TextBox>(includeSelf: true) is not null)
        {
            return true;
        }

        return false;
    }

    private static bool ExecuteIfReady(System.Windows.Input.ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
            return true;
        }

        return false;
    }

    private static bool TryMapStageShortcut(Key key, out PrimaryStageId stageId)
    {
        switch (key)
        {
            case Key.D1 or Key.NumPad1:
                stageId = PrimaryStageId.Input;
                return true;
            case Key.D2 or Key.NumPad2:
                stageId = PrimaryStageId.ColorTransfer;
                return true;
            case Key.D3 or Key.NumPad3:
                stageId = PrimaryStageId.Saliency;
                return true;
            case Key.D4 or Key.NumPad4:
                stageId = PrimaryStageId.Segmentation;
                return true;
            case Key.D5 or Key.NumPad5:
                stageId = PrimaryStageId.LodAbstraction;
                return true;
            case Key.D6 or Key.NumPad6:
                stageId = PrimaryStageId.WetInWet;
                return true;
            case Key.D7 or Key.NumPad7:
                stageId = PrimaryStageId.HandTremor;
                return true;
            case Key.D8 or Key.NumPad8:
                stageId = PrimaryStageId.Finishing;
                return true;
            default:
                stageId = default;
                return false;
        }
    }
}
