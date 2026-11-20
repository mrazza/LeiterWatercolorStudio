using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// A compact studio slider combining a label, modified indicator dot, synchronized numeric entry,
/// unit suffix, baseline reset action, and coalesced drag start/completed notifications.
/// </summary>
public partial class NumericSlider : UserControl
{
    private const double ModifiedEpsilon = 1e-6;

    /// <summary>
    /// Identifies the <see cref="Label"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<NumericSlider, string>(nameof(Label), string.Empty);

    /// <summary>
    /// Identifies the <see cref="Description"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<NumericSlider, string?>(nameof(Description), null);

    /// <summary>
    /// Identifies the <see cref="InfoText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> InfoTextProperty =
        AvaloniaProperty.Register<NumericSlider, string?>(nameof(InfoText), null);

    /// <summary>
    /// Identifies the <see cref="Unit"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> UnitProperty =
        AvaloniaProperty.Register<NumericSlider, string?>(nameof(Unit), null);

    /// <summary>
    /// Identifies the <see cref="Value"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumericSlider, double>(
            nameof(Value),
            0.0,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="Minimum"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<NumericSlider, double>(nameof(Minimum), 0.0);

    /// <summary>
    /// Identifies the <see cref="Maximum"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<NumericSlider, double>(nameof(Maximum), 1.0);

    /// <summary>
    /// Identifies the <see cref="Step"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<NumericSlider, double>(nameof(Step), 0.01);

    /// <summary>
    /// Identifies the <see cref="BaselineValue"/> styled property.
    /// </summary>
    public static readonly StyledProperty<double> BaselineValueProperty =
        AvaloniaProperty.Register<NumericSlider, double>(nameof(BaselineValue), 0.0);

    /// <summary>
    /// Identifies the <see cref="HasBaseline"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> HasBaselineProperty =
        AvaloniaProperty.Register<NumericSlider, bool>(nameof(HasBaseline), true);

    /// <summary>
    /// Identifies the <see cref="IsInteger"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> IsIntegerProperty =
        AvaloniaProperty.Register<NumericSlider, bool>(nameof(IsInteger), false);

    /// <summary>
    /// Identifies the <see cref="FormatString"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<NumericSlider, string>(nameof(FormatString), "F2");

    /// <summary>
    /// Identifies the <see cref="IsReadOnly"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<NumericSlider, bool>(nameof(IsReadOnly), false);

