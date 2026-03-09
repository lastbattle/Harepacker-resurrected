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
- `CUserLocal::Update` at `0x937330`
- `CUserLocal::SetMoveAction` at `0x903ce0`
- `CUserLocal::TryDoingPreparedSkill` at `0x944270`
- `CUserLocal::DrawKeyDownBar` at `0x9153b0`
- `CUserLocal::CheckPortal_Collision` at `0x919a10`
- `CUserLocal::CheckReactor_Collision` at `0x903d20`
- `CUserLocal::TryDoingTeleport` at `0x932c00`
- `CUserLocal::TryDoingRush` at `0x90b8c0`
- `CUserLocal::TryDoingFlyingRush` at `0x90bc10`
- `CUserLocal::TalkToNpc` at `0x9321f0`
- `CUser::LoadLayer` at `0x8e96d0`
- `CUser::PrepareActionLayer` at `0x8e3070`
- `CUser::SetMoveAction` at `0x8df540`
- `CUser::Update` at `0x8fb8d0`
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

## Client Function Index By Backlog Area

The list below maps each backlog area to concrete client seams confirmed in IDA.
These are the first functions to inspect before changing simulator behavior.

### 1. Avatar and player parity

- `CUser::LoadLayer` at `0x8e96d0`
- `CUser::PrepareActionLayer` at `0x8e3070`
- `CUser::SetMoveAction` at `0x8df540`
- `CUser::Update` at `0x8fb8d0`
- `CUserLocal::SetMoveAction` at `0x903ce0`
- `CUserLocal::Update` at `0x937330`

Notes:
`CUser::LoadLayer` and `CUser::PrepareActionLayer` are the clearest resolved avatar-composition seams for layer loading and action-layer setup. `CUserLocal::Update` also contains the local-player emotion reset path and action/state transitions that matter for expression and rare-action parity.

### 2. Player skill execution parity

- `CUserLocal::DoActiveSkill_MeleeAttack` at `0x93b210`
- `CUserLocal::DoActiveSkill_ShootAttack` at `0x93b520`
- `CUserLocal::DoActiveSkill_MagicAttack` at `0x93a010`
- `CUserLocal::DoActiveSkill_Prepare` at `0x941710`
- `CUserLocal::TryDoingPreparedSkill` at `0x944270`
- `CUserLocal::DrawKeyDownBar` at `0x9153b0`
- `CUserLocal::TryDoingTeleport` at `0x932c00`
- `CUserLocal::TryDoingRush` at `0x90b8c0`
- `CUserLocal::TryDoingFlyingRush` at `0x90bc10`

Notes:
`CUserLocal::Update` at `0x937330` orchestrates several of these paths and confirms that key-down gauge updates, prepared-skill execution, teleport, rush, flying-rush, repeated-skill handling, and status-bar feedback are coordinated in one local-player update loop rather than isolated one-off handlers.

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
- `CField::IsFearEffectOn` at `0x52a420`
- `CField::OffFearEffect` at `0x52b810`

### 6. UI parity

- `CUIStatusBar::Draw` at `0x876cd0`
- `CUIStatusBar::SetStatusValue` at `0x873590`
- `CUIStatusBar::SetNumberValue` at `0x873d50`
- `CUIStatusBar::ChatLogAdd` at `0x87aec0`
- `CUIStatusBar::CQuickSlot::Draw` at `0x875750`
- `CUISkill::Draw` at `0x84ed90`
- `CUISkill::GetSkillRootVisible` at `0x84a6f0`
- `CUISkill::GetRecommendSKill` at `0x84e710`

Notes:
`CUIStatusBar::Draw` confirms that name/job-or-level selection, HP/MP/EXP number updates, and quick-slot redraw are part of a single status-bar draw pass. `CUISkill::Draw` confirms visible skill-root selection, recommend-skill highlighting, bonus-SP rendering, skill icon row layout, and skill-level text placement.

### 7. Interaction and NPC parity

- `CUserLocal::TalkToNpc` at `0x9321f0`
- `CUserLocal::CheckReactor_Collision` at `0x903d20`
- `CUIStatusBar::ChatLogAdd` at `0x87aec0`

Notes:
`CUserLocal::TalkToNpc` is the first concrete client seam for future NPC bridge work. `CUserLocal::Update` also shows client-side quest-alert refresh calls and status-bar chat logging, which matters when interaction work expands into quest/UI parity.

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

