namespace LeiterWatercolorStudio.App.Services;

/// <summary>
/// Abstracts file picker, folder picker, confirmation, and error dialog interactions for testability.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Prompts the user to select an image file to open.
    /// </summary>
    Task<string?> PickOpenImageAsync();

    /// <summary>
    /// Prompts the user for a destination file path when saving the rendered final image.
    /// </summary>
    Task<string?> PickSaveFinalImageAsync(string suggestedFileName);

    /// <summary>
    /// Prompts the user to select an output folder for multi-stage exports.
    /// </summary>
    Task<string?> PickExportFolderAsync();

    /// <summary>
    /// Prompts the user for a destination path when saving a JSON preset file.
    /// </summary>
    Task<string?> PickSavePresetAsync(string suggestedFileName);

    /// <summary>
    /// Prompts the user to select a JSON preset file to load.
    /// </summary>
    Task<string?> PickOpenPresetAsync();

    /// <summary>
    /// Displays a confirmation prompt and returns <see langword="true"/> when the user selects the confirm action.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No");

    /// <summary>
    /// Displays an error message dialog to the user.
    /// </summary>
    Task ShowErrorAsync(string title, string message);
}
