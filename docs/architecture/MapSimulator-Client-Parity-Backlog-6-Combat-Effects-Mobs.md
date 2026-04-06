# MapSimulator Client Parity Backlog

## Purpose

This document is the single source of truth for MapSimulator parity work against the MapleStory client.

It does three things that the old notes did not do well:

1. Separates shipped work from missing work.
2. Distinguishes broad simulator features from player/avatar parity.
3. Anchors claims to actual code seams and, where relevant, client-side reverse engineering.

## Evidence Used

### Code seams reviewed

- `HaCreator/MapSimulator/MapSimulator.cs`
- `HaCreator/MapSimulator/Physics/CVecCtrl.cs`
- `HaCreator/MapSimulator/Character/CharacterLoader.cs`
- `HaCreator/MapSimulator/Character/CharacterAssembler.cs`
- `HaCreator/MapSimulator/Character/PlayerCharacter.cs`
- `HaCreator/MapSimulator/Character/Skills/SkillManager.cs`
- `HaCreator/MapSimulator/Effects/CombatEffects.cs`
- `HaCreator/MapSimulator/Pools/PortalPool.cs`
- `HaCreator/MapSimulator/UI/StatusBarUI.cs`
- `HaCreator/MapSimulator/UI/Windows/SkillUI.cs`
- `HaCreator/MapSimulator/UI/Windows/QuickSlotUI.cs`

### Client references checked

- `CUserLocal::DoActiveSkill_MeleeAttack` at `0x93b210`
- `CUserLocal::DoActiveSkill_ShootAttack` at `0x93b520`
- `CUserLocal::DoActiveSkill_MagicAttack` at `0x93a010`
- `CUserLocal::DoActiveSkill_Prepare` at `0x941710`
- `CUserLocal::TryDoingFinalAttack` at `0x93aaa0`
- `CUserLocal::TryDoingSerialAttack` at `0x93ac90`
- `CUserLocal::TryDoingSparkAttack` at `0x93abe0`
- `CUserLocal::Update` at `0x937330`
- `CUserLocal::SetMoveAction` at `0x903ce0`
- `CUserLocal::TryDoingPreparedSkill` at `0x944270`
- `CUserLocal::TryDoingRepeatSkill` at `0x93d400`
- `CUserLocal::DrawKeyDownBar` at `0x9153b0`
- `CUserLocal::CheckPortal_Collision` at `0x919a10`
- `CUserLocal::CheckReactor_Collision` at `0x903d20`
- `CUserLocal::TryDoingTeleport` at `0x932c00`
- `CUserLocal::TryDoingRush` at `0x90b8c0`
- `CUserLocal::TryDoingFlyingRush` at `0x90bc10`
- `CUserLocal::TryDoingRocketBooster` at `0x940e50`
- `CUserLocal::TryDoingRocketBoosterEnd` at `0x93d2d0`
- `CUserLocal::TryDoingSmoothingMovingShootAttack` at `0x92de70`
- `CUserLocal::TryDoingSwallowBuff` at `0x944520`
- `CUserLocal::TryDoingSwallowMobWriggle` at `0x93f500`
- `CUserLocal::TryDoingMine` at `0x907d70`
- `CUserLocal::TryDoingCyclone` at `0x932d60`
- `CUserLocal::TryDoingSitdownHealing` at `0x903d60`
- `CUserLocal::UpdateClientTimer` at `0x90e6d0`
- `CUserLocal::TryPassiveTransferField` at `0x91a2e0`
- `CUserLocal::TalkToNpc` at `0x9321f0`
- `CUserLocal::TryAutoRequestFollowCharacter` at `0x904bf0`
- `CUserLocal::TryLeaveDirectionMode` at `0x9054c0`
- `CUser::LoadLayer` at `0x8e96d0`
- `CUser::PrepareActionLayer` at `0x8e3070`
- `CUser::SetMoveAction` at `0x8df540`
- `CUser::Update` at `0x8fb8d0`
- `CUser::UpdateMoreWildEffect` at `0x8fb4c0`
- `CUser::PetAutoSpeaking` at `0x8deb10`
- `CVecCtrl::CollisionDetectFloat` at `0x994740`
- `CMovePath::IsTimeForFlush` at `0x666870`
- `CMovePath::MakeMovePath` at `0x667c90`
- `CMovePath::Flush` at `0x668160`
- `CMovePath::Encode` at `0x666e20`
- `CMovePath::Decode` at `0x667920`
- `CMovePath::OnMovePacket` at `0x6683f0`
- `CPortalList::FindPortal_Hidden` at `0x6ab5d0`
- `CMob::DoAttack` at `0x6504d0`
- `CMob::OnNextAttack` at `0x6528a0`
- `CMob::ProcessAttack` at `0x652950`
- `CMob::GetAttackInfo` at `0x641330`
- `CField::IsFearEffectOn` at `0x52a420`
- `CField::OffFearEffect` at `0x52b810`
- `CUISkill::Draw` at `0x84ed90`
- `CUISkill::GetSkillRootVisible` at `0x84a6f0`
- `CUISkill::GetRecommendSKill` at `0x84e710`
- `CUIStatusBar::Draw` at `0x876cd0`
- `CUIStatusBar::SetStatusValue` at `0x873590`
- `CUIStatusBar::SetNumberValue` at `0x873d50`
- `CUIStatusBar::ChatLogAdd` at `0x87aec0`
- `CUIStatusBar::CQuickSlot::Draw` at `0x875750`
- `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` at `0x871000`

