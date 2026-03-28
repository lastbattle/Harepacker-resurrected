using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;

namespace HaCreator.MapSimulator.UI
{
    internal enum SoftKeyboardConstraintProfile
    {
        AlphaNumericToggle,
        NumericOnly
    }

    internal enum SoftKeyboardInputMode
    {
        Alphabetic,
        Numeric
    }

    internal interface ISoftKeyboardHost
    {
        bool WantsSoftKeyboard { get; }
        Rectangle GetSoftKeyboardAnchorBounds();
        SoftKeyboardConstraintProfile SoftKeyboardConstraintProfile { get; }
        SoftKeyboardInputMode SoftKeyboardInputMode { get; set; }
        int SoftKeyboardTextLength { get; }
        int SoftKeyboardMaxLength { get; }
        bool TryInsertSoftKeyboardCharacter(char character, out string errorMessage);
        bool TryBackspaceSoftKeyboard(out string errorMessage);
        void OnSoftKeyboardClosed();
    }

    internal sealed class SoftKeyboardUI : UIWindowBase
    {
        private enum SoftKeyboardVisualState
        {
            Normal = 0,
            Pressed = 1,
            Disabled = 2,
            MouseOver = 3
        }

        private static readonly char[] KeyCharacters = "1234567890QWERTYUIOPASDFGHJKLZXCVBNM".ToCharArray();
        private static readonly int[] RowStarts = { 0, 10, 20, 29 };
        private static readonly int[] RowLengths = { 10, 10, 9, 7 };
        private const int KeyOriginXRow0 = 24;
        private const int KeyOriginXRow1 = 47;
        private const int KeyOriginXRow2 = 58;
        private const int KeyOriginXRow3 = 74;
        private const int KeyOriginY = 20;
        private const int KeyPitch = 23;
        private const int BackspaceX = 254;
        private const int BackspaceY = 20;
        private const int ModeButtonY = 4;
        private const int AlphaButtonX = 182;
        private const int NumericButtonX = 214;
        private const int ModeButtonWidth = 26;
        private const int ModeButtonHeight = 12;
        private const int CloseButtonX = 268;
        private const int CloseButtonY = 4;
        private const int CloseButtonSize = 12;

        private readonly Texture2D _backgroundTexture;
        private readonly Texture2D[] _keyTextures;
        private readonly Texture2D[] _backspaceTextures;
        private readonly Texture2D _pixelTexture;
        private readonly int _screenWidth;
        private readonly int _screenHeight;

        private SpriteFont _font;
        private ISoftKeyboardHost _host;
        private int _hoveredKeyIndex = -1;
        private bool _hoveredBackspace;
        private bool _hoveredAlphaButton;
        private bool _hoveredNumericButton;
        private bool _hoveredCloseButton;
        private int _pressedKeyIndex = -1;
        private bool _pressedBackspace;
        private bool _pressedAlphaButton;
        private bool _pressedNumericButton;
        private bool _pressedCloseButton;
        private string _statusMessage = string.Empty;

