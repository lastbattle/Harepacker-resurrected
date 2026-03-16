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
- `HaCreator/MapSimulator/Managers/GameStateManager.cs`
- `HaCreator/MapSimulator/Pools/DropPool.cs`
- `HaCreator/MapSimulator/UI/Windows/EquipUI.cs`
- `HaCreator/MapSimulator/UI/Windows/EquipUIBigBang.cs`
- `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`
- `HaCreator/MapSimulator/Animation/AnimationKeys.cs`

### Client references checked

- `CPet::Update` at `0x6a4980`
- `CUser::SetActivePet` at `0x8e40e0`
- `CUser::PetAutoSpeaking` at `0x8deb10`
- `CDropPool::TryPickUpDropByPet` at `0x511ab0`
- `CUser::SetActivePortableChair` at `0x8eff80`
- `CMiniRoomBaseDlg::Draw` at `0x6390d0`
- `CMemoListDlg::OnButtonClicked` at `0x624170`
- `CMemoListDlg::OnCreate` at `0x626120`
- `CMemoListDlg::Draw` at `0x625810`
- `CTrunkDlg::Draw` at `0x769850`
- `CUIMapleTV::Draw` at `0x7945e0`
- `CMapleTVMan::OnClearMessage` at `0x60f2f0`
- `CMapleTVMan::OnSendMessageResult` at `0x60f5f0`
- `CMapleTVMan::OnSetMessage` at `0x60f870`
- `CMapleTVMan::Init` at `0x60fbc0`
- `CUIFriendGroup::Draw` at `0x7bc770`
- `CUIMessenger::Draw` at `0x7f5c70`
- `CUIMessenger::OnButtonClicked` at `0x7f6380`
- `CUIUserList::Draw` at `0x8d0cd0`
- `CUIGuildBBS::Draw` at `0x7c82b0`
- `CUIFamilyChart::Draw` at `0x7b6760`
- `CEntrustedShopDlg::Draw` at `0x51f930`
- `CPersonalShopDlg::Draw` at `0x698d50`

## Client Function Index By Backlog Area

The list below maps this backlog area to concrete client seams confirmed in IDA.
These are the first functions to inspect before changing simulator behavior.

### 1. Companion and social-system parity

- `CPet::Update` at `0x6a4980`
- `CUser::SetActivePet` at `0x8e40e0`
- `CUser::PetAutoSpeaking` at `0x8deb10`
- `CDropPool::TryPickUpDropByPet` at `0x511ab0`
- `CUser::SetActivePortableChair` at `0x8eff80`
- `CMiniRoomBaseDlg::Draw` at `0x6390d0`
- `CMemoListDlg::OnButtonClicked` at `0x624170`
- `CMemoListDlg::OnCreate` at `0x626120`
- `CMemoListDlg::Draw` at `0x625810`
- `CTrunkDlg::Draw` at `0x769850`
- `CUIMapleTV::Draw` at `0x7945e0`
- `CMapleTVMan::OnClearMessage` at `0x60f2f0`
- `CMapleTVMan::OnSendMessageResult` at `0x60f5f0`
- `CMapleTVMan::OnSetMessage` at `0x60f870`
- `CMapleTVMan::Init` at `0x60fbc0`
- `CUIFriendGroup::Draw` at `0x7bc770`
- `CUIMessenger::Draw` at `0x7f5c70`
- `CUIMessenger::OnButtonClicked` at `0x7f6380`
- `CUIUserList::Draw` at `0x8d0cd0`
- `CUIGuildBBS::Draw` at `0x7c82b0`
- `CUIFamilyChart::Draw` at `0x7b6760`
- `CEntrustedShopDlg::Draw` at `0x51f930`
- `CPersonalShopDlg::Draw` at `0x698d50`