## Corrections To The Old Documents

The following items were previously tracked as missing, or were too stale to keep as backlog items:

| Area | Old claim | Actual state |
|------|-----------|--------------|
| Player character | No player character simulation | Implemented across `PlayerCharacter`, `PlayerManager`, `CharacterLoader`, `CharacterAssembler`, `PlayerCombat`, `PlayerInput` |
| Hidden portals | `FindPortal_Hidden`, `UpdateHiddenPortal`, `SetHiddenPortal` missing | Implemented in `PortalPool.cs` |
| Portal properties | `GetPropPH`, `GetPropPSH`, `GetPropPV` missing | Implemented in `PortalPool.cs` |
| Pet loot | `TryPickUpDropByPet` missing | Implemented in `DropPool.cs` |
| Damage numbers | `ShowDamage` missing | Implemented, including WZ-backed digit rendering in `CombatEffects.cs` and `DamageNumberRenderer.cs` |
| Boss HP bar | HP indicator missing | Implemented with both regular mob HP bars and boss HP bars in `CombatEffects.cs` and `BossHPBarUI.cs` |
| Skill UI constants | Generic TODO only | Layout constants were already updated from client analysis in `SkillUI.cs` |
| Special fields | Snowball, Coconut, Wedding, GuildBoss listed as not implemented | Implemented in `MinigameFields.cs` and `SpecialEffectFields.cs` |

## Latest Parity Confirmation

Confirmed changes since last pass:

- `Boss HP bar` ? Implemented (both regular and boss HP bars).
- `Damage numbers` (`ShowDamage`) ? Implemented with WZ-backed digit rendering.
- `Ladder / rope lookup` ? Partial; core behavior works, but `CVecCtrl` still has stub/lookup callback coupling.
- `Floating-map parity` ? Partial; swim/fly states exist, but client-specific map/skill float behavior is incomplete.
- `HP indicators` ? Implemented for regular mob and boss HP bars.
- `Player skill catalog / invocation` ? Implemented for simulator use: the skill window now loads the full `Skill.wz` player-skill catalog grouped by advancement tab, active skills are learned into the runtime, and selected skills can be cast directly from the skill window.
- `Runtime job swapping` ? Implemented for simulator debugging: `/job <jobId>` now updates the live player job label, reloads the runtime skill set for the normal advancement chain of that job (for example `412` => `0/400/410/411/412`) while keeping GM/SuperGM on their focused admin book behavior, refreshes the skill window to the same path-aware view without restarting the simulator, and clears active old-job projectiles/casts/buffs so stale transient skill state does not survive the swap and crash later updates.

## Remaining Parity Backlog

### 1. Avatar and player parity

These are the biggest remaining gaps for visible client parity.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Default avatar selection | Current defaults are hardcoded SuperGM-style presets with specific item IDs, not validated against the client's actual startup/default selection path | The simulator loads a valid avatar, but not necessarily the same avatar the client would choose | `CharacterLoader.LoadDefaultMale`, `CharacterLoader.LoadDefaultFemale` (`CUser::LoadLayer`, `CUser::Update`) |
| Partial | Action coverage | Loader action lists cover common stand, walk, jump, ladder, rope, swim, fly, alert, heal, and attack actions, but not the full client action surface | Missing actions force fallback rendering or empty states during uncommon skills, mounts, or scripted actions | `CharacterLoader.StandardActions`, `CharacterLoader.AttackActions` (`CUser::PrepareActionLayer`, `CUser::SetMoveAction`, `CUserLocal::SetMoveAction`) |
| Partial | Death / rare action frames | `PlayerCharacter` can enter `Dead`, but loader coverage is centered on standard and attack actions rather than a complete action inventory | Some state-to-animation mappings can degrade to fallback frames | `PlayerCharacter.GetCurrentAction`, `CharacterLoader` |
| Partial | Facial expression behavior | Face data loads, but runtime selection is essentially `default` then `blink`; no client-like blink timing, hit-expression timing, or state-driven expression changes | The avatar renders, but feels static compared with the client | `CharacterAssembler.GetFaceFrame`, `CharacterLoader.LoadFaceExpressions` (`CUserLocal::Update` emotion reset path) |
| Partial | Equip anchor fallback rules | Some alignment paths still fall back to approximate anchors such as `navel + offset` for weapons or `brow` for earrings when `ear` is missing | These fallbacks keep rendering alive but are not client-parity rules | `CharacterAssembler.CalculateEquipOffset`, `CharacterAssembler.TryCalculateHeadEquipOffset` |
| Missing | Client-verified fallback order | The current assembler uses pragmatic fallbacks, but not a documented client-confirmed precedence order for all part/action/frame combinations | This is where "mostly right" character assembly usually diverges from official behavior | `CharacterAssembler`, `CharacterPart.FindAnimation` (`CUser::LoadLayer`, `CUser::PrepareActionLayer`) |
| Missing | Mount / vehicle / mechanic mode rendering | No mount, vehicle passenger, or mechanic-specific avatar assembly path | Large class of player presentations is absent | `PlayerCharacter`, `CharacterLoader`, `CharacterAssembler` |
| Missing | Skill-specific avatar transforms | No class-specific assembly adjustments for skills that change body posture, effect layers, or weapon handling outside generic attack actions | Skill visuals remain generic even when the skill data loads | `SkillManager`, `PlayerCharacter.TriggerSkillAnimation`, `CharacterAssembler` |

