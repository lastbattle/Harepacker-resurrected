using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using HaCreator.MapEditor.Info;
using HaCreator.GUI.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace HaCreator.Wz
{
    public enum MapImportAssetKind { Map, Tile, Object, Background, Reactor, Mob, Npc, String, Sound }
    public enum MapImportAssetStatus { ToAdd, Replace, Existing, Missing, Conflict }

    public sealed class MapImportAsset
    {
        internal IDataSource Source { get; set; }
        public MapImportAssetKind Kind { get; init; }
        public MapImportAssetStatus Status { get; internal set; }
        public string Category { get; init; }
        public string RelativePath { get; init; }
        public string EntryPath { get; init; }
        public string DisplayName { get; init; }
        public string KindDisplay => DialogTextExtension.Get($"MapImport_Kind_{Kind}");
        public string StatusDisplay => DialogTextExtension.Get($"MapImport_Status_{Status}");
        public string DisplayPath => string.IsNullOrWhiteSpace(EntryPath)
            ? $"{Category}/{RelativePath}"
            : $"{Category}/{RelativePath}  →  {EntryPath}";
    }

    public sealed class MapImportPlan
    {
        public string MapId { get; internal set; }
        public string SourceMapPath { get; internal set; }
        public List<MapImportAsset> Assets { get; } = new();
    }

    public sealed class MapImportProgress
    {
        public int Completed { get; init; }
        public int Total { get; init; }
        public string CurrentAsset { get; init; }
    }

    public sealed class MapImportMapLabel
    {
        public string MapId { get; init; }
        public string StreetName { get; init; }
        public string MapName { get; init; }
    }

    public sealed class MapImportResult
    {
        public int AddedAssetCount { get; internal set; }
        public int SkippedAssetCount { get; internal set; }
        public List<string> Errors { get; } = new();
    }

    /// <summary>
    /// Builds an explicit, non-destructive import plan for maps from another IMG source.
    /// Aggregate String and Sound images are merged by entry rather than replaced.
    /// </summary>
    public sealed class MapImportService
    {
        private readonly IDataSource _target;
        private readonly WzInformationManager _infoManager;

        public MapImportService(IDataSource target, WzInformationManager infoManager = null)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            if (target is not ImgFileSystemDataSource &&
                (target is not HybridDataSource hybrid || hybrid.ImgSource == null))
                throw new InvalidOperationException(DialogTextExtension.Get("MapImport_WritableDestination"));
            _infoManager = infoManager;
        }

        public static IDataSource OpenSource(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                throw new DirectoryNotFoundException(DialogTextExtension.Format("MapImport_SourceMissing", path));
            return new ImgFileSystemDataSource(path);
        }

        public static IReadOnlyDictionary<string, MapImportMapLabel> GetMapLabels(IDataSource source)
        {
            var labels = new Dictionary<string, MapImportMapLabel>(StringComparer.OrdinalIgnoreCase);
            WzImage image = GetImage(source, "String", "Map.img");
            if (image == null) return labels;
            image.ParseImage();
            CollectMapLabels(image.WzProperties, labels);
            return labels;
        }

        private static void CollectMapLabels(IEnumerable<WzImageProperty> properties,
            IDictionary<string, MapImportMapLabel> labels)
        {
            foreach (WzImageProperty property in properties)
            {
                if (property.Name.Length <= 9 && property.Name.All(char.IsDigit) &&
                    (property["mapName"] != null || property["streetName"] != null))
                {
                    string mapId = property.Name.PadLeft(9, '0');
                    labels[mapId] = new MapImportMapLabel
                    {
                        MapId = mapId,
                        StreetName = GetValue(property["streetName"]) ?? string.Empty,
                        MapName = GetValue(property["mapName"]) ?? string.Empty
                    };
                    continue;
                }
                if (property.WzProperties != null)
                    CollectMapLabels(property.WzProperties, labels);
            }
        }

        public MapImportPlan Analyze(IDataSource source, string mapId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string paddedId = NormalizeMapId(mapId);
            var plan = new MapImportPlan { MapId = paddedId };
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AnalyzeMap(source, paddedId, plan, keys,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), true);
            return plan;
        }

        private void AnalyzeMap(IDataSource source, string mapId, MapImportPlan plan, ISet<string> keys,
            ISet<string> visitedMaps, ISet<string> visitedLifeAssets, bool selectedMap)
        {
            if (!visitedMaps.Add(mapId)) return;
            string mapPath = $"Map/Map{mapId[0]}/{mapId}.img";
            WzImage map = GetImage(source, "Map", mapPath);
            if (map == null)
            {
                AddAsset(plan, keys, source, MapImportAssetKind.Map, "Map", mapPath, null, mapId, MapImportAssetStatus.Missing);
                return;
            }

            if (selectedMap) plan.SourceMapPath = mapPath;
            AddStandalone(plan, keys, source, MapImportAssetKind.Map, "Map", mapPath, mapId);
            map.ParseImage();

            string link = GetValue(map.GetFromPath("info/link"));
            if (int.TryParse(link, out int linkedId) && linkedId > 0)
                AnalyzeMap(source, linkedId.ToString("D9"), plan, keys, visitedMaps, visitedLifeAssets, false);

            foreach (WzImageProperty layer in map.WzProperties.Where(p => p.Name.All(char.IsDigit)))
            {
                string tileSet = GetValue(layer.GetFromPath("info/tS"));
                if (!string.IsNullOrWhiteSpace(tileSet))
                {
                    WzImageProperty tiles = layer["tile"];
                    if (tiles?.WzProperties != null)
                        foreach (WzImageProperty tile in tiles.WzProperties)
                        {
                            string unit = GetValue(tile["u"]);
                            string number = GetValue(tile["no"]);
                            string tilePath = string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(number)
                                ? null : $"{unit}/{number}";
                            if (tilePath != null && GetImage(_target, "Map", $"Tile/{tileSet}.img") != null)
                                AddAggregateEntry(plan, keys, source, MapImportAssetKind.Tile, "Map", $"Tile/{tileSet}.img", tilePath, $"Tile {tileSet}/{tilePath}");
                        }
                    if (GetImage(_target, "Map", $"Tile/{tileSet}.img") == null)
                        AddStandalone(plan, keys, source, MapImportAssetKind.Tile, "Map", $"Tile/{tileSet}.img", tileSet);
                }

                WzImageProperty objects = layer["obj"];
                if (objects?.WzProperties == null) continue;
                foreach (WzImageProperty obj in objects.WzProperties)
                {
                    string objectSet = GetValue(obj["oS"]);
                    if (string.IsNullOrWhiteSpace(objectSet)) continue;
                    string l0 = GetValue(obj["l0"]);
                    string l1 = GetValue(obj["l1"]);
                    string l2 = GetValue(obj["l2"]);
                    string objectPath = $"{l0}/{l1}/{l2}";
                    if (string.IsNullOrWhiteSpace(l0) || string.IsNullOrWhiteSpace(l1) ||
                        string.IsNullOrWhiteSpace(l2) || GetImage(_target, "Map", $"Obj/{objectSet}.img") == null)
                        AddStandalone(plan, keys, source, MapImportAssetKind.Object, "Map", $"Obj/{objectSet}.img", objectSet);
                    else
                        AddAggregateEntry(plan, keys, source, MapImportAssetKind.Object, "Map", $"Obj/{objectSet}.img", objectPath, $"Object {objectSet}/{objectPath}");
                }
            }

            WzImageProperty backgrounds = map["back"];
            if (backgrounds?.WzProperties != null)
                foreach (WzImageProperty background in backgrounds.WzProperties)
                {
                    string backgroundSet = GetValue(background["bS"]);
                    if (string.IsNullOrWhiteSpace(backgroundSet)) continue;
                    string no = GetValue(background["no"]);
                    string type = !string.IsNullOrWhiteSpace(GetValue(background["spineAni"])) ? "spine" :
                        GetValue(background["ani"]) == "1" ? "ani" : "back";
                    string backgroundPath = $"{type}/{no}";
                    if (string.IsNullOrWhiteSpace(no) || GetImage(_target, "Map", $"Back/{backgroundSet}.img") == null)
                        AddStandalone(plan, keys, source, MapImportAssetKind.Background, "Map", $"Back/{backgroundSet}.img", backgroundSet);
                    else
                        AddAggregateEntry(plan, keys, source, MapImportAssetKind.Background, "Map", $"Back/{backgroundSet}.img", backgroundPath, $"Background {backgroundSet}/{backgroundPath}");
                }

            foreach (string miscObjectPath in new[] { "shipObj/shipObj", "healer/healer", "pulley/pulley" })
                AddObjectPathReference(plan, keys, source, GetValue(map.GetFromPath(miscObjectPath)));

            WzImageProperty life = map["life"];
            if (life?.WzProperties != null)
            {
                // Life entries are usually direct children, but newer exports may
                // group them below category/section nodes. Walk the complete life
                // subtree so nested NPC and mob rows are not silently omitted.
                foreach (WzImageProperty entry in EnumerateLifeEntries(life))
                {
                    string id = GetValue(entry["id"]);
                    string type = GetValue(entry["type"])?.Trim();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (string.Equals(type, "n", StringComparison.OrdinalIgnoreCase))
                    {
                        AddLifeAsset(plan, keys, source, visitedLifeAssets,
                            MapImportAssetKind.Npc, "Npc", id,
                            DialogTextExtension.Format("MapImport_Npc", id));
                    }
                    else if (string.Equals(type, "m", StringComparison.OrdinalIgnoreCase))
                    {
                        AddLifeAsset(plan, keys, source, visitedLifeAssets,
                            MapImportAssetKind.Mob, "Mob", id,
                            DialogTextExtension.Format("MapImport_Mob", id));
                    }
                }
            }

            WzImageProperty reactors = map["reactor"];
            if (reactors?.WzProperties != null)
                foreach (string id in reactors.WzProperties.Select(p => GetValue(p["id"]))
                    .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))
                    AddLifeAsset(plan, keys, source, visitedLifeAssets,
                        MapImportAssetKind.Reactor, "Reactor", id, id);

            AddMapString(plan, keys, source, mapId);
            if (map["ToolTip"] != null || map["tooltip"] != null)
                AddAggregateEntry(plan, keys, source, MapImportAssetKind.String, "String", "ToolTipHelp.img",
                    $"Mapobject/{int.Parse(mapId)}", DialogTextExtension.Format("MapImport_Tooltips", mapId));

            string bgm = GetValue(map.GetFromPath("info/bgm"));
            int separator = bgm?.IndexOf('/') ?? -1;
            if (separator > 0 && separator < bgm.Length - 1)
                AddAggregateEntry(plan, keys, source, MapImportAssetKind.Sound, "Sound",
                    bgm.Substring(0, separator) + ".img", bgm.Substring(separator + 1), bgm);
        }

        private void AddMapString(MapImportPlan plan, ISet<string> keys, IDataSource source, string mapId)
        {
            WzImage image = GetImage(source, "String", "Map.img");
            if (image == null)
            {
                AddAsset(plan, keys, source, MapImportAssetKind.String, "String", "Map.img", mapId,
                    DialogTextExtension.Format("MapImport_MapNameFormat", mapId), MapImportAssetStatus.Missing);
                return;
            }
            image.ParseImage();
            WzImageProperty entry = FindMapStringEntry(image.WzProperties, mapId);
            string entryPath = entry == null ? mapId : GetPropertyPath(entry);
            AddAggregateEntry(plan, keys, source, MapImportAssetKind.String, "String", "Map.img", entryPath, DialogTextExtension.Format("MapImport_MapNameFormat", mapId));
        }

        private void AddStandalone(MapImportPlan plan, ISet<string> keys, IDataSource source,
            MapImportAssetKind kind, string category, string relativePath, string displayName)
        {
            bool sourceExists = GetImage(source, category, relativePath) != null;
            bool targetExists = GetImage(_target, category, relativePath) != null;
            MapImportAssetStatus status = !sourceExists ? MapImportAssetStatus.Missing :
                targetExists ? MapImportAssetStatus.Conflict : MapImportAssetStatus.ToAdd;
            AddAsset(plan, keys, source, kind, category, relativePath, null, displayName, status);
        }

        private void AddLifeAsset(MapImportPlan plan, ISet<string> keys, IDataSource source,
            ISet<string> visitedLifeAssets, MapImportAssetKind kind, string category,
            string id, string displayName)
        {
            if (!TryNormalizeLifeId(id, out string normalizedId))
                return;

            string visitKey = $"{category}|{normalizedId}";
            if (!visitedLifeAssets.Add(visitKey))
                return;

            string relativePath = $"{normalizedId}.img";
            AddStandalone(plan, keys, source, kind, category, relativePath, displayName);

            // NPC and mob names live in aggregate String images. Keep the linked
            // template's string row available as well: the runtime may resolve the
            // linked image for rendering or metadata even when the map references
            // the child template.
            if (kind == MapImportAssetKind.Npc)
                AddNumericAggregateEntry(plan, keys, source, MapImportAssetKind.String,
                    "String", "Npc.img", normalizedId, DialogTextExtension.Format("MapImport_Npc", normalizedId));
            else if (kind == MapImportAssetKind.Mob)
                AddNumericAggregateEntry(plan, keys, source, MapImportAssetKind.String,
                    "String", "Mob.img", normalizedId, DialogTextExtension.Format("MapImport_Mob", normalizedId));

            WzImage image = GetImage(source, category, relativePath);
            string link = GetValue(image?.GetFromPath("info/link"));
            if (TryNormalizeLifeId(link, out string linkedId) &&
                !string.Equals(normalizedId, linkedId, StringComparison.OrdinalIgnoreCase))
            {
                AddLifeAsset(plan, keys, source, visitedLifeAssets, kind, category,
                    linkedId, kind == MapImportAssetKind.Npc
                        ? DialogTextExtension.Format("MapImport_Npc", linkedId)
                        : kind == MapImportAssetKind.Mob
                            ? DialogTextExtension.Format("MapImport_Mob", linkedId)
                            : linkedId);
            }
        }

        private static bool TryNormalizeLifeId(string value, out string normalizedId)
        {
            normalizedId = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string candidate = value.Trim();
            int separator = Math.Max(candidate.LastIndexOf('/'), candidate.LastIndexOf('\\'));
            if (separator >= 0 && separator < candidate.Length - 1)
                candidate = candidate.Substring(separator + 1);
            candidate = Path.GetFileNameWithoutExtension(candidate);
            if (candidate.Length == 0 || candidate.Length > 7 || !candidate.All(char.IsDigit))
                return false;

            normalizedId = candidate.PadLeft(7, '0');
            return true;
        }

        private void AddObjectPathReference(MapImportPlan plan, ISet<string> keys, IDataSource source, string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath)) return;
            string[] parts = objectPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) return;
            string objectSet = Path.GetFileNameWithoutExtension(parts[^4]);
            string entryPath = $"{parts[^3]}/{parts[^2]}/{parts[^1]}";
            string relativePath = $"Obj/{objectSet}.img";
            if (GetImage(_target, "Map", relativePath) == null)
                AddStandalone(plan, keys, source, MapImportAssetKind.Object, "Map", relativePath, objectSet);
            else
                AddAggregateEntry(plan, keys, source, MapImportAssetKind.Object, "Map", relativePath, entryPath,
                    $"Object {objectSet}/{entryPath}");
        }

        private void AddAggregateEntry(MapImportPlan plan, ISet<string> keys, IDataSource source,
            MapImportAssetKind kind, string category, string relativePath, string entryPath, string displayName)
        {
            WzImage sourceImage = GetImage(source, category, relativePath);
            WzImageProperty sourceEntry = GetProperty(sourceImage, entryPath);
            WzImage targetImage = GetImage(_target, category, relativePath);
            WzImageProperty targetEntry = GetProperty(targetImage, entryPath);
            MapImportAssetStatus status = sourceEntry == null ? MapImportAssetStatus.Missing :
                targetEntry != null ? MapImportAssetStatus.Existing : MapImportAssetStatus.ToAdd;
            AddAsset(plan, keys, source, kind, category, relativePath, entryPath, displayName, status);
        }

        private void AddNumericAggregateEntry(MapImportPlan plan, ISet<string> keys, IDataSource source,
            MapImportAssetKind kind, string category, string relativePath, string numericId, string displayName)
        {
            WzImage image = GetImage(source, category, relativePath);
            string entryPath = FindNumericEntryPath(image, numericId) ?? numericId;
            if (FindNumericEntryPath(GetImage(_target, category, relativePath), numericId) != null)
            {
                AddAsset(plan, keys, source, kind, category, relativePath, entryPath, displayName, MapImportAssetStatus.Existing);
                return;
            }
            AddAggregateEntry(plan, keys, source, kind, category, relativePath, entryPath, displayName);
        }

        private static void AddAsset(MapImportPlan plan, ISet<string> keys, IDataSource source,
            MapImportAssetKind kind, string category, string relativePath, string entryPath,
            string displayName, MapImportAssetStatus status)
        {
            string key = $"{kind}|{category}|{relativePath}|{entryPath}";
            if (!keys.Add(key)) return;
            plan.Assets.Add(new MapImportAsset
            {
                Source = source, Kind = kind, Category = category, RelativePath = relativePath,
                EntryPath = entryPath, DisplayName = displayName, Status = status
            });
        }

        public MapImportResult Import(MapImportPlan plan, CancellationToken cancellationToken,
            IProgress<MapImportProgress> progress)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var result = new MapImportResult();
            int completed = 0;
            foreach (MapImportAsset asset in plan.Assets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (asset.Status != MapImportAssetStatus.ToAdd && asset.Status != MapImportAssetStatus.Replace)
                    {
                        result.SkippedAssetCount++;
                    }
                    else if (string.IsNullOrWhiteSpace(asset.EntryPath))
                    {
                        if (asset.Status == MapImportAssetStatus.ToAdd &&
                            GetImage(_target, asset.Category, asset.RelativePath) != null)
                        {
                            asset.Status = MapImportAssetStatus.Existing;
                            result.SkippedAssetCount++;
                            completed++;
                            progress?.Report(new MapImportProgress { Completed = completed, Total = plan.Assets.Count, CurrentAsset = asset.DisplayPath });
                            continue;
                        }
                        WzImage sourceImage = GetImage(asset.Source, asset.Category, asset.RelativePath);
                        if (sourceImage == null || !_target.SaveImage(asset.Category, sourceImage.DeepClone(), asset.RelativePath))
                throw new IOException(DialogTextExtension.Get("MapImport_SaveRejected"));
                        result.AddedAssetCount++;
                        asset.Status = MapImportAssetStatus.Existing;
                    }
                    else
                    {
                        if (MergeEntry(asset)) result.AddedAssetCount++;
                        else result.SkippedAssetCount++;
                        asset.Status = MapImportAssetStatus.Existing;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"{asset.DisplayPath}: {ex.Message}");
                }
                completed++;
                progress?.Report(new MapImportProgress { Completed = completed, Total = plan.Assets.Count, CurrentAsset = asset.DisplayPath });
            }
            RegisterImportedAssets(plan);
            foreach (string mapId in plan.Assets.Where(asset => asset.Kind == MapImportAssetKind.Map && asset.Status != MapImportAssetStatus.Missing)
                .Select(asset => Path.GetFileNameWithoutExtension(asset.RelativePath)).Distinct(StringComparer.OrdinalIgnoreCase))
                RefreshMapCache(mapId);
            return result;
        }

        private bool MergeEntry(MapImportAsset asset)
        {
            WzImage sourceImage = GetImage(asset.Source, asset.Category, asset.RelativePath);
            WzImageProperty sourceEntry = GetProperty(sourceImage, asset.EntryPath);
            if (sourceEntry == null) throw new InvalidDataException(DialogTextExtension.Get("MapImport_SourceEntryMissing"));

            WzImage targetImage = GetImage(_target, asset.Category, asset.RelativePath) ?? new WzImage(Path.GetFileName(asset.RelativePath));
            targetImage.ParseImage();
            string[] parts = asset.EntryPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            IPropertyContainer container = targetImage;
            WzImageProperty current = null;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                current = current == null ? targetImage[parts[i]] : current[parts[i]];
                if (current == null)
                {
                    var created = new WzSubProperty(parts[i]);
                    container.AddProperty(created);
                    current = created;
                }
                if (current is not IPropertyContainer next)
                    throw new InvalidDataException(DialogTextExtension.Format("MapImport_NotContainer", parts[i]));
                container = next;
            }
            if ((current == null ? targetImage[parts[^1]] : current[parts[^1]]) != null)
                return false;
            container.AddProperty(sourceEntry.DeepClone());
            targetImage.Changed = true;
            if (!_target.SaveImage(asset.Category, targetImage, asset.RelativePath))
                throw new IOException(DialogTextExtension.Get("MapImport_MergeSaveFailed"));
            return true;
        }

        private void RegisterImportedAssets(MapImportPlan plan)
        {
            if (_infoManager == null) return;
            foreach (MapImportAsset asset in plan.Assets.Where(asset => asset.Status != MapImportAssetStatus.Missing))
            {
                string setName = Path.GetFileNameWithoutExtension(asset.RelativePath);
                if (asset.Kind == MapImportAssetKind.Tile) _infoManager.AddTileSet(setName);
                else if (asset.Kind == MapImportAssetKind.Object) _infoManager.AddObjectSet(setName);
                else if (asset.Kind == MapImportAssetKind.Background) _infoManager.AddBackgroundSet(setName);
                else if (asset.Kind == MapImportAssetKind.Reactor && !_infoManager.Reactors.ContainsKey(setName))
                {
                    WzImage image = GetImage(_target, "Reactor", asset.RelativePath);
                    if (image != null)
                    {
                        image.ParseImage();
                        WzSubProperty info = image["info"] as WzSubProperty;
                        string name = GetValue(info?["info"]) ?? GetValue(info?["viewName"]) ?? string.Empty;
                        _infoManager.Reactors[setName] = new ReactorInfo(null, new System.Drawing.Point(), setName, name, image);
                    }
                }
                else if (asset.Kind == MapImportAssetKind.Sound && !string.IsNullOrWhiteSpace(asset.EntryPath))
                {
                    string key = $"{setName}/{asset.EntryPath.Replace('\\', '/')}";
                    _infoManager.BGMs[key] = new WzInformationManager.BgmEntry(Path.GetFileName(asset.RelativePath), asset.EntryPath);
                }
            }
        }

        private void RefreshMapCache(string mapId)
        {
            if (_infoManager == null) return;
            string mapPath = $"Map/Map{mapId[0]}/{mapId}.img";
            WzImage map = GetImage(_target, "Map", mapPath);
            if (map == null) return;
            WzImage strings = GetImage(_target, "String", "Map.img");
            strings?.ParseImage();
            WzImageProperty entry = strings == null ? null : FindMapStringEntry(strings.WzProperties, mapId);
            string street = GetValue(entry?["streetName"]) ?? string.Empty;
            string name = GetValue(entry?["mapName"]) ?? mapId;
            string category = entry?.Parent?.Name ?? "Imported";
            var info = new MapInfo(map, street, name, category);
            _infoManager.MapsNameCache[mapId] = Tuple.Create(street, name, category);
            _infoManager.MapsCache[mapId] = Tuple.Create(map, street, name, category, info);
        }

        private static WzImage GetImage(IDataSource source, string category, string relativePath) =>
            source?.GetImage(category, relativePath) ?? source?.GetImageByPath($"{category}/{relativePath}");

        private static WzImageProperty GetProperty(WzImage image, string path)
        {
            if (image == null || string.IsNullOrWhiteSpace(path)) return null;
            image.ParseImage();
            return image.GetFromPath(path.Replace('\\', '/'));
        }

        private static string GetValue(WzImageProperty property)
        {
            if (property == null) return null;
            try { return property.GetString(); }
            catch { return property.WzValue?.ToString(); }
        }

        private static WzImageProperty FindPropertyRecursive(IEnumerable<WzImageProperty> properties, string name)
        {
            foreach (WzImageProperty property in properties)
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) return property;
                if (property.WzProperties == null) continue;
                WzImageProperty found = FindPropertyRecursive(property.WzProperties, name);
                if (found != null) return found;
            }
            return null;
        }

        private static IEnumerable<WzImageProperty> EnumerateLifeEntries(WzImageProperty life)
        {
            if (life?.WzProperties == null)
                yield break;

            foreach (WzImageProperty property in life.WzProperties)
            {
                // A life row is identified by both fields. Continue walking after
                // yielding it because some exports put additional rows below the
                // same grouping node.
                if (property["id"] != null && property["type"] != null)
                    yield return property;

                if (property.WzProperties == null)
                    continue;

                foreach (WzImageProperty nested in EnumerateLifeEntries(property))
                    yield return nested;
            }
        }

        private static WzImageProperty FindMapStringEntry(IEnumerable<WzImageProperty> properties, string mapId)
        {
            string normalizedId = mapId.TrimStart('0');
            if (normalizedId.Length == 0) normalizedId = "0";
            foreach (WzImageProperty property in properties)
            {
                string normalizedName = property.Name.TrimStart('0');
                if (normalizedName.Length == 0) normalizedName = "0";
                if (property.Name.All(char.IsDigit) && normalizedName == normalizedId &&
                    (property["mapName"] != null || property["streetName"] != null))
                    return property;
                if (property.WzProperties == null) continue;
                WzImageProperty found = FindMapStringEntry(property.WzProperties, mapId);
                if (found != null) return found;
            }
            return null;
        }

        private static string FindNumericEntryPath(WzImage image, string numericId)
        {
            if (image == null) return null;
            image.ParseImage();
            string normalized = numericId.TrimStart('0');
            if (normalized.Length == 0) normalized = "0";
            WzImageProperty found = FindNumericEntry(image.WzProperties, normalized);
            return found == null ? null : GetPropertyPath(found);
        }

        private static WzImageProperty FindNumericEntry(IEnumerable<WzImageProperty> properties, string normalized)
        {
            foreach (WzImageProperty property in properties)
            {
                string value = property.Name.TrimStart('0');
                if (value.Length == 0) value = "0";
                if (property.Name.All(char.IsDigit) && value == normalized) return property;
                if (property.WzProperties != null)
                {
                    WzImageProperty found = FindNumericEntry(property.WzProperties, normalized);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static string GetPropertyPath(WzImageProperty property)
        {
            var parts = new Stack<string>();
            WzObject current = property;
            while (current is WzImageProperty)
            {
                parts.Push(current.Name);
                current = current.Parent;
            }
            return string.Join("/", parts);
        }

        private static string NormalizeMapId(string mapId)
        {
            if (string.IsNullOrWhiteSpace(mapId) || !mapId.Trim().All(char.IsDigit) || mapId.Trim().Length > 9)
                throw new ArgumentException(DialogTextExtension.Get("MapImport_InvalidMapId"), nameof(mapId));
            return mapId.Trim().PadLeft(9, '0');
        }
    }
}
