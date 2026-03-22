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
    public sealed class LoginUtilityDialogWindow : UIWindowBase
    {
        private const int TextOffsetX = 17;
        private const int TextOffsetY = 13;
        private const float BodyWrapWidth = 248f;
        private const int ButtonBottomMargin = 14;
        private const int ButtonGap = 12;

        private readonly UIObject _okButton;
        private readonly UIObject _yesButton;
        private readonly UIObject _noButton;
        private readonly UIObject _acceptButton;
        private readonly UIObject _nowButton;
        private readonly UIObject _laterButton;
        private readonly UIObject _restartButton;
        private readonly UIObject _exitButton;
        private SpriteFont _font;
        private string _title = "Login Utility";
        private string _body = string.Empty;
        private string _primaryLabel = "OK";
        private string _secondaryLabel = "Cancel";
        private UIObject _activePrimaryButton;
        private UIObject _activeSecondaryButton;
        private LoginUtilityDialogButtonLayout _buttonLayout = LoginUtilityDialogButtonLayout.Ok;

        public LoginUtilityDialogWindow(
            IDXObject frame,
            UIObject okButton,
            UIObject yesButton,
            UIObject noButton,
            UIObject acceptButton,
            UIObject nowButton,
            UIObject laterButton,
            UIObject restartButton,
            UIObject exitButton)
            : base(frame)
        {
            _okButton = RegisterButton(okButton, true);
            _yesButton = RegisterButton(yesButton, true);
            _noButton = RegisterButton(noButton, false);
            _acceptButton = RegisterButton(acceptButton, true);
            _nowButton = RegisterButton(nowButton, true);
            _laterButton = RegisterButton(laterButton, false);
            _restartButton = RegisterButton(restartButton, true);
            _exitButton = RegisterButton(exitButton, false);
        }

        public override string WindowName => MapSimulatorWindowNames.LoginUtilityDialog;

        public override bool SupportsDragging => false;

        public event Action PrimaryRequested;
        public event Action SecondaryRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(
            string title,
            string body,
            string primaryLabel,
            string secondaryLabel,
            LoginUtilityDialogButtonLayout buttonLayout)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Login Utility" : title;
            _body = body ?? string.Empty;
            _primaryLabel = string.IsNullOrWhiteSpace(primaryLabel) ? "OK" : primaryLabel;
            _secondaryLabel = string.IsNullOrWhiteSpace(secondaryLabel) ? "Cancel" : secondaryLabel;
            _buttonLayout = buttonLayout;
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
            if (_font == null)
            {
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _title,
                new Vector2(Position.X + TextOffsetX, Position.Y + TextOffsetY),
                Color.White);

            float y = Position.Y + TextOffsetY + _font.LineSpacing + 6;
            foreach (string line in WrapText(_body, BodyWrapWidth))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(Position.X + TextOffsetX, y),
                    new Color(232, 232, 232));
                y += _font.LineSpacing;
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
            if (_font == null)
            {
                return;
            }

            DrawButtonLabel(sprite, _activePrimaryButton, _primaryLabel);
            DrawButtonLabel(sprite, _activeSecondaryButton, _secondaryLabel);
        }

        private void DrawButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (_font == null || button == null || !button.ButtonVisible || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text);
            float x = Position.X + button.X + ((button.CanvasSnapshotWidth - size.X) / 2f);
            float y = Position.Y + button.Y + ((button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, text, new Vector2(x, y), Color.White);
        }

        private UIObject RegisterButton(UIObject button, bool primaryButton)
        {
            if (button == null)
            {
                return null;
            }

            button.SetVisible(false);
            button.SetEnabled(false);
            button.ButtonClickReleased += _ =>
            {
                if (primaryButton)
                {
                    PrimaryRequested?.Invoke();
                }
                else
                {
                    SecondaryRequested?.Invoke();
                }
            };
            AddButton(button);
            return button;
        }

        private void ConfigureButtons()
        {
            _activePrimaryButton = null;
            _activeSecondaryButton = null;

            HideButton(_okButton);
            HideButton(_yesButton);
            HideButton(_noButton);
            HideButton(_acceptButton);
            HideButton(_nowButton);
            HideButton(_laterButton);
            HideButton(_restartButton);
            HideButton(_exitButton);

            switch (_buttonLayout)
            {
                case LoginUtilityDialogButtonLayout.YesNo:
                    _activePrimaryButton = _yesButton ?? _okButton;
                    _activeSecondaryButton = _noButton;
                    break;
                case LoginUtilityDialogButtonLayout.Accept:
                    _activePrimaryButton = _acceptButton ?? _okButton;
                    break;
                case LoginUtilityDialogButtonLayout.NowLater:
                    _activePrimaryButton = _nowButton ?? _okButton;
                    _activeSecondaryButton = _laterButton;
                    break;
                case LoginUtilityDialogButtonLayout.RestartExit:
                    _activePrimaryButton = _restartButton ?? _okButton;
                    _activeSecondaryButton = _exitButton;
                    break;
                default:
                    _activePrimaryButton = _okButton;
                    break;
            }

            if (_activePrimaryButton != null && _activeSecondaryButton != null)
            {
                int totalWidth = _activePrimaryButton.CanvasSnapshotWidth + ButtonGap + _activeSecondaryButton.CanvasSnapshotWidth;
                int startX = Math.Max(0, ((CurrentFrame?.Width ?? 312) - totalWidth) / 2);
                int buttonY = Math.Max(0, (CurrentFrame?.Height ?? 132) - Math.Max(_activePrimaryButton.CanvasSnapshotHeight, _activeSecondaryButton.CanvasSnapshotHeight) - ButtonBottomMargin);

                PositionButton(_activePrimaryButton, startX, buttonY);
                PositionButton(_activeSecondaryButton, startX + _activePrimaryButton.CanvasSnapshotWidth + ButtonGap, buttonY);
            }
            else if (_activePrimaryButton != null)
            {
                int buttonX = Math.Max(0, ((CurrentFrame?.Width ?? 312) - _activePrimaryButton.CanvasSnapshotWidth) / 2);
                int buttonY = Math.Max(0, (CurrentFrame?.Height ?? 132) - _activePrimaryButton.CanvasSnapshotHeight - ButtonBottomMargin);
                PositionButton(_activePrimaryButton, buttonX, buttonY);
            }
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