        public SoftKeyboardUI(
            IDXObject frame,
            Texture2D backgroundTexture,
            Texture2D[] keyTextures,
            Texture2D[] backspaceTextures,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
            : base(frame)
        {
            _backgroundTexture = backgroundTexture;
            _keyTextures = keyTextures ?? throw new ArgumentNullException(nameof(keyTextures));
            _backspaceTextures = backspaceTextures ?? throw new ArgumentNullException(nameof(backspaceTextures));
            _screenWidth = Math.Max(0, screenWidth);
            _screenHeight = Math.Max(0, screenHeight);

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            IsVisible = false;
        }

        public override string WindowName => "SoftKeyboard";
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => false;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SyncHost(ISoftKeyboardHost host)
        {
            if (!ReferenceEquals(_host, host))
            {
                _host = host;
                _statusMessage = string.Empty;
                ResetVisualState();
            }

            IsVisible = host != null;
            if (!IsVisible)
            {
                return;
            }

            Rectangle anchor = host.GetSoftKeyboardAnchorBounds();
            int desiredX = anchor.X;
            int desiredY = anchor.Bottom + 4;
            int width = _backgroundTexture?.Width ?? CurrentFrame?.Width ?? 290;
            int height = _backgroundTexture?.Height ?? CurrentFrame?.Height ?? 119;

            if (_screenHeight > 0 && desiredY + height > _screenHeight)
            {
                desiredY = Math.Max(0, anchor.Y - height - 4);
            }

            if (_screenWidth > 0)
            {
                desiredX = Math.Clamp(desiredX, 0, Math.Max(0, _screenWidth - width));
            }

            if (_screenHeight > 0)
            {
                desiredY = Math.Clamp(desiredY, 0, Math.Max(0, _screenHeight - height));
            }

            Position = new Point(desiredX, desiredY);
        }

        public void Dismiss()
        {
            base.Hide();
            _host = null;
            _statusMessage = string.Empty;
            ResetVisualState();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible)
            {
                ResetVisualState();
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
            if (!IsVisible || _host == null)
            {
                return;
            }

            if (_backgroundTexture != null)
            {
                sprite.Draw(_backgroundTexture, new Vector2(Position.X, Position.Y), Color.White);
            }

            DrawModeButton(sprite, GetAlphaButtonBounds(), "ABC", IsAlphabeticModeActive(), _hoveredAlphaButton, _pressedAlphaButton, _host.SoftKeyboardConstraintProfile == SoftKeyboardConstraintProfile.NumericOnly);
            DrawModeButton(sprite, GetNumericButtonBounds(), "123", IsNumericModeActive(), _hoveredNumericButton, _pressedNumericButton, false);
            DrawCloseButton(sprite, GetCloseButtonBounds());
            DrawBackspace(sprite, GetBackspaceBounds());

            for (int keyIndex = 0; keyIndex < KeyCharacters.Length; keyIndex++)
            {
                DrawKey(sprite, keyIndex);
            }

            if (_font != null && !string.IsNullOrWhiteSpace(_statusMessage))
            {
                sprite.DrawString(
                    _font,
                    _statusMessage,
                    new Vector2(Position.X + 12, Position.Y + 4),
                    new Color(96, 24, 24),
                    0f,
                    Vector2.Zero,
                    0.55f,
                    SpriteEffects.None,
                    0f);
            }
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible || _host == null)
            {
                return false;
            }

            Point mousePoint = new(mouseState.X, mouseState.Y);
            UpdateHoverState(mousePoint);

            if (!ContainsPoint(mousePoint.X, mousePoint.Y))
            {
                if (mouseState.LeftButton == ButtonState.Released)
                {
                    ResetPressedState();
                }

                return false;
            }

            mouseCursor?.SetMouseCursorMovedToClickableItem();

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool leftReleased = mouseState.LeftButton == ButtonState.Released;

            if (leftJustPressed)
            {
                _pressedCloseButton = _hoveredCloseButton;
                _pressedAlphaButton = _hoveredAlphaButton;
                _pressedNumericButton = _hoveredNumericButton;
                _pressedBackspace = _hoveredBackspace;
                _pressedKeyIndex = _hoveredKeyIndex;
            }
            else if (leftReleased)
            {
                if (_pressedCloseButton && _hoveredCloseButton)
                {
                    _host.OnSoftKeyboardClosed();
                    Dismiss();
                    return true;
                }

                if (_pressedAlphaButton && _hoveredAlphaButton && _host.SoftKeyboardConstraintProfile == SoftKeyboardConstraintProfile.AlphaNumericToggle)
                {
                    _host.SoftKeyboardInputMode = SoftKeyboardInputMode.Alphabetic;
                    _statusMessage = string.Empty;
                }
                else if (_pressedNumericButton && _hoveredNumericButton)
                {
                    _host.SoftKeyboardInputMode = SoftKeyboardInputMode.Numeric;
                    _statusMessage = string.Empty;
                }
                else if (_pressedBackspace && _hoveredBackspace)
                {
                    _host.TryBackspaceSoftKeyboard(out _statusMessage);
                }
                else if (_pressedKeyIndex >= 0 && _pressedKeyIndex == _hoveredKeyIndex && IsKeyEnabled(_pressedKeyIndex))
                {
                    _host.TryInsertSoftKeyboardCharacter(GetCharacterForKey(_pressedKeyIndex), out _statusMessage);
                }

                ResetPressedState();
            }

            return true;
        }

        private void DrawKey(SpriteBatch sprite, int keyIndex)
        {
            Rectangle bounds = GetKeyBounds(keyIndex);
            SoftKeyboardVisualState state = GetKeyVisualState(keyIndex);
            Texture2D texture = _keyTextures[(int)state];
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            }

            if (_font == null)
            {
                return;
            }

            char character = GetCharacterForKey(keyIndex);
            string label = character.ToString();
            Vector2 size = _font.MeasureString(label) * 0.55f;
            Color textColor = state == SoftKeyboardVisualState.Disabled
                ? new Color(110, 110, 110)
                : new Color(60, 45, 25);

