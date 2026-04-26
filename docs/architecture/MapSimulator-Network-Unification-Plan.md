# MapSimulator Network Unification Plan (Single Socket Per Server, GMS v95)

## Scope

Unify `HaCreator/MapSimulator` networking so the game client maintains exactly one live socket per server role:

1. Login server
2. Channel server
3. Cash Shop server
4. MTS server

Replace the current per-feature socket/listener model (`*OfficialSessionBridgeManager`, `*PacketInboxManager`, `*TransportManager`) with centralized transport and routing.

## Constraints

- Use `MapleLib` as the transport/protocol foundation.
- Enforce GMS v95 handshake (`version = 95`).
- Migrate incrementally (no big-bang cutover).

## Current State Summary

- Many feature managers currently own socket lifecycle, handshake parsing, crypto setup, and packet forwarding.
- Handshake/version behavior is fragmented across managers.
- No single authority currently enforces global v95 policy.

## Target Architecture

### 1. MapleLib is the transport owner

`MapleLib` owns:

- Socket accept/connect lifecycle
- Handshake + crypto bootstrap
- Packet framing/de-framing
- Session state/reconnect
- Packet publish pipeline

`HaCreator/MapSimulator` consumes transport via interfaces/events only.

### 2. Single session per server role

Server roles:

- `Login`
- `Channel`
- `CashShop`
- `Mts`

Exactly one active logical connection per role at runtime.

### 3. Central packet router in MapSimulator

- Accept packets from role session
- Dispatch by opcode to feature handlers
- Preserve existing feature runtime logic while removing feature transport ownership

### 4. Global v95 handshake policy

- `Version = 95`
- Centralized init encode/decode + validation
- Strict mismatch rejection by default

## MapleLib Work Items

### A. PacketLib primitives

Use existing:

- `MapleLib.PacketLib.Session`
- `MapleLib.PacketLib.Acceptor`
- `MapleLib.PacketLib.Connector`
- `MapleLib.MapleCryptoLib.MapleCrypto`

Add/maintain:

- `MapleServerRole`
- `MapleHandshakePolicy`
- `MapleSessionPacketEventArgs`
- `MapleRoleSessionProxy`
- Role-session publish/subscribe surfaces for app integration

### B. Pattern porting (as needed)

Adapt (do not copy directly) missing lifecycle/buffering patterns from:

- `LinkServer.cs`
- `LinkClient_Async.cs`
- `MapleSession_Async.cs`
- `Acceptor.cs`

### C. Handshake normalization

- Move init parse/relay behavior into shared MapleLib code
- Remove per-manager `CreateCrypto(...)` forks over time
- Ensure all role sessions use shared v95 policy

## MapSimulator Refactor Plan (Phased)

### Phase 0 - Baseline + guardrails

- Inventory each manager by role/opcode/port/runtime owner
- Add temporary telemetry (in/out packets, active sessions, queue depth, latency)
- Assert/log when more than one active session exists per role

### Phase 1 - MapleLib role-session core

- Implement role-session primitives
- Add tests for v95 enforcement, framing, reconnect, and single-session-per-role invariant

### Phase 2 - Login role integration

- Move login bridge transport ownership to MapleLib role session
- Keep login runtime semantics via adapter layer

### Phase 3 - Channel role integration

- Move channel bridges/inboxes to one role stream
- Consolidate channel opcode dispatch

### Phase 4 - Cash Shop role integration

- Migrate cash transport ownership to one cash role session

### Phase 5 - MTS role integration

- Migrate MTS transport ownership to one MTS role session
- Validate cash <-> MTS stage transitions

### Phase 6 - Legacy transport decommission

- Remove obsolete transport-only bridge/inbox/transport managers
- Keep feature/domain decode/runtime logic

### Phase 7 - Stabilization

- Remove deprecated ports/config
- Update docs/diagrams
- Freeze new transport outside MapleLib

