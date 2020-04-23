using MapleLib.WzLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaRepacker.Configuration
{
    public class ApplicationSettings
    {
        #region Window
        [JsonProperty(PropertyName = "WindowMaximized")]
        public bool WindowMaximized = false;

        [JsonProperty(PropertyName = "WindowWidth")]
        public int Width = 1024;
        [JsonProperty(PropertyName = "WindowHeight")]
        public int Height = 768;
        #endregion

        #region Etc
        [JsonProperty(PropertyName = "FirstRun")]
        public bool FirstRun = true;

        [JsonProperty(PropertyName = "LastBrowserPath")]
        public string LastBrowserPath = "";
        #endregion

        #region Encryption
        [JsonProperty(PropertyName = "MapleStoryVersion") ]
        [JsonConverter(typeof(StringEnumConverter))]
        public WzMapleVersion MapleVersion = WzMapleVersion.BMS;

        [JsonProperty(PropertyName = "MapleStoryVersion_EncryptionBytes")]
        public string MapleVersion_EncryptionBytes = "0x00-0x00-0x00-0x00";
        #endregion

        #region Auto update
        [JsonProperty(PropertyName = "UpdateServerURL")]
        public string UpdateServer = "";
        #endregion
    }
}
