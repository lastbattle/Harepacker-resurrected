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

### 1. Avatar and player parity

- `CUser::LoadLayer` at `0x8e96d0`
- `CUser::PrepareActionLayer` at `0x8e3070`
- `CUser::SetMoveAction` at `0x8df540`
- `CUser::Update` at `0x8fb8d0`
- `CUser::UpdateMoreWildEffect` at `0x8fb4c0`
- `CUserLocal::SetMoveAction` at `0x903ce0`
- `CUserLocal::Update` at `0x937330`

Notes:
`CUser::LoadLayer` and `CUser::PrepareActionLayer` are the clearest resolved avatar-composition seams for layer loading and action-layer setup. `CUser::Update` and `CUser::UpdateMoreWildEffect` also show that the client maintains several additional avatar/effect layers outside the base assembler path, while `CUserLocal::Update` contains the local-player emotion reset path and action/state transitions that matter for expression and rare-action parity.

### 2. Player skill execution parity

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

### 3. Physics and movement parity

- `CVecCtrl::CollisionDetectFloat` at `0x994740`
- `CUser::SetMoveAction` at `0x8df540`
- `CUserLocal::SetMoveAction` at `0x903ce0`
- `CUserLocal::Update` at `0x937330`

Notes:
`CVecCtrl::CollisionDetectFloat` is the strongest resolved client seam for current float/swim/fly collision parity work. `CUserLocal::Update` also shows direct branching for flying-skill action state and rush/fall/teleport follow-up behavior.

### 4. Mob combat and encounter parity

- `CMob::DoAttack` at `0x6504d0`
- `CMob::OnNextAttack` at `0x6528a0`
- `CMob::ProcessAttack` at `0x652950`
- `CMob::GetAttackInfo` at `0x641330`

### 5. Field and portal parity

- `CPortalList::FindPortal_Hidden` at `0x6ab5d0`
- `CUserLocal::CheckPortal_Collision` at `0x919a10`
- `CUserLocal::TryDoingTeleport` at `0x932c00`
- `CUserLocal::TryPassiveTransferField` at `0x91a2e0`
- `CField::IsFearEffectOn` at `0x52a420`
- `CField::OffFearEffect` at `0x52b810`

Notes:
`CUserLocal::TryPassiveTransferField` shows that client field transfer is not only explicit portal collision handling; the local-player update loop also retries an up-key style transfer after one-time actions finish when the character is allowed to move.

### 6. UI parity

- `CUIStatusBar::Draw` at `0x876cd0`
- `CUIStatusBar::SetStatusValue` at `0x873590`
- `CUIStatusBar::SetNumberValue` at `0x873d50`
- `CUIStatusBar::ChatLogAdd` at `0x87aec0`
- `CUIStatusBar::CQuickSlot::Draw` at `0x875750`
- `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` at `0x871000`
- `CUISkill::Draw` at `0x84ed90`
- `CUISkill::GetSkillRootVisible` at `0x84a6f0`
- `CUISkill::GetRecommendSKill` at `0x84e710`

Notes:
`CUIStatusBar::Draw` confirms that name/job-or-level selection, HP/MP/EXP number updates, and quick-slot redraw are part of a single status-bar draw pass, while `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` shows the client also revalidates slot contents against learned skills and inventory state before redraw. `CUISkill::Draw` confirms visible skill-root selection, recommend-skill highlighting, bonus-SP rendering, skill icon row layout, and skill-level text placement.

### 7. Interaction and NPC parity

- `CUserLocal::TalkToNpc` at `0x9321f0`
- `CUserLocal::CheckReactor_Collision` at `0x903d20`
- `CUserLocal::TryAutoRequestFollowCharacter` at `0x904bf0`
- `CUserLocal::TryLeaveDirectionMode` at `0x9054c0`
- `CUIStatusBar::ChatLogAdd` at `0x87aec0`
- `CUser::PetAutoSpeaking` at `0x8deb10`

Notes:
`CUserLocal::TalkToNpc` is the first concrete client seam for future NPC bridge work. `CUserLocal::Update` also shows client-side quest-alert refresh calls, emotion reset / balloon lifetime upkeep, timer refresh, timed direction-mode release, and status-bar chat logging, while `CUserLocal::TryAutoRequestFollowCharacter` covers automatic escort/follow requests and `CUser::PetAutoSpeaking` covers one of the idle-feedback surfaces tied to those broader interaction loops.

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

