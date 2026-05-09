
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

- `CActionMan::GetMobImgEntry` at `0x419f20`

- `CActionMan::LoadMobAction` at `0x41f530`
- `CAnimationDisplayer::Effect_Tremble` at `0x439a70`
- `CAnimationDisplayer::Effect_HP` at `0x444eb0`
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



### 2. Mob image-entry ownership discovered by targeted `CActionMan` scan

- `CActionMan::GetMobImgEntry` at `0x419f20`

Notes:
The follow-up `CActionMan` scan shows the client resolves mob template IMG data through a distinct cached owner before `LoadMobAction` runs. `GetMobImgEntry` formats the mob resource path through the StringPool-backed template, loads the property tree from the resource manager, follows `info/link` by recursively reopening the linked template, and copies missing top-level branches from the linked image onto the cached entry before later action loading consumes it. That means mob parity is split three ways rather than two: runtime behavior in `CMob::*`, image-entry normalization in `GetMobImgEntry`, and then frame-table construction in `LoadMobAction`.

### 3. Mob action-loader ownership discovered by targeted `CActionMan` scan



- `CActionMan::LoadMobAction` at `0x41f530`



Notes:

The combat backlog already tracked `CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, and `CMob::GetAttackInfo`, but the targeted `CActionMan` scan shows the client also keeps a distinct mob action-loader owner that was not named anywhere in this backlog set. `CActionMan::LoadMobAction` caches by `(templateId, action)` before the runtime ever updates or attacks, resolves the action node from the mob IMG entry through the static `s_sMobAction` table, enumerates each frame canvas, reads per-frame `delay`, `head`, `lt`, `rb`, and multi-body metadata, computes the frame rect and head anchor, and appends a reversed playback pass when the action publishes the client reverse flag. That means mob parity is still split the same way as summons, pets, employees, and NPCs in the other backlog docs: `CMob::*` owns behavior and targeting, while `CActionMan::LoadMobAction` owns the frame-table construction, delay fallback, cached reuse, and reverse-playback shape that the runtime consumes.

### 4. Combat feedback animation-owner parity discovered by `CAnimationDisplayer` scan

- `CAnimationDisplayer::Effect_Tremble` at `0x439a70`
- `CAnimationDisplayer::Effect_HP` at `0x444eb0`

Notes:
The broader combat rows already talked about damage numbers, hit feedback, and tremble, but the function scan shows the client owns that presentation through a dedicated animation manager rather than only through mob runtime code or UI widgets. `CAnimationDisplayer::Effect_HP` selects the red/blue/violet number families plus the separate critical branch, formats the number text through StringPool id `0x1A15`, composes the digit canvases into a temporary layer, inserts the extra critical banner, and then registers the whole result as a one-time animation. `CAnimationDisplayer::Effect_Tremble` likewise owns the screen-shake timing state directly by recording start/end ticks, the heavy-vs-normal reduction curve, and the config/enforcement gate before the effect ever reaches draw/update. That means combat feedback parity is also split three ways in this backlog set: `CMob::*` owns combat behavior, `CActionMan::LoadMobAction` owns mob frame construction, and `CAnimationDisplayer` owns the visible damage-number and tremble presentation the player actually sees.

### 5. Mob anger-gauge burst animation-owner discovered by follow-up IDA scan

- `CMob::AngerGaugeFullChargeEffect` at `0x6490b0`
- `CAnimationDisplayer::Effect_FullChargedAngerGauge` at `0x457d00`

Notes:
The broader special-mob row already tracks WZ-authored `ChargeCount`, `AngerGauge`, and `attack/info/AngerAttack`, but the follow-up IDA pass shows the visible full-charge burst also has a distinct client owner that is not named anywhere else in the backlog set. `CMob::AngerGaugeFullChargeEffect` rate-limits the burst by `m_bFullChargeEffectStartTime + m_bFullChargeEffectTime`, formats the UOL from the mob template id through StringPool ids `0x3CE` and `0xC2F`, and then routes that authored effect path through `CAnimationDisplayer::Effect_FullChargedAngerGauge` with the mob head vector as origin and the action layer as overlay parent. `Effect_FullChargedAngerGauge` is itself not a generic helper: it loads the authored layer, forces `GA_STOP` animation playback, and registers the result through `RegisterOneTimeAnimation` rather than leaving anger-gauge presentation on the mob runtime or the existing generic combat-feedback rows. That means anger-gauge parity is split one level further than the current doc suggests: WZ/runtime charge logic stays in the special-mob row, while the visible full-charge burst belongs to its own mob-to-animation-displayer owner seam.


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

| Status     | Area                        | Gap | Why it matters | Primary seam |
|------------|-----------------------------|-----|----------------|--------------|
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present. The local loop now keeps direct melee, projectile, ground-warning, direct skill, contact-disease, summon, mob-side buff/heal/cure, player debuff, affected-hit feedback, `MobSkill.img` sparse-level inheritance, authored source-slot prerequisite chains, `priority` / `onlyFsm` / `skillForbid` selection, WZ `lt` / `rb` range mirroring, summon limits/spawn rectangles, WZ-authored summon-entry presentation (`summonEffect` -> `Effect/Summon.img/<id>`), no-op status gating, mirrored player skill-blocking cleanup, seduce control modes, `StopMotion`, `StopPotion`, curse EXP scaling, undead recovery reflection, Battlefield flag team handoff, elemental resistance, direct mob-skill tile areas, WZ-authored mob skill MP costs (`mpCon`) backed by `MobData.MaxMP`, and WZ-authored natural mob HP/MP recovery (`info/hpRecovery` / `info/mpRecovery`) backed by parsed `MobData` on the existing runtime seams. Periodic player-status damage from count-driven `MobSkill.img` data keeps oversized authored `interval` values that cannot fit inside the WZ `time` / `count` window, such as `MobSkill.img/134` (`time=10`, `count=8`, `interval=48`), `138` (`time=60`, `count=30`, `interval=90`), and higher-level `132` HP damage rows, normalized through the WZ-fit frame-count interpretation instead of being treated as seconds and expiring before any tick can land. The runtime also carries `MobSkill.img` `mpCon` into local skill entries, initializes mob MP from parsed WZ `info/maxMP`, blocks skill starts that cannot pay the authored cost, spends the cost when the skill action begins, applies parsed WZ `hpRecovery` / `mpRecovery` through `MobAI` natural recovery ticks with max-value clamping and death gating so MP-cost skill selection can reopen after authored recovery, replays WZ `summonEffect` visuals at local mob-summon spawn points through the existing animation-effect seam, and now runs local mob cooldowns, state elapsed checks, action-recovery gates, skill-forbid windows, natural-recovery scheduling, boss/target timeout checks, damage-number expiry, applied mob-skill effect retention expiry, and self-destruction/remove-after timers through the same unsigned client tick semantics used by recovered client-owned timed effects. Client evidence for this timing lane is `CMob::DoAttack` (`get_update_time`, delayed entry times as `tCur + tAttackAfter`) and `CMob::ProcessAttack` (`tCur - tStart >= 0`, `tCur - ATTACKENTRY.tTime >= 0`) paired with existing WZ-authored `MobSkill.img` interval/count/mpCon/summonEffect data, `Effect/Summon.img` effect owners, and mob `info/maxMP`, `info/hpRecovery`, `info/mpRecovery`. | Periodic pain-mark, burn, and high-level reverse-input HP damage now tick inside their authored duration instead of becoming visual-only statuses when `interval` is published in frame-count form, local mob skill selection respects WZ-authored MP costs instead of allowing every skill while the mob has no available MP, mobs with authored recovery rows regain local HP/MP inside the loop instead of staying permanently drained after `mpCon` spend, WZ-authored mob summon casts now show their selected `Effect/Summon.img` entry at each local spawn point instead of silently materializing summoned mobs, and long-running sessions no longer allow mob action recovery, recovery ticks, forbid windows, cooldowns, duplicate mob-skill effect retention, or timeout gates to fire early or stall across `Environment.TickCount` rollover. Remaining gap is still `Partial`: deeper native skill-choice/recast lead policy, packet/server-owned MP corrections, branch-specific seduce/fear/reverse/stop-motion aftermath, uncommon event-only status consequences, byte-exact `CMob::ProcessAttack`/`CUserLocal::OnHit` side effects beyond the recovered delayed-entry timing gate, and broader field-script-owned transitions still need client-backed confirmation beyond the current WZ-driven local runtime approximation. | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `MobSkillEffectLoader.cs`, `MobSkillLevelResolver.cs`, `MobSkillData.cs`, `MobSummonSkillInfo.cs`, `MobItem.cs`, `CombatEffects.cs`, `MobSkillSelectionParity.cs`, `PlayerManager.cs`, `PlayerMobStatusController.cs`, `PlayerCharacter.cs`, `AffectedAreaPool.cs`, `Effect/Summon.img`, `UnitTest_MapSimulator/MobStatusParitySeamTests.cs`, `UnitTest_MapSimulator/MobSkillRuntimeSelectionParityTests.cs`, client `CMob::DoAttack`, client `CMob::ProcessAttack` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, `tremble`, `knockback`, and `type` attack metadata through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, triggers tremble feedback from flagged impacts, now carries WZ `info/attack/*/knockback` from parsed mob attack metadata into live attack entries and gates player plus mob-vs-mob impact knockback on that authored flag while preserving existing touch/skill fallback knockback, preserves puppet/summon aggro targets through AI updates, routes delayed mob hits to registered puppet targets instead of always falling back to the local player, now syncs targeted summons through their live runtime animation bounds instead of a fixed fallback box so summon-targeted mob attacks collide against the same world-space body rect the simulator draws, removes consumed summons from the active summon list when that branch lands, preserves summon slot indices alongside live summon object ids so queued summon-targeted attacks can still resolve through the client-style slot lane if the original summon instance is replaced before impact, preserves external mob targets through AI updates so delayed mob attacks can resolve `MobTargetType.Mob` entries against live target mob body rects and apply mob-vs-mob damage instead of silently dropping that client branch, now mirrors the client `CMob::ProcessAttack` rect pass for delayed direct or area hits by colliding non-targeted attack rectangles against every live summon and mob body in range instead of only the explicitly tracked target, lets explicitly targeted summon or mob projectiles apply their delayed impact when they reach the scheduled destination instead of depending only on the projectile transit hitbox, now also gives untargeted non-ground projectile impacts the same late rect sweep against the local player, live summons, and encounter mobs when the ball reaches its destination instead of dropping those arrival-time collateral hits, routes WZ `type = 1` delayed direct or ground attacks through the client-style locked-target branch so queued player, summon, or mob targets resolve on impact without depending on a late rectangle overlap test, matches the client `CMob::DoAttack` admission gate more closely by checking type-1 queue eligibility from the authored attack origin to a clamped target-body point within `range + 10` instead of the older broad rectangle overlap heuristic, now also preserves the signed authored `range/r` admission lane by applying the client's `abs(range)` normalization only for locked player and mob targets while keeping summoned-slot target checks on the raw signed radius branch, now resolves those player-targeted admission checks, multiball lane picks, and locked delayed impacts through the live player body rect seam instead of a coarse position-only fallback, now also narrows the WZ `jumpAttack` grounded gate to the same untargeted direct-rect player or summon overlap branch the client uses instead of incorrectly suppressing airborne hits from locked-target, projectile, or area impact paths, now parses WZ projectile `range/sp` + `range/r` launch-origin metadata, uses that authored origin for projectile spawn and type-1 admission checks, carries WZ `info/attack/*/attackCount` into live ranged attack entries, fans multi-ball projectiles across authored projectile lanes by choosing per-lane destinations against live player, summon, or encounter-mob bodies when those entities line up with the lane instead of cloning every extra ball off a fixed synthetic spread from the primary target, now synthesizes the extra multiball lane list from the full authored `range/start/areaCount` slot set instead of only the first `attackCount` offsets so wide projectile patterns stay centered and preserve authored lane order more closely, now keeps those authored lane points separate from the locked target body point so multiball projectiles continue to fly along the selected slot lane even when multiple balls resolve against the same live player, summon, or encounter-mob body, now also allows those per-lane heuristic resolutions to repeat the same live player, summon, or encounter-mob body across multiple bullets when separate authored lanes still line up with that body, reuses WZ-authored projectile and hit frame bounds for in-flight and arrival-time collision checks instead of a fixed synthetic `16x16` box, now mirrors the client `CMob::SetBallDestPoint` destination clamp for projectile travel by enforcing a forward lane, clamping the vertical slope to `0.6`, and normalizing against authored `range/r`, now reads WZ `attack/info/randDelayAttack` into delayed area attacks, now synthesizes the simulator-side random area delay list once per selected area slot so every foothold expanded from the same client-style `type = 3` column shares one queued delay instead of rolling a fresh delay per foothold hitbox, and now also keeps WZ `range/start` as the lane-origin offset while still selecting `attackCount` area slots from the full authored `range/areaCount` slot set (including non-zero or negative starts) so type-3 ground-column attacks no longer collapse to the first contiguous authored slots, and now also exposes a client-shaped override seam for `m_aMultiTargetForBall` and `m_aRandTimeforAreaAttack` so the attack scheduler will prefer packet-fed extra projectile lane points and per-slot area delays whenever those lists are supplied. In the packet path, the existing `MoveMob` packet codec and mob-attack inbox now preserve packet `TARGETINFO` too, so the next queued attack prefers packet-owned locked player, summon-slot, or mob victims through the same live body-rect seams the simulator uses for delayed type-1 admission and impact resolution instead of dropping that client-owned target branch and falling back to AI heuristics, packet-owned move-header attack admission now clears queued overrides when `NextAttackPossible` is false while carrying packet-facing into projectile lane fallback, launch-origin orientation, `SetBallDestPoint` forward-lane clamping, and locked-target admission-distance checks instead of always falling back to the live AI-facing bit, the same packet path now also honors client `bNotChangeAction` by refusing to queue simulator attack overrides for move headers that suppressed `CMob::DoAttack` on the client instead of leaking stale packet-owned locked targets or projectile lane lists into a later AI-selected attack, and the packet-owned move discard bit now feeds a live `MobMovementInfo` interrupt seam so attack move headers mirror the client `CMovePath::DiscardByInterrupt` split by clearing pending steering first and only forcing an airborne ground mob back onto a foothold when `bNotForceLandingWhenDiscard` is false; that same seam now also records a client-shaped last-motion snapshot (`x`, `y`, `vx`, `vy`, move/jump/action state, facing) at interrupt time and only zeroes horizontal speed when a foothold snap actually lands instead of wiping in-air lateral motion on discard requests that cannot resolve to a foothold. This run also extends that same discard seam with client-shaped buffered-path ownership: packet interrupts now truncate buffered move-path elements to a refreshed live-tail snapshot, rebase that tail to the packet receive tick, and preserve packet-owned move-action state (`stand`/`move`/`jump`/attack) through live movement state updates instead of treating move-action ownership as transient metadata only. This run closes another `MobBullet` timing branch straight from WZ plus client evidence: the simulator already loaded authored `range/r` and `attackAfter`, but ranged attacks now keep queued mob bullets dormant until `attackAfter` and time projectile expiry from the same client formula `range/r * 600 / nBulletSpeed` that `CMob::DoAttack` uses when it constructs `MobBullet`, instead of launching immediately and deriving impact time only from raw world-distance-over-speed, and in-flight projectile position now advances from the stored authored launch point on every tick so delayed transit and arrival checks no longer compound by re-lerping from the prior frame. This run also closes the WZ-authored arrival-hit presentation branch for projectile, direct, and area impacts: `info/hit` frames are now spawned as impact-side effects instead of only contributing collision bounds, and `hit/attach` metadata attaches those impact effects to the live locked player, summon, or mob target through the same body-rect seams used for delayed impact resolution, with non-locked hits still falling back to the resolved world impact position. This run also preserves WZ `attack*/info/hit/hitAfter` by deferring those impact-side `info/hit` effects, including attached player, summon, or mob hit effects, through the existing scheduler instead of spawning them immediately on impact. This run also closes the player-targeted projectile-arrival side of the locked-target branch: when a queued `MobBullet` reaches its scheduled destination, `MobAttackSystem` now passes the live `PlayerManager` into the same locked impact resolver used by direct and ground attacks, so packet-owned or AI-owned `MobTargetType.Player` projectile locks resolve through the live player body rect instead of silently falling back to an arrival overlap sweep. This run also tightens packet `TARGETINFO` ownership by splitting the raw field through the queued attack's WZ `type`: client area attacks (`type = 3` / `4`) feed it into the area-slot mask path, while non-area attacks keep it as the locked player, summon-slot, or mob target and no longer leak that encoded target id into the area-mask override lane. This run also fixes the live-facing side of that same `CMob::DoAttack` lane: simulator mob render `FlipX` is now converted back to client `bLeft` / source-facing semantics before direct rectangles, authored `range/sp` projectile origins, `SetBallDestPoint`, locked-target admission, type-3/4 area slot placement, source-anchored effects, and fallback range centers are computed, and delayed direct attacks preserve the queued source facing instead of recomputing from the mob's later live facing at impact time. | `CMob::DoAttack` / `ProcessAttack` are closer on attack-hit metadata, `MobBullet` setup, and delayed area placement now that the simulator carries WZ-authored attack knockback through the live entry and uses authored launch points, live player-body admission and impact resolution, full-slot multiball lane selection, authored-order extra bullet synthesis, authored lane-point preservation separate from delayed locked-target victim resolution, client-style direct-only `jumpAttack` grounding, WZ-sized projectile-impact bounds, client-style `SetBallDestPoint` destination normalization, client-style `attackAfter` projectile arming, authored `range/r * 600 / nBulletSpeed` bullet lifetime, fixed launch-point in-flight interpolation, authored projectile-arrival player lock resolution through the live player manager, authored `randDelayAttack`, per-slot random-delay reuse across type-3 multi-foothold expansions, client-style type-3 multi-foothold column expansion, type-4 source-Y placement, packet `TARGETINFO` disambiguation by WZ attack `type`, live `FlipX` to client source-facing conversion for AI-owned direct/projectile/area lanes, queued source-facing preservation for delayed direct rectangles, heuristic duplicate-target multiball resolution when multiple authored lanes legitimately overlap the same body, WZ `hit/attach` plus `hitAfter` impact-presentation ownership, and packet-owned `TARGETINFO`, `m_aMultiTargetForBall`, `m_aRandTimeforAreaAttack`, `NextAttackPossible`, `bNotChangeAction`, and `bNotForceLandingWhenDiscard` ingestion through the live `MoveMob` inbox path instead of only through manual override seams, and that same packet path now also decodes buffered `CMovePath` elements from the move payload and replays them through `MobMovementInfo` with receive-time rebasing plus per-element move-action ownership so packet-owned position/action transitions no longer collapse to the interrupt snapshot only, now preserves branch-specific packet attack actions (`attack1` through `attack9`) for move-actions `13..21` instead of folding buffered replay state back to `attack1`, while also decoding optional client flush-tail payload ownership (`passive key pad` count plus packet movement bounds) and feeding those tail fields into live `MobMovementInfo` whenever they are present. The remaining gap is narrower but still centered on deeper client-owned `MobBullet` and move-path behavior: the runtime still does not mirror richer non-linear projectile travel shapes or uncommon post-arrival side effects beyond the now-matched source-facing conversion, launch window, fixed-point interpolation, destination clamp, authored lifetime formula, locked player/summon/mob arrival resolution, and WZ `hit/attach` / `hitAfter` arrival-effect branch, and while the packet-owned move discard seam now covers steering clear, interrupt snapshot capture, buffered-element truncation, receive-time rebasing, downstream move-action ownership, packet `TARGETINFO` target-vs-area-mask disambiguation, and flush-tail passive-state/bounds ownership in `MobMovementInfo`, the remaining move-path gap is now limited to deeper client stream nuances: uncommon `CMovePath` attribute payload variants beyond the now-modeled common element stream, plus receive-time progression replay edge behavior that may differ from the current linear buffered-element interpolation. Focused simulator seam coverage now exists in `UnitTest_MapSimulator/MobAttackVitalSideEffectParityTests.cs` for authored attack-knockback gating, `UnitTest_MapSimulator/MobAttackSystemClientTickParityTests.cs` for live render-flip to client source-facing conversion and packet-facing override precedence, `UnitTest_MapSimulator/MobAttackSystemProjectileParityTests.cs` for WZ hit-attach and hitAfter impact-effect metadata plus player-targeted projectile locked-impact manager preservation, in `UnitTest_MapSimulator/MobProjectileTimingParityTests.cs` for the authored `range/r` bullet lifetime formula and launch-point linear travel interpolation, plus `UnitTest_MapSimulator/MobPacketMoveActionParityTests.cs` for packet-owned `TARGETINFO` area-mask versus locked-target splitting by attack type, packet-owned discard truncation, receive-time rebasing, move-action ownership mapping, buffered move-path replay progression through `MobMovementInfo`, packet attack move-action `13..21` branch preservation, `MoveMob` move-tail decode coverage with optional client flush-tail passive-state/bounds payloads, and live `MobMovementInfo` flush-tail preservation, and `UnitTest_MapSimulator/MobAttackGroundSlotParityTests.cs` for non-zero WZ `range/start` area-slot selection across the full authored `range/areaCount` slot set. | `MobAI.cs`, `MobAttackSystem.cs`, `MobPool.cs`, `MapSimulator.cs`, `MobMoveAttackPacketCodec.cs`, `MobMovementInfo.cs`, `MobItem.cs`, `UnitTest_MapSimulator/MobAttackSystemProjectileParityTests.cs`, `UnitTest_MapSimulator/MobProjectileTimingParityTests.cs`, `UnitTest_MapSimulator/MobPacketMoveActionParityTests.cs`, `UnitTest_MapSimulator/MobAttackVitalSideEffectParityTests.cs`, `UnitTest_MapSimulator/MobAttackSystemClientTickParityTests.cs` (`CMob::DoAttack`, `CMob::OnMove`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`, `CMovePath::DiscardByInterrupt`, `attack/info/range/r`, `attack/info/hit/hitAfter`) |
| Partial | Full mob stat/status parity | Existing mob-temp-stat parity work remains in place (runtime status entries with expiry/tick metadata, signed stat shifts, DoT ticks, freeze/stun lock, immunity/counter lanes, threat/showdown/mind-control/polymorph inference, mob-skill status clear/self-buff lanes, and doom render/collision swap to `mob/0100101.img`). In this run, the doom movement seam now reserves transformed speed to the WZ-backed doom baseline from `mob/0100101.img/info/speed = -50` instead of a hardcoded synthetic slow, the hypnosis target resolver now excludes doomed candidates and explicitly prioritizes same-team encounter targets ahead of same-team non-encounter fallback candidates, and it now also preserves an already-resolved hypnosis victim within the same priority tier instead of thrashing to a merely closer alternative every sync tick, which better matches the client-shaped locked-target feel until a higher-priority candidate appears. This run also tightens that same seam with bounded distance retention: an already-resolved hypnosis victim remains eligible through a 1.5x range window so same-tier locks do not churn immediately on slight spacing seams, while higher-priority candidates still preempt immediately. `MobSkill.img/157` (`info` rich token, `x=1`, `time=180`) now also contributes a concrete reward-side consequence by scaling meso payout through the existing mob reward lane (`x` maps to +`x*100%` meso bonus while active) instead of remaining visual/status-only, inferred `Showdown` reward bonus parity now also reaches the simulator's item reward seam instead of stopping at EXP because the official skill text in `string/Skill.img` (`4121003` / `4221003`) says Showdown increases both EXP and items, authored fear now also reaches the client-shaped field-visual seam instead of staying control-only through the same `CField::InitFearEffect` / `OnFearEffect` / `OffFearEffect`-style seam with live `MobSkill.img/137/x` intensity, and the item reward seam itself now goes beyond monster cards by reading authored `String/MonsterBook.img/<mobId>/reward` entries: mob deaths can spawn one lightweight WZ-backed reward-list item, with that authored reward choice now selected deterministically from the published WZ reward order per runtime mob identity seed (`PoolId`, with template-id fallback) instead of rerolling a different entry on each local death, while Showdown quantity scaling only duplicates stackable rewards and leaves equip-like drops singular. This run widens that same MonsterBook reward seam from one selected reward-list item to a bounded deterministic two-slot selection over the published reward order, still preserving authored zero-valued slots as legitimate no-drop positions before selecting later nonzero authored entries. The MonsterBook reward parser now also preserves authored zero-valued reward slots from `String/MonsterBook.img/<mobId>/reward` instead of filtering them out, so deterministic reward selection can legitimately resolve to no extra authored reward item when the published list includes empty slots. WZ-authored summon skill `MobSkill.img/105` (`level/1/0`, `limit`, `lt`/`rb`) now also routes through the existing summon runtime seam instead of falling through the generic status lane, so boss-side summon branches that publish the older `105` id no longer silently no-op while `200` works. `MobSkill.img/138` (`hp=45`, `interval=90`, `time=60`, `count=30`) now also routes through the existing player-targeted status seam as runtime `Burn` periodic damage instead of remaining visual/status-only when casts land inside the authored area. This run also brings the higher-level `MobSkill.img/132` reverse-input branch that publishes `HP` plus `interval`/`count` metadata into the same runtime periodic-damage entry instead of discarding those authored fields after applying input reversal only. This run also brings `MobSkill.img/134` (`prop=80`, `time=10`, `interval=48`, `count=8`, broad authored `lt`/`rb`) into the same abnormal-status resistance lane as the other hostile player-facing mob statuses before it applies runtime `PainMark` periodic damage, instead of letting that rarer damage-over-time branch bypass resistance ownership. The uncommon boss-control seam was tightened further using the same status/skill-blocking owner path: player skill-block mirroring for `MobSkill.img/170` now resolves duration through the runtime status-duration resolver (including WZ-authored `x`-seconds fallback when `time` is omitted), and `MobSkill.img/171` now routes through the same abnormal-status resistance lane as other hostile player statuses instead of bypassing resistance checks, while polymorph pair `MobSkill.img/172`/`173` now also follows that same resistance ownership instead of bypassing resist checks. Packet-owned `MoveMob` `TARGETINFO` mob locks now also feed the hypnosis target seam as preferred victims within the existing same-priority tier resolver path, so the client-owned lock target can preempt distance-only local swaps while still yielding to higher-priority encounter/team tiers and existing eligibility or range gates. Mob-side status/heal/clear targeting now also keeps encounter-team ownership in the same area-resolution seam: nearby mob-status skills no longer leak across opposing `life.team` lanes, while teamless/default maps keep the previous broad area admission. This run also tightens packet/field-owned mob affected-area status application: when an `AffectedAreaSourceKind.MobSkill` area lands a runtime mob status on the local player, the same post-apply seam now plays the authored affected-hit feedback and mirrors skill-blocking statuses through `PlayerSkillBlockingStatusMapper`/`TryApplyMobSkillBlockingStatus`, so lingering mob-skill fields no longer apply seal/stun/freeze/seduce/polymorph/stop-motion runtime state without the matching client-shaped skill rejection lane. WZ-authored player-skill blind debuffs now also preserve their authored status magnitude from skill-level metadata (for example `Skill/5112.img/skill/51121007/common/x`) on the existing inferred mob-temp-stat entry instead of storing a synthetic zero-valued `Blind` marker, and WZ-authored slow debuffs such as `Skill/210.img/skill/2101003/common/x = -8*x` now apply a signed `Speed` temp stat instead of being collapsed into the immobilizing `Web` lane. This run also tightens the mob-side reflect counter seam: WZ `MobSkill.img/143`/`144`/`145` publish `x`/`y` as physical or magical reflect caps while `hp` is the low-HP cast gate, so reflect status value now stays at full-strength `100` instead of incorrectly treating the authored HP gate as a reduced reflect percent. This run also carries source-level ownership through the mob-temp-stat entries themselves: player-skill inferred mob debuffs now retain both the authored player skill id and skill level on the runtime status entry, and WZ mob-skill self/nearby statuses now retain their `MobSkill.img` id plus cast level when applying the same entry, preserving more of the client-shaped `nOption/rOption/tOption` payload needed by packet-owned status mirrors and follow-up visuals. Direct player-targeted mob-skill statuses now also preserve the source mob pool id in the same local player status source-context lane, and mob-skill affected-area applications carry both area owner id and area object id, so WZ `MobSkill.img` applications keep owner identity instead of collapsing to skill id/level only. This run also tightens the mirrored player skill-blocking lane that those statuses drive: `PlayerCharacter` now expires seal/stun/freeze/seduce/polymorph/stop-motion skill-block mirrors with the same unsigned client tick arithmetic as `PlayerMobStatusController`, and same-status refreshes preserve the later unsigned deadline instead of shortening or expiring early across `Environment.TickCount` rollover. This run extends that unsigned refresh ownership into the mob/player runtime temp-stat entries themselves: changed same-status refreshes now keep the later unsigned deadline while still updating the live value and source payload, so packet-shaped mob temp stats and local player mob statuses do not expire early when a shorter refresh lands near tick rollover. Focused `UnitTest_MapSimulator` coverage now exists in `MobStatusParitySeamTests` for direct mob-skill player-status source-owner preservation plus mob-skill affected-area owner/object context preservation, player-skill blind magnitude plus source-level preservation, mob-skill source-level temp-stat preservation, changed same-status player/mob temp-stat unsigned refresh retention, player-skill slow signed-speed admission, remote mob affected-area blocking mirror admission, stable authored reward-list selection (including identity-seed determinism and bounded multi-slot order selection through authored zero slots), hypnosis target-retention tie-breaks plus bounded distance-retention assertions, packet-owned hypnosis target preference and packet-lock expiry assertions, `132` resistance-lane plus authored periodic-damage metadata assertions, `134` resistance-lane plus periodic-state assertions, `170` duration-fallback assertions, WZ reflect cap-vs-HP-gate ownership assertions, mirrored skill-blocking unsigned expiry/refresh assertions, and `171`/`172`/`173` resistance-lane assertions, alongside the earlier reward quantity coverage, summon-id routing assertions, `138` player-status admission/resistance assertions, and encounter-team mob-status target compatibility assertions. | Status-heavy encounters now track more client-shaped consequences beyond status visuals: doom movement speed reservation is WZ-backed, hypnosis retargeting has clearer target exclusion plus same-team Carnival-like priority order with stable same-tier victim retention plus bounded out-of-range lock retention for already-resolved victims, and now also respects packet-owned lock intent through same-tier preferred target admission from `TARGETINFO` instead of distance-only local churn when that packet lock is valid, `157` is no longer a no-op for rewards, showdown-side reward parity now affects EXP plus both monster-card and lightweight authored reward-list item drops, fear now applies both its runtime status lane and the matching field-darkening presentation instead of stopping at control-state effects, `138` now applies runtime burn-state damage through the same mob-skill player-status lane, higher-level `132` now preserves authored `HP`/`interval`/`count` periodic metadata while keeping reverse-input control active, and uncommon status branches now keep `134` PainMark resistance plus `170` lock timing and `171`/`172`/`173` resistance ownership aligned with the shared runtime status lane instead of synthetic side paths. Nearby mob-side status/heal/cure skills now also stop crossing opposing encounter teams in shared fields, so Carnival-like team ownership is preserved during area buff/cleanse casts. Mob-side reflect now also keeps the WZ-authored `143`/`144`/`145` separation between HP-gated cast admission and `x`/`y` reflected-damage caps instead of weakening the counter by using the HP gate as the reflect percent. Mob temp-stat entries now also preserve source skill level next to source skill id, and direct player-targeted mob-skill statuses preserve source mob pool id next to the source skill id/level, and mob-skill affected-area applications preserve owner id plus area object id, narrowing packet-shaped status ownership for player-skill inferred mob debuffs, WZ mob-skill status casts, immediate local player status applications, and lingering field-owned mob-skill statuses. Remaining gap is narrower but still real: the simulator still does not model full server-authored mob drop tables, per-item drop rates, or full server-authored multi-roll reward emission beyond this bounded `String/MonsterBook.img`-backed two-slot approximation, whether `157` has additional non-reward consequences, deeper doom control/branch behavior outside the speed reservation seam, deeper client-owned post-apply semantics for uncommon boss-only branches beyond the now-shared `132/134/170/171/172/173` status admission timing and resistance ownership, and deeper packet-priority hypnosis retarget semantics beyond the now-modeled same-tier preferred lock-target path still need client-side matching. Packet/field-owned affected-area status post-apply ownership is now covered for hit feedback and skill-block mirroring, WZ-authored player-skill blind metadata now survives into the mob temp-stat entry, player-skill slow now uses signed speed instead of web immobilization, source skill id/level now survive on mob temp-stat entries, direct player-targeted mob statuses now retain source mob pool id, mob-skill affected-area statuses now retain owner/object context, and mirrored skill-blocking statuses now keep unsigned client-tick expiry and refresh timing, changed same-status mob/player temp-stat refreshes now preserve the later unsigned deadline while updating payload values, but broader server-authored affected-area lifecycle, packet ordering, and byte-accurate official mob-stat packet payload ownership remain outside this row's current local runtime approximation. | `MobAI.cs`, `SkillManager.cs`, `HypnotizeTargetResolver.cs`, `MobStatusRewardParity.cs`, `MobSkillStatusTargetParity.cs`, `MonsterBookManager.cs`, `PlayerManager.cs`, `PlayerMobStatusController.cs`, `MapSimulator.cs`, `MobAttackSystem.cs`, `UnitTest_MapSimulator` |
| Partial | Special mob interactions | WZ `selfDestruction` mobs trigger reserved bomb or time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, and generic WZ `info/removeAfter` encounter mobs expire through the simulator `Timeout` death lane instead of living forever. Escort and WZ `info/damagedByMob` encounter mobs no longer aggro the player, this now flows through parsed mob-data flags instead of only the older simulator `Friendly` alias, escort, `damagedByMob`, and generic `removeAfter` encounter spawn points no longer auto-respawn through the normal mob-pool loop, and special-death, escort, `damagedByMob`, swallow, and timeout encounter kills suppress reward drops. WZ `info/damagedByMob` encounter mobs ignore player-originated basic, skill, projectile, and summon damage, no longer enter the ordinary delayed mob HP-bar lane when mob-vs-mob damage lands, hostile mobs retarget live escort or `damagedByMob` encounter actors onto `MobTargetType.Mob`, that lane treats only live encounter participants as valid mob-vs-mob damage recipients so delayed area or projectile splash from encounter combat no longer leaks onto unrelated ordinary mobs, and encounter retargeting honors authored `life.team` separation by rejecting explicit same-team actors and preferring opposing-team targets before teamless fallbacks. Escort follow respects live `life.info` progression by attaching only the lowest active escort index until that stage clears. Wild Hunter swallow routing mirrors client-backed digest cadence more closely by holding `33101005` targets in repeated `500 ms` wriggle suspension pulses across the `3000 ms` digest window before resolving the `33101006` follow-up buff. Unresolved absorb outcomes expire through a bounded client-style fallback window derived from visible swallow action timing plus wriggle cadence, with `skill/effect` only as a fallback when no action animation is available (WZ revalidated: `Skill/3310.img/skill/33101006/effect/*/delay=120`; `33101005/info/type=98`). Early absorb outcomes buffer briefly through the same swallow seam so packet-owned success or failure results that arrive before pending absorb state is armed still resolve onto the correct target. This run realigns swallow buffer ownership in runtime and tests to the explicit armed-vs-unarmed seam: absorb outcomes are now buffered only while no swallow state is armed (including no pending-absorb buffering once armed), and the short-lifetime buffer keeps only the newest result for each skill-target pair by replacing prior buffered entries for that same pair before capacity eviction, while still evicting the oldest unmatched pair first at capacity so newer authoritative outcomes win inside the parity window. Astaroth-style rage bosses parse WZ `ChargeCount` plus `AngerGauge` data, reserve `attack/info/AngerAttack` entries until charge fills, consume that gauge on the flagged attack, and render the mob-local anger gauge plus burst effect from dedicated WZ animation nodes. Automated seam coverage now reflects active tests in source: `MapleLib.Tests/MobDataSpecialInteractionParseTests.cs` covers `info/damagedByMob`, string-or-int `info/removeAfter`, `info/selfDestruction`, `info/ChargeCount`, and `info/AngerGauge`; `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeParityTests.cs` and `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeBufferParityTests.cs` cover swallow absorb-outcome gating, armed-state buffer rejection, newest-result replacement ownership for repeated skill-target pairs (including full-capacity matched-pair replacement without evicting unrelated buffered pairs), bounded buffering, and expiry pruning; `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs` covers anger-gauge burst cadence ownership; and `UnitTest_MapSimulator/SpecialMobInteractionEncounterParityTests.cs` now guards encounter respawn/drop suppression rules, WZ seconds-vs-milliseconds `removeAfter` normalization, generic timeout and self-destruction bomb death lanes, player-target aggro rejection for encounter mobs, encounter participant admission gating, `life.team` priority rejection/fallback ordering, and regular HP-bar suppression for WZ `info/damagedByMob` encounter actors. | The remaining gap is mostly script-, packet-, or end-to-end-owned rather than generic WZ seams: broader scripted rage or boss-phase logic beyond the Astaroth-style charge lane, authoritative packet choreography outside the now-tightened local swallow absorb-result ownership seam (for example full server-driven sequencing around follow-up skill ownership, server packet ordering, and field state), deeper map-specific escort scripting plus executable lowest-live-index escort progression coverage, the dedicated `bDamagedByMob` grey blink / adjusted HP-indicator presentation lane beyond ordinary HP-bar suppression, and broader field-script encounter choreography beyond the current WZ-backed retarget, timeout, escort progression, swallow seam, damaged-by-mob HP-bar suppression, and rage-gauge seams are still missing. | `MobItem.cs`, `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `SkillManager.cs`, `WildHunterSwallowAbsorbOutcomeBuffer.cs`, `MapSimulator.cs`, `CombatEffects.cs`, field systems, `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeParityTests.cs`, `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeBufferParityTests.cs`, `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs`, `UnitTest_MapSimulator/SpecialMobInteractionEncounterParityTests.cs` |



### 2. Mob image-entry ownership discovered by targeted `CActionMan` scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Mob image-entry / linked-template parity | `LifeLoader` now has a distinct `GetMobImgEntry`-style owner ahead of mob action and attack asset loading. It caches normalized mob image entries by template id, formats template ids to the Mob IMG resource name, follows `info/link` recursively, preserves the current entry's `info` branch, and exposes missing top-level branches from linked templates to the action loaders without mutating the underlying WZ tree. | This keeps the client ownership split explicit: mob runtime state, normalized image-entry resolution, and frame/action construction are separate seams, so linked-template inheritance is no longer hidden inside the broader action-loader or runtime rows. | mob template/image-entry layer (`CActionMan::GetMobImgEntry`, `LifeLoader.cs`, `MobAnimationSet.cs`, `MobItem.cs`) |

### 3. Mob action-loader ownership discovered by targeted `CActionMan` scan

| Status  | Area                                | Gap | Why it matters | Primary seam |
|---------|-------------------------------------|-----|----------------|--------------|
| Partial | Mob action-loader / frame-build parity | The simulator keeps the explicit `CActionMan::LoadMobAction` seam and now carries a broader explicit client action-slot owner path in `LifeLoader` instead of only keyed-by-authored-name loading. The simulator slot surface is expanded to the client-owned 43-slot contract recovered from IDA (`CMob::CMob` allocates `0x2B`), and `s_sMobAction` handling materializes a sparse canonical table with deterministic slot order over that full range rather than a dense 30-slot core-only table. Canonical resolution explicitly covers client-owned buckets used by `CUIMonsterBook::LoadMobAction` (`attack1..9` in slots `13..21`, `skill1..17` in slots `22..38`) plus recovered non-attack slots (`stand`/`move`/`fly`/`hit1`/`die1`/`regen`, `bomb`, `hit2`, `die2`, `dieF`, and slot `39` ownership canonicalized to `chase` with legacy alias `moveaction16` for `CMob::MoveAction2RawAction` case `16`). Follow-up IDA recovery on `_dynamic_initializer_for__s_sMobAction__` also resolves former unknown slot names through the same seam (`slot 7 -> hit1`, `slot 8 -> hit2`, `slot 11 -> die2`, `slot 40 -> rollingSpin`, `slot 41 -> siege_pre`, `slot 42 -> tornadoDashStop`), with duplicate-name slots retained as explicit owner slots while authored-name-to-slot resolution keeps existing primary canonical slots (`hit1 -> 3`, `hit2 -> 9`, `die2 -> 10`) to avoid drift in established runtime fallbacks. Mapped slot actions are cached by slot and then materialized before authored-name fallbacks are applied. WZ-first follow-up confirmed non-indexed authored mob roots (`hit`, `die`) still appear on some templates (for example `Mob/9300391.img` and `Mob/9400584.img`), and the loader canonicalizes those roots through the same slot-owner seam (`hit -> slot 3 -> hit1`, `die -> slot 4 -> die1`); authored `chase` and `dieF` roots in v95 mob data route through the slot-owner seam (`slot 39`, `slot 12`) rather than fallback-only authored-name loading. Existing per-frame parity covers the broader recovered frame-owned payload on this same seam: `delay`, `head`, `lt`, `rb`, `multiRect`, frame-owned `a0`/`a1` alpha ramps and `z` lane metadata, origin-backed visual bounds/frame metadata into `MobAnimationSet`, reverse-pass duplication for client replay flags (`zigzag` and `reverse`), runtime per-frame alpha interpolation in `MobItem`, and lane-aware body-rectangle collision consumers in `MobItem`/combat targeting. The native frame-entry construction side follows the recovered direct-canvas/UOL cache boundary: WZ sampling shows ordinary mob action frame rows as direct canvas children (for example `Mob/9300391.img/hit/0`, `Mob/9400633.img/attack4/*`, and `Mob/8840000.img/*/multiRect` under direct frame canvases), and IDA for `CActionMan::LoadMobAction` confirms each enumerated action child is queried as an `IWzCanvas`; the loader ignores nested numeric subproperty canvases for primary frame and metadata enumeration instead of recursively materializing phantom frames outside that contract, including the single-child nested numeric case. Rare action-level face branches in v95 data (`Mob/8860003.img` and `Mob/9300306.img` expose `stand`/`hit1`/`die1` `face/face` canvases with local `map/brow` metadata) remain nonnumeric authored subproperty branches and are no longer leaked into the action-frame table. WZ sampling also confirmed action-level `speak` branches under client-owned actions (for example `Mob/2220000.img/skill1/speak`, `Mob/3220000.img/attack1/speak`, `Mob/8300006.img/regen/speak`, and `Mob/8830000.img/attack4/speak`) with `prob`, optional chat-balloon metadata, HP gates, and ordered numeric message strings; the loader keeps that payload on the resolved action cache entry through `MobAnimationSet.ActionSpeakMetadata` while still treating `speak` as non-frame action metadata rather than a canvas row. WZ follow-up on `speak/con` found v95 condition rows on mob `info/speak` owners (for example `Mob/2220000.img/info/speak/con`, `Mob/9300456.img/info/speak/con`, and `Mob/9700035.img/info/speak/con`) using ordered alternative groups with `quest/questID` plus `state` gates and `pet` item-id gates; the same `ActionSpeakMetadata` seam now carries those condition groups for parent or variant `speak/con` nodes so action-level gated rows can reuse the parser without being flattened into ordinary message rows. Runtime `MobItem` presentation now receives a live action-speech condition context from the existing MapSimulator mob creation and respawn seams, so gated action speech can evaluate simulator quest state through `_questRuntime.GetCurrentState` and active pet item or pet-wear ids through `_playerManager.Pets.ActivePets` instead of being limited to parser-only tests. It also rolls probability and message selection when an action starts, treats small authored HP gates (`1..100`) as max-HP percentages while preserving larger raw thresholds, orders HP-gated `speak` variants by the most specific eligible HP phase before rolling probability so lower-health phase rows are reachable instead of being shadowed by earlier broader thresholds, keeps the authored chat-balloon id on the active message, resolves `UI/ChatBalloon.img/mob/<id>` skins through the existing UI texture cache, draws authored mob balloon nine-slice/arrow assets when available, carries the authored skin `screenChat` flag and text color on the loaded skin instead of hardcoding screen-chat placement/text fitting to id `1`, falls back to `mob/0` for missing authored balloon ids, still uses the managed rectangle fallback for missing skins or `floatNotice`, renders owner-anchored versus screen-chat placement through the existing map draw pass, and allows completed same-name one-shot attack/skill/hit/death actions to replay so later occurrences can reroll action speech instead of being suppressed by the same-action guard. This pass tightens the recovered `CChatBalloon` presentation seam: WZ evidence pins `UI/ChatBalloon.img/mob` to four mob skins (`0..3`), `mob/1` as authored `screenChat=1`, and authored text colors/arrows, while IDA evidence for `CChatBalloon::MakeScreenBalloon` at `0x4a94f0` pins the screen-chat composition as balloon type `1005`, `CreateLayer` option `0xC00616FC`, horizontal placement at native width `800` via `(800 - canvasWidth) / 2`, and vertical placement at native center line `100 - canvasHeight / 2`; owner-anchored mob speech stays on `CChatBalloon::MakeBalloon` type `1004`. The managed bounds now use that recovered screen-center rule instead of a fixed top offset, and the native composition trace now records owner-overlay versus screen-layer path, skin path, arrow inclusion, mob-skin fallback ownership, `CreateCanvas(type=1005)`, `CreateLayer(option=0xC00616FC)`, recovered screen-layer X/Y origin math, `Getcanvas(0)`, `InsertCanvas(0,0,alpha=255)`, layer priority `-1`, `m_pLayerChat` assignment, and the recovered AddRef/Release ordering visible in `CChatBalloon::MakeScreenBalloon`. This pass also adds a focused `LifeLoader` action-cache trace for tests so the canonical seam exposes normalized template id, authored action, resolved client slot, canonical action name, cache key, direct-canvas frame count, frame metadata count, action-level `speak` ownership, parsed parent and variant `speak/con` condition-group count, and reverse replay ownership without changing runtime materialization. Attack-support metadata and effects continue to key through the same resolved action-name seam so mapped slot actions stay consistent across frame, hit, projectile, warning, and extra-effect consumers, and focused seam coverage in `UnitTest_MapSimulator/MobActionLoaderParityTests.cs` now also covers direct-canvas-only frame enumeration including the single nested numeric no-frame case, nonnumeric action-level face-branch exclusion, action-level `speak` metadata extraction without phantom-frame materialization, HP-phased `speak` variant materialization, action-speech probability/message/raw-or-percent HP selection helpers, most-specific eligible HP-phase variant selection, `speak/con` quest-or-pet alternative group parsing and condition-gated variant selection helpers, parent-plus-variant condition-group cache tracing, screen-chat versus owner-anchored balloon bounds and text-fit width from authored skin ownership, recovered `MakeScreenBalloon` center-X/center-Y placement, composition constants, screen-layer insert/lifetime ordering, WZ mob chat-balloon skin path and authored `screenChat` flag ownership, completed one-shot same-action replay gating for speech rerolls, non-indexed `hit`/`die` canonicalization, `bomb`/`dieF`/`chase` slot-name resolution (including `moveaction16` alias retention), canonical action-cache key tracing for slot-owned and authored fallback actions, frame-owned `a0`/`a1`/`z` metadata extraction, and action-level `zigzag` replay-flag handling in addition to the expanded slot contract (43-slot surface, canonical name resolution, recovered attack/skill buckets, and unknown-action fallback). Remaining gap is narrower but still `Partial`: slot-name ownership for the 43-slot `s_sMobAction` surface is recovered, primary frame-entry construction follows the recovered direct-canvas/UOL cache boundary, rare nonnumeric face branches are no longer leaked into the frame table, action-level `speak` payloads now have managed runtime presentation with authored skin screen-chat/text-color ownership, percentage/raw HP gating, most-specific HP-phase variant selection, parsed quest/pet `speak/con` condition groups, live simulator quest-state and active-pet item/wear gating, same-action replay selection, recovered screen-chat center placement, explicit cache-key trace coverage, and a recovered native AddRef/Release/insert-order trace on the canonical action seam, but real COM `CChatBalloon` layer/canvas objects, byte-identical native blend/composite output, text fitting beyond the managed WZ mob nine-slice/wrap approximation, byte-proven client quest/pet context sources beyond the simulator quest runtime and active-pet item/wear ids, and broader proof across every authored action-speak branch family are not yet fully proven. | Naming and partially implementing the `CActionMan` owner keeps mob animation parity split the same way as summons, NPCs, pets, and employees: runtime `CMob::*` logic can keep evolving independently while loader-owned frame table construction, cached reuse, authored rect math, per-lane body geometry, reverse playback, and action-speech balloon presentation now have a concrete simulator seam instead of staying implicit inside generic mob loading. | `LifeLoader.cs`, `MobAnimationSet.cs`, `MobItem.cs`, `MobAttackSystem.cs`, `MapSimulator.MobActionSpeech.cs`, `UnitTest_MapSimulator/MobActionLoaderParityTests.cs` (`CActionMan::LoadMobAction`, `CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`, `CChatBalloon`) |





### 4. Combat feedback animation-owner parity discovered by `CAnimationDisplayer` scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Damage-number and tremble animation-owner parity | The simulator keeps combat feedback on the explicit `CAnimationDisplayer` seam instead of collapsing it into generic mob-runtime or HUD code. `CAnimationDisplayer::Effect_HP` owns red/blue/violet number-family selection, StringPool id `0x1A15` formatting, digit composition, temporary `CreateCanvas(lWidth, 57)` composition, critical-banner handling, and one-time layer registration, while `CAnimationDisplayer::Effect_Tremble` owns the config/enforcement gate, start/end ticks, heavy-vs-normal duration (`1500` vs `2000`), and reduction factors (`1.0`, `0.85`, `0.92`). WZ evidence remains anchored to `effect/BasicEff.img/NoCri0`, `NoCri1`, `NoCri1/effect` (`62x57`, origin `41,70`), and authored `NoRed0` special text (`Miss`, `guard`, `shot`, `counter`, `resist`), with `NoRed0/shot` still `137x65`, origin `54,38`, `NoBlue0` still digit-only, and `NoViolet0` still lacking the authored `shot` owner. IDA evidence remains `CAnimationDisplayer::Effect_HP` at `0x444eb0` (unsupported-color early return for non-`0/1/2`, red/blue/violet property-owner selection, red-only critical owner split, first digit from the large owner and later digits from the small owner, critical pre-spacing seed `0x1E`, `lCenterTop - 47`, `CreateLayer` canvas `0`, option `0xC0050004`, priority `-1`, two primary `InsertCanvas` phases `400ms` hold then `600ms` fade, separate critical layer write at `lY - 30`, AddRef/RegisterOneTimeAnimation/Release lifetime, and temporary-canvas release after registration) and `CAnimationDisplayer::Effect_Tremble` at `0x439a70`. Managed ownership keeps canonical special-result normalization, routes all special-text families back to authored `NoRed0`, canonicalizes unresolved names to `Miss`, mirrors unsupported-color rejection on color parsing and effect-UOL resolution, admits special-result sprites only from authored `NoRed0`, keeps the explicit combat-feedback frame-cache key from the owner UOL seam, preserves red-only critical presentation, large-first/small-tail digit composition, separate critical banner, `30px` critical-leading spacing, `400ms`/`600ms`/`30px`/`250ms` timing, composite-canvas release on eviction/natural completion, violet owner routing for party/summon and received damage, and `Effect_Tremble` additional-time no-reduction parity for positive and negative non-zero `tAddEffectTime`. This run narrows the native-composite boundary further: the recovered `OneTimeCanvasLayerAnimation` owner trace now records the native temporary-canvas factory StringPool id `0x03D0` (`Canvas`) alongside the managed substitute (`RenderTarget2D`, `SurfaceFormat.Color`, transparent clear, `AlphaBlend`, `PointClamp`), explicitly marks the managed composite as non-byte-identical, and carries a recovered `Effect_HP` owner-selection trace for red/blue/violet large/small owners, red critical `NoCri1`/`NoCri0`, and authored `NoRed0` special-text ownership. Focused seam coverage in `UnitTest_MapSimulator/CombatFeedbackAnimationOwnerParityTests.cs` pins the owner-lane canonicalization, unsupported-token rejection, special-text ownership/no-owner no-op, critical spacing and critical-layer constants, temporary-canvas operation ordering and release after registration, distinct simulated temporary-canvas/layer handles, final manager release, violet owner routing, managed composite-surface contract, recovered large/small color-family owner selection, red critical owner split, authored `NoRed0` special-text ownership, and tremble gate/duration/reduction behavior. Remaining gap stays `Partial` and is now limited to the native Gr2D execution path: the simulator still does not instantiate real COM `IWzCanvas`/`IWzGr2DLayer` objects and still cannot reproduce byte-identical native blend/composite output, but the owner-faithful managed approximation now records the native canvas factory id, managed surface substitute, object roles, color-family ownership, large-first/small-tail owner selection, lifetime ordering, and timing explicitly. | Without an explicit `CAnimationDisplayer` combat-feedback row, combat parity can look further along than it is once mob AI, hit resolution, HP bars, and generic damage text exist. Naming the owner keeps future work anchored to the client seam that actually builds and schedules damage-number and tremble presentation instead of burying remaining visual differences inside unrelated mob-runtime rows. | combat feedback animation layer (`CAnimationDisplayer::Effect_HP`, `CAnimationDisplayer::Effect_Tremble`, simulator seams: `CombatEffects.cs`, `DamageNumberRenderer.cs`, `ScreenEffects.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `AnimationEffects.cs`, `UnitTest_MapSimulator/CombatFeedbackAnimationOwnerParityTests.cs`) |

