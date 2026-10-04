using HaCreator.MapEditor.Info;
using HaSharedLibrary.Wz;
using HaCreator.Audio;
using MapleLib;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.Wz
{
    public class WzInformationManager
    {
        public sealed class BgmEntry
        {
            public BgmEntry(string imageName, string propertyPath)
            {
                ImageName = imageName;
                PropertyPath = propertyPath;
            }

            public string ImageName { get; }
            public string PropertyPath { get; }
            public string ImagePath => ImageName;
            public string FullPropertyPath => PropertyPath;
        }

        public Dictionary<string, ReactorInfo> Reactors = new Dictionary<string, ReactorInfo>();

        // Lazy-loading dictionaries for map assets - only load when accessed
        public IDictionary<string, WzImage> TileSets = new Dictionary<string, WzImage>();
        public IDictionary<string, WzImage> ObjectSets = new Dictionary<string, WzImage>();
        public IDictionary<string, WzImage> BackgroundSets = new Dictionary<string, WzImage>();

        public Dictionary<string, BgmEntry> BGMs = new Dictionary<string, BgmEntry>();
        private IAudioAssetCatalog audioCatalog;
        private readonly object deferredExtractionLock = new();

        /// <summary>Shared Sound catalog projection used by map and AI code.</summary>
        public IAudioAssetCatalog AudioCatalog
        {
            get
            {
                if (Program.DataSource == null)
                    return audioCatalog;
                if (audioCatalog == null || !ReferenceEquals(audioCatalog.DataSource, Program.DataSource))
                    audioCatalog = Program.AudioAssetCatalog ?? new AudioAssetCatalog(Program.DataSource);
                return audioCatalog;
            }
        }

        // Maps
        public Dictionary<string, Bitmap> MapMarks = new Dictionary<string, Bitmap>();
        public Dictionary<string, Tuple<string, string, string>> MapsNameCache = new Dictionary<string, Tuple<string, string, string>>(); // street name, map name, category name
        public Dictionary<string, Tuple<WzImage, string, string, string, MapInfo>> MapsCache = new Dictionary<string, Tuple<WzImage, string, string, string, MapInfo>>(); // mapImage, strMapProp, mapName, streetName, categoryName, info

        // Item 
        public Dictionary<int, Tuple<string, string, string>> ItemNameCache = new Dictionary<int, Tuple<string, string, string>>(); // itemid, <item category, item name, item desc>
        public Dictionary<int, WzCanvasProperty> ItemIconCache = new Dictionary<int, WzCanvasProperty>();
        public Dictionary<int, WzImage> EquipItemCache = new Dictionary<int, WzImage>();

        // Mobs
        public Dictionary<string, string> MobNameCache = new();
        public Dictionary<int, WzImageProperty> MobIconCache = new();

        // Skills
        public Dictionary<string, Tuple<string, string>> SkillNameCache = new Dictionary<string, Tuple<string, string>>(); // skillId, <name, desc>
        public Dictionary<string, WzImageProperty> SkillWzImageCache = new Dictionary<string, WzImageProperty>();

        // Npcs
        public Dictionary<string, Tuple<string, string>> NpcNameCache = new Dictionary<string, Tuple<string, string>>();
        public Dictionary<string, WzImage> NpcPropertyCache = new Dictionary<string, WzImage>();

        public Dictionary<PortalType, PortalInfo> Portals = new Dictionary<PortalType, PortalInfo>();
        public List<PortalType> PortalEditor_TypeById = new List<PortalType>();
        public Dictionary<PortalType, int> PortalIdByType = new Dictionary<PortalType, int>();
        public Dictionary<PortalType, PortalGameImageInfo> PortalGame = new Dictionary<PortalType, PortalGameImageInfo>();

        // Quests
        public Dictionary<string, WzSubProperty> QuestActs = new Dictionary<string, WzSubProperty>();
        public Dictionary<string, WzSubProperty> QuestChecks = new Dictionary<string, WzSubProperty>();
        public Dictionary<string, WzSubProperty> QuestInfos = new Dictionary<string, WzSubProperty>();
        public Dictionary<string, WzSubProperty> QuestSays = new Dictionary<string, WzSubProperty>();


        /// <summary>
        /// Gets a tile set image, loading on-demand if not already loaded.
        /// </summary>
        public WzImage GetTileSet(string name)
        {
            if (string.IsNullOrEmpty(name) || !TileSets.ContainsKey(name))
                return null;

            var image = TileSets[name];
            if (image == null && Program.DataSource != null)
            {
                image = Program.DataSource.GetImage("Map", $"Tile/{name}.img");
                if (image != null)
                {
                    if (!image.Parsed)
                        image.ParseImage();
                    TileSets[name] = image;
                }
            }
            return image;
        }

        /// <summary>
        /// Gets an object set image, loading on-demand if not already loaded.
        /// </summary>
        public WzImage GetObjectSet(string name)
        {
            if (string.IsNullOrEmpty(name) || !ObjectSets.ContainsKey(name))
                return null;

            var image = ObjectSets[name];
            if (image == null && Program.DataSource != null)
            {
                image = Program.DataSource.GetImage("Map", $"Obj/{name}.img");
                if (image != null)
                {
                    if (!image.Parsed)
                        image.ParseImage();
                    ObjectSets[name] = image;
                }
            }
            return image;
        }

        /// <summary>
        /// Gets a background set image, loading on-demand if not already loaded.
        /// </summary>
        public WzImage GetBackgroundSet(string name)
        {
            if (string.IsNullOrEmpty(name) || !BackgroundSets.ContainsKey(name))
                return null;

            var image = BackgroundSets[name];
            if (image == null && Program.DataSource != null)
            {
                image = Program.DataSource.GetImage("Map", $"Back/{name}.img");
                if (image != null)
                {
                    if (!image.Parsed)
                        image.ParseImage();
                    BackgroundSets[name] = image;
                }
            }
            return image;
        }

        /// <summary>
        /// Gets the equipment's WzSubProperty from Character.wz
        /// and caches it to memory
        /// </summary>
        /// <param name="id"></param>
        /// <param name="fileManager"></param>
        /// <returns></returns>
        public WzImage GetItemEquipSubProperty(int id, string categoryName, WzFileManager fileManager = null)
        {
            if (EquipItemCache.ContainsKey(id))
                return EquipItemCache[id];

            WzImage itemObj = null;
            string imageName = WzInfoTools.AddLeadingZeros(id.ToString(), 8) + ".img";

            if (Program.DataSource != null && !string.IsNullOrWhiteSpace(categoryName))
            {
                itemObj = Program.DataSource.GetImage("Character", $"{categoryName}/{imageName}");
            }

            WzDirectory charWzEqpCatDirectory = fileManager?.FindWzImageByName("character", categoryName) as WzDirectory;
            if (charWzEqpCatDirectory != null)
            {
                itemObj ??= charWzEqpCatDirectory[imageName] as WzImage;
            }

            if (itemObj != null)
            {
                lock (EquipItemCache)
                {
                    if (!EquipItemCache.ContainsKey(id))
                        EquipItemCache.Add(id, itemObj);
                }
                return itemObj;
            }
            return null;
        }

        /// <summary>
        /// Gets an item icon from either the IMG data source or the legacy WZ caches.
        /// </summary>
        public WzCanvasProperty GetItemIcon(int id, string categoryName, WzFileManager fileManager = null)
        {
            if (ItemIconCache.TryGetValue(id, out WzCanvasProperty cachedIcon))
                return cachedIcon;

            WzCanvasProperty icon = null;
            if (MapleLib.WzLib.WzStructure.Data.ItemStructure.ItemIdsCategory.IsEquipment(id))
            {
                WzImage equipmentImage = GetItemEquipSubProperty(id, categoryName, fileManager);
                icon = equipmentImage?["info"]?["icon"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
            }
            else if (Program.DataSource != null && !string.IsNullOrWhiteSpace(categoryName))
            {
                string paddedId = WzInfoTools.AddLeadingZeros(id.ToString(), 8);
                bool isPet = string.Equals(categoryName, "Pet", StringComparison.OrdinalIgnoreCase);
                string itemDirectory = string.Equals(categoryName, "Ins", StringComparison.OrdinalIgnoreCase)
                    ? "Install"
                    : categoryName;
                string relativePath = isPet
                    ? $"Pet/{id}.img"
                    : $"{itemDirectory}/{paddedId.Substring(0, 4)}.img";

                WzImage itemImage = Program.DataSource.GetImage("Item", relativePath);
                if (isPet)
                {
                    icon = itemImage?["info"]?["icon"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
                }
                else
                {
                    WzImageProperty itemProperty = itemImage?[id.ToString()] ?? itemImage?[paddedId];
                    icon = itemProperty?["info"]?["icon"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
                }
            }

            if (icon != null)
            {
                lock (ItemIconCache)
                {
                    ItemIconCache.TryAdd(id, icon);
                }
            }

            return icon;
        }

        /// <summary>
        /// Gets a mob preview icon, loading the mob IMG on demand when necessary.
        /// </summary>
        public WzCanvasProperty GetMobIcon(int id)
        {
            if (MobIconCache.TryGetValue(id, out WzImageProperty cachedIcon))
                return cachedIcon as WzCanvasProperty;

            WzImage mobImage = Program.FindImage("Mob", WzInfoTools.AddLeadingZeros(id.ToString(), 7) + ".img");
            WzCanvasProperty icon = mobImage?["stand"]?["0"]?.GetLinkedWzImageProperty() as WzCanvasProperty;
            if (icon != null)
            {
                lock (MobIconCache)
                {
                    MobIconCache.TryAdd(id, icon);
                }
            }

            return icon;
        }

        /// <summary>
        /// Gets a skill property, loading its owning skill IMG on demand when necessary.
        /// </summary>
        public WzImageProperty GetSkillProperty(string skillId)
        {
            if (SkillWzImageCache.TryGetValue(skillId, out WzImageProperty cachedSkill))
                return cachedSkill;
            if (!int.TryParse(skillId, out int parsedSkillId))
                return null;

            string groupName = (parsedSkillId / 10000).ToString("D3") + ".img";
            WzImage skillImage = Program.FindImage("Skill", groupName);
            WzImageProperty skillProperty = skillImage?["skill"]?[skillId];
            if (skillProperty != null)
            {
                lock (SkillWzImageCache)
                {
                    SkillWzImageCache.TryAdd(skillId, skillProperty);
                }
            }

            return skillProperty;
        }

        /// <summary>
        /// Clears existing data loaded
        /// </summary>
        public void Clear()
        {
            NpcNameCache.Clear();
            MobNameCache.Clear();
            MobIconCache.Clear();
            Reactors.Clear();
            TileSets.Clear();
            ObjectSets.Clear();
            BackgroundSets.Clear();
            BGMs.Clear();
            audioCatalog = null;
            MapMarks.Clear();
            MapsNameCache.Clear();
            MapsCache.Clear();
            Portals.Clear();
            PortalEditor_TypeById.Clear();
            PortalIdByType.Clear();
            PortalGame.Clear();
        }

        public WzBinaryProperty GetBgm(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (!BGMs.TryGetValue(name, out var entry))
            {
                if (!TryResolveBgmPath(name, out string imagePath, out string propertyPath))
                    return null;

                entry = new BgmEntry(imagePath, propertyPath);
                BGMs[name] = entry;
            }
            if (entry == null)
                return null;

            WzImage image = Program.FindImage("Sound", entry.ImageName)
                ?? Program.DataSource?.GetImage("Sound", entry.ImageName);
            image?.ParseImage();
            if (image == null)
                return null;

            WzImageProperty property = image.GetFromPath(entry.PropertyPath);
            if (property is WzBinaryProperty binary)
                return binary;
            try
            {
                return property?.GetLinkedWzImageProperty() as WzBinaryProperty;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Splits legacy paths such as Bgm00/FloralLife and canonical paths
        /// such as Sound/Bgm00.img/FloralLife without building the global
        /// Sound catalogue.
        /// </summary>
        internal static bool TryResolveBgmPath(string name, out string imagePath, out string propertyPath)
        {
            imagePath = null;
            propertyPath = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string[] segments = name.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            int first = segments.Length > 0 &&
                string.Equals(segments[0], "Sound", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (segments.Length - first < 2)
                return false;

            int imageEnd = Array.FindIndex(segments, first,
                segment => segment.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
            if (imageEnd < first)
                imageEnd = first;
            if (imageEnd >= segments.Length - 1)
                return false;

            imagePath = string.Join('/', segments.Skip(first).Take(imageEnd - first + 1));
            if (!imagePath.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
                imagePath += ".img";
            propertyPath = string.Join('/', segments.Skip(imageEnd + 1));
            return propertyPath.Length > 0;
        }

        /// <summary>
        /// Gets one reactor definition on demand.  Startup keeps only reactor
        /// IDs; image metadata and canvas data are loaded when a map or picker
        /// actually uses that ID.
        /// </summary>
        public ReactorInfo GetReactor(string reactorId)
        {
            if (string.IsNullOrWhiteSpace(reactorId))
                return null;
            if (Reactors.TryGetValue(reactorId, out ReactorInfo cached))
                return cached;

            string imageName = WzInfoTools.AddLeadingZeros(reactorId, 7) + ".img";
            WzImage image = Program.FindImage("Reactor", imageName);
            if (image == null)
                return null;

            image.ParseImage();
            WzSubProperty info = image["info"] as WzSubProperty;
            string name = (info?["info"] as WzStringProperty)?.Value ??
                (info?["viewName"] as WzStringProperty)?.Value ?? string.Empty;
            var result = new ReactorInfo(null, new System.Drawing.Point(), reactorId, name, image);
            lock (Reactors)
            {
                if (Reactors.TryGetValue(reactorId, out cached))
                    return cached;
                Reactors[reactorId] = result;
            }
            return result;
        }

        /// <summary>
        /// Enumerates reactor IDs without parsing reactor IMG files.
        /// </summary>
        public IEnumerable<string> GetReactorIds()
        {
            var ids = new HashSet<string>(Reactors.Keys, StringComparer.OrdinalIgnoreCase);
            if (Program.DataSource != null)
            {
                foreach (string name in Program.DataSource.GetImageNamesInDirectory("Reactor", string.Empty))
                {
                    string id = WzInfoTools.RemoveExtension(name);
                    if (!string.IsNullOrWhiteSpace(id))
                        ids.Add(id);
                }
            }
            return ids;
        }

        public IEnumerable<string> GetMobIds() => GetImageIds("Mob", MobNameCache.Keys);

        public IEnumerable<string> GetNpcIds() => GetImageIds("Npc", NpcNameCache.Keys);

        private IEnumerable<string> GetImageIds(string category, IEnumerable<string> cachedIds)
        {
            var ids = new HashSet<string>(cachedIds, StringComparer.OrdinalIgnoreCase);
            if (Program.DataSource != null)
            {
                foreach (string name in Program.DataSource.GetImageNamesInDirectory(category, string.Empty))
                {
                    string id = WzInfoTools.RemoveExtension(name).TrimStart('0');
                    if (id.Length == 0)
                        id = "0";
                    ids.Add(id);
                }
            }
            return ids;
        }

        /// <summary>Loads all localized selector names only when a selector needs them.</summary>
        public void EnsureStringData()
        {
            if (Program.DataSource == null ||
                (NpcNameCache.Count != 0 && MobNameCache.Count != 0 &&
                 SkillNameCache.Count != 0 && ItemNameCache.Count != 0))
                return;
            lock (deferredExtractionLock)
            {
                new ImgDataExtractor(Program.DataSource, this).ExtractStringData();
            }
        }

        /// <summary>Loads localized mob names when the life asset picker needs them.</summary>
        public void EnsureMobStringData()
        {
            if (Program.DataSource == null || MobNameCache.Count != 0)
                return;
            lock (deferredExtractionLock)
            {
                if (MobNameCache.Count == 0)
                    new ImgDataExtractor(Program.DataSource, this).ExtractMobStringData();
            }
        }

        /// <summary>Loads only localized NPC names when map NPC tooltips need them.</summary>
        public void EnsureNpcStringData()
        {
            if (Program.DataSource == null || NpcNameCache.Count != 0)
                return;
            lock (deferredExtractionLock)
            {
                if (NpcNameCache.Count == 0)
                    new ImgDataExtractor(Program.DataSource, this).ExtractNpcStringData();
            }
        }

        /// <summary>Loads quest metadata when the Quest editor is opened.</summary>
        public void EnsureQuestData()
        {
            if (Program.DataSource == null || QuestInfos.Count != 0)
                return;
            lock (deferredExtractionLock)
            {
                if (QuestInfos.Count == 0)
                    new ImgDataExtractor(Program.DataSource, this).ExtractQuestData();
            }
        }

        /// <summary>
        /// Rebuilds the legacy BGMs dictionary from the recursive catalog.
        /// Keys retain the historical <c>Bgm00/Track</c> form while nested
        /// BgmMultiTrack paths are represented without truncation.
        /// </summary>
        public bool RefreshAudioCatalogProjection()
        {
            IAudioAssetCatalog catalog = AudioCatalog;
            if (catalog == null)
                return false;
            IReadOnlyList<AudioAssetEntry> assets;
            try
            {
                assets = catalog.BuildIndexAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return false;
            }

            BGMs.Clear();
            foreach (AudioAssetEntry asset in assets.Where(item =>
                item.Category == AudioAssetCategory.Bgm ||
                item.Category == AudioAssetCategory.Regional))
            {
                string imageName = asset.ImagePath;
                string key = WzInfoTools.RemoveExtension(imageName) + "/" + asset.PropertyPath;
                if (!BGMs.ContainsKey(key))
                    BGMs[key] = new BgmEntry(imageName, asset.PropertyPath);

                string canonical = asset.CanonicalPath;
                if (!BGMs.ContainsKey(canonical))
                    BGMs[canonical] = new BgmEntry(imageName, asset.PropertyPath);
            }
            return true;
        }

        public static string GetPropertyPathRelativeToImage(WzImageProperty property)
        {
            if (property == null)
                return null;

            var segments = new Stack<string>();
            WzObject current = property;
            while (current is WzImageProperty imageProperty)
            {
                segments.Push(imageProperty.Name);
                current = imageProperty.Parent;
            }

            return segments.Count == 0 ? null : string.Join("/", segments);
        }

        #region Hot Swap Refresh Methods
        /// <summary>
        /// Refreshes a specific tile set from the data source
        /// </summary>
        /// <param name="setName">The tile set name</param>
        public void RefreshTileSet(string setName)
        {
            if (TileSets.ContainsKey(setName))
            {
                TileSets[setName] = null; // Clear cached value - will reload on next GetTileSet() call
            }
        }

        /// <summary>
        /// Refreshes a specific object set from the data source
        /// </summary>
        /// <param name="setName">The object set name</param>
        public void RefreshObjectSet(string setName)
        {
            if (ObjectSets.ContainsKey(setName))
            {
                ObjectSets[setName] = null; // Clear cached value
            }
        }

        /// <summary>
        /// Refreshes a specific background set from the data source
        /// </summary>
        /// <param name="setName">The background set name</param>
        public void RefreshBackgroundSet(string setName)
        {
            if (BackgroundSets.ContainsKey(setName))
            {
                BackgroundSets[setName] = null; // Clear cached value
            }
        }

        /// <summary>
        /// Adds a new tile set to the available sets list
        /// </summary>
        /// <param name="setName">The tile set name</param>
        public void AddTileSet(string setName)
        {
            if (!TileSets.ContainsKey(setName))
            {
                TileSets[setName] = null; // Will be lazy-loaded
            }
        }

        /// <summary>
        /// Removes a tile set from the available sets list
        /// </summary>
        /// <param name="setName">The tile set name</param>
        public void RemoveTileSet(string setName)
        {
            TileSets.Remove(setName);
        }

        /// <summary>
        /// Adds a new object set to the available sets list
        /// </summary>
        /// <param name="setName">The object set name</param>
        public void AddObjectSet(string setName)
        {
            if (!ObjectSets.ContainsKey(setName))
            {
                ObjectSets[setName] = null;
            }
        }

        /// <summary>
        /// Removes an object set from the available sets list
        /// </summary>
        /// <param name="setName">The object set name</param>
        public void RemoveObjectSet(string setName)
        {
            ObjectSets.Remove(setName);
        }

        /// <summary>
        /// Adds a new background set to the available sets list
        /// </summary>
        /// <param name="setName">The background set name</param>
        public void AddBackgroundSet(string setName)
        {
            if (!BackgroundSets.ContainsKey(setName))
            {
                BackgroundSets[setName] = null;
            }
        }

        /// <summary>
        /// Removes a background set from the available sets list
        /// </summary>
        /// <param name="setName">The background set name</param>
        public void RemoveBackgroundSet(string setName)
        {
            BackgroundSets.Remove(setName);
        }

        /// <summary>
        /// Refreshes mob data for a specific mob ID
        /// </summary>
        /// <param name="mobId">The mob ID</param>
        public void RefreshMob(string mobId)
        {
            MobNameCache.Remove(mobId);
            MobIconCache.Remove(int.TryParse(mobId, out int id) ? id : 0);
        }

        /// <summary>
        /// Refreshes NPC data for a specific NPC ID
        /// </summary>
        /// <param name="npcId">The NPC ID</param>
        public void RefreshNpc(string npcId)
        {
            NpcNameCache.Remove(npcId);
            NpcPropertyCache.Remove(npcId);
        }

        /// <summary>
        /// Refreshes reactor data for a specific reactor ID
        /// </summary>
        /// <param name="reactorId">The reactor ID</param>
        public void RefreshReactor(string reactorId)
        {
            Reactors.Remove(reactorId);
        }

        /// <summary>
        /// Refreshes all quest data
        /// </summary>
        public void RefreshQuestData()
        {
            QuestInfos.Clear();
            QuestActs.Clear();
            QuestChecks.Clear();
            QuestSays.Clear();
            // Data will be reloaded when QuestEditor accesses it
        }
        #endregion
    }
}
