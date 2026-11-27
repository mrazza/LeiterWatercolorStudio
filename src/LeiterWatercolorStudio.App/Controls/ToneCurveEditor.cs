using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Leiter.Algorithms.Stylization;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Built-in artistic tone curve presets for <see cref="ToneCurveEditor"/>.
/// </summary>
public enum ToneCurvePreset
{
    /// <summary>
    /// Pure 1:1 linear identity curve with no shadow lift or highlight compression.
    /// </summary>
    Linear,

    /// <summary>
    /// Gentle watercolor wash curve with lifted shadows and cushioned paper highlights.
    /// </summary>
    SoftWash,

    /// <summary>
    /// Deeper S-curve contrast with crisp midtones and subtle roll-off.
    /// </summary>
    HighContrast,

    /// <summary>
    /// Airy high-key paper wash lifting shadow floor and softening top highlights.
    /// </summary>
    LiftedPaper,
}

/// <summary>
/// Interactive studio editor for <see cref="PiecewiseToneCurveOptions"/> rendering the exact
/// piecewise Hermite tone curve, optional luminance histogram backdrop, shadow/midtone/highlight
/// region guides, and 4 draggable/keyboard-navigable control handles.
/// </summary>
public sealed class ToneCurveEditor : Control
{
    private const double MinToneSeparation = 0.02;
    private const double MinKneeSeparation = 0.02;
    private const double KneeEdgeMargin = 0.01;
    private const double HandleHitRadius = 12.0;
    private const double HandleRadiusNormal = 5.5;
    private const double HandleRadiusHover = 7.0;
    private const double HandleRadiusActive = 7.5;
    private const double KeyboardNudgeStep = 0.02;
    private const double KeyboardFineNudgeStep = 0.005;

    private static readonly ToneCurveHandle[] HandleTabOrder =
    [
        ToneCurveHandle.MinTone,
        ToneCurveHandle.ShadowKnee,
        ToneCurveHandle.HighlightKnee,
        ToneCurveHandle.MaxTone,
    ];

    private static readonly DashStyle IdentityDashStyle = new([4.0, 4.0], 0.0);
    private static readonly DashStyle RegionDashStyle = new([3.0, 3.0], 0.0);

    private static readonly Cursor VerticalCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor HorizontalCursor = new(StandardCursorType.SizeWestEast);

