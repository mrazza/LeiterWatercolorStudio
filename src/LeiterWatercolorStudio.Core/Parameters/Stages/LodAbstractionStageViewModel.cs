using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Parameters.Stages;

/// <summary>
/// View model for Stage 3b (Saliency-guided Level-of-Detail Abstraction) with bidirectional coupling
/// between <see cref="MinNonSalientKernelSize"/> and <see cref="MaxNonSalientKernelSize"/>.
/// </summary>
public sealed class LodAbstractionStageViewModel : StageViewModelBase
{
    private bool _enableLodAbstraction;
    private int _salientKernelSize;
    private int _minNonSalientKernelSize;
    private int _maxNonSalientKernelSize;
    private double _distanceKernelScale;
    private double _distanceKernelBias;
    private double _crossSegmentColorScale;
    private double _salientOverlapThreshold;

    /// <summary>
    /// Initializes a new <see cref="LodAbstractionStageViewModel"/> with default Stage 3b options.
    /// </summary>
    public LodAbstractionStageViewModel()
    {
        WatercolorOptions defaults = WatercolorOptions.Default;
        _enableLodAbstraction = defaults.EnableLodAbstraction;
        _salientKernelSize = defaults.LodAbstraction.SalientKernelSize;
        _minNonSalientKernelSize = defaults.LodAbstraction.MinNonSalientKernelSize;
        _maxNonSalientKernelSize = defaults.LodAbstraction.MaxNonSalientKernelSize;
        _distanceKernelScale = defaults.LodAbstraction.DistanceKernelScale;
        _distanceKernelBias = defaults.LodAbstraction.DistanceKernelBias;
        _crossSegmentColorScale = defaults.LodAbstraction.CrossSegmentColorScale;
        _salientOverlapThreshold = defaults.LodAbstraction.SalientOverlapThreshold;
    }

    /// <summary>
    /// Gets or sets whether Stage 3b LOD abstraction is enabled.
    /// </summary>
    public bool EnableLodAbstraction
    {
        get => _enableLodAbstraction;
        set => SetToggle(ref _enableLodAbstraction, value);
    }

    /// <summary>
    /// Gets or sets the fixed kernel size applied within salient regions (<c>&gt;= 1</c>).
    /// </summary>
    public int SalientKernelSize
    {
        get => _salientKernelSize;
        set => SetClampedInt(ref _salientKernelSize, value, 1);
    }

    /// <summary>
    /// Gets or sets the minimum kernel size in non-salient regions (<c>&gt;= 1</c>).
    /// Automatically raises <see cref="MaxNonSalientKernelSize"/> if needed to preserve <c>Min &lt;= Max</c>.
    /// </summary>
    public int MinNonSalientKernelSize
    {
        get => _minNonSalientKernelSize;
        set
        {
            int clamped = Math.Max(1, value);
            if (!SetProperty(ref _minNonSalientKernelSize, clamped))
            {
                if (clamped != value)
                {
                    OnPropertyChanged();
                }

                return;
            }

            if (_maxNonSalientKernelSize < clamped)
            {
                SetProperty(ref _maxNonSalientKernelSize, clamped, nameof(MaxNonSalientKernelSize));
            }

            RaiseParameterChanged(isImmediate: false);
        }
    }

    /// <summary>
    /// Gets or sets the maximum kernel size in non-salient regions (<c>&gt;= 1</c>).
    /// Automatically lowers <see cref="MinNonSalientKernelSize"/> if needed to preserve <c>Min &lt;= Max</c>.
    /// </summary>
    public int MaxNonSalientKernelSize
    {
        get => _maxNonSalientKernelSize;
        set
        {
            int clamped = Math.Max(1, value);
            if (!SetProperty(ref _maxNonSalientKernelSize, clamped))
            {
                if (clamped != value)
                {
                    OnPropertyChanged();
                }

                return;
            }

            if (_minNonSalientKernelSize > clamped)
            {
                SetProperty(ref _minNonSalientKernelSize, clamped, nameof(MinNonSalientKernelSize));
            }

            RaiseParameterChanged(isImmediate: false);
        }
    }

    /// <summary>
    /// Gets or sets the normalized distance-to-kernel scaling multiplier (<c>&gt;= 0.0</c>).
    /// </summary>
    public double DistanceKernelScale
    {
        get => _distanceKernelScale;
        set => SetNonNegativeDouble(ref _distanceKernelScale, value);
    }

    /// <summary>
    /// Gets or sets the additive bias offset for distance-based kernel sizing (any finite double).
    /// </summary>
    public double DistanceKernelBias
    {
        get => _distanceKernelBias;
        set => SetFiniteDouble(ref _distanceKernelBias, value);
    }

    /// <summary>
    /// Gets or sets the cross-segment color difference scale factor (<c>&gt;= 0.0</c>).
    /// </summary>
    public double CrossSegmentColorScale
    {
        get => _crossSegmentColorScale;
        set => SetNonNegativeDouble(ref _crossSegmentColorScale, value);
    }

    /// <summary>
    /// Gets or sets the salient overlap fraction threshold in <c>[0.0, 1.0]</c>.
    /// </summary>
    public double SalientOverlapThreshold
    {
        get => _salientOverlapThreshold;
        set => SetFiniteDouble(ref _salientOverlapThreshold, value, 0.0, 1.0);
    }

    /// <summary>
    /// Loads Stage 3b parameters from pipeline options without transient coupling distortion.
    /// </summary>
    public void LoadFrom(bool enableLodAbstraction, LodAbstractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _enableLodAbstraction, enableLodAbstraction, nameof(EnableLodAbstraction));
            SetProperty(ref _salientKernelSize, options.SalientKernelSize, nameof(SalientKernelSize));
            SetProperty(ref _minNonSalientKernelSize, options.MinNonSalientKernelSize, nameof(MinNonSalientKernelSize));
            SetProperty(ref _maxNonSalientKernelSize, options.MaxNonSalientKernelSize, nameof(MaxNonSalientKernelSize));
            SetProperty(ref _distanceKernelScale, options.DistanceKernelScale, nameof(DistanceKernelScale));
            SetProperty(ref _distanceKernelBias, options.DistanceKernelBias, nameof(DistanceKernelBias));
            SetProperty(ref _crossSegmentColorScale, options.CrossSegmentColorScale, nameof(CrossSegmentColorScale));
            SetProperty(ref _salientOverlapThreshold, options.SalientOverlapThreshold, nameof(SalientOverlapThreshold));
        }
    }

    /// <summary>
    /// Builds a validated <see cref="LodAbstractionOptions"/> instance.
    /// </summary>
    public LodAbstractionOptions ToOptions()
    {
        var options = new LodAbstractionOptions(
            _salientKernelSize,
            _minNonSalientKernelSize,
            _maxNonSalientKernelSize,
            _distanceKernelScale,
            _distanceKernelBias,
            _crossSegmentColorScale,
            _salientOverlapThreshold);
        options.Validate();
        return options;
    }
}
