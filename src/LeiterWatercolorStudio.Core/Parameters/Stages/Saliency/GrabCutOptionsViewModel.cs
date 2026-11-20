using Leiter.Algorithms.Segmentation;

namespace LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

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
