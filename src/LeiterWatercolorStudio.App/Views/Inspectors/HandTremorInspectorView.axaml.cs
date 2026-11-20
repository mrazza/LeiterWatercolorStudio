using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Leiter.Pixels;
using LeiterWatercolorStudio.App.Controls;
using LeiterWatercolorStudio.Core.Parameters.Stages;

namespace LeiterWatercolorStudio.App.Views.Inspectors;

/// <summary>
/// Stage 7 (Leiter Stage 5) inspector view for hand tremor boundary perturbation, paper color/gaps, and noise fields.
/// </summary>
public partial class HandTremorInspectorView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="ResetStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> ResetStageCommandProperty =
        AvaloniaProperty.Register<HandTremorInspectorView, ICommand?>(nameof(ResetStageCommand));

    /// <summary>
    /// Identifies the <see cref="PaperColorHexValidationError"/> direct property.
    /// </summary>
    public static readonly DirectProperty<HandTremorInspectorView, string?> PaperColorHexValidationErrorProperty =
        AvaloniaProperty.RegisterDirect<HandTremorInspectorView, string?>(
            nameof(PaperColorHexValidationError),
            o => o.PaperColorHexValidationError);

    private readonly ValidatedTextBoxHandler _hexInputHandler;
    private HandTremorStageViewModel? _subscribedViewModel;
    private string? _paperColorHexValidationError;

    /// <summary>
    /// Initializes a new instance of <see cref="HandTremorInspectorView"/>.
    /// </summary>
    public HandTremorInspectorView()
    {
        InitializeComponent();

        _hexInputHandler = new ValidatedTextBoxHandler(
            PART_PaperColorHexTextBox,
            PART_PaperColorHexErrorText,
            () => DataContext is HandTremorStageViewModel vm ? vm.PaperColorHex : string.Empty,
            rawText =>
            {
                if (DataContext is not HandTremorStageViewModel vm)
                {
                    return null;
                }

                if (!TryParseRgbHex(rawText, out Rgb8 color, out string error))
                {
                    return error;
                }

                vm.PaperColor = color;
                return null;
            },
            err => PaperColorHexValidationError = err);

        _hexInputHandler.SyncFromModel(forceEvenIfFocused: true);
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
    /// Gets the active validation error message for the hex paper color input, or <see langword="null"/> when valid.
    /// </summary>
    public string? PaperColorHexValidationError
    {
        get => _paperColorHexValidationError;
        private set => SetAndRaise(PaperColorHexValidationErrorProperty, ref _paperColorHexValidationError, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
        {
            UpdateViewModelSubscription(change.NewValue as HandTremorStageViewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateViewModelSubscription(DataContext as HandTremorStageViewModel);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UpdateViewModelSubscription(null);
    }

    private void UpdateViewModelSubscription(HandTremorStageViewModel? next)
    {
        if (ReferenceEquals(_subscribedViewModel, next))
        {
            _hexInputHandler.SyncFromModel();
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

        _hexInputHandler.ResetValidationAndSync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            string.Equals(e.PropertyName, nameof(HandTremorStageViewModel.PaperColorHex), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(HandTremorStageViewModel.PaperColor), StringComparison.Ordinal))
        {
            _hexInputHandler.ResetValidationAndSync();
        }
    }

    private static bool TryParseRgbHex(string? rawText, out Rgb8 color, out string errorMessage)
    {
        color = default;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            errorMessage = "Enter a 6-digit hex color (e.g. #FAF8F2).";
            return false;
        }

        string trimmed = rawText.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length != 6 ||
            !byte.TryParse(trimmed.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(trimmed.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(trimmed.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            errorMessage = "Enter a valid 6-digit #RRGGBB hex color (e.g. #FAF8F2).";
            return false;
        }

        color = new Rgb8(r, g, b);
        return true;
    }
}
