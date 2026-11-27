using System.Globalization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Pixels;

namespace LeiterWatercolorStudio.Core.Parameters.Stages;

/// <summary>
/// View model for Stage 5 (Hand Tremor boundary perturbation and paper gap rendering).
/// </summary>
public sealed class HandTremorStageViewModel : StageViewModelBase
{
    private bool _baselineEnableHandTremor;
    private HandTremorOptions _baseline;
    private bool _enableHandTremor;
    private double _amplitude;
    private double _salientAmplitudeScale;
    private double _similarHueThresholdDegrees;
    private double _overlapDarkening;
    private byte _paperColorR;
    private byte _paperColorG;
    private byte _paperColorB;
    private string _paperColorHex;
    private Rgb8 _paperColor;
    private double _minContrastingChroma;
    private double _paperGapBias;
    private double _paperGapOpacity;

    /// <summary>
    /// Initializes a new <see cref="HandTremorStageViewModel"/> with default Stage 5 options.
    /// </summary>
    public HandTremorStageViewModel()
    {
        WatercolorOptions defaults = WatercolorOptions.Default;
        HandTremorOptions tremor = defaults.HandTremor;

        _baselineEnableHandTremor = defaults.EnableHandTremor;
        _baseline = tremor;
        _enableHandTremor = defaults.EnableHandTremor;
        _amplitude = tremor.Amplitude;
        _salientAmplitudeScale = tremor.SalientAmplitudeScale;
        _similarHueThresholdDegrees = tremor.SimilarHueThresholdDegrees;
        _overlapDarkening = tremor.OverlapDarkening;
        _paperColor = tremor.PaperColor;
        _paperColorR = tremor.PaperColor.R;
        _paperColorG = tremor.PaperColor.G;
        _paperColorB = tremor.PaperColor.B;
        _paperColorHex = FormatHex(tremor.PaperColor);
        _minContrastingChroma = tremor.MinContrastingChroma;
        _paperGapBias = tremor.PaperGapBias;
        _paperGapOpacity = tremor.PaperGapOpacity;

        PrimaryNoise = new PerlinNoiseViewModel(tremor.PrimaryNoise);
        SecondaryNoise = new PerlinNoiseViewModel(tremor.SecondaryNoise);
        SubscribeChild(PrimaryNoise);
        SubscribeChild(SecondaryNoise);
    }

    /// <summary>
    /// Gets the baseline <see cref="HandTremorOptions"/> used for per-parameter resets.
    /// </summary>
    public HandTremorOptions Baseline => _baseline;

    /// <summary>
    /// Gets the baseline enabled state for Stage 5 hand tremor perturbation.
    /// </summary>
    public bool BaselineEnableHandTremor => _baselineEnableHandTremor;

    /// <summary>
    /// Gets or sets whether Stage 5 hand tremor perturbation is enabled.
    /// </summary>
    public bool EnableHandTremor
    {
        get => _enableHandTremor;
        set => SetToggle(ref _enableHandTremor, value);
    }

    /// <summary>
    /// Gets or sets the maximum displacement amplitude in pixels (<c>&gt;= 0.0</c>).
    /// </summary>
    public double Amplitude
    {
        get => _amplitude;
        set => SetNonNegativeDouble(ref _amplitude, value);
    }

