/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Diagnostics;
using System.Drawing;

namespace HaSharedLibrary.Wz
{
    public static class WzInfoTools
    {
        public static System.Drawing.Point PointFToSystemPoint(PointF source)
        {
            return new System.Drawing.Point((int)source.X, (int)source.Y);
        }

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

        /// <summary>
        /// Add leading zeros to the source string. (pad left)
        /// i.e 550  = 0000550
        /// </summary>
        /// <param name="source"></param>
        /// <param name="maxLength"></param>
        /// <returns></returns>
        public static string AddLeadingZeros(string source, int maxLength)
        {
            return source.PadLeft(maxLength, '0');
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

        public static string GetNpcNameById(string id, WzFileManager fileManager)
        {
            id = RemoveLeadingZeros(id);

            WzImage stringWzDirs = (WzImage)fileManager.FindWzImageByName("string", "Npc.img");
            if (stringWzDirs != null)
            {
                WzObject npcObj = stringWzDirs[id];
                WzStringProperty npcName = (WzStringProperty)npcObj["name"];
                if (npcName == null)
                    return "";

                return npcName.Value;
            }
            return "";
        }

        public static WzSubProperty GetMapStringProp(string id, WzFileManager fileManager)
        {
            id = RemoveLeadingZeros(id);

            WzImage mapImg = (WzImage)fileManager.FindWzImageByName("string", "Map.img");
            if (mapImg != null)
            {
                foreach (WzSubProperty mapNameCategory in mapImg.WzProperties)
                {
                    WzSubProperty mapNameDirectory = (WzSubProperty)mapNameCategory[id];
                    if (mapNameDirectory != null)
                    {
                        return mapNameDirectory;
                    }
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
                if (directive == "..") 
                    currentObject = currentObject.Parent;
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
            WzObject wzObjectInCurrentWz = (WzObject)GetObjectByRelativePath(uol.Parent, uol.Value);
                
            return wzObjectInCurrentWz;
        }

        public static string RemoveExtension(string source)
        {
            if (source.Substring(source.Length - 4) == ".img")
                return source.Substring(0, source.Length - 4);
            return source;
        }

        public static WzImageProperty GetRealProperty(WzImageProperty prop)
        {
            if (prop is WzUOLProperty) 
                return (WzImageProperty)ResolveUOL((WzUOLProperty)prop);
            else 
                return prop;
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

        /// <summary>
        /// Finds a map image from the list of Map.wzs
        /// On pre-bb client (BETA)
        /// Data.wz/Map/Map/Map1/10000000.img
        /// 
        /// On pre 64-bit client:
        /// Map.wz/Map/Map1/10000000.img
        /// 
        /// On post 64-bit client:
        /// Map/Map/Map1/Map1_000.wz/10000000.img
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static WzImage FindMapImage(string mapid, WzFileManager fileManager)
        {
            WzObject mapParent = FindMapDirectoryParent(mapid, fileManager);
            if (mapParent == null) 
                return null;

            string mapIdNamePadded = AddLeadingZeros(mapid, 9) + ".img";

            if (fileManager.Is64Bit) { // is WzFile
                return (WzImage)mapParent?[mapIdNamePadded];
            }
            // is WzDirectory
            return (WzImage)mapParent?[mapIdNamePadded];
        }

        /// <summary>
        /// Finds a map directory from the list of Map.wzs
        /// On pre-bb client (BETA)
        /// Data.wz/Map/Map/Map1/10000000.img (WzDirectory)
        /// 
        /// On pre 64-bit client:
        /// Map.wz/Map/Map1/10000000.img (WzDirectory)
        /// 
        /// On post 64-bit client:
        /// Map/Map/Map1/Map1_000.wz/10000000.img (WzFile)
        /// </summary>
        /// <param name="mapid"></param>
        /// <returns></returns>
        public static WzDirectory FindMapDirectoryParent(string mapid, WzFileManager fileManager) {
            string mapIdNamePadded = AddLeadingZeros(mapid, 9) + ".img";
            string mapcat = fileManager.Is64Bit ? mapIdNamePadded.Substring(0, 1) : "Map" + mapIdNamePadded.Substring(0, 1);
            string baseDir = fileManager.Is64Bit ? "map\\map\\map" + mapcat : "map";

            WzObject mapObjectWzDir = fileManager.FindWzImageByName(baseDir, fileManager.Is64Bit ? string.Empty : "Map");

            if (fileManager.Is64Bit) {
                //Debug.WriteLine("Init map: {0}\\{1}", baseDir, mapIdNamePadded);
                return (WzDirectory)mapObjectWzDir;
            }
            else {
                WzDirectory mapImage = (WzDirectory)mapObjectWzDir?[mapcat];
                return mapImage;
            }
        }

        public static Color XNAToDrawingColor(Microsoft.Xna.Framework.Color c)
        {
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
    }
}
