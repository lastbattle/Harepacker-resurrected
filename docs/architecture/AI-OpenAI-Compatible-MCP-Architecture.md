# HaCreator AI: OpenAI-Compatible API and MCP Architecture

## Objective

Allow a user to prompt HaCreator to create or edit a MapleStory map through any API that follows the OpenAI request format. OpenRouter is the default endpoint preset, while a private RPC gateway can use the same client by changing the base URL and API dialect.

## Design

### One tool registry

`MapEditorFunctions` remains the canonical registry for the 34 map tools. `MapMcpToolServer` projects that registry into:

- MCP `tools/list` and `tools/call` JSON-RPC messages.
- Chat Completions function tools.
- Responses API function tools.

The parity test compares MCP names and schemas against the registry so a tool cannot silently disappear from one interface.

### Provider-neutral transport

`OpenAICompatibleClient` supports both Chat Completions and Responses-style endpoints. It handles repeated tool-call turns, required discovery queries, strict-schema projections, cancellation, timeouts, and API error reporting. The endpoint is formed from `BaseUrl` unless the user supplies a complete `/chat/completions` or `/responses` URL.

### Model discovery in AI Settings

The AI Settings dialog presents one merged **Model catalog**. Built-in entries are labeled `Built-in`, endpoint entries are labeled `Endpoint`, and duplicates are labeled `Built-in + endpoint`; custom model IDs can still be typed directly. When the dialog opens, when the base URL or API key loses focus, or when the user presses **Refresh**, it calls the configured OpenAI-compatible `GET /models` endpoint and merges the result into the catalog.

Reasoning Effort is model-aware. The client consumes optional reasoning metadata from `/models`; when a standard endpoint only returns model IDs, the dialog safely infers the common levels for known reasoning-model families and otherwise offers `Auto (model default)` only. This avoids sending unsupported `reasoning_effort` values to general-purpose models.

### MCP endpoint

Each open AI Map Editor window hosts a loopback MCP endpoint at a dynamically selected `127.0.0.1` port. The Tools menu displays the endpoint and bearer token for an external MCP client. The endpoint is restricted to loopback and requires the generated bearer token.

MCP action calls are dispatched to the WPF UI thread, parsed by `MapAIParser`, applied by `MapAIExecutor`, and reported back to the caller. Query-before-action requirements remain enforced by the shared tool server.

### Autonomous editing and undo

Automatic application is enabled by default and can be disabled in AI Settings. Generated commands are applied after a successful AI turn; manual application remains available when automatic mode is disabled. All undo batches created by one AI command turn are collapsed into one user-visible undo operation.

## Migration stages

1. Replace provider-specific settings and clients with `OpenAICompatibleOptions` and the standard API loop.
2. Remove the legacy external-host wrappers, generated tool files, and auto-start behavior.
3. Add the loopback MCP host and parity tests over the existing map tool registry.
4. Connect MCP actions and generated commands to UI-thread-safe autonomous execution.
5. Validate the HaCreator build and keep the custom endpoint path compatible with OpenRouter.

## Operational safeguards

- Keep the MCP listener on `127.0.0.1`; do not bind it to a LAN interface without adding explicit authentication and origin policy.
- Treat the bearer token and API key as secrets. The current settings file preserves the existing local settings behavior; OS-backed secret storage can be added independently.
- Keep automatic edits reversible through the normal board undo/redo system.
- Keep the tool loop bounded by `MaxToolTurns` and request timeouts.

## Validation

- `dotnet build HaCreator/HaCreator.csproj -c Debug --no-restore` succeeds.
- `MapMcpToolTests` checks 34-tool parity, discovery-query enforcement, and command parity.
- The broader `UnitTest_MapSimulator` project currently has unrelated pre-existing compile failures in `AnimationControllerTests` and `MobAITests`; those are outside this migration.
