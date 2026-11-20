using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Parameters.Stages;

/// <summary>
/// View model for Stage 4 (Wet-in-Wet watercolor edge bleeding).
/// </summary>
public sealed class WetInWetStageViewModel : StageViewModelBase
{
    private bool _enableWetInWet;
    private WetInWetMode _mode;
    private double _colorGradientThreshold;
    private double _salientHueThresholdDegrees;
    private double _nonSalientHueThresholdDegrees;
    private double _minIntensityDifference;
    private double _salientOverlapThreshold;
    private int _scatterRadius;
    private double _seedProbability;
    private int _seed;
    private double _ellipticalMajorRadius;
    private double _ellipticalMinorRadius;
    private double _ellipticalSigmaScale;

    /// <summary>
    /// Initializes a new <see cref="WetInWetStageViewModel"/> with default Stage 4 options.
    /// </summary>
    public WetInWetStageViewModel()
    {
        WatercolorOptions defaults = WatercolorOptions.Default;
        WetInWetOptions wet = defaults.WetInWet;
        _enableWetInWet = defaults.EnableWetInWet;
        _mode = wet.Mode;
        _colorGradientThreshold = wet.ColorGradientThreshold;
        _salientHueThresholdDegrees = wet.SalientHueThresholdDegrees;
        _nonSalientHueThresholdDegrees = wet.NonSalientHueThresholdDegrees;
        _minIntensityDifference = wet.MinIntensityDifference;
        _salientOverlapThreshold = wet.SalientOverlapThreshold;
        _scatterRadius = wet.ScatterRadius;
        _seedProbability = wet.SeedProbability;
        _seed = wet.Seed;
        _ellipticalMajorRadius = wet.EllipticalFilter.MajorRadius;
        _ellipticalMinorRadius = wet.EllipticalFilter.MinorRadius;
        _ellipticalSigmaScale = wet.EllipticalFilter.SigmaScale;
    }

    /// <summary>
    /// Gets or sets whether Stage 4 wet-in-wet edge bleeding is enabled.
    /// </summary>
    public bool EnableWetInWet
    {
        get => _enableWetInWet;
        set => SetToggle(ref _enableWetInWet, value);
    }

    /// <summary>
    /// Gets or sets the boundary filtering mode.
    /// </summary>
    public WetInWetMode Mode
    {
        get => _mode;
        set => SetEnum(ref _mode, value);
    }

    /// <summary>
    /// Gets or sets the region-averaged color gradient magnitude threshold (<c>&gt;= 0.0</c>).
    /// </summary>
    public double ColorGradientThreshold
    {
        get => _colorGradientThreshold;
        set => SetNonNegativeDouble(ref _colorGradientThreshold, value);
    }

    /// <summary>
    /// Gets or sets the maximum hue difference in degrees for salient boundaries in <c>[0.0, 180.0]</c>.
    /// </summary>
    public double SalientHueThresholdDegrees
    {
        get => _salientHueThresholdDegrees;
        set => SetFiniteDouble(ref _salientHueThresholdDegrees, value, 0.0, 180.0);
    }

    /// <summary>
    /// Gets or sets the maximum hue difference in degrees for non-salient boundaries in <c>[0.0, 180.0]</c>.
    /// </summary>
    public double NonSalientHueThresholdDegrees
    {
        get => _nonSalientHueThresholdDegrees;
        set => SetFiniteDouble(ref _nonSalientHueThresholdDegrees, value, 0.0, 180.0);
    }

    /// <summary>
    /// Gets or sets the minimum HSI intensity difference between adjacent regions (<c>&gt;= 0.0</c>).
    /// </summary>
    public double MinIntensityDifference
    {
        get => _minIntensityDifference;
        set => SetNonNegativeDouble(ref _minIntensityDifference, value);
    }

