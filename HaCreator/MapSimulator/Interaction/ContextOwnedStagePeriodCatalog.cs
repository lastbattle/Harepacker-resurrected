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
        private const int StageBackXStringPoolId = 0x03E5;
        private const int StageBackYStringPoolId = 0x03E6;
        private const int MapBackBackgroundSetStringPoolId = 0x0610;
        private const int MapBackRxStringPoolId = 0x0612;
        private const int MapBackRyStringPoolId = 0x0613;
        private const int MapBackCxStringPoolId = 0x0614;
        private const int MapBackCyStringPoolId = 0x0615;
        private const int MapBackAlphaStringPoolId = 0x0617;
        private const int MapBackTypeStringPoolId = 0x0618;
        private const int StageBackAbsRxStringPoolId = 0x17F1;
        private const int StageBackAbsRyStringPoolId = 0x17F2;
        private static readonly HashSet<string> KeywordStructuralAliasNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "stageKeyword",
            "keyword",
            "aKeyword",
            "enabledQuest",
            "aEnabledQuest",
            "questID",
            "questId",
            "quest",
            "questState",
            "priority",
            "randTime",
            "affectedMap",
            "fieldID",
            "fieldId",
            "aAffectedMap",
            "stage",
            "stageList"
        };

        private readonly Dictionary<string, ContextOwnedStageThemeCatalogEntry> _themes;
        private readonly Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> _affectedMapsByFieldId;

        private ContextOwnedStageSystemCatalog(
            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes,
            Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> affectedMapsByFieldId)
        {
            _themes = themes ?? new Dictionary<string, ContextOwnedStageThemeCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            _affectedMapsByFieldId = affectedMapsByFieldId ?? new Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>>();
        }

        internal bool TryGetPeriod(string stageTheme, byte mode, out ContextOwnedStagePeriodCatalogEntry entry)
        {
            entry = null;
            string normalizedTheme = NormalizeStageTheme(stageTheme);
            if (string.IsNullOrWhiteSpace(normalizedTheme)
                || !_themes.TryGetValue(normalizedTheme, out ContextOwnedStageThemeCatalogEntry theme))
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
                if (MatchesAffectedMapEntries(
                    entries,
                    entry,
                    questStateProvider,
                    elapsedStagePeriodMilliseconds))
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
            ApplyStagePeriodModeCache(entry.StageTheme, entry.Mode, stagePeriodCache);
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

            TryLoadImage(stageKeywordPath, out WzImage stageKeywordImage);
            WzImage stageAffectedMapImage = null;
            TryLoadImage(stageAffectedMapPath, out stageAffectedMapImage);
            return BuildCore(
                stageSystemPath,
                stageSystemImage,
                stageKeywordPath,
                stageKeywordImage,
                stageAffectedMapImage,
                requireStageKeywordImage: false,
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
            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes = new(StringComparer.OrdinalIgnoreCase);
            if (themeProperties == null)
            {
                return themes;
            }

            HashSet<WzImageProperty> visited = new();
            foreach (WzImageProperty themeProperty in themeProperties)
            {
                AppendThemeEntries(themeProperty, themes, visited);
            }

            return themes;
        }

        private static void AppendThemeEntries(
            WzImageProperty property,
            IDictionary<string, ContextOwnedStageThemeCatalogEntry> themes,
            ISet<WzImageProperty> visited)
        {
            if (property == null
                || themes == null
                || visited == null
                || !visited.Add(property))
            {
                return;
            }

            Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> periods = BuildPeriods(property);
            if (periods.Count > 0
                && !IsStructuralStageAliasName(property.Name))
            {
                themes[property.Name.Trim()] = new ContextOwnedStageThemeCatalogEntry(property.Name.Trim(), periods);
                return;
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                AppendThemeEntries(child, themes, visited);
            }
        }

        private static Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> BuildPeriods(WzImageProperty themeProperty)
        {
            Dictionary<byte, ContextOwnedStagePeriodCatalogEntry> periods = new();
            foreach ((byte mode, WzImageProperty periodNode) in EnumeratePeriodNodes(themeProperty))
            {
                WzImageProperty[] keywordBranches = ResolveAliasBranches(
                    periodNode,
                    "stageKeyword",
                    "keyword",
                    "aKeyword");
                WzImageProperty[] questBranches = ResolveAliasBranches(
                    periodNode,
                    "enabledQuest",
                    "aEnabledQuest",
                    "questID",
                    "questId",
                    "quest");
                WzImageProperty[] affectedMapBranches = ResolveAliasBranches(
                    periodNode,
                    "affectedMap",
                    "fieldID",
                    "fieldId",
                    "aAffectedMap");
                periods[mode] = new ContextOwnedStagePeriodCatalogEntry(
                    themeProperty.Name,
                    mode,
                    TryReadStageBackColor(periodNode),
                    ParseStageBackImages(periodNode),
                    ParseStringSet(keywordBranches),
                    ParseIntSet(questBranches),
                    ParseIntSet(affectedMapBranches));
            }

            return periods;
        }

        private static IEnumerable<(byte Mode, WzImageProperty Property)> EnumeratePeriodNodes(WzImageProperty themeProperty)
        {
            if (GetChildProperty(themeProperty, "stage") is WzImageProperty stageProperty)
            {
                foreach ((byte mode, WzImageProperty property) in EnumerateIndexedPeriodNodes(stageProperty))
                {
                    yield return (mode, property);
                }
                yield break;
            }

            if (GetChildProperty(themeProperty, "stageList") is WzImageProperty stageList)
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
            Dictionary<byte, WzImageProperty> discovered = new();
            CollectIndexedPeriodNodes(container, discovered, new HashSet<WzImageProperty>());
            foreach (byte mode in discovered.Keys.OrderBy(static value => value))
            {
                yield return (mode, discovered[mode]);
            }
        }

        private static void CollectIndexedPeriodNodes(
            WzImageProperty container,
            IDictionary<byte, WzImageProperty> discovered,
            ISet<WzImageProperty> visited)
        {
            if (container == null
                || discovered == null
                || visited == null
                || !visited.Add(container))
            {
                return;
            }

            foreach (WzImageProperty child in container.WzProperties.OfType<WzImageProperty>())
            {
                if (byte.TryParse(child.Name, NumberStyles.None, CultureInfo.InvariantCulture, out byte mode))
                {
                    // `CStageSystem::IterateStageSystemClient` uses zero-based numeric names as mode keys.
                    discovered.TryAdd(mode, child);
                }
            }

            foreach (WzImageProperty child in container.WzProperties.OfType<WzImageProperty>())
            {
                if (byte.TryParse(child.Name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                if (!IsStructuralStageAliasName(child.Name) && ContainsConcretePeriodPayload(child))
                {
                    continue;
                }

                CollectIndexedPeriodNodes(child, discovered, visited);
            }
        }

        private static bool ContainsConcretePeriodPayload(WzImageProperty property)
        {
            if (property == null)
            {
                return false;
            }

            return ResolveAliasBranches(
                    property,
                    "stageKeyword",
                    "keyword",
                    "aKeyword",
                    "enabledQuest",
                    "aEnabledQuest",
                    "questID",
                    "questId",
                    "quest",
                    "affectedMap",
                    "fieldID",
                    "fieldId",
                    "aAffectedMap",
                    "backColor",
                    "aStageBackImg",
                    "stageBackImg",
                    "backImg")
                .Length > 0;
        }

        private static uint? TryReadStageBackColor(WzImageProperty periodNode)
        {
            int rawColor = -1;
            foreach (WzImageProperty branch in ResolveAliasBranches(periodNode, "backColor"))
            {
                int? candidate = TryReadNestedInt(branch);
                if (candidate.HasValue)
                {
                    rawColor = candidate.Value;
                    break;
                }
            }

            return rawColor == -1
                ? null
                : unchecked((uint)rawColor);
        }

        private static IReadOnlyList<ContextOwnedStageBackImageEntry> ParseStageBackImages(WzImageProperty periodNode)
        {
            List<ContextOwnedStageBackImageEntry> entries = new();
            WzImageProperty[] containers = ResolveAliasBranches(periodNode, "aStageBackImg", "stageBackImg", "backImg", "back");

            if (containers.Length == 0)
            {
                if (TryParseStageBackImageEntry(periodNode, out ContextOwnedStageBackImageEntry directEntry))
                {
                    entries.Add(directEntry);
                }

                return entries;
            }

            foreach (WzImageProperty container in containers)
            {
                foreach (WzImageProperty child in container.WzProperties.OfType<WzImageProperty>())
                {
                    if (TryParseStageBackImageEntry(child, out ContextOwnedStageBackImageEntry entry))
                    {
                        entries.Add(entry);
                        continue;
                    }

                    AppendNativeStageBackImageEntries(child, entries);
                }
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
            AppendNativeStageBackImageEntries(backgroundSet, GetChildProperty(stageBackImageGroup, "back"), front: false, entries);
            AppendNativeStageBackImageEntries(backgroundSet, GetChildProperty(stageBackImageGroup, "front"), front: true, entries);
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
            if (string.Equals(name, "back", StringComparison.OrdinalIgnoreCase))
            {
                front = false;
                return true;
            }

            if (string.Equals(name, "front", StringComparison.OrdinalIgnoreCase))
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
                || !TryParseClientStageBackObjectKey(property.Name, out int number))
            {
                return false;
            }

            ContextOwnedStageBackImageEntry entry = new(
                backgroundSet.Trim(),
                number.ToString(CultureInfo.InvariantCulture),
                BackgroundInfoType.Background,
                ReadClientInt(property, StageBackXStringPoolId, "x"),
                ReadClientInt(property, StageBackYStringPoolId, "y"),
                ReadClientInt(property, StageBackAbsRxStringPoolId, "absRX", 1),
                ReadClientInt(property, StageBackAbsRyStringPoolId, "absRY", 1),
                0,
                0,
                255,
                BackgroundType.Regular,
                front,
                false,
                0,
                0,
                0,
                null,
                false,
                UseSourceBackPieceFields: true);
            UpsertNativeStageBackImageEntry(entries, entry);
            return true;
        }

        internal static ContextOwnedStageBackImageEntry ResolveClientMakeBackPieceFields(
            ContextOwnedStageBackImageEntry entry,
            WzImageProperty sourceProperty)
        {
            if (entry == null || sourceProperty == null || !entry.UseSourceBackPieceFields)
            {
                return entry;
            }

            string spineAnimation = ReadStringWithFallback(sourceProperty, "spineAni");
            bool animated = ReadBoolWithFallback(sourceProperty, "ani", defaultValue: false);
            BackgroundInfoType infoType = !string.IsNullOrWhiteSpace(spineAnimation)
                ? BackgroundInfoType.Spine
                : animated
                    ? BackgroundInfoType.Animation
                    : entry.InfoType;
            MapleBool flipValue = InfoTool.GetOptionalBool(GetChildProperty(sourceProperty, "f"));
            WzImageProperty frontProperty = GetChildProperty(sourceProperty, "front");

            return entry with
            {
                InfoType = infoType,
                Cx = ReadClientInt(sourceProperty, MapBackCxStringPoolId, "cx", entry.Cx),
                Cy = ReadClientInt(sourceProperty, MapBackCyStringPoolId, "cy", entry.Cy),
                Alpha = Math.Clamp(ReadClientInt(sourceProperty, MapBackAlphaStringPoolId, "a", entry.Alpha), 0, 255),
                Type = (BackgroundType)ReadClientInt(sourceProperty, MapBackTypeStringPoolId, "type", (int)entry.Type),
                Front = frontProperty == null ? entry.Front : InfoTool.GetBool(frontProperty),
                Flip = flipValue.HasValue ? flipValue.Value : entry.Flip,
                Page = ReadIntWithFallback(sourceProperty, "page", entry.Page),
                ScreenMode = ReadIntWithFallback(sourceProperty, "screenMode", entry.ScreenMode),
                Z = ReadIntWithFallback(sourceProperty, "z", entry.Z),
                SpineAnimation = string.IsNullOrWhiteSpace(spineAnimation) ? entry.SpineAnimation : spineAnimation,
                SpineRandomStart = ReadBoolWithFallback(sourceProperty, "spineRandomStart", entry.SpineRandomStart)
            };
        }

        internal static IReadOnlyList<BackgroundInfoType> ResolveClientMakeBackInfoTypeLookupOrder(
            ContextOwnedStageBackImageEntry entry)
        {
            if (entry == null)
            {
                return Array.Empty<BackgroundInfoType>();
            }

            if (!entry.UseSourceBackPieceFields)
            {
                return new[] { entry.InfoType };
            }

            BackgroundInfoType[] clientPieceFamilies =
            {
                entry.InfoType,
                BackgroundInfoType.Spine,
                BackgroundInfoType.Animation,
                BackgroundInfoType.Background
            };
            return clientPieceFamilies
                .Distinct()
                .ToArray();
        }

        private static void UpsertNativeStageBackImageEntry(
            List<ContextOwnedStageBackImageEntry> entries,
            ContextOwnedStageBackImageEntry entry)
        {
            if (entries == null || entry == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ContextOwnedStageBackImageEntry existing = entries[i];
                if (existing.Front == entry.Front
                    && string.Equals(existing.BackgroundSet, entry.BackgroundSet, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Number, entry.Number, StringComparison.OrdinalIgnoreCase))
                {
                    entries[i] = entry;
                    return;
                }
            }

            entries.Add(entry);
        }

        private static bool TryParseClientStageBackObjectKey(string name, out int number)
        {
            number = 0;
            if (name == null)
            {
                return false;
            }

            string trimmed = name.TrimStart();
            int index = 0;
            int sign = 1;
            if (trimmed.Length > 0 && (trimmed[0] == '+' || trimmed[0] == '-'))
            {
                sign = trimmed[0] == '-' ? -1 : 1;
                index = 1;
            }

            long value = 0;
            bool hasDigit = false;
            while (index < trimmed.Length && char.IsDigit(trimmed[index]))
            {
                hasDigit = true;
                value = value * 10 + (trimmed[index] - '0');
                long signedValue = value * sign;
                if (signedValue > int.MaxValue || signedValue < int.MinValue)
                {
                    number = signedValue > 0 ? int.MaxValue : int.MinValue;
                    return true;
                }

                index++;
            }

            if (!hasDigit)
            {
                // `LoadStageBackImgInfo` uses `_wtoi`; non-numeric names become key 0.
                number = 0;
                return true;
            }

            number = (int)(value * sign);
            return true;
        }

        private static bool TryParseStageBackImageEntry(WzImageProperty property, out ContextOwnedStageBackImageEntry entry)
        {
            entry = null;
            if (property == null)
            {
                return false;
            }

            WzImageProperty backImgInfo = GetChildProperty(property, "backImgInfo");
            string backgroundSet = ReadClientString(property, MapBackBackgroundSetStringPoolId, "bS", backImgInfo);
            string number = ResolveBackgroundNumber(property) ?? ResolveBackgroundNumber(backImgInfo);
            if (string.IsNullOrWhiteSpace(backgroundSet) || string.IsNullOrWhiteSpace(number))
            {
                return false;
            }

            string spineAnimation = ReadStringWithFallback(property, "spineAni", backImgInfo);
            bool animated = ReadBoolWithFallback(property, "ani", defaultValue: false, backImgInfo);
            BackgroundInfoType infoType = !string.IsNullOrWhiteSpace(spineAnimation)
                ? BackgroundInfoType.Spine
                : animated
                    ? BackgroundInfoType.Animation
                    : BackgroundInfoType.Background;

            MapleBool flipValue = InfoTool.GetOptionalBool(GetChildProperty(property, "f"));
            if (!flipValue.HasValue)
            {
                flipValue = InfoTool.GetOptionalBool(GetChildProperty(backImgInfo, "f"));
            }
            entry = new ContextOwnedStageBackImageEntry(
                backgroundSet,
                number,
                infoType,
                ReadClientInt(property, StageBackXStringPoolId, "x", secondaryProperty: backImgInfo),
                ReadClientInt(property, StageBackYStringPoolId, "y", secondaryProperty: backImgInfo),
                ReadClientInt(property, MapBackRxStringPoolId, "rx", secondaryProperty: backImgInfo),
                ReadClientInt(property, MapBackRyStringPoolId, "ry", secondaryProperty: backImgInfo),
                ReadClientInt(property, MapBackCxStringPoolId, "cx", secondaryProperty: backImgInfo),
                ReadClientInt(property, MapBackCyStringPoolId, "cy", secondaryProperty: backImgInfo),
                Math.Clamp(ReadClientInt(property, MapBackAlphaStringPoolId, "a", 255, backImgInfo), 0, 255),
                (BackgroundType)ReadClientInt(property, MapBackTypeStringPoolId, "type", secondaryProperty: backImgInfo),
                ReadBoolWithFallback(property, "front", defaultValue: false, backImgInfo),
                flipValue.HasValue && flipValue.Value,
                ReadIntWithFallback(property, "page", defaultValue: 0, backImgInfo),
                ReadIntWithFallback(property, "screenMode", defaultValue: 0, backImgInfo),
                ReadIntWithFallback(property, "z", defaultValue: 0, backImgInfo),
                spineAnimation,
                ReadBoolWithFallback(property, "spineRandomStart", defaultValue: false, backImgInfo));
            return true;
        }

        private static string ResolveBackgroundNumber(WzImageProperty property)
        {
            WzImageProperty numberProperty = GetChildProperty(property, "no");
            if (numberProperty == null)
            {
                return null;
            }

            string text = InfoTool.GetString(numberProperty);
            return !string.IsNullOrWhiteSpace(text)
                ? text
                : InfoTool.GetInt(numberProperty).ToString(CultureInfo.InvariantCulture);
        }

        private static int ReadClientInt(
            WzImageProperty property,
            int stringPoolId,
            string fallbackName,
            int defaultValue = 0,
            WzImageProperty secondaryProperty = null)
        {
            string propertyName = ResolveClientPropertyName(stringPoolId, fallbackName);
            WzImageProperty valueProperty = ResolveFirstProperty(property, propertyName, fallbackName)
                ?? ResolveFirstProperty(secondaryProperty, propertyName, fallbackName);
            return InfoTool.GetInt(valueProperty, defaultValue);
        }

        private static string ReadClientString(
            WzImageProperty property,
            int stringPoolId,
            string fallbackName,
            WzImageProperty secondaryProperty = null)
        {
            string propertyName = ResolveClientPropertyName(stringPoolId, fallbackName);
            WzImageProperty valueProperty = ResolveFirstProperty(property, propertyName, fallbackName)
                ?? ResolveFirstProperty(secondaryProperty, propertyName, fallbackName);
            return InfoTool.GetString(valueProperty);
        }

        private static int ReadIntWithFallback(
            WzImageProperty property,
            string name,
            int defaultValue = 0,
            WzImageProperty secondaryProperty = null)
        {
            WzImageProperty valueProperty = ResolveFirstProperty(property, name)
                ?? ResolveFirstProperty(secondaryProperty, name);
            return InfoTool.GetInt(valueProperty, defaultValue);
        }

        private static string ReadStringWithFallback(
            WzImageProperty property,
            string name,
            WzImageProperty secondaryProperty = null)
        {
            WzImageProperty valueProperty = ResolveFirstProperty(property, name)
                ?? ResolveFirstProperty(secondaryProperty, name);
            return InfoTool.GetString(valueProperty);
        }

        private static bool ReadBoolWithFallback(
            WzImageProperty property,
            string name,
            bool defaultValue = false,
            WzImageProperty secondaryProperty = null)
        {
            WzImageProperty sourceProperty = ResolveFirstProperty(property, name)
                ?? ResolveFirstProperty(secondaryProperty, name);
            return sourceProperty == null
                ? defaultValue
                : InfoTool.GetBool(sourceProperty);
        }

        private static WzImageProperty ResolveFirstProperty(WzImageProperty parent, params string[] names)
        {
            if (parent == null || names == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                WzImageProperty property = GetChildProperty(parent, name);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static string ResolveClientPropertyName(int stringPoolId, string fallbackName)
        {
            return MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackName);
        }

        private static WzImageProperty[] ResolveAliasBranches(WzImageProperty root, params string[] aliases)
        {
            if (root == null || aliases == null || aliases.Length == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            HashSet<string> aliasSet = new(aliases.Where(static item => !string.IsNullOrWhiteSpace(item)), StringComparer.OrdinalIgnoreCase);
            if (aliasSet.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            List<WzImageProperty> matches = new();
            HashSet<WzImageProperty> visited = new();
            CollectAliasBranches(root, aliasSet, matches, visited);
            return matches.ToArray();
        }

        private static void CollectAliasBranches(
            WzImageProperty property,
            IReadOnlySet<string> aliases,
            List<WzImageProperty> matches,
            ISet<WzImageProperty> visited)
        {
            if (property == null
                || aliases == null
                || matches == null
                || visited == null
                || !visited.Add(property))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(property.Name) && aliases.Contains(property.Name.Trim()))
            {
                matches.Add(property);
                return;
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                CollectAliasBranches(child, aliases, matches, visited);
            }
        }

        private static int? TryReadNestedInt(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property is WzIntProperty intProperty)
            {
                return intProperty.Value;
            }

            string valueText = InfoTool.GetString(property);
            if (int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                int? nestedValue = TryReadNestedInt(child);
                if (nestedValue.HasValue)
                {
                    return nestedValue;
                }
            }

            return null;
        }

        private static HashSet<string> ParseStringSet(params WzImageProperty[] properties)
        {
            HashSet<string> values = new(StringComparer.OrdinalIgnoreCase);
            if (properties == null)
            {
                return values;
            }

            foreach (WzImageProperty property in properties)
            {
                AppendStringValues(property, values);
            }

            return values;
        }

        private static void AppendStringValues(WzImageProperty property, HashSet<string> values)
        {
            if (property == null)
            {
                return;
            }

            CollectStringValues(property, values);
        }

        private static bool CollectStringValues(WzImageProperty property, HashSet<string> values)
        {
            if (property == null || values == null)
            {
                return false;
            }

            bool hasValue = false;
            string directValue = property is WzStringProperty stringProperty
                ? stringProperty.Value
                : InfoTool.GetString(property);
            if (!string.IsNullOrWhiteSpace(directValue))
            {
                values.Add(directValue.Trim());
                hasValue = true;
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                bool childHasValue = CollectStringValues(child, values);
                if (!childHasValue
                    && !string.IsNullOrWhiteSpace(child.Name)
                    && !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                    && !KeywordStructuralAliasNames.Contains(child.Name))
                {
                    values.Add(child.Name.Trim());
                    hasValue = true;
                    continue;
                }

                hasValue |= childHasValue;
            }

            return hasValue;
        }

        private static HashSet<int> ParseIntSet(params WzImageProperty[] properties)
        {
            HashSet<int> values = new();
            if (properties == null)
            {
                return values;
            }

            foreach (WzImageProperty property in properties)
            {
                AppendIntValues(property, values);
            }

            return values;
        }

        private static void AppendIntValues(WzImageProperty property, HashSet<int> values)
        {
            if (property == null)
            {
                return;
            }

            CollectIntValues(property, values);
        }

        private static bool CollectIntValues(WzImageProperty property, HashSet<int> values)
        {
            if (property == null || values == null)
            {
                return false;
            }

            bool hasValue = false;
            if (property is WzIntProperty intProperty)
            {
                values.Add(intProperty.Value);
                hasValue = true;
            }
            else
            {
                string direct = InfoTool.GetString(property);
                if (int.TryParse(direct, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedDirect))
                {
                    values.Add(parsedDirect);
                    hasValue = true;
                }
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                bool childHasValue = CollectIntValues(child, values);
                if (!childHasValue
                    && int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    values.Add(parsed);
                    hasValue = true;
                    continue;
                }

                hasValue |= childHasValue;
            }

            return hasValue;
        }

        private static Dictionary<int, IReadOnlyList<ContextOwnedStageAffectedMapEntry>> BuildAffectedMapCatalog(IEnumerable<WzImageProperty> roots)
        {
            Dictionary<int, List<ContextOwnedStageAffectedMapEntry>> byFieldId = new();
            foreach (WzImageProperty root in roots ?? Enumerable.Empty<WzImageProperty>())
            {
                AppendAffectedMapEntries(
                    root,
                    inherited: ContextOwnedStageAffectedMapRow.Empty,
                    inheritedFieldId: int.MinValue,
                    allowNumericFieldIdFallback: true,
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
            int inheritedFieldId,
            bool allowNumericFieldIdFallback,
            Dictionary<int, List<ContextOwnedStageAffectedMapEntry>> byFieldId)
        {
            if (property == null)
            {
                return;
            }

            ContextOwnedStageAffectedMapRow row = inherited.Merge(property);
            bool isFieldIdAliasBranch = ContextOwnedStageAffectedMapRow.IsFieldIdAliasName(property.Name);
            bool allowNumericFieldIdFallbackForCurrentProperty = isFieldIdAliasBranch
                || allowNumericFieldIdFallback
                && !ContextOwnedStageAffectedMapRow.IsNonFieldMetadataAliasName(property.Name);
            int fieldId = row.ResolveFieldId(property, allowNumericFallback: allowNumericFieldIdFallbackForCurrentProperty);
            if (fieldId <= 0)
            {
                fieldId = inheritedFieldId;
            }

            if (fieldId > 0
                && !string.IsNullOrWhiteSpace(row.StageKeyword)
                && row.IsStructurallyValidForRuntimeMatch())
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
                AppendAffectedMapEntries(
                    child,
                    row,
                    fieldId,
                    isFieldIdAliasBranch
                    || allowNumericFieldIdFallbackForCurrentProperty && fieldId <= 0,
                    byFieldId);
            }
        }

        private static void ApplyStageKeywordAugmentations(
            Dictionary<string, ContextOwnedStageThemeCatalogEntry> themes,
            IEnumerable<WzImageProperty> stageKeywordRoots)
        {
            Dictionary<string, List<WzImageProperty>> lookup = BuildStageKeywordThemeLookup(
                stageKeywordRoots,
                themes?.Keys ?? Enumerable.Empty<string>());
            foreach ((string themeName, ContextOwnedStageThemeCatalogEntry theme) in themes)
            {
                if (!lookup.TryGetValue(themeName, out List<WzImageProperty> themeProperties))
                {
                    continue;
                }

                foreach (WzImageProperty themeProperty in themeProperties)
                {
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
        }

        private static Dictionary<string, List<WzImageProperty>> BuildStageKeywordThemeLookup(
            IEnumerable<WzImageProperty> stageKeywordRoots,
            IEnumerable<string> themeNames)
        {
            HashSet<string> themeNameSet = new(
                themeNames ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<WzImageProperty>> lookup = new(StringComparer.OrdinalIgnoreCase);
            if (themeNameSet.Count == 0)
            {
                return lookup;
            }

            HashSet<WzImageProperty> visited = new();
            foreach (WzImageProperty root in stageKeywordRoots ?? Enumerable.Empty<WzImageProperty>())
            {
                AppendStageKeywordThemeLookupEntries(root, themeNameSet, lookup, visited);
            }

            return lookup;
        }

        private static void AppendStageKeywordThemeLookupEntries(
            WzImageProperty property,
            IReadOnlySet<string> themeNameSet,
            Dictionary<string, List<WzImageProperty>> lookup,
            ISet<WzImageProperty> visited)
        {
            if (property == null
                || themeNameSet == null
                || lookup == null
                || visited == null
                || !visited.Add(property))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(property.Name)
                && themeNameSet.Contains(property.Name.Trim()))
            {
                if (!lookup.TryGetValue(property.Name.Trim(), out List<WzImageProperty> entries))
                {
                    entries = new List<WzImageProperty>();
                    lookup[property.Name.Trim()] = entries;
                }

                entries.Add(property);
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                AppendStageKeywordThemeLookupEntries(child, themeNameSet, lookup, visited);
            }
        }

        private static HashSet<string> ParseThemeWideKeywordAugmentations(WzImageProperty themeProperty)
        {
            HashSet<string> values = new(StringComparer.OrdinalIgnoreCase);
            if (themeProperty == null)
            {
                return values;
            }

            AppendStringValues(GetChildProperty(themeProperty, "stageKeyword"), values);
            AppendStringValues(GetChildProperty(themeProperty, "keyword"), values);
            AppendStringValues(GetChildProperty(themeProperty, "aKeyword"), values);

            foreach (WzImageProperty child in themeProperty.WzProperties.OfType<WzImageProperty>())
            {
                if (byte.TryParse(child.Name, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    continue;
                }

                if (string.Equals(child.Name, "stageKeyword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "keyword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "aKeyword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "stage", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(child.Name, "stageList", StringComparison.OrdinalIgnoreCase))
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
                HashSet<string> extraKeywords = ParseStringSet(
                    GetChildProperty(periodProperty, "stageKeyword"),
                    GetChildProperty(periodProperty, "keyword"),
                    GetChildProperty(periodProperty, "aKeyword"));
                if (extraKeywords.Count > 0)
                {
                    periodKeywords[mode] = extraKeywords;
                }
            }

            return periodKeywords;
        }

        private static bool MatchesAffectedMapEntries(
            IReadOnlyList<ContextOwnedStageAffectedMapEntry> entries,
            ContextOwnedStagePeriodCatalogEntry period,
            Func<int, QuestStateType> questStateProvider,
            int elapsedStagePeriodMilliseconds,
            bool ignoreRandomTimeGate = false)
        {
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            int highestPriority = entries.Max(static entry => entry.Priority);
            foreach (ContextOwnedStageAffectedMapEntry candidate in entries)
            {
                if (candidate.Priority != highestPriority)
                {
                    continue;
                }

                if (candidate.Matches(
                    period,
                    questStateProvider,
                    elapsedStagePeriodMilliseconds,
                    ignoreRandomTimeGate))
                {
                    return true;
                }
            }

            return false;
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

        private static WzImageProperty GetChildProperty(WzImageProperty parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (parent[name] is WzImageProperty exactMatch)
            {
                return exactMatch;
            }

            foreach (WzImageProperty child in parent.WzProperties.OfType<WzImageProperty>())
            {
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsStructuralStageAliasName(string name)
        {
            return string.IsNullOrWhiteSpace(name)
                || string.Equals(name, "stage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "stageList", StringComparison.OrdinalIgnoreCase)
                || int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        private static void ApplyThemeScopedEnableState<TKey>(
            string stageTheme,
            IEnumerable<TKey> enabledKeys,
            IDictionary<TKey, ContextOwnedStageUnitEnableState> cache)
        {
            string normalizedTheme = NormalizeStageTheme(stageTheme);
            if (cache == null || string.IsNullOrWhiteSpace(normalizedTheme))
            {
                return;
            }

            foreach ((TKey key, ContextOwnedStageUnitEnableState state) in cache.ToArray())
            {
                if (state != null && IsEquivalentStageTheme(state.StageTheme, normalizedTheme))
                {
                    state.Enabled = false;
                }
            }

            foreach (TKey key in enabledKeys ?? Enumerable.Empty<TKey>())
            {
                TKey normalizedKey = ResolveExistingEquivalentKey(cache, key);
                if (!cache.TryGetValue(normalizedKey, out ContextOwnedStageUnitEnableState state) || state == null)
                {
                    state = new ContextOwnedStageUnitEnableState(normalizedTheme, enabled: true);
                    cache[normalizedKey] = state;
                    continue;
                }

                state.StageTheme = normalizedTheme;
                state.Enabled = true;
            }
        }

        private static HashSet<TKey> CaptureEnabledValues<TKey>(IDictionary<TKey, ContextOwnedStageUnitEnableState> cache)
        {
            HashSet<TKey> enabledValues = new(ResolveEnabledValueComparer<TKey>());
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

        private static void ApplyStagePeriodModeCache(
            string stageTheme,
            byte mode,
            IDictionary<string, byte> stagePeriodCache)
        {
            string normalizedTheme = NormalizeStageTheme(stageTheme);
            if (stagePeriodCache == null || string.IsNullOrWhiteSpace(normalizedTheme))
            {
                return;
            }

            string existingTheme = ResolveExistingEquivalentStringKey(stagePeriodCache, normalizedTheme);
            stagePeriodCache[existingTheme ?? normalizedTheme] = mode;
        }

        private static string NormalizeStageTheme(string stageTheme)
        {
            return string.IsNullOrWhiteSpace(stageTheme)
                ? string.Empty
                : stageTheme.Trim();
        }

        private static bool IsEquivalentStageTheme(string left, string right)
        {
            return string.Equals(
                NormalizeStageTheme(left),
                NormalizeStageTheme(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static TKey ResolveExistingEquivalentKey<TKey>(
            IDictionary<TKey, ContextOwnedStageUnitEnableState> cache,
            TKey key)
        {
            if (cache == null || key == null || typeof(TKey) != typeof(string))
            {
                return key;
            }

            string stringKey = ((string)(object)key)?.Trim();
            if (string.IsNullOrWhiteSpace(stringKey))
            {
                return key;
            }

            string existingKey = ResolveExistingEquivalentStringKey(
                cache as IDictionary<string, ContextOwnedStageUnitEnableState>,
                stringKey);
            return (TKey)(object)(existingKey ?? stringKey);
        }

        private static string ResolveExistingEquivalentStringKey<TValue>(
            IDictionary<string, TValue> cache,
            string key)
        {
            if (cache == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            foreach (string existingKey in cache.Keys)
            {
                if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return existingKey;
                }
            }

            return null;
        }

        private static IEqualityComparer<TKey> ResolveEnabledValueComparer<TKey>()
        {
            return typeof(TKey) == typeof(string)
                ? (IEqualityComparer<TKey>)(object)StringComparer.OrdinalIgnoreCase
                : EqualityComparer<TKey>.Default;
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
            StageTheme = string.IsNullOrWhiteSpace(stageTheme)
                ? string.Empty
                : stageTheme.Trim();
            Mode = mode;
            BackColorArgb = backColorArgb;
            BackImages = backImages ?? Array.Empty<ContextOwnedStageBackImageEntry>();
            Keywords = keywords ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
        bool SpineRandomStart,
        bool UseSourceBackPieceFields = false);

    internal sealed class ContextOwnedStageUnitEnableState
    {
        internal ContextOwnedStageUnitEnableState(string stageTheme, bool enabled)
        {
            StageTheme = string.IsNullOrWhiteSpace(stageTheme)
                ? string.Empty
                : stageTheme.Trim();
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
        private const int StageAffectedMapPriorityAliasStringPoolId = 0x0B11;
        private const int StageAffectedMapQuestIdAliasStringPoolId = 0x1113;
        private const int StageAffectedMapFieldIdStringPoolId = 0x17DB;
        private const int StageAffectedMapQuestIdStringPoolId = 0x17DC;
        private const int StageAffectedMapQuestStateStringPoolId = 0x17DD;
        private const int StageAffectedMapRandomTimeStringPoolId = 0x17DE;
        private const int StageAffectedMapStageKeywordStringPoolId = 0x17DF;
        private const int StageAffectedMapPriorityStringPoolId = 0x17E0;

        internal static ContextOwnedStageAffectedMapRow Empty { get; } = new(
            StageKeyword: null,
            Priority: null,
            QuestId: null,
            QuestState: null,
            HasQuestStateGate: false,
            RandomTimeSeconds: null);

        internal ContextOwnedStageAffectedMapRow Merge(WzImageProperty property)
        {
            WzImageProperty[] stageKeywordProperties =
            {
                ResolveClientProperty(property, StageAffectedMapStageKeywordStringPoolId, "stageKeyword"),
                ResolveClientProperty(property, StageAffectedMapStageKeywordStringPoolId, "keyword"),
                ResolveClientProperty(property, StageAffectedMapStageKeywordStringPoolId, "aKeyword")
            };
            WzImageProperty[] priorityProperties =
            {
                ResolveClientProperty(property, StageAffectedMapPriorityStringPoolId, "priority"),
                ResolveClientProperty(property, StageAffectedMapPriorityAliasStringPoolId, "Priority")
            };
            WzImageProperty[] questIdProperties =
            {
                ResolveClientProperty(property, StageAffectedMapQuestIdStringPoolId, "questID"),
                ResolveClientProperty(property, StageAffectedMapQuestIdAliasStringPoolId, "questId"),
                ResolveClientProperty(property, StageAffectedMapQuestIdStringPoolId, "enabledQuest"),
                ResolveClientProperty(property, StageAffectedMapQuestIdStringPoolId, "aEnabledQuest"),
                ResolveClientProperty(property, StageAffectedMapQuestIdStringPoolId, "quest")
            };
            WzImageProperty[] questStateProperties =
            {
                ResolveClientProperty(property, StageAffectedMapQuestStateStringPoolId, "questState")
            };
            WzImageProperty[] randomTimeProperties =
            {
                ResolveClientProperty(property, StageAffectedMapRandomTimeStringPoolId, "randTime")
            };

            string stageKeyword = ReadFirstString(stageKeywordProperties) ?? StageKeyword;
            int? priority = ReadFirstInt(priorityProperties) ?? Priority;
            int? questId = ReadFirstInt(questIdProperties) ?? QuestId;
            int? questState = ReadFirstInt(questStateProperties) ?? QuestState;
            int? randomTimeSeconds = ReadFirstInt(randomTimeProperties) ?? RandomTimeSeconds;
            return new ContextOwnedStageAffectedMapRow(
                stageKeyword,
                priority,
                questId,
                questState,
                HasQuestStateGate || questStateProperties.Any(static item => item != null),
                randomTimeSeconds);
        }

        internal int ResolveFieldId(WzImageProperty property, bool allowNumericFallback = true)
        {
            int? currentFieldId = ReadFirstInt(
                ResolveClientProperty(property, StageAffectedMapFieldIdStringPoolId, "fieldID"),
                ResolveClientProperty(property, StageAffectedMapFieldIdStringPoolId, "fieldId"),
                ResolveClientProperty(property, StageAffectedMapFieldIdStringPoolId, "affectedMap"),
                ResolveClientProperty(property, StageAffectedMapFieldIdStringPoolId, "aAffectedMap"));
            if (currentFieldId.HasValue)
            {
                return currentFieldId.Value;
            }

            if (allowNumericFallback
                && property is WzIntProperty intProperty
                && int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return intProperty.Value;
            }

            return allowNumericFallback
                && property != null
                && int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : int.MinValue;
        }

        internal static bool IsFieldIdAliasName(string propertyName)
        {
            return IsAliasNameMatch(propertyName, StageAffectedMapFieldIdStringPoolId, "fieldID")
                || IsAliasNameMatch(propertyName, StageAffectedMapFieldIdStringPoolId, "fieldId")
                || IsAliasNameMatch(propertyName, StageAffectedMapFieldIdStringPoolId, "affectedMap")
                || IsAliasNameMatch(propertyName, StageAffectedMapFieldIdStringPoolId, "aAffectedMap");
        }

        internal static bool IsNonFieldMetadataAliasName(string propertyName)
        {
            return IsAliasNameMatch(propertyName, StageAffectedMapStageKeywordStringPoolId, "stageKeyword")
                || IsAliasNameMatch(propertyName, StageAffectedMapStageKeywordStringPoolId, "keyword")
                || IsAliasNameMatch(propertyName, StageAffectedMapStageKeywordStringPoolId, "aKeyword")
                || IsAliasNameMatch(propertyName, StageAffectedMapQuestIdStringPoolId, "questID")
                || IsAliasNameMatch(propertyName, StageAffectedMapQuestIdAliasStringPoolId, "questId")
                || IsAliasNameMatch(propertyName, StageAffectedMapQuestIdStringPoolId, "enabledQuest")
                || IsAliasNameMatch(propertyName, StageAffectedMapQuestIdStringPoolId, "aEnabledQuest")
                || IsAliasNameMatch(propertyName, StageAffectedMapQuestIdStringPoolId, "quest")
                || IsAliasNameMatch(propertyName, StageAffectedMapQuestStateStringPoolId, "questState")
                || IsAliasNameMatch(propertyName, StageAffectedMapPriorityStringPoolId, "priority")
                || IsAliasNameMatch(propertyName, StageAffectedMapPriorityAliasStringPoolId, "Priority")
                || IsAliasNameMatch(propertyName, StageAffectedMapRandomTimeStringPoolId, "randTime")
                || string.Equals(propertyName, "stage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyName, "stageList", StringComparison.OrdinalIgnoreCase);
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

        internal bool IsStructurallyValidForRuntimeMatch()
        {
            // `questState` rows without a concrete quest id are malformed metadata;
            // do not let them participate in priority-tier arbitration.
            return !HasQuestStateGate || QuestId.GetValueOrDefault() > 0;
        }

        private static string ReadString(WzImageProperty property)
        {
            if (property == null)
            {
                return null;
            }

            string text = InfoTool.GetString(property);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                string childValue = ReadString(child);
                if (!string.IsNullOrWhiteSpace(childValue))
                {
                    return childValue;
                }
            }

            return null;
        }

        private static string ReadFirstString(params WzImageProperty[] properties)
        {
            if (properties == null)
            {
                return null;
            }

            foreach (WzImageProperty property in properties)
            {
                string value = ReadString(property);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
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
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return parsed;
            }

            foreach (WzImageProperty child in property.WzProperties.OfType<WzImageProperty>())
            {
                int? childValue = ReadInt(child);
                if (childValue.HasValue)
                {
                    return childValue;
                }
            }

            return null;
        }

        private static int? ReadFirstInt(params WzImageProperty[] properties)
        {
            if (properties == null)
            {
                return null;
            }

            foreach (WzImageProperty property in properties)
            {
                int? value = ReadInt(property);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }

        private static WzImageProperty ResolveClientProperty(
            WzImageProperty parent,
            int stringPoolId,
            string fallbackName)
        {
            if (parent == null)
            {
                return null;
            }

            string resolvedName = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackName);
            if (!string.IsNullOrWhiteSpace(resolvedName))
            {
                WzImageProperty resolvedProperty = GetChildProperty(parent, resolvedName);
                if (resolvedProperty != null)
                {
                    return resolvedProperty;
                }
            }

            return GetChildProperty(parent, fallbackName);
        }

        private static WzImageProperty GetChildProperty(WzImageProperty parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (parent[name] is WzImageProperty exactMatch)
            {
                return exactMatch;
            }

            foreach (WzImageProperty child in parent.WzProperties.OfType<WzImageProperty>())
            {
                if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static WzImageProperty ResolveFirstClientProperty(
            WzImageProperty parent,
            params (int StringPoolId, string FallbackName)[] candidates)
        {
            if (parent == null || candidates == null)
            {
                return null;
            }

            foreach ((int stringPoolId, string fallbackName) in candidates)
            {
                WzImageProperty property = ResolveClientProperty(parent, stringPoolId, fallbackName);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static bool IsAliasNameMatch(string propertyName, int stringPoolId, string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return false;
            }

            string resolvedName = MapleStoryStringPool.GetOrFallback(stringPoolId, fallbackName);
            return string.Equals(propertyName, fallbackName, StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(resolvedName)
                && string.Equals(propertyName, resolvedName, StringComparison.OrdinalIgnoreCase);
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
            int elapsedStagePeriodMilliseconds = 0,
            bool ignoreRandomTimeGate = false)
        {
            if (period == null || string.IsNullOrWhiteSpace(StageKeyword) || !period.Keywords.Contains(StageKeyword))
            {
                return false;
            }

            if (!ignoreRandomTimeGate
                && RandomTimeSeconds > 0
                && elapsedStagePeriodMilliseconds < ResolveRandomTimeGateMilliseconds(RandomTimeSeconds))
            {
                return false;
            }

            if (HasQuestStateGate && QuestId <= 0)
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

        internal static int ResolveRandomTimeGateMilliseconds(int randomTimeSeconds)
        {
            if (randomTimeSeconds <= 0)
            {
                return 0;
            }

            long milliseconds = (long)randomTimeSeconds * 1000L;
            return milliseconds > int.MaxValue
                ? int.MaxValue
                : (int)milliseconds;
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
