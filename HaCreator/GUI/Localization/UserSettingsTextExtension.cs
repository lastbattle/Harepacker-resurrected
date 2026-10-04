using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class UserSettingsTextExtension : MarkupExtension
{
    private static readonly ResourceManager Resources = new("HaCreator.GUI.Localization.UserSettingsText", typeof(UserSettingsTextExtension).Assembly);
    public UserSettingsTextExtension(string key) => Key = key;
    [ConstructorArgument("key")] public string Key { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) => Get(Key);
    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? Resources.GetString(key, CultureInfo.InvariantCulture) ?? $"[{key}]";
}
