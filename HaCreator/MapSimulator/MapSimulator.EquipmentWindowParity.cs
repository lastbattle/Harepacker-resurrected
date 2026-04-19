using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Companions;
using HaCreator.MapSimulator.Fields;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int EquipmentChangeResponseDelayMs = 50;
        private const int CharacterEquipmentPacketAuthorityTimeoutMs = 350;
        private const int CompletedCharacterEquipmentAuthorityRetentionMs = CharacterEquipmentPacketAuthorityTimeoutMs * 2;
        private const int MechanicEquipmentPacketAuthorityTimeoutMs = 350;
        private int _nextEquipmentChangeRequestId = 1;
        private int _lastEquipmentExclusiveRequestSentTick = int.MinValue;
        private readonly Dictionary<int, PendingEquipmentChangeEnvelope> _pendingEquipmentChangeRequests = new();
        private readonly Dictionary<int, QueuedCharacterEquipmentPacketResult> _pendingCharacterEquipmentPacketResults = new();
        private readonly Dictionary<int, CompletedCharacterEquipmentAuthorityEnvelope> _completedCharacterEquipmentPacketRequests = new();
        private readonly Dictionary<int, EquipmentChangeResult> _pendingMechanicEquipmentPacketResults = new();

        private sealed class PendingEquipmentChangeEnvelope
        {
            public EquipmentChangeRequest Request { get; init; }
            public int ReadyAtTick { get; init; }
            public bool AwaitingCharacterPacketAuthority { get; set; }
            public int CharacterPacketAuthorityDeadlineAtTick { get; set; }
            public bool AwaitingMechanicPacketAuthority { get; set; }
            public int MechanicPacketAuthorityDeadlineAtTick { get; set; }
            public bool AllowObservedLiveMechanicRecovery { get; set; }
            public IReadOnlyDictionary<MechanicEquipSlot, int> MechanicStateBeforeLiveRecovery { get; set; }
        }

        private sealed class CompletedCharacterEquipmentAuthorityEnvelope
        {
            public EquipmentChangeRequest Request { get; init; }
            public CharacterBuild BuildBeforeLocalAccept { get; init; }
            public IReadOnlyList<InventorySlotData> EquipInventoryBeforeLocalAccept { get; init; }
            public IReadOnlyList<InventorySlotData> CashInventoryBeforeLocalAccept { get; init; }
            public int CompletedAtTick { get; set; }
        }

        private readonly record struct QueuedCharacterEquipmentPacketResult(
            EquipmentChangeResult Result,
            CharacterEquipmentAuthorityResultKind ResultKind,
            CompletedCharacterEquipmentAuthorityEnvelope CompletedLocalAcceptEnvelope);

        internal readonly record struct CharacterAuthoritySlotParts(
            CharacterPart VisiblePart,
            CharacterPart HiddenPart);

        private readonly record struct AuthorityInventoryStateCandidate(
            InventoryType InventoryType,
            int SlotIndex,
            InventorySlotData Slot);

        private EquipmentChangeSubmission SubmitEquipmentChangeRequest(EquipmentChangeRequest request)
        {
            if (request == null)
            {
                return EquipmentChangeSubmission.Reject("Equipment change request is missing.");
            }

            if (request.OwnerKind == EquipmentChangeOwnerKind.None)
            {
                return EquipmentChangeSubmission.Reject("The equipment request owner is missing.");
            }

            if (TryGetAndroidCompanionRestrictionRejectReason(request, out string androidRestrictionRejectReason))
            {
                return EquipmentChangeSubmission.Reject(androidRestrictionRejectReason);
            }

            if (TryGetTamingMobEquipmentRestrictionRejectReason(request, out string tamingMobRestrictionRejectReason))
            {
                return EquipmentChangeSubmission.Reject(tamingMobRestrictionRejectReason);
            }

            if (_pendingEquipmentChangeRequests.Count > 0)
            {
                return EquipmentChangeSubmission.Reject("An equipment change is already pending.");
            }

            request.RequestId = GetNextEquipmentChangeRequestId();
            request.RequestedAtTick = currTickCount;
            _lastEquipmentExclusiveRequestSentTick = request.RequestedAtTick;
            PendingEquipmentChangeEnvelope envelope = new()
            {
                Request = request,
                ReadyAtTick = currTickCount + EquipmentChangeResponseDelayMs
            };
            if (IsCharacterEquipmentRequest(request)
                && TryDispatchCharacterEquipmentAuthorityRequest(request, out _))
            {
                envelope.AwaitingCharacterPacketAuthority = true;
                envelope.CharacterPacketAuthorityDeadlineAtTick = currTickCount + CharacterEquipmentPacketAuthorityTimeoutMs;
            }
            else if (IsMechanicEquipmentRequest(request)
                && TryDispatchMechanicEquipmentAuthorityRequest(request, out MechanicAuthorityTransportOutcome mechanicTransportOutcome))
            {
                envelope.AwaitingMechanicPacketAuthority = true;
                envelope.MechanicPacketAuthorityDeadlineAtTick = currTickCount + MechanicEquipmentPacketAuthorityTimeoutMs;
                if (ShouldAllowObservedLiveMechanicRecovery(request, mechanicTransportOutcome))
                {
                    envelope.AllowObservedLiveMechanicRecovery = true;
                    envelope.MechanicStateBeforeLiveRecovery = CaptureMechanicStateSnapshot(_playerManager?.CompanionEquipment?.Mechanic);
                }
            }

            _pendingEquipmentChangeRequests[request.RequestId] = envelope;

            return EquipmentChangeSubmission.Accept(request.RequestId, request.RequestedAtTick);
        }

        private bool ShouldBlockEquipmentDragStart()
        {
            return EquipmentChangeClientParity.ShouldBlockDragStart(
                currTickCount,
                _lastEquipmentExclusiveRequestSentTick,
                _pendingEquipmentChangeRequests.Count > 0);
        }

        private int GetNextEquipmentChangeRequestId()
        {
            int requestId = _nextEquipmentChangeRequestId++;
            if (requestId <= 0)
            {
                _nextEquipmentChangeRequestId = 2;
                requestId = 1;
            }

            return requestId;
        }

        private int ComputeMechanicEquipmentStateToken(CharacterBuild build)
        {
            MechanicEquipmentController controller = _playerManager?.CompanionEquipment?.Mechanic;
            if (controller == null)
            {
                return 0;
            }

            controller.EnsureDefaults(build);
            return controller.ComputeStateToken();
        }

        private int ComputeCompanionStateTokenForRequest(EquipmentChangeRequest request, CharacterBuild build)
        {
            if (request == null)
            {
                return 0;
            }

            bool touchesMechanic = request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                                   || request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic;
            return touchesMechanic
                ? ComputeMechanicEquipmentStateToken(build)
                : 0;
        }

        private EquipmentChangeResult TryResolveEquipmentChangeRequest(EquipmentChangeResolutionQuery resolutionQuery)
        {
            PruneCompletedCharacterEquipmentPacketRequests(currTickCount);
            if (resolutionQuery == null
                || resolutionQuery.RequestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(resolutionQuery.RequestId, out PendingEquipmentChangeEnvelope pendingEnvelope))
            {
                return null;
            }

            if (_pendingCharacterEquipmentPacketResults.TryGetValue(resolutionQuery.RequestId, out QueuedCharacterEquipmentPacketResult queuedCharacterPacketResult))
            {
                if (ShouldDeferQueuedCharacterPacketResultDrain(currTickCount, pendingEnvelope.ReadyAtTick))
                {
                    return null;
                }

                _pendingCharacterEquipmentPacketResults.Remove(resolutionQuery.RequestId);
                _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);
                if (queuedCharacterPacketResult.ResultKind == CharacterEquipmentAuthorityResultKind.LocalRequestAccept
                    && queuedCharacterPacketResult.CompletedLocalAcceptEnvelope != null)
                {
                    queuedCharacterPacketResult.CompletedLocalAcceptEnvelope.CompletedAtTick = currTickCount;
                    _completedCharacterEquipmentPacketRequests[resolutionQuery.RequestId] = queuedCharacterPacketResult.CompletedLocalAcceptEnvelope;
                }

                return queuedCharacterPacketResult.Result;
            }

            if (_pendingMechanicEquipmentPacketResults.TryGetValue(resolutionQuery.RequestId, out EquipmentChangeResult packetResult))
            {
                if (ShouldDeferQueuedCharacterPacketResultDrain(currTickCount, pendingEnvelope.ReadyAtTick))
                {
                    return null;
                }

                _pendingMechanicEquipmentPacketResults.Remove(resolutionQuery.RequestId);
                _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);
                return packetResult;
            }

            if (pendingEnvelope.AwaitingMechanicPacketAuthority
                && TryQueueObservedLiveMechanicRecoveryResult(pendingEnvelope, out _)
                && _pendingMechanicEquipmentPacketResults.TryGetValue(resolutionQuery.RequestId, out packetResult))
            {
                if (ShouldDeferQueuedCharacterPacketResultDrain(currTickCount, pendingEnvelope.ReadyAtTick))
                {
                    return null;
                }

                _pendingMechanicEquipmentPacketResults.Remove(resolutionQuery.RequestId);
                _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);
                return packetResult;
            }

            if (pendingEnvelope.AwaitingCharacterPacketAuthority
                && unchecked(currTickCount - pendingEnvelope.CharacterPacketAuthorityDeadlineAtTick) < 0)
            {
                return null;
            }

            if (pendingEnvelope.AwaitingMechanicPacketAuthority
                && unchecked(currTickCount - pendingEnvelope.MechanicPacketAuthorityDeadlineAtTick) < 0)
            {
                return null;
            }

            if (unchecked(currTickCount - pendingEnvelope.ReadyAtTick) < 0)
            {
                return null;
            }

            EquipmentChangeRequest request = pendingEnvelope.Request;
            if (request == null)
            {
                _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);
                return EquipmentChangeResult.Reject("Equipment change request is missing.");
            }

            if (EquipmentChangeRequestValidator.TryGetResolutionRejectReason(request, resolutionQuery, out string resolutionRejectReason))
            {
                _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);
                return EquipmentChangeResult.Reject(resolutionRejectReason);
            }

            _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);

            CharacterBuild build = _playerManager?.Player?.Build;
            if (build == null)
            {
                return EquipmentChangeResult.Reject("No live character build is available for this equipment action.");
            }

            int resolvedCompanionStateToken = ComputeCompanionStateTokenForRequest(request, build);
            if (EquipmentChangeRequestValidator.TryGetRequestStateRejectReason(
                    request,
                    build,
                    out string requestStateRejectReason,
                    resolvedCompanionStateToken != 0 ? () => resolvedCompanionStateToken : null))
            {
                return EquipmentChangeResult.Reject(requestStateRejectReason)
                    .WithCompletionMetadata(
                        request.RequestId,
                        request.RequestedAtTick,
                        currTickCount,
                        build.ComputeEquipmentStateToken(),
                        resolvedCompanionStateToken);
            }

            EquipmentChangeResult result = request.Kind switch
            {
                EquipmentChangeRequestKind.InventoryToCharacter => HandleInventoryToCharacterChange(request, build),
                EquipmentChangeRequestKind.CharacterToCharacter => HandleCharacterToCharacterChange(request, build),
                EquipmentChangeRequestKind.CharacterToInventory => HandleCharacterToInventoryChange(request, build),
                EquipmentChangeRequestKind.InventoryToCompanion => HandleInventoryToCompanionChange(request, build),
                EquipmentChangeRequestKind.CompanionToInventory => HandleCompanionToInventoryChange(request, build),
                _ => EquipmentChangeResult.Reject("Unsupported equipment change request.")
            };

            return result.WithCompletionMetadata(
                request.RequestId,
                request.RequestedAtTick,
                currTickCount,
                build.ComputeEquipmentStateToken(),
                resolvedCompanionStateToken);
        }

        private bool IsCharacterEquipmentRequest(EquipmentChangeRequest request)
        {
            return request != null
                && (request.Kind == EquipmentChangeRequestKind.InventoryToCharacter
                    || request.Kind == EquipmentChangeRequestKind.CharacterToCharacter
                    || request.Kind == EquipmentChangeRequestKind.CharacterToInventory);
        }

        private bool TryDispatchCharacterEquipmentAuthorityRequest(EquipmentChangeRequest request, out string status)
        {
            status = "Character equipment authority dispatch is unavailable.";
            if (!IsCharacterEquipmentRequest(request))
            {
                status = "Equipment request does not target the character equipment owner.";
                return false;
            }

            byte[] payload = CharacterEquipmentPacketParity.EncodeAuthorityRequestPayload(request);
            const int opcode = LocalUtilityPacketInboxManager.CharacterEquipStatePacketType;
            MechanicAuthorityTransportOutcome outcome = MechanicAuthorityTransportPlanner.DispatchRequest(
                request.RequestId,
                opcode,
                payload,
                _localUtilityOfficialSessionBridgeEnabled,
                _localUtilityOfficialSessionBridge.TrySendOutboundPacket,
                _localUtilityPacketOutbox.TrySendOutboundPacket,
                _localUtilityOfficialSessionBridge.TryQueueOutboundPacket,
                _localUtilityPacketOutbox.TryQueueOutboundPacket);
            status = outcome.Status;
            return outcome.Accepted;
        }

        private bool IsMechanicEquipmentRequest(EquipmentChangeRequest request)
        {
            return request != null
                && (request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                    || request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic);
        }

        private bool TryDispatchMechanicEquipmentAuthorityRequest(
            EquipmentChangeRequest request,
            out MechanicAuthorityTransportOutcome outcome)
        {
            outcome = new MechanicAuthorityTransportOutcome(
                false,
                MechanicAuthorityTransportRoute.None,
                "Mechanic equipment authority dispatch is unavailable.");
            if (!IsMechanicEquipmentRequest(request))
            {
                outcome = new MechanicAuthorityTransportOutcome(
                    false,
                    MechanicAuthorityTransportRoute.None,
                    "Equipment request does not target the mechanic owner.");
                return false;
            }

            byte[] simulatorPayload = BuildMechanicEquipmentAuthorityRequestPayload(request);
            const int simulatorOpcode = LocalUtilityPacketInboxManager.MechanicEquipStatePacketType;
            int bridgeOpcode = simulatorOpcode;
            byte[] bridgePayload = simulatorPayload;
            if (MechanicEquipmentPacketParity.TryEncodeClientChangeSlotPositionRequest(
                    request,
                    out byte[] clientPayload,
                    out _))
            {
                bridgeOpcode = MechanicEquipmentPacketParity.ClientChangeSlotPositionRequestOpcode;
                bridgePayload = clientPayload;
            }

            outcome = MechanicAuthorityTransportPlanner.DispatchRequest(
                request.RequestId,
                bridgeOpcode,
                bridgePayload,
                simulatorOpcode,
                simulatorPayload,
                _localUtilityOfficialSessionBridgeEnabled,
                _localUtilityOfficialSessionBridge.TrySendOutboundPacket,
                _localUtilityPacketOutbox.TrySendOutboundPacket,
                _localUtilityOfficialSessionBridge.TryQueueOutboundPacket,
                _localUtilityPacketOutbox.TryQueueOutboundPacket);
            return outcome.Accepted;
        }

        private byte[] BuildMechanicEquipmentAuthorityRequestPayload(EquipmentChangeRequest request)
        {
            return MechanicEquipmentPacketParity.EncodeAuthorityRequestPayload(request);
        }

        private static bool ShouldAllowObservedLiveMechanicRecovery(
            EquipmentChangeRequest request,
            MechanicAuthorityTransportOutcome outcome)
        {
            return request != null
                && (request.Kind == EquipmentChangeRequestKind.InventoryToCompanion
                    && request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                    && request.SourceInventoryType == InventoryType.EQUIP
                    && request.TargetMechanicSlot.HasValue
                    || request.Kind == EquipmentChangeRequestKind.CompanionToInventory
                    && request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic
                    && request.SourceMechanicSlot.HasValue)
                && outcome.Accepted
                && outcome.Route == MechanicAuthorityTransportRoute.LiveBridge;
        }

        private bool TryApplyPacketOwnedCharacterEquipPayload(byte[] payload, out string message)
        {
            return TryApplyPacketOwnedCharacterEquipPayload(
                LocalUtilityPacketInboxManager.CharacterEquipStatePacketType,
                payload,
                out message);
        }

        private bool TryApplyPacketOwnedCharacterEquipPayload(int packetType, byte[] payload, out string message)
        {
            if (!CharacterEquipmentPacketParity.TryDecodePayload(payload, out CharacterEquipmentAuthorityPayload decodedPayload, out message))
            {
                return false;
            }

            decodedPayload = decodedPayload with
            {
                AuthorityPacketType = packetType,
                HasResultRequestContext = true
            };

            if (!IsCharacterEquipmentAuthorityPacketType(packetType))
            {
                message = $"Character equipment authority payload arrived on unsupported packet owner {packetType}.";
                return false;
            }

            return decodedPayload.Mode switch
            {
                CharacterEquipmentAuthorityPayloadMode.AuthorityRequest => TryResolvePacketOwnedCharacterAuthorityRequest(decodedPayload, out message),
                CharacterEquipmentAuthorityPayloadMode.AuthorityResult => TryQueuePacketOwnedCharacterAuthorityResult(decodedPayload, out message),
                _ => FailPacketOwnedCharacterAuthorityPayload("Unsupported character equipment authority payload mode.", out message)
            };
        }

        private bool TryResolvePacketOwnedCharacterAuthorityRequest(
            CharacterEquipmentAuthorityPayload payload,
            out string message)
        {
            message = null;
            if (payload.Mode != CharacterEquipmentAuthorityPayloadMode.AuthorityRequest)
            {
                message = "Character equipment authority payload is not a request.";
                return false;
            }

            if (!TryValidateCharacterEquipmentAuthorityPacketContext(payload, out message))
            {
                return false;
            }

            if (payload.RequestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(payload.RequestId, out PendingEquipmentChangeEnvelope pendingEnvelope)
                || pendingEnvelope?.Request == null)
            {
                message = $"Character equipment authority request did not match a pending request id ({payload.RequestId}).";
                return false;
            }

            EquipmentChangeRequest request = pendingEnvelope.Request;
            if (!IsCharacterEquipmentRequest(request))
            {
                message = $"Pending request {payload.RequestId} is not owned by a character equipment pane.";
                return false;
            }

            if (request.RequestedAtTick != payload.RequestedAtTick)
            {
                message = $"Character equipment authority request for {payload.RequestId} did not match the pending request timestamp.";
                return false;
            }

            if (request.Kind != payload.RequestKind
                || request.OwnerKind != payload.OwnerKind
                || request.OwnerSessionId != payload.OwnerSessionId
                || request.ExpectedCharacterId != payload.ExpectedCharacterId
                || request.ExpectedBuildStateToken != payload.ExpectedBuildStateToken
                || request.ItemId != payload.ItemId
                || request.SourceInventoryType != payload.SourceInventoryType
                || request.SourceInventoryIndex != payload.SourceInventoryIndex
                || request.SourceEquipSlot != payload.SourceEquipSlot
                || request.TargetEquipSlot != payload.TargetEquipSlot)
            {
                message = $"Character equipment authority request {payload.RequestId} did not match the pending request state.";
                return false;
            }

            return TryQueuePacketOwnedCharacterAuthorityResult(
                new CharacterEquipmentAuthorityPayload(
                    CharacterEquipmentAuthorityPayloadMode.AuthorityResult,
                    payload.RequestId,
                    payload.RequestedAtTick,
                    OwnerKind: request.OwnerKind,
                    OwnerSessionId: request.OwnerSessionId,
                    ExpectedCharacterId: request.ExpectedCharacterId,
                    ResultKind: CharacterEquipmentAuthorityResultKind.LocalRequestAccept,
                    HasOwnerSessionContext: true),
                out message);
        }

        private bool TryQueuePacketOwnedCharacterAuthorityResult(
            CharacterEquipmentAuthorityPayload payload,
            out string message)
        {
            message = null;
            PruneCompletedCharacterEquipmentPacketRequests(currTickCount);
            if (payload.Mode != CharacterEquipmentAuthorityPayloadMode.AuthorityResult)
            {
                message = "Character equipment authority payload is not a result.";
                return false;
            }

            if (!TryValidateCharacterEquipmentAuthorityPacketContext(payload, out message))
            {
                return false;
            }

            if (payload.RequestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(payload.RequestId, out PendingEquipmentChangeEnvelope pendingEnvelope)
                || pendingEnvelope?.Request == null)
            {
                return TryApplyLateCompletedCharacterPacketAuthorityResult(payload, out message);
            }

            EquipmentChangeRequest request = pendingEnvelope.Request;
            if (!IsCharacterEquipmentRequest(request))
            {
                message = $"Pending request {payload.RequestId} is not owned by a character equipment pane.";
                return false;
            }

            if (request.RequestedAtTick != payload.RequestedAtTick)
            {
                message = $"Character equipment authority result for request {payload.RequestId} did not match the pending request timestamp.";
                return false;
            }

            if (!TryValidateCharacterEquipmentAuthorityResultOwnerSession(
                    request,
                    payload,
                    out message))
            {
                return false;
            }

            CharacterBuild build = _playerManager?.Player?.Build;
            if (build == null)
            {
                message = "Character equipment runtime is unavailable.";
                return false;
            }

            if (payload.ResultKind == CharacterEquipmentAuthorityResultKind.Reject)
            {
                EquipmentChangeResult rejectResult = EquipmentChangeResult.Reject(
                    string.IsNullOrWhiteSpace(payload.RejectReason)
                        ? "The character equipment request was rejected by packet authority."
                        : payload.RejectReason)
                    .WithCompletionMetadata(
                        payload.RequestId,
                        payload.RequestedAtTick,
                        currTickCount,
                        payload.ResolvedBuildStateToken != 0 ? payload.ResolvedBuildStateToken : build.ComputeEquipmentStateToken());
                return TryQueueCharacterEquipmentPacketResult(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    rejectResult,
                    CharacterEquipmentAuthorityResultKind.Reject,
                    null,
                    out message);
            }

            if (EquipmentChangeRequestValidator.TryGetRequestStateRejectReason(
                    request,
                    build,
                    out string requestStateRejectReason))
            {
                EquipmentChangeResult staleReject = EquipmentChangeResult.Reject(requestStateRejectReason)
                    .WithCompletionMetadata(
                        payload.RequestId,
                        payload.RequestedAtTick,
                        currTickCount,
                        build.ComputeEquipmentStateToken());
                return TryQueueCharacterEquipmentPacketResult(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    staleReject,
                    CharacterEquipmentAuthorityResultKind.Reject,
                    null,
                    out message);
            }

            if (!TryCreatePacketOwnedCharacterAuthorityResult(
                    request,
                    build,
                    payload,
                    out EquipmentChangeResult acceptedResult,
                    out CompletedCharacterEquipmentAuthorityEnvelope completedLocalAcceptEnvelope,
                    out string rejectReason))
            {
                EquipmentChangeResult rejectResult = EquipmentChangeResult.Reject(rejectReason)
                    .WithCompletionMetadata(
                        payload.RequestId,
                        payload.RequestedAtTick,
                        currTickCount,
                        payload.ResolvedBuildStateToken != 0 ? payload.ResolvedBuildStateToken : build.ComputeEquipmentStateToken());
                return TryQueueCharacterEquipmentPacketResult(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    rejectResult,
                    payload.ResultKind == CharacterEquipmentAuthorityResultKind.Reject
                        ? CharacterEquipmentAuthorityResultKind.Reject
                        : CharacterEquipmentAuthorityResultKind.AuthoritativeStateAccept,
                    null,
                    out message);
            }

            return TryQueueCharacterEquipmentPacketResult(
                payload.RequestId,
                payload.RequestedAtTick,
                acceptedResult,
                payload.ResultKind,
                completedLocalAcceptEnvelope,
                out message);
        }

        private bool TryCreatePacketOwnedCharacterAuthorityResult(
            EquipmentChangeRequest request,
            CharacterBuild build,
            CharacterEquipmentAuthorityPayload payload,
            out EquipmentChangeResult result,
            out CompletedCharacterEquipmentAuthorityEnvelope completedLocalAcceptEnvelope,
            out string rejectReason)
        {
            result = null;
            completedLocalAcceptEnvelope = null;
            rejectReason = null;
            if (payload.ResultKind == CharacterEquipmentAuthorityResultKind.LocalRequestAccept)
            {
                completedLocalAcceptEnvelope = CaptureCompletedCharacterEquipmentAuthorityEnvelope(request, build);
                result = request.Kind switch
                {
                    EquipmentChangeRequestKind.InventoryToCharacter => HandleInventoryToCharacterChange(request, build),
                    EquipmentChangeRequestKind.CharacterToCharacter => HandleCharacterToCharacterChange(request, build),
                    EquipmentChangeRequestKind.CharacterToInventory => HandleCharacterToInventoryChange(request, build),
                    _ => EquipmentChangeResult.Reject("Unsupported character equipment authority request kind.")
                };

                if (!result.Accepted)
                {
                    completedLocalAcceptEnvelope = null;
                    rejectReason = result.RejectReason;
                    return false;
                }

                result = result.WithCompletionMetadata(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    currTickCount,
                    payload.ResolvedBuildStateToken != 0 ? payload.ResolvedBuildStateToken : build.ComputeEquipmentStateToken());
                return true;
            }

            return request.Kind switch
            {
                EquipmentChangeRequestKind.InventoryToCharacter => TryCreatePacketOwnedInventoryToCharacterAuthorityResult(
                    request,
                    build,
                    payload,
                    out result,
                    out rejectReason),
                EquipmentChangeRequestKind.CharacterToCharacter => TryCreatePacketOwnedCharacterToCharacterAuthorityResult(
                    request,
                    build,
                    payload,
                    out result,
                    out rejectReason),
                EquipmentChangeRequestKind.CharacterToInventory => TryCreatePacketOwnedCharacterToInventoryAuthorityResult(
                    request,
                    build,
                    payload,
                    out result,
                    out rejectReason),
                _ => FailPacketOwnedCharacterAuthorityResult("Unsupported character equipment authority request kind.", out result, out rejectReason)
            };
        }

        private bool TryCreatePacketOwnedInventoryToCharacterAuthorityResult(
            EquipmentChangeRequest request,
            CharacterBuild build,
            CharacterEquipmentAuthorityPayload payload,
            out EquipmentChangeResult result,
            out string rejectReason)
        {
            result = null;
            rejectReason = null;
            if (!request.TargetEquipSlot.HasValue)
            {
                rejectReason = "Packet authority result did not target a character equipment slot.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                rejectReason = "Inventory runtime is unavailable.";
                return false;
            }

            IReadOnlyList<InventorySlotData> liveSlots = inventoryWindow.GetSlots(request.SourceInventoryType);
            if (request.SourceInventoryIndex < 0 || request.SourceInventoryIndex >= liveSlots.Count)
            {
                rejectReason = "The source inventory slot changed before the equipment request completed.";
                return false;
            }

            InventorySlotData liveSlot = liveSlots[request.SourceInventoryIndex];
            if (EquipmentChangeRequestValidator.TryGetInventorySourceRejectReason(request, liveSlot, out rejectReason))
            {
                return false;
            }

            EquipSlot targetSlot = request.TargetEquipSlot.Value;
            HashSet<EquipSlot> affectedSlots = new() { targetSlot };
            Dictionary<EquipSlot, CharacterAuthoritySlotParts> beforeParts = CaptureCharacterAuthoritySlotParts(build, affectedSlots);
            Dictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> beforeState = CaptureCharacterAuthoritySlotStateSnapshot(build);
            if (!TryValidatePacketOwnedCharacterAuthorityScope(request, beforeState, payload, out rejectReason))
            {
                return false;
            }

            if (!CharacterEquipmentPacketParity.TryApplyAuthoritativeState(build, payload.AuthoritySlotStates, out rejectReason))
            {
                return false;
            }

            IReadOnlyList<CharacterPart> displacedParts = ResolvePacketOwnedAuthorityDisplacedParts(beforeParts, payload, targetSlot);
            result = EquipmentChangeResult.Accept(
                    displacedParts: displacedParts,
                    authorityInventorySlotStates: payload.AuthorityInventorySlotStates)
                .WithCompletionMetadata(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    currTickCount,
                    payload.ResolvedBuildStateToken != 0 ? payload.ResolvedBuildStateToken : build.ComputeEquipmentStateToken());
            return true;
        }

        private bool TryCreatePacketOwnedCharacterToCharacterAuthorityResult(
            EquipmentChangeRequest request,
            CharacterBuild build,
            CharacterEquipmentAuthorityPayload payload,
            out EquipmentChangeResult result,
            out string rejectReason)
        {
            result = null;
            rejectReason = null;
            if (!request.SourceEquipSlot.HasValue || !request.TargetEquipSlot.HasValue)
            {
                rejectReason = "Packet authority result did not identify the moved character equipment slots.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                rejectReason = "Inventory runtime is unavailable.";
                return false;
            }

            CharacterPart liveSourcePart = EquipSlotStateResolver.ResolveDisplayedPart(build, request.SourceEquipSlot.Value);
            if (liveSourcePart == null || liveSourcePart.ItemId != request.ItemId)
            {
                rejectReason = "The equipped item changed before the move request completed.";
                return false;
            }

            HashSet<EquipSlot> affectedSlots = new() { request.SourceEquipSlot.Value, request.TargetEquipSlot.Value };
            Dictionary<EquipSlot, CharacterAuthoritySlotParts> beforeParts = CaptureCharacterAuthoritySlotParts(build, affectedSlots);
            Dictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> beforeState = CaptureCharacterAuthoritySlotStateSnapshot(build);
            if (!TryValidatePacketOwnedCharacterAuthorityScope(request, beforeState, payload, out rejectReason))
            {
                return false;
            }

            IReadOnlyList<CharacterPart> displacedParts = ResolvePacketOwnedAuthorityDisplacedParts(beforeParts, payload, affectedSlots);
            if (!CanAcceptResolvedInventoryParts(displacedParts, inventoryWindow, out rejectReason))
            {
                return false;
            }

            if (!CharacterEquipmentPacketParity.TryApplyAuthoritativeState(build, payload.AuthoritySlotStates, out rejectReason))
            {
                return false;
            }

            result = EquipmentChangeResult.Accept(
                    displacedParts: displacedParts,
                    authorityInventorySlotStates: payload.AuthorityInventorySlotStates)
                .WithCompletionMetadata(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    currTickCount,
                    payload.ResolvedBuildStateToken != 0 ? payload.ResolvedBuildStateToken : build.ComputeEquipmentStateToken());
            return true;
        }

        private bool TryCreatePacketOwnedCharacterToInventoryAuthorityResult(
            EquipmentChangeRequest request,
            CharacterBuild build,
            CharacterEquipmentAuthorityPayload payload,
            out EquipmentChangeResult result,
            out string rejectReason)
        {
            result = null;
            rejectReason = null;
            if (!request.SourceEquipSlot.HasValue)
            {
                rejectReason = "Packet authority result did not identify the removed character equipment slot.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                rejectReason = "Inventory runtime is unavailable.";
                return false;
            }

            CharacterPart liveSourcePart = EquipSlotStateResolver.ResolveDisplayedPart(build, request.SourceEquipSlot.Value);
            if (liveSourcePart == null || liveSourcePart.ItemId != request.ItemId)
            {
                rejectReason = "The equipped item changed before the unequip request completed.";
                return false;
            }

            InventoryType inventoryType = EquipmentChangeClientParity.ResolveCharacterEquipmentInventoryType(liveSourcePart);
            if (!inventoryWindow.CanAcceptItem(inventoryType, liveSourcePart.ItemId, 1, maxStackSize: 1))
            {
                string inventoryLabel = inventoryType == InventoryType.CASH ? "cash" : "equipment";
                rejectReason = $"There is no free {inventoryLabel} inventory slot for this item.";
                return false;
            }

            HashSet<EquipSlot> affectedSlots = new() { request.SourceEquipSlot.Value };
            Dictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> beforeState = CaptureCharacterAuthoritySlotStateSnapshot(build);
            if (!TryValidatePacketOwnedCharacterAuthorityScope(request, beforeState, payload, out rejectReason))
            {
                return false;
            }

            if (!CharacterEquipmentPacketParity.TryApplyAuthoritativeState(build, payload.AuthoritySlotStates, out rejectReason))
            {
                return false;
            }

            result = EquipmentChangeResult.Accept(
                    returnedPart: liveSourcePart.Clone(),
                    authorityInventorySlotStates: payload.AuthorityInventorySlotStates)
                .WithCompletionMetadata(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    currTickCount,
                    payload.ResolvedBuildStateToken != 0 ? payload.ResolvedBuildStateToken : build.ComputeEquipmentStateToken());
            return true;
        }

        private bool TryQueueCharacterEquipmentPacketResult(
            int requestId,
            int requestedAtTick,
            EquipmentChangeResult result,
            CharacterEquipmentAuthorityResultKind resultKind,
            CompletedCharacterEquipmentAuthorityEnvelope completedLocalAcceptEnvelope,
            out string message)
        {
            message = null;
            if (requestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(requestId, out PendingEquipmentChangeEnvelope pendingEnvelope)
                || pendingEnvelope?.Request == null)
            {
                message = $"Character equipment packet result did not match a pending request id ({requestId}).";
                return false;
            }

            EquipmentChangeRequest request = pendingEnvelope.Request;
            if (!IsCharacterEquipmentRequest(request))
            {
                message = $"Pending request {requestId} is not owned by a character equipment pane.";
                return false;
            }

            if (request.RequestedAtTick != requestedAtTick)
            {
                message = $"Character equipment packet result for request {requestId} did not match the pending request timestamp.";
                return false;
            }

            if (_pendingCharacterEquipmentPacketResults.TryGetValue(requestId, out QueuedCharacterEquipmentPacketResult existingResult)
                && !ShouldReplaceQueuedCharacterPacketResult(existingResult.ResultKind, resultKind))
            {
                pendingEnvelope.AwaitingCharacterPacketAuthority = false;
                pendingEnvelope.CharacterPacketAuthorityDeadlineAtTick = currTickCount;
                message = $"Ignored provisional packet-authored character equipment result for request {requestId} because an explicit result is already queued.";
                return true;
            }

            _pendingCharacterEquipmentPacketResults[requestId] = new QueuedCharacterEquipmentPacketResult(
                result,
                resultKind,
                completedLocalAcceptEnvelope);
            pendingEnvelope.AwaitingCharacterPacketAuthority = false;
            pendingEnvelope.CharacterPacketAuthorityDeadlineAtTick = currTickCount;
            message = result.Accepted
                ? $"Queued packet-authored character equipment result for request {requestId}."
                : $"Queued packet-authored character equipment rejection for request {requestId}.";
            return true;
        }

        private bool TryApplyLateCompletedCharacterPacketAuthorityResult(
            CharacterEquipmentAuthorityPayload payload,
            out string message)
        {
            message = null;
            if (payload.RequestId <= 0
                || !_completedCharacterEquipmentPacketRequests.TryGetValue(payload.RequestId, out CompletedCharacterEquipmentAuthorityEnvelope completedEnvelope)
                || completedEnvelope?.Request == null)
            {
                message = $"Character equipment authority result did not match a pending request id ({payload.RequestId}).";
                return false;
            }

            if (completedEnvelope.Request.RequestedAtTick != payload.RequestedAtTick)
            {
                message = $"Character equipment authority result for request {payload.RequestId} did not match the completed request timestamp.";
                return false;
            }

            if (!TryValidateCharacterEquipmentAuthorityResultOwnerSession(
                    completedEnvelope.Request,
                    payload,
                    out message))
            {
                return false;
            }

            if (!ShouldRetainCompletedCharacterPacketRequest(
                    currTickCount,
                    completedEnvelope.CompletedAtTick,
                    CompletedCharacterEquipmentAuthorityRetentionMs))
            {
                _completedCharacterEquipmentPacketRequests.Remove(payload.RequestId);
                message = $"Character equipment authority result for request {payload.RequestId} arrived after the late-result reconciliation window closed.";
                return false;
            }

            if (payload.ResultKind == CharacterEquipmentAuthorityResultKind.LocalRequestAccept)
            {
                message = $"Ignored provisional packet-authored character equipment result for completed request {payload.RequestId}.";
                return true;
            }

            if (_playerManager?.Player?.Build is not CharacterBuild build)
            {
                message = "Character equipment runtime is unavailable.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                message = "Inventory runtime is unavailable.";
                return false;
            }

            CharacterBuild liveBuildSnapshot = build.Clone();
            IReadOnlyList<InventorySlotData> liveEquipInventory = CaptureInventorySnapshot(inventoryWindow, InventoryType.EQUIP);
            IReadOnlyList<InventorySlotData> liveCashInventory = CaptureInventorySnapshot(inventoryWindow, InventoryType.CASH);

            RestoreCompletedCharacterEquipmentAuthorityBaseline(build, inventoryWindow, completedEnvelope);
            if (payload.ResultKind == CharacterEquipmentAuthorityResultKind.Reject)
            {
                _completedCharacterEquipmentPacketRequests.Remove(payload.RequestId);
                message = $"Reconciled late packet-authored character equipment rejection for request {payload.RequestId}.";
                return true;
            }

            if (!TryCreatePacketOwnedCharacterAuthorityResult(
                    completedEnvelope.Request,
                    build,
                    payload,
                    out EquipmentChangeResult authoritativeResult,
                    out _,
                    out string rejectReason))
            {
                RestoreCharacterEquipmentState(build, liveBuildSnapshot);
                inventoryWindow.ReplaceInventory(InventoryType.EQUIP, liveEquipInventory);
                inventoryWindow.ReplaceInventory(InventoryType.CASH, liveCashInventory);
                message = rejectReason;
                return false;
            }

            if (!TryApplyResolvedCharacterEquipmentResultToInventory(
                    completedEnvelope.Request,
                    authoritativeResult,
                    inventoryWindow,
                    out string inventoryRejectReason))
            {
                RestoreCharacterEquipmentState(build, liveBuildSnapshot);
                inventoryWindow.ReplaceInventory(InventoryType.EQUIP, liveEquipInventory);
                inventoryWindow.ReplaceInventory(InventoryType.CASH, liveCashInventory);
                message = inventoryRejectReason;
                return false;
            }

            if (ShouldRefreshCompletedCharacterPacketRequestRetention(payload.ResultKind))
            {
                completedEnvelope.CompletedAtTick = currTickCount;
            }

            if (ShouldRemoveCompletedCharacterPacketRequestAfterLateResult(payload.ResultKind))
            {
                _completedCharacterEquipmentPacketRequests.Remove(payload.RequestId);
            }

            message = $"Reconciled late packet-authored character equipment result for request {payload.RequestId}.";
            return true;
        }

        internal static bool ShouldDeferQueuedCharacterPacketResultDrain(int currentTick, int readyAtTick)
        {
            return unchecked(currentTick - readyAtTick) < 0;
        }

        internal static bool ShouldRetainCompletedCharacterPacketRequest(int currentTick, int completedAtTick, int retentionMs)
        {
            return completedAtTick > 0
                   && retentionMs > 0
                   && unchecked(currentTick - completedAtTick) <= retentionMs;
        }

        internal static bool ShouldRefreshCompletedCharacterPacketRequestRetention(CharacterEquipmentAuthorityResultKind resultKind)
        {
            return resultKind == CharacterEquipmentAuthorityResultKind.AuthoritativeStateAccept;
        }

        internal static bool ShouldRemoveCompletedCharacterPacketRequestAfterLateResult(CharacterEquipmentAuthorityResultKind resultKind)
        {
            return resultKind == CharacterEquipmentAuthorityResultKind.Reject;
        }

        internal static bool ShouldReplaceQueuedCharacterPacketResult(
            CharacterEquipmentAuthorityResultKind existingKind,
            CharacterEquipmentAuthorityResultKind incomingKind)
        {
            return existingKind == CharacterEquipmentAuthorityResultKind.LocalRequestAccept
                   || incomingKind != CharacterEquipmentAuthorityResultKind.LocalRequestAccept;
        }

        internal static bool IsCharacterEquipmentAuthorityPacketType(int packetType)
        {
            return packetType == LocalUtilityPacketInboxManager.CharacterEquipStatePacketType
                   || packetType == CharacterEquipmentPacketParity.ClientInventoryOperationPacketType;
        }

        internal static bool TryValidateCharacterEquipmentAuthorityPacketContext(
            CharacterEquipmentAuthorityPayload payload,
            out string rejectReason)
        {
            if (!payload.HasResultRequestContext)
            {
                rejectReason = null;
                return true;
            }

            if (IsCharacterEquipmentAuthorityPacketType(payload.AuthorityPacketType))
            {
                rejectReason = null;
                return true;
            }

            rejectReason = $"Character equipment authority payload arrived on unsupported packet owner {payload.AuthorityPacketType}.";
            return false;
        }

        internal static bool TryValidateCharacterEquipmentAuthorityResultOwnerSession(
            EquipmentChangeRequest request,
            CharacterEquipmentAuthorityPayload payload,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Character equipment request is missing.";
                return false;
            }

            if (!payload.HasOwnerSessionContext)
            {
                return true;
            }

            if (request.OwnerKind != payload.OwnerKind)
            {
                rejectReason = $"Character equipment authority result for request {payload.RequestId} did not match the equipment window owner.";
                return false;
            }

            if (request.OwnerSessionId != payload.OwnerSessionId)
            {
                rejectReason = $"Character equipment authority result for request {payload.RequestId} did not match the equipment window session.";
                return false;
            }

            if (request.ExpectedCharacterId != payload.ExpectedCharacterId)
            {
                rejectReason = $"Character equipment authority result for request {payload.RequestId} did not match the active character.";
                return false;
            }

            return true;
        }

        internal static bool TryValidatePacketOwnedCharacterAuthorityScope(
            EquipmentChangeRequest request,
            IReadOnlyDictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> beforeState,
            CharacterEquipmentAuthorityPayload payload,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Character equipment request is missing.";
                return false;
            }

            if (!CharacterEquipmentPacketParity.HasExplicitAuthorityState(payload))
            {
                rejectReason = "Character equipment authority result did not include a usable character state.";
                return false;
            }

            HashSet<EquipSlot> affectedSlots = request.Kind switch
            {
                EquipmentChangeRequestKind.InventoryToCharacter when request.TargetEquipSlot.HasValue
                    => new() { request.TargetEquipSlot.Value },
                EquipmentChangeRequestKind.CharacterToCharacter when request.SourceEquipSlot.HasValue && request.TargetEquipSlot.HasValue
                    => new() { request.SourceEquipSlot.Value, request.TargetEquipSlot.Value },
                EquipmentChangeRequestKind.CharacterToInventory when request.SourceEquipSlot.HasValue
                    => new() { request.SourceEquipSlot.Value },
                _ => null
            };
            if (affectedSlots == null || affectedSlots.Count == 0)
            {
                rejectReason = "Character equipment authority result did not match the request scope.";
                return false;
            }

            Dictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> finalStates = new();
            for (int i = 0; i < payload.AuthoritySlotStates.Count; i++)
            {
                CharacterEquipmentAuthoritySlotState state = payload.AuthoritySlotStates[i];
                if (!affectedSlots.Contains(state.Slot))
                {
                    if (!TryGetAuthoritySlotState(beforeState, state.Slot, out CharacterEquipmentAuthoritySlotState beforeSlotState)
                        || beforeSlotState.VisibleItemId != state.VisibleItemId
                        || beforeSlotState.HiddenItemId != state.HiddenItemId)
                    {
                        rejectReason = "Packet-authored character authority changed slots outside the active request.";
                        return false;
                    }

                    continue;
                }

                if (finalStates.ContainsKey(state.Slot))
                {
                    rejectReason = "Packet-authored character authority returned duplicate slot states.";
                    return false;
                }

                finalStates[state.Slot] = state;
            }

            foreach (EquipSlot slot in affectedSlots)
            {
                if (!finalStates.ContainsKey(slot))
                {
                    rejectReason = "Packet-authored character authority did not return the full slot state for the active request.";
                    return false;
                }
            }

            return request.Kind switch
            {
                EquipmentChangeRequestKind.InventoryToCharacter => ValidatePacketOwnedInventoryToCharacterScope(
                    request,
                    finalStates[request.TargetEquipSlot!.Value],
                    out rejectReason),
                EquipmentChangeRequestKind.CharacterToCharacter => ValidatePacketOwnedCharacterToCharacterScope(
                    request,
                    finalStates[request.SourceEquipSlot!.Value],
                    finalStates[request.TargetEquipSlot!.Value],
                    out rejectReason),
                EquipmentChangeRequestKind.CharacterToInventory => ValidatePacketOwnedCharacterToInventoryScope(
                    request,
                    finalStates[request.SourceEquipSlot!.Value],
                    out rejectReason),
                _ => FailPacketOwnedCharacterAuthorityResult("Unsupported character equipment authority request kind.", out rejectReason)
            };
        }

        private static bool ValidatePacketOwnedInventoryToCharacterScope(
            EquipmentChangeRequest request,
            CharacterEquipmentAuthoritySlotState targetState,
            out string rejectReason)
        {
            if (!SlotStateContainsItem(targetState, request.ItemId))
            {
                rejectReason = "Packet-authored character authority did not place the requested item into the target slot.";
                return false;
            }

            rejectReason = null;
            return true;
        }

        private static bool ValidatePacketOwnedCharacterToCharacterScope(
            EquipmentChangeRequest request,
            CharacterEquipmentAuthoritySlotState sourceState,
            CharacterEquipmentAuthoritySlotState targetState,
            out string rejectReason)
        {
            if (SlotStateContainsItem(sourceState, request.ItemId))
            {
                rejectReason = "Packet-authored character authority left the moved item in the source slot.";
                return false;
            }

            if (!SlotStateContainsItem(targetState, request.ItemId))
            {
                rejectReason = "Packet-authored character authority did not place the moved item into the target slot.";
                return false;
            }

            rejectReason = null;
            return true;
        }

        private static bool ValidatePacketOwnedCharacterToInventoryScope(
            EquipmentChangeRequest request,
            CharacterEquipmentAuthoritySlotState sourceState,
            out string rejectReason)
        {
            if (SlotStateContainsItem(sourceState, request.ItemId))
            {
                rejectReason = "Packet-authored character authority did not clear the requested equipment slot.";
                return false;
            }

            rejectReason = null;
            return true;
        }

        private static bool SlotStateContainsItem(CharacterEquipmentAuthoritySlotState state, int itemId)
        {
            return itemId > 0 && (state.VisibleItemId == itemId || state.HiddenItemId == itemId);
        }

        private static Dictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> CaptureCharacterAuthoritySlotStateSnapshot(CharacterBuild build)
        {
            List<EquipSlot> slots = new();
            foreach (EquipSlot slot in Enum.GetValues<EquipSlot>())
            {
                if (slot != EquipSlot.None)
                {
                    slots.Add(slot);
                }
            }

            return CharacterEquipmentPacketParity.CaptureAuthoritySlotStates(build, slots);
        }

        private static bool TryGetAuthoritySlotState(
            IReadOnlyDictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> beforeState,
            EquipSlot slot,
            out CharacterEquipmentAuthoritySlotState slotState)
        {
            if (beforeState != null && beforeState.TryGetValue(slot, out slotState))
            {
                return true;
            }

            slotState = new CharacterEquipmentAuthoritySlotState(slot, 0, 0);
            return Enum.IsDefined(typeof(EquipSlot), slot) && slot != EquipSlot.None;
        }

        private static Dictionary<EquipSlot, CharacterAuthoritySlotParts> CaptureCharacterAuthoritySlotParts(
            CharacterBuild build,
            IEnumerable<EquipSlot> slots)
        {
            Dictionary<EquipSlot, CharacterAuthoritySlotParts> snapshot = new();
            if (build == null || slots == null)
            {
                return snapshot;
            }

            foreach (EquipSlot slot in slots)
            {
                build.Equipment.TryGetValue(slot, out CharacterPart visiblePart);
                build.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart);
                snapshot[slot] = new CharacterAuthoritySlotParts(visiblePart?.Clone(), hiddenPart?.Clone());
            }

            return snapshot;
        }

        private static IReadOnlyList<CharacterPart> ResolvePacketOwnedAuthorityDisplacedParts(
            IReadOnlyDictionary<EquipSlot, CharacterAuthoritySlotParts> beforeParts,
            CharacterEquipmentAuthorityPayload payload,
            EquipSlot slot)
        {
            return ResolvePacketOwnedAuthorityDisplacedParts(
                beforeParts,
                payload,
                new[] { slot });
        }

        internal static IReadOnlyList<CharacterPart> ResolvePacketOwnedAuthorityDisplacedParts(
            IReadOnlyDictionary<EquipSlot, CharacterAuthoritySlotParts> beforeParts,
            CharacterEquipmentAuthorityPayload payload,
            IEnumerable<EquipSlot> slots)
        {
            if (beforeParts == null
                || payload.AuthoritySlotStates == null
                || slots == null)
            {
                return Array.Empty<CharacterPart>();
            }

            HashSet<EquipSlot> requestedSlots = new();
            foreach (EquipSlot slot in slots)
            {
                if (slot != EquipSlot.None)
                {
                    requestedSlots.Add(slot);
                }
            }

            if (requestedSlots.Count == 0)
            {
                return Array.Empty<CharacterPart>();
            }

            List<int> retainedItemIds = new();
            for (int i = 0; i < payload.AuthoritySlotStates.Count; i++)
            {
                CharacterEquipmentAuthoritySlotState state = payload.AuthoritySlotStates[i];
                if (!requestedSlots.Contains(state.Slot))
                {
                    continue;
                }

                if (state.VisibleItemId > 0)
                {
                    retainedItemIds.Add(state.VisibleItemId);
                }

                if (state.HiddenItemId > 0)
                {
                    retainedItemIds.Add(state.HiddenItemId);
                }
            }

            List<CharacterPart> displacedParts = new();
            foreach (EquipSlot slot in requestedSlots)
            {
                if (!beforeParts.TryGetValue(slot, out CharacterAuthoritySlotParts parts))
                {
                    continue;
                }

                AddDisplacedPart(parts.VisiblePart, retainedItemIds, displacedParts);
                AddDisplacedPart(parts.HiddenPart, retainedItemIds, displacedParts);
            }

            return displacedParts.Count == 0
                ? Array.Empty<CharacterPart>()
                : displacedParts.AsReadOnly();
        }

        private static void AddDisplacedPart(CharacterPart candidate, List<int> retainedItemIds, List<CharacterPart> displacedParts)
        {
            if (candidate == null)
            {
                return;
            }

            int retainedIndex = retainedItemIds.IndexOf(candidate.ItemId);
            if (retainedIndex >= 0)
            {
                retainedItemIds.RemoveAt(retainedIndex);
                return;
            }

            displacedParts.Add(candidate.Clone());
        }

        private void PruneCompletedCharacterEquipmentPacketRequests(int currentTick)
        {
            if (_completedCharacterEquipmentPacketRequests.Count == 0)
            {
                return;
            }

            List<int> expiredRequestIds = null;
            foreach (KeyValuePair<int, CompletedCharacterEquipmentAuthorityEnvelope> entry in _completedCharacterEquipmentPacketRequests)
            {
                if (entry.Value == null
                    || !ShouldRetainCompletedCharacterPacketRequest(
                        currentTick,
                        entry.Value.CompletedAtTick,
                        CompletedCharacterEquipmentAuthorityRetentionMs))
                {
                    expiredRequestIds ??= new List<int>();
                    expiredRequestIds.Add(entry.Key);
                }
            }

            if (expiredRequestIds == null)
            {
                return;
            }

            for (int i = 0; i < expiredRequestIds.Count; i++)
            {
                _completedCharacterEquipmentPacketRequests.Remove(expiredRequestIds[i]);
            }
        }

        private CompletedCharacterEquipmentAuthorityEnvelope CaptureCompletedCharacterEquipmentAuthorityEnvelope(
            EquipmentChangeRequest request,
            CharacterBuild build)
        {
            InventoryUI inventoryWindow = uiWindowManager?.InventoryWindow as InventoryUI;
            return new CompletedCharacterEquipmentAuthorityEnvelope
            {
                Request = request,
                BuildBeforeLocalAccept = build?.Clone(),
                EquipInventoryBeforeLocalAccept = CaptureInventorySnapshot(inventoryWindow, InventoryType.EQUIP),
                CashInventoryBeforeLocalAccept = CaptureInventorySnapshot(inventoryWindow, InventoryType.CASH)
            };
        }

        private static IReadOnlyList<InventorySlotData> CaptureInventorySnapshot(InventoryUI inventoryWindow, InventoryType inventoryType)
        {
            if (inventoryWindow == null)
            {
                return Array.Empty<InventorySlotData>();
            }

            IReadOnlyList<InventorySlotData> liveSlots = inventoryWindow.GetSlots(inventoryType);
            List<InventorySlotData> snapshot = new(liveSlots.Count);
            for (int i = 0; i < liveSlots.Count; i++)
            {
                snapshot.Add(liveSlots[i]?.Clone());
            }

            return snapshot;
        }

        private static void RestoreCharacterEquipmentState(CharacterBuild build, CharacterBuild snapshot)
        {
            if (build == null || snapshot == null)
            {
                return;
            }

            build.Equipment = CloneEquipmentLayer(snapshot.Equipment);
            build.HiddenEquipment = CloneEquipmentLayer(snapshot.HiddenEquipment);
        }

        private static Dictionary<EquipSlot, CharacterPart> CloneEquipmentLayer(
            IReadOnlyDictionary<EquipSlot, CharacterPart> source)
        {
            Dictionary<EquipSlot, CharacterPart> clone = new();
            if (source == null)
            {
                return clone;
            }

            foreach (KeyValuePair<EquipSlot, CharacterPart> entry in source)
            {
                clone[entry.Key] = entry.Value?.Clone();
            }

            return clone;
        }

        private static void RestoreCompletedCharacterEquipmentAuthorityBaseline(
            CharacterBuild build,
            InventoryUI inventoryWindow,
            CompletedCharacterEquipmentAuthorityEnvelope envelope)
        {
            if (envelope == null)
            {
                return;
            }

            RestoreCharacterEquipmentState(build, envelope.BuildBeforeLocalAccept);
            inventoryWindow?.ReplaceInventory(InventoryType.EQUIP, envelope.EquipInventoryBeforeLocalAccept);
            inventoryWindow?.ReplaceInventory(InventoryType.CASH, envelope.CashInventoryBeforeLocalAccept);
        }

        private bool TryApplyResolvedCharacterEquipmentResultToInventory(
            EquipmentChangeRequest request,
            EquipmentChangeResult result,
            InventoryUI inventoryWindow,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null || result == null || inventoryWindow == null)
            {
                rejectReason = "Inventory runtime is unavailable.";
                return false;
            }

            if (EquipmentChangeClientParity.HasAuthorityInventorySlotStates(result))
            {
                IReadOnlyList<InventorySlotData> liveEquipInventory = CaptureInventorySnapshot(inventoryWindow, InventoryType.EQUIP);
                IReadOnlyList<InventorySlotData> liveCashInventory = CaptureInventorySnapshot(inventoryWindow, InventoryType.CASH);
                if (!TryValidateAuthorityInventoryStateRequestScope(
                        request,
                        result,
                        liveEquipInventory,
                        liveCashInventory,
                        out rejectReason))
                {
                    return false;
                }

                if (!TryResolveAuthoritativeInventorySnapshots(
                        result,
                        liveEquipInventory,
                        inventoryWindow.GetSlotLimit(InventoryType.EQUIP),
                        liveCashInventory,
                        inventoryWindow.GetSlotLimit(InventoryType.CASH),
                        out IReadOnlyList<InventorySlotData> authoritativeEquipInventory,
                        out IReadOnlyList<InventorySlotData> authoritativeCashInventory,
                        out rejectReason))
                {
                    return false;
                }

                inventoryWindow.ReplaceInventory(InventoryType.EQUIP, authoritativeEquipInventory);
                inventoryWindow.ReplaceInventory(InventoryType.CASH, authoritativeCashInventory);
                return true;
            }

            switch (request.Kind)
            {
                case EquipmentChangeRequestKind.InventoryToCharacter:
                    if (!inventoryWindow.TryRemoveSlotAt(request.SourceInventoryType, request.SourceInventoryIndex, out _))
                    {
                        rejectReason = "The source inventory slot changed before the packet-authored equipment result completed.";
                        return false;
                    }

                    AddResolvedInventorySlots(result.DisplacedParts, inventoryWindow);
                    return true;

                case EquipmentChangeRequestKind.CharacterToCharacter:
                    AddResolvedInventorySlots(result.DisplacedParts, inventoryWindow);
                    return true;

                case EquipmentChangeRequestKind.CharacterToInventory:
                    InventorySlotData returnedSlot = CreateInventorySlot(result.ReturnedPart);
                    if (returnedSlot != null)
                    {
                        inventoryWindow.AddItem(ResolveInventoryTypeForSlot(returnedSlot), returnedSlot);
                    }

                    return true;

                default:
                    rejectReason = "Unsupported character equipment authority request kind.";
                    return false;
            }
        }

        private void AddResolvedInventorySlots(IReadOnlyList<CharacterPart> parts, InventoryUI inventoryWindow)
        {
            if (parts == null || inventoryWindow == null)
            {
                return;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                InventorySlotData slot = CreateInventorySlot(parts[i]);
                if (slot != null)
                {
                    inventoryWindow.AddItem(ResolveInventoryTypeForSlot(slot), slot);
                }
            }
        }

        private bool CanAcceptResolvedInventoryParts(
            IReadOnlyList<CharacterPart> parts,
            InventoryUI inventoryWindow,
            out string rejectReason)
        {
            rejectReason = null;
            if (parts == null || parts.Count == 0)
            {
                return true;
            }

            if (inventoryWindow == null)
            {
                rejectReason = "Inventory runtime is unavailable.";
                return false;
            }

            Dictionary<InventoryType, int> requiredSlotsByType = new();
            for (int i = 0; i < parts.Count; i++)
            {
                CharacterPart part = parts[i];
                if (part == null)
                {
                    continue;
                }

                InventoryType inventoryType = part.IsCash ? InventoryType.CASH : InventoryType.EQUIP;
                if (!inventoryWindow.CanAcceptItem(inventoryType, part.ItemId, 1, maxStackSize: 1))
                {
                    string inventoryLabel = inventoryType == InventoryType.CASH ? "cash" : "equipment";
                    rejectReason = $"There is no free {inventoryLabel} inventory slot for the packet-displaced equipment.";
                    return false;
                }

                requiredSlotsByType.TryGetValue(inventoryType, out int requiredSlots);
                requiredSlotsByType[inventoryType] = requiredSlots + 1;
            }

            foreach (KeyValuePair<InventoryType, int> entry in requiredSlotsByType)
            {
                int freeSlotCount = CountAvailableInventorySlots(inventoryWindow, entry.Key);
                if (freeSlotCount < entry.Value)
                {
                    string inventoryLabel = entry.Key == InventoryType.CASH ? "cash" : "equipment";
                    rejectReason = $"There is no free {inventoryLabel} inventory slot for the packet-displaced equipment.";
                    return false;
                }
            }

            return true;
        }

        private static int CountAvailableInventorySlots(InventoryUI inventoryWindow, InventoryType inventoryType)
        {
            if (inventoryWindow == null || inventoryType == InventoryType.NONE)
            {
                return 0;
            }

            IReadOnlyList<InventorySlotData> slots = inventoryWindow.GetSlots(inventoryType);
            int occupiedCount = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    occupiedCount++;
                }
            }

            return Math.Max(0, inventoryWindow.GetSlotLimit(inventoryType) - occupiedCount);
        }

        private static InventoryType ResolveInventoryTypeForSlot(InventorySlotData slot)
        {
            return slot?.PreferredInventoryType is InventoryType type && type != InventoryType.NONE
                ? type
                : InventoryType.EQUIP;
        }

        private InventorySlotData CreateInventorySlot(CharacterPart part)
        {
            if (part == null)
            {
                return null;
            }

            return new InventorySlotData
            {
                ItemId = part.ItemId,
                Quantity = 1,
                MaxStackSize = 1,
                PreferredInventoryType = part.IsCash ? InventoryType.CASH : InventoryType.EQUIP,
                GradeFrameIndex = 0,
                ItemName = string.IsNullOrWhiteSpace(part.Name) ? $"Equip {part.ItemId}" : part.Name,
                ItemTypeName = string.IsNullOrWhiteSpace(part.ItemCategory) ? "Equip" : part.ItemCategory,
                Description = part.Description,
                OwnerAccountId = part.OwnerAccountId,
                OwnerCharacterId = part.OwnerCharacterId,
                IsCashOwnershipLocked = part.IsCashOwnershipLocked,
                TooltipPart = part.Clone()
            };
        }

        private bool TryQueueObservedLiveMechanicRecoveryResult(
            PendingEquipmentChangeEnvelope pendingEnvelope,
            out string message)
        {
            message = null;
            if (pendingEnvelope?.Request == null
                || !pendingEnvelope.AllowObservedLiveMechanicRecovery
                || !pendingEnvelope.AwaitingMechanicPacketAuthority)
            {
                return false;
            }

            if (_playerManager?.Player?.Build is not CharacterBuild build)
            {
                return false;
            }

            MechanicEquipmentController controller = _playerManager?.CompanionEquipment?.Mechanic;
            InventoryUI inventoryWindow = uiWindowManager?.InventoryWindow as InventoryUI;
            if (controller == null || inventoryWindow == null)
            {
                return false;
            }

            controller.EnsureDefaults(build);
            IReadOnlyDictionary<MechanicEquipSlot, int> currentMechanicState = CaptureMechanicStateSnapshot(controller);
            IReadOnlyList<InventorySlotData> currentEquipInventory = CaptureInventorySnapshot(inventoryWindow, InventoryType.EQUIP);
            if (!MechanicEquipmentPacketParity.TryRecognizeObservedLiveBridgeEquipInCompletion(
                    pendingEnvelope.Request,
                    pendingEnvelope.MechanicStateBeforeLiveRecovery,
                    currentMechanicState,
                    currentEquipInventory,
                    out _))
            {
                return false;
            }

            EquipmentChangeResult observedResult = EquipmentChangeResult.Accept(
                    displacedInventorySlots: Array.Empty<InventorySlotData>())
                .WithCompletionMetadata(
                    pendingEnvelope.Request.RequestId,
                    pendingEnvelope.Request.RequestedAtTick,
                    currTickCount,
                    build.ComputeEquipmentStateToken(),
                    ComputeMechanicEquipmentStateToken(build));
            return TryQueueMechanicEquipmentPacketResult(
                pendingEnvelope.Request.RequestId,
                pendingEnvelope.Request.RequestedAtTick,
                observedResult,
                out message);
        }

        private bool TryQueueMechanicAuthorityResultFromInventoryOperationPayload(
            byte[] payload,
            out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "Inventory-operation payload is empty.";
                return false;
            }

            List<PendingEquipmentChangeEnvelope> mechanicCandidates = new();
            foreach (PendingEquipmentChangeEnvelope envelope in _pendingEquipmentChangeRequests.Values)
            {
                if (envelope?.Request != null
                    && envelope.AwaitingMechanicPacketAuthority
                    && IsMechanicEquipmentRequest(envelope.Request))
                {
                    mechanicCandidates.Add(envelope);
                }
            }

            if (mechanicCandidates.Count == 0)
            {
                message = "Inventory-operation payload did not match an active mechanic packet-owned request.";
                return false;
            }

            PendingEquipmentChangeEnvelope matchedEnvelope = null;
            string lastMismatchReason = null;
            string structuralRejectReason = null;
            for (int i = 0; i < mechanicCandidates.Count; i++)
            {
                PendingEquipmentChangeEnvelope candidate = mechanicCandidates[i];
                if (MechanicEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                        candidate.Request,
                        payload,
                        out string rejectReason))
                {
                    matchedEnvelope = candidate;
                    break;
                }

                if (IsMechanicInventoryOperationRequestMismatch(rejectReason))
                {
                    lastMismatchReason = rejectReason;
                    continue;
                }

                structuralRejectReason = rejectReason;
                break;
            }

            if (matchedEnvelope?.Request == null)
            {
                message = !string.IsNullOrWhiteSpace(structuralRejectReason)
                    ? structuralRejectReason
                    : !string.IsNullOrWhiteSpace(lastMismatchReason)
                        ? lastMismatchReason
                        : "Inventory-operation payload did not match an active mechanic packet-owned request.";
                return false;
            }

            return TryQueuePacketOwnedMechanicAuthorityResult(
                new MechanicEquipPacketPayload(
                    MechanicEquipPacketPayloadMode.AuthorityResult,
                    null,
                    0,
                    null,
                    matchedEnvelope.Request.RequestId,
                    matchedEnvelope.Request.RequestedAtTick,
                    AuthorityResultKind: MechanicEquipAuthorityResultKind.LocalRequestAccept),
                out message);
        }

        private static bool IsMechanicInventoryOperationRequestMismatch(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            return reason.StartsWith("Inventory-operation swap did not ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Inventory-operation add entry did not ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic equip-in inventory-operation ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic drag-back-out inventory-operation ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Only mechanic equip-in or drag-back-out requests ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic equip-in request is missing ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic drag-back-out request is missing ", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryQueueCharacterAuthorityResultFromInventoryOperationPayload(
            byte[] payload,
            out string message)
        {
            message = null;
            if (payload == null || payload.Length == 0)
            {
                message = "Inventory-operation payload is empty.";
                return false;
            }

            PendingEquipmentChangeEnvelope pendingEnvelope = null;
            foreach (PendingEquipmentChangeEnvelope envelope in _pendingEquipmentChangeRequests.Values)
            {
                if (envelope?.Request == null
                    || !envelope.AwaitingCharacterPacketAuthority
                    || !IsCharacterEquipmentRequest(envelope.Request))
                {
                    continue;
                }

                pendingEnvelope = envelope;
                break;
            }

            if (pendingEnvelope?.Request == null)
            {
                message = "Inventory-operation payload did not match an active character equipment packet-owned request.";
                return false;
            }

            if (!CharacterEquipmentPacketParity.TryRecognizeClientInventoryOperationCompletion(
                    pendingEnvelope.Request,
                    payload,
                    out string rejectReason))
            {
                message = rejectReason;
                return false;
            }

            return TryQueuePacketOwnedCharacterAuthorityResult(
                new CharacterEquipmentAuthorityPayload(
                    CharacterEquipmentAuthorityPayloadMode.AuthorityResult,
                    pendingEnvelope.Request.RequestId,
                    pendingEnvelope.Request.RequestedAtTick,
                    OwnerKind: pendingEnvelope.Request.OwnerKind,
                    OwnerSessionId: pendingEnvelope.Request.OwnerSessionId,
                    ExpectedCharacterId: pendingEnvelope.Request.ExpectedCharacterId,
                    ResultKind: CharacterEquipmentAuthorityResultKind.LocalRequestAccept,
                    AuthorityPacketType: CharacterEquipmentPacketParity.ClientInventoryOperationPacketType,
                    HasResultRequestContext: true,
                    HasOwnerSessionContext: true),
                out message);
        }

        internal static bool TryValidateAuthorityInventoryStateRequestScope(
            EquipmentChangeRequest request,
            EquipmentChangeResult result,
            IReadOnlyList<InventorySlotData> liveEquipInventory,
            IReadOnlyList<InventorySlotData> liveCashInventory,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null || result == null)
            {
                rejectReason = "Character equipment authority inventory state did not match an active request.";
                return false;
            }

            if (!EquipmentChangeClientParity.HasAuthorityInventorySlotStates(result))
            {
                return true;
            }

            HashSet<int> requestOwnedItemIds = BuildRequestOwnedAuthorityInventoryItemIds(result);
            for (int i = 0; i < result.AuthorityInventorySlotStates.Count; i++)
            {
                CharacterEquipmentAuthorityInventorySlotState state = result.AuthorityInventorySlotStates[i];
                int liveItemId = GetAuthorityInventorySnapshotItemId(
                    state.InventoryType,
                    state.SlotIndex,
                    liveEquipInventory,
                    liveCashInventory);
                bool isFreedSourceSlot = IsFreedInventoryToCharacterSourceSlot(request, state.InventoryType, state.SlotIndex);

                if (state.ItemId <= 0)
                {
                    if (liveItemId <= 0 || isFreedSourceSlot)
                    {
                        continue;
                    }

                    rejectReason = "Packet-authored inventory state cleared a slot outside the active equipment request.";
                    return false;
                }

                if (state.ItemId == liveItemId)
                {
                    continue;
                }

                if (!requestOwnedItemIds.Contains(state.ItemId))
                {
                    rejectReason = "Packet-authored inventory state moved an item outside the active equipment request.";
                    return false;
                }

                if (liveItemId > 0 && !isFreedSourceSlot)
                {
                    rejectReason = "Packet-authored inventory state overwrote a slot outside the active equipment request.";
                    return false;
                }
            }

            return true;
        }

        internal static bool TryResolveAuthoritativeInventorySnapshots(
            EquipmentChangeResult result,
            IReadOnlyList<InventorySlotData> liveEquipInventory,
            int equipSlotLimit,
            IReadOnlyList<InventorySlotData> liveCashInventory,
            int cashSlotLimit,
            out IReadOnlyList<InventorySlotData> resolvedEquipInventory,
            out IReadOnlyList<InventorySlotData> resolvedCashInventory,
            out string rejectReason)
        {
            resolvedEquipInventory = Array.Empty<InventorySlotData>();
            resolvedCashInventory = Array.Empty<InventorySlotData>();
            rejectReason = null;
            if (!EquipmentChangeClientParity.HasAuthorityInventorySlotStates(result))
            {
                rejectReason = "Packet-authored inventory state is missing.";
                return false;
            }

            if (!TryValidateAuthorityInventorySnapshotTargets(
                    result.AuthorityInventorySlotStates,
                    equipSlotLimit,
                    cashSlotLimit,
                    out rejectReason))
            {
                return false;
            }

            List<InventorySlotData> equipSnapshot = CloneInventorySnapshot(liveEquipInventory);
            List<InventorySlotData> cashSnapshot = CloneInventorySnapshot(liveCashInventory);
            Dictionary<int, Queue<AuthorityInventoryStateCandidate>> candidatePools = BuildAuthorityInventoryCandidatePools(
                liveEquipInventory,
                liveCashInventory,
                result);

            for (int i = 0; i < result.AuthorityInventorySlotStates.Count; i++)
            {
                CharacterEquipmentAuthorityInventorySlotState state = result.AuthorityInventorySlotStates[i];
                List<InventorySlotData> snapshot = state.InventoryType == InventoryType.CASH
                    ? cashSnapshot
                    : equipSnapshot;

                EnsureInventorySnapshotCapacity(snapshot, state.SlotIndex + 1);
                if (state.ItemId <= 0)
                {
                    snapshot[state.SlotIndex] = null;
                    continue;
                }

                if (!TryConsumeAuthorityInventoryStateCandidate(
                        candidatePools,
                        snapshot,
                        state.ItemId,
                        out InventorySlotData resolvedSlot))
                {
                    rejectReason = $"Packet-authored inventory state referenced unresolved item {state.ItemId}.";
                    return false;
                }

                resolvedSlot.PendingRequestId = 0;
                resolvedSlot.IsDisabled = false;
                resolvedSlot.PreferredInventoryType = state.InventoryType;
                snapshot[state.SlotIndex] = resolvedSlot;
            }

            TrimTrailingNullInventorySlots(equipSnapshot);
            TrimTrailingNullInventorySlots(cashSnapshot);
            resolvedEquipInventory = equipSnapshot.AsReadOnly();
            resolvedCashInventory = cashSnapshot.AsReadOnly();
            return true;
        }

        private static HashSet<int> BuildRequestOwnedAuthorityInventoryItemIds(EquipmentChangeResult result)
        {
            HashSet<int> itemIds = new();
            if (result?.ReturnedPart?.ItemId > 0)
            {
                itemIds.Add(result.ReturnedPart.ItemId);
            }

            if (result?.DisplacedParts != null)
            {
                for (int i = 0; i < result.DisplacedParts.Count; i++)
                {
                    int itemId = result.DisplacedParts[i]?.ItemId ?? 0;
                    if (itemId > 0)
                    {
                        itemIds.Add(itemId);
                    }
                }
            }

            return itemIds;
        }

        private static int GetAuthorityInventorySnapshotItemId(
            InventoryType inventoryType,
            int slotIndex,
            IReadOnlyList<InventorySlotData> liveEquipInventory,
            IReadOnlyList<InventorySlotData> liveCashInventory)
        {
            IReadOnlyList<InventorySlotData> slots = inventoryType == InventoryType.CASH
                ? liveCashInventory
                : liveEquipInventory;
            return slotIndex >= 0 && slots != null && slotIndex < slots.Count
                ? slots[slotIndex]?.ItemId ?? 0
                : 0;
        }

        private static bool IsFreedInventoryToCharacterSourceSlot(
            EquipmentChangeRequest request,
            InventoryType inventoryType,
            int slotIndex)
        {
            return request?.Kind == EquipmentChangeRequestKind.InventoryToCharacter
                   && request.SourceInventoryType == inventoryType
                   && request.SourceInventoryIndex == slotIndex;
        }

        private static bool TryValidateAuthorityInventorySnapshotTargets(
            IReadOnlyList<CharacterEquipmentAuthorityInventorySlotState> slotStates,
            int equipSlotLimit,
            int cashSlotLimit,
            out string rejectReason)
        {
            rejectReason = null;
            if (slotStates == null || slotStates.Count == 0)
            {
                return true;
            }

            HashSet<(InventoryType InventoryType, int SlotIndex)> seenSlots = new();
            for (int i = 0; i < slotStates.Count; i++)
            {
                CharacterEquipmentAuthorityInventorySlotState state = slotStates[i];
                if (!EquipmentChangeClientParity.IsSupportedCharacterEquipmentSourceInventory(state.InventoryType))
                {
                    rejectReason = "Character equipment authority returned an unsupported inventory state.";
                    return false;
                }

                int slotLimit = state.InventoryType == InventoryType.CASH ? cashSlotLimit : equipSlotLimit;
                if (state.SlotIndex < 0 || state.SlotIndex >= slotLimit)
                {
                    rejectReason = "Character equipment authority returned an inventory state outside the active slot range.";
                    return false;
                }

                if (!seenSlots.Add((state.InventoryType, state.SlotIndex)))
                {
                    rejectReason = "Character equipment authority returned duplicate inventory slot states.";
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<int, Queue<AuthorityInventoryStateCandidate>> BuildAuthorityInventoryCandidatePools(
            IReadOnlyList<InventorySlotData> liveEquipInventory,
            IReadOnlyList<InventorySlotData> liveCashInventory,
            EquipmentChangeResult result)
        {
            Dictionary<int, Queue<AuthorityInventoryStateCandidate>> candidatePools = new();
            AddAuthorityInventoryStateCandidates(candidatePools, InventoryType.EQUIP, liveEquipInventory);
            AddAuthorityInventoryStateCandidates(candidatePools, InventoryType.CASH, liveCashInventory);

            if (result?.DisplacedParts != null)
            {
                for (int i = 0; i < result.DisplacedParts.Count; i++)
                {
                    InventorySlotData slot = CreateAuthorityInventorySlot(result.DisplacedParts[i]);
                    EnqueueAuthorityInventoryStateCandidate(
                        candidatePools,
                        new AuthorityInventoryStateCandidate(
                            slot?.PreferredInventoryType ?? InventoryType.EQUIP,
                            -1,
                            slot));
                }
            }

            InventorySlotData returnedSlot = CreateAuthorityInventorySlot(result?.ReturnedPart);
            EnqueueAuthorityInventoryStateCandidate(
                candidatePools,
                new AuthorityInventoryStateCandidate(
                    returnedSlot?.PreferredInventoryType ?? InventoryType.EQUIP,
                    -1,
                    returnedSlot));
            return candidatePools;
        }

        private static void AddAuthorityInventoryStateCandidates(
            Dictionary<int, Queue<AuthorityInventoryStateCandidate>> candidatePools,
            InventoryType inventoryType,
            IReadOnlyList<InventorySlotData> slots)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotData slot = slots[i]?.Clone();
                if (slot == null || slot.ItemId <= 0)
                {
                    continue;
                }

                slot.PendingRequestId = 0;
                slot.IsDisabled = false;
                slot.PreferredInventoryType = inventoryType;
                EnqueueAuthorityInventoryStateCandidate(
                    candidatePools,
                    new AuthorityInventoryStateCandidate(inventoryType, i, slot));
            }
        }

        private static void EnqueueAuthorityInventoryStateCandidate(
            Dictionary<int, Queue<AuthorityInventoryStateCandidate>> candidatePools,
            AuthorityInventoryStateCandidate candidate)
        {
            if (candidate.Slot == null || candidate.Slot.ItemId <= 0)
            {
                return;
            }

            if (!candidatePools.TryGetValue(candidate.Slot.ItemId, out Queue<AuthorityInventoryStateCandidate> candidates))
            {
                candidates = new Queue<AuthorityInventoryStateCandidate>();
                candidatePools[candidate.Slot.ItemId] = candidates;
            }

            candidates.Enqueue(candidate);
        }

        private static bool TryConsumeAuthorityInventoryStateCandidate(
            Dictionary<int, Queue<AuthorityInventoryStateCandidate>> candidatePools,
            List<InventorySlotData> targetSnapshot,
            int itemId,
            out InventorySlotData resolvedSlot)
        {
            resolvedSlot = null;
            if (itemId <= 0
                || candidatePools == null
                || !candidatePools.TryGetValue(itemId, out Queue<AuthorityInventoryStateCandidate> candidates))
            {
                return false;
            }

            while (candidates.Count > 0)
            {
                AuthorityInventoryStateCandidate candidate = candidates.Dequeue();
                if (candidate.Slot == null || candidate.Slot.ItemId != itemId)
                {
                    continue;
                }

                if (candidate.SlotIndex >= 0
                    && candidate.InventoryType == candidate.Slot.PreferredInventoryType
                    && targetSnapshot != null
                    && candidate.SlotIndex < targetSnapshot.Count)
                {
                    targetSnapshot[candidate.SlotIndex] = null;
                }

                resolvedSlot = candidate.Slot.Clone();
                return true;
            }

            return false;
        }

        private static List<InventorySlotData> CloneInventorySnapshot(IReadOnlyList<InventorySlotData> snapshot)
        {
            List<InventorySlotData> clone = new(snapshot?.Count ?? 0);
            if (snapshot == null)
            {
                return clone;
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                clone.Add(snapshot[i]?.Clone());
            }

            return clone;
        }

        private static void EnsureInventorySnapshotCapacity(List<InventorySlotData> snapshot, int requiredCount)
        {
            if (snapshot == null || requiredCount <= 0)
            {
                return;
            }

            while (snapshot.Count < requiredCount)
            {
                snapshot.Add(null);
            }
        }

        private static void TrimTrailingNullInventorySlots(List<InventorySlotData> snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                if (snapshot[i] != null)
                {
                    break;
                }

                snapshot.RemoveAt(i);
            }
        }

        private static InventorySlotData CreateAuthorityInventorySlot(CharacterPart part)
        {
            if (part == null)
            {
                return null;
            }

            return new InventorySlotData
            {
                ItemId = part.ItemId,
                Quantity = 1,
                MaxStackSize = 1,
                PreferredInventoryType = part.IsCash ? InventoryType.CASH : InventoryType.EQUIP,
                GradeFrameIndex = 0,
                ItemName = string.IsNullOrWhiteSpace(part.Name) ? $"Equip {part.ItemId}" : part.Name,
                ItemTypeName = string.IsNullOrWhiteSpace(part.ItemCategory) ? "Equip" : part.ItemCategory,
                Description = part.Description,
                OwnerAccountId = part.OwnerAccountId,
                OwnerCharacterId = part.OwnerCharacterId,
                IsCashOwnershipLocked = part.IsCashOwnershipLocked,
                TooltipPart = part.Clone()
            };
        }

        private static bool FailPacketOwnedCharacterAuthorityResult(
            string rejectReason,
            out EquipmentChangeResult result,
            out string message)
        {
            result = null;
            message = rejectReason;
            return false;
        }

        private static bool FailPacketOwnedCharacterAuthorityResult(string rejectReason, out string message)
        {
            message = rejectReason;
            return false;
        }

        private static bool FailPacketOwnedCharacterAuthorityPayload(string rejectReason, out string message)
        {
            message = rejectReason;
            return false;
        }

        private bool TryQueueMechanicEquipmentPacketResult(
            int requestId,
            int requestedAtTick,
            EquipmentChangeResult result,
            out string message)
        {
            message = null;
            if (requestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(requestId, out PendingEquipmentChangeEnvelope pendingEnvelope)
                || pendingEnvelope?.Request == null)
            {
                message = $"Mechanic packet result did not match a pending request id ({requestId}).";
                return false;
            }

            EquipmentChangeRequest request = pendingEnvelope.Request;
            if (!IsMechanicEquipmentRequest(request))
            {
                message = $"Pending request {requestId} is not owned by the mechanic equipment tab.";
                return false;
            }

            if (request.RequestedAtTick != requestedAtTick)
            {
                message = $"Mechanic packet result for request {requestId} did not match the pending request timestamp.";
                return false;
            }

            _pendingMechanicEquipmentPacketResults[requestId] = result;
            pendingEnvelope.AwaitingMechanicPacketAuthority = false;
            pendingEnvelope.MechanicPacketAuthorityDeadlineAtTick = currTickCount;
            message = result.Accepted
                ? $"Queued packet-authored mechanic equipment result for request {requestId}."
                : $"Queued packet-authored mechanic equipment rejection for request {requestId}.";
            return true;
        }

        private bool TryResolvePacketOwnedMechanicAuthorityRequest(
            MechanicEquipPacketPayload payload,
            out string message)
        {
            message = null;
            if (payload.Mode != MechanicEquipPacketPayloadMode.AuthorityRequest)
            {
                message = "Mechanic authority payload is not a request.";
                return false;
            }

            if (payload.RequestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(payload.RequestId, out PendingEquipmentChangeEnvelope pendingEnvelope)
                || pendingEnvelope?.Request == null)
            {
                message = $"Mechanic authority request did not match a pending request id ({payload.RequestId}).";
                return false;
            }

            EquipmentChangeRequest request = pendingEnvelope.Request;
            if (!IsMechanicEquipmentRequest(request))
            {
                message = $"Pending request {payload.RequestId} is not owned by the mechanic equipment tab.";
                return false;
            }

            if (request.RequestedAtTick != payload.RequestedAtTick)
            {
                message = $"Mechanic authority request for {payload.RequestId} did not match the pending request timestamp.";
                return false;
            }

            if (request.Kind != payload.RequestKind
                || request.OwnerKind != payload.OwnerKind
                || request.OwnerSessionId != payload.OwnerSessionId
                || request.ExpectedCharacterId != payload.ExpectedCharacterId
                || request.ExpectedBuildStateToken != payload.ExpectedBuildStateToken
                || request.ExpectedMechanicStateToken != payload.ExpectedMechanicStateToken
                || request.ItemId != payload.ItemId
                || request.SourceInventoryType != payload.SourceInventoryType
                || request.SourceInventoryIndex != payload.SourceInventoryIndex
                || request.TargetMechanicSlot != payload.TargetMechanicSlot
                || request.SourceMechanicSlot != payload.SourceMechanicSlot)
            {
                message = $"Mechanic authority request {payload.RequestId} did not match the pending request state.";
                return false;
            }

            return TryQueuePacketOwnedMechanicAuthorityResult(
                new MechanicEquipPacketPayload(
                    MechanicEquipPacketPayloadMode.AuthorityResult,
                    null,
                    0,
                    null,
                    payload.RequestId,
                    payload.RequestedAtTick,
                    AuthorityResultKind: MechanicEquipAuthorityResultKind.LocalRequestAccept),
                out message);
        }

        private bool TryQueuePacketOwnedMechanicAuthorityResult(
            MechanicEquipPacketPayload payload,
            out string message)
        {
            message = null;
            if (payload.Mode != MechanicEquipPacketPayloadMode.AuthorityResult)
            {
                message = "Mechanic authority payload is not a result.";
                return false;
            }

            if (payload.RequestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(payload.RequestId, out PendingEquipmentChangeEnvelope pendingEnvelope)
                || pendingEnvelope?.Request == null)
            {
                message = $"Mechanic authority result did not match a pending request id ({payload.RequestId}).";
                return false;
            }

            EquipmentChangeRequest request = pendingEnvelope.Request;
            if (!IsMechanicEquipmentRequest(request))
            {
                message = $"Pending request {payload.RequestId} is not owned by the mechanic equipment tab.";
                return false;
            }

            if (request.RequestedAtTick != payload.RequestedAtTick)
            {
                message = $"Mechanic authority result for request {payload.RequestId} did not match the pending request timestamp.";
                return false;
            }

            CharacterBuild build = _playerManager?.Player?.Build;
            MechanicEquipmentController controller = _playerManager?.CompanionEquipment?.Mechanic;
            if (build == null || controller == null)
            {
                message = "Mechanic equipment runtime is unavailable.";
                return false;
            }

            if (payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.Reject)
            {
                EquipmentChangeResult rejectResult = EquipmentChangeResult.Reject(
                    string.IsNullOrWhiteSpace(payload.RejectReason)
                        ? "The mechanic equipment request was rejected by packet authority."
                        : payload.RejectReason)
                    .WithCompletionMetadata(
                        payload.RequestId,
                        payload.RequestedAtTick,
                        currTickCount,
                        payload.ResolvedBuildStateToken != 0 ? payload.ResolvedBuildStateToken : build.ComputeEquipmentStateToken(),
                        payload.ResolvedMechanicStateToken != 0 ? payload.ResolvedMechanicStateToken : ComputeMechanicEquipmentStateToken(build));
                return TryQueueMechanicEquipmentPacketResult(payload.RequestId, payload.RequestedAtTick, rejectResult, out message);
            }

            if (EquipmentChangeRequestValidator.TryGetRequestStateRejectReason(
                    request,
                    build,
                    out string requestStateRejectReason,
                    () => ComputeMechanicEquipmentStateToken(build)))
            {
                EquipmentChangeResult staleReject = EquipmentChangeResult.Reject(requestStateRejectReason)
                    .WithCompletionMetadata(
                        payload.RequestId,
                        payload.RequestedAtTick,
                        currTickCount,
                        build.ComputeEquipmentStateToken(),
                        ComputeMechanicEquipmentStateToken(build));
                return TryQueueMechanicEquipmentPacketResult(payload.RequestId, payload.RequestedAtTick, staleReject, out message);
            }

            if (!TryCreatePacketOwnedMechanicAuthorityResult(request, build, controller, payload, out EquipmentChangeResult acceptedResult, out string rejectReason))
            {
                EquipmentChangeResult rejectResult = EquipmentChangeResult.Reject(rejectReason)
                    .WithCompletionMetadata(
                        payload.RequestId,
                        payload.RequestedAtTick,
                        currTickCount,
                        build.ComputeEquipmentStateToken(),
                        ComputeMechanicEquipmentStateToken(build));
                return TryQueueMechanicEquipmentPacketResult(payload.RequestId, payload.RequestedAtTick, rejectResult, out message);
            }

            return TryQueueMechanicEquipmentPacketResult(payload.RequestId, payload.RequestedAtTick, acceptedResult, out message);
        }

        private bool TryCreatePacketOwnedMechanicAuthorityResult(
            EquipmentChangeRequest request,
            CharacterBuild build,
            MechanicEquipmentController controller,
            MechanicEquipPacketPayload payload,
            out EquipmentChangeResult result,
            out string rejectReason)
        {
            result = null;
            rejectReason = null;
            if (payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.LocalRequestAccept)
            {
                result = request.Kind switch
                {
                    EquipmentChangeRequestKind.InventoryToCompanion => HandleInventoryToCompanionChange(request, build),
                    EquipmentChangeRequestKind.CompanionToInventory => HandleCompanionToInventoryChange(request, build),
                    _ => EquipmentChangeResult.Reject("Unsupported mechanic authority request kind.")
                };

                if (!result.Accepted)
                {
                    rejectReason = result.RejectReason;
                    return false;
                }

                result = result.WithCompletionMetadata(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    currTickCount,
                    build.ComputeEquipmentStateToken(),
                    ComputeMechanicEquipmentStateToken(build));
                return true;
            }

            return request.Kind switch
            {
                EquipmentChangeRequestKind.InventoryToCompanion => TryCreatePacketOwnedInventoryToMechanicAuthorityResult(
                    request,
                    build,
                    controller,
                    payload,
                    out result,
                    out rejectReason),
                EquipmentChangeRequestKind.CompanionToInventory => TryCreatePacketOwnedMechanicToInventoryAuthorityResult(
                    request,
                    build,
                    controller,
                    payload,
                    out result,
                    out rejectReason),
                _ => FailPacketOwnedMechanicAuthorityResult("Unsupported mechanic authority request kind.", out result, out rejectReason)
            };
        }

        private bool TryCreatePacketOwnedInventoryToMechanicAuthorityResult(
            EquipmentChangeRequest request,
            CharacterBuild build,
            MechanicEquipmentController controller,
            MechanicEquipPacketPayload payload,
            out EquipmentChangeResult result,
            out string rejectReason)
        {
            result = null;
            rejectReason = null;
            if (request.TargetCompanionKind != EquipmentChangeCompanionKind.Mechanic
                || !request.TargetMechanicSlot.HasValue)
            {
                rejectReason = "Packet authority result did not target a mechanic machine slot.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                rejectReason = "Inventory runtime is unavailable.";
                return false;
            }

            IReadOnlyList<InventorySlotData> liveSlots = inventoryWindow.GetSlots(request.SourceInventoryType);
            if (request.SourceInventoryIndex < 0 || request.SourceInventoryIndex >= liveSlots.Count)
            {
                rejectReason = "The source inventory slot changed before the mechanic equipment request completed.";
                return false;
            }

            InventorySlotData liveSlot = liveSlots[request.SourceInventoryIndex];
            if (EquipmentChangeRequestValidator.TryGetInventorySourceRejectReason(request, liveSlot, out rejectReason))
            {
                return false;
            }

            if (TryGetCompanionCashOwnershipRejectReason(
                    liveSlot,
                    build,
                    ResolveLoginRosterAccountId(),
                    out rejectReason))
            {
                return false;
            }

            controller.EnsureDefaults(build);
            Dictionary<MechanicEquipSlot, int> beforeState = CaptureMechanicStateSnapshot(controller);
            if (!TryValidatePacketOwnedMechanicAuthorityScope(request.TargetMechanicSlot.Value, beforeState, payload, request.ItemId, requireClearedSlot: false, out rejectReason))
            {
                return false;
            }

            controller.TryGetItem(request.TargetMechanicSlot.Value, out CompanionEquipItem displacedItem);
            if (!TryApplyPacketOwnedMechanicAuthorityState(controller, build, payload, out rejectReason))
            {
                return false;
            }

            result = EquipmentChangeResult.Accept(
                displacedInventorySlots: displacedItem == null
                    ? Array.Empty<InventorySlotData>()
                    : EquipUIBigBang.CreateInventorySlots(new[] { displacedItem }))
                .WithCompletionMetadata(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    currTickCount,
                    build.ComputeEquipmentStateToken(),
                    ComputeMechanicEquipmentStateToken(build));
            return true;
        }

        private bool TryCreatePacketOwnedMechanicToInventoryAuthorityResult(
            EquipmentChangeRequest request,
            CharacterBuild build,
            MechanicEquipmentController controller,
            MechanicEquipPacketPayload payload,
            out EquipmentChangeResult result,
            out string rejectReason)
        {
            result = null;
            rejectReason = null;
            if (request.SourceCompanionKind != EquipmentChangeCompanionKind.Mechanic
                || !request.SourceMechanicSlot.HasValue)
            {
                rejectReason = "Packet authority result did not target a mechanic machine slot.";
                return false;
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                rejectReason = "Inventory runtime is unavailable.";
                return false;
            }

            controller.EnsureDefaults(build);
            if (!controller.TryGetItem(request.SourceMechanicSlot.Value, out CompanionEquipItem liveItem)
                || liveItem == null
                || liveItem.ItemId != request.ItemId)
            {
                rejectReason = "The mechanic machine part changed before the request completed.";
                return false;
            }

            if (TryGetCompanionCashOwnershipRejectReason(
                    liveItem,
                    build,
                    ResolveLoginRosterAccountId(),
                    out rejectReason))
            {
                return false;
            }

            InventoryType inventoryType = liveItem.IsCash ? InventoryType.CASH : InventoryType.EQUIP;
            if (!inventoryWindow.CanAcceptItem(inventoryType, liveItem.ItemId, 1, maxStackSize: 1))
            {
                string inventoryLabel = inventoryType == InventoryType.CASH ? "cash" : "equipment";
                rejectReason = $"There is no free {inventoryLabel} inventory slot for this companion item.";
                return false;
            }

            Dictionary<MechanicEquipSlot, int> beforeState = CaptureMechanicStateSnapshot(controller);
            if (!TryValidatePacketOwnedMechanicAuthorityScope(request.SourceMechanicSlot.Value, beforeState, payload, request.ItemId, requireClearedSlot: true, out rejectReason))
            {
                return false;
            }

            if (!TryApplyPacketOwnedMechanicAuthorityState(controller, build, payload, out rejectReason))
            {
                return false;
            }

            InventorySlotData returnedSlot = EquipUIBigBang.CreateInventorySlot(liveItem);
            result = EquipmentChangeResult.Accept(
                    displacedInventorySlots: returnedSlot == null
                        ? Array.Empty<InventorySlotData>()
                        : new[] { returnedSlot })
                .WithCompletionMetadata(
                    payload.RequestId,
                    payload.RequestedAtTick,
                    currTickCount,
                    build.ComputeEquipmentStateToken(),
                    ComputeMechanicEquipmentStateToken(build));
            return true;
        }

        private static Dictionary<MechanicEquipSlot, int> CaptureMechanicStateSnapshot(MechanicEquipmentController controller)
        {
            Dictionary<MechanicEquipSlot, int> snapshot = new();
            foreach (MechanicEquipSlot slot in Enum.GetValues<MechanicEquipSlot>())
            {
                snapshot[slot] = controller != null && controller.TryGetItem(slot, out CompanionEquipItem item) && item != null
                    ? item.ItemId
                    : 0;
            }

            return snapshot;
        }

        internal static bool TryValidatePacketOwnedMechanicAuthorityScope(
            MechanicEquipSlot requestSlot,
            IReadOnlyDictionary<MechanicEquipSlot, int> beforeState,
            MechanicEquipPacketPayload payload,
            int expectedItemId,
            bool requireClearedSlot,
            out string rejectReason)
        {
            rejectReason = null;
            if (!MechanicEquipmentPacketParity.HasExplicitAuthorityState(payload))
            {
                rejectReason = "Mechanic authority result did not include a usable mechanic state.";
                return false;
            }

            if (payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.ClearAllAccept
                || payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.ResetDefaultsAccept)
            {
                rejectReason = "Multi-slot mechanic authority updates are not compatible with a single pending mechanic equipment request.";
                return false;
            }

            foreach (MechanicEquipSlot slot in Enum.GetValues<MechanicEquipSlot>())
            {
                if (!MechanicEquipmentPacketParity.TryReadFinalItemIdForSlot(payload, beforeState, slot, out int finalItemId, out rejectReason))
                {
                    return false;
                }

                beforeState.TryGetValue(slot, out int currentItemId);
                if (slot != requestSlot)
                {
                    if (finalItemId != currentItemId)
                    {
                        rejectReason = "Packet-authored mechanic authority changed slots outside the active request.";
                        return false;
                    }

                    continue;
                }

                if (requireClearedSlot)
                {
                    if (finalItemId == expectedItemId || finalItemId != 0)
                    {
                        rejectReason = "Packet-authored mechanic authority did not clear the requested machine slot.";
                        return false;
                    }
                }
                else if (finalItemId != expectedItemId)
                {
                    rejectReason = "Packet-authored mechanic authority did not place the requested machine part into the target slot.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryApplyPacketOwnedMechanicAuthorityState(
            MechanicEquipmentController controller,
            CharacterBuild build,
            MechanicEquipPacketPayload payload,
            out string rejectReason)
        {
            rejectReason = null;
            return payload.AuthorityResultKind switch
            {
                MechanicEquipAuthorityResultKind.SnapshotAccept => controller.TryApplyExternalSnapshot(
                    build,
                    payload.SnapshotItems,
                    out rejectReason),
                MechanicEquipAuthorityResultKind.SlotMutationAccept when payload.Slot.HasValue => controller.TryApplyExternalSlotMutation(
                    build,
                    payload.Slot.Value,
                    payload.ItemId,
                    out rejectReason),
                MechanicEquipAuthorityResultKind.ClearAllAccept => controller.TryApplyExternalSnapshot(
                    build,
                    null,
                    out rejectReason),
                MechanicEquipAuthorityResultKind.ResetDefaultsAccept => ApplyPacketOwnedMechanicEquipDefaults(
                    controller,
                    build,
                    out rejectReason),
                _ => FailPacketOwnedMechanicAuthorityResult("Mechanic authority payload does not contain an applicable mechanic state.", out rejectReason)
            };
        }

        private static bool FailPacketOwnedMechanicAuthorityResult(
            string rejectReason,
            out EquipmentChangeResult result,
            out string message)
        {
            result = null;
            message = rejectReason;
            return false;
        }

        private static bool FailPacketOwnedMechanicAuthorityResult(string rejectReason, out string message)
        {
            message = rejectReason;
            return false;
        }

        private EquipmentChangeResult HandleInventoryToCharacterChange(EquipmentChangeRequest request, CharacterBuild build)
        {
            if (!request.TargetEquipSlot.HasValue)
            {
                return EquipmentChangeResult.Reject("No target equipment slot was selected.");
            }

            if (TryGetTamingMobEquipmentRestrictionRejectReason(request, out string tamingMobRestrictionRejectReason))
            {
                return EquipmentChangeResult.Reject(tamingMobRestrictionRejectReason);
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return EquipmentChangeResult.Reject("Inventory runtime is unavailable.");
            }

            IReadOnlyList<InventorySlotData> liveSlots = inventoryWindow.GetSlots(request.SourceInventoryType);
            if (request.SourceInventoryIndex < 0 || request.SourceInventoryIndex >= liveSlots.Count)
            {
                return EquipmentChangeResult.Reject("The source inventory slot changed before the equip request was accepted.");
            }

            InventorySlotData liveSlot = liveSlots[request.SourceInventoryIndex];
            if (EquipmentChangeRequestValidator.TryGetInventorySourceRejectReason(request, liveSlot, out string sourceRejectReason))
            {
                return EquipmentChangeResult.Reject(sourceRejectReason);
            }

            EquipSlotVisualState targetState = EquipSlotStateResolver.ResolveVisualState(build, request.TargetEquipSlot.Value);
            if (targetState.IsDisabled)
            {
                return EquipmentChangeResult.Reject(targetState.Message);
            }

            string restrictionMessage = GetBattlefieldEquipRestrictionMessage(request.ItemId);
            if (!string.IsNullOrWhiteSpace(restrictionMessage))
            {
                return EquipmentChangeResult.Reject(restrictionMessage);
            }

            CharacterPart part = ResolveRequestedEquipmentPart(request, liveSlot);
            if (part == null)
            {
                string itemName = string.IsNullOrWhiteSpace(request.ItemName)
                    ? $"Item #{request.ItemId}"
                    : request.ItemName;
                return EquipmentChangeResult.Reject($"Unable to load {itemName} as an equipment item.");
            }

            if (EquipmentChangeClientParity.TryGetCharacterEquipmentSourceRejectReason(
                    request.SourceInventoryType,
                    part,
                    out string sourceInventoryRejectReason))
            {
                return EquipmentChangeResult.Reject(sourceInventoryRejectReason);
            }

            if (!EquipUIBigBang.CanDisplayPartInSlot(part, request.TargetEquipSlot.Value))
            {
                return EquipmentChangeResult.Reject(EquipUIBigBang.BuildSlotMismatchRejectReason(part));
            }

            if (!EquipUIBigBang.TryGetEquipRequirementRejectReason(part, build, out string requirementRejectReason))
            {
                return EquipmentChangeResult.Reject(requirementRejectReason);
            }

            IReadOnlyList<CharacterPart> displacedParts = build.PlaceEquipment(part, request.TargetEquipSlot.Value);
            return EquipmentChangeResult.Accept(displacedParts: displacedParts);
        }

        private EquipmentChangeResult HandleCharacterToCharacterChange(EquipmentChangeRequest request, CharacterBuild build)
        {
            if (!request.SourceEquipSlot.HasValue || !request.TargetEquipSlot.HasValue)
            {
                return EquipmentChangeResult.Reject("The equipment move is missing a source or target slot.");
            }

            if (TryGetTamingMobEquipmentRestrictionRejectReason(request, out string tamingMobRestrictionRejectReason))
            {
                return EquipmentChangeResult.Reject(tamingMobRestrictionRejectReason);
            }

            CharacterPart liveSourcePart = EquipSlotStateResolver.ResolveDisplayedPart(build, request.SourceEquipSlot.Value);
            if (liveSourcePart == null || liveSourcePart.ItemId != request.ItemId)
            {
                return EquipmentChangeResult.Reject("The equipped item changed before the move request was accepted.");
            }

            if (EquipmentChangeRequestValidator.TryGetCharacterMoveRejectReason(
                    build,
                    liveSourcePart,
                    request.SourceEquipSlot.Value,
                    request.TargetEquipSlot.Value,
                    GetBattlefieldEquipRestrictionMessage,
                    out string moveRejectReason))
            {
                return EquipmentChangeResult.Reject(moveRejectReason);
            }

            if (!EquipUIBigBang.CanDisplayPartInSlot(liveSourcePart, request.TargetEquipSlot.Value))
            {
                return EquipmentChangeResult.Reject(EquipUIBigBang.BuildSlotMismatchRejectReason(liveSourcePart));
            }

            CharacterPart targetPart = EquipSlotStateResolver.ResolveDisplayedPart(build, request.TargetEquipSlot.Value);
            if (targetPart != null
                && targetPart.ItemId != request.ItemId
                && !EquipUIBigBang.CanDisplayPartInSlot(targetPart, request.SourceEquipSlot.Value))
            {
                return EquipmentChangeResult.Reject("The destination equipment cannot return to the source slot.");
            }

            if (request.SourceEquipSlot.Value == request.TargetEquipSlot.Value)
            {
                return EquipmentChangeResult.Accept();
            }

            CharacterPart movingPart = build.Unequip(request.SourceEquipSlot.Value);
            if (movingPart == null)
            {
                return EquipmentChangeResult.Reject("The source equipment is no longer available.");
            }

            IReadOnlyList<CharacterPart> displacedParts = build.PlaceEquipment(movingPart, request.TargetEquipSlot.Value);
            CharacterPart swapCandidate = EquipUIBigBang.SelectSwapCandidateForSource(displacedParts, request.SourceEquipSlot.Value);
            if (swapCandidate != null)
            {
                build.PlaceEquipment(swapCandidate, request.SourceEquipSlot.Value);
            }

            return EquipmentChangeResult.Accept();
        }

        private EquipmentChangeResult HandleCharacterToInventoryChange(EquipmentChangeRequest request, CharacterBuild build)
        {
            if (!request.SourceEquipSlot.HasValue)
            {
                return EquipmentChangeResult.Reject("No source equipment slot was selected.");
            }

            if (TryGetTamingMobEquipmentRestrictionRejectReason(request, out string tamingMobRestrictionRejectReason))
            {
                return EquipmentChangeResult.Reject(tamingMobRestrictionRejectReason);
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return EquipmentChangeResult.Reject("Inventory runtime is unavailable.");
            }

            CharacterPart liveSourcePart = EquipSlotStateResolver.ResolveDisplayedPart(build, request.SourceEquipSlot.Value);
            if (liveSourcePart == null || liveSourcePart.ItemId != request.ItemId)
            {
                return EquipmentChangeResult.Reject("The equipped item changed before the unequip request was accepted.");
            }

            InventoryType inventoryType = EquipmentChangeClientParity.ResolveCharacterEquipmentInventoryType(liveSourcePart);
            if (!EquipmentChangeClientParity.IsSupportedCharacterEquipmentSourceInventory(inventoryType))
            {
                return EquipmentChangeResult.Reject("Only equipment items can return to inventory.");
            }

            if (!inventoryWindow.CanAcceptItem(inventoryType, request.ItemId, 1, maxStackSize: 1))
            {
                string inventoryLabel = inventoryType == InventoryType.CASH ? "cash" : "equipment";
                return EquipmentChangeResult.Reject($"There is no free {inventoryLabel} inventory slot for this item.");
            }

            if (EquipmentChangeRequestValidator.TryGetCharacterUnequipRejectReason(
                    liveSourcePart,
                    GetBattlefieldEquipRestrictionMessage,
                    out string unequipRejectReason))
            {
                return EquipmentChangeResult.Reject(unequipRejectReason);
            }

            CharacterPart removedPart = build.Unequip(request.SourceEquipSlot.Value);
            if (removedPart == null)
            {
                return EquipmentChangeResult.Reject("The source equipment is no longer available.");
            }

            return EquipmentChangeResult.Accept(returnedPart: removedPart);
        }

        private EquipmentChangeResult HandleInventoryToCompanionChange(EquipmentChangeRequest request, CharacterBuild build)
        {
            if (request == null)
            {
                return EquipmentChangeResult.Reject("Companion equipment request is missing.");
            }

            if (TryGetAndroidCompanionRestrictionRejectReason(request, out string androidRestrictionRejectReason))
            {
                return EquipmentChangeResult.Reject(androidRestrictionRejectReason);
            }

            if (TryGetTamingMobEquipmentRestrictionRejectReason(request, out string tamingMobRestrictionRejectReason))
            {
                return EquipmentChangeResult.Reject(tamingMobRestrictionRejectReason);
            }

            if (request.TargetCompanionKind == EquipmentChangeCompanionKind.None)
            {
                return EquipmentChangeResult.Reject("No companion target was selected.");
            }

            if (request.SourceInventoryType != InventoryType.EQUIP
                && request.SourceInventoryType != InventoryType.CASH)
            {
                return EquipmentChangeResult.Reject("Only equip or cash inventory entries can be equipped on companion pages.");
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return EquipmentChangeResult.Reject("Inventory runtime is unavailable.");
            }

            IReadOnlyList<InventorySlotData> liveSlots = inventoryWindow.GetSlots(request.SourceInventoryType);
            if (request.SourceInventoryIndex < 0 || request.SourceInventoryIndex >= liveSlots.Count)
            {
                return EquipmentChangeResult.Reject("The source inventory slot changed before the companion equip request was accepted.");
            }

            InventorySlotData liveSlot = liveSlots[request.SourceInventoryIndex];
            if (EquipmentChangeRequestValidator.TryGetInventorySourceRejectReason(request, liveSlot, out string sourceRejectReason))
            {
                return EquipmentChangeResult.Reject(sourceRejectReason);
            }

            int? ownerAccountId = ResolveLoginRosterAccountId();
            if (TryGetCompanionCashOwnershipRejectReason(
                    liveSlot,
                    build,
                    ownerAccountId,
                    out string ownershipRejectReason))
            {
                return EquipmentChangeResult.Reject(ownershipRejectReason);
            }

            if (TryGetCompanionDisplacedCapacityRejectReason(request, inventoryWindow, build, out string capacityRejectReason))
            {
                return EquipmentChangeResult.Reject(capacityRejectReason);
            }

            EquipmentChangeResult result = request.TargetCompanionKind switch
            {
                EquipmentChangeCompanionKind.Pet => HandleInventoryToPetCompanionChange(request, build, liveSlot, ownerAccountId),
                EquipmentChangeCompanionKind.Dragon => HandleInventoryToDragonCompanionChange(request, build, liveSlot, ownerAccountId),
                EquipmentChangeCompanionKind.Mechanic => HandleInventoryToMechanicCompanionChange(request, build, liveSlot, ownerAccountId),
                EquipmentChangeCompanionKind.Android => HandleInventoryToAndroidCompanionChange(request, build, liveSlot, ownerAccountId),
                _ => EquipmentChangeResult.Reject("Unsupported companion equipment target.")
            };

            return result;
        }

        private EquipmentChangeResult HandleCompanionToInventoryChange(EquipmentChangeRequest request, CharacterBuild build)
        {
            if (request == null)
            {
                return EquipmentChangeResult.Reject("Companion equipment request is missing.");
            }

            if (TryGetAndroidCompanionRestrictionRejectReason(request, out string androidRestrictionRejectReason))
            {
                return EquipmentChangeResult.Reject(androidRestrictionRejectReason);
            }

            if (TryGetTamingMobEquipmentRestrictionRejectReason(request, out string tamingMobRestrictionRejectReason))
            {
                return EquipmentChangeResult.Reject(tamingMobRestrictionRejectReason);
            }

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return EquipmentChangeResult.Reject("Inventory runtime is unavailable.");
            }

            if (!TryResolveLiveCompanionSourceItem(request, out CompanionEquipItem liveItem, out string rejectReason))
            {
                return EquipmentChangeResult.Reject(rejectReason);
            }

            if (TryGetCompanionCashOwnershipRejectReason(
                    liveItem,
                    build,
                    ResolveLoginRosterAccountId(),
                    out string ownershipRejectReason))
            {
                return EquipmentChangeResult.Reject(ownershipRejectReason);
            }

            InventoryType inventoryType = liveItem.IsCash ? InventoryType.CASH : InventoryType.EQUIP;
            if (!inventoryWindow.CanAcceptItem(inventoryType, liveItem.ItemId, 1, maxStackSize: 1))
            {
                string inventoryLabel = inventoryType == InventoryType.CASH ? "cash" : "equipment";
                return EquipmentChangeResult.Reject($"There is no free {inventoryLabel} inventory slot for this companion item.");
            }

            if (!TryUnequipLiveCompanionSourceItem(request, out CompanionEquipItem removedItem, out rejectReason))
            {
                return EquipmentChangeResult.Reject(rejectReason);
            }

            InventorySlotData returnedSlot = EquipUIBigBang.CreateInventorySlot(removedItem);
            return EquipmentChangeResult.Accept(
                displacedInventorySlots: returnedSlot == null
                    ? Array.Empty<InventorySlotData>()
                    : new[] { returnedSlot });
        }

        private EquipmentChangeResult HandleInventoryToPetCompanionChange(
            EquipmentChangeRequest request,
            CharacterBuild build,
            InventorySlotData sourceSlot,
            int? ownerAccountId)
        {
            PetRuntime targetPet = ResolvePetByRuntimeId(request.TargetPetRuntimeId);
            if (targetPet == null || _playerManager?.CompanionEquipment?.Pet == null)
            {
                return EquipmentChangeResult.Reject("Summon a pet before equipping pet accessories.");
            }

            _playerManager.CompanionEquipment.Pet.SetOwnerBuild(build);
            if (!_playerManager.CompanionEquipment.Pet.TryEquipItem(
                    targetPet,
                    request.ItemId,
                    out IReadOnlyList<CompanionEquipItem> displacedItems,
                    out string rejectReason,
                    sourceSlot,
                    ownerAccountId,
                    build?.Id ?? 0))
            {
                return EquipmentChangeResult.Reject(rejectReason);
            }

            return EquipmentChangeResult.Accept(displacedInventorySlots: EquipUIBigBang.CreateInventorySlots(displacedItems));
        }

        private EquipmentChangeResult HandleInventoryToDragonCompanionChange(
            EquipmentChangeRequest request,
            CharacterBuild build,
            InventorySlotData sourceSlot,
            int? ownerAccountId)
        {
            DragonEquipmentController controller = _playerManager?.CompanionEquipment?.Dragon;
            if (!request.TargetDragonSlot.HasValue || controller == null)
            {
                return EquipmentChangeResult.Reject("Drop this item on the matching dragon slot.");
            }

            controller.EnsureDefaults(build);
            if (!controller.TryEquipItem(
                    request.TargetDragonSlot.Value,
                    request.ItemId,
                    out IReadOnlyList<CompanionEquipItem> displacedItems,
                    out string rejectReason,
                    sourceSlot,
                    ownerAccountId,
                    build?.Id ?? 0))
            {
                return EquipmentChangeResult.Reject(rejectReason);
            }

            return EquipmentChangeResult.Accept(displacedInventorySlots: EquipUIBigBang.CreateInventorySlots(displacedItems));
        }

        private EquipmentChangeResult HandleInventoryToMechanicCompanionChange(
            EquipmentChangeRequest request,
            CharacterBuild build,
            InventorySlotData sourceSlot,
            int? ownerAccountId)
        {
            MechanicEquipmentController controller = _playerManager?.CompanionEquipment?.Mechanic;
            if (!request.TargetMechanicSlot.HasValue || controller == null)
            {
                return EquipmentChangeResult.Reject("Drop this item on the matching machine slot.");
            }

            controller.EnsureDefaults(build);
            if (!controller.TryEquipItem(
                    request.TargetMechanicSlot.Value,
                    request.ItemId,
                    out IReadOnlyList<CompanionEquipItem> displacedItems,
                    out string rejectReason,
                    sourceSlot,
                    ownerAccountId,
                    build?.Id ?? 0))
            {
                return EquipmentChangeResult.Reject(rejectReason);
            }

            return EquipmentChangeResult.Accept(displacedInventorySlots: EquipUIBigBang.CreateInventorySlots(displacedItems));
        }

        private EquipmentChangeResult HandleInventoryToAndroidCompanionChange(
            EquipmentChangeRequest request,
            CharacterBuild build,
            InventorySlotData sourceSlot,
            int? ownerAccountId)
        {
            AndroidEquipmentController controller = _playerManager?.CompanionEquipment?.Android;
            if (!request.TargetAndroidSlot.HasValue || controller == null)
            {
                return EquipmentChangeResult.Reject("Android equipment is unavailable.");
            }

            if (!controller.TryEquipItem(
                    request.TargetAndroidSlot.Value,
                    request.ItemId,
                    out IReadOnlyList<CompanionEquipItem> displacedItems,
                    out string rejectReason,
                    sourceSlot,
                    ownerAccountId,
                    build?.Id ?? 0))
            {
                return EquipmentChangeResult.Reject(rejectReason);
            }

            return EquipmentChangeResult.Accept(displacedInventorySlots: EquipUIBigBang.CreateInventorySlots(displacedItems));
        }

        private bool TryGetCompanionDisplacedCapacityRejectReason(
            EquipmentChangeRequest request,
            InventoryUI inventoryWindow,
            CharacterBuild build,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            Dictionary<InventoryType, int> displacedCounts = GetCompanionDisplacedInventoryCounts(request, build);
            if (displacedCounts.Count == 0)
            {
                return false;
            }

            foreach ((InventoryType inventoryType, int displacedCount) in displacedCounts)
            {
                if (displacedCount <= 0)
                {
                    continue;
                }

                int currentCount = inventoryWindow.GetSlots(inventoryType).Count;
                int availableSlots = Math.Max(0, inventoryWindow.GetSlotLimit(inventoryType) - currentCount);
                if (inventoryType == request.SourceInventoryType)
                {
                    availableSlots++;
                }

                if (availableSlots >= displacedCount)
                {
                    continue;
                }

                string inventoryLabel = inventoryType == InventoryType.CASH ? "cash" : "equipment";
                rejectReason = displacedCount == 1
                    ? $"There is no free {inventoryLabel} inventory slot for the displaced companion item."
                    : $"There are not enough free {inventoryLabel} inventory slots for the {displacedCount} displaced companion items.";
                return true;
            }

            return false;
        }

        private Dictionary<InventoryType, int> GetCompanionDisplacedInventoryCounts(EquipmentChangeRequest request, CharacterBuild build)
        {
            Dictionary<InventoryType, int> counts = new();
            if (request == null)
            {
                return counts;
            }

            switch (request.TargetCompanionKind)
            {
                case EquipmentChangeCompanionKind.Pet:
                {
                    PetRuntime targetPet = ResolvePetByRuntimeId(request.TargetPetRuntimeId);
                    if (targetPet != null
                        && _playerManager?.CompanionEquipment?.Pet != null
                        && _playerManager.CompanionEquipment.Pet.TryGetItem(targetPet, out CompanionEquipItem item))
                    {
                        AddDisplacedInventoryCount(counts, item);
                    }

                    break;
                }
                case EquipmentChangeCompanionKind.Dragon:
                {
                    if (request.TargetDragonSlot.HasValue
                        && _playerManager?.CompanionEquipment?.Dragon != null
                        && _playerManager.CompanionEquipment.Dragon.TryGetItem(request.TargetDragonSlot.Value, out CompanionEquipItem item))
                    {
                        AddDisplacedInventoryCount(counts, item);
                    }

                    break;
                }
                case EquipmentChangeCompanionKind.Mechanic:
                {
                    if (request.TargetMechanicSlot.HasValue
                        && _playerManager?.CompanionEquipment?.Mechanic != null
                        && _playerManager.CompanionEquipment.Mechanic.TryGetItem(request.TargetMechanicSlot.Value, out CompanionEquipItem item))
                    {
                        AddDisplacedInventoryCount(counts, item);
                    }

                    break;
                }
                case EquipmentChangeCompanionKind.Android:
                {
                    if (request.TargetAndroidSlot.HasValue
                        && _playerManager?.CompanionEquipment?.Android != null)
                    {
                        AndroidEquipmentController controller = _playerManager.CompanionEquipment.Android;
                        if (controller.TryGetItem(request.TargetAndroidSlot.Value, out CompanionEquipItem item))
                        {
                            AddDisplacedInventoryCount(counts, item);
                        }

                        if (request.RequestedPart?.Slot == EquipSlot.Longcoat
                            && controller.TryGetItem(AndroidEquipSlot.Pants, out CompanionEquipItem pantsItem))
                        {
                            AddDisplacedInventoryCount(counts, pantsItem);
                        }
                    }

                    break;
                }
            }

            return counts;
        }

        private bool TryGetAndroidCompanionRestrictionRejectReason(EquipmentChangeRequest request, out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                return false;
            }

            bool touchesAndroidEquipment =
                request.TargetCompanionKind == EquipmentChangeCompanionKind.Android
                || request.SourceCompanionKind == EquipmentChangeCompanionKind.Android
                || IsAndroidEquipmentSlot(request.TargetEquipSlot)
                || IsAndroidEquipmentSlot(request.SourceEquipSlot)
                || IsAndroidEquipmentSlot(request.RequestedPart?.Slot);
            if (!touchesAndroidEquipment)
            {
                return false;
            }

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            rejectReason = FieldInteractionRestrictionEvaluator.GetAndroidRestrictionMessage(fieldLimit);
            return !string.IsNullOrWhiteSpace(rejectReason);
        }

        private static bool IsAndroidEquipmentSlot(EquipSlot? slot)
        {
            return slot is EquipSlot.Android or EquipSlot.AndroidHeart;
        }

        private bool TryGetTamingMobEquipmentRestrictionRejectReason(EquipmentChangeRequest request, out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                return false;
            }

            bool touchesTamingMobEquipment =
                request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                || request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic
                || IsTamingMobEquipmentSlot(request.TargetEquipSlot)
                || IsTamingMobEquipmentSlot(request.SourceEquipSlot)
                || IsTamingMobEquipmentSlot(request.RequestedPart?.Slot);
            if (!touchesTamingMobEquipment)
            {
                return false;
            }

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            rejectReason = FieldInteractionRestrictionEvaluator.GetTamingMobRestrictionMessage(fieldLimit);
            return !string.IsNullOrWhiteSpace(rejectReason);
        }

        private static bool IsTamingMobEquipmentSlot(EquipSlot? slot)
        {
            return slot is EquipSlot.TamingMob or EquipSlot.Saddle;
        }

        private static void AddDisplacedInventoryCount(Dictionary<InventoryType, int> counts, CompanionEquipItem item)
        {
            if (item == null)
            {
                return;
            }

            InventoryType inventoryType = item.IsCash ? InventoryType.CASH : InventoryType.EQUIP;
            counts[inventoryType] = counts.TryGetValue(inventoryType, out int existing)
                ? existing + 1
                : 1;
        }

        private static bool TryGetCompanionCashOwnershipRejectReason(
            InventorySlotData sourceSlot,
            CharacterBuild build,
            int? accountId,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            if (sourceSlot == null)
            {
                return false;
            }

            CharacterPart tooltipPart = sourceSlot.TooltipPart;
            bool isCashCompanion = sourceSlot.PreferredInventoryType == InventoryType.CASH
                                   || sourceSlot.IsCashOwnershipLocked
                                   || (tooltipPart?.IsCash ?? false);
            if (!isCashCompanion)
            {
                return false;
            }

            bool isAccountSharable = tooltipPart?.IsAccountSharable ?? false;
            bool hasAccountShareTag = tooltipPart?.HasAccountShareTag ?? false;
            int ownerCharacterId = sourceSlot.OwnerCharacterId ?? tooltipPart?.OwnerCharacterId ?? 0;
            int ownerAccountId = sourceSlot.OwnerAccountId ?? tooltipPart?.OwnerAccountId ?? 0;
            int liveCharacterId = build?.Id ?? 0;
            int liveAccountId = accountId ?? 0;

            if (ownerAccountId > 0 && liveAccountId > 0 && ownerAccountId != liveAccountId)
            {
                rejectReason = "This companion cash item belongs to a different account.";
                return true;
            }

            if (ownerCharacterId > 0
                && liveCharacterId > 0
                && ownerCharacterId != liveCharacterId
                && !(isAccountSharable || hasAccountShareTag))
            {
                rejectReason = "This companion cash item belongs to a different character.";
                return true;
            }

            return false;
        }

        private static bool TryGetCompanionCashOwnershipRejectReason(
            CompanionEquipItem item,
            CharacterBuild build,
            int? accountId,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            if (item == null || !item.IsCash || !item.IsCashOwnershipLocked)
            {
                return false;
            }

            int ownerAccountId = item.OwnerAccountId ?? 0;
            int ownerCharacterId = item.OwnerCharacterId ?? 0;
            int liveCharacterId = build?.Id ?? 0;
            int liveAccountId = accountId ?? 0;

            if (ownerAccountId > 0 && liveAccountId > 0 && ownerAccountId != liveAccountId)
            {
                rejectReason = "This companion cash item belongs to a different account.";
                return true;
            }

            if (ownerCharacterId > 0
                && liveCharacterId > 0
                && ownerCharacterId != liveCharacterId
                && !(item.IsAccountSharable || item.HasAccountShareTag))
            {
                rejectReason = "This companion cash item belongs to a different character.";
                return true;
            }

            return false;
        }

        private PetRuntime ResolvePetByRuntimeId(int runtimeId)
        {
            IReadOnlyList<PetRuntime> pets = _playerManager?.Pets?.ActivePets;
            if (pets == null || runtimeId <= 0)
            {
                return null;
            }

            for (int i = 0; i < pets.Count; i++)
            {
                PetRuntime pet = pets[i];
                if (pet?.RuntimeId == runtimeId)
                {
                    return pet;
                }
            }

            return null;
        }

        private bool TryResolveLiveCompanionSourceItem(
            EquipmentChangeRequest request,
            out CompanionEquipItem item,
            out string rejectReason)
        {
            item = null;
            rejectReason = "The equipped companion item changed before the move request was accepted.";
            if (request == null)
            {
                return false;
            }

            switch (request.SourceCompanionKind)
            {
                case EquipmentChangeCompanionKind.Pet:
                {
                    PetRuntime pet = ResolvePetByRuntimeId(request.SourcePetRuntimeId);
                    return pet != null
                           && _playerManager?.CompanionEquipment?.Pet != null
                           && _playerManager.CompanionEquipment.Pet.TryGetItem(pet, out item)
                           && item?.ItemId == request.ItemId;
                }
                case EquipmentChangeCompanionKind.Dragon:
                    return request.SourceDragonSlot.HasValue
                           && _playerManager?.CompanionEquipment?.Dragon != null
                           && _playerManager.CompanionEquipment.Dragon.TryGetItem(request.SourceDragonSlot.Value, out item)
                           && item?.ItemId == request.ItemId;
                case EquipmentChangeCompanionKind.Mechanic:
                    return request.SourceMechanicSlot.HasValue
                           && _playerManager?.CompanionEquipment?.Mechanic != null
                           && _playerManager.CompanionEquipment.Mechanic.TryGetItem(request.SourceMechanicSlot.Value, out item)
                           && item?.ItemId == request.ItemId;
                case EquipmentChangeCompanionKind.Android:
                    return request.SourceAndroidSlot.HasValue
                           && _playerManager?.CompanionEquipment?.Android != null
                           && _playerManager.CompanionEquipment.Android.TryGetItem(request.SourceAndroidSlot.Value, out item)
                           && item?.ItemId == request.ItemId;
                default:
                    rejectReason = "The companion source slot is missing.";
                    return false;
            }
        }

        private bool TryUnequipLiveCompanionSourceItem(
            EquipmentChangeRequest request,
            out CompanionEquipItem item,
            out string rejectReason)
        {
            item = null;
            rejectReason = "The equipped companion item changed before the move request was accepted.";
            if (request == null)
            {
                return false;
            }

            switch (request.SourceCompanionKind)
            {
                case EquipmentChangeCompanionKind.Pet:
                {
                    PetRuntime pet = ResolvePetByRuntimeId(request.SourcePetRuntimeId);
                    return pet != null
                           && _playerManager?.CompanionEquipment?.Pet != null
                           && _playerManager.CompanionEquipment.Pet.TryUnequipItem(pet, out item)
                           && item?.ItemId == request.ItemId;
                }
                case EquipmentChangeCompanionKind.Dragon:
                    return request.SourceDragonSlot.HasValue
                           && _playerManager?.CompanionEquipment?.Dragon != null
                           && _playerManager.CompanionEquipment.Dragon.TryUnequipItem(request.SourceDragonSlot.Value, out item)
                           && item?.ItemId == request.ItemId;
                case EquipmentChangeCompanionKind.Mechanic:
                    return request.SourceMechanicSlot.HasValue
                           && _playerManager?.CompanionEquipment?.Mechanic != null
                           && _playerManager.CompanionEquipment.Mechanic.TryUnequipItem(request.SourceMechanicSlot.Value, out item)
                           && item?.ItemId == request.ItemId;
                case EquipmentChangeCompanionKind.Android:
                    return request.SourceAndroidSlot.HasValue
                           && _playerManager?.CompanionEquipment?.Android != null
                           && _playerManager.CompanionEquipment.Android.TryUnequipItem(request.SourceAndroidSlot.Value, out item)
                           && item?.ItemId == request.ItemId;
                default:
                    rejectReason = "The companion source slot is missing.";
                    return false;
            }
        }

        private CharacterPart ResolveRequestedEquipmentPart(EquipmentChangeRequest request, InventorySlotData liveSlot)
        {
            CharacterPart requestPart = request?.RequestedPart?.Clone();
            if (requestPart != null)
            {
                return requestPart;
            }

            CharacterPart liveTooltipPart = liveSlot?.TooltipPart?.Clone();
            if (liveTooltipPart != null)
            {
                return liveTooltipPart;
            }

            return _playerManager?.Loader?.LoadEquipment(request?.ItemId ?? 0);
        }
    }
}
