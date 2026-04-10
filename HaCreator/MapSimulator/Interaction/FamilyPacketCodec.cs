using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class FamilyPacketCodec
    {
        internal static bool TryDecodeInfoPayload(byte[] payload, out FamilyInfoPacketSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "Family info packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int currentReputation = reader.ReadInt();
                int totalReputation = reader.ReadInt();
                int todayReputation = reader.ReadInt();
                int childCount = reader.ReadShort();
                int childLimit = reader.ReadShort();
                int totalChildCount = reader.ReadShort();
                int bossId = reader.ReadInt();
                string familyName = reader.ReadMapleString();
                string precept = reader.ReadMapleString();
                int privilegeUseCount = reader.ReadInt();
                Dictionary<int, int> privilegeUses = new();
                for (int i = 0; i < privilegeUseCount; i++)
                {
                    privilegeUses[reader.ReadInt()] = Math.Max(0, reader.ReadInt());
                }

                snapshot = new FamilyInfoPacketSnapshot(
                    Math.Max(0, currentReputation),
                    Math.Max(0, totalReputation),
                    todayReputation,
                    Math.Max(0, childCount),
                    Math.Max(0, childLimit),
                    Math.Max(0, totalChildCount),
                    bossId,
                    familyName,
                    precept,
                    privilegeUses);
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Family info packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "Family info packet could not be read.";
                return false;
            }
        }

        internal static bool TryDecodeResultPayload(byte[] payload, out FamilyResultPacket packet, out string error)
        {
            packet = default;
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "Family result packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                packet = new FamilyResultPacket(reader.ReadInt(), reader.ReadInt());
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Family result packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "Family result packet could not be read.";
                return false;
            }
        }

        internal static bool TryDecodePrivilegeListPayload(byte[] payload, out IReadOnlyList<FamilyPrivilegePacketSnapshot> privileges, out string error)
        {
            privileges = Array.Empty<FamilyPrivilegePacketSnapshot>();
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "Family privilege-list packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int count = Math.Max(0, reader.ReadInt());
                List<FamilyPrivilegePacketSnapshot> results = new(count);
                for (int i = 0; i < count; i++)
                {
                    results.Add(new FamilyPrivilegePacketSnapshot(
                        reader.ReadByte(),
                        Math.Max(0, reader.ReadInt()),
                        Math.Max(0, reader.ReadInt()),
                        reader.ReadMapleString(),
                        reader.ReadMapleString()));
                }

                privileges = results;
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Family privilege-list packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "Family privilege-list packet could not be read.";
                return false;
            }
        }

        internal static bool TryDecodeSetPrivilegePayload(byte[] payload, out FamilyPrivilegeStatePacketSnapshot snapshot, out string error)
        {
            snapshot = default;
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "Family set-privilege packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                byte type = reader.ReadByte();
                if (type == 0)
                {
                    snapshot = new FamilyPrivilegeStatePacketSnapshot(0, 0, 0, 0, 0);
                    return true;
                }

                snapshot = new FamilyPrivilegeStatePacketSnapshot(
                    type,
                    reader.ReadInt(),
                    Math.Max(0, reader.ReadInt()),
                    Math.Max(0, reader.ReadInt()),
                    reader.ReadLong());
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Family set-privilege packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "Family set-privilege packet could not be read.";
                return false;
            }
        }

        internal static bool TryDecodeLocalChartPayload(byte[] payload, out FamilyLocalChartPacketSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (payload == null || payload.Length == 0)
            {
                error = "Family local-chart packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(payload);
                int focusMemberId = reader.ReadInt();
                int memberCount = Math.Max(0, reader.ReadInt());
                List<FamilyLocalChartMemberPacketSnapshot> members = new(memberCount);
                for (int i = 0; i < memberCount; i++)
                {
                    members.Add(new FamilyLocalChartMemberPacketSnapshot(
                        reader.ReadInt(),
                        reader.ReadInt(),
                        reader.ReadShort(),
                        reader.ReadByte(),
                        reader.ReadByte() != 0,
                        reader.ReadInt(),
                        reader.ReadInt(),
                        reader.ReadInt(),
                        reader.ReadInt(),
                        reader.ReadInt(),
                        reader.ReadInt(),
                        reader.ReadMapleString()));
                }

                IReadOnlyDictionary<int, int> statistics = ReadIntMap(ref reader);
                IReadOnlyDictionary<int, int> privilegeUses = ReadIntMap(ref reader);
                int juniorLimit = reader.ReadUShort();

                snapshot = new FamilyLocalChartPacketSnapshot(
                    focusMemberId,
                    members,
                    statistics,
                    privilegeUses,
                    juniorLimit);
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Family local-chart packet ended before decoding completed.";
                return false;
            }
            catch (IOException)
            {
                error = "Family local-chart packet could not be read.";
                return false;
            }
        }

        internal static bool TryDecodeOpcodeFramedPacket(byte[] rawPacket, out int opcode, out byte[] payload, out string error)
        {
            opcode = -1;
            payload = Array.Empty<byte>();
            error = string.Empty;

            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                error = "Family client packet must include a 2-byte opcode.";
                return false;
            }

            opcode = BinaryPrimitives.ReadUInt16LittleEndian(rawPacket);
            payload = rawPacket.Length == sizeof(ushort)
                ? Array.Empty<byte>()
                : rawPacket[sizeof(ushort)..];
            return true;
        }

        internal static bool TryParseHexBytes(string text, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "Family packet hex payload is missing.";
                return false;
            }

            string hex = text.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (hex.Length == 0 || (hex.Length % 2) != 0)
            {
                error = "Family packet hex payload must contain an even number of hexadecimal characters.";
                return false;
            }

            try
            {
                payload = Convert.FromHexString(hex);
                return true;
            }
            catch (FormatException)
            {
                error = "Family packet hex payload must contain only hexadecimal characters.";
                return false;
            }
        }

        internal static bool TryParsePayloadToken(string token, out byte[] payload, out string error)
        {
            payload = Array.Empty<byte>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                error = "Family packet payload is missing.";
                return false;
            }

            const string payloadHexPrefix = "payloadhex=";
            const string payloadBase64Prefix = "payloadb64=";
            if (token.StartsWith(payloadHexPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string hex = token[payloadHexPrefix.Length..].Trim();
                if (hex.Length == 0 || (hex.Length % 2) != 0)
                {
                    error = "payloadhex= must contain an even-length hexadecimal byte string.";
                    return false;
                }

                try
                {
                    payload = Convert.FromHexString(hex);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadhex= must contain only hexadecimal characters.";
                    return false;
                }
            }

            if (token.StartsWith(payloadBase64Prefix, StringComparison.OrdinalIgnoreCase))
            {
                string base64 = token[payloadBase64Prefix.Length..].Trim();
                if (base64.Length == 0)
                {
                    error = "payloadb64= must not be empty.";
                    return false;
                }

                try
                {
                    payload = Convert.FromBase64String(base64);
                    return true;
                }
                catch (FormatException)
                {
                    error = "payloadb64= must contain a valid base64 payload.";
                    return false;
                }
            }

            error = "Family packet payload must use payloadhex=.. or payloadb64=..";
            return false;
        }

        private static IReadOnlyDictionary<int, int> ReadIntMap(ref PacketReader reader)
        {
            int count = Math.Max(0, reader.ReadInt());
            Dictionary<int, int> values = new(count);
            for (int i = 0; i < count; i++)
            {
                values[reader.ReadInt()] = reader.ReadInt();
            }

            return values;
        }

        private ref struct PacketReader
        {
            private readonly ReadOnlySpan<byte> _payload;
            private int _offset;

            public PacketReader(ReadOnlySpan<byte> payload)
            {
                _payload = payload;
                _offset = 0;
            }

            public int ReadInt()
            {
                EnsureAvailable(sizeof(int));
                int value = BinaryPrimitives.ReadInt32LittleEndian(_payload.Slice(_offset, sizeof(int)));
                _offset += sizeof(int);
                return value;
            }

            public short ReadShort()
            {
                EnsureAvailable(sizeof(short));
                short value = BinaryPrimitives.ReadInt16LittleEndian(_payload.Slice(_offset, sizeof(short)));
                _offset += sizeof(short);
                return value;
            }

            public ushort ReadUShort()
            {
                EnsureAvailable(sizeof(ushort));
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_payload.Slice(_offset, sizeof(ushort)));
                _offset += sizeof(ushort);
                return value;
            }

            public byte ReadByte()
            {
                EnsureAvailable(sizeof(byte));
                return _payload[_offset++];
            }

            public long ReadLong()
            {
                EnsureAvailable(sizeof(long));
                long value = BinaryPrimitives.ReadInt64LittleEndian(_payload.Slice(_offset, sizeof(long)));
                _offset += sizeof(long);
                return value;
            }

            public string ReadMapleString()
            {
                ushort length = unchecked((ushort)ReadShort());
                EnsureAvailable(length);
                string value = Encoding.Default.GetString(_payload.Slice(_offset, length));
                _offset += length;
                return value;
            }

            private void EnsureAvailable(int count)
            {
                if (count < 0 || _offset + count > _payload.Length)
                {
                    throw new EndOfStreamException();
                }
            }
        }
    }

    internal sealed class FamilyInfoPacketSnapshot
    {
        internal FamilyInfoPacketSnapshot(
            int currentReputation,
            int totalReputation,
            int todayReputation,
            int childCount,
            int childLimit,
            int totalChildCount,
            int bossId,
            string familyName,
            string precept,
            IReadOnlyDictionary<int, int> privilegeUses)
        {
            CurrentReputation = currentReputation;
            TotalReputation = totalReputation;
            TodayReputation = todayReputation;
            ChildCount = childCount;
            ChildLimit = childLimit;
            TotalChildCount = totalChildCount;
            BossId = bossId;
            FamilyName = familyName ?? string.Empty;
            Precept = precept ?? string.Empty;
            PrivilegeUses = privilegeUses ?? new Dictionary<int, int>();
        }

        public int CurrentReputation { get; }
        public int TotalReputation { get; }
        public int TodayReputation { get; }
        public int ChildCount { get; }
        public int ChildLimit { get; }
        public int TotalChildCount { get; }
        public int BossId { get; }
        public string FamilyName { get; }
        public string Precept { get; }
        public IReadOnlyDictionary<int, int> PrivilegeUses { get; }
    }

    internal readonly record struct FamilyResultPacket(int Type, int Value);

    internal readonly record struct FamilyPrivilegePacketSnapshot(
        byte Type,
        int FameCost,
        int DayLimit,
        string Name,
        string Description);

    internal readonly record struct FamilyPrivilegeStatePacketSnapshot(
        byte Type,
        int Index,
        int IncrementExpRate,
        int IncrementDropRate,
        long EndTimeFileTimeUtc);

    internal sealed class FamilyLocalChartPacketSnapshot
    {
        internal FamilyLocalChartPacketSnapshot(
            int focusMemberId,
            IReadOnlyList<FamilyLocalChartMemberPacketSnapshot> members,
            IReadOnlyDictionary<int, int> statistics,
            IReadOnlyDictionary<int, int> privilegeUses,
            int juniorLimit)
        {
            FocusMemberId = focusMemberId;
            Members = members ?? Array.Empty<FamilyLocalChartMemberPacketSnapshot>();
            Statistics = statistics ?? new Dictionary<int, int>();
            PrivilegeUses = privilegeUses ?? new Dictionary<int, int>();
            JuniorLimit = Math.Max(0, juniorLimit);
        }

        public int FocusMemberId { get; }
        public IReadOnlyList<FamilyLocalChartMemberPacketSnapshot> Members { get; }
        public IReadOnlyDictionary<int, int> Statistics { get; }
        public IReadOnlyDictionary<int, int> PrivilegeUses { get; }
        public int JuniorLimit { get; }
    }

    internal readonly record struct FamilyLocalChartMemberPacketSnapshot(
        int CharacterId,
        int ParentId,
        short JobId,
        byte Level,
        bool IsOnline,
        int FamousPoint,
        int TotalFamousPoint,
        int TodayParentPoint,
        int TodayGrandParentPoint,
        int ChannelId,
        int LoginMinutes,
        string Name);
}