### 2. Player skill execution parity

The simulator has a generic skill runtime. It does not yet mirror the client's named `CUserLocal` behavior families.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Generic skill casting | The simulator now loads the full player skill catalog from `Skill.wz`, learns active skills into the runtime, supports direct cast invocation from the skill window in addition to hotkey casting, keeps GM/SuperGM skill-book resolution compatible with both `900.img` and `910.img` layouts while focusing those jobs on their own job-book window instead of the global advancement catalog, evaluates Big Bang `common` formulas in IMG-mode data sets so skills like `Power Strike` are treated as castable attacks instead of passive placeholders, mirrors skill effect anchoring/hitbox fallback by facing direction for asymmetric melee effects, sanitizes unsupported SpriteFont glyphs in the skill window so WZ-backed names no longer crash row rendering, and resolves cast SFX from `Sound/Skill.img` so skill use now plays the corresponding `Use` or fallback attack sound through the simulator audio path | This closes the previous "can only use the current job's limited skill book" runtime/UI gap and keeps special-job skill tabs populated and executable across client data variants | `SkillManager.cs`, `SkillLoader.cs`, `SkillUIBigBang.cs`, `MapSimulator.cs`, `UIWindowLoader.cs` |
| Implemented | Buff stat application | Buff logic now applies `PAD`, `PDD`, `MAD`, `MDD`, `ACC`, `EVA`, `Speed`, and `Jump` directly to the player build and cleanly removes them when buffs expire or refresh | Active buff skills now change simulator stats and movement-relevant values instead of only a partial subset | `SkillManager.ApplyBuffStats` |
| Partial | Melee / ranged / magic resolution | The runtime distinguishes melee, projectile, and magic-oriented execution, but real client behavior is still collapsed into generic hitboxes, projectile flight, and simplified target selection | Skills are broadly usable, but their hit logic is still not client-perfect | `SkillManager.ProcessMeleeAttack`, `CheckProjectileCollisions` (`CUserLocal::DoActiveSkill_MeleeAttack`, `CUserLocal::DoActiveSkill_ShootAttack`, `CUserLocal::DoActiveSkill_MagicAttack`) |
| Partial | `DoActiveSkill_*` family parity | Generic movement, summon, and prepare/charge families now execute, but the simulator still does not mirror every named client branch such as job-specific bound jumps, meso explosion, smoke shell, open gate, or admin-only rules one-for-one | The largest remaining gap is now fidelity of per-family behavior rather than total lack of execution support | `SkillManager`, `PlayerCharacter`, `PlayerCombat` (`CUserLocal::DoActiveSkill_*` family) |
| Partial | Key-down / charge skills | The simulator now has a generic prepare/charge flow with release handling, but it still lacks the client's dedicated key-down UI bar and branch-specific timing rules | Charge-family skills can execute, but the presentation and some timing semantics are still simplified | `SkillManager`, UI layer (`CUserLocal::DoActiveSkill_Prepare`, `CUserLocal::TryDoingPreparedSkill`, `CUserLocal::DrawKeyDownBar`) |
| Partial | Summon simulation | Summon-family skills now create a generic summon lifecycle with duration, rendering, and periodic nearby attacks, including deferred mob removal during summon attack resolution so kills no longer invalidate active mob iteration, but they do not yet reproduce client follow/placement/aggro rules per summon type | Summon-heavy jobs are now usable and more runtime-stable, but not yet client-parity | `SkillManager`, entity/pool layer |
| Missing | Skill cooldown UI parity | QuickSlot overlays exist, but there is no full client-like status bar or per-skill feedback system | Cooling-down skills are only partially surfaced to the player | `QuickSlotUI.cs`, `StatusBarUI.cs` |
| Missing | Skill restrictions by map/state | Client functions such as forbidden-skill checks and map-state gating are not modeled | Skills can cast in cases where the client would block them | `MapSimulator`, `SkillManager` (`CUserLocal::Update`, `CField::IsFearEffectOn`, `CField::OffFearEffect`) |

