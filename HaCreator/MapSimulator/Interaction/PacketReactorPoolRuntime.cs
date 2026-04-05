using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal enum PacketReactorPoolPacketKind
    {
        ChangeState = 334,
        Move = 335,
        EnterField = 336,
        LeaveField = 337
    }

    internal readonly record struct PacketReactorEnterFieldPacket(
        int ObjectId,
        string ReactorTemplateId,
        int InitialState,
        int X,
        int Y,
        bool Flip,
        string Name);

    internal readonly record struct PacketReactorChangeStatePacket(
        int ObjectId,
        int State,
        int X,
        int Y,
        int HitStartDelayMs,
        int ProperEventIndex,
        int StateEndDelayTicks);

    internal readonly record struct PacketReactorMovePacket(
        int ObjectId,
        int X,
        int Y);

    internal readonly record struct PacketReactorLeaveFieldPacket(
        int ObjectId,
        int State,
        int X,
        int Y);

    internal readonly record struct PacketReactorPoolApplyResult(
        bool Success,
        string Detail);

    internal sealed class PacketReactorPoolCallbacks
    {
        internal Func<PacketReactorEnterFieldPacket, int, PacketReactorPoolApplyResult> EnterField { get; init; }
        internal Func<PacketReactorChangeStatePacket, int, PacketReactorPoolApplyResult> ChangeState { get; init; }
        internal Func<PacketReactorMovePacket, int, PacketReactorPoolApplyResult> Move { get; init; }
        internal Func<PacketReactorLeaveFieldPacket, int, PacketReactorPoolApplyResult> LeaveField { get; init; }
    }

    internal sealed class PacketReactorPoolRuntime
    {
        private int _boundMapId = int.MinValue;
        private string _status = "Packet-owned reactor pool idle.";

        internal void BindMap(int mapId)
        {
            _boundMapId = mapId;
            _status = mapId > 0
                ? $"Packet-owned reactor pool bound to map {mapId.ToString(CultureInfo.InvariantCulture)}."
                : "Packet-owned reactor pool idle.";
        }

        internal void Clear()
        {
            _status = "Packet-owned reactor pool cleared.";
        }

        internal string DescribeStatus()
        {
            string mapSuffix = _boundMapId > 0
                ? $" map={_boundMapId.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            return $"{_status}{mapSuffix}";
        }

        internal bool TryApplyPacket(
            PacketReactorPoolPacketKind kind,
            byte[] payload,
            int currentTick,
            PacketReactorPoolCallbacks callbacks,
            out string message)
        {
            callbacks ??= new PacketReactorPoolCallbacks();
            payload ??= Array.Empty<byte>();

            try
            {
                PacketReactorPoolApplyResult applyResult = kind switch
                {
                    PacketReactorPoolPacketKind.EnterField => ApplyEnterField(payload, currentTick, callbacks),
                    PacketReactorPoolPacketKind.ChangeState => ApplyChangeState(payload, currentTick, callbacks),
                    PacketReactorPoolPacketKind.Move => ApplyMove(payload, currentTick, callbacks),
                    PacketReactorPoolPacketKind.LeaveField => ApplyLeaveField(payload, currentTick, callbacks),
                    _ => new PacketReactorPoolApplyResult(false, $"Unsupported reactor pool packet kind {(int)kind}.")
                };

                message = applyResult.Detail;
                if (applyResult.Success && !string.IsNullOrWhiteSpace(applyResult.Detail))
                {
                    _status = applyResult.Detail;
                }

                return applyResult.Success;
            }
            catch (EndOfStreamException ex)
            {
                message = $"Packet-owned reactor payload ended early: {ex.Message}";
                return false;
            }
            catch (IOException ex)
            {
                message = $"Packet-owned reactor payload could not be read: {ex.Message}";
                return false;
            }
            catch (ArgumentException ex)
            {
                message = $"Packet-owned reactor payload is invalid: {ex.Message}";
                return false;
            }
        }

        internal static byte[] BuildEnterFieldPayload(
            int objectId,
            int reactorTemplateId,
            int initialState,
            int x,
            int y,
            bool flip,
            string name)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            writer.Write(reactorTemplateId);
            writer.Write((byte)Math.Clamp(initialState, byte.MinValue, byte.MaxValue));
            writer.Write((short)x);
            writer.Write((short)y);
            writer.Write(flip ? (byte)1 : (byte)0);
            WriteMapleString(writer, name);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildChangeStatePayload(
            int objectId,
            int state,
            int x,
            int y,
            int hitStartDelayMs,
            int properEventIndex,
            int stateEndDelayTicks)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            writer.Write((byte)Math.Clamp(state, byte.MinValue, byte.MaxValue));
            writer.Write((short)x);
            writer.Write((short)y);
            writer.Write((ushort)Math.Clamp(hitStartDelayMs, ushort.MinValue, ushort.MaxValue));
            writer.Write((byte)Math.Clamp(properEventIndex, byte.MinValue, byte.MaxValue));
            writer.Write((byte)Math.Clamp(stateEndDelayTicks, byte.MinValue, byte.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildMovePayload(int objectId, int x, int y)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            writer.Write((short)x);
            writer.Write((short)y);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildLeaveFieldPayload(int objectId, int state, int x, int y)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(objectId);
            writer.Write((byte)Math.Clamp(state, byte.MinValue, byte.MaxValue));
            writer.Write((short)x);
            writer.Write((short)y);
            writer.Flush();
            return stream.ToArray();
        }

        private static PacketReactorPoolApplyResult ApplyEnterField(byte[] payload, int currentTick, PacketReactorPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketReactorEnterFieldPacket packet = new(
                reader.ReadInt32(),
                reader.ReadInt32().ToString(CultureInfo.InvariantCulture),
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadByte() != 0,
                ReadMapleString(reader));
            EnsureNoTrailingBytes(stream, "reactor enter-field");

            return callbacks.EnterField?.Invoke(packet, currentTick)
                ?? new PacketReactorPoolApplyResult(false, "CReactorPool::OnReactorEnterField routed without a simulator handler.");
        }

        private static PacketReactorPoolApplyResult ApplyChangeState(byte[] payload, int currentTick, PacketReactorPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketReactorChangeStatePacket packet = new(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadInt16(),
                reader.ReadUInt16(),
                reader.ReadByte(),
                reader.ReadByte());
            EnsureNoTrailingBytes(stream, "reactor change-state");

            return callbacks.ChangeState?.Invoke(packet, currentTick)
                ?? new PacketReactorPoolApplyResult(false, "CReactorPool::OnReactorChangeState routed without a simulator handler.");
        }

        private static PacketReactorPoolApplyResult ApplyMove(byte[] payload, int currentTick, PacketReactorPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketReactorMovePacket packet = new(
                reader.ReadInt32(),
                reader.ReadInt16(),
                reader.ReadInt16());
            EnsureNoTrailingBytes(stream, "reactor move");

            return callbacks.Move?.Invoke(packet, currentTick)
                ?? new PacketReactorPoolApplyResult(false, "CReactorPool::OnReactorMove routed without a simulator handler.");
        }

        private static PacketReactorPoolApplyResult ApplyLeaveField(byte[] payload, int currentTick, PacketReactorPoolCallbacks callbacks)
        {
            using MemoryStream stream = new(payload, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            PacketReactorLeaveFieldPacket packet = new(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadInt16(),
                reader.ReadInt16());
            EnsureNoTrailingBytes(stream, "reactor leave-field");

            return callbacks.LeaveField?.Invoke(packet, currentTick)
                ?? new PacketReactorPoolApplyResult(false, "CReactorPool::OnReactorLeaveField routed without a simulator handler.");
        }

        private static void EnsureNoTrailingBytes(Stream stream, string label)
        {
            long trailing = stream.Length - stream.Position;
            if (trailing > 0)
            {
                throw new InvalidDataException($"{label} payload has {trailing.ToString(CultureInfo.InvariantCulture)} trailing byte(s).");
            }
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

            if (length == 0)
            {
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Maple string ended before its declared length.");
            }

            return Encoding.Default.GetString(bytes);
        }
    }
}
