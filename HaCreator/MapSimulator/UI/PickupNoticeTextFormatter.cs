using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Interaction;
using Microsoft.Xna.Framework;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator.UI
{
    public readonly struct PickupNoticeMessagePair
    {
        public PickupNoticeMessagePair(string screenMessage, string chatMessage)
        {
            ScreenMessage = screenMessage ?? string.Empty;
            ChatMessage = chatMessage ?? string.Empty;
        }

        public string ScreenMessage { get; }
        public string ChatMessage { get; }
    }

    public readonly struct PickupNoticeSuccessMessages
    {
        public PickupNoticeSuccessMessages(
            string screenMessage,
            string chatMessage = "",
            string secondaryScreenMessage = "",
            Color? secondaryScreenColor = null)
        {
            ScreenMessage = screenMessage ?? string.Empty;
            ChatMessage = chatMessage ?? string.Empty;
            SecondaryScreenMessage = secondaryScreenMessage ?? string.Empty;
            SecondaryScreenColor = secondaryScreenColor ?? Color.White;
        }

        public string ScreenMessage { get; }
        public string ChatMessage { get; }
        public string SecondaryScreenMessage { get; }
        public Color SecondaryScreenColor { get; }
    }

    public static class PickupNoticeTextFormatter
    {
        // StringPool IDs recovered from CWvsContext::OnDropPickUpMessage in the v95 client.
        private const int MesoScreenStringPoolId = 0x12F;
        private const int MesoBonusScreenStringPoolId = 0x130;
        private const int MesoPetChatStringPoolId = 0x1491;
        private const int ItemMultiScreenStringPoolId = 0x1542;
        private const int ItemSingleScreenStringPoolId = 0x1543;
        private const int InventoryFullScreenStringPoolId = 0xBD2;
        private const int CantPickupScreenStringPoolId = 0x14D9;
        private const int CantPickupChatStringPoolId = 0x14D3;
        private const int GenericFailureScreenStringPoolId = 0x134;

        public static PickupNoticeSuccessMessages FormatMesoPickup(
            int amount,
            bool pickedByPet = false,
            string sourceName = null,
            int bonusMesoAmount = 0)
        {
            string chatMessage = pickedByPet
                ? FormatClientString(MesoPetChatStringPoolId, $"{FormatActorLabel(sourceName, "Your pet")} picked up some mesos.")
                : string.Empty;

            string secondaryScreenMessage = bonusMesoAmount > 0
                ? FormatClientString(MesoBonusScreenStringPoolId, $"You have gained {bonusMesoAmount} bonus meso(s).", bonusMesoAmount)
                : string.Empty;

            return new PickupNoticeSuccessMessages(
                FormatClientString(MesoScreenStringPoolId, $"You have gained {Math.Max(0, amount)} meso(s).", Math.Max(0, amount)),
                chatMessage,
                secondaryScreenMessage,
                Color.Yellow);
        }

        public static string FormatItemPickup(string itemName, string itemTypeName, int quantity)
        {
            string resolvedItemName = string.IsNullOrWhiteSpace(itemName) ? "Unknown Item" : itemName;
            string resolvedTypeName = string.IsNullOrWhiteSpace(itemTypeName) ? "item" : itemTypeName;

            return quantity > 1
                ? FormatClientString(ItemMultiScreenStringPoolId, $"You have gained a(n) {resolvedTypeName} ({resolvedItemName}) x {quantity}.", resolvedTypeName, resolvedItemName, quantity)
                : FormatClientString(ItemSingleScreenStringPoolId, $"You have gained a(n) {resolvedTypeName} ({resolvedItemName}).", resolvedTypeName, resolvedItemName);
        }

        public static string FormatQuestItemPickup(string itemName, string itemTypeName)
        {
            return FormatItemPickup(itemName, itemTypeName, 1);
        }

        public static PickupNoticeMessagePair FormatFailure(
            DropPickupFailureReason reason,
            string itemName = null,
            DropType dropType = DropType.Item,
            int quantity = 1,
            int mesoAmount = 0,
            bool pickedByPet = false,
            string sourceName = null,
            RecentPickupRecord recentPickup = null,
            string recentActorName = null)
        {
            switch (reason)
            {
                case DropPickupFailureReason.InventoryFull:
                    return new PickupNoticeMessagePair(
                        FormatClientString(InventoryFullScreenStringPoolId, "Your inventory is full."),
                        string.Empty);
                case DropPickupFailureReason.OwnershipRestricted:
                    return FormatCantPickupGeneric();
                case DropPickupFailureReason.PetPickupBlocked:
                    return FormatPetPickupBlocked(itemName, sourceName, pickedByPet);
                case DropPickupFailureReason.FieldRestricted:
                    return FormatCantPickupGeneric();
                case DropPickupFailureReason.Unavailable:
                    return FormatUnavailable(dropType, itemName, quantity, mesoAmount, recentPickup, recentActorName);
                default:
                    return new PickupNoticeMessagePair(
                        FormatClientString(GenericFailureScreenStringPoolId, "Unable to pick up the item."),
                        string.Empty);
            }
        }

        public static PickupMessageType ResolveFailureScreenType(DropPickupFailureReason reason)
        {
            return reason == DropPickupFailureReason.InventoryFull
                ? PickupMessageType.InventoryFull
                : PickupMessageType.CantPickup;
        }

        public static PickupNoticeMessagePair FormatMobPickup(
            DropType dropType,
            string sourceName,
            string itemName = null,
            int quantity = 1,
            int mesoAmount = 0)
        {
            return FormatRemotePickup(
                DropPickupActorKind.Mob,
                dropType,
                sourceName,
                itemName,
                quantity,
                mesoAmount);
        }

        public static PickupNoticeMessagePair FormatRemotePickup(
            DropPickupActorKind actorKind,
            DropType dropType,
            string sourceName,
            string itemName = null,
            int quantity = 1,
            int mesoAmount = 0)
        {
            string dropLabel = FormatDropLabel(dropType, itemName, quantity, mesoAmount);
            return actorKind switch
            {
                DropPickupActorKind.Pet => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "A pet"),
                    $"{FormatActorLabel(sourceName, "A pet")} picked up {dropLabel}."),
                DropPickupActorKind.Mob => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "A monster"),
                    $"{FormatActorLabel(sourceName, "A monster")} picked up {dropLabel}."),
                DropPickupActorKind.Player => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "Another player"),
                    $"{FormatActorLabel(sourceName, "Another player")} picked up {dropLabel}."),
                _ => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "Another character"),
                    $"{FormatActorLabel(sourceName, "Another character")} picked up {dropLabel}.")
            };
        }

        private static PickupNoticeMessagePair FormatPetPickupBlocked(string itemName, string sourceName, bool pickedByPet)
        {
            string actorLabel = pickedByPet && !string.IsNullOrWhiteSpace(sourceName)
                ? sourceName
                : "Your pet";

            if (!string.IsNullOrWhiteSpace(itemName))
            {
                string message = $"{actorLabel} cannot pick up {itemName}.";
                return new PickupNoticeMessagePair(message, message);
            }

            string genericMessage = $"{actorLabel} cannot pick up this item.";
            return new PickupNoticeMessagePair(genericMessage, genericMessage);
        }

        private static PickupNoticeMessagePair FormatUnavailable(
            DropType dropType,
            string itemName,
            int quantity,
            int mesoAmount,
            RecentPickupRecord recentPickup,
            string recentActorName)
        {
            if (recentPickup == null)
            {
                return FormatCantPickupGeneric();
            }

            return FormatRemotePickup(recentPickup.ActorKind, dropType, recentActorName, itemName, quantity, mesoAmount);
        }

        private static PickupNoticeMessagePair FormatCantPickupGeneric()
        {
            return new PickupNoticeMessagePair(
                FormatClientString(CantPickupScreenStringPoolId, "You cannot acquire any items."),
                FormatClientString(CantPickupChatStringPoolId, "You cannot acquire any items because the game file has been damaged. Please try again after reinstalling the game."));
        }

        private static string FormatClientString(int stringPoolId, string fallbackFormat, params object[] args)
        {
            string format = MapleStoryStringPool.GetCompositeFormatOrFallback(stringPoolId, fallbackFormat, args?.Length ?? 0, out _);
            return args == null || args.Length == 0
                ? format
                : string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private static string FormatActorLabel(string actorName, string fallback)
        {
            return string.IsNullOrWhiteSpace(actorName) ? fallback : actorName;
        }

        private static string FormatRemoteScreenMessage(string actorName, string fallback)
        {
            return $"{FormatActorLabel(actorName, fallback)} picked up the drop.";
        }

        private static string FormatDropLabel(DropType dropType, string itemName, int quantity, int mesoAmount)
        {
            if (dropType == DropType.Meso)
            {
                return $"{mesoAmount} meso(s)";
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                return quantity > 1
                    ? $"an item stack x {quantity}"
                    : "an item";
            }

            return quantity > 1
                ? $"{itemName} x {quantity}"
                : itemName;
        }
    }
}
