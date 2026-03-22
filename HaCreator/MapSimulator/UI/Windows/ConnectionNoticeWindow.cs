using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using HaCreator.MapSimulator;

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
        private const int ProgressWidth = 109;
        private const int ProgressHeight = 8;
        private const float BodyWrapWidth = 250f;

        private readonly IDXObject _noticeFrame;
        private readonly IDXObject _loadingFrame;
        private readonly IReadOnlyList<Texture2D> _progressFrames;
        private SpriteFont _font;
        private string _title = "Connection Notice";
        private string _body = string.Empty;
        private float _progress;
        private bool _showProgress;
        private ConnectionNoticeWindowVariant _variant = ConnectionNoticeWindowVariant.Notice;

        public ConnectionNoticeWindow(
            IDXObject noticeFrame,
            IDXObject loadingFrame,
            IReadOnlyList<Texture2D> progressFrames)
            : base(noticeFrame ?? loadingFrame)
        {
            _noticeFrame = noticeFrame ?? loadingFrame;
            _loadingFrame = loadingFrame ?? noticeFrame;
            _progressFrames = progressFrames;
        }

        public override string WindowName => MapSimulatorWindowNames.ConnectionNotice;

        public override bool SupportsDragging => false;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(
            string title,
            string body,
            bool showProgress,
            float progress,
            ConnectionNoticeWindowVariant variant)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Connection Notice" : title;
            _body = body ?? string.Empty;
            _showProgress = showProgress;
            _progress = MathHelper.Clamp(progress, 0f, 1f);
            _variant = variant;
            Frame = _variant == ConnectionNoticeWindowVariant.Loading
                ? _loadingFrame
                : _noticeFrame;
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
            if (_showProgress)
            {
                Rectangle trackRect = new Rectangle(
                    Position.X + ProgressOffsetX,
                    Position.Y + ProgressOffsetY,
                    ProgressWidth,
                    ProgressHeight);

                Texture2D progressTexture = null;
                if (_progressFrames != null && _progressFrames.Count > 0)
                {
                    int frameIndex = (int)Math.Round((_progressFrames.Count - 1) * _progress);
                    frameIndex = Math.Max(0, Math.Min(frameIndex, _progressFrames.Count - 1));
                    progressTexture = _progressFrames[frameIndex];
                }

                if (progressTexture != null)
                {
                    sprite.Draw(progressTexture, trackRect, Color.White);
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