## Migration Controls

- Use adapters to preserve feature runtime signatures
- Migrate one role at a time with rollback flag
- Keep old/new paths behind feature flags until parity is confirmed
- Prioritize Login first (handshake and stage-entry authority)

## Test Plan

### Automated

- `MapleLib.Tests`: handshake strictness, framing/crypto round-trip, reconnect/disconnect
- `UnitTest_MapSimulator`: role routing, single-session invariant, stage transitions

### Manual verification

1. Login through unified login session
2. Enter channel/field through unified channel session
3. Enter/exit Cash Shop through unified cash session
4. Enter/exit MTS through unified MTS session
5. Confirm no per-feature extra sockets during normal flow

## Deliverables

- MapleLib role-based networking core
- Central MapSimulator router + adapters
- Removal of per-feature socket ownership
- Enforced shared GMS v95 handshake path
- Updated architecture docs

## Out Of Scope

- One-shot implementation of all phases in a single PR
- Packet feature semantic changes unrelated to transport ownership
- Non-v95 protocol redesign

## Implementation Status (Condensed, as of April 23, 2026)

### Completed

- MapleLib role-session core added and active:
  - `MapleServerRole`
  - `MapleHandshakePolicy` (`GlobalV95`, strict mismatch rejection)
  - `MapleSessionPacketEventArgs`
  - `MapleRoleSessionProxy`
  - `MapleRoleSessionProxyFactory`
- MapleLib handshake policy now owns role-session crypto creation as well as v95 version resolution; remaining legacy bridge helpers delegate to `MapleHandshakePolicy.GlobalV95` instead of constructing `MapleCrypto` with per-manager version transforms.
- Login and broad Channel bridge migration started and expanded across many `*OfficialSessionBridgeManager` implementations using `MapleRoleSessionProxy`.
- Official-session bridge construction now routes through centralized `MapleRoleSessionProxyFactory.GlobalV95` creation helpers (`CreateLogin`/`CreateChannel`) in `MapleLib.PacketLib` instead of per-bridge `new MapleRoleSessionProxy(...)` calls.
- `MapSimulator` now owns and passes a shared role-session proxy factory (`_officialSessionRoleProxyFactory`) into bridge managers at construction time for login/channel bridge families, including packet-script/local-utility/messenger, map-transfer, packet-field, social-list/merchant, expedition, field-message-box, rock-paper-scissors, summoned, reactor-pool, and remote-user seams.
- Role-level session coordinator wiring is now active in `MapSimulator`: the runtime-owned factory is configured for shared per-role authority so role proxies are reused per role instead of created ad hoc.
- Cash/MTS phase groundwork added:
  - `CashServiceOfficialSessionBridgeManager` now mirrors live `CashShop` or `Mts` role-session traffic into the existing cash-service inbox seam.
  - `MapSimulator` now owns shared `CashShop` and `Mts` bridge instances from `_officialSessionRoleProxyFactory`.
  - Cash-service inbox draining now accepts role-session proxy ingress in addition to loopback/local ingress.
  - Cash-service ingress is now wired into the main `MapSimulator` update loop so cash/MTS proxy traffic reaches the runtime-owned stage packet application path.
  - Cash/MTS bridge lifecycle scaffolding (enable/direct/discovery config fields, ensure/refresh hooks, default listen ports, and status formatting) is now in place.
  - `/cashservice` now exposes runtime bridge control/status surfaces for both roles, including direct start, discovery listing, auto-discovery start, stop, and owner-family open helpers.
  - `/cashservice` now also exposes cash-service inbox lifecycle overrides (`start`/`stop`/`auto`) and packet injection helpers (`packet`, `packetraw`, `packetclientraw`) so unified stage ingress can be exercised without an external loopback client.
  - Cash/MTS bridge history commands (`history`/`clearhistory`) now keep recent mirrored packet visibility available during live manual parity passes.
