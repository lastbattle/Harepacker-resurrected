using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

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

                    using (MemoryStream stream_png = new MemoryStream())
                    {
                        texture.SaveAsPng(stream_png, backBufferWidth, backBufferHeight);

                        System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(stream_png);
                        ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);

                        // Create an EncoderParameters object with quality setting
                        EncoderParameters myEncoderParameters = new EncoderParameters(1);
                        myEncoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L);

                        bitmap.Save(fileName, jpgEncoder, myEncoderParameters);
                    }
                }
                _saveScreenshotComplete = true;
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
    }
}
