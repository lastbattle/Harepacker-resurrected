/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using HaSharedLibrary.Wz;
using MapleLib.Helpers;
using MapleLib.Img;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.Wz
{
    /// <summary>
    /// Extracts game data from an IDataSource into WzInformationManager.
    /// Works with both IMG filesystem and WZ file data sources.
    /// </summary>
    public class ImgDataExtractor
    {
        private readonly IDataSource _dataSource;
        private readonly WzInformationManager _infoManager;

        /// <summary>
        /// Event for progress reporting
        /// </summary>
        public event EventHandler<DataExtractionProgressEventArgs> ProgressChanged;

        /// <summary>
        /// Creates a new ImgDataExtractor
        /// </summary>
        public ImgDataExtractor(IDataSource dataSource, WzInformationManager infoManager)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _infoManager = infoManager ?? throw new ArgumentNullException(nameof(infoManager));
        }

        /// <summary>
        /// Extracts all data from the data source
        /// </summary>
        public void ExtractAll()
        {
            ReportProgress("Extracting String data...");
            ExtractStringData();

            ReportProgress("Extracting Mob data...");
            ExtractMobData();

            ReportProgress("Extracting NPC data...");
            ExtractNpcData();

            ReportProgress("Extracting Reactor data...");
            ExtractReactorData();

            ReportProgress("Extracting Sound data...");
            ExtractSoundData();

            ReportProgress("Extracting Quest data...");
            ExtractQuestData();

            ReportProgress("Extracting Skill data...");
            ExtractSkillData();

            ReportProgress("Extracting Item data...");
            ExtractItemData();

            ReportProgress("Extracting Map marks...");
            ExtractMapMarks();

            ReportProgress("Extracting Map portals...");
            ExtractMapPortals();

            ReportProgress("Extracting Map tile sets...");
            ExtractMapTileSets();

            ReportProgress("Extracting Map object sets...");
            ExtractMapObjSets();

            ReportProgress("Extracting Map background sets...");
            ExtractMapBackgroundSets();

            ReportProgress("Extracting Maps...");
            ExtractMaps();

            ReportProgress("Extraction complete.");
        }

        /// <summary>
        /// Extracts String.wz data (map names, mob names, item names, etc.)
        /// </summary>
        public void ExtractStringData()
        {
            if (_infoManager.MapsNameCache.Count != 0)
                return;

            // NPC strings
            var npcImg = _dataSource.GetImage("String", "Npc.img");
            if (npcImg != null)
            {
                npcImg.ParseImage();
                foreach (WzSubProperty npcProp in npcImg.WzProperties)
                {
                    string npcId = npcProp.Name;
                    string npcName = (npcProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                    string npcFunc = (npcProp["func"] as WzStringProperty)?.Value ?? string.Empty;

                    if (!_infoManager.NpcNameCache.ContainsKey(npcId))
                        _infoManager.NpcNameCache[npcId] = new Tuple<string, string>(npcName, npcFunc);
                }
            }

            // Map strings
            var mapImg = _dataSource.GetImage("String", "Map.img");
            if (mapImg != null)
            {
                mapImg.ParseImage();
                foreach (WzSubProperty mapCat in mapImg.WzProperties)
                {
                    foreach (WzSubProperty map in mapCat.WzProperties)
                    {
                        WzStringProperty streetNameProp = (WzStringProperty)map["streetName"];
                        WzStringProperty mapNameProp = (WzStringProperty)map["mapName"];

                        string mapIdStr = map.Name.Length == 9 ? map.Name : WzInfoTools.AddLeadingZeros(map.Name, 9);
                        string categoryName = map.Parent.Name;

                        if (mapNameProp == null)
                            _infoManager.MapsNameCache[mapIdStr] = new Tuple<string, string, string>("NO NAME", "NO NAME", "NO NAME");
                        else
                        {
                            _infoManager.MapsNameCache[mapIdStr] = new Tuple<string, string, string>(
                                streetNameProp?.Value ?? string.Empty,
                                mapNameProp.Value,
                                categoryName);
                        }
                    }
                }
            }

            // Mob strings
            var mobImg = _dataSource.GetImage("String", "Mob.img");
            if (mobImg != null)
            {
                mobImg.ParseImage();
                foreach (WzSubProperty mobProp in mobImg.WzProperties)
                {
                    string mobId = mobProp.Name;
                    string mobName = (mobProp["name"] as WzStringProperty)?.Value ?? "NO NAME";

                    if (!_infoManager.MobNameCache.ContainsKey(mobId))
                        _infoManager.MobNameCache[mobId] = mobName;
                }
            }

            // Skill strings
            var skillImg = _dataSource.GetImage("String", "Skill.img");
            if (skillImg != null)
            {
                skillImg.ParseImage();
                foreach (WzSubProperty skillProp in skillImg.WzProperties)
                {
                    string skillId = skillProp.Name;
                    string skillName = (skillProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                    string skillDesc = (skillProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                    if (!_infoManager.SkillNameCache.ContainsKey(skillId))
                        _infoManager.SkillNameCache[skillId] = new Tuple<string, string>(skillName, skillDesc);
                }
            }

            // Equipment strings
            var eqpImg = _dataSource.GetImage("String", "Eqp.img");
            if (eqpImg != null)
            {
                eqpImg.ParseImage();
                ExtractEquipmentStrings(eqpImg.WzProperties);
            }

            // Install strings
            var insImg = _dataSource.GetImage("String", "Ins.img");
            if (insImg != null)
            {
                insImg.ParseImage();
                ExtractItemStrings(insImg.WzProperties, "Ins");
            }

            // Cash strings
            var cashImg = _dataSource.GetImage("String", "Cash.img");
            if (cashImg != null)
            {
                cashImg.ParseImage();
                ExtractItemStrings(cashImg.WzProperties, "Cash");
            }

            // Consume strings
            var consumeImg = _dataSource.GetImage("String", "Consume.img");
            if (consumeImg != null)
            {
                consumeImg.ParseImage();
                ExtractItemStrings(consumeImg.WzProperties, "Consume");
            }

            // Etc strings
            var etcImg = _dataSource.GetImage("String", "Etc.img");
            if (etcImg != null)
            {
                etcImg.ParseImage();
                ExtractEtcStrings(etcImg.WzProperties);
            }

            // Pet strings
            var petImg = _dataSource.GetImage("String", "Pet.img");
            if (petImg != null)
            {
                petImg.ParseImage();
                ExtractItemStrings(petImg.WzProperties, "Pet");
            }
        }

        private void ExtractEquipmentStrings(WzPropertyCollection props)
        {
            foreach (WzSubProperty eqpSubProp in props)
            {
                foreach (WzSubProperty eqpCategoryProp in eqpSubProp.WzProperties)
                {
                    foreach (WzImageProperty itemProp in eqpCategoryProp.WzProperties)
                    {
                        if (itemProp is WzSubProperty itemSubProp)
                        {
                            string itemId = itemSubProp.Name;
                            string itemName = (itemSubProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                            string itemDesc = (itemSubProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                            if (int.TryParse(itemId, out int intId))
                            {
                                if (!_infoManager.ItemNameCache.ContainsKey(intId))
                                    _infoManager.ItemNameCache[intId] = new Tuple<string, string, string>(eqpCategoryProp.Name, itemName, itemDesc);
                            }
                        }
                    }
                }
            }
        }

        private void ExtractItemStrings(WzPropertyCollection props, string category)
        {
            foreach (WzImageProperty itemProp in props)
            {
                if (itemProp is WzSubProperty itemSubProp)
                {
                    string itemId = itemSubProp.Name;
                    string itemName = (itemSubProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                    string itemDesc = (itemSubProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                    if (int.TryParse(itemId, out int intId))
                    {
                        if (!_infoManager.ItemNameCache.ContainsKey(intId))
                            _infoManager.ItemNameCache[intId] = new Tuple<string, string, string>(category, itemName, itemDesc);
                    }
                }
            }
        }

        private void ExtractEtcStrings(WzPropertyCollection props)
        {
            foreach (WzSubProperty etcSubProp in props)
            {
                foreach (WzSubProperty itemProp in etcSubProp.WzProperties)
                {
                    string itemId = itemProp.Name;
                    string itemName = (itemProp["name"] as WzStringProperty)?.Value ?? "NO NAME";
                    string itemDesc = (itemProp["desc"] as WzStringProperty)?.Value ?? "NO DESC";

                    if (int.TryParse(itemId, out int intId))
                    {
                        if (!_infoManager.ItemNameCache.ContainsKey(intId))
                            _infoManager.ItemNameCache[intId] = new Tuple<string, string, string>("Etc", itemName, itemDesc);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts Mob.wz data.
        /// Note: Mob icons are NOT loaded here to save memory.
        /// Mob names are already available from MobNameCache (populated from String/Mob.img).
        /// Icons are loaded on-demand when needed (e.g., in mob selector UI).
        /// </summary>
        public void ExtractMobData()
        {
            // Mob icons are loaded on-demand to reduce memory usage.
            // The MobNameCache (populated from String/Mob.img) provides the list of mobs.
            // Individual mob images are loaded when needed.
        }

        /// <summary>
        /// Extracts NPC.wz data.
        /// Note: NPC WzImages are no longer preloaded during extraction to save memory.
        /// They are loaded on-demand when needed (e.g., when user opens NPC selector).
        /// The NpcNameCache from String.wz provides the list of available NPCs.
        /// </summary>
        public void ExtractNpcData()
        {
            // NPC images are loaded on-demand to reduce memory usage.
            // The NpcNameCache (populated from String/Npc.img) provides the list of NPCs.
            // Individual NPC images are loaded when needed via GetNpcImage().
        }

        /// <summary>
        /// Extracts Reactor.wz data
        /// </summary>
        public void ExtractReactorData()
        {
            if (_infoManager.Reactors.Count != 0)
                return;

            foreach (var reactorImage in _dataSource.GetImagesInCategory("Reactor"))
            {
                reactorImage.ParseImage();
                WzSubProperty infoProp = (WzSubProperty)reactorImage["info"];

                string reactorId = WzInfoTools.RemoveExtension(reactorImage.Name);
                string name = "NO NAME";

                if (infoProp != null)
                {
                    name = ((WzStringProperty)infoProp["info"])?.Value ??
                           ((WzStringProperty)infoProp["viewName"])?.Value ?? string.Empty;
                }

                ReactorInfo reactor = new ReactorInfo(null, new System.Drawing.Point(), reactorId, name, reactorImage);
                _infoManager.Reactors[reactor.ID] = reactor;
            }
        }

        /// <summary>
        /// Extracts Sound.wz data
        /// </summary>
        public void ExtractSoundData()
        {
            if (_infoManager.BGMs.Count != 0)
                return;

            foreach (var soundImage in _dataSource.GetImagesInCategory("Sound"))
            {
                if (!soundImage.Name.ToLower().Contains("bgm"))
                    continue;

                try
                {
                    soundImage.ParseImage();
                    foreach (WzImageProperty bgmProp in soundImage.WzProperties)
                    {
                        WzBinaryProperty binProperty = null;

                        if (bgmProp is WzBinaryProperty bgm)
                            binProperty = bgm;
                        else if (bgmProp is WzUOLProperty uol && uol.LinkValue is WzBinaryProperty linkBgm)
                            binProperty = linkBgm;

                        if (binProperty != null)
                        {
                            string key = WzInfoTools.RemoveExtension(soundImage.Name) + "/" + binProperty.Name;
                            _infoManager.BGMs[key] = binProperty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.Log(ErrorLevel.IncorrectStructure,
                        $"[ExtractSoundData] Error parsing {soundImage.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Extracts Quest.wz data
        /// </summary>
        public void ExtractQuestData()
        {
            if (_infoManager.QuestActs.Count != 0)
                return;

            var actImg = _dataSource.GetImage("Quest", "Act.img");
            if (actImg != null)
            {
                actImg.ParseImage();
                foreach (WzImageProperty prop in actImg.WzProperties)
                    _infoManager.QuestActs.Add(prop.Name, prop as WzSubProperty);
            }

            var checkImg = _dataSource.GetImage("Quest", "Check.img");
            if (checkImg != null)
            {
                checkImg.ParseImage();
                foreach (WzImageProperty prop in checkImg.WzProperties)
                    _infoManager.QuestChecks.Add(prop.Name, prop as WzSubProperty);
            }

            var infoImg = _dataSource.GetImage("Quest", "QuestInfo.img");
            if (infoImg != null)
            {
                infoImg.ParseImage();
                foreach (WzImageProperty prop in infoImg.WzProperties)
                    _infoManager.QuestInfos.Add(prop.Name, prop as WzSubProperty);
            }

            var sayImg = _dataSource.GetImage("Quest", "Say.img");
            if (sayImg != null)
            {
                sayImg.ParseImage();
                foreach (WzImageProperty prop in sayImg.WzProperties)
                    _infoManager.QuestSays.Add(prop.Name, prop as WzSubProperty);
            }
        }

        /// <summary>
        /// Extracts Skill.wz data.
        /// Note: Skill images are NOT loaded here to save memory.
        /// Skill names are already available from SkillNameCache (populated from String/Skill.img).
        /// Skill images are loaded on-demand when needed.
        /// </summary>
        public void ExtractSkillData()
        {
            // Skill images are loaded on-demand to reduce memory usage.
            // The SkillNameCache (populated from String/Skill.img) provides the list of skills.
        }

        /// <summary>
        /// Extracts Item.wz data.
        /// Note: Item icons are NOT loaded here to save memory.
        /// Item names are already available from ItemNameCache (populated from String/*.img).
        /// Item icons are loaded on-demand when needed.
        /// </summary>
        public void ExtractItemData()
        {
            // Item icons are loaded on-demand to reduce memory usage.
            // The ItemNameCache (populated from String/Eqp.img, Consume.img, etc.) provides the list of items.
        }

        /// <summary>
        /// Extracts Map marks from MapHelper.img
        /// </summary>
        public void ExtractMapMarks()
        {
            if (_infoManager.MapMarks.Count != 0)
                return;

            var mapHelperImg = _dataSource.GetImage("Map", "MapHelper.img");
            if (mapHelperImg == null)
            {
                // Try in Map/Map subdirectory
                mapHelperImg = _dataSource.GetImageByPath("Map/Map/MapHelper.img");
            }

            if (mapHelperImg == null)
                throw new Exception("MapHelper.img not found.");

            mapHelperImg.ParseImage();
            var markProp = mapHelperImg["mark"];

            if (markProp != null)
            {
                foreach (WzCanvasProperty mark in markProp.WzProperties)
                {
                    _infoManager.MapMarks[mark.Name] = mark.GetLinkedWzCanvasBitmap();
                }
            }
        }

        /// <summary>
        /// Extracts Map portals from MapHelper.img
        /// </summary>
        public void ExtractMapPortals()
        {
            if (_infoManager.PortalGame.Count != 0)
                return;

            var mapHelperImg = _dataSource.GetImage("Map", "MapHelper.img");
            if (mapHelperImg == null)
                mapHelperImg = _dataSource.GetImageByPath("Map/Map/MapHelper.img");

            if (mapHelperImg == null)
                throw new Exception("MapHelper.img not found for portals.");

            mapHelperImg.ParseImage();
            WzSubProperty portalParent = (WzSubProperty)mapHelperImg["portal"];
            if (portalParent == null)
                return;

            // Editor portals
            WzSubProperty editorParent = (WzSubProperty)portalParent["editor"];
            if (editorParent != null)
            {
                foreach (WzCanvasProperty portalProp in editorParent.WzProperties)
                {
                    _infoManager.PortalEditor_TypeById.Add(PortalTypeExtensions.FromCode(portalProp.Name));
                    PortalInfo.Load(portalProp);
                }
            }

            // Game portals
            WzSubProperty gameParent = (WzSubProperty)portalParent["game"];
            if (gameParent != null)
            {
                foreach (WzImageProperty portalProp in gameParent.WzProperties)
                {
                    PortalType portalType = PortalTypeExtensions.FromCode(portalProp.Name);

                    if (portalProp["default"]?["portalStart"] != null)
                    {
                        Dictionary<string, List<Bitmap>> portalTemplates = new();

                        foreach (WzSubProperty imgProp in portalProp.WzProperties)
                        {
                            var portalStart = imgProp["portalStart"] as WzSubProperty;
                            List<Bitmap> images = new();

                            if (portalStart != null)
                            {
                                foreach (WzCanvasProperty canvas in portalStart.WzProperties)
                                    images.Add(canvas.GetLinkedWzCanvasBitmap());
                            }

                            portalTemplates[imgProp.Name] = images;
                        }

                        var firstImages = portalTemplates.FirstOrDefault().Value;
                        _infoManager.PortalGame[portalType] = new PortalGameImageInfo(
                            firstImages?.FirstOrDefault(), portalTemplates);
                    }
                    else
                    {
                        Dictionary<string, List<Bitmap>> portalTemplates = new();
                        Bitmap defaultImage = null;
                        List<Bitmap> images = new();

                        foreach (WzImageProperty prop in portalProp.WzProperties)
                        {
                            if (prop is WzCanvasProperty canvas)
                            {
                                var bmp = canvas.GetLinkedWzCanvasBitmap();
                                defaultImage = bmp;
                                images.Add(bmp);
                            }
                        }

                        portalTemplates["default"] = images;
                        _infoManager.PortalGame[portalType] = new PortalGameImageInfo(defaultImage, portalTemplates);
                    }
                }
            }

            for (int i = 0; i < _infoManager.PortalEditor_TypeById.Count; i++)
                _infoManager.PortalIdByType[_infoManager.PortalEditor_TypeById[i]] = i;
        }

        /// <summary>
        /// Extracts Map tile sets.
        /// Only registers names - images are NOT loaded here to save memory.
        /// Images should be loaded on-demand via Program.DataSource.GetImage().
        /// </summary>
        public void ExtractMapTileSets()
        {
            if (_infoManager.TileSets.Count != 0)
                return;

            // Just register names with null values - images loaded on-demand
            var tileSets = new Dictionary<string, WzImage>(StringComparer.OrdinalIgnoreCase);
            var names = _dataSource.GetImageNamesInDirectory("Map", "Tile").OrderBy(n => n).ToList();
            foreach (var name in names)
            {
                tileSets[name] = null; // Will be loaded on-demand when accessed
            }

            _infoManager.TileSets = tileSets;
        }

        /// <summary>
        /// Extracts Map object sets.
        /// Only registers names - images are NOT loaded here to save memory.
        /// </summary>
        public void ExtractMapObjSets()
        {
            if (_infoManager.ObjectSets.Count != 0)
                return;

            var objSets = new Dictionary<string, WzImage>(StringComparer.OrdinalIgnoreCase);
            var names = _dataSource.GetImageNamesInDirectory("Map", "Obj").ToList();
            foreach (var name in names)
            {
                objSets[name] = null;
            }

            _infoManager.ObjectSets = objSets;
        }

        /// <summary>
        /// Extracts Map background sets.
        /// Only registers names - images are NOT loaded here to save memory.
        /// </summary>
        public void ExtractMapBackgroundSets()
        {
            if (_infoManager.BackgroundSets.Count != 0)
                return;

            var bgSets = new Dictionary<string, WzImage>(StringComparer.OrdinalIgnoreCase);
            var names = _dataSource.GetImageNamesInDirectory("Map", "Back").ToList();
            foreach (var name in names)
            {
                bgSets[name] = null;
            }

            _infoManager.BackgroundSets = bgSets;
        }

        /// <summary>
        /// Extracts all map data.
        /// Note: Map WzImages are NOT loaded here to save memory.
        /// Only map names from MapsNameCache are used. MapInfo is created on-demand when map is opened.
        /// </summary>
        public void ExtractMaps()
        {
            if (_infoManager.MapsCache.Count != 0)
                return;

            // Just populate MapsCache with name info from MapsNameCache
            // MapInfo will be created on-demand when user actually opens a map
            foreach (var kvp in _infoManager.MapsNameCache)
            {
                string mapId = kvp.Key;
                var names = kvp.Value;

                // Store null for both WzImage and MapInfo - loaded on-demand when map is opened
                _infoManager.MapsCache[mapId] = new Tuple<WzImage, string, string, string, MapInfo>(
                    null, names.Item1, names.Item2, names.Item3, null);
            }
        }

        /// <summary>
        /// Finds a map image by ID
        /// </summary>
        private WzImage FindMapImage(string mapId)
        {
            // Determine which Map folder (Map0, Map1, etc.) based on first digit
            string paddedId = mapId.PadLeft(9, '0');
            string folderNum = paddedId[0].ToString();

            // Try exact path first
            string relativePath = $"Map/Map{folderNum}/{paddedId}.img";
            var img = _dataSource.GetImageByPath($"Map/{relativePath}");

            if (img == null)
            {
                // Try without padding
                relativePath = $"Map/Map{folderNum}/{mapId}.img";
                img = _dataSource.GetImageByPath($"Map/{relativePath}");
            }

            return img;
        }

        private void ReportProgress(string message)
        {
            ProgressChanged?.Invoke(this, new DataExtractionProgressEventArgs(message));
        }
    }

    /// <summary>
    /// Event args for data extraction progress
    /// </summary>
    public class DataExtractionProgressEventArgs : EventArgs
    {
        public string Message { get; }

        public DataExtractionProgressEventArgs(string message)
        {
            Message = message;
        }
    }
}
