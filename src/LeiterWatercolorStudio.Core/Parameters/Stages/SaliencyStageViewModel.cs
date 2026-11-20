using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Parameters.Stages;

/// <summary>
/// View model for Stage 2 (Saliency: pre-blur, EGBI, Regional Contrast, SaliencyCut/GrabCut/Mask, and Distance Field).
/// </summary>
public sealed class SaliencyStageViewModel : StageViewModelBase
{
    private double _egbiBlurSigma;

    /// <summary>
    /// Initializes a new <see cref="SaliencyStageViewModel"/> with default Stage 2 options.
    /// </summary>
    public SaliencyStageViewModel()
    {
        WatercolorOptions defaults = WatercolorOptions.Default;
        _egbiBlurSigma = defaults.EgbiBlurSigma;

        Egbi = new EgbiOptionsViewModel(defaults.Egbi);
        RegionalContrastSaliency = new RegionalContrastSaliencyOptionsViewModel(defaults.RegionalContrastSaliency);
        SaliencyCut = new SaliencyCutOptionsViewModel(defaults.SaliencyCut);
        GrabCut = new GrabCutOptionsViewModel(defaults.SaliencyCut.GrabCut);
        SaliencyMask = new SaliencyMaskOptionsViewModel(defaults.SaliencyCut.Mask);
        SaliencyDistanceField = new SaliencyDistanceFieldOptionsViewModel(defaults.SaliencyDistanceField);

        SubscribeChild(Egbi);
        SubscribeChild(RegionalContrastSaliency);
        SubscribeChild(SaliencyCut);
        SubscribeChild(GrabCut);
        SubscribeChild(SaliencyMask);
        SubscribeChild(SaliencyDistanceField);
    }

    /// <summary>
    /// Gets or sets the Gaussian pre-blur standard deviation applied before Stage 2 EGBI (<c>&gt;= 0.0</c>).
    /// </summary>
    public double EgbiBlurSigma
    {
        get => _egbiBlurSigma;
        set => SetNonNegativeDouble(ref _egbiBlurSigma, value);
    }

    /// <summary>
    /// Gets the Stage 2 EGBI segmentation view model.
    /// </summary>
    public EgbiOptionsViewModel Egbi { get; }

    /// <summary>
    /// Gets the Stage 2 regional contrast saliency view model.
    /// </summary>
    public RegionalContrastSaliencyOptionsViewModel RegionalContrastSaliency { get; }

    /// <summary>
    /// Gets the Stage 2 SaliencyCut outer loop and working-dimension view model.
    /// </summary>
    public SaliencyCutOptionsViewModel SaliencyCut { get; }

    /// <summary>
    /// Gets the Stage 2 inner GrabCut solver view model.
    /// </summary>
    public GrabCutOptionsViewModel GrabCut { get; }

    /// <summary>
    /// Gets the Stage 2 saliency thresholding and connected-component mask view model.
    /// </summary>
    public SaliencyMaskOptionsViewModel SaliencyMask { get; }

    /// <summary>
    /// Gets the Stage 2 saliency distance field smoothing view model.
    /// </summary>
    public SaliencyDistanceFieldOptionsViewModel SaliencyDistanceField { get; }

    /// <summary>
    /// Loads all Stage 2 sub-stage options.
    /// </summary>
    public void LoadFrom(
        double egbiBlurSigma,
        EgbiOptions egbi,
        RegionalContrastSaliencyOptions regionalContrastSaliency,
        SaliencyCutOptions saliencyCut,
        SaliencyDistanceFieldOptions saliencyDistanceField)
    {
        ArgumentNullException.ThrowIfNull(egbi);
        ArgumentNullException.ThrowIfNull(regionalContrastSaliency);
        ArgumentNullException.ThrowIfNull(saliencyCut);
        ArgumentNullException.ThrowIfNull(saliencyDistanceField);

        egbi.Validate();
        regionalContrastSaliency.Validate();
        saliencyCut.Validate();
        saliencyDistanceField.Validate();

        using (SuppressNotifications())
        {
            SetNonNegativeDouble(ref _egbiBlurSigma, egbiBlurSigma, propertyName: nameof(EgbiBlurSigma));
            Egbi.LoadFrom(egbi);
            RegionalContrastSaliency.LoadFrom(regionalContrastSaliency);
            SaliencyCut.LoadFrom(saliencyCut);
            GrabCut.LoadFrom(saliencyCut.GrabCut);
            SaliencyMask.LoadFrom(saliencyCut.Mask);
            SaliencyDistanceField.LoadFrom(saliencyDistanceField);
        }
    }

