using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Parameters.Stages;

/// <summary>
/// View model for Stage 6 (Finishing: Edge Darkening, Turbulence Flow, Piecewise Tone Curve,
/// Paper Granulation, and FXAA).
/// </summary>
public sealed class FinishingStageViewModel : StageViewModelBase
{
    private const double ToneMinGap = 0.001;

    private bool _enableEdgeDarkening;
    private double _edgeIntensity;
    private double _edgeBlurSigma;
    private GradientOperator _edgeGradientOperator;

    private bool _enableTurbulenceFlow;
    private double _turbulenceIntensity;

    private bool _enableToneCurve;
    private double _minTone;
    private double _maxTone;
    private double _shadowKnee;
    private double _highlightKnee;

    private bool _enablePaperGranulation;
    private double _densityStrength;
    private double _warpAmplitude;

    private bool _enableFxaa;
    private double _subpixelBlendStrength;
    private double _relativeContrastThreshold;
    private double _absoluteContrastThreshold;
    private int _maxSpanSearchSteps;

    /// <summary>
    /// Initializes a new <see cref="FinishingStageViewModel"/> with default Stage 6 options.
    /// </summary>
    public FinishingStageViewModel()
    {
        WatercolorOptions defaults = WatercolorOptions.Default;

        _enableEdgeDarkening = defaults.EnableEdgeDarkening;
        _edgeIntensity = defaults.EdgeDarkening.Intensity;
        _edgeBlurSigma = defaults.EdgeDarkening.BlurSigma;
        _edgeGradientOperator = defaults.EdgeDarkening.GradientOperator;

        _enableTurbulenceFlow = defaults.EnableTurbulenceFlow;
        _turbulenceIntensity = defaults.TurbulenceFlow.Intensity;
        TurbulenceNoise = new PerlinNoiseViewModel(defaults.TurbulenceFlow.Noise);

        _enableToneCurve = defaults.EnableToneCurve;
        _minTone = defaults.ToneCurve.MinTone;
        _maxTone = defaults.ToneCurve.MaxTone;
        _shadowKnee = defaults.ToneCurve.ShadowKnee;
        _highlightKnee = defaults.ToneCurve.HighlightKnee;

        _enablePaperGranulation = defaults.EnablePaperGranulation;
        _densityStrength = defaults.PaperGranulation.DensityStrength;
        _warpAmplitude = defaults.PaperGranulation.WarpAmplitude;
        GrainNoise = new PerlinNoiseViewModel(defaults.PaperGranulation.GrainNoise);

        _enableFxaa = defaults.EnableFxaa;
        _subpixelBlendStrength = defaults.Fxaa.SubpixelBlendStrength;
        _relativeContrastThreshold = defaults.Fxaa.RelativeContrastThreshold;
        _absoluteContrastThreshold = defaults.Fxaa.AbsoluteContrastThreshold;
        _maxSpanSearchSteps = defaults.Fxaa.MaxSpanSearchSteps;

        SubscribeChild(TurbulenceNoise);
        SubscribeChild(GrainNoise);
    }

    /// <summary>
    /// Gets or sets whether pigment edge darkening is enabled.
    /// </summary>
    public bool EnableEdgeDarkening
    {
        get => _enableEdgeDarkening;
        set => SetToggle(ref _enableEdgeDarkening, value);
    }

    /// <summary>
    /// Gets or sets the edge darkening pigment density multiplier (<c>&gt;= 0.0</c>).
    /// </summary>
    public double EdgeIntensity
    {
        get => _edgeIntensity;
        set => SetNonNegativeDouble(ref _edgeIntensity, value);
    }

    /// <summary>
    /// Gets or sets the Gaussian blur sigma applied to the edge gradient map (<c>&gt;= 0.0</c>).
    /// </summary>
    public double EdgeBlurSigma
    {
        get => _edgeBlurSigma;
        set => SetNonNegativeDouble(ref _edgeBlurSigma, value);
    }

    /// <summary>
    /// Gets or sets the spatial gradient operator kernel used for edge darkening.
    /// </summary>
    public GradientOperator EdgeGradientOperator
    {
        get => _edgeGradientOperator;
        set => SetEnum(ref _edgeGradientOperator, value);
    }

    /// <summary>
    /// Gets or sets whether low-frequency turbulence wash variation is enabled.
    /// </summary>
    public bool EnableTurbulenceFlow
    {
        get => _enableTurbulenceFlow;
        set => SetToggle(ref _enableTurbulenceFlow, value);
    }

    /// <summary>
    /// Gets or sets the turbulence flow pigment density multiplier (<c>&gt;= 0.0</c>).
    /// </summary>
    public double TurbulenceIntensity
    {
        get => _turbulenceIntensity;
        set => SetNonNegativeDouble(ref _turbulenceIntensity, value);
    }

    /// <summary>
    /// Gets the Perlin noise view model for turbulence flow synthesis.
    /// </summary>
    public PerlinNoiseViewModel TurbulenceNoise { get; }

