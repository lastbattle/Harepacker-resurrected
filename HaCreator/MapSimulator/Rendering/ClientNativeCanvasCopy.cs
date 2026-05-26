using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace HaCreator.MapSimulator.Rendering
{
    internal static class ClientNativeCanvasCopy
    {
        internal const int Alpha255 = 255;

        internal static CompositingMode CompositingMode => System.Drawing.Drawing2D.CompositingMode.SourceOver;
        internal static InterpolationMode InterpolationMode => System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        internal static PixelOffsetMode PixelOffsetMode => System.Drawing.Drawing2D.PixelOffsetMode.Half;
        internal static CompositingQuality CompositingQuality => System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        internal static SmoothingMode SmoothingMode => System.Drawing.Drawing2D.SmoothingMode.None;
        internal static GraphicsUnit PageUnit => GraphicsUnit.Pixel;
        internal static float PageScale => 1f;

        internal static void ApplySettings(Graphics graphics)
        {
            if (graphics == null)
            {
                return;
            }

            graphics.CompositingMode = CompositingMode;
            graphics.CompositingQuality = CompositingQuality;
            graphics.InterpolationMode = InterpolationMode;
            graphics.PixelOffsetMode = PixelOffsetMode;
            graphics.SmoothingMode = SmoothingMode;
            graphics.PageUnit = PageUnit;
            graphics.PageScale = PageScale;
        }

        internal static void CopyAlpha255(Bitmap destination, Bitmap source, int x, int y)
        {
            if (destination == null || source == null)
            {
                return;
            }

            int sourceX = Math.Max(0, -x);
            int sourceY = Math.Max(0, -y);
            int destinationX = Math.Max(0, x);
            int destinationY = Math.Max(0, y);
            int copyWidth = Math.Min(source.Width - sourceX, destination.Width - destinationX);
            int copyHeight = Math.Min(source.Height - sourceY, destination.Height - destinationY);
            if (copyWidth <= 0 || copyHeight <= 0)
            {
                return;
            }

            using Bitmap normalizedSource = EnsureArgbBitmap(source);
            Rectangle sourceRect = new(sourceX, sourceY, copyWidth, copyHeight);
            Rectangle destinationRect = new(destinationX, destinationY, copyWidth, copyHeight);
            BitmapData sourceData = null;
            BitmapData destinationData = null;

            try
            {
                sourceData = normalizedSource.LockBits(sourceRect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                destinationData = destination.LockBits(destinationRect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

                int sourceStride = Math.Abs(sourceData.Stride);
                int destinationStride = Math.Abs(destinationData.Stride);
                byte[] sourceBytes = new byte[sourceStride * copyHeight];
                byte[] destinationBytes = new byte[destinationStride * copyHeight];
                Marshal.Copy(sourceData.Scan0, sourceBytes, 0, sourceBytes.Length);
                Marshal.Copy(destinationData.Scan0, destinationBytes, 0, destinationBytes.Length);

                for (int row = 0; row < copyHeight; row++)
                {
                    int sourceRow = ResolveRowOffset(sourceData.Stride, sourceStride, copyHeight, row);
                    int destinationRow = ResolveRowOffset(destinationData.Stride, destinationStride, copyHeight, row);
                    for (int column = 0; column < copyWidth; column++)
                    {
                        int sourceIndex = sourceRow + (column * 4);
                        int destinationIndex = destinationRow + (column * 4);
                        BlendSourceOverAlpha255(sourceBytes, sourceIndex, destinationBytes, destinationIndex);
                    }
                }

                Marshal.Copy(destinationBytes, 0, destinationData.Scan0, destinationBytes.Length);
            }
            finally
            {
                if (sourceData != null)
                {
                    normalizedSource.UnlockBits(sourceData);
                }

                if (destinationData != null)
                {
                    destination.UnlockBits(destinationData);
                }
            }
        }

        internal static Color[] CopyAlpha255PixelsForTesting(
            Color[] destinationPixels,
            int destinationWidth,
            int destinationHeight,
            Color[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            int x,
            int y)
        {
            if (destinationPixels == null ||
                sourcePixels == null ||
                destinationWidth <= 0 ||
                destinationHeight <= 0 ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                destinationPixels.Length < destinationWidth * destinationHeight ||
                sourcePixels.Length < sourceWidth * sourceHeight)
            {
                return Array.Empty<Color>();
            }

            using Bitmap destination = CreateBitmapFromPixelsForTesting(
                destinationPixels,
                destinationWidth,
                destinationHeight);
            using Bitmap source = CreateBitmapFromPixelsForTesting(
                sourcePixels,
                sourceWidth,
                sourceHeight);
            CopyAlpha255(destination, source, x, y);
            return ReadBitmapPixelsForTesting(destination);
        }

        internal static NativeCopyCaptureComparison CompareAlpha255Capture(
            Color[] destinationPixels,
            int destinationWidth,
            int destinationHeight,
            Color[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            int x,
            int y,
            Color[] nativeCapturedPixels)
        {
            if (nativeCapturedPixels == null ||
                destinationWidth <= 0 ||
                destinationHeight <= 0 ||
                nativeCapturedPixels.Length < destinationWidth * destinationHeight)
            {
                return NativeCopyCaptureComparison.Invalid;
            }

            Color[] managedPixels = CopyAlpha255PixelsForTesting(
                destinationPixels,
                destinationWidth,
                destinationHeight,
                sourcePixels,
                sourceWidth,
                sourceHeight,
                x,
                y);
            if (managedPixels.Length == 0)
            {
                return NativeCopyCaptureComparison.Invalid;
            }

            int mismatchedPixels = 0;
            int maxChannelDelta = 0;
            int comparedPixels = Math.Min(managedPixels.Length, nativeCapturedPixels.Length);
            for (int i = 0; i < comparedPixels; i++)
            {
                Color managed = managedPixels[i];
                Color native = nativeCapturedPixels[i];
                int channelDelta = Math.Max(
                    Math.Max(Math.Abs(managed.A - native.A), Math.Abs(managed.R - native.R)),
                    Math.Max(Math.Abs(managed.G - native.G), Math.Abs(managed.B - native.B)));
                if (channelDelta != 0)
                {
                    mismatchedPixels++;
                    maxChannelDelta = Math.Max(maxChannelDelta, channelDelta);
                }
            }

            return new NativeCopyCaptureComparison(
                true,
                comparedPixels,
                mismatchedPixels,
                maxChannelDelta);
        }

        internal static NativeCopyCaptureComparison CompareAlpha255CaptureRegion(
            Color[] destinationPixels,
            int destinationWidth,
            int destinationHeight,
            Color[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            int x,
            int y,
            Color[] nativeCapturedPixels,
            int captureX,
            int captureY,
            int captureWidth,
            int captureHeight)
        {
            if (nativeCapturedPixels == null ||
                destinationWidth <= 0 ||
                destinationHeight <= 0 ||
                captureWidth <= 0 ||
                captureHeight <= 0 ||
                nativeCapturedPixels.Length < captureWidth * captureHeight)
            {
                return NativeCopyCaptureComparison.Invalid;
            }

            Color[] managedPixels = CopyAlpha255PixelsForTesting(
                destinationPixels,
                destinationWidth,
                destinationHeight,
                sourcePixels,
                sourceWidth,
                sourceHeight,
                x,
                y);
            if (managedPixels.Length == 0)
            {
                return NativeCopyCaptureComparison.Invalid;
            }

            int left = Math.Max(0, captureX);
            int top = Math.Max(0, captureY);
            int right = Math.Min(destinationWidth, captureX + captureWidth);
            int bottom = Math.Min(destinationHeight, captureY + captureHeight);
            if (right <= left || bottom <= top)
            {
                return NativeCopyCaptureComparison.Invalid;
            }

            int mismatchedPixels = 0;
            int maxChannelDelta = 0;
            int comparedPixels = 0;
            for (int pixelY = top; pixelY < bottom; pixelY++)
            {
                int nativeY = pixelY - captureY;
                for (int pixelX = left; pixelX < right; pixelX++)
                {
                    int nativeX = pixelX - captureX;
                    Color managed = managedPixels[pixelY * destinationWidth + pixelX];
                    Color native = nativeCapturedPixels[nativeY * captureWidth + nativeX];
                    int channelDelta = Math.Max(
                        Math.Max(Math.Abs(managed.A - native.A), Math.Abs(managed.R - native.R)),
                        Math.Max(Math.Abs(managed.G - native.G), Math.Abs(managed.B - native.B)));
                    if (channelDelta != 0)
                    {
                        mismatchedPixels++;
                        maxChannelDelta = Math.Max(maxChannelDelta, channelDelta);
                    }

                    comparedPixels++;
                }
            }

            return new NativeCopyCaptureComparison(
                true,
                comparedPixels,
                mismatchedPixels,
                maxChannelDelta);
        }

        private static Bitmap CreateBitmapFromPixelsForTesting(Color[] pixels, int width, int height)
        {
            Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            for (int pixelY = 0; pixelY < height; pixelY++)
            {
                for (int pixelX = 0; pixelX < width; pixelX++)
                {
                    bitmap.SetPixel(pixelX, pixelY, pixels[pixelY * width + pixelX]);
                }
            }

            return bitmap;
        }

        private static Color[] ReadBitmapPixelsForTesting(Bitmap bitmap)
        {
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return Array.Empty<Color>();
            }

            Color[] pixels = new Color[bitmap.Width * bitmap.Height];
            for (int pixelY = 0; pixelY < bitmap.Height; pixelY++)
            {
                for (int pixelX = 0; pixelX < bitmap.Width; pixelX++)
                {
                    pixels[pixelY * bitmap.Width + pixelX] = bitmap.GetPixel(pixelX, pixelY);
                }
            }

            return pixels;
        }

        internal static Color BlendAlpha255(Color destination, Color source)
        {
            if (source.A == 0)
            {
                return destination;
            }

            if (source.A == Alpha255)
            {
                return source;
            }

            using Bitmap destinationBitmap = new(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using Bitmap sourceBitmap = new(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            destinationBitmap.SetPixel(0, 0, destination);
            sourceBitmap.SetPixel(0, 0, source);
            CopyAlpha255(destinationBitmap, sourceBitmap, 0, 0);
            return destinationBitmap.GetPixel(0, 0);
        }

        internal static Color BlendAlpha255(params Color[] layers)
        {
            Color result = Color.Transparent;
            if (layers == null)
            {
                return result;
            }

            foreach (Color layer in layers)
            {
                result = BlendAlpha255(result, layer);
            }

            return result;
        }

        private static Bitmap EnsureArgbBitmap(Bitmap source)
        {
            if (source.PixelFormat == PixelFormat.Format32bppArgb)
            {
                return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
            }

            Bitmap normalized = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(normalized);
            ApplySettings(graphics);
            graphics.DrawImageUnscaled(source, 0, 0);
            return normalized;
        }

        private static int ResolveRowOffset(int stride, int absoluteStride, int height, int row)
        {
            return stride >= 0
                ? row * absoluteStride
                : (height - row - 1) * absoluteStride;
        }

        private static void BlendSourceOverAlpha255(byte[] sourceBytes, int sourceIndex, byte[] destinationBytes, int destinationIndex)
        {
            int sourceAlpha = sourceBytes[sourceIndex + 3];
            if (sourceAlpha == 0)
            {
                return;
            }

            if (sourceAlpha == Alpha255)
            {
                destinationBytes[destinationIndex] = sourceBytes[sourceIndex];
                destinationBytes[destinationIndex + 1] = sourceBytes[sourceIndex + 1];
                destinationBytes[destinationIndex + 2] = sourceBytes[sourceIndex + 2];
                destinationBytes[destinationIndex + 3] = Alpha255;
                return;
            }

            int destinationAlpha = destinationBytes[destinationIndex + 3];
            int inverseSourceAlpha = Alpha255 - sourceAlpha;
            int outputAlpha = sourceAlpha + Divide255Round(destinationAlpha * inverseSourceAlpha);

            destinationBytes[destinationIndex] = BlendChannel(
                destinationBytes[destinationIndex],
                destinationAlpha,
                sourceBytes[sourceIndex],
                sourceAlpha,
                inverseSourceAlpha,
                outputAlpha);
            destinationBytes[destinationIndex + 1] = BlendChannel(
                destinationBytes[destinationIndex + 1],
                destinationAlpha,
                sourceBytes[sourceIndex + 1],
                sourceAlpha,
                inverseSourceAlpha,
                outputAlpha);
            destinationBytes[destinationIndex + 2] = BlendChannel(
                destinationBytes[destinationIndex + 2],
                destinationAlpha,
                sourceBytes[sourceIndex + 2],
                sourceAlpha,
                inverseSourceAlpha,
                outputAlpha);
            destinationBytes[destinationIndex + 3] = (byte)Math.Min(Alpha255, outputAlpha);
        }

        private static byte BlendChannel(
            int destinationChannel,
            int destinationAlpha,
            int sourceChannel,
            int sourceAlpha,
            int inverseSourceAlpha,
            int outputAlpha)
        {
            if (outputAlpha <= 0)
            {
                return 0;
            }

            int sourcePremultiplied = Divide255Round(sourceChannel * sourceAlpha);
            int destinationPremultiplied = Divide255Round(destinationChannel * destinationAlpha);
            int outputPremultiplied = sourcePremultiplied + Divide255Round(destinationPremultiplied * inverseSourceAlpha);
            return (byte)Math.Min(Alpha255, Math.Max(0, outputPremultiplied * Alpha255 / outputAlpha));
        }

        private static int Divide255Round(int value)
        {
            return (value + 127) / Alpha255;
        }
    }

    internal readonly record struct NativeCopyCaptureComparison(
        bool IsValid,
        int ComparedPixels,
        int MismatchedPixels,
        int MaxChannelDelta)
    {
        internal static NativeCopyCaptureComparison Invalid => new(false, 0, 0, 0);

        internal bool IsExactMatch => IsValid && ComparedPixels > 0 && MismatchedPixels == 0 && MaxChannelDelta == 0;
    }
}
