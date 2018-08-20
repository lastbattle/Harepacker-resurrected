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
        [JsonProperty(PropertyName = "WindowMaximized")]
        public bool WindowMaximized = false;

        [JsonProperty(PropertyName = "WindowWidth")]
        public int Width = 1024;
        [JsonProperty(PropertyName = "WindowHeight")]
        public int Height = 768;

        [JsonProperty(PropertyName = "FirstRun")]
        public bool FirstRun = true;

        [JsonProperty(PropertyName = "LastBrowserPath")]
        public string LastBrowserPath = "";

        [JsonProperty(PropertyName = "MapleStoryVersion") ]
        [JsonConverter(typeof(StringEnumConverter))]
        public WzMapleVersion MapleVersion = WzMapleVersion.BMS;

        [JsonProperty(PropertyName = "UpdateServerURL")]
        public string UpdateServer = "";
    }
}
