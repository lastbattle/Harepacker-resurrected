using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            if (element is Window window)
                window.Title = Text(window.Title, window.Title);

            if (element is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
                textBlock.Text = Text(textBlock.Text, textBlock.Text);

            if (element is ContentControl contentControl && contentControl.Content is string content)
                contentControl.Content = Text(content, content);

            if (element is HeaderedContentControl headeredControl && headeredControl.Header is string header)
                headeredControl.Header = Text(header, header);

            if (element is FrameworkElement frameworkElement && frameworkElement.ToolTip is string tooltip)
                frameworkElement.ToolTip = Text(tooltip, tooltip);

            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
                Apply(VisualTreeHelper.GetChild(element, index));
        }
    }
}
