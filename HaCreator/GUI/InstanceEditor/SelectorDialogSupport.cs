using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace HaCreator.GUI.InstanceEditor
{
    internal static class SelectorDialogSupport
    {
        public static void Filter(ListBox list, IEnumerable<string> source, string query)
        {
            string value = query?.Trim() ?? string.Empty;
            list.ItemsSource = source.Where(item =>
                value.Length == 0 || item.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();
            if (value.Length > 0 && list.Items.Count > 0)
                list.SelectedIndex = 0;
        }

        public static bool TryGetBracketedId(string value, out string id)
        {
            Match match = Regex.Match(value ?? string.Empty, @"\[(\d+)\]");
            id = match.Success ? match.Groups[1].Value : string.Empty;
            return match.Success;
        }

        public static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            if (bitmap == null)
                return null;
            IntPtr handle = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(handle);
            }
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);
    }
}
