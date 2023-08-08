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
using MapleLib.WzLib.Util;
using MapleLib.WzLib.WzProperties;
using System.IO;
using System.Drawing.Imaging;
using System.Globalization;
using System.Xml;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json;

namespace MapleLib.WzLib.Serialization
{
    public abstract class ProgressingWzSerializer
    {
        protected int total = 0;
        protected int curr = 0;
        public int Total { get { return total; } }
        public int Current { get { return curr; } }

        protected static void CreateDirSafe(ref string path)
        {
            if (path.Substring(path.Length - 1, 1) == @"\")
                path = path.Substring(0, path.Length - 1);

            string basePath = path;
            int curridx = 0;
            while (Directory.Exists(path) || File.Exists(path))
            {
                curridx++;
                path = basePath + curridx;
            }
            Directory.CreateDirectory(path);
        }

        private readonly static string regexSearch = ":" + new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        private readonly static Regex regex_invalidPath = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
        /// <summary>
        /// Escapes invalid file name and paths (if nexon uses any illegal character that causes issue during saving)
        /// </summary>
        /// <param name="path"></param>
        public static string EscapeInvalidFilePathNames(string path)
        {
            return regex_invalidPath.Replace(path, "");
        }
    }

    public abstract class WzSerializer : ProgressingWzSerializer
    {
        protected string indent;
        protected string lineBreak;
        public static NumberFormatInfo formattingInfo;
        protected bool bExportBase64Data = false;

        protected static char[] amp = "&amp;".ToCharArray();
        protected static char[] lt = "&lt;".ToCharArray();
        protected static char[] gt = "&gt;".ToCharArray();
        protected static char[] apos = "&apos;".ToCharArray();
        protected static char[] quot = "&quot;".ToCharArray();

        static WzSerializer()
        {
            formattingInfo = new NumberFormatInfo
            {
                NumberDecimalSeparator = ".",
                NumberGroupSeparator = ","
            };
        }

