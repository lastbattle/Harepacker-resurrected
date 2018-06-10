using Footholds;
using HaRepackerLib;
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
using HaRepackerLib.Controls;
using System.Security.Cryptography;
using HaRepackerLib.Controls.HaRepackerMainPanels;
using System.Diagnostics;

namespace HaRepacker.FHMapper
{
    public class FHMapper
    {
        public static string SettingsPath = Path.Combine(Program.GetLocalFolderPath(), "Settings.ini");
        public List<Object> settings = new List<object>();
        private HaRepackerMainPanel MainPanel;
        private TreeNode node;

        // Fonts
        private static Font FONT_DISPLAY_MAPID = new Font("Segoe UI", 20);
        private static Font FONT_DISPLAY_MINIMAP_NOT_AVAILABLE = new Font("Segoe UI", 18);
        private static Font FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD = new Font("Segoe UI", 8);

        public FHMapper(HaRepackerMainPanel MainPanel)
        {
            this.MainPanel = MainPanel;
        }

        public void SaveMap(WzImage img, double zoom)
        {
            node = MainPanel.DataTree.SelectedNode;
            WzFile wzFile = (WzFile)((WzObject)node.Tag).WzFileParent;
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
                        return;
                    }
                }
                else
                    return;
            }

            Bitmap mapRender = new Bitmap(bmpSize.Width, bmpSize.Height + 10);
            using (Graphics drawBuf = Graphics.FromImage(mapRender))
            {
                //drawBuf.FillRectangle(new SolidBrush(Color.CornflowerBlue), 0, 0, bmpSize.Width, bmpSize.Height);
                drawBuf.DrawString("Map " + img.Name.Substring(0, img.Name.Length - 4), FONT_DISPLAY_MAPID, new SolidBrush(Color.Black), new PointF(10, 10));

                if (miniMapSubProperty != null)
                    drawBuf.DrawImage(((WzCanvasProperty)miniMapSubProperty["canvas"]).PngProperty.GetPNG(false), 10, 45);
                else
                {
                    drawBuf.DrawString("Minimap not availible", FONT_DISPLAY_MINIMAP_NOT_AVAILABLE, new SolidBrush(Color.Black), new PointF(10, 45));
                }

                WzSubProperty ps = (WzSubProperty)img["portal"];
                foreach (WzSubProperty p in ps.WzProperties)
                {
                    //WzSubProperty p = (WzSubProperty)p10.ExtendedProperty;
                    int x = ((WzIntProperty)p["x"]).Value + center.X;
                    int y = ((WzIntProperty)p["y"]).Value + center.Y;
                    int type = ((WzIntProperty)p["pt"]).Value;
                    Color pColor = Color.Red;
                    if (type == 0)
                        pColor = Color.Orange;
                    else if (type == 2 || type == 7)//Normal
                        pColor = Color.Blue;
                    else if (type == 3)//Auto-enter
                        pColor = Color.Magenta;
                    else if (type == 1 || type == 8)
                        pColor = Color.BlueViolet;
                    else
                        pColor = Color.IndianRed;
                    drawBuf.FillRectangle(new SolidBrush(Color.FromArgb(95, pColor.R, pColor.G, pColor.B)), x - 20, y - 20, 40, 40);
                    drawBuf.DrawRectangle(new Pen(Color.Black, 1F), x - 20, y - 20, 40, 40);
                    drawBuf.DrawString(p.Name, FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD, new SolidBrush(Color.Red), x - 8, y - 7.7F);
                    Portals.Portal portal = new Portals.Portal();
                    portal.Shape = new Rectangle(x - 20, y - 20, 40, 40);
                    portal.Data = p;
                    Ps.Add(portal);
                }
                try
                {
                    WzSubProperty SPs = (WzSubProperty)img["life"];
                    foreach (WzSubProperty sp in SPs.WzProperties)
                    {
                        Color MSPColor = Color.ForestGreen;

                        switch (((WzStringProperty)sp["type"]).Value)
                        {
                            case "m": // monster
                                {
                                    int monsterId = int.Parse(((WzStringProperty)sp["id"]).GetString());

                                    int x = ((WzIntProperty)sp["x"]).Value + center.X;
                                    int y = ((WzIntProperty)sp["y"]).Value + center.Y;
                                    int x_text = x - 15;
                                    int y_text = y - 15;

                                    SpawnPoint.Spawnpoint MSP = new SpawnPoint.Spawnpoint();
                                    MSP.Shape = new Rectangle(x_text, y_text, 30, 30);
                                    MSP.Data = sp;
                                    MSPs.Add(MSP);


                                    // Render monster image
                                    string monsterStrId = monsterId < 1000000 ? ("0" + monsterId) : monsterId.ToString();

                                    WzStringProperty linkInfo = (WzStringProperty)WzFile.GetObjectFromMultipleWzFilePath(string.Format("Mob.wz/{0}.img/info/link", monsterStrId), Program.WzMan.WzFileListReadOnly);
                                    if (linkInfo != null)
                                    {
                                        monsterId = int.Parse(linkInfo.GetString());
                                        monsterStrId = monsterId < 1000000 ? ("0" + monsterId) : monsterId.ToString();
                                    }
                                    WzCanvasProperty mobImage = (WzCanvasProperty)WzFile.GetObjectFromMultipleWzFilePath(string.Format("Mob.wz/{0}.img/stand/0", monsterStrId), Program.WzMan.WzFileListReadOnly);
                                    if (mobImage != null)
                                    {
                                        WzVectorProperty originXY = (WzVectorProperty)mobImage["origin"];
                                        PointF renderXY;
                                        if (originXY != null)
                                            renderXY = new PointF(x - originXY.Pos.X, y - originXY.Pos.Y);
                                        else
                                            renderXY = new PointF(x, y);

                                        WzImageProperty linkedCanvas = mobImage.GetLinkedWzCanvasProperty();
                                        if (linkedCanvas != null)
                                            drawBuf.DrawImage(linkedCanvas.GetBitmap(), renderXY);
                                        else
                                            drawBuf.DrawImage(mobImage.GetBitmap(), renderXY);
                                    }
                                    else
                                    {
                                        drawBuf.FillRectangle(new SolidBrush(Color.FromArgb(95, MSPColor.R, MSPColor.G, MSPColor.B)), x_text, y_text, 30, 30);
                                        drawBuf.DrawRectangle(new Pen(Color.Black, 1F), x_text, y_text, 30, 30);
                                    }
                                    // Get monster name
                                    WzStringProperty stringName = (WzStringProperty)WzFile.GetObjectFromMultipleWzFilePath(string.Format("String.wz/Mob.img/{0}/name", monsterId), Program.WzMan.WzFileListReadOnly);

                                    drawBuf.DrawString(string.Format("SP: {0}, Name: {1}, ID: {2}", sp.Name, stringName != null ? stringName.GetString() : string.Empty, monsterId), FONT_DISPLAY_PORTAL_LFIE_FOOTHOLD, new SolidBrush(Color.Red), x_text + 7, y_text + 7.3F);
                                    break;
                                }
                            case "n": // NPC
                                {
                                    break;
                                }
                        }
                    }
                }
                catch (Exception exp)
                {
                    Debug.WriteLine(exp.ToString());
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
            mapRender.Save("Renders\\" + img.Name.Substring(0, img.Name.Length - 4) + "\\" + img.Name.Substring(0, img.Name.Length - 4) + "_footholdRender.bmp");

            Bitmap tileRender = new Bitmap(bmpSize.Width, bmpSize.Height);

            using (Graphics tileBuf = Graphics.FromImage(tileRender))
            {
                for (int i = 0; i < 7; i++)
                {
                    // The below code was commented out because it was creating problems when loading certain maps. When debugging it would throw an exception at line 469.
                    // Objects first
                    if (((WzSubProperty)((WzSubProperty)img[i.ToString()])["obj"]).WzProperties.Count > 0)
                    {
                        foreach (WzSubProperty obj in ((WzSubProperty)((WzSubProperty)img[i.ToString()])["obj"]).WzProperties)
                        {
                            //WzSubProperty obj = (WzSubProperty)oe.ExtendedProperty;
                            string imgName = ((WzStringProperty)obj["oS"]).Value + ".img";
                            string l0 = ((WzStringProperty)obj["l0"]).Value;
                            string l1 = ((WzStringProperty)obj["l1"]).Value;
                            string l2 = ((WzStringProperty)obj["l2"]).Value;
                            int x = ((WzIntProperty)obj["x"]).Value + center.X;
                            int y = ((WzIntProperty)obj["y"]).Value + center.Y;
                            WzVectorProperty origin;
                            WzPngProperty png;

                            string imgObjPath = wzFile.WzDirectory.Name + "/Obj/" + imgName + "/" + l0 + "/" + l1 + "/" + l2 + "/0";

                            WzImageProperty objData = (WzImageProperty)WzFile.GetObjectFromMultipleWzFilePath(imgObjPath, Program.WzMan.WzFileListReadOnly);
                            tryagain:
                            if (objData is WzCanvasProperty)
                            {
                                png = ((WzCanvasProperty)objData).PngProperty;
                                origin = (WzVectorProperty)((WzCanvasProperty)objData)["origin"];
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
                                            throw new Exception("UOL error at map renderer");
                                        }
                                    }
                                }
                                objData = (WzImageProperty)currProp;
                                goto tryagain;
                            }
                            else throw new Exception("unknown type at map renderer");
                            //WzVectorProperty origin = (WzVectorProperty)wzFile.GetObjectFromPath(wzFile.WzDirectory.Name + "/Obj/" + imgName + "/" + l0 + "/" + l1 + "/" + l2 + "/0");
                            //WzPngProperty png = (WzPngProperty)wzFile.GetObjectFromPath(wzFile.WzDirectory.Name + "/Obj/" + imgName + "/" + l0 + "/" + l1 + "/" + l2 + "/0/PNG");
                            tileBuf.DrawImage(png.GetPNG(false), x - origin.X.Value, y - origin.Y.Value);
                        }
                    }
                    if (((WzSubProperty)((WzSubProperty)img[i.ToString()])["info"]).WzProperties.Count == 0)
                        continue;

                    if (((WzSubProperty)((WzSubProperty)img[i.ToString()])["tile"]).WzProperties.Count == 0)
                        continue;

                    // Ok, we have some tiles and a tileset

                    string tileSetName = ((WzStringProperty)((WzSubProperty)((WzSubProperty)img[i.ToString()])["info"])["tS"]).Value;

                    // Browse to the tileset
                    string tilePath = wzFile.WzDirectory.Name + "/Tile/" + tileSetName + ".img";
                    WzImage tileSet = (WzImage)WzFile.GetObjectFromMultipleWzFilePath(tilePath, Program.WzMan.WzFileListReadOnly);
                    if (!tileSet.Parsed)
                        tileSet.ParseImage();

                    foreach (WzSubProperty tile in ((WzSubProperty)((WzSubProperty)img[i.ToString()])["tile"]).WzProperties)
                    {
                        //WzSubProperty tile = (WzSubProperty)te.ExtendedProperty;

                        int x = ((WzIntProperty)tile["x"]).Value + center.X;
                        int y = ((WzIntProperty)tile["y"]).Value + center.Y;
                        string tilePackName = ((WzStringProperty)tile["u"]).Value;
                        string tileID = ((WzIntProperty)tile["no"]).Value.ToString();
                        Point origin = new Point(((WzVectorProperty)((WzCanvasProperty)((WzSubProperty)tileSet[tilePackName])[tileID])["origin"]).X.Value, ((WzVectorProperty)((WzCanvasProperty)((WzSubProperty)tileSet[tilePackName])[tileID])["origin"]).Y.Value);

                        tileBuf.DrawImage(((WzCanvasProperty)((WzSubProperty)tileSet[tilePackName])[tileID]).PngProperty.GetPNG(false), x - origin.X, y - origin.Y);

                    }

                }

            }

            tileRender.Save("Renders\\" + img.Name.Substring(0, img.Name.Length - 4) + "\\" + img.Name.Substring(0, img.Name.Length - 4) + "_tileRender.bmp");

            Bitmap fullBmp = new Bitmap(bmpSize.Width, bmpSize.Height + 10);

            using (Graphics fullBuf = Graphics.FromImage(fullBmp))
            {
                fullBuf.FillRectangle(new SolidBrush(Color.CornflowerBlue), 0, 0, bmpSize.Width, bmpSize.Height + 10);
                fullBuf.DrawImage(tileRender, 0, 0);
                fullBuf.DrawImage(mapRender, 0, 0);

            }
            //pbx_Foothold_Render.Image = fullBmp;
            fullBmp.Save("Renders\\" + img.Name.Substring(0, img.Name.Length - 4) + "\\" + img.Name.Substring(0, img.Name.Length - 4) + "_fullRender.bmp");

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
            }
            catch (FormatException)
            {
                MessageBox.Show("You must set the render scale to a valid number.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
