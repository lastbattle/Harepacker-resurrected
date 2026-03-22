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
| Implemented | Active pet lifecycle and multi-pet rendering | The simulator now mirrors the client-owned active-pet lifecycle and climb rendering path end to end: active pets still use `CUser::SetActivePet`-shaped insert or remove reindexing, ladder or rope back-hang anchors stay rooted on the avatar body origin, hanging pets still enter the avatar stack through the dedicated under-face render plane, and the primary pet now switches to the same generic multi-pet hang table the client loads from `Effect/PetEff.img/Basic/hang` when `CPet::Update` resolves raw action `8` with an additional active pet present | Companion-heavy gameplay and visual parity now covers the visible activation, climb anchoring, hang layering, and the client-only primary multi-pet hang fallback that makes ladder or rope scenes with multiple pets match the official client more closely instead of reusing pet-specific hang frames in cases where the client does not | companion runtime, avatar or field-entity layer (`CPet::Update`, `CPet::PrepareActionLayer`, `CUser::SetActivePet`) |
| Implemented | Pet, dragon, and android equip-surface parity | `EquipUIBigBang` now backs the post-Big Bang `BtPet`, `BtDragon`, and `BtAndroid` tabs with live pet roster data plus dragon and android equipment presentation and tooltips, while the legacy `EquipUI` path now loads the client's `UIWindow/Equip` pet and dragon companion panes, wires `BtPetEquipShow` or `BtPetEquipHide`, `BtPet1` to `BtPet3`, and `BtDragonEquip`, and feeds those surfaces from the same simulator pet and dragon controllers instead of leaving them as dead art; the android default-equipment controller also now respects the client-facing `Top + Pants` versus `Overall` contract exposed by `UIWindow2.img/Equip/character/BtAndroid/ToolTip` instead of synthesizing an `overall + pants` fallback mix | Companion equipment parity is now visible in both UI generations, which means active pet order, dragon defaults, and android defaults can be exercised through the same equip-surface flows the client exposes instead of being confined to backend-only state | `EquipUI.cs`, `EquipUIBigBang.cs`, `UIWindowLoader.cs`, `MapSimulator.cs`, `CompanionEquipmentController.cs` |
| Implemented | Pet speech, commands, and idle feedback | The simulator now loads WZ-backed pet command tables, level-banded command gates, reaction speech, slang feedback, food-feedback dialog, and auto-speech event lines from `Item/Pet/*.img` plus `String/PetDialog.img`, routes submitted chat text through the active pet roster, lets `PetRuntime` own command-triggered reactions, timed auto-speaking balloons, and client-shaped idle action loops, automatically triggers level-up, pre-level-up, and low-HP speech, and exposes simulator-side `/petlevel`, `/petslang`, `/petfeed`, and `/petevent` hooks so the remaining `rest`, `e_NOHPPotion`, and `e_NOMPPotion` WZ surfaces can be exercised without a separate pet-care subsystem | Companion parity is no longer limited to follow visuals because pets now answer chat-triggered commands using the correct WZ level bands, emit in-world speech or idle feedback through the runtime, and expose the full loaded auto-speech event set to simulator-side verification, which makes the field feel much closer to the client and gives parity work a concrete seam for remaining pet-care systems instead of leaving pet feedback as placeholder behavior | `HaCreator/MapSimulator/Companions/`, `MapSimulator.cs`, `MapSimulatorChat.cs` (`CPet::Update`, `CUser::PetAutoSpeaking`) |
| Partial | Portable chair and install-item parity | The simulator now loads WZ-backed setup-chair layers from `Item/Install/0301*.img`, attaches their `effect` or `effect2` animations as avatar-bound sit overlays, exposes an owned-item `/chair <itemId|clear>` runtime path, mirrors the client-confirmed riding-chair handoff by equipping `info/tamingMob` mounts while the chair is active and restoring the previous taming-mob state on stand-up, and now uses couple-chair `info/distanceX` and `info/distanceY` metadata to render a second seated avatar preview for `3012xxx` chairs so pair spacing can be checked locally; however, the true two-user `CUserPool` pairing handshake and midpoint couple-effect path behind `CUser::SetCoupleChairEffect` still remain unmodeled | Chair usage now exercises the client-shaped additional-layer seam, riding-chair mount handoff, and local pair-chair seat spacing instead of collapsing entirely into a plain sit pose, which makes setup-item testing and visible avatar composition checks possible before the remaining cross-user couple-effect flow lands | avatar additional-layer path, install-item runtime (`CUser::SetActivePortableChair`) |
| Partial | Mini room, shop room, and trader-room parity | The simulator now registers client-backed social-room shells for MiniRoom, PersonalShop, EntrustedShop, and TradingRoom using `UIWindow(.2).img/Minigame`, `PersonalShop`, `MemberShop`, and `TradingRoom` art, routes existing character-info trade and item buttons plus keyboard hotkeys into those windows, binds the room runtimes to the live simulator inventory, and backs each shell with inventory-backed listing or escrow rows, mini-room wager escrow and settlement, persisted personal-shop visitor and blacklist lists, trade completion or refund flows, and a `/socialroom ... [packet] ...` command surface that can drive packet-shaped room actions through the same runtime; however, client-authentic remote participant inventories, long-lived cross-session persistence, and the real packet or server gameplay handshake for Omok, shop visitors, hired merchant uptime, and two-party trade acceptance still remain incomplete compared with the client | Room systems now exercise the same dedicated dialog chrome with simulator-owned inventory and ledger state behind it, which moves this backlog row beyond sample shells and makes room escrow, settlement, visitor persistence, and packet-style command verification testable before the deeper network-authored client lifecycle is modeled | room-system runtime, room UI layer (`CMiniRoomBaseDlg::Draw`, `CPersonalShopDlg::Draw`, `CEntrustedShopDlg::Draw`) |
| Partial | Trunk or storage parity | The simulator now loads the WZ-backed trunk window, backs it with a shared `SimulatorStorageRuntime`, exposes per-tab storage state with row selection, deposit/withdraw item flows, sort handling, dual storage-versus-inventory meso presentation anchored to the client draw positions, storage-keeper NPC launch entries derived from `String/Npc.img` `func = "Storage Keeper"` style metadata, client-shaped typed meso deposit/withdraw prompts on `BtOutCoin` and `BtInCoin`, a live Cash Shop storage-slot expansion path that advances the same runtime in 4-slot steps, derives storage access from the login roster so the active character name is checked against shared-account ownership instead of the window instance, and now persists storage contents, meso, and slot expansions to a simulator-side account store keyed by the active simulator world so account storage survives roster rebuilds and app restarts rather than resetting with the local session; however, the real server-authored storage expansion entitlement rules, billing/auth checks, and account ownership outside the simulator-authored local account scope are still unmodeled | Storage handling is now testable through its own runtime, NPC talk surface, typed meso flow, shared-character access gate, persistent account-owned contents and slot growth, and expansion purchase path instead of being inferred from the regular inventory window alone, while the remaining gap is narrowed to true server-side entitlement, billing, and account-auth authority | `UIWindowLoader.cs`, `TrunkUI.cs`, `AdminShopDialogUI.cs`, `SimulatorStorageRuntime.cs`, `StorageAccountStore.cs`, `QuestRuntimeManager.cs`, `MapSimulator.cs` (`CTrunkDlg::Draw`) |
| Implemented | Memo and mailbox parity | The simulator now covers the client-owned memo stack across `UIWindow.img/Memo` plus `UIWindow2.img/Memo/Send` and `UIWindow2.img/Memo/Get`: the inbox window still owns unread/read markers and read or keep or delete flow, while the new send dialog exposes a simulator-owned draft and send lifecycle, the get dialog owns package inspection and `BtClame` attachment claim flow, and `/memo` commands let parity work drive draft fields, ad hoc deliveries, attachments, and claims without collapsing the system back into generic chat | Player communication and mailbox checks can now be exercised through the same three memo surfaces the client exposes instead of stopping at a list-only inbox shell, which makes memo send, receipt, and package claim behavior testable as a dedicated social subsystem rather than a placeholder note viewer | social UI layer, message or inbox model (`CMemoListDlg::OnCreate`, `CMemoListDlg::Draw`, `CMemoListDlg::OnButtonClicked`) |
| Partial | Messenger parity | The simulator now exposes a dedicated Messenger window backed by `UIWindow2.img/Messenger` frames across the client-shaped `Max`, `Min`, and `Min2` states, repositions the WZ-authored `BtEnter`, `BtClame`, `BtMin`, and `BtMax` controls per state instead of pinning them to the maximized layout, loads and draws the WZ-authored Messenger `icon` canvas in each layout, mirrors the client-owned `DrawStatusBar` seam with participant-summary status text plus a timed blink pulse on remote chat or invite-result or leave or presence events, animates the WZ `chatBalloon` frames over active occupants, routes `BtEnter` through a client-shaped `ProcessChat` seam rather than treating it as an invite hotkey, recognizes Messenger-scoped slash handling for invite or leave flow such as `/m <name>` and `/leave`, records packet-shaped invite or room-chat or claim or remote event summaries, and still backs the shell with a messenger runtime that tracks slot selection, inviteable contacts, live contact presence pulses plus explicit presence overrides, packet-style pending invite acceptance or rejection, occupant presence cards, room-chat versus whisper log entries, click-to-focus text entry, claimed chat-log reporting, remote leave or reject or room-chat or whisper events, and the local leave reset path launched from the status-bar `BtMSN` menu entry; however, it still lacks true server-backed network presence, real socket or packet decoding, remote avatar or member data sourced from actual Messenger packets, and the full `CWvsContext`-owned Messenger session lifecycle beyond the simulator-owned packet and slash approximations around `CUIMessenger::Draw`, `DrawStatusBar`, and `OnButtonClicked` | Player-to-player coordination is no longer blocked on the complete absence of a Messenger shell because the simulator now has a dedicated presence surface with state-authored controls in each Messenger layout, visible whisper versus room-message history, WZ-backed per-occupant chat balloons, a WZ-authored status icon and participant status bar in the same part of the window the client uses, and a closer `BtEnter` plus slash-command flow for invite or leave or room-chat interaction, which exercises more of the Messenger seam before deeper network-owned lifecycle work lands | social UI layer, recipient or presence model (`CUIMessenger::Draw`, `CUIMessenger::OnButtonClicked`) |
| Partial | Friend, party, guild, alliance, and blacklist window parity | The simulator now loads the post-Big Bang `UIWindow2.img/UserList/Main` shell into a dedicated Social List window, mirrors the client-owned tab strip for friend, party, guild, alliance, and blacklist views, pages through simulator-owned rosters, shows per-entry location or channel summaries, wires more of the WZ-authored per-tab buttons into local actions such as add-group, friend chat, group whisper, note edit, guild grade changes, alliance chat, and guild-search launch, and now also opens the dedicated `UIWindow2.img/UserList/Search` and `UIWindow2.img/UserList/GuildSearch` popups so party or party-member or expedition finder tabs plus guild discovery can be exercised with simulator-owned sort, filter, invite, request, bookmark, join, whisper, renew, and expedition registration flows; however, client-authentic scroll bars, the deeper `GuildManage` or `GuildSkill` or alliance-notice edit surfaces, and packet-driven social-state synchronization still remain unmodeled | Once the common social tabs, search popup, and guild-search popup exist and are reachable from `BtCommunity`, `BtSearch`, and the guild tab, the remaining parity gap narrows to the deeper client-owned list-state synchronization and edit dialogs instead of the complete absence of the window family | social UI layer, party or guild data model (`CUIFriendGroup::Draw`, `CUIUserList::Draw`) |
| Partial | Guild BBS parity | The simulator now loads the dedicated `UI/GuildBBS.img` board shell, exposes a simulator-owned guild thread model with guild name/mark header, thread list selection, notice flags, thread/comment paging, keyboard-owned title/body/reply draft entry, WZ-backed basic/cash emoticon selection with cash-page cycling, and button-driven write/edit/delete/reply flows plus `/guildbbs` and keyboard launch paths, but the real client textbox control or IME behavior, server-populated cash-emoticon ownership, and packet-authored guild-board permission gates still remain unmodeled | Guild workflows can now be exercised through a much closer board surface with typed drafts, paged thread/comment review, and WZ-backed emoticon browsing instead of staying at button-only sample flows, which narrows this row to the remaining server-authored permission and textbox lifecycle work behind the dedicated guild board seam | social UI layer, guild board model (`CUIGuildBBS::Draw`) |
| Partial | Family chart parity | The simulator now opens dedicated WZ-backed `Family` and `FamilyTree` windows from `BtFamily`, backs them with a simulator-owned family roster, mirrors the client-confirmed split between the compact family-statistics panel and the larger 11-slot tree layout, exposes family-count and junior-count feedback plus current/today reputation and entitlement-use state, uses client-driven tree semantics for empty branch versus junior-entry slots plus online/offline plate selection and child-node alert coloring, and now lets the user execute the currently selected entitlement from `BtSpecial` while cycling entitlement type directly on the privilege icon so local summon, same-field move, and timed privilege-state flows are no longer inert; however, packet-driven family membership sync, the exact client `_DrawChartItem` node coordinates and string IDs, and deeper cross-map/server-authored privilege effects still remain unmodeled | Family progression and social checks are no longer blocked on the complete absence of family surfaces because the simulator now has dedicated family data, chart layout, client-shaped tree-state presentation, and locally executable privilege actions that can be exercised through the same two-window seam the client draws, while the remaining gap is narrowed to server-authored state plus the last exact draw constants and remote-effect semantics | family runtime, family UI layer (`CUIFamilyChart::Draw`) |
| Partial | MapleTV and broadcast parity | The simulator now loads both the WZ-backed `UIWindow.img/MapleTV` send-board art and `UI/MapleTV.img` broadcast preview assets, exposes a dedicated MapleTV window with the client-shaped `BtOk`, `BtCancel`, and `BtTo` controls, backs it with a simulator-owned MapleTV manager that mirrors the client’s init or set-message or clear-message or send-result lifecycle names, resolves the default media branch from the shipped `TVmedia` set, keeps a queue-visible idle state after timeout instead of dropping straight to empty, and renders sender plus optional receiver avatar previews from the same `CharacterBuild` seam that `CMapleTVMan::OnSetMessage` fills with sender/receiver `AvatarLook` data; however, true packet decoding for remote `AvatarLook` payloads, exact client-owned in-world MapleTV overlay placement and text composition, item-authored media selection beyond the inferred default branch, and the full network-owned broadcast queue lifecycle still remain unmodeled | MapleTV is no longer a completely absent social surface because the simulator can now exercise a dedicated MapleTV message owner, WZ-authored send-board and preview assets, a default-media fallback, queue-visible timeout behavior, and visible sender/receiver avatar presentation instead of leaving broadcast parity outside the runtime entirely, while still leaving room for later client-authentic packet and overlay behavior | social broadcast layer, timed overlay UI, broadcast-state manager (`CUIMapleTV::Draw`, `CMapleTVMan::Init`, `CMapleTVMan::OnClearMessage`, `CMapleTVMan::OnSendMessageResult`, `CMapleTVMan::OnSetMessage`) |

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
