using System;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace HaCreator.GUI.Skill;

[MarkupExtensionReturnType(typeof(string))]
public sealed class SkillEditorTextExtension : MarkupExtension
{
    private static readonly ResourceManager Resources = new("HaCreator.GUI.Skill.SkillEditorText", typeof(SkillEditorTextExtension).Assembly);
    public SkillEditorTextExtension(string key) => Key = key;
    [ConstructorArgument("key")] public string Key { get; set; }
    public override object ProvideValue(IServiceProvider serviceProvider) => Get(Key);
    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? Resources.GetString(key, CultureInfo.InvariantCulture) ?? $"[{key}]";
    public static string Format(string key, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
