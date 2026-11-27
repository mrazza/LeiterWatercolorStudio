using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;

namespace LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

/// <summary>
/// Observable view model for <see cref="SaliencyCutOptions"/> outer iteration and resolution properties.
/// </summary>
public sealed class SaliencyCutOptionsViewModel : StageViewModelBase
{
    private const int DefaultWorkingDimension = 1024;

    private SaliencyCutOptions _baseline;
    private int _maxOuterIterations;
    private int _trimapBandRadius;
    private bool _useMaxWorkingDimension;
    private int _maxWorkingDimension;

    internal SaliencyCutOptionsViewModel(SaliencyCutOptions initial)
    {
        initial.Validate();
        _baseline = initial;
        _maxOuterIterations = initial.MaxOuterIterations;
        _trimapBandRadius = initial.TrimapBandRadius;
        _useMaxWorkingDimension = initial.MaxWorkingDimension.HasValue;
        _maxWorkingDimension = initial.MaxWorkingDimension ?? DefaultWorkingDimension;
    }

    /// <summary>
    /// Gets the baseline <see cref="SaliencyCutOptions"/> used for per-parameter resets.
    /// </summary>
    public SaliencyCutOptions Baseline => _baseline;

    /// <summary>
    /// Gets the non-nullable baseline working dimension value for slider binding
    /// (falling back to <c>1024</c> when <see cref="SaliencyCutOptions.MaxWorkingDimension"/> is <see langword="null"/>).
    /// </summary>
    public int BaselineMaxWorkingDimension => _baseline.MaxWorkingDimension ?? DefaultWorkingDimension;

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
    /// Updates <see cref="Baseline"/> without modifying current parameter values or raising a parameter edit.
    /// </summary>
    public void SetBaseline(SaliencyCutOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (SetProperty(ref _baseline, baseline, nameof(Baseline)))
        {
            OnPropertyChanged(nameof(BaselineMaxWorkingDimension));
        }
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

    /// <inheritdoc />
    public override void ResetToBaseline()
    {
        int targetWorkingDim = BaselineMaxWorkingDimension;
        bool changed = _maxOuterIterations != _baseline.MaxOuterIterations
            || _trimapBandRadius != _baseline.TrimapBandRadius
            || _useMaxWorkingDimension != _baseline.MaxWorkingDimension.HasValue
            || _maxWorkingDimension != targetWorkingDim;

        using (SuppressNotifications())
        {
            LoadFrom(_baseline);
            SetProperty(ref _maxWorkingDimension, targetWorkingDim, nameof(MaxWorkingDimension));
        }

        if (changed)
        {
            RaiseParameterChanged(isImmediate: true);
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