## Client Function Index By Backlog Area

The list below maps each backlog area to concrete client seams confirmed in IDA.
These are the first functions to inspect before changing simulator behavior.

### 1. Mob combat and encounter parity

- `CMob::DoAttack` at `0x6504d0`
- `CMob::OnNextAttack` at `0x6528a0`
- `CMob::ProcessAttack` at `0x652950`
- `CMob::GetAttackInfo` at `0x641330`

## Current State Summary

MapSimulator is no longer "missing player simulation".
The project already has:

- A playable local character with foothold, ladder, rope, swim, fly, jump, prone, hit, death, and respawn states.
- Character WZ loading for body, head, face, hair, equipment, weapon overlays, z-map ordering, and slot-visibility conflicts.
- A working generic skill system with job skill loading, buffs, cooldowns, projectiles, hit effects, and quick-slot assignment.
- Mob and drop pools, hidden portal support, damage number rendering from WZ assets, mob HP bars, and boss HP bars.
- Several field-effect systems and special field implementations that are already present in the current simulator.

The real parity gap is no longer "does the simulator have a feature at all?".
The gap is now "how closely does the current implementation match client behavior, data selection, timing, and fallback rules?".

## Recent Progress Summary

Recent implementation progress has been folded into the backlog tables below instead of being tracked as a separate changelog.
The main baseline updates worth keeping visible are:

- Avatar parity moved out of the stub phase: action coverage, rare-action rendering, facial expression behavior, and anchor fallback rules are implemented, while mount and transform handling is partially in place.
- Skill parity is broader than the old notes implied: the simulator now loads and casts the full player skill catalog, supports runtime job swaps, shows buff and cooldown feedback, and covers more movement-family and summon behavior.
- Physics and movement now have real runtime seams for ladder lookup, float collision, movement-path snapshots, and moving-platform foothold sync.
- Combat and UI both shifted from "missing feature" to "refinement": HP indicators, damage rendering, status-bar layout, warning flashes, chat strip behavior, skill UI layout, and tooltip reuse all exist but still need client-accurate polish.
- NPC and quest interaction now have a usable simulator baseline through dialogue overlays, quest lists, and common quest-state mutations, but full scripting and inventory-backed requirements remain incomplete.

## Remaining Parity Backlog

### 1. Mob combat and encounter parity

