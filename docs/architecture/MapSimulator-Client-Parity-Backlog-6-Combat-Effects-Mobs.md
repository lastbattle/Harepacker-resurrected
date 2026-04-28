
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

- `HaCreator/MapSimulator/Physics/CVecCtrl.cs`

- `HaCreator/MapSimulator/Character/CharacterLoader.cs`

- `HaCreator/MapSimulator/Character/CharacterAssembler.cs`

- `HaCreator/MapSimulator/Character/PlayerCharacter.cs`

- `HaCreator/MapSimulator/Character/Skills/SkillManager.cs`

- `HaCreator/MapSimulator/Effects/CombatEffects.cs`

- `HaCreator/MapSimulator/Pools/PortalPool.cs`

- `HaCreator/MapSimulator/UI/StatusBarUI.cs`

- `HaCreator/MapSimulator/UI/Windows/SkillUI.cs`

- `HaCreator/MapSimulator/UI/Windows/QuickSlotUI.cs`



### Client references checked



- `CUserLocal::DoActiveSkill_MeleeAttack` at `0x93b210`

- `CUserLocal::DoActiveSkill_ShootAttack` at `0x93b520`

- `CUserLocal::DoActiveSkill_MagicAttack` at `0x93a010`

- `CUserLocal::DoActiveSkill_Prepare` at `0x941710`

- `CUserLocal::TryDoingFinalAttack` at `0x93aaa0`

- `CUserLocal::TryDoingSerialAttack` at `0x93ac90`

- `CUserLocal::TryDoingSparkAttack` at `0x93abe0`

- `CUserLocal::Update` at `0x937330`

- `CUserLocal::SetMoveAction` at `0x903ce0`

- `CUserLocal::TryDoingPreparedSkill` at `0x944270`

- `CUserLocal::TryDoingRepeatSkill` at `0x93d400`

- `CUserLocal::DrawKeyDownBar` at `0x9153b0`

- `CUserLocal::CheckPortal_Collision` at `0x919a10`

- `CUserLocal::CheckReactor_Collision` at `0x903d20`

- `CUserLocal::TryDoingTeleport` at `0x932c00`

- `CUserLocal::TryDoingRush` at `0x90b8c0`

- `CUserLocal::TryDoingFlyingRush` at `0x90bc10`

- `CUserLocal::TryDoingRocketBooster` at `0x940e50`

- `CUserLocal::TryDoingRocketBoosterEnd` at `0x93d2d0`

- `CUserLocal::TryDoingSmoothingMovingShootAttack` at `0x92de70`

- `CUserLocal::TryDoingSwallowBuff` at `0x944520`

- `CUserLocal::TryDoingSwallowMobWriggle` at `0x93f500`

- `CUserLocal::TryDoingMine` at `0x907d70`

- `CUserLocal::TryDoingCyclone` at `0x932d60`

- `CUserLocal::TryDoingSitdownHealing` at `0x903d60`

- `CUserLocal::UpdateClientTimer` at `0x90e6d0`

- `CUserLocal::TryPassiveTransferField` at `0x91a2e0`

- `CUserLocal::TalkToNpc` at `0x9321f0`

- `CUserLocal::TryAutoRequestFollowCharacter` at `0x904bf0`

- `CUserLocal::TryLeaveDirectionMode` at `0x9054c0`

- `CUser::LoadLayer` at `0x8e96d0`

- `CUser::PrepareActionLayer` at `0x8e3070`

- `CUser::SetMoveAction` at `0x8df540`

- `CUser::Update` at `0x8fb8d0`

- `CUser::UpdateMoreWildEffect` at `0x8fb4c0`

- `CUser::PetAutoSpeaking` at `0x8deb10`

- `CVecCtrl::CollisionDetectFloat` at `0x994740`

- `CMovePath::IsTimeForFlush` at `0x666870`

- `CMovePath::MakeMovePath` at `0x667c90`

- `CMovePath::Flush` at `0x668160`

- `CMovePath::Encode` at `0x666e20`

- `CMovePath::Decode` at `0x667920`

- `CMovePath::OnMovePacket` at `0x6683f0`

- `CPortalList::FindPortal_Hidden` at `0x6ab5d0`

- `CMob::DoAttack` at `0x6504d0`

- `CMob::OnNextAttack` at `0x6528a0`

- `CMob::ProcessAttack` at `0x652950`

- `CMob::GetAttackInfo` at `0x641330`

- `CActionMan::GetMobImgEntry` at `0x419f20`

- `CActionMan::LoadMobAction` at `0x41f530`
- `CAnimationDisplayer::Effect_Tremble` at `0x439a70`
- `CAnimationDisplayer::Effect_HP` at `0x444eb0`
- `CField::IsFearEffectOn` at `0x52a420`

- `CField::OffFearEffect` at `0x52b810`

- `CUISkill::Draw` at `0x84ed90`

- `CUISkill::GetSkillRootVisible` at `0x84a6f0`

- `CUISkill::GetRecommendSKill` at `0x84e710`

- `CUIStatusBar::Draw` at `0x876cd0`

- `CUIStatusBar::SetStatusValue` at `0x873590`

- `CUIStatusBar::SetNumberValue` at `0x873d50`

- `CUIStatusBar::ChatLogAdd` at `0x87aec0`

- `CUIStatusBar::CQuickSlot::Draw` at `0x875750`

- `CUIStatusBar::CQuickSlot::CompareValidateFuncKeyMappedInfo` at `0x871000`



## Client Function Index By Backlog Area



The list below maps each backlog area to concrete client seams confirmed in IDA.

These are the first functions to inspect before changing simulator behavior.



### 1. Mob combat and encounter parity



- `CMob::DoAttack` at `0x6504d0`

- `CMob::OnNextAttack` at `0x6528a0`

- `CMob::ProcessAttack` at `0x652950`

- `CMob::GetAttackInfo` at `0x641330`



### 2. Mob image-entry ownership discovered by targeted `CActionMan` scan

- `CActionMan::GetMobImgEntry` at `0x419f20`

Notes:
The follow-up `CActionMan` scan shows the client resolves mob template IMG data through a distinct cached owner before `LoadMobAction` runs. `GetMobImgEntry` formats the mob resource path through the StringPool-backed template, loads the property tree from the resource manager, follows `info/link` by recursively reopening the linked template, and copies missing top-level branches from the linked image onto the cached entry before later action loading consumes it. That means mob parity is split three ways rather than two: runtime behavior in `CMob::*`, image-entry normalization in `GetMobImgEntry`, and then frame-table construction in `LoadMobAction`.

### 3. Mob action-loader ownership discovered by targeted `CActionMan` scan



- `CActionMan::LoadMobAction` at `0x41f530`



Notes:

