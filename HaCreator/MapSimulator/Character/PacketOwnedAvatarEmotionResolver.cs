using HaCreator.MapSimulator.UI;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HaCreator.MapSimulator.Character
{
    internal readonly record struct PacketOwnedAvatarEmotionWeight(string EmotionName, int EmotionId, int Weight);

    internal readonly record struct PacketOwnedAvatarEmotionSelection(
        int AreaBuffItemId,
        int EmotionId,
        string EmotionName,
        int RandomRoll,
        int TotalWeight,
        int SelectedWeight);

    internal static class PacketOwnedAvatarEmotionResolver
    {
        // Client packet emotion ids inferred from the avatar face-expression surface
        // used by CAvatar::SetEmotion together with the authored area-buff emotion names.
        private static readonly string[] EmotionNamesById =
        {
            "default",
            "smile",
            "troubled",
            "cry",
            "angry",
            "bewildered",
            "stunned",
            "vomit",
            "oops",
            "cheers",
            "chu",
            "wink",
            "pain",
            "glitter",
            "shine",
            "love",
            "despair",
            "hum",
            "bowing",
            "dam",
            "qBlue",
            "angry_short",
            "walk1",
            "walk2"
        };

        private static readonly IReadOnlyDictionary<string, int> EmotionIdsByName = BuildEmotionIdsByName();

        public static bool TryResolveEmotionName(int emotionId, out string emotionName)
        {
            emotionName = null;
            if (emotionId < 0 || emotionId >= EmotionNamesById.Length)
            {
                return false;
            }

            emotionName = EmotionNamesById[emotionId];
            return !string.IsNullOrWhiteSpace(emotionName);
        }

        public static bool TryResolveEmotionId(string emotionName, out int emotionId)
        {
            emotionId = 0;
            return !string.IsNullOrWhiteSpace(emotionName)
                   && EmotionIdsByName.TryGetValue(emotionName.Trim(), out emotionId);
        }

        public static bool TryResolveWeightedEmotion(
            IReadOnlyList<PacketOwnedAvatarEmotionWeight> weightedEmotions,
            int randomRoll,
            int areaBuffItemId,
            out PacketOwnedAvatarEmotionSelection selection)
        {
            selection = default;
            if (weightedEmotions == null || weightedEmotions.Count == 0)
            {
                return false;
            }

            int totalWeight = 0;
            for (int i = 0; i < weightedEmotions.Count; i++)
            {
                if (weightedEmotions[i].Weight > 0)
                {
                    totalWeight += weightedEmotions[i].Weight;
                }
            }

            if (totalWeight <= 0)
            {
                return false;
            }

            int normalizedRoll = randomRoll < 0 ? -randomRoll : randomRoll;
            normalizedRoll %= totalWeight;

            int cumulativeWeight = 0;
            for (int i = 0; i < weightedEmotions.Count; i++)
            {
                PacketOwnedAvatarEmotionWeight weight = weightedEmotions[i];
                if (weight.Weight <= 0)
                {
                    continue;
                }

                cumulativeWeight += weight.Weight;
                if (normalizedRoll < cumulativeWeight)
                {
                    selection = new PacketOwnedAvatarEmotionSelection(
                        areaBuffItemId,
                        weight.EmotionId,
                        weight.EmotionName,
                        normalizedRoll,
                        totalWeight,
                        weight.Weight);
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveRandomEmotion(
            int areaBuffItemId,
            int randomRoll,
            out PacketOwnedAvatarEmotionSelection selection,
            out string error)
        {
            selection = default;
            error = null;

            if (areaBuffItemId <= 0)
            {
                error = "Area-buff item id must be positive.";
                return false;
            }

            if (!TryLoadAreaBuffEmotionWeights(areaBuffItemId, out IReadOnlyList<PacketOwnedAvatarEmotionWeight> weightedEmotions, out error))
            {
                return false;
            }

            if (!TryResolveWeightedEmotion(weightedEmotions, randomRoll, areaBuffItemId, out selection))
            {
                error = $"Area-buff item {areaBuffItemId} did not produce a weighted emotion selection.";
                return false;
            }

            return true;
        }

        public static bool TryResolveItemEmotion(
            int itemId,
            int randomRoll,
            out PacketOwnedAvatarEmotionSelection selection,
            out bool byItemOption,
            out string error)
        {
            selection = default;
            byItemOption = false;
            error = null;

            if (itemId <= 0)
            {
                error = "Item id must be positive.";
                return false;
            }

            WzSubProperty itemProperty = LoadItemProperty(itemId);
            if (itemProperty == null)
            {
                error = $"Item {itemId} could not be loaded from WZ.";
                return false;
            }

            if (TryResolveFixedEmotion(itemId, itemProperty, out selection))
            {
                byItemOption = true;
                return true;
            }

            if (itemProperty["emotion"] is WzSubProperty)
            {
                return TryResolveRandomEmotion(itemId, randomRoll, out selection, out error);
            }

            error = $"Item {itemId} does not publish a supported packet-owned emotion mapping.";
            return false;
        }

        public static bool TryLoadAreaBuffEmotionWeights(
            int areaBuffItemId,
            out IReadOnlyList<PacketOwnedAvatarEmotionWeight> weightedEmotions,
            out string error)
        {
            weightedEmotions = Array.Empty<PacketOwnedAvatarEmotionWeight>();
            error = null;

            WzSubProperty itemProperty = LoadItemProperty(areaBuffItemId);
            if (itemProperty == null)
            {
                error = $"Area-buff item {areaBuffItemId} could not be loaded from WZ.";
                return false;
            }

            if (itemProperty["emotion"] is not WzSubProperty emotionProperty)
            {
                error = $"Area-buff item {areaBuffItemId} does not publish an emotion table.";
                return false;
            }

            List<PacketOwnedAvatarEmotionWeight> resolvedWeights = new();
            foreach (WzImageProperty emotionEntry in emotionProperty.WzProperties)
            {
                int weight = GetIntValue(emotionEntry);
                if (weight <= 0 || !TryResolveEmotionId(emotionEntry.Name, out int emotionId))
                {
                    continue;
                }

                resolvedWeights.Add(new PacketOwnedAvatarEmotionWeight(
                    emotionEntry.Name,
                    emotionId,
                    weight));
            }

            if (resolvedWeights.Count == 0)
            {
                error = $"Area-buff item {areaBuffItemId} does not contain any supported weighted emotions.";
                return false;
            }

            weightedEmotions = resolvedWeights;
            return true;
        }

        private static IReadOnlyDictionary<string, int> BuildEmotionIdsByName()
        {
            Dictionary<string, int> idsByName = new(StringComparer.OrdinalIgnoreCase);
            for (int emotionId = 0; emotionId < EmotionNamesById.Length; emotionId++)
            {
                string emotionName = EmotionNamesById[emotionId];
                if (!string.IsNullOrWhiteSpace(emotionName))
                {
                    idsByName[emotionName] = emotionId;
                }
            }

            return idsByName;
        }

        private static bool TryResolveFixedEmotion(
            int itemId,
            WzSubProperty itemProperty,
            out PacketOwnedAvatarEmotionSelection selection)
        {
            selection = default;
            if (itemProperty?["info"] is not WzSubProperty infoProperty)
            {
                return false;
            }

            int emotionId = GetIntValue(infoProperty["emotion"]);
            if (!TryResolveEmotionName(emotionId, out string emotionName))
            {
                return false;
            }

            selection = new PacketOwnedAvatarEmotionSelection(
                itemId,
                emotionId,
                emotionName,
                RandomRoll: 0,
                TotalWeight: 1,
                SelectedWeight: 1);
            return true;
        }

        private static WzSubProperty LoadItemProperty(int itemId)
        {
            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemNodeName = category == "Character"
                ? itemId.ToString("D8", CultureInfo.InvariantCulture)
                : itemId.ToString("D7", CultureInfo.InvariantCulture);
            return itemImage[itemNodeName] as WzSubProperty;
        }

        private static int GetIntValue(WzImageProperty property)
        {
            return property switch
            {
                WzIntProperty intProperty => intProperty.Value,
                WzShortProperty shortProperty => shortProperty.Value,
                WzLongProperty longProperty => (int)Math.Clamp(longProperty.Value, int.MinValue, int.MaxValue),
                WzStringProperty stringProperty when int.TryParse(stringProperty.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) => parsedValue,
                _ => 0
            };
        }
    }
}
