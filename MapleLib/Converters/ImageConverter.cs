using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MapleLib.Converters
{
    public static class ImageConverter
    {
        #region Texture2D
        /// <summary>
        ///  Converts Microsoft.Xna.Framework.Graphics.Texture2D to PNG MemoryStream
        /// </summary>
        /// <param name="texture2D"></param>
        /// <returns></returns>
        public static MemoryStream Texture2DToPng(this Texture2D texture2D)
        {
            MemoryStream memoryStream = new MemoryStream();
            texture2D.SaveAsPng(memoryStream, texture2D.Width, texture2D.Height);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }

        /// <summary>
        /// Converts Microsoft.Xna.Framework.Graphics.Texture2D to JPG MemoryStream
        /// </summary>
        /// <param name="texture2D"></param>
        /// <returns></returns>
        public static MemoryStream Texture2DToJpg(this Texture2D texture2D)
        {
            MemoryStream memoryStream = new MemoryStream();
            texture2D.SaveAsJpeg(memoryStream, texture2D.Width, texture2D.Height);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream;
        }
        #endregion

        public static System.Drawing.Bitmap ToWinFormsBitmap(this BitmapSource bitmapsource)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(stream);

                using (var tempBitmap = new System.Drawing.Bitmap(stream))
                {
                    // According to MSDN, one "must keep the stream open for the lifetime of the Bitmap."
                    // So we return a copy of the new bitmap, allowing us to dispose both the bitmap and the stream.
                    return new System.Drawing.Bitmap(tempBitmap);
                }
            }
        }

        /// <summary>
        /// System.Drawing.Bitmap to System.Drawing.Image
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        public static System.Drawing.Image ToImage(this System.Drawing.Bitmap bitmap)
        {
            return (System.Drawing.Image)bitmap;
        }

        /// <summary>
        /// Converts System.Drawing.Bitmap to byte[]
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] BitmapToBytes(this System.Drawing.Bitmap bitmap)
        {
            BitmapData bmpdata = null;
            try
            {
                bmpdata = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                int numbytes = bmpdata.Stride * bitmap.Height;
                byte[] bytedata = new byte[numbytes];
                IntPtr ptr = bmpdata.Scan0;

                Marshal.Copy(ptr, bytedata, 0, numbytes);

                return bytedata;
            }
            finally
            {
                if (bmpdata != null)
                    bitmap.UnlockBits(bmpdata);
            }
        }

        public static BitmapSource ToWpfBitmap(this System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                stream.Position = 0;
                BitmapImage result = new BitmapImage();
                result.BeginInit();
                // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
                // Force the bitmap to load right now so we can dispose the stream.
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();
                return result;
            }
        }
    }
}