    /// <summary>
    /// Identifies the <see cref="DragStartedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragStartedCommandProperty =
        AvaloniaProperty.Register<NumericSlider, ICommand?>(nameof(DragStartedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragCompletedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragCompletedCommandProperty =
        AvaloniaProperty.Register<NumericSlider, ICommand?>(nameof(DragCompletedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragStarted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragStartedEvent =
        RoutedEvent.Register<NumericSlider, RoutedEventArgs>(nameof(DragStarted), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="DragCompleted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragCompletedEvent =
        RoutedEvent.Register<NumericSlider, RoutedEventArgs>(nameof(DragCompleted), RoutingStrategies.Bubble);

    private bool _isUpdatingControls;
    private bool _isDragging;

    /// <summary>
    /// Initializes a new instance of <see cref="NumericSlider"/>.
    /// </summary>
    public NumericSlider()
    {
        InitializeComponent();

        PART_Slider.PropertyChanged += OnSliderPropertyChanged;
        PART_Slider.AddHandler(PointerPressedEvent, OnSliderPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        PART_Slider.AddHandler(PointerReleasedEvent, OnSliderPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        PART_Slider.AddHandler(PointerCaptureLostEvent, OnSliderPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        PART_Slider.AddHandler(Thumb.DragStartedEvent, OnThumbDragStarted, RoutingStrategies.Bubble, handledEventsToo: true);
        PART_Slider.AddHandler(Thumb.DragCompletedEvent, OnThumbDragCompleted, RoutingStrategies.Bubble, handledEventsToo: true);
        PART_Slider.AddHandler(DoubleTappedEvent, OnResetGesture, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

        PART_LabelArea.DoubleTapped += OnResetGesture;
        PART_ResetButton.Click += OnResetButtonClick;

        PART_ValueTextBox.GotFocus += OnValueTextBoxGotFocus;
        PART_ValueTextBox.LostFocus += OnValueTextBoxLostFocus;
        PART_ValueTextBox.KeyDown += OnValueTextBoxKeyDown;

        SyncAllControls();
    }

    /// <summary>
    /// Occurs when an interactive slider drag begins.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    /// <summary>
    /// Occurs when an interactive slider drag ends.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    /// <summary>
    /// Gets or sets the parameter label displayed above the slider.
    /// </summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional tooltip description for the parameter.
    /// </summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    /// Gets or sets a layman-digestible explanation displayed on the adjacent info icon tooltip.
    /// </summary>
    public string? InfoText
    {
        get => GetValue(InfoTextProperty);
        set => SetValue(InfoTextProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional unit suffix displayed after the numeric entry (e.g., <c>px</c>, <c>°</c>, <c>%</c>).
    /// </summary>
    public string? Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>
    /// Gets or sets the current numeric value.
    /// </summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the lower bound of the slider range.
    /// </summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the upper bound of the slider range.
    /// </summary>
    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets the small step increment used by keyboard nudges and slider changes.
    /// </summary>
    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>
    /// Gets or sets the baseline value used to show the modified indicator dot and perform resets.
    /// </summary>
    public double BaselineValue
    {
        get => GetValue(BaselineValueProperty);
        set => SetValue(BaselineValueProperty, value);
    }

    /// <summary>
    /// Gets or sets whether baseline comparison and reset functionality are enabled.
    /// </summary>
    public bool HasBaseline
    {
        get => GetValue(HasBaselineProperty);
        set => SetValue(HasBaselineProperty, value);
    }

    /// <summary>
    /// Gets or sets whether values are snapped and formatted as integers.
    /// </summary>
    public bool IsInteger
    {
        get => GetValue(IsIntegerProperty);
        set => SetValue(IsIntegerProperty, value);
    }

    /// <summary>
    /// Gets or sets the .NET numeric format string applied to the text entry.
    /// </summary>
    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    /// <summary>
    /// Gets or sets whether user interaction is disabled.
    /// </summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when a slider drag starts.
    /// </summary>
    public ICommand? DragStartedCommand
    {
        get => GetValue(DragStartedCommandProperty);
        set => SetValue(DragStartedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when a slider drag completes.
    /// </summary>
    public ICommand? DragCompletedCommand
    {
        get => GetValue(DragCompletedCommandProperty);
        set => SetValue(DragCompletedCommandProperty, value);
    }

    /// <summary>
    /// Gets whether the current <see cref="Value"/> differs from <see cref="BaselineValue"/> when <see cref="HasBaseline"/> is true.
    /// </summary>
    public bool IsModified =>
        HasBaseline &&
        double.IsFinite(Value) &&
        double.IsFinite(BaselineValue) &&
        Math.Abs(Value - BaselineValue) > ModifiedEpsilon;

    /// <summary>
    /// Resets <see cref="Value"/> back to <see cref="BaselineValue"/> when <see cref="HasBaseline"/> is true and <see cref="IsReadOnly"/> is false.
    /// </summary>
    public void ResetToBaseline()
    {
        if (IsReadOnly || !HasBaseline || !double.IsFinite(BaselineValue))
        {
            return;
        }

        double target = IsInteger ? Math.Round(BaselineValue, MidpointRounding.AwayFromZero) : BaselineValue;
        SetCurrentValue(ValueProperty, target);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LabelProperty ||
            change.Property == DescriptionProperty ||
            change.Property == InfoTextProperty ||
            change.Property == UnitProperty ||
            change.Property == ValueProperty ||
            change.Property == MinimumProperty ||
            change.Property == MaximumProperty ||
            change.Property == StepProperty ||
            change.Property == BaselineValueProperty ||
            change.Property == HasBaselineProperty ||
            change.Property == IsIntegerProperty ||
            change.Property == FormatStringProperty ||
            change.Property == IsReadOnlyProperty)
        {
            SyncAllControls();
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EndDragSession();
    }

    private void SyncAllControls()
    {
        if (PART_LabelText is null)
        {
            return;
        }

        PART_LabelText.Text = Label ?? string.Empty;

        string? tip = string.IsNullOrWhiteSpace(Description) ? null : Description;
        ToolTip.SetTip(PART_LabelArea, tip);

        bool hasInfo = !string.IsNullOrWhiteSpace(InfoText);
        PART_InfoIcon.Header = Label;
        PART_InfoIcon.Text = hasInfo ? InfoText : null;
        PART_InfoIcon.IsVisible = hasInfo;

        string? unit = Unit;
        bool hasUnit = !string.IsNullOrWhiteSpace(unit);
        PART_UnitText.Text = unit ?? string.Empty;
        PART_UnitText.IsVisible = hasUnit;

        bool modified = IsModified;
        PART_ModifiedDot.IsVisible = modified;

        PART_ResetButton.IsVisible = HasBaseline;
        PART_ResetButton.IsEnabled = HasBaseline && modified && !IsReadOnly;
        PART_ResetButton.Opacity = modified ? 1.0 : 0.25;

        PART_ValueTextBox.IsReadOnly = IsReadOnly;
        PART_Slider.IsEnabled = !IsReadOnly;

        (double min, double max) = GetEffectiveBounds();
        double effectiveStep = GetEffectiveStep(min, max);
        double clampedValue = ClampAndSnapForSlider(Value, min, max);

        _isUpdatingControls = true;
        try
        {
            if (max > PART_Slider.Maximum)
            {
                PART_Slider.Maximum = max;
                PART_Slider.Minimum = min;
            }
            else
            {
                PART_Slider.Minimum = min;
                PART_Slider.Maximum = max;
            }

            PART_Slider.SmallChange = effectiveStep;
            PART_Slider.LargeChange = Math.Min(max - min, effectiveStep * 10.0);
            PART_Slider.TickFrequency = effectiveStep;
            PART_Slider.IsSnapToTickEnabled = IsInteger;
            PART_Slider.Value = clampedValue;

            if (!PART_ValueTextBox.IsFocused)
            {
                PART_ValueTextBox.Text = FormatNumericValue(Value);
            }
        }
        finally
        {
            _isUpdatingControls = false;
        }
    }

    private (double Min, double Max) GetEffectiveBounds()
    {
        double rawMin = double.IsFinite(Minimum) ? Minimum : 0.0;
        double rawMax = double.IsFinite(Maximum) ? Maximum : 1.0;

        double min = Math.Min(rawMin, rawMax);
        double max = Math.Max(rawMin, rawMax);
        if (max - min < 1e-9)
        {
            max = min + 1.0;
        }

        return (min, max);
    }

    private double GetEffectiveStep(double min, double max)
    {
        double step = double.IsFinite(Step) && Step > 0.0 ? Step : (max - min) / 100.0;
        if (IsInteger)
        {
            step = Math.Max(1.0, Math.Round(step, MidpointRounding.AwayFromZero));
        }

        return Math.Max(1e-9, step);
    }

    private double ClampAndSnapForSlider(double value, double min, double max)
    {
        double safe = double.IsFinite(value) ? value : min;
        double clamped = Math.Clamp(safe, min, max);
        return IsInteger ? Math.Round(clamped, MidpointRounding.AwayFromZero) : clamped;
    }

    private string FormatNumericValue(double value)
    {
        if (!double.IsFinite(value))
        {
            return "0";
        }

        string format = string.IsNullOrWhiteSpace(FormatString) ? "F2" : FormatString.Trim();
        if (IsInteger)
        {
            long rounded = (long)Math.Round(value, MidpointRounding.AwayFromZero);
            if (string.Equals(format, "F2", StringComparison.Ordinal))
            {
                return rounded.ToString("F0", CultureInfo.CurrentCulture);
            }

            try
            {
                return rounded.ToString(format, CultureInfo.CurrentCulture);
            }
            catch (FormatException)
            {
                return rounded.ToString(CultureInfo.CurrentCulture);
            }
        }

        try
        {
            return value.ToString(format, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return value.ToString("F2", CultureInfo.CurrentCulture);
        }
    }

    private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_isUpdatingControls || e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        if (IsReadOnly)
        {
            SyncAllControls();
            return;
        }

        (double min, double max) = GetEffectiveBounds();
        double next = ClampAndSnapForSlider(PART_Slider.Value, min, max);
        if (Math.Abs(next - Value) > 1e-12)
        {
            SetCurrentValue(ValueProperty, next);
        }
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        PointerPointProperties props = e.GetCurrentPoint(PART_Slider).Properties;
        if (props.IsLeftButtonPressed)
        {
            BeginDragSession();
        }
    }

    private void OnSliderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            EndDragSession();
        }
    }

    private void OnSliderPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        EndDragSession();
    }

    private void OnThumbDragStarted(object? sender, VectorEventArgs e)
    {
        if (!IsReadOnly)
        {
            BeginDragSession();
        }
    }

    private void OnThumbDragCompleted(object? sender, VectorEventArgs e)
    {
        EndDragSession();
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

    private void OnResetButtonClick(object? sender, RoutedEventArgs e)
    {
        ResetToBaseline();
    }

    private void OnResetGesture(object? sender, TappedEventArgs e)
    {
        if (IsReadOnly || !HasBaseline)
        {
            return;
        }

        ResetToBaseline();
        e.Handled = true;
    }

    private void OnValueTextBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        PART_ValueTextBox.SelectAll();
    }

    private void OnValueTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitTextBoxValue();
    }

    private void OnValueTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTextBoxValue();
            PART_ValueTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            PART_ValueTextBox.Text = FormatNumericValue(Value);
            PART_ValueTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (!IsReadOnly && (e.Key == Key.Up || e.Key == Key.Down))
        {
            (double min, double max) = GetEffectiveBounds();
            double step = GetEffectiveStep(min, max);
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                step *= 10.0;
            }

            double delta = e.Key == Key.Up ? step : -step;
            double next = ClampAndSnapForSlider(Value + delta, min, max);
            SetCurrentValue(ValueProperty, next);
            PART_ValueTextBox.Text = FormatNumericValue(Value);
            PART_ValueTextBox.SelectAll();
            e.Handled = true;
        }
    }

    private void CommitTextBoxValue()
    {
        if (IsReadOnly)
        {
            PART_ValueTextBox.Text = FormatNumericValue(Value);
            return;
        }

        if (TryParseUserText(PART_ValueTextBox.Text, out double parsed))
        {
            (double min, double max) = GetEffectiveBounds();
            double clamped = ClampAndSnapForSlider(parsed, min, max);
            if (Math.Abs(clamped - Value) > 1e-12)
            {
                SetCurrentValue(ValueProperty, clamped);
            }
        }

        PART_ValueTextBox.Text = FormatNumericValue(Value);
    }

    private bool TryParseUserText(string? rawText, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        string text = rawText.Trim();
        if (!string.IsNullOrWhiteSpace(Unit) &&
            text.EndsWith(Unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^Unit.Trim().Length].Trim();
        }

        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if (double.TryParse(text, styles, CultureInfo.CurrentCulture, out double local) && double.IsFinite(local))
        {
            value = local;
            return true;
        }

        if (double.TryParse(text, styles, CultureInfo.InvariantCulture, out double invariant) && double.IsFinite(invariant))
        {
            value = invariant;
            return true;
        }

        return false;
    }
}
