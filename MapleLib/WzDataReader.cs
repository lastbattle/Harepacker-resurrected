using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib
{
    /// <summary>
    /// 
    /// </summary>
    public static class WzDataReader
    {
        /// <summary>
        /// Reads a String from WzImageProperty
        /// </summary>
        /// <param name="value"></param>
        /// <param name="fallback"></param>
        /// <returns></returns>
        public static string ReadString(this WzImageProperty value, string fallback)
        {
            if (value == null || value.GetType() == typeof(WzNullProperty))
                return fallback;

            if (value != null)
            {
                if (value.PropertyType == WzPropertyType.String)
                    return ((WzStringProperty)value).Value;
                else if (value.PropertyType == WzPropertyType.Int)
                    return ((WzIntProperty)value).Value.ToString();
            }
            return ((WzStringProperty)value).Value;
        }

        /// <summary>
        /// Reads a vector value from WzImageProperty
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static WzVectorProperty ReadVector(this WzImageProperty value)
        {
            if (value == null)
                return null;
            return ((WzVectorProperty)value);
        }

        /// <summary>
        /// Reads an int64 value from WzImageProperty
        /// </summary>
        /// <param name="value"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static long ReadLong(this WzImageProperty value, long def = 0)
        {
            if (value == null)
                return 0;
            else if (value.PropertyType == WzPropertyType.Int)
                return ((WzIntProperty)value).Value;
            else if (value.PropertyType == WzPropertyType.Long)
                return ((WzLongProperty)value).Value;
            else if (value.PropertyType == WzPropertyType.String)
                try
                {
                    return long.Parse(((WzStringProperty)value).Value);
                }
                catch
                {
                    Debug.WriteLine("Error parsing string to long: " + ((WzStringProperty)value).Value);
                    return def;
                }
            return 0;
        }

        public static int ReadValue(this WzSubProperty value)
        {
            return ReadValue((WzImageProperty)value);
        }


        /// <summary>
        /// Reads an int32 value from WzImageProperty
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int ReadValue(this WzImageProperty value)
        {
            if (value == null)
                return 0;
            else if (value.PropertyType == WzPropertyType.Int)
                return ((WzIntProperty)value).Value;
            else if (value.PropertyType == WzPropertyType.String)
                return int.Parse(((WzStringProperty)value).Value);
            return 0;
        }


        public static int ReadValue(this WzSubProperty value, int def)
        {
            return ReadValue((WzImageProperty)value, def);
        }

        /// <summary>
        /// Reads an int32 value from WzImageProperty
        /// </summary>
        /// <param name="value"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static int ReadValue(this WzImageProperty value, int def)
        {
            if (value == null)
            {
                return def;
            }
            else if (value.PropertyType == WzPropertyType.Int)
            {
                return ((WzIntProperty)value).Value;
            }
            else if (value.PropertyType == WzPropertyType.String)
            {
                string strdata = ((WzStringProperty)value).Value;
                try
                {
                    return int.Parse(strdata);
                }
                catch
                {
                    if (strdata.EndsWith("%"))
                    { // Stupid nexon, see <imgdir name="02040016">
                        // It have a scroll success rate of 10% instead of 10
                        return int.Parse(strdata.Substring(0, strdata.Length - 1));
                    }
                }
            }
            return def;
        }
    }
}
