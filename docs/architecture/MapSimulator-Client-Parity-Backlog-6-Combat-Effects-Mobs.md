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



| Status | Area | Gap | Why it matters | Primary seam |

|--------|------|-----|----------------|--------------|

| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present, direct melee attacks now use the same delayed action scheduler as projectile and ground-warning families using WZ `attackAfter` timing instead of pure frame polling, mob skills now route their non-summon `MobSkill.img` `effect` and `mob` branches through the runtime animation layer so mob/target/screen-position skill cues no longer silently no-op, target-position mob skill visuals now anchor to the mob's actual live target instead of always snapping to the player when the mob is casting onto a summon or another mob, summon skill `200` still resolves its live summon list and limit from `MobSkill.img` so temporary summons enter the field through the mob pool instead of being skipped, common mob self or party buff skills now read `MobSkill.img` `x`/`time`/`lt`/`rb` data to apply runtime `PowerUp`, `MagicUp`, `PGuardUp`, `MGuardUp`, and `Speed` temp stats to the caster or nearby mobs instead of remaining visual-only, `MobSkill.img` skill `114` now uses the same WZ runtime area and `x` value to heal nearby mobs instead of staying cosmetic, common player-targeted mob debuffs now also read `MobSkill.img` `time` / `interval` / `prop` / `x` / `lt` / `rb` data so seal, darkness, weakness, stun or freeze, poison, slow, and dispel skills apply runtime local-player consequences instead of stopping at visuals, with darkness feeding player miss chance, weakness suppressing jumps, stun or freeze locking movement and skill use, poison ticking direct damage, slow scaling live move speed, and dispel clearing active buffs, while the rarer player-facing debuff family now also routes through the same runtime path instead of staying blocked by simulator heuristics: authored `MobSkill.img` `lt`/`rb` ranges now feed autonomous skill selection so wide-area curse, seduce, reverse-input, undead, and banish skills can fire from their published reach instead of a fixed short-range fallback, those same authored rectangles now mirror the mob's live facing when the runtime applies nearby or player-targeted mob skills, banish skill `129` now applies its runtime banish lockout after teleporting to spawn instead of short-circuiting at teleport only, undead skill `133` now reflects recovered HP using the authored `MobSkill.img/x` percentage instead of always inverting heals at a hardcoded full amount, potion-style and quest-buff consumable recovery now route through `PlayerCharacter.Recover` so the same runtime curse caps, undead reflected-heal percentage, and local-player recovery modifiers apply there too instead of only on skill- or chair-driven recovery, and mob skill `135` now blocks the simulator's supported consumable lanes through the same runtime `StopPotion` path instead of only blocking raw recovery, including HP or MP recovery, cure-only, temporary buff, morph, and map-move consumables. In this run, the higher-end status lane was tightened further using the published `MobSkill.img` ordering and `x` data: skill `136` now lands as runtime `StopMotion` instead of being folded into fear, now also mirrors into the shared player skill-blocking lane through the same runtime duration and clears that lockout immediately when the runtime status is cured or expires, so direct casts and contact-disease applications no longer stop at movement lock without the matching client-facing skill rejection path; skill `124` curse now also scales local mob-kill EXP reward through the same runtime curse percentage already carried by the player-status resolver instead of staying limited to the HP/MP recovery-cap side effect; and skill `137` now lands as `Fear` instead of borrowing seduce-style forced horizontal control, with authored `x` continuing to drive the existing field fear overlay intensity and repeated fear applications refreshing the live overlay whenever that authored intensity changes even if the new cast does not extend the previous fear duration. Inherited `MobSkill.img` level data for `lt` / `rb` / `time` / `interval` / `prop` / `count` / `targetMobType` / `x` / `y` / `hp` now also feeds the same runtime resolver instead of dropping back to partial defaults when later levels omit repeated fields, sparse higher-level `MobSkill.img` branches now also inherit prior `affected` / `effect` / `mob` / `tile` / `time` data through the shared resolver so direct-cast hit feedback, mob-side cues, and field-owned visuals keep rendering when later levels only carry an `info` stub, summon skill `200` now inherits authored `limit`, `lt` / `rb`, and summon-entry lists from the nearest previous level instead of treating sparse follow-up levels as empty, and mob skill source slots now preserve the authored WZ `info/skill/*` index so `preSkillIndex` chains key off the actual published slot instead of the loader's incidental enumeration order. The direct-attack disease lane now sources its extra player skill-blocking timer from that same runtime duration and only keeps that lockout when the shared runtime status lane actually lands instead of still blocking on resisted or failed casts, direct player-targeted mob skill casts now also play their authored `MobSkill.img/affected` hit feedback when the runtime status applies instead of reserving that animation lane to contact diseases only, and clearing or expiring those same runtime statuses now also clears the mirrored player skill-blocking lane immediately instead of leaving cured seal, stun, freeze, seduce, polymorph, or `StopMotion` casts blocked until a stale timer elapses. Regular attacks that carry `info/attack/*/disease` now also resolve the same runtime `MobSkill.img` status path and affected-hit animation feedback on contact instead of only applying the old blocking-only callback for seal, stun, freeze, seduce, or polymorph. Seduce-side control aftermath is now tighter too: the mob-status frame state explicitly marks pickup as blocked during forced-move control, the player manager now honors that state in the separate drop-pickup path, and entering seduce now forces the local player out of ladder or rope attachment before horizontal control takes over instead of leaving the character stuck in place on climb geometry or a seated hold state. Chained post-action combat decisions no longer incorrectly stall when the mob is currently targeting a summon or another mob instead of the player, and autonomous mob skill selection now honors WZ `priority`, `preSkillIndex` / `preSkillCount`, `onlyFsm`, and `skillForbid` so scripted-only skills stop leaking into the generic loop, charged follow-up skills wait for their prerequisite casts, and skill lockout windows stop immediately chaining into unrelated skills. This pass also makes the generic selector consult the authored runtime before it commits to a cast: self or party buff, heal, and cure skills now skip generic auto-selection when every eligible target already has the authored buff state, already sits above the authored heal `hp` threshold, or has no negative status to clear, summon skill `200` now also skips generic auto-selection while its authored `limit` is already full instead of wasting a cast on a guaranteed no-op, and player-facing disease skills now require the authored `lt` / `rb` area to reach the live player hitbox before the generic loop burns that action. | The loop is closer to client `CMob` behavior, and the remaining gap is now concentrated on deeper control fallout rather than WZ-backed skill admission: broader client matching for reverse-input edge cases beyond the current directional swap, any curse-side rules beyond the now-shared recovery cap plus EXP reduction seam, the exact client consequences of `StopMotion` beyond the current movement lock and mirrored skill-lock lane, and any still-unmodeled uncommon player-facing mob-skill transitions or branch-specific control rules after the new seduce forced-move cleanup, pickup lockout path, resisted-cast blocking fix, sparse-level `MobSkill.img` inheritance pass, authored source-slot preservation for prerequisite skill chains, the widened `StopPotion` consumable gate, the fear-overlay refresh path for skill `137` intensity changes, the local curse-EXP follow-through, and now the remaining no-op selector cases that still need fuller client evidence beyond the new WZ-backed heal/buff/cleanse/summon/player-area gate. Focused simulator seam coverage now also exists in `UnitTest_MapSimulator/PlayerMobStatusParityTests.cs` for curse EXP scaling and `StopMotion` mirrored skill-blocking cleanup, alongside `PlayerMobConsumableBlockEvaluatorTests` for the widened `StopPotion` consumable gate, `MobFearFieldEffectParityTests` for the fear overlay refresh gate when re-applied casts change WZ-authored intensity without extending duration, and `MobSkillSelectionParityTests.cs` for the new WZ-backed skill-admission gate on heal, buff, cure, and player-area selection. | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `MobSkillEffectLoader.cs`, `MobSkillLevelResolver.cs`, `MobSkillData.cs`, `MobItem.cs`, `CombatEffects.cs`, `MobSkillSelectionParity.cs`, `PlayerManager.cs`, `PlayerMobStatusController.cs`, `PlayerCharacter.cs`, `UnitTest_MapSimulator` |

| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |

| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, `tremble`, and `type` attack metadata through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, triggers tremble feedback from flagged impacts, preserves puppet/summon aggro targets through AI updates, routes delayed mob hits to registered puppet targets instead of always falling back to the local player, now syncs targeted summons through their live runtime animation bounds instead of a fixed fallback box so summon-targeted mob attacks collide against the same world-space body rect the simulator draws, removes consumed summons from the active summon list when that branch lands, preserves summon slot indices alongside live summon object ids so queued summon-targeted attacks can still resolve through the client-style slot lane if the original summon instance is replaced before impact, preserves external mob targets through AI updates so delayed mob attacks can resolve `MobTargetType.Mob` entries against live target mob body rects and apply mob-vs-mob damage instead of silently dropping that client branch, now mirrors the client `CMob::ProcessAttack` rect pass for delayed direct or area hits by colliding non-targeted attack rectangles against every live summon and mob body in range instead of only the explicitly tracked target, lets explicitly targeted summon or mob projectiles apply their delayed impact when they reach the scheduled destination instead of depending only on the projectile transit hitbox, now also gives untargeted non-ground projectile impacts the same late rect sweep against the local player, live summons, and encounter mobs when the ball reaches its destination instead of dropping those arrival-time collateral hits, routes WZ `type = 1` delayed direct or ground attacks through the client-style locked-target branch so queued player, summon, or mob targets resolve on impact without depending on a late rectangle overlap test, matches the client `CMob::DoAttack` admission gate more closely by checking type-1 queue eligibility from the authored attack origin to a clamped target-body point within `range + 10` instead of the older broad rectangle overlap heuristic, now resolves those player-targeted admission checks, multiball lane picks, and locked delayed impacts through the live player body rect seam instead of a coarse position-only fallback, now also narrows the WZ `jumpAttack` grounded gate to the same untargeted direct-rect player or summon overlap branch the client uses instead of incorrectly suppressing airborne hits from locked-target, projectile, or area impact paths, now parses WZ projectile `range/sp` + `range/r` launch-origin metadata, uses that authored origin for projectile spawn and type-1 admission checks, carries WZ `info/attack/*/attackCount` into live ranged attack entries, fans multi-ball projectiles across authored projectile lanes by choosing per-lane destinations against live player, summon, or encounter-mob bodies when those entities line up with the lane instead of cloning every extra ball off a fixed synthetic spread from the primary target, now synthesizes the extra multiball lane list from the full authored `range/start/areaCount` slot set instead of only the first `attackCount` offsets so wide projectile patterns stay centered and preserve authored lane order more closely, now keeps those authored lane points separate from the locked target body point so multiball projectiles continue to fly along the selected slot lane even when multiple balls resolve against the same live player, summon, or encounter-mob body, now also allows those per-lane heuristic resolutions to repeat the same live player, summon, or encounter-mob body across multiple bullets when separate authored lanes still line up with that body, reuses WZ-authored projectile and hit frame bounds for in-flight and arrival-time collision checks instead of a fixed synthetic `16x16` box, now mirrors the client `CMob::SetBallDestPoint` destination clamp for projectile travel by enforcing a forward lane, clamping the vertical slope to `0.6`, and normalizing against authored `range/r`, now reads WZ `attack/info/randDelayAttack` into delayed area attacks, now synthesizes the simulator-side random area delay list once per selected area slot so every foothold expanded from the same client-style `type = 3` column shares one queued delay instead of rolling a fresh delay per foothold hitbox, and now also exposes a client-shaped override seam for `m_aMultiTargetForBall` and `m_aRandTimeforAreaAttack` so the attack scheduler will prefer packet-fed extra projectile lane points and per-slot area delays whenever those lists are supplied. In the packet path, the existing `MoveMob` packet codec and mob-attack inbox now preserve packet `TARGETINFO` too, so the next queued attack prefers packet-owned locked player, summon-slot, or mob victims through the same live body-rect seams the simulator uses for delayed type-1 admission and impact resolution instead of dropping that client-owned target branch and falling back to AI heuristics, packet-owned move-header attack admission now clears queued overrides when `NextAttackPossible` is false while carrying packet-facing into projectile lane fallback, launch-origin orientation, `SetBallDestPoint` forward-lane clamping, and locked-target admission-distance checks instead of always falling back to the live AI-facing bit, the same packet path now also honors client `bNotChangeAction` by refusing to queue simulator attack overrides for move headers that suppressed `CMob::DoAttack` on the client instead of leaking stale packet-owned locked targets or projectile lane lists into a later AI-selected attack, and the packet-owned move discard bit now feeds a live `MobMovementInfo` interrupt seam so attack move headers mirror the client `CMovePath::DiscardByInterrupt` split by clearing pending steering first and only forcing an airborne ground mob back onto a foothold when `bNotForceLandingWhenDiscard` is false. This run closes another `MobBullet` timing branch straight from WZ plus client evidence: the simulator already loaded authored `range/r` and `attackAfter`, but ranged attacks now keep queued mob bullets dormant until `attackAfter` and time projectile expiry from the same client formula `range/r * 600 / nBulletSpeed` that `CMob::DoAttack` uses when it constructs `MobBullet`, instead of launching immediately and deriving impact time only from raw world-distance-over-speed. | `CMob::DoAttack` / `ProcessAttack` are closer on `MobBullet` setup and delayed area placement now that the simulator uses authored launch points, live player-body admission and impact resolution, full-slot multiball lane selection, authored-order extra bullet synthesis, authored lane-point preservation separate from delayed locked-target victim resolution, client-style direct-only `jumpAttack` grounding, WZ-sized projectile-impact bounds, client-style `SetBallDestPoint` destination normalization, client-style `attackAfter` projectile arming, authored `range/r * 600 / nBulletSpeed` bullet lifetime, authored `randDelayAttack`, per-slot random-delay reuse across type-3 multi-foothold expansions, client-style type-3 multi-foothold column expansion, type-4 source-Y placement, heuristic duplicate-target multiball resolution when multiple authored lanes legitimately overlap the same body, and packet-owned `TARGETINFO`, `m_aMultiTargetForBall`, `m_aRandTimeforAreaAttack`, `NextAttackPossible`, `bNotChangeAction`, and `bNotForceLandingWhenDiscard` ingestion through the live `MoveMob` inbox path instead of only through manual override seams. The remaining gap is narrower but still centered on deeper client-owned `MobBullet` and move-path behavior: the runtime still does not mirror richer projectile travel shapes or post-launch side effects beyond the now-matched launch window, destination clamp, and authored lifetime formula, and the packet-owned move discard seam still lacks a fuller replay of `CMovePath` list truncation and follow-up motion-state ownership beyond clearing pending steering plus optional foothold snap when attack headers arrive. Focused simulator seam coverage now exists in `UnitTest_MapSimulator/MobProjectileTimingParityTests.cs` for the authored `range/r` bullet lifetime formula. | `MobAI.cs`, `MobAttackSystem.cs`, `MobPool.cs`, `MapSimulator.cs`, `MobMoveAttackPacketCodec.cs`, `MobMovementInfo.cs`, `UnitTest_MapSimulator/MobProjectileTimingParityTests.cs` (`CMob::DoAttack`, `CMob::OnMove`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`, `CMovePath::DiscardByInterrupt`, `attack/info/range/r`) |

| Partial | Full mob stat/status parity | Existing mob-temp-stat parity work remains in place (runtime status entries with expiry/tick metadata, signed stat shifts, DoT ticks, freeze/stun lock, immunity/counter lanes, threat/showdown/mind-control/polymorph inference, mob-skill status clear/self-buff lanes, and doom render/collision swap to `mob/0100101.img`). In this run, the doom movement seam now reserves transformed speed to the WZ-backed doom baseline from `mob/0100101.img/info/speed = -50` instead of a hardcoded synthetic slow, the hypnosis target resolver now excludes doomed candidates and explicitly prioritizes same-team encounter targets ahead of same-team non-encounter fallback candidates, and it now also preserves an already-resolved hypnosis victim within the same priority tier instead of thrashing to a merely closer alternative every sync tick, which better matches the client-shaped locked-target feel until a higher-priority candidate appears. `MobSkill.img/157` (`info` rich token, `x=1`, `time=180`) now also contributes a concrete reward-side consequence by scaling meso payout through the existing mob reward lane (`x` maps to +`x*100%` meso bonus while active) instead of remaining visual/status-only, inferred `Showdown` reward bonus parity now also reaches the simulator's item reward seam instead of stopping at EXP because the official skill text in `string/Skill.img` (`4121003` / `4221003`) says Showdown increases both EXP and items, authored fear now also reaches the client-shaped field-visual seam instead of staying control-only through the same `CField::InitFearEffect` / `OnFearEffect` / `OffFearEffect`-style seam with live `MobSkill.img/137/x` intensity, and the item reward seam itself now goes beyond monster cards by reading authored `String/MonsterBook.img/<mobId>/reward` entries: mob deaths can spawn one lightweight WZ-backed reward-list item, with that authored reward choice now selected deterministically from the published WZ reward order per mob identity instead of rerolling a different entry on each local death, while Showdown quantity scaling only duplicates stackable rewards and leaves equip-like drops singular. Focused `UnitTest_MapSimulator` coverage now exists in `MobStatusParitySeamTests` for stable authored reward-list selection and hypnosis target-retention tie-breaks, alongside the earlier reward quantity coverage. | Status-heavy encounters now track more client-shaped consequences beyond status visuals: doom movement speed reservation is WZ-backed, hypnosis retargeting has clearer target exclusion plus same-team Carnival-like priority order with stable same-tier victim retention, `157` is no longer a no-op for rewards, showdown-side reward parity now affects EXP plus both monster-card and lightweight authored reward-list item drops, and fear now applies both its runtime status lane and the matching field-darkening presentation instead of stopping at control-state effects. Remaining gap is narrower but still real: the simulator still does not model full server-authored mob drop tables, per-item drop rates, or richer reward weighting beyond this `String/MonsterBook.img`-backed single-item seam, exact client semantics for uncommon boss-only statuses (including whether `157` has additional branches beyond the meso lane), deeper doom control/branch behavior outside the speed reservation seam, and fully packet-owned hypnosis victim selection plus any remaining fine-grained retarget priority branches still need client-side matching. | `MobAI.cs`, `HypnotizeTargetResolver.cs`, `MobStatusRewardParity.cs`, `MonsterBookManager.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator` |

| Partial | Special mob interactions | WZ `selfDestruction` mobs now trigger reserved bomb or time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, and generic WZ `info/removeAfter` encounter mobs now expire through the simulator `Timeout` death lane instead of living forever. Escort and WZ `info/damagedByMob` encounter mobs no longer aggro the player, that lane now flows through a dedicated parsed mob-data flag instead of only the older simulator `Friendly` alias, escort, `damagedByMob`, and generic `removeAfter` encounter spawn points no longer auto-respawn through the normal mob-pool loop, and special-death, escort, `damagedByMob`, swallow, and timeout encounter kills now suppress reward drops. WZ `info/damagedByMob` encounter mobs now ignore player-originated basic, skill, projectile, and summon damage, hostile mobs now retarget live escort or `damagedByMob` encounter actors onto the simulator's `MobTargetType.Mob` lane instead of staying player-only, that lane now treats only live encounter participants as valid mob-vs-mob damage recipients so delayed area or projectile splash from encounter combat stops leaking onto unrelated ordinary mobs, and encounter retargeting now honors authored `life.team` separation by rejecting explicit same-team actors and preferring opposing-team targets before teamless fallbacks. Escort follow now respects live `life.info` progression by only attaching the lowest active escort index until that stage is cleared, Wild Hunter swallow routing now mirrors the client-backed digest cadence more closely by holding `33101005` targets in repeated `500 ms` wriggle suspension pulses across the `3000 ms` digest window before resolving the `33101006` follow-up buff, unresolved absorb outcomes now expire through a bounded client-style fallback window derived from the visible swallow action timing plus wriggle cadence with `skill/effect` only as a fallback when no action animation is available, and early absorb outcomes now buffer briefly through the same swallow seam so packet-owned success or failure results that land before the simulator finishes arming the pending absorb state still resolve onto the correct target instead of being dropped. Astaroth-style rage bosses now parse WZ `ChargeCount` plus `AngerGauge` data, reserve `attack/info/AngerAttack` entries until the charge count fills, consume that gauge on the flagged attack, and render the mob-local anger gauge plus burst effect from the dedicated WZ animation nodes. Automated seam coverage in source now matches those row claims: `MapleLib.Tests` covers `info/damagedByMob`, string-or-int `info/removeAfter`, `info/selfDestruction`, `info/ChargeCount`, and `info/AngerGauge`, and `UnitTest_MapSimulator/SpecialMobInteractionParitySeamTests.cs` now covers swallow timeout selection, `500 ms` wriggle pulse scheduling across the `3000 ms` digest window, absorb-outcome buffer replacement and expiry, encounter team-priority resolution, escort progression gating, reward-drop suppression, encounter respawn suppression, and the simulator-side `LifeLoader.BuildAttackInfoMetadata` seam that reads `attack/info/AngerAttack`. | The remaining gap is now narrower and mostly script, packet, or end-to-end owned rather than generic WZ seam work: broader scripted rage or boss-phase logic beyond the Astaroth-style charge lane, later Wild Hunter swallow packet ordering and authoritative field-state choreography beyond the early-result absorb buffer plus action-timed fallback cancel seam, the existing `500 ms` wriggle suspension loop, and the `3000 ms` digest gate, deeper map-specific escort scripting, and broader field-script or packet-owned encounter choreography beyond the current WZ-backed retarget, timeout, escort progression, and rage-gauge seams are still missing. | `MobItem.cs`, `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, field systems |



### 2. Mob image-entry ownership discovered by targeted `CActionMan` scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Implemented | Mob image-entry / linked-template parity | `LifeLoader` now has a distinct `GetMobImgEntry`-style owner ahead of mob action and attack asset loading. It caches normalized mob image entries by template id, formats template ids to the Mob IMG resource name, follows `info/link` recursively, preserves the current entry's `info` branch, and exposes missing top-level branches from linked templates to the action loaders without mutating the underlying WZ tree. | This keeps the client ownership split explicit: mob runtime state, normalized image-entry resolution, and frame/action construction are separate seams, so linked-template inheritance is no longer hidden inside the broader action-loader or runtime rows. | mob template/image-entry layer (`CActionMan::GetMobImgEntry`, `LifeLoader.cs`, `MobAnimationSet.cs`, `MobItem.cs`) |

### 3. Mob action-loader ownership discovered by targeted `CActionMan` scan



| Status | Area | Gap | Why it matters | Primary seam |

|--------|------|-----|----------------|--------------|

| Partial | Mob action-loader / frame-build parity | The simulator now has an explicit mob action-loader seam anchored to `CActionMan::LoadMobAction`: `LifeLoader` caches per-mob action templates separately from runtime combat logic, instantiates per-instance drawables from those cached action entries, carries per-frame `delay`, `head`, `lt`, `rb`, `multiRect`, plus the same origin-backed visual frame state the client loader also reads into `MobAnimationSet`, uses that cached frame metadata for live mob body bounds plus damage-number head anchoring in `MobItem`, and appends the same client-style reversed playback pass for interior frames when the mob action publishes a reverse flag through the authored action node. This pass also moved the stored multi-body lane data beyond the old union-only path: mob-vs-mob direct hits, area sweeps, and projectile-lane target selection now test against the authored per-lane body rectangles instead of only the aggregated body box, so gaps between published body lanes no longer collide as a solid union rectangle. Focused seam coverage now also exists in `UnitTest_MapSimulator/MobActionLoaderParityTests.cs` for authored origin/visual-bounds plus frame-rect, head, and `multiRect` parsing, reverse-pass duplication order, and the new lane-aware collision helpers. The remaining gap is now mostly the dedicated client-owned action index table itself: the exact `s_sMobAction` index-to-name mapping recovered from IDA is still not mirrored as an explicit simulator enum table, so action-slot ownership still falls back to authored WZ action names rather than a confirmed client index map, and the client cache may still carry rarer frame-owned fields or branch-specific consumers beyond the now-modeled origin, rect, head, reverse-pass, and multi-body lane seams. | Naming and partially implementing the `CActionMan` owner keeps mob animation parity split the same way as summons, NPCs, pets, and employees: runtime `CMob::*` logic can keep evolving independently while loader-owned frame table construction, cached reuse, authored rect math, per-lane body geometry, and reverse playback now have a concrete simulator seam instead of staying implicit inside generic mob loading. | `LifeLoader.cs`, `MobAnimationSet.cs`, `MobItem.cs`, `MobAttackSystem.cs`, `UnitTest_MapSimulator/MobActionLoaderParityTests.cs` (`CActionMan::LoadMobAction`, `CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |





### 4. Combat feedback animation-owner parity discovered by `CAnimationDisplayer` scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Damage-number and tremble animation-owner parity | The simulator already renders combat text and impact feedback, and this row keeps the recovered visible owner explicit at the `CAnimationDisplayer` seam instead of letting the registration shape disappear into generic mob-runtime or HUD code. `CAnimationDisplayer::Effect_HP` still owns number-family selection, digit composition, critical-banner insertion, temporary canvas creation, and one-time layer registration, while `CAnimationDisplayer::Effect_Tremble` owns the shake gate, start/end timing, and reduction curve. WZ inspection remains anchored to `effect/BasicEff.img/NoCri0`, `NoCri1`, and `NoCri1/effect`; this pass rechecked `NoCri1/effect` as a separate `62x57` critical canvas with origin `41,70`, revalidated `NoRed0` as the authored special-text owner for `Miss`, `guard`, `shot`, `counter`, and `resist`, confirmed `NoRed0/shot` is a taller `137x65` source with origin `54,38` that still belongs inside the recovered `57px` temporary canvas owner, and confirmed `NoBlue0` carries no special-result canvases while `NoViolet0` only partially mirrors them. IDA evidence remains `CAnimationDisplayer::Effect_HP` at `0x444eb0`, where StringPool id `0x1A15`, the red-only critical branch, `CreateCanvas(lWidth, 57)`, `lCenterTop - 47`, the two primary `InsertCanvas` phases (`400ms` hold and `600ms` fade), the separate critical layer write at `lY - 30`, layer option `0xC0050004`, priority `-1`, final option `0`, and `RegisterOneTimeAnimation` are recovered, plus `CAnimationDisplayer::Effect_Tremble` at `0x439a70` for the config/enforcement gate, start/end ticks, heavy-vs-normal durations, and reduction factors. The managed simulator path now formats the prepared WZ path, the remaining `WzDamageNumber.GetDamageString` fallback path, and the legacy `CombatEffects` font fallback through the same damage-number seam backed by `MapleStoryStringPool` id `0x1A15`; this pass removes the last ad hoc font-fallback special-text spawn so authored BasicEff names (`Miss`, `guard`, `shot`, `counter`, `resist`) are canonicalized and queued through `AddDamageNumber` instead of bypassing the owner seam. The seam still resolves special-result sprite ownership back through `NoRed0` even when the active small-digit family is blue or another non-owner set, keeps the red-only critical-owner split on spawned and helper paths, preserves the client large-first/small-tail digit mix plus the separate critical banner over `NoCri0` digits, materializes the prepared payload into a temporary composite canvas texture once at spawn, keeps that composite texture at the recovered `CreateCanvas(lWidth, 57)` height instead of expanding it to taller BasicEff special-result source canvases, releases that composite when max-active damage numbers are evicted before registration or when the managed one-time canvas layer naturally completes and returns to the pool, and builds the shared one-time canvas-layer owner with the recovered `400ms` hold, `600ms` fade, `30px` rise, `250ms` critical delay, `lCenterTop - 47` placement, and `-30px` critical-banner Y offset. `BuildOneTimeLayerRegistration` carries the prepared insert-descriptor array and prepared managed registration payload together with the recovered `CreateCanvas(lWidth, 57)` dimensions, client-shaped layer position write, animation-layer `InsertCanvas` call shape, one-time registration marker, recovered layer-registration literals (`CreateLayer` canvas value `0`, initial layer option `0xC0050004`, layer priority `-1`, final layer option `0`), and a preserved owner trace containing the StringPool `0x1A15` format id, resolved text, prepared canvas dimensions, digit or special-text source canvases, a recovered temporary-canvas operation trace (`CreateCanvas` followed by ordered source `InsertCanvas` commands), and separate critical-banner overlay provenance. `RegisterOneTimeCanvasLayer` and the shared `OneTimeCanvasLayerAnimation` accept that prepared registration payload verbatim from the damage-number seam, insert managed layers by the recovered native layer priority instead of append order, and keep the recovered owner trace attached to the live managed canvas-layer object instead of dropping it once the temporary composite leaves `DamageNumberRenderer`. Focused seam coverage in `UnitTest_MapSimulator/CombatFeedbackAnimationOwnerParityTests.cs` is now on disk for authored-name canonicalization, fallback special-text routing through the shared damage-number seam, `NoRed0` special-text owner resolution across non-owner color families, critical-banner separation, recovered one-time layer placement and literals, recovered native-operation ordering for managed one-time canvas layers, BasicEff canvas-path provenance for digit, tall special-text, and banner sources, the recovered temporary-canvas operation trace, the recovered `57px` temporary-canvas height, and tremble gate plus duration or reduction rules. The remaining gap is now narrowed to the native Gr2D execution path: the simulator preserves the recovered temporary-canvas and layer-registration call shape as managed metadata and timing/priority behavior, but still does not instantiate the client's real COM `IWzGr2DLayer` object graph, execute the recovered writes and `InsertCanvas` calls against native layer objects, or reproduce byte-for-byte blend and composite output beyond the current managed approximation. | Without an explicit `CAnimationDisplayer` combat-feedback row, combat parity can look further along than it is once mob AI, hit resolution, HP bars, and generic damage text exist. Naming the owner keeps future work anchored to the client seam that actually builds and schedules damage-number and tremble presentation instead of burying the remaining visual differences inside unrelated mob-runtime rows. | combat feedback animation layer (`CAnimationDisplayer::Effect_HP`, `CAnimationDisplayer::Effect_Tremble`, simulator seams: `CombatEffects.cs`, `DamageNumberRenderer.cs`, `ScreenEffects.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `UnitTest_MapSimulator/CombatFeedbackAnimationOwnerParityTests.cs`) |

### 5. Mob anger-gauge burst animation-owner discovered by follow-up IDA scan

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Mob anger-gauge burst animation-owner parity | The simulator keeps the visible full-charge burst on its own owner seam instead of leaving it as an inline mob overlay. `LifeLoader` records the authored mob burst UOL through the client-backed StringPool ids for `Mob/%07d.img` and `AngerGaugeEffect`, `MobItem` resolves the mob head anchor as the burst origin, and full-charge updates now register the burst through the owner-specific `AnimationEffects.AddFullChargedAngerGauge` wrapper instead of the generic attached helper or the generic per-frame mob draw path. This pass rechecked WZ first against `Mob/9400633.img`: `AngerGaugeEffect` publishes 8 authored frames at 150 ms each, `info/ChargeCount = 3`, `info/AngerGauge = 1`, and `attack4/info/specialAttack = 1` with `attackAfter = 3300`. IDA evidence then revalidated the cooldown and presentation source: `CMob::Update` copies `MobAttackInfo::tAttackAfter` from each `bSpeicalAttack` attack into `m_bFullChargeEffectTime` before calling `CMob::AngerGaugeFullChargeEffect`, and `AngerGaugeFullChargeEffect` gates replay with `m_bFullChargeEffectStartTime + m_bFullChargeEffectTime` before routing the StringPool-built UOL, `m_pvcHead`, and `m_pLayerAction` into `CAnimationDisplayer::Effect_FullChargedAngerGauge`; that displayer owner calls `LoadLayer` with the authored UOL, canvas `0`, the head origin, `rx = 0`, `ry = 0`, the action-layer overlay parent, option `0xC00614A4`, alpha `255`, `bFlip = 0`, and reserved `0`, animates the loaded layer with `GA_STOP` and missing delay/repeat variants, then registers it through `RegisterOneTimeAnimation` with delay `0` and no flip-origin layer. The simulator already carries `attackN/info/specialAttack` through `AttackInfoMetadata` / `MobAttackEntry` into `MobAI.AngerGaugeFullChargeEffectIntervalMs`, and the managed trigger path now keeps its replay gate on the recovered client seam instead of consuming the burst once per attack-state start: `MobAI.ShouldTriggerAngerGaugeFullChargeEffect` now records the last burst start tick and reapplies the owner timing only after `m_bFullChargeEffectStartTime + m_bFullChargeEffectTime`, while still waiting for the current special attack to reach its normal trigger delay before it calls the animation-displayer owner. That keeps the full-charge burst on the recovered special-attack timing path the client uses, while leaving the summed authored burst-frame delays as a fallback only for mobs that do not publish any client-backed special-attack owner timing. The attached animation-owner lane still honors one-time `zOrder` on insertion, the `_animationEffects == null` inline fallback stays removed so the burst no longer re-enters the generic mob overlay draw path, and the managed owner approximation records `FullChargedAngerGauge`, the source UOL, `GA_STOP`, the explicit `MobActionLayer` overlay-parent kind recovered from `m_pLayerAction`, recovered `LoadLayer` arguments, and the live `LoadLayer -> Animate -> RegisterOneTimeAnimation` operation sequence on the live one-shot animation while drawing through attached DX frames. This run also keeps the recovered `LoadLayer` `bFlip = 0` value on the live one-shot instead of inheriting the mob's live flip while only recording no-flip metadata. This follow-up tightened the remaining parentage metadata further so the live one-shot and its recovered native operation trace preserve the action-layer parent kind instead of a generic overlay-parent boolean only. Focused coverage in `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs` is now on disk for the StringPool-authored burst path format, live special-attack `attackAfter` precedence, visual-duration fallback, fallback cadence gating, recovered replay cooldown timing, recovered `LoadLayer` / `GA_STOP` / `RegisterOneTimeAnimation` metadata, source UOL retention, action-layer overlay-parent kind retention, owner-specific one-shot registration, and the recovered no-flip `LoadLayer` draw lane. | Rage-boss parity is closer because the visible full-charge burst is now tracked as its own mob-to-animation owner with an explicit authored path, head-anchor origin, owner-specific one-time registration point, recovered `GA_STOP`, `LoadLayer`, no-flip layer argument, registration, action-layer parent metadata, and the recovered `m_bFullChargeEffectStartTime + m_bFullChargeEffectTime` replay gate instead of a simulator-only per-attack consumption rule. The remaining gap is now limited to native presentation mechanics: the simulator still approximates `CAnimationDisplayer::LoadLayer` / `GA_STOP` / `RegisterOneTimeAnimation` through attached DX frames and managed owner metadata, including the recovered `MobActionLayer` parent kind, but does not recreate the real COM `IWzGr2DLayer` parent object under the mob action layer, exact native COM lifetime, or byte-for-byte blend output. | mob anger-gauge presentation layer (`CMob::AngerGaugeFullChargeEffect`, `CAnimationDisplayer::Effect_FullChargedAngerGauge`, `CAnimationDisplayer::LoadLayer`, `CAnimationDisplayer::RegisterOneTimeAnimation`, `Mob/9400633.img/AngerGaugeEffect`, `Mob/9400633.img/attack4/info/specialAttack`, `AnimationEffects.cs`, `MobItem.cs`, `UnitTest_MapSimulator/MobAngerGaugeBurstParityTests.cs`) |

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


