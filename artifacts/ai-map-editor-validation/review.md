# GPT-6 Astra map editor review and validation

The AI Map Editor now uses a single live GPT-6 Astra Responses loop with map artwork, exact asset contact sheets, paginated geometry, execution feedback, and visual reinspection. This replaces the active text-only orchestration route.

## Problems corrected

- The old planner and specialist agents each worked from the original map state, without images. The active chat now uses the same board-aware visual tools as external MCP clients.
- Conversation context previously selected early exchanges and was not passed to specialist execution. The current request receives recent user/assistant history and fresh map state, with applied commands identified to prevent replay.
- Successful tool actions lost the model's final explanation; the client now preserves it and delivers real progress. Artificial delayed text playback was removed.
- Tile platform placement bypassed undo and collision. Native tile foothold offsets now define the walking surface. The v95 grassySoil case had a 12-pixel mismatch under the old heuristic.
- Layer preparation could route artwork away from its collision; native object placement could reset an explicitly selected layer. Placement preserves and reports the actual layer.
- Four slope variants used mismatched pieces and hardcoded offsets, producing disconnected art. Native segment endpoints now connect each incline and its filled underside.
- Rope redo failed to restore the serialized rope collection. Native object geometry could duplicate footholds on redo. Both are covered by actual-asset roundtrips.
- Coordinate-selected MOVE could ignore source coordinates and move all matching types. Moves now retain separate source/destination coordinates, and mutation tools require exact, unambiguous selectors with optional layer filters.
- Flips, clear, deletion and attached geometry now participate in undo. Collapsed groups preserve operation order when switching between undo and redo.
- Failed asset queries incorrectly satisfied query-before-edit checks. Catalog lookups now respect IMG lazy loading; failures remain failures.
- Optional strict tool fields, required arguments, types and command-string boundaries are validated. Rich image tool outputs remain images through both MCP and the Responses client.
- Responses reasoning now uses reasoning.effort. The selected request timeout is no longer silently shortened by HttpClient's default timeout.
- The old Run Tests menu scored generated strings without testing the map. It was removed in favor of executable regression tests and real-asset harness validation.

## Live model validation

Used the user-authorized temporary endpoint with gpt-6-astra, Responses, low reasoning. The key was passed through a temporary process environment; it was not saved in the repository or application settings. The harness used production MapAIExecutor, MapAIVisualRenderer, MapMcpToolServer and OpenAICompatibleClient against the actual exported v95 IMG source.

1. Start with the existing grassy ground at y=0.
2. Request a 360-pixel raised platform from x=0 to x=360 at y=-180, a rope at x=180 reaching y=0, and an asset-previewed flower clear of the rope.
3. Ask in a second conversation turn to move that flower to x=60 on the same platform.
4. Inspect resulting imagery and geometry. The model caught and corrected its initial movement origin/bottom confusion. The move tool description was subsequently clarified.

Independent checks in checks.json confirm exactly one object at x=60 with bottom y=-180, two footholds, one rope, and byte-equivalent non-object geometry before and after the follow-up. The two-turn run took 103 seconds and issued 18 tool calls, including five mutation calls. This demonstrates working visual feedback and correction; it is not a claim of instantaneous model latency or in-game playability.

Artifacts: before.png, first-after.png, after.png, slope.png, after-state.json, checks.json, calls.jsonl, final-before-followup.txt, final.txt, and ui-after.png. Logs contain tool metadata rather than image base64 or credentials.

## Manual verification in HaCreator

1. Open a v95 map and AI Map Editor. Check the fitted artwork preview and Geometry tab; click the preview and confirm a world coordinate is inserted into the prompt.
2. Request a raised grassy platform and connecting rope. Watch tool progress and live changes; compare artwork with foothold/rope overlays.
3. Move one decoration through a follow-up. Confirm adjacent objects stay in place, then Undo and Redo.
4. Turn off Apply as it works. Generate a request, review its commands, apply once, and confirm the same batch cannot be applied twice.
5. Press Stop during a request. Completed edits remain available to undo; later actions must not run.
6. Inspect get_map_view, get_map_state and get_asset_preview through the local authenticated MCP connection.

## Limits

The preview renders static artwork and collision overlays. Camera backgrounds, parallax, animation, Spine and simulator effects are explicitly omitted. No full movement simulation or broad artistic-quality benchmark was run. The test harness creates an in-memory board and does not overwrite the source IMG assets.

## Automated verification

Final focused run: **40 passed, 0 failed, 0 skipped**, with HACREATOR_AI_TEST_DATA pointing to the actual v95 export. HaCreator Debug builds passed. Coverage includes Responses image/feedback transport, Stop between mutations from the same model response, registry/selector validation, geometry pagination, renderer transforms, all four slopes, real asset undo/redo, and map/portal property undo including nested operation groups.

```powershell
$env:HACREATOR_AI_TEST_DATA = '<path-to-v95-export>'
dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj --filter 'FullyQualifiedName~AIConversationProtocolTests|FullyQualifiedName~AIMapToolTests|FullyQualifiedName~MapMcpToolTests|FullyQualifiedName~AIPlacementDatasetTests|FullyQualifiedName~MapAIPlacementParserTests|FullyQualifiedName~AIVisualRendererTests|FullyQualifiedName~AIMapPropertyUndoTests'
```

Map-setting and portal-property changes now restore their original values through undo, including nullable values, both BGM representations, map dimensions, and absent or replaced VR geometry. Consecutive live AI operations are grouped without absorbing intervening manual edits; composite groups redo in chronological order.

The final save-path regression also checks actual MapSaver.SaveFootholds output. All four slope directions form contiguous prev/next chains before and after undo/redo; coincident native anchors are joined within each generated structure. The final rerun remained 40/40 passing with no skipped dataset tests.
