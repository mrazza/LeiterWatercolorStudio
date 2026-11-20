using System.Globalization;
using Avalonia.Controls;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Shared helper for displaying inline text-input validation errors and parsing bounded integers.
/// </summary>
internal static class ValidationFeedbackHelper
{
    public const string InvalidClass = "input-invalid";

    /// <summary>
    /// Applies error styling, tooltip, and inline message to a text input and its error banner.
    /// </summary>
    public static void ShowError(TextBox textBox, TextBlock? errorTextBlock, string message)
    {
        if (!textBox.Classes.Contains(InvalidClass))
        {
            textBox.Classes.Add(InvalidClass);
        }

        ToolTip.SetTip(textBox, message);
        if (errorTextBlock is not null)
        {
            errorTextBlock.Text = $"\u26A0 {message}";
            errorTextBlock.IsVisible = true;
        }
    }

    /// <summary>
    /// Clears error styling, tooltip, and inline message from a text input and its error banner.
    /// </summary>
    public static void ClearError(TextBox textBox, TextBlock? errorTextBlock)
    {
        textBox.Classes.Remove(InvalidClass);
        ToolTip.SetTip(textBox, null);
        if (errorTextBlock is not null)
        {
            errorTextBlock.Text = string.Empty;
            errorTextBlock.IsVisible = false;
        }
    }

    /// <summary>
    /// Parses a signed 32-bit integer within <c>[min, max]</c>, producing a descriptive failure message.
    /// </summary>
    public static bool TryParseInt32(
        string? rawText,
        int min,
        int max,
        out int value,
        out string errorMessage)
    {
        value = 0;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(rawText))
        {
            errorMessage = "Enter a whole integer.";
            return false;
        }

        string trimmed = rawText.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) ||
            int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            if (value < min || value > max)
            {
                errorMessage = min == int.MinValue && max == int.MaxValue
                    ? $"Must be a 32-bit integer ({int.MinValue} to {int.MaxValue})."
                    : $"Must be between {min} and {max}.";
                return false;
            }

            return true;
        }

        if (double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out double d) ||
            double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out d))
        {
            if (!double.IsFinite(d))
            {
                errorMessage = "Enter a valid finite whole integer.";
                return false;
            }

            if (d < int.MinValue || d > int.MaxValue)
            {
                errorMessage = $"Must be a 32-bit integer ({int.MinValue} to {int.MaxValue}).";
                return false;
            }

            errorMessage = "Value must be a whole integer without decimals.";
            return false;
        }

        errorMessage = "Enter a valid 32-bit whole integer.";
        return false;
    }
}
