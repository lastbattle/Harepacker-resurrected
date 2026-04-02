using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator
{
    public partial class MapSimulator
    {
        private const int EquipmentChangeResponseDelayMs = 50;
        private int _nextEquipmentChangeRequestId = 1;
        private readonly Dictionary<int, PendingEquipmentChangeEnvelope> _pendingEquipmentChangeRequests = new();

        private sealed class PendingEquipmentChangeEnvelope
        {
            public EquipmentChangeRequest Request { get; init; }
            public int ReadyAtTick { get; init; }
        }

        private EquipmentChangeSubmission SubmitEquipmentChangeRequest(EquipmentChangeRequest request)
        {
            if (request == null)
            {
                return EquipmentChangeSubmission.Reject("Equipment change request is missing.");
            }

            if (_pendingEquipmentChangeRequests.Count > 0)
            {
                return EquipmentChangeSubmission.Reject("An equipment change is already pending.");
            }

            request.RequestId = GetNextEquipmentChangeRequestId();
            request.RequestedAtTick = currTickCount;
            _pendingEquipmentChangeRequests[request.RequestId] = new PendingEquipmentChangeEnvelope
            {
                Request = request,
                ReadyAtTick = currTickCount + EquipmentChangeResponseDelayMs
            };

            return EquipmentChangeSubmission.Accept(request.RequestId, request.RequestedAtTick);
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

        private EquipmentChangeResult TryResolveEquipmentChangeRequest(EquipmentChangeResolutionQuery resolutionQuery)
        {
            if (resolutionQuery == null
                || resolutionQuery.RequestId <= 0
                || !_pendingEquipmentChangeRequests.TryGetValue(resolutionQuery.RequestId, out PendingEquipmentChangeEnvelope pendingEnvelope))
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

            if (resolutionQuery.OwnerSessionId != request.OwnerSessionId
                || resolutionQuery.RequestedAtTick != request.RequestedAtTick)
            {
                _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);
                return EquipmentChangeResult.Reject("The equipment request session is no longer active.");
            }

            _pendingEquipmentChangeRequests.Remove(resolutionQuery.RequestId);

            CharacterBuild build = _playerManager?.Player?.Build;
            if (build == null)
            {
                return EquipmentChangeResult.Reject("No live character build is available for this equipment action.");
            }

            if (EquipmentChangeRequestValidator.TryGetRequestStateRejectReason(request, build, out string requestStateRejectReason))
            {
                return EquipmentChangeResult.Reject(requestStateRejectReason)
                    .WithCompletionMetadata(request.RequestId, request.RequestedAtTick, currTickCount, build.ComputeEquipmentStateToken());
            }

            EquipmentChangeResult result = request.Kind switch
            {
                EquipmentChangeRequestKind.InventoryToCharacter => HandleInventoryToCharacterChange(request, build),
                EquipmentChangeRequestKind.CharacterToCharacter => HandleCharacterToCharacterChange(request, build),
                EquipmentChangeRequestKind.CharacterToInventory => HandleCharacterToInventoryChange(request, build),
                _ => EquipmentChangeResult.Reject("Unsupported equipment change request.")
            };

            return result.WithCompletionMetadata(request.RequestId, request.RequestedAtTick, currTickCount, build.ComputeEquipmentStateToken());
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
