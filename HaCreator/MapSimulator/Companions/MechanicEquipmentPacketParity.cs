using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Companions
{
    internal enum MechanicEquipPacketPayloadMode : byte
    {
        Snapshot = 0,
        SlotMutation = 1,
        ClearAll = 2,
        ResetDefaults = 3,
        AuthorityRequest = 4,
        AuthorityResult = 5
    }

    internal enum MechanicEquipAuthorityResultKind : byte
    {
        Reject = 0,
        LocalRequestAccept = 1,
        SnapshotAccept = 2,
        SlotMutationAccept = 3,
        ClearAllAccept = 4,
        ResetDefaultsAccept = 5
    }

    internal readonly record struct MechanicEquipPacketPayload(
        MechanicEquipPacketPayloadMode Mode,
        MechanicEquipSlot? Slot,
        int ItemId,
        IReadOnlyDictionary<MechanicEquipSlot, int> SnapshotItems,
        int RequestId = 0,
        int RequestedAtTick = 0,
        EquipmentChangeRequestKind RequestKind = default,
        EquipmentChangeOwnerKind OwnerKind = default,
        int OwnerSessionId = 0,
        int ExpectedCharacterId = 0,
        int ExpectedBuildStateToken = 0,
        int ExpectedMechanicStateToken = 0,
        InventoryType SourceInventoryType = InventoryType.NONE,
        int SourceInventoryIndex = -1,
        MechanicEquipSlot? TargetMechanicSlot = null,
        MechanicEquipSlot? SourceMechanicSlot = null,
        MechanicEquipAuthorityResultKind AuthorityResultKind = default,
        int ResolvedBuildStateToken = 0,
        int ResolvedMechanicStateToken = 0,
        string RejectReason = null);

    internal static class MechanicEquipmentPacketParity
    {
        internal const int ClientChangeSlotPositionRequestOpcode = 77;
        internal const int ClientInventoryOperationPacketType = 28;
        internal const byte ClientEquipInventoryType = 1;
        internal const ushort ClientChangeSlotPositionCountAll = 0xFFFF;
        private const byte ItemSlotTypeEquip = 1;
        private const byte ItemSlotTypeBundle = 2;
        private const byte ItemSlotTypePet = 3;

        private readonly record struct MechanicInventoryOperationContext(
            bool SawPositiveEquipRemove,
            bool SawExpectedPositiveEquipRemove,
            bool SawNegativeEquipRemove,
            bool SawExpectedNegativeEquipRemove);
        internal readonly record struct MechanicInventoryOperationMutation(MechanicEquipSlot Slot, int ItemId);

        internal static bool TryEncodeClientChangeSlotPositionRequest(
            EquipmentChangeRequest request,
            out byte[] payload,
            out string rejectReason)
        {
            payload = Array.Empty<byte>();
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Mechanic equipment request is missing.";
                return false;
            }

            if (request.Kind != EquipmentChangeRequestKind.InventoryToCompanion
                || request.TargetCompanionKind != EquipmentChangeCompanionKind.Mechanic
                || !request.TargetMechanicSlot.HasValue)
            {
                rejectReason = "Only mechanic equip-in requests have a recovered retail ChangeSlotPosition body.";
                return false;
            }

            if (request.SourceInventoryIndex < 0)
            {
                rejectReason = "Mechanic retail ChangeSlotPosition request is missing a source inventory slot.";
                return false;
            }

            if (request.SourceInventoryType != InventoryType.EQUIP)
            {
                rejectReason = "Mechanic retail ChangeSlotPosition request only supports the equip inventory.";
                return false;
            }

            int sourceSlotPosition = request.SourceInventoryIndex + 1;
            if (sourceSlotPosition > ushort.MaxValue)
            {
                rejectReason = "Mechanic retail ChangeSlotPosition source slot is outside the client packet range.";
                return false;
            }

            int bodyPart = MechanicEquipmentSlotMap.GetBodyPart(request.TargetMechanicSlot.Value);
            if (bodyPart < 1100 || bodyPart > 1104)
            {
                rejectReason = "Mechanic retail ChangeSlotPosition target body part is invalid.";
                return false;
            }

            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write(request.RequestedAtTick);
            writer.Write(ClientEquipInventoryType);
            writer.Write((ushort)sourceSlotPosition);
            writer.Write(ToClientEquipPosition(bodyPart));
            writer.Write(ClientChangeSlotPositionCountAll);
            payload = stream.ToArray();
            return true;
        }

        internal static ushort ToClientEquipPosition(int bodyPart)
        {
            return unchecked((ushort)-bodyPart);
        }

        internal static bool TryRecognizeClientInventoryOperationCompletion(
            EquipmentChangeRequest request,
            IReadOnlyList<byte> payload,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Mechanic equipment request is missing.";
                return false;
            }

            bool isInventoryToMechanicRequest =
                request.Kind == EquipmentChangeRequestKind.InventoryToCompanion
                && request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                && request.TargetMechanicSlot.HasValue;
            bool isMechanicToInventoryRequest =
                request.Kind == EquipmentChangeRequestKind.CompanionToInventory
                && request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic
                && request.SourceMechanicSlot.HasValue;
            if (!isInventoryToMechanicRequest && !isMechanicToInventoryRequest)
            {
                rejectReason = "Only mechanic equip-in or drag-back-out requests can be recognized from inventory-operation payloads.";
                return false;
            }

            if (payload == null || payload.Count < sizeof(byte) * 2)
            {
                rejectReason = "Inventory-operation payload is missing the exclusive-reset and operation-count bytes.";
                return false;
            }

            string lastRemoveMismatchReason = null;
            try
            {
                byte[] buffer = payload as byte[] ?? new List<byte>(payload).ToArray();
                using MemoryStream stream = new(buffer, writable: false);
                using BinaryReader reader = new(stream);
                _ = reader.ReadByte(); // bExclRequestSent reset marker
                int operationCount = reader.ReadByte();
                if (operationCount <= 0)
                {
                    rejectReason = "Inventory-operation payload did not include any item move operation.";
                    return false;
                }

                MechanicInventoryOperationContext operationContext = default;
                bool requiresSecondaryStatChangedPointTrailer = false;
                bool sawMatchingSwap = false;
                bool sawMatchingAddEntry = false;
                bool sawMatchingRemoveEntry = false;
                for (int i = 0; i < operationCount; i++)
                {
                    if (stream.Length - stream.Position < sizeof(byte) * 2 + sizeof(short))
                    {
                        rejectReason = "Inventory-operation payload ended before a full operation header could be decoded.";
                        return false;
                    }

                    byte operationMode = reader.ReadByte();
                    byte inventoryType = reader.ReadByte();
                    short fromPosition = reader.ReadInt16();
                    switch (operationMode)
                    {
                        case 2:
                        {
                            if (stream.Length - stream.Position < sizeof(short))
                            {
                                rejectReason = "Inventory-operation swap entry is truncated.";
                                return false;
                            }

                            short toPosition = reader.ReadInt16();
                            requiresSecondaryStatChangedPointTrailer = requiresSecondaryStatChangedPointTrailer
                                || ShouldRequireSecondaryStatChangedPointTrailer(
                                    inventoryType,
                                    fromPosition,
                                    toPosition);
                            if (TryMatchesMechanicInventoryOperationSwap(request, inventoryType, fromPosition, toPosition, out rejectReason))
                            {
                                sawMatchingSwap = true;
                            }

                            break;
                        }
                        case 1:
                            if (stream.Length - stream.Position < sizeof(short))
                            {
                                rejectReason = "Inventory-operation quantity update entry is truncated.";
                                return false;
                            }

                            _ = reader.ReadInt16();
                            break;
                        case 3:
                            requiresSecondaryStatChangedPointTrailer = requiresSecondaryStatChangedPointTrailer
                                || ShouldRequireSecondaryStatChangedPointTrailerForRemove(inventoryType, fromPosition);
                            operationContext = ObserveMechanicRemoveEntry(request, operationContext, inventoryType, fromPosition);
                            if (TryMatchesMechanicInventoryOperationRemove(request, inventoryType, fromPosition, out rejectReason))
                            {
                                sawMatchingRemoveEntry = true;
                            }
                            else if (!string.IsNullOrWhiteSpace(rejectReason))
                            {
                                lastRemoveMismatchReason = rejectReason;
                            }

                            break;
                        case 4:
                            if (stream.Length - stream.Position < sizeof(int))
                            {
                                rejectReason = "Inventory-operation consume-item entry is truncated.";
                                return false;
                            }

                            _ = reader.ReadInt32();
                            break;
                        case 0:
                        {
                            if (!TryReadClientInventoryOperationAddEntry(
                                    request,
                                    operationContext,
                                    inventoryType,
                                    fromPosition,
                                    reader,
                                    isLastOperation: i == operationCount - 1,
                                    remainingOperationCount: operationCount - i - 1,
                                    reservedTrailerBytes: requiresSecondaryStatChangedPointTrailer ? sizeof(byte) : 0,
                                    out int addedItemId,
                                    out bool matchedByHeader,
                                    out rejectReason))
                            {
                                return false;
                            }

                            if (matchedByHeader)
                            {
                                sawMatchingAddEntry = true;
                                break;
                            }

                            if (TryMatchesMechanicInventoryOperationAdd(
                                    request,
                                    operationContext,
                                    inventoryType,
                                    fromPosition,
                                    addedItemId,
                                    out rejectReason))
                            {
                                sawMatchingAddEntry = true;
                            }

                            break;
                        }
                        default:
                            // CWvsContext::OnInventoryOperation falls through unknown modes after
                            // decoding the shared operation header; keep scanning the remaining
                            // entries instead of rejecting mechanic completion eagerly.
                            break;
                    }
                }

                if (!TryConsumeClientInventoryOperationTrailer(
                        reader,
                        requiresSecondaryStatChangedPointTrailer,
                        out rejectReason))
                {
                    if (!IsRecoverableInventoryOperationTrailerReason(rejectReason)
                        || (!sawMatchingSwap && !sawMatchingAddEntry && !sawMatchingRemoveEntry))
                    {
                        return false;
                    }

                    // Preserve completion ownership when a matching mechanic move
                    // was already recovered and the trailing bytes stay outside the
                    // decoded trailer contract in this local seam.
                    rejectReason = null;
                    return true;
                }

                if (sawMatchingAddEntry
                    && !TryValidateMechanicAddEntrySourceEvidence(request, operationContext, out rejectReason))
                {
                    return false;
                }

                if (sawMatchingSwap || sawMatchingAddEntry || sawMatchingRemoveEntry)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                rejectReason = $"Inventory-operation payload could not be decoded: {ex.Message}";
                return false;
            }

            rejectReason = string.IsNullOrWhiteSpace(lastRemoveMismatchReason)
                ? "Inventory-operation payload did not include a mechanic add, remove, or swap entry matching the active request."
                : lastRemoveMismatchReason;
            return false;
        }

        internal static bool TryFindMatchingClientInventoryOperationCompletionRequest(
            IReadOnlyList<EquipmentChangeRequest> candidateRequests,
            IReadOnlyList<byte> payload,
            out EquipmentChangeRequest matchedRequest,
            out string rejectReason)
        {
            matchedRequest = null;
            rejectReason = null;
            if (candidateRequests == null || candidateRequests.Count == 0)
            {
                rejectReason = "Inventory-operation payload did not match an active mechanic packet-owned request.";
                return false;
            }

            string lastMismatchReason = null;
            for (int i = 0; i < candidateRequests.Count; i++)
            {
                EquipmentChangeRequest candidate = candidateRequests[i];
                if (candidate == null)
                {
                    continue;
                }

                if (TryRecognizeClientInventoryOperationCompletion(candidate, payload, out string candidateRejectReason))
                {
                    if (matchedRequest != null)
                    {
                        matchedRequest = null;
                        rejectReason = "Inventory-operation payload matched multiple active mechanic packet-owned requests.";
                        return false;
                    }

                    matchedRequest = candidate;
                    rejectReason = null;
                    continue;
                }

                if (IsMechanicInventoryOperationRequestMismatch(candidateRejectReason))
                {
                    lastMismatchReason = candidateRejectReason;
                    continue;
                }

                rejectReason = candidateRejectReason;
                return false;
            }

            if (matchedRequest != null)
            {
                rejectReason = null;
                return true;
            }

            rejectReason = !string.IsNullOrWhiteSpace(lastMismatchReason)
                ? lastMismatchReason
                : "Inventory-operation payload did not match an active mechanic packet-owned request.";
            return false;
        }

        internal static bool TryDecodePassiveClientInventoryOperationMutations(
            IReadOnlyList<byte> payload,
            IReadOnlyList<InventorySlotData> equipInventorySlots,
            out IReadOnlyList<MechanicInventoryOperationMutation> mutations,
            out string rejectReason)
        {
            mutations = Array.Empty<MechanicInventoryOperationMutation>();
            rejectReason = null;
            if (payload == null || payload.Count < sizeof(byte) * 2)
            {
                rejectReason = "Inventory-operation payload is missing the exclusive-reset and operation-count bytes.";
                return false;
            }

            try
            {
                byte[] buffer = payload as byte[] ?? new List<byte>(payload).ToArray();
                using MemoryStream stream = new(buffer, writable: false);
                using BinaryReader reader = new(stream);
                _ = reader.ReadByte();
                int operationCount = reader.ReadByte();
                if (operationCount <= 0)
                {
                    rejectReason = "Inventory-operation payload did not include any operation entry.";
                    return false;
                }

                Dictionary<MechanicEquipSlot, int> recoveredMutations = new();
                bool requiresSecondaryStatChangedPointTrailer = false;
                bool terminatedAfterHeader = false;
                for (int i = 0; i < operationCount; i++)
                {
                    if (stream.Length - stream.Position < sizeof(byte) * 2 + sizeof(short))
                    {
                        rejectReason = "Inventory-operation payload ended before a full operation header could be decoded.";
                        return false;
                    }

                    byte operationMode = reader.ReadByte();
                    byte inventoryType = reader.ReadByte();
                    short fromPosition = reader.ReadInt16();
                    switch (operationMode)
                    {
                        case 0:
                        {
                            if (!TryReadPassiveClientInventoryOperationAddEntry(
                                    inventoryType,
                                    fromPosition,
                                    reader,
                                    isLastOperation: i == operationCount - 1,
                                    remainingOperationCount: operationCount - i - 1,
                                    reservedTrailerBytes: requiresSecondaryStatChangedPointTrailer ? sizeof(byte) : 0,
                                    out MechanicInventoryOperationMutation? passiveAddMutation,
                                    out bool terminateAfterHeader,
                                    out rejectReason))
                            {
                                if (recoveredMutations.Count > 0
                                    && IsPassiveAddEntryRecoveryTerminatorReason(rejectReason))
                                {
                                    terminatedAfterHeader = true;
                                    i = operationCount;
                                    break;
                                }

                                return false;
                            }

                            if (passiveAddMutation.HasValue)
                            {
                                recoveredMutations[passiveAddMutation.Value.Slot] = passiveAddMutation.Value.ItemId;
                            }

                            if (terminateAfterHeader)
                            {
                                terminatedAfterHeader = true;
                                i = operationCount;
                            }

                            break;
                        }
                        case 1:
                            if (stream.Length - stream.Position < sizeof(short))
                            {
                                rejectReason = "Inventory-operation quantity update entry is truncated.";
                                return false;
                            }

                            _ = reader.ReadInt16();
                            break;
                        case 2:
                        {
                            if (stream.Length - stream.Position < sizeof(short))
                            {
                                rejectReason = "Inventory-operation swap entry is truncated.";
                                return false;
                            }

                            short toPosition = reader.ReadInt16();
                            requiresSecondaryStatChangedPointTrailer = requiresSecondaryStatChangedPointTrailer
                                || ShouldRequireSecondaryStatChangedPointTrailer(
                                    inventoryType,
                                    fromPosition,
                                    toPosition);
                            if (inventoryType != ClientEquipInventoryType)
                            {
                                break;
                            }

                            bool sourceIsMechanic = TryResolveMechanicSlotFromClientPosition(fromPosition, out MechanicEquipSlot sourceMechanicSlot);
                            bool targetIsMechanic = TryResolveMechanicSlotFromClientPosition(toPosition, out MechanicEquipSlot targetMechanicSlot);
                            if (sourceIsMechanic && !targetIsMechanic)
                            {
                                recoveredMutations[sourceMechanicSlot] = 0;
                                break;
                            }

                            if (!sourceIsMechanic && targetIsMechanic)
                            {
                                if (!TryResolvePassiveEquipInventoryItemId(equipInventorySlots, fromPosition, out int sourceItemId))
                                {
                                    // Keep scanning in case this payload also carries a mode-0
                                    // add entry for the same mechanic slot with the authoritative
                                    // item id in the shared header.
                                    break;
                                }

                                if (!TryValidateMechanicItemFamilyForSlot(sourceItemId, targetMechanicSlot, out rejectReason))
                                {
                                    return false;
                                }

                                recoveredMutations[targetMechanicSlot] = sourceItemId;
                            }

                            break;
                        }
                        case 3:
                            requiresSecondaryStatChangedPointTrailer = requiresSecondaryStatChangedPointTrailer
                                || ShouldRequireSecondaryStatChangedPointTrailerForRemove(inventoryType, fromPosition);
                            if (inventoryType == ClientEquipInventoryType
                                && TryResolveMechanicSlotFromClientPosition(fromPosition, out MechanicEquipSlot removedSlot))
                            {
                                recoveredMutations[removedSlot] = 0;
                            }

                            break;
                        case 4:
                            if (stream.Length - stream.Position < sizeof(int))
                            {
                                rejectReason = "Inventory-operation consume-item entry is truncated.";
                                return false;
                            }

                            _ = reader.ReadInt32();
                            break;
                    }
                }

                if (!terminatedAfterHeader
                    && !TryConsumeClientInventoryOperationTrailer(
                        reader,
                        requiresSecondaryStatChangedPointTrailer,
                        out rejectReason))
                {
                    if (!(recoveredMutations.Count > 0
                          && IsRecoverableInventoryOperationTrailerReason(rejectReason)))
                    {
                        return false;
                    }

                    // Keep passive ownership recoverable when a mechanic mutation is
                    // already known but the tail contains unrecovered extra bytes.
                    rejectReason = null;
                }

                if (recoveredMutations.Count == 0)
                {
                    rejectReason = "Inventory-operation payload did not include a mechanic mutation that can be recovered passively.";
                    return false;
                }

                List<MechanicInventoryOperationMutation> mutationList = new(recoveredMutations.Count);
                foreach ((MechanicEquipSlot slot, int itemId) in recoveredMutations)
                {
                    mutationList.Add(new MechanicInventoryOperationMutation(slot, itemId));
                }

                mutations = mutationList;
                return true;
            }
            catch (Exception ex)
            {
                rejectReason = $"Inventory-operation payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        internal static bool TryRecognizeObservedLiveBridgeEquipInCompletion(
            EquipmentChangeRequest request,
            IReadOnlyDictionary<MechanicEquipSlot, int> beforeState,
            IReadOnlyDictionary<MechanicEquipSlot, int> currentState,
            IReadOnlyList<InventorySlotData> currentEquipInventory,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Mechanic equipment request is missing.";
                return false;
            }

            if (request.Kind == EquipmentChangeRequestKind.InventoryToCompanion
                && request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                && request.TargetMechanicSlot.HasValue)
            {
                if (currentEquipInventory == null)
                {
                    rejectReason = "Live equip inventory state is unavailable.";
                    return false;
                }

                int resolvedTargetItemId = TryGetSnapshotItem(currentState, request.TargetMechanicSlot.Value);
                if (resolvedTargetItemId != request.ItemId)
                {
                    rejectReason = "The live mechanic target slot does not yet contain the requested machine part.";
                    return false;
                }

                if (!TryValidateObservedLiveMechanicStateScope(
                        beforeState,
                        currentState,
                        request.TargetMechanicSlot.Value,
                        out rejectReason))
                {
                    return false;
                }

                if (request.SourceInventoryType != InventoryType.EQUIP
                    || request.SourceInventoryIndex < 0)
                {
                    rejectReason = "The live equip inventory no longer exposes the requested source slot.";
                    return false;
                }

                if (request.SourceInventoryIndex >= currentEquipInventory.Count)
                {
                    return true;
                }

                InventorySlotData liveSourceSlot = currentEquipInventory[request.SourceInventoryIndex];
                if (liveSourceSlot?.ItemId == request.ItemId)
                {
                    rejectReason = "The requested machine part is still sitting in the source equip inventory slot.";
                    return false;
                }

                return true;
            }

            if (request.Kind == EquipmentChangeRequestKind.CompanionToInventory
                && request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic
                && request.SourceMechanicSlot.HasValue)
            {
                MechanicEquipSlot sourceSlot = request.SourceMechanicSlot.Value;
                if (beforeState != null && TryGetSnapshotItem(beforeState, sourceSlot) != request.ItemId)
                {
                    rejectReason = "The live mechanic source slot no longer matched the requested machine part before recovery.";
                    return false;
                }

                if (TryGetSnapshotItem(currentState, sourceSlot) == request.ItemId)
                {
                    rejectReason = "The live mechanic source slot still contains the requested machine part.";
                    return false;
                }

                if (!TryValidateObservedLiveMechanicStateScope(
                        beforeState,
                        currentState,
                        sourceSlot,
                        out rejectReason))
                {
                    return false;
                }

                if (!TryContainsInventoryItem(currentEquipInventory, request.ItemId))
                {
                    rejectReason = "The live equip inventory does not yet contain the dragged-out mechanic machine part.";
                    return false;
                }

                return true;
            }

            rejectReason = "Only mechanic equip-in or drag-back-out requests can be recognized from live bridge state.";
            return false;
        }

        private static bool TryValidateObservedLiveMechanicStateScope(
            IReadOnlyDictionary<MechanicEquipSlot, int> beforeState,
            IReadOnlyDictionary<MechanicEquipSlot, int> currentState,
            MechanicEquipSlot allowedChangedSlot,
            out string rejectReason)
        {
            rejectReason = null;
            if (beforeState == null)
            {
                return true;
            }

            foreach (MechanicEquipSlot slot in Enum.GetValues<MechanicEquipSlot>())
            {
                if (slot == allowedChangedSlot)
                {
                    continue;
                }

                int beforeItemId = TryGetSnapshotItem(beforeState, slot);
                int currentItemId = TryGetSnapshotItem(currentState, slot);
                if (beforeItemId != currentItemId)
                {
                    rejectReason = "The live mechanic state changed outside the requested mechanic slot.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryContainsInventoryItem(
            IReadOnlyList<InventorySlotData> inventorySlots,
            int itemId)
        {
            if (inventorySlots == null || itemId <= 0)
            {
                return false;
            }

            for (int i = 0; i < inventorySlots.Count; i++)
            {
                if (inventorySlots[i]?.ItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        internal static byte[] EncodeAuthorityRequestPayload(EquipmentChangeRequest request)
        {
            if (request == null)
            {
                return Array.Empty<byte>();
            }

            return EncodePayload(new MechanicEquipPacketPayload(
                MechanicEquipPacketPayloadMode.AuthorityRequest,
                null,
                request.ItemId,
                null,
                request.RequestId,
                request.RequestedAtTick,
                request.Kind,
                request.OwnerKind,
                request.OwnerSessionId,
                request.ExpectedCharacterId,
                request.ExpectedBuildStateToken,
                request.ExpectedMechanicStateToken,
                request.SourceInventoryType,
                request.SourceInventoryIndex,
                request.TargetMechanicSlot,
                request.SourceMechanicSlot));
        }

        internal static byte[] EncodePayload(MechanicEquipPacketPayload payload)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)payload.Mode);
            switch (payload.Mode)
            {
                case MechanicEquipPacketPayloadMode.Snapshot:
                    WriteSnapshotItems(writer, payload.SnapshotItems);
                    break;
                case MechanicEquipPacketPayloadMode.SlotMutation:
                    WriteMechanicSlot(writer, payload.Slot);
                    writer.Write(payload.ItemId);
                    break;
                case MechanicEquipPacketPayloadMode.ClearAll:
                case MechanicEquipPacketPayloadMode.ResetDefaults:
                    break;
                case MechanicEquipPacketPayloadMode.AuthorityRequest:
                    writer.Write(payload.RequestId);
                    writer.Write(payload.RequestedAtTick);
                    writer.Write((byte)payload.RequestKind);
                    writer.Write((byte)payload.OwnerKind);
                    writer.Write(payload.OwnerSessionId);
                    writer.Write(payload.ExpectedCharacterId);
                    writer.Write(payload.ExpectedBuildStateToken);
                    writer.Write(payload.ExpectedMechanicStateToken);
                    writer.Write(payload.ItemId);
                    writer.Write((byte)payload.SourceInventoryType);
                    writer.Write(payload.SourceInventoryIndex);
                    WriteOptionalMechanicSlot(writer, payload.TargetMechanicSlot);
                    WriteOptionalMechanicSlot(writer, payload.SourceMechanicSlot);
                    break;
                case MechanicEquipPacketPayloadMode.AuthorityResult:
                    writer.Write(payload.RequestId);
                    writer.Write(payload.RequestedAtTick);
                    writer.Write((byte)payload.AuthorityResultKind);
                    writer.Write(payload.ResolvedBuildStateToken);
                    writer.Write(payload.ResolvedMechanicStateToken);
                    switch (payload.AuthorityResultKind)
                    {
                        case MechanicEquipAuthorityResultKind.Reject:
                            writer.Write(payload.RejectReason ?? string.Empty);
                            break;
                        case MechanicEquipAuthorityResultKind.LocalRequestAccept:
                        case MechanicEquipAuthorityResultKind.ClearAllAccept:
                        case MechanicEquipAuthorityResultKind.ResetDefaultsAccept:
                            break;
                        case MechanicEquipAuthorityResultKind.SnapshotAccept:
                            WriteSnapshotItems(writer, payload.SnapshotItems);
                            break;
                        case MechanicEquipAuthorityResultKind.SlotMutationAccept:
                            WriteMechanicSlot(writer, payload.Slot);
                            writer.Write(payload.ItemId);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported mechanic authority result kind '{payload.AuthorityResultKind}'.");
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported mechanic payload mode '{payload.Mode}'.");
            }

            return stream.ToArray();
        }

        internal static bool TryDecodePayload(
            byte[] payload,
            out MechanicEquipPacketPayload decodedPayload,
            out string errorMessage)
        {
            decodedPayload = default;
            errorMessage = null;
            if (payload == null || payload.Length == 0)
            {
                errorMessage =
                    "Mechanic equipment payload is missing. Use modes 0-3 for direct mechanic state, 4 for an authority request, or 5 for an authority result.";
                return false;
            }

            if (TryDecodeLegacyStatePayloadWithoutModePrefix(payload, out decodedPayload))
            {
                return true;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                MechanicEquipPacketPayloadMode mode = (MechanicEquipPacketPayloadMode)reader.ReadByte();
                switch (mode)
                {
                    case MechanicEquipPacketPayloadMode.Snapshot:
                    {
                        if (stream.Length - stream.Position != sizeof(int) * 5)
                        {
                            errorMessage = "Mechanic snapshot payload must contain five Int32 item ids ordered as ENGINE, FRAME, TRANS, ARM, LEG.";
                            return false;
                        }

                        decodedPayload = new MechanicEquipPacketPayload(
                            mode,
                            null,
                            0,
                            ReadSnapshotItems(reader));
                        return true;
                    }
                    case MechanicEquipPacketPayloadMode.SlotMutation:
                    {
                        long slotMutationLength = stream.Length - stream.Position;
                        if (slotMutationLength != sizeof(int) * 2
                            && slotMutationLength != sizeof(byte) + sizeof(int))
                        {
                            errorMessage = "Mechanic slot-mutation payload must contain a client body-part Int32 followed by an Int32 item id. Legacy one-byte slot ids are still accepted. Use item id 0 to clear that slot.";
                            return false;
                        }

                        if (!TryReadMechanicSlot(reader, slotMutationLength == sizeof(byte) + sizeof(int), out MechanicEquipSlot slot, out errorMessage))
                        {
                            return false;
                        }

                        decodedPayload = new MechanicEquipPacketPayload(
                            mode,
                            slot,
                            reader.ReadInt32(),
                            null);
                        return true;
                    }
                    case MechanicEquipPacketPayloadMode.ClearAll:
                    case MechanicEquipPacketPayloadMode.ResetDefaults:
                    {
                        if (stream.Position != stream.Length)
                        {
                            errorMessage = mode == MechanicEquipPacketPayloadMode.ClearAll
                                ? "Mechanic clear-all payload should not contain extra bytes."
                                : "Mechanic reset-defaults payload should not contain extra bytes.";
                            return false;
                        }

                        decodedPayload = new MechanicEquipPacketPayload(mode, null, 0, null);
                        return true;
                    }
                    case MechanicEquipPacketPayloadMode.AuthorityRequest:
                    {
                        const long clientAuthorityRequestLength =
                            sizeof(int) * 10
                            + sizeof(byte) * 3;
                        const long legacyAuthorityRequestLength =
                            sizeof(int) * 8
                            + sizeof(byte) * 5;
                        long authorityRequestLength = stream.Length - stream.Position;
                        if (authorityRequestLength != clientAuthorityRequestLength
                            && authorityRequestLength != legacyAuthorityRequestLength)
                        {
                            errorMessage =
                                "Mechanic authority-request payload must contain request id, requested tick, request kind, owner kind, owner session id, expected character id, build token, mechanic token, item id, source inventory type/index, target client body part, and source client body part.";
                            return false;
                        }

                        int requestId = reader.ReadInt32();
                        int requestedAtTick = reader.ReadInt32();
                        byte requestKindValue = reader.ReadByte();
                        if (!Enum.IsDefined(typeof(EquipmentChangeRequestKind), (int)requestKindValue))
                        {
                            errorMessage = "Mechanic authority-request kind is invalid.";
                            return false;
                        }

                        EquipmentChangeRequestKind requestKind = (EquipmentChangeRequestKind)requestKindValue;
                        byte ownerKindValue = reader.ReadByte();
                        if (!Enum.IsDefined(typeof(EquipmentChangeOwnerKind), (int)ownerKindValue))
                        {
                            errorMessage = "Mechanic authority-request owner kind is invalid.";
                            return false;
                        }

                        EquipmentChangeOwnerKind ownerKind = (EquipmentChangeOwnerKind)ownerKindValue;
                        int ownerSessionId = reader.ReadInt32();
                        int expectedCharacterId = reader.ReadInt32();
                        int expectedBuildStateToken = reader.ReadInt32();
                        int expectedMechanicStateToken = reader.ReadInt32();
                        int itemId = reader.ReadInt32();

                        byte sourceInventoryTypeValue = reader.ReadByte();
                        if (!Enum.IsDefined(typeof(InventoryType), (int)sourceInventoryTypeValue))
                        {
                            errorMessage = $"Mechanic authority-request source inventory type {sourceInventoryTypeValue} is invalid.";
                            return false;
                        }

                        int sourceInventoryIndex = reader.ReadInt32();
                        if (!TryReadOptionalMechanicSlot(reader, authorityRequestLength == legacyAuthorityRequestLength, out MechanicEquipSlot? targetSlot, out errorMessage)
                            || !TryReadOptionalMechanicSlot(reader, authorityRequestLength == legacyAuthorityRequestLength, out MechanicEquipSlot? sourceSlot, out errorMessage))
                        {
                            return false;
                        }

                        decodedPayload = new MechanicEquipPacketPayload(
                            mode,
                            null,
                            itemId,
                            null,
                            requestId,
                            requestedAtTick,
                            requestKind,
                            ownerKind,
                            ownerSessionId,
                            expectedCharacterId,
                            expectedBuildStateToken,
                            expectedMechanicStateToken,
                            (InventoryType)sourceInventoryTypeValue,
                            sourceInventoryIndex,
                            targetSlot,
                            sourceSlot);
                        return true;
                    }
                    case MechanicEquipPacketPayloadMode.AuthorityResult:
                    {
                        if (stream.Length - stream.Position < sizeof(int) * 4 + sizeof(byte))
                        {
                            errorMessage =
                                "Mechanic authority-result payload must contain request id, requested tick, result kind, build token, and mechanic token.";
                            return false;
                        }

                        int requestId = reader.ReadInt32();
                        int requestedAtTick = reader.ReadInt32();
                        byte resultKindValue = reader.ReadByte();
                        if (!Enum.IsDefined(typeof(MechanicEquipAuthorityResultKind), (int)resultKindValue))
                        {
                            errorMessage = $"Mechanic authority-result kind {resultKindValue} is invalid.";
                            return false;
                        }

                        MechanicEquipAuthorityResultKind resultKind = (MechanicEquipAuthorityResultKind)resultKindValue;
                        int resolvedBuildStateToken = reader.ReadInt32();
                        int resolvedMechanicStateToken = reader.ReadInt32();

                        switch (resultKind)
                        {
                            case MechanicEquipAuthorityResultKind.Reject:
                                if (stream.Position > stream.Length)
                                {
                                    errorMessage = "Mechanic authority rejection payload is truncated.";
                                    return false;
                                }

                                decodedPayload = new MechanicEquipPacketPayload(
                                    mode,
                                    null,
                                    0,
                                    null,
                                    requestId,
                                    requestedAtTick,
                                    ResolvedBuildStateToken: resolvedBuildStateToken,
                                    ResolvedMechanicStateToken: resolvedMechanicStateToken,
                                    AuthorityResultKind: resultKind,
                                    RejectReason: reader.ReadMapleString());
                                return stream.Position == stream.Length
                                    ? true
                                    : FailWithTrailingBytes("Mechanic authority rejection payload should not contain extra bytes.", out decodedPayload, out errorMessage);
                            case MechanicEquipAuthorityResultKind.LocalRequestAccept:
                            {
                                if (stream.Position != stream.Length)
                                {
                                    errorMessage = "Mechanic local-request authority acceptance should not contain extra bytes.";
                                    return false;
                                }

                                decodedPayload = new MechanicEquipPacketPayload(
                                    mode,
                                    null,
                                    0,
                                    null,
                                    requestId,
                                    requestedAtTick,
                                    AuthorityResultKind: resultKind,
                                    ResolvedBuildStateToken: resolvedBuildStateToken,
                                    ResolvedMechanicStateToken: resolvedMechanicStateToken);
                                return true;
                            }
                            case MechanicEquipAuthorityResultKind.SnapshotAccept:
                            {
                                if (stream.Length - stream.Position != sizeof(int) * 5)
                                {
                                    errorMessage = "Mechanic snapshot authority acceptance must contain five Int32 item ids ordered as ENGINE, FRAME, TRANS, ARM, LEG.";
                                    return false;
                                }

                                decodedPayload = new MechanicEquipPacketPayload(
                                    mode,
                                    null,
                                    0,
                                    ReadSnapshotItems(reader),
                                    requestId,
                                    requestedAtTick,
                                    AuthorityResultKind: resultKind,
                                    ResolvedBuildStateToken: resolvedBuildStateToken,
                                    ResolvedMechanicStateToken: resolvedMechanicStateToken);
                                return true;
                            }
                            case MechanicEquipAuthorityResultKind.SlotMutationAccept:
                            {
                                long slotMutationLength = stream.Length - stream.Position;
                                if (slotMutationLength != sizeof(int) * 2
                                    && slotMutationLength != sizeof(byte) + sizeof(int))
                                {
                                    errorMessage = "Mechanic slot-mutation authority acceptance must contain a client body-part Int32 followed by an Int32 item id. Legacy one-byte slot ids are still accepted.";
                                    return false;
                                }

                                if (!TryReadMechanicSlot(reader, slotMutationLength == sizeof(byte) + sizeof(int), out MechanicEquipSlot slot, out errorMessage))
                                {
                                    return false;
                                }

                                decodedPayload = new MechanicEquipPacketPayload(
                                    mode,
                                    slot,
                                    reader.ReadInt32(),
                                    null,
                                    requestId,
                                    requestedAtTick,
                                    AuthorityResultKind: resultKind,
                                    ResolvedBuildStateToken: resolvedBuildStateToken,
                                    ResolvedMechanicStateToken: resolvedMechanicStateToken);
                                return true;
                            }
                            case MechanicEquipAuthorityResultKind.ClearAllAccept:
                            case MechanicEquipAuthorityResultKind.ResetDefaultsAccept:
                            {
                                if (stream.Position != stream.Length)
                                {
                                    errorMessage = resultKind == MechanicEquipAuthorityResultKind.ClearAllAccept
                                        ? "Mechanic clear-all authority acceptance should not contain extra bytes."
                                        : "Mechanic reset-defaults authority acceptance should not contain extra bytes.";
                                    return false;
                                }

                                decodedPayload = new MechanicEquipPacketPayload(
                                    mode,
                                    null,
                                    0,
                                    null,
                                    requestId,
                                    requestedAtTick,
                                    AuthorityResultKind: resultKind,
                                    ResolvedBuildStateToken: resolvedBuildStateToken,
                                    ResolvedMechanicStateToken: resolvedMechanicStateToken);
                                return true;
                            }
                            default:
                                errorMessage = $"Mechanic authority-result kind {resultKindValue} is unsupported.";
                                return false;
                        }
                    }
                    default:
                        errorMessage = $"Mechanic equipment payload mode {(byte)mode} is unsupported.";
                        return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Mechanic equipment payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeLegacyStatePayloadWithoutModePrefix(
            byte[] payload,
            out MechanicEquipPacketPayload decodedPayload)
        {
            decodedPayload = default;
            if (payload == null)
            {
                return false;
            }

            if (payload.Length == sizeof(int) * 5)
            {
                using MemoryStream snapshotStream = new(payload, writable: false);
                using BinaryReader snapshotReader = new(snapshotStream);
                decodedPayload = new MechanicEquipPacketPayload(
                    MechanicEquipPacketPayloadMode.Snapshot,
                    null,
                    0,
                    ReadSnapshotItems(snapshotReader));
                return true;
            }

            if (payload.Length == sizeof(int) * 2)
            {
                using MemoryStream slotMutationStream = new(payload, writable: false);
                using BinaryReader slotMutationReader = new(slotMutationStream);
                int bodyPart = slotMutationReader.ReadInt32();
                if (!MechanicEquipmentSlotMap.TryResolveBodyPart(bodyPart, out MechanicEquipSlot slot))
                {
                    return false;
                }

                int itemId = slotMutationReader.ReadInt32();
                decodedPayload = new MechanicEquipPacketPayload(
                    MechanicEquipPacketPayloadMode.SlotMutation,
                    slot,
                    itemId,
                    null);
                return true;
            }

            if (payload.Length == sizeof(byte) + sizeof(int))
            {
                using MemoryStream legacySlotMutationStream = new(payload, writable: false);
                using BinaryReader legacySlotMutationReader = new(legacySlotMutationStream);
                byte legacySlotValue = legacySlotMutationReader.ReadByte();
                if (!Enum.IsDefined(typeof(MechanicEquipSlot), (int)legacySlotValue))
                {
                    return false;
                }

                int itemId = legacySlotMutationReader.ReadInt32();
                decodedPayload = new MechanicEquipPacketPayload(
                    MechanicEquipPacketPayloadMode.SlotMutation,
                    (MechanicEquipSlot)legacySlotValue,
                    itemId,
                    null);
                return true;
            }

            return false;
        }

        internal static bool TryReadFinalItemIdForSlot(
            MechanicEquipPacketPayload payload,
            IReadOnlyDictionary<MechanicEquipSlot, int> currentItems,
            MechanicEquipSlot slot,
            out int itemId,
            out string errorMessage)
        {
            itemId = 0;
            errorMessage = null;
            int currentItemId = 0;
            currentItems?.TryGetValue(slot, out currentItemId);
            switch (payload.AuthorityResultKind)
            {
                case MechanicEquipAuthorityResultKind.SnapshotAccept:
                    if (payload.SnapshotItems == null || !payload.SnapshotItems.TryGetValue(slot, out itemId))
                    {
                        itemId = 0;
                    }

                    return true;
                case MechanicEquipAuthorityResultKind.SlotMutationAccept:
                    itemId = payload.Slot == slot ? payload.ItemId : currentItemId;
                    return true;
                case MechanicEquipAuthorityResultKind.ClearAllAccept:
                    itemId = 0;
                    return true;
                case MechanicEquipAuthorityResultKind.ResetDefaultsAccept:
                    errorMessage = "Reset-defaults authority results are not compatible with single-slot mechanic equipment requests.";
                    return false;
                default:
                    errorMessage = "Mechanic authority payload does not expose a final mechanic slot state.";
                    return false;
            }
        }

        internal static bool HasExplicitAuthorityState(MechanicEquipPacketPayload payload)
        {
            return payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.SnapshotAccept
                   || payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.SlotMutationAccept
                   || payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.ClearAllAccept
                   || payload.AuthorityResultKind == MechanicEquipAuthorityResultKind.ResetDefaultsAccept;
        }

        internal static bool TryTranslateStatePayloadToAuthorityResult(
            MechanicEquipPacketPayload payload,
            int requestId,
            int requestedAtTick,
            out MechanicEquipPacketPayload authorityPayload,
            out string rejectReason)
        {
            authorityPayload = default;
            rejectReason = null;
            if (requestId <= 0)
            {
                rejectReason = "Mechanic authority translation requires a pending request id.";
                return false;
            }

            if (requestedAtTick <= 0)
            {
                rejectReason = "Mechanic authority translation requires the original request timestamp.";
                return false;
            }

            switch (payload.Mode)
            {
                case MechanicEquipPacketPayloadMode.Snapshot:
                    authorityPayload = new MechanicEquipPacketPayload(
                        MechanicEquipPacketPayloadMode.AuthorityResult,
                        null,
                        0,
                        payload.SnapshotItems,
                        requestId,
                        requestedAtTick,
                        AuthorityResultKind: MechanicEquipAuthorityResultKind.SnapshotAccept);
                    return true;
                case MechanicEquipPacketPayloadMode.SlotMutation:
                    if (!payload.Slot.HasValue)
                    {
                        rejectReason = "Mechanic slot-mutation payload is missing the target mechanic slot.";
                        return false;
                    }

                    authorityPayload = new MechanicEquipPacketPayload(
                        MechanicEquipPacketPayloadMode.AuthorityResult,
                        payload.Slot,
                        payload.ItemId,
                        null,
                        requestId,
                        requestedAtTick,
                        AuthorityResultKind: MechanicEquipAuthorityResultKind.SlotMutationAccept);
                    return true;
                case MechanicEquipPacketPayloadMode.ClearAll:
                    authorityPayload = new MechanicEquipPacketPayload(
                        MechanicEquipPacketPayloadMode.AuthorityResult,
                        null,
                        0,
                        null,
                        requestId,
                        requestedAtTick,
                        AuthorityResultKind: MechanicEquipAuthorityResultKind.ClearAllAccept);
                    return true;
                case MechanicEquipPacketPayloadMode.ResetDefaults:
                    authorityPayload = new MechanicEquipPacketPayload(
                        MechanicEquipPacketPayloadMode.AuthorityResult,
                        null,
                        0,
                        null,
                        requestId,
                        requestedAtTick,
                        AuthorityResultKind: MechanicEquipAuthorityResultKind.ResetDefaultsAccept);
                    return true;
                case MechanicEquipPacketPayloadMode.AuthorityRequest:
                case MechanicEquipPacketPayloadMode.AuthorityResult:
                    rejectReason = "Mechanic authority translation only supports direct mechanic state payload modes 0-3.";
                    return false;
                default:
                    rejectReason = $"Mechanic authority translation does not support payload mode {(byte)payload.Mode}.";
                    return false;
            }
        }

        private static Dictionary<MechanicEquipSlot, int> ReadSnapshotItems(BinaryReader reader)
        {
            return new Dictionary<MechanicEquipSlot, int>
            {
                [MechanicEquipSlot.Engine] = reader.ReadInt32(),
                [MechanicEquipSlot.Frame] = reader.ReadInt32(),
                [MechanicEquipSlot.Transistor] = reader.ReadInt32(),
                [MechanicEquipSlot.Arm] = reader.ReadInt32(),
                [MechanicEquipSlot.Leg] = reader.ReadInt32()
            };
        }

        private static void WriteSnapshotItems(BinaryWriter writer, IReadOnlyDictionary<MechanicEquipSlot, int> snapshotItems)
        {
            writer.Write(TryGetSnapshotItem(snapshotItems, MechanicEquipSlot.Engine));
            writer.Write(TryGetSnapshotItem(snapshotItems, MechanicEquipSlot.Frame));
            writer.Write(TryGetSnapshotItem(snapshotItems, MechanicEquipSlot.Transistor));
            writer.Write(TryGetSnapshotItem(snapshotItems, MechanicEquipSlot.Arm));
            writer.Write(TryGetSnapshotItem(snapshotItems, MechanicEquipSlot.Leg));
        }

        private static int TryGetSnapshotItem(IReadOnlyDictionary<MechanicEquipSlot, int> snapshotItems, MechanicEquipSlot slot)
        {
            return snapshotItems != null && snapshotItems.TryGetValue(slot, out int itemId)
                ? itemId
                : 0;
        }

        private static void WriteMechanicSlot(BinaryWriter writer, MechanicEquipSlot? slot)
        {
            if (!slot.HasValue)
            {
                throw new InvalidOperationException("Mechanic payload requires a concrete mechanic slot.");
            }

            writer.Write(MechanicEquipmentSlotMap.GetBodyPart(slot.Value));
        }

        private static void WriteOptionalMechanicSlot(BinaryWriter writer, MechanicEquipSlot? slot)
        {
            writer.Write(slot.HasValue ? MechanicEquipmentSlotMap.GetBodyPart(slot.Value) : 0);
        }

        private static bool TryReadMechanicSlot(BinaryReader reader, out MechanicEquipSlot slot, out string errorMessage)
        {
            return TryReadMechanicSlot(reader, legacySlotByte: false, out slot, out errorMessage);
        }

        private static bool TryReadMechanicSlot(BinaryReader reader, bool legacySlotByte, out MechanicEquipSlot slot, out string errorMessage)
        {
            slot = default;
            errorMessage = null;
            if (legacySlotByte)
            {
                byte slotValue = reader.ReadByte();
                if (!Enum.IsDefined(typeof(MechanicEquipSlot), (int)slotValue))
                {
                    errorMessage = $"Mechanic slot value {slotValue} is invalid.";
                    return false;
                }

                slot = (MechanicEquipSlot)slotValue;
                return true;
            }

            int bodyPart = reader.ReadInt32();
            if (!MechanicEquipmentSlotMap.TryResolveBodyPart(bodyPart, out slot))
            {
                errorMessage = $"Mechanic body-part value {bodyPart} is invalid.";
                return false;
            }

            return true;
        }

        private static bool TryReadOptionalMechanicSlot(BinaryReader reader, bool legacySlotByte, out MechanicEquipSlot? slot, out string errorMessage)
        {
            slot = null;
            errorMessage = null;
            if (legacySlotByte)
            {
                byte slotValue = reader.ReadByte();
                if (slotValue == byte.MaxValue)
                {
                    return true;
                }

                if (!Enum.IsDefined(typeof(MechanicEquipSlot), (int)slotValue))
                {
                    errorMessage = $"Mechanic slot value {slotValue} is invalid.";
                    return false;
                }

                slot = (MechanicEquipSlot)slotValue;
                return true;
            }

            int bodyPart = reader.ReadInt32();
            if (bodyPart == 0)
            {
                return true;
            }

            if (!MechanicEquipmentSlotMap.TryResolveBodyPart(bodyPart, out MechanicEquipSlot resolvedSlot))
            {
                errorMessage = $"Mechanic body-part value {bodyPart} is invalid.";
                return false;
            }

            slot = resolvedSlot;
            return true;
        }

        private static bool FailWithTrailingBytes(
            string message,
            out MechanicEquipPacketPayload decodedPayload,
            out string errorMessage)
        {
            decodedPayload = default;
            errorMessage = message;
            return false;
        }

        private static bool TryMatchesMechanicInventoryOperationSwap(
            EquipmentChangeRequest request,
            byte inventoryType,
            short sourcePosition,
            short targetPosition,
            out string rejectReason)
        {
            rejectReason = null;
            if (request.Kind == EquipmentChangeRequestKind.InventoryToCompanion)
            {
                if (inventoryType != ClientEquipInventoryType)
                {
                    rejectReason = "Mechanic equip-in inventory-operation swap did not target the equip inventory.";
                    return false;
                }

                if (request.SourceInventoryIndex < 0 || !request.TargetMechanicSlot.HasValue)
                {
                    rejectReason = "Mechanic equip-in request is missing source slot or target mechanic slot metadata.";
                    return false;
                }

                short expectedSourcePosition = (short)(request.SourceInventoryIndex + 1);
                short expectedTargetPosition =
                    unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(request.TargetMechanicSlot.Value));
                if (sourcePosition != expectedSourcePosition || targetPosition != expectedTargetPosition)
                {
                    rejectReason = "Inventory-operation swap did not match the requested equip-in source/target positions.";
                    return false;
                }

                return true;
            }

            if (request.Kind == EquipmentChangeRequestKind.CompanionToInventory)
            {
                if (!request.SourceMechanicSlot.HasValue)
                {
                    rejectReason = "Mechanic drag-back-out request is missing source mechanic slot metadata.";
                    return false;
                }

                if (inventoryType != ClientEquipInventoryType)
                {
                    rejectReason = "Mechanic drag-back-out inventory-operation swap did not target the equip inventory.";
                    return false;
                }

                short expectedSourcePosition =
                    unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(request.SourceMechanicSlot.Value));
                if (sourcePosition != expectedSourcePosition)
                {
                    rejectReason = "Inventory-operation swap did not originate from the requested mechanic source slot.";
                    return false;
                }

                if (targetPosition <= 0)
                {
                    rejectReason = "Inventory-operation swap did not land in a positive inventory slot.";
                    return false;
                }

                return true;
            }

            rejectReason = "Unsupported mechanic request kind for inventory-operation swap matching.";
            return false;
        }

        private static bool TryMatchesMechanicInventoryOperationAdd(
            EquipmentChangeRequest request,
            MechanicInventoryOperationContext operationContext,
            byte inventoryType,
            short targetPosition,
            int addedItemId,
            out string rejectReason)
        {
            rejectReason = null;
            if (request.Kind == EquipmentChangeRequestKind.InventoryToCompanion)
            {
                if (inventoryType != ClientEquipInventoryType)
                {
                    rejectReason = "Mechanic equip-in inventory-operation add entry did not target the equip inventory.";
                    return false;
                }

                if (!request.TargetMechanicSlot.HasValue)
                {
                    rejectReason = "Mechanic equip-in request is missing target mechanic slot metadata.";
                    return false;
                }

                short expectedTargetPosition =
                    unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(request.TargetMechanicSlot.Value));
                if (targetPosition != expectedTargetPosition)
                {
                    rejectReason = "Inventory-operation add entry did not target the requested mechanic slot.";
                    return false;
                }

                if (addedItemId != request.ItemId)
                {
                    rejectReason = "Inventory-operation add entry did not carry the requested mechanic machine-part item id.";
                    return false;
                }

                if (!TryValidateMechanicItemFamilyForSlot(addedItemId, request.TargetMechanicSlot.Value, out rejectReason))
                {
                    return false;
                }

                if (!TryValidateMechanicAddEntrySourceEvidence(request, operationContext, out rejectReason))
                {
                    return false;
                }

                return true;
            }

            if (request.Kind == EquipmentChangeRequestKind.CompanionToInventory)
            {
                if (!request.SourceMechanicSlot.HasValue)
                {
                    rejectReason = "Mechanic drag-back-out request is missing source mechanic slot metadata.";
                    return false;
                }

                if (inventoryType != ClientEquipInventoryType)
                {
                    rejectReason = "Mechanic drag-back-out inventory-operation add entry did not target the equip inventory.";
                    return false;
                }

                if (targetPosition <= 0)
                {
                    rejectReason = "Inventory-operation add entry did not land in a positive inventory slot.";
                    return false;
                }

                if (addedItemId != request.ItemId)
                {
                    rejectReason = "Inventory-operation add entry did not carry the requested mechanic machine-part item id.";
                    return false;
                }

                if (!TryValidateMechanicItemFamilyForSlot(addedItemId, request.SourceMechanicSlot.Value, out rejectReason))
                {
                    return false;
                }

                if (!TryValidateMechanicAddEntrySourceEvidence(request, operationContext, out rejectReason))
                {
                    return false;
                }

                return true;
            }

            rejectReason = "Unsupported mechanic request kind for inventory-operation add matching.";
            return false;
        }

        private static bool TryMatchesMechanicInventoryOperationRemove(
            EquipmentChangeRequest request,
            byte inventoryType,
            short sourcePosition,
            out string rejectReason)
        {
            rejectReason = null;
            if (request?.Kind != EquipmentChangeRequestKind.CompanionToInventory)
            {
                return false;
            }

            if (request.SourceCompanionKind != EquipmentChangeCompanionKind.Mechanic
                || !request.SourceMechanicSlot.HasValue)
            {
                rejectReason = "Mechanic drag-back-out request is missing source mechanic slot metadata.";
                return false;
            }

            if (inventoryType != ClientEquipInventoryType)
            {
                rejectReason = "Mechanic drag-back-out inventory-operation remove did not target the equip inventory.";
                return false;
            }

            short expectedSourcePosition =
                unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(request.SourceMechanicSlot.Value));
            if (sourcePosition != expectedSourcePosition)
            {
                rejectReason = "Inventory-operation remove did not originate from the requested mechanic source slot.";
                return false;
            }

            return true;
        }

        private static MechanicInventoryOperationContext ObserveMechanicRemoveEntry(
            EquipmentChangeRequest request,
            MechanicInventoryOperationContext context,
            byte inventoryType,
            short sourcePosition)
        {
            if (inventoryType != ClientEquipInventoryType)
            {
                return context;
            }

            bool sawPositiveEquipRemove = context.SawPositiveEquipRemove || sourcePosition > 0;
            bool sawExpectedPositiveEquipRemove = context.SawExpectedPositiveEquipRemove;
            bool sawNegativeEquipRemove = context.SawNegativeEquipRemove || sourcePosition < 0;
            bool sawExpectedNegativeEquipRemove = context.SawExpectedNegativeEquipRemove;
            if (request?.Kind == EquipmentChangeRequestKind.InventoryToCompanion
                && request.TargetCompanionKind == EquipmentChangeCompanionKind.Mechanic
                && request.SourceInventoryIndex >= 0)
            {
                short expectedSourcePosition = (short)(request.SourceInventoryIndex + 1);
                sawExpectedPositiveEquipRemove = sawExpectedPositiveEquipRemove || sourcePosition == expectedSourcePosition;
            }
            else if (request?.Kind == EquipmentChangeRequestKind.CompanionToInventory
                     && request.SourceCompanionKind == EquipmentChangeCompanionKind.Mechanic
                     && request.SourceMechanicSlot.HasValue)
            {
                short expectedSourcePosition =
                    unchecked((short)-MechanicEquipmentSlotMap.GetBodyPart(request.SourceMechanicSlot.Value));
                sawExpectedNegativeEquipRemove = sawExpectedNegativeEquipRemove || sourcePosition == expectedSourcePosition;
            }

            return new MechanicInventoryOperationContext(
                sawPositiveEquipRemove,
                sawExpectedPositiveEquipRemove,
                sawNegativeEquipRemove,
                sawExpectedNegativeEquipRemove);
        }

        private static bool TryValidateMechanicAddEntrySourceEvidence(
            EquipmentChangeRequest request,
            MechanicInventoryOperationContext operationContext,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Mechanic request metadata is unavailable for add-entry source validation.";
                return false;
            }

            if (request.Kind == EquipmentChangeRequestKind.InventoryToCompanion)
            {
                if (operationContext.SawPositiveEquipRemove && !operationContext.SawExpectedPositiveEquipRemove)
                {
                    rejectReason = "Inventory-operation add entry did not include removal from the requested source equip slot.";
                    return false;
                }

                return true;
            }

            if (request.Kind == EquipmentChangeRequestKind.CompanionToInventory)
            {
                if (operationContext.SawNegativeEquipRemove && !operationContext.SawExpectedNegativeEquipRemove)
                {
                    rejectReason = "Inventory-operation add entry did not include removal from the requested mechanic source slot.";
                    return false;
                }

                return true;
            }

            rejectReason = "Unsupported mechanic request kind for add-entry source validation.";
            return false;
        }

        private static bool TryReadClientInventoryOperationAddEntry(
            EquipmentChangeRequest request,
            MechanicInventoryOperationContext operationContext,
            byte inventoryType,
            short targetPosition,
            BinaryReader reader,
            bool isLastOperation,
            int remainingOperationCount,
            int reservedTrailerBytes,
            out int itemId,
            out bool matchedByHeader,
            out string rejectReason)
        {
            itemId = 0;
            matchedByHeader = false;
            rejectReason = null;
            if (reader == null)
            {
                rejectReason = "Inventory-operation add entry reader is unavailable.";
                return false;
            }

            Stream stream = reader.BaseStream;
            if (!TryEnsureRemaining(stream, sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long), out rejectReason))
            {
                return false;
            }

            byte slotType = reader.ReadByte();
            itemId = reader.ReadInt32();
            bool hasCashSerial = reader.ReadByte() != 0;
            if (hasCashSerial)
            {
                if (!TryEnsureRemaining(stream, sizeof(long), out rejectReason))
                {
                    return false;
                }

                _ = reader.ReadInt64(); // liCashItemSN
            }

            _ = reader.ReadInt64(); // dateExpire
            bool matchedMechanicAddByHeader = TryMatchesMechanicInventoryOperationAdd(
                    request,
                    operationContext,
                    inventoryType,
                    targetPosition,
                    itemId,
                    out _);
            if (matchedMechanicAddByHeader
                && slotType != ItemSlotTypeEquip)
            {
                rejectReason = $"Mechanic inventory-operation add entry used non-equip GW_ItemSlotBase type {slotType}.";
                return false;
            }

            if (matchedMechanicAddByHeader)
            {
                matchedByHeader = true;
            }

            long itemBodyStart = stream.CanSeek
                ? stream.Position
                : -1;
            if (TryConsumeClientInventoryOperationAddEntryBody(
                    reader,
                    slotType,
                    itemId,
                    hasCashSerial,
                    out rejectReason))
            {
                return true;
            }

            if (itemBodyStart >= 0)
            {
                stream.Position = itemBodyStart;
            }

            if (matchedByHeader)
            {
                if (TryConsumeHeaderMatchedModeZeroFallbackBody(
                        reader,
                        isLastOperation,
                        remainingOperationCount,
                        reservedTrailerBytes,
                        out rejectReason))
                {
                    // CWvsContext::OnInventoryOperation first commits the shared mode-0
                    // header mutation and then descends into GW_ItemSlotBase::Decode.
                    // Preserve completion ownership when the header already proves this
                    // request and local deep item-body decode is unavailable.
                    rejectReason = null;
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadPassiveClientInventoryOperationAddEntry(
            byte inventoryType,
            short targetPosition,
            BinaryReader reader,
            bool isLastOperation,
            int remainingOperationCount,
            int reservedTrailerBytes,
            out MechanicInventoryOperationMutation? mutation,
            out bool terminateAfterHeader,
            out string rejectReason)
        {
            mutation = null;
            terminateAfterHeader = false;
            rejectReason = null;
            if (reader == null)
            {
                rejectReason = "Inventory-operation add entry reader is unavailable.";
                return false;
            }

            Stream stream = reader.BaseStream;
            if (!TryEnsureRemaining(stream, sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long), out rejectReason))
            {
                return false;
            }

            byte slotType = reader.ReadByte();
            int itemId = reader.ReadInt32();
            bool hasCashSerial = reader.ReadByte() != 0;
            if (hasCashSerial)
            {
                if (!TryEnsureRemaining(stream, sizeof(long), out rejectReason))
                {
                    return false;
                }

                _ = reader.ReadInt64(); // liCashItemSN
            }

            _ = reader.ReadInt64(); // dateExpire
            if (inventoryType == ClientEquipInventoryType
                && TryResolveMechanicSlotFromClientPosition(targetPosition, out MechanicEquipSlot mechanicSlot)
                && slotType == ItemSlotTypeEquip
                && itemId > 0)
            {
                if (!TryValidateMechanicItemFamilyForSlot(itemId, mechanicSlot, out rejectReason))
                {
                    return false;
                }

                mutation = new MechanicInventoryOperationMutation(mechanicSlot, itemId);
            }

            long itemBodyStart = stream.CanSeek
                ? stream.Position
                : -1;
            if (TryConsumeClientInventoryOperationAddEntryBody(
                    reader,
                    slotType,
                    itemId,
                    hasCashSerial,
                    out rejectReason))
            {
                return true;
            }

            if (itemBodyStart >= 0)
            {
                stream.Position = itemBodyStart;
            }

            if (mutation.HasValue)
            {
                if (!TryConsumeHeaderMatchedModeZeroFallbackBody(
                        reader,
                        isLastOperation,
                        remainingOperationCount,
                        reservedTrailerBytes,
                        out rejectReason))
                {
                    return false;
                }

                // Keep passive non-proxy ownership recoverable from the mode-0
                // header when the follow-up GW_ItemSlotBase body is unavailable.
                terminateAfterHeader = isLastOperation || remainingOperationCount <= 0;
                rejectReason = null;
                return true;
            }

            return false;
        }

        private static bool TryConsumeClientInventoryOperationAddEntryBody(
            BinaryReader reader,
            byte slotType,
            int itemId,
            bool hasCashSerial,
            out string rejectReason)
        {
            rejectReason = null;
            if (reader == null)
            {
                rejectReason = "Inventory-operation add entry reader is unavailable.";
                return false;
            }

            return slotType switch
            {
                ItemSlotTypeEquip => TryReadClientInventoryOperationEquipBody(reader, hasCashSerial, out rejectReason),
                ItemSlotTypeBundle => TryReadClientInventoryOperationBundleBody(reader, itemId, out rejectReason),
                ItemSlotTypePet => TryReadClientInventoryOperationPetBody(reader, out rejectReason),
                _ => FailUnsupportedItemSlotType(slotType, out rejectReason)
            };
        }

        private static bool TryConsumeHeaderMatchedModeZeroFallbackBody(
            BinaryReader reader,
            bool isLastOperation,
            int remainingOperationCount,
            int reservedTrailerBytes,
            out string rejectReason)
        {
            rejectReason = null;
            Stream stream = reader?.BaseStream;
            if (stream is not { CanSeek: true })
            {
                rejectReason = "Inventory-operation add entry body could not be skipped for header-matched mechanic ownership recovery.";
                return false;
            }

            long bodyEnd = stream.Length - Math.Max(0, reservedTrailerBytes);
            if (bodyEnd < stream.Position)
            {
                rejectReason = "Inventory-operation add entry is truncated before the expected trailer.";
                return false;
            }

            if (isLastOperation || remainingOperationCount <= 0)
            {
                stream.Position = bodyEnd;
                return true;
            }

            long bodyStart = stream.Position;
            if (TryPositionAtClientInventoryOperationSuffix(
                    reader,
                    bodyStart,
                    bodyEnd,
                    remainingOperationCount,
                    out long suffixStart)
                && suffixStart > bodyStart)
            {
                stream.Position = suffixStart;
                return true;
            }

            long bodyEndBeforeEquipTrailer = bodyEnd - sizeof(byte);
            if (bodyEndBeforeEquipTrailer > bodyStart
                && TryPositionAtClientInventoryOperationSuffix(
                    reader,
                    bodyStart,
                    bodyEndBeforeEquipTrailer,
                    remainingOperationCount,
                    out suffixStart)
                && suffixStart > bodyStart)
            {
                stream.Position = suffixStart;
                return true;
            }

            stream.Position = bodyStart;
            rejectReason = "Inventory-operation add entry body could not be decoded before later mechanic operations.";
            return false;
        }

        private static bool TryPositionAtClientInventoryOperationSuffix(
            BinaryReader reader,
            long searchStart,
            long searchEnd,
            int remainingOperationCount,
            out long suffixStart)
        {
            suffixStart = 0;
            Stream stream = reader?.BaseStream;
            if (stream is not { CanSeek: true } || remainingOperationCount <= 0)
            {
                return false;
            }

            for (long candidate = searchStart; candidate <= searchEnd; candidate++)
            {
                stream.Position = candidate;
                if (TryConsumeClientInventoryOperationSuffix(reader, remainingOperationCount, searchEnd))
                {
                    suffixStart = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryConsumeClientInventoryOperationSuffix(
            BinaryReader reader,
            int remainingOperationCount,
            long expectedEnd)
        {
            Stream stream = reader?.BaseStream;
            if (stream is not { CanSeek: true })
            {
                return false;
            }

            long start = stream.Position;
            try
            {
                for (int i = 0; i < remainingOperationCount; i++)
                {
                    if (stream.Length - stream.Position < sizeof(byte) * 2 + sizeof(short))
                    {
                        stream.Position = start;
                        return false;
                    }

                    byte operationMode = reader.ReadByte();
                    _ = reader.ReadByte();
                    _ = reader.ReadInt16();
                    switch (operationMode)
                    {
                        case 0:
                            if (!TryConsumeClientInventoryOperationAddSuffixBody(reader))
                            {
                                stream.Position = start;
                                return false;
                            }

                            break;
                        case 1:
                            if (!TrySkipClientInventoryOperationBytes(reader, sizeof(short)))
                            {
                                stream.Position = start;
                                return false;
                            }

                            break;
                        case 2:
                            if (!TrySkipClientInventoryOperationBytes(reader, sizeof(short)))
                            {
                                stream.Position = start;
                                return false;
                            }

                            break;
                        case 3:
                            break;
                        case 4:
                            if (!TrySkipClientInventoryOperationBytes(reader, sizeof(int)))
                            {
                                stream.Position = start;
                                return false;
                            }

                            break;
                        default:
                            break;
                    }
                }

                bool matchedEnd = stream.Position == expectedEnd;
                stream.Position = start;
                return matchedEnd;
            }
            catch (EndOfStreamException)
            {
                stream.Position = start;
                return false;
            }
            catch (IOException)
            {
                stream.Position = start;
                return false;
            }
        }

        private static bool TryConsumeClientInventoryOperationAddSuffixBody(BinaryReader reader)
        {
            Stream stream = reader?.BaseStream;
            if (stream is not { CanSeek: true })
            {
                return false;
            }

            long start = stream.Position;
            if (!TryEnsureRemaining(stream, sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long), out _))
            {
                stream.Position = start;
                return false;
            }

            byte slotType = reader.ReadByte();
            if (slotType is not ItemSlotTypeEquip and not ItemSlotTypeBundle and not ItemSlotTypePet)
            {
                stream.Position = start;
                return false;
            }

            int itemId = reader.ReadInt32();
            bool hasCashSerial = reader.ReadByte() != 0;
            if (hasCashSerial && !TrySkipClientInventoryOperationBytes(reader, sizeof(long)))
            {
                stream.Position = start;
                return false;
            }

            if (!TrySkipClientInventoryOperationBytes(reader, sizeof(long)))
            {
                stream.Position = start;
                return false;
            }

            bool consumed = TryConsumeClientInventoryOperationAddEntryBody(
                reader,
                slotType,
                itemId,
                hasCashSerial,
                out _);
            if (!consumed)
            {
                stream.Position = start;
            }

            return consumed;
        }

        private static bool TrySkipClientInventoryOperationBytes(BinaryReader reader, int byteCount)
        {
            Stream stream = reader?.BaseStream;
            if (!TryEnsureRemaining(stream, byteCount, out _))
            {
                return false;
            }

            stream.Position += byteCount;
            return true;
        }

        private static bool TryResolveMechanicSlotFromClientPosition(short position, out MechanicEquipSlot slot)
        {
            slot = default;
            if (position >= 0)
            {
                return false;
            }

            int bodyPart = -position;
            return MechanicEquipmentSlotMap.TryResolveBodyPart(bodyPart, out slot);
        }

        private static bool TryValidateMechanicItemFamilyForSlot(
            int itemId,
            MechanicEquipSlot targetSlot,
            out string rejectReason)
        {
            rejectReason = null;
            if (!CompanionEquipmentController.TryResolveMechanicSlot(itemId, out MechanicEquipSlot itemSlot))
            {
                rejectReason = "Inventory-operation add entry did not carry a Character/Mechanic machine-part item id.";
                return false;
            }

            if (itemSlot != targetSlot)
            {
                rejectReason = "Inventory-operation add entry carried a machine-part item id for a different mechanic slot family.";
                return false;
            }

            return true;
        }

        private static bool TryResolvePassiveEquipInventoryItemId(
            IReadOnlyList<InventorySlotData> equipInventorySlots,
            short sourcePosition,
            out int itemId)
        {
            itemId = 0;
            if (sourcePosition <= 0 || equipInventorySlots == null)
            {
                return false;
            }

            int sourceIndex = sourcePosition - 1;
            if (sourceIndex < 0 || sourceIndex >= equipInventorySlots.Count)
            {
                return false;
            }

            itemId = equipInventorySlots[sourceIndex]?.ItemId ?? 0;
            return itemId > 0;
        }

        private static bool TryReadClientInventoryOperationEquipBody(
            BinaryReader reader,
            bool hasCashSerial,
            out string rejectReason)
        {
            long entryStart = reader?.BaseStream?.Position ?? 0;
            if (TryReadClientInventoryOperationEquipBody(reader, hasCashSerial, statFieldCount: 15, out rejectReason))
            {
                return true;
            }

            if (reader?.BaseStream is not { CanSeek: true } stream)
            {
                return false;
            }

            stream.Position = entryStart;
            return TryReadClientInventoryOperationEquipBody(reader, hasCashSerial, statFieldCount: 14, out rejectReason);
        }

        private static bool TryReadClientInventoryOperationEquipBody(
            BinaryReader reader,
            bool hasCashSerial,
            int statFieldCount,
            out string rejectReason)
        {
            rejectReason = null;
            if (reader == null)
            {
                rejectReason = "Inventory-operation add entry reader is unavailable.";
                return false;
            }

            Stream stream = reader.BaseStream;
            // Client evidence: GW_ItemSlotEquip::RawDecode (0x4f8360) decodes a
            // 15-stat STR..JUMP block in v95. Keep a 14-stat fallback here to
            // preserve recovery when mixed retail captures omit one stat entry.
            const int equipStatHeaderByteLength = sizeof(byte) * 2;
            if (!TryEnsureRemaining(stream, equipStatHeaderByteLength + (sizeof(short) * statFieldCount), out rejectReason))
            {
                return false;
            }

            _ = reader.ReadByte();
            _ = reader.ReadByte();
            for (int i = 0; i < statFieldCount; i++)
            {
                _ = reader.ReadInt16();
            }

            if (!TryReadClientMapleString(reader, out string title, out rejectReason))
            {
                return false;
            }

            // The client decodes the full title body first and only truncates for display later.
            // Keep long title payloads valid so mode-0 mechanic add entries can be recovered
            // from retail packets without an artificial simulator-only length gate.

            const int equipTailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            if (!TryEnsureRemaining(stream, equipTailLength + (hasCashSerial ? 0 : sizeof(long)) + sizeof(long) + sizeof(int), out rejectReason))
            {
                return false;
            }

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
            if (!hasCashSerial)
            {
                _ = reader.ReadInt64(); // liSN for non-cash equips
            }

            _ = reader.ReadInt64(); // ftEquipped
            _ = reader.ReadInt32(); // nPrevBonusExpRate
            return true;
        }

        private static bool TryReadClientInventoryOperationBundleBody(
            BinaryReader reader,
            int itemId,
            out string rejectReason)
        {
            rejectReason = null;
            if (!TryEnsureRemaining(reader.BaseStream, sizeof(ushort), out rejectReason))
            {
                return false;
            }

            _ = reader.ReadUInt16(); // nNumber
            if (!TryReadClientMapleString(reader, out _, out rejectReason))
            {
                return false;
            }

            if (!TryEnsureRemaining(reader.BaseStream, sizeof(short), out rejectReason))
            {
                return false;
            }

            _ = reader.ReadInt16(); // nAttribute
            if ((itemId / 10000) is 207 or 233)
            {
                if (!TryEnsureRemaining(reader.BaseStream, sizeof(long), out rejectReason))
                {
                    return false;
                }

                _ = reader.ReadInt64(); // liSN
            }

            return true;
        }

        private static bool TryReadClientInventoryOperationPetBody(
            BinaryReader reader,
            out string rejectReason)
        {
            const int petBodyLength = 13 + sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
            if (!TryEnsureRemaining(reader.BaseStream, petBodyLength, out rejectReason))
            {
                return false;
            }

            _ = reader.ReadBytes(13); // sPetName[13]
            _ = reader.ReadByte();
            _ = reader.ReadInt16();
            _ = reader.ReadByte();
            _ = reader.ReadInt64();
            _ = reader.ReadInt16();
            _ = reader.ReadUInt16();
            _ = reader.ReadInt32();
            _ = reader.ReadInt16();
            return true;
        }

        private static bool TryReadClientMapleString(
            BinaryReader reader,
            out string value,
            out string rejectReason)
        {
            value = string.Empty;
            rejectReason = null;
            Stream stream = reader.BaseStream;
            if (!TryEnsureRemaining(stream, sizeof(short), out rejectReason))
            {
                return false;
            }

            short lengthToken = reader.ReadInt16();
            if (lengthToken == 0)
            {
                value = string.Empty;
                return true;
            }

            if (lengthToken > 0)
            {
                int byteLength = lengthToken;
                if (!TryEnsureRemaining(stream, byteLength, out rejectReason))
                {
                    return false;
                }

                value = Encoding.ASCII.GetString(reader.ReadBytes(byteLength));
                return true;
            }

            int charLength = -lengthToken;
            int unicodeByteLength = charLength * sizeof(char);
            if (charLength <= 0 || !TryEnsureRemaining(stream, unicodeByteLength, out rejectReason))
            {
                rejectReason = "Inventory-operation add entry maple string length is invalid.";
                return false;
            }

            value = Encoding.Unicode.GetString(reader.ReadBytes(unicodeByteLength));
            return true;
        }

        private static bool ShouldRequireSecondaryStatChangedPointTrailer(
            byte inventoryType,
            short sourcePosition,
            short targetPosition)
        {
            return inventoryType == ClientEquipInventoryType
                   && (sourcePosition < 0 || targetPosition < 0);
        }

        private static bool ShouldRequireSecondaryStatChangedPointTrailerForRemove(
            byte inventoryType,
            short sourcePosition)
        {
            return inventoryType == ClientEquipInventoryType
                   && sourcePosition < 0;
        }

        private static bool TryConsumeClientInventoryOperationTrailer(
            BinaryReader reader,
            bool requiresSecondaryStatChangedPointTrailer,
            out string rejectReason)
        {
            rejectReason = null;
            if (reader?.BaseStream == null)
            {
                rejectReason = "Inventory-operation payload stream is unavailable while decoding trailer data.";
                return false;
            }

            Stream stream = reader.BaseStream;
            long remainingBytes = stream.Length - stream.Position;
            if (requiresSecondaryStatChangedPointTrailer)
            {
                if (remainingBytes < sizeof(byte))
                {
                    rejectReason = "Inventory-operation payload is missing the equip secondary-stat changed-point trailer.";
                    return false;
                }

                _ = reader.ReadByte();
                remainingBytes -= sizeof(byte);
            }

            if (remainingBytes != 0)
            {
                rejectReason = "Inventory-operation payload contained unsupported trailing bytes.";
                return false;
            }

            return true;
        }

        private static bool TryEnsureRemaining(Stream stream, int byteCount, out string rejectReason)
        {
            rejectReason = null;
            if (stream == null)
            {
                rejectReason = "Inventory-operation add entry stream is unavailable.";
                return false;
            }

            if (byteCount < 0 || stream.Length - stream.Position < byteCount)
            {
                rejectReason = "Inventory-operation add entry is truncated.";
                return false;
            }

            return true;
        }

        private static bool FailUnsupportedItemSlotType(byte slotType, out string rejectReason)
        {
            rejectReason = $"Inventory-operation add entry used unsupported GW_ItemSlotBase type {slotType}.";
            return false;
        }

        private static bool IsPassiveAddEntryRecoveryTerminatorReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            return string.Equals(reason, "Inventory-operation add entry is truncated.", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Inventory-operation add entry used unsupported GW_ItemSlotBase type ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Inventory-operation add entry maple string length is invalid.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRecoverableInventoryOperationTrailerReason(string reason)
        {
            return string.Equals(
                reason,
                "Inventory-operation payload contained unsupported trailing bytes.",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMechanicInventoryOperationRequestMismatch(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            return reason.StartsWith("Inventory-operation swap did not ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Inventory-operation add entry did not ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Inventory-operation remove did not ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic equip-in inventory-operation ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic drag-back-out inventory-operation ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Only mechanic equip-in or drag-back-out requests ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic equip-in request is missing ", StringComparison.OrdinalIgnoreCase)
                   || reason.StartsWith("Mechanic drag-back-out request is missing ", StringComparison.OrdinalIgnoreCase);
        }
    }
}
