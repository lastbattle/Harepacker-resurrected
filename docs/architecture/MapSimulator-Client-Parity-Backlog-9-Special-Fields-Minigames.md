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
- `HaCreator/MapSimulator/Effects/SpecialEffectFields.cs`
- `HaCreator/MapSimulator/Fields/MinigameFields.cs`

### Client references checked

- `CField_GuildBoss::CField_GuildBoss` at `0x53e980`
- `CField_Coconut::OnClock` at `0x54a100`
- `CField_Coconut::OnCoconutHit` at `0x54a470`
- `CField_Coconut::BasicActionAttack` at `0x54a5e0`
- `CField_Coconut::DrawBoard` at `0x54a750`
- `CField_Coconut::OnCoconutScore` at `0x54bf70`
- `CField_AriantArena::OnUserScore` at `0x5492b0`
- `CField_AriantArena::OnShowResult` at `0x547630`
- `CField_Battlefield::OnClock` at `0x549ad0`
- `CField_Battlefield::OnScoreUpdate` at `0x5499a0`
- `CField_Dojang::OnClock` at `0x550940`
- `CField_Dojang::Update` at `0x54ef10`
- `CField_CookieHouse::_UpdatePoint` at `0x54daa0`
- `CField_CookieHouse::Init` at `0x54e250`
- `CField_Massacre::OnClock` at `0x556af0`
- `CField_Massacre::UpdateKeyAnimation` at `0x556bf0`
- `CTimerboard_Massacre::Draw` at `0x557100`
- `CField_Massacre::Init` at `0x5579d0`
- `CMemoryGameDlg::OnButtonClicked` at `0x628df0`
- `CMemoryGameDlg::OnCreate` at `0x62f3c0`
- `CMemoryGameDlg::Draw` at `0x631cb0`
- `CField_MonsterCarnival::CreateUIWindow` at `0x55a510`
- `CField_MonsterCarnival::OnEnter` at `0x55a6c0`
- `CField_MonsterCarnival::OnRequestResult` at `0x55a890`
- `CField_MonsterCarnival::OnShowGameResult` at `0x55af80`
- `CField_MonsterCarnivalRevive::OnShowGameResult` at `0x55b110`
- `CField_PartyRaid::_UpdatePoint` at `0x55c120`
- `CField_PartyRaid::Init` at `0x55f110`
- `CField_PartyRaidBoss::OnFieldSetVariable` at `0x55ca40`
- `CField_PartyRaidBoss::OnPartyValue` at `0x55d070`
- `CField_PartyRaidResult::OnSessionValue` at `0x55e5c0`
- `CField_SnowBall::OnSnowBallTouch` at `0x560510`
- `CField_SnowBall::OnSnowBallState` at `0x560ab0`
- `CField_SnowBall::BasicActionAttack` at `0x5617b0`
- `CField_SnowBall::OnSnowBallHit` at `0x5619d0`
- `CField_SnowBall::OnSnowBallMsg` at `0x562040`
- `CField_SpaceGAGA::OnClock` at `0x5625d0`
- `CTimerboard_SpaceGAGA::Draw` at `0x5626c0`
- `CField_Wedding::SetBlessEffect` at `0x563b60`
- `CField_Wedding::OnWeddingProgress` at `0x5640f0`
- `CField_Witchtower::OnScoreUpdate` at `0x564ad0`
- `CScoreboard_Witchtower::Draw` at `0x564bd0`

## Client Function Index By Backlog Area

The list below maps this backlog area to concrete client seams confirmed in IDA.
These are the first functions to inspect before changing simulator behavior.

### 1. Special field and minigame parity

- `CField_GuildBoss::CField_GuildBoss` at `0x53e980`
- `CField_Coconut::OnClock` at `0x54a100`
- `CField_Coconut::OnCoconutHit` at `0x54a470`
- `CField_Coconut::BasicActionAttack` at `0x54a5e0`
- `CField_Coconut::DrawBoard` at `0x54a750`
- `CField_AriantArena::OnUserScore` at `0x5492b0`
- `CField_AriantArena::OnShowResult` at `0x547630`
- `CField_Battlefield::OnClock` at `0x549ad0`
- `CField_Battlefield::OnScoreUpdate` at `0x5499a0`
- `CField_Dojang::OnClock` at `0x550940`
- `CField_Dojang::Update` at `0x54ef10`
- `CField_CookieHouse::_UpdatePoint` at `0x54daa0`
- `CField_CookieHouse::Init` at `0x54e250`
- `CField_Wedding::OnWeddingProgress` at `0x5640f0`
- `CField_Wedding::SetBlessEffect` at `0x563b60`
- `CField_Witchtower::OnScoreUpdate` at `0x564ad0`
- `CScoreboard_Witchtower::Draw` at `0x564bd0`
- `CField_Massacre::OnClock` at `0x556af0`
- `CField_Massacre::UpdateKeyAnimation` at `0x556bf0`
- `CTimerboard_Massacre::Draw` at `0x557100`
- `CMemoryGameDlg::OnButtonClicked` at `0x628df0`
- `CMemoryGameDlg::OnCreate` at `0x62f3c0`
- `CMemoryGameDlg::Draw` at `0x631cb0`
- `CField_SnowBall::OnSnowBallState` at `0x560ab0`
- `CField_SnowBall::OnSnowBallHit` at `0x5619d0`
- `CField_SnowBall::OnSnowBallMsg` at `0x562040`
- `CField_SnowBall::OnSnowBallTouch` at `0x560510`
- `CField_MonsterCarnival::CreateUIWindow` at `0x55a510`
- `CField_MonsterCarnival::OnEnter` at `0x55a6c0`
- `CField_PartyRaid::Init` at `0x55f110`
- `CField_PartyRaidBoss::OnFieldSetVariable` at `0x55ca40`
- `CField_PartyRaidResult::OnSessionValue` at `0x55e5c0`
- `CField_SpaceGAGA::OnClock` at `0x5625d0`
- `CTimerboard_SpaceGAGA::Draw` at `0x5626c0`

