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

    public readonly struct PickupNoticeFailureContext
    {
        public PickupNoticeFailureContext(
            DropType dropType,
            string itemName,
            int quantity,
            int mesoAmount)
        {
            DropType = dropType;
            ItemName = itemName ?? string.Empty;
            Quantity = Math.Max(1, quantity);
            MesoAmount = Math.Max(0, mesoAmount);
        }

        public DropType DropType { get; }
        public string ItemName { get; }
        public int Quantity { get; }
        public int MesoAmount { get; }
    }

    public static class PickupNoticeTextFormatter
    {
        // StringPool IDs recovered from CWvsContext::OnDropPickUpMessage in the v95 client.
        private const int MesoScreenStringPoolId = 0x12F;
        private const int MesoBonusScreenStringPoolId = 0x130;
        private const int MesoPetChatStringPoolId = 0x1491;
        private const int ItemMultiScreenStringPoolId = 0x1542;
        private const int ItemSingleScreenStringPoolId = 0x1543;
        private const int ContinuationMarkerStringPoolId = 0x8B8;
        private const int InventoryFullScreenStringPoolId = 0xBD2;
        private const int CantPickupScreenStringPoolId = 0x14D9;
        private const int CantPickupChatStringPoolId = 0x14D3;
        private const int GenericFailureScreenStringPoolId = 0x134;
        private const int ItemSingleNameWidth = 120;
        private const int ItemMultiNameWidth = 96;

        public static PickupNoticeSuccessMessages FormatMesoPickup(
            int amount,
            bool pickedByPet = false,
            string sourceName = null,
            int bonusMesoAmount = 0)
        {
            string chatMessage = pickedByPet
                ? FormatClientString(MesoPetChatStringPoolId, "Your pet has picked up some mesos.")
                : string.Empty;

            string secondaryScreenMessage = bonusMesoAmount > 0
                ? FormatClientString(MesoBonusScreenStringPoolId, $"Internet Cafe Meso Bonus (+{bonusMesoAmount})", bonusMesoAmount)
                : string.Empty;

            return new PickupNoticeSuccessMessages(
                FormatClientString(MesoScreenStringPoolId, $"You have gained mesos (+{Math.Max(0, amount)})", Math.Max(0, amount)),
                chatMessage,
                secondaryScreenMessage,
                Color.Yellow);
        }

        public static string FormatItemPickup(string itemName, string itemTypeName, int quantity)
        {
            return FormatItemPickup(itemName, itemTypeName, quantity, null);
        }

        public static string FormatItemPickup(string itemName, string itemTypeName, int quantity, Func<string, float> measureTextWidth)
        {
            if (!CanFormatSuccessPickup(itemName))
            {
                return string.Empty;
            }

            string normalizedItemTypeName = NormalizeItemTypeName(itemTypeName);
            string shapedItemName = ShapePickupItemName(itemName, quantity, measureTextWidth);
            return quantity > 1
                ? FormatClientString(ItemMultiScreenStringPoolId, $"You have gained a(n) {normalizedItemTypeName} ({shapedItemName}) x {quantity}.", normalizedItemTypeName, shapedItemName, quantity)
                : FormatClientString(ItemSingleScreenStringPoolId, $"You have gained a(n) {normalizedItemTypeName} ({shapedItemName}).", normalizedItemTypeName, shapedItemName);
        }

        public static string FormatQuestItemPickup(string itemName, string itemTypeName)
        {
            return FormatQuestItemPickup(itemName, itemTypeName, null);
        }

        public static string FormatQuestItemPickup(string itemName, string itemTypeName, Func<string, float> measureTextWidth)
        {
            return FormatItemPickup(itemName, itemTypeName, 1, measureTextWidth);
        }

        public static bool TryFormatItemPickup(string itemName, string itemTypeName, int quantity, out string message)
        {
            return TryFormatItemPickup(itemName, itemTypeName, quantity, null, out message);
        }

        public static bool TryFormatItemPickup(string itemName, string itemTypeName, int quantity, Func<string, float> measureTextWidth, out string message)
        {
            message = string.Empty;
            if (!CanFormatSuccessPickup(itemName))
            {
                return false;
            }

            message = FormatItemPickup(itemName, itemTypeName, quantity, measureTextWidth);
            return true;
        }

        public static bool TryFormatQuestItemPickup(string itemName, string itemTypeName, out string message)
        {
            return TryFormatQuestItemPickup(itemName, itemTypeName, null, out message);
        }

        public static bool TryFormatQuestItemPickup(string itemName, string itemTypeName, Func<string, float> measureTextWidth, out string message)
        {
            message = string.Empty;
            if (!CanFormatSuccessPickup(itemName))
            {
                return false;
            }

            message = FormatQuestItemPickup(itemName, itemTypeName, measureTextWidth);
            return true;
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
                    return FormatGenericFailure();
            }
        }

        internal static PickupNoticeFailureContext ResolveFailureContext(
            DropType dropType,
            string itemName,
            int quantity,
            int mesoAmount,
            RecentPickupRecord recentPickup,
            Func<int, string> itemNameResolver = null)
        {
            DropType resolvedDropType = dropType;
            string resolvedItemName = itemName;
            int resolvedQuantity = Math.Max(1, quantity);
            int resolvedMesoAmount = Math.Max(0, mesoAmount);

            if (recentPickup != null && !HasResolvedDropContext(dropType, itemName, mesoAmount))
            {
                resolvedDropType = recentPickup.Type;
                resolvedQuantity = Math.Max(1, recentPickup.Quantity);
                resolvedMesoAmount = Math.Max(0, recentPickup.MesoAmount);

                if (resolvedDropType != DropType.Meso
                    && string.IsNullOrWhiteSpace(resolvedItemName)
                    && int.TryParse(recentPickup.ItemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int recentItemId)
                    && recentItemId > 0)
                {
                    resolvedItemName = itemNameResolver?.Invoke(recentItemId) ?? string.Empty;
                }
            }

            return new PickupNoticeFailureContext(
                resolvedDropType,
                resolvedItemName,
                resolvedQuantity,
                resolvedMesoAmount);
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
            string chatMessage = TryFormatRemoteChatMessage(
                actorKind,
                dropType,
                sourceName,
                itemName,
                quantity,
                mesoAmount);
            return actorKind switch
            {
                DropPickupActorKind.Pet => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "A pet"),
                    chatMessage),
                DropPickupActorKind.Mob => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "A monster"),
                    chatMessage),
                DropPickupActorKind.Player => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "Another player"),
                    chatMessage),
                _ => new PickupNoticeMessagePair(
                    FormatRemoteScreenMessage(sourceName, "Another character"),
                    chatMessage)
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
                return FormatGenericFailure();
            }

            return FormatRemotePickup(recentPickup.ActorKind, dropType, recentActorName, itemName, quantity, mesoAmount);
        }

        private static PickupNoticeMessagePair FormatGenericFailure()
        {
            return new PickupNoticeMessagePair(
                FormatClientString(GenericFailureScreenStringPoolId, "You can't get anymore items."),
                string.Empty);
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

        private static bool CanFormatSuccessPickup(string itemName)
        {
            return !string.IsNullOrWhiteSpace(itemName);
        }

        private static bool HasResolvedDropContext(DropType dropType, string itemName, int mesoAmount)
        {
            return dropType == DropType.Meso
                ? mesoAmount > 0
                : !string.IsNullOrWhiteSpace(itemName);
        }

        private static string NormalizeItemTypeName(string itemTypeName)
        {
            return string.IsNullOrWhiteSpace(itemTypeName)
                ? string.Empty
                : itemTypeName;
        }

        private static string ShapePickupItemName(string itemName, int quantity, Func<string, float> measureTextWidth)
        {
            if (string.IsNullOrWhiteSpace(itemName) || measureTextWidth == null)
            {
                return itemName;
            }

            int maxWidth = quantity > 1 ? ItemMultiNameWidth : ItemSingleNameWidth;
            return FormatStringToClientWidth(itemName, maxWidth, measureTextWidth);
        }

        // Mirrors the client `format_string` helper used by CWvsContext::OnDropPickUpMessage:
        // keep the full string when it fits; otherwise reserve the StringPool[0x8B8]
        // continuation marker, take the longest fitting prefix, trim its right edge, and append it.
        internal static string FormatStringToClientWidth(string value, int maxWidth, Func<string, float> measureTextWidth)
        {
            if (string.IsNullOrEmpty(value) || maxWidth <= 0 || measureTextWidth == null)
            {
                return value;
            }

            if (measureTextWidth(value) <= maxWidth)
            {
                return value;
            }

            string continuationMarker = MapleStoryStringPool.GetOrFallback(ContinuationMarkerStringPoolId, "..");
            float prefixWidth = Math.Max(0f, maxWidth - Math.Max(0f, measureTextWidth(continuationMarker)));
            int prefixLength = ResolveLongestFittingPrefixLength(value, prefixWidth, measureTextWidth);
            string prefix = prefixLength > 0
                ? value.Substring(0, prefixLength).TrimEnd()
                : string.Empty;
            return prefix + continuationMarker;
        }

        private static int ResolveLongestFittingPrefixLength(string value, float maxWidth, Func<string, float> measureTextWidth)
        {
            if (maxWidth <= 0f)
            {
                return 0;
            }

            int low = 0;
            int high = value.Length;
            while (low < high)
            {
                int mid = low + ((high - low + 1) / 2);
                if (measureTextWidth(value.Substring(0, mid)) <= maxWidth)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        private static string FormatRemoteScreenMessage(string actorName, string fallback)
        {
            return $"{FormatActorLabel(actorName, fallback)} picked up the drop.";
        }

        private static string TryFormatRemoteChatMessage(
            DropPickupActorKind actorKind,
            DropType dropType,
            string actorName,
            string itemName,
            int quantity,
            int mesoAmount)
        {
            string dropLabel = TryFormatDropLabel(dropType, itemName, quantity, mesoAmount);
            if (string.IsNullOrWhiteSpace(dropLabel))
            {
                return string.Empty;
            }

            string fallback = actorKind switch
            {
                DropPickupActorKind.Pet => "A pet",
                DropPickupActorKind.Mob => "A monster",
                DropPickupActorKind.Player => "Another player",
                _ => "Another character"
            };

            return $"{FormatActorLabel(actorName, fallback)} picked up {dropLabel}.";
        }

        private static string TryFormatDropLabel(DropType dropType, string itemName, int quantity, int mesoAmount)
        {
            if (dropType == DropType.Meso)
            {
                return $"{mesoAmount} mesos";
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                return string.Empty;
            }

            return quantity > 1
                ? $"{itemName} x {quantity}"
                : itemName;
        }
    }
}
