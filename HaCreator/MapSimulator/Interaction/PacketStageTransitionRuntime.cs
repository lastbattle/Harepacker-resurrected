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

        internal static byte[] BuildCharacterDataSetFieldPayload(
            int mapId,
            byte portalIndex = 0,
            int hp = 0,
            int channelId = 0,
            int oldDriverId = 0,
            byte fieldKey = 0,
            int characterId = 0,
            string characterName = "",
            byte gender = 0,
            byte skin = 0,
            int faceId = 20000,
            int hairId = 30000,
            byte level = 1,
            short jobId = 0,
            short strength = 12,
            short dexterity = 5,
            short intelligence = 4,
            short luck = 4,
            int maxHp = 50,
            int mp = 5,
            int maxMp = 5,
            short abilityPoints = 0,
            short skillPoints = 0,
            int experience = 0,
            short fame = 0,
            int tempExperience = 0,
            int playTime = 0,
            short subJob = 0,
            byte friendMax = 0,
            string linkedCharacterName = "",
            int damageSeed1 = 0,
            int damageSeed2 = 0,
            int damageSeed3 = 0,
            byte[] characterDataTail = null,
            byte[] logoutGiftConfigPayload = null,
            long serverFileTime = 0)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write((short)0);
            writer.Write(channelId);
            writer.Write(oldDriverId);
            writer.Write(fieldKey);
            writer.Write((byte)1);
            writer.Write((short)0);
            writer.Write(damageSeed1);
            writer.Write(damageSeed2);
            writer.Write(damageSeed3);
            WriteCharacterStatPayload(
                writer,
                characterId,
                characterName,
                gender,
                skin,
                faceId,
                hairId,
                level,
                jobId,
                strength,
                dexterity,
                intelligence,
                luck,
                hp,
                maxHp,
                mp,
                maxMp,
                abilityPoints,
                skillPoints,
                experience,
                fame,
                tempExperience,
                mapId,
                portalIndex,
                playTime,
                subJob);
            writer.Write(friendMax);
            writer.Write(!string.IsNullOrWhiteSpace(linkedCharacterName) ? (byte)1 : (byte)0);
            if (!string.IsNullOrWhiteSpace(linkedCharacterName))
            {
                WriteMapleString(writer, linkedCharacterName);
            }

            if (characterDataTail?.Length > 0)
            {
                writer.Write(characterDataTail);
            }

            if (logoutGiftConfigPayload?.Length > 0)
            {
                writer.Write(logoutGiftConfigPayload);
            }

            writer.Write(serverFileTime);
            writer.Flush();
            return stream.ToArray();
        }

        internal static bool TryDecodeTrailingLogoutGiftConfigPayload(
            byte[] trailingPayload,
            out int[] commoditySerialNumbers,
            out byte[] leadingOpaqueBytes,
            out int[] leadingOpaqueInt32Values,
            out string error)
        {
            const int logoutGiftEntryCount = 3;
            const int logoutGiftConfigByteLength = logoutGiftEntryCount * sizeof(int);

            commoditySerialNumbers = new int[logoutGiftEntryCount];
            leadingOpaqueBytes = Array.Empty<byte>();
            leadingOpaqueInt32Values = Array.Empty<int>();
            error = null;
            trailingPayload ??= Array.Empty<byte>();

            if (trailingPayload.Length < logoutGiftConfigByteLength)
            {
                error = trailingPayload.Length == 0
                    ? "Character-data SetField did not carry trailing logout-gift bytes."
                    : $"Character-data SetField preserved only {trailingPayload.Length.ToString(CultureInfo.InvariantCulture)} trailing byte(s), which is too short for the client 12-byte logout-gift cache.";
                return false;
            }

            int leadingOpaqueByteCount = trailingPayload.Length - logoutGiftConfigByteLength;
            if (leadingOpaqueByteCount > 0)
            {
                leadingOpaqueBytes = new byte[leadingOpaqueByteCount];
                Buffer.BlockCopy(trailingPayload, 0, leadingOpaqueBytes, 0, leadingOpaqueByteCount);
                if ((leadingOpaqueByteCount % sizeof(int)) == 0)
                {
                    leadingOpaqueInt32Values = new int[leadingOpaqueByteCount / sizeof(int)];
                    Buffer.BlockCopy(leadingOpaqueBytes, 0, leadingOpaqueInt32Values, 0, leadingOpaqueByteCount);
                }
            }

            using MemoryStream stream = new(trailingPayload, leadingOpaqueByteCount, logoutGiftConfigByteLength, writable: false);
            using BinaryReader reader = new(stream);
            for (int i = 0; i < commoditySerialNumbers.Length; i++)
            {
                commoditySerialNumbers[i] = Math.Max(0, reader.ReadInt32());
            }

            return true;
        }

        private static void WriteCharacterStatPayload(
            BinaryWriter writer,
            int characterId,
            string characterName,
            byte gender,
            byte skin,
            int faceId,
            int hairId,
            byte level,
            short jobId,
            short strength,
            short dexterity,
            short intelligence,
            short luck,
            int hp,
            int maxHp,
            int mp,
            int maxMp,
            short abilityPoints,
            short skillPoints,
            int experience,
            short fame,
            int tempExperience,
            int mapId,
            byte portalIndex,
            int playTime,
            short subJob)
        {
            writer.Write(characterId);
            WriteMapleString(writer, characterName ?? string.Empty);
            writer.Write(gender);
            writer.Write(skin);
            writer.Write(faceId);
            writer.Write(hairId);
            writer.Write(level);
            writer.Write(jobId);
            writer.Write(strength);
            writer.Write(dexterity);
            writer.Write(intelligence);
            writer.Write(luck);
            writer.Write(hp);
            writer.Write(maxHp);
            writer.Write(mp);
            writer.Write(maxMp);
            writer.Write(abilityPoints);
            writer.Write(skillPoints);
            writer.Write(experience);
            writer.Write(fame);
            writer.Write(tempExperience);
            writer.Write(mapId);
            writer.Write(portalIndex);
            writer.Write(playTime);
            writer.Write(subJob);
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
                    string decodeSuffix = officialPacket.HasCharacterData
                        ? $" (remaining opaque bytes {officialPacket.TrailingBytes.ToString(CultureInfo.InvariantCulture)})"
                        : string.Empty;
                    _stageStatus = $"CStage::OnSetField decoded the official {(officialPacket.HasCharacterData ? "character-data" : "non-character-data")} branch for channel {officialPacket.ChannelId}, but field transfer data could not be recovered{decodeSuffix}{notifierSuffix}.";
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
                string branchLabel = officialPacket.HasCharacterData
                    ? "official character-data branch"
                    : "official payload";
                string trailingSuffix = officialPacket.HasCharacterData
                    ? $" (remaining opaque bytes {officialPacket.TrailingBytes.ToString(CultureInfo.InvariantCulture)})"
                    : string.Empty;
                _stageStatus = queued
                    ? $"CStage::OnSetField queued map {officialPacket.FieldId}{FormatPortalIndexSuffix(officialPacket.PortalIndex)} from the {branchLabel} (channel {officialPacket.ChannelId}, fieldKey {officialPacket.FieldKey}){trailingSuffix}{notifierSummary}."
                    : $"CStage::OnSetField decoded map {officialPacket.FieldId}{FormatPortalIndexSuffix(officialPacket.PortalIndex)} from the {branchLabel}, but the simulator could not queue the transfer.";
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
                    long characterDataStart = stream.Position;
                    ulong characterDataFlags = 0;
                    PacketCharacterDataTransferHead transferHead;
                    PacketCharacterDataSnapshot characterDataSnapshot;
                    if (!TryDecodeCharacterDataDecodePrelude(reader, out transferHead, out characterDataSnapshot, out characterDataFlags, out _))
                    {
                        stream.Position = characterDataStart;
                        if (!TryDecodeCharacterDataTransferHead(reader, out transferHead, out characterDataSnapshot, out error))
                        {
                            return false;
                        }
                    }

                    int remainingBytes = checked((int)(stream.Length - stream.Position));
                    long transferServerFileTime = 0;
                    if (remainingBytes >= sizeof(long))
                    {
                        long restorePosition = stream.Position;
                        stream.Position = stream.Length - sizeof(long);
                        transferServerFileTime = reader.ReadInt64();
                        stream.Position = restorePosition;
                        remainingBytes -= sizeof(long);
                    }

                    packet = new PacketSetFieldPacket(
                        clientOptions,
                        channelId,
                        oldDriverId,
                        fieldKey,
                        hasCharacterData,
                        notifierTitle,
                        notifierLines,
                        transferHead.FieldId > 0,
                        transferHead.FieldId,
                        transferHead.PortalIndex,
                        transferHead.Hp,
                        false,
                        0,
                        0,
                        characterDataFlags,
                        characterDataSnapshot,
                        transferServerFileTime,
                        remainingBytes > 0 ? reader.ReadBytes(remainingBytes) : Array.Empty<byte>(),
                        remainingBytes);
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
                    0,
                    null,
                    serverFileTime,
                    Array.Empty<byte>(),
                    0);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException)
            {
                error = $"Official CStage::OnSetField payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeCharacterDataTransferHead(
            BinaryReader reader,
            out PacketCharacterDataTransferHead head,
            out PacketCharacterDataSnapshot snapshot,
            out string error)
        {
            head = default;
            snapshot = null;
            error = null;
            long restorePosition = reader.BaseStream.Position;
            try
            {
                _ = reader.ReadInt32(); // damage seed 1
                _ = reader.ReadInt32(); // damage seed 2
                _ = reader.ReadInt32(); // damage seed 3
                snapshot = ReadCharacterDataStatSnapshot(reader);
                if (TryDecodeCharacterDataStatTrailer(reader, snapshot, out PacketCharacterDataSnapshot decoratedSnapshot))
                {
                    snapshot = decoratedSnapshot;
                }

                head = new PacketCharacterDataTransferHead(snapshot.FieldId, snapshot.PortalIndex, snapshot.Hp);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException || ex is InvalidDataException)
            {
                if (reader.BaseStream.CanSeek)
                {
                    reader.BaseStream.Position = restorePosition;
                }

                error = $"Official CStage::OnSetField character-data branch could not be decoded far enough to recover transfer fields: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeCharacterDataDecodePrelude(
            BinaryReader reader,
            out PacketCharacterDataTransferHead head,
            out PacketCharacterDataSnapshot snapshot,
            out ulong characterDataFlags,
            out int consumedBytes)
        {
            head = default;
            snapshot = null;
            characterDataFlags = 0;
            consumedBytes = 0;

            long startPosition = reader.BaseStream.Position;
            try
            {
                characterDataFlags = reader.ReadUInt64();
                _ = reader.ReadByte(); // nCombatOrders
                bool hasBackwardUpdate = reader.ReadByte() != 0;
                if (hasBackwardUpdate)
                {
                    _ = reader.ReadByte(); // update subtype
                    int removedSnCount = reader.ReadInt32();
                    if (removedSnCount < 0)
                    {
                        return false;
                    }

                    checked
                    {
                        reader.BaseStream.Position += removedSnCount * sizeof(long);
                    }

                    int removedCashCount = reader.ReadInt32();
                    if (removedCashCount < 0)
                    {
                        return false;
                    }

                    checked
                    {
                        reader.BaseStream.Position += removedCashCount * sizeof(long);
                    }
                }

                if ((characterDataFlags & 0x1UL) == 0)
                {
                    return false;
                }

                snapshot = ReadCharacterDataStatSnapshot(reader);
                if (TryDecodeCharacterDataStatTrailer(reader, snapshot, out PacketCharacterDataSnapshot decoratedSnapshot))
                {
                    snapshot = decoratedSnapshot;
                }

                head = new PacketCharacterDataTransferHead(snapshot.FieldId, snapshot.PortalIndex, snapshot.Hp);
                consumedBytes = checked((int)(reader.BaseStream.Position - startPosition));
                return true;
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                head = default;
                snapshot = null;
                characterDataFlags = 0;
                consumedBytes = 0;
                return false;
            }
        }

        private static PacketCharacterDataSnapshot ReadCharacterDataStatSnapshot(BinaryReader reader)
        {
            int characterId = reader.ReadInt32();
            string characterName = ReadMapleString(reader);
            byte gender = reader.ReadByte();
            byte skin = reader.ReadByte();
            int faceId = reader.ReadInt32();
            int hairId = reader.ReadInt32();
            byte level = reader.ReadByte();
            short jobId = reader.ReadInt16();
            short strength = reader.ReadInt16();
            short dexterity = reader.ReadInt16();
            short intelligence = reader.ReadInt16();
            short luck = reader.ReadInt16();
            int hp = reader.ReadInt32();
            int maxHp = reader.ReadInt32();
            int mp = reader.ReadInt32();
            int maxMp = reader.ReadInt32();
            short abilityPoints = reader.ReadInt16();
            short skillPoints = reader.ReadInt16();
            int experience = reader.ReadInt32();
            short fame = reader.ReadInt16();
            int tempExperience = reader.ReadInt32();
            int fieldId = reader.ReadInt32();
            byte portalIndex = reader.ReadByte();
            int playTime = reader.ReadInt32();
            short subJob = reader.ReadInt16();

            return new PacketCharacterDataSnapshot(
                characterId,
                characterName,
                gender,
                skin,
                faceId,
                hairId,
                level,
                jobId,
                strength,
                dexterity,
                intelligence,
                luck,
                hp,
                maxHp,
                mp,
                maxMp,
                abilityPoints,
                skillPoints,
                experience,
                fame,
                tempExperience,
                fieldId,
                portalIndex,
                playTime,
                subJob,
                0,
                string.Empty);
        }

        private static bool TryDecodeCharacterDataStatTrailer(
            BinaryReader reader,
            PacketCharacterDataSnapshot snapshot,
            out PacketCharacterDataSnapshot decoratedSnapshot)
        {
            decoratedSnapshot = snapshot;
            long startPosition = reader.BaseStream.Position;
            try
            {
                byte friendMax = reader.ReadByte();
                bool hasLinkedCharacter = reader.ReadByte() != 0;
                string linkedCharacterName = hasLinkedCharacter
                    ? ReadMapleString(reader)
                    : string.Empty;
                decoratedSnapshot = snapshot with
                {
                    FriendMax = friendMax,
                    LinkedCharacterName = linkedCharacterName
                };
                return true;
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                decoratedSnapshot = snapshot;
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

    internal readonly record struct PacketCharacterDataTransferHead(int FieldId, byte PortalIndex, int Hp);

    internal sealed record PacketCharacterDataSnapshot(
        int CharacterId,
        string CharacterName,
        byte Gender,
        byte Skin,
        int FaceId,
        int HairId,
        byte Level,
        short JobId,
        short Strength,
        short Dexterity,
        short Intelligence,
        short Luck,
        int Hp,
        int MaxHp,
        int Mp,
        int MaxMp,
        short AbilityPoints,
        short SkillPoints,
        int Experience,
        short Fame,
        int TempExperience,
        int FieldId,
        byte PortalIndex,
        int PlayTime,
        short SubJob,
        byte FriendMax,
        string LinkedCharacterName);

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
        ulong CharacterDataFlags,
        PacketCharacterDataSnapshot CharacterDataSnapshot,
        long ServerFileTime,
        byte[] TrailingPayload,
        int TrailingBytes);
}
