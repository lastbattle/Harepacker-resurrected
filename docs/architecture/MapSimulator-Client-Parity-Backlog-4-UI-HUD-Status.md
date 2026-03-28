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
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present, direct melee attacks now use the same delayed action scheduler as projectile and ground-warning families using WZ `attackAfter` timing instead of pure frame polling, mob skills now route their non-summon `MobSkill.img` `effect` and `mob` branches through the runtime animation layer so mob/target/screen-position skill cues no longer silently no-op, target-position mob skill visuals now anchor to the mob's actual live target instead of always snapping to the player when the mob is casting onto a summon or another mob, summon skill `200` still resolves its live summon list and limit from `MobSkill.img` so temporary summons enter the field through the mob pool instead of being skipped, common mob self or party buff skills now read `MobSkill.img` `x`/`time`/`lt`/`rb` data to apply runtime `PowerUp`, `MagicUp`, `PGuardUp`, `MGuardUp`, and `Speed` temp stats to the caster or nearby mobs instead of remaining visual-only, `MobSkill.img` skill `114` now uses the same WZ runtime area and `x` value to heal nearby mobs instead of staying cosmetic, common player-targeted mob debuffs now also read `MobSkill.img` `time` / `interval` / `prop` / `x` / `lt` / `rb` data so seal, darkness, weakness, stun or freeze, poison, slow, and dispel skills apply runtime local-player consequences instead of stopping at visuals, with darkness feeding player miss chance, weakness suppressing jumps, stun or freeze locking movement and skill use, poison ticking direct damage, slow scaling live move speed, and dispel clearing active buffs, while the rarer player-facing debuff family now also routes through the same runtime path instead of staying blocked by simulator heuristics: authored `MobSkill.img` `lt`/`rb` ranges now feed autonomous skill selection so wide-area curse, seduce, reverse-input, undead, and banish skills can fire from their published reach instead of a fixed short-range fallback, those same authored rectangles now mirror the mob's live facing when the runtime applies nearby or player-targeted mob skills, banish skill `129` now applies its runtime banish lockout after teleporting to spawn instead of short-circuiting at teleport only, and undead skill `133` now reflects recovered HP using the authored `MobSkill.img/x` percentage instead of always inverting heals at a hardcoded full amount. Chained post-action combat decisions no longer incorrectly stall when the mob is currently targeting a summon or another mob instead of the player, and autonomous mob skill selection now honors WZ `priority`, `preSkillIndex` / `preSkillCount`, `onlyFsm`, and `skillForbid` so scripted-only skills stop leaking into the generic loop, charged follow-up skills wait for their prerequisite casts, and skill lockout windows stop immediately chaining into unrelated skills | Good baseline, but the loop is still simplified vs client `CMob` behavior because the remaining gap is now concentrated in narrower branch-specific aftermath and control semantics beyond these WZ-wired debuff applications, especially tighter client behavior for seduce pathing and confusion or reverse-input edge cases, curse side rules beyond the current HP/MP cap clamp, and any remaining uncommon player-facing mob-skill transitions that still need deeper client matching | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `CombatEffects.cs` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, `tremble`, and `type` attack metadata through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, triggers tremble feedback from flagged impacts, preserves puppet/summon aggro targets through AI updates, routes delayed mob hits to registered puppet targets instead of always falling back to the local player, now syncs targeted summons through their live runtime animation bounds instead of a fixed fallback box so summon-targeted mob attacks collide against the same world-space body rect the simulator draws, removes consumed summons from the active summon list when that branch lands, preserves summon slot indices alongside live summon object ids so queued summon-targeted attacks can still resolve through the client-style slot lane if the original summon instance is replaced before impact, preserves external mob targets through AI updates so delayed mob attacks can resolve `MobTargetType.Mob` entries against live target mob body rects and apply mob-vs-mob damage instead of silently dropping that client branch, now mirrors the client `CMob::ProcessAttack` rect pass for delayed direct or area hits by colliding non-targeted attack rectangles against every live summon and mob body in range instead of only the explicitly tracked target, lets explicitly targeted summon or mob projectiles apply their delayed impact when they reach the scheduled destination instead of depending only on the projectile transit hitbox, now also gives untargeted non-ground projectile impacts the same late rect sweep against the local player, live summons, and encounter mobs when the ball reaches its destination instead of dropping those arrival-time collateral hits, routes WZ `type = 1` delayed direct or ground attacks through the client-style locked-target branch so queued player, summon, or mob targets resolve on impact without depending on a late rectangle overlap test, matches the client `CMob::DoAttack` admission gate more closely by checking type-1 queue eligibility from the authored attack origin to a clamped target-body point within `range + 10` instead of the older broad rectangle overlap heuristic, still gates delayed `jumpAttack` hits on grounded player or summon targets instead of damaging airborne targets on overlap alone, now parses WZ projectile `range/sp` + `range/r` launch-origin metadata, uses that authored origin for projectile spawn and type-1 admission checks, carries WZ `info/attack/*/attackCount` into live ranged attack entries, fans multi-ball projectiles across authored projectile lanes by choosing per-lane destinations against live player, summon, or encounter-mob bodies when those entities line up with the lane instead of cloning every extra ball off a fixed synthetic spread from the primary target, and now reuses WZ-authored projectile and hit frame bounds for in-flight and arrival-time collision checks instead of a fixed synthetic `16x16` box | `CMob::DoAttack` / `ProcessAttack` are closer on `MobBullet` setup now that the simulator uses authored launch points plus lane-aware multiball destination selection and WZ-sized projectile-impact bounds, but the row still has a narrower projectile-container gap: the simulator still synthesizes the client/server `m_aMultiTargetForBall` packet list from live lane heuristics rather than true server-fed lane points, does not yet mirror the last `MobBullet` side effects such as exact travel-shape / arc resolution from `SetBallDestPoint`, and still approximates some lane ordering or duplication edge cases when multiple bullets target the same body | `MobAI.cs`, `MobAttackSystem.cs`, `MobPool.cs`, `MapSimulator.cs` (`CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |
| Partial | Full mob stat/status parity | Mob statuses now keep concrete temporary-stat entries with expiry metadata, tick values, reset events, and authored auxiliary values, poison/venom/burn deal runtime DoT damage, freeze and stun hold mobs in their incapacitated state until expiry, active temp stats now modify outgoing damage, incoming damage, chase speed, and player-vs-mob hit chance, active status flags tint affected mobs in-world for visible feedback across the more common debuff, immunity, counter, and reward-mark states, player skill hits now infer and apply a broader WZ-backed slice of mob debuffs from loaded skill metadata for poison/venom, burn, freeze, stun, seal, blindness, darkness, slow, weakness, signed `EVA`, `amplifyDamage`, `elementalWeaken`, `mindControl`, `polymorph`, and `incapacitate`, WZ-backed debuff skills such as `1201006` Threaten now drive signed mob attack/defense/accuracy reductions from their loaded stat fields instead of collapsing to damage-only hits while the shared stat-driven debuff lane also now materializes signed mob `EVA` shifts whenever the loaded skill level actually carries that authored stat, the mob-skill self-buff lane now materializes the `MobSkill.img` defense and counter family by applying WZ-backed `140` physical immunity, `141` magic immunity, `142` hard-skin damage reduction, and `143`/`144`/`145` weapon or magic counter states as live mob temp stats instead of leaving those casts visual-only, `MobSkill.img/146` now genuinely resolves its authored `targetMobType` from WZ and clears active negative mob temp stats from self or nearby mobs through the shared status-target area seam instead of remaining a dead mapping, and the later WZ-backed Monster Carnival guardian self-buff slice from `MCGuardian.img` / `MobSkill.img` now also materializes `150` Power Up, `151` Guard Up, `152` Magic Up, `153` Shield Up, `154` Accuracy Up, `155` Avoidability Up, and `156` Speed Up as live mob temp stats instead of remaining ignored. The shared player-originated damage path now carries physical-vs-magic hit type into mob damage resolution so magic-side reductions and immunity actually gate magic attacks, reflected counter damage now feeds back into the local player from the authored `MobSkill.img` reflect percent and per-hit cap fields, the simulator now also materializes `MobSkill.img/157` as its own timed mob temp stat instead of dropping that WZ-authored cast on the floor, and skill `info/mes` tokens still drive simulator-side debuff semantics like `buffLimit`, `attackLimit`, `restrict`, `Showdown`, `mindControl`, `polymorph`, `amplifyDamage`, and `elementalWeaken` so mob buffs cancel, later mob skill casts can be blocked, bind-style debuffs hold mobs in stun state, web-style roots fully stop chase movement, hypnotized mobs retarget nearby mobs, doomed mobs lose combat actions while slowed, `Ambush` or `Neutralise` damage-vulnerability states resolve from WZ `x/time/prop` data, `Showdown` remains its own WZ-backed reward bonus instead of damage taken, and the simulator's synthetic meso payout lane still reads that stored bonus when marked mobs die while mob-vs-player hit resolution treats `Blind` as a forced miss and also uses the player's total avoidability plus negative `ACC` or positive/negative `EVA` temp stats to shift live hit or miss chances instead of leaving those status interactions cosmetic | Status-heavy encounters are more legible and more WZ-authored debuff or self-buff skills now push real mob temp stats instead of only dealing damage, the common mob-cast defense buffs no longer stop at visuals for the physical-immunity, magic-immunity, hard-skin, counter-attack, debuff-cleansing, Monster Carnival guardian self-buff slice, or the special `MobSkill.img/157` timed status, and the authored `MobSkill.img/146` dispel lane now really follows WZ `targetMobType` for self-vs-nearby cleansing instead of being missing code behind backlog text. The remaining parity gap is narrower but still includes the exact client-side behavior attached to uncommon statuses like skill `157` and other rarer boss-only states beyond merely keeping them live as temp stats, true transformed-doom visuals or collision behavior, full client reward side effects for `Showdown`-style marks beyond the simulator's synthetic meso-drop lane, and tighter client distinctions between full roots, slows, hypnosis target rules, and other special-case debuffs | `MobAI.cs`, `SkillManager.cs`, `PlayerCombat.cs`, `MapSimulator.cs`, effect loaders |
| Partial | Special mob interactions | WZ `selfDestruction` mobs now trigger reserved bomb/time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, generic WZ `info/removeAfter` encounter mobs now expire through the simulator `Timeout` death lane instead of living forever, escort and WZ `info/damagedByMob` encounter mobs no longer aggro the player, that `damagedByMob` lane now flows through a dedicated parsed mob-data flag instead of only the older simulator `Friendly` alias, escort, `damagedByMob`, and generic `removeAfter` encounter spawn points no longer auto-respawn through the normal mob-pool loop, special-death, escort, and `damagedByMob` encounter kills now suppress reward drops, WZ `info/damagedByMob` encounter mobs now ignore player-originated basic, skill, projectile, and summon damage, hostile mobs now retarget live escort or `damagedByMob` encounter actors onto the simulator's `MobTargetType.Mob` lane instead of staying player-only, and that lane now treats only live encounter participants as valid mob-vs-mob damage recipients so delayed area or projectile splash from encounter combat stops leaking onto unrelated ordinary mobs. Escort follow now respects live `life.info` progression by only attaching the lowest active escort index until that stage is cleared, Wild Hunter swallow routing now holds `33101005` targets in a stunned digest state with repeated wriggle hit pulses before resolving the `33101006` follow-up buff instead of immediately killing and buffing on first contact, and Astaroth-style rage bosses now parse WZ `ChargeCount` plus `AngerGauge` data, reserve `attack/info/AngerAttack` entries until the charge count fills, consume that gauge on the flagged attack, and render the mob-local anger gauge plus burst effect from the dedicated WZ animation nodes; focused unit coverage now also locks the generic reward-suppression and encounter no-respawn seams in place, adds direct coverage for HP-threshold self-destruction reservation, verifies the anger-gauge charge-and-consume seam against the WZ-backed attack reservation model, and now also covers the WZ parse boundary for `info/damagedByMob`, `info/removeAfter`, `info/selfDestruction`, `info/ChargeCount`, `info/AngerGauge`, and `attack/info/AngerAttack` so those encounter and rage rules are no longer protected only by hand-authored runtime fixtures | This row's local WZ-backed simulator behavior is now mostly wired and covered, so the remaining gap is narrower and more script or server owned: broader scripted rage or boss-phase logic beyond the Astaroth-style charge lane, exact Wild Hunter swallow success or failure branching and packet choreography outside the current digest path, deeper map-specific escort scripting, and broader full-simulation coverage for encounter retargeting or escort progression across complete map flows are still missing | `MobItem.cs`, `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, field systems |


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
