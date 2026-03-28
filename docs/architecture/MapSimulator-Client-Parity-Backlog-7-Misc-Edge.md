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
- `CField::OnFieldSpecificData` at `0x52a7e0`
- `CField::OnSetQuestTime` at `0x52b790`
- `CField::OnSetQuestClear` at `0x52c870`
- `CField::OnDesc` at `0x5313d0`
- `CField::OnSetObjectState` at `0x539890`
- `CField::OnPacket` at `0x546d50`
- `CScriptMan::OnPacket` at `0x6de360`
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

### 2. Scripted field-state and quest-timer parity

- `CField::OnPacket` at `0x546d50`
- `CField::OnFieldSpecificData` at `0x52a7e0`
- `CField::OnDesc` at `0x5313d0`
- `CField::OnSetQuestClear` at `0x52c870`
- `CField::OnSetQuestTime` at `0x52b790`
- `CField::OnSetObjectState` at `0x539890`
- `CScriptMan::OnPacket` at `0x6de360`

Notes:
This packet cluster is a distinct client owner that the current backlog did not call out directly. `CField::OnPacket` dispatches field-specific data, help-message dialogs, quest-timer refresh and clear, and object-state flips from one place, while `CScriptMan::OnPacket` owns script-message delivery separately from NPC click handling or reactor collision.

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
| Partial | NPC talk flow | Nearby NPC clicks now open a real simulator dialogue overlay fed from loaded NPC string data including NPC `info/speak` idle lines, quest `Say.img` conversations preserve multi-page WZ ordering, root-level `yes`/`no` branches are now attached to the final numbered page, quest entries now open directly on the authored WZ conversation pages instead of always forcing a synthetic summary page first, common MapleStory script markup is stripped into readable text with additional `#q`, `#s`, `#m`, `#z`, `#v`, mob-name `#o`, quest-reference `#y`, and quest-selected `#M` or `#a` or `#x` token cleanup backed by loaded quest and mob data where possible, standalone client prompt markers such as `#E` and `#I` are now stripped from read-only simulator text, inline `#L` menu options now surface as clickable read-only choices that honor WZ `stop/<page>/answer` metadata so only authored correct answers continue along the numbered conversation flow while wrong answers stay on their WZ stop branches when present, quest dialogue entries now also reuse named WZ `stop/*` and `lost` branches such as `item`, `mob`, `monster`, `quest`, `default`, `info`, and lost-item fallback text instead of flattening those failure states into generic simulator prose, and started quests now also surface WZ `stop/npc` guidance when you revisit the starter NPC instead of falling back to generic progress text, but true script execution, broader automatic branch conditions beyond the current WZ stop-branch mapping, richer dynamic quest-script variable evaluation beyond the current WZ-backed token fallbacks, and full client conversation layouts are still missing | Core interaction now covers a closer read-only version of client conversation flow, including authored page-first quest conversations, multi-step selection menus that now respect WZ-authored answer metadata instead of advancing on every missing branch, more of the client’s WZ-backed failure and return-to-NPC text, and cleaner MapleStory token rendering across mob, selected-quest, quest-reference, and stray prompt markers, even though the simulator still lacks the full script bridge, wider conditional branch evaluation, deeper dynamic script variable resolution, and broader client conversation window parity | player/NPC/UI layer (`CUserLocal::TalkToNpc`) |
| Partial | Quest lists and quest-only actions | Nearby NPC overlays now list WZ-backed available, locked, in-progress, and completable quests, keep accept/completion state plus quest mob counts, picked-up quest item counts, and quest action item grants/consumes for the simulator session, gate startable quests on `Check.img/0/item` requirements, and now also mirror additional common quest gates from WZ by surfacing `Check.img/0/pop`, trait-min requirements such as `charismaMin` or `craftMin`, `Check.img/0/skill` acquire gates, and `Check.img/1/endmeso` costs through locked quest rows, NPC quest pages, quest detail requirement lines, completion progress, and accept or complete actions, while common quest actions now also apply `Act.img/skill` skill-level grants, `Act.img/sp` per-session SP rewards for the current job tab, trait EXP rewards from `Act.img/*EXP`, plus EXP, fame, next-quest notices, linked quest-state mutations, WZ `Act.img/item/*/resignRemove` cleanup on give-up, and reward-item `jobEx` gating for common explorer subjob branches such as Dual Blade and Cannoneer, with quest reward detail text now surfacing WZ choice or random markers plus job or `jobEx` filters instead of flattening every reward row to a plain item name, but full script branching, master-level or broader skill-book style quest rewards, interactive item-selection rewards, and wider inventory-backed quest parity are still incomplete | Progression-facing NPC parity now has a broader WZ-backed simulator baseline for both start and completion requirements plus common reward mutations, so fame, trait, skill-gate, skill-grant, SP, meso-cost, item, chained-quest state feedback, give-up item cleanup, and common explorer subjob reward filtering stay in sync across the NPC overlay and quest window while the remaining gap is narrowed to script-driven branches, master-level or more specialized skill rewards, player-driven reward selection UI, and the wider inventory model | NPC/UI layer |
| Partial | Auto-follow and escort follow requests | Escort mobs now mirror the client-side `TryAutoRequestFollowCharacter` gate with the verified `|dx| <= 80` and `|dy| <= 30` attach window, grounded-only initial attach, traversable foothold-path checks, the WZ-backed `Map.img/info/nofollowCharacter` suppression flag, and the simulator-owned local-user blockers that map cleanly onto the client request seam today, including seated, mob-status immovable (stun or freeze), skill-transform immovable, and mounted states before the auto-follow request can attach; `UnitTest_MapSimulator` now covers the attach window, mounted, seated, movement-locked suppression, map-level follow suppression, and the short airborne carry window, but remote character-to-character follow requests plus their request-throttle/failure-message flow are still not simulated because the field runtime still has no remote-user layer | Escort-style encounters now reject the client-invalid midair, map-disabled, or local-user-invalid auto-attach cases, respect the same proximity gate the client uses before issuing a follow request, preserve follow through short jump arcs instead of dropping immediately on takeoff, and now also refuse new follow attachment while the local user is hard-immobilized by player mob statuses, while broader party follow handshakes still need a simulated remote-character layer before the remaining request packet timing and failure surfaces can be mirrored | `PlayerCharacter`, `MobItem.cs`, `EscortFollowController.cs`, field interaction layer (`CUserLocal::TryAutoRequestFollowCharacter`) |
| Partial | Direction-mode release timing | NPC dialogue overlays now enter a simulator direction-mode lock that blocks local gameplay input without dropping into free-camera mode, closing those scripted overlays releases control on a delayed timer modeled after `CUserLocal::TryLeaveDirectionMode`, the overlay-dismissal click is now consumed so it cannot leak into same-frame NPC or portal interactions before the delay expires, special-field scripted dialogs are advanced before world interaction handlers so wedding ceremony field dialogs can claim that same blocking owner on the very frame they appear instead of leaking one frame of movement or click-through before the delayed-release path engages, and the delayed-release path now uses an explicit scripted-owner registry instead of only a fixed visible-window list so the tracked NPC or scripted launch points feed it directly for Cash Shop or MTS preview, trunk, item maker, MapleTV, memo send and package dialogs, the social-room preview surfaces for miniroom, player shop, hired merchant, and trading-room, NPC-owned item-upgrade launches and their Vega Spell child window when that child is opened from the already locked upgrade flow, explicit special-field owner tokens for the wedding dialog and memory game blocker, and inherited utility or community windows opened while a scripted lock is already active such as memo inbox, guild BBS, party or guild search, guild skill, family chart or tree, and messenger, but other future script-launched utility surfaces can still miss the delayed-release owner path if they bypass that inherited-owner helper | Scripted UI sequences now keep their dismiss or expiry transitions inside the scripted lifecycle instead of accidentally re-triggering world interactions while control is still supposed to be locked, and this pass moves the modal ownership closer to the client seam by making both the active scripted field runtimes and the remaining in-flow utility or community launches explicitly hold direction mode instead of relying only on a broad visibility sweep; the remaining gap is narrowed to future scripted utility surfaces that still need to opt into the same inherited owner helper | `GameStateManager.cs`, `DirectionModeWindowOwnerRegistry.cs`, `MapSimulator.cs`, `SpecialFieldRuntimeCoordinator.cs`, `SpecialEffectFields.cs` (`CUserLocal::TryLeaveDirectionMode`) |
| Partial | Reactor interaction parity | Local player movement now runs reactor touch checks through `ReactorPool`, throttles the local touch sweep to the client's once-per-second cadence from `CUserLocal::CheckReactor_Collision`, reactor visuals now load full WZ-defined direct frame sequences plus sparse numeric state branches instead of collapsing many reactors to `0/0` or hard-falling back to state `0`, runtime reactor activation now resolves `info/reactorType` back into touch, hit, skill, item, or animation-only handling instead of collapsing most non-quest reactors to touch, the non-touch branch stays wired into the simulator's actual player attack surfaces by forwarding world-space melee or magic hitboxes, projectile impacts and explosion areas, summon strike areas, and the basic no-skill attack fallback into bounded reactor hit checks with left-versus-right hit gating for directional reactor types, item-backed reactors now only activate from matching inventory use near the local player and can consume the matching item even when the simulator has no richer local effect for that item yet, quest-backed reactors now bind directly to the simulator quest runtime so they enter or reset their active state as quest requirements change, reactor runtime state progression now keeps the real WZ state ids and per-state frame durations alive during activation or follow-up hits instead of collapsing back to synthetic `1/2/3/4` states or destroying multi-state hit reactors after the first post-activation strike, and reactors with WZ `info/script` names now feed the simulator's existing dynamic object-tag publication seam through the same alias resolver used for field-entry scripts so script-backed tagged objects can turn on when those reactors activate and turn back off when the reactor resets, destroys, or respawns, but moving reactors and script behaviors that need a fuller named event bridge than object-tag publication are still incomplete | Environmental interaction now covers the client-like touch-reactor path, its collision polling cadence, more of the WZ reactor animation surface, WZ-backed touch versus hit versus skill versus item versus quest activation selection, local inventory-driven item reactor use, quest-state-driven reactor refresh, and a first pass of script-backed object-state publishing for tagged map objects driven by reactor `info/script`; the remaining gap is narrowed to moving-reactor motion plus richer named script or field-event execution beyond those existing dynamic-tag toggles | `ReactorPool.cs`, `ReactorItem.cs`, `MapSimulator.cs`, `EffectLoader.cs`, `PlayerManager.cs`, `SkillManager.cs`, `ReactorScriptStatePublisher.cs`, `UnitTest_MapSimulator/ReactorScriptStatePublisherTests.cs` (`CUserLocal::CheckReactor_Collision`) |
| Partial | Quest-alert, balloon, and idle social feedback loops | NPCs with available, in-progress, or completable quests now render the client-backed `QuestIcon` alert art above their heads based on the simulator quest runtime, quest accept/complete actions now queue timed in-world feedback balloons above the speaking NPC instead of collapsing to a single fixed message while also holding the NPC in `speak` until the active balloon expires, nearby NPCs with WZ `info/speak/*` idle lines now periodically surface those lines through the same in-world balloon path, and pets now mirror the client-backed `PetAutoSpeaking` surfaces for level-up, pre-level-up, idle `e_rest`, low-HP alert, and HP/MP potion failure speech with the same six WZ-backed `String/PetDialog.img` event keys that exist in this data set; USE-item clicks or hotkeys now parse WZ `Item/Consume/*/spec` recovery fields including numeric-string `hpR`/`mpR` values seen in this data set, HP/MP potion failure speech is throttled to the client-shaped first-few repeats instead of firing forever, and zero-count USE hotkey bindings now persist across config reloads instead of being dropped when the stack is empty, but broader non-`PetAutoSpeaking` specialist pet chatter call sites and pet-care subsystems are still not modeled | The simulator now surfaces both static quest-state alerts and closer client-like NPC and pet idle feedback loops in-world instead of hiding all progression feedback inside the dialogue overlay alone, and this pass closes one remaining WZ-read mismatch on consume recovery by honoring numeric-string recovery ratios in `Item/Consume/*/spec`; the remaining gap is narrowed to chatter that lives outside the client’s six `PetAutoSpeaking` events plus deeper pet-care/runtime ownership that would have to originate those other call sites | `QuestRuntimeManager.cs`, `NpcFeedbackBalloonQueue.cs`, `NpcItem.cs`, `LifeLoader.cs`, `PetLoader.cs`, `PetController.cs`, `MapSimulator.cs`, `UIWindow*.img/QuestIcon`, `String/PetDialog.img` (`CUserLocal::Update`, `CUser::PetAutoSpeaking`) |

