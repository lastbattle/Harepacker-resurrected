using HaCreator.MapSimulator.Character;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;

namespace HaCreator.MapSimulator.UI
{
    public static class CharacterEquipmentPacketParity
    {
        private const int MaxAuthoritySlotStateCount = 64;
        private const byte AuthorityResultOwnerSessionContextMarker = 0xEC;

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

            bool hasOwnerSessionContext = false;
            EquipmentChangeOwnerKind ownerKind = default;
            int ownerSessionId = 0;
            int expectedCharacterId = 0;
            if (stream.Position != stream.Length)
            {
                const long ownerSessionContextLength = sizeof(byte) * 2 + sizeof(int) * 2;
                if (stream.Length - stream.Position != ownerSessionContextLength)
                {
                    errorMessage = "Character equipment authority-result payload contained an invalid owner-session context trailer.";
                    return false;
                }

                byte marker = reader.ReadByte();
                if (marker != AuthorityResultOwnerSessionContextMarker)
                {
                    errorMessage = "Character equipment authority-result payload contained an unsupported owner-session context marker.";
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
            }

            if (stream.Position != stream.Length)
            {
                errorMessage = "Character equipment authority-result payload should not contain extra bytes.";
                return false;
            }

            decodedPayload = new CharacterEquipmentAuthorityPayload(
                mode,
                requestId,
                requestedAtTick,
                ResultKind: resultKind,
                ResolvedBuildStateToken: resolvedBuildStateToken,
                AuthoritySlotStates: authoritySlotStates,
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
