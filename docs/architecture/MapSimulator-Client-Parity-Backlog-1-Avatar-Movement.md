# MapSimulator Client Parity Backlog

## Purpose

This document is the single source of truth for MapSimulator parity work against the MapleStory client.

It does three things that the old notes did not do well:

1. Separates shipped work from missing work.
2. Distinguishes broad simulator features from player/avatar parity.
3. Anchors claims to actual code seams and, where relevant, client-side reverse engineering.

## Evidence Used

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
- `CUserPool::OnPacket` at `0x94ddf0`
- `CUserPool::Update` at `0x94c370`
- `CUserRemote::Update` at `0x955d50`
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

### 1. Avatar and player parity

- `CUser::LoadLayer` at `0x8e96d0`
- `CUser::PrepareActionLayer` at `0x8e3070`
- `CUser::SetMoveAction` at `0x8df540`
- `CUser::Update` at `0x8fb8d0`
- `CUser::UpdateMoreWildEffect` at `0x8fb4c0`
- `CUser::SetActivePet` at `0x8e40e0`
- `CUserPool::OnPacket` at `0x94ddf0`
- `CUserPool::Update` at `0x94c370`
- `CUserRemote::Update` at `0x955d50`
- `CUserLocal::SetMoveAction` at `0x903ce0`
- `CUserLocal::Update` at `0x937330`

Notes:
`CUser::LoadLayer` and `CUser::PrepareActionLayer` are the clearest resolved avatar-composition seams for layer loading and action-layer setup. `CUser::Update` and `CUser::UpdateMoreWildEffect` also show that the client maintains several additional avatar/effect layers outside the base assembler path, while `CUserLocal::Update` contains the local-player emotion reset path and action/state transitions that matter for expression and rare-action parity. A later IDA pass also confirms that remote avatar ownership is a first-class client surface rather than a loose extension of local-player logic: `CUserPool::OnPacket` dispatches enter, leave, common, remote, and local user packet families, `CUserPool::Update` advances local plus remote users and shared social-effect state, and `CUserRemote::Update` owns remote move-action, effect, portable-chair, and prepared-skill presentation.

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

### 1. Avatar and player parity

These are the biggest remaining gaps for visible client parity.

