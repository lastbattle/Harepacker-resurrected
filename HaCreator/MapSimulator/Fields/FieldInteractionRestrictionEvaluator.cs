using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldInteractionRestrictionEvaluator
    {
        private const string GenericMapTransferRegistrationRestrictionMessage = "This destination cannot be saved in a teleport slot.";
        private const string RegularFieldMapTransferRegistrationRestrictionMessage = "Only regular field maps can be saved in a teleport slot.";
        private const int PortalScrollItemGroup = 203;
        private const int UpgradeScrollItemGroup = 204;
        private const int SummonSackItemGroup = 210;
        private const int AntiMacroItemGroup = 219;
        private const int CashWeatherItemGroup = 512;
        private const int PetNameTagItemGroup = 517;
        private const int PetReviveItemGroup = 518;
        private const int PetSkillItemGroup = 519;
        private const int NearestTownPortalScrollItemId = 2030000;
        private const int NpcSummonScriptItemId = 2430011;
        private const int NpcSummonQuestItemId = 4032363;
        private const string SummonEventNpcScriptName = "summonEventNpc";
        private const int CashPetItemGroup = 500;
        private const int PetLifeRecoveryItemGroup = 513;
        private const int PetFoodItemGroup = 524;

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

        public static string GetTamingMobRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Taming_Mob.Check(fieldLimit)
                ? "Taming-mob and mechanic vehicle interactions are disabled in this map."
                : null;
        }

        public static bool CanUseTamingMob(long fieldLimit)
        {
            return GetTamingMobRestrictionMessage(fieldLimit) == null;
        }

        public static string GetTransferRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Migrate.Check(fieldLimit)
                ? "This field forbids map transfer."
                : null;
        }

        public static string GetChannelShiftRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Migrate.Check(fieldLimit)
                ? "This field forbids channel changes."
                : null;
        }

        public static string GetChannelShiftRestrictionMessage(long fieldLimit, MapInfo mapInfo)
        {
            string fieldLimitRestrictionMessage = GetChannelShiftRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(fieldLimitRestrictionMessage))
            {
                return fieldLimitRestrictionMessage;
            }

            return IsInfoFlagSet(mapInfo, "shiftChannelForbidden")
                ? "This map forbids channel changes."
                : null;
        }

        public static string GetFollowCharacterRestrictionMessage(MapInfo mapInfo)
        {
            return mapInfo?.nofollowCharacter == true
                ? "Follow-character requests are disabled in this map."
                : null;
        }

        public static string GetMiniGameRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Open_Mini_Game.Check(fieldLimit)
                ? "Mini-game and shop rooms cannot be opened in this map."
                : null;
        }

        public static string GetSocialRoomRestrictionMessage(
            long fieldLimit,
            MapInfo mapInfo,
            SocialRoomKind kind)
        {
            if (!IsFieldRestrictedSocialRoomKind(kind))
            {
                return null;
            }

            string fieldLimitRestriction = GetMiniGameRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(fieldLimitRestriction))
            {
                return fieldLimitRestriction;
            }

            return kind switch
            {
                SocialRoomKind.PersonalShop when mapInfo?.personalShop == false =>
                    "Personal shops cannot be opened in this map.",
                SocialRoomKind.EntrustedShop when mapInfo?.entrustedShop == false =>
                    "Entrusted shops cannot be opened in this map.",
                _ => null
            };
        }

        public static bool IsFieldRestrictedSocialRoomKind(SocialRoomKind kind)
        {
            return kind is SocialRoomKind.MiniRoom
                or SocialRoomKind.PersonalShop
                or SocialRoomKind.EntrustedShop
                or SocialRoomKind.TradingRoom;
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

        public static string GetCashWeatherRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Cash_Weather.Check(fieldLimit)
                ? "Cash weather items cannot be used in this field."
                : null;
        }

        public static string GetAndroidRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.No_Android.Check(fieldLimit)
                ? "Android companion features are disabled in this map."
                : null;
        }

        public static string GetAndroidRestrictionMessage(long fieldLimit, MapInfo mapInfo)
        {
            string fieldLimitRestrictionMessage = GetAndroidRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(fieldLimitRestrictionMessage))
            {
                return fieldLimitRestrictionMessage;
            }

            return IsInfoFlagSet(mapInfo, "vanishAndroid")
                ? "Android companion features are disabled in this map."
                : null;
        }

        public static string GetDragonCompanionRestrictionMessage(MapInfo mapInfo)
        {
            return mapInfo?.fieldType == FieldType.FIELDTYPE_NODRAGON
                   || mapInfo?.vanishDragon == true
                ? "Dragon companion features are disabled in this map."
                : null;
        }

        public static bool CanUseDragonCompanion(MapInfo mapInfo)
        {
            return GetDragonCompanionRestrictionMessage(mapInfo) == null;
        }

        public static string GetPartyBossRestrictionMessage(long fieldLimit)
        {
            return GetPartyBossRestrictionMessage(fieldLimit, mapInfo: null);
        }

        public static string GetPartyBossRestrictionMessage(long fieldLimit, MapInfo mapInfo)
        {
            return FieldLimitType.Unable_To_Change_Party_Boss.Check(fieldLimit)
                || mapInfo?.blockPBossChange == true
                ? "Party leader changes are disabled in this map."
                : null;
        }

        public static string GetShopOpenRestrictionMessage(MapInfo mapInfo)
        {
            return IsInfoFlagSet(mapInfo, "limitUseShop")
                ? "Shop windows cannot be opened in this map."
                : null;
        }

        public static string GetTrunkOpenRestrictionMessage(MapInfo mapInfo)
        {
            return IsInfoFlagSet(mapInfo, "limitUseTrunk")
                ? "Storage windows cannot be opened in this map."
                : null;
        }

        public static string GetPortableChairRestrictionMessage(MapInfo mapInfo)
        {
            return IsInfoFlagSet(mapInfo, "noChair")
                ? "Portable chairs cannot be used in this map."
                : null;
        }

        public static string GetLandingRestrictionMessage(MapInfo mapInfo)
        {
            return IsInfoFlagSet(mapInfo, "noLanding")
                ? "Foothold landing is disabled in this map."
                : null;
        }

        public static bool CanLandOnFoothold(MapInfo mapInfo)
        {
            return GetLandingRestrictionMessage(mapInfo) == null;
        }

        public static string GetActiveSkillCancelRestrictionMessage(MapInfo mapInfo)
        {
            return IsInfoFlagSet(mapInfo, "noCancelSkill")
                ? "Active skill cancellation is disabled in this field."
                : null;
        }

        internal static string GetExpeditionPartyBossChangeRestrictionMessage(
            long fieldLimit,
            ExpeditionIntermediaryOutboundRequestKind requestKind)
        {
            return GetExpeditionPartyBossChangeRestrictionMessage(fieldLimit, mapInfo: null, requestKind);
        }

        internal static string GetExpeditionPartyBossChangeRestrictionMessage(
            long fieldLimit,
            MapInfo mapInfo,
            ExpeditionIntermediaryOutboundRequestKind requestKind)
        {
            return requestKind == ExpeditionIntermediaryOutboundRequestKind.ChangePartyBoss
                ? GetPartyBossRestrictionMessage(fieldLimit, mapInfo)
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

        public static string GetDropRequestRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Drop_Limit.Check(fieldLimit)
                ? "Items cannot be dropped in this map."
                : null;
        }

        public static string GetDropSpawnRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Drop_Limit.Check(fieldLimit)
                ? "Field drops cannot spawn in this map."
                : null;
        }

        public static string GetMonsterCapacityLimitMessage(long fieldLimit)
        {
            return FieldLimitType.No_Monster_Capacity_Limit.Check(fieldLimit)
                ? "Monster capacity limits are disabled in this map."
                : null;
        }

        public static string GetExpDecreaseRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.No_EXP_Decrease.Check(fieldLimit)
                ? "EXP loss on death is disabled in this map."
                : null;
        }

        public static string GetItemOptionLimitMessage(long fieldLimit)
        {
            return FieldLimitType.No_Item_Option_Limit.Check(fieldLimit)
                ? "Item option limits are disabled in this map."
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

        internal static string GetWindowRestrictionMessage(long fieldLimit, string windowName)
        {
            if (string.Equals(windowName, MapSimulatorWindowNames.QuestTimer, StringComparison.Ordinal) ||
                string.Equals(windowName, MapSimulatorWindowNames.QuestTimerAction, StringComparison.Ordinal) ||
                MapSimulatorWindowNames.IsQuestTimerRuntimeWindowName(windowName))
            {
                return GetQuestAlertRestrictionMessage(fieldLimit);
            }

            return windowName switch
            {
                MapSimulatorWindowNames.MemoMailbox or
                MapSimulatorWindowNames.MemoSend or
                MapSimulatorWindowNames.MemoGet or
                MapSimulatorWindowNames.QuestDelivery =>
                    GetParcelOpenRestrictionMessage(fieldLimit),
                MapSimulatorWindowNames.QuestAlarm =>
                    GetQuestAlertRestrictionMessage(fieldLimit),
                _ => null
            };
        }

        public static string GetWindowOpenRestrictionMessage(
            long fieldLimit,
            MapInfo mapInfo,
            string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
            {
                return null;
            }

            string windowRestrictionMessage = GetWindowRestrictionMessage(fieldLimit, windowName);
            if (!string.IsNullOrWhiteSpace(windowRestrictionMessage))
            {
                return windowRestrictionMessage;
            }

            string socialRoomRestrictionMessage = windowName switch
            {
                MapSimulatorWindowNames.MiniRoom => GetSocialRoomRestrictionMessage(fieldLimit, mapInfo, SocialRoomKind.MiniRoom),
                MapSimulatorWindowNames.PersonalShop => GetSocialRoomRestrictionMessage(fieldLimit, mapInfo, SocialRoomKind.PersonalShop),
                MapSimulatorWindowNames.EntrustedShop => GetSocialRoomRestrictionMessage(fieldLimit, mapInfo, SocialRoomKind.EntrustedShop),
                MapSimulatorWindowNames.TradingRoom => GetSocialRoomRestrictionMessage(fieldLimit, mapInfo, SocialRoomKind.TradingRoom),
                MapSimulatorWindowNames.CashTradingRoom => GetSocialRoomRestrictionMessage(fieldLimit, mapInfo, SocialRoomKind.TradingRoom),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(socialRoomRestrictionMessage))
            {
                return socialRoomRestrictionMessage;
            }

            string fieldMetadataWindowRestrictionMessage = windowName switch
            {
                MapSimulatorWindowNames.NpcShop or
                MapSimulatorWindowNames.CashShop =>
                    GetShopOpenRestrictionMessage(mapInfo),
                MapSimulatorWindowNames.StoreBank or
                MapSimulatorWindowNames.Trunk =>
                    GetTrunkOpenRestrictionMessage(mapInfo),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(fieldMetadataWindowRestrictionMessage))
            {
                return fieldMetadataWindowRestrictionMessage;
            }

            return windowName == MapSimulatorWindowNames.MapTransfer
                ? GetMapTransferWindowRestrictionMessage(fieldLimit, mapInfo)
                : null;
        }

        public static bool CanTakeFallingDamage(long fieldLimit)
        {
            return !FieldLimitType.No_Damage_On_Falling.Check(fieldLimit);
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

        public static bool ResolvePacketOwnedMiniMapVisibility(
            long fieldLimit,
            MapInfo mapInfo,
            bool requestedVisible,
            out string overrideMessage)
        {
            if (mapInfo?.hideMinimap == true)
            {
                overrideMessage = "Map metadata keeps the minimap hidden in this field.";
                return false;
            }

            if (!requestedVisible && ShouldAutoExpandMinimap(fieldLimit))
            {
                overrideMessage = "Field rules keep the minimap expanded in this map.";
                return true;
            }

            overrideMessage = null;
            return requestedVisible;
        }

        public static string GetPetRuntimeRestrictionMessage(long fieldLimit)
        {
            return FieldLimitType.Unable_To_Use_Pet.Check(fieldLimit)
                ? "Pet runtime interactions are disabled in this map."
                : null;
        }

        public static string GetPetRuntimeRestrictionMessage(long fieldLimit, MapInfo mapInfo)
        {
            string fieldLimitRestrictionMessage = GetPetRuntimeRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(fieldLimitRestrictionMessage))
            {
                return fieldLimitRestrictionMessage;
            }

            return IsInfoFlagSet(mapInfo, "vanishPet")
                ? "Pet runtime interactions are disabled in this map."
                : null;
        }

        public static string GetMapTransferRestrictionMessage(long fieldLimit)
        {
            return GetTeleportItemRestrictionMessage(fieldLimit) ?? GetTransferRestrictionMessage(fieldLimit);
        }

        public static string GetMapTransferRestrictionMessage(long fieldLimit, MapInfo mapInfo)
        {
            return GetMapTransferEntryRestrictionMessage(mapInfo, context: null)
                ?? GetMapTransferRestrictionMessage(fieldLimit);
        }

        private static string GetMapTransferWindowRestrictionMessage(long fieldLimit, MapInfo mapInfo)
        {
            string entryRestrictionMessage = GetMapTransferEntryRestrictionMessage(mapInfo, context: null);
            if (!string.IsNullOrWhiteSpace(entryRestrictionMessage))
            {
                return entryRestrictionMessage;
            }

            return GetMapTransferRestrictionMessage(fieldLimit);
        }

        public static bool CanPickupDrops(long fieldLimit)
        {
            return GetDropPickupRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanRequestDrop(long fieldLimit)
        {
            return GetDropRequestRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanSpawnDrops(long fieldLimit)
        {
            return GetDropSpawnRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanUseAndroid(long fieldLimit)
        {
            return GetAndroidRestrictionMessage(fieldLimit) == null;
        }

        public static bool CanUseAndroid(long fieldLimit, MapInfo mapInfo)
        {
            return GetAndroidRestrictionMessage(fieldLimit, mapInfo) == null;
        }

        public static bool CanChangePartyLeader(long fieldLimit)
        {
            return CanChangePartyLeader(fieldLimit, mapInfo: null);
        }

        public static bool CanChangePartyLeader(long fieldLimit, MapInfo mapInfo)
        {
            return GetPartyBossRestrictionMessage(fieldLimit, mapInfo) == null;
        }

        public static string GetItemUseRestrictionMessage(
            long fieldLimit,
            MapInfo mapInfo,
            InventoryType inventoryType,
            int itemId,
            string itemName,
            string itemDescription,
            bool isStatChangeConsumable)
        {
            string fieldLimitRestrictionMessage = GetItemUseRestrictionMessage(
                fieldLimit,
                inventoryType,
                itemId,
                itemName,
                itemDescription,
                isStatChangeConsumable);
            if (!string.IsNullOrWhiteSpace(fieldLimitRestrictionMessage))
            {
                return fieldLimitRestrictionMessage;
            }

            return GetScrollUseRestrictionMessage(mapInfo, inventoryType, itemId);
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

            if (FieldLimitType.Unable_To_Use_Wedding_Invitation_Item.Check(fieldLimit) && IsWeddingInvitationItem(inventoryType, itemId, itemName, itemDescription))
            {
                return "Wedding invitation items cannot be used in this field.";
            }

            if (IsCashWeatherItem(inventoryType, itemId))
            {
                return GetCashWeatherRestrictionMessage(fieldLimit);
            }

            if (FieldLimitType.Unable_To_Use_Pet.Check(fieldLimit) && IsPetInteractionItem(inventoryType, itemId, itemName, itemDescription))
            {
                return "Pet items cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Use_AntiMacro_Item.Check(fieldLimit) && IsAntiMacroItem(inventoryType, itemId, itemName, itemDescription))
            {
                return "Anti-macro items cannot be used in this field.";
            }

            if (FieldLimitType.Unable_To_Summon_NPC.Check(fieldLimit) && IsNpcSummonItem(itemId, itemName, itemDescription))
            {
                return "NPC-summon items cannot be used in this field.";
            }

            return null;
        }

        public static string GetScrollUseRestrictionMessage(MapInfo mapInfo, InventoryType inventoryType, int itemId)
        {
            return IsInfoFlagSet(mapInfo, "scrollDisable") && IsUpgradeScrollItem(inventoryType, itemId)
                ? "Upgrade scrolls cannot be used in this map."
                : null;
        }

        public static bool IsStatChangeConsumable(
            bool hasRecoveryEffect,
            bool hasTemporaryBuffEffect,
            bool hasMorphEffect,
            bool hasCureEffect,
            bool hasEnvironmentalProtectionEffect = false)
        {
            return hasRecoveryEffect
                || hasTemporaryBuffEffect
                || hasMorphEffect
                || hasCureEffect
                || hasEnvironmentalProtectionEffect;
        }

        public static IReadOnlyList<string> GetFieldEntryItemRestrictionMessages(long fieldLimit)
        {
            return GetFieldEntryItemRestrictionMessages(fieldLimit, mapInfo: null);
        }

        public static IReadOnlyList<string> GetFieldEntryItemRestrictionMessages(long fieldLimit, MapInfo mapInfo)
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

            string cashWeatherRestrictionMessage = GetCashWeatherRestrictionMessage(fieldLimit);
            if (!string.IsNullOrWhiteSpace(cashWeatherRestrictionMessage))
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

            string scrollRestrictionMessage = GetScrollUseRestrictionMessage(mapInfo, InventoryType.USE, UpgradeScrollItemGroup * 10000);
            if (!string.IsNullOrWhiteSpace(scrollRestrictionMessage))
            {
                messages.Add("Upgrade scrolls are disabled in this map.");
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
            return GetFieldEntryInteractionRestrictionMessages(fieldLimit, mapInfo: null);
        }

        public static IReadOnlyList<string> GetFieldEntryInteractionRestrictionMessages(long fieldLimit, MapInfo mapInfo)
        {
            List<string> messages = new();
            AddFieldEntryMessage(messages, GetParcelOpenRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetQuestAlertRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetAndroidRestrictionMessage(fieldLimit, mapInfo));
            AddFieldEntryMessage(messages, GetDragonCompanionRestrictionMessage(mapInfo));
            AddFieldEntryMessage(messages, GetTamingMobRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetPartyBossRestrictionMessage(fieldLimit, mapInfo));
            AddFieldEntryMessage(messages, GetDropRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetMonsterCapacityLimitMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetExpDecreaseRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetItemOptionLimitMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetJumpDownRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetFallingDamageRestrictionMessage(fieldLimit));
            AddFieldEntryMessage(messages, GetShopOpenRestrictionMessage(mapInfo));
            AddFieldEntryMessage(messages, GetTrunkOpenRestrictionMessage(mapInfo));
            AddFieldEntryMessage(messages, GetPortableChairRestrictionMessage(mapInfo));
            AddFieldEntryMessage(messages, GetLandingRestrictionMessage(mapInfo));
            AddFieldEntryMessage(messages, GetActiveSkillCancelRestrictionMessage(mapInfo));
            AddFieldEntryMessage(messages, GetFollowCharacterRestrictionMessage(mapInfo));
            if (GetPetRuntimeRestrictionMessage(fieldLimit) == null)
            {
                AddFieldEntryMessage(messages, GetPetRuntimeRestrictionMessage(fieldLimit, mapInfo));
            }
            return messages;
        }

        public static bool CanRegisterMapTransferDestination(int mapId)
        {
            return CanRegisterMapTransferDestination(mapId, null, null);
        }

        public static bool CanRegisterMapTransferDestination(int mapId, MapInfo mapInfo)
        {
            return CanRegisterMapTransferDestination(mapId, mapInfo, null);
        }

        public static bool CanRegisterMapTransferDestination(
            int mapId,
            MapInfo mapInfo,
            FieldEntryRestrictionContext? context)
        {
            return GetMapTransferRegistrationRestrictionMessage(mapId, mapInfo, context) == null;
        }

        public static string GetMapTransferRegisterPreflightRestrictionMessage(int mapId)
        {
            if (mapId <= 0 || mapId == MapConstants.MaxMap)
            {
                return GenericMapTransferRegistrationRestrictionMessage;
            }

            if (mapId < 100_000_000)
            {
                return RegularFieldMapTransferRegistrationRestrictionMessage;
            }

            int millionGroup = (mapId / 1_000_000) % 100;
            return millionGroup == 9
                ? GenericMapTransferRegistrationRestrictionMessage
                : null;
        }

        public static string GetMapTransferRegistrationRestrictionMessage(int mapId)
        {
            return GetMapTransferRegistrationRestrictionMessage(mapId, null, null);
        }

        public static string GetMapTransferRegistrationRestrictionMessage(int mapId, MapInfo mapInfo)
        {
            return GetMapTransferRegistrationRestrictionMessage(mapId, mapInfo, null);
        }

        public static string GetMapTransferRegistrationRestrictionMessage(
            int mapId,
            MapInfo mapInfo,
            FieldEntryRestrictionContext? context)
        {
            string preflightRestrictionMessage = GetMapTransferRegisterPreflightRestrictionMessage(mapId);
            if (!string.IsNullOrWhiteSpace(preflightRestrictionMessage))
            {
                return preflightRestrictionMessage;
            }

            MapTransferRuntimePacketResultCode? runtimeResultCode = GetMapTransferRegistrationResultCode(mapId, mapInfo, context);
            return runtimeResultCode switch
            {
                MapTransferRuntimePacketResultCode.OfficialFailure11 => MapTransferClientParityText.ResolveFailureMessage(MapTransferRuntimePacketResultCode.OfficialFailure11),
                MapTransferRuntimePacketResultCode.CannotSaveDestination => GenericMapTransferRegistrationRestrictionMessage,
                _ => null
            };
        }

        public static string GetMapTransferEntryRestrictionMessage(
            MapInfo mapInfo,
            FieldEntryRestrictionContext? context)
        {
            return GetSharedMapTransferDestinationRestrictionMessage(mapInfo, context);
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

        private static string GetSharedMapTransferDestinationRestrictionMessage(
            MapInfo mapInfo,
            FieldEntryRestrictionContext? context)
        {
            if (mapInfo == null)
            {
                return null;
            }

            string entryRestrictionMessage = context.HasValue
                ? FieldEntryRestrictionEvaluator.GetRestrictionMessage(mapInfo, context.Value)
                : null;
            if (!string.IsNullOrWhiteSpace(entryRestrictionMessage))
            {
                return entryRestrictionMessage;
            }

            string transferRestrictionMessage = GetTransferRestrictionMessage(mapInfo.fieldLimit);
            if (!string.IsNullOrWhiteSpace(transferRestrictionMessage))
            {
                return transferRestrictionMessage;
            }

            if (mapInfo.noMapCmd == true ||
                (mapInfo.moveLimit.HasValue && mapInfo.moveLimit.Value > 0) ||
                (mapInfo.fieldType.HasValue && mapInfo.fieldType.Value != FieldType.FIELDTYPE_DEFAULT))
            {
                return GenericMapTransferRegistrationRestrictionMessage;
            }

            return null;
        }

        public static MapTransferRuntimePacketResultCode? GetMapTransferRegistrationResultCode(
            int mapId,
            MapInfo mapInfo,
            FieldEntryRestrictionContext? context)
        {
            if (mapInfo == null)
            {
                return null;
            }

            string entryRestrictionMessage = context.HasValue
                ? FieldEntryRestrictionEvaluator.GetRestrictionMessage(mapInfo, context.Value)
                : null;
            if (!string.IsNullOrWhiteSpace(entryRestrictionMessage))
            {
                return context.HasValue &&
                       IsEntryRestrictionRequestRejectedForMapTransfer(mapInfo, context.Value)
                    ? MapTransferRuntimePacketResultCode.OfficialFailure11
                    : MapTransferRuntimePacketResultCode.CannotSaveDestination;
            }

            string transferRestrictionMessage = GetTransferRestrictionMessage(mapInfo.fieldLimit);
            if (!string.IsNullOrWhiteSpace(transferRestrictionMessage))
            {
                return MapTransferRuntimePacketResultCode.CannotSaveDestination;
            }

            if (mapInfo.noMapCmd == true ||
                (mapInfo.moveLimit.HasValue && mapInfo.moveLimit.Value > 0) ||
                (mapInfo.fieldType.HasValue && mapInfo.fieldType.Value != FieldType.FIELDTYPE_DEFAULT))
            {
                return MapTransferRuntimePacketResultCode.CannotSaveDestination;
            }

            return null;
        }

        private static bool IsEntryRestrictionRequestRejectedForMapTransfer(
            MapInfo mapInfo,
            FieldEntryRestrictionContext context)
        {
            if (mapInfo == null)
            {
                return false;
            }

            FieldEntryRestrictionType restrictionType = FieldEntryRestrictionEvaluator.GetRestrictionType(mapInfo, context);
            if (restrictionType != FieldEntryRestrictionType.LevelLimit)
            {
                return false;
            }

            int requiredLevel = mapInfo.lvLimit ?? 0;
            return requiredLevel >= 7 && context.PlayerLevel < 7;
        }

        private static bool IsPortalScrollItem(InventoryType inventoryType, int itemId)
        {
            return inventoryType == InventoryType.USE && (itemId / 10000) == PortalScrollItemGroup;
        }

        private static bool IsSpecificPortalScrollItem(InventoryType inventoryType, int itemId)
        {
            return IsPortalScrollItem(inventoryType, itemId) && itemId != NearestTownPortalScrollItemId;
        }

        private static bool IsUpgradeScrollItem(InventoryType inventoryType, int itemId)
        {
            return inventoryType == InventoryType.USE && (itemId / 10000) == UpgradeScrollItemGroup;
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

        private static bool IsWeddingInvitationItem(InventoryType inventoryType, int itemId, string itemName, string itemDescription)
        {
            return InventoryItemMetadataResolver.IsWeddingInvitationItem(
                itemId,
                inventoryType,
                itemName,
                itemDescription);
        }

        private static bool IsNpcSummonItem(int itemId, string itemName, string itemDescription)
        {
            bool hasNpcReference = InventoryItemMetadataResolver.TryResolveNpcReference(itemId, out int npcId) && npcId > 0;
            bool hasSummonScript = InventoryItemMetadataResolver.TryResolveSpecScripts(itemId, out IReadOnlyList<string> scriptNames)
                                   && scriptNames.Any(scriptName =>
                                       string.Equals(scriptName, SummonEventNpcScriptName, StringComparison.OrdinalIgnoreCase));
            return IsNpcSummonItem(
                itemId is NpcSummonScriptItemId or NpcSummonQuestItemId,
                hasNpcReference,
                hasSummonScript,
                itemName,
                itemDescription);
        }

        internal static bool IsNpcSummonItem(
            bool isKnownNpcSummonItem,
            bool hasNpcReference,
            bool hasSummonEventNpcScript,
            string itemName,
            string itemDescription)
        {
            if (isKnownNpcSummonItem || hasNpcReference || hasSummonEventNpcScript)
            {
                return true;
            }

            return ContainsPhrase(itemName, "summon npc")
                   || ContainsPhrase(itemDescription, "summon npc")
                   || ContainsPhrase(itemDescription, "summons NPC");
        }

        private static bool IsPetInteractionItem(InventoryType inventoryType, int itemId, string itemName, string itemDescription)
        {
            int itemGroup = itemId / 10000;
            if (inventoryType == InventoryType.CASH
                && itemGroup is CashPetItemGroup
                    or PetLifeRecoveryItemGroup
                    or PetNameTagItemGroup
                    or PetReviveItemGroup
                    or PetSkillItemGroup
                    or PetFoodItemGroup)
            {
                return true;
            }

            if (InventoryItemMetadataResolver.IsPetFoodItem(itemId))
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

        private static bool IsInfoFlagSet(MapInfo mapInfo, string propertyName)
        {
            if (mapInfo == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            WzImageProperty property = FindInfoProperty(mapInfo, propertyName);
            if (property == null)
            {
                return false;
            }

            try
            {
                return property.GetInt() != 0;
            }
            catch
            {
                return property is WzStringProperty stringProperty
                       && int.TryParse(stringProperty.Value, out int value)
                       && value != 0;
            }
        }

        private static WzImageProperty FindInfoProperty(MapInfo mapInfo, string propertyName)
        {
            WzImageProperty property = FindNamedProperty(mapInfo.additionalProps, propertyName)
                ?? FindNamedProperty(mapInfo.unsupportedInfoProperties, propertyName);

            return property ?? mapInfo.Image?["info"]?[propertyName] as WzImageProperty;
        }

        private static WzImageProperty FindNamedProperty(IEnumerable<WzImageProperty> properties, string propertyName)
        {
            if (properties == null)
            {
                return null;
            }

            foreach (WzImageProperty property in properties)
            {
                if (string.Equals(property?.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }
    }
}
