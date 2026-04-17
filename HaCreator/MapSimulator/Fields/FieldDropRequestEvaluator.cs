using HaCreator.MapSimulator.UI;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Globalization;

namespace HaCreator.MapSimulator.Fields
{
    public readonly record struct LocalFieldItemDropRequest(
        InventoryType InventoryType,
        int SlotIndex,
        int ItemId,
        int Quantity);

    public readonly record struct LocalFieldMesoDropRequest(int Amount);

    public static class FieldDropRequestEvaluator
    {
        public const int ClientChangeSlotPositionRequestOpcode = 77;
        public const int ClientDropMoneyRequestOpcode = 106;
        public const int ClientMaxMesoDropAmount = 50_000;

        public static bool ShouldPromptForItemDropQuantity(
            InventoryType inventoryType,
            InventorySlotData slotData)
        {
            if (inventoryType == InventoryType.NONE || slotData == null || slotData.ItemId <= 0)
            {
                return false;
            }

            int stackQuantity = Math.Max(1, slotData.Quantity);
            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(inventoryType, slotData.MaxStackSize);
            return inventoryType != InventoryType.EQUIP
                && maxStackSize > 1
                && stackQuantity > 1;
        }

        public static int ResolveDropPromptQuantityText(string text, int maximum)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) && amount > 0
                ? Math.Clamp(amount, 1, Math.Max(1, maximum))
                : 1;
        }

        public static bool TryAppendDropPromptQuantityDigit(
            string currentText,
            char digit,
            int maximum,
            out string normalizedText,
            out int normalizedQuantity)
        {
            normalizedText = currentText ?? string.Empty;
            normalizedQuantity = ResolveDropPromptQuantityText(normalizedText, maximum);

            if (!char.IsDigit(digit))
            {
                return false;
            }

            string candidate = normalizedText + digit;
            if (!long.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedAmount))
            {
                return false;
            }

            if (parsedAmount <= 0)
            {
                normalizedText = string.Empty;
                normalizedQuantity = 1;
                return true;
            }

            normalizedQuantity = (int)Math.Min(parsedAmount, Math.Max(1, maximum));
            normalizedText = normalizedQuantity.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        public static bool TryAppendMesoDropPromptAmountDigit(
            string currentText,
            char digit,
            long maximum,
            out string normalizedText)
        {
            normalizedText = currentText ?? string.Empty;

            if (!char.IsDigit(digit))
            {
                return false;
            }

            string candidate = normalizedText + digit;
            if (!long.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedAmount))
            {
                return false;
            }

            if (parsedAmount <= 0)
            {
                normalizedText = string.Empty;
                return true;
            }

            long normalizedAmount = Math.Min(parsedAmount, ResolveClientMesoDropMaximum(maximum));
            normalizedText = normalizedAmount.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        public static byte[] BuildClientMesoDropRequestPayload(int currentTimeMs, int amount)
        {
            byte[] payload = new byte[sizeof(int) * 2];
            BitConverter.GetBytes(currentTimeMs).CopyTo(payload, 0);
            BitConverter.GetBytes(amount).CopyTo(payload, sizeof(int));
            return payload;
        }

        public static byte[] BuildClientItemDropRequestPayload(
            int currentTimeMs,
            InventoryType inventoryType,
            int sourceSlotPosition,
            int dropCount)
        {
            byte[] payload = new byte[sizeof(int) + sizeof(byte) + sizeof(short) * 3];
            BitConverter.GetBytes(currentTimeMs).CopyTo(payload, 0);
            payload[sizeof(int)] = (byte)inventoryType;
            BitConverter.GetBytes((short)sourceSlotPosition).CopyTo(payload, sizeof(int) + sizeof(byte));
            BitConverter.GetBytes((short)0).CopyTo(payload, sizeof(int) + sizeof(byte) + sizeof(short));
            BitConverter.GetBytes((short)dropCount).CopyTo(payload, sizeof(int) + sizeof(byte) + sizeof(short) * 2);
            return payload;
        }

        public static bool TryResolveLocalItemDropRequest(
            long fieldLimit,
            InventoryType inventoryType,
            int slotIndex,
            InventorySlotData slotData,
            int? requestedQuantity,
            out LocalFieldItemDropRequest request,
            out string restrictionMessage)
        {
            request = default;
            restrictionMessage = null;

            if (inventoryType == InventoryType.NONE
                || slotIndex < 0
                || slotData == null
                || slotData.ItemId <= 0)
            {
                restrictionMessage = "The drop request is invalid.";
                return false;
            }

            string fieldRestrictionMessage = FieldInteractionRestrictionEvaluator.GetDropRequestRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(fieldRestrictionMessage))
            {
                restrictionMessage = fieldRestrictionMessage;
                return false;
            }

            int stackQuantity = Math.Max(1, slotData.Quantity);
            int normalizedQuantity = requestedQuantity ?? stackQuantity;
            if (normalizedQuantity <= 0 || normalizedQuantity > stackQuantity)
            {
                restrictionMessage = "The drop request quantity is invalid.";
                return false;
            }

            request = new LocalFieldItemDropRequest(
                inventoryType,
                slotIndex,
                slotData.ItemId,
                normalizedQuantity);
            return true;
        }

        public static bool TryResolveLocalMesoDropRequest(
            long fieldLimit,
            long currentMeso,
            long requestedAmount,
            out LocalFieldMesoDropRequest request,
            out string restrictionMessage)
        {
            request = default;
            restrictionMessage = null;

            string fieldRestrictionMessage = FieldInteractionRestrictionEvaluator.GetDropRequestRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(fieldRestrictionMessage))
            {
                restrictionMessage = fieldRestrictionMessage;
                return false;
            }

            if (requestedAmount <= 0)
            {
                restrictionMessage = "The meso drop amount is invalid.";
                return false;
            }

            if (requestedAmount > int.MaxValue)
            {
                restrictionMessage = "The meso drop amount is too large.";
                return false;
            }

            if (requestedAmount > ClientMaxMesoDropAmount)
            {
                restrictionMessage = "The meso drop amount exceeds the client drop limit.";
                return false;
            }

            if (currentMeso < requestedAmount)
            {
                restrictionMessage = "You do not have enough mesos to drop that amount.";
                return false;
            }

            request = new LocalFieldMesoDropRequest((int)requestedAmount);
            return true;
        }

        private static long ResolveClientMesoDropMaximum(long currentMeso)
        {
            return Math.Min(Math.Max(1L, currentMeso), ClientMaxMesoDropAmount);
        }
    }
}
