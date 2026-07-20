using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.Localization
{
    /// <summary>Resolves localized text for HaCreator desktop dialogs and reusable controls.</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class DialogTextExtension : MarkupExtension
    {
        private static readonly ResourceManager Resources =
            new("HaCreator.GUI.Localization.DialogText", typeof(DialogTextExtension).Assembly);

        public DialogTextExtension(string key) => Key = key;

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