- Dojo, transportation, memory-game, massacre, Ariant Arena, mob-attack, combo-counter, context stage-period, expedition intermediary, remote-user, summoned, stage-transition, admin-shop, engagement-proposal, and wedding packet inboxes are now adapter-only seams: their loopback listeners, default ports, listener lifecycle, and per-manager ingress counters have been removed, while parse/queue/apply entry points remain available for role-session and local injection paths. Dojo/transportation/massacre/expedition intermediary official-session bridge output is routed through the corresponding inbox manager as queued proxy ingress, and manual packet commands still use the local adapter path.
- Adapter-first inbox convergence introduced across a broad set of packet-owned inbox managers.
- Ingress telemetry parity standardized (listener/proxy/local + last ingress mode), with shared ingress mode authority centralized.
- Phase 6 listener-fallback retirement completed for active channel-domain seams (packet-script, guild-boss, cookie-house, expedition intermediary, reactor pool/touch outbox, summoned, local utility inbox/outbox, remote user, rock-paper-scissors), with routing now proxy-primary when official-session bridge mode is enabled.
- Legacy loopback listener/transport activation is now retired in those same seams (proxy-required when bridge mode is disabled), while proxy/local inbox dispatch remains intact.
- Dormant packet-script transport fallback surface removed:
  - retired `/scriptmsg transport start/stop` command actions (status only),
  - removed `PacketScriptReplyTransportManager`,
  - removed packet-script transport lifecycle/config fields from `MapSimulator`.
- Rock-paper-scissors dormant outbox activation controls removed:
  - retired `/rps outbox` command surface,
  - removed inactive outbox start/stop lifecycle calls and stale update-loop hook.
- Additional listener retirement pass completed:
  - `RockPaperScissorsPacketInboxManager`, `TournamentPacketInboxManager`, and `TradingRoomPacketInboxManager` are now adapter-only inboxes with parsing, queueing, local/proxy ingress, and dispatch status retained.
  - Retired their loopback `TcpListener` ownership, default-port state, start/stop lifecycle, per-listener receive counters, and auto-start update-loop hooks.
  - Their legacy inbox start/stop chat commands now report the role-session/local packet command path instead of opening a feature-owned socket.
- Follow-up `TcpListener` audit pass:
  - `ReactorPoolPacketInboxManager` is now adapter-only; reactor packet inbox listener state, default port, start/stop lifecycle, and listener ingress counters were removed while proxy/local queueing and packet parsing remain.
  - `SocialRoomMerchantPacketInboxManager` is now adapter-only for personal-shop and entrusted-shop payloads; loopback listener lifecycle and default port state were removed while packet parsing and dispatch status remain.
  - `LocalOverlayPacketInboxManager` and `LocalUtilityPacketInboxManager` are now adapter-only packet inboxes; listener lifecycle, default-port state, and listener receive counters were removed while proxy/local ingress and dispatch accounting remain.
  - `RockPaperScissorsClientPacketTransportManager` is now a packet-framing helper only; its dormant loopback client outbox listener and queued socket delivery state were removed because official-session bridge code only needs raw packet construction.
  - `CashServicePacketInboxManager`, `LoginPacketInboxManager`, `CookieHousePointInboxManager`, `CoconutPacketInboxManager`, and `SnowBallPacketInboxManager` no longer open feature-owned loopback listeners. Their parse/queue/apply surfaces remain available for role-session proxy ingress and local command injection, while legacy `Start`/`Stop` calls are compatibility shims that report the retired listener path.
  - Coconut and SnowBall outbound minigame action fallbacks now report that role-session bridge or local packet command paths are required instead of attempting feature-owned loopback delivery.
