# MapSimulator Client Parity Backlog

## Purpose

This document is the single source of truth for MapSimulator parity work against the MapleStory client.

It does three things that the old notes did not do well:

1. Separates shipped work from missing work.
2. Distinguishes broad simulator features from player/avatar parity.
3. Anchors claims to actual code seams and, where relevant, client-side reverse engineering.

## Evidence Used

### Code seams reviewed

- `HaCreator/MapSimulator/UI/MinimapUI.cs`
- `HaCreator/MapSimulator/UI/MouseCursorItem.cs`
- `HaCreator/MapSimulator/UI/PickupNoticeUI.cs`
- `HaCreator/MapSimulator/UI/Windows/AbilityUI.cs`
- `HaCreator/MapSimulator/UI/Windows/AbilityUIBigBang.cs`
- `HaCreator/MapSimulator/UI/Windows/EquipUI.cs`
- `HaCreator/MapSimulator/UI/Windows/EquipUIBigBang.cs`
- `HaCreator/MapSimulator/UI/Windows/InventoryUI.cs`
- `HaCreator/MapSimulator/UI/Windows/InventoryUIBigBang.cs`
- `HaCreator/MapSimulator/UI/Windows/QuestUI.cs`
- `HaCreator/MapSimulator/UI/Windows/QuestUIBigBang.cs`
- `HaCreator/MapSimulator/UI/Windows/SkillMacroUI.cs`

### Client references checked

- `CUIStat::Draw` (captured in simulator layout comments and offsets)
- `CUIStat::DrawTextA` (captured in simulator layout comments and offsets)
- `CUISkillMacro` (captured in simulator window comments)
- `CWvsContext::OnDropPickUpMessage` (captured in `PickupNoticeUI` comments)
- `CUIScreenMsg::ScrMsg_Add` (captured in `PickupNoticeUI` comments)
- `CUIItem::Draw` at `0x7ccf50`
- `CAdminShopDlg::OnCreate` at `0x42ecd0`
- `CAdminShopDlg::Draw` at `0x42fdf0`
- `CAdminShopDlg::OnButtonClicked` at `0x430f90`
- `CUIChannelSelect::OnCreate` at `0x606a10`
- `CUIChannelSelect::Draw` at `0x6056d0`
- `CUIChannelSelect::OnButtonClicked` at `0x6035f0`
- `CUIWorldSelect::OnCreate` at `0x60b660`
- `CUIWorldSelect::OnButtonClicked` at `0x6025e0`
- `CUIChannelShift::OnCreate` at `0x96c160`
- `CUIChannelShift::Draw` at `0x96ccb0`
- `CUIItemMaker::Draw` at `0x7d4980`
- `CUIItemMaker::OnButtonClicked` at `0x7d5dc0`
- `CUIItemUpgrade::Draw` at `0x7c0100`
- `CUIItemUpgrade::OnButtonClicked` at `0x7c0ca0`
- `CUIMapTransfer::Draw` at `0x7df5c0`
- `CUIMapTransfer::OnButtonClicked` at `0x7e0ed0`
- `CUIEquip::Draw` at `0x7aa560`
- `CUIQuestInfo::Draw` at `0x82a780`
- `CUIQuestInfoDetail::Draw` at `0x8248c0`
- `CUIQuestInfoDetail::OnButtonClicked` at `0x834c90`
- `CUIQuestAlarm::Draw` at `0x82d850`
- `CUIStatusBar::OnButtonClicked` at `0x880540`
- `CUIStat::Draw` at `0x864bd0`
- `CUIUserInfo::Draw` at `0x8b3a50`
- `CUIUserInfo::OnButtonClicked` at `0x8b6d80`

Notes:
Direct IDA seam capture is now confirmed for inventory, cash-service, channel-shift, item-maker, item-upgrade, map-transfer, equipment, quest-log, quest-detail, quest-alarm, status-bar utility clicks, stat-window, and user-info owners. The remaining seam-collection gap in this backlog area is primarily the minimap window and any exact `CUISkillMacro` draw or interaction owners, not the broader progression-window family as a whole.

