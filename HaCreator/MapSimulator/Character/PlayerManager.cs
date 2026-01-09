using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Animation;
using MapleLib.WzLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using HaCreator.MapSimulator.Effects;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Player Manager - Integrates player character system with MapSimulator
    /// </summary>
    public class PlayerManager
    {
        #region Properties

        public PlayerCharacter Player { get; private set; }
        public PlayerInput Input { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public CharacterLoader Loader { get; private set; }
        public CharacterConfigManager Config { get; private set; }
        public SkillManager Skills { get; private set; }
        public SkillLoader SkillLoader { get; private set; }

        public bool IsPlayerActive => Player != null && Player.IsAlive;
        public bool IsPlayerControlEnabled { get; set; } = true;

        // Spawn point
        private Vector2 _spawnPoint;

        // References
        private readonly GraphicsDevice _device;
        private readonly TexturePool _texturePool;
        private WzFile _characterWz;
        private WzFile _skillWz;

        // Foothold system callback
        private Func<float, float, float, FootholdLine> _findFoothold;
        private Func<float, float, float, (int x, int top, int bottom, bool isLadder)?> _findLadder;
        private Func<float, float, float, bool> _checkSwimArea;
        private bool _isFlyingMap;

        // Mob/Drop pools for combat
        private MobPool _mobPool;
        private DropPool _dropPool;

        // Combat effects reference
        private CombatEffects _combatEffects;

        // Sound callbacks
        private Action _onJumpSound;

        #endregion

        #region Initialization

        public PlayerManager(GraphicsDevice device, TexturePool texturePool)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _texturePool = texturePool;

            Input = new PlayerInput();
            Config = new CharacterConfigManager();
        }

        /// <summary>
        /// Initialize with Character.wz (can be null - will use Program.FindImage fallback)
        /// </summary>
        public void Initialize(WzFile characterWz)
        {
            Initialize(characterWz, null);
        }

        /// <summary>
        /// Initialize with Character.wz and Skill.wz
        /// </summary>
        public void Initialize(WzFile characterWz, WzFile skillWz)
        {
            _characterWz = characterWz;
            _skillWz = skillWz;

            System.Diagnostics.Debug.WriteLine($"[PlayerManager] Initialize called with Character.wz: {characterWz?.Name ?? "NULL"}, Skill.wz: {skillWz?.Name ?? "NULL"}");

            // Always create CharacterLoader - it can use Program.FindImage as fallback
            Loader = new CharacterLoader(characterWz, _device, _texturePool);
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] CharacterLoader created (will use Program.FindImage if WzFile is null)");

            // Create SkillLoader if Skill.wz is available
            if (skillWz != null)
            {
                SkillLoader = new SkillLoader(skillWz, _device, _texturePool);
                System.Diagnostics.Debug.WriteLine($"[PlayerManager] SkillLoader created");
            }

            Config.LoadAllPresets();
        }

        /// <summary>
        /// Set foothold lookup callback
        /// </summary>
        public void SetFootholdLookup(Func<float, float, float, FootholdLine> findFoothold)
        {
            _findFoothold = findFoothold;
            if (Player != null)
            {
                Player.SetFootholdLookup(findFoothold);
            }
        }

        /// <summary>
        /// Set ladder lookup callback
        /// </summary>
        public void SetLadderLookup(Func<float, float, float, (int x, int top, int bottom, bool isLadder)?> findLadder)
        {
            _findLadder = findLadder;
            if (Player != null)
            {
                Player.SetLadderLookup(findLadder);
            }
        }

        /// <summary>
        /// Set swim area check callback
        /// </summary>
        public void SetSwimAreaCheck(Func<float, float, float, bool> checkSwimArea)
        {
            _checkSwimArea = checkSwimArea;
            if (Player != null)
            {
                Player.SetSwimAreaCheck(checkSwimArea);
            }
        }

        /// <summary>
        /// Set whether this is a flying map (fly=true in map info)
        /// </summary>
        public void SetFlyingMap(bool isFlyingMap)
        {
            _isFlyingMap = isFlyingMap;
            if (Player != null)
            {
                Player.Physics.IsFlyingMap = isFlyingMap;
            }
        }

        /// <summary>
        /// Get the current foothold lookup callback
        /// </summary>
        public Func<float, float, float, FootholdLine> GetFootholdLookup() => _findFoothold;

        /// <summary>
        /// Get the current ladder lookup callback
        /// </summary>
        public Func<float, float, float, (int x, int top, int bottom, bool isLadder)?> GetLadderLookup() => _findLadder;

        /// <summary>
        /// Get the current swim area check callback
        /// </summary>
        public Func<float, float, float, bool> GetSwimAreaCheck() => _checkSwimArea;

        /// <summary>
        /// Set jump sound callback (called when player jumps)
        /// </summary>
        public void SetJumpSoundCallback(Action onJump)
        {
            _onJumpSound = onJump;
            if (Player != null)
            {
                Player.SetJumpSoundCallback(onJump);
            }
        }

        /// <summary>
        /// Set mob pool for combat
        /// </summary>
        public void SetMobPool(MobPool mobPool)
        {
            _mobPool = mobPool;
        }

        /// <summary>
        /// Set drop pool for pickup
        /// </summary>
        public void SetDropPool(DropPool dropPool)
        {
            _dropPool = dropPool;
        }

        /// <summary>
        /// Set combat effects for damage display
        /// </summary>
        public void SetCombatEffects(CombatEffects combatEffects)
        {
            _combatEffects = combatEffects;
        }

        /// <summary>
        /// Set spawn point
        /// </summary>
        public void SetSpawnPoint(float x, float y)
        {
            _spawnPoint = new Vector2(x, y);
        }

        #endregion

        #region Player Creation

        /// <summary>
        /// Create player from preset
        /// </summary>
        public bool CreatePlayer(int presetId)
        {
            var preset = Config.GetPreset(presetId);
            if (preset == null || Loader == null)
                return false;

            var build = preset.ToBuild(Loader);
            if (build == null)
                return false;

            CreatePlayerFromBuild(build);
            return true;
        }

        /// <summary>
        /// Create player from build
        /// </summary>
        public void CreatePlayerFromBuild(CharacterBuild build)
        {
            Player = new PlayerCharacter(build);
            Combat = new PlayerCombat(Player);

            // Wire up attack hit effect callback
            Combat.OnAttackHitPlayer = (x, y, hitFrames) =>
            {
                if (_combatEffects != null && hitFrames != null)
                {
                    _combatEffects.AddAttackHitEffect(x, y, hitFrames, Environment.TickCount);
                }
            };

            // Create SkillManager if we have a SkillLoader
            if (SkillLoader != null)
            {
                Skills = new SkillManager(SkillLoader, Player);
                Skills.SetMobPool(_mobPool);
                Skills.SetCombatEffects(_combatEffects);

                // Load beginner skills (job 0) and set them all to max level for testing
                Skills.LoadSkillsForJob(0);
                foreach (var skill in Skills.GetActiveSkills())
                {
                    Skills.SetSkillLevel(skill.SkillId, skill.MaxLevel);
                }

                System.Diagnostics.Debug.WriteLine($"[PlayerManager] SkillManager created, loaded beginner skills");
            }

            // Set up callbacks
            Player.SetFootholdLookup(_findFoothold);
            Player.SetLadderLookup(_findLadder);
            Player.SetSwimAreaCheck(_checkSwimArea);
            Player.SetJumpSoundCallback(_onJumpSound);
            Player.Physics.IsFlyingMap = _isFlyingMap;

            // Set up attack callback
            Player.OnAttackHitbox = (player, hitbox) =>
            {
                if (_mobPool == null) return;

                var results = Combat.ProcessAttack(_mobPool, hitbox);

                // Add damage numbers to combat effects
                if (_combatEffects != null)
                {
                    int currentTime = Environment.TickCount;
                    int comboIndex = 0;
                    foreach (var result in results)
                    {
                        _combatEffects.AddDamageNumber(
                            result.Damage,
                            result.HitX,
                            result.HitY - 20,
                            result.IsCritical,
                            result.IsMiss,
                            currentTime,
                            comboIndex++);
                    }
                }
            };

            // Set up death callback
            Player.OnDeath = (player) =>
            {
                // Could trigger death effect, etc.
            };

            // Set up damage received callback
            Player.OnDamaged = (player, damage) =>
            {
                // Could show damage number above player
            };

            // Set spawn position and snap to foothold
            TeleportTo(_spawnPoint.X, _spawnPoint.Y);
        }

        /// <summary>
        /// Create default player
        /// </summary>
        public bool CreateDefaultPlayer()
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] CreateDefaultPlayer called, Loader: {Loader != null}");
            if (Loader == null)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayerManager] No Loader - cannot create default player");
                return false;
            }

            var build = Loader.LoadDefaultMale();
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] LoadDefaultMale returned: {build != null}");
            if (build == null)
                return false;

            CreatePlayerFromBuild(build);
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] Player created from build");
            return true;
        }

        /// <summary>
        /// Create random player
        /// </summary>
        public bool CreateRandomPlayer()
        {
            if (Loader == null)
                return false;

            var build = Loader.LoadRandom();
            if (build == null)
                return false;

            CreatePlayerFromBuild(build);
            return true;
        }

        /// <summary>
        /// Create a placeholder player without Character.wz (for testing/debugging)
        /// </summary>
        public bool CreatePlaceholderPlayer()
        {
            // Create player with null build - will just be a position marker
            Player = new PlayerCharacter(_device, _texturePool, null);

            // Set up callbacks
            if (_findFoothold != null)
                Player.SetFootholdLookup(_findFoothold);
            if (_findLadder != null)
                Player.SetLadderLookup(_findLadder);
            if (_onJumpSound != null)
                Player.SetJumpSoundCallback(_onJumpSound);
            if (_checkSwimArea != null)
                Player.SetSwimAreaCheck(_checkSwimArea);
            Player.Physics.IsFlyingMap = _isFlyingMap;

            // Set spawn position
            Player.SetPosition(_spawnPoint.X, _spawnPoint.Y);

            System.Diagnostics.Debug.WriteLine($"Placeholder player created at ({_spawnPoint.X}, {_spawnPoint.Y})");
            return true;
        }

        /// <summary>
        /// Remove player
        /// </summary>
        public void RemovePlayer()
        {
            Player = null;
            Combat = null;
        }

        #endregion

        #region Update

        /// <summary>
        /// Update player and combat
        /// </summary>
        /// <param name="currentTime">Current tick count</param>
        /// <param name="deltaTime">Delta time in seconds</param>
        /// <param name="chatIsActive">Whether chat input is active (blocks player input)</param>
        public void Update(int currentTime, float deltaTime, bool chatIsActive = false)
        {
            // Update input only if chat is not active
            if (!chatIsActive)
            {
                Input.Update();
            }

            if (Player == null)
                return;

            // If player is dead, skip all input processing and combat
            if (!Player.IsAlive)
            {
                Player.ClearInput();
                return;
            }

            // Apply input to player only if chat is not active
            if (IsPlayerControlEnabled && !chatIsActive)
            {
                Input.ApplyToPlayer(Player);

                // Handle pickup input separately
                if (Input.IsPressed(InputAction.Pickup) && _dropPool != null)
                {
                    Combat?.TryPickupDrop(_dropPool, currentTime);
                }

                // Handle GM fly mode toggle (G key)
                if (Input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.G))
                {
                    Player.SetGmFlyToggle(true);
                }

                // Handle God mode toggle (H key)
                if (Input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.H))
                {
                    Player.ToggleGodMode();
                }

                // Handle skill hotkeys (Skill1-8)
                if (Skills != null)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        if (Input.IsPressed(InputAction.Skill1 + i))
                        {
                            Skills.TryCastHotkey(i, currentTime);
                        }
                    }
                }
            }
            else
            {
                Player.ClearInput();
            }

            // Update player
            Player.Update(currentTime, deltaTime);

            // Update skills (projectiles, buffs, cooldowns)
            Skills?.Update(currentTime, deltaTime);

            // Check mob attacks
            if (Combat != null && _mobPool != null)
            {
                Combat.CheckMobAttacks(_mobPool, currentTime);
                Combat.CheckTouchDamage(_mobPool, currentTime);
            }
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw player
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (Player == null)
                return;

            // Draw invincibility flash effect
            if (Combat != null && Combat.IsInvincible(currentTime))
            {
                // Player.Draw handles flash internally
            }

            Player.Draw(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, currentTime);

            // Draw skill effects and projectiles
            if (Skills != null)
            {
                Skills.DrawProjectiles(spriteBatch, mapShiftX, mapShiftY, centerX, centerY, currentTime);
                Skills.DrawEffects(spriteBatch, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        /// <summary>
        /// Draw player UI (HP/MP bars)
        /// </summary>
        public void DrawUI(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (Player == null)
                return;

            int screenX = (int)Player.X - mapShiftX + centerX;
            int screenY = (int)Player.Y - mapShiftY + centerY;

            Player.DrawStatusBars(spriteBatch, screenX, screenY);
        }

        #endregion

        #region Respawn

        /// <summary>
        /// Respawn player at spawn point
        /// </summary>
        public void Respawn()
        {
            if (Player == null)
                return;

            // Respawn resets HP/state, then snap to foothold
            Player.Respawn(_spawnPoint.X, _spawnPoint.Y);
            SnapToFoothold(_spawnPoint.X, _spawnPoint.Y);

            // Set invincibility after respawn
            Combat?.SetInvincible(Environment.TickCount);
        }

        /// <summary>
        /// Respawn at specific position
        /// </summary>
        public void RespawnAt(float x, float y)
        {
            if (Player == null)
                return;

            // Respawn resets HP/state, then snap to foothold
            Player.Respawn(x, y);
            SnapToFoothold(x, y);
            Combat?.SetInvincible(Environment.TickCount);
        }

        /// <summary>
        /// Snap player position to nearest foothold below
        /// </summary>
        private void SnapToFoothold(float x, float y)
        {
            if (Player == null || _findFoothold == null)
                return;

            var fh = _findFoothold(x, y, 500);
            if (fh != null)
            {
                float dx = fh.SecondDot.X - fh.FirstDot.X;
                float dy = fh.SecondDot.Y - fh.FirstDot.Y;
                float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
                float fhY = fh.FirstDot.Y + t * dy;
                Player.SetPosition(x, fhY);
                Player.Physics.LandOnFoothold(fh);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Get player position
        /// </summary>
        public Vector2 GetPlayerPosition()
        {
            return Player?.Position ?? _spawnPoint;
        }

        /// <summary>
        /// Get player hitbox
        /// </summary>
        public Rectangle GetPlayerHitbox()
        {
            return Player?.GetHitbox() ?? Rectangle.Empty;
        }

        /// <summary>
        /// Get whether player is on ground (not jumping/falling)
        /// </summary>
        public bool IsPlayerOnGround()
        {
            return Player?.Physics?.IsOnFoothold() ?? true;
        }

        /// <summary>
        /// Get whether player is facing right
        /// </summary>
        public bool IsPlayerFacingRight()
        {
            return Player?.FacingRight ?? true;
        }

        /// <summary>
        /// Check if player is near a position
        /// </summary>
        public bool IsPlayerNear(float x, float y, float range)
        {
            return Player?.IsInRange(x, y, range) ?? false;
        }

        /// <summary>
        /// Teleport player to position, snapping to nearest foothold below
        /// </summary>
        public void TeleportTo(float x, float y)
        {
            if (Player == null) return;

            // Find a foothold at or below the target position to prevent falling
            // Search up to 500 pixels below the portal to find a platform
            float snappedY = y;
            if (_findFoothold != null)
            {
                var fh = _findFoothold(x, y, 500); // Large search range to find platform below
                if (fh != null)
                {
                    // Calculate Y position on the foothold at X
                    float dx = fh.SecondDot.X - fh.FirstDot.X;
                    float dy = fh.SecondDot.Y - fh.FirstDot.Y;
                    float t = (dx != 0) ? (x - fh.FirstDot.X) / dx : 0;
                    float fhY = fh.FirstDot.Y + t * dy;
                    snappedY = fhY;

                    // Set the foothold on the physics controller
                    Player.Physics.LandOnFoothold(fh);
                    System.Diagnostics.Debug.WriteLine($"[TeleportTo] Snapped from Y={y} to foothold Y={snappedY}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[TeleportTo] No foothold found at ({x}, {y})");
                }
            }

            Player.SetPosition(x, snappedY);
        }

        /// <summary>
        /// Set player stats
        /// </summary>
        public void SetStats(int level, int maxHp, int maxMp, int attack, int defense)
        {
            if (Player?.Build == null)
                return;

            Player.Build.Level = level;
            Player.Build.MaxHP = maxHp;
            Player.Build.MaxMP = maxMp;
            Player.Build.HP = maxHp;
            Player.Build.MP = maxMp;
            Player.Build.Attack = attack;
            Player.Build.Defense = defense;
        }

        /// <summary>
        /// Heal player
        /// </summary>
        public void Heal(int hp, int mp)
        {
            if (Player == null)
                return;

            Player.HP = Math.Min(Player.MaxHP, Player.HP + hp);
            Player.MP = Math.Min(Player.MaxMP, Player.MP + mp);
        }

        /// <summary>
        /// Kill player (for testing)
        /// </summary>
        public void KillPlayer()
        {
            Player?.Die();
        }

        // Random for attack selection
        private static readonly Random _attackRandom = new Random();

        /// <summary>
        /// Try doing a random attack (melee, shoot, or magic)
        /// Called when 'C' key is pressed
        /// </summary>
        public bool TryDoingRandomAttack(int currentTime)
        {
            if (Player == null)
                return false;

            // If SkillManager is available, use it
            if (Skills != null)
                return Skills.TryDoingRandomAttack(currentTime);

            // Otherwise, use basic attack directly on player
            int attackType = _attackRandom.Next(3);
            return attackType switch
            {
                0 => TryDoingBasicMeleeAttack(currentTime),
                1 => TryDoingBasicShoot(currentTime),
                2 => TryDoingBasicMagicAttack(currentTime),
                _ => TryDoingBasicMeleeAttack(currentTime)
            };
        }

        /// <summary>
        /// Try doing a melee attack
        /// </summary>
        public bool TryDoingMeleeAttack(int currentTime)
        {
            if (Player == null)
                return false;

            if (Skills != null)
                return Skills.TryDoingMeleeAttack(currentTime);

            return TryDoingBasicMeleeAttack(currentTime);
        }

        /// <summary>
        /// Try doing a shooting attack
        /// </summary>
        public bool TryDoingShoot(int currentTime)
        {
            if (Player == null)
                return false;

            if (Skills != null)
                return Skills.TryDoingShoot(currentTime);

            return TryDoingBasicShoot(currentTime);
        }

        /// <summary>
        /// Try doing a magic attack
        /// </summary>
        public bool TryDoingMagicAttack(int currentTime)
        {
            if (Player == null)
                return false;

            if (Skills != null)
                return Skills.TryDoingMagicAttack(currentTime);

            return TryDoingBasicMagicAttack(currentTime);
        }

        /// <summary>
        /// Basic melee attack - works without SkillManager
        /// </summary>
        private bool TryDoingBasicMeleeAttack(int currentTime)
        {
            // Trigger swing animation
            Player.TriggerSkillAnimation("swingO1");
            System.Diagnostics.Debug.WriteLine($"[Attack] TryDoingBasicMeleeAttack - swingO1 triggered");

            // Apply damage to nearby mobs if mob pool is available
            if (_mobPool != null)
            {
                int hitWidth = 80;
                int hitHeight = 60;
                int offsetX = Player.FacingRight ? 10 : -(hitWidth + 10);

                var worldHitbox = new Rectangle(
                    (int)Player.X + offsetX,
                    (int)Player.Y - hitHeight - 10,
                    hitWidth,
                    hitHeight);

                int hitCount = 0;
                foreach (var mob in _mobPool.ActiveMobs)
                {
                    if (hitCount >= 3) break;
                    if (mob?.AI == null || mob.AI.State == MobAIState.Death) continue;

                    var mobHitbox = new Rectangle(
                        (int)(mob.MovementInfo?.X ?? 0) - 20,
                        (int)(mob.MovementInfo?.Y ?? 0) - 50,
                        40, 50);

                    if (worldHitbox.Intersects(mobHitbox))
                    {
                        int damage = CalculateBasicDamage();
                        mob.ApplyDamage(damage, currentTime, damage > 100, Player.X, Player.Y);

                        _combatEffects?.AddDamageNumber(
                            damage,
                            mob.MovementInfo?.X ?? 0,
                            (mob.MovementInfo?.Y ?? 0) - 30,
                            damage > 100,
                            false,
                            currentTime,
                            hitCount);

                        hitCount++;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Basic shooting attack - works without SkillManager
        /// </summary>
        private bool TryDoingBasicShoot(int currentTime)
        {
            // Trigger shoot animation
            Player.TriggerSkillAnimation("shoot1");
            System.Diagnostics.Debug.WriteLine($"[Attack] TryDoingBasicShoot - shoot1 triggered");
            // Note: Projectile spawning requires SkillManager
            return true;
        }

        /// <summary>
        /// Basic magic attack - works without SkillManager
        /// </summary>
        private bool TryDoingBasicMagicAttack(int currentTime)
        {
            // Trigger magic animation
            Player.TriggerSkillAnimation("swingO1");
            System.Diagnostics.Debug.WriteLine($"[Attack] TryDoingBasicMagicAttack - swingO1 triggered");

            // Apply damage to closest mob if mob pool is available
            if (_mobPool != null)
            {
                int hitWidth = 120;
                int hitHeight = 80;
                int offsetX = Player.FacingRight ? 20 : -(hitWidth + 20);

                var worldHitbox = new Rectangle(
                    (int)Player.X + offsetX,
                    (int)Player.Y - hitHeight - 10,
                    hitWidth,
                    hitHeight);

                MobItem closestMob = null;
                float closestDist = float.MaxValue;

                foreach (var mob in _mobPool.ActiveMobs)
                {
                    if (mob?.AI == null || mob.AI.State == MobAIState.Death) continue;

                    var mobHitbox = new Rectangle(
                        (int)(mob.MovementInfo?.X ?? 0) - 20,
                        (int)(mob.MovementInfo?.Y ?? 0) - 50,
                        40, 50);

                    if (worldHitbox.Intersects(mobHitbox))
                    {
                        float dist = Math.Abs((mob.MovementInfo?.X ?? 0) - Player.X);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestMob = mob;
                        }
                    }
                }

                if (closestMob != null)
                {
                    int damage = CalculateBasicDamage() + 50;
                    bool isCritical = _attackRandom.Next(100) < 20;
                    if (isCritical) damage = (int)(damage * 1.5f);

                    closestMob.ApplyDamage(damage, currentTime, isCritical, Player.X, Player.Y);

                    _combatEffects?.AddDamageNumber(
                        damage,
                        closestMob.MovementInfo?.X ?? 0,
                        (closestMob.MovementInfo?.Y ?? 0) - 30,
                        isCritical,
                        false,
                        currentTime,
                        0);
                }
            }

            return true;
        }

        /// <summary>
        /// Calculate basic attack damage
        /// </summary>
        private int CalculateBasicDamage()
        {
            int baseAttack = Player.Build?.Attack ?? 10;
            var weapon = Player.Build?.GetWeapon();
            if (weapon != null)
            {
                baseAttack += weapon.Attack;
            }

            float variance = 0.9f + (float)_attackRandom.NextDouble() * 0.2f;
            return Math.Max(1, (int)(baseAttack * variance));
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Full cleanup - clears everything including caches.
        /// Use when completely disposing the player system.
        /// </summary>
        public void Clear()
        {
            RemovePlayer();
            Loader?.ClearCache();
            SkillLoader?.ClearCache();
            Skills?.Clear();
        }

        /// <summary>
        /// Prepare for map change - clears map-specific state but preserves:
        /// - Player character instance and appearance
        /// - Character/Skill loader caches (textures, animations)
        /// - Skill levels, hotkeys, and configuration
        /// - HP/MP and other persistent stats
        /// </summary>
        public void PrepareForMapChange()
        {
            // Clear only map-specific references
            _findFoothold = null;
            _findLadder = null;
            _checkSwimArea = null;
            _isFlyingMap = false;
            _mobPool = null;
            _dropPool = null;

            // Clear active skill state (projectiles, hit effects) but keep skill levels/hotkeys
            Skills?.ClearMapState();

            // Reset player physics state but keep character
            if (Player != null)
            {
                Player.Physics?.Reset();
                Player.ClearInput();
            }

            // Combat reference will be re-established
            // Note: We intentionally do NOT clear:
            // - Player (character appearance, stats)
            // - Loader cache (character textures)
            // - SkillLoader cache (skill textures)
            // - Skills (skill levels, hotkeys, cooldowns)
            // - Config (presets)
        }

        /// <summary>
        /// Re-establish map-specific callbacks after map change.
        /// Called after PrepareForMapChange() when loading new map.
        /// </summary>
        public void ReconnectToMap(
            Func<float, float, float, FootholdLine> findFoothold,
            Func<float, float, float, (int x, int top, int bottom, bool isLadder)?> findLadder,
            Func<float, float, float, bool> checkSwimArea,
            bool isFlyingMap,
            MobPool mobPool,
            DropPool dropPool,
            CombatEffects combatEffects)
        {
            SetFootholdLookup(findFoothold);
            SetLadderLookup(findLadder);
            SetSwimAreaCheck(checkSwimArea);
            SetFlyingMap(isFlyingMap);
            SetMobPool(mobPool);
            SetDropPool(dropPool);
            SetCombatEffects(combatEffects);

            // Reconnect skill manager references
            Skills?.SetMobPool(mobPool);
            Skills?.SetCombatEffects(combatEffects);
        }

        #endregion
    }
}
