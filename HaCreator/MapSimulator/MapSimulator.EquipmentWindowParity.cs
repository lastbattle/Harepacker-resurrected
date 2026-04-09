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
        private const int MechanicEquipmentPacketAuthorityTimeoutMs = 350;
        private int _nextEquipmentChangeRequestId = 1;
        private int _lastEquipmentExclusiveRequestSentTick = int.MinValue;
        private readonly Dictionary<int, PendingEquipmentChangeEnvelope> _pendingEquipmentChangeRequests = new();
        private readonly Dictionary<int, EquipmentChangeResult> _pendingMechanicEquipmentPacketResults = new();

        private sealed class PendingEquipmentChangeEnvelope
        {
            public EquipmentChangeRequest Request { get; init; }
            public int ReadyAtTick { get; init; }
            public bool AwaitingMechanicPacketAuthority { get; set; }
            public int MechanicPacketAuthorityDeadlineAtTick { get; set; }
        }

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
            if (IsMechanicEquipmentRequest(request)
                && TryDispatchMechanicEquipmentAuthorityRequest(request, out _))
            {
                envelope.AwaitingMechanicPacketAuthority = true;
                envelope.MechanicPacketAuthorityDeadlineAtTick = currTickCount + MechanicEquipmentPacketAuthorityTimeoutMs;
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
            if (resolutionQuery == null
                || resolutionQuery.RequestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(resolutionQuery.RequestId, out PendingEquipmentChangeEnvelope pendingEnvelope))
            {
                return null;
            }

            if (_pendingMechanicEquipmentPacketResults.TryGetValue(resolutionQuery.RequestId, out EquipmentChangeResult packetResult))
            {
                _pendingMechanicEquipmentPacketResults.Remove(resolutionQuery.RequestId);
                _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);
                return packetResult;
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

        private bool IsMechanicEquipmentRequest(EquipmentChangeRequest request)
        {
            return request != null
                && (request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                    || request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic);
        }

        private bool TryDispatchMechanicEquipmentAuthorityRequest(EquipmentChangeRequest request, out string status)
        {
            status = "Mechanic equipment authority dispatch is unavailable.";
            if (!IsMechanicEquipmentRequest(request))
            {
                status = "Equipment request does not target the mechanic owner.";
                return false;
            }

            byte[] payload = BuildMechanicEquipmentAuthorityRequestPayload(request);
            const int opcode = LocalUtilityPacketInboxManager.MechanicEquipStatePacketType;
            string payloadHex = payload.Length > 0 ? Convert.ToHexString(payload) : "<empty>";
            string bridgeStatus = "Local utility official-session bridge is unavailable.";
            string outboxStatus = "Local utility packet outbox is unavailable.";

            if (_localUtilityOfficialSessionBridge.TrySendOutboundPacket(opcode, payload, out bridgeStatus))
            {
                status = $"Mirrored mechanic authority request {request.RequestId} through the live local-utility bridge as opcode {opcode} [{payloadHex}]. {bridgeStatus}";
                return true;
            }

            if (_localUtilityPacketOutbox.TrySendOutboundPacket(opcode, payload, out outboxStatus))
            {
                status =
                    $"Mirrored mechanic authority request {request.RequestId} through the generic local-utility outbox as opcode {opcode} [{payloadHex}] after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
                return true;
            }

            if (_localUtilityOfficialSessionBridge.IsRunning
                && _localUtilityOfficialSessionBridge.TryQueueOutboundPacket(opcode, payload, out string queuedBridgeStatus))
            {
                status =
                    $"Queued mechanic authority request {request.RequestId} for deferred official-session injection as opcode {opcode} [{payloadHex}] after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred bridge: {queuedBridgeStatus}";
                return true;
            }

            if (_localUtilityPacketOutbox.TryQueueOutboundPacket(opcode, payload, out string queuedOutboxStatus))
            {
                status =
                    $"Queued mechanic authority request {request.RequestId} for deferred generic local-utility delivery as opcode {opcode} [{payloadHex}] after the live bridge path was unavailable. Bridge: {bridgeStatus} Outbox: {outboxStatus} Deferred outbox: {queuedOutboxStatus}";
                return true;
            }

            status =
                $"Neither the live local-utility bridge nor the generic outbox accepted mechanic authority request {request.RequestId} as opcode {opcode} [{payloadHex}]. Bridge: {bridgeStatus} Outbox: {outboxStatus}";
            return false;
        }

        private byte[] BuildMechanicEquipmentAuthorityRequestPayload(EquipmentChangeRequest request)
        {
            return MechanicEquipmentPacketParity.EncodeAuthorityRequestPayload(request);
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

        private static bool TryValidatePacketOwnedMechanicAuthorityScope(
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

            if (request.SourceInventoryType != InventoryType.EQUIP)
            {
                return EquipmentChangeResult.Reject("Only equipment inventory entries can be equipped here.");
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

            if (uiWindowManager?.InventoryWindow is not InventoryUI inventoryWindow)
            {
                return EquipmentChangeResult.Reject("Inventory runtime is unavailable.");
            }

            InventoryType inventoryType = InventoryItemMetadataResolver.ResolveInventoryType(request.ItemId);
            if (inventoryType != InventoryType.EQUIP)
            {
                return EquipmentChangeResult.Reject("Only equipment items can return to the equipment inventory.");
            }

            if (!inventoryWindow.CanAcceptItem(inventoryType, request.ItemId, 1, maxStackSize: 1))
            {
                return EquipmentChangeResult.Reject("There is no free equipment inventory slot for this item.");
            }

            CharacterPart liveSourcePart = EquipSlotStateResolver.ResolveDisplayedPart(build, request.SourceEquipSlot.Value);
            if (liveSourcePart == null || liveSourcePart.ItemId != request.ItemId)
            {
                return EquipmentChangeResult.Reject("The equipped item changed before the unequip request was accepted.");
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

            bool touchesAndroidCompanion =
                request.TargetCompanionKind == EquipmentChangeCompanionKind.Android
                || request.SourceCompanionKind == EquipmentChangeCompanionKind.Android;
            if (!touchesAndroidCompanion)
            {
                return false;
            }

            long fieldLimit = _mapBoard?.MapInfo?.fieldLimit ?? 0;
            rejectReason = FieldInteractionRestrictionEvaluator.GetAndroidRestrictionMessage(fieldLimit);
            return !string.IsNullOrWhiteSpace(rejectReason);
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
