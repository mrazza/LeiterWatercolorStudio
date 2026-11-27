using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Lightweight custom surface control that renders single, split-wipe, and synchronized side-by-side bitmaps.
/// </summary>
public sealed class ViewportSurface : Control
{
    private static readonly IBrush FallbackCanvasBackgroundBrush = new SolidColorBrush(Color.Parse("#14161B"));
    private static readonly IBrush FallbackImageMatBrush = new SolidColorBrush(Color.Parse("#1B1F27"));
    private static readonly IBrush FallbackImageBorderBrush = new SolidColorBrush(Color.Parse("#33FFFFFF"));
    private static readonly IBrush FallbackDividerBrush = new SolidColorBrush(Color.Parse("#2D3442"));
    private static readonly IBrush FallbackPixelGridBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));

    private IBrush _canvasBackgroundBrush = FallbackCanvasBackgroundBrush;
    private IBrush _imageMatBrush = FallbackImageMatBrush;
    private IPen _imageBorderPen = new Pen(FallbackImageBorderBrush, 1.0);
    private IPen _dividerPen = new Pen(FallbackDividerBrush, 2.0);
    private IPen _pixelGridPen = new Pen(FallbackPixelGridBrush, 1.0);

    /// <summary>
    /// Initializes a new instance of <see cref="ViewportSurface"/>.
    /// </summary>
    public ViewportSurface()
    {
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    internal ZoomPanViewport? Owner { get; set; }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateThemeBrushes();
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        UpdateThemeBrushes();
        InvalidateVisual();
    }

    private void UpdateThemeBrushes()
    {
        _canvasBackgroundBrush = ResolveBrush("StudioViewportMatBrush", FallbackCanvasBackgroundBrush);
        _imageMatBrush = ResolveBrush("StudioViewportImageFrameBrush", FallbackImageMatBrush);
        _imageBorderPen = new Pen(ResolveBrush("StudioViewportImageBorderBrush", FallbackImageBorderBrush), 1.0);
        _dividerPen = new Pen(ResolveBrush("StudioViewportDividerBrush", FallbackDividerBrush), 2.0);
        _pixelGridPen = new Pen(ResolveBrush("StudioViewportPixelGridBrush", FallbackPixelGridBrush), 1.0);
    }

    private IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, ActualThemeVariant, out object? resource) &&
            resource is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        Rect bounds = new(Bounds.Size);
        context.FillRectangle(_canvasBackgroundBrush, bounds);

        if (Owner is null || Owner.IsEmpty || (Owner.PrimaryBitmap is null && Owner.CompareBitmap is null))
        {
            return;
        }

        double zoom = Owner.Zoom;
        double panX = Owner.PanX;
        double panY = Owner.PanY;
        WriteableBitmap? primary = Owner.PrimaryBitmap;
        WriteableBitmap? compare = Owner.CompareBitmap;

        if (Owner.IsHoldComparing)
        {
            PixelSize? referenceSize = primary?.PixelSize ?? compare?.PixelSize;
            DrawPaneBitmap(context, bounds, compare ?? primary, referenceSize, zoom, panX, panY);
            return;
        }

        switch (Owner.CompareMode)
        {
            case CompareMode.SideBySide:
            {
                (Rect leftPane, Rect rightPane, double dividerX) = Owner.GetSideBySidePanes(Bounds.Size);
                DrawPaneBitmap(context, leftPane, compare, primary?.PixelSize, zoom, panX, panY);
                DrawPaneBitmap(context, rightPane, primary, primary?.PixelSize, zoom, panX, panY);
                context.DrawLine(
                    _dividerPen,
                    new Point(dividerX + 1.0, 0),
                    new Point(dividerX + 1.0, Bounds.Height));
                break;
            }
            case CompareMode.SplitWipe:
            {
                double wipeX = Math.Clamp(Owner.WipePosition, 0.0, 1.0) * Bounds.Width;
                PixelSize? referenceSize = primary?.PixelSize ?? compare?.PixelSize;

                Rect leftClip = new(0, 0, wipeX, Bounds.Height);
                Rect rightClip = new(wipeX, 0, Math.Max(0.0, Bounds.Width - wipeX), Bounds.Height);

                if (leftClip.Width > 0)
                {
                    using (context.PushClip(leftClip))
                    {
                        DrawPaneBitmap(context, bounds, compare, referenceSize, zoom, panX, panY);
                    }
                }

                if (rightClip.Width > 0)
                {
                    using (context.PushClip(rightClip))
                    {
                        DrawPaneBitmap(context, bounds, primary, referenceSize, zoom, panX, panY);
                    }
                }

                break;
            }
            default:
            {
                DrawPaneBitmap(context, bounds, primary, primary?.PixelSize, zoom, panX, panY);
                break;
            }
        }
    }

    private void DrawPaneBitmap(
        DrawingContext context,
        Rect paneRect,
        WriteableBitmap? bitmap,
        PixelSize? referencePixelSize,
        double zoom,
        double panX,
        double panY)
    {
        if (paneRect.Width <= 0 || paneRect.Height <= 0)
        {
            return;
        }

        using (context.PushClip(paneRect))
        {
            PixelSize? targetPixels = referencePixelSize ?? bitmap?.PixelSize;
            if (targetPixels is not PixelSize size || size.Width <= 0 || size.Height <= 0)
            {
                return;
            }

            double destWidth = size.Width * zoom;
            double destHeight = size.Height * zoom;
            double destX = paneRect.X + paneRect.Width * 0.5 + panX - destWidth * 0.5;
            double destY = paneRect.Y + paneRect.Height * 0.5 + panY - destHeight * 0.5;
            Rect destRect = new(destX, destY, destWidth, destHeight);

            context.FillRectangle(_imageMatBrush, destRect);

            if (bitmap is not null && bitmap.PixelSize.Width > 0 && bitmap.PixelSize.Height > 0)
            {
                Rect srcRect = new(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
                context.DrawImage(bitmap, srcRect, destRect);
            }

            context.DrawRectangle(_imageBorderPen, destRect);

            if (zoom >= ZoomPanViewport.PixelGridThresholdZoom && bitmap is not null)
            {
                DrawPixelGrid(context, paneRect, destRect, size.Width, size.Height, zoom);
            }
        }
    }

    private void DrawPixelGrid(
        DrawingContext context,
        Rect paneRect,
        Rect destRect,
        int pixelWidth,
        int pixelHeight,
        double zoom)
    {
        int startX = Math.Max(1, (int)Math.Ceiling((paneRect.Left - destRect.Left) / zoom));
        int endX = Math.Min(pixelWidth - 1, (int)Math.Floor((paneRect.Right - destRect.Left) / zoom));
        double top = Math.Max(paneRect.Top, destRect.Top);
        double bottom = Math.Min(paneRect.Bottom, destRect.Bottom);

        if (bottom > top)
        {
            for (int x = startX; x <= endX; x++)
            {
                double sx = destRect.Left + x * zoom;
                context.DrawLine(_pixelGridPen, new Point(sx, top), new Point(sx, bottom));
            }
        }

        int startY = Math.Max(1, (int)Math.Ceiling((paneRect.Top - destRect.Top) / zoom));
        int endY = Math.Min(pixelHeight - 1, (int)Math.Floor((paneRect.Bottom - destRect.Top) / zoom));
        double left = Math.Max(paneRect.Left, destRect.Left);
        double right = Math.Min(paneRect.Right, destRect.Right);

        if (right > left)
        {
            for (int y = startY; y <= endY; y++)
            {
                double sy = destRect.Top + y * zoom;
                context.DrawLine(_pixelGridPen, new Point(left, sy), new Point(right, sy));
            }
        }
    }
}
