using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HaCreator.MapSimulator.Physics;

using BinaryReader = MapleLib.PacketLib.PacketReader;
using BinaryWriter = MapleLib.PacketLib.PacketWriter;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketNpcPoolPacketKind
    {
        ImitateData = 84,
        UpdateLimitedDisableInfo = 85,
        EnterField = 311,
        LeaveField = 312,
        ChangeController = 313,
        Move = 314,
        UpdateLimitedInfo = 315,
        SetSpecialAction = 316,
        TemplatePacket = 317
    }

    internal readonly record struct PacketNpcEnterFieldPacket(
        int ObjectId,
        int TemplateId,
        int X,
        int Y,
        int MoveAction,
        int FootholdId,
        int Rx0,
        int Rx1,
        bool Enabled);

    internal readonly record struct PacketNpcLeaveFieldPacket(int ObjectId);

    internal readonly record struct PacketNpcChangeControllerPacket(
        bool LocalController,
        int ObjectId,
        PacketNpcEnterFieldPacket? LocalInit);

    internal readonly record struct PacketNpcMovePacket(
        int ObjectId,
        int OneTimeAction,
        int ChatIndex,
        byte[] MovePathPayload,
        IReadOnlyList<MovePathElement> MovePathElements);

    internal readonly record struct PacketNpcLimitedInfoPacket(int ObjectId, bool Enabled);

    internal readonly record struct PacketNpcLimitedDisableInfoPacket(IReadOnlyList<int> DisabledTemplateIds);

    internal readonly record struct PacketNpcSpecialActionPacket(int ObjectId, string ActionName);

    internal readonly record struct PacketNpcImitateEntry(
        int TemplateId,
        string Name,
        byte[] AvatarLookPayload);

    internal readonly record struct PacketNpcPoolApplyResult(bool Success, string Detail);

    internal sealed class PacketNpcPoolCallbacks
    {
        internal Func<PacketNpcEnterFieldPacket, int, PacketNpcPoolApplyResult> EnterField { get; init; }
        internal Func<PacketNpcLeaveFieldPacket, int, PacketNpcPoolApplyResult> LeaveField { get; init; }
        internal Func<PacketNpcChangeControllerPacket, int, PacketNpcPoolApplyResult> ChangeController { get; init; }
        internal Func<PacketNpcMovePacket, int, PacketNpcPoolApplyResult> Move { get; init; }
        internal Func<PacketNpcLimitedInfoPacket, int, PacketNpcPoolApplyResult> UpdateLimitedInfo { get; init; }
        internal Func<PacketNpcLimitedDisableInfoPacket, int, PacketNpcPoolApplyResult> UpdateLimitedDisableInfo { get; init; }
        internal Func<PacketNpcSpecialActionPacket, int, PacketNpcPoolApplyResult> SetSpecialAction { get; init; }
        internal Func<IReadOnlyList<PacketNpcImitateEntry>, int, PacketNpcPoolApplyResult> ImitateData { get; init; }
        internal Func<byte[], int, PacketNpcPoolApplyResult> TemplatePacket { get; init; }
    }

    internal sealed class PacketNpcPoolRuntime
    {
        private int _boundMapId = int.MinValue;
        private string _status = "Packet-owned NPC pool idle.";
        private readonly HashSet<int> _disabledTemplateIds = new();

        internal IReadOnlyCollection<int> DisabledTemplateIds => _disabledTemplateIds;

        internal void BindMap(int mapId)
        {
            _boundMapId = mapId;
            _status = mapId > 0
                ? $"Packet-owned NPC pool bound to map {mapId.ToString(CultureInfo.InvariantCulture)}."
                : "Packet-owned NPC pool idle.";
        }

        internal void Clear()
        {
            _disabledTemplateIds.Clear();
            _status = "Packet-owned NPC pool cleared.";
        }

        internal string DescribeStatus(int liveNpcCount)
        {
            string mapSuffix = _boundMapId > 0
                ? $" map={_boundMapId.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            string disabledSuffix = _disabledTemplateIds.Count > 0
                ? $" disabledTemplates={_disabledTemplateIds.Count.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            return $"{_status}{mapSuffix} live={Math.Max(0, liveNpcCount).ToString(CultureInfo.InvariantCulture)}{disabledSuffix}";
        }

        internal bool TryApplyPacket(
            PacketNpcPoolPacketKind kind,
            byte[] payload,
            int currentTick,
            PacketNpcPoolCallbacks callbacks,
            out string message)
        {
            callbacks ??= new PacketNpcPoolCallbacks();
            payload ??= Array.Empty<byte>();

            try
            {
                PacketNpcPoolApplyResult result = kind switch
                {
                    PacketNpcPoolPacketKind.EnterField => ApplyEnterField(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.LeaveField => ApplyLeaveField(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.ChangeController => ApplyChangeController(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.Move => ApplyMove(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.UpdateLimitedInfo => ApplyLimitedInfo(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.UpdateLimitedDisableInfo => ApplyLimitedDisableInfo(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.SetSpecialAction => ApplySpecialAction(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.ImitateData => ApplyImitateData(payload, currentTick, callbacks),
                    PacketNpcPoolPacketKind.TemplatePacket => callbacks.TemplatePacket?.Invoke(payload, currentTick)
                        ?? new PacketNpcPoolApplyResult(true, $"CNpcPool::OnNpcTemplatePacket retained {payload.Length.ToString(CultureInfo.InvariantCulture)} byte(s)."),
                    _ => new PacketNpcPoolApplyResult(false, $"Unsupported NPC pool packet kind {(int)kind}.")
                };

                message = result.Detail;
                if (result.Success && !string.IsNullOrWhiteSpace(result.Detail))
                {
                    _status = result.Detail;
                }

                return result.Success;
            }
            catch (EndOfStreamException ex)
            {
                message = $"Packet-owned NPC payload ended early: {ex.Message}";
                return false;
            }
            catch (IOException ex)
            {
                message = $"Packet-owned NPC payload could not be read: {ex.Message}";
                return false;
            }
            catch (ArgumentException ex)
            {
                message = $"Packet-owned NPC payload is invalid: {ex.Message}";
                return false;
            }
        }

        internal static byte[] BuildEnterFieldPayload(
            int objectId,
            int templateId,
            int x,
            int y,
            int moveAction,
            int footholdId,
            int rx0,
            int rx1,
            bool enabled)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            WriteInitTail(writer, templateId, x, y, moveAction, footholdId, rx0, rx1, enabled);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildChangeControllerPayload(
            bool localController,
            int objectId,
            int templateId = 0,
            int x = 0,
            int y = 0,
            int moveAction = 0,
            int footholdId = 0,
            int rx0 = 0,
            int rx1 = 0,
            bool enabled = true)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(localController ? (byte)1 : (byte)0);
            writer.Write(objectId);
            if (localController)
            {
                WriteInitTail(writer, templateId, x, y, moveAction, footholdId, rx0, rx1, enabled);
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildLeaveFieldPayload(int objectId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildMovePayload(int objectId, int oneTimeAction, int chatIndex, byte[] movePathPayload = null)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            writer.Write(EncodeSignedByte(oneTimeAction));
            writer.Write(EncodeSignedByte(chatIndex));
            if (movePathPayload?.Length > 0)
            {
                writer.Write(movePathPayload);
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildLimitedInfoPayload(int objectId, bool enabled)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            writer.Write(enabled ? (byte)1 : (byte)0);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildLimitedDisableInfoPayload(params int[] disabledTemplateIds)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            int count = Math.Clamp(disabledTemplateIds?.Length ?? 0, byte.MinValue, byte.MaxValue);
            writer.Write((byte)count);
            for (int i = 0; i < count; i++)
            {
                writer.Write(disabledTemplateIds[i]);
            }

            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildSpecialActionPayload(int objectId, string actionName)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            WriteMapleString(writer, actionName);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildImitateDataPayload(params PacketNpcImitateEntry[] entries)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            PacketNpcImitateEntry[] safeEntries = entries ?? Array.Empty<PacketNpcImitateEntry>();
            writer.Write((byte)Math.Clamp(safeEntries.Length, byte.MinValue, byte.MaxValue));
            for (int i = 0; i < safeEntries.Length && i <= byte.MaxValue; i++)
            {
                writer.Write(safeEntries[i].TemplateId);
                WriteMapleString(writer, safeEntries[i].Name);
                if (safeEntries[i].AvatarLookPayload?.Length > 0)
                {
                    writer.Write(safeEntries[i].AvatarLookPayload);
                }
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static PacketNpcPoolApplyResult ApplyEnterField(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int objectId = reader.ReadInt32();
            PacketNpcEnterFieldPacket packet = ReadInitTail(reader, objectId);
            EnsureNoTrailingBytes(stream, "NPC enter-field");
            return callbacks.EnterField?.Invoke(packet, currentTick)
                ?? new PacketNpcPoolApplyResult(false, "CNpcPool::OnNpcEnterField routed without a simulator handler.");
        }

        private static PacketNpcPoolApplyResult ApplyLeaveField(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketNpcLeaveFieldPacket packet = new(reader.ReadInt32());
            EnsureNoTrailingBytes(stream, "NPC leave-field");
            return callbacks.LeaveField?.Invoke(packet, currentTick)
                ?? new PacketNpcPoolApplyResult(false, "CNpcPool::OnNpcLeaveField routed without a simulator handler.");
        }

        private static PacketNpcPoolApplyResult ApplyChangeController(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            bool localController = reader.ReadByte() != 0;
            int objectId = reader.ReadInt32();
            PacketNpcEnterFieldPacket? init = null;
            if (localController)
            {
                init = ReadInitTail(reader, objectId);
            }

            EnsureNoTrailingBytes(stream, "NPC change-controller");
            PacketNpcChangeControllerPacket packet = new(localController, objectId, init);
            return callbacks.ChangeController?.Invoke(packet, currentTick)
                ?? new PacketNpcPoolApplyResult(false, "CNpcPool::OnNpcChangeController routed without a simulator handler.");
        }

        private static PacketNpcPoolApplyResult ApplyMove(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketNpcMovePacket packet = new(
                reader.ReadInt32(),
                ReadSignedByte(reader),
                ReadSignedByte(reader),
                ReadRemainingBytes(stream),
                Array.Empty<MovePathElement>());
            if (packet.MovePathPayload.Length > 0
                && CMovePathClientPacketCodec.TryDecode(
                    packet.MovePathPayload,
                    out IReadOnlyList<MovePathElement> movePathElements,
                    out _))
            {
                packet = packet with { MovePathElements = movePathElements };
            }

            return callbacks.Move?.Invoke(packet, currentTick)
                ?? new PacketNpcPoolApplyResult(false, "CNpcPool::OnNpcPacket/OnMove routed without a simulator handler.");
        }

        private static PacketNpcPoolApplyResult ApplyLimitedInfo(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketNpcLimitedInfoPacket packet = new(reader.ReadInt32(), reader.ReadByte() != 0);
            EnsureNoTrailingBytes(stream, "NPC limited-info");
            return callbacks.UpdateLimitedInfo?.Invoke(packet, currentTick)
                ?? new PacketNpcPoolApplyResult(false, "CNpc::OnUpdateLimitedInfo routed without a simulator handler.");
        }

        private PacketNpcPoolApplyResult ApplyLimitedDisableInfo(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int count = reader.ReadByte();
            var disabledTemplateIds = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                disabledTemplateIds.Add(reader.ReadInt32());
            }

            EnsureNoTrailingBytes(stream, "NPC limited-disable-info");
            _disabledTemplateIds.Clear();
            foreach (int templateId in disabledTemplateIds)
            {
                _disabledTemplateIds.Add(templateId);
            }

            return callbacks.UpdateLimitedDisableInfo?.Invoke(new PacketNpcLimitedDisableInfoPacket(disabledTemplateIds), currentTick)
                ?? new PacketNpcPoolApplyResult(true, $"CNpcPool::OnUpdateLimitedDisableInfo retained {disabledTemplateIds.Count.ToString(CultureInfo.InvariantCulture)} disabled NPC template id(s).");
        }

        private static PacketNpcPoolApplyResult ApplySpecialAction(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketNpcSpecialActionPacket packet = new(reader.ReadInt32(), ReadMapleString(reader));
            EnsureNoTrailingBytes(stream, "NPC special-action");
            return callbacks.SetSpecialAction?.Invoke(packet, currentTick)
                ?? new PacketNpcPoolApplyResult(false, "CNpc::OnSetSpecialAction routed without a simulator handler.");
        }

        private static PacketNpcPoolApplyResult ApplyImitateData(byte[] payload, int currentTick, PacketNpcPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            int count = reader.ReadByte();
            var entries = new List<PacketNpcImitateEntry>(count);
            for (int i = 0; i < count; i++)
            {
                int templateId = reader.ReadInt32();
                string name = ReadMapleString(reader);
                byte[] avatarPayload = i == count - 1
                    ? ReadRemainingBytes(stream)
                    : Array.Empty<byte>();
                entries.Add(new PacketNpcImitateEntry(templateId, name, avatarPayload));
            }

            return callbacks.ImitateData?.Invoke(entries, currentTick)
                ?? new PacketNpcPoolApplyResult(false, "CNpcPool::OnNpcImitateData routed without a simulator handler.");
        }

        private static PacketNpcEnterFieldPacket ReadInitTail(BinaryReader reader, int objectId)
        {
            return new PacketNpcEnterFieldPacket(
                objectId,
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadByte() != 0);
        }

        private static void WriteInitTail(
            BinaryWriter writer,
            int templateId,
            int x,
            int y,
            int moveAction,
            int footholdId,
            int rx0,
            int rx1,
            bool enabled)
        {
            writer.Write(templateId);
            writer.Write((short)x);
            writer.Write((short)y);
            writer.Write((byte)Math.Clamp(moveAction, byte.MinValue, byte.MaxValue));
            writer.Write((short)footholdId);
            writer.Write((short)rx0);
            writer.Write((short)rx1);
            writer.Write(enabled ? (byte)1 : (byte)0);
        }

        private static void EnsureNoTrailingBytes(Stream stream, string label)
        {
            long trailing = stream.Length - stream.Position;
            if (trailing > 0)
            {
                throw new InvalidDataException($"{label} payload has {trailing.ToString(CultureInfo.InvariantCulture)} trailing byte(s).");
            }
        }

        private static byte[] ReadRemainingBytes(Stream stream)
        {
            long remaining = Math.Max(0, stream.Length - stream.Position);
            if (remaining == 0)
            {
                return Array.Empty<byte>();
            }

            var bytes = new byte[remaining];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = stream.Read(bytes, offset, bytes.Length - offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Payload ended before all remaining bytes could be read.");
                }

                offset += read;
            }

            return bytes;
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            string text = value ?? string.Empty;
            byte[] bytes = Encoding.Default.GetBytes(text);
            writer.Write((short)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            short length = reader.ReadInt16();
            if (length < 0)
            {
                throw new InvalidDataException("Maple strings with negative lengths are not supported in this simulator-owned packet surface.");
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Maple string ended before its declared length.");
            }

            return Encoding.Default.GetString(bytes);
        }

        private static byte EncodeSignedByte(int value)
        {
            sbyte clamped = (sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
            return unchecked((byte)clamped);
        }

        private static int ReadSignedByte(BinaryReader reader)
        {
            return unchecked((sbyte)reader.ReadByte());
        }
    }
}