        public WzSerializer(int indentation, LineBreak lineBreakType)
        {
            switch (lineBreakType)
            {
                case LineBreak.None:
                    lineBreak = "";
                    break;
                case LineBreak.Windows:
                    lineBreak = "\r\n";
                    break;
                case LineBreak.Unix:
                    lineBreak = "\n";
                    break;
            }
            indent = new string(' ', indentation);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tw"></param>
        /// <param name="depth"></param>
        /// <param name="prop"></param>
        /// <param name="exportFilePath"></param>
        protected void WritePropertyToXML(TextWriter tw, string depth, WzImageProperty prop, string exportFilePath)
        {
            if (prop is WzCanvasProperty)
            {
                WzCanvasProperty property3 = (WzCanvasProperty)prop;
                if (bExportBase64Data)
                {
                    MemoryStream stream = new MemoryStream();
                    property3.PngProperty.GetImage(false).Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] pngbytes = stream.ToArray();
                    stream.Close();
                    tw.Write(string.Concat(new object[] { depth, "<canvas name=\"", XmlUtil.SanitizeText(property3.Name), "\" width=\"", property3.PngProperty.Width, "\" height=\"", property3.PngProperty.Height, "\" basedata=\"", Convert.ToBase64String(pngbytes), "\">" }) + lineBreak);
                }
                else
                    tw.Write(string.Concat(new object[] { depth, "<canvas name=\"", XmlUtil.SanitizeText(property3.Name), "\" width=\"", property3.PngProperty.Width, "\" height=\"", property3.PngProperty.Height, "\">" }) + lineBreak);
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property3.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth + "</canvas>" + lineBreak);
            }
            else if (prop is WzIntProperty)
            {
                WzIntProperty property4 = (WzIntProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<int name=\"", XmlUtil.SanitizeText(property4.Name), "\" value=\"", property4.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzDoubleProperty)
            {
                WzDoubleProperty property5 = (WzDoubleProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<double name=\"", XmlUtil.SanitizeText(property5.Name), "\" value=\"", property5.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzNullProperty)
            {
                WzNullProperty property6 = (WzNullProperty)prop;
                tw.Write(depth + "<null name=\"" + XmlUtil.SanitizeText(property6.Name) + "\"/>" + lineBreak);
            }
            else if (prop is WzBinaryProperty)
            {
                WzBinaryProperty property7 = (WzBinaryProperty)prop;
                if (bExportBase64Data)
                    tw.Write(string.Concat(new object[] { depth, "<sound name=\"", XmlUtil.SanitizeText(property7.Name), "\" length=\"", property7.Length.ToString(), "\" basehead=\"", Convert.ToBase64String(property7.Header), "\" basedata=\"", Convert.ToBase64String(property7.GetBytes(false)), "\"/>" }) + lineBreak);
                else
                    tw.Write(depth + "<sound name=\"" + XmlUtil.SanitizeText(property7.Name) + "\"/>" + lineBreak);
            }
            else if (prop is WzStringProperty)
            {
                WzStringProperty property8 = (WzStringProperty)prop;
                string str = XmlUtil.SanitizeText(property8.Value);
                tw.Write(depth + "<string name=\"" + XmlUtil.SanitizeText(property8.Name) + "\" value=\"" + str + "\"/>" + lineBreak);
            }
            else if (prop is WzSubProperty)
            {
                WzSubProperty property9 = (WzSubProperty)prop;
                tw.Write(depth + "<imgdir name=\"" + XmlUtil.SanitizeText(property9.Name) + "\">" + lineBreak);
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property9.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth + "</imgdir>" + lineBreak);
            }
            else if (prop is WzShortProperty)
            {
                WzShortProperty property10 = (WzShortProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<short name=\"", XmlUtil.SanitizeText(property10.Name), "\" value=\"", property10.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzLongProperty)
            {
                WzLongProperty long_prop = (WzLongProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<long name=\"", XmlUtil.SanitizeText(long_prop.Name), "\" value=\"", long_prop.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzUOLProperty)
            {
                WzUOLProperty property11 = (WzUOLProperty)prop;
                tw.Write(depth + "<uol name=\"" + property11.Name + "\" value=\"" + XmlUtil.SanitizeText(property11.Value) + "\"/>" + lineBreak);
            }
            else if (prop is WzVectorProperty)
            {
                WzVectorProperty property12 = (WzVectorProperty)prop;
                tw.Write(string.Concat(new object[] { depth, "<vector name=\"", XmlUtil.SanitizeText(property12.Name), "\" x=\"", property12.X.Value, "\" y=\"", property12.Y.Value, "\"/>" }) + lineBreak);
            }
            else if (prop is WzFloatProperty)
            {
                WzFloatProperty property13 = (WzFloatProperty)prop;
                string str2 = Convert.ToString(property13.Value, formattingInfo);
                if (!str2.Contains("."))
                    str2 = str2 + ".0";
                tw.Write(depth + "<float name=\"" + XmlUtil.SanitizeText(property13.Name) + "\" value=\"" + str2 + "\"/>" + lineBreak);
            }
            else if (prop is WzConvexProperty)
            {
                tw.Write(depth + "<extended name=\"" + XmlUtil.SanitizeText(prop.Name) + "\">" + lineBreak);

                WzConvexProperty property14 = (WzConvexProperty)prop;
                string newDepth = depth + indent;
                foreach (WzImageProperty property in property14.WzProperties)
                {
                    WritePropertyToXML(tw, newDepth, property, exportFilePath);
                }
                tw.Write(depth + "</extended>" + lineBreak);
            }
            else if (prop is WzLuaProperty propertyLua)
            {
                string parentName = propertyLua.Parent.Name;

                tw.Write(depth);
                tw.Write(lineBreak);
                if (bExportBase64Data)
                {

                }
                // Export standalone file here
                using (TextWriter twLua = new StreamWriter(File.Create(exportFilePath.Replace(parentName + ".xml", parentName))))
                {
                    twLua.Write(propertyLua.ToString());
                }
            }
        }

        /// <summary>
        /// Writes WzImageProperty to Json or Bson
        /// </summary>
        /// <param name="json"></param>
        /// <param name="depth"></param>
        /// <param name="prop"></param>
        /// <param name="exportFilePath"></param>
        protected void WritePropertyToJsonBson(JObject json, string depth, WzImageProperty prop, string exportFilePath)
        {
            const string FIELD_TYPE_NAME = "_dirType"; // avoid the same naming as anything in the WZ to avoid exceptions
            //const string FIELD_DEPTH_NAME = "_depth";
            const string FIELD_NAME_NAME = "_dirName";

            const string FIELD_WIDTH_NAME = "_width";
            const string FIELD_HEIGHT_NAME = "_height";

            const string FIELD_X_NAME = "_x";
            const string FIELD_Y_NAME = "_y";

            const string FIELD_BASEDATA_NAME = "_image";

            const string FIELD_VALUE_NAME = "_value";

            const string FIELD_LENGTH_NAME = "_length";
            const string FIELD_FILENAME_NAME = "_fileName";

            if (prop is WzCanvasProperty propertyCanvas)
            {
                JObject jsonCanvas = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyCanvas.Name) },
                        { FIELD_TYPE_NAME, "canvas" },
                        { FIELD_WIDTH_NAME, propertyCanvas.PngProperty.Width },
                        { FIELD_HEIGHT_NAME, propertyCanvas.PngProperty.Height },
                    };
                if (bExportBase64Data)
                {
                    byte[] pngbytes;
                    using (MemoryStream stream = new MemoryStream())
                    {
                        propertyCanvas.PngProperty.GetImage(false)?.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        pngbytes = stream.ToArray();
                    }
                    jsonCanvas.Add(FIELD_BASEDATA_NAME, Convert.ToBase64String(pngbytes));
                }

                string jPropertyName = XmlUtil.SanitizeText(propertyCanvas.Name);
                if (!json.ContainsKey(jPropertyName))  // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                                                       // FullPath = "Item.wz\\Install\\0380.img\\03800572\\info\\icon\\foothold\\foothold" <<< double 'foothold' here :( 
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyCanvas.Name), jsonCanvas)); // add this json to the main json object parent