Notes:
IDA shows a real client-owned surface here, not a grab bag of leftovers. `CPet::Update` covers pet movement, ladder-aware hanging, random actions, auto-pickup, and auto-speaking timers; `CUser::SetActivePet` confirms ordered multi-pet activation on the avatar; `CUser::SetActivePortableChair` shows chair visuals are layered avatar state rather than only a sit action; `CMemoListDlg` confirms a dedicated memo or note inbox rather than treating all player communication as chat; `CMapleTVMan` shows MapleTV has a real message lifecycle and packet-driven manager instead of being only a draw shell; and the draw owners for MiniRoom, trunk, MapleTV, messenger, friend or guild or alliance lists, guild BBS, family chart, and personal shop all confirm dedicated UI or gameplay systems that are currently outside the existing backlog set.

## Current State Summary

This area is not completely blank in the simulator, but it is only represented by fragments today.
The project currently includes:

- Pet drop chasing and pickup helpers in `DropPool.cs`, explicitly annotated against `CDropPool::TryPickUpDropByPet`.
- A simulator-owned pet runtime base in `PlayerManager` and `HaCreator/MapSimulator/Companions/`, including an ordered active-pet roster, live pet update loop, WZ-backed pet frame loading, slot-aware ladder or rope back-hang anchors, split behind or in-front pet rendering passes, and the client-confirmed pickup-forbidden gate for map `209080000`.
- Legacy and Big Bang equipment windows that already load pet, dragon, and android tab buttons and slot layouts.
- Sit-state animation keys and install-item drop categorization, which means chair-facing behavior is not entirely absent from the runtime vocabulary.
- Cash-shop-map gating flags in `MapSimulator.cs` and `GameStateManager.cs`, which indicates the simulator already distinguishes that map family even though it does not simulate the broader client surfaces there.

The gap is not whether these systems are acknowledged at all.
The gap is that the simulator does not yet reproduce the client's actual companion runtime, social windows, room systems, storage flows, or chair-backed avatar layering.

## Remaining Parity Backlog

### 1. Companion and social-system parity

