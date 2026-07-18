using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.Quest
{
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class QuestTextExtension : MarkupExtension
    {
        private static readonly ResourceManager ResourceManager =
            new("HaCreator.GUI.Quest.QuestEditorText", typeof(QuestTextExtension).Assembly);

        public QuestTextExtension(string key) => Key = key;
        public string Key { get; }

        public override object ProvideValue(System.IServiceProvider serviceProvider) =>
            ResourceManager.GetString(Key, CultureInfo.CurrentUICulture) ?? Key;

        public static string Get(string key, params object[] arguments)
        {
            string format = ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
            return arguments.Length == 0 ? format : string.Format(CultureInfo.CurrentCulture, format, arguments);
        }
    }
}
