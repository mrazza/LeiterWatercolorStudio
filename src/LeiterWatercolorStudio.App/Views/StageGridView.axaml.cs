using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.App.Views;

/// <summary>
/// Synchronized 4×2 multi-up view displaying all 8 primary pipeline stage outputs with lockstep zoom and pan.
/// </summary>
public partial class StageGridView : UserControl
{
    /// <summary>
    /// Minimum allowed zoom factor.
    /// </summary>
    public const double MinZoom = 0.1;

    /// <summary>
    /// Maximum allowed zoom factor.
    /// </summary>
    public const double MaxZoom = 16.0;

    private const double ZoomStepBase = 1.15;
    private const double PixelInspectionZoomThreshold = 3.0;

    /// <summary>
    /// Identifies the <see cref="Stages"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<StageCardViewModel>?> StagesProperty =
        AvaloniaProperty.Register<StageGridView, IReadOnlyList<StageCardViewModel>?>(nameof(Stages));

    /// <summary>
    /// Identifies the <see cref="SelectedStage"/> styled property.
    /// </summary>
    public static readonly StyledProperty<StageCardViewModel?> SelectedStageProperty =
        AvaloniaProperty.Register<StageGridView, StageCardViewModel?>(
            nameof(SelectedStage),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="Zoom"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<StageGridView, double>(
            nameof(Zoom),
            defaultValue: 1.0,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceZoom);

    /// <summary>
    /// Identifies the <see cref="PanX"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> PanXProperty =
        AvaloniaProperty.Register<StageGridView, double>(
            nameof(PanX),
            defaultValue: 0.0,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceFiniteOffset);

    /// <summary>
    /// Identifies the <see cref="PanY"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> PanYProperty =
        AvaloniaProperty.Register<StageGridView, double>(
            nameof(PanY),
            defaultValue: 0.0,
            defaultBindingMode: BindingMode.TwoWay,
            coerce: CoerceFiniteOffset);

    /// <summary>
    /// Identifies the <see cref="ActivateStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ActivateStageCommandProperty =
        AvaloniaProperty.Register<StageGridView, ICommand?>(nameof(ActivateStageCommand));

    /// <summary>
    /// Identifies the <see cref="TogglePinStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> TogglePinStageCommandProperty =
        AvaloniaProperty.Register<StageGridView, ICommand?>(nameof(TogglePinStageCommand));

    /// <summary>
    /// Identifies the <see cref="ZoomText"/> direct property.
    /// </summary>
    public static readonly DirectProperty<StageGridView, string> ZoomTextProperty =
        AvaloniaProperty.RegisterDirect<StageGridView, string>(
            nameof(ZoomText),
            static o => o.ZoomText);

    /// <summary>
    /// Identifies the <see cref="ActiveInterpolationMode"/> direct property.
    /// </summary>
    public static readonly DirectProperty<StageGridView, BitmapInterpolationMode> ActiveInterpolationModeProperty =
        AvaloniaProperty.RegisterDirect<StageGridView, BitmapInterpolationMode>(
            nameof(ActiveInterpolationMode),
            static o => o.ActiveInterpolationMode);

    private readonly List<StageCardViewModel> _subscribedCards = [];
    private INotifyCollectionChanged? _subscribedCollection;
    private bool _isPanning;
    private Point _lastPanPoint;
    private bool _doubleClickHandledOnPress;

    /// <summary>
    /// Raised when a stage tile is double-clicked to return to the single-stage viewport.
    /// </summary>
    public event EventHandler<PrimaryStageId>? StageDoubleClicked;

    /// <summary>
    /// Initializes a new <see cref="StageGridView"/>.
    /// </summary>
    public StageGridView()
    {
        InitializeComponent();
        GridItemsControl.ContainerPrepared += OnTileContainerPrepared;
    }

    /// <summary>
    /// Gets or sets the ordered collection of primary stage card view models.
    /// </summary>
    public IReadOnlyList<StageCardViewModel>? Stages
    {
        get => GetValue(StagesProperty);
        set => SetValue(StagesProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected primary stage card.
    /// </summary>
    public StageCardViewModel? SelectedStage
    {
        get => GetValue(SelectedStageProperty);
        set => SetValue(SelectedStageProperty, value);
    }

    /// <summary>
    /// Gets or sets the synchronized zoom level shared by all stage tiles (<c>0.1</c> to <c>16.0</c>).
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// Gets or sets the synchronized horizontal pan offset shared by all stage tiles.
    /// </summary>
    public double PanX
    {
        get => GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    /// <summary>
    /// Gets or sets the synchronized vertical pan offset shared by all stage tiles.
    /// </summary>
    public double PanY
    {
        get => GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when a stage tile is double-clicked.
    /// Receives the activated <see cref="StageCardViewModel"/> (or its <see cref="PrimaryStageId"/>).
    /// </summary>
    public ICommand? ActivateStageCommand
    {
        get => GetValue(ActivateStageCommandProperty);
        set => SetValue(ActivateStageCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when a tile's pin button is clicked.
    /// </summary>
    public ICommand? TogglePinStageCommand
    {
        get => GetValue(TogglePinStageCommandProperty);
        set => SetValue(TogglePinStageCommandProperty, value);
    }

    /// <summary>
    /// Gets the formatted zoom percentage string for display in the grid toolbar.
    /// </summary>
    public string ZoomText => FormatZoomText(Zoom);

    /// <summary>
    /// Gets the active bitmap interpolation mode (<see cref="BitmapInterpolationMode.None"/> at <c>Zoom &gt;= 3.0</c>,
    /// <see cref="BitmapInterpolationMode.HighQuality"/> below <c>3.0</c>).
    /// </summary>
    public BitmapInterpolationMode ActiveInterpolationMode => GetInterpolationMode(Zoom);

    /// <summary>
    /// Resets <see cref="Zoom"/>, <see cref="PanX"/>, and <see cref="PanY"/> to the default fitted view.
    /// </summary>
    public void ResetView()
    {
        Zoom = 1.0;
        PanX = 0.0;
        PanY = 0.0;
    }

    /// <summary>
    /// Zooms all synchronized grid tiles around each tile's center.
    /// </summary>
    public void ZoomAroundCenter(double targetZoom)
    {
        double oldZoom = Zoom;
        double newZoom = CoerceZoom(this, targetZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-9 || oldZoom <= 0.0)
        {
            return;
        }

        double ratio = newZoom / oldZoom;
        Zoom = newZoom;
        PanX *= ratio;
        PanY *= ratio;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StagesProperty)
        {
            RefreshCardSubscriptions();
        }
        else if (change.Property == SelectedStageProperty)
        {
            if (change.OldValue is StageCardViewModel oldCard)
            {
                oldCard.IsSelected = false;
            }

            StageCardViewModel? newCard = change.NewValue as StageCardViewModel;
            if (Stages is { } stages)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    stages[i].IsSelected = ReferenceEquals(stages[i], newCard);
                }
            }
            else if (newCard is not null)
            {
                newCard.IsSelected = true;
            }
        }
        else if (change.Property == ZoomProperty)
        {
            double oldZoom = change.GetOldValue<double>();
            double newZoom = change.GetNewValue<double>();

            string oldText = FormatZoomText(oldZoom);
            string newText = FormatZoomText(newZoom);
            if (!string.Equals(oldText, newText, StringComparison.Ordinal))
            {
                RaisePropertyChanged(ZoomTextProperty, oldText, newText);
            }

            BitmapInterpolationMode oldMode = GetInterpolationMode(oldZoom);
            BitmapInterpolationMode newMode = GetInterpolationMode(newZoom);
            if (oldMode != newMode)
            {
                RaisePropertyChanged(ActiveInterpolationModeProperty, oldMode, newMode);
                UpdateTileInterpolationModes();
            }
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshCardSubscriptions();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ClearCardSubscriptions();
        _isPanning = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnResetViewClick(object? sender, RoutedEventArgs e)
    {
        ResetView();
    }

    private void OnTileContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        UpdateContainerInterpolationMode(e.Container, ActiveInterpolationMode);
        Dispatcher.UIThread.Post(UpdateTileInterpolationModes, DispatcherPriority.Loaded);
    }

    private void UpdateTileInterpolationModes()
    {
        if (GridItemsControl is null)
        {
            return;
        }

        BitmapInterpolationMode mode = ActiveInterpolationMode;
        foreach (Image image in GridItemsControl.GetVisualDescendants().OfType<Image>())
        {
            if (image.Classes.Contains("tile-image"))
            {
                RenderOptions.SetBitmapInterpolationMode(image, mode);
            }
        }
    }

    private static void UpdateContainerInterpolationMode(Control container, BitmapInterpolationMode mode)
    {
        foreach (Image image in container.GetVisualDescendants().OfType<Image>())
        {
            if (image.Classes.Contains("tile-image"))
            {
                RenderOptions.SetBitmapInterpolationMode(image, mode);
            }
        }
    }

    private void OnTilePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border tile || tile.DataContext is not StageCardViewModel card)
        {
            return;
        }

        if (e.Source is Visual sourceVisual && sourceVisual.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        PointerPointProperties props = e.GetCurrentPoint(tile).Properties;
        if (props.IsLeftButtonPressed)
        {
            SelectedStage = card;

            if (e.ClickCount >= 2)
            {
                _isPanning = false;
                _doubleClickHandledOnPress = true;
                ActivateStage(card);
                e.Handled = true;
                return;
            }

            _doubleClickHandledOnPress = false;
        }
        else if (!props.IsMiddleButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _lastPanPoint = e.GetPosition(this);
        e.Pointer.Capture(tile);
        e.Handled = true;
    }

    private void OnTilePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        Point current = e.GetPosition(this);
        Vector delta = current - _lastPanPoint;
        _lastPanPoint = current;

        PanX += delta.X;
        PanY += delta.Y;
        e.Handled = true;
    }

    private void OnTilePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        if (ReferenceEquals(e.Pointer.Captured, sender))
        {
            e.Pointer.Capture(null);
        }

        e.Handled = true;
    }

    private void OnTilePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
    }

    private void OnTilePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not Border tile || Math.Abs(e.Delta.Y) < 1e-6)
        {
            return;
        }

        double oldZoom = Zoom;
        double factor = Math.Pow(ZoomStepBase, e.Delta.Y);
        double newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-9)
        {
            e.Handled = true;
            return;
        }