    /// <summary>
    /// Gets or sets whether piecewise tone curve adjustment is enabled.
    /// </summary>
    public bool EnableToneCurve
    {
        get => _enableToneCurve;
        set => SetToggle(ref _enableToneCurve, value);
    }

    /// <summary>
    /// Gets or sets the output minimum tone floor in <c>[0.0, MaxTone)</c>.
    /// </summary>
    public double MinTone
    {
        get => _minTone;
        set
        {
            double maxAllowed = _maxTone > ToneMinGap ? _maxTone - ToneMinGap : _maxTone * 0.5;
            SetFiniteDouble(ref _minTone, value, 0.0, maxAllowed);
        }
    }

    /// <summary>
    /// Gets or sets the output maximum tone ceiling in <c>(MinTone, 1.0]</c>.
    /// </summary>
    public double MaxTone
    {
        get => _maxTone;
        set
        {
            double minAllowed = _minTone < 1.0 - ToneMinGap ? _minTone + ToneMinGap : (_minTone + 1.0) * 0.5;
            SetFiniteDouble(ref _maxTone, value, minAllowed, 1.0);
        }
    }

    /// <summary>
    /// Gets or sets the shadow knee breakpoint in <c>(0.0, HighlightKnee)</c>.
    /// </summary>
    public double ShadowKnee
    {
        get => _shadowKnee;
        set
        {
            double maxAllowed = _highlightKnee > 2.0 * ToneMinGap ? _highlightKnee - ToneMinGap : _highlightKnee * 0.5;
            double minAllowed = Math.Min(ToneMinGap, maxAllowed);
            SetFiniteDouble(ref _shadowKnee, value, minAllowed, maxAllowed);
        }
    }

    /// <summary>
    /// Gets or sets the highlight knee breakpoint in <c>(ShadowKnee, 1.0)</c>.
    /// </summary>
    public double HighlightKnee
    {
        get => _highlightKnee;
        set
        {
            double minAllowed = _shadowKnee < 1.0 - 2.0 * ToneMinGap ? _shadowKnee + ToneMinGap : (_shadowKnee + 1.0) * 0.5;
            double maxAllowed = Math.Max(1.0 - ToneMinGap, minAllowed);
            SetFiniteDouble(ref _highlightKnee, value, minAllowed, maxAllowed);
        }
    }

    /// <summary>
    /// Gets or sets whether paper fiber granulation and micro-warp are enabled.
    /// </summary>
    public bool EnablePaperGranulation
    {
        get => _enablePaperGranulation;
        set => SetToggle(ref _enablePaperGranulation, value);
    }

    /// <summary>
    /// Gets or sets the paper granulation pigment density multiplier (<c>&gt;= 0.0</c>).
    /// </summary>
    public double DensityStrength
    {
        get => _densityStrength;
        set => SetNonNegativeDouble(ref _densityStrength, value);
    }

    /// <summary>
    /// Gets or sets the paper fiber micro-warp displacement amplitude (<c>&gt;= 0.0</c>).
    /// </summary>
    public double WarpAmplitude
    {
        get => _warpAmplitude;
        set => SetNonNegativeDouble(ref _warpAmplitude, value);
    }

    /// <summary>
    /// Gets the Perlin noise view model for procedural paper fiber grain synthesis.
    /// </summary>
    public PerlinNoiseViewModel GrainNoise { get; }

    /// <summary>
    /// Gets or sets whether final FXAA anti-aliasing is enabled.
    /// </summary>
    public bool EnableFxaa
    {
        get => _enableFxaa;
        set => SetToggle(ref _enableFxaa, value);
    }

