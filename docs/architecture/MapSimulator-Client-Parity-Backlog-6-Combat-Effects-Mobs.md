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

### 1. UI parity

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

### 1. UI parity

This area is more advanced than older parity notes suggested, but still far from complete client parity.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Status bar layout | Positions are informed by client analysis, bitmap/WZ-backed rendering exists for gauges and text, the overlay now stays anchored to the composed `StatusBar2.img/mainBar` frame, the Big Bang loader now composes the `mainBar` left cluster and gauge cluster from the client canvas origins and WZ `z` order instead of the older stack-panel approximation so `gaugeCover` no longer ends up underneath `gaugeBackgrd`, the left status cluster now includes the client `lvCover` overlay instead of only `lvBacktrnd`, the level label now uses the client `mainBar/lvNumber` digits, the left cluster's level/job/name baseline now matches the client `SetStatusValue` Y slots more closely, the name shadow matches the client's diagonal passes more closely, the HP/MP/EXP readouts now right-align against the client-derived `SetNumberValue` anchor slots as their widths change, the EXP label now shows live raw EXP plus percentage instead of only a percentage stub, the job label now collapses hierarchical names to the client-facing title slot, renders in a dim-green narrower fit instead of the older generic yellow string, and no longer applies the generic four-way shadow pass that `CUIStatusBar::SetStatusValue` reserves for the character name, and the Big Bang chat-strip branch now replays the client `ApNotify` / `SpNotify` badge animations from `StatusBar2.img/mainBar` when the simulator has distributable AP or SP | The status bar now uses the correct WZ layering and closer client text baselines, but the remaining gap is the rest of the `CUIStatusBar` draw-detail matrix around exact Big Bang chat-strip composition and any still-unmatched minor slot offsets rather than the old coarse frame assembly | `StatusBarUI.cs`, `StatusBarChatUI.cs`, `UILoader.cs`, `MapSimulator.cs` (`CUIStatusBar::Draw`, `CUIStatusBar::SetStatusValue`, `CUIStatusBar::SetNumberValue`, `UI/StatusBar2.img/mainBar/lvNumber`, `UI/StatusBar2.img/mainBar/lvCover`, `UI/StatusBar2.img/mainBar/gaugeBackgrd`, `UI/StatusBar2.img/mainBar/gaugeCover`, `UI/StatusBar2.img/mainBar/ApNotify`, `UI/StatusBar2.img/mainBar/SpNotify`) |
| Partial | Skill UI layout | Row height, positions, hit boxes, SP placement, quick-slot assignment wiring, and a client-backed `Basic.img/VScr` scrollbar at the confirmed `153,93,155` placement are now present, with the Big Bang hover tooltip reusing the client `tip0..tip2` frames, the scrollbar supporting arrow clicks, track paging, thumb dragging, and wheel-over-scrollbar input, the row list drawing the client `Skill/main/recommend/0` banner from `Etc/RecommendSkill.img/<skillRootId>` SP-threshold table at the client `47,nTop-19` overlay offset plus the existing visible-tab levelable-skill fallback, the book header now following the client width-driven two-line wrap path instead of splitting only on the last space, the visible and inactive skill-book tabs now drawing and hit-testing from their actual `UIWindow2.img/Skill/main/Tab/*` origins and heights so the enabled `30x20 @ y=27` and disabled `30x18 @ y=29` states no longer share one flattened baseline, and the `BtSpUp` control now using its normal/hover/pressed/disabled button states with a simulator-derived remaining-SP pool per tab plus loaded `req` / `reqLevel` prerequisite gates so locked skills no longer advertise live SP-up affordances or fallback recommendation highlights; remaining layout parity is narrower and mostly limited to smaller client-accurate chrome polish such as exact hover-frame anchoring and any residual per-state text placement differences inside the skill book | The window is usable and much closer to the client than the old notes implied, but it is still not a full `CUISkill` clone and now mainly needs smaller draw-placement refinements rather than the earlier structural layout gaps | `SkillUI.cs`, `SkillUIBigBang.cs`, `SkillDataLoader.cs`, `UIWindowLoader.cs`, `MapSimulator.cs` (`CUISkill::Draw`, `CUISkill::OnCreate`, `CUISkill::GetRecommendSKill`, `Etc/RecommendSkill.img`, `UI/UIWindow2.img/Skill/main/Tab`, `UI/UIWindow2.img/Skill/main/recommend`, `UI/UIWindow2.img/Skill/main/BtSpUp`) |
| Implemented | Quick slot cooldown overlay | Cooldown masking now uses the client `UIWindow2.img/Skill/main/CoolTime` frame family, remaining-seconds countdown text renders on assigned quick-slot skills, and the stepped mask holds the initial full-overlay frame until the next visible whole-second boundary instead of advancing immediately after cast | The core client-facing quick-slot cooldown feedback is now present on the simulator HUD | `QuickSlotUI.cs` (`CUIStatusBar::CQuickSlot::Draw`) |
| Implemented | Quick-slot validation and quantity sync | The simulator now rejects passive, hidden, and unlearned skills at hotkey assignment time, restores saved quick-slot bindings through the same validation path after preset load instead of trusting raw slot data, eagerly prunes stale skill slots as soon as learned levels change, cancels an in-flight quick-slot drag if its source binding becomes invalid before the next redraw, and now mirrors the client's `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` inventory-backed recount for owned `USE` / `CASH` quick-slot item entries so stack counts redraw live and empty item slots clear as soon as their backing inventory runs out | Quick-slot contents now stay synchronized with both learned-skill state and live inventory stacks, so the HUD no longer keeps stale consumable or cash-item entries after crafting, pickup, preset restore, or manual reassignment | `QuickSlotUI.cs`, `SkillManager.cs`, `CharacterConfig.cs`, `MapSimulator.cs` (`CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo`) |
| Implemented | HP/MP warning flash | HP/MP gauge warnings now replay the client `aniHPGauge` / `aniMPGauge` and `hpFlash` / `mpFlash` assets for 500 ms when the resource drops further while already below the configured threshold, and the simulator exposes live `/hpwarn` + `/mpwarn` commands to adjust those thresholds in-session | The client-visible low-resource cue is present across both modern and pre-BigBang status-bar loaders, and the trigger point is no longer hardcoded at 20% | `StatusBarUI.cs`, `UILoader.cs`, `MapSimulator.cs` (`CUIStatusBar::Draw`, `CUIStatusBar::SetNumberValue`) |
| Partial | Chat log / whisper flows | The Big Bang status-bar chat strip now renders wrapped recent chat lines from the simulator chat history, keeps wrapped continuation rows indented like the client `ChatLogAdd` flow, now skips that continuation indent for the client `lType 7..12` branch the way `ChatLogAdd` does for system-style entries, supports both button-driven and wheel-over-chat log scrolling, preserves manually scrolled history when new chat arrives until the log has been idle long enough to snap back to the newest line like the client `_RefreshChatLog` path, cycles the `chatTarget` badge through the client WZ labels, now applies the WZ `chatTarget/*/origin` deltas when drawing those labels so the wider alliance badge no longer reuses the base/all offset, supports `/w` + `/r` whisper targeting with an on-bar prompt plus an armed whisper mode where `/w <name>` selects the active recipient and subsequent plain Enter sends continue to that target until the target changes, keeps slash-mode target-selection commands armed on the bar instead of closing chat when they only switch channel, and now tags simulator-generated target/system/error/whisper entries with client-style chat-log types so the strip can follow the observed `ChatLogAdd` wrap-routing rules instead of treating every line as one generic row class; exact client chat-log font selection and the full per-`lType` color/font routing matrix are still missing | The status-bar chat surface is closer to the client's visible log behavior and no longer forces button-only review of older lines, while keyboard chat-mode switching and whisper arming now stay on the bar instead of bouncing the player back out of chat after every target change, the label placement now honors the WZ origin data instead of one flattened badge offset, and the log can distinguish client-style system/error routing for wrap behavior, but it is still short of a full client chat log and whisper workflow clone until the remaining client text-color and font-routing details are matched across the full chat-type set | `MapSimulatorChat.cs`, `StatusBarChatUI.cs`, `UILoader.cs` (`CUIStatusBar::ChatLogAdd`, `CUIStatusBar::_RefreshChatLog`, `UI/StatusBar2.img/mainBar/chatTarget/*`) |
| Partial | Full skill UI behavior | The Big Bang skill window now mirrors `CUISkill::GetSkillRootVisible` at the tab layer by only exposing the visible skill-book path for the current job, keeps keyboard-selected entries in view while accepting pane-hover wheel scrolling plus `Up` / `Down` / `PageUp` / `PageDown` / `Home` / `End` navigation, lets `Left` / `Right` cycle across the currently visible skill-book tabs without reaching for the mouse, renders the client recommendation banner from a WZ-loaded skill-root/SP-threshold recommendation table with the existing visible-tab levelable-skill fallback when no applicable entry can level, routes `BtSpUp` clicks into live skill-level increments with per-tab SP countdown sourced from the remaining unmaxed levels in the loaded book, loads the Aran-only `Skill/main/Tab/AranButton/Bt1..Bt4` guide buttons so the client `3001..3004 -> OpenSkillGuide` path opens the matching `UIWindow2.img/AranSkillGuide/<grade-1>` overlay page, and now instantiates the WZ-backed bottom-row `BtRide` / `BtGuildSkill` controls so they no longer sit dead in the skill frame and instead open the existing ride-profile and guild/social flows from the skill window | The simulator now respects the client's visible-root job path, no longer leaves the level-up button dead, follows the client-style recommendation progression more closely, and no longer drops the Aran guide or the WZ-backed ride / guild shortcut controls that the Big Bang skill window exposes; the remaining gap is the rest of the non-Aran `CUISkill` shell, especially the dedicated `UserList/GuildSkill` surface and other unmodeled generic button/window flows such as the `DualTab` branch | `SkillUI.cs`, `SkillUIBigBang.cs`, `UIWindowLoader.cs`, `MapSimulator.cs`, `UserInfoUI.cs`, `SocialListWindow.cs` (`CUISkill::Draw`, `CUISkill::GetSkillRootVisible`, `CUISkill::GetRecommendSKill`, `CUISkill::OnButtonClicked`, `CUISkill::OpenSkillGuide`, `UI/UIWindow2.img/Skill/main/BtRide`, `UI/UIWindow2.img/Skill/main/BtGuildSkill`, `UI/UIWindow2.img/Skill/main/Tab/DualTab`, `UI/UIWindow2.img/Skill/main/Tab/AranButton`, `UI/UIWindow2.img/AranSkillGuide`, `UI/UIWindow2.img/UserList/GuildSkill`) |
| Partial | Item/skill tooltip behavior | Skill hover tooltips now reuse the client `UIWindow2.img/Skill/main/tip0..tip2` frames in the skill window, quick-slot skill hovers, status-bar buff tray, and status-bar cooldown tray; the status-bar buff/cooldown hovers now also follow the same icon-led client-style layout and select the correct left/center/right frame variant instead of stretching those textures as a composite strip, while the cooldown tray continues to surface the live skill description plus remaining cooldown time instead of forcing the player to infer entries from icon art alone, and the Big Bang equipment window now renders live equipped-item icons from the current `CharacterBuild` plus a framed hover tooltip that pulls String.wz category/description text and parsed equip `info` metadata for stat gains, upgrade slots, and requirement lines instead of stopping at slot-plus-item-id stubs; full client `ToolTip/Equip` job filters, potential/additional-option blocks, and broader cross-UI item-tooltip parity are still incomplete | The simulator now has a shared client-style tooltip surface across the main skill HUDs and the Big Bang equipment window no longer leaves equipped-item hovers blank or metadata-thin, but it still lacks the rest of the client's richer item-tooltip behavior | `SkillUIBigBang.cs`, `QuickSlotUI.cs`, `StatusBarUI.cs`, `EquipUIBigBang.cs`, `CharacterData.cs`, `CharacterLoader.cs`, `UIWindowLoader.cs`, `MapSimulator.cs` |
| Partial | Charge bars and richer skill HUD | The status bar now renders a WZ-backed key-down/charge gauge for prepared skills, applies the client-confirmed `KeyDownBar1` / `KeyDownBar4` HUD branches for the supported skill families, uses the client `get_max_gauge_time` timing map to drive the bar fill for those keydown skills, anchors each HUD branch from the WZ `bar/origin` that the client canvas insertion path honors instead of reusing one hardcoded top-left for every skin, shows a live skill-name plus ready/charge label above the bar, surfaces active hold-state labels for sustained keydown skills so capped branches no longer sit on a stale generic `Ready` label after charge completes, now drains the bar across capped hold windows using the simulator's live hold timer instead of leaving the gauge pinned full for the rest of the sustain branch, no longer suppresses the HUD for the remaining client-timed default-branch keydown families, and now applies the client Evan/dragon `KeyDownBar2` / `KeyDownBar3` skins for `22151001` / `22121000`; the remaining gap is that the simulator still routes every supported branch through one status-bar surface instead of mirroring the client's separate dragon-layer insertion path and still lacks the rest of the branch-specific text/state variants | Prepared/keydown skills now surface closer client timing and HUD feedback instead of a single generic bar, sustained keydown branches expose both their live maintain label and remaining capped-hold bar state more clearly, the previously hidden default-branch families now get the same client-timed HUD treatment, and the Evan/dragon branch no longer falls back to the generic skin, but the simulator still lacks the rest of the client's richer skill-state UI and distinct dragon-owned placement path | `StatusBarUI.cs`, `UILoader.cs`, `SkillManager.cs`, `MapSimulator.cs` (`CUserLocal::DrawKeyDownBar`, `CDragon::DrawKeyDownBar`, `CUserLocal::Update`, `get_max_gauge_time`, `UI/Basic.img/KeyDownBar*/bar/origin`) |

