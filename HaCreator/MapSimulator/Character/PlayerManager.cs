using System;
using System.Collections.Generic;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Companions;
using MapleLib.WzLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator;
using MapleLib.WzLib.WzStructure;

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
        public PetController Pets { get; }
        internal DragonCompanionRuntime Dragon { get; }
        public CompanionEquipmentController CompanionEquipment { get; }

        public bool IsPlayerActive => Player != null && Player.IsAlive;
        public bool IsPlayerControlEnabled { get; set; } = true;
        internal bool IsMovementLockedByMobStatus => _currentMobStatusState.MovementLocked;

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
        private bool _requiresFlyingSkillForMap;

        // Mob/Drop pools for combat
        private MobPool _mobPool;
        private DropPool _dropPool;
        private int _lastPickupAttemptTime = int.MinValue;

        // Combat effects reference
        private CombatEffects _combatEffects;
        private MobSkillEffectLoader _mobSkillEffectLoader;
        private SoundManager _soundManager;
        private Action<Rectangle, int, int, int> _reactorAttackAreaHandler;
        private PlayerMobStatusController _mobStatusController;
        private PlayerMobStatusFrameState _currentMobStatusState = PlayerMobStatusFrameState.Default;
        private Action<PlayerCharacter, Rectangle, int> _attackHitboxHandler;
        private int _pendingRepeatSkillModeEndSkillId;
        private int _pendingRepeatSkillModeEndReturnSkillId;
        private int _pendingRepeatSkillModeEndRequestTime = int.MinValue;

        // Sound callbacks
        private Action _onJumpSound;
        private Func<string> _jumpRestrictionMessageProvider;
        private Action<string> _onJumpRestricted;
        private Func<float, float> _moveSpeedCapResolver;

        #endregion

        #region Initialization

        public PlayerManager(GraphicsDevice device, TexturePool texturePool)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _texturePool = texturePool;

            Input = new PlayerInput();
            Config = new CharacterConfigManager();
            Pets = new PetController(device);
            Dragon = new DragonCompanionRuntime(device);
            CompanionEquipment = new CompanionEquipmentController(device);
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

            // Always create SkillLoader - it can fall back to Program.FindImage / IDataSource in IMG mode
            SkillLoader = new SkillLoader(skillWz, _device, _texturePool);
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] SkillLoader created");

            _mobSkillEffectLoader = new MobSkillEffectLoader(_device, _texturePool);
            _mobSkillEffectLoader.Initialize();

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

            Skills?.SetFootholdLookup(findFoothold);
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
        /// and whether the client-style flying-skill gate is required.
        /// </summary>
        public void SetFlyingMap(bool isFlyingMap, bool requiresFlyingSkillForMap = false)
        {
            _isFlyingMap = isFlyingMap;
            _requiresFlyingSkillForMap = requiresFlyingSkillForMap;
            if (Player != null)
            {
                Player.Physics.IsFlyingMap = isFlyingMap;
                Player.Physics.RequiresFlyingSkillForMap = requiresFlyingSkillForMap;
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

        public void SetJumpRestrictionHandler(Func<string> getRestrictionMessage, Action<string> onJumpRestricted)
        {
            _jumpRestrictionMessageProvider = getRestrictionMessage;
            _onJumpRestricted = onJumpRestricted;
            if (Player != null)
            {
                Player.SetJumpRestrictionHandler(getRestrictionMessage, onJumpRestricted);
            }
        }

        public void SetMoveSpeedCapResolver(Func<float, float> moveSpeedCapResolver)
        {
            _moveSpeedCapResolver = moveSpeedCapResolver;
            if (Player != null)
            {
                Player.SetMoveSpeedCapResolver(moveSpeedCapResolver);
            }
        }

        public void SetReactorAttackAreaHandler(Action<Rectangle, int, int, int> reactorAttackAreaHandler)
        {
            _reactorAttackAreaHandler = reactorAttackAreaHandler;
            if (Skills != null)
            {
                Skills.OnAttackAreaResolved = reactorAttackAreaHandler;
            }
        }

        public void SetAttackHitboxHandler(Action<PlayerCharacter, Rectangle, int> attackHitboxHandler)
        {
            _attackHitboxHandler = attackHitboxHandler;
        }

        public MobSkillEffectData LoadMobSkillEffect(int skillId, int skillLevel = 1)
        {
            return _mobSkillEffectLoader?.LoadMobSkillEffect(skillId, skillLevel);
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

        public void SetSoundManager(SoundManager soundManager)
        {
            _soundManager = soundManager;
            Skills?.SetSoundManager(soundManager);
        }

        public void SetCurrentMapIdProvider(Func<int> currentMapIdProvider)
        {
            Pets.SetCurrentMapIdProvider(currentMapIdProvider);
        }

        public void SetCurrentMapInfoProvider(Func<MapInfo> currentMapInfoProvider)
        {
            Dragon.SetCurrentMapInfoProvider(currentMapInfoProvider);
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
            InitializePlayer(new PlayerCharacter(build));
            CompanionEquipment.EnsureDefaults(Loader, build);

            // Create SkillManager if we have a SkillLoader
            if (SkillLoader != null)
            {
                Skills = new SkillManager(SkillLoader, Player);
                _pendingRepeatSkillModeEndSkillId = 0;
                _pendingRepeatSkillModeEndSkillId = 0;
                _pendingRepeatSkillModeEndReturnSkillId = 0;
                _pendingRepeatSkillModeEndRequestTime = int.MinValue;
                Skills.SetMobPool(_mobPool);
                Skills.SetDropPool(_dropPool);
                Skills.SetCombatEffects(_combatEffects);
                Skills.SetSoundManager(_soundManager);
                Skills.SetFootholdLookup(_findFoothold);
                Skills.SetTamingMobLoader(Loader.LoadEquipment);
                Skills.OnAttackAreaResolved = _reactorAttackAreaHandler;
                Skills.OnRepeatSkillModeEndRequested = HandleRepeatSkillModeEndRequested;
                build.SkillStatBonusProvider = stat => Skills.GetPassiveBonus(stat) + Skills.GetBuffStat(stat);
                build.SkillMasteryProvider = () => Skills.GetMastery(build.GetWeapon());

                // Keep only the player's current job path resident at startup.
                Skills.LoadSkillsForJob(build.Job);
                Skills.LearnAllNonHiddenSkills();

                System.Diagnostics.Debug.WriteLine($"[PlayerManager] SkillManager created for job path {build.Job}");
            }

            _mobStatusController = new PlayerMobStatusController(Player, Skills, TeleportToSpawn);
            Skills?.SetExternalCastBlockedEvaluator(currentTime => _currentMobStatusState.SkillCastBlocked);
            Skills?.SetExternalStateRestrictionMessageProvider(currentTime => _mobStatusController?.GetSkillCastRestrictionMessage(currentTime));

            Combat.SetDamageBlockedEvaluator(currentTime => Skills?.IsPlayerProtectedByClientSkillZone(currentTime) == true);

            // Set up callbacks
            Player.SetFootholdLookup(_findFoothold);
            Player.SetLadderLookup(_findLadder);
            Player.SetSwimAreaCheck(_checkSwimArea);
            Func<int, CharacterPart> tamingMobLoader = Loader != null
                ? new Func<int, CharacterPart>(Loader.LoadEquipment)
                : null;
            Player.SetTamingMobLoader(tamingMobLoader);
            Player.SetPortableChairTamingMobLoader(tamingMobLoader);
            Player.SetSkillMorphLoader(Loader.LoadMorph);
            Player.SetJumpSoundCallback(_onJumpSound);
            Player.SetJumpRestrictionHandler(_jumpRestrictionMessageProvider, _onJumpRestricted);
            Player.SetMoveSpeedCapResolver(_moveSpeedCapResolver);
            Player.Physics.IsFlyingMap = _isFlyingMap;
            Player.Physics.RequiresFlyingSkillForMap = _requiresFlyingSkillForMap;

            // Set up attack callback
            Player.OnAttackHitbox = (player, hitbox) =>
            {
                int currentTime = Environment.TickCount;
                if (_mobPool != null)
                {
                    var results = Combat.ProcessAttack(_mobPool, hitbox);

                    // Add damage numbers to combat effects
                    if (_combatEffects != null)
                    {
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
                }

                _attackHitboxHandler?.Invoke(player, hitbox, currentTime);
            };

            // Set up death callback
            Player.OnDeath = (player) =>
            {
                // Could trigger death effect, etc.
            };

            // Set up damage received callback - show violet damage number above player
            Player.OnDamaged = (player, damage) =>
            {
                if (_combatEffects != null && damage > 0)
                {
                    int currentTime = Environment.TickCount;
                    // Show violet damage number above player's head (NoViolet)
                    _combatEffects.AddReceivedDamage(
                        damage,
                        player.X,
                        player.Y - 50, // Above player head
                        false, // Not critical (mobs don't crit in basic implementation)
                        currentTime);
                }
            };

            // Set spawn position and snap to foothold
            TeleportTo(_spawnPoint.X, _spawnPoint.Y);
            Pets.EnsureDefaultPetActive(Player);
        }

        private void InitializePlayer(PlayerCharacter player)
        {
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Combat = new PlayerCombat(Player);

            Combat.OnAttackHitPlayer = (x, y, hitFrames) =>
            {
                if (_combatEffects != null && hitFrames != null)
                {
                    _combatEffects.AddAttackHitEffect(x, y, hitFrames, Environment.TickCount);
                }
            };

            Combat.OnMobSkillHitPlayer = (x, y, skillId, skillLevel) =>
            {
                if (_combatEffects == null || _mobSkillEffectLoader == null)
                {
                    return;
                }

                var effectData = _mobSkillEffectLoader.LoadMobSkillEffect(skillId, skillLevel);
                if (effectData?.HasAffectedEffect != true)
                {
                    return;
                }

                int duration = effectData.Time > 0 ? effectData.Time : effectData.AffectedDuration;
                _combatEffects.AddMobSkillHitEffect(
                    x,
                    y,
                    effectData.AffectedFrames,
                    skillId,
                    skillLevel,
                    Environment.TickCount,
                    effectData.AffectedRepeat,
                    duration);
            };

            Combat.OnMobSkillStatusApplied = (skillId, skillLevel, currentTime) =>
            {
                ApplyPlayerSkillBlockingStatus(skillId, skillLevel, currentTime);
            };

            Combat.OnMobAttackMissPlayer = (x, y, currentTime) =>
            {
                _combatEffects?.AddMiss(x, y, currentTime);
            };

            Combat.OnDamageReceived = (combatPlayer, damage, mob) =>
            {
                Skills?.NotifyOwnerDamaged(mob, Environment.TickCount);
            };

            Combat.SetDamageBlockedEvaluator(currentTime => Skills?.IsPlayerProtectedByClientSkillZone(currentTime) == true);

            Player.SetFootholdLookup(_findFoothold);
            Player.SetLadderLookup(_findLadder);
            Player.SetSwimAreaCheck(_checkSwimArea);
            Func<int, CharacterPart> tamingMobLoader = Loader != null
                ? new Func<int, CharacterPart>(Loader.LoadEquipment)
                : null;
            Player.SetTamingMobLoader(tamingMobLoader);
            Func<int, CharacterPart> portableChairTamingMobLoader = Loader != null
                ? new Func<int, CharacterPart>(Loader.LoadEquipment)
                : null;
            Player.SetPortableChairTamingMobLoader(portableChairTamingMobLoader);
            Player.SetJumpSoundCallback(_onJumpSound);
            Player.SetJumpRestrictionHandler(_jumpRestrictionMessageProvider, _onJumpRestricted);
            Player.SetMoveSpeedCapResolver(_moveSpeedCapResolver);
            Player.Physics.IsFlyingMap = _isFlyingMap;
            Player.Physics.RequiresFlyingSkillForMap = _requiresFlyingSkillForMap;

            Player.OnAttackHitbox = (combatPlayer, hitbox) =>
            {
                int currentTime = Environment.TickCount;
                if (_mobPool != null)
                {
                    var results = Combat.ProcessAttack(_mobPool, hitbox);
                    if (_combatEffects != null)
                    {
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
                }

                _attackHitboxHandler?.Invoke(combatPlayer, hitbox, currentTime);
            };

            Player.OnDeath = combatPlayer =>
            {
                // Could trigger death effect, etc.
            };

            Player.OnDamaged = (combatPlayer, damage) =>
            {
                if (_combatEffects != null && damage > 0)
                {
                    int currentTime = Environment.TickCount;
                    _combatEffects.AddReceivedDamage(
                        damage,
                        combatPlayer.X,
                        combatPlayer.Y - 50,
                        false,
                        currentTime);
                }
            };
        }

        private void ApplyPlayerSkillBlockingStatus(int skillId, int skillLevel, int currentTime)
        {
            if (Player == null || !PlayerSkillBlockingStatusMapper.TryMapMobSkill(skillId, out PlayerSkillBlockingStatus status))
            {
                return;
            }

            MobSkillEffectData effectData = _mobSkillEffectLoader?.LoadMobSkillEffect(skillId, Math.Max(1, skillLevel));
            int durationMs = Math.Max(0, (effectData?.Time ?? 0) * 1000);
            if (durationMs <= 0)
            {
                return;
            }

            Player.ApplySkillBlockingStatus(status, durationMs, currentTime);
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
            InitializePlayer(new PlayerCharacter(_device, _texturePool, null));

            // Set spawn position
            Player.SetPosition(_spawnPoint.X, _spawnPoint.Y);
            Pets.EnsureDefaultPetActive(Player);

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
            _mobStatusController = null;
            _currentMobStatusState = PlayerMobStatusFrameState.Default;
            Pets.Clear();
            Dragon.Clear();
        }

        #endregion

        #region Update

        /// <summary>
        /// Update player and combat
        /// </summary>
        /// <param name="currentTime">Current tick count</param>
        /// <param name="deltaTime">Delta time in seconds</param>
        /// <param name="chatIsActive">Whether chat input is active (blocks player input)</param>
        public void Update(int currentTime, float deltaTime, bool chatIsActive = false, bool inputActive = true)
        {
            // Keep input state synchronized even while unfocused so focus changes don't create false edges.
            if (inputActive && !chatIsActive)
            {
                Input.Update();
            }
            else
            {
                Input.SyncState();
            }

            if (Player == null)
                return;

            UpdateMobStatusState(currentTime);

            // If player is dead, skip all input processing and combat
            if (!Player.IsAlive)
            {
                Skills?.ReleaseActiveKeydownSkill(currentTime);
                Player.ClearInput();
                return;
            }

            // Apply input to player only if chat is not active
            if (IsPlayerControlEnabled && inputActive && !chatIsActive)
            {
                InputState inputState = Input.GetState();
                InputState playerInputState = ApplyMobStatusToInput(inputState);
                Input.ApplyToPlayer(Player, playerInputState);

                // Handle pickup input separately
                bool pickupHeld = !_currentMobStatusState.MovementLocked && Input.IsHeld(InputAction.Pickup);
                bool pickupPressed = !_currentMobStatusState.MovementLocked && Input.IsPressed(InputAction.Pickup);

                if (_dropPool != null && ShouldAttemptPickup(pickupHeld, pickupPressed, currentTime, _lastPickupAttemptTime))
                {
                    Combat?.TryPickupDrop(_dropPool, currentTime);
                    _lastPickupAttemptTime = currentTime;
                }
                else if (!pickupHeld)
                {
                    _lastPickupAttemptTime = int.MinValue;
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

                // Handle skill hotkeys
                if (Skills != null && !_currentMobStatusState.SkillCastBlocked)
                {
                    // Primary skill hotkeys (Skill1-8, slots 0-7)
                    for (int i = 0; i < 8; i++)
                    {
                        if (inputState.Skills[i])
                        {
                            Skills.TryCastHotkey(i, currentTime);
                        }

                        if (inputState.SkillsReleased[i])
                        {
                            Skills.ReleaseHotkeyIfActive(i, currentTime);
                        }
                    }

                    // Function key hotkeys (F1-F12, slots 8-19)
                    for (int i = 0; i < 12; i++)
                    {
                        if (inputState.FunctionSlots[i])
                        {
                            Skills.TryCastHotkey(SkillManager.FUNCTION_SLOT_OFFSET + i, currentTime);
                        }

                        if (inputState.FunctionSlotsReleased[i])
                        {
                            Skills.ReleaseHotkeyIfActive(SkillManager.FUNCTION_SLOT_OFFSET + i, currentTime);
                        }
                    }

                    // Ctrl+Number hotkeys (Ctrl+1-8, slots 20-27)
                    for (int i = 0; i < 8; i++)
                    {
                        if (inputState.CtrlSlots[i])
                        {
                            Skills.TryCastHotkey(SkillManager.CTRL_SLOT_OFFSET + i, currentTime);
                        }

                        if (inputState.CtrlSlotsReleased[i])
                        {
                            Skills.ReleaseHotkeyIfActive(SkillManager.CTRL_SLOT_OFFSET + i, currentTime);
                        }
                    }
                }
            }
            else
            {
                Skills?.ReleaseActiveKeydownSkill(currentTime);
                Player.ClearInput();
            }

            if (_currentMobStatusState.MovementLocked)
            {
                Player.Physics.VelocityX = 0;
            }

            // Update player
            Player.Update(currentTime, deltaTime);

            // Update skills (projectiles, buffs, cooldowns)
            Skills?.Update(currentTime, deltaTime);
            TryAcknowledgePendingRepeatSkillModeEnd(currentTime);
            Pets.Update(Player, _dropPool, currentTime, deltaTime);
            Dragon.Update(Player, currentTime);

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

            Pets.Draw(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, PetRenderPlane.BehindOwner);
            Skills?.DrawBackgroundEffects(spriteBatch, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            Dragon.Draw(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            Player.Draw(
                spriteBatch,
                skeletonRenderer,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                currentTime,
                () => Pets.Draw(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, PetRenderPlane.UnderFace));
            Pets.Draw(spriteBatch, skeletonRenderer, mapShiftX, mapShiftY, centerX, centerY, PetRenderPlane.InFrontOfOwner);

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

        public bool TryExecutePetCommand(string message, int currentTime)
        {
            return Pets.TryExecuteCommand(message, currentTime);
        }

        internal bool TryApplyMobSkillStatus(int skillId, MobSkillRuntimeData runtimeData, int currentTime, float sourceX = 0f)
        {
            if (Player == null)
            {
                return false;
            }

            if (skillId == 129)
            {
                TeleportToSpawn();
                return true;
            }

            return _mobStatusController?.TryApplyMobSkill(skillId, runtimeData, currentTime, sourceX) == true;
        }

        internal int ClearMobStatuses(IEnumerable<PlayerMobStatusEffect> effects)
        {
            return _mobStatusController?.ClearStatuses(effects) ?? 0;
        }

        internal bool HasMobStatus(PlayerMobStatusEffect effect)
        {
            return _mobStatusController?.HasStatusEffect(effect) == true;
        }

        private void HandleRepeatSkillModeEndRequested(int skillId, int returnSkillId)
        {
            _pendingRepeatSkillModeEndSkillId = skillId;
            _pendingRepeatSkillModeEndReturnSkillId = returnSkillId;
            _pendingRepeatSkillModeEndRequestTime = Environment.TickCount;
        }

        private void TryAcknowledgePendingRepeatSkillModeEnd(int currentTime)
        {
            if (Skills == null || _pendingRepeatSkillModeEndRequestTime == int.MinValue || currentTime <= _pendingRepeatSkillModeEndRequestTime)
            {
                return;
            }

            int skillId = _pendingRepeatSkillModeEndSkillId;
            int returnSkillId = _pendingRepeatSkillModeEndReturnSkillId;
            if (Skills.TryAcknowledgeRepeatSkillModeEndRequest(skillId, currentTime)
                || Skills.TryAcknowledgeRepeatSkillModeEndRequest(returnSkillId, currentTime))
            {
                _pendingRepeatSkillModeEndSkillId = 0;
                _pendingRepeatSkillModeEndReturnSkillId = 0;
                _pendingRepeatSkillModeEndRequestTime = int.MinValue;
            }
        }

        private void UpdateMobStatusState(int currentTime)
        {
            _currentMobStatusState = _mobStatusController?.Update(currentTime) ?? PlayerMobStatusFrameState.Default;

            Player?.ApplyMobRecoveryModifiers(
                _currentMobStatusState.HpRecoveryReversed,
                _currentMobStatusState.MaxHpPercentCap,
                _currentMobStatusState.MaxMpPercentCap);
            Player?.SetExternalMoveSpeedMultiplier(_currentMobStatusState.MoveSpeedMultiplier);
            Combat?.SetAdditionalPlayerMissChance(_currentMobStatusState.AdditionalMissChance);
        }

        private InputState ApplyMobStatusToInput(InputState inputState)
        {
            if (_currentMobStatusState.MovementLocked)
            {
                inputState.Left = false;
                inputState.Right = false;
                inputState.Up = false;
                inputState.Down = false;
                inputState.Jump = false;
                inputState.JumpPressed = false;
                inputState.Attack = false;
                inputState.AttackPressed = false;
                inputState.Pickup = false;
                inputState.PickupPressed = false;
                return inputState;
            }

            if (_currentMobStatusState.InputReversed)
            {
                (inputState.Left, inputState.Right) = (inputState.Right, inputState.Left);
                (inputState.Up, inputState.Down) = (inputState.Down, inputState.Up);
            }

            if (_currentMobStatusState.ForcedHorizontalDirection != 0)
            {
                inputState.Left = _currentMobStatusState.ForcedHorizontalDirection < 0;
                inputState.Right = _currentMobStatusState.ForcedHorizontalDirection > 0;
                inputState.Up = false;
                inputState.Down = false;
                inputState.Attack = false;
                inputState.AttackPressed = false;
                inputState.Pickup = false;
                inputState.PickupPressed = false;
            }

            if (_currentMobStatusState.JumpBlocked)
            {
                inputState.Jump = false;
                inputState.JumpPressed = false;
            }

            return inputState;
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
        /// Force the player to standing state and clear all movement.
        /// Used when entering portals, interacting with objects, etc.
        /// </summary>
        public void ForceStand()
        {
            Player?.ForceStand();
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

        public void TeleportToSpawn()
        {
            if (Player == null)
            {
                return;
            }

            Player.ForceStand();
            TeleportTo(_spawnPoint.X, _spawnPoint.Y);
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

            Player.Recover(hp, mp);
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
            Skills?.UpdateBasicMeleeAfterImageState("swingO1", currentTime);
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
                _reactorAttackAreaHandler?.Invoke(worldHitbox, currentTime, 0, 1);

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
                        mob.ApplyDamage(damage, currentTime, damage > 100, Player.X, Player.Y, damageType: MobDamageType.Physical);
                        ApplyMobReflectDamage(mob, currentTime, MobDamageType.Physical);
                        Vector2 damageAnchor = mob.GetDamageNumberAnchor();

                        _combatEffects?.AddDamageNumber(
                            damage,
                            damageAnchor.X,
                            damageAnchor.Y,
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
                _reactorAttackAreaHandler?.Invoke(worldHitbox, currentTime, 0, 1);

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

                    closestMob.ApplyDamage(damage, currentTime, isCritical, Player.X, Player.Y, damageType: MobDamageType.Magical);
                    ApplyMobReflectDamage(closestMob, currentTime, MobDamageType.Magical);
                    Vector2 damageAnchor = closestMob.GetDamageNumberAnchor();

                    _combatEffects?.AddDamageNumber(
                        damage,
                        damageAnchor.X,
                        damageAnchor.Y,
                        isCritical,
                        false,
                        currentTime,
                        0);
                }
            }

            return true;
        }

        private void ApplyMobReflectDamage(MobItem mob, int currentTime, MobDamageType damageType)
        {
            if (mob?.AI == null || Player == null || !Player.IsAlive)
            {
                return;
            }

            int reflectedDamage = mob.AI.CalculateReflectedDamageToAttacker(mob.AI.LastDamageTaken, damageType);
            if (reflectedDamage > 0)
            {
                Player.TakeDamage(reflectedDamage, 0f, 0f);
            }
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
            _requiresFlyingSkillForMap = false;
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

            _lastPickupAttemptTime = int.MinValue;

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
            bool requiresFlyingSkillForMap,
            MobPool mobPool,
            DropPool dropPool,
            CombatEffects combatEffects)
        {
            SetFootholdLookup(findFoothold);
            SetLadderLookup(findLadder);
            SetSwimAreaCheck(checkSwimArea);
            SetFlyingMap(isFlyingMap, requiresFlyingSkillForMap);
            SetMobPool(mobPool);
            SetDropPool(dropPool);
            SetCombatEffects(combatEffects);

            // Reconnect skill manager references
            Skills?.SetMobPool(mobPool);
            Skills?.SetDropPool(dropPool);
            Skills?.SetCombatEffects(combatEffects);
            Skills?.SetSoundManager(_soundManager);
        }

        private static bool ShouldAttemptPickup(bool pickupHeld, bool pickupPressed, int currentTime, int lastPickupAttemptTime)
        {
            if (!pickupHeld)
            {
                return false;
            }

            if (pickupPressed || lastPickupAttemptTime == int.MinValue)
            {
                return true;
            }

            return currentTime - lastPickupAttemptTime >= DropItem.PICKUP_DURATION;
        }

        #endregion
    }
}