            sprite.DrawString(
                _font,
                label,
                new Vector2(bounds.Center.X - (size.X / 2f), bounds.Center.Y - (size.Y / 2f) - 1f),
                textColor,
                0f,
                Vector2.Zero,
                0.55f,
                SpriteEffects.None,
                0f);
        }

        private void DrawBackspace(SpriteBatch sprite, Rectangle bounds)
        {
            SoftKeyboardVisualState state = GetBackspaceState();
            Texture2D texture = _backspaceTextures[(int)state];
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            }

            if (_font != null)
            {
                sprite.DrawString(
                    _font,
                    "BS",
                    new Vector2(bounds.X + 3, bounds.Y + 14),
                    state == SoftKeyboardVisualState.Disabled ? new Color(110, 110, 110) : new Color(60, 45, 25),
                    0f,
                    Vector2.Zero,
                    0.5f,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void DrawModeButton(SpriteBatch sprite, Rectangle bounds, string label, bool active, bool hovered, bool pressed, bool disabled)
        {
            Color fill = disabled
                ? new Color(88, 88, 88, 170)
                : active
                    ? new Color(246, 224, 154, 230)
                    : hovered
                        ? new Color(214, 214, 214, 220)
                        : new Color(180, 180, 180, 190);
            if (pressed && !disabled)
            {
                fill = new Color(150, 150, 150, 230);
            }

            sprite.Draw(_pixelTexture, bounds, fill);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(38, 38, 38));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(38, 38, 38));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(38, 38, 38));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(38, 38, 38));

            if (_font == null)
            {
                return;
            }

            Vector2 size = _font.MeasureString(label) * 0.45f;
            sprite.DrawString(
                _font,
                label,
                new Vector2(bounds.Center.X - (size.X / 2f), bounds.Center.Y - (size.Y / 2f)),
                disabled ? new Color(104, 104, 104) : new Color(48, 36, 18),
                0f,
                Vector2.Zero,
                0.45f,
                SpriteEffects.None,
                0f);
        }

        private void DrawCloseButton(SpriteBatch sprite, Rectangle bounds)
        {
            Color fill = _hoveredCloseButton ? new Color(208, 104, 104) : new Color(176, 76, 76);
            if (_pressedCloseButton)
            {
                fill = new Color(136, 44, 44);
            }

            sprite.Draw(_pixelTexture, bounds, fill);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(52, 20, 20));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(52, 20, 20));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(52, 20, 20));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(52, 20, 20));

            if (_font != null)
            {
                sprite.DrawString(
                    _font,
                    "X",
                    new Vector2(bounds.X + 3, bounds.Y + 1),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    0.45f,
                    SpriteEffects.None,
                    0f);
            }
        }

        private SoftKeyboardVisualState GetKeyVisualState(int keyIndex)
        {
            if (!IsKeyEnabled(keyIndex))
            {
                return SoftKeyboardVisualState.Disabled;
            }

            if (_pressedKeyIndex == keyIndex)
            {
                return SoftKeyboardVisualState.Pressed;
            }

            if (_hoveredKeyIndex == keyIndex)
            {
                return SoftKeyboardVisualState.MouseOver;
            }

            return SoftKeyboardVisualState.Normal;
        }

        private SoftKeyboardVisualState GetBackspaceState()
        {
            if (_host == null || _host.SoftKeyboardTextLength <= 0)
            {
                return SoftKeyboardVisualState.Disabled;
            }

            if (_pressedBackspace)
            {
                return SoftKeyboardVisualState.Pressed;
            }

            if (_hoveredBackspace)
            {
                return SoftKeyboardVisualState.MouseOver;
            }

            return SoftKeyboardVisualState.Normal;
        }

        private bool IsKeyEnabled(int keyIndex)
        {
            if (_host == null || keyIndex < 0 || keyIndex >= KeyCharacters.Length || _host.SoftKeyboardTextLength >= _host.SoftKeyboardMaxLength)
            {
                return false;
            }

            if (_host.SoftKeyboardConstraintProfile == SoftKeyboardConstraintProfile.NumericOnly)
            {
                return keyIndex < 10;
            }

            return _host.SoftKeyboardInputMode == SoftKeyboardInputMode.Alphabetic
                ? keyIndex >= 10
                : keyIndex < 10;
        }

        private char GetCharacterForKey(int keyIndex)
        {
            char character = KeyCharacters[Math.Clamp(keyIndex, 0, KeyCharacters.Length - 1)];
            return _host?.SoftKeyboardInputMode == SoftKeyboardInputMode.Numeric || keyIndex < 10
                ? character
                : char.ToUpperInvariant(character);
        }

        private void UpdateHoverState(Point mousePoint)
        {
            _hoveredCloseButton = GetCloseButtonBounds().Contains(mousePoint);
            _hoveredAlphaButton = GetAlphaButtonBounds().Contains(mousePoint);
            _hoveredNumericButton = GetNumericButtonBounds().Contains(mousePoint);
            _hoveredBackspace = GetBackspaceBounds().Contains(mousePoint);
            _hoveredKeyIndex = ResolveKeyIndex(mousePoint);
        }

        private int ResolveKeyIndex(Point mousePoint)
        {
            int rx = mousePoint.X - Position.X;
            int ry = mousePoint.Y - Position.Y;
            if (ry - KeyOriginY < 0)
            {
                return -1;
            }

            return ((ry - KeyOriginY) / KeyPitch) switch
            {
                0 => ResolveRowIndex(rx - KeyOriginXRow0, 0),
                1 => ResolveRowIndex(rx - KeyOriginXRow1, 10),
                2 => ResolveRowIndex(rx - KeyOriginXRow2, 20),
                3 => ResolveRowIndex(rx - KeyOriginXRow3, 29),
                _ => -1
            };
        }

        private static int ResolveRowIndex(int relativeX, int rowStartIndex)
        {
            if (relativeX < 0)
            {
                return -1;
            }

            int column = relativeX / KeyPitch;
            int row = rowStartIndex switch
            {
                0 => 0,
                10 => 1,
                20 => 2,
                29 => 3,
                _ => -1
            };

            if (row < 0 || column >= RowLengths[row])
            {
                return -1;
            }

            return rowStartIndex + column;
        }

        private Rectangle GetKeyBounds(int keyIndex)
        {
            int row = 0;
            int rowStart = 0;
            int xBase = KeyOriginXRow0;
            for (int i = 0; i < RowStarts.Length; i++)
            {
                int start = RowStarts[i];
                int end = start + RowLengths[i];
                if (keyIndex >= start && keyIndex < end)
                {
                    row = i;
                    rowStart = start;
                    xBase = i switch
                    {
                        0 => KeyOriginXRow0,
                        1 => KeyOriginXRow1,
                        2 => KeyOriginXRow2,
                        _ => KeyOriginXRow3
                    };
                    break;
                }
            }

            int column = keyIndex - rowStart;
            return new Rectangle(
                Position.X + xBase + (column * KeyPitch),
                Position.Y + KeyOriginY + (row * KeyPitch),
                _keyTextures[0]?.Width ?? 21,
                _keyTextures[0]?.Height ?? 22);
        }

        private Rectangle GetBackspaceBounds()
        {
            return new Rectangle(
                Position.X + BackspaceX,
                Position.Y + BackspaceY,
                _backspaceTextures[0]?.Width ?? 21,
                _backspaceTextures[0]?.Height ?? 45);
        }

        private Rectangle GetAlphaButtonBounds() => new(Position.X + AlphaButtonX, Position.Y + ModeButtonY, ModeButtonWidth, ModeButtonHeight);
        private Rectangle GetNumericButtonBounds() => new(Position.X + NumericButtonX, Position.Y + ModeButtonY, ModeButtonWidth, ModeButtonHeight);
        private Rectangle GetCloseButtonBounds() => new(Position.X + CloseButtonX, Position.Y + CloseButtonY, CloseButtonSize, CloseButtonSize);

        private bool IsAlphabeticModeActive() => _host?.SoftKeyboardInputMode == SoftKeyboardInputMode.Alphabetic;
        private bool IsNumericModeActive() => _host?.SoftKeyboardInputMode == SoftKeyboardInputMode.Numeric || _host?.SoftKeyboardConstraintProfile == SoftKeyboardConstraintProfile.NumericOnly;

        private void ResetPressedState()
        {
            _pressedKeyIndex = -1;
            _pressedBackspace = false;
            _pressedAlphaButton = false;
            _pressedNumericButton = false;
            _pressedCloseButton = false;
        }

        private void ResetVisualState()
        {
            _hoveredKeyIndex = -1;
            _hoveredBackspace = false;
            _hoveredAlphaButton = false;
            _hoveredNumericButton = false;
            _hoveredCloseButton = false;
            ResetPressedState();
        }
    }
}
