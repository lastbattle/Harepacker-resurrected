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

### 1. Player skill execution parity

- `CUserLocal::DoActiveSkill_MeleeAttack` at `0x93b210`
- `CUserLocal::DoActiveSkill_ShootAttack` at `0x93b520`
- `CUserLocal::DoActiveSkill_MagicAttack` at `0x93a010`
- `CUserLocal::DoActiveSkill_Prepare` at `0x941710`
- `CUserLocal::TryDoingFinalAttack` at `0x93aaa0`
- `CUserLocal::TryDoingSerialAttack` at `0x93ac90`
- `CUserLocal::TryDoingSparkAttack` at `0x93abe0`
- `CUserLocal::TryDoingPreparedSkill` at `0x944270`
- `CUserLocal::TryDoingRepeatSkill` at `0x93d400`
- `CUserLocal::DrawKeyDownBar` at `0x9153b0`
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

Notes:
`CUserLocal::Update` at `0x937330` orchestrates several of these paths and confirms that key-down gauge updates, queued follow-up attacks, prepared-skill execution, repeat-skill handling, teleport, rush, flying-rush, rocket-booster start/end handling, movement-coupled shoot branches, timer-driven cancellations, and status-bar feedback are coordinated in one local-player update loop rather than isolated one-off handlers.

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

### 1. Player skill execution parity

