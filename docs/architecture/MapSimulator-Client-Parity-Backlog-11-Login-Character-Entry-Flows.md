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
- `HaCreator/MapSimulator/UI/StatusBarUI.cs`

### Client references checked

- `CLogin::Init` at `0x5d8010`
- `CLogin::Update` at `0x5dee90`
- `CLogin::OnPacket` at `0x5df940`
- `CUICharSelect::OnCreate` at `0x5e8550`
- `CUICharSelect::OnButtonClicked` at `0x5e0350`
- `CUIAvatar::OnCreate` at `0x5ebe10`
- `CUIAvatar::OnMouseButton` at `0x5ed130`
- `CUICharDetail::Draw` at `0x5e8860`
- `CConnectionNoticeDlg::Draw` at `0x5d6a30`
- `CLoginUtilDlg::Draw` at `0x5eee70`
- `CUIRecommendWorld::Draw` at `0x603980`
- `CUIRecommendWorld::OnCreate` at `0x6073a0`
- `CUIRecommendWorld::OnButtonClicked` at `0x607910`

Notes:
IDA confirms there is a coherent pre-field client surface that is not covered by the first ten backlog docs. The login bootstrap, character-selection roster, avatar preview, character-detail panel, recommend-world helper, and connection or utility dialogs all have dedicated owners and do not fit cleanly under in-map HUD work or the broader social-window bucket.

## Client Function Index By Backlog Area

The list below maps this backlog area to concrete client seams confirmed in IDA.
These are the first functions to inspect before changing simulator behavior.

### 1. Login and character-entry parity

- `CLogin::Init` at `0x5d8010`
- `CLogin::Update` at `0x5dee90`
- `CLogin::OnPacket` at `0x5df940`
- `CUICharSelect::OnCreate` at `0x5e8550`
- `CUICharSelect::OnButtonClicked` at `0x5e0350`
- `CUIAvatar::OnCreate` at `0x5ebe10`
- `CUIAvatar::OnMouseButton` at `0x5ed130`
- `CUICharDetail::Draw` at `0x5e8860`
- `CConnectionNoticeDlg::Draw` at `0x5d6a30`
- `CLoginUtilDlg::Draw` at `0x5eee70`
- `CUIRecommendWorld::Draw` at `0x603980`
- `CUIRecommendWorld::OnCreate` at `0x6073a0`
- `CUIRecommendWorld::OnButtonClicked` at `0x607910`

Notes:
`CLogin::Init`, `CLogin::Update`, and `CLogin::OnPacket` confirm a dedicated login-state machine and packet router rather than a generic splash-to-map handoff. `CUICharSelect`, `CUIAvatar`, and `CUICharDetail` confirm that roster browsing, avatar preview, and character metadata are separate entry-flow surfaces with their own create, draw, and click handling. `CConnectionNoticeDlg` and `CLoginUtilDlg` confirm that account-entry prompts such as connection notices and utility dialogs are distinct UI owners, while `CUIRecommendWorld` shows that server recommendation is not just data on the world list but a dedicated helper surface.

## Current State Summary

This area is still mostly absent from the simulator today.
The project currently includes:

- Cash-shop-map and wider game-state gating flags in `MapSimulator.cs` and `GameStateManager.cs`.
- Status-bar utility buttons that already expose some neighboring service entry points once the simulator is in-map.
- Local avatar loading and runtime state that now feed a simulator-side login roster window with selectable character builds and roster-driven field entry on login maps, even though avatar-preview and detail-specific surfaces are still separate gaps.
- A dedicated login bootstrap runtime in `HaCreator/MapSimulator/Managers/LoginRuntimeManager.cs` that now owns pre-field steps, delayed login-step changes, and login-packet dispatch on login maps.

The gap is not a small missing window shell.
The gap is that the simulator does not currently model the client's entire account-entry and character bootstrap pipeline before gameplay begins.

## Remaining Parity Backlog

### 1. Login and character-entry parity

