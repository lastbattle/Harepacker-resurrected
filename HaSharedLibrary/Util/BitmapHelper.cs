using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
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

        /// <summary>
        /// Takes a System.Windows.Media.ImageSource and converts it back to System.Drawing.Bitmap
        /// </summary>
        /// <param name="imageSource"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static Bitmap ConvertImageSourceToBitmap(this ImageSource imageSource) {
            BitmapSource bitmapSource = imageSource as BitmapSource;
            if (bitmapSource == null) {
                throw new ArgumentException("ImageSource must be of type BitmapSource");
            }

            Bitmap bitmap;
            using (MemoryStream outStream = new MemoryStream()) {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapSource));
                enc.Save(outStream);
                bitmap = new Bitmap(outStream);
            }
            return bitmap;
        }

        /// <summary>
        /// Converts a System.Drawing.Bitmap bitmap to Texture2D
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="device"></param>
        /// <returns></returns>
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
        /// Converts a bitmap to Texture2D and disposes the bitmap afterwards.
        /// Use this when the bitmap is only an intermediate decode step.
        /// </summary>
        public static Texture2D ToTexture2DAndDispose(this System.Drawing.Bitmap bitmap, GraphicsDevice device)
        {
            if (bitmap == null)
            {
                return null;
            }

            try
            {
                return bitmap.ToTexture2D(device);
            }
            finally
            {
                bitmap.Dispose();
            }
        }

        public static Bitmap ToBitmap(this Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            int width = texture.Width;
            int height = texture.Height;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            Microsoft.Xna.Framework.Color[] pixelData = new Microsoft.Xna.Framework.Color[width * height];
            texture.GetData(pixelData);

            Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Rectangle bounds = new Rectangle(0, 0, width, height);
            BitmapData bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[pixelData.Length * 4];
                for (int i = 0; i < pixelData.Length; i++)
                {
                    int index = i * 4;
                    Microsoft.Xna.Framework.Color color = pixelData[i];
                    bytes[index] = color.B;
                    bytes[index + 1] = color.G;
                    bytes[index + 2] = color.R;
                    bytes[index + 3] = color.A;
                }

                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bitmapData.Scan0, bytes.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        /// <summary>
        /// Get the size of an image in Kilobytes
        /// </summary>
        /// <param name="imageSource"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetImageSizeInKB(ImageSource imageSource) {
            if (imageSource == null)
                throw new ArgumentNullException(nameof(imageSource));

            if (!(imageSource is BitmapSource bitmapSource))
                throw new ArgumentException("ImageSource must be a BitmapSource", nameof(imageSource));

            using (MemoryStream stream = new MemoryStream()) {
                BitmapEncoder encoder = new PngBitmapEncoder(); // You can change this to other formats if needed
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(stream);

                return stream.Length / 1024.0; // Convert bytes to kilobytes
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

        /// <summary>
        /// Applies a color filter to a bitmap
        /// </summary>
        /// <param name="originalBitmap"></param>
        /// <param name="filterColor"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitmap ApplyColorFilter(Bitmap originalBitmap, System.Windows.Media.Color filterColor) {
            // Create a copy of the original bitmap
            Bitmap filteredBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height);

            // Create a ColorMatrix object
            ColorMatrix colorMatrix = new ColorMatrix(new float[][]
            {
                new float[] {filterColor.R / 255f, 0, 0, 0, 0},
                new float[] {0, filterColor.G / 255f, 0, 0, 0},
                new float[] {0, 0, filterColor.B / 255f, 0, 0},
                new float[] {0, 0, 0, filterColor.A / 255f, 0},
                new float[] {0, 0, 0, 0, 1}
            });

            // Create ImageAttributes
            using (ImageAttributes attributes = new ImageAttributes()) {
                attributes.SetColorMatrix(colorMatrix);

                // Draw the filtered image
                using (Graphics g = Graphics.FromImage(filteredBitmap)) {
                    g.DrawImage(originalBitmap, new Rectangle(0, 0, originalBitmap.Width, originalBitmap.Height),
                        0, 0, originalBitmap.Width, originalBitmap.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            return filteredBitmap;
        }
    }
}
