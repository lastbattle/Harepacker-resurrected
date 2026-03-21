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
- `CField_Battlefield::OnTeamChanged` at `0x5499e0`
- `CField_Battlefield::SetUserTeam` at `0x549870`
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
- `CField_Battlefield::OnTeamChanged` at `0x5499e0`
- `CField_Battlefield::SetUserTeam` at `0x549870`
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
| Partial | Guild boss event fields | The simulator now routes special-field ownership through `SpecialFieldRuntimeCoordinator`, auto-binds guild-boss maps into that dedicated runtime seam, configures `GuildBossField` directly from the map's `healer` and `pulley` WZ nodes, loads the exact `Map/Obj/guild.img/syarenian/boss/{0,1}` animated art with origin-aligned placement, exposes a `/guildboss` debug seam that drives healer movement and pulley state through the same packet-shaped runtime path, and now lets the local player trigger that pulley seam from the real `Up` interact input when their hitbox overlaps the verified pulley rectangle, consuming the map's `rise`, `fall`, `healMin`, and `healMax` values for a small client-aligned 1 -> 2 -> 0 pulley sequence with healer movement, heal cue text, and in-range prompting, but it still does not yet mirror the client's full guild-boss event flow or broader remote packet choreography outside the healer or pulley mechanic pair | This item now has a stable simulator-owned entry point with client-backed map props, art loading, and a non-debug local interaction path, so later work can focus on broader event choreography and packet coverage instead of first reconstructing the field shell | `MapSimulator.cs`, `SpecialFieldRuntimeCoordinator.cs`, `SpecialEffectFields.cs`, `GuildBossField` (`CField_GuildBoss`, `CField_GuildBoss::Init`, `CField_GuildBoss::OnPacket`, `CHealer::Init`, `CHealer::Move`, `CPulley::Init`) |
| Partial | Coconut minigame runtime | The simulator now follows the client's finish-timestamp clocking, keeps score packet-owned instead of inventing local score on ground contact, applies packet-driven coconut state changes through the runtime's delayed hit queue, mirrors `CField_Coconut::BasicActionAttack` by selecting an intersecting coconut from the player's normal-attack world hitbox before queuing the delayed state change, draws the client board from `Map/Obj/etc.img/coconut/backgrnd`, renders the client bitmap score and time fonts at the IDA-verified `DrawBoard` positions, and shows a WZ-backed victory or lose banner from `Map/Effect.img/event/coconut` when the round ends, but it still does not reproduce the client's full packet-driven result flow beyond that local banner | Coconut is another field mode with its own client-owned action loop, not a variant of ordinary combat or map-object handling | `MinigameFields.cs`, `MapSimulator.cs`, `CoconutField` (`CField_Coconut::OnClock`, `CField_Coconut::OnCoconutHit`, `CField_Coconut::BasicActionAttack`, `CField_Coconut::DrawBoard`, `CField_Coconut::OnCoconutScore`) |
| Partial | Wedding ceremony fields | The simulator now resolves ceremony lines from `String/Npc.img/<npcId>/wedding{step}` with WZ-backed fallbacks, distinguishes groom or bride versus guest state from the local character id, mirrors the chapel-only step-2 guest bless prompt that `CField_Wedding::OnWeddingProgress` special-cases, records the client's modal response packet flow through step-advance (`163`) and guest-bless (`164`) ceremony responses, exposes a `/wedding` debug seam to drive progress, actor anchors, and responses through that same runtime, and now loads the real bless animation frames from `Effect/BasicEff.img/Wedding` while keeping that effect alive until the same client-owned clear path disables it instead of timing it out locally, but it still does not reproduce the client's wedding BGM swap, broader ceremony scene transitions, or full remote actor rendering | Wedding maps have visible simulator support today, and the remaining gap is now the ceremony transition, BGM, and full actor-presentation side rather than placeholder text, packet acknowledgement, or a locally invented bless-effect lifetime | `SpecialEffectFields.cs`, `WeddingField`, `MapSimulator.cs` (`CField_Wedding::OnWeddingProgress`, `CField_Wedding::SetBlessEffect`) |
| Implemented | Witchtower score UI | The simulator anchors the Witchtower scoreboard to the client's center-top window origin, loads the exact `Map/Obj/etc.img/goldkey` background, key, and bitmap digits that match the `CScoreboard_Witchtower::OnCreate` asset shape, renders the zero-padded score at the verified `(67, 4)` draw origin with the client's negative digit spacing, and keeps the `witchscore` debug seam driving the same update path | This is a visible special-field HUD with client-backed art and placement now, so the remaining special-field work should move to other rows instead of revisiting a placeholder Witchtower overlay | `SpecialEffectFields.cs`, `WitchtowerField`, `MapSimulator.cs` (`CField_Witchtower::OnScoreUpdate`, `CScoreboard_Witchtower::OnCreate`, `CScoreboard_Witchtower::Draw`) |
| Partial | Massacre timerboard and gauge flow | The simulator now mirrors the client's type-2 timerboard ownership at the verified center-top origin, draws a dedicated MM:SS timer surface instead of a generic corner clock, advances a queued three-step key pulse whenever the gauge increases, applies `mobMassacre` map defaults for `gauge.total`, `gauge.decrease`, `gauge.hitAdd`, and `disableSkill` instead of hardcoded HUD values, surfaces the map's `countEffect` thresholds as next-milestone plus reached-threshold HUD state, and routes `/massacre kill` through that configured default gauge increment, but it still approximates the exact WZ canvases and still does not replay the client's full packet-fed count-effect or result presentation sequence | Hunting-event maps depend on dedicated timer and gauge behavior that the client does not route through the normal status bar, and the simulator now follows map-owned Massacre gauge config, disable-skill variants, and milestone thresholds instead of a one-size-fits-all placeholder HUD | `SpecialEffectFields.cs`, `MassacreField`, `MapSimulator.cs` (`CField_Massacre::Init`, `CField_Massacre::OnClock`, `CField_Massacre::UpdateKeyAnimation`, `CTimerboard_Massacre::Draw`) |
| Partial | Memory Game and MiniRoom card parity | The simulator now syncs the MiniRoom shell occupant roster and status text to the live Match Cards board, accepts mouse-driven sidebar and card hit testing on the `CMemoryGameDlg`-style surface, exposes delayed remote ready/start/flip/tie/give-up/end actions through the same runtime path that the local room uses, adds a typed `MemoryGamePacketType` dispatch seam behind `/memorygame packet ...` for open/ready/start/flip/tie/give-up/end or mode actions, and loads the dedicated `UI/UIWindow(.2).img/Minigame/MemoryGame` plus `Minigame/Common` canvases for the Match Cards board, card faces, ready markers, turn arrow, result banner, and button chrome at the client-backed `CMemoryGameDlg` draw anchors, but it still does not consume real MiniRoom network packets or fully mirror the shared `CMiniRoomBaseDlg` shell and chat presentation around Match Cards | MiniRoom parity now covers the room shell, board state machine, click flow, packet-shaped room updates, and WZ-backed Match Cards presentation behind one runtime seam, so the remaining work is focused on true packet decode plus the last shared MiniRoom chrome gaps rather than missing gameplay ownership | `MinigameFields.cs`, `SocialRoomModels.cs`, `SocialRoomWindow.cs`, `MapSimulator.cs` (`CMemoryGameDlg::OnCreate`, `CMemoryGameDlg::Draw`, `CMemoryGameDlg::OnButtonClicked`) |
| Partial | Snowball minigame runtime | The simulator now treats Snowball state `2/3` as the client's packet-owned win states instead of local touch-zone states, keeps snowman HP packet-owned via `OnSnowBallState`, applies WZ-backed snowman stun windows and damage defaults through the runtime seam, smooths active snowball movement from the packet position plus speed data instead of instant teleports, and routes the local player's world position through `SpecialFieldRuntimeCoordinator` so `SnowBallField` can surface the client's outbound touch-packet loop as pending team touch requests whenever the local player overlaps a snowball lane, but it still does not reproduce the client's exact `CSnowBall::ms_anDelay` cadence table or localized `OnSnowBallMsg` chat formatting | Snowball is one of the clearer cases where the simulator has a usable baseline but not the full client-owned minigame loop, and the remaining gap is now narrower and centered on exact timing plus packet/UI polish instead of the core round-state model | `MinigameFields.cs`, `SnowBallField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator/SnowBallFieldTests.cs` (`CField_SnowBall::OnSnowBallState`, `CField_SnowBall::BasicActionAttack`, `CField_SnowBall::OnSnowBallHit`, `CField_SnowBall::OnSnowBallMsg`, `CField_SnowBall::OnSnowBallTouch`) |
| Partial | Ariant Arena field flow | The simulator now auto-binds `FIELDTYPE_ARIANTARENA` maps into a dedicated `AriantArenaField`, mirrors the client's sorted top-left ranking surface with WZ-backed `AriantMatch/characterIcon` rank icons, plays the WZ-backed `AriantMatch/Result` animation from the client's center-top result origin, triggers a WZ-backed Ariant result UI sound when the result layer appears, pushes local player name plus job into the runtime each frame so the client-owned 8xx or 9xx local-job rank suppression is honored, and exposes an `ariantarena` debug seam that can now drive either single-row or packet-shaped batched score updates through the same sorted refresh path, but it still does not consume real Ariant packets or redraw real remote name tags because the simulator still lacks the client's user-pool-backed remote actor layer | Arena scoring and ranking are owned by a dedicated field class in the client and are not covered by generic mob or UI behavior, so having a simulator-owned runtime now makes the remaining packet and player-state deltas explicit instead of leaving Ariant invisible | `MinigameFields.cs`, `AriantArenaField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_AriantArena::OnUserScore`, `CField_AriantArena::UpdateScoreAndRank`, `CField_AriantArena::OnShowResult`) |
| Partial | Battlefield event flow | The simulator now detects Battlefield maps through the special-field runtime, loads `battleField` WZ defaults such as `timeDefault`, `timeFinish`, reward maps, and effect paths, follows the client's packet-owned `OnClock(type=2, seconds)` and `OnScoreUpdate(wolves, sheep)` seams, exposes a local Battlefield team-change seam aligned with `OnTeamChanged(characterId, team)` and `SetUserTeam`, resolves a finish phase that derives win/lose effect plus reward-map routing from the WZ `battleField` node, and now parses the map's `user/<team>/look` presets so local Battlefield team changes reapply the client-backed cap, clothes, glove, shoe, cape, and pants loadout from WZ before restoring the original avatar on clear or map exit, but it still does not reproduce the exact client scoreboard canvases, remote user look swaps, or the full map-transition and minimap-side effects that the client applies around `SetUserTeam` | Battlefield maps depend on a team-scoring field runtime rather than only ordinary combat rules, and the client keeps both the scoreboard window creation and team totals inside `CField_Battlefield` rather than generic field HUD code | `SpecialEffectFields.cs`, `BattlefieldField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_Battlefield::OnClock`, `CField_Battlefield::OnScoreUpdate`, `CField_Battlefield::OnTeamChanged`, `CField_Battlefield::SetUserTeam`) |
| Partial | Mu Lung Dojo field flow | The simulator now binds `FIELDTYPE_DOJANG` and the `MuruengRaid` map block into a dedicated `DojoField`, mirrors the client's type-2 clock ownership into the real `UI/UIWindow(.img)/muruengRaid` timerboard with bitmap digits and colon placement, draws the Dojo player, monster, and energy gauges from the client's WZ canvases at the IDA-verified layer anchors, swaps to the looping `energy/full` animation at max energy, and replays the stage-start, clear, and time-over presentations from `Map/Effect.img/dojang` through `/dojostage` and `/dojoresult` debug seams plus the live timer-expiry path, but it still does not consume real Dojo energy packets or reproduce the broader server-driven stage transition flow beyond those simulator-owned seams | Dojo behavior is timer-driven and mode-specific, and the simulator now has a client-backed HUD and presentation layer inside the dedicated runtime seam, leaving the remaining packet and transition parity work explicit instead of hidden behind placeholder rendering | `SpecialEffectFields.cs`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_Dojang::Init`, `CField_Dojang::OnClock`, `CField_Dojang::Update`, `CField_Dojang::UpdateTimer`) |
| Partial | Cookie House event flow | The simulator now binds `fieldType == CookieHouse` into a dedicated `CookieHouseField` runtime, mirrors the client-owned HUD geometry from `CField_CookieHouse::Init`, tracks score updates through a score-owned redraw path instead of generic UI, replaces the old solid-color placeholder with WZ-backed HUD panel pieces plus five grade badge frames loaded from `UI/UIWindow2.img/raise`, and still exposes a `cookiepoint` debug seam for manual verification, but it still falls back to placeholder grade thresholds, does not yet decode the client's exact `CBitmapNumber` digit asset path, and still lacks the live `CWvsContext`-driven point source | Cookie House is another distinct score-driven field, and this row now has a stable simulator-owned entry point for future packet or asset parity work instead of remaining invisible in planning | `CookieHouseField.cs`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_CookieHouse::_UpdatePoint`, `CField_CookieHouse::Init`) |
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
