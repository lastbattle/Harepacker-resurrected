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
        private const int SingleGaugeOffsetX = 104;
        private const int SingleGaugeOffsetY = 42;
        private const int SingleGaugeWidth = 137;
        private const int SingleGaugeHeight = 11;
        private const float BodyWrapWidth = 250f;

        private readonly IReadOnlyDictionary<ConnectionNoticeWindowVariant, IDXObject> _framesByVariant;
        private readonly IReadOnlyDictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> _progressFramesByVariant;
        private readonly IReadOnlyDictionary<int, Texture2D> _noticeTextTextures;
        private SpriteFont _font;
        private string _title = "Connection Notice";
        private string _body = string.Empty;
        private float _progress;
        private bool _showProgress;
        private int? _noticeTextIndex;
        private ConnectionNoticeWindowVariant _variant = ConnectionNoticeWindowVariant.Notice;

        public ConnectionNoticeWindow(
            IReadOnlyDictionary<ConnectionNoticeWindowVariant, IDXObject> framesByVariant,
            IReadOnlyDictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> progressFramesByVariant,
            IReadOnlyDictionary<int, Texture2D> noticeTextTextures)
            : base((framesByVariant != null && framesByVariant.TryGetValue(ConnectionNoticeWindowVariant.Notice, out IDXObject frame))
                ? frame
                : null)
        {
            _framesByVariant = framesByVariant ?? new Dictionary<ConnectionNoticeWindowVariant, IDXObject>();
            _progressFramesByVariant = progressFramesByVariant ?? new Dictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>>();
            _noticeTextTextures = noticeTextTextures ?? new Dictionary<int, Texture2D>();
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
            ConnectionNoticeWindowVariant variant,
            int? noticeTextIndex = null)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Connection Notice" : title;
            _body = body ?? string.Empty;
            _showProgress = showProgress;
            _progress = MathHelper.Clamp(progress, 0f, 1f);
            _variant = variant;
            _noticeTextIndex = noticeTextIndex;
            if (!_framesByVariant.TryGetValue(_variant, out IDXObject frame) ||
                frame == null)
            {
                _framesByVariant.TryGetValue(ConnectionNoticeWindowVariant.Notice, out frame);
            }

            Frame = frame;
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
                DrawProgress(sprite);
            }

            if (_noticeTextIndex.HasValue &&
                _noticeTextTextures.TryGetValue(_noticeTextIndex.Value, out Texture2D noticeTextTexture) &&
                noticeTextTexture != null)
            {
                sprite.Draw(
                    noticeTextTexture,
                    new Vector2(Position.X + BodyOffsetX, Position.Y + BodyOffsetY),
                    Color.White);
                return;
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

        private void DrawProgress(SpriteBatch sprite)
        {
            if (!_progressFramesByVariant.TryGetValue(_variant, out IReadOnlyList<Texture2D> frames) ||
                frames == null ||
                frames.Count == 0)
            {
                return;
            }

            int frameIndex = (int)Math.Round((frames.Count - 1) * _progress);
            frameIndex = Math.Max(0, Math.Min(frameIndex, frames.Count - 1));
            Texture2D progressTexture = frames[frameIndex];
            if (progressTexture == null)
            {
                return;
            }

            Rectangle trackRect = _variant == ConnectionNoticeWindowVariant.LoadingSingleGauge
                ? new Rectangle(Position.X + SingleGaugeOffsetX, Position.Y + SingleGaugeOffsetY, SingleGaugeWidth, SingleGaugeHeight)
                : new Rectangle(Position.X + ProgressOffsetX, Position.Y + ProgressOffsetY, ProgressWidth, ProgressHeight);
            sprite.Draw(progressTexture, trackRect, Color.White);
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
