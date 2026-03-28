using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
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

    public sealed class SummonedPool
    {
        private const int TeslaCoilSkillId = 35111002;

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
            public PlayerMovementSyncSnapshot MovementSnapshot { get; set; }
        }

        private readonly Dictionary<int, PacketOwnedSummonState> _summonsByObjectId = new();
        private readonly Dictionary<int, List<PacketOwnedSummonState>> _summonsByOwnerId = new();
        private readonly List<ActiveHitEffect> _hitEffects = new();
        private SkillLoader _skillLoader;
        private MobPool _mobPool;
        private RemoteUserActorPool _remoteUserPool;
        private Func<PlayerCharacter> _localPlayerAccessor;

        public int Count => _summonsByObjectId.Count;

        public void Initialize(
            SkillLoader skillLoader,
            MobPool mobPool,
            RemoteUserActorPool remoteUserPool,
            Func<PlayerCharacter> localPlayerAccessor)
        {
            _skillLoader = skillLoader;
            _mobPool = mobPool;
            _remoteUserPool = remoteUserPool;
            _localPlayerAccessor = localPlayerAccessor;
        }

        public void Clear()
        {
            foreach (PacketOwnedSummonState state in _summonsByObjectId.Values)
            {
                RemovePuppet(state.Summon);
            }

            _summonsByObjectId.Clear();
            _summonsByOwnerId.Clear();
            _hitEffects.Clear();
        }

        public IReadOnlyList<ActiveSummon> GetSummonsForOwner(int ownerCharacterId)
        {
            return _summonsByOwnerId.TryGetValue(ownerCharacterId, out List<PacketOwnedSummonState> summons)
                ? summons.Select(static state => state.Summon).ToArray()
                : Array.Empty<ActiveSummon>();
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

        public bool TryDamageSummonByObjectId(int objectId, int damage, int currentTime)
        {
            if (!_summonsByObjectId.TryGetValue(objectId, out PacketOwnedSummonState state))
            {
                return false;
            }

            ApplySummonDamage(state, Math.Max(1, damage), currentTime);
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

            SkillData skill = _skillLoader?.LoadSkill(packet.SkillId);
            SkillLevelData levelData = skill?.GetLevel(packet.SkillLevel);
            int durationMs = ResolveSummonDurationMs(skill, levelData);

            RemoveExistingState(packet.SummonedObjectId);

            ResolveOwnerState(packet.OwnerCharacterId, out string ownerName, out bool ownerIsLocal, out bool ownerFacingRight);
            bool facingRight = packet.MoveAction == 0
                ? ownerFacingRight
                : DecodeFacingRight(packet.MoveAction);

            var summon = new ActiveSummon
            {
                ObjectId = packet.SummonedObjectId,
                SkillId = packet.SkillId,
                Level = Math.Max(1, packet.SkillLevel),
                StartTime = currentTime,
                Duration = durationMs,
                LastAttackTime = currentTime,
                MoveAbility = packet.MoveAbility,
                MovementStyle = skill?.SummonMovementStyle ?? SummonMovementStyle.Stationary,
                SpawnDistanceX = skill?.SummonSpawnDistanceX ?? 0f,
                AnchorX = packet.Position.X,
                AnchorY = packet.Position.Y,
                PreviousPositionX = packet.Position.X,
                PreviousPositionY = packet.Position.Y,
                PositionX = packet.Position.X,
                PositionY = packet.Position.Y,
                SkillData = skill,
                LevelData = levelData,
                FacingRight = facingRight,
                AssistType = Enum.IsDefined(typeof(SummonAssistType), (int)packet.AssistType)
                    ? (SummonAssistType)packet.AssistType
                    : ResolveSummonAssistType(skill),
                ManualAssistEnabled = true,
                LastStateChangeTime = currentTime,
                MaxHealth = ResolveSummonMaxHealth(levelData),
                CurrentHealth = ResolveSummonMaxHealth(levelData)
            };

            var state = new PacketOwnedSummonState
            {
                Summon = summon,
                OwnerCharacterId = packet.OwnerCharacterId,
                OwnerCharacterLevel = Math.Max(1, packet.CharacterLevel),
                SkillLevel = Math.Max(1, packet.SkillLevel),
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
            state.Summon.LastAttackAnimationStartTime = currentTime;
            state.Summon.LastStateChangeTime = currentTime;
            state.Summon.FacingRight = !packet.FacingLeft;

            SpawnPacketAttackHitEffects(state, currentTime);
            return true;
        }

        public bool TryMarkSkill(int ownerCharacterId, int summonObjectId, byte attackAction, int currentTime, out string message)
        {
            message = null;
            if (!TryGetOwnedState(ownerCharacterId, summonObjectId, out PacketOwnedSummonState state, out message))
            {
                return false;
            }

            state.LastSkillAction = attackAction;
            state.LastSkillTime = currentTime;
            state.Summon.LastAttackAnimationStartTime = currentTime;
            state.Summon.LastStateChangeTime = currentTime;
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
            if (packet.Damage > 0)
            {
                ApplySummonDamage(state, packet.Damage, currentTime);
            }
            else
            {
                StartSummonHitReaction(state.Summon, currentTime);
            }

            return true;
        }

        public void Update(int currentTime)
        {
            for (int i = _hitEffects.Count - 1; i >= 0; i--)
            {
                if (_hitEffects[i].IsExpired(currentTime))
                {
                    _hitEffects.RemoveAt(i);
                }
            }

            foreach (PacketOwnedSummonState state in _summonsByObjectId.Values.ToArray())
            {
                ResolveOwnerState(state.OwnerCharacterId, out string ownerName, out bool ownerIsLocal, out bool ownerFacingRight);
                state.OwnerName = ownerName;
                state.OwnerIsLocal = ownerIsLocal;

                if (state.MovementSnapshot != null)
                {
                    ApplyMovementSnapshot(state, currentTime);
                }
                else if (state.LastMoveActionRaw != 0)
                {
                    state.Summon.FacingRight = DecodeFacingRight(state.LastMoveActionRaw);
                }
                else
                {
                    state.Summon.FacingRight = ownerFacingRight;
                }

                if (state.Summon.HasReachedNaturalExpiry(currentTime) && !state.Summon.ExpiryActionTriggered)
                {
                    BeginRemoval(state, currentTime, reason: 0);
                }

                if (state.Summon.IsPendingRemoval && currentTime >= state.Summon.PendingRemovalTime)
                {
                    RemoveState(state);
                    continue;
                }

                SyncPuppet(state, currentTime);
            }
        }

        public void Draw(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            foreach (PacketOwnedSummonState state in _summonsByObjectId.Values
                .OrderBy(static value => value.Summon.PositionY)
                .ThenBy(static value => value.OwnerCharacterId))
            {
                DrawSummon(spriteBatch, state.Summon, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            foreach (ActiveHitEffect hitEffect in _hitEffects)
            {
                DrawHitEffect(spriteBatch, hitEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
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

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
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

            state.RemovalReason = reason;
            state.Summon.ExpiryActionTriggered = true;
            state.Summon.RemovalAnimationStartTime = currentTime;
            state.Summon.ActorState = SummonActorState.Die;
            state.Summon.LastStateChangeTime = currentTime;
            state.Summon.PendingRemovalTime = currentTime + Math.Max(
                1,
                GetSkillAnimationDuration(state.Summon.SkillData?.SummonRemovalAnimation)
                ?? GetSkillAnimationDuration(state.Summon.SkillData?.SummonHitAnimation)
                ?? GetSkillAnimationDuration(state.Summon.SkillData?.SummonAttackAnimation)
                ?? state.Summon.SkillData?.HitEffect?.TotalDuration
                ?? 1);
            RemovePuppet(state.Summon);
        }

        private static int ResolveSummonMaxHealth(SkillLevelData levelData)
        {
            return Math.Max(1, levelData?.HP ?? 1);
        }

        private void ApplySummonDamage(PacketOwnedSummonState state, int damage, int currentTime)
        {
            if (state?.Summon == null || state.Summon.IsPendingRemoval)
            {
                return;
            }

            StartSummonHitReaction(state.Summon, currentTime);
            state.Summon.MaxHealth = Math.Max(1, state.Summon.MaxHealth);
            int startingHealth = state.Summon.CurrentHealth > 0 ? state.Summon.CurrentHealth : state.Summon.MaxHealth;
            state.Summon.CurrentHealth = Math.Max(0, startingHealth - Math.Max(1, damage));
            if (state.Summon.CurrentHealth <= 0)
            {
                BeginRemoval(state, currentTime, state.RemovalReason);
            }
        }

        private void StartSummonHitReaction(ActiveSummon summon, int currentTime)
        {
            if (summon == null)
            {
                return;
            }

            summon.LastHitAnimationStartTime = currentTime;
            summon.ActorState = SummonActorState.Hit;
            summon.LastStateChangeTime = currentTime;

            if (summon.SkillData?.HitEffect != null)
            {
                SpawnHitEffect(summon.SkillId, summon.SkillData.HitEffect, summon.PositionX, summon.PositionY, summon.FacingRight, currentTime);
            }
        }

        private void RemoveState(PacketOwnedSummonState state)
        {
            if (state == null)
            {
                return;
            }

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
                _mobPool?.SyncPuppetTargets(currentTime);
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

        private int ResolveSummonSlotIndex(PacketOwnedSummonState state)
        {
            if (!_summonsByOwnerId.TryGetValue(state.OwnerCharacterId, out List<PacketOwnedSummonState> summons))
            {
                return -1;
            }

            int slotIndex = 0;
            foreach (PacketOwnedSummonState candidate in summons.OrderBy(static value => value.Summon.StartTime).ThenBy(static value => value.Summon.ObjectId))
            {
                if (candidate.Summon.IsPendingRemoval)
                {
                    continue;
                }

                if (ReferenceEquals(candidate, state))
                {
                    return slotIndex;
                }

                slotIndex++;
            }

            return -1;
        }

        private static int ResolveSummonDurationMs(SkillData skill, SkillLevelData levelData)
        {
            if (levelData?.Time > 0)
            {
                return levelData.Time * 1000;
            }

            return skill != null && (skill.SkillId == 35111001 || skill.SkillId == 35111009 || skill.SkillId == 35111010)
                ? 0
                : 30000;
        }

        private static SummonAssistType ResolveSummonAssistType(SkillData skill)
        {
            if (skill == null)
            {
                return SummonAssistType.PeriodicAttack;
            }

            if (!string.IsNullOrWhiteSpace(skill.MinionAbility))
            {
                if (skill.MinionAbility.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0
                    || skill.MinionAbility.IndexOf("amplifyDamage", StringComparison.OrdinalIgnoreCase) >= 0
                    || skill.MinionAbility.IndexOf("mes", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return SummonAssistType.Support;
                }

                if (skill.MinionAbility.IndexOf("summon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return SummonAssistType.SummonAction;
                }
            }

            return skill.SkillId switch
            {
                35111001 or 35111009 or 35111010 => SummonAssistType.OwnerAttackTargeted,
                33111003 => SummonAssistType.TargetedAttack,
                _ => SummonAssistType.PeriodicAttack
            };
        }

        private static bool ShouldRegisterSummonPuppet(SkillData skill)
        {
            if (string.IsNullOrWhiteSpace(skill?.MinionAbility))
            {
                return false;
            }

            foreach (string token in skill.MinionAbility.Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Trim().Equals("taunt", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void SpawnPacketAttackHitEffects(PacketOwnedSummonState state, int currentTime)
        {
            if (state?.Summon?.SkillData?.HitEffect == null || state.LastAttackTargets == null || _mobPool == null)
            {
                return;
            }

            foreach (SummonedAttackTargetPacket target in state.LastAttackTargets)
            {
                if (target.MobObjectId <= 0)
                {
                    continue;
                }

                MobItem mob = _mobPool.GetMob(target.MobObjectId);
                float x = mob?.MovementInfo?.X ?? state.Summon.PositionX;
                float y = mob?.MovementInfo?.Y ?? state.Summon.PositionY;
                SpawnHitEffect(
                    state.Summon.SkillId,
                    state.Summon.SkillData.HitEffect,
                    x,
                    y,
                    state.Summon.FacingRight,
                    currentTime);
            }
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

        private void DrawSummon(SpriteBatch spriteBatch, ActiveSummon summon, int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            int elapsed = Math.Max(0, currentTime - summon.StartTime);
            SkillAnimation animation = ResolveSummonAnimation(summon, currentTime, elapsed, out int animationTime);
            SkillFrame frame = animation?.GetFrameAtTime(animationTime);
            if (frame?.Texture == null)
            {
                return;
            }

            int screenX = (int)MathF.Round(summon.PositionX) - mapShiftX + centerX;
            int screenY = (int)MathF.Round(summon.PositionY) - mapShiftY + centerY;
            bool shouldFlip = summon.FacingRight ^ frame.Flip;

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

            SkillAnimation prepareAnimation = skill.SummonAttackPrepareAnimation;
            SkillAnimation attackAnimation = skill.SummonAttackAnimation;
            if (attackAnimation?.Frames.Count > 0 && summon.LastAttackAnimationStartTime != int.MinValue)
            {
                int attackElapsed = currentTime - summon.LastAttackAnimationStartTime;
                int prepareDuration = GetSkillAnimationDuration(prepareAnimation) ?? 0;
                int attackDuration = GetSkillAnimationDuration(attackAnimation) ?? 0;
                int totalDuration = prepareDuration + attackDuration;
                if (attackElapsed >= 0 && totalDuration > 0 && attackElapsed < totalDuration)
                {
                    if (prepareAnimation?.Frames.Count > 0 && attackElapsed < prepareDuration)
                    {
                        animationTime = attackElapsed;
                        return prepareAnimation;
                    }

                    animationTime = Math.Max(0, attackElapsed - prepareDuration);
                    return attackAnimation;
                }
            }

            return skill.SummonAnimation?.Frames.Count > 0 ? skill.SummonAnimation : skill.Effect;
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