### 5. Mob anger-gauge burst animation-owner discovered by follow-up IDA scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Mob anger-gauge burst animation-owner parity | The simulator keeps the visible full-charge burst on the recovered mob-to-animation owner seam instead of inline mob rendering. WZ evidence remains anchored to `Mob/9400633.img` (`AngerGaugeEffect` has 8 frames at 150 ms with recovered width/height/origin metadata, `info/ChargeCount = 3`, `info/AngerGauge = 1`, `attack4/info/specialAttack = 1`, `attackAfter = 3300`). Client evidence remains `CMob::Update` -> `CMob::AngerGaugeFullChargeEffect` (`0x6490B0`) -> `CAnimationDisplayer::Effect_FullChargedAngerGauge` (`0x457D00`): timed replay gate `m_bFullChargeEffectStartTime + m_bFullChargeEffectTime`, caller-side start-time refresh before StringPool UOL construction and before the displayer call, slash separator append between StringPool ids `0x03CE` / `0x0C2F`, head-origin and action-layer overlay-parent handoff, `LoadLayer` (`canvas 0`, `rx/ry = 0`, option `0xC00614A4`, alpha `255`, `bFlip = 0`, reserved `0`), `GA_STOP`, then `RegisterOneTimeAnimation(delay 0)`. Managed parity now keeps owner-lane trigger/cadence split and routes the owner trigger through authored special-attack `attackAfter` instead of the generic attack damage/effect trigger helper: timed special attacks drive owner replay gating, untimed/missing-owner timing stays on authored-frame fallback cadence, stale timed intervals are cleared immediately when later special attacks omit authored `attackAfter`, fallback suppression outside special-attack states keys off live runtime owner timing (`_runtimeAngerGaugeFullChargeEffectIntervalMs`) instead of static attack-list metadata, fallback burst registrations refresh the shared owner start-time lane (`_fullChargeEffectStartTime`), and owner replay state advances only after an actual owner registration candidate exists (non-empty frames, resolved owner-or-loaded path, and active animation displayer) so timing is not consumed by non-rendered bursts. The recovered `MobAngerGaugeBurstParity` seam carries an Astaroth WZ authoring trace that pins the frame geometry/origins/delays plus charge and timed-special metadata, and the recovered `CMob::AngerGaugeFullChargeEffect` caller trace pins start-time-before-StringPool ordering and the native slash path separator in addition to the caller address, animation-displayer callee address, StringPool ids, source-UOL resolution, replay-gate result, start-time-before-displayer ordering, head-origin handoff, and action-layer overlay-parent ownership. Focused `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs` coverage now pins the authored-`attackAfter` owner trigger precedence over generic `delay` / `effectAfter`, timed replay gate (`m_bFullChargeEffectStartTime + m_bFullChargeEffectTime`), stale timed-interval clearing when later special attacks omit authored `attackAfter`, owner-vs-fallback cadence split helpers (including outside-special runtime-timing suppression), owner-lane start-time refresh gating after fallback registrations, recovered WZ authoring trace, recovered mob-caller trace and displayer handoff metadata, recovered one-time registration operation constants (`LoadLayer` option `0xC00614A4`, `GA_STOP`, `RegisterOneTimeAnimation` delay `0`), owner-registration precondition checks plus the actual `AnimationEffects.AddFullChargedAngerGauge` no-op entrypoint for missing renderable owner candidates, recovered native lifecycle/reference-balance and per-operation lifetime traces, live head-origin resolver behavior, GA_STOP authored-duration completion, and shared `MapleStoryStringPool` seam invariants (pinned ids `0x03CE` / `0x0C2F`, owner-path fallback behavior, and formatted owner path `Mob/<id>.img/AngerGaugeEffect`) so owner-path formatting, WZ authoring facts, renderability preconditions, caller/displayer handoff metadata, lifetime metadata, and timing ownership cannot drift. | Rage-boss parity is closer because burst ownership, WZ frame authoring, authored `attackAfter` owner trigger gating, replay timing gate, caller trace, source-UOL retention, native slash-separated StringPool formatting, `GA_STOP`, no-flip `LoadLayer`, action-layer overlay-parent kind, live head-origin resolution, renderable-candidate gating, and owner-specific one-shot registration stay on one recovered seam with executable coverage. Remaining gap: presentation is still a managed DX approximation and does not recreate byte-identical native Gr2D blend output. | mob anger-gauge presentation layer (`CMob::AngerGaugeFullChargeEffect`, `CAnimationDisplayer::Effect_FullChargedAngerGauge`, `CAnimationDisplayer::LoadLayer`, `CAnimationDisplayer::RegisterOneTimeAnimation`, `Mob/9400633.img/AngerGaugeEffect`, `Mob/9400633.img/attack4/info/specialAttack`, `MapleStoryStringPool.cs`, `MobAngerGaugeBurstParity.cs`, `AnimationEffects.cs`, `MobItem.cs`, `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs`) |

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
