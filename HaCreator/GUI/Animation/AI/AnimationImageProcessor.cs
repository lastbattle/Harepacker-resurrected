#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HaCreator.GUI.FrameAnimation.AI
{
    public sealed class AnimationImageProcessingOptions
    {
        public bool RemoveEdgeBackground { get; set; } = true;
        public bool TrimTransparentPixels { get; set; } = true;
        public int BackgroundColorTolerance { get; set; } = 28;
        public byte AlphaThreshold { get; set; } = 4;
        public bool MatchReferenceScale { get; set; } = true;
        public int MaxOutputDimension { get; set; } = 512;
        public int TransparentPadding { get; set; }
    }

    public sealed class ProcessedAnimationImage : IDisposable
    {
        public ProcessedAnimationImage(Bitmap bitmap, Point origin, Rectangle originalAlphaBounds)
        {
            Bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
            Origin = origin;
            OriginalAlphaBounds = originalAlphaBounds;
        }

        public Bitmap Bitmap { get; }
        public Point Origin { get; }
        public Rectangle OriginalAlphaBounds { get; }
        public void Dispose() => Bitmap.Dispose();
    }

    /// <summary>
    /// Local post-processing for generated sprite frames. Background removal is edge-connected,
    /// so colors enclosed by the sprite are not erased merely because they match the matte.
    /// </summary>
    public static class AnimationImageProcessor
    {
        public static Bitmap RemoveBackground(Bitmap source, int colorTolerance = 28, byte alphaThreshold = 4)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            colorTolerance = Math.Clamp(colorTolerance, 0, 441);

            Bitmap result = CopyArgb(source);
            int width = result.Width;
            int height = result.Height;
            int[] pixels = ReadPixels(result);
            int background = DetectBorderBackground(pixels, width, height, alphaThreshold);
            var visited = new bool[pixels.Length];
            var queue = new Queue<int>();

            void EnqueueIfBackground(int x, int y)
            {
                int index = y * width + x;
                if (visited[index])
                    return;
                visited[index] = true;
                if (IsBackground(pixels[index], background, colorTolerance, alphaThreshold))
                    queue.Enqueue(index);
            }

            for (int x = 0; x < width; x++)
            {
                EnqueueIfBackground(x, 0);
                if (height > 1) EnqueueIfBackground(x, height - 1);
            }
            for (int y = 1; y < height - 1; y++)
            {
                EnqueueIfBackground(0, y);
                if (width > 1) EnqueueIfBackground(width - 1, y);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                pixels[index] = 0;
                int x = index % width;
                int y = index / width;
                if (x > 0) EnqueueIfBackground(x - 1, y);
                if (x + 1 < width) EnqueueIfBackground(x + 1, y);
                if (y > 0) EnqueueIfBackground(x, y - 1);
                if (y + 1 < height) EnqueueIfBackground(x, y + 1);
            }

            if (GetSaturation(background) >= 0.35)
                RemoveChromaFringe(pixels, width, height, background, alphaThreshold, passes: 2);

            ClearHiddenRgb(pixels, alphaThreshold);

            WritePixels(result, pixels);
            return result;
        }

        public static Rectangle? FindAlphaBounds(Bitmap source, byte alphaThreshold = 4)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            using Bitmap bitmap = CopyArgb(source);
            int[] pixels = ReadPixels(bitmap);
            int left = bitmap.Width;
            int top = bitmap.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                int alpha = (pixels[y * bitmap.Width + x] >> 24) & 0xFF;
                if (alpha <= alphaThreshold)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
            return right < left ? null : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        public static Point CalculateAlignedOrigin(Bitmap generated, Bitmap? reference, Point referenceOrigin,
            byte alphaThreshold = 4)
        {
            if (generated == null)
                throw new ArgumentNullException(nameof(generated));
            Rectangle generatedBounds = FindAlphaBounds(generated, alphaThreshold) ??
                new Rectangle(0, 0, generated.Width, generated.Height);
            Point generatedAnchor = BottomCenter(generatedBounds);

            if (reference == null)
                return generatedAnchor;
            Rectangle referenceBounds = FindAlphaBounds(reference, alphaThreshold) ??
                new Rectangle(0, 0, reference.Width, reference.Height);
            Point referenceAnchor = BottomCenter(referenceBounds);
            return new Point(
                generatedAnchor.X - referenceAnchor.X + referenceOrigin.X,
                generatedAnchor.Y - referenceAnchor.Y + referenceOrigin.Y);
        }

        public static ProcessedAnimationImage Process(Bitmap generated, Bitmap? reference = null,
            Point? referenceOrigin = null, AnimationImageProcessingOptions? options = null)
        {
            if (generated == null)
                throw new ArgumentNullException(nameof(generated));
            options ??= new AnimationImageProcessingOptions();

            Bitmap working = options.RemoveEdgeBackground
                ? RemoveBackground(generated, options.BackgroundColorTolerance, options.AlphaThreshold)
                : CopyArgb(generated);
            Rectangle bounds = FindAlphaBounds(working, options.AlphaThreshold) ??
                new Rectangle(0, 0, working.Width, working.Height);
            Rectangle originalBounds = bounds;

            if (options.TrimTransparentPixels &&
                (bounds.X != 0 || bounds.Y != 0 || bounds.Width != working.Width || bounds.Height != working.Height))
            {
                Bitmap trimmed = working.Clone(bounds, PixelFormat.Format32bppArgb);
                working.Dispose();
                working = trimmed;
            }

            double scale = 1;
            if (reference != null && options.MatchReferenceScale)
            {
                Rectangle referenceBounds = FindAlphaBounds(reference, options.AlphaThreshold) ??
                    new Rectangle(0, 0, reference.Width, reference.Height);
                Rectangle generatedBounds = FindAlphaBounds(working, options.AlphaThreshold) ??
                    new Rectangle(0, 0, working.Width, working.Height);
                if (generatedBounds.Width > 0 && generatedBounds.Height > 0 &&
                    referenceBounds.Width > 0 && referenceBounds.Height > 0)
                {
                    scale = Math.Min(
                        (double)referenceBounds.Width / generatedBounds.Width,
                        (double)referenceBounds.Height / generatedBounds.Height);
                }
            }

            if (options.MaxOutputDimension > 0)
            {
                double scaledMaximum = Math.Max(working.Width * scale, working.Height * scale);
                if (scaledMaximum > options.MaxOutputDimension)
                    scale *= options.MaxOutputDimension / scaledMaximum;
            }

            if (scale > 0 && Math.Abs(scale - 1) > 0.001)
            {
                Bitmap resized = Resize(working,
                    Math.Max(1, (int)Math.Round(working.Width * scale)),
                    Math.Max(1, (int)Math.Round(working.Height * scale)));
                working.Dispose();
                working = resized;
            }

            if (options.TransparentPadding > 0)
            {
                Bitmap padded = AddTransparentPadding(working, options.TransparentPadding);
                working.Dispose();
                working = padded;
            }

            ClearHiddenRgb(working, options.AlphaThreshold);

            Point origin = CalculateAlignedOrigin(working, reference, referenceOrigin ?? Point.Empty,
                options.AlphaThreshold);
            return new ProcessedAnimationImage(working, origin, originalBounds);
        }

        private static Bitmap Resize(Bitmap source, int width, int height)
        {
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(result);
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height),
                new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
            return result;
        }

        private static Bitmap AddTransparentPadding(Bitmap source, int padding)
        {
            padding = Math.Max(0, padding);
            var result = new Bitmap(source.Width + padding * 2, source.Height + padding * 2,
                PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(result);
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(source, padding, padding);
            return result;
        }

        private static Point BottomCenter(Rectangle bounds) =>
            new(bounds.Left + bounds.Width / 2, bounds.Bottom - 1);

        private static int DetectBorderBackground(int[] pixels, int width, int height, byte alphaThreshold)
        {
            var buckets = new Dictionary<int, (int count, long r, long g, long b)>();
            void Add(int pixel)
            {
                int alpha = (pixel >> 24) & 0xFF;
                if (alpha <= alphaThreshold)
                    return;
                int r = (pixel >> 16) & 0xFF;
                int g = (pixel >> 8) & 0xFF;
                int b = pixel & 0xFF;
                int key = (r >> 4) << 8 | (g >> 4) << 4 | (b >> 4);
                buckets.TryGetValue(key, out var bucket);
                buckets[key] = (bucket.count + 1, bucket.r + r, bucket.g + g, bucket.b + b);
            }

            for (int x = 0; x < width; x++)
            {
                Add(pixels[x]);
                if (height > 1) Add(pixels[(height - 1) * width + x]);
            }
            for (int y = 1; y < height - 1; y++)
            {
                Add(pixels[y * width]);
                if (width > 1) Add(pixels[y * width + width - 1]);
            }

            if (buckets.Count == 0)
                return 0;
            var best = default((int count, long r, long g, long b));
            foreach (var bucket in buckets.Values)
                if (bucket.count > best.count) best = bucket;
            int divisor = Math.Max(1, best.count);
            return unchecked((int)0xFF000000) |
                ((int)(best.r / divisor) << 16) |
                ((int)(best.g / divisor) << 8) |
                (int)(best.b / divisor);
        }

        private static bool IsBackground(int pixel, int background, int tolerance, byte alphaThreshold)
        {
            int alpha = (pixel >> 24) & 0xFF;
            if (alpha <= alphaThreshold)
                return true;
            int dr = ((pixel >> 16) & 0xFF) - ((background >> 16) & 0xFF);
            int dg = ((pixel >> 8) & 0xFF) - ((background >> 8) & 0xFF);
            int db = (pixel & 0xFF) - (background & 0xFF);
            return dr * dr + dg * dg + db * db <= tolerance * tolerance;
        }

        private static bool IsChromaLike(int pixel, int background, byte alphaThreshold)
        {
            int alpha = (pixel >> 24) & 0xFF;
            if (alpha <= alphaThreshold || GetSaturation(pixel) < 0.35)
                return alpha <= alphaThreshold;
            double difference = Math.Abs(GetHue(pixel) - GetHue(background));
            difference = Math.Min(difference, 360 - difference);
            return difference <= 45;
        }

        private static void RemoveChromaFringe(int[] pixels, int width, int height, int background,
            byte alphaThreshold, int passes)
        {
            var remove = new bool[pixels.Length];
            for (int pass = 0; pass < Math.Max(0, passes); pass++)
            {
                bool found = false;
                Array.Clear(remove, 0, remove.Length);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (!IsChromaLike(pixels[index], background, alphaThreshold) ||
                        !TouchesTransparentPixel(pixels, width, height, x, y, alphaThreshold))
                        continue;
                    remove[index] = true;
                    found = true;
                }

                if (!found)
                    break;
                for (int index = 0; index < pixels.Length; index++)
                    if (remove[index]) pixels[index] = 0;
            }
        }

        private static bool TouchesTransparentPixel(int[] pixels, int width, int height, int x, int y,
            byte alphaThreshold)
        {
            int left = Math.Max(0, x - 1);
            int right = Math.Min(width - 1, x + 1);
            int top = Math.Max(0, y - 1);
            int bottom = Math.Min(height - 1, y + 1);
            for (int adjacentY = top; adjacentY <= bottom; adjacentY++)
            for (int adjacentX = left; adjacentX <= right; adjacentX++)
            {
                if (adjacentX == x && adjacentY == y)
                    continue;
                int alpha = (pixels[adjacentY * width + adjacentX] >> 24) & 0xFF;
                if (alpha <= alphaThreshold)
                    return true;
            }
            return false;
        }

        private static void ClearHiddenRgb(Bitmap bitmap, byte alphaThreshold)
        {
            int[] pixels = ReadPixels(bitmap);
            ClearHiddenRgb(pixels, alphaThreshold);
            WritePixels(bitmap, pixels);
        }

        private static void ClearHiddenRgb(int[] pixels, byte alphaThreshold)
        {
            for (int index = 0; index < pixels.Length; index++)
                if (((pixels[index] >> 24) & 0xFF) <= alphaThreshold)
                    pixels[index] = 0;
        }

        private static double GetSaturation(int pixel)
        {
            double r = ((pixel >> 16) & 0xFF) / 255d;
            double g = ((pixel >> 8) & 0xFF) / 255d;
            double b = (pixel & 0xFF) / 255d;
            double maximum = Math.Max(r, Math.Max(g, b));
            double minimum = Math.Min(r, Math.Min(g, b));
            return maximum <= 0 ? 0 : (maximum - minimum) / maximum;
        }

        private static double GetHue(int pixel)
        {
            int r = (pixel >> 16) & 0xFF;
            int g = (pixel >> 8) & 0xFF;
            int b = pixel & 0xFF;
            return Color.FromArgb(r, g, b).GetHue();
        }

        private static Bitmap CopyArgb(Bitmap source)
        {
            var result = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            result.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            using Graphics graphics = Graphics.FromImage(result);
            graphics.DrawImageUnscaled(source, 0, 0);
            return result;
        }

        private static int[] ReadPixels(Bitmap bitmap)
        {
            var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new int[bitmap.Width * bitmap.Height];
                if (data.Stride == bitmap.Width * 4)
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                else
                    for (int y = 0; y < bitmap.Height; y++)
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * bitmap.Width, bitmap.Width);
                return pixels;
            }
            finally { bitmap.UnlockBits(data); }
        }

        private static void WritePixels(Bitmap bitmap, int[] pixels)
        {
            var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                if (data.Stride == bitmap.Width * 4)
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                else
                    for (int y = 0; y < bitmap.Height; y++)
                        Marshal.Copy(pixels, y * bitmap.Width, IntPtr.Add(data.Scan0, y * data.Stride), bitmap.Width);
            }
            finally { bitmap.UnlockBits(data); }
        }
    }
}
