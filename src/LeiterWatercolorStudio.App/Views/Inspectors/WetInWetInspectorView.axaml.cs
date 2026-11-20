using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.Core.Parameters.Stages;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Stage 6 (Leiter Stage 4) inspector view for wet-in-wet edge bleeding parameters and boundary statistics.
/// </summary>
public partial class WetInWetInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<WetInWetInspectorView, ICommand?>(nameof(ResetStageCommand));

    /// <summary>
    /// Identifies the <see cref="StatsText"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> StatsTextProperty =
        AvaloniaProperty.Register<WetInWetInspectorView, string?>(nameof(StatsText));

    /// <summary>
    /// Identifies the <see cref="SeedValidationError"/> direct property.
    /// </summary>
    public static readonly DirectProperty<WetInWetInspectorView, string?> SeedValidationErrorProperty =
        AvaloniaProperty.RegisterDirect<WetInWetInspectorView, string?>(
            nameof(SeedValidationError),
            o => o.SeedValidationError);

    /// <summary>
    /// Gets all supported <see cref="WetInWetMode"/> values for binding to the mode selector.
    /// </summary>
    public static IReadOnlyList<WetInWetMode> AvailableModes { get; } = Enum.GetValues<WetInWetMode>();

    private readonly ValidatedTextBoxHandler _seedInputHandler;
    private WetInWetStageViewModel? _subscribedViewModel;
    private string? _seedValidationError;

    /// <summary>
    /// Initializes a new instance of <see cref="WetInWetInspectorView"/>.
    /// </summary>
    public WetInWetInspectorView()
    {
        InitializeComponent();

        _seedInputHandler = new ValidatedTextBoxHandler(
            PART_SeedTextBox,
            PART_SeedErrorText,
            () => DataContext is WetInWetStageViewModel vm
                ? vm.Seed.ToString(CultureInfo.CurrentCulture)
                : string.Empty,
            rawText =>
            {
                if (DataContext is not WetInWetStageViewModel vm)
                {
                    return null;
                }

                if (!ValidationFeedbackHelper.TryParseInt32(
                        rawText,
                        int.MinValue,
                        int.MaxValue,
                        out int seed,
                        out string error))
                {
                    return error;
                }

                if (vm.Seed != seed)
                {
                    vm.Seed = seed;
                }

                return null;
            },
            err => SeedValidationError = err);

        _seedInputHandler.SyncFromModel(forceEvenIfFocused: true);
    }

    /// <summary>
    /// Gets or sets the command invoked when the user clicks the stage reset button.
    /// </summary>
    public ICommand? ResetStageCommand
    {
        get => GetValue(ResetStageCommandProperty);
        set => SetValue(ResetStageCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the live qualifying boundary statistics text displayed in the inspector.
    /// </summary>
    public string? StatsText
    {
        get => GetValue(StatsTextProperty);
        set => SetValue(StatsTextProperty, value);
    }

    /// <summary>
    /// Gets the active validation error message for the seed input, or <see langword="null"/> when valid.
    /// </summary>
    public string? SeedValidationError
    {
        get => _seedValidationError;
        private set => SetAndRaise(SeedValidationErrorProperty, ref _seedValidationError, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
        {
            UpdateViewModelSubscription(change.NewValue as WetInWetStageViewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateViewModelSubscription(DataContext as WetInWetStageViewModel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateViewModelSubscription(null);
    }

    private void UpdateViewModelSubscription(WetInWetStageViewModel? next)
    {
        if (ReferenceEquals(_subscribedViewModel, next))
        {
            _seedInputHandler.SyncFromModel();
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

        _seedInputHandler.ResetValidationAndSync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            string.Equals(e.PropertyName, nameof(WetInWetStageViewModel.Seed), StringComparison.Ordinal))
        {
            _seedInputHandler.ResetValidationAndSync();
        }
    }

    private void OnRandomizeSeedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WetInWetStageViewModel vm)
        {
            vm.RandomizeSeed();
            _seedInputHandler.ResetValidationAndSync();
        }
    }
}
