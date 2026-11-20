using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace LeiterWatercolorStudio.App.Services;

/// <summary>
/// Avalonia <see cref="IStorageProvider"/> and modal dialog implementation of <see cref="IFileDialogService"/>.
/// Falls back safely when no active desktop window is available (e.g., in headless unit tests).
/// </summary>
public sealed class AvaloniaFileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType ImageFileType = new("Image Files")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff", "*.tif", "*.tga", "*.gif"],
        MimeTypes = ["image/*"],
    };

    private static readonly FilePickerFileType PngFileType = new("PNG Image")
    {
        Patterns = ["*.png"],
        MimeTypes = ["image/png"],
    };

    private static readonly FilePickerFileType JpegFileType = new("JPEG Image")
    {
        Patterns = ["*.jpg", "*.jpeg"],
        MimeTypes = ["image/jpeg"],
    };

    private static readonly FilePickerFileType WebpFileType = new("WebP Image")
    {
        Patterns = ["*.webp"],
        MimeTypes = ["image/webp"],
    };

    private static readonly FilePickerFileType BmpFileType = new("BMP Image")
    {
        Patterns = ["*.bmp"],
        MimeTypes = ["image/bmp"],
    };

    private static readonly FilePickerFileType PresetFileType = new("Watercolor Preset (*.json)")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    private readonly Func<TopLevel?> _topLevelProvider;

    /// <summary>
    /// Initializes a new <see cref="AvaloniaFileDialogService"/> using an optional <see cref="TopLevel"/> resolver.
    /// </summary>
    public AvaloniaFileDialogService(Func<TopLevel?>? topLevelProvider = null)
    {
        _topLevelProvider = topLevelProvider ?? ResolveDefaultTopLevel;
    }

    /// <inheritdoc />
    public async Task<string?> PickOpenImageAsync()
    {
        TopLevel? topLevel = ResolveTopLevel();
        if (topLevel?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await InvokeOnUiThreadAsync(() =>
            storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Source Image",
                AllowMultiple = false,
                FileTypeFilter = [ImageFileType, FilePickerFileTypes.All],
            })).ConfigureAwait(false);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFinalImageAsync(string suggestedFileName)
    {
        TopLevel? topLevel = ResolveTopLevel();
        if (topLevel?.StorageProvider is not { CanSave: true } storage)
        {
            return null;
        }

        IStorageFile? file = await InvokeOnUiThreadAsync(() =>
            storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Watercolor Image",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "png",
                ShowOverwritePrompt = true,
                FileTypeChoices = [PngFileType, JpegFileType, WebpFileType, BmpFileType, FilePickerFileTypes.All],
            })).ConfigureAwait(false);

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickExportFolderAsync()
    {
        TopLevel? topLevel = ResolveTopLevel();
        if (topLevel?.StorageProvider is not { CanPickFolder: true } storage)
        {
            return null;
        }

        IReadOnlyList<IStorageFolder> folders = await InvokeOnUiThreadAsync(() =>
            storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder for All-Stages Export",
                AllowMultiple = false,
            })).ConfigureAwait(false);

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickSavePresetAsync(string suggestedFileName)
    {
        TopLevel? topLevel = ResolveTopLevel();
        if (topLevel?.StorageProvider is not { CanSave: true } storage)
        {
            return null;
        }

        IStorageFile? file = await InvokeOnUiThreadAsync(() =>
            storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Watercolor Preset",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "json",
                ShowOverwritePrompt = true,
                FileTypeChoices = [PresetFileType, FilePickerFileTypes.All],
            })).ConfigureAwait(false);

        return file?.TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickOpenPresetAsync()
    {
        TopLevel? topLevel = ResolveTopLevel();
        if (topLevel?.StorageProvider is not { CanOpen: true } storage)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await InvokeOnUiThreadAsync(() =>
            storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Load Watercolor Preset",
                AllowMultiple = false,
                FileTypeFilter = [PresetFileType, FilePickerFileTypes.All],
            })).ConfigureAwait(false);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "Yes",
        string cancelText = "No")
    {
        if (ResolveOwnerWindow() is not Window owner)
        {
            return false;
        }

        return await InvokeOnUiThreadAsync(async () =>
        {
            var dialog = BuildMessageDialog(title, message, confirmText, cancelText, out Button confirmButton, out Button? cancelButton);
            confirmButton.Click += (_, _) => dialog.Close(true);
            if (cancelButton is not null)
            {
                cancelButton.Click += (_, _) => dialog.Close(false);
            }

            return await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ShowErrorAsync(string title, string message)
    {
        if (ResolveOwnerWindow() is not Window owner)
        {
            return;
        }

        await InvokeOnUiThreadAsync(async () =>
        {
            var dialog = BuildMessageDialog(title, message, "OK", cancelText: null, out Button okButton, out _);
            okButton.Click += (_, _) => dialog.Close(true);
            await dialog.ShowDialog(owner).ConfigureAwait(true);
        }).ConfigureAwait(false);
    }

    private TopLevel? ResolveTopLevel() => _topLevelProvider() ?? ResolveDefaultTopLevel();

    private Window? ResolveOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return ResolveTopLevel() as Window ?? desktop.MainWindow;
    }

    private static TopLevel? ResolveDefaultTopLevel() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

    private static Task<T> InvokeOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        return Dispatcher.UIThread.InvokeAsync(action);
    }

    private static Task InvokeOnUiThreadAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        return Dispatcher.UIThread.InvokeAsync(action);
    }

    private static Window BuildMessageDialog(
        string title,
        string message,
        string confirmText,
        string? cancelText,
        out Button confirmButton,
        out Button? cancelButton)
    {
        var dialog = new Window
        {
            Title = title,
            MinWidth = 360,
            MaxWidth = 520,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog[!Window.BackgroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("StudioPanelBrush");

        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 19,
        };
        messageBlock[!TextBlock.ForegroundProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("StudioForegroundBrush");

        confirmButton = new Button
        {
            Content = confirmText,
            IsDefault = true,
            MinWidth = 84,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        confirmButton.Classes.Add("studio-accent");

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        if (!string.IsNullOrEmpty(cancelText))
        {
            cancelButton = new Button
            {
                Content = cancelText,
                IsCancel = true,
                MinWidth = 84,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            cancelButton.Classes.Add("studio-btn");
            buttonPanel.Children.Add(cancelButton);
        }
        else
        {
            cancelButton = null;
            confirmButton.IsCancel = true;
        }

        buttonPanel.Children.Add(confirmButton);

        dialog.Content = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 18,
                Children =
                {
                    messageBlock,
                    buttonPanel,
                },
            },
        };

        return dialog;
    }
}
