using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using MapleLib.Converters;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Dedicated Cookie House HUD runtime.
    /// IDA shows the client initializes a field-owned point layer and redraws
    /// it when the score changes, so the simulator keeps that state here.
    /// </summary>
    public sealed class CookieHouseField
    {
        private readonly record struct ClientStringPoolEvidence(
            int Id,
            byte Seed,
            string RawHex,
            string InferredDecodedValue,
            string ClientSource);

        private sealed class CookieBitmapNumberStyle
        {
            public Texture2D[] Digits { get; } = new Texture2D[10];
            public Point[] DigitOrigins { get; } = new Point[10];
            public Texture2D SignPlus { get; set; }
            public Point SignPlusOrigin { get; set; }
            public Texture2D SignMinus { get; set; }
            public Point SignMinusOrigin { get; set; }

            public bool IsLoaded => Digits.All(texture => texture != null);
        }

        private enum CookieBitmapRootResolutionKind
        {
            Unresolved,
            WzCandidate,
            CompatibleShape,
            CompatibleDigitContainer
        }

        private readonly struct CookieCanvasSprite
        {
            public CookieCanvasSprite(Texture2D texture, Point origin)
            {
                Texture = texture;
                Origin = origin;
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
            public bool IsLoaded => Texture != null;
        }

        public const int LayerWidth = 187;
        public const int LayerHeight = 45;
        public const int LayerOffsetX = -93;
        public const int LayerTopY = 92;
        public const int ScoreDrawX = 92;
        public const int ScoreDrawY = 9;
        public const int GradeCount = 5;
        internal const int ClientBackgroundStringPoolId = 0x13FB;
        internal const int ClientBitmapRootStringPoolId = 0x13FC;
        internal const int ClientBitmapPlusStringPoolId = 0x14FA;
        internal const int ClientBitmapMinusStringPoolId = 0x14F7;
        private const string ClientBackgroundClientStringPoolSource = "StringPool::ms_aString[0x13FB]";
        private const string ClientBitmapRootClientStringPoolSource = "StringPool::ms_aString[0x13FC]";
        private const string ClientBitmapPlusClientStringPoolSource = "StringPool::ms_aString[0x14FA]";
        private const string ClientBitmapMinusClientStringPoolSource = "StringPool::ms_aString[0x14F7]";
        private const int ClientBitmapDigitWidth = 27;
        private const int ClientBitmapDigitCount = 3;
        private const string FallbackBackgroundPath = "raise/backgrnd";
        private static readonly string[] BitmapNumberSignPlusNames = { "plus", "signPlus", "Plus", "SignPlus" };
        private static readonly string[] BitmapNumberSignMinusNames = { "minus", "signMinus", "Minus", "SignMinus" };
        private static readonly string[] PreferredUiWindowImages = { "UIWindow.img", "UIWindow2.img" };
        private static readonly string[] PreferredBitmapRootCandidatePaths = { "raise/30", "raise/31" };
        private static readonly string[] PreferredCompatibleDigitContainerCandidatePaths =
        {
            "MonsterKilling/Result/number2",
            "MonsterKilling/Count/number2",
            "PartyRace/Stage/number2",
            "PartyRace/Result/number2"
        };
        private const string ClientPreferredBitmapRootHint = "raise";

        // Recovered from MapleStory.exe v95:
        // s_nGradeCount @ 0x00C568DC == 4 and s_anGrade @ 0x00C568CC == { 90, 160, 230, 350 }.
        // The client returns style index 4 when the point total reaches or exceeds the final threshold.
        public static readonly int[] GradeThresholds = { 90, 160, 230, 350 };
        private static readonly ClientStringPoolEvidence BackgroundStringPoolEvidence = new(
            ClientBackgroundStringPoolId,
            0x32,
            "32 C0 A6 D3 B6 A0 2E A6 EC F9 CF 38 57 BF 74 7E BD E8 B1 C6 F7 9B 1C A3 AA F2 CF 18 16 A3 77 6D BD EF A6 C0 F2 88 3E A2 A7",
            "UI/UIWindow.img/raise/backgrnd",
            "CField_CookieHouse::Init / MapleStory.exe ms_aString");
        private static readonly ClientStringPoolEvidence BitmapRootStringPoolEvidence = new(
            ClientBitmapRootStringPoolId,
            0x33,
            "33 56 EE 37 1C 91 FB F3 A8 5C 02 D5 DD C5 5F 54 0A 7E F9 22 5D AA C9 F6 EE 57 02 F5 9C D9 5C 47",
            null,
            "CField_CookieHouse::Init / MapleStory.exe ms_aString");
        private static readonly ClientStringPoolEvidence BitmapPlusStringPoolEvidence = new(
            ClientBitmapPlusStringPoolId,
            0x31,
            "31 36 8F A4 BF",
            "plus",
            "CBitmapNumber::CBitmapNumber / MapleStory.exe ms_aString");
        private static readonly ClientStringPoolEvidence BitmapMinusStringPoolEvidence = new(
            ClientBitmapMinusStringPoolId,
            0x2E,
            "2E 45 B5 14 4C ED",
            "minus",
            "CBitmapNumber::CBitmapNumber / MapleStory.exe ms_aString");

        private bool _isActive;
        private int _mapId;
        private int _point;
        private int _gradeIndex;
        private Func<int> _pointProvider;
        private bool _assetsLoaded;
        private GraphicsDevice _graphicsDevice;
        private SpriteBatch _hudSpriteBatch;
        private RenderTarget2D _hudRenderTarget;
        private bool _hudDirty;
        private CookieCanvasSprite _backgroundTopLeft;
        private CookieCanvasSprite _backgroundTopCenter;
        private CookieCanvasSprite _backgroundTopRight;
        private CookieCanvasSprite _backgroundCenterLeft;
        private CookieCanvasSprite _backgroundCenterCenter;
        private CookieCanvasSprite _backgroundCenterRight;
        private CookieCanvasSprite _backgroundBottomLeft;
        private CookieCanvasSprite _backgroundBottomCenter;
        private CookieCanvasSprite _backgroundBottomRight;
        private readonly CookieBitmapNumberStyle[] _bitmapNumberStyles = Enumerable.Range(0, GradeCount)
            .Select(_ => new CookieBitmapNumberStyle())
            .ToArray();
        private string _backgroundSourcePath;
        private string _bitmapNumberSourcePath;
        private bool _usesFallbackBackgroundSource;
        private bool _usesFallbackBitmapSource;
        private CookieBitmapRootResolutionKind _bitmapRootResolutionKind;

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int Point => _point;
        public int GradeIndex => _gradeIndex;
        internal static IReadOnlyList<string> BitmapRootCandidatePaths => PreferredBitmapRootCandidatePaths;
        internal static IReadOnlyList<string> CompatibleDigitContainerCandidatePaths => PreferredCompatibleDigitContainerCandidatePaths;

        public void Enable(int mapId, Func<int> pointProvider = null)
        {
            _isActive = true;
            _mapId = mapId;
            _pointProvider = pointProvider;
            _hudDirty = true;
            OnPointUpdate(pointProvider?.Invoke() ?? 0);
        }

        public void Update()
        {
            if (!_isActive || _pointProvider == null)
            {
                return;
            }

            int nextPoint = Math.Max(0, _pointProvider());
            if (nextPoint != _point)
            {
                OnPointUpdate(nextPoint);
            }
        }

        public void OnPointUpdate(int newPoint)
        {
            _point = Math.Max(0, newPoint);
            _gradeIndex = FindGrade(_point);
            _hudDirty = true;
        }

        public string DescribeStatus()
        {
            return $"Cookie House map={_mapId}, point={_point}, grade={_gradeIndex + 1}/{GradeCount}, thresholds=s_anGrade[{string.Join(",", GradeThresholds)}], {DescribeBackgroundSource()}, {DescribeBitmapSource()}";
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int centerX)
        {
            if (!_isActive || spriteBatch == null)
            {
                return;
            }

            EnsureAssetsLoaded(spriteBatch.GraphicsDevice);
            EnsureHudRenderTarget(spriteBatch.GraphicsDevice);

            int left = centerX + LayerOffsetX;
            RedrawHudTextureIfNeeded(pixelTexture, font);

            if (_hudRenderTarget != null)
            {
                spriteBatch.Draw(_hudRenderTarget, new Vector2(left, LayerTopY), Color.White);
            }
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _point = 0;
            _gradeIndex = 0;
            _pointProvider = null;
            _hudDirty = true;
            _bitmapRootResolutionKind = CookieBitmapRootResolutionKind.Unresolved;
        }

        private static int FindGrade(int point)
        {
            for (int i = 0; i < GradeThresholds.Length; i++)
            {
                if (point < GradeThresholds[i])
                {
                    return i;
                }
            }

            return GradeCount - 1;
        }

        internal static int FindGradeForTesting(int point)
        {
            return FindGrade(point);
        }

        internal static bool MatchesBitmapRootCandidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalizedPath = path.Replace('\\', '/');
            return PreferredBitmapRootCandidatePaths.Any(candidate =>
                normalizedPath.EndsWith(candidate, StringComparison.OrdinalIgnoreCase));
        }

        internal static bool MatchesCompatibleDigitContainerCandidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalizedPath = path.Replace('\\', '/');
            return PreferredCompatibleDigitContainerCandidatePaths.Any(candidate =>
                normalizedPath.EndsWith(candidate, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureAssetsLoaded(GraphicsDevice graphicsDevice)
        {
            if (_assetsLoaded || graphicsDevice == null)
            {
                return;
            }

            _graphicsDevice = graphicsDevice;
            _hudSpriteBatch ??= new SpriteBatch(graphicsDevice);

            WzImageProperty background = null;
            _backgroundSourcePath = null;
            _usesFallbackBackgroundSource = false;
            foreach (string imageName in PreferredUiWindowImages)
            {
                WzImage uiWindow = global::HaCreator.Program.FindImage("UI", imageName);
                WzImageProperty candidate = ResolvePropertyPath(uiWindow, FallbackBackgroundPath);
                if (candidate != null)
                {
                    background = candidate;
                    _backgroundSourcePath = $"UI/{imageName}/{FallbackBackgroundPath}";
                    _usesFallbackBackgroundSource = !string.Equals(imageName, PreferredUiWindowImages[0], StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }

            _backgroundTopLeft = LoadCanvas(background?["top"]?["left"]);
            _backgroundTopCenter = LoadCanvas(background?["top"]?["center"]);
            _backgroundTopRight = LoadCanvas(background?["top"]?["right"]);
            _backgroundCenterLeft = LoadCanvas(background?["center"]?["left"]);
            _backgroundCenterCenter = LoadCanvas(background?["center"]?["center"]);
            _backgroundCenterRight = LoadCanvas(background?["center"]?["right"]);
            _backgroundBottomLeft = LoadCanvas(background?["bottom"]?["left"]);
            _backgroundBottomCenter = LoadCanvas(background?["bottom"]?["center"]);
            _backgroundBottomRight = LoadCanvas(background?["bottom"]?["right"]);
            TryLoadBitmapNumberStyles();
            _assetsLoaded = true;
            _hudDirty = true;
        }

        private void TryLoadBitmapNumberStyles()
        {
            foreach (CookieBitmapNumberStyle style in _bitmapNumberStyles)
            {
                Array.Clear(style.Digits, 0, style.Digits.Length);
                Array.Clear(style.DigitOrigins, 0, style.DigitOrigins.Length);
                style.SignPlus = null;
                style.SignPlusOrigin = new Point(0, 0);
                style.SignMinus = null;
                style.SignMinusOrigin = new Point(0, 0);
            }

            _bitmapNumberSourcePath = null;
            _usesFallbackBitmapSource = false;
            _bitmapRootResolutionKind = CookieBitmapRootResolutionKind.Unresolved;
            if (TryLoadPreferredBitmapNumberRoots())
            {
                return;
            }

            if (TryLoadPreferredCompatibleDigitContainers())
            {
                return;
            }

            foreach (WzImage image in EnumerateUiImages())
            {
                if (image == null)
                {
                    continue;
                }

                string imagePath = $"UI/{image.Name}";
                if (TryFindBitmapNumberRoot(image, imagePath, out WzImageProperty discoveredRoot, out string discoveredPath)
                    && TryLoadBitmapNumberStyles(discoveredRoot))
                {
                    _bitmapNumberSourcePath = discoveredPath;
                    _usesFallbackBitmapSource = !IsPreferredUiWindowImage(image.Name);
                    _bitmapRootResolutionKind = CookieBitmapRootResolutionKind.CompatibleShape;
                    return;
                }
            }

            foreach (WzImage image in EnumerateUiImages())
            {
                if (image == null)
                {
                    continue;
                }

                string imagePath = $"UI/{image.Name}";
                if (TryFindCompatibleBitmapDigitContainer(image, imagePath, out WzImageProperty digitContainer, out string digitContainerPath)
                    && TryLoadCompatibleBitmapNumberStyles(digitContainer))
                {
                    _bitmapNumberSourcePath = digitContainerPath;
                    _usesFallbackBitmapSource = !IsPreferredUiWindowImage(image.Name);
                    _bitmapRootResolutionKind = CookieBitmapRootResolutionKind.CompatibleDigitContainer;
                    return;
                }
            }
        }

        private bool TryLoadPreferredBitmapNumberRoots()
        {
            foreach (WzImage image in EnumerateUiImages())
            {
                if (image == null)
                {
                    continue;
                }

                string imagePath = $"UI/{image.Name}";
                foreach (string candidatePath in PreferredBitmapRootCandidatePaths)
                {
                    WzImageProperty candidateRoot = ResolvePropertyPath(image, candidatePath);
                    if (candidateRoot == null || !TryLoadBitmapNumberStyles(candidateRoot))
                    {
                        continue;
                    }

                    _bitmapNumberSourcePath = $"{imagePath}/{candidatePath}";
                    _usesFallbackBitmapSource = !IsPreferredUiWindowImage(image.Name);
                    _bitmapRootResolutionKind = CookieBitmapRootResolutionKind.WzCandidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryLoadPreferredCompatibleDigitContainers()
        {
            foreach (WzImage image in EnumerateUiImages())
            {
                if (image == null)
                {
                    continue;
                }

                string imagePath = $"UI/{image.Name}";
                foreach (string candidatePath in PreferredCompatibleDigitContainerCandidatePaths)
                {
                    WzImageProperty digitContainer = ResolvePropertyPath(image, candidatePath);
                    if (digitContainer == null || !TryLoadCompatibleBitmapNumberStyles(digitContainer))
                    {
                        continue;
                    }

                    _bitmapNumberSourcePath = $"{imagePath}/{candidatePath}";
                    _usesFallbackBitmapSource = !IsPreferredUiWindowImage(image.Name);
                    _bitmapRootResolutionKind = CookieBitmapRootResolutionKind.CompatibleDigitContainer;
                    return true;
                }
            }

            return false;
        }

        private CookieCanvasSprite LoadCanvas(WzImageProperty source)
        {
            if (_graphicsDevice == null || source == null)
            {
                return default;
            }

            if (WzInfoTools.GetRealProperty(source) is not WzCanvasProperty canvas)
            {
                return default;
            }

            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap == null)
            {
                return default;
            }

            Texture2D texture = bitmap.ToTexture2DAndDispose(_graphicsDevice);
            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            return new CookieCanvasSprite(texture, new Point((int)origin.X, (int)origin.Y));
        }

        private bool TryDrawBackground(SpriteBatch spriteBatch, Rectangle bounds)
        {
            if (!_backgroundTopLeft.IsLoaded
                || !_backgroundTopCenter.IsLoaded
                || !_backgroundTopRight.IsLoaded
                || !_backgroundCenterLeft.IsLoaded
                || !_backgroundCenterCenter.IsLoaded
                || !_backgroundCenterRight.IsLoaded
                || !_backgroundBottomLeft.IsLoaded
                || !_backgroundBottomCenter.IsLoaded
                || !_backgroundBottomRight.IsLoaded)
            {
                return false;
            }

            int topHeight = _backgroundTopLeft.Texture.Height;
            int bottomHeight = _backgroundBottomLeft.Texture.Height;
            int centerHeight = Math.Max(1, bounds.Height - topHeight - bottomHeight);
            int leftWidth = _backgroundTopLeft.Texture.Width;
            int rightWidth = _backgroundTopRight.Texture.Width;
            int centerWidth = Math.Max(1, bounds.Width - leftWidth - rightWidth);

            spriteBatch.Draw(_backgroundTopLeft.Texture, new Rectangle(bounds.Left, bounds.Top, leftWidth, topHeight), Color.White);
            spriteBatch.Draw(_backgroundTopCenter.Texture, new Rectangle(bounds.Left + leftWidth, bounds.Top, centerWidth, topHeight), Color.White);
            spriteBatch.Draw(_backgroundTopRight.Texture, new Rectangle(bounds.Right - rightWidth, bounds.Top, rightWidth, topHeight), Color.White);

            int centerTop = bounds.Top + topHeight;
            spriteBatch.Draw(_backgroundCenterLeft.Texture, new Rectangle(bounds.Left, centerTop, leftWidth, centerHeight), Color.White);
            spriteBatch.Draw(_backgroundCenterCenter.Texture, new Rectangle(bounds.Left + leftWidth, centerTop, centerWidth, centerHeight), Color.White);
            spriteBatch.Draw(_backgroundCenterRight.Texture, new Rectangle(bounds.Right - rightWidth, centerTop, rightWidth, centerHeight), Color.White);

            int bottomTop = bounds.Bottom - bottomHeight;
            spriteBatch.Draw(_backgroundBottomLeft.Texture, new Rectangle(bounds.Left, bottomTop, leftWidth, bottomHeight), Color.White);
            spriteBatch.Draw(_backgroundBottomCenter.Texture, new Rectangle(bounds.Left + leftWidth, bottomTop, centerWidth, bottomHeight), Color.White);
            spriteBatch.Draw(_backgroundBottomRight.Texture, new Rectangle(bounds.Right - rightWidth, bottomTop, rightWidth, bottomHeight), Color.White);
            return true;
        }

        private bool TryLoadBitmapNumberStyles(WzImageProperty sourceRoot)
        {
            if (sourceRoot == null)
            {
                return false;
            }

            for (int i = 0; i < GradeCount; i++)
            {
                WzImageProperty styleRoot = sourceRoot[i.ToString()];
                WzImageProperty digitContainer = ResolveCookieDigitContainer(styleRoot);
                if (digitContainer == null)
                {
                    return false;
                }

                for (int digit = 0; digit < 10; digit++)
                {
                    WzCanvasProperty digitCanvas = ResolveCookieCanvas(digitContainer[digit.ToString()]);
                    _bitmapNumberStyles[i].Digits[digit] = LoadCanvasTexture(digitCanvas);
                    _bitmapNumberStyles[i].DigitOrigins[digit] = ResolveCanvasOrigin(digitCanvas);
                }

                if (!_bitmapNumberStyles[i].IsLoaded)
                {
                    return false;
                }

                WzCanvasProperty plusCanvas = ResolveNamedCanvas(styleRoot, BitmapNumberSignPlusNames)
                    ?? ResolveNamedCanvas(digitContainer, BitmapNumberSignPlusNames);
                if (plusCanvas != null)
                {
                    _bitmapNumberStyles[i].SignPlus = LoadCanvasTexture(plusCanvas);
                    _bitmapNumberStyles[i].SignPlusOrigin = ResolveCanvasOrigin(plusCanvas);
                }

                WzCanvasProperty minusCanvas = ResolveNamedCanvas(styleRoot, BitmapNumberSignMinusNames)
                    ?? ResolveNamedCanvas(digitContainer, BitmapNumberSignMinusNames);
                if (minusCanvas != null)
                {
                    _bitmapNumberStyles[i].SignMinus = LoadCanvasTexture(minusCanvas);
                    _bitmapNumberStyles[i].SignMinusOrigin = ResolveCanvasOrigin(minusCanvas);
                }

                if (_bitmapNumberStyles[i].SignPlus == null || _bitmapNumberStyles[i].SignMinus == null)
                {
                    var unnamedCanvases = GetNonDigitCanvases(styleRoot)
                        .Concat(GetNonDigitCanvases(digitContainer))
                        .Distinct()
                        .ToList();
                    if (_bitmapNumberStyles[i].SignPlus == null && unnamedCanvases.Count >= 1)
                    {
                        _bitmapNumberStyles[i].SignPlus = LoadCanvasTexture(unnamedCanvases[0]);
                        _bitmapNumberStyles[i].SignPlusOrigin = ResolveCanvasOrigin(unnamedCanvases[0]);
                    }

                    if (_bitmapNumberStyles[i].SignMinus == null && unnamedCanvases.Count >= 2)
                    {
                        _bitmapNumberStyles[i].SignMinus = LoadCanvasTexture(unnamedCanvases[1]);
                        _bitmapNumberStyles[i].SignMinusOrigin = ResolveCanvasOrigin(unnamedCanvases[1]);
                    }
                }

                if (_bitmapNumberStyles[i].SignMinus == null)
                {
                    _bitmapNumberStyles[i].SignMinus = _bitmapNumberStyles[i].SignPlus;
                    _bitmapNumberStyles[i].SignMinusOrigin = _bitmapNumberStyles[i].SignPlusOrigin;
                }
            }

            return true;
        }

        private bool TryLoadCompatibleBitmapNumberStyles(WzImageProperty digitContainer)
        {
            if (digitContainer == null)
            {
                return false;
            }

            var compatibleStyle = new CookieBitmapNumberStyle();
            for (int digit = 0; digit < 10; digit++)
            {
                WzCanvasProperty digitCanvas = ResolveCookieCanvas(digitContainer[digit.ToString()]);
                compatibleStyle.Digits[digit] = LoadCanvasTexture(digitCanvas);
                compatibleStyle.DigitOrigins[digit] = ResolveCanvasOrigin(digitCanvas);
            }

            if (!compatibleStyle.IsLoaded)
            {
                return false;
            }

            WzCanvasProperty plusCanvas = ResolveNamedCanvas(digitContainer, BitmapNumberSignPlusNames);
            if (plusCanvas != null)
            {
                compatibleStyle.SignPlus = LoadCanvasTexture(plusCanvas);
                compatibleStyle.SignPlusOrigin = ResolveCanvasOrigin(plusCanvas);
            }

            WzCanvasProperty minusCanvas = ResolveNamedCanvas(digitContainer, BitmapNumberSignMinusNames);
            if (minusCanvas != null)
            {
                compatibleStyle.SignMinus = LoadCanvasTexture(minusCanvas);
                compatibleStyle.SignMinusOrigin = ResolveCanvasOrigin(minusCanvas);
            }

            if (compatibleStyle.SignPlus == null || compatibleStyle.SignMinus == null)
            {
                var unnamedCanvases = GetNonDigitCanvases(digitContainer).ToList();
                if (compatibleStyle.SignPlus == null && unnamedCanvases.Count >= 1)
                {
                    compatibleStyle.SignPlus = LoadCanvasTexture(unnamedCanvases[0]);
                    compatibleStyle.SignPlusOrigin = ResolveCanvasOrigin(unnamedCanvases[0]);
                }

                if (compatibleStyle.SignMinus == null && unnamedCanvases.Count >= 2)
                {
                    compatibleStyle.SignMinus = LoadCanvasTexture(unnamedCanvases[1]);
                    compatibleStyle.SignMinusOrigin = ResolveCanvasOrigin(unnamedCanvases[1]);
                }
            }

            if (compatibleStyle.SignMinus == null)
            {
                compatibleStyle.SignMinus = compatibleStyle.SignPlus;
                compatibleStyle.SignMinusOrigin = compatibleStyle.SignPlusOrigin;
            }

            for (int i = 0; i < GradeCount; i++)
            {
                Array.Copy(compatibleStyle.Digits, _bitmapNumberStyles[i].Digits, compatibleStyle.Digits.Length);
                Array.Copy(compatibleStyle.DigitOrigins, _bitmapNumberStyles[i].DigitOrigins, compatibleStyle.DigitOrigins.Length);
                _bitmapNumberStyles[i].SignPlus = compatibleStyle.SignPlus;
                _bitmapNumberStyles[i].SignPlusOrigin = compatibleStyle.SignPlusOrigin;
                _bitmapNumberStyles[i].SignMinus = compatibleStyle.SignMinus;
                _bitmapNumberStyles[i].SignMinusOrigin = compatibleStyle.SignMinusOrigin;
            }

            return true;
        }

        private void EnsureHudRenderTarget(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                return;
            }

            if (_hudRenderTarget != null
                && !_hudRenderTarget.IsDisposed
                && _hudRenderTarget.GraphicsDevice == graphicsDevice
                && _hudRenderTarget.Width == LayerWidth
                && _hudRenderTarget.Height == LayerHeight)
            {
                return;
            }

            _hudRenderTarget?.Dispose();
            _hudRenderTarget = new RenderTarget2D(
                graphicsDevice,
                LayerWidth,
                LayerHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None);
            _hudDirty = true;
        }

        private void RedrawHudTextureIfNeeded(Texture2D pixelTexture, SpriteFont font)
        {
            if (!_hudDirty || _hudRenderTarget == null || _graphicsDevice == null || _hudSpriteBatch == null)
            {
                return;
            }

            RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
            Viewport previousViewport = _graphicsDevice.Viewport;

            _graphicsDevice.SetRenderTarget(_hudRenderTarget);
            _graphicsDevice.Clear(Color.Transparent);

            Rectangle panelBounds = new(0, 0, LayerWidth, LayerHeight);

            _hudSpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            if (TryDrawBackground(_hudSpriteBatch, panelBounds))
            {
            }
            else if (pixelTexture != null)
            {
                _hudSpriteBatch.Draw(pixelTexture, panelBounds, new Color(37, 26, 18, 220));
            }

            if (!TryDrawBitmapNumber(_hudSpriteBatch, _point, new Vector2(ScoreDrawX, ScoreDrawY)))
            {
                if (font != null)
                {
                    string scoreText = _point.ToString();
                    Vector2 textSize = font.MeasureString(scoreText);
                    Vector2 textPosition = new(
                        ScoreDrawX - (textSize.X / 2f),
                        ScoreDrawY);
                    _hudSpriteBatch.DrawString(font, scoreText, textPosition, new Color(56, 32, 20));
                }
            }

            _hudSpriteBatch.End();

            _graphicsDevice.SetRenderTargets(previousTargets);
            _graphicsDevice.Viewport = previousViewport;
            _hudDirty = false;
        }

        private bool TryDrawBitmapNumber(SpriteBatch spriteBatch, int value, Vector2 topCenter)
        {
            CookieBitmapNumberStyle style = _bitmapNumberStyles[Math.Clamp(_gradeIndex, 0, _bitmapNumberStyles.Length - 1)];
            if (!style.IsLoaded)
            {
                return false;
            }

            int remaining = Math.Abs(value);
            int slotIndex = ClientBitmapDigitCount - 1;
            int digitsDrawn = 0;
            bool drewDigit = false;
            do
            {
                int digit = remaining % 10;
                Texture2D digitTexture = style.Digits[digit];
                if (digitTexture == null)
                {
                    return false;
                }

                Point origin = style.DigitOrigins[digit];
                float drawX = topCenter.X + (slotIndex * ClientBitmapDigitWidth) - origin.X;
                float drawY = topCenter.Y - origin.Y;
                spriteBatch.Draw(digitTexture, new Vector2(drawX, drawY), Color.White);

                drewDigit = true;
                digitsDrawn++;
                remaining /= 10;
                slotIndex--;
            }
            while (remaining > 0 && slotIndex >= 0);

            Texture2D signTexture = value < 0
                ? style.SignMinus ?? style.SignPlus
                : style.SignPlus;
            Point signOrigin = value < 0
                ? (style.SignMinus != null ? style.SignMinusOrigin : style.SignPlusOrigin)
                : style.SignPlusOrigin;
            if (signTexture != null && digitsDrawn > 0)
            {
                int signSlotIndex = ClientBitmapDigitCount - digitsDrawn;
                float drawX = topCenter.X + (signSlotIndex * ClientBitmapDigitWidth) - signOrigin.X;
                float drawY = topCenter.Y - signOrigin.Y;
                spriteBatch.Draw(signTexture, new Vector2(drawX, drawY), Color.White);
            }

            return drewDigit;
        }

        private Texture2D LoadCanvasTexture(WzImageProperty source)
        {
            return LoadCanvasTexture(ResolveCookieCanvas(source));
        }

        private Texture2D LoadCanvasTexture(WzCanvasProperty canvas)
        {
            if (_graphicsDevice == null)
            {
                return null;
            }

            if (canvas == null)
            {
                return null;
            }

            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            return bitmap?.ToTexture2DAndDispose(_graphicsDevice);
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvas)
        {
            if (canvas == null)
            {
                return new Point(0, 0);
            }

            System.Drawing.PointF origin = canvas.GetCanvasOriginPosition();
            return new Point((int)origin.X, (int)origin.Y);
        }

        private static WzCanvasProperty ResolveCookieCanvas(WzImageProperty property)
        {
            if (WzInfoTools.GetRealProperty(property) is WzCanvasProperty canvas)
            {
                return canvas;
            }

            return property?.WzProperties?.OfType<WzCanvasProperty>().FirstOrDefault();
        }

        private static WzImageProperty ResolvePropertyPath(WzImage image, string path)
        {
            if (image == null || string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            WzImageProperty current = null;
            string[] segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length; i++)
            {
                current = i == 0
                    ? image[segments[i]]
                    : current?[segments[i]];

                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private static WzImageProperty ResolveCookieDigitContainer(WzImageProperty styleRoot)
        {
            if (HasDigitRange(styleRoot))
            {
                return styleRoot;
            }

            if (styleRoot?.WzProperties == null)
            {
                return null;
            }

            foreach (WzImageProperty child in styleRoot.WzProperties)
            {
                WzImageProperty resolvedChild = WzInfoTools.GetRealProperty(child) ?? child;
                if (HasDigitRange(resolvedChild))
                {
                    return resolvedChild;
                }
            }

            return null;
        }

        private static WzCanvasProperty ResolveNamedCanvas(WzImageProperty property, IEnumerable<string> names)
        {
            if (property == null || names == null)
            {
                return null;
            }

            foreach (string name in names)
            {
                WzCanvasProperty canvas = ResolveCookieCanvas(property[name]);
                if (canvas != null)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static IEnumerable<WzCanvasProperty> GetNonDigitCanvases(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                yield break;
            }

            foreach (WzImageProperty child in property.WzProperties)
            {
                if (int.TryParse(child?.Name, out _))
                {
                    continue;
                }

                WzCanvasProperty canvas = ResolveCookieCanvas(child);
                if (canvas != null)
                {
                    yield return canvas;
                }
            }
        }

        private static bool HasDigitRange(WzImageProperty property)
        {
            if (property?.WzProperties == null)
            {
                return false;
            }

            for (int i = 0; i < 10; i++)
            {
                if (ResolveCookieCanvas(property[i.ToString()]) == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<WzImage> EnumerateUiImages()
        {
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string imageName in PreferredUiWindowImages)
            {
                WzImage preferredImage = global::HaCreator.Program.FindImage("UI", imageName);
                if (preferredImage != null && seenNames.Add(preferredImage.Name))
                {
                    yield return preferredImage;
                }
            }

            foreach (WzDirectory directory in global::HaCreator.Program.GetDirectories("UI"))
            {
                if (directory == null)
                {
                    continue;
                }

                foreach (WzImage image in EnumerateImagesRecursive(directory))
                {
                    if (image != null && seenNames.Add(image.Name))
                    {
                        yield return image;
                    }
                }
            }
        }

        private static IEnumerable<WzImage> EnumerateImagesRecursive(WzDirectory directory)
        {
            if (directory == null)
            {
                yield break;
            }

            foreach (WzImage image in directory.WzImages)
            {
                if (image != null)
                {
                    yield return image;
                }
            }

            foreach (WzDirectory childDirectory in directory.WzDirectories)
            {
                foreach (WzImage image in EnumerateImagesRecursive(childDirectory))
                {
                    yield return image;
                }
            }
        }

        private static bool IsPreferredUiWindowImage(string imageName)
        {
            return PreferredUiWindowImages.Any(name => string.Equals(name, imageName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryFindBitmapNumberRoot(WzImage image, string currentPath, out WzImageProperty sourceRoot, out string sourcePath)
        {
            sourceRoot = null;
            sourcePath = null;
            if (image == null)
            {
                return false;
            }

            int bestScore = int.MinValue;
            foreach (WzImageProperty child in image.WzProperties)
            {
                string childPath = string.IsNullOrEmpty(currentPath)
                    ? child?.Name
                    : $"{currentPath}/{child?.Name}";
                TryFindBitmapNumberRootRecursive(WzInfoTools.GetRealProperty(child) ?? child, childPath, ref sourceRoot, ref sourcePath, ref bestScore);
            }

            return sourceRoot != null;
        }

        private static bool TryFindBitmapNumberRoot(WzImageProperty node, string currentPath, out WzImageProperty sourceRoot, out string sourcePath)
        {
            sourceRoot = null;
            sourcePath = null;
            int bestScore = int.MinValue;
            TryFindBitmapNumberRootRecursive(WzInfoTools.GetRealProperty(node) ?? node, currentPath, ref sourceRoot, ref sourcePath, ref bestScore);
            return sourceRoot != null;
        }

        private static bool TryFindCompatibleBitmapDigitContainer(WzImage image, string currentPath, out WzImageProperty digitContainer, out string digitContainerPath)
        {
            digitContainer = null;
            digitContainerPath = null;
            if (image == null)
            {
                return false;
            }

            int bestScore = int.MinValue;
            foreach (WzImageProperty child in image.WzProperties)
            {
                string childPath = string.IsNullOrEmpty(currentPath)
                    ? child?.Name
                    : $"{currentPath}/{child?.Name}";
                TryFindCompatibleBitmapDigitContainerRecursive(
                    WzInfoTools.GetRealProperty(child) ?? child,
                    childPath,
                    ref digitContainer,
                    ref digitContainerPath,
                    ref bestScore);
            }

            return digitContainer != null;
        }

        private static void TryFindBitmapNumberRootRecursive(
            WzImageProperty node,
            string currentPath,
            ref WzImageProperty bestRoot,
            ref string bestPath,
            ref int bestScore)
        {
            if (node == null)
            {
                return;
            }

            int candidateScore = ScoreBitmapNumberRoot(node, currentPath);
            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                bestRoot = node;
                bestPath = currentPath;
            }

            if (node.WzProperties == null)
            {
                return;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                string childPath = string.IsNullOrWhiteSpace(currentPath)
                    ? child?.Name ?? string.Empty
                    : $"{currentPath}/{child?.Name}";
                TryFindBitmapNumberRootRecursive(WzInfoTools.GetRealProperty(child) ?? child, childPath, ref bestRoot, ref bestPath, ref bestScore);
            }
        }

        private static void TryFindCompatibleBitmapDigitContainerRecursive(
            WzImageProperty node,
            string currentPath,
            ref WzImageProperty bestContainer,
            ref string bestPath,
            ref int bestScore)
        {
            if (node == null)
            {
                return;
            }

            int candidateScore = ScoreCompatibleBitmapDigitContainer(node, currentPath);
            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                bestContainer = node;
                bestPath = currentPath;
            }

            if (node.WzProperties == null)
            {
                return;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                string childPath = string.IsNullOrWhiteSpace(currentPath)
                    ? child?.Name ?? string.Empty
                    : $"{currentPath}/{child?.Name}";
                TryFindCompatibleBitmapDigitContainerRecursive(
                    WzInfoTools.GetRealProperty(child) ?? child,
                    childPath,
                    ref bestContainer,
                    ref bestPath,
                    ref bestScore);
            }
        }

        private static int ScoreBitmapNumberRoot(WzImageProperty root, string path)
        {
            if (root?.WzProperties == null)
            {
                return int.MinValue;
            }

            var digitContainers = new List<WzImageProperty>(GradeCount);
            int signCanvasCount = 0;
            for (int i = 0; i < GradeCount; i++)
            {
                WzImageProperty styleRoot = root[i.ToString()];
                WzImageProperty digitContainer = ResolveCookieDigitContainer(styleRoot);
                if (digitContainer == null)
                {
                    return int.MinValue;
                }

                digitContainers.Add(digitContainer);
                if (ResolveNamedCanvas(styleRoot, BitmapNumberSignPlusNames) != null)
                {
                    signCanvasCount++;
                }
            }

            int totalWidth = 0;
            int totalHeight = 0;
            foreach (WzImageProperty digitContainer in digitContainers)
            {
                for (int digit = 0; digit < 10; digit++)
                {
                    WzCanvasProperty canvas = ResolveCookieCanvas(digitContainer[digit.ToString()]);
                    if (canvas == null)
                    {
                        return int.MinValue;
                    }

                    totalWidth += canvas.PngProperty?.Width ?? 0;
                    totalHeight += canvas.PngProperty?.Height ?? 0;
                }
            }

            float averageWidth = totalWidth / (float)(GradeCount * 10);
            float averageHeight = totalHeight / (float)(GradeCount * 10);

            int score = 0;
            score -= (int)Math.Abs(averageWidth - 27f) * 4;
            score -= (int)Math.Abs(averageHeight - 32f) * 2;
            int propertyCount = root.WzProperties?.Count ?? 0;
            score -= Math.Abs(propertyCount - GradeCount) * 3;
            score += signCanvasCount * 8;
            score += digitContainers.Count(container => GetNonDigitCanvases(container).Any()) * 8;

            if (path.Contains(ClientPreferredBitmapRootHint, StringComparison.OrdinalIgnoreCase))
            {
                score += 60;
            }

            if (path.Contains("number", StringComparison.OrdinalIgnoreCase))
            {
                score += 20;
            }

            return score;
        }

        private static int ScoreCompatibleBitmapDigitContainer(WzImageProperty digitContainer, string path)
        {
            if (!HasDigitRange(digitContainer))
            {
                return int.MinValue;
            }

            int totalWidth = 0;
            int totalHeight = 0;
            for (int digit = 0; digit < 10; digit++)
            {
                WzCanvasProperty canvas = ResolveCookieCanvas(digitContainer[digit.ToString()]);
                if (canvas == null)
                {
                    return int.MinValue;
                }

                totalWidth += canvas.PngProperty?.Width ?? 0;
                totalHeight += canvas.PngProperty?.Height ?? 0;
            }

            float averageWidth = totalWidth / 10f;
            float averageHeight = totalHeight / 10f;

            int score = 0;
            score -= (int)Math.Abs(averageWidth - 27f) * 4;
            score -= (int)Math.Abs(averageHeight - 32f) * 2;
            score += ResolveNamedCanvas(digitContainer, BitmapNumberSignPlusNames) != null ? 30 : 0;
            score += ResolveNamedCanvas(digitContainer, BitmapNumberSignMinusNames) != null ? 20 : 0;
            score += GetNonDigitCanvases(digitContainer).Any() ? 10 : 0;

            if (path.Contains("number2", StringComparison.OrdinalIgnoreCase))
            {
                score += 24;
            }
            else if (path.Contains("number", StringComparison.OrdinalIgnoreCase))
            {
                score += 12;
            }

            if (path.Contains("result", StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
            }

            if (path.Contains(ClientPreferredBitmapRootHint, StringComparison.OrdinalIgnoreCase))
            {
                score += 6;
            }

            return score;
        }

        private string DescribeBackgroundSource()
        {
            if (string.IsNullOrWhiteSpace(_backgroundSourcePath))
            {
                return $"background=unresolved [{FormatStringPoolEntry(BackgroundStringPoolEvidence, ClientBackgroundClientStringPoolSource)}]";
            }

            if (_usesFallbackBackgroundSource)
            {
                return $"background=fallback:{_backgroundSourcePath} [{FormatStringPoolEntry(BackgroundStringPoolEvidence, ClientBackgroundClientStringPoolSource)}]";
            }

            return $"background={_backgroundSourcePath} [{FormatStringPoolEntry(BackgroundStringPoolEvidence, ClientBackgroundClientStringPoolSource)}]";
        }

        private string DescribeBitmapSource()
        {
            string signEvidence = $"{FormatStringPoolEntry(BitmapPlusStringPoolEvidence, ClientBitmapPlusClientStringPoolSource)}; {FormatStringPoolEntry(BitmapMinusStringPoolEvidence, ClientBitmapMinusClientStringPoolSource)}";
            if (string.IsNullOrWhiteSpace(_bitmapNumberSourcePath))
            {
                return $"bitmap=unresolved [constructor=styles:{GradeCount}/digits:{ClientBitmapDigitCount}/width:{ClientBitmapDigitWidth}; {FormatStringPoolEntry(BitmapRootStringPoolEvidence, ClientBitmapRootClientStringPoolSource)}; {signEvidence}]";
            }

            string sourceLabel = _bitmapRootResolutionKind switch
            {
                CookieBitmapRootResolutionKind.WzCandidate => "wz-candidate",
                CookieBitmapRootResolutionKind.CompatibleShape => "compatible-shape",
                CookieBitmapRootResolutionKind.CompatibleDigitContainer => "compatible-digit-container",
                _ => "unresolved"
            };

            if (_usesFallbackBitmapSource)
            {
                return $"bitmap={sourceLabel}-alt:{_bitmapNumberSourcePath} [constructor=styles:{GradeCount}/digits:{ClientBitmapDigitCount}/width:{ClientBitmapDigitWidth}; {FormatStringPoolEntry(BitmapRootStringPoolEvidence, ClientBitmapRootClientStringPoolSource)}; {signEvidence}]";
            }

            return $"bitmap={sourceLabel}:{_bitmapNumberSourcePath} [constructor=styles:{GradeCount}/digits:{ClientBitmapDigitCount}/width:{ClientBitmapDigitWidth}; {FormatStringPoolEntry(BitmapRootStringPoolEvidence, ClientBitmapRootClientStringPoolSource)}; {signEvidence}]";
        }

        private static string FormatStringPoolEntry(ClientStringPoolEvidence evidence, string clientStringPoolSource)
        {
            string decodedText = string.IsNullOrWhiteSpace(evidence.InferredDecodedValue)
                ? "decoded=unresolved"
                : $"decoded={evidence.InferredDecodedValue}";
            return $"StringPool 0x{evidence.Id:X} seed=0x{evidence.Seed:X2} raw={evidence.RawHex} {decodedText} via {clientStringPoolSource} / {evidence.ClientSource}";
        }
    }
}