    /// <summary>
    /// Gets or sets the FXAA subpixel blending strength in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double SubpixelBlendStrength
    {
        get => _subpixelBlendStrength;
        set => SetFiniteDouble(ref _subpixelBlendStrength, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the FXAA relative edge contrast threshold in <c>(0.0, 1.0]</c>.
    /// </summary>
    public double RelativeContrastThreshold
    {
        get => _relativeContrastThreshold;
        set => SetPositiveDouble(ref _relativeContrastThreshold, value, max: 1.0);
    }

    /// <summary>
    /// Gets or sets the FXAA absolute minimum contrast threshold (<c>&gt;= 0.0</c>).
    /// </summary>
    public double AbsoluteContrastThreshold
    {
        get => _absoluteContrastThreshold;
        set => SetNonNegativeDouble(ref _absoluteContrastThreshold, value);
    }

    /// <summary>
    /// Gets or sets the FXAA maximum tangent edge span search steps (<c>&gt;= 1</c>).
    /// </summary>
    public int MaxSpanSearchSteps
    {
        get => _maxSpanSearchSteps;
        set => SetClampedInt(ref _maxSpanSearchSteps, value, 1);
    }

    /// <summary>
    /// Loads all Stage 6 sub-stage parameters from pipeline options.
    /// </summary>
    public void LoadFrom(
        bool enableEdgeDarkening,
        EdgeDarkeningOptions edgeDarkening,
        bool enableTurbulenceFlow,
        TurbulenceFlowOptions turbulenceFlow,
        bool enableToneCurve,
        PiecewiseToneCurveOptions toneCurve,
        bool enablePaperGranulation,
        PaperGranulationOptions paperGranulation,
        bool enableFxaa,
        FxaaOptions fxaa)
    {
        ArgumentNullException.ThrowIfNull(edgeDarkening);
        ArgumentNullException.ThrowIfNull(turbulenceFlow);
        ArgumentNullException.ThrowIfNull(toneCurve);
        ArgumentNullException.ThrowIfNull(paperGranulation);
        ArgumentNullException.ThrowIfNull(fxaa);

        edgeDarkening.Validate();
        turbulenceFlow.Validate();
        toneCurve.Validate();
        paperGranulation.Validate();
        fxaa.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _enableEdgeDarkening, enableEdgeDarkening, nameof(EnableEdgeDarkening));
            SetProperty(ref _edgeIntensity, edgeDarkening.Intensity, nameof(EdgeIntensity));
            SetProperty(ref _edgeBlurSigma, edgeDarkening.BlurSigma, nameof(EdgeBlurSigma));
            SetProperty(ref _edgeGradientOperator, edgeDarkening.GradientOperator, nameof(EdgeGradientOperator));

            SetProperty(ref _enableTurbulenceFlow, enableTurbulenceFlow, nameof(EnableTurbulenceFlow));
            SetProperty(ref _turbulenceIntensity, turbulenceFlow.Intensity, nameof(TurbulenceIntensity));
            TurbulenceNoise.LoadFrom(turbulenceFlow.Noise);

            SetProperty(ref _enableToneCurve, enableToneCurve, nameof(EnableToneCurve));
            SetProperty(ref _minTone, toneCurve.MinTone, nameof(MinTone));
            SetProperty(ref _maxTone, toneCurve.MaxTone, nameof(MaxTone));
            SetProperty(ref _shadowKnee, toneCurve.ShadowKnee, nameof(ShadowKnee));
            SetProperty(ref _highlightKnee, toneCurve.HighlightKnee, nameof(HighlightKnee));

            SetProperty(ref _enablePaperGranulation, enablePaperGranulation, nameof(EnablePaperGranulation));
            SetProperty(ref _densityStrength, paperGranulation.DensityStrength, nameof(DensityStrength));
            SetProperty(ref _warpAmplitude, paperGranulation.WarpAmplitude, nameof(WarpAmplitude));
            GrainNoise.LoadFrom(paperGranulation.GrainNoise);

            SetProperty(ref _enableFxaa, enableFxaa, nameof(EnableFxaa));
            SetProperty(ref _subpixelBlendStrength, fxaa.SubpixelBlendStrength, nameof(SubpixelBlendStrength));
            SetProperty(ref _relativeContrastThreshold, fxaa.RelativeContrastThreshold, nameof(RelativeContrastThreshold));
            SetProperty(ref _absoluteContrastThreshold, fxaa.AbsoluteContrastThreshold, nameof(AbsoluteContrastThreshold));
            SetProperty(ref _maxSpanSearchSteps, fxaa.MaxSpanSearchSteps, nameof(MaxSpanSearchSteps));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="EdgeDarkeningOptions"/> instance.
    /// </summary>
    public EdgeDarkeningOptions ToEdgeDarkeningOptions()
    {
        var options = new EdgeDarkeningOptions(_edgeIntensity, _edgeBlurSigma, _edgeGradientOperator);
        options.Validate();
        return options;
    }

    /// <summary>
    /// Builds a validated <see cref="TurbulenceFlowOptions"/> instance.
    /// </summary>
    public TurbulenceFlowOptions ToTurbulenceFlowOptions()
    {
        var options = new TurbulenceFlowOptions(_turbulenceIntensity, TurbulenceNoise.ToOptions());
        options.Validate();
        return options;
    }

    /// <summary>
    /// Builds a validated <see cref="PiecewiseToneCurveOptions"/> instance.
    /// </summary>
    public PiecewiseToneCurveOptions ToToneCurveOptions()
    {
        var options = new PiecewiseToneCurveOptions(_minTone, _maxTone, _shadowKnee, _highlightKnee);
        options.Validate();
        return options;
    }

    /// <summary>
    /// Builds a validated <see cref="PaperGranulationOptions"/> instance.
    /// </summary>
    public PaperGranulationOptions ToPaperGranulationOptions()
    {
        var options = new PaperGranulationOptions(_densityStrength, _warpAmplitude, GrainNoise.ToOptions());
        options.Validate();
        return options;
    }

    /// <summary>
    /// Builds a validated <see cref="FxaaOptions"/> instance.
    /// </summary>
    public FxaaOptions ToFxaaOptions()
    {
        var options = new FxaaOptions(
            _relativeContrastThreshold,
            _absoluteContrastThreshold,
            _subpixelBlendStrength,
            _maxSpanSearchSteps);
        options.Validate();
        return options;
    }
}
