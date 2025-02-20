using MapleLib;
using MapleLib.ClientLib;
using MapleLib.Helpers;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace UnitTest_WzFile {
    [TestClass]
    public class UnitTest_MapleLib {


        public UnitTest_MapleLib() {
        }

        /// <summary>
        /// Test CCrc32::GetCrc32 calculation
        /// </summary>
        [TestMethod]
        [SupportedOSPlatform("windows")]
        public void TestCrcCalculation() {
            int useVersion = 200;

            uint crc_firstRun = CCrc32.GetCrc32(useVersion, 0, false, false);
            Assert.IsTrue(crc_firstRun == 2384409922, "Expected value = (2,384,409,922), got {0}", crc_firstRun.ToString());

            uint crc = CWvsPhysicalSpace2D.GetConstantCRC(useVersion);
            Assert.IsTrue(crc == 1696968404, "Expected value = (crc = 1,696,968,404), got {0}", crc.ToString());
        }

        /// <summary>
        /// Bgra32 (Small Icon): 27x30 pixels, with alpha, few colors. Represents a typical small icon that needs alpha channel preservation.
        /// Bgr565(Small Icon) : 27x30 pixels, no alpha, moderate color count.Suitable for small images without alpha that need decent color representation.
        /// Bgra4444(Small Icon): 27x30 pixels, with alpha, few colors.Good for small icons with simple color schemes that still need alpha.
        /// DXT5 (Large Texture): 128x128 pixels, with alpha, gradients, many colors. Ideal for larger textures with smooth alpha transitions.
        /// DXT3 (Large Texture): 128x128 pixels, with alpha, no gradients, moderate colors. Suitable for larger textures with sharp alpha transitions.
        /// Bgra32 (Large Texture): 128x128 pixels, no alpha, gradients, many colors. Used for larger textures that need full color preservation without alpha.
        /// </summary>
        [TestMethod]
        [SupportedOSPlatform("windows")]
        public void TestImageSurfaceFormatDetection() {
            string[] imageFiles = Directory.GetFiles("Assets/Images", "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => new[] { ".png", ".jpg", ".bmp" }.Contains(Path.GetExtension(file).ToLower()))
            .ToArray();

            Assert.IsTrue(imageFiles.Length > 0, "No image files found in the Assets/Images folder.");

            foreach (string imagePath in imageFiles) {
                using (Bitmap bitmap = new Bitmap(imagePath)) {
                    // get image
                    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                    int byteCount = bmpData.Stride * bitmap.Height;
                    byte[] argbData = new byte[byteCount];
                    System.Runtime.InteropServices.Marshal.Copy(bmpData.Scan0, argbData, 0, byteCount);

                    bitmap.UnlockBits(bmpData);

                    int width = bitmap.Width;
                    int height = bitmap.Height;

                    SurfaceFormat detectedFormat = ImageFormatDetector.DetermineTextureFormat(argbData, width, height);
                    var (uniqueColors, hasAlpha, hasPartialAlpha, maxAlpha, alphaTransitions, alphaVariance) = ImageFormatDetector.AnalyzeImageData(argbData);
                    bool isDxtCompressionCandidate = ImageFormatDetector.IsDxtCompressionCandidate(width, height);

                    Debug.WriteLine($"Image: {Path.GetFileName(imagePath)}");
                    Debug.WriteLine($"Dimensions: {width}x{height}");
                    Debug.WriteLine($"Total pixels: {width * height}");
                    Debug.WriteLine($"Has Alpha: {hasAlpha}");
                    Debug.WriteLine($"Has Partial Alpha: {hasPartialAlpha}");
                    Debug.WriteLine($"Unique Color: {uniqueColors}");
                    Debug.WriteLine($"Max Alpha: {maxAlpha}");
                    Debug.WriteLine($"Alpha Transitions: {alphaTransitions}");
                    Debug.WriteLine($"Alpha Variance: {alphaVariance}");
                    Debug.WriteLine($"IsDxtCompressionCandidate: {isDxtCompressionCandidate}");
                    Debug.WriteLine($"Detected Format: {detectedFormat}");

                    SurfaceFormat expectedFormat = GetExpectedFormat(imagePath);

                    Debug.WriteLine($"Expected: {expectedFormat.ToString()}");
                    Debug.WriteLine("");

                    Assert.AreEqual(expectedFormat, detectedFormat,
                        $"Incorrect format detected for {Path.GetFileName(imagePath)}. " +
                        $"Expected: {expectedFormat}, Detected: {detectedFormat}");
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private SurfaceFormat GetExpectedFormat(string imagePath) {
            // This is a placeholder. You should replace this with actual logic to determine
            // the expected format based on the image file name or properties.
            string fileName = Path.GetFileNameWithoutExtension(imagePath).ToLower();
            if (fileName.StartsWith("dxt5")) {
                return SurfaceFormat.Dxt5;
            }
            else if (fileName.StartsWith("dxt3")) {
                return SurfaceFormat.Dxt3;
            }
            else if (fileName.StartsWith("bgra32")) {
                return SurfaceFormat.Bgr32;
            }
            else if (fileName.StartsWith("bgr565")) {
                return SurfaceFormat.Bgr565;
            } 
            else if (fileName.StartsWith("bgra4444")) 
                return SurfaceFormat.Bgra4444;

            // Default case
            return SurfaceFormat.Color;
        }

    }
}
