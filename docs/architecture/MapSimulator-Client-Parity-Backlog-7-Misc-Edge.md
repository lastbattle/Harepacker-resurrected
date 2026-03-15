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

### 1. Interaction and NPC parity

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

### 1. Interaction and NPC parity

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | NPC talk flow | Nearby NPC clicks now open a real simulator dialogue overlay fed from loaded NPC string data including NPC `info/speak` idle lines, quest `Say.img` conversations preserve multi-page WZ ordering, root-level `yes`/`no` branches are now attached to the final numbered page, common MapleStory script markup is stripped into readable text, and inline `#L` menu options now surface as clickable read-only choices that route through WZ-backed `stop` answer branches when present or continue along the numbered conversation flow, but true script execution and full client conversation layouts are still missing | Core interaction now covers a much closer read-only version of client conversation flow, including multi-step selection menus and WZ-backed wrong-answer/failure text, even though the simulator still lacks the full script bridge and broader client conversation window parity | player/NPC/UI layer (`CUserLocal::TalkToNpc`) |
| Partial | Quest lists and quest-only actions | Nearby NPC overlays now list WZ-backed available, locked, in-progress, and completable quests, keep accept/completion state plus quest mob counts, picked-up quest item counts, and quest action item grants/consumes for the simulator session, now also gate startable quests on `Check.img/0/item` requirements so locked quest rows, quest detail requirements, and accept actions all reflect current held-item progress instead of ignoring start-item checks, apply common quest actions such as EXP rewards, fame rewards, next-quest unlock notices, and linked quest-state mutations, and reset stale mob progress when those quest actions auto-start or rewind follow-up quests, but full script branching and broader inventory/UI parity are still incomplete | Progression-facing NPC parity now has a usable simulator baseline with both start and completion quest-item requirement tracking plus cleaner chained-quest state handling, even though full script and inventory-system parity are still missing | NPC/UI layer |
| Partial | Auto-follow and escort follow requests | Escort mobs now mirror the client-side `TryAutoRequestFollowCharacter` gate with the verified `|dx| <= 80` and `|dy| <= 30` attach window, grounded-only initial attach, and traversable foothold-path checks, while existing escort follow still survives short hops by projecting against the player's carried foothold only inside a small fall-start foothold window; `UnitTest_MapSimulator` now locks those escort regressions down, but remote character-to-character follow requests are still not simulated | Escort-style encounters now reject the client-invalid midair auto-attach cases, respect the same proximity gate the client uses before issuing a follow request, and still preserve follow through short jump arcs instead of dropping immediately on takeoff, while broader party follow handshakes still need a simulated remote-character layer | `PlayerCharacter`, `MobItem.cs`, `EscortFollowController.cs`, field interaction layer (`CUserLocal::TryAutoRequestFollowCharacter`) |
| Partial | Direction-mode release timing | NPC dialogue overlays now enter a simulator direction-mode lock that blocks local gameplay input without dropping into free-camera mode, closing those scripted overlays releases control on a delayed timer modeled after `CUserLocal::TryLeaveDirectionMode`, the overlay-dismissal click is now consumed so it cannot leak into same-frame NPC or portal interactions before the delay expires, and special-field scripted dialogs are advanced before world interaction handlers so wedding ceremony field dialogs can claim that same blocking owner on the very frame they appear instead of leaking one frame of movement or click-through before the delayed-release path engages, but broader one-time action and field-script call sites are still missing | Scripted UI sequences now keep their dismiss or expiry transitions inside the scripted lifecycle instead of accidentally re-triggering world interactions while control is still supposed to be locked, even though the rest of the script producers still need to wire into the same lifecycle | `GameStateManager.cs`, `MapSimulator.cs`, `SpecialFieldRuntimeCoordinator.cs`, `SpecialEffectFields.cs` (`CUserLocal::TryLeaveDirectionMode`) |
| Partial | Reactor interaction parity | Local player movement now runs reactor touch checks through `ReactorPool`, throttles the local touch sweep to the client's once-per-second cadence from `CUserLocal::CheckReactor_Collision`, reactor visuals now load full WZ-defined direct frame sequences plus sparse numeric state branches instead of collapsing many reactors to `0/0` or hard-falling back to state `0`, runtime reactor activation preserves WZ `reactorType` metadata so touch-only collision sweeps stop misfiring skill, hit, or animation-only reactors, and that non-touch branch is now wired into the simulator's actual player attack surfaces by forwarding world-space melee or magic hitboxes, projectile impacts and explosion areas, summon strike areas, and the basic no-skill attack fallback into bounded reactor hit checks with left-versus-right hit gating for directional reactor types, but moving reactors and richer event-script handling are still incomplete | Environmental interaction now covers the client-like touch-reactor path, its collision polling cadence, more of the WZ reactor animation surface, and the first real end-to-end split between touch versus skill or hit reactor activation instead of leaving non-touch triggers as pool-only dead code, while directional hit reactors now stop responding from the wrong side, even though more specialized moving and scripted reactor branches are still missing | `ReactorPool.cs`, `ReactorItem.cs`, `MapSimulator.cs`, `EffectLoader.cs`, `PlayerManager.cs`, `SkillManager.cs` (`CUserLocal::CheckReactor_Collision`) |
| Partial | Quest-alert, balloon, and idle social feedback loops | NPCs with available, in-progress, or completable quests now render the client-backed `QuestIcon` alert art above their heads based on the simulator quest runtime, quest accept/complete actions now queue timed in-world feedback balloons above the speaking NPC instead of collapsing to a single fixed message while also holding the NPC in `speak` until the active balloon expires, nearby NPCs with WZ `info/speak/*` idle lines now periodically surface those lines through the same in-world balloon path, and pets now mirror the `CUser::Update` idle rest-alarm path by preferring structured `String/PetDialog.img` event keys such as `e_rest` when present while still falling back to legacy talk-chat lines, plus the simulator now surfaces the same WZ-backed `PetAutoSpeaking` near-level-up, level-change, and low-HP alert lines when those runtime conditions occur, but the potion-failure and broader effect-specific pet chatter call sites are still not modeled | The simulator now surfaces both static quest-state alerts and closer client-like NPC and pet idle feedback loops in-world instead of hiding all progression feedback inside the dialogue overlay alone, while the remaining pet auto-speaking gaps are narrowed to the potion-failure and other non-idle specialist triggers the current runtime still does not produce | `QuestRuntimeManager.cs`, `NpcFeedbackBalloonQueue.cs`, `NpcItem.cs`, `LifeLoader.cs`, `PetLoader.cs`, `PetController.cs`, `MapSimulator.cs`, `UIWindow*.img/QuestIcon`, `String/PetDialog.img` (`CUserLocal::Update`, `CUser::PetAutoSpeaking`) |

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
