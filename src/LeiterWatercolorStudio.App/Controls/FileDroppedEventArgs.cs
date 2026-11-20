using Avalonia.Interactivity;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Event data for a local file dropped onto the viewport.
/// </summary>
public sealed class FileDroppedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of <see cref="FileDroppedEventArgs"/>.
    /// </summary>
    public FileDroppedEventArgs(RoutedEvent? routedEvent, string path)
        : base(routedEvent)
    {
        Path = path;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="FileDroppedEventArgs"/>.
    /// </summary>
    public FileDroppedEventArgs(string path)
        : this(null, path)
    {
    }

    /// <summary>
    /// Gets the local file path dropped onto the viewport.
    /// </summary>
    public string Path { get; }
}
