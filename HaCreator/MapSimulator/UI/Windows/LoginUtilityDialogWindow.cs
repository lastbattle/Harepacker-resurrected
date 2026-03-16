using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class LoginUtilityDialogWindow : UIWindowBase
    {
        private const int TextOffsetX = 17;
        private const int TextOffsetY = 13;
        private const float BodyWrapWidth = 248f;

        private readonly UIObject _primaryButton;
        private readonly UIObject _secondaryButton;
        private SpriteFont _font;
        private string _title = "Login Utility";
        private string _body = string.Empty;
        private string _primaryLabel = "OK";
        private string _secondaryLabel = "Cancel";

        public LoginUtilityDialogWindow(
            IDXObject frame,
            UIObject primaryButton,
            UIObject secondaryButton)
            : base(frame)
        {
            _primaryButton = primaryButton;
            _secondaryButton = secondaryButton;

            if (_primaryButton != null)
            {
                _primaryButton.ButtonClickReleased += _ => PrimaryRequested?.Invoke();
                AddButton(_primaryButton);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.ButtonClickReleased += _ => SecondaryRequested?.Invoke();
                AddButton(_secondaryButton);
            }
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
            bool showPrimaryButton,
            bool showSecondaryButton)
        {
            _title = string.IsNullOrWhiteSpace(title) ? "Login Utility" : title;
            _body = body ?? string.Empty;
            _primaryLabel = string.IsNullOrWhiteSpace(primaryLabel) ? "OK" : primaryLabel;
            _secondaryLabel = string.IsNullOrWhiteSpace(secondaryLabel) ? "Cancel" : secondaryLabel;

            if (_primaryButton != null)
            {
                _primaryButton.SetVisible(showPrimaryButton);
                _primaryButton.SetEnabled(showPrimaryButton);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.SetVisible(showSecondaryButton);
                _secondaryButton.SetEnabled(showSecondaryButton);
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

            DrawButtonLabel(sprite, _primaryButton, _primaryLabel);
            DrawButtonLabel(sprite, _secondaryButton, _secondaryLabel);
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
