using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Leiter.Algorithms.Noise;
using LeiterWatercolorStudio.Core.Parameters;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Reusable collapsible card editor for <see cref="PerlinNoiseViewModel"/> supporting
/// seed randomization, per-parameter sliders, baseline reset, and drag coalescing events.
/// </summary>
public partial class PerlinNoiseEditor : UserControl
{
    private const double ModifiedEpsilon = 1e-6;

    /// <summary>
    /// Identifies the <see cref="Title"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, string>(nameof(Title), "Noise");

    /// <summary>
    /// Identifies the <see cref="IsExpanded"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, bool>(nameof(IsExpanded), true);

    /// <summary>
    /// Identifies the <see cref="DragStartedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragStartedCommandProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, ICommand?>(nameof(DragStartedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragCompletedCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> DragCompletedCommandProperty =
        AvaloniaProperty.Register<PerlinNoiseEditor, ICommand?>(nameof(DragCompletedCommand), null);

    /// <summary>
    /// Identifies the <see cref="DragStarted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragStartedEvent =
        RoutedEvent.Register<PerlinNoiseEditor, RoutedEventArgs>(nameof(DragStarted), RoutingStrategies.Bubble);

    /// <summary>
    /// Identifies the <see cref="DragCompleted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> DragCompletedEvent =
        RoutedEvent.Register<PerlinNoiseEditor, RoutedEventArgs>(nameof(DragCompleted), RoutingStrategies.Bubble);

    private PerlinNoiseViewModel? _subscribedViewModel;

    /// <summary>
    /// Initializes a new instance of <see cref="PerlinNoiseEditor"/>.
    /// </summary>
    public PerlinNoiseEditor()
    {
        InitializeComponent();

        PART_ExpandButton.Click += OnExpandButtonClick;
        PART_ResetButton.Click += OnResetToBaselineClick;
        PART_RandomizeSeedButton.Click += OnRandomizeSeedClick;

        AddHandler(NumericSlider.DragStartedEvent, OnChildSliderDragStarted);
        AddHandler(NumericSlider.DragCompletedEvent, OnChildSliderDragCompleted);

        SyncHeaderState();
        SyncExpansionState();
    }

    /// <summary>
    /// Occurs when an embedded slider drag begins.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    /// <summary>
    /// Occurs when an embedded slider drag completes.
    /// </summary>
    public event EventHandler<RoutedEventArgs>? DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    /// <summary>
    /// Gets or sets the header title displayed on the noise card.
    /// </summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the noise editor body is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when an embedded slider drag starts.
    /// </summary>
    public ICommand? DragStartedCommand
    {
        get => GetValue(DragStartedCommandProperty);
        set => SetValue(DragStartedCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional command executed when an embedded slider drag completes.
    /// </summary>
    public ICommand? DragCompletedCommand
    {
        get => GetValue(DragCompletedCommandProperty);
        set => SetValue(DragCompletedCommandProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            SyncHeaderState();
        }
        else if (change.Property == IsExpandedProperty)
        {
            SyncExpansionState();
        }
        else if (change.Property == DataContextProperty)
        {
            UpdateViewModelSubscription(change.NewValue as PerlinNoiseViewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateViewModelSubscription(DataContext as PerlinNoiseViewModel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateViewModelSubscription(null);
    }

    private void UpdateViewModelSubscription(PerlinNoiseViewModel? next)
    {
        if (ReferenceEquals(_subscribedViewModel, next))
        {
            SyncHeaderState();
            return;
        }

        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _subscribedViewModel = next;
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        SyncHeaderState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SyncHeaderState();
    }

    private void SyncHeaderState()
    {
        if (PART_TitleText is null)
        {
            return;
        }

        PART_TitleText.Text = string.IsNullOrWhiteSpace(Title) ? "Noise" : Title;

        bool isModified = IsNoiseModified(DataContext as PerlinNoiseViewModel);
        PART_ModifiedDot.IsVisible = isModified;
        PART_ResetButton.IsEnabled = DataContext is PerlinNoiseViewModel && isModified;
        PART_ResetButton.Opacity = isModified ? 1.0 : 0.3;
    }

    private void SyncExpansionState()
    {
        if (PART_BodyPanel is null)
        {
            return;
        }

        bool expanded = IsExpanded;
        PART_BodyPanel.IsVisible = expanded;
        PART_ChevronText.Text = expanded ? "▾" : "▸";
    }

    private void OnExpandButtonClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsExpandedProperty, !IsExpanded);
    }

    private void OnRandomizeSeedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PerlinNoiseViewModel vm)
        {
            vm.RandomizeSeed();
        }
    }

    private void OnResetToBaselineClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PerlinNoiseViewModel vm)
        {
            vm.ResetToBaseline();
        }
    }

    private void OnChildSliderDragStarted(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(DragStartedEvent, this));
        ICommand? command = DragStartedCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void OnChildSliderDragCompleted(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(DragCompletedEvent, this));
        ICommand? command = DragCompletedCommand;
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private static bool IsNoiseModified(PerlinNoiseViewModel? vm)
    {
        if (vm is null)
        {
            return false;
        }

        PerlinNoiseOptions baseline = vm.Baseline;
        return vm.Seed != baseline.Seed ||
               Math.Abs(vm.Frequency - baseline.Frequency) > ModifiedEpsilon ||
               vm.Octaves != baseline.Octaves ||
               Math.Abs(vm.Lacunarity - baseline.Lacunarity) > ModifiedEpsilon ||
               Math.Abs(vm.Persistence - baseline.Persistence) > ModifiedEpsilon;
    }
}