The combat backlog already tracked `CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, and `CMob::GetAttackInfo`, but the targeted `CActionMan` scan shows the client also keeps a distinct mob action-loader owner that was not named anywhere in this backlog set. `CActionMan::LoadMobAction` caches by `(templateId, action)` before the runtime ever updates or attacks, resolves the action node from the mob IMG entry through the static `s_sMobAction` table, enumerates each frame canvas, reads per-frame `delay`, `head`, `lt`, `rb`, and multi-body metadata, computes the frame rect and head anchor, and appends a reversed playback pass when the action publishes the client reverse flag. That means mob parity is still split the same way as summons, pets, employees, and NPCs in the other backlog docs: `CMob::*` owns behavior and targeting, while `CActionMan::LoadMobAction` owns the frame-table construction, delay fallback, cached reuse, and reverse-playback shape that the runtime consumes.

### 4. Combat feedback animation-owner parity discovered by `CAnimationDisplayer` scan

- `CAnimationDisplayer::Effect_Tremble` at `0x439a70`
- `CAnimationDisplayer::Effect_HP` at `0x444eb0`

Notes:
The broader combat rows already talked about damage numbers, hit feedback, and tremble, but the function scan shows the client owns that presentation through a dedicated animation manager rather than only through mob runtime code or UI widgets. `CAnimationDisplayer::Effect_HP` selects the red/blue/violet number families plus the separate critical branch, formats the number text through StringPool id `0x1A15`, composes the digit canvases into a temporary layer, inserts the extra critical banner, and then registers the whole result as a one-time animation. `CAnimationDisplayer::Effect_Tremble` likewise owns the screen-shake timing state directly by recording start/end ticks, the heavy-vs-normal reduction curve, and the config/enforcement gate before the effect ever reaches draw/update. That means combat feedback parity is also split three ways in this backlog set: `CMob::*` owns combat behavior, `CActionMan::LoadMobAction` owns mob frame construction, and `CAnimationDisplayer` owns the visible damage-number and tremble presentation the player actually sees.

### 5. Mob anger-gauge burst animation-owner discovered by follow-up IDA scan

- `CMob::AngerGaugeFullChargeEffect` at `0x6490b0`
- `CAnimationDisplayer::Effect_FullChargedAngerGauge` at `0x457d00`

Notes:
The broader special-mob row already tracks WZ-authored `ChargeCount`, `AngerGauge`, and `attack/info/AngerAttack`, but the follow-up IDA pass shows the visible full-charge burst also has a distinct client owner that is not named anywhere else in the backlog set. `CMob::AngerGaugeFullChargeEffect` rate-limits the burst by `m_bFullChargeEffectStartTime + m_bFullChargeEffectTime`, formats the UOL from the mob template id through StringPool ids `0x3CE` and `0xC2F`, and then routes that authored effect path through `CAnimationDisplayer::Effect_FullChargedAngerGauge` with the mob head vector as origin and the action layer as overlay parent. `Effect_FullChargedAngerGauge` is itself not a generic helper: it loads the authored layer, forces `GA_STOP` animation playback, and registers the result through `RegisterOneTimeAnimation` rather than leaving anger-gauge presentation on the mob runtime or the existing generic combat-feedback rows. That means anger-gauge parity is split one level further than the current doc suggests: WZ/runtime charge logic stays in the special-mob row, while the visible full-charge burst belongs to its own mob-to-animation-displayer owner seam.


## Current State Summary



MapSimulator is no longer "missing player simulation".

The project already has:



- A playable local character with foothold, ladder, rope, swim, fly, jump, prone, hit, death, and respawn states.

- Character WZ loading for body, head, face, hair, equipment, weapon overlays, z-map ordering, and slot-visibility conflicts.

- A working generic skill system with job skill loading, buffs, cooldowns, projectiles, hit effects, and quick-slot assignment.

- Mob and drop pools, hidden portal support, damage number rendering from WZ assets, mob HP bars, and boss HP bars.

- Several field-effect systems and special field implementations that are already present in the current simulator.



The real parity gap is no longer "does the simulator have a feature at all?".

The gap is now "how closely does the current implementation match client behavior, data selection, timing, and fallback rules?".



## Recent Progress Summary



Recent implementation progress has been folded into the backlog tables below instead of being tracked as a separate changelog.

The main baseline updates worth keeping visible are:



- Avatar parity moved out of the stub phase: action coverage, rare-action rendering, facial expression behavior, and anchor fallback rules are implemented, while mount and transform handling is partially in place.

- Skill parity is broader than the old notes implied: the simulator now loads and casts the full player skill catalog, supports runtime job swaps, shows buff and cooldown feedback, and covers more movement-family and summon behavior.

- Physics and movement now have real runtime seams for ladder lookup, float collision, movement-path snapshots, and moving-platform foothold sync.

- Combat and UI both shifted from "missing feature" to "refinement": HP indicators, damage rendering, status-bar layout, warning flashes, chat strip behavior, skill UI layout, and tooltip reuse all exist but still need client-accurate polish.

- NPC and quest interaction now have a usable simulator baseline through dialogue overlays, quest lists, and common quest-state mutations, but full scripting and inventory-backed requirements remain incomplete.



## Remaining Parity Backlog



### 1. Mob combat and encounter parity
The simulator has working mob AI and effects, but not the full client combat pipeline.

| Status     | Area                        | Gap | Why it matters | Primary seam |
|------------|-----------------------------|-----|----------------|--------------|
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present, direct melee attacks now use the same delayed action scheduler as projectile and ground-warning families using WZ `attackAfter` timing instead of pure frame polling, mob skills now route their non-summon `MobSkill.img` `effect` and `mob` branches through the runtime animation layer so mob/target/screen-position skill cues no longer silently no-op, target-position mob skill visuals now anchor to the mob's actual live target instead of always snapping to the player when the mob is casting onto a summon or another mob, summon skill `200` still resolves its live summon list and limit from `MobSkill.img` so temporary summons enter the field through the mob pool instead of being skipped, common mob self or party buff skills now read `MobSkill.img` `x`/`time`/`lt`/`rb` data to apply runtime `PowerUp`, `MagicUp`, `PGuardUp`, `MGuardUp`, and `Speed` temp stats to the caster or nearby mobs instead of remaining visual-only, `MobSkill.img` skill `114` now uses the same WZ runtime area and `x` value to heal nearby mobs instead of staying cosmetic, common player-targeted mob debuffs now also read `MobSkill.img` `time` / `interval` / `prop` / `x` / `lt` / `rb` data so seal, darkness, weakness, stun or freeze, poison, slow, and dispel skills apply runtime local-player consequences instead of stopping at visuals, with darkness feeding player miss chance, weakness suppressing jumps, stun or freeze locking movement and skill use, poison ticking direct damage, slow scaling live move speed, and dispel clearing active buffs, while the rarer player-facing debuff family now also routes through the same runtime path instead of staying blocked by simulator heuristics: authored `MobSkill.img` `lt`/`rb` ranges now feed autonomous skill selection so wide-area curse, seduce, reverse-input, undead, and banish skills can fire from their published reach instead of a fixed short-range fallback, those same authored rectangles now mirror the mob's live facing when the runtime applies nearby or player-targeted mob skills, banish skill `129` now applies its runtime banish lockout after teleporting to spawn instead of short-circuiting at teleport only, undead skill `133` now reflects recovered HP using the authored `MobSkill.img/x` percentage instead of always inverting heals at a hardcoded full amount, potion-style and quest-buff consumable recovery now route through `PlayerCharacter.Recover` so the same runtime curse caps, undead reflected-heal percentage, and local-player recovery modifiers apply there too instead of only on skill- or chair-driven recovery, and mob skill `135` now blocks the simulator's supported consumable lanes through the same runtime `StopPotion` path instead of only blocking raw recovery, including HP or MP recovery, cure-only, temporary buff, morph, and map-move consumables. In this run, the higher-end status lane was tightened further using the published `MobSkill.img` ordering and `x` data: skill `136` now lands as runtime `StopMotion` instead of being folded into fear, now also mirrors into the shared player skill-blocking lane through the same runtime duration and clears that lockout immediately when the runtime status is cured or expires, so direct casts and contact-disease applications no longer stop at movement lock without the matching client-facing skill rejection path; skill `124` curse now also scales local mob-kill EXP reward through the same runtime curse percentage already carried by the player-status resolver instead of staying limited to the HP/MP recovery-cap side effect; and skill `137` now lands as `Fear` instead of borrowing seduce-style forced horizontal control, with authored `x` continuing to drive the existing field fear overlay intensity and repeated fear applications refreshing the live overlay whenever that authored intensity changes even if the new cast does not extend the previous fear duration. Inherited `MobSkill.img` level data for `lt` / `rb` / `time` / `interval` / `prop` / `count` / `targetMobType` / `x` / `y` / `hp` now also feeds the same runtime resolver instead of dropping back to partial defaults when later levels omit repeated fields, sparse higher-level `MobSkill.img` branches now also inherit prior `affected` / `effect` / `mob` / `tile` / `time` data through the shared resolver so direct-cast hit feedback, mob-side cues, and field-owned visuals keep rendering when later levels only carry an `info` stub, summon skill `200` now inherits authored `limit`, `lt` / `rb`, and summon-entry lists from the nearest previous level instead of treating sparse follow-up levels as empty, and mob skill source slots now preserve the authored WZ `info/skill/*` index so `preSkillIndex` chains key off the actual published slot instead of the loader's incidental enumeration order. The direct-attack disease lane now sources its extra player skill-blocking timer from that same runtime duration and only keeps that lockout when the shared runtime status lane actually lands instead of still blocking on resisted or failed casts, direct player-targeted mob skill casts now also play their authored `MobSkill.img/affected` hit feedback when the runtime status applies instead of reserving that animation lane to contact diseases only, and clearing or expiring those same runtime statuses now also clears the mirrored player skill-blocking lane immediately instead of leaving cured seal, stun, freeze, seduce, polymorph, or `StopMotion` casts blocked until a stale timer elapses. Regular attacks that carry `info/attack/*/disease` now also resolve the same runtime `MobSkill.img` status path and affected-hit animation feedback on contact instead of only applying the old blocking-only callback for seal, stun, freeze, seduce, or polymorph. Seduce-side control aftermath is now tighter too: the mob-status frame state explicitly marks pickup as blocked during forced-move control, the player manager now honors that state in the separate drop-pickup path, and entering seduce now forces the local player out of ladder or rope attachment before horizontal control takes over instead of leaving the character stuck in place on climb geometry or a seated hold state. The same mob-status control gate now also applies to world-interact (`Up`) handling, so seduce or `StopMotion` locked states no longer bypass into portal-interact attempts through the raw key path. Entering `StopMotion` now also routes through the same forced-control preparation seam so newly applied movement lock releases ladder/rope attachment and seated/prone holds immediately instead of freezing those states in place. Chained post-action combat decisions no longer incorrectly stall when the mob is currently targeting a summon or another mob instead of the player, and autonomous mob skill selection now honors WZ `priority`, `preSkillIndex` / `preSkillCount`, `onlyFsm`, and `skillForbid` so scripted-only skills stop leaking into the generic loop, charged follow-up skills wait for their prerequisite casts, and skill lockout windows stop immediately chaining into unrelated skills. This pass also makes the generic selector consult the authored runtime before it commits to a cast: self or party buff, heal, and cure skills now skip generic auto-selection when every eligible target already has the authored buff state, already sits above the authored heal `hp` threshold, or has no negative status to clear, summon skill `200` now also skips generic auto-selection while its authored `limit` is already full instead of wasting a cast on a guaranteed no-op, and player-facing disease skills now require the authored `lt` / `rb` area to reach the live player hitbox before the generic loop burns that action. The runtime status application lane now also reports success only when a status or dispel actually lands (`MobSkill.img/127` no longer reports success when no active buffs exist, and durationless status casts no longer report applied), so direct-cast and contact-disease affected-hit feedback plus mirrored skill-blocking no longer fire on no-op mob-skill casts. | The loop is closer to client `CMob` behavior, and the remaining gap is now concentrated on deeper control fallout rather than WZ-backed skill admission: broader client matching for reverse-input edge cases beyond the current directional swap, any curse-side rules beyond the now-shared recovery cap plus EXP reduction seam, the exact client consequences of `StopMotion` beyond the current movement lock, mirrored skill-lock lane, world-interact lockout path, and newly mirrored ladder/rope + seated/prone break-out path, and any still-unmodeled uncommon player-facing mob-skill transitions or branch-specific control rules after the new seduce forced-move cleanup, pickup lockout path, resisted-cast blocking fix, no-op cast application gating for status feedback/blocking, sparse-level `MobSkill.img` inheritance pass, authored source-slot preservation for prerequisite skill chains, the widened `StopPotion` consumable gate, the fear-overlay refresh path for skill `137` intensity changes, the local curse-EXP follow-through, while player-targeted no-op recasts are now gated by live status remaining time plus cast-lead timing from the selected mob skill (`max(1000, max(skillAfter, effectAfter))`), so unchanged seal/darkness/weakness/stun/freeze/reverse/undead/fear/burn/stop lanes, unchanged polymorph ids, and dispel casts with no active buffs stay suppressed immediately after a successful application but can refresh again near expiry; WZ-backed `MobSkill.img/799` now also lands as a timed local-player `BattlefieldFlag` status using authored `time` and `x` flag/team values so direct casts and contact disease routing report success and play the published maple-flag `affected` animation instead of silently no-oping. The remaining selector gap is now narrowed to client-only edge behavior around the exact native recast lead policy plus field-script-owned consequences for rare event-only statuses beyond the local timed status/feedback lane. Focused simulator seam coverage now also exists in `UnitTest_MapSimulator/PlayerMobStatusParityTests.cs` for curse EXP scaling and `StopMotion` mirrored skill-blocking cleanup, `PlayerMobInteractInputParityTests.cs` for world-interact lockout while movement lock or seduce forced-control is active and for control-lockout transition admission that triggers forced-control cleanup when `StopMotion` movement lock starts, alongside `PlayerMobConsumableBlockEvaluatorTests` for the widened `StopPotion` consumable gate, `MobFearFieldEffectParityTests` for the fear overlay refresh gate when re-applied casts change WZ-authored intensity without extending duration, `PlayerMobStatusAutoSelectParityTests.cs` for the cast-lead refresh gate that keeps unchanged player-status recasts suppressed until near-expiry windows, and `MobSkill799BattlefieldFlagParityTests.cs` for the WZ-authored Battlefield flag status lane. | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `MobSkillEffectLoader.cs`, `MobSkillLevelResolver.cs`, `MobSkillData.cs`, `MobItem.cs`, `CombatEffects.cs`, `MobSkillSelectionParity.cs`, `PlayerManager.cs`, `PlayerMobStatusController.cs`, `PlayerCharacter.cs`, `UnitTest_MapSimulator` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, `tremble`, and `type` attack metadata through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, triggers tremble feedback from flagged impacts, preserves puppet/summon aggro targets through AI updates, routes delayed mob hits to registered puppet targets instead of always falling back to the local player, now syncs targeted summons through their live runtime animation bounds instead of a fixed fallback box so summon-targeted mob attacks collide against the same world-space body rect the simulator draws, removes consumed summons from the active summon list when that branch lands, preserves summon slot indices alongside live summon object ids so queued summon-targeted attacks can still resolve through the client-style slot lane if the original summon instance is replaced before impact, preserves external mob targets through AI updates so delayed mob attacks can resolve `MobTargetType.Mob` entries against live target mob body rects and apply mob-vs-mob damage instead of silently dropping that client branch, now mirrors the client `CMob::ProcessAttack` rect pass for delayed direct or area hits by colliding non-targeted attack rectangles against every live summon and mob body in range instead of only the explicitly tracked target, lets explicitly targeted summon or mob projectiles apply their delayed impact when they reach the scheduled destination instead of depending only on the projectile transit hitbox, now also gives untargeted non-ground projectile impacts the same late rect sweep against the local player, live summons, and encounter mobs when the ball reaches its destination instead of dropping those arrival-time collateral hits, routes WZ `type = 1` delayed direct or ground attacks through the client-style locked-target branch so queued player, summon, or mob targets resolve on impact without depending on a late rectangle overlap test, matches the client `CMob::DoAttack` admission gate more closely by checking type-1 queue eligibility from the authored attack origin to a clamped target-body point within `range + 10` instead of the older broad rectangle overlap heuristic, now resolves those player-targeted admission checks, multiball lane picks, and locked delayed impacts through the live player body rect seam instead of a coarse position-only fallback, now also narrows the WZ `jumpAttack` grounded gate to the same untargeted direct-rect player or summon overlap branch the client uses instead of incorrectly suppressing airborne hits from locked-target, projectile, or area impact paths, now parses WZ projectile `range/sp` + `range/r` launch-origin metadata, uses that authored origin for projectile spawn and type-1 admission checks, carries WZ `info/attack/*/attackCount` into live ranged attack entries, fans multi-ball projectiles across authored projectile lanes by choosing per-lane destinations against live player, summon, or encounter-mob bodies when those entities line up with the lane instead of cloning every extra ball off a fixed synthetic spread from the primary target, now synthesizes the extra multiball lane list from the full authored `range/start/areaCount` slot set instead of only the first `attackCount` offsets so wide projectile patterns stay centered and preserve authored lane order more closely, now keeps those authored lane points separate from the locked target body point so multiball projectiles continue to fly along the selected slot lane even when multiple balls resolve against the same live player, summon, or encounter-mob body, now also allows those per-lane heuristic resolutions to repeat the same live player, summon, or encounter-mob body across multiple bullets when separate authored lanes still line up with that body, reuses WZ-authored projectile and hit frame bounds for in-flight and arrival-time collision checks instead of a fixed synthetic `16x16` box, now mirrors the client `CMob::SetBallDestPoint` destination clamp for projectile travel by enforcing a forward lane, clamping the vertical slope to `0.6`, and normalizing against authored `range/r`, now reads WZ `attack/info/randDelayAttack` into delayed area attacks, now synthesizes the simulator-side random area delay list once per selected area slot so every foothold expanded from the same client-style `type = 3` column shares one queued delay instead of rolling a fresh delay per foothold hitbox, and now also keeps WZ `range/start` as the lane-origin offset while still selecting `attackCount` area slots from the full authored `range/areaCount` slot set (including non-zero or negative starts) so type-3 ground-column attacks no longer collapse to the first contiguous authored slots, and now also exposes a client-shaped override seam for `m_aMultiTargetForBall` and `m_aRandTimeforAreaAttack` so the attack scheduler will prefer packet-fed extra projectile lane points and per-slot area delays whenever those lists are supplied. In the packet path, the existing `MoveMob` packet codec and mob-attack inbox now preserve packet `TARGETINFO` too, so the next queued attack prefers packet-owned locked player, summon-slot, or mob victims through the same live body-rect seams the simulator uses for delayed type-1 admission and impact resolution instead of dropping that client-owned target branch and falling back to AI heuristics, packet-owned move-header attack admission now clears queued overrides when `NextAttackPossible` is false while carrying packet-facing into projectile lane fallback, launch-origin orientation, `SetBallDestPoint` forward-lane clamping, and locked-target admission-distance checks instead of always falling back to the live AI-facing bit, the same packet path now also honors client `bNotChangeAction` by refusing to queue simulator attack overrides for move headers that suppressed `CMob::DoAttack` on the client instead of leaking stale packet-owned locked targets or projectile lane lists into a later AI-selected attack, and the packet-owned move discard bit now feeds a live `MobMovementInfo` interrupt seam so attack move headers mirror the client `CMovePath::DiscardByInterrupt` split by clearing pending steering first and only forcing an airborne ground mob back onto a foothold when `bNotForceLandingWhenDiscard` is false; that same seam now also records a client-shaped last-motion snapshot (`x`, `y`, `vx`, `vy`, move/jump/action state, facing) at interrupt time and only zeroes horizontal speed when a foothold snap actually lands instead of wiping in-air lateral motion on discard requests that cannot resolve to a foothold. This run also extends that same discard seam with client-shaped buffered-path ownership: packet interrupts now truncate buffered move-path elements to a refreshed live-tail snapshot, rebase that tail to the packet receive tick, and preserve packet-owned move-action state (`stand`/`move`/`jump`/attack) through live movement state updates instead of treating move-action ownership as transient metadata only. This run closes another `MobBullet` timing branch straight from WZ plus client evidence: the simulator already loaded authored `range/r` and `attackAfter`, but ranged attacks now keep queued mob bullets dormant until `attackAfter` and time projectile expiry from the same client formula `range/r * 600 / nBulletSpeed` that `CMob::DoAttack` uses when it constructs `MobBullet`, instead of launching immediately and deriving impact time only from raw world-distance-over-speed, and in-flight projectile position now advances from the stored authored launch point on every tick so delayed transit and arrival checks no longer compound by re-lerping from the prior frame. | `CMob::DoAttack` / `ProcessAttack` are closer on `MobBullet` setup and delayed area placement now that the simulator uses authored launch points, live player-body admission and impact resolution, full-slot multiball lane selection, authored-order extra bullet synthesis, authored lane-point preservation separate from delayed locked-target victim resolution, client-style direct-only `jumpAttack` grounding, WZ-sized projectile-impact bounds, client-style `SetBallDestPoint` destination normalization, client-style `attackAfter` projectile arming, authored `range/r * 600 / nBulletSpeed` bullet lifetime, fixed launch-point in-flight interpolation, authored `randDelayAttack`, per-slot random-delay reuse across type-3 multi-foothold expansions, client-style type-3 multi-foothold column expansion, type-4 source-Y placement, heuristic duplicate-target multiball resolution when multiple authored lanes legitimately overlap the same body, and packet-owned `TARGETINFO`, `m_aMultiTargetForBall`, `m_aRandTimeforAreaAttack`, `NextAttackPossible`, `bNotChangeAction`, and `bNotForceLandingWhenDiscard` ingestion through the live `MoveMob` inbox path instead of only through manual override seams, and that same packet path now also decodes buffered `CMovePath` elements from the move payload and replays them through `MobMovementInfo` with receive-time rebasing plus per-element move-action ownership so packet-owned position/action transitions no longer collapse to the interrupt snapshot only, while also preserving optional client flush-tail payload ownership (`passive key pad` count plus packet movement bounds) whenever those tail fields are present. The remaining gap is narrower but still centered on deeper client-owned `MobBullet` and move-path behavior: the runtime still does not mirror richer non-linear projectile travel shapes or uncommon post-arrival side effects beyond the now-matched launch window, fixed-point interpolation, destination clamp, and authored lifetime formula, and while the packet-owned move discard seam now covers steering clear, interrupt snapshot capture, buffered-element truncation, receive-time rebasing, and downstream move-action ownership in `MobMovementInfo`, the remaining move-path gap is now limited to deeper client stream nuances: uncommon `CMovePath` attribute payload variants beyond the now-modeled common element stream, plus any still-unmodeled per-element action transitions and receive-time progression replay edge behavior. Focused simulator seam coverage now exists in `UnitTest_MapSimulator/MobProjectileTimingParityTests.cs` for the authored `range/r` bullet lifetime formula and launch-point linear travel interpolation, plus `UnitTest_MapSimulator/MobMoveInterruptParityTests.cs` for packet-owned discard truncation, receive-time rebasing, move-action ownership mapping, and buffered move-path replay progression through `MobMovementInfo`, alongside `UnitTest_MapSimulator/MobMoveAttackPacketCodecTests.cs` for `MoveMob` move-tail decode coverage (with and without client optional random-count suffixes and with optional client flush-tail passive-state/bounds payloads), and `UnitTest_MapSimulator/MobAttackGroundSlotParityTests.cs` for non-zero WZ `range/start` area-slot selection across the full authored `range/areaCount` slot set. | `MobAI.cs`, `MobAttackSystem.cs`, `MobPool.cs`, `MapSimulator.cs`, `MobMoveAttackPacketCodec.cs`, `MobMovementInfo.cs`, `MobItem.cs`, `UnitTest_MapSimulator/MobProjectileTimingParityTests.cs`, `UnitTest_MapSimulator/MobMoveInterruptParityTests.cs`, `UnitTest_MapSimulator/MobMoveAttackPacketCodecTests.cs` (`CMob::DoAttack`, `CMob::OnMove`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`, `CMovePath::DiscardByInterrupt`, `attack/info/range/r`) |
| Partial | Full mob stat/status parity | Existing mob-temp-stat parity work remains in place (runtime status entries with expiry/tick metadata, signed stat shifts, DoT ticks, freeze/stun lock, immunity/counter lanes, threat/showdown/mind-control/polymorph inference, mob-skill status clear/self-buff lanes, and doom render/collision swap to `mob/0100101.img`). In this run, the doom movement seam now reserves transformed speed to the WZ-backed doom baseline from `mob/0100101.img/info/speed = -50` instead of a hardcoded synthetic slow, the hypnosis target resolver now excludes doomed candidates and explicitly prioritizes same-team encounter targets ahead of same-team non-encounter fallback candidates, and it now also preserves an already-resolved hypnosis victim within the same priority tier instead of thrashing to a merely closer alternative every sync tick, which better matches the client-shaped locked-target feel until a higher-priority candidate appears. This run also tightens that same seam with bounded distance retention: an already-resolved hypnosis victim remains eligible through a 1.5x range window so same-tier locks do not churn immediately on slight spacing seams, while higher-priority candidates still preempt immediately. `MobSkill.img/157` (`info` rich token, `x=1`, `time=180`) now also contributes a concrete reward-side consequence by scaling meso payout through the existing mob reward lane (`x` maps to +`x*100%` meso bonus while active) instead of remaining visual/status-only, inferred `Showdown` reward bonus parity now also reaches the simulator's item reward seam instead of stopping at EXP because the official skill text in `string/Skill.img` (`4121003` / `4221003`) says Showdown increases both EXP and items, authored fear now also reaches the client-shaped field-visual seam instead of staying control-only through the same `CField::InitFearEffect` / `OnFearEffect` / `OffFearEffect`-style seam with live `MobSkill.img/137/x` intensity, and the item reward seam itself now goes beyond monster cards by reading authored `String/MonsterBook.img/<mobId>/reward` entries: mob deaths can spawn one lightweight WZ-backed reward-list item, with that authored reward choice now selected deterministically from the published WZ reward order per runtime mob identity seed (`PoolId`, with template-id fallback) instead of rerolling a different entry on each local death, while Showdown quantity scaling only duplicates stackable rewards and leaves equip-like drops singular. The MonsterBook reward parser now also preserves authored zero-valued reward slots from `String/MonsterBook.img/<mobId>/reward` instead of filtering them out, so deterministic reward selection can legitimately resolve to no extra authored reward item when the published list includes empty slots. WZ-authored summon skill `MobSkill.img/105` (`level/1/0`, `limit`, `lt`/`rb`) now also routes through the existing summon runtime seam instead of falling through the generic status lane, so boss-side summon branches that publish the older `105` id no longer silently no-op while `200` works. `MobSkill.img/138` (`hp=45`, `interval=90`, `time=60`, `count=30`) now also routes through the existing player-targeted status seam as runtime `Burn` periodic damage instead of remaining visual/status-only when casts land inside the authored area. This run also brings `MobSkill.img/134` (`prop=80`, `time=10`, `interval=48`, `count=8`, broad authored `lt`/`rb`) into the same abnormal-status resistance lane as the other hostile player-facing mob statuses before it applies runtime `PainMark` periodic damage, instead of letting that rarer damage-over-time branch bypass resistance ownership. The uncommon boss-control seam was tightened further using the same status/skill-blocking owner path: player skill-block mirroring for `MobSkill.img/170` now resolves duration through the runtime status-duration resolver (including WZ-authored `x`-seconds fallback when `time` is omitted), and `MobSkill.img/171` now routes through the same abnormal-status resistance lane as other hostile player statuses instead of bypassing resistance checks, while polymorph pair `MobSkill.img/172`/`173` now also follows that same resistance ownership instead of bypassing resist checks. Packet-owned `MoveMob` `TARGETINFO` mob locks now also feed the hypnosis target seam as preferred victims within the existing same-priority tier resolver path, so the client-owned lock target can preempt distance-only local swaps while still yielding to higher-priority encounter/team tiers and existing eligibility or range gates. Mob-side status/heal/clear targeting now also keeps encounter-team ownership in the same area-resolution seam: nearby mob-status skills no longer leak across opposing `life.team` lanes, while teamless/default maps keep the previous broad area admission. This run also tightens packet/field-owned mob affected-area status application: when an `AffectedAreaSourceKind.MobSkill` area lands a runtime mob status on the local player, the same post-apply seam now plays the authored affected-hit feedback and mirrors skill-blocking statuses through `PlayerSkillBlockingStatusMapper`/`TryApplyMobSkillBlockingStatus`, so lingering mob-skill fields no longer apply seal/stun/freeze/seduce/polymorph/stop-motion runtime state without the matching client-shaped skill rejection lane. Focused `UnitTest_MapSimulator` coverage now exists in `MobStatusParitySeamTests` for remote mob affected-area blocking mirror admission, stable authored reward-list selection (including identity-seed determinism), hypnosis target-retention tie-breaks plus bounded distance-retention assertions, packet-owned hypnosis target preference and packet-lock expiry assertions, `134` resistance-lane plus periodic-state assertions, `170` duration-fallback assertions, and `171`/`172`/`173` resistance-lane assertions, alongside the earlier reward quantity coverage, summon-id routing assertions, `138` player-status admission/resistance assertions, and encounter-team mob-status target compatibility assertions. | Status-heavy encounters now track more client-shaped consequences beyond status visuals: doom movement speed reservation is WZ-backed, hypnosis retargeting has clearer target exclusion plus same-team Carnival-like priority order with stable same-tier victim retention plus bounded out-of-range lock retention for already-resolved victims, and now also respects packet-owned lock intent through same-tier preferred target admission from `TARGETINFO` instead of distance-only local churn when that packet lock is valid, `157` is no longer a no-op for rewards, showdown-side reward parity now affects EXP plus both monster-card and lightweight authored reward-list item drops, fear now applies both its runtime status lane and the matching field-darkening presentation instead of stopping at control-state effects, `138` now applies runtime burn-state damage through the same mob-skill player-status lane, and uncommon status branches now keep `134` PainMark resistance plus `170` lock timing and `171`/`172`/`173` resistance ownership aligned with the shared runtime status lane instead of synthetic side paths. Nearby mob-side status/heal/cure skills now also stop crossing opposing encounter teams in shared fields, so Carnival-like team ownership is preserved during area buff/cleanse casts. Remaining gap is narrower but still real: the simulator still does not model full server-authored mob drop tables, per-item drop rates, or full multi-roll reward emission beyond this `String/MonsterBook.img`-backed single-roll seam, whether `157` has additional non-reward consequences, deeper doom control/branch behavior outside the speed reservation seam, deeper client-owned post-apply semantics for uncommon boss-only branches beyond the now-shared `134/170/171/172/173` status admission timing and resistance ownership, and deeper packet-priority hypnosis retarget semantics beyond the now-modeled same-tier preferred lock-target path still need client-side matching. Packet/field-owned affected-area status post-apply ownership is now covered for hit feedback and skill-block mirroring, but broader server-authored affected-area lifecycle and packet ordering remain outside this row's current local runtime approximation. | `MobAI.cs`, `HypnotizeTargetResolver.cs`, `MobStatusRewardParity.cs`, `MobSkillStatusTargetParity.cs`, `MonsterBookManager.cs`, `PlayerManager.cs`, `PlayerMobStatusController.cs`, `MapSimulator.cs`, `MobAttackSystem.cs`, `UnitTest_MapSimulator` |
| Partial | Special mob interactions | WZ `selfDestruction` mobs trigger reserved bomb or time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, and generic WZ `info/removeAfter` encounter mobs expire through the simulator `Timeout` death lane instead of living forever. Escort and WZ `info/damagedByMob` encounter mobs no longer aggro the player, this now flows through parsed mob-data flags instead of only the older simulator `Friendly` alias, escort, `damagedByMob`, and generic `removeAfter` encounter spawn points no longer auto-respawn through the normal mob-pool loop, and special-death, escort, `damagedByMob`, swallow, and timeout encounter kills suppress reward drops. WZ `info/damagedByMob` encounter mobs ignore player-originated basic, skill, projectile, and summon damage, hostile mobs retarget live escort or `damagedByMob` encounter actors onto `MobTargetType.Mob`, that lane treats only live encounter participants as valid mob-vs-mob damage recipients so delayed area or projectile splash from encounter combat no longer leaks onto unrelated ordinary mobs, and encounter retargeting honors authored `life.team` separation by rejecting explicit same-team actors and preferring opposing-team targets before teamless fallbacks. Escort follow respects live `life.info` progression by attaching only the lowest active escort index until that stage clears. Wild Hunter swallow routing mirrors client-backed digest cadence more closely by holding `33101005` targets in repeated `500 ms` wriggle suspension pulses across the `3000 ms` digest window before resolving the `33101006` follow-up buff. Unresolved absorb outcomes expire through a bounded client-style fallback window derived from visible swallow action timing plus wriggle cadence, with `skill/effect` only as a fallback when no action animation is available (WZ anchor: `Skill/3310.img/skill/33101006/effect/*/delay=120`). Early absorb outcomes buffer briefly through the same swallow seam so packet-owned success or failure results that arrive before pending absorb state is armed still resolve onto the correct target. This run tightens swallow buffer ownership to the explicit armed-vs-unarmed seam: absorb outcomes are buffered only while no swallow state is armed (including no pending-absorb buffering once armed), and the short-lifetime buffer now keeps only the newest result for each skill-target pair by replacing prior buffered entries for that same pair, while still evicting the oldest unmatched pair first at capacity so newer authoritative outcomes win inside the parity window. Astaroth-style rage bosses parse WZ `ChargeCount` plus `AngerGauge` data, reserve `attack/info/AngerAttack` entries until charge fills, consume that gauge on the flagged attack, and render the mob-local anger gauge plus burst effect from dedicated WZ animation nodes. Automated seam coverage now reflects active tests in source: `MapleLib.Tests/MobDataSpecialInteractionParseTests.cs` covers `info/damagedByMob`, string-or-int `info/removeAfter`, `info/selfDestruction`, `info/ChargeCount`, and `info/AngerGauge`; `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeParityTests.cs` and `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeBufferParityTests.cs` cover swallow absorb-outcome gating, armed-state buffer rejection, newest-result replacement ownership for repeated skill-target pairs (including full-capacity matched-pair replacement without evicting unrelated buffered pairs), bounded buffering, and expiry pruning; `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs` covers anger-gauge burst cadence ownership; and `UnitTest_MapSimulator/SpecialMobInteractionEncounterParityTests.cs` now guards encounter respawn/drop suppression rules, encounter participant admission gating, `life.team` priority rejection/fallback ordering, and lowest-live-index escort progression follow gating. | The remaining gap is mostly script-, packet-, or end-to-end-owned rather than generic WZ seams: broader scripted rage or boss-phase logic beyond the Astaroth-style charge lane, authoritative packet choreography outside the now-tightened swallow absorb-result ownership seam (for example full server-driven sequencing around follow-up skill ownership, server packet ordering, and field state), deeper map-specific escort scripting, and broader field-script encounter choreography beyond the current WZ-backed retarget, timeout, escort progression, swallow seam, and rage-gauge seams are still missing. | `MobItem.cs`, `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `SkillManager.cs`, `WildHunterSwallowAbsorbOutcomeBuffer.cs`, `MapSimulator.cs`, field systems, `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeParityTests.cs`, `UnitTest_MapSimulator/WildHunterSwallowAbsorbOutcomeBufferParityTests.cs`, `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs`, `UnitTest_MapSimulator/SpecialMobInteractionEncounterParityTests.cs` |



