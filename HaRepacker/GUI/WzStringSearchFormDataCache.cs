using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using MapleLib;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;

namespace HaRepacker.GUI
{
    public class WzStringSearchFormDataCache
    {
        public enum WzDataCacheItemType
        {
            Cash, Use, Setup, Eqp, Etc
        }

        /// <summary>
        /// Wz encryption to use
        /// </summary>
        private WzMapleVersion WzMapleVersion = WzMapleVersion.BMS;

        /// <summary>
        /// List of WZ Directories
        /// </summary>
        private Dictionary<string, WzFile> Files { get; set; }

        private Dictionary<int, Tuple<string, string, string>> MapNameCache;  // Region Name, MapName, Street Name
        private Dictionary<int, KeyValuePair<string, string>> CashItemCache; // <ItemId, <Name, Desc>>
        private Dictionary<int, KeyValuePair<string, string>> EtcItemCache; // <ItemId, <Name, Desc>>
        private Dictionary<int, KeyValuePair<string, string>> SetupItemCache; // <ItemId, <Name, Desc>>
        private Dictionary<int, KeyValuePair<string, string>> UseItemCache; // <ItemId, <Name, Desc>>

        private Dictionary<int, KeyValuePair<string, string>> EqpItemCache; // <ItemId, <Name, Desc>>
        private Dictionary<int, Tuple<string, string, string>> Quests; // <QuestID, <Name, Desc, Parent>>

        private Dictionary<int, Tuple<string, string, string>> SkillsCache; // <SkillId, <Name, Desc, h>>
        private Dictionary<int, string> JobsCache; // <jobid, job name>

        private Dictionary<int, KeyValuePair<string, string>> NPCsCache; // <NPCId, <Name, func>>

        public WzStringSearchFormDataCache(WzMapleVersion wzMapleVersion)
        {
            this.WzMapleVersion = wzMapleVersion;

            Files = new Dictionary<string, WzFile>();

            MapNameCache = new Dictionary<int, Tuple<string, string, string>>();
            CashItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            EtcItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            SetupItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            UseItemCache = new Dictionary<int, KeyValuePair<string, string>>();
            EqpItemCache = new Dictionary<int, KeyValuePair<string, string>>();

            Quests = new Dictionary<int, Tuple<string, string, string>>();

            SkillsCache = new Dictionary<int, Tuple<string, string, string>>();
            JobsCache = new Dictionary<int, string>();

            NPCsCache = new Dictionary<int, KeyValuePair<string, string>>();
        }

        #region Getter Methods
        /// <summary>
        /// Gets the map name by ID
        /// </summary>
        /// <param name="mapid"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string GetMapName(int mapid, string defaultValue = null)
        {
            return MapNameCache.ContainsKey(mapid) ? MapNameCache[mapid].Item2 : defaultValue;
        }

        /// <summary>
        /// Lookup the list of maps with the given search query.
        /// </summary>
        /// <param name="SearchQuery"></param>
        /// <param name="HexJumpList"></param>
        public void LookupMaps(string SearchQuery, Dictionary<int, KeyValuePair<string, string>> HexJumpList)
        {
            SearchQuery = SearchQuery.ToLower();

            List<Tuple<int, string, string, string>> RetList = new List<Tuple<int, string, string, string>>();
            foreach (KeyValuePair<int, Tuple<string, string, string>> item in MapNameCache)
            {
                Tuple<string, string, string> val = item.Value;  // Region Name, MapName, Street Name

                string RegionName = val.Item1;
                string MapName = val.Item2;
                string StreetName = val.Item3;

                string RegionNameLower = RegionName.ToLower();
                string MapNameLower = MapName.ToLower();
                string StreetNameLower = StreetName.ToLower();

                if (RegionNameLower.Contains(SearchQuery) || MapNameLower.Contains(SearchQuery) || StreetNameLower.Contains(SearchQuery))
                {
                    HexJumpList.Add(item.Key, new KeyValuePair<string, string>(string.Format("[{0}]{1}", RegionName, StreetName), MapName));
                }
            }
        }

        /// <summary>
        /// Lookup the list of quests with the given search query
        /// </summary>
        /// <param name="SearchQuery"></param>
        /// <param name="HexJumpList"></param>
        public void LookupQuest(string SearchQuery, Dictionary<int, KeyValuePair<string, string>> HexJumpList)
        {
            SearchQuery = SearchQuery.ToLower();

            foreach (KeyValuePair<int, Tuple<string, string, string>> item in Quests)
            {
                Tuple<string, string, string> val = item.Value;

                string questName = val.Item1;
                string basic0Desc = val.Item2;
                string parentQuest = val.Item3;

                if (questName.ToLower().Contains(SearchQuery))
                {
                    HexJumpList.Add(item.Key, new KeyValuePair<string, string>(questName, basic0Desc));
                }
            }
        }