    /// <summary>
    /// Builds a validated <see cref="SaliencyCutOptions"/> combining <see cref="SaliencyCut"/>, <see cref="GrabCut"/>, and <see cref="SaliencyMask"/>.
    /// </summary>
    public SaliencyCutOptions ToSaliencyCutOptions() =>
        SaliencyCut.ToOptions(GrabCut.ToOptions(), SaliencyMask.ToOptions());
}

/// <summary>
/// Observable view model for <see cref="EgbiOptions"/>.
/// </summary>
public sealed class EgbiOptionsViewModel : StageViewModelBase
{
    private double _kFactor;
    private double _epsilon;
    private int _minRegionSize;
    private GridConnectivity _connectivity;

    internal EgbiOptionsViewModel(EgbiOptions initial)
    {
        _kFactor = initial.KFactor;
        _epsilon = initial.Epsilon;
        _minRegionSize = initial.MinRegionSize;
        _connectivity = initial.Connectivity;
    }

    /// <summary>
    /// Gets or sets the scale factor controlling preference for larger segments (<c>&gt;= 0.0</c>).
    /// </summary>
    public double KFactor
    {
        get => _kFactor;
        set => SetNonNegativeDouble(ref _kFactor, value);
    }

    /// <summary>
    /// Gets or sets the minimum color merge distance threshold (<c>&gt;= 0.0</c>).
    /// </summary>
    public double Epsilon
    {
        get => _epsilon;
        set => SetNonNegativeDouble(ref _epsilon, value);
    }

    /// <summary>
    /// Gets or sets the minimum region pixel count (<c>&gt;= 0</c>).
    /// </summary>
    public int MinRegionSize
    {
        get => _minRegionSize;
        set => SetClampedInt(ref _minRegionSize, value, 0);
    }

    /// <summary>
    /// Gets or sets the pixel neighborhood connectivity.
    /// </summary>
    public GridConnectivity Connectivity
    {
        get => _connectivity;
        set => SetEnum(ref _connectivity, value);
    }

    /// <summary>
    /// Loads properties from <paramref name="options"/>.
    /// </summary>
    public void LoadFrom(EgbiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _kFactor, options.KFactor, nameof(KFactor));
            SetProperty(ref _epsilon, options.Epsilon, nameof(Epsilon));
            SetProperty(ref _minRegionSize, options.MinRegionSize, nameof(MinRegionSize));
            SetProperty(ref _connectivity, options.Connectivity, nameof(Connectivity));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="EgbiOptions"/> instance.
    /// </summary>
    public EgbiOptions ToOptions()
    {
        var options = new EgbiOptions(_kFactor, _epsilon, _minRegionSize, _connectivity);
        options.Validate();
        return options;
    }
}

/// <summary>
/// Observable view model for <see cref="RegionalContrastSaliencyOptions"/>.
/// </summary>
public sealed class RegionalContrastSaliencyOptionsViewModel : StageViewModelBase
{
    private bool _enableSmoothing;
    private bool _computeBorderRegions;
    private double _centerPriorWeight;

    internal RegionalContrastSaliencyOptionsViewModel(RegionalContrastSaliencyOptions initial)
    {
        _enableSmoothing = initial.EnableSmoothing;
        _computeBorderRegions = initial.ComputeBorderRegions;
        _centerPriorWeight = initial.CenterPriorWeight;
    }

    /// <summary>
    /// Gets or sets whether feature-space saliency smoothing is enabled.
    /// </summary>
    public bool EnableSmoothing
    {
        get => _enableSmoothing;
        set => SetToggle(ref _enableSmoothing, value);
    }