### 1. Avatar and player parity

These are the biggest remaining gaps for visible client parity.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Default avatar selection | Current defaults are hardcoded SuperGM-style presets with specific item IDs, not validated against the client's actual startup/default selection path | The simulator loads a valid avatar, but not necessarily the same avatar the client would choose | `CharacterLoader.LoadDefaultMale`, `CharacterLoader.LoadDefaultFemale` (`CUser::LoadLayer`, `CUser::Update`) |
| Implemented | Action coverage | Character-part loading now preserves the existing core action order and appends additional WZ action nodes discovered on each part instead of stopping at the old fixed lists | Uncommon body/equipment poses can now load when the source IMG exposes them, which closes the loader-side empty-state gap for rare, scripted, and death-adjacent actions | `CharacterLoader.StandardActions`, `CharacterLoader.AttackActions` (`CUser::PrepareActionLayer`, `CUser::SetMoveAction`, `CUserLocal::SetMoveAction`) |
| Implemented | Death / rare action frames | Runtime action selection now carries raw one-shot action names through to `CharacterAssembler` instead of forcing uncommon actions back through the limited enum-only render path | Loaded death and other rare WZ actions can now survive from selection to final frame assembly instead of degrading immediately to generic fallbacks | `PlayerCharacter`, `CharacterLoader`, `CharacterAssembler` |
| Implemented | Facial expression behavior | The local player runtime now owns blink cadence and short-lived hit expressions, and updates the assembler expression state when those transitions occur | The avatar now reacts over time instead of staying pinned to the static default face | `CharacterAssembler.GetFaceFrame`, `CharacterLoader.LoadFaceExpressions`, `PlayerCharacter` (`CUserLocal::Update` emotion reset path) |
| Implemented | Equip anchor fallback rules | Equipment alignment now resolves through explicit shared map-point precedence instead of using literal pixel nudges when preferred anchors are missing | Parts stay render-safe while remaining tied to WZ anchor data rather than ad hoc offsets | `CharacterAssembler.CalculateEquipOffset`, `CharacterAssembler.TryCalculateHeadEquipOffset` |
| Implemented | Client-verified fallback order | Action lookup now prefers exact action names, then raw-action family reductions, then swim/fly aliasing, before the final `stand1` escape hatch | Character assembly now follows a documented action-family precedence order instead of jumping straight from miss to generic standing frames | `CharacterAssembler`, `CharacterPart.FindAnimation` (`CUser::LoadLayer`, `CUser::PrepareActionLayer`, `CUser::SetMoveAction`) |
| Partial | Mount / vehicle / mechanic mode rendering | `TamingMob` assembly now preserves exact ride frames for normal mounts, falls back to seated passenger variants when the asset only exposes `sit`, and equips ride parts for skills that publish an `eventTamingMob` mount id, but the simulator still lacks the client's broader mechanic-mode vehicle routing beyond those skill-backed ride buffs | Event ride buffs now transition the avatar onto a real `TamingMob` asset instead of leaving the player visually unmounted, but broader special vehicle and mechanic activation parity is still incomplete | `PlayerCharacter`, `CharacterLoader`, `CharacterAssembler`, `SkillLoader`, `SkillManager` (`Character/TamingMob/01902000`, `Character/TamingMob/01983000`, `Character/TamingMob/01932017`, `Skill/8000.img/80001045/eventTamingMob`, `CAvatar::MoveAction2RawAction`, `CUser::Update`) |
| Partial | Skill-specific avatar transforms | Mechanic-mode skills now activate dedicated avatar action families (`tank_*`, `siege_*`, `tank_siege*`) from their v115 WZ `action/0` nodes, play the corresponding `_after` exit actions when those transforms clear, and the prepare-skill flamethrower family now stays on its `flamethrower` / `flamethrower2` avatar branches until release triggers `flamethrower_after` / `flamethrower_after2`, but other class-specific transform families are still missing | Mechanic and flamethrower skills no longer snap straight back to generic standing/attack presentation at transform clear or prepare-skill release, but broader transform parity is still incomplete | `SkillManager`, `PlayerCharacter.TriggerSkillAnimation`, `CharacterAssembler` (`Skill/3500.img/35001001`, `Skill/3510.img/35101009`, `Skill/3512.img/35121005`, `Skill/3511.img/35111004`, `Skill/3512.img/35121013`, `CAvatar::MoveAction2RawAction`) |
| Missing | Client-managed avatar effect layers | The client still maintains additional avatar and under-face layers for effects such as `More Wild`, double-jump, sudden-death, final-cut, flying-wing, swallowing, and related ride/oak-cask cleanup in `CUser::Update`, but the simulator does not model those overlay/effect-layer lifecycles beyond a narrow mechanic-transform slice | Visible client presentation still diverges even when the base avatar action is correct because these extra effect layers change what is drawn, how it is anchored, and when it is cleaned up | `PlayerCharacter`, `CharacterAssembler`, effect loaders (`CUser::Update`, `CUser::UpdateMoreWildEffect`) |

