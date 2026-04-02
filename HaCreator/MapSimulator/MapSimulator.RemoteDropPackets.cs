using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Entities;
using Microsoft.Xna.Framework;

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
                    packet => _dropPool.ApplyPacketLeave(packet, currTickCount, _playerManager?.Player?.Build?.Id ?? 0, ResolveRemoteDropPacketActorName),
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

        private string ResolveRemoteDropPacketActorName(PacketDropLeaveReason reason, RemoteDropLeavePacket packet)
        {
            return reason switch
            {
                PacketDropLeaveReason.PlayerPickup => ResolveRemotePickupActorName(DropPickupActorKind.Player, packet.ActorId, null),
                PacketDropLeaveReason.MobPickup => ResolveRemotePickupActorName(DropPickupActorKind.Mob, packet.ActorId, null),
                PacketDropLeaveReason.PetPickup => ResolveRemotePickupActorName(DropPickupActorKind.Player, packet.ActorId, null),
                _ => null
            };
        }
    }
}
