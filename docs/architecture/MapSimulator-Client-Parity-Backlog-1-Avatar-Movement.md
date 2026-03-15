# MapSimulator Client Parity Backlog

## Purpose

This document is the single source of truth for MapSimulator parity work against the MapleStory client.

It does three things that the old notes did not do well:

1. Separates shipped work from missing work.
2. Distinguishes broad simulator features from player/avatar parity.
3. Anchors claims to actual code seams and, where relevant, client-side reverse engineering.

## Evidence Used

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
| Partial | Default avatar selection | The simulator now seeds beginner-level starter builds instead of hardcoded SuperGM/Wizet presets, but the exact client startup selection path and any version-specific randomization or validation are still not mirrored | The default avatar now starts from a much closer novice baseline, but it is still not guaranteed to match the client's true startup choice rules | `CharacterLoader.LoadDefaultMale`, `CharacterLoader.LoadDefaultFemale` (`CUser::LoadLayer`, `CUser::Update`) |
| Implemented | Action coverage | Character-part loading now preserves the existing core action order and appends additional WZ action nodes discovered on each part instead of stopping at the old fixed lists | Uncommon body/equipment poses can now load when the source IMG exposes them, which closes the loader-side empty-state gap for rare, scripted, and death-adjacent actions | `CharacterLoader.StandardActions`, `CharacterLoader.AttackActions` (`CUser::PrepareActionLayer`, `CUser::SetMoveAction`, `CUserLocal::SetMoveAction`) |
| Implemented | Death / rare action frames | Runtime action selection now carries raw one-shot action names through to `CharacterAssembler` instead of forcing uncommon actions back through the limited enum-only render path | Loaded death and other rare WZ actions can now survive from selection to final frame assembly instead of degrading immediately to generic fallbacks | `PlayerCharacter`, `CharacterLoader`, `CharacterAssembler` |
| Implemented | Facial expression behavior | The local player runtime now owns blink cadence and short-lived hit expressions, and updates the assembler expression state when those transitions occur | The avatar now reacts over time instead of staying pinned to the static default face | `CharacterAssembler.GetFaceFrame`, `CharacterLoader.LoadFaceExpressions`, `PlayerCharacter` (`CUserLocal::Update` emotion reset path) |
| Implemented | Equip anchor fallback rules | Equipment alignment now resolves through explicit shared map-point precedence instead of using literal pixel nudges when preferred anchors are missing | Parts stay render-safe while remaining tied to WZ anchor data rather than ad hoc offsets | `CharacterAssembler.CalculateEquipOffset`, `CharacterAssembler.TryCalculateHeadEquipOffset` |
| Implemented | Client-verified fallback order | Action lookup now prefers exact action names, then raw-action family reductions, then swim/fly aliasing, before the final `stand1` escape hatch | Character assembly now follows a documented action-family precedence order instead of jumping straight from miss to generic standing frames | `CharacterAssembler`, `CharacterPart.FindAnimation` (`CUser::LoadLayer`, `CUser::PrepareActionLayer`, `CUser::SetMoveAction`) |
| Partial | Mount / vehicle / mechanic mode rendering | `TamingMob` assembly now preserves exact ride frames for normal mounts, falls back to seated passenger variants when the asset only exposes `sit`, equips ride parts for skills that publish an `eventTamingMob` mount id, routes mechanic vehicle skills onto the shared ride layer while keeping that mount owned across both direct and timer-driven tank-to-siege return transitions, and now treats the shared Mechanic mount (`Character/TamingMob/01932016`) as the frame-timing and visibility owner for vehicle-only actions such as `tank_*`, `siege_*`, `flamethrower*`, `rbooster*`, and `tank_laser` so the human body/equipment stack no longer renders underneath the mech sprite, but the simulator still lacks the client's broader vehicle-routing surface outside those ride-backed cases | Event ride buffs and mechanic transforms now transition the avatar onto a real `TamingMob` asset instead of leaving the player visually unmounted, avoid dropping back to the previous ride too early when siege sustain expires on the same tick as buff cleanup, and stop double-rendering the base avatar under full-body Mechanic vehicle sprites, but broader special vehicle and mechanic activation parity is still incomplete | `PlayerCharacter`, `CharacterLoader`, `CharacterAssembler`, `SkillLoader`, `SkillManager` (`CAvatar::MoveAction2RawAction`, `CUser::Update`, `Character/TamingMob/01932016`) |
| Partial | Skill-specific avatar transforms | Mechanic-mode skills now activate dedicated avatar action families (`tank_*`, `siege_*`, `tank_siege*`) from `action/0` nodes, prefer transform-specific jump / ladder / rope / fly / swim / hit branches when those movement variants exist on the avatar, play the corresponding `_after` exit actions when those transforms clear, the prepare-skill flamethrower family now stays on its `flamethrower` / `flamethrower2` avatar branches until release triggers `flamethrower_after` / `flamethrower_after2`, Rocket Booster now transitions from its one-shot cast into the dedicated airborne `rbooster` avatar branch while preserving the mechanic tank-specific `tank_rbooster_pre` / `tank_rbooster_after` avatar variants when the booster is fired out of an active tank transform, and Cyclone now promotes its `cyclone_pre` cast into the persistent `cyclone` avatar branch until buff expiry triggers `cyclone_after`, but other class-specific transform families are still missing | Mechanic avatar movement now stays on transform-family motion frames when the loaded body actually exposes them instead of collapsing every non-walk state back to the stand pose, while flamethrower, Rocket Booster, and Cyclone still keep their transform-specific cast and teardown windows; broader transform parity remains incomplete | `SkillManager`, `PlayerCharacter.TriggerSkillAnimation`, `CharacterAssembler` (`32121003`, `35001001`, `35101009`, `35101004`, `35121005`, `35111004`, `35121013`, `CAvatar::MoveAction2RawAction`, `CUserLocal::TryDoingCyclone`) |
| Partial | Client-managed avatar effect layers | The simulator now loads persistent avatar-bound skill layer branches from WZ (`special`, `special0`, `finish`, `finish0`, ladder-aware `back` / `back_finish`, and `repeat` on `suddenDeath` skills), attaches them to the player render path as over-character / under-face layers, switches the `More Wild` family onto its ladder override, plays finish cleanup on buff expiry or cancellation, routes `doubleJump` action-family movement skills onto a client-style transient under-face avatar layer instead of the generic cast sprite, promotes fly/flying buff `effect` branches onto looping player-owned avatar layers so wing visuals stay attached to the avatar for the full buff window instead of rendering as detached cast sprites, and now also promotes invisible swallow buff `effect` branches such as `Skill/3310.img/33101006/effect` onto the same client-owned avatar layer path instead of leaving them as detached cast effects, but broader ride/oak-cask cleanup in `CUser::Update` still remains | Visible parity is closer for `More Wild`, `Final Cut`-style sudden-death buffs, double-jump movement skills, flying-wing buffs, and Wild Hunter swallow buffs because those extra layers now follow the avatar on the same lifecycle the client uses instead of staying entirely absent or drawing as detached cast effects, but the rest of the client's update-owned effect layer surface is still incomplete | `PlayerCharacter`, `SkillLoader`, `SkillManager`, `CharacterAssembler` (`CUser::Update`, `CUser::UpdateMoreWildEffect`, `CUserLocal::TryDoingSwallowBuff`, `Skill/3312.img/33121006/{special,special0,finish,finish0,back,back_finish}`, `Skill/434.img/4341002/repeat`, `Skill/310.img/3101003/effect`, `Skill/3300.img/33001002/effect`, `Skill/3310.img/33101006/effect`) |


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
