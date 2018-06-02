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
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure;
using System.Drawing;
using MapleLib.WzLib.Serialization;

namespace HaRepackerLib
{
    public static class UserSettings
    {
        public static int Indentation = 0;
        public static LineBreak LineBreakType = LineBreak.None;
        public static string DefaultXmlFolder = "";
        public static bool UseApngIncompatibilityFrame = true;
        public static bool AutoAssociate = true;
        public static bool AutoUpdate = true;
        public static bool Sort = true;
        public static bool SuppressWarnings = false;
        public static bool ParseImagesInSearch = false;
        public static bool SearchStringValues = true;
    }

    public static class ApplicationSettings
    {
        public static bool Maximized = false;
        public static Size WindowSize = new Size(800, 600);
        public static bool FirstRun = true;
        public static string LastBrowserPath = "";
        public static WzMapleVersion MapleVersion = WzMapleVersion.BMS;
        public static string UpdateServer = "";
    }

    public static class Constants
    {
        public const int Version = 423;
    }
}
