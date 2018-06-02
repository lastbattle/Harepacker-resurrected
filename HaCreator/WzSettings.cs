/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System.IO;
using System.Reflection;
using XNA = Microsoft.Xna.Framework;
using HaCreator.MapEditor;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using System.Drawing;

namespace HaCreator
{
    public static class UserSettings
    {
        public static bool ShowErrorsMessage = true;
        public static bool XGAResolution = false;
        public static bool ClipText = false;
        public static Color TabColor = Color.LightSteelBlue;
        public static int LineWidth = 1;
        public static int DotWidth = 3;
        public static XNA.Color SelectSquare = new XNA.Color(0, 0, 255, 255);
        public static XNA.Color SelectSquareFill = new XNA.Color(0, 0, 200, 200);
        public static XNA.Color SelectedColor = new XNA.Color(0, 0, 255, 250);
        public static XNA.Color VRColor = new XNA.Color(0, 0, 255, 255);
        public static XNA.Color FootholdColor = XNA.Color.Red;
        public static XNA.Color RopeColor = XNA.Color.Green;
        public static XNA.Color ChairColor = XNA.Color.Orange;
        public static XNA.Color ToolTipColor = XNA.Color.SkyBlue;
        public static XNA.Color ToolTipFill = new XNA.Color(0, 0, 255, 100);
        public static XNA.Color ToolTipSelectedFill = new XNA.Color(0, 0, 255, 150);
        public static XNA.Color ToolTipCharFill = new XNA.Color(0, 255, 0, 100);
        public static XNA.Color ToolTipCharSelectedFill = new XNA.Color(0, 255, 0, 150);
        public static XNA.Color ToolTipBindingLine = XNA.Color.Magenta;
        public static XNA.Color MiscColor = XNA.Color.Brown;
        public static XNA.Color MiscFill = new XNA.Color(150, 75, 0, 100);
        public static XNA.Color MiscSelectedFill = new XNA.Color(150, 75, 0, 150);
        public static XNA.Color OriginColor = XNA.Color.LightGreen;
        public static XNA.Color MinimapBoundColor = XNA.Color.DarkOrange;
        public static int NonActiveAlpha = 63;
        public static int Mobrx0Offset = 200;
        public static int Mobrx1Offset = 200;
        public static int Npcrx0Offset = 20;
        public static int Npcrx1Offset = 20;
        public static int defaultMobTime = 0;
        public static int defaultReactorTime = 0;
        public static float SnapDistance = 10;
        public static float SignificantDistance = 10;
        public static int ScrollDistance = 90;
        public static double ScrollFactor = 1;
        public static double ScrollBase = 1.05;
        public static double ScrollExponentFactor = 1;
        public static int zShift = 1;
        public static int HiddenLifeR = 127;
        public static string FontName = "Arial";
        public static int FontSize = 15;
        public static System.Drawing.FontStyle FontStyle = System.Drawing.FontStyle.Regular;
        public static int dotDescriptionBoxSize = 100;
        public static int ImageViewerHeight = 100;
        public static int ImageViewerWidth = 100;

        public static bool useMiniMap = true;
        public static bool useSnapping = true;
        public static bool emulateParallax = true;
        public static bool suppressWarnings = false;
        public static bool FixFootholdMispositions = true;
        public static bool InverseUpDown = true;
        public static bool BackupEnabled = true;
        public static int BackupIdleTime = 5000;
        public static int BackupMaxTime = 60000;

        // Controls debug features such feature compatibility testing and special exception handling
        public static bool enableDebug =
#if DEBUG
            true;
#else
            false;
#endif
    }

    public static class ApplicationSettings
    {
        public static ItemTypes theoreticalVisibleTypes = ItemTypes.All; // These two are marked theoretical because the visible\edited types in effect (Board.VisibleTypes\EditedTypes)
        public static ItemTypes theoreticalEditedTypes = ItemTypes.All ^ ItemTypes.Backgrounds; // are subject to the current mode of operation
        public static int MapleVersionIndex = 3;
        public static string MapleFolder = "";
        public static int MapleFolderIndex = 0;
        public static System.Drawing.Size LastMapSize = new System.Drawing.Size(800, 800);
        public static int lastRadioIndex = 3;
        public static bool randomTiles = true;
        public static bool InfoMode = false;
        public static int lastDefaultLayer = 0;
        public static bool lastAllLayers = true;
        public static string LastHamPath = "";
        public static string LastXmlPath = "";
        public static string AuthorEmail = new string(Encoding.ASCII.GetChars(new byte[] { 
                                            0x68,0x61,0x68,0x61,0x30,0x31,0x68,0x61,0x68,0x61,0x30,0x31,0x40,0x67,0x6d,0x61,0x69,0x6c,0x2e,0x63,0x6f,0x6d 
                                            })); // The email address is obfuscated to prevent spambots from finding it on the git webpage
    }
}