### 2. Mob image-entry ownership discovered by targeted `CActionMan` scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Mob image-entry / linked-template parity | `LifeLoader` now has a distinct `GetMobImgEntry`-style owner ahead of mob action and attack asset loading. It caches normalized mob image entries by template id, formats template ids to the Mob IMG resource name, follows `info/link` recursively, preserves the current entry's `info` branch, and exposes missing top-level branches from linked templates to the action loaders without mutating the underlying WZ tree. | This keeps the client ownership split explicit: mob runtime state, normalized image-entry resolution, and frame/action construction are separate seams, so linked-template inheritance is no longer hidden inside the broader action-loader or runtime rows. | mob template/image-entry layer (`CActionMan::GetMobImgEntry`, `LifeLoader.cs`, `MobAnimationSet.cs`, `MobItem.cs`) |

### 3. Mob action-loader ownership discovered by targeted `CActionMan` scan

| Status  | Area                                | Gap | Why it matters | Primary seam |
|---------|-------------------------------------|-----|----------------|--------------|
| Partial | Mob action-loader / frame-build parity | The simulator keeps the explicit `CActionMan::LoadMobAction` seam and now carries a broader explicit client action-slot owner path in `LifeLoader` instead of only keyed-by-authored-name loading. In this run the simulator slot surface was expanded to the client-owned 43-slot contract recovered from IDA (`CMob::CMob` allocates `0x2B`), and `s_sMobAction` handling now materializes a sparse canonical table with deterministic slot order over that full range rather than a dense 30-slot core-only table. Canonical resolution now explicitly covers client-owned buckets used by `CUIMonsterBook::LoadMobAction` (`attack1..9` in slots `13..21`, `skill1..17` in slots `22..38`) plus recovered non-attack slots (`stand`/`move`/`fly`/`hit1`/`die1`/`regen`, `bomb`, `hit2`, `die2`, `dieF`, and slot `39` ownership now canonicalized to `chase` with legacy alias `moveaction16` for `CMob::MoveAction2RawAction` case `16`). Follow-up IDA recovery on `_dynamic_initializer_for__s_sMobAction__` now also resolves the previously unknown slot names through the same seam (`slot 7 -> hit1`, `slot 8 -> hit2`, `slot 11 -> die2`, `slot 40 -> rollingSpin`, `slot 41 -> siege_pre`, `slot 42 -> tornadoDashStop`), with duplicate-name slots retained as explicit owner slots while authored-name-to-slot resolution keeps the existing primary canonical slots (`hit1 -> 3`, `hit2 -> 9`, `die2 -> 10`) to avoid drift in established runtime fallbacks. Mapped slot actions are still cached by slot and then materialized before authored-name fallbacks are applied. WZ-first follow-up in this run confirmed non-indexed authored mob roots (`hit`, `die`) still appear on some templates (for example `Mob/9300391.img` and `Mob/9400584.img`), and the loader now canonicalizes those roots through the same slot-owner seam (`hit -> slot 3 -> hit1`, `die -> slot 4 -> die1`) instead of leaving them outside slot-owned naming; the same pass also confirmed authored `chase` and `dieF` roots in v95 mob data and now routes them through the slot-owner seam (`slot 39`, `slot 12`) rather than fallback-only authored-name loading. Existing per-frame parity now covers the broader recovered frame-owned payload on this same seam: `delay`, `head`, `lt`, `rb`, `multiRect`, frame-owned `a0`/`a1` alpha ramps and `z` lane metadata, origin-backed visual bounds/frame metadata into `MobAnimationSet`, reverse-pass duplication for client replay flags (`zigzag` and `reverse`), runtime per-frame alpha interpolation in `MobItem`, and lane-aware body-rectangle collision consumers in `MobItem`/combat targeting. This pass tightened the native frame-entry construction side of the same seam: WZ sampling still shows ordinary mob action frame rows as direct canvas children (for example `Mob/9300391.img/hit/0`, `Mob/9400633.img/attack4/*`, and `Mob/8840000.img/*/multiRect` under direct frame canvases), and IDA for `CActionMan::LoadMobAction` confirms each enumerated action child is queried as an `IWzCanvas`; the loader now ignores nested numeric subproperty canvases for primary frame and metadata enumeration instead of recursively materializing phantom frames outside the native direct-canvas/UOL cache contract. Attack-support metadata and effects continue to key through the same resolved action-name seam so mapped slot actions stay consistent across frame, hit, projectile, warning, and extra-effect consumers, and focused seam coverage in `UnitTest_MapSimulator/MobActionLoaderParityTests.cs` now also covers direct-canvas-only frame enumeration, non-indexed `hit`/`die` canonicalization, `bomb`/`dieF`/`chase` slot-name resolution (including `moveaction16` alias retention), frame-owned `a0`/`a1`/`z` metadata extraction, and action-level `zigzag` replay-flag handling in addition to the expanded slot contract (43-slot surface, canonical name resolution, recovered attack/skill buckets, and unknown-action fallback). Remaining gap is narrower but still `Partial`: slot-name ownership for the 43-slot `s_sMobAction` surface is now recovered (including former unknowns `7`, `8`, `11`, `40`, `41`, `42`) and primary frame-entry construction now follows the recovered direct-canvas cache boundary, but rarer frame-owned fields or branch-specific consumers beyond the currently modeled origin/rect/head/zigzag/reverse/multi-body/alpha/z/direct-canvas seams may still exist in the native cache path, and byte-identical native cache construction behavior is not yet fully proven across all branch families. | Naming and partially implementing the `CActionMan` owner keeps mob animation parity split the same way as summons, NPCs, pets, and employees: runtime `CMob::*` logic can keep evolving independently while loader-owned frame table construction, cached reuse, authored rect math, per-lane body geometry, and reverse playback now have a concrete simulator seam instead of staying implicit inside generic mob loading. | `LifeLoader.cs`, `MobAnimationSet.cs`, `MobItem.cs`, `MobAttackSystem.cs`, `UnitTest_MapSimulator/MobActionLoaderParityTests.cs` (`CActionMan::LoadMobAction`, `CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |





### 4. Combat feedback animation-owner parity discovered by `CAnimationDisplayer` scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Damage-number and tremble animation-owner parity | The simulator keeps combat feedback on the explicit `CAnimationDisplayer` seam instead of collapsing it into generic mob-runtime or HUD code. `CAnimationDisplayer::Effect_HP` remains the owner for number-family selection, StringPool id `0x1A15` formatting, digit composition, temporary `CreateCanvas(lWidth, 57)` composition, critical-banner handling, and one-time layer registration, while `CAnimationDisplayer::Effect_Tremble` remains the owner for config/enforcement gate, start/end ticks, heavy-vs-normal duration (`1500` vs `2000`), and reduction factors (`1.0`, `0.85`, `0.92`). WZ evidence stays anchored to `effect/BasicEff.img/NoCri0`, `NoCri1`, and `NoCri1/effect`; this run revalidated `NoCri1/effect` as `62x57` with origin `41,70`, revalidated `NoRed0` as the authored special-text owner for `Miss`, `guard`, `shot`, `counter`, and `resist`, reconfirmed `NoRed0/shot` as `137x65` with origin `54,38` inside the same `57px` temporary-canvas owner lane, and reconfirmed `NoBlue0/shot` and `NoViolet0/shot` are absent. IDA evidence remains `CAnimationDisplayer::Effect_HP` at `0x444eb0` (red-only critical branch, unsupported-color early return for non-`0/1/2`, critical pre-spacing seed `0x1E`, `lCenterTop - 47`, two primary `InsertCanvas` phases `400ms` hold then `600ms` fade, separate critical layer write at `lY - 30`, `CreateLayer` canvas `0`, initial option `0xC0050004`, priority `-1`, final option `0`, AddRef/RegisterOneTimeAnimation/Release layer lifetime, and temporary-canvas release after registration) and `CAnimationDisplayer::Effect_Tremble` at `0x439a70`. Managed ownership keeps canonical special-result normalization (`Miss`, `guard`, `shot`, `counter`, `resist`), routes all special-text families back to authored `NoRed0` ownership, canonicalizes unresolved names to `Miss`, mirrors native unsupported-color rejection on both color-family parsing and effect-UOL resolution seams, enforces strict authored special-text ownership, and hardens the no-owner seam so missing authored owner data cleanly no-ops instead of null-dereferencing. Load-time ownership continues to admit special-result sprites only from authored owner set `NoRed0` (case-insensitive set-name match), and runtime owner identity still resolves an explicit combat-feedback frame-cache key from the owner UOL seam (`ResolveAnimationDisplayerCombatFeedbackFrameCacheKey`) so non-owner aliases cannot split owner-cache lanes. The damage-number seam keeps red-only critical-owner split, large-first/small-tail digit mix, separate critical banner, recovered critical-leading spacing (`30px` from native `0x1E` seed), composite-canvas release on eviction/natural completion, and shared one-time registration timing (`400ms` hold, `600ms` fade, `30px` rise, `250ms` critical delay). This run also closes color-owner leaks at the public combat-effects and renderer helper seams: `CombatEffects.AddPartyDamage` now routes party/summon damage through the violet owner lane (`NoViolet0`/`NoViolet1`) instead of collapsing it back to red, and `DamageNumberRenderer.SpawnReceivedDamage` now uses the same violet owner lane that `CUser::MakeIncDecHPEffect` uses for received damage instead of the blue HP-increase lane. `OneTimeCanvasLayerAnimation` replay continues to preserve owner-to-layer ordering by prepending temporary-canvas operations before layer operations and now carries the recovered temporary-canvas release after one-time layer registration in addition to `CreateCanvas` size, source `InsertCanvas` fixed move offsets, and explicit `255 -> 255` alpha. Focused executable seam coverage in `UnitTest_MapSimulator/CombatFeedbackAnimationOwnerParityTests.cs` now also pins party/summon and received-damage routing to the violet owner lane and recovered temporary-canvas release ordering after registration, in addition to combat-feedback cache-key owner-lane canonicalization across color aliases, unknown-name canonicalization to `Miss` on cache-key/UOL resolution, supported-special-name admission, recovered red-only critical leading-spacing behavior, canonical special-text normalization, load-time owner-set admission (`NoRed0` only), strict owner color-token admission (`red`/`blue`/`violet` and `0/1/2`) with rejection of non-owner aliases/tokens (`purple`, `3`, `-1`), `NoRed0` owner routing plus unsupported-color/UOL rejection parity, recovered critical-layer registration constants (`400ms` hold, `600ms` fade, `250ms` overlay delay, `30px` rise, `0xC0050004` option, priority `-1`), recovered temporary-canvas native trace ordering with explicit `255 -> 255` insert invariants, and `Effect_Tremble` gate/duration/reduction parity including additional-time no-reduction extension for both positive and negative non-zero additional-time inputs (`if (tAddEffectTime)` parity). Remaining gap is narrowed to the native Gr2D execution path: the simulator still does not instantiate real COM `IWzGr2DLayer` objects, cannot replay COM object identity/reference-count behavior exactly beyond recovered owner metadata and release ordering, and does not reproduce byte-identical native blend/composite output, so parity remains an owner-faithful managed approximation with recovered metadata, color ownership, lifetime ordering, and timing. | Without an explicit `CAnimationDisplayer` combat-feedback row, combat parity can look further along than it is once mob AI, hit resolution, HP bars, and generic damage text exist. Naming the owner keeps future work anchored to the client seam that actually builds and schedules damage-number and tremble presentation instead of burying remaining visual differences inside unrelated mob-runtime rows. | combat feedback animation layer (`CAnimationDisplayer::Effect_HP`, `CAnimationDisplayer::Effect_Tremble`, simulator seams: `CombatEffects.cs`, `DamageNumberRenderer.cs`, `ScreenEffects.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `AnimationEffects.cs`, `UnitTest_MapSimulator/CombatFeedbackAnimationOwnerParityTests.cs`) |

