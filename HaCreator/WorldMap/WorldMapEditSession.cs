using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.WorldMap;

namespace HaCreator.WorldMap;

public sealed class WorldMapChangeSet
{
    public WorldMapChangeSet(string description, WorldMapDocument before, WorldMapDocument after)
    {
        Description = description ?? string.Empty;
        Before = before?.DeepClone() ?? throw new ArgumentNullException(nameof(before));
        After = after?.DeepClone() ?? throw new ArgumentNullException(nameof(after));
    }
    public string Description { get; }
    public WorldMapDocument Before { get; }
    public WorldMapDocument After { get; }
    public void Apply(WorldMapDocument document) => document.ReplaceFrom(After);
    public void Revert(WorldMapDocument document) => document.ReplaceFrom(Before);
    public static WorldMapChangeSet FromSnapshots(string description, WorldMapDocument before, WorldMapDocument after) => new(description, before, after);
}
/// <summary>Small snapshot-based undo/redo session suitable for coalesced canvas edits.</summary>
public sealed class WorldMapEditSession
{
    private readonly Stack<WorldMapChangeSet> _undo = new();
    private readonly Stack<WorldMapChangeSet> _redo = new();
    private readonly WorldMapDocument _document;

    public WorldMapEditSession(WorldMapDocument document) => _document = document ?? throw new ArgumentNullException(nameof(document));
    public WorldMapDocument Document => _document;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsDirty { get; private set; }
    public IReadOnlyCollection<WorldMapChangeSet> UndoStack => _undo;
    public IReadOnlyCollection<WorldMapChangeSet> RedoStack => _redo;

    public WorldMapChangeSet Record(string description, Action<WorldMapDocument> edit)
    {
        if (edit == null) throw new ArgumentNullException(nameof(edit));
        WorldMapDocument before = _document.DeepClone();
        edit(_document);
        WorldMapDocument after = _document.DeepClone();
        var change = new WorldMapChangeSet(description, before, after);
        _undo.Push(change); _redo.Clear(); IsDirty = true; _document.IsDirty = true;
        return change;
    }

    public void Apply(WorldMapChangeSet change)
    {
        if (change == null) throw new ArgumentNullException(nameof(change));
        change.Apply(_document); _undo.Push(change); _redo.Clear(); IsDirty = true; _document.IsDirty = true;
    }

    public void Execute(WorldMapChangeSet change) => Apply(change);
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        WorldMapChangeSet change = _undo.Pop(); change.Revert(_document); _redo.Push(change); IsDirty = _undo.Count > 0; _document.IsDirty = IsDirty; return true;
    }
    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        WorldMapChangeSet change = _redo.Pop(); change.Apply(_document); _undo.Push(change); IsDirty = true; _document.IsDirty = true; return true;
    }
    public void MarkSaved() { IsDirty = false; _document.IsDirty = false; }
    public void ClearHistory() { _undo.Clear(); _redo.Clear(); IsDirty = false; }
}
