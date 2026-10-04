# GMS v95 client-port validation

This note defines what the current checks can prove for the 1:1 GMS v95 client
target. It is a validation guide, not a parity certificate. Package status and
execution order belong in `EXECUTION-PLAN.md`; historical `Implemented`,
`Partial`, and `Missing` rows in the backlog files remain research claims until
the evidence below is attached.

## Evidence levels

| Level | Evidence | Establishes | Does not establish |
|---|---|---|---|
| A | Build, static ownership review, and source inspection | The selected code compiles and the intended owner exists | Native behavior, packet correctness, or end-to-end gameplay |
| B | Deterministic xUnit contract tests | A managed invariant at its owning boundary, such as session disposal, WZ ownership, codec parsing, or UI model state | Equivalence to the v95 executable or a live server |
| C | Real v95 WZ/IMG data and opt-in graphics/lifecycle tests | Behavior against the identified data export and selected graphics/resource lifecycle | Authenticated networking, native rendering metrics, or complete client flow |
| D | Pinned native static evidence or byte-level packet/replay comparison | A recovered native branch, constant, data path, or exact packet shape for the scoped case | Correct managed call-site wiring, live timing, or a whole subsystem |
| E | Direct standalone-client run against a controlled compatible v95 server | The executable completes the scoped account, stage, transport, and visible behavior flow without the original MapleStory client attached | Parity outside the exercised scenario; record the scenario and artifacts |

Levels A-D are useful prerequisites. A package that owns online account entry,
stage migration, or field gameplay is not `Verified` without level E for its
scope. A codec round trip, loopback relay, injected packet, screenshot, or
successful build is not a substitute for that run.

## Current automated baseline

The following commands were run from the repository root,
using the existing Debug outputs and `--no-restore`:

```powershell
dotnet test UnitTest_MapleGame/UnitTest_MapleGame.csproj -c Debug --no-restore -m:1
# Passed: 152, Skipped: 7, Total: 159 (with MAPLEGAME_TEST_EXPORTS pointing at the pinned gms_v95/gms_v270 exports; graphics opt-in suite passed 6/6)

dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj -c Debug --no-restore -m:1
# Passed: 190, Skipped: 4, Total: 194
```

The MapSimulator baseline is green. The earlier
`AICompactEditTests.CompactRegistryKeepsQueriesAndStrictWrapper` failure was a
stale count in the test: three legitimate query tools (`get_tile_info`,
`get_map_state`, `get_asset_preview`) were added to the compact registry by
visual Astra tooling without updating the expected total. The test now asserts
the current 14-tool contract plus explicit query membership.

The skipped tests are deliberate environment gates, not passing parity proof:

- `MAPLEGAME_AUDIO_TESTS=1` enables the native audio relaunch check.
- `MAPLEGAME_GRAPHICS_TESTS=1` enables graphics lifecycle checks; the runtime
  graphics suite also requires `MAPLEGAME_GRAPHICS_WZ` or
  `MAPLEGAME_TEST_EXPORTS` containing a `gms_v95` export.
- `MAPLEGAME_TEST_EXPORTS` enables real-map and WZ ownership cases that require
  both `gms_v95` and `gms_v270` exports.
- `HACREATOR_AI_TEST_DATA` enables the real v95 AI placement dataset.

Run opt-in checks only after pinning the export identity and recording the
environment. A skipped real-data or graphics test must remain visible in the
result summary.

## What the existing tests cover

The strongest current managed owners are:

- [`GameSessionHostTests.cs`](../../UnitTest_MapleGame/GameSessionHostTests.cs)
  covers STA execution, completion, cancellation, failure propagation, and
  disposal ordering.
- [`RuntimeAssetSourceTests.cs`](../../UnitTest_MapleGame/RuntimeAssetSourceTests.cs),
  [`RuntimeMapProviderTests.cs`](../../UnitTest_MapleGame/RuntimeMapProviderTests.cs),
  [`RuntimeWzOwnershipTests.cs`](../../UnitTest_MapleGame/RuntimeWzOwnershipTests.cs),
  and [`WzRuntimeMapReaderTests.cs`](../../UnitTest_MapleGame/WzRuntimeMapReaderTests.cs)
  cover managed asset ownership, map loading, and WZ-backed lookup behavior.
- [`RuntimeGraphicsLifecycleTests.cs`](../../UnitTest_MapleGame/RuntimeGraphicsLifecycleTests.cs)
  and [`FrameworkGraphicsLifecycleTests.cs`](../../UnitTest_MapleGame/FrameworkGraphicsLifecycleTests.cs)
  own graphics-generation and activation lifecycle behavior when the opt-in
  native environment is available.