                    string newDepth = depth + indent;
                    foreach (WzImageProperty property in propertyCanvas.WzProperties)
                    {
                        WritePropertyToJsonBson(jsonCanvas, newDepth, property, exportFilePath);
                    }
                }
            }
            else if (prop is WzIntProperty propertyInt)
            {
                JObject jsonInt = new JObject
                {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyInt.Name) },
                        { FIELD_TYPE_NAME, "int" },
                        { FIELD_VALUE_NAME, propertyInt.Value },
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyInt.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyInt.Name), jsonInt)); // add this json to the main json object parent
                }
            }
            else if (prop is WzDoubleProperty propertyDouble)
            {
                JObject jsonDouble = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyDouble.Name) },
                         { FIELD_TYPE_NAME, "double" },
                        { FIELD_VALUE_NAME, propertyDouble.Value },
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyDouble.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyDouble.Name), jsonDouble)); // add this json to the main json object parent
                }
            }
            else if (prop is WzNullProperty propertyNull)
            {
                JObject jsonNull = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyNull.Name) },
                        { FIELD_TYPE_NAME, "null" },
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyNull.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyNull.Name), jsonNull)); // add this json to the main json object parent
                }
            }
            else if (prop is WzBinaryProperty propertyBin)
            {
                JObject jsonBinary = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyBin.Name) },
                        { FIELD_TYPE_NAME, "binary" },
                        { FIELD_LENGTH_NAME, propertyBin.Length.ToString() }
                    };
                if (bExportBase64Data)
                {
                    jsonBinary.Add("basehead", Convert.ToBase64String(propertyBin.Header));
                    jsonBinary.Add("basedata", Convert.ToBase64String(propertyBin.GetBytes(false)));
                }

                string jPropertyName = XmlUtil.SanitizeText(propertyBin.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyBin.Name), jsonBinary)); // add this json to the main json object parent
                }
            }
            else if (prop is WzStringProperty propertyStr)
            {
                string str = XmlUtil.SanitizeText(propertyStr.Value);

                JObject jsonString = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyStr.Name) },
                        { FIELD_TYPE_NAME, "string" },
                        { FIELD_VALUE_NAME, str }
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyStr.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyStr.Name), jsonString)); // add this json to the main json object parent
                }
            }
            else if (prop is WzSubProperty propertySub)
            {
                JObject jsonSub = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertySub.Name) },
                        { FIELD_TYPE_NAME, "sub" },
                    };

                string newDepth = depth + indent;
                foreach (WzImageProperty property in propertySub.WzProperties)
                {
                    WritePropertyToJsonBson(jsonSub, newDepth, property, exportFilePath);
                }

                string jPropertyName = XmlUtil.SanitizeText(propertySub.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertySub.Name), jsonSub)); // add this json to the main json object parent
                }
            }
            else if (prop is WzShortProperty propertyShort)
            {
                JObject jsonShort = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyShort.Name) },
                        { FIELD_TYPE_NAME, "short" },
                        { FIELD_VALUE_NAME, propertyShort.Value},
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyShort.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyShort.Name), jsonShort)); // add this json to the main json object parent
                }
            }
            else if (prop is WzLongProperty propertyLong)
            {
                JObject jsonLong = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyLong.Name) },
                        { FIELD_TYPE_NAME, "long" },
                        { FIELD_VALUE_NAME, propertyLong.Value},
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyLong.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyLong.Name), jsonLong)); // add this json to the main json object parent
                }
            }
            else if (prop is WzUOLProperty propertyUOL)
            {
                JObject jsonUOL = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyUOL.Name) },
                        { FIELD_TYPE_NAME, "uol" },
                        { FIELD_VALUE_NAME, propertyUOL.Value},
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyUOL.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyUOL.Name), jsonUOL)); // add this json to the main json object parent
                }
            }
            else if (prop is WzVectorProperty propertyVector)
            {
                JObject jsonVector = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyVector.Name) },
                        { FIELD_TYPE_NAME, "vector" },
                        { FIELD_X_NAME, propertyVector.X.Value },
                        { FIELD_Y_NAME, propertyVector.Y.Value },
                    };

                string jPropertyName = XmlUtil.SanitizeText(propertyVector.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyVector.Name), jsonVector)); // add this json to the main json object parent
                }
            }
            else if (prop is WzFloatProperty propertyFloat)
            {
                JObject jsonfloat = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyFloat.Name) },
                        { FIELD_TYPE_NAME, "float" },
                    };
                string str2 = Convert.ToString(propertyFloat.Value, formattingInfo);
                if (!str2.Contains("."))
                    str2 += ".0";
                jsonfloat.Add(FIELD_VALUE_NAME, str2);

                string jPropertyName = XmlUtil.SanitizeText(propertyFloat.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyFloat.Name), jsonfloat)); // add this json to the main json object parent
                }
            }
            else if (prop is WzConvexProperty propertyConvex)
            {
                JObject jsonConvex = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyConvex.Name) },
                        { FIELD_TYPE_NAME, "convex" },
                    };
                string newDepth = depth + indent;
                foreach (WzImageProperty property in propertyConvex.WzProperties)
                {
                    WritePropertyToJsonBson(jsonConvex, newDepth, property, exportFilePath);
                }

                string jPropertyName = XmlUtil.SanitizeText(propertyConvex.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyConvex.Name), jsonConvex)); // add this json to the main json object parent
                }
            }
            else if (prop is WzLuaProperty propertyLua)
            {
                string parentName = propertyLua.Parent.Name;

                JObject jsonLua = new JObject
                    {
                        //{ FIELD_DEPTH_NAME, depth },
                        { FIELD_NAME_NAME, XmlUtil.SanitizeText(propertyLua.Name) },
                        { FIELD_TYPE_NAME, "lua" },
                        { FIELD_FILENAME_NAME, parentName },
                    };
                if (bExportBase64Data)
                {
                    jsonLua.Add(FIELD_BASEDATA_NAME, propertyLua.ToString());
                }

                string jPropertyName = XmlUtil.SanitizeText(propertyLua.Name);
                if (!json.ContainsKey(jPropertyName)) // making the assumption that only the first wz image will be used, everything is dropped since its not going to be read in wz anyway
                {
                    json.Add(new JProperty(XmlUtil.SanitizeText(propertyLua.Name), jsonLua)); // add this json to the main json object parent
                }
            }
        }
    }

    public interface IWzFileSerializer
    {
        void SerializeFile(WzFile file, string path);
    }

    public interface IWzDirectorySerializer : IWzFileSerializer
    {
        void SerializeDirectory(WzDirectory dir, string path);
    }

    public interface IWzImageSerializer : IWzDirectorySerializer
    {
        void SerializeImage(WzImage img, string path);
    }

    public interface IWzObjectSerializer
    {
        void SerializeObject(WzObject file, string path);
    }

    public enum LineBreak
    {
        None,
        Windows,
        Unix
    }

    public class NoBase64DataException : System.Exception
    {
        public NoBase64DataException() : base() { }
        public NoBase64DataException(string message) : base(message) { }
        public NoBase64DataException(string message, System.Exception inner) : base(message, inner) { }
        protected NoBase64DataException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context)
        { }
    }

    public class WzImgSerializer : ProgressingWzSerializer, IWzImageSerializer
    {

        public byte[] SerializeImage(WzImage img)
        {
            total = 1; curr = 0;

            using (MemoryStream stream = new MemoryStream())
            {
                using (WzBinaryWriter wzWriter = new WzBinaryWriter(stream, ((WzDirectory)img.parent).WzIv))
                {
                    img.SaveImage(wzWriter);
                    byte[] result = stream.ToArray();

                    return result;
                }
            }
        }

        public void SerializeImage(WzImage img, string outPath)
        {
            total = 1; curr = 0;
            if (Path.GetExtension(outPath) != ".img")
            {
                outPath += ".img";
            }

            using (FileStream stream = File.Create(outPath))
            {
                using (WzBinaryWriter wzWriter = new WzBinaryWriter(stream, ((WzDirectory)img.parent).WzIv))
                {
                    img.SaveImage(wzWriter);
                }
            }
        }

        public void SerializeDirectory(WzDirectory dir, string outPath)
        {
            total = dir.CountImages();
            curr = 0;

            if (!Directory.Exists(outPath))
                WzSerializer.CreateDirSafe(ref outPath);

            if (outPath.Substring(outPath.Length - 1, 1) != @"\")
            {
                outPath += @"\";
            }

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                SerializeDirectory(subdir, outPath + subdir.Name + @"\");
            }
            foreach (WzImage img in dir.WzImages)
            {
                SerializeImage(img, outPath + img.Name);
            }
        }

        public void SerializeFile(WzFile f, string outPath)
        {
            SerializeDirectory(f.WzDirectory, outPath);
        }
    }


    public class WzImgDeserializer : ProgressingWzSerializer
    {
        private readonly bool freeResources;

        public WzImgDeserializer(bool freeResources)
            : base()
        {
            this.freeResources = freeResources;
        }

        public WzImage WzImageFromIMGBytes(byte[] bytes, WzMapleVersion version, string name, bool freeResources)
        {
            byte[] iv = WzTool.GetIvByMapleVersion(version);
            MemoryStream stream = new MemoryStream(bytes);
            WzBinaryReader wzReader = new WzBinaryReader(stream, iv);
            WzImage img = new WzImage(name, wzReader)
            {
                BlockSize = bytes.Length
            };
            img.CalculateAndSetImageChecksum(bytes);

            img.Offset = 0;
            if (freeResources)
            {
                img.ParseEverything = true;
                img.ParseImage(true);

                img.Changed = true;
                wzReader.Close();
            }
            return img;
        }

        /// <summary>
        /// Parse a WZ image from .img file/
        /// </summary>
        /// <param name="inPath"></param>
        /// <param name="iv"></param>
        /// <param name="name"></param>
        /// <param name="successfullyParsedImage"></param>
        /// <returns></returns>
        public WzImage WzImageFromIMGFile(string inPath, byte[] iv, string name, out bool successfullyParsedImage)
        {
            FileStream stream = File.OpenRead(inPath);
            WzBinaryReader wzReader = new WzBinaryReader(stream, iv);

            WzImage img = new WzImage(name, wzReader)
            {
                BlockSize = (int)stream.Length
            };
            byte[] bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            stream.Position = 0;
            img.CalculateAndSetImageChecksum(bytes);
            img.Offset = 0;

            if (freeResources)
            {
                img.ParseEverything = true;

                successfullyParsedImage = img.ParseImage(true);
                img.Changed = true;
                wzReader.Close();
            }
            else
            {
                successfullyParsedImage = true;
            }
            return img;
        }
    }


    public class WzPngMp3Serializer : ProgressingWzSerializer, IWzImageSerializer, IWzObjectSerializer
    {
        //List<WzImage> imagesToUnparse = new List<WzImage>();
        private string outPath;

        public void SerializeObject(WzObject obj, string outPath)
        {
            //imagesToUnparse.Clear();
            total = 0; curr = 0;
            this.outPath = outPath;
            if (!Directory.Exists(outPath))
            {
                WzSerializer.CreateDirSafe(ref outPath);
            }

            if (outPath.Substring(outPath.Length - 1, 1) != @"\")
                outPath += @"\";

            total = CalculateTotal(obj);
            ExportRecursion(obj, outPath);
            /*foreach (WzImage img in imagesToUnparse)
                img.UnparseImage();
            imagesToUnparse.Clear();*/
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeObject(file, path);
        }

        public void SerializeDirectory(WzDirectory file, string path)
        {
            SerializeObject(file, path);
        }

        public void SerializeImage(WzImage file, string path)
        {
            SerializeObject(file, path);
        }

        private int CalculateTotal(WzObject currObj)
        {
            int result = 0;
            if (currObj is WzFile file)
            {
                result += file.WzDirectory.CountImages();
            }
            else if (currObj is WzDirectory directory)
            {
                result += directory.CountImages();
            }
            return result;
        }

        private void ExportRecursion(WzObject currObj, string outPath)
        {
            if (currObj is WzFile wzFile)
            {
                ExportRecursion(wzFile.WzDirectory, outPath);
            }
            else if (currObj is WzDirectory directoryProperty)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + @"\";
                if (!Directory.Exists(outPath))
                    Directory.CreateDirectory(outPath);

                foreach (WzDirectory subdir in directoryProperty.WzDirectories)
                {
                    ExportRecursion(subdir, outPath + subdir.Name + @"\");
                }
                foreach (WzImage subimg in directoryProperty.WzImages)
                {
                    ExportRecursion(subimg, outPath + subimg.Name + @"\");
                }
            }
            else if (currObj is WzCanvasProperty canvasProperty)
            {
                Bitmap bmp = canvasProperty.GetLinkedWzCanvasBitmap();

                string path = outPath + ProgressingWzSerializer.EscapeInvalidFilePathNames(currObj.Name) + ".png";

                bmp.Save(path, ImageFormat.Png);
                //curr++;
            }
            else if (currObj is WzBinaryProperty binProperty)
            {
                string path = outPath + EscapeInvalidFilePathNames(currObj.Name) + ".mp3";

                binProperty.SaveToFile(path);
            }
            else if (currObj is WzImage wzImage)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + @"\";
                if (!Directory.Exists(outPath))

                    Directory.CreateDirectory(outPath);

                bool parse = wzImage.Parsed || wzImage.Changed;
                if (!parse)
                {
                    wzImage.ParseImage();
                }
                foreach (WzImageProperty subprop in wzImage.WzProperties)
                {
                    ExportRecursion(subprop, outPath);
                }
                if (!parse)
                {
                    wzImage.UnparseImage();
                }
                curr++;
            }
            else if (currObj is IPropertyContainer container)
            {
                outPath += EscapeInvalidFilePathNames(currObj.Name) + ".";

                foreach (WzImageProperty subprop in container.WzProperties)
                {
                    ExportRecursion(subprop, outPath);
                }
            }
            else if (currObj is WzUOLProperty property)
            {
                WzObject linkValue = property.LinkValue;

                if (linkValue is WzCanvasProperty canvas)
                {
                    ExportRecursion(canvas, outPath);
                }
            }
        }
    }

    public class WzJsonBsonSerializer : WzSerializer, IWzImageSerializer
    {
        private readonly bool bExportAsJson; // otherwise bson

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="indentation"></param>
        /// <param name="lineBreakType"></param>
        /// <param name="bExportBase64Data"></param>
        /// <param name="bExportAsJson"></param>
        public WzJsonBsonSerializer(int indentation, LineBreak lineBreakType, bool bExportBase64Data, bool bExportAsJson)
            : base(indentation, lineBreakType)
        {
            this.bExportBase64Data = bExportBase64Data;
            this.bExportAsJson = bExportAsJson;
        }

        private void ExportInternal(WzImage img, string path)
        {
            bool parsed = img.Parsed || img.Changed;
            if (!parsed)
                img.ParseImage();
            curr++;

            // TODO: Use System.Text.Json after .NET 5.0 or above 
            // for better performance via SMID related intrinsics
            JObject jsonObject = new JObject();
            foreach (WzImageProperty property in img.WzProperties)
            {
                WritePropertyToJsonBson(jsonObject, indent, property, path);
            }

            if (File.Exists(path))
                File.Delete(path);
            using (System.IO.FileStream file = File.Create(path))
            {
                if (!bExportAsJson)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (BsonWriter writer = new BsonWriter(ms))
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            serializer.Serialize(writer, jsonObject);

                            using (StreamWriter st = new StreamWriter(file))
                            {
                                st.WriteLine(Convert.ToBase64String(ms.ToArray()));
                            }
                        }
                    }
                }
                else // json string
                {
                    using (StreamWriter st = new StreamWriter(file))
                    {
                        st.WriteLine(jsonObject.ToString());
                    }
                }
            }

            if (!parsed)
                img.UnparseImage();
        }

        private void exportDirInternal(WzDirectory dir, string path)
        {
            if (!Directory.Exists(path))
                CreateDirSafe(ref path);

            if (path.Substring(path.Length - 1) != @"\")
                path += @"\";

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                exportDirInternal(subdir, path + ProgressingWzSerializer.EscapeInvalidFilePathNames(subdir.name) + @"\");
            }
            foreach (WzImage subimg in dir.WzImages)
            {
                ExportInternal(subimg, path + ProgressingWzSerializer.EscapeInvalidFilePathNames(subimg.Name) + (bExportAsJson ? ".json" : ".bin"));
            }
        }

        public void SerializeImage(WzImage img, string path)
        {
            total = 1;
            curr = 0;

            if (Path.GetExtension(path) != (bExportAsJson ? ".json" : ".bin"))
                path += (bExportAsJson ? ".json" : ".bin");
            ExportInternal(img, path);
        }

        public void SerializeDirectory(WzDirectory dir, string path)
        {
            total = dir.CountImages();
            curr = 0;
            exportDirInternal(dir, path);
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeDirectory(file.WzDirectory, path);
        }
    }

    public class WzClassicXmlSerializer : WzSerializer, IWzImageSerializer
    {
        public WzClassicXmlSerializer(int indentation, LineBreak lineBreakType, bool exportbase64)
            : base(indentation, lineBreakType)
        { bExportBase64Data = exportbase64; }

        private void exportXmlInternal(WzImage img, string path)
        {
            bool parsed = img.Parsed || img.Changed;
            if (!parsed)
                img.ParseImage();
            curr++;

            if (File.Exists(path))
                File.Delete(path);
            using (TextWriter tw = new StreamWriter(File.Create(path)))
            {
                tw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + lineBreak);
                tw.Write("<imgdir name=\"" + XmlUtil.SanitizeText(img.Name) + "\">" + lineBreak);
                foreach (WzImageProperty property in img.WzProperties)
                {
                    WritePropertyToXML(tw, indent, property, path);
                }
                tw.Write("</imgdir>" + lineBreak);
            }

            if (!parsed)
                img.UnparseImage();
        }

        private void exportDirXmlInternal(WzDirectory dir, string path)
        {
            if (!Directory.Exists(path))
                CreateDirSafe(ref path);

            if (path.Substring(path.Length - 1) != @"\")
                path += @"\";

            foreach (WzDirectory subdir in dir.WzDirectories)
            {
                exportDirXmlInternal(subdir, path + ProgressingWzSerializer.EscapeInvalidFilePathNames(subdir.name) + @"\");
            }
            foreach (WzImage subimg in dir.WzImages)
            {
                exportXmlInternal(subimg, path + ProgressingWzSerializer.EscapeInvalidFilePathNames(subimg.Name) + ".xml");
            }
        }

        public void SerializeImage(WzImage img, string path)
        {
            total = 1; curr = 0;
            if (Path.GetExtension(path) != ".xml") path += ".xml";
            exportXmlInternal(img, path);
        }

        public void SerializeDirectory(WzDirectory dir, string path)
        {
            total = dir.CountImages(); curr = 0;
            exportDirXmlInternal(dir, path);
        }

        public void SerializeFile(WzFile file, string path)
        {
            SerializeDirectory(file.WzDirectory, path);
        }
    }

    public class WzNewXmlSerializer : WzSerializer
    {
        public WzNewXmlSerializer(int indentation, LineBreak lineBreakType)
            : base(indentation, lineBreakType)
        { }

        internal void DumpImageToXML(TextWriter tw, string depth, WzImage img, string exportFilePath)
        {
            bool parsed = img.Parsed || img.Changed;
            if (!parsed)
                img.ParseImage();

            curr++;
            tw.Write(depth + "<wzimg name=\"" + XmlUtil.SanitizeText(img.Name) + "\">" + lineBreak);
            string newDepth = depth + indent;
            foreach (WzImageProperty property in img.WzProperties)
            {
                WritePropertyToXML(tw, newDepth, property, exportFilePath);
            }
            tw.Write(depth + "</wzimg>");
            if (!parsed)
                img.UnparseImage();
        }

        internal void DumpDirectoryToXML(TextWriter tw, string depth, WzDirectory dir, string exportFilePath)
        {
            tw.Write(depth + "<wzdir name=\"" + XmlUtil.SanitizeText(dir.Name) + "\">" + lineBreak);
            foreach (WzDirectory subdir in dir.WzDirectories)
                DumpDirectoryToXML(tw, depth + indent, subdir, exportFilePath);
            foreach (WzImage img in dir.WzImages)
            {
                DumpImageToXML(tw, depth + indent, img, exportFilePath);
            }
            tw.Write(depth + "</wzdir>" + lineBreak);
        }

        /// <summary>
        /// Export combined XML
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="exportFilePath"></param>
        public void ExportCombinedXml(List<WzObject> objects, string exportFilePath)
        {
            total = 1; curr = 0;

            if (Path.GetExtension(exportFilePath) != ".xml")
                exportFilePath += ".xml";

            total += objects.OfType<WzImage>().Count();
            total += objects.OfType<WzDirectory>().Sum(d => d.CountImages());

            bExportBase64Data = true;

            using (TextWriter tw = new StreamWriter(exportFilePath))
            {
                tw.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + lineBreak);
                tw.Write("<xmldump>" + lineBreak);
                foreach (WzObject obj in objects)
                {
                    if (obj is WzDirectory)
                    {
                        DumpDirectoryToXML(tw, indent, (WzDirectory)obj, exportFilePath);
                    }
                    else if (obj is WzImage)
                    {
                        DumpImageToXML(tw, indent, (WzImage)obj, exportFilePath);
                    }
                    else if (obj is WzImageProperty)
                    {
                        WritePropertyToXML(tw, indent, (WzImageProperty)obj, exportFilePath);
                    }
                }
                tw.Write("</xmldump>" + lineBreak);
            }
        }
    }

    public class WzXmlDeserializer : ProgressingWzSerializer
    {
        public static NumberFormatInfo formattingInfo;

        private readonly bool useMemorySaving;
        private readonly byte[] iv;
        private readonly WzImgDeserializer imgDeserializer = new WzImgDeserializer(false);

        public WzXmlDeserializer(bool useMemorySaving, byte[] iv)
            : base()
        {
            this.useMemorySaving = useMemorySaving;
            this.iv = iv;
        }

        #region Public Functions
        public List<WzObject> ParseXML(string path)
        {
            List<WzObject> result = new List<WzObject>();
            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            XmlElement mainElement = (XmlElement)doc.ChildNodes[1];
            curr = 0;
            if (mainElement.Name == "xmldump")
            {
                total = CountImgs(mainElement);
                foreach (XmlElement subelement in mainElement)
                {
                    if (subelement.Name == "wzdir")
                        result.Add(ParseXMLWzDir(subelement));
                    else if (subelement.Name == "wzimg")
                        result.Add(ParseXMLWzImg(subelement));
                    else 
                        throw new InvalidDataException("unknown XML prop " + subelement.Name);
                }
            }
            else if (mainElement.Name == "imgdir")
            {
                total = 1;
                result.Add(ParseXMLWzImg(mainElement));
                curr++;
            }
            else throw new InvalidDataException("unknown main XML prop " + mainElement.Name);
            return result;
        }
        #endregion

        #region Internal Functions
        internal int CountImgs(XmlElement element)
        {
            // Count the number of "wzimg" elements and the number of "wzdir" elements
            int wzimgCount = element.Cast<XmlElement>()
                .Count(e => e.Name == "wzimg");

            // Recursively count the number of "wzimg" elements in each "wzdir" element
            int wzimgInWzdirCount = element.Cast<XmlElement>()
                .Where(e => e.Name == "wzdir")
                .Sum(e => CountImgs(e));

            // Return the total number of "wzimg" elements
            return wzimgCount + wzimgInWzdirCount;
        }


        internal WzDirectory ParseXMLWzDir(XmlElement dirElement)
        {
            WzDirectory result = new WzDirectory(dirElement.GetAttribute("name"));
            foreach (XmlElement subelement in dirElement)
            {
                if (subelement.Name == "wzdir")
                    result.AddDirectory(ParseXMLWzDir(subelement));
                else if (subelement.Name == "wzimg")
                    result.AddImage(ParseXMLWzImg(subelement));
                else throw new InvalidDataException("unknown XML prop " + subelement.Name);
            }
            return result;
        }

        internal WzImage ParseXMLWzImg(XmlElement imgElement)
        {
            string name = imgElement.GetAttribute("name");
            WzImage result = new WzImage(name);
            foreach (XmlElement subelement in imgElement)
            {
                result.WzProperties.Add(ParsePropertyFromXMLElement(subelement));
            }
            result.Changed = true;
            if (this.useMemorySaving)
            {
                string path = Path.GetTempFileName();
                using (WzBinaryWriter wzWriter = new WzBinaryWriter(File.Create(path), iv))
                {
                    result.SaveImage(wzWriter);
                    result.Dispose();
                }

                bool successfullyParsedImage;
                result = imgDeserializer.WzImageFromIMGFile(path, iv, name, out successfullyParsedImage);
            }
            return result;
        }

        internal WzImageProperty ParsePropertyFromXMLElement(XmlElement element)
        {
            switch (element.Name)
            {
                case "imgdir":
                    WzSubProperty sub = new WzSubProperty(element.GetAttribute("name"));
                    foreach (XmlElement subelement in element)
                        sub.AddProperty(ParsePropertyFromXMLElement(subelement));
                    return sub;

                case "canvas":
                    WzCanvasProperty canvas = new WzCanvasProperty(element.GetAttribute("name"));
                    if (!element.HasAttribute("basedata")) 
                        throw new NoBase64DataException("no base64 data in canvas element with name " + canvas.Name);
                    canvas.PngProperty = new WzPngProperty();
                    MemoryStream pngstream = new MemoryStream(Convert.FromBase64String(element.GetAttribute("basedata")));
                    canvas.PngProperty.SetImage((Bitmap)Image.FromStream(pngstream, true, true));
                    foreach (XmlElement subelement in element)
                        canvas.AddProperty(ParsePropertyFromXMLElement(subelement));
                    return canvas;

                case "int":
                    WzIntProperty compressedInt = new WzIntProperty(element.GetAttribute("name"), int.Parse(element.GetAttribute("value"), formattingInfo));
                    return compressedInt;

                case "double":
                    WzDoubleProperty doubleProp = new WzDoubleProperty(element.GetAttribute("name"), double.Parse(element.GetAttribute("value"), formattingInfo));
                    return doubleProp;

                case "null":
                    WzNullProperty nullProp = new WzNullProperty(element.GetAttribute("name"));
                    return nullProp;

                case "sound":
                    if (!element.HasAttribute("basedata") || !element.HasAttribute("basehead") || !element.HasAttribute("length")) throw new NoBase64DataException("no base64 data in sound element with name " + element.GetAttribute("name"));
                    WzBinaryProperty sound = new WzBinaryProperty(element.GetAttribute("name"),
                        int.Parse(element.GetAttribute("length")),
                        Convert.FromBase64String(element.GetAttribute("basehead")),
                        Convert.FromBase64String(element.GetAttribute("basedata")));
                    return sound;

                case "string":
                    WzStringProperty stringProp = new WzStringProperty(element.GetAttribute("name"), element.GetAttribute("value"));
                    return stringProp;

                case "short":
                    WzShortProperty shortProp = new WzShortProperty(element.GetAttribute("name"), short.Parse(element.GetAttribute("value"), formattingInfo));
                    return shortProp;

                case "long":
                    WzLongProperty longProp = new WzLongProperty(element.GetAttribute("name"), long.Parse(element.GetAttribute("value"), formattingInfo));
                    return longProp;

                case "uol":
                    WzUOLProperty uol = new WzUOLProperty(element.GetAttribute("name"), element.GetAttribute("value"));
                    return uol;

                case "vector":
                    WzVectorProperty vector = new WzVectorProperty(element.GetAttribute("name"), new WzIntProperty("x", Convert.ToInt32(element.GetAttribute("x"))), new WzIntProperty("y", Convert.ToInt32(element.GetAttribute("y"))));
                    return vector;

                case "float":
                    WzFloatProperty floatProp = new WzFloatProperty(element.GetAttribute("name"), float.Parse(element.GetAttribute("value"), formattingInfo));
                    return floatProp;

                case "extended":
                    WzConvexProperty convex = new WzConvexProperty(element.GetAttribute("name"));
                    foreach (XmlElement subelement in element)
                        convex.AddProperty(ParsePropertyFromXMLElement(subelement));
                    return convex;
            }
            throw new InvalidDataException("unknown XML prop " + element.Name);
        }
        #endregion
    }
}
