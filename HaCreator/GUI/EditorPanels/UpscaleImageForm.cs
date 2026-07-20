using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace HaCreator.GUI.EditorPanels
{
    public partial class UpscaleImageForm : Window
    {
        private bool _bUserAcceptedImage = false;
        private readonly Bitmap _beforeImage;
        /// <summary>
        /// The return values for the user selection
        /// </summary>
        public bool UserAcceptedImage
        {
            get { return _bUserAcceptedImage; }
            private set { this._bUserAcceptedImage = value; }
        }


        private Bitmap _upscaledImage = null;
        public Bitmap UpscaledImage
        {
            get { return _upscaledImage; }
            private set { this._upscaledImage = value; }
        }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="beforeImage"></param>
        public UpscaleImageForm(Bitmap beforeImage)
        {
            InitializeComponent();
            EditorPanelLocalizer.Attach(this);
            _beforeImage = beforeImage;
            BeforeImage.Source = ToBitmapSource(beforeImage);
        }

        #region Window events
        /// <summary>
        /// On load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_beforeImage != null)
            {
                try
                {
                    Bitmap returnImage = await AiSingleImageUpscale(_beforeImage, 0.25f);
                    _upscaledImage = returnImage;
                    AfterImage.Source = ToBitmapSource(returnImage);
                    ProcessingStatus.Text = EditorPanelLocalizer.Text("Status_Ready", "Ready");
                    ProcessingStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
                    AcceptButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    ProcessingStatus.Text = EditorPanelLocalizer.Format("Status_UpscaleFailed", ex.Message);
                    ProcessingStatus.Foreground = System.Windows.Media.Brushes.Firebrick;
                }
            }
        }

        /// <summary>
        /// The form is being closed by the user (e.g., clicking the X button)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult != true)
                _bUserAcceptedImage = false;
            AfterImage.Source = null;
            BeforeImage.Source = null;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _bUserAcceptedImage = false;
                Close();
            }
            else if (e.Key == Key.Enter && AcceptButton.IsEnabled)
            {
                Accept_Click(null, null);
            }
        }
        #endregion

        #region UI Events
        /// <summary>
        /// Ok ok accepted click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            _bUserAcceptedImage = true;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Ok cancel click
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _bUserAcceptedImage = false;
            Close();
        }
        #endregion

        #region Image upscale
        private async Task<Bitmap> AiSingleImageUpscale(Bitmap inputImage, float downscaleFactorAfter)
        {
            const float SCALE_UP_FACTOR = 4; // factor to scale up with neural networks

            // Create temporary directories
            string pathIn = Path.Combine(Path.GetTempPath(), "HaCreator_ImageUpscaleInput_" + Guid.NewGuid().ToString());
            string pathOut = Path.Combine(Path.GetTempPath(), "HaCreator_ImageUpscaleOutput_" + Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(pathIn);
                Directory.CreateDirectory(pathOut);

                // Save input image
                string inputFilePath = Path.Combine(pathIn, "input.png");
                inputImage.Save(inputFilePath, System.Drawing.Imaging.ImageFormat.Png);

                // Upscale image
                await RealESRGAN_AI_Upscale.EsrganNcnn.Run(pathIn, pathOut, (int)SCALE_UP_FACTOR);

                // Load upscaled image
                string outputFilePath = Path.Combine(pathOut, "input.png");
                using (Bitmap upscaledBitmap = new Bitmap(outputFilePath))
                {
                    // Downscale if necessary
                    if (downscaleFactorAfter != 1)
                    {
                        int newWidth = (int)(upscaledBitmap.Width * downscaleFactorAfter);
                        int newHeight = (int)(upscaledBitmap.Height * downscaleFactorAfter);

                        Bitmap finalBitmap = new Bitmap(newWidth, newHeight);
                        using (Graphics g = Graphics.FromImage(finalBitmap))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                            g.DrawImage(upscaledBitmap, 0, 0, newWidth, newHeight);
                        }

                        return finalBitmap;
                    }
                    else
                    {
                        return new Bitmap(upscaledBitmap);
                    }
                }
            }
            finally
            {
                // Clean up temporary directories
                if (Directory.Exists(pathIn))
                    Directory.Delete(pathIn, true);
                if (Directory.Exists(pathOut))
                    Directory.Delete(pathOut, true);
            }
        }
        #endregion

        private static BitmapSource ToBitmapSource(Bitmap bitmap)
        {
            if (bitmap == null)
                return null;

            using MemoryStream stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