This area is not represented by the existing ten documents even though IDA shows a large, dedicated client surface here.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Login bootstrap and packet-state parity | The simulator now wires the dedicated login bootstrap runtime into a real login-title owner built from `Login.img/Title` (`backFrame`, `MSTitle`, `MapleDolls`, `signboard`, `ID`, `PW`, plus the title buttons), drives CheckPassword or GuestId bootstrap requests from that UI through the existing login-packet inbox instead of chat commands alone, surfaces live inbox status on the title scene, reuses the runtime's delayed step timing to show a visible connection-notice progress pass while Title advances into WorldSelect, and now keeps the packet-routed account follow-up chain alive inside the existing `Login.img/Notice` utility owner by accepting EULA, capturing typed PIC and SPW setup or verification, recording migration decisions, and feeding the accepted path back into the inbox until the flow can resume toward `CheckPasswordResult` instead of stopping at one-shot modal text. Remaining work is narrower: intake is still a simulator loopback packet feed rather than the client's authenticated socket protocol, the follow-up packets are still simulator-authored state transitions instead of full decoded account payloads from a backend session, and dedicated owners beyond the current title plus notice pair (for example fuller migration, website handoff, or other account-authored login classes) are still approximated through the shared utility dialog seam rather than implemented as separate packet-backed client owners | Without the login-state owner, all account-facing transitions are effectively skipped, which hides an entire category of client behavior before the character ever enters a map. The new runtime establishes that owner so later roster, avatar, and dialog work can attach to a real pre-field pipeline instead of jumping straight into a field | account-entry runtime, state machine, packet-routing layer (`CLogin::Init`, `CLogin::Update`, `CLogin::OnPacket`; simulator seam: `HaCreator/MapSimulator/Managers/LoginRuntimeManager.cs`) |
| Partial | Character roster and selection parity | The simulator now exposes a dedicated login-map character-select window, keeps a slot-aware roster model that distinguishes occupied entries from empty and buy-character slots, pages the selection surface across the full visible slot count instead of only populated entries, renders the client `Login.img/CharSelect` empty-slot silhouette and animated `buyCharacter/*` card state on those extra pages, and continues to hand the selected roster build into field entry before map transition. Packet-authored roster data from `SelectWorldResult` and `ViewAllCharResult` is now part of the active path rather than the window always depending on simulator-seeded entries. Remaining work is narrower: roster ownership still falls back to simulator-authored entries when no login packet payload is supplied, and new/delete/account-backed character list authority still runs through simulator or loopback packet seams instead of a true authenticated backend feed | The client's path into gameplay is built around explicit character selection rather than spawning a hardcoded local avatar directly into a field, so even this partial shell materially changes how the simulator enters gameplay on login maps | roster UI layer, character-selection model (`CUICharSelect::OnCreate`, `CUICharSelect::OnButtonClicked`; simulator seams: `HaCreator/MapSimulator/Managers/LoginCharacterRosterManager.cs`, `HaCreator/MapSimulator/UI/Windows/CharacterSelectWindow.cs`, `HaCreator/MapSimulator/UI/Windows/AvatarPreviewCarouselWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Partial | Avatar preview carousel parity | The simulator now drives the login avatar-preview surface through a dedicated `AvatarPreviewCarouselWindow` owner separate from the wider roster shell, replaces the placeholder preview cards and page controls with client `Login.img/CharSelect` canvases (`charInfo`, `charInfo1`, `pageL`, `pageR`, plus the real `BtSelect`/`BtNew`/`BtDelete` button art on the roster shell), keeps the live assembled three-slot browsing, card focus, page-left/page-right cycling, and double-click field-entry handoff aligned with the three-slots-per-page interaction recovered from `CUIAvatar::OnMouseButton`, renders the per-card client nametag strip and job-family decoration path (`nameTag`, `adventure`, `knight`, `aran`, `evan`, `resistance`) using the client job-group selection logic recovered from `CUIAvatar::ResetCharacter`, and now replaces the wider simulator-owned owner shell with the client `Login.img/CharSelect` shell layers that are present in this data set (`scroll/0`, `effect/0`, `effect/1`, `character/0`, and the `BtSelect/keyFocused` flourish) so the roster owner animates a parchment status shell and client selection accent behind the carousel instead of only a plain frame plus footer text. Remaining work is narrower: the owner still approximates `CUIAvatar::OnCreate` text through simulator `SpriteFont` draws and inferred offsets rather than the client's exact font or canvas composition path, and any still-unrecovered micro-placement or owner-children around that shell remain to be mirrored end-to-end | Character-entry parity needs more than a text roster; the client lets the player browse and inspect live avatar previews before entering the game, and the simulator now exposes that browse and inspect loop through a dedicated preview owner with client-backed card, paging, nametag, job-decoration, and outer-shell art instead of only a text list | avatar-preview UI layer, character-preview assembly path (`CUIAvatar::OnCreate`, `CUIAvatar::OnMouseButton`, `CUIAvatar::ResetCharacter`; simulator seams: `HaCreator/MapSimulator/UI/Windows/AvatarPreviewCarouselWindow.cs`, `HaCreator/MapSimulator/UI/Windows/CharacterSelectWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`) |
| Partial | Character detail panel parity | The simulator now constrains the login-map character-detail owner to the real client `Login.img/CharSelect/charInfo` and `charInfo1` canvases instead of extending them with a simulator-authored lower metadata slab, resolves and draws the real `icon/{up,down,same,job/*}` rank assets, keeps the world and job rank lanes right-aligned off the recovered `CUICharDetail::Draw` composition, and now composes the detail rows live in the window draw pass instead of baking simulator GDI text into a replacement panel texture. Client inspection of `CUICharDetail::Draw` confirms the owner copies the background first, then renders the stat rows with `FONT_BASIC_BLACK`, so the simulator now follows that same background-plus-live-overlay structure and uses client-backed black text positions rather than a pre-rasterized Tahoma panel. WZ inspection for `Login.img/CharSelect` still confirms `charInfo1` is only the taller second rank panel, not a hidden native owner for extra name, guild, EXP, or map rows. Remaining work is narrower: the simulator still approximates the exact native `FONT_BASIC_BLACK` glyph metrics with its existing shared SpriteFont instead of reproducing the client font asset byte-for-byte | The client distinguishes high-level roster browsing from the focused detail view, so even a partial dedicated details owner materially improves the pre-entry feedback loop and gives later art-parity work a real surface to land on | character-detail UI layer, character metadata presentation (`CUICharDetail::Draw`; simulator seams: `HaCreator/MapSimulator/UI/Windows/CharacterDetailWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Implemented | Recommend world helper parity | The simulator now builds the login recommend-world helper from the client `Login.img/WorldSelect/alert` frame and button art, draws the selected world's WZ-backed `Login.img/WorldSelect/world/<id>` badge instead of a placeholder label, wraps per-world helper text in the same 125px message lane the client uses, places the owner off the recovered client `CUIRecommendWorld` login-layout rect (`CreateDlg(302, 152, 200, 228)` against the centered 800x600 login scene), and accepts live loopback `RecommendWorldMessage` inbox traffic by routing socket-fed payload text through the existing packet-configuration seam for packet-authored recommend-world ordering plus per-world message strings | Server recommendation is part of the client's account-entry UX, not just an incidental line of text on the world list. Wiring a dedicated helper into the login world step means recommendation and guided selection now exist as a distinct pre-field surface instead of being implied only through world-list status text | world-recommendation UI layer, world-selection assist flow (`CUIRecommendWorld::OnCreate`, `CUIRecommendWorld::Draw`, `CUIRecommendWorld::OnButtonClicked`; simulator seams: `HaCreator/MapSimulator/UI/Windows/RecommendWorldWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Partial | Connection-notice and login-utility dialog parity | The simulator now exposes dedicated login-map `ConnectionNotice` and `LoginUtilityDialog` modal windows wired to world or channel request progress, blocked character-entry validation, delete confirmation, client `Login.img/Notice` background variants (`backgrnd/0`, `backgrnd/1`, `backgrnd/2`, `Loading`, `LoadingSG`), client canned notice canvases (`Notice/text/*`) rendered through both owners, packet-authored account prompt overrides on the existing `/loginpacket` and loopback inbox seam for `AccountInfoResult`, `SetAccountResult`, `ConfirmEulaResult`, `CheckPinCodeResult`, `UpdatePinCodeResult`, `EnableSpwResult`, `CheckSpwResult`, `CreateNewCharacterResult`, and `DeleteCharacterResult`, plus the client-backed yes/no security prompts for `SelectWorldResult` server codes `14` and `15` via `Notice/text/27` and `Notice/text/26`. Remaining work is narrower: the prompts are still simulator-configured overrides rather than full packet-decoded account payloads, connection notices still approximate the client's richer modal family with the first two owners instead of adding the other pre-field dialog classes, and flows such as true PIC/SPW entry, account migration, and website handoff remain modal placeholders rather than implemented account owners | Many account-entry flows are mediated through modal notices and utility prompts, so adding explicit modal owners closes an important parity gap and lets login-shell actions surface through dedicated dialogs instead of footer text alone, even though more account-specific prompts still remain | account-entry dialog layer, modal utility flow (`CConnectionNoticeDlg::Draw`, `CLoginUtilDlg::Draw`; simulator seams: `HaCreator/MapSimulator/UI/Windows/ConnectionNoticeWindow.cs`, `HaCreator/MapSimulator/UI/Windows/LoginUtilityDialogWindow.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |

## Priority Order

If the goal is visible parity first, the next work in this area should be sequenced like this:

1. Bootstrap pass:
   Add a minimal login-state shell and step model so the simulator can represent pre-field transitions at all.
2. Character-entry pass:
   Add roster, avatar-preview, and character-detail surfaces before attempting deeper account prompts.
3. Utility-dialog pass:
   Add connection notices, recommend-world flows, and login utility dialogs once the main entry shell exists.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
