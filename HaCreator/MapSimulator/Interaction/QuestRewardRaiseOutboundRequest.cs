using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed record QuestRewardRaisePacketPayload(
        int ManagerSessionId,
        int OwnerRequestId,
        int PieceRequestId,
        int QuestId,
        int OwnerItemId,
        int QrData,
        QuestRewardRaiseWindowMode WindowMode,
        QuestRewardRaiseWindowMode DisplayMode,
        InventoryType InventoryType,
        int SlotIndex,
        int ItemId,
        int Quantity,
        int PlacedPieceCount,
        IReadOnlyDictionary<int, int> SelectedItemsByGroup);

    internal enum QuestRewardRaiseOutboundRequestKind
    {
        PutItemAdd,
        PutItemRelease,
        PutItemConfirm
    }

    internal sealed record QuestRewardRaiseOutboundRequest(
        QuestRewardRaiseOutboundRequestKind Kind,
        int Opcode,
        IReadOnlyList<byte> Payload,
        int ClientOpcode,
        IReadOnlyList<byte> ClientPayload,
        string Summary)
    {
        internal const int ClientPutItemOpcode = 286;
        internal const int PutItemAddOpcode = 283;
        internal const int PutItemReleaseOpcode = 285;
        internal const int PutItemConfirmOpcode = 286;

        internal static QuestRewardRaiseOutboundRequest CreatePutItemAdd(QuestRewardRaiseState state, QuestRewardRaisePlacedPiece piece)
        {
            byte[] payload = BuildPayload(
                state,
                piece.RequestId,
                piece.InventoryType,
                piece.SlotIndex,
                piece.ItemId,
                Math.Max(1, piece.Quantity),
                state.PlacedPieces.Count,
                state.SelectedItemsByGroup);
            return new QuestRewardRaiseOutboundRequest(
                QuestRewardRaiseOutboundRequestKind.PutItemAdd,
                PutItemAddOpcode,
                Array.AsReadOnly(payload),
                ClientPutItemOpcode,
                Array.AsReadOnly(BuildClientPutItemPayload(piece.InventoryType, piece.SlotIndex, piece.ItemId)),
                $"PutItem add owner #{Math.Max(0, state?.OwnerItemId ?? 0)} req #{piece.RequestId} item {piece.ItemId} slot {piece.InventoryType}:{piece.SlotIndex + 1}");
        }

        internal static QuestRewardRaiseOutboundRequest CreatePutItemRelease(QuestRewardRaiseState state, QuestRewardRaisePlacedPiece piece)
        {
            byte[] payload = BuildPayload(
                state,
                piece.RequestId,
                piece.InventoryType,
                piece.SlotIndex,
                piece.ItemId,
                Math.Max(1, piece.Quantity),
                Math.Max(0, (state?.PlacedPieces?.Count ?? 1) - 1),
                state?.SelectedItemsByGroup);
            return new QuestRewardRaiseOutboundRequest(
                QuestRewardRaiseOutboundRequestKind.PutItemRelease,
                PutItemReleaseOpcode,
                Array.AsReadOnly(payload),
                -1,
                Array.Empty<byte>(),
                $"PutItem release owner #{Math.Max(0, state?.OwnerItemId ?? 0)} req #{piece.RequestId} item {piece.ItemId} slot {piece.InventoryType}:{piece.SlotIndex + 1}");
        }

        internal static QuestRewardRaiseOutboundRequest CreatePutItemConfirm(QuestRewardRaiseState state)
        {
            byte[] payload = BuildPayload(
                state,
                state?.RequestId ?? 0,
                InventoryType.NONE,
                -1,
                state?.OwnerItemId ?? 0,
                Math.Max(1, state?.PlacedPieces?.Count ?? 1),
                state?.PlacedPieces?.Count ?? 0,
                state?.SelectedItemsByGroup);
            return new QuestRewardRaiseOutboundRequest(
                QuestRewardRaiseOutboundRequestKind.PutItemConfirm,
                PutItemConfirmOpcode,
                Array.AsReadOnly(payload),
                -1,
                Array.Empty<byte>(),
                $"PutItem confirm owner #{Math.Max(0, state?.OwnerItemId ?? 0)} session #{Math.Max(0, state?.ManagerSessionId ?? 0)} pieces {state?.PlacedPieces?.Count ?? 0}");
        }

        internal static bool TryDecodePayload(byte[] payload, out QuestRewardRaisePacketPayload decoded, out string error)
        {
            decoded = null;
            error = null;

            if (payload == null)
            {
                error = "Raise payload is missing.";
                return false;
            }

            const int minimumLength =
                sizeof(int) * 6 +
                sizeof(byte) * 3 +
                sizeof(short) * 4;
            if (payload.Length < minimumLength)
            {
                error = $"Raise payload must be at least {minimumLength} bytes.";
                return false;
            }

            try
            {
                using MemoryStream stream = new(payload, writable: false);
                using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);

                int managerSessionId = reader.ReadInt32();
                int ownerRequestId = reader.ReadInt32();
                int pieceRequestId = reader.ReadInt32();
                int questId = reader.ReadInt32();
                int ownerItemId = reader.ReadInt32();
                int qrData = reader.ReadInt32();
                QuestRewardRaiseWindowMode windowMode = (QuestRewardRaiseWindowMode)reader.ReadByte();
                QuestRewardRaiseWindowMode displayMode = (QuestRewardRaiseWindowMode)reader.ReadByte();
                InventoryType inventoryType = (InventoryType)reader.ReadByte();
                int slotIndex = reader.ReadInt16();
                int itemId = reader.ReadInt32();
                int quantity = reader.ReadInt16();
                int placedPieceCount = reader.ReadInt16();
                int selectedEntryCount = reader.ReadInt16();
                if (selectedEntryCount < 0)
                {
                    error = "Raise payload selected-entry count cannot be negative.";
                    return false;
                }

                Dictionary<int, int> selectedItemsByGroup = new(selectedEntryCount);
                for (int i = 0; i < selectedEntryCount; i++)
                {
                    if ((stream.Length - stream.Position) < sizeof(int) * 2)
                    {
                        error = "Raise payload ended before all selected-group pairs were available.";
                        return false;
                    }

                    selectedItemsByGroup[reader.ReadInt32()] = reader.ReadInt32();
                }

                decoded = new QuestRewardRaisePacketPayload(
                    managerSessionId,
                    ownerRequestId,
                    pieceRequestId,
                    questId,
                    ownerItemId,
                    qrData,
                    windowMode,
                    displayMode,
                    inventoryType,
                    slotIndex,
                    itemId,
                    quantity,
                    placedPieceCount,
                    selectedItemsByGroup);
                return true;
            }
            catch (EndOfStreamException)
            {
                error = "Raise payload ended unexpectedly.";
                return false;
            }
        }

        private static byte[] BuildPayload(
            QuestRewardRaiseState state,
            int requestId,
            InventoryType inventoryType,
            int slotIndex,
            int itemId,
            int quantity,
            int placedPieceCount,
            IReadOnlyDictionary<int, int> selectedItemsByGroup)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(Math.Max(0, state?.ManagerSessionId ?? 0));
            writer.Write(Math.Max(0, state?.RequestId ?? 0));
            writer.Write(Math.Max(0, requestId));
            writer.Write(Math.Max(0, state?.Prompt?.QuestId ?? 0));
            writer.Write(Math.Max(0, state?.OwnerItemId ?? 0));
            writer.Write(state?.QrData ?? 0);
            writer.Write((byte)(state?.WindowMode ?? QuestRewardRaiseWindowMode.Selection));
            writer.Write((byte)(state?.DisplayMode ?? QuestRewardRaiseWindowMode.Selection));
            writer.Write((byte)inventoryType);
            writer.Write((short)Math.Max(-1, slotIndex));
            writer.Write(itemId);
            writer.Write((short)Math.Max(1, quantity));
            writer.Write((short)Math.Max(0, placedPieceCount));

            KeyValuePair<int, int>[] selectedEntries = selectedItemsByGroup?
                .OrderBy(pair => pair.Key)
                .ToArray()
                ?? Array.Empty<KeyValuePair<int, int>>();
            writer.Write((short)selectedEntries.Length);
            for (int i = 0; i < selectedEntries.Length; i++)
            {
                writer.Write(selectedEntries[i].Key);
                writer.Write(selectedEntries[i].Value);
            }

            writer.Flush();
            return stream.ToArray();
        }

        private static byte[] BuildClientPutItemPayload(InventoryType inventoryType, int slotIndex, int itemId)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(ResolveClientItemType(inventoryType));
            writer.Write((ushort)Math.Clamp(slotIndex + 1, 0, ushort.MaxValue));
            writer.Write(Math.Max(0, itemId));
            writer.Flush();
            return stream.ToArray();
        }

        private static byte ResolveClientItemType(InventoryType inventoryType)
        {
            int rawValue = (int)inventoryType;
            return rawValue > 0 && rawValue <= byte.MaxValue
                ? (byte)rawValue
                : (byte)0;
        }
    }
}
