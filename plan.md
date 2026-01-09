# HaCreator MapSimulator & MapleLib Improvement Plan

## Project Overview

This document outlines a comprehensive multi-phase improvement plan for HaCreator's MapSimulator and MapleLib, based on:
1. Analysis of current codebase (~39 MapSimulator files, ~167 MapleLib files)
2. Reverse engineering data from MapleStory v95 client via IDA Pro
3. Identified gaps between current implementation and actual client behavior

---

## Executive Summary

### Current State
- **MapSimulator**: Functional 2D map renderer with basic mob/NPC movement, UI system, and Spine animation support
- **MapleLib**: Comprehensive WZ file parser with support for multiple encryption schemes and file formats

### Key Gaps Identified
1. Physics system lacks accuracy compared to real client (CVecCtrl)
2. Missing effect systems (screen tremble, fading, explosions, chain lightning)
3. No dynamic foothold support (CField_DynamicFoothold)
4. Limited field effect implementations
5. No player character simulation
6. Memory management issues (TexturePool never clears)
7. Incomplete specialized field types

---

## Phase 1: Foundation & Core Systems
**Focus: Fix critical issues and establish proper architecture**

### 1.1 Memory Management Overhaul
- [x] Implement TTL-based texture cleanup in `TexturePool.cs`
- [x] Add object pooling for frequently allocated items (frames, sprites)
- [ ] Fix WzObject.MSTag/MSTagSpine reference leaks
- [ ] Add proper IDisposable patterns throughout

### 1.2 Physics System Accuracy (CVecCtrl Parity)
Based on decompiled client code, implement accurate physics:
- [x] Create `CVecCtrl` equivalent class with:
  - `CurrentFoothold` - current foothold pointer (m_pfh)
  - `IsOnFoothold()` - simple foothold check
  - Ladder/rope physics (IsOnLadderOrRope, GrabLadder, ReleaseLadder)
  - Knockback bounds clamping to prevent falling off platforms
- [x] Accurate physics constants from Map.wz/Physics.img:
  - Gravity: 2000 px/s² (confirmed via IDA Pro)
  - Jump: 555 px/s initial velocity
  - Terminal velocity: 670 px/s
  - Walk speed: 100 px/s base (Speed stat = px/s, official formula)
  - Climb speed: 100 px/s
- [x] Time-based physics (framerate-independent)
  - Formula: v += (force/mass) * tSec; pos += v * tSec
  - AccSpeed/DecSpeed functions from official client
- [ ] Implement proper slope movement with 256-unit circle angles

### 1.3 Foothold System Enhancement
- [ ] Implement anchor-based foothold navigation (matching client)
- [ ] Add wall collision detection (FindWallL/FindWallR)
- [ ] Support for foothold info packets (`OnFootHoldInfo`)
- [ ] Dynamic foothold preparation for Phase 3

### 1.4 Code Architecture Cleanup
- [x] Refactor MapSimulatorLoader.cs (1,256 → ~500 lines)
  - Created `Loaders/UILoader.cs` - StatusBar, Minimap, MouseCursor
  - Created `Loaders/LifeLoader.cs` - Mob, NPC
  - Created `Loaders/EffectLoader.cs` - Portal, Reactor
- [ ] Create abstraction for UI layout (HaUIGrid/StackPanel improvements)
- [ ] Standardize animation state machine pattern
- [ ] Add proper logging/debugging infrastructure

---

## Phase 2: Animation & Effects System
**Focus: Match client's CAnimationDisplayer capabilities**

### 2.1 Screen Effects (From Client Analysis)
Implement effects found in CAnimationDisplayer:
- [x] **Effect_Tremble**: Screen shake effect
  - Parameters: trembleForce, bHeavyNShortTremble, tDelay, tAddEffectTime
  - Duration: 1500ms (heavy) or 2000ms (normal)
  - Reduction factors: 0.85 (heavy) or 0.92 (normal)
- [x] **FADEINFO**: Layer fade effects
- [x] **Portal Fade Effects**: Screen fade on portal enter/exit
  - Based on CStage::FadeIn / CStage::FadeOut from IDA Pro analysis
  - FadeOut when leaving map (CMapLoadable::Close)
  - FadeIn when entering map (CField::Init)
  - Duration: 600ms normal, 300ms for fast travel mode
  - Uses RGB tone manipulation from black (0,0,0) to normal (255,255,255)
  - State machine: None → FadingOut → (map change) → FadingIn → None
  - Callback-based completion detection for map transition timing
- [x] **MOTIONBLURINFO**: Motion blur rendering
  - Directional blur with configurable strength and angle
  - Horizontal/Vertical blur convenience methods
  - Sample offset generation for multi-pass rendering
- [x] **EXPLOSIONINFO**: Explosion effects
  - Expanding ring with configurable radius and duration
  - Element variants (fire, ice, dark)
  - Automatic screen shake integration

### 2.2 Advanced Animation Types
From CAnimationDisplayer structures:
- [x] **ONETIMEINFO**: One-shot animations (play once and remove)
- [x] **REPEATINFO**: Looping animations at fixed positions
- [ ] **PREPAREINFO**: Prepared/cached animations
- [ ] **SQUIBINFO**: Squib effects
- [x] **CHAINLIGHTNINGINFO**: Chain lightning effects between targets
  - Jagged bolt segments with glow effects
  - Blue/yellow lightning presets
- [x] **FALLINGINFO**: Falling object animations
  - Gravity-based physics with drift
  - Burst spawn for particle effects
- [x] **FOLLOWINFO**: Following target animations
  - Lambda-based target position tracking
  - Duration-based lifecycle
- [ ] **FOOTHOLDINFO**: Foothold-based animations

### 2.3 CActionFrame Improvements
Based on client's CActionFrame:
- [x] Implement `LoadMappers()` - ActionMapper class for action/frame mapping
- [x] Implement `Merge()` and `MergeGroup()` - sprite merging with z-order
- [x] Implement `UpdateMBR()` - minimum bounding rectangle calculation
- [x] Implement `UpdateVisibility()` - view frustum culling
- [x] Proper `Draw()` method with world-space rendering

### 2.4 Particle System
- [x] Basic particle emitter framework (ParticleSystem.cs)
- [x] Weather effects: Rain, Snow, Leaves
- [x] Sparkle/firework burst effects
- [x] Smoke/cloud emitters
- [x] Object pooling for performance
- [ ] Support for Effect.wz particle definitions (future)

---

## Phase 3: Specialized Field Types
**Focus: Implement field types found in client**

### 3.1 Core Field Enhancements (CField)
- [ ] `LoadMap()` improvements
- [x] `OnFieldEffect()` - field effect handling
- [x] `OnBlowWeather()` - weather effects
- [x] Weather message system (WEATHERMSGINFO)
- [x] Fear effect system (`InitFearEffect`, `OnFearEffect`, `DrawFearEffect`)
- [x] Field obstacles (`OnFieldObstacleOnOff`)
- [x] Screen effect integration
- [x] NPC hover cursor

### 3.2 Dynamic Foothold Fields (CField_DynamicFoothold)
- [x] Moving platform support (horizontal, vertical, waypoint)
- [x] Platform spawn/despawn with fade effects
- [x] Collision detection for platform surface
- [ ] Sync with mob/player positions (future enhancement)

### 3.3 Transportation Fields (CField_ContiMove)
Based on IDA Pro decompilation of `CField_ContiMove::OnContiMove` and related functions:

**Packet Handling (OnContiMove):**
- Case 8: `OnStartShipMoveField` - If value == 2, calls `LeaveShipMove()` (ship departs)
- Case 10: `OnMoveField` - If value == 4, `AppearShip()`; If value == 5, `DisappearShip()` (Balrog type)
- Case 12: `OnEndShipMoveField` - If value == 6, calls `EnterShipMove()` (ship arrives)

**CShip Class Structure (from CShip::Init):**
```
m_nShipKind  - Ship type: 0 = regular ship (Orbis-Ellinia), 1 = Balrog type
m_x, m_y     - Docked position (target position)
m_x0         - Away position (start position for regular ships)
m_f          - Flip direction (0 = right, 1 = left)
m_tMove      - Movement duration in seconds
m_sShipPath  - WZ path to ship animation (from map info "shipObj")
```

**Movement Functions:**
- `LeaveShipMove()` - Regular ship: moves from x (dock) to x0 (away) over tMove seconds
- `EnterShipMove()` - Regular ship: moves from x0 (away) to x (dock) over tMove seconds
- `AppearShip()` - Balrog type: fades in from offset position (+/-100 based on flip, -100 Y)
- `DisappearShip()` - Balrog type: fades out with alpha 255→0

