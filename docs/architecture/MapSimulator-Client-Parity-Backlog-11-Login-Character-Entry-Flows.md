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
| Partial | Login bootstrap and packet-state parity | The simulator now wires the dedicated login bootstrap runtime into a real login-title owner built from `Login.img/Title` (`backFrame`, `MSTitle`, `MapleDolls`, `signboard`, `ID`, `PW`, plus the title buttons), drives CheckPassword or GuestId bootstrap requests from that UI through the existing login-packet inbox instead of chat commands alone, surfaces live inbox status on the title scene, reuses the runtime's delayed step timing to show a visible connection-notice progress pass while Title advances into WorldSelect, and now keeps the packet-routed account follow-up chain alive inside the existing `Login.img/Notice` utility owner by accepting EULA, capturing typed PIC and SPW setup or verification, recording migration decisions, and feeding the accepted path back into the inbox until the flow can resume toward `CheckPasswordResult` instead of stopping at one-shot modal text. The row also now decodes and stores packet-authored account-dialog payloads for `AccountInfoResult`, `SetAccountResult`, `ConfirmEulaResult`, `CheckPinCodeResult`, `UpdatePinCodeResult`, `EnableSpwResult`, `CheckSpwResult`, `CreateNewCharacterResult`, and `DeleteCharacterResult`, so the shared utility owner can display packet-carried text, result codes, account ids, and character ids instead of only simulator-authored boilerplate. WZ inspection of `Login.img/Title/BtHomePage` and `Login.img/Notice/BtNexon` is now also reflected in the active path: the title owner exposes the homepage button, and the security-site `SelectWorldResult` code `14` / `15` prompts can continue into a dedicated website-handoff step inside the existing notice seam instead of stopping at a passive yes/no placeholder. Remaining work is narrower: intake is still a simulator loopback packet feed rather than the client's authenticated socket protocol, the decoded follow-up payloads are still cached command or loopback data rather than live backend session state, and dedicated owners beyond the current title plus notice pair are still approximated through the shared utility or notice seam instead of implemented as separate packet-backed client classes | Without the login-state owner, all account-facing transitions are effectively skipped, which hides an entire category of client behavior before the character ever enters a map. The new runtime establishes that owner so later roster, avatar, and dialog work can attach to a real pre-field pipeline instead of jumping straight into a field | account-entry runtime, state machine, packet-routing layer (`CLogin::Init`, `CLogin::Update`, `CLogin::OnPacket`; simulator seam: `HaCreator/MapSimulator/Managers/LoginRuntimeManager.cs`) |
| Partial | Character roster and selection parity | The simulator now exposes a dedicated login-map character-select window, keeps a slot-aware roster model that distinguishes occupied entries from empty and buy-character slots, pages the selection surface across the full visible slot count instead of only populated entries, renders the client `Login.img/CharSelect` empty-slot silhouette and animated `buyCharacter/*` card state on those extra pages, and continues to hand the selected roster build into field entry before map transition. WZ inspection of `Login.img/CharSelect` still keeps the row anchored to the existing `BtSelect`/`BtNew`/`BtDelete` roster seam, while client inspection of `CUICharSelect::OnButtonClicked`, `CLogin::OnCreateNewCharacterResult`, and `CLogin::OnDeleteCharacterResult` confirms the real client sends create/delete through dedicated packets and then mutates the in-memory avatar roster from those packet results rather than treating the buttons as local-only UI edits. Packet-authored roster data from `SelectWorldResult` and `ViewAllCharResult` is now part of the active path rather than the window always depending on simulator-seeded entries, and that packet-owned roster now caches into a persisted simulator account roster keyed by login account plus world so the no-packet fallback path reopens the last account-backed list instead of reseeding ad hoc simulator entries. The `BtNew` and `BtDelete` actions also now mutate that same persisted account-backed roster through the existing login utility dialog seam, including typed new-character naming, empty-slot reuse, buy-character-slot consumption, persisted deletion, and reload of the live selection surface. Successful packet-authored `CreateNewCharacterResult` and `DeleteCharacterResult` paths now also apply server-owned roster mutations end to end inside that same seam: create upserts the returned character into the live roster, consumes buy-character slots when the packet-owned add overflows the base slot count, rewrites the active `SelectWorldResult` / `ViewAllCharResult` roster snapshots, persists the updated account-backed snapshot, restores character-select, and suppresses the fallback success modal; delete now removes the targeted character id from the live roster, rewrites those packet-owned roster snapshots, persists the deletion, shifts selection to the next valid slot the way the client does, and suppresses the passive success dialog on the success path. Remaining work is narrower: the simulator still does not consume a true authenticated backend character-list feed, packet-authored rosters are still simulator-maintained snapshots rather than state owned by a real backend session, and there is still no reconciliation against a live authenticated server account beyond the loopback and packet-configuration seam | The client's path into gameplay is built around explicit character selection rather than spawning a hardcoded local avatar directly into a field, so even this partial shell materially changes how the simulator enters gameplay on login maps | roster UI layer, character-selection model (`CUICharSelect::OnCreate`, `CUICharSelect::OnButtonClicked`; simulator seams: `HaCreator/MapSimulator/Managers/LoginCharacterRosterManager.cs`, `HaCreator/MapSimulator/Managers/LoginCharacterAccountStore.cs`, `HaCreator/MapSimulator/UI/Windows/CharacterSelectWindow.cs`, `HaCreator/MapSimulator/UI/Windows/AvatarPreviewCarouselWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Partial | Avatar preview carousel parity | The simulator now drives the login avatar-preview surface through a dedicated `AvatarPreviewCarouselWindow` owner separate from the wider roster shell, replaces the placeholder preview cards and page controls with client `Login.img/CharSelect` canvases (`charInfo`, `charInfo1`, `pageL`, `pageR`, plus the real `BtSelect`/`BtNew`/`BtDelete` button art on the roster shell), keeps the live assembled three-slot browsing, card focus, page-left/page-right cycling, and double-click field-entry handoff aligned with the three-slots-per-page interaction recovered from `CUIAvatar::OnMouseButton`, renders the per-card client nametag strip and job-family decoration path (`nameTag`, `adventure`, `knight`, `aran`, `evan`, `resistance`) using the client job-group selection logic recovered from `CUIAvatar::ResetCharacter`, and now replaces the wider simulator-owned owner shell with the client `Login.img/CharSelect` shell layers that are present in this data set (`scroll/0`, `effect/0`, `effect/1`, `character/0`, and the `BtSelect/keyFocused` flourish) so the roster owner animates a parchment status shell and client selection accent behind the carousel instead of only a plain frame plus footer text. Remaining work is narrower: the owner still approximates `CUIAvatar::OnCreate` text through simulator `SpriteFont` draws and inferred offsets rather than the client's exact font or canvas composition path, and any still-unrecovered micro-placement or owner-children around that shell remain to be mirrored end-to-end | Character-entry parity needs more than a text roster; the client lets the player browse and inspect live avatar previews before entering the game, and the simulator now exposes that browse and inspect loop through a dedicated preview owner with client-backed card, paging, nametag, job-decoration, and outer-shell art instead of only a text list | avatar-preview UI layer, character-preview assembly path (`CUIAvatar::OnCreate`, `CUIAvatar::OnMouseButton`, `CUIAvatar::ResetCharacter`; simulator seams: `HaCreator/MapSimulator/UI/Windows/AvatarPreviewCarouselWindow.cs`, `HaCreator/MapSimulator/UI/Windows/CharacterSelectWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`) |
| Partial | Character detail panel parity | The simulator now constrains the login-map character-detail owner to the real client `Login.img/CharSelect/charInfo` and `charInfo1` canvases instead of extending them with a simulator-authored lower metadata slab, resolves and draws the real `icon/{up,down,same,job/*}` rank assets, keeps the world and job rank lanes right-aligned off the recovered `CUICharDetail::Draw` composition, and renders the detail rows live in the window draw pass rather than rasterizing text into a replacement panel bitmap. Client inspection of `CUICharDetail::Draw` confirms the owner copies the background first and then renders the stat rows with `FONT_BASIC_BLACK`, so the simulator now follows that same background-plus-live-overlay structure and no longer depends on the shared MonoGame `SpriteFont` metrics for the normal detail rows: `CharacterDetailWindow` now measures and caches those rows through a dedicated runtime bitmap-text path so right alignment and per-row spacing are driven by the closer typographic raster pass instead of the generic XNA debug font. WZ inspection for `Login.img/CharSelect` still confirms `charInfo1` is only the taller second rank panel, not a hidden native owner for extra name, guild, EXP, or map rows. Remaining work is now only the last font-asset step: the simulator still approximates `FONT_BASIC_BLACK` with a runtime Windows raster font/cache instead of reproducing the client font asset's exact glyph shapes, spacing table, and byte-exact output verbatim | The client distinguishes high-level roster browsing from the focused detail view, so even a partial dedicated details owner materially improves the pre-entry feedback loop and gives later art-parity work a real surface to land on | character-detail UI layer, character metadata presentation (`CUICharDetail::Draw`; simulator seams: `HaCreator/MapSimulator/UI/Windows/CharacterDetailWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Implemented | Recommend world helper parity | The simulator now builds the login recommend-world helper from the client `Login.img/WorldSelect/alert` frame and button art, draws the selected world's WZ-backed `Login.img/WorldSelect/world/<id>` badge instead of a placeholder label, wraps per-world helper text in the same 125px message lane the client uses, places the owner off the recovered client `CUIRecommendWorld` login-layout rect (`CreateDlg(302, 152, 200, 228)` against the centered 800x600 login scene), and accepts live loopback `RecommendWorldMessage` inbox traffic by routing socket-fed payload text through the existing packet-configuration seam for packet-authored recommend-world ordering plus per-world message strings | Server recommendation is part of the client's account-entry UX, not just an incidental line of text on the world list. Wiring a dedicated helper into the login world step means recommendation and guided selection now exist as a distinct pre-field surface instead of being implied only through world-list status text | world-recommendation UI layer, world-selection assist flow (`CUIRecommendWorld::OnCreate`, `CUIRecommendWorld::Draw`, `CUIRecommendWorld::OnButtonClicked`; simulator seams: `HaCreator/MapSimulator/UI/Windows/RecommendWorldWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Partial | Connection-notice and login-utility dialog parity | The simulator now exposes dedicated login-map `ConnectionNotice` and `LoginUtilityDialog` modal windows wired to world or channel request progress, blocked character-entry validation, delete confirmation, client `Login.img/Notice` background variants (`backgrnd/0`, `backgrnd/1`, `backgrnd/2`, `Loading`, `LoadingSG`), the `Login.img/Notice/Loading/circle/*` 16-frame spinner animation on the loading notice owner, client canned notice canvases (`Notice/text/*`) rendered through both owners, packet-authored account prompt overrides on the existing `/loginpacket` and loopback inbox seam for `AccountInfoResult`, `SetAccountResult`, `ConfirmEulaResult`, `CheckPinCodeResult`, `UpdatePinCodeResult`, `EnableSpwResult`, `CheckSpwResult`, `CreateNewCharacterResult`, and `DeleteCharacterResult`, plus the client-backed yes/no security prompts for `SelectWorldResult` server codes `14` and `15` via `Notice/text/27` and `Notice/text/26`. Packet-authored account-dialog payloads now also flow through the active prompt path instead of only simulator-authored boilerplate: the login packet configuration seam correctly routes those packet types through the account-dialog codec, and the shared utility owner surfaces decoded payload detail such as result codes, secondary codes, account ids, character ids, gender, grade, account flags, country, club id, purchase experience, chat-block metadata, register date, character count, and client key. Remaining work is narrower: the decoded prompts are still cached command or loopback payloads rather than live authenticated backend session state, connection notices still approximate the client's richer modal family with the first two owners instead of adding the other pre-field dialog classes, and flows such as true PIC/SPW entry, account migration, and website handoff still remain inside the shared modal seam rather than dedicated packet-backed client owners | Many account-entry flows are mediated through modal notices and utility prompts, so adding explicit modal owners closes an important parity gap and lets login-shell actions surface through dedicated dialogs instead of footer text alone, even though more account-specific prompts still remain | account-entry dialog layer, modal utility flow (`CConnectionNoticeDlg::Draw`, `CLoginUtilDlg::Draw`; simulator seams: `HaCreator/MapSimulator/UI/Windows/ConnectionNoticeWindow.cs`, `HaCreator/MapSimulator/UI/Windows/LoginUtilityDialogWindow.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |

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
