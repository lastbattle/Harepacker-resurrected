# Current-state audit

Baseline: `a85382914002a72da14a5d2b2c84032138754acb`. The worktree contains unrelated
user changes; this review only adds/updates port-planning documentation. Findings
are based on source, the existing documentation, targeted test inspection, and
the limited fresh [native checks](NATIVE-EVIDENCE.md). No live client/server parity
run or full subsystem proof was performed.

## Assessment

The project has a useful standalone runtime foundation and a large amount of
feature-specific implementation. It is not yet an independently connected v95
client. The primary risk is treating simulator approximations, relay instrumentation,
and isolated packet/UI implementations as completed client behavior.

At this baseline, `MapleGame.Runtime/Simulator` contains 776 C# files totaling
40,347,685 bytes. There are 78 `MapSimulator*.cs` files, 30
`*OfficialSessionBridge*` manager files, and 28 `*PacketInbox*` manager files.
`MapSimulator.cs` is about 1.91 MB, `SkillManager.cs` 1.74 MB, and
`MapSimulator.PacketOwnedUtilityParity.cs` 1.39 MB. Size identifies review and
ownership bottlenecks; it does not establish that every method is unnecessary.

The original documents total roughly 9.6 MB; the Misc/Edge document alone is
7,858,921 bytes across only about 800 physical lines. Long historical table cells
and repeated summaries hide unresolved work. The new plan supersedes their
execution order and status authority while retaining their research.

## Useful foundations to preserve

- `MapleGame.Client/Program.cs` supplies a real Windows host, source selection and
  profile ownership. It currently enters a chosen map, normally Henesys.
- `MapleGame.Runtime.csproj` depends on MapleLib and HaSharedLibrary, not the editor.
  `Contracts/GameSessionHost.cs`, runtime map definitions, asset services, and
  candidate map generations keep editor data and native resources owned correctly.
- The simulator includes avatar assembly, WZ-driven presentation, player physics,
  skill families, many packet codecs, UI windows, entity pools and field handlers.
  Their existence is not the same as full native parity, but wholesale replacement
  would discard substantial useful work.
- `MapleLib/MapleLib/PacketLib` centralizes framing/crypto foundations and role relays.
  Preserve the relay as a capture/replay tool while implementing a direct client.
- Current tests protect valuable host, source, graphics, persistence, codec and
  editor/runtime boundaries. See [validation](VALIDATION.md) for limits and owners.

## Findings that change execution order

Paths in the source column are relative to the repository unless prefixed `Simulator/`,
which means `MapleGame.Runtime/Simulator/`. Each finding has an owner in the
[execution plan](EXECUTION-PLAN.md).

