using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;

namespace LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

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
