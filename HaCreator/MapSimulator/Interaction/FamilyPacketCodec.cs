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
}
