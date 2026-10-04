using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.FrameAnimation
{
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class AnimationEditorTextExtension : MarkupExtension
    {
        private static readonly ResourceManager ResourceManager =
            new("HaCreator.GUI.Animation.AnimationEditorText", typeof(AnimationEditorTextExtension).Assembly);

        public AnimationEditorTextExtension(string key) => Key = key;
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
