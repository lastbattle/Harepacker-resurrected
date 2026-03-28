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
- `CField::OnTransferFieldReqIgnored` at `0x52f3b0`
- `CField::OnTransferChannelReqIgnored` at `0x52f5f0`
- `CField::OnGroupMessage` at `0x535490`
- `CField::OnCoupleMessage` at `0x5357f0`
- `CField::OnFieldObstacleOnOff` at `0x535a80`
- `CField::OnFieldObstacleOnOffStatus` at `0x535b00`
- `CField::OnFieldEffect` at `0x53b790`
- `CField::OnWarnMessage` at `0x538160`
- `CField::OnPlayJukeBox` at `0x537940`
- `CField::OnWhisper` at `0x5448a0`
- `CField::OnSummonItemInavailable` at `0x52f7b0`
- `CField::OnDestroyClock` at `0x52a7c0`
- `CField::OnZakumTimer` at `0x530cc0`
- `CField::OnHontailTimer` at `0x530e70`
- `CField::OnChaosZakumTimer` at `0x531020`
- `CField::OnHontaleTimer` at `0x5311d0`
- `CField::OnPacket` at `0x546d50`
- `CScriptMan::OnPacket` at `0x6de360`
- `CUserLocal::OnFieldFadeOutForce` at `0x9057f0`
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

### 3. Packet-authored field messaging and feedback parity

- `CField::OnTransferFieldReqIgnored` at `0x52f3b0`
- `CField::OnTransferChannelReqIgnored` at `0x52f5f0`
- `CField::OnGroupMessage` at `0x535490`
- `CField::OnWhisper` at `0x5448a0`
- `CField::OnCoupleMessage` at `0x5357f0`
- `CField::OnSummonItemInavailable` at `0x52f7b0`
- `CField::OnFieldEffect` at `0x53b790`
- `CField::OnFieldObstacleOnOff` at `0x535a80`
- `CField::OnFieldObstacleOnOffStatus` at `0x535b00`
- `CField::OnFieldObstacleAllReset` at `0x52c830`
- `CField::OnPlayJukeBox` at `0x537940`
- `CField::OnWarnMessage` at `0x538160`
- `CField::OnDestroyClock` at `0x52a7c0`
- `CField::OnZakumTimer` at `0x530cc0`
- `CField::OnHontailTimer` at `0x530e70`
- `CField::OnChaosZakumTimer` at `0x531020`
- `CField::OnHontaleTimer` at `0x5311d0`
- `CUserLocal::OnFieldFadeOutForce` at `0x9057f0`

