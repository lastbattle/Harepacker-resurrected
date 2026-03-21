using HaSharedLibrary.Util;
using HaSharedLibrary.Wz;
using MapleLib.Converters;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Dedicated Cookie House HUD runtime.
    /// IDA shows the client initializes a field-owned point layer and redraws
    /// it when the score changes, so the simulator keeps that state here.
    /// </summary>
    public sealed class CookieHouseField
    {
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

        // The client uses four thresholds to select five bitmap-number grades.
        // These values keep the grade-owned simulator path alive until the
        // exact threshold table is lifted from the client data segment.
        public static readonly int[] GradeThresholds = { 1000, 3000, 6000, 10000 };

        private bool _isActive;
        private int _mapId;
        private int _point;
        private int _gradeIndex;
        private bool _assetsLoaded;
        private GraphicsDevice _graphicsDevice;
        private CookieCanvasSprite _backgroundTopLeft;
        private CookieCanvasSprite _backgroundTopCenter;
        private CookieCanvasSprite _backgroundTopRight;
        private CookieCanvasSprite _backgroundCenterLeft;
        private CookieCanvasSprite _backgroundCenterCenter;
        private CookieCanvasSprite _backgroundCenterRight;
        private CookieCanvasSprite _backgroundBottomLeft;
        private CookieCanvasSprite _backgroundBottomCenter;
        private CookieCanvasSprite _backgroundBottomRight;
        private readonly CookieCanvasSprite[] _gradeBadges = new CookieCanvasSprite[GradeCount];

        public bool IsActive => _isActive;
        public int MapId => _mapId;
        public int Point => _point;
        public int GradeIndex => _gradeIndex;

        public void Enable(int mapId)
        {
            _isActive = true;
            _mapId = mapId;
            _point = 0;
            _gradeIndex = 0;
        }

        public void OnPointUpdate(int newPoint)
        {
            _point = Math.Max(0, newPoint);
            _gradeIndex = FindGrade(_point);
        }

        public string DescribeStatus()
        {
            return $"Cookie House map={_mapId}, point={_point}, grade={_gradeIndex + 1}/{GradeCount}";
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, SpriteFont font, int centerX)
        {
            if (!_isActive || spriteBatch == null)
            {
                return;
            }

            EnsureAssetsLoaded(spriteBatch.GraphicsDevice);

            int left = centerX + LayerOffsetX;
            Rectangle panelBounds = new(left, LayerTopY, LayerWidth, LayerHeight);

            if (TryDrawBackground(spriteBatch, panelBounds))
            {
                DrawGradeBadge(spriteBatch, panelBounds);
            }
            else if (pixelTexture != null)
            {
                spriteBatch.Draw(pixelTexture, panelBounds, new Color(37, 26, 18, 220));
            }

            if (font != null)
            {
                string scoreText = _point.ToString();
                Vector2 textSize = font.MeasureString(scoreText);
                Vector2 textPosition = new(
                    left + ScoreDrawX - (textSize.X / 2f),
                    LayerTopY + ScoreDrawY);

                spriteBatch.DrawString(font, scoreText, textPosition, new Color(56, 32, 20));
            }
        }

        public void Reset()
        {
            _isActive = false;
            _mapId = 0;
            _point = 0;
            _gradeIndex = 0;
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

        private void EnsureAssetsLoaded(GraphicsDevice graphicsDevice)
        {
            if (_assetsLoaded || graphicsDevice == null)
            {
                return;
            }

            _graphicsDevice = graphicsDevice;

            WzImage uiWindow = global::HaCreator.Program.FindImage("UI", "UIWindow2.img")
                ?? global::HaCreator.Program.FindImage("UI", "UIWindow.img");
            WzImageProperty raiseRoot = uiWindow?["raise"];
            WzImageProperty background = raiseRoot?["backgrnd"];

            _backgroundTopLeft = LoadCanvas(background?["top"]?["left"]);
            _backgroundTopCenter = LoadCanvas(background?["top"]?["center"]);
            _backgroundTopRight = LoadCanvas(background?["top"]?["right"]);
            _backgroundCenterLeft = LoadCanvas(background?["center"]?["left"]);
            _backgroundCenterCenter = LoadCanvas(background?["center"]?["center"]);
            _backgroundCenterRight = LoadCanvas(background?["center"]?["right"]);
            _backgroundBottomLeft = LoadCanvas(background?["bottom"]?["left"]);
            _backgroundBottomCenter = LoadCanvas(background?["bottom"]?["center"]);
            _backgroundBottomRight = LoadCanvas(background?["bottom"]?["right"]);

            LoadGradeBadges(raiseRoot?["30"]);
            _assetsLoaded = true;
        }

        private void LoadGradeBadges(WzImageProperty source)
        {
            Array.Clear(_gradeBadges, 0, _gradeBadges.Length);
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < _gradeBadges.Length; i++)
            {
                _gradeBadges[i] = LoadCanvas(source[i.ToString()]);
            }
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

        private void DrawGradeBadge(SpriteBatch spriteBatch, Rectangle bounds)
        {
            CookieCanvasSprite badge = _gradeBadges[Math.Clamp(_gradeIndex, 0, _gradeBadges.Length - 1)];
            if (!badge.IsLoaded)
            {
                return;
            }

            int badgeX = bounds.Left + 12;
            int badgeY = bounds.Center.Y - (badge.Texture.Height / 2);
            spriteBatch.Draw(badge.Texture, new Vector2(badgeX, badgeY), Color.White);
        }
    }
}
