using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using LeiterWatercolorStudio.App.ViewModels;
using LeiterWatercolorStudio.Core.Models;

namespace LeiterWatercolorStudio.App.Controls;

/// <summary>
/// Displays the 8 primary pipeline stages connected by directional chevrons along with a collapsible
/// sub-stage pill drawer for inspecting intermediate steps.
/// </summary>
public partial class PipelineFilmstrip : UserControl
{
    /// <summary>
    /// Identifies the <see cref="Stages"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<StageCardViewModel>?> StagesProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, IReadOnlyList<StageCardViewModel>?>(nameof(Stages));

    /// <summary>
    /// Identifies the <see cref="SelectedStage"/> styled property.
    /// </summary>
    public static readonly StyledProperty<StageCardViewModel?> SelectedStageProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, StageCardViewModel?>(
            nameof(SelectedStage),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="SubStages"/> styled property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<SubStageItemViewModel>?> SubStagesProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, IReadOnlyList<SubStageItemViewModel>?>(nameof(SubStages));

    /// <summary>
    /// Identifies the <see cref="SelectedSubStage"/> styled property.
    /// </summary>
    public static readonly StyledProperty<SubStageItemViewModel?> SelectedSubStageProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, SubStageItemViewModel?>(
            nameof(SelectedSubStage),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Identifies the <see cref="ShowSubStageDrawer"/> styled property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowSubStageDrawerProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, bool>(nameof(ShowSubStageDrawer));

    /// <summary>
    /// Identifies the <see cref="ActiveStageSummary"/> styled property.
    /// </summary>
    public static readonly StyledProperty<string?> ActiveStageSummaryProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, string?>(
            nameof(ActiveStageSummary),
            "Single-view stage — no sub-views");

    /// <summary>
    /// Identifies the <see cref="SelectStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> SelectStageCommandProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, ICommand?>(nameof(SelectStageCommand));

    /// <summary>
    /// Identifies the <see cref="SelectSubStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> SelectSubStageCommandProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, ICommand?>(nameof(SelectSubStageCommand));

    /// <summary>
    /// Identifies the <see cref="TogglePinStageCommand"/> styled property.
    /// </summary>
    public static readonly StyledProperty<ICommand?> TogglePinStageCommandProperty =
        AvaloniaProperty.Register<PipelineFilmstrip, ICommand?>(nameof(TogglePinStageCommand));

    private const double CompactHeightThreshold = 132.0;

    /// <summary>
    /// Identifies the <see cref="IsCompactHeight"/> direct property.
    /// </summary>
    public static readonly DirectProperty<PipelineFilmstrip, bool> IsCompactHeightProperty =
        AvaloniaProperty.RegisterDirect<PipelineFilmstrip, bool>(
            nameof(IsCompactHeight),
            o => o.IsCompactHeight);

    private readonly List<StageCardViewModel> _subscribedCards = [];
    private INotifyCollectionChanged? _subscribedCollection;
    private bool _isCompactHeight;

    /// <summary>
    /// Initializes a new <see cref="PipelineFilmstrip"/>.
    /// </summary>
    public PipelineFilmstrip()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the ordered collection of primary stage card view models.
    /// </summary>
    public IReadOnlyList<StageCardViewModel>? Stages
    {
        get => GetValue(StagesProperty);
        set => SetValue(StagesProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected primary stage card.
    /// </summary>
    public StageCardViewModel? SelectedStage
    {
        get => GetValue(SelectedStageProperty);
        set => SetValue(SelectedStageProperty, value);
    }

    /// <summary>
    /// Gets or sets the sub-stage pills displayed in the collapsible drawer.
    /// </summary>
    public IReadOnlyList<SubStageItemViewModel>? SubStages
    {
        get => GetValue(SubStagesProperty);
        set => SetValue(SubStagesProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected sub-stage pill.
    /// </summary>
    public SubStageItemViewModel? SelectedSubStage
    {
        get => GetValue(SelectedSubStageProperty);
        set => SetValue(SelectedSubStageProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the sub-stage drawer above the filmstrip is active.
    /// </summary>
    public bool ShowSubStageDrawer
    {
        get => GetValue(ShowSubStageDrawerProperty);
        set => SetValue(ShowSubStageDrawerProperty, value);
    }

    /// <summary>
    /// Gets or sets the quiet summary text displayed in the persistent bar when the active stage has no sub-stages.
    /// </summary>
    public string? ActiveStageSummary
    {
        get => GetValue(ActiveStageSummaryProperty);
        set => SetValue(ActiveStageSummaryProperty, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when a stage card is selected.
    /// Receives the selected <see cref="StageCardViewModel"/> (or its <see cref="Core.Models.PrimaryStageId"/>).
    /// </summary>
    public ICommand? SelectStageCommand
    {
        get => GetValue(SelectStageCommandProperty);
        set => SetValue(SelectStageCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when a sub-stage pill is selected.
    /// Receives the selected <see cref="SubStageItemViewModel"/> (or its <see cref="Core.Models.StageViewId"/>).
    /// </summary>
    public ICommand? SelectSubStageCommand
    {
        get => GetValue(SelectSubStageCommandProperty);
        set => SetValue(SelectSubStageCommandProperty, value);
    }

    /// <summary>
    /// Gets whether the filmstrip is currently resized into compact text-only height (thumbnails collapsed).
    /// </summary>
    public bool IsCompactHeight
    {
        get => _isCompactHeight;
        private set => SetAndRaise(IsCompactHeightProperty, ref _isCompactHeight, value);
    }

    /// <summary>
    /// Gets or sets the command invoked when a stage's pin button is clicked.
    /// Receives the target <see cref="StageCardViewModel"/> (or its <see cref="Core.Models.PrimaryStageId"/>).
    /// </summary>
    public ICommand? TogglePinStageCommand
    {
        get => GetValue(TogglePinStageCommandProperty);
        set => SetValue(TogglePinStageCommandProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == StagesProperty)
        {
            RefreshCardSubscriptions();
        }
        else if (change.Property == SelectedStageProperty)
        {
            if (change.OldValue is StageCardViewModel oldCard)
            {
                oldCard.IsSelected = false;
            }

            StageCardViewModel? newCard = change.NewValue as StageCardViewModel;
            if (Stages is { } stages)
            {
                for (int i = 0; i < stages.Count; i++)
                {
                    stages[i].IsSelected = ReferenceEquals(stages[i], newCard);
                }
            }
            else if (newCard is not null)
            {
                newCard.IsSelected = true;
            }
        }
        else if (change.Property == SelectedSubStageProperty)
        {
            if (change.OldValue is SubStageItemViewModel oldSub)
            {
                oldSub.IsSelected = false;
            }

            SubStageItemViewModel? newSub = change.NewValue as SubStageItemViewModel;
            if (SubStages is { } subStages)
            {
                for (int i = 0; i < subStages.Count; i++)
                {
                    subStages[i].IsSelected = ReferenceEquals(subStages[i], newSub);
                }
            }
            else if (newSub is not null)
            {
                newSub.IsSelected = true;
            }
        }
        else if (change.Property == BoundsProperty)
        {
            double height = Bounds.Height;
            IsCompactHeight = height > 0.0 && height < CompactHeightThreshold;
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshCardSubscriptions();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ClearCardSubscriptions();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnStageCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not StageCardViewModel card)
        {
            return;
        }

        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Pin button clicks are handled exclusively by OnPinButtonClick.
        if (e.Source is Visual sourceVisual && sourceVisual.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        SelectedStage = card;
        ExecuteStageCommand(SelectStageCommand, card);
        e.Handled = true;
    }

    private void OnPinButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StageCardViewModel card)
        {
            return;
        }

        e.Handled = true;
        if (TogglePinStageCommand is { } command)
        {
            ExecuteStageCommand(command, card);
        }
        else
        {
            card.IsPinned = !card.IsPinned;
        }
    }

    private void OnSubStageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not SubStageItemViewModel subStage)
        {
            return;
        }

        SelectedSubStage = subStage;
        ExecuteSubStageCommand(SelectSubStageCommand, subStage);
        e.Handled = true;
    }

    private void RefreshCardSubscriptions()
    {
        ClearCardSubscriptions();

        IReadOnlyList<StageCardViewModel>? stages = Stages;
        if (stages is null)
        {
            return;
        }

        if (stages is INotifyCollectionChanged notifyCollection)
        {
            _subscribedCollection = notifyCollection;
            notifyCollection.CollectionChanged += OnStagesCollectionChanged;
        }

        for (int i = 0; i < stages.Count; i++)
        {
            StageCardViewModel card = stages[i];
            card.BitmapUpdated += OnCardBitmapUpdated;
            _subscribedCards.Add(card);
        }
    }

    private void ClearCardSubscriptions()
    {
        if (_subscribedCollection is not null)
        {
            _subscribedCollection.CollectionChanged -= OnStagesCollectionChanged;
            _subscribedCollection = null;
        }

        for (int i = 0; i < _subscribedCards.Count; i++)
        {
            _subscribedCards[i].BitmapUpdated -= OnCardBitmapUpdated;
        }

        _subscribedCards.Clear();
    }

    private void OnStagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshCardSubscriptions();
    }

    private void OnCardBitmapUpdated(object? sender, EventArgs e)
    {
        if (sender is not StageCardViewModel card || PrimaryStagesItemsControl is null)
        {
            return;
        }

        foreach (Image image in PrimaryStagesItemsControl.GetVisualDescendants().OfType<Image>())
        {
            if (ReferenceEquals(image.DataContext, card))
            {
                image.InvalidateVisual();
            }
        }
    }

    private static void ExecuteStageCommand(ICommand? command, StageCardViewModel card) =>
        ExecuteTypedCommand(command, card.StageId, card);

    private static void ExecuteSubStageCommand(ICommand? command, SubStageItemViewModel subStage) =>
        ExecuteTypedCommand(command, subStage.ViewId, subStage);

    private static void ExecuteTypedCommand<TId, TItem>(ICommand? command, TId id, TItem item)
        where TId : struct
    {
        if (command is null)
        {
            return;
        }

        if (command is IRelayCommand<TId> idCommand)
        {
            if (idCommand.CanExecute(id))
            {
                idCommand.Execute(id);
            }

            return;
        }

        if (command is IRelayCommand<TId?> nullableIdCommand)
        {
            if (nullableIdCommand.CanExecute(id))
            {
                nullableIdCommand.Execute(id);
            }

            return;
        }

        if (command is IRelayCommand<TItem> itemCommand)
        {
            if (itemCommand.CanExecute(item))
            {
                itemCommand.Execute(item);
            }

            return;
        }

        if (command.CanExecute(item))
        {
            command.Execute(item);
        }
        else if (command.CanExecute(id))
        {
            command.Execute(id);
        }
    }
}