| ID | Evidence and limitation | Consequence / work package |
|---|---|---|
| A01 | `MapleLib/MapleLib/PacketLib/MapleRoleSessionProxy.cs`: `Start` binds a listener; `AcceptClientAsync` opens upstream only after an external client attaches. | This is a relay, not standalone connection ownership. Implement direct transport and migration, P02-P04. |
| A02 | `Simulator/MapSimulator.cs::IssuePacketOwnedSelectCharacterDirectConnect` and `IssuePacketOwnedSelectCharacterByVacDirectConnect` record `LoginIssuedDirectConnect` and entry presentation. `Contracts/GameSessionOptions.cs` lacks an online authority service. | Endpoint metadata and packet handlers do not execute authentication-to-field migration. P01-P04. |
| A03 | `Simulator/Physics/CVecCtrl.cs::CollisionDetectFloat` contains a commented-out foothold lookup/landing block; the float update calls it. N03 confirms native crossing/landing logic is substantially richer. | The movement backlog's `Implemented` float-collision claim is contradicted at this owner. Trace compensating player code, then port collision/timing, P05. |
| A04 | `Simulator/MapSimulator.UpdateLoop.cs::Update` uses `gameTime.ElapsedGameTime.Milliseconds / 1000f` alongside `Environment.TickCount`. | This uses a time component rather than total duration. Determine affected consumers and native catch-up/wrap behavior; do not assume replacing one expression establishes timing parity. P05. |
| A05 | `Simulator/Character/PlayerCombat.cs::CalculateDamage` uses 90-110% attack variance, a fixed 5% critical chance and 1.5 multiplier, and explicitly simplified defense. `IsMobInHitbox` uses a fixed 40x50 rectangle. `PlayerManager` constructs and calls this combat path. | Real gameplay still relies on simulator formulas/hit geometry. Recover v95 calculation, random sequence and authority rules; replace or isolate the approximation, P07. |
| A06 | `Simulator/Character/Skills/SkillLoader.cs::UsesAccuracyXAlias`, `UsesWeaponAttackXAlias` and `ApplyDescriptionBackedGenericStatAliases` infer stat semantics from English descriptions/names. | Localized text can influence gameplay; recover v95 skill-ID/data rules and explicitly separate generic preview support, P06-P07. |
| A07 | `Simulator/MapSimulator.ReviveOwnerParity.cs::ShouldUseCurrentFieldReviveSpawnApproximation` explicitly selects an approximation; extraction docs retain local offline respawn behavior. | Verify server/native death-return authority and field transitions, P04/P08. Do not label a local spawn fallback a completed v95 revive flow. |
| A08 | `Simulator/MapSimulator.ChatCommands.cs`, bridge/inbox managers and many `*Parity.cs` partials mix injected scenarios, status/control surfaces and production state. | Inventory callers and ownership before removal; extract capture/debug policy where it contaminates the production flow, P01/P03. Names alone are not deletion grounds. |
| A09 | Historical network plan opens with one active logical connection per role but later records singleton close/connect migration; N04 freshly confirms the latter native behavior. | Separate relay-tool topology from direct-client topology. Preserve recovered evidence and replace contradictory execution guidance, P02/P11. |
| A10 | Backlog 3 simultaneously claims completed float/ladder work and prioritizes removal of those stubs. Historical files repeatedly call themselves the single source of truth and point to old physical paths. | Use the new index/status model and research crosswalk; update individual claims only after source/native checks. |
| A11 | `Simulator/Character/PlayerCombat.cs::TryPickupDrop` removes/consumes nearby drops but leaves meso/inventory mutation as comments. | Pickup is not a complete character-state contract. P08 must connect rights, inventory/meso mutation, server results and offline behavior. |
| A12 | `Simulator/AI/MobAI.cs` uses a 50 ms trigger window, hardcoded alert/stun/death durations, and `Random.Shared.Next(100) < 35` skill preference. | Mob behavior is heuristic and nondeterministic relative to a native oracle. Recover timing, random streams, skill selection and server authority in P07. |
| A13 | `Simulator/Interaction/QuestRuntimeManager.cs` explicitly reports that some quest scripting requires unimplemented simulator script execution. | Classify script messages as client/server ownership and implement only the v95 contract in P08; do not silently fill the gap with guessed local scripting. |
| A14 | `Simulator/UI/Windows/ItemUpgradeUI.cs` calls itself a scaffold; `Character/PlayerManager.cs::CreatePlaceholderPlayer` is a deliberate fallback used when assets are absent. | Trace production/offline/editor callers before removal. Replace scaffolds at their real owner and retain explicit diagnostic fallbacks until P01/P08/P09 provide equivalents. |

## Cleanup disposition

These are evidence-led cleanup work packages, not authorization to delete every
matching filename. Before removing a seam, trace its real callers, applicable
native contract, offline/editor use, test ownership and replacement path.

| Candidate | Disposition | Required evidence before removal |
|---|---|---|
| Shared role proxies and feature bridge lifecycle knobs | Keep relay capability as tooling; stop making it the online-client authority. | Direct connection/migration and equivalent capture path work; consumers no longer require feature-owned lifecycle commands. |
| Repeated inbox/bridge/transport wrappers and retired-listener status shims | Consolidate per owning protocol boundary in P03. Retain distinct decode/state semantics. | Caller inventory, queue ordering, connection generation and outbound-byte equivalence. |
| Broad description-based skill aliases and modern/generic data fallback | Isolate from pinned v95 behavior or replace with native/data-backed rules in P06/P07. | Per-family provenance and representative native fixtures; preview compatibility classified explicitly. |
| Local damage, NPC/quest, login and respawn simulation | Make authority explicit; keep supported preview behavior behind the offline owner. | Online response-driven mutations and independent offline checks both pass. |
| Huge `MapSimulator` partials and utility catch-all state | Move coherent state/lifecycle owners as their vertical slice is fixed. | Same observable state/order/cleanup contract; avoid a file-splitting rewrite that leaves authority unchanged. |
| Test-facing constructors, reflection helpers, flags and generic parity snapshots | Audit individually under the test-audit skill. No deletion is approved by this review. | Full caller/history review and stronger surviving contract proof; see VALIDATION. |
| Generated string pools, packet constants and native data tables | Preserve unless independently disproved or unreachable in all supported modes. | Generation/provenance and native callers, not file size or naming. |

## Coverage of the remaining client

The execution queue covers client startup/account/character selection, transport and
stage migration, authoritative character/inventory/quest state, movement, avatar
assets, skills/combat/mobs, field/NPC/reactor/drop interactions, HUD/input/windows,
companions/social rooms, Cash Shop/ITC, special fields/minigames, rendering/audio,
and deployment/lifecycle. Broader domains contain existing implementations; the
review does not claim every branch in them is missing or wrong. Their packages
require native verification and targeted repair, with smaller child tasks derived
from the linked research. No completion percentage is defensible from feature
counts or historical `Implemented` rows.

Unknowns include the complete supported v95 data manifest, compatible test-server
configuration, live auth/migration behavior, independent native fixtures for most
subsystems, and visual/timing differences across the full game. P00 records these
inputs explicitly; unresolved inputs must not turn into guessed behavior.
