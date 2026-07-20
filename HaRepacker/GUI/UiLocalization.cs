using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Controls;

namespace HaRepacker.GUI
{
    internal static class UiLocalization
    {
        private static readonly ResourceManager TextResources =
            new("HaRepacker.GUI.UiText", typeof(UiLocalization).Assembly);

        static UiLocalization()
        {
            TextResources.IgnoreCase = true;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(LocalizeLoadedElement));
            EventManager.RegisterClassHandler(typeof(UserControl), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(LocalizeLoadedElement));
        }

        private static void LocalizeLoadedElement(object sender, RoutedEventArgs e)
        {
            if (sender is DependencyObject element)
                Apply(element);
        }

        public static string Translate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            try
            {
                return TextResources.GetString(text, CultureInfo.CurrentUICulture) ?? text;
            }
            catch (MissingManifestResourceException)
            {
                // Keep dialogs usable while a culture does not provide the optional
                // centralized WPF text catalog; type-specific resources still apply.
                return text;
            }
        }

        public static void Apply(DependencyObject root)
        {
            if (root is Window window)
                window.Title = Translate(window.Title);

            if (root is TextBlock textBlock)
                textBlock.Text = Translate(textBlock.Text);

            if (root is HeaderedContentControl headered && headered.Header is string header)
                headered.Header = Translate(header);

            if (root is HeaderedItemsControl headeredItems && headeredItems.Header is string itemsHeader)
                headeredItems.Header = Translate(itemsHeader);

            if (root is ContentControl contentControl && contentControl.Content is string content)
                contentControl.Content = Translate(content);

            if (root is FrameworkElement element && element.ToolTip is string toolTip)
                element.ToolTip = Translate(toolTip);

            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject dependencyObject)
                    Apply(dependencyObject);
            }
        }
    }
}
