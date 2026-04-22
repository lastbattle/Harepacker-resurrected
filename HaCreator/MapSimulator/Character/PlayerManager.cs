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
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Player Manager - Integrates player character system with MapSimulator
    /// </summary>
    public class PlayerManager
    {
        private const int AffectedAreaAvatarEffectIdBase = int.MinValue;
        private const int DragonFurySkillId = 22160000;
        private const string WeaponSfxAttackSoundName = "Attack";
        internal const int ClientPickupRepeatDelayMs = 30;
        private const float DragonVecCtrlLayerSearchRange = int.MaxValue;

        private sealed class AffectedAreaAvatarEffectCacheEntry
        {
            public int Signature { get; init; }
            public SkillData Skill { get; init; }
        }

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
        private Func<DragonCompanionRuntime.OwnerPhaseContext> _dragonOwnerPhaseContextProvider;
        private bool _isFlyingMap;
        private bool _requiresFlyingSkillForMap;

        // Mob/Drop pools for combat
        private MobPool _mobPool;
        private DropPool _dropPool;
        private int _lastPickupAttemptTime = int.MinValue;

        // Combat effects reference
        private CombatEffects _combatEffects;
        private AnimationEffects _animationEffects;
        private MobSkillEffectLoader _mobSkillEffectLoader;
        private AffectedAreaPool _affectedAreaPool;
        private Func<int, bool> _remoteAffectedAreaDamageBlockEvaluator;
        private Action<int, int, int> _remoteAffectedAreaDamageShareHandler;
        private Func<int, bool> _affectedAreaOwnerPartyMembershipEvaluator;
        private Func<int, bool> _affectedAreaOwnerTeamMembershipEvaluator;
        private Func<int, int, MobSkillRuntimeData> _mobSkillRuntimeResolver;
        private SoundManager _soundManager;
        private WzImage _weaponSoundImage;
        private Action<Rectangle, int, int, int> _reactorAttackAreaHandler;
        private Action<SkillManager.LocalAttackAreaResolution> _localAttackAreaHandler;
        private PlayerMobStatusController _mobStatusController;
        private PlayerMobStatusFrameState _currentMobStatusState = PlayerMobStatusFrameState.Default;
        private Action<PlayerCharacter, Rectangle, int> _attackHitboxHandler;
        private const int TankSiegeModeEndFallbackDelayMs = 180;
        private int _pendingRepeatSkillModeEndSkillId;
        private int _pendingRepeatSkillModeEndReturnSkillId;
        private int _pendingRepeatSkillModeEndRequestTime = int.MinValue;
        private readonly HashSet<int> _activeAffectedAreaAvatarEffectIds = new();
        private readonly Dictionary<int, int> _activeAffectedAreaAvatarEffectSignatures = new();
        private readonly Dictionary<int, AffectedAreaAvatarEffectCacheEntry> _affectedAreaAvatarEffectSkillCache = new();

        // Sound callbacks
        private Action _onJumpSound;
        private Func<string> _jumpRestrictionMessageProvider;
        private Func<string> _jumpDownRestrictionMessageProvider;
        private Action<string> _onJumpRestricted;
        private Func<float, float> _moveSpeedCapResolver;
        private Action<PlayerCharacter, PlayerLandingInfo> _onLanded;
        internal Action<int, int, int, PacketOwnedSkillEffectRequest> OnRepeatSkillModeEndEffectRequestReady { get; set; }

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
            ConfigureDragonOwnerPhaseContextProvider();
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
            ConfigureDragonActionLayerOwnerZProvider();
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

        internal void SetMobSkillRuntimeResolver(Func<int, int, MobSkillRuntimeData> resolver)
        {
            _mobSkillRuntimeResolver = resolver;
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

        public void SetJumpRestrictionHandler(
            Func<string> getRestrictionMessage,
            Func<string> getJumpDownRestrictionMessage,
            Action<string> onJumpRestricted)
        {
            _jumpRestrictionMessageProvider = getRestrictionMessage;
            _jumpDownRestrictionMessageProvider = getJumpDownRestrictionMessage;
            _onJumpRestricted = onJumpRestricted;
            if (Player != null)
            {
                Player.SetJumpRestrictionHandler(getRestrictionMessage, getJumpDownRestrictionMessage, onJumpRestricted);
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

        public void SetLandingHandler(Action<PlayerCharacter, PlayerLandingInfo> onLanded)
        {
            _onLanded = onLanded;
            if (Player != null)
            {
                Player.SetLandingHandler(onLanded);
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

        internal void SetLocalAttackAreaHandler(Action<SkillManager.LocalAttackAreaResolution> localAttackAreaHandler)
        {
            _localAttackAreaHandler = localAttackAreaHandler;
            if (Skills != null)
            {
                Skills.OnLocalAttackAreaResolved = localAttackAreaHandler;
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

        public MobSkillEffectLoader GetMobSkillEffectLoader()
        {
            return _mobSkillEffectLoader;
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

        public void SetAnimationEffects(AnimationEffects animationEffects)
        {
            _animationEffects = animationEffects;
            Skills?.SetAnimationEffects(animationEffects);
        }

        public void SetAffectedAreaPool(AffectedAreaPool affectedAreaPool)
        {
            _affectedAreaPool = affectedAreaPool;
        }

        public void SetRemoteAffectedAreaDamageBlockEvaluator(Func<int, bool> evaluator)
        {
            _remoteAffectedAreaDamageBlockEvaluator = evaluator;
        }

        public void SetRemoteAffectedAreaDamageShareHandler(Action<int, int, int> handler)
        {
            _remoteAffectedAreaDamageShareHandler = handler;
            if (Skills != null)
            {
                Skills.OnExternalAreaDamageSharingApplied = handler;
            }
        }

        public void SetAffectedAreaOwnerPartyMembershipEvaluator(Func<int, bool> evaluator)
        {
            _affectedAreaOwnerPartyMembershipEvaluator = evaluator;
        }

        public void SetAffectedAreaOwnerTeamMembershipEvaluator(Func<int, bool> evaluator)
        {
            _affectedAreaOwnerTeamMembershipEvaluator = evaluator;
        }

        public void SetSoundManager(SoundManager soundManager)
        {
            _soundManager = soundManager;
            Skills?.SetSoundManager(soundManager);
        }

        private void PlayClientOwnedWeaponSfx(string sfx)
        {
            if (_soundManager == null || string.IsNullOrWhiteSpace(sfx))
            {
                return;
            }

            WzBinaryProperty sound = ResolveClientOwnedWeaponSfx(sfx);
            if (sound == null)
            {
                return;
            }

            string soundKey = $"WeaponSfx:{sfx.Trim()}:{WeaponSfxAttackSoundName}";
            _soundManager.RegisterSound(soundKey, sound);
            _soundManager.PlaySound(soundKey);
        }

        internal WzBinaryProperty ResolveClientOwnedWeaponSfx(string sfx)
        {
            if (string.IsNullOrWhiteSpace(sfx))
            {
                return null;
            }

            _weaponSoundImage ??= Program.FindImage("Sound", "Weapon.img");
            _weaponSoundImage?.ParseImage();

            WzImageProperty soundNode = _weaponSoundImage?[sfx.Trim()]?[WeaponSfxAttackSoundName];
            return soundNode as WzBinaryProperty
                   ?? (soundNode as WzUOLProperty)?.LinkValue as WzBinaryProperty;
        }

        public void SetCurrentMapIdProvider(Func<int> currentMapIdProvider)
        {
            Pets.SetCurrentMapIdProvider(currentMapIdProvider);
        }

        public void SetCurrentMapInfoProvider(Func<MapInfo> currentMapInfoProvider)
        {
            Dragon.SetCurrentMapInfoProvider(currentMapInfoProvider);
        }

        public void SetDragonWrapperOwnedNoDragonSuppression(bool? suppressDragonPresentation)
        {
            Dragon.SetWrapperOwnedNoDragonSuppression(suppressDragonPresentation);
        }

        internal void SetDragonOwnerPhaseContextProvider(Func<DragonCompanionRuntime.OwnerPhaseContext> ownerPhaseContextProvider)
        {
            _dragonOwnerPhaseContextProvider = ownerPhaseContextProvider;
            ConfigureDragonOwnerPhaseContextProvider();
        }

        public void SetDragonQuestInfoStateProvider(Func<int?> questInfoStateProvider)
        {
            Dragon.SetQuestInfoStateProvider(questInfoStateProvider);
        }

        internal static bool HasClientOwnedDragonFuryEffect(SkillManager skills)
        {
            return skills?.HasBuff(DragonFurySkillId) == true;
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
            var createPlayerStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var initializePlayerStopwatch = System.Diagnostics.Stopwatch.StartNew();
            InitializePlayer(new PlayerCharacter(_device, _texturePool, build));
            initializePlayerStopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] InitializePlayer(new PlayerCharacter(...)) completed in {initializePlayerStopwatch.ElapsedMilliseconds} ms");

            var ensureDefaultsStopwatch = System.Diagnostics.Stopwatch.StartNew();
            CompanionEquipment.EnsureDefaults(Loader, build);
            ensureDefaultsStopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] CompanionEquipment.EnsureDefaults completed in {ensureDefaultsStopwatch.ElapsedMilliseconds} ms");

            // Create SkillManager if we have a SkillLoader
            if (SkillLoader != null)
            {
                var skillManagerSetupStopwatch = System.Diagnostics.Stopwatch.StartNew();
                Skills = new SkillManager(SkillLoader, Player);
                _pendingRepeatSkillModeEndSkillId = 0;
                _pendingRepeatSkillModeEndReturnSkillId = 0;
                _pendingRepeatSkillModeEndRequestTime = int.MinValue;
                Skills.SetMobPool(_mobPool);
                Skills.SetDropPool(_dropPool);
                Skills.SetCombatEffects(_combatEffects);
                Skills.SetAnimationEffects(_animationEffects);
                Skills.SetSoundManager(_soundManager);
                Skills.SetFootholdLookup(_findFoothold);
                Skills.SetTamingMobLoader(Loader.LoadTamingMob);
                Skills.OnAttackAreaResolved = _reactorAttackAreaHandler;
                Skills.OnLocalAttackAreaResolved = _localAttackAreaHandler;
                Skills.OnRepeatSkillModeEndRequested = HandleRepeatSkillModeEndRequested;
                Skills.OnRepeatSkillImmediateEffectRequestReady = HandleRepeatSkillImmediateEffectRequestReady;
                Skills.OnExternalAreaDamageSharingApplied = _remoteAffectedAreaDamageShareHandler;
                Skills.OnClientSkillCancelDragonCleanupRequested = (_, currentTime) =>
                    Dragon.ClearClientOwnedOneTimeActionOnSkillCancel(Player, currentTime);
                build.SkillStatBonusProvider = stat => Skills.GetPassiveBonus(stat) + Skills.GetBuffStat(stat);
                build.SkillMasteryProvider = () => Skills.GetMastery(build.GetWeapon());

                // Keep only the player's current job path resident at startup.
                var loadSkillsStopwatch = System.Diagnostics.Stopwatch.StartNew();
                Skills.LoadSkillsForJob(build.Job);
                loadSkillsStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[PlayerManager] Skills.LoadSkillsForJob completed in {loadSkillsStopwatch.ElapsedMilliseconds} ms");

                var learnSkillsStopwatch = System.Diagnostics.Stopwatch.StartNew();
                Skills.LearnAllNonHiddenSkills();
                learnSkillsStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[PlayerManager] Skills.LearnAllNonHiddenSkills completed in {learnSkillsStopwatch.ElapsedMilliseconds} ms");

                skillManagerSetupStopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[PlayerManager] SkillManager setup completed in {skillManagerSetupStopwatch.ElapsedMilliseconds} ms");

                System.Diagnostics.Debug.WriteLine($"[PlayerManager] SkillManager created for job path {build.Job}");
            }

            var postSkillSetupStopwatch = System.Diagnostics.Stopwatch.StartNew();
            Dragon.SetDragonFuryVisibleProvider(() => HasClientOwnedDragonFuryEffect(Skills));
            ConfigureDragonActionLayerOwnerZProvider();

            _mobStatusController = new PlayerMobStatusController(Player, Skills, TeleportToSpawn);
            Skills?.SetExternalCastBlockedEvaluator(currentTime => _currentMobStatusState.SkillCastBlocked);
            Skills?.SetExternalStateRestrictionMessageProvider(currentTime => _mobStatusController?.GetSkillCastRestrictionMessage(currentTime));

            Combat.SetDamageBlockedEvaluator(IsDamageBlockedByAffectedArea);
            Combat.SetIncomingDamageResolver((damage, currentTime) => Skills?.ResolveIncomingDamageAfterActiveBuffs(damage, currentTime) ?? damage);

            // Set up callbacks
            Player.SetFootholdLookup(_findFoothold);
            Player.SetLadderLookup(_findLadder);
            Player.SetSwimAreaCheck(_checkSwimArea);
            Func<int, CharacterPart> tamingMobLoader = Loader != null
                ? new Func<int, CharacterPart>(Loader.LoadTamingMob)
                : null;
            Player.SetTamingMobLoader(tamingMobLoader);
            Player.SetPortableChairTamingMobLoader(tamingMobLoader);
            Player.SetSkillMorphLoader(Loader.LoadMorph);
            Player.SetJumpSoundCallback(_onJumpSound);
            Player.SetWeaponSfxSoundCallback(PlayClientOwnedWeaponSfx);
            Player.SetJumpRestrictionHandler(_jumpRestrictionMessageProvider, _jumpDownRestrictionMessageProvider, _onJumpRestricted);
            Player.SetMoveSpeedCapResolver(_moveSpeedCapResolver);
            Player.SetLandingHandler(_onLanded);
            Player.Physics.IsFlyingMap = _isFlyingMap;
            Player.Physics.RequiresFlyingSkillForMap = _requiresFlyingSkillForMap;

            // Set up attack callback
            Player.OnAttackHitbox = (player, hitbox) =>
            {
                int currentTime = Environment.TickCount;
                if (_mobPool != null)
                {
                    var results = Combat.ProcessAttack(_mobPool, hitbox);
                    bool landedCritical = false;

                    // Add damage numbers to combat effects
                    if (_combatEffects != null)
                    {
                        int comboIndex = 0;
                        foreach (var result in results)
                        {
                            landedCritical |= result.IsCritical;
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
                    else
                    {
                        foreach (var result in results)
                        {
                            landedCritical |= result.IsCritical;
                        }
                    }

                    if (landedCritical)
                    {
                        Skills?.NotifyLocalCriticalHit(currentTime);
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
                int currentTime = Environment.TickCount;
                Skills?.NotifyLocalPlayerDamaged(currentTime);
                if (_combatEffects != null && damage > 0)
                {
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
            postSkillSetupStopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] Post-skill player wiring completed in {postSkillSetupStopwatch.ElapsedMilliseconds} ms");

        }

        private void ConfigureDragonActionLayerOwnerZProvider()
        {
            Dragon.SetActionLayerOwnerZProvider(ResolveDragonActionLayerOwnerZFromVecCtrlContext);
        }

        private void ConfigureDragonOwnerPhaseContextProvider()
        {
            Dragon.SetOwnerPhaseContextProvider(ResolveDragonOwnerPhaseContext);
        }

        private DragonCompanionRuntime.OwnerPhaseContext ResolveDragonOwnerPhaseContext()
        {
            if (_dragonOwnerPhaseContextProvider != null)
            {
                return _dragonOwnerPhaseContextProvider();
            }

            // The local PlayerManager-owned dragon runtime always tracks the local avatar owner.
            return Player?.Build == null
                ? DragonCompanionRuntime.OwnerPhaseContext.NoLocalUser
                : new DragonCompanionRuntime.OwnerPhaseContext(hasLocalUser: true, ownerMatchesLocalPhase: true, phaseAlpha: byte.MaxValue);
        }

        private int? ResolveDragonActionLayerOwnerZFromVecCtrlContext(Vector2 dragonAnchor)
        {
            bool onLadderOrRope = Player?.Physics?.IsOnLadderOrRope == true
                                  || Player?.State is PlayerState.Ladder or PlayerState.Rope;
            int? dragonAnchorLayerPage = null;
            int? dragonAnchorLayerZMass = null;
            if (_findFoothold != null)
            {
                FootholdLine dragonFoothold = _findFoothold(dragonAnchor.X, dragonAnchor.Y, DragonVecCtrlLayerSearchRange);
                if (dragonFoothold != null)
                {
                    dragonAnchorLayerPage = dragonFoothold.LayerNumber;
                    dragonAnchorLayerZMass = dragonFoothold.PlatformNumber;
                }
            }

            return ResolveDragonOwnerLayerZFromVecCtrlContext(
                dragonAnchorLayerPage,
                dragonAnchorLayerZMass,
                Player?.Physics?.CurrentFoothold?.LayerNumber,
                Player?.Physics?.CurrentFoothold?.PlatformNumber,
                Player?.Physics?.FallStartFoothold?.LayerNumber,
                Player?.Physics?.FallStartFoothold?.PlatformNumber,
                onLadderOrRope);
        }

        internal static int? ResolveDragonOwnerLayerZFromFoothold(FootholdLine foothold, bool onLadderOrRope)
        {
            if (foothold == null)
            {
                return null;
            }

            return DragonCompanionRuntime.ResolveClientOwnerLayerZFromVecCtrlContext(
                foothold.LayerNumber,
                foothold.PlatformNumber,
                onLadderOrRope);
        }

        internal static int? ResolveDragonOwnerLayerZFromVecCtrlState(
            int? currentLayerPage,
            int? currentLayerZMass,
            int? fallStartLayerPage,
            int? fallStartLayerZMass,
            bool onLadderOrRope)
        {
            int? currentFootholdLayerZ = DragonCompanionRuntime.ResolveClientOwnerLayerZFromVecCtrlContext(
                currentLayerPage,
                currentLayerZMass,
                onLadderOrRope);
            if (currentFootholdLayerZ.HasValue)
            {
                return currentFootholdLayerZ.Value;
            }

            return DragonCompanionRuntime.ResolveClientOwnerLayerZFromVecCtrlContext(
                fallStartLayerPage,
                fallStartLayerZMass,
                onLadderOrRope);
        }

        internal static int? ResolveDragonOwnerLayerZFromVecCtrlContext(
            int? dragonAnchorLayerPage,
            int? dragonAnchorLayerZMass,
            int? currentLayerPage,
            int? currentLayerZMass,
            int? fallStartLayerPage,
            int? fallStartLayerZMass,
            bool onLadderOrRope)
        {
            _ = currentLayerPage;
            _ = currentLayerZMass;
            _ = fallStartLayerPage;
            _ = fallStartLayerZMass;

            int? dragonAnchorLayerZ = DragonCompanionRuntime.ResolveClientOwnerLayerZFromVecCtrlContext(
                dragonAnchorLayerPage,
                dragonAnchorLayerZMass,
                onLadderOrRope);
            return dragonAnchorLayerZ;
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
                PlayMobSkillHitEffect(skillId, skillLevel, Environment.TickCount, x, y);
            };

            Combat.OnMobSkillStatusApplied = (skillId, skillLevel, currentTime, sourceX, applyRuntimeStatus) =>
            {
                return ApplyPlayerMobSkillStatus(skillId, skillLevel, currentTime, sourceX, applyRuntimeStatus);
            };

            Combat.OnMobAttackMissPlayer = (x, y, currentTime) =>
            {
                _combatEffects?.AddMiss(x, y, currentTime);
            };

            Combat.OnDamageReceived = (combatPlayer, damage, mob) =>
            {
                Skills?.NotifyOwnerDamaged(mob, Environment.TickCount);
            };

            Combat.SetDamageBlockedEvaluator(IsDamageBlockedByAffectedArea);
            Combat.SetIncomingDamageResolver((damage, currentTime) => Skills?.ResolveIncomingDamageAfterActiveBuffs(damage, currentTime) ?? damage);

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
            Player.SetWeaponSfxSoundCallback(PlayClientOwnedWeaponSfx);
            Player.SetJumpRestrictionHandler(_jumpRestrictionMessageProvider, _jumpDownRestrictionMessageProvider, _onJumpRestricted);
            Player.SetMoveSpeedCapResolver(_moveSpeedCapResolver);
            Player.Physics.IsFlyingMap = _isFlyingMap;
            Player.Physics.RequiresFlyingSkillForMap = _requiresFlyingSkillForMap;

            Player.OnAttackHitbox = (combatPlayer, hitbox) =>
            {
                int currentTime = Environment.TickCount;
                if (_mobPool != null)
                {
                    var results = Combat.ProcessAttack(_mobPool, hitbox);
                    bool landedCritical = false;
                    if (_combatEffects != null)
                    {
                        int comboIndex = 0;
                        foreach (var result in results)
                        {
                            landedCritical |= result.IsCritical;
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
                    else
                    {
                        foreach (var result in results)
                        {
                            landedCritical |= result.IsCritical;
                        }
                    }

                    if (landedCritical)
                    {
                        Skills?.NotifyLocalCriticalHit(currentTime);
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
                int currentTime = Environment.TickCount;
                Skills?.NotifyLocalPlayerDamaged(currentTime);
                if (_combatEffects != null && damage > 0)
                {
                    _combatEffects.AddReceivedDamage(
                        damage,
                        combatPlayer.X,
                        combatPlayer.Y - 50,
                        false,
                        currentTime);
                }
            };

        }

        private bool ApplyPlayerMobSkillStatus(int skillId, int skillLevel, int currentTime, float sourceX, bool applyRuntimeStatus)
        {
            if (Player == null)
            {
                return false;
            }

            MobSkillRuntimeData runtimeData = _mobSkillRuntimeResolver?.Invoke(skillId, Math.Max(1, skillLevel));
            bool runtimeApplied;
            if (applyRuntimeStatus)
            {
                runtimeApplied = TryApplyMobSkillStatus(skillId, runtimeData, currentTime, sourceX);
            }
            else
            {
                runtimeApplied = _mobStatusController?.HasAppliedMobSkillState(skillId, currentTime) == true;
            }

            if (!runtimeApplied)
            {
                return false;
            }

            if (!PlayerSkillBlockingStatusMapper.TryMapMobSkill(skillId, out _))
            {
                return true;
            }

            TryApplyMobSkillBlockingStatus(skillId, Math.Max(1, skillLevel), runtimeData, currentTime);

            return true;
        }

        internal bool TryApplyRemoteAffectedAreaPlayerSkillStatus(
            SkillData skill,
            SkillLevelData levelData,
            int currentTime)
        {
            return _mobStatusController?.TryApplyRemoteAffectedAreaPlayerSkill(
                skill,
                levelData,
                currentTime) == true;
        }

        internal void PlayMobSkillHitEffect(int skillId, int skillLevel, int currentTime)
        {
            if (Player == null)
            {
                return;
            }

            PlayMobSkillHitEffect(skillId, skillLevel, currentTime, Player.X, Player.Y);
        }

        private void PlayMobSkillHitEffect(int skillId, int skillLevel, int currentTime, float x, float y)
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
                currentTime,
                effectData.AffectedRepeat,
                duration);
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

            System.Diagnostics.Stopwatch loadBuildStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var build = Loader.LoadDefaultMale();
            loadBuildStopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] LoadDefaultMale returned: {build != null}");
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] LoadDefaultMale completed in {loadBuildStopwatch.ElapsedMilliseconds} ms");
            if (build == null)
                return false;

            System.Diagnostics.Stopwatch createPlayerStopwatch = System.Diagnostics.Stopwatch.StartNew();
            CreatePlayerFromBuild(build);
            createPlayerStopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] Player created from build");
            System.Diagnostics.Debug.WriteLine($"[PlayerManager] CreatePlayerFromBuild completed in {createPlayerStopwatch.ElapsedMilliseconds} ms");
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
                ReleaseActiveKeydownSkillWithinClientCancelBatchScope(Skills, currentTime);
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
                if (_currentMobStatusState.PickupBlocked)
                {
                    pickupHeld = false;
                    pickupPressed = false;
                }

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
                SkillManager skills = Skills;
                if (skills != null && !_currentMobStatusState.SkillCastBlocked)
                {
                    ProcessHotkeyInputWithinClientCancelBatchScope(skills, inputState, currentTime);
                }
            }
            else
            {
                ReleaseActiveKeydownSkillWithinClientCancelBatchScope(Skills, currentTime);
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
            UpdateAffectedAreaAvatarEffects(currentTime);
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
        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            Action drawBetweenDragonAndPlayer = null)
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
            drawBetweenDragonAndPlayer?.Invoke();
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

            _affectedAreaPool?.Draw(spriteBatch, mapShiftX, mapShiftY, centerX, centerY, currentTime);
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

        public bool TryTriggerSpecialistPetChatFeedback(string message, int currentTime)
        {
            return Pets.TryTriggerSpecialistChatFeedback(message, currentTime);
        }

        private bool IsDamageBlockedByAffectedArea(int currentTime)
        {
            if (Skills?.IsPlayerProtectedByClientSkillZone(currentTime) == true)
            {
                return true;
            }

            return _remoteAffectedAreaDamageBlockEvaluator?.Invoke(currentTime) == true;
        }

        private void UpdateAffectedAreaAvatarEffects(int currentTime)
        {
            if (Player == null || !Player.IsAlive || _affectedAreaPool == null)
            {
                ClearTrackedAffectedAreaAvatarEffects(currentTime);
                return;
            }

            int localPlayerId = Player.Build?.Id ?? 0;
            HashSet<int> nextActiveEffectIds = new();
            var nextActiveEffectSignatures = new Dictionary<int, int>();
            foreach (ActiveAffectedArea area in _affectedAreaPool.ActiveAreas)
            {
                if (!TryResolveAffectedAreaAvatarEffect(
                        area,
                        localPlayerId,
                        currentTime,
                        out int avatarEffectId,
                        out SkillData effectSkill,
                        out int effectSignature))
                {
                    continue;
                }

                nextActiveEffectIds.Add(avatarEffectId);
                nextActiveEffectSignatures[avatarEffectId] = effectSignature;
                if (ShouldRefreshAffectedAreaAvatarEffect(
                        _activeAffectedAreaAvatarEffectSignatures.TryGetValue(avatarEffectId, out int existingSignature)
                            ? existingSignature
                            : (int?)null,
                        effectSignature,
                        Player.HasSkillAvatarEffect(avatarEffectId)))
                {
                    Player.ApplySkillAvatarEffect(avatarEffectId, effectSkill, currentTime);
                }
            }

            foreach (int activeEffectId in _activeAffectedAreaAvatarEffectIds)
            {
                if (nextActiveEffectIds.Contains(activeEffectId))
                {
                    continue;
                }

                Player.ClearSkillAvatarEffect(activeEffectId, currentTime, playFinish: false);
                _activeAffectedAreaAvatarEffectSignatures.Remove(activeEffectId);
            }

            _activeAffectedAreaAvatarEffectIds.Clear();
            foreach (int activeEffectId in nextActiveEffectIds)
            {
                _activeAffectedAreaAvatarEffectIds.Add(activeEffectId);
            }

            _activeAffectedAreaAvatarEffectSignatures.Clear();
            foreach ((int activeEffectId, int signature) in nextActiveEffectSignatures)
            {
                _activeAffectedAreaAvatarEffectSignatures[activeEffectId] = signature;
            }
        }

        private bool TryResolveAffectedAreaAvatarEffect(
            ActiveAffectedArea area,
            int localPlayerId,
            int currentTime,
            out int avatarEffectId,
            out SkillData effectSkill,
            out int effectSignature)
        {
            avatarEffectId = 0;
            effectSkill = null;
            effectSignature = 0;

            if (area?.SourceKind != AffectedAreaSourceKind.PlayerSkill
                || area.ObjectId <= 0
                || !area.IsActive(currentTime)
                || !area.Contains(Player.X, Player.Y))
            {
                return false;
            }

            SkillData skill = SkillLoader?.LoadSkill(area.SkillId);
            SkillData[] supportSkills = ResolveAffectedAreaSupportSkills(skill);
            SkillLevelData levelData = ResolveAffectedAreaSkillLevel(skill, area.SkillLevel);
            SkillLevelData supportLevelData = ResolveAffectedAreaSupportLevelData(levelData, supportSkills);
            SkillLevelData effectiveLevelData = supportLevelData ?? levelData;
            if (!ShouldPromoteAffectedAreaAvatarEffect(skill, supportSkills, effectiveLevelData))
            {
                return false;
            }

            bool ownerIsPartyMember = _affectedAreaOwnerPartyMembershipEvaluator?.Invoke(area.OwnerId) == true;
            bool ownerIsSameTeamMember = _affectedAreaOwnerTeamMembershipEvaluator?.Invoke(area.OwnerId) == true;
            if (!RemoteAffectedAreaSupportResolver.CanAffectLocalPlayer(
                    skill,
                    supportSkills,
                    localPlayerId,
                    area.OwnerId,
                    ownerIsPartyMember,
                    ownerIsSameTeamMember,
                    effectiveLevelData))
            {
                return false;
            }

            effectSkill = GetOrCreateAffectedAreaAvatarEffectSkill(skill, supportSkills, out effectSignature);
            if (effectSkill == null)
            {
                return false;
            }

            avatarEffectId = CreateAffectedAreaAvatarEffectId(area.ObjectId);
            return true;
        }

        private SkillData[] ResolveAffectedAreaSupportSkills(SkillData skill)
        {
            if (skill == null || SkillLoader == null)
            {
                return Array.Empty<SkillData>();
            }

            var supportSkills = new List<SkillData>();
            var visitedSkillIds = new HashSet<int>();
            CollectAffectedAreaSupportSkills(skill, supportSkills, visitedSkillIds);
            return supportSkills.ToArray();
        }

        private void CollectAffectedAreaSupportSkills(
            SkillData skill,
            ICollection<SkillData> supportSkills,
            ISet<int> visitedSkillIds)
        {
            if (skill == null)
            {
                return;
            }

            int[] affectedSkillIds = skill.GetAffectedSkillIds();
            for (int i = 0; i < affectedSkillIds.Length; i++)
            {
                int affectedSkillId = affectedSkillIds[i];
                if (affectedSkillId <= 0 || visitedSkillIds?.Add(affectedSkillId) != true)
                {
                    continue;
                }

                SkillData affectedSkill = SkillLoader?.LoadSkill(affectedSkillId);
                if (affectedSkill == null)
                {
                    continue;
                }

                supportSkills?.Add(affectedSkill);
                CollectAffectedAreaSupportSkills(affectedSkill, supportSkills, visitedSkillIds);
            }
        }

        internal static bool ShouldPromoteAffectedAreaAvatarEffect(
            SkillData skill,
            IReadOnlyCollection<SkillData> supportSkills,
            SkillLevelData levelData = null)
        {
            return RemoteAffectedAreaSupportResolver.IsFriendlyPlayerAreaSkill(skill, supportSkills, levelData)
                   && AffectedAreaAvatarEffectResolver.HasPromotableAffectedBranch(skill, supportSkills);
        }

        internal static SkillLevelData ResolveAffectedAreaSupportLevelData(
            SkillLevelData primaryLevelData,
            params SkillData[] supportSkills)
        {
            if (supportSkills == null || supportSkills.Length == 0)
            {
                return primaryLevelData;
            }

            SkillData primarySkill = null;
            for (int i = 0; i < supportSkills.Length; i++)
            {
                if (supportSkills[i] != null)
                {
                    primarySkill = supportSkills[i];
                    break;
                }
            }

            return RemoteAffectedAreaSupportResolver.CreateProjectedSupportBuffLevelData(
                       primarySkill,
                       primaryLevelData,
                       supportSkills)
                   ?? primaryLevelData;
        }

        internal static SkillLevelData ResolveAffectedAreaSkillLevel(SkillData skill, int skillLevel)
        {
            if (skill == null)
            {
                return null;
            }

            int resolvedLevel = skillLevel > 0 ? skillLevel : 1;
            return skill.GetLevel(resolvedLevel);
        }

        private SkillData GetOrCreateAffectedAreaAvatarEffectSkill(
            SkillData skill,
            IReadOnlyCollection<SkillData> supportSkills,
            out int signature)
        {
            signature = 0;
            if (skill == null)
            {
                return null;
            }

            if (!AffectedAreaAvatarEffectResolver.TryBuildLoopingAvatarEffectSkill(
                    skill,
                    supportSkills,
                    out SkillData effectSkill,
                    out signature))
            {
                _affectedAreaAvatarEffectSkillCache.Remove(skill.SkillId);
                return null;
            }

            if (_affectedAreaAvatarEffectSkillCache.TryGetValue(skill.SkillId, out AffectedAreaAvatarEffectCacheEntry cachedEntry)
                && cachedEntry?.Skill != null
                && cachedEntry.Signature == signature)
            {
                return cachedEntry.Skill;
            }

            _affectedAreaAvatarEffectSkillCache[skill.SkillId] = new AffectedAreaAvatarEffectCacheEntry
            {
                Signature = signature,
                Skill = effectSkill
            };
            return effectSkill;
        }

        internal static bool ShouldRefreshAffectedAreaAvatarEffect(
            int? existingSignature,
            int nextSignature,
            bool hasActiveEffect)
        {
            return !hasActiveEffect
                   || !existingSignature.HasValue
                   || existingSignature.Value != nextSignature;
        }

        private void ClearTrackedAffectedAreaAvatarEffects(int currentTime)
        {
            foreach (int activeEffectId in _activeAffectedAreaAvatarEffectIds)
            {
                Player?.ClearSkillAvatarEffect(activeEffectId, currentTime, playFinish: false);
            }

            _activeAffectedAreaAvatarEffectIds.Clear();
            _activeAffectedAreaAvatarEffectSignatures.Clear();
        }

        private static int CreateAffectedAreaAvatarEffectId(int objectId)
        {
            return unchecked(AffectedAreaAvatarEffectIdBase + objectId);
        }

        private static int ResolveMobSkillBlockingDurationMs(int skillId, MobSkillRuntimeData runtimeData, MobSkillEffectData effectData)
        {
            int runtimeStatusDurationMs = PlayerMobStatusController.ResolveSkillStatusDurationMs(skillId, runtimeData);
            if (runtimeStatusDurationMs > 0)
            {
                return runtimeStatusDurationMs;
            }

            if (runtimeData?.DurationMs > 0)
            {
                return runtimeData.DurationMs;
            }

            return Math.Max(0, (effectData?.Time ?? 0) * 1000);
        }

        internal bool TryApplyMobSkillStatus(int skillId, MobSkillRuntimeData runtimeData, int currentTime, float sourceX = 0f, int elementAttribute = 0)
        {
            if (Player == null)
            {
                return false;
            }

            return _mobStatusController?.TryApplyMobSkill(skillId, runtimeData, currentTime, sourceX, elementAttribute) == true;
        }

        internal bool TryApplyMobSkillBlockingStatus(int skillId, int skillLevel, MobSkillRuntimeData runtimeData, int currentTime)
        {
            if (Player == null || !PlayerSkillBlockingStatusMapper.TryMapMobSkill(skillId, out PlayerSkillBlockingStatus status))
            {
                return false;
            }

            MobSkillEffectData effectData = _mobSkillEffectLoader?.LoadMobSkillEffect(skillId, Math.Max(1, skillLevel));
            int durationMs = ResolveMobSkillBlockingDurationMs(skillId, runtimeData, effectData);
            if (durationMs <= 0)
            {
                return false;
            }

            Player.ApplySkillBlockingStatus(status, durationMs, currentTime);
            return true;
        }

        internal int ClearMobStatuses(IEnumerable<PlayerMobStatusEffect> effects)
        {
            return _mobStatusController?.ClearStatuses(effects) ?? 0;
        }

        internal bool HasMobStatus(PlayerMobStatusEffect effect)
        {
            return _mobStatusController?.HasStatusEffect(effect) == true;
        }

        internal bool CanAutoSelectMobSkillStatus(
            int skillId,
            MobSkillRuntimeData runtimeData,
            int currentTime,
            float sourceX = 0f,
            int recastLeadTimeMs = 0)
        {
            return _mobStatusController?.CanAutoSelectMobSkill(
                       skillId,
                       runtimeData,
                       currentTime,
                       sourceX,
                       recastLeadTimeMs) == true;
        }

        internal bool TryGetFearMobStatusVisualState(int currentTime, out float intensity, out int remainingDurationMs)
        {
            if (_mobStatusController == null)
            {
                intensity = 0f;
                remainingDurationMs = 0;
                return false;
            }

            return _mobStatusController.TryGetFearVisualState(currentTime, out intensity, out remainingDurationMs);
        }

        internal bool IsInteractPressedForWorldInput()
        {
            if (Input == null)
            {
                return false;
            }

            if (IsInteractBlockedByMobStatus(_currentMobStatusState))
            {
                return false;
            }

            return Input.IsPressed(InputAction.Interact);
        }

        internal static bool IsInteractBlockedByMobStatus(PlayerMobStatusFrameState state)
        {
            return state.MovementLocked || state.ForcedHorizontalDirection != 0;
        }

        internal static bool ShouldPrepareForMobControlLockout(
            PlayerMobStatusFrameState previousState,
            PlayerMobStatusFrameState currentState)
        {
            bool enteredForcedHorizontalControl = currentState.ForcedHorizontalDirection != 0
                                                 && previousState.ForcedHorizontalDirection == 0;
            bool enteredMovementLock = currentState.MovementLocked
                                       && !previousState.MovementLocked;
            return enteredForcedHorizontalControl || enteredMovementLock;
        }

        internal int AdjustMobAffectedExperienceReward(int baseAmount, int currentTime)
        {
            return _mobStatusController?.AdjustExperienceReward(baseAmount, currentTime) ?? Math.Max(0, baseAmount);
        }

        private void HandleRepeatSkillModeEndRequested(int skillId, int returnSkillId, int requestedAt)
        {
            _pendingRepeatSkillModeEndSkillId = skillId;
            _pendingRepeatSkillModeEndReturnSkillId = returnSkillId;
            _pendingRepeatSkillModeEndRequestTime = requestedAt;
            if (Skills?.TryBuildPendingRepeatSkillModeEndEffectRequest(
                    skillId,
                    returnSkillId,
                    requestedAt,
                    out PacketOwnedSkillEffectRequest request,
                    out _) == true)
            {
                OnRepeatSkillModeEndEffectRequestReady?.Invoke(skillId, returnSkillId, requestedAt, request);
            }
        }

        private void HandleRepeatSkillImmediateEffectRequestReady(
            int skillId,
            int requestedAt,
            PacketOwnedSkillEffectRequest request)
        {
            OnRepeatSkillModeEndEffectRequestReady?.Invoke(skillId, 0, requestedAt, request);
        }

        public bool TryResolvePacketOwnedRepeatSkillModeEndRequest(
            int skillId,
            int returnSkillId,
            int requestedAt,
            int currentTime)
        {
            if (Skills == null
                || requestedAt == int.MinValue
                || requestedAt != _pendingRepeatSkillModeEndRequestTime
                || skillId != _pendingRepeatSkillModeEndSkillId
                || returnSkillId != _pendingRepeatSkillModeEndReturnSkillId
                || !Skills.HasPendingRepeatSkillModeEndRequest(skillId, returnSkillId, requestedAt))
            {
                return false;
            }

            if (!Skills.TryAcknowledgeRepeatSkillModeEndRequest(skillId, currentTime, requestedAt)
                && !Skills.TryAcknowledgeRepeatSkillModeEndRequest(returnSkillId, currentTime, requestedAt))
            {
                return false;
            }

            _pendingRepeatSkillModeEndSkillId = 0;
            _pendingRepeatSkillModeEndReturnSkillId = 0;
            _pendingRepeatSkillModeEndRequestTime = int.MinValue;
            return true;
        }

        public bool TryResolvePacketOwnedSg88ManualAttackRequest(int summonObjectId, int requestedAt, int currentTime)
        {
            return Skills?.TryResolvePendingSg88ManualAttackRequest(summonObjectId, requestedAt, currentTime) == true;
        }

        public bool TryResolvePacketOwnedSg88ManualAttackPacket(
            int summonObjectId,
            IReadOnlyList<int> targetMobIds,
            int currentTime)
        {
            return Skills?.TryResolvePendingSg88ManualAttackPacket(summonObjectId, targetMobIds, currentTime) == true;
        }

        public bool TryResolvePacketOwnedSg88ManualAttackPacket(
            int summonObjectId,
            IReadOnlyList<int> targetMobIds,
            int currentTime,
            out int resolvedRequestedAt)
        {
            resolvedRequestedAt = int.MinValue;
            return Skills?.TryResolvePendingSg88ManualAttackPacket(
                summonObjectId,
                targetMobIds,
                currentTime,
                out resolvedRequestedAt) == true;
        }

        public bool TryResolvePacketOwnedTeslaCoilAttackPacket(
            int summonObjectId,
            IReadOnlyList<int> targetMobIds,
            int currentTime)
        {
            return Skills?.TryResolvePendingTeslaAttackPacket(summonObjectId, targetMobIds, currentTime) == true;
        }

        public bool TryResolvePacketOwnedTeslaCoilAttackPacket(
            int summonObjectId,
            IReadOnlyList<int> targetMobIds,
            int currentTime,
            out int resolvedRequestedAt)
        {
            resolvedRequestedAt = int.MinValue;
            return Skills?.TryResolvePendingTeslaAttackPacket(
                summonObjectId,
                targetMobIds,
                currentTime,
                out resolvedRequestedAt) == true;
        }

        public bool TryApplyPacketOwnedSelfDestructSummonAttack(PacketOwnedSelfDestructAttackRequest request)
        {
            return Skills?.TryApplyPacketOwnedSelfDestructSummonAttack(request) == true;
        }

        private void TryAcknowledgePendingRepeatSkillModeEnd(int currentTime)
        {
            if (Skills == null || _pendingRepeatSkillModeEndRequestTime == int.MinValue)
            {
                return;
            }

            int skillId = _pendingRepeatSkillModeEndSkillId;
            int returnSkillId = _pendingRepeatSkillModeEndReturnSkillId;
            if (!Skills.HasPendingRepeatSkillModeEndRequest(skillId, returnSkillId, _pendingRepeatSkillModeEndRequestTime))
            {
                _pendingRepeatSkillModeEndSkillId = 0;
                _pendingRepeatSkillModeEndReturnSkillId = 0;
                _pendingRepeatSkillModeEndRequestTime = int.MinValue;
                return;
            }

            int fallbackDelayMs = Skills.GetPendingRepeatSkillModeEndFallbackDelayMs(
                skillId,
                returnSkillId,
                _pendingRepeatSkillModeEndRequestTime);
            if (fallbackDelayMs <= 0)
            {
                fallbackDelayMs = TankSiegeModeEndFallbackDelayMs;
            }

            if (currentTime < _pendingRepeatSkillModeEndRequestTime + fallbackDelayMs)
            {
                return;
            }

            if (Skills.TryAcknowledgeRepeatSkillModeEndRequest(
                    skillId,
                    currentTime,
                    _pendingRepeatSkillModeEndRequestTime)
                || Skills.TryAcknowledgeRepeatSkillModeEndRequest(
                    returnSkillId,
                    currentTime,
                    _pendingRepeatSkillModeEndRequestTime))
            {
                _pendingRepeatSkillModeEndSkillId = 0;
                _pendingRepeatSkillModeEndReturnSkillId = 0;
                _pendingRepeatSkillModeEndRequestTime = int.MinValue;
            }
        }

        private static void ReleaseActiveKeydownSkillWithinClientCancelBatchScope(SkillManager skills, int currentTime)
        {
            if (skills == null)
            {
                return;
            }

            using var _ = skills.BeginClientCancelBatchScope();
            skills.ReleaseActiveKeydownSkill(currentTime);
        }

        private static void ProcessHotkeyInputWithinClientCancelBatchScope(
            SkillManager skills,
            InputState inputState,
            int currentTime)
        {
            if (skills == null)
            {
                return;
            }

            using var _ = skills.BeginClientCancelBatchScope();

            // Primary skill hotkeys (Skill1-8, slots 0-7)
            for (int i = 0; i < 8; i++)
            {
                if (inputState.Skills[i])
                {
                    skills.TryCastHotkey(i, currentTime, inputState.SkillInputTokens[i]);
                }
            }

            // Function key hotkeys (F1-F12, slots 8-19)
            for (int i = 0; i < 12; i++)
            {
                if (inputState.FunctionSlots[i])
                {
                    skills.TryCastHotkey(
                        SkillManager.FUNCTION_SLOT_OFFSET + i,
                        currentTime,
                        inputState.FunctionSlotInputTokens[i]);
                }
            }

            // Ctrl+Number hotkeys (Ctrl+1-8, slots 20-27)
            for (int i = 0; i < 8; i++)
            {
                if (inputState.CtrlSlots[i])
                {
                    skills.TryCastHotkey(
                        SkillManager.CTRL_SLOT_OFFSET + i,
                        currentTime,
                        inputState.CtrlSlotInputTokens[i]);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                if (inputState.SkillsReleased[i])
                {
                    skills.ReleaseHotkeyIfActive(i, currentTime, inputState.SkillReleaseInputTokens[i]);
                }
            }

            for (int i = 0; i < 12; i++)
            {
                if (inputState.FunctionSlotsReleased[i])
                {
                    skills.ReleaseHotkeyIfActive(
                        SkillManager.FUNCTION_SLOT_OFFSET + i,
                        currentTime,
                        inputState.FunctionSlotReleaseInputTokens[i]);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                if (inputState.CtrlSlotsReleased[i])
                {
                    skills.ReleaseHotkeyIfActive(
                        SkillManager.CTRL_SLOT_OFFSET + i,
                        currentTime,
                        inputState.CtrlSlotReleaseInputTokens[i]);
                }
            }
        }

        private void UpdateMobStatusState(int currentTime)
        {
            PlayerMobStatusFrameState previousState = _currentMobStatusState;
            _currentMobStatusState = _mobStatusController?.Update(currentTime) ?? PlayerMobStatusFrameState.Default;

            if (ShouldPrepareForMobControlLockout(previousState, _currentMobStatusState))
            {
                Player?.PrepareForForcedHorizontalControl();
            }

            Player?.ApplyMobRecoveryModifiers(
                _currentMobStatusState.HpRecoveryReversed,
                _currentMobStatusState.MaxHpPercentCap,
                _currentMobStatusState.MaxMpPercentCap,
                _currentMobStatusState.HpRecoveryDamagePercent);
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
                inputState.Interact = false;
                inputState.InteractPressed = false;
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
                inputState.Interact = false;
                inputState.InteractPressed = false;
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

        public Vector2 GetSpawnPoint()
        {
            return _spawnPoint;
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
            Player.ResetLandingTracking();
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
            string actionName = ResolveClientBasicAttackActionName(AttackType.Swing);
            Player.TriggerSkillAnimation(actionName, currentTime: currentTime, playEffectiveWeaponSfx: true);
            Skills?.UpdateBasicMeleeAfterImageState(actionName, currentTime);
            System.Diagnostics.Debug.WriteLine($"[Attack] TryDoingBasicMeleeAttack - {actionName} triggered");

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
            string actionName = ResolveClientBasicAttackActionName(AttackType.Shoot);
            Player.TriggerSkillAnimation(actionName, currentTime: currentTime, playEffectiveWeaponSfx: true);
            System.Diagnostics.Debug.WriteLine($"[Attack] TryDoingBasicShoot - {actionName} triggered");
            // Note: Projectile spawning requires SkillManager
            return true;
        }

        /// <summary>
        /// Basic magic attack - works without SkillManager
        /// </summary>
        private bool TryDoingBasicMagicAttack(int currentTime)
        {
            string actionName = ResolveClientBasicAttackActionName(AttackType.Swing);
            Player.TriggerSkillAnimation(actionName, currentTime: currentTime);
            System.Diagnostics.Debug.WriteLine($"[Attack] TryDoingBasicMagicAttack - {actionName} triggered");

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

        private string ResolveClientBasicAttackActionName(AttackType fallbackAttackType)
        {
            return Player?.Build?.GetEffectiveAttackActionWeapon()?.ResolveClientBasicAttackActionName(fallbackAttackType)
                   ?? CharacterPart.GetActionString(fallbackAttackType == AttackType.Shoot
                       ? CharacterAction.Shoot1
                       : CharacterAction.SwingO1);
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
                reflectedDamage = Skills?.ResolveIncomingDamageAfterActiveBuffs(reflectedDamage, currentTime) ?? reflectedDamage;
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
            Skills?.SetAnimationEffects(_animationEffects);
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

            return currentTime - lastPickupAttemptTime >= ClientPickupRepeatDelayMs;
        }

        #endregion
    }
}