**Implementation Status:**
- [x] TransportationField.cs with accurate state machine
- [x] ShipState enum: Idle, WaitingDeparture, Moving, InTransit, Docked, Appearing, Visible, Disappearing
- [x] BalrogState enum for Crimson Balrog attack events
- [x] Ship texture loading from Map.wz/Obj/contimove.img/ship/* or mob fallback
- [x] Balrog texture loading from Mob.wz (8150000 Crimson Balrog)
- [x] DrawBackground-based rendering for precise positioning
- [x] Alpha fade support for Balrog type ships
- [x] Background scrolling during voyage
- [x] Auto-detect transport maps from ShipObject in MiscItems
- [x] Actual ship sprites loaded from ObjectInfo path (oS/l0/l1, e.g., contimove/ship/0)
- [ ] Player position sync with ship during voyage

### 3.4 Limited View Fields (CField_LimitedView)
- [x] LimitedViewField.cs with viewport restriction system
- [x] Multiple view modes: Circle, Rectangle, Spotlight, Flashlight
- [x] Fog of war overlay with soft edges
- [x] Smooth radius/alpha transitions
- [x] Pulse effect for spotlight mode
- [x] Test key [0] to cycle through modes

### 3.5 Event/Minigame Fields
Priority order based on complexity:
- [x] **CField_SnowBall**: Snowball fight mechanics (MinigameFields.cs)
  - Two-team snowball pushing competition with SnowBall and SnowMan classes
  - SnowBall: Move(dx) with rotation, Win(), SetPos(), Update()
  - SnowMan: HP system, Hit(damage), stun mechanics, DrawHPBar()
  - DamageInfo queue for delayed damage display (matching client behavior)
  - GameState machine: NotStarted → Active → Team0/1InZone → Team0/1Win
  - Packet handling: OnSnowBallState(338), OnSnowBallHit(339), OnSnowBallMsg(340), OnSnowBallTouch(341)
  - Simulation mode for local testing without server packets
- [x] **CField_Coconut**: Coconut throwing game (MinigameFields.cs)
  - Two-team coconut harvesting competition with timer
  - Coconut physics: gravity-based falling, rotation, velocity
  - State machine: OnTree → Falling → Team0/1Claimed → Scored/Destroyed
  - HitInfo queue for delayed state changes
  - Packet handling: OnCoconutHit(342), OnCoconutScore(343), OnClock()
  - Team scoring with EndGame() win detection
  - Avatar equipment configuration support from WZ
- [ ] **CField_MonsterCarnival**: Carnival PvP mode
- [ ] **CField_Tournament**: Tournament brackets
- [ ] **CField_Battlefield**: Team-based battles
- [ ] **CField_AriantArena**: Arena combat

### 3.6 Special Effect Fields
- [x] **CField_Wedding**: Wedding ceremony effects
- [x] **CField_Witchtower**: Witchtower mechanics
- [x] **CField_GuildBoss**: Guild boss healer/pulley mechanics
- [x] **CField_Massacre**: Kill counting and gauges

---

## Phase 4: Mob AI & Combat Simulation
**Focus: Realistic mob behavior and combat**

### 4.1 CMob Enhancement
Based on client structures:
- [x] **MobAttackEntry**: Attack pattern data (MobAI.cs)
- [x] **MobDamageInfo**: Damage display info (MobAI.cs)
- [ ] **HITEFFECT**: Hit effect rendering (needs visual effects)
- [x] Boss detection (`IsBoss` from MobData)
- [x] Mob skills and attack patterns (MobAI.AddAttack, attack cooldowns)

### 4.2 AI State Machine
- [x] Idle/patrol behavior (MobAIState.Idle, Patrol)
- [x] Aggro detection and range (MobTargetInfo, _aggroRange)
- [x] Chase behavior (MobAIState.Chase, GetChaseDirection, ForceDirection)
- [x] Attack patterns (MobAIState.Attack, attack cooldowns)
- [x] Skill usage with cooldowns (MobAIState.Skill, NextAttackTime)
- [x] Death state (MobAIState.Death, MobDeathType)
- [x] Respawn (integrated with MobPool)

### 4.3 Mob Pool System (CMobPool)
- [x] `GetMob()` - efficient mob lookup by pool ID (MobPool.cs)
- [x] `GetMobsByType()` - get mobs by type ID
- [x] `GetMobsInRadius()` - spatial query for nearby mobs
- [x] `GetClosestMob()` - find closest mob to position
- [x] Mob spawning with proper timing (respawn timers per spawn point)
- [x] Mob despawning and cleanup (death animation → removal)
- [x] Boss spawn announcements (queue-based announcement system)
- [x] Spawn point management with MobSpawnPoint class

### 4.4 Combat Effects
- [x] Damage numbers display (CombatEffects.cs - DamageNumberDisplay class)
  - Rising animation with ease-out
  - Fade out after duration
  - Critical hit scaling/coloring
  - Combo stacking for multiple hits
  - Synced from MobAI.DamageDisplays
- [x] Hit effects (HitEffectDisplay class)
  - Frame-based hit animations at mob position
  - Color tinting support
  - Multiple variation support
- [x] Death animations (DeathEffectDisplay class in CombatEffects.cs)
  - Particle burst system with configurable colors per death type
  - Boss deaths have more particles and scale pulse effect
  - Integrated via MobPool.SetOnMobDied callback
- [x] Drop item spawning (DropPool.cs)
  - DropItem class with spawn float, gravity physics, bouncing
  - Meso and item drop types with visual rendering
  - DropState machine: Spawning → Falling → Bouncing → Idle → PickingUp
  - Pickup range detection and ownership priority
  - Integrated via MobPool death callback

---

## Phase 5: Player Character Simulation
**Focus: Playable character in simulator with full avatar rendering**

### 5.1 Character Data Parsing (Character.wz)
Understanding Character.wz structure:
```
Character.wz/
├── 00002000.img (Skin - body frames)
├── 00012000.img (Skin - head frames)
├── Face/
│   └── 00020000.img, 00020001.img... (Face sprites)
├── Hair/
│   └── 00030000.img, 00030001.img... (Hair sprites)
├── Cap/ Coat/ Pants/ Shoes/ Glove/ Cape/ Shield/ (Equipment)
├── Weapon/
│   └── 01302000.img... (Weapon sprites by ID)
└── Accessory/ (Face/Eye accessories)
```

- [x] Parse body frames (00002xxx.img) - walk, stand, jump, prone, ladder, rope, etc.
- [x] Parse face frames from Face/*.img - blink, default, expressions
- [x] Parse hair frames from Hair/*.img - including backHair layers
- [x] Parse equipment overlays from respective folders
- [x] Parse weapon frames with attack animations
- [x] Handle z-ordering (zmap) for proper layer compositing
**Implementation**: `CharacterData.cs` (data structures), `CharacterLoader.cs` (WZ parsing)

### 5.2 Character Assembler (CAvatar equivalent)
Based on client's avatar system:
- [x] Create `CharacterAssembler` class for compositing parts
- [x] Implement z-layer sorting (body, arm, armOverBody, mail, etc.)
- [x] Handle equipment mixing (e.g., cash items hiding default)
- [x] Support transparency mixing for semi-transparent items
- [x] Handle "vslot" (visible slot) conflicts
- [x] Implement origin point alignment across all parts
- [x] Cache assembled frames for performance
**Implementation**: `CharacterAssembler.cs` with map point alignment (navel, neck, brow)

### 5.3 Animation State Machine
Character animation states from client:
```
States: stand1, stand2, walk1, walk2, jump, sit, prone, proneStab
        ladder, rope, fly, swim, stabO1, stabO2, stabOF, swingO1...
        alert, heal, etc.
```
- [x] Create state machine matching client actions
- [x] Handle action transitions (stand → walk → jump)
- [x] Synchronize body/weapon/effect animations
- [x] Support action delay timings from WZ
- [x] Handle attack animation chains
**Implementation**: `PlayerCharacter.cs` with PlayerState enum and UpdateStateMachine()

### 5.4 CUser/CUserLocal Implementation
- [x] Create `PlayerCharacter` class using CVecCtrl for physics
- [x] Implement foothold detection and ground snapping
- [x] Implement ladder/rope climbing mechanics
- [x] Handle prone/sit states
- [x] Implement swimming in swim areas
- [ ] Support flying mounts (future)
**Implementation**: `PlayerCharacter.cs` integrates with `CVecCtrl.cs`

### 5.5 Input Handling
- [x] Arrow keys for movement (left/right/up/down)
- [x] Alt key for jump
- [x] Ctrl key for attack
- [x] Up arrow for ladder/rope/portal interaction
- [x] Down arrow for sit/prone
- [x] Configurable key bindings
- [x] Gamepad support
**Implementation**: `PlayerInput.cs` with KeyBinding system and gamepad thumbstick support

### 5.6 Character Configuration UI
- [x] Character selector/creator (CharacterConfigManager.CreatePreset)
- [x] Equipment slot assignment (CharacterBuild.Equip/Unequip)
- [x] Skin/hair/face customization (CharacterPreset properties)
- [x] Save/load character presets (JSON serialization)
- [x] Random character generator (CreateRandomPreset)
**Implementation**: `CharacterConfig.cs` with CharacterPreset, CharacterConfigManager

### 5.7 Combat Integration
- [x] Attack hitbox calculation from weapon range
- [x] Collision detection with mobs
- [x] Apply damage and knockback to mobs
- [x] Receive damage from mob attacks
- [x] Death and respawn at spawn point
- [x] Simple HP/MP bars
**Implementation**: `PlayerCombat.cs` with damage calculation, knockback, invincibility frames
**Integration**: `PlayerManager.cs` connects all systems with MapSimulator

### 5.8 Character Spawning on Map Load
- [x] Auto-spawn player character when MapSimulator starts
- [x] Spawn at viewfinder center position
- [x] Create placeholder player if no Character.wz loaded
- [x] Initialize PlayerManager and connect to MapSimulator
- [x] Camera follows player position (arrow keys move player)
- [x] Debug box visualization (F5 mode shows green box + red crosshair)
- [x] Tab key toggles between player control and free camera modes
- [x] R key respawns player at viewfinder center

### 5.9a Character Graphics Loading
- [x] Load Character.wz file in MapSimulator initialization
- [x] Pass Character.wz to PlayerManager.Initialize()
- [x] CharacterLoader.LoadDefaultMale() creates basic character build
- [x] Load body frames from 00002xxx.img (skin) - working
- [x] Load head frames from 00012xxx.img - uses WzUOLProperty links, resolved via LinkValue
- [x] Load face frames from Face/00020xxx.img - VirtualWzDirectory subdirectory navigation
- [x] Load hair frames from Hair/00030xxx.img - direct FindImage lookup (skip VirtualDir enumeration)
- [x] CharacterAssembler composites parts with z-ordering
- [x] Render assembled character frames in PlayerCharacter.Draw()
- [x] Fix character facing direction (sprites face LEFT by default, flip when FacingRight)
- [x] Fix flipped sprite positioning (account for texture width in offset calculation)
- [x] Ensure monster knockback does not cause character to fall off platform
- [x] Match official client physics constants (speed, jump, gravity)
- [x] Tune walk speed for better visual match with framerate-independent physics
  - Updated to proper time-based physics (px/s instead of px/tick)
  - Walk speed: 100 px/s (official formula: Speed stat = px/s)
  - Note: MapleNecrocer uses 150 px/s (1.5x faster than official)
  - Walk force: 400 px/s² acceleration
  - Walk drag: 600 px/s² deceleration
  - Jump velocity: 555 px/s
  - Gravity: 2000 px/s²
  - Terminal velocity: 670 px/s
  - Source: IDA Pro analysis of CVecCtrl::CalcWalk, AccSpeed, DecSpeed functions
  - Formula: v += (force/mass) * tSec; pos += v * tSec
- [x] GM Fly Mode toggle (G key) - free flying ignoring physics (400 px/s)
- [x] Framerate-independent physics (runs correctly at 60fps to 200fps+)
- [x] Load weapon frames from Character.wz/Weapon/01302000.img (One-Handed Sword)
- [x] Equip weapon in LoadDefaultMale() character build
- [x] Render weapon sprite with correct z-ordering in CharacterAssembler
- [x] Fix weapon position - transform arm's hand coords to body's coordinate system
  - Formula: `hand_in_body = body.navel - arm.navel + arm.hand`
- [x] Fix weapon on ladder/rope - hide weapon if no climbing animation exists
- [x] Fix missing right hand in character - load all body sub-parts (body, arm, lHand, rHand)
  - Extended CharacterFrame with SubParts list to hold multiple canvases per frame
  - Added CharacterSubPart class with NavelOffset for positioning relative to body navel
  - CharacterLoader.LoadBodyFrameWithSubParts() loads all body canvases as sub-parts
  - CharacterAssembler.AddPart() renders each sub-part with correct offset and z-ordering
- [x] Fix camera centering - on some maps, camera isn't truly positioned at character X/Y center
  - Fixed incorrect camera formula in Update() that was missing the centering calculation
  - Previous: `mapShiftX = playerPos.X` (placed player at left edge)
  - Correct: `mapShiftX = playerPos.X + _mapCenterX - RenderWidth/2` (centers player)
  - Rendering formula: `screenX = worldX - mapShiftX + centerX`
  - To center player at screen center: `RenderWidth/2 = player.X - mapShiftX + mapCenterX`
  - Solving for mapShiftX: `mapShiftX = player.X + mapCenterX - RenderWidth/2`
  - Same formula as GM fly mode (lines 1737-1738) now used in Update() camera follow
- [x] Fix character rendered below platform (navel on ground instead of feet)
  - Player Y position represents feet (on foothold), but character was rendered with navel at Y
  - Added `FeetOffset` property to `AssembledFrame` - distance from navel to feet
  - `CalculateBounds()` now calculates FeetOffset from maxY (bottom of character bounds)
  - `Draw()` method applies offset: `adjustedY = screenY - FeetOffset`
  - Updated `HITBOX_OFFSET_Y` from -30 to -60 (hitbox now extends upward from feet)
  - Character now stands correctly on platforms with feet touching the foothold
- [x] Fix character animation playing too fast
  - Frame delay was being read from body sub-part canvas (`stand1/0/body`) instead of frame node (`stand1/0`)
  - In MapleStory WZ structure, the delay property is at the frame level, not sub-part level
  - Updated `LoadBodyFrameWithSubParts()` to read delay from `frameSub["delay"]` and override sub-part delay
  - Changed default delay from 100ms to 200ms (typical MapleStory character animations use 200-300ms/frame)
- [x] Portal teleportation with UP key (matching official client behavior)
  - Player can press UP arrow when near a portal to teleport (same as double-clicking)
  - Added `HandlePortalUpInteract()` method in MapSimulator.cs
  - Uses `InputAction.Interact` (bound to UP key) from PlayerInput
  - Portal proximity detection: 40px horizontal, 60px vertical range
  - Checks both visible portals and hidden portals with valid destinations
  - Finds nearest portal if multiple are in range
  - Triggers same teleportation logic as double-click (PlayPortalSE, _pendingMapChange)
  - Works with both same-map portals and cross-map portals
- [x] Fix player spawn at target portal position
  - Player now spawns exactly at the target portal position when teleporting
  - Fixed `LoadMapContent()` to use `spawnX/spawnY` instead of view center calculation
  - Same-map teleportation now moves player via `_playerManager.TeleportTo()`
  - Updated spawn point for R key respawn via `_playerManager.SetSpawnPoint()`
- [x] Foothold snapping on portal spawn (prevent falling)
  - Portal position might be above a platform, causing player to fall
  - `TeleportTo()` now finds foothold at/below position and snaps player to it
  - `Respawn()` and `RespawnAt()` call `SnapToFoothold()` after respawn
  - Uses `_findFoothold(x, y, 500)` to search 500px below for platforms
  - Calculates exact Y on foothold and calls `Physics.LandOnFoothold(fh)`

**Technical Notes**:
- Body: `stand1/0/body` - numbered frames with body parts
  - Body frame contains multiple sub-parts: body, arm, lHand, rHand, head (UOL)
  - Each sub-part has its own canvas, origin, z-layer, and map points
  - Sub-parts are positioned relative to body's navel using their navel map points
  - "hand" map point is in `arm` canvas, NOT `body` canvas
  - "handMove" map point may be in `lHand` canvas
  - rHand is the "other" hand/arm, visible in certain poses
- Head: `stand1/0/head` - uses WzUOLProperty (links), resolve via `uol.LinkValue`
- Face: `default/face` (WzCanvasProperty) - expression/face structure
- Hair: `stand1/hair/0` - action/hair/frameNumber structure
- Weapon: `stand1/0/weapon` - action/frame/weapon or action/weapon/frame structure
  - ID 01302000 = One-Handed Sword (basic beginner sword)
  - info/attackSpeed, info/incPAD, info/twoHanded properties
  - Attack actions: swingO1, swingO2, swingOF, stabO1, stabO2, stabOF
  - Weapons without ladder/rope animations should be hidden while climbing
- VirtualWzDirectory: Avoid `.WzImages` enumeration (loads ALL files), use direct indexer or FindImage

### 5.9b Character Skills (Skill.wz)
Understanding Skill.wz structure:
```
Skill.wz/
├── 0.img (Beginner skills)
├── 100.img, 110.img... (Warrior skills by job)
├── 200.img, 210.img... (Magician skills)
├── 300.img, 310.img... (Bowman skills)
├── 400.img, 410.img... (Thief skills)
├── 500.img, 510.img... (Pirate skills)
└── Each skill ID contains:
    ├── icon (skill icon)
    ├── effect/ (skill visual effects)
    ├── hit/ (hit effects on target)
    ├── ball/ (projectile graphics)
    ├── level/ (skill data per level)
    │   ├── damage, mpCon, range, time, etc.
    └── common/ (shared properties)
```

Implementation tasks:
- [x] **SkillData parsing**: Parse Skill.wz structure for skill properties
  - SkillData.cs: Complete data structures (SkillType, SkillElement, SkillTarget, SkillLevelData)
  - SkillLoader.cs: WZ parsing (LoadSkill, LoadJobSkills, ParseSkillLevels, LoadSkillAnimation)
- [x] **Skill effects**: Load and render skill effect animations
  - SkillAnimation class with frame-based animation
  - DrawCastEffect for skill use effects on caster
- [x] **Hit effects**: Display hit effects on mobs when skills connect
  - ActiveHitEffect class for tracking active hit animations
  - SpawnHitEffect/DrawHitEffect in SkillManager
  - Auto-cleanup of expired hit effects
- [x] **Projectiles**: Ball/projectile rendering with trajectory
  - ProjectileData with speed, pierce, max hits
  - ActiveProjectile with physics and collision detection
- [x] **Skill hotkeys**: Bind skills to keyboard keys (1-8 via PlayerInput)
  - PlayerInput already has Skill1-8 bindings (Insert, Home, PageUp, Delete, End, PageDown, D1, D2)
  - SkillManager.TryCastHotkey() processes hotkey presses
  - Integrated in PlayerManager.Update()
- [x] **Skill casting**: Animation, cooldown, MP consumption
  - SkillManager.StartCast() handles cast state, MP/HP consumption, cooldown tracking
  - Player animation trigger via TriggerSkillAnimation()
- [x] **Skill damage calculation**: Apply skill multipliers to damage
  - CalculateSkillDamage() uses skill damage %, player attack, weapon attack, variance
- [x] **Area of effect**: Handle multi-target skills
  - mobCount property from skill level data
  - attackCount for multi-hit attacks
- [x] **Buff skills**: Apply and display buff effects on character
  - ActiveBuff tracking with duration
  - ApplyBuffStats for stat modifiers (PAD, PDD, MAD, etc.)
  - DrawAffectedEffect for looping buff visuals while active

### 5.10 Skill Usage Integration

#### 5.10a Melee Attack Skills
- [x] TryDoingMeleeAttack() - close range attack skills (ProcessMeleeAttack in SkillManager)
- [x] Attack hitbox calculation from skill range (lt/rb) - GetAttackRange in SkillData
- [x] Multi-target damage (mobCount property) - maxTargets in ProcessMeleeAttack
- [x] Multi-hit attacks (attackCount property) - attackCount loop in ProcessMeleeAttack
- [x] Skill effect animation on caster - DrawCastEffect
- [x] Hit effect animation on targets - SpawnHitEffect/DrawHitEffect
- [x] Melee skill hotkeys (Ctrl for basic attack) - integrated via PlayerInput

#### 5.10b Ranged/Shooting Skills
- [x] TryDoingShoot() - projectile-based attacks (SpawnProjectile in SkillManager)
- [x] Projectile spawning from ball node - LoadProjectile in SkillLoader
- [x] Projectile trajectory and collision - ActiveProjectile.Update, CheckProjectileCollisions
- [x] Pierce vs non-pierce projectiles - Piercing property, RegisterHit logic
- [x] Multiple projectile spread (bulletCount) - spread angle calculation in SpawnProjectile
- [ ] Arrow/throwing star consumption (itemCon)
- [x] Ranged skill hotkeys - via TryCastHotkey

#### 5.10c Magic Attack Skills
- [x] TryDoingMagicAttack() - magic-based attacks (same flow as melee with effect animation)
- [x] Magic effect animations (effect node) - SkillAnimation loading
- [x] MP consumption and cooldowns - StartCast handles mpCon, cooldown tracking
- [x] Area of effect damage - mobCount handling
- [ ] Element-based damage types (fire, ice, lightning, etc.) - SkillElement enum defined
- [x] Magic skill hotkeys - via TryCastHotkey

#### 5.10d Buff and Support Skills
- [x] Apply buffs to player stats (pad, mad, pdd, speed, etc.) - ApplyBuffStats
- [x] Buff duration tracking (time property) - ActiveBuff with Duration, StartTime
- [x] Affected effect display while buff active - DrawAffectedEffect
- [ ] Buff icon display in UI
- [ ] Party buff support
- [x] Heal skills (HP/MP recovery) - ApplyHeal in SkillManager

#### 5.10e Character Attack Skills Hotkey System
Implement a complete hotkey system for character attack skills matching the official client behavior:

**Hotkey Configuration:**
- [ ] QuickSlot UI panel for skill assignment (8 slots matching client)
- [ ] Drag-and-drop skill assignment from skill window
- [ ] Keyboard number keys 1-8 for quick skill activation (configurable)
- [ ] Function keys F1-F12 for extended hotkey slots
- [ ] Ctrl+1-8 for secondary hotkey bar
- [ ] Save/load hotkey configurations per character preset

**Skill Execution Flow:**
- [ ] Hotkey press → SkillManager.TryExecuteHotkey(slotIndex)
- [ ] Validate skill availability (learned, cooldown, MP/HP requirements)
- [ ] Check attack range and target availability for attack skills
- [ ] Play character action animation (swingO1, stabO1, etc.)
- [ ] Spawn skill visual effect at character/target position
- [ ] Calculate and apply damage to targets in range
- [ ] Consume MP/HP/items as required
- [ ] Start skill cooldown timer

**Attack Skill Types:**
- [ ] **Basic Attack (Ctrl key)**: Default weapon attack based on weapon type
  - Sword/Axe/Mace: swingO1, swingO2, swingOF animations
  - Spear/Polearm: stabO1, stabO2, stabOF animations
  - Wand/Staff: swingO1 (magic weapons)
  - Bow/Crossbow: shoot1, shoot2 (ranged)
  - Claw: stabO1 (throwing stars)
  - Gun: shoot1 (bullets)
- [ ] **Job Skills (1-8 keys)**: Assigned skills from skill window
  - Cooldown tracking per skill
  - Cast animation based on skill type
  - Effect animations from Skill.wz/effect/
- [ ] **Combo System**: Chain attacks for certain skills
  - Track last skill used and timing
  - Enable follow-up skills within combo window

**Visual Feedback:**
- [ ] Skill icon cooldown overlay (grey sweep animation)
- [ ] MP/HP cost display on hotbar when insufficient
- [ ] Skill name popup on hotkey press
- [ ] Damage numbers synced with skill hit timing
- [ ] Screen flash/shake for powerful skills

**Sound Effects:**
- [ ] Load skill sound effects from Sound.wz/Skill.img
- [ ] Play attack sound on skill activation
- [ ] Play hit sound on successful damage
- [ ] Play miss/block sound on failed attacks

**Client Reference (CUserLocal attack functions):**
```cpp
// From IDA Pro analysis:
CUserLocal::TryDoingBasicAttack()  // Ctrl key basic attack
CUserLocal::OnKeyDown_Skill()      // Hotkey skill activation
CUserLocal::PrepareAttack()        // Setup attack state
CUserLocal::SendMeleeAttack()      // Send attack packet (local simulation)
CSkillInfo::GetSkillByID()         // Get skill data
CAvatar::DoAction()                // Trigger attack animation
```

**Implementation Files:**
- `HotkeyManager.cs` - Hotkey slot management and persistence
- `QuickSlotUI.cs` - Visual hotbar with drag-drop support
- `SkillExecutor.cs` - Skill execution logic and validation
- Update `PlayerInput.cs` - Add F1-F12 and Ctrl+key bindings
- Update `SkillManager.cs` - Connect hotkey system to existing skill logic

#### 5.10f Random Attack Key (C Key)
Test functionality for quickly testing all three attack types:
- [x] **'C' Key = Random Attack**: Pressing 'C' activates one of the following randomly:
  - TryDoingMeleeAttack() - close range melee attack with swingO1 animation
  - TryDoingShoot() - ranged projectile attack with shoot1 animation
  - TryDoingMagicAttack() - magic attack with MP cost and higher damage
- [x] **TryDoingMeleeAttack()**: Basic melee attack implementation
  - 80x60 hitbox in front of player
  - Can hit up to 3 mobs
  - Triggers swingO1 animation
  - Shows damage numbers via CombatEffects
- [x] **TryDoingShoot()**: Basic projectile attack implementation
  - Spawns projectile at player hand height
  - Speed 8.0f, 2000ms lifetime
  - Non-piercing (stops on first hit)
  - Triggers shoot1 animation
- [x] **TryDoingMagicAttack()**: Basic magic attack implementation
  - 120x80 hitbox (larger than melee)
  - Single target (closest mob in range)
  - Consumes 10 MP
  - 20% critical hit chance with 1.5x damage
  - Triggers swingO1 animation
- [x] **TryDoingRandomAttack()**: Random selection wrapper
  - Randomly picks attack type 0-2
  - Calls corresponding attack method
- [x] **Shift+C = Toggle Smooth Camera** (moved from C key)
  - Smooth camera toggle now requires Shift modifier
  - Prevents conflict with attack key

**Implementation Files:**
- `SkillManager.cs` - Added TryDoingMeleeAttack, TryDoingShoot, TryDoingMagicAttack, TryDoingRandomAttack
- `PlayerManager.cs` - Added wrapper methods for attack functions
- `MapSimulator.cs` - Updated C key handler to call random attack, Shift+C for camera toggle

#### 5.10g Monster HP Bar Implementation
**Regular Mob HP Bar:**
- [x] `MobHPBarDisplay` class in CombatEffects.cs
  - Tracks mob reference, HP percent, damage time, alpha
  - 60x5 pixel bar positioned at mob's top boundary
  - 5 second display duration, 500ms fade out
- [x] `DrawSingleMobHPBar()` method
  - Red HP bar with dark background and border
  - Orange color when HP < 30%
  - Highlight at top for visual polish

**Boss HP Bar:**
- [x] `BossHPBarDisplay` class in CombatEffects.cs
  - Tracks boss mob, name, HP, animated display percent
  - 400x20 pixel bar centered at top of screen
  - Gold border, dark background
  - Animated HP drain (orange trail shows recent damage)
- [x] `DrawSingleBossHPBar()` method
  - Boss name above bar
  - HP text below (current/max or percentage for high HP)
  - Multiple boss support (stacked vertically)

**Integration:**
- [x] `OnMobDamaged(mob, time)` - Triggers HP bar on damage
- [x] `SyncHPBarsFromMobPool()` - Syncs bars with mob pool state
- [x] Attack methods call `OnMobDamaged` when hitting mobs
- [x] Mob death handling - removes HP bar, triggers death effect
- [x] Boss HP bar drawn in UI layer (after minimap)

**Death and Removal System:**
- [x] Mobs die when HP drops to 0 or below
- [x] Death animation plays once (die1), then mob is removed from map
- [x] `MobAI.Kill()` triggers death state with timer
- [x] `MobPool.RemoveMob()` clears mob from pool after death animation
- [x] `_onMobRemoved` callback nulls array entry in MapSimulator
- [x] HP bars hidden for dead mobs (checks `IsDead`)
- [x] Dead mobs cannot be targeted by attacks

**Knockback Effect:**
- [x] Melee attacks apply 6-12 force knockback based on damage
- [x] Magic attacks apply 4-8 force knockback
- [x] `MobMovementInfo.ApplyKnockback()` handles physics
- [x] Knockback direction based on player facing

**Mob Sound Effects:**
- [x] Load mob-specific sounds from Sound.wz/Mob.img/{mobId}/
  - `Damage` - played when mob takes damage
  - `Die` - played when mob dies
- [x] `MobItem.DamageSE` and `DieSE` properties
- [x] `MobItem.PlayDamageSound()` and `PlayDieSound()` methods
- [x] `LifeLoader.LoadMobSounds()` loads sounds during mob creation
  - Fixed WzUOLProperty handling - resolves UOL links before casting to WzBinaryProperty
- [x] `MobItem.ApplyDamage()` - wrapper method that calls AI.TakeDamage and plays sounds
  - Plays damage sound on every hit
  - Plays die sound when mob dies
- [x] All damage call sites updated to use `mob.ApplyDamage()` instead of `mob.AI.TakeDamage()`

**Mob Death Animation & Cleanup:**
- [x] Dead mobs stop moving immediately when killed
  - `UpdateMovement()` returns early when `AI.IsDead`, skipping all movement updates
- [x] `MobItem.IsDeathAnimationComplete` property tracks when death animation finishes
  - `_deathAnimationCompleted` flag set when death animation plays through all frames
  - Works for both single-frame and multi-frame death animations
- [x] `SetAction()` resets animation timing when switching to death animation
  - Resets `_lastFrameSwitchTime` for fresh animation start
  - Resets `_deathAnimationCompleted` flag
- [x] `MobPool.Update()` detects dead mobs and moves them to dying list
  - Scans `_activeMobs` for mobs with `AI.IsDead == true`
  - Moves dead mobs to `_dyingMobs` list for death animation processing
  - Updates spawn point timers for respawn
- [x] Mobs removed immediately when death animation completes
  - `MobPool.Update()` checks `mob.IsDeathAnimationComplete`
  - Removes mob from pool when animation finishes (not fixed timer)

**Implementation Files:**
- `CombatEffects.cs` - MobHPBarDisplay, BossHPBarDisplay, HP bar drawing
- `MobPool.cs` - OnMobRemoved callback, mob lifecycle management, dead mob detection
- `MobAI.cs` - Death state, IsDead property, Kill() method
- `MobItem.cs` - Death animation handling, ApplyDamage(), IsDeathAnimationComplete, sound playback
- `SkillManager.cs` - Uses mob.ApplyDamage() for damage with sounds
- `PlayerManager.cs` - Uses mob.ApplyDamage() for basic attacks
- `PlayerCombat.cs` - Uses mob.ApplyDamage() for combat damage
- `MapSimulator.cs` - Mob array null handling
- `LifeLoader.cs` - LoadMobSounds() with WzUOLProperty link resolution

#### 5.10h Character Jump Down Functionality
Implement jump down (fall through platform) when player presses Down + Jump:
- [x] **CVecCtrl.JumpDown()** - New method to initiate jump down
  - Sets `FallStartFoothold` to current foothold (prevents landing on same platform)
  - Clears `CurrentFoothold` to start falling
  - Sets `IsJumpingDown = true` to distinguish from normal jumps
  - Small initial downward velocity (50 px/s)
  - Clears knockback state since this is a voluntary action
- [x] **CVecCtrl.IsJumpingDown** - New flag to track jump-down state
  - Set to true in `JumpDown()`
  - Cleared in `LandOnFoothold()` and `Reset()`
  - Used by landing detection to determine if player should fall through foothold
- [x] **PlayerCharacter.TryJumpDown()** - Handle Down+Jump input combo
  - Called when player presses Down + Jump while on a foothold
  - Checks `CantThrough` property - platforms with `CantThrough=true` cannot be jumped through
  - Initiates jump down via `Physics.JumpDown()`
  - Sets player state to `Falling`
- [x] **CheckFootholdLanding() improvement** - Better landing detection
  - Landing on different foothold: always allowed
  - Normal jump on same foothold: land when at or below foothold Y
  - Jump-down on same foothold: requires falling at least 30 pixels below it
  - Uses `IsJumpingDown` flag to distinguish between normal jump and jump-down
  - Uses linear interpolation to calculate foothold Y at player X position

**Implementation Files:**
- `CVecCtrl.cs` - Added `JumpDown()` method, `IsJumpingDown` property
- `PlayerCharacter.cs` - Added `TryJumpDown()` method, improved `CheckFootholdLanding()`

**Usage:**
- Press Down + Alt (or Down + Jump key) while standing on a platform
- Player will fall through the platform to land on a lower platform
- Platforms marked with `cantThrough=true` in the map data cannot be jumped through

### Client Reference (from IDA Pro)
```cpp
// CUser key functions to implement:
- CUserLocal::TryDoingMeleeAttack()
- CUserLocal::TryDoingShoot()
- CUserLocal::TryDoingMagicAttack()
- CAvatar::PrepareActionFrame()
- CAvatar::DrawCharacter()
- CBodyAnimator::Update()
```

---

## Phase 6: Audio & Polish
**Focus: Complete experience**

### 6.1 Sound System
- [ ] BGM playback (currently partial)
- [x] SFX for actions (portal, jump)
  - Portal sound: Sound.wz/Game.img/Portal
  - Jump sound: Sound.wz/Game.img/Jump
- [x] Mob SFX from Sound.wz
  - Mob-specific sounds: Sound.wz/Mob.img/{mobId}/Damage, Die
  - Loaded per-mob in LifeLoader, plays via MobItem.PlayDamageSound()/PlayDieSound()
- [ ] UI sounds from Sound.wz/UI.img
- [ ] Ambient sounds

### 6.2 UI Completion
- [x] Functional inventory display
  - InventoryUI.cs with tab system (Equip/Use/Setup/Etc/Cash)
  - Item slot grid with scrolling support
  - Meso display and item quantity indicators
  - Loaded from UI.wz/UIWindow.img/Item
- [x] Equipment window
  - EquipUI.cs with equipment slot grid
  - Slot positions for all equipment types (Ring, Pendant, Weapon, etc.)
  - Character preview area
  - Tab system for Normal/Cash/Pet equipment
  - Loaded from UI.wz/UIWindow.img/Equip
- [x] Skill window (view only)
  - SkillUI.cs with job advancement tabs (Beginner through 4th job)
  - Skill icon grid with level indicators
  - Skill point display per tab
  - Selected skill description area
  - Loaded from UI.wz/UIWindow.img/Skill
- [x] Quest log display
  - QuestUI.cs with quest state tabs (In Progress/Completed/Available)
  - Quest list with selection and scrolling
  - Progress bar for in-progress quests
  - Quest detail panel with description
  - Quest state icons from UI.wz/UIWindow.img/QuestIcon
- [x] UIWindowBase base class
  - Draggable windows with title bar
  - Close button support
  - Visibility toggle with cooldown
  - Mouse event handling
- [x] UIWindowManager for window management
  - Keyboard shortcuts: I=Inventory, E=Equipment, S=Skills, Q=Quest
  - Window focus/z-order management
  - Mouse event routing to topmost window
- [x] UIWindowLoader for WZ asset loading
  - Loads window backgrounds from UI.wz/UIWindow.img
  - Creates placeholder windows when WZ assets unavailable
  - Initializes all UI windows on map load

### 6.3 Camera System
- [x] Smooth scrolling with easing + smooth background when jumping
  - CameraController.cs with exponential smoothing (camera "chases" target)
  - Different smoothing factors: horizontal (8.0), vertical (6.0)
  - Jump smoothing: slower vertical follow (2.5) during jumps for stable camera
  - Landing catch-up: faster vertical follow (10.0) when landing
  - Dead zones: camera doesn't move until player exits 100px H / 60px V zone
  - Look-ahead: camera leads 80px in movement direction
- [x] Boundary handling improvements
  - ClampToBoundaries() properly handles maps narrower/shorter than viewport
  - Accounts for zoom level and object scaling
  - Centers camera when map dimension is smaller than viewport
- [x] Zoom controls
  - Mouse scroll wheel: zoom in/out (smooth interpolation)
  - Home key: reset zoom to 1.0
  - Zoom range: 0.5x to 2.0x
  - Smooth zoom transitions via exponential interpolation
- [x] Focus on player/mob
  - FocusOn(worldX, worldY) to focus camera on specific position
  - ClearFocus() to return to player tracking
  - Smooth blend transition between player and focus target
- [x] Screen shake integration
  - StartShake(intensity, duration) method
  - Shake offset applied to final camera position
  - Decreasing intensity over duration
- [x] Toggle controls
  - C key: toggle smooth camera on/off (fallback to legacy instant camera)
  - Tab key: toggle player control vs free camera mode
- **Implementation**: `CameraController.cs` (new file)

### 6.4 Debug Tools
- [ ] Foothold visualization
- [ ] Collision box display
- [ ] Mob AI state display
- [ ] Performance metrics overlay

---

## Phase 7: MapleLib Enhancements
**Focus: Complete WZ parsing capabilities**

### 7.1 Missing Format Support
- [ ] Canvas#Video display (`WzVideoProperty.cs` TODO)
- [ ] Raw data visualization (`WzRawDataProperty.cs` TODO)
- [ ] New image header types (0x30, 0x6C, 0xBC)
- [ ] Extended UOL types (beyond 0 and 1)

### 7.2 Write Support Completion
- [ ] `WzConvexProperty.WriteValue()` - currently NotImplementedException
- [ ] `WzNullProperty.WriteValue()` - currently NotImplementedException
- [ ] Multi-file export ("Under Construction")

### 7.3 Pre-Big Bang Compatibility
- [ ] Verify CharacterJobPreBBType integration
- [ ] Test pre-BB map loading
- [ ] Validate pre-BB image format handling

### 7.4 MS File Format
- [ ] Verify round-trip read/write (`WzMsFile.cs` TODO)
- [ ] Document Snow2 cipher behavior
- [ ] Handle edge cases in entry verification

---

## Technical Debt & Maintenance

### Known Issues to Fix
1. `MapSimulator.cs:2594` - GDI+ JPEG save error
2. TexturePool unbounded growth
3. Spatial grid only active >100 objects
4. Frame delays not synced across resolution scales
5. Spine animation TimeScale hardcoded at 0.1x
6. Mirror reflection hardcoded 200px height

### Code Quality
- [ ] Add unit tests for physics calculations
- [ ] Add integration tests for WZ parsing
- [ ] Document public APIs
- [ ] Performance profiling and optimization

---

## Dependencies & Prerequisites

### External Libraries
- MonoGame/XNA Framework (current)
- Spine-CSharp runtime (current)
- NAudio for sound (current)
- Consider: ImageSharp for better image handling

### Reference Materials
- MapleStory v95 PDB (via IDA Pro MCP)
- WZ file format documentation
- Physics constants from Map.wz/Physics.img

### IDA Pro MCP Connection
The IDA Pro MCP uses JSON-RPC over HTTP on localhost:13337
Example: `curl -s -X POST http://localhost:13337/mcp -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"get_function_by_name","params":["FunctionName"],"id":1}'`

---

## Progress Tracking

### Phase Status
| Phase | Status | Progress |
|-------|--------|----------|
| Phase 1 | In Progress | 95% |
| Phase 2 | Complete | 95% |
| Phase 3 | In Progress | 90% |
| Phase 4 | Complete | 100% |
| Phase 5 | In Progress | 75% |
| Phase 6 | In Progress | 75% |
| Phase 7 | Not Started | 0% |

### Completed Tasks
- [x] **TexturePool TTL Cleanup** (Phase 1.1) - Implemented automatic texture eviction after 5 min TTL
- [x] **Screen Tremble Effect** (Phase 2.1) - Implemented Effect_Tremble matching client behavior
- [x] **Fade/Flash Effects** (Phase 2.1) - Added screen fade and flash capabilities
- [x] **Motion Blur Effect** (Phase 2.1) - Directional blur with horizontal/vertical helpers
- [x] **Explosion Effect** (Phase 2.1) - Expanding ring with element variants (fire/ice/dark)
- [x] **Advanced Animation Types** (Phase 2.2) - AnimationEffects.cs with:
  - ONETIMEINFO: One-shot animations with fade support
  - REPEATINFO: Looping animations with duration control
  - CHAINLIGHTNINGINFO: Jagged lightning bolts with glow
  - FALLINGINFO: Gravity-based falling with drift
  - FOLLOWINFO: Target-tracking animations
- [x] **CActionFrame System** (Phase 2.3) - Sprite composition with:
  - Layer management with z-ordering
  - Merge/MergeGroup for sprite compositing
  - UpdateMBR for bounding rectangle optimization
  - UpdateVisibility for view frustum culling
  - ActionMapper for action-to-frame mapping
- [x] **Particle System** (Phase 2.4) - ParticleSystem.cs with:
  - Generic emitter framework with pooling
  - Weather presets: Rain, Snow, Leaves
  - Burst effects: Sparkles, Smoke
  - Gravity, wind, rotation, sway physics
  - Fade and scale over lifetime
- [x] **CVecCtrl Physics Class** (Phase 1.2) - Full physics controller matching client:
  - IsOnFoothold(), IsOnLadder(), IsOnRope(), IsStopped(), IsSwimming()
  - SetImpactNext() knockback with velocity accumulation
  - Jump physics with gravity, terminal velocity
  - Ladder/rope climbing support
- [x] **Object Pooling** (Phase 1.4) - Generic ObjectPool<T>, ListPool, HashSetPool, StringBuilderPool
- [x] **Mob Knockback System** (Phase 4) - Integrated knockback into MobMovementInfo:
  - ApplyKnockback() with velocity accumulation matching CVecCtrl
  - Knockback physics with gravity and air drag
- [x] **CField_ContiMove Transportation System** (Phase 3.3) - TransportationField.cs:
  - Full state machine matching client (Idle, Moving, Docked, Appearing, Disappearing)
  - Ship kinds: Regular (moves x0→x) and Balrog type (alpha fade appear/disappear)
  - Packet handling: OnStartShipMoveField, OnMoveField, OnEndShipMoveField
  - LeaveShipMove, EnterShipMove, AppearShip, DisappearShip functions
  - Balrog attack event system with separate BalrogState machine
  - Texture loading from contimove.img with mob sprite fallback
  - DrawBackground rendering for precise ship/Balrog positioning
- [x] **Field Effects System** (Phase 3.1) - FieldEffects.cs with:
  - OnFieldEffect() handling for various field effect types
  - OnBlowWeather() for weather effects with messages
  - WEATHERMSGINFO weather message display system
  - Fear effect system (InitFearEffect, OnFearEffect, DrawFearEffect)
  - Field obstacles on/off with transitions
  - Screen flash, grey screen, damage mist effects
  - [4] Fear effect test key, [5] Weather message test key
- [x] **NPC Hover Cursor** (Phase 3.1) - Mouse cursor changes when hovering over NPCs:
  - ContainsScreenPoint() method in NpcItem for collision detection
  - SetMouseCursorMovedToNpc() in MouseCursorItem
  - NPC hover cursor loaded from UI.wz cursor style 4
- [x] **Dynamic Foothold System** (Phase 3.2) - DynamicFootholdSystem.cs with:
  - Horizontal moving platforms with bounds and pause delay
  - Vertical moving platforms with bounds and pause delay
  - Waypoint platforms following custom paths with looping
  - Timed spawn/despawn platforms with fade effects
  - Collision detection for platform surface
  - Debug visualization (F5 mode)
  - Test keys: [6] H-Platform, [7] V-Platform, [8] Timed, [9] Waypoint

- [x] **Limited View Field System** (Phase 3.4) - LimitedViewField.cs with:
  - Circle, Rectangle, Spotlight, Flashlight view modes
  - Fog of war overlay with gradient soft edges
  - Smooth radius and alpha transitions
  - Pulse effect for spotlight mode
  - Follow player or fixed center options
  - Test key [0] to cycle through modes

- [x] **Special Effect Fields System** (Phase 3.6) - SpecialEffectFields.cs with:
  - **CField_Wedding**: Wedding ceremony effects (packets 379, 380)
    - OnWeddingProgress for wedding step dialogs
    - OnWeddingCeremonyEnd for bless sparkle effect
    - Map detection: 680000110 (Cathedral), 680000210 (Chapel)
    - Sparkle particles between bride/groom positions
  - **CField_Witchtower**: Score tracking UI (packet 358)
    - OnScoreUpdate for updating score display
    - CScoreboard_Witchtower-style UI with progress bar
    - Score animation and goal tracking
  - **CField_GuildBoss**: Healer/pulley mechanics (packets 344, 345)
    - OnHealerMove for vertical NPC movement
    - OnPulleyStateChange for interaction state
    - CHealer class with animation and heal particles
    - CPulley class with interaction area
  - **CField_Massacre**: Kill counting and gauge (packet 173)
    - OnMassacreIncGauge for gauge updates
    - Timer with countdown display
    - Gauge decay over time (m_nGaugeDec)
    - Kill combo tracking with timeout
    - Clear effect on completion

- [x] **Event/Minigame Fields** (Phase 3.5) - MinigameFields.cs with:
  - **CField_SnowBall**: Snowball fight minigame (packets 338-341)
    - SnowBall class: Move(dx) with rotation, Win(), SetPos(), Update()
    - SnowMan class: HP, Hit(damage), stun mechanics, HP bars
    - DamageInfo queue for delayed damage effects
    - GameState: NotStarted → Active → TeamInZone → TeamWin
    - Debug/simulation mode for local testing
  - **CField_Coconut**: Coconut harvest minigame (packets 342-343)
    - Coconut physics: gravity, rotation, velocity
    - State machine: OnTree → Falling → Claimed → Scored
    - HitInfo queue for delayed state changes
    - Timer-based game with score tracking
    - EndGame() with win detection

- [x] **Knockback Bounds Clamping** (Phase 5.9a) - CVecCtrl.cs improvements:
  - Added `IsInKnockback`, `KnockbackMinX`, `KnockbackMaxX` properties
  - `ApplyPendingImpact()` records foothold bounds before leaving platform
  - `UpdateAirMovement()` clamps X position to foothold bounds during knockback
  - `UpdateGroundMovement()` applies bounds clamping for ground knockback
  - Knockback state auto-clears after 30 ticks (~0.5 seconds)
  - Voluntary jump clears knockback state (allows free movement)
  - Ladder/rope grab clears knockback state
  - Prevents player from being knocked off platforms by monster attacks

- [x] **Foothold Transition System** (Phase 5.9a) - PlayerCharacter.cs improvements:
  - Added `CheckFootholdTransition()` to handle walking between connected footholds
  - Detects when player walks past current foothold X bounds
  - Searches for adjacent foothold and transitions to it
  - Falls only when no adjacent foothold exists (actual edge)
  - Foothold lookup now finds footholds slightly above player (10px tolerance)
  - Fixed `TryJump()` to use `Physics.Jump()` for proper knockback clearing

- [x] **Official Client Physics Constants** (Phase 5.9a) - CVecCtrl.cs & PlayerCharacter.cs:
  - Values from Map/Physics.img confirmed via IDA Pro decompilation of CWvsPhysicalSpace2D
  - Gravity: 2000 px/s² → 0.556 px/tick² (gravityAcc)
  - Jump velocity: 555 px/s → 9.25 px/tick (jumpSpeed)
  - Terminal velocity: 670 px/s → 11.17 px/tick (fallSpeed)
  - Walk speed: 100 px/s at 100% → 1.67 px/tick (walkSpeed, official formula)
  - Climb speed: ~100 px/s → 1.7 px/tick (adjusted from 2.0)
  - Jump formula: vy = -(dJumpSpeed * walkJump / g) where walkJump is character stat

- [x] **Player Respawn at Spawn Point** (Phase 5.7) - MapSimulator.cs fixes:
  - Fixed player spawn to use portal StartPoint position instead of viewfinder center
  - Changed `InitializePlayerManager(viewCenterX, viewCenterY)` to `InitializePlayerManager(spawnX, spawnY)`
  - Fixed R key respawn to use stored spawn point instead of recalculating from camera position
  - Previously Y was wildly off (-47467, 44213, etc.) due to camera offset in calculation
  - Now spawns at actual map portal locations (StartPoint type)

- [x] **Weapon Loading from Character.wz** (Phase 5.9a) - CharacterLoader.cs:
  - Added `LoadWeaponAnimations()` for weapon-specific structure parsing
  - Added `LoadWeaponAnimation()` to handle action/frame/weapon or action/weapon/frame
  - Parses weapon info: attackSpeed, incPAD, twoHanded properties
  - Loads all standard actions (stand, walk, jump, etc.) plus attack actions
  - Debug output shows loaded action counts per weapon
  - `LoadDefaultMale()` now equips beginner sword (ID 1302000)
  - CharacterAssembler renders weapon via MAP_HAND anchor point with z-ordering

- [x] **Walk Speed Tuning & GM Fly Mode** (Phase 5.9a) - PlayerCharacter.cs:
  - Reduced BASE_WALK_SPEED: 2.1 → 1.4 px/tick for more authentic feel
  - Reduced WALK_ACCELERATION: 0.8 → 0.4 for smoother movement
  - Increased WALK_DECELERATION: 0.85 → 0.90 for less abrupt stops
  - Added `GmFlyMode` property - free flying ignoring physics
  - G key toggles GM fly mode (handled in PlayerManager.cs)
  - GM_FLY_SPEED = 8.0 px/tick for fast map exploration
  - Fly mode uses arrow keys for 8-directional movement
  - Displays jump animation while flying

### Last Updated
2026-01-02 - Phase 2.1: Portal Fade Effects (Screen fade on portal enter/exit)
  - **FEATURE**: Implemented portal fade matching official MapleStory client behavior
  - **IDA Pro Analysis**: Investigated CStage::FadeIn (0x718cd0) and CStage::FadeOut (0x7192b0)
    - CStage::FadeIn called from: CField::Init, CCashShop::Init, CITC::Init, CLogin::Init, CWvsContext::OnStageChange
    - CStage::FadeOut called from: CMapLoadable::Close, CCashShop::Close, CITC::Close, CWvsContext::OnStageChange
    - Duration: 600ms normal, 300ms for fast travel mode (checked via CWvsContext + 3604)
    - Uses IWzGr2D::GetredTone/GetgreenBlueTone with IWzVector2D::RelMove for smooth animation
  - **ScreenEffects.cs improvements**:
    - Fixed UpdateFade() interpolation (was lerping from current to target, now lerps from start to target)
    - Added _fadeStartAlpha to properly track starting alpha for interpolation
    - Added _fadeCompleteCallback for completion notification
    - Added ForceFadeComplete() to skip animation
    - Added IsFadeOutComplete/IsFadeInComplete properties
    - Updated FadeIn/FadeOut methods with onComplete callback parameter
  - **MapSimulator.cs implementation**:
    - Added PORTAL_FADE_DURATION_MS (600ms) and PORTAL_FADE_DURATION_FAST_MS (300ms) constants
    - Added PortalFadeState enum (None, FadingOut, FadingIn) for state machine
    - Portal activation now triggers FadeOut instead of immediate map change
    - Map change only occurs after FadeOut completes (screen fully black)
    - FadeIn starts after map content loads
    - Initial map load also triggers FadeIn (matching CField::Init)
    - DrawScreenEffects now draws fade overlay when FadeAlpha > 0 (not just when active)
    - **BUG FIX**: Called UpdateFade() explicitly during fade-out phase (return early skipped normal update location)
    - **BUG FIX**: Fade overlay now drawn AFTER UI elements via separate DrawPortalFadeOverlay() method
      - Moved from DrawScreenEffects (drawn before UI) to after chat/before cursor
      - Ensures fade covers everything including minimap, status bar, and tooltips
  - Files modified: ScreenEffects.cs, MapSimulator.cs, plan.md

Previous: 2026-01-01 - Phase 5.10g: Mob Sound Effects and Death Animation Fixes
  - **BUG FIX**: WzUOLProperty causing null sounds in LifeLoader.cs
    - Sound properties may be WzUOLProperty links instead of WzBinaryProperty
    - Added link resolution: `(prop as WzUOLProperty)?.LinkValue as WzBinaryProperty`
    - Now properly loads Damage and Die sounds for all mobs
  - **FEATURE**: `MobItem.ApplyDamage()` wrapper method
    - Calls `AI.TakeDamage()` and automatically plays damage/die sounds
    - Updated all damage call sites (PlayerManager, PlayerCombat, SkillManager)
    - Eliminates need to manually call sound methods after damage
  - **BUG FIX**: Dead mobs continued moving after death
    - `UpdateMovement()` now returns early when `AI.IsDead`
    - Still calls `UpdateAnimationAction()` to show death animation
  - **BUG FIX**: Mobs stuck in death animation forever
    - Added `_deathAnimationCompleted` flag and `IsDeathAnimationComplete` property
    - Flag set when death animation plays through all frames
    - Works for both single-frame and multi-frame animations
    - `SetAction()` now resets `_lastFrameSwitchTime` and `_deathAnimationCompleted`
  - **BUG FIX**: Dead mobs not moved to dying pool
    - `MobPool.Update()` now scans `_activeMobs` for dead mobs
    - Moves dead mobs to `_dyingMobs` list automatically
    - Previously only `KillMob()` moved mobs, but damage didn't call it
  - **IMPROVEMENT**: Mobs disappear immediately when death animation completes
    - `MobPool.Update()` checks `IsDeathAnimationComplete` instead of fixed timer
    - Removes mob as soon as animation finishes, not after arbitrary delay
  - Files modified: LifeLoader.cs, MobItem.cs, MobPool.cs, PlayerManager.cs, PlayerCombat.cs, SkillManager.cs

Previous: 2026-01-01 - Phase 5.10h: Character Jump Down Functionality
  - **FEATURE**: Implemented jump down (fall through platform) matching official client behavior
  - **How to use**: Press Down + Alt (Jump key) while standing on a platform
  - **CVecCtrl.JumpDown()**: New method to initiate jump down
    - Sets `FallStartFoothold` to current foothold (prevents immediate re-landing)
    - Clears `CurrentFoothold` to start falling
    - Sets `IsJumpingDown = true` to distinguish from normal jumps
    - Small initial downward velocity (50 px/s) to begin descent
    - Clears knockback state since this is a voluntary action
  - **CVecCtrl.IsJumpingDown**: New flag to track jump-down state
    - Set to true in `JumpDown()`
    - Cleared in `LandOnFoothold()` and `Reset()`
    - Used by landing detection to distinguish normal jump from jump-down
  - **PlayerCharacter.TryJumpDown()**: Handle Down+Jump input combo
    - Checks `CantThrough` property before allowing jump down
    - Platforms with `cantThrough=true` cannot be jumped through
    - Calls `Physics.JumpDown()` to initiate falling
  - **CheckFootholdLanding() improvement**: Better landing detection for jump down
    - Landing on different foothold: always allowed immediately
    - Normal jump on same foothold: land when at or below foothold Y (original behavior)
    - Jump-down on same foothold: requires falling at least 30 pixels below it
    - Uses `IsJumpingDown` flag to select correct landing behavior
  - **BUG FIX**: Character was falling through ground after normal jumps
    - Initial implementation broke normal jumping by requiring 30px fall for all same-foothold landings
    - Fixed by using `IsJumpingDown` flag to apply 30px requirement only for jump-down
  - Files modified: `CVecCtrl.cs`, `PlayerCharacter.cs`

Previous: 2026-01-01 - Phase 5.10g: Monster HP Bar Implementation
  - **FEATURE**: Added regular monster HP bar and boss HP bar like the official server
  - **Regular Monster HP Bar** (MobHPBarDisplay class):
    - Displays above damaged mobs at the mob's top boundary
    - 60px wide, 5px tall red bar with dark background
    - Fades to orange when HP < 30%
    - Auto-fades out after 5 seconds of no damage
    - Smooth alpha fade during last 500ms
  - **Boss HP Bar** (BossHPBarDisplay class):
    - Displays at top of screen (centered, 400px wide, 20px tall)
    - Shows boss name above the bar
    - HP text below (current/max or percentage for high HP bosses)
    - Animated HP drain effect (orange trail shows recent damage)
    - Gold border with dark background
    - Supports multiple simultaneous boss bars (stacked)
  - **Integration**:
    - `CombatEffects.OnMobDamaged(mob, time)` triggers HP bar display
    - `CombatEffects.SyncHPBarsFromMobPool()` keeps bars synced with mob states
    - Attack methods (melee, magic) now call `OnMobDamaged`
    - Mobs die and show death animation when HP <= 0
    - Death triggers `MobPool.KillMob()` and `AddDeathEffectForMob()`
    - HP bar removed on mob death via `RemoveMobHPBar()`
  - **BUG FIX**: Mob death during iteration
    - Fixed collection modification during foreach iteration
    - Mobs queued for death in `mobsToKill` list, killed after loop completes
    - Prevents InvalidOperationException when killing mobs mid-attack
  - **BUG FIX**: Death animation now plays correctly
    - MobItem.UpdateAnimationAction() now checks death/hit states first
    - Death state takes priority over movement-based animation
    - Properly switches to "die1" animation when AI.State is Death
  - **BUG FIX**: Removed mobs no longer drawn
    - DrawMobs() skips mobs with AI.State == Removed
    - UpdateMobMovement() skips removed mobs
    - MobPool.Update() updates dying mobs' AI for death timer progression
  - **FEATURE**: Knockback on hit
    - Melee attacks apply 6-12 force knockback scaled by damage
    - Magic attacks apply 4-8 force knockback (less than melee)
    - Knockback direction based on player facing
    - Dead mobs don't receive knockback
  - Files modified:
    - CombatEffects.cs - Added MobHPBarDisplay, BossHPBarDisplay, drawing methods
    - SkillManager.cs - Added HP bar notifications, death handling, knockback
    - MapSimulator.cs - Added boss HP bar to UI draw, HP bar sync, removed mob skip
    - MobItem.cs - Added `GetCurrentFrame()`, fixed death animation priority
    - MobPool.cs - Added dying mob AI update for death timer

Previous: 2026-01-01 - Phase 6.3b: Camera Landing Delay Timer
  - **IMPROVEMENT**: Added 0.6 second landing delay after falling/jumping
    - Camera now uses slow catch-up speed for 0.6 seconds after landing
    - Prevents jarring fast camera snap immediately after landing
    - More faithful to official MapleStory client camera behavior
  - Implementation:
    - Added `_landingDelayTimer` field (0.6 second duration)
    - Timer starts when player transitions from air to ground
    - While timer active, uses slower vSmoothFactor (0.02f-0.06f based on distance)
    - After timer expires, normal ground speed resumes
  - Files modified: CameraController.cs

Previous: 2026-01-01 - Phase 5.10f: Random Attack Key (C Key) Implementation
  - **FEATURE**: Added 'C' key to activate random attack type for testing
  - **BUG FIX**: Attack methods now work without Skill.wz loaded
    - Added fallback attack methods directly in PlayerManager
    - TryDoingBasicMeleeAttack, TryDoingBasicShoot, TryDoingBasicMagicAttack
    - Works when SkillManager is null
  - **BUG FIX**: TriggerSkillAnimation now sets _animationStartTime
    - Previously attack would never complete because _animationStartTime wasn't set
    - Fixed case sensitivity in action name matching (swingo1 vs swingO1)
  - **BUG FIX**: UpdateStateMachine now handles missing attack animations
    - If attack animation not found, returns to Standing after 300ms
    - Added debug logging for attack state transitions
  - New methods in SkillManager.cs:
    - `TryDoingMeleeAttack()` - Basic melee attack (80x60 hitbox, up to 3 mobs, swingO1 animation)
    - `TryDoingShoot()` - Basic projectile attack (speed 8.0f, 2000ms lifetime, shoot1 animation)
    - `TryDoingMagicAttack()` - Magic attack (120x80 hitbox, 10 MP cost, 20% crit, single target)
    - `TryDoingRandomAttack()` - Randomly calls one of the above three attack methods
    - `CalculateBasicDamage()` - Base damage calculation with weapon attack and variance
    - `CreateBasicProjectileData()` - Creates default projectile settings
  - New methods in PlayerManager.cs:
    - `TryDoingRandomAttack()` - Wrapper to SkillManager method
    - `TryDoingMeleeAttack()` - Wrapper to SkillManager method
    - `TryDoingShoot()` - Wrapper to SkillManager method
    - `TryDoingMagicAttack()` - Wrapper to SkillManager method
  - MapSimulator.cs changes:
    - 'C' key now triggers `TryDoingRandomAttack()` when player is active
    - 'Shift+C' key now toggles smooth camera (moved from 'C' key)
  - Files modified: SkillManager.cs, PlayerManager.cs, MapSimulator.cs

Previous: 2026-01-01 - Phase 6.3: Camera System with Smooth Scrolling and Zoom
  - **FEATURE**: Implemented complete camera controller for smooth gameplay experience
  - New file created:
    - `CameraController.cs` - Smooth scrolling, zoom, focus, and shake effects
  - Key features:
    - Exponential smoothing for camera following (camera "chases" target)
    - Different smoothing factors for horizontal (8.0) and vertical (6.0) movement
    - Jump smoothing: slower vertical follow (2.5) during jumps for stable camera
    - Landing catch-up: faster vertical follow (10.0) when landing
    - Dead zones: camera doesn't move until player exits 100px H / 60px V zone
    - Look-ahead: camera leads 80px in movement direction
    - Mouse scroll wheel zoom (0.5x to 2.0x range)
    - Home key to reset zoom to 1.0
    - Focus on position feature for cutscenes/boss fights
    - Screen shake integration
    - Toggle between smooth and legacy instant camera (C key)
  - Integration:
    - CameraController initialized on map load with field boundaries
    - Update loop in MapSimulator.Update() uses controller for position
    - Teleportation uses controller's TeleportTo() for instant snap
    - Help text updated with new controls
  - Files modified:
    - MapSimulator.cs - Integrated CameraController
    - PlayerManager.cs - Added IsPlayerOnGround(), IsPlayerFacingRight()

Previous: 2026-01-01 - Phase 6.2: UI Windows Implementation (Inventory, Equipment, Skills, Quest)
  - **FEATURE**: Implemented complete UI window system for MapSimulator
  - New files created:
    - `UIWindowBase.cs` - Base class for draggable, closeable windows
    - `InventoryUI.cs` - Inventory display with 5 tabs (Equip/Use/Setup/Etc/Cash)
    - `EquipUI.cs` - Equipment window with slot grid and character preview
    - `SkillUI.cs` - Skill window (view-only) with job advancement tabs
    - `QuestUI.cs` - Quest log with progress tracking and state tabs
    - `UIWindowManager.cs` - Window lifecycle and keyboard shortcut management
    - `UIWindowLoader.cs` - WZ asset loading for UI windows
  - Key features:
    - Keyboard shortcuts: I=Inventory, E=Equipment, S=Skills, Q=Quest
    - Draggable windows with title bar and close button
    - Window focus/z-order management (click to bring to front)
    - Tab switching within windows
    - Scrollable content areas
    - Placeholder windows when WZ assets unavailable
  - Integration:
    - Windows initialized on map load via UIWindowLoader.CreateUIWindowManager()
    - Update loop integrated in MapSimulator.Update()
    - Draw and mouse events integrated in MapSimulator.DrawUI()
  - Files modified:
    - MapSimulator.cs - Added UIWindowManager field and integration

Previous: 2026-01-01 - Phase 5.9a: Jump sound effect when character jumps
  - **FEATURE**: Added jump sound effect from Sound.wz/Game.img/Jump
  - Implementation:
    - Added `jumpSE` field to MapSimulator.cs (WzSoundResourceStreamer)
    - Load jump sound from `Sound.wz/Game.img/Jump` during initialization
    - Added `PlayJumpSE()` method in MapSimulator.cs
    - Added sound callback system in PlayerCharacter/PlayerManager:
      - `_onJumpSound` callback field in PlayerCharacter
      - `SetJumpSoundCallback(Action onJump)` in PlayerCharacter and PlayerManager
      - Callback invoked in `TryJump()` after successful jump
    - Wired up in `InitializePlayerManager()` via `_playerManager.SetJumpSoundCallback(PlayJumpSE)`
  - **BUG FIX**: Fixed WzSoundResourceStreamer.Play() to properly replay sounds
    - Previous: Sound only played once, subsequent calls did nothing
    - Issue: NAudio WaveOut player needs stream reset after playback finishes
    - Solution: Modified Play() to call Stop(), seek to beginning, then Play()
    - Only resets if not currently playing (allows overlapping sounds)
  - Files changed: MapSimulator.cs, PlayerCharacter.cs, PlayerManager.cs, WzSoundResourceStreamer.cs

Previous: 2026-01-01 - Phase 5.9a: Foothold snapping on portal spawn (prevent falling)
  - **BUG FIX**: Player now snaps to nearest foothold below portal when teleporting
  - Problem: Portal position might be above a platform, causing player to fall
  - Solution: Added foothold snapping in PlayerManager:
    - `TeleportTo()` now finds foothold at/below position and snaps player to it
    - `Respawn()` and `RespawnAt()` now call `SnapToFoothold()` after respawn
    - Added `SnapToFoothold()` helper method to find and land on nearest foothold
    - Uses `_findFoothold(x, y, 500)` to search 500px below for platforms
    - Calculates exact Y position on foothold using linear interpolation
    - Calls `Player.Physics.LandOnFoothold(fh)` to properly land on platform
  - Files changed: PlayerManager.cs (TeleportTo, Respawn, RespawnAt, SnapToFoothold)

Previous: 2026-01-01 - Phase 5.9a: Portal teleportation spawn at target portal position
  - **BUG FIX**: Player now spawns at the exact target portal position when teleporting
  - Previous bug: `LoadMapContent()` was using view center calculation instead of `spawnX/spawnY`
    - Old: `InitializePlayerManager(-mapShiftX + RenderWidth/2, -mapShiftY + RenderHeight/2)`
    - Fixed: `InitializePlayerManager(spawnX, spawnY)` where spawnX/spawnY = target portal coords
  - Same-map teleportation now also moves player to target portal:
    - Added `_playerManager?.TeleportTo(targetPortal.X, targetPortal.Y)`
    - Added `_playerManager?.SetSpawnPoint(targetPortal.X, targetPortal.Y)`
  - Portal spawn logic (in priority order):
    1. Target portal from `_spawnPortalName` (portal.pn == _spawnPortalName)
    2. Random StartPoint portal if no target portal specified
    3. Field boundary center as fallback
  - Files changed: MapSimulator.cs (lines 1206-1208, 1683-1685)

Previous: 2026-01-01 - Phase 5.9a: Portal UP key teleportation (matching official client)
  - Player can press UP arrow when near a portal to teleport (same as double-clicking)
  - Added `HandlePortalUpInteract()` method in MapSimulator.cs (lines 2522-2621)
  - Uses `InputAction.Interact` (bound to UP key) from PlayerInput
  - Portal proximity detection: 40px horizontal, 60px vertical range
  - Checks both visible portals (`_portalsArray`) and hidden portals (`mapBoard.BoardItems.Portals`)
  - Finds nearest portal if multiple are in range (Manhattan distance)
  - Triggers same teleportation logic as double-click (PlayPortalSE, _pendingMapChange)
  - Called from input handling section right after `HandlePortalDoubleClick()`
  - Files changed: MapSimulator.cs

Previous: 2026-01-01 - Phase 5.9a: Fix character animation timing and feet offset
  - **Feet offset fix**: Player Y = feet position, character rendered with navel at Y
    - Added `FeetOffset` property to `AssembledFrame` (distance from navel to feet)
    - `Draw()` applies: `adjustedY = screenY - FeetOffset`
    - Updated hitbox offset from -30 to -60
  - **Animation timing fix**: Frames were playing too fast (100ms instead of 200-300ms)
    - Delay was read from body sub-part canvas instead of frame node
    - `LoadBodyFrameWithSubParts()` now reads delay from `frameSub["delay"]`
    - Default delay changed from 100ms to 200ms
  - Files changed: CharacterAssembler.cs, CharacterLoader.cs, PlayerCharacter.cs

Previous: 2026-01-01 - Phase 5.9a: Fixed physics system and walk speed tuning

  **BUG FIX: Direct position modification bypassing physics**
  - Found MapSimulator.cs lines 1722-1743 were directly modifying `player.Physics.X/Y`
    with hardcoded speed (4-8 px/frame = 240-480 px/s at 60fps)
  - This completely bypassed the physics system in PlayerCharacter.ProcessInput()
  - Removed direct position modification, now uses physics-based movement properly

  **Walk Speed Tuning (from IDA Pro + MapleNecrocer reference)**
  - Official CalcWalk formula: `vMax = walkSpeed * (dWalkSpeed * footholdDrag)`
  - With dWalkSpeed=1250 from Physics.img and walkSpeed scaling
  - MapleNecrocer reference: 2.5 px/frame at 60fps = 150 px/s max
  - Final tuning: WalkSpeedScale = 1.25, giving Speed 100 = 125 px/s

  **Jump/Gravity Tuning (MapleNecrocer reference)**
  - Raw Physics.img values were too high for natural feel:
    - jumpSpeed: 1555 → tuned to 555 px/s
    - gravityAcc: 3000 → tuned to 2000 px/s²
    - fallSpeed: 1670 → tuned to 670 px/s
  - Based on MapleNecrocer: JumpHeight=9.5, JumpSpeed=0.6, MaxFallSpeed=8 per frame

  **Deceleration Tuning (responsive stopping)**
  - Official client uses dWalkDrag=10000 → 100 px/s² deceleration (slow, 1.25s to stop)
  - MapleNecrocer uses 0.25 px/frame² = ~900 px/s² (responsive)
  - Tuned to 80000 → 800 px/s² deceleration (stops in ~0.15s)
  - Official client formula confirmed at CalcWalk line 211:
    `v27 = dMaxFriction * v26->dWalkDrag` where dMaxFriction=1.0 on normal ground

Previous: 2026-01-01 - Phase 5.9a: Corrected walking speed formula from CalcWalk analysis
  - Re-analyzed CVecCtrl::CalcWalk at 0x992ba0 for accurate speed formula
  - Key discovery at line 143-144 of CalcWalk decompilation:
    ```
    v16 = TSecType<double>::GetData(&v15->walkSpeed);  // CAttrShoe::walkSpeed
    pAttrFootholda = v16 * (v14->dWalkSpeed * drag);   // vMax calculation
    ```
  - Official formula: vMax = walkSpeed * (dWalkSpeed * footholdDrag)
  - Where walkSpeed = CAttrShoe::walkSpeed = characterSpeed / dWalkSpeed
  - This simplifies to: vMax = (Speed / 1250) * 1250 * footholdDrag = Speed * footholdDrag

Previous: 2026-01-01 - Phase 5.9a: Official client physics integration from IDA Pro analysis
  - Updated PhysicsConstants.cs with actual Physics.img raw values from v95 client:
    - walkForce: 999999, walkSpeed: 1250, walkDrag: 10000
    - gravityAcc: 3000, fallSpeed: 1670, jumpSpeed: 1555
    - slipForce: 90000, slipSpeed: 420
    - swimForce: 320000, swimSpeed: 440, swimSpeedDec: 0.1
    - flyForce: 420000, flySpeed: 600, flyJumpDec: 0.15
    - floatDrag1: 300000, floatDrag2: 30000, floatCoefficient: 0.03
    - maxFriction: 10, minFriction: 0.2
  - Discovered CAttrShoe default values from IDA Pro (0x50b710):
    - mass: 100.0, walkAcc: 1.0, walkSpeed: 1.0, walkDrag: 1.0
    - These are the base shoe attributes for a "naked" character
  - Added official client physics functions from IDA Pro decompilation:
    - CVecCtrl.AccSpeed(ref v, force, mass, vMax, tSec) - acceleration formula
    - CVecCtrl.DecSpeed(ref v, drag, mass, vMax, tSec) - deceleration formula
    - Formula: v += (force / mass) * tSec, clamped to vMax
  - Updated PlayerCharacter.cs to use official AccSpeed/DecSpeed functions
  - Physics.img is loaded dynamically at MapSimulator initialization via LoadPhysicsConstants()
  - Source: IDA Pro analysis of CVecCtrl::CalcWalk, AccSpeed, DecSpeed functions at 0x992ba0, 0x990850, 0x9908c0

Previous: 2026-01-01 - Phase 5.9a: Fix camera centering on player
  - Fixed Update() camera follow formula that was missing centering calculation
  - Previous incorrect: `mapShiftX = playerPos.X` (player at left edge of screen)
  - Correct formula: `mapShiftX = playerPos.X + _mapCenterX - RenderWidth/2`
  - Based on rendering formula: `screenX = worldX - mapShiftX + centerX`
  - Now matches GM fly mode (lines 1737-1738) for consistent camera behavior
  - Location: MapSimulator.cs Update() around line 2052-2053

Previous: 2026-01-01 - Phase 5.9a: Fix missing right hand in character
  - Body frames now load all sub-parts (body, arm, lHand, rHand) instead of just "body"
  - Added CharacterSubPart class with NavelOffset for relative positioning
  - CharacterFrame extended with SubParts list and HasSubParts property
  - CharacterLoader.LoadBodyFrameWithSubParts() loads all canvases as sub-parts
  - CharacterAssembler.AddPart() iterates and renders all sub-parts
  - Each sub-part positioned using formula: offset + navelOffset - origin

Previous: 2026-01-01 - Weapon position fix (coordinate transformation)
  - Fixed weapon rendered at wrong position (was at leg, now at hand)
  - Arm's hand point must be transformed to body's coordinate system:
    `hand_in_body = body.navel - arm.navel + arm.hand`
  - CharacterLoader now properly calculates hand/handMove in body coords
  - CharacterAssembler tries hand -> handMove -> navel+offset fallback
  - Weapon hidden on ladder/rope if no climbing animation exists

---

## Notes from Reverse Engineering Session

### Key Client Classes Discovered
- **CVecCtrl**: Physics/movement controller with foothold, ladder/rope support
- **CAnimationDisplayer**: Central animation system with 15+ effect types
- **CActionFrame**: Sprite composition with MAPINFO structures
- **CSpriteInstance**: Individual sprite rendering
- **CMob/CMobPool**: Mob management with attack/damage structures
- **CField variants**: 20+ specialized field types for different gameplay

### Physics Constants from Map.wz/Physics.img (v95)
Raw values stored in Physics.img (before character attribute scaling):
```
Walking: walkForce=999999, walkSpeed=1250, walkDrag=10000
Slipping: slipForce=90000, slipSpeed=420
Floating: floatDrag1=300000, floatDrag2=30000, floatCoefficient=0.03
Swimming: swimForce=320000, swimSpeed=440, swimSpeedDec=0.1
Flying: flyForce=420000, flySpeed=600, flyJumpDec=0.15
Gravity: gravityAcc=3000, fallSpeed=1670, jumpSpeed=1555
Friction: maxFriction=10, minFriction=0.2
```

### Scaled Gameplay Values (Official Client Formula)
```
Walk speed: 100 px/s (Speed stat = px/s, official CalcWalk simplification)
  Formula: maxSpeed = (characterSpeed / dWalkSpeed) * dWalkSpeed * footholdDrag = characterSpeed * footholdDrag
  With Speed 100 and footholdDrag 1.0: maxSpeed = 100 px/s
  Note: MapleNecrocer uses 150 px/s (1.5x faster than official)
Walk acceleration: ~10000 px/s² (walkForce 999999 / mass 100, near-instant)
Walk deceleration: ~800 px/s² (walkDrag 80000 / mass 100, responsive stopping)
Gravity: 2000 px/s² (tuned from raw 3000)
Jump velocity: 555 px/s (tuned from raw 1555)
Terminal velocity: 670 px/s (tuned from raw 1670)
Screen tremble: 1500ms (heavy) / 2000ms (normal)
Tremble reduction: 0.85 (heavy) / 0.92 (normal)
```

### Official Client Physics Formulas (from IDA Pro)
```cpp
// From CVecCtrl::CalcWalk at 0x992ba0
tSec = tElapse / 1000.0;  // milliseconds to seconds
force = inputDir * walkForce * footholdDrag * characterWalkAcc * fieldWalk;
maxSpeed = walkSpeed * footholdDrag * characterWalkSpeed;

// AccSpeed at 0x990850 - Accelerate towards maxSpeed
void AccSpeed(double* v, double force, double mass, double vMax, double tSec) {
    if (vMax >= 0) {
        if (force > 0 && vMax > *v) {
            *v += (force / mass) * tSec;
            if (*v > vMax) *v = vMax;
        } else if (force <= 0 && -vMax < *v) {
            *v += (force / mass) * tSec;
            if (*v < -vMax) *v = -vMax;
        }
    }
}

// DecSpeed at 0x9908c0 - Decelerate towards 0
void DecSpeed(double* v, double drag, double mass, double vMax, double tSec) {
    if (vMax >= 0) {
        if (vMax < *v) {
            *v -= (drag / mass) * tSec;
            if (*v < vMax) *v = vMax;
        } else if (-vMax > *v) {
            *v += (drag / mass) * tSec;
            if (*v > -vMax) *v = -vMax;
        }
    }
}

// Position update - trapezoidal integration
pos += (v_old + v_new) * 0.5 * tSec;
```

### Effect System Architecture
The client uses a list-based effect system where each effect type has:
- Add/Remove operations
- Update(deltaTime) method
- Draw integration
- Reference counting for cleanup

### Proper Character Physics (CVecCtrl Implementation)

The `CVecCtrl` class in `MapSimulator/CVecCtrl.cs` implements accurate physics matching the MapleStory client:

**Core Physics Constants:**
```
GravityAcceleration = 0.556 px/tick²    (2000 px/s² at 60fps)
JumpVelocity = 9.25 px/tick             (555 px/s initial jump)
TerminalVelocity = 11.17 px/tick        (670 px/s max fall)
AirDrag = 0.98                          (horizontal slowdown in air)
GroundFriction = 0.85                   (horizontal slowdown on ground)
ClimbSpeed = 2.0 px/tick                (ladder/rope movement)
SwimSpeedFactor = 0.7                   (speed reduction in water)
```

**State Management:**
- `CurrentFoothold` - foothold entity is standing on (null = airborne)
- `FallStartFoothold` - foothold where fall started (for drop-through)
- `IsOnLadderOrRope` - climbing state with ladder bounds
- `CurrentJumpState` - None/Jumping/Falling for animation
- `FacingRight` - direction for sprite flipping

**Knockback System:**
- `SetImpactNext(vx, vy)` - queue knockback with accumulation
- `ApplyPendingImpact()` - applies queued knockback, records foothold bounds
- `IsInKnockback` - prevents falling off platform during knockback
- `KnockbackMinX/MaxX` - horizontal bounds from original foothold
- Auto-clears after 30 ticks or on voluntary jump/ladder grab

**Movement Updates:**
- `UpdateGroundMovement()` - friction, foothold following, edge detection
- `UpdateAirMovement()` - gravity, terminal velocity, knockback clamping
- `UpdateLadderMovement()` - vertical movement within ladder bounds

**Key Methods:**
- `Jump()` - initiates jump, clears knockback state
- `LandOnFoothold(fh)` - lands on foothold, clears knockback
- `GrabLadder()` - starts ladder climbing, clears knockback
- `ClearKnockback()` - manually clears knockback bounds

**Foothold Transition System (PlayerCharacter.cs):**
- `CheckFootholdTransition()` - called while walking to handle foothold changes
- Detects when player X moves past current foothold X bounds
- Uses foothold lookup callback to find adjacent foothold
- Transitions to new foothold or starts falling if at actual edge
- Foothold lookup includes 10px upward tolerance for sloped connections
