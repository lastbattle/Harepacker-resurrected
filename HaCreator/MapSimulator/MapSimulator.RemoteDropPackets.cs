using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using Microsoft.Xna.Framework;
using System;
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
                    packet => _dropPool.ApplyPacketLeave(
                        packet,
                        currTickCount,
                        _playerManager?.Player?.Build?.Id ?? 0,
                        ResolveRemoteDropPacketActorName,
                        ResolveRemoteDropPacketTargetPosition,
                        ResolveRemoteDropPacketPetActorId,
                        HandlePacketOwnedLocalPetPickup),
                    out string result))
            {
                return ChatCommandHandler.CommandResult.Error(result ?? $"Failed to apply remote drop packet {packetType}.");
            }

            return ChatCommandHandler.CommandResult.Ok($"{result} {DescribeRemoteDropStatus()}");
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

            return TryResolveRemotePetPickupPosition(sourceId, _remoteUserPool, out Vector2 remotePetPosition)
                ? remotePetPosition
                : null;
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
            return AreDropActorsInSameParty(
                ownerId,
                actorId,
                _socialListRuntime.ClientPartyId,
                _playerManager?.Player?.Build?.Id ?? 0,
                _socialListRuntime.IsTrackedPartyActor,
                IsTrackedDropPartyActor,
                AreObservedDropPartyActorsLinked,
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
            Func<int, bool> observedPartyAnchorEvaluator = null)
        {
            if (ownerId <= 0 || actorId <= 0)
            {
                return false;
            }

            if (localPartyId > 0 && ownerId == localPartyId)
            {
                return actorId == localCharacterId
                    || trackedPartyActorEvaluator?.Invoke(actorId) == true;
            }

            if (ownerId == actorId)
            {
                return true;
            }

            bool packetTrackedOwner = trackedPartyActorEvaluator?.Invoke(ownerId) == true;
            bool packetTrackedActor = actorId == localCharacterId
                || trackedPartyActorEvaluator?.Invoke(actorId) == true;
            bool legacyTrackedOwner = legacyTrackedActorEvaluator?.Invoke(ownerId) == true;
            bool legacyTrackedActor = actorId == localCharacterId
                || legacyTrackedActorEvaluator?.Invoke(actorId) == true;
            bool observedTrackedOwner = observedPartyAnchorEvaluator?.Invoke(ownerId) == true;
            bool observedTrackedActor = observedPartyAnchorEvaluator?.Invoke(actorId) == true;

            if ((packetTrackedOwner || legacyTrackedOwner || observedTrackedOwner)
                && (packetTrackedActor || legacyTrackedActor || observedTrackedActor))
            {
                return true;
            }

            if (observedPartyLinkEvaluator?.Invoke(ownerId, actorId) == true)
            {
                return true;
            }

            return false;
        }

        private void RegisterObservedDropPartyActorLink(int firstActorId, int secondActorId)
        {
            RegisterObservedDropPartyActorLink(_observedDropPartyActorParents, firstActorId, secondActorId);
        }

        private void ClearObservedDropPartyActorLinks()
        {
            _observedDropPartyActorParents.Clear();
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
                IsTrackedDropPartyActor);
        }

        internal static void RegisterObservedDropPartyActorLink(
            System.Collections.Generic.IDictionary<int, int> actorParents,
            int firstActorId,
            int secondActorId)
        {
            if (actorParents == null || firstActorId <= 0 || secondActorId <= 0)
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
            if (actorParents == null || firstActorId <= 0 || secondActorId <= 0)
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
            Func<int, bool> legacyTrackedActorEvaluator)
        {
            if (actorParents == null || actorId <= 0)
            {
                return false;
            }

            if (IsKnownDropPartyActor(
                actorId,
                localPartyId,
                localCharacterId,
                trackedPartyActorEvaluator,
                legacyTrackedActorEvaluator))
            {
                return true;
            }

            int targetRoot = FindObservedDropPartyActorRoot(actorParents, actorId);
            int[] linkedActorIds = actorParents.Keys.ToArray();
            foreach (int linkedActorId in linkedActorIds)
            {
                if (FindObservedDropPartyActorRoot(actorParents, linkedActorId) != targetRoot)
                {
                    continue;
                }

                if (IsKnownDropPartyActor(
                    linkedActorId,
                    localPartyId,
                    localCharacterId,
                    trackedPartyActorEvaluator,
                    legacyTrackedActorEvaluator))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKnownDropPartyActor(
            int actorId,
            int localPartyId,
            int localCharacterId,
            Func<int, bool> trackedPartyActorEvaluator,
            Func<int, bool> legacyTrackedActorEvaluator)
        {
            return actorId > 0
                && (actorId == localPartyId
                    || actorId == localCharacterId
                    || trackedPartyActorEvaluator?.Invoke(actorId) == true
                    || legacyTrackedActorEvaluator?.Invoke(actorId) == true);
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

            if (!_remoteUserPool.TryGetActor(actorId, out RemoteUserActor actor) || string.IsNullOrWhiteSpace(actor?.Name))
            {
                return false;
            }

            return _socialListRuntime.IsTrackedPartyMember(actor.Name)
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

            int petIndex = Math.Max(0, packet.SecondaryActorId);
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

                    if (TryResolveRemotePetPickupPosition(actorId, _remoteUserPool, out Vector2 remotePetPosition))
                    {
                        return remotePetPosition;
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
            }

            return null;
        }
    }
}