- Extended listener audit pass:
  - Removed the remaining `TcpListener` declarations and accept-loop remnants from HaCreator MapSimulator manager code.
  - The Coconut, Cookie House, Dojo, Expedition Intermediary, Guild Boss, Map Transfer, Massacre, Memory Game, Messenger, Monster Carnival, Rock-Paper-Scissors, SnowBall, social room employee/merchant, and Transportation official-session bridge managers now rely on `MapleRoleSessionProxy` for live socket ownership.
  - `GuildBossPacketTransportManager`, `LocalUtilityPacketTransportManager`, and `ReactorTouchPacketTransportManager` no longer bind loopback listeners; their start/stop paths are compatibility shims and packet parsing/queueing helpers remain available for role-session or local command paths.
- Final feature-manager socket ownership pass:
  - Removed stale per-manager `BridgePair`/`AcceptClientAsync` relay implementations and per-manager `CreateCrypto(...)` helpers that survived behind the `MapleRoleSessionProxy` integration.
  - Removed the last direct `TcpClient` line-send helper from `EngagementProposalInboxManager`; engagement proposal ingress is now queue/local/proxy adapter-only.
  - A direct manager audit no longer finds `TcpListener`, `AcceptTcpClientAsync`, `new TcpClient`, `NetworkStream`, per-manager `BridgePair`, or per-manager `MapleCrypto` construction in `HaCreator/MapSimulator/Managers`.
- Listener-ingress compatibility counters were removed from the remaining adapter inbox status surfaces. Login, Cash Service, Coconut, and Cookie House now report only role-session proxy ingress and local command ingress; unknown adapter ingress is normalized to role-session proxy ingress instead of preserving a retired listener bucket.
- Central role-session telemetry now lives on `MapleRoleSessionProxy` for all roles, including active session count, server-to-client packet count, client-to-server packet count, sent injection count, and last packet timestamp. The shared proxy factory authority status includes those per-role counters so feature managers do not need listener ownership or listener-specific accounting.
- Shared role-session proxy ownership is now hardened at the MapleLib transport layer: a running `MapleRoleSessionProxy` accepts idempotent starts for the same endpoint but rejects incompatible reconfiguration requests, preventing per-feature bridge commands from silently replacing the single authoritative role socket for login/channel/cash/MTS.
- Shared role-session proxy auto-port binding now reports the actual OS-assigned loopback port when callers request listen port `0`, so `/... session startauto` and `attachproxy` status does not leave reconnect instructions pointing at port `0`.
- Cash Shop and MTS official-session bridge managers now mirror that auto-port behavior on their own status/configuration surface, so a direct `listenPort = 0` start reports the real role-session proxy port instead of preserving `0`.
- Shared role-session authority status now exposes the full central telemetry surface needed for migration validation: active session count, server packet count, client packet count, injected packet count, and last packet timestamp per role.
- HaRepacker `FHMapper/DisplayMap*.resx` resources now use explicit `Footholds.DisplayMap*.resources` logical names, matching the WinForms designer namespace and unblocking `HaCreator`/`UnitTest_MapSimulator` project-reference builds that previously expected a stale `HaRepacker.FHMapper.DisplayMap.zh-CHS.resources` path.
- Cash-service inbox command tests now assert the retired listener contract: `/cashservice inbox start` preserves command override and configured port for status visibility, but the adapter remains non-listening because proxy/local ingress is authoritative.
- Retired feature-owned socket status surfaces were audited and consolidated behind one shared inactive socket-state helper instead of duplicating per-manager `Port`, `IsRunning => false`, connected-client, and retired-listener status formatting. Affected adapter/transport shims:
  - `CashServicePacketInboxManager`
  - `CoconutPacketInboxManager`
  - `CookieHousePointInboxManager`
  - `GuildBossPacketTransportManager`
  - `LocalUtilityPacketTransportManager`
  - `LoginPacketInboxManager`
  - `ReactorTouchPacketTransportManager`
  - `SnowBallPacketInboxManager`

### Verified in migration passes