Notes:
This is another packet-owned surface that the current backlog set does not describe explicitly. IDA shows `CField::OnPacket` and `CUserLocal::OnPacket` owning a broad feedback layer that sits between ordinary gameplay and the existing UI rows: group, whisper, and couple message routing feed status-bar chat and whisper-candidate state; field-effect packets drive screen effects, field sounds, BGM changes, boss HP tags, reward-roulette visuals, and object-state changes; obstacle, warning, and forced-fade packets update map presentation directly; and dedicated boss-timer plus transfer-ignored handlers show that several visible notice/timer flows are owned by packets rather than generic field-rule polling.

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
| Partial | NPC talk flow | Nearby NPC clicks now open a real simulator dialogue overlay fed from loaded NPC string data including NPC `info/speak` idle lines, quest `Say.img` conversations preserve multi-page WZ ordering, root-level `yes`/`no` branches are now attached to the final numbered page, quest entries now open directly on the authored WZ conversation pages instead of always forcing a synthetic summary page first, common MapleStory script markup is stripped into readable text with additional `#q`, `#s`, `#m`, `#z`, `#v`, quest-item count `#c`, mob-name `#o`, quest-reference `#y`, quest-state `#u`, and quest-selected `#M` or `#a` or `#x` token cleanup backed by loaded quest, item, and mob data where possible, standalone client prompt markers such as `#E` and `#I` are now stripped from read-only simulator text, quest reward-summary markers such as `#Wbasic#`, `#Wselect#`, and `#Wprob#` are now stripped from simulator-visible text instead of leaking raw WZ tags into NPC quest pages, inline `#L` menu options now surface as clickable read-only choices that honor WZ `stop/<page>/answer` metadata so only authored correct answers continue along the numbered conversation flow while wrong answers stay on their WZ stop branches when present, quest dialogue entries now also reuse named WZ `stop/*` and `lost` branches such as `item`, `mob`, `monster`, `quest`, `default`, `info`, and lost-item fallback text instead of flattening those failure states into generic simulator prose, started quests now also surface WZ `stop/npc` guidance when you revisit the starter NPC instead of falling back to generic progress text, nested authored branch conversations reached through WZ `stop/*`, `yes`, `no`, or other branch nodes now reuse the same multi-page, final-page root-choice, and inline-selection parsing rules as top-level `Say.img` pages instead of dropping those branch-root choices, authored `stop` metadata no longer leaks into the overlay as a fake visible `Stop` button, and item-gated quest conversations now fall back to authored `lost` text whenever the specific `stop/item` branch is absent instead of only when every required item count has dropped to zero, but true script execution, broader automatic branch conditions beyond the current WZ stop-branch mapping, richer dynamic quest-script variable evaluation beyond the current WZ-backed token fallbacks, action or check-script conversation execution beyond these read-only WZ branches, and full client conversation layouts are still missing | Core interaction now covers a closer read-only version of client conversation flow, including authored page-first quest conversations, nested WZ branch conversations that now keep their own final-page root choices instead of flattening to plain text, multi-step selection menus that respect WZ-authored answer metadata instead of advancing on every missing branch, more of the client’s WZ-backed failure and return-to-NPC text, runtime quest item-count and quest-state token rendering across both quest-detail text and authored `Say.img` conversation pages, cleaner MapleStory token rendering across mob, selected-quest, quest-reference, stray prompt markers, and reward-summary category tags, no longer exposes `stop` bookkeeping as a user-facing branch choice, and now uses authored lost-item fallback dialogue more often when a quest item is missing. The remaining gap is narrowed to true script execution, wider conditional branch selection beyond the current named WZ stop-branch mapping, deeper dynamic script-variable resolution beyond the current WZ-backed `#c` and `#u` fallbacks, post-action script execution, and broader client conversation window parity | player/NPC/UI layer (`CUserLocal::TalkToNpc`) |
| Partial | Quest lists and quest-only actions | Nearby NPC overlays now list WZ-backed available, locked, in-progress, and completable quests, keep accept/completion state plus quest mob counts, picked-up quest item counts, and quest action item grants/consumes for the simulator session, gate startable quests on `Check.img/0/item` requirements, and now also mirror additional common quest gates from WZ by surfacing `Check.img/0/pop`, trait-min requirements such as `charismaMin` or `craftMin`, `Check.img/0/skill` acquire gates including common bare-`id` entries with no explicit `acquire` flag, `Check.img/0/job` ancestor or branch matches such as a root magician gate accepting current magician advancements instead of only exact job ids, and `Check.img/1/endmeso` costs through locked quest rows, NPC quest pages, quest detail requirement lines, completion progress, and accept or complete actions, while common quest actions now also apply `Act.img/skill` skill-level grants plus direct `masterLevel` and `onlyMasterLevel` session updates, `Act.img/sp` per-session SP rewards for the current job tab, trait EXP rewards from `Act.img/*EXP`, plus EXP, fame, next-quest notices, linked quest-state mutations, WZ `Act.img/item/*/resignRemove` cleanup on give-up, and reward-item `jobEx` gating for common explorer subjob branches such as Dual Blade and Cannoneer, with quest reward, SP, and skill job filters now resolving root-job and class-branch matches instead of only exact job ids, quest accept and completion actions pre-resolving their WZ reward items and blocking on live inventory-space failures when an inventory runtime is present instead of silently shunting those rewards into quest-tracked counts, and WZ `Act.img/item` choice groups now no longer silently grant the first reward when multiple eligible `prop = -1` entries remain after job or gender filtering, instead surfacing those pending choices as blocking requirement text while still auto-resolving single remaining eligible choice rows, and quest reward detail text surfacing WZ choice or random markers plus job or `jobEx` filters instead of flattening every reward row to a plain item name, but full script branching, player-driven item-selection reward UI, broader skill-book or other specialized quest reward flows beyond direct skill/master-level updates, and wider inventory-backed quest parity such as richer per-slot inventory failure handling beyond simple accept-or-block checks are still incomplete | Progression-facing NPC parity now has a broader WZ-backed simulator baseline for both start and completion requirements plus common reward mutations, so fame, trait, skill-gate, root-job start gating, skill-level and master-level grants, SP, meso-cost, item, chained-quest state feedback, give-up item cleanup, bare-`id` skill acquire gates, root-job or class-branch reward filtering, and the common `prop = -1` choice-reward surface stay in sync across the NPC overlay and quest window, and live inventory-backed sessions now stop quest starts or turn-ins from claiming reward items the current inventory cannot accept or from incorrectly auto-claiming multi-choice rewards. The remaining gap is narrowed to script-driven branches, actual player-facing reward-selection UI, more specialized skill-book style quest rewards beyond direct level/master-level fields, and deeper inventory semantics beyond the current item-capacity gate | NPC/UI layer |
| Partial | Auto-follow and escort follow requests | Escort mobs now mirror more of the client-side `TryAutoRequestFollowCharacter` plus `SendFollowCharacterRequest` seam with the verified `|dx| <= 80` and `|dy| <= 30` attach window, grounded-only initial attach, traversable foothold-path checks, the WZ-backed `Map.img/info/nofollowCharacter` suppression flag, and the simulator-owned local-user blockers that map cleanly onto the request path today, including seated, morph-template avatar transforms, skill-transform immovable, mounted, and mob-status immovable states before the auto-follow request can attach; fresh escort requests are now also throttled to the client-backed `1000` ms cadence instead of reattaching every frame after a break, while an already-attached escort still keeps the existing leash alive through those temporary local-user request blockers so the simulator no longer drops an already-following escort just because the player sat, mounted, morphed, or became temporarily movement-locked after the request gate had already succeeded. The packet-owned local utility seam now also mirrors more of `CUserLocal::OnFollowCharacterFailed` plus `FollowCharacterFailedMsg` by decoding the client-shaped two-`int32` follow-failure payload, honoring the special `-2` clear-pending case, preserving typed reason and driver-id state in the simulator status surface, resolving occupied-target failures against simulated remote-user names when available, and allowing `/localutility followfail <reasonCode> [driverId]` to exercise those client-backed reason codes directly instead of only raw freeform text; `UnitTest_MapSimulator` now includes focused coverage for both escort request gating and the new follow-failure payload decoder. The remaining gap is the actual remote character-to-character follow handshake itself, including target acceptance or attachment state transitions and the server-authored follow attach or detach lifecycle, because that still depends on a fuller remote-user follow runtime rather than escort mobs or packet-only failure playback alone | Escort-style encounters now reject the client-invalid midair, map-disabled, or local-user-invalid auto-attach cases, respect the same proximity gate and one-second request cadence the client uses before issuing a follow request, preserve follow through short jump arcs instead of dropping immediately on takeoff, keep an already-attached escort stable across the temporary local-user blockers that belong to request issuance rather than release semantics, and now expose reason-coded follow-failure playback on the same utility seam the client uses for remote follow rejection. The remaining gap is narrowed to the remote-user follow handshake and its follow-state lifecycle rather than the previously missing failure-message decoding itself | `PlayerCharacter`, `MobItem.cs`, `EscortFollowController.cs`, `MapSimulator.PacketOwnedUtilityParity.cs`, `FollowCharacterFailureCodec.cs`, field interaction layer (`CUserLocal::TryAutoRequestFollowCharacter`, `CWvsContext::SendFollowCharacterRequest`, `CUserLocal::OnFollowCharacterFailed`) |
| Partial | Direction-mode release timing | NPC dialogue overlays now enter a simulator direction-mode lock that blocks local gameplay input without dropping into free-camera mode, closing those scripted overlays releases control on a delayed timer modeled after `CUserLocal::TryLeaveDirectionMode`, the overlay-dismissal click is now consumed so it cannot leak into same-frame NPC or portal interactions before the delay expires, special-field scripted dialogs are advanced before world interaction handlers so wedding ceremony field dialogs can claim that same blocking owner on the very frame they appear instead of leaking one frame of movement or click-through before the delayed-release path engages, the delayed-release path now uses an explicit scripted-owner registry instead of only a fixed visible-window list so the tracked NPC or scripted launch points feed it directly for Cash Shop or MTS preview, trunk, item maker, MapleTV, memo send and package dialogs, the social-room preview surfaces for miniroom, player shop, hired merchant, and trading-room, NPC-owned item-upgrade launches and their Vega Spell child window when that child is opened from the already locked upgrade flow, explicit special-field owner tokens for the wedding dialog and memory game blocker, inherited utility or community windows opened while a scripted lock is already active such as memo inbox, guild BBS, party or guild search, guild skill, family chart or tree, and messenger, packet-owned quest delivery and class-competition placeholder launches, plus the skill-window ride shortcut into Character Info, and known script-launched utility surfaces that still route through `UIWindowManager.ShowWindow` now also feed that same delayed-release owner path through a manager-level pre-show hook so status-bar community opens, status-bar character-info opens, skill-window guild-skill opens, skill-window ride opens, character-info party, family, miniroom, player-shop, hired-merchant, and trading-room launches, plus equivalent manager-routed reopen paths for Cash Shop, MTS, MapleTV, memo, guild, family, quest-delivery, class-competition, and social-room windows keep direction mode alive even after the original scripted owner has already started its release delay; the remaining gap is narrowed to script-launched utility surfaces that still call `window.Show()` directly or any future owner windows that use `UIWindowManager.ShowWindow` without being added to the implicit-owner policy list | Scripted UI sequences now keep their dismiss or expiry transitions inside the scripted lifecycle instead of accidentally re-triggering world interactions while control is still supposed to be locked, and this pass moves the modal ownership closer to the client seam by making both the active scripted field runtimes and the in-flow packet, skill, and utility-community launches explicitly hold direction mode instead of relying only on a broad visibility sweep. The remaining gap is narrowed to raw `window.Show()` launcher paths and future scripted owner surfaces that are introduced without being added to the manager-level implicit-owner window policy | `GameStateManager.cs`, `DirectionModeWindowOwnerRegistry.cs`, `UIWindowManager.cs`, `MapSimulator.cs`, `SpecialFieldRuntimeCoordinator.cs`, `SpecialEffectFields.cs` (`CUserLocal::TryLeaveDirectionMode`) |
| Partial | Reactor interaction parity | Local player movement now runs reactor touch checks through `ReactorPool`, throttles the local touch sweep to the client's once-per-second cadence from `CUserLocal::CheckReactor_Collision`, reactor visuals now load full WZ-defined direct frame sequences plus sparse numeric state branches instead of collapsing many reactors to `0/0` or hard-falling back to state `0`, runtime reactor activation now resolves `info/reactorType` back into touch, hit, skill, item, or animation-only handling instead of collapsing most non-quest reactors to touch, now also falls back to WZ-authored state `event/*/type` metadata when `info/reactorType` is absent so common harvest, item-use, skill-use, and touch-only reactors stop defaulting to touch just because the info block is sparse, the non-touch branch stays wired into the simulator's actual player attack surfaces by forwarding world-space melee or magic hitboxes, projectile impacts and explosion areas, summon strike areas, and the basic no-skill attack fallback into bounded reactor hit checks with left-versus-right hit gating for directional reactor types, item-backed reactors now only activate from matching inventory use near the local player and can consume the matching item even when the simulator has no richer local effect for that item yet, quest-backed reactors now bind directly to the simulator quest runtime so they enter or reset their active state as quest requirements change, reactor runtime state progression now keeps the real WZ state ids and per-state frame durations alive during activation or follow-up hits instead of collapsing back to synthetic `1/2/3/4` states or destroying multi-state hit reactors after the first post-activation strike, authored WZ state-event targets are now consumed for single-target activation and timed reset paths so reactors that encode their next or reset state in `event/*/state` no longer rely only on numeric state ordering, non-hit reactors now also continue walking authored numeric WZ state chains on their own timer instead of freezing forever on the first progressed state, reactor frame advancement now catches up across larger elapsed ticks instead of only stepping one frame per update, pool runtime state now keeps the live authored frame index for the active state, reactors with WZ `info/script` names now feed the simulator's existing dynamic object-tag publication seam through the same alias resolver used for field-entry scripts so script-backed tagged objects can turn on when those reactors activate and turn back off when the reactor resets, destroys, or respawns, live reactor collision or item-use checks now consume the active WZ frame bounds instead of a static `ReactorInstance` box so frame-authored origin shifts keep touch, hit, directional-hit, and local item-use interaction aligned to the visible reactor pose, and destroyed non-respawning reactors now drop out of the renderable set immediately instead of lingering onscreen because no fade path ever ran for the common destroy case; true reactor locomotion beyond those per-frame bounds shifts, multi-target authored event graphs that need a richer event-type dispatch than the current unique-target fallback, and script behaviors that need a fuller named event bridge than object-tag publication are still incomplete | Environmental interaction now covers the client-like touch-reactor path, its collision polling cadence, more of the WZ reactor animation surface, WZ-backed touch versus hit versus skill versus item versus quest activation selection, local inventory-driven item reactor use, quest-state-driven reactor refresh, a first pass of script-backed object-state publishing for tagged map objects driven by reactor `info/script`, live WZ-frame collision bounds so authored frame offsets also affect touch, hit, directional-hit, and local item-reactor checks, closer WZ-authored state playback because the runtime now also honors single-target state-event metadata for missing activation typing and timed reset playback instead of relying only on `info/reactorType` and numeric next-state order, and immediate hide-on-destroy behavior for non-respawning reactors so one-shot or manually removed reactors stop lingering visually after destruction. The remaining gap is narrowed to reactors that truly move independently of their current frame bounds, authored branches with multiple competing event targets that still need deeper event-type dispatch, plus richer named script or field-event execution beyond those existing dynamic-tag toggles | `ReactorPool.cs`, `ReactorItem.cs`, `MapSimulator.cs`, `EffectLoader.cs`, `PlayerManager.cs`, `SkillManager.cs`, `ReactorScriptStatePublisher.cs`, `UnitTest_MapSimulator/ReactorPoolDynamicBoundsTests.cs` (`CUserLocal::CheckReactor_Collision`) |
| Partial | Quest-alert, balloon, and idle social feedback loops | NPCs with available, in-progress, or completable quests now render the client-backed `QuestIcon` alert art above their heads based on the simulator quest runtime, quest accept/complete actions now queue timed in-world feedback balloons above the speaking NPC instead of collapsing to a single fixed message while also holding the NPC in `speak` until the active balloon expires, nearby NPCs with WZ `info/speak/*` idle lines now periodically surface those lines through the same in-world balloon path, and pets now mirror the client-backed `PetAutoSpeaking` surfaces for level-up, pre-level-up, idle `e_rest`, low-HP alert, and HP/MP potion failure speech with the same six WZ-backed `String/PetDialog.img` event keys that exist in this data set; USE-item clicks or hotkeys now parse WZ `Item/Consume/*/spec` recovery fields including numeric-string `hpR`/`mpR` values seen in this data set, HP/MP potion failure speech is throttled to the client-shaped first-few repeats instead of firing forever, zero-count USE hotkey bindings now persist across config reloads instead of being dropped when the stack is empty, and live pet-food item uses now route through the loaded `f1`-`f4` dialog buckets by deriving the same four command-level tiers exposed by pet `interact/c*` WZ ranges, selecting a compatible active pet from the food item’s `spec/*` whitelist when present, and only consuming food when the targeted pet’s new simulator fullness state can actually accept the WZ `spec/inc` increase while already-full pets surface the matching failure bucket instead, but broader non-`PetAutoSpeaking` specialist pet chatter call sites, closeness-driven command progression, and fuller long-lived pet-care ownership are still not modeled | The simulator now surfaces both static quest-state alerts and closer client-like NPC and pet idle feedback loops in-world instead of hiding all progression feedback inside the dialogue overlay alone, closes the consume recovery mismatch by honoring numeric-string recovery ratios in `Item/Consume/*/spec`, and now turns real pet-food item uses into WZ-backed in-world pet speech using the pet’s existing command-tier ranges, compatibility list, and `spec/inc` fullness gain so failure speech no longer burns food on already-full pets; the remaining gap is narrowed to chatter that lives outside the client’s six `PetAutoSpeaking` events plus closeness and broader pet-care state machines that should own command growth, cleanup, and other specialist pet call sites | `QuestRuntimeManager.cs`, `NpcFeedbackBalloonQueue.cs`, `NpcItem.cs`, `LifeLoader.cs`, `PetLoader.cs`, `PetController.cs`, `MapSimulator.cs`, `UIWindow*.img/QuestIcon`, `String/PetDialog.img`, `Item/Pet/*/interact`, `Item/Consume/0212/*/spec` (`CUserLocal::Update`, `CUser::PetAutoSpeaking`) |