        /// <summary>
        /// Lookup the list of skills with the given search query
        /// </summary>
        /// <param name="SearchQuery"></param>
        /// <param name="HexJumpList"></param>
        public void LookupSkills(string SearchQuery, Dictionary<int, KeyValuePair<string, string>> HexJumpList)
        {
            SearchQuery = SearchQuery.ToLower();

            foreach (KeyValuePair<int, Tuple<string, string, string>> item in SkillsCache)
            {
                Tuple<string, string, string> val = item.Value;

                string skillName = val.Item1;
                string desc = val.Item2;
                string h = val.Item3;

                if (skillName == null)
                {
                    Debug.WriteLine("Skillid of " + item.Key + " is null.");
                }
                else
                {
                    if (skillName.ToLower().Contains(SearchQuery) || desc.ToLower().Contains(SearchQuery))
                    {
                        HexJumpList.Add(item.Key, new KeyValuePair<string, string>(skillName, string.Format("{0}\r\n{1}", desc, h)));
                    }
                }
            }
        }

        /// <summary>
        /// Lookup the list of jobs with the given search query
        /// </summary>
        /// <param name="SearchQuery"></param>
        /// <param name="HexJumpList"></param>
        public void LookupJobs(string SearchQuery, Dictionary<int, KeyValuePair<string, string>> HexJumpList)
        {
            SearchQuery = SearchQuery.ToLower();

            foreach (KeyValuePair<int, string> item in JobsCache)
            {
                string jobName = item.Value;


                if (jobName.ToLower().Contains(SearchQuery))
                {
                    HexJumpList.Add(item.Key, new KeyValuePair<string, string>(item.Key.ToString(), jobName));
                }
            }
        }

        /// <summary>
        /// Lookup the list of quests with the given search query
        /// </summary>
        /// <param name="SearchQuery"></param>
        /// <param name="HexJumpList"></param>
        public void LookupNPCs(string SearchQuery, Dictionary<int, KeyValuePair<string, string>> HexJumpList)
        {
            SearchQuery = SearchQuery.ToLower();

            foreach (KeyValuePair<int, KeyValuePair<string, string>> item in NPCsCache)
            {
                KeyValuePair<string, string> val = item.Value;

                string NPCName = val.Key.ToLower();
                string NPCFUnction = val.Value.ToLower();

                string NPCNameLower = NPCName.ToLower();
                string NPCFunctionLower = NPCFUnction.ToLower();

                if (NPCNameLower.Contains(SearchQuery) || NPCFunctionLower.Contains(SearchQuery))
                {
                    HexJumpList.Add(item.Key, new KeyValuePair<string, string>(NPCName, NPCFUnction));
                }
            }
        }