### 2. Full CMob action pipeline

This area tracks runtime parity for mob-driven attacks, warnings, and combat effects after the AI commits to an action.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Combat Effects Mobs | Mob attack runtime now consumes the parsed `rush` and `jumpAttack` flags during action playback, `jumpAttack` no longer stays locked to base `MobMoveType.Jump` mobs because grounded movers can now launch their attack jump branch on demand, attack or skill completion can immediately chain into the next eligible combat branch instead of always dropping through a visible chase reset first, and structured `attack/info/effect` nodes now load and replay the WZ-backed `effectType = 2` repeated range-impact family plus the `fall` / `x` / `y` motion hints those nodes publish, while `effectType = 1` range sweeps now honor WZ `duration` and generic structured-node `interval` pacing when staggering their spawned lanes instead of front-loading the full pattern on one tick, and sweep placements that outnumber the published sequence variants now cycle those WZ variants instead of pinning every extra lane to the last sequence; per-mob follow-up timing heuristics and any remaining client-only attack families outside the observed `effectType = 1/2` branches are still missing | The simulator now carries more of the client `CMob::DoAttack` movement payload into visible mob attacks, closes one of the more obvious `CMob::OnNextAttack` gaps by preserving combat flow across chained actions, and no longer drops the meteor or repeated-impact style attack-effect branch that several mobs expose through `attack/info/effect`, including the falling offset path used by mobs such as `4240000`, the duration-spread range sweeps used by `effectType = 1` nodes, and the repeated sweep variants seen on boss-style `effect0` families such as `8200011`, `8500001`, `8510000`, `8520000`, and `8800000`, which makes repeated mob attack sequences read closer to the client even though the full action pipeline is still incomplete | `MobItem.cs`, `MobMovementInfo.cs`, `LifeLoader.cs`, `MobAttackSystem.cs`, `MobAI.cs` (`CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |

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
