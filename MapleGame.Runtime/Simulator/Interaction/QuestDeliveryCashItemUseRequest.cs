using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using BinaryReader = MapleLib.PacketLib.PacketReader;
using BinaryWriter = MapleLib.PacketLib.PacketWriter;
namespace HaCreator.MapSimulator.Interaction
{
    internal enum QuestDeliveryCashItemUseRequestKind
    {
        AcceptDelivery,
        CompleteDelivery
    }

    internal sealed record QuestDeliveryCashItemUseRequest(
        QuestDeliveryCashItemUseRequestKind Kind,
        int Opcode,
        IReadOnlyList<byte> Payload,
        int UpdateTime,
        int CashItemSlotPosition,
        int CashItemId,
        int ConsumeCashItemType,
        int EquipmentPosition,
        string Summary)
    {
        // CWvsContext::SendConsumeCashItemUseRequest constructs COutPacket(0x55).
        internal const int ClientOpcode = 0x55;
        internal const int DeliveryAcceptCashItemId = 5660000;
        internal const int DeliveryCompleteCashItemId = 5660001;
        internal const int DeliveryConsumeCashItemType = 78;

        internal static QuestDeliveryCashItemUseRequest Create(
            QuestDeliveryCashItemUseRequestKind kind,
            int updateTime,
            int cashItemSlotPosition,
            int cashItemId,
            int equipmentPosition = 0)
        {
            int normalizedCashItemId = cashItemId > 0
                ? cashItemId
                : kind == QuestDeliveryCashItemUseRequestKind.CompleteDelivery
                    ? DeliveryCompleteCashItemId
                    : DeliveryAcceptCashItemId;
            int normalizedSlot = Math.Clamp(cashItemSlotPosition, ushort.MinValue, ushort.MaxValue);
            int normalizedEquipmentPosition = Math.Clamp(equipmentPosition, ushort.MinValue, ushort.MaxValue);
            int consumeType = ResolveConsumeCashItemType(normalizedCashItemId);
            byte[] payload = BuildClientPrefixPayload(
                updateTime,
                normalizedSlot,
                normalizedCashItemId,
                consumeType,
                normalizedEquipmentPosition);

            string phaseText = kind == QuestDeliveryCashItemUseRequestKind.CompleteDelivery
                ? "complete"
                : "accept";
            return new QuestDeliveryCashItemUseRequest(
                kind,
                ClientOpcode,
                Array.AsReadOnly(payload),
                updateTime,
                normalizedSlot,
                normalizedCashItemId,
                consumeType,
                normalizedEquipmentPosition,
                $"SendConsumeCashItemUseRequest {phaseText} delivery item {normalizedCashItemId} slot {normalizedSlot} type {consumeType} opcode {ClientOpcode}");
        }

        internal static int ResolveConsumeCashItemType(int itemId)
        {
            // get_cashslot_item_type returns 78 for the 566xxxx family, and
            // get_consume_cash_item_type keeps 78 as a sendable consume type.
            return itemId / 10000 == 566
                ? DeliveryConsumeCashItemType
                : 0;
        }

        internal static bool TryDecodeClientPrefixPayload(
            IReadOnlyList<byte> payload,
            out int updateTime,
            out int cashItemSlotPosition,
            out int cashItemId,
            out int consumeCashItemType,
            out int equipmentPosition,
            out string error)
        {
            updateTime = 0;
            cashItemSlotPosition = 0;
            cashItemId = 0;
            consumeCashItemType = 0;
            equipmentPosition = 0;
            error = null;

            if (payload == null)
            {
                error = "Delivery cash-item payload is missing.";
                return false;
            }

            byte[] bytes = new byte[payload.Count];
            for (int i = 0; i < payload.Count; i++)
            {
                bytes[i] = payload[i];
            }

            const int minimumLength = sizeof(int) + sizeof(ushort) + sizeof(int) + sizeof(byte);
            if (bytes.Length < minimumLength)
            {
                error = $"Delivery cash-item payload must be at least {minimumLength} bytes.";
                return false;
            }

            using MemoryStream stream = new(bytes, writable: false);
            using BinaryReader reader = new(stream, Encoding.Default, leaveOpen: false);
            updateTime = reader.ReadInt32();
            cashItemSlotPosition = reader.ReadUInt16();
            cashItemId = reader.ReadInt32();
            consumeCashItemType = reader.ReadByte();
            if (stream.Position + sizeof(ushort) <= stream.Length)
            {
                equipmentPosition = reader.ReadUInt16();
            }

            if (stream.Position != stream.Length)
            {
                error = $"Delivery cash-item payload has {stream.Length - stream.Position} trailing byte(s).";
                return false;
            }

            return true;
        }

        private static byte[] BuildClientPrefixPayload(
            int updateTime,
            int cashItemSlotPosition,
            int cashItemId,
            int consumeCashItemType,
            int equipmentPosition)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.Default, leaveOpen: true);
            writer.Write(updateTime);
            writer.Write((ushort)Math.Clamp(cashItemSlotPosition, ushort.MinValue, ushort.MaxValue));
            writer.Write(Math.Max(0, cashItemId));
            writer.Write((byte)Math.Clamp(consumeCashItemType, byte.MinValue, byte.MaxValue));
            if (consumeCashItemType == DeliveryConsumeCashItemType)
            {
                writer.Write((ushort)Math.Clamp(equipmentPosition, ushort.MinValue, ushort.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