        /// <summary>
        /// Gets the name of the item by ID
        /// </summary>
        /// <param name="itemid"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string GetItemName(WzDataCacheItemType type, int itemid, string defaultValue = null)
        {
            switch (type)
            {
                case WzDataCacheItemType.Cash:
                    return CashItemCache.ContainsKey(itemid) ? CashItemCache[itemid].Key : defaultValue;
                case WzDataCacheItemType.Eqp:
                    return EqpItemCache.ContainsKey(itemid) ? EqpItemCache[itemid].Key : defaultValue;
                case WzDataCacheItemType.Etc:
                    return EtcItemCache.ContainsKey(itemid) ? EtcItemCache[itemid].Key : defaultValue;
                case WzDataCacheItemType.Setup:
                    return SetupItemCache.ContainsKey(itemid) ? SetupItemCache[itemid].Key : defaultValue;
                case WzDataCacheItemType.Use:
                    return UseItemCache.ContainsKey(itemid) ? UseItemCache[itemid].Key : defaultValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets the description of the item by ID
        /// </summary>
        /// <param name="itemid"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string GetItemDesc(WzDataCacheItemType type, int itemid, string defaultValue = null)
        {
            switch (type)
            {
                case WzDataCacheItemType.Cash:
                    return CashItemCache.ContainsKey(itemid) ? CashItemCache[itemid].Value : defaultValue;
                case WzDataCacheItemType.Eqp:
                    return EqpItemCache.ContainsKey(itemid) ? EqpItemCache[itemid].Value : defaultValue;
                case WzDataCacheItemType.Etc:
                    return EtcItemCache.ContainsKey(itemid) ? EtcItemCache[itemid].Value : defaultValue;
                case WzDataCacheItemType.Setup:
                    return SetupItemCache.ContainsKey(itemid) ? SetupItemCache[itemid].Value : defaultValue;
                case WzDataCacheItemType.Use:
                    return UseItemCache.ContainsKey(itemid) ? UseItemCache[itemid].Value : defaultValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// Lookup the database of items with a given search query
        /// </summary>
        /// <param name="type"></param>
        /// <param name="SearchQuery"></param>
        /// <param name="HexJumpList"></param>
        public void LookupItemNameDesc(WzDataCacheItemType type, string SearchQuery, Dictionary<int, KeyValuePair<string, string>> HexJumpList)
        {
            Dictionary<int, KeyValuePair<string, string>> LookupSource = null;
            switch (type)
            {
                case WzDataCacheItemType.Cash:
                    LookupSource = CashItemCache;
                    break;
                case WzDataCacheItemType.Eqp:
                    LookupSource = EqpItemCache;
                    break;
                case WzDataCacheItemType.Etc:
                    LookupSource = EtcItemCache;
                    break;
                case WzDataCacheItemType.Setup:
                    LookupSource = SetupItemCache;
                    break;
                case WzDataCacheItemType.Use:
                    LookupSource = UseItemCache;
                    break;
            }
            if (LookupSource != null)
            {
                SearchQuery = SearchQuery.ToLower();

                foreach (KeyValuePair<int, KeyValuePair<string, string>> item in LookupSource)
                {
                    KeyValuePair<string, string> val = item.Value;
                    if (val.Key.ToLower().Contains(SearchQuery) || val.Value.ToLower().Contains(SearchQuery))
                    {
                        HexJumpList.Add(item.Key, item.Value);
                    }
                }
            }
        }

        #endregion


        #region Cache

        /// <summary>
        /// Open Base.WZ maplestory data
        /// </summary>
        public bool OpenBaseWZFile(out string LoadedVersion)
        {
            LoadedVersion = string.Empty;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "MapleStory Base.wz | Base.wz";
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string Dir = ofd.FileName.Replace("\\Base.wz", "");
                    foreach (string Name in Directory.GetFiles(Dir))
                    {
                        FileInfo Info = new FileInfo(Name);
                        if (Info.Extension != ".wz")
                            continue;
                        WzFile File = new WzFile(Name, WzMapleVersion);
                        File.ParseWzFile();

                        this.Files.Add(Info.Name, File);

                        if (LoadedVersion == string.Empty)
                            LoadedVersion = "MapleStory v." + File.Version + " WZ version: " + File.MapleVersion.ToString();
                    }
                    ParseWZFiles();

                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Gets the loaded WZ file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>null if it does not exist</returns>
        public WzFile GetLoadedWZFile(string fileName)
        {
            if (!this.Files.ContainsKey(fileName))
            {
                return null;
            }
            return this.Files[fileName];
        }

        /// <summary>
        /// Parse all WZ files and cache data
        /// </summary>
        private void ParseWZFiles()
        {
            if (Files.Count() == 0)
            {
                throw new Exception("BaseWZ file not opened yet.");
            }

            CacheMaps();
            CacheInventoryData();
            CacheQuestData();
        }


        /// <summary>
        /// Caches all Quest data from Quest.wz
        /// </summary>
        private void CacheQuestData()
        {
            WzDirectory QuestDir = Files["Quest.wz"].WzDirectory;

            WzImage QuestInfoImg = (WzImage)QuestDir["QuestInfo.img"];

            foreach (WzSubProperty Item in QuestInfoImg.WzProperties)
            {
                int QuestID = int.Parse(Item.Name);
                string QuestName = Item["name"].ReadString("NULL");
                string ParentQuestName = Item["parent"].ReadString("NULL");
                string QuestDescription = Item["0"].ReadString("NULL");

                Quests.Add(QuestID, new Tuple<string, string, string>(QuestName, QuestDescription, ParentQuestName));
            }
        }

        /// <summary>
        /// Caches all inventory data from String.wz
        /// </summary>
        private void CacheInventoryData()
        {
            WzDirectory StringDir = Files["String.wz"].WzDirectory;

            // Directories
            WzImage MobDir = (WzImage)StringDir["Mob.img"];

            WzImage CashDir = (WzImage)StringDir["Cash.img"];
            WzImage ConsumeDir = (WzImage)StringDir["Consume.img"];
            WzImage EqpDir = (WzImage)StringDir["Eqp.img"];
            WzImage EtcDir = (WzImage)StringDir["Etc.img"];
            WzImage InsDir = (WzImage)StringDir["Ins.img"];
            WzImage NpcDir = (WzImage)StringDir["Npc.img"];
            WzImage SkillDir = (WzImage)StringDir["Skill.img"];

            // Skills
            foreach (WzSubProperty skill in SkillDir.WzProperties)
            {
                int SkillIdOrJobID = int.Parse(skill.Name);

                string bookName = skill["bookName"]?.ReadString(null);
                if (bookName != null)
                {
                    if (!JobsCache.ContainsKey(SkillIdOrJobID))
                        JobsCache.Add(SkillIdOrJobID, bookName);
                }
                else
                {
                    string name = skill["name"]?.ReadString("NULL");
                    string desc = skill["desc"]?.ReadString(null);
                    string h1 = skill["h1"]?.ReadString(null);

                    if (desc == null) desc = string.Empty;
                    if (h1 == null) h1 = string.Empty;

                    if (name != null)
                    {
                        if (!SkillsCache.ContainsKey(SkillIdOrJobID))
                        {
                            SkillsCache.Add(SkillIdOrJobID, new Tuple<string, string, string>(name, desc, h1));
                        }
                        else
                        {
                            Debug.WriteLine("[WzDataCache] Skillid already exist in the key " + SkillIdOrJobID);
                        }
                    }
                }
            }

            // NPCs
            foreach (WzSubProperty NPC in NpcDir.WzProperties)
            {
                int NPCId = int.Parse(NPC.Name);

                if (NPCsCache.ContainsKey(NPCId))
                    continue;

                NPCsCache.Add(NPCId, new KeyValuePair<string, string>(
                    NPC["name"].ReadString("NULL"),
                    NPC["func"].ReadString("NULL")));
            }

            // Cash Item
            foreach (WzSubProperty Item in CashDir.WzProperties)
            {
                int Id = int.Parse(Item.Name);

                if (CashItemCache.ContainsKey(Id))
                    continue;

                CashItemCache.Add(Id, new KeyValuePair<string, string>(
                    Item["name"].ReadString("NULL"),
                    Item["desc"].ReadString("NULL")));
            }

            // Consume
            foreach (WzSubProperty Item in ConsumeDir.WzProperties)
            {
                int Id = int.Parse(Item.Name);

                if (UseItemCache.ContainsKey(Id))
                    continue;

                UseItemCache.Add(Id, new KeyValuePair<string, string>(
                    Item["name"].ReadString("NULL"),
                    Item["desc"].ReadString("NULL")));
            }

            // Ins / Setup
            foreach (WzSubProperty Item in InsDir.WzProperties)
            {
                int Id = int.Parse(Item.Name);

                if (SetupItemCache.ContainsKey(Id))
                    continue;

                SetupItemCache.Add(Id, new KeyValuePair<string, string>(
                    Item["name"].ReadString("NULL"),
                    Item["desc"].ReadString("NULL")));
            }

            // Etc
            foreach (WzSubProperty MapArea in EtcDir.WzProperties)
            {
                string dirName = MapArea.Name;

                foreach (WzSubProperty Item in MapArea.WzProperties)
                {
                    int Id = int.Parse(Item.Name);

                    if (EtcItemCache.ContainsKey(Id))
                        continue;

                    EtcItemCache.Add(Id, new KeyValuePair<string, string>(
                        Item["name"].ReadString("NULL"),
                        Item["desc"].ReadString("NULL")));
                }
            }

            // Equip
            foreach (WzSubProperty Eqp in EqpDir.WzProperties)
            {
                string dirName = Eqp.Name;

                foreach (WzSubProperty EqpCategories in Eqp.WzProperties)
                {
                    string EqpCategoriesName = Eqp.Name;

                    foreach (WzSubProperty EqpCategoriesItem in EqpCategories.WzProperties)
                    {
                        int Id = int.Parse(EqpCategoriesItem.Name);

                        if (EqpItemCache.ContainsKey(Id))
                            continue;
                        EqpItemCache.Add(Id, new KeyValuePair<string, string>(
                            EqpCategoriesItem["name"].ReadString("NULL"),
                            EqpCategoriesItem["desc"].ReadString("NULL")));
                    }
                }
            }
        }

        /// <summary>
        /// Cache map data from String.wz
        /// </summary>
        private void CacheMaps()
        {
            WzDirectory StringDir = Files["String.wz"].WzDirectory;
            WzImage MapStringDir = (WzImage)StringDir["Map.img"];

            foreach (WzSubProperty MapArea in MapStringDir.WzProperties)
            {
                string RegionName = MapArea.Name;

                foreach (WzSubProperty Map in MapArea.WzProperties)
                {
                    int Id = int.Parse(Map.Name);

                    string MapName = ((WzStringProperty)Map["mapName"]).ReadString("");
                    string StreetName = ((WzStringProperty)Map["streetName"]).ReadString("");

                    if (!MapNameCache.ContainsKey(Id))
                        MapNameCache.Add(Id, new Tuple<string, string, string>(RegionName, MapName, StreetName));
                }
            }
        }
        #endregion
    }
}
