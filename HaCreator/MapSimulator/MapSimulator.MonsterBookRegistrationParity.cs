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
        private const int MonsterBookOwnershipSaveResponseDelayMs = 120;
        private const int MonsterBookOwnershipSaveSyntheticResultTimeoutMs = 2000;
        private const int MonsterBookOwnershipSaveOfficialSessionTimeoutMs = 5000;
        // No recovered dedicated Monster Book save opcode is wired yet in this local utility seam,
        // so save requests currently ride the ownership-sync channel contract.
        private const int MonsterBookOwnershipSaveRequestOpcode = LocalUtilityPacketInboxManager.MonsterBookOwnershipSyncPacketType;
        private PendingMonsterBookRegistrationRequest _pendingMonsterBookRegistrationRequest;
        private PendingMonsterBookOwnershipSaveRequest _pendingMonsterBookOwnershipSaveRequest;
        private int _nextMonsterBookRegistrationRequestId = 1;
        private int _nextMonsterBookOwnershipSaveRequestId = 1;

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
                bool hasOwnershipSnapshot,
                bool? saveAccepted,
                int? requestId,
                int? characterId,
                string characterName,
                int? registeredMobId,
                IReadOnlyDictionary<int, int> cardCountsByMob,
                string statusText)
            {
                ClearRequested = clearRequested;
                ReplaceExisting = replaceExisting;
                HasOwnershipSnapshot = hasOwnershipSnapshot;
                SaveAccepted = saveAccepted;
                RequestId = requestId;
                CharacterId = characterId;
                CharacterName = characterName ?? string.Empty;
                RegisteredMobId = registeredMobId;
                CardCountsByMob = cardCountsByMob ?? new Dictionary<int, int>();
                StatusText = statusText ?? string.Empty;
            }

            public bool ClearRequested { get; }
            public bool ReplaceExisting { get; }
            public bool HasOwnershipSnapshot { get; }
            public bool? SaveAccepted { get; }
            public int? RequestId { get; }
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

        private sealed class PendingMonsterBookOwnershipSaveRequest
        {
            public CharacterBuild Build { get; init; }
            public int CharacterId { get; init; }
            public string CharacterName { get; init; } = string.Empty;
            public int RequestId { get; init; }
            public int RegisteredMobId { get; init; }
            public IReadOnlyDictionary<int, int> CardCountsByMob { get; init; } = new Dictionary<int, int>();
            public byte[] Payload { get; init; } = Array.Empty<byte>();
            public long SentTick { get; init; }
            public int ResponseDelayMs { get; init; } = MonsterBookOwnershipSaveResponseDelayMs;
            public string SourceSummary { get; init; } = string.Empty;
            public bool SyntheticResultQueued { get; set; }
            public long SyntheticResultQueuedTick { get; set; } = long.MinValue;
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

            MonsterBookSnapshot snapshot = _monsterBookManager.SetRegisteredCard(
                request.Build,
                request.CharacterId,
                request.CharacterName,
                request.MobId,
                request.Registered,
                persistToDisk: false);

            QueuePacketOwnedMonsterBookOwnershipSaveApply(
                request.Build,
                request.CharacterId,
                request.CharacterName,
                snapshot,
                "CBookDlg register/release result",
                showFeedback: false);

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
            CharacterBuild targetBuild = ResolvePacketOwnedMonsterBookSyncBuild(
                resolvedCharacterId,
                resolvedCharacterName,
                ownerIdentity.Build);
            if (resolvedCharacterId <= 0 && targetBuild?.Id > 0)
            {
                resolvedCharacterId = targetBuild.Id;
            }

            if (string.IsNullOrWhiteSpace(resolvedCharacterName))
            {
                resolvedCharacterName = targetBuild?.Name ?? ownerIdentity.CharacterName;
            }

            if (!sync.HasOwnershipSnapshot)
            {
                PendingMonsterBookOwnershipSaveRequest pendingSaveRequest = _pendingMonsterBookOwnershipSaveRequest;
                bool acknowledgedPendingSave = pendingSaveRequest != null
                    && IsMonsterBookOwnershipSaveAckForPendingRequest(
                        pendingSaveRequest,
                        sync.RequestId,
                        resolvedCharacterId,
                        resolvedCharacterName);
                bool persistedAckOwnedSnapshot = false;
                if (acknowledgedPendingSave)
                {
                    if (sync.SaveAccepted.GetValueOrDefault(true))
                    {
                        _monsterBookManager.ApplyOwnershipSync(
                            pendingSaveRequest.Build,
                            pendingSaveRequest.CharacterId,
                            pendingSaveRequest.CharacterName,
                            pendingSaveRequest.CardCountsByMob,
                            registeredMobId: pendingSaveRequest.RegisteredMobId,
                            replaceExisting: true);
                        persistedAckOwnedSnapshot = true;
                    }

                    _pendingMonsterBookOwnershipSaveRequest = null;
                }

                StampPacketOwnedUtilityRequestState();
                string ackSummary = sync.SaveAccepted.HasValue && !sync.SaveAccepted.Value
                    ? "Monster Book ownership-save acknowledgement reported rejection without an ownership snapshot apply."
                    : persistedAckOwnedSnapshot
                        ? "Monster Book ownership-save acknowledgement accepted and persisted the pending packet-owned ownership snapshot without forcing a synthetic ownership-sync payload."
                    : "Monster Book ownership-save acknowledgement was applied without forcing a synthetic ownership snapshot.";
                string ackRequestIdText = sync.RequestId.HasValue && sync.RequestId.Value > 0
                    ? $" request #{sync.RequestId.Value.ToString(CultureInfo.InvariantCulture)}"
                    : string.Empty;
                message = AppendMonsterBookRegistrationStatusText(
                    sync.StatusText,
                    $"{ackSummary}{ackRequestIdText}");
                return true;
            }

            MonsterBookSnapshot snapshot = _monsterBookManager.ApplyOwnershipSync(
                targetBuild,
                resolvedCharacterId,
                resolvedCharacterName,
                sync.CardCountsByMob,
                registeredMobId: sync.RegisteredMobId ?? 0,
                replaceExisting: sync.ClearRequested || sync.ReplaceExisting);

            if (_pendingMonsterBookOwnershipSaveRequest != null
                && IsMonsterBookOwnershipSyncForPendingSaveRequest(
                    _pendingMonsterBookOwnershipSaveRequest,
                    sync.RequestId,
                    resolvedCharacterId,
                    resolvedCharacterName,
                    snapshot))
            {
                _pendingMonsterBookOwnershipSaveRequest = null;
            }

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

        private CharacterBuild ResolvePacketOwnedMonsterBookSyncBuild(
            int characterId,
            string characterName,
            CharacterBuild fallbackBuild)
        {
            CharacterBuild activeBuild = _playerManager?.Player?.Build;
            if (characterId > 0)
            {
                if (activeBuild?.Id == characterId)
                {
                    return activeBuild;
                }

                if (fallbackBuild?.Id == characterId)
                {
                    return fallbackBuild;
                }

                foreach (LoginCharacterRosterEntry entry in _loginCharacterRoster.Entries)
                {
                    if (entry?.Build?.Id == characterId)
                    {
                        return entry.Build;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(characterName))
            {
                string lookupName = characterName.Trim();
                if (!string.IsNullOrWhiteSpace(activeBuild?.Name)
                    && string.Equals(activeBuild.Name.Trim(), lookupName, StringComparison.OrdinalIgnoreCase))
                {
                    return activeBuild;
                }

                if (!string.IsNullOrWhiteSpace(fallbackBuild?.Name)
                    && string.Equals(fallbackBuild.Name.Trim(), lookupName, StringComparison.OrdinalIgnoreCase))
                {
                    return fallbackBuild;
                }

                foreach (LoginCharacterRosterEntry entry in _loginCharacterRoster.Entries)
                {
                    if (entry?.Build != null
                        && !string.IsNullOrWhiteSpace(entry.Build.Name)
                        && string.Equals(entry.Build.Name.Trim(), lookupName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Build;
                    }
                }
            }

            return fallbackBuild ?? activeBuild ?? _loginCharacterRoster.SelectedEntry?.Build;
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

        private int ReserveMonsterBookOwnershipSaveRequestId()
        {
            if (_nextMonsterBookOwnershipSaveRequestId <= 0)
            {
                _nextMonsterBookOwnershipSaveRequestId = 1;
            }

            int requestId = _nextMonsterBookOwnershipSaveRequestId;
            _nextMonsterBookOwnershipSaveRequestId = requestId == int.MaxValue
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
                hasOwnershipSnapshot: false,
                saveAccepted: null,
                requestId: null,
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

                JsonElement payloadRoot = ResolveMonsterBookJsonPayloadRoot(
                    root,
                    IsMonsterBookOwnershipSyncJsonObject,
                    out bool usedNestedRoot);

                bool clearRequested = ReadBoolean(payloadRoot, false, "clear", "reset", "clearRequested", "clear_request", "clear_requested");
                bool replaceExisting = ReadBoolean(payloadRoot, true, "replaceExisting", "replace", "overwrite", "replace_existing");
                bool? saveAccepted = ReadNullableBoolean(payloadRoot, "success", "succeeded", "ok", "accepted", "saved", "saveAccepted", "save_accepted", "saveOk", "save_ok", "acknowledged", "ack");
                bool? saveRejected = ReadNullableBoolean(payloadRoot, "failure", "failed", "rejected", "error", "denied", "saveRejected", "save_rejected", "saveFail", "save_fail", "nack", "reject");
                int? requestId = NormalizePositiveInt(ReadInt(payloadRoot, "requestId", "requestToken", "requestSeq", "sequence", "request_id", "requestID", "reqId", "requestNo"));
                int? characterId = NormalizePositiveInt(ReadInt(payloadRoot, "characterId", "charId", "id", "character_id", "ownerId", "owner_id"));
                string characterName = ReadString(payloadRoot, "characterName", "charName", "name", "ownerName", "character_name", "owner_name");
                int? registeredMobId = NormalizePositiveInt(ReadInt(payloadRoot, "registeredMobId", "registeredMob", "selectedMobId", "registered_mob_id", "selected_mob_id", "registerMobId"));
                string statusText = ReadString(payloadRoot, "statusText", "message", "text", "notice", "status_text", "status", "detail");
                if (string.IsNullOrWhiteSpace(statusText) && usedNestedRoot)
                {
                    statusText = ReadString(root, "statusText", "message", "text", "notice", "status_text", "status", "detail");
                }
                if (!requestId.HasValue && usedNestedRoot)
                {
                    requestId = NormalizePositiveInt(ReadInt(root, "requestId", "requestToken", "requestSeq", "sequence", "request_id", "requestID", "reqId", "requestNo"));
                }
                if (!saveAccepted.HasValue && usedNestedRoot)
                {
                    saveAccepted = ReadNullableBoolean(root, "success", "succeeded", "ok", "accepted", "saved", "saveAccepted", "save_accepted", "saveOk", "save_ok", "acknowledged", "ack");
                }

                if (!saveRejected.HasValue && usedNestedRoot)
                {
                    saveRejected = ReadNullableBoolean(root, "failure", "failed", "rejected", "error", "denied", "saveRejected", "save_rejected", "saveFail", "save_fail", "nack", "reject");
                }

                if (TryResolveMonsterBookOwnershipSaveResultJsonObject(payloadRoot, root, usedNestedRoot, out JsonElement saveResultElement))
                {
                    requestId ??= NormalizePositiveInt(ReadInt(saveResultElement, "requestId", "requestToken", "requestSeq", "sequence", "token", "seq", "request_id", "requestID", "reqId", "requestNo"));
                    saveAccepted ??= ReadNullableBoolean(saveResultElement, "success", "succeeded", "ok", "accepted", "saved", "saveAccepted", "save_accepted", "saveOk", "save_ok", "acknowledged", "ack");
                    saveRejected ??= ReadNullableBoolean(saveResultElement, "failure", "failed", "rejected", "error", "denied", "saveRejected", "save_rejected", "saveFail", "save_fail", "nack", "reject");
                    if (string.IsNullOrWhiteSpace(statusText))
                    {
                        statusText = ReadString(saveResultElement, "statusText", "message", "text", "notice", "status_text", "status", "detail");
                    }
                }

                if (!saveAccepted.HasValue && saveRejected == true)
                {
                    saveAccepted = false;
                }

                if (payloadRoot.TryGetProperty("owner", out JsonElement ownerElement)
                    && ownerElement.ValueKind == JsonValueKind.Object)
                {
                    characterId ??= NormalizePositiveInt(ReadInt(ownerElement, "characterId", "charId", "id", "character_id", "ownerId", "owner_id"));
                    if (string.IsNullOrWhiteSpace(characterName))
                    {
                        characterName = ReadString(ownerElement, "characterName", "charName", "name", "character_name", "owner_name");
                    }

                    registeredMobId ??= NormalizePositiveInt(ReadInt(ownerElement, "registeredMobId", "registeredMob", "selectedMobId", "registered_mob_id", "selected_mob_id", "registerMobId"));
                }
                else if (usedNestedRoot
                    && root.TryGetProperty("owner", out JsonElement outerOwnerElement)
                    && outerOwnerElement.ValueKind == JsonValueKind.Object)
                {
                    characterId ??= NormalizePositiveInt(ReadInt(outerOwnerElement, "characterId", "charId", "id", "character_id", "ownerId", "owner_id"));
                    if (string.IsNullOrWhiteSpace(characterName))
                    {
                        characterName = ReadString(outerOwnerElement, "characterName", "charName", "name", "character_name", "owner_name");
                    }

                    registeredMobId ??= NormalizePositiveInt(ReadInt(outerOwnerElement, "registeredMobId", "registeredMob", "selectedMobId", "registered_mob_id", "selected_mob_id", "registerMobId"));
                }

                Dictionary<int, int> counts = new();
                bool hasCountsPayload = TryReadMonsterBookCardCounts(payloadRoot, out Dictionary<int, int> parsedCounts)
                    || (usedNestedRoot && TryReadMonsterBookCardCounts(root, out parsedCounts));
                if (hasCountsPayload)
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
                    hasOwnershipSnapshot: clearRequested || hasCountsPayload || registeredMobId.HasValue,
                    saveAccepted: saveAccepted,
                    requestId,
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
                bool hasRequestId = (flags & 0x40) != 0;
                bool hasSaveAccepted = (flags & 0x80) != 0;

                int? characterId = hasCharacterId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;
                int? registeredMobId = hasRegisteredMob
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;
                int? requestId = hasRequestId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;
                bool? saveAccepted = hasSaveAccepted
                    ? ReadNullableBooleanByte(reader)
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
                    hasOwnershipSnapshot: clearRequested || hasRegisteredMob || counts.Count > 0,
                    saveAccepted: saveAccepted,
                    requestId,
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
                bool hasRequestId = (flags & 0x40) != 0;
                bool hasSaveAccepted = (flags & 0x80) != 0;

                int? characterId = hasCharacterId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;

                string characterName = hasCharacterName
                    ? ReadLengthPrefixedUtf8(reader)
                    : string.Empty;

                int? registeredMobId = hasRegisteredMobId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;
                int? requestId = hasRequestId
                    ? NormalizePositiveInt(reader.ReadInt32())
                    : null;
                bool? saveAccepted = hasSaveAccepted
                    ? ReadNullableBooleanByte(reader)
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
                    hasOwnershipSnapshot: clearRequested || hasRegisteredMobId || counts.Count > 0,
                    saveAccepted: saveAccepted,
                    requestId,
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

        private static bool IsMonsterBookOwnershipSyncForPendingSaveRequest(
            PendingMonsterBookOwnershipSaveRequest pendingRequest,
            int? syncedRequestId,
            int syncedCharacterId,
            string syncedCharacterName,
            MonsterBookSnapshot syncedSnapshot)
        {
            if (pendingRequest == null)
            {
                return false;
            }

            if (syncedRequestId.HasValue && syncedRequestId.Value > 0)
            {
                return syncedRequestId.Value == pendingRequest.RequestId;
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

            if (syncedSnapshot == null)
            {
                return false;
            }

            if (syncedSnapshot.RegisteredCardMobId != pendingRequest.RegisteredMobId)
            {
                return false;
            }

            Dictionary<int, int> syncedCounts = BuildMonsterBookOwnedCountsByMob(syncedSnapshot);
            if (syncedCounts.Count != pendingRequest.CardCountsByMob.Count)
            {
                return false;
            }

            foreach (KeyValuePair<int, int> entry in pendingRequest.CardCountsByMob)
            {
                if (!syncedCounts.TryGetValue(entry.Key, out int syncedCount)
                    || syncedCount != entry.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsMonsterBookOwnershipSaveAckForPendingRequest(
            PendingMonsterBookOwnershipSaveRequest pendingRequest,
            int? syncedRequestId,
            int syncedCharacterId,
            string syncedCharacterName)
        {
            if (pendingRequest == null)
            {
                return false;
            }

            if (syncedRequestId.HasValue && syncedRequestId.Value > 0)
            {
                return syncedRequestId.Value == pendingRequest.RequestId;
            }

            if (syncedCharacterId > 0 && pendingRequest.CharacterId > 0)
            {
                return syncedCharacterId == pendingRequest.CharacterId;
            }

            return !string.IsNullOrWhiteSpace(syncedCharacterName)
                && !string.IsNullOrWhiteSpace(pendingRequest.CharacterName)
                && string.Equals(
                    syncedCharacterName.Trim(),
                    pendingRequest.CharacterName.Trim(),
                    StringComparison.OrdinalIgnoreCase);
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

        private static bool? ReadNullableBooleanByte(BinaryReader reader)
        {
            if (reader == null || reader.BaseStream == null || !reader.BaseStream.CanRead)
            {
                return null;
            }

            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                return null;
            }

            byte value = reader.ReadByte();
            return value switch
            {
                0 => false,
                1 => true,
                _ => null
            };
        }

        private static bool TryReadMonsterBookCardCounts(JsonElement root, out Dictionary<int, int> counts)
        {
            counts = new Dictionary<int, int>();
            foreach (string propertyName in new[] { "cardCountsByMob", "card_counts_by_mob", "cardCounts", "cards", "counts", "ownership", "bookByMob", "book_by_mob", "ownedCardsByMob", "owned_cards_by_mob" })
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

                        int? mobId = ReadInt(entry, "mobId", "mob", "id", "mob_id", "monsterId", "monster_id");
                        int? count = ReadInt(entry, "count", "copies", "ownedCopies", "value", "owned_copies", "cardCount", "card_count");
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

                JsonElement payloadRoot = ResolveMonsterBookJsonPayloadRoot(
                    root,
                    IsMonsterBookRegistrationResultJsonObject,
                    out bool usedNestedRoot);

                bool success = ReadBoolean(payloadRoot, true, "success", "succeeded", "ok", "accepted", "isSuccess", "is_success")
                    && !ReadBoolean(payloadRoot, false, "failure", "failed", "rejected", "error", "isFailure", "is_failure");
                int? requestId = NormalizePositiveInt(ReadInt(payloadRoot, "requestId", "requestToken", "token", "sequence", "seq", "request_id", "requestID", "reqId"));
                int? mobId = NormalizePositiveInt(ReadInt(payloadRoot, "mobId", "mob", "targetMobId", "nMobID", "mob_id", "target_mob_id"));
                bool? registered = ReadNullableBoolean(payloadRoot, "registered", "isRegistered", "register", "cover", "is_registered");
                int? reasonCode = ReadInt(payloadRoot, "reasonCode", "reason", "errorCode", "rejectReason", "reason_code", "error_code");
                string statusText = ReadString(payloadRoot, "statusText", "message", "text", "notice", "localizedText", "status_text", "status", "detail");
                if (string.IsNullOrWhiteSpace(statusText) && usedNestedRoot)
                {
                    statusText = ReadString(root, "statusText", "message", "text", "notice", "localizedText", "status_text", "status", "detail");
                }

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

        private static JsonElement ResolveMonsterBookJsonPayloadRoot(
            JsonElement root,
            Func<JsonElement, bool> isPayloadRoot,
            out bool usedNestedRoot)
        {
            usedNestedRoot = false;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return root;
            }

            if (isPayloadRoot != null && isPayloadRoot(root))
            {
                return root;
            }

            foreach (string candidateName in new[] { "monsterBook", "book", "result", "registration", "ownershipSync", "ownership_save", "ownershipSave", "save", "saveResult", "saveAck", "saveResponse", "ack", "response", "packet", "payload", "data", "body" })
            {
                if (!root.TryGetProperty(candidateName, out JsonElement nested))
                {
                    continue;
                }

                if (nested.ValueKind == JsonValueKind.Object
                    && (isPayloadRoot == null || isPayloadRoot(nested)))
                {
                    usedNestedRoot = true;
                    return nested;
                }
            }

            return root;
        }

        private static bool IsMonsterBookOwnershipSyncJsonObject(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (element.TryGetProperty("owner", out JsonElement owner)
                && owner.ValueKind == JsonValueKind.Object)
            {
                return true;
            }

            return element.TryGetProperty("cardCountsByMob", out _)
                || element.TryGetProperty("card_counts_by_mob", out _)
                || element.TryGetProperty("cardCounts", out _)
                || element.TryGetProperty("cards", out _)
                || element.TryGetProperty("counts", out _)
                || element.TryGetProperty("ownership", out _)
                || element.TryGetProperty("bookByMob", out _)
                || element.TryGetProperty("book_by_mob", out _)
                || element.TryGetProperty("clear", out _)
                || element.TryGetProperty("clearRequested", out _)
                || element.TryGetProperty("clear_requested", out _)
                || element.TryGetProperty("replaceExisting", out _)
                || element.TryGetProperty("replace_existing", out _)
                || element.TryGetProperty("save", out _)
                || element.TryGetProperty("saveResult", out _)
                || element.TryGetProperty("saveAck", out _)
                || element.TryGetProperty("ack", out _)
                || element.TryGetProperty("registeredMobId", out _)
                || element.TryGetProperty("selectedMobId", out _)
                || element.TryGetProperty("registered_mob_id", out _)
                || element.TryGetProperty("selected_mob_id", out _);
        }

        private static bool TryResolveMonsterBookOwnershipSaveResultJsonObject(
            JsonElement payloadRoot,
            JsonElement root,
            bool usedNestedRoot,
            out JsonElement saveResultElement)
        {
            if (TryFindNestedPropertyObject(payloadRoot, maxDepth: 3, out saveResultElement, "saveResult", "save", "saveAck", "saveResponse", "ack", "response"))
            {
                return true;
            }

            if (usedNestedRoot
                && TryFindNestedPropertyObject(root, maxDepth: 3, out saveResultElement, "saveResult", "save", "saveAck", "saveResponse", "ack", "response"))
            {
                return true;
            }

            saveResultElement = default;
            return false;
        }

        private void QueuePacketOwnedMonsterBookOwnershipSaveApply(
            CharacterBuild build,
            int characterId,
            string characterName,
            MonsterBookSnapshot snapshot,
            string source,
            bool showFeedback)
        {
            if (snapshot == null)
            {
                return;
            }

            Dictionary<int, int> cardCountsByMob = BuildMonsterBookOwnedCountsByMob(snapshot);
            int requestId = ReserveMonsterBookOwnershipSaveRequestId();
            byte[] savePayload = BuildMonsterBookOwnershipSaveSyncPayload(
                requestId,
                characterId,
                characterName,
                snapshot.RegisteredCardMobId,
                cardCountsByMob,
                source);
            string dispatchStatus = DispatchMonsterBookOwnershipSaveRequest(savePayload, source);
            _pendingMonsterBookOwnershipSaveRequest = new PendingMonsterBookOwnershipSaveRequest
            {
                Build = build,
                CharacterId = characterId,
                CharacterName = characterName ?? string.Empty,
                RequestId = requestId,
                RegisteredMobId = snapshot.RegisteredCardMobId,
                CardCountsByMob = new Dictionary<int, int>(cardCountsByMob),
                Payload = savePayload,
                SentTick = Environment.TickCount64,
                ResponseDelayMs = MonsterBookOwnershipSaveResponseDelayMs,
                SourceSummary = source ?? string.Empty,
                SyntheticResultQueuedTick = long.MinValue
            };

            if (showFeedback)
            {
                ShowUtilityFeedbackMessage(
                    $"{dispatchStatus} Queued packet-owned ownership-save request #{requestId.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        private void ProcessPendingMonsterBookOwnershipSaveRequest()
        {
            PendingMonsterBookOwnershipSaveRequest request = _pendingMonsterBookOwnershipSaveRequest;
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
            bool fallbackFromNoLiveSession = !hasLiveOfficialSession;
            bool fallbackFromOfficialSessionTimeout = hasLiveOfficialSession
                && elapsedMs >= request.ResponseDelayMs + MonsterBookOwnershipSaveOfficialSessionTimeoutMs;
            if (!request.SyntheticResultQueued
                && (fallbackFromNoLiveSession || fallbackFromOfficialSessionTimeout))
            {
                request.SyntheticResultQueued = true;
                request.SyntheticResultQueuedTick = Environment.TickCount64;
                _localUtilityPacketInbox.EnqueueLocal(
                    LocalUtilityPacketInboxManager.MonsterBookOwnershipSyncPacketType,
                    request.Payload,
                    "monster-book-save");
                return;
            }

            if (request.SyntheticResultQueued)
            {
                long syntheticElapsedMs = request.SyntheticResultQueuedTick > long.MinValue
                    ? Environment.TickCount64 - request.SyntheticResultQueuedTick
                    : elapsedMs;
                if (syntheticElapsedMs < MonsterBookOwnershipSaveSyntheticResultTimeoutMs)
                {
                    return;
                }
            }
            else if (elapsedMs < request.ResponseDelayMs + MonsterBookOwnershipSaveOfficialSessionTimeoutMs)
            {
                return;
            }

            _pendingMonsterBookOwnershipSaveRequest = null;
            ShowUtilityFeedbackMessage(
                $"Monster Book ownership-save request #{request.RequestId.ToString(CultureInfo.InvariantCulture)} timed out while waiting for local utility packet {LocalUtilityPacketInboxManager.MonsterBookOwnershipSyncPacketType.ToString(CultureInfo.InvariantCulture)}.");
        }

        private static bool TryFindNestedPropertyObject(
            JsonElement root,
            int maxDepth,
            out JsonElement match,
            params string[] names)
        {
            if (maxDepth < 0 || root.ValueKind != JsonValueKind.Object)
            {
                match = default;
                return false;
            }

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (root.TryGetProperty(name, out JsonElement direct)
                    && direct.ValueKind == JsonValueKind.Object)
                {
                    match = direct;
                    return true;
                }
            }

            if (maxDepth == 0)
            {
                match = default;
                return false;
            }

            foreach (JsonProperty property in root.EnumerateObject())
            {
                JsonElement value = property.Value;
                if (value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (TryFindNestedPropertyObject(value, maxDepth - 1, out match, names))
                {
                    return true;
                }
            }

            match = default;
            return false;
        }

        private string DispatchMonsterBookOwnershipSaveRequest(byte[] payload, string source)
        {
            string sourceLabel = string.IsNullOrWhiteSpace(source) ? "Monster Book ownership save" : source.Trim();
            byte[] safePayload = payload ?? Array.Empty<byte>();
            string payloadHex = safePayload.Length > 0 ? Convert.ToHexString(safePayload) : "<empty>";
            string bridgeStatus = "Unavailable.";
            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(MonsterBookOwnershipSaveRequestOpcode, safePayload, out bridgeStatus))
            {
                return $"{sourceLabel} emitted opcode {MonsterBookOwnershipSaveRequestOpcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the live local-utility bridge. {bridgeStatus}";
            }

            string outboxStatus = "Unavailable.";
            if (_localUtilityPacketOutbox.TrySendOutboundPacket(MonsterBookOwnershipSaveRequestOpcode, safePayload, out outboxStatus))
            {
                return $"{sourceLabel} emitted opcode {MonsterBookOwnershipSaveRequestOpcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] through the generic local-utility outbox after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            }

            string deferredBridgeStatus = "Official-session bridge deferred delivery is disabled.";
            if (_localUtilityOfficialSessionBridgeEnabled
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(
                    MonsterBookOwnershipSaveRequestOpcode,
                    safePayload,
                    out deferredBridgeStatus))
            {
                return $"{sourceLabel} queued opcode {MonsterBookOwnershipSaveRequestOpcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred official-session injection after immediate delivery was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus}";
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(MonsterBookOwnershipSaveRequestOpcode, safePayload, out string queuedOutboxStatus))
            {
                return $"{sourceLabel} queued opcode {MonsterBookOwnershipSaveRequestOpcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] for deferred generic local-utility outbox delivery after immediate delivery was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus} Deferred outbox: {queuedOutboxStatus}";
            }

            return $"{sourceLabel} kept opcode {MonsterBookOwnershipSaveRequestOpcode.ToString(CultureInfo.InvariantCulture)} [{payloadHex}] simulator-local because neither the live bridge nor the generic outbox nor either deferred queue accepted it. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {deferredBridgeStatus} Deferred outbox: {queuedOutboxStatus}";
        }

        private static byte[] BuildMonsterBookOwnershipSaveSyncPayload(
            int requestId,
            int characterId,
            string characterName,
            int registeredMobId,
            IReadOnlyDictionary<int, int> cardCountsByMob,
            string statusText)
        {
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                ownershipSync = new
                {
                    requestId = requestId > 0 ? requestId : (int?)null,
                    replaceExisting = true,
                    owner = new
                    {
                        characterId = characterId > 0 ? (int?)characterId : null,
                        characterName = string.IsNullOrWhiteSpace(characterName) ? null : characterName.Trim()
                    },
                    registeredMobId = registeredMobId > 0 ? (int?)registeredMobId : null,
                    cardCountsByMob = cardCountsByMob ?? new Dictionary<int, int>(),
                    statusText = string.IsNullOrWhiteSpace(statusText)
                        ? "Synthetic packet-owned Monster Book ownership save apply."
                        : statusText.Trim()
                }
            });
        }

        internal static Dictionary<int, int> BuildMonsterBookOwnedCountsByMob(MonsterBookSnapshot snapshot)
        {
            Dictionary<int, int> counts = new();
            if (snapshot?.Grades == null)
            {
                return counts;
            }

            foreach (MonsterBookGradeSnapshot grade in snapshot.Grades)
            {
                if (grade?.Pages == null)
                {
                    continue;
                }

                foreach (MonsterBookPageSnapshot page in grade.Pages)
                {
                    if (page?.Cards == null)
                    {
                        continue;
                    }

                    foreach (MonsterBookCardSnapshot card in page.Cards)
                    {
                        if (card?.MobId > 0 && card.OwnedCopies > 0)
                        {
                            counts[card.MobId] = Math.Clamp(card.OwnedCopies, 0, 5);
                        }
                    }
                }
            }

            return counts;
        }

        private static bool IsMonsterBookRegistrationResultJsonObject(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return element.TryGetProperty("success", out _)
                || element.TryGetProperty("succeeded", out _)
                || element.TryGetProperty("ok", out _)
                || element.TryGetProperty("accepted", out _)
                || element.TryGetProperty("failure", out _)
                || element.TryGetProperty("failed", out _)
                || element.TryGetProperty("rejected", out _)
                || element.TryGetProperty("requestId", out _)
                || element.TryGetProperty("requestToken", out _)
                || element.TryGetProperty("mobId", out _)
                || element.TryGetProperty("targetMobId", out _)
                || element.TryGetProperty("registered", out _)
                || element.TryGetProperty("isRegistered", out _)
                || element.TryGetProperty("reasonCode", out _)
                || element.TryGetProperty("errorCode", out _);
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
