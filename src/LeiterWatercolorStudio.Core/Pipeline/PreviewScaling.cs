using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.Core.Pipeline;

/// <summary>
/// Computes aspect-preserving preview dimensions and size-scaled preview options from canonical native options.
/// </summary>
public static class PreviewScaling
{
    /// <summary>
    /// Computes the preview dimensions for <paramref name="nativeSize"/> under <paramref name="cap"/>,
    /// preserving aspect ratio, never upscaling, and ensuring at least 1px per axis for non-empty images.
    /// </summary>
    public static Size ComputePreviewSize(Size nativeSize, PreviewResolutionCap cap)
    {
        if (nativeSize.Width < 0 || nativeSize.Height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeSize), nativeSize, "Image dimensions cannot be negative.");
        }

        if (!Enum.IsDefined(cap))
        {
            throw new ArgumentOutOfRangeException(nameof(cap), cap, "Invalid preview resolution cap.");
        }

        if (nativeSize.Width == 0 || nativeSize.Height == 0 || cap == PreviewResolutionCap.Native)
        {
            return nativeSize;
        }

        int maxCap = (int)cap;
        int maxDim = Math.Max(nativeSize.Width, nativeSize.Height);
        if (maxDim <= maxCap)
        {
            return nativeSize;
        }

        double scale = (double)maxCap / maxDim;
        if (nativeSize.Width >= nativeSize.Height)
        {
            int height = Math.Max(1, (int)Math.Round(nativeSize.Height * scale, MidpointRounding.AwayFromZero));
            return new Size(maxCap, height);
        }

        int width = Math.Max(1, (int)Math.Round(nativeSize.Width * scale, MidpointRounding.AwayFromZero));
        return new Size(width, maxCap);
    }

    /// <summary>
    /// Computes effective options for <paramref name="previewSize"/> scaled from <paramref name="canonical"/>
    /// calibrated at <paramref name="nativeSize"/> when downscaled; otherwise returns <paramref name="canonical"/>.
    /// </summary>
    public static WatercolorOptions ComputeEffectivePreviewOptions(
        WatercolorOptions canonical,
        Size nativeSize,
        Size previewSize)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        if (nativeSize.Width < 0 || nativeSize.Height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeSize), nativeSize, "Native dimensions cannot be negative.");
        }

        if (previewSize.Width < 0 || previewSize.Height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(previewSize), previewSize, "Preview dimensions cannot be negative.");
        }

        canonical.Validate();

        if (nativeSize.Width == 0 ||
            nativeSize.Height == 0 ||
            previewSize.Width == 0 ||
            previewSize.Height == 0 ||
            (previewSize.Width >= nativeSize.Width && previewSize.Height >= nativeSize.Height))
        {
            return canonical;
        }

        double nativeReferenceDimension = Math.Sqrt((double)nativeSize.Width * nativeSize.Height);
        return WatercolorOptions.ForSize(previewSize, canonical, nativeReferenceDimension);
    }
}