### 2. Scripted field-state and quest-timer parity

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Field-owned script/message and quest-timer packet parity | The simulator now has a dedicated packet-owned field-state runtime wired at the `CField::OnPacket` seam for `OnFieldSpecificData` (`149`), `OnDesc` (`162`), `OnSetQuestClear` (`166`), `OnSetQuestTime` (`167`), and `OnSetObjectState` (`169`), plus a packet-owned script-message bridge at the `CScriptMan::OnPacket` seam for `OnScriptMessage` (`363`). The script bridge decodes the shared `speakerType` / `speakerTemplate` / `msgType` / `bParam` header confirmed in IDA, opens the existing NPC interaction overlay in direction mode instead of a new ad hoc surface, resolves speaker NPC names from map/runtime or WZ string data, and renders packet-authored `OnSay`, `OnAskYesNo`, `OnAskMenu`, `OnAskText`, `OnAskNumber`, and `OnAskBoxText` prompts through the same overlay/page-choice runtime already used by NPC and quest conversations. Manual packet exercise was also expanded with `/scriptmsg` for raw payloads plus canned say / yes-no / menu prompts. The field-state side still binds map help text from map info, shows packet-authored field help overlays, tracks quest-timer start/end windows with WZ-backed quest names and `timerUI` metadata, clears packet-authored timers, appends active timer text into the quest UI surfaces, captures field-specific-data handoff state for the active special-field owner, and routes named object-state flips through the existing tagged-object/runtime seam. Remaining gap: field-specific-data payloads are still only handed off and summarized rather than deeply decoded per field owner, typed script prompts are currently read-only because packet reply submission widgets are not wired, and the help / timer / script presentation still uses simulator-native visuals instead of the client’s dedicated `UtilDlgEx` / timer UI assets and behavior. | Map scripts and event fields are not driven only by NPC clicks or reactor collisions. Without this packet-owned bridge, parity work keeps re-implementing isolated outcomes while missing the client seam that coordinates post-entry scripted help dialogs, timed quest deadlines, quest-clear resets, script prompts, and `CMapLoadable::SetObjectState` updates for object-tag and map-state changes | field/script runtime bridge (`CField::OnPacket`, `CField::OnFieldSpecificData`, `CField::OnDesc`, `CField::OnSetQuestClear`, `CField::OnSetQuestTime`, `CField::OnSetObjectState`, `CScriptMan::OnPacket`) |

