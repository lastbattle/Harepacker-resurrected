using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapEditor.Info;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Loaders;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Wz;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Pools
{
    public enum SummonedPacketType
    {
        Created = 0x116,
        Removed = 0x117,
        Move = 0x118,
        Attack = 0x119,
        Skill = 0x11A,
        Hit = 0x11B
    }

    public readonly record struct SummonedCreatePacket(
        int OwnerCharacterId,
        int SummonedObjectId,
        int SkillId,
        int CharacterLevel,
        int SkillLevel,
        Vector2 Position,
        byte MoveAction,
        short FootholdId,
        byte MoveAbility,
        byte AssistType,
        byte EnterType,
        LoginAvatarLook AvatarLook,
        byte TeslaCoilState,
        IReadOnlyList<Point> TeslaTrianglePoints);

    public readonly record struct SummonedRemovePacket(
        int OwnerCharacterId,
        int SummonedObjectId,
        byte Reason);

    public readonly record struct SummonedHitPacket(
        int OwnerCharacterId,
        int SummonedObjectId,
        sbyte AttackIndex,
        int Damage,
        int? MobTemplateId,
        bool? MobFacingLeft);

    public readonly record struct SummonedAttackTargetPacket(
        int MobObjectId,
        byte HitAction,
        int Damage);

    public readonly record struct SummonedAttackPacket(
        int OwnerCharacterId,
        int SummonedObjectId,
        int CharacterLevel,
        byte AttackAction,
        bool FacingLeft,
        IReadOnlyList<SummonedAttackTargetPacket> Targets,
        byte TailByte);

    public readonly record struct PacketOwnedSummonTimerExpiration(
        int SkillId,
        int SummonedObjectId,
        int ExpireTime,
        int CurrentTime,
        int OwnerCharacterId,
        bool OwnerIsLocal);

    internal readonly record struct PacketOwnedOneTimeActionClip(
        SkillAnimation Animation,
        int BaseAnimationTime,
        int StartTime,
        int EndTime);

    internal readonly record struct PacketOwnedExpiryTargetCandidate(
        int MobObjectId,
        Rectangle Hitbox);

    internal readonly record struct PacketOwnedMobAttackFeedbackPresentation(
        MobAnimationSet.AttackInfoMetadata AttackInfo,
        MobAnimationSet.AttackHitEffectEntry HitEffectEntry,
        string CharDamSoundKey,
        int HitAfterMs,
        bool PreferTemplateCharDamSound = false);

    internal readonly record struct PacketOwnedHitMobCandidate(
        bool IsAlive,
        bool MatchesObservedAttack,
        bool MatchesCurrentAttack,
        int ObservedFrameIndex,
        bool MatchesPacketFacing);

    public sealed class SummonedPool
    {
        private const int TeslaCoilSkillId = 35111002;
        private const int HealingRobotSkillId = 35111011;
        private const int TeslaCoilMasterySkillId = 35120001;
        private const byte HealingRobotHealSkillAction = 13;
        private const int TeslaMinimumImpactDelayMs = 300;
        private const int PacketOwnedSummonBodyContactCooldownMs = 700;
        private const int PacketOwnedSummonPassiveEffectCooldownMs = 240;
        private const int PacketOwnedHitRetainedAttackFrameWindowMs = 240;
        private const int SummonHitPeriodDurationMs = 1500;

        private sealed class PacketOwnedSummonState
        {
            public ActiveSummon Summon { get; init; }
            public int OwnerCharacterId { get; init; }
            public int OwnerCharacterLevel { get; set; }
            public int SkillLevel { get; set; }
            public string OwnerName { get; set; }
            public bool OwnerIsLocal { get; set; }
            public LoginAvatarLook AvatarLook { get; set; }
            public byte TeslaCoilState { get; set; }
            public Point[] TeslaTrianglePoints { get; set; } = Array.Empty<Point>();
            public byte LastMoveActionRaw { get; set; }
            public short CurrentFootholdId { get; set; }
            public byte EnterType { get; set; }
            public byte LastSkillAction { get; set; }
            public byte LastAttackAction { get; set; }
            public byte LastAttackTailByte { get; set; }
            public IReadOnlyList<SummonedAttackTargetPacket> LastAttackTargets { get; set; } = Array.Empty<SummonedAttackTargetPacket>();
            public byte RemovalReason { get; set; }
            public int LastSkillTime { get; set; } = int.MinValue;
            public int LastHitTime { get; set; } = int.MinValue;
            public int LastHitDamage { get; set; }
            public int OneTimeAction { get; set; }
            public int OneTimeActionEndTime { get; set; } = int.MinValue;
            public PacketOwnedOneTimeActionClip? OneTimeActionClip { get; set; }
            public int LastCompletedAttackLayerRefreshStartTime { get; set; } = int.MinValue;
            public int LastPassiveMovementUpdateTime { get; set; } = int.MinValue;
            public PlayerMovementSyncSnapshot MovementSnapshot { get; set; }
        }

        internal readonly record struct RemoteSupportSummonCandidate(
            ActiveSummon Summon,
            int OwnerCharacterId,
            bool OwnerIsPartyMember);

        private sealed class PacketOwnedSummonTimer
        {
            public int SummonedObjectId { get; init; }
            public int SkillId { get; init; }
            public int ExpireTime { get; init; }
        }

        private sealed class PacketOwnedMobAttackHitEffectDisplay
        {
            public float X { get; init; }
            public float Y { get; init; }
            public int AttachedSummonObjectId { get; init; }
            public bool FollowSummon { get; init; }
            public bool FollowSummonFacing { get; init; }
            public bool MirrorOffsetWithSummonFacing { get; init; }
            public Vector2 AttachedOffset { get; init; }
            public MobAnimationSet.AttackInfoMetadata AttackInfo { get; init; }
            public int HitAnimationSourceFrameIndex { get; init; }
            public int StartTime { get; init; }
            public List<IDXObject> Frames { get; init; }
            public int CurrentFrame { get; set; }
            public int LastFrameTime { get; set; }
            public bool Flip { get; init; }
            public Color Tint { get; init; } = Color.White;
            public bool IsComplete { get; private set; }

            public void Update(int currentTime)
            {
                if (currentTime < StartTime)
                {
                    return;
                }

                if (IsComplete || Frames == null || Frames.Count == 0 || CurrentFrame >= Frames.Count)
                {
                    IsComplete = true;
                    return;
                }

                IDXObject frame = Frames[CurrentFrame];
                int delay = frame?.Delay ?? 100;
                if (currentTime - LastFrameTime < delay)
                {
                    return;
                }

                CurrentFrame++;
                LastFrameTime = currentTime;
                if (CurrentFrame >= Frames.Count)
                {
                    IsComplete = true;
                }
            }
        }

        private sealed class ScheduledPacketOwnedHitEffect
        {
            public long SequenceId { get; init; }
            public ActiveHitEffect HitEffect { get; init; }
            public int ExecuteTime { get; init; }
        }

        private sealed class ScheduledPacketOwnedReactiveChainEffect
        {
            public long SequenceId { get; init; }
            public Vector2 Source { get; init; }
            public Vector2 Target { get; init; }
            public int ExecuteTime { get; init; }
            public int DurationMs { get; init; }
            public SkillAnimation Animation { get; init; }
            public string AnimationPath { get; init; }
            public bool FacingRight { get; init; }
        }

        private sealed class ScheduledPacketOwnedSound
        {
            public long SequenceId { get; init; }
            public string SoundKey { get; init; }
            public int ExecuteTime { get; init; }
        }

        private sealed class PacketOwnedReactiveChainEffectDisplay
        {
            public Vector2 Source { get; init; }
            public Vector2 Target { get; init; }
            public int StartTime { get; init; }
            public int EndTime { get; init; }
            public SkillAnimation Animation { get; init; }
            public string AnimationPath { get; init; }
            public bool FacingRight { get; init; }

            public bool IsExpired(int currentTime)
            {
                return Animation?.Frames.Count <= 0
                       || currentTime >= EndTime;
            }
        }

        private readonly Dictionary<int, PacketOwnedSummonState> _summonsByObjectId = new();
        private readonly Dictionary<int, List<PacketOwnedSummonState>> _summonsByOwnerId = new();
        private readonly List<ActiveProjectile> _projectiles = new();
        private readonly List<ScheduledPacketOwnedHitEffect> _scheduledHitEffects = new();
        private readonly List<ScheduledPacketOwnedReactiveChainEffect> _scheduledReactiveChainEffects = new();
        private readonly List<ScheduledPacketOwnedSound> _scheduledSounds = new();
        private readonly List<PacketOwnedReactiveChainEffectDisplay> _reactiveChainEffects = new();
        private readonly List<ActiveHitEffect> _hitEffects = new();
        private readonly List<PacketOwnedMobAttackHitEffectDisplay> _mobAttackHitEffects = new();
        private readonly List<PacketOwnedSummonTileEffectDisplay> _summonTileEffects = new();
        private readonly List<PacketOwnedSummonTimer> _summonExpiryTimers = new();
        private long _nextScheduledHitEffectSequenceId = 1;
        private IReadOnlyCollection<SkillData> _cancelSkillCatalog;
        private readonly Random _random = new();
        private SkillLoader _skillLoader;
        private MobPool _mobPool;
        private RemoteUserActorPool _remoteUserPool;
        private TexturePool _texturePool;
        private GraphicsDevice _graphicsDevice;
        private Func<PlayerCharacter> _localPlayerAccessor;
        private Func<int, int> _localSkillLevelAccessor;
        private Func<int, int, int> _localCancelFamilyRemainingDurationAccessor;
        private SoundManager _soundManager;
        private CombatEffects _combatEffects;
        private AnimationEffects _animationEffects;

        public Action<PacketOwnedSummonTimerExpiration[]> OnSummonExpiryTimersExpiredBatch { get; set; }
        public Action<PacketOwnedSummonTimerExpiration> OnSummonExpiryTimerExpired { get; set; }
        public Action<SummonedAttackPacket, int> OnLocalOwnerAttackPacketApplied { get; set; }

        public int Count => _summonsByObjectId.Count;

        public void Initialize(
            SkillLoader skillLoader,
            MobPool mobPool,
            RemoteUserActorPool remoteUserPool,
            Func<PlayerCharacter> localPlayerAccessor,
            Func<int, int> localSkillLevelAccessor = null,
            Func<int, int, int> localCancelFamilyRemainingDurationAccessor = null,
            SoundManager soundManager = null,
            CombatEffects combatEffects = null,
            AnimationEffects animationEffects = null,
            TexturePool texturePool = null,
            GraphicsDevice graphicsDevice = null)
        {
            _skillLoader = skillLoader;
            _mobPool = mobPool;
            _remoteUserPool = remoteUserPool;
            _texturePool = texturePool;
            _graphicsDevice = graphicsDevice;
            _localPlayerAccessor = localPlayerAccessor;
            _localSkillLevelAccessor = localSkillLevelAccessor;
            _localCancelFamilyRemainingDurationAccessor = localCancelFamilyRemainingDurationAccessor;
            _soundManager = soundManager;
            _combatEffects = combatEffects;
            _animationEffects = animationEffects;
            _cancelSkillCatalog = null;
        }

        public void Clear()
        {
            foreach (PacketOwnedSummonState state in _summonsByObjectId.Values)
            {
                UnregisterOwnerSummon(state);
                RemovePuppet(state.Summon);
            }

            _summonsByObjectId.Clear();
            _summonsByOwnerId.Clear();
            _projectiles.Clear();
            _scheduledHitEffects.Clear();
            _scheduledReactiveChainEffects.Clear();
            _scheduledSounds.Clear();
            _reactiveChainEffects.Clear();
            _hitEffects.Clear();
            _mobAttackHitEffects.Clear();
            _summonTileEffects.Clear();
            _summonExpiryTimers.Clear();
            _cancelSkillCatalog = null;
        }

        public IReadOnlyList<ActiveSummon> GetSummonsForOwner(int ownerCharacterId)
        {
            return GetRegisteredOwnerSummons(ownerCharacterId);
        }

        public IReadOnlyList<ActiveSummon> GetSupportSummonsAffectingLocalPlayer(Func<int, bool> ownerIsPartyMemberEvaluator = null)
        {
            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            int localPlayerId = localPlayer?.Build?.Id ?? 0;
            if (localPlayerId <= 0)
            {
                return Array.Empty<ActiveSummon>();
            }

            IEnumerable<RemoteSupportSummonCandidate> candidates = _summonsByObjectId.Values
                .Where(state => state?.Summon != null
                                && state.OwnerCharacterId != localPlayerId
                                && state.Summon.AssistType == SummonAssistType.Support)
                .Select(state => new RemoteSupportSummonCandidate(
                    state.Summon,
                    state.OwnerCharacterId,
                    ownerIsPartyMemberEvaluator?.Invoke(state.OwnerCharacterId) == true));
            return SelectSupportSummonsAffectingLocalPlayer(candidates, localPlayerId);
        }

        internal static IReadOnlyList<ActiveSummon> SelectSupportSummonsAffectingLocalPlayer(
            IEnumerable<RemoteSupportSummonCandidate> candidates,
            int localPlayerId)
        {
            if (localPlayerId <= 0 || candidates == null)
            {
                return Array.Empty<ActiveSummon>();
            }

            List<RemoteSupportSummonCandidate> filteredCandidates = new();
            foreach (RemoteSupportSummonCandidate candidate in candidates)
            {
                ActiveSummon summon = candidate.Summon;
                if (summon?.SkillData == null
                    || summon.LevelData == null
                    || summon.IsPendingRemoval
                    || !CanRemoteSupportSummonAffectLocalPlayer(
                        summon,
                        localPlayerId,
                        candidate.OwnerCharacterId,
                        candidate.OwnerIsPartyMember))
                {
                    continue;
                }

                filteredCandidates.Add(candidate);
            }

            if (filteredCandidates.Count == 0)
            {
                return Array.Empty<ActiveSummon>();
            }

            Dictionary<(int OwnerCharacterId, int SkillId), RemoteSupportSummonCandidate> selectedSitdownSummons = new();
            foreach (RemoteSupportSummonCandidate candidate in filteredCandidates)
            {
                ActiveSummon summon = candidate.Summon;
                if (!SummonRuntimeRules.IsSitdownHealingSupportSummon(summon.SkillData))
                {
                    continue;
                }

                var key = (candidate.OwnerCharacterId, summon.SkillId);
                if (!selectedSitdownSummons.TryGetValue(key, out RemoteSupportSummonCandidate existing)
                    || IsPreferredSitdownHealingSupportSummon(candidate.Summon, existing.Summon))
                {
                    selectedSitdownSummons[key] = candidate;
                }
            }

            List<ActiveSummon> result = new(filteredCandidates.Count);
            HashSet<(int OwnerCharacterId, int SkillId)> emittedSitdownSummons = new();
            foreach (RemoteSupportSummonCandidate candidate in filteredCandidates)
            {
                ActiveSummon summon = candidate.Summon;
                if (!SummonRuntimeRules.IsSitdownHealingSupportSummon(summon.SkillData))
                {
                    result.Add(summon);
                    continue;
                }

                var key = (candidate.OwnerCharacterId, summon.SkillId);
                if (emittedSitdownSummons.Contains(key))
                {
                    continue;
                }

                if (selectedSitdownSummons.TryGetValue(key, out RemoteSupportSummonCandidate selected)
                    && ReferenceEquals(selected.Summon, summon))
                {
                    result.Add(summon);
                    emittedSitdownSummons.Add(key);
                }
            }

            return result.Count > 0
                ? result.ToArray()
                : Array.Empty<ActiveSummon>();
        }

        internal static bool CanRemoteSupportSummonAffectLocalPlayer(
            ActiveSummon summon,
            int localPlayerId,
            int ownerCharacterId,
            bool ownerIsPartyMember)
        {
            if (summon?.SkillData == null || localPlayerId <= 0 || ownerCharacterId <= 0)
            {
                return false;
            }

            return RemoteAffectedAreaSupportResolver.CanAffectLocalPlayer(
                summon.SkillData,
                localPlayerId,
                ownerCharacterId,
                ownerIsPartyMember,
                ownerIsSameTeamMember: false,
                summon.LevelData);
        }

        internal static bool IsPreferredSitdownHealingSupportSummon(ActiveSummon candidate, ActiveSummon existing)
        {
            if (candidate == null)
            {
                return false;
            }

            if (existing == null)
            {
                return true;
            }

            if (candidate.StartTime != existing.StartTime)
            {
                return candidate.StartTime > existing.StartTime;
            }

            return candidate.ObjectId > existing.ObjectId;
        }

        public bool TryConsumeSummonByObjectId(int objectId)
        {
            if (!_summonsByObjectId.TryGetValue(objectId, out PacketOwnedSummonState state))
            {
                return false;
            }

            RemoveState(state);
            return true;
        }

        public int RemoveOwnerSummons(int ownerCharacterId, int currentTime, byte reason = 0)
        {
            if (ownerCharacterId <= 0)
            {
                return 0;
            }

            List<PacketOwnedSummonState> ownedStates = EnumerateOwnerSummonStates(ownerCharacterId)
                .Where(static state => state?.Summon != null && !state.Summon.IsPendingRemoval)
                .ToList();
            foreach (PacketOwnedSummonState state in ownedStates)
            {
                BeginRemoval(state, currentTime, reason);
            }

            return ownedStates.Count;
        }

        public bool TryCancelLocalOwnerSummonsBySkillRequest(int requestedSkillId, int currentTime)
        {
            if (requestedSkillId <= 0)
            {
                return false;
            }

            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            int localOwnerId = localPlayer?.Build?.Id ?? 0;
            if (localOwnerId <= 0)
            {
                return false;
            }

            int removedCount = 0;
            foreach (PacketOwnedSummonState state in EnumerateOwnerSummonStates(localOwnerId)
                         .OrderByDescending(static candidate => candidate?.Summon?.StartTime ?? int.MinValue)
                         .ThenByDescending(static candidate => candidate?.Summon?.ObjectId ?? int.MinValue)
                         .ToArray())
            {
                if (state?.Summon == null
                    || state.Summon.IsPendingRemoval
                    || !DoesClientCancelMatchSkillId(state.Summon.SkillId, requestedSkillId))
                {
                    continue;
                }

                if (TryCancelLocalOwnerSummonByClientRequest(state, currentTime))
                {
                    removedCount++;
                }
            }

            return removedCount > 0;
        }

        private bool TryCancelLocalOwnerSummonByClientRequest(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon == null || state.Summon.IsPendingRemoval)
            {
                return false;
            }

            if (ShouldUseNaturalExpiryPlaybackOnClientCancel(state.Summon, currentTime))
            {
                if (TryBeginSelfDestructRemoval(state, currentTime, requiresNaturalExpiry: true))
                {
                    return true;
                }

                if (TryTriggerExpiredSelfDestructAction(state, currentTime))
                {
                    return true;
                }

                if (TryBeginNaturalExpiryRemoval(state, currentTime))
                {
                    return true;
                }
            }

            BeginRemoval(state, currentTime, reason: 0);
            return true;
        }

        public bool TryPrimeLocalOwnerSummonNaturalExpiry(int summonedObjectId, int currentTime)
        {
            if (summonedObjectId <= 0
                || !_summonsByObjectId.TryGetValue(summonedObjectId, out PacketOwnedSummonState state)
                || state?.Summon == null
                || !state.OwnerIsLocal
                || state.Summon.IsPendingRemoval
                || state.Summon.ExpiryActionTriggered)
            {
                return false;
            }

            if (TryBeginSelfDestructRemoval(state, currentTime, requiresNaturalExpiry: true))
            {
                return true;
            }

            if (TryTriggerExpiredSelfDestructAction(state, currentTime))
            {
                return true;
            }

            return TryBeginNaturalExpiryRemoval(state, currentTime);
        }

        public bool TryDamageSummonByObjectId(int objectId, int damage, int currentTime)
        {
            if (!_summonsByObjectId.TryGetValue(objectId, out PacketOwnedSummonState state))
            {
                return false;
            }

            ApplySummonDamage(state, Math.Max(1, damage), currentTime, useHitAnimationState: true);
            return true;
        }

        public bool TryDispatchPacket(int packetType, byte[] payload, int currentTime, out string message)
        {
            message = null;
            if (payload == null || payload.Length < sizeof(int))
            {
                message = "Summoned packet payload is missing the owning character id.";
                return false;
            }

            if (!Enum.IsDefined(typeof(SummonedPacketType), packetType))
            {
                message = $"Unsupported summoned packet type: 0x{packetType:X}.";
                return false;
            }

            var reader = new PacketReader(payload);
            int ownerCharacterId = reader.ReadInt32();

            try
            {
                switch ((SummonedPacketType)packetType)
                {
                    case SummonedPacketType.Created:
                        return TryHandleCreated(ownerCharacterId, ref reader, currentTime, out message);
                    case SummonedPacketType.Removed:
                        return TryHandleRemoved(ownerCharacterId, ref reader, currentTime, out message);
                    case SummonedPacketType.Move:
                        return TryHandleMove(ownerCharacterId, ref reader, currentTime, out message);
                    case SummonedPacketType.Attack:
                        return TryHandleAttack(ownerCharacterId, ref reader, currentTime, out message);
                    case SummonedPacketType.Skill:
                        return TryHandleSkill(ownerCharacterId, ref reader, currentTime, out message);
                    case SummonedPacketType.Hit:
                        return TryHandleHit(ownerCharacterId, ref reader, currentTime, out message);
                    default:
                        message = $"Unsupported summoned packet type: 0x{packetType:X}.";
                        return false;
                }
            }
            catch (InvalidOperationException ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public bool TryCreate(SummonedCreatePacket packet, int currentTime, out string message)
        {
            message = null;
            if (packet.OwnerCharacterId <= 0)
            {
                message = "Summoned create packet requires a positive owner character id.";
                return false;
            }

            if (packet.SummonedObjectId <= 0)
            {
                message = "Summoned create packet requires a positive summoned object id.";
                return false;
            }

            ResolveOwnerState(packet.OwnerCharacterId, out string ownerName, out bool ownerIsLocal, out bool ownerFacingRight);
            SkillData skill = _skillLoader?.LoadSkill(packet.SkillId);
            int resolvedSkillLevel = ResolvePacketOwnedSkillLevel(skill, packet.SkillId, packet.SkillLevel, ownerIsLocal);
            SkillLevelData levelData = skill?.GetLevel(resolvedSkillLevel);
            int durationMs = ResolvePacketOwnedCreateDurationMs(
                skill,
                levelData,
                resolvedSkillLevel,
                packet.SkillId,
                ownerIsLocal,
                currentTime,
                ResolveLocalOwnerCancelFamilyDurationMs,
                _localSkillLevelAccessor,
                ResolveCancelSkillData,
                GetCancelSkillCatalog());

            RemoveExistingState(packet.SummonedObjectId);

            bool facingRight = packet.MoveAction == 0
                ? ownerFacingRight
                : DecodeFacingRight(packet.MoveAction);

            SummonAssistType assistType = Enum.IsDefined(typeof(SummonAssistType), (int)packet.AssistType)
                ? (SummonAssistType)packet.AssistType
                : ResolveSummonAssistType(skill);

            var summon = new ActiveSummon
            {
                ObjectId = packet.SummonedObjectId,
                SkillId = packet.SkillId,
                Level = resolvedSkillLevel,
                StartTime = currentTime,
                Duration = durationMs,
                LastAttackTime = currentTime,
                MoveAbility = packet.MoveAbility,
                MovementStyle = skill?.SummonMovementStyle ?? SummonMovementResolver.ResolveStyle(packet.MoveAbility),
                SpawnDistanceX = skill?.SummonSpawnDistanceX ?? SummonMovementResolver.ResolveSpawnDistanceX(packet.SkillId),
                AnchorX = packet.Position.X,
                AnchorY = packet.Position.Y,
                PreviousPositionX = packet.Position.X,
                PreviousPositionY = packet.Position.Y,
                PositionX = packet.Position.X,
                PositionY = packet.Position.Y,
                SkillData = skill,
                LevelData = levelData,
                FacingRight = facingRight,
                AssistType = assistType,
                ManualAssistEnabled = assistType != SummonAssistType.ManualAttack,
                LastStateChangeTime = currentTime,
                MaxHealth = ResolveSummonMaxHealth(levelData),
                CurrentHealth = ResolveSummonMaxHealth(levelData),
                TeslaCoilState = packet.TeslaCoilState
            };

            var state = new PacketOwnedSummonState
            {
                Summon = summon,
                OwnerCharacterId = packet.OwnerCharacterId,
                OwnerCharacterLevel = Math.Max(1, packet.CharacterLevel),
                SkillLevel = resolvedSkillLevel,
                OwnerName = ownerName,
                OwnerIsLocal = ownerIsLocal,
                AvatarLook = packet.AvatarLook,
                TeslaCoilState = packet.TeslaCoilState,
                TeslaTrianglePoints = packet.TeslaTrianglePoints?.ToArray() ?? Array.Empty<Point>(),
                LastMoveActionRaw = packet.MoveAction,
                CurrentFootholdId = packet.FootholdId,
                EnterType = packet.EnterType
            };

            _summonsByObjectId[summon.ObjectId] = state;
            GetOrCreateOwnerList(packet.OwnerCharacterId).Add(state);
            RegisterOwnerSummon(state);
            RegisterSummonExpiryTimer(summon);
            SyncPuppet(state, currentTime);
            ApplyCreatedOwnerSideEffects(state, currentTime);
            message = $"Summoned {summon.ObjectId} for owner {packet.OwnerCharacterId} created via packet-owned pool.";
            return true;
        }

        public bool TryApplyMoveSnapshot(int ownerCharacterId, int summonObjectId, PlayerMovementSyncSnapshot movementSnapshot, byte moveAction, int currentTime, out string message)
        {
            message = null;
            if (!_summonsByObjectId.TryGetValue(summonObjectId, out PacketOwnedSummonState state))
            {
                message = $"Summoned {summonObjectId} does not exist.";
                return false;
            }

            if (state.OwnerCharacterId != ownerCharacterId)
            {
                message = $"Summoned {summonObjectId} is owned by {state.OwnerCharacterId}, not {ownerCharacterId}.";
                return false;
            }

            state.MovementSnapshot = movementSnapshot ?? throw new ArgumentNullException(nameof(movementSnapshot));
            state.LastMoveActionRaw = moveAction;
            ApplyMovementSnapshot(state, currentTime);
            SyncPuppet(state, currentTime);
            return true;
        }

        public bool TryRemove(SummonedRemovePacket packet, int currentTime, out string message)
        {
            message = null;
            if (!_summonsByObjectId.TryGetValue(packet.SummonedObjectId, out PacketOwnedSummonState state))
            {
                message = $"Summoned {packet.SummonedObjectId} does not exist.";
                return false;
            }

            BeginRemoval(state, currentTime, packet.Reason);
            message = $"Summoned {packet.SummonedObjectId} removal queued.";
            return true;
        }

        public bool TryMarkAttack(SummonedAttackPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!TryGetOwnedState(packet.OwnerCharacterId, packet.SummonedObjectId, out PacketOwnedSummonState state, out message))
            {
                return false;
            }

            state.OwnerCharacterLevel = Math.Max(1, packet.CharacterLevel);
            state.LastAttackAction = packet.AttackAction;
            state.LastAttackTailByte = packet.TailByte;
            state.LastAttackTargets = packet.Targets ?? Array.Empty<SummonedAttackTargetPacket>();
            state.Summon.LastAttackTime = currentTime;
            PromotePacketOwnedTeslaRuntimeState(state);
            state.Summon.FacingRight = PacketOwnedSummonUpdateRules.ResolvePacketAttackFacingRight(
                state.Summon,
                state.LastMoveActionRaw,
                packet.FacingLeft,
                state.Summon.FacingRight);
            BeginPacketOwnedAttackAnimation(state, currentTime);

            SpawnPacketAttackVisuals(state, currentTime);
            TryRegisterClientOwnedAttackTileOverlay(state, currentTime);
            if (state.OwnerIsLocal)
            {
                OnLocalOwnerAttackPacketApplied?.Invoke(packet, currentTime);
            }

            return true;
        }

        public bool TryMarkSkill(int ownerCharacterId, int summonObjectId, byte attackAction, int currentTime, out string message)
        {
            message = null;
            if (!TryGetOwnedState(ownerCharacterId, summonObjectId, out PacketOwnedSummonState state, out message))
            {
                return false;
            }

            state.LastSkillAction = (byte)(attackAction & 0x7F);
            state.LastSkillTime = currentTime;
            PromotePacketOwnedTeslaRuntimeState(state);
            if (ShouldApplyHealingRobotSkillPacketFacing(state.Summon, state.LastSkillAction))
            {
                state.Summon.FacingRight = ResolveHealingRobotSkillPacketFacingRight(attackAction);
            }

            BeginPacketOwnedSkillAnimation(state, currentTime);
            return true;
        }

        public bool TryMarkHit(SummonedHitPacket packet, int currentTime, out string message)
        {
            message = null;
            if (!TryGetOwnedState(packet.OwnerCharacterId, packet.SummonedObjectId, out PacketOwnedSummonState state, out message))
            {
                return false;
            }

            state.LastHitTime = currentTime;
            state.LastHitDamage = packet.Damage;
            state.Summon.LastStateChangeTime = currentTime;
            PlayPacketMobAttackFeedback(state, packet, currentTime);
            if (packet.Damage > 0)
            {
                bool useHitAnimationState = PreparePacketOwnedHitAction(state, currentTime);
                ApplySummonDamage(state, packet.Damage, currentTime, useHitAnimationState);
            }
            else
            {
                state.Summon.HitPeriodRemainingMs = -ResolveSummonHitPeriodDurationMs(state.Summon);
                state.Summon.LastHitPeriodUpdateTime = currentTime;
                state.Summon.HitFlashCounter++;
            }

            PlayPacketIncDecHpFeedback(state.Summon, packet.Damage, currentTime);
            return true;
        }

        private void BeginPacketOwnedSkillAnimation(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon == null)
            {
                return;
            }

            PreparePacketOwnedSkillActionOwner(state);
            state.Summon.LastAttackAnimationStartTime = currentTime;
            state.Summon.CurrentAnimationBranchName = ResolvePacketOwnedSkillBranch(state);
            ArmPacketOwnedOneTimeAction(state, currentTime, state.LastSkillAction, isSkillAction: true);
            ArmPacketOwnedSupportSuspend(state, currentTime);
            bool hasPrepareAnimation = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                state.Summon.SkillData,
                state.Summon.CurrentAnimationBranchName) > 0;
            state.Summon.ActorState = hasPrepareAnimation
                ? SummonActorState.Prepare
                : SummonActorState.Attack;
            state.Summon.LastStateChangeTime = currentTime;

            if (state.Summon.SkillId != TeslaCoilSkillId)
            {
                return;
            }

            foreach (PacketOwnedSummonState teslaState in EnumerateOwnerSummonStates(state.OwnerCharacterId)
                         .Where(static candidate => candidate?.Summon?.SkillId == TeslaCoilSkillId && !candidate.Summon.IsPendingRemoval))
            {
                PromotePacketOwnedTeslaRuntimeState(teslaState);
                PreparePacketOwnedSkillActionOwner(teslaState);
                teslaState.Summon.CurrentAnimationBranchName = state.Summon.CurrentAnimationBranchName;
                teslaState.Summon.LastAttackAnimationStartTime = currentTime;
                ArmPacketOwnedOneTimeAction(teslaState, currentTime, state.LastSkillAction, isSkillAction: true);
                teslaState.Summon.ActorState = hasPrepareAnimation
                    ? SummonActorState.Prepare
                    : SummonActorState.Attack;
                teslaState.Summon.LastStateChangeTime = currentTime;
            }
        }

        private void PreparePacketOwnedSkillActionOwner(PacketOwnedSummonState state)
        {
            if (state?.Summon == null)
            {
                return;
            }

            ClearPacketOwnedOneTimeAction(state);
            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
        }

        private static void PromotePacketOwnedTeslaRuntimeState(PacketOwnedSummonState state)
        {
            if (state?.Summon?.SkillId != TeslaCoilSkillId)
            {
                return;
            }

            byte resolvedState = PacketOwnedSummonUpdateRules.ResolvePacketOwnedTeslaRuntimeState(
                state.TeslaCoilState,
                state.Summon.TeslaCoilState);
            state.TeslaCoilState = resolvedState;
            state.Summon.TeslaCoilState = resolvedState;
        }

        private static void ArmPacketOwnedSupportSuspend(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon?.SkillData == null)
            {
                return;
            }

            bool preferHealFirst = SummonRuntimeRules.HasMinionAbilityToken(
                state.Summon.SkillData.MinionAbility,
                "heal");
            int suspendDurationMs = ResolvePacketOwnedSupportSuspendDurationMs(
                state.Summon,
                preferHealFirst,
                HealingRobotSkillId);
            if (!SummonRuntimeRules.ShouldTrackSupportSuspendWindow(
                    state.Summon.SkillData,
                    state.Summon.AssistType,
                    preferHealFirst,
                    state.Summon.CurrentAnimationBranchName,
                    suspendDurationMs))
            {
                return;
            }

            state.Summon.SupportSuspendUntilTime = suspendDurationMs > 0
                ? currentTime + suspendDurationMs
                : int.MinValue;
        }

        internal static int ResolvePacketOwnedSupportSuspendDurationMs(
            ActiveSummon summon,
            bool preferHealFirst,
            int healingRobotSkillId)
        {
            if (summon?.SkillData == null)
            {
                return 0;
            }

            int suspendDurationMs = SummonRuntimeRules.ResolveTrackedSuspendDurationMs(
                summon.SkillData,
                summon.AssistType,
                preferHealFirst,
                explicitBranchName: summon.CurrentAnimationBranchName);
            if (suspendDurationMs <= 0
                && summon.SkillId == healingRobotSkillId
                && SummonRuntimeRules.IsSitdownHealingSupportSummon(summon.SkillData))
            {
                suspendDurationMs = GetSkillAnimationDuration(summon.SkillData.SummonAttackAnimation)
                    ?? summon.SkillData.SummonAttackHitDelayMs;
            }

            if (suspendDurationMs <= 0
                && summon.SkillId == healingRobotSkillId
                && SummonRuntimeRules.IsSitdownHealingSupportSummon(summon.SkillData))
            {
                suspendDurationMs = summon.SkillData.HitEffect?.TotalDuration ?? 0;
            }

            return Math.Max(0, suspendDurationMs);
        }

        private void BeginPacketOwnedAttackAnimation(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon == null)
            {
                return;
            }

            ClearPacketOwnedOneTimeAction(state);
            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            state.Summon.CurrentAnimationBranchName = SummonRuntimeRules.ResolvePacketAttackBranch(
                state.Summon.SkillData,
                state.LastAttackAction);
            ArmPacketOwnedOneTimeAction(state, currentTime, state.LastAttackAction, isSkillAction: false);
            int prepareDuration = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                state.Summon.SkillData,
                state.Summon.CurrentAnimationBranchName);
            if (state.Summon.LastAttackAnimationStartTime == int.MinValue
                || prepareDuration <= 0
                || currentTime - state.Summon.LastAttackAnimationStartTime > prepareDuration)
            {
                state.Summon.LastAttackAnimationStartTime = currentTime;
            }

            state.Summon.ActorState = SummonActorState.Attack;
            state.Summon.LastStateChangeTime = currentTime;
        }

        private static void ArmPacketOwnedOneTimeAction(
            PacketOwnedSummonState state,
            int currentTime,
            byte rawAction,
            bool isSkillAction)
        {
            if (state?.Summon?.SkillData == null)
            {
                return;
            }

            byte normalizedAction = (byte)(rawAction & 0x7F);
            SkillAnimation actionAnimation = ResolvePacketOwnedOneTimeActionAnimation(state, normalizedAction, isSkillAction);
            if (actionAnimation?.Frames.Count <= 0)
            {
                return;
            }

            int duration = GetSkillAnimationDuration(actionAnimation) ?? 0;
            if (duration <= 0)
            {
                return;
            }

            state.OneTimeAction = normalizedAction;
            state.OneTimeActionEndTime = currentTime + duration;
            state.Summon.OneTimeActionFallbackAnimation = actionAnimation;
            state.Summon.OneTimeActionFallbackStartTime = currentTime;
            state.Summon.OneTimeActionFallbackAnimationTime = 0;
            state.Summon.OneTimeActionFallbackEndTime = currentTime + duration;
        }

        private static SkillAnimation ResolvePacketOwnedOneTimeActionAnimation(
            PacketOwnedSummonState state,
            byte normalizedAction,
            bool isSkillAction)
        {
            SkillData skill = state?.Summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            string branchName = isSkillAction
                ? SummonRuntimeRules.ResolvePacketSkillBranch(skill, normalizedAction, state.Summon.AssistType)
                : SummonRuntimeRules.ResolvePacketAttackBranch(skill, normalizedAction);
            if (string.IsNullOrWhiteSpace(branchName))
            {
                branchName = state.Summon.CurrentAnimationBranchName;
            }

            if (!string.IsNullOrWhiteSpace(branchName))
            {
                if (skill.SummonActionAnimations != null
                    && skill.SummonActionAnimations.TryGetValue(branchName, out SkillAnimation actionAnimation)
                    && actionAnimation?.Frames.Count > 0)
                {
                    return actionAnimation;
                }

                if (skill.SummonNamedAnimations != null
                    && skill.SummonNamedAnimations.TryGetValue(branchName, out SkillAnimation namedAnimation)
                    && namedAnimation?.Frames.Count > 0)
                {
                    return namedAnimation;
                }

                SkillAnimation retryAnimation = ResolveEmptyActionRetryAnimation(state.Summon);
                if (retryAnimation?.Frames.Count > 0)
                {
                    return retryAnimation;
                }

                return null;
            }

            return isSkillAction
                ? ResolvePacketPendingRemovalActionAnimation(state.Summon)
                : ResolveAttackAnimation(state.Summon);
        }

        private static SkillAnimation ResolveEmptyActionRetryAnimation(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill?.SummonActionAnimations == null || skill.SummonActionAnimations.Count == 0)
            {
                return null;
            }

            string retryBranchName = SummonRuntimeRules.ResolveEmptyActionRetryBranch(skill);
            if (string.IsNullOrWhiteSpace(retryBranchName))
            {
                return null;
            }

            return skill.SummonActionAnimations.TryGetValue(retryBranchName, out SkillAnimation retryAnimation)
                   && retryAnimation?.Frames.Count > 0
                ? retryAnimation
                : null;
        }

        private static void ClearPacketOwnedOneTimeAction(PacketOwnedSummonState state)
        {
            if (state?.Summon == null)
            {
                return;
            }

            state.OneTimeAction = 0;
            state.OneTimeActionEndTime = int.MinValue;
            state.OneTimeActionClip = null;
            state.Summon.OneTimeActionFallbackAnimation = null;
            state.Summon.OneTimeActionFallbackStartTime = int.MinValue;
            state.Summon.OneTimeActionFallbackAnimationTime = int.MinValue;
            state.Summon.OneTimeActionFallbackEndTime = int.MinValue;
        }

        private void SpawnPacketAttackVisuals(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon?.SkillData == null || state.LastAttackTargets == null || _mobPool == null)
            {
                return;
            }

            List<MobItem> targets = ResolvePacketAttackTargets(state.LastAttackTargets, _mobPool.GetMob);
            if (targets.Count == 0)
            {
                return;
            }

            if (state.Summon.SkillId == TeslaCoilSkillId)
            {
                List<PacketOwnedSummonState> teslaStates = EnumerateOwnerSummonStates(state.OwnerCharacterId)
                    .Where(static candidate => candidate?.Summon?.SkillId == TeslaCoilSkillId && !candidate.Summon.IsPendingRemoval)
                    .OrderBy(static candidate => candidate.Summon.StartTime)
                    .ThenBy(static candidate => candidate.Summon.ObjectId)
                    .ToList();

                if (teslaStates.Count > 0)
                {
                    foreach (PacketOwnedSummonState teslaState in teslaStates)
                    {
                        PromotePacketOwnedTeslaRuntimeState(teslaState);
                        BeginPacketOwnedAttackAnimation(teslaState, currentTime);
                    }

                    SpawnPacketTeslaTriangleEffect(teslaStates, currentTime);
                    SpawnPacketTeslaAttackProjectiles(teslaStates, targets, currentTime, useTeslaPerTargetDelayJitter: true);
                    SchedulePacketAttackImpactEffects(teslaStates[0].Summon, targets, currentTime, useTeslaPerTargetDelayJitter: true);
                    return;
                }
            }

            if (!PacketOwnedSummonUpdateRules.ShouldRegisterClientOwnedReactiveAttackChainEffect(state.Summon))
            {
                SpawnPacketSummonProjectiles(state.Summon, targets, currentTime);
            }

            SchedulePacketAttackImpactEffects(state.Summon, targets, currentTime);
            SchedulePacketReactiveChainEffects(state, targets, currentTime);
        }

        private void TryRegisterClientOwnedAttackTileOverlay(PacketOwnedSummonState state, int currentTime)
        {
            int resolvedSkillLevel = state?.SkillLevel > 0
                ? state.SkillLevel
                : Math.Max(1, state?.Summon?.Level ?? 1);
            int resolvedOwnerCharacterLevel = Math.Max(1, state?.OwnerCharacterLevel ?? 1);

            if (state?.Summon == null
                || _mobPool == null
                || !PacketOwnedSummonUpdateRules.ShouldRegisterClientOwnedAttackTileOverlay(
                    state.Summon,
                    resolvedSkillLevel,
                    resolvedOwnerCharacterLevel))
            {
                return;
            }

            MobItem targetMob = ResolvePacketAttackTargets(state.LastAttackTargets, _mobPool.GetMob)
                .FirstOrDefault();
            if (targetMob == null)
            {
                return;
            }

            Vector2 targetAnchor = ResolvePacketAttackImpactPosition(
                state.Summon.SkillData,
                0,
                state.Summon.CurrentAnimationBranchName,
                targetMob,
                new Vector2(state.Summon.PositionX, state.Summon.PositionY),
                currentTime);
            Rectangle area = PacketOwnedSummonUpdateRules.BuildClientOwnedAttackTileOverlayArea(
                targetAnchor,
                state.Summon,
                state.Summon.CurrentAnimationBranchName);
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            SkillAnimation zoneAnimation = state.Summon.SkillData?.ZoneEffect?.ResolveAnimationVariant(
                                            resolvedSkillLevel,
                                            resolvedOwnerCharacterLevel,
                                            state.Summon.SkillData?.MaxLevel ?? 0)
                                        ?? state.Summon.SkillData?.ZoneAnimation;
            if (zoneAnimation?.Frames.Count <= 0)
            {
                return;
            }

            int attackDelayMs = PacketOwnedSummonUpdateRules.ResolveClientOwnedPostAttackEffectDelayMs(state.Summon);
            const int tileDelayMs = 200;
            const int tileDurationMs = 500;
            _summonTileEffects.Add(new PacketOwnedSummonTileEffectDisplay
            {
                Animation = zoneAnimation,
                AnimationPath = PacketOwnedSummonUpdateRules.ResolveClientOwnedTileOverlayAnimationPath(
                    state.Summon,
                    resolvedSkillLevel,
                    resolvedOwnerCharacterLevel),
                Area = area,
                EffectDistance = PacketOwnedSummonUpdateRules.ResolveClientOwnedTileOverlayEffectDistance(
                    state.Summon,
                    resolvedSkillLevel,
                    resolvedOwnerCharacterLevel),
                StartTime = currentTime + attackDelayMs + tileDelayMs,
                EndTime = currentTime + attackDelayMs + tileDelayMs + tileDurationMs,
                StartAlpha = 128,
                EndAlpha = byte.MaxValue
            });
        }

        internal static List<MobItem> ResolvePacketAttackTargets(
            IEnumerable<SummonedAttackTargetPacket> targets,
            Func<int, MobItem> mobResolver)
        {
            var resolvedTargets = new List<MobItem>();
            if (targets == null || mobResolver == null)
            {
                return resolvedTargets;
            }

            foreach (SummonedAttackTargetPacket target in targets)
            {
                if (target.MobObjectId <= 0)
                {
                    continue;
                }

                MobItem mob = mobResolver(target.MobObjectId);
                if (IsMobEligibleForPacketOwnedTargeting(mob))
                {
                    resolvedTargets.Add(mob);
                }
            }

            return resolvedTargets;
        }

        public void Update(int currentTime)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                ActiveProjectile projectile = _projectiles[i];
                projectile.Update(1f / 60f, currentTime);
                if (projectile.IsExpired)
                {
                    _projectiles.RemoveAt(i);
                }
            }

            ScheduledPacketOwnedHitEffect[] dueHitEffects = _scheduledHitEffects
                .Where(effect => effect != null && effect.ExecuteTime <= currentTime)
                .OrderBy(effect => effect.ExecuteTime)
                .ThenBy(effect => effect.SequenceId)
                .ToArray();
            if (dueHitEffects.Length > 0)
            {
                _scheduledHitEffects.RemoveAll(effect => effect != null && effect.ExecuteTime <= currentTime);
                foreach (ScheduledPacketOwnedHitEffect scheduledEffect in dueHitEffects)
                {
                    if (scheduledEffect.HitEffect != null)
                    {
                        _hitEffects.Add(scheduledEffect.HitEffect);
                    }
                }
            }

            ScheduledPacketOwnedReactiveChainEffect[] dueReactiveChainEffects = _scheduledReactiveChainEffects
                .Where(effect => effect != null && effect.ExecuteTime <= currentTime)
                .OrderBy(effect => effect.ExecuteTime)
                .ThenBy(effect => effect.SequenceId)
                .ToArray();
            if (dueReactiveChainEffects.Length > 0)
            {
                _scheduledReactiveChainEffects.RemoveAll(effect => effect != null && effect.ExecuteTime <= currentTime);
                foreach (ScheduledPacketOwnedReactiveChainEffect scheduledEffect in dueReactiveChainEffects)
                {
                    if (scheduledEffect.Animation?.Frames.Count > 0)
                    {
                        _reactiveChainEffects.Add(new PacketOwnedReactiveChainEffectDisplay
                        {
                            Source = scheduledEffect.Source,
                            Target = scheduledEffect.Target,
                            StartTime = scheduledEffect.ExecuteTime,
                            EndTime = scheduledEffect.ExecuteTime + Math.Max(1, scheduledEffect.DurationMs),
                            Animation = scheduledEffect.Animation,
                            AnimationPath = scheduledEffect.AnimationPath,
                            FacingRight = scheduledEffect.FacingRight
                        });
                    }
                    else
                    {
                        _animationEffects?.AddBlueLightning(
                            scheduledEffect.Source,
                            scheduledEffect.Target,
                            scheduledEffect.DurationMs,
                            currentTime);
                    }
                }
            }

            ScheduledPacketOwnedSound[] dueSounds = _scheduledSounds
                .Where(sound => sound != null && sound.ExecuteTime <= currentTime)
                .OrderBy(sound => sound.ExecuteTime)
                .ThenBy(sound => sound.SequenceId)
                .ToArray();
            if (dueSounds.Length > 0)
            {
                _scheduledSounds.RemoveAll(sound => sound != null && sound.ExecuteTime <= currentTime);
                foreach (ScheduledPacketOwnedSound scheduledSound in dueSounds)
                {
                    if (!string.IsNullOrWhiteSpace(scheduledSound.SoundKey))
                    {
                        _soundManager?.PlaySound(scheduledSound.SoundKey);
                    }
                }
            }

            for (int i = _reactiveChainEffects.Count - 1; i >= 0; i--)
            {
                if (_reactiveChainEffects[i].IsExpired(currentTime))
                {
                    _reactiveChainEffects.RemoveAt(i);
                }
            }

            for (int i = _hitEffects.Count - 1; i >= 0; i--)
            {
                if (_hitEffects[i].IsExpired(currentTime))
                {
                    _hitEffects.RemoveAt(i);
                }
            }

            for (int i = _mobAttackHitEffects.Count - 1; i >= 0; i--)
            {
                PacketOwnedMobAttackHitEffectDisplay effect = _mobAttackHitEffects[i];
                effect.Update(currentTime);
                if (effect.IsComplete)
                {
                    _mobAttackHitEffects.RemoveAt(i);
                }
            }

            for (int i = _summonTileEffects.Count - 1; i >= 0; i--)
            {
                if (_summonTileEffects[i].IsExpired(currentTime))
                {
                    _summonTileEffects.RemoveAt(i);
                }
            }

            UpdateSummonExpiryTimers(currentTime);

            foreach (PacketOwnedSummonState state in _summonsByObjectId.Values.ToArray())
            {
                ResolveOwnerState(state.OwnerCharacterId, out string ownerName, out bool ownerIsLocal, out bool ownerFacingRight);
                state.OwnerName = ownerName;
                state.OwnerIsLocal = ownerIsLocal;
                SyncOwnerSummonRegistration(state);
                AdvanceSummonHitPeriod(state.Summon, currentTime);

                if (state.MovementSnapshot != null)
                {
                    ApplyMovementSnapshot(state, currentTime);
                }
                else if (TryApplyPassiveMovement(state, currentTime, ownerFacingRight))
                {
                    // Client `CSummoned::Update` falls back to passive vec-ctrl updates when no move path is active.
                }
                else if (state.LastMoveActionRaw != 0)
                {
                    state.Summon.FacingRight = DecodeFacingRight(state.LastMoveActionRaw);
                }
                else
                {
                    state.Summon.FacingRight = ownerFacingRight;
                }

                RefreshIdleActorState(state, currentTime);

                if (state.Summon.IsPendingRemoval
                    && !ShouldDeferSummonRemovalPlayback(state.Summon, currentTime)
                    && state.Summon.ActorState != SummonActorState.Die)
                {
                    state.Summon.ActorState = SummonActorState.Die;
                    state.Summon.LastStateChangeTime = currentTime;
                }

                if (TryResolveBodyContactDamage(state, currentTime))
                {
                    if (state.Summon.IsPendingRemoval && currentTime >= state.Summon.PendingRemovalTime)
                    {
                        RemoveState(state);
                    }

                    continue;
                }

                if (state.Summon.IsPendingRemoval && currentTime >= state.Summon.PendingRemovalTime)
                {
                    RemoveState(state);
                    continue;
                }

                SyncPuppet(state, currentTime);
                ApplyLocalOwnerPuppetAggro(state);
            }
        }

        public void Draw(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            foreach (PacketOwnedSummonState state in _summonsByObjectId.Values
                .OrderBy(static value => value.Summon.PositionY)
                .ThenBy(static value => value.OwnerCharacterId))
            {
                DrawSummon(spriteBatch, state, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (ActiveProjectile projectile in _projectiles)
            {
                DrawProjectile(spriteBatch, projectile, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (ActiveHitEffect hitEffect in _hitEffects)
            {
                DrawHitEffect(spriteBatch, hitEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (PacketOwnedSummonTileEffectDisplay tileEffect in _summonTileEffects)
            {
                DrawSummonTileEffect(spriteBatch, tileEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (PacketOwnedReactiveChainEffectDisplay chainEffect in _reactiveChainEffects)
            {
                DrawReactiveChainEffect(spriteBatch, chainEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (PacketOwnedMobAttackHitEffectDisplay hitEffect in _mobAttackHitEffects)
            {
                DrawMobAttackHitEffect(spriteBatch, hitEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        public string DescribeStatus()
        {
            if (_summonsByObjectId.Count == 0)
            {
                return "Packet-owned summoned pool empty.";
            }

            return $"Packet-owned summoned pool active, count={_summonsByObjectId.Count}, summons={string.Join("; ", _summonsByObjectId.Values.OrderBy(static value => value.OwnerCharacterId).ThenBy(static value => value.Summon.ObjectId).Select(DescribeSummon))}";
        }

        private static string DescribeSummon(PacketOwnedSummonState state)
        {
            ActiveSummon summon = state.Summon;
            string ownerName = string.IsNullOrWhiteSpace(state.OwnerName) ? $"owner:{state.OwnerCharacterId}" : state.OwnerName;
            string teslaText = state.TeslaCoilState > 0 ? $"/tesla:{state.TeslaCoilState}" : string.Empty;
            string avatarText = state.AvatarLook != null ? "/avatar" : string.Empty;
            return $"{ownerName}#{state.OwnerCharacterId}/summon:{summon.ObjectId}/skill:{summon.SkillId}@({summon.PositionX:0},{summon.PositionY:0}){teslaText}{avatarText}";
        }

        private bool TryHandleCreated(int ownerCharacterId, ref PacketReader reader, int currentTime, out string message)
        {
            LoginAvatarLook avatarLook = null;
            byte teslaCoilState = 0;
            IReadOnlyList<Point> teslaTrianglePoints = Array.Empty<Point>();

            var packet = new SummonedCreatePacket(
                ownerCharacterId,
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadByte(),
                new Vector2(reader.ReadInt16(), reader.ReadInt16()),
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                avatarLook,
                teslaCoilState,
                teslaTrianglePoints);

            if (reader.CanRead(1))
            {
                bool hasAvatarLook = reader.ReadByte() != 0;
                if (hasAvatarLook && !TryReadAvatarLook(ref reader, out avatarLook, out message))
                {
                    return false;
                }

                if (packet.SkillId == TeslaCoilSkillId && reader.CanRead(1))
                {
                    teslaCoilState = reader.ReadByte();
                    if (teslaCoilState == 1)
                    {
                        Point[] points = new Point[3];
                        for (int i = 0; i < points.Length; i++)
                        {
                            if (!reader.CanRead(sizeof(short) * 2))
                            {
                                message = "Summoned Tesla Coil packet ended before the triangle point list completed.";
                                return false;
                            }

                            points[i] = new Point(reader.ReadInt16(), reader.ReadInt16());
                        }

                        teslaTrianglePoints = points;
                    }
                }
            }

            packet = packet with
            {
                AvatarLook = avatarLook,
                TeslaCoilState = teslaCoilState,
                TeslaTrianglePoints = teslaTrianglePoints
            };

            return TryCreate(packet, currentTime, out message);
        }

        private bool TryHandleRemoved(int ownerCharacterId, ref PacketReader reader, int currentTime, out string message)
        {
            var packet = new SummonedRemovePacket(ownerCharacterId, reader.ReadInt32(), reader.CanRead(1) ? reader.ReadByte() : (byte)0);
            return TryRemove(packet, currentTime, out message);
        }

        private bool TryHandleMove(int ownerCharacterId, ref PacketReader reader, int currentTime, out string message)
        {
            message = null;
            int summonObjectId = reader.ReadInt32();
            if (!TryDecodeMoveSnapshot(ref reader, currentTime, out PlayerMovementSyncSnapshot snapshot, out byte moveAction))
            {
                message = $"Summoned move packet for {summonObjectId} could not be decoded.";
                return false;
            }

            return TryApplyMoveSnapshot(ownerCharacterId, summonObjectId, snapshot, moveAction, currentTime, out message);
        }

        private bool TryHandleAttack(int ownerCharacterId, ref PacketReader reader, int currentTime, out string message)
        {
            int summonObjectId = reader.ReadInt32();
            int characterLevel = reader.CanRead(1) ? reader.ReadByte() : 0;
            byte packedAction = reader.CanRead(1) ? reader.ReadByte() : (byte)0;
            byte mobCount = reader.CanRead(1) ? reader.ReadByte() : (byte)0;
            List<SummonedAttackTargetPacket> targets = new(mobCount);
            for (int i = 0; i < mobCount; i++)
            {
                int mobObjectId = reader.ReadInt32();
                byte hitAction = 0;
                int damage = 0;
                if (mobObjectId != 0)
                {
                    hitAction = reader.ReadByte();
                    damage = reader.ReadInt32();
                }

                targets.Add(new SummonedAttackTargetPacket(mobObjectId, hitAction, damage));
            }

            byte tailByte = reader.CanRead(1) ? reader.ReadByte() : (byte)0;
            var packet = new SummonedAttackPacket(
                ownerCharacterId,
                summonObjectId,
                characterLevel,
                (byte)(packedAction & 0x7F),
                (packedAction & 0x80) != 0,
                targets,
                tailByte);
            return TryMarkAttack(packet, currentTime, out message);
        }

        private bool TryHandleSkill(int ownerCharacterId, ref PacketReader reader, int currentTime, out string message)
        {
            int summonObjectId = reader.ReadInt32();
            byte attackAction = reader.CanRead(1) ? reader.ReadByte() : (byte)0;
            return TryMarkSkill(ownerCharacterId, summonObjectId, attackAction, currentTime, out message);
        }

        private bool TryHandleHit(int ownerCharacterId, ref PacketReader reader, int currentTime, out string message)
        {
            int summonObjectId = reader.ReadInt32();
            sbyte attackIndex = unchecked((sbyte)reader.ReadByte());
            int damage = reader.ReadInt32();
            int? mobTemplateId = null;
            bool? mobFacingLeft = null;
            if (attackIndex > -2 && reader.CanRead(sizeof(int) + sizeof(byte)))
            {
                mobTemplateId = reader.ReadInt32();
                mobFacingLeft = reader.ReadByte() != 0;
            }

            var packet = new SummonedHitPacket(ownerCharacterId, summonObjectId, attackIndex, damage, mobTemplateId, mobFacingLeft);
            return TryMarkHit(packet, currentTime, out message);
        }

        private bool TryDecodeMoveSnapshot(ref PacketReader reader, int currentTime, out PlayerMovementSyncSnapshot snapshot, out byte moveAction)
        {
            snapshot = null;
            moveAction = 0;

            if (!reader.CanRead(sizeof(short) * 4 + sizeof(byte)))
            {
                return false;
            }

            int startX = reader.ReadInt16();
            int startY = reader.ReadInt16();
            short startVelocityX = reader.ReadInt16();
            short startVelocityY = reader.ReadInt16();
            byte elementCount = reader.ReadByte();

            var elements = new List<MovePathElement>(elementCount);
            int currentX = startX;
            int currentY = startY;
            short currentVelocityX = startVelocityX;
            short currentVelocityY = startVelocityY;
            short currentFoothold = 0;
            int cursorTime = currentTime;

            for (int i = 0; i < elementCount; i++)
            {
                if (!reader.CanRead(1))
                {
                    return false;
                }

                byte attr = reader.ReadByte();
                int elementX = currentX;
                int elementY = currentY;
                short elementVelocityX = currentVelocityX;
                short elementVelocityY = currentVelocityY;
                short elementFoothold = currentFoothold;

                switch (attr)
                {
                    case 0:
                    case 5:
                    case 12:
                    case 14:
                    case 35:
                    case 36:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        elementFoothold = reader.ReadInt16();
                        if (attr == 12)
                        {
                            reader.ReadInt16();
                        }

                        reader.ReadInt16();
                        reader.ReadInt16();
                        break;
                    case 1:
                    case 2:
                    case 13:
                    case 16:
                    case 18:
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        elementFoothold = 0;
                        break;
                    case 3:
                    case 4:
                    case 6:
                    case 7:
                    case 8:
                    case 10:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementFoothold = reader.ReadInt16();
                        elementVelocityX = 0;
                        elementVelocityY = 0;
                        break;
                    case 9:
                        reader.ReadByte();
                        elementVelocityX = 0;
                        elementVelocityY = 0;
                        elementFoothold = 0;
                        break;
                    case 11:
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        reader.ReadInt16();
                        elementFoothold = 0;
                        break;
                    case 17:
                        elementX = reader.ReadInt16();
                        elementY = reader.ReadInt16();
                        elementVelocityX = reader.ReadInt16();
                        elementVelocityY = reader.ReadInt16();
                        break;
                    case 20:
                    case 21:
                    case 22:
                    case 23:
                    case 24:
                    case 25:
                    case 26:
                    case 27:
                    case 28:
                    case 29:
                    case 30:
                        break;
                    default:
                        break;
                }

                moveAction = reader.ReadByte();
                short elapsed = reader.ReadInt16();

                elements.Add(new MovePathElement
                {
                    X = elementX,
                    Y = elementY,
                    VelocityX = elementVelocityX,
                    VelocityY = elementVelocityY,
                    Action = MoveAction.Stand,
                    FootholdId = elementFoothold,
                    TimeStamp = cursorTime,
                    Duration = elapsed,
                    FacingRight = DecodeFacingRight(moveAction),
                    StatChanged = false
                });

                currentX = elementX;
                currentY = elementY;
                currentVelocityX = elementVelocityX;
                currentVelocityY = elementVelocityY;
                currentFoothold = elementFoothold;
                cursorTime += Math.Max(1, (int)elapsed);
            }

            var passive = new PassivePositionSnapshot
            {
                X = currentX,
                Y = currentY,
                VelocityX = currentVelocityX,
                VelocityY = currentVelocityY,
                Action = MoveAction.Stand,
                FootholdId = currentFoothold,
                TimeStamp = cursorTime,
                FacingRight = DecodeFacingRight(moveAction)
            };

            snapshot = new PlayerMovementSyncSnapshot(passive, elements);
            return true;
        }

        private void ApplyMovementSnapshot(PacketOwnedSummonState state, int currentTime)
        {
            PassivePositionSnapshot sampled = state.MovementSnapshot.SampleAtTime(currentTime);
            state.Summon.PreviousPositionX = state.Summon.PositionX;
            state.Summon.PreviousPositionY = state.Summon.PositionY;
            state.Summon.PositionX = sampled.X;
            state.Summon.PositionY = sampled.Y;
            state.Summon.AnchorX = sampled.X;
            state.Summon.AnchorY = sampled.Y;
            state.Summon.FacingRight = sampled.FacingRight;
            state.CurrentFootholdId = (short)sampled.FootholdId;
        }

        private bool TryApplyPassiveMovement(PacketOwnedSummonState state, int currentTime, bool ownerFacingRight)
        {
            ActiveSummon summon = state?.Summon;
            if (summon == null || summon.IsPendingRemoval)
            {
                return false;
            }

            Vector2? ownerPosition = TryResolveOwnerPosition(state.OwnerCharacterId, out Vector2 resolvedOwnerPosition)
                ? resolvedOwnerPosition
                : null;
            if (!ownerPosition.HasValue
                && !PacketOwnedSummonUpdateRules.ShouldUseAnchorBoundPassiveFallback(summon))
            {
                return false;
            }

            float deltaTimeSeconds = state.LastPassiveMovementUpdateTime == int.MinValue
                ? 0f
                : Math.Max(0f, currentTime - state.LastPassiveMovementUpdateTime) / 1000f;
            state.LastPassiveMovementUpdateTime = currentTime;

            summon.PreviousPositionX = summon.PositionX;
            summon.PreviousPositionY = summon.PositionY;

            Vector2 targetPosition = PacketOwnedSummonUpdateRules.ResolvePassiveTargetPosition(
                summon,
                ownerPosition,
                ownerFacingRight,
                currentTime);
            Vector2 nextPosition = PacketOwnedSummonUpdateRules.ResolvePassiveStepPosition(
                summon,
                targetPosition,
                deltaTimeSeconds);

            summon.PositionX = nextPosition.X;
            summon.PositionY = nextPosition.Y;

            SummonMovementStyle effectiveMovementStyle = PacketOwnedSummonUpdateRules.ResolveEffectiveMovementStyle(summon);
            if (effectiveMovementStyle == SummonMovementStyle.GroundFollow
                || effectiveMovementStyle == SummonMovementStyle.HoverFollow
                || effectiveMovementStyle == SummonMovementStyle.DriftAroundOwner)
            {
                summon.FacingRight = ownerFacingRight;
            }

            if (PacketOwnedSummonUpdateRules.ShouldEmitPassiveEffectFromMotion(summon)
                && currentTime - summon.LastPassiveEffectTime >= PacketOwnedSummonPassiveEffectCooldownMs)
            {
                float movedDistanceSq = Vector2.DistanceSquared(
                    new Vector2(summon.PreviousPositionX, summon.PreviousPositionY),
                    new Vector2(summon.PositionX, summon.PositionY));
                if (movedDistanceSq >= 36f)
                {
                    SkillAnimation passiveEffect = summon.SkillData?.Effect ?? summon.SkillData?.AffectedEffect;
                    if (passiveEffect?.Frames.Count > 0)
                    {
                        SpawnHitEffect(
                            summon.SkillId,
                            passiveEffect,
                            summon.PositionX,
                            summon.PositionY,
                            summon.FacingRight,
                            currentTime);
                        summon.LastPassiveEffectTime = currentTime;
                    }
                }
            }

            return true;
        }

        private bool TryResolveOwnerPosition(int ownerCharacterId, out Vector2 ownerPosition)
        {
            ownerPosition = Vector2.Zero;

            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            if (localPlayer?.Build?.Id == ownerCharacterId)
            {
                ownerPosition = localPlayer.Position;
                return true;
            }

            if (_remoteUserPool != null && _remoteUserPool.TryGetActor(ownerCharacterId, out RemoteUserActor actor))
            {
                ownerPosition = actor.Position;
                return true;
            }

            return false;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        internal static bool ShouldApplyHealingRobotSkillPacketFacing(ActiveSummon summon, byte normalizedAction)
        {
            return summon?.SkillId == HealingRobotSkillId
                   && SummonRuntimeRules.IsSitdownHealingSupportSummon(summon.SkillData)
                   && normalizedAction == HealingRobotHealSkillAction;
        }

        internal static bool ResolveHealingRobotSkillPacketFacingRight(byte rawSkillAction)
        {
            // CSummoned::TryDoingHealingRobot sends (moveAction << 7) | 13.
            return (rawSkillAction & 0x80) == 0;
        }

        private bool TryResolveBodyContactDamage(PacketOwnedSummonState state, int currentTime)
        {
            if (_mobPool?.ActiveMobs == null
                || state?.Summon == null
                || !PacketOwnedSummonUpdateRules.ShouldResolveBodyContact(
                    state.Summon,
                    ShouldRegisterSummonPuppet(state.Summon.SkillData),
                    currentTime,
                    PacketOwnedSummonBodyContactCooldownMs))
            {
                return false;
            }

            Rectangle summonHitbox = GetSummonContactBounds(state.Summon, currentTime);
            if (summonHitbox.IsEmpty)
            {
                return false;
            }

            foreach (MobItem mob in _mobPool.ActiveMobs)
            {
                if (mob == null)
                {
                    continue;
                }

                Rectangle mobHitbox = mob.GetBodyHitbox(currentTime);
                if (!PacketOwnedSummonUpdateRules.IsClientBodyAttackMobCandidate(
                        mob.AI?.IsDead == true,
                        state.Summon.ObjectId,
                        mobHitbox,
                        summonHitbox))
                {
                    continue;
                }

                state.Summon.LastBodyContactTime = currentTime;
                int damage = ResolveSummonBodyContactDamage(mob);
                ApplySummonBodyContactDirection(state.Summon, mob);
                ApplySummonDamage(state, damage, currentTime, useHitAnimationState: true);
                PlayPacketIncDecHpFeedback(state.Summon, damage, currentTime);
                return true;
            }

            return false;
        }

        private static void ApplySummonBodyContactDirection(ActiveSummon summon, MobItem mob)
        {
            if (summon == null)
            {
                return;
            }

            int relativeMotionX = SummonDamageRuntimeRules.ResolveBodyContactRelativeMotionX(
                mob?.MovementInfo?.X ?? 0f,
                mob?.MovementInfo?.VelocityX ?? 0f,
                summon.PositionX,
                summon.PreviousPositionX);
            summon.LastBodyContactRelativeMotionX = relativeMotionX;
            summon.LastBodyContactHitFacingRight = SummonDamageRuntimeRules.ResolveBodyContactHitFacingRight(relativeMotionX);
        }

        private static int ResolveSummonBodyContactDamage(MobItem mob)
        {
            int baseDamage = SummonDamageRuntimeRules.ResolveBodyContactBaseDamage(
                mob?.MobData?.PADamage ?? 0,
                mob?.AI?.GetCurrentAttack()?.Damage ?? 0,
                mob?.MobData?.MADamage ?? 0);
            int resolvedDamage = mob?.AI?.CalculateOutgoingDamage(baseDamage, MobDamageType.Physical) ?? baseDamage;
            return Math.Max(1, resolvedDamage);
        }

        private bool TryReadAvatarLook(ref PacketReader reader, out LoginAvatarLook avatarLook, out string message)
        {
            avatarLook = null;
            message = null;

            try
            {
                byte genderByte = reader.ReadByte();
                CharacterGender gender = Enum.IsDefined(typeof(CharacterGender), (int)genderByte)
                    ? (CharacterGender)genderByte
                    : CharacterGender.Male;
                byte skinByte = reader.ReadByte();
                SkinColor skin = Enum.IsDefined(typeof(SkinColor), (int)skinByte)
                    ? (SkinColor)skinByte
                    : SkinColor.Light;
                int faceId = reader.ReadInt32();
                reader.ReadByte();
                int hairId = reader.ReadInt32();
                Dictionary<byte, int> visibleEquipment = ReadAvatarLookEquipmentMap(ref reader);
                Dictionary<byte, int> hiddenEquipment = ReadAvatarLookEquipmentMap(ref reader);
                int weaponStickerItemId = reader.ReadInt32();
                int[] petIds = { reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32() };

                avatarLook = new LoginAvatarLook
                {
                    Gender = gender,
                    Skin = skin,
                    FaceId = faceId,
                    HairId = hairId,
                    VisibleEquipmentByBodyPart = visibleEquipment,
                    HiddenEquipmentByBodyPart = hiddenEquipment,
                    WeaponStickerItemId = weaponStickerItemId,
                    PetIds = petIds
                };

                return true;
            }
            catch (InvalidOperationException ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static Dictionary<byte, int> ReadAvatarLookEquipmentMap(ref PacketReader reader)
        {
            Dictionary<byte, int> equipment = new();
            while (true)
            {
                byte bodyPart = reader.ReadByte();
                if (bodyPart == byte.MaxValue)
                {
                    return equipment;
                }

                equipment[bodyPart] = reader.ReadInt32();
            }
        }

        private bool TryGetOwnedState(int ownerCharacterId, int summonObjectId, out PacketOwnedSummonState state, out string message)
        {
            message = null;
            if (!_summonsByObjectId.TryGetValue(summonObjectId, out state))
            {
                message = $"Summoned {summonObjectId} does not exist.";
                return false;
            }

            if (state.OwnerCharacterId != ownerCharacterId)
            {
                message = $"Summoned {summonObjectId} is owned by {state.OwnerCharacterId}, not {ownerCharacterId}.";
                state = null;
                return false;
            }

            return true;
        }

        private void RemoveExistingState(int summonObjectId)
        {
            if (_summonsByObjectId.TryGetValue(summonObjectId, out PacketOwnedSummonState existing))
            {
                RemoveState(existing);
            }
        }

        private List<PacketOwnedSummonState> GetOrCreateOwnerList(int ownerCharacterId)
        {
            if (!_summonsByOwnerId.TryGetValue(ownerCharacterId, out List<PacketOwnedSummonState> summons))
            {
                summons = new List<PacketOwnedSummonState>();
                _summonsByOwnerId[ownerCharacterId] = summons;
            }

            return summons;
        }

        private void BeginRemoval(PacketOwnedSummonState state, int currentTime, byte reason)
        {
            if (state?.Summon == null || state.Summon.IsPendingRemoval)
            {
                return;
            }

            CancelSummonExpiryTimer(state.Summon.ObjectId);
            state.RemovalReason = reason;
            state.Summon.ExpiryActionTriggered = true;
            ClearPacketOwnedOneTimeAction(state);
            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            state.Summon.RemovalAnimationStartTime = currentTime;
            state.Summon.ActorState = SummonActorState.Die;
            state.Summon.LastStateChangeTime = currentTime;
            state.Summon.PendingRemovalTime = currentTime + ResolveSummonRemovalPlaybackDurationMs(state.Summon);
            RemovePuppet(state.Summon);
        }

        private bool TryBeginNaturalExpiryRemoval(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon == null || !TryPrepareNaturalExpiryRemovalPlaybackForParity(state.Summon, currentTime))
            {
                return false;
            }

            CancelSummonExpiryTimer(state.Summon.ObjectId);
            state.RemovalReason = 0;
            ClearPacketOwnedOneTimeAction(state);
            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            RemovePuppet(state.Summon);
            return true;
        }

        internal static bool TryPrepareNaturalExpiryRemovalPlaybackForParity(ActiveSummon summon, int currentTime)
        {
            if (summon == null
                || summon.IsPendingRemoval
                || summon.ExpiryActionTriggered
                || !summon.HasReachedNaturalExpiry(currentTime))
            {
                return false;
            }

            summon.ExpiryActionTriggered = true;
            summon.LastAttackAnimationStartTime = currentTime;
            int actionDuration = ResolveSummonPendingRemovalActionDurationMs(summon);
            int removalDuration = ResolveSummonRemovalPlaybackDurationMs(summon);
            summon.RemovalAnimationStartTime = actionDuration > 0
                ? currentTime + actionDuration
                : currentTime;
            summon.PendingRemovalTime = summon.RemovalAnimationStartTime + removalDuration;

            if (actionDuration <= 0)
            {
                summon.ActorState = SummonActorState.Die;
                summon.LastStateChangeTime = currentTime;
            }

            return true;
        }

        internal static bool ShouldUseNaturalExpiryPlaybackOnClientCancel(ActiveSummon summon, int currentTime)
        {
            return summon != null
                   && !summon.IsPendingRemoval
                   && !summon.ExpiryActionTriggered
                   && summon.HasReachedNaturalExpiry(currentTime);
        }

        private static int ResolveSummonMaxHealth(SkillLevelData levelData)
        {
            return Math.Max(1, levelData?.HP ?? 1);
        }

        private void RefreshIdleActorState(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon == null || state.Summon.IsPendingRemoval)
            {
                return;
            }

            if (state.OneTimeAction != 0)
            {
                if (currentTime < state.OneTimeActionEndTime)
                {
                    return;
                }

                state.OneTimeAction = 0;
                state.OneTimeActionEndTime = int.MinValue;
                state.OneTimeActionClip = null;
                state.Summon.OneTimeActionFallbackAnimation = null;
                state.Summon.OneTimeActionFallbackStartTime = int.MinValue;
                state.Summon.OneTimeActionFallbackAnimationTime = int.MinValue;
                state.Summon.OneTimeActionFallbackEndTime = int.MinValue;
                ResolvePacketOwnedTeslaRuntimeStateAfterActionPlayback(state);
                ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            }

            if (IsSummonAnimationActive(state.Summon.SkillData?.SummonHitAnimation, state.Summon.LastHitAnimationStartTime, currentTime))
            {
                if (state.Summon.ActorState != SummonActorState.Hit)
                {
                    state.Summon.ActorState = SummonActorState.Hit;
                    state.Summon.LastStateChangeTime = currentTime;
                }

                return;
            }

            if (TryResolvePacketOwnedActiveAttackActorState(state.Summon, currentTime, out SummonActorState activeAttackState))
            {
                if (state.Summon.ActorState != activeAttackState)
                {
                    state.Summon.ActorState = activeAttackState;
                    state.Summon.LastStateChangeTime = currentTime;
                }

                return;
            }

            if (HasCompletedPacketOwnedAttackLayerRefresh(
                    state.Summon,
                    state.LastCompletedAttackLayerRefreshStartTime,
                    currentTime))
            {
                state.LastCompletedAttackLayerRefreshStartTime = state.Summon.LastAttackAnimationStartTime;
                ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            }

            SummonActorState idleState = PacketOwnedSummonUpdateRules.ResolveIdleActorState(
                state.Summon,
                currentTime,
                TeslaCoilSkillId);
            if (state.Summon.ActorState != idleState)
            {
                state.Summon.ActorState = idleState;
                state.Summon.LastStateChangeTime = currentTime;
            }

            if (idleState == SummonActorState.Idle)
            {
                if (ShouldClearPacketOwnedSupportSuspend(state, currentTime))
                {
                    state.Summon.SupportSuspendUntilTime = int.MinValue;
                }
            }

            if (idleState != SummonActorState.Prepare)
            {
                state.Summon.CurrentAnimationBranchName = null;
            }
        }

        private static void ResolvePacketOwnedTeslaRuntimeStateAfterActionPlayback(PacketOwnedSummonState state)
        {
            if (state?.Summon?.SkillId != TeslaCoilSkillId)
            {
                return;
            }

            byte resolvedState = PacketOwnedSummonUpdateRules.ResolvePacketOwnedTeslaRuntimeStateAfterOneTimeAction(
                state.Summon.TeslaCoilState,
                hasActiveOneTimeActionPlayback: false);
            state.Summon.TeslaCoilState = resolvedState;
            state.TeslaCoilState = resolvedState;
        }

        private static bool IsSummonAnimationActive(SkillAnimation animation, int animationStartTime, int currentTime, int initialDelay = 0)
        {
            if (animation?.Frames.Count <= 0 || animationStartTime == int.MinValue)
            {
                return false;
            }

            int elapsed = currentTime - animationStartTime;
            int duration = initialDelay + (GetSkillAnimationDuration(animation) ?? 0);
            return elapsed >= 0 && duration > 0 && elapsed < duration;
        }

        private static bool ShouldClearPacketOwnedSupportSuspend(PacketOwnedSummonState state, int currentTime)
        {
            return SummonRuntimeRules.ShouldClearSupportSuspend(state?.Summon, currentTime);
        }

        private static void AdvanceSummonHitPeriod(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return;
            }

            if (summon.LastHitPeriodUpdateTime == int.MinValue)
            {
                summon.LastHitPeriodUpdateTime = currentTime;
                return;
            }

            int elapsed = Math.Max(0, currentTime - summon.LastHitPeriodUpdateTime);
            summon.LastHitPeriodUpdateTime = currentTime;
            if (elapsed <= 0 || summon.HitPeriodRemainingMs == 0)
            {
                return;
            }

            if (summon.HitPeriodRemainingMs > 0)
            {
                summon.HitPeriodRemainingMs = Math.Max(0, summon.HitPeriodRemainingMs - elapsed);
            }
            else
            {
                summon.HitPeriodRemainingMs = Math.Min(0, summon.HitPeriodRemainingMs + elapsed);
            }

            summon.HitFlashCounter += (uint)Math.Max(1, (elapsed + 29) / 30);
        }

        private static int ResolveSummonHitPeriodDurationMs(ActiveSummon summon)
        {
            return SummonHitPeriodDurationMs;
        }

        private static int ResolveSummonHitActionDurationMs(ActiveSummon summon)
        {
            int hitAnimationDuration = GetSkillAnimationDuration(summon?.SkillData?.SummonHitAnimation) ?? 0;
            return Math.Max(240, hitAnimationDuration);
        }

        private static bool ShouldDeferSummonRemovalPlayback(ActiveSummon summon, int currentTime)
        {
            return summon?.IsPendingRemoval == true
                && summon.RemovalAnimationStartTime != int.MinValue
                && currentTime < summon.RemovalAnimationStartTime;
        }

        private static int ResolveSummonPendingRemovalActionDurationMs(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
            {
                return 0;
            }

            int prepareDuration = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                summon.SkillData,
                summon.CurrentAnimationBranchName);
            SkillAnimation actionAnimation = ResolvePacketPendingRemovalActionAnimation(summon);
            int actionDuration = GetSkillAnimationDuration(actionAnimation) ?? 0;
            return actionDuration > 0 ? prepareDuration + actionDuration : 0;
        }

        private static SkillAnimation ResolvePacketPendingRemovalActionAnimation(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName)
                && skill.SummonNamedAnimations != null
                && skill.SummonNamedAnimations.TryGetValue(summon.CurrentAnimationBranchName, out SkillAnimation namedAnimation)
                && namedAnimation?.Frames.Count > 0)
            {
                return namedAnimation;
            }

            SkillAnimation retryAnimation = ResolveEmptyActionRetryAnimation(summon);
            if (retryAnimation?.Frames.Count > 0)
            {
                return retryAnimation;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName))
            {
                return null;
            }

            return skill.SummonAttackAnimation;
        }

        private static int ResolveSummonRemovalPlaybackDurationMs(ActiveSummon summon)
        {
            return Math.Max(
                1,
                GetSkillAnimationDuration(summon?.SkillData?.SummonRemovalAnimation)
                ?? GetSkillAnimationDuration(summon?.SkillData?.SummonHitAnimation)
                ?? GetSkillAnimationDuration(ResolvePacketPendingRemovalActionAnimation(summon))
                ?? summon?.SkillData?.HitEffect?.TotalDuration
                ?? 1);
        }

        private static int ResolveOneTimeActionFallbackDurationMs(SkillAnimation animation, int animationTime)
        {
            if (animation?.Frames.Count <= 0)
            {
                return 240;
            }

            int totalDuration = GetSkillAnimationDuration(animation) ?? 0;
            if (totalDuration <= 0)
            {
                return 240;
            }

            int remainingDuration = Math.Max(0, totalDuration - Math.Max(0, animationTime));
            return Math.Max(240, remainingDuration);
        }

        private int ClearPacketOwnedMobAttackHitEffects(int summonObjectId)
        {
            return PacketOwnedSummonUpdateRules.ClearAttachedHitEffects(
                _mobAttackHitEffects,
                summonObjectId,
                static effect => effect?.AttachedSummonObjectId ?? 0);
        }

        private bool PreparePacketOwnedHitAction(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon == null)
            {
                return false;
            }

            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            bool hasHitAnimation = state.Summon.SkillData?.SummonHitAnimation?.Frames.Count > 0;
            state.OneTimeActionClip = null;
            state.Summon.OneTimeActionFallbackAnimation = null;
            state.Summon.OneTimeActionFallbackStartTime = int.MinValue;
            state.Summon.OneTimeActionFallbackAnimationTime = int.MinValue;
            state.Summon.OneTimeActionFallbackEndTime = int.MinValue;
            if (!hasHitAnimation)
            {
                int elapsed = Math.Max(0, currentTime - state.Summon.StartTime);
                SkillAnimation fallbackAnimation = ResolvePreHitSummonAnimation(state.Summon, currentTime, elapsed, out int fallbackAnimationTime);
                state.OneTimeActionClip = CreatePacketOwnedOneTimeActionClip(fallbackAnimation, fallbackAnimationTime, currentTime);
            }

            state.OneTimeAction = 15;
            state.OneTimeActionEndTime = hasHitAnimation
                ? currentTime + ResolveSummonHitActionDurationMs(state.Summon)
                : state.OneTimeActionClip?.EndTime ?? currentTime + 240;
            if (hasHitAnimation)
            {
                state.Summon.ActorState = SummonActorState.Hit;
                state.Summon.LastStateChangeTime = currentTime;
            }

            return hasHitAnimation;
        }

        private static SkillAnimation ResolvePreHitSummonAnimation(ActiveSummon summon, int currentTime, int elapsedTime, out int animationTime)
        {
            animationTime = Math.Max(0, elapsedTime);
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            SkillAnimation spawnAnimation = skill.SummonSpawnAnimation;
            if (spawnAnimation?.Frames.Count > 0)
            {
                int spawnDuration = GetSkillAnimationDuration(spawnAnimation) ?? 0;
                if (spawnDuration > 0 && elapsedTime < spawnDuration)
                {
                    animationTime = elapsedTime;
                    return spawnAnimation;
                }

                animationTime = Math.Max(0, elapsedTime - spawnDuration);
            }

            SkillAnimation removalAnimation = skill.SummonRemovalAnimation;
            if (removalAnimation?.Frames.Count > 0 && summon.RemovalAnimationStartTime != int.MinValue)
            {
                int removalElapsed = currentTime - summon.RemovalAnimationStartTime;
                int removalDuration = GetSkillAnimationDuration(removalAnimation) ?? 0;
                if (removalElapsed >= 0 && removalDuration > 0 && removalElapsed < removalDuration)
                {
                    animationTime = removalElapsed;
                    return removalAnimation;
                }
            }

            if (TryResolveSummonAttackPlaybackAnimation(summon, currentTime, skill, out SkillAnimation attackPlaybackAnimation, out int attackPlaybackTime))
            {
                animationTime = attackPlaybackTime;
                return attackPlaybackAnimation;
            }

            SkillAnimation prepareAnimation = skill.SummonAttackPrepareAnimation;
            if (ShouldUseSummonPrepareAnimation(summon, skill)
                && prepareAnimation?.Frames.Count > 0)
            {
                int prepareElapsed = Math.Max(0, currentTime - summon.LastStateChangeTime);
                int prepareDuration = GetSkillAnimationDuration(prepareAnimation) ?? 0;
                if (prepareDuration <= 0 || prepareElapsed < prepareDuration)
                {
                    animationTime = prepareElapsed;
                    return prepareAnimation;
                }
            }

            return skill.SummonAnimation?.Frames.Count > 0 ? skill.SummonAnimation : skill.Effect;
        }

        private static Color ResolveSummonDrawColor(ActiveSummon summon)
        {
            if (summon?.HitPeriodRemainingMs == 0)
            {
                return Color.White;
            }

            return (summon.HitFlashCounter & 3u) < 2u
                ? new Color(128, 128, 128)
                : Color.White;
        }

        private void ApplySummonDamage(PacketOwnedSummonState state, int damage, int currentTime, bool useHitAnimationState)
        {
            if (state?.Summon == null || state.Summon.IsPendingRemoval)
            {
                return;
            }

            StartSummonHitReaction(state.Summon, damage, currentTime, useHitAnimationState);
            state.Summon.MaxHealth = Math.Max(1, state.Summon.MaxHealth);
            state.Summon.CurrentHealth = SummonDamageRuntimeRules.ResolveRemainingHealth(
                state.Summon.CurrentHealth,
                state.Summon.MaxHealth,
                damage);
            if (state.Summon.CurrentHealth <= 0)
            {
                if (TryBeginSelfDestructRemoval(state, currentTime, requiresNaturalExpiry: false))
                {
                    return;
                }

                BeginRemoval(state, currentTime, state.RemovalReason);
            }
        }

        private void StartSummonHitReaction(ActiveSummon summon, int hitDamage, int currentTime, bool useHitAnimationState)
        {
            if (summon == null)
            {
                return;
            }

            if (useHitAnimationState)
            {
                summon.LastHitAnimationStartTime = currentTime;
                summon.ActorState = SummonActorState.Hit;
                summon.LastStateChangeTime = currentTime;
            }

            summon.HitPeriodRemainingMs = hitDamage > 0
                ? ResolveSummonHitPeriodDurationMs(summon)
                : -ResolveSummonHitPeriodDurationMs(summon);
            summon.LastHitPeriodUpdateTime = currentTime;

            if (hitDamage > 0 && summon.SkillData?.HitEffect != null)
            {
                SpawnHitEffect(
                    summon.SkillId,
                    summon.SkillData.HitEffect,
                    summon.PositionX,
                    summon.PositionY - 20f,
                    summon.LastBodyContactHitFacingRight ?? summon.FacingRight,
                    currentTime);
            }
        }

        private void RemoveState(PacketOwnedSummonState state)
        {
            if (state == null)
            {
                return;
            }

            CancelSummonExpiryTimer(state.Summon.ObjectId);
            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            UnregisterOwnerSummon(state);
            RemovePuppet(state.Summon);
            _summonsByObjectId.Remove(state.Summon.ObjectId);

            if (_summonsByOwnerId.TryGetValue(state.OwnerCharacterId, out List<PacketOwnedSummonState> summons))
            {
                summons.Remove(state);
                if (summons.Count == 0)
                {
                    _summonsByOwnerId.Remove(state.OwnerCharacterId);
                }
            }
        }

        private void ResolveOwnerState(int ownerCharacterId, out string ownerName, out bool ownerIsLocal, out bool ownerFacingRight)
        {
            ownerName = $"Character {ownerCharacterId}";
            ownerIsLocal = false;
            ownerFacingRight = true;

            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            if (localPlayer?.Build?.Id == ownerCharacterId)
            {
                ownerName = string.IsNullOrWhiteSpace(localPlayer.Build.Name) ? ownerName : localPlayer.Build.Name;
                ownerIsLocal = true;
                ownerFacingRight = localPlayer.FacingRight;
                return;
            }

            if (_remoteUserPool != null && _remoteUserPool.TryGetActor(ownerCharacterId, out RemoteUserActor actor))
            {
                ownerName = string.IsNullOrWhiteSpace(actor.Name) ? ownerName : actor.Name;
                ownerFacingRight = actor.FacingRight;
            }
        }

        private IReadOnlyList<ActiveSummon> GetRegisteredOwnerSummons(int ownerCharacterId)
        {
            if (ownerCharacterId <= 0)
            {
                return Array.Empty<ActiveSummon>();
            }

            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            if (localPlayer?.Build?.Id == ownerCharacterId)
            {
                return localPlayer.PacketOwnedSummons.Summons;
            }

            if (_remoteUserPool != null && _remoteUserPool.TryGetActor(ownerCharacterId, out RemoteUserActor actor))
            {
                return actor.PacketOwnedSummons.Summons;
            }

            return _summonsByOwnerId.TryGetValue(ownerCharacterId, out List<PacketOwnedSummonState> summons)
                ? summons.Select(static state => state.Summon).ToArray()
                : Array.Empty<ActiveSummon>();
        }

        private IEnumerable<PacketOwnedSummonState> EnumerateOwnerSummonStates(int ownerCharacterId)
        {
            IReadOnlyList<ActiveSummon> registeredSummons = GetRegisteredOwnerSummons(ownerCharacterId);
            if (registeredSummons.Count > 0)
            {
                foreach (ActiveSummon summon in registeredSummons)
                {
                    if (summon != null && _summonsByObjectId.TryGetValue(summon.ObjectId, out PacketOwnedSummonState state))
                    {
                        yield return state;
                    }
                }

                yield break;
            }

            if (!_summonsByOwnerId.TryGetValue(ownerCharacterId, out List<PacketOwnedSummonState> summons))
            {
                yield break;
            }

            foreach (PacketOwnedSummonState state in summons)
            {
                if (state != null)
                {
                    yield return state;
                }
            }
        }

        private void SyncOwnerSummonRegistration(PacketOwnedSummonState state)
        {
            if (state?.Summon == null || state.Summon.IsPendingRemoval)
            {
                return;
            }

            RegisterOwnerSummon(state);
        }

        private void RegisterOwnerSummon(PacketOwnedSummonState state)
        {
            if (state?.Summon == null)
            {
                return;
            }

            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            if (localPlayer?.Build?.Id == state.OwnerCharacterId)
            {
                localPlayer.PacketOwnedSummons.AddOrReplace(state.Summon);
                return;
            }

            if (_remoteUserPool != null && _remoteUserPool.TryGetActor(state.OwnerCharacterId, out RemoteUserActor actor))
            {
                actor.PacketOwnedSummons.AddOrReplace(state.Summon);
            }
        }

        private void UnregisterOwnerSummon(PacketOwnedSummonState state)
        {
            if (state?.Summon == null)
            {
                return;
            }

            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            if (localPlayer?.Build?.Id == state.OwnerCharacterId)
            {
                localPlayer.PacketOwnedSummons.Remove(state.Summon.ObjectId);
                return;
            }

            if (_remoteUserPool != null && _remoteUserPool.TryGetActor(state.OwnerCharacterId, out RemoteUserActor actor))
            {
                actor.PacketOwnedSummons.Remove(state.Summon.ObjectId);
            }
        }

        private void SyncPuppet(PacketOwnedSummonState state, int currentTime)
        {
            if (_mobPool == null || state?.Summon == null)
            {
                return;
            }

            if (state.Summon.IsPendingRemoval || !ShouldRegisterSummonPuppet(state.Summon.SkillData))
            {
                RemovePuppet(state.Summon);
                return;
            }

            Vector2 summonPosition = new(state.Summon.PositionX, state.Summon.PositionY);
            int expirationTime = state.Summon.Duration > 0 ? state.Summon.StartTime + state.Summon.Duration : 0;
            float aggroRange = ResolvePuppetAggroRange(state);

            _mobPool.RegisterPuppet(new PuppetInfo
            {
                ObjectId = state.Summon.ObjectId,
                SummonSlotIndex = ResolveSummonSlotIndex(state),
                X = summonPosition.X,
                Y = summonPosition.Y,
                Hitbox = GetSummonHitbox(state.Summon, currentTime),
                IsGrounded = state.CurrentFootholdId != 0,
                OwnerId = state.OwnerCharacterId,
                AggroValue = 1,
                AggroRange = aggroRange,
                ExpirationTime = expirationTime,
                IsActive = true
            });
        }

        private void ApplyCreatedOwnerSideEffects(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon == null || !state.OwnerIsLocal)
            {
                return;
            }

            if (ShouldRegisterSummonPuppet(state.Summon.SkillData))
            {
                SyncPuppet(state, currentTime);
                ApplyLocalOwnerPuppetAggro(state);
                _mobPool?.SyncPuppetTargets(currentTime);
            }

            TryRefreshLocalTeslaCoilDurations(state, currentTime);
        }

        private void TryRefreshLocalTeslaCoilDurations(PacketOwnedSummonState createdState, int currentTime)
        {
            if (createdState?.Summon == null
                || createdState.Summon.SkillId != TeslaCoilSkillId)
            {
                return;
            }

            List<PacketOwnedSummonState> teslaCoils = EnumerateOwnerSummonStates(createdState.OwnerCharacterId)
                .Where(static candidate => candidate?.Summon?.SkillId == TeslaCoilSkillId && !candidate.Summon.IsPendingRemoval)
                .OrderBy(static candidate => candidate.Summon.StartTime)
                .ThenBy(static candidate => candidate.Summon.ObjectId)
                .ToList();
            if (teslaCoils.Count != 3)
            {
                return;
            }

            SkillData teslaSkill = createdState.Summon.SkillData ?? _skillLoader?.LoadSkill(TeslaCoilSkillId);
            SkillLevelData teslaLevelData = teslaSkill?.GetLevel(Math.Max(1, createdState.SkillLevel));
            int masteryLevel = Math.Max(0, _localSkillLevelAccessor?.Invoke(TeslaCoilMasterySkillId) ?? 0);
            SkillLevelData masteryLevelData = masteryLevel > 0
                ? _skillLoader?.LoadSkill(TeslaCoilMasterySkillId)?.GetLevel(masteryLevel)
                : null;

            int baseDurationMs = Math.Max(0, (teslaLevelData?.Y ?? 0) * 1000);
            int masteryBonusMs = Math.Max(0, ((masteryLevelData?.Y ?? 0) * baseDurationMs) / 100);
            int refreshedDurationMs = baseDurationMs + masteryBonusMs;
            if (refreshedDurationMs <= 0)
            {
                return;
            }

            foreach (PacketOwnedSummonState teslaCoil in teslaCoils)
            {
                teslaCoil.Summon.StartTime = currentTime;
                teslaCoil.Summon.Duration = refreshedDurationMs;
                byte teslaCoilState = teslaCoil.TeslaCoilState;
                Point[] teslaTrianglePoints = teslaCoil.TeslaTrianglePoints;
                PacketOwnedSummonUpdateRules.RearmPacketOwnedTeslaCoilForRefresh(
                    teslaCoil.Summon,
                    ref teslaCoilState,
                    ref teslaTrianglePoints,
                    TeslaCoilSkillId);
                teslaCoil.TeslaCoilState = teslaCoilState;
                teslaCoil.TeslaTrianglePoints = teslaTrianglePoints;
                RegisterSummonExpiryTimer(teslaCoil.Summon);
            }
        }

        private void RemovePuppet(ActiveSummon summon)
        {
            if (_mobPool == null || summon == null)
            {
                return;
            }

            _mobPool.RemovePuppet(summon.ObjectId);
        }

        private void ApplyLocalOwnerPuppetAggro(PacketOwnedSummonState state)
        {
            if (_mobPool == null
                || state?.Summon == null
                || !state.OwnerIsLocal
                || !ShouldRegisterSummonPuppet(state.Summon.SkillData))
            {
                return;
            }

            float aggroRange = ResolvePuppetAggroRange(state);
            if (aggroRange <= 0f)
            {
                return;
            }

            _mobPool.LetMobChasePuppet(state.Summon.PositionX, state.Summon.PositionY, aggroRange, state.Summon.ObjectId);
        }

        private float ResolvePuppetAggroRange(PacketOwnedSummonState state)
        {
            if (state?.Summon == null)
            {
                return 0f;
            }

            float ownerX = state.Summon.PositionX;
            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            if (localPlayer?.Build?.Id == state.OwnerCharacterId)
            {
                ownerX = localPlayer.X;
            }
            else if (_remoteUserPool != null && _remoteUserPool.TryGetActor(state.OwnerCharacterId, out RemoteUserActor actor))
            {
                ownerX = actor.Position.X;
            }

            return Math.Max(220f, Math.Abs(state.Summon.PositionX - ownerX) + 170f);
        }

        private int ResolveSummonSlotIndex(PacketOwnedSummonState state)
        {
            int slotIndex = 0;
            foreach (ActiveSummon candidate in GetRegisteredOwnerSummons(state.OwnerCharacterId))
            {
                if (candidate == null || candidate.IsPendingRemoval)
                {
                    continue;
                }

                if (ReferenceEquals(candidate, state.Summon))
                {
                    return slotIndex;
                }

                slotIndex++;
            }

            return -1;
        }

        private void RegisterSummonExpiryTimer(ActiveSummon summon)
        {
            if (summon?.ObjectId <= 0 || summon.Duration <= 0)
            {
                return;
            }

            CancelSummonExpiryTimer(summon.ObjectId);
            _summonExpiryTimers.Add(new PacketOwnedSummonTimer
            {
                SummonedObjectId = summon.ObjectId,
                SkillId = summon.SkillId,
                ExpireTime = summon.StartTime + summon.Duration
            });
        }

        private void CancelSummonExpiryTimer(int summonedObjectId)
        {
            if (summonedObjectId <= 0)
            {
                return;
            }

            for (int i = _summonExpiryTimers.Count - 1; i >= 0; i--)
            {
                if (_summonExpiryTimers[i].SummonedObjectId == summonedObjectId)
                {
                    _summonExpiryTimers.RemoveAt(i);
                }
            }
        }

        private void UpdateSummonExpiryTimers(int currentTime)
        {
            if (_summonExpiryTimers.Count == 0)
            {
                return;
            }

            List<PacketOwnedSummonTimer> expiredTimers = null;
            for (int i = _summonExpiryTimers.Count - 1; i >= 0; i--)
            {
                PacketOwnedSummonTimer timer = _summonExpiryTimers[i];
                if (timer.ExpireTime > currentTime)
                {
                    continue;
                }

                expiredTimers ??= new List<PacketOwnedSummonTimer>();
                expiredTimers.Add(timer);
                _summonExpiryTimers.RemoveAt(i);
            }

            if (expiredTimers == null)
            {
                return;
            }

            PacketOwnedSummonTimer[] orderedTimers = expiredTimers
                .OrderBy(static timer => timer.ExpireTime)
                .ThenBy(static timer => timer.SkillId)
                .ThenBy(static timer => timer.SummonedObjectId)
                .ToArray();

            PacketOwnedSummonTimerExpiration[] expirations = orderedTimers
                .Select(timer => CreateSummonExpiryTimerExpiration(timer, currentTime))
                .ToArray();

            OnSummonExpiryTimersExpiredBatch?.Invoke(expirations);

            for (int i = 0; i < orderedTimers.Length; i++)
            {
                PacketOwnedSummonTimer timer = orderedTimers[i];
                OnSummonExpiryTimerExpired?.Invoke(expirations[i]);

                if (!_summonsByObjectId.TryGetValue(timer.SummonedObjectId, out PacketOwnedSummonState state)
                    || state.Summon == null
                    || state.Summon.IsPendingRemoval
                    || state.Summon.ExpiryActionTriggered)
                {
                    continue;
                }

                if (TryBeginSelfDestructRemoval(state, currentTime, requiresNaturalExpiry: true))
                {
                    continue;
                }

                if (TryBeginNaturalExpiryRemoval(state, currentTime))
                {
                    continue;
                }

                if (!TryTriggerExpiredSelfDestructAction(state, currentTime))
                {
                    BeginRemoval(state, currentTime, reason: 0);
                }
            }
        }

        private sealed class PacketOwnedSummonTileEffectDisplay
        {
            public SkillAnimation Animation { get; init; }
            public string AnimationPath { get; init; }
            public Rectangle Area { get; init; }
            public int EffectDistance { get; init; }
            public int StartTime { get; init; }
            public int EndTime { get; init; }
            public byte StartAlpha { get; init; }
            public byte EndAlpha { get; init; }

            public bool IsActive(int currentTime) => currentTime >= StartTime && currentTime < EndTime;
            public bool IsExpired(int currentTime) => currentTime >= EndTime;

            public float GetAlpha(int currentTime)
            {
                if (EndTime <= StartTime)
                {
                    return EndAlpha / 255f;
                }

                float progress = MathHelper.Clamp((currentTime - StartTime) / (float)(EndTime - StartTime), 0f, 1f);
                int alpha = (int)Math.Round(StartAlpha + ((EndAlpha - StartAlpha) * progress));
                return MathHelper.Clamp(alpha / 255f, 0f, 1f);
            }
        }

        private PacketOwnedSummonTimerExpiration CreateSummonExpiryTimerExpiration(PacketOwnedSummonTimer timer, int currentTime)
        {
            if (timer == null)
            {
                return default;
            }

            if (_summonsByObjectId.TryGetValue(timer.SummonedObjectId, out PacketOwnedSummonState state))
            {
                return new PacketOwnedSummonTimerExpiration(
                    timer.SkillId,
                    timer.SummonedObjectId,
                    timer.ExpireTime,
                    currentTime,
                    state?.OwnerCharacterId ?? 0,
                    state?.OwnerIsLocal == true);
            }

            return new PacketOwnedSummonTimerExpiration(
                timer.SkillId,
                timer.SummonedObjectId,
                timer.ExpireTime,
                currentTime,
                0,
                false);
        }

        private bool TryTriggerExpiredSelfDestructAction(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon?.SkillData?.SelfDestructMinion != true)
            {
                return false;
            }

            state.Summon.ExpiryActionTriggered = true;
            ClearPacketOwnedOneTimeAction(state);
            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            state.Summon.LastAttackAnimationStartTime = currentTime;
            (string actionBranchName, int explicitActionCode) =
                ResolvePacketOwnedExplicitSelfDestructPlayback(state.Summon, requiresNaturalExpiry: true);
            state.Summon.CurrentAnimationBranchName = actionBranchName;
            if (explicitActionCode > 0)
            {
                ArmPacketOwnedOneTimeAction(state, currentTime, (byte)explicitActionCode, isSkillAction: true);
            }

            bool hasPrepareAnimation = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                state.Summon.SkillData,
                state.Summon.CurrentAnimationBranchName) > 0;
            state.Summon.ActorState = hasPrepareAnimation
                ? SummonActorState.Prepare
                : SummonActorState.Attack;
            state.Summon.LastStateChangeTime = currentTime;
            TryDispatchExpirySelfDestructSideEffects(state, currentTime);
            RemovePuppet(state.Summon);

            int actionDuration = ResolveSummonPendingRemovalActionDurationMs(state.Summon);
            int removalDuration = ResolveSummonRemovalPlaybackDurationMs(state.Summon);
            state.Summon.RemovalAnimationStartTime = actionDuration > 0
                ? currentTime + actionDuration
                : currentTime;
            state.Summon.PendingRemovalTime = state.Summon.RemovalAnimationStartTime + removalDuration;

            if (actionDuration <= 0)
            {
                state.Summon.ActorState = SummonActorState.Die;
                state.Summon.LastStateChangeTime = currentTime;
            }

            return true;
        }

        private static int ResolveSummonDurationMs(SkillData skill, SkillLevelData levelData, int skillLevel)
        {
            return SummonRuntimeRules.ResolveDurationMs(skill, levelData, skillLevel);
        }

        internal static int ResolvePacketOwnedCreateDurationMs(
            SkillData skill,
            SkillLevelData levelData,
            int skillLevel,
            int skillId,
            bool ownerIsLocal,
            int currentTime,
            Func<int, int, int> localCancelFamilyRemainingDurationAccessor,
            Func<int, int> localSkillLevelAccessor,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            int authoredDurationMs = SummonRuntimeRules.ResolveAuthoredDurationMs(skill, levelData, skillLevel);
            if (authoredDurationMs > 0)
            {
                return authoredDurationMs;
            }

            if (ownerIsLocal)
            {
                int inheritedDurationMs = Math.Max(0, localCancelFamilyRemainingDurationAccessor?.Invoke(skillId, currentTime) ?? 0);
                if (inheritedDurationMs > 0)
                {
                    return inheritedDurationMs;
                }

                int inheritedAuthoredDurationMs = ResolveConnectedFamilyAuthoredDurationMs(
                    skillId,
                    localSkillLevelAccessor,
                    getSkillData,
                    skillCatalog);
                if (inheritedAuthoredDurationMs > 0)
                {
                    return inheritedAuthoredDurationMs;
                }
            }

            return ResolveSummonDurationMs(skill, levelData, skillLevel);
        }

        private int ResolvePacketOwnedSkillLevel(SkillData skill, int skillId, int packetSkillLevel, bool ownerIsLocal)
        {
            return ResolvePacketOwnedSkillLevelCore(
                skill,
                skillId,
                packetSkillLevel,
                ownerIsLocal,
                _localSkillLevelAccessor,
                ResolveCancelSkillData,
                GetCancelSkillCatalog());
        }

        internal static int ResolvePacketOwnedSkillLevelCore(
            SkillData skill,
            int skillId,
            int packetSkillLevel,
            bool ownerIsLocal,
            Func<int, int> localSkillLevelAccessor,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            int resolvedSkillLevel = Math.Max(1, packetSkillLevel);
            if (skill?.GetLevel(resolvedSkillLevel) != null)
            {
                return resolvedSkillLevel;
            }

            if (!ownerIsLocal)
            {
                return resolvedSkillLevel;
            }

            foreach (int candidateSkillId in ClientSkillCancelResolver.ResolveConnectedCancelFamilySkillIds(skillId, getSkillData, skillCatalog))
            {
                int localSkillLevel = Math.Max(0, localSkillLevelAccessor?.Invoke(candidateSkillId) ?? 0);
                if (localSkillLevel <= 0)
                {
                    continue;
                }

                SkillData candidateSkill = candidateSkillId == skillId
                    ? skill
                    : getSkillData?.Invoke(candidateSkillId);
                if (skill == null
                    || skill.GetLevel(localSkillLevel) != null
                    || candidateSkill?.GetLevel(localSkillLevel) != null)
                {
                    return localSkillLevel;
                }
            }

            return resolvedSkillLevel;
        }

        private int ResolveLocalOwnerCancelFamilyDurationMs(int skillId, int currentTime)
        {
            if (skillId <= 0)
            {
                return 0;
            }

            PlayerCharacter localPlayer = _localPlayerAccessor?.Invoke();
            int localOwnerId = localPlayer?.Build?.Id ?? 0;
            IEnumerable<ActiveSummon> localPacketOwnedSummons = localOwnerId > 0
                ? EnumerateOwnerSummonStates(localOwnerId).Select(static state => state.Summon)
                : Array.Empty<ActiveSummon>();

            return ResolveInheritedLocalCancelFamilyDurationMs(
                skillId,
                currentTime,
                localPacketOwnedSummons,
                _localCancelFamilyRemainingDurationAccessor,
                ResolveCancelSkillData,
                GetCancelSkillCatalog());
        }

        internal static int ResolveConnectedFamilyAuthoredDurationMs(
            int skillId,
            Func<int, int> localSkillLevelAccessor,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            if (skillId <= 0)
            {
                return 0;
            }

            foreach (int candidateSkillId in ClientSkillCancelResolver.ResolveConnectedCancelFamilySkillIds(
                         skillId,
                         getSkillData,
                         skillCatalog))
            {
                int localSkillLevel = Math.Max(0, localSkillLevelAccessor?.Invoke(candidateSkillId) ?? 0);
                if (localSkillLevel <= 0)
                {
                    continue;
                }

                SkillData candidateSkill = getSkillData?.Invoke(candidateSkillId);
                if (candidateSkill == null)
                {
                    continue;
                }

                SkillLevelData candidateLevelData = candidateSkill.GetLevel(localSkillLevel);
                int authoredDurationMs = SummonRuntimeRules.ResolveAuthoredDurationMs(
                    candidateSkill,
                    candidateLevelData,
                    localSkillLevel);
                if (authoredDurationMs > 0)
                {
                    return authoredDurationMs;
                }
            }

            return 0;
        }

        internal static int ResolveInheritedLocalCancelFamilyDurationMs(
            int skillId,
            int currentTime,
            IEnumerable<ActiveSummon> localPacketOwnedSummons,
            Func<int, int, int> localCancelFamilyRemainingDurationAccessor,
            Func<int, SkillData> getSkillData,
            IReadOnlyCollection<SkillData> skillCatalog)
        {
            int remainingDurationMs = Math.Max(0, localCancelFamilyRemainingDurationAccessor?.Invoke(skillId, currentTime) ?? 0);
            if (skillId <= 0 || localPacketOwnedSummons == null)
            {
                return remainingDurationMs;
            }

            HashSet<int> cancelFamily = new(ClientSkillCancelResolver.ResolveConnectedCancelFamilySkillIds(
                skillId,
                getSkillData,
                skillCatalog));
            if (cancelFamily.Count == 0)
            {
                cancelFamily.Add(skillId);
            }

            foreach (int candidateSkillId in cancelFamily)
            {
                remainingDurationMs = Math.Max(
                    remainingDurationMs,
                    Math.Max(0, localCancelFamilyRemainingDurationAccessor?.Invoke(candidateSkillId, currentTime) ?? 0));
            }

            foreach (ActiveSummon summon in localPacketOwnedSummons)
            {
                if (summon?.SkillId <= 0
                    || summon.IsPendingRemoval
                    || !cancelFamily.Contains(summon.SkillId))
                {
                    continue;
                }

                remainingDurationMs = Math.Max(remainingDurationMs, summon.GetRemainingTime(currentTime));
            }

            return remainingDurationMs;
        }

        private bool DoesClientCancelMatchSkillId(int activeSkillId, int requestedSkillId)
        {
            return ClientSkillCancelResolver.DoesClientCancelMatchSkillId(
                activeSkillId,
                requestedSkillId,
                ResolveCancelSkillData,
                GetCancelSkillCatalog());
        }

        private SkillData ResolveCancelSkillData(int skillId)
        {
            return _skillLoader?.LoadSkill(skillId);
        }

        private IReadOnlyCollection<SkillData> GetCancelSkillCatalog()
        {
            _cancelSkillCatalog ??= _skillLoader?.LoadAllSkills();
            return _cancelSkillCatalog;
        }

        private static SummonAssistType ResolveSummonAssistType(SkillData skill)
        {
            return SummonRuntimeRules.ResolveAssistType(skill);
        }

        private static bool ShouldRegisterSummonPuppet(SkillData skill)
        {
            return SummonRuntimeRules.ShouldRegisterPuppet(skill);
        }

        private bool TryBeginSelfDestructRemoval(PacketOwnedSummonState state, int currentTime, bool requiresNaturalExpiry)
        {
            if (state?.Summon == null)
            {
                return false;
            }

            if (requiresNaturalExpiry
                && !PacketOwnedSummonUpdateRules.ShouldTriggerExpirySelfDestruct(state.Summon, currentTime))
            {
                return false;
            }

            if (!requiresNaturalExpiry
                && (state.Summon.SkillData?.SelfDestructMinion != true || state.Summon.ExpiryActionTriggered))
            {
                return false;
            }

            (string actionBranchName, int explicitActionCode) =
                ResolvePacketOwnedExplicitSelfDestructPlayback(state.Summon, requiresNaturalExpiry);
            int attackWindowMs = ResolveSelfDestructActionWindowMs(state.Summon, actionBranchName);
            if (attackWindowMs <= 0)
            {
                return false;
            }

            state.RemovalReason = 0;
            state.Summon.ExpiryActionTriggered = true;
            ClearPacketOwnedOneTimeAction(state);
            ClearPacketOwnedMobAttackHitEffects(state.Summon.ObjectId);
            state.Summon.LastAttackAnimationStartTime = currentTime;
            state.Summon.CurrentAnimationBranchName = actionBranchName;
            if (explicitActionCode > 0)
            {
                ArmPacketOwnedOneTimeAction(state, currentTime, (byte)explicitActionCode, isSkillAction: true);
            }

            bool hasPrepareAnimation = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                state.Summon.SkillData,
                actionBranchName) > 0;
            state.Summon.ActorState = hasPrepareAnimation
                ? SummonActorState.Prepare
                : SummonActorState.Attack;
            state.Summon.LastStateChangeTime = currentTime;
            if (requiresNaturalExpiry)
            {
                TryDispatchExpirySelfDestructSideEffects(state, currentTime);
            }

            int removalWindowMs = ResolveSelfDestructRemovalWindowMs(state.Summon);
            (state.Summon.RemovalAnimationStartTime, state.Summon.PendingRemovalTime) =
                PacketOwnedSummonUpdateRules.BuildSelfDestructRemovalSchedule(currentTime, attackWindowMs, removalWindowMs);
            RemovePuppet(state.Summon);
            return true;
        }

        private bool TryDispatchExpirySelfDestructSideEffects(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon?.SkillData == null)
            {
                return false;
            }

            if (state.Summon.AssistType == SummonAssistType.Support)
            {
                return TryDispatchExpirySelfDestructSupportEffects(state, currentTime);
            }

            List<MobItem> targets = ResolveExpirySelfDestructTargets(state, currentTime);
            if (targets.Count == 0)
            {
                return false;
            }

            Vector2 primaryTargetCenter = GetMobHitboxCenter(targets[0], currentTime);
            if (MathF.Abs(primaryTargetCenter.X - state.Summon.PositionX) > 0.5f)
            {
                state.Summon.FacingRight = primaryTargetCenter.X >= state.Summon.PositionX;
            }

            state.LastAttackTargets = targets
                .Select(static target => new SummonedAttackTargetPacket(target.PoolId, 0, 0))
                .ToArray();
            SpawnPacketAttackVisuals(state, currentTime);
            if (state.OwnerIsLocal)
            {
                TryRegisterClientOwnedAttackTileOverlay(state, currentTime);
            }

            state.Summon.LastAttackTime = currentTime;
            return true;
        }

        private bool TryDispatchExpirySelfDestructSupportEffects(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon?.SkillData == null || state.Summon.AssistType != SummonAssistType.Support)
            {
                return false;
            }

            ArmPacketOwnedSupportSuspend(state, currentTime);
            if (!TryScheduleExpirySelfDestructSupportTargetEffects(state, currentTime))
            {
                ScheduleExpirySelfDestructSupportHitEffect(state.Summon, currentTime);
            }

            state.Summon.LastAttackTime = currentTime;
            return true;
        }

        private bool TryScheduleExpirySelfDestructSupportTargetEffects(PacketOwnedSummonState state, int currentTime)
        {
            List<MobItem> targets = ResolveExpirySelfDestructTargets(state, currentTime);
            if (targets.Count == 0)
            {
                return false;
            }

            Vector2 primaryTargetCenter = GetMobHitboxCenter(targets[0], currentTime);
            if (MathF.Abs(primaryTargetCenter.X - state.Summon.PositionX) > 0.5f)
            {
                state.Summon.FacingRight = primaryTargetCenter.X >= state.Summon.PositionX;
            }

            state.LastAttackTargets = targets
                .Select(static target => new SummonedAttackTargetPacket(target.PoolId, 0, 0))
                .ToArray();
            SpawnPacketAttackVisuals(state, currentTime);
            return true;
        }

        private void ScheduleExpirySelfDestructSupportHitEffect(ActiveSummon summon, int currentTime)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return;
            }

            SkillAnimation impactAnimation = ResolvePacketAttackImpactAnimation(
                skill,
                0,
                summon.CurrentAnimationBranchName);
            if (impactAnimation == null)
            {
                return;
            }

            int executeTime = currentTime + ResolvePacketAttackImpactAuthoredDelayMs(
                skill,
                0,
                summon.CurrentAnimationBranchName);
            ActiveHitEffect hitEffect = new()
            {
                SkillId = summon.SkillId,
                X = summon.PositionX,
                Y = summon.PositionY - 20f,
                StartTime = executeTime,
                Animation = impactAnimation,
                FacingRight = summon.FacingRight
            };

            if (executeTime > currentTime)
            {
                _scheduledHitEffects.Add(new ScheduledPacketOwnedHitEffect
                {
                    SequenceId = _nextScheduledHitEffectSequenceId++,
                    ExecuteTime = executeTime,
                    HitEffect = hitEffect
                });
                return;
            }

            _hitEffects.Add(hitEffect);
        }

        private List<MobItem> ResolveExpirySelfDestructTargets(PacketOwnedSummonState state, int currentTime)
        {
            ActiveSummon summon = state?.Summon;
            if (_mobPool?.ActiveMobs == null || summon?.SkillData == null)
            {
                return new List<MobItem>();
            }

            int maxTargets = ResolvePacketOwnedExpiryMaxTargetCount(summon);
            Dictionary<int, MobItem> candidatesById = _mobPool.ActiveMobs
                .Where(static mob => IsMobEligibleForPacketOwnedTargeting(mob) && mob.PoolId > 0)
                .GroupBy(static mob => mob.PoolId)
                .ToDictionary(static group => group.Key, static group => group.First());

            float? ownerReferenceX = TryResolveOwnerPosition(state.OwnerCharacterId, out Vector2 ownerPosition)
                ? ownerPosition.X
                : null;
            int[] orderedTargetIds = ResolvePacketOwnedExpiryTargetOrder(
                summon,
                candidatesById.Values.Select(mob => new PacketOwnedExpiryTargetCandidate(
                    mob.PoolId,
                    GetMobHitbox(mob, currentTime))),
                maxTargets,
                ResolvePacketOwnedExpiryPreferredTargetMobIds(state),
                ownerReferenceX);

            return orderedTargetIds
                .Where(candidatesById.ContainsKey)
                .Select(targetId => candidatesById[targetId])
                .ToList();
        }

        private static int ResolvePacketOwnedExpiryMaxTargetCount(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
            {
                return 1;
            }

            int authoredMobCount = summon.SkillData.ResolveSummonMobCountOverride(summon.CurrentAnimationBranchName);
            if (authoredMobCount > 0)
            {
                return Math.Max(1, authoredMobCount);
            }

            return Math.Max(1, summon.LevelData?.MobCount ?? 1);
        }

        internal static int[] ResolvePacketOwnedExpiryTargetOrder(
            ActiveSummon summon,
            IEnumerable<PacketOwnedExpiryTargetCandidate> candidates,
            int maxTargets,
            IReadOnlyList<int> preferredTargetMobIds = null,
            float? ownerReferenceX = null)
        {
            if (summon?.SkillData == null || candidates == null || maxTargets <= 0)
            {
                return Array.Empty<int>();
            }

            List<PacketOwnedExpiryTargetCandidate> candidateList = candidates
                .Where(static candidate => candidate.MobObjectId > 0 && !candidate.Hitbox.IsEmpty)
                .GroupBy(static candidate => candidate.MobObjectId)
                .Select(static group => group.First())
                .ToList();
            Dictionary<int, PacketOwnedExpiryTargetCandidate> candidatesById = candidateList
                .ToDictionary(static candidate => candidate.MobObjectId);
            if (candidateList.Count == 0)
            {
                return Array.Empty<int>();
            }

            List<int> orderedTargetIds = new(maxTargets);
            if (preferredTargetMobIds != null)
            {
                foreach (int preferredTargetMobId in preferredTargetMobIds)
                {
                    if (preferredTargetMobId <= 0
                        || orderedTargetIds.Count >= maxTargets
                        || orderedTargetIds.Contains(preferredTargetMobId)
                        || !candidatesById.TryGetValue(preferredTargetMobId, out PacketOwnedExpiryTargetCandidate preferredCandidate)
                        || !IsCandidateInEitherPacketOwnedSummonAttackRange(summon, preferredCandidate))
                    {
                        continue;
                    }

                    orderedTargetIds.Add(preferredTargetMobId);
                }
            }

            if (orderedTargetIds.Count >= maxTargets)
            {
                return orderedTargetIds.ToArray();
            }

            bool fallbackFacingRight = ResolvePacketOwnedExpiryFallbackFacingRight(
                summon,
                candidateList.Where(candidate => !orderedTargetIds.Contains(candidate.MobObjectId)),
                ownerReferenceX);
            Rectangle summonBounds = GetPacketOwnedSummonAttackBounds(summon, fallbackFacingRight);
            IEnumerable<int> fallbackTargetIds = OrderPacketOwnedExpiryFallbackCandidates(
                    summon,
                    candidateList.Where(candidate => !orderedTargetIds.Contains(candidate.MobObjectId)
                                                     && IsMobHitboxInPacketOwnedSummonAttackRange(summon, summonBounds, candidate.Hitbox, fallbackFacingRight)),
                    fallbackFacingRight)
                .Select(static candidate => candidate.MobObjectId);

            foreach (int fallbackTargetId in fallbackTargetIds)
            {
                if (orderedTargetIds.Count >= maxTargets)
                {
                    break;
                }

                orderedTargetIds.Add(fallbackTargetId);
            }

            return orderedTargetIds.ToArray();
        }

        internal static IReadOnlyList<PacketOwnedExpiryTargetCandidate> OrderPacketOwnedExpiryFallbackCandidates(
            ActiveSummon summon,
            IEnumerable<PacketOwnedExpiryTargetCandidate> candidates,
            bool facingRight)
        {
            if (summon == null || candidates == null)
            {
                return Array.Empty<PacketOwnedExpiryTargetCandidate>();
            }

            Vector2 summonPosition = new(summon.PositionX, summon.PositionY);
            return candidates
                .Where(candidate => candidate.MobObjectId > 0
                                    && !candidate.Hitbox.IsEmpty)
                .Select(candidate =>
                {
                    float centerX = candidate.Hitbox.Left + (candidate.Hitbox.Width * 0.5f);
                    float centerY = candidate.Hitbox.Top + (candidate.Hitbox.Height * 0.5f);
                    float leadingEdgeDistance = facingRight
                        ? MathF.Max(0f, candidate.Hitbox.Left - summonPosition.X)
                        : MathF.Max(0f, summonPosition.X - candidate.Hitbox.Right);
                    float centerDistanceSq = ((centerX - summonPosition.X) * (centerX - summonPosition.X))
                                             + ((centerY - summonPosition.Y) * (centerY - summonPosition.Y));
                    float facingOrderX = facingRight ? centerX : -centerX;
                    return new
                    {
                        Candidate = candidate,
                        LeadingEdgeDistance = leadingEdgeDistance,
                        CenterDistanceSq = centerDistanceSq,
                        VerticalDistance = MathF.Abs(centerY - summonPosition.Y),
                        FacingOrderX = facingOrderX
                    };
                })
                .OrderBy(static entry => entry.LeadingEdgeDistance)
                .ThenBy(static entry => entry.CenterDistanceSq)
                .ThenBy(static entry => entry.VerticalDistance)
                .ThenBy(static entry => entry.FacingOrderX)
                .ThenBy(static entry => entry.Candidate.MobObjectId)
                .Select(static entry => entry.Candidate)
                .ToArray();
        }

        private static bool IsCandidateInEitherPacketOwnedSummonAttackRange(
            ActiveSummon summon,
            PacketOwnedExpiryTargetCandidate candidate)
        {
            return candidate.MobObjectId > 0
                   && !candidate.Hitbox.IsEmpty
                   && (IsMobHitboxInPacketOwnedSummonAttackRange(
                           summon,
                           GetPacketOwnedSummonAttackBounds(summon, facingRightOverride: true),
                           candidate.Hitbox,
                           facingRightOverride: true)
                       || IsMobHitboxInPacketOwnedSummonAttackRange(
                           summon,
                           GetPacketOwnedSummonAttackBounds(summon, facingRightOverride: false),
                           candidate.Hitbox,
                           facingRightOverride: false));
        }

        internal static bool ResolvePacketOwnedExpiryFallbackFacingRight(
            ActiveSummon summon,
            IEnumerable<PacketOwnedExpiryTargetCandidate> candidates,
            float? ownerReferenceX = null)
        {
            if (summon?.SkillData == null || candidates == null)
            {
                return summon?.FacingRight ?? true;
            }

            IReadOnlyList<PacketOwnedExpiryTargetCandidate> candidateList = candidates as IReadOnlyList<PacketOwnedExpiryTargetCandidate>
                ?? candidates.ToArray();
            if (TryResolvePacketOwnedExpiryProbeFacingRight(summon, candidateList, ownerReferenceX, out bool probeFacingRight))
            {
                return probeFacingRight;
            }

            PacketOwnedExpiryFacingScore rightScore = ScorePacketOwnedExpiryFacingCandidates(
                summon,
                candidateList,
                facingRight: true);
            PacketOwnedExpiryFacingScore leftScore = ScorePacketOwnedExpiryFacingCandidates(
                summon,
                candidateList,
                facingRight: false);
            if (rightScore.InRangeCount != leftScore.InRangeCount)
            {
                return rightScore.InRangeCount > leftScore.InRangeCount;
            }

            if (ownerReferenceX.HasValue
                && rightScore.InRangeCount > 0)
            {
                float ownerDeltaX = ownerReferenceX.Value - summon.PositionX;
                if (MathF.Abs(ownerDeltaX) > 0.5f)
                {
                    return ownerDeltaX >= 0f;
                }
            }

            int nearestDistanceComparison = rightScore.NearestDistanceSq.CompareTo(leftScore.NearestDistanceSq);
            if (nearestDistanceComparison != 0)
            {
                return nearestDistanceComparison < 0;
            }

            int nearestVerticalComparison = rightScore.NearestVerticalDistance.CompareTo(leftScore.NearestVerticalDistance);
            if (nearestVerticalComparison != 0)
            {
                return nearestVerticalComparison < 0;
            }

            return summon.FacingRight;
        }

        internal static bool TryResolvePacketOwnedExpiryProbeFacingRight(
            ActiveSummon summon,
            IReadOnlyList<PacketOwnedExpiryTargetCandidate> candidates,
            float? ownerReferenceX,
            out bool facingRight)
        {
            facingRight = summon?.FacingRight ?? true;
            if (summon?.SkillData == null || candidates == null || candidates.Count == 0)
            {
                return false;
            }

            bool hasRightHit = FindFirstPacketOwnedExpiryTargetInRange(
                summon,
                candidates,
                facingRight: true) > 0;
            bool hasLeftHit = FindFirstPacketOwnedExpiryTargetInRange(
                summon,
                candidates,
                facingRight: false) > 0;
            if (!hasRightHit && !hasLeftHit)
            {
                return false;
            }

            if (hasRightHit != hasLeftHit)
            {
                facingRight = hasRightHit;
                return true;
            }

            if (!ownerReferenceX.HasValue)
            {
                return false;
            }

            float ownerDeltaX = ownerReferenceX.Value - summon.PositionX;
            if (MathF.Abs(ownerDeltaX) <= 0.5f)
            {
                return false;
            }

            facingRight = ownerDeltaX >= 0f;
            return true;
        }

        private static int FindFirstPacketOwnedExpiryTargetInRange(
            ActiveSummon summon,
            IReadOnlyList<PacketOwnedExpiryTargetCandidate> candidates,
            bool facingRight)
        {
            if (summon?.SkillData == null || candidates == null || candidates.Count == 0)
            {
                return 0;
            }

            Rectangle summonBounds = GetPacketOwnedSummonAttackBounds(summon, facingRight);
            if (summonBounds.IsEmpty)
            {
                return 0;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                PacketOwnedExpiryTargetCandidate candidate = candidates[i];
                if (candidate.MobObjectId <= 0
                    || candidate.Hitbox.IsEmpty
                    || !IsMobHitboxInPacketOwnedSummonAttackRange(
                        summon,
                        summonBounds,
                        candidate.Hitbox,
                        facingRight))
                {
                    continue;
                }

                return candidate.MobObjectId;
            }

            return 0;
        }

        private static PacketOwnedExpiryFacingScore ScorePacketOwnedExpiryFacingCandidates(
            ActiveSummon summon,
            IEnumerable<PacketOwnedExpiryTargetCandidate> candidates,
            bool facingRight)
        {
            Rectangle summonBounds = GetPacketOwnedSummonAttackBounds(summon, facingRight);
            if (summonBounds.IsEmpty)
            {
                return PacketOwnedExpiryFacingScore.Empty;
            }

            Vector2 summonPosition = new(summon.PositionX, summon.PositionY);
            int inRangeCount = 0;
            float nearestDistanceSq = float.MaxValue;
            float nearestVerticalDistance = float.MaxValue;
            foreach (PacketOwnedExpiryTargetCandidate candidate in candidates)
            {
                if (!IsMobHitboxInPacketOwnedSummonAttackRange(summon, summonBounds, candidate.Hitbox, facingRight))
                {
                    continue;
                }

                inRangeCount++;
                float centerX = candidate.Hitbox.Left + (candidate.Hitbox.Width * 0.5f);
                float centerY = candidate.Hitbox.Top + (candidate.Hitbox.Height * 0.5f);
                float deltaX = centerX - summonPosition.X;
                float deltaY = centerY - summonPosition.Y;
                float distanceSq = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestVerticalDistance = MathF.Abs(deltaY);
                }
                else if (Math.Abs(distanceSq - nearestDistanceSq) < 0.5f)
                {
                    nearestVerticalDistance = MathF.Min(nearestVerticalDistance, MathF.Abs(deltaY));
                }
            }

            return inRangeCount > 0
                ? new PacketOwnedExpiryFacingScore(inRangeCount, nearestDistanceSq, nearestVerticalDistance)
                : PacketOwnedExpiryFacingScore.Empty;
        }

        private static bool IsMobEligibleForPacketOwnedTargeting(MobItem mob)
        {
            return mob != null
                   && mob.PoolId > 0
                   && IsMobEligibleForPacketOwnedFindHitMobInRect(
                       mob.AI?.IsDead == true,
                       mob.IsProtectedFromPlayerDamage,
                       mob.AI?.IsDazzled == true);
        }

        internal static bool IsMobEligibleForPacketOwnedFindHitMobInRect(
            bool isDead,
            bool isProtectedFromPlayerDamage,
            bool isDazzled)
        {
            return !isDead
                   && !isProtectedFromPlayerDamage
                   && !isDazzled;
        }

        private readonly record struct PacketOwnedExpiryFacingScore(
            int InRangeCount,
            float NearestDistanceSq,
            float NearestVerticalDistance)
        {
            public static PacketOwnedExpiryFacingScore Empty { get; } =
                new(0, float.MaxValue, float.MaxValue);
        }

        private static IReadOnlyList<int> ResolvePacketOwnedExpiryPreferredTargetMobIds(PacketOwnedSummonState state)
        {
            return state?.LastAttackTargets?
                .Where(static target => target.MobObjectId > 0)
                .Select(static target => target.MobObjectId)
                .Distinct()
                .ToArray()
                ?? Array.Empty<int>();
        }

        internal static Rectangle GetPacketOwnedSummonAttackBounds(ActiveSummon summon, bool? facingRightOverride = null)
        {
            if (summon?.SkillData == null)
            {
                return Rectangle.Empty;
            }

            Vector2 summonPosition = new(summon.PositionX, summon.PositionY);
            bool facingRight = facingRightOverride ?? summon.FacingRight;
            string attackBranchName = summon.CurrentAnimationBranchName;
            string selfDestructFinalBranch = summon.SkillData.SelfDestructMinion
                ? SummonRuntimeRules.ResolveSelfDestructFinalBranch(summon.SkillData, summon.AssistType)
                : null;
            Rectangle localHitbox = summon.AssistType == SummonAssistType.Support
                ? summon.SkillData.SelfDestructMinion
                  && !string.IsNullOrWhiteSpace(selfDestructFinalBranch)
                  && string.Equals(attackBranchName, selfDestructFinalBranch, StringComparison.OrdinalIgnoreCase)
                    ? SummonRuntimeRules.ResolveSupportOwnedExpiryRange(
                        summon.SkillData,
                        facingRight,
                        selfDestructFinalBranch)
                    : SummonRuntimeRules.ResolveSupportOwnedRange(
                        summon.SkillData,
                        facingRight,
                        attackBranchName)
                : summon.SkillData.TryGetSummonAttackRange(
                    facingRight,
                    attackBranchName,
                    out Rectangle branchRange)
                    ? branchRange
                    : summon.SkillData.GetSummonAttackRange(facingRight);
            if (!localHitbox.IsEmpty)
            {
                return new Rectangle(
                    (int)summonPosition.X + localHitbox.X,
                    (int)summonPosition.Y + localHitbox.Y,
                    localHitbox.Width,
                    localHitbox.Height);
            }

            int attackRadius = summon.SkillData.ResolveSummonAttackRadius(attackBranchName);
            if (attackRadius > 0)
            {
                Point centerOffset = summon.SkillData.GetSummonAttackCircleCenterOffset(
                    facingRight,
                    attackBranchName);
                int radius = (int)MathF.Ceiling(attackRadius);
                return new Rectangle(
                    (int)summonPosition.X + centerOffset.X - radius,
                    (int)summonPosition.Y + centerOffset.Y - radius,
                    Math.Max(1, radius * 2),
                    Math.Max(1, radius * 2));
            }

            return new Rectangle(
                (int)summonPosition.X - 90,
                (int)summonPosition.Y - 70,
                180,
                100);
        }

        private static bool IsMobInPacketOwnedSummonAttackRange(
            ActiveSummon summon,
            Rectangle summonBounds,
            MobItem mob,
            int currentTime,
            bool? facingRightOverride = null)
        {
            Rectangle mobHitbox = GetMobHitbox(mob, currentTime);
            if (mobHitbox.IsEmpty)
            {
                return false;
            }

            return IsMobHitboxInPacketOwnedSummonAttackRange(summon, summonBounds, mobHitbox, facingRightOverride);
        }

        private static bool IsMobHitboxInPacketOwnedSummonAttackRange(
            ActiveSummon summon,
            Rectangle summonBounds,
            Rectangle mobHitbox,
            bool? facingRightOverride = null)
        {
            if (summon?.SkillData == null || mobHitbox.IsEmpty)
            {
                return false;
            }

            string attackBranchName = summon.CurrentAnimationBranchName;
            int attackRadius = summon.SkillData.ResolveSummonAttackRadius(attackBranchName);
            if (attackRadius > 0)
            {
                bool facingRight = facingRightOverride ?? summon.FacingRight;
                Point centerOffset = summon.SkillData.GetSummonAttackCircleCenterOffset(
                    facingRight,
                    attackBranchName);
                Vector2 circleCenter = new(
                    summon.PositionX + centerOffset.X,
                    summon.PositionY + centerOffset.Y);
                return DoesRectangleIntersectCircle(mobHitbox, circleCenter, attackRadius);
            }

            return summonBounds.Intersects(mobHitbox);
        }

        private static bool DoesRectangleIntersectCircle(Rectangle rectangle, Vector2 circleCenter, float radius)
        {
            float closestX = Math.Clamp(circleCenter.X, rectangle.Left, rectangle.Right);
            float closestY = Math.Clamp(circleCenter.Y, rectangle.Top, rectangle.Bottom);
            float dx = circleCenter.X - closestX;
            float dy = circleCenter.Y - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private void SpawnPacketSummonProjectiles(ActiveSummon summon, IReadOnlyList<MobItem> targets, int currentTime)
        {
            IReadOnlyList<SkillAnimation> projectileAnimations = summon?.SkillData?.GetSummonProjectileAnimations(summon.CurrentAnimationBranchName);
            if (projectileAnimations == null
                || projectileAnimations.Count == 0
                || targets == null
                || targets.Count == 0)
            {
                return;
            }

            Vector2 source = new(summon.PositionX, summon.PositionY);
            Vector2 projectileSource = SummonImpactPresentationResolver.ResolveSourceAnchor(source);
            for (int i = 0; i < targets.Count; i++)
            {
                MobItem target = targets[i];
                if (target == null)
                {
                    continue;
                }

                Vector2 targetCenter = ResolvePacketAttackImpactPosition(
                    summon.SkillData,
                    i,
                    summon.CurrentAnimationBranchName,
                    target,
                    source,
                    currentTime);
                int impactDelayMs = Math.Max(60, ResolvePacketAttackImpactDelayMs(summon, target, currentTime, i));
                SpawnPacketProjectileVisual(
                    summon,
                    projectileAnimations,
                    projectileSource,
                    targetCenter,
                    currentTime,
                    impactDelayMs,
                    i);
            }
        }

        private void SpawnPacketTeslaAttackProjectiles(
            IReadOnlyList<PacketOwnedSummonState> teslaStates,
            IReadOnlyList<MobItem> targets,
            int currentTime,
            bool useTeslaPerTargetDelayJitter = false)
        {
            if (teslaStates == null || teslaStates.Count == 0 || targets == null || targets.Count == 0)
            {
                return;
            }

            Vector2[] assignedSources = ResolvePacketTeslaProjectileSources(teslaStates);
            for (int i = 0; i < teslaStates.Count; i++)
            {
                PacketOwnedSummonState teslaState = teslaStates[i];
                ActiveSummon teslaCoil = teslaState?.Summon;
                IReadOnlyList<SkillAnimation> projectileAnimations = teslaCoil?.SkillData?.GetSummonProjectileAnimations(teslaCoil.CurrentAnimationBranchName);
                if (projectileAnimations == null || projectileAnimations.Count == 0)
                {
                    continue;
                }

                Vector2 source = i < assignedSources.Length
                    ? assignedSources[i]
                    : new Vector2(teslaCoil.PositionX, teslaCoil.PositionY);
                MobItem target = targets
                    .OrderBy(candidate => Vector2.DistanceSquared(GetMobHitboxCenter(candidate, currentTime), source))
                    .FirstOrDefault();
                if (target == null)
                {
                    continue;
                }

                int targetIndex = -1;
                for (int targetCursor = 0; targetCursor < targets.Count; targetCursor++)
                {
                    if (ReferenceEquals(targets[targetCursor], target))
                    {
                        targetIndex = targetCursor;
                        break;
                    }
                }

                Vector2 targetCenter = ResolvePacketAttackImpactPosition(
                    teslaCoil.SkillData,
                    targetIndex >= 0 ? targetIndex : 0,
                    teslaCoil.CurrentAnimationBranchName,
                    target,
                    source,
                    currentTime);
                int impactDelayMs = Math.Max(
                    60,
                    useTeslaPerTargetDelayJitter
                        ? ResolvePacketTeslaTargetImpactDelayMs(teslaCoil, source, target, currentTime, targetIndex >= 0 ? targetIndex : 0)
                        : ResolvePacketAttackImpactDelayMs(teslaCoil, source, target, currentTime, targetIndex >= 0 ? targetIndex : 0));
                SpawnPacketProjectileVisual(
                    teslaCoil,
                    projectileAnimations,
                    SummonImpactPresentationResolver.ResolveSourceAnchor(source),
                    targetCenter,
                    currentTime,
                    impactDelayMs,
                    i);
            }
        }

        private void SpawnPacketProjectileVisual(
            ActiveSummon summon,
            IReadOnlyList<SkillAnimation> projectileAnimations,
            Vector2 source,
            Vector2 target,
            int currentTime,
            int impactDelayMs,
            int variantIndex)
        {
            if (projectileAnimations == null || projectileAnimations.Count == 0)
            {
                return;
            }

            SkillAnimation animation = projectileAnimations[Math.Abs(variantIndex) % projectileAnimations.Count];
            if (animation?.Frames.Count <= 0)
            {
                return;
            }

            int lifeTime = Math.Max(60, impactDelayMs);
            float durationSeconds = lifeTime / 1000f;
            if (durationSeconds <= 0f)
            {
                return;
            }

            Vector2 delta = target - source;
            _projectiles.Add(new ActiveProjectile
            {
                Id = _projectiles.Count + 1,
                SkillId = summon.SkillId,
                SkillLevel = summon.Level,
                Data = new ProjectileData
                {
                    Animation = animation,
                    Speed = delta.Length() / durationSeconds,
                    LifeTime = lifeTime
                },
                LevelData = summon.LevelData,
                X = source.X,
                Y = source.Y,
                PreviousX = source.X,
                PreviousY = source.Y,
                VelocityX = delta.X / durationSeconds,
                VelocityY = delta.Y / durationSeconds,
                FacingRight = delta.X >= 0f,
                SpawnTime = currentTime,
                OwnerId = summon.ObjectId,
                OwnerX = source.X,
                OwnerY = source.Y,
                VisualOnly = true
            });
        }

        private void SchedulePacketAttackImpactEffects(
            ActiveSummon summon,
            IReadOnlyList<MobItem> targets,
            int currentTime,
            bool useTeslaPerTargetDelayJitter = false)
        {
            if (summon?.SkillData == null || targets == null || targets.Count == 0)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                MobItem target = targets[i];
                if (target == null)
                {
                    continue;
                }

                SkillAnimation impactAnimation = ResolvePacketAttackImpactAnimation(
                    summon.SkillData,
                    i,
                    summon.CurrentAnimationBranchName);
                if (impactAnimation == null)
                {
                    continue;
                }

                Vector2 impactPosition = ResolvePacketAttackImpactPosition(
                    summon.SkillData,
                    i,
                    summon.CurrentAnimationBranchName,
                    target,
                    new Vector2(summon.PositionX, summon.PositionY),
                    currentTime);
                int executeTime = currentTime + (useTeslaPerTargetDelayJitter
                    ? ResolvePacketTeslaTargetImpactDelayMs(summon, target, currentTime, i)
                    : ResolvePacketAttackImpactDelayMs(summon, target, currentTime, i));
                _scheduledHitEffects.Add(new ScheduledPacketOwnedHitEffect
                {
                    SequenceId = _nextScheduledHitEffectSequenceId++,
                    ExecuteTime = executeTime,
                    HitEffect = new ActiveHitEffect
                    {
                        SkillId = summon.SkillId,
                        X = impactPosition.X,
                        Y = impactPosition.Y,
                        StartTime = executeTime,
                        Animation = impactAnimation,
                        FacingRight = summon.FacingRight
                    }
                });
            }
        }

        private void SchedulePacketReactiveChainEffects(
            PacketOwnedSummonState state,
            IReadOnlyList<MobItem> targets,
            int currentTime)
        {
            ActiveSummon summon = state?.Summon;
            if (summon == null
                || targets == null
                || targets.Count == 0
                || !PacketOwnedSummonUpdateRules.ShouldRegisterClientOwnedReactiveAttackChainEffect(summon))
            {
                return;
            }

            const int chainDurationMs = 270;
            SkillAnimation chainAnimation = ResolveClientOwnedReactiveAttackChainAnimation(
                summon,
                state?.OwnerCharacterLevel ?? 1);
            string chainAnimationPath = PacketOwnedSummonUpdateRules.ResolveClientOwnedReactiveAttackChainAnimationPath(
                summon,
                state?.OwnerCharacterLevel ?? 1);
            bool canRenderChain = chainAnimation?.Frames.Count > 0 || _animationEffects != null;
            if (!canRenderChain)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                MobItem target = targets[i];
                if (target == null)
                {
                    continue;
                }

                Rectangle targetHitbox = GetMobHitbox(target, currentTime);
                (Vector2 source, Vector2 chainTarget) =
                    PacketOwnedSummonUpdateRules.ResolveClientOwnedReactiveAttackChainEndpoints(
                        summon,
                        targetHitbox,
                        summon.FacingRight);
                int executeTime = currentTime + PacketOwnedSummonUpdateRules.ResolveClientOwnedPostAttackEffectDelayMs(summon);
                _scheduledReactiveChainEffects.Add(new ScheduledPacketOwnedReactiveChainEffect
                {
                    SequenceId = _nextScheduledHitEffectSequenceId++,
                    Source = source,
                    Target = chainTarget,
                    ExecuteTime = executeTime,
                    DurationMs = chainDurationMs,
                    Animation = chainAnimation,
                    AnimationPath = chainAnimationPath,
                    FacingRight = chainTarget.X >= source.X
                });
            }
        }

        internal static SkillAnimation ResolveClientOwnedReactiveAttackChainAnimation(
            ActiveSummon summon,
            int ownerCharacterLevel = 1)
        {
            SkillData skill = summon?.SkillData;
            SkillAnimation authoredReactiveChainAnimation = SummonClientPostEffectRules.ResolveReactiveAttackChainAnimation(
                summon?.SkillId ?? 0,
                skill,
                summon?.Level ?? 1,
                ownerCharacterLevel);
            if (authoredReactiveChainAnimation?.Frames.Count > 0)
            {
                return authoredReactiveChainAnimation;
            }

            if (SummonClientPostEffectRules.IsReactiveAttackChainSkill(summon?.SkillId ?? 0))
            {
                return null;
            }

            if (skill?.SummonProjectileAnimations != null)
            {
                foreach (SkillAnimation animation in skill.SummonProjectileAnimations)
                {
                    if (animation?.Frames.Count > 0)
                    {
                        return animation;
                    }
                }
            }

            return skill?.Projectile?.Animation?.Frames.Count > 0
                ? skill.Projectile.Animation
                : null;
        }

        private static Rectangle GetMobHitbox(MobItem mob, int currentTime)
        {
            if (mob == null)
            {
                return Rectangle.Empty;
            }

            Rectangle bodyHitbox = mob.GetBodyHitbox(currentTime);
            if (!bodyHitbox.IsEmpty)
            {
                return bodyHitbox;
            }

            if (mob.MovementInfo == null)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                (int)mob.MovementInfo.X - 20,
                (int)mob.MovementInfo.Y - 50,
                40,
                50);
        }

        private void SpawnPacketTeslaTriangleEffect(IReadOnlyList<PacketOwnedSummonState> teslaStates, int currentTime)
        {
            if (!TryResolvePacketTeslaTriangleVertices(teslaStates, out Vector2 left, out Vector2 apex, out Vector2 right))
            {
                return;
            }

            SkillData skill = teslaStates?[0]?.Summon?.SkillData;
            if (skill?.SummonNamedAnimations == null
                || !skill.SummonNamedAnimations.TryGetValue("attackTriangle", out SkillAnimation animation)
                || animation?.Frames.Count <= 0)
            {
                return;
            }

            Vector2 triangleCenter = (left + apex + right) / 3f;
            bool facingRight = teslaStates[0]?.Summon?.FacingRight ?? true;
            SpawnHitEffect(TeslaCoilSkillId, animation, triangleCenter.X, triangleCenter.Y, facingRight, currentTime);
        }

        private void SpawnHitEffect(int skillId, SkillAnimation animation, float x, float y, bool facingRight, int currentTime)
        {
            if (animation == null)
            {
                return;
            }

            _hitEffects.Add(new ActiveHitEffect
            {
                SkillId = skillId,
                X = x,
                Y = y,
                StartTime = currentTime,
                Animation = animation,
                FacingRight = facingRight
            });
        }

        private static SummonImpactPresentation ResolvePacketAttackImpactPresentation(
            SkillData skill,
            int targetIndex,
            string attackBranchName = null)
        {
            return skill?.GetSummonTargetHitPresentation(targetIndex, attackBranchName);
        }

        private static SkillAnimation ResolvePacketAttackImpactAnimation(
            SkillData skill,
            int targetIndex,
            string attackBranchName = null)
        {
            return ResolvePacketAttackImpactPresentation(skill, targetIndex, attackBranchName)?.Animation ?? skill?.HitEffect;
        }

        private static int ResolvePacketAttackImpactAuthoredDelayMs(
            SkillData skill,
            int targetIndex,
            string attackBranchName = null)
        {
            SummonImpactPresentation presentation = ResolvePacketAttackImpactPresentation(skill, targetIndex, attackBranchName);
            return presentation?.HitAfterMs > 0
                ? presentation.HitAfterMs
                : Math.Max(0, skill?.ResolveSummonAttackAfterMs(attackBranchName) ?? skill?.SummonAttackHitDelayMs ?? 0);
        }

        private static Vector2 ResolvePacketAttackImpactPosition(
            SkillData skill,
            int targetIndex,
            string attackBranchName,
            MobItem target,
            Vector2 source,
            int currentTime)
        {
            if (target == null)
            {
                return source;
            }

            SummonImpactPresentation presentation = ResolvePacketAttackImpactPresentation(skill, targetIndex, attackBranchName);
            int? projectilePositionCode = skill?.ResolveSummonProjectilePositionCode(attackBranchName, targetIndex);
            return SummonImpactPresentationResolver.ResolveImpactPosition(
                presentation,
                target.GetBodyHitbox(currentTime),
                source,
                GetMobHitboxCenter(target, currentTime),
                projectilePositionCode);
        }

        private int ResolvePacketTeslaTargetImpactDelayMs(ActiveSummon summon, MobItem target, int currentTime, int targetIndex = 0)
        {
            Vector2 source = summon == null
                ? Vector2.Zero
                : new Vector2(summon.PositionX, summon.PositionY);
            return ResolvePacketTeslaTargetImpactDelayMs(summon, source, target, currentTime, targetIndex);
        }

        private int ResolvePacketTeslaTargetImpactDelayMs(ActiveSummon summon, Vector2 source, MobItem target, int currentTime, int targetIndex = 0)
        {
            int attackAfterMs = Math.Max(
                0,
                summon?.SkillData?.ResolveExplicitSummonAttackAfterMs(summon.CurrentAnimationBranchName) ?? 0);
            return SummonClientPostEffectRules.ResolveSummonedAttackImpactDelayMs(
                summon?.SkillId ?? 0,
                attackAfterMs,
                ResolvePacketTeslaAttackDelayWindowMs(summon),
                _random.Next);
        }

        private static int ResolvePacketAttackImpactDelayMs(ActiveSummon summon, MobItem target, int currentTime, int targetIndex = 0)
        {
            Vector2 source = summon == null
                ? Vector2.Zero
                : new Vector2(summon.PositionX, summon.PositionY);
            return ResolvePacketAttackImpactDelayMs(summon, source, target, currentTime, targetIndex);
        }

        private static int ResolvePacketAttackImpactDelayMs(ActiveSummon summon, Vector2 source, MobItem target, int currentTime, int targetIndex = 0)
        {
            if (summon?.SkillData == null || target == null)
            {
                return 0;
            }

            string attackBranchName = summon.CurrentAnimationBranchName;
            int delayMs = ResolvePacketAttackImpactAuthoredDelayMs(summon.SkillData, targetIndex, attackBranchName);
            delayMs = SummonRuntimeRules.ResolveSummonImpactExecutionDelayMs(
                summon.SkillData,
                delayMs,
                attackBranchName);
            return delayMs;
        }

        private static int ResolvePacketTeslaAttackDelayWindowMs(ActiveSummon summon)
        {
            if (summon?.SkillData == null)
            {
                return TeslaMinimumImpactDelayMs;
            }

            return Math.Max(
                TeslaMinimumImpactDelayMs,
                Math.Max(90, summon.SkillData.ResolveSummonAttackIntervalMs(summon.Level)));
        }

        private static Vector2[] ResolvePacketTeslaProjectileSources(IReadOnlyList<PacketOwnedSummonState> teslaStates)
        {
            if (teslaStates == null || teslaStates.Count == 0)
            {
                return Array.Empty<Vector2>();
            }

            Vector2[] triangleVertices = ResolvePacketTeslaTriangleVerticesOrFallback(teslaStates);
            if (triangleVertices.Length == 0)
            {
                return Array.Empty<Vector2>();
            }

            Vector2[] assignedSources = new Vector2[teslaStates.Count];
            List<Vector2> remainingVertices = triangleVertices.ToList();
            for (int i = 0; i < teslaStates.Count; i++)
            {
                ActiveSummon summon = teslaStates[i]?.Summon;
                Vector2 summonPosition = summon == null
                    ? Vector2.Zero
                    : new Vector2(summon.PositionX, summon.PositionY);
                if (remainingVertices.Count == 0)
                {
                    assignedSources[i] = summonPosition;
                    continue;
                }

                int nearestIndex = 0;
                float nearestDistance = Vector2.DistanceSquared(summonPosition, remainingVertices[0]);
                for (int vertexIndex = 1; vertexIndex < remainingVertices.Count; vertexIndex++)
                {
                    float distance = Vector2.DistanceSquared(summonPosition, remainingVertices[vertexIndex]);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestIndex = vertexIndex;
                    }
                }

                assignedSources[i] = remainingVertices[nearestIndex];
                remainingVertices.RemoveAt(nearestIndex);
            }

            return assignedSources;
        }

        private static Vector2[] ResolvePacketTeslaTriangleVerticesOrFallback(IReadOnlyList<PacketOwnedSummonState> teslaStates)
        {
            if (TryResolvePacketTeslaTriangleVertices(teslaStates, out Vector2 left, out Vector2 apex, out Vector2 right))
            {
                return new[] { left, apex, right };
            }

            return teslaStates
                .Where(static state => state?.Summon != null)
                .Select(static state => new Vector2(state.Summon.PositionX, state.Summon.PositionY))
                .Take(3)
                .ToArray();
        }

        private static bool TryResolvePacketTeslaTriangleVertices(
            IReadOnlyList<PacketOwnedSummonState> teslaStates,
            out Vector2 left,
            out Vector2 apex,
            out Vector2 right)
        {
            left = Vector2.Zero;
            apex = Vector2.Zero;
            right = Vector2.Zero;
            if (teslaStates == null || teslaStates.Count == 0)
            {
                return false;
            }

            Point[] packetTrianglePoints = teslaStates
                .Select(static state => state?.TeslaTrianglePoints)
                .FirstOrDefault(static points => points?.Length >= 3);
            if (packetTrianglePoints == null || packetTrianglePoints.Length < 3)
            {
                return false;
            }

            left = new Vector2(packetTrianglePoints[0].X, packetTrianglePoints[0].Y);
            apex = new Vector2(packetTrianglePoints[1].X, packetTrianglePoints[1].Y);
            right = new Vector2(packetTrianglePoints[2].X, packetTrianglePoints[2].Y);
            return true;
        }

        private void PlayPacketMobAttackFeedback(PacketOwnedSummonState state, SummonedHitPacket packet, int currentTime)
        {
            if (state?.Summon == null || packet.AttackIndex < 0)
            {
                return;
            }

            string attackAction = $"attack{packet.AttackIndex + 1}";
            MobItem mob = ResolvePacketHitMob(packet, attackAction, currentTime);
            PacketOwnedMobAttackFeedbackPresentation presentation = ResolvePacketMobAttackFeedbackPresentation(
                mob,
                packet.MobTemplateId,
                attackAction,
                currentMobAttackFrameIndex: ResolvePacketHitMobAttackFrameIndex(mob, attackAction, currentTime),
                soundManager: _soundManager,
                texturePool: _texturePool,
                graphicsDevice: _graphicsDevice,
                damageSoundIndex: packet.AttackIndex >= 1 ? 2 : 1);
            if (presentation.HitEffectEntry?.Frames?.Count > 0)
            {
                SpawnPacketMobAttackHitEffect(state.Summon, packet, presentation, currentTime);
            }

            string packetHitSoundKey = ShouldPlayPacketMobAttackFeedbackSound(presentation)
                ? ResolvePacketMobAttackSoundKey(
                    mob,
                    packet.AttackIndex >= 1 ? 2 : 1,
                    presentation.CharDamSoundKey,
                    presentation.PreferTemplateCharDamSound)
                : null;
            if (!string.IsNullOrWhiteSpace(packetHitSoundKey))
            {
                PlayOrSchedulePacketOwnedSound(
                    packetHitSoundKey,
                    currentTime + ResolvePacketMobAttackFeedbackHitAfterMs(presentation),
                    currentTime);
            }

            if (packet.Damage > 0)
            {
                PlayPacketSummonHitSound(state.Summon.SkillData);
            }
        }

        private MobItem ResolvePacketHitMob(SummonedHitPacket packet, string attackAction, int currentTime)
        {
            if (_mobPool == null || packet.MobTemplateId is not int mobTemplateId || mobTemplateId <= 0)
            {
                return null;
            }

            string mobTypeId = mobTemplateId.ToString();
            MobItem[] candidates = _mobPool.GetMobsByType(mobTypeId)
                .ToArray();
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            int bestCandidateIndex = SelectPacketHitMobCandidateIndex(
                candidates.Select(candidate => BuildPacketHitMobCandidate(
                    candidate,
                    attackAction,
                    currentTime,
                    packet.MobFacingLeft)).ToArray());
            return bestCandidateIndex >= 0 && bestCandidateIndex < candidates.Length
                ? candidates[bestCandidateIndex]
                : null;
        }

        internal static PacketOwnedHitMobCandidate BuildPacketHitMobCandidate(
            MobItem mob,
            string attackAction,
            int currentTime,
            bool? packetMobFacingLeft = null)
        {
            bool isAlive = mob?.AI?.IsDead != true;
            bool matchesCurrentAttack = mob != null
                && !string.IsNullOrWhiteSpace(attackAction)
                && string.Equals(mob.CurrentAction, attackAction, StringComparison.OrdinalIgnoreCase);
            bool matchesPacketFacing = !packetMobFacingLeft.HasValue
                || mob?.MovementInfo == null
                || mob.MovementInfo.FlipX == packetMobFacingLeft.Value;
            int observedFrameIndex = -1;
            bool matchesObservedAttack = mob?.TryGetRecentAttackFrameIndex(
                attackAction,
                currentTime,
                PacketOwnedHitRetainedAttackFrameWindowMs,
                out observedFrameIndex) == true;
            return new PacketOwnedHitMobCandidate(
                isAlive,
                matchesObservedAttack,
                matchesCurrentAttack,
                matchesObservedAttack ? Math.Max(0, observedFrameIndex) : -1,
                matchesPacketFacing);
        }

        internal static int SelectPacketHitMobCandidateIndex(IReadOnlyList<PacketOwnedHitMobCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return -1;
            }

            int bestIndex = -1;
            PacketOwnedHitMobCandidate bestCandidate = default;
            for (int i = 0; i < candidates.Count; i++)
            {
                PacketOwnedHitMobCandidate candidate = candidates[i];
                if (bestIndex < 0 || ComparePacketHitMobCandidate(candidate, bestCandidate) > 0)
                {
                    bestIndex = i;
                    bestCandidate = candidate;
                }
            }

            return bestIndex;
        }

        private static int ComparePacketHitMobCandidate(
            PacketOwnedHitMobCandidate left,
            PacketOwnedHitMobCandidate right)
        {
            int comparison = CompareBool(left.IsAlive, right.IsAlive);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareBool(left.MatchesObservedAttack, right.MatchesObservedAttack);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.ObservedFrameIndex.CompareTo(right.ObservedFrameIndex);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareBool(left.MatchesCurrentAttack, right.MatchesCurrentAttack);
            if (comparison != 0)
            {
                return comparison;
            }

            return CompareBool(left.MatchesPacketFacing, right.MatchesPacketFacing);
        }

        private static int CompareBool(bool left, bool right)
        {
            return left == right ? 0 : left ? 1 : -1;
        }

        private void SpawnPacketMobAttackHitEffect(
            ActiveSummon summon,
            SummonedHitPacket packet,
            PacketOwnedMobAttackFeedbackPresentation presentation,
            int currentTime)
        {
            MobAnimationSet.AttackHitEffectEntry hitEffectEntry = presentation.HitEffectEntry;
            List<IDXObject> frames = hitEffectEntry?.Frames;
            if (summon == null || frames == null || frames.Count == 0)
            {
                return;
            }

            MobAnimationSet.AttackInfoMetadata attackInfo = presentation.AttackInfo;
            int hitAnimationSourceFrameIndex = hitEffectEntry?.SourceFrameIndex ?? attackInfo?.HitAnimationSourceFrameIndex ?? 0;
            Vector2 hitPosition = ResolvePacketHitEffectPosition(
                summon,
                attackInfo,
                currentTime,
                hitFrameIndex: 0,
                hitAnimationSourceFrameIndex: hitAnimationSourceFrameIndex);
            bool followSummon = ResolveHitAttachForDisplayFrame(
                attackInfo,
                hitFrameIndex: 0,
                hitAnimationSourceFrameIndex: hitAnimationSourceFrameIndex);
            bool facingAttach = ResolveFacingAttachForDisplayFrame(
                attackInfo,
                hitFrameIndex: 0,
                hitAnimationSourceFrameIndex: hitAnimationSourceFrameIndex);
            bool followSummonFacing = followSummon || facingAttach;
            Vector2 detachedFallbackPosition = followSummon
                ? PacketOwnedSummonUpdateRules.ResolvePacketOwnedDetachedMobAttackHitAnchor(
                    GetSummonHitbox(summon, currentTime),
                    new Vector2(summon.PositionX, summon.PositionY),
                    attackInfo,
                    _random)
                : hitPosition;
            int startTime = currentTime + ResolvePacketMobAttackFeedbackHitAfterMs(presentation);
            _mobAttackHitEffects.Add(new PacketOwnedMobAttackHitEffectDisplay
            {
                X = detachedFallbackPosition.X,
                Y = detachedFallbackPosition.Y,
                AttachedSummonObjectId = summon.ObjectId,
                FollowSummon = followSummon,
                FollowSummonFacing = followSummonFacing,
                MirrorOffsetWithSummonFacing = facingAttach,
                AttachedOffset = PacketOwnedSummonUpdateRules.ResolvePacketOwnedAttachedMobAttackHitOffset(attackInfo),
                AttackInfo = attackInfo,
                HitAnimationSourceFrameIndex = hitAnimationSourceFrameIndex,
                StartTime = startTime,
                Frames = frames,
                CurrentFrame = 0,
                LastFrameTime = startTime,
                Flip = packet.MobFacingLeft != true
            });
        }

        internal static PacketOwnedMobAttackFeedbackPresentation ResolvePacketMobAttackFeedbackPresentation(
            MobItem mob,
            int? mobTemplateId,
            string attackAction,
            int? currentMobAttackFrameIndex,
            SoundManager soundManager,
            TexturePool texturePool,
            GraphicsDevice graphicsDevice,
            int damageSoundIndex)
        {
            MobAttackData liveAttackData = mob?.GetAttackData(attackAction);
            MobAnimationSet.AttackInfoMetadata liveAttackInfo = ApplyPacketMobAttackDataOverrides(
                mob?.GetAttackInfo(attackAction),
                liveAttackData);
            MobAnimationSet.AttackHitEffectEntry liveHitEffectEntry = mob?.GetAttackHitEffectEntry(
                attackAction,
                currentMobAttackFrameIndex);
            if (mobTemplateId is not int resolvedMobTemplateId || resolvedMobTemplateId <= 0)
            {
                return SelectPacketMobAttackFeedbackPresentation(
                    liveAttackInfo,
                    liveHitEffectEntry,
                    templateAttackInfo: null,
                    templateHitEffectEntry: null,
                    templateCharDamSoundKey: null);
            }

            bool hasLiveHitFrames = liveHitEffectEntry?.Frames?.Count > 0;
            bool needsTemplateAttackInfo = ShouldBorrowTemplateAttackInfoForLiveHitEntry(liveAttackInfo, liveHitEffectEntry);
            bool needsTemplateSoundKey = !HasRequestedLivePacketMobAttackSound(mob, damageSoundIndex);
            MobAttackData templateAttackData = ResolvePacketMobAttackData(resolvedMobTemplateId, attackAction);
            MobAnimationSet templateAnimationSet = null;
            MobAnimationSet.AttackInfoMetadata templateAttackInfo = null;
            bool hasTemplateAttackInfoHitPath = !string.IsNullOrWhiteSpace(templateAttackData?.HitEffectPath);
            if (needsTemplateAttackInfo || !hasLiveHitFrames || hasTemplateAttackInfoHitPath)
            {
                templateAnimationSet = LifeLoader.CreateMobAttackPresentationSet(
                    texturePool,
                    graphicsDevice,
                    resolvedMobTemplateId.ToString());
                templateAttackInfo = ApplyPacketMobAttackDataOverrides(
                    templateAnimationSet?.GetAttackInfoMetadata(attackAction),
                    templateAttackData);
            }

            MobAnimationSet.AttackHitEffectEntry templateAttackInfoHitEffectEntry = hasTemplateAttackInfoHitPath
                ? ResolvePacketMobAttackGeneralEffectEntry(
                    templateAttackData,
                    resolvedMobTemplateId.ToString(),
                    attackAction,
                    texturePool,
                    graphicsDevice)
                : null;
            if (templateAttackInfoHitEffectEntry?.Frames?.Count > 0)
            {
                return SelectPacketMobAttackFeedbackPresentation(
                    liveAttackInfo,
                    null,
                    templateAttackInfo,
                    templateAttackInfoHitEffectEntry,
                    LifeLoader.ResolveMobCharDamSoundKey(
                        soundManager,
                        resolvedMobTemplateId.ToString(),
                        damageSoundIndex));
            }

            if (hasLiveHitFrames)
            {
                return SelectPacketMobAttackFeedbackPresentation(
                    liveAttackInfo,
                    liveHitEffectEntry,
                    templateAttackInfo,
                    templateHitEffectEntry: null,
                    templateCharDamSoundKey: needsTemplateSoundKey
                        ? LifeLoader.ResolveMobCharDamSoundKey(
                            soundManager,
                            resolvedMobTemplateId.ToString(),
                            damageSoundIndex)
                        : null);
            }

            MobAnimationSet.AttackHitEffectEntry templateHitEffectEntry = ResolvePacketTemplateHitEffectEntry(
                templateAnimationSet,
                attackAction,
                currentMobAttackFrameIndex);
            string charDamSoundKey = LifeLoader.ResolveMobCharDamSoundKey(
                soundManager,
                resolvedMobTemplateId.ToString(),
                damageSoundIndex);
            return SelectPacketMobAttackFeedbackPresentation(
                liveAttackInfo,
                liveHitEffectEntry,
                templateAttackInfo,
                templateHitEffectEntry,
                charDamSoundKey);
        }

        internal static PacketOwnedMobAttackFeedbackPresentation SelectPacketMobAttackFeedbackPresentation(
            MobAnimationSet.AttackInfoMetadata liveAttackInfo,
            MobAnimationSet.AttackHitEffectEntry liveHitEffectEntry,
            MobAnimationSet.AttackInfoMetadata templateAttackInfo,
            MobAnimationSet.AttackHitEffectEntry templateHitEffectEntry,
            string templateCharDamSoundKey)
        {
            if (liveHitEffectEntry?.Frames?.Count > 0)
            {
                MobAnimationSet.AttackInfoMetadata attackInfo = SelectPacketMobAttackInfoForLiveHitEntry(
                    liveAttackInfo,
                    liveHitEffectEntry,
                    templateAttackInfo);

                return new PacketOwnedMobAttackFeedbackPresentation(
                    attackInfo,
                    liveHitEffectEntry,
                    templateCharDamSoundKey,
                    ResolvePacketMobAttackFeedbackHitAfterMs(attackInfo));
            }

            bool usesAttackInfoHitEffect = templateHitEffectEntry?.UsesAttackInfoHitEffect == true;
            MobAnimationSet.AttackInfoMetadata selectedAttackInfo = templateAttackInfo ?? liveAttackInfo;
            return new PacketOwnedMobAttackFeedbackPresentation(
                selectedAttackInfo,
                templateHitEffectEntry,
                templateCharDamSoundKey,
                usesAttackInfoHitEffect ? 0 : ResolvePacketMobAttackFeedbackHitAfterMs(selectedAttackInfo),
                usesAttackInfoHitEffect && !string.IsNullOrWhiteSpace(templateCharDamSoundKey));
        }

        internal static MobAnimationSet.AttackInfoMetadata SelectPacketMobAttackInfoForLiveHitEntry(
            MobAnimationSet.AttackInfoMetadata liveAttackInfo,
            MobAnimationSet.AttackHitEffectEntry liveHitEffectEntry,
            MobAnimationSet.AttackInfoMetadata templateAttackInfo)
        {
            if (liveHitEffectEntry?.Frames?.Count <= 0)
            {
                return templateAttackInfo ?? liveAttackInfo;
            }

            if (ShouldBorrowTemplateAttackInfoForLiveHitEntry(liveAttackInfo, liveHitEffectEntry))
            {
                return templateAttackInfo ?? liveAttackInfo;
            }

            if (liveAttackInfo == null || templateAttackInfo == null)
            {
                return liveAttackInfo ?? templateAttackInfo;
            }

            bool needsTemplateHitAfter = !liveAttackInfo.HasHitAfterMetadata && templateAttackInfo.HasHitAfterMetadata;
            bool needsTemplateRangeOrigin = !liveAttackInfo.HasRangeOrigin && templateAttackInfo.HasRangeOrigin;
            bool needsTemplateRangeBounds = !liveAttackInfo.HasRangeBounds && templateAttackInfo.HasRangeBounds;
            if (!needsTemplateHitAfter && !needsTemplateRangeOrigin && !needsTemplateRangeBounds)
            {
                return liveAttackInfo;
            }

            MobAnimationSet.AttackInfoMetadata mergedAttackInfo = CloneAttackInfoMetadata(liveAttackInfo);
            if (needsTemplateHitAfter)
            {
                mergedAttackInfo.HitAfterMs = templateAttackInfo.HitAfterMs;
                mergedAttackInfo.HasHitAfterMetadata = true;
            }

            if (needsTemplateRangeOrigin)
            {
                mergedAttackInfo.HasRangeOrigin = true;
                mergedAttackInfo.RangeOrigin = templateAttackInfo.RangeOrigin;
            }

            if (needsTemplateRangeBounds)
            {
                mergedAttackInfo.HasRangeBounds = true;
                mergedAttackInfo.RangeBounds = templateAttackInfo.RangeBounds;
            }

            return mergedAttackInfo;
        }

        private static MobAnimationSet.AttackInfoMetadata CloneAttackInfoMetadata(
            MobAnimationSet.AttackInfoMetadata source)
        {
            if (source == null)
            {
                return null;
            }

            MobAnimationSet.AttackInfoMetadata clone = new()
            {
                AttackType = source.AttackType,
                HitAnimationSourceFrameIndex = source.HitAnimationSourceFrameIndex,
                HitAttach = source.HitAttach,
                FacingAttach = source.FacingAttach,
                HitAfterMs = source.HitAfterMs,
                HasHitAfterMetadata = source.HasHitAfterMetadata,
                EffectFacingAttach = source.EffectFacingAttach,
                HasRangeBounds = source.HasRangeBounds,
                RangeBounds = source.RangeBounds,
                HasRangeOrigin = source.HasRangeOrigin,
                RangeOrigin = source.RangeOrigin,
                RangeRadius = source.RangeRadius,
                EffectAfter = source.EffectAfter,
                AttackAfter = source.AttackAfter,
                RandDelayAttack = source.RandDelayAttack,
                AreaCount = source.AreaCount,
                AttackCount = source.AttackCount,
                StartOffset = source.StartOffset,
                HasPrimaryEffect = source.HasPrimaryEffect,
                HasAreaWarning = source.HasAreaWarning,
                IsRushAttack = source.IsRushAttack,
                IsJumpAttack = source.IsJumpAttack,
                Tremble = source.Tremble,
                IsAngerAttack = source.IsAngerAttack,
                IsSpecialAttack = source.IsSpecialAttack
            };

            foreach (KeyValuePair<int, bool> entry in source.FrameHitAttachOverrides)
            {
                clone.FrameHitAttachOverrides[entry.Key] = entry.Value;
            }

            foreach (KeyValuePair<int, bool> entry in source.FrameFacingAttachOverrides)
            {
                clone.FrameFacingAttachOverrides[entry.Key] = entry.Value;
            }

            return clone;
        }

        internal static bool ShouldBorrowTemplateAttackInfoForLiveHitEntry(
            MobAnimationSet.AttackInfoMetadata liveAttackInfo,
            MobAnimationSet.AttackHitEffectEntry liveHitEffectEntry)
        {
            if (liveHitEffectEntry?.Frames?.Count <= 0)
            {
                return false;
            }

            if (liveAttackInfo == null)
            {
                return true;
            }

            if (!liveHitEffectEntry.IsAttackFrameOwned)
            {
                return false;
            }

            int frameIndex = liveHitEffectEntry.SourceFrameIndex;
            return !liveAttackInfo.HasExplicitHitAttachMetadata(frameIndex)
                   && !liveAttackInfo.HasExplicitFacingAttachMetadata(frameIndex);
        }

        internal static int ResolvePacketMobAttackFeedbackHitAfterMs(PacketOwnedMobAttackFeedbackPresentation presentation)
        {
            return Math.Max(0, presentation.HitAfterMs);
        }

        internal static int ResolvePacketMobAttackFeedbackHitAfterMs(MobAnimationSet.AttackInfoMetadata attackInfo)
        {
            return Math.Max(0, attackInfo?.HitAfterMs ?? 0);
        }

        internal static bool ShouldPlayPacketMobAttackFeedbackSound(
            PacketOwnedMobAttackFeedbackPresentation presentation)
        {
            return presentation.HitEffectEntry?.Frames?.Count > 0;
        }

        private static bool HasRequestedLivePacketMobAttackSound(MobItem mob, int damageSoundIndex)
        {
            return HasRequestedLivePacketMobAttackSound(
                mob?.CharDam1SE,
                mob?.CharDam2SE,
                damageSoundIndex);
        }

        internal static bool HasRequestedLivePacketMobAttackSound(
            string liveCharDam1SoundKey,
            string liveCharDam2SoundKey,
            int damageSoundIndex)
        {
            if (damageSoundIndex >= 2)
            {
                return !string.IsNullOrWhiteSpace(liveCharDam2SoundKey);
            }

            return !string.IsNullOrWhiteSpace(liveCharDam1SoundKey);
        }

        private Vector2 ResolvePacketHitEffectPosition(
            ActiveSummon summon,
            MobAnimationSet.AttackInfoMetadata attackInfo,
            int currentTime,
            int hitFrameIndex = 0,
            int? hitAnimationSourceFrameIndex = null)
        {
            Rectangle hitbox = GetSummonHitbox(summon, currentTime);
            Vector2 summonPosition = new(summon.PositionX, summon.PositionY);
            return ResolvePacketHitEffectPosition(hitbox, summonPosition, attackInfo, summon.FacingRight, _random, hitFrameIndex, hitAnimationSourceFrameIndex);
        }

        private static Vector2 ResolvePacketHitEffectPosition(
            Rectangle hitbox,
            Vector2 summonPosition,
            MobAnimationSet.AttackInfoMetadata attackInfo,
            bool facingRight,
            Random random,
            int hitFrameIndex = 0,
            int? hitAnimationSourceFrameIndex = null)
        {
            return PacketOwnedSummonUpdateRules.ResolvePacketOwnedMobAttackHitAnchor(
                hitbox,
                summonPosition,
                attackInfo,
                facingRight,
                random,
                hitFrameIndex,
                hitAnimationSourceFrameIndex);
        }

        internal static MobAnimationSet.AttackHitEffectEntry ResolvePacketTemplateHitEffectEntry(
            MobAnimationSet templateAnimationSet,
            string attackAction,
            int? currentMobAttackFrameIndex)
        {
            if (templateAnimationSet == null || string.IsNullOrWhiteSpace(attackAction))
            {
                return null;
            }

            return templateAnimationSet.GetAttackHitEffectEntry(attackAction, currentMobAttackFrameIndex)
                   ?? templateAnimationSet.GetAttackHitEffectEntry(attackAction);
        }

        internal static string ResolvePacketMobAttackSoundKey(
            MobItem mob,
            int damageSoundIndex,
            string templateCharDamSoundKey,
            bool preferTemplateCharDamSound = false)
        {
            string liveCharDam1SoundKey = mob?.CharDam1SE;
            string liveCharDam2SoundKey = mob?.CharDam2SE;
            return ResolvePacketMobAttackSoundKey(
                liveCharDam1SoundKey,
                liveCharDam2SoundKey,
                damageSoundIndex,
                templateCharDamSoundKey,
                preferTemplateCharDamSound);
        }

        internal static string ResolvePacketMobAttackSoundKey(
            string liveCharDam1SoundKey,
            string liveCharDam2SoundKey,
            int damageSoundIndex,
            string templateCharDamSoundKey,
            bool preferTemplateCharDamSound = false)
        {
            if (preferTemplateCharDamSound && !string.IsNullOrWhiteSpace(templateCharDamSoundKey))
            {
                return templateCharDamSoundKey;
            }

            if (damageSoundIndex >= 2)
            {
                if (!string.IsNullOrWhiteSpace(liveCharDam2SoundKey))
                {
                    return liveCharDam2SoundKey;
                }

                if (!string.IsNullOrWhiteSpace(templateCharDamSoundKey))
                {
                    return templateCharDamSoundKey;
                }

                if (!string.IsNullOrWhiteSpace(liveCharDam1SoundKey))
                {
                    return liveCharDam1SoundKey;
                }
            }
            else if (!string.IsNullOrWhiteSpace(liveCharDam1SoundKey))
            {
                return liveCharDam1SoundKey;
            }

            return templateCharDamSoundKey;
        }

        internal static MobAnimationSet.AttackInfoMetadata ApplyPacketMobAttackDataOverrides(
            MobAnimationSet.AttackInfoMetadata attackInfo,
            MobAttackData attackData)
        {
            if (attackData?.HasHitAttach != true)
            {
                return attackInfo;
            }

            if (attackInfo == null)
            {
                return new MobAnimationSet.AttackInfoMetadata
                {
                    HitAttach = attackData.HitAttach
                };
            }

            if (attackInfo.HitAttach == attackData.HitAttach)
            {
                return attackInfo;
            }

            MobAnimationSet.AttackInfoMetadata clone = CloneAttackInfoMetadata(attackInfo);
            clone.HitAttach = attackData.HitAttach;
            return clone;
        }

        private static MobAttackData ResolvePacketMobAttackData(int mobTemplateId, string attackAction)
        {
            if (mobTemplateId <= 0 || string.IsNullOrWhiteSpace(attackAction))
            {
                return null;
            }

            string mobInfoId = mobTemplateId.ToString().TrimStart('0');
            if (mobInfoId.Length == 0)
            {
                mobInfoId = "0";
            }

            MobInfo mobInfo = MobInfo.Get(mobInfoId);
            IReadOnlyList<MobAttackData> attackData = mobInfo?.MobData?.AttackData;
            if (attackData == null || attackData.Count == 0)
            {
                return null;
            }

            int actionIndex = ResolvePacketMobAttackActionIndex(attackAction);
            if (actionIndex <= 0)
            {
                return null;
            }

            for (int i = 0; i < attackData.Count; i++)
            {
                MobAttackData entry = attackData[i];
                int attackNum = entry?.AttackNum ?? 0;
                if ((entry?.Action ?? 0) == actionIndex || attackNum == actionIndex || attackNum + 1 == actionIndex)
                {
                    return entry;
                }
            }

            return attackData[0];
        }

        private static int ResolvePacketMobAttackActionIndex(string attackAction)
        {
            if (string.IsNullOrWhiteSpace(attackAction))
            {
                return 0;
            }

            if (attackAction.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(attackAction["attack".Length..], out int attackIndex))
            {
                return attackIndex;
            }

            if (attackAction.StartsWith("skill", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(attackAction["skill".Length..], out int skillIndex))
            {
                return skillIndex;
            }

            return 0;
        }

        private static MobAnimationSet.AttackHitEffectEntry ResolvePacketMobAttackGeneralEffectEntry(
            MobAttackData attackData,
            string mobTemplateId,
            string attackAction,
            TexturePool texturePool,
            GraphicsDevice graphicsDevice)
        {
            List<IDXObject> frames = TryLoadPacketMobAttackGeneralEffectFrames(
                attackData,
                mobTemplateId,
                attackAction,
                texturePool,
                graphicsDevice);
            return frames?.Count > 0
                ? new MobAnimationSet.AttackHitEffectEntry
                {
                    Frames = frames,
                    SourceFrameIndex = 0,
                    IsAttackFrameOwned = false,
                    UsesAttackInfoHitEffect = true
                }
                : null;
        }

        private static List<IDXObject> TryLoadPacketMobAttackGeneralEffectFrames(
            MobAttackData attackData,
            string mobTemplateId,
            string attackAction,
            TexturePool texturePool,
            GraphicsDevice graphicsDevice)
        {
            if (texturePool == null
                || graphicsDevice == null
                || string.IsNullOrWhiteSpace(attackData?.HitEffectPath))
            {
                return null;
            }

            string[] candidates = EnumeratePacketMobAttackGeneralEffectCandidateUols(
                attackData.HitEffectPath,
                mobTemplateId,
                attackAction);
            for (int i = 0; i < candidates.Length; i++)
            {
                WzImageProperty property = ResolvePacketMobAttackGeneralEffectProperty(candidates[i]);
                if (property == null)
                {
                    continue;
                }

                List<IDXObject> frames = MapSimulatorLoader.LoadFrames(
                    texturePool,
                    property.GetLinkedWzImageProperty() ?? property,
                    0,
                    0,
                    graphicsDevice,
                    usedProps: null);
                if (frames?.Count > 0)
                {
                    return frames;
                }
            }

            return null;
        }

        internal static string[] EnumeratePacketMobAttackGeneralEffectCandidateUols(
            string effectPath,
            string mobTemplateId,
            string attackAction)
        {
            string normalizedEffectPath = effectPath?.Replace('\\', '/').Trim().Trim('/');
            if (string.IsNullOrWhiteSpace(normalizedEffectPath))
            {
                return Array.Empty<string>();
            }

            var candidates = new List<string>();
            if (normalizedEffectPath.Split('/').Length >= 3)
            {
                candidates.Add(normalizedEffectPath);
            }

            string normalizedTemplateId = mobTemplateId?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedTemplateId))
            {
                normalizedTemplateId = normalizedTemplateId.PadLeft(7, '0');
            }
            if (!string.IsNullOrWhiteSpace(normalizedTemplateId))
            {
                candidates.Add($"Mob/{normalizedTemplateId}.img/{normalizedEffectPath}");
                if (!string.IsNullOrWhiteSpace(attackAction))
                {
                    candidates.Add($"Mob/{normalizedTemplateId}.img/{attackAction}/{normalizedEffectPath}");
                    candidates.Add($"Mob/{normalizedTemplateId}.img/{attackAction}/info/{normalizedEffectPath}");
                }
            }

            return candidates
                .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static WzImageProperty ResolvePacketMobAttackGeneralEffectProperty(string effectUol)
        {
            if (string.IsNullOrWhiteSpace(effectUol))
            {
                return null;
            }

            string normalized = effectUol.Replace('\\', '/').Trim().Trim('/');
            string[] segments = normalized.Split('/');
            if (segments.Length < 3)
            {
                return null;
            }

            WzImage image = global::HaCreator.Program.FindImage(segments[0], segments[1]);
            if (image == null)
            {
                return null;
            }

            WzImageProperty current = null;
            for (int i = 2; i < segments.Length; i++)
            {
                current = i == 2
                    ? WzInfoTools.GetRealProperty(image[segments[i]])
                    : WzInfoTools.GetRealProperty(current?[segments[i]]);
                if (current == null)
                {
                    break;
                }
            }

            return current;
        }

        private void PlayPacketSummonHitSound(SkillData skill)
        {
            if (skill == null || _soundManager == null)
            {
                return;
            }

            string soundKey = _skillLoader?.EnsureRepeatSoundRegistered(skill, _soundManager);
            if (!string.IsNullOrWhiteSpace(soundKey))
            {
                _soundManager.PlaySound(soundKey);
            }
        }

        private void PlayOrSchedulePacketOwnedSound(string soundKey, int executeTime, int currentTime)
        {
            if (string.IsNullOrWhiteSpace(soundKey) || _soundManager == null)
            {
                return;
            }

            if (executeTime <= currentTime)
            {
                _soundManager.PlaySound(soundKey);
                return;
            }

            _scheduledSounds.Add(new ScheduledPacketOwnedSound
            {
                SequenceId = _nextScheduledHitEffectSequenceId++,
                SoundKey = soundKey,
                ExecuteTime = executeTime
            });
        }

        private void DrawSummonTileEffect(
            SpriteBatch spriteBatch,
            PacketOwnedSummonTileEffectDisplay tileEffect,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (tileEffect?.Animation?.Frames.Count <= 0 || !tileEffect.IsActive(currentTime))
            {
                return;
            }

            SkillFrame frame = tileEffect.Animation.GetFrameAtTime(Math.Max(0, currentTime - tileEffect.StartTime));
            if (frame?.Texture == null)
            {
                return;
            }

            int tileWidth = Math.Max(1, frame.Texture.Width);
            int tileHeight = Math.Max(1, frame.Texture.Height);
            Color tint = Color.White * tileEffect.GetAlpha(currentTime);

            foreach (Point worldOrigin in PacketOwnedSummonUpdateRules.EnumerateClientOwnedTileOverlayOrigins(
                         tileEffect.Area,
                         tileWidth,
                         tileHeight,
                         tileEffect.EffectDistance))
            {
                int screenX = worldOrigin.X - mapShiftX + centerX;
                int screenY = worldOrigin.Y - mapShiftY + centerY;
                frame.Texture.DrawBackground(
                    spriteBatch,
                    null,
                    null,
                    screenX - frame.Origin.X,
                    screenY - frame.Origin.Y,
                    tint,
                    false,
                    null);
            }
        }

        private void DrawMobAttackHitEffect(SpriteBatch spriteBatch, PacketOwnedMobAttackHitEffectDisplay hitEffect, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (hitEffect?.Frames == null
                || hitEffect.CurrentFrame < 0
                || hitEffect.CurrentFrame >= hitEffect.Frames.Count)
            {
                return;
            }

            if (currentTime < hitEffect.StartTime)
            {
                return;
            }

            IDXObject frame = hitEffect.Frames[hitEffect.CurrentFrame];
            if (frame == null)
            {
                return;
            }

            Vector2 drawPosition = ResolveHitEffectDrawPosition(hitEffect);
            int screenX = (int)MathF.Round(drawPosition.X) - mapShiftX + centerX;
            int screenY = (int)MathF.Round(drawPosition.Y) - mapShiftY + centerY;
            bool shouldFlip = ResolveHitEffectFlip(hitEffect, frame);
            Point frameTopLeft = ResolvePacketOverlayFrameTopLeft(frame, screenX, screenY, shouldFlip);
            frame.DrawBackground(
                spriteBatch,
                null,
                null,
                frameTopLeft.X,
                frameTopLeft.Y,
                hitEffect.Tint,
                shouldFlip,
                null);
        }

        private void DrawReactiveChainEffect(
            SpriteBatch spriteBatch,
            PacketOwnedReactiveChainEffectDisplay chainEffect,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime)
        {
            if (chainEffect?.Animation?.Frames.Count <= 0
                || currentTime < chainEffect.StartTime
                || currentTime >= chainEffect.EndTime)
            {
                return;
            }

            int elapsed = Math.Max(0, currentTime - chainEffect.StartTime);
            SkillFrame frame = chainEffect.Animation.GetFrameAtTime(elapsed);
            if (frame?.Texture == null)
            {
                return;
            }

            Vector2 delta = chainEffect.Target - chainEffect.Source;
            float distance = delta.Length();
            int frameWidth = Math.Max(1, frame.Texture.Width);
            int segmentCount = Math.Max(1, (int)MathF.Ceiling(distance / Math.Max(18f, frameWidth * 0.75f)));
            bool shouldFlip = chainEffect.FacingRight ^ frame.Flip;

            for (int i = 0; i <= segmentCount; i++)
            {
                float progress = segmentCount == 0 ? 0f : i / (float)segmentCount;
                Vector2 point = Vector2.Lerp(chainEffect.Source, chainEffect.Target, progress);
                int screenX = (int)MathF.Round(point.X) - mapShiftX + centerX;
                int screenY = (int)MathF.Round(point.Y) - mapShiftY + centerY;
                frame.Texture.DrawBackground(
                    spriteBatch,
                    null,
                    null,
                    GetFrameDrawX(screenX, frame, shouldFlip),
                    screenY - frame.Origin.Y,
                    Color.White,
                    shouldFlip,
                    null);
            }
        }

        private Vector2 ResolveHitEffectDrawPosition(PacketOwnedMobAttackHitEffectDisplay hitEffect)
        {
            if (!ShouldHitEffectFollowSummon(hitEffect))
            {
                return new Vector2(hitEffect?.X ?? 0f, hitEffect?.Y ?? 0f);
            }

            if (_summonsByObjectId.TryGetValue(hitEffect.AttachedSummonObjectId, out PacketOwnedSummonState state)
                && state?.Summon != null)
            {
                return PacketOwnedSummonUpdateRules.ResolvePacketOwnedAttachedHitPosition(
                    new Vector2(state.Summon.PositionX, state.Summon.PositionY),
                    hitEffect.AttachedOffset,
                    ResolveHitEffectMirrorOffsetWithSummonFacing(hitEffect),
                    state.Summon.FacingRight);
            }

            return new Vector2(hitEffect.X, hitEffect.Y);
        }

        private bool ResolveHitEffectFlip(PacketOwnedMobAttackHitEffectDisplay hitEffect, IDXObject frame)
        {
            if (ShouldHitEffectFollowSummonFacing(hitEffect)
                && _summonsByObjectId.TryGetValue(hitEffect.AttachedSummonObjectId, out PacketOwnedSummonState state)
                && state?.Summon != null)
            {
                return state.Summon.FacingRight;
            }

            return hitEffect?.Flip ?? false;
        }

        private static bool ShouldHitEffectFollowSummon(PacketOwnedMobAttackHitEffectDisplay hitEffect)
        {
            if (hitEffect == null)
            {
                return false;
            }

            return hitEffect.AttackInfo != null
                ? ResolveHitAttachForDisplayFrame(
                    hitEffect.AttackInfo,
                    hitEffect.CurrentFrame,
                    hitEffect.HitAnimationSourceFrameIndex)
                : hitEffect.FollowSummon;
        }

        private static bool ShouldHitEffectFollowSummonFacing(PacketOwnedMobAttackHitEffectDisplay hitEffect)
        {
            if (hitEffect == null)
            {
                return false;
            }

            if (hitEffect.AttackInfo != null)
            {
                return ResolveHitAttachForDisplayFrame(
                           hitEffect.AttackInfo,
                           hitEffect.CurrentFrame,
                           hitEffect.HitAnimationSourceFrameIndex)
                       || ResolveFacingAttachForDisplayFrame(
                           hitEffect.AttackInfo,
                           hitEffect.CurrentFrame,
                           hitEffect.HitAnimationSourceFrameIndex);
            }

            return hitEffect.FollowSummonFacing;
        }

        private static bool ResolveHitEffectMirrorOffsetWithSummonFacing(PacketOwnedMobAttackHitEffectDisplay hitEffect)
        {
            if (hitEffect == null)
            {
                return false;
            }

            return hitEffect.AttackInfo != null
                ? ResolveFacingAttachForDisplayFrame(
                    hitEffect.AttackInfo,
                    hitEffect.CurrentFrame,
                    hitEffect.HitAnimationSourceFrameIndex)
                : hitEffect.MirrorOffsetWithSummonFacing;
        }

        private static int? ResolvePacketHitMobAttackFrameIndex(MobItem mob, string attackAction, int currentTime)
        {
            if (mob == null || string.IsNullOrWhiteSpace(attackAction))
            {
                return null;
            }

            return mob.TryGetRecentAttackFrameIndex(
                attackAction,
                currentTime,
                PacketOwnedHitRetainedAttackFrameWindowMs,
                out int frameIndex)
                ? frameIndex
                : null;
        }

        private static bool ResolveHitAttachForDisplayFrame(
            MobAnimationSet.AttackInfoMetadata attackInfo,
            int hitFrameIndex,
            int hitAnimationSourceFrameIndex)
        {
            return attackInfo?.ResolveHitAttach(hitAnimationSourceFrameIndex + Math.Max(0, hitFrameIndex)) == true;
        }

        private static bool ResolveFacingAttachForDisplayFrame(
            MobAnimationSet.AttackInfoMetadata attackInfo,
            int hitFrameIndex,
            int hitAnimationSourceFrameIndex)
        {
            return attackInfo?.ResolveFacingAttach(hitAnimationSourceFrameIndex + Math.Max(0, hitFrameIndex)) == true;
        }

        internal static Point ResolvePacketOverlayFrameTopLeft(IDXObject frame, int anchorX, int anchorY, bool shouldFlip)
        {
            if (frame == null)
            {
                return new Point(anchorX, anchorY);
            }

            int drawX = shouldFlip
                ? anchorX - (frame.Width + frame.X)
                : anchorX + frame.X;
            return new Point(drawX, anchorY + frame.Y);
        }

        private void PlayPacketIncDecHpFeedback(ActiveSummon summon, int delta, int currentTime)
        {
            if (_combatEffects == null || summon == null)
            {
                return;
            }

            Rectangle hitbox = GetSummonHitbox(summon, currentTime);
            float x = summon.PositionX;
            float y = !hitbox.IsEmpty ? hitbox.Top : summon.PositionY - 40f;
            if (delta > 0)
            {
                _combatEffects.AddPartyDamage(delta, x, y, isCritical: false, currentTime);
                return;
            }

            _combatEffects.AddMiss(x, y, currentTime);
        }

        private void DrawSummon(SpriteBatch spriteBatch, PacketOwnedSummonState state, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            ActiveSummon summon = state?.Summon;
            if (summon == null)
            {
                return;
            }

            int elapsed = Math.Max(0, currentTime - summon.StartTime);
            SkillAnimation animation = ResolveSummonAnimation(summon, currentTime, elapsed, out int animationTime);
            SkillFrame frame = null;
            float frameAlpha = 1f;
            if (animation?.TryGetFrameAtTime(animationTime, out frame, out int frameElapsedMs) == true)
            {
                frameAlpha = ResolveSkillFrameAlpha(frame, frameElapsedMs);
            }

            DrawSummonFrame(spriteBatch, summon, frame, mapShiftX, mapShiftY, centerX, centerY, frameAlpha);

            if (state.OneTimeActionClip is PacketOwnedOneTimeActionClip actionClip
                && TryResolvePacketOwnedOneTimeActionPlayback(actionClip, currentTime, out int actionAnimationTime))
            {
                SkillFrame actionFrame = null;
                float actionFrameAlpha = 1f;
                if (actionClip.Animation?.TryGetFrameAtTime(actionAnimationTime, out actionFrame, out int actionFrameElapsedMs) == true)
                {
                    actionFrameAlpha = ResolveSkillFrameAlpha(actionFrame, actionFrameElapsedMs);
                }

                DrawSummonFrame(spriteBatch, summon, actionFrame, mapShiftX, mapShiftY, centerX, centerY, actionFrameAlpha);
            }
        }

        private void DrawSummonFrame(SpriteBatch spriteBatch, ActiveSummon summon, SkillFrame frame, int mapShiftX, int mapShiftY, int centerX, int centerY, float frameAlpha = 1f)
        {
            if (summon == null || frame?.Texture == null || frameAlpha <= 0f)
            {
                return;
            }

            int screenX = (int)MathF.Round(summon.PositionX) - mapShiftX + centerX;
            int screenY = (int)MathF.Round(summon.PositionY) - mapShiftY + centerY;
            bool shouldFlip = summon.FacingRight ^ frame.Flip;
            Color tint = ResolveSummonDrawColor(summon) * MathHelper.Clamp(frameAlpha, 0f, 1f);

            frame.Texture.DrawBackground(
                spriteBatch,
                null,
                null,
                GetFrameDrawX(screenX, frame, shouldFlip),
                screenY - frame.Origin.Y,
                tint,
                shouldFlip,
                null);
        }

        internal static float ResolveSkillFrameAlpha(SkillFrame frame, int frameElapsedMs)
        {
            if (frame == null)
            {
                return 0f;
            }

            int startAlpha = Math.Clamp(frame.AlphaStart, 0, 255);
            int endAlpha = Math.Clamp(frame.AlphaEnd, 0, 255);
            if (startAlpha == endAlpha)
            {
                return startAlpha / 255f;
            }

            float progress = frame.Delay <= 0
                ? 1f
                : MathHelper.Clamp(frameElapsedMs / (float)Math.Max(1, frame.Delay), 0f, 1f);
            return MathHelper.Lerp(startAlpha, endAlpha, progress) / 255f;
        }

        private void DrawProjectile(SpriteBatch spriteBatch, ActiveProjectile projectile, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            SkillAnimation animation = projectile?.IsExploding == true
                ? projectile.Data?.ExplosionAnimation
                : projectile?.Data?.Animation;
            int animationTime = projectile?.IsExploding == true
                ? currentTime - projectile.ExplodeTime
                : currentTime - (projectile?.SpawnTime ?? currentTime);
            if (animation == null)
            {
                return;
            }

            SkillFrame frame = animation.GetFrameAtTime(animationTime);
            if (frame?.Texture == null)
            {
                return;
            }

            int screenX = (int)MathF.Round(projectile.X) - mapShiftX + centerX;
            int screenY = (int)MathF.Round(projectile.Y) - mapShiftY + centerY;
            bool shouldFlip = projectile.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(
                spriteBatch,
                null,
                null,
                GetFrameDrawX(screenX, frame, shouldFlip),
                screenY - frame.Origin.Y,
                Color.White,
                shouldFlip,
                null);
        }

        private void DrawHitEffect(SpriteBatch spriteBatch, ActiveHitEffect hitEffect, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (hitEffect.Animation == null)
            {
                return;
            }

            SkillFrame frame = hitEffect.Animation.GetFrameAtTime(hitEffect.AnimationTime(currentTime));
            if (frame?.Texture == null)
            {
                return;
            }

            int screenX = (int)MathF.Round(hitEffect.X) - mapShiftX + centerX;
            int screenY = (int)MathF.Round(hitEffect.Y) - mapShiftY + centerY;
            bool shouldFlip = hitEffect.FacingRight ^ frame.Flip;

            frame.Texture.DrawBackground(
                spriteBatch,
                null,
                null,
                GetFrameDrawX(screenX, frame, shouldFlip),
                screenY - frame.Origin.Y,
                Color.White,
                shouldFlip,
                null);
        }

        private Rectangle GetSummonHitbox(ActiveSummon summon, int currentTime)
        {
            int elapsed = Math.Max(0, currentTime - summon.StartTime);
            SkillAnimation animation = ResolveSummonAnimation(summon, currentTime, elapsed, out int animationTime)
                ?? summon.SkillData?.AffectedEffect
                ?? summon.SkillData?.Effect;
            SkillFrame frame = animation?.GetFrameAtTime(animationTime);
            if (frame?.Texture == null)
            {
                return new Rectangle((int)summon.PositionX - 24, (int)summon.PositionY - 60, 48, 60);
            }

            bool shouldFlip = summon.FacingRight ^ frame.Flip;
            int drawX = GetFrameDrawX((int)MathF.Round(summon.PositionX), frame, shouldFlip);
            int drawY = (int)MathF.Round(summon.PositionY) - frame.Origin.Y;
            return new Rectangle(drawX, drawY, frame.Texture.Width, frame.Texture.Height);
        }

        private Rectangle GetSummonContactBounds(ActiveSummon summon, int currentTime)
        {
            Rectangle currentBounds = GetSummonHitbox(summon, currentTime);
            if (summon == null || currentBounds.IsEmpty)
            {
                return currentBounds;
            }

            int deltaX = (int)MathF.Round(summon.PreviousPositionX - summon.PositionX);
            int deltaY = (int)MathF.Round(summon.PreviousPositionY - summon.PositionY);
            if (deltaX == 0 && deltaY == 0)
            {
                return currentBounds;
            }

            Rectangle previousBounds = new Rectangle(
                currentBounds.X + deltaX,
                currentBounds.Y + deltaY,
                currentBounds.Width,
                currentBounds.Height);
            return UnionRectangles(currentBounds, previousBounds);
        }

        private static Vector2 GetMobHitboxCenter(MobItem mob, int currentTime)
        {
            if (mob == null)
            {
                return Vector2.Zero;
            }

            Rectangle hitbox = mob.GetBodyHitbox(currentTime);
            if (!hitbox.IsEmpty)
            {
                return new Vector2(hitbox.Center.X, hitbox.Center.Y);
            }

            return SummonImpactPresentationResolver.ResolveFallbackTargetPosition(
                new Vector2(
                    mob.MovementInfo?.X ?? mob.CurrentX,
                    mob.MovementInfo?.Y ?? mob.CurrentY),
                mob.GetVisualHeight());
        }

        private static SkillAnimation ResolveSummonAnimation(ActiveSummon summon, int currentTime, int elapsedTime, out int animationTime)
        {
            animationTime = Math.Max(0, elapsedTime);
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            SkillAnimation spawnAnimation = skill.SummonSpawnAnimation;
            if (spawnAnimation?.Frames.Count > 0)
            {
                int spawnDuration = GetSkillAnimationDuration(spawnAnimation) ?? 0;
                if (spawnDuration > 0 && elapsedTime < spawnDuration)
                {
                    animationTime = elapsedTime;
                    return spawnAnimation;
                }

                animationTime = Math.Max(0, elapsedTime - spawnDuration);
            }

            SkillAnimation removalAnimation = skill.SummonRemovalAnimation;
            if (removalAnimation?.Frames.Count > 0 && summon.RemovalAnimationStartTime != int.MinValue)
            {
                int removalElapsed = currentTime - summon.RemovalAnimationStartTime;
                int removalDuration = GetSkillAnimationDuration(removalAnimation) ?? 0;
                if (removalElapsed >= 0 && removalDuration > 0 && removalElapsed < removalDuration)
                {
                    animationTime = removalElapsed;
                    return removalAnimation;
                }
            }

            SkillAnimation hitAnimation = skill.SummonHitAnimation;
            if (hitAnimation?.Frames.Count > 0 && summon.LastHitAnimationStartTime != int.MinValue)
            {
                int hitElapsed = currentTime - summon.LastHitAnimationStartTime;
                int hitDuration = GetSkillAnimationDuration(hitAnimation) ?? 0;
                if (hitElapsed >= 0 && hitDuration > 0 && hitElapsed < hitDuration)
                {
                    animationTime = hitElapsed;
                    return hitAnimation;
                }
            }

            if (summon?.OneTimeActionFallbackAnimation?.Frames.Count > 0
                && TryResolveOneTimeActionFallbackPlayback(summon, currentTime, out int fallbackAnimationTime))
            {
                animationTime = fallbackAnimationTime;
                return summon.OneTimeActionFallbackAnimation;
            }

            SkillAnimation prepareAnimation = skill.SummonAttackPrepareAnimation;
            if (ShouldUseSummonPrepareAnimation(summon, skill)
                && prepareAnimation?.Frames.Count > 0)
            {
                int prepareElapsed = Math.Max(0, currentTime - summon.LastStateChangeTime);
                int prepareDuration = GetSkillAnimationDuration(prepareAnimation) ?? 0;
                if (prepareDuration <= 0 || prepareElapsed < prepareDuration)
                {
                    animationTime = prepareElapsed;
                    return prepareAnimation;
                }
            }

            if (TryResolveSummonAttackPlaybackAnimation(summon, currentTime, skill, out SkillAnimation attackPlaybackAnimation, out int attackPlaybackTime))
            {
                animationTime = attackPlaybackTime;
                return attackPlaybackAnimation;
            }

            return skill.SummonAnimation?.Frames.Count > 0 ? skill.SummonAnimation : skill.Effect;
        }

        internal static bool TryResolveOneTimeActionFallbackPlayback(ActiveSummon summon, int currentTime, out int animationTime)
        {
            animationTime = 0;
            if (summon?.OneTimeActionFallbackAnimation?.Frames.Count <= 0
                || summon.OneTimeActionFallbackAnimationTime == int.MinValue)
            {
                return false;
            }

            int baseAnimationTime = Math.Max(0, summon.OneTimeActionFallbackAnimationTime);
            int totalDuration = GetSkillAnimationDuration(summon.OneTimeActionFallbackAnimation) ?? 0;
            if (totalDuration <= 0)
            {
                animationTime = baseAnimationTime;
                return summon.OneTimeActionFallbackEndTime > currentTime;
            }

            int remainingDuration = Math.Max(0, totalDuration - Math.Min(baseAnimationTime, totalDuration));
            if (remainingDuration <= 0)
            {
                return false;
            }

            int fallbackStartTime = summon.OneTimeActionFallbackStartTime == int.MinValue
                ? currentTime
                : summon.OneTimeActionFallbackStartTime;
            int elapsed = Math.Max(0, currentTime - fallbackStartTime);
            if (elapsed >= remainingDuration)
            {
                return false;
            }

            animationTime = Math.Min(totalDuration - 1, baseAnimationTime + elapsed);
            return true;
        }

        internal static PacketOwnedOneTimeActionClip? CreatePacketOwnedOneTimeActionClip(SkillAnimation animation, int animationTime, int currentTime)
        {
            if (animation?.Frames.Count <= 0)
            {
                return null;
            }

            int normalizedAnimationTime = Math.Max(0, animationTime);
            int duration = ResolveOneTimeActionFallbackDurationMs(animation, normalizedAnimationTime);
            return new PacketOwnedOneTimeActionClip(
                animation,
                normalizedAnimationTime,
                currentTime,
                currentTime + duration);
        }

        internal static bool TryResolvePacketOwnedOneTimeActionPlayback(PacketOwnedOneTimeActionClip clip, int currentTime, out int animationTime)
        {
            animationTime = 0;
            if (clip.Animation?.Frames.Count <= 0)
            {
                return false;
            }

            int totalDuration = GetSkillAnimationDuration(clip.Animation) ?? 0;
            if (totalDuration <= 0)
            {
                animationTime = Math.Max(0, clip.BaseAnimationTime);
                return currentTime < clip.EndTime;
            }

            int baseAnimationTime = Math.Max(0, clip.BaseAnimationTime);
            int remainingDuration = Math.Max(0, totalDuration - Math.Min(baseAnimationTime, totalDuration));
            if (remainingDuration <= 0)
            {
                return false;
            }

            int elapsed = Math.Max(0, currentTime - clip.StartTime);
            if (elapsed >= remainingDuration || currentTime >= clip.EndTime)
            {
                return false;
            }

            animationTime = Math.Min(totalDuration - 1, baseAnimationTime + elapsed);
            return true;
        }

        private static bool ShouldUseSummonPrepareAnimation(ActiveSummon summon, SkillData skill)
        {
            if (summon?.ActorState != SummonActorState.Prepare || skill == null)
            {
                return false;
            }

            if (skill.SkillId != TeslaCoilSkillId)
            {
                return true;
            }

            return summon.TeslaCoilState == 1
                || summon.TeslaCoilState == 2
                || summon.LastAttackAnimationStartTime == int.MinValue;
        }

        private static bool TryResolveSummonAttackPlaybackAnimation(
            ActiveSummon summon,
            int currentTime,
            SkillData skill,
            out SkillAnimation animation,
            out int animationTime)
        {
            animation = null;
            animationTime = 0;
            if (summon == null || skill == null || summon.LastAttackAnimationStartTime == int.MinValue)
            {
                return false;
            }

            SkillAnimation prepareAnimation = skill.SummonAttackPrepareAnimation;
            int prepareDuration = GetSkillAnimationDuration(prepareAnimation) ?? 0;

            SkillAnimation branchAnimation = null;
            bool hasBranchAnimation = !string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName)
                && skill.SummonNamedAnimations != null
                && skill.SummonNamedAnimations.TryGetValue(summon.CurrentAnimationBranchName, out branchAnimation)
                && branchAnimation?.Frames.Count > 0;
            SkillAnimation retryAttackAnimation = hasBranchAnimation
                ? null
                : ResolveEmptyActionRetryAnimation(summon);
            bool hasActionPlayback = hasBranchAnimation
                                     || retryAttackAnimation?.Frames.Count > 0;
            SkillAnimation attackAnimation = hasBranchAnimation
                ? branchAnimation
                : retryAttackAnimation?.Frames.Count > 0
                    ? retryAttackAnimation
                    : string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName)
                        ? skill.SummonAttackAnimation
                        : null;
            if (attackAnimation?.Frames.Count <= 0)
            {
                return false;
            }

            int attackElapsed = currentTime - summon.LastAttackAnimationStartTime;
            int attackDuration = GetSkillAnimationDuration(attackAnimation) ?? 0;
            int totalDuration = (hasActionPlayback ? 0 : prepareDuration) + attackDuration;
            if (attackElapsed < 0 || totalDuration <= 0 || attackElapsed >= totalDuration)
            {
                return false;
            }

            if (!hasActionPlayback
                && prepareAnimation?.Frames.Count > 0
                && attackElapsed < prepareDuration)
            {
                animation = prepareAnimation;
                animationTime = attackElapsed;
                return true;
            }

            animation = attackAnimation;
            animationTime = hasActionPlayback
                ? attackElapsed
                : Math.Max(0, attackElapsed - prepareDuration);
            return true;
        }

        private static SkillAnimation ResolveAttackAnimation(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName)
                && skill.SummonNamedAnimations != null
                && skill.SummonNamedAnimations.TryGetValue(summon.CurrentAnimationBranchName, out SkillAnimation branchAnimation)
                && branchAnimation?.Frames.Count > 0)
            {
                return branchAnimation;
            }

            SkillAnimation retryAnimation = ResolveEmptyActionRetryAnimation(summon);
            if (retryAnimation?.Frames.Count > 0)
            {
                return retryAnimation;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName))
            {
                return null;
            }

            return skill.SummonAttackAnimation;
        }

        internal static bool TryResolvePacketOwnedActiveAttackActorState(
            ActiveSummon summon,
            int currentTime,
            out SummonActorState state)
        {
            state = SummonActorState.Idle;
            SkillData skill = summon?.SkillData;
            if (summon == null
                || skill == null
                || summon.LastAttackAnimationStartTime == int.MinValue)
            {
                return false;
            }

            int elapsed = currentTime - summon.LastAttackAnimationStartTime;
            if (elapsed < 0)
            {
                return false;
            }

            int prepareDuration = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                skill,
                summon.CurrentAnimationBranchName);
            if (prepareDuration > 0 && elapsed < prepareDuration)
            {
                state = SummonActorState.Prepare;
                return true;
            }

            int attackDuration = GetSkillAnimationDuration(ResolveAttackAnimation(summon)) ?? 0;
            if (attackDuration <= 0)
            {
                return false;
            }

            if (elapsed < prepareDuration + attackDuration)
            {
                state = SummonActorState.Attack;
                return true;
            }

            return false;
        }

        internal static bool HasCompletedPacketOwnedAttackLayerRefresh(
            ActiveSummon summon,
            int lastCompletedAttackLayerRefreshStartTime,
            int currentTime)
        {
            if (summon?.SkillData == null)
            {
                return false;
            }

            int attackStartTime = summon.LastAttackAnimationStartTime;
            if (attackStartTime == int.MinValue
                || attackStartTime == lastCompletedAttackLayerRefreshStartTime)
            {
                return false;
            }

            int playbackDuration = ResolveSummonAttackPlaybackDurationMs(summon);
            if (playbackDuration <= 0)
            {
                return false;
            }

            return currentTime - attackStartTime >= playbackDuration;
        }

        internal static int ResolveSummonAttackPlaybackDurationMs(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return 0;
            }

            SkillAnimation attackAnimation = ResolveAttackAnimation(summon);
            int attackDuration = GetSkillAnimationDuration(attackAnimation) ?? 0;
            if (attackDuration <= 0)
            {
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(summon.CurrentAnimationBranchName))
            {
                return attackDuration;
            }

            int prepareDuration = GetSkillAnimationDuration(skill.SummonAttackPrepareAnimation) ?? 0;
            return prepareDuration + attackDuration;
        }

        private static string ResolveSelfDestructAttackBranch(ActiveSummon summon)
        {
            SkillData skill = summon?.SkillData;
            if (skill?.SummonNamedAnimations == null || string.IsNullOrWhiteSpace(skill.SummonAttackBranchName))
            {
                return null;
            }

            return skill.SummonNamedAnimations.ContainsKey(skill.SummonAttackBranchName)
                ? skill.SummonAttackBranchName
                : null;
        }

        private static int ResolveSelfDestructActionWindowMs(ActiveSummon summon, string branchName)
        {
            if (summon?.SkillData == null)
            {
                return 0;
            }

            int prepareDurationMs = SummonRuntimeRules.ResolveSummonActionPrepareDurationMs(
                summon.SkillData,
                branchName);
            string originalBranchName = summon.CurrentAnimationBranchName;
            summon.CurrentAnimationBranchName = branchName;
            SkillAnimation attackAnimation = ResolveAttackAnimation(summon);
            summon.CurrentAnimationBranchName = originalBranchName;

            int attackDurationMs = GetSkillAnimationDuration(attackAnimation) ?? 0;
            return prepareDurationMs + attackDurationMs;
        }

        private static int ResolveSelfDestructRemovalWindowMs(ActiveSummon summon)
        {
            return Math.Max(
                1,
                GetSkillAnimationDuration(summon?.SkillData?.SummonRemovalAnimation)
                ?? GetSkillAnimationDuration(summon?.SkillData?.SummonHitAnimation)
                ?? GetSkillAnimationDuration(summon?.SkillData?.SummonAttackAnimation)
                ?? summon?.SkillData?.HitEffect?.TotalDuration
                ?? 1);
        }

        private static string ResolvePacketOwnedSkillBranch(PacketOwnedSummonState state)
        {
            ActiveSummon summon = state?.Summon;
            SkillData skill = summon?.SkillData;
            if (skill == null)
            {
                return null;
            }

            return SummonRuntimeRules.ResolvePacketSkillBranch(skill, state.LastSkillAction, summon.AssistType);
        }

        internal static (string BranchName, int ActionCode) ResolvePacketOwnedExplicitSelfDestructPlayback(
            ActiveSummon summon,
            bool requiresNaturalExpiry)
        {
            if (summon?.SkillData == null)
            {
                return (null, 0);
            }

            string finalBranch = SummonRuntimeRules.ResolveSelfDestructFinalBranch(
                summon.SkillData,
                summon.AssistType);
            string attackBranch = ResolveSelfDestructAttackBranch(summon);
            string branchName = requiresNaturalExpiry
                ? finalBranch ?? attackBranch
                : attackBranch ?? finalBranch;
            int actionCode = 0;
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonRuntimeRules.TryResolveExplicitSelfDestructPlayback(
                    summon.SkillData,
                    summon.AssistType,
                    branchName,
                    out string explicitBranchName,
                    out int explicitActionCode))
            {
                branchName = explicitBranchName;
                actionCode = explicitActionCode;
            }

            return (branchName, actionCode);
        }

        private static int? GetSkillAnimationDuration(SkillAnimation animation)
        {
            if (animation?.Frames.Count <= 0)
            {
                return null;
            }

            return animation.TotalDuration > 0
                ? animation.TotalDuration
                : animation.Frames.Sum(frame => frame.Delay);
        }

        private static Rectangle UnionRectangles(Rectangle first, Rectangle second)
        {
            if (first.IsEmpty)
            {
                return second;
            }

            if (second.IsEmpty)
            {
                return first;
            }

            int left = Math.Min(first.Left, second.Left);
            int top = Math.Min(first.Top, second.Top);
            int right = Math.Max(first.Right, second.Right);
            int bottom = Math.Max(first.Bottom, second.Bottom);

            return new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
        }

        private static int GetFrameDrawX(int anchorX, SkillFrame frame, bool shouldFlip)
        {
            if (frame?.Texture == null)
            {
                return anchorX;
            }

            return shouldFlip
                ? anchorX - (frame.Texture.Width - frame.Origin.X)
                : anchorX - frame.Origin.X;
        }

        private ref struct PacketReader
        {
            private readonly ReadOnlySpan<byte> _buffer;
            private int _offset;

            public PacketReader(ReadOnlySpan<byte> buffer)
            {
                _buffer = buffer;
                _offset = 0;
            }

            public bool CanRead(int byteCount) => _offset + byteCount <= _buffer.Length;

            public byte ReadByte()
            {
                EnsureReadable(sizeof(byte));
                return _buffer[_offset++];
            }

            public short ReadInt16()
            {
                EnsureReadable(sizeof(short));
                short value = (short)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
                _offset += sizeof(short);
                return value;
            }

            public int ReadInt32()
            {
                EnsureReadable(sizeof(int));
                int value = _buffer[_offset]
                    | (_buffer[_offset + 1] << 8)
                    | (_buffer[_offset + 2] << 16)
                    | (_buffer[_offset + 3] << 24);
                _offset += sizeof(int);
                return value;
            }

            private void EnsureReadable(int byteCount)
            {
                if (!CanRead(byteCount))
                {
                    throw new InvalidOperationException("Summoned packet payload ended unexpectedly.");
                }
            }
        }
    }
}