This area is currently unowned by the backlog set even though both the client and simulator already expose concrete seams here.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Pet auto-loot parity | `PlayerManager` now owns a live pet runtime that drives `DropPool` from active pet movement, per-pet auto-loot toggles, owner-linked request timing, and the client-confirmed pickup-forbidden gate in map `209080000`, so the existing `DropPool` pet-loot rules are exercised end to end instead of staying detached helper logic | Auto-loot can now be validated through the same pet-owned update loop shape the client uses, which unblocks in-field companion pickup checks instead of limiting them to direct `DropPool` calls | `DropPool.cs`, `PlayerManager.cs`, `HaCreator/MapSimulator/Companions/` (`CDropPool::TryPickUpDropByPet`, `CPet::Update`, `CPet::IsInPickupForbiddenMap`) |
| Partial | Active pet lifecycle and multi-pet rendering | The simulator now exposes an ordered active-pet roster with client-shaped insert or remove reindexing, slot-aware ladder or rope back-hang anchors, and split behind or in-front pet rendering so active pets can climb on the avatar instead of always trailing on the ground, but it still approximates the client's exact under-face layering and special multi-pet hang frame tables owned by `CPet::Update` and `CUser::SetActivePet` | Companion-heavy gameplay and visual parity now cover the most visible activation and climbing cases, which makes multi-pet fields feel substantially closer to the client even before the remaining per-frame hang tables are mirrored exactly | companion runtime, avatar or field-entity layer (`CPet::Update`, `CUser::SetActivePet`) |
| Implemented | Pet, dragon, and android equip-surface parity | `EquipUIBigBang` now backs the post-Big Bang `BtPet`, `BtDragon`, and `BtAndroid` tabs with live pet roster data plus dragon and android equipment presentation and tooltips, while the legacy `EquipUI` path now loads the client's `UIWindow/Equip` pet and dragon companion panes, wires `BtPetEquipShow` or `BtPetEquipHide`, `BtPet1` to `BtPet3`, and `BtDragonEquip`, and feeds those surfaces from the same simulator pet and dragon controllers instead of leaving them as dead art | Companion equipment parity is now visible in both UI generations, which means active pet order, dragon defaults, and android defaults can be exercised through the same equip-surface flows the client exposes instead of being confined to backend-only state | `EquipUI.cs`, `EquipUIBigBang.cs`, `UIWindowLoader.cs`, `MapSimulator.cs` |
| Implemented | Pet speech, commands, and idle feedback | The simulator now loads WZ-backed pet command tables, level-banded command gates, reaction speech, slang feedback, and food-feedback dialog from `Item/Pet/*.img` plus `String/PetDialog.img`, routes submitted chat text through the active pet roster, lets `PetRuntime` own command-triggered reactions, timed auto-speaking balloons, and client-shaped idle action loops, and exposes simulator-side pet-level and dialog triggers so those WZ surfaces can be exercised without a separate pet-care subsystem | Companion parity is no longer limited to follow visuals because pets now answer chat-triggered commands using the correct WZ level bands and emit in-world speech or idle feedback through the runtime, which makes the field feel much closer to the client and gives parity work a concrete seam for remaining pet-care systems instead of leaving pet feedback as placeholder behavior | `HaCreator/MapSimulator/Companions/`, `MapSimulator.cs`, `MapSimulatorChat.cs` (`CPet::Update`, `CUser::PetAutoSpeaking`) |
| Partial | Portable chair and install-item parity | The simulator now loads WZ-backed setup-chair layers from `Item/Install/0301*.img`, attaches their `effect` or `effect2` animations as avatar-bound sit overlays, and exposes an owned-item `/chair <itemId|clear>` runtime path so portable chairs are no longer just generic install drops; however, the client-owned pair-chair flow, per-chair sit-action variants, and riding-chair handoff via `info/tamingMob` still remain | Chair usage now exercises the client-shaped additional-layer seam instead of collapsing entirely into a plain sit pose, which makes setup-item testing and visible avatar composition checks possible even before the remaining couple-chair and riding-chair cases land | avatar additional-layer path, install-item runtime (`CUser::SetActivePortableChair`) |
| Partial | Mini room, shop room, and trader-room parity | The simulator now registers client-backed social-room shells for MiniRoom, PersonalShop, EntrustedShop, and TradingRoom using `UIWindow(.2).img/Minigame`, `PersonalShop`, `MemberShop`, and `TradingRoom` art, routes existing character-info trade and item buttons plus dedicated simulator hotkeys into those windows, and backs each shell with occupant lists and room-state transitions for ready, start, arrange, claim, offer, and lock actions; however, the underlying item escrow, wager, visitor persistence, and packet-driven room gameplay remain simulator-owned placeholders rather than full client logic | Room systems are no longer absent from the simulator surface, which means companion and social-system parity can now exercise the dedicated room dialogs and their visible state changes before deeper gameplay or network parity lands | room-system runtime, room UI layer (`CMiniRoomBaseDlg::Draw`, `CPersonalShopDlg::Draw`, `CEntrustedShopDlg::Draw`) |
| Partial | Trunk or storage parity | The simulator now loads the WZ-backed trunk window, exposes per-tab storage state with row selection, deposit/withdraw item flows, sort handling, dual storage-versus-inventory meso presentation anchored to the client draw positions, and a simulator launch shortcut, but NPC-backed access flow, typed meso amounts, and server-authored storage capacity expansion rules are still unmodeled | Storage handling is now testable through its own runtime and window surface instead of being inferred from the regular inventory window alone, even though the surrounding NPC and account-state rules still need parity work | `UIWindowLoader.cs`, `TrunkUI.cs`, `InventoryUI.cs` (`CTrunkDlg::Draw`) |
| Partial | Memo and mailbox parity | The simulator now exposes a dedicated memo mailbox window backed by `UIWindow.img/Memo`, a simulator-owned inbox delivery model, unread/read markers, a status-bar memo launcher plus simulator shortcut, and a button-driven read or keep or delete flow instead of collapsing notes into generic social feedback, but compose or send-note parity, attachment claim flow, and the broader mailbox lifecycle around `UIWindow2.img/Memo` still remain unmodeled | Player communication is no longer limited to whisper and messenger placeholders because memo delivery can now be exercised through its own inbox surface and direct launcher path, but more client-specific mailbox actions still need follow-up work before this area is complete | social UI layer, message or inbox model (`CMemoListDlg::OnCreate`, `CMemoListDlg::Draw`, `CMemoListDlg::OnButtonClicked`) |
| Partial | Messenger parity | The simulator now exposes a dedicated Messenger window backed by `UIWindow2.img/Messenger` frames, min or max states, and WZ-authored `BtEnter` or `BtClame` or `BtMin` or `BtMax` controls, plus a simulator-owned messenger runtime that tracks slot selection, inviteable contacts, occupant presence cards, and a lightweight message log launched from the status-bar `BtMSN` menu entry; however, it still lacks client-authentic text entry, live network presence, and the fuller invite or leave or whisper packet flow behind `CUIMessenger::OnButtonClicked` | Player-to-player coordination is no longer blocked on the complete absence of a Messenger shell because the simulator now has a dedicated presence surface with button-driven invite and whisper actions, even though the deeper client-backed messaging lifecycle is still missing | social UI layer, recipient or presence model (`CUIMessenger::Draw`, `CUIMessenger::OnButtonClicked`) |
| Missing | Friend, party, guild, alliance, and blacklist window parity | `CUIFriendGroup::Draw` and `CUIUserList::Draw` confirm the client owns dedicated list-building, scroll, tab, and location-summary surfaces for friends, party, guild, alliance, expedition, and blacklist data, but the simulator does not currently surface those windows or backing social models | Once other UI windows approach parity, these missing social windows become one of the most visible remaining client gaps | social UI layer, party or guild data model (`CUIFriendGroup::Draw`, `CUIUserList::Draw`) |
| Missing | Guild BBS parity | `CUIGuildBBS::Draw` shows a full guild-board UI with thread lists, comment rows, marks, dates, and write-mode surfaces, but there is no simulator-owned counterpart today | Guild workflows remain absent from parity planning unless they are tracked separately from the basic guild member list surface | social UI layer, guild data model (`CUIGuildBBS::Draw`) |
| Missing | Family chart parity | `CUIFamilyChart::Draw` confirms a dedicated family-tree and family-statistics window, but the simulator does not currently expose family data, chart layout, or family-count feedback | The family system is a distinct progression and social surface that does not fit under the existing quest or stat-window backlog | family runtime, family UI layer (`CUIFamilyChart::Draw`) |
| Missing | MapleTV and broadcast parity | `CUIMapleTV::Draw` confirms the client owns a distinct broadcast or overlay window for MapleTV-style announcements, and `CMapleTVMan::Init`, `CMapleTVMan::OnClearMessage`, `CMapleTVMan::OnSendMessageResult`, and `CMapleTVMan::OnSetMessage` show a dedicated packet-driven message lifecycle behind it, but the simulator does not currently expose that presentation surface, manager state, or any way to exercise those social broadcast flows | Broadcast UI is a visible part of the social client surface and remains entirely absent from parity planning today, which hides a separate game-feedback gap once chat and window systems are otherwise present | social broadcast layer, timed overlay UI, broadcast-state manager (`CUIMapleTV::Draw`, `CMapleTVMan::Init`, `CMapleTVMan::OnClearMessage`, `CMapleTVMan::OnSendMessageResult`, `CMapleTVMan::OnSetMessage`) |

## Priority Order

If the goal is visible parity first, the next work in this area should be sequenced like this:

1. Companion runtime pass:
   Add live pet entities, activation ordering, and chair-backed avatar layers so the simulator can represent the most visible companion-state surfaces in-field.
2. Social utility pass:
   Add trunk, memo, friend or guild or alliance list, and blacklist shells so the common account-facing windows exist before deeper room systems.
3. Room-system pass:
   Add MiniRoom, personal shop, and entrusted shop runtime shells once the shared social and inventory primitives are in place.
4. Feedback pass:
   Close pet speech, companion equip tabs, and other secondary UI behaviors after the runtime owners exist.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
