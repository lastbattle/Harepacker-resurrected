using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace HaRepacker.GUI.Input
{
    public partial class BitmapInputBox : Window
    {
        private string nameResult;
        private readonly List<Bitmap> bmpResult = new();

        public static bool Show(string title, out string name, out List<Bitmap> bmp)
        {
            BitmapInputBox form = new(title);
            bool accepted = form.ShowDialog() == true;
            name = form.nameResult;
            bmp = form.bmpResult;
            return accepted;
        }

        public BitmapInputBox(string title)
        {
            InitializeComponent();
            Title = title;
            labelName.Text = InputDialogSupport.Text(GetType(), "label1.Text", "Name:");
            labelPath.Text = InputDialogSupport.Text(GetType(), "label2.Text", "Path:");
            okButton.Content = InputDialogSupport.Text(GetType(), "okButton.Text", "OK");
            cancelButton.Content = InputDialogSupport.Text(GetType(), "cancelButton.Text", "Cancel");
            browseButton.Content = InputDialogSupport.Text(GetType(), "browseButton.Text", "Browse…");
        }

        private void Input_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Accept(); }
        private void OkButton_Click(object sender, RoutedEventArgs e) => Accept();
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = Properties.Resources.SelectImage,
                Filter = $"{Properties.Resources.ImagesFilter}|*.jpg;*.bmp;*.png;*.gif;*.tiff"
            };
            if (dialog.ShowDialog(this) == true) pathBox.Text = dialog.FileName;
        }

        private void PathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            bmpResult.Clear();
            previewImage.Source = null;
            try
            {
                string path = pathBox.Text;
                if (!File.Exists(path)) return;

                BitmapImage preview = new();
                preview.BeginInit();
                preview.CacheOption = BitmapCacheOption.OnLoad;
                preview.UriSource = new Uri(path, UriKind.Absolute);
                preview.EndInit();
                preview.Freeze();
                previewImage.Source = preview;

                if (IsPathGif(path))
                {
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    GifBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    foreach (BitmapSource frame in decoder.Frames)
                        bmpResult.Add(BitmapFromSource(frame));
                }
                else
                {
                    using Bitmap source = new(path);
                    bmpResult.Add(new Bitmap(source));
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }

        private void Accept()
        {
            string path = pathBox.Text;
            string name = nameBox.Text;
            bool valid = !string.IsNullOrEmpty(path) && bmpResult.Count > 0 &&
                         ((!string.IsNullOrEmpty(name)) || IsPathGif(path));
            if (!valid) { InputDialogSupport.WarnInvalidInput(); return; }
            nameResult = name;
            DialogResult = true;
        }

        private static Bitmap BitmapFromSource(BitmapSource source)
        {
            using MemoryStream stream = new();
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(stream);
            stream.Position = 0;
            using Bitmap bitmap = new(stream);
            return new Bitmap(bitmap);
        }

        private static bool IsPathGif(string path) =>
            string.Equals(Path.GetExtension(path), ".gif", StringComparison.OrdinalIgnoreCase);
    }
}
