using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace HaRepacker.GUI.Input
{
    internal static class InputDialogSupport
    {
        public static string Text(Type dialogType, string key, string fallback)
        {
            string text = new ComponentResourceManager(dialogType).GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
            return UiLocalization.Translate(text);
        }

        public static void WarnInvalidInput()
        {
            MessageBox.Show(Properties.Resources.EnterValidInput, Properties.Resources.Warning,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
