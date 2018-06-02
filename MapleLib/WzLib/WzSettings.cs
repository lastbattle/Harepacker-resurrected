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
using MapleLib.WzLib.WzProperties;
using System.Reflection;
using MapleLib.WzLib.WzStructure;
using System.IO;

namespace MapleLib.WzLib
{
    public class WzSettingsManager
    {
        string wzPath;
        Type userSettingsType;
        Type appSettingsType;
        Type xnaColorType = null;


        public WzSettingsManager(string wzPath, Type userSettingsType, Type appSettingsType)
        {
            this.wzPath = wzPath;
            this.userSettingsType = userSettingsType;
            this.appSettingsType = appSettingsType;
        }

        public WzSettingsManager(string wzPath, Type userSettingsType, Type appSettingsType, Type xnaColorType)
            : this(wzPath, userSettingsType, appSettingsType)
        {
            this.xnaColorType = xnaColorType;
        }

        private void ExtractSettingsImage(WzImage settingsImage, Type settingsHolderType)
        {
            if (!settingsImage.Parsed) settingsImage.ParseImage();
            foreach (FieldInfo fieldInfo in settingsHolderType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string settingName = fieldInfo.Name;
                WzImageProperty settingProp = settingsImage[settingName];
                byte[] argb;
                if (settingProp == null)
                    SaveField(settingsImage, fieldInfo);
                else if (fieldInfo.FieldType.BaseType != null && fieldInfo.FieldType.BaseType.FullName == "System.Enum")
                    fieldInfo.SetValue(null, InfoTool.GetInt(settingProp));
                else switch (fieldInfo.FieldType.FullName)
                    {
                        //case "Microsoft.Xna.Framework.Graphics.Color":
                        case "Microsoft.Xna.Framework.Color":
                            if (xnaColorType == null) throw new InvalidDataException("XNA color detected, but XNA type activator is null");
                            argb = BitConverter.GetBytes((uint)((WzDoubleProperty)settingProp).Value);
                            fieldInfo.SetValue(null, Activator.CreateInstance(xnaColorType, argb[0], argb[1], argb[2], argb[3]));
                            break;
                        case "System.Drawing.Color":
                            argb = BitConverter.GetBytes((uint)((WzDoubleProperty)settingProp).Value);
                            fieldInfo.SetValue(null, System.Drawing.Color.FromArgb(argb[3], argb[2], argb[1], argb[0]));
                            break;
                        case "System.Int32":
                            fieldInfo.SetValue(null, InfoTool.GetInt(settingProp));
                            break;
                        case "System.Double":
                            fieldInfo.SetValue(null, ((WzDoubleProperty)settingProp).Value);
                            break;
                        case "System.Boolean":
                            fieldInfo.SetValue(null, InfoTool.GetBool(settingProp));
                            //bool a = (bool)fieldInfo.GetValue(null);
                            break;
                        case "System.Single":
                            fieldInfo.SetValue(null, ((WzFloatProperty)settingProp).Value);
                            break;
                        /*case "WzMapleVersion":
                            fieldInfo.SetValue(null, (WzMapleVersion)InfoTool.GetInt(settingProp));
                            break;
                        case "ItemTypes":
                            fieldInfo.SetValue(null, (ItemTypes)InfoTool.GetInt(settingProp));
                            break;*/
                        case "System.Drawing.Size":
                            fieldInfo.SetValue(null, new System.Drawing.Size(((WzVectorProperty)settingProp).X.Value, ((WzVectorProperty)settingProp).Y.Value));
                            break;
                        case "System.String":
                            fieldInfo.SetValue(null, InfoTool.GetString(settingProp));
                            break;
                        case "System.Drawing.Bitmap":
                            fieldInfo.SetValue(null, ((WzCanvasProperty)settingProp).PngProperty.GetPNG(false));
                            break;
                        default:
                            throw new Exception("unrecognized setting type");
                    }
            }
        }

        private void CreateWzProp(IPropertyContainer parent, WzPropertyType propType, string propName, object value)
        {
            WzImageProperty addedProp;
            switch (propType)
            {
                case WzPropertyType.Float:
                    addedProp = new WzFloatProperty(propName);
                    break;
                case WzPropertyType.Canvas:
                    addedProp = new WzCanvasProperty(propName);
                    ((WzCanvasProperty)addedProp).PngProperty = new WzPngProperty();
                    break;
                case WzPropertyType.Int:
                    addedProp = new WzIntProperty(propName);
                    break;
                case WzPropertyType.Double:
                    addedProp = new WzDoubleProperty(propName);
                    break;
                /*case WzPropertyType.Sound:
                    addedProp = new WzSoundProperty(propName);
                    break;*/
                case WzPropertyType.String:
                    addedProp = new WzStringProperty(propName);
                    break;
                case WzPropertyType.Short:
                    addedProp = new WzShortProperty(propName);
                    break;
                case WzPropertyType.Vector:
                    addedProp = new WzVectorProperty(propName);
                    ((WzVectorProperty)addedProp).X = new WzIntProperty("X");
                    ((WzVectorProperty)addedProp).Y = new WzIntProperty("Y");
                    break;
                default:
                    throw new NotSupportedException("not supported type");
            }
            addedProp.SetValue(value);
            parent.AddProperty(addedProp);
        }

