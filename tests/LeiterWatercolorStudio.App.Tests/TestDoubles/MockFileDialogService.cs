using LeiterWatercolorStudio.App.Services;

namespace LeiterWatercolorStudio.App.Tests.TestDoubles;

internal sealed class MockFileDialogService : IFileDialogService
{
    public string? OpenImagePath { get; set; }
    public string? SaveFinalImagePath { get; set; }
    public string? ExportFolderPath { get; set; }
    public string? SavePresetPath { get; set; }
    public string? OpenPresetPath { get; set; }
    public bool ConfirmResult { get; set; }
    public List<(string Title, string Message)> ConfirmCalls { get; } = [];
    public List<(string Title, string Message)> ErrorCalls { get; } = [];

    public Task<string?> PickOpenImageAsync() => Task.FromResult(OpenImagePath);
    public Task<string?> PickSaveFinalImageAsync(string suggestedFileName) => Task.FromResult(SaveFinalImagePath);
    public Task<string?> PickExportFolderAsync() => Task.FromResult(ExportFolderPath);
    public Task<string?> PickSavePresetAsync(string suggestedFileName) => Task.FromResult(SavePresetPath);
    public Task<string?> PickOpenPresetAsync() => Task.FromResult(OpenPresetPath);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Yes", string cancelText = "No")
    {
        ConfirmCalls.Add((title, message));
        return Task.FromResult(ConfirmResult);
    }

    public Task ShowErrorAsync(string title, string message)
    {
        ErrorCalls.Add((title, message));
        return Task.CompletedTask;
    }
}
