using MapleLib.WzLib.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaRepacker.Configuration
{
    public class UserSettings
    {
        [JsonProperty(PropertyName = "Indentation")]
        public int Indentation = 0;

        [JsonProperty(PropertyName = "LineBreakType")]
        [JsonConverter(typeof(StringEnumConverter))]
        public LineBreak LineBreakType = LineBreak.None;

        [JsonProperty(PropertyName = "DefaultXmlFolder")]
        public string DefaultXmlFolder = "";

        [JsonProperty(PropertyName = "UseApngIncompatibilityFrame")]
        public bool UseApngIncompatibilityFrame = true;

        [JsonProperty(PropertyName = "AutoAssociate")]
        public bool AutoAssociate = true;

        [JsonProperty(PropertyName = "AutoUpdate")]
        public bool AutoUpdate = true;

        [JsonProperty(PropertyName = "Sort")]
        public bool Sort = true;

        [JsonProperty(PropertyName = "SuppressWarnings")]
        public bool SuppressWarnings = false;

        [JsonProperty(PropertyName = "ParseImagesInSearch")]
        public bool ParseImagesInSearch = false;

        [JsonProperty(PropertyName = "SearchStringValues")]
        public bool SearchStringValues = true;

        // Animate
        [JsonProperty(PropertyName = "DevImgSequences")]
        public bool DevImgSequences = false;

        [JsonProperty(PropertyName = "CartesianPlane")]
        public bool CartesianPlane = true;

        [JsonProperty(PropertyName = "DelayNextLoop")]
        public int DelayNextLoop = 60;

        [JsonProperty(PropertyName = "Plane")]
        public bool Plane = true;

        [JsonProperty(PropertyName = "PlanePosition")]
        public int PlanePosition = 0;//center

        // Themes
        [JsonProperty(PropertyName = "ThemeColor")]
        public int ThemeColor = 1;//white = 1, black = 0
    }
}
