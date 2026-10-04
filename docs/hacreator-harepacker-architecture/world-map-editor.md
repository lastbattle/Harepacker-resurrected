# World Map Editor implementation

The World Map Editor keeps `Map/WorldMap/*.img` authoritative.  The editor reads
those images into a detached model, applies edits to a clone, validates the
candidate, and saves through the active `IDataSource`.  Unknown properties and
untouched canvas properties remain attached to the source clone so legacy and
modern clients can round-trip without schema cleanup.

## Runtime layers

- `HaCreator.WorldMap` contains the document model, codec, source profile,
  hierarchy index, validation, command history, and source operations.
- `WorldMapAvailabilityIndex` scans only `life/*/{type,id}` for referenced maps;
  NPC and mob lists are derived and are never written under `Map/WorldMap`.
- `HaCreator.GUI.WorldMap` is a native WPF workspace using the shared Harepacker
  theme.  It owns navigation, canvas interaction, inspector fields, diagnostics,
  and localized strings, but no WZ serialization logic.
- **Check all world maps** audits every catalog entry in one pass. It reports
  unloadable images, missing referenced map IDs, broken or cyclic hierarchy and
  navigation targets, duplicate native keys, unknown or invalid marker assets,
  missing/corrupt canvases, and inconsistent fog quest state in the diagnostics
  pane, with per-surface counts and an error/warning summary.
- Referenced map IDs are edited as a list through the shared **Select a map**
  browser. Each row resolves the ID to its map and street names from the active
  map catalog while keeping the native ID visible. Marker types stay numeric in the document but are presented with
  WZ-backed icon labels; values outside the known legacy icon set remain
  source-defined and round-trip unchanged.
- The marker placement tool beside the canvas creates a marker at the clicked
  logical canvas position, accounting for the base-image origin. It remains
  coordinate-accurate while zoomed and can be cancelled with Escape or a
  right-click.
- Markers support direct canvas editing: click to select, drag to move, use the
  arrow keys for one-pixel nudges (Shift for ten pixels), and press Delete or
  use the canvas trash tool to remove the selection. Dragging records one undo
  operation when released; Escape restores the pre-drag position.
- Canvas zoom uses layout-aware scaling so scroll extents and Fit Canvas track
  the viewport. Ctrl+mouse-wheel switches from automatic fitting to manual zoom.
  The canvas footer only exposes implemented display options: grid, labels, and
  raw bounds.
- `WorldMapPreviewCache` converts selected canvas assets to frozen WPF bitmaps
  with bounded source/fingerprint keyed caching.
- `HotSwapRefreshService.WorldMapDataChanged` lets a workspace reload clean
  documents and mark dirty documents for conflict resolution when WorldMap assets
  change externally.

## Source behavior

`WorldMapSourceOperations` is the single source-aware entry point for enumeration,
loading, creation, batch saves, and recoverable deletion.  IMG writes use the
normal atomic serializer and WZ writes remain pending on the owning WZ file until
the normal repack workflow.  Hybrid mode reports its IMG-preferred destination in
the workspace status row.

## Manual verification

1. Open the World Map Editor from HaCreator and enumerate `Map/WorldMap` without
   decoding every background.
2. Open `WorldMap082` and `WorldMap0823` from the modern export; verify the
   Lacheln marker, base origin, links, and marker type 29.
3. Activate the marker placement tool, click an empty canvas position at several
   zoom levels, and confirm the marker inspector reports that position. Cancel
   once with Escape and once with right-click. Select and drag a marker, cancel
   one drag with Escape, nudge with the arrow keys and Shift+arrow, then delete
   it with both the Delete key and canvas trash tool. Confirm undo restores each
   move or deletion. Then add and remove referenced maps with **Select a map**,
   including double-click/Enter from search results, choose a labelled marker
   type, validate, save, and reopen.
4. Select a grouped marker and confirm progressive NPC/mob availability from map
   `life` entries, including missing-asset and categorised-life diagnostics.
5. Toggle grid, labels, links, fog, derived overlays, and raw bounds; verify menu
   and toolbar state remain synchronized.
6. Create a blank surface, duplicate a surface, and review the affected IMG/WZ
   paths before saving.  Delete only after inbound hierarchy references validate.
7. Repeat the read/edit/save flow against a legacy client export and at 100%,
   125%, and 150% Windows DPI scaling.
8. Click **Check all world maps** and confirm every catalog surface receives its
   own diagnostics count, broken links and missing map IDs resolve to their
   owning IMG path, and the status row totals checked maps, errors, and warnings.
