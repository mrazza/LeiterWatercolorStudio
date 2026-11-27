using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Parameters.Stages;

/// <summary>
/// View model for Stage 3a (CIELAB Mean-Shift filtering and segmentation).
/// </summary>
public sealed class SegmentationStageViewModel : StageViewModelBase
{
    private MeanShiftOptions _baseline;
    private int _spatialRadius;
    private double _rangeRadius;
    private int _minRegionSize;
    private int _maxIterations;
    private double _convergenceEpsilon;
    private GridConnectivity _connectivity;

    /// <summary>
    /// Initializes a new <see cref="SegmentationStageViewModel"/> with default Stage 3a options.
    /// </summary>
    public SegmentationStageViewModel()
    {
        MeanShiftOptions defaults = WatercolorOptions.Default.MeanShift;
        _baseline = defaults;
        _spatialRadius = defaults.SpatialRadius;
        _rangeRadius = defaults.RangeRadius;
        _minRegionSize = defaults.MinRegionSize;
        _maxIterations = defaults.MaxIterations;
        _convergenceEpsilon = defaults.ConvergenceEpsilon;
        _connectivity = defaults.Connectivity;
    }

    /// <summary>
    /// Gets the baseline <see cref="MeanShiftOptions"/> used for per-parameter resets.
    /// </summary>
    public MeanShiftOptions Baseline => _baseline;

    /// <summary>
    /// Gets or sets the spatial search window radius in pixels (<c>&gt;= 1</c>).
    /// </summary>
    public int SpatialRadius
    {
        get => _spatialRadius;
        set => SetClampedInt(ref _spatialRadius, value, 1);
    }

    /// <summary>
    /// Gets or sets the CIELAB color range search window radius (<c>&gt; 0.0</c>).
    /// </summary>
    public double RangeRadius
    {
        get => _rangeRadius;
        set => SetPositiveDouble(ref _rangeRadius, value);
    }

    /// <summary>
    /// Gets or sets the minimum pixel count per segmented region (<c>&gt;= 0</c>).
    /// </summary>
    public int MinRegionSize
    {
        get => _minRegionSize;
        set => SetClampedInt(ref _minRegionSize, value, 0);
    }

    /// <summary>
    /// Gets or sets the maximum Mean-Shift mode-seeking iterations per pixel (<c>&gt;= 1</c>).
    /// </summary>
    public int MaxIterations
    {
        get => _maxIterations;
        set => SetClampedInt(ref _maxIterations, value, 1);
    }

    /// <summary>
    /// Gets or sets the 5D spatial-range convergence epsilon (<c>&gt; 0.0</c>).
    /// </summary>
    public double ConvergenceEpsilon
    {
        get => _convergenceEpsilon;
        set => SetPositiveDouble(ref _convergenceEpsilon, value);
    }

    /// <summary>
    /// Gets or sets the pixel connectivity for region clustering and pruning.
    /// </summary>
    public GridConnectivity Connectivity
    {
        get => _connectivity;
        set => SetEnum(ref _connectivity, value);
    }

    /// <summary>
    /// Updates <see cref="Baseline"/> without modifying current parameter values or raising a parameter edit.
    /// </summary>
    public void SetBaseline(MeanShiftOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        SetProperty(ref _baseline, baseline, nameof(Baseline));
    }

    /// <summary>
    /// Loads Stage 3a parameters from <paramref name="options"/>.
    /// </summary>
    public void LoadFrom(MeanShiftOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using (SuppressNotifications())
        {
            SetProperty(ref _spatialRadius, options.SpatialRadius, nameof(SpatialRadius));
            SetProperty(ref _rangeRadius, options.RangeRadius, nameof(RangeRadius));
            SetProperty(ref _minRegionSize, options.MinRegionSize, nameof(MinRegionSize));
            SetProperty(ref _maxIterations, options.MaxIterations, nameof(MaxIterations));
            SetProperty(ref _convergenceEpsilon, options.ConvergenceEpsilon, nameof(ConvergenceEpsilon));
            SetProperty(ref _connectivity, options.Connectivity, nameof(Connectivity));
        }
    }

    /// <inheritdoc />
    public override void ResetToBaseline()
    {
        bool changed = _spatialRadius != _baseline.SpatialRadius
            || _rangeRadius != _baseline.RangeRadius
            || _minRegionSize != _baseline.MinRegionSize
            || _maxIterations != _baseline.MaxIterations
            || _convergenceEpsilon != _baseline.ConvergenceEpsilon
            || _connectivity != _baseline.Connectivity;

        LoadFrom(_baseline);
        if (changed)
        {
            RaiseParameterChanged(isImmediate: true);
        }
    }

    /// <summary>
    /// Builds a validated <see cref="MeanShiftOptions"/> instance.
    /// </summary>
    public MeanShiftOptions ToOptions()
    {
        var options = new MeanShiftOptions(
            _spatialRadius,
            _rangeRadius,
            _minRegionSize,
            _maxIterations,
            _convergenceEpsilon,
            _connectivity);
        options.Validate();
        return options;
    }
}
