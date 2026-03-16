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

- A `SpecialEffectFields` surface with wedding, witchtower, guild-boss, and massacre handling.
- A concrete `MinigameFields` surface that now owns the snowball and coconut runtimes behind one backlog-aligned seam.
- A `SpecialFieldRuntimeCoordinator` seam in `MapSimulator` that binds special-field ownership to map load, update, draw, and reset flow so later agents can take one backlog row without redoing the simulator plumbing.
- Several packet-commented simulator seams that already reference the corresponding client field owners.

The parity gap here is not whether the simulator recognizes special maps at all.
The gap is whether those maps reproduce the client's dedicated rules, scoreboards, timers, UI flows, and win or lose transitions.

## Remaining Parity Backlog

### 1. Special field and minigame parity

This area is currently unowned by backlog documents even though the client and simulator both have dedicated field-specific seams.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Guild boss event fields | The simulator now routes special-field ownership through `SpecialFieldRuntimeCoordinator`, auto-binds guild-boss maps into that dedicated runtime seam, and still exposes healer plus pulley state through `GuildBossField`, but it does not yet mirror the client's full guild-boss event flow, UI cues, encounter sequencing, or wider packet-driven interactions beyond those mechanics | This item now has a stable simulator-owned entry point that other agents can extend without touching unrelated field logic, and IDA confirms the client treats guild boss as a dedicated field owner rather than a generic boss fight | `MapSimulator.cs`, `SpecialFieldRuntimeCoordinator.cs`, `SpecialEffectFields.cs`, `GuildBossField` (`CField_GuildBoss`) |
| Partial | Coconut minigame runtime | The simulator now follows the client's finish-timestamp clocking, keeps score packet-owned instead of inventing local score on ground contact, and applies packet-driven coconut state changes through the runtime's delayed hit queue, but it still does not reproduce the client's normal-attack targeting, exact board art or text placement, and wider round-result presentation | Coconut is another field mode with its own client-owned action loop, not a variant of ordinary combat or map-object handling | `MinigameFields.cs`, `CoconutField` (`CField_Coconut::OnClock`, `CField_Coconut::OnCoconutHit`, `CField_Coconut::BasicActionAttack`, `CField_Coconut::DrawBoard`, `CField_Coconut::OnCoconutScore`) |
| Partial | Wedding ceremony fields | The simulator now resolves ceremony lines from `String/Npc.img/<npcId>/wedding{step}` with WZ-backed fallbacks, distinguishes groom or bride versus guest state from the local character id, mirrors the chapel-only step-2 guest bless prompt that `CField_Wedding::OnWeddingProgress` special-cases, and anchors the bless sparkle pass to the couple instead of a generic screen-center overlay, but it still does not reproduce the client's modal response packets, remote participant actors, or broader ceremony scene transitions | Wedding maps have visible simulator support today, and the remaining gap is now the packet and multi-actor side of the ceremony rather than placeholder text or a generic overlay effect | `SpecialEffectFields.cs`, `WeddingField` (`CField_Wedding::OnWeddingProgress`, `CField_Wedding::SetBlessEffect`) |
| Partial | Witchtower score UI | The simulator now anchors the Witchtower scoreboard to the client's center-top window origin, uses a dedicated two-layer widget with a zero-padded score readout at the client draw coordinates, and exposes a `witchscore` debug seam that drives the same update path, but it still approximates the client art instead of loading the exact WZ canvases that `CScoreboard_Witchtower::OnCreate` resolves | This is already a visible special-field HUD, and IDA confirms the client uses a dedicated scoreboard owner rather than a generic overlay | `SpecialEffectFields.cs`, `WitchtowerField`, `MapSimulator.cs` (`CField_Witchtower::OnScoreUpdate`, `CScoreboard_Witchtower::OnCreate`, `CScoreboard_Witchtower::Draw`) |
| Partial | Massacre timerboard and gauge flow | The simulator already models massacre gauge growth, clear effects, and a basic field HUD, but it still does not match the client's clock setup, key-animation cadence, and full result or timerboard lifecycle | Hunting-event maps depend on dedicated timer and gauge behavior that the client does not route through the normal status bar | `SpecialEffectFields.cs`, `MassacreField` (`CField_Massacre::OnClock`, `CField_Massacre::UpdateKeyAnimation`, `CTimerboard_Massacre::Draw`) |
| Partial | Memory Game and MiniRoom card parity | The simulator now exposes a dedicated Memory Game MiniRoom runtime with a visible room shell, ready/start/tie/give-up/end command seams that mirror `CMemoryGameDlg::OnButtonClicked`, a card board with pair matching and delayed mismatch hides, turn ownership with countdown resets, and result-to-lobby flow, but it still does not hook into a broader room occupant system, mouse-driven button hit testing, or packet-fed remote interactions | MiniRoom parity is no longer a blank spot: the simulator now owns the card-board state machine and dialog shell that future MiniRoom work can extend into full social-room packet and input parity | `MinigameFields.cs`, minigame room runtime, board UI layer (`CMemoryGameDlg::OnCreate`, `CMemoryGameDlg::Draw`, `CMemoryGameDlg::OnButtonClicked`) |
| Partial | Snowball minigame runtime | The simulator already has snowball field state, hit, message, touch-zone handling, and simple win detection, but it still does not reproduce the client's full action cadence, snowman stun rules, team scoring flow, and result handling | Snowball is one of the clearer cases where the simulator has a usable baseline but not the client-owned minigame loop | `MinigameFields.cs`, `SnowBallField` (`CField_SnowBall::OnSnowBallState`, `CField_SnowBall::BasicActionAttack`, `CField_SnowBall::OnSnowBallHit`, `CField_SnowBall::OnSnowBallMsg`, `CField_SnowBall::OnSnowBallTouch`) |
| Partial | Ariant Arena field flow | The simulator now auto-binds `FIELDTYPE_ARIANTARENA` maps into a dedicated `AriantArenaField`, mirrors the client's sorted top-left ranking surface with WZ-backed `AriantMatch/characterIcon` rank icons, plays the WZ-backed `AriantMatch/Result` animation from the client's center-top result origin, and exposes an `ariantarena` debug seam that drives the same score or result path, but it still does not reproduce the client's packet decode path, job-filter edge cases, or remote name-tag refresh side effects | Arena scoring and ranking are owned by a dedicated field class in the client and are not covered by generic mob or UI behavior, so having a simulator-owned runtime now makes the remaining packet and player-state deltas explicit instead of leaving Ariant invisible | `MinigameFields.cs`, `AriantArenaField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_AriantArena::OnUserScore`, `CField_AriantArena::UpdateScoreAndRank`, `CField_AriantArena::OnShowResult`) |
| Partial | Battlefield event flow | The simulator now detects Battlefield maps through the special-field runtime, loads `battleField` WZ defaults such as `timeDefault`, `timeFinish`, reward maps, and effect paths, and exposes a centered timerboard plus wolf or sheep score flow that follows the client's packet-owned `OnClock(type=2, seconds)` and `OnScoreUpdate(wolves, sheep)` seams, but it still does not reproduce team-change packets, exact client art, or the broader reward and result transition pipeline | Battlefield maps depend on a team-scoring field runtime rather than only ordinary combat rules, and the client keeps both the scoreboard window creation and team totals inside `CField_Battlefield` rather than generic field HUD code | `SpecialEffectFields.cs`, `BattlefieldField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_Battlefield::OnClock`, `CField_Battlefield::OnScoreUpdate`) |
| Partial | Mu Lung Dojo field flow | The simulator now binds `FIELDTYPE_DOJANG` and the `MuruengRaid` map block into a dedicated `DojoField`, mirrors the client's type-2 clock ownership into a Dojo-only timer HUD, continuously surfaces boss HP plus player HP through dedicated gauges, and exposes a manual energy plus timer debug seam for exercising the field flow, but it still does not load the client's exact WZ canvases, consume real Dojo energy packets, or reproduce the full stage-clear and result presentation path | Dojo behavior is timer-driven and mode-specific, and the simulator now has a backlog-owned runtime seam that keeps its HUD and timer flow out of generic map logic while leaving the packet and art parity work explicit | `SpecialEffectFields.cs`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_Dojang::OnClock`, `CField_Dojang::Update`) |
| Partial | Cookie House event flow | The simulator now binds `fieldType == CookieHouse` into a dedicated `CookieHouseField` runtime, mirrors the client-owned HUD geometry from `CField_CookieHouse::Init`, tracks score updates through a score-owned redraw path instead of generic UI, and exposes a `cookiepoint` debug seam for manual verification, but it still uses placeholder grade thresholds and art instead of the client's exact bitmap-number assets and live `CWvsContext`-driven point source | Cookie House is another distinct score-driven field, and this row now has a stable simulator-owned entry point for future packet or asset parity work instead of remaining invisible in planning | `CookieHouseField.cs`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_CookieHouse::_UpdatePoint`, `CField_CookieHouse::Init`) |
| Partial | Monster Carnival field flow | The simulator now detects Monster Carnival maps through the backlog coordinator, loads the map's WZ-backed Carnival definition into a dedicated runtime, surfaces the client-owned mob, skill, and guardian HUD lists with CP counters, and exposes simulator seams for enter, request-result, game-result, and summoned-mob count updates, but it still does not yet mirror the client's exact localized request or result strings, revive-only result flow, live packet-driven CP deltas, or the actual summon, guardian, and reactor-side gameplay rules | Monster Carnival is a self-contained field mode with client-owned rules and UI, so landing the dedicated runtime and HUD surface makes the parity gap concrete instead of leaving it hidden behind generic mob or field systems | `MonsterCarnivalField.cs`, `MinigameFields.cs`, `MapSimulator.cs` (`CField_MonsterCarnival::CreateUIWindow`, `CField_MonsterCarnival::OnEnter`, `CField_MonsterCarnival::OnRequestResult`, `CField_MonsterCarnival::OnShowGameResult`) |
| Partial | Party Raid field flow | The simulator now binds `FIELDTYPE_PARTYRAID`, `FIELDTYPE_PARTYRAID_BOSS`, and `FIELDTYPE_PARTYRAID_RESULT` into a dedicated `PartyRaidField` runtime, mirrors the client's split between stage or point HUD, boss red-vs-blue damage gauge, and result-session summary overlay, and exposes a `/partyraid` command seam to drive the same point, damage, gauge-cap, and result-state updates before packet handlers are wired, but it still does not consume the real Party Raid packet keys, exact WZ canvases, or the full map-specific result art and timer lifecycle from the client owners | Party Raid is no longer invisible in the simulator: the field, boss, and result variants now have a backlog-owned runtime surface that future packet and art parity work can extend without falling back to generic boss or field HUD code | `SpecialFieldRuntimeCoordinator.cs`, `PartyRaidField.cs`, `MapSimulator.cs` (`CField_PartyRaid::_UpdatePoint`, `CField_PartyRaid::Init`, `CField_PartyRaidBoss::OnFieldSetVariable`, `CField_PartyRaidBoss::OnPartyValue`, `CField_PartyRaidResult::OnSessionValue`) |
| Partial | SpaceGAGA timerboard flow | The simulator now binds `FIELDTYPE_SPACEGAGA` maps into a dedicated `SpaceGagaField`, mirrors the client's clock-type-2 timerboard ownership, keeps the board at the verified center-top window origin, and exposes a `/spacegaga` debug seam that drives the same MM:SS countdown path, but it still approximates the client art instead of loading the exact timerboard source canvas and digit font that `CTimerboard_SpaceGAGA::OnCreate` resolves | This is another example of client-owned special field logic that would otherwise be lost under generic map handling, and the simulator now has a backlog-owned runtime seam for the remaining art-loading follow-up | `SpecialEffectFields.cs`, `SpaceGagaField` (`CField_SpaceGAGA::OnClock`, `CTimerboard_SpaceGAGA::Draw`, `CTimerboard_SpaceGAGA::OnCreate`) |

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
