using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AntiMacroNoticeWindow : UIWindowBase
    {
        private static readonly Point AvatarOrigin = new(20, 20);
        private static readonly Point TextOrigin = new(87, 43);
        private static readonly Point OkButtonOrigin = new(84, 95);
        private static readonly Point CancelButtonOrigin = new(136, 95);
        private const float WrapWidth = 160f;
        private const int LineSpacing = 17;

        private readonly string _windowName;
        private Texture2D _avatarTexture;
        private SpriteFont _font;
        private UIObject _okButton;
        private UIObject _cancelButton;
        private string[] _lines = Array.Empty<string>();
        private string _text = string.Empty;
        private int _stringPoolId = -1;

        public AntiMacroNoticeWindow(string windowName, Texture2D frameTexture)
            : base(new DXObject(0, 0, frameTexture, 0))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;

        public event Action<int> CloseRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            _lines = WrapText(_text);
        }

        public void ConfigureVisuals(Texture2D avatarTexture, UIObject okButton, UIObject cancelButton)
        {
            _avatarTexture = avatarTexture;

            if (!ReferenceEquals(_okButton, okButton))
            {
                if (_okButton != null)
                {
                    _okButton.ButtonClickReleased -= OnOkButtonReleased;
                    uiButtons.Remove(_okButton);
                }

                _okButton = okButton;
                if (_okButton != null)
                {
                    _okButton.X = OkButtonOrigin.X;
                    _okButton.Y = OkButtonOrigin.Y;
                    _okButton.ButtonClickReleased += OnOkButtonReleased;
                    AddButton(_okButton);
                }
            }

            if (!ReferenceEquals(_cancelButton, cancelButton))
            {
                if (_cancelButton != null)
                {
                    _cancelButton.ButtonClickReleased -= OnCancelButtonReleased;
                    uiButtons.Remove(_cancelButton);
                }

                _cancelButton = cancelButton;
                if (_cancelButton != null)
                {
                    _cancelButton.X = CancelButtonOrigin.X;
                    _cancelButton.Y = CancelButtonOrigin.Y;
                    _cancelButton.ButtonClickReleased += OnCancelButtonReleased;
                    AddButton(_cancelButton);
                }
            }
        }

        public void Configure(string text, int stringPoolId)
        {
            _text = text ?? string.Empty;
            _stringPoolId = stringPoolId;
            _lines = WrapText(_text);
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
            int tickCount)
        {
            if (_avatarTexture != null)
            {
                sprite.Draw(_avatarTexture, new Vector2(Position.X + AvatarOrigin.X, Position.Y + AvatarOrigin.Y), Color.White);
            }

            if (_font == null || _lines.Length == 0)
            {
                return;
            }

            float y = Position.Y + TextOrigin.Y;
            for (int i = 0; i < _lines.Length; i++)
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    _lines[i],
                    new Vector2(Position.X + TextOrigin.X, y + (i * LineSpacing)),
                    Color.White);
            }
        }

        private string[] WrapText(string text)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return string.IsNullOrWhiteSpace(text)
                    ? Array.Empty<string>()
                    : new[] { text };
            }

            List<string> lines = new();
            string remaining = text.TrimStart();
            while (!string.IsNullOrEmpty(remaining))
            {
                int breakIndex = FindWrapIndex(remaining);
                lines.Add(remaining[..breakIndex].TrimEnd());
                remaining = breakIndex >= remaining.Length
                    ? string.Empty
                    : remaining[breakIndex..].TrimStart();
            }

            return lines.ToArray();
        }

        private int FindWrapIndex(string text)
        {
            int bestFit = text.Length;
            for (int i = 1; i <= text.Length; i++)
            {
                string candidate = text[..i];
                if (_font.MeasureString(candidate).X >= WrapWidth)
                {
                    bestFit = i > 1 ? i - 1 : 1;
                    break;
                }
            }

            if (bestFit >= text.Length)
            {
                return text.Length;
            }

            int lastSpace = text.LastIndexOf(' ', bestFit - 1, bestFit);
            return lastSpace > 0 ? lastSpace : bestFit;
        }

        private void OnOkButtonReleased(UIObject sender)
        {
            Hide();
            CloseRequested?.Invoke(1);
        }

        private void OnCancelButtonReleased(UIObject sender)
        {
            Hide();
            CloseRequested?.Invoke(2);
        }
    }
}
