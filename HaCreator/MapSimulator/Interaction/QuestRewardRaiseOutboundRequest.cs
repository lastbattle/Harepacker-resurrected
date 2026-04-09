using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.Interaction
{
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
        string Summary)
    {
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
                $"PutItem confirm owner #{Math.Max(0, state?.OwnerItemId ?? 0)} session #{Math.Max(0, state?.ManagerSessionId ?? 0)} pieces {state?.PlacedPieces?.Count ?? 0}");
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
    }
}
