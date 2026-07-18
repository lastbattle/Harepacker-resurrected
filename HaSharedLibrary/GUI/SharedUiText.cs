using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaSharedLibrary.GUI
{
    internal static class SharedUiText
    {
        private static readonly ResourceManager ResourceManager =
            new("HaSharedLibrary.GUI.SharedUiText", typeof(SharedUiText).Assembly);

        internal static string Get(string key, string fallback = null)
        {
            return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback ?? key;
        }
    }

    [MarkupExtensionReturnType(typeof(string))]
    public sealed class SharedTextExtension : MarkupExtension
    {
        public SharedTextExtension(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public override object ProvideValue(System.IServiceProvider serviceProvider) => SharedUiText.Get(Key);
    }
}
