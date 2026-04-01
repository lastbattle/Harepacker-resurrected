using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;

namespace HaCreator.MapSimulator.UI
{
    public static class InventoryItemMetadataResolver
    {
        private const int DefaultStackLimit = 100;

        public static InventoryType ResolveInventoryType(int itemId)
        {
            int typeBucket = itemId / 1000000;
            return typeBucket switch
            {
                1 => InventoryType.EQUIP,
                2 => InventoryType.USE,
                3 => InventoryType.SETUP,
                4 => InventoryType.ETC,
                5 => InventoryType.CASH,
                _ => InventoryType.NONE
            };
        }

        public static InventoryType ResolveInventoryType(InventorySlotData slot, InventoryType fallback = InventoryType.NONE)
        {
            if (slot?.PreferredInventoryType.HasValue == true
                && slot.PreferredInventoryType.Value != InventoryType.NONE)
            {
                return slot.PreferredInventoryType.Value;
            }

            return slot != null && slot.ItemId > 0
                ? ResolveInventoryType(slot.ItemId)
                : fallback;
        }

        public static int ResolveMaxStack(InventoryType type, int? slotMax = null)
        {
            int defaultValue = type switch
            {
                InventoryType.EQUIP => 1,
                InventoryType.NONE => 1,
                _ => DefaultStackLimit
            };

            int resolved = slotMax ?? defaultValue;
            return resolved > 0 ? resolved : defaultValue;
        }

        public static bool TryResolveItemName(int itemId, out string itemName)
        {
            itemName = null;
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out System.Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item2)
                ? (itemName = itemInfo.Item2) != null
                : false;
        }

        public static bool TryResolveItemDescription(int itemId, out string description)
        {
            description = null;
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out System.Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item3)
                ? (description = itemInfo.Item3) != null
                : false;
        }

        public static bool TryResolveItemInfoPath(int itemId, out string path)
        {
            path = null;
            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty?["info"] is not WzSubProperty infoProperty)
            {
                return false;
            }

            string resolvedPath = (infoProperty["path"] as WzStringProperty)?.Value;
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return false;
            }

            path = resolvedPath.Trim();
            return true;
        }

        public static bool TryResolveTradeRestrictionFlags(int itemId, out bool isCashItem, out bool isNotForSale, out bool isQuestItem)
        {
            isCashItem = false;
            isNotForSale = false;
            isQuestItem = false;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty?["info"] is not WzSubProperty infoProperty)
            {
                return false;
            }

            isCashItem = GetIntValue(infoProperty["cash"]) == 1;
            isNotForSale = GetIntValue(infoProperty["notSale"]) == 1;
            isQuestItem = GetIntValue(infoProperty["quest"]) == 1;
            return true;
        }

        public static bool TryResolveMaxStackForItem(int itemId, out int maxStackSize)
        {
            maxStackSize = ResolveMaxStack(ResolveInventoryType(itemId));
            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty?["info"] is not WzSubProperty infoProperty)
            {
                return false;
            }

            int? slotMax = infoProperty["slotMax"] switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzFloatProperty floatProperty => (int)floatProperty.Value,
                WzDoubleProperty doubleProperty => (int)doubleProperty.Value,
                _ => null
            };

            maxStackSize = ResolveMaxStack(ResolveInventoryType(itemId), slotMax);
            return true;
        }

        public static bool TryResolveImageSource(int itemId, out string category, out string imagePath)
        {
            category = null;
            imagePath = null;

            InventoryType inventoryType = ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                return false;
            }

            if (inventoryType == InventoryType.EQUIP)
            {
                string folder = ResolveEquipmentFolder(itemId);
                if (string.IsNullOrEmpty(folder))
                {
                    return false;
                }

                category = "Character";
                imagePath = $"{folder}/{itemId:D8}.img";
                return true;
            }

            int group = itemId / 10000;
            string folderName = inventoryType switch
            {
                InventoryType.USE => "Consume",
                InventoryType.SETUP => "Install",
                InventoryType.ETC => "Etc",
                InventoryType.CASH => "Cash",
                _ => null
            };

            if (string.IsNullOrEmpty(folderName))
            {
                return false;
            }

            category = "Item";
            imagePath = $"{folderName}/{group:D4}.img";
            return true;
        }

        private static string ResolveEquipmentFolder(int itemId)
        {
            int category = itemId / 10000;
            return category switch
            {
                100 => "Cap",
                101 => "Accessory",
                102 => "Accessory",
                103 => "Earrings",
                104 => "Coat",
                105 => "Longcoat",
                106 => "Pants",
                107 => "Shoes",
                108 => "Glove",
                109 => "Shield",
                110 => "Cape",
                111 => "Ring",
                112 => "Accessory",
                113 => "Accessory",
                114 => "Accessory",
                115 => "Accessory",
                116 => "Accessory",
                118 => "Accessory",
                166 => "Android",
                167 => "Android",
                180 => "PetEquip",
                181 => "PetEquip",
                >= 130 and < 170 => "Weapon",
                >= 190 and < 200 => "TamingMob",
                _ => null
            };
        }

        private static WzSubProperty LoadItemProperty(int itemId)
        {
            if (!TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            var itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemNodeName = category == "Character" ? itemId.ToString("D8") : itemId.ToString("D7");
            return itemImage[itemNodeName] as WzSubProperty;
        }

        private static int GetIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzFloatProperty floatProperty => (int)floatProperty.Value,
                WzDoubleProperty doubleProperty => (int)doubleProperty.Value,
                _ => 0
            };
        }
    }
}
