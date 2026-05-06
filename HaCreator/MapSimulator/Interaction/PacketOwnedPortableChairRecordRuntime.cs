using HaCreator.MapSimulator.Pools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
        public int TotalRejectedCount { get; private set; }
        public ushort LastObservedOpcode { get; private set; }
        public int LastOwnerCharacterId { get; private set; }
        public int LastPayloadLength { get; private set; }
        public string LastOperation { get; private set; } = "none";
        public string LastSource { get; private set; } = "none";
        public string LastDispatchSummary { get; private set; } = "Packet-owned portable-chair record runtime idle.";
        private readonly Dictionary<ushort, int> _observedAddCountByOpcode = new();
        private readonly Dictionary<ushort, int> _observedRemoveCountByOpcode = new();

        public void Clear()
        {
            TotalAddCount = 0;
            TotalRemoveCount = 0;
            TotalRejectedCount = 0;
            LastObservedOpcode = 0;
            LastOwnerCharacterId = 0;
            LastPayloadLength = 0;
            LastOperation = "none";
            LastSource = "none";
            _observedAddCountByOpcode.Clear();
            _observedRemoveCountByOpcode.Clear();
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
            ushort observedOpcode = ResolveObservedOpcodeForParity(normalizedSource);
            LastObservedOpcode = observedOpcode;
            LastPayloadLength = payload.Length;
            LastSource = normalizedSource;

            switch ((RemoteUserPacketType)packetType)
            {
                case RemoteUserPacketType.UserCoupleChairRecordAdd:
                    LastOperation = "add";
                    if (!RemoteUserPacketCodec.TryParsePortableChairRecordAdd(
                            payload,
                            out RemoteUserPortableChairRecordAddPacket addPacket,
                            out string addError))
                    {
                        TotalRejectedCount++;
                        message = addError;
                        LastDispatchSummary = $"Rejected {DescribePacketKind(packetType)} from {normalizedSource}: {addError}";
                        return false;
                    }

                    LastOwnerCharacterId = addPacket.CharacterId;
                    bool addApplied = remoteUserPool.TryApplyPortableChairRecordAdd(addPacket, out string addDetail);
                    if (addApplied)
                    {
                        TotalAddCount++;
                        RememberObservedOpcode(_observedAddCountByOpcode, observedOpcode);
                    }
                    else
                    {
                        TotalRejectedCount++;
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
                    LastOperation = "remove";
                    if (!RemoteUserPacketCodec.TryParsePortableChairRecordRemove(
                            payload,
                            out RemoteUserPortableChairRecordRemovePacket removePacket,
                            out string removeError))
                    {
                        TotalRejectedCount++;
                        message = removeError;
                        LastDispatchSummary = $"Rejected {DescribePacketKind(packetType)} from {normalizedSource}: {removeError}";
                        return false;
                    }

                    LastOwnerCharacterId = removePacket.CharacterId;
                    bool removeApplied = remoteUserPool.TryApplyPortableChairRecordRemove(removePacket, out string removeDetail);
                    if (removeApplied)
                    {
                        TotalRemoveCount++;
                        RememberObservedOpcode(_observedRemoveCountByOpcode, observedOpcode);
                    }
                    else
                    {
                        TotalRejectedCount++;
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
                "Packet-owned portable-chair record runtime: adds={0}, removes={1}, rejected={2}, last={3}@{4} owner={5} payloadBytes={6} source={7}. {8}",
                TotalAddCount,
                TotalRemoveCount,
                TotalRejectedCount,
                LastOperation,
                LastObservedOpcode == 0 ? "none" : LastObservedOpcode.ToString(CultureInfo.InvariantCulture),
                LastOwnerCharacterId == 0 ? "none" : LastOwnerCharacterId.ToString(CultureInfo.InvariantCulture),
                LastPayloadLength,
                LastSource,
                $"{DescribeObservedOpcodes()} {LastDispatchSummary}");
        }

        internal static ushort ResolveObservedOpcodeForParity(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return 0;
            }

            const string opcodeToken = "opcode=";
            string[] segments = source.Split(new[] { ' ', ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                string segment = segments[i].Trim();
                if (segment.StartsWith(opcodeToken, StringComparison.OrdinalIgnoreCase)
                    && TryParseOpcode(segment.Substring(opcodeToken.Length), out ushort opcode))
                {
                    return opcode;
                }
            }

            return 0;
        }

        private static bool TryParseOpcode(string text, out ushort opcode)
        {
            opcode = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out opcode);
            }

            return ushort.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out opcode);
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

        private static void RememberObservedOpcode(Dictionary<ushort, int> countsByOpcode, ushort opcode)
        {
            if (opcode == 0)
            {
                return;
            }

            countsByOpcode.TryGetValue(opcode, out int count);
            countsByOpcode[opcode] = count + 1;
        }

        private string DescribeObservedOpcodes()
        {
            string addOpcodes = DescribeObservedOpcodeCounts(_observedAddCountByOpcode);
            string removeOpcodes = DescribeObservedOpcodeCounts(_observedRemoveCountByOpcode);
            string lastOpcode = LastObservedOpcode == 0
                ? "none"
                : LastObservedOpcode.ToString(CultureInfo.InvariantCulture);
            return $"Observed live opcodes: last={lastOpcode}, add={addOpcodes}, remove={removeOpcodes}.";
        }

        private static string DescribeObservedOpcodeCounts(Dictionary<ushort, int> countsByOpcode)
        {
            if (countsByOpcode.Count == 0)
            {
                return "none";
            }

            return string.Join(
                "|",
                countsByOpcode
                    .OrderBy(entry => entry.Key)
                    .Select(entry => $"{entry.Key.ToString(CultureInfo.InvariantCulture)}:{entry.Value.ToString(CultureInfo.InvariantCulture)}"));
        }
    }
}