    /// <summary>
    /// Gets or sets the minimum salient pixel overlap fraction in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double SalientOverlapThreshold
    {
        get => _salientOverlapThreshold;
        set => SetFiniteDouble(ref _salientOverlapThreshold, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the maximum pixel distance from qualifying boundaries for seed scattering (<c>&gt;= 1</c>).
    /// </summary>
    public int ScatterRadius
    {
        get => _scatterRadius;
        set => SetClampedInt(ref _scatterRadius, value, 1);
    }

    /// <summary>
    /// Gets or sets the probability in <c>[0.0, 1.0]</c> of depositing a pigment seed at a candidate pixel.
    /// </summary>
    public double SeedProbability
    {
        get => _seedProbability;
        set => SetFiniteDouble(ref _seedProbability, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the pseudo-random seed used for pigment seed scattering.
    /// </summary>
    public int Seed
    {
        get => _seed;
        set
        {
            if (SetProperty(ref _seed, value))
            {
                RaiseParameterChanged(isImmediate: false);
            }
        }
    }

    /// <summary>
    /// Gets or sets the semi-major radius of the oriented elliptical Gaussian filter (<c>&gt; 0.0</c>).
    /// </summary>
    public double EllipticalMajorRadius
    {
        get => _ellipticalMajorRadius;
        set => SetPositiveDouble(ref _ellipticalMajorRadius, value);
    }

    /// <summary>
    /// Gets or sets the semi-minor radius of the oriented elliptical Gaussian filter (<c>&gt; 0.0</c>).
    /// </summary>
    public double EllipticalMinorRadius
    {
        get => _ellipticalMinorRadius;
        set => SetPositiveDouble(ref _ellipticalMinorRadius, value);
    }

    /// <summary>
    /// Gets or sets the Gaussian sigma scale relative to the elliptical filter radii (<c>&gt; 0.0</c>).
    /// </summary>
    public double EllipticalSigmaScale
    {
        get => _ellipticalSigmaScale;
        set => SetPositiveDouble(ref _ellipticalSigmaScale, value);
    }

    /// <summary>
    /// Assigns a new random <see cref="Seed"/> and raises an immediate parameter change.
    /// </summary>
    public void RandomizeSeed()
    {
        if (SetProperty(ref _seed, NextDistinctSeed(_seed), nameof(Seed)))
        {
            RaiseParameterChanged(isImmediate: true);
        }
    }

    /// <summary>
    /// Loads Stage 4 parameters from pipeline options.
    /// </summary>
    public void LoadFrom(bool enableWetInWet, WetInWetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _enableWetInWet, enableWetInWet, nameof(EnableWetInWet));
            SetProperty(ref _mode, options.Mode, nameof(Mode));
            SetProperty(ref _colorGradientThreshold, options.ColorGradientThreshold, nameof(ColorGradientThreshold));
            SetProperty(ref _salientHueThresholdDegrees, options.SalientHueThresholdDegrees, nameof(SalientHueThresholdDegrees));
            SetProperty(ref _nonSalientHueThresholdDegrees, options.NonSalientHueThresholdDegrees, nameof(NonSalientHueThresholdDegrees));
            SetProperty(ref _minIntensityDifference, options.MinIntensityDifference, nameof(MinIntensityDifference));
            SetProperty(ref _salientOverlapThreshold, options.SalientOverlapThreshold, nameof(SalientOverlapThreshold));
            SetProperty(ref _scatterRadius, options.ScatterRadius, nameof(ScatterRadius));
            SetProperty(ref _seedProbability, options.SeedProbability, nameof(SeedProbability));
            SetProperty(ref _seed, options.Seed, nameof(Seed));
            SetProperty(ref _ellipticalMajorRadius, options.EllipticalFilter.MajorRadius, nameof(EllipticalMajorRadius));
            SetProperty(ref _ellipticalMinorRadius, options.EllipticalFilter.MinorRadius, nameof(EllipticalMinorRadius));
            SetProperty(ref _ellipticalSigmaScale, options.EllipticalFilter.SigmaScale, nameof(EllipticalSigmaScale));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="WetInWetOptions"/> instance.
    /// </summary>
    public WetInWetOptions ToOptions()
    {
        var elliptical = new OrientedEllipticalFilterOptions(
            _ellipticalMajorRadius,
            _ellipticalMinorRadius,
            _ellipticalSigmaScale);
        var options = new WetInWetOptions(
            _mode,
            _colorGradientThreshold,
            _salientHueThresholdDegrees,
            _nonSalientHueThresholdDegrees,
            _minIntensityDifference,
            _salientOverlapThreshold,
            _scatterRadius,
            _seedProbability,
            elliptical,
            _seed);
        options.Validate();
        return options;
    }
}
