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
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
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
        private static readonly XnaColor ValueColor = new XnaColor(97, 77, 63);
        private static readonly XnaColor StatusColor = new XnaColor(220, 220, 220);
        private static readonly StringFormat TypographicStringFormat = StringFormat.GenericTypographic;
        private LoginCharacterRosterEntry _entry;
        private string _statusMessage = "Select a character to inspect details.";
        private readonly Texture2D _panelTexture;
        private readonly Texture2D _panelTextureWithRank;
        private readonly Texture2D _rankUpTexture;
        private readonly Texture2D _rankDownTexture;
        private readonly Texture2D _rankSameTexture;
        private readonly IReadOnlyDictionary<int, Texture2D> _jobBadgeTextures;
        private Texture2D _composedPanelTexture;
        private bool _isComposedPanelDirty = true;

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
            Texture2D panelTexture = EnsureComposedPanelTexture();
            if (panelTexture != null)
            {
                sprite.Draw(panelTexture, new Vector2(Position.X, Position.Y), XnaColor.White);
            }
        }

        private Texture2D EnsureComposedPanelTexture()
        {
            if (!_isComposedPanelDirty)
            {
                return _composedPanelTexture;
            }

            DisposeComposedPanelTexture();
            Texture2D baseTexture = ResolvePanelTexture();
            if (baseTexture == null)
            {
                _isComposedPanelDirty = false;
                return null;
            }

            try
            {
                using Bitmap bitmap = CreateCompositeBitmap(baseTexture);
                using Graphics graphics = Graphics.FromImage(bitmap);
                ConfigureGraphics(graphics);

                if (_entry?.Build == null)
                {
                    DrawStatusMessage(graphics, bitmap.Width, bitmap.Height);
                }
                else
                {
                    DrawPanelValues(graphics, _entry.Build);
                }

                _composedPanelTexture = bitmap.ToTexture2DAndDispose(baseTexture.GraphicsDevice);
            }
            catch
            {
                _composedPanelTexture = baseTexture;
            }

            _isComposedPanelDirty = false;
            return _composedPanelTexture;
        }

        private void DrawPanelValues(Graphics graphics, CharacterBuild build)
        {
            DrawValue(graphics, LeftValueX, JobValueY, build.JobName);
            DrawValue(graphics, LeftValueX, LevelValueY, build.Level.ToString());
            DrawValue(graphics, RightValueX, FameValueY, build.Fame.ToString());
            DrawValue(graphics, LeftValueX, StrengthValueY, build.TotalSTR.ToString());
            DrawValue(graphics, RightValueX, IntelligenceValueY, build.TotalINT.ToString());
            DrawValue(graphics, LeftValueX, DexterityValueY, build.TotalDEX.ToString());
            DrawValue(graphics, RightValueX, LuckValueY, build.TotalLUK.ToString());

            if (!HasRankInfo())
            {
                return;
            }

            DrawRightAlignedValue(graphics, RankRightEdgeX, WorldRankValueY, FormatRank(build.WorldRank));
            DrawRankGlyph(graphics, build.WorldRank, _entry.PreviousWorldRank, WorldRankGlyphY);

            Texture2D jobBadgeTexture = ResolveJobBadgeTexture(build);
            if (jobBadgeTexture != null)
            {
                using Bitmap badgeBitmap = CloneTextureBitmap(jobBadgeTexture);
                graphics.DrawImage(
                    badgeBitmap,
                    JobRankIconRightEdgeX - badgeBitmap.Width,
                    JobRankIconY,
                    badgeBitmap.Width,
                    badgeBitmap.Height);
            }

            DrawRightAlignedValue(graphics, RankRightEdgeX, JobRankValueY, FormatRank(build.JobRank));
            DrawRankGlyph(graphics, build.JobRank, _entry.PreviousJobRank, JobRankGlyphY);
        }

        private Texture2D ResolvePanelTexture()
        {
            return HasRankInfo() ? _panelTextureWithRank : _panelTexture;
        }

        private bool HasRankInfo()
        {
            return (_entry?.Build?.WorldRank ?? 0) > 0 || (_entry?.Build?.JobRank ?? 0) > 0;
        }

        private void DrawValue(Graphics graphics, int x, int y, string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "-" : value;
            DrawText(graphics, safeValue, x, y, ValueColor);
        }

        private void DrawRightAlignedValue(Graphics graphics, int rightEdgeX, int y, string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "-" : value;
            SizeF size = MeasureText(graphics, safeValue);
            DrawText(graphics, safeValue, rightEdgeX - (int)Math.Ceiling(size.Width), y, ValueColor);
        }

        private void DrawRankGlyph(Graphics graphics, int currentRank, int? previousRank, int y)
        {
            Texture2D glyphTexture = ResolveRankGlyph(currentRank, previousRank);
            if (glyphTexture == null)
            {
                return;
            }

            using Bitmap glyphBitmap = CloneTextureBitmap(glyphTexture);
            int yOffset = glyphTexture == _rankSameTexture ? 0 : -4;
            graphics.DrawImage(
                glyphBitmap,
                RankGlyphX,
                y + yOffset,
                glyphBitmap.Width,
                glyphBitmap.Height);
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

        private void DrawStatusMessage(Graphics graphics, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                return;
            }

            string text = _statusMessage;
            RectangleF layout = new RectangleF(
                StatusPaddingX,
                StatusPaddingY,
                Math.Max(0, width - (StatusPaddingX * 2)),
                Math.Max(0, height - (StatusPaddingY * 2)));
            using Font font = CreatePanelFont();
            using SolidBrush brush = new SolidBrush(ToDrawingColor(StatusColor));
            graphics.DrawString(text, font, brush, layout, TypographicStringFormat);
        }

        private void DrawText(Graphics graphics, string text, int x, int y, XnaColor color)
        {
            using Font font = CreatePanelFont();
            using SolidBrush brush = new SolidBrush(ToDrawingColor(color));
            graphics.DrawString(text, font, brush, x, y, TypographicStringFormat);
        }

        private static SizeF MeasureText(Graphics graphics, string text)
        {
            using Font font = CreatePanelFont();
            return graphics.MeasureString(text, font, PointF.Empty, TypographicStringFormat);
        }

        private static Font CreatePanelFont()
        {
            return new Font("Tahoma", 8f, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private static void ConfigureGraphics(Graphics graphics)
        {
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        }

        private static Bitmap CloneTextureBitmap(Texture2D texture)
        {
            using MemoryStream stream = new MemoryStream();
            texture.SaveAsPng(stream, texture.Width, texture.Height);
            stream.Position = 0;
            using Bitmap source = new Bitmap(stream);
            return new Bitmap(source);
        }

        private static Bitmap CreateCompositeBitmap(Texture2D texture)
        {
            using Bitmap baseBitmap = CloneTextureBitmap(texture);
            return new Bitmap(baseBitmap);
        }

        private static System.Drawing.Color ToDrawingColor(XnaColor color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
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
            _isComposedPanelDirty = true;
        }

        public void SetEntry(LoginCharacterRosterEntry entry, string statusMessage)
        {
            _entry = entry;
            _statusMessage = statusMessage ?? string.Empty;
            _isComposedPanelDirty = true;
        }

        private void DisposeComposedPanelTexture()
        {
            if (_composedPanelTexture != null
                && _composedPanelTexture != _panelTexture
                && _composedPanelTexture != _panelTextureWithRank)
            {
                _composedPanelTexture.Dispose();
            }

            _composedPanelTexture = null;
        }
    }
}
