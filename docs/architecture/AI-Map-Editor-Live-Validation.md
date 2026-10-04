# AI map editor live validation

Validated against v95 assets using the production map loader, AI WPF dialog, GPT-6 Astra client, MCP tool server, parser and executor. Source IMG files were never overwritten. Map images now have a 1000-pixel maximum side, preserving aspect ratio without enlarging small crops. Wide maps still require close-up queries to inspect placement accurately.

## Existing maps and live edits

The load/open/close sweep covered 100000000 (Henesys), 100000001, 100010000, 101000000 and 103000000. Loaded element counts matched source data, including backgrounds, tiles, objects, NPCs, monsters, ropes and footholds. Reactor coverage used 100000100. The actual AI dialog was shown, closed, reopened and disposed; boards were removed and source hashes remained unchanged.

| Map | Actual prompted result | Independent verification |
| --- | --- | --- |
| 100000000 | Move the leftmost NPC 32 pixels right and add a flower | Only the taxi moved among existing elements; its Y followed the slope. Flower placement was visually inspected. |
| 100010000 | Add a raised platform with a connecting rope | Added 18 tiles, three rope artwork objects, one foothold and one rope; no original element changed or disappeared. |
| 100000100 | Move a reactor, add a snail and translucent cloud | Reactor moved 24 pixels with its other properties preserved; original backgrounds remained intact. |
| Empty board | Create a small training map | Size 2400×1600; VR left/top/right/bottom = -550/-350/1750/1150. Added ground, raised platform, climbable rope and artwork, spawn portal, snail and two backgrounds. Gameplay artwork was inside VR. |

The first reactor prompt exposed missing reactor context; repeating it after adding reactor state succeeded. The blank-map run exposed duplicate layer membership and a null-tileset redo failure. Those lifecycle issues were fixed and the original model commands replayed against the final implementation.

Final replay of all four scenarios compared every spatial-state page, complete undo/redo snapshots and rendered PNGs. Undo restored the initial state; redo restored the final state, with byte-identical final and redo PNGs. Existing-map source hashes remained unchanged.

Local evidence is under `artifacts/ai-final-validation`: `checks.json`, per-scenario results, full snapshots and PNGs. `check_results.py` independently checks the recorded results. These temporary artifacts are not required to build the application.

## Regression coverage

The targeted AI suite passed 83 tests, with no failures or skips, including real v95 placement tests. HaCreator built successfully. Coverage includes actor previews, background camera coordinates, state pagination and bounds, exact selectors, culture-independent parsing, empty text properties, artwork ordering, map settings, VR lifecycle and empty-layer undo/redo.

## UI verification steps

1. Open Henesys and its AI Map Editor; inspect the scenic overview, then request a crop around an NPC. Close and reopen the dialog.
2. Request a small NPC move and decoration. Inspect their grounding and confirm other content stays in place.
3. Request a platform and connecting rope; inspect both collision overlays and visible artwork.
4. On a reactor map, request a reactor move and verify identity and properties through state readback.
5. On an empty board, set map dimensions and VR, create terrain and a spawn portal, then Undo and Redo the complete session. Confirm imagery, geometry and layer membership are restored.

## Limits

Previews use static frames/time zero and do not render Spine or simulator effects. They include background pages and screen modes without runtime filtering, and editor portal markers remain visible. These checks used production loading and the actual AI dialog, but did not establish gameplay correctness in MapSimulator. Live prompt completion took approximately one to two minutes; this is not an instant-generation guarantee.

`set_map_size` currently accepts widths 600–5000 and heights 400–5000. `set_vr` sets camera boundaries; physical boundaries still require collision geometry. Both are undoable and readable through `get_map_state`.
