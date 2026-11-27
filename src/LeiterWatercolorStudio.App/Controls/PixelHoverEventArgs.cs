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
    public PixelHoverEventArgs(RoutedEvent? routedEvent, int x, int y, bool isCompareSide = false)
        : base(routedEvent)
    {
        X = x;
        Y = y;
        IsCompareSide = isCompareSide;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PixelHoverEventArgs"/>.
    /// </summary>
    public PixelHoverEventArgs(int x, int y, bool isCompareSide = false)
        : this(null, x, y, isCompareSide)
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

    /// <summary>
    /// Gets whether the hovered point lies inside the comparison reference pane (left side of Split Wipe / Side-by-Side or Hold-to-Compare).
    /// </summary>
    public bool IsCompareSide { get; }
}
