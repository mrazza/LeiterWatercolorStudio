using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Interactive zoomable, pannable, split-wipe, and side-by-side image viewport with floating HUD overlays.
/// </summary>
public partial class ZoomPanViewport : UserControl
{
    /// <summary>
    /// Minimum supported zoom factor (10%).
    /// </summary>
    public const double MinZoom = 0.1;

    /// <summary>
    /// Maximum supported zoom factor (1600%).
    /// </summary>
    public const double MaxZoom = 16.0;

    private const double PixelSnapThresholdZoom = 3.0;
    internal const double PixelGridThresholdZoom = 8.0;
    private const double SideBySideDividerWidth = 2.0;
    private const double WipeHitHalfWidth = 10.0;
    private const double ZoomButtonStep = 1.25;
    private const double WheelZoomBase = 1.15;

    public static readonly StyledProperty<WriteableBitmap?> PrimaryBitmapProperty =
        AvaloniaProperty.Register<ZoomPanViewport, WriteableBitmap?>(nameof(PrimaryBitmap));

    public static readonly StyledProperty<WriteableBitmap?> CompareBitmapProperty =
        AvaloniaProperty.Register<ZoomPanViewport, WriteableBitmap?>(nameof(CompareBitmap));

    public static readonly StyledProperty<CompareMode> CompareModeProperty =
        AvaloniaProperty.Register<ZoomPanViewport, CompareMode>(nameof(CompareMode), CompareMode.Off);

    public static readonly StyledProperty<bool> IsHoldComparingProperty =
        AvaloniaProperty.Register<ZoomPanViewport, bool>(nameof(IsHoldComparing), false);

    public static readonly StyledProperty<double> WipePositionProperty =
        AvaloniaProperty.Register<ZoomPanViewport, double>(
            nameof(WipePosition),
            defaultValue: 0.5,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: CoerceWipePosition);

    public static readonly StyledProperty<string?> PrimaryLabelProperty =
        AvaloniaProperty.Register<ZoomPanViewport, string?>(nameof(PrimaryLabel));

    public static readonly StyledProperty<string?> CompareLabelProperty =
        AvaloniaProperty.Register<ZoomPanViewport, string?>(nameof(CompareLabel));

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ZoomPanViewport, double>(
            nameof(Zoom),
            defaultValue: 1.0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: CoerceZoom);

    public static readonly StyledProperty<double> PanXProperty =
        AvaloniaProperty.Register<ZoomPanViewport, double>(
            nameof(PanX),
            defaultValue: 0.0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: CoerceFinite);

    public static readonly StyledProperty<double> PanYProperty =
        AvaloniaProperty.Register<ZoomPanViewport, double>(
            nameof(PanY),
            defaultValue: 0.0,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay,
            coerce: CoerceFinite);

    public static readonly StyledProperty<string?> HoverHudTextProperty =
        AvaloniaProperty.Register<ZoomPanViewport, string?>(nameof(HoverHudText));

    public static readonly StyledProperty<string?> HoverHudDetailsTextProperty =
        AvaloniaProperty.Register<ZoomPanViewport, string?>(nameof(HoverHudDetailsText));

