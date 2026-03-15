using MapleLib.WzLib.WzStructure;

namespace HaCreator.MapSimulator.Fields
{
    public static class FieldEnvironmentEffectEvaluator
    {
        public static WeatherType ResolveAmbientWeather(MapInfo mapInfo)
        {
            if (mapInfo?.snow == true)
            {
                return WeatherType.Snow;
            }

            if (mapInfo?.rain == true)
            {
                return WeatherType.Rain;
            }

            return WeatherType.None;
        }

        public static string GetAmbientWeatherEntryMessage(MapInfo mapInfo)
        {
            return ResolveAmbientWeather(mapInfo) switch
            {
                WeatherType.Rain => "Ambient field weather: rain.",
                WeatherType.Snow => "Ambient field weather: snow.",
                _ => null
            };
        }
    }
}
