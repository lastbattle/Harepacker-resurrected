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
- `HaCreator/MapSimulator/UI/Windows/AdminShopDialogUI.cs`
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
- Progression or utility window groundwork now has a shared seam: named owners for world map, map transfer, cash-service, channel-selection, crafting, enhancement, character-info, and quest-side utility windows are registered with `UIWindowManager`, while minimap and status-bar utility buttons now route into those owners instead of staying entirely dead-ended. The world/channel path now opens dedicated simulator-side `WorldSelect`, `ChannelSelect`, and timed `ChannelShift` surfaces backed by `UIWindow2.img/Channel` art, with the channel grid, centered placement, and per-channel load feedback now aligned to the client-owned selector layout instead of remaining a placeholder-only launch seam.

## Remaining Parity Backlog

### 1. Progression and utility window parity

This area is currently absent from the backlog set even though the simulator already has concrete window and feedback implementations.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Minimap window parity | The minimap already supports client-style frame loading, street or map-name overlays, collapse/maximize toggles, drag bounds, a live player dot derived from world position, `BtBig` / `BtMap` routing into named world-map and map-transfer owners, and the post-Big-Bang `BtNpc` seam now drives WZ-backed NPC marker icons plus an NPC list panel built from `ListNpc`, but the simulator still does not mirror the client's broader marker families, direction overlays, or the actual region/full-map transition runtime behind those launch buttons | Navigation feedback now has real minimap-owned marker and NPC-toggle behavior instead of only decorative art, but it still falls short of the client's full minimap toolchain and transition surfaces | `MinimapUI.cs`, minimap/window loader path |
| Partial | Map transfer / navigation window parity | The simulator now routes minimap `BtMap` into a dedicated WZ-backed `MapTransfer` owner built from the `Teleport*` window art, constrains the destination surface to the client's five visible rows, caps saved teleport destinations to a client-shaped five-vs-ten slot budget based on the loaded `Teleport` vs `Teleport3` art variant, respects teleport-item field limits separately from generic migration checks, and still layers return-map plus portal-derived routes on top of the saved list while driving actual map changes through the existing pending-map seam instead of stopping at the minimap shell, but it still does not mirror the client's full `CUIMapTransfer` behavior surface such as persisted character teleport-rock data, the edit-target/manual entry path, richer world-map integration, or stricter client destination categories beyond the simulator's route synthesis | Navigation testing can now exercise a more client-shaped transfer and destination-selection surface above raw portal travel, but the window still needs deeper character-data sourcing and button-flow parity before it matches the official flow | navigation/UI layer, minimap integration, destination-selection flow (`CUIMapTransfer::Draw`, `CUIMapTransfer::OnButtonClicked`, `CUIMapTransfer::OnCreate`) |
| Partial | Cash shop / admin shop dialog parity | The simulator now replaces the generic Cash Shop and MTS placeholders with a dedicated `AdminShopDialogUI` owner backed by `UIWindow2.img/Shop`, reuses the shop dialog frame plus `BtBuy` / `BtSell` / `BtExit` / `BtRecharge`, keeps Cash Shop and MTS inside the same button-driven surface, routes the status-bar launchers so only the requested service window stays open, and now exposes separate scrollable NPC-offer and user-listing panes with per-row selection, request submission messaging, and a two-step wishlist confirmation flow, but it still does not mirror the full `CAdminShopDlg` behavior surface such as packet-backed shop sessions, live server-populated catalogs, or the client's exact tab, scrollbar, and modal-dialog sequencing | The simulator can now exercise the client cash-service launch path through a dedicated dialog owner with concrete per-item browsing and request-state interactions instead of a dead-end placeholder, while the remaining gap is the deeper live session and presentation fidelity inside that owner | cash-service UI layer, status-bar integration, shop-session flow (`CAdminShopDlg::OnCreate`, `CAdminShopDlg::Draw`, `CAdminShopDlg::OnButtonClicked`) |
| Partial | World and channel selector parity | The simulator now routes `BtChannel` into dedicated `WorldSelect`, `ChannelSelect`, and timed `ChannelShift` windows, uses `UIWindow2.img/Channel` world badges and channel art for the local selector stack, preserves a visible current world/channel choice for the simulator session, expands the world list to the full badge set, aligns the channel grid and window centering to the client `GetRect`/owner layout, and simulates per-world channel availability plus busy/full load bars so invalid targets no longer behave like always-open buttons, but it still lacks the client's packet-backed world population data, exact login-stage request gating, adult-channel rules, and server-driven transition results | Channel switching is no longer a dead-end HUD affordance, and the remaining gap has narrowed to live server data and request-state fidelity rather than missing selector surfaces or always-generic channel entries | world-selection and channel-selection UI layer, status-bar integration (`CUIWorldSelect::OnCreate`, `CUIWorldSelect::OnButtonClicked`, `CUIChannelSelect::OnCreate`, `CUIChannelSelect::Draw`, `CUIChannelSelect::OnButtonClicked`, `CUIChannelShift::OnCreate`, `CUIChannelShift::Draw`) |
| Partial | Inventory window parity | Legacy and Big Bang inventory windows now render right-aligned meso text, per-slot item icons and stack counts, WZ-backed grade and quality markers, disabled-slot overlays, active-slot highlighting, and Big Bang tab or expanded layouts, while the simulator also feeds meso and picked-up items into the inventory surface at runtime, resolves equip pickup icons through `Character/*/*.img` like the client asset families, and now respects `info/slotMax`-style stack splitting and merge limits for stackable pickups, but it still lacks fuller client-accurate slot-unlock progression plus drag/drop and tooltip-side behavior | The simulator inventory is now a usable player-facing surface instead of a frame shell, so the remaining gap is narrowed to client-accurate inventory state rules and interactions rather than basic draw ownership | `InventoryUI.cs`, `InventoryUIBigBang.cs`, `UIWindowLoader.cs`, inventory/runtime integration (`CUIItem::Draw`) |
| Partial | Item maker / crafting window parity | The simulator now replaces the `ItemMaker` placeholder with a dedicated WZ-backed `Maker` window owner, exposes recipe selection plus `BtStart` / `BtCancel` flow, animates the maker gauge during crafting, seeds a starter ore inventory plus meso and catalyst stock, scrolls through the client ETC-material recipe catalog from `Etc/ItemMake.img`, enforces per-recipe level, meso, and catalyst requirements against the shared inventory runtime, and resolves weighted `randomReward` branches during completion, but it still lacks NPC launch parity, the broader non-ETC recipe pages, profession or mastery runtime gating, and the wider `CUIItemMaker` page-state surface beyond this initial progression loop | Production and crafting progression now has a visible dedicated surface and inventory-backed loop instead of a dead-end placeholder, while the remaining gap is the deeper launch path, broader page set, and client-side gating rules that surround that loop | inventory/runtime integration, crafting UI layer (`CUIItemMaker::Draw`, `CUIItemMaker::OnButtonClicked`) |
| Partial | Item upgrade / enhancement window parity | The simulator now registers a dedicated `ItemUpgrade` owner instead of a placeholder, loads Gold Hammer or Vicious Hammer style client art from `UIWindow2.img` or `UIWindow.img`, exposes a button-driven enhancement loop over live equipped items, tracks per-item remaining upgrade slots and success or fail counts, and applies lightweight stat growth back into `CharacterBuild`, but it still lacks inventory-backed scroll consumption, destroy or curse outcomes, NPC or inventory launch seams, and the broader family of enhancement variants such as cube, potential-stamp, and Vega-style result flows | Progression-sensitive item workflows now have a visible dedicated surface and runtime seam, but the simulator still needs item-inventory validation and broader enhancement result coverage before it matches the client's full progression tooling | inventory/runtime integration, enhancement UI layer (`CUIItemUpgrade::Draw`, `CUIItemUpgrade::OnButtonClicked`) |
| Partial | Equipment window parity | Legacy and Big Bang equip windows now share `EquipSlotStateResolver`-driven disable handling for overall-vs-pants conflicts, beginner subjob and two-handed shield gating, Monster Riding slot gating, expired-item overlays, and zero-durability overlays, while both windows stay bound to the live `CharacterBuild` used by the avatar preview, but they still do not present the rest of the client `CUIEquip::Draw` behavior surface such as richer equipped-versus-cash slot resolution, fully wired non-character tabs, and item drag/drop or compare flows | Equipment-heavy testing now has visible disable and break-state feedback on the actual character equipment surface instead of a mostly decorative frame, while the remaining gap is narrowed to cash-slot resolution and deeper interaction fidelity | `EquipUI.cs`, `EquipUIBigBang.cs`, `CharacterBuild`, `CharacterLoader`, `CharacterAssembler`, `EquipSlotStateResolver.cs` (`CUIEquip::Draw`) |
| Partial | Pet, dragon, and android equipment tabs | The Big Bang equip window now loads the WZ-backed companion pane art for `pet`, `dragon`, `mechanic`, and `Android`, keeps the character equipment surface visible while those panes are open, highlights the active tab buttons, populates the Multi Pet pane from the live `PetController` with per-pet icons plus auto-loot markers and tooltips, and now seeds dedicated dragon and android companion equipment runtimes so those pages render live equipped-item icons and tooltips on the client slot geometry instead of stopping at empty-state shells, but the simulator still does not support runtime drag/drop changes, mechanic pane behavior, or deeper client-side validation around companion-specific equip ownership | Companion tab clicks are no longer dead-end toggles, and all three requested companion panes now reflect simulator-owned data, while the remaining gap is narrower client-fidelity around mechanic support and item-management rules rather than owner absence | `EquipUIBigBang.cs`, `UIWindowLoader.cs`, `PetLoader.cs`, `PetController.cs`, `CompanionEquipmentController.cs`, equip/runtime integration (`CUIEquip::Draw`) |
| Partial | Ability and stat window parity | The simulator already draws both legacy and Big Bang stat windows, supports AP assignment, job-biased auto-assign, detail-panel toggles, build-backed guild/fame/EXP text, hands/critical detail values, per-stat AP cap enablement, and Big Bang open-vs-close detail button visibility; the stat rows now also resolve equipped stat bonuses through `CharacterBuild`, the detail pane now derives attack/defense, accuracy/avoidability, hands, speed, and jump from job-aware totals instead of static placeholder fields, and Big Bang HP/MP AP clicks now use class-aware gain ranges with a 30,000 cap instead of a flat `+20`, but the simulator still does not mirror the client's full passive-skill, mastery, and exact per-job formula surface | Visible stat windows now expose runtime-backed totals from equipped gear and class-sensitive HP/MP AP growth instead of only placeholder baselines, but progression-sensitive calculations still diverge from client expectations where passive and mastery effects are missing | `AbilityUI.cs`, `AbilityUIBigBang.cs`, `CharacterBuild`, stat/runtime layer (`CUIStat::Draw`, `CUIStat::DrawTextA`) |
| Partial | Character info / profile window parity | The simulator now loads a dedicated WZ-backed `UserInfo` window from `UIWindow.img/UserInfo` or `UIWindow2.img/UserInfo/character`, routes the status-bar `BtCharacter` button into that owner, keeps the primary profile page build-backed with name/job/level plus rank or guild or alliance fields, and the Big Bang owner now switches into WZ-backed ride, pet, collect, and personality subpages that render simulator-side mount state, active pet data, collection summaries, and optional trait values instead of leaving those buttons decorative, but it still lacks the deeper `CUIUserInfo` interaction surface such as real party or trade submission, family-chart flow, exception-list handling, exact per-page button states, and packet-backed ranking or collection metadata | Character-facing information now has its own launch path and a broader button-driven surface distinct from the stat window, while the remaining gap is the deeper networked workflow and client-accurate subpage behavior that make the official profile window more than a local readout | character/profile UI layer, player metadata presentation (`CUIUserInfo::Draw`, `CUIUserInfo::OnButtonClicked`) |
| Partial | Quest log window parity | The simulator quest windows now render live runtime-backed lists for available, in-progress, completed, and recommended quests, instantiate the client `Tab/enabled` / `Tab/disabled` category buttons plus `BtMyLevel` / `BtAllLevel` filters from `UIWindow.img` or `UIWindow2.img`, keep selection synchronized with quest state mutations, color rows by accept/complete readiness, and now allow remote accept/complete actions through the shared quest-window runtime instead of leaving completion as a message-only stub, but they still do not mirror the client's exact category-button rebuild cadence, icon-info popups, or the separate detail-owner presentation and button-variant behavior tracked below | Quest interaction now has a usable standalone log that reflects the same runtime state as NPC quest overlays and uses WZ-backed quest categories instead of a custom tab strip, while the remaining gap is narrowed to client-fidelity rebuild and detail presentation behavior rather than a blank or stale list shell | `QuestUI.cs`, `QuestUIBigBang.cs`, `QuestRuntimeManager.cs`, `UIWindowLoader.cs`, NPC/UI bridge (`CUIQuestInfo::Draw`) |
| Partial | Quest detail window parity | The simulator now exposes a dedicated `QuestDetail` owner backed by the quest runtime, reuses the client `Quest/quest_info` frame family when present, opens from quest-list selection, renders separate summary/requirements/rewards sections with NPC hints and progress, supports previous/next quest navigation within the active tab, and wires standalone accept or give-up actions plus completion guidance distinct from the list shell, but it still does not match the client's exact button art states, reward icon rows, quest-delivery button variants, or richer condition coloring and demand-rect layout | The simulator now exercises the client's split between quest browsing and per-quest inspection instead of compressing everything into a single log surface, while leaving the remaining gap in presentation fidelity and specialized client-side detail rendering rather than owner absence | `QuestUI.cs`, `QuestUIBigBang.cs`, `QuestDetailWindow.cs`, `QuestRuntimeManager.cs`, quest-detail interaction layer (`CUIQuestInfoDetail::Draw`, `CUIQuestInfoDetail::OnButtonClicked`) |
| Partial | Quest alarm / notifier window parity | The simulator now replaces the `QuestAlarm` placeholder with a dedicated WZ-backed tracker built from `UIWindow.img/UIWindow2.img/QuestAlarm`, supports minimize/maximize toggles, title counts, live in-progress quest rows with mob and item requirement text plus demand-summary lines, completion-state coloring, row selection, dismissible entries, and `Track` or `BtQ` routing into the quest detail flow, but it still does not mirror the client's full `CUIQuestAlarm` behavior such as packet-driven quest pinning, meso-requirement progress rows, richer demand-rect art fidelity, and exact alert-button semantics | Active questing now has a persistent standalone progress tracker instead of forcing players to bounce between NPC head icons and the quest log, while the remaining gap is narrowed to deeper client-specific tracking rules and presentation fidelity rather than the window being absent entirely | progression/UI layer, quest runtime, demand rendering (`CUIQuestAlarm::Draw`) |
| Partial | Skill macro parity | The macro window now enforces trimmed 12-character macro names with inline validation, supports drag-binding selected macros into QuickSlot entries, routes macro hotkeys back through `SkillManager` with party-chat announce semantics, and paces queued casts so the next macro skill waits for the current cast to finish, but bindings still remain simulator-session state rather than a client-accurate persisted macro store and the exact client byte-count or locale validation rules are not yet mirrored | Macro support now behaves like a real runtime surface instead of an editor-only stub, and the remaining gap is narrowed to persistence and stricter client data rules rather than missing interaction ownership | `SkillMacroUI.cs`, `SkillManager.cs`, `QuickSlotUI.cs` (`CUISkillMacro`) |
| Partial | Pickup notice parity | The simulator pickup-notice stack now renders item icons, resolves cached item names and type labels for success messages, records player pickup history, and blocks item pickup with inventory-full or ownership-failure notices before consuming the drop, but it still lacks the client's full pickup-result surface such as broader special-case reason strings, pet or remote-loot variants, and exact screen-message formatting across every pickup path | Pickup feedback now reflects real item metadata and inventory capacity rather than only generic success text, so the remaining gap is narrowed to the deeper result matrix and presentation edge cases the client exposes | `PickupNoticeUI.cs`, drop/item runtime (`CWvsContext::OnDropPickUpMessage`, `CUIScreenMsg::ScrMsg_Add`) |
| Partial | Cursor and utility-button parity | The simulator now builds dedicated WZ-backed `Menu` and `System` popup owners from `StatusBar2.img/mainBar/Menu` and `StatusBar2.img/mainBar/System`, routes the status-bar `BtMenu` and `BtSystem` buttons through those owners, fans their core entries back into inventory/equip/stat/skill/quest/character-info and channel-selection flows instead of leaving them as dead launchers, and switches the cursor to the client hold art during active skill, quick-slot, and window drag flows, but forbidden/busy feedback plus the broader system submenu behaviors still remain simplified or message-only compared with the client `CUIStatusBar::OnButtonClicked` surface | This is part of the client feel layer and affects almost every window or interaction surface once the rest of the UI becomes more complete, and the remaining gap is now the deeper cursor-state matrix and submenu fidelity rather than dead utility-button seams | `MouseCursorItem.cs`, `StatusBarUI.cs`, `UILoader.cs`, `StatusBarPopupMenuWindow.cs`, broader UI manager layer (`CUIStatusBar::OnButtonClicked`) |

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
