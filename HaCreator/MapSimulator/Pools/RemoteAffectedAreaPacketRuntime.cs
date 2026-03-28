using System;

namespace HaCreator.MapSimulator.Pools
{
    internal sealed class RemoteAffectedAreaPacketRuntime
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
            Func<RemoteAffectedAreaCreatedPacket, bool> applyCreate,
            Func<int, bool> applyRemove,
            out string result)
        {
            result = null;
            payload ??= Array.Empty<byte>();

            switch (packetType)
            {
                case (int)RemoteAffectedAreaPacketType.Create:
                    if (!RemoteAffectedAreaPacketCodec.TryParseCreated(payload, out RemoteAffectedAreaCreatedPacket created, out string createError))
                    {
                        result = createError;
                        return false;
                    }

                    if (applyCreate?.Invoke(created) != true)
                    {
                        result = $"Failed to apply {RemoteAffectedAreaPacketCodec.DescribePacketType(packetType)} for object {created.ObjectId}.";
                        return false;
                    }

                    result = $"Applied {RemoteAffectedAreaPacketCodec.DescribePacketType(packetType)} for object {created.ObjectId}.";
                    return true;

                case (int)RemoteAffectedAreaPacketType.Remove:
                    if (!RemoteAffectedAreaPacketCodec.TryParseRemoved(payload, out RemoteAffectedAreaRemovedPacket removed, out string removeError))
                    {
                        result = removeError;
                        return false;
                    }

                    bool removedKnown = applyRemove?.Invoke(removed.ObjectId) == true;
                    result = removedKnown
                        ? $"Applied {RemoteAffectedAreaPacketCodec.DescribePacketType(packetType)} for object {removed.ObjectId}."
                        : $"Ignored {RemoteAffectedAreaPacketCodec.DescribePacketType(packetType)} for unknown object {removed.ObjectId}.";
                    return true;

                default:
                    result = $"Unsupported remote affected-area packet type {packetType}.";
                    return false;
            }
        }
    }
}
