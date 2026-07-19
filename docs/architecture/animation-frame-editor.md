# Animation frame editor

HaCreator's Animation Frame Editor is a native WPF editor for WZ sprite animations. It is available from **Map > Animation frame editor**, **Tools > Animation frame editor**, and the main command bar.

## Supported asset families

| Family | Observed IMG filesystem shape |
| --- | --- |
| Monsters | `Mob/<id>.img/<action>/<frame>` |
| NPCs | `Npc/<id>.img/<action>/<frame>` |
| Reactors | `Reactor/<id>.img/<state>/<frame>` and `<state>/hit/<frame>` |
| Items | `Item/<group>/<prefix>.img/<itemId>/<effect channel>/<frame>` and `Item/Pet/<id>.img/<action>/<frame>` |
| Equipment | `Character/<slot>/<itemId>.img/<action>/<frame>/<layer>` |
| Map objects | `Map/Obj/<oS>.img/<l0>/<l1>/<l2>/<frame>` |
| Map backgrounds | `Map/Back/<bS>.img/ani/<no>/<frame>` and static `back/<no>` |
| Skill effects | `Skill/<job>.img/skill/<skillId>/<effect channel>/<frame>` |

Track discovery is structural. A timeline-capable node has numerically named Canvas, UOL, or composite layer children, so effect and equipment action names do not need to be hard-coded. Item metadata/icon branches are excluded, while nested cash-item effects and pet actions are discovered recursively. Equipment is grouped by its physical Character WZ directory; owner-image slot metadata such as `islot` and `vslot` is preserved unchanged for specialized weapon, accessory, mechanic, dragon, android, saddle, and pet-equipment variants. Nonnumeric sibling properties such as `zigzag`, `flip`, and `z` remain in the animation property inspector.

## Editing model

- The editor works on a detached clone of the selected animation track. Browsing, previewing, undoing, and cancelling do not mutate the live image.
- **Save** atomically replaces only the selected track, marks the owner image changed, and persists it through the active `IDataSource` using the exact category-relative IMG path. A failed write restores the tree, changed flag, and cache tag.
- Numeric frames are serialized in timeline order as `0..n-1`. Unknown frame and animation properties are preserved.
- UOL frames remain links by default. **Make linked frame independent** materializes a direct Canvas copy before editing its pixels or metadata.
- Reactor `info/link` assets are opened against their linked owner image and show a warning in the status bar.

## Coordinate semantics

The Transform section edits frame asset metadata:

- **Origin X/Y** is the Canvas pivot. The client draws the sprite at `world position - origin`.
- **Z order** is the frame/effect-local `z` property. It accepts integer or string layer values.
- **Delay** is milliseconds and defaults to 100 in preview when absent.
- **Alpha start/end** maps to `a0` and `a1`.

Map placement coordinates are a different layer of data. Object `x/y/z/zM/f` and background parallax fields (`x/y/rx/ry/cx/cy/type/a`) remain in the map instance editors; changing a frame origin intentionally affects every use of the asset.

## Preview and timeline

- Pixel-oriented checkerboard preview with grid, bounds, origin axes, zoom, fit, and previous/next onion skin.
- Selecting an asset automatically opens its first animation/action. Preview zoom starts at 150%, centers the WZ origin when a track opens, and remains unchanged while switching assets or tracks; **Fit** remains an explicit command.
- Layered frames are composited by numeric `z`; each Canvas layer can be inspected independently.
- Playback follows each frame's delay and supports speed scaling and looping.
- Frames can be imported, exported, bitmap-replaced, duplicated, deleted, and reordered.
- Dragging in the preview changes the selected layer origin. Arrow keys nudge by one pixel and Shift+arrow nudges by ten.

## AI frame studio

The **AI** inspector tab uses the OpenAI-compatible endpoint configured in **Tools > AI settings**. The settings dialog is global to HaCreator rather than owned by the AI Map Editor and stores separate text/reasoning and image model selections.

- A short request can be expanded into a production sprite prompt by the configured text model.
- The selected WZ frame can be sent to `/images/edits` as a visual reference, or generation can start without an image reference.
- Results can replace the selected frame or add up to eight continuation frames after it. A sequence uses the previous result as the next reference to improve motion continuity.
- Generated changes are collected off-tree and applied as one undoable operation only after every requested frame succeeds. Cancellation and API errors apply no partial frames.
- Existing frame metadata and composite siblings are cloned where possible. UOL frames are materialized before an edit that must own pixels.
- Model-native transparency is optional. The local processor detects and removes edge-connected mattes, clears hidden chroma RGB to avoid resize fringes, trims alpha bounds, adds safe padding, matches reference scale within a 512-pixel output cap, and recalculates the WZ origin from the reference bottom-center anchor.
- The endpoint credential remains in the existing per-user AI settings file and is never written to the repository.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+S` | Save |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
| `Ctrl+L` | Focus asset search |
| `Ctrl+D` | Duplicate frame |
| `Alt+Left` / `Alt+Right` | Reorder frame |
| `Space` | Play / pause |
| `Home` / `End` | First / last frame |
| Arrow keys | Nudge origin |
| `Shift` + arrow keys | Nudge origin by 10 |
| `G`, `O`, `B` | Toggle grid, onion skin, bounds |
| `F`, `0` | Fit, 100% zoom |

## Manual verification

1. Open each supported family and confirm asset filtering and track discovery against the WZ paths above.
2. Preview a multi-frame action, a one-frame background, a UOL frame, and a layered skill effect.
3. Change origin, delay, Z, alpha, and a raw property; verify playback and overlays update immediately.
4. Import, duplicate, reorder, delete, undo, and redo frames.
5. Save in IMG filesystem mode, reopen the track, and verify all changes persisted without changing sibling tracks.
6. Repeat with a WZ data source and verify the owning WZ is marked for repack.
7. Check layout and splitter behavior at 100%, 125%, and 150% scaling and at the 1280×720 minimum size.
8. Switch among English, Traditional Chinese, Simplified Chinese, Korean, and Japanese and verify no resource key is shown as UI text.
9. Open **Tools > AI settings**, discover models, select text and image models, and test the endpoint.
10. In the AI tab, suggest a prompt, generate without a reference, edit with the selected frame as reference, cancel a sequence, and undo/redo the applied result.
11. Verify generated frames have transparent corners, no matte fringe, a scale comparable to the reference, and a stable origin during playback.
