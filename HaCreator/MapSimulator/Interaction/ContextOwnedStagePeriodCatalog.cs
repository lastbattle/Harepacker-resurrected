using HaCreator.MapEditor.Info;
using HaCreator.MapSimulator.Managers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal sealed class ContextOwnedStageSystemCatalog
    {
        private readonly Dictionary<string, ContextOwnedStageThemeCatalogEntry> _themes;
        private readonly Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> _affectedMapsByFieldId;

        private ContextOwnedStageSystemCatalog(
            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes,
            Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> affectedMapsByFieldId)
        {
            _themes = themes ?? new Dictionary<string, ContextOwnedStageThemeCatalogEntry>(StringComparer.Ordinal);
            _affectedMapsByFieldId = affectedMapsByFieldId ?? new Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>>();
        }

        internal bool TryGetPeriod(string stageTheme, byte mode, out ContextOwnedStagePeriodCatalogEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(stageTheme)
                || !_themes.TryGetValue(stageTheme, out ContextOwnedStageThemeCatalogEntry theme))
            {
                return false;
            }

            return theme.TryGetPeriod(mode, out entry);
        }

        internal HashSet<int> ResolveAffectedMaps(
            ContextOwnedStagePeriodCatalogEntry entry,
            Func<int, QuestStateType> questStateProvider = null,
            int elapsedStagePeriodMilliseconds = 0)
        {
            HashSet<int> affectedMaps = new(entry?.AffectedMapIds ?? Enumerable.Empty<int>());
            if (entry == null || entry.Keywords.Count == 0)
            {
                return affectedMaps;
            }

            foreach ((int fieldId, IReadOnlyList<ContextOwnedStageAffectedMapEntry> entries) in _affectedMapsByFieldId)
            {
                if (entries != null && entries.Any(candidate => candidate.Matches(entry, questStateProvider, elapsedStagePeriodMilliseconds)))
                {
                    affectedMaps.Add(fieldId);
                }
            }

            return affectedMaps;
        }

        internal void ApplyCacheData(
            ContextOwnedStagePeriodCatalogEntry entry,
            IDictionary<string, ContextOwnedStageUnitEnableState> keywordCache,
            IDictionary<int, ContextOwnedStageUnitEnableState> questCache,
            IDictionary<string, byte> stagePeriodCache)
        {
            if (entry == null)
            {
                return;
            }

            ApplyThemeScopedEnableState(entry.StageTheme, entry.Keywords, keywordCache);
            ApplyThemeScopedEnableState(entry.StageTheme, entry.EnabledQuestIds, questCache);
            if (stagePeriodCache != null && !string.IsNullOrWhiteSpace(entry.StageTheme))
            {
                stagePeriodCache[entry.StageTheme] = entry.Mode;
            }
        }

        internal static void ResetCacheData(
            IDictionary<string, ContextOwnedStageUnitEnableState> keywordCache,
            IDictionary<int, ContextOwnedStageUnitEnableState> questCache,
            IDictionary<string, byte> stagePeriodCache)
        {
            keywordCache?.Clear();
            questCache?.Clear();
            stagePeriodCache?.Clear();
        }

        internal static HashSet<string> CaptureEnabledKeywords(IDictionary<string, ContextOwnedStageUnitEnableState> keywordCache)
        {
            return CaptureEnabledValues(keywordCache);
        }

        internal static HashSet<int> CaptureEnabledQuestIds(IDictionary<int, ContextOwnedStageUnitEnableState> questCache)
        {
            return CaptureEnabledValues(questCache);
        }

        internal static ContextOwnedStageSystemCatalog Build(
            string stageSystemPath,
            string stageKeywordPath,
            string stageAffectedMapPath,
            out string error)
        {
            error = null;
            if (!TryLoadImage(stageSystemPath, out WzImage stageSystemImage))
            {
                error = $"Context-owned stage-period validation could not load {stageSystemPath}, so the simulator cannot mirror CStageSystem::BuildCacheData acceptance yet.";
                return null;
            }

            if (!TryLoadImage(stageKeywordPath, out WzImage stageKeywordImage))
            {
                error = $"Context-owned stage-period validation could not load {stageKeywordPath}, so the simulator cannot mirror CStageSystem::IterateStageSystemClient acceptance yet.";
                return null;
            }

            WzImage stageAffectedMapImage = null;
            TryLoadImage(stageAffectedMapPath, out stageAffectedMapImage);
            return BuildCore(
                stageSystemPath,
                stageSystemImage,
                stageKeywordPath,
                stageKeywordImage,
                stageAffectedMapImage,
                requireStageKeywordImage: true,
                out error);
        }

        internal static ContextOwnedStageSystemCatalog BuildForTesting(
            WzImageProperty stageSystemRoot,
            out string error,
            WzImageProperty stageKeywordRoot = null,
            WzImageProperty stageAffectedMapRoot = null,
            bool requireStageKeywordImage = false)
        {
            return BuildCore(
                "Etc/StageSystem.img",
                stageSystemRoot,
                "Etc/StageKeyword.img",
                stageKeywordRoot,
                stageAffectedMapRoot,
                requireStageKeywordImage,
                out error);
        }

        internal IEnumerable<ContextOwnedStagePeriodCatalogEntry> EnumeratePeriods()
        {
            foreach (ContextOwnedStageThemeCatalogEntry theme in _themes.Values)
            {
                foreach (ContextOwnedStagePeriodCatalogEntry period in theme.Periods)
                {
                    yield return period;
                }
            }
        }

        private static Dictionary<string, ContextOwnedStageThemeCatalogEntry> BuildThemes(IEnumerable<WzImageProperty> themeProperties)
        {
            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes = new(StringComparer.Ordinal);
            if (themeProperties == null)
            {
                return themes;
            }

            foreach (WzImageProperty themeProperty in themeProperties)
            {
                Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> periods = BuildPeriods(themeProperty);
                if (periods.Count > 0)
                {
                    themes[themeProperty.Name] = new ContextOwnedStageThemeCatalogEntry(themeProperty.Name, periods);
                }
            }

            return themes;
        }

        private static Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> BuildPeriods(WzImageProperty themeProperty)
        {
            Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> periods = new();
            foreach ((byte mode, WzImageProperty periodNode) in EnumeratePeriodNodes(themeProperty))
            {
                periods[mode] = new ContextOwnedStagePeriodCatalogEntry(
                    themeProperty.Name,
                    mode,
                    TryReadStageBackColor(periodNode),
                    ParseStageBackImages(periodNode),
                    ParseStringSet(periodNode["stageKeyword"] ?? periodNode["keyword"] ?? periodNode["aKeyword"]),
                    ParseIntSet(periodNode["enabledQuest"] ?? periodNode["aEnabledQuest"] ?? periodNode["questID"] ?? periodNode["quest"]),
                    ParseIntSet(periodNode["affectedMap"] ?? periodNode["fieldID"] ?? periodNode["aAffectedMap"]));
            }

            return periods;
        }

        private static IEnumerable<(byte Mode, WzImageProperty Property)> EnumeratePeriodNodes(WzImageProperty themeProperty)
        {
            if (themeProperty["stage"] is WzImageProperty stageProperty)
            {
                foreach ((byte mode, WzImageProperty property) in EnumerateIndexedPeriodNodes(stageProperty))
                {
                    yield return (mode, property);
                }
                yield break;
            }

            if (themeProperty["stageList"] is WzImageProperty stageList)
            {
                foreach ((byte mode, WzImageProperty property) in EnumerateIndexedPeriodNodes(stageList))
                {
                    yield return (mode, property);
                }
                yield break;
            }

            foreach (WzImageProperty child in themeProperty.WzProperties.OfType<WzImageProperty>())
            {
                if (byte.TryParse(child.Name, NumberStyles.None, CultureInfo.InvariantCulture, out byte mode))
                {
                    yield return (mode, child);
                }
            }
        }

        private static IEnumerable<(byte Mode, WzImageProperty Property)> EnumerateIndexedPeriodNodes(WzImageProperty container)
        {
            int count = container?.WzProperties?.Count ?? 0;
            for (int ordinal = 0; ordinal < count && ordinal <= byte.MaxValue; ordinal++)
            {
                // `CStageSystem::IterateStageSystemClient` walks `stage` / `stageList`
                // by zero-based string index and uses that same index as the period mode.
                if (container[ordinal.ToString(CultureInfo.InvariantCulture)] is WzImageProperty period)
                {
                    yield return ((byte)ordinal, period);
                }
            }
        }

        private static uint? TryReadStageBackColor(WzImageProperty periodNode)
        {
            return unchecked((uint)InfoTool.GetInt(periodNode["backColor"], -1));
        }

        private static IReadOnlyList<ContextOwnedStageBackImageEntry> ParseStageBackImages(WzImageProperty periodNode)
        {
            List<ContextOwnedStageBackImageEntry> entries = new();
            WzImageProperty container = periodNode["aStageBackImg"]
                ?? periodNode["stageBackImg"]
                ?? periodNode["backImg"]
                ?? periodNode["back"];

            if (container == null)
            {
                if (TryParseStageBackImageEntry(periodNode, out ContextOwnedStageBackImageEntry directEntry))
                {
                    entries.Add(directEntry);
                }

                return entries;
            }

            foreach (WzImageProperty child in container.WzProperties.OfType<WzImageProperty>())
            {
                if (TryParseStageBackImageEntry(child, out ContextOwnedStageBackImageEntry entry))
                {
                    entries.Add(entry);
                    continue;
                }

                AppendNativeStageBackImageEntries(child, entries);
            }

            return entries;
        }

        private static void AppendNativeStageBackImageEntries(
            WzImageProperty stageBackImageGroup,
            List<ContextOwnedStageBackImageEntry> entries)
        {
            if (stageBackImageGroup == null || entries == null)
            {
                return;
            }

            if (TryGetNativeStageBackSide(stageBackImageGroup.Name, out bool front))
            {
                AppendNativeStageBackSideContainerEntries(stageBackImageGroup, front, entries);
                return;
            }

            string backgroundSet = stageBackImageGroup.Name;
            int entryCountBeforeWrapperParse = entries.Count;
            AppendNativeStageBackImageEntries(backgroundSet, stageBackImageGroup["back"], front: false, entries);
            AppendNativeStageBackImageEntries(backgroundSet, stageBackImageGroup["front"], front: true, entries);
            if (entries.Count != entryCountBeforeWrapperParse)
            {
                return;
            }

            // `LoadStageBackImgInfo` enumerates each background set under
            // `aStageBackImg` and then its numeric object children directly.
            AppendNativeStageBackImageEntries(backgroundSet, stageBackImageGroup, front: false, entries);
        }

        private static void AppendNativeStageBackSideContainerEntries(
            WzImageProperty sideContainer,
            bool front,
            List<ContextOwnedStageBackImageEntry> entries)
        {
            if (sideContainer == null || entries == null)
            {
                return;
            }

            foreach (WzImageProperty backgroundSetProperty in sideContainer.WzProperties.OfType<WzImageProperty>())
            {
                AppendNativeStageBackImageEntries(backgroundSetProperty.Name, backgroundSetProperty, front, entries);
            }
        }

        private static bool TryGetNativeStageBackSide(string name, out bool front)
        {
            if (string.Equals(name, "back", StringComparison.Ordinal))
            {
                front = false;
                return true;
            }

            if (string.Equals(name, "front", StringComparison.Ordinal))
            {
                front = true;
                return true;
            }

            front = false;
            return false;
        }

        private static void AppendNativeStageBackImageEntries(
            string backgroundSet,
            WzImageProperty container,
            bool front,
            List<ContextOwnedStageBackImageEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(backgroundSet) || container == null || entries == null)
            {
                return;
            }

            foreach (WzImageProperty property in container.WzProperties.OfType<WzImageProperty>())
            {
                if (TryAppendNativeStageBackImageEntry(backgroundSet, property, front, entries))
                {
                    continue;
                }

                foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
                {
                    TryAppendNativeStageBackImageEntry(property.Name, child, front, entries);
                }
            }
        }

        private static bool TryAppendNativeStageBackImageEntry(
            string backgroundSet,
            WzImageProperty property,
            bool front,
            List<ContextOwnedStageBackImageEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(backgroundSet)
                || property == null
                || entries == null
                || !int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                || number < 0)
            {
                return false;
            }

            string spineAnimation = InfoTool.GetString(property["spineAni"]);
            bool animated = InfoTool.GetBool(property["ani"]);
            BackgroundInfoType infoType = !string.IsNullOrWhiteSpace(spineAnimation)
                ? BackgroundInfoType.Spine
                : animated
                    ? BackgroundInfoType.Animation
                    : BackgroundInfoType.Background;
            MapleBool flipValue = InfoTool.GetOptionalBool(property["f"]);
            entries.Add(new ContextOwnedStageBackImageEntry(
                backgroundSet.Trim(),
                number.ToString(CultureInfo.InvariantCulture),
                infoType,
                InfoTool.GetInt(property["x"]),
                InfoTool.GetInt(property["y"]),
                InfoTool.GetInt(property["absRX"], 1),
                InfoTool.GetInt(property["absRY"], 1),
                InfoTool.GetInt(property["cx"]),
                InfoTool.GetInt(property["cy"]),
                Math.Clamp(InfoTool.GetInt(property["a"], 255), 0, 255),
                (BackgroundType)InfoTool.GetInt(property["type"]),
                property["front"] == null ? front : InfoTool.GetBool(property["front"]),
                flipValue.HasValue && flipValue.Value,
                InfoTool.GetInt(property["page"]),
                InfoTool.GetInt(property["screenMode"], 0),
                InfoTool.GetInt(property["z"]),
                spineAnimation,
                InfoTool.GetBool(property["spineRandomStart"])));
            return true;
        }

        private static bool TryParseStageBackImageEntry(WzImageProperty property, out ContextOwnedStageBackImageEntry entry)
        {
            entry = null;
            if (property == null)
            {
                return false;
            }

            string backgroundSet = InfoTool.GetString(property["bS"]);
            string number = ResolveBackgroundNumber(property);
            if (string.IsNullOrWhiteSpace(backgroundSet) || string.IsNullOrWhiteSpace(number))
            {
                return false;
            }

            string spineAnimation = InfoTool.GetString(property["spineAni"]);
            bool animated = InfoTool.GetBool(property["ani"]);
            BackgroundInfoType infoType = !string.IsNullOrWhiteSpace(spineAnimation)
                ? BackgroundInfoType.Spine
                : animated
                    ? BackgroundInfoType.Animation
                    : BackgroundInfoType.Background;

            MapleBool flipValue = InfoTool.GetOptionalBool(property["f"]);
            entry = new ContextOwnedStageBackImageEntry(
                backgroundSet,
                number,
                infoType,
                InfoTool.GetInt(property["x"]),
                InfoTool.GetInt(property["y"]),
                InfoTool.GetInt(property["rx"]),
                InfoTool.GetInt(property["ry"]),
                InfoTool.GetInt(property["cx"]),
                InfoTool.GetInt(property["cy"]),
                Math.Clamp(InfoTool.GetInt(property["a"], 255), 0, 255),
                (BackgroundType)InfoTool.GetInt(property["type"]),
                InfoTool.GetBool(property["front"]),
                flipValue.HasValue && flipValue.Value,
                InfoTool.GetInt(property["page"]),
                InfoTool.GetInt(property["screenMode"], 0),
                InfoTool.GetInt(property["z"]),
                spineAnimation,
                InfoTool.GetBool(property["spineRandomStart"]));
            return true;
        }

        private static string ResolveBackgroundNumber(WzImageProperty property)
        {
            if (property["no"] == null)
            {
                return null;
            }

            string text = InfoTool.GetString(property["no"]);
            return !string.IsNullOrWhiteSpace(text)
                ? text
                : InfoTool.GetInt(property["no"]).ToString(CultureInfo.InvariantCulture);
        }

        private static HashSet<string> ParseStringSet(WzImageProperty property)
        {
            HashSet<string> values = new(StringComparer.Ordinal);
            AppendStringValues(property, values);
            return values;
        }

        private static void AppendStringValues(WzImageProperty property, HashSet<string> values)
        {
            if (property == null)
            {
                return;
            }

            if (property is WzStringProperty stringProperty && !string.IsNullOrWhiteSpace(stringProperty.Value))
            {
                values.Add(stringProperty.Value.Trim());
                return;
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                string text = child is WzStringProperty childString
                    ? childString.Value
                    : InfoTool.GetString(child);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text.Trim());
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(child.Name)
                    && !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    values.Add(child.Name.Trim());
                }
            }
        }

        private static HashSet<int> ParseIntSet(WzImageProperty property)
        {
            HashSet<int> values = new();
            AppendIntValues(property, values);
            return values;
        }

        private static void AppendIntValues(WzImageProperty property, HashSet<int> values)
        {
            if (property == null)
            {
                return;
            }

            if (property is WzIntProperty intProperty)
            {
                values.Add(intProperty.Value);
                return;
            }

            string direct = InfoTool.GetString(property);
            if (int.TryParse(direct, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDirect))
            {
                values.Add(parsedDirect);
                return;
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                if (child is WzIntProperty childInt)
                {
                    values.Add(childInt.Value);
                    continue;
                }

                string text = InfoTool.GetString(child);
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    || int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    values.Add(parsed);
                }
            }
        }

        private static Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> BuildAffectedMapCatalog(IEnumerable<WzImageProperty> roots)
        {
            Dictionary<int, List<ContextOwnedStageAffectedMapEntry>> byFieldId = new();
            foreach (WzImageProperty root in roots ?? Enumerable.Empty<WzImageProperty>())
            {
                AppendAffectedMapEntries(
                    root,
                    inherited: ContextOwnedStageAffectedMapRow.Empty,
                    byFieldId);
            }

            return byFieldId.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ContextOwnedStageAffectedMapEntry>)pair.Value
                    .OrderByDescending(static entry => entry.Priority)
                    .ToArray());
        }

        private static void AppendAffectedMapEntries(
            WzImageProperty property,
            ContextOwnedStageAffectedMapRow inherited,
            Dictionary<int, List<ContextOwnedStageAffectedMapEntry>> byFieldId)
        {
            if (property == null)
            {
                return;
            }

            ContextOwnedStageAffectedMapRow row = inherited.Merge(property);
            int fieldId = row.ResolveFieldId(property);
            if (fieldId > 0 && !string.IsNullOrWhiteSpace(row.StageKeyword))
            {
                if (!byFieldId.TryGetValue(fieldId, out List<ContextOwnedStageAffectedMapEntry> entries))
                {
                    entries = new List<ContextOwnedStageAffectedMapEntry>();
                    byFieldId[fieldId] = entries;
                }

                entries.Add(row.CreateEntry(fieldId));
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                AppendAffectedMapEntries(child, row, byFieldId);
            }
        }

        private static void ApplyStageKeywordAugmentations(
            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes,
            IEnumerable<WzImageProperty> stageKeywordRoots)
        {
            foreach (WzImageProperty themeProperty in stageKeywordRoots ?? Enumerable.Empty<WzImageProperty>())
            {
                if (!themes.TryGetValue(themeProperty.Name, out ContextOwnedStageThemeCatalogEntry theme))
                {
                    continue;
                }

                HashSet<string> themeWideKeywords = ParseThemeWideKeywordAugmentations(themeProperty);
                foreach (ContextOwnedStagePeriodCatalogEntry period in theme.Periods)
                {
                    period.Keywords.UnionWith(themeWideKeywords);
                }

                Dictionary<byte, HashSet<string>> periodKeywords = BuildPeriodKeywordAugmentations(themeProperty);
                foreach (ContextOwnedStagePeriodCatalogEntry period in theme.Periods)
                {
                    if (periodKeywords.TryGetValue(period.Mode, out HashSet<string> extraKeywords))
                    {
                        period.Keywords.UnionWith(extraKeywords);
                    }
                }
            }
        }

        private static HashSet<string> ParseThemeWideKeywordAugmentations(WzImageProperty themeProperty)
        {
            HashSet<string> values = new(StringComparer.Ordinal);
            if (themeProperty == null)
            {
                return values;
            }

            if (themeProperty["stageKeyword"] is WzImageProperty stageKeywordProperty)
            {
                AppendStringValues(stageKeywordProperty, values);
                return values;
            }

            foreach (WzImageProperty child in themeProperty.WzProperties.OfType<WzImageProperty>())
            {
                if (byte.TryParse(child.Name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                AppendStringValues(child, values);
            }

            return values;
        }

        private static Dictionary<byte, HashSet<string>> BuildPeriodKeywordAugmentations(WzImageProperty themeProperty)
        {
            Dictionary<byte, HashSet<string>> periodKeywords = new();
            foreach ((byte mode, WzImageProperty periodProperty) in EnumeratePeriodNodes(themeProperty))
            {
                HashSet<string> extraKeywords = ParseStringSet(periodProperty["stageKeyword"] ?? periodProperty);
                if (extraKeywords.Count > 0)
                {
                    periodKeywords[mode] = extraKeywords;
                }
            }

            return periodKeywords;
        }

        private static void ApplyAffectedMapAugmentations(
            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes,
            Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> affectedMapsByFieldId)
        {
            foreach (ContextOwnedStageThemeCatalogEntry theme in themes.Values)
            {
                foreach (ContextOwnedStagePeriodCatalogEntry period in theme.Periods)
                {
                    foreach ((int fieldId, IReadOnlyList<ContextOwnedStageAffectedMapEntry> entries) in affectedMapsByFieldId)
                    {
                        if (entries.Any(entry => entry.RandomTimeSeconds <= 0 && entry.Matches(period)))
                        {
                            period.AffectedMapIds.Add(fieldId);
                        }
                    }
                }
            }
        }

        private static bool TryLoadImage(string path, out WzImage image)
        {
            image = null;
            if (!TrySplitPath(path, out string category, out string imageName))
            {
                return false;
            }

            image = Program.FindImage(category, imageName);
            return image != null;
        }

        private static ContextOwnedStageSystemCatalog BuildCore(
            string stageSystemPath,
            WzImageProperty stageSystemRoot,
            string stageKeywordPath,
            WzImageProperty stageKeywordRoot,
            WzImageProperty stageAffectedMapRoot,
            bool requireStageKeywordImage,
            out string error)
        {
            error = null;
            if (stageSystemRoot == null)
            {
                error = $"Context-owned stage-period validation could not load {stageSystemPath}, so the simulator cannot mirror CStageSystem::BuildCacheData acceptance yet.";
                return null;
            }

            if (requireStageKeywordImage && stageKeywordRoot == null)
            {
                error = $"Context-owned stage-period validation could not load {stageKeywordPath}, so the simulator cannot mirror CStageSystem::IterateStageSystemClient acceptance yet.";
                return null;
            }

            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes = BuildThemes(stageSystemRoot.WzProperties.OfType<WzImageProperty>());
            if (themes.Count == 0)
            {
                error = $"Context-owned stage-period validation loaded {stageSystemPath}, but no client stage themes with concrete period entries were discovered.";
                return null;
            }

            if (stageKeywordRoot != null)
            {
                ApplyStageKeywordAugmentations(themes, stageKeywordRoot.WzProperties.OfType<WzImageProperty>());
            }

            Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> affectedMapsByFieldId = stageAffectedMapRoot != null
                ? BuildAffectedMapCatalog(stageAffectedMapRoot.WzProperties.OfType<WzImageProperty>())
                : new Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>>();
            ApplyAffectedMapAugmentations(themes, affectedMapsByFieldId);
            return new ContextOwnedStageSystemCatalog(themes, affectedMapsByFieldId);
        }

        private static ContextOwnedStageSystemCatalog BuildCore(
            string stageSystemPath,
            WzImage stageSystemImage,
            string stageKeywordPath,
            WzImage stageKeywordImage,
            WzImage stageAffectedMapImage,
            bool requireStageKeywordImage,
            out string error)
        {
            stageSystemImage?.ParseImage();
            stageKeywordImage?.ParseImage();
            stageAffectedMapImage?.ParseImage();
            return BuildCore(
                stageSystemPath,
                stageSystemImage,
                stageKeywordPath,
                stageKeywordImage,
                stageAffectedMapImage,
                requireStageKeywordImage,
                out error);
        }

        private static bool TrySplitPath(string path, out string category, out string imageName)
        {
            category = null;
            imageName = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            category = parts[0];
            imageName = parts[^1];
            return !string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(imageName);
        }

        private static void ApplyThemeScopedEnableState<TKey>(
            string stageTheme,
            IEnumerable<TKey> enabledKeys,
            IDictionary<TKey, ContextOwnedStageUnitEnableState> cache)
        {
            if (cache == null || string.IsNullOrWhiteSpace(stageTheme))
            {
                return;
            }

            foreach ((TKey key, ContextOwnedStageUnitEnableState state) in cache.ToArray())
            {
                if (state != null && string.Equals(state.StageTheme, stageTheme, StringComparison.Ordinal))
                {
                    state.Enabled = false;
                }
            }

            foreach (TKey key in enabledKeys ?? Enumerable.Empty<TKey>())
            {
                if (!cache.TryGetValue(key, out ContextOwnedStageUnitEnableState state) || state == null)
                {
                    state = new ContextOwnedStageUnitEnableState(stageTheme, enabled: true);
                    cache[key] = state;
                    continue;
                }

                state.StageTheme = stageTheme;
                state.Enabled = true;
            }
        }

        private static HashSet<TKey> CaptureEnabledValues<TKey>(IDictionary<TKey, ContextOwnedStageUnitEnableState> cache)
        {
            HashSet<TKey> enabledValues = new();
            if (cache == null)
            {
                return enabledValues;
            }

            foreach ((TKey key, ContextOwnedStageUnitEnableState state) in cache)
            {
                if (state?.Enabled == true)
                {
                    enabledValues.Add(key);
                }
            }

            return enabledValues;
        }
    }

    internal sealed class ContextOwnedStageThemeCatalogEntry
    {
        private readonly Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> _periods;

        internal ContextOwnedStageThemeCatalogEntry(string themeName, Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> periods)
        {
            ThemeName = themeName ?? string.Empty;
            _periods = periods ?? new Dictionary<byte, ContextOwnedStagePeriodCatalogEntry>();
        }

        internal string ThemeName { get; }

        internal IEnumerable<ContextOwnedStagePeriodCatalogEntry> Periods => _periods.Values;

        internal bool TryGetPeriod(byte mode, out ContextOwnedStagePeriodCatalogEntry entry)
        {
            return _periods.TryGetValue(mode, out entry);
        }
    }

    internal sealed class ContextOwnedStagePeriodCatalogEntry
    {
        internal ContextOwnedStagePeriodCatalogEntry(
            string stageTheme,
            byte mode,
            uint? backColorArgb,
            IReadOnlyList<ContextOwnedStageBackImageEntry> backImages,
            HashSet<string> keywords,
            HashSet<int> enabledQuestIds,
            HashSet<int> affectedMapIds)
        {
            StageTheme = stageTheme ?? string.Empty;
            Mode = mode;
            BackColorArgb = backColorArgb;
            BackImages = backImages ?? Array.Empty<ContextOwnedStageBackImageEntry>();
            Keywords = keywords ?? new HashSet<string>(StringComparer.Ordinal);
            EnabledQuestIds = enabledQuestIds ?? new HashSet<int>();
            AffectedMapIds = affectedMapIds ?? new HashSet<int>();
        }

        internal string StageTheme { get; }
        internal byte Mode { get; }
        internal uint? BackColorArgb { get; }
        internal IReadOnlyList<ContextOwnedStageBackImageEntry> BackImages { get; }
        internal HashSet<string> Keywords { get; }
        internal HashSet<int> EnabledQuestIds { get; }
        internal HashSet<int> AffectedMapIds { get; }

        internal uint? ResolveActiveBackColorArgb()
        {
            return Mode > 0 ? BackColorArgb : null;
        }

        internal IReadOnlyList<ContextOwnedStageBackImageEntry> ResolveActiveBackImages()
        {
            return Mode > 0 ? BackImages : Array.Empty<ContextOwnedStageBackImageEntry>();
        }
    }

    internal sealed record ContextOwnedStageBackImageEntry(
        string BackgroundSet,
        string Number,
        BackgroundInfoType InfoType,
        int X,
        int Y,
        int Rx,
        int Ry,
        int Cx,
        int Cy,
        int Alpha,
        BackgroundType Type,
        bool Front,
        bool Flip,
        int Page,
        int ScreenMode,
        int Z,
        string SpineAnimation,
        bool SpineRandomStart);

    internal sealed class ContextOwnedStageUnitEnableState
    {
        internal ContextOwnedStageUnitEnableState(string stageTheme, bool enabled)
        {
            StageTheme = stageTheme ?? string.Empty;
            Enabled = enabled;
        }

        internal string StageTheme { get; set; }
        internal bool Enabled { get; set; }
    }

    internal readonly record struct ContextOwnedStageAffectedMapRow(
        string StageKeyword,
        int? Priority,
        int? QuestId,
        int? QuestState,
        bool HasQuestStateGate,
        int? RandomTimeSeconds)
    {
        internal static ContextOwnedStageAffectedMapRow Empty { get; } = new(
            StageKeyword: null,
            Priority: null,
            QuestId: null,
            QuestState: null,
            HasQuestStateGate: false,
            RandomTimeSeconds: null);

        internal ContextOwnedStageAffectedMapRow Merge(WzImageProperty property)
        {
            string stageKeyword = ReadString(property["stageKeyword"]) ?? StageKeyword;
            int? priority = ReadInt(property["priority"]) ?? Priority;
            int? questId = ReadInt(property["questID"]) ?? QuestId;
            int? questState = ReadInt(property["questState"]) ?? QuestState;
            int? randomTimeSeconds = ReadInt(property["randTime"]) ?? RandomTimeSeconds;
            return new ContextOwnedStageAffectedMapRow(
                stageKeyword,
                priority,
                questId,
                questState,
                HasQuestStateGate || property["questState"] != null,
                randomTimeSeconds);
        }

        internal int ResolveFieldId(WzImageProperty property)
        {
            int? currentFieldId = ReadInt(property?["fieldID"]);
            if (currentFieldId.HasValue)
            {
                return currentFieldId.Value;
            }

            if (property is WzIntProperty intProperty
                && int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return intProperty.Value;
            }

            return property != null
                && int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : int.MinValue;
        }

        internal ContextOwnedStageAffectedMapEntry CreateEntry(int fieldId)
        {
            return new ContextOwnedStageAffectedMapEntry(
                fieldId,
                StageKeyword.Trim(),
                Priority.GetValueOrDefault(),
                QuestId.GetValueOrDefault(),
                QuestState.GetValueOrDefault(),
                HasQuestStateGate,
                RandomTimeSeconds.GetValueOrDefault());
        }

        private static string ReadString(WzImageProperty property)
        {
            string text = InfoTool.GetString(property);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static int? ReadInt(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property is WzIntProperty intProperty)
            {
                return intProperty.Value;
            }

            string text = InfoTool.GetString(property);
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : null;
        }
    }

    internal sealed record ContextOwnedStageAffectedMapEntry(
        int FieldId,
        string StageKeyword,
        int Priority,
        int QuestId,
        int QuestState,
        bool HasQuestStateGate,
        int RandomTimeSeconds)
    {
        internal bool Matches(
            ContextOwnedStagePeriodCatalogEntry period,
            Func<int, QuestStateType> questStateProvider = null,
            int elapsedStagePeriodMilliseconds = 0)
        {
            if (period == null || string.IsNullOrWhiteSpace(StageKeyword) || !period.Keywords.Contains(StageKeyword))
            {
                return false;
            }

            if (RandomTimeSeconds > 0
                && elapsedStagePeriodMilliseconds < RandomTimeSeconds * 1000)
            {
                return false;
            }

            if (QuestId <= 0)
            {
                return true;
            }

            if (!period.EnabledQuestIds.Contains(QuestId))
            {
                return false;
            }

            return !HasQuestStateGate
                || questStateProvider != null
                && ItemMakerQuestRequirementPolicy.MatchesClientQuestRequirement(
                    questStateProvider(QuestId),
                    QuestState);
        }
    }

    internal static class ContextOwnedStagePeriodColorHelper
    {
        internal static Color Resolve(uint colorValue)
        {
            byte a = (byte)((colorValue >> 24) & 0xFF);
            byte r = (byte)((colorValue >> 16) & 0xFF);
            byte g = (byte)((colorValue >> 8) & 0xFF);
            byte b = (byte)(colorValue & 0xFF);
            return new Color(r, g, b, a == 0 ? byte.MaxValue : a);
        }
    }
}
