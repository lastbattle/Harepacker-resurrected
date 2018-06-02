/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
//using HaCreator.MapEditor;
using MapleLib.WzLib.WzStructure;

namespace MapleLib.WzLib.WzStructure
{
    public static class InfoTool
    {
        public static string GetString(WzImageProperty source)
        {
            return source.GetString();
        }

        public static WzStringProperty SetString(string value)
        {
            return new WzStringProperty("", value);
        }

        public static string GetOptionalString(WzImageProperty source)
        {
            return source == null ? null : source.GetString();
        }

        public static WzStringProperty SetOptionalString(string value)
        {
            return value == null ? null : SetString(value);
        }

        public static double GetDouble(WzImageProperty source)
        {
            return source.GetDouble();
        }

        public static WzDoubleProperty SetDouble(double value)
        {
            return new WzDoubleProperty("", value);
        }

        public static int GetInt(WzImageProperty source)
        {
            return source.GetInt();
        }

        public static WzIntProperty SetInt(int value)
        {
            return new WzIntProperty("", value);
        }

        public static int? GetOptionalInt(WzImageProperty source)
        {
            return source == null ? (int?)null : source.GetInt();
        }

        public static WzIntProperty SetOptionalInt(int? value)
        {
            return value.HasValue ? SetInt(value.Value) : null;
        }

        public static bool GetBool(WzImageProperty source)
        {
            return source.GetInt() == 1;
        }

        public static WzIntProperty SetBool(bool value)
        {
            return new WzIntProperty("", value ? 1 : 0);
        }

        public static MapleBool GetOptionalBool(WzImageProperty source)
        {
            if (source == null) return MapleBool.NotExist;
            else return source.GetInt() == 1;
        }

        public static WzIntProperty SetOptionalBool(MapleBool value)
        {
            return value.HasValue ? SetBool(value.Value) : null;
        }

        public static float GetFloat(WzImageProperty source)
        {
            return source.GetFloat();
        }

        public static WzFloatProperty SetFloat(float value)
        {
            return new WzFloatProperty("", value);
        }

        public static float? GetOptionalFloat(WzImageProperty source)
        {
            return source == null ? (float?)null : source.GetFloat();
        }

        public static WzFloatProperty SetOptionalFloat(float? value)
        {
            return value.HasValue ? SetFloat(value.Value) : null;
        }

        public static int? GetOptionalTranslatedInt(WzImageProperty source)
        {
            string str = InfoTool.GetOptionalString(source);
            if (str == null) return null;
            return int.Parse(str);
        }

        public static WzStringProperty SetOptionalTranslatedInt(int? value)
        {
            if (value.HasValue)
            {
                return SetString(value.Value.ToString());
            }
            else
            {
                return null;
            }
        }
    }
}
