using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Effects;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Pools
{
    public enum RemoteRelationshipOverlayType
    {
        Generic = 0,
        Couple = 1,
        Friendship = 2,
        NewYearCard = 3,
        Marriage = 4
    }

    /// <summary>
    /// Shared owner for remote user actors. This gives the simulator a single seam
    /// for remote avatar look decode, map insertion/removal, world rendering,
    /// helper markers, team state, chairs, mounts, and prepared-skill overlays.
    /// </summary>
    public sealed class RemoteUserActorPool
    {
        internal readonly record struct PortableChairPairParticipant(
            int CharacterId,
            PortableChair Chair,
            Vector2 Position,
            bool FacingRight,
            int? PreferredPairCharacterId,
            bool IsVisibleInWorld,
            bool IsRelationshipOverlaySuppressed);

        private readonly record struct PortableChairPairCandidate(
            PortableChairPairParticipant Left,
            PortableChairPairParticipant Right,
            int Priority,
            float Score);

        private const int MinimumMeleeAfterImageFadeDurationMs = 60;
        private const float FollowDriverGroundHorizontalOffset = 50f;
        private const float FollowDriverLadderRopeVerticalOffset = 30f;
        private const float RemoteDragonGroundSideOffset = 42f;
        private const float RemoteDragonGroundVerticalOffset = -12f;
        private const float RemoteDragonKeyDownBarHalfWidth = 36f;
        private const float RemoteDragonKeyDownBarVerticalGap = 30f;
        private const int RelationshipOverlayVisibleRangeX = 700;
        private const int RelationshipOverlayVisibleRangeY = 500;
        private const int RelationshipOverlayNearRangeX = 100;
        private const int RelationshipOverlayNearRangeY = 100;
        private const int NewYearCardOverlayNearRangeX = 250;
        private const int NewYearCardOverlayNearRangeY = 250;
        private const int NewYearCardDefaultItemId = 4300000;
        private static readonly EquipSlot[] BattlefieldAppearanceSlots =
        {
            EquipSlot.Cap,
            EquipSlot.Coat,
            EquipSlot.Longcoat,
            EquipSlot.Pants,
            EquipSlot.Shoes,
            EquipSlot.Glove,
            EquipSlot.Cape,
        };
        private static readonly Dictionary<int, RemoteDragonHudMetadata> RemoteDragonHudMetadataCache = new();
        private static readonly IComparer<RemoteUserActor> VisibleWorldActorComparer = Comparer<RemoteUserActor>.Create(static (left, right) =>
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return 1;
            }

            if (right is null)
            {
                return -1;
            }

            int yComparison = left.Position.Y.CompareTo(right.Position.Y);
            if (yComparison != 0)
            {
                return yComparison;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });

        private readonly Dictionary<int, RemoteUserActor> _actorsById = new();
        private readonly Dictionary<string, int> _actorIdsByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<RemoteUserActor> _visibleWorldActorsBuffer = new();
        private readonly List<StatusBarPreparedSkillRenderData> _preparedSkillWorldOverlayBuffer = new();
        private readonly List<MinimapUI.TrackedUserMarker> _helperMarkerBuffer = new();
        private readonly HashSet<(int LeftId, int RightId)> _renderedCouplePairsBuffer = new();
        private readonly HashSet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> _renderedItemEffectPairsBuffer = new();
        private int _preparedSkillWorldOverlayCount;
        private int _helperMarkerCount;
        private CharacterLoader _loader;
        private SkillLoader _skillLoader;

        public int Count => _actorsById.Count;
        public IEnumerable<RemoteUserActor> Actors => _actorsById.Values;
        public int PreparedSkillWorldOverlayCount => _preparedSkillWorldOverlayCount;
        public int HelperMarkerCount => _helperMarkerCount;
        public Action<int, string> ActorRemovedCallback { get; set; }

        public void Initialize(CharacterLoader loader, SkillLoader skillLoader)
        {
            _loader = loader;
            _skillLoader = skillLoader;
        }

        public void Clear()
        {
            foreach (RemoteUserActor actor in _actorsById.Values.ToArray())
            {
                ClearActorFollowLinks(actor);
                NotifyActorRemoved(actor.CharacterId, actor.Name);
            }

            _actorsById.Clear();
            _actorIdsByName.Clear();
        }

        public void RemoveBySourceTag(string sourceTag)
        {
            if (string.IsNullOrWhiteSpace(sourceTag))
            {
                return;
            }

            foreach (int characterId in _actorsById.Values
                .Where(actor => string.Equals(actor.SourceTag, sourceTag.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(actor => actor.CharacterId)
                .ToArray())
            {
                if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
                {
                    ClearActorFollowLinks(actor);
                    NotifyActorRemoved(actor.CharacterId, actor.Name);
                    _actorIdsByName.Remove(actor.Name);
                    _actorsById.Remove(characterId);
                }
            }
        }

        public void RemoveBySourceTagExcept(string sourceTag, IReadOnlySet<int> keepCharacterIds)
        {
            if (string.IsNullOrWhiteSpace(sourceTag))
            {
                return;
            }

            keepCharacterIds ??= new HashSet<int>();
            foreach (int characterId in _actorsById.Values
                .Where(actor => string.Equals(actor.SourceTag, sourceTag.Trim(), StringComparison.OrdinalIgnoreCase)
                    && !keepCharacterIds.Contains(actor.CharacterId))
                .Select(actor => actor.CharacterId)
                .ToArray())
            {
                if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
                {
                    ClearActorFollowLinks(actor);
                    NotifyActorRemoved(actor.CharacterId, actor.Name);
                    _actorIdsByName.Remove(actor.Name);
                    _actorsById.Remove(characterId);
                }
            }
        }

        public bool TryGetActor(int characterId, out RemoteUserActor actor)
        {
            return _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetActorByName(string name, out RemoteUserActor actor)
        {
            actor = null;
            return !string.IsNullOrWhiteSpace(name)
                   && _actorIdsByName.TryGetValue(name.Trim(), out int characterId)
                   && _actorsById.TryGetValue(characterId, out actor);
        }

        public bool TryGetPosition(string name, out Vector2 position)
        {
            if (TryGetActorByName(name, out RemoteUserActor actor))
            {
                position = actor.Position;
                return true;
            }

            position = default;
            return false;
        }

        public bool TryAddOrUpdate(
            int characterId,
            CharacterBuild build,
            Vector2 position,
            out string message,
            bool facingRight = true,
            string actionName = null,
            string sourceTag = null,
            bool isVisibleInWorld = true)
        {
            message = null;
            if (characterId <= 0)
            {
                message = "Remote character ID must be positive.";
                return false;
            }

            if (build == null)
            {
                message = "Remote character build is required.";
                return false;
            }

            build.Id = characterId;
            if (string.IsNullOrWhiteSpace(build.Name))
            {
                build.Name = $"Remote{characterId}";
            }

            if (_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                string previousName = actor.Name;
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                ResetBattlefieldAppearanceState(actor);
                actor.Build = build;
                actor.Name = build.Name.Trim();
                actor.Position = position;
                actor.FacingRight = facingRight;
                actor.ActionName = NormalizeActionName(actionName, actor.Build.ActivePortableChair != null);
                actor.SourceTag = string.IsNullOrWhiteSpace(sourceTag) ? actor.SourceTag : sourceTag.Trim();
                actor.IsVisibleInWorld = isVisibleInWorld;
                actor.RefreshAssembler();
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                UpdateNameLookup(previousName, actor.Name, characterId);
                return true;
            }

            RemoteUserActor created = new RemoteUserActor(
                characterId,
                build.Name.Trim(),
                build,
                position,
                facingRight,
                NormalizeActionName(actionName, build.ActivePortableChair != null),
                string.IsNullOrWhiteSpace(sourceTag) ? "remote" : sourceTag.Trim(),
                isVisibleInWorld);
            RegisterMeleeAfterImage(created, 0, created.ActionName, Environment.TickCount, 10, 0);
            _actorsById[characterId] = created;
            _actorIdsByName[created.Name] = characterId;
            return true;
        }

        public bool TryAddOrUpdateAvatarLook(
            int characterId,
            string name,
            LoginAvatarLook avatarLook,
            CharacterBuild template,
            Vector2 position,
            out string message,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (avatarLook == null)
            {
                message = "AvatarLook payload is required.";
                return false;
            }

            CharacterBuild build = _loader.LoadFromAvatarLook(avatarLook, template);
            if (build == null)
            {
                message = "AvatarLook could not be converted into a remote character build.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                build.Name = name.Trim();
            }

            return TryAddOrUpdate(
                characterId,
                build,
                position,
                out message,
                facingRight,
                actionName,
                sourceTag,
                isVisibleInWorld);
        }

        public bool TryMove(int characterId, Vector2 position, bool? facingRight, string actionName, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.Position = position;
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            if (!string.IsNullOrWhiteSpace(actionName))
            {
                actor.BeginMeleeAfterImageFade(Environment.TickCount);
                actor.ActionName = NormalizeActionName(actionName, actor.Build.ActivePortableChair != null);
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            }

            return true;
        }

        public bool TryApplyFollowCharacter(
            int characterId,
            int driverId,
            bool transferField,
            Vector2? transferPosition,
            int localCharacterId,
            Vector2 localCharacterPosition,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (driverId > 0 && driverId == characterId)
            {
                message = $"Remote character {characterId} cannot follow itself.";
                return false;
            }

            int previousDriverId = actor.FollowDriverId;
            ClearDriverPassengerLink(previousDriverId, characterId);

            if (driverId > 0)
            {
                actor.FollowDriverId = driverId;
                AssignDriverPassengerLink(driverId, characterId);
                return true;
            }

            actor.FollowDriverId = 0;
            if (transferField && transferPosition.HasValue)
            {
                actor.Position = transferPosition.Value;
            }
            else if (previousDriverId > 0)
            {
                if (previousDriverId == localCharacterId)
                {
                    actor.Position = localCharacterPosition;
                }
                else if (_actorsById.TryGetValue(previousDriverId, out RemoteUserActor previousDriver))
                {
                    actor.Position = previousDriver.Position;
                }
            }

            return true;
        }

        public bool TryAssignLocalPassengerToDriver(int driverId, int localPassengerId, out string message)
        {
            message = null;
            if (driverId <= 0)
            {
                message = "Remote driver ID must be positive.";
                return false;
            }

            if (localPassengerId <= 0)
            {
                message = "Local passenger ID must be positive.";
                return false;
            }

            if (!_actorsById.TryGetValue(driverId, out _))
            {
                message = $"Remote driver {driverId} does not exist.";
                return false;
            }

            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.FollowPassengerId == localPassengerId)
                {
                    actor.FollowPassengerId = 0;
                }
            }

            AssignDriverPassengerLink(driverId, localPassengerId);
            return true;
        }

        public bool TryClearLocalPassengerFromDriver(int driverId, int localPassengerId, out string message)
        {
            message = null;
            if (driverId <= 0)
            {
                message = "Remote driver ID must be positive.";
                return false;
            }

            if (localPassengerId <= 0)
            {
                message = "Local passenger ID must be positive.";
                return false;
            }

            if (!_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor))
            {
                message = $"Remote driver {driverId} does not exist.";
                return false;
            }

            if (driverActor.FollowPassengerId == localPassengerId)
            {
                driverActor.FollowPassengerId = 0;
            }

            return true;
        }

        public bool TrySetAction(int characterId, string actionName, bool? facingRight, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            actor.ActionName = NormalizeActionName(actionName, actor.Build.ActivePortableChair != null);
            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            actor.MovementDrivenActionSelection = false;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryRegisterMeleeAfterImage(
            int characterId,
            int skillId,
            string actionName,
            int? actionCode,
            int masteryPercent,
            int chargeSkillId,
            bool? facingRight,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (facingRight.HasValue)
            {
                actor.FacingRight = facingRight.Value;
            }

            SkillData skill = null;
            if (skillId > 0 && _skillLoader != null)
            {
                skill = _skillLoader.LoadSkill(skillId);
            }

            int chargeElement = AfterImageChargeSkillResolver.TryGetChargeElement(chargeSkillId, out int resolvedChargeElement)
                ? resolvedChargeElement
                : 0;
            string resolvedActionName = ResolveRemoteMeleeActionName(
                actor,
                skill,
                actionName,
                actionCode,
                masteryPercent,
                chargeElement);

            if (!string.IsNullOrWhiteSpace(resolvedActionName))
            {
                actor.BeginMeleeAfterImageFade(currentTime);
                actor.ActionName = NormalizeActionName(resolvedActionName, actor.Build.ActivePortableChair != null);
            }
            RegisterMeleeAfterImage(actor, skillId, actor.ActionName, currentTime, masteryPercent, chargeElement, actionCode);
            return true;
        }

        public bool TryApplyMoveAction(int characterId, byte moveAction, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.LastMoveActionRaw = moveAction;
            actor.FacingRight = DecodeFacingRight(moveAction);
            actor.BeginMeleeAfterImageFade(Environment.TickCount);
            actor.ActionName = ResolveActionName(actor, MoveActionFromRaw(moveAction));
            actor.MovementDrivenActionSelection = true;
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TryApplyMoveSnapshot(int characterId, PlayerMovementSyncSnapshot movementSnapshot, byte moveAction, int currentTime, out string message)
        {
            message = null;
            if (movementSnapshot == null)
            {
                message = "Remote movement snapshot is required.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.MovementSnapshot = movementSnapshot;
            actor.LastMoveActionRaw = moveAction;
            actor.MovementDrivenActionSelection = true;
            ApplyMovementSnapshot(actor, currentTime);
            return true;
        }

        public bool TrySetHelperMarker(int characterId, MinimapUI.HelperMarkerType? markerType, bool showDirectionOverlay, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.HelperMarkerType = markerType;
            actor.ShowDirectionOverlay = showDirectionOverlay;
            return true;
        }

        public bool TrySetBattlefieldTeam(int characterId, int? teamId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.BattlefieldTeamId = teamId;
            return true;
        }

        public void SyncBattlefieldAppearance(BattlefieldField battlefield)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                SyncBattlefieldAppearance(actor, battlefield);
            }
        }

        public bool TrySetPortableChair(int characterId, int? chairItemId, out string message, int? pairCharacterId = null)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ClearPortableChairMountState(actor);
            if (!chairItemId.HasValue || chairItemId.Value <= 0)
            {
                actor.Build.ActivePortableChair = null;
                actor.PreferredPortableChairPairCharacterId = null;
                actor.ActionName = CharacterPart.GetActionString(CharacterAction.Stand1);
                actor.RefreshAssembler();
                actor.ClearMeleeAfterImage();
                return true;
            }

            PortableChair chair = _loader.LoadPortableChair(chairItemId.Value);
            if (chair == null)
            {
                message = $"Portable chair {chairItemId.Value} could not be loaded.";
                return false;
            }

            actor.Build.ActivePortableChair = chair;
            actor.PreferredPortableChairPairCharacterId = pairCharacterId;
            ApplyPortableChairMount(actor, chair);
            actor.ActionName = NormalizeActionName("sit", allowSitFallback: true);
            actor.RefreshAssembler();
            actor.ClearMeleeAfterImage();
            return true;
        }

        public bool TrySetMount(int characterId, int? tamingMobItemId, out string message)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (!tamingMobItemId.HasValue || tamingMobItemId.Value <= 0)
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
                actor.RefreshAssembler();
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
                return true;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId.Value);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                message = $"Item {tamingMobItemId.Value} is not a taming mob mount.";
                return false;
            }

            actor.Build.Equip(mountPart);
            actor.RefreshAssembler();
            RegisterMeleeAfterImage(actor, 0, actor.ActionName, Environment.TickCount, 10, 0);
            return true;
        }

        public bool TrySetItemEffect(int characterId, int? itemId, int? pairCharacterId, int currentTime, out string message)
        {
            return TrySetItemEffect(
                characterId,
                RemoteRelationshipOverlayType.Generic,
                itemId,
                pairCharacterId,
                currentTime,
                out message);
        }

        public bool TrySetItemEffect(
            int characterId,
            RemoteRelationshipOverlayType relationshipType,
            int? itemId,
            int? pairCharacterId,
            int currentTime,
            out string message)
        {
            message = null;
            if (_loader == null)
            {
                message = "Character loader is not available.";
                return false;
            }

            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            if (!itemId.HasValue || itemId.Value <= 0)
            {
                actor.RelationshipOverlays.Remove(relationshipType);
                return true;
            }

            int resolvedItemId = itemId.Value;
            ItemEffectAnimationSet effect = _loader.LoadItemEffectAnimationSet(resolvedItemId);
            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard
                && effect == null
                && resolvedItemId != NewYearCardDefaultItemId)
            {
                resolvedItemId = NewYearCardDefaultItemId;
                effect = _loader.LoadItemEffectAnimationSet(resolvedItemId);
            }

            if (effect == null && relationshipType != RemoteRelationshipOverlayType.NewYearCard)
            {
                message = $"Item effect {resolvedItemId} could not be loaded from Effect/ItemEff.img.";
                return false;
            }

            actor.RelationshipOverlays[relationshipType] = new RemoteRelationshipOverlayState
            {
                RelationshipType = relationshipType,
                ItemId = resolvedItemId,
                PairCharacterId = pairCharacterId,
                Effect = NormalizeRelationshipOverlayEffect(
                    effect,
                    relationshipType,
                    shouldLoop: relationshipType != RemoteRelationshipOverlayType.Generic),
                StartTime = currentTime
            };
            return true;
        }

        public bool TryApplyAvatarModified(
            RemoteUserAvatarModifiedPacket packet,
            int currentTime,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            if (packet.AvatarLook != null)
            {
                if (_loader == null)
                {
                    message = "Character loader is not available.";
                    return false;
                }

                CharacterBuild updatedBuild = _loader.LoadFromAvatarLook(packet.AvatarLook, actor.Build?.Clone());
                if (updatedBuild == null)
                {
                    message = $"AvatarLook refresh for remote user {packet.CharacterId} could not be applied.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(actor.Name))
                {
                    updatedBuild.Name = actor.Name.Trim();
                }

                actor.BeginMeleeAfterImageFade(currentTime);
                actor.Build = updatedBuild;
                actor.ActionName = NormalizeActionName(actor.ActionName, actor.Build.ActivePortableChair != null);
                if (actor.Build.ActivePortableChair != null)
                {
                    ApplyPortableChairMount(actor, actor.Build.ActivePortableChair);
                }
                else
                {
                    ClearPortableChairMountState(actor);
                }

                actor.RefreshAssembler();
                RegisterMeleeAfterImage(actor, 0, actor.ActionName, currentTime, 10, 0);
            }

            if (packet.Speed.HasValue && actor.Build != null)
            {
                actor.Build.Speed = packet.Speed.Value;
            }

            if (packet.CarryItemEffect.HasValue)
            {
                actor.CarryItemEffectId = packet.CarryItemEffect.Value > 0
                    ? packet.CarryItemEffect
                    : null;
            }

            actor.CompletedSetItemId = packet.CompletedSetItemId;

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Couple,
                    packet.CoupleRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Friendship,
                    packet.FriendshipRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            RemoteUserRelationshipRecord marriageRecord = packet.MarriageRecord.IsActive
                ? packet.MarriageRecord with
                {
                    PairCharacterId = ResolveMarriagePairCharacterId(packet.CharacterId, packet.MarriageRecord)
                }
                : packet.MarriageRecord;
            if (!TryApplyAvatarModifiedRelationshipRecord(
                    actor,
                    RemoteRelationshipOverlayType.Marriage,
                    marriageRecord,
                    currentTime,
                    out message))
            {
                return false;
            }

            message = $"Remote user {packet.CharacterId} avatar-modified state applied.";
            return true;
        }

        public bool TryApplyTemporaryStatSnapshot(
            int characterId,
            RemoteUserTemporaryStatSnapshot temporaryStats,
            ushort delay,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote user {characterId} does not exist.";
                return false;
            }

            actor.TemporaryStats = temporaryStats;
            actor.TemporaryStatDelay = delay;
            message = temporaryStats.HasPayload
                ? $"Remote user {characterId} temporary-stat snapshot applied."
                : $"Remote user {characterId} temporary-stat snapshot cleared.";
            return true;
        }

        public bool TryApplyTemporaryStatSet(
            RemoteUserTemporaryStatSetPacket packet,
            out string message)
        {
            return TryApplyTemporaryStatSnapshot(
                packet.CharacterId,
                packet.TemporaryStats,
                packet.Delay,
                out message);
        }

        public bool TryApplyTemporaryStatReset(
            RemoteUserTemporaryStatResetPacket packet,
            out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(packet.CharacterId, out RemoteUserActor actor))
            {
                message = $"Remote user {packet.CharacterId} does not exist.";
                return false;
            }

            int[] currentMaskWords = actor.TemporaryStats.MaskWords ?? Array.Empty<int>();
            int[] resetMaskWords = packet.MaskWords ?? Array.Empty<int>();
            int maskWordCount = Math.Max(currentMaskWords.Length, resetMaskWords.Length);
            if (maskWordCount == 0)
            {
                actor.TemporaryStats = default;
                actor.TemporaryStatDelay = 0;
                message = $"Remote user {packet.CharacterId} temporary-stat mask cleared.";
                return true;
            }

            int[] remainingMaskWords = new int[maskWordCount];
            bool hasActiveBits = false;
            for (int i = 0; i < maskWordCount; i++)
            {
                int currentWord = i < currentMaskWords.Length ? currentMaskWords[i] : 0;
                int resetWord = i < resetMaskWords.Length ? resetMaskWords[i] : 0;
                int remainingWord = currentWord & ~resetWord;
                remainingMaskWords[i] = remainingWord;
                hasActiveBits |= remainingWord != 0;
            }

            actor.TemporaryStats = hasActiveBits
                ? actor.TemporaryStats with { MaskWords = remainingMaskWords }
                : default;
            actor.TemporaryStatDelay = 0;
            message = hasActiveBits
                ? $"Remote user {packet.CharacterId} temporary-stat mask updated."
                : $"Remote user {packet.CharacterId} temporary-stat mask cleared.";
            return true;
        }

        public bool TrySetPreparedSkill(
            int characterId,
            int skillId,
            string skillName,
            int durationMs,
            string skinKey,
            bool isKeydownSkill,
            bool isHolding,
            int gaugeDurationMs,
            int maxHoldDurationMs,
            PreparedSkillHudTextVariant textVariant,
            bool showText,
            int currentTime,
            out string message,
            int prepareDurationMs = 0,
            bool autoEnterHold = false)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.PreparedSkill = new RemotePreparedSkillState
            {
                SkillId = skillId,
                SkillName = string.IsNullOrWhiteSpace(skillName) ? $"Skill {skillId}" : skillName.Trim(),
                SkinKey = string.IsNullOrWhiteSpace(skinKey) ? "KeyDownBar" : skinKey.Trim(),
                DurationMs = Math.Max(0, durationMs),
                PrepareDurationMs = Math.Max(0, prepareDurationMs),
                GaugeDurationMs = gaugeDurationMs > 0 ? gaugeDurationMs : Math.Max(0, durationMs),
                StartTime = currentTime,
                IsKeydownSkill = isKeydownSkill,
                IsHolding = isHolding,
                AutoEnterHold = autoEnterHold && prepareDurationMs > 0,
                MaxHoldDurationMs = Math.Max(0, maxHoldDurationMs),
                TextVariant = textVariant,
                ShowText = showText
            };
            return true;
        }

        public bool TryClearPreparedSkill(int characterId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.PreparedSkill = null;
            return true;
        }

        public bool TrySetWorldVisibility(int characterId, bool isVisibleInWorld, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            actor.IsVisibleInWorld = isVisibleInWorld;
            return true;
        }

        public bool TryRemove(int characterId, out string message)
        {
            message = null;
            if (!_actorsById.TryGetValue(characterId, out RemoteUserActor actor))
            {
                message = $"Remote character {characterId} does not exist.";
                return false;
            }

            ClearActorFollowLinks(actor);
            NotifyActorRemoved(actor.CharacterId, actor.Name);
            _actorsById.Remove(characterId);
            _actorIdsByName.Remove(actor.Name);
            return true;
        }

        private void NotifyActorRemoved(int characterId, string name)
        {
            ActorRemovedCallback?.Invoke(characterId, name);
        }

        public void Update(int currentTime, PlayerCharacter localPlayer = null)
        {
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.FollowDriverId <= 0 && actor.MovementSnapshot != null)
                {
                    ApplyMovementSnapshot(actor, currentTime);
                }

                ApplyFollowDriverState(actor, localPlayer);

                if (actor.PreparedSkill != null && actor.PreparedSkill.DurationMs > 0)
                {
                    if (!TryResolvePreparedSkillPhase(actor.PreparedSkill, currentTime, out _, out _, out _, out _, out _))
                    {
                        actor.PreparedSkill = null;
                    }
                }

                foreach (KeyValuePair<RemoteRelationshipOverlayType, RemoteRelationshipOverlayState> overlayEntry in actor.RelationshipOverlays.ToArray())
                {
                    RemoteRelationshipOverlayState overlay = overlayEntry.Value;
                    if (overlay?.Effect == null
                        || overlay.RelationshipType != RemoteRelationshipOverlayType.Generic
                        || overlay.Effect.TotalDurationMs <= 0
                        || currentTime - overlay.StartTime < overlay.Effect.TotalDurationMs)
                    {
                        continue;
                    }

                    actor.RelationshipOverlays.Remove(overlayEntry.Key);
                }

                actor.UpdateMeleeAfterImage(currentTime);
            }
        }

        public void SyncPortableChairPairState(PlayerCharacter player)
        {
            if (player == null)
            {
                return;
            }

            player.ClearPortableChairExternalOwnerPair();
            PortableChair chair = player.Build?.ActivePortableChair;
            bool requestsExternalPair = chair?.IsCoupleChair == true;
            player.SetPortableChairPairRequestActive(requestsExternalPair);
            int localCharacterId = player.Build?.Id ?? 0;
            Dictionary<int, int> pairMap = BuildPortableChairPairMap(player, preferVisibleOnly: true);
            if (requestsExternalPair)
            {
                if (pairMap.TryGetValue(localCharacterId, out int pairCharacterId)
                    && _actorsById.TryGetValue(pairCharacterId, out RemoteUserActor pairActor))
                {
                    player.SetPortableChairExternalPair(pairActor.Position, pairActor.FacingRight);
                }
                else
                {
                    player.ClearPortableChairExternalPair();
                }
            }

            if (TryResolvePortableChairOwnerForLocalPlayer(player, pairMap, out RemoteUserActor ownerActor))
            {
                player.SetPortableChairExternalOwnerPair(
                    ownerActor.Build.ActivePortableChair,
                    ownerActor.Position,
                    ownerActor.FacingRight);
            }
        }

        public IReadOnlyList<StatusBarPreparedSkillRenderData> BuildPreparedSkillWorldOverlays(int currentTime)
        {
            _preparedSkillWorldOverlayCount = 0;
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                RemotePreparedSkillState prepared = actor.PreparedSkill;
                if (!actor.IsVisibleInWorld
                    || prepared == null
                    || PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId))
                {
                    continue;
                }

                StatusBarPreparedSkillRenderData overlay = BuildPreparedSkillWorldOverlay(actor, currentTime, _preparedSkillWorldOverlayCount);
                if (overlay == null || PreparedSkillHudRules.IsDragonOverlaySkill(overlay.SkillId))
                {
                    continue;
                }

                _preparedSkillWorldOverlayCount++;
            }

            return _preparedSkillWorldOverlayBuffer;
        }

        private StatusBarPreparedSkillRenderData BuildPreparedSkillWorldOverlay(RemoteUserActor actor, int currentTime, int bufferIndex)
        {
            RemotePreparedSkillState prepared = actor?.PreparedSkill;
            if (prepared == null)
            {
                return null;
            }

            if (!TryResolvePreparedSkillPhase(
                    prepared,
                    currentTime,
                    out int remainingMs,
                    out int duration,
                    out float progress,
                    out bool isHolding,
                    out int holdElapsedMs))
            {
                return null;
            }

            StatusBarPreparedSkillRenderData overlay = GetOrCreatePreparedSkillWorldOverlay(bufferIndex);
            overlay.SkillId = prepared.SkillId;
            overlay.SkillName = prepared.SkillName;
            overlay.SkinKey = prepared.SkinKey;
            overlay.Surface = PreparedSkillHudSurface.World;
            overlay.RemainingMs = remainingMs;
            overlay.DurationMs = duration;
            overlay.GaugeDurationMs = prepared.GaugeDurationMs > 0 ? prepared.GaugeDurationMs : duration;
            overlay.Progress = progress;
            overlay.IsKeydownSkill = prepared.IsKeydownSkill;
            overlay.IsHolding = isHolding;
            overlay.HoldElapsedMs = holdElapsedMs;
            overlay.MaxHoldDurationMs = prepared.MaxHoldDurationMs;
            overlay.TextVariant = prepared.TextVariant;
            overlay.ShowText = prepared.ShowText && !PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId);
            overlay.WorldAnchor = ResolvePreparedSkillWorldAnchor(actor, prepared, currentTime);
            return overlay;
        }

        private static bool TryResolvePreparedSkillPhase(
            RemotePreparedSkillState prepared,
            int currentTime,
            out int remainingMs,
            out int durationMs,
            out float progress,
            out bool isHolding,
            out int holdElapsedMs)
        {
            remainingMs = 0;
            durationMs = 0;
            progress = 0f;
            isHolding = false;
            holdElapsedMs = 0;

            if (prepared == null)
            {
                return false;
            }

            int elapsed = Math.Max(0, currentTime - prepared.StartTime);
            if (prepared.AutoEnterHold && prepared.PrepareDurationMs > 0)
            {
                if (elapsed < prepared.PrepareDurationMs)
                {
                    durationMs = prepared.PrepareDurationMs;
                    remainingMs = Math.Max(0, prepared.PrepareDurationMs - elapsed);
                    progress = durationMs > 0
                        ? MathHelper.Clamp(elapsed / (float)durationMs, 0f, 1f)
                        : 0f;
                    return true;
                }

                int holdDurationMs = Math.Max(0, prepared.MaxHoldDurationMs);
                holdElapsedMs = Math.Max(0, elapsed - prepared.PrepareDurationMs);
                if (holdDurationMs > 0 && holdElapsedMs >= holdDurationMs)
                {
                    return false;
                }

                durationMs = holdDurationMs;
                remainingMs = holdDurationMs > 0
                    ? Math.Max(0, holdDurationMs - holdElapsedMs)
                    : 0;
                progress = holdDurationMs > 0
                    ? MathHelper.Clamp(holdElapsedMs / (float)holdDurationMs, 0f, 1f)
                    : 1f;
                isHolding = true;
                return true;
            }

            durationMs = Math.Max(0, prepared.DurationMs);
            if (durationMs > 0 && elapsed >= durationMs)
            {
                return false;
            }

            remainingMs = durationMs > 0
                ? Math.Max(0, durationMs - elapsed)
                : 0;
            progress = durationMs > 0
                ? MathHelper.Clamp(elapsed / (float)durationMs, 0f, 1f)
                : 0f;
            isHolding = prepared.IsHolding;
            holdElapsedMs = isHolding ? elapsed : 0;
            return true;
        }

        private static Vector2 ResolvePreparedSkillWorldAnchor(RemoteUserActor actor, RemotePreparedSkillState prepared, int currentTime)
        {
            if (actor == null)
            {
                return Vector2.Zero;
            }

            if (prepared != null
                && PreparedSkillHudRules.IsDragonOverlaySkill(prepared.SkillId))
            {
                if (TryResolveRemoteDragonKeyDownBarAnchor(actor, prepared.SkillId, currentTime, out Vector2 dragonAnchor))
                {
                    return dragonAnchor;
                }

                return new Vector2(actor.Position.X, actor.Position.Y - 92f);
            }

            return ResolveStandardPreparedSkillWorldAnchor(actor, currentTime);
        }

        private static Vector2 ResolveStandardPreparedSkillWorldAnchor(RemoteUserActor actor, int currentTime)
        {
            AssembledFrame frame = actor.Assembler?.GetFrameAtTime(actor.ActionName, currentTime)
                ?? actor.Assembler?.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), currentTime);
            if (frame != null)
            {
                float topY = actor.Position.Y - frame.FeetOffset + frame.Bounds.Top;
                return new Vector2(actor.Position.X, topY - 18f);
            }

            return new Vector2(actor.Position.X, actor.Position.Y - 80f);
        }

        private static bool TryResolveRemoteDragonKeyDownBarAnchor(
            RemoteUserActor actor,
            int skillId,
            int currentTime,
            out Vector2 anchor)
        {
            anchor = Vector2.Zero;
            if (actor?.Build == null
                || !TryResolveRemoteDragonHudMetadata(actor.Build.Job, out RemoteDragonHudMetadata metadata))
            {
                return false;
            }

            AssembledFrame ownerFrame = actor.Assembler?.GetFrameAtTime(actor.ActionName, currentTime)
                ?? actor.Assembler?.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), currentTime);
            float ownerBodyOriginY = ownerFrame != null
                ? actor.Position.Y - ownerFrame.FeetOffset
                : actor.Position.Y;
            float side = actor.FacingRight ? -1f : 1f;
            float horizontalOffset = Math.Max(RemoteDragonGroundSideOffset, metadata.StandOriginX * 0.55f);
            Vector2 dragonAnchor = new(
                actor.Position.X + (side * horizontalOffset),
                ownerBodyOriginY + RemoteDragonGroundVerticalOffset);

            int dragonFrameHeight = metadata.ResolveFrameHeight(ResolveRemoteDragonActionName(skillId));
            anchor = new Vector2(
                dragonAnchor.X - RemoteDragonKeyDownBarHalfWidth,
                dragonAnchor.Y - dragonFrameHeight - RemoteDragonKeyDownBarVerticalGap);
            return true;
        }

        private static bool TryResolveRemoteDragonHudMetadata(int jobId, out RemoteDragonHudMetadata metadata)
        {
            metadata = default;
            int dragonJob = jobId switch
            {
                >= 2200 and <= 2218 => jobId,
                _ => 0
            };
            if (dragonJob == 0)
            {
                return false;
            }

            if (RemoteDragonHudMetadataCache.TryGetValue(dragonJob, out metadata))
            {
                return true;
            }

            WzImage image = global::HaCreator.Program.FindImage("Skill", $"Dragon/{dragonJob}.img");
            if (image == null)
            {
                return false;
            }

            var actionHeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int standOriginX = 79;

            foreach (WzSubProperty actionNode in image.WzProperties.OfType<WzSubProperty>())
            {
                if (string.Equals(actionNode.Name, "info", StringComparison.OrdinalIgnoreCase)
                    || !TryReadRemoteDragonFrameMetrics(actionNode, out int originX, out int height))
                {
                    continue;
                }

                actionHeights[actionNode.Name] = height;
                if (string.Equals(actionNode.Name, "stand", StringComparison.OrdinalIgnoreCase))
                {
                    standOriginX = originX;
                }
            }

            if (actionHeights.Count == 0)
            {
                return false;
            }

            metadata = new RemoteDragonHudMetadata(standOriginX, actionHeights);
            RemoteDragonHudMetadataCache[dragonJob] = metadata;
            return true;
        }

        private static bool TryReadRemoteDragonFrameMetrics(WzSubProperty actionNode, out int originX, out int height)
        {
            originX = 0;
            height = 0;

            WzCanvasProperty frame = actionNode.WzProperties
                .OfType<WzCanvasProperty>()
                .OrderBy(static canvas => ParseRemoteDragonFrameIndex(canvas.Name))
                .FirstOrDefault();
            if (frame == null)
            {
                return false;
            }

            if (frame["origin"] is not WzVectorProperty origin
                || frame["lt"] is not WzVectorProperty lt
                || frame["rb"] is not WzVectorProperty rb)
            {
                return false;
            }

            originX = origin.X.Value;
            height = Math.Max(1, rb.Y.Value - lt.Y.Value);
            return true;
        }

        private static int ParseRemoteDragonFrameIndex(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : int.MaxValue;
        }

        private static string ResolveRemoteDragonActionName(int skillId)
        {
            return skillId switch
            {
                22121000 => "icebreathe_prepare",
                22151001 => "breathe_prepare",
                _ => "stand"
            };
        }

        public IReadOnlyList<MinimapUI.TrackedUserMarker> BuildHelperMarkers()
        {
            _helperMarkerCount = 0;
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                MinimapUI.HelperMarkerType? markerType = actor.HelperMarkerType;
                if (!markerType.HasValue && actor.BattlefieldTeamId.HasValue)
                {
                    markerType = MinimapUI.HelperMarkerType.Match;
                }

                if (!markerType.HasValue)
                {
                    continue;
                }

                MinimapUI.TrackedUserMarker marker = GetOrCreateHelperMarker(_helperMarkerCount++);
                marker.WorldX = actor.Position.X;
                marker.WorldY = actor.Position.Y;
                marker.MarkerType = markerType.Value;
                marker.ShowDirectionOverlay = actor.ShowDirectionOverlay;
                marker.TooltipText = actor.Name;
            }

            return _helperMarkerBuffer;
        }

        public void Draw(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int tickCount,
            SpriteFont font,
            PlayerCharacter localPlayer = null,
            StatusBarUI statusBarUi = null)
        {
            List<RemoteUserActor> visibleActors = BuildVisibleWorldActorBuffer();
            _renderedCouplePairsBuffer.Clear();
            _renderedItemEffectPairsBuffer.Clear();

            for (int i = 0; i < visibleActors.Count; i++)
            {
                RemoteUserActor actor = visibleActors[i];
                AssembledFrame frame = actor.Assembler.GetFrameAtTime(actor.ActionName, tickCount)
                    ?? actor.Assembler.GetFrameAtTime(CharacterPart.GetActionString(CharacterAction.Stand1), tickCount);
                if (frame == null)
                {
                    continue;
                }

                int screenX = (int)Math.Round(actor.Position.X) - mapShiftX + centerX;
                int screenY = (int)Math.Round(actor.Position.Y) - mapShiftY + centerY;
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: false,
                    _renderedCouplePairsBuffer);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                DrawMeleeAfterImage(spriteBatch, skeletonMeshRenderer, actor, screenX, screenY, tickCount);
                DrawCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: false);
                if (statusBarUi != null
                    && actor.PreparedSkill != null
                    && PreparedSkillHudRules.IsDragonOverlaySkill(actor.PreparedSkill.SkillId))
                {
                    StatusBarPreparedSkillRenderData preparedOverlay = BuildPreparedSkillWorldOverlay(actor, tickCount, 0);
                    if (preparedOverlay != null)
                    {
                    statusBarUi.DrawPreparedSkillWorldOverlay(
                        spriteBatch,
                        mapShiftX,
                        mapShiftY,
                        centerX,
                        centerY,
                        tickCount,
                        preparedOverlay);
                    }
                }

                frame.Draw(spriteBatch, skeletonMeshRenderer, screenX, screenY, actor.FacingRight, Color.White);
                DrawRelationshipOverlays(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    font,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    screenX,
                    screenY,
                    tickCount,
                    _renderedItemEffectPairsBuffer);
                DrawPortableChairCoupleSharedLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);
                DrawPortableChairCoupleMidpointEffects(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    tickCount,
                    drawFrontLayers: true,
                    _renderedCouplePairsBuffer);
                DrawCompletedSetItemEffect(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    screenX,
                    screenY,
                    tickCount,
                    drawFrontLayers: true);

                if (font == null)
                {
                    continue;
                }

                Vector2 textSize = font.MeasureString(actor.Name);
                float topY = screenY - frame.FeetOffset + frame.Bounds.Top;
                Vector2 textPosition = new(screenX - (textSize.X / 2f), topY - textSize.Y - 10f);
                DrawOutlinedText(spriteBatch, font, actor.Name, textPosition, Color.Black, ResolveNameColor(actor));
            }
        }

        private List<RemoteUserActor> BuildVisibleWorldActorBuffer()
        {
            _visibleWorldActorsBuffer.Clear();
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                if (actor.IsVisibleInWorld)
                {
                    _visibleWorldActorsBuffer.Add(actor);
                }
            }

            if (_visibleWorldActorsBuffer.Count > 1)
            {
                _visibleWorldActorsBuffer.Sort(VisibleWorldActorComparer);
            }

            return _visibleWorldActorsBuffer;
        }

        private StatusBarPreparedSkillRenderData GetOrCreatePreparedSkillWorldOverlay(int index)
        {
            while (_preparedSkillWorldOverlayBuffer.Count <= index)
            {
                _preparedSkillWorldOverlayBuffer.Add(new StatusBarPreparedSkillRenderData());
            }

            return _preparedSkillWorldOverlayBuffer[index];
        }

        private MinimapUI.TrackedUserMarker GetOrCreateHelperMarker(int index)
        {
            while (_helperMarkerBuffer.Count <= index)
            {
                _helperMarkerBuffer.Add(new MinimapUI.TrackedUserMarker());
            }

            return _helperMarkerBuffer[index];
        }

        private void DrawPortableChairCoupleMidpointEffects(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int currentTime,
            bool drawFrontLayers,
            ISet<(int LeftId, int RightId)> renderedPairs)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleMidpointLayers == null
                || chair.CoupleMidpointLayers.Count == 0)
            {
                return;
            }

            if (TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _))
            {
                return;
            }

            RemoteUserActor partnerActor = FindPortableChairPairActor(
                chair,
                actor.CharacterId,
                actor.FacingRight,
                actor.Position.X,
                actor.Position.Y,
                skipCharacterId: actor.CharacterId,
                preferVisibleOnly: true);
            if (partnerActor == null)
            {
                return;
            }

            var pairKey = actor.CharacterId < partnerActor.CharacterId
                ? (actor.CharacterId, partnerActor.CharacterId)
                : (partnerActor.CharacterId, actor.CharacterId);
            if (drawFrontLayers)
            {
                if (!renderedPairs.Add(pairKey))
                {
                    return;
                }
            }
            else if (renderedPairs.Contains(pairKey))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((actor.Position.X + partnerActor.Position.X) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((actor.Position.Y + partnerActor.Position.Y) * 0.5f) - mapShiftY + centerY;
            int animationTime = currentTime;
            for (int i = 0; i < chair.CoupleMidpointLayers.Count; i++)
            {
                PortableChairLayer layer = chair.CoupleMidpointLayers[i];
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame layerFrame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, animationTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    layerFrame,
                    midpointScreenX,
                    midpointScreenY,
                    actor.FacingRight);
            }
        }

        private void DrawPortableChairCoupleSharedLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            PortableChair chair = actor?.Build?.ActivePortableChair;
            if (chair?.IsCoupleChair != true
                || chair.CoupleSharedLayers == null
                || chair.CoupleSharedLayers.Count == 0)
            {
                return;
            }

            bool hasPair = TryResolvePortableChairPairWithLocalPlayer(actor, chair, localPlayer, out _, out _);
            if (!hasPair)
            {
                hasPair = FindPortableChairPairActor(
                              chair,
                              actor.CharacterId,
                              actor.FacingRight,
                              actor.Position.X,
                              actor.Position.Y,
                              skipCharacterId: actor.CharacterId,
                              preferVisibleOnly: true) != null;
            }

            if (!hasPair)
            {
                return;
            }

            PlayerCharacter.DrawPortableChairLayers(
                spriteBatch,
                skeletonMeshRenderer,
                chair.CoupleSharedLayers,
                screenX,
                screenY,
                actor.FacingRight,
                currentTime,
                drawFrontLayers);
        }

        private void DrawRelationshipOverlays(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            SpriteFont font,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int screenX,
            int screenY,
            int currentTime,
            ISet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> renderedPairs)
        {
            if (actor?.RelationshipOverlays == null || actor.RelationshipOverlays.Count == 0)
            {
                return;
            }

            foreach (RemoteRelationshipOverlayState overlay in actor.RelationshipOverlays.Values.OrderBy(static value => value.RelationshipType))
            {
                DrawRelationshipOverlay(
                    spriteBatch,
                    skeletonMeshRenderer,
                    actor,
                    overlay,
                    font,
                    localPlayer,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    screenX,
                    screenY,
                    currentTime,
                    renderedPairs);
            }
        }

        private void DrawCompletedSetItemEffect(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime,
            bool drawFrontLayers)
        {
            if (_loader == null
                || actor?.CompletedSetItemId <= 0)
            {
                return;
            }

            ItemEffectAnimationSet effect = _loader.LoadCompletedSetItemEffectAnimationSet(actor.CompletedSetItemId);
            if (effect?.OwnerLayers == null || effect.OwnerLayers.Count == 0)
            {
                return;
            }

            foreach (PortableChairLayer layer in effect.OwnerLayers)
            {
                if ((layer.RelativeZ > 0) != drawFrontLayers)
                {
                    continue;
                }

                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, currentTime);
                PlayerCharacter.DrawPortableChairLayerFrame(
                    spriteBatch,
                    skeletonMeshRenderer,
                    frame,
                    screenX,
                    screenY,
                    actor.FacingRight);
            }
        }

        private void DrawRelationshipOverlay(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            RemoteRelationshipOverlayState overlay,
            SpriteFont font,
            PlayerCharacter localPlayer,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            int screenX,
            int screenY,
            int currentTime,
            ISet<(RemoteRelationshipOverlayType Type, int ItemId, int LeftId, int RightId)> renderedPairs)
        {
            if (actor == null || overlay == null)
            {
                return;
            }

            int elapsedTime = Math.Max(0, currentTime - overlay.StartTime);
            bool usesClientRelationshipAdmission = UsesClientRelationshipAdmission(overlay.RelationshipType);
            int relationshipStatus = usesClientRelationshipAdmission
                ? 0
                : 1;
            int partnerCharacterId = 0;
            Vector2 partnerPosition = Vector2.Zero;
            bool partnerFacingRight = actor.FacingRight;
            if (TryResolveRelationshipOverlayPair(
                    actor,
                    overlay,
                    localPlayer,
                    out int resolvedPartnerCharacterId,
                    out Vector2 resolvedPartnerPosition,
                    out bool resolvedPartnerFacingRight))
            {
                partnerCharacterId = resolvedPartnerCharacterId;
                partnerPosition = resolvedPartnerPosition;
                partnerFacingRight = resolvedPartnerFacingRight;
                if (usesClientRelationshipAdmission)
                {
                    relationshipStatus = ResolveRelationshipOverlayStatus(
                        actor.Position,
                        partnerPosition,
                        overlay.RelationshipType,
                        IsRelationshipOverlaySuppressed(actor),
                        IsRelationshipOverlayPartnerSuppressed(localPlayer, partnerCharacterId, actor.CharacterId));
                }
            }
            else if (usesClientRelationshipAdmission)
            {
                relationshipStatus = 0;
            }

            if ((relationshipStatus & 1) != 0 && overlay.Effect != null)
            {
                DrawItemEffectLayers(
                    spriteBatch,
                    skeletonMeshRenderer,
                    overlay.Effect.OwnerLayers,
                    screenX,
                    screenY,
                    actor.FacingRight,
                    elapsedTime);
            }

            bool canDrawSharedRelationshipSurface = partnerCharacterId > 0 && (relationshipStatus & 2) != 0;
            bool hasSharedLayers = overlay.Effect?.SharedLayers != null && overlay.Effect.SharedLayers.Count > 0;
            if (!canDrawSharedRelationshipSurface || !hasSharedLayers)
            {
                return;
            }

            int leftId = Math.Min(actor.CharacterId, partnerCharacterId);
            int rightId = Math.Max(actor.CharacterId, partnerCharacterId);
            if (!renderedPairs.Add((overlay.RelationshipType, overlay.ItemId, leftId, rightId)))
            {
                return;
            }

            int midpointScreenX = (int)Math.Round((actor.Position.X + partnerPosition.X) * 0.5f) - mapShiftX + centerX;
            int midpointScreenY = (int)Math.Round((actor.Position.Y + partnerPosition.Y) * 0.5f) - mapShiftY + centerY
                + PlayerCharacter.PortableChairCoupleMidpointScreenYOffset;

            DrawItemEffectLayers(
                spriteBatch,
                skeletonMeshRenderer,
                overlay.Effect.SharedLayers,
                midpointScreenX,
                midpointScreenY,
                partnerFacingRight,
                elapsedTime);
        }

        private static void DrawItemEffectLayers(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            IEnumerable<PortableChairLayer> layers,
            int screenX,
            int screenY,
            bool facingRight,
            int elapsedTime)
        {
            if (layers == null)
            {
                return;
            }

            foreach (PortableChairLayer layer in layers)
            {
                CharacterFrame frame = PlayerCharacter.GetPortableChairLayerFrameAtTime(layer, elapsedTime);
                PlayerCharacter.DrawPortableChairLayerFrame(spriteBatch, skeletonMeshRenderer, frame, screenX, screenY, facingRight);
            }
        }

        public static int ResolveRelationshipOverlayStatus(
            Vector2 ownerPosition,
            Vector2 partnerPosition,
            RemoteRelationshipOverlayType relationshipType,
            bool ownerSuppressed,
            bool partnerSuppressed)
        {
            if (ownerSuppressed || partnerSuppressed)
            {
                return 0;
            }

            (int nearRangeX, int nearRangeY) = relationshipType switch
            {
                RemoteRelationshipOverlayType.NewYearCard => (NewYearCardOverlayNearRangeX, NewYearCardOverlayNearRangeY),
                _ => (RelationshipOverlayNearRangeX, RelationshipOverlayNearRangeY)
            };

            int deltaX = Math.Abs((int)Math.Round(ownerPosition.X - partnerPosition.X));
            int deltaY = Math.Abs((int)Math.Round(ownerPosition.Y - partnerPosition.Y));
            int status = 0;
            if (deltaX < RelationshipOverlayVisibleRangeX && deltaY < RelationshipOverlayVisibleRangeY)
            {
                status = 1;
            }

            if (deltaX < nearRangeX && deltaY < nearRangeY)
            {
                status |= 2;
            }

            return status;
        }

        private bool TryResolveRelationshipOverlayPair(
            RemoteUserActor ownerActor,
            RemoteRelationshipOverlayState overlay,
            PlayerCharacter localPlayer,
            out int partnerCharacterId,
            out Vector2 partnerPosition,
            out bool partnerFacingRight)
        {
            partnerCharacterId = 0;
            partnerPosition = Vector2.Zero;
            partnerFacingRight = ownerActor?.FacingRight ?? true;
            if (ownerActor == null || overlay == null)
            {
                return false;
            }

            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (overlay.PairCharacterId.HasValue
                && localCharacterId > 0
                && overlay.PairCharacterId.Value == localCharacterId
                && localPlayer?.IsAlive == true
                && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                    || !IsRelationshipOverlaySuppressed(localPlayer, localCharacterId, ownerActor.CharacterId)))
            {
                partnerCharacterId = localCharacterId;
                partnerPosition = localPlayer.Position;
                partnerFacingRight = localPlayer.FacingRight;
                return true;
            }

            if (overlay.PairCharacterId.HasValue
                && overlay.PairCharacterId.Value > 0
                && overlay.PairCharacterId.Value != ownerActor.CharacterId
                && _actorsById.TryGetValue(overlay.PairCharacterId.Value, out RemoteUserActor explicitPartner)
                && explicitPartner.IsVisibleInWorld
                && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                    || !IsRelationshipOverlaySuppressed(explicitPartner)))
            {
                partnerCharacterId = explicitPartner.CharacterId;
                partnerPosition = explicitPartner.Position;
                partnerFacingRight = explicitPartner.FacingRight;
                return true;
            }

            RemoteUserActor fallbackPartner = _actorsById.Values
                .Where(candidate => candidate.IsVisibleInWorld
                    && candidate.CharacterId != ownerActor.CharacterId
                    && (!UsesClientRelationshipAdmission(overlay.RelationshipType)
                        || !IsRelationshipOverlaySuppressed(candidate))
                    && candidate.RelationshipOverlays.TryGetValue(overlay.RelationshipType, out RemoteRelationshipOverlayState candidateOverlay)
                    && candidateOverlay?.ItemId == overlay.ItemId)
                .OrderBy(candidate =>
                {
                    candidate.RelationshipOverlays.TryGetValue(overlay.RelationshipType, out RemoteRelationshipOverlayState candidateOverlay);
                    return overlay.PairCharacterId.HasValue
                           && candidateOverlay?.PairCharacterId == ownerActor.CharacterId
                        ? 0
                        : 1;
                })
                .ThenBy(candidate => Vector2.DistanceSquared(candidate.Position, ownerActor.Position))
                .FirstOrDefault();
            if (fallbackPartner == null)
            {
                return false;
            }

            partnerCharacterId = fallbackPartner.CharacterId;
            partnerPosition = fallbackPartner.Position;
            partnerFacingRight = fallbackPartner.FacingRight;
            return true;
        }

        private static bool UsesClientRelationshipAdmission(RemoteRelationshipOverlayType relationshipType)
        {
            return relationshipType != RemoteRelationshipOverlayType.Generic;
        }

        private static bool IsRelationshipOverlaySuppressed(PlayerCharacter player, int expectedCharacterId, int ownerCharacterId)
        {
            if (player?.Build == null
                || !player.IsAlive
                || player.Build.Id <= 0
                || player.Build.Id != expectedCharacterId
                || player.Build.Id == ownerCharacterId)
            {
                return true;
            }

            return player.HasActiveMorphTransform
                || IsRelationshipOverlayGhostAction(player.CurrentActionName);
        }

        private static bool IsRelationshipOverlaySuppressed(RemoteUserActor actor)
        {
            if (actor == null || !actor.IsVisibleInWorld)
            {
                return true;
            }

            return actor.HasMorphTemplate
                || IsRelationshipOverlayGhostAction(actor.ActionName);
        }

        private bool IsRelationshipOverlayPartnerSuppressed(PlayerCharacter localPlayer, int partnerCharacterId, int ownerCharacterId)
        {
            if (partnerCharacterId <= 0 || partnerCharacterId == ownerCharacterId)
            {
                return true;
            }

            if (localPlayer?.Build?.Id == partnerCharacterId)
            {
                return IsRelationshipOverlaySuppressed(localPlayer, partnerCharacterId, ownerCharacterId);
            }

            return !_actorsById.TryGetValue(partnerCharacterId, out RemoteUserActor actor)
                   || IsRelationshipOverlaySuppressed(actor);
        }

        private static bool IsRelationshipOverlayGhostAction(string actionName)
        {
            return string.Equals(
                actionName,
                CharacterPart.GetActionString(CharacterAction.Ghost),
                StringComparison.OrdinalIgnoreCase);
        }

        private static ItemEffectAnimationSet NormalizeRelationshipOverlayEffect(
            ItemEffectAnimationSet source,
            RemoteRelationshipOverlayType relationshipType,
            bool shouldLoop)
        {
            ItemEffectAnimationSet cloned = CloneRelationshipOverlayEffect(source, shouldLoop);
            if (cloned == null)
            {
                return null;
            }

            if (relationshipType == RemoteRelationshipOverlayType.NewYearCard
                && cloned.SharedLayers.Count == 0
                && cloned.OwnerLayers.Count > 0)
            {
                cloned.SharedLayers.AddRange(cloned.OwnerLayers);
                cloned.OwnerLayers.Clear();
            }

            return cloned;
        }

        private static ItemEffectAnimationSet CloneRelationshipOverlayEffect(ItemEffectAnimationSet source, bool shouldLoop)
        {
            if (source == null)
            {
                return null;
            }

            return new ItemEffectAnimationSet
            {
                ItemId = source.ItemId,
                OwnerLayers = CloneRelationshipOverlayLayers(source.OwnerLayers, shouldLoop),
                SharedLayers = CloneRelationshipOverlayLayers(source.SharedLayers, shouldLoop)
            };
        }

        private static List<PortableChairLayer> CloneRelationshipOverlayLayers(
            IEnumerable<PortableChairLayer> layers,
            bool shouldLoop)
        {
            List<PortableChairLayer> clones = new();
            if (layers == null)
            {
                return clones;
            }

            foreach (PortableChairLayer layer in layers)
            {
                if (layer == null)
                {
                    continue;
                }

                CharacterAnimation animation = null;
                if (layer.Animation != null)
                {
                    animation = new CharacterAnimation
                    {
                        Action = layer.Animation.Action,
                        ActionName = layer.Animation.ActionName,
                        Frames = layer.Animation.Frames,
                        Loop = shouldLoop
                    };
                    animation.CalculateTotalDuration();
                }

                clones.Add(new PortableChairLayer
                {
                    Name = layer.Name,
                    Animation = animation,
                    RelativeZ = layer.RelativeZ,
                    PositionHint = layer.PositionHint
                });
            }

            return clones;
        }

        public string DescribeStatus()
        {
            if (_actorsById.Count == 0)
            {
                return "Remote user pool empty.";
            }

            return $"Remote user pool active, count={_actorsById.Count}, users={string.Join("; ", _actorsById.Values.OrderBy(static value => value.CharacterId).Select(static value => value.Describe()))}";
        }

        private void AssignDriverPassengerLink(int driverId, int passengerId)
        {
            if (!_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor))
            {
                return;
            }

            int previousPassengerId = driverActor.FollowPassengerId;
            if (previousPassengerId > 0
                && previousPassengerId != passengerId
                && _actorsById.TryGetValue(previousPassengerId, out RemoteUserActor previousPassenger)
                && previousPassenger.FollowDriverId == driverId)
            {
                previousPassenger.FollowDriverId = 0;
            }

            driverActor.FollowPassengerId = passengerId;
        }

        private void ClearDriverPassengerLink(int driverId, int passengerId)
        {
            if (driverId <= 0
                || !_actorsById.TryGetValue(driverId, out RemoteUserActor driverActor)
                || driverActor.FollowPassengerId != passengerId)
            {
                return;
            }

            driverActor.FollowPassengerId = 0;
        }

        private void ClearActorFollowLinks(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            ClearDriverPassengerLink(actor.FollowDriverId, actor.CharacterId);
            if (actor.FollowPassengerId > 0
                && _actorsById.TryGetValue(actor.FollowPassengerId, out RemoteUserActor passengerActor)
                && passengerActor.FollowDriverId == actor.CharacterId)
            {
                passengerActor.FollowDriverId = 0;
            }

            actor.FollowDriverId = 0;
            actor.FollowPassengerId = 0;
        }

        private bool TryApplyAvatarModifiedRelationshipRecord(
            RemoteUserActor actor,
            RemoteRelationshipOverlayType relationshipType,
            RemoteUserRelationshipRecord relationshipRecord,
            int currentTime,
            out string message)
        {
            if (actor == null)
            {
                message = "Remote actor is required.";
                return false;
            }

            return TrySetItemEffect(
                actor.CharacterId,
                relationshipType,
                relationshipRecord.IsActive ? relationshipRecord.ItemId : null,
                relationshipRecord.IsActive ? relationshipRecord.PairCharacterId : null,
                currentTime,
                out message);
        }

        private static int? ResolveMarriagePairCharacterId(int ownerCharacterId, RemoteUserRelationshipRecord relationshipRecord)
        {
            if (!relationshipRecord.IsActive)
            {
                return null;
            }

            if (relationshipRecord.CharacterId.HasValue
                && relationshipRecord.CharacterId.Value > 0
                && relationshipRecord.CharacterId.Value != ownerCharacterId)
            {
                return relationshipRecord.CharacterId.Value;
            }

            if (relationshipRecord.PairCharacterId.HasValue
                && relationshipRecord.PairCharacterId.Value > 0
                && relationshipRecord.PairCharacterId.Value != ownerCharacterId)
            {
                return relationshipRecord.PairCharacterId.Value;
            }

            return relationshipRecord.PairCharacterId;
        }

        private static void DrawOutlinedText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, Color shadowColor, Color textColor)
        {
            Vector2[] offsets =
            {
                new Vector2(-1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, -1f),
                new Vector2(0f, 1f)
            };

            foreach (Vector2 offset in offsets)
            {
                spriteBatch.DrawString(font, text, position + offset, shadowColor);
            }

            spriteBatch.DrawString(font, text, position, textColor);
        }

        private static Color ResolveNameColor(RemoteUserActor actor)
        {
            return actor.BattlefieldTeamId switch
            {
                0 => new Color(255, 232, 170),
                1 => new Color(255, 189, 189),
                2 => new Color(185, 229, 255),
                _ => Color.White
            };
        }

        internal static IReadOnlyDictionary<int, int> ResolvePortableChairPairings(
            IEnumerable<PortableChairPairParticipant> participants,
            bool preferVisibleOnly)
        {
            List<PortableChairPairParticipant> resolvedParticipants = participants?
                .Where(static participant => participant.CharacterId > 0 && participant.Chair?.IsCoupleChair == true)
                .OrderBy(static participant => participant.CharacterId)
                .ToList()
                ?? new List<PortableChairPairParticipant>();
            if (resolvedParticipants.Count < 2)
            {
                return new Dictionary<int, int>();
            }

            List<PortableChairPairCandidate> candidates = new();
            for (int i = 0; i < resolvedParticipants.Count - 1; i++)
            {
                PortableChairPairParticipant left = resolvedParticipants[i];
                for (int j = i + 1; j < resolvedParticipants.Count; j++)
                {
                    PortableChairPairParticipant right = resolvedParticipants[j];
                    if (!TryBuildPortableChairPairCandidate(left, right, preferVisibleOnly, out PortableChairPairCandidate candidate))
                    {
                        continue;
                    }

                    candidates.Add(candidate);
                }
            }

            Dictionary<int, int> pairs = new();
            foreach (PortableChairPairCandidate candidate in candidates
                .OrderBy(static candidate => candidate.Priority)
                .ThenBy(static candidate => candidate.Score)
                .ThenBy(static candidate => candidate.Left.CharacterId)
                .ThenBy(static candidate => candidate.Right.CharacterId))
            {
                if (pairs.ContainsKey(candidate.Left.CharacterId)
                    || pairs.ContainsKey(candidate.Right.CharacterId))
                {
                    continue;
                }

                pairs[candidate.Left.CharacterId] = candidate.Right.CharacterId;
                pairs[candidate.Right.CharacterId] = candidate.Left.CharacterId;
            }

            return pairs;
        }

        private Dictionary<int, int> BuildPortableChairPairMap(PlayerCharacter localPlayer, bool preferVisibleOnly)
        {
            List<PortableChairPairParticipant> participants = new(_actorsById.Count + 1);
            foreach (RemoteUserActor actor in _actorsById.Values)
            {
                PortableChair chair = actor?.Build?.ActivePortableChair;
                if (chair?.IsCoupleChair != true)
                {
                    continue;
                }

                participants.Add(new PortableChairPairParticipant(
                    actor.CharacterId,
                    chair,
                    actor.Position,
                    actor.FacingRight,
                    actor.PreferredPortableChairPairCharacterId,
                    actor.IsVisibleInWorld,
                    IsRelationshipOverlaySuppressed(actor)));
            }

            PortableChair localChair = localPlayer?.Build?.ActivePortableChair;
            int localCharacterId = localPlayer?.Build?.Id ?? 0;
            if (localChair?.IsCoupleChair == true
                && localPlayer.IsAlive
                && localCharacterId > 0)
            {
                participants.Add(new PortableChairPairParticipant(
                    localCharacterId,
                    localChair,
                    localPlayer.Position,
                    localPlayer.FacingRight,
                    null,
                    true,
                    IsRelationshipOverlaySuppressed(localPlayer, localCharacterId, ownerCharacterId: 0)));
            }

            return new Dictionary<int, int>(ResolvePortableChairPairings(participants, preferVisibleOnly));
        }

        private static bool TryBuildPortableChairPairCandidate(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right,
            bool preferVisibleOnly,
            out PortableChairPairCandidate candidate)
        {
            candidate = default;
            if (left.CharacterId == right.CharacterId
                || left.Chair?.IsCoupleChair != true
                || right.Chair?.IsCoupleChair != true
                || left.Chair.ItemId <= 0
                || left.Chair.ItemId != right.Chair.ItemId
                || (preferVisibleOnly && (!left.IsVisibleInWorld || !right.IsVisibleInWorld))
                || left.IsRelationshipOverlaySuppressed
                || right.IsRelationshipOverlaySuppressed
                || !PlayerCharacter.IsPortableChairActualPairActive(
                    left.Chair,
                    left.FacingRight,
                    left.Position.X,
                    left.Position.Y,
                    right.FacingRight,
                    right.Position.X,
                    right.Position.Y))
            {
                return false;
            }

            int priority = ResolvePortableChairPairPriority(left, right);
            float score = ComputePortableChairPairScore(left, right);
            candidate = new PortableChairPairCandidate(left, right, priority, score);
            return true;
        }

        private static int ResolvePortableChairPairPriority(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right)
        {
            bool leftPrefersRight = left.PreferredPairCharacterId == right.CharacterId;
            bool rightPrefersLeft = right.PreferredPairCharacterId == left.CharacterId;
            return (leftPrefersRight, rightPrefersLeft) switch
            {
                (true, true) => 0,
                (true, false) => 1,
                (false, true) => 1,
                _ => 2
            };
        }

        private static float ComputePortableChairPairScore(
            PortableChairPairParticipant left,
            PortableChairPairParticipant right)
        {
            Point expectedRightOffset = PlayerCharacter.ResolvePortableChairPairOffset(left.Chair, left.FacingRight);
            Point expectedLeftOffset = PlayerCharacter.ResolvePortableChairPairOffset(right.Chair, right.FacingRight);
            Vector2 expectedRightPosition = left.Position + new Vector2(expectedRightOffset.X, expectedRightOffset.Y);
            Vector2 expectedLeftPosition = right.Position + new Vector2(expectedLeftOffset.X, expectedLeftOffset.Y);
            return Vector2.DistanceSquared(expectedRightPosition, right.Position)
                + Vector2.DistanceSquared(expectedLeftPosition, left.Position);
        }

        private bool TryResolvePortableChairPairWithLocalPlayer(
            RemoteUserActor actor,
            PortableChair chair,
            PlayerCharacter localPlayer,
            out Vector2 partnerPosition,
            out bool partnerFacingRight)
        {
            partnerPosition = Vector2.Zero;
            partnerFacingRight = false;
            if (actor == null
                || chair?.IsCoupleChair != true
                || localPlayer?.Build == null
                || !localPlayer.IsAlive)
            {
                return false;
            }

            Dictionary<int, int> pairMap = BuildPortableChairPairMap(localPlayer, preferVisibleOnly: true);
            int localCharacterId = localPlayer.Build.Id;
            if (!pairMap.TryGetValue(actor.CharacterId, out int pairCharacterId)
                || pairCharacterId != localCharacterId)
            {
                return false;
            }

            partnerPosition = localPlayer.Position;
            partnerFacingRight = localPlayer.FacingRight;
            return true;
        }

        private bool TryResolvePortableChairOwnerForLocalPlayer(PlayerCharacter localPlayer, out RemoteUserActor ownerActor)
        {
            return TryResolvePortableChairOwnerForLocalPlayer(
                localPlayer,
                BuildPortableChairPairMap(localPlayer, preferVisibleOnly: true),
                out ownerActor);
        }

        private bool TryResolvePortableChairOwnerForLocalPlayer(
            PlayerCharacter localPlayer,
            IReadOnlyDictionary<int, int> pairMap,
            out RemoteUserActor ownerActor)
        {
            ownerActor = null;
            if (localPlayer?.Build == null || !localPlayer.IsAlive)
            {
                return false;
            }

            int localCharacterId = localPlayer.Build.Id;
            if (localCharacterId <= 0)
            {
                return false;
            }

            return pairMap != null
                   && pairMap.TryGetValue(localCharacterId, out int ownerCharacterId)
                   && ownerCharacterId != localCharacterId
                   && _actorsById.TryGetValue(ownerCharacterId, out ownerActor)
                   && ownerActor.IsVisibleInWorld;
        }

        private RemoteUserActor FindPortableChairPairActor(
            PortableChair chair,
            int ownerCharacterId,
            bool ownerFacingRight,
            float ownerX,
            float ownerY,
            int skipCharacterId,
            bool preferVisibleOnly)
        {
            if (chair?.IsCoupleChair != true)
            {
                return null;
            }

            Dictionary<int, int> pairMap = BuildPortableChairPairMap(localPlayer: null, preferVisibleOnly);
            return pairMap.TryGetValue(ownerCharacterId, out int pairCharacterId)
                   && pairCharacterId != skipCharacterId
                   && _actorsById.TryGetValue(pairCharacterId, out RemoteUserActor pairActor)
                ? pairActor
                : null;
        }

        private void SyncBattlefieldAppearance(RemoteUserActor actor, BattlefieldField battlefield)
        {
            if (actor?.Build == null)
            {
                return;
            }

            int? assignedTeamId = battlefield != null
                && battlefield.TryGetAssignedTeamId(actor.CharacterId, out int resolvedTeamId)
                ? resolvedTeamId
                : actor.BattlefieldTeamId;
            if (battlefield?.IsActive != true)
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                return;
            }

            if (actor.BattlefieldAppliedTeamId == assignedTeamId
                && (!assignedTeamId.HasValue || actor.BattlefieldOriginalEquipment != null))
            {
                return;
            }

            if (!assignedTeamId.HasValue
                || !battlefield.TryGetAssignedTeamLookPreset(actor.CharacterId, out BattlefieldField.BattlefieldTeamLookPreset preset))
            {
                RestoreBattlefieldAppearance(actor, clearTeamId: false);
                actor.BattlefieldAppliedTeamId = assignedTeamId;
                actor.BattlefieldTeamId = assignedTeamId;
                return;
            }

            EnsureBattlefieldOriginalAppearanceSnapshot(actor);
            if (actor.BattlefieldOriginalSpeed == null)
            {
                actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Longcoat))
            {
                actor.Build.Unequip(EquipSlot.Coat);
                actor.Build.Unequip(EquipSlot.Pants);
            }
            else if (preset.EquipmentItemIds.ContainsKey(EquipSlot.Coat))
            {
                actor.Build.Unequip(EquipSlot.Longcoat);
            }

            foreach (KeyValuePair<EquipSlot, int> entry in preset.EquipmentItemIds)
            {
                CharacterPart part = _loader?.LoadEquipment(entry.Value);
                if (part != null)
                {
                    actor.Build.Equip(part);
                }
            }

            if (preset.MoveSpeed.HasValue)
            {
                actor.Build.Speed = preset.MoveSpeed.Value;
            }

            actor.RefreshAssembler();
            actor.BattlefieldAppliedTeamId = assignedTeamId;
            actor.BattlefieldTeamId = assignedTeamId;
        }

        private static void EnsureBattlefieldOriginalAppearanceSnapshot(RemoteUserActor actor)
        {
            if (actor.BattlefieldOriginalEquipment != null || actor?.Build == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = new Dictionary<EquipSlot, CharacterPart>();
            actor.BattlefieldOriginalSpeed = actor.Build.Speed;
            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                if (actor.Build.Equipment.TryGetValue(slot, out CharacterPart part) && part != null)
                {
                    actor.BattlefieldOriginalEquipment[slot] = part;
                }
            }
        }

        private static void RestoreBattlefieldAppearance(RemoteUserActor actor, bool clearTeamId)
        {
            if (actor?.Build == null)
            {
                return;
            }

            if (actor.BattlefieldOriginalEquipment == null)
            {
                actor.BattlefieldAppliedTeamId = null;
                if (clearTeamId)
                {
                    actor.BattlefieldTeamId = null;
                }
                return;
            }

            foreach (EquipSlot slot in BattlefieldAppearanceSlots)
            {
                actor.Build.Unequip(slot);
            }

            foreach (KeyValuePair<EquipSlot, CharacterPart> entry in actor.BattlefieldOriginalEquipment)
            {
                actor.Build.Equip(entry.Value);
            }

            if (actor.BattlefieldOriginalSpeed.HasValue)
            {
                actor.Build.Speed = actor.BattlefieldOriginalSpeed.Value;
            }

            actor.RefreshAssembler();
            ResetBattlefieldAppearanceState(actor);
            if (clearTeamId)
            {
                actor.BattlefieldTeamId = null;
            }
        }

        private static void ResetBattlefieldAppearanceState(RemoteUserActor actor)
        {
            if (actor == null)
            {
                return;
            }

            actor.BattlefieldOriginalEquipment = null;
            actor.BattlefieldOriginalSpeed = null;
            actor.BattlefieldAppliedTeamId = null;
        }

        private static string NormalizeActionName(string actionName, bool allowSitFallback)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return allowSitFallback
                    ? "sit"
                    : CharacterPart.GetActionString(CharacterAction.Stand1);
            }

            return actionName.Trim();
        }

        private void RegisterMeleeAfterImage(
            RemoteUserActor actor,
            int skillId,
            string actionName,
            int currentTime,
            int masteryPercent,
            int chargeElement,
            int? rawActionCode = null)
        {
            if (_skillLoader == null
                || actor?.Build == null
                || actor.Assembler == null)
            {
                actor?.ClearMeleeAfterImage();
                return;
            }

            WeaponPart weapon = actor.Build.GetWeapon();
            SkillData skill = skillId > 0 ? _skillLoader.LoadSkill(skillId) : null;
            if (!_skillLoader.TryResolveMeleeAfterImageAction(
                    skill,
                    weapon,
                    EnumerateRemoteAfterImageActionNames(actor, skill, actionName),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    out MeleeAfterImageAction afterImageAction))
            {
                afterImageAction = _skillLoader.ApplyClientMeleeRangeOverride(null, skillId, rawActionCode, actor.FacingRight);
                if (afterImageAction != null)
                {
                    actor.ApplyMeleeAfterImage(
                        skillId,
                        actionName,
                        afterImageAction,
                        currentTime,
                        actor.FacingRight,
                        GetActionDuration(actor.Assembler, actionName),
                        GetAfterImageFadeDuration(actor.Assembler, actionName));
                    return;
                }

                if (actor.MeleeAfterImage?.FadeStartTime < 0)
                {
                    actor.ClearMeleeAfterImage();
                }

                return;
            }

            afterImageAction = _skillLoader.ApplyClientMeleeRangeOverride(afterImageAction, skillId, rawActionCode, actor.FacingRight);

            actor.ApplyMeleeAfterImage(
                skillId,
                actionName,
                afterImageAction,
                currentTime,
                actor.FacingRight,
                GetActionDuration(actor.Assembler, actionName),
                GetAfterImageFadeDuration(actor.Assembler, actionName));
        }

        private string ResolveRemoteMeleeActionName(
            RemoteUserActor actor,
            SkillData skill,
            string actionName,
            int? rawActionCode,
            int masteryPercent,
            int chargeElement)
        {
            if (actor?.Build == null)
            {
                return actionName;
            }

            WeaponPart weapon = actor.Build.GetWeapon();
            foreach (string candidate in EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode))
            {
                if (_skillLoader != null
                    && _skillLoader.TryResolveMeleeAfterImageAction(
                        skill,
                        weapon,
                        candidate,
                        actor.Build.Level,
                        masteryPercent,
                        chargeElement,
                        out _,
                        out string matchedAfterImageActionName))
                {
                    string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, candidate, matchedAfterImageActionName);
                    if (!string.IsNullOrWhiteSpace(preferredActionName))
                    {
                        return preferredActionName;
                    }
                }

                if (actor.Assembler?.GetAnimation(candidate)?.Length > 0)
                {
                    return candidate;
                }
            }

            if (_skillLoader != null
                && actor?.Build != null
                && _skillLoader.TryResolveUniqueMeleeAfterImageActionName(
                    skill,
                    actor.Build.GetWeapon(),
                    EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    out string uniqueAfterImageActionName))
            {
                string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, actionName, uniqueAfterImageActionName);
                if (!string.IsNullOrWhiteSpace(preferredActionName))
                {
                    return preferredActionName;
                }
            }

            if (_skillLoader != null
                && actor?.Build != null
                && _skillLoader.TryResolveRenderableMeleeAfterImageActionName(
                    skill,
                    actor.Build.GetWeapon(),
                    EnumerateRemoteActionResolutionCandidates(actor, skill, actionName, rawActionCode),
                    actor.Build.Level,
                    masteryPercent,
                    chargeElement,
                    candidate => actor.Assembler?.GetAnimation(candidate)?.Length > 0,
                    out string renderableAfterImageActionName))
            {
                string preferredActionName = ResolvePreferredRemoteMeleeActionName(actor, actionName, renderableAfterImageActionName);
                if (!string.IsNullOrWhiteSpace(preferredActionName))
                {
                    return preferredActionName;
                }
            }

            return actionName;
        }

        private static string ResolvePreferredRemoteMeleeActionName(
            RemoteUserActor actor,
            string requestedActionName,
            string matchedAfterImageActionName)
        {
            string normalizedRequestedActionName = string.IsNullOrWhiteSpace(requestedActionName)
                ? null
                : requestedActionName.Trim();
            string normalizedMatchedActionName = string.IsNullOrWhiteSpace(matchedAfterImageActionName)
                ? null
                : matchedAfterImageActionName.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedMatchedActionName)
                && actor?.Assembler?.GetAnimation(normalizedMatchedActionName)?.Length > 0)
            {
                return normalizedMatchedActionName;
            }

            if (!string.IsNullOrWhiteSpace(normalizedRequestedActionName)
                && actor?.Assembler?.GetAnimation(normalizedRequestedActionName)?.Length > 0)
            {
                return normalizedRequestedActionName;
            }

            return normalizedMatchedActionName ?? normalizedRequestedActionName;
        }

        private static IEnumerable<string> EnumerateRemoteAfterImageActionNames(
            RemoteUserActor actor,
            SkillData skill,
            string actionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName.Trim()))
            {
                yield return actionName.Trim();
            }

            if (skill?.ActionNames != null)
            {
                foreach (string skillActionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(skillActionName) && yielded.Add(skillActionName.Trim()))
                    {
                        yield return skillActionName.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName.Trim()))
            {
                yield return skill.ActionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(actor?.ActionName) && yielded.Add(actor.ActionName.Trim()))
            {
                yield return actor.ActionName.Trim();
            }
        }

        private static IEnumerable<string> EnumerateRemoteActionResolutionCandidates(
            RemoteUserActor actor,
            SkillData skill,
            string actionName,
            int? rawActionCode)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName.Trim()))
            {
                yield return actionName.Trim();
            }

            if (rawActionCode.HasValue
                && CharacterPart.TryGetActionStringFromCode(rawActionCode.Value, out string resolvedCodeActionName)
                && !string.IsNullOrWhiteSpace(resolvedCodeActionName)
                && yielded.Add(resolvedCodeActionName))
            {
                yield return resolvedCodeActionName;
            }

            if (skill?.ActionNames != null)
            {
                foreach (string skillActionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(skillActionName) && yielded.Add(skillActionName.Trim()))
                    {
                        yield return skillActionName.Trim();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName) && yielded.Add(skill.ActionName.Trim()))
            {
                yield return skill.ActionName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(actor?.ActionName) && yielded.Add(actor.ActionName.Trim()))
            {
                yield return actor.ActionName.Trim();
            }
        }

        private static int GetActionDuration(CharacterAssembler assembler, string actionName)
        {
            AssembledFrame[] animation = assembler?.GetAnimation(actionName);
            if (animation == null || animation.Length == 0)
            {
                return 0;
            }

            int duration = 0;
            foreach (AssembledFrame frame in animation)
            {
                duration += Math.Max(0, frame?.Duration ?? 0);
            }

            return duration;
        }

        private static int GetAfterImageFadeDuration(CharacterAssembler assembler, string actionName)
        {
            return Math.Max(MinimumMeleeAfterImageFadeDurationMs, GetActionDuration(assembler, actionName) / 4);
        }

        private static void DrawMeleeAfterImage(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            RemoteUserActor actor,
            int screenX,
            int screenY,
            int currentTime)
        {
            if (actor?.Assembler == null || actor.MeleeAfterImage?.AfterImageAction?.FrameSets == null)
            {
                return;
            }

            RemoteMeleeAfterImageState state = actor.MeleeAfterImage;
            bool activeAction = state.FadeStartTime < 0
                && string.Equals(actor.ActionName, state.ActionName, StringComparison.OrdinalIgnoreCase);
            int frameIndex = state.LastFrameIndex;
            float alpha = 1f;

            if (activeAction)
            {
                int animationTime = Math.Max(0, currentTime - state.AnimationStartTime);
                frameIndex = actor.Assembler.GetFrameIndexAtTime(state.ActionName, animationTime);
                if (frameIndex >= 0)
                {
                    state.LastFrameIndex = frameIndex;
                }
            }
            else if (state.FadeStartTime >= 0)
            {
                int fadeElapsed = Math.Max(0, currentTime - state.FadeStartTime);
                if (fadeElapsed >= state.FadeDuration)
                {
                    actor.ClearMeleeAfterImage();
                    return;
                }

                alpha = 1f - (fadeElapsed / (float)Math.Max(1, state.FadeDuration));
            }

            if (frameIndex < 0
                || !state.AfterImageAction.FrameSets.TryGetValue(frameIndex, out MeleeAfterImageFrameSet frameSet)
                || frameSet?.Frames == null)
            {
                return;
            }

            Color tint = Color.White * MathHelper.Clamp(alpha, 0f, 1f);
            foreach (SkillFrame frame in frameSet.Frames)
            {
                if (frame?.Texture == null)
                {
                    continue;
                }

                bool shouldFlip = state.FacingRight ^ frame.Flip;
                int drawX = shouldFlip
                    ? screenX - (frame.Texture.Width - frame.Origin.X)
                    : screenX - frame.Origin.X;
                int drawY = screenY - frame.Origin.Y;
                frame.Texture.DrawBackground(spriteBatch, skeletonMeshRenderer, null, drawX, drawY, tint, shouldFlip, null);
            }
        }

        private void ApplyMovementSnapshot(RemoteUserActor actor, int currentTime)
        {
            string previousActionName = actor.ActionName;
            PassivePositionSnapshot sampled = actor.MovementSnapshot.SampleAtTime(currentTime);
            actor.Position = new Vector2(sampled.X, sampled.Y);
            actor.FacingRight = sampled.FacingRight;
            actor.CurrentFootholdId = sampled.FootholdId;

            if (actor.MovementDrivenActionSelection)
            {
                actor.ActionName = ResolveActionName(actor, sampled.Action);
                if (!string.Equals(previousActionName, actor.ActionName, StringComparison.OrdinalIgnoreCase))
                {
                    actor.BeginMeleeAfterImageFade(currentTime);
                    RegisterMeleeAfterImage(actor, 0, actor.ActionName, currentTime, 10, 0);
                }
            }
        }

        private void ApplyFollowDriverState(RemoteUserActor actor, PlayerCharacter localPlayer)
        {
            if (actor == null || actor.FollowDriverId <= 0)
            {
                return;
            }

            if (!TryResolveFollowDriverWorldState(actor, localPlayer, out Vector2 position, out bool facingRight))
            {
                return;
            }

            actor.Position = position;
            actor.FacingRight = facingRight;
        }

        private bool TryResolveFollowDriverWorldState(
            RemoteUserActor actor,
            PlayerCharacter localPlayer,
            out Vector2 position,
            out bool facingRight)
        {
            position = default;
            facingRight = actor?.FacingRight ?? true;
            if (actor == null || actor.FollowDriverId <= 0)
            {
                return false;
            }

            if (TryResolveLocalFollowDriverState(actor.FollowDriverId, localPlayer, out position, out facingRight))
            {
                return true;
            }

            if (!_actorsById.TryGetValue(actor.FollowDriverId, out RemoteUserActor driverActor))
            {
                return false;
            }

            return TryResolveFollowDriverOffsetPosition(
                driverActor.Position,
                driverActor.FacingRight,
                driverActor.ActionName,
                out position,
                out facingRight);
        }

        private static bool TryResolveLocalFollowDriverState(
            int driverCharacterId,
            PlayerCharacter localPlayer,
            out Vector2 position,
            out bool facingRight)
        {
            position = default;
            facingRight = localPlayer?.FacingRight ?? true;
            if (localPlayer?.Build?.Id != driverCharacterId)
            {
                return false;
            }

            return TryResolveFollowDriverOffsetPosition(
                localPlayer.Position,
                localPlayer.FacingRight,
                localPlayer.CurrentActionName,
                out position,
                out facingRight);
        }

        internal static bool TryResolveFollowDriverOffsetPosition(
            Vector2 driverPosition,
            bool driverFacingRight,
            string driverActionName,
            out Vector2 followerPosition,
            out bool followerFacingRight)
        {
            followerFacingRight = driverFacingRight;
            if (IsFollowDriverLadderOrRopeAction(driverActionName))
            {
                followerPosition = new Vector2(
                    driverPosition.X,
                    driverPosition.Y + FollowDriverLadderRopeVerticalOffset);
                return true;
            }

            followerPosition = new Vector2(
                driverPosition.X + (driverFacingRight ? FollowDriverGroundHorizontalOffset : -FollowDriverGroundHorizontalOffset),
                driverPosition.Y);
            return true;
        }

        private static bool IsFollowDriverLadderOrRopeAction(string actionName)
        {
            return string.Equals(actionName, "ladder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "rope", StringComparison.OrdinalIgnoreCase);
        }

        private static MoveAction MoveActionFromRaw(byte moveAction)
        {
            int normalized = (moveAction >> 1) & 0x0F;
            return Enum.IsDefined(typeof(MoveAction), normalized)
                ? (MoveAction)normalized
                : MoveAction.Stand;
        }

        private static bool DecodeFacingRight(byte moveAction)
        {
            return (moveAction & 1) == 0;
        }

        private static string ResolveActionName(RemoteUserActor actor, MoveAction moveAction)
        {
            bool hasPortableChair = actor?.Build?.ActivePortableChair != null;
            return moveAction switch
            {
                MoveAction.Walk => "walk1",
                MoveAction.Jump or MoveAction.Fall => "jump",
                MoveAction.Ladder => "ladder",
                MoveAction.Rope => "rope",
                MoveAction.Swim => "swim",
                MoveAction.Fly => "fly",
                MoveAction.Attack => string.IsNullOrWhiteSpace(actor?.ActionName) ? "alert" : actor.ActionName,
                MoveAction.Hit => "alert",
                MoveAction.Die => "dead",
                _ => hasPortableChair ? "sit" : CharacterPart.GetActionString(CharacterAction.Stand1)
            };
        }

        private void UpdateNameLookup(string previousName, string currentName, int characterId)
        {
            if (!string.IsNullOrWhiteSpace(previousName)
                && !string.Equals(previousName, currentName, StringComparison.OrdinalIgnoreCase))
            {
                _actorIdsByName.Remove(previousName);
            }

            _actorIdsByName[currentName] = characterId;
        }

        private void ApplyPortableChairMount(RemoteUserActor actor, PortableChair chair)
        {
            if (_loader == null
                || actor?.Build == null
                || chair?.TamingMobItemId is not int tamingMobItemId
                || tamingMobItemId <= 0)
            {
                return;
            }

            CharacterPart mountPart = _loader.LoadEquipment(tamingMobItemId);
            if (mountPart?.Slot != EquipSlot.TamingMob)
            {
                return;
            }

            actor.Build.Equipment.TryGetValue(EquipSlot.TamingMob, out CharacterPart previousMount);
            actor.PortableChairPreviousMount = previousMount;
            actor.PortableChairAppliedMount = true;
            actor.Build.Equip(mountPart);
        }

        private static void ClearPortableChairMountState(RemoteUserActor actor)
        {
            if (actor?.Build == null || !actor.PortableChairAppliedMount)
            {
                return;
            }

            if (actor.PortableChairPreviousMount != null)
            {
                actor.Build.Equip(actor.PortableChairPreviousMount);
            }
            else
            {
                actor.Build.Unequip(EquipSlot.TamingMob);
            }

            actor.PortableChairPreviousMount = null;
            actor.PortableChairAppliedMount = false;
        }
    }

    public sealed class RemoteUserActor
    {
        public RemoteUserActor(
            int characterId,
            string name,
            CharacterBuild build,
            Vector2 position,
            bool facingRight,
            string actionName,
            string sourceTag,
            bool isVisibleInWorld)
        {
            CharacterId = characterId;
            Name = name;
            Build = build;
            Position = position;
            FacingRight = facingRight;
            ActionName = actionName;
            SourceTag = sourceTag;
            IsVisibleInWorld = isVisibleInWorld;
            RefreshAssembler();
        }

        public int CharacterId { get; }
        public string Name { get; set; }
        public CharacterBuild Build { get; set; }
        public CharacterAssembler Assembler { get; private set; }
        public Vector2 Position { get; set; }
        public bool FacingRight { get; set; }
        public string ActionName { get; set; }
        public string SourceTag { get; set; }
        public bool IsVisibleInWorld { get; set; }
        public MinimapUI.HelperMarkerType? HelperMarkerType { get; set; }
        public bool ShowDirectionOverlay { get; set; } = true;
        public int? BattlefieldTeamId { get; set; }
        public RemotePreparedSkillState PreparedSkill { get; set; }
        public CharacterPart PortableChairPreviousMount { get; set; }
        public bool PortableChairAppliedMount { get; set; }
        public int? PreferredPortableChairPairCharacterId { get; set; }
        public Dictionary<RemoteRelationshipOverlayType, RemoteRelationshipOverlayState> RelationshipOverlays { get; } = new();
        public RemoteUserTemporaryStatSnapshot TemporaryStats { get; set; }
        public ushort TemporaryStatDelay { get; set; }
        public PlayerMovementSyncSnapshot MovementSnapshot { get; set; }
        public byte LastMoveActionRaw { get; set; }
        public int CurrentFootholdId { get; set; }
        public bool MovementDrivenActionSelection { get; set; }
        public bool HasMorphTemplate { get; set; }
        public int? CarryItemEffectId { get; set; }
        public int CompletedSetItemId { get; set; }
        public Dictionary<EquipSlot, CharacterPart> BattlefieldOriginalEquipment { get; set; }
        public float? BattlefieldOriginalSpeed { get; set; }
        public int? BattlefieldAppliedTeamId { get; set; }
        public RemoteMeleeAfterImageState MeleeAfterImage { get; private set; }
        public PacketOwnedUserSummonRegistry PacketOwnedSummons { get; } = new();
        public int FollowDriverId { get; set; }
        public int FollowPassengerId { get; set; }

        public void RefreshAssembler()
        {
            Assembler = new CharacterAssembler(Build);
        }

        public void ApplyMeleeAfterImage(
            int skillId,
            string actionName,
            MeleeAfterImageAction afterImageAction,
            int currentTime,
            bool facingRight,
            int actionDuration,
            int fadeDuration)
        {
            if (afterImageAction == null
                || string.IsNullOrWhiteSpace(actionName)
                || ((afterImageAction.FrameSets == null || afterImageAction.FrameSets.Count == 0) && !afterImageAction.HasRange))
            {
                MeleeAfterImage = null;
                return;
            }

            MeleeAfterImage = new RemoteMeleeAfterImageState
            {
                SkillId = skillId,
                ActionName = actionName,
                AfterImageAction = afterImageAction,
                AnimationStartTime = currentTime,
                FacingRight = facingRight,
                ActionDuration = actionDuration,
                FadeDuration = Math.Max(60, fadeDuration)
            };
        }

        public void BeginMeleeAfterImageFade(int currentTime)
        {
            if (MeleeAfterImage == null || MeleeAfterImage.FadeStartTime >= 0)
            {
                return;
            }

            if (Assembler != null && !string.IsNullOrWhiteSpace(MeleeAfterImage.ActionName))
            {
                int animationTime = Math.Max(0, currentTime - MeleeAfterImage.AnimationStartTime);
                int frameIndex = Assembler.GetFrameIndexAtTime(MeleeAfterImage.ActionName, animationTime);
                if (frameIndex >= 0)
                {
                    MeleeAfterImage.LastFrameIndex = frameIndex;
                }
            }

            MeleeAfterImage.FadeStartTime = currentTime;
        }

        public void UpdateMeleeAfterImage(int currentTime)
        {
            if (MeleeAfterImage == null)
            {
                return;
            }

            if (MeleeAfterImage.FadeStartTime >= 0)
            {
                if (currentTime - MeleeAfterImage.FadeStartTime >= MeleeAfterImage.FadeDuration)
                {
                    MeleeAfterImage = null;
                }

                return;
            }

            if (!string.Equals(ActionName, MeleeAfterImage.ActionName, StringComparison.OrdinalIgnoreCase))
            {
                BeginMeleeAfterImageFade(currentTime);
                return;
            }

            if (MeleeAfterImage.ActionDuration > 0
                && currentTime - MeleeAfterImage.AnimationStartTime >= MeleeAfterImage.ActionDuration)
            {
                BeginMeleeAfterImageFade(currentTime);
            }
        }

        public void ClearMeleeAfterImage()
        {
            MeleeAfterImage = null;
        }

        public string Describe()
        {
            string helperText = HelperMarkerType?.ToString() ?? "none";
            string teamText = BattlefieldTeamId?.ToString() ?? "none";
            string preparedText = PreparedSkill != null ? PreparedSkill.SkillId.ToString() : "none";
            string chairPairText = PreferredPortableChairPairCharacterId?.ToString() ?? "none";
            string itemEffectText = RelationshipOverlays.Count > 0
                ? string.Join(
                    ",",
                    RelationshipOverlays
                        .OrderBy(static entry => entry.Key)
                        .Select(static entry =>
                            $"{entry.Key}:{entry.Value.ItemId}:{entry.Value.PairCharacterId?.ToString() ?? "none"}"))
                : "none";
            string carryItemEffectText = CarryItemEffectId?.ToString() ?? "none";
            string setItemText = CompletedSetItemId > 0 ? CompletedSetItemId.ToString() : "none";
            string followDriverText = FollowDriverId > 0 ? FollowDriverId.ToString() : "none";
            string followPassengerText = FollowPassengerId > 0 ? FollowPassengerId.ToString() : "none";
            return $"{CharacterId}:{Name}@({Position.X:0},{Position.Y:0}) action={ActionName} source={SourceTag} helper={helperText} team={teamText} prep={preparedText} chairPair={chairPairText} itemEffect={itemEffectText} carry={carryItemEffectText} setItem={setItemText} followDriver={followDriverText} followPassenger={followPassengerText}";
        }
    }

    public sealed class RemoteMeleeAfterImageState
    {
        public int SkillId { get; init; }
        public string ActionName { get; init; }
        public MeleeAfterImageAction AfterImageAction { get; init; }
        public int AnimationStartTime { get; set; }
        public bool FacingRight { get; init; }
        public int ActionDuration { get; init; }
        public int FadeDuration { get; init; }
        public int FadeStartTime { get; set; } = -1;
        public int LastFrameIndex { get; set; } = -1;
    }

    public sealed class RemotePreparedSkillState
    {
        public int SkillId { get; init; }
        public string SkillName { get; init; }
        public string SkinKey { get; init; } = "KeyDownBar";
        public int DurationMs { get; init; }
        public int PrepareDurationMs { get; init; }
        public int GaugeDurationMs { get; init; }
        public int StartTime { get; init; }
        public bool IsKeydownSkill { get; init; }
        public bool IsHolding { get; init; }
        public bool AutoEnterHold { get; init; }
        public int MaxHoldDurationMs { get; init; }
        public PreparedSkillHudTextVariant TextVariant { get; init; }
        public bool ShowText { get; init; } = true;
    }

    public sealed class RemoteRelationshipOverlayState
    {
        public RemoteRelationshipOverlayType RelationshipType { get; init; }
        public int ItemId { get; init; }
        public int? PairCharacterId { get; init; }
        public ItemEffectAnimationSet Effect { get; init; }
        public int StartTime { get; init; }
    }

    internal readonly struct RemoteDragonHudMetadata
    {
        public RemoteDragonHudMetadata(int standOriginX, IReadOnlyDictionary<string, int> actionHeights)
        {
            StandOriginX = standOriginX;
            ActionHeights = actionHeights ?? throw new ArgumentNullException(nameof(actionHeights));
        }

        public int StandOriginX { get; }
        public IReadOnlyDictionary<string, int> ActionHeights { get; }

        public int ResolveFrameHeight(string actionName)
        {
            if (!string.IsNullOrWhiteSpace(actionName)
                && ActionHeights.TryGetValue(actionName, out int actionHeight)
                && actionHeight > 0)
            {
                return actionHeight;
            }

            if (ActionHeights.TryGetValue("stand", out int standHeight) && standHeight > 0)
            {
                return standHeight;
            }

            return 1;
        }
    }
}
