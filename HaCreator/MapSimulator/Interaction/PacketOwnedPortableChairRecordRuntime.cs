using HaCreator.MapSimulator.Pools;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Interaction
{
    /// <summary>
    /// Owns the packet-fed couple-chair record lifecycle that sits between
    /// remote chair state packets and the later pair-reconciliation pass.
    /// </summary>
    public sealed class PacketOwnedPortableChairRecordRuntime
    {
        public int TotalAddCount { get; private set; }
        public int TotalRemoveCount { get; private set; }
        public string LastDispatchSummary { get; private set; } = "Packet-owned portable-chair record runtime idle.";

        public void Clear()
        {
            TotalAddCount = 0;
            TotalRemoveCount = 0;
            LastDispatchSummary = "Packet-owned portable-chair record runtime idle.";
        }

        public bool IsPortableChairRecordPacket(int packetType)
        {
            return packetType is
                (int)RemoteUserPacketType.UserCoupleChairRecordAdd or
                (int)RemoteUserPacketType.UserCoupleChairRecordRemove;
        }

        public bool TryApplyPacket(
            int packetType,
            byte[] payload,
            RemoteUserActorPool remoteUserPool,
            string source,
            out string message)
        {
            message = null;
            if (remoteUserPool == null)
            {
                message = "Packet-owned portable-chair record runtime requires a remote-user pool.";
                LastDispatchSummary = message;
                return false;
            }

            if (!IsPortableChairRecordPacket(packetType))
            {
                message = $"Remote user packet type {packetType} is not a portable-chair record dispatch.";
                LastDispatchSummary = message;
                return false;
            }

            payload ??= Array.Empty<byte>();
            string normalizedSource = string.IsNullOrWhiteSpace(source) ? "remote-user-packet" : source.Trim();

            switch ((RemoteUserPacketType)packetType)
            {
                case RemoteUserPacketType.UserCoupleChairRecordAdd:
                    if (!RemoteUserPacketCodec.TryParsePortableChairRecordAdd(
                            payload,
                            out RemoteUserPortableChairRecordAddPacket addPacket,
                            out string addError))
                    {
                        message = addError;
                        LastDispatchSummary = $"Rejected {DescribePacketKind(packetType)} from {normalizedSource}: {addError}";
                        return false;
                    }

                    bool addApplied = remoteUserPool.TryApplyPortableChairRecordAdd(addPacket, out string addDetail);
                    if (addApplied)
                    {
                        TotalAddCount++;
                    }

                    message = addDetail;
                    LastDispatchSummary = BuildDispatchSummary(
                        normalizedSource,
                        addPacket.CharacterId,
                        addApplied,
                        operation: "add",
                        addDetail);
                    return addApplied;

                case RemoteUserPacketType.UserCoupleChairRecordRemove:
                    if (!RemoteUserPacketCodec.TryParsePortableChairRecordRemove(
                            payload,
                            out RemoteUserPortableChairRecordRemovePacket removePacket,
                            out string removeError))
                    {
                        message = removeError;
                        LastDispatchSummary = $"Rejected {DescribePacketKind(packetType)} from {normalizedSource}: {removeError}";
                        return false;
                    }

                    bool removeApplied = remoteUserPool.TryApplyPortableChairRecordRemove(removePacket, out string removeDetail);
                    if (removeApplied)
                    {
                        TotalRemoveCount++;
                    }

                    message = removeDetail;
                    LastDispatchSummary = BuildDispatchSummary(
                        normalizedSource,
                        removePacket.CharacterId,
                        removeApplied,
                        operation: "remove",
                        removeDetail);
                    return removeApplied;

                default:
                    message = $"Remote user packet type {packetType} is not a portable-chair record dispatch.";
                    LastDispatchSummary = message;
                    return false;
            }
        }

        public string DescribeStatus()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Packet-owned portable-chair record runtime: adds={0}, removes={1}. {2}",
                TotalAddCount,
                TotalRemoveCount,
                LastDispatchSummary);
        }

        private static string DescribePacketKind(int packetType)
        {
            return Enum.IsDefined(typeof(RemoteUserPacketType), packetType)
                ? ((RemoteUserPacketType)packetType).ToString()
                : $"packet {packetType.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string BuildDispatchSummary(
            string source,
            int characterId,
            bool applied,
            string operation,
            string detail)
        {
            string outcome = applied ? "Applied" : "Ignored";
            string ownerText = characterId > 0
                ? $" for character {characterId.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            return string.IsNullOrWhiteSpace(detail)
                ? $"{outcome} portable-chair record {operation} from {source}{ownerText}."
                : $"{outcome} portable-chair record {operation} from {source}{ownerText}: {detail}";
        }
    }
}
