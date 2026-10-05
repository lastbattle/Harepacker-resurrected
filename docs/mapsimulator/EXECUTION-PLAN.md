# GMS v95 port execution queue

This is the execution authority for the remaining client work. Read
[the current-state audit](CURRENT-STATE.md), [architecture](PORT-ARCHITECTURE.md),
and [validation requirements](VALIDATION.md) first. Research links are indexed in
[RESEARCH-MAP.md](RESEARCH-MAP.md). All packages below are **Open** at the review
baseline; existing feature code must be reused and validated, not assumed absent.

## Order and ownership

| Package | Deliverable | Prerequisites | Primary file ownership |
|---|---|---|---|
| P00 | Pinned native/data baseline and reproducible comparisons | None | Focused evidence notes and fixtures |
| P01 | Explicit online/offline authority and cleanup inventory | P00 for native decisions | Client bootstrap, Runtime Contracts, integration partials |
| P02 | Direct v95 connection and migration lifecycle | P00, P01 | MapleLib PacketLib; Runtime session owner |
| P03 | Stage/opcode routing with real outbound delivery | P02 | Runtime Managers and packet ingress/outbound integration |
| P04 | Standalone Login → Channel → field vertical slice | P01-P03 | Login managers/UI, stage transition owner, Client bootstrap |
| P05 | Native clock, physics and movement packet parity | P00, P01; P03-P04 for live proof | Simulator Physics, PlayerCharacter movement |
| P06 | Avatar/action/data selection parity | P00, P01; P04 for live character data | Character loaders/assembly, action assets |
| P07 | Skill/combat/mob authority and native rules | P03-P06 | Skills, PlayerCombat, MobAI/MobPool |
| P08 | Character progression, inventory, drops and field interaction | P03-P05; P07 for combat rewards | Interaction, entity pools, authoritative character state |
| P09 | HUD, windows, input and text parity | P04, P06, P08; P07 for skill feedback | Simulator UI, input/IME and drawing integration |
| P10 | Companions, remote users and social transactions | P03-P09 as used by each family | Companions, RemoteUserActorPool, social owners |
| P11 | Cash Shop/ITC and all service-stage return paths | P02-P04, P08-P09 | Cash stage/transaction owners, migration integration |
| P12 | Special fields, minigames and event lifecycles | P03-P09 | Fields and feature-specific state owners |
| P13 | Rendering, effects, audio and resource parity | P00-P06; feature owners as exercised | Rendering/Animation/Effects, shared audio boundary |
| P14 | Full-client compatibility and release closure | P00-P13 | Client packaging, focused validation records |

P00/P01 establish the contracts. Prioritize P02-P04 as one integrated delivery:
prove a real standalone online client before adding more injected feature seams.
P05/P06 research can run alongside transport work; their final checks include live
wiring. P09/P13 can improve presentation incrementally without claiming broad
completion. Dependencies in the table are final acceptance gates, not a ban on
independent research before upstream implementation is complete.

## Current slice status (implementation vs verification)

Statuses below distinguish implemented source from verified behavior. Level
definitions live in [VALIDATION.md](VALIDATION.md); the pinned oracle record is
[P00-ORACLE-PIN.md](P00-ORACLE-PIN.md).

Process note (2026-10-05): the user removed the push requirement from this
goal; completed items are committed locally only. The remaining external input
for slice acceptance is a controlled compatible v95 server.

