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
                    return new PickupNoticeMessagePair("Your inventory is full.", "Your inventory is full.");
                case DropPickupFailureReason.OwnershipRestricted:
                    return new PickupNoticeMessagePair("You may not loot this item yet.", "You may not loot this item yet.");
                case DropPickupFailureReason.PetPickupBlocked:
                    return FormatPetPickupBlocked(itemName, sourceName, pickedByPet);
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

            string dropLabel = FormatDropLabel(dropType, itemName, quantity, mesoAmount);
            return recentPickup.ActorKind switch
            {
                DropPickupActorKind.Pet => new PickupNoticeMessagePair(
                    "A pet picked up the drop.",
                    $"{FormatActorLabel(recentActorName, "A pet")} picked up {dropLabel}."),
                DropPickupActorKind.Mob => new PickupNoticeMessagePair(
                    "A monster picked up the drop.",
                    $"{FormatActorLabel(recentActorName, "A monster")} picked up {dropLabel}."),
                DropPickupActorKind.Player => new PickupNoticeMessagePair(
                    "Another player picked up the drop.",
                    $"{FormatActorLabel(recentActorName, "Another player")} picked up {dropLabel}."),
                _ => new PickupNoticeMessagePair(
                    "Another character picked up the drop.",
                    $"{FormatActorLabel(recentActorName, "Another character")} picked up {dropLabel}.")
            };
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