The simulator has a generic skill runtime. It does not yet mirror the client's named `CUserLocal` behavior families.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Generic skill casting | The simulator now loads the full player skill catalog from `Skill.wz`, learns active skills into the runtime, supports direct cast invocation from the skill window in addition to hotkey casting, keeps GM/SuperGM skill-book resolution compatible with both `900.img` and `910.img` layouts while focusing those jobs on their own job-book window instead of the global advancement catalog, evaluates Big Bang `common` formulas in IMG-mode data sets so skills like `Power Strike` are treated as castable attacks instead of passive placeholders, mirrors skill effect anchoring/hitbox fallback by facing direction for asymmetric melee effects, sanitizes unsupported SpriteFont glyphs in the skill window so WZ-backed names no longer crash row rendering, and resolves cast SFX from `Sound/Skill.img` so skill use now plays the corresponding `Use` or fallback attack sound through the simulator audio path | This closes the previous "can only use the current job's limited skill book" runtime/UI gap and keeps special-job skill tabs populated and executable across client data variants | `SkillManager.cs`, `SkillLoader.cs`, `SkillUIBigBang.cs`, `MapSimulator.cs`, `UIWindowLoader.cs` |
| Implemented | Buff stat application | Buff logic now applies `PAD`, `PDD`, `MAD`, `MDD`, `ACC`, `EVA`, `Speed`, and `Jump` directly to the player build and cleanly removes them when buffs expire or refresh | Active buff skills now change simulator stats and movement-relevant values instead of only a partial subset | `SkillManager.ApplyBuffStats` |
| Partial | Buff icon / temporary-stat UI parity | Active buffs now drive a WZ-backed status-bar tray with live duration countdowns sourced from `UI/BuffIcon.img`, the tray supports client-style right-click cancellation directly from the live buff list, `UILoader` normalizes and loads the full `BuffIcon.img` icon surface so mapped temporary-stat entries can actually resolve their `BuffIcon` art instead of falling through the generic icon path, and the status-bar snapshot now orders the known `BuffIcon.img/buff/*` families by the WZ catalog surface while also inferring additional temporary-stat families from loaded skill text and flags so buffs can surface `Craft`, `Max HP`, `Max MP`, `Booster`, `Stance`, `Invincible`, `Critical Rate`, `Damage Reduction`, `Transform`, `EXP Rate`, `Drop Rate`, `Meso Rate`, `Critical Damage`, `Combo`, `Charge`, `Aura`, `Shadow Partner`, `Debuff Resistance`, and recovery-style states even when parsed numeric level data is sparse or a fallback description only exposes abbreviated `PAD`/`MAD`/`ACC`-style labels. Multi-stat buffs still prefer a more concrete primary family before falling back to the skill icon, and tooltips enumerate the affected temporary stats with the matching `BuffIcon.img` display names where available, but the simulator still lacks client-confirmed exact cross-family ordering and a truly exhaustive temporary-stat catalog | Buff-heavy jobs are visible on the HUD, can be cleared without waiting for expiry, and no longer collapse as often into a fully opaque generic tray entry while tooltip text and primary-icon selection now stay closer to the WZ stat-family surface for both numeric and several text-, abbreviation-, or flag-inferred buff families. The remaining parity gap has narrowed to the client's exact runtime ordering rules and the last long tail of temporary-stat families whose presentation is owned by deeper client-side state rather than `UI/BuffIcon.img` or the simulator's current parsed buff metadata | `SkillManager.ActiveBuffs`, `SkillManager.GetStatusBarBuffEntries`, `StatusBarUI.cs`, `UILoader.cs` (`CUIStatusBar::Draw`, `CUserLocal::Update`) |
| Partial | Melee / ranged / magic resolution | The runtime now preserves WZ `lt/rb` attack bounds, resolves melee and magic targets with separate ordering rules, gives projectile skills family-specific collision ordering plus homing steering, keeps homing active even when WZ `ball` data also marks the projectile as exploding, detonates WZ `ball/explode` projectiles on impact so nearby mobs within `explodeRadius` are resolved under the same effective target cap, respects the effective projectile hit cap from both `level/mobCount` and `ball/mobCount`, routes WZ `info/type` through the skill-family classifier so no-ball spell and shoot families no longer collapse into melee resolution by default, gives direct no-projectile ranged skills their own fallback hitbox and target ordering instead of reusing the melee branch, routes WZ `info/rectBasedOnTarget=1` projectile skills through an additional target-anchored rectangle after first impact so client rect-attack families such as `3001004`, `3101005`, and `5201002` no longer depend on raw projectile overlap alone, re-resolves chained follow-up targets from the live impact/serial origin using the WZ `range` bounce radius plus nearest-neighbor chaining instead of only replaying the initial overlap snapshot, and now stages multi-shot projectile release from WZ `ballDelay` / `ballDelayN` timing instead of front-loading every projectile on cast start, but it still does not mirror every client-only branch or projectile behavior one-for-one | Skills are more consistent with WZ data and attack family shape, no-ball spell or shoot families now resolve through the intended client attack family instead of inheriting melee defaults, chain families keep later bounces anchored to current mob positions instead of stale first-frame overlap, and multi-bullet projectile skills can now follow authored release timing instead of always spawning simultaneously, but swept collision timing, damage falloff, and edge-case behavior are still not fully client-perfect | `SkillManager.ProcessMeleeAttack`, `ProcessMagicAttack`, `SpawnProjectile`, `CheckProjectileCollisions`, `ResolveQueuedSerialTargets`, `SkillLoader.ParseSkillInfo`, `SkillLoader.CreateLevelData` (`CUserLocal::DoActiveSkill_MeleeAttack`, `CUserLocal::DoActiveSkill_ShootAttack`, `CUserLocal::DoActiveSkill_MagicAttack`, `CUserLocal::TryDoingSmoothingMovingShootAttack`) |
| Partial | `DoActiveSkill_*` family parity | Generic movement, summon, and prepare/charge families now execute, movement-family dispatch now separates teleport snaps, grounded rush, flying-rush glide, and jump-rush launches from client-confirmed `TryDoingTeleport` / `TryDoingRush` / `TryDoingFlyingRush` seams, teleport skills now honor WZ-linked Teleport Mastery body-attack passives by treating `info/affectedSkillEffect=bodyAttack&&stun` skills such as `2111007`, `2211007`, `2311007`, and `32111010` as passive follow-up data instead of directly castable actives and firing their destination hitbox immediately after the associated teleport resolves, WZ `psd=1` linked helper branches are now also kept off the direct-cast lane so affected-skill riders such as Battle Mage `Advanced Dark Aura` (`32120000`) stay passive in the simulator instead of surfacing as standalone actives, Battle Mage aura actives now honor the WZ text on `Dark Aura` (`32001003`), `Blue Aura` (`32101002`), and `Yellow Aura` (`32101003`) by cancelling on same-skill recast and removing any other active aura before the new one applies so those buffs cannot stack simultaneously, learned aura-dot riders with `info/affectedSkillEffect=dot` plus `info/dotType=aura` now pulse once per second around the player while their linked aura buff is active and stop at 1 HP like the client-facing WZ text, meso-explosion skills flagged by WZ `info/mesoExplosion` now consume nearby owned mesos inside the skill `lt/rb` box before applying their multi-hit payload, smoke-field skills with WZ `info/zoneType=invincible` now create a persistent local protection zone and tile it through the simulator draw path so `Smoke Bomb` and `Party Shield` block mob and touch damage while the player stays inside the skill box, and admin-skill execution now enforces GM/SuperGM job ownership at cast time so broader skill catalogs cannot fire `900.img` / `910.img` skills from normal jobs, but the simulator still does not mirror every named client branch such as deeper bound-jump variants or the remaining field-family one-offs one-for-one | The remaining gap is narrower and centered on the last client-owned per-family special cases rather than generic execution support; linked passive rider skills no longer leak onto the direct cast surface, Battle Mage aura toggles now cancel/swap in the client-facing WZ manner, and aura-dot follow-up data executes from the owning aura instead of being stranded as inert metadata, but deeper bound-jump and residual field-family branches are still simplified | `SkillManager`, `PlayerCharacter`, `PlayerCombat` (`CUserLocal::DoActiveSkill_*` family) |
| Missing | Shadow Partner render/action parity | The client still treats Shadow Partner as a dedicated action/property owner through `CActionMan::GetShadowPartnerProp` and `CActionMan::LoadShadowPartnerAction`, but the simulator backlog currently only mentions `Shadow Partner` as a buff-icon/temporary-stat family rather than tracking clone frame loading, layered playback, or attack-synchronized mirror timing as its own parity surface | Jobs that depend on clone mirroring are visually wrong even when the underlying buff state exists, because the client does not render Shadow Partner by reusing the base avatar path alone and instead resolves dedicated properties and action frames with their own timing and fallback behavior | skill-owned avatar companion layer (`CActionMan::GetShadowPartnerProp`, `CActionMan::LoadShadowPartnerAction`) |
| Partial | Queued follow-up attack families | Attack skills that publish WZ `finalAttack` follow-up maps now queue a deferred extra strike after the initiating melee or projectile hit resolves, gate that follow-up by the equipped weapon type both when the proc is queued and again when it executes, reuse the learned follow-up skill's proc rate, carry the preferred target forward into the queued execution path, preserve the queued facing through deferred hitbox and projectile resolution even if the player turns before the follow-up fires, overwrite any still-pending final-attack proc with the newest one so the simulator matches the client's single pending follow-up slot instead of stacking multiple deferred final attacks, route Thunder Breaker `Spark` (`15111006`) through its own timed attack-triggered proc family by treating the WZ `info/condition=attack` + `info/chainAttack=1` cast as a timed buff with a separate pending spark slot, and now also queue a distinct single pending serial-attack slot for WZ `info/chainAttack=1` + `info/chainattackPenalty=1` skills so chain families such as `2221006` and `5121002` stop front-loading every chained hit on the initiating frame, keep the queued serial facing and preferred target, recheck the weapon type before the deferred chain branch executes, and derive the deferred serial branch from the client-like single stored origin mob/position instead of replaying an upfront queued target list, but chain-penalty scaling and the remaining spark/final-attack edge cases are still missing | Final-attack style procs no longer collapse into the initiating cast timing, queued strikes now cancel if the player changes to a non-matching weapon before the deferred attack fires or if a newer follow-up proc replaces the pending one before it can execute, active `Spark` buffs now produce their own delayed nearby follow-up hit without stealing the client's final-attack pending slot, and WZ-marked serial-attack families now use their own pending slot while re-deriving later chain hits from the live stored origin target instead of replaying stale first-frame target snapshots, which brings cadence and serial target selection closer to the client even though chain damage falloff and deeper spark or final-attack nuances remain incomplete | `SkillManager`, `SkillLoader` (`CUserLocal::TryDoingFinalAttack`, `CUserLocal::TryDoingSerialAttack`, `CUserLocal::TryDoingSparkAttack`) |
| Partial | Repeat-skill sustain families | `CUserLocal::TryDoingRepeatSkill` still owns more of the Mechanic repeat-state machine than the simulator, but the runtime now carries repeat-skill state not just for siege and tank-siege sustain teardown but also for tank mode and SG-88: siege and tank-siege still enforce the WZ `fixedState` / `canNotMoveInState` lock, honor `onlyNormalAttack` by blocking unrelated skill casts, auto-release after their 5-second client timer, emit the client-mapped timeout effect-request ids (`35110004` for siege and `35120013` for tank-siege), and hand tank-siege back to tank mode instead of leaving the transform latched; tank mode (`35121005`) and the two siege sustains now also route normal attack input through their repeat-owned skill shots with the client-confirmed `TryDoingSiege` lockout windows (`420 ms` for tank mode, `180 ms` for siege / tank-siege) instead of falling back to the simulator's generic basic attack; tank-siege timeout now also follows a closer client-style two-step path by latching a pending mode-end request on the first timeout update, suppressing further repeat-owned attacks while that latch is set, and only handing back to tank mode from the repeat-state update on the following tick instead of clearing immediately; and SG-88 now uses the client skill id (`35121003`), starts with assist fire disabled, tracks the local `bDone` / branch-point progression in repeat state, shows the `KeyDownBar4` HUD after the client's `810 ms` delay, arms summon assist after the client's `2790 ms` branch timing, and tears the summon down after the client-style `2520 ms` idle window if assist is toggled off and not re-armed. The remaining gap is the true client/server confirmation path behind tank-siege mode-end completion (`m_bSendTankSiegeModeEnd` beyond the local latch), any exact packet or request acknowledgement sequencing tied to that confirmation, and residual per-branch Mechanic assist or attack bookkeeping that still lives outside the simulator's modeled repeat-state timings | Mechanic repeat skills are now closer on fixed-state timing, in-state attack restrictions, repeat-owned normal attack cadence, SG-88 branch-state timing, and tank-siege timeout teardown sequencing, but the simulator still simplifies the final client-owned confirmation and ack path behind tank-siege end plus some deeper branch bookkeeping | `SkillManager`, `PlayerCharacter` (`CUserLocal::TryDoingRepeatSkill`, `CUserLocal::TryDoingSiege`, `CUserLocal::DrawKeyDownBar`) |
| Partial | Bound-jump, rocket-booster end, and smoothing moving-shoot branches | `Rocket Booster` still uses the staged `rbooster_pre` startup, airborne touchdown detection, landing strike, and 500 ms recovery cleanup while preserving the original launch-facing through landing, and the moving-shoot path still snapshots the cast-start preferred mob, firing direction, and world origin so deferred projectile and direct ranged payloads resolve from that stored origin after movement startup instead of re-reading a later position or facing. This row is now closer to the client because WZ `info/delay` values and movement-action animation duration now hold deferred `casterMove` payloads until longer startup branches such as `backstep` or `assaulter` finish instead of forcing the old 60-180 ms fallback window, and the runtime now also promotes client `info/type=40` bound-jump families plus `assaulter` / `backspin` / `screw` / `doublejump` move actions onto a stronger bound-jump arc even when WZ omits an explicit move token on the primary action. The remaining gap is the client's full moving-shoot entry snapshot such as stored bullet or cash-item metadata, exact mob-target derivation or shoot-point rules, and the last per-skill bound-jump formulas or request gating that still live in client-owned special cases | Movement-coupled attack skills now preserve more of the client's traversal, startup timing, landing cadence, moving-shot target retention, cast-origin lock, and move-family directionality instead of collapsing into the old teleport/rush/flying-rush-only movement path or instant moving-fire hits | `SkillManager`, `SkillLoader`, `SkillData`, `PlayerCharacter` (`CUserLocal::TryDoingRocketBooster`, `CUserLocal::TryDoingRocketBoosterEnd`, `CUserLocal::TryDoingSmoothingMovingShootAttack`) |
| Partial | Key-down / charge skills | The simulator now routes key-down HUD rules through the prepared-skill runtime so client-confirmed bar suppression for `3121004`, `5201002`, `13111002`, `22121000`, `22151001`, `33121009`, `35001001`, and `35101009` no longer shows the generic status-bar gauge, preserves the `KeyDownBar4` skin for `35121003`, mirrors the client's 1-second generic gauge timing for `14111006`, caps mechanic `35001001` / `35101009` sustain after the charge window enters its actual hold phase instead of counting the 8-second branch limit from cast start, loads WZ `repeat` branches into the prepared-skill hold loop so charge families such as `4341002`, `5101004`, and `15101003` swap to their dedicated repeat effect/cadence once the hold phase begins, and now also loads and renders WZ companion prepare/hold/release branches such as `prepare0`, `keydown0`, and `keydownend0` so multi-layer prepared-skill effects like `24121000`, `5221004`, and `5721001` can draw their negative-`z` underlay branch behind the avatar instead of dropping that branch entirely. Prepared hold teardown is now closer to the client's shared timer path because capped keydown sustain starts a `prepared-release` client timer when the real hold phase begins instead of relying only on per-frame hold polling, and cancel requests now only acknowledge matching local prepared-skill state while tearing that state down through the shared cancel seam instead of blindly succeeding on unmatched ids. The remaining gap is the deeper client-owned prepared-skill handshake around actual key-state/scan-code ownership, exclusive request throttling, dragon-owned keydown-bar placement, and the remaining per-skill release or follow-up branches that still live outside WZ-authored visual data | Charge-family skills now match more of the client's visible HUD branching, mechanic timeout behavior, timer-owned hold expiry, and multi-layer prepare visuals instead of always reusing one generic bar or one foreground-only cast effect, the mechanic flamethrower family now gets its full sustained window after charging before auto-release, repeated charge holds can follow the WZ-authored repeat branch, and cancel requests no longer report false-positive prepared-skill teardown. The underlying prepared-skill execution loop is still simplified vs client behavior because the simulator does not yet model the full input/request handshake or every special-case release branch | `SkillManager`, `SkillLoader.cs`, `StatusBarUI.cs`, `UILoader.cs`, `PlayerManager.cs` (`CUserLocal::DoActiveSkill_Prepare`, `CUserLocal::TryDoingPreparedSkill`, `CUserLocal::DrawKeyDownBar`) |
| Partial | Swallow, mine, cyclone, and sit-down healing branches | The simulator now auto-deploys mine summons from learned `33101008` after the client's 1.5s continuous mounted-movement cadence, sustains cyclone as a timed `32121003` body-attack loop with 1s pulses while the action remains active, loads sit-down healing support ranges from the summon branch metadata, lets healing summons honor WZ `minionAbility=heal` plus `condition=whenUserLieDown` only while the sitting player remains inside that heal box, treats `35111011` recovery as its WZ-authored HP-percent heal and applies the skill text's `x=5` second re-heal lock instead of healing every summon tick, routes swallow-family skill gating through loaded WZ `info/swallow` / `dummyOf` metadata instead of name checks, and now drives Wild Hunter swallow (`33101005`) through a client-shaped charge-and-release path where the visible skill uses the `900 ms` keydown gauge before release, early release cancels without executing, release-triggered payloads consume cost only once the charge completed, capture and digest both ride that same visible-skill release seam, the dedicated `500 ms` wriggle/digest loop still starts only when the digest branch is requested, and the hidden attack dummy branch (`33101007`) still consumes the captured mob before firing its follow-up ranged payload, but the exact packet/request sequencing, server-owned absorb outcome branching, and any remaining non-Wild-Hunter swallow family nuances are still simplified | Several client-only active-skill branches now execute in play instead of falling through the generic cast path, sit-down healing now follows the authored area, percent-heal, and re-heal cadence instead of restoring on every support pulse, and Wild Hunter swallow is closer to the client's visible `33101005` input loop because charge threshold, early cancel, and digest start now ride the release flow instead of hidden direct-cast shortcuts, but the wider swallow control flow is still not fully client-parity | `SkillManager`, `SkillLoader.cs`, `SkillData.cs`, `PlayerCharacter` (`CUserLocal::TryDoingSwallowBuff`, `CUserLocal::TryDoingSwallowMobWriggle`, `CUserLocal::TryDoingMine`, `CUserLocal::TryDoingCyclone`, `CUserLocal::TryDoingSitdownHealing`) |
| Partial | Summon simulation | Summon-family skills now create a generic summon lifecycle with duration, rendering, summon attack-branch playback, periodic nearby attacks, deferred mob removal during summon attack resolution, taunt-only registration into the simulator's puppet/aggro system based on WZ `minionAbility`, client-backed movement-style buckets for stationary emplacement, grounded leash, owner-hover, and anchor-based roaming summons, owner ladder/rope attack gating with the client-confirmed octopus / self-destruct exemptions, foothold-settled spawn and follow positioning for stationary, grounded-follow, and anchor-bound summons, WZ-backed summon attack-branch fallback for variant names such as `attackTriangle` instead of only `attack1` / `attack` / `attack0`, summon attack metadata sourced from `summon/attack*/info` so per-skill `attackAfter`, `attackCount`, `mobCount`, and both rectangular `lt/rb` plus circular `sp/r` attack ranges now drive the runtime, WZ `common/subTime` fallback cadence for summons that omit `attackAfter`, damaged-trigger reflector summons that now fire back at the mob that hit the owner instead of running on the generic periodic loop, WZ `info/minionAttack` tokens flowing into the summon hit-status inference path, self-destruct summons now consuming WZ `info/selfDestructMinion` plus `common/selfDestruction` to burst once and then remove themselves after their resolved attack/support action, and the runtime now classifies summons into client-shaped assist buckets so reactive reflectors stay target-driven, SG-88 stays on the manual-assist lane, support summons keyed by WZ `minionAbility` (`heal`, `mes`, `amplifyDamage`) run through a non-damaging support path instead of the generic attack loop, `minionAbility=summon` summons execute an action-only branch before self-removal, and periodic summon attackers now pick nearest in-range mobs from the summon origin instead of raw pool order, but it still does not reproduce the client's full per-skill assist-type targeting, Tesla Coil / octopus and other special-case summon attack branches, or mob aggro retargeting one-for-one | Summon-heavy jobs are now closer on assist-type dispatch, support-only summon behavior, reflector response timing, self-destruct cadence, and summon-origin target ordering instead of treating every summon as the same periodic puppet attacker, but the remaining parity gap is still concentrated in deeper client-only summon target selection, special-case attack logic, and exact aggro retarget behavior | `SkillManager`, `SkillLoader.cs`, `SkillData.cs`, `SummonMovementResolver.cs`, `MobPool.cs`, entity/pool layer (`CUserLocal::DoActiveSkill_Summon`, `CSummoned::Init`, `CVecCtrlSummoned::WorkUpdateActive`, `CSummoned::TryDoingAttack`) |
| Missing | Weapon afterimage / melee trail parity | The client still owns a dedicated melee-afterimage cache and layer builder through `CActionMan::GetWeaponAfterImage`, `CActionMan::CreateAfterimageLayer`, and `CActionMan::GetMeleeAttackRange`, including per-action afterimage canvases and authored attack-arc rectangles, but the backlog does not currently track that trail/effect surface as its own parity item | Many melee jobs rely on afterimage trails and authored swing ranges to sell timing and hit shape, and the client stores those assets separately from ordinary hit sprites, so omitting this row leaves a visible combat-feedback gap that is easy to miss while focusing only on damage resolution | combat/effect presentation layer (`CActionMan::GetWeaponAfterImage`, `CActionMan::CreateAfterimageLayer`, `CActionMan::GetMeleeAttackRange`) |
| Partial | Skill cooldown UI parity | QuickSlot overlays now show live remaining-seconds countdown text per assigned cooling-down skill, quick-slot hover tooltips surface live ready vs remaining cooldown state instead of only echoing the static WZ cooldown value, the legacy pre-Big Bang skill window now mirrors the Big Bang window's cooldown dimming, countdown text, and ready-vs-remaining hover text, the skill window once again routes icon drag-and-drop into the instantiated quick-slot bar, Big Bang skill-book entries now dim and show live remaining-seconds text while their skill is cooling down with the hover tooltip surfacing ready vs remaining cooldown state, the status bar now renders a compact cooldown tray for hotkeyed skills that are actively cooling down and orders those tray entries by active cooldown start time with a stable hotkey-slot tie-break instead of the simulator's former raw slot-index ordering, non-hotkey cooldowns now also surface in a second compact status-bar tray using the same live icon-mask countdown treatment, and the runtime drives off-bar cooldown start/blocked/ready notifications through the existing HUD/chat seams while playing the WZ-backed `Sound/UI.img/DlgNotice` notice cue, but the simulator still lacks the client's exact dedicated cooldown-notice art, per-notice placement/stacking rules, and any remaining bespoke non-hotkey cooldown HUD widgets beyond the shared seams plus the new off-bar tray | Cooling-down skills are now readable in both skill-book variants, the quick-slot bar, and two HUD trays, the hotkey tray still follows runtime cast-recency instead of a static slot walk, and off-bar cooldowns no longer fail silently because they now surface both as notice messages and as persistent status-bar entries until ready, but the simulator still does not reproduce the client's exact cooldown-notification presentation stack | `QuickSlotUI.cs`, `SkillUI.cs`, `SkillUIBigBang.cs`, `UIWindowManager.cs`, `StatusBarUI.cs`, `MapSimulator.cs`, `SkillManager.cs` |
| Partial | Client-timer skill expiry and cancellation | `SkillManager` now owns a centralized client-timer registry that drives buff expiry, timed prepared-skill release, mechanic repeat-sustain expiry, cyclone teardown, and Wild Hunter swallow digest completion through one `UpdateClientTimer`-style update seam; the runtime now also publishes ordered timer-expiry batch snapshots that mirror the client's packed expired-timer list more closely, normalizes client cancel requests through WZ-backed `info/type` + `affectedSkill` mappings for affected-skill families such as `32110000 -> 32101002`, `32120000 -> 32001003`, and `32120001 -> 32101003`, and routes manual status-bar buff cancellation through that same cancel-request seam with simulator time instead of bypassing it. That shared cancel path now tears down matching prepared-skill state, active buffs, cyclone sustain, repeat-sustain state, and local skill zones while preserving the existing swallow reset behavior; tank-siege cancel now reuses the local pending mode-end latch instead of clearing immediately, swallow-buff state teardown still rides the timer-owned buff expiry path, and swallowed-mob loss during Wild Hunter digest still emits the client-style cancel-request hook instead of silently dropping state. The simulator still does not mirror the client's full per-skill timer catalog or the remaining `SendSkillCancelRequest` special-case consumers such as vehicle-valid bookkeeping and keydown-specific prepare-animation cleanup | Timed skill teardown now has a real shared runtime surface instead of being split across isolated expiry checks, manual buff cancellation no longer bypasses the local cancel/request path, repeat/cyclone or zone-backed skill state can now be cleared through the same client-style request seam as prepared skills and swallow, and affected-skill cancel normalization stays data-backed. The remaining parity gap is narrower and centered on the last client-owned timer families plus `SendSkillCancelRequest` side effects that are still outside the simulator's current consumers | `SkillManager`, `MapSimulator` (`CUserLocal::UpdateClientTimer`, `CUserLocal::SendSkillCancelRequest`, `CUserLocal::TryDoingSwallowMobWriggle`) |
| Partial | Skill restrictions by map/state | The simulator now enforces parsed map `fieldLimit` bits for `Unable_To_Use_Skill`, `Move_Skill_Only`, `Unable_To_Use_Mystic_Door`, and the Mechanic-only `Unable_To_Use_Rocket_Boost` branch through the shared skill runtime, rejects active skill use while the local player is dead, in hit-stun, seated, or prone so skill-window and hotkey casts no longer bypass those transient local states, mirrors the client's swallow-specific ladder/rope precondition in the shared evaluator, tears down client-timer / upkeep-driven skill paths such as keydown holds, cyclone pulses, and mechanic mine auto-deploy when those same local-state restrictions become invalid after cast start, and now also mirrors the v115 `CUserLocal::IsSkillAvailable` disease gate by blocking skills while the local player is under mob-sourced `Stun` (`MobSkill.img/120`), `Seal` (`121`), or `Attract` (`128`) using the WZ-authored durations from both direct mob-skill casts and diseased mob attacks, but broader client-only forbidden-skill branches and deeper `CField` availability checks are still incomplete | Maps that explicitly suppress skills now block the same broad skill families in the simulator, Rocket Booster can be rejected on field entry and cast attempt, seated/prone transient states no longer let active skills slip through the generic cast path, swallow casts now fail on ladder/rope like the client branch, ongoing upkeep skills stop once the player falls into a forbidden local state instead of continuing through it, and the client's core stun / seal / seduce skill lockout now applies from mob skills and diseased attacks with the authored WZ timers instead of leaving those debuffs as visual-only hits, but the remaining `CField` gate branches and other client-only forbidden-skill checks outside that `IsSkillAvailable` trio are still not modeled | `MapSimulator`, `SkillManager`, `FieldSkillRestrictionEvaluator.cs`, `PlayerSkillStateRestrictionEvaluator.cs`, `PlayerSkillBlockingStatusMapper.cs`, `PlayerCharacter`, `PlayerCombat`, `PlayerManager`, `MapInfo.fieldLimit`, `FieldLimitType` (`CUserLocal::Update`, `CUserLocal::IsSkillAvailable`, `CField::IsUnableToUseSkill`, `CField::IsMoveSkillOnly`, `CField::IsUnableToUseRocketBoost`, `CUserLocal::TryDoingSwallowAbsorb`) |

Buff icon plan:

1. Confirm asset sources and ordering:
   Use `UI/BuffIcon.img` for temporary-stat icon chrome and `Skill/*/icon` for per-skill icon sourcing where the client uses the skill's own icon, then validate row ordering and refresh cadence against `CUIStatusBar::Draw` and `CUserLocal::Update`.
2. Add a runtime-to-UI snapshot:
   Expose a small status-bar model from `SkillManager` that includes `skillId`, applied stat family, start time, duration, and refresh/replace semantics so the UI does not need to inspect buff internals directly.
3. Extend status-bar loading and drawing:
   Teach `UILoader` and `StatusBarUI` to load buff-slot art, render the active icon row in the client-aligned status-bar region, and reserve the interaction seam for later right-click cancel behavior.
4. Verify state synchronization:
   Check short-duration buffs, long-duration buffs, same-skill refresh, natural expiry, and `/job` cleanup so icon removal stays synchronized with the runtime and does not fight quick-slot cooldown overlays.


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
