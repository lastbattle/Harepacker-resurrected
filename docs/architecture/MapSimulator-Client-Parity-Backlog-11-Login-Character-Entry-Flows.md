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
- `CLogin::OnCheckDuplicatedIDResult` at `0x5d5790`
- `CLogin::OnCreateNewCharacterResult` at `0x5dab90`
- `CUINewCharNameSelect::OnCreate` at `0x5f3330`
- `CUINewCharNameSelect::OnButtonClicked` at `0x5f4fe0`
- `CUINewCharNameSelect::OnKey` at `0x5f5090`
- `CUINewCharAvatarSelect::OnCreate` at `0x5f3340`
- `CUINewCharAvatarSelect::OnButtonClicked` at `0x5f36b0`
- `CUINewCharAvatarSelect::Draw` at `0x5f51a0`
- `CUINewCharJobSelect::OnButtonClicked` at `0x5f35d0`
- `CUINewCharJobSelect::Draw` at `0x5f38e0`
- `CUINewCharJobSelect::OnCreate` at `0x5fa760`
- `CUINewCharRaceSelect::Draw` at `0x5f37d0`
- `CUINewCharRaceSelect::OnCreate` at `0x5f81c0`
- `CUINewCharRaceSelect::OnButtonClicked` at `0x5f9470`
- `CUIRecommendWorld::Draw` at `0x603980`
- `CUIRecommendWorld::OnCreate` at `0x6073a0`
- `CUIRecommendWorld::OnButtonClicked` at `0x607910`

Notes:
IDA confirms there is a coherent pre-field client surface that is not covered by the first ten backlog docs. The login bootstrap, character-selection roster, avatar preview, character-detail panel, recommend-world helper, connection or utility dialogs, and the separate create-character race/job/avatar/name owners all have dedicated client classes and do not fit cleanly under in-map HUD work or the broader social-window bucket.

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

### 2. Additional client-owned create-character wrappers discovered by targeted IDA scan

- `CLogin::OnCheckDuplicatedIDResult` at `0x5d5790`
- `CLogin::OnCreateNewCharacterResult` at `0x5dab90`
- `CUINewCharNameSelect::OnCreate` at `0x5f3330`
- `CUINewCharNameSelect::OnButtonClicked` at `0x5f4fe0`
- `CUINewCharNameSelect::OnKey` at `0x5f5090`
- `CUINewCharAvatarSelect::OnCreate` at `0x5f3340`
- `CUINewCharAvatarSelect::OnButtonClicked` at `0x5f36b0`
- `CUINewCharAvatarSelect::Draw` at `0x5f51a0`
- `CUINewCharJobSelect::OnButtonClicked` at `0x5f35d0`
- `CUINewCharJobSelect::Draw` at `0x5f38e0`
- `CUINewCharJobSelect::OnCreate` at `0x5fa760`
- `CUINewCharRaceSelect::Draw` at `0x5f37d0`
- `CUINewCharRaceSelect::OnCreate` at `0x5f81c0`
- `CUINewCharRaceSelect::OnButtonClicked` at `0x5f9470`

