using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using LeiterSize = Leiter.Core.Size;

namespace LeiterWatercolorStudio.App.Rendering;

/// <summary>
/// Converts <see cref="IReadOnlyMatrix{T}"/> RGB buffers into reusable Avalonia <see cref="WriteableBitmap"/> instances.
/// </summary>
public static class MatrixBitmapConverter
{
    private static readonly Vector DefaultDpi = new(96, 96);

    /// <summary>
    /// Blits <paramref name="source"/> into a <see cref="PixelFormat.Bgra8888"/>, <see cref="AlphaFormat.Opaque"/>
    /// <see cref="WriteableBitmap"/>, reusing <paramref name="existing"/> when its pixel dimensions and format match.
    /// </summary>
    /// <remarks>
    /// When <paramref name="existing"/> cannot be reused, a new <see cref="WriteableBitmap"/> is allocated and
    /// <paramref name="existing"/> is left intact so the caller can update UI bindings before disposing superseded bitmaps.
    /// </remarks>
    public static WriteableBitmap Blit(IReadOnlyMatrix<Rgb8> source, WriteableBitmap? existing = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentException("Source matrix must have positive width and height.", nameof(source));
        }

        int width = source.Width;
        int height = source.Height;

        bool canReuse = existing is not null
            && existing.PixelSize.Width == width
            && existing.PixelSize.Height == height
            && (!existing.Format.HasValue || existing.Format.Value == PixelFormat.Bgra8888);

        WriteableBitmap target = canReuse
            ? existing!
            : new WriteableBitmap(
                new PixelSize(width, height),
                DefaultDpi,
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

        using ILockedFramebuffer framebuffer = target.Lock();
        BlitToFramebuffer(source, framebuffer, width, height);
        return target;
    }

    /// <summary>
    /// Computes aspect-preserving thumbnail dimensions constrained by <paramref name="maxEdge"/> without upscaling.
    /// </summary>
    public static LeiterSize ComputeThumbnailSize(LeiterSize sourceSize, int maxEdge = 160)
    {
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            throw new ArgumentException("Source size must have positive width and height.", nameof(sourceSize));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEdge);

        int longestEdge = Math.Max(sourceSize.Width, sourceSize.Height);
        if (longestEdge <= maxEdge)
        {
            return sourceSize;
        }

        double scale = (double)maxEdge / longestEdge;
        if (sourceSize.Width >= sourceSize.Height)
        {
            int height = Math.Max(1, (int)Math.Round(sourceSize.Height * scale, MidpointRounding.AwayFromZero));
            return new LeiterSize(maxEdge, height);
        }

        int width = Math.Max(1, (int)Math.Round(sourceSize.Width * scale, MidpointRounding.AwayFromZero));
        return new LeiterSize(width, maxEdge);
    }

    /// <summary>
    /// Downscales <paramref name="source"/> with <see cref="MatrixResampler.ResizeBox{T}"/> without upscaling
    /// and blits the result into a reusable <see cref="WriteableBitmap"/>.
    /// </summary>
    public static WriteableBitmap BlitThumbnail(
        IReadOnlyMatrix<Rgb8> source,
        WriteableBitmap? existing = null,
        int maxEdge = 160)
    {
        ArgumentNullException.ThrowIfNull(source);
        LeiterSize targetSize = ComputeThumbnailSize(source.Size, maxEdge);

        if (targetSize == source.Size)
        {
            return Blit(source, existing);
        }

        Matrix<Rgb8> downscaled = source.ResizeBox(targetSize);
        return Blit(downscaled, existing);
    }

    private static unsafe void BlitToFramebuffer(
        IReadOnlyMatrix<Rgb8> source,
        ILockedFramebuffer framebuffer,
        int width,
        int height)
    {
        byte* dstBase = (byte*)framebuffer.Address;
        int rowBytes = framebuffer.RowBytes;

        if (source.TryGetReadOnlySpan(out ReadOnlySpan<Rgb8> span) && span.Length >= width * height)
        {
            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<Rgb8> srcRow = span.Slice(y * width, width);
                byte* dstRow = dstBase + (nint)y * rowBytes;
                CopyRowFromSpan(srcRow, dstRow);
            }

            return;
        }

        for (int y = 0; y < height; y++)
        {
            byte* dstRow = dstBase + (nint)y * rowBytes;
            CopyRowIndexed(source, y, width, dstRow);
        }
    }

    private static unsafe void CopyRowFromSpan(ReadOnlySpan<Rgb8> srcRow, byte* dstRow)
    {
        if (BitConverter.IsLittleEndian && ((nuint)dstRow & 3) == 0)
        {
            uint* dst = (uint*)dstRow;
            for (int x = 0; x < srcRow.Length; x++)
            {
                dst[x] = PackBgra8888(srcRow[x]);
            }

            return;
        }

        for (int x = 0; x < srcRow.Length; x++)
        {
            WritePixelBytes(dstRow, x, srcRow[x]);
        }
    }

    private static unsafe void CopyRowIndexed(IReadOnlyMatrix<Rgb8> source, int y, int width, byte* dstRow)
    {
        if (BitConverter.IsLittleEndian && ((nuint)dstRow & 3) == 0)
        {
            uint* dst = (uint*)dstRow;
            for (int x = 0; x < width; x++)
            {
                dst[x] = PackBgra8888(source[x, y]);
            }

            return;
        }

        for (int x = 0; x < width; x++)
        {
            WritePixelBytes(dstRow, x, source[x, y]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackBgra8888(Rgb8 pixel) =>
        0xFF000000u | ((uint)pixel.R << 16) | ((uint)pixel.G << 8) | pixel.B;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void WritePixelBytes(byte* dstRow, int x, Rgb8 pixel)
    {
        int offset = x * 4;
        dstRow[offset] = pixel.B;
        dstRow[offset + 1] = pixel.G;
        dstRow[offset + 2] = pixel.R;
        dstRow[offset + 3] = 255;
    }
}
