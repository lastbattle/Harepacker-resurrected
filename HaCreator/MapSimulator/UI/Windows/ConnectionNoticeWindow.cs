using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class ConnectionNoticeWindow : UIWindowBase
    {
        private const int TitleOffsetX = 16;
        private const int TitleOffsetY = 12;
        private const int BodyOffsetX = 17;
        private const int BodyOffsetY = 44;
        private const int ProgressOffsetX = 87;
        private const int ProgressOffsetY = 34;
        private const int ProgressWidth = 126;
        private const int ProgressHeight = 8;
        private const float BodyWrapWidth = 250f;

        private readonly Texture2D _progressTrackTexture;
        private readonly Texture2D _progressFillTexture;
        private SpriteFont _font;
        private string _title = "Connection Notice";
        private string _body = string.Empty;
        private float _progress;
        private bool _showProgress;

        public ConnectionNoticeWindow(
            IDXObject frame,
            Texture2D progressTrackTexture,
            Texture2D progressFillTexture)
            : base(frame)
        {
            _progressTrackTexture = progressTrackTexture;
            _progressFillTexture = progressFillTexture;
        }

        public override string WindowName => MapSimulatorWindowNames.ConnectionNotice;

        public override bool SupportsDragging => false;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(string title, string body, bool showProgress, float progress)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Connection Notice" : title;
            _body = body ?? string.Empty;
            _showProgress = showProgress;
            _progress = MathHelper.Clamp(progress, 0f, 1f);
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
            if (_showProgress && _progressTrackTexture != null)
            {
                Rectangle trackRect = new Rectangle(
                    Position.X + ProgressOffsetX,
                    Position.Y + ProgressOffsetY,
                    ProgressWidth,
                    ProgressHeight);
                sprite.Draw(_progressTrackTexture, trackRect, new Color(30, 36, 48, 230));

                if (_progressFillTexture != null)
                {
                    int fillWidth = Math.Max(0, (int)Math.Round(ProgressWidth * _progress));
                    if (fillWidth > 0)
                    {
                        Rectangle fillRect = new Rectangle(trackRect.X, trackRect.Y, fillWidth, ProgressHeight);
                        sprite.Draw(_progressFillTexture, fillRect, new Color(127, 196, 255, 235));
                    }
                }
            }

            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _title,
                new Vector2(Position.X + TitleOffsetX, Position.Y + TitleOffsetY),
                Color.White);

            float y = Position.Y + BodyOffsetY;
            foreach (string line in WrapText(_body, BodyWrapWidth))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(Position.X + BodyOffsetX, y),
                    new Color(232, 232, 232));
                y += _font.LineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }
    }
}
