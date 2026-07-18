using System.Globalization;
using System.Resources;

namespace HaCreator.GUI.Localization
{
    internal static class MapEditorText
    {
        private static readonly ResourceManager ResourceManager =
            new("HaCreator.GUI.Localization.MapEditorText", typeof(MapEditorText).Assembly);

        internal static string Get(string key, string fallback = null) =>
            ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback ?? key;

        internal static string Format(string key, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }
}
