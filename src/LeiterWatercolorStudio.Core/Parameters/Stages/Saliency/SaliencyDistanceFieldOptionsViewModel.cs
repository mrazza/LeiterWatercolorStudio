using Leiter.Algorithms.Saliency;

namespace LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

/// <summary>
/// Observable view model for <see cref="SaliencyDistanceFieldOptions"/>.
/// </summary>
public sealed class SaliencyDistanceFieldOptionsViewModel : StageViewModelBase
{
    private const double DefaultExplicitSmoothingSigma = 12.0;

    private SaliencyDistanceFieldOptions _baseline;
    private bool _useExplicitSmoothingSigma;
    private double _smoothingSigma;
    private double _defaultSigmaScale;

    internal SaliencyDistanceFieldOptionsViewModel(SaliencyDistanceFieldOptions initial)
    {
        initial.Validate();
        _baseline = initial;
        _useExplicitSmoothingSigma = initial.SmoothingSigma.HasValue;
        _smoothingSigma = initial.SmoothingSigma ?? DefaultExplicitSmoothingSigma;
        _defaultSigmaScale = initial.DefaultSigmaScale;
    }

    /// <summary>
    /// Gets the baseline <see cref="SaliencyDistanceFieldOptions"/> used for per-parameter resets.
    /// </summary>
    public SaliencyDistanceFieldOptions Baseline => _baseline;

    /// <summary>
    /// Gets the non-nullable baseline smoothing sigma value for slider binding
    /// (falling back to <c>12.0</c> when <see cref="SaliencyDistanceFieldOptions.SmoothingSigma"/> is <see langword="null"/>).
    /// </summary>
    public double BaselineSmoothingSigma => _baseline.SmoothingSigma ?? DefaultExplicitSmoothingSigma;

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
    /// Updates <see cref="Baseline"/> without modifying current parameter values or raising a parameter edit.
    /// </summary>
    public void SetBaseline(SaliencyDistanceFieldOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (SetProperty(ref _baseline, baseline, nameof(Baseline)))
        {
            OnPropertyChanged(nameof(BaselineSmoothingSigma));
        }
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

    /// <inheritdoc />
    public override void ResetToBaseline()
    {
        double targetSmoothingSigma = BaselineSmoothingSigma;
        bool changed = _useExplicitSmoothingSigma != _baseline.SmoothingSigma.HasValue
            || _smoothingSigma != targetSmoothingSigma
            || _defaultSigmaScale != _baseline.DefaultSigmaScale;

        using (SuppressNotifications())
        {
            LoadFrom(_baseline);
            SetProperty(ref _smoothingSigma, targetSmoothingSigma, nameof(SmoothingSigma));
        }

        if (changed)
        {
            RaiseParameterChanged(isImmediate: true);
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
