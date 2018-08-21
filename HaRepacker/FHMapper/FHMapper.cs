using Footholds;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Diagnostics;
using MapleLib;
using HaRepacker.GUI.Panels;

namespace HaRepacker.FHMapper
{
    public class FHMapper
    {
        public static string SettingsPath = Path.Combine(Program.GetLocalFolderPath(), "Settings.ini");
        public List<Object> settings = new List<object>();
        private MainPanel MainPanel;
        private TreeNode node;

        // Fonts
        private static Font FONT_DISPLAY_MAPID = new Font("Segoe UI", 20);
        private static Font FONT_GAME_TOOLTIP = new Font("Segoe UI", 9);
        private static Font FONT_DISPLAY_MINIMAP_NOT_AVAILABLE = new Font("Segoe UI", 18);
        private static Font FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD = new Font("Segoe UI", 8);

        public FHMapper(MainPanel MainPanel)
        {
            this.MainPanel = MainPanel;
        }

        #region Renders
        private Bitmap RenderMinimap(Size bmpSize, WzFile wzFile, WzImage img, string mapIdName, WzSubProperty miniMapSubProperty)
        {
            Bitmap minimapRender = new Bitmap(400, 200);
            using (Graphics drawBuf = Graphics.FromImage(minimapRender))
            {
                // Draw map mark
                WzStringProperty mapMark = ((WzStringProperty)img["info"]["mapMark"]);
                if (mapMark != null)
                {
                    string mapMarkPath = wzFile.WzDirectory.Name + "/MapHelper.img/mark/" + mapMark.GetString();
                    WzCanvasProperty mapMarkCanvas = (WzCanvasProperty)wzFile.GetObjectFromPath(mapMarkPath);

                    if (mapMarkCanvas != null && mapMark.ToString() != "None") // Doesnt have to render mapmark if its not available. Actual client does not crash
                    {
                        drawBuf.DrawImage(mapMarkCanvas.GetLinkedWzCanvasBitmap(), 10, 10);
                    }
                }
                // Get map name
                string mapName = string.Empty;
                string streetName = string.Empty;

                string mapNameStringPath = "String.wz/Map.img";
                WzImage mapNameImages = (WzImage)WzFile.GetObjectFromMultipleWzFilePath(mapNameStringPath, Program.WzMan.WzFileListReadOnly);
                foreach (WzSubProperty subAreaImgProp in mapNameImages.WzProperties)
                {
                    foreach (WzSubProperty mapImg in subAreaImgProp.WzProperties)
                    {
                        if (mapImg.Name == mapIdName)
                        {
                            mapName = mapImg["mapName"].ReadString(string.Empty);
                            streetName = mapImg["streetName"].ReadString(string.Empty);
                            break;
                        }
                    }
                }

                // Draw map name and ID
                //drawBuf.FillRectangle(new SolidBrush(Color.CornflowerBlue), 0, 0, bmpSize.Width, bmpSize.Height);
                drawBuf.DrawString(string.Format("[{0}] {1}", mapIdName, streetName), FONT_DISPLAY_MAPID, new SolidBrush(Color.Black), new PointF(60, 10));
                drawBuf.DrawString(mapName, FONT_DISPLAY_MAPID, new SolidBrush(Color.Black), new PointF(60, 30));

                // Draw mini map
                if (miniMapSubProperty != null)
                    drawBuf.DrawImage(((WzCanvasProperty)miniMapSubProperty["canvas"]).GetLinkedWzCanvasBitmap(), 10, 80);
                else
                {
                    drawBuf.DrawString("Minimap not availible", FONT_DISPLAY_MINIMAP_NOT_AVAILABLE, new SolidBrush(Color.Black), new PointF(10, 45));
                }
            }
            minimapRender.Save("Renders\\" + mapIdName + "\\" + mapIdName + "_miniMapRender.bmp");
            return minimapRender;
        }
        #endregion

