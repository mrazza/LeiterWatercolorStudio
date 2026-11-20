using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Parameters.Stages.Saliency;

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
