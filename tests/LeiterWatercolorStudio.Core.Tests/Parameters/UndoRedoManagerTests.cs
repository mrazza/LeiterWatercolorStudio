using Leiter.Algorithms.Stylization.Watercolor;
using LeiterWatercolorStudio.Core.Parameters;
using Xunit;

namespace LeiterWatercolorStudio.Core.Tests.Parameters;

public class UndoRedoManagerTests
{
    private static WatercolorOptions WithStrength(double strength) =>
        WatercolorOptions.Default with { ColorTransferStrength = strength };

    [Fact]
    public void EmptyManager_ThrowsOnUndoOrRedo()
    {
        var manager = new UndoRedoManager();

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
        Assert.Null(manager.Current);
        Assert.Throws<InvalidOperationException>(() => manager.Undo());
        Assert.Throws<InvalidOperationException>(() => manager.Redo());
    }

    [Fact]
    public void RecordUndoRedo_TracksHistoryAndClearsRedoOnNewEdit()
    {
        var s0 = WithStrength(0.1);
        var s1 = WithStrength(0.2);
        var s2 = WithStrength(0.3);
        var s3 = WithStrength(0.4);

        var manager = new UndoRedoManager(s0);
        var changedProps = new List<string?>();
        manager.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        manager.Record(s1);
        manager.Record(s2);

        Assert.True(manager.CanUndo);
        Assert.False(manager.CanRedo);
        Assert.Equal(2, manager.UndoCount);
        Assert.Contains(nameof(UndoRedoManager.CanUndo), changedProps);

        Assert.Equal(s1, manager.Undo());
        Assert.True(manager.CanUndo);
        Assert.True(manager.CanRedo);
        Assert.Equal(s1, manager.Current);

        Assert.Equal(s0, manager.Undo());
        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);
        Assert.Equal(2, manager.RedoCount);

        Assert.Equal(s1, manager.Redo());
        Assert.Equal(s1, manager.Current);

        // Recording a new state after undo clears redo history.
        manager.Record(s3);
        Assert.False(manager.CanRedo);
        Assert.Equal(0, manager.RedoCount);
        Assert.Equal(s3, manager.Current);
        Assert.Equal(s1, manager.Undo());
    }

    [Fact]
    public void Record_DuplicateSnapshot_IsIgnoredWithoutClearingRedo()
    {
        var s0 = WithStrength(0.2);
        var s1 = WithStrength(0.8);
        var manager = new UndoRedoManager(s0);
        manager.Record(s1);
        manager.Undo();

        Assert.True(manager.CanRedo);

        manager.Record(WithStrength(0.2));

        Assert.False(manager.CanUndo);
        Assert.True(manager.CanRedo);
        Assert.Equal(s0, manager.Current);
    }

    [Fact]
    public void Capacity_EvictsOldestEntriesBeyondMaxCapacity()
    {
        var manager = new UndoRedoManager(WithStrength(0.0), maxCapacity: 3);

        manager.Record(WithStrength(0.1));
        manager.Record(WithStrength(0.2));
        manager.Record(WithStrength(0.3));
        manager.Record(WithStrength(0.4));
        manager.Record(WithStrength(0.5));

        Assert.Equal(3, manager.UndoCount);
        Assert.Equal(WithStrength(0.4), manager.Undo());
        Assert.Equal(WithStrength(0.3), manager.Undo());
        Assert.Equal(WithStrength(0.2), manager.Undo());
        Assert.False(manager.CanUndo);
    }

    [Fact]
    public void DefaultCapacity_Is200AndEnforced()
    {
        var manager = new UndoRedoManager(WithStrength(0.0));
        Assert.Equal(200, manager.MaxCapacity);

        for (int i = 1; i <= 205; i++)
        {
            manager.Record(WithStrength(i / 1000.0));
        }

        Assert.Equal(200, manager.UndoCount);
        WatercolorOptions oldest = manager.Current!;
        while (manager.CanUndo)
        {
            oldest = manager.Undo();
        }

        Assert.Equal(WithStrength(5 / 1000.0), oldest);
    }

    [Fact]
    public void Reset_ClearsStacksAndActiveTransaction()
    {
        var manager = new UndoRedoManager(WithStrength(0.1));
        manager.Record(WithStrength(0.2));
        manager.Undo();
        manager.BeginTransaction();

        var fresh = WithStrength(0.9);
        manager.Reset(fresh);

        Assert.False(manager.CanUndo);
        Assert.False(manager.CanRedo);
        Assert.False(manager.IsInTransaction);
        Assert.Equal(fresh, manager.Current);
    }

    [Fact]
    public void TransactionCoalescing_CollapsesDragIntoSingleUndoStep()
    {
        var s0 = WithStrength(0.1);
        var sFinal = WithStrength(0.9);
        var manager = new UndoRedoManager(s0);

        manager.BeginTransaction();
        Assert.True(manager.IsInTransaction);
        manager.Record(WithStrength(0.3));
        manager.Record(WithStrength(0.5));
        manager.Record(WithStrength(0.7));
        Assert.False(manager.CanUndo);

        manager.CommitTransaction(sFinal);

        Assert.False(manager.IsInTransaction);
        Assert.Equal(1, manager.UndoCount);
        Assert.Equal(sFinal, manager.Current);
        Assert.Equal(s0, manager.Undo());
    }

    [Fact]
    public void TransactionCoalescing_NoOpAndCancel_DoNotPushUndo()
    {
        var s0 = WithStrength(0.4);
        var manager = new UndoRedoManager(s0);

        manager.BeginTransaction();
        manager.Record(WithStrength(0.8));
        manager.CommitTransaction(WithStrength(0.4));

        Assert.False(manager.CanUndo);
        Assert.Equal(s0, manager.Current);

        manager.BeginTransaction();
        manager.Record(WithStrength(0.9));
        manager.CancelTransaction();

        Assert.False(manager.IsInTransaction);
        Assert.False(manager.CanUndo);
        Assert.Equal(s0, manager.Current);
    }

    [Fact]
    public void NestedTransactions_OnlyCommitOnOuterCompletion()
    {
        var s0 = WithStrength(0.1);
        var s2 = WithStrength(0.7);
        var manager = new UndoRedoManager(s0);

        manager.BeginTransaction();
        manager.BeginTransaction();
        manager.CommitTransaction(WithStrength(0.4));
        Assert.True(manager.IsInTransaction);
        Assert.False(manager.CanUndo);

        manager.CommitTransaction(s2);
        Assert.False(manager.IsInTransaction);
        Assert.Equal(1, manager.UndoCount);
        Assert.Equal(s0, manager.Undo());
    }
}
