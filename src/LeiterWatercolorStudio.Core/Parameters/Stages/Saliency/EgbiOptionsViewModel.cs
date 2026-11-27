using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;

namespace LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

/// <summary>
/// Observable view model for <see cref="EgbiOptions"/>.
/// </summary>
public sealed class EgbiOptionsViewModel : StageViewModelBase
{
    private EgbiOptions _baseline;
    private double _kFactor;
    private double _epsilon;
    private int _minRegionSize;
    private GridConnectivity _connectivity;

    internal EgbiOptionsViewModel(EgbiOptions initial)
    {
        initial.Validate();
        _baseline = initial;
        _kFactor = initial.KFactor;
        _epsilon = initial.Epsilon;
        _minRegionSize = initial.MinRegionSize;
        _connectivity = initial.Connectivity;
    }

    /// <summary>
    /// Gets the baseline <see cref="EgbiOptions"/> used for per-parameter resets.
    /// </summary>
    public EgbiOptions Baseline => _baseline;

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
    /// Updates <see cref="Baseline"/> without modifying current parameter values or raising a parameter edit.
    /// </summary>
    public void SetBaseline(EgbiOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        SetProperty(ref _baseline, baseline, nameof(Baseline));
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

    /// <inheritdoc />
    public override void ResetToBaseline()
    {
        bool changed = _kFactor != _baseline.KFactor
            || _epsilon != _baseline.Epsilon
            || _minRegionSize != _baseline.MinRegionSize
            || _connectivity != _baseline.Connectivity;

        LoadFrom(_baseline);
        if (changed)
        {
            RaiseParameterChanged(isImmediate: true);
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
