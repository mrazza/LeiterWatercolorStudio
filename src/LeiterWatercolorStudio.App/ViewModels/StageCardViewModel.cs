using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using LeiterWatercolorStudio.Core.Models;
using LeiterWatercolorStudio.Core.Pipeline;

namespace LeiterWatercolorStudio.App.ViewModels;

/// <summary>
/// View model representing one of the 8 primary pipeline stage cards shown in the
/// <c>PipelineFilmstrip</c> and <c>StageGridView</c>.
/// </summary>
public sealed class StageCardViewModel : ObservableObject, IDisposable
{
    private PrimaryStageId _stageId;
    private string _title = string.Empty;
    private char _shortcutKey = '1';
    private bool _hasSubStages;
    private StageViewId _activeViewId;
    private WriteableBitmap? _thumbnail;
    private WriteableBitmap? _gridBitmap;
    private string _timingText = "—";
    private string _statusBadgeText = string.Empty;
    private bool _isSelected;
    private bool _isRunning;
    private bool _isStale;
    private bool _isFromCache;
    private bool _isBypassed;
    private bool _isPinned;
    private IReadOnlyList<SubStageItemViewModel> _subStages = Array.Empty<SubStageItemViewModel>();
    private bool _disposed;

    /// <summary>
    /// Raised whenever <see cref="Thumbnail"/> or <see cref="GridBitmap"/> is assigned or refreshed in place,
    /// allowing visual hosts to invalidate images when a <see cref="WriteableBitmap"/> buffer is reused.
    /// </summary>
    public event EventHandler? BitmapUpdated;

    /// <summary>
    /// Initializes a new <see cref="StageCardViewModel"/> with default values.
    /// </summary>
    public StageCardViewModel()
    {
    }

    /// <summary>
    /// Initializes a new <see cref="StageCardViewModel"/> from a canonical <see cref="PipelineStageDescriptor"/>.
    /// </summary>
    /// <param name="descriptor">The primary stage descriptor.</param>
    public StageCardViewModel(PipelineStageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _stageId = descriptor.StageId;
        _title = descriptor.DisplayName;
        _shortcutKey = descriptor.ShortcutKey;
        _hasSubStages = descriptor.HasSubStages;
        _activeViewId = descriptor.DefaultView;

        if (descriptor.HasSubStages && descriptor.ChildViews.Count > 0)
        {
            var items = new SubStageItemViewModel[descriptor.ChildViews.Count];
            for (int i = 0; i < descriptor.ChildViews.Count; i++)
            {
                StageViewDescriptor viewDescriptor = PipelineStageRegistry.GetView(descriptor.ChildViews[i]);
                items[i] = new SubStageItemViewModel(viewDescriptor)
                {
                    IsSelected = viewDescriptor.ViewId == descriptor.DefaultView,
                };
            }

            _subStages = Array.AsReadOnly(items);
        }
    }

    /// <summary>
    /// Initializes a new <see cref="StageCardViewModel"/> with explicit stage metadata.
    /// </summary>
    public StageCardViewModel(
        PrimaryStageId stageId,
        string title,
        char shortcutKey,
        bool hasSubStages,
        StageViewId activeViewId,
        IReadOnlyList<SubStageItemViewModel>? subStages = null)
    {
        _stageId = stageId;
        _title = title ?? string.Empty;
        _shortcutKey = shortcutKey;
        _hasSubStages = hasSubStages;
        _activeViewId = activeViewId;
        _subStages = subStages ?? Array.Empty<SubStageItemViewModel>();
    }

    /// <summary>
    /// Gets or sets the primary stage identifier.
    /// </summary>
    public PrimaryStageId StageId
    {
        get => _stageId;
        set => SetProperty(ref _stageId, value);
    }

