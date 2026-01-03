using HaCreator.MapEditor.Info;
using HaSharedLibrary.Wz;
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
        public Dictionary<string, ReactorInfo> Reactors = new Dictionary<string, ReactorInfo>();

        // Lazy-loading dictionaries for map assets - only load when accessed
        public IDictionary<string, WzImage> TileSets = new Dictionary<string, WzImage>();
        public IDictionary<string, WzImage> ObjectSets = new Dictionary<string, WzImage>();
        public IDictionary<string, WzImage> BackgroundSets = new Dictionary<string, WzImage>();

        public Dictionary<string, WzBinaryProperty> BGMs = new Dictionary<string, WzBinaryProperty>();

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
        public WzImage GetItemEquipSubProperty(int id, string categoryName, WzFileManager fileManager)
        {
            if (EquipItemCache.ContainsKey(id))
                return EquipItemCache[id];

            WzDirectory charWzEqpCatDirectory = (WzDirectory)fileManager.FindWzImageByName("character", categoryName);
            if (charWzEqpCatDirectory != null)
            {
                WzImage itemObj = (WzImage)charWzEqpCatDirectory[WzInfoTools.AddLeadingZeros(id.ToString(), 8) + ".img"];
                if (itemObj != null)
                {
                    lock (EquipItemCache)
                    {
                        if (!EquipItemCache.ContainsKey(id))
                            EquipItemCache.Add(id, itemObj);
                    }
                    return itemObj;
                }
            }
            return null;
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
            MapMarks.Clear();
            MapsNameCache.Clear();
            MapsCache.Clear();
            Portals.Clear();
            PortalEditor_TypeById.Clear();
            PortalIdByType.Clear();
            PortalGame.Clear();
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
