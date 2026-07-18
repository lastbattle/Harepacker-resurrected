using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.Localization
{
    /// <summary>Resolves desktop UI text from the culture-specific UiText resource.</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        private static readonly ResourceManager Resources =
            new("HaCreator.GUI.Localization.UiText", typeof(LocExtension).Assembly);

        public LocExtension(string key) => Key = key;

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Get(Key);

        public static string Get(string key) =>
            Resources.GetString(key, CultureInfo.CurrentUICulture) ??
            Resources.GetString(key, CultureInfo.InvariantCulture) ??
            $"[{key}]";

        public static string Format(string key, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }
}