The simulator has working mob AI and effects, but not the full client combat pipeline.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present, direct melee attacks now use the same delayed action scheduler as projectile and ground-warning families using WZ `attackAfter` timing instead of pure frame polling, mob skills now route their non-summon `MobSkill.img` `effect` and `mob` branches through the runtime animation layer so mob/target/screen-position skill cues no longer silently no-op, target-position mob skill visuals now anchor to the mob's actual live target instead of always snapping to the player when the mob is casting onto a summon or another mob, summon skill `200` still resolves its live summon list and limit from `MobSkill.img` so temporary summons enter the field through the mob pool instead of being skipped, common mob self or party buff skills now read `MobSkill.img` `x`/`time`/`lt`/`rb` data to apply runtime `PowerUp`, `MagicUp`, `PGuardUp`, `MGuardUp`, and `Speed` temp stats to the caster or nearby mobs instead of remaining visual-only, `MobSkill.img` skill `114` now uses the same WZ runtime area and `x` value to heal nearby mobs instead of staying cosmetic, common player-targeted mob debuffs now also read `MobSkill.img` `time` / `interval` / `prop` / `x` / `lt` / `rb` data so seal, darkness, weakness, stun or freeze, poison, slow, and dispel skills apply runtime local-player consequences instead of stopping at visuals, with darkness feeding player miss chance, weakness suppressing jumps, stun or freeze locking movement and skill use, poison ticking direct damage, slow scaling live move speed, and dispel clearing active buffs, while the rarer player-facing debuff family now also routes through the same runtime path instead of staying blocked by simulator heuristics: authored `MobSkill.img` `lt`/`rb` ranges now feed autonomous skill selection so wide-area curse, seduce, reverse-input, undead, and banish skills can fire from their published reach instead of a fixed short-range fallback, those same authored rectangles now mirror the mob's live facing when the runtime applies nearby or player-targeted mob skills, banish skill `129` now applies its runtime banish lockout after teleporting to spawn instead of short-circuiting at teleport only, undead skill `133` now reflects recovered HP using the authored `MobSkill.img/x` percentage instead of always inverting heals at a hardcoded full amount, potion-style and quest-buff consumable recovery now route through `PlayerCharacter.Recover` so the same runtime curse caps, undead reflected-heal percentage, and local-player recovery modifiers apply there too instead of only on skill- or chair-driven recovery, and mob skill `135` now blocks both HP or MP recovery consumables plus cure-only consumables through the same runtime `StopPotion` lane instead of only blocking raw recovery. Inherited `MobSkill.img` level data for `lt` / `rb` / `time` / `interval` / `prop` / `count` / `targetMobType` / `x` / `y` / `hp` now also feeds the same runtime resolver instead of dropping back to partial defaults when later levels omit repeated fields, sparse higher-level `MobSkill.img` branches now also inherit prior `affected` / `effect` / `mob` / `tile` / `time` data through the shared resolver so direct-cast hit feedback, mob-side cues, and field-owned visuals keep rendering when later levels only carry an `info` stub, summon skill `200` now inherits authored `limit`, `lt` / `rb`, and summon-entry lists from the nearest previous level instead of treating sparse follow-up levels as empty, and mob skill source slots now preserve the authored WZ `info/skill/*` index so `preSkillIndex` chains key off the actual published slot instead of the loader's incidental enumeration order. The direct-attack disease lane now sources its extra player skill-blocking timer from that same runtime duration and only keeps that lockout when the shared runtime status lane actually lands instead of still blocking on resisted or failed casts, direct player-targeted mob skill casts now also play their authored `MobSkill.img/affected` hit feedback when the runtime status applies instead of reserving that animation lane to contact diseases only, and clearing or expiring those same runtime statuses now also clears the mirrored player skill-blocking lane immediately instead of leaving cured seal, stun, freeze, seduce, or polymorph casts blocked until the stale timer elapses. Regular attacks that carry `info/attack/*/disease` now also resolve the same runtime `MobSkill.img` status path and affected-hit animation feedback on contact instead of only applying the old blocking-only callback for seal, stun, freeze, seduce, or polymorph. Seduce-side control aftermath is now tighter too: the mob-status frame state explicitly marks pickup as blocked during forced-move control, the player manager now honors that state in the separate drop-pickup path, and entering seduce now forces the local player out of ladder or rope attachment before horizontal control takes over instead of leaving the character stuck in place on climb geometry or a seated hold state. Chained post-action combat decisions no longer incorrectly stall when the mob is currently targeting a summon or another mob instead of the player, and autonomous mob skill selection now honors WZ `priority`, `preSkillIndex` / `preSkillCount`, `onlyFsm`, and `skillForbid` so scripted-only skills stop leaking into the generic loop, charged follow-up skills wait for their prerequisite casts, and skill lockout windows stop immediately chaining into unrelated skills | The loop is closer to client `CMob` behavior, and the remaining gap is now mostly narrower control and aftermath polish beyond the shared WZ-backed status resolver: deeper client matching for reverse-input edge cases beyond the current directional swap, any broader client-side `StopPotion` semantics outside the now-wired recovery and cure-consumable lanes, curse side rules beyond the current shared HP/MP cap clamp, and any still-unmodeled uncommon player-facing mob-skill transitions or branch-specific control rules after the new seduce forced-move cleanup, pickup lockout path, resisted-cast blocking fix, sparse-level `MobSkill.img` inheritance pass, authored source-slot preservation for prerequisite skill chains, and any remaining rare player-facing status ids that still need a concrete client-backed consequence beyond the now-synced clear or expiry path. Focused `UnitTest_MapSimulator` coverage was added for `PlayerMobStatusConsumableRecoveryParityTests`, but this row could not be revalidated end-to-end in this run because `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj --filter PlayerMobStatusConsumableRecoveryParityTests` still stops on unrelated pre-existing compile errors in `HaCreator/MapSimulator/Companions/CompanionEquipmentController.cs` and `HaCreator/MapSimulator/Managers/LocalUtilityPacketInboxManager.cs` before the new tests execute. | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `MobSkillEffectLoader.cs`, `MobSkillLevelResolver.cs`, `MobData.cs`, `MobSkillData.cs`, `MobItem.cs`, `CombatEffects.cs`, `PlayerManager.cs`, `PlayerMobStatusController.cs`, `PlayerCharacter.cs`, `UnitTest_MapSimulator` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, `tremble`, and `type` attack metadata through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, triggers tremble feedback from flagged impacts, preserves puppet/summon aggro targets through AI updates, routes delayed mob hits to registered puppet targets instead of always falling back to the local player, now syncs targeted summons through their live runtime animation bounds instead of a fixed fallback box so summon-targeted mob attacks collide against the same world-space body rect the simulator draws, removes consumed summons from the active summon list when that branch lands, preserves summon slot indices alongside live summon object ids so queued summon-targeted attacks can still resolve through the client-style slot lane if the original summon instance is replaced before impact, preserves external mob targets through AI updates so delayed mob attacks can resolve `MobTargetType.Mob` entries against live target mob body rects and apply mob-vs-mob damage instead of silently dropping that client branch, now mirrors the client `CMob::ProcessAttack` rect pass for delayed direct or area hits by colliding non-targeted attack rectangles against every live summon and mob body in range instead of only the explicitly tracked target, lets explicitly targeted summon or mob projectiles apply their delayed impact when they reach the scheduled destination instead of depending only on the projectile transit hitbox, now also gives untargeted non-ground projectile impacts the same late rect sweep against the local player, live summons, and encounter mobs when the ball reaches its destination instead of dropping those arrival-time collateral hits, routes WZ `type = 1` delayed direct or ground attacks through the client-style locked-target branch so queued player, summon, or mob targets resolve on impact without depending on a late rectangle overlap test, matches the client `CMob::DoAttack` admission gate more closely by checking type-1 queue eligibility from the authored attack origin to a clamped target-body point within `range + 10` instead of the older broad rectangle overlap heuristic, now resolves those player-targeted admission checks, multiball lane picks, and locked delayed impacts through the live player body rect seam instead of a coarse position-only fallback, now also narrows the WZ `jumpAttack` grounded gate to the same untargeted direct-rect player or summon overlap branch the client uses instead of incorrectly suppressing airborne hits from locked-target, projectile, or area impact paths, now parses WZ projectile `range/sp` + `range/r` launch-origin metadata, uses that authored origin for projectile spawn and type-1 admission checks, carries WZ `info/attack/*/attackCount` into live ranged attack entries, fans multi-ball projectiles across authored projectile lanes by choosing per-lane destinations against live player, summon, or encounter-mob bodies when those entities line up with the lane instead of cloning every extra ball off a fixed synthetic spread from the primary target, now synthesizes the extra multiball lane list from the full authored `range/start/areaCount` slot set instead of only the first `attackCount` offsets so wide projectile patterns stay centered and preserve authored lane order more closely, now keeps those authored lane points separate from the locked target body point so multiball projectiles continue to fly along the selected slot lane even when multiple balls resolve against the same live player, summon, or encounter-mob body, now also allows those per-lane heuristic resolutions to repeat the same live player, summon, or encounter-mob body across multiple bullets when separate authored lanes still line up with that body, reuses WZ-authored projectile and hit frame bounds for in-flight and arrival-time collision checks instead of a fixed synthetic `16x16` box, now mirrors the client `CMob::SetBallDestPoint` destination clamp for projectile travel by enforcing a forward lane, clamping the vertical slope to `0.6`, and normalizing against authored `range/r`, now reads WZ `attack/info/randDelayAttack` into delayed area attacks, now synthesizes the simulator-side random area delay list once per selected area slot so every foothold expanded from the same client-style `type = 3` column shares one queued delay instead of rolling a fresh delay per foothold hitbox, and now also exposes a client-shaped override seam for `m_aMultiTargetForBall` and `m_aRandTimeforAreaAttack` so the attack scheduler will prefer packet-fed extra projectile lane points and per-slot area delays whenever those lists are supplied. In this run, the existing `MoveMob` packet codec and mob-attack inbox were extended to preserve packet `TARGETINFO` too, so the next queued attack now prefers packet-owned locked player, summon-slot, or mob victims through the same live body-rect seams the simulator uses for delayed type-1 admission and impact resolution instead of dropping that client-owned target branch and falling back to AI heuristics, packet-owned move-header attack admission now clears queued overrides when `NextAttackPossible` is false while carrying packet-facing into projectile lane fallback, launch-origin orientation, `SetBallDestPoint` forward-lane clamping, and locked-target admission-distance checks instead of always falling back to the live AI-facing bit, and the same packet path now also honors client `bNotChangeAction` by refusing to queue simulator attack overrides for move headers that suppressed `CMob::DoAttack` on the client instead of leaking stale packet-owned locked targets or projectile lane lists into a later AI-selected attack. | `CMob::DoAttack` / `ProcessAttack` are closer on `MobBullet` setup and delayed area placement now that the simulator uses authored launch points, live player-body admission and impact resolution, full-slot multiball lane selection, authored-order extra bullet synthesis, authored lane-point preservation separate from delayed locked-target victim resolution, client-style direct-only `jumpAttack` grounding, WZ-sized projectile-impact bounds, client-style `SetBallDestPoint` destination normalization, authored `randDelayAttack`, per-slot random-delay reuse across type-3 multi-foothold expansions, client-style type-3 multi-foothold column expansion, type-4 source-Y placement, heuristic duplicate-target multiball resolution when multiple authored lanes legitimately overlap the same body, and packet-owned `TARGETINFO`, `m_aMultiTargetForBall`, `m_aRandTimeforAreaAttack`, `NextAttackPossible`, and `bNotChangeAction` ingestion through the live `MoveMob` inbox path instead of only through manual override seams. The remaining gap is now mostly deeper client-owned `MobBullet` behavior and movement-discard aftermath: the runtime still does not mirror richer travel-shape, arc, or other post-launch side effects beyond the now-matched `SetBallDestPoint` destination clamp, and it still lacks a concrete consumer for the packet `bNotForceLandingWhenDiscard` move-discard bit or any broader client-only mob-attack state beyond `TARGETINFO`, `NextAttackPossible`, facing, `bNotChangeAction`, and the two override lists currently carried through the move-attack header seam. Focused `UnitTest_MapSimulator` seam coverage was added for `MobMoveAttackPacketCodec`, but `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj --filter MobMoveAttackPacketCodecTests` is currently blocked by unrelated pre-existing generated-file build failures under `HaRepacker` (`obj\\Debug\\net10.0-windows\\*.g.cs` missing in the temporary WPF project) before the new tests execute. | `MobAI.cs`, `MobAttackSystem.cs`, `MobPool.cs`, `MapSimulator.cs`, `MobMoveAttackPacketCodec.cs` (`CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |
| Partial | Full mob stat/status parity | Existing mob-temp-stat parity work remains in place (runtime status entries with expiry/tick metadata, signed stat shifts, DoT ticks, freeze/stun lock, immunity/counter lanes, threat/showdown/mind-control/polymorph inference, mob-skill status clear/self-buff lanes, and doom render/collision swap to `mob/0100101.img`). In this run, the doom movement seam now reserves transformed speed to the WZ-backed doom baseline from `mob/0100101.img/info/speed = -50` instead of a hardcoded synthetic slow, the hypnosis target resolver now excludes doomed candidates and explicitly prioritizes same-team encounter targets ahead of same-team non-encounter fallback candidates, `MobSkill.img/157` (`info` rich token, `x=1`, `time=180`) now also contributes a concrete reward-side consequence by scaling meso payout through the existing mob reward lane (`x` maps to +`x*100%` meso bonus while active) instead of remaining visual/status-only, and inferred `Showdown` reward bonus parity now also reaches the simulator's current item reward seam instead of stopping at EXP: the official skill text in `string/Skill.img` (`4121003` / `4221003`) says Showdown increases both EXP and items, so active showdown now converts its percent bonus into guaranteed-plus-remainder extra quantity on mob reward items, which currently means monster-card drops spawned from the mob death reward lane can award an additional card copy on the same percentage basis. | Status-heavy encounters now track more client-shaped consequences beyond status visuals: doom movement speed reservation is WZ-backed, hypnosis retargeting has clearer target exclusion plus same-team Carnival-like priority order, `157` is no longer a no-op for rewards, and showdown-side reward parity now affects the simulator's actual item reward output instead of EXP only. Remaining gap is still narrower but real: exact client semantics for uncommon boss-only statuses (including whether `157` has additional branches beyond this meso lane), deeper doom control/branch behavior outside the speed reservation seam, the eventual general-item drop-table hookup beyond the simulator's current monster-card item reward seam, and packet-fed hypnosis victim selection or other fine-grained retarget priority/exclusion branches still need client-side matching. Focused `UnitTest_MapSimulator` coverage was added in `MobStatusParityTests`, but `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj --filter MobStatusParityTests` is currently blocked by unrelated pre-existing `HaCreator` compile errors in `MapSimulator/UI/Windows/TrunkUI.cs` where `TrunkUI` no longer satisfies the full `ISoftKeyboardHost` interface, so the new tests do not execute until that separate build break is fixed. | `MobAI.cs`, `HypnotizeTargetResolver.cs`, `MobStatusRewardParity.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator` |
| Partial | Special mob interactions | WZ `selfDestruction` mobs now trigger reserved bomb or time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, and generic WZ `info/removeAfter` encounter mobs now expire through the simulator `Timeout` death lane instead of living forever. Escort and WZ `info/damagedByMob` encounter mobs no longer aggro the player, that lane now flows through a dedicated parsed mob-data flag instead of only the older simulator `Friendly` alias, escort, `damagedByMob`, and generic `removeAfter` encounter spawn points no longer auto-respawn through the normal mob-pool loop, and special-death, escort, `damagedByMob`, swallow, and timeout encounter kills now suppress reward drops. WZ `info/damagedByMob` encounter mobs now ignore player-originated basic, skill, projectile, and summon damage, hostile mobs now retarget live escort or `damagedByMob` encounter actors onto the simulator's `MobTargetType.Mob` lane instead of staying player-only, and that lane now treats only live encounter participants as valid mob-vs-mob damage recipients so delayed area or projectile splash from encounter combat stops leaking onto unrelated ordinary mobs. Escort follow now respects live `life.info` progression by only attaching the lowest active escort index until that stage is cleared, Wild Hunter swallow routing now mirrors the client-backed digest cadence more closely by holding `33101005` targets in repeated `500 ms` wriggle suspension pulses across the `3000 ms` digest window before resolving the `33101006` follow-up buff, and unresolved absorb outcomes now expire through a bounded client-style fallback window derived from the swallow action timing plus wriggle cadence so stale pending swallow states auto-cancel instead of hanging indefinitely when no absorb result arrives. Astaroth-style rage bosses now parse WZ `ChargeCount` plus `AngerGauge` data, reserve `attack/info/AngerAttack` entries until the charge count fills, consume that gauge on the flagged attack, and render the mob-local anger gauge plus burst effect from the dedicated WZ animation nodes. Automated seam coverage now exists for the WZ parse and simulator-entry paths used by this row: `MapleLib.Tests` covers `info/damagedByMob`, string-or-int `info/removeAfter`, `info/selfDestruction`, `info/ChargeCount`, and `info/AngerGauge`, while `UnitTest_MapSimulator` now contains focused tests for swallow wriggle scheduling, encounter reward suppression, timeout death handling, HP-threshold self-destruction reservation, anger-gauge charge and consume attack selection, and loader-side `attack/info/AngerAttack` metadata parsing. | The remaining gap is now narrower and mostly script, packet, or end-to-end owned rather than generic WZ seam work: broader scripted rage or boss-phase logic beyond the Astaroth-style charge lane, any remaining Wild Hunter swallow packet-order edge cases now that unresolved absorb outcomes time out through the new bounded cancel fallback on top of the existing `500 ms` wriggle suspension loop plus `3000 ms` digest gate, deeper map-specific escort scripting, and broader full-map simulation coverage for encounter retargeting or escort progression are still missing. Verification is also still partially blocked by unrelated `HaCreator` compile failures in the current tree, so `dotnet test UnitTest_MapSimulator/UnitTest_MapSimulator.csproj --filter SpecialMobInteractionParityTests` does not complete until those external errors are fixed. | `MobItem.cs`, `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, field systems |


## Priority Order

If the goal is visible parity first, the next work should be sequenced like this:

1. Avatar parity pass:
   Validate default asset selection, client-managed overlay/effect layers, and remaining action coverage against WZ and client behavior.
2. Skill execution pass:
   Implement queued follow-up, repeat-skill, bound-jump, rocket-booster teardown, and movement-coupled shoot families rather than extending the generic cast path indefinitely.
3. UI feedback pass:
   Add quick-slot validation, quest or balloon feedback, chat surfaces, and complete skill-window behavior.
4. Movement refinement pass:
   Remove the remaining ladder/float stubs, add passive transfer-field handoff, and tighten platform/dynamic foothold sync.
5. Interaction pass:
   Add NPC talk/quest flow, follow and direction-mode handling, and richer reactor interactions.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".

