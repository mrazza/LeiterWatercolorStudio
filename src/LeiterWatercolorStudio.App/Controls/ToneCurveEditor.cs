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
/// Interactive studio editor for <see cref="PiecewiseToneCurveOptions"/> rendering the exact
/// piecewise Hermite tone curve, shadow/midtone/highlight region guides, and 4 draggable control handles.
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

    private static readonly IBrush SurfaceBrush = new ImmutableSolidColorBrush(Color.Parse("#181C24"));
    private static readonly IPen SurfaceBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#2D3545")), 1.0);
    private static readonly IPen GridMinorPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#252C3A")), 1.0);
    private static readonly IPen GridMajorPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#333C4F")), 1.0);
    private static readonly IPen IdentityLinePen = new ImmutablePen(
        new ImmutableSolidColorBrush(Color.Parse("#4B5568")),
        1.0,
        new ImmutableDashStyle([4.0, 4.0], 0.0));
    private static readonly IPen RegionGuidePen = new ImmutablePen(
        new ImmutableSolidColorBrush(Color.Parse("#64748B")),
        1.0,
        new ImmutableDashStyle([3.0, 3.0], 0.0));
    private static readonly IBrush ShadowTintBrush = new ImmutableSolidColorBrush(Color.FromArgb(24, 99, 102, 241));
    private static readonly IBrush HighlightTintBrush = new ImmutableSolidColorBrush(Color.FromArgb(24, 245, 158, 11));
    private static readonly IBrush CurveAreaBrush = new ImmutableSolidColorBrush(Color.FromArgb(36, 56, 189, 248));
    private static readonly IPen CurveStrokePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#38BDF8")), 2.2, lineCap: PenLineCap.Round);
    private static readonly IPen CurveMutedPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#64748B")), 2.0, lineCap: PenLineCap.Round);

    private static readonly IBrush HandleFillBrush = new ImmutableSolidColorBrush(Color.Parse("#F8FAFC"));
    private static readonly IImmutableBrush EndpointAccentBrush = new ImmutableSolidColorBrush(Color.Parse("#38BDF8"));
    private static readonly IImmutableBrush KneeAccentBrush = new ImmutableSolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IPen EndpointRingPen = new ImmutablePen(EndpointAccentBrush, 2.0);
    private static readonly IPen EndpointActiveRingPen = new ImmutablePen(EndpointAccentBrush, 2.5);
    private static readonly IPen KneeRingPen = new ImmutablePen(KneeAccentBrush, 2.0);
    private static readonly IPen KneeActiveRingPen = new ImmutablePen(KneeAccentBrush, 2.5);
    private static readonly IBrush HandleHaloBrush = new ImmutableSolidColorBrush(Color.FromArgb(68, 56, 189, 248));
    private static readonly IBrush KneeHaloBrush = new ImmutableSolidColorBrush(Color.FromArgb(68, 245, 158, 11));
    private static readonly IBrush AxisLabelBrush = new ImmutableSolidColorBrush(Color.Parse("#94A3B8"));
    private static readonly IBrush BadgeBgBrush = new ImmutableSolidColorBrush(Color.FromArgb(230, 15, 23, 42));
    private static readonly IPen BadgeBorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.Parse("#475569")), 1.0);
    private static readonly IBrush BadgeTextBrush = new ImmutableSolidColorBrush(Color.Parse("#F1F5F9"));

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor VerticalCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor HorizontalCursor = new(StandardCursorType.SizeWestEast);

    /// <summary>
    /// Identifies the <see cref="MinTone"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> MinToneProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(MinTone),
            0.05,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="MaxTone"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> MaxToneProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(MaxTone),
            0.96,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="ShadowKnee"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> ShadowKneeProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(ShadowKnee),
            0.15,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="HighlightKnee"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> HighlightKneeProperty =
        AvaloniaProperty.Register<ToneCurveEditor, double>(
            nameof(HighlightKnee),
            0.85,
            defaultBindingMode: BindingMode.TwoWay);

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
    private bool _isDragging;

    static ToneCurveEditor()
    {
        AffectsRender<ToneCurveEditor>(
            MinToneProperty,
            MaxToneProperty,
            ShadowKneeProperty,
            HighlightKneeProperty,
            IsInteractiveProperty);
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ToneCurveEditor"/>.
    /// </summary>
    public ToneCurveEditor()
    {
        ClipToBounds = true;
        MinHeight = 140;
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
    /// Gets or sets whether pointer interaction with the 4 handles is enabled.
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

        context.DrawRectangle(SurfaceBrush, SurfaceBorderPen, new RoundedRect(outer.Deflate(0.5), 6.0));

        Rect plot = GetPlotRect();
        PiecewiseToneCurveOptions options = GetSanitizedCurveOptions();

        DrawRegionGuides(context, plot, options);
        DrawGridAndAxes(context, plot);
        DrawToneCurve(context, plot, options);
        DrawHandles(context, plot, options);
        DrawActiveBadge(context, plot, options);
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

        _activeHandle = hit;
        _hoveredHandle = hit;
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

        if (shadowX > plot.Left)
        {
            context.FillRectangle(ShadowTintBrush, new Rect(plot.Left, plot.Top, shadowX - plot.Left, plot.Height));
        }

        if (plot.Right > highlightX)
        {
            context.FillRectangle(HighlightTintBrush, new Rect(highlightX, plot.Top, plot.Right - highlightX, plot.Height));
        }

        context.DrawLine(RegionGuidePen, new Point(shadowX, plot.Top), new Point(shadowX, plot.Bottom));
        context.DrawLine(RegionGuidePen, new Point(highlightX, plot.Top), new Point(highlightX, plot.Bottom));
    }

    private void DrawGridAndAxes(DrawingContext context, Rect plot)
    {
        for (int i = 0; i <= 4; i++)
        {
            double t = i * 0.25;
            IPen pen = (i == 0 || i == 2 || i == 4) ? GridMajorPen : GridMinorPen;

            double x = plot.Left + (t * plot.Width);
            double y = plot.Bottom - (t * plot.Height);

            context.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            context.DrawLine(pen, new Point(plot.Left, y), new Point(plot.Right, y));
        }

        // Diagonal identity reference line (0,0) -> (1,1)
        context.DrawLine(IdentityLinePen, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Top));

        if (Bounds.Width >= 120 && Bounds.Height >= 100)
        {
            Typeface typeface = Typeface.Default;
            DrawAxisLabel(context, "0", new Point(plot.Left - 14, plot.Bottom - 6), typeface);
            DrawAxisLabel(context, ".5", new Point(plot.Left - 18, plot.Top + (plot.Height * 0.5) - 6), typeface);
            DrawAxisLabel(context, "1", new Point(plot.Left - 14, plot.Top - 5), typeface);

            DrawAxisLabel(context, "0", new Point(plot.Left - 3, plot.Bottom + 4), typeface);
            DrawAxisLabel(context, "0.5", new Point(plot.Left + (plot.Width * 0.5) - 8, plot.Bottom + 4), typeface);
            DrawAxisLabel(context, "1", new Point(plot.Right - 4, plot.Bottom + 4), typeface);
        }
    }

    private static void DrawAxisLabel(DrawingContext context, string text, Point origin, Typeface typeface)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            9.5,
            AxisLabelBrush);
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

        context.DrawGeometry(CurveAreaBrush, null, fillGeo);

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

        context.DrawGeometry(null, IsInteractive ? CurveStrokePen : CurveMutedPen, strokeGeo);
    }

    private void DrawHandles(DrawingContext context, Rect plot, PiecewiseToneCurveOptions options)
    {
        (ToneCurveHandle Handle, Point Position)[] handles = GetHandlePositions(options);

        foreach ((ToneCurveHandle handle, Point pos) in handles)
        {
            bool isActive = handle == _activeHandle;
            bool isHovered = handle == _hoveredHandle;
            bool isKnee = handle is ToneCurveHandle.ShadowKnee or ToneCurveHandle.HighlightKnee;

            IPen ringPen = isKnee
                ? (isActive ? KneeActiveRingPen : KneeRingPen)
                : (isActive ? EndpointActiveRingPen : EndpointRingPen);
            IBrush haloBrush = isKnee ? KneeHaloBrush : HandleHaloBrush;

            double radius = isActive
                ? HandleRadiusActive
                : isHovered ? HandleRadiusHover : HandleRadiusNormal;

            if ((isActive || isHovered) && IsInteractive)
            {
                context.DrawEllipse(haloBrush, null, pos, radius + 4.5, radius + 4.5);
            }

            context.DrawEllipse(HandleFillBrush, ringPen, pos, radius, radius);
        }
    }

    private void DrawActiveBadge(DrawingContext context, Rect plot, PiecewiseToneCurveOptions options)
    {
        if (!_isDragging || _activeHandle == ToneCurveHandle.None)
        {
            return;
        }

        (string label, Point anchor) = _activeHandle switch
        {
            ToneCurveHandle.MinTone => (
                $"Min {options.MinTone:F2}",
                CurveToScreen(0.0, options.MinTone)),
            ToneCurveHandle.MaxTone => (
                $"Max {options.MaxTone:F2}",
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

        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            10.5,
            BadgeTextBrush);

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
        context.DrawRectangle(BadgeBgBrush, BadgeBorderPen, new RoundedRect(badgeRect, 4.0));
        context.DrawText(text, new Point(boxX + padX, boxY + padY));
    }
}
