using HaCreator.MapSimulator.Pools;

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

    public static class PickupNoticeTextFormatter
    {
        public static PickupNoticeMessagePair FormatFailure(
            DropPickupFailureReason reason,
            string itemName = null,
            bool pickedByPet = false,
            string sourceName = null)
        {
            switch (reason)
            {
                case DropPickupFailureReason.InventoryFull:
                    return new PickupNoticeMessagePair("Your inventory is full.", string.Empty);
                case DropPickupFailureReason.OwnershipRestricted:
                    return new PickupNoticeMessagePair("You may not loot this item yet.", "You may not loot this item yet.");
                case DropPickupFailureReason.PetPickupBlocked:
                    return FormatPetPickupBlocked(itemName, sourceName, pickedByPet);
                case DropPickupFailureReason.Unavailable:
                    return new PickupNoticeMessagePair("Unable to pick up the item.", "Unable to pick up the item.");
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
            string actorLabel = string.IsNullOrWhiteSpace(sourceName) ? "A monster" : sourceName;
            string dropLabel = FormatDropLabel(dropType, itemName, quantity, mesoAmount);
            return new PickupNoticeMessagePair(
                "A monster picked up the drop.",
                $"{actorLabel} picked up {dropLabel}.");
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
