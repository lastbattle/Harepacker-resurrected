/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HaCreator.MapEditor;
using Microsoft.Xna.Framework;
using MapleLib.WzLib;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.Helpers;
using MapleLib.WzLib.WzProperties;
using HaCreator.ThirdParty.TabPages;
using System.Collections;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance.Misc;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.Wz
{
    public class MapLoader
    {
        public MapLoader()
        {
        }

        public List<string> VerifyMapPropsKnown(WzImage mapImage, bool userless)
        {
            List<string> copyPropNames = new List<string>();
            foreach (WzImageProperty prop in mapImage.WzProperties)
            {
                switch (prop.Name)
                {
                    case "0":
                    case "1":
                    case "2":
                    case "3":
                    case "4":
                    case "5":
                    case "6":
                    case "7":
                    case "info":
                    case "life":
                    case "ladderRope":
                    case "reactor":
                    case "back":
                    case "foothold":
                    case "miniMap":
                    case "portal":
                    case "seat":
                    case "ToolTip":
                    case "clock":
                    case "shipObj":
                    case "area":
                    case "healer":
                    case "pulley":
                    case "BuffZone":
                    case "swimArea":
                        continue;
                    case "coconut": // The coconut event. Prop is copied but not edit-supported, we don't need to notify the user since it has no stateful objects. (e.g. 109080002)
                    case "user": // A map prop that dresses the user with predefined items according to his job. No stateful objects. (e.g. 930000010)
                    case "noSkill": // Preset in Monster Carnival maps, can only guess by its name that it blocks skills. Nothing stateful. (e.g. 980031100)
                    case "snowMan": // I don't even know what is this for; it seems to only have 1 prop with a path to the snowman, which points to a nonexistant image. (e.g. 889100001)
                    case "weather": // This has something to do with cash weather items, and exists in some nautlius maps (e.g. 108000500)
                    case "mobMassacre": // This is the Mu Lung Dojo header property (e.g. 926021200)
                    case "battleField": // The sheep vs wolf event and other useless maps (e.g. 109090300, 910040100)
                        copyPropNames.Add(prop.Name);
                        continue;
                    case "snowBall": // The snowball/snowman event. It has the snowman itself, which is a stateful object (somewhat of a mob), but we do not support it.
                    case "monsterCarnival": // The Monster Carnival. It has an immense amount of info and stateful objects, including the mobs and guardians. We do not support it. (e.g. 980000201)
                        copyPropNames.Add(prop.Name);
                        if (!userless)
                        {
                            MessageBox.Show("The map you are opening has the feature \"" + prop.Name + "\", which is purposely not supported in the editor.\r\nTo get around this, HaCreator will copy the original feature's data byte-to-byte. This might cause the feature to stop working if it depends on map objects, such as footholds or mobs.");
                        }
                        continue;
                    default:
                        string loggerSuffix = ", map " + mapImage.Name + ((mapImage.WzFileParent != null) ? (" of version " + Enum.GetName(typeof(WzMapleVersion), mapImage.WzFileParent.MapleVersion) + ", v" + mapImage.WzFileParent.Version.ToString()) : "");
                        string error = "Unknown property " + prop.Name + loggerSuffix;
                        MapleLib.Helpers.ErrorLogger.Log(ErrorLevel.MissingFeature, error);
                        copyPropNames.Add(prop.Name);
                        break;
                }
            }
            return copyPropNames;
        }

        public MapType GetMapType(WzImage mapImage)
        {
            switch (mapImage.Name)
            {
                case "MapLogin1.img":
                case "MapLogin.img":
                    return MapType.MapLogin;
                case "CashShopPreview.img":
                    return MapType.CashShopPreview;
                default:
                    return MapType.RegularMap;
            }
        }

        private static bool GetMapVR(WzImage mapImage, ref System.Drawing.Rectangle? VR)
        {
            WzSubProperty fhParent = (WzSubProperty)mapImage["foothold"];
            if (fhParent == null) { VR = null; return false; }
            int mostRight = int.MinValue, mostLeft = int.MaxValue, mostTop = int.MaxValue, mostBottom = int.MinValue;
            foreach (WzSubProperty fhLayer in fhParent.WzProperties)
            {
                foreach (WzSubProperty fhCat in fhLayer.WzProperties)
                {
                    foreach (WzSubProperty fh in fhCat.WzProperties)
                    {
                        int x1 = InfoTool.GetInt(fh["x1"]);
                        int x2 = InfoTool.GetInt(fh["x2"]);
                        int y1 = InfoTool.GetInt(fh["y1"]);
                        int y2 = InfoTool.GetInt(fh["y2"]);
                        if (x1 > mostRight) mostRight = x1;
                        if (x1 < mostLeft) mostLeft = x1;
                        if (x2 > mostRight) mostRight = x2;
                        if (x2 < mostLeft) mostLeft = x2;
                        if (y1 > mostBottom) mostBottom = y1;
                        if (y1 < mostTop) mostTop = y1;
                        if (y2 > mostBottom) mostBottom = y2;
                        if (y2 < mostTop) mostTop = y2;
                    }
                }
            }
            if (mostRight == int.MinValue || mostLeft == int.MaxValue || mostTop == int.MaxValue || mostBottom == int.MinValue)
            {
                VR = null; return false;
            }
            int VRLeft = mostLeft - 10;
            int VRRight = mostRight + 10;
            int VRBottom = mostBottom + 110;
            int VRTop = Math.Min(mostBottom - 600, mostTop - 360);
            VR = new System.Drawing.Rectangle(VRLeft, VRTop, VRRight - VRLeft, VRBottom - VRTop);
            return true;
        }

        public void LoadLayers(WzImage mapImage, Board mapBoard)
        {
            for (int layer = 0; layer <= 7; layer++)
            {
                WzSubProperty layerProp = (WzSubProperty)mapImage[layer.ToString()];
                WzImageProperty tSprop = layerProp["info"]["tS"];
                string tS = null;
                if (tSprop != null) tS = InfoTool.GetString(tSprop);
                foreach (WzImageProperty obj in layerProp["obj"].WzProperties)
                {
                    int x = InfoTool.GetInt(obj["x"]);
                    int y = InfoTool.GetInt(obj["y"]);
                    int z = InfoTool.GetInt(obj["z"]);
                    int zM = InfoTool.GetInt(obj["zM"]);
                    string oS = InfoTool.GetString(obj["oS"]);
                    string l0 = InfoTool.GetString(obj["l0"]);
                    string l1 = InfoTool.GetString(obj["l1"]);
                    string l2 = InfoTool.GetString(obj["l2"]);
                    string name = InfoTool.GetOptionalString(obj["name"]);
                    MapleBool r = InfoTool.GetOptionalBool(obj["r"]);
                    MapleBool hide = InfoTool.GetOptionalBool(obj["hide"]);
                    MapleBool reactor = InfoTool.GetOptionalBool(obj["reactor"]);
                    MapleBool flow = InfoTool.GetOptionalBool(obj["flow"]);
                    int? rx = InfoTool.GetOptionalTranslatedInt(obj["rx"]);
                    int? ry = InfoTool.GetOptionalTranslatedInt(obj["ry"]);
                    int? cx = InfoTool.GetOptionalTranslatedInt(obj["cx"]);
                    int? cy = InfoTool.GetOptionalTranslatedInt(obj["cy"]);
                    string tags = InfoTool.GetOptionalString(obj["tags"]);
                    WzImageProperty questParent = obj["quest"];
                    List<ObjectInstanceQuest> questInfo = null;
                    if (questParent != null)
                    {
                        questInfo = new List<ObjectInstanceQuest>();
                        foreach (WzIntProperty info in questParent.WzProperties)
                        {
                            questInfo.Add(new ObjectInstanceQuest(int.Parse(info.Name), (QuestState)info.Value));
                        }
                    }
                    bool flip = InfoTool.GetBool(obj["f"]);
                    ObjectInfo objInfo = ObjectInfo.Get(oS, l0, l1, l2);
                    Layer l = mapBoard.Layers[layer];
                    mapBoard.BoardItems.TileObjs.Add((LayeredItem)objInfo.CreateInstance(l, mapBoard, x, y, z, zM, r, hide, reactor, flow, rx, ry, cx, cy, name, tags, questInfo, flip, false));
                    l.zMList.Add(zM);
                }
                WzImageProperty tileParent = layerProp["tile"];
                foreach (WzImageProperty tile in tileParent.WzProperties)
                {
                    int x = InfoTool.GetInt(tile["x"]);
                    int y = InfoTool.GetInt(tile["y"]);
                    int zM = InfoTool.GetInt(tile["zM"]);
                    string u = InfoTool.GetString(tile["u"]);
                    int no = InfoTool.GetInt(tile["no"]);
                    Layer l = mapBoard.Layers[layer];
                    TileInfo tileInfo = TileInfo.Get(tS, u, no.ToString());
                    mapBoard.BoardItems.TileObjs.Add((LayeredItem)tileInfo.CreateInstance(l, mapBoard, x, y, int.Parse(tile.Name), zM, false, false));
                    l.zMList.Add(zM);
                }
            }
        }

        public void LoadLife(WzImage mapImage, Board mapBoard)
        {
            WzImageProperty lifeParent = mapImage["life"];
            if (lifeParent == null) return;
            foreach (WzSubProperty life in lifeParent.WzProperties)
            {
                string id = InfoTool.GetString(life["id"]);
                int x = InfoTool.GetInt(life["x"]);
                int y = InfoTool.GetInt(life["y"]);
                int cy = InfoTool.GetInt(life["cy"]);
                int? mobTime = InfoTool.GetOptionalInt(life["mobTime"]);
                int? info = InfoTool.GetOptionalInt(life["info"]);
                int? team = InfoTool.GetOptionalInt(life["team"]);
                int rx0 = InfoTool.GetInt(life["rx0"]);
                int rx1 = InfoTool.GetInt(life["rx1"]);
                MapleBool flip = InfoTool.GetOptionalBool(life["f"]);
                MapleBool hide = InfoTool.GetOptionalBool(life["hide"]);
                string type = InfoTool.GetString(life["type"]);
                string limitedname = InfoTool.GetOptionalString(life["limitedname"]);

                switch (type)
                {
                    case "m":
                        MobInfo mobInfo = MobInfo.Get(id);
                        if (mobInfo == null)
                            continue;
                        mapBoard.BoardItems.Mobs.Add((MobInstance)mobInfo.CreateInstance(mapBoard, x, cy, x - rx0, rx1 - x, cy - y, limitedname, mobTime, flip, hide, info, team));
                        break;
                    case "n":
                        NpcInfo npcInfo = NpcInfo.Get(id);
                        if (npcInfo == null)
                            continue;
                        mapBoard.BoardItems.NPCs.Add((NpcInstance)npcInfo.CreateInstance(mapBoard, x, cy, x - rx0, rx1 - x, cy - y, limitedname, mobTime, flip, hide, info, team));
                        break;
                    default:
                        throw new Exception("invalid life type " + type);
                }
            }
        }

        public void LoadReactors(WzImage mapImage, Board mapBoard)
        {
            WzSubProperty reactorParent = (WzSubProperty)mapImage["reactor"];
            if (reactorParent == null) return;
            foreach (WzSubProperty reactor in reactorParent.WzProperties)
            {
                int x = InfoTool.GetInt(reactor["x"]);
                int y = InfoTool.GetInt(reactor["y"]);
                int reactorTime = InfoTool.GetInt(reactor["reactorTime"]);
                string name = InfoTool.GetOptionalString(reactor["name"]);
                string id = InfoTool.GetString(reactor["id"]);
                bool flip = InfoTool.GetBool(reactor["f"]);
                mapBoard.BoardItems.Reactors.Add((ReactorInstance)Program.InfoManager.Reactors[id].CreateInstance(mapBoard, x, y, reactorTime, name, flip));
            }
        }

        private void LoadChairs(WzImage mapImage, Board mapBoard)
        {
            WzSubProperty chairParent = (WzSubProperty)mapImage["seat"];
            if (chairParent != null)
            {
                foreach (WzVectorProperty chair in chairParent.WzProperties)
                {
                    mapBoard.BoardItems.Chairs.Add(new Chair(mapBoard, chair.X.Value, chair.Y.Value));
                }
            }
            mapBoard.BoardItems.Chairs.Sort(new Comparison<Chair>(
                    delegate(Chair a, Chair b)
                    {
                        if (a.X > b.X)
                            return 1;
                        else if (a.X < b.X)
                            return -1;
                        else
                        {
                            if (a.Y > b.Y)
                                return 1;
                            else if (a.Y < b.Y)
                                return -1;
                            else return 0;
                        }
                    }));
            for (int i = 0; i < mapBoard.BoardItems.Chairs.Count - 1; i++)
            {
                Chair a = mapBoard.BoardItems.Chairs[i];
                Chair b = mapBoard.BoardItems.Chairs[i + 1];
                if (a.Y == b.Y && a.X == b.X) //removing b is more comfortable because that way we don't need to decrease i
                {
                    if (a.Parent == null && b.Parent != null)
                    {
                        mapBoard.BoardItems.Chairs.Remove(a);
                        i--;
                    }
                    else
                        mapBoard.BoardItems.Chairs.Remove(b);

                }
            }
        }

        public void LoadRopes(WzImage mapImage, Board mapBoard)
        {
            WzSubProperty ropeParent = (WzSubProperty)mapImage["ladderRope"];
            foreach (WzSubProperty rope in ropeParent.WzProperties)
            {
                int x = InfoTool.GetInt(rope["x"]);
                int y1 = InfoTool.GetInt(rope["y1"]);
                int y2 = InfoTool.GetInt(rope["y2"]);
                bool uf = InfoTool.GetBool(rope["uf"]);
                int page = InfoTool.GetInt(rope["page"]);
                bool l = InfoTool.GetBool(rope["l"]);
                mapBoard.BoardItems.Ropes.Add(new Rope(mapBoard, x, y1, y2, l, page, uf));
            }
        }

        private bool IsAnchorPrevOfFoothold(FootholdAnchor a, FootholdLine x)
        {
            int prevnum = x.prev;
            int nextnum = x.next;

            foreach (FootholdLine l in a.connectedLines)
            {
                if (l.num == prevnum)
                {
                    return true;
                }
                else if (l.num == nextnum)
                {
                    return false;
                }
            }

            throw new Exception("Could not match anchor to foothold");
        }

        public void LoadFootholds(WzImage mapImage, Board mapBoard)
        {
            List<FootholdAnchor> anchors = new List<FootholdAnchor>();
            WzSubProperty footholdParent = (WzSubProperty)mapImage["foothold"];
            int layer;
            FootholdAnchor a;
            FootholdAnchor b;
            Dictionary<int, FootholdLine> fhs = new Dictionary<int, FootholdLine>();
            foreach (WzSubProperty layerProp in footholdParent.WzProperties)
            {
                layer = int.Parse(layerProp.Name);
                Layer l = mapBoard.Layers[layer];
                foreach (WzSubProperty platProp in layerProp.WzProperties)
                {
                    int zM = int.Parse(platProp.Name);
                    l.zMList.Add(zM);
                    foreach (WzSubProperty fhProp in platProp.WzProperties)
                    {
                        a = new FootholdAnchor(mapBoard, InfoTool.GetInt(fhProp["x1"]), InfoTool.GetInt(fhProp["y1"]), layer, zM, false);
                        b = new FootholdAnchor(mapBoard, InfoTool.GetInt(fhProp["x2"]), InfoTool.GetInt(fhProp["y2"]), layer, zM, false);
                        int num = int.Parse(fhProp.Name);
                        int next = InfoTool.GetInt(fhProp["next"]);
                        int prev = InfoTool.GetInt(fhProp["prev"]);
                        MapleBool cantThrough = InfoTool.GetOptionalBool(fhProp["cantThrough"]);
                        MapleBool forbidFallDown = InfoTool.GetOptionalBool(fhProp["forbidFallDown"]);
                        int? piece = InfoTool.GetOptionalInt(fhProp["piece"]);
                        int? force = InfoTool.GetOptionalInt(fhProp["force"]);
                        if (a.X != b.X || a.Y != b.Y)
                        {
                            FootholdLine fh = new FootholdLine(mapBoard, a, b, forbidFallDown, cantThrough, piece, force);
                            fh.num = num;
                            fh.prev = prev;
                            fh.next = next;
                            mapBoard.BoardItems.FootholdLines.Add(fh);
                            fhs[num] = fh;
                            anchors.Add(a);
                            anchors.Add(b);
                        }
                    }
                }

                anchors.Sort(new Comparison<FootholdAnchor>(FootholdAnchor.FHAnchorSorter));
                for (int i = 0; i < anchors.Count - 1; i++)
                {
                    a = anchors[i];
                    b = anchors[i + 1];
                    if (a.X == b.X && a.Y == b.Y)
                    {
                        FootholdAnchor.MergeAnchors(a, b); // Transfer lines from b to a
                        anchors.RemoveAt(i + 1); // Remove b
                        i--; // Fix index after we removed b
                    }
                }
                foreach (FootholdAnchor anchor in anchors)
                {
                    if (anchor.connectedLines.Count > 2)
                    {
                        foreach (FootholdLine line in anchor.connectedLines)
                        {
                            if (IsAnchorPrevOfFoothold(anchor, line))
                            {
                                if (fhs.ContainsKey(line.prev))
                                {
                                    line.prevOverride = fhs[line.prev];
                                }
                            }
                            else
                            {
                                if (fhs.ContainsKey(line.next))
                                {
                                    line.nextOverride = fhs[line.next];
                                }
                            }
                        }
                    }
                    mapBoard.BoardItems.FHAnchors.Add(anchor);
                }
                anchors.Clear();
            }
        }

        public static void GenerateDefaultZms(Board mapBoard)
        {
            // generate default zM's
            HashSet<int> allExistingZMs = new HashSet<int>();
            foreach (Layer l in mapBoard.Layers)
            {
                l.RecheckTileSet();
                l.RecheckZM();
                l.zMList.ToList().ForEach(y => allExistingZMs.Add(y));
            }

            for (int i = 0; i < mapBoard.Layers.Count; i++)
            {
                for (int zm_cand = 0; mapBoard.Layers[i].zMList.Count == 0; zm_cand++)
                {
                    // Choose a zM that is free
                    if (!allExistingZMs.Contains(zm_cand))
                    {
                        mapBoard.Layers[i].zMList.Add(zm_cand);
                        allExistingZMs.Add(zm_cand);
                        break;
                    }
                }
            }
        }

        public void LoadPortals(WzImage mapImage, Board mapBoard)
        {
            WzSubProperty portalParent = (WzSubProperty)mapImage["portal"];
            foreach (WzSubProperty portal in portalParent.WzProperties)
            {
                int x = InfoTool.GetInt(portal["x"]);
                int y = InfoTool.GetInt(portal["y"]);
                string pt = Program.InfoManager.PortalTypeById[InfoTool.GetInt(portal["pt"])];
                int tm = InfoTool.GetInt(portal["tm"]);
                string tn = InfoTool.GetString(portal["tn"]);
                string pn = InfoTool.GetString(portal["pn"]);
                string image = InfoTool.GetOptionalString(portal["image"]);
                string script = InfoTool.GetOptionalString(portal["script"]);
                int? verticalImpact = InfoTool.GetOptionalInt(portal["verticalImpact"]);
                int? horizontalImpact = InfoTool.GetOptionalInt(portal["horizontalImpact"]);
                int? hRange = InfoTool.GetOptionalInt(portal["hRange"]);
                int? vRange = InfoTool.GetOptionalInt(portal["vRange"]);
                int? delay = InfoTool.GetOptionalInt(portal["delay"]);
                MapleBool hideTooltip = InfoTool.GetOptionalBool(portal["hideTooltip"]);
                MapleBool onlyOnce = InfoTool.GetOptionalBool(portal["onlyOnce"]);
                mapBoard.BoardItems.Portals.Add(PortalInfo.GetPortalInfoByType(pt).CreateInstance(mapBoard, x, y, pn, tn, tm, script, delay, hideTooltip, onlyOnce, horizontalImpact, verticalImpact, image, hRange, vRange));
            }
        }

        public void LoadToolTips(WzImage mapImage, Board mapBoard)
        {
            WzSubProperty tooltipsParent = (WzSubProperty)mapImage["ToolTip"];
            if (tooltipsParent == null)
            {
                return;
            }

            WzImage tooltipsStringImage = (WzImage)Program.WzManager.String["ToolTipHelp.img"];
            if (!tooltipsStringImage.Parsed)
            {
                tooltipsStringImage.ParseImage();
            }

            WzSubProperty tooltipStrings = (WzSubProperty)tooltipsStringImage["Mapobject"][mapBoard.MapInfo.id.ToString()];
            if (tooltipStrings == null)
            {
                return;
            }

            for (int i = 0; true; i++)
            {
                string num = i.ToString();
                WzSubProperty tooltipString = (WzSubProperty)tooltipStrings[num];
                WzSubProperty tooltipProp = (WzSubProperty)tooltipsParent[num];
                WzSubProperty tooltipChar = (WzSubProperty)tooltipsParent[num + "char"];
                if (tooltipString == null && tooltipProp == null) break;
                if (tooltipString == null ^ tooltipProp == null) continue;
                string title = InfoTool.GetOptionalString(tooltipString["Title"]);
                string desc = InfoTool.GetOptionalString(tooltipString["Desc"]);
                int x1 = InfoTool.GetInt(tooltipProp["x1"]);
                int x2 = InfoTool.GetInt(tooltipProp["x2"]);
                int y1 = InfoTool.GetInt(tooltipProp["y1"]);
                int y2 = InfoTool.GetInt(tooltipProp["y2"]);
                Microsoft.Xna.Framework.Rectangle tooltipPos = new Microsoft.Xna.Framework.Rectangle(x1, y1, x2 - x1, y2 - y1);
                ToolTipInstance tt = new ToolTipInstance(mapBoard, tooltipPos, title, desc, i);
                mapBoard.BoardItems.ToolTips.Add(tt);
                if (tooltipChar != null)
                {
                    x1 = InfoTool.GetInt(tooltipChar["x1"]);
                    x2 = InfoTool.GetInt(tooltipChar["x2"]);
                    y1 = InfoTool.GetInt(tooltipChar["y1"]);
                    y2 = InfoTool.GetInt(tooltipChar["y2"]);
                    tooltipPos = new Microsoft.Xna.Framework.Rectangle(x1, y1, x2 - x1, y2 - y1);
                    ToolTipChar ttc = new ToolTipChar(mapBoard, tooltipPos, tt);
                    mapBoard.BoardItems.CharacterToolTips.Add(ttc);
                }
            }
        }

        public void LoadBackgrounds(WzImage mapImage, Board mapBoard)
        {
            WzSubProperty bgParent = (WzSubProperty)mapImage["back"];
            WzSubProperty bgProp;
            int i = 0;
            while ((bgProp = (WzSubProperty)bgParent[(i++).ToString()]) != null)
            {
                int x = InfoTool.GetInt(bgProp["x"]);
                int y = InfoTool.GetInt(bgProp["y"]);
                int rx = InfoTool.GetInt(bgProp["rx"]);
                int ry = InfoTool.GetInt(bgProp["ry"]);
                int cx = InfoTool.GetInt(bgProp["cx"]);
                int cy = InfoTool.GetInt(bgProp["cy"]);
                int a = InfoTool.GetInt(bgProp["a"]);
                BackgroundType type = (BackgroundType)InfoTool.GetInt(bgProp["type"]);
                bool front = InfoTool.GetBool(bgProp["front"]);
                bool? flip_t = InfoTool.GetOptionalBool(bgProp["f"]);
                bool flip = flip_t.HasValue ? flip_t.Value : false;
                string bS = InfoTool.GetString(bgProp["bS"]);
                bool ani = InfoTool.GetBool(bgProp["ani"]);
                string no = InfoTool.GetInt(bgProp["no"]).ToString();
                BackgroundInfo bgInfo = BackgroundInfo.Get(bS, ani, no);
                if (bgInfo == null)
                    continue;
                IList list = front ? mapBoard.BoardItems.FrontBackgrounds : mapBoard.BoardItems.BackBackgrounds;
                list.Add((BackgroundInstance)bgInfo.CreateInstance(mapBoard, x, y, i, rx, ry, cx, cy, type, a, front, flip));
            }
        }

        public void LoadMisc(WzImage mapImage, Board mapBoard)
        {
            // All of the following properties are extremely esoteric features that only appear in a handful of maps. 
            // They are implemented here for the sake of completeness, and being able to repack their maps without corruption.

            WzImageProperty clock = mapImage["clock"];
            WzImageProperty ship = mapImage["shipObj"];
            WzImageProperty area = mapImage["area"];
            WzImageProperty healer = mapImage["healer"];
            WzImageProperty pulley = mapImage["pulley"];
            WzImageProperty BuffZone = mapImage["BuffZone"];
            WzImageProperty swimArea = mapImage["swimArea"];
            if (clock != null)
            {
                Clock clockInstance = new Clock(mapBoard, new Rectangle(InfoTool.GetInt(clock["x"]), InfoTool.GetInt(clock["y"]), InfoTool.GetInt(clock["width"]), InfoTool.GetInt(clock["height"])));
                mapBoard.BoardItems.Add(clockInstance, false);
            }
            if (ship != null)
            {
                string objPath = InfoTool.GetString(ship["shipObj"]);
                string[] objPathParts = objPath.Split("/".ToCharArray());
                string oS = WzInfoTools.RemoveExtension(objPathParts[objPathParts.Length - 4]);
                string l0 = objPathParts[objPathParts.Length - 3];
                string l1 = objPathParts[objPathParts.Length - 2];
                string l2 = objPathParts[objPathParts.Length - 1];
                ObjectInfo objInfo = ObjectInfo.Get(oS, l0, l1, l2);
                ShipObject shipInstance = new ShipObject(objInfo, mapBoard, 
                    InfoTool.GetInt(ship["x"]), 
                    InfoTool.GetInt(ship["y"]), 
                    InfoTool.GetOptionalInt(ship["z"]), 
                    InfoTool.GetOptionalInt(ship["x0"]), 
                    InfoTool.GetInt(ship["tMove"]), 
                    InfoTool.GetInt(ship["shipKind"]), 
                    InfoTool.GetBool(ship["f"]));
                mapBoard.BoardItems.Add(shipInstance, false);
            }
            if (area != null)
            {
                foreach (WzImageProperty prop in area.WzProperties)
                {
                    int x1 = InfoTool.GetInt(prop["x1"]);
                    int x2 = InfoTool.GetInt(prop["x2"]);
                    int y1 = InfoTool.GetInt(prop["y1"]);
                    int y2 = InfoTool.GetInt(prop["y2"]);
                    Area currArea = new Area(mapBoard, new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)), prop.Name);
                    mapBoard.BoardItems.Add(currArea, false);
                }
            }
            if (healer != null)
            {
                string objPath = InfoTool.GetString(healer["healer"]);
                string[] objPathParts = objPath.Split("/".ToCharArray());
                string oS = WzInfoTools.RemoveExtension(objPathParts[objPathParts.Length - 4]);
                string l0 = objPathParts[objPathParts.Length - 3];
                string l1 = objPathParts[objPathParts.Length - 2];
                string l2 = objPathParts[objPathParts.Length - 1];
                ObjectInfo objInfo = ObjectInfo.Get(oS, l0, l1, l2);
                Healer healerInstance = new Healer(objInfo, mapBoard,
                    InfoTool.GetInt(healer["x"]),
                    InfoTool.GetInt(healer["yMin"]),
                    InfoTool.GetInt(healer["yMax"]),
                    InfoTool.GetInt(healer["healMin"]),
                    InfoTool.GetInt(healer["healMax"]),
                    InfoTool.GetInt(healer["fall"]),
                    InfoTool.GetInt(healer["rise"]));
                mapBoard.BoardItems.Add(healerInstance, false);
            }
            if (pulley != null)
            {
                string objPath = InfoTool.GetString(pulley["pulley"]);
                string[] objPathParts = objPath.Split("/".ToCharArray());
                string oS = WzInfoTools.RemoveExtension(objPathParts[objPathParts.Length - 4]);
                string l0 = objPathParts[objPathParts.Length - 3];
                string l1 = objPathParts[objPathParts.Length - 2];
                string l2 = objPathParts[objPathParts.Length - 1];
                ObjectInfo objInfo = ObjectInfo.Get(oS, l0, l1, l2);
                Pulley pulleyInstance = new Pulley(objInfo, mapBoard,
                    InfoTool.GetInt(pulley["x"]),
                    InfoTool.GetInt(pulley["y"]));
                mapBoard.BoardItems.Add(pulleyInstance, false);
            }
            if (BuffZone != null)
            {
                foreach (WzImageProperty zone in BuffZone.WzProperties)
                {
                    int x1 = InfoTool.GetInt(zone["x1"]);
                    int x2 = InfoTool.GetInt(zone["x2"]);
                    int y1 = InfoTool.GetInt(zone["y1"]);
                    int y2 = InfoTool.GetInt(zone["y2"]);
                    int id = InfoTool.GetInt(zone["ItemID"]);
                    int interval = InfoTool.GetInt(zone["Interval"]);
                    int duration = InfoTool.GetInt(zone["Duration"]);
                    BuffZone currZone = new BuffZone(mapBoard, new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)), id, interval, duration, zone.Name);
                    mapBoard.BoardItems.Add(currZone, false);
                }
            }
            if (swimArea != null)
            {
                foreach (WzImageProperty prop in swimArea.WzProperties)
                {
                    int x1 = InfoTool.GetInt(prop["x1"]);
                    int x2 = InfoTool.GetInt(prop["x2"]);
                    int y1 = InfoTool.GetInt(prop["y1"]);
                    int y2 = InfoTool.GetInt(prop["y2"]);
                    SwimArea currArea = new SwimArea(mapBoard, new Rectangle(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)), prop.Name);
                    mapBoard.BoardItems.Add(currArea, false);
                }
            }
            // Some misc items are not implemented here; these are copied byte-to-byte from the original. See VerifyMapPropsKnown for details.
        }

        public ContextMenuStrip CreateStandardMapMenu(EventHandler[] rightClickHandler)
        {
            ContextMenuStrip result = new ContextMenuStrip();
            result.Items.Add(new ToolStripMenuItem("Edit map info...", Properties.Resources.mapEditMenu, rightClickHandler[0]));
            result.Items.Add(new ToolStripMenuItem("Add VR", Properties.Resources.mapEditMenu, rightClickHandler[1]));
            result.Items.Add(new ToolStripMenuItem("Add Minimap", Properties.Resources.mapEditMenu, rightClickHandler[2]));
            return result;
        }

        public static void GetMapDimensions(WzImage mapImage, out Rectangle VR, out Point mapCenter, out Point mapSize, out Point minimapCenter, out Point minimapSize, out bool hasVR, out bool hasMinimap)
        {
            System.Drawing.Rectangle? vr = MapInfo.GetVR(mapImage);
            hasVR = vr.HasValue;
            hasMinimap = mapImage["miniMap"] != null;
            if (!hasMinimap)
            {
                // No minimap, generate sizes from VR
                if (vr == null)
                {
                    // No minimap and no VR, our only chance of getting sizes is by generating a VR, if that fails we're screwed
                    if (!GetMapVR(mapImage, ref vr))
                    {
                        throw new NoVRException();
                    }
                }
                minimapSize = new Point(vr.Value.Width + 10, vr.Value.Height + 10); //leave 5 pixels on each side
                minimapCenter = new Point(5 - vr.Value.Left, 5 - vr.Value.Top);
                mapSize = new Point(minimapSize.X, minimapSize.Y);
                mapCenter = new Point(minimapCenter.X, minimapCenter.Y);
            }
            else
            {
                WzImageProperty miniMap = mapImage["miniMap"];
                minimapSize = new Point(InfoTool.GetInt(miniMap["width"]), InfoTool.GetInt(miniMap["height"]));
                minimapCenter = new Point(InfoTool.GetInt(miniMap["centerX"]), InfoTool.GetInt(miniMap["centerY"]));
                int topOffs = 0, botOffs = 0, leftOffs = 0, rightOffs = 0;
                int leftTarget = 69 - minimapCenter.X, topTarget = 86 - minimapCenter.Y, rightTarget = minimapSize.X - 69 - 69, botTarget = minimapSize.Y - 86 - 86;
                if (vr == null)
                {
                    // We have no VR info, so set all VRs according to their target
                    vr = new System.Drawing.Rectangle(leftTarget, topTarget, rightTarget, botTarget);
                }
                else
                {
                    if (vr.Value.Left < leftTarget)
                    {
                        leftOffs = leftTarget - vr.Value.Left;
                    }
                    if (vr.Value.Top < topTarget)
                    {
                        topOffs = topTarget - vr.Value.Top;
                    }
                    if (vr.Value.Right > rightTarget)
                    {
                        rightOffs = vr.Value.Right - rightTarget;
                    }
                    if (vr.Value.Bottom > botTarget)
                    {
                        botOffs = vr.Value.Bottom - botTarget;
                    }
                }
                mapSize = new Point(minimapSize.X + leftOffs + rightOffs, minimapSize.Y + topOffs + botOffs);
                mapCenter = new Point(minimapCenter.X + leftOffs, minimapCenter.Y + topOffs);
            }
            VR = new Rectangle(vr.Value.X, vr.Value.Y, vr.Value.Width, vr.Value.Height);
        }

        public void CreateMapFromImage(WzImage mapImage, string mapName, string streetName, string categoryName, WzSubProperty strMapProp, PageCollection Tabs, MultiBoard multiBoard, EventHandler[] rightClickHandler)
        {
            if (!mapImage.Parsed) mapImage.ParseImage();
            List<string> copyPropNames = VerifyMapPropsKnown(mapImage, false);
            MapInfo info = new MapInfo(mapImage, mapName, streetName, categoryName);
            foreach (string copyPropName in copyPropNames)
            {
                info.additionalNonInfoProps.Add(mapImage[copyPropName]);
            }
            MapType type = GetMapType(mapImage);
            if (type == MapType.RegularMap)
                info.id = int.Parse(WzInfoTools.RemoveLeadingZeros(WzInfoTools.RemoveExtension(mapImage.Name)));
            info.mapType = type;

            Rectangle VR = new Rectangle();
            Point center = new Point();
            Point size = new Point();
            Point minimapSize = new Point();
            Point minimapCenter = new Point();
            bool hasMinimap = false;
            bool hasVR = false;

            try
            {
                GetMapDimensions(mapImage, out VR, out center, out size, out minimapCenter, out minimapSize, out hasVR, out hasMinimap);
            }
            catch (NoVRException)
            {
                MessageBox.Show("Error - map does not contain size information and HaCreator was unable to generate it. An error has been logged.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ErrorLogger.Log(ErrorLevel.IncorrectStructure, "no size @map " + info.id.ToString());
                return;
            }
            
            lock (multiBoard)
            {
                CreateMap(mapName, WzInfoTools.RemoveLeadingZeros(WzInfoTools.RemoveExtension(mapImage.Name)), CreateStandardMapMenu(rightClickHandler), size, center, 8, Tabs, multiBoard);
                Board mapBoard = multiBoard.SelectedBoard;
                mapBoard.Loading = true; // prevents TS Change callbacks
                mapBoard.MapInfo = info;
                if (hasMinimap)
                {
                    mapBoard.MiniMap = ((WzCanvasProperty)mapImage["miniMap"]["canvas"]).PngProperty.GetPNG(false);
                    System.Drawing.Point mmPos = new System.Drawing.Point(-minimapCenter.X, -minimapCenter.Y);
                    mapBoard.MinimapPosition = mmPos;
                    mapBoard.MinimapRectangle = new MinimapRectangle(mapBoard, new Rectangle(mmPos.X, mmPos.Y, minimapSize.X, minimapSize.Y));
                }
                if (hasVR)
                {
                    mapBoard.VRRectangle = new VRRectangle(mapBoard, VR);
                }
                LoadLayers(mapImage, mapBoard);
                LoadLife(mapImage, mapBoard);
                LoadFootholds(mapImage, mapBoard);
                GenerateDefaultZms(mapBoard);
                LoadRopes(mapImage, mapBoard);
                LoadChairs(mapImage, mapBoard);
                LoadPortals(mapImage, mapBoard);
                LoadReactors(mapImage, mapBoard);
                LoadToolTips(mapImage, mapBoard);
                LoadBackgrounds(mapImage, mapBoard);
                LoadMisc(mapImage, mapBoard);

                mapBoard.BoardItems.Sort();
                mapBoard.Loading = false;
            }
            if (ErrorLogger.ErrorsPresent())
            {
                ErrorLogger.SaveToFile("errors.txt");
                if (UserSettings.ShowErrorsMessage)
                {
                    MessageBox.Show("Errors were encountered during the loading process. These errors were saved to \"errors.txt\". Please send this file to the author, either via mail (" + ApplicationSettings.AuthorEmail + ") or from the site you got this software from.\n\n(In the case that this program was not updated in so long that this message is now thrown on every map load, you may cancel this message from the settings)", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                ErrorLogger.ClearErrors();
            }
        }

        public void CreateMap(string text, string tooltip, ContextMenuStrip menu, Point size, Point center, int layers, HaCreator.ThirdParty.TabPages.PageCollection Tabs, MultiBoard multiBoard)
        {
            lock (multiBoard)
            {
                Board newBoard = multiBoard.CreateBoard(size, center, layers, menu);
                GenerateDefaultZms(newBoard);
                HaCreator.ThirdParty.TabPages.TabPage page = new HaCreator.ThirdParty.TabPages.TabPage(text, multiBoard, tooltip, menu);
                newBoard.TabPage = page;
                page.Tag = newBoard;
                Tabs.Add(page);
                Tabs.CurrentPage = page;
                multiBoard.SelectedBoard = newBoard;
                menu.Tag = newBoard;
                foreach (ToolStripItem item in menu.Items)
                    item.Tag = newBoard;
            }
        }

        public void CreateMapFromHam(MultiBoard multiBoard, HaCreator.ThirdParty.TabPages.PageCollection Tabs, string data, EventHandler[] rightClickHandler)
        {
            CreateMap("", "", CreateStandardMapMenu(rightClickHandler), new XNA.Point(), new XNA.Point(), 8, Tabs, multiBoard);
            multiBoard.SelectedBoard.Loading = true; // Prevent TS Change callbacks while were loading
            lock (multiBoard)
            {
                multiBoard.SelectedBoard.SerializationManager.DeserializeBoard(data);
                multiBoard.AdjustScrollBars();
            }
            multiBoard.SelectedBoard.Loading = false;
        }
    }

    public class NoVRException : Exception
    {
    }
}