## Client Function Index By Backlog Area

The list below maps this backlog area to the concrete client seams that are already named in code or prior reverse-engineering notes.
These are the first functions or classes to inspect before changing simulator behavior.

### 1. Progression and utility window parity

- `CUIStat::Draw`
- `CUIStat::DrawTextA`
- `CUIItem::Draw` at `0x7ccf50`
- `CAdminShopDlg::OnCreate` at `0x42ecd0`
- `CAdminShopDlg::Draw` at `0x42fdf0`
- `CAdminShopDlg::OnButtonClicked` at `0x430f90`
- `CUIChannelSelect::OnCreate` at `0x606a10`
- `CUIChannelSelect::Draw` at `0x6056d0`
- `CUIChannelSelect::OnButtonClicked` at `0x6035f0`
- `CUIWorldSelect::OnCreate` at `0x60b660`
- `CUIWorldSelect::OnButtonClicked` at `0x6025e0`
- `CUIChannelShift::OnCreate` at `0x96c160`
- `CUIChannelShift::Draw` at `0x96ccb0`
- `CUIItemMaker::Draw` at `0x7d4980`
- `CUIItemMaker::OnButtonClicked` at `0x7d5dc0`
- `CUIItemUpgrade::Draw` at `0x7c0100`
- `CUIItemUpgrade::OnButtonClicked` at `0x7c0ca0`
- `CUIMapTransfer::Draw` at `0x7df5c0`
- `CUIMapTransfer::OnButtonClicked` at `0x7e0ed0`
- `CUIEquip::Draw` at `0x7aa560`
- `CUIQuestInfo::Draw` at `0x82a780`
- `CUIQuestInfoDetail::Draw` at `0x8248c0`
- `CUIQuestInfoDetail::OnButtonClicked` at `0x834c90`
- `CUIQuestAlarm::Draw` at `0x82d850`
- `CUIStatusBar::OnButtonClicked` at `0x880540`
- `CUIUserInfo::Draw` at `0x8b3a50`
- `CUIUserInfo::OnButtonClicked` at `0x8b6d80`
- `CUISkillMacro`
- `CWvsContext::OnDropPickUpMessage`
- `CUIScreenMsg::ScrMsg_Add`

Notes:
`CUIItem::Draw` confirms that the client inventory window owns meso text, per-slot icon and quantity drawing, item-grade frames, disabled-slot overlays, and special overlays such as the active bullet marker. `CAdminShopDlg::OnCreate`, `CAdminShopDlg::Draw`, and `CAdminShopDlg::OnButtonClicked` confirm a dedicated cash-service shop dialog rather than treating cash-shop and MTS access as simple status-bar button stubs. `CUIChannelSelect::OnCreate`, `CUIChannelSelect::Draw`, `CUIChannelSelect::OnButtonClicked`, `CUIWorldSelect::OnCreate`, and `CUIWorldSelect::OnButtonClicked` confirm that the status-bar channel path fans out into explicit world and channel selector owners rather than a generic toggle, while `CUIChannelShift::OnCreate` and `CUIChannelShift::Draw` keep a separate shift or transition surface in play. `CUIItemMaker::Draw` and `CUIItemMaker::OnButtonClicked` confirm a dedicated crafting or maker window rather than burying production flow inside generic inventory handling. `CUIItemUpgrade::Draw` and `CUIItemUpgrade::OnButtonClicked` confirm a separate item-enhancement surface rather than treating scroll or upgrade flow as a generic inventory tooltip action. `CUIMapTransfer::Draw` and `CUIMapTransfer::OnButtonClicked` confirm a dedicated map-transfer window distinct from the minimap shell. `CUIEquip::Draw` confirms that equip-window parity is tied to slot-disable rules, expiration and durability overlays, and job or subjob gating rather than just slot placement. `CUIQuestInfo::Draw`, `CUIQuestInfoDetail::Draw`, and `CUIQuestAlarm::Draw` confirm that quest progression parity includes a separate detail pane with dedicated button handling, category-button rebuilds, scroll-driven list rebuilding, quest-delivery requirement rendering, condition-state coloring, and a standalone quest-alert window rather than only the quest log shell. `CUIStatusBar::OnButtonClicked` confirms the utility-button cluster is a real interaction owner instead of only decorative art. `CUIUserInfo::Draw` and `CUIUserInfo::OnButtonClicked` confirm a separate character-information or profile surface beyond the stat window itself.

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

