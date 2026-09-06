# Compact map edits benchmark

Baseline: 2026-09-06, commit 1b87d266, Windows, Python tiktoken o200k_base. Token counts are a proxy, not the exact GPT-6 tokenizer. Serialization measurements exclude image payloads.

Metrics: UTF-8 bytes and proxy tokens for equivalent edits and tool schemas. Preserve operation order, validation, geometry, query prerequisites, partial failure reporting, cancellation and undo/redo. Legacy MCP tools and command parser remain supported.

## Run 0 — before implementation

Measured with Python tiktoken.encode on the supplied six-command example and kind=command entries in the prior Henesys snow-reference JSONL corpus.

| Dataset | Characters | UTF-8 bytes | Proxy tokens |
|---|---:|---:|---:|
| User six-command fixture | 485 | 485 | 181 |
| Prior Henesys live command corpus | 6636 | 6636 | 2589 |

Decision: introduce a compact batch adapter; keep commands local for review. The old corpus lacks original tool arguments, so do not claim a reconstructed API traffic comparison. Prior correctness baseline: 102 targeted tests passed in the preceding phase.

## Run 1 — compact adapter with complete inline action guide

2026-09-06. Exported strict Responses schemas with `HACREATOR_COMPACT_BENCHMARK`, measured minified UTF-8 using the same tokenizer. Legacy export excludes the new batch tool.

| Payload | Bytes | Proxy tokens |
|---|---:|---:|
| compact-tools | 33718 | 8267 |
| legacy-tools | 40732 | 9793 |
| compact user fixture | 191 | 89 |

Correctness: 19 compact batch, review, legacy MCP and API conversation tests passed. Production build passed. Decision: keep batch transport and review rows; evaluate on-demand detailed action help to avoid repeating unused parameter descriptions.

## Run 2 — on-demand detailed action help

2026-09-06. Same schema export and tokenizer. Compact reference retains types, enums, bounds, required keys and essential placement semantics; full descriptions are available through get_edit_help.

| Payload | Bytes | Proxy tokens |
|---|---:|---:|
| compact-tools | 21255 | 5259 |
| legacy-tools | 40732 | 9793 |

Correctness: 116 targeted tests passed, none skipped, using v95 assets. Includes batch preflight, nested coordinates, bounds, cancellation, partial failure, strict schemas, API reasoning preservation and review replay protection. Decision: keep; test the exact Henesys conversion with the live API before further compression.

## Live run — Henesys, compact interface

2026-09-06. Production saved GPT-6 Astra settings; exact prompt: “Turn this entire map into the snowey village like EL Nath.” v95 map 100000000, disposable board. Total harness live-edit phase including evidence and review replay: 199.46 seconds (not a controlled latency comparison with earlier runs).

90 successful action rows across 3 edit_map calls; 14 API requests. Provider-reported cumulative input tokens: 998518, output tokens: 3222. These include repeated history, image inputs and caching; they are not unique context tokens. Largest request: 6825596 bytes including images.

All 621 original terrain tiles rethemed; final 692 tiles, 52 objects, four backgrounds. All 25 NPCs, 27 portals, 232 footholds and two ladders preserved. Structural retheme acceptance, source-file hash, complete undo/redo, PNG byte equality, window close/reopen/disposal passed. Review UI applied the first three selected rows, then the remainder; produced the identical live map; repeating Apply made no changes.

Inspected overview and left/center/right crops. Snowy terrain, buildings and backgrounds cover the map. Some stair/edge artwork remains approximate against preserved collision; this is not a claim of perfect artwork alignment or simulator playability. The large mushroom billboard belongs to retained functional scene content.

The captured action rows retain expanded arguments, not the original grouped model code. Their compact serialization measures per-row encoding only; no claim about actual grouped API output savings is made from this corpus.
- Equivalent command rows: 8402 bytes, 3294 proxy tokens.
- Equivalent compact rows: 7900 bytes, 3549 proxy tokens.

## Run 3 — grouped export and compact placement receipts

2026-09-06. Same exported schemas; live run 1 command receipts and canonical action arguments. Export groups only consecutive identical placements, preserving operation order. Receipt compression removes repeated asset identifiers while retaining actual anchors, tile variants, layers, origin adjustments and warnings.

| Payload | Bytes | Proxy tokens |
|---|---:|---:|
| compact-receipts.txt | 9475 | 3192 |
| compact-tools.json | 21245 | 5257 |
| grouped-edits.txt | 3309 | 1473 |
| legacy-receipts.txt | 12955 | 4585 |
| legacy-tools.json | 40727 | 9792 |

