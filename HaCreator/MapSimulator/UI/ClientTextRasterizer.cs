using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SD = System.Drawing;
using SDImaging = System.Drawing.Imaging;
using SDText = System.Drawing.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SWF = System.Windows.Forms;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class ClientTextRasterizer
    {
        private const int RasterPadding = 2;
        private const byte KoreanGdiCharset = 129;
        private const string ClientTextFontPathEnvironmentVariable = "MAPSIM_CLIENT_TEXT_FONT_PATH";
        private const string ClientTextFontFaceEnvironmentVariable = "MAPSIM_CLIENT_TEXT_FONT_FACE";
        private static readonly string[] DefaultPrivateFontFileCandidates =
        {
            "DotumChe.ttf",
            "Dotum.ttf",
            "dotum.ttc",
            "GulimChe.ttf",
            "Gulim.ttf",
            "gulim.ttc",
            "batang.ttc"
        };
        private static readonly string[] DefaultPrivateFontFiles =
        {
            "dotum.ttc",
            "gulim.ttc"
        };
        private static readonly string[] DefaultPrivateFontFamilyCandidates =
        {
            "DotumChe",
            "Dotum",
            "DOTOOMCHE",
            "DOTOOM",
            "DODUMCHE",
            "DODUM",
            "돋움체",
            "돋움",
            "GulimChe",
            "Gulim",
            "굴림체",
            "굴림",
            "BatangChe",
            "Batang",
            "바탕체",
            "바탕"
        };
        private static readonly string[] DefaultPrivateFontSearchDirectorySuffixes =
        {
            string.Empty,
            "Fonts",
            "Content",
            Path.Combine("Content", "Fonts")
        };
        private static readonly string[] DefaultFontFamilyCandidates =
        {
            "DotumChe",
            "Dotum",
            "DOTOOM",
            "DOTOOMCHE",
            "DODUM",
            "DODUMCHE",
            "돋움체",
            "돋움",
            "GulimChe",
            "Gulim",
            "굴림체",
            "굴림",
            "BatangChe",
            "Batang",
            "・被ヵ・ｴ",
            "・被ヵ",
            "Tahoma",
            SD.SystemFonts.MessageBoxFont?.FontFamily?.Name,
            SD.FontFamily.GenericSansSerif.Name
        };
        private const SWF.TextFormatFlags ClientTextFormatFlags =
            SWF.TextFormatFlags.NoPadding |
            SWF.TextFormatFlags.NoPrefix |
            SWF.TextFormatFlags.PreserveGraphicsClipping |
            SWF.TextFormatFlags.PreserveGraphicsTranslateTransform |
            SWF.TextFormatFlags.SingleLine;

        private readonly GraphicsDevice _graphicsDevice;
        private readonly string _fontFamily;
        private readonly float _basePointSize;
        private readonly SD.FontStyle _fontStyle;
        private readonly Dictionary<string, RasterTextTexture> _textureCache = new Dictionary<string, RasterTextTexture>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _measureCache = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Dictionary<int, SD.Font> _fontCache = new Dictionary<int, SD.Font>();
        private readonly SD.Bitmap _measureBitmap = new SD.Bitmap(1, 1, SDImaging.PixelFormat.Format32bppArgb);
        private readonly SD.Graphics _measureGraphics;

        public ClientTextRasterizer(GraphicsDevice graphicsDevice, string fontFamily = null, float basePointSize = 12f, SD.FontStyle fontStyle = SD.FontStyle.Regular)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _fontFamily = ResolvePreferredFontFamily(fontFamily);
            _basePointSize = basePointSize <= 0f ? 12f : basePointSize;
            _fontStyle = fontStyle;

            _measureGraphics = SD.Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            _measureGraphics.PageUnit = SD.GraphicsUnit.Pixel;
        }

        internal static string ResolvePreferredFontFamily(string requestedFamily = null)
        {
            return ResolveFontFamily(requestedFamily);
        }

        internal static SD.Font CreateClientFont(float pixelSize, SD.FontStyle style = SD.FontStyle.Regular, string requestedFamily = null)
        {
            string resolvedFamily = ResolvePreferredFontFamily(requestedFamily);
            float normalizedSize = pixelSize <= 0f ? 12f : pixelSize;

            try
            {
                return new SD.Font(resolvedFamily, normalizedSize, style, SD.GraphicsUnit.Pixel, KoreanGdiCharset);
            }
            catch (ArgumentException)
            {
                return new SD.Font(resolvedFamily, normalizedSize, style, SD.GraphicsUnit.Pixel);
            }
        }

        public Vector2 MeasureString(string text, float scale = 1.0f)
        {
            string normalizedText = text ?? string.Empty;
            if (normalizedText.Length == 0)
            {
                return Vector2.Zero;
            }

            string cacheKey = BuildCacheKey(normalizedText, scale);
            if (_measureCache.TryGetValue(cacheKey, out Vector2 cachedSize))
            {
                return cachedSize;
            }

            RasterTextTexture rasterText = GetOrCreateRasterText(normalizedText, scale, Color.White);
            Vector2 measuredSize = rasterText.Measurement;
            _measureCache[cacheKey] = measuredSize;
            return measuredSize;
        }

        public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 1.0f, float? maxWidth = null)
        {
            if (spriteBatch == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            RasterTextTexture rasterText = GetOrCreateRasterText(text, scale, color);
            if (rasterText.Texture == null)
            {
                return;
            }

            Vector2 drawPosition = new Vector2(position.X + rasterText.OffsetX, position.Y + rasterText.OffsetY);
            if (maxWidth.HasValue)
            {
                int sourceWidth = (int)Math.Floor((position.X + maxWidth.Value) - drawPosition.X);
                if (sourceWidth <= 0)
                {
                    return;
                }

                if (sourceWidth < rasterText.Texture.Width)
                {
                    spriteBatch.Draw(
                        rasterText.Texture,
                        drawPosition,
                        new Rectangle(0, 0, sourceWidth, rasterText.Texture.Height),
                        Color.White);
                    return;
                }
            }

            spriteBatch.Draw(rasterText.Texture, drawPosition, Color.White);
        }

        private RasterTextTexture GetOrCreateRasterText(string text, float scale, Color color)
        {
            string cacheKey = BuildTextureCacheKey(text, scale, color);
            if (_textureCache.TryGetValue(cacheKey, out RasterTextTexture cachedTexture)
                && cachedTexture.Texture != null
                && !cachedTexture.Texture.IsDisposed)
            {
                return cachedTexture;
            }

            using SD.Font font = CreateScaledFont(scale);
            SD.Size measuredSize = SWF.TextRenderer.MeasureText(
                _measureGraphics,
                text,
                font,
                new SD.Size(int.MaxValue, int.MaxValue),
                ClientTextFormatFlags);
            int width = Math.Max(1, measuredSize.Width + (RasterPadding * 2));
            int height = Math.Max(1, measuredSize.Height + (RasterPadding * 2));

            using SD.Bitmap bitmap = new SD.Bitmap(width, height, SDImaging.PixelFormat.Format32bppArgb);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);

            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.PageUnit = SD.GraphicsUnit.Pixel;
            SWF.TextRenderer.DrawText(
                graphics,
                text,
                font,
                new SD.Rectangle(RasterPadding, RasterPadding, width - (RasterPadding * 2), height - (RasterPadding * 2)),
                ToDrawingColor(color),
                SD.Color.Transparent,
                ClientTextFormatFlags);

            RasterTextTexture texture = CreateRasterTextTexture(bitmap, measuredSize);
            _textureCache[cacheKey] = texture;
            return texture;
        }

        private SD.Font CreateScaledFont(float scale)
        {
            int fontSizeKey = QuantizeScale(scale);
            if (_fontCache.TryGetValue(fontSizeKey, out SD.Font cachedFont))
            {
                return (SD.Font)cachedFont.Clone();
            }

            float pointSize = Math.Max(1f, _basePointSize * Math.Max(0.1f, scale));
            SD.Font font;
            try
            {
                font = new SD.Font(_fontFamily, pointSize, _fontStyle, SD.GraphicsUnit.Pixel, KoreanGdiCharset);
            }
            catch (ArgumentException)
            {
                font = new SD.Font(_fontFamily, pointSize, _fontStyle, SD.GraphicsUnit.Pixel);
            }

            _fontCache[fontSizeKey] = font;
            return (SD.Font)font.Clone();
        }

        private RasterTextTexture CreateRasterTextTexture(SD.Bitmap bitmap, SD.Size measuredSize)
        {
            if (!TryFindOpaqueBounds(bitmap, out SD.Rectangle bounds))
            {
                return new RasterTextTexture(
                    bitmap.ToTexture2D(_graphicsDevice),
                    0,
                    0,
                    new Vector2(Math.Max(1, measuredSize.Width), Math.Max(1, measuredSize.Height)));
            }

            using SD.Bitmap croppedBitmap = bitmap.Clone(bounds, bitmap.PixelFormat);
            Texture2D texture = croppedBitmap.ToTexture2D(_graphicsDevice);
            Vector2 measurement = new Vector2(
                Math.Max(Math.Max(1, measuredSize.Width), bounds.Right - RasterPadding),
                Math.Max(Math.Max(1, measuredSize.Height), bounds.Bottom - RasterPadding));

            return new RasterTextTexture(
                texture,
                bounds.X - RasterPadding,
                bounds.Y - RasterPadding,
                measurement);
        }

        private static bool TryFindOpaqueBounds(SD.Bitmap bitmap, out SD.Rectangle bounds)
        {
            int left = bitmap.Width;
            int top = bitmap.Height;
            int right = -1;
            int bottom = -1;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).A == 0)
                    {
                        continue;
                    }

                    if (x < left)
                    {
                        left = x;
                    }

                    if (x > right)
                    {
                        right = x;
                    }

                    if (y < top)
                    {
                        top = y;
                    }

                    if (y > bottom)
                    {
                        bottom = y;
                    }
                }
            }

            if (right < left || bottom < top)
            {
                bounds = SD.Rectangle.Empty;
                return false;
            }

            bounds = SD.Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
            return true;
        }

        private static string ResolveFontFamily(string requestedFamily)
        {
            if (TryResolveConfiguredFontFamily(out string configuredFamily))
            {
                return configuredFamily;
            }

            if (TryResolveDefaultPrivateFontFamily(out string privateFontFamily))
            {
                return privateFontFamily;
            }

            if (!string.IsNullOrWhiteSpace(requestedFamily))
            {
                string installedRequestedFamily = ResolveInstalledFontFamilyName(requestedFamily);
                if (!string.IsNullOrWhiteSpace(installedRequestedFamily))
                {
                    return installedRequestedFamily;
                }
            }

            return ResolveInstalledFontFamilyName(DefaultFontFamilyCandidates);
        }

        private static bool TryResolveConfiguredFontFamily(out string fontFamilyName)
        {
            fontFamilyName = null;

            string configuredFontPath = Environment.GetEnvironmentVariable(ClientTextFontPathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredFontPath))
            {
                return false;
            }

            string candidatePath;
            try
            {
                candidatePath = Path.GetFullPath(configuredFontPath.Trim());
            }
            catch
            {
                return false;
            }

            if (!TryResolveFontPath(candidatePath, out string resolvedFontPath))
            {
                return false;
            }

            string configuredFontFace = Environment.GetEnvironmentVariable(ClientTextFontFaceEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredFontFace))
            {
                return PrivateFontRegistry.TryRegister(resolvedFontPath, configuredFontFace, out fontFamilyName);
            }

            return PrivateFontRegistry.TryRegister(resolvedFontPath, DefaultPrivateFontFamilyCandidates, out fontFamilyName);
        }

        private static string ResolveInstalledFontFamilyName(params string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return SD.FontFamily.GenericSansSerif.Name;
            }

            HashSet<string> installedFamilies = new HashSet<string>(
                SD.FontFamily.Families.Select(static family => family.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && installedFamilies.Contains(candidate))
                {
                    return candidate;
                }
            }

            return SD.FontFamily.GenericSansSerif.Name;
        }

        private static bool TryResolveDefaultPrivateFontFamily(out string fontFamilyName)
        {
            fontFamilyName = null;

            foreach (string candidatePath in EnumerateDefaultPrivateFontCandidatePaths())
            {
                if (PrivateFontRegistry.TryRegister(candidatePath, DefaultPrivateFontFamilyCandidates, out fontFamilyName))
                {
                    return !string.IsNullOrWhiteSpace(fontFamilyName);
                }
            }

            return false;
        }

        private static bool TryResolveFontPath(string candidatePath, out string resolvedFontPath)
        {
            if (File.Exists(candidatePath))
            {
                resolvedFontPath = candidatePath;
                return true;
            }

            if (!Directory.Exists(candidatePath))
            {
                resolvedFontPath = null;
                return false;
            }

            foreach (string discoveredFontPath in EnumerateCandidateFontPaths(candidatePath))
            {
                if (File.Exists(discoveredFontPath))
                {
                    resolvedFontPath = discoveredFontPath;
                    return true;
                }
            }

            resolvedFontPath = null;
            return false;
        }

        private static IEnumerable<string> EnumerateDefaultPrivateFontCandidatePaths()
        {
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (string rootDirectory in EnumerateDefaultPrivateFontSearchRoots())
            {
                foreach (string candidatePath in EnumerateCandidateFontPaths(rootDirectory))
                {
                    if (seenPaths.Add(candidatePath))
                    {
                        yield return candidatePath;
                    }
                }
            }

            foreach (string directWindowsFontPath in EnumerateLegacyWindowsFontCandidates())
            {
                if (seenPaths.Add(directWindowsFontPath))
                {
                    yield return directWindowsFontPath;
                }
            }
        }

        private static IEnumerable<string> EnumerateLegacyWindowsFontCandidates()
        {
            string windowsFontsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Fonts");
            if (string.IsNullOrWhiteSpace(windowsFontsPath) || !Directory.Exists(windowsFontsPath))
            {
                yield break;
            }

            foreach (string fileName in DefaultPrivateFontFiles)
            {
                string candidatePath = Path.Combine(windowsFontsPath, fileName);
                if (File.Exists(candidatePath))
                {
                    yield return candidatePath;
                }
            }
        }

        private static IEnumerable<string> EnumerateCandidateFontPaths(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                yield break;
            }

            foreach (string fileName in DefaultPrivateFontFileCandidates)
            {
                string directCandidatePath = Path.Combine(rootDirectory, fileName);
                if (File.Exists(directCandidatePath))
                {
                    yield return directCandidatePath;
                }
            }

            IEnumerable<string> recursiveMatches;
            try
            {
                recursiveMatches = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories);
            }
            catch
            {
                yield break;
            }

            foreach (string recursiveMatch in recursiveMatches)
            {
                string fileName = Path.GetFileName(recursiveMatch);
                if (DefaultPrivateFontFileCandidates.Any(candidate => string.Equals(candidate, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return recursiveMatch;
                }
            }
        }

        private static IEnumerable<string> EnumerateDefaultPrivateFontSearchRoots()
        {
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);
            string windowsFontsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Fonts");

            foreach (string baseDirectory in new[]
                     {
                         AppContext.BaseDirectory,
                         Environment.CurrentDirectory,
                         AppDomain.CurrentDomain.BaseDirectory,
                         windowsFontsDirectory
                     })
            {
                if (string.IsNullOrWhiteSpace(baseDirectory))
                {
                    continue;
                }

                foreach (string rootDirectory in EnumerateSearchRootAncestors(baseDirectory))
                {
                    foreach (string suffix in DefaultPrivateFontSearchDirectorySuffixes)
                    {
                        string candidate = string.IsNullOrEmpty(suffix)
                            ? rootDirectory
                            : Path.Combine(rootDirectory, suffix);

                        string fullPath;
                        try
                        {
                            fullPath = Path.GetFullPath(candidate);
                        }
                        catch
                        {
                            continue;
                        }

                        if (!Directory.Exists(fullPath) || !seenPaths.Add(fullPath))
                        {
                            continue;
                        }

                        yield return fullPath;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateSearchRootAncestors(string baseDirectory)
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(baseDirectory);
            }
            catch
            {
                yield break;
            }

            DirectoryInfo current = new DirectoryInfo(fullPath);
            for (int depth = 0; current != null && depth < 4; depth++, current = current.Parent)
            {
                yield return current.FullName;
            }
        }

        private static int QuantizeScale(float scale)
        {
            return (int)Math.Round(Math.Max(0.1f, scale) * 1000f);
        }

        private static string BuildCacheKey(string text, float scale)
        {
            return QuantizeScale(scale).ToString(CultureInfo.InvariantCulture) + "|" + text;
        }

        private static string BuildTextureCacheKey(string text, float scale, Color color)
        {
            return BuildCacheKey(text, scale) + "|" + color.PackedValue.ToString(CultureInfo.InvariantCulture);
        }

        private static SD.Color ToDrawingColor(Color color)
        {
            return SD.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private readonly struct RasterTextTexture
        {
            public RasterTextTexture(Texture2D texture, int offsetX, int offsetY, Vector2 measurement)
            {
                Texture = texture;
                OffsetX = offsetX;
                OffsetY = offsetY;
                Measurement = measurement;
            }

            public Texture2D Texture { get; }
            public int OffsetX { get; }
            public int OffsetY { get; }
            public Vector2 Measurement { get; }
        }

        private static class PrivateFontRegistry
        {
            private static readonly object Sync = new object();
            private static readonly Dictionary<string, RegisteredPrivateFont> RegisteredFonts = new(StringComparer.OrdinalIgnoreCase);

            public static bool TryRegister(string fontPath, string preferredFamilyName, out string resolvedFamilyName)
            {
                return TryRegister(
                    fontPath,
                    string.IsNullOrWhiteSpace(preferredFamilyName) ? null : new[] { preferredFamilyName },
                    out resolvedFamilyName);
            }

            public static bool TryRegister(string fontPath, IEnumerable<string> preferredFamilyNames, out string resolvedFamilyName)
            {
                lock (Sync)
                {
                    if (RegisteredFonts.TryGetValue(fontPath, out RegisteredPrivateFont cachedFont))
                    {
                        resolvedFamilyName = cachedFont.ResolveFamilyName(preferredFamilyNames);
                        return !string.IsNullOrWhiteSpace(resolvedFamilyName);
                    }

                    try
                    {
                        byte[] fontBytes = File.ReadAllBytes(fontPath);
                        if (fontBytes.Length == 0)
                        {
                            resolvedFamilyName = null;
                            return false;
                        }

                        IntPtr fontData = Marshal.AllocCoTaskMem(fontBytes.Length);
                        Marshal.Copy(fontBytes, 0, fontData, fontBytes.Length);

                        var privateFonts = new SDText.PrivateFontCollection();
                        privateFonts.AddMemoryFont(fontData, fontBytes.Length);

                        uint fontsAdded = 0;
                        IntPtr gdiHandle = AddFontMemResourceEx(fontData, (uint)fontBytes.Length, IntPtr.Zero, ref fontsAdded);
                        RegisteredPrivateFont registeredFont = new RegisteredPrivateFont(fontData, privateFonts, gdiHandle);
                        RegisteredFonts[fontPath] = registeredFont;

                        resolvedFamilyName = registeredFont.ResolveFamilyName(preferredFamilyNames);
                        return !string.IsNullOrWhiteSpace(resolvedFamilyName);
                    }
                    catch
                    {
                        resolvedFamilyName = null;
                        return false;
                    }
                }
            }

            [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);
        }

        private sealed class RegisteredPrivateFont
        {
            public RegisteredPrivateFont(IntPtr fontData, SDText.PrivateFontCollection collection, IntPtr gdiHandle)
            {
                FontData = fontData;
                Collection = collection;
                GdiHandle = gdiHandle;
            }

            public IntPtr FontData { get; }
            public SDText.PrivateFontCollection Collection { get; }
            public IntPtr GdiHandle { get; }

            public string ResolveFamilyName(IEnumerable<string> preferredFamilyNames)
            {
                SD.FontFamily[] families = Collection?.Families;
                if (families == null || families.Length == 0)
                {
                    return null;
                }

                if (preferredFamilyNames != null)
                {
                    foreach (string preferredFamilyName in preferredFamilyNames)
                    {
                        if (string.IsNullOrWhiteSpace(preferredFamilyName))
                        {
                            continue;
                        }

                        SD.FontFamily preferredFamily = families.FirstOrDefault(
                            family => string.Equals(family.Name, preferredFamilyName, StringComparison.OrdinalIgnoreCase));
                        if (preferredFamily != null)
                        {
                            return preferredFamily.Name;
                        }
                    }
                }

                return families[0].Name;
            }
        }
    }
}
