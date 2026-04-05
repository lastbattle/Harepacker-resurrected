using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SD = System.Drawing;
using SDImaging = System.Drawing.Imaging;
using SDText = System.Drawing.Text;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using SWF = System.Windows.Forms;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class ClientTextRasterizer : IDisposable
    {
        private const int RasterPadding = 2;
        private const int EmbeddedFontSignatureScanWindow = 8192;
        private const int MaxEmbeddedFontDecompressedBytes = 8 * 1024 * 1024;
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
        private static readonly string[] DefaultFontFileExtensions =
        {
            ".ttf",
            ".ttc",
            ".otf"
        };
        private static readonly string[] PrivateFontContainerExtensions =
        {
            ".exe",
            ".dll"
        };
        private static readonly string[] PreferredFontFileNameFragments =
        {
            "DotumChe",
            "Dotum",
            "DOTOOMCHE",
            "DOTOOM",
            "DODUMCHE",
            "DODUM",
            "GulimChe",
            "Gulim",
            "BatangChe",
            "Batang"
        };
        private static readonly string[] PreferredFontPathFragments =
        {
            $"{Path.DirectorySeparatorChar}Fonts{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}Content{Path.DirectorySeparatorChar}Fonts{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}Maple{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}MapleStory{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}Nexon{Path.DirectorySeparatorChar}"
        };
        private static readonly string[] PerUserWindowsFontDirectories =
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "Windows",
                "Fonts"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Fonts")
        };
        private static readonly string[] MapleInstallRegistrySubKeys =
        {
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Wizet",
            @"SOFTWARE\Wizet"
        };
        private static readonly string[] MapleInstallValueNames =
        {
            "InstallLocation",
            "DisplayIcon"
        };
        private static readonly string[] WindowsFontRegistrySubKeys =
        {
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Fonts"
        };
        private static readonly string[] DefaultPrivateFontRegistryNameCandidates =
        {
            "DotumChe",
            "Dotum",
            "DOTOOMCHE",
            "DOTOOM",
            "DODUMCHE",
            "DODUM",
            "GulimChe",
            "Gulim",
            "BatangChe",
            "Batang"
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
        private const uint LoadLibraryAsDataFile = 0x00000002;
        private static readonly IntPtr RtFont = new IntPtr(8);
        private static readonly IntPtr RtRcData = new IntPtr(10);

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

        internal static string ResolvePreferredFontFamily(
            string requestedFamily = null,
            string fontPathEnvironmentVariable = null,
            string fontFaceEnvironmentVariable = null,
            IEnumerable<string> preferredPrivateFontFamilyCandidates = null)
        {
            return ResolveFontFamily(
                requestedFamily,
                fontPathEnvironmentVariable,
                fontFaceEnvironmentVariable,
                preferredPrivateFontFamilyCandidates);
        }

        internal static SD.Font CreateClientFont(
            float pixelSize,
            SD.FontStyle style = SD.FontStyle.Regular,
            string requestedFamily = null,
            string fontPathEnvironmentVariable = null,
            string fontFaceEnvironmentVariable = null,
            IEnumerable<string> preferredPrivateFontFamilyCandidates = null)
        {
            string resolvedFamily = ResolvePreferredFontFamily(
                requestedFamily,
                fontPathEnvironmentVariable,
                fontFaceEnvironmentVariable,
                preferredPrivateFontFamilyCandidates);
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

        public void Dispose()
        {
            DisposeCachedResources();
            _measureGraphics?.Dispose();
            _measureBitmap?.Dispose();
        }

        public void DisposeCachedResources()
        {
            foreach (RasterTextTexture texture in _textureCache.Values)
            {
                if (texture.Texture != null && !texture.Texture.IsDisposed)
                {
                    texture.Texture.Dispose();
                }
            }

            _textureCache.Clear();
            _measureCache.Clear();

            foreach (SD.Font font in _fontCache.Values)
            {
                font?.Dispose();
            }

            _fontCache.Clear();
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

        private static string ResolveFontFamily(
            string requestedFamily,
            string fontPathEnvironmentVariable,
            string fontFaceEnvironmentVariable,
            IEnumerable<string> preferredPrivateFontFamilyCandidates)
        {
            if (TryResolveConfiguredFontFamily(
                    fontPathEnvironmentVariable,
                    fontFaceEnvironmentVariable,
                    preferredPrivateFontFamilyCandidates,
                    out string configuredFamily))
            {
                return configuredFamily;
            }

            if (TryResolveConfiguredFontFamily(out configuredFamily))
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
            return TryResolveConfiguredFontFamily(
                ClientTextFontPathEnvironmentVariable,
                ClientTextFontFaceEnvironmentVariable,
                DefaultPrivateFontFamilyCandidates,
                out fontFamilyName);
        }

        private static bool TryResolveConfiguredFontFamily(
            string fontPathEnvironmentVariable,
            string fontFaceEnvironmentVariable,
            IEnumerable<string> preferredFamilyCandidates,
            out string fontFamilyName)
        {
            fontFamilyName = null;

            if (string.IsNullOrWhiteSpace(fontPathEnvironmentVariable))
            {
                return false;
            }

            string configuredFontPath = Environment.GetEnvironmentVariable(fontPathEnvironmentVariable);
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

            string configuredFontFace = string.IsNullOrWhiteSpace(fontFaceEnvironmentVariable)
                ? null
                : Environment.GetEnvironmentVariable(fontFaceEnvironmentVariable);
            IEnumerable<string> preferredFamilies = BuildPreferredFamilyCandidates(
                configuredFontFace,
                preferredFamilyCandidates,
                DefaultPrivateFontFamilyCandidates);
            return TryRegisterPrivateFontSource(resolvedFontPath, preferredFamilies, out fontFamilyName);
        }

        private static IEnumerable<string> BuildPreferredFamilyCandidates(
            string configuredFontFace,
            IEnumerable<string> preferredFamilyCandidates,
            IEnumerable<string> fallbackFamilyCandidates)
        {
            HashSet<string> seenFamilies = new(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(configuredFontFace))
            {
                string trimmedFace = configuredFontFace.Trim();
                if (seenFamilies.Add(trimmedFace))
                {
                    yield return trimmedFace;
                }
            }

            if (preferredFamilyCandidates != null)
            {
                foreach (string preferredFamilyCandidate in preferredFamilyCandidates)
                {
                    if (!string.IsNullOrWhiteSpace(preferredFamilyCandidate) &&
                        seenFamilies.Add(preferredFamilyCandidate))
                    {
                        yield return preferredFamilyCandidate;
                    }
                }
            }

            if (fallbackFamilyCandidates != null)
            {
                foreach (string fallbackFamilyCandidate in fallbackFamilyCandidates)
                {
                    if (!string.IsNullOrWhiteSpace(fallbackFamilyCandidate) &&
                        seenFamilies.Add(fallbackFamilyCandidate))
                    {
                        yield return fallbackFamilyCandidate;
                    }
                }
            }
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
                if (TryRegisterPrivateFontSource(candidatePath, DefaultPrivateFontFamilyCandidates, out fontFamilyName))
                {
                    return !string.IsNullOrWhiteSpace(fontFamilyName);
                }
            }

            foreach (string candidatePath in EnumerateMapleEmbeddedFontContainerPaths())
            {
                if (TryRegisterPrivateFontSource(candidatePath, DefaultPrivateFontFamilyCandidates, out fontFamilyName))
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

            foreach (string discoveredContainerPath in EnumerateCandidateFontContainerPaths(candidatePath))
            {
                if (File.Exists(discoveredContainerPath))
                {
                    resolvedFontPath = discoveredContainerPath;
                    return true;
                }
            }

            resolvedFontPath = null;
            return false;
        }

        private static bool TryRegisterPrivateFontSource(
            string sourcePath,
            IEnumerable<string> preferredFamilyNames,
            out string fontFamilyName)
        {
            fontFamilyName = null;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            string extension = Path.GetExtension(sourcePath);
            if (IsFontFileExtension(extension))
            {
                return PrivateFontRegistry.TryRegister(sourcePath, preferredFamilyNames, out fontFamilyName);
            }

            if (!IsFontContainerExtension(extension))
            {
                return false;
            }

            foreach ((string sourceKey, byte[] fontBytes) in EnumerateEmbeddedFontPayloads(sourcePath))
            {
                if (PrivateFontRegistry.TryRegister(sourceKey, fontBytes, preferredFamilyNames, out fontFamilyName))
                {
                    return !string.IsNullOrWhiteSpace(fontFamilyName);
                }
            }

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

            foreach (string registryFontPath in EnumerateRegisteredPrivateFontPaths())
            {
                if (seenPaths.Add(registryFontPath))
                {
                    yield return registryFontPath;
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

        private static readonly EnumerationOptions RecursiveFontSearchOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        private static IEnumerable<string> EnumerateCandidateFontPaths(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                yield break;
            }

            HashSet<string> yieldedPaths = new(StringComparer.OrdinalIgnoreCase);
            foreach (string fileName in DefaultPrivateFontFileCandidates)
            {
                string directCandidatePath = Path.Combine(rootDirectory, fileName);
                if (File.Exists(directCandidatePath) && yieldedPaths.Add(directCandidatePath))
                {
                    yield return directCandidatePath;
                }
            }

            foreach (string discoveredFontPath in EnumerateScoredFontPaths(rootDirectory))
            {
                if (yieldedPaths.Add(discoveredFontPath))
                {
                    yield return discoveredFontPath;
                }
            }
        }

        internal static IReadOnlyList<string> EnumerateCandidateFontPathsForTests(string rootDirectory)
        {
            return EnumerateCandidateFontPaths(rootDirectory).ToArray();
        }

        internal static IReadOnlyList<string> EnumerateCandidateFontContainerPathsForTests(string rootDirectory)
        {
            return EnumerateCandidateFontContainerPaths(rootDirectory).ToArray();
        }

        internal static bool LooksLikeEmbeddedFontPayloadForTests(byte[] fontBytes)
        {
            return LooksLikeEmbeddedFontPayload(fontBytes);
        }

        internal static bool TryExtractEmbeddedFontPayloadForTests(byte[] sourceBytes, out byte[] fontBytes)
        {
            return TryExtractEmbeddedFontPayload(sourceBytes, out fontBytes);
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

            foreach (string userFontsDirectory in PerUserWindowsFontDirectories)
            {
                foreach (string rootDirectory in EnumerateSearchRootAncestors(userFontsDirectory))
                {
                    if (Directory.Exists(rootDirectory) && seenPaths.Add(rootDirectory))
                    {
                        yield return rootDirectory;
                    }
                }
            }

            foreach (string installDirectory in EnumerateMapleInstallDirectories())
            {
                foreach (string rootDirectory in EnumerateSearchRootAncestors(installDirectory))
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

        private static IEnumerable<string> EnumerateMapleEmbeddedFontContainerPaths()
        {
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (string installDirectory in EnumerateMapleInstallDirectories())
            {
                foreach (string candidatePath in EnumerateCandidateFontContainerPaths(installDirectory))
                {
                    if (seenPaths.Add(candidatePath))
                    {
                        yield return candidatePath;
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

        private static IEnumerable<string> EnumerateScoredFontPaths(string rootDirectory)
        {
            IEnumerable<string> recursiveMatches;
            try
            {
                recursiveMatches = Directory.EnumerateFiles(rootDirectory, "*", RecursiveFontSearchOptions);
            }
            catch
            {
                yield break;
            }

            List<(string Path, int Score)> scoredPaths = new();
            foreach (string recursiveMatch in recursiveMatches)
            {
                int score = ScoreDiscoveredFontPath(recursiveMatch);
                if (score < 0)
                {
                    continue;
                }

                scoredPaths.Add((recursiveMatch, score));
            }

            foreach ((string path, _) in scoredPaths
                .OrderByDescending(static entry => entry.Score)
                .ThenBy(static entry => entry.Path, StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }

        private static IEnumerable<string> EnumerateCandidateFontContainerPaths(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory) || !Directory.Exists(rootDirectory))
            {
                yield break;
            }

            HashSet<string> yieldedPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (string preferredFileName in new[] { "MapleStory.exe", "MapleStory.dll" })
            {
                string directCandidatePath = Path.Combine(rootDirectory, preferredFileName);
                if (File.Exists(directCandidatePath) && yieldedPaths.Add(directCandidatePath))
                {
                    yield return directCandidatePath;
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                yield break;
            }

            foreach (string candidatePath in files
                .Where(static path => IsFontContainerExtension(Path.GetExtension(path)))
                .OrderByDescending(static path => ScoreDiscoveredFontContainerPath(path))
                .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (yieldedPaths.Add(candidatePath))
                {
                    yield return candidatePath;
                }
            }
        }

        private static int ScoreDiscoveredFontPath(string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return -1;
            }

            string extension = Path.GetExtension(candidatePath);
            if (!DefaultFontFileExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return -1;
            }

            string fileName = Path.GetFileName(candidatePath);
            int score = 0;

            if (DefaultPrivateFontFileCandidates.Any(candidate => string.Equals(candidate, fileName, StringComparison.OrdinalIgnoreCase)))
            {
                score += 1000;
            }

            if (PreferredFontFileNameFragments.Any(fragment => candidatePath.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                score += 200;
            }

            if (PreferredFontPathFragments.Any(fragment => candidatePath.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                score += 50;
            }

            if (string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase))
            {
                score += 25;
            }

            if (string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase))
            {
                score += 15;
            }

            return score;
        }

        private static int ScoreDiscoveredFontContainerPath(string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                return -1;
            }

            string extension = Path.GetExtension(candidatePath);
            if (!IsFontContainerExtension(extension))
            {
                return -1;
            }

            int score = 0;
            string fileName = Path.GetFileName(candidatePath);

            if (string.Equals(fileName, "MapleStory.exe", StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }
            else if (fileName.IndexOf("Maple", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 250;
            }

            if (PreferredFontPathFragments.Any(fragment => candidatePath.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                score += 50;
            }

            return score;
        }

        private static IEnumerable<string> EnumerateMapleInstallDirectories()
        {
            HashSet<string> seenDirectories = new(StringComparer.OrdinalIgnoreCase);

            foreach (string registrySubKey in MapleInstallRegistrySubKeys)
            {
                foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    {
                        RegistryKey baseKey;
                        try
                        {
                            baseKey = RegistryKey.OpenBaseKey(hive, view);
                        }
                        catch
                        {
                            continue;
                        }

                        using (baseKey)
                        using (RegistryKey rootKey = baseKey.OpenSubKey(registrySubKey))
                        {
                            if (rootKey == null)
                            {
                                continue;
                            }

                            foreach (string subKeyName in rootKey.GetSubKeyNames())
                            {
                                if (subKeyName.IndexOf("maple", StringComparison.OrdinalIgnoreCase) < 0 &&
                                    subKeyName.IndexOf("wizet", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    continue;
                                }

                                using RegistryKey installKey = rootKey.OpenSubKey(subKeyName);
                                if (installKey == null)
                                {
                                    continue;
                                }

                                foreach (string valueName in MapleInstallValueNames)
                                {
                                    if (!TryNormalizeRegistryDirectory(installKey.GetValue(valueName) as string, out string directory) ||
                                        !seenDirectories.Add(directory))
                                    {
                                        continue;
                                    }

                                    yield return directory;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateRegisteredPrivateFontPaths()
        {
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    RegistryKey baseKey;
                    try
                    {
                        baseKey = RegistryKey.OpenBaseKey(hive, view);
                    }
                    catch
                    {
                        continue;
                    }

                    using (baseKey)
                    {
                        foreach (string subKey in WindowsFontRegistrySubKeys)
                        {
                            using RegistryKey fontKey = baseKey.OpenSubKey(subKey);
                            if (fontKey == null)
                            {
                                continue;
                            }

                            foreach (string valueName in fontKey.GetValueNames())
                            {
                                if (!IsPrivateFontRegistryEntry(valueName) ||
                                    !TryNormalizeRegistryFontFile(fontKey.GetValue(valueName)?.ToString(), out string fontPath) ||
                                    !seenPaths.Add(fontPath))
                                {
                                    continue;
                                }

                                yield return fontPath;
                            }
                        }
                    }
                }
            }
        }

        private static bool TryNormalizeRegistryDirectory(string rawValue, out string directory)
        {
            directory = null;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            string normalizedValue = rawValue.Trim().Trim('"');
            int resourceSuffixIndex = normalizedValue.IndexOf(",0", StringComparison.Ordinal);
            if (resourceSuffixIndex >= 0)
            {
                normalizedValue = normalizedValue.Substring(0, resourceSuffixIndex).Trim().Trim('"');
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(normalizedValue);
            }
            catch
            {
                return false;
            }

            if (Directory.Exists(fullPath))
            {
                directory = fullPath;
                return true;
            }

            if (!File.Exists(fullPath))
            {
                return false;
            }

            string parentDirectory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                return false;
            }

            directory = parentDirectory;
            return true;
        }

        private static bool IsPrivateFontRegistryEntry(string valueName)
        {
            if (string.IsNullOrWhiteSpace(valueName))
            {
                return false;
            }

            foreach (string candidate in DefaultPrivateFontRegistryNameCandidates)
            {
                if (valueName.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryNormalizeRegistryFontFile(string rawValue, out string fontPath)
        {
            fontPath = null;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            string normalizedValue = rawValue.Trim().Trim('"');
            string[] candidateValues =
            {
                normalizedValue,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Fonts",
                    normalizedValue)
            };

            foreach (string candidateValue in candidateValues)
            {
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(candidateValue);
                }
                catch
                {
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    continue;
                }

                fontPath = fullPath;
                return true;
            }

            return false;
        }

        private static bool IsFontFileExtension(string extension)
        {
            return !string.IsNullOrWhiteSpace(extension)
                && DefaultFontFileExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsFontContainerExtension(string extension)
        {
            return !string.IsNullOrWhiteSpace(extension)
                && PrivateFontContainerExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<(string SourceKey, byte[] FontBytes)> EnumerateEmbeddedFontPayloads(string containerPath)
        {
            if (string.IsNullOrWhiteSpace(containerPath) || !File.Exists(containerPath))
            {
                yield break;
            }

            IntPtr module = NativeResourceReader.LoadLibraryEx(containerPath, IntPtr.Zero, LoadLibraryAsDataFile);
            if (module == IntPtr.Zero)
            {
                yield break;
            }

            try
            {
                foreach (IntPtr resourceType in new[] { RtFont, RtRcData })
                {
                    foreach (string resourceName in NativeResourceReader.EnumerateResourceNames(module, resourceType))
                    {
                        if (!NativeResourceReader.TryLoadResource(module, resourceType, resourceName, out byte[] resourceBytes) ||
                            !TryExtractEmbeddedFontPayload(resourceBytes, out byte[] fontBytes))
                        {
                            continue;
                        }

                        yield return ($"{containerPath}|{resourceType.ToInt64()}|{resourceName}", fontBytes);
                    }
                }
            }
            finally
            {
                NativeResourceReader.FreeLibrary(module);
            }
        }

        private static bool LooksLikeEmbeddedFontPayload(byte[] fontBytes)
        {
            if (fontBytes == null || fontBytes.Length < 4)
            {
                return false;
            }

            if (IsTrueTypeHeader(fontBytes, 0))
            {
                return true;
            }

            if (IsTrueTypeCollectionHeader(fontBytes, 0))
            {
                return true;
            }

            return IsOpenTypeHeader(fontBytes, 0);
        }

        private static bool TryExtractEmbeddedFontPayload(byte[] sourceBytes, out byte[] fontBytes)
        {
            fontBytes = null;
            if (sourceBytes == null || sourceBytes.Length < 4)
            {
                return false;
            }

            if (LooksLikeEmbeddedFontPayload(sourceBytes))
            {
                fontBytes = sourceBytes;
                return true;
            }

            if (TryFindEmbeddedFontHeaderOffset(sourceBytes, out int rawOffset))
            {
                fontBytes = SliceBytes(sourceBytes, rawOffset);
                return true;
            }

            if (!TryDecompressEmbeddedPayload(sourceBytes, out byte[] decompressedBytes))
            {
                return false;
            }

            if (LooksLikeEmbeddedFontPayload(decompressedBytes))
            {
                fontBytes = decompressedBytes;
                return true;
            }

            if (!TryFindEmbeddedFontHeaderOffset(decompressedBytes, out int decompressedOffset))
            {
                return false;
            }

            fontBytes = SliceBytes(decompressedBytes, decompressedOffset);
            return true;
        }

        private static bool TryFindEmbeddedFontHeaderOffset(byte[] bytes, out int headerOffset)
        {
            headerOffset = -1;
            if (bytes == null || bytes.Length < 4)
            {
                return false;
            }

            int maxOffset = Math.Min(bytes.Length - 4, EmbeddedFontSignatureScanWindow);
            for (int offset = 0; offset <= maxOffset; offset++)
            {
                if (IsTrueTypeHeader(bytes, offset) ||
                    IsTrueTypeCollectionHeader(bytes, offset) ||
                    IsOpenTypeHeader(bytes, offset))
                {
                    headerOffset = offset;
                    return true;
                }
            }

            return false;
        }

        private static bool TryDecompressEmbeddedPayload(byte[] sourceBytes, out byte[] decompressedBytes)
        {
            if (TryDecompressWithZlib(sourceBytes, out decompressedBytes))
            {
                return true;
            }

            return TryDecompressWithGzip(sourceBytes, out decompressedBytes);
        }

        private static bool TryDecompressWithZlib(byte[] sourceBytes, out byte[] decompressedBytes)
        {
            decompressedBytes = null;
            if (!LooksLikeZlibHeader(sourceBytes))
            {
                return false;
            }

            return TryDecompressStream(sourceBytes, static stream => new ZLibStream(stream, CompressionMode.Decompress), out decompressedBytes);
        }

        private static bool TryDecompressWithGzip(byte[] sourceBytes, out byte[] decompressedBytes)
        {
            decompressedBytes = null;
            if (sourceBytes.Length < 2 || sourceBytes[0] != 0x1F || sourceBytes[1] != 0x8B)
            {
                return false;
            }

            return TryDecompressStream(sourceBytes, static stream => new GZipStream(stream, CompressionMode.Decompress), out decompressedBytes);
        }

        private static bool TryDecompressStream(
            byte[] sourceBytes,
            Func<MemoryStream, Stream> createDecompressStream,
            out byte[] decompressedBytes)
        {
            decompressedBytes = null;

            try
            {
                using MemoryStream compressed = new(sourceBytes);
                using Stream decompressor = createDecompressStream(compressed);
                using MemoryStream output = new();
                byte[] buffer = new byte[4096];
                int totalBytes = 0;

                while (true)
                {
                    int readCount = decompressor.Read(buffer, 0, buffer.Length);
                    if (readCount <= 0)
                    {
                        break;
                    }

                    totalBytes += readCount;
                    if (totalBytes > MaxEmbeddedFontDecompressedBytes)
                    {
                        return false;
                    }

                    output.Write(buffer, 0, readCount);
                }

                decompressedBytes = output.ToArray();
                return decompressedBytes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool LooksLikeZlibHeader(byte[] sourceBytes)
        {
            if (sourceBytes.Length < 2)
            {
                return false;
            }

            byte cmf = sourceBytes[0];
            byte flg = sourceBytes[1];
            if ((cmf & 0x0F) != 0x08)
            {
                return false;
            }

            return ((cmf << 8) + flg) % 31 == 0;
        }

        private static byte[] SliceBytes(byte[] sourceBytes, int offset)
        {
            if (offset <= 0)
            {
                return sourceBytes;
            }

            int length = sourceBytes.Length - offset;
            byte[] sliced = new byte[length];
            Buffer.BlockCopy(sourceBytes, offset, sliced, 0, length);
            return sliced;
        }

        private static bool IsTrueTypeHeader(byte[] bytes, int offset)
        {
            return offset + 3 < bytes.Length
                && bytes[offset] == 0x00
                && bytes[offset + 1] == 0x01
                && bytes[offset + 2] == 0x00
                && bytes[offset + 3] == 0x00;
        }

        private static bool IsTrueTypeCollectionHeader(byte[] bytes, int offset)
        {
            return offset + 3 < bytes.Length
                && bytes[offset] == (byte)'t'
                && bytes[offset + 1] == (byte)'t'
                && bytes[offset + 2] == (byte)'c'
                && bytes[offset + 3] == (byte)'f';
        }

        private static bool IsOpenTypeHeader(byte[] bytes, int offset)
        {
            return offset + 3 < bytes.Length
                && bytes[offset] == (byte)'O'
                && bytes[offset + 1] == (byte)'T'
                && bytes[offset + 2] == (byte)'T'
                && bytes[offset + 3] == (byte)'O';
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
                        return TryRegister(fontPath, fontBytes, preferredFamilyNames, out resolvedFamilyName);
                    }
                    catch
                    {
                        resolvedFamilyName = null;
                        return false;
                    }
                }
            }

            public static bool TryRegister(
                string sourceKey,
                byte[] fontBytes,
                IEnumerable<string> preferredFamilyNames,
                out string resolvedFamilyName)
            {
                lock (Sync)
                {
                    if (RegisteredFonts.TryGetValue(sourceKey, out RegisteredPrivateFont cachedFont))
                    {
                        resolvedFamilyName = cachedFont.ResolveFamilyName(preferredFamilyNames);
                        return !string.IsNullOrWhiteSpace(resolvedFamilyName);
                    }

                    if (fontBytes == null || fontBytes.Length == 0)
                    {
                        resolvedFamilyName = null;
                        return false;
                    }

                    try
                    {
                        IntPtr fontData = Marshal.AllocCoTaskMem(fontBytes.Length);
                        Marshal.Copy(fontBytes, 0, fontData, fontBytes.Length);

                        var privateFonts = new SDText.PrivateFontCollection();
                        privateFonts.AddMemoryFont(fontData, fontBytes.Length);

                        uint fontsAdded = 0;
                        IntPtr gdiHandle = AddFontMemResourceEx(fontData, (uint)fontBytes.Length, IntPtr.Zero, ref fontsAdded);
                        RegisteredPrivateFont registeredFont = new RegisteredPrivateFont(fontData, privateFonts, gdiHandle);
                        RegisteredFonts[sourceKey] = registeredFont;

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

        private static class NativeResourceReader
        {
            internal delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);

            internal static IEnumerable<string> EnumerateResourceNames(IntPtr module, IntPtr resourceType)
            {
                var names = new List<string>();
                EnumResNameProc callback = (hModule, lpszType, lpszName, lParam) =>
                {
                    names.Add(ToResourceName(lpszName));
                    return true;
                };

                EnumResourceNames(module, resourceType, callback, IntPtr.Zero);
                return names;
            }

            internal static bool TryLoadResource(IntPtr module, IntPtr resourceType, string resourceName, out byte[] resourceBytes)
            {
                resourceBytes = null;
                IntPtr resourceNamePtr = TryParseResourceName(resourceName, out ushort resourceId)
                    ? new IntPtr(resourceId)
                    : Marshal.StringToHGlobalUni(resourceName);

                try
                {
                    IntPtr resourceInfo = FindResource(module, resourceNamePtr, resourceType);
                    if (resourceInfo == IntPtr.Zero)
                    {
                        return false;
                    }

                    uint resourceSize = SizeofResource(module, resourceInfo);
                    if (resourceSize == 0)
                    {
                        return false;
                    }

                    IntPtr resourceHandle = LoadResource(module, resourceInfo);
                    IntPtr resourceData = LockResource(resourceHandle);
                    if (resourceData == IntPtr.Zero)
                    {
                        return false;
                    }

                    resourceBytes = new byte[(int)resourceSize];
                    Marshal.Copy(resourceData, resourceBytes, 0, (int)resourceSize);
                    return true;
                }
                finally
                {
                    if (!TryParseResourceName(resourceName, out _))
                    {
                        Marshal.FreeHGlobal(resourceNamePtr);
                    }
                }
            }

            private static string ToResourceName(IntPtr namePointer)
            {
                return namePointer.ToInt64() <= ushort.MaxValue
                    ? "#" + namePointer.ToInt64().ToString(CultureInfo.InvariantCulture)
                    : Marshal.PtrToStringUni(namePointer);
            }

            private static bool TryParseResourceName(string resourceName, out ushort resourceId)
            {
                resourceId = 0;
                return !string.IsNullOrWhiteSpace(resourceName)
                    && resourceName.Length > 1
                    && resourceName[0] == '#'
                    && ushort.TryParse(resourceName.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out resourceId);
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern bool FreeLibrary(IntPtr hModule);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool EnumResourceNames(IntPtr hModule, IntPtr lpszType, EnumResNameProc lpEnumFunc, IntPtr lParam);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr LockResource(IntPtr hResData);
        }
    }
}