### 2. Player skill execution parity

The simulator has a generic skill runtime. It does not yet mirror the client's named `CUserLocal` behavior families.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Generic skill casting | The simulator now loads the full player skill catalog from `Skill.wz`, learns active skills into the runtime, supports direct cast invocation from the skill window in addition to hotkey casting, keeps GM/SuperGM skill-book resolution compatible with both `900.img` and `910.img` layouts while focusing those jobs on their own job-book window instead of the global advancement catalog, evaluates Big Bang `common` formulas in IMG-mode data sets so skills like `Power Strike` are treated as castable attacks instead of passive placeholders, mirrors skill effect anchoring/hitbox fallback by facing direction for asymmetric melee effects, sanitizes unsupported SpriteFont glyphs in the skill window so WZ-backed names no longer crash row rendering, and resolves cast SFX from `Sound/Skill.img` so skill use now plays the corresponding `Use` or fallback attack sound through the simulator audio path | This closes the previous "can only use the current job's limited skill book" runtime/UI gap and keeps special-job skill tabs populated and executable across client data variants | `SkillManager.cs`, `SkillLoader.cs`, `SkillUIBigBang.cs`, `MapSimulator.cs`, `UIWindowLoader.cs` |
| Implemented | Buff stat application | Buff logic now applies `PAD`, `PDD`, `MAD`, `MDD`, `ACC`, `EVA`, `Speed`, and `Jump` directly to the player build and cleanly removes them when buffs expire or refresh | Active buff skills now change simulator stats and movement-relevant values instead of only a partial subset | `SkillManager.ApplyBuffStats` |
| Partial | Buff icon / temporary-stat UI parity | Active buffs now drive a WZ-backed status-bar tray with live duration countdowns sourced from `UI/BuffIcon.img`, but the simulator still lacks client-confirmed ordering and right-click cancel interaction driven by the live buff list | Buff-heavy jobs are now visible on the HUD, but exact temporary-stat behavior is still not fully client-parity | `SkillManager.ActiveBuffs`, `SkillManager.OnBuffApplied`, `SkillManager.OnBuffExpired`, `StatusBarUI.cs`, `UILoader.cs` (`CUIStatusBar::Draw`, `CUserLocal::Update`) |
| Partial | Melee / ranged / magic resolution | The runtime now preserves WZ `lt/rb` attack bounds, resolves melee and magic targets with separate ordering rules, gives projectile skills family-specific collision ordering plus homing steering, and respects the effective projectile hit cap from both `level/mobCount` and `ball/mobCount`, but it still does not mirror every client-only branch or projectile behavior one-for-one | Skills are more consistent with WZ data and attack family shape, but hit timing and edge-case behavior are still not fully client-perfect | `SkillManager.ProcessMeleeAttack`, `ProcessMagicAttack`, `CheckProjectileCollisions` (`CUserLocal::DoActiveSkill_MeleeAttack`, `CUserLocal::DoActiveSkill_ShootAttack`, `CUserLocal::DoActiveSkill_MagicAttack`) |
| Partial | `DoActiveSkill_*` family parity | Generic movement, summon, and prepare/charge families now execute, and movement-family dispatch now separates teleport snaps, grounded rush, flying-rush glide, and jump-rush launches from client-confirmed `TryDoingTeleport` / `TryDoingRush` / `TryDoingFlyingRush` seams, but the simulator still does not mirror every named client branch such as job-specific bound jumps, meso explosion, smoke shell, open gate, or admin-only rules one-for-one | The largest remaining gap is now fidelity of per-family behavior rather than total lack of execution support | `SkillManager`, `PlayerCharacter`, `PlayerCombat` (`CUserLocal::DoActiveSkill_*` family) |
| Missing | Queued follow-up attack families | The client still runs deferred `TryDoingFinalAttack`, `TryDoingSerialAttack`, and `TryDoingSparkAttack` branches after the initiating attack resolves, with weapon-type gating and stored skill or target context, and the simulator has no equivalent queued follow-up pipeline | Passive procs and chained attack families still collapse into a single generic cast path, which changes both timing and hit behavior | `SkillManager`, `PlayerCombat` (`CUserLocal::TryDoingFinalAttack`, `CUserLocal::TryDoingSerialAttack`, `CUserLocal::TryDoingSparkAttack`) |
| Partial | Repeat-skill sustain families | `CUserLocal::TryDoingRepeatSkill` still manages repeat-skill state for mechanic sustain skills such as siege/tank/SG-88, including key-down bar setup, summon assist toggles, timeout-driven effect requests, and mode handoff; the simulator only covers a subset of that behavior through generic prepared skills plus a few mechanic transforms | Mechanic sustain skills still diverge in timing, teardown, and summon interaction even though the visible transform baseline is now present | `SkillManager`, `PlayerCharacter` (`CUserLocal::TryDoingRepeatSkill`) |
| Missing | Bound-jump, rocket-booster end, and smoothing moving-shoot branches | The client still routes rocket-booster startup into `DoActiveSkill_BoundJump` through `TryDoingRocketBooster`, lands into a follow-up melee/effect teardown path in `TryDoingRocketBoosterEnd`, and separately drives moving-fire behavior in `TryDoingSmoothingMovingShootAttack`, while the simulator currently stops at teleport/rush/flying-rush movement families | Several movement-coupled attack skills still lack the client's traversal, landing, and firing cadence | `SkillManager`, `PlayerCharacter` (`CUserLocal::TryDoingRocketBooster`, `CUserLocal::TryDoingRocketBoosterEnd`, `CUserLocal::TryDoingSmoothingMovingShootAttack`) |
| Partial | Key-down / charge skills | The simulator now has a generic prepare/charge flow with release handling plus a WZ-backed status-bar key-down gauge sourced from `UI/Basic.img/KeyDownBar*`, but it still lacks the client's full branch-specific timing, skin selection, and per-skill rules | Charge-family skills can execute and now expose visible hold progress, but the presentation and timing semantics are still simplified vs client behavior | `SkillManager`, `StatusBarUI.cs`, `UILoader.cs` (`CUserLocal::DoActiveSkill_Prepare`, `CUserLocal::TryDoingPreparedSkill`, `CUserLocal::DrawKeyDownBar`) |
| Missing | Swallow, mine, cyclone, and sit-down healing branches | `CUserLocal::Update` still invokes dedicated handlers for swallow buff/wriggle, mine deployment, cyclone, and sit-down healing, none of which are represented in the simulator's current skill runtime | A real slice of class- and state-specific active-skill behavior is still absent even though the generic cast path exists | `SkillManager`, `PlayerCharacter` (`CUserLocal::TryDoingSwallowBuff`, `CUserLocal::TryDoingSwallowMobWriggle`, `CUserLocal::TryDoingMine`, `CUserLocal::TryDoingCyclone`, `CUserLocal::TryDoingSitdownHealing`) |
| Partial | Summon simulation | Summon-family skills now create a generic summon lifecycle with duration, rendering, summon attack-branch playback, periodic nearby attacks, deferred mob removal during summon attack resolution, registration into the simulator's puppet/aggro system, client-backed movement-style buckets for stationary emplacement, grounded leash, owner-hover, and anchor-based roaming summons, and owner ladder/rope attack gating with the client-confirmed octopus / self-destruct exemptions, but they still do not reproduce per-skill summon attack logic, foothold settling, or full aggro behavior one-for-one | Summon-heavy jobs are now usable, more runtime-stable, and visually closer to client summon behavior, but they are still not client-parity | `SkillManager`, `SkillLoader.cs`, `SummonMovementResolver.cs`, `MobPool.cs`, entity/pool layer (`CUserLocal::DoActiveSkill_Summon`, `CSummoned::Init`, `CVecCtrlSummoned::WorkUpdateActive`, `CSummoned::TryDoingAttack`) |
| Partial | Skill cooldown UI parity | QuickSlot overlays now show live remaining-seconds countdown text per assigned cooling-down skill, and the v115 skill window once again routes icon drag-and-drop into the instantiated quick-slot bar, but there is still no full client-like status-bar cooldown tray or broader per-skill feedback system | Cooling-down skills are more readable during normal play and hotkey assignment is reachable again, but the simulator still lacks the full client cooldown HUD surface | `QuickSlotUI.cs`, `SkillUI.cs`, `SkillUIBigBang.cs`, `UIWindowManager.cs`, `StatusBarUI.cs` |
| Missing | Client-timer skill expiry and cancellation | `CUserLocal::UpdateClientTimer` still tracks per-skill client timers and emits cancel requests when they expire, and the simulator has no equivalent timer-driven cancellation surface | Skills with client-managed expiry semantics can linger or end at the wrong time | `SkillManager` (`CUserLocal::UpdateClientTimer`) |
| Partial | Skill restrictions by map/state | The simulator now enforces parsed map `fieldLimit` bits for `Unable_To_Use_Skill` and `Move_Skill_Only` through the shared skill runtime, but broader client-only forbidden-skill branches and transient field-state checks are still incomplete | Maps that explicitly suppress skills now block the same broad skill families in the simulator, but the full `CUserLocal` / `CField` gating surface is still not modeled | `MapSimulator`, `SkillManager`, `FieldSkillRestrictionEvaluator.cs`, `MapInfo.fieldLimit`, `FieldLimitType` (`CUserLocal::Update`, `CField::IsUnableToUseSkill`, `CField::IsMoveSkillOnly`) |

