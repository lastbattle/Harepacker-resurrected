/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
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
        public Dictionary<string, string> NPCs = new Dictionary<string, string>();
        public Dictionary<string, string> Mobs = new Dictionary<string, string>();
        public Dictionary<string, ReactorInfo> Reactors = new Dictionary<string, ReactorInfo>();
        public Dictionary<string, WzImage> TileSets = new Dictionary<string, WzImage>();
        public Dictionary<string, WzImage> ObjectSets = new Dictionary<string, WzImage>();
        public Dictionary<string, WzImage> BackgroundSets = new Dictionary<string, WzImage>();
        public Dictionary<string, WzSoundProperty> BGMs = new Dictionary<string, WzSoundProperty>();
        public Dictionary<string, Bitmap> MapMarks = new Dictionary<string, Bitmap>();
        public Dictionary<string, string> Maps = new Dictionary<string, string>();
        public Dictionary<string, PortalInfo> Portals = new Dictionary<string, PortalInfo>();
        public List<string> PortalTypeById = new List<string>();
        public Dictionary<string, int> PortalIdByType = new Dictionary<string,int>();
        public Dictionary<string, PortalGameImageInfo> GamePortals = new Dictionary<string, PortalGameImageInfo>();
    }
}