### 3. Packet-authored field messaging and feedback parity

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Missing | Field chat, effect, warning, and timer packet parity | The backlog still has no dedicated row for the packet-owned field feedback surface revealed by the dispatcher scan. IDA shows that `CField::OnPacket` and `CUserLocal::OnPacket` own a distinct cluster of visible messaging and presentation handlers outside the scripted-field row: `OnGroupMessage` (`150`) routes multiple group-chat families through status-bar log types and blacklist/swindle-warning checks; `OnWhisper` (`151`) owns inbound and outbound whisper logging, whisper-candidate state, location-find responses, chase routing, and the explicit client result/failure notices; `OnCoupleMessage` (`152`) owns couple-chat and couple-notice log routing; `OnFieldEffect` (`154`) drives screen effects, summon effects, tremble, object-state pushes, field sounds, boss HP tags, BGM swaps, and reward-roulette visuals; `OnFieldObstacleOnOff`, `OnFieldObstacleOnOffStatus`, and `OnFieldObstacleAllReset` own packet-driven obstacle presentation; `OnWarnMessage` owns modal warn dialogs; `OnPlayJukeBox` owns packet-driven field music changes; `OnTransferFieldReqIgnored`, `OnTransferChannelReqIgnored`, and `OnSummonItemInavailable` show that visible failure messaging around map transfer and summon-item use is packet-owned; `OnDestroyClock` plus the dedicated Zakum/Horntail/Chaos Zakum/Hontale timer handlers show that several boss-timer surfaces are not part of the generic field clock row; and `CUserLocal::OnFieldFadeOutForce` owns forced fade-animation teardown separately from the normal fade-in/out path. The simulator currently spreads nearby concerns across chat-strip, field-rule, weather, and overlay rows but still does not represent this dispatcher-owned feedback layer as one parity target. | Without a dedicated row, visible packet-owned feedback keeps getting treated as unrelated one-off polish when the client actually owns it as a cohesive surface between gameplay, the status bar, field overlays, and special-event timers. Capturing it explicitly will keep future parity work anchored to the right handlers instead of burying group/whisper chat, boss timers, forced fades, obstacle toggles, warn dialogs, jukebox swaps, and packet-authored field effects inside unrelated rows. | packet-owned field feedback layer (`CField::OnPacket`, `CField::OnGroupMessage`, `CField::OnWhisper`, `CField::OnCoupleMessage`, `CField::OnFieldEffect`, `CField::OnFieldObstacleOnOff`, `CField::OnFieldObstacleOnOffStatus`, `CField::OnFieldObstacleAllReset`, `CField::OnWarnMessage`, `CField::OnPlayJukeBox`, `CField::OnTransferFieldReqIgnored`, `CField::OnTransferChannelReqIgnored`, `CField::OnSummonItemInavailable`, `CField::OnDestroyClock`, `CField::OnZakumTimer`, `CField::OnHontailTimer`, `CField::OnChaosZakumTimer`, `CField::OnHontaleTimer`, `CUserLocal::OnFieldFadeOutForce`) |

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
7. Packet-feedback pass:
   Add the packet-owned field messaging, warn-dialog, forced-fade, obstacle, field-effect, and boss-timer surface before more status-bar or field-overlay polish is tracked as isolated UI work.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
