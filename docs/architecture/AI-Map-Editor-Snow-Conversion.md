# Whole-map winter conversion validation

The exact prompt “Turn this entire map into the snowey village like EL Nath.” now produces a substantial visual conversion of v95 Henesys (100000000) through the production client and MCP tools. The final live run used saved application settings: GPT-6 Astra, low reasoning, 40 tool turns and 16,000 maximum output tokens. Source IMG files were not overwritten.

## Verified result

The agent found El Nath (211000000) through `get_reference_map`, retrieved its actual asset usages, and selected its houses, scenery and backgrounds. It rethemed all 621 original tiles in one operation, then added supporting snow artwork and replaced the village scenery. The final run completed in approximately 362 seconds with 71 undo batches. This is a measured test result, not an instant-generation guarantee.

| Element | Before | After | Original entries unchanged |
| --- | ---: | ---: | ---: |
| Tiles | 621 | 690 | 0 |
| Objects | 206 | 44 | 0 |
| Backgrounds | 8 | 4 | 0 |
| NPCs | 25 | 25 | 25 |
| Portals | 27 | 27 | 27 |
| Footholds | 232 | 232 | 232 |
| Ladders | 2 | 2 | 2 |

The full-map acceptance check passed: terrain, scenery and backgrounds changed substantially, replacement artwork exists, and functional spatial entries remained unchanged. Undo restored the initial snapshot; redo restored the final snapshot and byte-identical PNG. Source counts matched, the source hash stayed unchanged, and the actual AI dialog closed/reopened successfully. Matching left, center and right crops were visually inspected. Local evidence is under `artifacts/ai-snow-reference`, including `results.json`, `100000000-acceptance.json`, full spatial snapshots, asset previews, tool results, crop grids and the final dialog screenshot.

## Problems fixed

The original implementation reproduced the reported failure: only BGM and the snow flag changed, followed by a request for asset lists. Henesys exceeds the 48,000-character initial summary limit, which could truncate the catalog. Tools required known set names but offered no way to list them. The old temporary harness separately appended the catalog and concealed that production failure.

- `get_asset_sets` now lists/searches exact loaded tile, object and background set names with bounded pagination. Initial context and tool descriptions direct the agent to this query even when the summary is truncated.
- `get_reference_map` searches actual map names and retrieves exact source-map asset usages, including preview-ready identifiers. Named style requests no longer depend on interpreting abbreviated asset names.
- `change_tileset` rethemes existing terrain in one undoable operation. It validates categories and scale, preserves original asset references, and can translate artwork anchors when native collision shapes match by translation. Explicit `allow_shape_mismatch=true` permits visual retexturing with different native templates while preserving actual map collision and reporting affected categories for crop review. Missing categories and scale mismatches still fail before mutation.
- `add_object(create_bindings=false)` places replacement decoration without creating native footholds or chairs. Existing behavior remains the default. An earlier run spent many actions removing newly generated geometry; the final run could request visual-only additions directly.
- The prompt now covers complete visual theme changes, source-map references, functional geometry preservation and inspection across the map.

A stricter intermediate terrain implementation caused a snow overlay rather than full replacement. The new harness correctly rejected that result. Dataset checks showed that the snowy sets match 25 of woodMarble's 27 native template variants; the two cap variants require deliberate visual retexturing. Actual map footholds remain authoritative.

## Harness and tests

The reusable [AIMapHarness](../../tools/AIMapHarness/README.md) uses the same initial context and saved runtime options as the actual dialog. It records exact prompts, query results and asset images; compares every spatial-state page; saves matching before/after crop grids; and checks undo/redo, rendered PNGs and source hashes. Optional retheme acceptance requires changes to at least 80% of each original visual type, replacement artwork, and unchanged functional spatial entries. This structural check complements visual review; it does not grade artistry.

The targeted AI suite passed **102 tests**, with no failures or skips, including real v95 reference discovery and tile compatibility checks. HaCreator and the harness built successfully.

## Manual verification and limits

Open Henesys and the AI Map Editor, use the quoted prompt, and inspect the result across the left, center and right sections. Check snowy surfaces against collision overlays, entrances against portals, and climbing artwork against ladder endpoints. Undo and redo the session, then close/reopen the dialog.

Thirty-one original cap tiles use different native templates; their visual collision alignment is not guaranteed by retexturing alone. The agent inspected and supplemented artwork, but gameplay simulation was not performed. Henesys's large mushroom noticeboard is NPC artwork, and some portals have their own visual frames; preserving those functional entities preserves their artwork. Previews remain static frames rather than simulator output. These results establish an actual whole-map transformation, not universal artistic perfection.
