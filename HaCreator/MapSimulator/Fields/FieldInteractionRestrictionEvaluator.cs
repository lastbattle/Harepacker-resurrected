using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldInteractionRestrictionEvaluator
    {
        private const int PortalScrollItemGroup = 203;
        private const int SummonSackItemGroup = 210;
        private const int AntiMacroItemGroup = 219;
        private const int CashWeatherItemGroup = 512;
        private const int PetNameTagItemGroup = 517;
        private const int PetReviveItemGroup = 518;
        private const int PetSkillItemGroup = 519;
        private const int NearestTownPortalScrollItemId = 2030000;

        public static bool CanTransferField(long fieldLimit)
        {
            return GetTransferRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanJump(long fieldLimit)
        {
            return GetJumpRestrictionMessage(fieldLimit) == null;
        }

        public static string GetTeleportItemRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Teleport_Item.Check(fieldLimit)
                ? "Teleport items cannot be used in this map."
                : null;
        }

        public static string GetTransferRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Migrate.Check(fieldLimit)
                ? "This field forbids map transfer."
                : null;
        }

        public static string GetMiniGameRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Open_Mini_Game.Check(fieldLimit)
                ? "Mini-game and shop rooms cannot be opened in this map."
                : null;
        }

        public static string GetParcelOpenRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Parcel_Open_Limit.Check(fieldLimit)
                ? "Parcel-owned delivery and mailbox windows cannot be opened in this map."
                : null;
        }

        public static string GetQuestAlertRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.No_Quest_Alert.Check(fieldLimit)
                ? "Quest alert windows are disabled in this map."
                : null;
        }

        public static string GetAndroidRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.No_Android.Check(fieldLimit)
                ? "Android companion features are disabled in this map."
                : null;
        }

        public static string GetPartyBossRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Change_Party_Boss.Check(fieldLimit)
                ? "Party leader changes are disabled in this map."
                : null;
        }

        public static string GetDropRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Drop_Limit.Check(fieldLimit)
                ? "Field drop interactions are restricted in this map."
                : null;
        }

        public static string GetDropPickupRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Drop_Limit.Check(fieldLimit)
                ? "Drops cannot be looted in this map."
                : null;
        }

        public static string GetJumpDownRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Fall_Down.Check(fieldLimit)
                ? "Dropping down through footholds is disabled in this map."
                : null;
        }

        public static string GetFallingDamageRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.No_Damage_On_Falling.Check(fieldLimit)
                ? "Falling damage is disabled in this map."
                : null;
        }

        public static bool ShouldAutoExpandMinimap(long fieldLimit)
        {
            return FieldLimitType.Auto_Expand_Minimap.Check(fieldLimit);
        }

        public static string GetAutoExpandMinimapMessage(long fieldLimit)
        {
            return ShouldAutoExpandMinimap(fieldLimit)
                ? "The minimap automatically expands in this map."
                : null;
        }

        public static string GetPetRuntimeRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Pet.Check(fieldLimit)
                ? "Pet runtime interactions are disabled in this map."
                : null;
        }

        public static string GetMapTransferRestrictionMessage(long fieldLimit)
        {
            return GetTeleportItemRestrictionMessage(fieldLimit) ?? GetTransferRestrictionMessage(fieldLimit);
        }

        public static bool CanPickupDrops(long fieldLimit)
        {
            return GetDropPickupRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanUseAndroid(long fieldLimit)
        {
            return GetAndroidRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanChangePartyLeader(long fieldLimit)
        {
            return GetPartyBossRestrictionMessage(fieldLimit) == null;
        }

        public static string GetItemUseRestrictionMessage(
            long fieldLimit,
            InventoryType inventoryType,
            int itemId,
            string itemName,
            string itemDescription,
            bool isStatChangeConsumable)
        {
            if (itemId <= 0 || inventoryType == InventoryType.NONE)
            {
                return null;
            }

            if (FieldLimitType.Unable_To_Use_Portal_Scroll.Check(fieldLimit) && IsPortalScrollItem(inventoryType, itemId))
            {
                return "Portal scrolls cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Use_Specific_Portal_Scroll.Check(fieldLimit) && IsSpecificPortalScrollItem(inventoryType, itemId))
            {
                return "Destination portal scrolls cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Use_Summon_Item.Check(fieldLimit) && IsSummonItem(inventoryType, itemId))
            {
                return "The summon item cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Consume_Stat_Change_Item.Check(fieldLimit) && isStatChangeConsumable)
            {
                return "Stat-change consumables cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Use_Wedding_Invitation_Item.Check(fieldLimit) && IsWeddingInvitationItem(inventoryType, itemName, itemDescription))
            {
                return "Wedding invitation items cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Use_Cash_Weather.Check(fieldLimit) && IsCashWeatherItem(inventoryType, itemId))
            {
                return "Cash weather items cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Use_Pet.Check(fieldLimit) && IsPetInteractionItem(inventoryType, itemId, itemName, itemDescription))
            {
                return "Pet items cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Use_AntiMacro_Item.Check(fieldLimit) && IsAntiMacroItem(inventoryType, itemId, itemName, itemDescription))
            {
                return "Anti-macro items cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Summon_NPC.Check(fieldLimit) && IsNpcSummonItem(itemName, itemDescription))
            {
                return "NPC-summon items cannot be used in this field.";
            }

            return null;
        }

        public static bool IsStatChangeConsumable(
            bool hasRecoveryEffect,
            bool hasTemporaryBuffEffect,
            bool hasMorphEffect,
            bool hasCureEffect)
        {
            return hasRecoveryEffect
                || hasTemporaryBuffEffect
                || hasMorphEffect
                || hasCureEffect;
        }

        public static IReadOnlyList<string> GetFieldEntryItemRestrictionMessages(long fieldLimit)
        {
            List<string> messages = new();

            if (FieldLimitType.Unable_To_Use_Portal_Scroll.Check(fieldLimit))
            {
                messages.Add("Portal scroll use is disabled in this map.");
            }

            if (FieldLimitType.Unable_To_Use_Specific_Portal_Scroll.Check(fieldLimit))
            {
                messages.Add("Destination portal scroll use is disabled in this map.");
            }

            if (FieldLimitType.Unable_To_Use_Summon_Item.Check(fieldLimit))
            {
                messages.Add("Monster summon items are disabled in this map.");
            }

            if (FieldLimitType.Unable_To_Consume_Stat_Change_Item.Check(fieldLimit))
            {
                messages.Add("Stat-change consumables are disabled in this map.");
            }

            if (FieldLimitType.Unable_To_Use_Wedding_Invitation_Item.Check(fieldLimit))
            {
                messages.Add("Wedding invitation items are disabled in this map.");
            }

            if (FieldLimitType.Unable_To_Use_Cash_Weather.Check(fieldLimit))
            {
                messages.Add("Cash weather items are disabled in this map.");
            }

            if (FieldLimitType.Unable_To_Use_Pet.Check(fieldLimit))
            {
                messages.Add("Pet item interactions are disabled in this map.");
            }

            string petRuntimeMessage = GetPetRuntimeRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(petRuntimeMessage))
            {
                messages.Add(petRuntimeMessage);
            }

            if (FieldLimitType.Unable_To_Use_AntiMacro_Item.Check(fieldLimit))
            {
                messages.Add("Anti-macro items are disabled in this map.");
            }

            if (FieldLimitType.Unable_To_Summon_NPC.Check(fieldLimit))
            {
                messages.Add("NPC-summon items are disabled in this map.");
            }

            string miniGameMessage = GetMiniGameRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(miniGameMessage))
            {
                messages.Add(miniGameMessage);
            }

            return messages;
        }

        public static IReadOnlyList<string> GetFieldEntryInteractionRestrictionMessages(long fieldLimit)
        {
            List<string> messages = new();
            AddFieldEntryMessage(messages, GetParcelOpenRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetQuestAlertRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetAndroidRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetPartyBossRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetDropRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetJumpDownRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetFallingDamageRestrictionMessage(fieldLimit));
            return messages;
        }

        public static bool CanRegisterMapTransferDestination(int mapId)
        {
            return GetMapTransferRegistrationRestrictionMessage(mapId) == null;
        }

        public static string GetMapTransferRegistrationRestrictionMessage(int mapId)
        {
            if (mapId <= 0 || mapId == MapConstants.MaxMap)
            {
                return "This destination cannot be saved in a teleport slot.";
            }

            if (mapId < 100_000_000)
            {
                return "Only regular field maps can be saved in a teleport slot.";
            }

            int millionGroup = (mapId / 1_000_000) % 100;
            return millionGroup == 9
                ? "This destination cannot be saved in a teleport slot."
                : null;
        }

        public static string GetJumpRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Jump.Check(fieldLimit)
                ? "Jumping is disabled in this map."
                : null;
        }

        private static void AddFieldEntryMessage(ICollection<string> messages, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        private static bool IsPortalScrollItem(InventoryType inventoryType, int itemId)
        {
            return inventoryType == InventoryType.USE && (itemId / 10000) == PortalScrollItemGroup;
        }

        private static bool IsSpecificPortalScrollItem(InventoryType inventoryType, int itemId)
        {
            return IsPortalScrollItem(inventoryType, itemId) && itemId != NearestTownPortalScrollItemId;
        }

        private static bool IsSummonItem(InventoryType inventoryType, int itemId)
        {
            return inventoryType == InventoryType.USE && (itemId / 10000) == SummonSackItemGroup;
        }

        private static bool IsCashWeatherItem(InventoryType inventoryType, int itemId)
        {
            return inventoryType == InventoryType.CASH && (itemId / 10000) == CashWeatherItemGroup;
        }

        private static bool IsAntiMacroItem(InventoryType inventoryType, int itemId, string itemName, string itemDescription)
        {
            return (inventoryType == InventoryType.USE && (itemId / 10000) == AntiMacroItemGroup)
                   || ContainsPhrase(itemName, "lie detector")
                   || ContainsPhrase(itemDescription, "lie detector");
        }

        private static bool IsWeddingInvitationItem(InventoryType inventoryType, string itemName, string itemDescription)
        {
            if (inventoryType is not (InventoryType.CASH or InventoryType.ETC))
            {
                return false;
            }

            return ContainsPhrase(itemName, "wedding invitation")
                   || ContainsPhrase(itemName, "wedding invitation card")
                   || ContainsPhrase(itemName, "wedding invitation ticket")
                   || ContainsPhrase(itemDescription, "wedding invitation");
        }

        private static bool IsNpcSummonItem(string itemName, string itemDescription)
        {
            return ContainsPhrase(itemName, "summon npc")
                   || ContainsPhrase(itemDescription, "summon npc")
                   || ContainsPhrase(itemDescription, "summons NPC");
        }

        private static bool IsPetInteractionItem(InventoryType inventoryType, int itemId, string itemName, string itemDescription)
        {
            int itemGroup = itemId / 10000;
            if (inventoryType == InventoryType.CASH
                && itemGroup is PetNameTagItemGroup or PetReviveItemGroup or PetSkillItemGroup)
            {
                return true;
            }

            return ContainsWholeWord(itemName, "pet") || ContainsWholeWord(itemDescription, "pet");
        }

        private static bool ContainsPhrase(string text, string phrase)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsWholeWord(string text, string word)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(word))
            {
                return false;
            }

            int startIndex = 0;
            while (true)
            {
                int index = text.IndexOf(word, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                bool startBoundary = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                int endIndex = index + word.Length;
                bool endBoundary = endIndex >= text.Length || !char.IsLetterOrDigit(text[endIndex]);
                if (startBoundary && endBoundary)
                {
                    return true;
                }

                startIndex = index + word.Length;
            }
        }
    }
}
