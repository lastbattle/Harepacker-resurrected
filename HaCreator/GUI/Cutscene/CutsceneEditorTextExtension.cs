using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.Cutscene
{
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class CutsceneEditorTextExtension : MarkupExtension
    {
        private static readonly ResourceManager Resources =
            new("HaCreator.GUI.Cutscene.CutsceneEditorText", typeof(CutsceneEditorTextExtension).Assembly);

        public CutsceneEditorTextExtension(string key) => Key = key;
        public string Key { get; }
        public override object ProvideValue(System.IServiceProvider serviceProvider) => Get(Key);
        public static string Get(string key, params object[] arguments)
        {
            string value = Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
            return arguments.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, arguments);
        }
    }
}