    /// <summary>
    /// Identifies the <see cref="MinTone"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> MinToneProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(MinTone),
            PiecewiseToneCurveOptions.Default.MinTone,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="MaxTone"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> MaxToneProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(MaxTone),
            PiecewiseToneCurveOptions.Default.MaxTone,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="ShadowKnee"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowKneeProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(ShadowKnee),
            PiecewiseToneCurveOptions.Default.ShadowKnee,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="HighlightKnee"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> HighlightKneeProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(HighlightKnee),
            PiecewiseToneCurveOptions.Default.HighlightKnee,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="BaselineMinTone"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> BaselineMinToneProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(BaselineMinTone),
            PiecewiseToneCurveOptions.Default.MinTone);

    /// <summary>
    /// Identifies the <see cref="BaselineMaxTone"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> BaselineMaxToneProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(BaselineMaxTone),
            PiecewiseToneCurveOptions.Default.MaxTone);

    /// <summary>
    /// Identifies the <see cref="BaselineShadowKnee"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> BaselineShadowKneeProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(BaselineShadowKnee),
            PiecewiseToneCurveOptions.Default.ShadowKnee);

    /// <summary>
    /// Identifies the <see cref="BaselineHighlightKnee"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> BaselineHighlightKneeProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(BaselineHighlightKnee),
            PiecewiseToneCurveOptions.Default.HighlightKnee);

    /// <summary>
    /// Identifies the <see cref="Histogram"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> HistogramProperty =
        AvaloniaProperty.Register<ToneCurveEditor, IReadOnlyList<double>?>(nameof(Histogram), null);

    /// <summary>
    /// Identifies the <see cref="IsInteractive"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<ToneCurveEditor, bool>(
            nameof(IsInteractive),
            true,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="DragStartedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragStartedCommandProperty =
        AvaloniaProperty.Register<ToneCurveEditor, ICommand?>(nameof(DragStartedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragCompletedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragCompletedCommandProperty =
        AvaloniaProperty.Register<ToneCurveEditor, ICommand?>(nameof(DragCompletedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragStarted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragStartedEvent =
        RoutedEvent.Register<ToneCurveEditor, RoutedEventArgs>(nameof(DragStarted), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="DragCompleted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragCompletedEvent =
        RoutedEvent.Register<ToneCurveEditor, RoutedEventArgs>(nameof(DragCompleted), RoutingStrategies.Bubble);

    private ToneCurveHandle _activeHandle;
    private ToneCurveHandle _hoveredHandle;
    private ToneCurveHandle _selectedHandle;
    private bool _isDragging;

    static ToneCurveEditor()
    {
        AffectsRender<ToneCurveEditor>(
            MinToneProperty,
            MaxToneProperty,
            ShadowKneeProperty,
            HighlightKneeProperty,
            HistogramProperty,
            IsInteractiveProperty);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ToneCurveEditor"/>.
    /// </summary>
    public ToneCurveEditor()
    {
        ClipToBounds = true;
        MinHeight = 140;
        Focusable = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>
    /// Occurs when the user begins dragging a curve handle.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    /// <summary>
    /// Occurs when the user completes dragging a curve handle.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    /// <summary>
    /// Gets or sets the output minimum tone floor in <c>[0.0, MaxTone)</c>.
    /// </summary>
    public double MinTone
    {
        get => GetValue(MinToneProperty);
        set => SetValue(MinToneProperty, value);
    }

    /// <summary>
    /// Gets or sets the output maximum tone ceiling in <c>(MinTone, 1.0]</c>.
    /// </summary>
    public double MaxTone
    {
        get => GetValue(MaxToneProperty);
        set => SetValue(MaxToneProperty, value);
    }

    /// <summary>
    /// Gets or sets the shadow knee breakpoint in <c>(0.0, HighlightKnee)</c>.
    /// </summary>
    public double ShadowKnee
    {
        get => GetValue(ShadowKneeProperty);
        set => SetValue(ShadowKneeProperty, value);
    }

    /// <summary>
    /// Gets or sets the highlight knee breakpoint in <c>(ShadowKnee, 1.0)</c>.
    /// </summary>
    public double HighlightKnee
    {
        get => GetValue(HighlightKneeProperty);
        set => SetValue(HighlightKneeProperty, value);
    }

    /// <summary>
    /// Gets or sets the baseline minimum tone used by handle double-click reset and <see cref="ResetToBaseline"/>.
    /// </summary>
    public double BaselineMinTone
    {
        get => GetValue(BaselineMinToneProperty);
        set => SetValue(BaselineMinToneProperty, value);
    }

    /// <summary>
    /// Gets or sets the baseline maximum tone used by handle double-click reset and <see cref="ResetToBaseline"/>.
    /// </summary>
    public double BaselineMaxTone
    {
        get => GetValue(BaselineMaxToneProperty);
        set => SetValue(BaselineMaxToneProperty, value);
    }

    /// <summary>
    /// Gets or sets the baseline shadow knee used by handle double-click reset and <see cref="ResetToBaseline"/>.
    /// </summary>
    public double BaselineShadowKnee
    {
        get => GetValue(BaselineShadowKneeProperty);
        set => SetValue(BaselineShadowKneeProperty, value);
    }

    /// <summary>
    /// Gets or sets the baseline highlight knee used by handle double-click reset and <see cref="ResetToBaseline"/>.
    /// </summary>
    public double BaselineHighlightKnee
    {
        get => GetValue(BaselineHighlightKneeProperty);
        set => SetValue(BaselineHighlightKneeProperty, value);
    }

    /// <summary>
    /// Gets or sets the normalized <c>[0.0, 1.0]</c> luminance histogram bin heights rendered behind the curve.
    /// </summary>
    public IReadOnlyList<double>? Histogram
    {
        get => GetValue(HistogramProperty);
        set => SetValue(HistogramProperty, value);
    }

    /// <summary>
    /// Gets or sets whether pointer and keyboard interaction with the 4 handles is enabled.
    /// </summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when dragging starts.
    /// </summary>
    public ICommand? DragStartedCommand
    {
        get => GetValue(DragStartedCommandProperty);
        set => SetValue(DragStartedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when dragging completes.
    /// </summary>
    public ICommand? DragCompletedCommand
    {
        get => GetValue(DragCompletedCommandProperty);
        set => SetValue(DragCompletedCommandProperty, value);
    }

    /// <summary>
    /// Applies one of the built-in artistic <see cref="ToneCurvePreset"/> curves.
    /// </summary>
    public void ApplyPreset(ToneCurvePreset preset)
    {
        if (!IsInteractive)
        {
            return;
        }

        (double minTone, double maxTone, double shadowKnee, double highlightKnee) = preset switch
        {
            ToneCurvePreset.Linear => (0.0, 1.0, 0.25, 0.75),
            ToneCurvePreset.SoftWash => (0.08, 0.94, 0.20, 0.80),
            ToneCurvePreset.HighContrast => (0.02, 0.98, 0.28, 0.72),
            ToneCurvePreset.LiftedPaper => (0.12, 0.92, 0.18, 0.84),
            _ => (0.05, 0.96, 0.15, 0.85),
        };

        ApplyCurveValues(minTone, maxTone, shadowKnee, highlightKnee);
    }

    /// <summary>
    /// Applies a built-in preset by name (<c>Linear</c>, <c>Soft Wash</c>, <c>High Contrast</c>, <c>Lifted Paper</c>).
    /// </summary>
    /// <returns><see langword="true"/> if the preset name was recognized and applied; otherwise <see langword="false"/>.</returns>
    public bool ApplyPreset(string? presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
        {
            return false;
        }

        string normalized = presetName.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse(normalized, ignoreCase: true, out ToneCurvePreset preset))
        {
            ApplyPreset(preset);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets all four curve handles back to the configured baseline values.
    /// </summary>
    public void ResetToBaseline()
    {
        if (!IsInteractive)
        {
            return;
        }

        double min = double.IsFinite(BaselineMinTone) ? Math.Clamp(BaselineMinTone, 0.0, 1.0 - MinToneSeparation) : 0.05;
        double max = double.IsFinite(BaselineMaxTone) ? Math.Clamp(BaselineMaxTone, min + MinToneSeparation, 1.0) : 0.96;
        double shadow = double.IsFinite(BaselineShadowKnee) ? Math.Clamp(BaselineShadowKnee, KneeEdgeMargin, 1.0 - KneeEdgeMargin - MinKneeSeparation) : 0.15;
        double highlight = double.IsFinite(BaselineHighlightKnee) ? Math.Clamp(BaselineHighlightKnee, shadow + MinKneeSeparation, 1.0 - KneeEdgeMargin) : 0.85;

        ApplyCurveValues(min, max, shadow, highlight);
    }

    /// <summary>
    /// Returns the <see cref="Rect"/> representing the normalized <c>[0, 1] x [0, 1]</c> plot region inside current <see cref="Visual.Bounds"/>.
    /// </summary>
    public Rect GetPlotRect()
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        double leftPad = width >= 120 ? 28.0 : 12.0;
        double rightPad = width >= 120 ? 16.0 : 10.0;
        double topPad = height >= 100 ? 14.0 : 10.0;
        double bottomPad = height >= 100 ? 22.0 : 12.0;

        double plotWidth = Math.Max(10.0, width - leftPad - rightPad);
        double plotHeight = Math.Max(10.0, height - topPad - bottomPad);
        return new Rect(leftPad, topPad, plotWidth, plotHeight);
    }

    /// <summary>
    /// Maps normalized curve coordinates <c>(x, y)</c> in <c>[0, 1] x [0, 1]</c> to control-local screen coordinates.
    /// </summary>
    public Point CurveToScreen(double x, double y)
    {
        Rect plot = GetPlotRect();
        return new Point(
            plot.Left + Math.Clamp(x, 0.0, 1.0) * plot.Width,
            plot.Bottom - Math.Clamp(y, 0.0, 1.0) * plot.Height);
    }

    /// <summary>
    /// Maps control-local screen coordinates to normalized curve coordinates <c>(X, Y)</c> clamped to <c>[0, 1] x [0, 1]</c>.
    /// </summary>
    public (double X, double Y) ScreenToCurve(Point point)
    {
        Rect plot = GetPlotRect();
        double x = Math.Clamp((point.X - plot.Left) / plot.Width, 0.0, 1.0);
        double y = Math.Clamp((plot.Bottom - point.Y) / plot.Height, 0.0, 1.0);
        return (x, y);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 240.0 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 180.0 : Math.Min(availableSize.Height, 220.0);
        return new Size(Math.Max(120.0, w), Math.Max(140.0, h));
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect outer = new(Bounds.Size);
        if (outer.Width <= 4 || outer.Height <= 4)
        {
            return;
        }

        IBrush surfaceBrush = ResolveBrush("StudioCurveCanvasBgBrush", Color.Parse("#181C24"));
        IPen surfaceBorderPen = new Pen(ResolveBrush("StudioCurveCanvasBorderBrush", Color.Parse("#2D3545")), 1.0);
        context.DrawRectangle(surfaceBrush, surfaceBorderPen, new RoundedRect(outer.Deflate(0.5), 6.0));

        Rect plot = GetPlotRect();
        PiecewiseToneCurveOptions options = GetSanitizedCurveOptions();

        DrawRegionGuides(context, plot, options);
        DrawHistogram(context, plot);
        DrawGridAndAxes(context, plot);
        DrawToneCurve(context, plot, options);
        DrawHandles(context, plot, options);
        DrawActiveBadge(context, options);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!IsInteractive)
        {
            return;
        }

        PointerPoint point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        ToneCurveHandle hit = HitTestHandle(point.Position);
        if (hit == ToneCurveHandle.None)
        {
            return;
        }

        Focus();
        _selectedHandle = hit;
        _hoveredHandle = hit;

        if (e.ClickCount >= 2)
        {
            ResetHandleToBaseline(hit);
            UpdateCursor(hit);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        _activeHandle = hit;
        e.Pointer.Capture(this);
        BeginDragSession();
        ApplyHandlePosition(hit, point.Position);
        UpdateCursor(hit);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Point pos = e.GetPosition(this);
        if (_isDragging && _activeHandle != ToneCurveHandle.None)
        {
            ApplyHandlePosition(_activeHandle, pos);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (!IsInteractive)
        {
            return;
        }

        ToneCurveHandle hover = HitTestHandle(pos);
        if (hover != _hoveredHandle)
        {
            _hoveredHandle = hover;
            UpdateCursor(hover);
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDragging && e.InitialPressMouseButton == MouseButton.Left)
        {
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }

            FinishDrag();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_isDragging)
        {
            FinishDrag();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_isDragging && _hoveredHandle != ToneCurveHandle.None)
        {
            _hoveredHandle = ToneCurveHandle.None;
            Cursor = Cursor.Default;
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        if (IsInteractive && _selectedHandle == ToneCurveHandle.None)
        {
            _selectedHandle = ToneCurveHandle.MinTone;
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsInteractive)
        {
            return;
        }

        if (e.Key == Key.Tab)
        {
            bool reverse = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            _selectedHandle = GetNextHandle(_selectedHandle, reverse);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        int direction = e.Key switch
        {
            Key.Up or Key.Right => 1,
            Key.Down or Key.Left => -1,
            _ => 0,
        };

        if (direction == 0)
        {
            return;
        }

        if (_selectedHandle == ToneCurveHandle.None)
        {
            _selectedHandle = ToneCurveHandle.MinTone;
        }

        double step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? KeyboardFineNudgeStep : KeyboardNudgeStep;
        NudgeHandle(_selectedHandle, direction * step);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsInteractiveProperty && !IsInteractive)
        {
            _hoveredHandle = ToneCurveHandle.None;
            if (_isDragging)
            {
                FinishDrag();
            }
            else
            {
                Cursor = Cursor.Default;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_isDragging)
        {
            FinishDrag();
        }
    }

    private IBrush ResolveBrush(string key, Color fallback)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out object? resource) && resource is IBrush brush)
        {
            return brush;
        }

        if (Application.Current is not null &&
            Application.Current.TryFindResource(key, ActualThemeVariant, out object? appResource) &&
            appResource is IBrush appBrush)
        {
            return appBrush;
        }

        return new ImmutableSolidColorBrush(fallback);
    }

    private static ToneCurveHandle GetNextHandle(ToneCurveHandle current, bool reverse)
    {
        int index = Array.IndexOf(HandleTabOrder, current);
        if (index < 0)
        {
            return reverse ? HandleTabOrder[^1] : HandleTabOrder[0];
        }

        int nextIndex = reverse
            ? (index - 1 + HandleTabOrder.Length) % HandleTabOrder.Length
            : (index + 1) % HandleTabOrder.Length;
        return HandleTabOrder[nextIndex];
    }

    private void ApplyCurveValues(double minTone, double maxTone, double shadowKnee, double highlightKnee)
    {
        // Always update upper bound first when expanding upward (and lower bound first when shrinking)
        // so ViewModels with coupled setter checks never reject an intermediate state.
        if (maxTone > MaxTone)
        {
            SetCurrentValue(MaxToneProperty, Math.Round(maxTone, 4));
            SetCurrentValue(MinToneProperty, Math.Round(minTone, 4));
        }
        else
        {
            SetCurrentValue(MinToneProperty, Math.Round(minTone, 4));
            SetCurrentValue(MaxToneProperty, Math.Round(maxTone, 4));
        }

        if (highlightKnee > HighlightKnee)
        {
            SetCurrentValue(HighlightKneeProperty, Math.Round(highlightKnee, 4));
            SetCurrentValue(ShadowKneeProperty, Math.Round(shadowKnee, 4));
        }
        else
        {
            SetCurrentValue(ShadowKneeProperty, Math.Round(shadowKnee, 4));
            SetCurrentValue(HighlightKneeProperty, Math.Round(highlightKnee, 4));
        }

        InvalidateVisual();
    }

    private void ResetHandleToBaseline(ToneCurveHandle handle)
    {
        PiecewiseToneCurveOptions current = GetSanitizedCurveOptions();

        switch (handle)
        {
            case ToneCurveHandle.MinTone:
            {
                double target = double.IsFinite(BaselineMinTone) ? BaselineMinTone : 0.05;
                double clamped = Math.Clamp(target, 0.0, Math.Max(0.0, current.MaxTone - MinToneSeparation));
                SetCurrentValue(MinToneProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.MaxTone:
            {
                double target = double.IsFinite(BaselineMaxTone) ? BaselineMaxTone : 0.96;
                double clamped = Math.Clamp(target, Math.Min(1.0, current.MinTone + MinToneSeparation), 1.0);
                SetCurrentValue(MaxToneProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.ShadowKnee:
            {
                double target = double.IsFinite(BaselineShadowKnee) ? BaselineShadowKnee : 0.15;
                double clamped = Math.Clamp(target, KneeEdgeMargin, Math.Max(KneeEdgeMargin, current.HighlightKnee - MinKneeSeparation));
                SetCurrentValue(ShadowKneeProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.HighlightKnee:
            {
                double target = double.IsFinite(BaselineHighlightKnee) ? BaselineHighlightKnee : 0.85;
                double clamped = Math.Clamp(target, Math.Min(1.0 - KneeEdgeMargin, current.ShadowKnee + MinKneeSeparation), 1.0 - KneeEdgeMargin);
                SetCurrentValue(HighlightKneeProperty, Math.Round(clamped, 4));
                break;
            }
        }
    }

    private void NudgeHandle(ToneCurveHandle handle, double delta)
    {
        PiecewiseToneCurveOptions current = GetSanitizedCurveOptions();

        switch (handle)
        {
            case ToneCurveHandle.MinTone:
            {
                double maxAllowed = Math.Max(0.0, current.MaxTone - MinToneSeparation);
                double clamped = Math.Clamp(current.MinTone + delta, 0.0, maxAllowed);
                SetCurrentValue(MinToneProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.MaxTone:
            {
                double minAllowed = Math.Min(1.0, current.MinTone + MinToneSeparation);
                double clamped = Math.Clamp(current.MaxTone + delta, minAllowed, 1.0);
                SetCurrentValue(MaxToneProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.ShadowKnee:
            {
                double maxAllowed = Math.Max(KneeEdgeMargin, current.HighlightKnee - MinKneeSeparation);
                double clamped = Math.Clamp(current.ShadowKnee + delta, KneeEdgeMargin, maxAllowed);
                SetCurrentValue(ShadowKneeProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.HighlightKnee:
            {
                double minAllowed = Math.Min(1.0 - KneeEdgeMargin, current.ShadowKnee + MinKneeSeparation);
                double clamped = Math.Clamp(current.HighlightKnee + delta, minAllowed, 1.0 - KneeEdgeMargin);
                SetCurrentValue(HighlightKneeProperty, Math.Round(clamped, 4));
                break;
            }
        }
    }

    private void FinishDrag()
    {
        _activeHandle = ToneCurveHandle.None;
        EndDragSession();
        UpdateCursor(_hoveredHandle);
        InvalidateVisual();
    }

    private void BeginDragSession()
    {
        if (_isDragging)
        {
            return;
        }

        _isDragging = true;
        RaiseEvent(new RoutedEventArgs(DragStartedEvent, this));
        ICommand? command = DragStartedCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void EndDragSession()
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        RaiseEvent(new RoutedEventArgs(DragCompletedEvent, this));
        ICommand? command = DragCompletedCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void UpdateCursor(ToneCurveHandle handle)
    {
        Cursor = handle switch
        {
            ToneCurveHandle.MinTone or ToneCurveHandle.MaxTone => VerticalCursor,
            ToneCurveHandle.ShadowKnee or ToneCurveHandle.HighlightKnee => HorizontalCursor,
            _ => Cursor.Default,
        };
    }

    private PiecewiseToneCurveOptions GetSanitizedCurveOptions()
    {
        const double renderMinGap = 0.001;
        double rawMin = double.IsFinite(MinTone) ? MinTone : 0.05;
        double rawMax = double.IsFinite(MaxTone) ? MaxTone : 0.96;
        double rawShadow = double.IsFinite(ShadowKnee) ? ShadowKnee : 0.15;
        double rawHighlight = double.IsFinite(HighlightKnee) ? HighlightKnee : 0.85;

        double minTone = Math.Clamp(rawMin, 0.0, 1.0 - renderMinGap);
        double maxTone = Math.Clamp(rawMax, minTone + renderMinGap, 1.0);

        double shadowKnee = Math.Clamp(rawShadow, renderMinGap, 1.0 - 2.0 * renderMinGap);
        double highlightKnee = Math.Clamp(rawHighlight, shadowKnee + renderMinGap, 1.0 - renderMinGap);

        return new PiecewiseToneCurveOptions(minTone, maxTone, shadowKnee, highlightKnee);
    }

    private (ToneCurveHandle Handle, Point Position)[] GetHandlePositions(PiecewiseToneCurveOptions options) =>
    [
        (ToneCurveHandle.MinTone, CurveToScreen(0.0, options.MinTone)),
        (ToneCurveHandle.MaxTone, CurveToScreen(1.0, options.MaxTone)),
        (ToneCurveHandle.ShadowKnee, CurveToScreen(options.ShadowKnee, PiecewiseToneCurve.Evaluate(options.ShadowKnee, options))),
        (ToneCurveHandle.HighlightKnee, CurveToScreen(options.HighlightKnee, PiecewiseToneCurve.Evaluate(options.HighlightKnee, options))),
    ];

    private ToneCurveHandle HitTestHandle(Point pointer)
    {
        PiecewiseToneCurveOptions options = GetSanitizedCurveOptions();
        (ToneCurveHandle Handle, Point Position)[] handles = GetHandlePositions(options);

        ToneCurveHandle best = ToneCurveHandle.None;
        double bestDistSq = HandleHitRadius * HandleHitRadius;

        foreach ((ToneCurveHandle handle, Point pos) in handles)
        {
            double dx = pointer.X - pos.X;
            double dy = pointer.Y - pos.Y;
            double distSq = (dx * dx) + (dy * dy);
            if (distSq <= bestDistSq)
            {
                bestDistSq = distSq;
                best = handle;
            }
        }

        return best;
    }

    private void ApplyHandlePosition(ToneCurveHandle handle, Point pointer)
    {
        (double nx, double ny) = ScreenToCurve(pointer);
        PiecewiseToneCurveOptions current = GetSanitizedCurveOptions();

        switch (handle)
        {
            case ToneCurveHandle.MinTone:
            {
                double maxAllowed = Math.Max(0.0, current.MaxTone - MinToneSeparation);
                double clamped = Math.Clamp(ny, 0.0, maxAllowed);
                SetCurrentValue(MinToneProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.MaxTone:
            {
                double minAllowed = Math.Min(1.0, current.MinTone + MinToneSeparation);
                double clamped = Math.Clamp(ny, minAllowed, 1.0);
                SetCurrentValue(MaxToneProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.ShadowKnee:
            {
                double maxAllowed = Math.Max(KneeEdgeMargin, current.HighlightKnee - MinKneeSeparation);
                double clamped = Math.Clamp(nx, KneeEdgeMargin, maxAllowed);
                SetCurrentValue(ShadowKneeProperty, Math.Round(clamped, 4));
                break;
            }
            case ToneCurveHandle.HighlightKnee:
            {
                double minAllowed = Math.Min(1.0 - KneeEdgeMargin, current.ShadowKnee + MinKneeSeparation);
                double clamped = Math.Clamp(nx, minAllowed, 1.0 - KneeEdgeMargin);
                SetCurrentValue(HighlightKneeProperty, Math.Round(clamped, 4));
                break;
            }
        }
    }

    private void DrawRegionGuides(DrawingContext context, Rect plot, PiecewiseToneCurveOptions options)
    {
        double shadowX = plot.Left + (options.ShadowKnee * plot.Width);
        double highlightX = plot.Left + (options.HighlightKnee * plot.Width);

        IBrush shadowTint = ResolveBrush("StudioCurveShadowTintBrush", Color.FromArgb(24, 99, 102, 241));
        IBrush highlightTint = ResolveBrush("StudioCurveHighlightTintBrush", Color.FromArgb(24, 245, 158, 11));
        IPen regionPen = new Pen(
            ResolveBrush("StudioCurveZoneBrush", Color.Parse("#64748B")),
            1.0,
            RegionDashStyle);

        if (shadowX > plot.Left)
        {
            context.FillRectangle(shadowTint, new Rect(plot.Left, plot.Top, shadowX - plot.Left, plot.Height));
        }

        if (plot.Right > highlightX)
        {
            context.FillRectangle(highlightTint, new Rect(highlightX, plot.Top, plot.Right - highlightX, plot.Height));
        }

        context.DrawLine(regionPen, new Point(shadowX, plot.Top), new Point(shadowX, plot.Bottom));
        context.DrawLine(regionPen, new Point(highlightX, plot.Top), new Point(highlightX, plot.Bottom));
    }

    private void DrawHistogram(DrawingContext context, Rect plot)
    {
        IReadOnlyList<double>? bins = Histogram;
        if (bins is null || bins.Count < 2)
        {
            return;
        }

        double maxHeight = plot.Height * 0.85;
        var fillGeo = new StreamGeometry();
        var strokeGeo = new StreamGeometry();

        using (StreamGeometryContext fillCtx = fillGeo.Open())
        using (StreamGeometryContext strokeCtx = strokeGeo.Open())
        {
            fillCtx.BeginFigure(new Point(plot.Left, plot.Bottom), isFilled: true);

            for (int i = 0; i < bins.Count; i++)
            {
                double t = i / (double)(bins.Count - 1);
                double raw = bins[i];
                double norm = double.IsFinite(raw) ? Math.Clamp(raw, 0.0, 1.0) : 0.0;
                double x = plot.Left + (t * plot.Width);
                double y = plot.Bottom - (norm * maxHeight);
                Point pt = new(x, y);

                fillCtx.LineTo(pt);
                if (i == 0)
                {
                    strokeCtx.BeginFigure(pt, isFilled: false);
                }
                else
                {
                    strokeCtx.LineTo(pt);
                }
            }

            fillCtx.LineTo(new Point(plot.Right, plot.Bottom));
            fillCtx.EndFigure(isClosed: true);
            strokeCtx.EndFigure(isClosed: false);
        }

        IBrush fillBrush = ResolveBrush("StudioCurveHistogramBrush", Color.FromArgb(48, 148, 163, 184));
        IPen strokePen = new Pen(ResolveBrush("StudioCurveHistogramStrokeBrush", Color.FromArgb(85, 148, 163, 184)), 1.0);
        context.DrawGeometry(fillBrush, null, fillGeo);
        context.DrawGeometry(null, strokePen, strokeGeo);
    }

    private void DrawGridAndAxes(DrawingContext context, Rect plot)
    {
        IPen gridMinorPen = new Pen(ResolveBrush("StudioCurveGridBrush", Color.Parse("#252C3A")), 1.0);
        IPen gridMajorPen = new Pen(ResolveBrush("StudioCurveGridMajorBrush", Color.Parse("#333C4F")), 1.0);
        IPen identityPen = new Pen(
            ResolveBrush("StudioCurveIdentityBrush", Color.Parse("#4B5568")),
            1.0,
            IdentityDashStyle);
        IBrush axisBrush = ResolveBrush("StudioCurveAxisBrush", Color.Parse("#94A3B8"));

        for (int i = 0; i <= 4; i++)
        {
            double t = i * 0.25;
            IPen pen = (i == 0 || i == 2 || i == 4) ? gridMajorPen : gridMinorPen;

            double x = plot.Left + (t * plot.Width);
            double y = plot.Bottom - (t * plot.Height);

            context.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            context.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        // Diagonal identity reference line (0,0) -> (1,1)
        context.DrawLine(identityPen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Top));

        if (Bounds.Width >= 120 && Bounds.Height >= 100)
        {
            Typeface typeface = Typeface.Default;
            DrawAxisLabel(context, "0", new Point(plot.Left - 14, plot.Bottom - 6), typeface, axisBrush);
            DrawAxisLabel(context, ".5", new Point(plot.Left - 18, plot.Top + (plot.Height * 0.5) - 6), typeface, axisBrush);
            DrawAxisLabel(context, "1", new Point(plot.Left - 14, plot.Top - 5), typeface, axisBrush);

            DrawAxisLabel(context, "0", new Point(plot.Left - 3, plot.Bottom + 4), typeface, axisBrush);
            DrawAxisLabel(context, "0.5", new Point(plot.Left + (plot.Width * 0.5) - 8, plot.Bottom + 4), typeface, axisBrush);
            DrawAxisLabel(context, "1", new Point(plot.Right - 4, plot.Bottom + 4), typeface, axisBrush);
        }
    }

    private static void DrawAxisLabel(DrawingContext context, string text, Point origin, Typeface typeface, IBrush axisBrush)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            9.5,
            axisBrush);
        context.DrawText(formatted, origin);
    }

    private void DrawToneCurve(DrawingContext context, Rect plot, PiecewiseToneCurveOptions options)
    {
        const int sampleCount = 128;
        var xs = new List<double>(sampleCount + 3);
        for (int i = 0; i <= sampleCount; i++)
        {
            xs.Add(i / (double)sampleCount);
        }

        xs.Add(options.ShadowKnee);
        xs.Add(options.HighlightKnee);
        xs.Sort();

        var points = new List<Point>(xs.Count);
        double lastX = -1.0;
        for (int i = 0; i < xs.Count; i++)
        {
            double x = xs[i];
            if (Math.Abs(x - lastX) < 1e-7)
            {
                continue;
            }

            lastX = x;
            double y = PiecewiseToneCurve.Evaluate(x, options);
            points.Add(CurveToScreen(x, y));
        }

        if (points.Count < 2)
        {
            return;
        }

        var fillGeo = new StreamGeometry();
        using (StreamGeometryContext gc = fillGeo.Open())
        {
            gc.BeginFigure(new Point(plot.Left, plot.Bottom), isFilled: true);
            for (int i = 0; i < points.Count; i++)
            {
                gc.LineTo(points[i]);
            }

            gc.LineTo(new Point(plot.Right, plot.Bottom));
            gc.EndFigure(isClosed: true);
        }

        IBrush areaBrush = ResolveBrush("StudioCurveAreaBrush", Color.FromArgb(36, 56, 189, 248));
        context.DrawGeometry(areaBrush, null, fillGeo);

        var strokeGeo = new StreamGeometry();
        using (StreamGeometryContext gc = strokeGeo.Open())
        {
            gc.BeginFigure(points[0], isFilled: false);
            for (int i = 1; i < points.Count; i++)
            {
                gc.LineTo(points[i]);
            }

            gc.EndFigure(isClosed: false);
        }

        IBrush strokeBrush = IsInteractive
            ? ResolveBrush("StudioCurveLineBrush", Color.Parse("#38BDF8"))
            : ResolveBrush("StudioCurveMutedLineBrush", Color.Parse("#64748B"));
        IPen strokePen = new Pen(strokeBrush, IsInteractive ? 2.2 : 2.0, lineCap: PenLineCap.Round);
        context.DrawGeometry(null, strokePen, strokeGeo);
    }

    private void DrawHandles(DrawingContext context, Rect plot, PiecewiseToneCurveOptions options)
    {
        (ToneCurveHandle Handle, Point Position)[] handles = GetHandlePositions(options);

        IBrush handleFillBrush = ResolveBrush("StudioCurveHandleFillBrush", Color.Parse("#F8FAFC"));
        IBrush endpointBrush = ResolveBrush("StudioCurveHandleStrokeBrush", Color.Parse("#38BDF8"));
        IBrush kneeBrush = ResolveBrush("StudioCurveKneeStrokeBrush", Color.Parse("#F59E0B"));
        IBrush endpointHalo = ResolveBrush("StudioCurveHandleHaloBrush", Color.FromArgb(68, 56, 189, 248));
        IBrush kneeHalo = ResolveBrush("StudioCurveKneeHaloBrush", Color.FromArgb(68, 245, 158, 11));

        foreach ((ToneCurveHandle handle, Point pos) in handles)
        {
            bool isActive = handle == _activeHandle;
            bool isHovered = handle == _hoveredHandle;
            bool isKeyboardSelected = IsFocused && handle == _selectedHandle;
            bool isKnee = handle is ToneCurveHandle.ShadowKnee or ToneCurveHandle.HighlightKnee;

            IBrush ringBrush = isKnee ? kneeBrush : endpointBrush;
            IPen ringPen = new Pen(ringBrush, (isActive || isKeyboardSelected) ? 2.5 : 2.0);
            IBrush haloBrush = isKnee ? kneeHalo : endpointHalo;

            double radius = (isActive || isKeyboardSelected)
                ? HandleRadiusActive
                : isHovered ? HandleRadiusHover : HandleRadiusNormal;

            if ((isActive || isHovered || isKeyboardSelected) && IsInteractive)
            {
                context.DrawEllipse(haloBrush, null, pos, radius + 4.5, radius + 4.5);
            }

            context.DrawEllipse(handleFillBrush, ringPen, pos, radius, radius);
        }
    }

    private void DrawActiveBadge(DrawingContext context, PiecewiseToneCurveOptions options)
    {
        if (!IsInteractive)
        {
            return;
        }

        ToneCurveHandle displayHandle = _isDragging && _activeHandle != ToneCurveHandle.None
            ? _activeHandle
            : _hoveredHandle != ToneCurveHandle.None
                ? _hoveredHandle
                : IsFocused && _selectedHandle != ToneCurveHandle.None
                    ? _selectedHandle
                    : ToneCurveHandle.None;

        if (displayHandle == ToneCurveHandle.None)
        {
            return;
        }

        (string label, Point anchor) = displayHandle switch
        {
            ToneCurveHandle.MinTone => (
                $"Min (0.00, {options.MinTone:F2})",
                CurveToScreen(0.0, options.MinTone)),
            ToneCurveHandle.MaxTone => (
                $"Max (1.00, {options.MaxTone:F2})",
                CurveToScreen(1.0, options.MaxTone)),
            ToneCurveHandle.ShadowKnee => (
                $"Shadow ({options.ShadowKnee:F2}, {PiecewiseToneCurve.Evaluate(options.ShadowKnee, options):F2})",
                CurveToScreen(options.ShadowKnee, PiecewiseToneCurve.Evaluate(options.ShadowKnee, options))),
            ToneCurveHandle.HighlightKnee => (
                $"Highlight ({options.HighlightKnee:F2}, {PiecewiseToneCurve.Evaluate(options.HighlightKnee, options):F2})",
                CurveToScreen(options.HighlightKnee, PiecewiseToneCurve.Evaluate(options.HighlightKnee, options))),
            _ => (string.Empty, default),
        };

        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        IBrush badgeBg = ResolveBrush("StudioCurveBadgeBgBrush", Color.FromArgb(230, 15, 23, 42));
        IPen badgeBorder = new Pen(ResolveBrush("StudioCurveBadgeBorderBrush", Color.Parse("#475569")), 1.0);
        IBrush badgeFg = ResolveBrush("StudioCurveBadgeFgBrush", Color.Parse("#F1F5F9"));

        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            10.5,
            badgeFg);

        double padX = 6.0;
        double padY = 3.0;
        double boxWidth = text.Width + (padX * 2.0);
        double boxHeight = text.Height + (padY * 2.0);

        double boxX = anchor.X + 10.0;
        if (boxX + boxWidth > Bounds.Width - 4.0)
        {
            boxX = anchor.X - boxWidth - 10.0;
        }

        boxX = Math.Clamp(boxX, 4.0, Math.Max(4.0, Bounds.Width - boxWidth - 4.0));

        double boxY = anchor.Y - boxHeight - 8.0;
        if (boxY < 4.0)
        {
            boxY = anchor.Y + 12.0;
        }

        boxY = Math.Clamp(boxY, 4.0, Math.Max(4.0, Bounds.Height - boxHeight - 4.0));

        var badgeRect = new Rect(boxX, boxY, boxWidth, boxHeight);
        context.DrawRectangle(badgeBg, badgeBorder, new RoundedRect(badgeRect, 4.0));
        context.DrawText(text, new Point(boxX + padX, boxY + padY));
    }
}
