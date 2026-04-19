using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int MonsterBookRegistrationResponseDelayMs = 120;
        private const int MonsterBookRegistrationSyntheticResultTimeoutMs = 2000;
        private const int MonsterBookRegistrationOfficialSessionTimeoutMs = 5000;
        private PendingMonsterBookRegistrationRequest _pendingMonsterBookRegistrationRequest;
        private int _nextMonsterBookRegistrationRequestId = 1;

        internal readonly struct MonsterBookRegistrationResultPayload
        {
            public MonsterBookRegistrationResultPayload(
                bool success,
                int? requestId,
                int? mobId,
                bool? registered,
                int? reasonCode,
                string statusText)
            {
                Success = success;
                RequestId = requestId;
                MobId = mobId;
                Registered = registered;
                ReasonCode = reasonCode;
                StatusText = statusText ?? string.Empty;
            }

            public bool Success { get; }
            public int? RequestId { get; }
            public int? MobId { get; }
            public bool? Registered { get; }
            public int? ReasonCode { get; }
            public string StatusText { get; }
        }

        internal readonly struct MonsterBookOwnershipSyncPayload
        {
            public MonsterBookOwnershipSyncPayload(
                bool clearRequested,
                bool replaceExisting,
                int? characterId,
                string characterName,
                int? registeredMobId,
                IReadOnlyDictionary<int, int> cardCountsByMob,
                string statusText)
            {
                ClearRequested = clearRequested;
                ReplaceExisting = replaceExisting;
                CharacterId = characterId;
                CharacterName = characterName ?? string.Empty;
                RegisteredMobId = registeredMobId;
                CardCountsByMob = cardCountsByMob ?? new Dictionary<int, int>();
                StatusText = statusText ?? string.Empty;
            }

            public bool ClearRequested { get; }
            public bool ReplaceExisting { get; }
            public int? CharacterId { get; }
            public string CharacterName { get; }
            public int? RegisteredMobId { get; }
            public IReadOnlyDictionary<int, int> CardCountsByMob { get; }
            public string StatusText { get; }
        }

        private sealed class PendingMonsterBookRegistrationRequest
        {
            public CharacterBuild Build { get; init; }
            public int CharacterId { get; init; }
            public string CharacterName { get; init; } = string.Empty;
            public int RequestId { get; init; }
            public int MobId { get; init; }
            public bool Registered { get; init; }
            public long SentTick { get; init; }
            public int ResponseDelayMs { get; init; } = MonsterBookRegistrationResponseDelayMs;
            public string RequestSummary { get; init; } = string.Empty;
            public bool SyntheticResultQueued { get; set; }
        }

        private string DispatchMonsterBookRegistrationRequest(
            CharacterBuild build,
            int characterId,
            string characterName,
            int mobId,
            bool registered,
            int requestId,
            out int responseDelayMs)
        {
            responseDelayMs = MonsterBookRegistrationResponseDelayMs;
            string actionLabel = registered ? "register" : "release";
            string ownerLabel = string.IsNullOrWhiteSpace(characterName)
                ? "the active character"
                : characterName.Trim();
            return $"Book Collection queued a packet-owned {actionLabel} request #{requestId.ToString(CultureInfo.InvariantCulture)} for mob {mobId.ToString(CultureInfo.InvariantCulture)} on {ownerLabel}, awaiting local utility result packet {LocalUtilityPacketInboxManager.MonsterBookRegistrationResultPacketType}.";
        }

        private void ProcessPendingMonsterBookRegistrationRequest()
        {
            PendingMonsterBookRegistrationRequest request = _pendingMonsterBookRegistrationRequest;
            if (request == null)
            {
                return;
            }

            long elapsedMs = Environment.TickCount64 - request.SentTick;
            if (elapsedMs < request.ResponseDelayMs)
            {
                return;
            }

            bool hasLiveOfficialSession = _localUtilityOfficialSessionBridge?.HasConnectedSession == true;
            if (!request.SyntheticResultQueued)
            {
                if (!hasLiveOfficialSession)
                {
                    request.SyntheticResultQueued = true;
                    _localUtilityPacketInbox.EnqueueLocal(
                        LocalUtilityPacketInboxManager.MonsterBookRegistrationResultPacketType,
                        BuildMonsterBookRegistrationSyntheticResultPayload(request),
                        "monster-book-registration");
                    return;
                }
            }

            int timeoutMs = hasLiveOfficialSession
                ? MonsterBookRegistrationOfficialSessionTimeoutMs
                : MonsterBookRegistrationSyntheticResultTimeoutMs;
            if (elapsedMs >= request.ResponseDelayMs + timeoutMs)
            {
                _pendingMonsterBookRegistrationRequest = null;
                ShowUtilityFeedbackMessage(
                    $"Monster Book register/release request #{request.RequestId.ToString(CultureInfo.InvariantCulture)} timed out while waiting for local utility packet {LocalUtilityPacketInboxManager.MonsterBookRegistrationResultPacketType}.");
            }
        }

        private bool TryApplyPacketOwnedMonsterBookRegistrationResultPayload(byte[] payload, out string message)
        {
            message = null;
            PendingMonsterBookRegistrationRequest request = _pendingMonsterBookRegistrationRequest;
            if (request == null)
            {
                message = "Monster Book registration result was ignored because no register/release request is pending.";
                return false;
            }

            if (!TryDecodeMonsterBookRegistrationResultPayload(payload, out MonsterBookRegistrationResultPayload result, out string decodeDetail))
            {
                message = decodeDetail ?? "Monster Book registration result payload could not be decoded.";
                return false;
            }

            if (result.RequestId.HasValue && result.RequestId.Value != request.RequestId)
            {
                message = $"Monster Book registration result request id {result.RequestId.Value.ToString(CultureInfo.InvariantCulture)} does not match pending request #{request.RequestId.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            if (result.MobId.HasValue && result.MobId.Value != request.MobId)
            {
                message = $"Monster Book registration result mob id {result.MobId.Value.ToString(CultureInfo.InvariantCulture)} does not match pending mob {request.MobId.ToString(CultureInfo.InvariantCulture)}.";
                return false;
            }

            if (result.Registered.HasValue && result.Registered.Value != request.Registered)
            {
                string requestAction = request.Registered ? "register" : "release";
                string responseAction = result.Registered.Value ? "register" : "release";
                message = $"Monster Book registration result action '{responseAction}' does not match pending '{requestAction}' request.";
                return false;
            }

            _pendingMonsterBookRegistrationRequest = null;
            if (!result.Success)
            {
                message = AppendMonsterBookRegistrationStatusText(
                    result.StatusText,
                    result.ReasonCode.HasValue
                        ? $"Monster Book register/release request #{request.RequestId.ToString(CultureInfo.InvariantCulture)} was rejected with reason {result.ReasonCode.Value.ToString(CultureInfo.InvariantCulture)}."
                        : $"Monster Book register/release request #{request.RequestId.ToString(CultureInfo.InvariantCulture)} was rejected.");
                return true;
            }

            _monsterBookManager.SetRegisteredCard(
                request.Build,
                request.CharacterId,
                request.CharacterName,
                request.MobId,
                request.Registered);

            string actionLabel = request.Registered ? "registered" : "released";
            string ownerLabel = string.IsNullOrWhiteSpace(request.CharacterName)
                ? "the active character"
                : request.CharacterName;
            message = AppendMonsterBookRegistrationStatusText(
                result.StatusText,
                $"Monster Book {actionLabel} mob {request.MobId.ToString(CultureInfo.InvariantCulture)} ({ownerLabel}) via packet-owned result request #{request.RequestId.ToString(CultureInfo.InvariantCulture)}.");
            return true;
        }

        private bool TryApplyPacketOwnedMonsterBookOwnershipSyncPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodeMonsterBookOwnershipSyncPayload(payload, out MonsterBookOwnershipSyncPayload sync, out string detail))
            {
                message = detail ?? "Monster Book ownership sync payload could not be decoded.";
                return false;
            }

            UserInfoUI.UserInfoActionContext context = ResolveBookCollectionActionContext();
            var ownerIdentity = ResolveMonsterBookOwnerIdentity(
                context,
                _playerManager?.Player?.Build ?? _loginCharacterRoster.SelectedEntry?.Build);

            int resolvedCharacterId = sync.CharacterId.HasValue && sync.CharacterId.Value > 0
                ? sync.CharacterId.Value
                : ownerIdentity.CharacterId;
            string resolvedCharacterName = !string.IsNullOrWhiteSpace(sync.CharacterName)
                ? sync.CharacterName.Trim()
                : ownerIdentity.CharacterName;

            MonsterBookSnapshot snapshot = _monsterBookManager.ApplyOwnershipSync(
                ownerIdentity.Build,
                resolvedCharacterId,
                resolvedCharacterName,
                sync.CardCountsByMob,
                registeredMobId: sync.RegisteredMobId ?? 0,
                replaceExisting: sync.ClearRequested || sync.ReplaceExisting);

            if (_pendingMonsterBookRegistrationRequest != null
                && IsMonsterBookOwnershipSyncForPendingRequest(
                    _pendingMonsterBookRegistrationRequest,
                    resolvedCharacterId,
                    resolvedCharacterName,
                    snapshot?.RegisteredCardMobId ?? 0))
            {
                _pendingMonsterBookRegistrationRequest = null;
            }

            StampPacketOwnedUtilityRequestState();

            string ownerLabel = string.IsNullOrWhiteSpace(resolvedCharacterName)
                ? (resolvedCharacterId > 0
                    ? $"id:{resolvedCharacterId.ToString(CultureInfo.InvariantCulture)}"
                    : "the active character")
                : resolvedCharacterName;
            int appliedCount = snapshot?.OwnedCardTypes ?? 0;
            int totalCount = snapshot?.TotalCardTypes ?? 0;
            int registeredMobId = snapshot?.RegisteredCardMobId ?? 0;

            string summary = sync.ClearRequested
                ? $"Monster Book ownership was cleared and refreshed for {ownerLabel} via packet-owned sync."
                : $"Monster Book ownership synced for {ownerLabel} via packet-owned sync.";
            message = AppendMonsterBookRegistrationStatusText(
                sync.StatusText,
                $"{summary} Owned card types now {appliedCount.ToString(CultureInfo.InvariantCulture)}/{totalCount.ToString(CultureInfo.InvariantCulture)}; registered mob {registeredMobId.ToString(CultureInfo.InvariantCulture)}.");
            return true;
        }

        internal static bool TryDecodeMonsterBookRegistrationResultPayloadForTests(
            byte[] payload,
            out MonsterBookRegistrationResultPayload result,
            out string detail)
        {
            return TryDecodeMonsterBookRegistrationResultPayload(payload, out result, out detail);
        }

        internal static bool TryDecodeMonsterBookOwnershipSyncPayloadForTests(
            byte[] payload,
            out MonsterBookOwnershipSyncPayload result,
            out string detail)
        {
            return TryDecodeMonsterBookOwnershipSyncPayload(payload, out result, out detail);
        }

        private int ReserveMonsterBookRegistrationRequestId()
        {
            if (_nextMonsterBookRegistrationRequestId <= 0)
            {
                _nextMonsterBookRegistrationRequestId = 1;
            }

            int requestId = _nextMonsterBookRegistrationRequestId;
            _nextMonsterBookRegistrationRequestId = requestId == int.MaxValue
                ? 1
                : requestId + 1;
            return requestId;
        }

        private static byte[] BuildMonsterBookRegistrationSyntheticResultPayload(PendingMonsterBookRegistrationRequest request)
        {
            if (request == null)
            {
                return Array.Empty<byte>();
            }

            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                success = true,
                requestId = request.RequestId,
                mobId = request.MobId,
                registered = request.Registered,
                statusText = "Synthetic packet-owned Monster Book registration result."
            });
        }

        private static string AppendMonsterBookRegistrationStatusText(string statusText, string baseMessage)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return baseMessage;
            }

            if (string.IsNullOrWhiteSpace(baseMessage))
            {
                return statusText.Trim();
            }

            return $"{baseMessage} Server text: {statusText.Trim()}";
        }

        private static bool TryDecodeMonsterBookRegistrationResultPayload(
            byte[] payload,
            out MonsterBookRegistrationResultPayload result,
            out string detail)
        {
            result = new MonsterBookRegistrationResultPayload(
                success: true,
                requestId: null,
                mobId: null,
                registered: null,
                reasonCode: null,
                statusText: string.Empty);
            detail = null;

            if (payload == null || payload.Length == 0)
            {
                detail = "Decoded empty Monster Book registration result payload as success.";
                return true;
            }

            if (TryDecodeMonsterBookRegistrationJsonPayload(payload, out result, out detail))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                return false;
            }

            if (TryDecodeMonsterBookRegistrationBinaryPayload(payload, out result, out detail))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = "Monster Book registration result payload could not be decoded from JSON or binary shape.";
            }

            return false;
        }

        private static bool TryDecodeMonsterBookOwnershipSyncPayload(
            byte[] payload,
            out MonsterBookOwnershipSyncPayload result,
            out string detail)
        {
            result = new MonsterBookOwnershipSyncPayload(
                clearRequested: false,
                replaceExisting: true,
                characterId: null,
                characterName: string.Empty,
                registeredMobId: null,
                cardCountsByMob: new Dictionary<int, int>(),
                statusText: string.Empty);
            detail = null;

            if (payload == null || payload.Length == 0)
            {
                detail = "Monster Book ownership sync payload is missing.";
                return false;
            }

            if (TryDecodeMonsterBookOwnershipSyncJsonPayload(payload, out result, out detail))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                return false;
            }

            if (TryDecodeMonsterBookOwnershipSyncBinaryPayload(payload, out result, out detail))
            {
                return true;
            }

            if (TryDecodeMonsterBookOwnershipSyncCompactBinaryPayload(payload, out result, out detail))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = "Monster Book ownership sync payload could not be decoded from JSON or binary shape.";
            }

            return false;
        }

        private static bool TryDecodeMonsterBookOwnershipSyncJsonPayload(
            byte[] payload,
            out MonsterBookOwnershipSyncPayload result,
            out string detail)
        {
            result = default;
            detail = null;
            ReadOnlySpan<byte> trimmed = TrimJsonPayload(payload);
            if (trimmed.Length <= 0 || trimmed[0] != (byte)'{')
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed.ToArray());
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    detail = "Monster Book ownership sync JSON payload must be an object.";
                    return false;
                }

                bool clearRequested = ReadBoolean(root, false, "clear", "reset", "clearRequested");
                bool replaceExisting = ReadBoolean(root, true, "replaceExisting", "replace", "overwrite");
                int? characterId = NormalizePositiveInt(ReadInt(root, "characterId", "charId", "id"));
                string characterName = ReadString(root, "characterName", "charName", "name", "ownerName");
                int? registeredMobId = NormalizePositiveInt(ReadInt(root, "registeredMobId", "registeredMob", "selectedMobId"));
                string statusText = ReadString(root, "statusText", "message", "text", "notice");

                if (root.TryGetProperty("owner", out JsonElement ownerElement)
                    && ownerElement.ValueKind == JsonValueKind.Object)
                {
                    characterId ??= NormalizePositiveInt(ReadInt(ownerElement, "characterId", "charId", "id"));
                    if (string.IsNullOrWhiteSpace(characterName))
                    {
                        characterName = ReadString(ownerElement, "characterName", "charName", "name");
                    }
                }

                Dictionary<int, int> counts = new();
                if (TryReadMonsterBookCardCounts(root, out Dictionary<int, int> parsedCounts))
                {
                    counts = parsedCounts;
                }

                if (clearRequested)
                {
                    counts.Clear();
                    replaceExisting = true;
                }

                result = new MonsterBookOwnershipSyncPayload(
                    clearRequested,
                    replaceExisting,
                    characterId,
                    characterName,
                    registeredMobId,
                    counts,
                    statusText);
                detail = "Decoded Monster Book ownership sync JSON payload.";
                return true;
            }
            catch (JsonException ex)
            {
                detail = $"Monster Book ownership sync JSON payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeMonsterBookOwnershipSyncBinaryPayload(
            byte[] payload,
            out MonsterBookOwnershipSyncPayload result,
            out string detail)
        {
            result = default;
            detail = null;
            if (payload == null || payload.Length < 8)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
                if (reader.ReadByte() != (byte)'M'
                    || reader.ReadByte() != (byte)'B'
                    || reader.ReadByte() != (byte)'O'
                    || reader.ReadByte() != (byte)'S')
                {
                    return false;
                }

                byte flags = reader.ReadByte();
                bool clearRequested = (flags & 0x01) != 0;
                bool replaceExisting = (flags & 0x02) != 0 || clearRequested;
                bool hasCharacterId = (flags & 0x04) != 0;
                bool hasRegisteredMob = (flags & 0x08) != 0;
                bool hasCharacterName = (flags & 0x10) != 0;
                bool hasStatusText = (flags & 0x20) != 0;

                int? characterId = hasCharacterId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;
                int? registeredMobId = hasRegisteredMob
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;
                ushort entryCount = reader.ReadUInt16();
                Dictionary<int, int> counts = new();
                for (int i = 0; i < entryCount; i++)
                {
                    int mobId = reader.ReadInt32();
                    int count = reader.ReadByte();
                    if (mobId > 0 && count > 0)
                    {
                        counts[mobId] = Math.Clamp(count, 0, 5);
                    }
                }

                string characterName = hasCharacterName
                    ? ReadLengthPrefixedUtf8(reader)
                    : string.Empty;
                string statusText = hasStatusText
                    ? ReadLengthPrefixedUtf8(reader)
                    : string.Empty;

                if (clearRequested)
                {
                    counts.Clear();
                }

                result = new MonsterBookOwnershipSyncPayload(
                    clearRequested,
                    replaceExisting,
                    characterId,
                    characterName,
                    registeredMobId,
                    counts,
                    statusText);
                detail = "Decoded Monster Book ownership sync binary payload.";
                return true;
            }
            catch (Exception ex)
            {
                detail = $"Monster Book ownership sync binary payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeMonsterBookOwnershipSyncCompactBinaryPayload(
            byte[] payload,
            out MonsterBookOwnershipSyncPayload result,
            out string detail)
        {
            result = default;
            detail = null;
            if (payload == null || payload.Length < 3)
            {
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

                byte flags = reader.ReadByte();
                bool clearRequested = (flags & 0x01) != 0;
                bool replaceExisting = (flags & 0x02) != 0 || clearRequested;
                bool hasCharacterId = (flags & 0x04) != 0;
                bool hasCharacterName = (flags & 0x08) != 0;
                bool hasRegisteredMobId = (flags & 0x10) != 0;
                bool hasStatusText = (flags & 0x20) != 0;

                int? characterId = hasCharacterId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;

                string characterName = hasCharacterName
                    ? ReadLengthPrefixedUtf8(reader)
                    : string.Empty;

                int? registeredMobId = hasRegisteredMobId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;

                ushort entryCount = reader.ReadUInt16();
                if (entryCount > 1024)
                {
                    detail = $"Monster Book ownership sync compact payload entry count {entryCount.ToString(CultureInfo.InvariantCulture)} is outside expected range.";
                    return false;
                }

                Dictionary<int, int> counts = new();
                for (int i = 0; i < entryCount; i++)
                {
                    int mobId = reader.ReadInt32();
                    int count = reader.ReadByte();
                    if (mobId > 0 && count > 0)
                    {
                        counts[mobId] = Math.Clamp(count, 0, 5);
                    }
                }

                string statusText = hasStatusText
                    ? ReadLengthPrefixedUtf8(reader)
                    : string.Empty;

                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    return false;
                }

                if (clearRequested)
                {
                    counts.Clear();
                }

                result = new MonsterBookOwnershipSyncPayload(
                    clearRequested,
                    replaceExisting,
                    characterId,
                    characterName,
                    registeredMobId,
                    counts,
                    statusText);
                detail = "Decoded Monster Book ownership sync compact binary payload.";
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsMonsterBookOwnershipSyncForPendingRequest(
            PendingMonsterBookRegistrationRequest pendingRequest,
            int syncedCharacterId,
            string syncedCharacterName,
            int syncedRegisteredMobId)
        {
            if (pendingRequest == null)
            {
                return false;
            }

            bool sameCharacter = false;
            if (syncedCharacterId > 0 && pendingRequest.CharacterId > 0)
            {
                sameCharacter = syncedCharacterId == pendingRequest.CharacterId;
            }
            else if (!string.IsNullOrWhiteSpace(syncedCharacterName)
                && !string.IsNullOrWhiteSpace(pendingRequest.CharacterName))
            {
                sameCharacter = string.Equals(
                    syncedCharacterName.Trim(),
                    pendingRequest.CharacterName.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (!sameCharacter)
            {
                return false;
            }

            return pendingRequest.Registered
                ? syncedRegisteredMobId == pendingRequest.MobId
                : syncedRegisteredMobId <= 0 || syncedRegisteredMobId != pendingRequest.MobId;
        }

        private static string ReadLengthPrefixedUtf8(BinaryReader reader)
        {
            if (reader == null || reader.BaseStream == null || !reader.BaseStream.CanRead)
            {
                return string.Empty;
            }

            if (reader.BaseStream.Position + sizeof(ushort) > reader.BaseStream.Length)
            {
                return string.Empty;
            }

            ushort byteCount = reader.ReadUInt16();
            if (byteCount <= 0 || reader.BaseStream.Position + byteCount > reader.BaseStream.Length)
            {
                return string.Empty;
            }

            byte[] bytes = reader.ReadBytes(byteCount);
            return bytes?.Length > 0
                ? Encoding.UTF8.GetString(bytes).Trim()
                : string.Empty;
        }

        private static bool TryReadMonsterBookCardCounts(JsonElement root, out Dictionary<int, int> counts)
        {
            counts = new Dictionary<int, int>();
            foreach (string propertyName in new[] { "cardCountsByMob", "cards", "counts", "ownership", "bookByMob" })
            {
                if (!root.TryGetProperty(propertyName, out JsonElement element))
                {
                    continue;
                }

                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mobId)
                            || mobId <= 0)
                        {
                            continue;
                        }

                        int? count = TryReadJsonInt(property.Value);
                        if (count.GetValueOrDefault() <= 0)
                        {
                            continue;
                        }

                        counts[mobId] = Math.Clamp(count.Value, 0, 5);
                    }

                    return true;
                }

                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in element.EnumerateArray())
                    {
                        if (entry.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        int? mobId = ReadInt(entry, "mobId", "mob", "id");
                        int? count = ReadInt(entry, "count", "copies", "ownedCopies", "value");
                        if (mobId.GetValueOrDefault() <= 0 || count.GetValueOrDefault() <= 0)
                        {
                            continue;
                        }

                        counts[mobId.Value] = Math.Clamp(count.Value, 0, 5);
                    }

                    return true;
                }
            }

            return false;
        }

        private static int? TryReadJsonInt(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String
                && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }

            return null;
        }

        private static bool TryDecodeMonsterBookRegistrationJsonPayload(
            byte[] payload,
            out MonsterBookRegistrationResultPayload result,
            out string detail)
        {
            result = default;
            detail = null;
            ReadOnlySpan<byte> trimmed = TrimJsonPayload(payload);
            if (trimmed.Length <= 0 || trimmed[0] != (byte)'{')
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed.ToArray());
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    detail = "Monster Book registration result JSON payload must be an object.";
                    return false;
                }

                bool success = ReadBoolean(root, true, "success", "succeeded", "ok", "accepted")
                    && !ReadBoolean(root, false, "failure", "failed", "rejected", "error");
                int? requestId = NormalizePositiveInt(ReadInt(root, "requestId", "requestToken", "token", "sequence", "seq"));
                int? mobId = NormalizePositiveInt(ReadInt(root, "mobId", "mob", "targetMobId", "nMobID"));
                bool? registered = ReadNullableBoolean(root, "registered", "isRegistered", "register", "cover");
                int? reasonCode = ReadInt(root, "reasonCode", "reason", "errorCode", "rejectReason");
                string statusText = ReadString(root, "statusText", "message", "text", "notice", "localizedText");

                result = new MonsterBookRegistrationResultPayload(
                    success,
                    requestId,
                    mobId,
                    registered,
                    reasonCode,
                    statusText);
                detail = "Decoded Monster Book registration result JSON payload.";
                return true;
            }
            catch (JsonException ex)
            {
                detail = $"Monster Book registration result JSON payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeMonsterBookRegistrationBinaryPayload(
            byte[] payload,
            out MonsterBookRegistrationResultPayload result,
            out string detail)
        {
            result = new MonsterBookRegistrationResultPayload(
                success: true,
                requestId: null,
                mobId: null,
                registered: null,
                reasonCode: null,
                statusText: string.Empty);
            detail = null;
            if (payload == null || payload.Length == 0)
            {
                return false;
            }

            int offset = 0;
            bool success = true;
            if (payload[offset] == 0 || payload[offset] == 1)
            {
                success = payload[offset] == 0;
                offset++;
            }

            int? mobId = null;
            if (payload.Length - offset >= sizeof(int))
            {
                int candidateMobId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                if (candidateMobId > 0)
                {
                    mobId = candidateMobId;
                    offset += sizeof(int);
                }
            }

            bool? registered = null;
            if (payload.Length - offset >= 1 && (payload[offset] == 0 || payload[offset] == 1))
            {
                registered = payload[offset] != 0;
                offset++;
            }

            int? requestId = null;
            if (payload.Length - offset >= sizeof(int))
            {
                int candidateRequestId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                if (candidateRequestId > 0)
                {
                    requestId = candidateRequestId;
                    offset += sizeof(int);
                }
            }

            int? reasonCode = null;
            if (payload.Length - offset >= sizeof(int))
            {
                reasonCode = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
                offset += sizeof(int);
            }

            string statusText = payload.Length - offset > 0
                ? DecodeMonsterBookResultStatusText(payload.AsSpan(offset))
                : string.Empty;
            result = new MonsterBookRegistrationResultPayload(
                success,
                requestId,
                mobId,
                registered,
                reasonCode,
                statusText);
            detail = "Decoded Monster Book registration result binary payload.";
            return true;
        }

        private static string DecodeMonsterBookResultStatusText(ReadOnlySpan<byte> payload)
        {
            if (payload.Length <= 0)
            {
                return string.Empty;
            }

            if (TryDecodeLengthPrefixedResultStatusText(payload, out string lengthPrefixedText))
            {
                return lengthPrefixedText;
            }

            if (LooksLikeUtf16Le(payload))
            {
                int terminatorIndex = FindUtf16LeTerminator(payload);
                if (terminatorIndex >= 0)
                {
                    payload = payload[..terminatorIndex];
                }

                if ((payload.Length & 1) != 0)
                {
                    payload = payload[..^1];
                }

                return payload.Length <= 0
                    ? string.Empty
                    : Encoding.Unicode.GetString(payload).Trim();
            }

            int utf8TerminatorIndex = payload.IndexOf((byte)0);
            if (utf8TerminatorIndex >= 0)
            {
                payload = payload[..utf8TerminatorIndex];
            }

            return Encoding.UTF8.GetString(payload).Trim();
        }

        private static bool TryDecodeLengthPrefixedResultStatusText(ReadOnlySpan<byte> payload, out string statusText)
        {
            statusText = string.Empty;
            if (payload.Length < sizeof(ushort))
            {
                return false;
            }

            ushort lengthPrefix = BinaryPrimitives.ReadUInt16LittleEndian(payload);
            ReadOnlySpan<byte> encodedText = payload[sizeof(ushort)..];
            if (lengthPrefix == encodedText.Length
                || (encodedText.Length > 0
                    && (lengthPrefix * sizeof(char)) == encodedText.Length
                    && LooksLikeUtf16Le(encodedText)))
            {
                statusText = DecodeMonsterBookResultStatusText(encodedText);
                return true;
            }

            return false;
        }

        private static int FindUtf16LeTerminator(ReadOnlySpan<byte> payload)
        {
            for (int i = 0; i + 1 < payload.Length; i += 2)
            {
                if (payload[i] == 0 && payload[i + 1] == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool LooksLikeUtf16Le(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 2 || (payload.Length & 1) != 0)
            {
                return false;
            }

            int zeroCount = 0;
            for (int i = 1; i < payload.Length; i += 2)
            {
                if (payload[i] == 0)
                {
                    zeroCount++;
                }
            }

            return zeroCount >= payload.Length / 4;
        }

        private static ReadOnlySpan<byte> TrimJsonPayload(byte[] payload)
        {
            ReadOnlySpan<byte> span = payload ?? Array.Empty<byte>();
            while (span.Length > 0 && char.IsWhiteSpace((char)span[0]))
            {
                span = span[1..];
            }

            while (span.Length > 0 && (span[^1] == 0 || char.IsWhiteSpace((char)span[^1])))
            {
                span = span[..^1];
            }

            return span;
        }

        private static int? NormalizePositiveInt(int? value)
        {
            return value.HasValue && value.Value > 0
                ? value
                : null;
        }

        private static int? ReadInt(JsonElement root, params string[] names)
        {
            foreach (string name in names)
            {
                if (!root.TryGetProperty(name, out JsonElement value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                {
                    return number;
                }

                if (value.ValueKind == JsonValueKind.String
                    && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    return number;
                }
            }

            return null;
        }

        private static bool ReadBoolean(JsonElement root, bool defaultValue, params string[] names)
        {
            foreach (string name in names)
            {
                if (!root.TryGetProperty(name, out JsonElement value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (value.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                {
                    return number != 0;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    string text = value.GetString();
                    if (bool.TryParse(text, out bool parsedBool))
                    {
                        return parsedBool;
                    }

                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        return number != 0;
                    }
                }
            }

            return defaultValue;
        }

        private static bool? ReadNullableBoolean(JsonElement root, params string[] names)
        {
            foreach (string name in names)
            {
                if (!root.TryGetProperty(name, out JsonElement value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                if (value.ValueKind == JsonValueKind.False)
                {
                    return false;
                }

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
                {
                    return number != 0;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    string text = value.GetString();
                    if (bool.TryParse(text, out bool parsedBool))
                    {
                        return parsedBool;
                    }

                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                    {
                        return number != 0;
                    }
                }
            }

            return null;
        }

        private static string ReadString(JsonElement root, params string[] names)
        {
            foreach (string name in names)
            {
                if (root.TryGetProperty(name, out JsonElement value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
