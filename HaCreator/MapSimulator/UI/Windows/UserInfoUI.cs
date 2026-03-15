using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Character profile window backed by UI.wz/UIWindow(.2).img/UserInfo.
    /// This currently covers the primary character-information page and leaves
    /// the wider party, trade, pet, ride, and personality flows disabled.
    /// </summary>
    public sealed class UserInfoUI : UIWindowBase
    {
        private readonly bool _isBigBang;
        private readonly List<UIObject> _decorativeButtons = new List<UIObject>();

        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _nameBanner;
        private Point _nameBannerOffset;
        private SpriteFont _font;
        private CharacterBuild _characterBuild;

        private static readonly Color ValueColor = new Color(45, 45, 45);
        private static readonly Color SecondaryColor = new Color(96, 96, 96);
        private static readonly Color BannerTextColor = new Color(248, 248, 248);

        private static readonly Point BigBangNamePos = new Point(57, 154);
        private static readonly Point BigBangPortraitSummaryPos = new Point(19, 173);
        private static readonly Point BigBangFieldValuePos = new Point(131, 41);
        private const int BigBangFieldRowHeight = 23;
        private const int BigBangFieldMaxWidth = 116;

        private static readonly Point PreBigBangNamePos = new Point(16, 19);
        private static readonly Point PreBigBangPortraitSummaryPos = new Point(16, 146);
        private static readonly Point PreBigBangFieldValuePos = new Point(127, 54);
        private const int PreBigBangFieldRowHeight = 22;
        private const int PreBigBangFieldMaxWidth = 132;

        public UserInfoUI(IDXObject frame, bool isBigBang)
            : base(frame)
        {
            _isBigBang = isBigBang;
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterInfo;

        public override CharacterBuild CharacterBuild
        {
            get => _characterBuild;
            set => _characterBuild = value;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetForeground(IDXObject foreground, int offsetX, int offsetY)
        {
            _foreground = foreground;
            _foregroundOffset = new Point(offsetX, offsetY);
        }

        public void SetNameBanner(IDXObject nameBanner, int offsetX, int offsetY)
        {
            _nameBanner = nameBanner;
            _nameBannerOffset = new Point(offsetX, offsetY);
        }

        public void InitializeDecorativeButtons(params UIObject[] buttons)
        {
            foreach (UIObject button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                button.SetEnabled(false);
                _decorativeButtons.Add(button);
                AddButton(button);
            }
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
            DrawLayer(_foreground, _foregroundOffset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawLayer(_nameBanner, _nameBannerOffset, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);

            if (_font == null)
            {
                return;
            }

            string name = _characterBuild?.Name;
            string job = _characterBuild?.JobName;
            string level = _characterBuild != null ? _characterBuild.Level.ToString() : "-";
            string fame = "0";
            string rank = "-";
            string guild = "-";
            string alliance = "-";

            if (_isBigBang)
            {
                if (_nameBanner != null)
                {
                    Rectangle bannerBounds = new Rectangle(
                        Position.X + _nameBannerOffset.X,
                        Position.Y + _nameBannerOffset.Y,
                        _nameBanner.Width,
                        _nameBanner.Height);
                    DrawCenteredText(sprite, FitText(name, 72), bannerBounds, BannerTextColor, 0.6f);
                }

                DrawSummaryText(sprite, name, job, BigBangPortraitSummaryPos, 116);
                DrawValueColumn(sprite, BigBangFieldValuePos, BigBangFieldRowHeight, BigBangFieldMaxWidth,
                    level,
                    job,
                    rank,
                    fame,
                    guild,
                    alliance);
                return;
            }

            DrawPlainText(sprite, FitText(name, 110),
                new Vector2(Position.X + PreBigBangNamePos.X, Position.Y + PreBigBangNamePos.Y),
                SecondaryColor,
                0.75f);
            DrawSummaryText(sprite, name, job, PreBigBangPortraitSummaryPos, 98);
            DrawValueColumn(sprite, PreBigBangFieldValuePos, PreBigBangFieldRowHeight, PreBigBangFieldMaxWidth,
                level,
                job,
                fame,
                guild,
                alliance);
        }

        private void DrawValueColumn(SpriteBatch sprite, Point start, int rowHeight, int maxWidth, params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                DrawPlainText(
                    sprite,
                    FitText(values[i], maxWidth),
                    new Vector2(Position.X + start.X, Position.Y + start.Y + (i * rowHeight)),
                    ValueColor,
                    0.7f);
            }
        }

        private void DrawSummaryText(SpriteBatch sprite, string name, string job, Point position, int maxWidth)
        {
            DrawPlainText(
                sprite,
                FitText(name, maxWidth),
                new Vector2(Position.X + position.X, Position.Y + position.Y),
                SecondaryColor,
                0.65f);
            DrawPlainText(
                sprite,
                FitText(job, maxWidth),
                new Vector2(Position.X + position.X, Position.Y + position.Y + 14),
                SecondaryColor,
                0.6f);
        }

        private void DrawCenteredText(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 textSize = _font.MeasureString(text) * scale;
            Vector2 position = new Vector2(
                bounds.X + Math.Max(0f, (bounds.Width - textSize.X) * 0.5f),
                bounds.Y + Math.Max(0f, (bounds.Height - textSize.Y) * 0.5f) - 1f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawPlainText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private string FitText(string text, float maxWidth)
        {
            string safeText = string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
            if (_font == null || _font.MeasureString(safeText).X <= maxWidth)
            {
                return safeText;
            }

            const string ellipsis = "...";
            for (int length = safeText.Length - 1; length > 0; length--)
            {
                string candidate = safeText.Substring(0, length) + ellipsis;
                if (_font.MeasureString(candidate).X <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private void DrawLayer(
            IDXObject layer,
            Point offset,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            if (layer == null)
            {
                return;
            }

            layer.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }
    }
}
