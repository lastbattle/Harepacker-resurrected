using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketStageTransitionRuntime
    {
        private int _boundMapId = int.MinValue;
        private string _stageStatus = "Packet-owned stage transition idle.";
        private string _mapLoadStatus = "Packet-owned map-load presentation idle.";

        internal void BindMap(int mapId)
        {
            _boundMapId = mapId;
            _mapLoadStatus = mapId > 0
                ? $"Packet-owned map-load presentation bound to map {mapId}."
                : "Packet-owned map-load presentation idle.";
        }

        internal void Clear()
        {
            _stageStatus = "Packet-owned stage transition cleared.";
            _mapLoadStatus = "Packet-owned map-load presentation cleared.";
        }

        internal string DescribeStatus()
        {
            string mapSuffix = _boundMapId > 0
                ? $" map={_boundMapId}"
                : string.Empty;
            return $"{_stageStatus} {_mapLoadStatus}{mapSuffix}";
        }

        internal bool TryApplyPacket(int packetType, byte[] payload, int currentTick, PacketStageTransitionCallbacks callbacks, out string message)
        {
            payload ??= Array.Empty<byte>();
            callbacks ??= new PacketStageTransitionCallbacks();

            switch (packetType)
            {
                case 141:
                    return TryApplySetField(payload, callbacks, out message);
                case 142:
                    _stageStatus = callbacks.OpenItc?.Invoke() ?? "CStage::OnSetITC routed without a simulator handler.";
                    message = _stageStatus;
                    return true;
                case 143:
                    _stageStatus = callbacks.OpenCashShop?.Invoke() ?? "CStage::OnSetCashShop routed without a simulator handler.";
                    message = _stageStatus;
                    return true;
                case 144:
                    return TryApplySetBackEffect(payload, currentTick, callbacks, out message);
                case 145:
                    return TryApplySetMapObjectVisible(payload, callbacks, out message);
                case 146:
                    _mapLoadStatus = callbacks.ClearBackEffect?.Invoke() ?? "CMapLoadable::OnClearBackEffect routed without a simulator handler.";
                    message = _mapLoadStatus;
                    return true;
                default:
                    message = $"Unsupported stage or map-load packet type {packetType}.";
                    return false;
            }
        }

        internal static byte[] BuildOfficialSetFieldPayload(
            int mapId,
            byte portalIndex = 0,
            int channelId = 0,
            int oldDriverId = 0,
            byte fieldKey = 0,
            int hp = 0,
            bool chaseEnabled = false,
            int chaseX = 0,
            int chaseY = 0,
            long serverFileTime = 0)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((short)0); // CClientOptMan::DecodeOpt count
            writer.Write(channelId);
            writer.Write(oldDriverId);
            writer.Write(fieldKey);
            writer.Write((byte)0); // bCharacterData
            writer.Write((short)0); // notifier entry count
            writer.Write((byte)0); // revive flag
            writer.Write(mapId);
            writer.Write(portalIndex);
            writer.Write(hp);
            writer.Write(chaseEnabled ? (byte)1 : (byte)0);
            if (chaseEnabled)
            {
                writer.Write(chaseX);
                writer.Write(chaseY);
            }

            writer.Write(serverFileTime);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildSyntheticSetFieldPayload(int mapId, string portalName)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(mapId);
            WriteMapleString(writer, portalName);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildBackEffectPayload(byte effect, int fieldId, byte pageId, int durationMs)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(effect);
            writer.Write(fieldId);
            writer.Write(pageId);
            writer.Write(durationMs);
            writer.Flush();
            return stream.ToArray();
        }

        internal static byte[] BuildMapObjectVisiblePayload(params (string name, bool visible)[] entries)
        {
            entries ??= Array.Empty<(string name, bool visible)>();
            if (entries.Length > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(entries), "Map-object visibility payloads support at most 255 entries.");
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((byte)entries.Length);
            foreach ((string name, bool visible) in entries)
            {
                WriteMapleString(writer, name);
                writer.Write(visible ? (byte)1 : (byte)0);
            }

            writer.Flush();
            return stream.ToArray();
        }

        private bool TryApplySetField(byte[] payload, PacketStageTransitionCallbacks callbacks, out string message)
        {
            if (TryDecodeOfficialSetFieldPayload(payload, out PacketSetFieldPacket officialPacket, out string officialDecodeError))
            {
                if (!officialPacket.SupportsFieldTransfer)
                {
                    string notifierSuffix = FormatNotifierSuffix(officialPacket.NotifierTitle, officialPacket.NotifierLines);
                    _stageStatus = $"CStage::OnSetField decoded the official character-data branch for channel {officialPacket.ChannelId}, but CharacterData field entry is not modeled yet{notifierSuffix}.";
                    message = _stageStatus;
                    return false;
                }

                PacketStageFieldTransferRequest request = new(
                    officialPacket.FieldId,
                    null,
                    officialPacket.PortalIndex,
                    "CStage::OnSetField");
                bool queued = callbacks.QueueFieldTransfer?.Invoke(request) == true;
                string notifierSummary = FormatNotifierSuffix(officialPacket.NotifierTitle, officialPacket.NotifierLines);
                _stageStatus = queued
                    ? $"CStage::OnSetField queued map {officialPacket.FieldId}{FormatPortalIndexSuffix(officialPacket.PortalIndex)} from the official payload (channel {officialPacket.ChannelId}, fieldKey {officialPacket.FieldKey}){notifierSummary}."
                    : $"CStage::OnSetField decoded map {officialPacket.FieldId}{FormatPortalIndexSuffix(officialPacket.PortalIndex)} from the official payload, but the simulator could not queue the transfer.";
                message = _stageStatus;
                return queued;
            }

            if (!TryDecodeSyntheticSetFieldPayload(payload, out int mapId, out string portalName, out string decodeError))
            {
                message = officialDecodeError != null
                    ? $"CStage::OnSetField payload could not be decoded as the official or legacy helper layout. {officialDecodeError}"
                    : $"CStage::OnSetField payload could not be decoded. {decodeError}";
                return false;
            }

            bool legacyQueued = callbacks.QueueFieldTransfer?.Invoke(new PacketStageFieldTransferRequest(mapId, portalName, -1, "synthetic-helper")) == true;
            _stageStatus = legacyQueued
                ? $"CStage::OnSetField queued map {mapId}{FormatPortalSuffix(portalName)} from the legacy helper payload."
                : $"CStage::OnSetField ignored map {mapId}{FormatPortalSuffix(portalName)} because the simulator could not queue the transfer.";
            message = _stageStatus;
            return legacyQueued;
        }

        private bool TryApplySetBackEffect(byte[] payload, int currentTick, PacketStageTransitionCallbacks callbacks, out string message)
        {
            if (!TryDecodeBackEffectPayload(payload, out PacketBackEffectPacket packet, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            string detail = callbacks.ApplyBackEffect?.Invoke(packet, currentTick)
                ?? "CMapLoadable::OnSetBackEffect routed without a simulator handler.";
            _mapLoadStatus = detail;
            message = detail;
            return true;
        }

        private bool TryApplySetMapObjectVisible(byte[] payload, PacketStageTransitionCallbacks callbacks, out string message)
        {
            if (!TryDecodeMapObjectVisiblePayload(payload, out IReadOnlyList<PacketMapObjectVisibilityEntry> entries, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            int matchedObjects = 0;
            foreach (PacketMapObjectVisibilityEntry entry in entries)
            {
                matchedObjects += Math.Max(0, callbacks.ApplyMapObjectVisibility?.Invoke(entry.Name, entry.Visible) ?? 0);
            }

            string sample = entries.Count == 0
                ? "none"
                : string.Join(", ", entries.Take(3).Select(static entry => $"{entry.Name}={(entry.Visible ? "on" : "off")}"));
            _mapLoadStatus = $"CMapLoadable::OnSetMapObjectVisible applied {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}; matched {matchedObjects} object(s) [{sample}].";
            message = _mapLoadStatus;
            return true;
        }

        private static bool TryDecodeSyntheticSetFieldPayload(byte[] payload, out int mapId, out string portalName, out string error)
        {
            mapId = 0;
            portalName = string.Empty;
            error = "Use `/stagepacket field <mapId> [portal]` or the synthetic stage packet helper payload.";
            payload ??= Array.Empty<byte>();

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                if (stream.Length < sizeof(int))
                {
                    error = "CStage::OnSetField payload is too short for the simulator-owned map-id handoff.";
                    return false;
                }

                mapId = reader.ReadInt32();
                portalName = ReadMapleString(reader);
                if (stream.Position != stream.Length)
                {
                    error = $"CStage::OnSetField payload has {stream.Length - stream.Position} trailing byte(s).";
                    return false;
                }

                if (mapId <= 0)
                {
                    error = $"CStage::OnSetField payload contains an invalid map id: {mapId.ToString(CultureInfo.InvariantCulture)}.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException)
            {
                error = $"CStage::OnSetField payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodeOfficialSetFieldPayload(byte[] payload, out PacketSetFieldPacket packet, out string error)
        {
            packet = default;
            error = null;
            payload ??= Array.Empty<byte>();

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                ushort optionCount = reader.ReadUInt16();
                Dictionary<uint, int> clientOptions = new(optionCount);
                for (int i = 0; i < optionCount; i++)
                {
                    clientOptions[reader.ReadUInt32()] = reader.ReadInt32();
                }

                int channelId = reader.ReadInt32();
                int oldDriverId = reader.ReadInt32();
                byte fieldKey = reader.ReadByte();
                bool hasCharacterData = reader.ReadByte() != 0;
                ushort notifierEntryCount = reader.ReadUInt16();
                string notifierTitle = string.Empty;
                List<string> notifierLines = new();
                if (notifierEntryCount > 0)
                {
                    notifierTitle = ReadMapleString(reader);
                    for (int i = 0; i < notifierEntryCount; i++)
                    {
                        notifierLines.Add(ReadMapleString(reader));
                    }
                }

                if (hasCharacterData)
                {
                    packet = new PacketSetFieldPacket(
                        clientOptions,
                        channelId,
                        oldDriverId,
                        fieldKey,
                        hasCharacterData,
                        notifierTitle,
                        notifierLines,
                        false,
                        0,
                        0,
                        0,
                        false,
                        0,
                        0,
                        0,
                        (int)(stream.Length - stream.Position));
                    return true;
                }

                _ = reader.ReadByte(); // revive flag
                int fieldId = reader.ReadInt32();
                byte portalIndex = reader.ReadByte();
                int hp = reader.ReadInt32();
                bool chaseEnabled = reader.ReadByte() != 0;
                int chaseX = 0;
                int chaseY = 0;
                if (chaseEnabled)
                {
                    chaseX = reader.ReadInt32();
                    chaseY = reader.ReadInt32();
                }

                long serverFileTime = reader.ReadInt64();
                long remaining = stream.Length - stream.Position;
                if (remaining > 0)
                {
                    error = $"Official CStage::OnSetField payload has {remaining} trailing byte(s) after the non-character-data branch.";
                    return false;
                }

                if (fieldId <= 0)
                {
                    error = $"Official CStage::OnSetField payload contains an invalid field id: {fieldId.ToString(CultureInfo.InvariantCulture)}.";
                    return false;
                }

                packet = new PacketSetFieldPacket(
                    clientOptions,
                    channelId,
                    oldDriverId,
                    fieldKey,
                    hasCharacterData,
                    notifierTitle,
                    notifierLines,
                    true,
                    fieldId,
                    portalIndex,
                    hp,
                    chaseEnabled,
                    chaseX,
                    chaseY,
                    serverFileTime,
                    0);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException)
            {
                error = $"Official CStage::OnSetField payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodeBackEffectPayload(byte[] payload, out PacketBackEffectPacket packet, out string error)
        {
            packet = default;
            error = null;
            payload ??= Array.Empty<byte>();

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                packet = new PacketBackEffectPacket(
                    reader.ReadByte(),
                    reader.ReadInt32(),
                    reader.ReadByte(),
                    reader.ReadInt32());
                long remaining = stream.Length - stream.Position;
                if (remaining > 0)
                {
                    error = $"Back-effect packet has {remaining} trailing byte(s).";
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException)
            {
                error = $"Back-effect packet could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryDecodeMapObjectVisiblePayload(byte[] payload, out IReadOnlyList<PacketMapObjectVisibilityEntry> entries, out string error)
        {
            entries = Array.Empty<PacketMapObjectVisibilityEntry>();
            error = null;
            payload ??= Array.Empty<byte>();

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
                int count = reader.ReadByte();
                List<PacketMapObjectVisibilityEntry> decoded = new(count);
                for (int i = 0; i < count; i++)
                {
                    string name = ReadMapleString(reader);
                    bool visible = reader.ReadByte() != 0;
                    decoded.Add(new PacketMapObjectVisibilityEntry(name, visible));
                }

                long remaining = stream.Length - stream.Position;
                if (remaining > 0)
                {
                    error = $"Map-object visibility packet has {remaining} trailing byte(s).";
                    return false;
                }

                entries = decoded;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException)
            {
                error = $"Map-object visibility packet could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static string FormatPortalSuffix(string portalName)
        {
            return string.IsNullOrWhiteSpace(portalName)
                ? string.Empty
                : $" via portal '{portalName.Trim()}'";
        }

        private static string FormatPortalIndexSuffix(int portalIndex)
        {
            return portalIndex < 0
                ? string.Empty
                : $" via portal index {portalIndex.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string FormatNotifierSuffix(string title, IReadOnlyList<string> lines)
        {
            int lineCount = lines?.Count ?? 0;
            if (lineCount == 0 && string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(title)
                ? $" with {lineCount.ToString(CultureInfo.InvariantCulture)} notifier line(s)"
                : $" with notifier '{title.Trim()}' ({lineCount.ToString(CultureInfo.InvariantCulture)} line(s))";
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
                throw new InvalidDataException("Maple strings with negative lengths are not supported in this simulator-owned packet surface.");
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException("Maple string ended before its declared length.");
            }

            return Encoding.Default.GetString(bytes);
        }
    }

    internal sealed class PacketStageTransitionCallbacks
    {
        internal Func<PacketBackEffectPacket, int, string> ApplyBackEffect { get; init; }
        internal Func<string, bool, int> ApplyMapObjectVisibility { get; init; }
        internal Func<string> ClearBackEffect { get; init; }
        internal Func<string> OpenCashShop { get; init; }
        internal Func<string> OpenItc { get; init; }
        internal Func<PacketStageFieldTransferRequest, bool> QueueFieldTransfer { get; init; }
    }

    internal readonly record struct PacketBackEffectPacket(byte Effect, int FieldId, byte PageId, int DurationMs);

    internal readonly record struct PacketMapObjectVisibilityEntry(string Name, bool Visible);

    internal readonly record struct PacketStageFieldTransferRequest(int MapId, string PortalName, int PortalIndex, string Source);

    internal readonly record struct PacketSetFieldPacket(
        IReadOnlyDictionary<uint, int> ClientOptions,
        int ChannelId,
        int OldDriverId,
        byte FieldKey,
        bool HasCharacterData,
        string NotifierTitle,
        IReadOnlyList<string> NotifierLines,
        bool SupportsFieldTransfer,
        int FieldId,
        byte PortalIndex,
        int Hp,
        bool ChaseEnabled,
        int ChaseX,
        int ChaseY,
        long ServerFileTime,
        int TrailingBytes);
}
