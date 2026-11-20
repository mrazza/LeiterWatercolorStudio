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
    /// Identifies the <see cref="ValidMinimum"/> styled property defining the lower mathematical validity domain.
    /// </summary>
    public static readonly StyledProperty<double> ValidMinimumProperty =
        AvaloniaProperty.Register<NumericSlider, double>(nameof(ValidMinimum), double.NegativeInfinity);

    /// <summary>
    /// Identifies the <see cref="ValidMaximum"/> styled property defining the upper mathematical validity domain.
    /// </summary>
    public static readonly StyledProperty<double> ValidMaximumProperty =
        AvaloniaProperty.Register<NumericSlider, double>(nameof(ValidMaximum), double.PositiveInfinity);

    /// <summary>
    /// Identifies the <see cref="IsValidMinimumExclusive"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> IsValidMinimumExclusiveProperty =
        AvaloniaProperty.Register<NumericSlider, bool>(nameof(IsValidMinimumExclusive), false);

    /// <summary>
    /// Identifies the <see cref="IsValidMaximumExclusive"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> IsValidMaximumExclusiveProperty =
        AvaloniaProperty.Register<NumericSlider, bool>(nameof(IsValidMaximumExclusive), false);

    /// <summary>
    /// Identifies the <see cref="ValidationError"/> direct property.
    /// </summary>
    public static readonly DirectProperty<NumericSlider, string?> ValidationErrorProperty =
        AvaloniaProperty.RegisterDirect<NumericSlider, string?>(
            nameof(ValidationError),
            o => o.ValidationError);

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
    private bool _isUpdatingTextBox;
    private bool _isCommittingTextBox;
    private bool _isDragging;
    private string? _validationError;

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
        PART_ValueTextBox.PropertyChanged += OnValueTextBoxPropertyChanged;

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
    /// Gets or sets the mathematical minimum value accepted by numeric text entry.
    /// Defaults to <see cref="double.NegativeInfinity"/>.
    /// </summary>
    public double ValidMinimum
    {
        get => GetValue(ValidMinimumProperty);
        set => SetValue(ValidMinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the mathematical maximum value accepted by numeric text entry.
    /// Defaults to <see cref="double.PositiveInfinity"/>.
    /// </summary>
    public double ValidMaximum
    {
        get => GetValue(ValidMaximumProperty);
        set => SetValue(ValidMaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets whether <see cref="ValidMinimum"/> is strictly exclusive (<c>&gt;</c> instead of <c>&gt;=</c>).
    /// </summary>
    public bool IsValidMinimumExclusive
    {
        get => GetValue(IsValidMinimumExclusiveProperty);
        set => SetValue(IsValidMinimumExclusiveProperty, value);
    }

    /// <summary>
    /// Gets or sets whether <see cref="ValidMaximum"/> is strictly exclusive (<c>&lt;</c> instead of <c>&lt;=</c>).
    /// </summary>
    public bool IsValidMaximumExclusive
    {
        get => GetValue(IsValidMaximumExclusiveProperty);
        set => SetValue(IsValidMaximumExclusiveProperty, value);
    }

    /// <summary>
    /// Gets the active validation error message, or <see langword="null"/> when valid.
    /// </summary>
    public string? ValidationError
    {
        get => _validationError;
        private set => SetAndRaise(ValidationErrorProperty, ref _validationError, value);
    }

    /// <summary>
    /// Gets whether the numeric text entry currently has an active validation error.
    /// </summary>
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

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
        ClearValidationError();
        SetCurrentValue(ValueProperty, target);
        SetTextBoxTextProgrammatically(FormatNumericValue(Value));
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ValueProperty && !_isCommittingTextBox)
        {
            ClearValidationError();
        }

        if (change.Property == DataContextProperty)
        {
            ClearValidationError();
        }

        if (change.Property == LabelProperty ||
            change.Property == DescriptionProperty ||
            change.Property == InfoTextProperty ||
            change.Property == UnitProperty ||
            change.Property == ValueProperty ||
            change.Property == MinimumProperty ||
            change.Property == MaximumProperty ||
            change.Property == ValidMinimumProperty ||
            change.Property == ValidMaximumProperty ||
            change.Property == IsValidMinimumExclusiveProperty ||
            change.Property == IsValidMaximumExclusiveProperty ||
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

            if (double.IsFinite(Value) && (Value < min - 1e-12 || Value > max + 1e-12))
            {
                ToolTip.SetTip(
                    PART_Slider,
                    $"Current value ({FormatNumericValue(Value)}) is outside slider track [{FormatNumericValue(min)}, {FormatNumericValue(max)}]. Dragging the slider returns within track bounds.");
            }
            else
            {
                ToolTip.SetTip(PART_Slider, null);
            }

            if (!PART_ValueTextBox.IsFocused)
            {
                SetTextBoxTextProgrammatically(FormatNumericValue(Value));
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

        if (HasBaseline && double.IsFinite(BaselineValue) && IsWithinValidDomain(BaselineValue))
        {
            if (BaselineValue < min)
            {
                min = BaselineValue;
            }

            if (BaselineValue > max)
            {
                max = BaselineValue;
            }
        }

        if (max - min < 1e-9)
        {
            max = min + 1.0;
        }

        return (min, max);
    }

    private (double Min, bool MinExclusive, double Max, bool MaxExclusive) GetValidDomain()
    {
        double min = double.IsNaN(ValidMinimum) ? double.NegativeInfinity : ValidMinimum;
        double max = double.IsNaN(ValidMaximum) ? double.PositiveInfinity : ValidMaximum;
        return (min, IsValidMinimumExclusive, max, IsValidMaximumExclusive);
    }

    private bool IsWithinValidDomain(double candidate)
    {
        if (!double.IsFinite(candidate))
        {
            return false;
        }

        (double min, bool minExclusive, double max, bool maxExclusive) = GetValidDomain();
        if (!double.IsNegativeInfinity(min) && (minExclusive ? candidate <= min : candidate < min))
        {
            return false;
        }

        if (!double.IsPositiveInfinity(max) && (maxExclusive ? candidate >= max : candidate > max))
        {
            return false;
        }

        if (IsInteger && (candidate < int.MinValue || candidate > int.MaxValue || Math.Truncate(candidate) != candidate))
        {
            return false;
        }

        return true;
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

        if (value == 0.0)
        {
            value = 0.0;
        }

        string format = string.IsNullOrWhiteSpace(FormatString) ? "F2" : FormatString.Trim();
        if (IsInteger)
        {
            long rounded = (long)Math.Round(
                Math.Clamp(value, long.MinValue / 2.0, long.MaxValue / 2.0),
                MidpointRounding.AwayFromZero);
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

        string formatted;
        try
        {
            formatted = value.ToString(format, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            formatted = value.ToString("F2", CultureInfo.CurrentCulture);
        }

        if (double.TryParse(formatted, NumberStyles.Float, CultureInfo.CurrentCulture, out double roundTripped))
        {
            bool sameZeroSign = (roundTripped == 0.0 && value == 0.0) || (roundTripped != 0.0 && value != 0.0);
            double tolerance = 1e-12 * Math.Max(1e-12, Math.Abs(value));
            if (sameZeroSign && Math.Abs(roundTripped - value) <= tolerance)
            {
                return formatted;
            }
        }

        double abs = Math.Abs(value);
        if (abs == 0.0 || (abs >= 1e-8 && abs < 1e9))
        {
            return value.ToString("0.############", CultureInfo.CurrentCulture);
        }

        return value.ToString("G9", CultureInfo.CurrentCulture);
    }

    private string FormatBoundValue(double bound)
    {
        if (!double.IsFinite(bound))
        {
            return bound.ToString(CultureInfo.CurrentCulture);
        }

        if (bound == 0.0)
        {
            return "0";
        }

        if (Math.Truncate(bound) == bound && Math.Abs(bound) < 1e9)
        {
            return ((long)bound).ToString(CultureInfo.CurrentCulture);
        }

        return FormatNumericValue(bound);
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

        ClearValidationError();
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

    private void OnValueTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _isUpdatingControls || _isUpdatingTextBox)
        {
            return;
        }

        ClearValidationError();
    }

    private void OnValueTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        CommitTextBoxValue(keepInvalidText: false);
    }

    private void OnValueTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTextBoxValue(keepInvalidText: true);
            PART_ValueTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            ClearValidationError();
            SetTextBoxTextProgrammatically(FormatNumericValue(Value));
            PART_ValueTextBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (!IsReadOnly && (e.Key == Key.Up || e.Key == Key.Down))
        {
            (double sliderMin, double sliderMax) = GetEffectiveBounds();
            double step = GetEffectiveStep(sliderMin, sliderMax);
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                step *= 10.0;
            }

            double delta = e.Key == Key.Up ? step : -step;
            double baseValue = TryValidateUserText(PART_ValueTextBox.Text, out double editedValue, out _)
                ? editedValue
                : Value;
            double next = ClampForKeyboardNudge(baseValue, delta);

            ClearValidationError();
            if (next != Value)
            {
                SetCurrentValue(ValueProperty, next);
            }

            SetTextBoxTextProgrammatically(FormatNumericValue(Value));
            PART_ValueTextBox.SelectAll();
            e.Handled = true;
        }
    }

    private double ClampForKeyboardNudge(double current, double delta)
    {
        double safeCurrent = double.IsFinite(current) ? current : 0.0;
        double candidate = safeCurrent + delta;
        if (!double.IsFinite(candidate))
        {
            return safeCurrent;
        }

        if (IsInteger)
        {
            candidate = Math.Round(candidate, MidpointRounding.AwayFromZero);
            candidate = Math.Clamp(candidate, int.MinValue, int.MaxValue);
        }

        (double validMin, bool minExclusive, double validMax, bool maxExclusive) = GetValidDomain();
        if (!double.IsNegativeInfinity(validMin))
        {
            if (minExclusive)
            {
                if (candidate <= validMin)
                {
                    return safeCurrent;
                }
            }
            else if (candidate < validMin)
            {
                candidate = validMin;
            }
        }

        if (!double.IsPositiveInfinity(validMax))
        {
            if (maxExclusive)
            {
                if (candidate >= validMax)
                {
                    return safeCurrent;
                }
            }
            else if (candidate > validMax)
            {
                candidate = validMax;
            }
        }

        return candidate == 0.0 ? 0.0 : candidate;
    }

    private void CommitTextBoxValue(bool keepInvalidText)
    {
        if (IsReadOnly)
        {
            ClearValidationError();
            SetTextBoxTextProgrammatically(FormatNumericValue(Value));
            return;
        }

        string? rawText = PART_ValueTextBox.Text;
        if (TryValidateUserText(rawText, out double parsed, out string errorMessage))
        {
            ClearValidationError();
            _isCommittingTextBox = true;
            try
            {
                if (parsed != Value)
                {
                    SetCurrentValue(ValueProperty, parsed);
                }
            }
            finally
            {
                _isCommittingTextBox = false;
            }

            SetTextBoxTextProgrammatically(FormatNumericValue(Value));
            return;
        }

        if (keepInvalidText)
        {
            SetValidationError(errorMessage);
            return;
        }

        SetTextBoxTextProgrammatically(FormatNumericValue(Value));
        string rejectedMessage = string.IsNullOrWhiteSpace(rawText)
            ? errorMessage
            : $"Rejected \"{rawText.Trim()}\": {errorMessage}";
        SetValidationError(rejectedMessage);
    }

    private bool TryValidateUserText(string? rawText, out double value, out string errorMessage)
    {
        value = 0.0;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            errorMessage = IsInteger ? "Enter a whole integer." : "Enter a numeric value.";
            return false;
        }

        string text = rawText.Trim();
        if (!string.IsNullOrWhiteSpace(Unit) &&
            text.EndsWith(Unit.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^Unit.Trim().Length].Trim();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage = IsInteger ? "Enter a whole integer." : "Enter a numeric value.";
            return false;
        }

        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        bool parsedOk =
            (double.TryParse(text, styles, CultureInfo.CurrentCulture, out double parsed) ||
             double.TryParse(text, styles, CultureInfo.InvariantCulture, out parsed)) &&
            double.IsFinite(parsed);

        if (!parsedOk)
        {
            errorMessage = IsInteger ? "Enter a valid finite whole integer." : "Enter a valid finite number.";
            return false;
        }

        if (IsInteger && Math.Truncate(parsed) != parsed)
        {
            errorMessage = "Value must be a whole integer without decimals.";
            return false;
        }

        (double validMin, bool minExclusive, double validMax, bool maxExclusive) = GetValidDomain();

        if (!double.IsNegativeInfinity(validMin) && (minExclusive ? parsed <= validMin : parsed < validMin))
        {
            errorMessage = minExclusive
                ? $"Must be greater than {FormatBoundValue(validMin)}."
                : $"Must be at least {FormatBoundValue(validMin)}.";
            return false;
        }

        if (!double.IsPositiveInfinity(validMax) && (maxExclusive ? parsed >= validMax : parsed > validMax))
        {
            errorMessage = maxExclusive
                ? $"Must be less than {FormatBoundValue(validMax)}."
                : $"Must be at most {FormatBoundValue(validMax)}.";
            return false;
        }

        if (IsInteger && (parsed < int.MinValue || parsed > int.MaxValue))
        {
            errorMessage = !double.IsNegativeInfinity(validMin) && double.IsPositiveInfinity(validMax)
                ? $"Must be an integer from {FormatBoundValue(Math.Max(int.MinValue, validMin))} to {int.MaxValue}."
                : $"Must be a 32-bit integer ({int.MinValue} to {int.MaxValue}).";
            return false;
        }

        value = IsInteger ? Math.Truncate(parsed) : parsed;
        if (value == 0.0)
        {
            value = 0.0;
        }

        return true;
    }

    private void SetTextBoxTextProgrammatically(string text)
    {
        _isUpdatingTextBox = true;
        try
        {
            PART_ValueTextBox.Text = text;
        }
        finally
        {
            _isUpdatingTextBox = false;
        }
    }

    private void SetValidationError(string message)
    {
        ValidationError = message;
        if (PART_ValueTextBox is not null)
        {
            ValidationFeedbackHelper.ShowError(PART_ValueTextBox, PART_ValidationErrorText, message);
        }
    }

    private void ClearValidationError()
    {
        ValidationError = null;
        if (PART_ValueTextBox is not null)
        {
            ValidationFeedbackHelper.ClearError(PART_ValueTextBox, PART_ValidationErrorText);
        }
    }
}
