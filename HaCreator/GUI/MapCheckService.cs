using HaCreator.MapEditor;
using HaCreator.MapEditor.Instance;
using HaCreator.Wz;
using HaSharedLibrary.Wz;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.GUI
{
    internal static class MapCheckService
    {
        internal const string OutputErrorFilename = "Errors_MapDebug.txt";

        public static IReadOnlyDictionary<ErrorLevel, List<Error>> CheckLoadedMaps()
        {
            EnsureDataSourceLoaded();
            return CheckMaps(Program.InfoManager.MapsNameCache.Keys.ToList());
        }

        public static IReadOnlyDictionary<ErrorLevel, List<Error>> CheckMap(string mapId)
        {
            EnsureDataSourceLoaded();
            if (string.IsNullOrWhiteSpace(mapId) || FindMapImage(mapId) == null)
            {
                throw new InvalidOperationException($"Map '{mapId}' was not found in the loaded map source.");
            }

            return CheckMaps(new[] { mapId });
        }

        private static void EnsureDataSourceLoaded()
        {
            if (Program.InfoManager == null ||
                (Program.DataSource == null && Program.WzManager == null))
            {
                throw new InvalidOperationException("No map data source is loaded.");
            }
        }

        private static IReadOnlyDictionary<ErrorLevel, List<Error>> CheckMaps(IEnumerable<string> mapIds)
        {
            MultiBoard multiBoard = new MultiBoard();
            Board mapBoard = new Board(
                new Microsoft.Xna.Framework.Point(),
                new Microsoft.Xna.Framework.Point(),
                multiBoard,
                false,
                null,
                ItemTypes.None,
                ItemTypes.None);

            HashSet<Error> existingErrors = ErrorLogger.GetErrorSnapshot()
                .SelectMany(group => group.Value)
                .ToHashSet();
            Dictionary<ErrorLevel, List<Error>> checkErrors = new Dictionary<ErrorLevel, List<Error>>();

            foreach (string mapId in mapIds)
            {
                WzImage mapImage = FindMapImage(mapId);
                if (mapImage == null)
                {
                    continue;
                }

                bool wasParsed = mapImage.Parsed;
                try
                {
                    if (!wasParsed)
                    {
                        mapImage.ParseImage();
                    }

                    if (mapImage["info"]?["link"] != null)
                    {
                        continue;
                    }

                    MapLoader.VerifyMapPropsKnown(mapImage, true);
                    mapBoard.CreateMapLayers();

                    MapLoader.LoadLayers(mapImage, mapBoard);
                    MapLoader.LoadLife(mapImage, mapBoard);
                    MapLoader.LoadFootholds(mapImage, mapBoard);
                    MapLoader.GenerateDefaultZms(mapBoard);
                    MapLoader.LoadRopes(mapImage, mapBoard);
                    MapLoader.LoadChairs(mapImage, mapBoard);
                    MapLoader.LoadPortals(mapImage, mapBoard);
                    MapLoader.LoadReactors(mapImage, mapBoard);
                    MapLoader.LoadToolTips(mapImage, mapBoard);
                    MapLoader.LoadBackgrounds(mapImage, mapBoard);
                    MapLoader.LoadMisc(mapImage, mapBoard);

                    List<BackgroundInstance> allBackgrounds = new List<BackgroundInstance>();
                    allBackgrounds.AddRange(mapBoard.BoardItems.BackBackgrounds);
                    allBackgrounds.AddRange(mapBoard.BoardItems.FrontBackgrounds);

                    foreach (BackgroundInstance background in allBackgrounds)
                    {
                        if (background.type != BackgroundType.Regular &&
                            (background.cx < 0 || background.cy < 0))
                        {
                            string error = string.Format(
                                "Negative CX/ CY moving background object. CX='{0}', CY={1}, Type={2}, {3}{4}",
                                background.cx,
                                background.cy,
                                background.type,
                                Environment.NewLine,
                                mapImage);
                            ErrorLogger.Log(ErrorLevel.IncorrectStructure, error);
                        }
                    }
                }
                catch (Exception exception)
                {
                    string error = string.Format(
                        "Exception occured loading {0}{1}{2}{3}",
                        Environment.NewLine,
                        mapImage,
                        Environment.NewLine,
                        exception);
                    ErrorLogger.Log(ErrorLevel.Crash, error);
                }
                finally
                {
                    mapBoard.Dispose();
                    mapBoard.BoardItems.BackBackgrounds.Clear();
                    mapBoard.BoardItems.FrontBackgrounds.Clear();

                    if (!wasParsed && !mapImage.Changed)
                    {
                        mapImage.UnparseImage();
                    }
                }

                if (ErrorLogger.NumberOfErrorsPresent() > 200)
                {
                    AddNewErrors(checkErrors, ErrorLogger.GetErrorSnapshot(), existingErrors);
                    ErrorLogger.SaveToFile(OutputErrorFilename);
                }
            }

            AddNewErrors(checkErrors, ErrorLogger.GetErrorSnapshot(), existingErrors);
            ErrorLogger.SaveToFile(OutputErrorFilename);
            return checkErrors;
        }

        private static void AddNewErrors(
            Dictionary<ErrorLevel, List<Error>> target,
            IReadOnlyDictionary<ErrorLevel, List<Error>> snapshot,
            ISet<Error> existingErrors)
        {
            foreach (KeyValuePair<ErrorLevel, List<Error>> group in snapshot)
            {
                foreach (Error error in group.Value)
                {
                    if (existingErrors.Contains(error))
                    {
                        continue;
                    }

                    if (!target.TryGetValue(group.Key, out List<Error> errors))
                    {
                        errors = new List<Error>();
                        target[group.Key] = errors;
                    }

                    if (!errors.Contains(error))
                    {
                        errors.Add(error);
                    }
                }
            }
        }

        private static WzImage FindMapImage(string mapId)
        {
            if (Program.InfoManager.MapsCache.TryGetValue(
                    mapId,
                    out Tuple<WzImage, string, string, string, MapInfo> cachedMap) &&
                cachedMap.Item1 != null)
            {
                return cachedMap.Item1;
            }

            if (Program.DataSource != null)
            {
                string paddedMapId = mapId.PadLeft(9, '0');
                string folderNumber = paddedMapId[0].ToString();
                string imagePath = $"Map/Map{folderNumber}/{paddedMapId}.img";

                WzImage mapImage = Program.DataSource.GetImageByPath($"Map/{imagePath}") ??
                    Program.DataSource.GetImage("Map", imagePath);
                if (mapImage != null)
                {
                    return mapImage;
                }
            }

            return Program.WzManager == null
                ? null
                : WzInfoTools.FindMapImage(mapId, Program.WzManager);
        }
    }
}
