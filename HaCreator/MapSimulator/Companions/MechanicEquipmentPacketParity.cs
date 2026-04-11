using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;

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
        internal const byte ClientEquipInventoryType = 1;
        internal const ushort ClientChangeSlotPositionCountAll = 0xFFFF;

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
            writer.Write((ushort)bodyPart);
            writer.Write(ClientChangeSlotPositionCountAll);
            payload = stream.ToArray();
            return true;
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
                        if (stream.Length - stream.Position != sizeof(byte) + sizeof(int))
                        {
                            errorMessage = "Mechanic slot-mutation payload must contain a client body-part Int32 followed by an Int32 item id. Legacy one-byte slot ids are still accepted. Use item id 0 to clear that slot.";
                            return false;
                        }

                        if (!TryReadMechanicSlot(reader, out MechanicEquipSlot slot, out errorMessage))
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
                                    RejectReason: reader.ReadString());
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
    }
}
