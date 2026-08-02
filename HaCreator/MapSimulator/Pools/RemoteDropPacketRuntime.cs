using System;

namespace HaCreator.MapSimulator.Pools
{
    internal sealed class RemoteDropPacketRuntime
    {
        private int _boundMapId = int.MinValue;

        public void BindField(int mapId, Action clearCallback)
        {
            if (mapId < 0 || clearCallback == null || _boundMapId == mapId)
            {
                return;
            }

            _boundMapId = mapId;
            clearCallback();
        }

        public bool TryApplyPacket(
            int packetType,
            byte[] payload,
            Func<RemoteDropEnterPacket, bool> applyEnter,
            Func<RemoteDropLeavePacket, bool> applyLeave,
            out string result)
        {
            result = null;
            payload ??= Array.Empty<byte>();

            switch (packetType)
            {
                case (int)RemoteDropPacketType.Enter:
                    if (!RemoteDropPacketCodec.TryParseEnter(payload, out RemoteDropEnterPacket enterPacket, out string enterError))
                    {
                        result = enterError;
                        return false;
                    }

                    if (applyEnter?.Invoke(enterPacket) != true)
                    {
                        result = $"Failed to apply {RemoteDropPacketCodec.DescribePacketType(packetType)} for drop {enterPacket.DropId}.";
                        return false;
                    }

                    result = $"Applied {RemoteDropPacketCodec.DescribePacketType(packetType)} for drop {enterPacket.DropId}.";
                    return true;

                case (int)RemoteDropPacketType.Leave:
                    if (!RemoteDropPacketCodec.TryParseLeave(payload, out RemoteDropLeavePacket leavePacket, out string leaveError))
                    {
                        result = leaveError;
                        return false;
                    }

                    bool applied = applyLeave?.Invoke(leavePacket) == true;
                    result = applied
                        ? $"Applied {RemoteDropPacketCodec.DescribePacketType(packetType)} for drop {leavePacket.DropId}."
                        : $"Ignored {RemoteDropPacketCodec.DescribePacketType(packetType)} for unknown drop {leavePacket.DropId}.";
                    return true;

                default:
                    result = $"Unsupported remote drop packet type {packetType}.";
                    return false;
            }
        }
    }
}
