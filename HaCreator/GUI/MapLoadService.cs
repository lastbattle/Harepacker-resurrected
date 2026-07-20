using HaCreator.MapEditor;
using HaCreator.GUI.Localization;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.Serializer;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    internal static class MapLoadService
    {
        public static void LoadHamMap(
            string filePath,
            MultiBoard multiBoard,
            System.Windows.Controls.TabControl tabs,
            System.Windows.RoutedEventHandler[] rightClickHandler)
        {
            MapLoader.CreateMapFromHam(multiBoard, tabs, File.ReadAllText(filePath), rightClickHandler);
        }

        public static bool TryLoadXmlMap(
            string filePath,
            System.Windows.Controls.TabControl tabs,
            MultiBoard multiBoard,
            System.Windows.RoutedEventHandler[] rightClickHandler,
            out string errorMessage)
        {
            errorMessage = null;

            WzImage mapImage;
            try
            {
                mapImage = (WzImage)new WzXmlDeserializer(false, null).ParseXML(filePath)[0];
            }
            catch
            {
                errorMessage = "Error while loading XML. Aborted.";
                return false;
            }

            MapInfo info = new MapInfo(mapImage, null, string.Empty, string.Empty);
            MapLoader.CreateMapFromImage(-1, mapImage, info, null, string.Empty, string.Empty, tabs, multiBoard, rightClickHandler);
            return true;
        }

        public static bool TryLoadWzMapSelection(
            string selectedItem,
            System.Windows.Controls.TabControl tabs,
            MultiBoard multiBoard,
            System.Windows.RoutedEventHandler[] rightClickHandler,
            out string errorMessage)
        {
            if (!TryResolveWzMapSelection(selectedItem, out SelectedMapLoadResult selectedMap, out errorMessage))
            {
                return false;
            }

            MapLoader.CreateMapFromImage(
                selectedMap.MapId,
                selectedMap.MapImage,
                selectedMap.Info,
                selectedMap.MapName,
                selectedMap.StreetName,
                selectedMap.CategoryName,
                tabs,
                multiBoard,
                rightClickHandler);

            return true;
        }

        public static MissingMapResolutionResult ResolveMissingMapStringEntries()
        {
            WzImage stringMapImage = GetStringMapImage();
            if (stringMapImage == null)
            {
                throw new InvalidOperationException("String.wz/Map.img could not be found.");
            }

            stringMapImage.ParseImage();

            Dictionary<string, WzSubProperty> stringCategories = stringMapImage.WzProperties
                .OfType<WzSubProperty>()
                .ToDictionary(category => category.Name, category => category, StringComparer.OrdinalIgnoreCase);

            HashSet<string> existingMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WzSubProperty category in stringCategories.Values)
            {
                foreach (WzSubProperty mapEntry in category.WzProperties.OfType<WzSubProperty>())
                {
                    existingMapIds.Add(WzInfoTools.AddLeadingZeros(mapEntry.Name, 9));
                }
            }

            MissingMapResolutionResult result = new MissingMapResolutionResult();
            foreach (WzImage mapImage in EnumerateAllMapImages())
            {
                string mapId = Path.GetFileNameWithoutExtension(mapImage.Name);
                if (string.IsNullOrEmpty(mapId))
                {
                    continue;
                }

                if (existingMapIds.Contains(mapId))
                {
                    result.AlreadyPresentMapIds.Add(mapId);
                    continue;
                }

                if (HasPositiveLinkTarget(mapImage))
                {
                    continue;
                }

                string categoryName = GetGeneratedMapCategoryName(mapImage);
                if (!stringCategories.TryGetValue(categoryName, out WzSubProperty stringCategory))
                {
                    stringCategory = new WzSubProperty(categoryName);
                    stringMapImage.AddProperty(stringCategory);
                    stringCategories[categoryName] = stringCategory;
                }

                WzSubProperty mapEntry = new WzSubProperty(mapId);
                mapEntry.AddProperty(new WzStringProperty("streetName", "NO NAME"));
                mapEntry.AddProperty(new WzStringProperty("mapName", "NO NAME"));
                stringCategory.AddProperty(mapEntry);

                Program.InfoManager.MapsNameCache[mapId] = new Tuple<string, string, string>("NO NAME", "NO NAME", categoryName);

                if (mapImage["info"] != null)
                {
                    MapInfo info = new MapInfo(mapImage, "NO NAME", "NO NAME", categoryName);
                    Program.InfoManager.MapsCache[mapId] = new Tuple<WzImage, string, string, string, MapInfo>(
                        mapImage,
                        "NO NAME",
                        "NO NAME",
                        categoryName,
                        info);
                }

                existingMapIds.Add(mapId);
                result.AddedMapIds.Add(mapId);
            }

            if (result.AddedMapIds.Count > 0)
            {
                stringMapImage.Changed = true;

                if (ShouldConfirmImmediateStringMapSave() &&
                    MessageBox.Show(
                        DialogTextExtension.Get("Dialog_ImmediateStringMapSavePrompt"),
                        DialogTextExtension.Get("Dialog_ResolveMissingMaps"),
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    Program.DataSource?.MarkImageUpdated("String", stringMapImage);
                }
            }

            result.AlreadyPresentMapIds.Sort(StringComparer.OrdinalIgnoreCase);
            result.AddedMapIds.Sort(StringComparer.OrdinalIgnoreCase);

            return result;
        }

        public static string BuildResolutionSummaryMessage(MissingMapResolutionResult result)
        {
            List<string> lines = new List<string>
            {
                $"Added {result.AddedMapIds.Count} new entr{(result.AddedMapIds.Count == 1 ? "y" : "ies")}.",
                $"Already present in memory: {result.AlreadyPresentMapIds.Count}.",
            };

            if (result.AddedMapIds.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Added:");
                lines.AddRange(result.AddedMapIds);
            }

            if (result.AddedMapIds.Count == 0 && result.AlreadyPresentMapIds.Count == 0)
            {
                lines.Add(string.Empty);
                lines.Add("No maps were discovered under Map.wz\\Map\\Map*.");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static bool TryResolveWzMapSelection(
            string selectedItem,
            out SelectedMapLoadResult selectedMap,
            out string errorMessage)
        {
            selectedMap = null;
            errorMessage = null;

            if (string.IsNullOrEmpty(selectedItem))
            {
                errorMessage = "No map is selected.";
                return false;
            }

            WzImage mapImage = null;
            int mapId = -1;
            string mapName;
            string streetName;
            string categoryName;
            MapInfo info;

            if (selectedItem.StartsWith("MapLogin", StringComparison.Ordinal) ||
                selectedItem == "CashShopPreview" ||
                selectedItem == "ITCPreview")
            {
                mapImage = Program.FindImage("UI", selectedItem + ".img");

                mapName = streetName = categoryName = selectedItem;
                if (mapImage == null)
                {
                    errorMessage = "Failed to load map image.";
                    return false;
                }

                info = new MapInfo(mapImage, mapName, streetName, categoryName);
            }
            else
            {
                string mapIdString = selectedItem.Substring(0, 9);
                int.TryParse(mapIdString, out mapId);

                if (!Program.InfoManager.MapsCache.ContainsKey(mapIdString))
                {
                    errorMessage = "Map is missing.";
                    return false;
                }

                Tuple<WzImage, string, string, string, MapInfo> loadedMap = Program.InfoManager.MapsCache[mapIdString];

                mapImage = loadedMap.Item1;
                streetName = loadedMap.Item2;
                mapName = loadedMap.Item3;
                categoryName = loadedMap.Item4;
                info = loadedMap.Item5;

                if (mapImage == null)
                {
                    mapImage = LoadMapImageOnDemand(mapIdString);
                }
                if (mapImage == null)
                {
                    errorMessage = "Failed to load map image.";
                    return false;
                }

                if (info == null)
                {
                    info = new MapInfo(mapImage, streetName, mapName, categoryName);
                }
            }

            if (mapImage == null)
            {
                errorMessage = "Failed to load map image.";
                return false;
            }

            selectedMap = new SelectedMapLoadResult(mapId, mapImage, info, mapName, streetName, categoryName);
            return true;
        }

        private static WzImage LoadMapImageOnDemand(string mapId)
        {
            if (Program.DataSource == null)
            {
                return null;
            }

            string paddedId = mapId.PadLeft(9, '0');
            string folderNum = paddedId[0].ToString();

            string relativePath = $"Map/Map{folderNum}/{paddedId}.img";
            WzImage mapImage = Program.DataSource.GetImageByPath($"Map/{relativePath}");

            if (mapImage == null)
            {
                mapImage = Program.DataSource.GetImage("Map", $"Map/Map{folderNum}/{paddedId}.img");
            }

            if (mapImage != null)
            {
                mapImage.ParseImage();
            }

            return mapImage;
        }

        private static WzImage GetStringMapImage()
        {
            WzImage stringMapImage = Program.DataSource?.GetImage("String", "Map.img");
            if (stringMapImage == null)
            {
                stringMapImage = Program.DataSource?.GetImageByPath("String/Map.img");
            }
            if (stringMapImage == null)
            {
                stringMapImage = (WzImage)Program.WzManager?.FindWzImageByName("string", "Map.img");
            }

            return stringMapImage;
        }

        private static IEnumerable<WzImage> EnumerateAllMapImages()
        {
            HashSet<string> seenMapIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Program.DataSource != null)
            {
                foreach (string subDirectory in Program.DataSource.GetSubdirectories("Map"))
                {
                    if (!IsMapImageDirectory(subDirectory))
                    {
                        continue;
                    }

                    foreach (WzImage mapImage in Program.DataSource.GetImagesInDirectory("Map", subDirectory))
                    {
                        string mapId = Path.GetFileNameWithoutExtension(mapImage.Name);
                        if (string.IsNullOrEmpty(mapId) || !seenMapIds.Add(mapId))
                        {
                            continue;
                        }

                        yield return mapImage;
                    }
                }

                yield break;
            }

            foreach (WzDirectory rootDirectory in GetMapRootsFromWzManager())
            {
                foreach (WzImage mapImage in EnumerateMapImagesRecursive(rootDirectory))
                {
                    string mapId = Path.GetFileNameWithoutExtension(mapImage.Name);
                    if (!seenMapIds.Add(mapId))
                    {
                        continue;
                    }

                    yield return mapImage;
                }
            }
        }

        private static IEnumerable<WzImage> EnumerateMapImagesRecursive(WzDirectory directory)
        {
            if (directory == null)
            {
                yield break;
            }

            foreach (WzImage image in directory.WzImages)
            {
                string mapId = Path.GetFileNameWithoutExtension(image.Name);
                if (image.Name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) &&
                    mapId.Length == 9 &&
                    mapId.All(char.IsDigit))
                {
                    yield return image;
                }
            }

            foreach (WzDirectory childDirectory in directory.WzDirectories)
            {
                foreach (WzImage image in EnumerateMapImagesRecursive(childDirectory))
                {
                    yield return image;
                }
            }
        }

        private static IEnumerable<WzDirectory> GetMapRootsFromWzManager()
        {
            foreach (WzDirectory rootDirectory in Program.WzManager.GetWzDirectoriesFromBase("map"))
            {
                if (rootDirectory == null)
                {
                    continue;
                }

                WzDirectory mapDirectory = rootDirectory["Map"] as WzDirectory;
                yield return mapDirectory ?? rootDirectory;
            }
        }

        private static string GetGeneratedMapCategoryName(WzImage mapImage)
        {
            WzObject current = mapImage.Parent;
            while (current != null)
            {
                if (current is WzDirectory directory &&
                    directory.Name.Length == 4 &&
                    directory.Name.StartsWith("Map", StringComparison.OrdinalIgnoreCase) &&
                    char.IsDigit(directory.Name[3]))
                {
                    return directory.Name;
                }

                current = current.Parent;
            }

            return "AutoGenerated";
        }

        private static bool IsMapImageDirectory(string subDirectory)
        {
            if (string.IsNullOrEmpty(subDirectory))
            {
                return false;
            }

            string[] segments = subDirectory
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 1)
            {
                return IsMapCategorySegment(segments[0]);
            }

            if (segments.Length == 2 && segments[0].Equals("Map", StringComparison.OrdinalIgnoreCase))
            {
                return IsMapCategorySegment(segments[1]);
            }

            return false;
        }

        private static bool IsMapCategorySegment(string segment)
        {
            return !string.IsNullOrEmpty(segment) &&
                   segment.Length == 4 &&
                   segment.StartsWith("Map", StringComparison.OrdinalIgnoreCase) &&
                   char.IsDigit(segment[3]);
        }

        private static bool HasPositiveLinkTarget(WzImage mapImage)
        {
            WzImageProperty linkProperty = mapImage.GetFromPath("info/link");
            if (linkProperty == null)
            {
                return false;
            }

            try
            {
                return linkProperty.GetInt() > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldConfirmImmediateStringMapSave()
        {
            if (Program.DataSource is ImgFileSystemDataSource)
            {
                return true;
            }

            if (Program.DataSource is HybridDataSource hybridDataSource && hybridDataSource.ImgSource != null)
            {
                return true;
            }

            return false;
        }

        public sealed class MissingMapResolutionResult
        {
            public List<string> AddedMapIds { get; } = new List<string>();
            public List<string> AlreadyPresentMapIds { get; } = new List<string>();
        }

        private sealed class SelectedMapLoadResult
        {
            public SelectedMapLoadResult(
                int mapId,
                WzImage mapImage,
                MapInfo info,
                string mapName,
                string streetName,
                string categoryName)
            {
                MapId = mapId;
                MapImage = mapImage;
                Info = info;
                MapName = mapName;
                StreetName = streetName;
                CategoryName = categoryName;
            }

            public int MapId { get; }
            public WzImage MapImage { get; }
            public MapInfo Info { get; }
            public string MapName { get; }
            public string StreetName { get; }
            public string CategoryName { get; }
        }
    }
}
