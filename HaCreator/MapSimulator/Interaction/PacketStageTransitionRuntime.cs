using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class PacketStageTransitionRuntime
    {
        private static readonly InventoryType[] CharacterDataInventoryOrder =
        {
            InventoryType.EQUIP,
            InventoryType.USE,
            InventoryType.SETUP,
            InventoryType.ETC,
            InventoryType.CASH
        };

        private static readonly ulong[] CharacterDataInventorySectionFlags =
        {
            0x4UL,
            0x8UL,
            0x10UL,
            0x20UL,
            0x40UL
        };

        private const ulong CharacterDataKnownOpaquePreMapTransferFlags = 0x30000UL;
        private const int CharacterDataMiniGameRecordByteLength = 0x14;
        private const int CharacterDataCoupleRecordByteLength = 0x21;
        private const int CharacterDataFriendRecordByteLength = 0x25;
        private const int CharacterDataMarriageRecordByteLength = 0x30;

        private enum CharacterDataLeadingTailLayout
        {
            None = 0,
            MiniGameOnly,
            RelationshipsOnly,
            MiniGameAndRelationships
        }

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
            int? meso = null,
            IReadOnlyDictionary<InventoryType, int> inventorySlotLimits = null,
            int damageSeed1 = 0,
            int damageSeed2 = 0,
            int damageSeed3 = 0,
            bool useCharacterDataDecodeLayout = false,
            IReadOnlyDictionary<byte, int> visibleEquipmentByBodyPart = null,
            IReadOnlyDictionary<byte, int> hiddenEquipmentByBodyPart = null,
            int weaponStickerItemId = 0,
            ulong additionalCharacterDataFlags = 0,
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

            if (useCharacterDataDecodeLayout)
            {
                ulong characterDataFlags = 0x1UL;
                if (meso.HasValue)
                {
                    characterDataFlags |= 0x2UL;
                }

                if (inventorySlotLimits != null)
                {
                    characterDataFlags |= 0x80UL;
                }

                if (HasAnyAvatarLookEquipment(visibleEquipmentByBodyPart, hiddenEquipmentByBodyPart, weaponStickerItemId))
                {
                    characterDataFlags |= 0x4UL;
                }

                writer.Write(characterDataFlags | additionalCharacterDataFlags);
                writer.Write((byte)0); // nCombatOrders
                writer.Write((byte)0); // bBackwardUpdate
                WriteCharacterDataStatAndTrailer(
                    writer,
                    mapId,
                    portalIndex,
                    hp,
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
                    maxHp,
                    mp,
                    maxMp,
                    abilityPoints,
                    skillPoints,
                    experience,
                    fame,
                    tempExperience,
                    playTime,
                    subJob,
                    friendMax,
                    linkedCharacterName);

                if (meso.HasValue)
                {
                    writer.Write(meso.Value);
                }

                if (inventorySlotLimits != null)
                {
                    WriteCharacterDataInventorySlotLimits(writer, inventorySlotLimits);
                }

                if ((characterDataFlags & 0x4UL) != 0)
                {
                    WriteCharacterDataEquipInventorySection(
                        writer,
                        visibleEquipmentByBodyPart,
                        hiddenEquipmentByBodyPart,
                        weaponStickerItemId);
                }
            }
            else
            {
                WriteCharacterDataStatAndTrailer(
                    writer,
                    mapId,
                    portalIndex,
                    hp,
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
                    maxHp,
                    mp,
                    maxMp,
                    abilityPoints,
                    skillPoints,
                    experience,
                    fame,
                    tempExperience,
                    playTime,
                    subJob,
                    friendMax,
                    linkedCharacterName);
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

        private static bool HasAnyAvatarLookEquipment(
            IReadOnlyDictionary<byte, int> visibleEquipmentByBodyPart,
            IReadOnlyDictionary<byte, int> hiddenEquipmentByBodyPart,
            int weaponStickerItemId)
        {
            return weaponStickerItemId > 0
                || (visibleEquipmentByBodyPart?.Any(static entry => entry.Value > 0) == true)
                || (hiddenEquipmentByBodyPart?.Any(static entry => entry.Value > 0) == true);
        }

        private static void WriteCharacterDataStatAndTrailer(
            BinaryWriter writer,
            int mapId,
            byte portalIndex,
            int hp,
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
            int maxHp,
            int mp,
            int maxMp,
            short abilityPoints,
            short skillPoints,
            int experience,
            short fame,
            int tempExperience,
            int playTime,
            short subJob,
            byte friendMax,
            string linkedCharacterName)
        {
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
        }

        private static void WriteCharacterDataEquipInventorySection(
            BinaryWriter writer,
            IReadOnlyDictionary<byte, int> visibleEquipmentByBodyPart,
            IReadOnlyDictionary<byte, int> hiddenEquipmentByBodyPart,
            int weaponStickerItemId)
        {
            foreach (KeyValuePair<byte, int> entry in (visibleEquipmentByBodyPart ?? new Dictionary<byte, int>()).OrderBy(static entry => entry.Key))
            {
                if (entry.Value <= 0 || entry.Key == 0 || entry.Key > 59)
                {
                    continue;
                }

                writer.Write((short)(-entry.Key));
                WriteCharacterDataEquipItem(writer, entry.Value);
            }

            foreach (KeyValuePair<byte, int> entry in (hiddenEquipmentByBodyPart ?? new Dictionary<byte, int>()).OrderBy(static entry => entry.Key))
            {
                if (entry.Value <= 0 || entry.Key == 0 || entry.Key > 59)
                {
                    continue;
                }

                writer.Write((short)(-(100 + entry.Key)));
                WriteCharacterDataEquipItem(writer, entry.Value);
            }

            if (weaponStickerItemId > 0)
            {
                writer.Write((short)-111);
                WriteCharacterDataEquipItem(writer, weaponStickerItemId);
            }

            writer.Write((short)0);
        }

        private static void WriteCharacterDataInventorySlotLimits(
            BinaryWriter writer,
            IReadOnlyDictionary<InventoryType, int> inventorySlotLimits)
        {
            foreach (InventoryType inventoryType in CharacterDataInventoryOrder)
            {
                int slotLimit = inventorySlotLimits != null && inventorySlotLimits.TryGetValue(inventoryType, out int configuredValue)
                    ? configuredValue
                    : 24;
                writer.Write((byte)Math.Clamp(slotLimit, 0, byte.MaxValue));
            }
        }

        private static void WriteCharacterDataEquipItem(BinaryWriter writer, int itemId)
        {
            writer.Write((byte)1); // GW_ItemSlotEquip
            writer.Write(itemId);
            writer.Write((byte)0); // hasCashItemSN
            writer.Write((long)0); // dateExpire
            writer.Write((byte)0); // nRUC
            writer.Write((byte)0); // nCUC

            for (int i = 0; i < 14; i++)
            {
                writer.Write((short)0);
            }

            WriteMapleString(writer, string.Empty);
            writer.Write((short)0); // nAttribute
            writer.Write((byte)0); // nLevelUpType
            writer.Write((byte)0); // nLevel
            writer.Write(0); // nEXP
            writer.Write(0); // nDurability
            writer.Write(0); // nIUC
            writer.Write((byte)0); // nGrade
            writer.Write((byte)0); // nCHUC
            writer.Write((short)0); // nOption1
            writer.Write((short)0); // nOption2
            writer.Write((short)0); // nOption3
            writer.Write((short)0); // nSocket1
            writer.Write((short)0); // nSocket2
            writer.Write((long)0); // liSN
            writer.Write((long)0); // ftEquipped
            writer.Write(0); // nPrevBonusExpRate
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

                snapshot = DecodeCharacterDataOwnedPreludeSections(reader, characterDataFlags, snapshot);
                if (TryDecodeCharacterDataInventorySections(reader, characterDataFlags, snapshot, out PacketCharacterDataSnapshot inventoryDecoratedSnapshot))
                {
                    snapshot = inventoryDecoratedSnapshot;
                }

                if (TryDecodeKnownCharacterDataTailSections(reader, characterDataFlags, snapshot, out PacketCharacterDataSnapshot tailDecoratedSnapshot))
                {
                    snapshot = tailDecoratedSnapshot;
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

        private static PacketCharacterDataSnapshot DecodeCharacterDataOwnedPreludeSections(
            BinaryReader reader,
            ulong characterDataFlags,
            PacketCharacterDataSnapshot snapshot)
        {
            if ((characterDataFlags & 0x2UL) != 0)
            {
                snapshot = snapshot with
                {
                    Meso = Math.Max(0, reader.ReadInt32())
                };
            }

            if ((characterDataFlags & 0x80UL) != 0)
            {
                snapshot = snapshot with
                {
                    InventorySlotLimits = ReadCharacterDataInventorySlotLimits(reader)
                };
            }

            if ((characterDataFlags & 0x100000UL) != 0)
            {
                snapshot = snapshot with
                {
                    PreInventoryHeaderValue1 = reader.ReadInt32(),
                    PreInventoryHeaderValue2 = reader.ReadInt32()
                };
            }

            return snapshot;
        }

        private static IReadOnlyDictionary<InventoryType, int> ReadCharacterDataInventorySlotLimits(BinaryReader reader)
        {
            Dictionary<InventoryType, int> slotLimits = new(CharacterDataInventoryOrder.Length);
            foreach (InventoryType inventoryType in CharacterDataInventoryOrder)
            {
                slotLimits[inventoryType] = Math.Max(0, (int)reader.ReadByte());
            }

            return slotLimits;
        }

        private static bool TryDecodeCharacterDataInventorySections(
            BinaryReader reader,
            ulong characterDataFlags,
            PacketCharacterDataSnapshot snapshot,
            out PacketCharacterDataSnapshot decoratedSnapshot)
        {
            decoratedSnapshot = snapshot;
            long startPosition = reader.BaseStream.Position;
            try
            {
                for (int inventoryIndex = 0; inventoryIndex < CharacterDataInventoryOrder.Length; inventoryIndex++)
                {
                    if ((characterDataFlags & CharacterDataInventorySectionFlags[inventoryIndex]) == 0)
                    {
                        continue;
                    }

                    if (inventoryIndex == 0)
                    {
                        if (!TryDecodeCharacterDataEquipInventory(reader, decoratedSnapshot, out PacketCharacterDataSnapshot equipDecoratedSnapshot))
                        {
                            return false;
                        }

                        decoratedSnapshot = equipDecoratedSnapshot;
                        continue;
                    }

                    if (!TrySkipCharacterDataInventoryEntries(reader))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                decoratedSnapshot = snapshot;
                return false;
            }
        }

        private static bool TryDecodeCharacterDataEquipInventory(
            BinaryReader reader,
            PacketCharacterDataSnapshot snapshot,
            out PacketCharacterDataSnapshot decoratedSnapshot)
        {
            decoratedSnapshot = snapshot;
            long startPosition = reader.BaseStream.Position;
            try
            {
                Dictionary<byte, int> equipped = new();
                Dictionary<byte, int> cashEquipped = new();
                while (true)
                {
                    short position = reader.ReadInt16();
                    if (position == 0)
                    {
                        break;
                    }

                    if (!TryDecodeCharacterDataItemSlot(reader, out PacketCharacterDataItemSlot itemSlot))
                    {
                        return false;
                    }

                    if (itemSlot.ItemId <= 0)
                    {
                        continue;
                    }

                    if (position <= -1 && position >= -59)
                    {
                        equipped[(byte)(-position)] = itemSlot.ItemId;
                    }
                    else if (position <= -101 && position >= -159)
                    {
                        cashEquipped[(byte)(-position - 100)] = itemSlot.ItemId;
                    }
                }

                Dictionary<byte, int> visibleEquipment = new();
                Dictionary<byte, int> hiddenEquipment = new();
                for (byte bodyPart = 1; bodyPart <= 59; bodyPart++)
                {
                    if (bodyPart == 11)
                    {
                        if (equipped.TryGetValue(bodyPart, out int weaponItemId) && weaponItemId > 0)
                        {
                            visibleEquipment[bodyPart] = weaponItemId;
                        }

                        continue;
                    }

                    if (!LoginAvatarLookCodec.TryGetEquipSlot(bodyPart, out _))
                    {
                        continue;
                    }

                    if (cashEquipped.TryGetValue(bodyPart, out int cashItemId) && cashItemId > 0)
                    {
                        visibleEquipment[bodyPart] = cashItemId;
                        if (equipped.TryGetValue(bodyPart, out int concealedItemId) && concealedItemId > 0)
                        {
                            hiddenEquipment[bodyPart] = concealedItemId;
                        }
                    }
                    else if (equipped.TryGetValue(bodyPart, out int equippedItemId) && equippedItemId > 0)
                    {
                        visibleEquipment[bodyPart] = equippedItemId;
                    }
                }

                int weaponStickerItemId = cashEquipped.TryGetValue(11, out int stickerItemId)
                    ? stickerItemId
                    : 0;
                decoratedSnapshot = snapshot with
                {
                    AvatarLook = new LoginAvatarLook
                    {
                        Gender = Enum.IsDefined(typeof(CharacterGender), (int)snapshot.Gender)
                            ? (CharacterGender)snapshot.Gender
                            : CharacterGender.Male,
                        Skin = Enum.IsDefined(typeof(SkinColor), (int)snapshot.Skin)
                            ? (SkinColor)snapshot.Skin
                            : SkinColor.Light,
                        FaceId = snapshot.FaceId,
                        HairId = snapshot.HairId,
                        VisibleEquipmentByBodyPart = visibleEquipment,
                        HiddenEquipmentByBodyPart = hiddenEquipment,
                        WeaponStickerItemId = weaponStickerItemId,
                        PetIds = Array.Empty<int>()
                    }
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

        private static bool TrySkipCharacterDataInventoryEntries(BinaryReader reader)
        {
            long startPosition = reader.BaseStream.Position;
            try
            {
                while (true)
                {
                    short position = reader.ReadInt16();
                    if (position == 0)
                    {
                        return true;
                    }

                    if (!TryDecodeCharacterDataItemSlot(reader, out _))
                    {
                        return false;
                    }
                }
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                return false;
            }
        }

        private static bool TryDecodeCharacterDataItemSlot(BinaryReader reader, out PacketCharacterDataItemSlot itemSlot)
        {
            itemSlot = default;
            long startPosition = reader.BaseStream.Position;
            try
            {
                byte itemType = reader.ReadByte();
                int itemId = reader.ReadInt32();
                bool hasCashItemSerialNumber = reader.ReadByte() != 0;
                if (hasCashItemSerialNumber)
                {
                    _ = reader.ReadInt64();
                }

                _ = reader.ReadInt64(); // dateExpire
                switch (itemType)
                {
                    case 1:
                        _ = reader.ReadByte();
                        _ = reader.ReadByte();
                        for (int i = 0; i < 14; i++)
                        {
                            _ = reader.ReadInt16();
                        }

                        _ = ReadMapleString(reader);
                        _ = reader.ReadInt16();
                        _ = reader.ReadByte();
                        _ = reader.ReadByte();
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt32();
                        _ = reader.ReadByte();
                        _ = reader.ReadByte();
                        _ = reader.ReadInt16();
                        _ = reader.ReadInt16();
                        _ = reader.ReadInt16();
                        _ = reader.ReadInt16();
                        _ = reader.ReadInt16();
                        _ = reader.ReadInt64();
                        _ = reader.ReadInt64();
                        _ = reader.ReadInt32();
                        break;
                    case 2:
                        _ = reader.ReadUInt16();
                        _ = ReadMapleString(reader);
                        _ = reader.ReadInt16();
                        if ((itemId / 10000) is 207 or 233)
                        {
                            _ = reader.ReadInt64();
                        }

                        break;
                    case 3:
                        _ = reader.ReadBytes(13);
                        _ = reader.ReadByte();
                        _ = reader.ReadInt16();
                        _ = reader.ReadByte();
                        _ = reader.ReadInt64();
                        _ = reader.ReadInt16();
                        _ = reader.ReadUInt16();
                        _ = reader.ReadInt32();
                        _ = reader.ReadInt16();
                        break;
                    default:
                        return false;
                }

                itemSlot = new PacketCharacterDataItemSlot(itemType, itemId);
                return true;
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                itemSlot = default;
                return false;
            }
        }

        private static bool TryDecodeKnownCharacterDataTailSections(
            BinaryReader reader,
            ulong characterDataFlags,
            PacketCharacterDataSnapshot snapshot,
            out PacketCharacterDataSnapshot decoratedSnapshot)
        {
            decoratedSnapshot = snapshot;
            long startPosition = reader.BaseStream.Position;
            foreach (CharacterDataLeadingTailLayout leadingLayout in Enum.GetValues(typeof(CharacterDataLeadingTailLayout)))
            {
                if (TryDecodeKnownCharacterDataTailSectionsWithLeadingLayout(
                        reader,
                        characterDataFlags,
                        snapshot,
                        leadingLayout,
                        out decoratedSnapshot))
                {
                    return true;
                }

                if (reader.BaseStream.CanSeek)
                {
                    reader.BaseStream.Position = startPosition;
                }
            }

            decoratedSnapshot = snapshot;
            return false;
        }

        private static bool TryDecodeKnownCharacterDataTailSectionsWithLeadingLayout(
            BinaryReader reader,
            ulong characterDataFlags,
            PacketCharacterDataSnapshot snapshot,
            CharacterDataLeadingTailLayout leadingLayout,
            out PacketCharacterDataSnapshot decoratedSnapshot)
        {
            decoratedSnapshot = snapshot;
            long startPosition = reader.BaseStream.Position;
            try
            {
                if (!TryDecodeCharacterDataPreMapTransferSections(
                        reader,
                        characterDataFlags,
                        decoratedSnapshot,
                        out PacketCharacterDataSnapshot preMapTransferSnapshot))
                {
                    return false;
                }

                decoratedSnapshot = preMapTransferSnapshot;

                long preKnownTailPosition = reader.BaseStream.Position;
                ulong opaquePreMapTransferFlags = characterDataFlags & CharacterDataKnownOpaquePreMapTransferFlags;
                if (opaquePreMapTransferFlags == 0)
                {
                    if (!TryDecodeKnownCharacterDataTailSectionsAfterOpaqueMiddleSections(
                            reader,
                            characterDataFlags,
                            leadingLayout,
                            decoratedSnapshot,
                            Array.Empty<byte>(),
                            opaquePreMapTransferFlags,
                            out PacketCharacterDataSnapshot decodedTailSnapshot))
                    {
                        return false;
                    }

                    decoratedSnapshot = decodedTailSnapshot;
                    return true;
                }

                byte[] remainingBytes = ReadRemainingBytes(reader);
                for (int skippedByteCount = 0; skippedByteCount <= remainingBytes.Length; skippedByteCount++)
                {
                    reader.BaseStream.Position = preKnownTailPosition + skippedByteCount;
                    byte[] opaqueBytes = skippedByteCount == 0
                        ? Array.Empty<byte>()
                        : remainingBytes.Take(skippedByteCount).ToArray();
                    if (!TryDecodeKnownCharacterDataTailSectionsAfterOpaqueMiddleSections(
                            reader,
                            characterDataFlags,
                            leadingLayout,
                            decoratedSnapshot,
                            opaqueBytes,
                            opaquePreMapTransferFlags,
                            out PacketCharacterDataSnapshot decodedTailSnapshot))
                    {
                        continue;
                    }

                    decoratedSnapshot = decodedTailSnapshot;
                    return true;
                }

                return false;
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                decoratedSnapshot = snapshot;
                return false;
            }
        }

        private static bool TryDecodeKnownCharacterDataTailSectionsAfterOpaqueMiddleSections(
            BinaryReader reader,
            ulong characterDataFlags,
            CharacterDataLeadingTailLayout leadingLayout,
            PacketCharacterDataSnapshot snapshot,
            byte[] opaquePreMapTransferBytes,
            ulong opaquePreMapTransferFlags,
            out PacketCharacterDataSnapshot decoratedSnapshot)
        {
            decoratedSnapshot = snapshot;
            long startPosition = reader.BaseStream.Position;
            try
            {
                if (!TryDecodeCharacterDataLeadingTailSections(
                        reader,
                        characterDataFlags,
                        leadingLayout,
                        out int miniGameRecordCount,
                        out int coupleRecordCount,
                        out int friendRecordCount,
                        out int marriageRecordCount))
                {
                    return false;
                }

                decoratedSnapshot = snapshot with
                {
                    OpaquePreMapTransferFlags = opaquePreMapTransferFlags,
                    OpaquePreMapTransferSectionByteCount = opaquePreMapTransferBytes?.Length ?? 0,
                    OpaquePreMapTransferSectionBytes = opaquePreMapTransferBytes ?? Array.Empty<byte>(),
                    MiniGameRecordCount = miniGameRecordCount,
                    CoupleRecordCount = coupleRecordCount,
                    FriendRecordCount = friendRecordCount,
                    MarriageRecordCount = marriageRecordCount
                };

                if ((characterDataFlags & MapTransferAuthoritativeBootstrapDecoder.CharacterDataMapTransferFlag) != 0)
                {
                    decoratedSnapshot = decoratedSnapshot with
                    {
                        RegularMapTransferFields = ReadCharacterDataMapTransferFields(reader, MapTransferRuntimeManager.RegularCapacity),
                        ContinentMapTransferFields = ReadCharacterDataMapTransferFields(reader, MapTransferRuntimeManager.ContinentCapacity)
                    };
                }

                if ((characterDataFlags & 0x40000UL) != 0)
                {
                    ushort newYearCardCount = reader.ReadUInt16();
                    SkipCharacterDataNewYearCardRecords(reader, newYearCardCount);
                    decoratedSnapshot = decoratedSnapshot with
                    {
                        NewYearCardRecordCount = newYearCardCount
                    };
                }

                if ((characterDataFlags & 0x80000UL) != 0)
                {
                    ushort questExCount = reader.ReadUInt16();
                    for (int i = 0; i < questExCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = ReadMapleString(reader);
                    }

                    decoratedSnapshot = decoratedSnapshot with
                    {
                        QuestExRecordCount = questExCount
                    };
                }

                if ((characterDataFlags & 0x200000UL) != 0 &&
                    decoratedSnapshot.JobId / 100 == 33)
                {
                    SkipCharacterDataWildHunterInfo(reader);
                    decoratedSnapshot = decoratedSnapshot with
                    {
                        HasWildHunterInfo = true
                    };
                }

                if ((characterDataFlags & 0x400000UL) != 0)
                {
                    ushort questCompleteCount = reader.ReadUInt16();
                    for (int i = 0; i < questCompleteCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = reader.ReadInt64();
                    }

                    decoratedSnapshot = decoratedSnapshot with
                    {
                        QuestCompleteRecordCount = questCompleteCount
                    };
                }

                if ((characterDataFlags & 0x800000UL) != 0)
                {
                    ushort visitorQuestCount = reader.ReadUInt16();
                    for (int i = 0; i < visitorQuestCount; i++)
                    {
                        _ = reader.ReadUInt16();
                        _ = reader.ReadUInt16();
                    }

                    decoratedSnapshot = decoratedSnapshot with
                    {
                        VisitorQuestRecordCount = visitorQuestCount
                    };
                }

                return true;
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                decoratedSnapshot = snapshot;
                return false;
            }
        }

        private static bool TryDecodeCharacterDataPreMapTransferSections(
            BinaryReader reader,
            ulong characterDataFlags,
            PacketCharacterDataSnapshot snapshot,
            out PacketCharacterDataSnapshot decoratedSnapshot)
        {
            decoratedSnapshot = snapshot;
            long startPosition = reader.BaseStream.Position;
            try
            {
                IReadOnlyList<PacketCharacterDataSkillRecord> skillRecordEntries = null;
                IReadOnlyDictionary<int, int> skillRecords = null;
                IReadOnlyDictionary<int, long> skillExpirations = null;
                IReadOnlyDictionary<int, int> skillCooldowns = null;
                int skillRecordCount = 0;
                int skillExpirationRecordCount = 0;
                int skillCooldownRecordCount = 0;

                if ((characterDataFlags & 0x100UL) != 0)
                {
                    ReadCharacterDataSkillRecordSection(
                        reader,
                        out skillRecordEntries,
                        out skillRecords);
                    skillRecordCount = skillRecordEntries?.Count ?? 0;
                }

                if ((characterDataFlags & 0x200UL) != 0)
                {
                    skillExpirations = ReadCharacterDataSkillExpirationRecords(reader);
                    skillExpirationRecordCount = skillExpirations.Count;
                }

                if ((characterDataFlags & 0x4000UL) != 0)
                {
                    skillCooldowns = ReadCharacterDataInt16ValueRecords(reader);
                    skillCooldownRecordCount = skillCooldowns.Count;
                }

                decoratedSnapshot = snapshot with
                {
                    SkillRecordCount = skillRecordCount,
                    SkillExpirationRecordCount = skillExpirationRecordCount,
                    SkillCooldownRecordCount = skillCooldownRecordCount,
                    SkillRecords = skillRecords,
                    SkillExpirationFileTimes = skillExpirations,
                    SkillCooldownRemainingSecondsBySkillId = skillCooldowns,
                    SkillRecordEntries = skillRecordEntries,
                    SkillMasterLevelRecordCount = 0,
                    SkillMasterLevels = null,
                    Int16ValueRecordCount = 0,
                    Int16ValueRecords = null,
                    QuestRecordCount = 0,
                    QuestRecordValues = null,
                    ShortFileTimeRecordCount = 0,
                    ShortFileTimeRecords = null
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

        private static void ReadCharacterDataSkillRecordSection(
            BinaryReader reader,
            out IReadOnlyList<PacketCharacterDataSkillRecord> skillRecordEntries,
            out IReadOnlyDictionary<int, int> skillRecords)
        {
            ushort count = reader.ReadUInt16();
            List<PacketCharacterDataSkillRecord> entries = new(count);
            Dictionary<int, int> levelsBySkillId = new(count);
            for (int i = 0; i < count; i++)
            {
                int skillId = reader.ReadInt32();
                int skillLevel = reader.ReadInt32();
                if (skillId > 0)
                {
                    entries.Add(new PacketCharacterDataSkillRecord(skillId, Math.Max(0, skillLevel), 0));
                    levelsBySkillId[skillId] = Math.Max(0, skillLevel);
                }
            }

            skillRecordEntries = entries;
            skillRecords = levelsBySkillId;
        }

        private static IReadOnlyDictionary<int, long> ReadCharacterDataSkillExpirationRecords(BinaryReader reader)
        {
            ushort count = reader.ReadUInt16();
            Dictionary<int, long> records = new(count);
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadInt32();
                long value = reader.ReadInt64();
                if (key > 0)
                {
                    records[key] = value;
                }
            }

            return records;
        }

        private static IReadOnlyDictionary<int, int> ReadCharacterDataInt16ValueRecords(BinaryReader reader)
        {
            ushort count = reader.ReadUInt16();
            Dictionary<int, int> records = new(count);
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadInt32();
                int value = reader.ReadUInt16();
                if (key > 0)
                {
                    records[key] = Math.Max(0, value);
                }
            }

            return records;
        }

        private static IReadOnlyDictionary<int, string> ReadCharacterDataQuestStringRecords(BinaryReader reader)
        {
            ushort count = reader.ReadUInt16();
            Dictionary<int, string> records = new(count);
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadUInt16();
                string value = ReadMapleString(reader);
                if (key > 0)
                {
                    records[key] = value ?? string.Empty;
                }
            }

            return records;
        }

        private static IReadOnlyDictionary<int, long> ReadCharacterDataShortFileTimeRecords(BinaryReader reader)
        {
            ushort count = reader.ReadUInt16();
            Dictionary<int, long> records = new(count);
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadUInt16();
                long value = reader.ReadInt64();
                if (key > 0)
                {
                    records[key] = value;
                }
            }

            return records;
        }

        private static bool TryDecodeCharacterDataLeadingTailSections(
            BinaryReader reader,
            ulong characterDataFlags,
            CharacterDataLeadingTailLayout leadingLayout,
            out int miniGameRecordCount,
            out int coupleRecordCount,
            out int friendRecordCount,
            out int marriageRecordCount)
        {
            miniGameRecordCount = 0;
            coupleRecordCount = 0;
            friendRecordCount = 0;
            marriageRecordCount = 0;

            long startPosition = reader.BaseStream.Position;
            try
            {
                if ((characterDataFlags & 0x400UL) != 0 &&
                    (leadingLayout is CharacterDataLeadingTailLayout.MiniGameOnly or CharacterDataLeadingTailLayout.MiniGameAndRelationships))
                {
                    miniGameRecordCount = reader.ReadUInt16();
                    SkipCharacterDataFixedRecordGroup(reader, miniGameRecordCount, CharacterDataMiniGameRecordByteLength);
                }

                if ((characterDataFlags & 0x800UL) != 0 &&
                    (leadingLayout is CharacterDataLeadingTailLayout.RelationshipsOnly or CharacterDataLeadingTailLayout.MiniGameAndRelationships))
                {
                    coupleRecordCount = reader.ReadUInt16();
                    SkipCharacterDataFixedRecordGroup(reader, coupleRecordCount, CharacterDataCoupleRecordByteLength);

                    friendRecordCount = reader.ReadUInt16();
                    SkipCharacterDataFixedRecordGroup(reader, friendRecordCount, CharacterDataFriendRecordByteLength);

                    marriageRecordCount = reader.ReadUInt16();
                    SkipCharacterDataFixedRecordGroup(reader, marriageRecordCount, CharacterDataMarriageRecordByteLength);
                }

                if ((characterDataFlags & 0x400UL) == 0 &&
                    (leadingLayout is CharacterDataLeadingTailLayout.MiniGameOnly or CharacterDataLeadingTailLayout.MiniGameAndRelationships))
                {
                    return false;
                }

                if ((characterDataFlags & 0x800UL) == 0 &&
                    (leadingLayout is CharacterDataLeadingTailLayout.RelationshipsOnly or CharacterDataLeadingTailLayout.MiniGameAndRelationships))
                {
                    return false;
                }

                return true;
            }
            catch (Exception) when (reader.BaseStream.CanSeek)
            {
                reader.BaseStream.Position = startPosition;
                miniGameRecordCount = 0;
                coupleRecordCount = 0;
                friendRecordCount = 0;
                marriageRecordCount = 0;
                return false;
            }
        }

        private static int[] ReadCharacterDataMapTransferFields(BinaryReader reader, int count)
        {
            int[] fields = new int[count];
            for (int i = 0; i < count; i++)
            {
                fields[i] = reader.ReadInt32();
            }

            return fields;
        }

        private static byte[] ReadRemainingBytes(BinaryReader reader)
        {
            long remainingLength = reader.BaseStream.Length - reader.BaseStream.Position;
            if (remainingLength <= 0)
            {
                return Array.Empty<byte>();
            }

            int byteCount = checked((int)remainingLength);
            long startPosition = reader.BaseStream.Position;
            byte[] bytes = reader.ReadBytes(byteCount);
            reader.BaseStream.Position = startPosition;
            return bytes;
        }

        private static void SkipCharacterDataFixedRecordGroup(BinaryReader reader, int count, int recordByteLength)
        {
            if (count <= 0)
            {
                return;
            }

            int totalByteLength = checked(count * recordByteLength);
            byte[] skippedBytes = reader.ReadBytes(totalByteLength);
            if (skippedBytes.Length != totalByteLength)
            {
                throw new EndOfStreamException("Character-data record group ended before all fixed-size records could be consumed.");
            }
        }

        private static void SkipCharacterDataNewYearCardRecords(BinaryReader reader, int count)
        {
            for (int i = 0; i < count; i++)
            {
                _ = reader.ReadInt32();
                _ = reader.ReadInt32();
                _ = ReadMapleString(reader);
                _ = reader.ReadByte();
                _ = reader.ReadInt64();
                _ = reader.ReadInt32();
                _ = ReadMapleString(reader);
                _ = reader.ReadByte();
                _ = reader.ReadByte();
                _ = reader.ReadInt64();
                _ = ReadMapleString(reader);
            }
        }

        private static void SkipCharacterDataWildHunterInfo(BinaryReader reader)
        {
            _ = reader.ReadByte();
            for (int i = 0; i < 5; i++)
            {
                _ = reader.ReadInt32();
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

    internal readonly record struct PacketCharacterDataSkillRecord(int SkillId, int SkillLevel, long ExpirationFileTime);

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
        string LinkedCharacterName,
        int? Meso = null,
        IReadOnlyDictionary<InventoryType, int> InventorySlotLimits = null,
        LoginAvatarLook AvatarLook = null,
        int? PreInventoryHeaderValue1 = null,
        int? PreInventoryHeaderValue2 = null,
        int SkillRecordCount = 0,
        int SkillExpirationRecordCount = 0,
        int SkillCooldownRecordCount = 0,
        IReadOnlyDictionary<int, int> SkillRecords = null,
        IReadOnlyDictionary<int, long> SkillExpirationFileTimes = null,
        IReadOnlyDictionary<int, int> SkillCooldownRemainingSecondsBySkillId = null,
        IReadOnlyList<PacketCharacterDataSkillRecord> SkillRecordEntries = null,
        int SkillMasterLevelRecordCount = 0,
        IReadOnlyList<int> SkillMasterLevels = null,
        int Int16ValueRecordCount = 0,
        IReadOnlyDictionary<int, int> Int16ValueRecords = null,
        int QuestRecordCount = 0,
        IReadOnlyDictionary<int, string> QuestRecordValues = null,
        int ShortFileTimeRecordCount = 0,
        IReadOnlyDictionary<int, long> ShortFileTimeRecords = null,
        ulong OpaquePreMapTransferFlags = 0,
        int OpaquePreMapTransferSectionByteCount = 0,
        byte[] OpaquePreMapTransferSectionBytes = null,
        IReadOnlyList<int> RegularMapTransferFields = null,
        IReadOnlyList<int> ContinentMapTransferFields = null,
        int MiniGameRecordCount = 0,
        int CoupleRecordCount = 0,
        int FriendRecordCount = 0,
        int MarriageRecordCount = 0,
        int NewYearCardRecordCount = 0,
        int QuestExRecordCount = 0,
        bool HasWildHunterInfo = false,
        int QuestCompleteRecordCount = 0,
        int VisitorQuestRecordCount = 0);

    internal readonly record struct PacketCharacterDataItemSlot(byte ItemType, int ItemId);

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
