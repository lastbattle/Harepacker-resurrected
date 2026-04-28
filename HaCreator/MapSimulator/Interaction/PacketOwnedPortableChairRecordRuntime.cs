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
        public ushort LastObservedOpcode { get; private set; }
        public string LastDispatchSummary { get; private set; } = "Packet-owned portable-chair record runtime idle.";
        private readonly Dictionary<ushort, int> _observedAddCountByOpcode = new();
        private readonly Dictionary<ushort, int> _observedRemoveCountByOpcode = new();

        public void Clear()
        {
            TotalAddCount = 0;
            TotalRemoveCount = 0;
            LastObservedOpcode = 0;
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
                        RememberObservedOpcode(_observedAddCountByOpcode, observedOpcode);
                    }

                    LastObservedOpcode = observedOpcode;
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
                        RememberObservedOpcode(_observedRemoveCountByOpcode, observedOpcode);
                    }

                    LastObservedOpcode = observedOpcode;
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
