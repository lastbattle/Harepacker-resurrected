using HaCreator.MapSimulator.Character.Skills;
using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Combat;
using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.Fields;
using MapleLib.ClientLib;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using HaCreator.MapSimulator.Managers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class QuestRewardRaiseItemMetadata
    {
        public int OwnerItemId { get; init; }
        public int QuestId { get; init; }
        public int IncrementExpUnit { get; init; }
        public int Grade { get; init; }
        public int MaxDropCount { get; init; }
        public string UiData { get; init; } = string.Empty;
    }

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
        public IReadOnlyList<string> AuthoredSampleLines { get; init; } = Array.Empty<string>();
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

    public readonly struct ConsumeItemRequirementMetadata
    {
        public ConsumeItemRequirementMetadata(int itemId, int count, int rate)
        {
            ItemId = itemId;
            Count = Math.Max(1, count);
            Rate = Math.Max(0, rate);
        }

        public int ItemId { get; }
        public int Count { get; }
        public int Rate { get; }
    }

    public static class InventoryItemMetadataResolver
    {
        private const int DefaultStackLimit = 100;
        private const int RewardPreviewLineLimit = 5;
        private const int ConsumeItemRequirementLineLimit = 4;
        private const string StackLimitMetadataPrefix = "Max per Slot:";
        private const int WeddingInvitationCashCardItemId = 5090100;
        private const int WeddingInvitationTicketStartItemId = 5251000;
        private const int WeddingInvitationTicketEndItemId = 5251003;
        private const int WeddingInvitationTicketItemId = 5251100;
        private const int WeddingInvitationEtcCardStartGroup = 4211;
        private const int WeddingInvitationEtcCardEndGroup = 4212;
        private const string WeddingKeyword = "wedding";
        private const string InvitationKeyword = "invitation";
        private const string InvitationMisspellingKeyword = "inviation";
        private static readonly Regex SkillBookSuccessRateRegex = new(@"(\d+)\s*%\s*chance", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly (string Key, string Label)[] CurableStatusEffectKeys =
        {
            ("poison", "Poison"),
            ("seal", "Seal"),
            ("darkness", "Darkness"),
            ("weakness", "Weakness"),
            ("awake", "Sleep"),
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

        public static string FormatStackLimitMetadataLine(int maxStackSize)
        {
            return maxStackSize > 0
                ? $"{StackLimitMetadataPrefix} {maxStackSize.ToString("N0", CultureInfo.InvariantCulture)}"
                : string.Empty;
        }

        public static string BuildRuntimeFallbackStackLimitMetadataLine(int? maxStackSize, IReadOnlyList<string> metadataLines)
        {
            if (HasStackLimitMetadataLine(metadataLines))
            {
                return string.Empty;
            }

            int resolvedMaxStackSize = maxStackSize.GetValueOrDefault(1);
            return resolvedMaxStackSize > 1 && maxStackSize.HasValue
                ? FormatStackLimitMetadataLine(maxStackSize.Value)
                : string.Empty;
        }

        public static bool HasStackLimitMetadataLine(IReadOnlyList<string> metadataLines)
        {
            if (metadataLines == null)
            {
                return false;
            }

            for (int i = 0; i < metadataLines.Count; i++)
            {
                if (metadataLines[i]?.StartsWith(StackLimitMetadataPrefix, StringComparison.Ordinal) == true)
                {
                    return true;
                }
            }

            return false;
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

        public static bool IsPetPickupBlocked(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return HasPetPickupRestriction(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty);
        }

        public static bool IsPickupBlocked(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            WzSubProperty infoProperty = itemProperty?["info"] as WzSubProperty;
            return GetIntValue(infoProperty?["pickUpBlock"]) == 1;
        }

        internal static bool HasPetPickupRestriction(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return GetIntValue(specProperty?["notPickupByPet"]) == 1
                || GetIntValue(specExProperty?["notPickupByPet"]) == 1;
        }

        public static bool TryResolveSkillBookUseMetadata(int itemId, out SkillBookUseMetadata metadata)
        {
            metadata = default;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty?["info"] is not WzSubProperty infoProperty)
            {
                return false;
            }

            return TryResolveSkillBookUseMetadata(infoProperty, TryResolveItemDescription(itemId, out string description) ? description : null, out metadata);
        }

        internal static bool TryResolveSkillBookUseMetadata(
            WzSubProperty infoProperty,
            string description,
            out SkillBookUseMetadata metadata)
        {
            metadata = default;
            if (infoProperty == null)
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
            int successRatePercent = TryResolveSkillBookSuccessRate(infoProperty, description);
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
            return TryResolveSpecScript(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty,
                out script);
        }

        public static bool TryResolveSpecScripts(int itemId, out IReadOnlyList<string> scripts)
        {
            scripts = Array.Empty<string>();

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return TryResolveSpecScripts(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty,
                out scripts);
        }

        internal static bool TryResolveSpecScriptPublications(
            int itemId,
            out IReadOnlyList<FieldObjectScriptPublication> publications)
        {
            publications = Array.Empty<FieldObjectScriptPublication>();

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return TryResolveSpecScriptPublications(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty,
                out publications);
        }

        public static bool TryResolveSpecNpc(int itemId, out int npcId)
        {
            npcId = 0;

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            int resolvedNpcId = GetIntOrStringValue((itemProperty?["spec"] as WzSubProperty)?["npc"]);
            if (resolvedNpcId <= 0)
            {
                resolvedNpcId = GetIntOrStringValue((itemProperty?["specEx"] as WzSubProperty)?["npc"]);
            }

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
            return TryResolveNpcReference(itemProperty, out npcId);
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

        public static bool IsWeddingInvitationItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            return IsWeddingInvitationItem(
                itemId,
                ResolveInventoryType(itemId),
                itemName: null,
                itemDescription: null);
        }

        public static bool IsNotConsumedOnUse(int itemId)
        {
            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsNotConsumedOnUse(itemProperty?["info"] as WzSubProperty);
        }

        public static bool IsConsumedOnPickup(int itemId)
        {
            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsConsumedOnPickup(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty);
        }

        public static bool IsRunOnPickup(int itemId)
        {
            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsRunOnPickup(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty);
        }

        public static bool IsOnlyPickup(int itemId)
        {
            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsOnlyPickup(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty);
        }

        public static bool ShouldAutoRunOnPickupInteraction(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return ShouldAutoRunOnPickupInteraction(itemProperty);
        }

        internal static bool IsNotConsumedOnUse(WzSubProperty infoProperty)
        {
            return GetIntOrStringValue(infoProperty?["noExpend"]) == 1
                   || GetIntOrStringValue(infoProperty?["notConsume"]) == 1;
        }

        internal static bool IsConsumedOnPickup(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return GetIntValue(specProperty?["consumeOnPickup"]) == 1
                   || GetIntValue(specExProperty?["consumeOnPickup"]) == 1;
        }

        internal static bool IsRunOnPickup(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return GetIntValue(specProperty?["runOnPickup"]) == 1
                   || GetIntValue(specExProperty?["runOnPickup"]) == 1;
        }

        internal static bool IsOnlyPickup(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return GetIntValue(specProperty?["onlyPickup"]) == 1
                   || GetIntValue(specExProperty?["onlyPickup"]) == 1;
        }

        internal static bool IsDeathMarkCureSpec(WzSubProperty specProperty)
        {
            return GetIntValue(specProperty?["deathmark"]) == 1;
        }

        public static bool IsPetFoodItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsPetFoodSpec(itemId, itemProperty?["spec"] as WzSubProperty);
        }

        public static bool IsRandomMorphItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return IsRandomMorphSpec(itemProperty?["spec"] as WzSubProperty);
        }

        internal static bool IsRandomMorphSpec(WzSubProperty specProperty)
        {
            return specProperty?["morphRandom"] is WzSubProperty morphRandomProperty
                && morphRandomProperty.WzProperties != null
                && morphRandomProperty.WzProperties.Count > 0;
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

        internal static bool TryResolveRaiseOwnerContextForQuest(
            int questId,
            out QuestRewardRaiseOwnerContext ownerContext,
            out QuestRewardRaiseItemMetadata metadata)
        {
            ownerContext = null;
            metadata = null;
            if (questId <= 0)
            {
                return false;
            }

            metadata = EnumerateRaiseItemMetadata()
                .Where(candidate => candidate.QuestId == questId)
                .OrderByDescending(candidate => candidate.MaxDropCount)
                .ThenBy(candidate => candidate.OwnerItemId)
                .FirstOrDefault();
            if (metadata == null)
            {
                return false;
            }

            ownerContext = new QuestRewardRaiseOwnerContext
            {
                OwnerItemId = metadata.OwnerItemId,
                WindowMode = metadata.MaxDropCount > 0
                    ? QuestRewardRaiseWindowMode.PiecePlacement
                    : QuestRewardRaiseWindowMode.Selection,
                MaxDropCount = Math.Max(1, metadata.MaxDropCount),
                InitialQrData = 0
            };
            return true;
        }

        public static IReadOnlyList<ConsumeItemRequirementMetadata> ResolveConsumeItemRequirements(int itemId)
        {
            if (itemId <= 0)
            {
                return Array.Empty<ConsumeItemRequirementMetadata>();
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return ResolveConsumeItemRequirements(itemProperty?["info"] as WzSubProperty);
        }

        public static IReadOnlyList<ConsumeItemRequirementMetadata> ResolveConsumeItemRequirements(WzSubProperty infoProperty)
        {
            WzSubProperty consumeItemProperty = infoProperty?["consumeitem"] as WzSubProperty
                                               ?? infoProperty?["consumeItem"] as WzSubProperty;
            List<ConsumeItemRequirementEntry> entries = GetConsumeItemRequirementEntries(consumeItemProperty);
            if (entries.Count == 0)
            {
                return Array.Empty<ConsumeItemRequirementMetadata>();
            }

            List<ConsumeItemRequirementMetadata> resolvedEntries = new(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                ConsumeItemRequirementEntry entry = entries[i];
                if (entry.ItemId <= 0)
                {
                    continue;
                }

                resolvedEntries.Add(new ConsumeItemRequirementMetadata(entry.ItemId, entry.Count, entry.Rate));
            }

            return resolvedEntries;
        }

        public static bool TrySelectConsumeItemRequirement(
            IReadOnlyList<ConsumeItemRequirementMetadata> requirements,
            Func<int, int> ownedItemCountResolver,
            int weightedRoll,
            out ConsumeItemRequirementMetadata selectedRequirement)
        {
            selectedRequirement = default;
            if (requirements == null || requirements.Count == 0 || ownedItemCountResolver == null)
            {
                return false;
            }

            List<ConsumeItemRequirementMetadata> eligibleRequirements = new(requirements.Count);
            int totalWeightedRate = 0;
            for (int i = 0; i < requirements.Count; i++)
            {
                ConsumeItemRequirementMetadata requirement = requirements[i];
                if (requirement.ItemId <= 0 || ownedItemCountResolver(requirement.ItemId) < requirement.Count)
                {
                    continue;
                }

                eligibleRequirements.Add(requirement);
                if (requirement.Rate > 0)
                {
                    totalWeightedRate += requirement.Rate;
                }
            }

            if (eligibleRequirements.Count == 0)
            {
                return false;
            }

            if (totalWeightedRate <= 0)
            {
                selectedRequirement = eligibleRequirements[0];
                return true;
            }

            int normalizedRoll = Math.Clamp(weightedRoll, 1, totalWeightedRate);
            int cumulativeRate = 0;
            for (int i = 0; i < eligibleRequirements.Count; i++)
            {
                ConsumeItemRequirementMetadata requirement = eligibleRequirements[i];
                if (requirement.Rate <= 0)
                {
                    continue;
                }

                cumulativeRate += requirement.Rate;
                if (normalizedRoll <= cumulativeRate)
                {
                    selectedRequirement = requirement;
                    return true;
                }
            }

            selectedRequirement = eligibleRequirements[0];
            return true;
        }

        public static int ResolveItemCooldownSeconds(int itemId)
        {
            if (itemId <= 0)
            {
                return 0;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return ResolveItemCooldownSeconds(itemProperty?["info"] as WzSubProperty);
        }

        public static int ResolveSetupUseLevelRequirement(int itemId)
        {
            if (itemId <= 0)
            {
                return 0;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            return ResolveSetupUseLevelRequirement(
                itemProperty?["spec"] as WzSubProperty,
                itemProperty?["specEx"] as WzSubProperty);
        }

        internal static int ResolveSetupUseLevelRequirement(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            int specUseLevel = Math.Max(0, GetIntOrStringValue(specProperty?["useLevel"]));
            int specExUseLevel = Math.Max(0, GetIntOrStringValue(specExProperty?["useLevel"]));
            return Math.Max(specUseLevel, specExUseLevel);
        }

        public static int ResolveItemCooldownSeconds(WzSubProperty infoProperty)
        {
            return Math.Max(0, GetIntOrStringValue(infoProperty?["cooltime"]));
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
            WzSubProperty specExProperty = itemProperty?["specEx"] as WzSubProperty;

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

            List<string> effectLines = BuildEffectLines(itemId, itemProperty, infoProperty, specProperty, specExProperty);
            IReadOnlyList<string> authoredSampleLines = BuildAuthoredSampleTextLines(itemProperty?["sample"] as WzSubProperty);
            List<string> metadataLines = BuildMetadataLines(
                itemId,
                infoProperty,
                specProperty,
                specExProperty,
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
                RequiredLevel = ResolveRequiredLevel(infoProperty),
                Price = TryGetPositiveInt(infoProperty?["price"]),
                UnitPrice = TryGetPositiveDouble(infoProperty?["unitPrice"]),
                ExpirationDateUtc = expirationDateUtc,
                EffectLines = effectLines,
                MetadataLines = metadataLines,
                AuthoredSampleLines = authoredSampleLines
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

        public static bool TryResolveInfoCanvas(int itemId, string canvasName, out WzCanvasProperty canvas)
        {
            canvas = null;
            if (itemId <= 0 || string.IsNullOrWhiteSpace(canvasName))
            {
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            canvas = (itemProperty?["info"] as WzSubProperty)?[canvasName] as WzCanvasProperty;
            return canvas != null;
        }

        public static bool TryResolveRootCanvas(int itemId, string canvasPath, out WzCanvasProperty canvas)
        {
            canvas = null;
            if (itemId <= 0 || string.IsNullOrWhiteSpace(canvasPath))
            {
                return false;
            }

            return TryResolveCanvasAtPath(LoadItemProperty(itemId), canvasPath, out canvas);
        }

        public static bool TryResolveSampleUiFrame(
            int itemId,
            out WzCanvasProperty topCanvas,
            out WzCanvasProperty centerCanvas,
            out WzCanvasProperty bottomCanvas)
        {
            return TryResolveSampleUiFrame(
                LoadItemProperty(itemId),
                out topCanvas,
                out centerCanvas,
                out bottomCanvas);
        }

        private static bool TryResolveSampleUiFrame(
            WzSubProperty itemProperty,
            out WzCanvasProperty topCanvas,
            out WzCanvasProperty centerCanvas,
            out WzCanvasProperty bottomCanvas)
        {
            topCanvas = null;
            centerCanvas = null;
            bottomCanvas = null;

            return TryResolveCanvasAtPath(itemProperty, "ui/t", out topCanvas)
                   && TryResolveCanvasAtPath(itemProperty, "ui/c", out centerCanvas)
                   && TryResolveCanvasAtPath(itemProperty, "ui/s", out bottomCanvas);
        }

        private static bool TryResolveCanvasAtPath(WzSubProperty rootProperty, string canvasPath, out WzCanvasProperty canvas)
        {
            canvas = null;
            if (rootProperty == null || string.IsNullOrWhiteSpace(canvasPath))
            {
                return false;
            }

            WzImageProperty current = rootProperty;
            string[] parts = canvasPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (current is not WzSubProperty subProperty)
                {
                    return false;
                }

                current = subProperty[parts[i]];
                if (current == null)
                {
                    return false;
                }
            }

            canvas = current as WzCanvasProperty;
            return canvas != null;
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

        private static IReadOnlyList<QuestRewardRaiseItemMetadata> _raiseItemMetadataCache;

        private static IReadOnlyList<QuestRewardRaiseItemMetadata> EnumerateRaiseItemMetadata()
        {
            if (_raiseItemMetadataCache != null)
            {
                return _raiseItemMetadataCache;
            }

            var metadata = new List<QuestRewardRaiseItemMetadata>();
            var itemImage = global::HaCreator.Program.FindImage("Item", "Etc/0422.img");
            if (itemImage == null)
            {
                _raiseItemMetadataCache = Array.Empty<QuestRewardRaiseItemMetadata>();
                return _raiseItemMetadataCache;
            }

            itemImage.ParseImage();
            foreach (WzImageProperty property in itemImage.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (property is not WzSubProperty itemProperty
                    || !int.TryParse(itemProperty.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ownerItemId)
                    || itemProperty["info"] is not WzSubProperty infoProperty)
                {
                    continue;
                }

                string uiData = (infoProperty["uiData"] as WzStringProperty)?.Value?.Trim() ?? string.Empty;
                int questId = GetIntValue(infoProperty["questId"]);
                if (questId <= 0 || string.IsNullOrWhiteSpace(uiData) || uiData.IndexOf("/raise/", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                metadata.Add(new QuestRewardRaiseItemMetadata
                {
                    OwnerItemId = ownerItemId,
                    QuestId = questId,
                    IncrementExpUnit = GetIntValue(infoProperty["exp"]),
                    Grade = GetIntValue(infoProperty["grade"]),
                    MaxDropCount = CountPositiveRaiseItemEntries(infoProperty["item"] as WzSubProperty),
                    UiData = uiData
                });
            }

            _raiseItemMetadataCache = metadata;
            return _raiseItemMetadataCache;
        }

        private static int CountPositiveRaiseItemEntries(WzSubProperty itemListProperty)
        {
            if (itemListProperty?.WzProperties == null || itemListProperty.WzProperties.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < itemListProperty.WzProperties.Count; i++)
            {
                WzSubProperty entryProperty = itemListProperty.WzProperties[i] as WzSubProperty;
                int itemId = GetIntValue(entryProperty?["0"]);
                if (itemId > 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static int TryResolveSkillBookSuccessRate(WzSubProperty infoProperty, string description)
        {
            if (infoProperty?["success"] != null)
            {
                return Math.Clamp(GetIntValue(infoProperty["success"]), 0, 100);
            }

            return TryResolveSkillBookSuccessRate(description);
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
            int resolvedNpcId = GetIntOrStringValue(property?["npc"]);
            if (resolvedNpcId <= 0)
            {
                return false;
            }

            npcId = resolvedNpcId;
            return true;
        }

        public static bool TryResolveNpcValueForTests(WzSubProperty property, out int npcId)
        {
            return TryResolveNpcValue(property, out npcId);
        }

        internal static bool TryResolveNpcReference(WzSubProperty itemProperty, out int npcId)
        {
            npcId = 0;
            if (TryResolveNpcValue(itemProperty?["spec"] as WzSubProperty, out int resolvedNpcId)
                || TryResolveNpcValue(itemProperty?["specEx"] as WzSubProperty, out resolvedNpcId)
                || TryResolveNpcValue(itemProperty?["info"] as WzSubProperty, out resolvedNpcId))
            {
                npcId = resolvedNpcId;
                return true;
            }

            return false;
        }

        public static bool TryResolveNpcReferenceForTests(WzSubProperty itemProperty, out int npcId)
        {
            return TryResolveNpcReference(itemProperty, out npcId);
        }

        public static bool IsNotConsumedOnUseForTests(WzSubProperty infoProperty)
        {
            return IsNotConsumedOnUse(infoProperty);
        }

        public static bool IsConsumedOnPickupForTests(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return IsConsumedOnPickup(specProperty, specExProperty);
        }

        public static bool IsRunOnPickupForTests(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return IsRunOnPickup(specProperty, specExProperty);
        }

        public static bool IsOnlyPickupForTests(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return IsOnlyPickup(specProperty, specExProperty);
        }

        public static bool IsDeathMarkCureSpecForTests(WzSubProperty specProperty)
        {
            return IsDeathMarkCureSpec(specProperty);
        }

        public static int ResolveSetupUseLevelRequirementForTests(WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return ResolveSetupUseLevelRequirement(specProperty, specExProperty);
        }

        internal static bool TryResolveSpecScript(WzSubProperty specProperty, out string script)
        {
            return TryResolveSpecScript(specProperty, null, out script);
        }

        internal static bool TryResolveSpecScript(
            WzSubProperty specProperty,
            WzSubProperty specExProperty,
            out string script)
        {
            script = null;
            if (!TryResolveSpecScripts(specProperty, specExProperty, out IReadOnlyList<string> scripts)
                || scripts.Count == 0)
            {
                return false;
            }

            script = scripts[0];
            return true;
        }

        internal static bool TryResolveSpecScripts(WzSubProperty specProperty, out IReadOnlyList<string> scripts)
        {
            return TryResolveSpecScripts(specProperty, null, out scripts);
        }

        internal static bool TryResolveSpecScripts(
            WzSubProperty specProperty,
            WzSubProperty specExProperty,
            out IReadOnlyList<string> scripts)
        {
            scripts = Array.Empty<string>();

            var mergedScripts = new List<string>();
            var seenScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IReadOnlyList<string> resolvedSpecScripts = QuestRuntimeManager.ParseScriptNames(specProperty?["script"]);
            for (int index = 0; index < resolvedSpecScripts.Count; index++)
            {
                string candidate = resolvedSpecScripts[index];
                if (!string.IsNullOrWhiteSpace(candidate) && seenScripts.Add(candidate))
                {
                    mergedScripts.Add(candidate);
                }
            }

            IReadOnlyList<string> resolvedSpecExScripts = QuestRuntimeManager.ParseScriptNames(specExProperty?["script"]);
            for (int index = 0; index < resolvedSpecExScripts.Count; index++)
            {
                string candidate = resolvedSpecExScripts[index];
                if (!string.IsNullOrWhiteSpace(candidate) && seenScripts.Add(candidate))
                {
                    mergedScripts.Add(candidate);
                }
            }

            if (mergedScripts.Count == 0)
            {
                return false;
            }

            scripts = mergedScripts;
            return true;
        }

        internal static bool TryResolveSpecScriptPublications(
            WzSubProperty specProperty,
            out IReadOnlyList<FieldObjectScriptPublication> publications)
        {
            return TryResolveSpecScriptPublications(specProperty, null, out publications);
        }

        internal static bool TryResolveSpecScriptPublications(
            WzSubProperty specProperty,
            WzSubProperty specExProperty,
            out IReadOnlyList<FieldObjectScriptPublication> publications)
        {
            publications = Array.Empty<FieldObjectScriptPublication>();

            var mergedPublications = new List<FieldObjectScriptPublication>();
            var seenPublicationKeys = new HashSet<string>(StringComparer.Ordinal);

            IReadOnlyList<FieldObjectScriptPublication> resolvedSpecPublications =
                FieldObjectScriptPublicationParser.Parse(specProperty?["script"]);
            AddSpecScriptPublications(resolvedSpecPublications, mergedPublications, seenPublicationKeys);

            IReadOnlyList<FieldObjectScriptPublication> resolvedSpecExPublications =
                FieldObjectScriptPublicationParser.Parse(specExProperty?["script"]);
            AddSpecScriptPublications(resolvedSpecExPublications, mergedPublications, seenPublicationKeys);

            if (mergedPublications.Count == 0)
            {
                return false;
            }

            publications = mergedPublications;
            return true;
        }

        private static void AddSpecScriptPublications(
            IReadOnlyList<FieldObjectScriptPublication> source,
            ICollection<FieldObjectScriptPublication> destination,
            ISet<string> seenKeys)
        {
            if (source == null || source.Count == 0 || destination == null || seenKeys == null)
            {
                return;
            }

            for (int index = 0; index < source.Count; index++)
            {
                FieldObjectScriptPublication publication = source[index];
                if (publication == null || string.IsNullOrWhiteSpace(publication.ScriptName))
                {
                    continue;
                }

                string key = string.Concat(
                    publication.ScriptName.Trim(),
                    "|",
                    publication.DelayMs.ToString(CultureInfo.InvariantCulture));
                if (!seenKeys.Add(key))
                {
                    continue;
                }

                destination.Add(publication);
            }
        }

        public static bool TryResolveSpecScriptForTests(WzSubProperty specProperty, out string script)
        {
            return TryResolveSpecScript(specProperty, out script);
        }

        public static bool TryResolveSpecScriptForTests(
            WzSubProperty specProperty,
            WzSubProperty specExProperty,
            out string script)
        {
            return TryResolveSpecScript(specProperty, specExProperty, out script);
        }

        public static bool TryResolveSpecScriptsForTests(WzSubProperty specProperty, out IReadOnlyList<string> scripts)
        {
            return TryResolveSpecScripts(specProperty, out scripts);
        }

        public static bool TryResolveSpecScriptsForTests(
            WzSubProperty specProperty,
            WzSubProperty specExProperty,
            out IReadOnlyList<string> scripts)
        {
            return TryResolveSpecScripts(specProperty, specExProperty, out scripts);
        }

        internal static bool TryResolveSpecScriptPublicationsForTests(
            WzSubProperty specProperty,
            out IReadOnlyList<FieldObjectScriptPublication> publications)
        {
            return TryResolveSpecScriptPublications(specProperty, out publications);
        }

        internal static bool TryResolveSpecScriptPublicationsForTests(
            WzSubProperty specProperty,
            WzSubProperty specExProperty,
            out IReadOnlyList<FieldObjectScriptPublication> publications)
        {
            return TryResolveSpecScriptPublications(specProperty, specExProperty, out publications);
        }

        public static bool IsWeddingInvitationItemForTests(
            int itemId,
            InventoryType inventoryType,
            string itemName,
            string itemDescription)
        {
            return IsWeddingInvitationItem(itemId, inventoryType, itemName, itemDescription);
        }

        internal static bool HasAuthoredNpcInteraction(WzSubProperty itemProperty)
        {
            return TryResolveNpcReference(itemProperty, out _)
                   || TryResolveSpecScripts(
                       itemProperty?["spec"] as WzSubProperty,
                       itemProperty?["specEx"] as WzSubProperty,
                       out _);
        }

        internal static bool IsWeddingInvitationItem(
            int itemId,
            InventoryType inventoryType,
            string itemName,
            string itemDescription)
        {
            if (itemId <= 0 || inventoryType is not (InventoryType.CASH or InventoryType.ETC))
            {
                return false;
            }

            if (itemId == WeddingInvitationCashCardItemId
                || itemId == WeddingInvitationTicketItemId
                || (itemId >= WeddingInvitationTicketStartItemId && itemId <= WeddingInvitationTicketEndItemId))
            {
                return true;
            }

            int itemGroup = itemId / 1000;
            if (itemGroup >= WeddingInvitationEtcCardStartGroup
                && itemGroup <= WeddingInvitationEtcCardEndGroup)
            {
                return true;
            }

            if (itemId is 4031377 or 4031395 or 4031406 or 4031407)
            {
                return true;
            }

            string resolvedItemName = !string.IsNullOrWhiteSpace(itemName)
                ? itemName
                : (TryResolveItemName(itemId, out string metadataName) ? metadataName : null);
            string resolvedItemDescription = !string.IsNullOrWhiteSpace(itemDescription)
                ? itemDescription
                : (TryResolveItemDescription(itemId, out string metadataDescription) ? metadataDescription : null);

            return ContainsWeddingInvitationPhrase(resolvedItemName)
                   || ContainsWeddingInvitationPhrase(resolvedItemDescription);
        }

        private static bool ContainsWeddingInvitationPhrase(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !ContainsPhrase(text, WeddingKeyword))
            {
                return false;
            }

            return ContainsPhrase(text, InvitationKeyword)
                   || ContainsPhrase(text, InvitationMisspellingKeyword);
        }

        internal static bool ShouldAutoRunOnPickupInteraction(WzSubProperty itemProperty)
        {
            if (itemProperty == null)
            {
                return false;
            }

            return IsRunOnPickup(
                       itemProperty["spec"] as WzSubProperty,
                       itemProperty["specEx"] as WzSubProperty)
                   && HasAuthoredNpcInteraction(itemProperty);
        }

        private static bool ContainsPhrase(string text, string phrase)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ShouldAutoRunOnPickupInteractionForTests(WzSubProperty itemProperty)
        {
            return ShouldAutoRunOnPickupInteraction(itemProperty);
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

        private static List<string> BuildEffectLines(
            int itemId,
            WzSubProperty itemProperty,
            WzSubProperty infoProperty,
            WzSubProperty specProperty,
            WzSubProperty specExProperty)
        {
            List<string> effectLines = new();
            WzSubProperty effectSpecProperty = specProperty ?? specExProperty;
            AppendInfoEffectLines(effectLines, infoProperty);
            AppendInfoExperienceEffectLines(effectLines, infoProperty);
            AppendChairRecoveryEffectLines(effectLines, infoProperty);
            AppendPetFoodEffectLine(effectLines, itemId, specProperty);
            AppendBuffItemEffectLines(effectLines, itemProperty?["buff"] as WzSubProperty);
            AppendScriptedUseEffectLines(effectLines, specProperty, specExProperty);
            AppendPickupModifierEffectLines(effectLines, effectSpecProperty);
            AppendMobEffectLines(effectLines, itemProperty?["mob"] as WzSubProperty);
            AppendRewardEffectLines(effectLines, itemProperty?["reward"] as WzSubProperty);

            if (specProperty == null && specExProperty == null)
            {
                return effectLines;
            }

            AppendStatEffectLine(effectLines, "HP", TryGetPositiveInt(effectSpecProperty["hp"]), false);
            AppendStatEffectLine(effectLines, "HP", ResolveFirstPositiveInt(effectSpecProperty, "hpR", "hpRatio", "hpPer"), true);
            AppendStatEffectLine(effectLines, "MP", TryGetPositiveInt(effectSpecProperty["mp"]), false);
            AppendStatEffectLine(effectLines, "MP", ResolveFirstPositiveInt(effectSpecProperty, "mpR", "mpRatio", "mpPer"), true);
            AppendStatEffectLine(effectLines, "Weapon ATT", TryGetPositiveInt(effectSpecProperty["pad"]), false);
            AppendStatEffectLine(effectLines, "Magic ATT", TryGetPositiveInt(effectSpecProperty["mad"]), false);
            AppendStatEffectLine(effectLines, "Weapon DEF", TryGetPositiveInt(effectSpecProperty["pdd"]), false);
            AppendStatEffectLine(effectLines, "Magic DEF", TryGetPositiveInt(effectSpecProperty["mdd"]), false);
            AppendStatEffectLine(effectLines, "Accuracy", TryGetPositiveInt(effectSpecProperty["acc"]), false);
            AppendStatEffectLine(effectLines, "Avoidability", TryGetPositiveInt(effectSpecProperty["eva"]), false);
            AppendStatEffectLine(effectLines, "Speed", TryGetPositiveInt(effectSpecProperty["speed"]), false);
            AppendStatEffectLine(effectLines, "Jump", TryGetPositiveInt(effectSpecProperty["jump"]), false);
            AppendPrimaryFlatEffectLines(effectLines, effectSpecProperty);
            AppendPercentEffectLines(effectLines, effectSpecProperty);
            AppendIndependentFlatEffectLines(effectLines, effectSpecProperty);
            AppendIndependentPercentEffectLines(effectLines, effectSpecProperty);
            AppendCureEffectLine(effectLines, effectSpecProperty);
            AppendFatigueEffectLine(effectLines, effectSpecProperty["incFatigue"]);
            AppendMoveToEffectLine(effectLines, TryGetPositiveInt(effectSpecProperty["moveTo"]));
            AppendMorphEffectLine(effectLines, effectSpecProperty);
            AppendBoosterEffectLine(effectLines, effectSpecProperty["booster"], effectSpecProperty["indieBooster"]);
            AppendBerserkEffectLine(effectLines, effectSpecProperty["berserk"]);
            AppendThawEffectLine(effectLines, effectSpecProperty["thaw"]);
            AppendCrossContinentEffectLine(effectLines, effectSpecProperty["ignoreContinent"]);
            AppendReturnMapRecordEffectLine(effectLines, effectSpecProperty["returnMapQR"]);
            AppendRandomMoveInFieldSetEffectLine(effectLines, effectSpecProperty["randomMoveInFieldSet"]);
            AppendExperienceEffectLines(effectLines, effectSpecProperty);
            AppendEventPointEffectLine(effectLines, effectSpecProperty["eventPoint"]);
            AppendDeathmarkEffectLine(effectLines, itemId, effectSpecProperty);
            AppendMobEffectLines(effectLines, effectSpecProperty["mob"] as WzSubProperty);
            AppendSpecExMobSkillEffectLines(effectLines, specExProperty);
            AppendMobSkillOwnershipEffectLines(effectLines, specProperty, specExProperty);
            AppendPickupTriggerEffectLines(effectLines, specProperty, specExProperty);
            AppendScreenMessageEffectLines(effectLines, specProperty, specExProperty);

            int? durationMs = TryGetPositiveInt(effectSpecProperty["time"]);
            if (durationMs.HasValue)
            {
                effectLines.Add($"Duration: {FormatDuration(durationMs.Value)}");
            }

            if (GetIntValue(effectSpecProperty["party"]) == 1)
            {
                effectLines.Add("Applies to party members");
            }

            if (GetIntValue(specProperty?["otherParty"]) == 1
                || GetIntValue(specExProperty?["otherParty"]) == 1)
            {
                effectLines.Add("Applies to other party members");
            }

            return effectLines;
        }

        private static List<string> BuildMetadataLines(
            int itemId,
            WzSubProperty infoProperty,
            WzSubProperty specProperty,
            WzSubProperty specExProperty,
            bool isNotForSale,
            bool isQuestItem,
            bool isTradeBlocked,
            bool isOneOfAKind,
            DateTime? expirationDateUtc)
        {
            List<string> metadataLines = new();

            int? requiredLevel = ResolveRequiredLevel(infoProperty);
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

            AppendInfoMetadataLines(metadataLines, itemId, infoProperty, specProperty, specExProperty);
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

        private static void AppendInfoExperienceEffectLines(List<string> effectLines, WzSubProperty infoProperty)
        {
            int experienceAmount = GetIntOrStringValue(infoProperty?["exp"]);
            if (experienceAmount > 0)
            {
                effectLines.Add($"EXP +{experienceAmount.ToString("N0", CultureInfo.InvariantCulture)}");
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
                List<WzSubProperty> randomMorphEntries = GetNumericNamedChildren(morphRandom);
                if (randomMorphEntries.Count <= 0)
                {
                    effectLines.Add("Transforms into a random morph");
                    return;
                }

                List<string> previewEntries = new();
                for (int i = 0; i < randomMorphEntries.Count && previewEntries.Count < 3; i++)
                {
                    WzSubProperty entry = randomMorphEntries[i];
                    int randomMorphId = GetIntOrStringValue(entry["morph"]);
                    if (randomMorphId <= 0)
                    {
                        continue;
                    }

                    int probability = GetIntOrStringValue(entry["prop"]);
                    previewEntries.Add(probability > 0
                        ? $"#{randomMorphId.ToString(CultureInfo.InvariantCulture)} ({probability.ToString(CultureInfo.InvariantCulture)}%)"
                        : $"#{randomMorphId.ToString(CultureInfo.InvariantCulture)}");
                }

                if (previewEntries.Count <= 0)
                {
                    effectLines.Add("Transforms into a random morph");
                    return;
                }

                string suffix = randomMorphEntries.Count > previewEntries.Count
                    ? $", +{(randomMorphEntries.Count - previewEntries.Count).ToString(CultureInfo.InvariantCulture)} more"
                    : string.Empty;
                effectLines.Add($"Random Morphs: {string.Join(", ", previewEntries)}{suffix}");
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

        private static void AppendPetFoodEffectLine(List<string> effectLines, int itemId, WzSubProperty specProperty)
        {
            int fullnessIncrease = GetIntValue(specProperty?["inc"]);
            if (fullnessIncrease <= 0 || !IsPetFoodSpec(itemId, specProperty))
            {
                return;
            }

            effectLines.Add($"Pet Fullness {FormatSignedValue(fullnessIncrease)}");
        }

        private static void AppendScriptedUseEffectLines(
            List<string> effectLines,
            WzSubProperty specProperty,
            WzSubProperty specExProperty)
        {
            if (effectLines == null)
            {
                return;
            }

            var seenNpcIds = new HashSet<int>();
            bool addedPickupTriggerLine = false;

            AppendScriptedUseEffectLinesFromProperty(
                effectLines,
                specProperty,
                seenNpcIds,
                ref addedPickupTriggerLine);
            AppendScriptedUseEffectLinesFromProperty(
                effectLines,
                specExProperty,
                seenNpcIds,
                ref addedPickupTriggerLine);
        }

        private static void AppendScriptedUseEffectLinesFromProperty(
            ICollection<string> effectLines,
            WzSubProperty specProperty,
            ISet<int> seenNpcIds,
            ref bool addedPickupTriggerLine)
        {
            if (effectLines == null || specProperty == null)
            {
                return;
            }

            int npcId = GetIntOrStringValue(specProperty["npc"]);
            if (npcId > 0 && (seenNpcIds == null || seenNpcIds.Add(npcId)))
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

            if (!addedPickupTriggerLine && GetIntValue(specProperty["runOnPickup"]) == 1)
            {
                effectLines.Add("Runs immediately on pickup");
                addedPickupTriggerLine = true;
            }
        }

        private static void AppendPickupTriggerEffectLines(
            List<string> effectLines,
            WzSubProperty specProperty,
            WzSubProperty specExProperty)
        {
            bool consumedOnPickup = GetIntValue(specProperty?["consumeOnPickup"]) == 1
                                    || GetIntValue(specExProperty?["consumeOnPickup"]) == 1;
            if (consumedOnPickup)
            {
                effectLines.Add("Consumed on pickup");
            }

            if (GetIntValue(specProperty?["onlyPickup"]) == 1
                || GetIntValue(specExProperty?["onlyPickup"]) == 1)
            {
                effectLines.Add("Can only be used when picked up");
            }

            if (GetIntValue(specProperty?["notPickupByPet"]) == 1
                || GetIntValue(specExProperty?["notPickupByPet"]) == 1)
            {
                effectLines.Add("Cannot be picked up by pets");
            }
        }

        private static void AppendScreenMessageEffectLines(
            List<string> effectLines,
            WzSubProperty specProperty,
            WzSubProperty specExProperty)
        {
            string specMessage = NormalizeTooltipText((specProperty?["screenMsg"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(specMessage))
            {
                effectLines.Add($"Screen Message: {specMessage}");
            }

            string specExMessage = NormalizeTooltipText((specExProperty?["screenMsg"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(specExMessage)
                && !string.Equals(specMessage, specExMessage, StringComparison.Ordinal))
            {
                effectLines.Add($"Screen Message: {specExMessage}");
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

        private static void AppendEventPointEffectLine(List<string> effectLines, WzImageProperty property)
        {
            int eventPoint = GetIntOrStringValue(property);
            if (eventPoint <= 0)
            {
                return;
            }

            effectLines.Add($"Cookie House points +{eventPoint.ToString("N0", CultureInfo.InvariantCulture)}");
        }

        private static void AppendDeathmarkEffectLine(List<string> effectLines, int itemId, WzSubProperty specProperty)
        {
            if (GetIntValue(specProperty?["deathmark"]) != 1)
            {
                return;
            }

            string description = TryResolveItemDescription(itemId, out string resolvedDescription)
                ? resolvedDescription?.Trim()
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(description)
                && description.IndexOf("death curse", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                effectLines.Add("Removes the Death Curse");
                return;
            }

            effectLines.Add("Death Mark effect");
        }

        private static void AppendInfoMetadataLines(
            List<string> metadataLines,
            int itemId,
            WzSubProperty infoProperty,
            WzSubProperty specProperty = null,
            WzSubProperty specExProperty = null)
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
            AppendTargetMobMetadataLines(metadataLines, infoProperty, specProperty);
            AppendInfoNpcMetadataLines(metadataLines, infoProperty, specProperty);
            AppendStateChangeItemMetadataLines(metadataLines, infoProperty);
            AppendItemPeriodMetadataLines(metadataLines, infoProperty);
            AppendCashItemExtensionMetadataLines(metadataLines, infoProperty);
            AppendCreateMetadataLines(metadataLines, infoProperty);
            AppendReplaceMetadataLines(metadataLines, infoProperty);
            AppendRecoveryRateMetadataLines(metadataLines, infoProperty);
            AppendScrollUpgradeMetadataLines(metadataLines, infoProperty);
            AppendEquipLevelBypassMetadataLines(metadataLines, infoProperty);
            AppendRandomChairEffectMetadataLines(metadataLines, infoProperty);
            AppendAdditionalExperienceMetadataLines(metadataLines, infoProperty);
            AppendGrowthItemMetadataLines(metadataLines, infoProperty);
            AppendAuthoredLevelRangeMetadataLines(metadataLines, infoProperty);
            AppendLevelBandMetadataLines(metadataLines, infoProperty);
            AppendBundleLimitMetadataLines(metadataLines, infoProperty);
            AppendLevelUpWarningMetadataLines(metadataLines, infoProperty);
            AppendRecipeMetadataLines(metadataLines, specProperty);
            AppendConditionalMapMetadataLines(metadataLines, specProperty);
            AppendRepeatEffectMetadataLines(metadataLines, specProperty, specExProperty);
            AppendItemBagMetadataLines(metadataLines, specProperty);
            AppendSpecExMobSkillMetadataLines(metadataLines, specExProperty);
            AppendCashAvailabilityMetadataLines(metadataLines, infoProperty);
            AppendCashRateScheduleMetadataLines(metadataLines, infoProperty);
            AppendPartyScaleMetadataLines(metadataLines, infoProperty);
            AppendAuthoredAssetPathMetadataLines(metadataLines, infoProperty);
            AppendWeatherPresentationMetadataLines(metadataLines, infoProperty);
            AppendAdditionalInfoFlagsMetadataLines(metadataLines, infoProperty, specProperty);
        }

        private static void AppendItemBagMetadataLines(List<string> metadataLines, WzSubProperty specProperty)
        {
            if (specProperty == null)
            {
                return;
            }

            int slotCount = GetIntOrStringValue(specProperty["slotCount"]);
            int slotsPerLine = GetIntOrStringValue(specProperty["slotPerLine"]);
            int bagType = GetIntOrStringValue(specProperty["type"]);
            if (slotCount <= 0 && slotsPerLine <= 0 && bagType <= 0)
            {
                return;
            }

            if (slotCount > 0)
            {
                metadataLines.Add($"Bag Slots: {slotCount.ToString(CultureInfo.InvariantCulture)}");
            }

            if (slotsPerLine > 0)
            {
                metadataLines.Add($"Bag Layout: {slotsPerLine.ToString(CultureInfo.InvariantCulture)} per row");
            }

            string bagTypeLabel = ResolveItemBagTypeLabel(bagType);
            if (!string.IsNullOrWhiteSpace(bagTypeLabel))
            {
                metadataLines.Add($"Bag Type: {bagTypeLabel}");
            }
        }

        private static string ResolveItemBagTypeLabel(int bagType)
        {
            return bagType switch
            {
                1 => "Herb",
                2 => "Mineral",
                4 => "Party Quest",
                5 => "Coin",
                > 0 => $"Type {bagType.ToString(CultureInfo.InvariantCulture)}",
                _ => string.Empty
            };
        }

        private static void AppendSpecExMobSkillEffectLines(List<string> effectLines, WzSubProperty specExProperty)
        {
            if (specExProperty?.WzProperties == null)
            {
                return;
            }

            HashSet<string> seenLines = new(StringComparer.Ordinal);
            List<WzSubProperty> entries = GetNumericNamedChildren(specExProperty);
            for (int i = 0; i < entries.Count; i++)
            {
                WzSubProperty entry = entries[i];
                int mobSkillId = GetIntOrStringValue(entry["mobSkill"]);
                if (mobSkillId <= 0)
                {
                    continue;
                }

                int level = Math.Max(1, GetIntOrStringValue(entry["level"]));
                string line = $"Applies {ResolveMobSkillTooltipLabel(mobSkillId)} Lv. {level.ToString(CultureInfo.InvariantCulture)}";
                if (seenLines.Add(line))
                {
                    effectLines.Add(line);
                }
            }
        }

        private static void AppendMobSkillOwnershipEffectLines(
            List<string> effectLines,
            WzSubProperty specProperty,
            WzSubProperty specExProperty)
        {
            bool affectsAllies = GetIntValue(specProperty?["effectedOnAlly"]) == 1
                                  || GetIntValue(specExProperty?["effectedOnAlly"]) == 1;
            bool affectsEnemies = GetIntValue(specProperty?["effectedOnEnemy"]) == 1
                                   || GetIntValue(specExProperty?["effectedOnEnemy"]) == 1;
            if (!affectsAllies && !affectsEnemies)
            {
                return;
            }

            if (affectsAllies)
            {
                effectLines.Add("Affects allies");
            }

            if (affectsEnemies)
            {
                effectLines.Add("Affects enemies");
            }
        }

        private static void AppendSpecExMobSkillMetadataLines(List<string> metadataLines, WzSubProperty specExProperty)
        {
            if (specExProperty?.WzProperties == null)
            {
                return;
            }

            HashSet<string> seenLines = new(StringComparer.Ordinal);
            List<WzSubProperty> entries = GetNumericNamedChildren(specExProperty);
            for (int i = 0; i < entries.Count; i++)
            {
                WzSubProperty entry = entries[i];
                int target = GetIntOrStringValue(entry["target"]);
                if (target <= 0)
                {
                    continue;
                }

                string line = $"Mob Skill Target: {FormatMobSkillTargetLabel(target)}";
                if (seenLines.Add(line))
                {
                    metadataLines.Add(line);
                }
            }
        }

        private static void AppendStateChangeItemMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int stateChangeItemId = GetIntOrStringValue(infoProperty?["stateChangeItem"]);
            if (stateChangeItemId > 0)
            {
                string stateChangeItemLabel = ResolveTooltipItemLabel(stateChangeItemId);
                metadataLines.Add(HasWeatherPresentationInfo(infoProperty)
                    ? $"Weather Trigger Item: {stateChangeItemLabel}"
                    : $"Linked Cash Effect: {stateChangeItemLabel}");
            }
        }

        private static bool HasWeatherPresentationInfo(WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return false;
            }

            string effectPath = NormalizeTooltipText((infoProperty["path"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(effectPath)
                && effectPath.StartsWith("Map/MapHelper.img/weather/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return infoProperty["type"] != null
                   || infoProperty["uiType"] != null
                   || infoProperty["direction"] != null
                   || infoProperty["floatType"] != null
                   || infoProperty["speed"] != null
                   || infoProperty["rotateSpeed"] != null
                   || infoProperty["isBgmOrEffect"] != null
                   || infoProperty["repeat"] != null
                   || TryGetWeatherNoCancelProperty(infoProperty) != null;
        }

        private static void AppendInfoNpcMetadataLines(
            List<string> metadataLines,
            WzSubProperty infoProperty,
            WzSubProperty specProperty)
        {
            int npcId = GetIntOrStringValue(infoProperty?["npc"]);
            if (npcId <= 0 || GetIntOrStringValue(specProperty?["npc"]) == npcId)
            {
                return;
            }

            string npcName = ResolveNpcName(npcId);
            metadataLines.Add(string.IsNullOrWhiteSpace(npcName)
                ? $"Linked NPC: #{npcId.ToString(CultureInfo.InvariantCulture)}"
                : $"Linked NPC: {npcName}");
        }

        private static void AppendItemPeriodMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int itemPeriodHours = GetIntOrStringValue(infoProperty?["period"]);
            if (itemPeriodHours > 0)
            {
                metadataLines.Add($"Item expires after {FormatHourDuration(itemPeriodHours)}");
            }
        }

        private static void AppendCashItemExtensionMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int addTimeSeconds = GetIntOrStringValue(infoProperty["addTime"]);
            if (addTimeSeconds > 0)
            {
                metadataLines.Add($"Adds Duration: {FormatSecondDuration(addTimeSeconds)}");
            }

            int addDays = GetIntOrStringValue(infoProperty["addDay"]);
            if (addDays > 0)
            {
                metadataLines.Add($"Adds Duration: {FormatDayCount(addDays)}");
            }

            int maxDays = GetIntOrStringValue(infoProperty["maxDays"]);
            if (maxDays > 0)
            {
                metadataLines.Add($"Maximum Duration: {FormatDayCount(maxDays)}");
            }

            int minusLevel = GetIntOrStringValue(infoProperty["minusLevel"]);
            if (minusLevel > 0)
            {
                metadataLines.Add($"Required Level Reduction: {minusLevel.ToString(CultureInfo.InvariantCulture)} level{(minusLevel == 1 ? string.Empty : "s")}");
            }

            IReadOnlyList<int> selectedSlots = GetNumericNamedIntRows(infoProperty["selectedSlot"] as WzSubProperty);
            if (selectedSlots.Count > 0)
            {
                metadataLines.Add($"Eligible Equip Slots: {FormatEligibleEquipSlotLabels(selectedSlots)}");
            }

            int slotIndex = GetIntOrStringValue(infoProperty["slotIndex"]);
            if (slotIndex >= 0 && infoProperty["slotIndex"] != null)
            {
                metadataLines.Add($"Extension Slot: {FormatExtensionSlotLabel(slotIndex)}");
            }
        }

        private static void AppendRepeatEffectMetadataLines(
            List<string> metadataLines,
            WzSubProperty specProperty,
            WzSubProperty specExProperty)
        {
            if (GetIntValue(specProperty?["repeatEffect"]) == 1
                || GetIntValue(specExProperty?["repeatEffect"]) == 1)
            {
                metadataLines.Add("Repeats effect while active");
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

        private static void AppendTargetMobMetadataLines(
            List<string> metadataLines,
            WzSubProperty infoProperty,
            WzSubProperty specProperty)
        {
            if (TryResolveTargetMobIdForTooltip(infoProperty, specProperty, out int targetMobId))
            {
                metadataLines.Add($"Target Mob: {ResolveMobTooltipLabel(targetMobId)}");
            }

            int targetMobHpPercent = GetIntOrStringValue(infoProperty?["mobHP"]);
            if (targetMobHpPercent > 0)
            {
                metadataLines.Add($"Target Mob HP: {targetMobHpPercent.ToString(CultureInfo.InvariantCulture)}% or below");
            }

            int targetMobHpValue = GetIntOrStringValue(specProperty?["mobHp"]);
            if (targetMobHpValue > 0)
            {
                metadataLines.Add($"Target Mob HP: {targetMobHpValue.ToString("N0", CultureInfo.InvariantCulture)} or below");
            }

            int attackIndex = GetIntOrStringValue(specProperty?["attackIndex"]);
            if (attackIndex > 0)
            {
                metadataLines.Add($"Attack Index: {attackIndex.ToString(CultureInfo.InvariantCulture)}");
            }

            AppendCaptureAreaMetadataLines(metadataLines, infoProperty);
            AppendCaptureChanceMetadataLines(metadataLines, infoProperty);

            int useDelayMs = GetIntOrStringValue(infoProperty?["useDelay"]);
            if (useDelayMs > 0)
            {
                metadataLines.Add($"Use Delay: {FormatDuration(useDelayMs)}");
            }

            string delayMessage = NormalizeTooltipText((infoProperty?["delayMsg"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(delayMessage))
            {
                metadataLines.Add($"Delay Notice: {delayMessage}");
            }

            string noMobMessage = NormalizeTooltipText((infoProperty?["nomobMsg"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(noMobMessage))
            {
                metadataLines.Add($"No Target Notice: {noMobMessage}");
            }
        }

        private static void AppendCaptureAreaMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (!TryGetSignedInt(infoProperty?["left"], out int left)
                || !TryGetSignedInt(infoProperty?["right"], out int right)
                || !TryGetSignedInt(infoProperty?["top"], out int top)
                || !TryGetSignedInt(infoProperty?["bottom"], out int bottom))
            {
                return;
            }

            metadataLines.Add(
                $"Capture Area: X {left.ToString(CultureInfo.InvariantCulture)} to {right.ToString(CultureInfo.InvariantCulture)}, Y {top.ToString(CultureInfo.InvariantCulture)} to {bottom.ToString(CultureInfo.InvariantCulture)}");
        }

        internal static bool TryResolveTargetMobIdForTooltip(
            WzSubProperty infoProperty,
            WzSubProperty specProperty,
            out int targetMobId)
        {
            targetMobId = GetIntOrStringValue(specProperty?["mobID"]);
            if (targetMobId > 0)
            {
                return true;
            }

            targetMobId = GetIntOrStringValue(specProperty?["attackMobID"]);
            if (targetMobId > 0)
            {
                return true;
            }

            int infoMobId = GetIntOrStringValue(infoProperty?["mob"]);
            if (infoMobId <= 0 || !HasInfoOwnedTargetedMobTooltipMetadata(infoProperty, specProperty))
            {
                return false;
            }

            targetMobId = infoMobId;
            return true;
        }

        internal static bool HasInfoOwnedTargetedMobTooltipMetadata(
            WzSubProperty infoProperty,
            WzSubProperty specProperty)
        {
            if (GetIntOrStringValue(specProperty?["mobID"]) > 0
                || GetIntOrStringValue(specProperty?["attackMobID"]) > 0
                || GetIntOrStringValue(specProperty?["mobHp"]) > 0
                || GetIntOrStringValue(infoProperty?["mobHP"]) > 0
                || GetIntOrStringValue(infoProperty?["useDelay"]) > 0
                || GetIntOrStringValue(infoProperty?["bridleMsgType"]) > 0)
            {
                return true;
            }

            string delayMessage = NormalizeTooltipText((infoProperty?["delayMsg"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(delayMessage))
            {
                return true;
            }

            string noMobMessage = NormalizeTooltipText((infoProperty?["nomobMsg"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(noMobMessage))
            {
                return true;
            }

            return TryGetSignedInt(infoProperty?["left"], out _)
                   && TryGetSignedInt(infoProperty?["right"], out _)
                   && TryGetSignedInt(infoProperty?["top"], out _)
                   && TryGetSignedInt(infoProperty?["bottom"], out _);
        }

        private static void AppendCaptureChanceMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int captureChancePercent = GetIntOrStringValue(infoProperty?["bridleProp"]);
            if (captureChancePercent > 0)
            {
                metadataLines.Add($"Capture Chance: {captureChancePercent.ToString(CultureInfo.InvariantCulture)}%");
            }

            double? captureChanceGrowth = TryGetPositiveDouble(infoProperty?["bridlePropChg"]);
            if (captureChanceGrowth.HasValue
                && Math.Abs(captureChanceGrowth.Value - 1d) > 0.0001d)
            {
                metadataLines.Add($"Capture Chance Growth: x{captureChanceGrowth.Value.ToString("0.##", CultureInfo.InvariantCulture)}");
            }

            int messageType = GetIntOrStringValue(infoProperty?["bridleMsgType"]);
            if (messageType > 0)
            {
                metadataLines.Add($"Capture Message Type: {messageType.ToString(CultureInfo.InvariantCulture)}");
            }
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

        private static void AppendScrollUpgradeMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            if (GetIntValue(infoProperty["randstat"]) == 1)
            {
                metadataLines.Add("Randomizes applied stat values");
            }

            if (GetIntValue(infoProperty["incRandVol"]) == 1)
            {
                metadataLines.Add("Uses stronger random stat variance");
            }

            if (GetIntValue(infoProperty["blackUpgrade"]) == 1)
            {
                metadataLines.Add("Lets the previous upgrade result be kept");
            }

            if (GetIntValue(infoProperty["recover"]) == 1)
            {
                metadataLines.Add("Restores failed upgrade slots");
            }

            if (GetIntValue(infoProperty["reset"]) == 1)
            {
                metadataLines.Add("Resets upgrade state");
            }

            int requiredCurrentUpgradeCount = GetIntOrStringValue(infoProperty["reqCUC"]);
            if (requiredCurrentUpgradeCount > 0)
            {
                metadataLines.Add($"Required Upgrade Count: {requiredCurrentUpgradeCount.ToString(CultureInfo.InvariantCulture)}");
            }

            int upgradeSlotCount = GetIntOrStringValue(infoProperty["tuc"]);
            if (upgradeSlotCount > 0)
            {
                metadataLines.Add($"Upgrade Slots Added: {upgradeSlotCount.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private static void AppendEquipLevelBypassMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            int levelBypass = GetIntOrStringValue(infoProperty?["incLEV"]);
            if (levelBypass <= 0)
            {
                return;
            }

            metadataLines.Add($"Equip Level Bypass: {levelBypass.ToString(CultureInfo.InvariantCulture)} level{(levelBypass == 1 ? string.Empty : "s")}");
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

        private static void AppendGrowthItemMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int questId = GetIntOrStringValue(infoProperty["questId"]);
            if (questId <= 0)
            {
                questId = GetIntOrStringValue(infoProperty["qid"]);
            }

            if (questId > 0)
            {
                metadataLines.Add($"Related Quest: {ResolveQuestTooltipLabel(questId)}");
            }

            int growthGrade = GetIntOrStringValue(infoProperty["grade"]);
            if (growthGrade > 0)
            {
                metadataLines.Add($"Growth Grade: {growthGrade.ToString(CultureInfo.InvariantCulture)}");
            }

            string growthName = NormalizeTooltipText((infoProperty["name"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(growthName))
            {
                metadataLines.Add($"Growth Name: {growthName}");
            }

            AppendAuthoredMessageMetadataLines(metadataLines, infoProperty["message"] as WzSubProperty);
        }

        private static void AppendAuthoredMessageMetadataLines(List<string> metadataLines, WzSubProperty messageProperty)
        {
            if (messageProperty?.WzProperties == null)
            {
                return;
            }

            const int previewLineLimit = 3;
            int appended = 0;
            List<(int Index, string Text)> authoredRows = GetNumericNamedStringRows(messageProperty);
            foreach ((_, string text) in authoredRows)
            {
                if (appended < previewLineLimit)
                {
                    metadataLines.Add($"Message: {text}");
                    appended++;
                }
            }

            if (authoredRows.Count > appended)
            {
                metadataLines.Add($"Message: ... and {(authoredRows.Count - appended).ToString(CultureInfo.InvariantCulture)} more");
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

                AppendCantAccountShareJobFlagsMetadataLines(metadataLines, cantAccountSharableProperty["job"] as WzSubProperty);
            }
        }

        private static void AppendCantAccountShareJobFlagsMetadataLines(
            ICollection<string> metadataLines,
            WzSubProperty jobFlagsProperty)
        {
            if (metadataLines == null || jobFlagsProperty?.WzProperties == null)
            {
                return;
            }

            List<(int JobId, int Flag)> resolvedFlags = new();
            foreach (WzImageProperty child in jobFlagsProperty.WzProperties)
            {
                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int jobId))
                {
                    continue;
                }

                int flagValue = GetIntOrStringValue(child);
                if (flagValue <= 0)
                {
                    continue;
                }

                resolvedFlags.Add((jobId, flagValue));
            }

            if (resolvedFlags.Count <= 0)
            {
                return;
            }

            resolvedFlags.Sort((left, right) => left.JobId.CompareTo(right.JobId));

            const int previewLimit = 6;
            List<string> previewEntries = new();
            int previewCount = Math.Min(previewLimit, resolvedFlags.Count);
            for (int i = 0; i < previewCount; i++)
            {
                (int jobId, int flag) = resolvedFlags[i];
                previewEntries.Add($"{jobId.ToString(CultureInfo.InvariantCulture)}={flag.ToString(CultureInfo.InvariantCulture)}");
            }

            string suffix = resolvedFlags.Count > previewCount
                ? $" (+{(resolvedFlags.Count - previewCount).ToString(CultureInfo.InvariantCulture)} more)"
                : string.Empty;
            metadataLines.Add($"Account-share class flags: {string.Join(", ", previewEntries)}{suffix}");
        }

        private static void AppendAuthoredLevelRangeMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int minimumLevel = GetIntOrStringValue(infoProperty["lvMin"]);
            int maximumLevel = GetIntOrStringValue(infoProperty["lvMax"]);
            if (minimumLevel > 0 && maximumLevel > 0)
            {
                metadataLines.Add($"Level Range: Lv. {minimumLevel.ToString(CultureInfo.InvariantCulture)}-{maximumLevel.ToString(CultureInfo.InvariantCulture)}");
                return;
            }

            if (minimumLevel > 0)
            {
                metadataLines.Add($"Minimum Level: {minimumLevel.ToString(CultureInfo.InvariantCulture)}");
                return;
            }

            if (maximumLevel > 0)
            {
                metadataLines.Add($"Maximum Level: {maximumLevel.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        private static void AppendLevelBandMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int optimumLevel = GetIntOrStringValue(infoProperty["lvOptimum"]);
            int levelRange = GetIntOrStringValue(infoProperty["lvRange"]);
            if (optimumLevel > 0 && levelRange > 0)
            {
                int maximumLevel = optimumLevel + Math.Max(0, levelRange - 1);
                metadataLines.Add(
                    $"Effective Level Range: Lv. {optimumLevel.ToString(CultureInfo.InvariantCulture)}-{maximumLevel.ToString(CultureInfo.InvariantCulture)}");
                return;
            }

            if (optimumLevel > 0)
            {
                metadataLines.Add($"Effective from Lv. {optimumLevel.ToString(CultureInfo.InvariantCulture)}");
                return;
            }

            if (levelRange > 0)
            {
                metadataLines.Add($"Effective for {levelRange.ToString(CultureInfo.InvariantCulture)} level{(levelRange == 1 ? string.Empty : "s")}");
            }
        }

        private static void AppendBundleLimitMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int slotMax = GetIntOrStringValue(infoProperty["slotMax"]);
            if (slotMax > 0)
            {
                metadataLines.Add(FormatStackLimitMetadataLine(slotMax));
            }

            int maxOwned = GetIntOrStringValue(infoProperty["max"]);
            if (maxOwned > 0)
            {
                metadataLines.Add($"Max Owned: {maxOwned.ToString("N0", CultureInfo.InvariantCulture)}");
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

            int requiredSkillId = GetIntOrStringValue(specProperty["reqSkill"]);
            if (requiredSkillId > 0)
            {
                metadataLines.Add($"Required Skill: {ResolveSkillTooltipLabel(requiredSkillId)}");
            }

            int requiredSkillProficiency = GetIntOrStringValue(specProperty["reqSkillProficiency"]);
            if (requiredSkillProficiency > 0)
            {
                metadataLines.Add($"Required Skill Proficiency: {requiredSkillProficiency.ToString(CultureInfo.InvariantCulture)}");
            }

            int useLevel = GetIntOrStringValue(specProperty["useLevel"]);
            if (useLevel > 0)
            {
                metadataLines.Add($"Use Level: {useLevel.ToString(CultureInfo.InvariantCulture)}");
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

        private static void AppendConditionalMapMetadataLines(List<string> metadataLines, WzSubProperty specProperty)
        {
            if (specProperty?["con"] is not WzSubProperty conditionProperty
                || conditionProperty.WzProperties == null)
            {
                return;
            }

            HashSet<string> seenLines = new(StringComparer.Ordinal);
            foreach (WzImageProperty child in conditionProperty.WzProperties)
            {
                if (child is not WzSubProperty entryProperty)
                {
                    continue;
                }

                int startMapId = GetIntOrStringValue(entryProperty["sMap"]);
                int endMapId = GetIntOrStringValue(entryProperty["eMap"]);
                if (startMapId <= 0 || endMapId <= 0)
                {
                    continue;
                }

                string line = $"Usable In: {FormatMapRangeLabel(startMapId, endMapId)}";
                if (seenLines.Add(line))
                {
                    metadataLines.Add(line);
                }
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

            if (GetIntValue(infoProperty["add"]) == 1)
            {
                metadataLines.Add("Adds pet utility skill");
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

            int limitSeconds = GetIntOrStringValue(infoProperty["limitSec"]);
            if (limitSeconds > 0)
            {
                metadataLines.Add($"Time limit: {FormatSecondDuration(limitSeconds)}");
            }

            int limitCount = GetIntOrStringValue(infoProperty["limitCount"]);
            if (limitCount > 0)
            {
                metadataLines.Add($"Use limit: {limitCount.ToString(CultureInfo.InvariantCulture)} time{(limitCount == 1 ? string.Empty : "s")}");
            }
        }

        private static void AppendCashRateScheduleMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int rate = GetIntOrStringValue(infoProperty["rate"]);
            if (rate > 0)
            {
                metadataLines.Add($"Cash Rate: {rate.ToString(CultureInfo.InvariantCulture)}x");
            }

            AppendAuthoredScheduleMetadataLines(metadataLines, infoProperty["time"] as WzSubProperty);
        }

        private static void AppendPartyScaleMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            List<(int Index, int Value)> partyRows = GetNumericNamedIntValueRows(infoProperty?["party"] as WzSubProperty);
            if (partyRows.Count <= 0)
            {
                return;
            }

            const int previewLimit = 6;
            int previewCount = Math.Min(previewLimit, partyRows.Count);
            List<string> previewEntries = new(previewCount);
            for (int i = 0; i < previewCount; i++)
            {
                (int index, int value) = partyRows[i];
                previewEntries.Add(
                    $"{index.ToString(CultureInfo.InvariantCulture)}={value.ToString(CultureInfo.InvariantCulture)}");
            }

            string suffix = partyRows.Count > previewCount
                ? $" (+{(partyRows.Count - previewCount).ToString(CultureInfo.InvariantCulture)} more)"
                : string.Empty;
            metadataLines.Add($"Party scale rows: {string.Join(", ", previewEntries)}{suffix}");
        }

        private static void AppendAuthoredAssetPathMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            string effectPath = NormalizeTooltipText((infoProperty["path"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(effectPath))
            {
                metadataLines.Add($"Field Effect Path: {effectPath}");
            }

            string bgmPath = NormalizeTooltipText((infoProperty["bgmPath"] as WzStringProperty)?.Value);
            if (!string.IsNullOrWhiteSpace(bgmPath))
            {
                metadataLines.Add($"BGM Path: {bgmPath}");
            }
        }

        private static void AppendWeatherPresentationMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            if (infoProperty["type"] != null)
            {
                int weatherType = GetIntOrStringValue(infoProperty["type"]);
                metadataLines.Add($"Weather Type: {weatherType.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["uiType"] != null)
            {
                int uiType = GetIntOrStringValue(infoProperty["uiType"]);
                metadataLines.Add($"Weather UI Type: {uiType.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["direction"] != null)
            {
                int direction = GetIntOrStringValue(infoProperty["direction"]);
                metadataLines.Add($"Weather Direction: {direction.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["floatType"] != null)
            {
                int floatType = GetIntOrStringValue(infoProperty["floatType"]);
                metadataLines.Add($"Weather Float Type: {floatType.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["speed"] != null)
            {
                int speed = GetIntOrStringValue(infoProperty["speed"]);
                metadataLines.Add($"Weather Speed: {speed.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["rotateSpeed"] != null)
            {
                int rotateSpeed = GetIntOrStringValue(infoProperty["rotateSpeed"]);
                metadataLines.Add($"Weather Rotation Speed: {rotateSpeed.ToString(CultureInfo.InvariantCulture)}");
            }

            if (infoProperty["isBgmOrEffect"] != null)
            {
                metadataLines.Add(GetIntValue(infoProperty["isBgmOrEffect"]) == 1
                    ? "Weather BGM/Effect broadcast"
                    : "Weather local visual effect");
            }

            if (infoProperty["repeat"] != null)
            {
                metadataLines.Add(GetIntValue(infoProperty["repeat"]) == 1
                    ? "Weather effect repeats"
                    : "Weather effect does not repeat");
            }

            WzImageProperty noCancelProperty = TryGetWeatherNoCancelProperty(infoProperty);
            if (noCancelProperty != null)
            {
                metadataLines.Add(GetIntValue(noCancelProperty) == 1
                    ? "Weather effect cannot be canceled manually"
                    : "Weather effect can be canceled manually");
            }
        }

        private static WzImageProperty TryGetWeatherNoCancelProperty(WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return null;
            }

            return infoProperty["NoCancel"] ?? infoProperty["noCancel"];
        }

        private static void AppendAuthoredScheduleMetadataLines(List<string> metadataLines, WzSubProperty scheduleProperty)
        {
            if (scheduleProperty?.WzProperties == null)
            {
                return;
            }

            List<(int Index, string Text)> scheduleRows = GetNumericNamedStringRows(scheduleProperty);

            if (scheduleRows.Count == 0)
            {
                return;
            }

            const int previewLineLimit = 4;
            int previewCount = Math.Min(scheduleRows.Count, previewLineLimit);
            for (int i = 0; i < previewCount; i++)
            {
                metadataLines.Add($"Available: {scheduleRows[i].Text}");
            }

            if (scheduleRows.Count > previewCount)
            {
                metadataLines.Add($"Available: ... and {(scheduleRows.Count - previewCount).ToString(CultureInfo.InvariantCulture)} more");
            }
        }

        private static List<(int Index, string Text)> GetNumericNamedStringRows(WzSubProperty property)
        {
            List<(int Index, string Text)> rows = new();
            if (property?.WzProperties == null)
            {
                return rows;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    continue;
                }

                string text = NormalizeTooltipText(GetStringValue(child));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    rows.Add((index, text));
                }
            }

            rows.Sort((left, right) => left.Index.CompareTo(right.Index));
            return rows;
        }

        private static IReadOnlyList<string> BuildAuthoredSampleTextLines(WzSubProperty sampleProperty)
        {
            if (sampleProperty?.WzProperties == null)
            {
                return Array.Empty<string>();
            }

            List<WzSubProperty> sampleVariants = GetNumericNamedChildren(sampleProperty);
            if (sampleVariants.Count == 0)
            {
                List<(int Index, string Text)> directRows = GetNumericNamedStringRows(sampleProperty);
                return BuildAuthoredSampleTextPreview(directRows, remainingVariantCount: 0);
            }

            for (int variantIndex = 0; variantIndex < sampleVariants.Count; variantIndex++)
            {
                List<(int Index, string Text)> rows = GetNumericNamedStringRows(sampleVariants[variantIndex]);
                if (rows.Count == 0)
                {
                    continue;
                }

                return BuildAuthoredSampleTextPreview(
                    rows,
                    Math.Max(0, sampleVariants.Count - variantIndex - 1));
            }

            return Array.Empty<string>();
        }

        private static IReadOnlyList<string> BuildAuthoredSampleTextPreview(
            IReadOnlyList<(int Index, string Text)> rows,
            int remainingVariantCount)
        {
            if (rows == null || rows.Count == 0)
            {
                return Array.Empty<string>();
            }

            const int previewLineLimit = 3;
            List<string> lines = new(Math.Min(previewLineLimit, rows.Count) + 1);
            int previewCount = Math.Min(previewLineLimit, rows.Count);
            for (int i = 0; i < previewCount; i++)
            {
                string text = NormalizeTooltipText(rows[i].Text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    lines.Add(text);
                }
            }

            int remainingRows = rows.Count - previewCount;
            if (remainingRows > 0)
            {
                lines.Add($"... and {remainingRows.ToString(CultureInfo.InvariantCulture)} more sample line{(remainingRows == 1 ? string.Empty : "s")}");
            }
            else if (remainingVariantCount > 0)
            {
                lines.Add($"... and {remainingVariantCount.ToString(CultureInfo.InvariantCulture)} more sample variant{(remainingVariantCount == 1 ? string.Empty : "s")}");
            }

            return lines;
        }

        private static IReadOnlyList<int> GetNumericNamedIntRows(WzSubProperty property)
        {
            List<(int Index, int Value)> rows = new();
            if (property?.WzProperties == null)
            {
                return Array.Empty<int>();
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    continue;
                }

                int value = GetIntOrStringValue(child);
                if (value > 0)
                {
                    rows.Add((index, value));
                }
            }

            if (rows.Count == 0)
            {
                return Array.Empty<int>();
            }

            rows.Sort((left, right) => left.Index.CompareTo(right.Index));
            List<int> values = new(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                values.Add(rows[i].Value);
            }

            return values;
        }

        private static List<(int Index, int Value)> GetNumericNamedIntValueRows(WzSubProperty property)
        {
            List<(int Index, int Value)> rows = new();
            if (property?.WzProperties == null)
            {
                return rows;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (!int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    continue;
                }

                int value = GetIntOrStringValue(child);
                if (value > 0)
                {
                    rows.Add((index, value));
                }
            }

            rows.Sort((left, right) => left.Index.CompareTo(right.Index));
            return rows;
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

            if (GetIntValue(infoProperty["notExtend"]) == 1)
            {
                metadataLines.Add("Duration cannot be extended");
            }

            if (GetIntValue(infoProperty["expireOnLogout"]) == 1)
            {
                metadataLines.Add("Expires on logout");
            }

            if (GetIntValue(infoProperty["noCancelMouse"]) == 1)
            {
                metadataLines.Add("Cannot be canceled by mouse");
            }

            if (GetIntValue(infoProperty["buffchair"]) == 1)
            {
                metadataLines.Add("Buff Chair");
            }

            if (GetIntValue(infoProperty["pachinko"]) == 1)
            {
                metadataLines.Add("Pachinko item");
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

            if (GetIntValue(infoProperty["pickUpBlock"]) == 1)
            {
                metadataLines.Add("Cannot be picked up");
            }

            if (GetIntValue(infoProperty["noMoveToLocker"]) == 1)
            {
                metadataLines.Add("Cannot be moved to locker");
            }

            if (GetIntValue(infoProperty["preventslip"]) == 1)
            {
                metadataLines.Add("Adds shoe traction");
            }

            if (GetIntValue(infoProperty["warmsupport"]) == 1)
            {
                metadataLines.Add("Adds cold-weather protection");
            }

            int protectionDays = GetIntOrStringValue(infoProperty["protectTime"]);
            if (protectionDays > 0)
            {
                metadataLines.Add($"Seals item for {FormatDayCount(protectionDays)}");
            }

            int karmaTier = GetIntOrStringValue(infoProperty["karma"]);
            if (karmaTier > 0)
            {
                metadataLines.Add($"Karma transfer tier: {karmaTier.ToString(CultureInfo.InvariantCulture)}");
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

            int setItemId = GetIntOrStringValue(infoProperty["setItemID"]);
            if (setItemId > 0)
            {
                metadataLines.Add($"Set Item ID: {setItemId.ToString(CultureInfo.InvariantCulture)}");
            }

            AppendPersonalityExperienceMetadataLines(metadataLines, infoProperty, specProperty);
        }

        private static void AppendPetUtilityMetadataLines(List<string> metadataLines, WzSubProperty infoProperty)
        {
            if (infoProperty == null)
            {
                return;
            }

            int petLifeDays = GetIntOrStringValue(infoProperty["life"]);
            if (petLifeDays > 0)
            {
                metadataLines.Add($"Pet Lifespan: {FormatDayCount(petLifeDays)}");
            }

            int limitedPetLifeSeconds = GetIntOrStringValue(infoProperty["limitedLife"]);
            if (limitedPetLifeSeconds > 0)
            {
                metadataLines.Add($"Pet Limited Lifespan: {FormatSecondDuration(limitedPetLifeSeconds)}");
            }

            int hunger = GetIntOrStringValue(infoProperty["hungry"]);
            if (hunger > 0)
            {
                metadataLines.Add($"Pet Hunger: {hunger.ToString(CultureInfo.InvariantCulture)}");
            }

            int chatBalloonStyle = GetIntOrStringValue(infoProperty["chatBalloon"]);
            if (chatBalloonStyle > 0)
            {
                metadataLines.Add($"Pet Chat Balloon Style: {chatBalloonStyle.ToString(CultureInfo.InvariantCulture)}");
            }

            int nameTagStyle = GetIntOrStringValue(infoProperty["nameTag"]);
            if (nameTagStyle > 0)
            {
                metadataLines.Add($"Pet Name Tag Style: {nameTagStyle.ToString(CultureInfo.InvariantCulture)}");
            }

            if (GetIntValue(infoProperty["pickupItem"]) == 1)
            {
                metadataLines.Add("Automatically loots items");
            }

            if (GetIntValue(infoProperty["pickupMeso"]) == 1)
            {
                metadataLines.Add("Automatically loots mesos");
            }

            if (GetIntValue(infoProperty["pickupAll"]) == 1)
            {
                metadataLines.Add("Automatically loots items and mesos");
            }

            if (GetIntValue(infoProperty["pickupOthers"]) == 1)
            {
                metadataLines.Add("Automatically loots other players' drops");
            }

            if (GetIntValue(infoProperty["ignorePickup"]) == 1)
            {
                metadataLines.Add("Ignores pet looting");
            }

            if (GetIntValue(infoProperty["sweepForDrop"]) == 1
                || GetIntValue(infoProperty["dropSweep"]) == 1)
            {
                metadataLines.Add("Sweeps nearby drops");
            }

            if (GetIntValue(infoProperty["longRange"]) == 1)
            {
                metadataLines.Add("Extended pet pickup range");
            }

            if (GetIntValue(infoProperty["multiPet"]) == 1)
            {
                metadataLines.Add("Supports multi-pet ownership");
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

        internal static IReadOnlyList<string> BuildPetUtilityMetadataLinesForTesting(WzSubProperty infoProperty)
        {
            List<string> metadataLines = new();
            AppendCashAvailabilityMetadataLines(metadataLines, infoProperty);
            AppendPetUtilityMetadataLines(metadataLines, infoProperty);
            return metadataLines;
        }

        internal static IReadOnlyList<string> BuildTargetMobMetadataLinesForTesting(
            WzSubProperty infoProperty,
            WzSubProperty specProperty)
        {
            List<string> metadataLines = new();
            AppendTargetMobMetadataLines(metadataLines, infoProperty, specProperty);
            return metadataLines;
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

        internal static bool IsPetFoodSpecForTesting(int itemId, WzSubProperty specProperty)
        {
            return IsPetFoodSpec(itemId, specProperty);
        }

        private static bool IsPetFoodSpec(int itemId, WzSubProperty specProperty)
        {
            if (GetIntValue(specProperty?["inc"]) <= 0)
            {
                return false;
            }

            return itemId / 10000 == 212
                   || HasNumericChildEntries(specProperty, "inc");
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

        private static string ResolveMobSkillTooltipLabel(int skillId)
        {
            if (TryResolvePlayerMobSkillStatusLabel(skillId, out string playerStatusLabel))
            {
                return playerStatusLabel;
            }

            if (MobSkillStatusMapper.TryGetDefinition(skillId, out MobSkillStatusDefinition definition))
            {
                return ResolveMobBuffSkillLabel(definition);
            }

            return $"Mob Skill #{skillId.ToString(CultureInfo.InvariantCulture)}";
        }

        private static bool TryResolvePlayerMobSkillStatusLabel(int skillId, out string label)
        {
            label = skillId switch
            {
                120 => "Seal",
                121 => "Darkness",
                122 => "Weakness",
                123 => "Stun",
                124 => "Curse",
                125 => "Poison",
                126 => "Slow",
                127 => "Buff Cancel",
                128 => "Seduce",
                129 => "Banish",
                131 => "Freeze",
                132 => "Reverse Controls",
                133 => "Zombie",
                134 => "Pain Mark",
                135 => "Potion Lock",
                136 => "Stop Motion",
                137 => "Fear",
                172 or 173 => "Polymorph",
                _ => null
            };

            return !string.IsNullOrWhiteSpace(label);
        }

        private static string ResolveMobBuffSkillLabel(MobSkillStatusDefinition definition)
        {
            if (definition.Operation == MobSkillOperation.Heal)
            {
                return "Monster Heal";
            }

            if (definition.Operation == MobSkillOperation.ClearNegativeStatuses)
            {
                return "Monster Dispel";
            }

            return definition.Effect switch
            {
                MobStatusEffect.PowerUp => "Power Up",
                MobStatusEffect.MagicUp => "Magic Up",
                MobStatusEffect.PGuardUp => "Weapon DEF Up",
                MobStatusEffect.MGuardUp => "Magic DEF Up",
                MobStatusEffect.Speed => "Speed Up",
                MobStatusEffect.PImmune => "Weapon Immune",
                MobStatusEffect.MImmune => "Magic Immune",
                MobStatusEffect.HardSkin => "Hard Skin",
                MobStatusEffect.Reflect => "Weapon / Magic Reflect",
                MobStatusEffect.ACC => "Accuracy Up",
                MobStatusEffect.EVA => "Avoidability Up",
                MobStatusEffect.Rich => "Treasure Up",
                _ => $"Mob Skill #{definition.SkillId.ToString(CultureInfo.InvariantCulture)}"
            };
        }

        private static string FormatMobSkillTargetLabel(int target)
        {
            return target switch
            {
                1 => "Self",
                2 => "Target",
                3 => "Area",
                _ => $"Target {target.ToString(CultureInfo.InvariantCulture)}"
            };
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

        private static string FormatMapRangeLabel(int startMapId, int endMapId)
        {
            if (startMapId == endMapId)
            {
                string mapName = ResolveMapName(startMapId);
                return string.IsNullOrWhiteSpace(mapName)
                    ? startMapId.ToString(CultureInfo.InvariantCulture)
                    : mapName;
            }

            string startLabel = ResolveMapName(startMapId);
            if (string.IsNullOrWhiteSpace(startLabel))
            {
                startLabel = startMapId.ToString(CultureInfo.InvariantCulture);
            }

            string endLabel = ResolveMapName(endMapId);
            if (string.IsNullOrWhiteSpace(endLabel))
            {
                endLabel = endMapId.ToString(CultureInfo.InvariantCulture);
            }

            return $"{startLabel} - {endLabel}";
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

        public static IReadOnlyList<string> BuildAuthoredSampleTextLinesForTests(WzSubProperty sampleProperty)
        {
            return BuildAuthoredSampleTextLines(sampleProperty);
        }

        public static bool TryResolveSampleUiFrameForTests(
            WzSubProperty itemProperty,
            out WzCanvasProperty topCanvas,
            out WzCanvasProperty centerCanvas,
            out WzCanvasProperty bottomCanvas)
        {
            return TryResolveSampleUiFrame(itemProperty, out topCanvas, out centerCanvas, out bottomCanvas);
        }

        public static bool TrySelectConsumeItemRequirementForTests(
            IReadOnlyList<ConsumeItemRequirementMetadata> requirements,
            IReadOnlyDictionary<int, int> ownedCounts,
            int weightedRoll,
            out ConsumeItemRequirementMetadata selectedRequirement)
        {
            return TrySelectConsumeItemRequirement(
                requirements,
                itemId => ownedCounts != null && ownedCounts.TryGetValue(itemId, out int count) ? count : 0,
                weightedRoll,
                out selectedRequirement);
        }

        public static IReadOnlyList<string> BuildEffectLinesForTests(WzSubProperty specProperty)
        {
            return BuildEffectLines(0, null, null, specProperty, null);
        }

        public static IReadOnlyList<string> BuildEffectLinesForTests(int itemId, WzSubProperty specProperty)
        {
            return BuildEffectLines(itemId, null, null, specProperty, null);
        }

        public static IReadOnlyList<string> BuildEffectLinesForTests(WzSubProperty infoProperty, WzSubProperty specProperty)
        {
            return BuildEffectLines(0, null, infoProperty, specProperty, null);
        }

        public static IReadOnlyList<string> BuildEffectLinesWithSpecExForTests(WzSubProperty infoProperty, WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return BuildEffectLines(0, null, infoProperty, specProperty, specExProperty);
        }

        public static IReadOnlyList<string> BuildEffectLinesWithItemPropertyForTests(WzSubProperty itemProperty, WzSubProperty infoProperty, WzSubProperty specProperty)
        {
            return BuildEffectLines(0, itemProperty, infoProperty, specProperty, null);
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
                null,
                isNotForSale: false,
                isQuestItem: false,
                isTradeBlocked: false,
                isOneOfAKind: false,
                expirationDateUtc: null);
        }

        public static IReadOnlyList<string> BuildMetadataLinesWithSpecExForTests(WzSubProperty infoProperty, WzSubProperty specProperty, WzSubProperty specExProperty)
        {
            return BuildMetadataLines(
                0,
                infoProperty,
                specProperty,
                specExProperty,
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

        private static string FormatEligibleEquipSlotLabels(IReadOnlyList<int> selectedSlots)
        {
            if (selectedSlots == null || selectedSlots.Count == 0)
            {
                return string.Empty;
            }

            List<string> labels = new(selectedSlots.Count);
            HashSet<string> seenLabels = new(StringComparer.Ordinal);
            for (int i = 0; i < selectedSlots.Count; i++)
            {
                string label = ResolveEligibleEquipSlotLabel(selectedSlots[i]);
                if (!string.IsNullOrWhiteSpace(label) && seenLabels.Add(label))
                {
                    labels.Add(label);
                }
            }

            return labels.Count > 0
                ? string.Join(", ", labels)
                : string.Join(", ", selectedSlots);
        }

        private static string FormatExtensionSlotLabel(int slotIndex)
        {
            return slotIndex switch
            {
                0 => "Pendant",
                _ => $"Slot {slotIndex.ToString(CultureInfo.InvariantCulture)}"
            };
        }

        private static string ResolveEligibleEquipSlotLabel(int slotValue)
        {
            EquipSlot slot = Enum.IsDefined(typeof(EquipSlot), slotValue)
                ? (EquipSlot)slotValue
                : EquipSlot.None;

            return slot switch
            {
                EquipSlot.Cap => "Cap",
                EquipSlot.FaceAccessory => "Face Accessory",
                EquipSlot.EyeAccessory => "Eye Accessory",
                EquipSlot.Earrings => "Earrings",
                EquipSlot.Coat => "Top",
                EquipSlot.Longcoat => "Overall",
                EquipSlot.Pants => "Bottom",
                EquipSlot.Shoes => "Shoes",
                EquipSlot.Glove => "Gloves",
                EquipSlot.Shield => "Shield",
                EquipSlot.Cape => "Cape",
                EquipSlot.Ring1 or EquipSlot.Ring2 or EquipSlot.Ring3 or EquipSlot.Ring4 => "Ring",
                EquipSlot.Pendant or EquipSlot.Pendant2 => "Pendant",
                EquipSlot.Belt => "Belt",
                EquipSlot.Medal => "Medal",
                EquipSlot.Shoulder => "Shoulder",
                EquipSlot.Pocket => "Pocket",
                EquipSlot.Badge => "Badge",
                EquipSlot.Weapon => "Weapon",
                EquipSlot.TamingMob => "Mount",
                EquipSlot.Saddle => "Saddle",
                EquipSlot.TamingMobAccessory => "Mount Accessory",
                EquipSlot.Android => "Android",
                EquipSlot.AndroidHeart => "Android Heart",
                _ => slotValue.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static int? ResolveRequiredLevel(WzSubProperty infoProperty)
        {
            return TryGetPositiveInt(infoProperty?["lv"])
                   ?? TryGetPositiveInt(infoProperty?["reqLevel"])
                   ?? TryGetPositiveInt(infoProperty?["reqLEV"]);
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

        private static bool TryGetSignedInt(WzImageProperty property, out int value)
        {
            if (property is WzStringProperty stringProperty
                && int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                value = parsed;
                return true;
            }

            switch (property)
            {
                case WzIntProperty intProperty:
                    value = intProperty.Value;
                    return true;
                case WzShortProperty shortProperty:
                    value = shortProperty.Value;
                    return true;
                case WzFloatProperty floatProperty:
                    value = (int)floatProperty.Value;
                    return true;
                case WzDoubleProperty doubleProperty:
                    value = (int)doubleProperty.Value;
                    return true;
                default:
                    value = 0;
                    return false;
            }
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
