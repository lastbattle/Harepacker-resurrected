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
using SD = System.Drawing;
using SDText = System.Drawing.Text;
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

        private static readonly XnaColor ValueColor = XnaColor.Black;
        private static readonly XnaColor StatusColor = new XnaColor(64, 64, 64);

        private readonly Texture2D _panelTexture;
        private readonly Texture2D _panelTextureWithRank;
        private readonly Texture2D _rankUpTexture;
        private readonly Texture2D _rankDownTexture;
        private readonly Texture2D _rankSameTexture;
        private readonly IReadOnlyDictionary<int, Texture2D> _jobBadgeTextures;
        private readonly Dictionary<TextRenderCacheKey, Texture2D> _textTextureCache = new();
        private readonly SD.Bitmap _measureBitmap;
        private readonly SD.Graphics _measureGraphics;
        private readonly SD.Font _basicBlackFont;

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
            _basicBlackFont = new SD.Font("Tahoma", 11f, SD.FontStyle.Regular, SD.GraphicsUnit.Pixel);
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

            if (_font == null)
            {
                return;
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

            Texture2D textTexture = GetOrCreateTextTexture(text, color);
            if (textTexture != null)
            {
                sprite.Draw(textTexture, new Vector2(Position.X + x, Position.Y + y), XnaColor.White);
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

            Vector2 rasterSize = MeasureRasterText(text);
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

            SD.SizeF size = _measureGraphics.MeasureString(text, _basicBlackFont, SD.PointF.Empty, SD.StringFormat.GenericTypographic);
            if (size.Width <= 0f || size.Height <= 0f)
            {
                size = _measureGraphics.MeasureString(text, _basicBlackFont);
            }

            return new Vector2((float)Math.Ceiling(size.Width), (float)Math.Ceiling(size.Height));
        }

        private Texture2D GetOrCreateTextTexture(string text, XnaColor color)
        {
            if (_basicBlackFont == null || string.IsNullOrEmpty(text))
            {
                return null;
            }

            TextRenderCacheKey cacheKey = new TextRenderCacheKey(text, color);
            if (_textTextureCache.TryGetValue(cacheKey, out Texture2D cachedTexture) &&
                cachedTexture != null &&
                !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            Vector2 size = MeasureRasterText(text);
            int width = Math.Max(1, (int)size.X);
            int height = Math.Max(1, (int)size.Y);

            using var bitmap = new SD.Bitmap(width, height);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);
            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            using var brush = new SD.SolidBrush(SD.Color.FromArgb(color.A, color.R, color.G, color.B));
            graphics.DrawString(text, _basicBlackFont, brush, 0f, 0f, SD.StringFormat.GenericTypographic);

            Texture2D texture = bitmap.ToTexture2D(_panelTexture.GraphicsDevice);
            _textTextureCache[cacheKey] = texture;
            return texture;
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
    }
}
