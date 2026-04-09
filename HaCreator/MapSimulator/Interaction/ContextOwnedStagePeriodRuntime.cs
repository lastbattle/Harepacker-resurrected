using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class ContextOwnedStagePeriodRuntime
    {
        private int _boundMapId = int.MinValue;
        private string _currentStagePeriod = string.Empty;
        private byte _currentMode;
        private string _status = "Context-owned stage-period transition idle.";

        internal void BindMap(int mapId)
        {
            _boundMapId = mapId;
        }

        internal void Clear()
        {
            _currentStagePeriod = string.Empty;
            _currentMode = 0;
            _status = "Context-owned stage-period transition cleared.";
        }

        internal string DescribeStatus()
        {
            string mapSuffix = _boundMapId > 0
                ? $" map={_boundMapId.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty;
            string currentSuffix = string.IsNullOrWhiteSpace(_currentStagePeriod)
                ? " current=(none)"
                : $" current='{_currentStagePeriod}' mode={_currentMode.ToString(CultureInfo.InvariantCulture)}";
            return $"{_status}{currentSuffix}{mapSuffix}";
        }

        internal bool TryApplyPacket(
            byte[] payload,
            int currentTick,
            ContextOwnedStagePeriodCallbacks callbacks,
            out string message)
        {
            callbacks ??= new ContextOwnedStagePeriodCallbacks();
            if (!TryDecodePayload(payload, out PacketStagePeriodChangePacket packet, out string error))
            {
                message = error;
                return false;
            }

            ContextOwnedStagePeriodValidationResult validation = callbacks.ValidateStagePeriodChange?.Invoke(packet)
                ?? ContextOwnedStagePeriodValidationResult.Accepted();
            if (!validation.Success)
            {
                _status = validation.Detail
                    ?? $"CWvsContext::OnStageChange rejected '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)} because CStageSystem::BuildCacheData would fail for that stage-theme cache key.";
                message = _status;
                return false;
            }

            if (string.Equals(_currentStagePeriod, packet.StagePeriod, StringComparison.Ordinal)
                && _currentMode == packet.Mode)
            {
                _status = $"CWvsContext::OnStageChange decoded '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)}, but the live stage-period cache was already current.";
                message = _status;
                return true;
            }

            _currentStagePeriod = packet.StagePeriod;
            _currentMode = packet.Mode;
            _status = callbacks.ApplyStagePeriodChange?.Invoke(packet, currentTick)
                ?? $"CWvsContext::OnStageChange decoded '{packet.StagePeriod}' mode {packet.Mode.ToString(CultureInfo.InvariantCulture)}, but the simulator has no context-owned stage-period handler.";
            message = _status;
            return true;
        }

        internal static byte[] BuildPayload(string stagePeriod, byte mode)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            WriteMapleString(writer, stagePeriod);
            writer.Write(mode);
            writer.Flush();
            return stream.ToArray();
        }

        internal static bool TryDecodePayload(byte[] payload, out PacketStagePeriodChangePacket packet, out string error)
        {
            packet = default;
            error = null;
            payload ??= Array.Empty<byte>();

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                string stagePeriod = ReadMapleString(reader);
                if (string.IsNullOrWhiteSpace(stagePeriod))
                {
                    error = "Stage-period payload decoded an empty Maple string.";
                    return false;
                }

                byte mode = reader.ReadByte();
                long remaining = stream.Length - stream.Position;
                if (remaining > 0)
                {
                    error = $"Stage-period payload has {remaining.ToString(CultureInfo.InvariantCulture)} trailing byte(s).";
                    return false;
                }

                packet = new PacketStagePeriodChangePacket(stagePeriod, mode);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is InvalidDataException)
            {
                error = $"Stage-period payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static void WriteMapleString(BinaryWriter writer, string value)
        {
            string text = value ?? string.Empty;
            byte[] encoded = Encoding.Default.GetBytes(text);
            writer.Write((short)encoded.Length);
            writer.Write(encoded);
        }

        private static string ReadMapleString(BinaryReader reader)
        {
            short length = reader.ReadInt16();
            if (length < 0)
            {
                throw new InvalidDataException("Maple strings with negative lengths are not supported in the context-owned stage-period runtime.");
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Stage-period Maple string ended before its declared length.");
            }

            return Encoding.Default.GetString(bytes);
        }
    }

    internal sealed class ContextOwnedStagePeriodCallbacks
    {
        internal Func<PacketStagePeriodChangePacket, int, string> ApplyStagePeriodChange { get; init; }
        internal Func<PacketStagePeriodChangePacket, ContextOwnedStagePeriodValidationResult> ValidateStagePeriodChange { get; init; }
    }

    internal readonly record struct ContextOwnedStagePeriodValidationResult(bool Success, string Detail)
    {
        internal static ContextOwnedStagePeriodValidationResult Accepted(string detail = null)
        {
            return new(true, detail);
        }

        internal static ContextOwnedStagePeriodValidationResult Rejected(string detail)
        {
            return new(false, detail);
        }
    }

    internal readonly record struct PacketStagePeriodChangePacket(string StagePeriod, byte Mode);
}