        private void SetWzProperty(WzImage parentImage, string propName, WzPropertyType propType, object value)
        {
            WzImageProperty property = parentImage[propName];
            if (property != null)
            {
                if (property.PropertyType == propType)
                    property.SetValue(value);
                else
                {
                    property.Remove();
                    CreateWzProp(parentImage, propType, propName, value);
                }
            }
            else
                CreateWzProp(parentImage, propType, propName, value);
        }

        private void SaveSettingsImage(WzImage settingsImage, Type settingsHolderType)
        {
            if (!settingsImage.Parsed) settingsImage.ParseImage();
            foreach (FieldInfo fieldInfo in settingsHolderType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                SaveField(settingsImage, fieldInfo);
            }
            settingsImage.Changed = true;
        }

        private void SaveField(WzImage settingsImage, FieldInfo fieldInfo)
        {
            string settingName = fieldInfo.Name;
            if (fieldInfo.FieldType.BaseType != null && fieldInfo.FieldType.BaseType.FullName == "System.Enum")
                SetWzProperty(settingsImage, settingName, WzPropertyType.Int, fieldInfo.GetValue(null));
            else switch (fieldInfo.FieldType.FullName)
                {
                    //case "Microsoft.Xna.Framework.Graphics.Color":
                    case "Microsoft.Xna.Framework.Color":
                        object xnaColor = fieldInfo.GetValue(null);
                        //for some odd reason .NET requires casting the result to uint before it can be
                        //casted to double
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Double, (double)(uint)xnaColor.GetType().GetProperty("PackedValue").GetValue(xnaColor, null));
                        break;
                    case "System.Drawing.Color":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Double, (double)((System.Drawing.Color)fieldInfo.GetValue(null)).ToArgb());
                        break;
                    case "System.Int32":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Int, fieldInfo.GetValue(null));
                        break;
                    case "System.Double":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Double, fieldInfo.GetValue(null));
                        break;
                    case "Single":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Float, fieldInfo.GetValue(null));
                        break;
                    case "System.Drawing.Size":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Vector, fieldInfo.GetValue(null));
                        break;
                    case "System.String":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.String, fieldInfo.GetValue(null));
                        break;
                    case "System.Drawing.Bitmap":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Canvas, fieldInfo.GetValue(null));
                        break;
                    case "System.Boolean":
                        SetWzProperty(settingsImage, settingName, WzPropertyType.Int, (bool)fieldInfo.GetValue(null) ? 1 : 0);
                        break;
                }
        }

        public void Load()
        {
            if (File.Exists(wzPath))
            {
                WzFile wzFile = new WzFile(wzPath, 1337, WzMapleVersion.CLASSIC);
                try
                {
                    wzFile.ParseWzFile();
                    ExtractSettingsImage((WzImage)wzFile["UserSettings.img"], userSettingsType);
                    ExtractSettingsImage((WzImage)wzFile["ApplicationSettings.img"], appSettingsType);
                    wzFile.Dispose();
                }
                catch
                {
                    wzFile.Dispose();
                    throw;
                }
            }
        }

        public void Save()
        {
            bool settingsExist = File.Exists(wzPath);
            WzFile wzFile;
            if (settingsExist)
            {
                wzFile = new WzFile(wzPath, 1337, WzMapleVersion.CLASSIC);
                wzFile.ParseWzFile();
            }
            else
            {
                wzFile = new WzFile(1337, WzMapleVersion.CLASSIC);
                wzFile.Header.Copyright = "Wz settings file generated by MapleLib's WzSettings module created by haha01haha01";
                wzFile.Header.RecalculateFileStart();
                WzImage US = new WzImage("UserSettings.img") { Changed = true, Parsed = true };
                WzImage AS = new WzImage("ApplicationSettings.img") { Changed = true, Parsed = true };
                wzFile.WzDirectory.WzImages.Add(US);
                wzFile.WzDirectory.WzImages.Add(AS);
            }
            SaveSettingsImage((WzImage)wzFile["UserSettings.img"], userSettingsType);
            SaveSettingsImage((WzImage)wzFile["ApplicationSettings.img"], appSettingsType);
            if (settingsExist)
            {
                string tempFile = Path.GetTempFileName();
                string settingsPath = wzFile.FilePath;
                wzFile.SaveToDisk(tempFile);
                wzFile.Dispose();
                File.Delete(settingsPath);
                File.Move(tempFile, settingsPath);
            }
            else
                wzFile.SaveToDisk(wzPath);
        }
    }
}
