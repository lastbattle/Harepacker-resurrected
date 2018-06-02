/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using HaCreator.MapEditor.Info;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.Wz
{
    public static class WzInfoTools
    {
        public static System.Drawing.Point VectorToSystemPoint(WzVectorProperty source)
        {
            return new System.Drawing.Point(source.X.Value, source.Y.Value);
        }

        public static Microsoft.Xna.Framework.Point VectorToXNAPoint(WzVectorProperty source)
        {
            return new Microsoft.Xna.Framework.Point(source.X.Value, source.Y.Value);
        }

        public static WzVectorProperty PointToVector(string name, System.Drawing.Point source)
        {
            return new WzVectorProperty(name, new WzIntProperty("X", source.X), new WzIntProperty("Y", source.Y));
        }

        public static WzVectorProperty PointToVector(string name, Microsoft.Xna.Framework.Point source)
        {
            return new WzVectorProperty(name, new WzIntProperty("X", source.X), new WzIntProperty("Y", source.Y));
        }

        public static string AddLeadingZeros(string source, int maxLength)
        {
            while (source.Length < maxLength)
                source = "0" + source;
            return source;
        }

        public static string RemoveLeadingZeros(string source)
        {
            int firstNonZeroIndex = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != (char)0x30) //char at index i is not 0
                {
                    firstNonZeroIndex = i;
                    break;
                }
                else if (i == source.Length - 1) //all chars are 0, return 0
                    return "0";
            }
            return source.Substring(firstNonZeroIndex);
        }

        public static string GetMobNameById(string id)
        {
            id = RemoveLeadingZeros(id);
            WzObject obj = Program.WzManager.String["Mob.img"][id];
            if (obj == null)
            {
                return "";
            }
            WzStringProperty mobName = (WzStringProperty)obj["name"];
            if (mobName == null)
            {
                return "";
            }
            return mobName.Value;
        }

        public static string GetNpcNameById(string id)
        {
            id = RemoveLeadingZeros(id);
            WzObject obj = Program.WzManager.String["Npc.img"][id];
            if (obj == null)
            {
                return "";
            }
            WzStringProperty npcName = (WzStringProperty)obj["name"];
            if (npcName == null)
            {
                return "";
            }
            return npcName.Value;
        }

        public static WzSubProperty GetMapStringProp(string id)
        {
            id = RemoveLeadingZeros(id);
            WzImage mapNameParent = (WzImage)Program.WzManager.String["Map.img"];
            foreach (WzSubProperty mapNameCategory in mapNameParent.WzProperties)
            {
                WzSubProperty mapNameDirectory = (WzSubProperty)mapNameCategory[id];
                if (mapNameDirectory != null)
                {
                    return mapNameDirectory;
                }
            }
            return null;
        }

        public static string GetMapName(WzSubProperty mapProp)
        {
            if (mapProp == null)
            {
                return "";
            }
            WzStringProperty mapName = (WzStringProperty)mapProp["mapName"];
            if (mapName == null)
            {
                return "";
            }
            return mapName.Value;
        }

        public static string GetMapStreetName(WzSubProperty mapProp)
        {
            if (mapProp == null)
            {
                return "";
            }
            WzStringProperty streetName = (WzStringProperty)mapProp["streetName"];
            if (streetName == null)
            {
                return "";
            }
            return streetName.Value;
        }

        public static string GetMapCategoryName(WzSubProperty mapProp)
        {
            if (mapProp == null)
            {
                return "";
            }
            return mapProp.Parent.Name;
        }

        public static WzObject GetObjectByRelativePath(WzObject currentObject, string path)
        {
            foreach (string directive in path.Split("/".ToCharArray()))
            {
                if (directive == "..") currentObject = currentObject.Parent;
                else if (currentObject is WzImageProperty)
                    currentObject = ((WzImageProperty)currentObject)[directive];
                else if (currentObject is WzImage)
                    currentObject = ((WzImage)currentObject)[directive];
                else if (currentObject is WzDirectory)
                    currentObject = ((WzDirectory)currentObject)[directive];
                else throw new Exception("invalid type");
            }
            return currentObject;
        }

        public static WzObject ResolveUOL(WzUOLProperty uol)
        {
            return (WzObject)GetObjectByRelativePath(uol.Parent, uol.Value);
        }

        public static string RemoveExtension(string source)
        {
            if (source.Substring(source.Length - 4) == ".img")
                return source.Substring(0, source.Length - 4);
            return source;
        }

        public static WzImageProperty GetRealProperty(WzImageProperty prop)
        {
            if (prop is WzUOLProperty) return (WzImageProperty)ResolveUOL((WzUOLProperty)prop);
            else return prop;
        }

        public static WzCanvasProperty GetMobImage(WzImage parentImage)
        {
            WzSubProperty standParent = (WzSubProperty)parentImage["stand"];
            if (standParent != null)
            {
                WzCanvasProperty frame1 = (WzCanvasProperty)GetRealProperty(standParent["0"]);
                if (frame1 != null) return frame1;
            }
            WzSubProperty flyParent = (WzSubProperty)parentImage["fly"];
            if (flyParent != null)
            {
                WzCanvasProperty frame1 = (WzCanvasProperty)GetRealProperty(flyParent["0"]);
                if (frame1 != null) return frame1;
            }
            return null;
        }

        public static WzCanvasProperty GetNpcImage(WzImage parentImage)
        {
            WzSubProperty standParent = (WzSubProperty)parentImage["stand"];
            if (standParent != null)
            {
                WzCanvasProperty frame1 = (WzCanvasProperty)GetRealProperty(standParent["0"]);
                if (frame1 != null) return frame1;
            }
            return null;
        }

        public static WzCanvasProperty GetReactorImage(WzImage parentImage)
        {
            WzSubProperty action0 = (WzSubProperty)parentImage["0"];
            if (action0 != null)
            {
                WzCanvasProperty frame1 = (WzCanvasProperty)GetRealProperty(action0["0"]);
                if (frame1 != null) return frame1;
            }
            return null;
        }

        public static MobInfo GetMobInfoById(string id)
        {
            id = AddLeadingZeros(id, 7);
            return MobInfo.Get(id);
        }

        public static NpcInfo GetNpcInfoById(string id)
        {
            id = AddLeadingZeros(id, 7);
            return NpcInfo.Get(id);
        }

        public static Color XNAToDrawingColor(XNA.Color c)
        {
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }
}
