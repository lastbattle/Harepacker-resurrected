using MapleLib.PacketLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    [Flags]
    internal enum SocialRoomEmployeePoolFlags : byte
    {
        None = 0,
        EnteredField = 1
    }

    internal sealed class SocialRoomEmployeePoolEntryState
    {
        internal SocialRoomEmployeePoolEntryState(int employerId)
        {
            EmployerId = Math.Max(0, employerId);
            NameTag = string.Empty;
            BalloonTitle = string.Empty;
        }

        internal int EmployerId { get; }
        internal SocialRoomEmployeePoolFlags Flags { get; set; }
        internal int TemplateId { get; set; }
        internal short WorldX { get; set; }
        internal short WorldY { get; set; }
        internal short FootholdId { get; set; }
        internal string NameTag { get; set; }
        internal byte MiniRoomType { get; set; }
        internal int MiniRoomSerial { get; set; }
        internal string BalloonTitle { get; set; }
        internal byte BalloonByte0 { get; set; }
        internal byte BalloonByte1 { get; set; }
        internal byte BalloonByte2 { get; set; }
        internal bool IsVisible => (Flags & SocialRoomEmployeePoolFlags.EnteredField) != 0;

        internal SocialRoomEmployeePoolEntrySnapshot ToSnapshot()
        {
            return new SocialRoomEmployeePoolEntrySnapshot
            {
                EmployerId = EmployerId,
                Flags = (byte)Flags,
                TemplateId = TemplateId,
                WorldX = WorldX,
                WorldY = WorldY,
                FootholdId = FootholdId,
                NameTag = NameTag,
                MiniRoomType = MiniRoomType,
                MiniRoomSerial = MiniRoomSerial,
                BalloonTitle = BalloonTitle,
                BalloonByte0 = BalloonByte0,
                BalloonByte1 = BalloonByte1,
                BalloonByte2 = BalloonByte2
            };
        }
    }

    internal readonly record struct SocialRoomEmployeeLegacyPacketState(
        bool HasPacketData,
        bool IsActorHidden,
        int EmployerId,
        int FootholdId,
        string NameTag,
        byte MiniRoomType,
        int MiniRoomSerial,
        string BalloonTitle,
        byte BalloonByte0,
        byte BalloonByte1,
        byte BalloonByte2,
        int TemplateId,
        int WorldX,
        int WorldY,
        bool HasWorldPosition);

    internal static class SocialRoomEmployeePoolCodec
    {
        internal readonly record struct EnterFieldPacket(
            int EmployerId,
            int TemplateId,
            short WorldX,
            short WorldY,
            short FootholdId,
            string NameTag,
            byte MiniRoomType,
            int MiniRoomSerial,
            string BalloonTitle,
            byte BalloonByte0,
            byte BalloonByte1,
            byte BalloonByte2);

        internal readonly record struct LeaveFieldPacket(int EmployerId);
        internal readonly record struct MiniRoomBalloonPacket(
            int EmployerId,
            byte MiniRoomType,
            int MiniRoomSerial,
            string BalloonTitle,
            byte BalloonByte0,
            byte BalloonByte1,
            byte BalloonByte2);

        internal static bool TryDecodeEmployerId(byte[] packetBytes, out int employerId, out string error)
        {
            employerId = 0;
            error = null;
            if (packetBytes == null || packetBytes.Length < sizeof(int))
            {
                error = "Employee packet payload is too short to decode the employer id.";
                return false;
            }

            try
            {
                PacketReader reader = new(packetBytes);
                employerId = reader.ReadInt();
                return true;
            }
            catch (EndOfStreamException)
            {
                error = $"Employee packet ended unexpectedly: {BitConverter.ToString(packetBytes ?? Array.Empty<byte>())}";
                return false;
            }
        }

        internal static bool TryDecodeEnterField(byte[] packetBytes, out EnterFieldPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (packetBytes == null || packetBytes.Length == 0)
            {
                error = "Employee field packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(packetBytes);
                int employerId = reader.ReadInt();
                int templateId = reader.ReadInt();
                short worldX = reader.ReadShort();
                short worldY = reader.ReadShort();
                short footholdId = reader.ReadShort();
                string nameTag = NormalizePacketText(reader.ReadMapleString());
                byte miniRoomType = reader.ReadByte();
                int miniRoomSerial = 0;
                string balloonTitle = string.Empty;
                byte balloonByte0 = 0;
                byte balloonByte1 = 0;
                byte balloonByte2 = 0;
                if (miniRoomType != 0)
                {
                    miniRoomSerial = reader.ReadInt();
                    balloonTitle = NormalizePacketText(reader.ReadMapleString());
                    balloonByte0 = reader.ReadByte();
                    balloonByte1 = reader.ReadByte();
                    balloonByte2 = reader.ReadByte();
                }

                packet = new EnterFieldPacket(
                    employerId,
                    templateId,
                    worldX,
                    worldY,
                    footholdId,
                    nameTag,
                    miniRoomType,
                    miniRoomSerial,
                    balloonTitle,
                    balloonByte0,
                    balloonByte1,
                    balloonByte2);
                return true;
            }
            catch (EndOfStreamException)
            {
                error = $"Employee field packet ended unexpectedly: {BitConverter.ToString(packetBytes)}";
                return false;
            }
        }

        internal static bool TryDecodeLeaveField(byte[] packetBytes, out LeaveFieldPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (!TryDecodeEmployerId(packetBytes, out int employerId, out error))
            {
                return false;
            }

            packet = new LeaveFieldPacket(employerId);
            return true;
        }

        internal static bool TryDecodeMiniRoomBalloon(byte[] packetBytes, out MiniRoomBalloonPacket packet, out string error)
        {
            packet = default;
            error = null;
            if (packetBytes == null || packetBytes.Length == 0)
            {
                error = "Employee mini-room balloon packet payload is empty.";
                return false;
            }

            try
            {
                PacketReader reader = new(packetBytes);
                int employerId = reader.ReadInt();
                byte miniRoomType = reader.ReadByte();
                int miniRoomSerial = 0;
                string balloonTitle = string.Empty;
                byte balloonByte0 = 0;
                byte balloonByte1 = 0;
                byte balloonByte2 = 0;
                if (miniRoomType != 0)
                {
                    miniRoomSerial = reader.ReadInt();
                    balloonTitle = NormalizePacketText(reader.ReadMapleString());
                    balloonByte0 = reader.ReadByte();
                    balloonByte1 = reader.ReadByte();
                    balloonByte2 = reader.ReadByte();
                }

                packet = new MiniRoomBalloonPacket(
                    employerId,
                    miniRoomType,
                    miniRoomSerial,
                    balloonTitle,
                    balloonByte0,
                    balloonByte1,
                    balloonByte2);
                return true;
            }
            catch (EndOfStreamException)
            {
                error = $"Employee mini-room balloon packet ended unexpectedly: {BitConverter.ToString(packetBytes)}";
                return false;
            }
        }

        private static string NormalizePacketText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    internal sealed class SocialRoomEmployeePoolRuntime
    {
        private readonly Dictionary<int, SocialRoomEmployeePoolEntryState> _entries = new();
        private int _preferredEmployerId;
        private int _lastTouchedEmployerId;

        internal int EntryCount => _entries.Count;
        internal bool HasEntries => _entries.Count > 0;
        internal int PreferredEmployerId => _preferredEmployerId;

        internal void Restore(
            IReadOnlyList<SocialRoomEmployeePoolEntrySnapshot> snapshots,
            SocialRoomEmployeeLegacyPacketState legacyState)
        {
            _entries.Clear();
            _lastTouchedEmployerId = 0;
            _preferredEmployerId = 0;

            if (snapshots?.Count > 0)
            {
                foreach (SocialRoomEmployeePoolEntrySnapshot snapshot in snapshots.Where(entry => entry != null))
                {
                    int employerId = Math.Max(0, snapshot.EmployerId);
                    if (employerId <= 0)
                    {
                        continue;
                    }

                    SocialRoomEmployeePoolEntryState state = new(employerId)
                    {
                        Flags = (SocialRoomEmployeePoolFlags)snapshot.Flags,
                        TemplateId = Math.Max(0, snapshot.TemplateId),
                        WorldX = snapshot.WorldX,
                        WorldY = snapshot.WorldY,
                        FootholdId = snapshot.FootholdId,
                        NameTag = snapshot.NameTag ?? string.Empty,
                        MiniRoomType = snapshot.MiniRoomType,
                        MiniRoomSerial = snapshot.MiniRoomSerial,
                        BalloonTitle = snapshot.BalloonTitle ?? string.Empty,
                        BalloonByte0 = snapshot.BalloonByte0,
                        BalloonByte1 = snapshot.BalloonByte1,
                        BalloonByte2 = snapshot.BalloonByte2
                    };
                    _entries[employerId] = state;
                    _lastTouchedEmployerId = employerId;
                }

                return;
            }

            if (!legacyState.HasPacketData
                || legacyState.IsActorHidden
                || legacyState.EmployerId <= 0)
            {
                return;
            }

            SocialRoomEmployeePoolEntryState legacyEntry = new(legacyState.EmployerId)
            {
                Flags = SocialRoomEmployeePoolFlags.EnteredField,
                TemplateId = Math.Max(0, legacyState.TemplateId),
                WorldX = (short)legacyState.WorldX,
                WorldY = (short)legacyState.WorldY,
                FootholdId = (short)legacyState.FootholdId,
                NameTag = legacyState.NameTag ?? string.Empty,
                MiniRoomType = legacyState.MiniRoomType,
                MiniRoomSerial = legacyState.MiniRoomSerial,
                BalloonTitle = legacyState.BalloonTitle ?? string.Empty,
                BalloonByte0 = legacyState.BalloonByte0,
                BalloonByte1 = legacyState.BalloonByte1,
                BalloonByte2 = legacyState.BalloonByte2
            };
            _entries[legacyEntry.EmployerId] = legacyEntry;
            _lastTouchedEmployerId = legacyEntry.EmployerId;
        }

        internal IReadOnlyList<SocialRoomEmployeePoolEntrySnapshot> BuildSnapshots()
        {
            return _entries.Values
                .OrderBy(entry => entry.EmployerId)
                .Select(entry => entry.ToSnapshot())
                .ToList();
        }

        internal void SetPreferredEmployerId(int employerId)
        {
            _preferredEmployerId = Math.Max(0, employerId);
        }

        internal bool TryGetPrimaryEntry(out SocialRoomEmployeePoolEntryState state)
        {
            state = null;
            if (_entries.Count == 0)
            {
                return false;
            }

            if (_preferredEmployerId > 0
                && _entries.TryGetValue(_preferredEmployerId, out SocialRoomEmployeePoolEntryState preferred)
                && preferred.IsVisible)
            {
                state = preferred;
                return true;
            }

            if (_lastTouchedEmployerId > 0
                && _entries.TryGetValue(_lastTouchedEmployerId, out SocialRoomEmployeePoolEntryState lastTouched)
                && lastTouched.IsVisible)
            {
                state = lastTouched;
                return true;
            }

            state = _entries.Values
                .OrderByDescending(entry => entry.IsVisible)
                .ThenBy(entry => entry.EmployerId)
                .FirstOrDefault(entry => entry.IsVisible);
            return state != null;
        }

        internal bool TryApplyEnterField(byte[] packetBytes, out string message)
        {
            message = null;
            if (!SocialRoomEmployeePoolCodec.TryDecodeEmployerId(packetBytes, out int employerId, out string employerError))
            {
                message = employerError;
                return false;
            }

            if (_entries.TryGetValue(employerId, out SocialRoomEmployeePoolEntryState existing))
            {
                existing.Flags |= SocialRoomEmployeePoolFlags.EnteredField;
                _lastTouchedEmployerId = employerId;
                message = $"Retained pooled employee entry for employer={employerId}; enter-field only refreshed the pool flag.";
                return true;
            }

            if (!SocialRoomEmployeePoolCodec.TryDecodeEnterField(packetBytes, out SocialRoomEmployeePoolCodec.EnterFieldPacket packet, out string error))
            {
                message = error;
                return false;
            }

            SocialRoomEmployeePoolEntryState state = new(packet.EmployerId)
            {
                Flags = SocialRoomEmployeePoolFlags.EnteredField,
                TemplateId = Math.Max(0, packet.TemplateId),
                WorldX = packet.WorldX,
                WorldY = packet.WorldY,
                FootholdId = packet.FootholdId,
                NameTag = packet.NameTag,
                MiniRoomType = packet.MiniRoomType,
                MiniRoomSerial = packet.MiniRoomSerial,
                BalloonTitle = packet.BalloonTitle,
                BalloonByte0 = packet.BalloonByte0,
                BalloonByte1 = packet.BalloonByte1,
                BalloonByte2 = packet.BalloonByte2
            };
            _entries[state.EmployerId] = state;
            _lastTouchedEmployerId = state.EmployerId;
            string displayTemplate = state.TemplateId > 0 ? state.TemplateId.ToString() : "legacy";
            string displayBalloon = string.IsNullOrWhiteSpace(state.BalloonTitle) ? "no balloon" : state.BalloonTitle;
            message =
                $"Applied pooled employee enter-field packet: employer={state.EmployerId}, template={displayTemplate}, world=({state.WorldX}, {state.WorldY}), owner={state.NameTag}, balloon={displayBalloon}.";
            return true;
        }

        internal bool TryApplyLeaveField(byte[] packetBytes, out string message)
        {
            message = null;
            if (!SocialRoomEmployeePoolCodec.TryDecodeLeaveField(packetBytes, out SocialRoomEmployeePoolCodec.LeaveFieldPacket packet, out string error))
            {
                message = error;
                return false;
            }

            if (!_entries.TryGetValue(packet.EmployerId, out SocialRoomEmployeePoolEntryState state))
            {
                message = $"Employee leave-field packet targeted employer={packet.EmployerId}, but no pooled entry exists.";
                return false;
            }

            state.Flags &= ~SocialRoomEmployeePoolFlags.EnteredField;
            string displayName = string.IsNullOrWhiteSpace(state.NameTag) ? "Owner" : state.NameTag;
            if (state.Flags == SocialRoomEmployeePoolFlags.None)
            {
                _entries.Remove(packet.EmployerId);
                message = $"Employee leave-field packet removed pooled employer={packet.EmployerId} ({displayName}) after the last pool flag cleared.";
                return true;
            }

            message = $"Employee leave-field packet cleared the enter-field flag for pooled employer={packet.EmployerId} ({displayName}).";
            return true;
        }

        internal bool TryApplyMiniRoomBalloon(byte[] packetBytes, out string message)
        {
            message = null;
            if (!SocialRoomEmployeePoolCodec.TryDecodeMiniRoomBalloon(packetBytes, out SocialRoomEmployeePoolCodec.MiniRoomBalloonPacket packet, out string error))
            {
                message = error;
                return false;
            }

            if (!_entries.TryGetValue(packet.EmployerId, out SocialRoomEmployeePoolEntryState state))
            {
                message = $"Employee mini-room balloon packet targeted employer={packet.EmployerId}, but no pooled entry exists.";
                return false;
            }

            state.MiniRoomType = packet.MiniRoomType;
            state.MiniRoomSerial = packet.MiniRoomSerial;
            state.BalloonTitle = packet.BalloonTitle;
            state.BalloonByte0 = packet.BalloonByte0;
            state.BalloonByte1 = packet.BalloonByte1;
            state.BalloonByte2 = packet.BalloonByte2;
            _lastTouchedEmployerId = packet.EmployerId;
            string displayBalloon = string.IsNullOrWhiteSpace(state.BalloonTitle) ? "cleared" : state.BalloonTitle;
            message =
                $"Applied pooled employee mini-room balloon packet: employer={packet.EmployerId}, type={state.MiniRoomType}, balloon={displayBalloon}.";
            return true;
        }
    }
}
