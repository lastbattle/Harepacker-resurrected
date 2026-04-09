using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Entities;
using Microsoft.Xna.Framework;
using System;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
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
                        ResolveRemoteDropPacketPetActorId),
                    out string result))
            {
                return ChatCommandHandler.CommandResult.Error(result ?? $"Failed to apply remote drop packet {packetType}.");
            }

            return ChatCommandHandler.CommandResult.Ok($"{result} {DescribeRemoteDropStatus()}");
        }

        private string DescribeRemoteDropStatus()
        {
            return _dropPool == null
                ? "Drop pool unavailable."
                : $"Drop pool count={_dropPool.ActiveDropCount}.";
        }

        private void BindRemoteDropPacketField()
        {
            _remoteDropPacketRuntime.BindField(_mapBoard?.MapInfo?.id ?? -1, () => _dropPool?.ClearPacketDrops());
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
            if (ownerId <= 0 || actorId <= 0)
            {
                return false;
            }

            if (ownerId == actorId)
            {
                return true;
            }

            return IsTrackedDropPartyActor(ownerId) && IsTrackedDropPartyActor(actorId);
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

            return _socialListRuntime.IsTrackedPartyMember(actor.Name);
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
