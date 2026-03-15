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
| Partial | Pet auto-loot parity | The simulator already has pet-target selection, pickup range and cooldown rules, ownership checks, exception filtering, and pickup callbacks in `DropPool.cs`, but it still lacks the client pet object that actually drives those requests from live pet movement, per-pet loot toggles, forbidden-map checks, and request timing owned by `CPet::Update` | Auto-loot logic exists, but it is still decoupled from the companion runtime that the client uses, so pet-enabled maps cannot be validated end to end | `DropPool.cs` (`CDropPool::TryPickUpDropByPet`, `CPet::Update`) |
| Missing | Active pet lifecycle and multi-pet rendering | IDA confirms `CUser::SetActivePet` maintains an ordered three-pet roster and `CPet::Update` owns movement, frame advancement, hang or back behavior, random idle actions, and auto-speaking timers, but the simulator does not currently expose a live pet entity, multi-pet attachment order, or pet-to-owner movement controller | Companion-heavy gameplay and visual parity stay blocked until pets exist as first-class field entities rather than only as drop-pickup helpers or equip-tab placeholders | companion runtime, avatar or field-entity layer (`CPet::Update`, `CUser::SetActivePet`) |
| Partial | Pet, dragon, and android equip-surface parity | The simulator already loads `BtPet`, `BtDragon`, and `BtAndroid` controls and corresponding equip-tab slot layouts, but those tabs still have no backing companion runtime, item presentation, or client-facing activation flow | The UI surface exists today, so this is a concrete parity gap rather than a speculative future feature | `EquipUI.cs`, `EquipUIBigBang.cs`, `UIWindowLoader.cs` |
| Missing | Pet speech, commands, and idle feedback | `CPet::Update` and `CUser::PetAutoSpeaking` show that the client owns timed pet speech and behavior loops, but the simulator currently tracks only NPC idle balloons and explicitly leaves pet auto-speech unmodeled | Companion parity is not only visual follow behavior; pets also contribute continuous in-world feedback that is currently absent | companion runtime, chat or balloon layer (`CPet::Update`, `CUser::PetAutoSpeaking`) |
| Partial | Portable chair and install-item parity | The simulator already has a `sit` action key and install-item drop categorization, but `CUser::SetActivePortableChair` shows the client also loads chair-specific additional layers, pair-chair assets, animation loops, and riding-chair handoff, none of which are modeled today | Chair usage affects both avatar composition and social interaction testing, so treating it as a plain sit action leaves a real client-visible gap | avatar additional-layer path, install-item runtime (`CUser::SetActivePortableChair`) |
| Missing | Mini room, shop room, and trader-room parity | IDA confirms dedicated draw owners for `CMiniRoomBaseDlg`, `CPersonalShopDlg`, and `CEntrustedShopDlg`, but the simulator does not currently expose Omok or Match Cards or trade room or hired shop shells, occupant lists, or room-state UI | These are distinct social-gameplay systems in the client, not variants of ordinary chat or inventory windows | room-system runtime, room UI layer (`CMiniRoomBaseDlg::Draw`, `CPersonalShopDlg::Draw`, `CEntrustedShopDlg::Draw`) |
| Missing | Trunk or storage parity | `CTrunkDlg::Draw` shows the client owns a separate storage window with item in/out flows and dual money presentation, but the simulator does not currently expose a trunk runtime or storage UI | Storage handling is progression-facing behavior that cannot be inferred from the regular inventory window alone | storage runtime, inventory or UI layer (`CTrunkDlg::Draw`) |
| Missing | Memo and mailbox parity | `CMemoListDlg::OnCreate`, `CMemoListDlg::Draw`, and `CMemoListDlg::OnButtonClicked` confirm the client owns a dedicated memo-list or mailbox-style social window with its own list-building, read-state, and button-driven flow, but the simulator does not currently expose a note inbox, delivery model, or memo UI shell | Player communication in the client is broader than whisper and messenger presence, and memo delivery stays untracked unless it is separated from the generic social-window bucket | social UI layer, message or inbox model (`CMemoListDlg::OnCreate`, `CMemoListDlg::Draw`, `CMemoListDlg::OnButtonClicked`) |
| Missing | Messenger parity | `CUIMessenger::Draw` and `CUIMessenger::OnButtonClicked` confirm a dedicated messenger window with its own row layout, invite or whisper controls, and button-driven interaction flow, but the simulator does not currently expose a messenger model or UI shell | Player-to-player coordination remains incomplete without the lightweight chat-presence surface that sits between whispering and full party or guild systems in the client | social UI layer, recipient or presence model (`CUIMessenger::Draw`, `CUIMessenger::OnButtonClicked`) |
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
