using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private DateTime? _remoteDropPacketServerUtcAnchor;
        private int _remoteDropPacketServerTickAnchor;
        private string _remoteDropPacketServerClockSource = "hostUtc";
        private int _remoteDropPacketServerClockFieldId;
        private readonly System.Collections.Generic.Dictionary<int, int> _observedDropPartyActorParents = new();
        private readonly System.Collections.Generic.HashSet<int> _observedDropPartyAnchorActorIds = new();
        private readonly System.Collections.Generic.Dictionary<int, int> _observedDropPartyActorOwners = new();
        private readonly Dictionary<int, Vector2> _observedRemotePetPickupActorPositions = new();
        private readonly Dictionary<int, Vector2> _predictedRemotePetPickupActorPositions = new();

        private ChatCommandHandler.CommandResult ApplyRemoteDropPacketCommand(int packetType, byte[] payload)
        {
            if (_dropPool == null || _mapBoard?.MapInfo == null)
            {
                return ChatCommandHandler.CommandResult.Error("Drop pool is unavailable until a field is loaded.");
            }

            if (!_remoteDropPacketRuntime.TryApplyPacket(
                    packetType,
                    payload,
                    packet => _dropPool.ApplyPacketEnter(packet, currTickCount),
                    packet => ApplyRemoteDropPacketLeave(packet, currTickCount),
                    out string result))
            {
                return ChatCommandHandler.CommandResult.Error(result ?? $"Failed to apply remote drop packet {packetType}.");
            }

            return ChatCommandHandler.CommandResult.Ok($"{result} {DescribeRemoteDropStatus()}");
        }

        private bool ApplyRemoteDropPacketLeave(RemoteDropLeavePacket packet, int currentTime)
        {
            ObserveRemoteDropPacketLeavePartyLink(packet);

            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            int resolvedPetActorId = packet.Reason == PacketDropLeaveReason.PetPickup
                ? ResolveRemoteDropPacketPetActorId(packet)
                : packet.ActorId;
            int resolvedPetOwnerCharacterId = packet.Reason == PacketDropLeaveReason.PetPickup
                ? ResolveRemoteDropPacketLeaveOwnerCharacterId(packet, resolvedPetActorId)
                : 0;
            Vector2? petTargetPosition = packet.Reason == PacketDropLeaveReason.PetPickup
                ? ResolveRemoteDropPacketTargetPosition(packet.Reason, packet)
                : null;
            if (ShouldRetainRemoteDropPacketPetTargetPosition(packet, petTargetPosition))
            {
                foreach (int observedPetActorId in ResolveRemoteDropPacketLeaveObservedPetActorIds(
                    packet,
                    resolvedPetActorId,
                    resolvedPetOwnerCharacterId))
                {
                    RememberObservedRemotePetPickupActorPosition(observedPetActorId, petTargetPosition.Value);
                }
            }

            bool applied = _dropPool?.ApplyPacketLeave(
                packet,
                currentTime,
                localCharacterId,
                ResolveRemoteDropPacketActorName,
                ResolveRemoteDropPacketTargetPosition,
                ResolveRemoteDropPacketPetActorId,
                HandlePacketOwnedLocalPetPickup) == true;
            if (applied)
            {
                return true;
            }

            if (!ShouldReplayMissingDropPacketPickupNotice(packet.Reason, packet.ActorId, localCharacterId)
                || !TryResolveDropPickupActorKind(packet.Reason, out DropPickupActorKind actorKind))
            {
                return false;
            }

            int actorId = actorKind == DropPickupActorKind.Pet
                ? ResolveRemoteDropPacketPetActorId(packet)
                : packet.ActorId;
            int fallbackOwnerId = ResolveRemoteDropPacketPickupNoticeFallbackOwnerId(
                packet,
                actorId,
                ResolveDropPartyActorOwnerId);

            return TrySurfaceRecentRemoteDropPickupNotice(
                packet.DropId,
                currentTime,
                actorKind,
                actorId,
                ResolveRemoteDropPacketActorName(packet.Reason, packet),
                fallbackOwnerId,
                ResolveRemoteDropPacketTargetPosition(packet.Reason, packet));
        }

        private void ObserveRemoteDropPacketLeavePartyLink(RemoteDropLeavePacket packet)
        {
            DropItem drop = _dropPool?.GetDrop(packet.DropId);
            if (drop?.IsPacketControlled != true
                || drop.OwnershipType != DropOwnershipType.Party
                || drop.OwnerId <= 0)
            {
                return;
            }

            int resolvedActorId = packet.Reason == PacketDropLeaveReason.PetPickup
                ? ResolveRemoteDropPacketPetActorId(packet)
                : packet.ActorId;
            int resolvedOwnerCharacterId = ResolveRemoteDropPacketLeaveOwnerCharacterId(packet, resolvedActorId);
            foreach (int linkedActorId in ResolveRemoteDropPacketLeavePartyLinkActorIds(packet, resolvedActorId, resolvedOwnerCharacterId))
            {
                RegisterObservedDropPartyActorLink(drop.OwnerId, linkedActorId);
            }

            if (resolvedOwnerCharacterId > 0)
            {
                RememberObservedDropPartyActorOwner(packet.ActorId, resolvedOwnerCharacterId);
                RememberObservedDropPartyActorOwner(resolvedActorId, resolvedOwnerCharacterId);
            }
        }

        private void HandlePacketOwnedLocalPetPickup(RemoteDropLeavePacket packet)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (packet.Reason != PacketDropLeaveReason.PetPickup || packet.ActorId != localCharacterId)
            {
                return;
            }

            PlayPacketOwnedLocalPetPickupSound();
        }

        internal static bool TryResolveDropPickupActorKind(PacketDropLeaveReason reason, out DropPickupActorKind actorKind)
        {
            actorKind = reason switch
            {
                PacketDropLeaveReason.PlayerPickup => DropPickupActorKind.Player,
                PacketDropLeaveReason.PetPickup => DropPickupActorKind.Pet,
                PacketDropLeaveReason.MobPickup => DropPickupActorKind.Mob,
                PacketDropLeaveReason.OtherPickup => DropPickupActorKind.Other,
                _ => default
            };

            return reason == PacketDropLeaveReason.PlayerPickup
                || reason == PacketDropLeaveReason.PetPickup
                || reason == PacketDropLeaveReason.MobPickup
                || reason == PacketDropLeaveReason.OtherPickup;
        }

        internal static bool ShouldReplayMissingDropPacketPickupNotice(
            PacketDropLeaveReason reason,
            int actorId,
            int localCharacterId)
        {
            if (localCharacterId > 0)
            {
                if (reason == PacketDropLeaveReason.PlayerPickup && actorId == localCharacterId)
                {
                    return false;
                }

                if (reason == PacketDropLeaveReason.PetPickup && actorId == localCharacterId)
                {
                    return false;
                }
            }

            return reason == PacketDropLeaveReason.PlayerPickup
                || reason == PacketDropLeaveReason.PetPickup
                || reason == PacketDropLeaveReason.MobPickup
                || reason == PacketDropLeaveReason.OtherPickup;
        }

        private string DescribeRemoteDropStatus()
        {
            if (_dropPool == null)
            {
                return "Drop pool unavailable.";
            }

            string clockText = _remoteDropPacketServerUtcAnchor.HasValue
                ? $"serverClock={ResolveRemoteDropPacketServerUtc():O} ({_remoteDropPacketServerClockSource})"
                : "serverClock=hostUtc";
            return $"Drop pool count={_dropPool.ActiveDropCount}; {clockText}.";
        }

        private void SetRemoteDropPacketServerClock(DateTime serverUtc, int currentTime, string sourceLabel = "packet", int fieldId = 0)
        {
            _remoteDropPacketServerUtcAnchor = NormalizeRemoteDropPacketClockUtc(serverUtc);
            _remoteDropPacketServerTickAnchor = currentTime;
            _remoteDropPacketServerClockSource = string.IsNullOrWhiteSpace(sourceLabel)
                ? "packet"
                : sourceLabel;
            _remoteDropPacketServerClockFieldId = fieldId;
        }

        private void ClearRemoteDropPacketServerClock()
        {
            _remoteDropPacketServerUtcAnchor = null;
            _remoteDropPacketServerTickAnchor = 0;
            _remoteDropPacketServerClockSource = "hostUtc";
            _remoteDropPacketServerClockFieldId = 0;
        }

        private DateTime ResolveRemoteDropPacketServerUtc()
        {
            if (!_remoteDropPacketServerUtcAnchor.HasValue)
            {
                return DateTime.UtcNow;
            }

            int elapsedMs = unchecked(currTickCount - _remoteDropPacketServerTickAnchor);
            return _remoteDropPacketServerUtcAnchor.Value.AddMilliseconds(elapsedMs);
        }

        internal static DateTime NormalizeRemoteDropPacketClockUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Local => value.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                _ => value
            };
        }

        private void UpdateRemoteDropPacketServerClockFromSetField(PacketSetFieldPacket packet)
        {
            if (!TryResolveRemoteDropPacketServerClock(packet, out DateTime serverUtc))
            {
                return;
            }

            SetRemoteDropPacketServerClock(serverUtc, currTickCount, "setfield", packet.FieldId);
        }

        internal static bool TryResolveRemoteDropPacketServerClock(PacketSetFieldPacket packet, out DateTime serverUtc)
        {
            serverUtc = default;
            return TryResolveRemoteDropPacketServerClock(packet.ServerFileTime, out serverUtc);
        }

        internal static bool TryResolveRemoteDropPacketServerClock(long serverFileTime, out DateTime serverUtc)
        {
            serverUtc = default;
            if (serverFileTime <= 0 || serverFileTime == long.MaxValue)
            {
                return false;
            }

            try
            {
                serverUtc = DateTime.FromFileTimeUtc(serverFileTime);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private void BindRemoteDropPacketField()
        {
            int mapId = _mapBoard?.MapInfo?.id ?? -1;
            _remoteDropPacketRuntime.BindField(mapId, () =>
            {
                _dropPool?.ClearPacketDrops();
                ClearObservedDropPartyActorLinks();
                ClearObservedDropPartyActorOwners();
                ClearObservedRemotePetPickupActorPositions();
                ClearPredictedRemotePetPickupActorPositions();
                if (ShouldClearRemoteDropPacketServerClockOnFieldBind(
                    mapId,
                    _remoteDropPacketServerClockSource,
                    _remoteDropPacketServerClockFieldId))
                {
                    ClearRemoteDropPacketServerClock();
                }
            });
        }

        internal static bool ShouldClearRemoteDropPacketServerClockOnFieldBind(
            int mapId,
            string clockSource,
            int anchoredFieldId)
        {
            if (mapId <= 0)
            {
                return false;
            }

            if (!string.Equals(clockSource, "setfield", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return anchoredFieldId <= 0 || anchoredFieldId != mapId;
        }

        private Vector2? ResolveDropPacketSourcePosition(int sourceId)
        {
            Vector2? resolvedSourcePosition = ResolveDropPacketSourcePosition(
                sourceId,
                _playerManager?.Player?.Build?.Id ?? 0,
                _playerManager?.Player?.Position,
                ResolveLocalPetDropPacketSourcePosition,
                ResolveRemoteUserDropPacketSourcePosition,
                ResolveMobDropPacketSourcePosition);

            if (resolvedSourcePosition.HasValue)
            {
                return resolvedSourcePosition;
            }

            if (ResolveObservedRemotePetPickupActorPosition(sourceId) is Vector2 observedRemotePetPosition)
            {
                return observedRemotePetPosition;
            }

            if (ResolveObservedRemotePetPickupPosition(sourceId) is Vector2 observedOwnerScopedRemotePetPosition)
            {
                return observedOwnerScopedRemotePetPosition;
            }

            if (TryResolveRemotePetPickupPosition(
                sourceId,
                _remoteUserPool,
                _predictedRemotePetPickupActorPositions,
                out Vector2 remotePetPosition,
                ResolveObservedRemotePetPickupPosition))
            {
                RememberPredictedRemotePetPickupActorPosition(sourceId, remotePetPosition);
                return remotePetPosition;
            }

            if (TryResolveRemotePetPickupPositionFromObservedOwnerAlias(
                sourceId,
                _observedDropPartyActorParents,
                _observedDropPartyActorOwners,
                _remoteUserPool,
                _predictedRemotePetPickupActorPositions,
                ResolveObservedRemotePetPickupPosition,
                out int resolvedPetActorId,
                out Vector2 linkedOwnerScopedPetPosition))
            {
                RememberPredictedRemotePetPickupActorPosition(resolvedPetActorId, linkedOwnerScopedPetPosition);
                if (sourceId != resolvedPetActorId)
                {
                    RememberPredictedRemotePetPickupActorPosition(sourceId, linkedOwnerScopedPetPosition);
                }

                return linkedOwnerScopedPetPosition;
            }

            return null;
        }

        internal static Vector2? ResolveDropPacketSourcePosition(
            int sourceId,
            int localCharacterId,
            Vector2? localCharacterPosition,
            Func<int, Vector2?> localPetPositionResolver,
            Func<int, Vector2?> remoteUserPositionResolver,
            Func<int, Vector2?> mobPositionResolver)
        {
            if (sourceId <= 0)
            {
                return null;
            }

            if (sourceId == localCharacterId && localCharacterPosition.HasValue)
            {
                return localCharacterPosition.Value;
            }

            if (localPetPositionResolver?.Invoke(sourceId) is Vector2 localPetPosition)
            {
                return localPetPosition;
            }

            if (remoteUserPositionResolver?.Invoke(sourceId) is Vector2 remoteUserPosition)
            {
                return remoteUserPosition;
            }

            if (mobPositionResolver?.Invoke(sourceId) is Vector2 mobPosition)
            {
                return mobPosition;
            }

            return null;
        }

        private Vector2? ResolveLocalPetDropPacketSourcePosition(int sourceId)
        {
            if (sourceId <= 0 || _playerManager?.Pets?.ActivePets == null)
            {
                return null;
            }

            foreach (var pet in _playerManager.Pets.ActivePets)
            {
                if (pet?.RuntimeId == sourceId)
                {
                    return new Vector2(pet.X, pet.Y);
                }
            }

            return null;
        }

        private Vector2? ResolveRemoteUserDropPacketSourcePosition(int sourceId)
        {
            if (sourceId <= 0 || _remoteUserPool?.TryGetActor(sourceId, out RemoteUserActor actor) != true || actor == null)
            {
                return null;
            }

            return actor.Position;
        }

        private Vector2? ResolveMobDropPacketSourcePosition(int sourceId)
        {
            if (sourceId <= 0)
            {
                return null;
            }

            MobItem mob = _mobPool?.GetMob(sourceId);
            if (mob?.MovementInfo == null)
            {
                return null;
            }

            return new Vector2(mob.MovementInfo.X, mob.MovementInfo.Y);
        }

        private bool AreDropActorsInSameParty(int ownerId, int actorId)
        {
            RememberObservedDropPartyAnchor(ownerId);
            RememberObservedDropPartyAnchor(actorId);

            return AreDropActorsInSameParty(
                ownerId,
                actorId,
                _socialListRuntime.ClientPartyId,
                _playerManager?.Player?.Build?.Id ?? 0,
                _socialListRuntime.IsTrackedPartyActor,
                IsTrackedDropPartyActor,
                AreObservedDropPartyActorsLinked,
                IsObservedDropPartyActorPartyLinked,
                ResolveDropPartyActorOwnerId,
                IsObservedDropPartyActorPartyLinked);
        }

        internal static bool AreDropActorsInSameParty(
            int ownerId,
            int actorId,
            int localPartyId,
            int localCharacterId,
            Func<int, bool> trackedPartyActorEvaluator,
            Func<int, bool> legacyTrackedActorEvaluator,
            Func<int, int, bool> observedPartyLinkEvaluator = null,
            Func<int, bool> observedPartyAnchorEvaluator = null,
            Func<int, int> partyActorOwnerResolver = null,
            Func<int, bool> observedPartyLinkedEvaluator = null)
        {
            int normalizedOwnerId = NormalizeDropPartyActorId(ownerId, partyActorOwnerResolver);
            int normalizedActorId = NormalizeDropPartyActorId(actorId, partyActorOwnerResolver);
            if (!HasUsableDropPartyActorId(ownerId, normalizedOwnerId)
                || !HasUsableDropPartyActorId(actorId, normalizedActorId))
            {
                return false;
            }

            bool ownerMatchesLocalPartyId = localPartyId > 0
                && (ownerId == localPartyId || normalizedOwnerId == localPartyId);
            if (ownerMatchesLocalPartyId)
            {
                return IsKnownDropPartyActor(
                    actorId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator,
                    observedPartyAnchorEvaluator)
                    || IsKnownDropPartyActor(
                        normalizedActorId,
                        localPartyId,
                        localCharacterId,
                        trackedPartyActorEvaluator,
                        legacyTrackedActorEvaluator,
                        observedPartyAnchorEvaluator)
                    || observedPartyLinkedEvaluator?.Invoke(actorId) == true
                    || (normalizedActorId != actorId
                        && observedPartyLinkedEvaluator?.Invoke(normalizedActorId) == true);
            }

            if (ownerId == actorId
                || normalizedOwnerId == normalizedActorId
                || normalizedOwnerId == actorId
                || ownerId == normalizedActorId)
            {
                return true;
            }

            bool packetTrackedOwner = trackedPartyActorEvaluator?.Invoke(ownerId) == true
                || trackedPartyActorEvaluator?.Invoke(normalizedOwnerId) == true;
            bool packetTrackedActor = actorId == localCharacterId
                || normalizedActorId == localCharacterId
                || trackedPartyActorEvaluator?.Invoke(actorId) == true
                || trackedPartyActorEvaluator?.Invoke(normalizedActorId) == true;
            bool legacyTrackedOwner = legacyTrackedActorEvaluator?.Invoke(ownerId) == true
                || legacyTrackedActorEvaluator?.Invoke(normalizedOwnerId) == true;
            bool legacyTrackedActor = actorId == localCharacterId
                || normalizedActorId == localCharacterId
                || legacyTrackedActorEvaluator?.Invoke(actorId) == true
                || legacyTrackedActorEvaluator?.Invoke(normalizedActorId) == true;
            bool observedTrackedOwner = observedPartyAnchorEvaluator?.Invoke(ownerId) == true
                || observedPartyAnchorEvaluator?.Invoke(normalizedOwnerId) == true;
            bool observedTrackedActor = observedPartyAnchorEvaluator?.Invoke(actorId) == true
                || observedPartyAnchorEvaluator?.Invoke(normalizedActorId) == true;
            bool observedPartyLinkedOwner = observedPartyLinkedEvaluator?.Invoke(ownerId) == true
                || observedPartyLinkedEvaluator?.Invoke(normalizedOwnerId) == true;
            bool observedPartyLinkedActor = observedPartyLinkedEvaluator?.Invoke(actorId) == true
                || observedPartyLinkedEvaluator?.Invoke(normalizedActorId) == true;
            bool knownOwner = IsKnownDropPartyActor(
                    ownerId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator,
                    observedPartyAnchorEvaluator)
                || IsKnownDropPartyActor(
                    normalizedOwnerId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator,
                    observedPartyAnchorEvaluator);
            bool knownActor = IsKnownDropPartyActor(
                    actorId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator,
                    observedPartyAnchorEvaluator)
                || IsKnownDropPartyActor(
                    normalizedActorId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator,
                    observedPartyAnchorEvaluator);

            if ((knownOwner || packetTrackedOwner || legacyTrackedOwner || observedTrackedOwner)
                && (knownActor || packetTrackedActor || legacyTrackedActor || observedTrackedActor))
            {
                return true;
            }

            if ((knownOwner || packetTrackedOwner || legacyTrackedOwner || observedTrackedOwner || observedPartyLinkedOwner)
                && (knownActor || packetTrackedActor || legacyTrackedActor || observedTrackedActor || observedPartyLinkedActor))
            {
                return true;
            }

            if (observedPartyLinkEvaluator?.Invoke(ownerId, actorId) == true
                || observedPartyLinkEvaluator?.Invoke(normalizedOwnerId, actorId) == true
                || observedPartyLinkEvaluator?.Invoke(ownerId, normalizedActorId) == true
                || observedPartyLinkEvaluator?.Invoke(normalizedOwnerId, normalizedActorId) == true)
            {
                return packetTrackedOwner
                    || knownOwner
                    || legacyTrackedOwner
                    || observedTrackedOwner
                    || observedPartyLinkedOwner
                    || packetTrackedActor
                    || knownActor
                    || legacyTrackedActor
                    || observedTrackedActor
                    || observedPartyLinkedActor;
            }

            return false;
        }

        private static bool HasUsableDropPartyActorId(int actorId, int normalizedActorId)
        {
            return actorId != 0 || normalizedActorId != 0;
        }

        internal static int ResolveRemoteDropPacketLeaveOwnerCharacterId(RemoteDropLeavePacket packet, int resolvedActorId)
        {
            if (packet.Reason != PacketDropLeaveReason.PetPickup)
            {
                return packet.ActorId > 0 ? packet.ActorId : 0;
            }

            if (packet.ActorId > 0)
            {
                return packet.ActorId;
            }

            if (TryDecodeRemotePetPickupActorId(packet.ActorId, out int packetOwnerCharacterId, out _))
            {
                return packetOwnerCharacterId;
            }

            if (TryDecodeRemotePetPickupActorId(resolvedActorId, out int resolvedOwnerCharacterId, out _))
            {
                return resolvedOwnerCharacterId;
            }

            return 0;
        }

        internal static int ResolveRemoteDropPacketPickupNoticeFallbackOwnerId(
            RemoteDropLeavePacket packet,
            int resolvedActorId,
            Func<int, int> partyActorOwnerResolver)
        {
            if (packet.Reason == PacketDropLeaveReason.PetPickup)
            {
                int petOwnerCharacterId = ResolveRemoteDropPacketLeaveOwnerCharacterId(packet, resolvedActorId);
                return petOwnerCharacterId > 0
                    ? petOwnerCharacterId
                    : 0;
            }

            if (packet.Reason != PacketDropLeaveReason.OtherPickup)
            {
                return 0;
            }

            int normalizedOwnerCharacterId = partyActorOwnerResolver?.Invoke(resolvedActorId) ?? 0;
            if (normalizedOwnerCharacterId <= 0 && packet.ActorId > 0)
            {
                normalizedOwnerCharacterId = partyActorOwnerResolver?.Invoke(packet.ActorId) ?? 0;
            }

            if (normalizedOwnerCharacterId > 0)
            {
                return normalizedOwnerCharacterId;
            }

            return packet.ActorId > 0
                ? packet.ActorId
                : 0;
        }

        internal static int[] ResolveRemoteDropPacketLeavePartyLinkActorIds(
            RemoteDropLeavePacket packet,
            int resolvedActorId,
            int resolvedOwnerCharacterId)
        {
            HashSet<int> linkedActorIds = new();
            if (packet.ActorId != 0)
            {
                linkedActorIds.Add(packet.ActorId);
            }

            if (resolvedActorId != 0)
            {
                linkedActorIds.Add(resolvedActorId);
            }

            if (resolvedOwnerCharacterId > 0)
            {
                linkedActorIds.Add(resolvedOwnerCharacterId);
            }

            if (packet.Reason == PacketDropLeaveReason.PetPickup && resolvedOwnerCharacterId > 0)
            {
                AddRemotePetPickupActorAliasesForOwner(linkedActorIds, resolvedOwnerCharacterId);
            }

            return linkedActorIds.ToArray();
        }

        internal static int[] ResolveRemoteDropPacketLeaveObservedPetActorIds(
            RemoteDropLeavePacket packet,
            int resolvedActorId,
            int resolvedOwnerCharacterId)
        {
            if (packet.Reason != PacketDropLeaveReason.PetPickup)
            {
                return Array.Empty<int>();
            }

            HashSet<int> observedActorIds = new();
            if (packet.ActorId != 0)
            {
                observedActorIds.Add(packet.ActorId);
            }

            if (resolvedActorId != 0)
            {
                observedActorIds.Add(resolvedActorId);
            }

            if (resolvedOwnerCharacterId > 0)
            {
                AddRemotePetPickupActorAliasesForOwner(observedActorIds, resolvedOwnerCharacterId);
            }

            return observedActorIds.ToArray();
        }

        internal static bool ShouldRetainRemoteDropPacketPetTargetPosition(
            RemoteDropLeavePacket packet,
            Vector2? pickupTargetPosition)
        {
            return packet.Reason == PacketDropLeaveReason.PetPickup
                && pickupTargetPosition.HasValue;
        }

        internal static void AddRemotePetPickupActorAliasesForOwner(ISet<int> actorIds, int ownerCharacterId)
        {
            if (actorIds == null || ownerCharacterId <= 0)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < RemotePetPickupPredictedSlotCount; slotIndex++)
            {
                actorIds.Add(BuildRemotePetPickupActorId(ownerCharacterId, slotIndex));
            }
        }

        internal static bool TryResolveRemotePetPickupOwnerAndSlotForPacketParity(
            int actorId,
            int fallbackOwnerId,
            Func<int, int> partyActorOwnerResolver,
            out int ownerCharacterId,
            out int slotIndex)
        {
            ownerCharacterId = 0;
            slotIndex = 0;

            if (TryDecodeRemotePetPickupActorId(actorId, out int decodedOwnerCharacterId, out int decodedSlotIndex))
            {
                ownerCharacterId = decodedOwnerCharacterId;
                slotIndex = NormalizeRemotePetPickupSlotIndexForPacketParity(decodedSlotIndex);
                return true;
            }

            int normalizedOwnerCharacterId = partyActorOwnerResolver?.Invoke(actorId) ?? 0;
            if (normalizedOwnerCharacterId > 0 && normalizedOwnerCharacterId != actorId)
            {
                ownerCharacterId = normalizedOwnerCharacterId;
                slotIndex = 0;
                return true;
            }

            if (fallbackOwnerId > 0)
            {
                ownerCharacterId = fallbackOwnerId;
                slotIndex = 0;
                return true;
            }

            return false;
        }

        private int ResolveDropPartyActorOwnerId(int actorId)
        {
            if (actorId <= 0)
            {
                if (TryDecodeRemotePetPickupActorId(actorId, out int remotePetOwnerId, out _))
                {
                    return remotePetOwnerId;
                }

                return actorId;
            }

            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (localCharacterId > 0
                && _playerManager?.Pets?.ActivePets != null)
            {
                foreach (var pet in _playerManager.Pets.ActivePets)
                {
                    if (pet?.RuntimeId == actorId)
                    {
                        return localCharacterId;
                    }
                }
            }

            if (TryResolveObservedDropPartyActorOwner(actorId, out int observedOwnerId))
            {
                return observedOwnerId;
            }

            if (TryResolveObservedDropPartyLinkedOwnerAlias(
                _observedDropPartyActorParents,
                _observedDropPartyActorOwners,
                actorId,
                out int linkedOwnerCharacterId,
                out _))
            {
                return linkedOwnerCharacterId;
            }

            return actorId;
        }

        private bool TryResolveRemotePetPickupPositionByOwnerForPacketParity(
            int ownerCharacterId,
            int slotIndex,
            int actorId,
            out Vector2 position)
        {
            position = default;
            if (ownerCharacterId <= 0 || slotIndex < 0)
            {
                return false;
            }

            if (ResolveObservedRemotePetPickupPosition(ownerCharacterId, slotIndex) is Vector2 observedPosition)
            {
                position = observedPosition;
                return true;
            }

            if (ResolvePredictedRemotePetPickupPosition(ownerCharacterId, slotIndex) is Vector2 predictedPosition)
            {
                position = predictedPosition;
                return true;
            }

            if (_remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor ownerActor) != true
                || ownerActor == null
                || !TryResolveRemotePetPickupSlotIndexForPacketParity(
                    ownerActor.Build?.RemotePetItemIds,
                    slotIndex,
                    out int resolvedSlotIndex))
            {
                return false;
            }

            if (CanResolveRemotePetPickupSlot(ownerActor, resolvedSlotIndex))
            {
                position = ResolveRemotePetPickupPosition(ownerActor, resolvedSlotIndex);
            }
            else if (!TryResolveRemotePetPickupPositionFromOwnerState(
                ownerActor.Position,
                ownerActor.FacingRight,
                ownerActor.Build?.RemotePetItemIds,
                resolvedSlotIndex,
                out position))
            {
                return false;
            }

            int resolvedPetActorId = BuildRemotePetPickupActorId(ownerCharacterId, resolvedSlotIndex);
            RememberPredictedRemotePetPickupActorPosition(resolvedPetActorId, position);
            if (actorId != 0 && actorId != resolvedPetActorId)
            {
                RememberPredictedRemotePetPickupActorPosition(actorId, position);
            }

            return true;
        }

        internal static bool TryResolveRemotePetPickupPositionFromObservedOwnerAlias(
            int actorId,
            System.Collections.Generic.IDictionary<int, int> actorParents,
            IReadOnlyDictionary<int, int> actorOwners,
            RemoteUserActorPool remoteUserPool,
            IReadOnlyDictionary<int, Vector2> predictedPetActorPositions,
            Func<int, int, Vector2?> observedOwnerSlotPositionResolver,
            out int resolvedPetActorId,
            out Vector2 position)
        {
            resolvedPetActorId = 0;
            position = default;
            if (!TryResolveObservedDropPartyLinkedOwnerAlias(
                    actorParents,
                    actorOwners,
                    actorId,
                    out int ownerCharacterId,
                    out int slotIndex)
                || ownerCharacterId <= 0
                || slotIndex < 0)
            {
                return false;
            }

            if (observedOwnerSlotPositionResolver?.Invoke(ownerCharacterId, slotIndex) is Vector2 observedPosition)
            {
                resolvedPetActorId = BuildRemotePetPickupActorId(ownerCharacterId, slotIndex);
                position = observedPosition;
                return true;
            }

            if (ResolveObservedRemotePetPickupPosition(predictedPetActorPositions, ownerCharacterId, slotIndex) is Vector2 predictedPosition)
            {
                resolvedPetActorId = BuildRemotePetPickupActorId(ownerCharacterId, slotIndex);
                position = predictedPosition;
                return true;
            }

            if (remoteUserPool?.TryGetActor(ownerCharacterId, out RemoteUserActor ownerActor) != true
                || ownerActor == null
                || !TryResolveRemotePetPickupSlotIndexForPacketParity(
                    ownerActor.Build?.RemotePetItemIds,
                    slotIndex,
                    out int resolvedSlotIndex))
            {
                return false;
            }

            if (CanResolveRemotePetPickupSlot(ownerActor, resolvedSlotIndex))
            {
                position = ResolveRemotePetPickupPosition(ownerActor, resolvedSlotIndex);
            }
            else if (!TryResolveRemotePetPickupPositionFromOwnerState(
                ownerActor.Position,
                ownerActor.FacingRight,
                ownerActor.Build?.RemotePetItemIds,
                resolvedSlotIndex,
                out position))
            {
                return false;
            }

            resolvedPetActorId = BuildRemotePetPickupActorId(ownerCharacterId, resolvedSlotIndex);
            return true;
        }

        private static int NormalizeDropPartyActorId(int actorId, Func<int, int> partyActorOwnerResolver)
        {
            if (actorId <= 0)
            {
                return partyActorOwnerResolver?.Invoke(actorId) ?? actorId;
            }

            int resolvedActorId = partyActorOwnerResolver?.Invoke(actorId) ?? actorId;
            return resolvedActorId > 0 ? resolvedActorId : actorId;
        }

        private void RegisterObservedDropPartyActorLink(int firstActorId, int secondActorId)
        {
            RegisterObservedDropPartyActorLink(_observedDropPartyActorParents, firstActorId, secondActorId);
            RememberObservedDropPartyAnchor(firstActorId);
            RememberObservedDropPartyAnchor(secondActorId);
        }

        private void ClearObservedDropPartyActorLinks()
        {
            _observedDropPartyActorParents.Clear();
            _observedDropPartyAnchorActorIds.Clear();
        }

        private void RememberObservedDropPartyActorOwner(int actorId, int ownerCharacterId)
        {
            if (actorId == 0 || ownerCharacterId <= 0)
            {
                return;
            }

            _observedDropPartyActorOwners[actorId] = ownerCharacterId;
        }

        private bool TryResolveObservedDropPartyActorOwner(int actorId, out int ownerCharacterId)
        {
            if (actorId != 0 && _observedDropPartyActorOwners.TryGetValue(actorId, out int observedOwnerId) && observedOwnerId > 0)
            {
                ownerCharacterId = observedOwnerId;
                return true;
            }

            ownerCharacterId = 0;
            return false;
        }

        private void ClearObservedDropPartyActorOwners()
        {
            _observedDropPartyActorOwners.Clear();
        }

        private void RememberObservedRemotePetPickupActorPosition(int petActorId, Vector2 position)
        {
            if (petActorId == 0)
            {
                return;
            }

            _observedRemotePetPickupActorPositions[petActorId] = position;
        }

        private Vector2? ResolveObservedRemotePetPickupActorPosition(int petActorId)
        {
            return petActorId != 0 && _observedRemotePetPickupActorPositions.TryGetValue(petActorId, out Vector2 position)
                ? position
                : null;
        }

        private void RememberPredictedRemotePetPickupActorPosition(int petActorId, Vector2 position)
        {
            if (petActorId == 0)
            {
                return;
            }

            _predictedRemotePetPickupActorPositions[petActorId] = position;
        }

        private void RememberPredictedRemotePetPickupActorPositionsForOwnerState(RemoteUserActor ownerActor)
        {
            if (ownerActor == null || ownerActor.CharacterId <= 0)
            {
                return;
            }

            RememberPredictedRemotePetPickupActorPositionsForOwnerState(
                _predictedRemotePetPickupActorPositions,
                ownerActor.CharacterId,
                ownerActor.Position,
                ownerActor.FacingRight,
                ownerActor.Build?.RemotePetItemIds);
        }

        internal static void RememberPredictedRemotePetPickupActorPositionsForOwnerState(
            IDictionary<int, Vector2> predictedPetActorPositions,
            int ownerCharacterId,
            Vector2 ownerPosition,
            bool ownerFacingRight,
            IReadOnlyList<int> remotePetItemIds)
        {
            if (predictedPetActorPositions == null || ownerCharacterId <= 0)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < RemotePetPickupPredictedSlotCount; slotIndex++)
            {
                if (!TryResolveRemotePetPickupSlotIndexForPacketParity(
                        remotePetItemIds,
                        slotIndex,
                        out int resolvedSlotIndex)
                    || resolvedSlotIndex < 0)
                {
                    continue;
                }

                if (!TryResolveRemotePetPickupPositionFromOwnerState(
                        ownerPosition,
                        ownerFacingRight,
                        remotePetItemIds,
                        resolvedSlotIndex,
                        out Vector2 predictedPosition))
                {
                    continue;
                }

                int petActorId = BuildRemotePetPickupActorId(ownerCharacterId, resolvedSlotIndex);
                predictedPetActorPositions[petActorId] = predictedPosition;
            }
        }

        private Vector2? ResolvePredictedRemotePetPickupActorPosition(int petActorId)
        {
            return petActorId != 0 && _predictedRemotePetPickupActorPositions.TryGetValue(petActorId, out Vector2 position)
                ? position
                : null;
        }

        private Vector2? ResolvePredictedRemotePetPickupPosition(int ownerCharacterId, int slotIndex)
        {
            return ResolveObservedRemotePetPickupPosition(
                _predictedRemotePetPickupActorPositions,
                ownerCharacterId,
                slotIndex);
        }

        private Vector2? ResolvePredictedRemotePetPickupPosition(int petActorId)
        {
            return ResolveObservedRemotePetPickupPosition(
                _predictedRemotePetPickupActorPositions,
                petActorId);
        }

        private Vector2? ResolveObservedRemotePetPickupPosition(int ownerCharacterId, int slotIndex)
        {
            return ResolveObservedRemotePetPickupPosition(
                _observedRemotePetPickupActorPositions,
                ownerCharacterId,
                slotIndex);
        }

        private Vector2? ResolveObservedRemotePetPickupPosition(int petActorId)
        {
            return ResolveObservedRemotePetPickupPosition(
                _observedRemotePetPickupActorPositions,
                petActorId);
        }

        internal static Vector2? ResolveObservedRemotePetPickupPosition(
            IReadOnlyDictionary<int, Vector2> observedPetActorPositions,
            int ownerCharacterId,
            int slotIndex)
        {
            if (observedPetActorPositions == null || ownerCharacterId <= 0 || slotIndex < 0)
            {
                return null;
            }

            int normalizedSlotIndex = NormalizeRemotePetPickupSlotIndexForPacketParity(slotIndex);
            int exactPetActorId = BuildRemotePetPickupActorId(ownerCharacterId, normalizedSlotIndex);
            if (observedPetActorPositions.TryGetValue(exactPetActorId, out Vector2 exactPosition))
            {
                return exactPosition;
            }

            int closestSlotDelta = int.MaxValue;
            Vector2? closestPosition = null;
            foreach (KeyValuePair<int, Vector2> observedEntry in observedPetActorPositions)
            {
                if (!TryDecodeRemotePetPickupActorId(observedEntry.Key, out int observedOwnerId, out int observedSlotIndex)
                    || observedOwnerId != ownerCharacterId)
                {
                    continue;
                }

                int normalizedObservedSlotIndex = NormalizeRemotePetPickupSlotIndexForPacketParity(observedSlotIndex);
                int slotDelta = Math.Abs(normalizedObservedSlotIndex - normalizedSlotIndex);
                if (slotDelta >= closestSlotDelta)
                {
                    continue;
                }

                closestSlotDelta = slotDelta;
                closestPosition = observedEntry.Value;
            }

            return closestPosition;
        }

        internal static Vector2? ResolveObservedRemotePetPickupPosition(
            IReadOnlyDictionary<int, Vector2> observedPetActorPositions,
            int petActorId)
        {
            if (!TryDecodeRemotePetPickupActorId(petActorId, out int ownerCharacterId, out int slotIndex))
            {
                return null;
            }

            return ResolveObservedRemotePetPickupPosition(
                observedPetActorPositions,
                ownerCharacterId,
                slotIndex);
        }

        private void ClearObservedRemotePetPickupActorPositions()
        {
            _observedRemotePetPickupActorPositions.Clear();
        }

        private void ClearPredictedRemotePetPickupActorPositions()
        {
            _predictedRemotePetPickupActorPositions.Clear();
        }

        private void ForgetObservedDropPacketActorState(int actorId)
        {
            if (actorId == 0)
            {
                return;
            }

            ForgetObservedDropPartyActor(actorId);
            ForgetObservedDropPartyActorOwner(actorId);
            ForgetObservedRemotePetPickupOwner(actorId);
        }

        private void ForgetObservedDropPartyActor(int actorId)
        {
            RemoveObservedDropPartyActor(_observedDropPartyActorParents, _observedDropPartyAnchorActorIds, actorId);

            int normalizedActorId = NormalizeDropPartyActorId(actorId, ResolveDropPartyActorOwnerId);
            if (normalizedActorId != actorId)
            {
                RemoveObservedDropPartyActor(_observedDropPartyActorParents, _observedDropPartyAnchorActorIds, normalizedActorId);
            }

            RemoveObservedDropPartyOwnerActorAliases(
                _observedDropPartyActorParents,
                _observedDropPartyAnchorActorIds,
                actorId);
        }

        private void ForgetObservedDropPartyActorOwner(int actorId)
        {
            RemoveObservedDropPartyActorOwners(_observedDropPartyActorOwners, actorId);

            int normalizedActorId = NormalizeDropPartyActorId(actorId, ResolveDropPartyActorOwnerId);
            if (normalizedActorId != actorId)
            {
                RemoveObservedDropPartyActorOwners(_observedDropPartyActorOwners, normalizedActorId);
            }

            RemoveObservedDropPartyOwnerActorAliasOwners(_observedDropPartyActorOwners, actorId);
        }

        private void ForgetObservedRemotePetPickupOwner(int ownerCharacterId)
        {
            RemoveObservedRemotePetPickupOwner(_observedRemotePetPickupActorPositions, ownerCharacterId);
            RemoveObservedRemotePetPickupOwner(_predictedRemotePetPickupActorPositions, ownerCharacterId);
        }

        internal static void RemoveObservedDropPartyActor(
            IDictionary<int, int> actorParents,
            ISet<int> observedPartyAnchorActorIds,
            int actorId)
        {
            if (actorId == 0)
            {
                return;
            }

            observedPartyAnchorActorIds?.Remove(actorId);
            if (actorParents == null)
            {
                return;
            }

            int[] keysToRemove = actorParents
                .Where(entry => entry.Key == actorId || entry.Value == actorId)
                .Select(entry => entry.Key)
                .ToArray();
            for (int i = 0; i < keysToRemove.Length; i++)
            {
                actorParents.Remove(keysToRemove[i]);
            }
        }

        internal static void RemoveObservedDropPartyOwnerActorAliases(
            IDictionary<int, int> actorParents,
            ISet<int> observedPartyAnchorActorIds,
            int ownerCharacterId)
        {
            if (ownerCharacterId <= 0)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < RemotePetPickupPredictedSlotCount; slotIndex++)
            {
                RemoveObservedDropPartyActor(
                    actorParents,
                    observedPartyAnchorActorIds,
                    BuildRemotePetPickupActorId(ownerCharacterId, slotIndex));
            }
        }

        internal static void RemoveObservedRemotePetPickupOwner(
            IDictionary<int, Vector2> observedPetActorPositions,
            int ownerCharacterId)
        {
            if (observedPetActorPositions == null || ownerCharacterId <= 0)
            {
                return;
            }

            int[] actorIdsToRemove = observedPetActorPositions.Keys
                .Where(petActorId => TryDecodeRemotePetPickupActorId(petActorId, out int decodedOwnerId, out _)
                    && decodedOwnerId == ownerCharacterId)
                .ToArray();
            for (int i = 0; i < actorIdsToRemove.Length; i++)
            {
                observedPetActorPositions.Remove(actorIdsToRemove[i]);
            }
        }

        internal static void RemoveObservedDropPartyActorOwners(
            IDictionary<int, int> actorOwners,
            int actorId)
        {
            if (actorOwners == null || actorId == 0)
            {
                return;
            }

            actorOwners.Remove(actorId);
            int[] keysToRemove = actorOwners
                .Where(entry => entry.Value == actorId)
                .Select(entry => entry.Key)
                .ToArray();
            for (int i = 0; i < keysToRemove.Length; i++)
            {
                actorOwners.Remove(keysToRemove[i]);
            }
        }

        internal static void RemoveObservedDropPartyOwnerActorAliasOwners(
            IDictionary<int, int> actorOwners,
            int ownerCharacterId)
        {
            if (actorOwners == null || ownerCharacterId <= 0)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < RemotePetPickupPredictedSlotCount; slotIndex++)
            {
                RemoveObservedDropPartyActorOwners(
                    actorOwners,
                    BuildRemotePetPickupActorId(ownerCharacterId, slotIndex));
            }
        }

        private bool AreObservedDropPartyActorsLinked(int firstActorId, int secondActorId)
        {
            return AreObservedDropPartyActorsLinked(_observedDropPartyActorParents, firstActorId, secondActorId);
        }

        private bool IsObservedDropPartyActorPartyLinked(int actorId)
        {
            return IsObservedDropPartyActorPartyLinked(
                _observedDropPartyActorParents,
                actorId,
                _socialListRuntime.ClientPartyId,
                _playerManager?.Player?.Build?.Id ?? 0,
                _socialListRuntime.IsTrackedPartyActor,
                IsTrackedDropPartyActor,
                actorId => _observedDropPartyAnchorActorIds.Contains(actorId),
                ResolveDropPartyActorOwnerId);
        }

        private void RememberObservedDropPartyAnchor(int actorId)
        {
            int normalizedActorId = NormalizeDropPartyActorId(actorId, ResolveDropPartyActorOwnerId);
            if (!HasUsableDropPartyActorId(actorId, normalizedActorId))
            {
                return;
            }

            if (IsKnownDropPartyActor(
                    actorId,
                    _socialListRuntime.ClientPartyId,
                    _playerManager?.Player?.Build?.Id ?? 0,
                    _socialListRuntime.IsTrackedPartyActor,
                    IsTrackedDropPartyActor))
            {
                _observedDropPartyAnchorActorIds.Add(actorId);
            }

            if (normalizedActorId > 0
                && normalizedActorId != actorId
                && IsKnownDropPartyActor(
                    normalizedActorId,
                    _socialListRuntime.ClientPartyId,
                    _playerManager?.Player?.Build?.Id ?? 0,
                    _socialListRuntime.IsTrackedPartyActor,
                    IsTrackedDropPartyActor))
            {
                _observedDropPartyAnchorActorIds.Add(normalizedActorId);
                if (actorId != 0)
                {
                    _observedDropPartyAnchorActorIds.Add(actorId);
                }
            }
        }

        internal static void RegisterObservedDropPartyActorLink(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            int firstActorId,
            int secondActorId)
        {
            if (actorParents == null || firstActorId == 0 || secondActorId == 0)
            {
                return;
            }

            int firstRoot = FindObservedDropPartyActorRoot(actorParents, firstActorId);
            int secondRoot = FindObservedDropPartyActorRoot(actorParents, secondActorId);
            if (firstRoot == secondRoot)
            {
                return;
            }

            actorParents[secondRoot] = firstRoot;
        }

        internal static bool AreObservedDropPartyActorsLinked(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            int firstActorId,
            int secondActorId)
        {
            if (actorParents == null || firstActorId == 0 || secondActorId == 0)
            {
                return false;
            }

            return FindObservedDropPartyActorRoot(actorParents, firstActorId)
                == FindObservedDropPartyActorRoot(actorParents, secondActorId);
        }

        internal static bool IsObservedDropPartyActorPartyLinked(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            int actorId,
            int localPartyId,
            int localCharacterId,
            Func<int, bool> trackedPartyActorEvaluator,
            Func<int, bool> legacyTrackedActorEvaluator,
            Func<int, bool> persistedPartyAnchorEvaluator = null,
            Func<int, int> partyActorOwnerResolver = null)
        {
            if (actorParents == null || actorId == 0)
            {
                return false;
            }

            int normalizedActorId = NormalizeDropPartyActorId(actorId, partyActorOwnerResolver);
            if (IsKnownDropPartyActor(
                actorId,
                localPartyId,
                localCharacterId,
                trackedPartyActorEvaluator,
                legacyTrackedActorEvaluator,
                persistedPartyAnchorEvaluator))
            {
                return true;
            }

            if (normalizedActorId != actorId
                && IsKnownDropPartyActor(
                    normalizedActorId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator,
                    persistedPartyAnchorEvaluator))
            {
                return true;
            }

            int[] targetRoots = ResolveObservedDropPartyTargetRoots(
                actorParents,
                actorId,
                normalizedActorId,
                partyActorOwnerResolver);
            if (targetRoots.Length == 0)
            {
                return false;
            }

            int[] linkedActorIds = actorParents.Keys.ToArray();
            foreach (int linkedActorId in linkedActorIds)
            {
                int linkedRoot = FindObservedDropPartyActorRoot(actorParents, linkedActorId);
                if (Array.IndexOf(targetRoots, linkedRoot) < 0)
                {
                    continue;
                }

                int normalizedLinkedActorId = NormalizeDropPartyActorId(linkedActorId, partyActorOwnerResolver);
                if (IsKnownDropPartyActor(
                    linkedActorId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator,
                    persistedPartyAnchorEvaluator))
                {
                    return true;
                }

                if (normalizedLinkedActorId != linkedActorId
                    && IsKnownDropPartyActor(
                        normalizedLinkedActorId,
                        localPartyId,
                        localCharacterId,
                        trackedPartyActorEvaluator,
                        legacyTrackedActorEvaluator,
                        persistedPartyAnchorEvaluator))
                {
                    return true;
                }
            }

            return false;
        }

        private static int[] ResolveObservedDropPartyTargetRoots(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            int actorId,
            int normalizedActorId,
            Func<int, int> partyActorOwnerResolver = null)
        {
            if (actorParents == null)
            {
                return Array.Empty<int>();
            }

            var targetRoots = new HashSet<int>();
            if (actorId != 0 && actorParents.ContainsKey(actorId))
            {
                targetRoots.Add(FindObservedDropPartyActorRoot(actorParents, actorId));
            }

            if (normalizedActorId != 0
                && normalizedActorId != actorId
                && actorParents.ContainsKey(normalizedActorId))
            {
                targetRoots.Add(FindObservedDropPartyActorRoot(actorParents, normalizedActorId));
            }

            if (partyActorOwnerResolver != null)
            {
                int[] linkedActorIds = actorParents.Keys.ToArray();
                for (int i = 0; i < linkedActorIds.Length; i++)
                {
                    int linkedActorId = linkedActorIds[i];
                    int normalizedLinkedActorId = NormalizeDropPartyActorId(linkedActorId, partyActorOwnerResolver);
                    if (normalizedLinkedActorId == 0
                        || (normalizedLinkedActorId != actorId && normalizedLinkedActorId != normalizedActorId))
                    {
                        continue;
                    }

                    targetRoots.Add(FindObservedDropPartyActorRoot(actorParents, linkedActorId));
                }
            }

            return targetRoots.Count == 0
                ? Array.Empty<int>()
                : targetRoots.ToArray();
        }

        private static bool IsKnownDropPartyActor(
            int actorId,
            int localPartyId,
            int localCharacterId,
            Func<int, bool> trackedPartyActorEvaluator,
            Func<int, bool> legacyTrackedActorEvaluator,
            Func<int, bool> persistedPartyAnchorEvaluator = null)
        {
            return actorId > 0
                && (actorId == localPartyId
                    || actorId == localCharacterId
                    || trackedPartyActorEvaluator?.Invoke(actorId) == true
                    || legacyTrackedActorEvaluator?.Invoke(actorId) == true
                    || persistedPartyAnchorEvaluator?.Invoke(actorId) == true);
        }

        private static int FindObservedDropPartyActorRoot(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            int actorId)
        {
            if (!actorParents.TryGetValue(actorId, out int parentActorId))
            {
                actorParents[actorId] = actorId;
                return actorId;
            }

            if (parentActorId == actorId)
            {
                return actorId;
            }

            int rootActorId = FindObservedDropPartyActorRoot(actorParents, parentActorId);
            actorParents[actorId] = rootActorId;
            return rootActorId;
        }

        internal static bool TryResolveObservedDropPartyLinkedOwnerAlias(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            IReadOnlyDictionary<int, int> actorOwners,
            int actorId,
            out int ownerCharacterId,
            out int slotIndex)
        {
            ownerCharacterId = 0;
            slotIndex = 0;
            if (actorParents == null || actorId == 0)
            {
                return false;
            }

            HashSet<int> targetRoots = new();
            if (actorParents.ContainsKey(actorId))
            {
                targetRoots.Add(FindObservedDropPartyActorRoot(actorParents, actorId));
            }

            if (actorOwners != null)
            {
                if (actorOwners.TryGetValue(actorId, out int directOwnerCharacterId)
                    && directOwnerCharacterId > 0)
                {
                    AddObservedDropPartyOwnerAliasRoots(actorParents, targetRoots, directOwnerCharacterId);
                }

                foreach (KeyValuePair<int, int> observedOwnerEntry in actorOwners)
                {
                    if (observedOwnerEntry.Value != actorId
                        || !actorParents.ContainsKey(observedOwnerEntry.Key))
                    {
                        continue;
                    }

                    targetRoots.Add(FindObservedDropPartyActorRoot(actorParents, observedOwnerEntry.Key));
                }
            }

            if (targetRoots.Count == 0)
            {
                return false;
            }

            int[] linkedActorIds = actorParents.Keys.ToArray();

            if (TryDecodeRemotePetPickupActorId(actorId, out int requestedOwnerCharacterId, out int requestedSlotIndex))
            {
                int normalizedRequestedSlotIndex = NormalizeRemotePetPickupSlotIndexForPacketParity(requestedSlotIndex);
                foreach (int linkedActorId in linkedActorIds)
                {
                    if (linkedActorId != actorId)
                    {
                        continue;
                    }

                    int linkedActorRoot = FindObservedDropPartyActorRoot(actorParents, linkedActorId);
                    if (!targetRoots.Contains(linkedActorRoot))
                    {
                        continue;
                    }

                    ownerCharacterId = requestedOwnerCharacterId;
                    slotIndex = normalizedRequestedSlotIndex;
                    return true;
                }
            }

            bool foundOwnerAlias = false;
            int bestOwnerAliasSlot = int.MaxValue;
            int bestOwnerAliasOwnerId = 0;
            foreach (int linkedActorId in linkedActorIds)
            {
                int linkedActorRoot = FindObservedDropPartyActorRoot(actorParents, linkedActorId);
                if (!targetRoots.Contains(linkedActorRoot))
                {
                    continue;
                }

                if (!TryDecodeRemotePetPickupActorId(linkedActorId, out int decodedOwnerCharacterId, out int decodedSlotIndex))
                {
                    continue;
                }

                int normalizedDecodedSlotIndex = NormalizeRemotePetPickupSlotIndexForPacketParity(decodedSlotIndex);
                if (!foundOwnerAlias || normalizedDecodedSlotIndex < bestOwnerAliasSlot)
                {
                    bestOwnerAliasOwnerId = decodedOwnerCharacterId;
                    bestOwnerAliasSlot = normalizedDecodedSlotIndex;
                    foundOwnerAlias = true;
                }
            }

            if (foundOwnerAlias && bestOwnerAliasOwnerId > 0)
            {
                ownerCharacterId = bestOwnerAliasOwnerId;
                slotIndex = bestOwnerAliasSlot;
                return true;
            }

            foreach (int linkedActorId in linkedActorIds)
            {
                int linkedActorRoot = FindObservedDropPartyActorRoot(actorParents, linkedActorId);
                if (!targetRoots.Contains(linkedActorRoot))
                {
                    continue;
                }

                if (linkedActorId != 0
                    && actorOwners != null
                    && actorOwners.TryGetValue(linkedActorId, out int observedOwnerCharacterId)
                    && observedOwnerCharacterId > 0)
                {
                    ownerCharacterId = observedOwnerCharacterId;
                    slotIndex = 0;
                    return true;
                }
            }

            return false;
        }

        private static void AddObservedDropPartyOwnerAliasRoots(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            ISet<int> targetRoots,
            int ownerCharacterId)
        {
            if (actorParents == null || targetRoots == null || ownerCharacterId <= 0)
            {
                return;
            }

            if (actorParents.ContainsKey(ownerCharacterId))
            {
                targetRoots.Add(FindObservedDropPartyActorRoot(actorParents, ownerCharacterId));
            }

            for (int slotIndex = 0; slotIndex < RemotePetPickupPredictedSlotCount; slotIndex++)
            {
                int ownerSlotActorId = BuildRemotePetPickupActorId(ownerCharacterId, slotIndex);
                if (actorParents.ContainsKey(ownerSlotActorId))
                {
                    targetRoots.Add(FindObservedDropPartyActorRoot(actorParents, ownerSlotActorId));
                }
            }
        }

        internal static bool ShouldSurfacePickupNotice(
            DropOwnershipType ownershipType,
            int ownerId,
            int localCharacterId,
            Func<int, int, bool> partyMembershipEvaluator)
        {
            if (ownerId <= 0 || localCharacterId <= 0)
            {
                return true;
            }

            return ownershipType switch
            {
                DropOwnershipType.Character => ownerId == localCharacterId,
                DropOwnershipType.Party => ownerId == localCharacterId
                    || partyMembershipEvaluator?.Invoke(ownerId, localCharacterId) == true,
                _ => true
            };
        }

        internal static bool ShouldSurfaceRecentPickupNotice(
            RecentPickupRecord recentPickup,
            int localCharacterId,
            Func<int, int, bool> partyMembershipEvaluator)
        {
            if (recentPickup == null)
            {
                return false;
            }

            return ShouldSurfacePickupNotice(
                recentPickup.OwnershipType,
                recentPickup.OwnerId,
                localCharacterId,
                partyMembershipEvaluator);
        }

        internal static bool IsClientPartyHelperMarker(MinimapUI.HelperMarkerType? markerType)
        {
            return markerType == MinimapUI.HelperMarkerType.Party
                || markerType == MinimapUI.HelperMarkerType.PartyMaster;
        }

        private bool IsTrackedDropPartyActor(int actorId)
        {
            int localCharacterId = _playerManager?.Player?.Build?.Id ?? 0;
            if (actorId > 0 && actorId == localCharacterId)
            {
                return true;
            }

            if (!_remoteUserPool.TryGetActor(actorId, out RemoteUserActor actor) || actor == null)
            {
                return false;
            }

            bool isTrackedRosterMember = !string.IsNullOrWhiteSpace(actor.Name)
                && _socialListRuntime.IsTrackedPartyMember(actor.Name);
            return isTrackedRosterMember
                || IsClientPartyHelperMarker(actor.HelperMarkerType)
                || HasClientPartyHpGauge(actor);
        }

        internal static bool HasClientPartyHpGauge(RemoteUserActor actor)
        {
            return actor?.PartyHpGaugePos.HasValue == true;
        }

        private string ResolveRemoteDropPacketActorName(PacketDropLeaveReason reason, RemoteDropLeavePacket packet)
        {
            if (reason == PacketDropLeaveReason.PetPickup)
            {
                int resolvedPetActorId = ResolveRemoteDropPacketPetActorId(packet);
                return ResolveRemotePickupActorName(
                    DropPickupActorKind.Pet,
                    resolvedPetActorId,
                    null,
                    _remoteUserPool,
                    ResolveMobPickupSourceName,
                    ResolvePickupItemName,
                    packet.ActorId);
            }

            return ResolveRemoteDropPacketActorName(
                reason,
                packet,
                _remoteUserPool,
                ResolveMobPickupSourceName,
                ResolvePickupItemName);
        }

        internal static string ResolveRemoteDropPacketActorName(
            PacketDropLeaveReason reason,
            RemoteDropLeavePacket packet,
            RemoteUserActorPool remoteUserPool,
            Func<int, string> mobNameResolver,
            Func<int, string> itemNameResolver)
        {
            return reason switch
            {
                PacketDropLeaveReason.OtherPickup => ResolveRemotePickupActorName(
                    DropPickupActorKind.Other,
                    packet.ActorId,
                    null,
                    remoteUserPool,
                    mobNameResolver,
                    itemNameResolver,
                    packet.ActorId),
                PacketDropLeaveReason.PlayerPickup => ResolveRemotePickupActorName(
                    DropPickupActorKind.Player,
                    packet.ActorId,
                    null,
                    remoteUserPool,
                    mobNameResolver,
                    itemNameResolver),
                PacketDropLeaveReason.MobPickup => ResolveRemotePickupActorName(
                    DropPickupActorKind.Mob,
                    packet.ActorId,
                    null,
                    remoteUserPool,
                    mobNameResolver,
                    itemNameResolver),
                PacketDropLeaveReason.PetPickup => ResolveRemotePickupActorName(
                    DropPickupActorKind.Pet,
                    ResolveRemoteDropPacketPetActorId(packet, localCharacterId: 0, _ => 0),
                    null,
                    remoteUserPool,
                    mobNameResolver,
                    itemNameResolver,
                    packet.ActorId),
                _ => null
            };
        }

        private Vector2? ResolveRemoteDropPacketTargetPosition(PacketDropLeaveReason reason, RemoteDropLeavePacket packet)
        {
            return reason switch
            {
                PacketDropLeaveReason.OtherPickup => ResolveDropPickupActorPosition(DropPickupActorKind.Other, packet.ActorId, packet.ActorId),
                PacketDropLeaveReason.PlayerPickup => ResolveDropPickupActorPosition(DropPickupActorKind.Player, packet.ActorId, 0),
                PacketDropLeaveReason.MobPickup => ResolveDropPickupActorPosition(DropPickupActorKind.Mob, packet.ActorId, 0),
                PacketDropLeaveReason.PetPickup => ResolveDropPickupActorPosition(
                    DropPickupActorKind.Pet,
                    ResolveRemoteDropPacketPetActorId(packet),
                    packet.ActorId),
                _ => null
            };
        }

        private int ResolveRemoteDropPacketPetActorId(RemoteDropLeavePacket packet)
        {
            return ResolveRemoteDropPacketPetActorId(
                packet,
                _playerManager?.Player?.Build?.Id ?? 0,
                ResolveLocalPetRuntimeIdByIndex);
        }

        internal static int ResolveRemoteDropPacketPetActorId(
            RemoteDropLeavePacket packet,
            int localCharacterId,
            Func<int, int> localPetRuntimeIdResolver)
        {
            if (packet.Reason != PacketDropLeaveReason.PetPickup)
            {
                return packet.ActorId;
            }

            int petIndex = NormalizeRemotePetPickupSlotIndexForPacketParity(packet.SecondaryActorId);
            if (packet.ActorId > 0 && packet.ActorId == localCharacterId)
            {
                int localPetRuntimeId = localPetRuntimeIdResolver?.Invoke(petIndex) ?? 0;
                return localPetRuntimeId > 0
                    ? localPetRuntimeId
                    : packet.ActorId;
            }

            return packet.ActorId > 0
                ? BuildRemotePetPickupActorId(packet.ActorId, petIndex)
                : packet.ActorId;
        }

        internal static int NormalizeRemotePetPickupSlotIndexForPacketParity(int slotIndex)
        {
            return Math.Clamp(slotIndex, 0, RemotePetPickupPredictedSlotCount - 1);
        }

        private int ResolveLocalPetRuntimeIdByIndex(int petIndex)
        {
            if (petIndex < 0 || _playerManager?.Pets?.ActivePets == null)
            {
                return 0;
            }

            foreach (var pet in _playerManager.Pets.ActivePets)
            {
                if (pet?.SlotIndex == petIndex)
                {
                    return pet.RuntimeId;
                }
            }

            return 0;
        }

        private Vector2? ResolveDropPickupActorPosition(DropPickupActorKind actorKind, int actorId, int fallbackOwnerId)
        {
            switch (actorKind)
            {
                case DropPickupActorKind.Player:
                    if (actorId > 0 && actorId == (_playerManager?.Player?.Build?.Id ?? 0) && _playerManager?.Player != null)
                    {
                        return _playerManager.Player.Position;
                    }

                    if (actorId > 0 && _remoteUserPool.TryGetActor(actorId, out RemoteUserActor remoteActor))
                    {
                        return remoteActor.Position;
                    }

                    break;

                case DropPickupActorKind.Pet:
                    if (ResolveObservedRemotePetPickupActorPosition(actorId) is Vector2 observedPetPosition)
                    {
                        return observedPetPosition;
                    }

                    if (ResolveObservedRemotePetPickupPosition(actorId) is Vector2 ownerScopedObservedPetPosition)
                    {
                        return ownerScopedObservedPetPosition;
                    }

                    if (ResolvePredictedRemotePetPickupActorPosition(actorId) is Vector2 predictedPetPosition)
                    {
                        return predictedPetPosition;
                    }

                    if (ResolvePredictedRemotePetPickupPosition(actorId) is Vector2 ownerScopedPredictedPetPosition)
                    {
                        return ownerScopedPredictedPetPosition;
                    }

                    if (_playerManager?.Pets?.ActivePets != null)
                    {
                        foreach (var pet in _playerManager.Pets.ActivePets)
                        {
                            if (pet?.RuntimeId == actorId)
                            {
                                return new Vector2(pet.X, pet.Y);
                            }
                        }
                    }

                    if (TryResolveRemotePetPickupPosition(
                        actorId,
                        _remoteUserPool,
                        _predictedRemotePetPickupActorPositions,
                        out Vector2 remotePetPosition,
                        ResolveObservedRemotePetPickupPosition))
                    {
                        RememberPredictedRemotePetPickupActorPosition(actorId, remotePetPosition);
                        return remotePetPosition;
                    }

                    if (TryResolveObservedDropPartyLinkedOwnerAlias(
                            _observedDropPartyActorParents,
                            _observedDropPartyActorOwners,
                            actorId,
                            out int linkedOwnerCharacterId,
                            out int linkedSlotIndex)
                        && TryResolveRemotePetPickupPositionByOwnerForPacketParity(
                            linkedOwnerCharacterId,
                            linkedSlotIndex,
                            actorId,
                            out Vector2 linkedOwnerScopedPetPosition))
                    {
                        return linkedOwnerScopedPetPosition;
                    }

                    if (TryResolveRemotePetPickupOwnerAndSlotForPacketParity(
                            actorId,
                            fallbackOwnerId,
                            ResolveDropPartyActorOwnerId,
                            out int resolvedOwnerCharacterId,
                            out int resolvedSlotIndex)
                        && TryResolveRemotePetPickupPositionByOwnerForPacketParity(
                            resolvedOwnerCharacterId,
                            resolvedSlotIndex,
                            actorId,
                            out Vector2 ownerScopedPetPosition))
                    {
                        return ownerScopedPetPosition;
                    }

                    if (fallbackOwnerId > 0)
                    {
                        Vector2? ownerPosition = ResolveDropPickupActorPosition(DropPickupActorKind.Player, fallbackOwnerId, 0);
                        if (ownerPosition.HasValue)
                        {
                            return ownerPosition;
                        }
                    }

                    break;

                case DropPickupActorKind.Mob:
                    return ResolveDropPacketSourcePosition(actorId);

                case DropPickupActorKind.Other:
                    Vector2? remoteActorPosition = ResolveDropPickupActorPosition(DropPickupActorKind.Player, actorId, 0);
                    if (remoteActorPosition.HasValue)
                    {
                        return remoteActorPosition;
                    }

                    Vector2? remotePetActorPosition = ResolveDropPickupActorPosition(DropPickupActorKind.Pet, actorId, fallbackOwnerId > 0 ? fallbackOwnerId : actorId);
                    if (remotePetActorPosition.HasValue)
                    {
                        return remotePetActorPosition;
                    }

                    if (actorId > 0)
                    {
                        return ResolveDropPacketSourcePosition(actorId);
                    }

                    break;
            }

            return null;
        }
    }
}
