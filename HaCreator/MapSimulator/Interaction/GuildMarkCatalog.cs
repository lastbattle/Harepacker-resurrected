using HaCreator;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Interaction
{
    internal static class GuildMarkCatalog
    {
        private static readonly Lazy<GuildMarkCatalogData> Catalog = new(LoadCatalog);

        internal static GuildMarkCatalogData GetCatalog()
        {
            return Catalog.Value;
        }

        private static GuildMarkCatalogData LoadCatalog()
        {
            try
            {
                WzImage image = Program.FindImage("UI", "GuildMark.img");
                if (image == null)
                {
                    return GuildMarkCatalogData.CreateFallback();
                }

                image.ParseImage();

                List<int> backgroundIds = [];
                Dictionary<int, string> backgroundNames = [];
                Dictionary<int, string> backgroundCanvasPaths = [];

                if (image["BackGround"] is WzSubProperty backgroundRoot)
                {
                    foreach (WzSubProperty backgroundProperty in backgroundRoot.WzProperties.OfType<WzSubProperty>())
                    {
                        if (!TryParseMarkId(backgroundProperty.Name, out int backgroundId))
                        {
                            continue;
                        }

                        backgroundIds.Add(backgroundId);
                        backgroundNames[backgroundId] = (backgroundProperty["name"] as WzStringProperty)?.Value ?? backgroundProperty.Name;
                        backgroundCanvasPaths[backgroundId] = $"BackGround/{backgroundProperty.Name}";
                    }
                }

                List<GuildMarkFamilyInfo> families = [];
                Dictionary<int, string> markNames = [];
                Dictionary<int, string> markCanvasPaths = [];

                if (image["Mark"] is WzSubProperty markRoot)
                {
                    foreach (WzSubProperty familyProperty in markRoot.WzProperties.OfType<WzSubProperty>())
                    {
                        List<int> markIds = [];
                        foreach (WzSubProperty markProperty in familyProperty.WzProperties.OfType<WzSubProperty>())
                        {
                            if (!TryParseMarkId(markProperty.Name, out int markId))
                            {
                                continue;
                            }

                            markIds.Add(markId);
                            markNames[markId] = (markProperty["name"] as WzStringProperty)?.Value ?? markProperty.Name;
                            markCanvasPaths[markId] = $"Mark/{familyProperty.Name}/{markProperty.Name}";
                        }

                        if (markIds.Count == 0)
                        {
                            continue;
                        }

                        markIds.Sort();
                        families.Add(new GuildMarkFamilyInfo(markIds[0] / 1000, familyProperty.Name, markIds));
                    }
                }

                if (backgroundIds.Count == 0 || families.Count == 0)
                {
                    return GuildMarkCatalogData.CreateFallback();
                }

                backgroundIds.Sort();
                families.Sort((left, right) => left.Group.CompareTo(right.Group));
                return new GuildMarkCatalogData(backgroundIds, backgroundNames, backgroundCanvasPaths, families, markNames, markCanvasPaths);
            }
            catch
            {
                return GuildMarkCatalogData.CreateFallback();
            }
        }

        private static bool TryParseMarkId(string text, out int value)
        {
            return int.TryParse(text?.TrimStart('0'), out value) || int.TryParse(text, out value);
        }
    }

    internal sealed class GuildMarkCatalogData
    {
        private readonly Dictionary<int, int> _backgroundIndexById;
        private readonly Dictionary<int, int> _familyIndexByGroup;
        private readonly Dictionary<int, int> _markIndexById;
        private readonly Dictionary<int, int> _lastMarkIdByGroup;

        internal GuildMarkCatalogData(
            IReadOnlyList<int> backgroundIds,
            IReadOnlyDictionary<int, string> backgroundNames,
            IReadOnlyDictionary<int, string> backgroundCanvasPaths,
            IReadOnlyList<GuildMarkFamilyInfo> families,
            IReadOnlyDictionary<int, string> markNames,
            IReadOnlyDictionary<int, string> markCanvasPaths)
        {
            BackgroundIds = backgroundIds;
            BackgroundNames = backgroundNames;
            BackgroundCanvasPaths = backgroundCanvasPaths;
            Families = families;
            MarkNames = markNames;
            MarkCanvasPaths = markCanvasPaths;
            _backgroundIndexById = backgroundIds
                .Select((id, index) => new KeyValuePair<int, int>(id, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            _familyIndexByGroup = families
                .Select((family, index) => new KeyValuePair<int, int>(family.Group, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            _markIndexById = families
                .SelectMany(family => family.MarkIds)
                .Select((id, index) => new KeyValuePair<int, int>(id, index))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            _lastMarkIdByGroup = families.ToDictionary(family => family.Group, family => family.MarkIds[^1]);
        }

        internal IReadOnlyList<int> BackgroundIds { get; }

        internal IReadOnlyDictionary<int, string> BackgroundNames { get; }

        internal IReadOnlyDictionary<int, string> BackgroundCanvasPaths { get; }

        internal IReadOnlyList<GuildMarkFamilyInfo> Families { get; }

        internal IReadOnlyDictionary<int, string> MarkNames { get; }

        internal IReadOnlyDictionary<int, string> MarkCanvasPaths { get; }

        internal int DefaultBackgroundId => BackgroundIds.Count > 0 ? BackgroundIds[0] : 1000;

        internal int DefaultGroup => Families.Count > 0 ? Families[0].Group : 2;

        internal int DefaultMarkId => Families.Count > 0 && Families[0].MarkIds.Count > 0 ? Families[0].MarkIds[0] : 2000;

        internal string ResolveBackgroundName(int backgroundId)
        {
            return BackgroundNames.TryGetValue(backgroundId, out string name) ? name : $"BG {backgroundId}";
        }

        internal string ResolveMarkName(int markId)
        {
            return MarkNames.TryGetValue(markId, out string name) ? name : $"Mark {markId}";
        }

        internal int ResolveFamilyIndex(int markId)
        {
            int group = Math.Max(0, markId / 1000);
            return _familyIndexByGroup.TryGetValue(group, out int familyIndex) ? familyIndex : 0;
        }

        internal int ResolveLastMarkId(int group)
        {
            return _lastMarkIdByGroup.TryGetValue(group, out int lastMarkId) ? lastMarkId : group * 1000;
        }

        internal GuildMarkFamilyInfo ResolveFamilyByIndex(int comboIndex)
        {
            if (Families.Count == 0)
            {
                return GuildMarkFamilyInfo.CreateFallback();
            }

            int clampedIndex = Math.Clamp(comboIndex, 0, Families.Count - 1);
            return Families[clampedIndex];
        }

        internal int MoveBackground(int currentBackgroundId, int delta)
        {
            if (BackgroundIds.Count == 0)
            {
                return currentBackgroundId;
            }

            if (!_backgroundIndexById.TryGetValue(currentBackgroundId, out int index))
            {
                index = 0;
            }

            int nextIndex = WrapIndex(index + delta, BackgroundIds.Count);
            return BackgroundIds[nextIndex];
        }

        internal int MoveMark(int currentMarkId, int comboIndex, int delta)
        {
            GuildMarkFamilyInfo family = ResolveFamilyByIndex(comboIndex);
            if (family.MarkIds.Count == 0)
            {
                return currentMarkId;
            }

            if (!family.MarkIds.Contains(currentMarkId))
            {
                return family.MarkIds[0];
            }

            int currentIndex = 0;
            for (int i = 0; i < family.MarkIds.Count; i++)
            {
                if (family.MarkIds[i] == currentMarkId)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = WrapIndex(currentIndex + delta, family.MarkIds.Count);
            return family.MarkIds[nextIndex];
        }

        internal bool TryGetBackgroundCanvasPath(int backgroundId, int colorIndex, out string path)
        {
            path = null;
            if (!BackgroundCanvasPaths.TryGetValue(backgroundId, out string basePath))
            {
                return false;
            }

            path = $"{basePath}/{Math.Clamp(colorIndex, 1, 16)}";
            return true;
        }

        internal bool TryGetMarkCanvasPath(int markId, int colorIndex, out string path)
        {
            path = null;
            if (!MarkCanvasPaths.TryGetValue(markId, out string basePath))
            {
                return false;
            }

            path = $"{basePath}/{Math.Clamp(colorIndex, 1, 16)}";
            return true;
        }

        internal static GuildMarkCatalogData CreateFallback()
        {
            int[] backgroundIds = Enumerable.Range(1000, 16).ToArray();
            Dictionary<int, string> backgroundNames = backgroundIds.ToDictionary(id => id, id => $"Background {id}");
            Dictionary<int, string> backgroundCanvasPaths = backgroundIds.ToDictionary(id => id, id => $"BackGround/{id:00000000}");
            GuildMarkFamilyInfo[] families =
            [
                new GuildMarkFamilyInfo(2, "Animal", Enumerable.Range(2000, 8).ToArray()),
                new GuildMarkFamilyInfo(3, "Plant", Enumerable.Range(3000, 8).ToArray()),
                new GuildMarkFamilyInfo(4, "Pattern", Enumerable.Range(4000, 8).ToArray()),
                new GuildMarkFamilyInfo(5, "Letter", Enumerable.Range(5000, 8).ToArray()),
                new GuildMarkFamilyInfo(9, "Etc", Enumerable.Range(9000, 8).ToArray())
            ];
            Dictionary<int, string> markNames = families
                .SelectMany(family => family.MarkIds.Select(id => new KeyValuePair<int, string>(id, $"{family.Label} {id}")))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<int, string> markCanvasPaths = families
                .SelectMany(family => family.MarkIds.Select(id => new KeyValuePair<int, string>(id, $"Mark/{family.Label}/{id:00000000}")))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            return new GuildMarkCatalogData(backgroundIds, backgroundNames, backgroundCanvasPaths, families, markNames, markCanvasPaths);
        }

        private static int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }

    internal sealed class GuildMarkFamilyInfo
    {
        internal GuildMarkFamilyInfo(int group, string label, IReadOnlyList<int> markIds)
        {
            Group = group;
            Label = string.IsNullOrWhiteSpace(label) ? $"Group {group}" : label.Trim();
            MarkIds = markIds ?? [];
        }

        internal int Group { get; }

        internal string Label { get; }

        internal IReadOnlyList<int> MarkIds { get; }

        internal static GuildMarkFamilyInfo CreateFallback()
        {
            return new GuildMarkFamilyInfo(2, "Animal", [2000]);
        }
    }
}
