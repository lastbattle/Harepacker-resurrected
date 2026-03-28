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
- `CAffectedAreaPool::OnPacket` at `0x438330`
- `CTownPortalPool::OnPacket` at `0x7636b0`
- `COpenGatePool::OnPacket` at `0x68c8b0`
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
- `CAffectedAreaPool::OnPacket` at `0x438330`
- `CTownPortalPool::OnPacket` at `0x7636b0`
- `COpenGatePool::OnPacket` at `0x68c8b0`
- `CField::IsFearEffectOn` at `0x52a420`
- `CField::OffFearEffect` at `0x52b810`

Notes:
`CUserLocal::TryPassiveTransferField` shows that client field transfer is not only explicit portal collision handling; the local-player update loop also retries an up-key style transfer after one-time actions finish when the character is allowed to move. A later IDA pass also shows that packet-authored field utility surfaces are split into their own remote pools rather than hanging off local collision handling: `CAffectedAreaPool::OnPacket` owns affected-area create/remove, `CTownPortalPool::OnPacket` owns town-portal create/remove, and `COpenGatePool::OnPacket` owns Open Gate create/remove.

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
| Partial | Dynamic objects / quest layers | Field object drawables now retain WZ `quest` visibility metadata, keep WZ `hide=1` object layers suppressed until a matching quest state or a published object-tag toggle explicitly reveals them, resolve WZ object `tags` through the dedicated published object-state seam instead of the obstacle bucket, ingest authored map-level tagged-object defaults from `publicTaggedObjectVisible` plus the client-data typo variant `pulbicTaggedObjectVisible`, including grouped multi-tag entries, auto-publish quest-owned layer tags from `QuestInfo.img/showLayerTag`, publish entry-owned map script aliases from `info/onUserEnter`, `info/onFirstUserEnter`, and `info/fieldScript` when their WZ script names resolve onto the current map's tagged-object set, now also load WZ `directionInfo/*/EventQ` trigger points and publish their resolved tags once the local player's hitbox reaches the authored `x`/`y` point, publish portal-owned script aliases when a portal is actually used, parse `Quest/Check.img` `startscript` / `endscript` names so successful NPC quest accept and complete actions publish their resolved tagged-object aliases through the same seam, and now also publish non-quest NPC aliases from `Npc/<id>.img/info/script/*/script` when the player actually opens that NPC interaction; staged `_01` / `_02` / `_03` style script aliases now retire sibling stage tags from the same camel-cased family when later entry, trigger, portal, quest, or NPC publications fire, but broader time-delayed or script-authored multi-step sequencing after map entry is still absent | Quest-driven and event-driven maps now respect quest state, quest-authored `showLayerTag` reveals, author-authored hidden-layer defaults, entry-script publication, WZ-authored `directionInfo/EventQ` trigger points, portal-script-owned tagged-layer publication, quest-owned NPC `startscript` / `endscript` publication, non-quest NPC `info/script` publication on talk, and WZ-authored staged tutorial/event script progression that swaps sibling stage tags through the same published-object seam instead of only ever accumulating visible stages; tagged event-map objects still fall back to authored map defaults when runtime overrides are cleared, generic object-state publication no longer has to masquerade as an obstacle toggle, and the remaining gap is narrowed to broader post-entry timer or script-driven sequencing beyond simple staged-family retirement | map/field load path, `MapSimulator.cs`, `QuestRuntimeManager.cs`, `FieldEffects.cs`, `FieldObjectTagStateDefaultsLoader.cs`, `FieldObjectScriptTagAliasResolver.cs`, `FieldObjectDirectionEventTriggerLoader.cs`, `FieldObjectNpcScriptNameResolver.cs` |
| Implemented | Town portal / mystic door lifecycle | `Mystic Door` skill casts now create a WZ-backed cross-map door pair, place the return-side door on the target town map's `TownPortalPoint` / `tp` spawn, transfer into town on `Up`, and restore the player to the original cast position when the town-side door is used before expiry | The simulator now covers the visible create, enter, restore, and positioning loop for the client's town-door utility instead of leaving the skill family nonfunctional | `TemporaryPortalField.cs`, `MapSimulator.cs`, portal/field transition layer (`Skill/231.img/2311002/{cDoor,mDoor,common/time}`, `PortalType.TownPortalPoint`) |
| Partial | Remote portal-pool parity | Packet-authored remote portal pools now exist in the simulator runtime instead of being folded into local skill ownership: `330/331` decode and apply remote town-portal create/remove keyed by owner character id, `332/333` decode and apply remote Open Gate create/remove keyed by owner plus first/second slot, packet-owned remote portals clear on field reload, remote Open Gates link and teleport through the existing same-map interaction seam, remote town portals render on their own pool, preserve their packet-authored state byte, add the WZ `Frame` overlay for the nonzero remote-door phase, route into the field's resolved town return target when that destination is available, synthesize the inferred town-side return door back into the original field when that field-side return target is known, cache owner-level field/town Mystic Door metadata so later town-side packets can reuse an earlier observed source-field return, and a `/portalpacket` debug seam now drives the pool with either raw packet payloads or typed create/remove arguments. Remote Mystic Door timing is also closer to `CTownPortalPool`: state-`0` create packets now stay in the WZ-backed `cDoor` opening run for the asset-authored 1700 ms before promoting to the steady door/frame pair, stable remote door loops now retain their own phase start instead of sharing a global zero-tick cycle, and state-`0` remove packets now snapshot the currently visible field/town door canvases plus the field-side `Frame` overlay into a 1000 ms fade instead of fading a freshly restarted animation. Remaining gap: the simulator still approximates the client's COM-layer `InsertCanvas` / `RemoveCanvas` ownership and one-time animation registration with drawable-level snapshots rather than reproducing the exact layer object choreography, and a remote Mystic Door that is only ever first observed from the town side still cannot recover the original-field return without any prior field-side metadata for that owner. | Visible parity no longer stops at self-authored portal utility: other users' Open Gates and packet-owned Mystic Doors now have their own runtime pool, the field-side remote door can hand off into town through the packet seam, inferred town-side doors can return to the original cast field without needing a second authored packet stream, later town-side packets can now recover a previously observed source field via owner metadata caching, and the remaining work is narrowed to exact client layer/canvas bookkeeping plus cases where no source-field metadata is ever observed for that owner. | field portal/runtime layer (`CTownPortalPool::OnPacket`, `COpenGatePool::OnPacket`, local reference seam: `CUserLocal::CheckPortal_Collision`) |
| Partial | Remote affected-area / mist parity | The simulator now has a real packet-owned `328/329` path for affected areas: a dedicated codec decodes create/remove payloads into the remote pool, `/affectedpacket` can drive the same raw-packet seam or build typed test payloads, packet create timing now honors the client packet's short start-delay field before the area becomes active, and a dedicated packet-runtime binder clears remote affected areas on field bind instead of depending on unrelated simulator call sites. Hostile gameplay is no longer purely render-only either: remote mob-skill mist packets now reapply mapped mob-skill statuses to the local player while contained, hostile remote player poison/burn-style tile zones now tick mob poison/burn status through the existing mob-status seam, client-confirmed `type=3` affected areas now route through their own packet source kind and load item-backed `darkFog` visuals instead of being misclassified as skill zones, invincible support zones now use real remote owner resolution against the simulator's remote-user roster plus party list so self-only and `massSpell` party protection stop shielding unrelated players, and recovery-style remote support zones that resolve through those same confirmed self-or-party ownership rules now tick real HP/MP healing onto the local player instead of staying visual-only. Remaining gap: broader non-poison/non-burn remote player zone semantics are still approximated rather than reconstructed from more complete client ownership rules, `type=3` fog lifetime still falls back to packet remove timing instead of client-confirmed area-buff-item duration metadata, and support-area targeting outside the currently confirmed self-or-party invincible/recovery branch still needs fuller packet or client evidence. | This closes the main transport and lifecycle gap by giving `CAffectedAreaPool` parity work the same packet-decoder and field-bind ownership shape as the other remote pools, while also pushing remote mist beyond visuals into actual hostile status resolution for the player and mobs, adding a dedicated area-buff-item fog path, and resolving the first real slice of owner-filtered support semantics through the existing remote-user and social-roster seams. Recovery-style support mists now also feed through the local-player healing seam instead of being ignored. The remaining work is narrower and more data-sensitive: fuller remote support-target classification, client-exact area-buff-item timing, and the rest of the client-specific affected-area semantics that still need deeper packet and ownership reconstruction. | field affected-area/runtime layer (`CAffectedAreaPool::OnPacket`, `CAffectedAreaPool::OnAffectedAreaCreated`, `CAffectedAreaPool::OnAffectedAreaRemoved`, local reference seam: `SkillManager`, packet-owned seam in `AffectedAreaPool`, zone/effect draw path) |
| Partial | Field-specific restrictions and effects | Map entry now surfaces `timeLimit`, `decHP`, `protectItem`, `allowedItem`, ambient `rain` / `snow`, `Unable_To_Jump`, `Unable_To_Use_Teleport_Item`, field-transfer, `Unable_To_Use_Rocket_Boost`, Mystic Door field-limit metadata, positive WZ `moveLimit` counts, and WZ-authored `onUserEnter` / `onFirstUserEnter` / `fieldScript` names through simulator notices, matching `rain` / `snow` map-info flags automatically start the simulator's ambient weather layer on field entry, `onFirstUserEnter` notices and tagged-object publication now only fire on the first visit to that map in the current simulator session while `onUserEnter` / `fieldScript` continue to publish every entry, blocked skill casts raise explicit field-limit feedback for both Rocket Booster and Mystic Door map bans, jump-disabled maps now reject jump input with the same field-rule messaging seam, cross-map portal travel respects `Unable_To_Migrate`, `decHP` maps deal periodic environmental damage with a mist cue unless the inventory currently holds a listed `protectItem`, WZ-authored positive `recovery` values now tick HP and MP recovery through the player's existing recover seam every 10 seconds, `timeLimit` maps warn before expiry then transfer to `returnMap` / `forcedReturn`, `allowedItem` maps now reject non-listed inventory-backed item consumption plus setup-chair activation through the same field-rule notice path, cash teleport-rock items now route into the simulator's map-transfer window unless `Unable_To_Use_Teleport_Item` blocks them, WZ-authored numeric `consumeItemCoolTime` values now enforce a shared consume-item cooldown through the same inventory guard seam, pending cross-map transfers now reject under-`lvLimit` characters plus maps marked `partyOnly` or `expeditionOnly` using the simulator's existing party-roster and expedition-search session state with explicit map-restriction notices, and positive `moveLimit` values now cap WZ-classified movement-skill casts through the same field-skill rejection seam, but actual field-script execution beyond these notice/publication effects, broader field-limit item or interaction bans outside the currently modeled seams, and server-authored admission state still remain unmodeled | Event and boss maps now expose the first real slice of client field-rule timing, movement locks, migration locks, hazard behavior, periodic recovery maps, ambient field weather, `protectItem` hazard suppression, `allowedItem` item-use gating, teleport-item map bans, Mechanic rocket-mobility bans, numeric consume cooldowns, movement-skill count caps from positive `moveLimit` values, first-visit-only `onFirstUserEnter` behavior, and basic level, party, and expedition entry gating, while generic entry-owned field scripts are still not executed beyond notices/tag publication, several other field-limit bans still are not enforced through concrete item or interaction seams, and deeper server/session admission checks are still absent | `MapSimulator.cs`, `FieldRuleRuntime.cs`, `FieldRuleEffectApplier.cs`, `FieldEntryRestrictionEvaluator.cs`, `FieldEnvironmentEffectEvaluator.cs`, `FieldSkillRestrictionEvaluator.cs`, `FieldInteractionRestrictionEvaluator.cs`, `TeleportItemUsageEvaluator.cs`, `UnitTest_MapSimulator/FieldRuleRuntimeParityTests.cs`, field systems (`CField::IsFearEffectOn`, `CField::OffFearEffect`) |

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
