using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace HaSharedLibrary.Util
{
    public static class BitmapHelper
    { /// <summary>
      /// Takes a bitmap and converts it to an image that can be handled by WPF ImageBrush
      /// </summary>
      /// <param name="src">A bitmap image</param>
      /// <param name="format"></param>
      /// <returns>The image as a BitmapImage for WPF</returns>
        public static BitmapImage Convert(this Bitmap src, System.Drawing.Imaging.ImageFormat format)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                src.Save(ms, format);
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                ms.Seek(0, SeekOrigin.Begin);
                image.StreamSource = ms;
                image.EndInit();

                return image;
            }
        }

        public static Texture2D ToTexture2D(this System.Drawing.Bitmap bitmap, GraphicsDevice device)
        {
            if (bitmap == null)
            {
                return null; //todo handle this in a useful way
            }

            using (System.IO.MemoryStream s = new System.IO.MemoryStream())
            {
                bitmap.Save(s, System.Drawing.Imaging.ImageFormat.Png);
                s.Seek(0, System.IO.SeekOrigin.Begin);
                return Texture2D.FromStream(device, s);
            }
        }

        /// <summary>
        /// Gets the image format extention from ImageFormat object i.e Png returns "png"
        /// </summary>
        /// <param name="imageFormat"></param>
        /// <returns></returns>
        public static string GetImageFormatExtension(ImageFormat imageFormat) {
            // Get all image encoders
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            // Find the codec with the matching format
            foreach (ImageCodecInfo codec in codecs) {
                //Debug.WriteLine(codec.FormatID.ToString() + ", " + imageFormat.Guid.ToString());
                if (codec.FormatID.ToString() == imageFormat.Guid.ToString()) {
                    // Extract the file extension from the codec's FilenameExtension property
                    string extension = codec.FilenameExtension.Split(';').First().TrimStart('*');
                    return extension;
                }
            }
            return "unknown";
        }
    }
}
