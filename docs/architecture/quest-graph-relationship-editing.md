# Quest graph relationship editing

The Quest Editor graph supports a deliberately small writable relationship
surface. It can add, update, remove, undo, and redo:

- `nextQuest` actions in the start or completion phase;
- quest-state requirements in the start or completion check phase.

Dialogue branches, non-quest requirement predicates, and Act quest-state
mutations remain read-only. Act quest-state entries change another quest's
runtime state and are not prerequisites, even though both contain quest IDs.

## Lossless mutation boundary

Graph relationship commands update the in-memory editor model and the matching
raw Act or Check property together. They do not call the Quest Editor's full
quest serializer. The full serializer rebuilds the Info, Say, Act, and Check
subtrees and therefore cannot guarantee preservation of properties the editor
does not model.

Each editable graph edge carries a structured address containing its owner,
phase, model index, and requirement index. Commands use that address instead
of parsing a display label or provenance string. A command refuses malformed
or ambiguous source data and rolls back both representations when either side
cannot be changed.

Only the owning Act or Check image is marked as updated. Repack remains the
durability boundary for the loaded quest data.

## Validation and history

Relationship commands reject invalid targets, self-links, duplicate links,
ambiguous phase containers, and cycles. A preview is shown before a graph edit
is applied. The graph keeps an operation history for relationship commands;
undo and redo apply the inverse mutation to both the editor model and the raw
property tree.

## Manual verification

1. Open the Quest Editor and select the Graph tab.
2. Add a completion `nextQuest` link to a loaded quest.
3. Edit and remove the new edge from its graph context action.
4. Exercise undo and redo using both toolbar buttons and keyboard shortcuts.
5. Add start and completion requirements with different quest states.
6. Confirm self-links, duplicates, and cycles are rejected without changing
   the graph.
7. Repack, reload the data, and confirm the relationships remain identical.
8. Confirm unrelated Info, Say, Act, and Check properties are unchanged.
