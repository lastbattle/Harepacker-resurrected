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
            return _localUtilityPacketOutbox.TrySendOutboundPacket(LocalUtilityPacketInboxManager.MechanicEquipStatePacketType, payload, out status);
        }

        private byte[] BuildMechanicEquipmentAuthorityRequestPayload(EquipmentChangeRequest request)
        {
            using System.IO.MemoryStream stream = new();
            using System.IO.BinaryWriter writer = new(stream);
            writer.Write((byte)PacketOwnedMechanicEquipPayloadMode.SlotMutation);
            writer.Write(request.RequestId);
            writer.Write(request.RequestedAtTick);
            writer.Write((byte)request.Kind);
            writer.Write((byte)request.OwnerKind);
            writer.Write(request.OwnerSessionId);
            writer.Write(request.ExpectedCharacterId);
            writer.Write(request.ExpectedBuildStateToken);
            writer.Write(request.ExpectedMechanicStateToken);
            writer.Write(request.ItemId);
            writer.Write((byte)request.SourceInventoryType);
            writer.Write(request.SourceInventoryIndex);
            writer.Write(request.TargetMechanicSlot.HasValue ? (byte)request.TargetMechanicSlot.Value : byte.MaxValue);
            writer.Write(request.SourceMechanicSlot.HasValue ? (byte)request.SourceMechanicSlot.Value : byte.MaxValue);
            return stream.ToArray();
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
