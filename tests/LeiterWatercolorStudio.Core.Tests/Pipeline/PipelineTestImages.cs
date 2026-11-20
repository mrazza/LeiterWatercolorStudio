using Leiter.Core;
using Leiter.Pixels;

namespace LeiterWatercolorStudio.Core.Tests.Pipeline;

internal static class PipelineTestImages
{
    public static SequentialMatrix<Rgb8> CreateSyntheticScene(int width, int height)
    {
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inCenterBlock = x >= width / 4 && x < (3 * width) / 4 && y >= height / 4 && y < (3 * height) / 4;
                if (inCenterBlock)
                {
                    byte r = (byte)Math.Clamp(190 + ((x * 40) / Math.Max(1, width)), 0, 255);
                    byte g = (byte)Math.Clamp(60 + ((y * 50) / Math.Max(1, height)), 0, 255);
                    byte b = 45;
                    matrix[x, y] = new Rgb8(r, g, b);
                }
                else
                {
                    byte r = (byte)Math.Clamp(40 + ((x * 30) / Math.Max(1, width)), 0, 255);
                    byte g = (byte)Math.Clamp(95 + ((y * 60) / Math.Max(1, height)), 0, 255);
                    byte b = (byte)Math.Clamp(170 + (((x + y) * 30) / Math.Max(1, width + height)), 0, 255);
                    matrix[x, y] = new Rgb8(r, g, b);
                }
            }
        }

        return matrix;
    }
}
