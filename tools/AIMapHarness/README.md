# Live map editing harness

Runs the production loader, AI dialog, client and MCP executor against exported IMG maps. Uses the application's saved AI settings without saving changes to settings. Live mode sends map context and rendered artwork to that configured API and incurs its normal usage. Source IMG files are read only; edits are made on disposable boards.

```powershell
$env:HACREATOR_AI_TEST_DATA = '<path-to-v95-export>'
$env:HACREATOR_EXISTING_MAP_OUTPUT = '<new-empty-artifact-directory>'
$env:HACREATOR_TEST_MAPS = '100000000'
$env:HACREATOR_LIVE_EDITS = '1'
$env:HACREATOR_REQUIRE_RETHEME = '1'
$env:HACREATOR_LIVE_PROMPT = 'Turn this entire map into the snowey village like EL Nath.'
dotnet run --project tools/AIMapHarness/AIMapHarness.csproj
```

Omit `HACREATOR_LIVE_EDITS` for loading/rendering/dialog lifecycle checks without API requests. Omit `HACREATOR_REQUIRE_RETHEME` for local edits; when enabled it requires changes to at least 80% of each original visual type, replacement artwork, and unchanged functional spatial entries. It does not judge artistic quality. Set `HACREATOR_SCRATCH_TEST=1` to exercise an empty board. For deterministic command replay, set `HACREATOR_REPLAY_DIRECTORY` to an earlier run's directory containing `<map-id>-live-calls.jsonl`; replay makes no API calls. Use a fresh output directory for each run.

The initial context is exactly the production `GenerateAISummary` output, without separately appending asset catalogs. This matters: the old temporary harness concealed truncated catalog failures on large maps.

Evidence includes the exact prompt/context, tool queries and returned text, asset contact sheets, before/after world-space crop grids, complete paginated spatial state, per-element-type change counts, undo/redo snapshots, rendered PNGs, source hashes and dialog lifecycle checks. Runtime metadata excludes endpoints and credentials. Local logs can contain map-authored content; review artifacts before sharing.

Inspect `results.json`, `<map-id>-comparison.json` and the crop grids together. A completed response or a music/weather change does not establish a visual conversion. Compare terrain, buildings and background changes across every region, and check functional geometry and actors. Static visual checks do not establish simulator playability.


Compact-edit runs also record expanded action arguments, grouped-copy compatibility and numeric request/usage metrics without endpoint or authorization metadata. After successful undo/redo checks, the harness undoes the live edit, imports its grouped compact representation into the production review checklist, applies the first three selected rows and then the remainder, and checks that the result exactly matches live execution. It tests a duplicate Apply and saves ready/applied UI screenshots.

For repeatable serialization measurements without an API call, set `HACREATOR_COMPACT_BENCHMARK` to an output directory and optionally `HACREATOR_COMPACT_CORPUS` to a captured live-calls JSONL file, then run the built harness. It exports legacy/compact tool schemas, receipts and equivalent grouped edits for tokenizer analysis. Unset the benchmark variable before map runs. Tokenizer results in the benchmark report use `o200k_base` as a proxy; provider usage metrics are recorded separately.
