using Leiter.Algorithms.Saliency;

namespace LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

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