### 5. Mob anger-gauge burst animation-owner discovered by follow-up IDA scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Mob anger-gauge burst animation-owner parity | The simulator keeps the visible full-charge burst on the recovered mob-to-animation owner seam instead of inline mob rendering. WZ evidence remains anchored to `Mob/9400633.img` (`AngerGaugeEffect` has 8 frames at 150 ms, `info/ChargeCount = 3`, `info/AngerGauge = 1`, `attack4/info/specialAttack = 1`, `attackAfter = 3300`). Client evidence remains `CMob::Update` -> `CMob::AngerGaugeFullChargeEffect` -> `CAnimationDisplayer::Effect_FullChargedAngerGauge`: timed replay gate `m_bFullChargeEffectStartTime + m_bFullChargeEffectTime`, StringPool UOL build from ids `0x03CE` / `0x0C2F`, head-origin and action-layer overlay-parent, `LoadLayer` (`canvas 0`, `rx/ry = 0`, option `0xC00614A4`, alpha `255`, `bFlip = 0`, reserved `0`), `GA_STOP`, then `RegisterOneTimeAnimation(delay 0)`. Managed parity keeps owner-lane trigger/cadence split: timed special attacks drive owner replay gating, untimed/missing-owner timing stays on authored-frame fallback cadence, stale timed intervals are cleared immediately when later special attacks omit `attackAfter`, fallback suppression outside special-attack states keys off live runtime owner timing (`_runtimeAngerGaugeFullChargeEffectIntervalMs`) instead of static attack-list metadata, fallback burst registrations refresh the shared owner start-time lane (`_fullChargeEffectStartTime`), and owner replay state now advances only after an actual owner registration candidate exists (non-empty frames, resolved owner-or-loaded path, and active animation displayer) so timing is not consumed by non-rendered bursts. This run keeps that seam intact and now ships focused `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs` coverage for the timed replay gate (`m_bFullChargeEffectStartTime + m_bFullChargeEffectTime`), stale timed-interval clearing when later special attacks omit `attackAfter`, owner-vs-fallback cadence split helpers (including outside-special runtime-timing suppression), owner-lane start-time refresh gating after fallback registrations, recovered one-time registration operation constants (`LoadLayer` option `0xC00614A4`, `GA_STOP`, `RegisterOneTimeAnimation` delay `0`), owner-registration precondition checks, and shared `MapleStoryStringPool` seam invariants (pinned ids `0x03CE` / `0x0C2F`, owner-path fallback behavior, and formatted owner path `Mob/<id>.img/AngerGaugeEffect`) so owner-path formatting and timing ownership cannot drift. | Rage-boss parity is closer because burst ownership, timing gate, source-UOL retention, `GA_STOP`, no-flip `LoadLayer`, action-layer overlay-parent kind, and owner-specific one-shot registration stay on one recovered seam with executable coverage. Remaining gap: presentation is still a managed DX approximation and does not recreate the native COM `IWzGr2DLayer` graph/lifetime or byte-identical Gr2D blend output. | mob anger-gauge presentation layer (`CMob::AngerGaugeFullChargeEffect`, `CAnimationDisplayer::Effect_FullChargedAngerGauge`, `CAnimationDisplayer::LoadLayer`, `CAnimationDisplayer::RegisterOneTimeAnimation`, `Mob/9400633.img/AngerGaugeEffect`, `Mob/9400633.img/attack4/info/specialAttack`, `MapleStoryStringPool.cs`, `AnimationEffects.cs`, `MobItem.cs`, `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs`) |

## Priority Order


If the goal is visible parity first, the next work should be sequenced like this:



1. Avatar parity pass:

   Validate default asset selection, client-managed overlay/effect layers, and remaining action coverage against WZ and client behavior.

2. Skill execution pass:

   Implement queued follow-up, repeat-skill, bound-jump, rocket-booster teardown, and movement-coupled shoot families rather than extending the generic cast path indefinitely.

3. UI feedback pass:

   Add quick-slot validation, quest or balloon feedback, chat surfaces, and complete skill-window behavior.

4. Movement refinement pass:

   Remove the remaining ladder/float stubs, add passive transfer-field handoff, and tighten platform/dynamic foothold sync.

5. Interaction pass:

   Add NPC talk/quest flow, follow and direction-mode handling, and richer reactor interactions.



## Working Rule For Future Updates



Update this file directly, and always classify each item as one of:



- `Implemented`

- `Partial`

- `Missing`



That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".




