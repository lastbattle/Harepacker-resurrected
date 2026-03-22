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
| Partial | Guild boss event fields | The simulator now routes special-field ownership through `SpecialFieldRuntimeCoordinator`, auto-binds guild-boss maps into that dedicated runtime seam, configures `GuildBossField` directly from the map's `healer` and `pulley` WZ nodes, loads the exact `Map/Obj/guild.img/syarenian/boss/{0,1}` animated art with origin-aligned placement, exposes a `/guildboss` debug seam that can now inject raw client packet types `344` and `345`, decodes those packet payloads through a dedicated runtime packet path that mirrors `CField_GuildBoss::OnPacket`, and cancels the simulator's local pulley preview whenever an external healer or pulley packet arrives so packet-owned state wins over local scheduling. The local player can still trigger that pulley seam from the real `Up` interact input when their hitbox overlaps the verified pulley rectangle, and that preview now advances through the same packet decoder plus has targeted unit coverage for packet decode and preview-cancellation behavior, but it still does not yet mirror the client's full guild-boss event flow beyond the healer or pulley pair or receive real live network traffic from the simulator transport | This item now has a stable simulator-owned entry point with client-backed map props, art loading, an explicit packet decoder for the two client-owned guild-boss updates, and a non-debug local interaction path, so later work can focus on true transport wiring and any broader event choreography instead of first reconstructing the field shell | `MapSimulator.cs`, `SpecialFieldRuntimeCoordinator.cs`, `SpecialEffectFields.cs`, `UnitTest_MapSimulator/GuildBossFieldTests.cs`, `GuildBossField` (`CField_GuildBoss`, `CField_GuildBoss::Init`, `CField_GuildBoss::OnPacket`, `CHealer::Init`, `CHealer::Move`, `CPulley::Init`) |
| Partial | Coconut minigame runtime | The simulator now follows the client's finish-timestamp clocking, keeps score packet-owned instead of inventing local score on ground contact, applies packet-driven coconut state changes through the runtime's delayed hit queue, mirrors `CField_Coconut::BasicActionAttack` by selecting an intersecting coconut from the player's normal-attack world hitbox before queuing the delayed state change, draws the client board from `Map/Obj/etc.img/coconut/backgrnd`, renders the client bitmap score and time fonts at the IDA-verified `DrawBoard` positions, and shows a WZ-backed victory or lose banner from `Map/Effect.img/event/coconut` when the round ends, but it still does not reproduce the client's full packet-driven result flow beyond that local banner | Coconut is another field mode with its own client-owned action loop, not a variant of ordinary combat or map-object handling | `MinigameFields.cs`, `MapSimulator.cs`, `CoconutField` (`CField_Coconut::OnClock`, `CField_Coconut::OnCoconutHit`, `CField_Coconut::BasicActionAttack`, `CField_Coconut::DrawBoard`, `CField_Coconut::OnCoconutScore`) |
| Partial | Wedding ceremony fields | The simulator now resolves ceremony lines from `String/Npc.img/<npcId>/wedding{step}` with WZ-backed fallbacks, distinguishes groom or bride versus guest state from the local character id, mirrors the chapel-only step-2 guest bless prompt that `CField_Wedding::OnWeddingProgress` special-cases, records the client's modal response packet flow through step-advance (`163`) and guest-bless (`164`) ceremony responses, exposes a `/wedding` debug seam to drive progress, actor anchors, and responses through that same runtime, loads the real bless animation frames from `Effect/BasicEff.img/Wedding` while keeping that effect alive until the same client-owned clear path disables it instead of timing it out locally, swaps BGM to the WZ-backed `Sound/BgmEvent.img/wedding` track from the same step-0 ceremony seam the client uses, restores the map BGM when the wedding runtime clears, and draws the WZ-backed declaration overlay from `UI/UIWindow.img/wedding/text/0` as a ceremony scene layer during the opening step, but it still does not reproduce the rest of the client's wedding scene choreography beyond that opening overlay or the full remote actor rendering and remote user-pool presentation | Wedding maps now cover the client-owned opening audio swap plus the first ceremony presentation layer inside the existing runtime seam, so the remaining gap is narrower and centered on the rest of the scene choreography and remote actor presentation rather than missing BGM or a completely absent ceremony overlay | `SpecialEffectFields.cs`, `WeddingField`, `MapSimulator.cs` (`CField_Wedding::OnWeddingProgress`, `CField_Wedding::SetBlessEffect`) |
| Implemented | Witchtower score UI | The simulator anchors the Witchtower scoreboard to the client's center-top window origin, loads the exact `Map/Obj/etc.img/goldkey` background, key, and bitmap digits that match the `CScoreboard_Witchtower::OnCreate` asset shape, renders the zero-padded score at the verified `(67, 4)` draw origin with the client's negative digit spacing, and keeps the `witchscore` debug seam driving the same update path | This is a visible special-field HUD with client-backed art and placement now, so the remaining special-field work should move to other rows instead of revisiting a placeholder Witchtower overlay | `SpecialEffectFields.cs`, `WitchtowerField`, `MapSimulator.cs` (`CField_Witchtower::OnScoreUpdate`, `CScoreboard_Witchtower::OnCreate`, `CScoreboard_Witchtower::Draw`) |
| Partial | Massacre timerboard and gauge flow | The simulator now mirrors the client's type-2 timerboard ownership at the verified center-top origin, draws a dedicated MM:SS timer surface instead of a generic corner clock, advances the real three-stage `UI/UIWindow(.2).img/MonsterKilling/Count/keyBackgrd/{open,ing,close}` pulse whenever the gauge increases, renders the Massacre gauge from `MonsterKilling/Gauge` with the animated danger overlays and `Map/Effect.img/killing` bonus or clear/fail presentations, applies `mobMassacre` map defaults for `gauge.total`, `gauge.decrease`, `gauge.hitAdd`, and `disableSkill` instead of hardcoded HUD values, surfaces the map's `countEffect` thresholds as next-milestone plus reached-threshold HUD state, and exposes `/massacre bonus` plus `/massacre result ...` seams to replay the WZ-backed presentation layer, but the dedicated timerboard source canvas and font that `CTimerboard_Massacre::OnCreate` resolves are still approximated and the true packet-fed count-effect/result packet choreography is still not decoded, so score, rank, and clear/fail are currently simulator-driven rather than server-authored | Hunting-event maps depend on dedicated timer and gauge behavior that the client does not route through the normal status bar, and the simulator now follows map-owned Massacre gauge config, disable-skill variants, milestone thresholds, and most of the visible WZ-backed presentation shell instead of a one-size-fits-all placeholder HUD | `SpecialEffectFields.cs`, `MassacreField`, `MapSimulator.cs` (`CField_Massacre::Init`, `CField_Massacre::OnClock`, `CField_Massacre::UpdateKeyAnimation`, `CTimerboard_Massacre::Draw`) |
| Partial | Memory Game and MiniRoom card parity | The simulator now syncs the MiniRoom shell occupant roster and status text to the live Match Cards board, accepts mouse-driven sidebar and card hit testing on the `CMemoryGameDlg`-style surface, exposes delayed remote ready/start/flip/tie/give-up/end actions through the same runtime path that the local room uses, adds both a typed `MemoryGamePacketType` dispatch seam behind `/memorygame packet ...` and a raw MiniRoom payload seam behind `/memorygame packetraw ...`, decodes the client-backed MiniRoom gameplay subtypes (`6 -> 50/51/58/59/61/62/63/68`) plus base leave (`10`) into packet-owned ready, start, turn-up-card, time-over, game-result, tie, and leave handling, applies packet-owned board shuffle data from `CMemoryGameDlg::OnUserStart`, and upgrades the shared MiniRoom shell with a dedicated Match Cards chat log plus system or speaker message presentation instead of the old generic preview-only pane, but it still does not hook into live MiniRoom network traffic or decode the remaining base enter/avatar/chat packet families that `CMiniRoomBaseDlg` owns around Match Cards | MiniRoom parity now covers the room shell, board state machine, click flow, raw gameplay-packet decode, packet-owned start-board data, leave handling, and WZ-backed Match Cards presentation behind one runtime seam, so the remaining work is concentrated on live socket integration and the last shared base-packet families instead of missing core Match Cards ownership | `MinigameFields.cs`, `SocialRoomModels.cs`, `SocialRoomWindow.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator/MemoryGameFieldPacketTests.cs` (`CMemoryGameDlg::OnCreate`, `CMemoryGameDlg::Draw`, `CMemoryGameDlg::OnPacket`, `CMemoryGameDlg::OnUserStart`, `CMemoryGameDlg::OnTurnUpCard`, `CMemoryGameDlg::OnTimeOver`, `CMemoryGameDlg::OnGameResult`, `CMiniRoomBaseDlg::OnPacketBase`, `CMiniRoomBaseDlg::OnChat`) |
| Partial | Snowball minigame runtime | The simulator now treats Snowball state `2/3` as the client's packet-owned win states instead of local touch-zone states, keeps snowman HP packet-owned via `OnSnowBallState`, applies WZ-backed snowman stun windows and damage defaults through the runtime seam, smooths active snowball movement from the packet position plus speed data instead of instant teleports, routes the local player's world position through `SpecialFieldRuntimeCoordinator` so `SnowBallField` can surface the client's outbound touch-packet loop as pending team touch requests whenever the local player overlaps a snowball lane, aligns the Story-vs-Maple team labeling used by win banners and the local scoreboard with the client message owner, and now routes `OnSnowBallMsg` through the simulator chat log seam with client-shaped team ordering plus packet-owned fallback text instead of showing those packet messages as center-screen overlays, but it still does not reproduce the client's exact `CSnowBall::ms_anDelay` static cadence table or source the real localized `OnSnowBallMsg` StringPool text for IDs `0xD75`-`0xD77` | Snowball is one of the clearer cases where the simulator has a usable baseline but not the full client-owned minigame loop, and the remaining gap is now narrower and centered on the last unresolved timing table plus literal client localization data instead of the core round-state model or the chat-delivery path | `MinigameFields.cs`, `SnowBallField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator/SnowBallFieldTests.cs` (`CField_SnowBall::OnSnowBallState`, `CField_SnowBall::BasicActionAttack`, `CField_SnowBall::OnSnowBallHit`, `CField_SnowBall::OnSnowBallMsg`, `CField_SnowBall::OnSnowBallTouch`) |
| Partial | Ariant Arena field flow | The simulator now auto-binds `FIELDTYPE_ARIANTARENA` maps into a dedicated `AriantArenaField`, mirrors the client's sorted top-left ranking surface with WZ-backed `AriantMatch/characterIcon` row icons and the client's `(icon=5,y) / (name=21,y+2) / (score=106,y+2)` row offsets, keeps each player's Ariant icon assignment attached to that user across later score-driven re-sorts, plays the WZ-backed `AriantMatch/Result` animation from the client's center-top result origin, resolves the Ariant result UI sound from `Sound/MiniGame.img/Show` with a `Win` fallback instead of a broad heuristic, pushes local player name plus job into the runtime each frame so the client-owned 8xx or 9xx local-job rank suppression is honored, and now consumes the real Ariant packet types through a runtime packet seam for `354` score batches and `171` show-result packets that the `ariantarena raw <type> <hex>` debug path can feed directly, but it still does not receive live network packets from the simulator transport or redraw true remote name tags because the simulator still lacks the client's user-pool-backed remote actor layer | Arena scoring and ranking are owned by a dedicated field class in the client and are not covered by generic mob or UI behavior, so having a simulator-owned runtime now makes the remaining packet and player-state deltas explicit instead of leaving Ariant invisible | `MinigameFields.cs`, `AriantArenaField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_AriantArena::OnUserScore`, `CField_AriantArena::UpdateScoreAndRank`, `CField_AriantArena::OnShowResult`) |
| Partial | Battlefield event flow | The simulator now detects Battlefield maps through the special-field runtime, loads `battleField` WZ defaults such as `timeDefault`, `timeFinish`, reward maps, and effect paths, follows the client's packet-owned `OnClock(type=2, seconds)` and `OnScoreUpdate(wolves, sheep)` seams, exposes a local Battlefield team-change seam aligned with `OnTeamChanged(characterId, team)` and `SetUserTeam`, resolves a finish phase that derives win/lose effect plus reward-map routing from the WZ `battleField` node, parses the map's `user/<team>/look` presets so local Battlefield team changes reapply the client-backed cap, clothes, glove, shoe, cape, and pants loadout from WZ before restoring the original avatar on clear or map exit, now renders the client Battlefield board from `Map/Obj/etc.img/battleField/backgrnd` with the real sheep or wolf score fonts and the six-digit `fontTime` clock layout, queues the resolved reward map through the existing special-field transfer seam once the WZ `timeFinish` phase expires, and mirrors the local `SetUserTeam` minimap side effect by forcing the simulator minimap out of its collapsed state for local teams `0` and `2`, but it still does not swap remote user looks because the simulator has no client-style user pool actor layer and therefore still cannot reproduce the full remote-side `SetUserTeam` presentation path | Battlefield maps depend on a team-scoring field runtime rather than only ordinary combat rules, and the client keeps both the scoreboard window creation and team totals inside `CField_Battlefield` rather than generic field HUD code | `SpecialEffectFields.cs`, `BattlefieldField`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs`, `MinimapUI.cs` (`CField_Battlefield::OnClock`, `CField_Battlefield::OnScoreUpdate`, `CField_Battlefield::OnTeamChanged`, `CField_Battlefield::SetUserTeam`, `CScoreboard_Battlefield::OnCreate`) |
| Partial | Mu Lung Dojo field flow | The simulator now binds `FIELDTYPE_DOJANG` and the `MuruengRaid` map block into a dedicated `DojoField`, mirrors the client's type-2 clock ownership into the real `UI/UIWindow(.img)/muruengRaid` timerboard with bitmap digits and colon placement, draws the Dojo player, monster, and energy gauges from the client's WZ canvases at the IDA-verified layer anchors, swaps to the looping `energy/full` animation at max energy, replays the stage-start, clear, and time-over presentations from `Map/Effect.img/dojang` through `/dojostage` and `/dojoresult` debug seams plus the live timer-expiry path, now auto-triggers the clear presentation when the live boss HP feed drops to zero, and consumes the map's real `returnMap` or `forcedReturn` data so timer expiry waits for the WZ-backed time-over presentation to finish before queuing the Dojo exit transfer through the simulator's normal map-change seam, but it still does not consume real Dojo energy packets or reproduce the broader server-driven next-floor packet choreography beyond boss-death clear detection and the time-over return path | Dojo behavior is timer-driven and mode-specific, and the simulator now has a client-backed HUD, boss-death clear trigger, and map-owned time-over exit flow inside the dedicated runtime seam, leaving the remaining packet and next-stage transition parity work explicit instead of hidden behind placeholder rendering | `SpecialEffectFields.cs`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs` (`CField_Dojang::Init`, `CField_Dojang::OnClock`, `CField_Dojang::Update`, `CField_Dojang::UpdateTimer`) |
| Partial | Cookie House event flow | The simulator now binds `fieldType == CookieHouse` into a dedicated `CookieHouseField` runtime, mirrors the client-owned HUD geometry from `CField_CookieHouse::Init`, tracks score updates through a score-owned redraw path instead of generic UI, replaces the old solid-color placeholder with WZ-backed HUD panel pieces plus five grade badge frames loaded from `UI/UIWindow2.img/raise`, mirrors the client-owned redraw ownership more closely by caching the HUD into a dedicated 187x45 render target that only rebuilds when the point value changes before drawing that cached layer each frame, and now mirrors the client-owned update path by polling a simulator context point source each frame before redrawing instead of mutating the HUD as the source of truth, with that source preferring local character context (`CharacterBuild.CookieHousePoint`) and `cookiepoint` writing through the same seam for manual verification, but it still falls back to placeholder grade thresholds, does not yet decode the client's exact five-style `CBitmapNumber` digit asset root, and still lacks live packet wiring for the real server-owned Cookie House point feed | Cookie House is another distinct score-driven field, and this row now has a client-shaped cached HUD redraw path plus a simulator-owned character-context seam for future packet work instead of remaining a purely manual overlay | `CookieHouseField.cs`, `SpecialFieldRuntimeCoordinator.cs`, `MapSimulator.cs`, `CharacterData.cs` (`CField_CookieHouse::_UpdatePoint`, `CField_CookieHouse::Init`, `CField_CookieHouse::Update`) |
| Partial | Monster Carnival field flow | The simulator now detects Monster Carnival maps through the backlog coordinator, loads the map's WZ-backed Carnival definition into a dedicated runtime, enriches carnival mob entries with WZ-backed `getCP` and revive-chain metadata, enforces local CP spending plus mob or guardian generation caps when requests succeed, exposes packet-shaped enter, request-result, request-failure, game-result, revive-death, CP-update, CP-delta, and summoned-mob-count handlers behind the same runtime seam, and now applies revive-map death flow plus CP delta updates through dedicated simulator commands and tests, but it still does not yet mirror the client's exact localized request or result strings, consume real live Monster Carnival packets, or reproduce the full summon, guardian placement, and reactor-side gameplay rules | Monster Carnival is a self-contained field mode with client-owned rules and UI, so landing the dedicated runtime and HUD surface makes the parity gap concrete instead of leaving it hidden behind generic mob or field systems | `MonsterCarnivalField.cs`, `MinigameFields.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator/MonsterCarnivalFieldTests.cs` (`CField_MonsterCarnival::CreateUIWindow`, `CField_MonsterCarnival::OnEnter`, `CField_MonsterCarnival::OnRequestResult`, `CField_MonsterCarnival::OnShowGameResult`, `CField_MonsterCarnival::OnProcessForDeath`) |
| Partial | Party Raid field flow | The simulator now binds `FIELDTYPE_PARTYRAID`, `FIELDTYPE_PARTYRAID_BOSS`, and `FIELDTYPE_PARTYRAID_RESULT` into a dedicated `PartyRaidField` runtime, renders the field, boss, and result HUDs from the client WZ asset roots (`UI/UIWindow.img/PartyRace/*`, `UI/UIWindow.img/DualMobGauge/*`, `Map/Effect.img/praid/*`), fixes the field and boss point-vs-stage board placement to match the `PartyRace/Stage/backgrd` labels, mirrors the client's team-colored stage-state art, boss red-vs-blue gauge fill, map-owned clear or timeout result effect, and result summary overlay positions, now infers win-vs-fail result mode from the result maps' `info/onUserEnter` scripts (`PRaid_WinEnter`, `PRaid_FailEnter`) and constrains the result panel to the map's `LBSide/LBTop/LBBottom` safe area, carries a type-2 timer lifecycle through the runtime, broadens the existing raw `field|party|session` key seam with numeric team aliases plus session outcome text handling, and adds focused Party Raid runtime tests for script-owned outcome binding and raw-key decode, but it still does not consume live Party Raid packets from the simulator network path, decode the exact server-owned Party Raid key names beyond the aliases recovered so far, or confirm any remaining client-only battery or per-map boss layout details that are not explicit in WZ | Party Raid now has a WZ-backed runtime surface with corrected field-board placement, script-backed result outcome selection, map-safe result placement, and a stronger raw-key test seam, so the remaining gap is concentrated on live packet ownership, the unrecovered literal key names, and the last client-only HUD details rather than broad mode presentation holes | `SpecialFieldRuntimeCoordinator.cs`, `PartyRaidField.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator/PartyRaidFieldTests.cs` (`CField_PartyRaid::_UpdatePoint`, `CField_PartyRaid::Init`, `CField_PartyRaidBoss::Init`, `CField_PartyRaidBoss::OnFieldSetVariable`, `CField_PartyRaidBoss::OnPartyValue`, `CField_PartyRaidResult::Init`, `CField_PartyRaidResult::OnSessionValue`) |
| Partial | SpaceGAGA timerboard flow | The simulator now binds `FIELDTYPE_SPACEGAGA` maps into a dedicated `SpaceGagaField`, mirrors the client's clock-type-2 timerboard ownership, keeps the board at the verified center-top window origin, removes the invented title banner, and now tries to render a WZ-backed board plus bitmap timer digits by scanning the loaded `UIWindow(.2).img` trees for a `258x69` timerboard canvas and `0-9` plus colon digit container before falling back to the older procedural board or SpriteFont path, but it still does not yet pin the literal `CTimerboard_SpaceGAGA::OnCreate` string-pool asset path and therefore still relies on shape-based WZ discovery instead of a confirmed fixed source node | This is another example of client-owned special field logic that would otherwise be lost under generic map handling, and the simulator now has a backlog-owned runtime seam with WZ-backed timerboard asset discovery for the remaining exact-path confirmation work | `SpecialEffectFields.cs`, `SpaceGagaField` (`CField_SpaceGAGA::OnClock`, `CTimerboard_SpaceGAGA::Draw`, `CTimerboard_SpaceGAGA::OnCreate`) |

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
