using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Attaches focus, Enter/Escape, LostFocus, and error-affordance handling to a validated <see cref="TextBox"/>.
/// </summary>
internal sealed class ValidatedTextBoxHandler
{
    private readonly TextBox _textBox;
    private readonly TextBlock? _errorTextBlock;
    private readonly Func<string> _getModelText;
    private readonly Func<string?, string?> _tryCommit;
    private readonly Action<string?>? _onErrorChanged;
    private bool _isUpdating;
    private bool _isCommitting;

    /// <summary>
    /// Initializes a new <see cref="ValidatedTextBoxHandler"/> wired to the specified text input.
    /// </summary>
    public ValidatedTextBoxHandler(
        TextBox textBox,
        TextBlock? errorTextBlock,
        Func<string> getModelText,
        Func<string?, string?> tryCommit,
        Action<string?>? onErrorChanged = null)
    {
        _textBox = textBox;
        _errorTextBlock = errorTextBlock;
        _getModelText = getModelText;
        _tryCommit = tryCommit;
        _onErrorChanged = onErrorChanged;

        _textBox.GotFocus += OnGotFocus;
        _textBox.PropertyChanged += OnPropertyChanged;
        _textBox.LostFocus += OnLostFocus;
        _textBox.KeyDown += OnKeyDown;
    }

    /// <summary>
    /// Synchronizes the text box with the underlying model when appropriate.
    /// </summary>
    public void SyncFromModel(bool forceEvenIfFocused = false)
    {
        if (_isCommitting || (!forceEvenIfFocused && _textBox.IsFocused))
        {
            return;
        }

        SetTextProgrammatically(_getModelText());
    }

    /// <summary>
    /// Clears any active validation error and synchronizes the text box from the model.
    /// </summary>
    public void ResetValidationAndSync()
    {
        if (_isCommitting)
        {
            return;
        }

        ClearError();
        SetTextProgrammatically(_getModelText());
    }

    /// <summary>
    /// Clears any displayed validation error without modifying text.
    /// </summary>
    public void ClearError()
    {
        ValidationFeedbackHelper.ClearError(_textBox, _errorTextBlock);
        _onErrorChanged?.Invoke(null);
    }

    private void ShowError(string message)
    {
        ValidationFeedbackHelper.ShowError(_textBox, _errorTextBlock, message);
        _onErrorChanged?.Invoke(message);
    }

    private void Commit(bool keepInvalidText)
    {
        string? rawText = _textBox.Text;
        _isCommitting = true;
        string? error;
        try
        {
            error = _tryCommit(rawText);
        }
        finally
        {
            _isCommitting = false;
        }

        if (error is null)
        {
            ClearError();
            SetTextProgrammatically(_getModelText());
            return;
        }

        if (keepInvalidText)
        {
            ShowError(error);
            return;
        }

        SetTextProgrammatically(_getModelText());
        string rejectedMessage = string.IsNullOrWhiteSpace(rawText)
            ? error
            : $"Rejected \"{rawText.Trim()}\": {error}";
        ShowError(rejectedMessage);
    }

    private void SetTextProgrammatically(string text)
    {
        _isUpdating = true;
        try
        {
            _textBox.Text = text;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        _textBox.SelectAll();
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _isUpdating)
        {
            return;
        }

        ClearError();
    }

    private void OnLostFocus(object? sender, RoutedEventArgs e)
    {
        Commit(keepInvalidText: false);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit(keepInvalidText: true);
            _textBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            ClearError();
            SetTextProgrammatically(_getModelText());
            _textBox.SelectAll();
            e.Handled = true;
        }
    }
}