| Package | Implementation status | Verification status |
|---|---|---|
| P00 | Pinned executable identity, confirmed N04 observations, pinned `gms_v95` IMG export identity, and scenario list recorded (`P00-ORACLE-PIN.md`). | Level C: real-data and graphics lifecycle suites pass against the pinned export (`MAPLEGAME_TEST_EXPORTS`; 152 passed/7 skipped, graphics opt-in 6 passed). Level E scenarios still require a controlled server. |
| P01 | `GameSessionAuthority` contract in Runtime Contracts, host `--online <host[:port]>` flag, offline default preserved. Authority and disposition inventory published (`P01-AUTHORITY-INVENTORY.md`). | Source-verified (A); no behavior difference offline. |
| P02 | `MapleClientDirectSession` (MapleLib) with handshake, framing, crypto, cancellation, disconnect, reconnect, global generations, stale rejection. `codex/p02-direct-session`. Native call sites recovered: channel (N04), shop/ITC (N06), post-connect hwid packet (N05). | Level B: 10 focused xUnit tests pass (`MapleClientDirectSessionTests`, `MapleOnlineDirectSessionOwnerTests`). Level E pending server. |
| P03 | `MapleOnlineDirectSessionOwner` single ingress path, per-role sessions, game-thread drain, N04 retire-before-dial migration, trace ring. Reactor touch requests (field action outbound), field-scoped inbound, and post-migration login-stage packets route through the owner; route inventory published (`P03-ROUTE-INVENTORY.md`). | Level B: routing, role isolation, migration, and stage-fan-out tests pass. Live trace pending server. |
| P04 | Login/field/reactor bridges prefer the direct session; owner pumped on the game thread; select-character handoff performs the real close/connect channel migration; the N05 post-connect hwid packet (opcode 0x1A + length + blob) is sent after every handshake; stage-aware reconnect. `--hwid-file` supplies the raw N05 blob, while `--online-trace` persists connection generations, packet direction, stage transitions, and committed scene/player/input state. | Level A/B/C: transport, field, and post-connect request seams are source- and data-verified (hwid packet covered by an encrypted round-trip test and trace persistence by a focused sink test); the full login-to-field flow, server-authored roster, and field action/response require a controlled v95 server, which also defines the accepted hwid blob. |

Use one integrator for shared `MapSimulator.cs`, UpdateLoop, Drawing, Content and
stage partials. Assign domain files to separate workers only after agreeing on
session, clock and authority contracts. Do not concurrently edit the same giant
partial through nominally different feature tasks.

## P00: pin the oracle and comparison scenarios

Record executable/data identity, v95 WZ/IMG manifest, locale and client options,
server build/configuration, map/job/skill IDs and launch conditions in the private
comparison record. Publish only neutral references and scoped observations from
NATIVE-EVIDENCE; do not assume every directory named v95 contains matching data.

Create small independent comparison cases: login success/failure, migration,
walk/jump/ladder/swim/fly, one melee/ranged/magic action, drop pickup, NPC exchange,
and service-stage return. Capture bytes, state transitions and timed visuals as
appropriate. Keep asset data outside source control. Record unavailable server or
native execution inputs precisely.

Acceptance: another agent can reproduce each baseline scenario and distinguish
static recovered behavior from observed behavior. A fixture generated by the
managed implementation under test is not an independent oracle.

## P01: make authority explicit and bound cleanup

Trace `Program.Main`, `GameSessionOptions`, offline-mode defaults, local character
stores, packet-owned state and bridge controls. Define runtime-owned online
session/stage/character authority with an explicit offline preview implementation.
Preserve detached maps, session-owned assets, STA disposal and game-thread GPU work.

Inventory each candidate wrapper or debug command with real callers, domain state,
native counterpart, offline use and replacement. Remove or consolidate it only in
the package that supplies its replacement. Large files, generated constants,
legacy namespaces and `Parity` suffixes are not sufficient deletion evidence.

Acceptance: production mutations have a named authority; offline preview still
launches/closes/transitions; native client behavior does not depend on simulator
command injection or English-description inference. Publish the caller/disposition
inventory for subsequent cleanup slices.

## P02: direct connection and migration

Reuse MapleLib framing/crypto primitives. Add a client-owned direct connection
that does not require `MapleRoleSessionProxy`'s attached external client. Define
handshake, cancellation, send/receive ordering, disconnect and reconnect ownership.
Keep relay capture as a separate tool.

Port the close/connect transition observed in N04; associate queued ingress with
the connection/stage generation so late packets cannot mutate a new session.
Recover Login, Channel, Shop and ITC migration call sites and error branches.

Acceptance: independent connection completes a v95 handshake; wrong-version,
partial-frame, disconnect and cancellation behavior is independently checked;
migration retires old I/O and crypto state before the new connection becomes
authoritative. Native/server traces prove endpoint transitions, rather than one
shared relay object per role being treated as the native socket model.

## P03: one packet application path

Inventory stage, direction, opcode/subtype, codec, state owner, queue, outbound
sender and real consumers for the existing 30 bridge and 28 inbox families.
Route the active direct session through the owning stage into existing decoders;
apply mutations on the game thread and preserve packet order. Consolidate repeated
bridge/listener-status scaffolding without flattening distinct payload semantics.

