using System.Globalization;
using System.Linq;
using System.Resources;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace HaCreator.GUI.EditorPanels
{
    internal static class EditorPanelLocalizer
    {
        private static readonly ResourceManager ResourceManager =
            new("HaCreator.GUI.EditorPanels.EditorPanelText", typeof(EditorPanelLocalizer).Assembly);

        internal static string Text(string key, string fallback = null)
        {
            return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback ?? key;
        }

        internal static string Format(string key, params object[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, Text(key), arguments);

        internal static void Attach(FrameworkElement root)
        {
            root.Loaded += (_, _) => Apply(root);
        }

        private static void Apply(DependencyObject element)
        {
            if (element is Window window && !BindingOperations.IsDataBound(window, Window.TitleProperty))
                window.Title = Text(window.Title, window.Title);

            if (element is TextBlock textBlock && !BindingOperations.IsDataBound(textBlock, TextBlock.TextProperty) && !string.IsNullOrWhiteSpace(textBlock.Text))
                textBlock.Text = Text(textBlock.Text, textBlock.Text);

            if (element is ContentControl contentControl &&
                contentControl.ReadLocalValue(ContentControl.ContentProperty) is string content)
                contentControl.Content = Text(content, content);

            if (element is HeaderedContentControl headeredControl && !BindingOperations.IsDataBound(headeredControl, HeaderedContentControl.HeaderProperty) && headeredControl.Header is string header)
                headeredControl.Header = Text(header, header);

            if (element is FrameworkElement frameworkElement)
            {
                if (!BindingOperations.IsDataBound(frameworkElement, FrameworkElement.ToolTipProperty) && frameworkElement.ToolTip is string tooltip)
                    frameworkElement.ToolTip = Text(tooltip, tooltip);

                string automationName = AutomationProperties.GetName(frameworkElement);
                if (!BindingOperations.IsDataBound(frameworkElement, AutomationProperties.NameProperty) && !string.IsNullOrWhiteSpace(automationName))
                    AutomationProperties.SetName(frameworkElement, Text(automationName, automationName));

                string automationHelp = AutomationProperties.GetHelpText(frameworkElement);
                if (!BindingOperations.IsDataBound(frameworkElement, AutomationProperties.HelpTextProperty) && !string.IsNullOrWhiteSpace(automationHelp))
                    AutomationProperties.SetHelpText(frameworkElement, Text(automationHelp, automationHelp));
            }

            // Localize authored controls, not generated template visuals. ContentPresenter text can
            // look like a literal while being regenerated from a bound header; replacing it freezes the display.
            foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
                Apply(child);
        }
    }
}