    /// <summary>
    /// Gets or sets whether border regions are attenuated.
    /// </summary>
    public bool ComputeBorderRegions
    {
        get => _computeBorderRegions;
        set => SetToggle(ref _computeBorderRegions, value);
    }

    /// <summary>
    /// Gets or sets the exponential center-prior weight (<c>&gt;= 0.0</c>).
    /// </summary>
    public double CenterPriorWeight
    {
        get => _centerPriorWeight;
        set => SetNonNegativeDouble(ref _centerPriorWeight, value);
    }

    /// <summary>
    /// Loads properties from <paramref name="options"/>.
    /// </summary>
    public void LoadFrom(RegionalContrastSaliencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _enableSmoothing, options.EnableSmoothing, nameof(EnableSmoothing));
            SetProperty(ref _computeBorderRegions, options.ComputeBorderRegions, nameof(ComputeBorderRegions));
            SetProperty(ref _centerPriorWeight, options.CenterPriorWeight, nameof(CenterPriorWeight));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="RegionalContrastSaliencyOptions"/> instance.
    /// </summary>
    public RegionalContrastSaliencyOptions ToOptions()
    {
        var options = new RegionalContrastSaliencyOptions(_enableSmoothing, _computeBorderRegions, _centerPriorWeight);
        options.Validate();
        return options;
    }
}

/// <summary>
/// Observable view model for <see cref="SaliencyCutOptions"/> outer iteration and resolution properties.
/// </summary>
public sealed class SaliencyCutOptionsViewModel : StageViewModelBase
{
    private const int DefaultWorkingDimension = 1024;

    private int _maxOuterIterations;
    private int _trimapBandRadius;
    private bool _useMaxWorkingDimension;
    private int _maxWorkingDimension;

    internal SaliencyCutOptionsViewModel(SaliencyCutOptions initial)
    {
        _maxOuterIterations = initial.MaxOuterIterations;
        _trimapBandRadius = initial.TrimapBandRadius;
        _useMaxWorkingDimension = initial.MaxWorkingDimension.HasValue;
        _maxWorkingDimension = initial.MaxWorkingDimension ?? DefaultWorkingDimension;
    }

    /// <summary>
    /// Gets or sets the maximum number of trimap refinement iterations (<c>&gt;= 1</c>).
    /// </summary>
    public int MaxOuterIterations
    {
        get => _maxOuterIterations;
        set => SetClampedInt(ref _maxOuterIterations, value, 1);
    }

    /// <summary>
    /// Gets or sets the trimap dilation/erosion radius in pixels (<c>&gt;= 1</c>).
    /// </summary>
    public int TrimapBandRadius
    {
        get => _trimapBandRadius;
        set => SetClampedInt(ref _trimapBandRadius, value, 1);
    }

    /// <summary>
    /// Gets or sets whether <see cref="MaxWorkingDimension"/> bounds SaliencyCut working resolution.
    /// </summary>
    public bool UseMaxWorkingDimension
    {
        get => _useMaxWorkingDimension;
        set => SetToggle(ref _useMaxWorkingDimension, value);
    }

    /// <summary>
    /// Gets or sets the maximum working dimension used when <see cref="UseMaxWorkingDimension"/> is enabled (<c>&gt;= 2</c>).
    /// </summary>
    public int MaxWorkingDimension
    {
        get => _maxWorkingDimension;
        set => SetClampedInt(ref _maxWorkingDimension, value, 2);
    }

    /// <summary>
    /// Loads outer SaliencyCut properties from <paramref name="options"/>.
    /// </summary>
    public void LoadFrom(SaliencyCutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _maxOuterIterations, options.MaxOuterIterations, nameof(MaxOuterIterations));
            SetProperty(ref _trimapBandRadius, options.TrimapBandRadius, nameof(TrimapBandRadius));
            SetProperty(ref _useMaxWorkingDimension, options.MaxWorkingDimension.HasValue, nameof(UseMaxWorkingDimension));
            if (options.MaxWorkingDimension is int dim)
            {
                SetProperty(ref _maxWorkingDimension, dim, nameof(MaxWorkingDimension));
            }
        }
    }

    /// <summary>
    /// Builds a validated <see cref="SaliencyCutOptions"/> using the provided inner <paramref name="grabCut"/> and <paramref name="mask"/> options.
    /// </summary>
    public SaliencyCutOptions ToOptions(GrabCutOptions grabCut, SaliencyMaskOptions mask)
    {
        ArgumentNullException.ThrowIfNull(grabCut);
        ArgumentNullException.ThrowIfNull(mask);

        var options = new SaliencyCutOptions(
            _maxOuterIterations,
            _trimapBandRadius,
            grabCut,
            mask,
            _useMaxWorkingDimension ? _maxWorkingDimension : null);
        options.Validate();
        return options;
    }
}