Buff icon plan:

1. Confirm asset sources and ordering:
   Use `UI/BuffIcon.img` for temporary-stat icon chrome and `Skill/*/icon` for per-skill icon sourcing where the client uses the skill's own icon, then validate row ordering and refresh cadence against `CUIStatusBar::Draw` and `CUserLocal::Update`.
2. Add a runtime-to-UI snapshot:
   Expose a small status-bar model from `SkillManager` that includes `skillId`, applied stat family, start time, duration, and refresh/replace semantics so the UI does not need to inspect buff internals directly.
3. Extend status-bar loading and drawing:
   Teach `UILoader` and `StatusBarUI` to load buff-slot art, render the active icon row in the client-aligned status-bar region, and reserve the interaction seam for later right-click cancel behavior.
4. Verify state synchronization:
   Check short-duration buffs, long-duration buffs, same-skill refresh, natural expiry, and `/job` cleanup so icon removal stays synchronized with the runtime and does not fight quick-slot cooldown overlays.

### 3. Physics and movement parity

This is no longer a blank area, but a refinement area.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Ladder / rope lookup | `CVecCtrl` now owns the ladder/rope lookup seam and resolves ladder metadata for grab/re-grab paths instead of exposing a stub helper | Core climb behavior now routes through the same vector-controller seam the client uses | `CVecCtrl.cs`, `PlayerCharacter.SetLadderLookup` (`CUserLocal::SetMoveAction`, `CUser::SetMoveAction`) |
| Implemented | Float collision | `CVecCtrl::CollisionDetectFloat` now uses the configured foothold lookup plus saved float-state crossing checks to land on footholds during swim/fly motion instead of carrying a TODO block | Swim/fly landing and platform-contact behavior now runs inside the vector controller instead of a partial fallback path | `CVecCtrl.cs` (`CVecCtrl::CollisionDetectFloat`) |
| Partial | Floating-map parity | The simulator now applies both `fly` and `needSkillForFly` map attributes to the player physics controller, but broader client-only map/skill float branches still remain outside the runtime | Flying maps that require a skill gate now behave closer to the client, but float behavior is still not fully parity-complete | `MapSimulator.cs`, `PlayerManager.cs`, `PlayerCharacter`, `CVecCtrl` (`CVecCtrl::CollisionDetectFloat`, `CUserLocal::Update`) |
| Partial | Movement path parity | The player runtime now seeds and timestamps `CMovePath`-style elements from the live game clock, preserves duration/facing in queued path entries, and exposes passive-position plus flushed move-path snapshots from `PlayerCharacter`, but encode/decode and broader client-faithful movement replay are still missing | The simulator now has a concrete movement-sync surface instead of dormant controller-only helpers, but it is still not a full client networking model | `PlayerCharacter.cs`, `CVecCtrl.cs`, broader movement serialization layer |
| Partial | Dynamic foothold passenger sync | Moving platforms and the transport deck now feed synthetic footholds back into the player and grounded-mob movement seams, but transport heuristics and fuller map-specific passenger cases are still incomplete | Passengers now stay attached to moving foothold surfaces through the normal physics path, but full transport/platform parity still needs refinement | `PassengerSyncController.cs`, `DynamicFoothold.cs`, `TransportationField.cs`, `MapSimulator.cs` |