| Status | Area | Gap | Why it matters | Primary seam |
|--------|------|-----|----------------|--------------|
| Partial | Default avatar selection | The simulator still keeps the deterministic, WZ-verified SuperGM/Wizet fallback selection of `1002140` (Wizet Invincible Hat), `1042003` (Wizet Plain Suit), `1062007` (Wizet Plain Suit Pants), `1072005` (Leather Sandals), `1092008` (Pan Lid), and `1322013` (Wizet Secret Agent Suitcase) with matching face, hair, level, and SuperGM job metadata, but login-side avatar ownership is now materially closer to the client: `AvatarLook` decode still validates gender, skin, face, hair, visible equip bodyparts, hidden equip lists, pet ids, and applies the packet-owned `weaponStickerID` visual through the normal weapon render seam, while the login packet seam can ingest packet-authored `SelectWorldResult`, `ViewAllCharResult`, and `ExtraCharInfoResult` payloads supplied as hex or Base64 through `/loginpacket` or the inbox. `SelectWorldResult` and `ViewAllCharResult` now also preserve the client-owned rank block more faithfully by decoding `worldRank`, `worldRankMove`, `jobRank`, and `jobRankMove` from the `GW_CharacterStat -> AvatarLook -> family/rank -> slot metadata` sequence confirmed from `CLogin::OnSelectWorldResult` / `CLogin::OnViewAllCharResult`, carrying those move deltas through the roster owner, and rebuilding the character-detail previous-rank arrows from the packet-authored gap instead of flattening every packet entry to a same-rank glyph. `ExtraCharInfoResult` continues to decode the client-owned `accountId -> flag` payload confirmed from `CLogin::OnExtraCharInfoResult` so the login shell can track extra-slot eligibility alongside that roster state. The remaining gap is narrower: the simulator still does not consume live encrypted login traffic directly, still does not mirror the full live multi-packet login stream beyond manually injected payloads, and still does not apply the rest of the broader packet surface such as packet-driven character-creation randomization end to end | Default avatars and login previews are no longer owned only by direct preset construction or simulator-authored roster clones: the same `AvatarLook` seam now accepts decoded `SelectWorldResult` and `ViewAllCharResult` roster payloads carrying `GW_CharacterStat` metadata, avatar bytes, packet-owned weapon-sticker visuals, and rank-gap deltas, which is materially closer to the client's `OnSelectWorldResult` / `OnViewAllCharResult` -> `GW_CharacterStat::Decode` -> `AvatarLook::Decode` ownership for both preview assembly and selected-character field entry | `CharacterLoader.LoadDefaultMale`, `CharacterLoader.LoadDefaultFemale`, `CharacterLoader.LoadFromAvatarLook`, `CharacterAssembler`, `LoginAvatarLookCodec`, `LoginSelectWorldResultCodec`, `LoginViewAllCharResultCodec`, `LoginExtraCharInfoResultCodec`, `MapSimulator.InitializeLoginCharacterRoster`, `MapSimulator.TryConfigureLoginPacketPayload`, `LoginCharacterRosterEntry.CreateRuntimeBuild`, `CharacterConfigManager.CreateDefaultMalePreset`, `CharacterConfigManager.CreateDefaultFemalePreset` (`CUser::LoadLayer`, `CUIAvatar::CUIAvatar`, `CLogin::OnSelectWorldResult`, `CLogin::OnViewAllCharResult`, `CLogin::OnExtraCharInfoResult`, `GW_CharacterStat::Decode`, `AvatarLook::Decode`) |
| Implemented | Action coverage | Character-part loading now preserves the existing core action order and appends additional WZ action nodes discovered on each part instead of stopping at the old fixed lists | Uncommon body/equipment poses can now load when the source IMG exposes them, which closes the loader-side empty-state gap for rare, scripted, and death-adjacent actions | `CharacterLoader.StandardActions`, `CharacterLoader.AttackActions` (`CUser::PrepareActionLayer`, `CUser::SetMoveAction`, `CUserLocal::SetMoveAction`) |
| Implemented | Death / rare action frames | Runtime action selection now carries raw one-shot action names through to `CharacterAssembler` instead of forcing uncommon actions back through the limited enum-only render path | Loaded death and other rare WZ actions can now survive from selection to final frame assembly instead of degrading immediately to generic fallbacks | `PlayerCharacter`, `CharacterLoader`, `CharacterAssembler` |
| Implemented | Facial expression behavior | The local player runtime now owns blink cadence and short-lived hit expressions, and updates the assembler expression state when those transitions occur | The avatar now reacts over time instead of staying pinned to the static default face | `CharacterAssembler.GetFaceFrame`, `CharacterLoader.LoadFaceExpressions`, `PlayerCharacter` (`CUserLocal::Update` emotion reset path) |
| Implemented | Equip anchor fallback rules | Equipment alignment now resolves through explicit shared map-point precedence instead of using literal pixel nudges when preferred anchors are missing | Parts stay render-safe while remaining tied to WZ anchor data rather than ad hoc offsets | `CharacterAssembler.CalculateEquipOffset`, `CharacterAssembler.TryCalculateHeadEquipOffset` |
| Implemented | Client-verified fallback order | Action lookup now prefers exact action names, then raw-action family reductions, then swim/fly aliasing, before the final `stand1` escape hatch | Character assembly now follows a documented action-family precedence order instead of jumping straight from miss to generic standing frames | `CharacterAssembler`, `CharacterPart.FindAnimation` (`CUser::LoadLayer`, `CUser::PrepareActionLayer`, `CUser::SetMoveAction`) |
| Partial | Morph avatar pipeline parity | MapSimulator now resolves `common/morph` template ids into `Morph/*.img`, loads those images as a dedicated avatar render owner, resolves UOL-linked morph frames, preserves direct per-frame anchors like `head`, and switches buff-driven transforms onto morph-specific action playback through the existing assembler/runtime timing path instead of leaving morphs as UI-only suppression metadata. The loader now also backfills missing morph animation branches through same-family base IMG fallback (`id -> id0 -> id00 -> id000`) so partial morph sets can inherit broader action coverage instead of hard failing on the exact IMG only, and hostile polymorph mob skills that publish a concrete morph template id in WZ now route through the same avatar-owner swap seam: `MobSkill.img/172/level/1/x = 2` resolves onto `Morph/0002.img`, while `MobSkill.img/173/level/1/x = 1000` resolves onto `Morph/1000.img` for the duration of the debuff instead of remaining status-only metadata. Remaining gap: the full client-side morph inheritance table and lookup behavior in `CActionMan::GetMorphImgEntry` is still not mirrored beyond this family-base fallback, and other hostile transform sources without an explicit WZ morph id are still unresolved. | Morph-heavy jobs and the confirmed hostile morph mob-skill pair now render from the same dedicated morph-owner path rather than ordinary body/equip assembly or status-only metadata, which closes the main parity hole around morph frame ownership, action timing, and non-buff transform visibility. The remaining work is the narrower client lookup layer for broader inherited morph templates plus any hostile transform source that does not expose a direct WZ morph template id. | avatar transform/runtime assembly (`CActionMan::GetMorphImgEntry`, `CActionMan::LoadMorphAction`, `MobSkill.img/172/level/1/x`, `MobSkill.img/173/level/1/x`) |
| Partial | Mount / vehicle / mechanic mode rendering | `TamingMob` assembly now preserves exact ride frames for normal mounts, falls back to seated passenger variants when the asset only exposes `sit`, equips ride parts for skills that publish an `eventTamingMob` mount id, routes mechanic vehicle skills onto the shared ride layer while keeping that mount owned across both direct and timer-driven tank-to-siege return transitions, treats the shared Mechanic mount (`Character/TamingMob/01932016`) as the frame-timing and visibility owner for vehicle-only actions such as `tank_*`, `siege_*`, `flamethrower*`, `rbooster*`, `tank_laser`, and other mount-owned mechanic actions confirmed in WZ, now auto-equips that shared Mechanic ride whenever a Mechanic skill窶冱 `action`, `prepare`, `keydown`, or `keydownend` node maps onto an exact or `tank_`-prefixed mount animation, and now also mirrors the client窶冱 explicit vehicle enter/exit ownership surface more closely by auto-detecting non-skill `TamingMob` equip or unequip changes, playing WZ-backed `ride2` / `getoff2` transitions when the active mount exposes them, and preserving the departing mount as a transient render owner so `getoff2` can complete even after the equipment slot has already been cleared; the remaining gap is that the simulator still does not consume the client窶冱 full move-action and update-owned vehicle state catalog beyond those local ownership transitions | Event ride buffs, mechanic transforms, direct mechanic vehicle-action skills, additional WZ-backed mechanic actions like `alert3` and tank-prefixed summon or rush variants, and now non-skill mount equip or teardown swaps that expose `ride2` / `getoff2` all transition the avatar onto the real `TamingMob` asset instead of leaving the player visually unmounted, avoid dropping back to the previous ride too early when siege sustain expires on the same tick as buff cleanup, stop double-rendering the base avatar under full-body Mechanic vehicle sprites, keep tank-mode summon or rush casts on the mount-specific action families the shared mechanic ride actually exposes, and preserve the outgoing mount through `getoff2` so explicit vehicle dismount animations do not vanish when the slot clears first; however, broader client-managed vehicle state sources beyond these detected ownership transitions are still incomplete | `PlayerCharacter`, `CharacterLoader`, `CharacterAssembler`, `SkillLoader`, `SkillManager` (`CAvatar::MoveAction2RawAction`, `CUser::Update`, `Character/TamingMob/01932016`, `Character/TamingMob/01932016/{ride2,getoff2}`, `Skill/3500.img/35001004/action/0`, `Skill/3510.img/35101003/action/0`, `Skill/3510.img/35101005/action/0`, `Skill/3510.img/35101006/action/0`, `Skill/3511.img/35111015/action/0`, `Skill/3512.img/35121012/action/0`, `Skill/3511.img/35111013/action/0`, `Skill/3512.img/35121007/action/0`) |
| Partial | Skill-specific avatar transforms | Mechanic-mode skills now activate dedicated avatar action families (`tank_*`, `siege_*`, `tank_siege*`) from `action/0` nodes, prefer transform-specific jump / ladder / rope / fly / swim / hit branches when those movement variants exist on the avatar, play the corresponding `_after` exit actions when those transforms clear, the prepare-skill flamethrower family now stays on its `flamethrower` / `flamethrower2` avatar branches until release triggers `flamethrower_after` / `flamethrower_after2`, Rocket Booster now transitions from its one-shot cast into the dedicated airborne `rbooster` avatar branch while preserving the mechanic tank-specific `tank_rbooster_pre` / `tank_rbooster_after` avatar variants when the booster is fired out of an active tank transform, Cyclone now promotes its `cyclone_pre` cast into the persistent `cyclone` avatar branch until buff expiry triggers `cyclone_after`, `Monkey Wave` now promotes `noiseWave_pre` into the sustained `noiseWave_ing` avatar branch before release plays the body `noiseWave` action from the WZ `action/0` node, Wild Hunter `Swallow` now holds the body on `swallow_loop` from `prepare/action` until release clears into `swallow`, Wild Hunter `More Wild` now keeps the body on the dedicated `wildbeast` avatar branch for the full buff window exposed by `Skill/3312.img/33121006/action/0`, Evan dragon-breath charge skills now hold `icebreathe_prepare` / `breathe_prepare` until release resolves onto `dragonIceBreathe` / `dragonBreathe`, Dual Blade charge skills `Final Cut` and `Monster Bomb` now keep the avatar on `finalCutPrepare` / `monsterBombPrepare` for the hold window before release resolves onto `finalCut` / `monsterBombThrow`, and Resistance `Dual Vulcan` now promotes `dualVulcanPrep` into the sustained `dualVulcanLoop` avatar branch until release triggers `dualVulcanEnd`; the remaining gap is the rest of the class-specific transform and charge families that still need WZ-backed coverage and client confirmation | Mechanic avatar movement now stays on transform-family motion frames when the loaded body actually exposes them instead of collapsing every non-walk state back to the stand pose, while flamethrower, Rocket Booster, Cyclone, Monkey Wave, Wild Hunter Swallow, Wild Hunter More Wild, Evan dragon-breath charges, Dual Blade `Final Cut` / `Monster Bomb`, and Battle Mage `Dual Vulcan` now keep their transform-specific cast, sustain, and teardown windows; broader transform parity remains incomplete for the remaining non-Mechanic class transform branches that still need WZ-to-client confirmation | `SkillManager`, `PlayerCharacter.TriggerSkillAnimation`, `CharacterAssembler` (`22121000`, `22151001`, `23121000`, `32121003`, `33101005`, `33121006`, `4341002`, `4341003`, `5311002`, `35001001`, `35101009`, `35101004`, `35121005`, `35111004`, `35121013`, `CAvatar::MoveAction2RawAction`, `CUserLocal::TryDoingCyclone`, `CUserLocal::TryDoingPreparedSkill`, `CUserLocal::TryDoingSwallowBuff`, `Skill/2212.img/22121000/{prepare,action}`, `Skill/2215.img/22151001/{prepare,action}`, `Skill/2312.img/23121000/{prepare,keydown,keydownend}`, `Skill/3310.img/33101005/prepare/action`, `Skill/3312.img/33121006/action/0`, `Skill/434.img/4341002/{prepare,action}`, `Skill/434.img/4341003/{prepare,action}`, `Skill/531.img/5311002/{prepare,keydown,action}`, `Character/00002000.img/{icebreathe_prepare,dragonIceBreathe,breathe_prepare,dragonBreathe,dualVulcanPrep,dualVulcanLoop,dualVulcanEnd,finalCutPrepare,finalCut,monsterBombPrepare,monsterBombThrow,swallow_loop,swallow,wildbeast}`) |
| Partial | Client-managed avatar effect layers | The simulator now loads persistent avatar-bound skill layer branches from WZ (`special`, `special0`, `finish`, `finish0`, ladder-aware `back` / `back_finish`, and `repeat` on `suddenDeath` skills), attaches them to the player render path as over-character / under-face layers, switches the `More Wild` family onto its ladder override, plays finish cleanup on buff expiry or cancellation, routes `doubleJump` action-family movement skills onto a client-style transient under-face avatar layer instead of the generic cast sprite, now extends that same transient avatar-owned seam to `Rush`-family movement casts so skills like `Skill/122.img/1221007/{effect,effect0}` suppress the detached cast sprite and keep both planes bound to the avatar for the dash window, promotes fly/flying buff `effect` branches onto looping player-owned avatar layers so wing visuals stay attached to the avatar for the full buff window instead of rendering as detached cast sprites, promotes Dark Sight-family `effect` branches such as `Skill/400.img/4001003/effect` and `Skill/1400.img/14001003/effect` onto the same looping avatar-owned layer path instead of leaving them as detached cast sprites, promotes invisible swallow buff `effect` branches such as `Skill/3310.img/33101006/effect` onto a client-style under-face avatar layer that now hides while the player is on a ladder or rope, now preserves sibling `effect0` branches on transient client-owned movement layers as an extra avatar-bound plane instead of dropping them, also promotes root-`invisible` buff skills whose WZ publishes only timed `effect/effect0` planes without `affected` or `special` branches, such as `Skill/000.img/0001037/{effect,effect0}`, onto the same looping avatar-owned path instead of leaving them detached, and also promotes WZ-backed damage-reflect buff visuals that publish timed `effect/effect0` without `affected` or `special` branches, such as `Skill/110.img/1101007`, `Skill/120.img/1201007`, and `Skill/3110.img/31101003`, onto looping avatar-owned layers while still suppressing those client-owned avatar layers whenever Oak Barrel-style morph buffs or full-body Mechanic ride actions own the sprite; the remaining gap is the rest of the client's update-owned effect-layer surface outside these confirmed families | Visible parity is closer for `More Wild`, `Final Cut`-style sudden-death buffs, double-jump and rush movement skills, flying-wing buffs, Dark Sight-family stealth buffs, root-`invisible` effect-only buffs, Wild Hunter swallow buffs, and the confirmed damage-reflect buff family because the promoted avatar-owned layers now follow the client's ownership rules more closely and no longer drop transient `effect0` companion planes on movement casts or on effect-only invisible buffs whose WZ flag lives at the skill root; broader `CUser::Update`-owned effect families outside the confirmed `special*`, invisible/effect-only buff, flight, swallow, reflect, and movement cases are still incomplete | `PlayerCharacter`, `SkillLoader`, `SkillManager`, `CharacterAssembler` (`CUser::Update`, `CUser::UpdateMoreWildEffect`, `CUserLocal::TryDoingSwallowBuff`, `Skill/3312.img/33121006/{special,special0,finish,finish0,back,back_finish}`, `Skill/434.img/4341002/repeat`, `Skill/400.img/4001003/effect`, `Skill/1400.img/14001003/effect`, `Skill/310.img/3101003/effect`, `Skill/3300.img/33001002/effect`, `Skill/122.img/1221007/{effect,effect0}`, `Skill/3310.img/33101006/effect`, `Skill/000.img/0001037/{effect,effect0,invisible}`, `Skill/110.img/1101007/{effect,effect0}`, `Skill/120.img/1201007/{effect,effect0}`, `Skill/3110.img/31101003/{effect,effect0}`, `Skill/1411.img/14111000/effect0`, `Skill/510.img/5101007`) |
| Missing | Remote user / `CUserPool` actor parity | A second IDA and backlog pass shows that remote-user ownership is still only represented indirectly as a blocker note under wedding, Ariant, Battlefield, escort-follow, minimap-helper, couple-chair, Messenger, and remote afterimage work instead of as a first-class backlog row. The simulator still lacks a true `CUserPool`-style actor layer that can own remote avatar look decode, per-user field insertion/removal, packet-driven common-vs-remote user dispatch, move-path or stance updates, remote action selection, chair or mount state, social/helper marker state, prepared-skill or portable-chair effect playback, and shared overlay registration for systems that currently fake remotes through per-feature runtime overlays | This is now a cross-cutting dependency rather than a local footnote: without a real remote-user actor owner, many rows can only simulate visible remotes through one-off field overlays or deterministic placeholders, which blocks client-shaped parity for ceremony participants, special-field competitors, remote follow requests, multiplayer chair state, minimap helper families, messenger-presence visuals, packet-owned remote portals or affected-area interactions, and any future packet-authored user effects or afterimages. IDA confirms this is broader than avatar assembly alone because `CUserPool::OnPacket` owns user enter/leave plus common/remote/local packet routing, `CUserPool::Update` advances both remote actors and shared social-effect state, and `CUserRemote::Update` handles remote movement, one-time action playback, effect dispatch, keydown-prep presentation, and portable-chair activation before falling through to `CUser::Update`. | shared field actor/runtime layer (`CUserPool::OnPacket`, `CUserPool::Update`, `CUserRemote::Update`, `CUser::LoadLayer`, `CUser::PrepareActionLayer`, `CUser::SetMoveAction`, `CUser::Update`, `CMovePath::{Decode,OnMovePacket}`) |


## Priority Order

If the goal is visible parity first, the next work should be sequenced like this:

1. Avatar parity pass:
   Validate default asset selection, client-managed overlay/effect layers, and remaining action coverage against WZ and client behavior.
2. Remote actor foundation pass:
   Add a real `CUserPool`-style remote-user actor layer before continuing to extend wedding, Ariant, Battlefield, minimap-helper, and follow-request overlays independently.
3. Skill execution pass:
   Implement queued follow-up, repeat-skill, bound-jump, rocket-booster teardown, and movement-coupled shoot families rather than extending the generic cast path indefinitely.
4. UI feedback pass:
   Add quick-slot validation, quest or balloon feedback, chat surfaces, and complete skill-window behavior.
5. Movement refinement pass:
   Remove the remaining ladder/float stubs, add passive transfer-field handoff, and tighten platform/dynamic foothold sync.
6. Interaction pass:
   Add NPC talk/quest flow, follow and direction-mode handling, and richer reactor interactions.

## Working Rule For Future Updates

Update this file directly, and always classify each item as one of:

- `Implemented`
- `Partial`
- `Missing`

That keeps the backlog honest and prevents already-shipped work from reappearing as "missing".