    /// <summary>
    /// Gets or sets the displacement amplitude scale inside salient regions in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double SalientAmplitudeScale
    {
        get => _salientAmplitudeScale;
        set => SetFiniteDouble(ref _salientAmplitudeScale, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the hue threshold in degrees separating similar-hue from contrasting boundaries in <c>[0.0, 180.0]</c>.
    /// </summary>
    public double SimilarHueThresholdDegrees
    {
        get => _similarHueThresholdDegrees;
        set => SetFiniteDouble(ref _similarHueThresholdDegrees, value, 0.0, 180.0);
    }

    /// <summary>
    /// Gets or sets the overlap darkening blend factor in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double OverlapDarkening
    {
        get => _overlapDarkening;
        set => SetFiniteDouble(ref _overlapDarkening, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the red channel of <see cref="PaperColor"/>.
    /// </summary>
    public byte PaperColorR
    {
        get => _paperColorR;
        set => ApplyPaperColor(new Rgb8(value, _paperColorG, _paperColorB));
    }

    /// <summary>
    /// Gets or sets the green channel of <see cref="PaperColor"/>.
    /// </summary>
    public byte PaperColorG
    {
        get => _paperColorG;
        set => ApplyPaperColor(new Rgb8(_paperColorR, value, _paperColorB));
    }

    /// <summary>
    /// Gets or sets the blue channel of <see cref="PaperColor"/>.
    /// </summary>
    public byte PaperColorB
    {
        get => _paperColorB;
        set => ApplyPaperColor(new Rgb8(_paperColorR, _paperColorG, value));
    }

    /// <summary>
    /// Gets or sets the hexadecimal <c>#RRGGBB</c> representation of <see cref="PaperColor"/>.
    /// </summary>
    public string PaperColorHex
    {
        get => _paperColorHex;
        set
        {
            if (TryParseHexColor(value, out Rgb8 parsed))
            {
                ApplyPaperColor(parsed);
            }
            else
            {
                OnPropertyChanged(nameof(PaperColorHex));
            }
        }
    }

    /// <summary>
    /// Gets or sets the synchronized paper background color revealed in contrasting boundary gaps.
    /// </summary>
    public Rgb8 PaperColor
    {
        get => _paperColor;
        set => ApplyPaperColor(value);
    }

    /// <summary>
    /// Gets or sets the minimum normalized RGB chroma required on both sides of contrasting boundaries in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double MinContrastingChroma
    {
        get => _minContrastingChroma;
        set => SetFiniteDouble(ref _minContrastingChroma, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the normalized boundary-normal gap bias in <c>[-1.0, 1.0]</c>.
    /// </summary>
    public double PaperGapBias
    {
        get => _paperGapBias;
        set => SetFiniteDouble(ref _paperGapBias, value, -1.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the opacity in <c>[0.0, 1.0]</c> for blending <see cref="PaperColor"/> into paper gaps.
    /// </summary>
    public double PaperGapOpacity
    {
        get => _paperGapOpacity;
        set => SetFiniteDouble(ref _paperGapOpacity, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets the primary Perlin noise displacement view model.
    /// </summary>
    public PerlinNoiseViewModel PrimaryNoise { get; }

    /// <summary>
    /// Gets the secondary Perlin noise displacement view model.
    /// </summary>
    public PerlinNoiseViewModel SecondaryNoise { get; }

    /// <summary>
    /// Updates <see cref="Baseline"/> and child noise baselines without modifying current parameter values.
    /// </summary>
    public void SetBaseline(HandTremorOptions baseline) =>
        SetBaseline(_baselineEnableHandTremor, baseline);

    /// <summary>
    /// Updates <see cref="BaselineEnableHandTremor"/>, <see cref="Baseline"/>, and child noise baselines
    /// without modifying current parameter values.
    /// </summary>
    public void SetBaseline(bool enableHandTremor, HandTremorOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        SetProperty(ref _baselineEnableHandTremor, enableHandTremor, nameof(BaselineEnableHandTremor));
        SetProperty(ref _baseline, baseline, nameof(Baseline));
        PrimaryNoise.SetBaseline(baseline.PrimaryNoise);
        SecondaryNoise.SetBaseline(baseline.SecondaryNoise);
    }

    /// <summary>
    /// Loads Stage 5 parameters from pipeline options.
    /// </summary>
    public void LoadFrom(bool enableHandTremor, HandTremorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _enableHandTremor, enableHandTremor, nameof(EnableHandTremor));
            SetProperty(ref _amplitude, options.Amplitude, nameof(Amplitude));
            SetProperty(ref _salientAmplitudeScale, options.SalientAmplitudeScale, nameof(SalientAmplitudeScale));
            SetProperty(ref _similarHueThresholdDegrees, options.SimilarHueThresholdDegrees, nameof(SimilarHueThresholdDegrees));
            SetProperty(ref _overlapDarkening, options.OverlapDarkening, nameof(OverlapDarkening));
            ApplyPaperColorCore(options.PaperColor);
            SetProperty(ref _minContrastingChroma, options.MinContrastingChroma, nameof(MinContrastingChroma));
            SetProperty(ref _paperGapBias, options.PaperGapBias, nameof(PaperGapBias));
            SetProperty(ref _paperGapOpacity, options.PaperGapOpacity, nameof(PaperGapOpacity));
            PrimaryNoise.LoadFrom(options.PrimaryNoise);
            SecondaryNoise.LoadFrom(options.SecondaryNoise);
        }
    }

    /// <inheritdoc />
    public override void ResetToBaseline()
    {
        bool changed = _enableHandTremor != _baselineEnableHandTremor
            || _amplitude != _baseline.Amplitude
            || _salientAmplitudeScale != _baseline.SalientAmplitudeScale
            || _similarHueThresholdDegrees != _baseline.SimilarHueThresholdDegrees
            || _overlapDarkening != _baseline.OverlapDarkening
            || _paperColor != _baseline.PaperColor
            || _minContrastingChroma != _baseline.MinContrastingChroma
            || _paperGapBias != _baseline.PaperGapBias
            || _paperGapOpacity != _baseline.PaperGapOpacity
            || !Equals(PrimaryNoise.ToOptions(), _baseline.PrimaryNoise)
            || !Equals(SecondaryNoise.ToOptions(), _baseline.SecondaryNoise);

        LoadFrom(_baselineEnableHandTremor, _baseline);
        if (changed)
        {
            RaiseParameterChanged(isImmediate: true);
        }
    }

    /// <summary>
    /// Builds a validated <see cref="HandTremorOptions"/> instance.
    /// </summary>
    public HandTremorOptions ToOptions()
    {
        var options = new HandTremorOptions(
            _amplitude,
            _salientAmplitudeScale,
            _similarHueThresholdDegrees,
            _overlapDarkening,
            _paperColor,
            PrimaryNoise.ToOptions(),
            SecondaryNoise.ToOptions(),
            _minContrastingChroma,
            _paperGapBias,
            _paperGapOpacity);
        options.Validate();
        return options;
    }

    private void ApplyPaperColor(Rgb8 color)
    {
        if (ApplyPaperColorCore(color))
        {
            RaiseParameterChanged(isImmediate: false);
        }
    }

    private bool ApplyPaperColorCore(Rgb8 color)
    {
        string formattedHex = FormatHex(color);
        bool changed = false;

        changed |= SetProperty(ref _paperColor, color, nameof(PaperColor));
        changed |= SetProperty(ref _paperColorR, color.R, nameof(PaperColorR));
        changed |= SetProperty(ref _paperColorG, color.G, nameof(PaperColorG));
        changed |= SetProperty(ref _paperColorB, color.B, nameof(PaperColorB));
        changed |= SetProperty(ref _paperColorHex, formattedHex, nameof(PaperColorHex));

        return changed;
    }

    private static string FormatHex(Rgb8 color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static bool TryParseHexColor(string? text, out Rgb8 color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        ReadOnlySpan<char> span = text.AsSpan().Trim();
        if (span.StartsWith('#'))
        {
            span = span[1..];
        }

        if (span.Length != 6)
        {
            return false;
        }

        for (int i = 0; i < span.Length; i++)
        {
            if (!char.IsAsciiHexDigit(span[i]))
            {
                return false;
            }
        }

        if (!byte.TryParse(span[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)
            || !byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)
            || !byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        color = new Rgb8(r, g, b);
        return true;
    }
}