### 2. Scripted field-state and quest-timer parity

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Missing | Field-owned script/message and quest-timer packet parity | A broader IDA pass surfaced a client-owned coordination layer that is not tracked as its own backlog area today. `CField::OnPacket` dispatches `OnFieldSpecificData` (`149`), `OnDesc` (`162`), `OnSetQuestClear` (`166`), `OnSetQuestTime` (`167`), and `OnSetObjectState` (`169`), while `CScriptMan::OnPacket` separately routes script messages (`363`) into `OnScriptMessage`. The simulator already mirrors pieces of the downstream consequences through NPC overlays, quest lists, direction-mode ownership, and tagged-object publication, but it still has no dedicated packet-authored runtime for field help messages, quest-timer start/end windows, quest-timer clear resets, field-specific data handoff, or named object-state flips sourced from the actual client dispatch seam | Map scripts and event fields are not driven only by NPC clicks or reactor collisions. Without this packet-owned bridge, parity work keeps re-implementing isolated outcomes while missing the client seam that coordinates post-entry scripted help dialogs, timed quest deadlines, quest-clear resets, and `CMapLoadable::SetObjectState` updates for object-tag and map-state changes | field/script runtime bridge (`CField::OnPacket`, `CField::OnFieldSpecificData`, `CField::OnDesc`, `CField::OnSetQuestClear`, `CField::OnSetQuestTime`, `CField::OnSetObjectState`, `CScriptMan::OnPacket`) |

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
6. Scripted field-state pass:
   Add the packet-owned bridge for field help messages, quest timers, quest-clear resets, and object-state flips before layering more one-off script behavior onto NPC or reactor seams.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
