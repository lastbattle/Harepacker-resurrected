using System;
using System.Collections.Generic;
using System.Linq;
using MapleLib.WzLib;

namespace HaCreator.MapEditor.AI
{
    /// <summary>Metadata-only image discovery; properties are loaded only when browsing an image.</summary>
    internal sealed class WzMentionCatalog
    {
        internal sealed record Entry(string Path, string Name, bool CanBrowse)
        {
            public string Display => string.IsNullOrEmpty(Name) ? Path : $"{Name} — {Path}";
        }

        private readonly List<Entry> entries = new();
        private readonly Dictionary<string, WzImage> legacyImages = new(StringComparer.OrdinalIgnoreCase);

        public WzMentionCatalog()
        {
            var source = Program.DataSource;
            if (source != null && source is not MapleLib.Img.WzFileDataSource)
            {
                foreach (string category in source.GetCategories())
                {
                    Add(category + "/", true);
                    foreach (string directory in source.GetSubdirectories(category).Prepend("").Distinct())
                    {
                        string prefix = category + "/" + (directory.Length == 0 ? "" : directory.Replace('\\', '/').Trim('/') + "/");
                        if (directory.Length > 0) Add(prefix, true);
                        foreach (string name in source.GetImageNamesInDirectory(category, directory))
                            Add(prefix + (name.EndsWith(".img", StringComparison.OrdinalIgnoreCase) ? name : name + ".img"), true);
                    }
                }
            }
            else
            {
                // Traverse every split WZ root, including subdirectories absent from the first archive.
                foreach (string category in MapleLib.Img.ImgFileSystemManager.STANDARD_CATEGORIES)
                    foreach (var root in Program.GetDirectories(category))
                        Visit(root, category + "/");
            }
            entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Path, b.Path));
        }

        private void Visit(WzDirectory directory, string prefix)
        {
            Add(prefix, true);
            foreach (var image in directory.WzImages)
            {
                string path = prefix + image.Name;
                legacyImages.TryAdd(path, image);
                Add(path, true);
            }
            foreach (var child in directory.WzDirectories) Visit(child, prefix + child.Name + "/");
        }

        private void Add(string path, bool browse) => entries.Add(new Entry(path, FriendlyName(path), browse));

        private static string FriendlyName(string path)
        {
            var info = Program.InfoManager;
            if (info == null) return "";
            string id = System.IO.Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith("Map/Map/", StringComparison.OrdinalIgnoreCase) && info.MapsNameCache.TryGetValue(id, out var map)) return map.Item1 + " / " + map.Item2;
            if (path.StartsWith("Mob/", StringComparison.OrdinalIgnoreCase) && info.MobNameCache.TryGetValue(id, out var mob)) return mob;
            if (path.StartsWith("Npc/", StringComparison.OrdinalIgnoreCase) && info.NpcNameCache.TryGetValue(id, out var npc)) return npc.Item1;
            if ((path.StartsWith("Character/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Item/", StringComparison.OrdinalIgnoreCase)) && int.TryParse(id, out int itemId) && info.ItemNameCache.TryGetValue(itemId, out var item)) return item.Item2;
            return "";
        }

        public List<Entry> Search(string query)
        {
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["maps"] = "Map/Map/", ["objects"] = "Map/Obj/", ["backgrounds"] = "Map/Back/", ["tiles"] = "Map/Tile/", ["monsters"] = "Mob/", ["npcs"] = "Npc/", ["reactors"] = "Reactor/", ["strings"] = "String/", ["items"] = "Item/", ["equipment"] = "Character/", ["equipments"] = "Character/" };
            string first = query.Split(' ', '/')[0];
            string scope = null;
            string scopedName = null;
            if (aliases.TryGetValue(first, out string prefix))
            {
                scope = prefix;
                scopedName = query.Substring(first.Length).TrimStart(' ', '/');
                query = prefix + scopedName;
            }
            int imageEnd = query.IndexOf(".img/", StringComparison.OrdinalIgnoreCase);
            IEnumerable<Entry> candidates = string.IsNullOrEmpty(query)
                ? entries.Where(e => e.Path.Count(c => c == '/') == 1 && e.Path.EndsWith("/")) : entries;
            string nameQuery = scopedName ?? query;
            if (imageEnd >= 0)
            {
                string imagePath = query.Substring(0, imageEnd + 4);
                var image = legacyImages.GetValueOrDefault(imagePath) ?? Program.DataSource?.GetImageByPath(imagePath);
                if (image == null) return new();
                image.ParseImage();
                string tail = query.Substring(imageEnd + 5);
                int slash = tail.LastIndexOf('/');
                nameQuery = tail.Substring(slash + 1);
                var children = image.WzProperties;
                string parent = imagePath + "/";
                if (slash >= 0)
                {
                    foreach (string part in tail.Substring(0, slash).Split('/'))
                    {
                        children = children?.FirstOrDefault(p => p.Name == part)?.WzProperties;
                        parent += part + "/";
                    }
                }
                candidates = (children ?? new List<WzImageProperty>()).Select(p => new Entry(parent + p.Name,
                    p.WzValue is string value ? value.Substring(0, Math.Min(value.Length, 120)) : FriendlyName(parent + p.Name),
                    p.WzProperties?.Count > 0));
            }
            return candidates.Where(e => !string.Equals(e.Path, query, StringComparison.OrdinalIgnoreCase) &&
                    (e.Path.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     (!string.IsNullOrEmpty(nameQuery) && (scope == null || e.Path.StartsWith(scope, StringComparison.OrdinalIgnoreCase)) && e.Name.Contains(nameQuery, StringComparison.OrdinalIgnoreCase))))
                .DistinctBy(e => e.Path, StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        }
    }
}
