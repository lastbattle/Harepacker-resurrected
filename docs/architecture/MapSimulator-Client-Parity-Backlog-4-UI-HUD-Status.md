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
| Partial | Local mob combat loop | Attack patterns, cooldowns, targeting helpers, knockback, damage numbers, hit effects, and drop spawning are present, direct melee attacks now use the same delayed action scheduler as projectile and ground-warning families using WZ `attackAfter` timing instead of pure frame polling, mob skills now route their non-summon `MobSkill.img` `effect` and `mob` branches through the runtime animation layer so mob/target/screen-position skill cues no longer silently no-op, summon skill `200` still resolves its live summon list and limit from `MobSkill.img` so temporary summons enter the field through the mob pool instead of being skipped, common mob self or party buff skills now read `MobSkill.img` `x`/`time`/`lt`/`rb` data to apply runtime `PowerUp`, `MagicUp`, `PGuardUp`, `MGuardUp`, and `Speed` temp stats to the caster or nearby mobs instead of remaining visual-only, `MobSkill.img` skill `114` now uses the same WZ runtime area and `x` value to heal nearby mobs instead of staying cosmetic, common player-targeted mob debuffs now also read `MobSkill.img` `time` / `interval` / `prop` / `x` / `lt` / `rb` data so seal, darkness, weakness, stun or freeze, poison, slow, and dispel skills apply runtime local-player consequences instead of stopping at visuals, with darkness feeding player miss chance, weakness suppressing jumps, stun or freeze locking movement and skill use, poison ticking direct damage, slow scaling live move speed, and dispel clearing active buffs, chained post-action combat decisions no longer incorrectly stall when the mob is currently targeting a summon or another mob instead of the player, and autonomous mob skill selection now honors WZ `priority`, `preSkillIndex` / `preSkillCount`, `onlyFsm`, and `skillForbid` so scripted-only skills stop leaking into the generic loop, charged follow-up skills wait for their prerequisite casts, and skill lockout windows stop immediately chaining into unrelated skills | Good baseline, but the loop is still simplified vs client `CMob` behavior because the remaining player-facing mob-skill branches such as curse-style stat distortion, undead heal inversion, seduce or reverse-input control hijacks, banish-style outcomes, and other rarer branch-specific transition rules beyond the newly wired WZ skill-order and common debuff metadata are still missing | `MobAI.cs`, `MobPool.cs`, `MobAttackSystem.cs`, `MapSimulator.cs`, `CombatEffects.cs` |
| Implemented | HP indicators | Regular mob HP bars and boss HP bars are implemented, with combat damage text and boss HP UI integrated | Indicator baseline is present in simulator; advanced client-only indicator semantics remain a separate follow-up item | `CombatEffects.cs`, `BossHPBarUI.cs` |
| Partial | Full `CMob` action pipeline | The runtime now carries WZ `rush`, `jumpAttack`, `tremble`, and `type` attack metadata through the loader and live attack entries, allows rush/jump movement during flagged attack states, recomputes direct-hit rectangles from the mob's live position when the delayed attack entry fires, triggers tremble feedback from flagged impacts, preserves puppet/summon aggro targets through AI updates, routes delayed mob hits to registered puppet targets instead of always falling back to the local player, now syncs targeted summons through their live runtime animation bounds instead of a fixed fallback box so summon-targeted mob attacks collide against the same world-space body rect the simulator draws, removes consumed summons from the active summon list when that branch lands, preserves summon slot indices alongside live summon object ids so queued summon-targeted attacks can still resolve through the client-style slot lane if the original summon instance is replaced before impact, preserves external mob targets through AI updates so delayed mob attacks can resolve `MobTargetType.Mob` entries against live target mob body rects and apply mob-vs-mob damage instead of silently dropping that client branch, now mirrors the client `CMob::ProcessAttack` rect pass for delayed direct or area hits by colliding non-targeted attack rectangles against every live summon and mob body in range instead of only the explicitly tracked target, lets explicitly targeted summon or mob projectiles apply their delayed impact when they reach the scheduled destination instead of depending only on the projectile transit hitbox, now also routes WZ `type = 1` delayed direct or ground attacks through the client-style locked-target branch so queued player, summon, or mob targets resolve on impact without depending on a late rectangle overlap test, matches the client `CMob::DoAttack` admission gate more closely by checking type-1 queue eligibility from the authored attack origin to a clamped target-body point within `range + 10` instead of the older broad rectangle overlap heuristic, and still gates delayed `jumpAttack` hits on grounded player or summon targets instead of damaging airborne targets on overlap alone | Attack timing, target locking, and type-1 admission are closer to the client, but `CMob::DoAttack` / `ProcessAttack` still diverge on full `MobBullet` container behavior for multi-target projectile lanes and a smaller set of remaining bullet-container side effects outside the now-wired summon-slot, mob-target, and direct, ground, or projectile target-lock collision paths | `MobAI.cs`, `MobAttackSystem.cs`, `MobPool.cs`, `MapSimulator.cs` (`CMob::DoAttack`, `CMob::OnNextAttack`, `CMob::ProcessAttack`, `CMob::GetAttackInfo`) |
| Partial | Full mob stat/status parity | Mob statuses now keep concrete temporary-stat entries with expiry metadata, tick values, reset events, and authored auxiliary values, poison/venom/burn deal runtime DoT damage, freeze and stun hold mobs in their incapacitated state until expiry, active temp stats now modify outgoing damage, incoming damage, chase speed, and player-vs-mob hit chance, active status flags tint affected mobs in-world for visible feedback across the more common debuff, immunity, counter, and reward-mark states, player skill hits now infer and apply a broader WZ-backed slice of mob debuffs from loaded skill metadata for poison/venom, burn, freeze, stun, seal, blindness, darkness, slow, weakness, signed `EVA`, `amplifyDamage`, `elementalWeaken`, `mindControl`, `polymorph`, and `incapacitate`, WZ-backed debuff skills such as `1201006` Threaten now drive signed mob attack/defense/accuracy/evasion reductions from their loaded stat fields instead of collapsing to damage-only hits, the mob-skill self-buff lane now materializes the `MobSkill.img` defense and counter family by applying WZ-backed `140` physical immunity, `141` magic immunity, `142` hard-skin damage reduction, and `143`/`144`/`145` weapon or magic counter states as live mob temp stats instead of leaving those casts visual-only, `MobSkill.img/146` now uses its authored `targetMobType` plus area data to clear active negative mob temp stats from self or nearby mobs instead of remaining a visual-only cast, the shared player-originated damage path now carries physical-vs-magic hit type into mob damage resolution so magic-side reductions and immunity actually gate magic attacks, and reflected counter damage now feeds back into the local player from the authored `MobSkill.img` reflect percent and per-hit cap fields while the simulator also reads skill `info/mes` tokens so client-authored debuff semantics like `buffLimit`, `attackLimit`, `restrict`, `Showdown`, `mindControl`, `polymorph`, `amplifyDamage`, and `elementalWeaken` can cancel active mob buffs, block later mob skill casts, hold bundled bind-style debuffs in stun state, root mobs via full web slow, reroute hypnotized mobs onto nearby mob targets, suppress doomed mob combat actions while slowing them, feed `Ambush` or `Neutralise` style damage-vulnerability states from WZ `x/time/prop` data, store `Showdown` as its own WZ-backed reward bonus instead of damage taken, and boost the simulator's synthetic meso payout lane from that stored bonus when marked mobs die while mob-vs-player hit resolution treats `Blind` as a forced miss and now also uses the player's total avoidability plus negative `ACC` or positive/negative `EVA` temp stats to shift live hit or miss chances instead of leaving those status interactions cosmetic | Status-heavy encounters are more legible and more WZ-authored debuff or self-buff skills now push real mob temp stats instead of only dealing damage, the common mob-cast defense buffs no longer stop at visuals for the physical-immunity, magic-immunity, hard-skin, counter-attack, and debuff-cleansing slice, and magic-vs-physical incoming damage distinctions now actually matter for those statuses while skill-authored evasion shifts no longer stay cosmetic. The remaining parity gap is narrower but still includes the rest of the uncommon mob-stat surface beyond the now-wired `140`-`146` family, true transformed-doom visuals or collision behavior, full client reward side effects for `Showdown`-style marks beyond the simulator's synthetic meso-drop lane, and tighter client distinctions between full roots, slows, hypnosis target rules, and other special-case debuffs | `MobAI.cs`, `SkillManager.cs`, `PlayerCombat.cs`, `MapSimulator.cs`, effect loaders |
| Partial | Special mob interactions | WZ `selfDestruction` mobs now trigger reserved bomb/time-bomb actions from HP thresholds or `removeAfter` countdowns, finish through the simulator's special bomb death path, generic WZ `info/removeAfter` encounter mobs now expire through the simulator `Timeout` death lane instead of living forever, escort and WZ `info/damagedByMob` encounter mobs no longer aggro the player, that `damagedByMob` lane now flows through a dedicated parsed mob-data flag instead of only the older simulator `Friendly` alias, escort spawn points do not auto-respawn, special-death, escort, and `damagedByMob` encounter kills now suppress reward drops, WZ `info/damagedByMob` encounter mobs now ignore player-originated basic, skill, projectile, and summon damage, hostile mobs now retarget live escort or `damagedByMob` encounter actors onto the simulator's `MobTargetType.Mob` lane instead of staying player-only, that lane is now limited to encounter participants so ordinary mobs do not splash unrelated mobs during delayed area attacks, escort follow now respects live `life.info` progression by only attaching the lowest active escort index until that stage is cleared, Wild Hunter swallow routing now holds `33101005` targets in a stunned digest state with repeated wriggle hit pulses before resolving the `33101006` follow-up buff instead of immediately killing and buffing on first contact, and Astaroth-style rage bosses now parse WZ `ChargeCount` plus `AngerGauge` data, reserve `attack/info/AngerAttack` entries until the charge count fills, consume that gauge on the flagged attack, and render the mob-local anger gauge plus burst effect from the dedicated WZ animation nodes; focused unit coverage now also locks the generic reward-suppression, escort-index gating, timeout expiry, and reserved self-destruction action seams in place | This row now has its generic WZ-backed encounter rules covered in both runtime and targeted tests, so the remaining gap is narrower: broader scripted rage or boss-phase logic beyond the Astaroth-style charge lane, exact server-owned Wild Hunter swallow outcome branching and packet choreography outside the current digest path, and deeper map-specific escort scripting are still missing | `MobItem.cs`, `MobAI.cs`, `MobPool.cs`, `MapSimulator.cs`, field systems |


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