### 1. Progression and utility window parity

This area is currently absent from the backlog set even though the simulator already has concrete window and feedback implementations.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Minimap window parity | The minimap already supports client-style frame loading, collapse/maximize toggles, drag bounds, and a live player dot derived from world position, but `BtBig` and `BtMap` are still no-op handlers and the simulator does not yet mirror client marker sets, map-name overlays, region/full-map transitions, or richer minimap interaction states | Navigation feedback exists, but it still falls short of the client's actual minimap tool rather than merely needing polish | `MinimapUI.cs`, minimap/window loader path |
| Missing | Map transfer / navigation window parity | IDA shows `CUIMapTransfer` is a separate draw and button-click owner for map-transfer style navigation, but the simulator currently stops at the minimap shell with `BtMap` still acting as a no-op and no dedicated transfer or destination-selection window | Without the standalone transfer surface, the simulator cannot exercise the client's broader navigation workflows that sit above the minimap and portal-only travel | navigation/UI layer, minimap integration, destination-selection flow (`CUIMapTransfer::Draw`, `CUIMapTransfer::OnButtonClicked`) |
| Missing | Cash shop / admin shop dialog parity | The simulator already distinguishes cash-shop preview maps and loads status-bar buttons for Cash Shop and MTS access, but IDA shows the client routes those flows through a dedicated `CAdminShopDlg` lifecycle with its own create, draw, and button-click handling that the simulator does not expose today | Without the dialog owner, the simulator cannot exercise the client's cash-service browsing and button-driven interaction surface even though the entry points are already visible on the HUD | cash-service UI layer, status-bar integration, shop-session flow (`CAdminShopDlg::OnCreate`, `CAdminShopDlg::Draw`, `CAdminShopDlg::OnButtonClicked`) |
| Missing | World and channel selector parity | The simulator already loads the `BtChannel` status-bar button when the UI data provides it, but IDA shows the client routes that flow through dedicated `CUIWorldSelect` and `CUIChannelSelect` owners with their own create, draw, and button-click handling, plus a separate `CUIChannelShift` surface, none of which the simulator exposes today | Channel switching remains a dead-end HUD affordance unless the selector stack is tracked as its own world-selection and channel-selection workflow instead of being collapsed into a generic utility-button bucket | world-selection and channel-selection UI layer, status-bar integration (`CUIWorldSelect::OnCreate`, `CUIWorldSelect::OnButtonClicked`, `CUIChannelSelect::OnCreate`, `CUIChannelSelect::Draw`, `CUIChannelSelect::OnButtonClicked`, `CUIChannelShift::OnCreate`, `CUIChannelShift::Draw`) |
| Partial | Inventory window parity | Both legacy and Big Bang inventory windows already load the correct frame families, tabs, and small vs expanded layouts, but the simulator still does not mirror the client inventory draw surface confirmed in `CUIItem::Draw`: right-aligned meso text, per-slot icon and quantity rendering, grade-frame overlays, disabled-slot overlays in expanded bags, active bullet highlighting, and the inventory-backed item-state checks that drive those visuals | The simulator already exposes the inventory surface, so the remaining gap is real player-facing inventory behavior rather than window existence, and the client evidence now shows that this includes more than drag/drop and tooltip polish | `InventoryUI.cs`, `InventoryUIBigBang.cs`, `UIWindowLoader.cs`, inventory/runtime integration (`CUIItem::Draw`) |
| Missing | Item maker / crafting window parity | `CUIItemMaker::Draw` and `CUIItemMaker::OnButtonClicked` confirm the client owns a dedicated maker or crafting window with its own interaction surface, but the simulator does not currently expose a corresponding crafting runtime, recipe presentation, or button flow | Production and crafting progression remain invisible in parity planning unless they are tracked independently from raw inventory, equip, and enhancement behavior | inventory/runtime integration, crafting UI layer (`CUIItemMaker::Draw`, `CUIItemMaker::OnButtonClicked`) |
| Missing | Item upgrade / enhancement window parity | `CUIItemUpgrade::Draw` and `CUIItemUpgrade::OnButtonClicked` confirm the client owns a dedicated item-upgrade interaction surface with its own button flow, but the simulator does not currently expose a scrolling, enhancement, or item-upgrade runtime or window | Progression-sensitive item workflows remain absent from parity planning unless enhancement is tracked separately from raw inventory and equip presentation | inventory/runtime integration, enhancement UI layer (`CUIItemUpgrade::Draw`, `CUIItemUpgrade::OnButtonClicked`) |
| Partial | Equipment window parity | Legacy and Big Bang equip windows already define slot layouts, tabs, and overlay art, but they still do not present the client `CUIEquip::Draw` behavior surface: slot-disable rules from job/subjob and novice-skill gating, expired-item and zero-durability disable overlays, richer equipped versus cash-slot resolution, live avatar preview sync, non-character tabs, and item drag/drop or compare flows | Equipment-heavy testing needs the actual character equipment surface to behave like the client instead of acting as a mostly decorative frame, and the client owner now makes the missing disable or gating logic concrete | `EquipUI.cs`, `EquipUIBigBang.cs`, `CharacterBuild`, `CharacterLoader`, `CharacterAssembler` (`CUIEquip::Draw`) |
| Partial | Pet, dragon, and android equipment tabs | The Big Bang equip window already loads `BtPet`, `BtDragon`, and `BtAndroid` tab controls and defines slot positions for those companion-facing surfaces, but the simulator still lacks the corresponding companion runtime, equip resolution, and tab-specific item presentation that make those pages meaningful in the client | The buttons and slots exist in the simulator UI today, so this is a real parity gap in companion-facing equipment behavior rather than an absent window shell | `EquipUIBigBang.cs`, `UIWindowLoader.cs`, equip/runtime integration (`CUIEquip::Draw`) |
| Partial | Ability and stat window parity | The simulator already draws both legacy and Big Bang stat windows, supports AP assignment, job-biased auto-assign, and detail-panel toggles, but HP/MP/AP growth rules, full derived-stat formulas, detail-page values, EXP/fame/guild presentation, and button enablement rules are still simplified | Visible stat windows exist, but their underlying calculations and affordances still diverge from client expectations in progression-sensitive testing | `AbilityUI.cs`, `AbilityUIBigBang.cs`, `CharacterBuild`, stat/runtime layer (`CUIStat::Draw`, `CUIStat::DrawTextA`) |
| Missing | Character info / profile window parity | `CUIUserInfo::Draw` and `CUIUserInfo::OnButtonClicked` confirm the client owns a separate user-info surface beyond the base stat window, but the simulator does not currently expose a profile-style window for the broader character information and interaction flow | Without a dedicated user-info surface, the simulator collapses account- and character-facing information into the stat HUD alone even though the client distinguishes those responsibilities | character/profile UI layer, player metadata presentation (`CUIUserInfo::Draw`, `CUIUserInfo::OnButtonClicked`) |
| Partial | Quest log window parity | The simulator already has quest window classes, tab state, quest storage, and selection models, but it still falls well short of the client `CUIQuestInfo::Draw` surface: category-button rebuilds on scroll changes, quest-list row state coloring, reward and requirement icon rows, quest-delivery eligibility checks, detailed accept or complete condition text, and tighter synchronization between list selection and the live quest runtime | Quest interaction now exists in overlays and runtime state, but the standalone quest log remains below client parity in exactly the areas the client draw owner is responsible for, not just generic list rendering | `QuestUI.cs`, `QuestUIBigBang.cs`, `QuestRuntimeManager.cs`, NPC/UI bridge (`CUIQuestInfo::Draw`) |
| Missing | Quest detail window parity | IDA shows `CUIQuestInfoDetail` is a separate quest-detail owner with its own draw and button-click surface, but the simulator does not currently expose a dedicated detail pane that mirrors the client's reward rows, requirement sections, navigation controls, or accept or give-up action flow distinct from the list shell | Without the standalone detail surface, quest progression remains compressed into simplified overlays and list state even though the client splits browsing from per-quest inspection and actions | `QuestUI.cs`, `QuestUIBigBang.cs`, `QuestRuntimeManager.cs`, quest-detail interaction layer (`CUIQuestInfoDetail::Draw`, `CUIQuestInfoDetail::OnButtonClicked`) |
| Missing | Quest alarm / notifier window parity | The simulator backlog did not track the standalone quest-alert window at all, but `CUIQuestAlarm::Draw` shows the client owns a separate maximize/minimizeable quest-progress surface with title counts, per-quest progress rows, mob and item requirement text, meso requirement progress, completion-state coloring, and demand-rect layout that is distinct from both NPC head icons and the quest log | Without this window, progression feedback remains split between in-world quest icons and quest-log shells, while the client also exposes a persistent quest-progress tracker that players rely on during active questing | progression/UI layer, quest runtime, demand rendering (`CUIQuestAlarm::Draw`) |
| Partial | Skill macro parity | The macro window already supports five macro slots, drag/drop skill assignment, editing state, save/delete hooks, and queued execution through `SkillManager`, but client naming validation, macro hotkey/binding integration, party-notify semantics, and final execution cadence are still simplified | Macro support has moved beyond a stub and should be tracked as parity work instead of being hidden as an unowned side feature | `SkillMacroUI.cs`, `SkillManager.cs`, `QuickSlotUI.cs` (`CUISkillMacro`) |
| Partial | Pickup notice parity | The simulator already has a dedicated pickup-notice stack with client-style message lifetimes, stacking, and fade behavior, but it still lacks item-icon drawing, broader reason-string coverage, stricter inventory-space coupling, and the full pickup-result surface the client shows | Pickup feedback exists and is visible in play, so the remaining gap is client-fidelity of message content and presentation | `PickupNoticeUI.cs`, drop/item runtime (`CWvsContext::OnDropPickUpMessage`, `CUIScreenMsg::ScrMsg_Add`) |
| Partial | Cursor and utility-button parity | The simulator already swaps cursor art for normal, pressed, clickable, and NPC-hover states, while the status bar loads several client utility buttons, but richer cursor modes, drag/forbidden/busy feedback, and actual utility-button behaviors such as menu/system/channel/cash-shop flows are still missing even though `CUIStatusBar::OnButtonClicked` confirms the client routes that button cluster through a concrete interaction owner | This is part of the client feel layer and affects almost every window or interaction surface once the rest of the UI becomes more complete | `MouseCursorItem.cs`, `StatusBarUI.cs`, broader UI manager layer (`CUIStatusBar::OnButtonClicked`) |

## Priority Order

If the goal is visible parity first, the next work in this area should be sequenced like this:

1. Window usefulness pass:
   Finish minimap, inventory, equip, and quest-log interactions so the existing windows become usable client-facing tools rather than frame shells.
2. Progression correctness pass:
   Tighten ability/stat formulas, equip presentation, and inventory-backed validations so the windows reflect the runtime accurately.
3. Utility feedback pass:
   Complete pickup notices, macro behavior, cursor states, and status-bar utility-button behavior.
4. Client seam pass:
   Capture direct IDA seams for minimap, inventory, equip, and quest windows so future parity fixes are anchored to named client owners instead of simulator-only structure.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
