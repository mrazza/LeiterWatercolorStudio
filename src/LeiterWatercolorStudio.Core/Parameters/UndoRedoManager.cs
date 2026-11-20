using CommunityToolkit.Mvvm.ComponentModel;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Parameters;

/// <summary>
/// Manages an immutable snapshot history stack of <see cref="WatercolorOptions"/> with bounded capacity
/// and transaction coalescing for continuous UI interactions such as slider drags.
/// </summary>
public sealed class UndoRedoManager : ObservableObject
{
    /// <summary>
    /// The default maximum number of undo snapshots retained by the history stack.
    /// </summary>
    public const int DefaultMaxCapacity = 200;

    private readonly LinkedList<WatercolorOptions> _undoStack = new();
    private readonly LinkedList<WatercolorOptions> _redoStack = new();

    private int _transactionDepth;
    private WatercolorOptions? _transactionBaseline;

    /// <summary>
    /// Initializes a new empty <see cref="UndoRedoManager"/>.
    /// </summary>
    /// <param name="maxCapacity">The maximum number of undo snapshots retained (defaults to <see cref="DefaultMaxCapacity"/>).</param>
    public UndoRedoManager(int maxCapacity = DefaultMaxCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCapacity, 1);
        MaxCapacity = maxCapacity;
    }

    /// <summary>
    /// Initializes a new <see cref="UndoRedoManager"/> seeded with an initial snapshot.
    /// </summary>
    /// <param name="initialState">The initial options snapshot.</param>
    /// <param name="maxCapacity">The maximum number of undo snapshots retained (defaults to <see cref="DefaultMaxCapacity"/>).</param>
    public UndoRedoManager(WatercolorOptions initialState, int maxCapacity = DefaultMaxCapacity)
        : this(maxCapacity)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        Current = initialState;
    }

    /// <summary>
    /// Gets the maximum number of undo entries stored in the undo stack.
    /// </summary>
    public int MaxCapacity { get; }

    /// <summary>
    /// Gets the current active <see cref="WatercolorOptions"/> snapshot, or <see langword="null"/> if not initialized.
    /// </summary>
    public WatercolorOptions? Current { get; private set; }

    /// <summary>
    /// Gets the number of available undo snapshots.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Gets the number of available redo snapshots.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Gets a value indicating whether an undo operation can currently be performed.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a redo operation can currently be performed.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a drag/coalescing transaction is currently active.
    /// </summary>
    public bool IsInTransaction => _transactionDepth > 0;

    /// <summary>
    /// Clears all undo/redo history and any active transaction, setting <see cref="Current"/> to <paramref name="baseline"/>.
    /// </summary>
    /// <param name="baseline">The new baseline snapshot.</param>
    public void Reset(WatercolorOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        _undoStack.Clear();
        _redoStack.Clear();
        _transactionDepth = 0;
        _transactionBaseline = null;
        Current = baseline;

        NotifyUndoRedoStateChanged();
    }

    /// <summary>
    /// Records a new <see cref="WatercolorOptions"/> snapshot. During an active transaction, updates <see cref="Current"/>
    /// without pushing intermediate entries to the undo stack.
    /// </summary>
    /// <param name="options">The new options snapshot.</param>
    public void Record(WatercolorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_transactionDepth > 0)
        {
            Current = options;
            return;
        }

        CommitSnapshot(Current, options);
    }

    /// <summary>
    /// Starts a coalesced drag transaction so intermediate changes collapse into a single undo step on commit.
    /// Supports nested calls cleanly.
    /// </summary>
    public void BeginTransaction()
    {
        if (_transactionDepth == 0)
        {
            _transactionBaseline = Current;
        }

        _transactionDepth++;
    }

    /// <summary>
    /// Commits the active drag transaction with <paramref name="finalOptions"/>, pushing at most one undo step
    /// if the state changed from the transaction baseline.
    /// </summary>
    /// <param name="finalOptions">The final options snapshot at transaction completion.</param>
    public void CommitTransaction(WatercolorOptions finalOptions)
    {
        ArgumentNullException.ThrowIfNull(finalOptions);

        if (_transactionDepth == 0)
        {
            Record(finalOptions);
            return;
        }

        _transactionDepth--;
        if (_transactionDepth > 0)
        {
            Current = finalOptions;
            return;
        }

        WatercolorOptions? baseline = _transactionBaseline;
        _transactionBaseline = null;
        CommitSnapshot(baseline, finalOptions);
    }

    /// <summary>
    /// Cancels the active drag transaction and restores <see cref="Current"/> to the pre-transaction baseline
    /// without modifying undo or redo history.
    /// </summary>
    public void CancelTransaction()
    {
        if (_transactionDepth == 0)
        {
            return;
        }

        _transactionDepth = 0;
        Current = _transactionBaseline;
        _transactionBaseline = null;
    }

    /// <summary>
    /// Reverts to the most recent undo snapshot and pushes the current snapshot onto the redo stack.
    /// </summary>
    /// <returns>The restored <see cref="WatercolorOptions"/> snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="CanUndo"/> is <see langword="false"/>.</exception>
    public WatercolorOptions Undo()
    {
        if (_undoStack.Count == 0)
        {
            throw new InvalidOperationException("Cannot undo when the undo stack is empty.");
        }

        ClearTransactionState();

        if (Current is not null)
        {
            _redoStack.AddLast(Current);
        }

        WatercolorOptions previous = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        Current = previous;

        NotifyUndoRedoStateChanged();
        return previous;
    }

    /// <summary>
    /// Re-applies the most recent redo snapshot and pushes the current snapshot onto the undo stack.
    /// </summary>
    /// <returns>The restored <see cref="WatercolorOptions"/> snapshot.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="CanRedo"/> is <see langword="false"/>.</exception>
    public WatercolorOptions Redo()
    {
        if (_redoStack.Count == 0)
        {
            throw new InvalidOperationException("Cannot redo when the redo stack is empty.");
        }

        ClearTransactionState();

        if (Current is not null)
        {
            PushUndo(Current);
        }

        WatercolorOptions next = _redoStack.Last!.Value;
        _redoStack.RemoveLast();
        Current = next;

        NotifyUndoRedoStateChanged();
        return next;
    }

    private void CommitSnapshot(WatercolorOptions? previous, WatercolorOptions next)
    {
        if (previous is null)
        {
            Current = next;
            _redoStack.Clear();
            NotifyUndoRedoStateChanged();
            return;
        }

        if (Equals(previous, next))
        {
            Current = next;
            return;
        }

        PushUndo(previous);
        _redoStack.Clear();
        Current = next;

        NotifyUndoRedoStateChanged();
    }

    private void PushUndo(WatercolorOptions snapshot)
    {
        while (_undoStack.Count >= MaxCapacity)
        {
            _undoStack.RemoveFirst();
        }

        _undoStack.AddLast(snapshot);
    }

    private void ClearTransactionState()
    {
        _transactionDepth = 0;
        _transactionBaseline = null;
    }

    private void NotifyUndoRedoStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
    }
}
