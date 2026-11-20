using Avalonia.Interactivity;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Event data for viewport pointer hover over an image pixel.
/// </summary>
public sealed class PixelHoverEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of <see cref="PixelHoverEventArgs"/>.
    /// </summary>
    public PixelHoverEventArgs(RoutedEvent? routedEvent, int x, int y)
        : base(routedEvent)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PixelHoverEventArgs"/>.
    /// </summary>
    public PixelHoverEventArgs(int x, int y)
        : this(null, x, y)
    {
    }

    /// <summary>
    /// Gets the zero-based image X coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the zero-based image Y coordinate.
    /// </summary>
    public int Y { get; }
}
