using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Leiter.Core;
using Leiter.Pixels;
using LeiterWatercolorStudio.App.Rendering;
using Xunit;
using LeiterSize = Leiter.Core.Size;

namespace LeiterWatercolorStudio.App.Tests;

public class MatrixBitmapConverterTests
{
    [AvaloniaFact]
    public void Blit_SequentialMatrix_WritesExactBgra8888ByteLayout()
    {
        var matrix = new SequentialMatrix<Rgb8>(3, 2);
        matrix[0, 0] = new Rgb8(12, 34, 56);
        matrix[1, 0] = new Rgb8(255, 0, 128);
        matrix[2, 0] = new Rgb8(0, 255, 64);
        matrix[0, 1] = new Rgb8(17, 99, 201);
        matrix[1, 1] = new Rgb8(240, 241, 242);
        matrix[2, 1] = new Rgb8(1, 2, 3);

        using WriteableBitmap bitmap = MatrixBitmapConverter.Blit(matrix);

        Assert.Equal(new PixelSize(3, 2), bitmap.PixelSize);
        Assert.Equal(PixelFormat.Bgra8888, bitmap.Format);
        Assert.Equal(new Vector(96, 96), bitmap.Dpi);

        AssertBgraPixelsMatch(bitmap, matrix);
    }

    [AvaloniaFact]
    public void Blit_NonContiguousMatrixFallback_WritesExactBgra8888ByteLayout()
    {
        var inner = new SequentialMatrix<Rgb8>(3, 2);
        inner[0, 0] = new Rgb8(10, 20, 30);
        inner[1, 0] = new Rgb8(40, 50, 60);
        inner[2, 0] = new Rgb8(70, 80, 90);
        inner[0, 1] = new Rgb8(100, 110, 120);
        inner[1, 1] = new Rgb8(130, 140, 150);
        inner[2, 1] = new Rgb8(160, 170, 180);

        var nonContiguous = new NonContiguousMatrixAdapter(inner);

        using WriteableBitmap bitmap = MatrixBitmapConverter.Blit(nonContiguous);

        Assert.Equal(new PixelSize(3, 2), bitmap.PixelSize);
        AssertBgraPixelsMatch(bitmap, inner);
    }

    [AvaloniaFact]
    public void Blit_SameDimensions_ReusesExistingWriteableBitmapInstance()
    {
        var first = new SequentialMatrix<Rgb8>(4, 3);
        first[2, 1] = new Rgb8(11, 22, 33);

        var second = new SequentialMatrix<Rgb8>(4, 3);
        second[0, 0] = new Rgb8(200, 100, 50);
        second[3, 2] = new Rgb8(5, 15, 25);

        WriteableBitmap bmp1 = MatrixBitmapConverter.Blit(first);
        try
        {
            WriteableBitmap bmp2 = MatrixBitmapConverter.Blit(second, bmp1);
            Assert.True(ReferenceEquals(bmp1, bmp2));
            AssertBgraPixelsMatch(bmp2, second);
        }
        finally
        {
            bmp1.Dispose();
        }
    }

