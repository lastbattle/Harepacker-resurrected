# GMS v95 client port architecture

This note separates the existing standalone runtime from the architecture still needed
for a complete client port. See [the parity index](README.md) and
[the execution plan](EXECUTION-PLAN.md). Source inspection supports the current-state
claims below. [Native evidence](NATIVE-EVIDENCE.md) records the narrowly rechecked
movement and socket-migration functions. Other historical native references remain
research leads requiring verification.

## Existing ownership

| Owner | Current responsibility and source |
|---|---|
| `MapleGame.Client` | Windows launch UI, command-line/source/profile selection. `Program.Main` opens assets and loads a map before constructing `MapSimulator`. The normal default is Henesys (`100000000`), rather than an authenticated account-entry bootstrap. |
| `MapleGame.Runtime` | MonoGame game loop, rendering/input, local gameplay, packet codecs and feature managers, runtime UI, owned maps and transitions. The project references MapleLib and HaSharedLibrary without an editor project reference. |
| `GameSessionHost` | One process-wide runtime session on an STA thread; cancellation and completion; disposal before the host releases assets. |
| Runtime asset services | Session-owned IMG/WZ/hybrid sources and catalog. `RuntimeMapProvider.Load` produces owned definitions and gives launch snapshots precedence. Built-in sources require a new session to observe replaced backing data. |
| HaCreator adapters | Detached editor snapshots, unsaved overrides and preview/write coordination. Editor objects remain outside the runtime. |
| MapleLib | WZ/IMG and protocol/crypto foundations; current role-session relay transport. |
| HaSharedLibrary | Shared rendering, audio, Spine, content fonts and utilities. |

The retained `HaCreator.MapSimulator` namespaces are deliberate compatibility choices,
not proof of an editor assembly dependency. Preserve the physical assembly boundary
described in [the extraction plan](../architecture/map-simulator-extraction-plan.md).

## Transport gap that blocks an independent online client

`MapSimulator.cs` sets `EnableOfflineClientMode = true` and derives default packet
connections from its inverse. `GameSessionOptions` has host/content/presentation
settings but no injected online session or explicit online/offline authority policy.

The simulator creates `MapleRoleSessionProxyFactory` with
`shareRoleSessionProxyPerRole: true`. That successfully centralizes **relay** ownership:
feature managers share one relay for each Login, Channel, CashShop or Mts role.
It does not implement a self-contained game-client connection:

- `MapleRoleSessionProxy.Start` starts a loopback `TcpListener`.
- `ListenLoopAsync` waits for an external client to attach.
- `AcceptClientAsync` then connects a second `TcpClient` to the server and creates
  separate client-facing and server-facing `Session` instances.
- `HandleServerPacket` relays packets to the attached client and publishes mirrored
  packets to simulator consumers.

This is useful capture/injection infrastructure. Preserve it as a parity tool while
adding an independent client-owned connection path. A shared relay per role also does
not establish the native migration rule recorded in the historical network document:
one active client socket whose endpoint changes during stage migration. That native
rule is confirmed statically by N04 in the native evidence note; live traffic and
each stage-specific call site still need validation.

`IssuePacketOwnedSelectCharacterDirectConnect` and
`IssuePacketOwnedSelectCharacterByVacDirectConnect` currently create a
`LoginIssuedDirectConnect` record. Their finalizers record the handoff and play entry
audio; those methods do not open the recorded endpoint. Recording an endpoint is not
execution of the network migration.

## Target responsibilities

1. **MapleLib direct transport:** connect without an external relay client, bootstrap
   v95 crypto/framing, send/receive packets, expose disconnect/error/cancellation,
   and retire the previous connection safely. Keep relay mode separately usable.
2. **Runtime session authority:** own account, selected world/channel, character,
   current stage and endpoint migration; define which state is server-authored and
   which behavior is local presentation/prediction. Supply an offline implementation
   to the editor preview through an injected contract.
3. **Runtime packet routing:** route the active connection by stage/opcode into
   existing codecs and feature handlers; queue state application onto the game thread.
   Prevent stale packets from a retired connection mutating a new stage.
4. **Runtime scene owners:** login, interstage, field, Cash Shop and ITC/MTS own their
   lifecycle and UI. Reuse existing rendering and asset services without handing
   editor state or network lifecycle to individual windows.
5. **Client host:** select online/offline launch mode and endpoint configuration,
   initialize the chosen session services, and keep deployment/profile concerns out
   of gameplay and transport managers.

These are incremental ownership changes, not a request to rewrite the large
`MapSimulator` partial class before a playable slice exists.

## First online vertical slice

Use a compatible controlled v95 server and pinned v95 data to prove this sequence:

1. Start the standalone host in online mode and connect directly to Login.
2. Complete the v95 handshake and account/auth result handling through existing
   login codecs and `LoginBackendSessionManager` state.
3. Populate world/channel selection and the server-authored character roster.
4. Send character selection; consume the successful endpoint/handoff payload.
5. Retire Login, connect to Channel, perform its migration handshake/request, then
   apply the server-authored field and character state before enabling field input.
6. Observe a field action and its response, disconnect, and reconnect without stale
   packets, duplicated scene state or retained connection resources.

Acceptance requires the standalone executable completing the flow with no original
MapleStory client attached. Preserve packet captures, endpoint/connection transitions,
and visible stage behavior. Extend the same lifecycle to channel change, Cash Shop,
ITC/MTS and return-to-field after this slice works.

## Execution dependencies and current limitations

- The host/runtime require Windows, .NET 10 Windows Desktop, MonoGame WindowsDX,
  shared/native dependencies and published `Content` fonts.
- Exact parity needs one identified v95 executable/data set and reproducible native
  observations; historical IDA addresses alone do not establish current execution.
- Live online proof requires compatible server endpoints and protocol/auth material.
  Local packet injection proves a handler seam, not an end-to-end client session.
- `RunContentStage` currently combines CPU decode and GPU upload on the game thread.
  Preserve that affinity until those stages are explicitly separated.
- Map activation preflights a candidate, commits on the game thread and retires the
  previous generation. Mandatory failure after commit is fatal; do not introduce
  speculative rollback across shared session mutation.
- `MapSimulator.UpdateLoop.cs` mixes wall-clock ticks with frame timing; its `delta`
  uses the `TimeSpan.Milliseconds` component. Establish the intended native timing
  contract and replay observations before asserting frame-rate-independent parity.
- `CVecCtrl.CollisionDetectFloat` has a called but commented-out foothold landing
  branch. The movement backlog's broad Implemented rows therefore need scoped
  acceptance evidence rather than being treated as complete physics proof.

## Documentation contract

Keep the index and execution plan short. Domain backlogs retain their recovered
functions, WZ paths and narrow behavioral findings. Every executable work item should
identify its runtime owner, prerequisite, observed native behavior, remaining mismatch,
and a concrete completion check. Distinguish implemented code from live-verified
behavior, and source-confirmed gaps from hypotheses awaiting native evidence.

Update historical `HaCreator/MapSimulator/...` source references to the current
`MapleGame.Runtime/Simulator/...` paths when touching the relevant rows. Do not erase
useful recovered evidence or reclassify a whole subsystem from a successful isolated
packet or UI check.
