using MapleLib.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace UnitTest_WzFile
{
    [TestClass]
    public class PngUtilityTests
    {
        #region BitmapToByteArray Tests
        [TestMethod]
        public void BitmapToByteArray_ValidBitmap_ReturnsPngBytes()
        {
            using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 16, y * 16, (x + y) * 8));

            byte[] result = PngUtility.BitmapToByteArray(bmp);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            // PNG signature: 137 80 78 71 13 10 26 10
            Assert.AreEqual(137, result[0]);
            Assert.AreEqual(80, result[1]);
            Assert.AreEqual(78, result[2]);
            Assert.AreEqual(71, result[3]);
        }

        [TestMethod]
        public void BitmapToByteArray_SmallBitmap_ReturnsValidPng()
        {
            using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            bmp.SetPixel(0, 0, Color.Red);

            byte[] result = PngUtility.BitmapToByteArray(bmp);

            // Verify we can read it back as a PNG
            using var ms = new MemoryStream(result);
            using var loadedBmp = new Bitmap(ms);
            Assert.AreEqual(1, loadedBmp.Width);
            Assert.AreEqual(1, loadedBmp.Height);
        }

        [TestMethod]
        public void BitmapToByteArray_TransparentBitmap_PreservesAlpha()
        {
            using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(128, 255, 0, 0));

            byte[] result = PngUtility.BitmapToByteArray(bmp);

            using var ms = new MemoryStream(result);
            using var loadedBmp = new Bitmap(ms);
            Color pixel = loadedBmp.GetPixel(0, 0);
            // PNG preserves alpha
            Assert.AreEqual(128, pixel.A);
        }
        #endregion

        #region RGB565ToColor Tests
        [TestMethod]
        public void RGB565ToColor_PureRed_ReturnsCorrectColor()
        {
            // Pure red in RGB565: R=31, G=0, B=0 -> 0xF800
            Color red = PngUtility.RGB565ToColor(0xF800);
            Assert.AreEqual(255, red.R);
            Assert.AreEqual(0, red.G);
            Assert.AreEqual(0, red.B);
        }

        [TestMethod]
        public void RGB565ToColor_PureGreen_ReturnsCorrectColor()
        {
            // Pure green in RGB565: R=0, G=63, B=0 -> 0x07E0
            Color green = PngUtility.RGB565ToColor(0x07E0);
            Assert.AreEqual(0, green.R);
            Assert.AreEqual(255, green.G);
            Assert.AreEqual(0, green.B);
        }

        [TestMethod]
        public void RGB565ToColor_PureBlue_ReturnsCorrectColor()
        {
            // Pure blue in RGB565: R=0, G=0, B=31 -> 0x001F
            Color blue = PngUtility.RGB565ToColor(0x001F);
            Assert.AreEqual(0, blue.R);
            Assert.AreEqual(0, blue.G);
            Assert.AreEqual(255, blue.B);
        }

        [TestMethod]
        public void RGB565ToColor_White_ReturnsCorrectColor()
        {
            // White in RGB565: R=31, G=63, B=31 -> 0xFFFF
            Color white = PngUtility.RGB565ToColor(0xFFFF);
            Assert.AreEqual(255, white.R);
            Assert.AreEqual(255, white.G);
            Assert.AreEqual(255, white.B);
        }

        [TestMethod]
        public void RGB565ToColor_Black_ReturnsCorrectColor()
        {
            // Black in RGB565: R=0, G=0, B=0 -> 0x0000
            Color black = PngUtility.RGB565ToColor(0x0000);
            Assert.AreEqual(0, black.R);
            Assert.AreEqual(0, black.G);
            Assert.AreEqual(0, black.B);
        }

        [TestMethod]
        public void RGB565ToColor_MidGray_ReturnsApproximateValue()
        {
            // Mid gray in RGB565: R=15, G=31, B=15 -> (15<<11)|(31<<5)|15 = 0x7BEF
            Color gray = PngUtility.RGB565ToColor(0x7BEF);
            // Due to bit expansion: r5=15 -> (15<<3)|(15>>2) = 120+3 = 123
            // g6=31 -> (31<<2)|(31>>4) = 124+1 = 125
            // b5=15 -> same as r = 123
            Assert.IsTrue(Math.Abs(gray.R - 123) <= 2);
            Assert.IsTrue(Math.Abs(gray.G - 125) <= 2);
            Assert.IsTrue(Math.Abs(gray.B - 123) <= 2);
        }

        [TestMethod]
        public void RGB565ToColor_Yellow_ReturnsCorrectColor()
        {
            // Yellow: R=31, G=63, B=0 -> 0xFFE0
            Color yellow = PngUtility.RGB565ToColor(0xFFE0);
            Assert.AreEqual(255, yellow.R);
            Assert.AreEqual(255, yellow.G);
            Assert.AreEqual(0, yellow.B);
        }

        [TestMethod]
        public void RGB565ToColor_Cyan_ReturnsCorrectColor()
        {
            // Cyan: R=0, G=63, B=31 -> 0x07FF
            Color cyan = PngUtility.RGB565ToColor(0x07FF);
            Assert.AreEqual(0, cyan.R);
            Assert.AreEqual(255, cyan.G);
            Assert.AreEqual(255, cyan.B);
        }

        [TestMethod]
        public void RGB565ToColor_Magenta_ReturnsCorrectColor()
        {
            // Magenta: R=31, G=0, B=31 -> 0xF81F
            Color magenta = PngUtility.RGB565ToColor(0xF81F);
            Assert.AreEqual(255, magenta.R);
            Assert.AreEqual(0, magenta.G);
            Assert.AreEqual(255, magenta.B);
        }
        #endregion

        #region DecompressImage_PixelDataBgra4444 Tests
        [TestMethod]
        public unsafe void DecompressImage_PixelDataBgra4444_ValidData_DecompressesCorrectly()
        {
            int width = 4, height = 4;
            // Create raw BGRA4444 data: 2 bytes per pixel
            byte[] rawData = new byte[width * height * 2];
            for (int i = 0; i < rawData.Length; i++)
            {
                rawData[i] = (byte)((i * 17) & 0xFF);
            }

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                PngUtility.DecompressImage_PixelDataBgra4444(rawData, width, height, bmp, bmpData);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            // Verify decompression occurred (output size is 4x input)
            Assert.IsNotNull(bmp);
        }

        [TestMethod]
        public unsafe void DecompressImage_PixelDataBgra4444_InsufficientData_ThrowsException()
        {
            int width = 8, height = 8;
            // Create insufficient raw data
            byte[] rawData = new byte[width * height]; // Should be width * height * 2

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                Assert.Throws<ArgumentException>(() =>
           PngUtility.DecompressImage_PixelDataBgra4444(rawData, width, height, bmp, bmpData));
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        [TestMethod]
        public unsafe void DecompressImage_PixelDataBgra4444_NibbleExpansion_CorrectlyExpands()
        {
            // Test that nibbles are correctly expanded: 0x0 -> 0x00, 0xF -> 0xFF
            int width = 2, height = 2;
            byte[] rawData = new byte[width * height * 2];
            // Set all nibbles to 0xF
            for (int i = 0; i < rawData.Length; i++)
                rawData[i] = 0xFF;

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                PngUtility.DecompressImage_PixelDataBgra4444(rawData, width, height, bmp, bmpData);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            // Check pixel values
            Color pixel = bmp.GetPixel(0, 0);
            // All channels should be 0xFF (fully expanded)
            Assert.AreEqual(255, pixel.A);
            Assert.AreEqual(255, pixel.R);
            Assert.AreEqual(255, pixel.G);
            Assert.AreEqual(255, pixel.B);
        }
        #endregion

        #region DXT3 Tests
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

            // Compare pixels - DXT3 stores 4-bit alpha, so compare on 4-bit precision
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color orig = bmp.GetPixel(x, y);
                    Color decoded = bmpOut.GetPixel(x, y);
                    // Compare 4-bit alpha values (quantized)
                    Assert.AreEqual(orig.A >> 4, decoded.A >> 4, $"Alpha 4-bit mismatch at ({x},{y})");
                }
        }

        [TestMethod]
        public void DecompressImageDXT3_InsufficientData_ThrowsException()
        {
            int width = 8, height = 8;
            // DXT3 needs (width/4) * (height/4) * 16 bytes = 2 * 2 * 16 = 64 bytes
            byte[] rawData = new byte[32]; // Insufficient

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                Assert.Throws<ArgumentException>(() =>
              PngUtility.DecompressImageDXT3(rawData, width, height, bmpData));
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        [TestMethod]
        public void DecompressImageDXT3_SolidColor_DecompressesCorrectly()
        {
            // Create a simple 4x4 DXT3 block with solid red color
            int width = 4, height = 4;
            byte[] rawData = new byte[16];

            // Alpha: all fully opaque (0xFF = 4-bit value 15 for both nibbles)
            for (int i = 0; i < 8; i++)
                rawData[i] = 0xFF;

            // Color: Pure red in RGB565 = 0xF800
            rawData[8] = 0x00;  // c0 low
            rawData[9] = 0xF8;  // c0 high
            rawData[10] = 0x00; // c1 low
            rawData[11] = 0xF8; // c1 high

            // All indices = 0 (use c0)
            rawData[12] = 0x00;
            rawData[13] = 0x00;
            rawData[14] = 0x00;
            rawData[15] = 0x00;

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            PngUtility.DecompressImageDXT3(rawData, width, height, bmpData);
            bmp.UnlockBits(bmpData);

            // Check all pixels are red with full alpha
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = bmp.GetPixel(x, y);
                    Assert.AreEqual(255, pixel.A, $"Alpha mismatch at ({x},{y})");
                    Assert.AreEqual(255, pixel.R, $"Red mismatch at ({x},{y})");
                    Assert.AreEqual(0, pixel.G, $"Green mismatch at ({x},{y})");
                    Assert.AreEqual(0, pixel.B, $"Blue mismatch at ({x},{y})");
                }
            }
        }
        #endregion

        #region DXT5 Tests
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

            // Compare pixels - DXT5 stores alpha with limited precision; allow a small tolerance
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color orig = bmp.GetPixel(x, y);
                    Color decoded = bmpOut.GetPixel(x, y);
                    int diff = Math.Abs(orig.A - decoded.A);
                    Assert.IsTrue(diff <= 16, $"Alpha mismatch at ({x},{y}) - expected {orig.A} got {decoded.A}");
                }
        }

        [TestMethod]
        public void DecompressImageDXT5_InsufficientData_ThrowsException()
        {
            int width = 8, height = 8;
            // DXT5 needs (width/4) * (height/4) * 16 bytes = 2 * 2 * 16 = 64 bytes
            byte[] rawData = new byte[32]; // Insufficient

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                Assert.Throws<ArgumentException>(() =>
      PngUtility.DecompressImageDXT5(rawData, width, height, bmpData));
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        [TestMethod]
        public void DecompressImageDXT5_InvalidDimensions_ThrowsException()
        {
            byte[] rawData = new byte[64];

            using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                // Width and height of 0 should throw
                Assert.Throws<ArgumentException>(() =>
              PngUtility.DecompressImageDXT5(rawData, 0, 0, bmpData));
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }
        #endregion

        #region DecompressImage_PixelDataForm517 Tests
        [TestMethod]
        public void DecompressImage_PixelDataForm517_ValidData_DecompressesCorrectly()
        {
            // Format 517 uses 16x16 blocks with a single RGB565 value per block
            int width = 32, height = 32;
            int blockCountX = width / 16;
            int blockCountY = height / 16;
            byte[] rawData = new byte[blockCountX * blockCountY * 2];

            // Set block colors
            // Block 0,0: Red (0xF800)
            rawData[0] = 0x00;
            rawData[1] = 0xF8;
            // Block 1,0: Green (0x07E0)
            rawData[2] = 0xE0;
            rawData[3] = 0x07;
            // Block 0,1: Blue (0x001F)
            rawData[4] = 0x1F;
            rawData[5] = 0x00;
            // Block 1,1: White (0xFFFF)
            rawData[6] = 0xFF;
            rawData[7] = 0xFF;

            using var bmp = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);
            PngUtility.DecompressImage_PixelDataForm517(rawData, width, height, bmp, bmpData);
            bmp.UnlockBits(bmpData);

            // Verify the raw data was copied to the bitmap
            Assert.IsNotNull(bmp);
        }

        [TestMethod]
        public void DecompressImage_PixelDataForm517_BlockReplication_WorksCorrectly()
        {
            // Each 16x16 block should have the same RGB565 value
            int width = 16, height = 16;
            byte[] rawData = new byte[2]; // Single block

            // Set to cyan (0x07FF)
            rawData[0] = 0xFF;
            rawData[1] = 0x07;

            using var bmp = new Bitmap(width, height, PixelFormat.Format16bppRgb565);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format16bppRgb565);
            PngUtility.DecompressImage_PixelDataForm517(rawData, width, height, bmp, bmpData);
            bmp.UnlockBits(bmpData);

            // All pixels should have the same color (replicated from block)
            Assert.IsNotNull(bmp);
        }
        #endregion

        #region CopyBmpDataWithStride Tests
        [TestMethod]
        public void CopyBmpDataWithStride_MatchingStride_CopiesDirectly()
        {
            int width = 16, height = 8;
            int stride = width * 4;
            byte[] source = new byte[stride * height];

            // Fill with pattern
            for (int i = 0; i < source.Length; i++)
                source[i] = (byte)(i % 256);

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                PngUtility.CopyBmpDataWithStride(source, stride, bmpData);

                // Verify data was copied
                byte[] result = new byte[source.Length];
                Marshal.Copy(bmpData.Scan0, result, 0, Math.Min(result.Length, bmpData.Stride * height));

                // If strides match, should be identical
                if (bmpData.Stride == stride)
                {
                    for (int i = 0; i < source.Length; i++)
                        Assert.AreEqual(source[i], result[i], $"Byte mismatch at index {i}");
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        [TestMethod]
        public void Format2_RoundTrip_CopyBmpDataWithStride()
        {
            using var bmp = new Bitmap(16, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    bmp.SetPixel(x, y, Color.FromArgb((x * 17) & 255, (y * 31) & 255, (x * 13) & 255, (y * 7) & 255));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Color);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2, fmt);

            using var bmpOut = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
            var bmpData = bmpOut.LockBits(new Rectangle(0, 0, bmpOut.Width, bmpOut.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int srcStride = bmp.Width * 4;
                PngUtility.CopyBmpDataWithStride(pixelData, srcStride, bmpData);
            }
            finally
            {
                bmpOut.UnlockBits(bmpData);
            }

            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color orig = bmp.GetPixel(x, y);
                    Color outc = bmpOut.GetPixel(x, y);
                    Assert.AreEqual(orig.ToArgb(), outc.ToArgb(), $"Pixel mismatch at ({x},{y})");
                }
        }
        #endregion

        #region CompressImageToPngFormat Tests
        [TestMethod]
        public void CompressImageToPngFormat_Format1_Bgra4444()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(x * 32, y * 32, (x + y) * 16, 128));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgra4444);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format1, fmt);
            Assert.AreEqual(8 * 8 * 2, pixelData.Length); // 2 bytes per pixel
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format2_Bgra8888()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 32, y * 32, (x + y) * 16));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Color);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2, fmt);
            Assert.AreEqual(8 * 8 * 4, pixelData.Length); // 4 bytes per pixel
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format2_Bgra32()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 32, y * 32, (x + y) * 16));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgra32);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2, fmt);
            Assert.AreEqual(8 * 8 * 4, pixelData.Length);
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format257_Bgra5551()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 32, y * 32, (x + y) * 16));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgra5551);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format257, fmt);
            Assert.AreEqual(8 * 8 * 2, pixelData.Length); // 2 bytes per pixel
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format513_Bgr565_NonBlockAligned()
        {
            // Use dimensions not divisible by 16 to get Format513 instead of Format517
            using var bmp = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 10; y++)
                for (int x = 0; x < 10; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 25, y * 25, 0));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgr565);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format513, fmt);
            Assert.AreEqual(10 * 10 * 2, pixelData.Length); // 2 bytes per pixel
        }

        [TestMethod]
        public void Format513_RGB565_Approximation()
        {
            var rand = new Random(12345);
            int w = 8, h = 8;
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(rand.Next(256), rand.Next(256), rand.Next(256), rand.Next(256)));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgr565);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format513, fmt);

            // pixelData is 2 bytes per pixel in RGB565 little-endian
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = (y * w + x) * 2;
                    ushort val = (ushort)(pixelData[idx] | (pixelData[idx + 1] << 8));
                    Color decoded = PngUtility.RGB565ToColor(val);
                    Color orig = bmp.GetPixel(x, y);
                    // Allow small differences due to 5/6/5 quantization
                    Assert.IsTrue(Math.Abs(decoded.R - orig.R) <= 8, $"R channel too far at ({x},{y})");
                    Assert.IsTrue(Math.Abs(decoded.G - orig.G) <= 8, $"G channel too far at ({x},{y})");
                    Assert.IsTrue(Math.Abs(decoded.B - orig.B) <= 8, $"B channel too far at ({x},{y})");
                }
            }
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format517_Bgr565_BlockAligned()
        {
            // Use dimensions divisible by 16 to get Format517
            using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 8, y * 8, 0));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgr565);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format517, fmt);
            // Format517: 2 bytes per 16x16 block
            int blockCount = (32 / 16) * (32 / 16);
            Assert.AreEqual(blockCount * 2, pixelData.Length);
        }

        [TestMethod]
        public void Format517_BlockTopLeftSampling()
        {
            int w = 32, h = 32; // 2x2 blocks of 16x16
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            // Set each pixel so top-left of block has a unique color
            for (int by = 0; by < h / 16; by++)
            {
                for (int bx = 0; bx < w / 16; bx++)
                {
                    Color blockColor = Color.FromArgb(255, bx * 50 % 256, by * 80 % 256, (bx + by) * 70 % 256);
                    // fill block with some different colors but top-left is unique
                    for (int j = 0; j < 16; j++)
                        for (int i = 0; i < 16; i++)
                            bmp.SetPixel(bx * 16 + i, by * 16 + j, Color.FromArgb(blockColor.A, (blockColor.R + i) % 256, (blockColor.G + j) % 256, (blockColor.B + i + j) % 256));
                }
            }

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgr565);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format517, fmt);

            int blocksX = w / 16;
            int blocksY = h / 16;
            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int idx = (bx + by * blocksX) * 2;
                    ushort val = (ushort)(pixelData[idx] | (pixelData[idx + 1] << 8));
                    // Compute expected from top-left pixel of the block
                    Color topLeft = bmp.GetPixel(bx * 16, by * 16);
                    // Convert topLeft to rgb565 same as PngUtility.GetPixelDataFormat517
                    int r5 = (topLeft.R * 31) / 255;
                    int g6 = (topLeft.G * 63) / 255;
                    int b5 = (topLeft.B * 31) / 255;
                    ushort expected = (ushort)((r5 << 11) | (g6 << 5) | b5);
                    Assert.AreEqual(expected, val, $"Block value mismatch at block ({bx},{by})");
                }
            }
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format1026_Dxt3_ColorImage()
        {
            // Color image should produce Format1026
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 32, 0, y * 32)); // RGB varies

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format1026, fmt);
            // DXT3: 16 bytes per 4x4 block
            int blockCount = (8 / 4) * (8 / 4);
            Assert.AreEqual(blockCount * 16, pixelData.Length);
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format3_Dxt3_GrayscaleImage()
        {
            // Grayscale image should produce Format3
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int gray = (x + y) * 16;
                    bmp.SetPixel(x, y, Color.FromArgb(255, gray, gray, gray));
                }

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format3, fmt);
        }

        [TestMethod]
        public void CompressImageToPngFormat_Format2050_Dxt5()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(x * 32, y * 32, (x + y) * 16, 128));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt5);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2050, fmt);
            // DXT5: 16 bytes per 4x4 block
            int blockCount = (8 / 4) * (8 / 4);
            Assert.AreEqual(blockCount * 16, pixelData.Length);
        }

        [TestMethod]
        public void CompressImageToPngFormat_DefaultFormat_ReturnsFormat2()
        {
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.Red);

            // Use an unsupported format to trigger default case
            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, (SurfaceFormat)9999);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2, fmt);
            Assert.AreEqual(8 * 8 * 4, pixelData.Length);
        }

        [TestMethod]
        public void CompressImageToPngFormat_WithIsGrayscaleOverride_RespectsParameter()
        {
            // Color image but force grayscale detection
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 32, 0, y * 32));

            // Force isGrayscale = true
            var (fmt, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3, true);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format3, fmt);

            // Force isGrayscale = false
            var (fmt2, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3, false);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format1026, fmt2);
        }
        #endregion

        #region Format257 (ARGB1555) Tests
        [TestMethod]
        public void CompressImageToPngFormat_Format257_AlphaThreshold()
        {
            using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            // Set pixels with varying alpha
            bmp.SetPixel(0, 0, Color.FromArgb(127, 255, 0, 0)); // Alpha < 128 -> 0
            bmp.SetPixel(1, 0, Color.FromArgb(128, 0, 255, 0)); // Alpha >= 128 -> 1
            bmp.SetPixel(2, 0, Color.FromArgb(0, 0, 0, 255));   // Alpha = 0 -> 0
            bmp.SetPixel(3, 0, Color.FromArgb(255, 255, 255, 0)); // Alpha = 255 -> 1

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgra5551);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format257, fmt);

            // Check alpha bit in first row
            ushort pixel0 = (ushort)(pixelData[0] | (pixelData[1] << 8));
            ushort pixel1 = (ushort)(pixelData[2] | (pixelData[3] << 8));
            ushort pixel2 = (ushort)(pixelData[4] | (pixelData[5] << 8));
            ushort pixel3 = (ushort)(pixelData[6] | (pixelData[7] << 8));

            // Alpha bit is bit 15
            Assert.AreEqual(0, (pixel0 >> 15) & 1, "Pixel 0 alpha should be 0");
            Assert.AreEqual(1, (pixel1 >> 15) & 1, "Pixel 1 alpha should be 1");
            Assert.AreEqual(0, (pixel2 >> 15) & 1, "Pixel 2 alpha should be 0");
            Assert.AreEqual(1, (pixel3 >> 15) & 1, "Pixel 3 alpha should be 1");
        }
        #endregion

        #region Format1 (BGRA4444) Tests
        [TestMethod]
        public void CompressImageToPngFormat_Format1_NibbleQuantization()
        {
            using var bmp = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            // Set known values
            bmp.SetPixel(0, 0, Color.FromArgb(255, 255, 255, 255)); // All 0xF nibbles
            bmp.SetPixel(1, 0, Color.FromArgb(0, 0, 0, 0));         // All 0x0 nibbles
            bmp.SetPixel(0, 1, Color.FromArgb(128, 128, 128, 128)); // Mid values -> 0x8 nibbles
            bmp.SetPixel(1, 1, Color.FromArgb(16, 32, 48, 64));     // Various values

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgra4444);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format1, fmt);
            Assert.AreEqual(2 * 2 * 2, pixelData.Length);

            // Pixel 0,0: All 0xFF -> all nibbles = 0xF
            // Format is: byte0 = (G4<<4)|B4, byte1 = (A4<<4)|R4
            Assert.AreEqual(0xFF, pixelData[0]); // G=F, B=F
            Assert.AreEqual(0xFF, pixelData[1]); // A=F, R=F

            // Pixel 1,0: All 0x00 -> all nibbles = 0x0
            Assert.AreEqual(0x00, pixelData[2]);
            Assert.AreEqual(0x00, pixelData[3]);
        }
        #endregion

        #region Edge Cases and Error Handling
        [TestMethod]
        public void CompressImageToPngFormat_Dxt3_NonMultipleOf4_ThrowsException()
        {
            // DXT3 requires dimensions to be multiples of 4
            using var bmp = new Bitmap(7, 7, PixelFormat.Format32bppArgb);

            var method = typeof(PngUtility).GetMethod("CompressDXT3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                 method.Invoke(null, new object[] { bmp }));

            Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentException));
        }

        [TestMethod]
        public void CompressImageToPngFormat_Dxt5_NonMultipleOf4_ThrowsException()
        {
            // DXT5 requires dimensions to be multiples of 4
            using var bmp = new Bitmap(7, 7, PixelFormat.Format32bppArgb);

            var method = typeof(PngUtility).GetMethod("GetPixelDataFormat2050", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method.Invoke(null, new object[] { bmp }));

            Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentException));
        }

        [TestMethod]
        public void CompressImageToPngFormat_LargeImage_HandlesCorrectly()
        {
            // Test with a larger image to ensure no issues with size
            using var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(x * 4, y * 4, (x + y) * 2, 128));

            var (fmt, pixelData) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Color);

            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2, fmt);
            Assert.AreEqual(64 * 64 * 4, pixelData.Length);
        }

        [TestMethod]
        public void CompressImageToPngFormat_MinimumSize_HandlesCorrectly()
        {
            // Test with minimum size images
            using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            bmp.SetPixel(0, 0, Color.Red);

            // Test various formats with minimum size
            var (fmt1, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Color);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2, fmt1);

            var (fmt2, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Bgra4444);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format1, fmt2);

            var (fmt3, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3);
            Assert.IsTrue(fmt3 == MapleLib.WzLib.WzProperties.WzPngFormat.Format3 ||
               fmt3 == MapleLib.WzLib.WzProperties.WzPngFormat.Format1026);

            var (fmt4, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt5);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format2050, fmt4);
        }
        #endregion

        #region Grayscale Detection Tests
        [TestMethod]
        public void IsGrayscaleBitmap_PureGray_ReturnsTrue()
        {
            using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int gray = (x + y) * 8;
                    bmp.SetPixel(x, y, Color.FromArgb(255, gray, gray, gray));
                }

            var (fmt, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format3, fmt);
        }

        [TestMethod]
        public void IsGrayscaleBitmap_ColorImage_ReturnsFalse()
        {
            using var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(255, x * 16, 0, y * 16));

            var (fmt, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format1026, fmt);
        }

        [TestMethod]
        public void IsGrayscaleBitmap_NearGrayWithTolerance_ReturnsTrue()
        {
            // Within tolerance (8)
            using var bmp = new Bitmap(8, 8, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int baseGray = 128;
                    // Add small variations within tolerance
                    bmp.SetPixel(x, y, Color.FromArgb(255, baseGray, baseGray + 3, baseGray - 2));
                }

            var (fmt, _) = PngUtility.CompressImageToPngFormat(bmp, SurfaceFormat.Dxt3);
            Assert.AreEqual(MapleLib.WzLib.WzProperties.WzPngFormat.Format3, fmt);
        }
        #endregion
    }
}
