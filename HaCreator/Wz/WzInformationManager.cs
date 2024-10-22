/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using HaSharedLibrary.Wz;
using MapleLib;
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
        public Dictionary<string, WzImage> TileSets = new Dictionary<string, WzImage>();
        public Dictionary<string, WzImage> ObjectSets = new Dictionary<string, WzImage>();

        public Dictionary<string, WzImage> BackgroundSets = new Dictionary<string, WzImage>();
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
        public Dictionary<string, string> NpcNameCache = new Dictionary<string, string>();
        public Dictionary<string, WzImage> NpcPropertyCache = new Dictionary<string, WzImage>();

        public Dictionary<string, PortalInfo> Portals = new Dictionary<string, PortalInfo>();
        public List<string> PortalTypeById = new List<string>();
        public Dictionary<string, int> PortalIdByType = new Dictionary<string,int>();
        public Dictionary<string, PortalGameImageInfo> GamePortals = new Dictionary<string, PortalGameImageInfo>();

        // Quests
        public Dictionary<string, WzSubProperty> QuestActs = new Dictionary<string, WzSubProperty>();
        public Dictionary<string, WzSubProperty> QuestChecks = new Dictionary<string, WzSubProperty>();
        public Dictionary<string, WzSubProperty> QuestInfos = new Dictionary<string, WzSubProperty>();
        public Dictionary<string, WzSubProperty> QuestSays = new Dictionary<string, WzSubProperty>();


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
            PortalTypeById.Clear();
            PortalIdByType.Clear();
            GamePortals.Clear();
        }
    }
}
