using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CharacterDetailWindow : UIWindowBase
    {
        private const int PanelWidth = 183;
        private const int PanelHeight = 151;
        private const int MetaRowHeight = 12;
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
        private const float MetaTextWidth = 167f;

        private SpriteFont _font;
        private LoginCharacterRosterEntry _entry;
        private string _statusMessage = "Select a character to inspect details.";
        private readonly Texture2D _panelTexture;
        private readonly Texture2D _panelTextureWithRank;
        private readonly Texture2D _rankUpTexture;
        private readonly Texture2D _rankDownTexture;
        private readonly Texture2D _rankSameTexture;
        private readonly IReadOnlyDictionary<int, Texture2D> _jobBadgeTextures;

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

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetEntry(LoginCharacterRosterEntry entry, string statusMessage)
        {
            _entry = entry;
            _statusMessage = statusMessage ?? string.Empty;
        }

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
            if (_font == null)
            {
                return;
            }

            Texture2D panelTexture = ResolvePanelTexture();
            if (panelTexture != null)
            {
                sprite.Draw(panelTexture, new Vector2(Position.X, Position.Y), Color.White);
            }

            if (_entry?.Build == null)
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + 8, Position.Y + GetMetaStartY()),
                    new Color(220, 220, 220));
                return;
            }

            CharacterBuild build = _entry.Build;
            DrawPanelValues(sprite, build);
            DrawSupplementalRows(sprite, build);

            if (ShouldDrawStatusMessage())
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + 8, Position.Y + GetStatusY()),
                    new Color(97, 77, 63));
            }
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
                    Color.White);
            }

            DrawRightAlignedValue(sprite, RankRightEdgeX, JobRankValueY, FormatRank(build.JobRank));
            DrawRankGlyph(sprite, build.JobRank, _entry.PreviousJobRank, JobRankGlyphY);
        }

        private void DrawSupplementalRows(SpriteBatch sprite, CharacterBuild build)
        {
            DrawSupplementalText(sprite, 0, build.Name, new Color(255, 234, 171));
            DrawSupplementalText(sprite, 1, $"Guild  {build.GuildDisplayText}", new Color(122, 92, 63));
            DrawSupplementalText(sprite, 2, $"EXP  {build.ExpDisplayText}", new Color(122, 92, 63));

            string targetMap = string.IsNullOrWhiteSpace(_entry.FieldDisplayName)
                ? _entry.FieldMapId.ToString()
                : $"{_entry.FieldDisplayName} ({_entry.FieldMapId})";
            DrawSupplementalText(sprite, 3, $"Map  {targetMap}", new Color(122, 92, 63));
        }

        private void DrawValue(SpriteBatch sprite, int x, int y, string value)
        {
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                string.IsNullOrWhiteSpace(value) ? "-" : value,
                new Vector2(Position.X + x, Position.Y + y),
                new Color(97, 77, 63));
        }

        private void DrawRightAlignedValue(SpriteBatch sprite, int rightEdgeX, int y, string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "-" : value;
            float width = _font.MeasureString(safeValue).X;
            DrawValue(sprite, rightEdgeX - (int)MathF.Ceiling(width), y, safeValue);
        }

        private void DrawRankGlyph(SpriteBatch sprite, int currentRank, int? previousRank, int y)
        {
            Texture2D glyphTexture = ResolveRankGlyph(currentRank, previousRank);
            if (glyphTexture == null)
            {
                return;
            }

            int yOffset = glyphTexture == _rankSameTexture ? 0 : -4;
            sprite.Draw(glyphTexture, new Vector2(Position.X + RankGlyphX, Position.Y + y + yOffset), Color.White);
        }

        private void DrawSupplementalText(SpriteBatch sprite, int rowIndex, string text, Color color)
        {
            string trimmed = TrimToWidth(text, MetaTextWidth);
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                trimmed,
                new Vector2(Position.X + 8, Position.Y + GetMetaStartY() + (rowIndex * MetaRowHeight)),
                color);
        }

        private Texture2D ResolvePanelTexture()
        {
            return HasRankInfo() ? _panelTextureWithRank : _panelTexture;
        }

        private bool HasRankInfo()
        {
            return (_entry?.Build?.WorldRank ?? 0) > 0 || (_entry?.Build?.JobRank ?? 0) > 0;
        }

        private int GetMetaStartY()
        {
            return (ResolvePanelTexture()?.Height ?? PanelHeight) + 8;
        }

        private int GetStatusY()
        {
            return GetMetaStartY() + (MetaRowHeight * 4) + 10;
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

        private bool ShouldDrawStatusMessage()
        {
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                return false;
            }

            return _entry?.Build == null ||
                   _statusMessage.IndexOf("Client-backed detail view", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string FormatRank(int rank)
        {
            return rank > 0 ? rank.ToString() : "-";
        }

        private string TrimToWidth(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (_font.MeasureString(text).X <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            string value = text;
            while (value.Length > 0 && _font.MeasureString(value + ellipsis).X > maxWidth)
            {
                value = value[..^1];
            }

            return value.Length == 0 ? ellipsis : value + ellipsis;
        }
    }
}
