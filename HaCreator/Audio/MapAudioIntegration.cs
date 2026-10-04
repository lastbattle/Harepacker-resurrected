using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.Audio
{
    public sealed class MapAudioReference
    {
        public string Role { get; init; }
        public string Path { get; init; }
        public AudioAssetEntry Asset { get; init; }
        public bool IsMissing => Asset == null;
    }

    public sealed class MapAudioUsage
    {
        public string MapId { get; init; }
        public MapInfo Map { get; init; }
        public string Role { get; init; }
        public string OriginalPath { get; init; }
    }

    /// <summary>
    /// Map-facing adapter over the shared Sound catalog.  It deliberately
    /// leaves MapInfo's raw bgmSub tree untouched while exposing recognized
    /// primary/ambient references for editor and AI workflows.
    /// </summary>
    public static class MapAudioCatalogIntegration
    {
        public static IReadOnlyList<MapAudioReference> GetReferences(
            MapInfo map,
            IAudioAssetCatalog catalog)
        {
            if (map == null || catalog == null)
                return Array.Empty<MapAudioReference>();
            MapAudioInfo audio = map.Audio ?? new MapAudioInfo { PrimaryBgm = map.bgm };
            var references = new List<MapAudioReference>();
            AddReference(references, "PrimaryBgm", audio.PrimaryBgm ?? map.bgm, catalog);
            AddReference(references, "AmbientBgm", audio.AmbientBgm, catalog);
            if (audio.BgmSub != null)
            {
                foreach ((string path, string propertyPath) in EnumerateBgmSubReferences(audio.BgmSub))
                    AddReference(references, $"BgmSub/{propertyPath}", path, catalog);
            }
            return references;
        }

        public static AudioAssetEntry ResolvePrimary(MapInfo map, IAudioAssetCatalog catalog)
            => Resolve(catalog, map?.Audio?.PrimaryBgm ?? map?.bgm);

        public static AudioAssetEntry ResolveAmbient(MapInfo map, IAudioAssetCatalog catalog)
            => Resolve(catalog, map?.Audio?.AmbientBgm);

        public static bool SetPrimary(MapInfo map, string path, IAudioAssetCatalog catalog = null)
        {
            if (map == null || string.IsNullOrWhiteSpace(path))
                return false;
            if (catalog != null && Resolve(catalog, path) == null)
                return false;
            map.SetPrimaryBgm(path.Trim());
            return true;
        }

        public static bool SetAmbient(
            MapInfo map,
            string path,
            int? volume = null,
            IAudioAssetCatalog catalog = null)
        {
            if (map == null)
                return false;
            if (!string.IsNullOrWhiteSpace(path) && catalog != null && Resolve(catalog, path) == null)
                return false;
            map.SetAmbientBgm(string.IsNullOrWhiteSpace(path) ? null : path.Trim(), volume);
            return true;
        }

        public static IEnumerable<WzImageProperty> EnumerateRawBgmSub(MapInfo map)
        {
            WzImageProperty root = map?.Audio?.BgmSub;
            if (root == null)
                yield break;
            yield return root;
            foreach (WzImageProperty child in EnumerateChildren(root))
                yield return child;
        }

        /// <summary>Returns loaded maps that reference a catalog asset.</summary>
        public static IReadOnlyList<MapAudioUsage> FindMapsUsingAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || HaCreator.Program.InfoManager?.MapsCache == null)
                return Array.Empty<MapAudioUsage>();
            string normalized = NormalizeMapPath(assetPath);
            var usages = new List<MapAudioUsage>();
            foreach (var pair in HaCreator.Program.InfoManager.MapsCache)
            {
                MapInfo map = pair.Value?.Item5;
                if (map == null)
                    continue;
                MapAudioInfo audio = map.Audio;
                string primary = audio?.PrimaryBgm ?? map.bgm;
                if (string.Equals(NormalizeMapPath(primary), normalized, StringComparison.OrdinalIgnoreCase))
                    usages.Add(new MapAudioUsage { MapId = pair.Key, Map = map, Role = "PrimaryBgm", OriginalPath = primary });
                if (!string.IsNullOrWhiteSpace(audio?.AmbientBgm) &&
                    string.Equals(NormalizeMapPath(audio.AmbientBgm), normalized, StringComparison.OrdinalIgnoreCase))
                    usages.Add(new MapAudioUsage { MapId = pair.Key, Map = map, Role = "AmbientBgm", OriginalPath = audio.AmbientBgm });
                if (audio?.BgmSub != null)
                {
                    foreach ((string path, string propertyPath) in EnumerateBgmSubReferences(audio.BgmSub))
                    {
                        if (string.Equals(NormalizeMapPath(path), normalized, StringComparison.OrdinalIgnoreCase))
                            usages.Add(new MapAudioUsage { MapId = pair.Key, Map = map, Role = $"BgmSub/{propertyPath}", OriginalPath = path });
                    }
                }
            }
            return usages;
        }

        private static IEnumerable<WzImageProperty> EnumerateChildren(WzImageProperty parent)
        {
            WzPropertyCollection children;
            try { children = parent.WzProperties; }
            catch { yield break; }
            if (children == null)
                yield break;
            foreach (WzImageProperty child in children)
            {
                yield return child;
                foreach (WzImageProperty nested in EnumerateChildren(child))
                    yield return nested;
            }
        }

        private static IEnumerable<(string Path, string PropertyPath)> EnumerateBgmSubReferences(
            WzImageProperty root)
        {
            var visited = new HashSet<WzObject>();
            foreach ((WzImageProperty property, string propertyPath) in EnumerateBgmSubProperties(root, string.Empty, visited))
            {
                string value = property switch
                {
                    WzStringProperty text => text.Value,
                    WzUOLProperty link => link.Value,
                    _ => null,
                };
                if (!string.IsNullOrWhiteSpace(value))
                    yield return (value, propertyPath);
            }
        }

        private static IEnumerable<(WzImageProperty Property, string PropertyPath)> EnumerateBgmSubProperties(
            WzImageProperty node,
            string prefix,
            ISet<WzObject> visited)
        {
            if (node == null || !visited.Add(node))
                yield break;
            yield return (node, prefix);
            WzPropertyCollection children;
            try { children = node.WzProperties; }
            catch { yield break; }
            if (children == null)
                yield break;
            foreach (WzImageProperty child in children)
            {
                string path = string.IsNullOrEmpty(prefix) ? child.Name : $"{prefix}/{child.Name}";
                foreach (var item in EnumerateBgmSubProperties(child, path, visited))
                    yield return item;
            }
        }

        private static void AddReference(
            ICollection<MapAudioReference> output,
            string role,
            string path,
            IAudioAssetCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            output.Add(new MapAudioReference
            {
                Role = role,
                Path = path,
                Asset = Resolve(catalog, path)
            });
        }

        private static AudioAssetEntry Resolve(IAudioAssetCatalog catalog, string path)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(path))
                return null;
            AudioAssetEntry entry = catalog.Find(path);
            if (entry != null)
                return entry;
            string normalized = path.Replace('\\', '/').Trim('/');
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                return null;
            int offset = string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (segments.Length <= offset + 1)
                return null;
            int imageEnd = Array.FindIndex(segments, offset,
                segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageEnd < offset)
                imageEnd = offset;
            string imagePath = string.Join('/', segments.Skip(offset).Take(imageEnd - offset + 1));
            string propertyPath = string.Join('/', segments.Skip(imageEnd + 1));
            return catalog.Find(imagePath, propertyPath);
        }

        private static string NormalizeMapPath(string path)
        {
            string normalized = path?.Replace('\\', '/').Trim('/');
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;
            string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return string.Empty;
            if (string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase))
                segments = segments.Skip(1).ToArray();
            if (segments.Length == 0)
                return "Sound";
            int imageEnd = Array.FindIndex(segments,
                segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageEnd < 0)
                imageEnd = 0;
            if (!segments[imageEnd].EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                segments[imageEnd] += ".img";
            return "Sound/" + string.Join('/', segments);
        }
    }
}
