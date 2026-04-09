using MapleLib.ClientLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

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

    public readonly struct SkillBookUseMetadata
    {
        public SkillBookUseMetadata(
            int masterLevel,
            int requiredSkillLevel,
            int successRatePercent,
            IReadOnlyList<int> skillIds)
        {
            MasterLevel = masterLevel;
            RequiredSkillLevel = requiredSkillLevel;
            SuccessRatePercent = successRatePercent;
            SkillIds = skillIds ?? Array.Empty<int>();
        }

        public int MasterLevel { get; }
        public int RequiredSkillLevel { get; }
        public int SuccessRatePercent { get; }
        public IReadOnlyList<int> SkillIds { get; }
        public bool IsValid => MasterLevel > 0 && SkillIds.Count > 0;
    }

    public static class InventoryItemMetadataResolver
    {
        private const int DefaultStackLimit = 100;
        private const int RewardPreviewLineLimit = 5;
        private const int ConsumeItemRequirementLineLimit = 4;
        private static readonly Regex SkillBookSuccessRateRegex = new(@"(\d+)\s*%\s*chance", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
            (new[] { "pddRate", "pddR" }, "Weapon DEF"),
            (new[] { "mddRate", "mddR" }, "Magic DEF"),
            (new[] { "accRate", "accR" }, "Accuracy"),
            (new[] { "evaRate", "evaR" }, "Avoidability"),
            (new[] { "padRate" }, "Weapon ATT"),
            (new[] { "madRate" }, "Magic ATT"),
            (new[] { "speedRate" }, "Speed"),
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
            (new[] { "expBuff", "expR", "plusExpRate" }, "EXP"),
            (new[] { "dropRate", "dropR" }, "Item Drop Rate"),
            (new[] { "mesoR" }, "Meso Rate")
        };
        private static readonly (string Key, string Label)[] InfoFlatEffectKeys =
        {
            ("incSTR", "STR"),
            ("incDEX", "DEX"),
            ("incINT", "INT"),
            ("incLUK", "LUK"),
            ("incMHP", "Max HP"),
            ("incMMP", "Max MP"),
            ("incPAD", "Weapon ATT"),
            ("incMAD", "Magic ATT"),
            ("incPDD", "Weapon DEF"),
            ("incMDD", "Magic DEF"),
            ("incACC", "Accuracy"),
            ("incEVA", "Avoidability"),
            ("incSpeed", "Speed"),
            ("incJump", "Jump"),
            ("incCraft", "Craft")
        };
        private static readonly (string Key, string Label)[] PersonalityExperienceKeys =
        {
            ("charismaEXP", "Charisma EXP"),
            ("insightEXP", "Insight EXP"),
            ("willEXP", "Will EXP"),
            ("craftEXP", "Craft EXP"),
            ("senseEXP", "Sense EXP"),
            ("charmEXP", "Charm EXP")
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

        public static bool TryResolveSkillBookUseMetadata(int itemId, out SkillBookUseMetadata metadata)
        {
            metadata = default;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty?["info"] is not WzSubProperty infoProperty)
            {
                return false;
            }

            int masterLevel = GetIntValue(infoProperty["masterLevel"]);
            if (masterLevel <= 0 || infoProperty["skill"] is not WzSubProperty skillProperty)
            {
                return false;
            }

            List<int> skillIds = new();
            HashSet<int> seenSkillIds = new();
            foreach (WzImageProperty child in skillProperty.WzProperties)
            {
                int skillId = GetIntValue(child);
                if (skillId > 0 && seenSkillIds.Add(skillId))
                {
                    skillIds.Add(skillId);
                }
            }

            if (skillIds.Count <= 0)
            {
                return false;
            }

            int requiredSkillLevel = GetIntValue(infoProperty["reqSkillLevel"]);
            int successRatePercent = TryResolveItemDescription(itemId, out string description)
                ? TryResolveSkillBookSuccessRate(description)
                : 100;
            metadata = new SkillBookUseMetadata(
                masterLevel,
                requiredSkillLevel,
                successRatePercent,
                skillIds);
            return metadata.IsValid;
        }

        public static bool TryResolveSpecScript(int itemId, out string script)
        {
            script = null;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return TryResolveSpecScript(itemProperty?["spec"] as WzSubProperty, out script);
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

        public static bool TryResolveNpcReference(int itemId, out int npcId)
        {
            npcId = 0;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (TryResolveNpcValue(itemProperty?["spec"] as WzSubProperty, out int resolvedNpcId)
                || TryResolveNpcValue(itemProperty?["info"] as WzSubProperty, out resolvedNpcId))
            {
                npcId = resolvedNpcId;
                return true;
            }

            return false;
        }

        public static bool HasAuthoredNpcInteraction(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return HasAuthoredNpcInteraction(itemProperty);
        }

        public static bool IsNotConsumedOnUse(int itemId)
        {
            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsNotConsumedOnUse(itemProperty?["info"] as WzSubProperty);
        }

        internal static bool IsNotConsumedOnUse(WzSubProperty infoProperty)
        {
            return GetIntValue(infoProperty?["noExpend"]) == 1
                   || GetIntValue(infoProperty?["notConsume"]) == 1;
        }

        public static bool IsPetFoodItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsPetFoodSpec(itemProperty?["spec"] as WzSubProperty);
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

            List<string> effectLines = BuildEffectLines(itemId, itemProperty, infoProperty, specProperty);
            List<string> metadataLines = BuildMetadataLines(
                itemId,
                infoProperty,
                specProperty,
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
                EffectLines = effectLines,
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
            if (inventoryType == InventoryType.CASH && group == 500)
            {
                category = "Item";
                imagePath = $"Pet/{itemId:D7}.img";
                return true;
            }

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

        private static int TryResolveSkillBookSuccessRate(string description)
        {
            Match match = SkillBookSuccessRateRegex.Match(description ?? string.Empty);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return 100;
            }

            return Math.Clamp(value, 0, 100);
        }

        private static bool TryResolveNpcValue(WzSubProperty property, out int npcId)
        {
            npcId = 0;
            int resolvedNpcId = GetIntValue(property?["npc"]);
            if (resolvedNpcId <= 0)
            {
                return false;
            }

            npcId = resolvedNpcId;
            return true;
        }

        internal static bool TryResolveSpecScript(WzSubProperty specProperty, out string script)
        {
            script = null;

            string resolvedScript = (specProperty?["script"] as WzStringProperty)?.Value;
            if (string.IsNullOrWhiteSpace(resolvedScript))
            {
                return false;
            }

            script = resolvedScript.Trim();
            return true;
        }

        internal static bool HasAuthoredNpcInteraction(WzSubProperty itemProperty)
        {
            return TryResolveNpcValue(itemProperty?["spec"] as WzSubProperty, out _)
                   || TryResolveNpcValue(itemProperty?["info"] as WzSubProperty, out _)
                   || TryResolveSpecScript(itemProperty?["spec"] as WzSubProperty, out _);
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

            uint headerCrc = ComputeClientCrc32(GetIntValue(infoProperty["reqLEV"]))
                | ComputeClientCrc32(GetIntValue(infoProperty["price"]))
                | ComputeClientCrc32(ReadDoubleValue(infoProperty["unitPrice"]));
            uint limitCrc = ComputeClientCrc32(GetIntValue(infoProperty["slotMax"]))
                | ComputeClientCrc32(GetIntValue(infoProperty["max"]));
            return commonCrc
                ^ headerCrc
                ^ limitCrc
                ^ ComputeClientCrc32(ComposeBundleItemFlagMask(infoProperty));
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

        private static List<string> BuildEffectLines(int itemId, WzSubProperty itemProperty, WzSubProperty infoProperty, WzSubProperty specProperty)
        {
            List<string> effectLines = new();
            AppendInfoEffectLines(effectLines, infoProperty);
            AppendChairRecoveryEffectLines(effectLines, infoProperty);
            AppendPetFoodEffectLine(effectLines, specProperty);
            AppendBuffItemEffectLines(effectLines, itemProperty?["buff"] as WzSubProperty);
            AppendScriptedUseEffectLines(effectLines, specProperty);
            AppendPickupModifierEffectLines(effectLines, specProperty);
            AppendMobEffectLines(effectLines, itemProperty?["mob"] as WzSubProperty);
            AppendRewardEffectLines(effectLines, itemProperty?["reward"] as WzSubProperty);

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
            AppendFatigueEffectLine(effectLines, specProperty["incFatigue"]);
            AppendMoveToEffectLine(effectLines, TryGetPositiveInt(specProperty["moveTo"]));
            AppendMorphEffectLine(effectLines, specProperty);
            AppendBoosterEffectLine(effectLines, specProperty["booster"], specProperty["indieBooster"]);
            AppendBerserkEffectLine(effectLines, specProperty["berserk"]);
            AppendThawEffectLine(effectLines, specProperty["thaw"]);
            AppendCrossContinentEffectLine(effectLines, specProperty["ignoreContinent"]);
            AppendReturnMapRecordEffectLine(effectLines, specProperty["returnMapQR"]);
            AppendRandomMoveInFieldSetEffectLine(effectLines, specProperty["randomMoveInFieldSet"]);
            AppendExperienceEffectLines(effectLines, specProperty);
            AppendMobEffectLines(effectLines, specProperty["mob"] as WzSubProperty);

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
            int itemId,
            WzSubProperty infoProperty,
            WzSubProperty specProperty,
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

            AppendInfoMetadataLines(metadataLines, itemId, infoProperty, specProperty);
            AppendMonsterBookMetadataLines(metadataLines, infoProperty);
            AppendQuestRequirementMetadataLines(metadataLines, infoProperty);
            AppendConsumeItemMetadataLines(metadataLines, infoProperty);

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

        private static void AppendInfoEffectLines(List<string> effectLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            for (int i = 0; i < InfoFlatEffectKeys.Length; i++)
            {
                (string key, string label) = InfoFlatEffectKeys[i];
                AppendStatEffectLine(effectLines, label, TryGetPositiveInt(infoProperty[key]), isPercent: false);
            }
        }

        private static void AppendChairRecoveryEffectLines(List<string> effectLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int recoveryHp = GetIntOrStringValue(infoProperty["recoveryHP"]);
            int recoveryMp = GetIntOrStringValue(infoProperty["recoveryMP"]);
            if (recoveryHp <= 0 && recoveryMp <= 0)
            {
                return;
            }

            int waitSeconds = GetIntOrStringValue(infoProperty["waittime"]);
            string intervalSuffix = waitSeconds > 0
                ? $" every {FormatSecondDuration(waitSeconds)}"
                : string.Empty;

            if (recoveryHp > 0 && recoveryMp > 0)
            {
                effectLines.Add(
                    $"Recovers {recoveryHp.ToString("N0", CultureInfo.InvariantCulture)} HP / {recoveryMp.ToString("N0", CultureInfo.InvariantCulture)} MP{intervalSuffix} while seated");
                return;
            }

            if (recoveryHp > 0)
            {
                effectLines.Add($"Recovers {recoveryHp.ToString("N0", CultureInfo.InvariantCulture)} HP{intervalSuffix} while seated");
                return;
            }

            effectLines.Add($"Recovers {recoveryMp.ToString("N0", CultureInfo.InvariantCulture)} MP{intervalSuffix} while seated");
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

        private static void AppendPetFoodEffectLine(List<string> effectLines, WzSubProperty specProperty)
        {
            int fullnessIncrease = GetIntValue(specProperty?["inc"]);
            if (fullnessIncrease <= 0 || !IsPetFoodSpec(specProperty))
            {
                return;
            }

            effectLines.Add($"Pet Fullness {FormatSignedValue(fullnessIncrease)}");
        }

        private static void AppendScriptedUseEffectLines(List<string> effectLines, WzSubProperty specProperty)
        {
            if (specProperty == null)
            {
                return;
            }

            int npcId = GetIntValue(specProperty["npc"]);
            if (npcId > 0)
            {
                string npcName = ResolveNpcName(npcId);
                effectLines.Add(string.IsNullOrWhiteSpace(npcName)
                    ? $"Opens NPC #{npcId.ToString(CultureInfo.InvariantCulture)}"
                    : $"Opens NPC: {npcName}");
            }

            string scriptName = (specProperty["script"] as WzStringProperty)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(scriptName))
            {
                effectLines.Add($"Uses script: {scriptName}");
            }

            if (GetIntValue(specProperty["runOnPickup"]) == 1)
            {
                effectLines.Add("Runs immediately on pickup");
            }
        }

        private static void AppendBuffItemEffectLines(List<string> effectLines, WzSubProperty buffProperty)
        {
            if (buffProperty?.WzProperties == null)
            {
                return;
            }

            HashSet<string> seenLines = new(StringComparer.Ordinal);
            foreach (WzImageProperty child in buffProperty.WzProperties)
            {
                if (child is not WzSubProperty buffEntry)
                {
                    continue;
                }

                int buffItemId = GetIntValue(buffEntry["buffItemID"]);
                if (buffItemId <= 0)
                {
                    continue;
                }

                string label = ResolveTooltipItemLabel(buffItemId);
                int probability = GetIntValue(buffEntry["prob"]);
                string line = probability > 0 && probability < 100
                    ? $"Buff Item: {label} ({probability.ToString(CultureInfo.InvariantCulture)}%)"
                    : $"Buff Item: {label}";
                if (seenLines.Add(line))
                {
                    effectLines.Add(line);
                }
            }
        }

        private static void AppendPickupModifierEffectLines(List<string> effectLines, WzSubProperty specProperty)
        {
            if (specProperty == null)
            {
                return;
            }

            int mesoPickupModifier = GetIntValue(specProperty["mesoupbyitem"]);
            int itemPickupModifier = GetIntValue(specProperty["itemupbyitem"]);
            int procChance = GetIntValue(specProperty["prob"]);
            string chanceSuffix = procChance > 0 ? $" ({procChance.ToString(CultureInfo.InvariantCulture)}% chance)" : string.Empty;

            if (mesoPickupModifier > 0)
            {
                effectLines.Add($"Meso Pickup Modifier: {mesoPickupModifier.ToString(CultureInfo.InvariantCulture)}{chanceSuffix}");
            }

            if (itemPickupModifier <= 0)
            {
                return;
            }

            int itemCode = GetIntValue(specProperty["itemCode"]);
            string targetItemLabel = itemCode > 0
                ? ResolveTooltipItemLabel(itemCode)
                : string.Empty;
            effectLines.Add(string.IsNullOrWhiteSpace(targetItemLabel)
                ? $"Item Pickup Modifier: {itemPickupModifier.ToString(CultureInfo.InvariantCulture)}{chanceSuffix}"
                : $"Item Pickup Modifier: {itemPickupModifier.ToString(CultureInfo.InvariantCulture)} ({targetItemLabel}){chanceSuffix}");
        }

        private static void AppendRewardEffectLines(List<string> effectLines, WzSubProperty rewardProperty)
        {
            if (rewardProperty?.WzProperties == null)
            {
                return;
            }

            IReadOnlyList<string> previewLines = BuildRewardPreviewLines(rewardProperty, RewardPreviewLineLimit);
            for (int i = 0; i < previewLines.Count; i++)
            {
                effectLines.Add(previewLines[i]);
            }
        }

        private static void AppendMobEffectLines(List<string> effectLines, WzSubProperty mobProperty)
        {
            if (mobProperty?.WzProperties == null)
            {
                return;
            }

            HashSet<string> seenLines = new(StringComparer.Ordinal);
            foreach (WzImageProperty child in mobProperty.WzProperties)
            {
                if (child is not WzSubProperty mobEntry)
                {
                    continue;
                }

                int mobId = GetIntValue(mobEntry["id"]);
                if (mobId <= 0)
                {
                    continue;
                }

                string mobLabel = ResolveMobTooltipLabel(mobId);
                int probability = GetIntValue(mobEntry["prob"]);
                string line = probability > 0 && probability < 100
                    ? $"Summons: {mobLabel} ({probability.ToString(CultureInfo.InvariantCulture)}%)"
                    : $"Summons: {mobLabel}";
                if (seenLines.Add(line))
                {
                    effectLines.Add(line);
                }
            }
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

        private static void AppendFatigueEffectLine(List<string> effectLines, WzImageProperty property)
        {
            int fatigue = GetIntValue(property);
            if (fatigue == 0)
            {
                return;
            }

            effectLines.Add($"Fatigue {FormatSignedValue(fatigue)}");
        }

        private static void AppendThawEffectLine(List<string> effectLines, WzImageProperty property)
        {
            int thaw = GetIntValue(property);
            if (thaw == 0)
            {
                return;
            }

            effectLines.Add($"Reduces environmental damage by {Math.Abs(thaw).ToString(CultureInfo.InvariantCulture)}");
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
                string questLabel = ResolveQuestTooltipLabel(returnMapRecordId);
                effectLines.Add(string.Equals(questLabel, $"Quest #{returnMapRecordId.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal)
                    ? $"Uses return-map record #{returnMapRecordId.ToString(CultureInfo.InvariantCulture)}"
                    : $"Uses return-map record: {questLabel}");
            }
        }

        private static void AppendRandomMoveInFieldSetEffectLine(List<string> effectLines, WzImageProperty property)
        {
            if (GetIntValue(property) == 1)
            {
                effectLines.Add("Moves to a random location in the current field");
            }
        }

        private static void AppendExperienceEffectLines(List<string> effectLines, WzSubProperty specProperty)
        {
            if (specProperty == null)
            {
                return;
            }

            int experienceAmount = GetIntOrStringValue(specProperty["expinc"]);
            if (experienceAmount <= 0)
            {
                experienceAmount = GetIntOrStringValue(specProperty["exp"]);
            }

            if (experienceAmount > 0)
            {
                effectLines.Add($"EXP +{experienceAmount.ToString("N0", CultureInfo.InvariantCulture)}");
            }
        }

        private static void AppendInfoMetadataLines(
            List<string> metadataLines,
            int itemId,
            WzSubProperty infoProperty,
            WzSubProperty specProperty = null)
        {
            if (infoProperty == null)
            {
                return;
            }

            int? successRate = TryGetPositiveInt(infoProperty["success"]);
            if (successRate.HasValue)
            {
                metadataLines.Add($"Success Rate: {successRate.Value.ToString(CultureInfo.InvariantCulture)}%");
            }

            int? curseRate = TryGetPositiveInt(infoProperty["cursed"]);
            if (curseRate.HasValue)
            {
                metadataLines.Add($"Destroyed on Failure: {curseRate.Value.ToString(CultureInfo.InvariantCulture)}%");
            }

            AppendSkillBookMetadataLines(metadataLines, infoProperty);
            AppendRequiredMapMetadataLines(metadataLines, infoProperty);
            AppendStateChangeItemMetadataLines(metadataLines, infoProperty);
            AppendItemPeriodMetadataLines(metadataLines, infoProperty);
            AppendCreateMetadataLines(metadataLines, infoProperty);
            AppendReplaceMetadataLines(metadataLines, infoProperty);
            AppendRecoveryRateMetadataLines(metadataLines, infoProperty);
            AppendRandomChairEffectMetadataLines(metadataLines, infoProperty);
            AppendAdditionalExperienceMetadataLines(metadataLines, infoProperty);
            AppendLevelUpWarningMetadataLines(metadataLines, infoProperty);
            AppendRecipeMetadataLines(metadataLines, specProperty);
            AppendCashAvailabilityMetadataLines(metadataLines, infoProperty);
            AppendAdditionalInfoFlagsMetadataLines(metadataLines, infoProperty, specProperty);
        }

        private static void AppendStateChangeItemMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int stateChangeItemId = GetIntOrStringValue(infoProperty?["stateChangeItem"]);
            if (stateChangeItemId > 0)
            {
                metadataLines.Add($"Linked Cash Effect: {ResolveTooltipItemLabel(stateChangeItemId)}");
            }
        }

        private static void AppendItemPeriodMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int itemPeriodHours = GetIntOrStringValue(infoProperty?["period"]);
            if (itemPeriodHours > 0)
            {
                metadataLines.Add($"Item expires after {FormatHourDuration(itemPeriodHours)}");
            }
        }

        private static void AppendSkillBookMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int? masterLevel = TryGetPositiveInt(infoProperty["masterLevel"]);
            if (masterLevel.HasValue)
            {
                metadataLines.Add($"Master Level: {masterLevel.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            int? requiredSkillLevel = TryGetPositiveInt(infoProperty["reqSkillLevel"]);
            if (requiredSkillLevel.HasValue)
            {
                metadataLines.Add($"Required Skill Level: {requiredSkillLevel.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["skill"] is not WzSubProperty skillProperty || skillProperty.WzProperties == null)
            {
                return;
            }

            HashSet<string> seenSkills = new(StringComparer.Ordinal);
            foreach (WzImageProperty child in skillProperty.WzProperties)
            {
                int skillId = GetIntValue(child);
                if (skillId <= 0)
                {
                    continue;
                }

                string skillLine = ResolveSkillTooltipLine(skillId);
                if (seenSkills.Add(skillLine))
                {
                    metadataLines.Add(skillLine);
                }
            }
        }

        private static void AppendRequiredMapMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty?["reqMap"] is not WzSubProperty reqMapProperty || reqMapProperty.WzProperties == null)
            {
                return;
            }

            List<string> mapNames = new();
            foreach (WzImageProperty child in reqMapProperty.WzProperties)
            {
                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mapId) || mapId <= 0)
                {
                    continue;
                }

                string mapName = ResolveMapName(mapId);
                mapNames.Add(string.IsNullOrWhiteSpace(mapName)
                    ? mapId.ToString(CultureInfo.InvariantCulture)
                    : mapName);
            }

            if (mapNames.Count > 0)
            {
                metadataLines.Add($"Usable In: {string.Join(", ", mapNames)}");
            }
        }

        private static void AppendMonsterBookMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int mobId = GetIntValue(infoProperty["mob"]);
            if (mobId <= 0)
            {
                return;
            }

            string mobLabel = ResolveMobTooltipLabel(mobId);
            bool isMonsterBookCard = GetIntValue(infoProperty["monsterBook"]) == 1;
            metadataLines.Add(isMonsterBookCard
                ? $"Monster Book Card: {mobLabel}"
                : $"Associated Mob: {mobLabel}");
        }

        private static void AppendQuestRequirementMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int requiredQuestId = GetIntValue(infoProperty["reqQuestOnProgress"]);
            if (requiredQuestId > 0)
            {
                metadataLines.Add($"Requires Quest In Progress: {ResolveQuestTooltipLabel(requiredQuestId)}");
            }

            if (GetIntValue(infoProperty["pquest"]) == 1)
            {
                metadataLines.Add("Party Quest Item");
            }
        }

        private static void AppendConsumeItemMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            WzSubProperty consumeItemProperty = infoProperty?["consumeitem"] as WzSubProperty
                                               ?? infoProperty?["consumeItem"] as WzSubProperty;
            if (consumeItemProperty == null)
            {
                return;
            }

            IReadOnlyList<string> requirementLines = BuildConsumeItemRequirementLines(consumeItemProperty, ConsumeItemRequirementLineLimit);
            for (int i = 0; i < requirementLines.Count; i++)
            {
                metadataLines.Add(requirementLines[i]);
            }
        }

        private static void AppendCreateMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int createdItemId = GetIntOrStringValue(infoProperty?["create"]);
            if (createdItemId <= 0)
            {
                return;
            }

            metadataLines.Add($"Creates: {ResolveTooltipItemLabel(createdItemId)}");

            int createPeriodDays = GetIntOrStringValue(infoProperty["createPeriod"]);
            if (createPeriodDays > 0)
            {
                metadataLines.Add($"Created item expires after {FormatDayCount(createPeriodDays)}");
            }
        }

        private static void AppendReplaceMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty?["replace"] is not WzSubProperty replaceProperty)
            {
                return;
            }

            int replacementItemId = GetIntOrStringValue(replaceProperty["itemid"]);
            if (replacementItemId > 0)
            {
                metadataLines.Add($"Replaces with: {ResolveTooltipItemLabel(replacementItemId)}");
            }

            int replacementPeriodMinutes = GetIntOrStringValue(replaceProperty["period"]);
            if (replacementPeriodMinutes > 0)
            {
                metadataLines.Add($"Replacement lasts {FormatMinuteDuration(replacementPeriodMinutes)}");
            }

            string replacementMessage = (replaceProperty["msg"] as WzStringProperty)?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(replacementMessage))
            {
                metadataLines.Add($"Replacement note: {replacementMessage}");
            }
        }

        private static void AppendRecoveryRateMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int recoveryRate = GetIntOrStringValue(infoProperty?["recoveryRate"]);
            if (recoveryRate > 0)
            {
                metadataLines.Add($"Recovery Rate {FormatSignedValue(recoveryRate)}%");
            }
        }

        private static void AppendRandomChairEffectMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty?["randEffect"] is not WzSubProperty randomEffectProperty
                || randomEffectProperty.WzProperties == null)
            {
                return;
            }

            HashSet<string> seenLines = new(StringComparer.Ordinal);
            foreach (WzImageProperty child in randomEffectProperty.WzProperties)
            {
                if (child is not WzSubProperty effectProperty)
                {
                    continue;
                }

                string expressionName = (effectProperty["face"] as WzStringProperty)?.Value;
                if (string.IsNullOrWhiteSpace(expressionName))
                {
                    continue;
                }

                int probability = GetIntOrStringValue(effectProperty["prob"]);
                string probabilitySuffix = probability > 0
                    ? $" ({probability.ToString(CultureInfo.InvariantCulture)}%)"
                    : string.Empty;
                string line = $"Chair Expression: {FormatTooltipKeywordLabel(expressionName)}{probabilitySuffix}";
                if (seenLines.Add(line))
                {
                    metadataLines.Add(line);
                }
            }
        }

        private static void AppendAdditionalExperienceMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            if (infoProperty["exp"] is WzSubProperty expProperty)
            {
                int minLevel = GetIntOrStringValue(expProperty["minLev"]);
                int maximumLevel = GetIntOrStringValue(expProperty["maxLev"]);
                if (minLevel > 0 && maximumLevel > 0)
                {
                    metadataLines.Add($"Level Range: Lv. {minLevel.ToString(CultureInfo.InvariantCulture)}-{maximumLevel.ToString(CultureInfo.InvariantCulture)}");
                }
                else if (minLevel > 0)
                {
                    metadataLines.Add($"Minimum Level: {minLevel.ToString(CultureInfo.InvariantCulture)}");
                }
                else if (maximumLevel > 0)
                {
                    metadataLines.Add($"Maximum Level: {maximumLevel.ToString(CultureInfo.InvariantCulture)}");
                }
            }

            int maxLevelValue = GetIntOrStringValue(infoProperty["maxLevel"]);
            if (maxLevelValue > 0)
            {
                metadataLines.Add($"Max Level: {maxLevelValue.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["cantAccountSharable"] is WzSubProperty cantAccountSharableProperty)
            {
                string tooltip = NormalizeTooltipText((cantAccountSharableProperty["tooltip"] as WzStringProperty)?.Value);
                if (!string.IsNullOrWhiteSpace(tooltip))
                {
                    metadataLines.Add(tooltip);
                }
            }
        }

        private static void AppendLevelUpWarningMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty?["LvUpWarning"] is not WzSubProperty warningProperty
                || warningProperty.WzProperties == null)
            {
                return;
            }

            SortedDictionary<string, List<int>> warningsByText = new(StringComparer.Ordinal);
            foreach (WzImageProperty child in warningProperty.WzProperties)
            {
                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                    || level <= 0)
                {
                    continue;
                }

                string warningText = NormalizeTooltipText(GetStringValue(child));
                if (string.IsNullOrWhiteSpace(warningText))
                {
                    continue;
                }

                if (!warningsByText.TryGetValue(warningText, out List<int> levels))
                {
                    levels = new List<int>();
                    warningsByText[warningText] = levels;
                }

                if (!levels.Contains(level))
                {
                    levels.Add(level);
                }
            }

            foreach (KeyValuePair<string, List<int>> entry in warningsByText)
            {
                entry.Value.Sort();
                string levelLabel = string.Join("/", entry.Value.ConvertAll(
                    level => level.ToString(CultureInfo.InvariantCulture)));
                metadataLines.Add($"Level-up warning (Lv. {levelLabel}): {entry.Key}");
            }
        }

        private static string GetStringValue(WzImageProperty property)
        {
            return property switch
            {
                WzStringProperty stringProperty => stringProperty.Value,
                WzIntProperty intProperty => intProperty.Value.ToString(CultureInfo.InvariantCulture),
                WzLongProperty longProperty => longProperty.Value.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
        }

        private static void AppendRecipeMetadataLines(List<string> metadataLines, WzSubProperty specProperty)
        {
            if (specProperty == null)
            {
                return;
            }

            int recipeId = GetIntOrStringValue(specProperty["recipe"]);
            if (recipeId > 0)
            {
                string recipeLine = TryResolveMakerRecipeFamily(recipeId, out string familyLabel)
                    ? $"Recipe: {familyLabel} recipe #{recipeId.ToString(CultureInfo.InvariantCulture)}"
                    : $"Recipe: #{recipeId.ToString(CultureInfo.InvariantCulture)}";
                metadataLines.Add(recipeLine);
            }

            int requiredSkillLevel = GetIntOrStringValue(specProperty["reqSkillLevel"]);
            if (requiredSkillLevel > 0)
            {
                metadataLines.Add($"Maker Skill Level Required: {requiredSkillLevel.ToString(CultureInfo.InvariantCulture)}");
            }

            int recipeUseCount = GetIntOrStringValue(specProperty["recipeUseCount"]);
            if (recipeUseCount > 0)
            {
                metadataLines.Add($"Recipe Uses: {recipeUseCount.ToString(CultureInfo.InvariantCulture)}");
            }

            int recipeValidDays = GetIntOrStringValue(specProperty["recipeValidDay"]);
            if (recipeValidDays > 0)
            {
                metadataLines.Add($"Recipe valid for {FormatDayCount(recipeValidDays)}");
            }
        }

        private static void AppendCashAvailabilityMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            if (GetIntValue(infoProperty["autoBuff"]) == 1)
            {
                metadataLines.Add("Auto Buff item");
            }

            if (GetIntValue(infoProperty["flatRate"]) == 1)
            {
                metadataLines.Add("Flat-rate item");
            }

            int limitMinutes = GetIntOrStringValue(infoProperty["limitMin"]);
            if (limitMinutes > 0)
            {
                metadataLines.Add($"Time limit: {FormatMinuteDuration(limitMinutes)}");
            }
        }

        private static void AppendAdditionalInfoFlagsMetadataLines(
            List<string> metadataLines,
            WzSubProperty infoProperty,
            WzSubProperty specProperty = null)
        {
            if (infoProperty == null)
            {
                return;
            }

            int tradeAvailable = GetIntOrStringValue(infoProperty["tradeAvailable"]);
            if (tradeAvailable > 0)
            {
                metadataLines.Add($"Trade available {tradeAvailable} time{(tradeAvailable == 1 ? string.Empty : "s")}");
            }

            if (GetIntValue(infoProperty["accountSharable"]) == 1
                || GetIntValue(infoProperty["accountShareable"]) == 1)
            {
                metadataLines.Add("Account-sharable");
            }

            if (GetIntValue(infoProperty["timeLimited"]) == 1)
            {
                metadataLines.Add("Time-limited item");
            }

            if (GetIntValue(infoProperty["expireOnLogout"]) == 1)
            {
                metadataLines.Add("Expires on logout");
            }

            if (GetIntValue(infoProperty["buffchair"]) == 1)
            {
                metadataLines.Add("Buff Chair");
            }

            int cooltimeSeconds = GetIntOrStringValue(infoProperty["cooltime"]);
            if (cooltimeSeconds > 0)
            {
                metadataLines.Add($"Cooldown: {FormatSecondDuration(cooltimeSeconds)}");
            }

            if (GetIntValue(infoProperty["dropBlock"]) == 1)
            {
                metadataLines.Add("Cannot be dropped");
            }

            if (GetIntValue(infoProperty["noMoveToLocker"]) == 1)
            {
                metadataLines.Add("Cannot be moved to locker");
            }

            if (IsNotConsumedOnUse(infoProperty))
            {
                metadataLines.Add("Not consumed on use");
            }

            AppendPetUtilityMetadataLines(metadataLines, infoProperty);

            int expRate = GetIntOrStringValue(infoProperty["expRate"]);
            if (expRate > 0)
            {
                metadataLines.Add($"EXP Rate {FormatSignedValue(expRate)}%");
            }

            int bonusExpRate = GetIntOrStringValue(infoProperty["bonusEXPRate"]);
            if (bonusExpRate > 0)
            {
                metadataLines.Add($"Bonus EXP Rate {FormatSignedValue(bonusExpRate)}%");
            }

            if (GetIntValue(infoProperty["cashExpTicketOn"]) == 1)
            {
                metadataLines.Add("Cash EXP Ticket: Enabled");
            }

            if (GetIntValue(infoProperty["partyExpOn"]) == 1)
            {
                metadataLines.Add("Party EXP: Enabled");
            }

            int maxExp = GetIntOrStringValue(infoProperty["maxExp"]);
            if (maxExp > 0)
            {
                metadataLines.Add($"Max EXP: {maxExp.ToString("N0", CultureInfo.InvariantCulture)}");
            }

            int nickSkillId = GetIntOrStringValue(infoProperty["nickSkill"]);
            if (nickSkillId > 0)
            {
                metadataLines.Add($"Nickname Effect: {ResolveSkillTooltipLabel(nickSkillId)}");
            }

            AppendPersonalityExperienceMetadataLines(metadataLines, infoProperty, specProperty);
        }

        private static void AppendPetUtilityMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            if (GetIntValue(infoProperty["pickupItem"]) == 1)
            {
                metadataLines.Add("Automatically loots items");
            }

            if (GetIntValue(infoProperty["sweepForDrop"]) == 1)
            {
                metadataLines.Add("Sweeps nearby drops");
            }

            if (GetIntValue(infoProperty["consumeHP"]) == 1)
            {
                metadataLines.Add("Automatically consumes HP items");
            }

            if (GetIntValue(infoProperty["consumeMP"]) == 1)
            {
                metadataLines.Add("Automatically consumes MP items");
            }

            if (GetIntValue(infoProperty["useBasicSkill"]) == 1)
            {
                metadataLines.Add("Uses pet basic skills");
            }

            if (GetIntValue(infoProperty["noScroll"]) == 1)
            {
                metadataLines.Add("Cannot use pet scrolls");
            }

            if (GetIntValue(infoProperty["noRevive"]) == 1)
            {
                metadataLines.Add("Cannot be revived");
            }
        }

        private static void AppendPersonalityExperienceMetadataLines(
            List<string> metadataLines,
            WzSubProperty infoProperty,
            WzSubProperty specProperty)
        {
            for (int i = 0; i < PersonalityExperienceKeys.Length; i++)
            {
                (string key, string label) = PersonalityExperienceKeys[i];
                int experienceAmount = GetIntOrStringValue(infoProperty?[key]);
                if (experienceAmount <= 0)
                {
                    experienceAmount = GetIntOrStringValue(specProperty?[key]);
                }

                if (experienceAmount > 0)
                {
                    metadataLines.Add($"{label} {FormatSignedValue(experienceAmount)}");
                }
            }
        }

        private static bool HasNumericChildEntries(WzSubProperty property, params string[] ignoredNames)
        {
            if (property?.WzProperties == null)
            {
                return false;
            }

            HashSet<string> ignored = ignoredNames != null && ignoredNames.Length > 0
                ? new HashSet<string>(ignoredNames, StringComparer.OrdinalIgnoreCase)
                : null;
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child == null || (ignored != null && ignored.Contains(child.Name)))
                {
                    continue;
                }

                if (int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && GetIntValue(child) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPetFoodSpec(WzSubProperty specProperty)
        {
            return GetIntValue(specProperty?["inc"]) > 0
                   && HasNumericChildEntries(specProperty, "inc");
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

        private static string ResolveNpcName(int npcId)
        {
            if (global::HaCreator.Program.InfoManager?.NpcNameCache == null)
            {
                return string.Empty;
            }

            string key = npcId.ToString(CultureInfo.InvariantCulture);
            return global::HaCreator.Program.InfoManager.NpcNameCache.TryGetValue(key, out Tuple<string, string> npcInfo)
                   && npcInfo != null
                ? npcInfo.Item1?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static string ResolveMobName(int mobId)
        {
            if (global::HaCreator.Program.InfoManager?.MobNameCache == null)
            {
                return string.Empty;
            }

            string key = mobId.ToString(CultureInfo.InvariantCulture);
            return global::HaCreator.Program.InfoManager.MobNameCache.TryGetValue(key, out string mobName)
                   && !string.IsNullOrWhiteSpace(mobName)
                ? mobName.Trim()
                : string.Empty;
        }

        private static string ResolveMobTooltipLabel(int mobId)
        {
            string mobName = ResolveMobName(mobId);
            return string.IsNullOrWhiteSpace(mobName)
                ? $"Mob #{mobId.ToString(CultureInfo.InvariantCulture)}"
                : mobName;
        }

        private static string FormatTooltipKeywordLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().Replace('_', ' ').Replace('-', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized);
        }

        private static string NormalizeTooltipText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Replace("\\r", " ")
                       .Replace("\\n", " ")
                       .Replace('\r', ' ')
                       .Replace('\n', ' ')
                       .Trim();
        }

        private static string ResolveTooltipItemLabel(int itemId)
        {
            if (TryResolveItemName(itemId, out string itemName) && !string.IsNullOrWhiteSpace(itemName))
            {
                return itemName.Trim();
            }

            return $"Item #{itemId.ToString(CultureInfo.InvariantCulture)}";
        }

        private static bool TryResolveMakerRecipeFamily(int recipeId, out string familyLabel)
        {
            familyLabel = null;
            if (recipeId <= 0)
            {
                return false;
            }

            ItemMakerRecipeFamily family = (recipeId / 10000) switch
            {
                9201 => ItemMakerRecipeFamily.Gloves,
                9202 => ItemMakerRecipeFamily.Shoes,
                9203 => ItemMakerRecipeFamily.Toys,
                9200 => ItemMakerRecipeFamily.Generic,
                _ => (ItemMakerRecipeFamily)(-1)
            };

            familyLabel = family switch
            {
                ItemMakerRecipeFamily.Gloves => "Glove",
                ItemMakerRecipeFamily.Shoes => "Shoe",
                ItemMakerRecipeFamily.Toys => "Toy",
                ItemMakerRecipeFamily.Generic => "Generic",
                _ => null
            };

            return !string.IsNullOrWhiteSpace(familyLabel);
        }

        private static string ResolveSkillTooltipLine(int skillId)
        {
            string skillName = ResolveSkillName(skillId);
            return string.IsNullOrWhiteSpace(skillName)
                ? $"Skill: #{skillId.ToString(CultureInfo.InvariantCulture)}"
                : $"Skill: {skillName}";
        }

        private static string ResolveSkillTooltipLabel(int skillId)
        {
            string skillName = ResolveSkillName(skillId);
            return string.IsNullOrWhiteSpace(skillName)
                ? $"Skill #{skillId.ToString(CultureInfo.InvariantCulture)}"
                : skillName;
        }

        private static string ResolveQuestTooltipLabel(int questId)
        {
            if (global::HaCreator.Program.InfoManager?.QuestInfos != null
                && global::HaCreator.Program.InfoManager.QuestInfos.TryGetValue(
                    questId.ToString(CultureInfo.InvariantCulture),
                    out WzSubProperty questProperty))
            {
                string questName = (questProperty?["name"] as WzStringProperty)?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(questName))
                {
                    return questName;
                }
            }

            return $"Quest #{questId.ToString(CultureInfo.InvariantCulture)}";
        }

        private static IReadOnlyList<string> BuildRewardPreviewLines(WzSubProperty rewardProperty, int previewLineLimit)
        {
            List<string> lines = new();
            if (rewardProperty?.WzProperties == null)
            {
                return lines;
            }

            List<WzSubProperty> entries = GetNumericNamedChildren(rewardProperty);
            if (entries.Count == 0)
            {
                return lines;
            }

            int maxLines = previewLineLimit <= 0 ? RewardPreviewLineLimit : previewLineLimit;
            int visibleCount = Math.Min(entries.Count, maxLines);
            for (int i = 0; i < visibleCount; i++)
            {
                WzSubProperty entry = entries[i];
                int itemId = GetIntOrStringValue(entry["item"]);
                if (itemId <= 0)
                {
                    continue;
                }

                string itemLabel = ResolveTooltipItemLabel(itemId);
                int count = Math.Max(1, GetIntOrStringValue(entry["count"]));
                int probability = GetIntOrStringValue(entry["prob"]);
                string chanceSuffix = probability > 0
                    ? $" ({probability.ToString(CultureInfo.InvariantCulture)}%)"
                    : string.Empty;
                int periodMinutes = GetIntOrStringValue(entry["period"]);
                string periodSuffix = periodMinutes > 0
                    ? $", expires after {FormatMinuteDuration(periodMinutes)}"
                    : string.Empty;
                lines.Add($"Reward: {itemLabel} x{count.ToString(CultureInfo.InvariantCulture)}{chanceSuffix}{periodSuffix}");
            }

            int remaining = entries.Count - visibleCount;
            if (remaining > 0)
            {
                lines.Add($"Reward: ... and {remaining.ToString(CultureInfo.InvariantCulture)} more");
            }

            return lines;
        }

        private static IReadOnlyList<string> BuildConsumeItemRequirementLines(WzSubProperty consumeItemProperty, int previewLineLimit)
        {
            List<string> lines = new();
            if (consumeItemProperty?.WzProperties == null)
            {
                return lines;
            }

            List<ConsumeItemRequirementEntry> entries = GetConsumeItemRequirementEntries(consumeItemProperty);
            if (entries.Count == 0)
            {
                return lines;
            }

            int maxLines = previewLineLimit <= 0 ? ConsumeItemRequirementLineLimit : previewLineLimit;
            int visibleCount = Math.Min(entries.Count, maxLines);
            for (int i = 0; i < visibleCount; i++)
            {
                ConsumeItemRequirementEntry entry = entries[i];
                int itemId = entry.ItemId;
                if (itemId <= 0)
                {
                    continue;
                }

                string itemLabel = ResolveTooltipItemLabel(itemId);
                int count = Math.Max(1, entry.Count);
                int rate = entry.Rate;
                string countSuffix = count > 1 ? $" x{count.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
                string rateSuffix = rate > 0 ? $" ({rate.ToString(CultureInfo.InvariantCulture)}%)" : string.Empty;
                lines.Add($"Consumes: {itemLabel}{countSuffix}{rateSuffix}");
            }

            int remaining = entries.Count - visibleCount;
            if (remaining > 0)
            {
                lines.Add($"Consumes: ... and {remaining.ToString(CultureInfo.InvariantCulture)} more");
            }

            return lines;
        }

        private static List<ConsumeItemRequirementEntry> GetConsumeItemRequirementEntries(WzSubProperty consumeItemProperty)
        {
            List<(int Index, ConsumeItemRequirementEntry Entry)> indexedEntries = new();
            if (consumeItemProperty?.WzProperties == null)
            {
                return new List<ConsumeItemRequirementEntry>();
            }

            foreach (WzImageProperty child in consumeItemProperty.WzProperties)
            {
                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    continue;
                }

                if (child is WzSubProperty structuredEntry)
                {
                    int itemId = GetIntOrStringValue(structuredEntry["itemcode"]);
                    if (itemId <= 0)
                    {
                        itemId = GetIntOrStringValue(structuredEntry["item"]);
                    }

                    if (itemId <= 0)
                    {
                        continue;
                    }

                    indexedEntries.Add((index, new ConsumeItemRequirementEntry
                    {
                        ItemId = itemId,
                        Count = Math.Max(1, GetIntOrStringValue(structuredEntry["count"])),
                        Rate = Math.Max(0, GetIntOrStringValue(structuredEntry["rate"]))
                    }));
                    continue;
                }

                int simpleItemId = GetIntOrStringValue(child);
                if (simpleItemId <= 0)
                {
                    continue;
                }

                indexedEntries.Add((index, new ConsumeItemRequirementEntry
                {
                    ItemId = simpleItemId,
                    Count = 1,
                    Rate = 0
                }));
            }

            indexedEntries.Sort((left, right) => left.Index.CompareTo(right.Index));
            List<ConsumeItemRequirementEntry> sortedEntries = new(indexedEntries.Count);
            for (int i = 0; i < indexedEntries.Count; i++)
            {
                sortedEntries.Add(indexedEntries[i].Entry);
            }

            return sortedEntries;
        }

        public static IReadOnlyList<string> BuildRewardPreviewLinesForTests(WzSubProperty rewardProperty, int previewLineLimit = RewardPreviewLineLimit)
        {
            return BuildRewardPreviewLines(rewardProperty, previewLineLimit);
        }

        public static IReadOnlyList<string> BuildConsumeItemRequirementLinesForTests(WzSubProperty consumeItemProperty, int previewLineLimit = ConsumeItemRequirementLineLimit)
        {
            return BuildConsumeItemRequirementLines(consumeItemProperty, previewLineLimit);
        }

        public static IReadOnlyList<string> BuildEffectLinesForTests(WzSubProperty specProperty)
        {
            return BuildEffectLines(0, null, null, specProperty);
        }

        public static IReadOnlyList<string> BuildEffectLinesForTests(WzSubProperty infoProperty, WzSubProperty specProperty)
        {
            return BuildEffectLines(0, null, infoProperty, specProperty);
        }

        public static IReadOnlyList<string> BuildEffectLinesForTests(WzSubProperty itemProperty, WzSubProperty infoProperty, WzSubProperty specProperty)
        {
            return BuildEffectLines(0, itemProperty, infoProperty, specProperty);
        }

        public static IReadOnlyList<string> BuildQuestRequirementMetadataLinesForTests(WzSubProperty infoProperty)
        {
            List<string> lines = new();
            AppendQuestRequirementMetadataLines(lines, infoProperty);
            return lines;
        }

        public static IReadOnlyList<string> BuildInfoMetadataLinesForTests(int itemId, WzSubProperty infoProperty)
        {
            List<string> lines = new();
            AppendInfoMetadataLines(lines, itemId, infoProperty);
            return lines;
        }

        public static IReadOnlyList<string> BuildMetadataLinesForTests(WzSubProperty infoProperty, WzSubProperty specProperty)
        {
            return BuildMetadataLines(
                0,
                infoProperty,
                specProperty,
                isNotForSale: false,
                isQuestItem: false,
                isTradeBlocked: false,
                isOneOfAKind: false,
                expirationDateUtc: null);
        }

        public static IReadOnlyList<string> BuildReturnMapRecordEffectLinesForTests(WzImageProperty property)
        {
            List<string> lines = new();
            AppendReturnMapRecordEffectLine(lines, property);
            return lines;
        }

        private static string ResolveSkillName(int skillId)
        {
            if (global::HaCreator.Program.InfoManager?.SkillNameCache == null)
            {
                return string.Empty;
            }

            string key = skillId.ToString(CultureInfo.InvariantCulture);
            return global::HaCreator.Program.InfoManager.SkillNameCache.TryGetValue(key, out Tuple<string, string> skillInfo)
                   && skillInfo != null
                ? skillInfo.Item1?.Trim() ?? string.Empty
                : string.Empty;
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

        private static string FormatSecondDuration(int seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            if (duration.TotalDays >= 1d && Math.Abs(duration.TotalDays - Math.Round(duration.TotalDays)) < 0.0001d)
            {
                int wholeDays = Math.Max(1, (int)Math.Round(duration.TotalDays));
                return FormatDayCount(wholeDays);
            }

            if (duration.TotalHours >= 1d && Math.Abs(duration.TotalHours - Math.Round(duration.TotalHours)) < 0.0001d)
            {
                int wholeHours = Math.Max(1, (int)Math.Round(duration.TotalHours));
                return wholeHours == 1
                    ? "1 hour"
                    : $"{wholeHours.ToString(CultureInfo.InvariantCulture)} hours";
            }

            if (duration.TotalMinutes >= 1d && Math.Abs(duration.TotalMinutes - Math.Round(duration.TotalMinutes)) < 0.0001d)
            {
                int wholeMinutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes));
                return wholeMinutes == 1
                    ? "1 minute"
                    : $"{wholeMinutes.ToString(CultureInfo.InvariantCulture)} minutes";
            }

            return seconds == 1
                ? "1 second"
                : $"{seconds.ToString(CultureInfo.InvariantCulture)} seconds";
        }

        private static string FormatDayCount(int days)
        {
            return days == 1
                ? "1 day"
                : $"{days.ToString(CultureInfo.InvariantCulture)} days";
        }

        private static string FormatMinuteDuration(int minutes)
        {
            TimeSpan duration = TimeSpan.FromMinutes(minutes);
            if (duration.TotalDays >= 1d && Math.Abs(duration.TotalDays - Math.Round(duration.TotalDays)) < 0.0001d)
            {
                int wholeDays = Math.Max(1, (int)Math.Round(duration.TotalDays));
                return FormatDayCount(wholeDays);
            }

            if (duration.TotalHours >= 1d && Math.Abs(duration.TotalHours - Math.Round(duration.TotalHours)) < 0.0001d)
            {
                int wholeHours = Math.Max(1, (int)Math.Round(duration.TotalHours));
                return wholeHours == 1
                    ? "1 hour"
                    : $"{wholeHours.ToString(CultureInfo.InvariantCulture)} hours";
            }

            return minutes == 1
                ? "1 minute"
                : $"{minutes.ToString(CultureInfo.InvariantCulture)} minutes";
        }

        private static string FormatHourDuration(int hours)
        {
            TimeSpan duration = TimeSpan.FromHours(hours);
            if (duration.TotalDays >= 1d && Math.Abs(duration.TotalDays - Math.Round(duration.TotalDays)) < 0.0001d)
            {
                int wholeDays = Math.Max(1, (int)Math.Round(duration.TotalDays));
                return FormatDayCount(wholeDays);
            }

            return hours == 1
                ? "1 hour"
                : $"{hours.ToString(CultureInfo.InvariantCulture)} hours";
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

        private static int GetIntOrStringValue(WzImageProperty property)
        {
            if (property is WzStringProperty stringProperty
                && int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            return GetIntValue(property);
        }

        private static List<WzSubProperty> GetNumericNamedChildren(WzSubProperty property)
        {
            List<(int Index, WzSubProperty Entry)> indexedEntries = new();
            if (property?.WzProperties == null)
            {
                return new List<WzSubProperty>();
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child is not WzSubProperty entry
                    || !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    continue;
                }

                indexedEntries.Add((index, entry));
            }

            indexedEntries.Sort((left, right) => left.Index.CompareTo(right.Index));
            List<WzSubProperty> sorted = new(indexedEntries.Count);
            for (int i = 0; i < indexedEntries.Count; i++)
            {
                sorted.Add(indexedEntries[i].Entry);
            }

            return sorted;
        }

        private sealed class ConsumeItemRequirementEntry
        {
            public int ItemId { get; init; }
            public int Count { get; init; } = 1;
            public int Rate { get; init; }
        }
    }
}
