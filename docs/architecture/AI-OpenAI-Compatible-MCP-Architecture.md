# HaCreator AI: OpenAI-Compatible API and MCP Architecture

## Objective

Allow a user to prompt HaCreator to create or edit a MapleStory map through any API that follows the OpenAI request format. OpenRouter is the default endpoint preset, while a private RPC gateway can use the same client by changing the base URL and API dialect.

## Design

### One tool registry

`MapEditorFunctions` remains the canonical registry for the 38 map tools. `MapMcpToolServer` projects that registry into:

- MCP `tools/list` and `tools/call` JSON-RPC messages.
- Chat Completions function tools.
- Responses API function tools.

The parity test compares MCP names and schemas against the registry so a tool cannot silently disappear from one interface.

### Visual and spatial queries

- `get_map_state(x?, y?, width?, height?, element_type?, offset?, limit?)` returns paginated geometry as JSON. Supply all four region fields or omit them; `element_type` accepts `all`, `tile`, `object`, `foothold`, `rope`, `ladder`, `mob`, `npc` or `portal`. Pages default to 100 entries and are bounded to 200. Results include match counts, `nextOffset`, global geometry bounds unaffected by filtering, and exact tile/object paths. Diagnostic source indices are collection indices, not stable mutation IDs. Backgrounds, chairs, reactors and tooltips are explicitly excluded. Coordinates use map pixels, positive X right and positive Y down. Existing content bounds are descriptive, not a restriction against expanding a map. The initial prose summary is capped at 48,000 characters with an explicit truncation marker directing the model to this query.
- `get_map_view(x?, y?, width?, height?, maxDimension?, overlays?)` renders a map overview or crop. Supply all four crop fields together, or omit all four. The default output dimension is 1200 pixels; accepted limits are 256–1600. Overlays default to true and expose footholds and climbing geometry. Camera-dependent backgrounds and Spine are omitted and identified in the result; static previews cannot verify animation or simulator behavior.
- `get_tile_info(tileset, category?, offset?, limit?)` lists actual variants with size, origin, Z and raw foothold offsets. The default page size is 80, bounded to 1–200, and offset defaults to zero. Results identify the next offset. A 90×60 grid is common but cannot be assumed for every asset.
- `get_asset_preview(assets, maxDimension?)` returns an indexed contact sheet of 1–16 exact asset paths. Tile entries use `{type:"tile",tS,u,no}`, objects use `{type:"object",oS,l0,l1,l2}`, and backgrounds use `{type:"background",bS,backgroundType,no}` with `backgroundType` equal to `back` or `ani`. Discover paths with catalog queries before requesting previews. Animated assets use their first frame.

`MapAIVisualRenderer` runs on the board UI thread. `RichQueryExecutor` returns standard MCP text/image blocks, or null to use the text query callback. `MapMcpToolCallResult.Content` preserves those blocks; `Text` provides extracted text for logs and text-only consumers. This lets the embedded client and external MCP callers consume the same images without decoding an image hidden inside a text result.

Strict schemas represent optional fields as nullable while requiring their presence in the wire schema. The tool server removes top-level null arguments before invoking existing handlers and rejects omitted required arguments, wrong primitive types, unknown arguments, invalid enums and out-of-range values. Action strings containing double quotes or line breaks are rejected because the command grammar has no quoted-string escaping. Failed discovery queries do not unlock their associated mutation tools.

`tile_platform` and `tile_structure` support `create_foothold` (default true). Flat/tall platform coordinates describe the world walking surface; native collision offsets determine image origins. Single tile placement remains visual-only at its native origin. Inspect the resulting collision and imagery together with `get_map_view`.

Single-element move, remove and flip tools require complete current-origin coordinates, or a nonempty portal name where supported. Names cannot select non-portals, and names cannot be mixed with coordinates. A layer filter disambiguates layered elements. For moves, `from_x/from_y` are stored editor origins; destination `to_y` for objects and portals is the desired bottom world Y, while other element types use destination origin Y. Tile move/remove operations are supported; rope and ladder movement is not advertised and requires explicit removal/recreation. Missing or ambiguous selectors must not fall through to type-wide mutations; `clear_elements` is the separate explicit bulk operation.

### Provider-neutral transport

`OpenAICompatibleClient` supports both Chat Completions and Responses-style endpoints. It handles repeated tool-call turns, required discovery queries, strict-schema projections, cancellation, timeouts, and API error reporting. The endpoint is formed from `BaseUrl` unless the user supplies a complete `/chat/completions` or `/responses` URL.

### Model discovery in AI Settings

