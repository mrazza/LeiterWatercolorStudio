using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Pixels;

namespace LeiterWatercolorStudio.Core.Presets.Converters;

internal sealed class HandTremorOptionsDto
{
    public double Amplitude { get; set; } = HandTremorOptions.Default.Amplitude;
    public double SalientAmplitudeScale { get; set; } = HandTremorOptions.Default.SalientAmplitudeScale;
    public double SimilarHueThresholdDegrees { get; set; } = HandTremorOptions.Default.SimilarHueThresholdDegrees;
    public double OverlapDarkening { get; set; } = HandTremorOptions.Default.OverlapDarkening;
    public Rgb8? PaperColor { get; set; }
    public PerlinNoiseOptions? PrimaryNoise { get; set; }
    public PerlinNoiseOptions? SecondaryNoise { get; set; }
    public double MinContrastingChroma { get; set; } = HandTremorOptions.Default.MinContrastingChroma;
    public double PaperGapBias { get; set; } = HandTremorOptions.Default.PaperGapBias;
    public double PaperGapOpacity { get; set; } = HandTremorOptions.Default.PaperGapOpacity;
}