Trace outbound requests through actual transport delivery, not only a queued
record or status flag. Define unsupported opcode, malformed payload and retired
generation handling from native/protocol evidence. Keep injection/replay tooling
out of production state authority.

Acceptance: representative Login/Channel requests are delivered byte-for-byte and
responses apply once in order; old-generation events are rejected; tests observe
real delivery/state changes. Publish the route inventory so later feature agents
can add handlers without new socket or queue ownership.

## P04: login, roster and field entry

Reuse current Login codecs, `LoginBackendSessionManager`, roster/create-character
state and UI. Connect host online startup to authentication, world/channel data,
character selection and migration. Replace endpoint-only `LoginIssuedDirectConnect`
handoffs with execution through P02. Load server character/field state before
enabling input; keep interstage/loading/error ownership explicit.

Split follow-ups into password/account/security results, world/channel selection,
roster/create/delete/extra-character/guest cases, and entry/reconnect/channel change.
Do not invent local success results for server-authored flows.

Acceptance: standalone executable completes Login → roster → selection → Channel
→ field action/response with no original client attached. Exercise rejection,
disconnect and reconnect. Native packet/state/UI evidence covers each follow-up
before the whole package becomes Verified.

## P05: clock, vector control and movement

Audit all consumers of UpdateLoop delta, wall-clock ticks, gather duration and
movement snapshots. Recover native catch-up, rounding, wraparound and update order.
Trace both `PlayerCharacter` and `CVecCtrl`; implement float crossing/landing,
foothold adjacency/layers, slopes/walls, ladders/ropes, swim/fly gates, impulses,
moving-platform passengers and map bounds from native rules.

Use N01-N03 as starting evidence. Audit `CMovePathClientPacketCodec` callers for
header, attribute, action, elapsed time, random-count and keypad/rectangle tail
choices. Validate flush retention/playback and special movement owners individually.

Acceptance: native position/action/foothold and emitted bytes match scoped cases
at boundaries and under uneven frames; no commented collision branch is documented
as implemented. Include landing ties, negative coordinates, timer wrap, portal
movement and dynamic footholds. Preserve evidence for intentional offline behavior.

## P06: avatar/action assets

Review CharacterLoader/Assembler, CActionFrame, asset lookup, z-map/slot visibility,
anchors, facing and action-layer lifecycle against v95 data and `CActionMan`/`CUser`
research. Cover body/head/face/hair/equipment, weapon overlays, emotion, chairs,
mounts/transforms and job-specific effects. Separate later-version/generic preview
fallback from native v95 selection.

Acceptance: action/frame/origin/z/visibility selection matches native records and
timed visuals for representative jobs and equipment combinations, including missing
asset behavior. Packet-authored appearance changes and scene disposal work live.

## P07: skills, combat and mobs

Replace or isolate `PlayerCombat`'s fixed bounds, attack variance, critical/defense
and incoming-damage approximations. Recover client-side computation versus server
authority, native random consumption, hit selection and attack packet construction.
Audit MobAI hardcoded durations, 50 ms trigger windows and random skill preference.

Port skill families in bounded slices: melee/ranged/magic, prepare/repeat/keydown,
movement skills, buffs/status/cancel, follow-up attacks, summons/vehicles and
job-specific families. Replace description-derived stat aliases with v95 data/ID
rules in native mode. Trace generic casts and special-family code together.

Acceptance: native eligibility, target selection, costs, cooldown/status, packet
bytes, action timing and effects match each family; server results reconcile local
presentation correctly. Representative families are an initial milestone, not
proof that the full catalog is Verified.

## P08: authoritative progression and interaction

Trace drop removal to inventory/meso mutation or server response; `TryPickupDrop`
currently leaves local rewards as comments. Complete inventory operations, item
metadata/equip/use/upgrade, stats/AP/SP/EXP, quest state and persistence authority.
Review NPC/shop/trunk/script requests, reactor state, pickup rights/timeouts,
portals, return/revive and map restrictions.

`QuestRuntimeManager` explicitly lacks some simulator script execution. Determine
which logic belongs to the v95 client versus server script messages; do not fill a
server-owned gap by guessing a local quest engine. Offline preview support needs
its own clear behavior and failure reporting.

