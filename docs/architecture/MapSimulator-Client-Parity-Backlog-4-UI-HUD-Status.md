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
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present, direct melee attacks now use the same delayed action scheduler as projectile and ground-warning families using WZ `attackAfter` timing instead of pure frame polling, mob skills now route their non-summon `MobSkill.img` `effect` and `mob` branches through the runtime animation layer so mob/target/screen-position skill cues no longer silently no-op, target-position mob skill visuals now anchor to the mob's actual live target instead of always snapping to the player when the mob is casting onto a summon or another mob, summon skill `200` still resolves its live summon list and limit from `MobSkill.img` so temporary summons enter the field through the mob pool instead of being skipped, common mob self or party buff skills now read `MobSkill.img` `x`/`time`/`lt`/`rb` data to apply runtime `PowerUp`, `MagicUp`, `PGuardUp`, `MGuardUp`, and `Speed` temp stats to the caster or nearby mobs instead of remaining visual-only, `MobSkill.img` skill `114` now uses the same WZ runtime area and `x` value to heal nearby mobs instead of staying cosmetic, common player-targeted mob debuffs now also read `MobSkill.img` `time` / `interval` / `prop` / `x` / `lt` / `rb` data so seal, darkness, weakness, stun or freeze, poison, slow, and dispel skills apply runtime local-player consequences instead of stopping at visuals, with darkness feeding player miss chance, weakness suppressing jumps, stun or freeze locking movement and skill use, poison ticking direct damage, slow scaling live move speed, and dispel clearing active buffs, while the rarer player-facing debuff family now also routes through the same runtime path instead of staying blocked by simulator heuristics: authored `MobSkill.img` `lt`/`rb` ranges now feed autonomous skill selection so wide-area curse, seduce, reverse-input, undead, and banish skills can fire from their published reach instead of a fixed short-range fallback, those same authored rectangles now mirror the mob's live facing when the runtime applies nearby or player-targeted mob skills, banish skill `129` now applies its runtime banish lockout after teleporting to spawn instead of short-circuiting at teleport only, undead skill `133` now reflects recovered HP using the authored `MobSkill.img/x` percentage instead of always inverting heals at a hardcoded full amount, inherited `MobSkill.img` level data for `lt` / `rb` / `time` / `interval` / `prop` / `count` / `targetMobType` / `x` / `y` / `hp` now also feeds the same runtime resolver instead of dropping back to partial defaults when later levels omit repeated fields, and the direct-attack disease lane now sources its extra player skill-blocking timer from that same runtime duration instead of a separate effect-time fallback. Regular attacks that carry `info/attack/*/disease` now also resolve the same runtime `MobSkill.img` status path and affected-hit animation feedback on contact instead of only applying the old blocking-only callback for seal, stun, freeze, seduce, or polymorph. Seduce-side control aftermath is now tighter too: the mob-status frame state explicitly marks pickup as blocked during forced-move control, the player manager now honors that state in the separate drop-pickup path, and entering seduce now forces the local player out of ladder or rope attachment before horizontal control takes over instead of leaving the character stuck in place on climb geometry or a seated hold state. Chained post-action combat decisions no longer incorrectly stall when the mob is currently targeting a summon or another mob instead of the player, and autonomous mob skill selection now honors WZ `priority`, `preSkillIndex` / `preSkillCount`, `onlyFsm`, and `skillForbid` so scripted-only skills stop leaking into the generic loop, charged follow-up skills wait for their prerequisite casts, and skill lockout windows stop immediately chaining into unrelated skills | The loop is closer to client `CMob` behavior, but the remaining gap is now mostly narrower control and aftermath polish beyond the shared WZ-backed status resolver: deeper client matching for reverse-input edge cases beyond the current directional swap, curse side rules beyond the current HP/MP cap clamp, and any still-unmodeled uncommon player-facing mob-skill transitions or branch-specific control rules after the new seduce forced-move cleanup and pickup lockout path. Focused `UnitTest_MapSimulator` coverage was added for seduce forced-move state and ladder-release control preparation, but this row could not be revalidated end-to-end in this run because `dotnet test UnitTest_MapSimulator` still stops on unrelated pre-existing `HaCreator` compile errors in `ClientOwnedVehicleSkillClassifier.cs` and `InventoryUI.cs`. | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `CombatEffects.cs`, `PlayerManager.cs`, `PlayerMobStatusController.cs`, `PlayerCharacter.cs`, `UnitTest_MapSimulator` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, `tremble`, and `type` attack metadata through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, triggers tremble feedback from flagged impacts, preserves puppet/summon aggro targets through AI updates, routes delayed mob hits to registered puppet targets instead of always falling back to the local player, now syncs targeted summons through their live runtime animation bounds instead of a fixed fallback box so summon-targeted mob attacks collide against the same world-space body rect the simulator draws, removes consumed summons from the active summon list when that branch lands, preserves summon slot indices alongside live summon object ids so queued summon-targeted attacks can still resolve through the client-style slot lane if the original summon instance is replaced before impact, preserves external mob targets through AI updates so delayed mob attacks can resolve `MobTargetType.Mob` entries against live target mob body rects and apply mob-vs-mob damage instead of silently dropping that client branch, now mirrors the client `CMob::ProcessAttack` rect pass for delayed direct or area hits by colliding non-targeted attack rectangles against every live summon and mob body in range instead of only the explicitly tracked target, lets explicitly targeted summon or mob projectiles apply their delayed impact when they reach the scheduled destination instead of depending only on the projectile transit hitbox, now also gives untargeted non-ground projectile impacts the same late rect sweep against the local player, live summons, and encounter mobs when the ball reaches its destination instead of dropping those arrival-time collateral hits, routes WZ `type = 1` delayed direct or ground attacks through the client-style locked-target branch so queued player, summon, or mob targets resolve on impact without depending on a late rectangle overlap test, matches the client `CMob::DoAttack` admission gate more closely by checking type-1 queue eligibility from the authored attack origin to a clamped target-body point within `range + 10` instead of the older broad rectangle overlap heuristic, now resolves those player-targeted admission checks, multiball lane picks, and locked delayed impacts through the live player body rect seam instead of a coarse position-only fallback, still gates delayed `jumpAttack` hits on grounded player or summon targets instead of damaging airborne targets on overlap alone, now parses WZ projectile `range/sp` + `range/r` launch-origin metadata, uses that authored origin for projectile spawn and type-1 admission checks, carries WZ `info/attack/*/attackCount` into live ranged attack entries, fans multi-ball projectiles across authored projectile lanes by choosing per-lane destinations against live player, summon, or encounter-mob bodies when those entities line up with the lane instead of cloning every extra ball off a fixed synthetic spread from the primary target, now synthesizes the extra multiball lane list from the full authored `range/start/areaCount` slot set instead of only the first `attackCount` offsets so wide projectile patterns stay centered and preserve authored lane order more closely, now keeps those authored lane points separate from the locked target body point so multiball projectiles continue to fly along the selected slot lane even when multiple balls resolve against the same live player, summon, or encounter-mob body, now also allows those per-lane heuristic resolutions to repeat the same live player, summon, or encounter-mob body across multiple bullets when separate authored lanes still line up with that body, reuses WZ-authored projectile and hit frame bounds for in-flight and arrival-time collision checks instead of a fixed synthetic `16x16` box, now reads WZ `attack/info/randDelayAttack` into delayed area attacks instead of synthesizing a generic window from range, expands WZ `type = 3` area attacks across every foothold intersecting the authored vertical column instead of collapsing them to a single grounded impact, and keeps WZ `type = 4` area attacks on the source mob's live Y lane instead of incorrectly snapping them to the nearest floor | `CMob::DoAttack` / `ProcessAttack` are closer on `MobBullet` setup and delayed area placement now that the simulator uses authored launch points, live player-body admission and impact resolution, full-slot multiball lane selection, authored-order extra bullet synthesis, authored lane-point preservation separate from delayed locked-target victim resolution, WZ-sized projectile-impact bounds, authored `randDelayAttack`, client-style type-3 multi-foothold column expansion, type-4 source-Y placement, and heuristic duplicate-target multiball resolution when multiple authored lanes legitimately overlap the same body. The remaining gap is narrower and still mostly packet or client owned: the simulator still synthesizes the client/server `m_aMultiTargetForBall` packet list from live lane heuristics rather than true server-fed lane points, does not yet mirror the final `MobBullet` travel-shape or arc side effects driven by `SetBallDestPoint`, and still approximates the client's per-entry random area-delay list by sampling against the authored `randDelayAttack` window instead of replaying a true server-fed `m_aRandTimeforAreaAttack` sequence. A focused `UnitTest_MapSimulator` seam test was added for repeated player-target lane resolution, but this row was not revalidated end-to-end in this run because the current `HaCreator` project still has unrelated pre-existing build errors in `ClientOwnedVehicleSkillClassifier`, `InventoryUI`, and `SkillUI` outside this backlog row. | `MobAI.cs`, `MobAttackSystem.cs`, `MobPool.cs`, `MapSimulator.cs` (`CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |
| Partial | Full mob stat/status parity | Mob statuses now keep concrete temporary-stat entries with expiry metadata, tick values, reset events, and authored auxiliary values, poison/venom/burn deal runtime DoT damage, freeze and stun hold mobs in their incapacitated state until expiry, active temp stats now modify outgoing damage, incoming damage, chase speed, and player-vs-mob hit chance, active status flags tint affected mobs in-world for visible feedback across the more common debuff, immunity, counter, and reward-mark states, player skill hits now infer and apply a broader WZ-backed slice of mob debuffs from loaded skill metadata for poison/venom, burn, freeze, stun, seal, blindness, darkness, slow, weakness, signed `EVA`, `amplifyDamage`, `elementalWeaken`, `mindControl`, `polymorph`, and `incapacitate`, WZ-backed debuff skills such as `1201006` Threaten now drive signed mob attack/defense/accuracy reductions from their loaded stat fields instead of collapsing to damage-only hits while the shared stat-driven debuff lane also now materializes signed mob `EVA` shifts whenever the loaded skill level actually carries that authored stat, the mob-skill self-buff lane now materializes the `MobSkill.img` defense and counter family by applying WZ-backed `140` physical immunity, `141` magic immunity, `142` hard-skin damage reduction, and `143`/`144`/`145` weapon or magic counter states as live mob temp stats instead of leaving those casts visual-only, `MobSkill.img/146` now genuinely resolves its authored `targetMobType` from WZ and clears active negative mob temp stats from self or nearby mobs through the shared status-target area seam, including signed attack/defense/accuracy/avoidability/speed debuffs instead of leaving those Threaten-style entries behind, and skill `info/mes` tokens still drive simulator-side debuff semantics like `buffLimit`, `attackLimit`, `restrict`, `Showdown`, `mindControl`, `polymorph`, `amplifyDamage`, and `elementalWeaken` so mob buffs cancel through the same shared positive-stat cleanup lane rather than only removing a hand-picked subset of classic buffs. The later WZ-backed Monster Carnival guardian self-buff slice from `MCGuardian.img` / `MobSkill.img` now also materializes `150` Power Up, `151` Guard Up, `152` Magic Up, `153` Shield Up, `154` Accuracy Up, `155` Avoidability Up, and `156` Speed Up as live mob temp stats instead of remaining ignored. The shared player-originated damage path now carries physical-vs-magic hit type into mob damage resolution so magic-side reductions and immunity actually gate magic attacks, reflected counter damage now feeds back into the local player from the authored `MobSkill.img` reflect percent and per-hit cap fields, the simulator now also materializes `MobSkill.img/157` as its own timed mob temp stat instead of dropping that WZ-authored cast on the floor, and mob-vs-player hit resolution now treats `Blind` as a forced miss while folding the player's live total avoidability plus temporary-stat-shifted accuracy and avoidability totals into the hit or miss chance instead of leaving those stat lanes cosmetic. Web-style roots now fully stop chase movement instead of only slowing it, doomed mobs lose combat actions while slowed, their runtime body hitbox and effect-height anchor now snap to the client-backed doom form footprint derived from mob `0100101.img` (`lt = -16,-34`, `rb = 19,0`) instead of staying at the original mob's collision profile, the same WZ-backed doom seam now swaps rendering onto the transformed `0100101.img` stand/move/hit/die frames instead of only changing collision, `Ambush` or `Neutralise` damage-vulnerability states resolve from WZ `x/time/prop` data, `Showdown` remains its own WZ-backed reward bonus instead of damage taken, and hypnotized mobs now claim their own runtime mob-target override lane so mind-control immediately forces mob-vs-mob aggro, refreshes that live target while the status is active, and cleanly releases the override when hypnosis ends or no candidate remains | Status-heavy encounters are more legible and more WZ-authored debuff or self-buff skills now push real mob temp stats instead of only dealing damage, the common mob-cast defense buffs no longer stop at visuals for the physical-immunity, magic-immunity, hard-skin, counter-attack, debuff-cleansing, Monster Carnival guardian self-buff slice, or the special `MobSkill.img/157` timed status, the shared clear-status seams now actually remove signed negative or positive temp-stat entries instead of leaving `ACC`/`EVA`/defense/speed shifts stuck behind generic dispel or buff-cancel casts, hypnosis no longer piggybacks on the generic encounter-target lane when redirecting mobs onto other mobs, and doom no longer leaves the original mob-sized body rectangle in place once the transformed state is active or keeps the original mob art after the transform lands. The remaining parity gap is narrower but still includes the exact client-side behavior attached to uncommon statuses like skill `157` and other rarer boss-only states beyond merely keeping them live as temp stats, any deeper movement-stat reservation semantics or branch-specific control rules tied to doom beyond the new client-backed collision and render swap, full client reward side effects for `Showdown`-style marks beyond the simulator's synthetic meso-drop lane, and any finer-grained client retarget priorities or exclusions inside hypnosis and other rare boss-only debuff branches that still need deeper client matching | `MobAI.cs`, `SkillManager.cs`, `PlayerCombat.cs`, `MapSimulator.cs`, effect loaders |
| Partial | Special mob interactions | WZ `selfDestruction` mobs now trigger reserved bomb/time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, generic WZ `info/removeAfter` encounter mobs now expire through the simulator `Timeout` death lane instead of living forever, escort and WZ `info/damagedByMob` encounter mobs no longer aggro the player, that `damagedByMob` lane now flows through a dedicated parsed mob-data flag instead of only the older simulator `Friendly` alias, escort, `damagedByMob`, and generic `removeAfter` encounter spawn points no longer auto-respawn through the normal mob-pool loop, special-death, escort, and `damagedByMob` encounter kills now suppress reward drops, WZ `info/damagedByMob` encounter mobs now ignore player-originated basic, skill, projectile, and summon damage, hostile mobs now retarget live escort or `damagedByMob` encounter actors onto the simulator's `MobTargetType.Mob` lane instead of staying player-only, and that lane now treats only live encounter participants as valid mob-vs-mob damage recipients so delayed area or projectile splash from encounter combat stops leaking onto unrelated ordinary mobs. Escort follow now respects live `life.info` progression by only attaching the lowest active escort index until that stage is cleared, Wild Hunter swallow routing now holds `33101005` targets in a stunned digest state with repeated wriggle hit pulses before resolving the `33101006` follow-up buff instead of immediately killing and buffing on first contact, and Astaroth-style rage bosses now parse WZ `ChargeCount` plus `AngerGauge` data, reserve `attack/info/AngerAttack` entries until the charge count fills, consume that gauge on the flagged attack, and render the mob-local anger gauge plus burst effect from the dedicated WZ animation nodes. Targeted automated coverage now also lives in committed seam tests: `MapleLib.Tests` covers the WZ parse boundaries for `info/damagedByMob`, string-or-int `info/removeAfter`, `info/selfDestruction`, `info/ChargeCount`, and `info/AngerGauge`, while `UnitTest_MapSimulator` now exercises timeout death handling, HP-threshold self-destruction reservation, anger-gauge charge-and-consume attack selection, escort-stage gating, reward suppression, and loader-side `attack/info/AngerAttack` metadata parsing. | This row's generic WZ-backed runtime behavior is now wired and covered at the seam level, so the remaining gap is narrower and mostly script or server owned: broader scripted rage or boss-phase logic beyond the Astaroth-style charge lane, exact Wild Hunter swallow success or failure branching and packet choreography outside the current digest-state hold, deeper map-specific escort scripting, and broader end-to-end simulation coverage for encounter retargeting or escort progression across complete map flows are still missing. In this run, the new `UnitTest_MapSimulator` coverage was added but could not be executed end-to-end because the current `HaCreator` project has unrelated pre-existing build errors outside this backlog row. | `MobItem.cs`, `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, field systems |


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