- Repeated `HaCreator`/`MapleLib` builds and `MapleLib.Tests`/`UnitTest_MapSimulator` runs succeeded in migration slices.
- Latest slice verified with `dotnet build HaCreator/HaCreator.csproj -c Debug` and focused `UnitTest_MapSimulator` networking suites (`OfficialSessionBridge*`, `PacketInboxManager*`, `PacketTransportManager*` filters).
- Latest listener-retirement slice verified with `dotnet build HaCreator/HaCreator.csproj -c Debug --no-restore`.
- Final feature-manager socket ownership pass verified by static manager audit for direct socket/listener/bridge-pair ownership and by a Roslyn syntax pass over manager sources with no syntax diagnostics (`CS100x`/`CS151x`).
- Some full-solution/full-suite runs were intermittently blocked by pre-existing unrelated workspace issues (WPF generated symbol errors, process file locks, and missing symbols outside touched slices).
- The `spine-csharp 2.1.25.csproj` project-reference walk can fail under default parallel MSBuild with no compile diagnostics; running focused verification with single-node MSBuild (`-m:1`) avoids that flaky evaluation path.
- Current focused verification with repo-local `DOTNET_CLI_HOME` and single-node MSBuild:
  - `dotnet test MapleLib.Tests/MapleLib.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PacketLib" -m:1` passed: 13 tests.
  - `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj -c Debug --no-restore --filter "FullyQualifiedName~MapleRoleSessionProxyFactoryTests|FullyQualifiedName~CashServiceOfficialSessionBridgeManagerTests|FullyQualifiedName~CashServiceBridgeCommandTests" -m:1` passed: 14 tests.
- April 26, 2026 follow-up:
  - Static manager audit for direct feature-owned transport APIs found no remaining `TcpListener`, `AcceptTcpClientAsync`, `new TcpClient(...)`, `NetworkStream`, `BridgePair`, per-manager `new MapleCrypto(...)`, or per-manager `CreateCrypto(...)` ownership under `HaCreator/MapSimulator/Managers`.
  - `dotnet build HaCreator/HaCreator.csproj -c Debug --no-restore -m:1` passed with existing warnings.
  - `dotnet test MapleLib.Tests/MapleLib.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PacketLib" -m:1` passed: 13 tests.
  - `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj -c Debug --no-restore --filter "FullyQualifiedName~MapleRoleSessionProxyFactoryTests|FullyQualifiedName~CashServiceOfficialSessionBridgeManagerTests|FullyQualifiedName~CashServiceBridgeCommandTests|FullyQualifiedName~OfficialSessionBridgeManagerTests|FullyQualifiedName~PacketInboxManagerPhase6Tests|FullyQualifiedName~PacketInboxManagerTests|FullyQualifiedName~PacketTransportManagerTests" -m:1` passed: 51 tests.
  - Follow-up execution moved `MapleRoleSessionProxyFactory` from `HaCreator/MapSimulator/Managers` into `MapleLib.PacketLib`, so shared per-role proxy authority now lives with the transport core rather than MapSimulator manager code.
  - Rechecked static manager audit after the move: no direct feature-owned socket/listener/crypto ownership remains under `HaCreator/MapSimulator/Managers`.
  - Rechecked focused verification after the move:
    - `dotnet build HaCreator/HaCreator.csproj -c Debug --no-restore -m:1` passed with existing warnings.
    - `dotnet test MapleLib.Tests/MapleLib.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~PacketLib" -m:1` passed: 13 tests.
    - `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj -c Debug --no-restore --filter "FullyQualifiedName~MapleRoleSessionProxyFactoryTests|FullyQualifiedName~CashServiceOfficialSessionBridgeManagerTests|FullyQualifiedName~CashServiceBridgeCommandTests|FullyQualifiedName~OfficialSessionBridgeManagerTests|FullyQualifiedName~PacketInboxManagerPhase6Tests|FullyQualifiedName~PacketInboxManagerTests|FullyQualifiedName~PacketTransportManagerTests" -m:1` passed: 51 tests.
  - Broader relevant project verification also passed:
    - `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj -c Debug --no-restore -m:1` passed: 54 tests.
    - `dotnet test MapleLib.Tests/MapleLib.Tests.csproj -c Debug --no-restore -m:1` passed: 106 tests.
  - Live parity preflight found no running `MapleStory`/`HaCreator` process and no checked parity ports listening or established (`8484`, `8585`, `8586`, `8600`, `8601`, `18484`, `18486`, `18507`, `18508`), so the login/channel/cash/mts manual pass still requires a live v95 client/server session.