- [`OfficialSessionBridgeManagerTests.cs`](../../UnitTest_MapleGame/OfficialSessionBridgeManagerTests.cs),
  [`OfficialSessionBridgeConnectedSessionHarnessTests.cs`](../../UnitTest_MapleGame/OfficialSessionBridgeConnectedSessionHarnessTests.cs),
  [`PacketInboxManagerTests.cs`](../../UnitTest_MapleGame/PacketInboxManagerTests.cs),
  [`PacketInboxManagerPhase6Tests.cs`](../../UnitTest_MapleGame/PacketInboxManagerPhase6Tests.cs),
  and [`PacketTransportManagerTests.cs`](../../UnitTest_MapleGame/PacketTransportManagerTests.cs)
  cover relay/bridge queues, parsing, local or proxy ingress, and deferred
  delivery. They are valuable transport seams but do not prove an independently
  connected client or a server-authored stage transition.
- [`RuntimeMapProducerParityTests.cs`](../../UnitTest_MapSimulator/RuntimeMapProducerParityTests.cs),
  [`RuntimeMapSnapshotTests.cs`](../../UnitTest_MapSimulator/RuntimeMapSnapshotTests.cs),
  and the AI/editor tests cover editor/runtime map ownership, serialization,
  undo behavior, and deterministic tooling contracts.

These owners protect real contracts at useful boundaries. They should be
extended only for a distinct failure mode; source inventories, exact import
greps, and duplicate assertions at every packet layer do not add parity proof.

## Gaps that still require direct or native evidence

No current test demonstrates the complete standalone sequence described in
[`PORT-ARCHITECTURE.md`](PORT-ARCHITECTURE.md):

1. Launch `MapleGame.Client` in online mode without an original client attached.
2. Establish the v95 handshake and authenticate through Login.
3. Consume server-authored world/channel data and the character roster.
4. Select a character, retire Login, migrate to Channel, and apply the field
   and character state before enabling input.
5. Observe a field action and response, then disconnect and reconnect without
   stale packets, duplicate scene state, or retained connection resources.

The current suite also does not prove, end to end:

- an independent direct socket path, endpoint migration, or stage-owned
  connection retirement;
- server-authored login/roster state and authenticated result handling;
- Cash Shop, ITC/MTS, channel change, and return-to-field flows through a live
  v95 connection;
- byte-for-byte movement packets in their complete packet context, native game
  clock timing, or the full `CVecCtrl::CollisionDetectFloat` contract;
- native glyph metrics, UI pixel placement, animation timing, or visual parity
  across the complete login and field scenes.

The fresh static record in [`NATIVE-EVIDENCE.md`](NATIVE-EVIDENCE.md) currently
contains three scoped observations (`CMovePath::IsTimeForFlush`,
`CMovePath::Encode`, and `CVecCtrl::CollisionDetectFloat`) against the identified
v95 input. It explicitly does not establish managed call-site wiring or live
gameplay. In particular, the managed floating-collision owner still contains a
commented-out downward-landing branch; movement backlog rows that describe
configured landing therefore need scoped acceptance evidence.

## Required proof for a package

For each package, record all of the following in the execution plan or a linked
focused note:

1. Runtime owner and entry point, including the exact test owner when automated
   proof exists.
2. Identified v95 executable and WZ/IMG export identity used for comparison.
3. Native observation or protocol requirement, with uncertainty stated when
   the evidence is static only.
4. Managed behavior and remaining mismatch.
5. The smallest executable check that can fail for the intended regression.
6. For online packages, a level-E run artifact: packet capture or decoded
   trace, connection/stage transitions, visible result, and clean shutdown or
   reconnect result.

Use `Open` while prerequisites are missing, `In progress` while implementation
or evidence is incomplete, `Blocked` with the exact absent server/data/native
input, and `Verified` only after the scoped checks pass. A package may have
implemented code while remaining unverified.

## Practical command sequence

Use the smallest owner tests first, then broaden after the owner is green:

```powershell
dotnet build MapleGame.Client/MapleGame.Client.csproj -c Debug --no-restore
dotnet test UnitTest_MapleGame/UnitTest_MapleGame.csproj -c Debug --no-restore -m:1 --filter "FullyQualifiedName~GameSessionHostTests|FullyQualifiedName~RuntimeAssetSource|FullyQualifiedName~OfficialSessionBridge"
dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj -c Debug --no-restore -m:1 --filter "FullyQualifiedName~RuntimeMap|FullyQualifiedName~EditorRuntime|FullyQualifiedName~Packet"
```

For real-data or graphics proof, set the opt-in variables in the environment,
run the focused owner tests, and retain the output and export identity. Then
run the complete project suites above. For a live parity pass, launch the
published standalone client, attach no original MapleStory client, and retain
the handshake, endpoint-generation, stage, packet, and visible-state trace for
the exact scenario. Compare native and managed observations by behavior and
bytes where applicable; do not promote a simulator convenience or relay-only
path into the v95 contract merely because an injected test passes.