Notes:
IDA shows that these field modes are not generic map-rule variants. The client owns separate state machines, packet handlers, timer or scoreboard widgets, and even dedicated minigame dialog owners such as `CMemoryGameDlg` for Guild Boss, Coconut, Wedding, Witchtower, Massacre, SnowBall, Ariant Arena, Battlefield, Dojang, Cookie House, Monster Carnival, Party Raid, SpaceGAGA, and MiniRoom card games, so parity work should be tracked separately from the broader field, combat, or UI backlog areas.

## Current State Summary

MapSimulator already has a partial special-field baseline instead of a blank area.
The project currently includes:

- A `SpecialEffectFields` surface with wedding, witchtower, and massacre effect handling.
- A `MinigameFields` surface with snowball-specific state, hit, and message handling.
- Several packet-commented simulator seams that already reference the corresponding client field owners.

The parity gap here is not whether the simulator recognizes special maps at all.
The gap is whether those maps reproduce the client's dedicated rules, scoreboards, timers, UI flows, and win or lose transitions.

## Remaining Parity Backlog

### 1. Special field and minigame parity

This area is currently unowned by backlog documents even though the client and simulator both have dedicated field-specific seams.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Guild boss event fields | The simulator already detects guild-boss maps and exposes healer plus pulley state through a dedicated field surface, but it still does not mirror the client's full guild-boss event flow, UI cues, encounter sequencing, or wider packet-driven interactions beyond those two mechanics | This is already a distinct simulator event mode, and IDA confirms the client treats guild boss as a dedicated field owner rather than a generic boss fight | `SpecialEffectFields.cs`, `GuildBossField` (`CField_GuildBoss`) |
| Partial | Coconut minigame runtime | The simulator already models coconut object state, score updates, board drawing, and basic attack-driven state changes, but it still does not reproduce the client's full timing, object-state transitions, and round-result flow | Coconut is another field mode with its own client-owned action loop, not a variant of ordinary combat or map-object handling | `MinigameFields.cs`, `CoconutField` (`CField_Coconut::OnClock`, `CField_Coconut::OnCoconutHit`, `CField_Coconut::BasicActionAttack`, `CField_Coconut::DrawBoard`, `CField_Coconut::OnCoconutScore`) |
| Partial | Wedding ceremony fields | The simulator already detects wedding maps, queues ceremony dialogs, and plays a simplified bless-effect sparkle pass, but it still does not mirror the client's full ceremony step scripting, NPC dialog sourcing, participant state flow, or packet-driven scene transitions owned by `CField_Wedding` | Wedding maps have visible simulator support today, so the remaining work is concrete client-sequence parity rather than generic effect polish | `SpecialEffectFields.cs`, `WeddingField` (`CField_Wedding::OnWeddingProgress`, `CField_Wedding::SetBlessEffect`) |
| Partial | Witchtower score UI | The simulator already exposes a witchtower score tracker and draws a lightweight scoreboard, but it still lacks the client widget's exact score presentation, focus behavior, and broader event-state integration | This is already a visible special-field HUD, and IDA confirms the client uses a dedicated scoreboard owner rather than a generic overlay | `SpecialEffectFields.cs`, `WitchtowerField` (`CField_Witchtower::OnScoreUpdate`, `CScoreboard_Witchtower::Draw`) |
| Partial | Massacre timerboard and gauge flow | The simulator already models massacre gauge growth, clear effects, and a basic field HUD, but it still does not match the client's clock setup, key-animation cadence, and full result or timerboard lifecycle | Hunting-event maps depend on dedicated timer and gauge behavior that the client does not route through the normal status bar | `SpecialEffectFields.cs`, `MassacreField` (`CField_Massacre::OnClock`, `CField_Massacre::UpdateKeyAnimation`, `CTimerboard_Massacre::Draw`) |
| Missing | Memory Game and MiniRoom card parity | IDA shows `CMemoryGameDlg` owns a dedicated Memory Game or Match Cards dialog with its own creation path, draw surface, and button-click flow, but the simulator does not currently expose the corresponding board state, turn flow, card-reveal rules, or room-driven UI shell | MiniRoom parity is not only about a generic room container; one of the client’s most visible room minigames has its own concrete dialog owner and interaction model that remains entirely absent | minigame room runtime, board UI layer (`CMemoryGameDlg::OnCreate`, `CMemoryGameDlg::Draw`, `CMemoryGameDlg::OnButtonClicked`) |
| Partial | Snowball minigame runtime | The simulator already has snowball field state, hit, message, touch-zone handling, and simple win detection, but it still does not reproduce the client's full action cadence, snowman stun rules, team scoring flow, and result handling | Snowball is one of the clearer cases where the simulator has a usable baseline but not the client-owned minigame loop | `MinigameFields.cs`, `SnowBallField` (`CField_SnowBall::OnSnowBallState`, `CField_SnowBall::BasicActionAttack`, `CField_SnowBall::OnSnowBallHit`, `CField_SnowBall::OnSnowBallMsg`, `CField_SnowBall::OnSnowBallTouch`) |
| Missing | Ariant Arena field flow | IDA shows dedicated score and result handlers for Ariant Arena, but the simulator does not currently expose a corresponding event runtime, ranking surface, or result presentation | Arena scoring and ranking are owned by a dedicated field class in the client and are not covered by generic mob or UI behavior | special field runtime, ranking or result UI (`CField_AriantArena::OnUserScore`, `CField_AriantArena::OnShowResult`) |
| Missing | Battlefield event flow | The client has a dedicated battlefield field class with team-change, score-update, and clock handlers, but the simulator does not appear to surface that event mode today | Battlefield maps depend on a team-scoring field runtime rather than only ordinary combat rules | special field runtime, scoreboard layer (`CField_Battlefield::OnClock`, `CField_Battlefield::OnScoreUpdate`) |
| Missing | Mu Lung Dojo field flow | IDA shows a dedicated dojo field with timer update and draw logic, but the simulator does not currently track dojo progression as its own field mode | Dojo behavior is timer-driven and mode-specific, so it should not be left implied under generic map logic | special field runtime, timer or score HUD (`CField_Dojang::OnClock`, `CField_Dojang::Update`) |
| Missing | Cookie House event flow | The client owns a dedicated Cookie House field with its own point-update and initialization path, but the simulator does not currently expose that event mode | Cookie House is another distinct score-driven field that would otherwise stay invisible in parity planning | special field runtime (`CField_CookieHouse::_UpdatePoint`, `CField_CookieHouse::Init`) |
| Missing | Monster Carnival field flow | IDA shows dedicated field-enter, request-result, revive, game-result, and UI-window handlers for Monster Carnival, but the simulator does not appear to expose a corresponding runtime or HUD surface yet | Monster Carnival is a self-contained field mode with client-owned rules and UI, so leaving it untracked hides a large parity gap behind generic mob or field systems | special field runtime, event UI layer (`CField_MonsterCarnival::CreateUIWindow`, `CField_MonsterCarnival::OnEnter`, `CField_MonsterCarnival::OnRequestResult`, `CField_MonsterCarnival::OnShowGameResult`) |
| Missing | Party Raid field flow | The client has dedicated Party Raid, boss, and result field classes that manage point updates, field variables, party values, and result-session rendering, but the simulator does not currently track that mode as its own runtime area | Party Raid behavior is not covered by normal field-limit or boss HP logic; it has its own event-state pipeline and result handling | special field runtime, scoreboard and timer layer (`CField_PartyRaid::_UpdatePoint`, `CField_PartyRaid::Init`, `CField_PartyRaidBoss::OnFieldSetVariable`, `CField_PartyRaidBoss::OnPartyValue`, `CField_PartyRaidResult::OnSessionValue`) |
| Missing | SpaceGAGA timerboard flow | IDA confirms a dedicated SpaceGAGA field clock and timerboard draw surface, but there is no simulator-owned counterpart yet | This is another example of client-owned special field logic that would otherwise be lost under generic map handling | special field runtime, timerboard UI (`CField_SpaceGAGA::OnClock`, `CTimerboard_SpaceGAGA::Draw`) |

## Priority Order

If the goal is visible parity first, the next work in this area should be sequenced like this:

1. Existing-special-field refinement pass:
   Tighten Wedding, Witchtower, Massacre, and SnowBall behavior against the client owners that already have simulator counterparts.
2. Missing-event-field bring-up pass:
   Add Monster Carnival, Party Raid, SpaceGAGA, and MiniRoom card-game runtime shells so the major field-specific owners present in the client are at least represented in the simulator.
3. Packet and HUD parity pass:
   Match timerboard, scoreboard, and result-surface behavior before extending generic field systems again.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