/// <summary>
/// Observable view model for <see cref="GrabCutOptions"/>.
/// </summary>
public sealed class GrabCutOptionsViewModel : StageViewModelBase
{
    private int _iterations;
    private int _mixtureComponentCount;
    private double _smoothnessWeight;

    internal GrabCutOptionsViewModel(GrabCutOptions initial)
    {
        _iterations = initial.Iterations;
        _mixtureComponentCount = initial.MixtureComponentCount;
        _smoothnessWeight = initial.SmoothnessWeight;
    }

    /// <summary>
    /// Gets or sets the number of alternating GMM-update and graph-cut iterations (<c>&gt;= 1</c>).
    /// </summary>
    public int Iterations
    {
        get => _iterations;
        set => SetClampedInt(ref _iterations, value, 1);
    }

    /// <summary>
    /// Gets or sets the number of Gaussian mixture components per foreground/background model (<c>&gt;= 1</c>).
    /// </summary>
    public int MixtureComponentCount
    {
        get => _mixtureComponentCount;
        set => SetClampedInt(ref _mixtureComponentCount, value, 1);
    }

    /// <summary>
    /// Gets or sets the pairwise boundary smoothness weight (<c>&gt;= 0.0</c>).
    /// </summary>
    public double SmoothnessWeight
    {
        get => _smoothnessWeight;
        set => SetNonNegativeDouble(ref _smoothnessWeight, value);
    }

    /// <summary>
    /// Loads properties from <paramref name="options"/>.
    /// </summary>
    public void LoadFrom(GrabCutOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _iterations, options.Iterations, nameof(Iterations));
            SetProperty(ref _mixtureComponentCount, options.MixtureComponentCount, nameof(MixtureComponentCount));
            SetProperty(ref _smoothnessWeight, options.SmoothnessWeight, nameof(SmoothnessWeight));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="GrabCutOptions"/> instance.
    /// </summary>
    public GrabCutOptions ToOptions()
    {
        var options = new GrabCutOptions(_iterations, _mixtureComponentCount, _smoothnessWeight);
        options.Validate();
        return options;
    }
}

/// <summary>
/// Observable view model for <see cref="SaliencyMaskOptions"/>.
/// </summary>
public sealed class SaliencyMaskOptionsViewModel : StageViewModelBase
{
    private SaliencyThresholdMethod _method;
    private double _fixedThreshold;
    private ComponentRetentionPolicy _retentionPolicy;
    private double _minAreaFraction;
    private GridConnectivity _connectivity;

    internal SaliencyMaskOptionsViewModel(SaliencyMaskOptions initial)
    {
        _method = initial.Method;
        _fixedThreshold = initial.FixedThreshold;
        _retentionPolicy = initial.RetentionPolicy;
        _minAreaFraction = initial.MinAreaFraction;
        _connectivity = initial.Connectivity;
    }

    /// <summary>
    /// Gets or sets the saliency thresholding method.
    /// </summary>
    public SaliencyThresholdMethod Method
    {
        get => _method;
        set => SetEnum(ref _method, value);
    }