### 3. Physics and movement parity

This is no longer a blank area, but a refinement area.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Ladder / rope lookup | `CVecCtrl` still exposes stub-level ladder lookup helpers and depends on external lookup callbacks | Core climb behavior works, but the vector controller is not self-contained like the client | `CVecCtrl.cs`, `PlayerCharacter.SetLadderLookup` (`CUserLocal::SetMoveAction`, `CUser::SetMoveAction`) |
| Partial | Float collision | There is still a TODO for foothold lookup during floating collision | Swim/fly collision edge cases can differ from client behavior | `CVecCtrl.cs` (`CVecCtrl::CollisionDetectFloat`) |
| Partial | Floating-map parity | Swim and fly states exist, but they remain generic float modes rather than map- or skill-specific client behaviors | The simulator supports movement without matching all map rules | `PlayerCharacter`, `CVecCtrl` (`CVecCtrl::CollisionDetectFloat`, `CUserLocal::Update`) |
| Missing | Movement path parity | The client's move-path encode/decode/passive-pos flow is still not modeled as a full parity target | Network-faithful movement replay and sync are incomplete | `CVecCtrl`, broader movement serialization layer |
| Missing | Dynamic foothold passenger sync | Dynamic footholds exist, but full player and mob sync with moving platforms is still listed as future work | Transport/platform maps still diverge under movement | `DynamicFoothold.cs`, `TransportationField.cs` |

### 4. Mob combat and encounter parity

The simulator has working mob AI and effects, but not the full client combat pipeline.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present | Good baseline, but still simplified vs client CMob behavior | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `CombatEffects.cs` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Missing | Full `CMob` action pipeline | The client's `DoAttack`, `OnNextAttack`, `ProcessAttack`, richer `GetAttackInfo`, and more complete move/action transitions are not mirrored one-for-one | Attack timing and reaction behavior can still diverge materially | `MobAI.cs`, `MobAttackSystem.cs` (`CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |
| Missing | Full mob stat/status parity | Some temporary stat and affected-skill infrastructure exists, but not the entire `OnStatSet`, `OnStatReset`, temporary-stat, and debuff-visual pipeline | Status-heavy encounters still lack fidelity | `MobAI.cs`, effect loaders |
| Missing | Special mob interactions | Escort, bomb/time-bomb, swallow, mob-vs-mob, and advanced boss rage/anger behaviors are still absent | Important encounter-specific behaviors are still unsupported | `MobAI.cs`, field systems |

### 5. Field and portal parity

The old backlog understated what is already present here, but there is still real work left.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Hidden portals | Reveal, hide, alpha fade, and collision gating are implemented | Remove from missing backlog | `PortalPool.cs` (`CPortalList::FindPortal_Hidden`) |
| Implemented | Portal property accessors | `PH`, `PSH`, and `PV` equivalents are implemented | Remove from missing backlog | `PortalPool.cs` |
| Partial | Portal interaction parity | Basic portal triggering and hidden portal behavior exist, but town portal, open gate, and more specialized client portal flows are still absent | Travel behavior is incomplete outside standard portals | `PortalPool.cs`, `MapSimulator.cs` (`CUserLocal::CheckPortal_Collision`, `CUserLocal::TryDoingTeleport`) |
| Partial | Transportation fields | Ship and Balrog transport handling exists, but player-on-ship sync is still explicitly unfinished | Visual field logic is ahead of gameplay sync | `TransportationField.cs` |
| Partial | Dynamic objects / quest layers | Older notes listed these as missing; they still do not appear as a consolidated parity system in the current simulator surface | Event maps and quest-driven field presentation remain incomplete | map/field load path |
| Missing | Town portal / mystic door lifecycle | Client-side create, enter, restore, and position flows are not implemented | Important movement utility remains absent | field and portal layer |
| Missing | Field-specific restrictions and effects | Skill restrictions, allowed item lists, session/party values, and several field timers/messages remain absent | Event maps and boss maps still lack client rules | `MapSimulator.cs`, field systems (`CField::IsFearEffectOn`, `CField::OffFearEffect`) |