- April 26, 2026 IDA v95 client evidence (`D:\Installations\MapleStoryGlobal v95\MapleStory.exe`, md5 `600b1c2dda171684007f080aed6947eb`):
  - `CLogin::OnSelectCharacterResult` (`0x5dea80`) and `CLogin::OnSelectCharacterByVACResult` (`0x5de670`) decode the channel endpoint from the login packet and call `CWvsContext::IssueConnect`.
  - `CClientSocket::OnMigrateCommand` (`0x4add50`) decodes the next endpoint and calls `CWvsContext::IssueConnect`.
  - `CWvsContext::IssueConnect` (`0x9e0300`) uses `TSingleton<CClientSocket>::ms_pInstance`, calls `CClientSocket::Close`, builds one `CONNECTCONTEXT`, and calls `CClientSocket::Connect`; `CClientSocket::Close` (`0x4ae990`) clears send/receive context and closes the existing socket handle.
  - `CWvsContext::SendMigrateToShopRequest` (`0x9dc280`) sends opcode `43` through `TSingleton<CClientSocket>::ms_pInstance`, confirming Cash Shop migration starts on the active game socket rather than a feature-owned socket.
  - `CWvsContext::SendMigrateToITCRequest` (`0x9def50`) sends opcode `180` through `TSingleton<CClientSocket>::ms_pInstance`, confirming MTS/ITC migration follows the same singleton-socket rule.
  - `CStage::OnSetCashShop` (`0x71adf0`) decodes `CharacterData`, constructs `CCashShop`, calls `set_stage`, and saves session channel id `100`, matching the dedicated cash-service stage ownership model.
  - `CCashShop::SendTransferFieldPacket` (`0x494a20`) sends opcode `41` through `TSingleton<CClientSocket>::ms_pInstance` and switches to `CInterStage` while waiting to return to the field.
  - `CCashShop::OnPacket` (`0x4997e0`) dispatches server opcodes `382`, `383`, `384`, `385`, `386`, `387`, `388`, `390`, `391`, `392`, `393`, `395`, and `396`; `389` and `394` are not handled in the v95 switch.
  - `CCashShop::OnCashItemResult` (`0x499370`) dispatches subtype-driven result handling under opcode `384`, so the simulator should mirror opcode `384` wholesale and decode subtypes at the stage-runtime layer.
  - `CWvsContext::OnPacket` (`0x9e5830`) dispatches central context opcodes, including stage change (`135`) to `CWvsContext::OnStageChange` (`0x9e5360`) and transfer channel (`138`) to `CWvsContext::OnTransferChannel` (`0xa02890`).

### Remaining focus

- Use the new `/cashservice` bridge controls to validate live Cash Shop and MTS role traffic against manual parity flows.
- Keep port ownership and connection lifecycle in the main role socket layer for login/channel/cash/mts. This is enforced in code by the static manager audit above, with `MapleRoleSessionProxyFactory` now in `MapleLib.PacketLib`, and matches the IDA evidence that official v95 migration reuses `TSingleton<CClientSocket>` rather than feature-owned sockets.
- Execute full manual parity pass for login/channel/cash/mts flows against a live v95 client. Static, automated, and IDA-backed verification no longer show a code-side socket-retirement task; remaining validation is runtime parity, and any future fallback removal should be driven by packets observed during that pass.
