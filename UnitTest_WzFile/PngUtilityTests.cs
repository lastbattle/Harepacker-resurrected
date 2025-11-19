using MapleLib.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using System.Drawing;
using System.Drawing.Imaging;

namespace UnitTest_WzFile
{
    [TestClass]
    public class PngUtilityTests
    {
        [TestMethod]
        public void DXT3_RoundTrip_EncodeDecode()
        {
            // Create a test image (32x32, BGRA)
            using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(128 + x % 128, x * 8 % 256, y * 8 % 256, (x + y) % 256));

            // Encode to DXT3
            var dxt3 = typeof(PngUtility).GetMethod("CompressDXT3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            byte[] encoded = (byte[])dxt3.Invoke(null, new object[] { bmp });

            // Decode back
            using var bmpOut = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmpOut.LockBits(new Rectangle(0, 0, 32, 32), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            PngUtility.DecompressImageDXT3(encoded, 32, 32, bmpData);
            bmpOut.UnlockBits(bmpData);

            // Compare pixels
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color orig = bmp.GetPixel(x, y);
                    Color decoded = bmpOut.GetPixel(x, y);
                    Assert.AreEqual(orig.A, decoded.A, $"Alpha mismatch at ({x},{y})");
                }
        }

        [TestMethod]
        public void DXT5_RoundTrip_EncodeDecode()
        {
            using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255 - x % 128, x * 8 % 256, y * 8 % 256, (x + y) % 256));

            var dxt5 = typeof(PngUtility).GetMethod("GetPixelDataFormat2050", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            byte[] encoded = (byte[])dxt5.Invoke(null, new object[] { bmp });

            using var bmpOut = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmpOut.LockBits(new Rectangle(0, 0, 32, 32), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            PngUtility.DecompressImageDXT5(encoded, 32, 32, bmpData);
            bmpOut.UnlockBits(bmpData);

            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color orig = bmp.GetPixel(x, y);
                    Color decoded = bmpOut.GetPixel(x, y);
                    Assert.AreEqual(orig.A, decoded.A, $"Alpha mismatch at ({x},{y})");
                }
        }
    }
}
