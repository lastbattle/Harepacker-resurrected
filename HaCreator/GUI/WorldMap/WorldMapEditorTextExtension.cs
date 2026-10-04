using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.WorldMap;

/// <summary>Markup and code-behind access to World Map Editor strings.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class WorldMapEditorTextExtension : MarkupExtension
{
    private static readonly ResourceManager Resources =
        new("HaCreator.GUI.WorldMap.WorldMapEditorText", typeof(WorldMapEditorTextExtension).Assembly);

    public WorldMapEditorTextExtension(string key) => Key = key;

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