    /// <summary>
    /// Gets or sets the stage title shown in the filmstrip and grid tiles.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value ?? string.Empty);
    }

    /// <summary>
    /// Gets or sets the keyboard shortcut key (<c>'1'</c>–<c>'8'</c>).
    /// </summary>
    public char ShortcutKey
    {
        get => _shortcutKey;
        set
        {
            if (SetProperty(ref _shortcutKey, value))
            {
                OnPropertyChanged(nameof(ShortcutText));
            }
        }
    }

    /// <summary>
    /// Gets the string representation of <see cref="ShortcutKey"/> for compiled XAML bindings.
    /// </summary>
    public string ShortcutText => _shortcutKey == '\0' ? string.Empty : _shortcutKey.ToString();

    /// <summary>
    /// Gets or sets whether this primary stage exposes collapsible sub-stage pills.
    /// </summary>
    public bool HasSubStages
    {
        get => _hasSubStages;
        set => SetProperty(ref _hasSubStages, value);
    }

    /// <summary>
    /// Gets or sets the currently active inspectable <see cref="StageViewId"/> for this primary stage.
    /// </summary>
    public StageViewId ActiveViewId
    {
        get => _activeViewId;
        set => SetProperty(ref _activeViewId, value);
    }

    /// <summary>
    /// Gets or sets the compact thumbnail bitmap displayed in the pipeline filmstrip.
    /// Replacing this property with a different bitmap instance disposes the previous bitmap unless shared with <see cref="GridBitmap"/>.
    /// </summary>
    public WriteableBitmap? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value))
            {
                if (value is not null)
                {
                    BitmapUpdated?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            WriteableBitmap? previous = _thumbnail;
            if (SetProperty(ref _thumbnail, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
                OnPropertyChanged(nameof(DisplayBitmap));
                OnPropertyChanged(nameof(HasDisplayBitmap));
                BitmapUpdated?.Invoke(this, EventArgs.Empty);

                if (previous is not null && !ReferenceEquals(previous, _gridBitmap))
                {
                    previous.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the working-resolution bitmap displayed in the synchronized <c>StageGridView</c>.
    /// Replacing this property with a different bitmap instance disposes the previous bitmap unless shared with <see cref="Thumbnail"/>.
    /// </summary>
    public WriteableBitmap? GridBitmap
    {
        get => _gridBitmap;
        set
        {
            if (ReferenceEquals(_gridBitmap, value))
            {
                if (value is not null)
                {
                    BitmapUpdated?.Invoke(this, EventArgs.Empty);
                }

                return;
            }

            WriteableBitmap? previous = _gridBitmap;
            if (SetProperty(ref _gridBitmap, value))
            {
                OnPropertyChanged(nameof(DisplayBitmap));
                OnPropertyChanged(nameof(HasDisplayBitmap));
                BitmapUpdated?.Invoke(this, EventArgs.Empty);

                if (previous is not null && !ReferenceEquals(previous, _thumbnail))
                {
                    previous.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a thumbnail bitmap is currently available.
    /// </summary>
    public bool HasThumbnail => _thumbnail is not null;

    /// <summary>
    /// Gets the preferred bitmap for grid display (<see cref="GridBitmap"/> falling back to <see cref="Thumbnail"/>).
    /// </summary>
    public WriteableBitmap? DisplayBitmap => _gridBitmap ?? _thumbnail;

    /// <summary>
    /// Gets a value indicating whether <see cref="DisplayBitmap"/> is non-null.
    /// </summary>
    public bool HasDisplayBitmap => DisplayBitmap is not null;

    /// <summary>
    /// Gets or sets the formatted timing or status readout (e.g., <c>"42 ms"</c>, <c>"Cached"</c>, <c>"Bypassed"</c>, <c>"—"</c>).
    /// </summary>
    public string TimingText
    {
        get => _timingText;
        set => SetProperty(ref _timingText, string.IsNullOrEmpty(value) ? "—" : value);
    }

    /// <summary>
    /// Gets or sets an optional status badge label.
    /// </summary>
    public string StatusBadgeText
    {
        get => _statusBadgeText;
        set
        {
            string normalized = value ?? string.Empty;
            if (SetProperty(ref _statusBadgeText, normalized))
            {
                OnPropertyChanged(nameof(HasStatusBadge));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="StatusBadgeText"/> has non-whitespace text.
    /// </summary>
    public bool HasStatusBadge => !string.IsNullOrWhiteSpace(_statusBadgeText);

    /// <summary>
    /// Gets or sets whether this stage card is currently selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Gets or sets whether this stage is currently executing.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    /// <summary>
    /// Gets or sets whether this stage needs recomputation due to upstream edits.
    /// </summary>
    public bool IsStale
    {
        get => _isStale;
        set => SetProperty(ref _isStale, value);
    }

    /// <summary>
    /// Gets or sets whether this stage result was served from cache.
    /// </summary>
    public bool IsFromCache
    {
        get => _isFromCache;
        set => SetProperty(ref _isFromCache, value);
    }

    /// <summary>
    /// Gets or sets whether this stage is currently bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set => SetProperty(ref _isBypassed, value);
    }

    /// <summary>
    /// Gets or sets whether this stage is pinned as the A/B comparison reference.
    /// </summary>
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    /// <summary>
    /// Gets or sets the list of sub-stage view models associated with this primary stage.
    /// </summary>
    public IReadOnlyList<SubStageItemViewModel> SubStages
    {
        get => _subStages;
        set => SetProperty(ref _subStages, value ?? Array.Empty<SubStageItemViewModel>());
    }

    /// <summary>
    /// Notifies visual hosts that <see cref="Thumbnail"/> or <see cref="GridBitmap"/> pixels were updated in place.
    /// </summary>
    public void NotifyBitmapsUpdated()
    {
        BitmapUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears and disposes both <see cref="Thumbnail"/> and <see cref="GridBitmap"/> cleanly.
    /// </summary>
    public void ClearBitmaps()
    {
        Thumbnail = null;
        GridBitmap = null;
    }

    /// <summary>
    /// Disposes managed bitmaps owned by this card view model.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearBitmaps();
        BitmapUpdated = null;
    }
}
