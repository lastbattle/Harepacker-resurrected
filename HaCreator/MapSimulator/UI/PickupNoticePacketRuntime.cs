using System;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.UI
{
    internal readonly struct PickupNoticePacketMessage
    {
        public PickupNoticePacketMessage(
            string screenMessage,
            PickupMessageType screenMessageType,
            string chatMessage = "",
            string secondaryScreenMessage = "",
            Color? secondaryScreenColor = null,
            int chatLogType = -1,
            int itemId = 0,
            int quantity = 1,
            int mesoAmount = 0)
        {
            ScreenMessage = screenMessage ?? string.Empty;
            ScreenMessageType = screenMessageType;
            ChatMessage = chatMessage ?? string.Empty;
            SecondaryScreenMessage = secondaryScreenMessage ?? string.Empty;
            SecondaryScreenColor = secondaryScreenColor ?? Color.White;
            ChatLogType = chatLogType;
            ItemId = itemId;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
        }

        public string ScreenMessage { get; }
        public PickupMessageType ScreenMessageType { get; }
        public string ChatMessage { get; }
        public string SecondaryScreenMessage { get; }
        public Color SecondaryScreenColor { get; }
        public int ChatLogType { get; }
        public int ItemId { get; }
        public int Quantity { get; }
        public int MesoAmount { get; }
    }

    internal static class PickupNoticePacketRuntime
    {
        private const byte DropPickupMessageKind = 0;

        public static bool TryDecodeClientMessagePayload(
            byte[] payload,
            Func<int, string> itemNameResolver,
            Func<int, string> itemTypeNameResolver,
            Func<string, float> measureTextWidth,
            out PickupNoticePacketMessage packetMessage,
            out string message)
        {
            packetMessage = default;
            message = "Drop-pickup message payload is missing.";
            if (payload == null || payload.Length < 1)
            {
                return false;
            }

            // Primary path: context-owned envelope [messageKind][pickupType]...
            if (payload.Length >= 2
                && payload[0] == DropPickupMessageKind
                && TryDecodePickupMessageBody(
                    payload,
                    offset: 1,
                    pickupMessageType: unchecked((sbyte)payload[1]),
                    itemNameResolver,
                    itemTypeNameResolver,
                    measureTextWidth,
                    out packetMessage,
                    out _))
            {
                message = "Applied packet-owned drop-pickup message.";
                return true;
            }

            // Fallback producer path: direct OnDropPickUpMessage body [pickupType]...
            if (TryDecodePickupMessageBody(
                    payload,
                    offset: 1,
                    pickupMessageType: unchecked((sbyte)payload[0]),
                    itemNameResolver,
                    itemTypeNameResolver,
                    measureTextWidth,
                    out packetMessage,
                    out _))
            {
                message = "Applied packet-owned drop-pickup message from direct OnDropPickUpMessage payload.";
                return true;
            }

            if (payload[0] != DropPickupMessageKind)
            {
                message = $"Context message kind {payload[0]} is not OnDropPickUpMessage(0), and direct OnDropPickUpMessage decoding failed.";
                return false;
            }

            message = "Drop-pickup message payload could not be decoded as either context envelope or direct OnDropPickUpMessage body.";
            return false;
        }

        private static bool TryDecodePickupMessageBody(
            byte[] payload,
            int offset,
            sbyte pickupMessageType,
            Func<int, string> itemNameResolver,
            Func<int, string> itemTypeNameResolver,
            Func<string, float> measureTextWidth,
            out PickupNoticePacketMessage packetMessage,
            out string message)
        {
            return pickupMessageType switch
            {
                1 => TryDecodeMesoPickup(payload, offset, out packetMessage, out message),
                0 => TryDecodeItemPickup(payload, offset, itemNameResolver, itemTypeNameResolver, measureTextWidth, out packetMessage, out message),
                2 => TryDecodeQuestItemPickup(payload, offset, itemNameResolver, itemTypeNameResolver, measureTextWidth, out packetMessage, out message),
                -2 => TryDecodeFailure(payload, offset, DropPickupFailureKind.InventoryFull, out packetMessage, out message),
                -3 => TryDecodeFailure(payload, offset, DropPickupFailureKind.CantPickup, out packetMessage, out message),
                _ => TryDecodeFailure(payload, offset, DropPickupFailureKind.Generic, out packetMessage, out message)
            };
        }

        private static bool TryDecodeMesoPickup(
            byte[] payload,
            int offset,
            out PickupNoticePacketMessage packetMessage,
            out string message)
        {
            packetMessage = default;
            message = null;
            if (!TryReadByte(payload, ref offset, out byte pickedByPet)
                || !TryReadInt32(payload, ref offset, out int mesoAmount)
                || !TryReadInt16(payload, ref offset, out short bonusMesoAmount))
            {
                message = "Drop-pickup meso payload must contain pet flag, meso amount, and bonus meso amount.";
                return false;
            }

            if (!TryRequireNoTrailingBytes(payload, offset, out message))
            {
                return false;
            }

            PickupNoticeSuccessMessages messages = PickupNoticeTextFormatter.FormatMesoPickup(
                mesoAmount,
                pickedByPet != 0,
                bonusMesoAmount: Math.Max(0, (int)bonusMesoAmount));
            packetMessage = new PickupNoticePacketMessage(
                messages.ScreenMessage,
                PickupMessageType.MesoPickup,
                messages.ChatMessage,
                messages.SecondaryScreenMessage,
                messages.SecondaryScreenColor,
                messages.ChatLogType,
                quantity: mesoAmount,
                mesoAmount: mesoAmount);
            message = "Applied packet-owned drop-pickup meso message.";
            return true;
        }

        private static bool TryDecodeItemPickup(
            byte[] payload,
            int offset,
            Func<int, string> itemNameResolver,
            Func<int, string> itemTypeNameResolver,
            Func<string, float> measureTextWidth,
            out PickupNoticePacketMessage packetMessage,
            out string message)
        {
            packetMessage = default;
            message = null;
            if (!TryReadInt32(payload, ref offset, out int itemId)
                || !TryReadInt32(payload, ref offset, out int quantity))
            {
                message = "Drop-pickup item payload must contain item id and quantity.";
                return false;
            }

            if (!TryRequireNoTrailingBytes(payload, offset, out message))
            {
                return false;
            }

            string itemName = itemNameResolver?.Invoke(itemId);
            string itemTypeName = itemTypeNameResolver?.Invoke(itemId);
            if (!PickupNoticeTextFormatter.TryFormatItemPickup(
                    itemName,
                    itemTypeName,
                    Math.Max(1, quantity),
                    measureTextWidth,
                    out string screenMessage))
            {
                message = $"Drop-pickup item message suppressed because item {itemId} has no resolved client name.";
                return false;
            }

            packetMessage = new PickupNoticePacketMessage(
                screenMessage,
                PickupMessageType.ItemPickup,
                itemId: itemId,
                quantity: Math.Max(1, quantity));
            message = "Applied packet-owned drop-pickup item message.";
            return true;
        }

        private static bool TryDecodeQuestItemPickup(
            byte[] payload,
            int offset,
            Func<int, string> itemNameResolver,
            Func<int, string> itemTypeNameResolver,
            Func<string, float> measureTextWidth,
            out PickupNoticePacketMessage packetMessage,
            out string message)
        {
            packetMessage = default;
            message = null;
            if (!TryReadInt32(payload, ref offset, out int itemId))
            {
                message = "Drop-pickup quest-item payload must contain item id.";
                return false;
            }

            if (!TryRequireNoTrailingBytes(payload, offset, out message))
            {
                return false;
            }

            string itemName = itemNameResolver?.Invoke(itemId);
            string itemTypeName = itemTypeNameResolver?.Invoke(itemId);
            if (!PickupNoticeTextFormatter.TryFormatQuestItemPickup(
                    itemName,
                    itemTypeName,
                    measureTextWidth,
                    out string screenMessage))
            {
                message = $"Drop-pickup quest-item message suppressed because item {itemId} has no resolved client name.";
                return false;
            }

            packetMessage = new PickupNoticePacketMessage(
                screenMessage,
                PickupMessageType.QuestItemPickup,
                itemId: itemId);
            message = "Applied packet-owned drop-pickup quest-item message.";
            return true;
        }

        private static bool TryDecodeFailure(
            byte[] payload,
            int offset,
            DropPickupFailureKind failureKind,
            out PickupNoticePacketMessage packetMessage,
            out string message)
        {
            packetMessage = default;
            message = null;
            if (!TryRequireNoTrailingBytes(payload, offset, out message))
            {
                return false;
            }

            PickupNoticeMessagePair messages = failureKind switch
            {
                DropPickupFailureKind.InventoryFull => PickupNoticeTextFormatter.FormatFailure(Pools.DropPickupFailureReason.InventoryFull),
                DropPickupFailureKind.CantPickup => PickupNoticeTextFormatter.FormatFailure(Pools.DropPickupFailureReason.FieldRestricted),
                _ => PickupNoticeTextFormatter.FormatFailure(Pools.DropPickupFailureReason.NoDropInRange)
            };
            PickupMessageType screenMessageType = failureKind == DropPickupFailureKind.InventoryFull
                ? PickupMessageType.InventoryFull
                : PickupMessageType.CantPickup;

            packetMessage = new PickupNoticePacketMessage(
                messages.ScreenMessage,
                screenMessageType,
                messages.ChatMessage,
                chatLogType: messages.ChatLogType);
            message = failureKind switch
            {
                DropPickupFailureKind.InventoryFull => "Applied packet-owned drop-pickup inventory-full message.",
                DropPickupFailureKind.CantPickup => "Applied packet-owned drop-pickup cannot-acquire message.",
                _ => "Applied packet-owned drop-pickup generic failure message."
            };
            return true;
        }

        private static bool TryReadByte(byte[] payload, ref int offset, out byte value)
        {
            value = 0;
            if (payload == null || offset >= payload.Length)
            {
                return false;
            }

            value = payload[offset++];
            return true;
        }

        private static bool TryReadInt16(byte[] payload, ref int offset, out short value)
        {
            value = 0;
            if (payload == null || offset + sizeof(short) > payload.Length)
            {
                return false;
            }

            value = BitConverter.ToInt16(payload, offset);
            offset += sizeof(short);
            return true;
        }

        private static bool TryReadInt32(byte[] payload, ref int offset, out int value)
        {
            value = 0;
            if (payload == null || offset + sizeof(int) > payload.Length)
            {
                return false;
            }

            value = BitConverter.ToInt32(payload, offset);
            offset += sizeof(int);
            return true;
        }

        private static bool TryRequireNoTrailingBytes(byte[] payload, int offset, out string message)
        {
            int trailingBytes = (payload?.Length ?? 0) - offset;
            if (trailingBytes == 0)
            {
                message = null;
                return true;
            }

            message = $"Drop-pickup message payload has {trailingBytes} trailing byte(s).";
            return false;
        }

        private enum DropPickupFailureKind
        {
            InventoryFull,
            CantPickup,
            Generic
        }
    }
}