Correctness: 119 targeted tests passed, no skipped tests. Includes grouped export/import equivalence and receipt diagnostics. Decision: keep grouping and receipt compression. Per-row key shortening alone regressed proxy tokens (3,294 to 3,549 on the live corpus); grouped export addresses repetition. Further description/diagnostic stripping would risk placement correctness, so stop local syntax compression and investigate conversation/state ownership instead.

## Architecture audit — next optimization targets

2026-09-06. Live run 1 initial context is 47344 UTF-8 bytes / 22616 proxy tokens, before the separate system prompt, tools and images. Largest summary sections:

- Tiles: 15341 proxy tokens.
- Platforms (Footholds): 5302 proxy tokens.
- Portals: 704 proxy tokens.
- NPCs: 626 proxy tokens.
- ASCII Map Visualization: 318 proxy tokens.
- # Map Summary for AI Editing: 159 proxy tokens.
- Map Settings: 89 proxy tokens.
- Element Counts: 70 proxy tokens.

Cumulative unique query-result text generated during that run (not resend totals):
- get_map_state: 22784 proxy tokens.
- get_object_info: 11522 proxy tokens.
- get_reference_map: 7227 proxy tokens.
- get_bgm_list: 1911 proxy tokens.
- get_tile_info: 730 proxy tokens.
- get_asset_sets: 694 proxy tokens.
- get_background_info: 133 proxy tokens.

These are the next targets; no reduction from these architectural changes is claimed in this phase.

1. Replace the monolithic initial summary/ASCII map with a scene brief: map identity, revision, bounds, counts, selection and settings. Fetch scoped geometry only when needed. Keep the detailed serializer as a diagnostic adapter.
2. Store the authoritative scene and an edit journal in the editor. Give elements stable session IDs and send versioned patches, rather than repeatedly treating full serialized snapshots as working memory. A revision conflict must reject stale edits and trigger a fresh read.
3. Maintain an explicit visual working set: overview, active camera crop and relevant asset contact sheets. Superseded images and query pages should leave the active model context through a supported conversation-compaction boundary, retaining essential measurements and valid reasoning/tool-call associations. Do not simply delete arbitrary API items.
4. Use a disposable draft board for review mode. Render and validate the proposed result before user Apply, then apply a checked scene diff transactionally. This replaces the current fundamental limitation that review queries still see the original map. It requires clone fidelity, deterministic diffing, asset ownership and undo tests.
5. Move repetitive theme conversion and geometric fitting into measured scene operations (theme palette, portal/building alignment, terrain support). Let the model choose intent and inspect results while deterministic code solves repeated placements. Keep individual edit adapters for precise corrections.

Evaluate the redesigned scene workflow on identical real maps with time-to-first-preview, cumulative input/output usage, bytes, request count, visual crop review, actor/collision invariants and exact undo/redo. Avoid claiming architectural improvements from smaller serialized strings alone.

## Live run 2 — grouped import and shorter receipts

2026-09-06. Same exact Henesys prompt and saved runtime settings. 166.61 seconds including evidence and review replay; 75 applied actions. 11 API requests, 707456 cumulative provider input tokens and 2955 output tokens. This is an independent stochastic run, not a controlled latency benchmark.

Structural retheme acceptance, source hash, undo/redo and PNG byte equality passed. Grouped compact import reproduced all original commands; selected application, complete replay equality and duplicate Apply protection passed. Screenshot review caught a separate existing localization defect: the localizer replaced bound TextBlock text and Expander headers, so rows still displayed Ready after their data state became Applied. The fix skips bound properties and walks the logical tree of authored controls rather than generated template visuals; a regression test and real visual-tree assertions are added. This explains why checking only the data model was insufficient.


## Final verification

2026-09-06. Production build and 120 targeted tests passed, with v95 assets and no skipped tests. Offline replay of the second live run through the production import/checklist workflow reproduced all 75 commands and the exact live map. Both actual visible row labels and rendered header counters now update correctly after applying; minimum-width (900 px) screenshots were inspected. Source hash, complete undo/redo, PNG equality, window lifecycle and duplicate-apply checks passed. Failed execution receipts retain their command and diagnostic locally but cannot replay automatically. Privacy scan of added/changed source and documentation found no installation paths, private gateway or credentials.

Final proxy-token reductions: supplied six-command fixture 181 to 89 (51%); equivalent grouped live edit corpus 3,294 to 1,473 (55%); strict tool schemas approximately 9,793 to 5,259 (46%); live placement receipts 4,585 to 3,192 (30%). Schema snapshots vary by a few tokens after minor guide text normalization. Full benchmark rows above are authoritative. No claim is made that the whole request or billing shrinks by these percentages: images, detailed initial map state, query results and repeated history remain substantial.
