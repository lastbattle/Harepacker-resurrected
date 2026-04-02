using MapleLib.ClientLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    public sealed class InventoryItemTooltipMetadata
    {
        public int ItemId { get; init; }
        public InventoryType InventoryType { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string TypeName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool IsCashItem { get; init; }
        public bool IsNotForSale { get; init; }
        public bool IsQuestItem { get; init; }
        public bool IsTradeBlocked { get; init; }
        public bool IsOneOfAKind { get; init; }
        public int? RequiredLevel { get; init; }
        public int? Price { get; init; }
        public double? UnitPrice { get; init; }
        public DateTime? ExpirationDateUtc { get; init; }
        public IReadOnlyList<string> EffectLines { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MetadataLines { get; init; } = Array.Empty<string>();
    }

    public static class InventoryItemMetadataResolver
    {
        private const int DefaultStackLimit = 100;
        private static readonly (string Key, string Label)[] CurableStatusEffectKeys =
        {
            ("poison", "Poison"),
            ("seal", "Seal"),
            ("darkness", "Darkness"),
            ("weakness", "Weakness"),
            ("curse", "Curse"),
            ("painmark", "Pain Mark"),
            ("stun", "Stun"),
            ("slow", "Slow"),
            ("freeze", "Freeze"),
            ("seduce", "Seduce"),
            ("attract", "Attract"),
            ("confusion", "Reverse Controls"),
            ("reverseInput", "Reverse Controls"),
            ("undead", "Zombie"),
            ("zombie", "Zombie")
        };
        private static readonly (string[] Keys, string Label)[] PositivePercentEffectKeys =
        {
            (new[] { "mhpR", "mhpRRate" }, "Max HP"),
            (new[] { "mmpR", "mmpRRate" }, "Max MP"),
            (new[] { "asrR", "indieAsrR" }, "Status Resistance")
        };
        private static readonly (string[] Keys, string Label)[] PrimaryFlatEffectKeys =
        {
            (new[] { "str", "indieSTR" }, "STR"),
            (new[] { "dex", "indieDEX" }, "DEX"),
            (new[] { "int", "indieINT" }, "INT"),
            (new[] { "luk", "indieLUK" }, "LUK")
        };
        private static readonly (string Key, string Label)[] IndependentFlatEffectKeys =
        {
            ("indieMhp", "Max HP"),
            ("indieMmp", "Max MP"),
            ("indiePad", "Weapon ATT"),
            ("indieMad", "Magic ATT"),
            ("indiePdd", "Weapon DEF"),
            ("indieMdd", "Magic DEF"),
            ("indieSpeed", "Speed"),
            ("indieJump", "Jump"),
            ("indieAllStat", "All Stats")
        };
        private static readonly (string[] Keys, string Label)[] IndependentPercentEffectKeys =
        {
            (new[] { "expBuff", "expR" }, "EXP"),
            (new[] { "dropRate", "dropR" }, "Item Drop Rate")
        };

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

        public static bool TryResolveItemTypeName(int itemId, out string typeName)
        {
            typeName = null;
            return global::HaCreator.Program.InfoManager?.ItemNameCache != null
                   && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                   && !string.IsNullOrWhiteSpace(itemInfo?.Item1)
                ? (typeName = itemInfo.Item1) != null
                : false;
        }

        public static bool TryResolveSpecScript(int itemId, out string script)
        {
            script = null;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty?["spec"] is not WzSubProperty specProperty)
            {
                return false;
            }

            string resolvedScript = (specProperty["script"] as WzStringProperty)?.Value;
            if (string.IsNullOrWhiteSpace(resolvedScript))
            {
                return false;
            }

            script = resolvedScript.Trim();
            return true;
        }

        public static bool TryResolveSpecNpc(int itemId, out int npcId)
        {
            npcId = 0;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty?["spec"] is not WzSubProperty specProperty)
            {
                return false;
            }

            int resolvedNpcId = GetIntValue(specProperty["npc"]);
            if (resolvedNpcId <= 0)
            {
                return false;
            }

            npcId = resolvedNpcId;
            return true;
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

        public static bool TryResolveClientItemCrc(int itemId, out uint crc)
        {
            crc = 0;
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            uint commonCrc = ComputeItemCommonCrc(itemId, itemProperty);
            InventoryType inventoryType = ResolveInventoryType(itemId);
            crc = inventoryType is InventoryType.USE or InventoryType.SETUP or InventoryType.ETC
                ? ComputeBundleItemCrc(itemProperty, itemId, commonCrc)
                : commonCrc;
            return crc != 0;
        }

        public static InventoryItemTooltipMetadata ResolveTooltipMetadata(int itemId, InventoryType fallbackType = InventoryType.NONE)
        {
            InventoryType inventoryType = ResolveInventoryType(itemId);
            if (inventoryType == InventoryType.NONE)
            {
                inventoryType = fallbackType;
            }

            TryResolveItemName(itemId, out string itemName);
            TryResolveItemDescription(itemId, out string description);

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            WzSubProperty infoProperty = itemProperty?["info"] as WzSubProperty;
            WzSubProperty specProperty = itemProperty?["spec"] as WzSubProperty;

            bool isCashItem = GetIntValue(infoProperty?["cash"]) == 1;
            bool isNotForSale = GetIntValue(infoProperty?["notSale"]) == 1;
            bool isQuestItem = GetIntValue(infoProperty?["quest"]) == 1;
            bool isTradeBlocked = GetIntValue(infoProperty?["tradeBlock"]) == 1;
            bool isOneOfAKind = GetIntValue(infoProperty?["only"]) == 1;

            DateTime? expirationDateUtc = null;
            if (infoProperty?["dateExpire"] is WzStringProperty expirationProperty)
            {
                DateTime? resolvedExpiration = expirationProperty.GetDateTime();
                if (resolvedExpiration.HasValue)
                {
                    expirationDateUtc = DateTime.SpecifyKind(resolvedExpiration.Value, DateTimeKind.Utc);
                }
            }

            List<string> metadataLines = BuildMetadataLines(
                infoProperty,
                isNotForSale,
                isQuestItem,
                isTradeBlocked,
                isOneOfAKind,
                expirationDateUtc);

            return new InventoryItemTooltipMetadata
            {
                ItemId = itemId,
                InventoryType = inventoryType,
                ItemName = itemName ?? $"Item #{itemId}",
                TypeName = ResolveTypeName(itemId, inventoryType),
                Description = description ?? string.Empty,
                IsCashItem = isCashItem,
                IsNotForSale = isNotForSale,
                IsQuestItem = isQuestItem,
                IsTradeBlocked = isTradeBlocked,
                IsOneOfAKind = isOneOfAKind,
                RequiredLevel = TryGetPositiveInt(infoProperty?["lv"]),
                Price = TryGetPositiveInt(infoProperty?["price"]),
                UnitPrice = TryGetPositiveDouble(infoProperty?["unitPrice"]),
                ExpirationDateUtc = expirationDateUtc,
                EffectLines = BuildEffectLines(specProperty),
                MetadataLines = metadataLines
            };
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

        private static uint ComputeItemCommonCrc(int itemId, WzSubProperty itemProperty)
        {
            uint seed = ComputeClientCrc32(95);
            seed = ComputeClientCrc32(seed);

            uint combined = seed ^ ComputeClientCrc32(itemId);
            combined ^= ComputeItemIconCrc(itemProperty);

            string description = TryResolveItemDescription(itemId, out string resolvedDescription)
                ? resolvedDescription ?? string.Empty
                : string.Empty;
            string name = TryResolveItemName(itemId, out string resolvedName)
                ? resolvedName ?? string.Empty
                : string.Empty;
            string combinedText = string.Concat(name, description);
            if (!string.IsNullOrEmpty(combinedText))
            {
                combined ^= ComputeClientCrc32(Encoding.Default.GetBytes(combinedText));
            }

            return combined;
        }

        private static uint ComputeBundleItemCrc(WzSubProperty itemProperty, int itemId, uint commonCrc)
        {
            WzSubProperty infoProperty = itemProperty?["info"] as WzSubProperty;
            if (infoProperty == null)
            {
                return commonCrc;
            }

            uint combined = commonCrc;
            combined ^= ComputeClientCrc32(GetIntValue(infoProperty["reqLEV"]));
            combined ^= ComputeClientCrc32(GetIntValue(infoProperty["price"]));
            combined ^= ComputeClientCrc32(ReadDoubleValue(infoProperty["unitPrice"]));
            combined ^= ComputeClientCrc32(GetIntValue(infoProperty["slotMax"]));
            combined ^= ComputeClientCrc32(GetIntValue(infoProperty["max"]));
            combined ^= ComputeClientCrc32(ComposeBundleItemFlagMask(infoProperty));
            return combined;
        }

        private static int ComposeBundleItemFlagMask(WzSubProperty infoProperty)
        {
            return (GetIntValue(infoProperty["noCancelMouse"]) != 0 ? 1 : 0)
                 | (GetIntValue(infoProperty["expireOnLogout"]) != 0 ? 1 << 1 : 0)
                 | (GetIntValue(infoProperty["notSale"]) != 0 ? 1 << 2 : 0)
                 | (GetIntValue(infoProperty["karma"]) != 0 ? 1 << 3 : 0)
                 | (GetIntValue(infoProperty["tradeBlock"]) != 0 ? 1 << 4 : 0)
                 | (GetIntValue(infoProperty["timeLimited"]) != 0 ? 1 << 5 : 0)
                 | (GetIntValue(infoProperty["partyQuest"]) != 0 ? 1 << 6 : 0)
                 | (GetIntValue(infoProperty["quest"]) != 0 ? 1 << 7 : 0);
        }

        private static uint ComputeItemIconCrc(WzSubProperty itemProperty)
        {
            WzSubProperty infoProperty = itemProperty?["info"] as WzSubProperty;
            if (infoProperty == null)
            {
                return 0;
            }

            uint iconCrc = 0;
            if (infoProperty["icon"] is WzCanvasProperty iconCanvas)
            {
                iconCrc ^= ComputeCanvasCrc(iconCanvas);
            }

            if (infoProperty["iconRaw"] is WzCanvasProperty iconRawCanvas)
            {
                iconCrc ^= ComputeCanvasCrc(iconRawCanvas);
            }

            return iconCrc;
        }

        private static uint ComputeCanvasCrc(WzCanvasProperty canvas)
        {
            try
            {
                using Bitmap source = canvas?.GetLinkedWzImageProperty() is WzCanvasProperty linkedCanvas
                    ? linkedCanvas.GetBitmap()
                    : canvas?.GetBitmap();
                if (source == null)
                {
                    return 0;
                }

                using Bitmap converted = new Bitmap(source.Width, source.Height, PixelFormat.Format16bppArgb1555);
                using (Graphics graphics = Graphics.FromImage(converted))
                {
                    graphics.DrawImageUnscaled(source, 0, 0);
                }

                Rectangle bounds = new Rectangle(0, 0, converted.Width, converted.Height);
                BitmapData bitmapData = converted.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format16bppArgb1555);
                try
                {
                    int rowLength = converted.Width * 2;
                    byte[] row = new byte[rowLength];
                    uint crc = 0;
                    for (int y = 0; y < converted.Height; y++)
                    {
                        IntPtr rowPtr = bitmapData.Scan0 + (y * bitmapData.Stride);
                        Marshal.Copy(rowPtr, row, 0, rowLength);
                        crc ^= ComputeClientCrc32(row);
                    }

                    return crc;
                }
                finally
                {
                    converted.UnlockBits(bitmapData);
                }
            }
            catch
            {
                return 0;
            }
        }

        private static double ReadDoubleValue(WzImageProperty property)
        {
            return property switch
            {
                WzDoubleProperty doubleProperty => doubleProperty.Value,
                WzFloatProperty floatProperty => floatProperty.Value,
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                _ => 0d
            };
        }

        private static uint ComputeClientCrc32(int value)
        {
            return CCrc32.GetCrc32(value, 0, xorInitialCrc: false, flag2: false);
        }

        private static uint ComputeClientCrc32(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            return CCrc32.GetCrc32(bits, 0, xorInitialCrc: false, flag2: false);
        }

        private static uint ComputeClientCrc32(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return 0;
            }

            return ComputeClientCrc32(bytes.AsSpan());
        }

        private static uint ComputeClientCrc32(ReadOnlySpan<byte> bytes)
        {
            const uint polynomial = 0x04C11DB7u;
            uint crc = 0;
            for (int index = 0; index < bytes.Length; index++)
            {
                crc ^= (uint)bytes[index] << 24;
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x80000000u) != 0
                        ? (crc << 1) ^ polynomial
                        : crc << 1;
                }
            }

            return crc;
        }

        private static List<string> BuildEffectLines(WzSubProperty specProperty)
        {
            List<string> effectLines = new();
            if (specProperty == null)
            {
                return effectLines;
            }

            AppendStatEffectLine(effectLines, "HP", TryGetPositiveInt(specProperty["hp"]), false);
            AppendStatEffectLine(effectLines, "HP", ResolveFirstPositiveInt(specProperty, "hpR", "hpRatio", "hpPer"), true);
            AppendStatEffectLine(effectLines, "MP", TryGetPositiveInt(specProperty["mp"]), false);
            AppendStatEffectLine(effectLines, "MP", ResolveFirstPositiveInt(specProperty, "mpR", "mpRatio", "mpPer"), true);
            AppendStatEffectLine(effectLines, "Weapon ATT", TryGetPositiveInt(specProperty["pad"]), false);
            AppendStatEffectLine(effectLines, "Magic ATT", TryGetPositiveInt(specProperty["mad"]), false);
            AppendStatEffectLine(effectLines, "Weapon DEF", TryGetPositiveInt(specProperty["pdd"]), false);
            AppendStatEffectLine(effectLines, "Magic DEF", TryGetPositiveInt(specProperty["mdd"]), false);
            AppendStatEffectLine(effectLines, "Accuracy", TryGetPositiveInt(specProperty["acc"]), false);
            AppendStatEffectLine(effectLines, "Avoidability", TryGetPositiveInt(specProperty["eva"]), false);
            AppendStatEffectLine(effectLines, "Speed", TryGetPositiveInt(specProperty["speed"]), false);
            AppendStatEffectLine(effectLines, "Jump", TryGetPositiveInt(specProperty["jump"]), false);
            AppendPrimaryFlatEffectLines(effectLines, specProperty);
            AppendPercentEffectLines(effectLines, specProperty);
            AppendIndependentFlatEffectLines(effectLines, specProperty);
            AppendIndependentPercentEffectLines(effectLines, specProperty);
            AppendCureEffectLine(effectLines, specProperty);
            AppendMoveToEffectLine(effectLines, TryGetPositiveInt(specProperty["moveTo"]));
            AppendMorphEffectLine(effectLines, specProperty);
            AppendBoosterEffectLine(effectLines, specProperty["booster"], specProperty["indieBooster"]);
            AppendBerserkEffectLine(effectLines, specProperty["berserk"]);
            AppendThawEffectLine(effectLines, specProperty["thaw"]);
            AppendCrossContinentEffectLine(effectLines, specProperty["ignoreContinent"]);
            AppendReturnMapRecordEffectLine(effectLines, specProperty["returnMapQR"]);

            int? durationMs = TryGetPositiveInt(specProperty["time"]);
            if (durationMs.HasValue)
            {
                effectLines.Add($"Duration: {FormatDuration(durationMs.Value)}");
            }

            if (GetIntValue(specProperty["party"]) == 1)
            {
                effectLines.Add("Applies to party members");
            }

            if (GetIntValue(specProperty["consumeOnPickup"]) == 1)
            {
                effectLines.Add("Consumed on pickup");
            }

            return effectLines;
        }

        private static List<string> BuildMetadataLines(
            WzSubProperty infoProperty,
            bool isNotForSale,
            bool isQuestItem,
            bool isTradeBlocked,
            bool isOneOfAKind,
            DateTime? expirationDateUtc)
        {
            List<string> metadataLines = new();

            int? requiredLevel = TryGetPositiveInt(infoProperty?["lv"]);
            if (requiredLevel.HasValue)
            {
                metadataLines.Add($"Required Level: {requiredLevel.Value}");
            }

            int? price = TryGetPositiveInt(infoProperty?["price"]);
            if (price.HasValue)
            {
                metadataLines.Add($"Price: {price.Value.ToString("N0", CultureInfo.InvariantCulture)} mesos");
            }

            double? unitPrice = TryGetPositiveDouble(infoProperty?["unitPrice"]);
            if (unitPrice.HasValue)
            {
                metadataLines.Add($"Unit Price: {unitPrice.Value.ToString("0.##", CultureInfo.InvariantCulture)} mesos");
            }

            if (expirationDateUtc.HasValue)
            {
                metadataLines.Add($"Expires {expirationDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
            }

            if (isTradeBlocked)
            {
                metadataLines.Add("Untradeable");
            }

            if (isOneOfAKind)
            {
                metadataLines.Add("One of a kind");
            }

            if (isQuestItem)
            {
                metadataLines.Add("Quest Item");
            }

            if (isNotForSale)
            {
                metadataLines.Add("Not for sale");
            }

            return metadataLines;
        }

        private static string ResolveTypeName(int itemId, InventoryType inventoryType)
        {
            if (global::HaCreator.Program.InfoManager?.ItemNameCache != null
                && global::HaCreator.Program.InfoManager.ItemNameCache.TryGetValue(itemId, out Tuple<string, string, string> itemInfo)
                && !string.IsNullOrWhiteSpace(itemInfo?.Item1))
            {
                return itemInfo.Item1;
            }

            return inventoryType.ToString();
        }

        private static void AppendStatEffectLine(List<string> effectLines, string label, int? value, bool isPercent)
        {
            if (!value.HasValue || value.Value <= 0)
            {
                return;
            }

            string suffix = isPercent ? "%" : string.Empty;
            effectLines.Add($"{label} +{value.Value.ToString(CultureInfo.InvariantCulture)}{suffix}");
        }

        private static void AppendPercentEffectLines(List<string> effectLines, WzSubProperty specProperty)
        {
            for (int i = 0; i < PositivePercentEffectKeys.Length; i++)
            {
                (string[] keys, string label) = PositivePercentEffectKeys[i];
                AppendStatEffectLine(effectLines, label, ResolveFirstPositiveInt(specProperty, keys), isPercent: true);
            }
        }

        private static void AppendPrimaryFlatEffectLines(List<string> effectLines, WzSubProperty specProperty)
        {
            for (int i = 0; i < PrimaryFlatEffectKeys.Length; i++)
            {
                (string[] keys, string label) = PrimaryFlatEffectKeys[i];
                AppendStatEffectLine(effectLines, label, ResolveFirstPositiveInt(specProperty, keys), isPercent: false);
            }
        }

        private static void AppendIndependentFlatEffectLines(List<string> effectLines, WzSubProperty specProperty)
        {
            for (int i = 0; i < IndependentFlatEffectKeys.Length; i++)
            {
                (string key, string label) = IndependentFlatEffectKeys[i];
                AppendStatEffectLine(effectLines, label, TryGetPositiveInt(specProperty[key]), isPercent: false);
            }
        }

        private static void AppendIndependentPercentEffectLines(List<string> effectLines, WzSubProperty specProperty)
        {
            for (int i = 0; i < IndependentPercentEffectKeys.Length; i++)
            {
                (string[] keys, string label) = IndependentPercentEffectKeys[i];
                AppendStatEffectLine(effectLines, label, ResolveFirstPositiveInt(specProperty, keys), isPercent: true);
            }
        }

        private static void AppendCureEffectLine(List<string> effectLines, WzSubProperty specProperty)
        {
            List<string> curedStatuses = new();
            for (int i = 0; i < CurableStatusEffectKeys.Length; i++)
            {
                (string key, string label) = CurableStatusEffectKeys[i];
                if (GetIntValue(specProperty[key]) == 1)
                {
                    curedStatuses.Add(label);
                }
            }

            if (curedStatuses.Count > 0)
            {
                effectLines.Add($"Cures {string.Join(", ", curedStatuses)}");
            }
        }

        private static void AppendMoveToEffectLine(List<string> effectLines, int? moveToMapId)
        {
            if (!moveToMapId.HasValue)
            {
                return;
            }

            if (moveToMapId.Value == 999999999)
            {
                effectLines.Add("Moves to the nearest town");
                return;
            }

            string destinationName = ResolveMapName(moveToMapId.Value);
            effectLines.Add(string.IsNullOrWhiteSpace(destinationName)
                ? $"Moves to map {moveToMapId.Value.ToString(CultureInfo.InvariantCulture)}"
                : $"Moves to {destinationName}");
        }

        private static void AppendMorphEffectLine(List<string> effectLines, WzSubProperty specProperty)
        {
            int? morphId = TryGetPositiveInt(specProperty["morph"]);
            if (morphId.HasValue)
            {
                effectLines.Add($"Transforms into morph #{morphId.Value.ToString(CultureInfo.InvariantCulture)}");
                return;
            }

            if (specProperty["morphRandom"] is WzSubProperty morphRandom
                && morphRandom.WzProperties != null
                && morphRandom.WzProperties.Count > 0)
            {
                effectLines.Add("Transforms into a random morph");
            }
        }

        private static void AppendBoosterEffectLine(
            List<string> effectLines,
            WzImageProperty property,
            WzImageProperty independentProperty = null)
        {
            int booster = GetIntValue(property);
            if (booster == 0)
            {
                booster = GetIntValue(independentProperty);
            }

            if (booster == 0)
            {
                return;
            }

            effectLines.Add($"Attack Speed {FormatSignedValue(booster)}");
        }

        private static int? ResolveFirstPositiveInt(WzSubProperty specProperty, params string[] keys)
        {
            if (specProperty == null || keys == null)
            {
                return null;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                int? value = TryGetPositiveInt(specProperty[keys[i]]);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private static void AppendBerserkEffectLine(List<string> effectLines, WzImageProperty property)
        {
            int berserk = GetIntValue(property);
            if (berserk == 0)
            {
                return;
            }

            effectLines.Add($"Berserk {FormatSignedValue(berserk)}");
        }

        private static void AppendThawEffectLine(List<string> effectLines, WzImageProperty property)
        {
            int thaw = GetIntValue(property);
            if (thaw == 0)
            {
                return;
            }

            effectLines.Add($"Thaw {FormatSignedValue(thaw)}");
        }

        private static void AppendCrossContinentEffectLine(List<string> effectLines, WzImageProperty property)
        {
            if (GetIntValue(property) == 1)
            {
                effectLines.Add("Can be used across continents");
            }
        }

        private static void AppendReturnMapRecordEffectLine(List<string> effectLines, WzImageProperty property)
        {
            int returnMapRecordId = GetIntValue(property);
            if (returnMapRecordId > 0)
            {
                effectLines.Add($"Uses return-map record #{returnMapRecordId.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private static string ResolveMapName(int mapId)
        {
            if (global::HaCreator.Program.InfoManager?.MapsNameCache == null)
            {
                return string.Empty;
            }

            string[] candidateKeys =
            {
                mapId.ToString(CultureInfo.InvariantCulture),
                mapId.ToString("D9", CultureInfo.InvariantCulture)
            };

            for (int i = 0; i < candidateKeys.Length; i++)
            {
                if (!global::HaCreator.Program.InfoManager.MapsNameCache.TryGetValue(candidateKeys[i], out Tuple<string, string, string> mapInfo)
                    || mapInfo == null)
                {
                    continue;
                }

                string streetName = mapInfo.Item1?.Trim();
                string mapName = mapInfo.Item2?.Trim();
                if (!string.IsNullOrWhiteSpace(streetName) && !string.IsNullOrWhiteSpace(mapName))
                {
                    return $"{streetName} - {mapName}";
                }

                if (!string.IsNullOrWhiteSpace(mapName))
                {
                    return mapName;
                }

                if (!string.IsNullOrWhiteSpace(streetName))
                {
                    return streetName;
                }
            }

            return string.Empty;
        }

        private static string FormatDuration(int durationMs)
        {
            TimeSpan duration = TimeSpan.FromMilliseconds(durationMs);
            if (duration.TotalHours >= 1d)
            {
                return $"{Math.Round(duration.TotalHours, 1).ToString("0.#", CultureInfo.InvariantCulture)} hr";
            }

            if (duration.TotalMinutes >= 1d)
            {
                return $"{Math.Round(duration.TotalMinutes, 1).ToString("0.#", CultureInfo.InvariantCulture)} min";
            }

            return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture)} sec";
        }

        private static string FormatSignedValue(int value)
        {
            return value >= 0
                ? $"+{value.ToString(CultureInfo.InvariantCulture)}"
                : value.ToString(CultureInfo.InvariantCulture);
        }

        private static int? TryGetPositiveInt(WzImageProperty property)
        {
            int value = GetIntValue(property);
            return value > 0 ? value : null;
        }

        private static double? TryGetPositiveDouble(WzImageProperty property)
        {
            double value = property switch
            {
                WzDoubleProperty doubleProperty => doubleProperty.Value,
                WzFloatProperty floatProperty => floatProperty.Value,
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                _ => 0d
            };

            return value > 0d ? value : null;
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
