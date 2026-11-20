using Leiter.Algorithms.Saliency;

namespace LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

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