    public static readonly StyledProperty<bool> ShowHoverDiagnosticsProperty =
        AvaloniaProperty.Register<ZoomPanViewport, bool>(
            nameof(ShowHoverDiagnostics),
            defaultValue: false,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<Color?> HoverSwatchColorProperty =
        AvaloniaProperty.Register<ZoomPanViewport, Color?>(nameof(HoverSwatchColor));

    public static readonly StyledProperty<IReadOnlyList<VisualizationMode>?> AvailableVisualizationModesProperty =
        AvaloniaProperty.Register<ZoomPanViewport, IReadOnlyList<VisualizationMode>?>(nameof(AvailableVisualizationModes));

    public static readonly StyledProperty<VisualizationMode> SelectedVisualizationModeProperty =
        AvaloniaProperty.Register<ZoomPanViewport, VisualizationMode>(
            nameof(SelectedVisualizationMode),
            defaultValue: VisualizationMode.Rgb,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowReseedButtonProperty =
        AvaloniaProperty.Register<ZoomPanViewport, bool>(nameof(ShowReseedButton), false);

    public static readonly StyledProperty<bool> IsEmptyProperty =
        AvaloniaProperty.Register<ZoomPanViewport, bool>(nameof(IsEmpty), true);

    public static readonly StyledProperty<bool> IsProcessingProperty =
        AvaloniaProperty.Register<ZoomPanViewport, bool>(nameof(IsProcessing), false);

    public static readonly StyledProperty<string?> ProcessingStatusTextProperty =
        AvaloniaProperty.Register<ZoomPanViewport, string?>(nameof(ProcessingStatusText));

    public static readonly StyledProperty<ICommand?> OpenImageCommandProperty =
        AvaloniaProperty.Register<ZoomPanViewport, ICommand?>(nameof(OpenImageCommand));

    public static readonly StyledProperty<ICommand?> SampleImageCommandProperty =
        AvaloniaProperty.Register<ZoomPanViewport, ICommand?>(nameof(SampleImageCommand));

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<ZoomPanViewport, ICommand?>(nameof(CancelCommand));

    public static readonly StyledProperty<ICommand?> ReseedCommandProperty =
        AvaloniaProperty.Register<ZoomPanViewport, ICommand?>(nameof(ReseedCommand));

    public static readonly StyledProperty<ICommand?> FileDroppedCommandProperty =
        AvaloniaProperty.Register<ZoomPanViewport, ICommand?>(nameof(FileDroppedCommand));

    public static readonly RoutedEvent<PixelHoverEventArgs> HoverPixelChangedEvent =
        RoutedEvent.Register<ZoomPanViewport, PixelHoverEventArgs>(
            nameof(HoverPixelChanged),
            RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> HoverExitedEvent =
        RoutedEvent.Register<ZoomPanViewport, RoutedEventArgs>(
            nameof(HoverExited),
            RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> ReseedRequestedEvent =
        RoutedEvent.Register<ZoomPanViewport, RoutedEventArgs>(
            nameof(ReseedRequested),
            RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> OpenImageRequestedEvent =
        RoutedEvent.Register<ZoomPanViewport, RoutedEventArgs>(
            nameof(OpenImageRequested),
            RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> SampleImageRequestedEvent =
        RoutedEvent.Register<ZoomPanViewport, RoutedEventArgs>(
            nameof(SampleImageRequested),
            RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> CancelRequestedEvent =
        RoutedEvent.Register<ZoomPanViewport, RoutedEventArgs>(
            nameof(CancelRequested),
            RoutingStrategies.Bubble);

    public static readonly RoutedEvent<FileDroppedEventArgs> FileDroppedEvent =
        RoutedEvent.Register<ZoomPanViewport, FileDroppedEventArgs>(
            nameof(FileDropped),
            RoutingStrategies.Bubble);

    private static readonly Cursor DefaultCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor SizeWeCursor = new(StandardCursorType.SizeWestEast);

    private readonly List<(Button Button, VisualizationMode Mode)> _modeButtons = [];
    private bool _isPanning;
    private bool _isDraggingWipe;
    private bool _isSpaceDown;
    private Point _lastDragPoint;
    private PixelPoint? _lastHoverPixel;
    private bool _lastHoverIsCompare;
    private bool _isSyncingDiagnosticsToggle;

    /// <summary>
    /// Initializes a new instance of <see cref="ZoomPanViewport"/>.
    /// </summary>
    public ZoomPanViewport()
    {
        InitializeComponent();

        PART_Surface.Owner = this;

        PART_OpenImageButton.Click += (_, _) => RequestOpenImage();
        PART_TrySampleButton.Click += (_, _) => RequestSampleImage();
        PART_CancelButton.Click += (_, _) => RequestCancel();
        PART_ReseedButton.Click += (_, _) => RequestReseed();
        PART_FitButton.Click += (_, _) => FitToView();
        PART_OneToOneButton.Click += (_, _) => ZoomOneToOne();
        PART_ZoomOutButton.Click += (_, _) => ZoomAroundCenter(Zoom / ZoomButtonStep);
        PART_ZoomInButton.Click += (_, _) => ZoomAroundCenter(Zoom * ZoomButtonStep);
        PART_DiagnosticsToggle.IsCheckedChanged += (_, _) =>
        {
            if (_isSyncingDiagnosticsToggle)
            {
                return;
            }

            ShowHoverDiagnostics = PART_DiagnosticsToggle.IsChecked == true;
        };

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        ApplyInterpolationMode();
        UpdateAllVisualState();
    }

    public WriteableBitmap? PrimaryBitmap
    {
        get => GetValue(PrimaryBitmapProperty);
        set => SetValue(PrimaryBitmapProperty, value);
    }

    public WriteableBitmap? CompareBitmap
    {
        get => GetValue(CompareBitmapProperty);
        set => SetValue(CompareBitmapProperty, value);
    }

    public CompareMode CompareMode
    {
        get => GetValue(CompareModeProperty);
        set => SetValue(CompareModeProperty, value);
    }

    public bool IsHoldComparing
    {
        get => GetValue(IsHoldComparingProperty);
        set => SetValue(IsHoldComparingProperty, value);
    }

    public double WipePosition
    {
        get => GetValue(WipePositionProperty);
        set => SetValue(WipePositionProperty, value);
    }

    public string? PrimaryLabel
    {
        get => GetValue(PrimaryLabelProperty);
        set => SetValue(PrimaryLabelProperty, value);
    }

    public string? CompareLabel
    {
        get => GetValue(CompareLabelProperty);
        set => SetValue(CompareLabelProperty, value);
    }

    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double PanX
    {
        get => GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public double PanY
    {
        get => GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    public string? HoverHudText
    {
        get => GetValue(HoverHudTextProperty);
        set => SetValue(HoverHudTextProperty, value);
    }

    public string? HoverHudDetailsText
    {
        get => GetValue(HoverHudDetailsTextProperty);
        set => SetValue(HoverHudDetailsTextProperty, value);
    }

    public bool ShowHoverDiagnostics
    {
        get => GetValue(ShowHoverDiagnosticsProperty);
        set => SetValue(ShowHoverDiagnosticsProperty, value);
    }

    public Color? HoverSwatchColor
    {
        get => GetValue(HoverSwatchColorProperty);
        set => SetValue(HoverSwatchColorProperty, value);
    }

    public IReadOnlyList<VisualizationMode>? AvailableVisualizationModes
    {
        get => GetValue(AvailableVisualizationModesProperty);
        set => SetValue(AvailableVisualizationModesProperty, value);
    }

    public VisualizationMode SelectedVisualizationMode
    {
        get => GetValue(SelectedVisualizationModeProperty);
        set => SetValue(SelectedVisualizationModeProperty, value);
    }

    public bool ShowReseedButton
    {
        get => GetValue(ShowReseedButtonProperty);
        set => SetValue(ShowReseedButtonProperty, value);
    }

    public bool IsEmpty
    {
        get => GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    public bool IsProcessing
    {
        get => GetValue(IsProcessingProperty);
        set => SetValue(IsProcessingProperty, value);
    }

    public string? ProcessingStatusText
    {
        get => GetValue(ProcessingStatusTextProperty);
        set => SetValue(ProcessingStatusTextProperty, value);
    }

    public ICommand? OpenImageCommand
    {
        get => GetValue(OpenImageCommandProperty);
        set => SetValue(OpenImageCommandProperty, value);
    }

    public ICommand? SampleImageCommand
    {
        get => GetValue(SampleImageCommandProperty);
        set => SetValue(SampleImageCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public ICommand? ReseedCommand
    {
        get => GetValue(ReseedCommandProperty);
        set => SetValue(ReseedCommandProperty, value);
    }

    public ICommand? FileDroppedCommand
    {
        get => GetValue(FileDroppedCommandProperty);
        set => SetValue(FileDroppedCommandProperty, value);
    }

    public event EventHandler<PixelHoverEventArgs>? HoverPixelChanged
    {
        add => AddHandler(HoverPixelChangedEvent, value);
        remove => RemoveHandler(HoverPixelChangedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? HoverExited
    {
        add => AddHandler(HoverExitedEvent, value);
        remove => RemoveHandler(HoverExitedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? ReseedRequested
    {
        add => AddHandler(ReseedRequestedEvent, value);
        remove => RemoveHandler(ReseedRequestedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? OpenImageRequested
    {
        add => AddHandler(OpenImageRequestedEvent, value);
        remove => RemoveHandler(OpenImageRequestedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? SampleImageRequested
    {
        add => AddHandler(SampleImageRequestedEvent, value);
        remove => RemoveHandler(SampleImageRequestedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? CancelRequested
    {
        add => AddHandler(CancelRequestedEvent, value);
        remove => RemoveHandler(CancelRequestedEvent, value);
    }

    public event EventHandler<FileDroppedEventArgs>? FileDropped
    {
        add => AddHandler(FileDroppedEvent, value);
        remove => RemoveHandler(FileDroppedEvent, value);
    }

    /// <summary>
    /// Fits the active image comfortably within the viewport pane(s) and resets pan offsets to zero.
    /// </summary>
    public void FitToView()
    {
        WriteableBitmap? referenceBitmap = PrimaryBitmap ?? CompareBitmap;
        if (referenceBitmap is null || referenceBitmap.PixelSize.Width <= 0 || referenceBitmap.PixelSize.Height <= 0)
        {
            Zoom = 1.0;
            PanX = 0.0;
            PanY = 0.0;
            return;
        }

        Size paneSize = GetReferencePaneSize();
        if (paneSize.Width <= 0 || paneSize.Height <= 0)
        {
            Zoom = 1.0;
            PanX = 0.0;
            PanY = 0.0;
            return;
        }

        double padding = Math.Min(32.0, Math.Min(paneSize.Width, paneSize.Height) * 0.1);
        double availableWidth = Math.Max(1.0, paneSize.Width - padding);
        double availableHeight = Math.Max(1.0, paneSize.Height - padding);

        double fitZoom = Math.Min(
            availableWidth / referenceBitmap.PixelSize.Width,
            availableHeight / referenceBitmap.PixelSize.Height);

        Zoom = CoerceZoom(this, fitZoom);
        PanX = 0.0;
        PanY = 0.0;
    }

    /// <summary>
    /// Fits the image to the active pane when the current zoom or pan overflows the pane bounds
    /// (for example, when entering Side-by-Side comparison while zoomed).
    /// </summary>
    public void FitToViewIfNeeded()
    {
        WriteableBitmap? referenceBitmap = PrimaryBitmap ?? CompareBitmap;
        if (referenceBitmap is null || referenceBitmap.PixelSize.Width <= 0 || referenceBitmap.PixelSize.Height <= 0)
        {
            return;
        }

        Size paneSize = GetReferencePaneSize();
        if (paneSize.Width <= 0 || paneSize.Height <= 0)
        {
            return;
        }

        double renderedW = referenceBitmap.PixelSize.Width * Zoom;
        double renderedH = referenceBitmap.PixelSize.Height * Zoom;
        double halfPaneW = paneSize.Width * 0.5;
        double halfPaneH = paneSize.Height * 0.5;
        bool overflows =
            renderedW > paneSize.Width + 2.0 ||
            renderedH > paneSize.Height + 2.0 ||
            Math.Abs(PanX) + renderedW * 0.5 > halfPaneW + 2.0 ||
            Math.Abs(PanY) + renderedH * 0.5 > halfPaneH + 2.0;

        if (overflows)
        {
            FitToView();
        }
    }

    /// <summary>
    /// Overrides split-wipe handle capture while the user holds Space to pan anywhere.
    /// </summary>
    public void SetSpacePanOverride(bool active)
    {
        _isSpaceDown = active;
        if (!_isPanning && !_isDraggingWipe)
        {
            Cursor = active ? HandCursor : DefaultCursor;
        }
    }

    /// <summary>
    /// Resets the viewport to 100% zoom (1:1 pixels) centered in the pane.
    /// </summary>
    public void ZoomOneToOne()
    {
        Zoom = 1.0;
        PanX = 0.0;
        PanY = 0.0;
    }

    /// <summary>
    /// Forces a surface redraw when an underlying <see cref="WriteableBitmap"/> is mutated in place.
    /// </summary>
    public void RefreshImage()
    {
        PART_Surface.InvalidateVisual();
    }

    /// <summary>
    /// Maps a point in viewport coordinates to zero-based integer image pixel coordinates,
    /// returning <see langword="null"/> when outside the rendered image bounds or when empty.
    /// </summary>
    public PixelPoint? ScreenToImagePixel(Point viewportPoint)
    {
        if (IsEmpty)
        {
            return null;
        }

        WriteableBitmap? referenceBitmap = PrimaryBitmap ?? CompareBitmap;
        if (referenceBitmap is null || referenceBitmap.PixelSize.Width <= 0 || referenceBitmap.PixelSize.Height <= 0)
        {
            return null;
        }

        Size viewportSize = GetEffectiveViewportSize();
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
        {
            return null;
        }

        Rect paneRect = ResolvePaneAtPoint(viewportPoint, viewportSize);
        if (paneRect.Width <= 0 || paneRect.Height <= 0 || !paneRect.Contains(viewportPoint))
        {
            return null;
        }

        int imageWidth = referenceBitmap.PixelSize.Width;
        int imageHeight = referenceBitmap.PixelSize.Height;

        if (CompareMode == CompareMode.SideBySide && paneRect.X == 0 && CompareBitmap is not null && PrimaryBitmap is null)
        {
            imageWidth = CompareBitmap.PixelSize.Width;
            imageHeight = CompareBitmap.PixelSize.Height;
        }

        double localX = viewportPoint.X - paneRect.X;
        double localY = viewportPoint.Y - paneRect.Y;

        double imageX = (localX - paneRect.Width * 0.5 - PanX) / Zoom + imageWidth * 0.5;
        double imageY = (localY - paneRect.Height * 0.5 - PanY) / Zoom + imageHeight * 0.5;

        int pixelX = (int)Math.Floor(imageX);
        int pixelY = (int)Math.Floor(imageY);

        if (pixelX < 0 || pixelX >= imageWidth || pixelY < 0 || pixelY >= imageHeight)
        {
            return null;
        }

        return new PixelPoint(pixelX, pixelY);
    }

    internal Size GetEffectiveViewportSize()
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            return Bounds.Size;
        }

        double width = !double.IsNaN(Width) && Width > 0 ? Width : 0.0;
        double height = !double.IsNaN(Height) && Height > 0 ? Height : 0.0;
        return new Size(width, height);
    }

    internal (Rect LeftPane, Rect RightPane, double DividerX) GetSideBySidePanes(Size viewportSize)
    {
        double leftWidth = Math.Max(0.0, Math.Floor((viewportSize.Width - SideBySideDividerWidth) * 0.5));
        double rightX = leftWidth + SideBySideDividerWidth;
        double rightWidth = Math.Max(0.0, viewportSize.Width - rightX);

        return (
            new Rect(0, 0, leftWidth, viewportSize.Height),
            new Rect(rightX, 0, rightWidth, viewportSize.Height),
            leftWidth);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        AvaloniaProperty property = change.Property;
        if (property == ZoomProperty)
        {
            ApplyInterpolationMode();
            UpdateZoomText();
            PART_Surface.InvalidateVisual();
        }
        else if (property == PanXProperty ||
                 property == PanYProperty ||
                 property == PrimaryBitmapProperty ||
                 property == CompareBitmapProperty)
        {
            if (property == PrimaryBitmapProperty && PrimaryBitmap is null)
            {
                ClearHoverPixel();
            }

            PART_Surface.InvalidateVisual();
        }
        else if (property == WipePositionProperty)
        {
            UpdateWipeOverlayLayout();
            PART_Surface.InvalidateVisual();
        }
        else if (property == CompareModeProperty ||
                 property == IsHoldComparingProperty ||
                 property == PrimaryLabelProperty ||
                 property == CompareLabelProperty)
        {
            UpdateCompareVisuals();
            PART_Surface.InvalidateVisual();
        }
        else if (property == IsEmptyProperty)
        {
            if (IsEmpty)
            {
                ClearHoverPixel();
            }

            UpdateVisibilityState();
            PART_Surface.InvalidateVisual();
        }
        else if (property == HoverHudTextProperty ||
                 property == HoverHudDetailsTextProperty ||
                 property == ShowHoverDiagnosticsProperty ||
                 property == HoverSwatchColorProperty)
        {
            UpdateHoverHud();
        }
        else if (property == AvailableVisualizationModesProperty)
        {
            RebuildVisualizationModeChips();
        }
        else if (property == SelectedVisualizationModeProperty)
        {
            UpdateActiveModeChipHighlight();
        }
        else if (property == ShowReseedButtonProperty)
        {
            UpdateModeBarVisibility();
        }
        else if (property == IsProcessingProperty || property == ProcessingStatusTextProperty)
        {
            UpdateProcessingIndicator();
        }
        else if (property == BoundsProperty)
        {
            UpdateWipeOverlayLayout();
            UpdateModeBarPosition();
            PART_Surface.InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space && !IsEmpty && e.Source is not Button)
        {
            _isSpaceDown = true;
            UpdateCursorForPoint(_lastDragPoint);
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Key == Key.Space)
        {
            _isSpaceDown = false;
            if (!_isPanning && !_isDraggingWipe)
            {
                Cursor = DefaultCursor;
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (IsEmpty)
        {
            return;
        }

        Focus();
        PointerPoint point = e.GetCurrentPoint(this);
        Point pos = point.Position;
        _lastDragPoint = pos;

        bool isLeft = point.Properties.IsLeftButtonPressed;
        bool isMiddle = point.Properties.IsMiddleButtonPressed;

        if (isLeft && !_isSpaceDown && IsPointOnWipeHandle(pos))
        {
            _isDraggingWipe = true;
            e.Pointer.Capture(this);
            UpdateWipeFromPoint(pos);
            Cursor = SizeWeCursor;
            e.Handled = true;
            return;
        }

        if (isMiddle || isLeft)
        {
            _isPanning = true;
            e.Pointer.Capture(this);
            Cursor = HandCursor;
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (IsEmpty)
        {
            return;
        }

        Point pos = e.GetPosition(this);

        if (_isDraggingWipe)
        {
            UpdateWipeFromPoint(pos);
            e.Handled = true;
            return;
        }

        if (_isPanning)
        {
            Vector delta = pos - _lastDragPoint;
            _lastDragPoint = pos;
            PanX += delta.X;
            PanY += delta.Y;
            UpdateHoverFromPoint(pos);
            e.Handled = true;
            return;
        }

        _lastDragPoint = pos;
        UpdateCursorForPoint(pos);
        UpdateHoverFromPoint(pos);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDraggingWipe || _isPanning)
        {
            _isDraggingWipe = false;
            _isPanning = false;
            if (Equals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(null);
            }

            UpdateCursorForPoint(e.GetPosition(this));
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        _isDraggingWipe = false;
        _isPanning = false;
        Cursor = _isSpaceDown ? HandCursor : DefaultCursor;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (!_isPanning && !_isDraggingWipe)
        {
            Cursor = DefaultCursor;
            ClearHoverPixel();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (IsEmpty || (PrimaryBitmap is null && CompareBitmap is null))
        {
            return;
        }

        double deltaY = e.Delta.Y;
        if (Math.Abs(deltaY) < 1e-6)
        {
            return;
        }

        double factor = Math.Pow(WheelZoomBase, deltaY);
        ZoomAtViewportPoint(Zoom * factor, e.GetPosition(this));
        e.Handled = true;
    }

    private static double CoerceZoom(AvaloniaObject _, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1.0;
        }

        return Math.Clamp(value, MinZoom, MaxZoom);
    }

    private static double CoerceWipePosition(AvaloniaObject _, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0.5;
        }

        return Math.Clamp(value, 0.0, 1.0);
    }

    private static double CoerceFinite(AvaloniaObject _, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0.0;
        }

        return value;
    }

    /// <summary>
    /// Zooms around the center of the active viewport pane.
    /// </summary>
    public void ZoomAroundCenter(double targetZoom)
    {
        Size size = GetEffectiveViewportSize();
        Point center = CompareMode == CompareMode.SideBySide && !IsHoldComparing
            ? GetSideBySidePanes(size).RightPane.Center
            : new Point(size.Width * 0.5, size.Height * 0.5);

        ZoomAtViewportPoint(targetZoom, center);
    }

    private void ZoomAtViewportPoint(double targetZoom, Point viewportPoint)
    {
        double oldZoom = Zoom;
        double newZoom = CoerceZoom(this, targetZoom);
        if (Math.Abs(newZoom - oldZoom) < 1e-9)
        {
            return;
        }

        Size viewportSize = GetEffectiveViewportSize();
        Rect paneRect = ResolvePaneAtPoint(viewportPoint, viewportSize);
        if (paneRect.Width > 0 && paneRect.Height > 0 && oldZoom > 0)
        {
            double offsetX = viewportPoint.X - paneRect.X - paneRect.Width * 0.5;
            double offsetY = viewportPoint.Y - paneRect.Y - paneRect.Height * 0.5;
            double ratio = newZoom / oldZoom;

            PanX = offsetX - (offsetX - PanX) * ratio;
            PanY = offsetY - (offsetY - PanY) * ratio;
        }

        Zoom = newZoom;
    }

    private Size GetReferencePaneSize()
    {
        Size viewportSize = GetEffectiveViewportSize();
        if (CompareMode == CompareMode.SideBySide)
        {
            return GetSideBySidePanes(viewportSize).RightPane.Size;
        }

        return viewportSize;
    }

    private Rect ResolvePaneAtPoint(Point viewportPoint, Size viewportSize)
    {
        if (CompareMode == CompareMode.SideBySide && !IsHoldComparing)
        {
            (Rect leftPane, Rect rightPane, _) = GetSideBySidePanes(viewportSize);
            return viewportPoint.X <= leftPane.Right ? leftPane : rightPane;
        }

        return new Rect(viewportSize);
    }

    private bool IsPointOnWipeHandle(Point pos)
    {
        if (CompareMode != CompareMode.SplitWipe || IsHoldComparing || IsEmpty)
        {
            return false;
        }

        Size viewportSize = GetEffectiveViewportSize();
        if (viewportSize.Width <= 0)
        {
            return false;
        }

        double wipeX = WipePosition * viewportSize.Width;
        double handleCenterY = viewportSize.Height * 0.5;
        bool nearHandleCircle = Math.Abs(pos.X - wipeX) <= 18.0 && Math.Abs(pos.Y - handleCenterY) <= 18.0;
        bool nearWipeLine = Math.Abs(pos.X - wipeX) <= WipeHitHalfWidth;
        return nearHandleCircle || nearWipeLine;
    }

    private void UpdateWipeFromPoint(Point pos)
    {
        Size viewportSize = GetEffectiveViewportSize();
        if (viewportSize.Width <= 0)
        {
            return;
        }

        WipePosition = Math.Clamp(pos.X / viewportSize.Width, 0.0, 1.0);
    }

    private void UpdateCursorForPoint(Point pos)
    {
        if (_isDraggingWipe)
        {
            Cursor = SizeWeCursor;
            return;
        }

        if (_isPanning || _isSpaceDown)
        {
            Cursor = HandCursor;
            return;
        }

        Cursor = IsPointOnWipeHandle(pos) ? SizeWeCursor : DefaultCursor;
    }

    private void UpdateHoverFromPoint(Point pos)
    {
        PixelPoint? pixel = ScreenToImagePixel(pos);
        if (pixel is PixelPoint px)
        {
            bool isCompareSide = IsPointInComparePane(pos, GetEffectiveViewportSize());
            if (!_lastHoverPixel.HasValue || _lastHoverPixel.Value != px || _lastHoverIsCompare != isCompareSide)
            {
                _lastHoverPixel = px;
                _lastHoverIsCompare = isCompareSide;
                RaiseEvent(new PixelHoverEventArgs(HoverPixelChangedEvent, px.X, px.Y, isCompareSide));
            }

            return;
        }

        ClearHoverPixel();
    }

    private bool IsPointInComparePane(Point viewportPoint, Size viewportSize)
    {
        if (IsHoldComparing)
        {
            return true;
        }

        if (CompareMode == CompareMode.SideBySide)
        {
            (Rect leftPane, _, _) = GetSideBySidePanes(viewportSize);
            return leftPane.Contains(viewportPoint);
        }

        if (CompareMode == CompareMode.SplitWipe && viewportSize.Width > 0)
        {
            double wipeX = Math.Clamp(WipePosition, 0.0, 1.0) * viewportSize.Width;
            return viewportPoint.X < wipeX;
        }

        return false;
    }

    private void ClearHoverPixel()
    {
        if (_lastHoverPixel.HasValue)
        {
            _lastHoverPixel = null;
            _lastHoverIsCompare = false;
            RaiseEvent(new RoutedEventArgs(HoverExitedEvent));
        }
    }

    private void ApplyInterpolationMode()
    {
        BitmapInterpolationMode mode = Zoom >= PixelSnapThresholdZoom
            ? BitmapInterpolationMode.None
            : BitmapInterpolationMode.HighQuality;

        RenderOptions.SetBitmapInterpolationMode(this, mode);
        RenderOptions.SetBitmapInterpolationMode(PART_Surface, mode);
    }

    private void UpdateAllVisualState()
    {
        UpdateVisibilityState();
        UpdateZoomText();
        UpdateCompareVisuals();
        UpdateHoverHud();
        UpdateProcessingIndicator();
        RebuildVisualizationModeChips();
        UpdateModeBarVisibility();
    }

    private void UpdateVisibilityState()
    {
        bool hasImage = !IsEmpty;
        PART_EmptyState.IsVisible = IsEmpty;
        PART_HudOverlay.IsVisible = hasImage;
        UpdateCompareVisuals();
        UpdateProcessingIndicator();
        UpdateHoverHud();
    }

    private void UpdateZoomText()
    {
        PART_ZoomText.Text = $"{Math.Round(Zoom * 100.0):0}%";
    }

    private void UpdateCompareVisuals()
    {
        bool holdComparing = !IsEmpty && IsHoldComparing;
        bool showWipe = !IsEmpty && !holdComparing && CompareMode == CompareMode.SplitWipe;
        bool showSide = !IsEmpty && !holdComparing && CompareMode == CompareMode.SideBySide;

        PART_WipeOverlay.IsVisible = showWipe;
        PART_WipeLabels.IsVisible = showWipe;
        PART_SideBySideLabels.IsVisible = showSide;

        string? compareLabel = CompareLabel;
        string? primaryLabel = PrimaryLabel;
        bool hasCompareLabel = !string.IsNullOrWhiteSpace(compareLabel);
        bool hasPrimaryLabel = !string.IsNullOrWhiteSpace(primaryLabel);

        PART_HoldCompareLabel.Text = hasCompareLabel
            ? $"Comparing: {compareLabel} (Hold)"
            : "Comparing Reference";
        PART_HoldCompareBadge.IsVisible = holdComparing;

        PART_WipeCompareLabel.Text = compareLabel ?? string.Empty;
        PART_WipePrimaryLabel.Text = primaryLabel ?? string.Empty;
        PART_WipeCompareBadge.IsVisible = showWipe && hasCompareLabel;
        PART_WipePrimaryBadge.IsVisible = showWipe && hasPrimaryLabel;

        PART_SideCompareLabel.Text = compareLabel ?? string.Empty;
        PART_SidePrimaryLabel.Text = primaryLabel ?? string.Empty;
        PART_SideCompareBadge.IsVisible = showSide && hasCompareLabel;
        PART_SidePrimaryBadge.IsVisible = showSide && hasPrimaryLabel;

        if (showWipe)
        {
            UpdateWipeOverlayLayout();
        }

        UpdateModeBarPosition();
    }

    private void UpdateModeBarPosition()
    {
        if (!IsEmpty && !IsHoldComparing && CompareMode == CompareMode.SideBySide)
        {
            Size viewportSize = GetEffectiveViewportSize();
            if (viewportSize.Width > 0)
            {
                (Rect _, Rect rightPane, _) = GetSideBySidePanes(viewportSize);
                PART_ModeBar.Margin = new Thickness(Math.Max(0.0, rightPane.X), 0, 0, 0);
                return;
            }
        }

        PART_ModeBar.Margin = default;
    }

    private void UpdateWipeOverlayLayout()
    {
        if (CompareMode != CompareMode.SplitWipe || IsEmpty)
        {
            return;
        }

        Size viewportSize = GetEffectiveViewportSize();
        if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
        {
            return;
        }

        double wipeX = Math.Clamp(WipePosition, 0.0, 1.0) * viewportSize.Width;

        Canvas.SetLeft(PART_WipeLine, wipeX - 1.0);
        Canvas.SetTop(PART_WipeLine, 0.0);
        PART_WipeLine.Height = viewportSize.Height;

        Canvas.SetLeft(PART_WipeHandle, wipeX - PART_WipeHandle.Width * 0.5);
        Canvas.SetTop(PART_WipeHandle, Math.Max(0.0, viewportSize.Height * 0.5 - PART_WipeHandle.Height * 0.5));
    }

    private void UpdateHoverHud()
    {
        _isSyncingDiagnosticsToggle = true;
        try
        {
            if (PART_DiagnosticsToggle.IsChecked != ShowHoverDiagnostics)
            {
                PART_DiagnosticsToggle.IsChecked = ShowHoverDiagnostics;
            }
        }
        finally
        {
            _isSyncingDiagnosticsToggle = false;
        }

        bool show = !IsEmpty && !string.IsNullOrWhiteSpace(HoverHudText);
        PART_HoverHud.IsVisible = show;
        PART_HoverHudText.Text = HoverHudText ?? string.Empty;

        bool showDetails = show && ShowHoverDiagnostics && !string.IsNullOrWhiteSpace(HoverHudDetailsText);
        PART_HoverHudDetailsText.Text = HoverHudDetailsText ?? string.Empty;
        PART_HoverHudDetailsText.IsVisible = showDetails;

        if (HoverSwatchColor is Color swatch)
        {
            PART_HoverSwatch.Background = new SolidColorBrush(swatch);
            PART_HoverSwatch.IsVisible = true;
        }
        else
        {
            PART_HoverSwatch.IsVisible = false;
        }
    }

    private void UpdateProcessingIndicator()
    {
        PART_ProcessingPill.IsVisible = !IsEmpty && IsProcessing;
        PART_ProcessingText.Text = string.IsNullOrWhiteSpace(ProcessingStatusText)
            ? "Processing..."
            : ProcessingStatusText;
    }

    private void RebuildVisualizationModeChips()
    {
        PART_ModeChips.Children.Clear();
        _modeButtons.Clear();

        IReadOnlyList<VisualizationMode>? modes = AvailableVisualizationModes;
        if (modes is not null)
        {
            foreach (VisualizationMode mode in modes)
            {
                var button = new Button
                {
                    Content = FormatVisualizationModeName(mode),
                };
                button.Classes.Add("mode-chip");
                button.Click += (_, _) => SelectedVisualizationMode = mode;

                _modeButtons.Add((button, mode));
                PART_ModeChips.Children.Add(button);
            }
        }

        UpdateActiveModeChipHighlight();
        UpdateModeBarVisibility();
    }

    private void UpdateActiveModeChipHighlight()
    {
        VisualizationMode selected = SelectedVisualizationMode;
        foreach ((Button button, VisualizationMode mode) in _modeButtons)
        {
            button.Classes.Set("active", mode == selected);
        }
    }

    private void UpdateModeBarVisibility()
    {
        bool hasMultipleModes = _modeButtons.Count > 1;
        bool showReseed = ShowReseedButton;

        PART_ModeChips.IsVisible = hasMultipleModes;
        PART_ReseedButton.IsVisible = showReseed;
        PART_ReseedSeparator.IsVisible = hasMultipleModes && showReseed;
        PART_ModeBar.IsVisible = hasMultipleModes || showReseed;
    }

    private void RequestOpenImage()
    {
        RaiseEvent(new RoutedEventArgs(OpenImageRequestedEvent));
        if (OpenImageCommand is ICommand cmd && cmd.CanExecute(null))
        {
            cmd.Execute(null);
        }
    }

    private void RequestSampleImage()
    {
        RaiseEvent(new RoutedEventArgs(SampleImageRequestedEvent));
        if (SampleImageCommand is ICommand cmd && cmd.CanExecute(null))
        {
            cmd.Execute(null);
        }
    }

    private void RequestCancel()
    {
        RaiseEvent(new RoutedEventArgs(CancelRequestedEvent));
        if (CancelCommand is ICommand cmd && cmd.CanExecute(null))
        {
            cmd.Execute(null);
        }
    }

    private void RequestReseed()
    {
        RaiseEvent(new RoutedEventArgs(ReseedRequestedEvent));
        if (ReseedCommand is ICommand cmd && cmd.CanExecute(null))
        {
            cmd.Execute(null);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool hasFile = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
        PART_DropOverlay.IsVisible = hasFile;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        PART_DropOverlay.IsVisible = false;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        PART_DropOverlay.IsVisible = false;

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
                RaiseEvent(new FileDroppedEventArgs(FileDroppedEvent, localPath));
                if (FileDroppedCommand is ICommand cmd && cmd.CanExecute(localPath))
                {
                    cmd.Execute(localPath);
                }

                e.Handled = true;
                break;
            }
        }
    }

    private static string FormatVisualizationModeName(VisualizationMode mode) => mode switch
    {
        VisualizationMode.Rgb => "RGB",
        VisualizationMode.RegionAverageColor => "Region Avg",
        VisualizationMode.RegionRandomColor => "Region IDs",
        VisualizationMode.RegionBoundariesOverlay => "Boundaries",
        VisualizationMode.SalientRegionClassification => "Classification",
        VisualizationMode.Grayscale => "Grayscale",
        VisualizationMode.HeatmapTurbo => "Turbo",
        VisualizationMode.HeatmapViridis => "Viridis",
        VisualizationMode.MaskedColorOverlay => "Mask Overlay",
        VisualizationMode.BinaryMask => "Binary Mask",
        VisualizationMode.TranslucentTintOverlay => "Tint Overlay",
        VisualizationMode.ContourOverlay => "Contours",
        VisualizationMode.DistanceSmoothedGrayscale => "Smooth Gray",
        VisualizationMode.DistanceSmoothedHeatmap => "Smooth Heat",
        VisualizationMode.DistanceSmoothedContours => "Smooth Contour",
        VisualizationMode.DistanceRawGrayscale => "Raw Gray",
        VisualizationMode.DistanceRawHeatmap => "Raw Heat",
        VisualizationMode.DistanceRawContours => "Raw Contour",
        VisualizationMode.WetMaskBinary => "Wet Mask",
        VisualizationMode.WetMaskOverlay => "Wet Overlay",
        VisualizationMode.QualifyingBoundariesOverlay => "Qualifying Edges",
        VisualizationMode.IsolatedPigmentPreview => "Pigment",
        VisualizationMode.DensityHeatmap => "Density Heat",
        VisualizationMode.DensityGrayscale => "Density Gray",
        VisualizationMode.PaperHeightGrayscale => "Paper Gray",
        VisualizationMode.PaperHeightRelief => "Paper Relief",
        VisualizationMode.FxaaDiffMagnifier => "FXAA Diff",
        _ => mode.ToString(),
    };
}
