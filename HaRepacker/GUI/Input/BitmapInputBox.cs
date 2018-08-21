/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace HaRepacker.GUI.Input
{
    /// <summary>
    /// Bitmap input box
    /// Returns a list of images selected
    /// </summary>
    public partial class BitmapInputBox : Form
    {
        public static bool Show(string title, out string name, out List<Bitmap> bmp)
        {
            BitmapInputBox form = new BitmapInputBox(title);
            bool result = form.ShowDialog() == DialogResult.OK;
            name = form.nameResult;
            bmp = form.bmpResult;

            return result;
        }

        private string nameResult = null;
        private List<Bitmap> bmpResult = new List<Bitmap>();

        public BitmapInputBox(string title)
        {
            InitializeComponent();
            DialogResult = DialogResult.Cancel;
            Text = title;
        }

        private void keyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
                okButton_Click(null, null);
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            string filePath = pathBox.Text;
            string fileName = nameBox.Text;

            bool validated = false;

            // Validate input
            if (nameBox.Text != null && pathBox.Text != null && pathBox.Text != "" && pictureBox.Image != null)
            {
                if ((fileName == string.Empty && IsPathGIF(filePath)) || fileName != string.Empty) // only allow string empty name if its a GIF. [Frames of file name 0, 1, 2, 3, 4, 5]
                {
                    validated = true;
                }
            }

            if (validated)
            {
                nameResult = nameBox.Text;

                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(Properties.Resources.EnterValidInput, Properties.Resources.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = Properties.Resources.SelectImage,
                Filter = string.Format("{0}|*.jpg;*.bmp;*.png;*.gif;*.tiff", Properties.Resources.ImagesFilter)
            };
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // Set path
                pathBox.Text = dialog.FileName;
            }
        }

        /// <summary>
        /// On path changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pathBox_TextChanged(object sender, EventArgs e)
        {
            if (pictureBox.Image != null)
            {
                pictureBox.Image.Dispose();
                pictureBox.Image = null;
            }

            string FilePath = pathBox.Text;

            try
            {
                Image img = Image.FromFile(FilePath);

                pictureBox.Size = img.Size;
                pictureBox.Image = img;


                if (IsPathGIF(FilePath))
                {
                    using (Stream imageStreamSource = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                    {
                        GifBitmapDecoder decoder = new GifBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                        foreach (BitmapSource src in decoder.Frames)
                        {
                            bmpResult.Add(BitmapFromSource(src));
                        }
                    }
                } else
                {
                    bmpResult.Add((Bitmap)img);
                }
            }
            catch (Exception exp)
            {
                Debug.WriteLine(exp.ToString());
            }
        }
        /// <summary>
        /// Converts a BitmapSource object to Bitmap
        /// </summary>
        /// <param name="bitmapsource"></param>
        /// <returns></returns>
        private static Bitmap BitmapFromSource(BitmapSource bitmapsource)
        {
            using (var outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                return new Bitmap(outStream);
            }
        }

        /// <summary>
        /// Is the file path specified a GIF
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static bool IsPathGIF(string filePath)
        {
            return filePath.ToLower().EndsWith("gif");
        }
    }
}
