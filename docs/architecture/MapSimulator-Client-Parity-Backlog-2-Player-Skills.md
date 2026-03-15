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
| Partial | Buff icon / temporary-stat UI parity | Active buffs now drive a WZ-backed status-bar tray with live duration countdowns sourced from `UI/BuffIcon.img`, the tray supports client-style right-click cancellation directly from the live buff list, and the status-bar snapshot now exposes mapped temporary-stat families so multi-stat buffs prefer a concrete primary stat icon while tooltips enumerate the affected temporary stats using the matching `BuffIcon.img` display names, but the simulator still lacks client-confirmed ordering and the full temporary-stat surface | Buff-heavy jobs are now visible on the HUD, can be cleared without waiting for expiry, and no longer collapse multi-stat buffs into a fully opaque generic tray entry while tooltip text now matches the WZ stat-family names more closely, but exact temporary-stat behavior is still not fully client-parity | `SkillManager.ActiveBuffs`, `SkillManager.GetStatusBarBuffEntries`, `StatusBarUI.cs`, `UILoader.cs` (`CUIStatusBar::Draw`, `CUserLocal::Update`) |
| Partial | Melee / ranged / magic resolution | The runtime now preserves WZ `lt/rb` attack bounds, resolves melee and magic targets with separate ordering rules, gives projectile skills family-specific collision ordering plus homing steering, keeps homing active even when WZ `ball` data also marks the projectile as exploding, detonates WZ `ball/explode` projectiles on impact so nearby mobs within `explodeRadius` are resolved under the same effective target cap, respects the effective projectile hit cap from both `level/mobCount` and `ball/mobCount`, and now routes WZ `info/rectBasedOnTarget=1` projectile skills through an additional target-anchored rectangle after first impact so client rect-attack families such as `3001004`, `3101005`, and `5201002` no longer depend on raw projectile overlap alone, but it still does not mirror every client-only branch or projectile behavior one-for-one | Skills are more consistent with WZ data and attack family shape, but hit timing and edge-case behavior are still not fully client-perfect | `SkillManager.ProcessMeleeAttack`, `ProcessMagicAttack`, `CheckProjectileCollisions`, `SkillLoader.ParseSkillInfo` (`CUserLocal::DoActiveSkill_MeleeAttack`, `CUserLocal::DoActiveSkill_ShootAttack`, `CUserLocal::DoActiveSkill_MagicAttack`, `CUserLocal::TryDoingSmoothingMovingShootAttack`) |
| Partial | `DoActiveSkill_*` family parity | Generic movement, summon, and prepare/charge families now execute, movement-family dispatch now separates teleport snaps, grounded rush, flying-rush glide, and jump-rush launches from client-confirmed `TryDoingTeleport` / `TryDoingRush` / `TryDoingFlyingRush` seams, meso-explosion skills flagged by WZ `info/mesoExplosion` now consume nearby owned mesos inside the skill `lt/rb` box before applying their multi-hit payload, smoke-field skills with WZ `info/zoneType=invincible` now create a persistent local protection zone and tile it through the simulator draw path so `Smoke Bomb` blocks mob and touch damage while the player stays inside the skill box, and admin-skill execution now enforces GM/SuperGM job ownership at cast time so broader skill catalogs cannot fire `900.img` / `910.img` skills from normal jobs, but the simulator still does not mirror every named client branch such as job-specific bound jumps or every remaining field-family branch one-for-one | The largest remaining gap is now fidelity of per-family behavior rather than total lack of execution support | `SkillManager`, `PlayerCharacter`, `PlayerCombat` (`CUserLocal::DoActiveSkill_*` family) |
| Partial | Queued follow-up attack families | Attack skills that publish WZ `finalAttack` follow-up maps now queue a deferred extra strike after the initiating melee or projectile hit resolves, gate that follow-up by the equipped weapon type both when the proc is queued and again when it executes, reuse the learned follow-up skill's proc rate, carry the preferred target forward into the queued execution path, preserve the queued facing through deferred hitbox and projectile resolution even if the player turns before the follow-up fires, and overwrite any still-pending final-attack proc with the newest one so the simulator matches the client's single pending follow-up slot instead of stacking multiple deferred final attacks, but client `TryDoingSerialAttack` / `TryDoingSparkAttack` families and any remaining `finalAttack` branch nuances are still missing | Final-attack style procs no longer collapse into the initiating cast timing, and queued strikes now cancel if the player changes to a non-matching weapon before the deferred attack fires or if a newer follow-up proc replaces the pending one before it can execute, which brings visible cadence, weapon validation, pending-slot behavior, and follow-up directionality closer to the client even though the broader queued follow-up surface is still incomplete | `SkillManager`, `SkillLoader` (`CUserLocal::TryDoingFinalAttack`, `CUserLocal::TryDoingSerialAttack`, `CUserLocal::TryDoingSparkAttack`) |
| Partial | Repeat-skill sustain families | `CUserLocal::TryDoingRepeatSkill` still manages repeat-skill state for mechanic sustain skills such as siege/tank/SG-88, including key-down bar setup, summon assist toggles, and timeout-driven effect requests; the simulator now enforces the WZ `fixedState` / `canNotMoveInState` lock for siege and tank-siege transforms, honors their WZ `onlyNormalAttack` state by blocking unrelated skill casts while the sustain transform is active, auto-releases those two sustain states after their 5-second client timer, hands tank-siege back to tank mode instead of leaving the transform latched, and treats SG-88 recasts as a manual assist toggle that pauses or resumes the live summon's attack cadence without redeploying it, but the remaining repeat branches are still missing | Mechanic sustain skills are closer on fixed-state timing, in-state attack restrictions, teardown, and summon assist interaction, but the broader repeat-skill family still diverges from the client | `SkillManager`, `PlayerCharacter` (`CUserLocal::TryDoingRepeatSkill`) |
| Partial | Bound-jump, rocket-booster end, and smoothing moving-shoot branches | `Rocket Booster` now uses a staged startup that waits for its `rbooster_pre` action before launching into a bound-jump-style arc, tracks the airborne phase until foothold re-entry, and triggers the landing strike plus recovery cleanup on touchdown, while ranged attack skills marked with WZ `casterMove` now snapshot both the preferred mob and the firing direction from the cast-start attack box and defer their projectile or ranged payload slightly so the shot resolves after movement startup without retargeting to a later-in-range mob or flipping to a post-turn facing; broader client-only moving-shoot targeting nuances and any remaining bound-jump family variants are still missing | Movement-coupled attack skills now preserve more of the client's traversal, landing cadence, and moving-shot target retention instead of collapsing into the old teleport/rush/flying-rush-only movement path or instant moving-fire hits | `SkillManager`, `SkillLoader`, `SkillData`, `PlayerCharacter` (`CUserLocal::TryDoingRocketBooster`, `CUserLocal::TryDoingRocketBoosterEnd`, `CUserLocal::TryDoingSmoothingMovingShootAttack`) |
| Partial | Key-down / charge skills | The simulator now routes key-down HUD rules through the prepared-skill runtime so client-confirmed bar suppression for `3121004`, `5201002`, `13111002`, `22121000`, `22151001`, `33121009`, `35001001`, and `35101009` no longer shows the generic status-bar gauge, preserves the `KeyDownBar4` skin for `35121003`, mirrors the client's 1-second generic gauge timing for `14111006`, and caps mechanic `35001001` / `35101009` sustain after the charge window enters its actual hold phase instead of counting the 8-second branch limit from cast start, but it still lacks the client's full repeat cadence, remaining special skins, and other per-skill prepare branches | Charge-family skills now match more of the client's visible HUD branching and mechanic timeout behavior instead of always reusing one generic bar, and the mechanic flamethrower family now gets its full sustained window after charging before auto-release, but the underlying prepared-skill execution loop is still simplified vs client behavior | `SkillManager`, `StatusBarUI.cs`, `UILoader.cs` (`CUserLocal::DoActiveSkill_Prepare`, `CUserLocal::TryDoingPreparedSkill`, `CUserLocal::DrawKeyDownBar`) |
| Partial | Swallow, mine, cyclone, and sit-down healing branches | The simulator now auto-deploys mine summons from learned `33101008` after the client's 1.5s continuous mounted-movement cadence, sustains cyclone as a timed `32121003` body-attack loop with 1s pulses while the action remains active, lets healing summons honor WZ `minionAbility=heal` plus `condition=whenUserLieDown` by restoring HP/MP while the player is sitting, routes swallow-named casts through a dedicated branch that consumes the first mob in the skill hit box with `MobDeathType.Swallowed`, starts the skill's timed buff state when the swallow skill carries one, and gives Wild Hunter swallow (`33101005`) a dedicated 500 ms wriggle/digest loop that stuns the captured mob until the digest window ends and then applies the follow-up buff, but broader swallow-family request/cancel nuances are still simplified | Several client-only active-skill branches now execute in play instead of falling through the generic cast path, and Wild Hunter swallow now has explicit digest cadence instead of skipping straight from capture to buff, but the wider swallow control flow is still not fully client-parity | `SkillManager`, `SkillLoader.cs`, `SkillData.cs`, `PlayerCharacter` (`CUserLocal::TryDoingSwallowBuff`, `CUserLocal::TryDoingSwallowMobWriggle`, `CUserLocal::TryDoingMine`, `CUserLocal::TryDoingCyclone`, `CUserLocal::TryDoingSitdownHealing`) |
| Partial | Summon simulation | Summon-family skills now create a generic summon lifecycle with duration, rendering, summon attack-branch playback, periodic nearby attacks, deferred mob removal during summon attack resolution, registration into the simulator's puppet/aggro system, client-backed movement-style buckets for stationary emplacement, grounded leash, owner-hover, and anchor-based roaming summons, owner ladder/rope attack gating with the client-confirmed octopus / self-destruct exemptions, foothold-settled spawn and follow positioning for stationary, grounded-follow, and anchor-bound summons, and WZ-backed summon attack-branch fallback for variant names such as `attackTriangle` instead of only `attack1` / `attack` / `attack0`, but they still do not reproduce per-skill summon attack logic or full aggro behavior one-for-one | Summon-heavy jobs are now usable, more runtime-stable, and visually closer to client summon behavior, with more non-standard summon attack animations now playing from WZ branch data and ground-based summons no longer hanging at raw spawn offsets, but they are still not client-parity | `SkillManager`, `SkillLoader.cs`, `SummonMovementResolver.cs`, `MobPool.cs`, entity/pool layer (`CUserLocal::DoActiveSkill_Summon`, `CSummoned::Init`, `CVecCtrlSummoned::WorkUpdateActive`, `CSummoned::TryDoingAttack`) |
| Partial | Skill cooldown UI parity | QuickSlot overlays now show live remaining-seconds countdown text per assigned cooling-down skill, the skill window once again routes icon drag-and-drop into the instantiated quick-slot bar, Big Bang skill-book entries now dim and show live remaining-seconds text while their skill is cooling down with the hover tooltip surfacing ready vs remaining cooldown state, and the status bar now renders a compact cooldown tray for hotkeyed skills that are actively cooling down and orders those tray entries by active cooldown start time with a stable hotkey-slot tie-break instead of the simulator's former raw slot-index ordering, but the simulator still lacks the client's broader per-skill feedback system | Cooling-down skills are more readable in the skill book, quick-slot bar, and main HUD, and the status-bar tray now follows a runtime-driven cast-recency order instead of a static hotkey walk, but the simulator still lacks the full client cooldown HUD surface | `QuickSlotUI.cs`, `SkillUI.cs`, `SkillUIBigBang.cs`, `UIWindowManager.cs`, `StatusBarUI.cs`, `MapSimulator.cs`, `SkillManager.cs` |
| Partial | Client-timer skill expiry and cancellation | `SkillManager` now owns a centralized client-timer registry that drives buff expiry and timed prepared-skill release through one `UpdateClientTimer`-style update seam, swallow-buff state teardown now rides that same timer-owned buff expiry path instead of a separate polling check, manual buff cancellation clears the swallow state immediately, and the runtime still emits timer-expired callbacks that future client-only cancellation/request paths can hook into, but the simulator still does not mirror the client's full per-skill timer catalog or downstream cancel-request behavior | Timed skill teardown now has a real shared runtime surface instead of being split across isolated expiry checks, which brings swallow-buff expiry and cancel cleanup under the same client-timer-owned path and makes future client-owned timer semantics implementable without reworking the core skill loop again | `SkillManager` (`CUserLocal::UpdateClientTimer`) |
| Partial | Skill restrictions by map/state | The simulator now enforces parsed map `fieldLimit` bits for `Unable_To_Use_Skill`, `Move_Skill_Only`, `Unable_To_Use_Mystic_Door`, and the Mechanic-only `Unable_To_Use_Rocket_Boost` branch through the shared skill runtime, but broader client-only forbidden-skill branches and transient field-state checks are still incomplete | Maps that explicitly suppress skills now block the same broad skill families in the simulator and can reject Rocket Booster on field entry and cast attempt, but the full `CUserLocal` / `CField` gating surface is still not modeled | `MapSimulator`, `SkillManager`, `FieldSkillRestrictionEvaluator.cs`, `MapInfo.fieldLimit`, `FieldLimitType` (`CUserLocal::Update`, `CField::IsUnableToUseSkill`, `CField::IsMoveSkillOnly`, `CField::IsUnableToUseRocketBoost`) |

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