### 4. Mob combat and encounter parity

The simulator has working mob AI and effects, but not the full client combat pipeline.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present, and direct melee attacks now use the same delayed action scheduler as projectile and ground-warning families using WZ `attackAfter` timing instead of pure frame polling | Good baseline, but the loop is still simplified vs client `CMob` behavior because special attack branches, summon routing, and richer transition rules remain missing | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `CombatEffects.cs` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, and `tremble` attack flags through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, and triggers tremble feedback from flagged impacts | Attack timing and reaction behavior are closer to the client, but `CMob::DoAttack` / `ProcessAttack` target routing, summon hit handling, and other branch-specific side effects still diverge materially | `MobAI.cs`, `MobAttackSystem.cs` (`CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |
| Partial | Full mob stat/status parity | Mob statuses now keep concrete temporary-stat entries with expiry metadata, tick values, and reset events, poison/venom/burn deal runtime DoT damage, and active status flags tint affected mobs in-world for visible feedback | Status-heavy encounters are more legible and the runtime is closer to a real `OnStatSet`/`OnStatReset` pipeline, but skill-driven stat application coverage and the full client temporary-stat surface still lack fidelity | `MobAI.cs`, effect loaders |
| Partial | Special mob interactions | WZ `selfDestruction` mobs now trigger reserved bomb/time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, escort/friendly mobs no longer aggro the player, escort spawn points do not auto-respawn, and special-death/escort kills suppress reward drops | This covers the first client-facing slice of escort and bomb encounters, but swallow routing, mob-vs-mob protection logic, escort progression/index behavior, and advanced rage/anger boss scripts are still missing | `MobAI.cs`, `MobPool.cs`, `MapSimulator.cs`, field systems |

### 5. Field and portal parity

The old backlog understated what is already present here, but there is still real work left.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Hidden portals | Reveal, hide, alpha fade, and collision gating are implemented | Remove from missing backlog | `PortalPool.cs` (`CPortalList::FindPortal_Hidden`) |
| Implemented | Portal property accessors | `PH`, `PSH`, and `PV` equivalents are implemented | Remove from missing backlog | `PortalPool.cs` |
| Implemented | Portal interaction parity | Standard and hidden portal triggering now share the same same-map delay seam as client-confirmed portal teleports, and Mechanic `Open Portal: GX-9` casts now place WZ-backed temporary linked gates that resolve on `Up` interaction with the client-like 120 ms same-map handoff; town portal / mystic door remains tracked separately below | Portal travel now covers the main visible, hidden, and same-map skill-portal branches instead of reducing open-gate travel to unsupported or generic transitions | `PortalPool.cs`, `MapSimulator.cs`, `TemporaryPortalField.cs` (`CUserLocal::CheckPortal_Collision`, `CUserLocal::TryRegisterTeleport`, `Skill/3510.img/skill/35101005`) |
| Missing | Passive transfer-field triggers | `CUserLocal::TryPassiveTransferField` still retries an up-key style field transfer after one-time actions finish when the local player is allowed to move, and the simulator has no equivalent passive handoff path | Passive transfer maps and scripted transitions can stall or require manual input where the client advances automatically | `MapSimulator.cs`, portal/field transition layer (`CUserLocal::TryPassiveTransferField`) |
| Partial | Transportation fields | Ship and Balrog transport handling exists, but player-on-ship sync is still explicitly unfinished | Visual field logic is ahead of gameplay sync | `TransportationField.cs` |
| Partial | Dynamic objects / quest layers | Older notes listed these as missing; they still do not appear as a consolidated parity system in the current simulator surface | Event maps and quest-driven field presentation remain incomplete | map/field load path |
| Missing | Town portal / mystic door lifecycle | Client-side create, enter, restore, and position flows are not implemented | Important movement utility remains absent | field and portal layer |
| Missing | Field-specific restrictions and effects | Skill restrictions, allowed item lists, session/party values, and several field timers/messages remain absent | Event maps and boss maps still lack client rules | `MapSimulator.cs`, field systems (`CField::IsFearEffectOn`, `CField::OffFearEffect`) |

### 6. UI parity

This area is more advanced than older parity notes suggested, but still far from complete client parity.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Status bar layout | Positions are informed by client analysis, bitmap/WZ-backed rendering exists for gauges and text, the overlay now stays anchored to the composed `StatusBar2.img/mainBar` frame, the level label now uses the client `mainBar/lvNumber` digits, the name shadow matches the client's diagonal passes more closely, and the EXP label now shows live raw EXP plus percentage instead of only a percentage stub | Strong base, but job-text font/color selection and the remaining status-bar draw details are still not a full `CUIStatusBar` clone | `StatusBarUI.cs`, `UILoader.cs` (`CUIStatusBar::Draw`, `CUIStatusBar::SetStatusValue`, `CUIStatusBar::SetNumberValue`, `UI/StatusBar2.img/mainBar/lvNumber`) |
| Partial | Skill UI layout | Row height, positions, hit boxes, SP placement, quick-slot assignment wiring, and a client-backed `Basic.img/VScr` scrollbar at the confirmed `153,93,155` placement are now present, with the Big Bang hover tooltip reusing the client `tip0..tip2` frames and the scrollbar supporting arrow clicks, track paging, thumb dragging, and wheel-over-scrollbar input, but the window still lacks recommendation logic and full skill-up behavior | The window is usable and closer to the client than the old notes imply, but it is still not a full `CUISkill` clone | `SkillUI.cs`, `SkillUIBigBang.cs`, `UIWindowLoader.cs` (`CUISkill::Draw`, `CUISkill::OnCreate`) |
| Partial | Quick slot cooldown overlay | Cooldown masking now uses the client `UIWindow2.img/Skill/main/CoolTime` frame family and remaining-seconds countdown text still renders on assigned quick-slot skills | Surface-level parity exists without the client's full cooldown tray and feedback behavior | `QuickSlotUI.cs` (`CUIStatusBar::CQuickSlot::Draw`) |
| Partial | Quick-slot validation and quantity sync | The simulator now rejects passive, hidden, and unlearned skills at hotkey assignment time, revalidates saved quick-slot bindings after preset restore, and lazily prunes stale skill slots during redraw and hotkey lookup, but it still lacks the client's inventory-backed recount for consumable and cash-item entries from `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` | Stale skill assignments no longer survive job swaps and state changes, but item quantity parity is still blocked on the missing simulator inventory model | `QuickSlotUI.cs`, `SkillManager.cs`, `CharacterConfig.cs` (`CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo`) |
| Partial | HP/MP warning flash | HP/MP gauge warnings now replay the client `aniHPGauge` / `aniMPGauge` and `hpFlash` / `mpFlash` assets for 500 ms when the resource drops further while already below the simulator's current 20% threshold default, but the underlying client-configurable warning thresholds are not yet exposed in the simulator UI | The client-visible low-resource cue now exists, but settings parity is still incomplete | `StatusBarUI.cs`, `UILoader.cs` (`CUIStatusBar::Draw`, `CUIStatusBar::SetNumberValue`) |
| Partial | Chat log / whisper flows | The Big Bang status-bar chat strip now renders wrapped recent chat lines from the simulator chat history, cycles the `chatTarget` badge through the client WZ labels, and supports `/w` + `/r` whisper targeting with an on-bar prompt, but full client message typing modes, scroll behavior, and exact chat-log color/layout rules are still missing | The status-bar chat surface now exists, but it is still short of a full client chat log and whisper workflow clone | `MapSimulatorChat.cs`, `StatusBarChatUI.cs`, `UILoader.cs` (`CUIStatusBar::ChatLogAdd`) |
| Partial | Full skill UI behavior | The Big Bang skill window now mirrors `CUISkill::GetSkillRootVisible` at the tab layer by only exposing the visible skill-book path for the current job, and the list now behaves more like a real skill pane by keeping keyboard-selected entries in view while accepting pane-hover wheel scrolling plus `Up` / `Down` / `PageUp` / `PageDown` / `Home` / `End` navigation, but skill-up actions, guide flow, and recommendation logic are still missing | The simulator now respects the client's visible-root job path and has a less brittle list-navigation surface, but the rest of the `CUISkill` behavior remains incomplete | `SkillUI.cs`, `SkillUIBigBang.cs`, `UIWindowLoader.cs` (`CUISkill::Draw`, `CUISkill::GetSkillRootVisible`, `CUISkill::GetRecommendSKill`) |
| Partial | Item/skill tooltip behavior | Skill hover tooltips now reuse the client `UIWindow2.img/Skill/main/tip0..tip2` frames in the skill window, quick-slot skill hovers, and status-bar buff tray, but item tooltips and broader cross-UI parity are still incomplete | The simulator now has a shared client-style skill-tooltip surface across the main skill HUDs, but it still lacks the rest of the client's tooltip behavior | `SkillUIBigBang.cs`, `QuickSlotUI.cs`, `StatusBarUI.cs`, `UILoader.cs`, `MapSimulator.cs` |
| Partial | Charge bars and richer skill HUD | The status bar now renders a WZ-backed key-down/charge gauge for prepared skills, applies the client-confirmed `KeyDownBar1` / `KeyDownBar4` HUD branches for the supported skill families, uses the client `get_max_gauge_time` timing map to drive the bar fill for those keydown skills, and shows a live skill-name plus ready/charge label above the bar, but broader branch-specific HUD states are still incomplete | Prepared/keydown skills now surface closer client timing and HUD feedback instead of a single generic bar, but the simulator still lacks the rest of the client's richer skill-state UI | `StatusBarUI.cs`, `UILoader.cs` (`CUserLocal::DrawKeyDownBar`, `get_max_gauge_time`) |

### 7. Interaction and NPC parity

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | NPC talk flow | Nearby NPC clicks now open a real simulator dialogue overlay fed from loaded NPC string data instead of stopping at hover-only cursor feedback, but quest/script branching and client-grade conversation layouts are still missing | Core interaction now exists, which closes the old hover-only gap even though the simulator still lacks the full script bridge | player/NPC/UI layer (`CUserLocal::TalkToNpc`) |
| Partial | Quest lists and quest-only actions | Nearby NPC overlays now list WZ-backed available, locked, in-progress, and completable quests, keep accept/completion state plus quest mob counts for the simulator session, and apply common quest actions such as EXP rewards, next-quest unlock notices, and quest-state mutations, but inventory-backed item requirements/rewards and full script branching are still incomplete | Progression-facing NPC parity now has a usable simulator baseline even though full script and inventory parity are still missing | NPC/UI layer |
| Missing | Auto-follow and escort follow requests | `CUserLocal::TryAutoRequestFollowCharacter` still checks driver proximity, vertical alignment, and traversability before sending a follow-character request, and the simulator has no equivalent follow handshake | Escort-style maps and character-follow interactions cannot reproduce the client's automatic attach behavior | `PlayerCharacter`, field/party interaction layer (`CUserLocal::TryAutoRequestFollowCharacter`) |
| Partial | Direction-mode release timing | NPC dialogue overlays now enter a simulator direction-mode lock that blocks local gameplay input without dropping into free-camera mode, and closing those scripted overlays releases control on a delayed timer modeled after `CUserLocal::TryLeaveDirectionMode`, but broader one-time action and field-script call sites are still missing | Scripted UI sequences now stop leaking movement/combat input and hand control back on a client-like delay, even though the rest of the script producers still need to wire into the same lifecycle | `GameStateManager.cs`, `MapSimulator.cs` (`CUserLocal::TryLeaveDirectionMode`) |
| Partial | Reactor interaction parity | Local player movement now runs reactor touch checks through `ReactorPool`, and reactor visuals now load full WZ-defined direct frame sequences plus sparse numeric state branches instead of collapsing many reactors to `0/0` or hard-falling back to state `0`, but skill reactors, moving reactors, and richer event-script handling are still incomplete | Environmental interaction now covers the basic client touch-reactor path and more of the WZ reactor animation surface, even though more specialized reactor branches are still missing | `ReactorPool.cs`, `ReactorItem.cs`, `MapSimulator.cs`, `EffectLoader.cs` (`CUserLocal::CheckReactor_Collision`) |
| Partial | Quest-alert, balloon, and idle social feedback loops | NPCs with available, in-progress, or completable quests now render the client-backed `QuestIcon` alert art above their heads based on the simulator quest runtime, and quest accept/complete actions now raise a short-lived in-world feedback balloon above the speaking NPC, but broader balloon lifetimes, emotion reset timing, and idle pet auto-speech are still not modeled | The simulator now surfaces both static quest-state alerts and a first timed NPC feedback balloon in-world instead of hiding all progression feedback inside the dialogue overlay alone, while the broader social/balloon update loop still remains incomplete | `QuestRuntimeManager.cs`, `MapSimulator.cs`, `UIWindow*.img/QuestIcon` (`CUserLocal::Update`, `CUser::PetAutoSpeaking`) |

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
