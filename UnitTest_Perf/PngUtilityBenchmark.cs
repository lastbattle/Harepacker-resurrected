using BenchmarkDotNet.Attributes;
using MapleLib.Helpers;
using System.Drawing;
using System.Drawing.Imaging;

namespace UnitTest_Perf
{
    public class PngUtilityBenchmark
    {
        private Bitmap bmp;
        private byte[] dxt3Data;
        private byte[] dxt5Data;

        [GlobalSetup]
        public void Setup()
        {
            bmp = new Bitmap(128, 128, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                    bmp.SetPixel(x, y, Color.FromArgb(128 + x % 128, x * 8 % 256, y * 8 % 256, (x + y) % 256));
            dxt3Data = (byte[])typeof(PngUtility).GetMethod("CompressDXT3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
            dxt5Data = (byte[])typeof(PngUtility).GetMethod("GetPixelDataFormat2050", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
        }

        [Benchmark(Description = "DXT3 Decode 128x128")]
        public byte DXT3_Decode()
        {
            using var bmpOut = new Bitmap(128, 128, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmpOut.LockBits(new Rectangle(0, 0, 128, 128), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            PngUtility.DecompressImageDXT3(dxt3Data, 128, 128, bmpData);
            bmpOut.UnlockBits(bmpData);
            // Return a pixel value to prevent dead code elimination
            return bmpOut.GetPixel(0, 0).A;
        }

        [Benchmark(Description = "DXT5 Decode 128x128")]
        public byte DXT5_Decode()
        {
            using var bmpOut = new Bitmap(128, 128, PixelFormat.Format32bppArgb);
            BitmapData bmpData = bmpOut.LockBits(new Rectangle(0, 0, 128, 128), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            PngUtility.DecompressImageDXT5(dxt5Data, 128, 128, bmpData);
            bmpOut.UnlockBits(bmpData);
            return bmpOut.GetPixel(0, 0).A;
        }

        [Benchmark(Description = "DXT3 Encode 128x128")]
        public byte DXT3_Encode()
        {
            byte[] encoded = (byte[])typeof(PngUtility).GetMethod("CompressDXT3", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
            return encoded[0];
        }

        [Benchmark(Description = "DXT5 Encode 128x128")]
        public byte DXT5_Encode()
        {
            byte[] encoded = (byte[])typeof(PngUtility).GetMethod("GetPixelDataFormat2050", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bmp });
            return encoded[0];
        }
    }
}
