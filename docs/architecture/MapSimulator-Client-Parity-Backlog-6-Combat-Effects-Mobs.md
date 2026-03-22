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
| Partial | Status bar layout | Positions are informed by client analysis, bitmap/WZ-backed rendering exists for gauges and text, the overlay now stays anchored to the composed `StatusBar2.img/mainBar` frame, the Big Bang loader now composes the `mainBar` left cluster and gauge cluster from the client canvas origins and WZ `z` order instead of the older stack-panel approximation so `gaugeCover` no longer ends up underneath `gaugeBackgrd`, the left status cluster now includes the client `lvCover` overlay instead of only `lvBacktrnd`, the level label now uses the client `mainBar/lvNumber` digits, the left cluster's level/job/name baseline now matches the client `SetStatusValue` Y slots more closely, the name shadow matches the client's diagonal passes more closely, the HP/MP/EXP readouts now right-align against the client-derived `SetNumberValue` anchor slots as their widths change, the EXP label now shows live raw EXP plus percentage instead of only a percentage stub, the job label now collapses hierarchical names to the client-facing title slot, renders in a dim-green narrower fit instead of the older generic yellow string, and no longer applies the generic four-way shadow pass that `CUIStatusBar::SetStatusValue` reserves for the character name, and the Big Bang chat-strip branch now composes `chatSpace`, `chatSpace2`, `chatCover`, and `notice` from the client canvas origins and WZ `z` order, positions its chat-target/chat-toggle/scroll/status buttons from the same `mainBar` button origins instead of the older width-stacked approximation, aligns the target badge and `chatEnter` field from the WZ origins that the client canvases publish, and continues to replay the client `ApNotify` / `SpNotify` badge animations when the simulator has distributable AP or SP | The status bar now uses the correct WZ layering for both the main bar and the Big Bang chat strip and is closer to the client's chat-branch layout, but the row still has some remaining `CUIStatusBar` draw-detail work around exact open-vs-close chat-toggle state art, any residual text-slot micro-offsets, and other minor per-state polish rather than the older coarse frame assembly | `StatusBarUI.cs`, `StatusBarChatUI.cs`, `UILoader.cs`, `MapSimulator.cs` (`CUIStatusBar::Draw`, `CUIStatusBar::SetStatusValue`, `CUIStatusBar::SetNumberValue`, `UI/StatusBar2.img/mainBar/lvNumber`, `UI/StatusBar2.img/mainBar/lvCover`, `UI/StatusBar2.img/mainBar/gaugeBackgrd`, `UI/StatusBar2.img/mainBar/gaugeCover`, `UI/StatusBar2.img/mainBar/chatSpace`, `UI/StatusBar2.img/mainBar/chatSpace2`, `UI/StatusBar2.img/mainBar/chatCover`, `UI/StatusBar2.img/mainBar/chatEnter`, `UI/StatusBar2.img/mainBar/ApNotify`, `UI/StatusBar2.img/mainBar/SpNotify`) |
| Partial | Skill UI layout | Row height, positions, hit boxes, SP placement, quick-slot assignment wiring, and a client-backed `Basic.img/VScr` scrollbar at the confirmed `153,93,155` placement are now present, with the Big Bang hover tooltip reusing the client `tip0..tip2` frames, the scrollbar supporting arrow clicks, track paging, thumb dragging, and wheel-over-scrollbar input, the row list drawing the client `Skill/main/recommend/0` banner from `Etc/RecommendSkill.img/<skillRootId>` SP-threshold table at the client `47,nTop-19` overlay offset plus the existing visible-tab levelable-skill fallback, the book header now following the client width-driven two-line wrap path instead of splitting only on the last space, the visible and inactive skill-book tabs now drawing and hit-testing from their actual `UIWindow2.img/Skill/main/Tab/*` origins and heights so the enabled `30x20 @ y=27` and disabled `30x18 @ y=29` states no longer share one flattened baseline, the `BtSpUp` control now using its normal/hover/pressed/disabled button states with a simulator-derived remaining-SP pool per tab plus loaded `req` / `reqLevel` prerequisite gates so locked skills no longer advertise live SP-up affordances or fallback recommendation highlights, and the dedicated client `tip1` / `tip2` hint bubbles now surface on `BtSpUp` and skill-point-count hovers instead of falling through to the generic row tooltip; remaining layout parity is narrower and mostly limited to the still-generic mouse-anchoring of those `tip*` frames and a few residual per-state text-placement differences inside the skill book | The window is usable and much closer to the client than the old notes implied, and the remaining gap is now limited to smaller draw-placement polish rather than the earlier structural layout misses | `SkillUI.cs`, `SkillUIBigBang.cs`, `SkillDataLoader.cs`, `UIWindowLoader.cs`, `MapSimulator.cs` (`CUISkill::Draw`, `CUISkill::OnCreate`, `CUISkill::GetRecommendSKill`, `Etc/RecommendSkill.img`, `UI/UIWindow2.img/Skill/main/Tab`, `UI/UIWindow2.img/Skill/main/recommend`, `UI/UIWindow2.img/Skill/main/BtSpUp`, `UI/UIWindow2.img/Skill/main/tip0`, `UI/UIWindow2.img/Skill/main/tip1`, `UI/UIWindow2.img/Skill/main/tip2`) |
| Implemented | Quick slot cooldown overlay | Cooldown masking now uses the client `UIWindow2.img/Skill/main/CoolTime` frame family, remaining-seconds countdown text renders on assigned quick-slot skills, and the stepped mask holds the initial full-overlay frame until the next visible whole-second boundary instead of advancing immediately after cast | The core client-facing quick-slot cooldown feedback is now present on the simulator HUD | `QuickSlotUI.cs` (`CUIStatusBar::CQuickSlot::Draw`) |
| Implemented | Quick-slot validation and quantity sync | The simulator now rejects passive, hidden, and unlearned skills at hotkey assignment time, restores saved quick-slot bindings through the same validation path after preset load instead of trusting raw slot data, eagerly prunes stale skill slots as soon as learned levels change, cancels an in-flight quick-slot drag if its source binding becomes invalid before the next redraw, and now mirrors the client's `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` inventory-backed recount for owned `USE` / `CASH` quick-slot item entries so stack counts redraw live and empty item slots clear as soon as their backing inventory runs out | Quick-slot contents now stay synchronized with both learned-skill state and live inventory stacks, so the HUD no longer keeps stale consumable or cash-item entries after crafting, pickup, preset restore, or manual reassignment | `QuickSlotUI.cs`, `SkillManager.cs`, `CharacterConfig.cs`, `MapSimulator.cs` (`CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo`) |
| Implemented | HP/MP warning flash | HP/MP gauge warnings now replay the client `aniHPGauge` / `aniMPGauge` and `hpFlash` / `mpFlash` assets for 500 ms when the resource drops further while already below the configured threshold, and the simulator exposes live `/hpwarn` + `/mpwarn` commands to adjust those thresholds in-session | The client-visible low-resource cue is present across both modern and pre-BigBang status-bar loaders, and the trigger point is no longer hardcoded at 20% | `StatusBarUI.cs`, `UILoader.cs`, `MapSimulator.cs` (`CUIStatusBar::Draw`, `CUIStatusBar::SetNumberValue`) |
| Partial | Chat log / whisper flows | The Big Bang status-bar chat strip now renders wrapped recent chat lines from the simulator chat history, keeps wrapped continuation rows indented like the client `ChatLogAdd` flow, now skips that continuation indent for the client `lType 7..12` branch the way `ChatLogAdd` does for system-style entries, now applies the client `ChatLogAdd` first-line width reduction for the observed whisper or special-entry `lType 14/16/19/20` branch before wrapped continuations fall back to the normal width, supports both button-driven and wheel-over-chat log scrolling, preserves manually scrolled history when new chat arrives until the log has been idle long enough to snap back to the newest line like the client `_RefreshChatLog` path, cycles the `chatTarget` badge through the client WZ labels, now applies the WZ `chatTarget/*/origin` deltas when drawing those labels so the wider alliance badge no longer reuses the base/all offset, supports `/w` + `/r` whisper targeting with an on-bar prompt plus an armed whisper mode where `/w <name>` selects the active recipient and subsequent plain Enter sends continue to that target until the target changes, keeps slash-mode target-selection commands armed on the bar instead of closing chat when they only switch channel, now routes target/system/error/whisper text colors through the simulator's client chat-log type mapping instead of a separate target-color table, and now replays the observed `ChatLogAdd` backdrop or shadow tint branch for the currently modeled special `lType` values rather than drawing every chat row with the same black strip and shadow; exact client chat-log font selection and the remaining unmodeled per-`lType` text-color or font-routing cases are still missing | The status-bar chat surface is closer to the client's visible log behavior and no longer forces button-only review of older lines, while keyboard chat-mode switching and whisper arming now stay on the bar instead of bouncing the player back out of chat after every target change, the label placement now honors the WZ origin data instead of one flattened badge offset, the wrap pass now follows the observed narrower first-line branch for whisper or special rows, and the log no longer flattens every row to one background or shadow treatment, but it is still short of a full client chat log and whisper workflow clone until the remaining client font selection and the rest of the text-color or font-routing matrix are matched across the broader chat-type set | `MapSimulatorChat.cs`, `StatusBarChatUI.cs`, `UILoader.cs` (`CUIStatusBar::ChatLogAdd`, `CUIStatusBar::_RefreshChatLog`, `UI/StatusBar2.img/mainBar/chatTarget/*`) |
| Partial | Full skill UI behavior | The Big Bang skill window now mirrors `CUISkill::GetSkillRootVisible` at the tab layer by only exposing the visible skill-book path for the current job, keeps keyboard-selected entries in view while accepting pane-hover wheel scrolling plus `Up` / `Down` / `PageUp` / `PageDown` / `Home` / `End` navigation, lets `Left` / `Right` cycle across the currently visible skill-book tabs without reaching for the mouse, renders the client recommendation banner from a WZ-loaded skill-root/SP-threshold recommendation table with the existing visible-tab levelable-skill fallback when no applicable entry can level, routes `BtSpUp` clicks into live skill-level increments with per-tab SP countdown sourced from the remaining unmaxed levels in the loaded book, loads the Aran-only `Skill/main/Tab/AranButton/Bt1..Bt4` guide buttons so the client `3001..3004 -> OpenSkillGuide` path opens the matching `UIWindow2.img/AranSkillGuide/<grade-1>` overlay page, instantiates the WZ-backed bottom-row `BtRide` / `BtGuildSkill` controls, and now also opens a dedicated WZ-backed `UIWindow2.img/UserList/GuildSkill` owner from both the skill-window guild shortcut and the guild member-list `BtSkill` action, loading the visible guild-skill catalog from `Skill/9100.img` plus `String/Skill.img` with the row recommendation banner, `BtRenewal`, and `BtUp` actions gated by the parsed `reqGuildLevel` formulas instead of bouncing the player back through the generic social list | The simulator now respects the client's visible-root job path, no longer leaves the level-up button dead, follows the client-style recommendation progression more closely, no longer drops the Aran guide or the WZ-backed ride / guild shortcut controls that the Big Bang skill window exposes, and no longer treats guild-skill access as a dead generic-tab fallback; the remaining gap is narrower and mostly limited to the still-unmodeled generic `CUISkill` branches such as the `DualTab` path plus deeper client-accurate guild-skill runtime semantics beyond this dedicated owner shell | `SkillUI.cs`, `SkillUIBigBang.cs`, `UIWindowLoader.cs`, `SkillDataLoader.cs`, `MapSimulator.cs`, `GuildSkillWindow.cs`, `GuildSkillRuntime.cs`, `UserInfoUI.cs`, `SocialListWindow.cs` (`CUISkill::Draw`, `CUISkill::GetSkillRootVisible`, `CUISkill::GetRecommendSKill`, `CUISkill::OnButtonClicked`, `CUISkill::OpenSkillGuide`, `UI/UIWindow2.img/Skill/main/BtRide`, `UI/UIWindow2.img/Skill/main/BtGuildSkill`, `UI/UIWindow2.img/Skill/main/Tab/DualTab`, `UI/UIWindow2.img/Skill/main/Tab/AranButton`, `UI/UIWindow2.img/AranSkillGuide`, `UI/UIWindow2.img/UserList/GuildSkill`, `Skill/9100.img`, `String/Skill.img`) |
| Partial | Item/skill tooltip behavior | Skill hover tooltips now reuse the client `UIWindow2.img/Skill/main/tip0..tip2` frames in the skill window, quick-slot skill hovers, status-bar buff tray, and status-bar cooldown tray; the status-bar buff/cooldown hovers now also follow the same icon-led client-style layout and select the correct left/center/right frame variant instead of stretching those textures as a composite strip, while the cooldown tray continues to surface the live skill description plus remaining cooldown time instead of forcing the player to infer entries from icon art alone, and the Big Bang equipment window now renders live equipped-item icons from the current `CharacterBuild` plus a framed hover tooltip that pulls String.wz category/description text and parsed equip `info` metadata for stat gains, upgrade slots, durability, `reqJob`, `reqPOP`, and slot-specific eligibility lines instead of stopping at slot-plus-item-id stubs; the same richer summary/requirement text now also carries into the Big Bang dragon, mechanic, and android equipment-tab hovers when those companion items expose parsed `CharacterPart` metadata. Full client `UIWindow2.img/ToolTip/Equip` label-canvas rendering, bitmap job-filter blocks, potential/additional-option sections, and broader cross-UI item-tooltip parity are still incomplete | The simulator now has a shared client-style tooltip surface across the main skill HUDs, the Big Bang equipment window no longer leaves equipped-item hovers blank or metadata-thin, and the companion equipment tabs no longer fall back to item-id-only hover stubs, but it still lacks the rest of the client's richer item-tooltip presentation and `ToolTip/Equip`-specific ornamentation | `SkillUIBigBang.cs`, `QuickSlotUI.cs`, `StatusBarUI.cs`, `EquipUIBigBang.cs`, `CharacterData.cs`, `CharacterLoader.cs`, `UIWindowLoader.cs`, `MapSimulator.cs` |
| Partial | Charge bars and richer skill HUD | The simulator still renders the client-timed prepared-skill gauge set for the supported status-bar branches, but it now also routes the Evan/dragon `22151001` / `22121000` family through a separate world-overlay pass instead of the bottom status-bar surface, keeps the client `KeyDownBar2` / `KeyDownBar3` WZ skins on that split path, anchors those dragon bars from the same `bar/origin` data the client insertion path uses, and suppresses the generic skill-name plus ready-label text on the dragon-owned branch so it no longer reuses the status-bar caption treatment; the remaining gap is the rest of the client's branch-specific text or state variants and the exact dragon-layer ownership or placement details beyond the simulator's player-anchored overlay approximation | Prepared/keydown skills now surface closer client timing and HUD feedback instead of a single generic bar, sustained keydown branches expose both their live maintain label and remaining capped-hold bar state more clearly, the previously hidden default-branch families now get the same client-timed HUD treatment, and the Evan/dragon branch no longer falls back to the status-bar path or generic captioning, but the simulator still lacks the rest of the client's richer skill-state UI and some finer dragon-branch placement parity | `StatusBarUI.cs`, `UILoader.cs`, `SkillManager.cs`, `MapSimulator.cs` (`CUserLocal::DrawKeyDownBar`, `CDragon::DrawKeyDownBar`, `CUserLocal::Update`, `get_max_gauge_time`, `UI/Basic.img/KeyDownBar*/bar/origin`) |

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
