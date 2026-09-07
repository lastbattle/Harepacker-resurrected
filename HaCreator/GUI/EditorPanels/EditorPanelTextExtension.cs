using System;
using System.Windows.Markup;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>Resolves editor text when XAML is instantiated, including deferred chat templates.</summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class EditorPanelTextExtension : MarkupExtension
    {
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => EditorPanelLocalizer.Text(Key);
    }
}
