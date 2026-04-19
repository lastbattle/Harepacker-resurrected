using HaCreator.MapSimulator.Character;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    public static class CharacterEquipmentPacketParity
    {
        private const int MaxAuthoritySlotStateCount = 64;
        private const byte AuthorityResultOwnerSessionContextMarker = 0xEC;
        private const byte AuthorityResultInventoryStateContextMarker = 0xED;
        private const int MaxAuthorityInventorySlotStateCount = 192;
        private const byte ClientEquipInventoryType = (byte)InventoryType.EQUIP;
        private const byte ClientCashInventoryType = (byte)InventoryType.CASH;
        private const byte ItemSlotTypeEquip = 1;
        private const byte ItemSlotTypeBundle = 2;
        private const byte ItemSlotTypePet = 3;
        internal const int ClientInventoryOperationPacketType = 28;
        private readonly record struct CharacterInventoryOperationContext(
            bool SawPositiveEquipRemove,
            bool SawExpectedPositiveEquipRemove,
            bool SawNegativeEquipRemove,
            bool SawExpectedNegativeEquipRemove,
            bool SawExpectedTargetEquipRemove,
            bool SawExpectedTargetCashRemove);

        internal static bool TryRecognizeClientInventoryOperationCompletion(
            EquipmentChangeRequest request,
            IReadOnlyList<byte> payload,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Character equipment request is missing.";
                return false;
            }

            bool isCharacterRequest = request.Kind == EquipmentChangeRequestKind.InventoryToCharacter
                                      || request.Kind == EquipmentChangeRequestKind.CharacterToCharacter
                                      || request.Kind == EquipmentChangeRequestKind.CharacterToInventory;
            if (!isCharacterRequest)
            {
                rejectReason = "Only character equipment requests can be recognized from inventory-operation payloads.";
                return false;
            }

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
                bool sawMatchingAddEntry = false;
                bool sawMatchingSwap = false;
                bool sawDisplacedAddEntry = false;
                CharacterInventoryOperationContext operationContext = default;
                bool sawConflictingCharacterMutation = false;
                string conflictingCharacterMutationRejectReason = null;
                _ = reader.ReadByte(); // bExclRequestSent reset marker
                int operationCount = reader.ReadByte();
                if (operationCount <= 0)
                {
                    rejectReason = "Inventory-operation payload did not include any item move operation.";
                    return false;
                }

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
                            operationContext = ObserveCharacterInventoryOperationRemove(
                                request,
                                operationContext,
                                inventoryType,
                                fromPosition);
                            if (TryMatchesCharacterInventoryOperationSwap(
                                    request,
                                    inventoryType,
                                    fromPosition,
                                    toPosition,
                                    out rejectReason))
                            {
                                sawMatchingSwap = true;
                                break;
                            }

                            if (IsSupportedClientCharacterInventoryType(inventoryType))
                            {
                                sawConflictingCharacterMutation = true;
                                conflictingCharacterMutationRejectReason ??=
                                    string.IsNullOrWhiteSpace(rejectReason)
                                        ? "Inventory-operation payload mutated an unexpected character inventory swap while resolving the active request."
                                        : rejectReason;
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
                            operationContext = ObserveCharacterInventoryOperationRemove(
                                request,
                                operationContext,
                                inventoryType,
                                fromPosition);
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
                                    inventoryType,
                                    fromPosition,
                                    reader,
                                    out int addedItemId,
                                    out bool matchedByHeader,
                                    out rejectReason))
                            {
                                return false;
                            }

                            string addMismatchRejectReason = null;
                            bool isMatchingAddEntry = matchedByHeader
                                || TryMatchesCharacterInventoryOperationAdd(
                                    request,
                                    inventoryType,
                                    fromPosition,
                                    addedItemId,
                                    out addMismatchRejectReason);
                            if (isMatchingAddEntry)
                            {
                                sawMatchingAddEntry = true;
                            }
                            else if (TryMatchesExpectedCharacterDisplacedAdd(
                                         request,
                                         operationContext,
                                         inventoryType,
                                         fromPosition,
                                         addedItemId,
                                         sawDisplacedAddEntry,
                                         out string displacedAddRejectReason))
                            {
                                sawDisplacedAddEntry = true;
                            }
                            else if (IsSupportedClientCharacterInventoryType(inventoryType))
                            {
                                sawConflictingCharacterMutation = true;
                                conflictingCharacterMutationRejectReason ??=
                                    !string.IsNullOrWhiteSpace(displacedAddRejectReason)
                                        ? displacedAddRejectReason
                                        :
                                    string.IsNullOrWhiteSpace(addMismatchRejectReason)
                                        ? "Inventory-operation payload mutated an unexpected character inventory add entry while resolving the active request."
                                        : addMismatchRejectReason;
                            }

                            break;
                        }
                        default:
                            // CWvsContext::OnInventoryOperation falls through for unknown modes after reading
                            // the common operation header, so keep scanning instead of failing the completion lane.
                            break;
                    }
                }

                if (sawConflictingCharacterMutation)
                {
                    rejectReason = $"Inventory-operation payload mutated character inventory slots outside the active request: {conflictingCharacterMutationRejectReason}";
                    return false;
                }

                if (sawMatchingSwap)
                {
                    return true;
                }

                if (sawMatchingAddEntry)
                {
                    return TryValidateCharacterAddEntrySourceEvidence(
                        request,
                        operationContext,
                        out rejectReason);
                }
            }
            catch (Exception ex)
            {
                rejectReason = $"Inventory-operation payload could not be decoded: {ex.Message}";
                return false;
            }

            rejectReason = "Inventory-operation payload did not include a character-equipment add-or-swap entry matching the active request.";
            return false;
        }

        private static CharacterInventoryOperationContext ObserveCharacterInventoryOperationRemove(
            EquipmentChangeRequest request,
            CharacterInventoryOperationContext context,
            byte inventoryType,
            short sourcePosition)
        {
            if (request == null)
            {
                return context;
            }

            bool sawPositiveEquipRemove = context.SawPositiveEquipRemove;
            bool sawExpectedPositiveEquipRemove = context.SawExpectedPositiveEquipRemove;
            bool sawNegativeEquipRemove = context.SawNegativeEquipRemove;
            bool sawExpectedNegativeEquipRemove = context.SawExpectedNegativeEquipRemove;
            bool sawExpectedTargetEquipRemove = context.SawExpectedTargetEquipRemove;
            bool sawExpectedTargetCashRemove = context.SawExpectedTargetCashRemove;

            if (sourcePosition > 0)
            {
                sawPositiveEquipRemove = sawPositiveEquipRemove || IsSupportedClientCharacterInventoryType(inventoryType);
                if (request.Kind == EquipmentChangeRequestKind.InventoryToCharacter
                    && request.SourceInventoryIndex >= 0
                    && inventoryType == (byte)request.SourceInventoryType)
                {
                    short expectedSourcePosition = (short)(request.SourceInventoryIndex + 1);
                    sawExpectedPositiveEquipRemove = sawExpectedPositiveEquipRemove || sourcePosition == expectedSourcePosition;
                }

                return new CharacterInventoryOperationContext(
                    sawPositiveEquipRemove,
                    sawExpectedPositiveEquipRemove,
                    sawNegativeEquipRemove,
                    sawExpectedNegativeEquipRemove,
                    sawExpectedTargetEquipRemove,
                    sawExpectedTargetCashRemove);
            }

            if (sourcePosition < 0)
            {
                sawNegativeEquipRemove = sawNegativeEquipRemove || IsSupportedClientCharacterInventoryType(inventoryType);
                if ((request.Kind == EquipmentChangeRequestKind.CharacterToCharacter
                     || request.Kind == EquipmentChangeRequestKind.CharacterToInventory)
                    && request.SourceEquipSlot.HasValue
                    && IsExpectedCharacterSourceInventory(request, inventoryType))
                {
                    short expectedSourcePosition = ToClientEquipPosition(request.SourceEquipSlot.Value);
                    sawExpectedNegativeEquipRemove = sawExpectedNegativeEquipRemove || sourcePosition == expectedSourcePosition;
                }

                if ((request.Kind == EquipmentChangeRequestKind.InventoryToCharacter
                     || request.Kind == EquipmentChangeRequestKind.CharacterToCharacter)
                    && request.TargetEquipSlot.HasValue
                    && IsExpectedCharacterTargetInventory(request, inventoryType))
                {
                    short expectedTargetPosition = ToClientEquipPosition(request.TargetEquipSlot.Value);
                    if (sourcePosition == expectedTargetPosition)
                    {
                        if (inventoryType == ClientCashInventoryType)
                        {
                            sawExpectedTargetCashRemove = true;
                        }
                        else if (inventoryType == ClientEquipInventoryType)
                        {
                            sawExpectedTargetEquipRemove = true;
                        }
                    }
                }
            }

            return new CharacterInventoryOperationContext(
                sawPositiveEquipRemove,
                sawExpectedPositiveEquipRemove,
                sawNegativeEquipRemove,
                sawExpectedNegativeEquipRemove,
                sawExpectedTargetEquipRemove,
                sawExpectedTargetCashRemove);
        }

        private static bool TryValidateCharacterAddEntrySourceEvidence(
            EquipmentChangeRequest request,
            CharacterInventoryOperationContext operationContext,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null)
            {
                rejectReason = "Character request metadata is unavailable for add-entry source validation.";
                return false;
            }

            switch (request.Kind)
            {
                case EquipmentChangeRequestKind.InventoryToCharacter:
                    if (operationContext.SawPositiveEquipRemove && !operationContext.SawExpectedPositiveEquipRemove)
                    {
                        rejectReason = "Inventory-operation add entry did not include removal from the requested source inventory slot.";
                        return false;
                    }

                    if (!operationContext.SawExpectedPositiveEquipRemove)
                    {
                        rejectReason = "Inventory-operation add entry is missing source-slot removal for the requested equip-in operation.";
                        return false;
                    }

                    return true;
                case EquipmentChangeRequestKind.CharacterToCharacter:
                case EquipmentChangeRequestKind.CharacterToInventory:
                    if (operationContext.SawNegativeEquipRemove && !operationContext.SawExpectedNegativeEquipRemove)
                    {
                        rejectReason = "Inventory-operation add entry did not include removal from the requested character source slot.";
                        return false;
                    }

                    if (!operationContext.SawExpectedNegativeEquipRemove)
                    {
                        rejectReason = "Inventory-operation add entry is missing source-slot removal for the requested character equipment operation.";
                        return false;
                    }

                    return true;
                default:
                    rejectReason = "Unsupported character equipment request kind for add-entry source validation.";
                    return false;
            }
        }

        private static bool TryMatchesExpectedCharacterDisplacedAdd(
            EquipmentChangeRequest request,
            CharacterInventoryOperationContext operationContext,
            byte inventoryType,
            short targetPosition,
            int addedItemId,
            bool sawDisplacedAddEntry,
            out string rejectReason)
        {
            rejectReason = null;
            if (request == null || addedItemId <= 0)
            {
                return false;
            }

            switch (request.Kind)
            {
                case EquipmentChangeRequestKind.CharacterToCharacter:
                    if (!request.SourceEquipSlot.HasValue || !request.TargetEquipSlot.HasValue)
                    {
                        return false;
                    }

                    if (inventoryType != ClientEquipInventoryType)
                    {
                        return false;
                    }

                    if (targetPosition != ToClientEquipPosition(request.SourceEquipSlot.Value))
                    {
                        return false;
                    }

                    if (!operationContext.SawExpectedTargetEquipRemove)
                    {
                        rejectReason = "Inventory-operation add entry returned a displaced character slot item before the requested target slot was removed.";
                        return false;
                    }

                    if (sawDisplacedAddEntry)
                    {
                        rejectReason = "Inventory-operation payload returned duplicate displaced character-slot add entries for one move request.";
                        return false;
                    }

                    return true;
                case EquipmentChangeRequestKind.InventoryToCharacter:
                    if (!request.TargetEquipSlot.HasValue
                        || !IsSupportedClientCharacterInventoryType(inventoryType)
                        || targetPosition <= 0)
                    {
                        return false;
                    }

                    bool sawExpectedTargetRemove = inventoryType switch
                    {
                        ClientEquipInventoryType => operationContext.SawExpectedTargetEquipRemove,
                        ClientCashInventoryType => operationContext.SawExpectedTargetCashRemove,
                        _ => false
                    };
                    if (!sawExpectedTargetRemove)
                    {
                        rejectReason = "Inventory-operation add entry returned a displaced target-slot item before the requested character slot was removed.";
                        return false;
                    }

                    if (sawDisplacedAddEntry)
                    {
                        rejectReason = "Inventory-operation payload returned duplicate displaced target-slot add entries for one equip-in request.";
                        return false;
                    }

                    return true;
                default:
                    return false;
            }
        }

        private static bool IsExpectedCharacterSourceInventory(EquipmentChangeRequest request, byte inventoryType)
        {
            if (request?.Kind is not EquipmentChangeRequestKind.CharacterToCharacter
                and not EquipmentChangeRequestKind.CharacterToInventory)
            {
                return false;
            }

            if (request.Kind == EquipmentChangeRequestKind.CharacterToCharacter)
            {
                return inventoryType == ClientEquipInventoryType;
            }

            if (request.RequestedPart?.IsCash == true)
            {
                return inventoryType == ClientCashInventoryType;
            }

            if (request.RequestedPart?.IsCash == false)
            {
                return inventoryType == ClientEquipInventoryType;
            }

            return IsSupportedClientCharacterInventoryType(inventoryType);
        }

        private static bool IsExpectedCharacterTargetInventory(EquipmentChangeRequest request, byte inventoryType)
        {
            if (request?.Kind is not EquipmentChangeRequestKind.InventoryToCharacter
                and not EquipmentChangeRequestKind.CharacterToCharacter)
            {
                return false;
            }

            if (request.Kind == EquipmentChangeRequestKind.CharacterToCharacter)
            {
                return inventoryType == ClientEquipInventoryType;
            }

            if (request.RequestedPart?.IsCash == true)
            {
                return inventoryType == ClientCashInventoryType;
            }

            if (request.RequestedPart?.IsCash == false)
            {
                return inventoryType == ClientEquipInventoryType;
            }

            return IsSupportedClientCharacterInventoryType(inventoryType);
        }

        public static byte[] EncodeAuthorityRequestPayload(EquipmentChangeRequest request)
        {
            if (request == null)
            {
                return Array.Empty<byte>();
            }

            return EncodePayload(new CharacterEquipmentAuthorityPayload(
                CharacterEquipmentAuthorityPayloadMode.AuthorityRequest,
                request.RequestId,
                request.RequestedAtTick,
                request.Kind,
                request.OwnerKind,
                request.OwnerSessionId,
                request.ExpectedCharacterId,
                request.ExpectedBuildStateToken,
                request.SourceInventoryType,
                request.SourceInventoryIndex,
                request.SourceEquipSlot,
                request.TargetEquipSlot,
                request.ItemId));
        }

        public static byte[] EncodePayload(CharacterEquipmentAuthorityPayload payload)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream);
            writer.Write((byte)payload.Mode);
            switch (payload.Mode)
            {
                case CharacterEquipmentAuthorityPayloadMode.AuthorityRequest:
                    writer.Write(payload.RequestId);
                    writer.Write(payload.RequestedAtTick);
                    writer.Write((byte)payload.RequestKind);
                    writer.Write((byte)payload.OwnerKind);
                    writer.Write(payload.OwnerSessionId);
                    writer.Write(payload.ExpectedCharacterId);
                    writer.Write(payload.ExpectedBuildStateToken);
                    writer.Write((byte)payload.SourceInventoryType);
                    writer.Write(payload.SourceInventoryIndex);
                    WriteOptionalEquipSlot(writer, payload.SourceEquipSlot);
                    WriteOptionalEquipSlot(writer, payload.TargetEquipSlot);
                    writer.Write(payload.ItemId);
                    break;
                case CharacterEquipmentAuthorityPayloadMode.AuthorityResult:
                    writer.Write(payload.RequestId);
                    writer.Write(payload.RequestedAtTick);
                    writer.Write((byte)payload.ResultKind);
                    writer.Write(payload.ResolvedBuildStateToken);
                    if (payload.ResultKind == CharacterEquipmentAuthorityResultKind.AuthoritativeStateAccept)
                    {
                        WriteAuthoritySlotStates(writer, payload.AuthoritySlotStates);
                        if (payload.AuthorityInventorySlotStates?.Count > 0)
                        {
                            writer.Write(AuthorityResultInventoryStateContextMarker);
                            WriteAuthorityInventorySlotStates(writer, payload.AuthorityInventorySlotStates);
                        }
                    }

                    if (payload.ResultKind == CharacterEquipmentAuthorityResultKind.Reject)
                    {
                        writer.Write(payload.RejectReason ?? string.Empty);
                    }

                    if (payload.HasOwnerSessionContext)
                    {
                        writer.Write(AuthorityResultOwnerSessionContextMarker);
                        writer.Write((byte)payload.OwnerKind);
                        writer.Write(payload.OwnerSessionId);
                        writer.Write(payload.ExpectedCharacterId);
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unsupported character equipment payload mode '{payload.Mode}'.");
            }

            return stream.ToArray();
        }

        public static bool TryDecodePayload(
            byte[] payload,
            out CharacterEquipmentAuthorityPayload decodedPayload,
            out string errorMessage)
        {
            decodedPayload = default;
            errorMessage = null;
            if (payload == null || payload.Length == 0)
            {
                errorMessage = "Character equipment authority payload is missing. Use mode 0 for a request or mode 1 for a result.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream);
                CharacterEquipmentAuthorityPayloadMode mode = (CharacterEquipmentAuthorityPayloadMode)reader.ReadByte();
                switch (mode)
                {
                    case CharacterEquipmentAuthorityPayloadMode.AuthorityRequest:
                        return TryDecodeAuthorityRequest(reader, stream, mode, out decodedPayload, out errorMessage);
                    case CharacterEquipmentAuthorityPayloadMode.AuthorityResult:
                        return TryDecodeAuthorityResult(reader, stream, mode, out decodedPayload, out errorMessage);
                    default:
                        errorMessage = $"Character equipment authority payload mode {(byte)mode} is unsupported.";
                        return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Character equipment authority payload could not be decoded: {ex.Message}";
                return false;
            }
        }

        private static bool TryDecodeAuthorityRequest(
            BinaryReader reader,
            Stream stream,
            CharacterEquipmentAuthorityPayloadMode mode,
            out CharacterEquipmentAuthorityPayload decodedPayload,
            out string errorMessage)
        {
            decodedPayload = default;
            const long expectedLength = sizeof(int) * 9 + sizeof(byte) * 3;
            if (stream.Length - stream.Position != expectedLength)
            {
                errorMessage = "Character equipment authority-request payload must contain request id, requested tick, request kind, owner kind, owner session id, expected character id, build token, source inventory type/index, source slot, target slot, and item id.";
                return false;
            }

            int requestId = reader.ReadInt32();
            int requestedAtTick = reader.ReadInt32();
            byte requestKindValue = reader.ReadByte();
            if (!Enum.IsDefined(typeof(EquipmentChangeRequestKind), (int)requestKindValue))
            {
                errorMessage = "Character equipment authority-request kind is invalid.";
                return false;
            }

            byte ownerKindValue = reader.ReadByte();
            if (!Enum.IsDefined(typeof(EquipmentChangeOwnerKind), (int)ownerKindValue))
            {
                errorMessage = "Character equipment authority-request owner kind is invalid.";
                return false;
            }

            int ownerSessionId = reader.ReadInt32();
            int expectedCharacterId = reader.ReadInt32();
            int expectedBuildStateToken = reader.ReadInt32();
            byte sourceInventoryTypeValue = reader.ReadByte();
            if (!Enum.IsDefined(typeof(InventoryType), (int)sourceInventoryTypeValue))
            {
                errorMessage = $"Character equipment authority-request source inventory type {sourceInventoryTypeValue} is invalid.";
                return false;
            }

            int sourceInventoryIndex = reader.ReadInt32();
            if (!TryReadOptionalEquipSlot(reader, out HaCreator.MapSimulator.Character.EquipSlot? sourceSlot, out errorMessage)
                || !TryReadOptionalEquipSlot(reader, out HaCreator.MapSimulator.Character.EquipSlot? targetSlot, out errorMessage))
            {
                return false;
            }

            decodedPayload = new CharacterEquipmentAuthorityPayload(
                mode,
                requestId,
                requestedAtTick,
                (EquipmentChangeRequestKind)requestKindValue,
                (EquipmentChangeOwnerKind)ownerKindValue,
                ownerSessionId,
                expectedCharacterId,
                expectedBuildStateToken,
                (InventoryType)sourceInventoryTypeValue,
                sourceInventoryIndex,
                sourceSlot,
                targetSlot,
                reader.ReadInt32());
            return true;
        }

        private static bool TryDecodeAuthorityResult(
            BinaryReader reader,
            Stream stream,
            CharacterEquipmentAuthorityPayloadMode mode,
            out CharacterEquipmentAuthorityPayload decodedPayload,
            out string errorMessage)
        {
            decodedPayload = default;
            errorMessage = null;
            if (stream.Length - stream.Position < sizeof(int) * 3 + sizeof(byte))
            {
                errorMessage = "Character equipment authority-result payload must contain request id, requested tick, result kind, and build token.";
                return false;
            }

            int requestId = reader.ReadInt32();
            int requestedAtTick = reader.ReadInt32();
            byte resultKindValue = reader.ReadByte();
            if (!Enum.IsDefined(typeof(CharacterEquipmentAuthorityResultKind), (int)resultKindValue))
            {
                errorMessage = $"Character equipment authority-result kind {resultKindValue} is invalid.";
                return false;
            }

            CharacterEquipmentAuthorityResultKind resultKind = (CharacterEquipmentAuthorityResultKind)resultKindValue;
            int resolvedBuildStateToken = reader.ReadInt32();
            CharacterEquipmentAuthoritySlotState[] authoritySlotStates = null;
            if (resultKind == CharacterEquipmentAuthorityResultKind.AuthoritativeStateAccept)
            {
                if (!TryReadAuthoritySlotStates(reader, out authoritySlotStates, out errorMessage))
                {
                    return false;
                }
            }

            string rejectReason = null;
            if (resultKind == CharacterEquipmentAuthorityResultKind.Reject)
            {
                rejectReason = reader.ReadString();
            }

            CharacterEquipmentAuthorityInventorySlotState[] authorityInventorySlotStates = null;
            bool hasOwnerSessionContext = false;
            EquipmentChangeOwnerKind ownerKind = default;
            int ownerSessionId = 0;
            int expectedCharacterId = 0;
            while (stream.Position != stream.Length)
            {
                byte marker = reader.ReadByte();
                switch (marker)
                {
                    case AuthorityResultInventoryStateContextMarker:
                        if (authorityInventorySlotStates != null)
                        {
                            errorMessage = "Character equipment authority-result payload contained duplicate inventory-state context.";
                            return false;
                        }

                        if (!TryReadAuthorityInventorySlotStates(reader, out authorityInventorySlotStates, out errorMessage))
                        {
                            return false;
                        }

                        break;
                    case AuthorityResultOwnerSessionContextMarker:
                        if (hasOwnerSessionContext)
                        {
                            errorMessage = "Character equipment authority-result payload contained duplicate owner-session context.";
                            return false;
                        }

                        const long ownerSessionContextLength = sizeof(byte) + sizeof(int) * 2;
                        if (stream.Length - stream.Position != ownerSessionContextLength)
                        {
                            errorMessage = "Character equipment authority-result payload contained an invalid owner-session context trailer.";
                            return false;
                        }

                        byte ownerKindValue = reader.ReadByte();
                        if (!Enum.IsDefined(typeof(EquipmentChangeOwnerKind), (int)ownerKindValue))
                        {
                            errorMessage = "Character equipment authority-result owner kind is invalid.";
                            return false;
                        }

                        ownerKind = (EquipmentChangeOwnerKind)ownerKindValue;
                        ownerSessionId = reader.ReadInt32();
                        expectedCharacterId = reader.ReadInt32();
                        hasOwnerSessionContext = true;
                        break;
                    default:
                        errorMessage = "Character equipment authority-result payload contained an unsupported context marker.";
                        return false;
                }
            }

            decodedPayload = new CharacterEquipmentAuthorityPayload(
                mode,
                requestId,
                requestedAtTick,
                ResultKind: resultKind,
                ResolvedBuildStateToken: resolvedBuildStateToken,
                AuthoritySlotStates: authoritySlotStates,
                AuthorityInventorySlotStates: authorityInventorySlotStates,
                RejectReason: rejectReason,
                OwnerKind: ownerKind,
                OwnerSessionId: ownerSessionId,
                ExpectedCharacterId: expectedCharacterId,
                HasOwnerSessionContext: hasOwnerSessionContext);
            return true;
        }

        private static void WriteAuthoritySlotStates(BinaryWriter writer, System.Collections.Generic.IReadOnlyList<CharacterEquipmentAuthoritySlotState> slotStates)
        {
            int count = slotStates?.Count ?? 0;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                CharacterEquipmentAuthoritySlotState state = slotStates[i];
                writer.Write((int)state.Slot);
                writer.Write(state.VisibleItemId);
                writer.Write(state.HiddenItemId);
            }
        }

        private static bool TryReadAuthoritySlotStates(
            BinaryReader reader,
            out CharacterEquipmentAuthoritySlotState[] slotStates,
            out string errorMessage)
        {
            slotStates = null;
            errorMessage = null;
            int count = reader.ReadInt32();
            if (count <= 0 || count > MaxAuthoritySlotStateCount)
            {
                errorMessage = $"Character equipment authority-result state must contain one to {MaxAuthoritySlotStateCount} slot states.";
                return false;
            }

            slotStates = new CharacterEquipmentAuthoritySlotState[count];
            for (int i = 0; i < count; i++)
            {
                int slotValue = reader.ReadInt32();
                if (!Enum.IsDefined(typeof(HaCreator.MapSimulator.Character.EquipSlot), slotValue))
                {
                    errorMessage = $"Character equipment authority-result slot value {slotValue} is invalid.";
                    return false;
                }

                slotStates[i] = new CharacterEquipmentAuthoritySlotState(
                    (HaCreator.MapSimulator.Character.EquipSlot)slotValue,
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }

            return true;
        }

        private static void WriteAuthorityInventorySlotStates(
            BinaryWriter writer,
            IReadOnlyList<CharacterEquipmentAuthorityInventorySlotState> slotStates)
        {
            int count = slotStates?.Count ?? 0;
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                CharacterEquipmentAuthorityInventorySlotState state = slotStates[i];
                writer.Write((byte)state.InventoryType);
                writer.Write(state.SlotIndex);
                writer.Write(state.ItemId);
            }
        }

        private static bool TryReadAuthorityInventorySlotStates(
            BinaryReader reader,
            out CharacterEquipmentAuthorityInventorySlotState[] slotStates,
            out string errorMessage)
        {
            slotStates = null;
            errorMessage = null;
            int count = reader.ReadInt32();
            if (count <= 0 || count > MaxAuthorityInventorySlotStateCount)
            {
                errorMessage = $"Character equipment authority-result inventory state must contain one to {MaxAuthorityInventorySlotStateCount} slot states.";
                return false;
            }

            slotStates = new CharacterEquipmentAuthorityInventorySlotState[count];
            for (int i = 0; i < count; i++)
            {
                byte inventoryTypeValue = reader.ReadByte();
                if (!Enum.IsDefined(typeof(InventoryType), (int)inventoryTypeValue))
                {
                    errorMessage = $"Character equipment authority-result inventory type {inventoryTypeValue} is invalid.";
                    return false;
                }

                slotStates[i] = new CharacterEquipmentAuthorityInventorySlotState(
                    (InventoryType)inventoryTypeValue,
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }

            return true;
        }

        internal static bool HasExplicitAuthorityState(CharacterEquipmentAuthorityPayload payload)
        {
            return payload.ResultKind == CharacterEquipmentAuthorityResultKind.AuthoritativeStateAccept
                   && payload.AuthoritySlotStates?.Count > 0;
        }

        internal static Dictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> CaptureAuthoritySlotStates(
            CharacterBuild build,
            IEnumerable<EquipSlot> slots)
        {
            Dictionary<EquipSlot, CharacterEquipmentAuthoritySlotState> states = new();
            if (build == null || slots == null)
            {
                return states;
            }

            foreach (EquipSlot slot in slots)
            {
                int visibleItemId = build.Equipment.TryGetValue(slot, out CharacterPart visiblePart) && visiblePart != null
                    ? visiblePart.ItemId
                    : 0;
                int hiddenItemId = build.HiddenEquipment.TryGetValue(slot, out CharacterPart hiddenPart) && hiddenPart != null
                    ? hiddenPart.ItemId
                    : 0;
                states[slot] = new CharacterEquipmentAuthoritySlotState(slot, visibleItemId, hiddenItemId);
            }

            return states;
        }

        internal static bool TryApplyAuthoritativeState(
            CharacterBuild build,
            IReadOnlyList<CharacterEquipmentAuthoritySlotState> slotStates,
            out string rejectReason)
        {
            rejectReason = null;
            if (build == null)
            {
                rejectReason = "Character equipment runtime is unavailable.";
                return false;
            }

            if (slotStates == null || slotStates.Count == 0)
            {
                rejectReason = "Character equipment authority result did not include a usable character state.";
                return false;
            }

            HashSet<EquipSlot> appliedSlots = new();
            for (int i = 0; i < slotStates.Count; i++)
            {
                CharacterEquipmentAuthoritySlotState state = slotStates[i];
                if (state.Slot == EquipSlot.None)
                {
                    rejectReason = "Character equipment authority result cannot apply the empty equipment slot.";
                    return false;
                }

                if (!appliedSlots.Add(state.Slot))
                {
                    rejectReason = "Character equipment authority result returned duplicate slot states.";
                    return false;
                }

                if (!TryResolveAuthorityPart(build, state.Slot, state.VisibleItemId, out CharacterPart visiblePart, out rejectReason)
                    || !TryResolveAuthorityPart(build, state.Slot, state.HiddenItemId, out CharacterPart hiddenPart, out rejectReason))
                {
                    return false;
                }

                if (hiddenPart != null && visiblePart?.IsCash != true)
                {
                    rejectReason = "Character equipment authority state cannot keep a hidden item without a visible cash item.";
                    return false;
                }

                if (visiblePart == null)
                {
                    build.Equipment.Remove(state.Slot);
                }
                else
                {
                    build.Equipment[state.Slot] = visiblePart;
                }

                if (hiddenPart == null)
                {
                    build.HiddenEquipment.Remove(state.Slot);
                }
                else
                {
                    build.HiddenEquipment[state.Slot] = hiddenPart;
                }
            }

            return true;
        }

        private static void WriteOptionalEquipSlot(BinaryWriter writer, HaCreator.MapSimulator.Character.EquipSlot? slot)
        {
            writer.Write(slot.HasValue ? (int)slot.Value : -1);
        }

        private static bool TryResolveAuthorityPart(
            CharacterBuild build,
            EquipSlot slot,
            int itemId,
            out CharacterPart part,
            out string rejectReason)
        {
            rejectReason = null;
            part = null;
            if (itemId <= 0)
            {
                return true;
            }

            if (build?.EquipmentPartLoader == null)
            {
                rejectReason = $"Character equipment loader is unavailable for authoritative item {itemId}.";
                return false;
            }

            CharacterPart loadedPart = build.EquipmentPartLoader.Invoke(itemId)?.Clone();
            if (loadedPart == null)
            {
                rejectReason = $"Character equipment authority could not load item {itemId}.";
                return false;
            }

            loadedPart.Slot = slot;
            part = loadedPart;
            return true;
        }

        private static bool TryMatchesCharacterInventoryOperationSwap(
            EquipmentChangeRequest request,
            byte inventoryType,
            short sourcePosition,
            short targetPosition,
            out string rejectReason)
        {
            rejectReason = null;
            switch (request.Kind)
            {
                case EquipmentChangeRequestKind.InventoryToCharacter:
                    if (!request.TargetEquipSlot.HasValue || request.SourceInventoryIndex < 0)
                    {
                        rejectReason = "Character equip-in request is missing source slot or target equipment slot metadata.";
                        return false;
                    }

                    byte expectedInventoryType = (byte)request.SourceInventoryType;
                    if (inventoryType != expectedInventoryType)
                    {
                        rejectReason = "Character equip-in inventory-operation swap did not target the requested inventory.";
                        return false;
                    }

                    short expectedSourcePosition = (short)(request.SourceInventoryIndex + 1);
                    short expectedTargetPosition = ToClientEquipPosition(request.TargetEquipSlot.Value);
                    if (sourcePosition != expectedSourcePosition || targetPosition != expectedTargetPosition)
                    {
                        rejectReason = "Inventory-operation swap did not match the requested equip-in source/target positions.";
                        return false;
                    }

                    return true;
                case EquipmentChangeRequestKind.CharacterToCharacter:
                    if (!request.SourceEquipSlot.HasValue || !request.TargetEquipSlot.HasValue)
                    {
                        rejectReason = "Character move request is missing source or target equipment slot metadata.";
                        return false;
                    }

                    if (inventoryType != ClientEquipInventoryType)
                    {
                        rejectReason = "Character move inventory-operation swap did not target the equipped inventory.";
                        return false;
                    }

                    short expectedCharacterSourcePosition = ToClientEquipPosition(request.SourceEquipSlot.Value);
                    short expectedCharacterTargetPosition = ToClientEquipPosition(request.TargetEquipSlot.Value);
                    if (sourcePosition != expectedCharacterSourcePosition || targetPosition != expectedCharacterTargetPosition)
                    {
                        rejectReason = "Inventory-operation swap did not match the requested character slot move.";
                        return false;
                    }

                    return true;
                case EquipmentChangeRequestKind.CharacterToInventory:
                    if (!request.SourceEquipSlot.HasValue)
                    {
                        rejectReason = "Character unequip request is missing source equipment slot metadata.";
                        return false;
                    }

                    if (!IsSupportedClientCharacterInventoryType(inventoryType))
                    {
                        rejectReason = "Character unequip inventory-operation swap targeted an unsupported inventory.";
                        return false;
                    }

                    if (request.RequestedPart?.IsCash == true && inventoryType != ClientCashInventoryType)
                    {
                        rejectReason = "Character unequip inventory-operation swap did not target the cash inventory for a cash item.";
                        return false;
                    }

                    if (request.RequestedPart?.IsCash == false && inventoryType != ClientEquipInventoryType)
                    {
                        rejectReason = "Character unequip inventory-operation swap did not target the equip inventory for a non-cash item.";
                        return false;
                    }

                    short expectedUnequipSourcePosition = ToClientEquipPosition(request.SourceEquipSlot.Value);
                    if (sourcePosition != expectedUnequipSourcePosition)
                    {
                        rejectReason = "Inventory-operation swap did not originate from the requested equipment slot.";
                        return false;
                    }

                    if (targetPosition <= 0)
                    {
                        rejectReason = "Inventory-operation swap did not land in a positive inventory slot.";
                        return false;
                    }

                    return true;
                default:
                    rejectReason = "Unsupported character equipment request kind for inventory-operation swap matching.";
                    return false;
            }
        }

        private static bool TryMatchesCharacterInventoryOperationAdd(
            EquipmentChangeRequest request,
            byte inventoryType,
            short targetPosition,
            int addedItemId,
            out string rejectReason)
        {
            rejectReason = null;
            switch (request.Kind)
            {
                case EquipmentChangeRequestKind.InventoryToCharacter:
                    if (!request.TargetEquipSlot.HasValue)
                    {
                        rejectReason = "Character equip-in request is missing target equipment slot metadata.";
                        return false;
                    }

                    if (inventoryType != (byte)request.SourceInventoryType)
                    {
                        rejectReason = "Character equip-in inventory-operation add entry did not target the requested inventory.";
                        return false;
                    }

                    if (targetPosition != ToClientEquipPosition(request.TargetEquipSlot.Value))
                    {
                        rejectReason = "Inventory-operation add entry did not target the requested equip-in slot.";
                        return false;
                    }

                    if (addedItemId != request.ItemId)
                    {
                        rejectReason = "Inventory-operation add entry did not carry the requested equip-in item id.";
                        return false;
                    }

                    return true;

                case EquipmentChangeRequestKind.CharacterToCharacter:
                    if (!request.TargetEquipSlot.HasValue)
                    {
                        rejectReason = "Character move request is missing target equipment slot metadata.";
                        return false;
                    }

                    if (inventoryType != ClientEquipInventoryType)
                    {
                        rejectReason = "Character move inventory-operation add entry did not target the equipped inventory.";
                        return false;
                    }

                    if (targetPosition != ToClientEquipPosition(request.TargetEquipSlot.Value))
                    {
                        rejectReason = "Inventory-operation add entry did not target the requested character slot move.";
                        return false;
                    }

                    if (addedItemId != request.ItemId)
                    {
                        rejectReason = "Inventory-operation add entry did not carry the requested character item id.";
                        return false;
                    }

                    return true;

                case EquipmentChangeRequestKind.CharacterToInventory:
                    if (!request.SourceEquipSlot.HasValue)
                    {
                        rejectReason = "Character unequip request is missing source equipment slot metadata.";
                        return false;
                    }

                    if (!IsSupportedClientCharacterInventoryType(inventoryType))
                    {
                        rejectReason = "Character unequip inventory-operation add entry targeted an unsupported inventory.";
                        return false;
                    }

                    if (request.RequestedPart?.IsCash == true && inventoryType != ClientCashInventoryType)
                    {
                        rejectReason = "Character unequip inventory-operation add entry did not target the cash inventory for a cash item.";
                        return false;
                    }

                    if (request.RequestedPart?.IsCash == false && inventoryType != ClientEquipInventoryType)
                    {
                        rejectReason = "Character unequip inventory-operation add entry did not target the equip inventory for a non-cash item.";
                        return false;
                    }

                    if (targetPosition <= 0)
                    {
                        rejectReason = "Inventory-operation add entry did not land in a positive inventory slot.";
                        return false;
                    }

                    if (addedItemId != request.ItemId)
                    {
                        rejectReason = "Inventory-operation add entry did not carry the requested unequip item id.";
                        return false;
                    }

                    return true;

                default:
                    rejectReason = "Unsupported character equipment request kind for inventory-operation add matching.";
                    return false;
            }
        }

        private static short ToClientEquipPosition(EquipSlot slot)
        {
            return unchecked((short)-(int)slot);
        }

        private static bool TryReadClientInventoryOperationAddEntry(
            EquipmentChangeRequest request,
            byte inventoryType,
            short targetPosition,
            BinaryReader reader,
            out int itemId,
            out bool matchedByHeader,
            out string rejectReason)
        {
            itemId = 0;
            matchedByHeader = false;
            rejectReason = null;
            if (!TryEnsureRemaining(reader?.BaseStream, sizeof(byte) + sizeof(int) + sizeof(byte) + sizeof(long), out rejectReason))
            {
                return false;
            }

            try
            {
                byte slotType = reader.ReadByte();
                if (slotType is not ItemSlotTypeEquip and not ItemSlotTypeBundle and not ItemSlotTypePet)
                {
                    rejectReason = $"Inventory-operation add entry used unsupported GW_ItemSlotBase type {slotType}.";
                    return false;
                }

                itemId = reader.ReadInt32();
                bool hasCashSerial = reader.ReadByte() != 0;
                if (hasCashSerial)
                {
                    _ = reader.ReadInt64();
                }

                _ = reader.ReadInt64(); // dateExpire
                if (TryMatchesCharacterInventoryOperationAdd(
                        request,
                        inventoryType,
                        targetPosition,
                        itemId,
                        out _))
                {
                    matchedByHeader = true;
                    return true;
                }

                switch (slotType)
                {
                    case ItemSlotTypeEquip:
                        return TryReadClientEquipAddEntryBody(reader, hasCashSerial, out rejectReason);
                    case ItemSlotTypeBundle:
                        return TryReadClientBundleAddEntryBody(reader, itemId, out rejectReason);
                    case ItemSlotTypePet:
                        return TryReadClientPetAddEntryBody(reader, out rejectReason);
                    default:
                        rejectReason = $"Inventory-operation add entry used unsupported GW_ItemSlotBase type {slotType}.";
                        return false;
                }
            }
            catch (EndOfStreamException)
            {
                rejectReason = "Inventory-operation add entry is truncated.";
                return false;
            }
            catch (IOException)
            {
                rejectReason = "Inventory-operation add entry is truncated.";
                return false;
            }
        }

        private static bool TryReadClientEquipAddEntryBody(
            BinaryReader reader,
            bool hasCashSerial,
            out string rejectReason)
        {
            long entryStart = reader?.BaseStream?.Position ?? 0;
            if (TryReadClientEquipAddEntryBody(reader, hasCashSerial, statFieldCount: 14, out rejectReason))
            {
                return true;
            }

            if (reader?.BaseStream is { CanSeek: true } stream)
            {
                stream.Position = entryStart;
                return TryReadClientEquipAddEntryBody(reader, hasCashSerial, statFieldCount: 15, out rejectReason);
            }

            return false;
        }

        private static bool TryReadClientEquipAddEntryBody(
            BinaryReader reader,
            bool hasCashSerial,
            int statFieldCount,
            out string rejectReason)
        {
            rejectReason = null;
            Stream stream = reader.BaseStream;
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

            if (title.Length > 13)
            {
                rejectReason = "Inventory-operation equip add entry title is outside the expected client byte range.";
                return false;
            }

            const int equipTailLength = sizeof(short) + (sizeof(byte) * 2) + (sizeof(int) * 3) + (sizeof(byte) * 2) + (sizeof(short) * 5);
            if (!TryEnsureRemaining(
                    stream,
                    equipTailLength + (hasCashSerial ? 0 : sizeof(long)) + sizeof(long) + sizeof(int),
                    out rejectReason))
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
                _ = reader.ReadInt64();
            }

            _ = reader.ReadInt64();
            _ = reader.ReadInt32();
            return true;
        }

        private static bool TryReadClientBundleAddEntryBody(
            BinaryReader reader,
            int itemId,
            out string rejectReason)
        {
            rejectReason = null;
            if (!TryEnsureRemaining(reader.BaseStream, sizeof(ushort), out rejectReason))
            {
                return false;
            }

            _ = reader.ReadUInt16();
            if (!TryReadClientMapleString(reader, out _, out rejectReason))
            {
                return false;
            }

            if (!TryEnsureRemaining(reader.BaseStream, sizeof(short), out rejectReason))
            {
                return false;
            }

            _ = reader.ReadInt16();
            if ((itemId / 10000) is 207 or 233)
            {
                if (!TryEnsureRemaining(reader.BaseStream, sizeof(long), out rejectReason))
                {
                    return false;
                }

                _ = reader.ReadInt64();
            }

            return true;
        }

        private static bool TryReadClientPetAddEntryBody(
            BinaryReader reader,
            out string rejectReason)
        {
            const int petBodyLength = 13 + sizeof(byte) + sizeof(short) + sizeof(byte) + sizeof(long) + sizeof(short) + sizeof(ushort) + sizeof(int) + sizeof(short);
            if (!TryEnsureRemaining(reader.BaseStream, petBodyLength, out rejectReason))
            {
                return false;
            }

            _ = reader.ReadBytes(13);
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
            if (!TryEnsureRemaining(reader.BaseStream, sizeof(short), out rejectReason))
            {
                return false;
            }

            short length = reader.ReadInt16();
            if (length < 0)
            {
                rejectReason = "Inventory-operation add entry maple string length is invalid.";
                return false;
            }

            if (!TryEnsureRemaining(reader.BaseStream, length, out rejectReason))
            {
                return false;
            }

            value = length == 0
                ? string.Empty
                : Encoding.ASCII.GetString(reader.ReadBytes(length));
            return true;
        }

        private static bool TryEnsureRemaining(Stream stream, int byteCount, out string rejectReason)
        {
            rejectReason = null;
            if (stream == null)
            {
                rejectReason = "Inventory-operation stream is unavailable.";
                return false;
            }

            if (byteCount < 0 || stream.Length - stream.Position < byteCount)
            {
                rejectReason = "Inventory-operation add entry is truncated.";
                return false;
            }

            return true;
        }

        private static bool IsSupportedClientCharacterInventoryType(byte inventoryType)
        {
            return inventoryType == ClientEquipInventoryType || inventoryType == ClientCashInventoryType;
        }

        private static bool TryReadOptionalEquipSlot(
            BinaryReader reader,
            out HaCreator.MapSimulator.Character.EquipSlot? slot,
            out string errorMessage)
        {
            slot = null;
            errorMessage = null;
            int slotValue = reader.ReadInt32();
            if (slotValue == -1)
            {
                return true;
            }

            if (!Enum.IsDefined(typeof(HaCreator.MapSimulator.Character.EquipSlot), slotValue))
            {
                errorMessage = $"Character equipment slot value {slotValue} is invalid.";
                return false;
            }

            slot = (HaCreator.MapSimulator.Character.EquipSlot)slotValue;
            return true;
        }
    }
}
