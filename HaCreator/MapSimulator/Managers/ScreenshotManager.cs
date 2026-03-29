using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;
using System.Drawing;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Handles screenshot capture and saving for the MapSimulator.
    /// Screenshots are saved in the same naming format as the official MapleStory client.
    /// </summary>
    public class ScreenshotManager
    {
        private bool _saveScreenshot = false;
        private bool _saveScreenshotComplete = true;

        /// <summary>
        /// Gets or sets whether a screenshot should be taken on the next frame
        /// </summary>
        public bool TakeScreenshot
        {
            get => _saveScreenshot;
            set => _saveScreenshot = value;
        }

        /// <summary>
        /// Gets whether the screenshot save is complete
        /// </summary>
        public bool IsComplete => _saveScreenshotComplete;

        /// <summary>
        /// Request a screenshot to be taken
        /// </summary>
        public void RequestScreenshot()
        {
            _saveScreenshot = true;
        }

        /// <summary>
        /// Process screenshot capture if requested.
        /// Should be called at the end of Draw() after all rendering is complete.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device to capture from</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ProcessScreenshot(GraphicsDevice graphicsDevice)
        {
            if (!_saveScreenshotComplete)
                return;

            if (_saveScreenshot)
            {
                _saveScreenshot = false;

                // Pull the picture from the buffer
                int backBufferWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
                int backBufferHeight = graphicsDevice.PresentationParameters.BackBufferHeight;
                int[] backBuffer = new int[backBufferWidth * backBufferHeight];
                graphicsDevice.GetBackBufferData(backBuffer);

                // Copy to texture
                using (Texture2D texture = new Texture2D(graphicsDevice, backBufferWidth, backBufferHeight, false, SurfaceFormat.Color))
                {
                    texture.SetData(backBuffer);

                    // Get a date for file name (same naming scheme as official client)
                    DateTime dateTimeNow = DateTime.Now;
                    string fileName = string.Format("Maple_{0}{1}{2}_{3}{4}{5}.png",
                            dateTimeNow.Day.ToString("D2"),
                            dateTimeNow.Month.ToString("D2"),
                            (dateTimeNow.Year - 2000).ToString("D2"),
                            dateTimeNow.Hour.ToString("D2"),
                            dateTimeNow.Minute.ToString("D2"),
                            dateTimeNow.Second.ToString("D2"));

                    SaveTextureAsJpeg(texture, fileName, backBufferWidth, backBufferHeight);
                }
                _saveScreenshotComplete = true;
            }
        }

        public bool TrySaveBackBufferAsJpeg(GraphicsDevice graphicsDevice, string filePath, out string error)
        {
            error = null;
            if (graphicsDevice == null)
            {
                error = "Graphics device is unavailable.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                error = "Screenshot path is empty.";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                int backBufferWidth = graphicsDevice.PresentationParameters.BackBufferWidth;
                int backBufferHeight = graphicsDevice.PresentationParameters.BackBufferHeight;
                int[] backBuffer = new int[backBufferWidth * backBufferHeight];
                graphicsDevice.GetBackBufferData(backBuffer);

                using Texture2D texture = new Texture2D(graphicsDevice, backBufferWidth, backBufferHeight, false, SurfaceFormat.Color);
                texture.SetData(backBuffer);
                SaveTextureAsJpeg(texture, filePath, backBufferWidth, backBufferHeight);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Gets the image encoder for the specified format
        /// </summary>
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private void SaveTextureAsJpeg(Texture2D texture, string fileName, int width, int height)
        {
            using MemoryStream streamPng = new MemoryStream();
            texture.SaveAsPng(streamPng, width, height);
            streamPng.Position = 0;

            using Bitmap bitmap = new Bitmap(streamPng);
            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            EncoderParameters encoderParameters = new EncoderParameters(1);
            encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);
            bitmap.Save(fileName, jpgEncoder, encoderParameters);
        }
    }
}
