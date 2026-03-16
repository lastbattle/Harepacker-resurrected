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
| Partial | Active pet lifecycle and multi-pet rendering | The simulator now exposes an ordered active-pet roster with client-shaped insert or remove reindexing, slot-aware ladder or rope back-hang anchors rooted on the avatar body origin, and a dedicated under-face pet render plane so hanging pets are inserted into the avatar stack instead of being dumped into a coarse behind-owner pass, but it still lacks the client-owned generic multi-pet hang action table that `CPet::Update` switches to for the primary pet when additional pets are active | Companion-heavy gameplay and visual parity now cover the visible activation, climb anchoring, and hang layering cases that stand out most in-field, which makes multi-pet fields feel substantially closer to the client even before the remaining client-only hang action frames are mirrored exactly | companion runtime, avatar or field-entity layer (`CPet::Update`, `CUser::SetActivePet`) |
| Implemented | Pet, dragon, and android equip-surface parity | `EquipUIBigBang` now backs the post-Big Bang `BtPet`, `BtDragon`, and `BtAndroid` tabs with live pet roster data plus dragon and android equipment presentation and tooltips, while the legacy `EquipUI` path now loads the client's `UIWindow/Equip` pet and dragon companion panes, wires `BtPetEquipShow` or `BtPetEquipHide`, `BtPet1` to `BtPet3`, and `BtDragonEquip`, and feeds those surfaces from the same simulator pet and dragon controllers instead of leaving them as dead art; the android default-equipment controller also now respects the client-facing `Top + Pants` versus `Overall` contract exposed by `UIWindow2.img/Equip/character/BtAndroid/ToolTip` instead of synthesizing an `overall + pants` fallback mix | Companion equipment parity is now visible in both UI generations, which means active pet order, dragon defaults, and android defaults can be exercised through the same equip-surface flows the client exposes instead of being confined to backend-only state | `EquipUI.cs`, `EquipUIBigBang.cs`, `UIWindowLoader.cs`, `MapSimulator.cs`, `CompanionEquipmentController.cs` |
| Implemented | Pet speech, commands, and idle feedback | The simulator now loads WZ-backed pet command tables, level-banded command gates, reaction speech, slang feedback, food-feedback dialog, and auto-speech event lines from `Item/Pet/*.img` plus `String/PetDialog.img`, routes submitted chat text through the active pet roster, lets `PetRuntime` own command-triggered reactions, timed auto-speaking balloons, and client-shaped idle action loops, automatically triggers level-up, pre-level-up, and low-HP speech, and exposes simulator-side `/petlevel`, `/petslang`, `/petfeed`, and `/petevent` hooks so the remaining `rest`, `e_NOHPPotion`, and `e_NOMPPotion` WZ surfaces can be exercised without a separate pet-care subsystem | Companion parity is no longer limited to follow visuals because pets now answer chat-triggered commands using the correct WZ level bands, emit in-world speech or idle feedback through the runtime, and expose the full loaded auto-speech event set to simulator-side verification, which makes the field feel much closer to the client and gives parity work a concrete seam for remaining pet-care systems instead of leaving pet feedback as placeholder behavior | `HaCreator/MapSimulator/Companions/`, `MapSimulator.cs`, `MapSimulatorChat.cs` (`CPet::Update`, `CUser::PetAutoSpeaking`) |
| Partial | Portable chair and install-item parity | The simulator now loads WZ-backed setup-chair layers from `Item/Install/0301*.img`, attaches their `effect` or `effect2` animations as avatar-bound sit overlays, exposes an owned-item `/chair <itemId|clear>` runtime path, and mirrors the client-confirmed riding-chair handoff by equipping `info/tamingMob` mounts while the chair is active and restoring the previous taming-mob state on stand-up; however, the client-owned pair-chair flow and per-chair sit-action variants still remain | Chair usage now exercises the client-shaped additional-layer seam plus the riding-chair mount handoff instead of collapsing entirely into a plain sit pose, which makes setup-item testing and visible avatar composition checks possible even before the remaining couple-chair and sit-variant cases land | avatar additional-layer path, install-item runtime (`CUser::SetActivePortableChair`) |
| Partial | Mini room, shop room, and trader-room parity | The simulator now registers client-backed social-room shells for MiniRoom, PersonalShop, EntrustedShop, and TradingRoom using `UIWindow(.2).img/Minigame`, `PersonalShop`, `MemberShop`, and `TradingRoom` art, routes existing character-info trade and item buttons plus dedicated simulator hotkeys into those windows, and backs each shell with occupant lists, simulator-owned sale or escrow ledgers, and room-state transitions for ready, start, arrange, claim, offer, and lock actions; however, the underlying inventory escrow, wager settlement, visitor persistence, and packet-driven room gameplay still remain incomplete compared with the client | Room systems are no longer absent from the simulator surface, and this pass makes bundle or escrow state visible inside the dedicated room dialogs instead of limiting changes to status text, which gives parity work a better seam for later inventory or networking follow-up | room-system runtime, room UI layer (`CMiniRoomBaseDlg::Draw`, `CPersonalShopDlg::Draw`, `CEntrustedShopDlg::Draw`) |
| Partial | Trunk or storage parity | The simulator now loads the WZ-backed trunk window, exposes per-tab storage state with row selection, deposit/withdraw item flows, sort handling, dual storage-versus-inventory meso presentation anchored to the client draw positions, storage-keeper NPC launch entries derived from `String/Npc.img` `func = "Storage Keeper"` style metadata, and client-shaped typed meso deposit/withdraw prompts on `BtOutCoin` and `BtInCoin`; however, server-authored storage capacity expansion rules and wider account-storage ownership rules are still unmodeled | Storage handling is now testable through its own runtime, NPC talk surface, and typed meso flow instead of being inferred from the regular inventory window alone, even though the surrounding account-state rules still need parity work | `UIWindowLoader.cs`, `TrunkUI.cs`, `InventoryUI.cs`, `QuestRuntimeManager.cs`, `MapSimulator.cs` (`CTrunkDlg::Draw`) |
| Implemented | Memo and mailbox parity | The simulator now covers the client-owned memo stack across `UIWindow.img/Memo` plus `UIWindow2.img/Memo/Send` and `UIWindow2.img/Memo/Get`: the inbox window still owns unread/read markers and read or keep or delete flow, while the new send dialog exposes a simulator-owned draft and send lifecycle, the get dialog owns package inspection and `BtClame` attachment claim flow, and `/memo` commands let parity work drive draft fields, ad hoc deliveries, attachments, and claims without collapsing the system back into generic chat | Player communication and mailbox checks can now be exercised through the same three memo surfaces the client exposes instead of stopping at a list-only inbox shell, which makes memo send, receipt, and package claim behavior testable as a dedicated social subsystem rather than a placeholder note viewer | social UI layer, message or inbox model (`CMemoListDlg::OnCreate`, `CMemoListDlg::Draw`, `CMemoListDlg::OnButtonClicked`) |
| Partial | Messenger parity | The simulator now exposes a dedicated Messenger window backed by `UIWindow2.img/Messenger` frames, min or max states, and WZ-authored `BtEnter` or `BtClame` or `BtMin` or `BtMax` controls, plus a simulator-owned messenger runtime that tracks slot selection, inviteable contacts, occupant presence cards, room-chat versus whisper log entries, click-to-focus text entry, and a lightweight leave reset path launched from the status-bar `BtMSN` menu entry; however, it still lacks live network presence, packet-authored invite acceptance, and the fuller remote leave or reject or whisper lifecycle behind `CUIMessenger::OnButtonClicked` | Player-to-player coordination is no longer blocked on the complete absence of a Messenger shell because the simulator now has a dedicated presence surface with owned text entry, visible whisper versus room-message history, and a leave flow that exercises more of the Messenger interaction seam before deeper client-backed lifecycle work lands | social UI layer, recipient or presence model (`CUIMessenger::Draw`, `CUIMessenger::OnButtonClicked`) |
| Partial | Friend, party, guild, alliance, and blacklist window parity | The simulator now loads the post-Big Bang `UIWindow2.img/UserList/Main` shell into a dedicated Social List window, mirrors the client-owned tab strip for friend, party, guild, alliance, and blacklist views, pages through simulator-owned rosters, shows per-entry location or channel summaries, and wires the WZ-authored per-tab buttons into local actions such as add, invite, kick, whisper, memo, block, and delete; however, expedition/search surfaces, client-authentic scroll bars, and packet-driven social-state synchronization still remain unmodeled | Once the common social tabs exist and are reachable from `BtCommunity`, the remaining parity gap narrows to the deeper client-owned list states instead of the complete absence of the window family | social UI layer, party or guild data model (`CUIFriendGroup::Draw`, `CUIUserList::Draw`) |
| Partial | Guild BBS parity | The simulator now loads the dedicated `UI/GuildBBS.img` board shell, exposes a simulator-owned guild thread model with guild name/mark header, thread list selection, notice flags, comment rows, and button-driven write/edit/delete/reply flows plus `/guildbbs` and keyboard launch paths, but client-authentic text entry, emoticon paging, pagination, and packet-backed permissions still remain unmodeled | Guild workflows can now be exercised through their own board surface instead of staying completely absent from social-system parity, which closes one of the most visible guild-specific UI gaps before the deeper server-backed lifecycle lands | social UI layer, guild board model (`CUIGuildBBS::Draw`) |
| Missing | Family chart parity | `CUIFamilyChart::Draw` confirms a dedicated family-tree and family-statistics window, but the simulator does not currently expose family data, chart layout, or family-count feedback | The family system is a distinct progression and social surface that does not fit under the existing quest or stat-window backlog | family runtime, family UI layer (`CUIFamilyChart::Draw`) |
| Partial | MapleTV and broadcast parity | The simulator now loads the WZ-backed `UIWindow.img/MapleTV` send-board art, exposes a dedicated MapleTV window with the client-shaped `BtOk`, `BtCancel`, and `BtTo` controls, backs it with a simulator-owned MapleTV manager that mirrors the client’s init or set-message or clear-message or send-result lifecycle names, and adds `/mapletv` commands plus a keyboard launcher so timed broadcasts can be authored, published, inspected, and cleared; however, the packet-authentic sender or receiver avatar rendering, default-media resolution, and full network-owned broadcast queue semantics behind the live client manager still remain unmodeled | MapleTV is no longer a completely absent social surface because the simulator can now exercise a dedicated MapleTV message owner, timed display state, and WZ-authored window shell instead of leaving broadcast parity outside the runtime entirely, while still leaving room for later client-authentic avatar and packet behavior | social broadcast layer, timed overlay UI, broadcast-state manager (`CUIMapleTV::Draw`, `CMapleTVMan::Init`, `CMapleTVMan::OnClearMessage`, `CMapleTVMan::OnSendMessageResult`, `CMapleTVMan::OnSetMessage`) |

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
