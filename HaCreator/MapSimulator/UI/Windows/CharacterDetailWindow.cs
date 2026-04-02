using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SD = System.Drawing;
using SDText = System.Drawing.Text;
using SWF = System.Windows.Forms;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CharacterDetailWindow : UIWindowBase
    {
        private const int LeftValueX = 46;
        private const int RightValueX = 136;
        private const int JobValueY = 1;
        private const int LevelValueY = 19;
        private const int FameValueY = 19;
        private const int StrengthValueY = 41;
        private const int IntelligenceValueY = 41;
        private const int DexterityValueY = 59;
        private const int LuckValueY = 59;
        private const int WorldRankValueY = 99;
        private const int JobRankValueY = 135;
        private const int WorldRankGlyphY = 109;
        private const int JobRankGlyphY = 145;
        private const int RankRightEdgeX = 165;
        private const int RankGlyphX = 170;
        private const int JobRankIconRightEdgeX = 83;
        private const int JobRankIconY = 120;
        private const int StatusPaddingX = 8;
        private const int StatusPaddingY = 8;

        private static readonly XnaColor ValueColor = new XnaColor(64, 64, 64);
        private static readonly XnaColor StatusColor = new XnaColor(64, 64, 64);
        private const int BasicBlackFontHeight = 12;
        private const int TextRasterPadding = 2;
        private const byte KoreanGdiCharset = 129;
        private const string BasicBlackFontPathEnvironmentVariable = "MAPSIM_FONT_BASIC_BLACK_PATH";
        private const string BasicBlackFontFaceEnvironmentVariable = "MAPSIM_FONT_BASIC_BLACK_FACE";
        private const string ClientTextFontPathEnvironmentVariable = "MAPSIM_CLIENT_TEXT_FONT_PATH";
        private const string ClientTextFontFaceEnvironmentVariable = "MAPSIM_CLIENT_TEXT_FONT_FACE";
        private const SWF.TextFormatFlags BasicBlackTextFormatFlags =
            SWF.TextFormatFlags.NoPadding |
            SWF.TextFormatFlags.NoPrefix |
            SWF.TextFormatFlags.PreserveGraphicsClipping |
            SWF.TextFormatFlags.PreserveGraphicsTranslateTransform |
            SWF.TextFormatFlags.SingleLine;
        private static readonly string[] BasicBlackFontFileCandidates =
        {
            "DotumChe.ttf",
            "Dotum.ttf",
            "dotum.ttc",
            "GulimChe.ttf",
            "Gulim.ttf",
            "gulim.ttc",
            "batang.ttc"
        };
        private static readonly string[] BasicBlackFontSearchDirectorySuffixes =
        {
            string.Empty,
            "Fonts",
            "Content",
            Path.Combine("Content", "Fonts")
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
        private static readonly string[] BasicBlackFontRegistryNameCandidates =
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
        private static readonly string[] BasicBlackFontFamilyCandidates =
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
            "Tahoma",
            SD.SystemFonts.MessageBoxFont?.FontFamily?.Name,
            SD.FontFamily.GenericSansSerif.Name
        };

        private readonly Texture2D _panelTexture;
        private readonly Texture2D _panelTextureWithRank;
        private readonly Texture2D _rankUpTexture;
        private readonly Texture2D _rankDownTexture;
        private readonly Texture2D _rankSameTexture;
        private readonly IReadOnlyDictionary<int, Texture2D> _jobBadgeTextures;
        private readonly Dictionary<TextRenderCacheKey, RasterTextTexture> _textTextureCache = new();
        private readonly SD.Bitmap _measureBitmap;
        private readonly SD.Graphics _measureGraphics;
        private readonly SD.Font _basicBlackFont;
        private readonly string _basicBlackFontFamilyName;

        private LoginCharacterRosterEntry _entry;
        private string _statusMessage = "Select a character to inspect details.";
        private SpriteFont _font;

        public CharacterDetailWindow(
            IDXObject frame,
            Texture2D panelTexture,
            Texture2D panelTextureWithRank,
            Texture2D rankUpTexture,
            Texture2D rankDownTexture,
            Texture2D rankSameTexture,
            IReadOnlyDictionary<int, Texture2D> jobBadgeTextures)
            : base(frame)
        {
            _panelTexture = panelTexture;
            _panelTextureWithRank = panelTextureWithRank ?? panelTexture;
            _rankUpTexture = rankUpTexture;
            _rankDownTexture = rankDownTexture;
            _rankSameTexture = rankSameTexture;
            _jobBadgeTextures = jobBadgeTextures ?? new Dictionary<int, Texture2D>();
            _measureBitmap = new SD.Bitmap(1, 1);
            _measureGraphics = SD.Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            _basicBlackFont = CreateBasicBlackFont(out _basicBlackFontFamilyName);
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterDetail;

        public override bool SupportsDragging => false;

        protected override void DrawContents(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            Texture2D panelTexture = ResolvePanelTexture();
            if (panelTexture != null)
            {
                sprite.Draw(panelTexture, new Vector2(Position.X, Position.Y), XnaColor.White);
            }

            if (_entry?.Build == null)
            {
                DrawStatusMessage(sprite);
                return;
            }

            DrawPanelValues(sprite, _entry.Build);
        }

        private void DrawPanelValues(SpriteBatch sprite, CharacterBuild build)
        {
            DrawValue(sprite, LeftValueX, JobValueY, build.JobName);
            DrawValue(sprite, LeftValueX, LevelValueY, build.Level.ToString());
            DrawValue(sprite, RightValueX, FameValueY, build.Fame.ToString());
            DrawValue(sprite, LeftValueX, StrengthValueY, build.TotalSTR.ToString());
            DrawValue(sprite, RightValueX, IntelligenceValueY, build.TotalINT.ToString());
            DrawValue(sprite, LeftValueX, DexterityValueY, build.TotalDEX.ToString());
            DrawValue(sprite, RightValueX, LuckValueY, build.TotalLUK.ToString());

            if (!HasRankInfo())
            {
                return;
            }

            DrawRightAlignedValue(sprite, RankRightEdgeX, WorldRankValueY, FormatRank(build.WorldRank));
            DrawRankGlyph(sprite, build.WorldRank, _entry.PreviousWorldRank, WorldRankGlyphY);

            Texture2D jobBadgeTexture = ResolveJobBadgeTexture(build);
            if (jobBadgeTexture != null)
            {
                sprite.Draw(
                    jobBadgeTexture,
                    new Vector2(Position.X + JobRankIconRightEdgeX - jobBadgeTexture.Width, Position.Y + JobRankIconY),
                    XnaColor.White);
            }

            DrawRightAlignedValue(sprite, RankRightEdgeX, JobRankValueY, FormatRank(build.JobRank));
            DrawRankGlyph(sprite, build.JobRank, _entry.PreviousJobRank, JobRankGlyphY);
        }

        private Texture2D ResolvePanelTexture()
        {
            return HasRankInfo() ? _panelTextureWithRank : _panelTexture;
        }

        private bool HasRankInfo()
        {
            return (_entry?.Build?.WorldRank ?? 0) > 0 || (_entry?.Build?.JobRank ?? 0) > 0;
        }

        private void DrawValue(SpriteBatch sprite, int x, int y, string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "-" : value;
            DrawText(sprite, safeValue, x, y, ValueColor);
        }

        private void DrawRightAlignedValue(SpriteBatch sprite, int rightEdgeX, int y, string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "-" : value;
            Vector2 size = MeasureText(safeValue);
            DrawText(sprite, safeValue, rightEdgeX - (int)Math.Ceiling(size.X), y, ValueColor);
        }

        private void DrawRankGlyph(SpriteBatch sprite, int currentRank, int? previousRank, int y)
        {
            Texture2D glyphTexture = ResolveRankGlyph(currentRank, previousRank);
            if (glyphTexture == null)
            {
                return;
            }

            int yOffset = glyphTexture == _rankSameTexture ? 0 : -4;
            sprite.Draw(
                glyphTexture,
                new Vector2(Position.X + RankGlyphX, Position.Y + y + yOffset),
                XnaColor.White);
        }

        private Texture2D ResolveJobBadgeTexture(CharacterBuild build)
        {
            if (build == null)
            {
                return null;
            }

            if (IsJobName(build.JobName, "warrior"))
            {
                return GetJobBadgeTexture(1);
            }

            if (IsJobName(build.JobName, "magician", "mage"))
            {
                return GetJobBadgeTexture(2);
            }

            if (IsJobName(build.JobName, "bowman", "archer"))
            {
                return GetJobBadgeTexture(3);
            }

            if (IsJobName(build.JobName, "thief", "rogue", "dual"))
            {
                return GetJobBadgeTexture(4);
            }

            if (IsJobName(build.JobName, "beginner", "noblesse", "legend", "citizen"))
            {
                return GetJobBadgeTexture(0);
            }

            return (build.Job / 100) switch
            {
                1 => GetJobBadgeTexture(1),
                2 => GetJobBadgeTexture(2),
                3 => GetJobBadgeTexture(3),
                4 => GetJobBadgeTexture(4),
                _ => GetJobBadgeTexture(0)
            };
        }

        private Texture2D GetJobBadgeTexture(int index)
        {
            return _jobBadgeTextures.TryGetValue(index, out Texture2D texture) ? texture : null;
        }

        private Texture2D ResolveRankGlyph(int currentRank, int? previousRank)
        {
            if (currentRank <= 0)
            {
                return null;
            }

            if (!previousRank.HasValue || previousRank.Value <= 0 || previousRank.Value == currentRank)
            {
                return _rankSameTexture;
            }

            return currentRank < previousRank.Value ? _rankUpTexture : _rankDownTexture;
        }

        private void DrawStatusMessage(SpriteBatch sprite)
        {
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                return;
            }

            DrawText(sprite, _statusMessage, StatusPaddingX, StatusPaddingY, StatusColor);
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, XnaColor color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            RasterTextTexture textTexture = GetOrCreateTextTexture(text, color);
            if (textTexture.Texture != null)
            {
                sprite.Draw(
                    textTexture.Texture,
                    new Vector2(Position.X + x + textTexture.OffsetX, Position.Y + y + textTexture.OffsetY),
                    XnaColor.White);
                return;
            }

            if (_font == null)
            {
                return;
            }

            sprite.DrawString(_font, text, new Vector2(Position.X + x, Position.Y + y), color, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        private Vector2 MeasureText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            RasterTextTexture textTexture = GetOrCreateTextTexture(text, ValueColor);
            Vector2 rasterSize = textTexture.Measurement;
            if (rasterSize != Vector2.Zero)
            {
                return rasterSize;
            }

            return _font == null ? Vector2.Zero : _font.MeasureString(text) * 0.5f;
        }

        private static bool IsJobName(string jobName, params string[] fragments)
        {
            if (string.IsNullOrWhiteSpace(jobName) || fragments == null)
            {
                return false;
            }

            foreach (string fragment in fragments)
            {
                if (jobName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatRank(int rank)
        {
            return rank > 0 ? rank.ToString() : "-";
        }

        private static SD.Font CreateBasicBlackFont(out string fontFamilyName)
        {
            if (TryCreateConfiguredBasicBlackFont(out SD.Font configuredFont, out fontFamilyName))
            {
                return configuredFont;
            }

            string selectedFamilyName = ResolveInstalledFontFamilyName(BasicBlackFontFamilyCandidates);

            fontFamilyName = selectedFamilyName;

            try
            {
                // Client inspection shows FONT_BASIC_BLACK is created from the same
                // underlying face resource used by the DODOOMCHE/basic-black variants.
                return new SD.Font(
                    selectedFamilyName,
                    BasicBlackFontHeight,
                    SD.FontStyle.Regular,
                    SD.GraphicsUnit.Pixel,
                    KoreanGdiCharset);
            }
            catch (ArgumentException)
            {
                return new SD.Font(selectedFamilyName, BasicBlackFontHeight, SD.FontStyle.Regular, SD.GraphicsUnit.Pixel);
            }
        }

        private static bool TryCreateConfiguredBasicBlackFont(out SD.Font font, out string fontFamilyName)
        {
            font = null;
            fontFamilyName = null;

            if (!TryResolveConfiguredBasicBlackFontPath(out string resolvedFontPath, out string configuredFontFace))
            {
                return false;
            }

            if (!BasicBlackPrivateFontRegistry.TryRegister(
                    resolvedFontPath,
                    CreatePreferredBasicBlackFamilyNames(configuredFontFace),
                    out string resolvedFamilyName))
            {
                return false;
            }

            fontFamilyName = resolvedFamilyName;

            try
            {
                font = new SD.Font(
                    resolvedFamilyName,
                    BasicBlackFontHeight,
                    SD.FontStyle.Regular,
                    SD.GraphicsUnit.Pixel,
                    KoreanGdiCharset);
                return true;
            }
            catch (ArgumentException)
            {
                font = new SD.Font(resolvedFamilyName, BasicBlackFontHeight, SD.FontStyle.Regular, SD.GraphicsUnit.Pixel);
                return true;
            }
        }

        private static bool TryResolveConfiguredBasicBlackFontPath(out string resolvedFontPath, out string configuredFontFace)
        {
            configuredFontFace = null;

            if (TryResolveFontPathFromEnvironment(
                    BasicBlackFontPathEnvironmentVariable,
                    BasicBlackFontFaceEnvironmentVariable,
                    out resolvedFontPath,
                    out configuredFontFace))
            {
                return true;
            }

            if (TryResolveFontPathFromEnvironment(
                    ClientTextFontPathEnvironmentVariable,
                    ClientTextFontFaceEnvironmentVariable,
                    out resolvedFontPath,
                    out configuredFontFace))
            {
                return true;
            }

            return TryResolveBundledBasicBlackFontPath(out resolvedFontPath);
        }

        private static bool TryResolveFontPathFromEnvironment(
            string pathEnvironmentVariable,
            string faceEnvironmentVariable,
            out string resolvedFontPath,
            out string configuredFontFace)
        {
            resolvedFontPath = null;
            configuredFontFace = null;

            string configuredFontPath = Environment.GetEnvironmentVariable(pathEnvironmentVariable);
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

            if (!TryResolveFontPath(candidatePath, out resolvedFontPath))
            {
                return false;
            }

            configuredFontFace = Environment.GetEnvironmentVariable(faceEnvironmentVariable);
            return true;
        }

        private static bool TryResolveBundledBasicBlackFontPath(out string resolvedFontPath)
        {
            foreach (string candidatePath in EnumerateBasicBlackFontCandidatePaths())
            {
                if (File.Exists(candidatePath))
                {
                    resolvedFontPath = candidatePath;
                    return true;
                }
            }

            resolvedFontPath = null;
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

        private static IEnumerable<string> EnumerateBasicBlackFontCandidatePaths()
        {
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

            foreach (string rootDirectory in EnumerateBasicBlackFontSearchRoots())
            {
                foreach (string candidatePath in EnumerateCandidateFontPaths(rootDirectory))
                {
                    if (seenPaths.Add(candidatePath))
                    {
                        yield return candidatePath;
                    }
                }
            }

            foreach (string registryFontPath in EnumerateRegisteredBasicBlackFontPaths())
            {
                if (seenPaths.Add(registryFontPath))
                {
                    yield return registryFontPath;
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

            foreach (string fileName in BasicBlackFontFileCandidates)
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
                recursiveMatches = Directory.EnumerateFiles(rootDirectory, "*", RecursiveFontSearchOptions);
            }
            catch
            {
                yield break;
            }

            foreach (string recursiveMatch in recursiveMatches)
            {
                string fileName = Path.GetFileName(recursiveMatch);
                if (BasicBlackFontFileCandidates.Any(candidate => string.Equals(candidate, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    yield return recursiveMatch;
                }
            }
        }

        private static IEnumerable<string> EnumerateBasicBlackFontSearchRoots()
        {
            HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);
            string windowsFontsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Fonts");

            foreach (string baseDirectory in EnumerateBaseFontSearchRoots(windowsFontsDirectory))
            {
                foreach (string nearbyDirectory in EnumerateNearbyRootDirectories(baseDirectory))
                {
                    foreach (string suffix in BasicBlackFontSearchDirectorySuffixes)
                    {
                        string candidate = string.IsNullOrEmpty(suffix)
                            ? nearbyDirectory
                            : Path.Combine(nearbyDirectory, suffix);

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

        private static IEnumerable<string> EnumerateBaseFontSearchRoots(string windowsFontsDirectory)
        {
            foreach (string baseDirectory in new[]
                     {
                         AppContext.BaseDirectory,
                         Environment.CurrentDirectory,
                         windowsFontsDirectory
                     })
            {
                if (!string.IsNullOrWhiteSpace(baseDirectory))
                {
                    yield return baseDirectory;
                }
            }

            foreach (string userFontsDirectory in PerUserWindowsFontDirectories)
            {
                if (!string.IsNullOrWhiteSpace(userFontsDirectory))
                {
                    yield return userFontsDirectory;
                }
            }

            foreach (string installDirectory in EnumerateMapleInstallDirectories())
            {
                yield return installDirectory;
            }
        }

        private static IEnumerable<string> EnumerateNearbyRootDirectories(string baseDirectory)
        {
            string currentDirectory = baseDirectory;
            for (int depth = 0; depth < 3 && !string.IsNullOrWhiteSpace(currentDirectory); depth++)
            {
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(currentDirectory);
                }
                catch
                {
                    yield break;
                }

                yield return fullPath;

                DirectoryInfo parentDirectory;
                try
                {
                    parentDirectory = Directory.GetParent(fullPath);
                }
                catch
                {
                    yield break;
                }

                currentDirectory = parentDirectory?.FullName;
            }
        }

        private static IEnumerable<string> EnumerateMapleInstallDirectories()
        {
            HashSet<string> seenDirectories = new(StringComparer.OrdinalIgnoreCase);

            foreach (string registrySubKey in MapleInstallRegistrySubKeys)
            {
                foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                {
                    RegistryKey baseKey;
                    try
                    {
                        baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
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

        private static IEnumerable<string> EnumerateRegisteredBasicBlackFontPaths()
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
                                if (!IsBasicBlackFontRegistryEntry(valueName) ||
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

        private static bool IsBasicBlackFontRegistryEntry(string valueName)
        {
            if (string.IsNullOrWhiteSpace(valueName))
            {
                return false;
            }

            foreach (string candidate in BasicBlackFontRegistryNameCandidates)
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

        private static IEnumerable<string> CreatePreferredBasicBlackFamilyNames(string configuredFontFace)
        {
            if (!string.IsNullOrWhiteSpace(configuredFontFace))
            {
                yield return configuredFontFace.Trim();
            }

            foreach (string candidate in BasicBlackFontFamilyCandidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    yield return candidate;
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

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetEntry(LoginCharacterRosterEntry entry, string statusMessage)
        {
            _entry = entry;
            _statusMessage = statusMessage ?? string.Empty;
        }

        private Vector2 MeasureRasterText(string text)
        {
            if (_basicBlackFont == null || string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            SD.Size size = SWF.TextRenderer.MeasureText(
                _measureGraphics,
                text,
                _basicBlackFont,
                new SD.Size(int.MaxValue, int.MaxValue),
                BasicBlackTextFormatFlags);

            return new Vector2(size.Width, size.Height);
        }

        private RasterTextTexture GetOrCreateTextTexture(string text, XnaColor color)
        {
            if (_basicBlackFont == null || string.IsNullOrEmpty(text))
            {
                return default;
            }

            TextRenderCacheKey cacheKey = new TextRenderCacheKey(text, color);
            if (_textTextureCache.TryGetValue(cacheKey, out RasterTextTexture cachedTexture) &&
                cachedTexture.Texture != null &&
                !cachedTexture.Texture.IsDisposed)
            {
                return cachedTexture;
            }

            Vector2 measuredSize = MeasureRasterText(text);
            int width = Math.Max(1, (int)measuredSize.X + (TextRasterPadding * 2));
            int height = Math.Max(1, (int)measuredSize.Y + (TextRasterPadding * 2));

            using var bitmap = new SD.Bitmap(width, height);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);
            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            SWF.TextRenderer.DrawText(
                graphics,
                text,
                _basicBlackFont,
                new SD.Rectangle(TextRasterPadding, TextRasterPadding, width - (TextRasterPadding * 2), height - (TextRasterPadding * 2)),
                SD.Color.FromArgb(color.A, color.R, color.G, color.B),
                SD.Color.Transparent,
                BasicBlackTextFormatFlags);

            RasterTextTexture texture = CreateRasterTextTexture(bitmap, measuredSize);
            _textTextureCache[cacheKey] = texture;
            return texture;
        }

        private RasterTextTexture CreateRasterTextTexture(SD.Bitmap bitmap, Vector2 measuredSize)
        {
            if (!TryFindOpaqueBounds(bitmap, out SD.Rectangle bounds))
            {
                return new RasterTextTexture(
                    bitmap.ToTexture2D(_panelTexture.GraphicsDevice),
                    0,
                    0,
                    measuredSize);
            }

            using SD.Bitmap croppedBitmap = bitmap.Clone(bounds, bitmap.PixelFormat);
            Texture2D texture = croppedBitmap.ToTexture2D(_panelTexture.GraphicsDevice);
            Vector2 measurement = new Vector2(
                Math.Max(0, bounds.Right - TextRasterPadding),
                Math.Max(0, bounds.Bottom - TextRasterPadding));

            return new RasterTextTexture(
                texture,
                bounds.X - TextRasterPadding,
                bounds.Y - TextRasterPadding,
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

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
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

        private readonly struct TextRenderCacheKey : IEquatable<TextRenderCacheKey>
        {
            public TextRenderCacheKey(string text, XnaColor color)
            {
                Text = text ?? string.Empty;
                Color = color.PackedValue;
            }

            public string Text { get; }
            public uint Color { get; }

            public bool Equals(TextRenderCacheKey other)
            {
                return Color == other.Color && string.Equals(Text, other.Text, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TextRenderCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Text, Color);
            }
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

        private static class BasicBlackPrivateFontRegistry
        {
            private const uint PrivateFontResourceFlag = 0x10;
            private static readonly object Sync = new object();
            private static readonly Dictionary<string, RegisteredPrivateFont> RegisteredFonts = new(StringComparer.OrdinalIgnoreCase);

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
                        if (!TryRegisterFileFont(fontPath, out RegisteredPrivateFont registeredFont) &&
                            !TryRegisterMemoryFont(fontPath, out registeredFont))
                        {
                            resolvedFamilyName = null;
                            return false;
                        }

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

            private static bool TryRegisterFileFont(string fontPath, out RegisteredPrivateFont registeredFont)
            {
                registeredFont = null;

                try
                {
                    var privateFonts = new SDText.PrivateFontCollection();
                    privateFonts.AddFontFile(fontPath);
                    IntPtr gdiHandle = AddFontResourceEx(fontPath, PrivateFontResourceFlag, IntPtr.Zero);
                    registeredFont = new RegisteredPrivateFont(IntPtr.Zero, privateFonts, gdiHandle);
                    return privateFonts.Families.Length > 0;
                }
                catch
                {
                    return false;
                }
            }

            private static bool TryRegisterMemoryFont(string fontPath, out RegisteredPrivateFont registeredFont)
            {
                registeredFont = null;

                try
                {
                    byte[] fontBytes = File.ReadAllBytes(fontPath);
                    if (fontBytes.Length == 0)
                    {
                        return false;
                    }

                    IntPtr fontData = Marshal.AllocCoTaskMem(fontBytes.Length);
                    Marshal.Copy(fontBytes, 0, fontData, fontBytes.Length);

                    var privateFonts = new SDText.PrivateFontCollection();
                    privateFonts.AddMemoryFont(fontData, fontBytes.Length);

                    uint fontsAdded = 0;
                    IntPtr gdiHandle = AddFontMemResourceEx(fontData, (uint)fontBytes.Length, IntPtr.Zero, ref fontsAdded);
                    registeredFont = new RegisteredPrivateFont(fontData, privateFonts, gdiHandle);
                    return privateFonts.Families.Length > 0;
                }
                catch
                {
                    return false;
                }
            }

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

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
