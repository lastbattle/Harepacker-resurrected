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

### 1. Field and portal parity

- `CPortalList::FindPortal_Hidden` at `0x6ab5d0`
- `CUserLocal::CheckPortal_Collision` at `0x919a10`
- `CUserLocal::TryDoingTeleport` at `0x932c00`
- `CUserLocal::TryPassiveTransferField` at `0x91a2e0`
- `CField::IsFearEffectOn` at `0x52a420`
- `CField::OffFearEffect` at `0x52b810`

Notes:
`CUserLocal::TryPassiveTransferField` shows that client field transfer is not only explicit portal collision handling; the local-player update loop also retries an up-key style transfer after one-time actions finish when the character is allowed to move.

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

### 1. Field and portal parity

The old backlog understated what is already present here, but there is still real work left.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Hidden portals | Reveal, hide, alpha fade, and collision gating are implemented | Remove from missing backlog | `PortalPool.cs` (`CPortalList::FindPortal_Hidden`) |
| Implemented | Portal property accessors | `PH`, `PSH`, and `PV` equivalents are implemented | Remove from missing backlog | `PortalPool.cs` |
| Implemented | Portal interaction parity | Standard and hidden portal triggering now share the same same-map delay seam as client-confirmed portal teleports, and Mechanic `Open Portal: GX-9` casts now place WZ-backed temporary linked gates that resolve on `Up` interaction with the client-like 120 ms same-map handoff; town portal / mystic door remains tracked separately below | Portal travel now covers the main visible, hidden, and same-map skill-portal branches instead of reducing open-gate travel to unsupported or generic transitions | `PortalPool.cs`, `MapSimulator.cs`, `TemporaryPortalField.cs` (`CUserLocal::CheckPortal_Collision`, `CUserLocal::TryRegisterTeleport`, `Skill/3510.img/skill/35101005`) |
| Implemented | Passive transfer-field triggers | Portal `Up` requests now latch for a short client-style retry window while one-time actions or temporary movement locks are active, then auto-replay the transfer attempt as soon as the local player can move again | Passive transfer maps and scripted transitions no longer require perfectly timed repeat input after short lockout actions before the field handoff occurs | `MapSimulator.cs`, portal/field transition layer (`CUserLocal::TryPassiveTransferField`) |
| Implemented | Transportation fields | Regular passenger ships now expose a dedicated transport-deck foothold that carries both the player and grounded mob passengers through the existing movement seam, while Balrog-only ship visuals no longer publish a false passenger deck | Transport maps now keep visible deck passengers riding the ship during departures and arrivals instead of leaving the transport system as a mostly visual-only layer | `TransportationField.cs`, `PassengerSyncController.cs`, `MapSimulator.cs` |
| Partial | Dynamic objects / quest layers | Field object drawables now retain WZ `quest` visibility metadata, preserve per-object `hide` flags for both quest-tagged and hide-only object layers, re-evaluate those combined rules against the simulator quest runtime, and now honor WZ object `tags` when the existing field-obstacle runtime publishes matching object-state toggles, but richer event-scripted sequencing and non-obstacle script families are still absent | Quest-driven maps now respect both quest state and author-authored hidden-layer defaults instead of treating hide-only object layers as permanently visible once loaded, and obstacle-backed event maps can finally hide or reveal tagged objects through the simulator's dynamic field-effect seam, while wider event-map presentation parity remains incomplete | map/field load path, `MapSimulator.cs`, `QuestRuntimeManager.cs`, `FieldEffects.cs` |
| Implemented | Town portal / mystic door lifecycle | `Mystic Door` skill casts now create a WZ-backed cross-map door pair, place the return-side door on the target town map's `TownPortalPoint` / `tp` spawn, transfer into town on `Up`, and restore the player to the original cast position when the town-side door is used before expiry | The simulator now covers the visible create, enter, restore, and positioning loop for the client's town-door utility instead of leaving the skill family nonfunctional | `TemporaryPortalField.cs`, `MapSimulator.cs`, portal/field transition layer (`Skill/231.img/2311002/{cDoor,mDoor,common/time}`, `PortalType.TownPortalPoint`) |
| Partial | Field-specific restrictions and effects | Map entry now surfaces `timeLimit`, `decHP`, `protectItem`, `allowedItem`, `Unable_To_Jump`, field-transfer, and Mystic Door field-limit metadata through simulator notices, blocked skill casts raise explicit field-limit feedback, jump-disabled maps now reject jump input with the same field-rule messaging seam, cross-map portal travel respects `Unable_To_Migrate`, `decHP` maps deal periodic environmental damage with a mist cue, and `timeLimit` maps warn before expiry then transfer to `returnMap` / `forcedReturn`, but session, party, inventory, and broader field-script enforcement are still not modeled | Event and boss maps now expose the first real slice of client field-rule timing, movement locks, migration locks, and hazard behavior instead of silently ignoring those map-info properties, even though wider rule enforcement is still incomplete | `MapSimulator.cs`, `FieldRuleRuntime.cs`, `FieldSkillRestrictionEvaluator.cs`, `FieldInteractionRestrictionEvaluator.cs`, field systems (`CField::IsFearEffectOn`, `CField::OffFearEffect`) |

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
