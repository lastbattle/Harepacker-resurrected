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
        private const int NoticeBodySpacingY = 6;
        private const int CancelButtonOffsetX = 100;
        private const int CancelButtonOffsetY = 106;
        private const int SecurityQuestionYesButtonX = 59;
        private const int SecurityQuestionNoButtonX = 129;
        private const int NoticeNexonButtonX = 89;
        private const int DialogButtonY = 106;
        private const int ProgressOffsetX = 87;
        private const int ProgressOffsetY = 34;
        private const int ProgressWidth = 109;
        private const int ProgressHeight = 8;
        private const int LoadingCircleOffsetX = 84;
        private const int LoadingCircleOffsetY = 46;
        private const int SingleGaugeOffsetX = 104;
        private const int SingleGaugeOffsetY = 42;
        private const int SingleGaugeWidth = 137;
        private const int SingleGaugeHeight = 11;
        private const float BodyWrapWidth = 250f;

        private readonly IReadOnlyDictionary<ConnectionNoticeWindowVariant, IDXObject> _framesByVariant;
        private readonly IReadOnlyDictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> _progressFramesByVariant;
        private readonly IReadOnlyDictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> _animationFramesByVariant;
        private readonly IReadOnlyDictionary<int, Texture2D> _noticeTextTextures;
        private readonly UIObject _cancelButton;
        private readonly UIObject _questionYesButton;
        private readonly UIObject _questionNoButton;
        private readonly UIObject _nexonButton;
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private string _title = "Connection Notice";
        private string _body = string.Empty;
        private string _primaryLabel = string.Empty;
        private string _secondaryLabel = string.Empty;
        private float _progress;
        private bool _showProgress;
        private int? _noticeTextIndex;
        private ConnectionNoticeWindowVariant _variant = ConnectionNoticeWindowVariant.Notice;
        private LoginUtilityDialogButtonLayout _buttonLayout = LoginUtilityDialogButtonLayout.Ok;
        private UIObject _activePrimaryButton;
        private UIObject _activeSecondaryButton;
        private bool _drawPrimaryButtonLabel;
        private bool _drawSecondaryButtonLabel;

        public ConnectionNoticeWindow(
            IReadOnlyDictionary<ConnectionNoticeWindowVariant, IDXObject> framesByVariant,
            IReadOnlyDictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> progressFramesByVariant,
            IReadOnlyDictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>> animationFramesByVariant,
            IReadOnlyDictionary<int, Texture2D> noticeTextTextures,
            UIObject cancelButton,
            UIObject questionYesButton,
            UIObject questionNoButton,
            UIObject nexonButton,
            int screenWidth,
            int screenHeight)
            : base((framesByVariant != null && framesByVariant.TryGetValue(ConnectionNoticeWindowVariant.Notice, out IDXObject frame))
                ? frame
                : null)
        {
            _framesByVariant = framesByVariant ?? new Dictionary<ConnectionNoticeWindowVariant, IDXObject>();
            _progressFramesByVariant = progressFramesByVariant ?? new Dictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>>();
            _animationFramesByVariant = animationFramesByVariant ?? new Dictionary<ConnectionNoticeWindowVariant, IReadOnlyList<Texture2D>>();
            _noticeTextTextures = noticeTextTextures ?? new Dictionary<int, Texture2D>();
            _cancelButton = RegisterButton(cancelButton);
            _questionYesButton = RegisterPrimaryButton(questionYesButton);
            _questionNoButton = RegisterSecondaryButton(questionNoButton);
            _nexonButton = RegisterPrimaryButton(nexonButton);
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
        }

        public override string WindowName => MapSimulatorWindowNames.ConnectionNotice;

        public override bool SupportsDragging => false;

        public override void SetFont(SpriteFont font)
        {
            base.SetFont(font);
        }

        public event Action CancelRequested;
        public event Action PrimaryRequested;
        public event Action SecondaryRequested;

        public void Configure(
            string title,
            string body,
            bool showProgress,
            float progress,
            ConnectionNoticeWindowVariant variant,
            int? noticeTextIndex = null,
            LoginUtilityDialogButtonLayout buttonLayout = LoginUtilityDialogButtonLayout.Ok,
            string primaryLabel = null,
            string secondaryLabel = null)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Connection Notice" : title;
            _body = body ?? string.Empty;
            _showProgress = showProgress;
            _progress = MathHelper.Clamp(progress, 0f, 1f);
            _variant = variant;
            _noticeTextIndex = noticeTextIndex;
            _buttonLayout = buttonLayout;
            _primaryLabel = primaryLabel ?? string.Empty;
            _secondaryLabel = secondaryLabel ?? string.Empty;
            _drawPrimaryButtonLabel = !string.IsNullOrWhiteSpace(primaryLabel);
            _drawSecondaryButtonLabel = !string.IsNullOrWhiteSpace(secondaryLabel);
            if (!_framesByVariant.TryGetValue(_variant, out IDXObject frame) ||
                frame == null)
            {
                _framesByVariant.TryGetValue(ConnectionNoticeWindowVariant.Notice, out frame);
            }

            Frame = frame;
            CenterFrame(frame);
            ConfigureButtons();
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
                DrawAnimatedOverlay(sprite, TickCount);
            }

            if (!CanDrawWindowText)
            {
                return;
            }

            float y = Position.Y + BodyOffsetY;
            if (_noticeTextIndex.HasValue &&
                _noticeTextTextures.TryGetValue(_noticeTextIndex.Value, out Texture2D noticeTextTexture) &&
                noticeTextTexture != null)
            {
                sprite.Draw(
                    noticeTextTexture,
                    new Vector2(Position.X + BodyOffsetX, Position.Y + BodyOffsetY),
                    Color.White);

                if (string.IsNullOrWhiteSpace(_body))
                {
                    return;
                }

                y += noticeTextTexture.Height + NoticeBodySpacingY;
            }
            else
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    WindowFont,
                    _title,
                    new Vector2(Position.X + TitleOffsetX, Position.Y + TitleOffsetY),
                    Color.White);
            }

            foreach (string line in WrapText(_body, BodyWrapWidth))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    WindowFont,
                    line,
                    new Vector2(Position.X + BodyOffsetX, y),
                    new Color(232, 232, 232));
                y += WindowLineSpacing;
            }
        }

        protected override void DrawOverlay(
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
            if (_drawPrimaryButtonLabel)
            {
                DrawButtonLabel(sprite, _activePrimaryButton, _primaryLabel);
            }

            if (_drawSecondaryButtonLabel)
            {
                DrawButtonLabel(sprite, _activeSecondaryButton, _secondaryLabel);
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

        private void DrawAnimatedOverlay(SpriteBatch sprite, int tickCount)
        {
            if (!_animationFramesByVariant.TryGetValue(_variant, out IReadOnlyList<Texture2D> frames) ||
                frames == null ||
                frames.Count == 0)
            {
                return;
            }

            int frameIndex = Math.Abs(tickCount / 90) % frames.Count;
            Texture2D animationFrame = frames[frameIndex];
            if (animationFrame == null)
            {
                return;
            }

            sprite.Draw(
                animationFrame,
                new Vector2(Position.X + LoadingCircleOffsetX, Position.Y + LoadingCircleOffsetY),
                Color.White);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate).X > maxWidth)
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

        private void DrawButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (WindowFont == null || button == null || !button.ButtonVisible || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = ClientTextDrawing.Measure((GraphicsDevice)null, text, 1.0f, WindowFont);
            float x = Position.X + button.X + ((button.CanvasSnapshotWidth - size.X) / 2f);
            float y = Position.Y + button.Y + ((button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            SelectorWindowDrawing.DrawShadowedText(sprite, WindowFont, text, new Vector2(x, y), Color.White);
        }

        private UIObject RegisterButton(UIObject button)
        {
            if (button == null)
            {
                return null;
            }

            button.SetVisible(false);
            button.SetEnabled(false);
            button.ButtonClickReleased += _ => CancelRequested?.Invoke();
            AddButton(button);
            return button;
        }

        private UIObject RegisterPrimaryButton(UIObject button)
        {
            UIObject registeredButton = RegisterButton(button);
            if (registeredButton != null)
            {
                registeredButton.ButtonClickReleased -= HandlePrimaryButtonReleased;
                registeredButton.ButtonClickReleased += HandlePrimaryButtonReleased;
            }

            return registeredButton;
        }

        private UIObject RegisterSecondaryButton(UIObject button)
        {
            UIObject registeredButton = RegisterButton(button);
            if (registeredButton != null)
            {
                registeredButton.ButtonClickReleased -= HandleSecondaryButtonReleased;
                registeredButton.ButtonClickReleased += HandleSecondaryButtonReleased;
            }

            return registeredButton;
        }

        private void HandlePrimaryButtonReleased(UIObject _)
        {
            PrimaryRequested?.Invoke();
        }

        private void HandleSecondaryButtonReleased(UIObject _)
        {
            SecondaryRequested?.Invoke();
        }

        private void ConfigureButtons()
        {
            _activePrimaryButton = null;
            _activeSecondaryButton = null;

            if (_cancelButton == null)
            {
                HideButton(_questionYesButton);
                HideButton(_questionNoButton);
                HideButton(_nexonButton);
            }
            else
            {
                bool showCancelButton = _variant is ConnectionNoticeWindowVariant.Loading or ConnectionNoticeWindowVariant.LoadingSingleGauge;
                _cancelButton.X = CancelButtonOffsetX;
                _cancelButton.Y = CancelButtonOffsetY;
                _cancelButton.SetVisible(showCancelButton);
                _cancelButton.SetEnabled(showCancelButton);
            }

            HideButton(_questionYesButton);
            HideButton(_questionNoButton);
            HideButton(_nexonButton);

            if (_variant is ConnectionNoticeWindowVariant.Loading or ConnectionNoticeWindowVariant.LoadingSingleGauge)
            {
                return;
            }

            switch (_buttonLayout)
            {
                case LoginUtilityDialogButtonLayout.YesNo:
                    _activePrimaryButton = _questionYesButton;
                    _activeSecondaryButton = _questionNoButton;
                    PositionButton(_activePrimaryButton, SecurityQuestionYesButtonX, DialogButtonY);
                    PositionButton(_activeSecondaryButton, SecurityQuestionNoButtonX, DialogButtonY);
                    break;
                case LoginUtilityDialogButtonLayout.Nexon:
                    _activePrimaryButton = _nexonButton;
                    PositionButton(_activePrimaryButton, NoticeNexonButtonX, DialogButtonY);
                    break;
            }
        }

        private void CenterFrame(IDXObject frame)
        {
            int width = frame?.Width > 0 ? frame.Width : 249;
            int height = frame?.Height > 0 ? frame.Height : 142;
            Position = new Point(
                Math.Max(24, (_screenWidth / 2) - (width / 2)),
                Math.Max(24, (_screenHeight / 2) - (height / 2)));
        }

        private static void HideButton(UIObject button)
        {
            if (button == null)
            {
                return;
            }

            button.SetVisible(false);
            button.SetEnabled(false);
        }

        private static void PositionButton(UIObject button, int x, int y)
        {
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            button.SetVisible(true);
            button.SetEnabled(true);
        }
    }
}