        Border? viewport = FindTileViewport(tile);
        Vector cursorOffset = default;
        if (viewport is not null && viewport.Bounds.Width > 0.0 && viewport.Bounds.Height > 0.0)
        {
            Point cursorPos = e.GetPosition(viewport);
            Point center = new(viewport.Bounds.Width * 0.5, viewport.Bounds.Height * 0.5);
            cursorOffset = cursorPos - center;
        }

        double ratio = newZoom / oldZoom;
        double nextPanX = cursorOffset.X * (1.0 - ratio) + PanX * ratio;
        double nextPanY = cursorOffset.Y * (1.0 - ratio) + PanY * ratio;

        Zoom = newZoom;
        PanX = nextPanX;
        PanY = nextPanY;
        e.Handled = true;
    }

    private void OnPinButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StageCardViewModel card)
        {
            return;
        }

        e.Handled = true;
        ICommand? command = TogglePinStageCommand;
        if (command is null)
        {
            card.IsPinned = !card.IsPinned;
            return;
        }

        if (command is IRelayCommand<StageCardViewModel> cardCommand && cardCommand.CanExecute(card))
        {
            cardCommand.Execute(card);
        }
        else if (command is IRelayCommand<PrimaryStageId> idCommand && idCommand.CanExecute(card.StageId))
        {
            idCommand.Execute(card.StageId);
        }
        else if (command.CanExecute(card))
        {
            command.Execute(card);
        }
        else if (command.CanExecute(card.StageId))
        {
            command.Execute(card.StageId);
        }
    }

    private void OnTileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual sourceVisual && sourceVisual.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            e.Handled = true;
            return;
        }
        if (_doubleClickHandledOnPress)
        {
            _doubleClickHandledOnPress = false;
            e.Handled = true;
            return;
        }

        if (sender is not Border tile || tile.DataContext is not StageCardViewModel card)
        {
            return;
        }

        SelectedStage = card;
        ActivateStage(card);
        e.Handled = true;
    }

    private void ActivateStage(StageCardViewModel card)
    {
        StageDoubleClicked?.Invoke(this, card.StageId);

        ICommand? command = ActivateStageCommand;
        if (command is null)
        {
            return;
        }

        if (command is IRelayCommand<PrimaryStageId> stageIdCommand)
        {
            if (stageIdCommand.CanExecute(card.StageId))
            {
                stageIdCommand.Execute(card.StageId);
            }

            return;
        }

        if (command is IRelayCommand<PrimaryStageId?> nullableStageIdCommand)
        {
            if (nullableStageIdCommand.CanExecute(card.StageId))
            {
                nullableStageIdCommand.Execute(card.StageId);
            }

            return;
        }

        if (command is IRelayCommand<StageCardViewModel> cardCommand)
        {
            if (cardCommand.CanExecute(card))
            {
                cardCommand.Execute(card);
            }

            return;
        }

        if (command.CanExecute(card))
        {
            command.Execute(card);
        }
        else if (command.CanExecute(card.StageId))
        {
            command.Execute(card.StageId);
        }
    }

    private void RefreshCardSubscriptions()
    {
        ClearCardSubscriptions();

        IReadOnlyList<StageCardViewModel>? stages = Stages;
        if (stages is null)
        {
            return;
        }

        if (stages is INotifyCollectionChanged notifyCollection)
        {
            _subscribedCollection = notifyCollection;
            notifyCollection.CollectionChanged += OnStagesCollectionChanged;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageCardViewModel card = stages[i];
            card.BitmapUpdated += OnCardBitmapUpdated;
            _subscribedCards.Add(card);
        }
    }

    private void ClearCardSubscriptions()
    {
        if (_subscribedCollection is not null)
        {
            _subscribedCollection.CollectionChanged -= OnStagesCollectionChanged;
            _subscribedCollection = null;
        }

        for (int i = 0; i < _subscribedCards.Count; i++)
        {
            _subscribedCards[i].BitmapUpdated -= OnCardBitmapUpdated;
        }

        _subscribedCards.Clear();
    }

    private void OnStagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshCardSubscriptions();
    }

    private void OnCardBitmapUpdated(object? sender, EventArgs e)
    {
        if (sender is not StageCardViewModel card || GridItemsControl is null)
        {
            return;
        }

        foreach (Image image in GridItemsControl.GetVisualDescendants().OfType<Image>())
        {
            if (ReferenceEquals(image.DataContext, card))
            {
                image.InvalidateVisual();
            }
        }
    }

    private static Border? FindTileViewport(Border tile)
    {
        foreach (Border child in tile.GetVisualDescendants().OfType<Border>())
        {
            if (child.Classes.Contains("tile-viewport"))
            {
                return child;
            }
        }

        return null;
    }

    private static double CoerceZoom(AvaloniaObject _, double value) =>
        double.IsFinite(value) ? Math.Clamp(value, MinZoom, MaxZoom) : 1.0;

    private static double CoerceFiniteOffset(AvaloniaObject _, double value) =>
        double.IsFinite(value) ? value : 0.0;

    private static string FormatZoomText(double zoom) =>
        $"{Math.Round(zoom * 100.0):0}%";

    private static BitmapInterpolationMode GetInterpolationMode(double zoom) =>
        zoom >= PixelInspectionZoomThreshold
            ? BitmapInterpolationMode.None
            : BitmapInterpolationMode.HighQuality;
}