### 6. UI parity

This area is more advanced than older parity notes suggested, but still far from complete client parity.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Status bar layout | Positions are informed by client analysis, and bitmap/WZ-backed rendering exists for gauges and text | Strong base, but not complete behavior parity | `StatusBarUI.cs` (`CUIStatusBar::Draw`, `CUIStatusBar::SetStatusValue`, `CUIStatusBar::SetNumberValue`) |
| Partial | Skill UI layout | Row height, positions, hit boxes, SP placement, drag-and-drop, and quick-slot integration are present | The window is usable and closer to the client than the old notes imply | `SkillUI.cs` (`CUISkill::Draw`) |
| Partial | Quick slot cooldown overlay | Basic cooldown masking exists | Surface-level parity exists without full client feedback behavior | `QuickSlotUI.cs` (`CUIStatusBar::CQuickSlot::Draw`) |
| Missing | HP/MP warning flash | No low-HP or low-MP flash behavior | Very visible missing client cue | `StatusBarUI.cs` (`CUIStatusBar::Draw`) |
| Missing | Chat log / whisper flows | No chat history drawing or whisper-target workflow | Status bar behavior is incomplete | `StatusBarUI.cs`, `StatusBarChatUI.cs` (`CUIStatusBar::ChatLogAdd`) |
| Missing | Full skill UI behavior | Skill-up actions, guide flow, scrollbar parity, recommendation logic, and root visibility handling are still missing | Current window is a layout approximation, not a full CUISkill clone | `SkillUI.cs` (`CUISkill::Draw`, `CUISkill::GetSkillRootVisible`, `CUISkill::GetRecommendSKill`) |
| Missing | Item/skill tooltip behavior | There are UI windows and drag operations, but not full client tooltip behavior across status bar and skill windows | UI still feels simulator-specific | UI layer |
| Missing | Charge bars and richer skill HUD | No equivalent to the client charge UI or richer skill state feedback | Blocks parity for prepare/keydown skills | UI layer (`CUserLocal::DrawKeyDownBar`) |

### 7. Interaction and NPC parity

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Missing | NPC talk flow | No real `TalkToNpc` flow or script bridge | Core interaction loop is absent | player/NPC/UI layer (`CUserLocal::TalkToNpc`) |
| Missing | Quest lists and quest-only actions | NPC quest visibility and accept/complete flows are not modeled | Progression-facing NPC parity is absent | NPC/UI layer |
| Missing | Reactor interaction parity | Reactor proximity, skill reactors, moving reactors, and richer state handling remain incomplete | Environmental interaction is still basic | `ReactorPool.cs` (`CUserLocal::CheckReactor_Collision`) |

## New Gaps Missing From The Old Notes

These were either not documented at all or were buried in older parity notes instead of being tracked as backlog.

1. Client-verified default avatar selection is still missing.
2. Face-expression runtime behavior is still far behind the client.
3. Action coverage is incomplete for less common avatar states.
4. Equip-anchor fallback rules are still approximate in edge cases.
5. The generic skill runtime exists, but the named `CUserLocal::DoActiveSkill_*` families are still mostly absent.
6. Buff application only updates a subset of stats.
7. Mount, vehicle, mechanic, and summon presentation are still missing.
8. Charge/prepare skills and their UI are still missing.
9. Movement and float collision still have explicit stub/TODO points in `CVecCtrl.cs`.

## Priority Order

If the goal is visible parity first, the next work should be sequenced like this:

1. Avatar parity pass:
   Validate default asset selection, face-expression timing, and action coverage against WZ and client behavior.
2. Skill execution pass:
   Implement named active-skill families rather than extending the generic cast path indefinitely.
3. UI feedback pass:
   Add HP/MP flash, charge bars, and complete skill-window behavior.
4. Movement refinement pass:
   Remove the remaining ladder/float stubs and tighten platform/dynamic foothold sync.
5. Interaction pass:
   Add NPC talk/quest flow and richer reactor interactions.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".