Notes:
Targeted IDA lookup shows the client does not treat new-character creation as a small extension of `CUICharSelect`. It has a separate owner family for race selection, job selection, avatar selection, and typed name validation, with `CLogin` packet handlers feeding duplicate-name and create-result state back into that flow.

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
| Partial | Login bootstrap and packet-state parity | The simulator now wires the dedicated login bootstrap runtime into a real login-title owner built from `Login.img/Title` (`backFrame`, `MSTitle`, `MapleDolls`, `signboard`, `ID`, `PW`, plus the title buttons), drives CheckPassword or GuestId bootstrap requests from that UI through the existing login-packet inbox instead of chat commands alone, surfaces live inbox status on the title scene, reuses the runtime's delayed step timing to show a visible connection-notice progress pass while Title advances into WorldSelect, and now keeps the packet-routed account follow-up chain alive inside the existing `Login.img/Notice` utility owner by accepting EULA, capturing typed PIC and SPW setup or verification, recording migration decisions, and feeding the accepted path back into the inbox until the flow can resume toward `CheckPasswordResult` instead of stopping at one-shot modal text. The row also now decodes and stores packet-authored account-dialog payloads for `AccountInfoResult`, `SetAccountResult`, `ConfirmEulaResult`, `CheckPinCodeResult`, `UpdatePinCodeResult`, `EnableSpwResult`, `CheckSpwResult`, `CreateNewCharacterResult`, and `DeleteCharacterResult`, so the shared utility owner can display packet-carried text, result codes, account ids, and character ids instead of only simulator-authored boilerplate. Client inspection of `CLogin::OnSetAccountResult`, `CLogin::OnConfirmEULAResult`, and `CLogin::OnCheckPinCodeResult` is now reflected in the active packet path as well: packet-authored success payloads for `SetAccountResult`, `ConfirmEulaResult`, and `CheckPinCodeResult` no longer reopen the generic migration, EULA, or PIC modal, but suppress that success dialog, enqueue the next bootstrap packet through the inbox, and keep the login chain advancing the way the client does; packet-authored `CheckPinCodeResult` prompt selection is now result-code aware so code `1` opens PIC creation, codes `2` and `4` open PIC verification, and code `7` forces the runtime back to Title while still surfacing the client-backed failure notice. WZ inspection of `Login.img/Title/BtHomePage` and `Login.img/Notice/BtNexon` is now also reflected in the active path: the title owner exposes the homepage button, and the security-site `SelectWorldResult` code `14` / `15` prompts can continue into a dedicated website-handoff step inside the existing notice seam instead of stopping at a passive yes/no placeholder. Remaining work is narrower: intake is still a simulator loopback packet feed rather than the client's authenticated socket protocol, the decoded follow-up payloads are still cached command or loopback data rather than live backend session state, and dedicated owners beyond the current title plus notice pair are still approximated through the shared utility or notice seam instead of implemented as separate packet-backed client classes | Without the login-state owner, all account-facing transitions are effectively skipped, which hides an entire category of client behavior before the character ever enters a map. The new runtime establishes that owner so later roster, avatar, and dialog work can attach to a real pre-field pipeline instead of jumping straight into a field | account-entry runtime, state machine, packet-routing layer (`CLogin::Init`, `CLogin::Update`, `CLogin::OnPacket`, `CLogin::OnSetAccountResult`, `CLogin::OnConfirmEULAResult`, `CLogin::OnCheckPinCodeResult`; simulator seam: `HaCreator/MapSimulator/Managers/LoginRuntimeManager.cs`) |
| Partial | Character roster and selection parity | The simulator now exposes a dedicated login-map character-select window, keeps a slot-aware roster model that distinguishes occupied entries from empty and buy-character slots, pages the selection surface across the full visible slot count instead of only populated entries, renders the client `Login.img/CharSelect` empty-slot silhouette and animated `buyCharacter/*` card state on those extra pages, and continues to hand the selected roster build into field entry before map transition. WZ inspection of `Login.img/CharSelect` still keeps the row anchored to the existing `BtSelect`/`BtNew`/`BtDelete` roster seam, while client inspection of `CUICharSelect::OnButtonClicked`, `CLogin::OnCreateNewCharacterResult`, and `CLogin::OnDeleteCharacterResult` confirms the real client sends create/delete through dedicated packets and then mutates the in-memory avatar roster from those packet results rather than treating the buttons as local-only UI edits. Packet-authored roster data from `SelectWorldResult` and `ViewAllCharResult` is now part of the active path rather than the window always depending on simulator-seeded entries, and that packet-owned roster now caches into a persisted simulator account roster keyed by login account plus world so the no-packet fallback path reopens the last account-backed list instead of reseeding ad hoc simulator entries. The `BtNew` and `BtDelete` actions now both stay inside that same packet-authored mutation seam through the existing login utility dialog owner: typed new-character naming still generates a starter `CreateNewCharacterResult`, empty-slot reuse and buy-character-slot consumption still resolve there, and delete confirmation now also generates a success `DeleteCharacterResult` profile instead of mutating the roster as a local-only side effect, so both actions rewrite the live `SelectWorldResult` / `ViewAllCharResult` snapshots, persist the account-backed roster snapshot, restore character-select, and keep success handling inside the packet flow without a fallback modal. Remaining work is narrower and now mostly backend-owned: the simulator still does not consume a true authenticated backend character-list feed, packet-authored rosters are still simulator-maintained snapshots rather than roster state owned by a real backend session, and there is still no reconciliation against a live authenticated server account beyond the loopback and packet-configuration seam | The client's path into gameplay is built around explicit character selection rather than spawning a hardcoded local avatar directly into a field, so even this partial shell materially changes how the simulator enters gameplay on login maps | roster UI layer, character-selection model (`CUICharSelect::OnCreate`, `CUICharSelect::OnButtonClicked`; simulator seams: `HaCreator/MapSimulator/Managers/LoginCharacterRosterManager.cs`, `HaCreator/MapSimulator/Managers/LoginCharacterAccountStore.cs`, `HaCreator/MapSimulator/UI/Windows/CharacterSelectWindow.cs`, `HaCreator/MapSimulator/UI/Windows/AvatarPreviewCarouselWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Partial | Avatar preview carousel parity | The simulator now drives the login avatar-preview surface through a dedicated `AvatarPreviewCarouselWindow` owner separate from the wider roster shell, replaces the placeholder preview cards and page controls with client `Login.img/CharSelect` canvases (`charInfo`, `charInfo1`, `pageL`, `pageR`, plus the real `BtSelect`/`BtNew`/`BtDelete` button art on the roster shell), keeps the live assembled three-slot browsing, card focus, page-left/page-right cycling, and double-click field-entry handoff aligned with the three-slots-per-page interaction recovered from `CUIAvatar::OnMouseButton`, renders the per-card client nametag strip and job-family decoration path (`nameTag`, `adventure`, `knight`, `aran`, `evan`, `resistance`) using the client job-group selection logic recovered from `CUIAvatar::ResetCharacter`, and now replaces the wider simulator-owned owner shell with the client `Login.img/CharSelect` shell layers that are present in this data set (`scroll/0`, `effect/0`, `effect/1`, `character/0`, and the `BtSelect/keyFocused` flourish) so the roster owner animates a parchment status shell and client selection accent behind the carousel instead of only a plain frame plus footer text. Remaining work is narrower: the owner still approximates `CUIAvatar::OnCreate` text through simulator `SpriteFont` draws and inferred offsets rather than the client's exact font or canvas composition path, and any still-unrecovered micro-placement or owner-children around that shell remain to be mirrored end-to-end | Character-entry parity needs more than a text roster; the client lets the player browse and inspect live avatar previews before entering the game, and the simulator now exposes that browse and inspect loop through a dedicated preview owner with client-backed card, paging, nametag, job-decoration, and outer-shell art instead of only a text list | avatar-preview UI layer, character-preview assembly path (`CUIAvatar::OnCreate`, `CUIAvatar::OnMouseButton`, `CUIAvatar::ResetCharacter`; simulator seams: `HaCreator/MapSimulator/UI/Windows/AvatarPreviewCarouselWindow.cs`, `HaCreator/MapSimulator/UI/Windows/CharacterSelectWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`) |
| Partial | Character detail panel parity | The simulator now constrains the login-map character-detail owner to the real client `Login.img/CharSelect/charInfo` and `charInfo1` canvases instead of extending them with a simulator-authored lower metadata slab, resolves and draws the real `icon/{up,down,same,job/*}` rank assets, keeps the world and job rank lanes right-aligned off the recovered `CUICharDetail::Draw` composition, and renders the detail rows live in the window draw pass rather than rasterizing text into a replacement panel bitmap. Client inspection of `CUICharDetail::Draw` confirms the owner copies the background first and then renders the stat rows with `FONT_BASIC_BLACK`, so the simulator now follows that same background-plus-live-overlay structure and no longer depends on the shared MonoGame `SpriteFont` metrics for the normal detail rows: `CharacterDetailWindow` measures and caches those rows through a dedicated runtime bitmap-text path, no longer hard-gates detail-row drawing on a shared `SpriteFont`, now sizes the fallback raster font at the client-backed `12`-pixel `FONT_BASIC_BLACK` height recovered from `get_basic_font`, and trims/caches the rendered glyph bitmap before alignment so right-aligned rank numbers key off the actual rendered ink bounds instead of the looser pre-render `MeasureString` box. WZ inspection for `Login.img/CharSelect` still confirms `charInfo1` is only the taller second rank panel, not a hidden native owner for extra name, guild, EXP, or map rows. Remaining work is now the unrecovered font-asset identity step: the simulator still approximates `FONT_BASIC_BLACK` through a runtime Windows font/cache because the client font face/resource behind `get_basic_font(..., FONT_BASIC_BLACK)` has not yet been reproduced byte-for-byte, so exact glyph shapes, kerning/advance behavior, and final raster output can still differ from the live client | The client distinguishes high-level roster browsing from the focused detail view, so even a partial dedicated details owner materially improves the pre-entry feedback loop and gives later art-parity work a real surface to land on | character-detail UI layer, character metadata presentation (`CUICharDetail::Draw`, `get_basic_font`; simulator seams: `HaCreator/MapSimulator/UI/Windows/CharacterDetailWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Missing | Dedicated create-character flow parity | IDA shows that race selection, job selection, avatar selection, typed-name entry, duplicate-name validation, and create-result handling are owned by a distinct pre-field UI family rather than by the existing roster shell or shared login utility dialog: `CUINewCharRaceSelect::{OnCreate,Draw,OnButtonClicked}`, `CUINewCharJobSelect::{OnCreate,Draw,OnButtonClicked}`, `CUINewCharAvatarSelect::{OnCreate,Draw,OnButtonClicked}`, and `CUINewCharNameSelect::{OnCreate,OnButtonClicked,OnKey}` all exist, while `CLogin::OnCheckDuplicatedIDResult` and `CLogin::OnCreateNewCharacterResult` feed packet state back into that same path. The simulator currently shortcuts this into typed modal prompts and synthetic `CreateNewCharacterResult` snapshots hanging off the roster mutation seam, so it still misses the client-owned multi-step create-character state machine, its dedicated art/layout owners, and the separate duplicate-name loop | Character creation is part of the visible pre-field pipeline, not just a roster mutation side effect. Until these owners exist, login parity will keep skipping a client-owned flow that players use before the avatar ever appears in the field | create-character owner family, login packet follow-up path (`CUINewCharRaceSelect`, `CUINewCharJobSelect`, `CUINewCharAvatarSelect`, `CUINewCharNameSelect`, `CLogin::OnCheckDuplicatedIDResult`, `CLogin::OnCreateNewCharacterResult`) |
| Implemented | Recommend world helper parity | The simulator now builds the login recommend-world helper from the client `Login.img/WorldSelect/alert` frame and button art, draws the selected world's WZ-backed `Login.img/WorldSelect/world/<id>` badge instead of a placeholder label, wraps per-world helper text in the same 125px message lane the client uses, places the owner off the recovered client `CUIRecommendWorld` login-layout rect (`CreateDlg(302, 152, 200, 228)` against the centered 800x600 login scene), and accepts live loopback `RecommendWorldMessage` inbox traffic by routing socket-fed payload text through the existing packet-configuration seam for packet-authored recommend-world ordering plus per-world message strings | Server recommendation is part of the client's account-entry UX, not just an incidental line of text on the world list. Wiring a dedicated helper into the login world step means recommendation and guided selection now exist as a distinct pre-field surface instead of being implied only through world-list status text | world-recommendation UI layer, world-selection assist flow (`CUIRecommendWorld::OnCreate`, `CUIRecommendWorld::Draw`, `CUIRecommendWorld::OnButtonClicked`; simulator seams: `HaCreator/MapSimulator/UI/Windows/RecommendWorldWindow.cs`, `HaCreator/MapSimulator/Loaders/UIWindowLoader.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Partial | Connection-notice and login-utility dialog parity | The simulator now exposes dedicated login-map `ConnectionNotice` and `LoginUtilityDialog` modal windows wired to world or channel request progress, blocked character-entry validation, delete confirmation, client `Login.img/Notice` background variants (`backgrnd/0`, `backgrnd/1`, `backgrnd/2`, `Loading`, `LoadingSG`), the `Login.img/Notice/Loading/circle/*` 16-frame spinner animation on the loading notice owner, client canned notice canvases (`Notice/text/*`) rendered through both owners, packet-authored account prompt overrides on the existing `/loginpacket` and loopback inbox seam for `AccountInfoResult`, `SetAccountResult`, `ConfirmEulaResult`, `CheckPinCodeResult`, `UpdatePinCodeResult`, `EnableSpwResult`, `CheckSpwResult`, `CreateNewCharacterResult`, and `DeleteCharacterResult`, plus the client-backed yes/no security prompts for `SelectWorldResult` server codes `14` and `15` via `Notice/text/27` and `Notice/text/26`. WZ inspection of `Login.img/Notice/Loading/BtCancel` and `LoadingSG/BtCancel`, together with IDA for `CConnectionNoticeDlg::OnCreate`, is now also reflected in the active path: the loading connection notice exposes a real cancel button at the client-backed slot instead of behaving like a passive splash only, and that cancel path can now abort pending selector waits or the title-to-world delayed bootstrap transition inside the existing runtime seam. Client inspection of `CLogin::OnSelectWorldResult`, `CLogin::OnUpdatePinCodeResult`, `CLogin::OnEnableSPWResult`, and `CLogin::OnCheckSPWResult` is now also reflected in the active path: packet-authored `SelectWorldResult` failures no longer stop at plain prose for only the website-handoff cases, but map through the same broader `CLoginUtilDlg::Error(...)` family (`Notice/text/3`, `14`, `15`, `16`, `17`, `19`, `20`, `21`, `27`, `33`, `40`) the client uses, while packet-authored PIC and SPW result payloads can collapse into passive canned notices instead of reopening the shared input flow when the client would already be on a result-only notice (`UpdatePinCodeResult` success/failure via `Notice/text/8` or `15`, `EnableSPWResult` result families via `Notice/text/18`, `39`, `40`, `91`, `92`, `93`, and `CheckSPWResult` via `Notice/text/93`). Packet-authored account-dialog payloads now also flow through the active prompt path instead of only simulator-authored boilerplate: the login packet configuration seam correctly routes those packet types through the account-dialog codec, and both modal owners now keep supplemental body text visible underneath canned notice canvases so decoded payload detail such as result codes, secondary codes, account ids, character ids, gender, grade, account flags, country, club id, purchase experience, chat-block metadata, register date, character count, and client key survive even when the client-backed notice art is shown. Remaining work is narrower: the decoded prompts are still cached command or loopback payloads rather than live authenticated backend session state, connection notices still approximate the client's richer modal family with the first two owners instead of adding the other pre-field dialog classes, and true dedicated packet-backed owners for PIC dialogs, SPW setup and verification, account migration, and website handoff are still not present because those flows continue to run through the shared simulator modal seam | Many account-entry flows are mediated through modal notices and utility prompts, so adding explicit modal owners closes an important parity gap and lets login-shell actions surface through dedicated dialogs instead of footer text alone, even though more account-specific prompts still remain | account-entry dialog layer, modal utility flow (`CConnectionNoticeDlg::Draw`, `CConnectionNoticeDlg::OnCreate`, `CLoginUtilDlg::Draw`, `CLogin::OnSelectWorldResult`, `CLogin::OnUpdatePinCodeResult`, `CLogin::OnEnableSPWResult`, `CLogin::OnCheckSPWResult`; simulator seams: `HaCreator/MapSimulator/UI/Windows/ConnectionNoticeWindow.cs`, `HaCreator/MapSimulator/UI/Windows/LoginUtilityDialogWindow.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |

## Priority Order

If the goal is visible parity first, the next work in this area should be sequenced like this:

1. Bootstrap pass:
   Add a minimal login-state shell and step model so the simulator can represent pre-field transitions at all.
2. Character-entry pass:
   Add roster, avatar-preview, and character-detail surfaces before attempting deeper account prompts.
3. Create-character pass:
   Add the dedicated race, job, avatar, and name-selection owners before treating create-character parity as complete.
4. Utility-dialog pass:
   Add connection notices, recommend-world flows, and login utility dialogs once the main entry shell exists.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