The AI Settings dialog presents one merged **Model catalog**. Built-in entries are labeled `Built-in`, endpoint entries are labeled `Endpoint`, and duplicates are labeled `Built-in + endpoint`; custom model IDs can still be typed directly. When the dialog opens, when the base URL or API key loses focus, or when the user presses **Refresh**, it calls the configured OpenAI-compatible `GET /models` endpoint and merges the result into the catalog.

Reasoning Effort is model-aware. The client consumes optional reasoning metadata from `/models`; when a standard endpoint only returns model IDs, the dialog safely infers the common levels for known reasoning-model families and otherwise offers `Auto (model default)` only. This avoids sending unsupported `reasoning_effort` values to general-purpose models.

### MCP endpoint

Each open AI Map Editor window hosts a loopback MCP endpoint at a dynamically selected `127.0.0.1` port. The Tools menu displays the endpoint and bearer token for an external MCP client. The endpoint is restricted to loopback and requires the generated bearer token.

MCP action calls are dispatched to the WPF UI thread, parsed by `MapAIParser`, applied by `MapAIExecutor`, and reported back to the caller. Query-before-action requirements remain enforced by the shared tool server.

### Autonomous editing and undo

Automatic application is enabled by default and can be disabled in AI Settings. One continuous tool loop applies successful calls to the live board as they arrive, so the next `get_map_state` or `get_map_view` sees those edits. The embedded client uses an ephemeral tool server with the same board callbacks and rich image contract as the external MCP endpoint. A final visual query lets the model verify its result before reporting completion. Commands already applied by the loop are not applied again at the end of the turn. Review mode records successful tool calls for later application.

Map previews display world coordinates; clicking a preview inserts the corresponding world position in the prompt. Stop is checked between actions, preserving completed edits and preventing subsequent actions. Undo continues to use the editor's normal reversible board operations.

Exposed map settings and portal edits register explicit old/new value callbacks in the undo system. Only touched fields are restored, preserving nullable settings, the separate legacy and typed primary BGM values, and unrelated ambient audio. VR restoration removes/recreates its normal rectangle and control points, including an initially absent VR. Collapsed AI sessions retain child undo batches and reverse their order for redo, so repeated edits to one setting or item return to the final state correctly.

### GPT-6 configuration

Map editing sessions use GPT-6 Astra with the Responses API. New settings use that default, and legacy model/protocol selections are normalized when a map request starts. Existing endpoint and credential configuration is preserved. GPT-6 requires Responses rather than Chat Completions. Responses reasoning uses `reasoning.effort`; `reasoning_effort` is the Chat Completions field. The generic transport remains available internally, while this map workflow targets GPT-6. See the [OpenAI function calling guide](https://developers.openai.com/api/docs/guides/function-calling) for Responses tool calls and their outputs.

## Operational safeguards

- Keep the MCP listener on `127.0.0.1`; do not bind it to a LAN interface without adding explicit authentication and origin policy.
- Treat the bearer token and API key as secrets. The current settings file preserves the existing local settings behavior; OS-backed secret storage can be added independently.
- Keep automatic edits reversible through the normal board undo/redo system.
- Keep the tool loop bounded by `MaxToolTurns` and request timeouts.

## Validation

- `dotnet build HaCreator/HaCreator.csproj -c Debug --no-restore` succeeds.
- `MapMcpToolTests` checks 38-tool parity, discovery-query enforcement, and command parity.
- `AIMapToolTests` checks image preservation, single query dispatch, failed-query gating, argument validation, strict optional-field behavior and bounded spatial queries.
- `AIMapPropertyUndoTests` checks scalar/null settings, BGM state, portal fields, map dimensions, VR lifecycle and repeated/nested collapsed value edits.

### UI verification

Open a v95 map and the AI Map Editor. The preview fits current content; click it to insert a world coordinate. Request a platform, connecting rope, and decoration, then use a follow-up to move only that decoration. Check Undo/Redo, Stop between edits, and the **Apply as it works** switch; review-mode commands apply only once. The initial context identifies selected items; the preview and geometry query expose their positions. Static previews do not establish simulator playability and omit camera backgrounds, animation and effects.

A temporary in-memory v95 harness exercised two live GPT-6 Astra turns: create a raised platform/rope/flower, then move only the flower. Independent geometry comparison confirmed the second turn preserved every other element. Regression tests also cover actual native tile surfaces, all four slope directions, saved foothold connectivity, and repeated undo/redo. The harness never overwrites IMG source assets.

GPT-6 Astra settings normalize the model ID to `openai/gpt-6-astra` on OpenRouter and `gpt-6-astra` on other endpoints, for both connection tests and saved runtime options. Selecting or typing Astra selects Responses and enables low, medium, high, xhigh, max, and ultra reasoning (endpoint-advertised levels take precedence). Unrecognized model IDs are preserved; spelling errors must be corrected explicitly.
