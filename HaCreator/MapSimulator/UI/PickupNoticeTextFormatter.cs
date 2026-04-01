using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using System;

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
        // IDs recovered from CWvsContext::OnDropPickUpMessage in the v95 client.
        public static PickupNoticeSuccessMessages FormatMesoPickup(
            int amount,
            bool pickedByPet = false,
            string sourceName = null,
            int bonusMesoAmount = 0)
        {
            string chatMessage = pickedByPet
                ? $"{FormatActorLabel(sourceName, "Your pet")} picked up some mesos."
                : string.Empty;

            string secondaryScreenMessage = bonusMesoAmount > 0
                ? $"You have gained {bonusMesoAmount} bonus meso(s)."
                : string.Empty;

            return new PickupNoticeSuccessMessages(
                $"You have gained {Math.Max(0, amount)} meso(s).",
                chatMessage,
                secondaryScreenMessage,
                Color.Yellow);
        }

        public static string FormatItemPickup(string itemName, string itemTypeName, int quantity)
        {
            string resolvedItemName = string.IsNullOrWhiteSpace(itemName) ? "Unknown Item" : itemName;
            string resolvedTypeName = string.IsNullOrWhiteSpace(itemTypeName) ? "item" : itemTypeName;

            return quantity > 1
                ? $"You have gained a(n) {resolvedTypeName} ({resolvedItemName}) x {quantity}."
                : $"You have gained a(n) {resolvedTypeName} ({resolvedItemName}).";
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
                    return new PickupNoticeMessagePair("Your inventory is full.", string.Empty);
                case DropPickupFailureReason.OwnershipRestricted:
                    return new PickupNoticeMessagePair("You may not loot this item yet.", "You may not loot this item yet.");
                case DropPickupFailureReason.PetPickupBlocked:
                    return FormatPetPickupBlocked(itemName, sourceName, pickedByPet);
                case DropPickupFailureReason.FieldRestricted:
                    return new PickupNoticeMessagePair("You cannot loot drops in this map.", "You cannot loot drops in this map.");
                case DropPickupFailureReason.Unavailable:
                    return FormatUnavailable(dropType, itemName, quantity, mesoAmount, recentPickup, recentActorName);
                default:
                    return new PickupNoticeMessagePair(string.Empty, string.Empty);
            }
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
                    "A pet picked up the drop.",
                    $"{FormatActorLabel(sourceName, "A pet")} picked up {dropLabel}."),
                DropPickupActorKind.Mob => new PickupNoticeMessagePair(
                    "A monster picked up the drop.",
                    $"{FormatActorLabel(sourceName, "A monster")} picked up {dropLabel}."),
                DropPickupActorKind.Player => new PickupNoticeMessagePair(
                    "Another player picked up the drop.",
                    $"{FormatActorLabel(sourceName, "Another player")} picked up {dropLabel}."),
                _ => new PickupNoticeMessagePair(
                    "Another character picked up the drop.",
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
                return new PickupNoticeMessagePair("Unable to pick up the item.", "Unable to pick up the item.");
            }

            return FormatRemotePickup(recentPickup.ActorKind, dropType, recentActorName, itemName, quantity, mesoAmount);
        }

        private static string FormatActorLabel(string actorName, string fallback)
        {
            return string.IsNullOrWhiteSpace(actorName) ? fallback : actorName;
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