Acceptance: request/result flows update state once, refuse rejected actions and
survive field migration. Pickup affects the correct character state; script/NPC
responses and revive destinations match native/server observations. Profile data
cannot overwrite online character authority.

## P09: UI and input

Validate v95 UI family/assets, HUD/quick slots, windows, modal ownership, key maps,
mouse hit regions, chat/balloons/tooltips and drag/drop behavior. Include focus,
fullscreen/resolution/DPI, keyboard repeat, IME, clipboard and text metrics.
Replace scaffolds such as ItemUpgradeUI only after tracing its production owner.

Acceptance: native timed interactions and rendered geometry match the chosen v95
scenario; controls emit the correct requests and reflect server outcomes. Debug
status/command text does not become normal client UI. Input capture/order is tested
through real window behavior, not private predicates alone.

## P10: remote actors, companions and social systems

Review remote-user movement/action/status lifecycle, pets/dragon/summons and
employee pools, party/guild/friends/family/messenger, trade/minigame/merchant rooms
and transactions. Use P03 transport and P08 state authority. Separate social window
presentation from room membership, transaction commit and actor ownership.

Acceptance: enter/update/leave and disconnect retire all actors/state; remote replay
and companion actions match native timing; requests/results preserve transaction
and relationship state. Exercise failures and migration, not only packet decode.

## P11: Cash Shop and ITC/MTS

Use the direct migration owner for entry and return. Reuse existing cash-stage
codecs/windows while validating character/account snapshots, subtype results,
inventory/currency/wishlist/preview/purchase and ITC transaction authority. Recover
stage-specific dispatch separately from the historical per-role relay controls.

Acceptance: live field → shop/ITC → field runs without the original client; endpoint,
stage, character and UI state match native observations, including rejected
transactions, disconnect and return. No stale field or shop packet mutates the
other stage.

## P12: fields, minigames and events

Derive child tasks per field family from research 9 and related interaction notes:
transportation, Dojo, Carnival, raid/boss, arena, massacre, Coconut/SnowBall,
cookie-house, quizzes, room games and event wrappers. Validate FieldFactory/data
selection, packet-owned clocks/scores/restrictions and entry/exit cleanup.

Acceptance: every applicable v95 family has an explicit supported scenario or
named unresolved child task. Native/server state and timer/UI transitions agree;
heuristic local event state is not promoted into online authority.

## P13: rendering, effects, audio and lifecycle

Review layer ordering, camera/viewport, animation-displayer ownership, projectile/
damage/effect timing, fonts and BGM/sound/focus behavior. Use recovered damage-number
research as a scoped oracle, not a full effects certificate. Preserve shared audio
and GPU ownership and candidate-generation failure semantics.

Acceptance: timed native comparisons cover effects and representative scenes;
repeated field/service transitions and close/relaunch do not retain resources.
Real-data/graphics/audio checks report skips explicitly. Any performance rewrite
must preserve native behavior and follow measured optimization separately.

## P14: closure and packaging

Resolve baseline test failures before reporting release-green. Build relevant apps
and the solution in required configurations, run affected/full suites as appropriate,
and test the published archive from a clean directory with external data/profile.
Execute complete native/server compatibility scenarios across account entry, jobs,
field/service transitions, social/transaction systems and special fields.

Acceptance: all packages and their child tasks have scoped evidence; no unresolved
native branch is hidden by an Implemented row. Record supported data/build and
remaining limitations. A successful offline launch, package or green managed test
suite cannot close the full 1:1 port.

## Agent handoff template

Copy into a focused child-task note; avoid appending long history to a table cell.

```text
Package/child ID and status:
Goal and observable v95 behavior:
Owner files/symbols and integration owner:
Prerequisites and exact missing inputs:
Current source revision, entry point and real callers:
Native executable/data identity; addresses and caller context:
Confirmed mismatch versus uncertainty:
Implementation and cleanup disposition:
Independent fixtures/manual scenario; expected bytes/state/timing/visuals:
Focused tests and live/native results, including failures/skips:
Remaining child tasks and evidence links:
```

Package verification requires the package's complete scope. Use child IDs (for
example P07-melee) to land useful slices without marking the whole skill catalog
complete. Source-only cleanup may be delivered before live proof, with its
implementation status and outstanding parity evidence recorded separately.