        /// <summary>
        /// Attempts to render the map and save the progress
        /// </summary>
        /// <param name="img"></param>
        /// <param name="zoom"></param>
        /// <param name="errorList"></param>
        public bool TryRenderMapAndSave(WzImage img, double zoom, ref List<string> errorList)
        {
            string mapIdName = img.Name.Substring(0, img.Name.Length - 4);

            node = MainPanel.DataTree.SelectedNode;
            WzFile wzFile = ((WzObject)node.Tag).WzFileParent;

            // Spawnpoint foothold and portal lists
            List<SpawnPoint.Spawnpoint> MSPs = new List<SpawnPoint.Spawnpoint>();
            List<FootHold.Foothold> FHs = new List<FootHold.Foothold>();
            List<Portals.Portal> Ps = new List<Portals.Portal>();
            Size bmpSize;
            Point center;

            WzSubProperty miniMapSubProperty = ((WzSubProperty)img["miniMap"]);
            try
            {
                bmpSize = new Size(((WzIntProperty)miniMapSubProperty["width"]).Value, ((WzIntProperty)miniMapSubProperty["height"]).Value);
                center = new Point(((WzIntProperty)miniMapSubProperty["centerX"]).Value, ((WzIntProperty)miniMapSubProperty["centerY"]).Value);
            }
            catch (Exception exp)
            {
                if (exp is KeyNotFoundException || exp is NullReferenceException)
                {
                    try
                    {
                        WzSubProperty infoSubProperty = ((WzSubProperty)img["info"]);

                        bmpSize = new Size(((WzIntProperty)infoSubProperty["VRRight"]).Value - ((WzIntProperty)infoSubProperty["VRLeft"]).Value, ((WzIntProperty)infoSubProperty["VRBottom"]).Value - ((WzIntProperty)infoSubProperty["VRTop"]).Value);
                        center = new Point(((WzIntProperty)infoSubProperty["VRRight"]).Value, ((WzIntProperty)infoSubProperty["VRBottom"]).Value);
                    }
                    catch
                    {
                        errorList.Add("Missing map info WzSubProperty. Path: " + mapIdName + ".img/info/VRRight; VRLeft; VRBottom; VRTop\r\n OR info/miniMap/width ; height; centerX; centerY");
                        return false;
                    }
                }
                else
                    return false;
            }

            // Render minimap
            Bitmap minimapRender = RenderMinimap(bmpSize, wzFile, img, mapIdName, miniMapSubProperty);

            // Render map
            Bitmap mapRender = new Bitmap(bmpSize.Width, bmpSize.Height);
            using (Graphics drawBuf = Graphics.FromImage(mapRender))
            {
                WzSubProperty ps = (WzSubProperty)img["portal"];
                foreach (WzSubProperty p in ps.WzProperties)
                {
                    //WzSubProperty p = (WzSubProperty)p10.ExtendedProperty;
                    int x = ((WzIntProperty)p["x"]).Value + center.X;
                    int y = ((WzIntProperty)p["y"]).Value + center.Y;
                    int pt = ((WzIntProperty)p["pt"]).Value;
                    string pn = ((WzStringProperty)p["pn"]).ReadString(string.Empty);
                    int tm = ((WzIntProperty)p["tm"]).ReadValue(999999999);

                    Color pColor = Color.Red;
                    if (pt == 0)
                        pColor = Color.Orange;
                    else if (pt == 2 || pt == 7)//Normal
                        pColor = Color.Blue;
                    else if (pt == 3)//Auto-enter
                        pColor = Color.Magenta;
                    else if (pt == 1 || pt == 8)
                        pColor = Color.BlueViolet;
                    else
                        pColor = Color.IndianRed;

                    // Draw portal preview image
                    bool drewPortalImg = false;
                    if (pn != string.Empty || pt == 2)
                    {
                        string portalEditorImage = wzFile.WzDirectory.Name + "/MapHelper.img/portal/editor/" + (pt == 2 ? "pv" : pn);
                        WzCanvasProperty portalEditorCanvas = (WzCanvasProperty)wzFile.GetObjectFromPath(portalEditorImage);
                        if (portalEditorCanvas != null)
                        {
                            drewPortalImg = true;

                            PointF canvasOriginPosition = portalEditorCanvas.GetCanvasVectorPosition();
                            drawBuf.DrawImage(portalEditorCanvas.GetLinkedWzCanvasBitmap(), x - canvasOriginPosition.X, y - canvasOriginPosition.Y);
                        }
                    }
                    if (!drewPortalImg)
                    {
                        drawBuf.FillRectangle(new SolidBrush(Color.FromArgb(95, pColor.R, pColor.G, pColor.B)), x - 20, y - 20, 40, 40);
                        drawBuf.DrawRectangle(new Pen(Color.Black, 1F), x - 20, y - 20, 40, 40);
                    }

                    // Draw portal name
                    drawBuf.DrawString("Portal: " + p.Name, FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD, new SolidBrush(Color.Red), x - 8, y - 7.7F);

                    Portals.Portal portal = new Portals.Portal();
                    portal.Shape = new Rectangle(x - 20, y - 20, 40, 40);
                    portal.Data = p;
                    Ps.Add(portal);
                }

                WzSubProperty SPs = (WzSubProperty)img["life"];
                foreach (WzSubProperty sp in SPs.WzProperties)
                {
                    Color MSPColor = Color.ForestGreen;

                    string type = ((WzStringProperty)sp["type"]).Value;
                    switch (type)
                    {
                        case "n": // NPC
                        case "m": // monster
                            {
                                bool isNPC = type == "n";
                                int lifeId = int.Parse(((WzStringProperty)sp["id"]).GetString());

                                int x = ((WzIntProperty)sp["x"]).Value + center.X;
                                int y = ((WzIntProperty)sp["y"]).Value + center.Y;
                                int x_text = x - 15;
                                int y_text = y - 15;
                                bool facingLeft = ((WzIntProperty)sp["f"]).ReadValue(0) == 0; // This value is optional. If its not stated in the WZ, its assumed to be 0

                                SpawnPoint.Spawnpoint MSP = new SpawnPoint.Spawnpoint();
                                MSP.Shape = new Rectangle(x_text, y_text, 30, 30);
                                MSP.Data = sp;
                                MSPs.Add(MSP);


                                // Render monster image
                                string lifeStrId = lifeId.ToString().PadLeft(7, '0');

                                string mobWzPath;
                                string mobLinkWzPath;
                                string mobNamePath;

                                if (!isNPC)
                                {
                                    mobWzPath = string.Format("Mob.wz/{0}.img/info/link", lifeStrId);
                                    mobNamePath = string.Format("String.wz/Mob.img/{0}/name", lifeId);
                                }
                                else
                                {
                                    mobWzPath = string.Format("Npc.wz/{0}.img/info/link", lifeStrId);
                                    mobNamePath = string.Format("String.wz/Npc.img/{0}/name", lifeId);
                                }

                                WzStringProperty linkInfo = (WzStringProperty)WzFile.GetObjectFromMultipleWzFilePath(mobWzPath, Program.WzMan.WzFileListReadOnly);
                                if (linkInfo != null)
                                {
                                    lifeId = int.Parse(linkInfo.GetString());
                                    lifeStrId = lifeId.ToString().PadLeft(7, '0');
                                }

                                if (!isNPC)
                                    mobLinkWzPath = string.Format("Mob.wz/{0}.img/stand/0", lifeStrId);
                                else
                                    mobLinkWzPath = string.Format("Npc.wz/{0}.img/stand/0", lifeStrId);

                                WzCanvasProperty lifeImg = (WzCanvasProperty)WzFile.GetObjectFromMultipleWzFilePath(mobLinkWzPath, Program.WzMan.WzFileListReadOnly);
                                if (lifeImg != null)
                                {
                                    PointF canvasOriginPosition = lifeImg.GetCanvasVectorPosition();
                                    PointF renderXY = new PointF(x - canvasOriginPosition.X, y - canvasOriginPosition.Y);

                                    Bitmap renderMobbitmap = lifeImg.GetLinkedWzCanvasBitmap();

                                    if (!facingLeft)
                                        renderMobbitmap.RotateFlip(RotateFlipType.RotateNoneFlipX);

                                    drawBuf.DrawImage(renderMobbitmap, renderXY);
                                }
                                else
                                {
                                    //drawBuf.FillRectangle(new SolidBrush(Color.FromArgb(95, MSPColor.R, MSPColor.G, MSPColor.B)), x_text, y_text, 30, 30);
                                    //drawBuf.DrawRectangle(new Pen(Color.Black, 1F), x_text, y_text, 30, 30);
                                    errorList.Add("Missing monster/npc object. Path: " + mobWzPath + "\r\n" + mobLinkWzPath);
                                }

                                // Get monster name
                                WzStringProperty stringName = (WzStringProperty)WzFile.GetObjectFromMultipleWzFilePath(mobNamePath, Program.WzMan.WzFileListReadOnly);
                                if (stringName != null)
                                    drawBuf.DrawString(string.Format("SP: {0}, Name: {1}, ID: {2}", sp.Name, stringName.GetString(), lifeId), FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD, new SolidBrush(Color.Red), x_text + 7, y_text + 7.3F);
                                else
                                    errorList.Add("Missing monster/npc string object. Path: " + mobNamePath);
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }


                WzSubProperty fhs = (WzSubProperty)img["foothold"];
                foreach (WzImageProperty fhspl0 in fhs.WzProperties)
                {
                    foreach (WzImageProperty fhspl1 in fhspl0.WzProperties)
                    {
                        Color c = Color.FromArgb(95, Color.FromArgb(GetPseudoRandomColor(fhspl1.Name)));
                        foreach (WzSubProperty fh in fhspl1.WzProperties)
                        {
                            int x = ((WzIntProperty)fh["x1"]).Value + center.X;
                            int y = ((WzIntProperty)fh["y1"]).Value + center.Y;
                            int width = ((((WzIntProperty)fh["x2"]).Value + center.X) - x);
                            int height = ((((WzIntProperty)fh["y2"]).Value + center.Y) - y);

                            if (width < 0)
                            {
                                x += width;// *2;
                                width = -width;
                            }
                            if (height < 0)
                            {
                                y += height;// *2;
                                height = -height;
                            }
                            if (width == 0 || width < 15)
                                width = 15;
                            height += 10;

                            FootHold.Foothold nFH = new FootHold.Foothold();
                            nFH.Shape = new Rectangle(x, y, width, height);
                            nFH.Data = fh;
                            FHs.Add(nFH);

                            //drawBuf.FillRectangle(new SolidBrush(Color.FromArgb(95, Color.Gray.R, Color.Gray.G, Color.Gray.B)), x, y, width, height);
                            drawBuf.FillRectangle(new SolidBrush(c), x, y, width, height);
                            drawBuf.DrawRectangle(new Pen(Color.Black, 1F), x, y, width, height);
                            drawBuf.DrawString(fh.Name, FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD, new SolidBrush(Color.Red), new PointF(x + (width / 2) - 8, y + (height / 2) - 7.7F));
                        }
                    }
                }
            }
            mapRender.Save("Renders\\" + mapIdName + "\\" + mapIdName + "_footholdRender.bmp");

            Bitmap backgroundRender = new Bitmap(bmpSize.Width, bmpSize.Height);
            using (Graphics tileBuf = Graphics.FromImage(backgroundRender))
            {
                WzSubProperty backImg = (WzSubProperty)img["back"];
                if (backImg != null)
                {
                    foreach (WzSubProperty bgItem in backImg.WzProperties)
                    {
                        string bS = ((WzStringProperty)bgItem["bS"]).Value;
                        int front = ((WzIntProperty)bgItem["front"]).Value;
                        int ani = ((WzIntProperty)bgItem["ani"]).Value;
                        int no = ((WzIntProperty)bgItem["no"]).Value;
                        int x = ((WzIntProperty)bgItem["x"]).Value;
                        int y = ((WzIntProperty)bgItem["y"]).Value;
                        int rx = ((WzIntProperty)bgItem["rx"]).Value;
                        int ry = ((WzIntProperty)bgItem["ry"]).Value;
                        int type = ((WzIntProperty)bgItem["type"]).Value;
                        int cx = ((WzIntProperty)bgItem["cx"]).Value;
                        int cy = ((WzIntProperty)bgItem["cy"]).Value;
                        int a = ((WzIntProperty)bgItem["a"]).Value;
                        bool facingLeft = ((WzIntProperty)bgItem["f"]).ReadValue(0) == 0;

                        if (bS == string.Empty)
                            continue;

                        string bgObjImagePath = "Map.wz/Back/" + bS + ".img/Back/" + no;
                        WzCanvasProperty wzBgCanvas = (WzCanvasProperty)WzFile.GetObjectFromMultipleWzFilePath(bgObjImagePath, Program.WzMan.WzFileListReadOnly);
                        if (wzBgCanvas != null)
                        {
                            PointF canvasOriginPosition = wzBgCanvas.GetCanvasVectorPosition();
                            PointF renderXY = new PointF(x + canvasOriginPosition.X + center.X, y + canvasOriginPosition.X + center.Y);

                            Bitmap drawImage = wzBgCanvas.GetLinkedWzCanvasBitmap();

                            if (!facingLeft)
                                drawImage.RotateFlip(RotateFlipType.RotateNoneFlipX);

                            tileBuf.DrawImage(drawImage, renderXY);
                        }
                        else
                        {
                            errorList.Add("Missing Map BG object. Path: " + bgObjImagePath);
                        }
                    }
                }
            }
            backgroundRender.Save("Renders\\" + mapIdName + "\\" + mapIdName + "_backgroundRender.bmp");


            // Render tooltip
            WzSubProperty tooltipProperty = (WzSubProperty)img["ToolTip"];
            Bitmap toolTip = null;
            if (tooltipProperty != null)
            {
                toolTip = new Bitmap(bmpSize.Width, bmpSize.Height);
                using (Graphics toolTipBuf = Graphics.FromImage(toolTip))
                {
                    string stringTooltipPath = "String.wz/ToolTipHelp.img/Mapobject/" + mapIdName;
                    WzSubProperty wzToolTip = (WzSubProperty)WzFile.GetObjectFromMultipleWzFilePath(stringTooltipPath, Program.WzMan.WzFileListReadOnly);

                    if (wzToolTip == null)
                    {
                        errorList.Add("Map tooltip object is missing. Path: " + stringTooltipPath);
                    }

                    for (int i = 0; i < 99; i++) // starts from 0
                    {
                        WzSubProperty toolTipItem = (WzSubProperty)tooltipProperty[i.ToString()];
                        if (toolTipItem == null)
                            break;

                        int x1 = toolTipItem["x1"].ReadValue();
                        int x2 = toolTipItem["x2"].ReadValue();
                        int y1 = toolTipItem["y1"].ReadValue();
                        int y2 = toolTipItem["y2"].ReadValue();

                        // Check String.wz
                        WzSubProperty wzToolTipForI = (WzSubProperty)wzToolTip[i.ToString()];
                        if (wzToolTipForI == null)
                        {
                            errorList.Add("Map tooltip is missing. Path: " + stringTooltipPath + "/" + i);
                        }
                        string title = wzToolTipForI["Title"].ReadString(null);
                        string desc = wzToolTipForI["Desc"].ReadString(null);

                        if (title == null)
                        {
                            errorList.Add("Map tooltip is missing. Path: " + stringTooltipPath + "/" + i + "/Title");
                        }
                        toolTipBuf.DrawString(string.Format("{0}\n{1}", title, desc == null ? string.Empty : desc), FONT_GAME_TOOLTIP, new SolidBrush(Color.Black), new PointF(x1 + center.X, y1 + center.Y));
                    }
                }
                toolTip.Save("Renders\\" + mapIdName + "\\" + mapIdName + "_tooltip.bmp");
            }

            // Render Tiles
            Bitmap tileRender = new Bitmap(bmpSize.Width, bmpSize.Height);
            using (Graphics tileBuf = Graphics.FromImage(tileRender))
            {
                for (int i = 0; i < 7; i++)
                {
                    // The below code was commented out because it was creating problems when loading certain maps. When debugging it would throw an exception at line 469.
                    // Objects first
                    WzSubProperty iProperty = (WzSubProperty)img[i.ToString()];
                    WzSubProperty objProperties = ((WzSubProperty)iProperty["obj"]);
                    WzSubProperty infoProperties = ((WzSubProperty)iProperty["info"]);
                    WzSubProperty tileProperties = ((WzSubProperty)iProperty["tile"]);

                    if (objProperties.WzProperties.Count > 0)
                    {
                        foreach (WzSubProperty obj in objProperties.WzProperties)
                        {
                            //WzSubProperty obj = (WzSubProperty)oe.ExtendedProperty;
                            string imgName = ((WzStringProperty)obj["oS"]).Value + ".img";
                            string l0 = ((WzStringProperty)obj["l0"]).Value;
                            string l1 = ((WzStringProperty)obj["l1"]).Value;
                            string l2 = ((WzStringProperty)obj["l2"]).Value;
                            int x = ((WzIntProperty)obj["x"]).Value + center.X;
                            int y = ((WzIntProperty)obj["y"]).Value + center.Y;

                            PointF origin;
                            WzCanvasProperty png;

                            string imgObjPath = string.Format("{0}/Obj/{1}/{2}/{3}/{4}/0", wzFile.WzDirectory.Name, imgName, l0, l1, l2);

                            WzImageProperty objData = (WzImageProperty)WzFile.GetObjectFromMultipleWzFilePath(imgObjPath, Program.WzMan.WzFileListReadOnly);
                            tryagain:
                            if (objData is WzCanvasProperty)
                            {
                                png = ((WzCanvasProperty)objData);
                                origin = ((WzCanvasProperty)objData).GetCanvasVectorPosition();
                            }
                            else if (objData is WzUOLProperty)
                            {
                                WzObject currProp = objData.Parent;
                                foreach (string directive in ((WzUOLProperty)objData).Value.Split("/".ToCharArray()))
                                {
                                    if (directive == "..")
                                        currProp = currProp.Parent;
                                    else
                                    {
                                        if (currProp.GetType() == typeof(WzSubProperty))
                                        {
                                            currProp = ((WzSubProperty)currProp)[directive];
                                        }
                                        else if (currProp.GetType() == typeof(WzCanvasProperty))
                                        {
                                            currProp = ((WzCanvasProperty)currProp)[directive];
                                        }
                                        else if (currProp.GetType() == typeof(WzImage))
                                        {
                                            currProp = ((WzImage)currProp)[directive];
                                        }
                                        else if (currProp.GetType() == typeof(WzConvexProperty))
                                        {
                                            currProp = ((WzConvexProperty)currProp)[directive];
                                        }
                                        else
                                        {
                                            errorList.Add("UOL error at map renderer");
                                            return false;
                                        }
                                    }
                                }
                                objData = (WzImageProperty)currProp;
                                goto tryagain;
                            }
                            else
                            {
                                errorList.Add("Unknown Wz type at map renderer");
                                return false;
                            }

                            //WzVectorProperty origin = (WzVectorProperty)wzFile.GetObjectFromPath(wzFile.WzDirectory.Name + "/Obj/" + imgName + "/" + l0 + "/" + l1 + "/" + l2 + "/0");
                            //WzPngProperty png = (WzPngProperty)wzFile.GetObjectFromPath(wzFile.WzDirectory.Name + "/Obj/" + imgName + "/" + l0 + "/" + l1 + "/" + l2 + "/0/PNG");
                            tileBuf.DrawImage(png.GetLinkedWzCanvasBitmap(), x - origin.X, y - origin.Y);
                        }
                    }
                    if (infoProperties.WzProperties.Count == 0)
                        continue;

                    if (tileProperties.WzProperties.Count == 0)
                        continue;

                    // Ok, we have some tiles and a tileset
                    string tileSetName = ((WzStringProperty)infoProperties["tS"]).Value;

                    // Browse to the tileset
                    string tilePath = wzFile.WzDirectory.Name + "/Tile/" + tileSetName + ".img";
                    WzImage tileSet = (WzImage)WzFile.GetObjectFromMultipleWzFilePath(tilePath, Program.WzMan.WzFileListReadOnly);
                    if (!tileSet.Parsed)
                        tileSet.ParseImage();

                    foreach (WzSubProperty tile in tileProperties.WzProperties)
                    {
                        //WzSubProperty tile = (WzSubProperty)te.ExtendedProperty;

                        int x = ((WzIntProperty)tile["x"]).Value + center.X;
                        int y = ((WzIntProperty)tile["y"]).Value + center.Y;
                        string tilePackName = ((WzStringProperty)tile["u"]).Value;
                        string tileID = ((WzIntProperty)tile["no"]).Value.ToString();

                        WzSubProperty tilePack = ((WzSubProperty)tileSet[tilePackName]);
                        WzCanvasProperty tileCanvas = (WzCanvasProperty)tilePack[tileID];
                        if (tileCanvas == null)
                        {
                            errorList.Add(string.Format("Tile {0}, ID: {1} is not found.", tilePackName, tileID));
                        }
                        PointF tileVector = tileCanvas.GetCanvasVectorPosition();
                        tileBuf.DrawImage(tileCanvas.GetBitmap(), x - tileVector.X, y - tileVector.Y);
                    }
                }
            }
            tileRender.Save("Renders\\" + mapIdName + "\\" + mapIdName + "_tileRender.bmp");

            // Render nodeInfo
            Bitmap nodeInfoRender = null;
            WzSubProperty nodeInfoProperty = (WzSubProperty)img["nodeInfo"];
            if (nodeInfoProperty != null)
            {
                nodeInfoRender = new Bitmap(bmpSize.Width, bmpSize.Height);
                using (Graphics nodeInfoBuffer = Graphics.FromImage(nodeInfoRender))
                {
                    int start = 0;
                    int end = 0;

                    foreach (WzImageProperty nodeInfoImg in nodeInfoProperty.WzProperties)
                    {
                        switch (nodeInfoImg.Name)
                        {
                            case "edgeInfo":
                                {
                                    break;
                                }
                            case "end":
                                {
                                    end = ((WzIntProperty)nodeInfoImg).ReadValue();
                                    break;
                                }
                            case "start":
                                {
                                    start = ((WzIntProperty)nodeInfoImg).ReadValue();
                                    break;
                                }
                            default:
                                {
                                    int nodeInfoImgFileName = -1;
                                    if (int.TryParse(nodeInfoImg.Name, out nodeInfoImgFileName))
                                    {
                                        int attr = ((WzIntProperty)nodeInfoImg["attr"]).ReadValue();
                                        int key = ((WzIntProperty)nodeInfoImg["key"]).ReadValue();
                                        int x = ((WzIntProperty)nodeInfoImg["x"]).ReadValue() + center.X;
                                        int y = ((WzIntProperty)nodeInfoImg["y"]).ReadValue() + center.Y;

                                        List<int> edges = new List<int>();
                                        foreach (WzImageProperty edge in nodeInfoImg["edge"].WzProperties)
                                        {
                                            edges.Add(edge.ReadValue());
                                        }

                                        const int width = 200;
                                        const int height = 20;

                                        nodeInfoBuffer.FillRectangle(new SolidBrush(Color.Wheat), x, y, width, height);
                                        nodeInfoBuffer.DrawRectangle(new Pen(Color.Black, 1F), x, y, width, height);
                                        nodeInfoBuffer.DrawString(
                                            string.Format("Key: {0}, x: {1}, y: {1}", key, x, y),
                                            FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD, new SolidBrush(Color.Black), new PointF(x + (width / 2) - 8, y + (height / 2) - 7.7F));
                                    }
                                    break;
                                }
                        }
                    }
                }
                nodeInfoRender.Save("Renders\\" + mapIdName + "\\" + mapIdName + "_nodeInfoRender.bmp");
            }


            // Render everything combined
            Bitmap fullBmp = new Bitmap(bmpSize.Width, bmpSize.Height + 10);
            using (Graphics fullBuf = Graphics.FromImage(fullBmp))
            {
                fullBuf.FillRectangle(new SolidBrush(Color.CornflowerBlue), 0, 0, bmpSize.Width, bmpSize.Height + 10);
                fullBuf.DrawImage(backgroundRender, 0, 0);
                fullBuf.DrawImage(tileRender, 0, 0);
                fullBuf.DrawImage(mapRender, 0, 0);
                if (toolTip != null)
                {
                    fullBuf.DrawImage(toolTip, 0, 0);
                }
                if (nodeInfoRender != null)
                {
                    fullBuf.DrawImage(nodeInfoRender, 0, 0);
                }
                fullBuf.DrawImage(minimapRender, 0, 0);
            }
            //pbx_Foothold_Render.Image = fullBmp;
            fullBmp.Save("Renders\\" + mapIdName + "\\" + mapIdName + "_fullRender.bmp");

            // Cleanup resources
            backgroundRender.Dispose();
            tileRender.Dispose();
            mapRender.Dispose();
            toolTip?.Dispose();
            minimapRender.Dispose();

            if (errorList.Count() > 0)
                return false;

            // Display render map
            DisplayMap showMap = new DisplayMap();
            showMap.map = fullBmp;
            showMap.Footholds = FHs;
            showMap.thePortals = Ps;
            showMap.settings = settings;
            showMap.MobSpawnPoints = MSPs;
            showMap.FormClosed += new FormClosedEventHandler(DisplayMapClosed);
            try
            {
                showMap.scale = zoom;
                showMap.Show();
                return true;
            }
            catch (FormatException)
            {
                MessageBox.Show("You must set the render scale to a valid number.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        public int GetPseudoRandomColor(string x)
        {
            MD5 md5ctx = MD5.Create();
            byte[] md5 = md5ctx.ComputeHash(Encoding.ASCII.GetBytes(x));
            return BitConverter.ToInt32(md5, 0) & 0xFFFFFF;
        }

        private void DisplayMapClosed(object sender, FormClosedEventArgs e)
        {
            ((WzNode)node).Reparse();
        }

        internal void ParseSettings()
        {
            //Clear current settings
            settings.Clear();
            try
            {
                // Add the new ones
                string theSettings;
                if (!File.Exists(SettingsPath))
                    File.WriteAllText(SettingsPath, "!TAB1-!DPt:0!DPc:False!DNt:0!DNc:True!DFt:-230!DFc:False!\r\n!TAB2-!DXt:100!DXc:False!DYt:100!DYc:False!DTt:2!DTc:False!\r\n!TAB3-!DFPt:C:\\NEXON\\MapleStory\\Map.wz!DFPc:False!DSt:1!DSc:True!");
                using (TextReader settingsFile = new StreamReader(SettingsPath))
                    theSettings = settingsFile.ReadToEnd();
                settings.Add(Regex.Match(theSettings, @"(?<=!DPt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DPc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(theSettings, @"(?<=!DNt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DNc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(theSettings, @"(?<=!DFt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DFc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(theSettings, @"(?<=!DXt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DXc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(theSettings, @"(?<=!DYt:)-?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DYc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(theSettings, @"(?<=!DTt:)\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DTc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(theSettings, @"(?<=!DFPt:)C:(%\w+)+.wz(?=!)").Value.Replace('%', '/'));
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DFPc:)\w+(?=!)").Value));
                settings.Add(Regex.Match(theSettings, @"(?<=!DSt:)\d*,?\d*(?=!)").Value);
                settings.Add(bool.Parse(Regex.Match(theSettings, @"(?<=!DSc:)\w+(?=!)").Value));
            }
            catch { MessageBox.Show("Failed to load settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            foreach (Form form in Application.OpenForms)
            {
                DisplayMap mapForm;
                if (form.Name == "DisplayMap")// If the Map window is open, update its settings
                {
                    mapForm = (DisplayMap)form;
                    mapForm.settings = settings;
                }
            }
        }
    }
}
