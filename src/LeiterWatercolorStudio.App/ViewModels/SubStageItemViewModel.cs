using CommunityToolkit.Mvvm.ComponentModel;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;

namespace LeiterWatercolorStudio.App.ViewModels;

/// <summary>
/// View model representing an inspectable sub-stage pill inside the pipeline filmstrip drawer.
/// </summary>
public sealed class SubStageItemViewModel : ObservableObject
{
    private StageViewId _viewId;
    private PrimaryStageId _parentStage;
    private string _title = string.Empty;
    private string _timingText = "—";
    private bool _isSelected;
    private bool _isRunning;
    private bool _isStale;
    private bool _isFromCache;
    private bool _isBypassed;

    /// <summary>
    /// Initializes a new <see cref="SubStageItemViewModel"/> with default values.
    /// </summary>
    public SubStageItemViewModel()
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SubStageItemViewModel"/> from a canonical <see cref="StageViewDescriptor"/>.
    /// </summary>
    /// <param name="descriptor">The stage view descriptor.</param>
    public SubStageItemViewModel(StageViewDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _viewId = descriptor.ViewId;
        _parentStage = descriptor.ParentStage;
        _title = descriptor.DisplayName;
    }

    /// <summary>
    /// Initializes a new <see cref="SubStageItemViewModel"/> with explicit metadata.
    /// </summary>
    public SubStageItemViewModel(
        StageViewId viewId,
        PrimaryStageId parentStage,
        string title,
        string timingText = "—")
    {
        _viewId = viewId;
        _parentStage = parentStage;
        _title = title ?? string.Empty;
        _timingText = string.IsNullOrEmpty(timingText) ? "—" : timingText;
    }

    /// <summary>
    /// Gets or sets the inspectable stage view identifier.
    /// </summary>
    public StageViewId ViewId
    {
        get => _viewId;
        set => SetProperty(ref _viewId, value);
    }

    /// <summary>
    /// Gets or sets the owning primary stage identifier.
    /// </summary>
    public PrimaryStageId ParentStage
    {
        get => _parentStage;
        set => SetProperty(ref _parentStage, value);
    }

    /// <summary>
    /// Gets or sets the display title for this sub-stage.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the formatted timing or status readout text (e.g., <c>"14 ms"</c>, <c>"Cached"</c>, <c>"—"</c>).
    /// </summary>
    public string TimingText
    {
        get => _timingText;
        set => SetProperty(ref _timingText, string.IsNullOrEmpty(value) ? "—" : value);
    }

    /// <summary>
    /// Gets or sets whether this sub-stage is currently selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Gets or sets whether this sub-stage is currently executing.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    /// <summary>
    /// Gets or sets whether this sub-stage needs recomputation due to upstream edits.
    /// </summary>
    public bool IsStale
    {
        get => _isStale;
        set => SetProperty(ref _isStale, value);
    }

    /// <summary>
    /// Gets or sets whether this sub-stage result was served from cache.
    /// </summary>
    public bool IsFromCache
    {
        get => _isFromCache;
        set => SetProperty(ref _isFromCache, value);
    }

    /// <summary>
    /// Gets or sets whether this sub-stage is currently bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set => SetProperty(ref _isBypassed, value);
    }
}