    [AvaloniaFact]
    public void Blit_DimensionOrFormatChange_ReallocatesWithoutDisposingPrevious()
    {
        var initial = new SequentialMatrix<Rgb8>(4, 3);
        initial[1, 1] = new Rgb8(77, 88, 99);

        var resized = new SequentialMatrix<Rgb8>(5, 3);
        resized[4, 2] = new Rgb8(111, 122, 133);

        WriteableBitmap bmp1 = MatrixBitmapConverter.Blit(initial);
        WriteableBitmap? bmp2 = null;
        WriteableBitmap? wrongFormat = null;
        WriteableBitmap? bmp3 = null;

        try
        {
            bmp2 = MatrixBitmapConverter.Blit(resized, bmp1);
            Assert.False(ReferenceEquals(bmp1, bmp2));
            Assert.Equal(new PixelSize(4, 3), bmp1.PixelSize);
            Assert.Equal(new PixelSize(5, 3), bmp2.PixelSize);
            AssertBgraPixelsMatch(bmp1, initial);
            AssertBgraPixelsMatch(bmp2, resized);

            wrongFormat = new WriteableBitmap(
                new PixelSize(4, 3),
                new Vector(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Opaque);

            bmp3 = MatrixBitmapConverter.Blit(initial, wrongFormat);
            Assert.False(ReferenceEquals(wrongFormat, bmp3));
            Assert.Equal(PixelFormat.Bgra8888, bmp3.Format);
            AssertBgraPixelsMatch(bmp3, initial);
        }
        finally
        {
            bmp1.Dispose();
            bmp2?.Dispose();
            wrongFormat?.Dispose();
            bmp3?.Dispose();
        }
    }

    [AvaloniaFact]
    public void BlitThumbnail_PreservesAspectRatio_AndNeverUpscalesSmallInputs()
    {
        var landscape = new SequentialMatrix<Rgb8>(320, 160);
        var portrait = new SequentialMatrix<Rgb8>(120, 300);
        var nonSquare = new SequentialMatrix<Rgb8>(400, 300);
        var small = new SequentialMatrix<Rgb8>(64, 48);
        small[10, 20] = new Rgb8(42, 84, 126);

        using WriteableBitmap landscapeThumb = MatrixBitmapConverter.BlitThumbnail(landscape, maxEdge: 160);
        Assert.Equal(new PixelSize(160, 80), landscapeThumb.PixelSize);

        using WriteableBitmap reusedLandscapeThumb = MatrixBitmapConverter.BlitThumbnail(landscape, landscapeThumb, maxEdge: 160);
        Assert.True(ReferenceEquals(landscapeThumb, reusedLandscapeThumb));

        using WriteableBitmap portraitThumb = MatrixBitmapConverter.BlitThumbnail(portrait, maxEdge: 150);
        Assert.Equal(new PixelSize(60, 150), portraitThumb.PixelSize);

        using WriteableBitmap nonSquareThumb = MatrixBitmapConverter.BlitThumbnail(nonSquare, maxEdge: 160);
        Assert.Equal(new PixelSize(160, 120), nonSquareThumb.PixelSize);

        using WriteableBitmap smallThumb = MatrixBitmapConverter.BlitThumbnail(small, maxEdge: 160);
        Assert.Equal(new PixelSize(64, 48), smallThumb.PixelSize);
        AssertBgraPixelsMatch(smallThumb, small);
    }

    [Fact]
    public void Converter_InvalidArguments_ThrowExpectedExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => MatrixBitmapConverter.Blit(null!));
        Assert.Throws<ArgumentException>(() => MatrixBitmapConverter.ComputeThumbnailSize(new LeiterSize(0, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MatrixBitmapConverter.ComputeThumbnailSize(new LeiterSize(10, 10), maxEdge: 0));
    }

    private static void AssertBgraPixelsMatch(WriteableBitmap bitmap, IReadOnlyMatrix<Rgb8> expected)
    {
        using ILockedFramebuffer fb = bitmap.Lock();
        Assert.Equal(expected.Width, fb.Size.Width);
        Assert.Equal(expected.Height, fb.Size.Height);
        Assert.Equal(PixelFormat.Bgra8888, fb.Format);

        byte[] row = new byte[expected.Width * 4];
        for (int y = 0; y < expected.Height; y++)
        {
            Marshal.Copy(fb.Address + ((nint)y * fb.RowBytes), row, 0, row.Length);
            for (int x = 0; x < expected.Width; x++)
            {
                Rgb8 pixel = expected[x, y];
                int offset = x * 4;
                Assert.Equal(pixel.B, row[offset]);
                Assert.Equal(pixel.G, row[offset + 1]);
                Assert.Equal(pixel.R, row[offset + 2]);
                Assert.Equal(255, row[offset + 3]);
            }
        }
    }

    private sealed class NonContiguousMatrixAdapter(SequentialMatrix<Rgb8> inner) : Matrix<Rgb8>(inner.Width, inner.Height)
    {
        public override Rgb8 GetElement(int index) => inner.GetElement(index);
        public override Rgb8 GetElement(int x, int y) => inner.GetElement(x, y);
        public override void SetElement(int index, Rgb8 value) => inner.SetElement(index, value);
        public override void SetElement(int x, int y, Rgb8 value) => inner.SetElement(x, y, value);
        public override void SetAll(Rgb8 value) => inner.SetAll(value);
        public override Matrix<R> Map<R>(Func<Rgb8, R> func) => inner.Map(func);
        public override Matrix<Rgb8> Clone() => inner.Clone();
    }
}
