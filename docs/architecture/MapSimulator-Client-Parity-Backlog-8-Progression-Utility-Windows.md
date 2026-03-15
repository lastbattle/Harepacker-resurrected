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

Notes:
Direct IDA seam capture for minimap, inventory, equip, and quest windows is still missing from the backlog set. Those areas already have simulator surfaces and should be part of future client-seam collection instead of staying undocumented.

## Client Function Index By Backlog Area

The list below maps this backlog area to the concrete client seams that are already named in code or prior reverse-engineering notes.
These are the first functions or classes to inspect before changing simulator behavior.

### 1. Progression and utility window parity

- `CUIStat::Draw`
- `CUIStat::DrawTextA`
- `CUISkillMacro`
- `CWvsContext::OnDropPickUpMessage`
- `CUIScreenMsg::ScrMsg_Add`

Notes:
The current backlog set does not yet carry direct seam names for the minimap, inventory, equip, or quest windows. That is a documentation gap, not evidence that the client has no corresponding behavior.

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
| Partial | Inventory window parity | Both legacy and Big Bang inventory windows already load the correct frame families, tabs, and small vs expanded layouts, but slot contents are still mostly scaffolding: item icons, counts, scroll views, drag/drop, use behavior, tooltips, sort/gather actions, meso sync, and inventory-backed validation are not fully wired | The simulator already exposes the inventory surface, so the remaining gap is real player-facing inventory behavior rather than window existence | `InventoryUI.cs`, `InventoryUIBigBang.cs`, `UIWindowLoader.cs`, inventory/runtime integration |
| Partial | Equipment window parity | Legacy and Big Bang equip windows already define slot layouts, tabs, and overlay art, but they still do not present full client-parity equip interaction such as live avatar preview sync, richer non-character tabs, item drag/drop and compare flows, slot restrictions, or tooltip/stat presentation | Equipment-heavy testing needs the actual character equipment surface to behave like the client instead of acting as a mostly decorative frame | `EquipUI.cs`, `EquipUIBigBang.cs`, `CharacterBuild`, `CharacterLoader`, `CharacterAssembler` |
| Partial | Ability and stat window parity | The simulator already draws both legacy and Big Bang stat windows, supports AP assignment, job-biased auto-assign, and detail-panel toggles, but HP/MP/AP growth rules, full derived-stat formulas, detail-page values, EXP/fame/guild presentation, and button enablement rules are still simplified | Visible stat windows exist, but their underlying calculations and affordances still diverge from client expectations in progression-sensitive testing | `AbilityUI.cs`, `AbilityUIBigBang.cs`, `CharacterBuild`, stat/runtime layer (`CUIStat::Draw`, `CUIStat::DrawTextA`) |
| Partial | Quest log window parity | The simulator already has quest window classes, tab state, quest storage, and selection models, but the actual list and detail rendering, reward or requirement presentation, client-style row interactions, scroll behavior, and synchronization with the live quest runtime are still incomplete | Quest interaction now exists in overlays and runtime state, but the standalone quest log remains below client parity and is not currently tracked anywhere else | `QuestUI.cs`, `QuestUIBigBang.cs`, `QuestRuntimeManager.cs`, NPC/UI bridge |
| Partial | Skill macro parity | The macro window already supports five macro slots, drag/drop skill assignment, editing state, save/delete hooks, and queued execution through `SkillManager`, but client naming validation, macro hotkey/binding integration, party-notify semantics, and final execution cadence are still simplified | Macro support has moved beyond a stub and should be tracked as parity work instead of being hidden as an unowned side feature | `SkillMacroUI.cs`, `SkillManager.cs`, `QuickSlotUI.cs` (`CUISkillMacro`) |
| Partial | Pickup notice parity | The simulator already has a dedicated pickup-notice stack with client-style message lifetimes, stacking, and fade behavior, but it still lacks item-icon drawing, broader reason-string coverage, stricter inventory-space coupling, and the full pickup-result surface the client shows | Pickup feedback exists and is visible in play, so the remaining gap is client-fidelity of message content and presentation | `PickupNoticeUI.cs`, drop/item runtime (`CWvsContext::OnDropPickUpMessage`, `CUIScreenMsg::ScrMsg_Add`) |
| Partial | Cursor and utility-button parity | The simulator already swaps cursor art for normal, pressed, clickable, and NPC-hover states, while the status bar loads several client utility buttons, but richer cursor modes, drag/forbidden/busy feedback, and actual utility-button behaviors such as menu/system/channel/cash-shop flows are still missing | This is part of the client feel layer and affects almost every window or interaction surface once the rest of the UI becomes more complete | `MouseCursorItem.cs`, `StatusBarUI.cs`, broader UI manager layer |

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