    /// <summary>
    /// Gets or sets the fixed threshold in <c>[0.0, 1.0]</c> used when <see cref="Method"/> is <see cref="SaliencyThresholdMethod.Fixed"/>.
    /// </summary>
    public double FixedThreshold
    {
        get => _fixedThreshold;
        set => SetFiniteDouble(ref _fixedThreshold, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the connected-component retention policy.
    /// </summary>
    public ComponentRetentionPolicy RetentionPolicy
    {
        get => _retentionPolicy;
        set => SetEnum(ref _retentionPolicy, value);
    }

    /// <summary>
    /// Gets or sets the minimum connected-component area fraction in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double MinAreaFraction
    {
        get => _minAreaFraction;
        set => SetFiniteDouble(ref _minAreaFraction, value, 0.0, 1.0);
    }

    /// <summary>
    /// Gets or sets the neighborhood connectivity for component extraction.
    /// </summary>
    public GridConnectivity Connectivity
    {
        get => _connectivity;
        set => SetEnum(ref _connectivity, value);
    }

    /// <summary>
    /// Loads properties from <paramref name="options"/>.
    /// </summary>
    public void LoadFrom(SaliencyMaskOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _method, options.Method, nameof(Method));
            SetProperty(ref _fixedThreshold, options.FixedThreshold, nameof(FixedThreshold));
            SetProperty(ref _retentionPolicy, options.RetentionPolicy, nameof(RetentionPolicy));
            SetProperty(ref _minAreaFraction, options.MinAreaFraction, nameof(MinAreaFraction));
            SetProperty(ref _connectivity, options.Connectivity, nameof(Connectivity));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="SaliencyMaskOptions"/> instance.
    /// </summary>
    public SaliencyMaskOptions ToOptions()
    {
        var options = new SaliencyMaskOptions(_method, _fixedThreshold, _retentionPolicy, _minAreaFraction, _connectivity);
        options.Validate();
        return options;
    }
}

/// <summary>
/// Observable view model for <see cref="SaliencyDistanceFieldOptions"/>.
/// </summary>
public sealed class SaliencyDistanceFieldOptionsViewModel : StageViewModelBase
{
    private const double DefaultExplicitSmoothingSigma = 12.0;

    private bool _useExplicitSmoothingSigma;
    private double _smoothingSigma;
    private double _defaultSigmaScale;

    internal SaliencyDistanceFieldOptionsViewModel(SaliencyDistanceFieldOptions initial)
    {
        _useExplicitSmoothingSigma = initial.SmoothingSigma.HasValue;
        _smoothingSigma = initial.SmoothingSigma ?? DefaultExplicitSmoothingSigma;
        _defaultSigmaScale = initial.DefaultSigmaScale;
    }

    /// <summary>
    /// Gets or sets whether an explicit <see cref="SmoothingSigma"/> overrides <see cref="DefaultSigmaScale"/>.
    /// </summary>
    public bool UseExplicitSmoothingSigma
    {
        get => _useExplicitSmoothingSigma;
        set => SetToggle(ref _useExplicitSmoothingSigma, value);
    }

    /// <summary>
    /// Gets or sets the explicit Gaussian smoothing standard deviation (<c>&gt;= 0.0</c>).
    /// </summary>
    public double SmoothingSigma
    {
        get => _smoothingSigma;
        set => SetNonNegativeDouble(ref _smoothingSigma, value);
    }

    /// <summary>
    /// Gets or sets the fraction of <c>min(Width, Height)</c> used when <see cref="UseExplicitSmoothingSigma"/> is disabled (<c>&gt;= 0.0</c>).
    /// </summary>
    public double DefaultSigmaScale
    {
        get => _defaultSigmaScale;
        set => SetNonNegativeDouble(ref _defaultSigmaScale, value);
    }

    /// <summary>
    /// Loads properties from <paramref name="options"/>.
    /// </summary>
    public void LoadFrom(SaliencyDistanceFieldOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _useExplicitSmoothingSigma, options.SmoothingSigma.HasValue, nameof(UseExplicitSmoothingSigma));
            if (options.SmoothingSigma is double explicitSigma)
            {
                SetProperty(ref _smoothingSigma, explicitSigma, nameof(SmoothingSigma));
            }

            SetProperty(ref _defaultSigmaScale, options.DefaultSigmaScale, nameof(DefaultSigmaScale));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="SaliencyDistanceFieldOptions"/> instance.
    /// </summary>
    public SaliencyDistanceFieldOptions ToOptions()
    {
        var options = new SaliencyDistanceFieldOptions(
            _useExplicitSmoothingSigma ? _smoothingSigma : null,
            _defaultSigmaScale);
        options.Validate();
        return options;
    }
}
