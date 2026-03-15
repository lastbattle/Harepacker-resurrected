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
| Partial | Login bootstrap and packet-state parity | The simulator now exposes a dedicated login bootstrap runtime with client-backed step names, delayed step changes, and a login-packet router on login maps, but it is still command-driven and not yet wired to real account UI, roster UI, or socket-backed packet intake | Without the login-state owner, all account-facing transitions are effectively skipped, which hides an entire category of client behavior before the character ever enters a map. The new runtime establishes that owner so later roster, avatar, and dialog work can attach to a real pre-field pipeline instead of jumping straight into a field | account-entry runtime, state machine, packet-routing layer (`CLogin::Init`, `CLogin::Update`, `CLogin::OnPacket`; simulator seam: `HaCreator/MapSimulator/Managers/LoginRuntimeManager.cs`) |
| Partial | Character roster and selection parity | The simulator now exposes a dedicated login-map character-select window, a roster model with selection and delete validation, and a roster-driven field-entry handoff that swaps the selected build before map transition, but it still relies on simulator-seeded roster entries and does not yet include the client's full avatar-card layout, slot paging, or backend-fed character list | The client's path into gameplay is built around explicit character selection rather than spawning a hardcoded local avatar directly into a field, so even this partial shell materially changes how the simulator enters gameplay on login maps | roster UI layer, character-selection model (`CUICharSelect::OnCreate`, `CUICharSelect::OnButtonClicked`; simulator seams: `HaCreator/MapSimulator/Managers/LoginCharacterRosterManager.cs`, `HaCreator/MapSimulator/UI/Windows/CharacterSelectWindow.cs`, `HaCreator/MapSimulator/MapSimulator.cs`) |
| Missing | Avatar preview carousel parity | `CUIAvatar::OnCreate` and `CUIAvatar::OnMouseButton` confirm a dedicated avatar-preview owner for clickable character presentation, name-tag focus, and preview-state transitions, but the simulator does not currently expose any equivalent avatar carousel or preview interaction flow | Character-entry parity needs more than a text roster; the client lets the player browse and inspect live avatar previews before entering the game | avatar-preview UI layer, character-preview assembly path (`CUIAvatar::OnCreate`, `CUIAvatar::OnMouseButton`) |
| Missing | Character detail panel parity | `CUICharDetail::Draw` confirms a separate character-detail owner for per-character metadata and presentation, but the simulator does not currently surface a dedicated details pane for the selected character | The client distinguishes high-level roster browsing from the focused detail view, so skipping the detail panel hides a visible layer of pre-entry feedback | character-detail UI layer, character metadata presentation (`CUICharDetail::Draw`) |
| Missing | Recommend world helper parity | `CUIRecommendWorld::OnCreate`, `CUIRecommendWorld::Draw`, and `CUIRecommendWorld::OnButtonClicked` confirm a dedicated recommend-world helper surface, but the simulator does not currently expose any equivalent recommendation or guided world-selection flow | Server recommendation is part of the client's account-entry UX, not just an incidental line of text on the world list | world-recommendation UI layer, world-selection assist flow (`CUIRecommendWorld::OnCreate`, `CUIRecommendWorld::Draw`, `CUIRecommendWorld::OnButtonClicked`) |
| Missing | Connection-notice and login-utility dialog parity | `CConnectionNoticeDlg::Draw` and `CLoginUtilDlg::Draw` confirm dedicated utility and notice dialogs during account entry, but the simulator does not currently expose connection notices, utility prompts, or the broader modal dialog family that gates login and character-entry actions | Many account-entry flows are mediated through modal notices and utility prompts, so parity will remain incomplete even if the main roster UI appears without these supporting surfaces | account-entry dialog layer, modal utility flow (`CConnectionNoticeDlg::Draw`, `CLoginUtilDlg::Draw`) |

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
