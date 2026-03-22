using MapleLib.WzLib.WzStructure.Data.ItemStructure;

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
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item1)
                ? (itemName = itemInfo.Item1) != null
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
    }
}
