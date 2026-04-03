using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public enum SoftKeyboardKeyboardType
    {
        AlphaNumeric = 0,
        AlphaNumericWithAlphaEdges = 1,
        NumericOnly = 2,
        NumericOnlyAlt = 3
    }

    internal enum SoftKeyboardKeyMode
    {
        AlphaNumeric = 0,
        AlphabeticOnly = 1,
        NumericOnly = 2,
        Disabled = 3
    }

    internal interface ISoftKeyboardHost
    {
        bool WantsSoftKeyboard { get; }
        Rectangle GetSoftKeyboardAnchorBounds();
        SoftKeyboardKeyboardType SoftKeyboardKeyboardType { get; }
        int SoftKeyboardTextLength { get; }
        int SoftKeyboardMaxLength { get; }
        bool CanSubmitSoftKeyboard { get; }
        bool TryInsertSoftKeyboardCharacter(char character, out string errorMessage);
        bool TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage);
        bool TryBackspaceSoftKeyboard(out string errorMessage);
        bool TrySubmitSoftKeyboard(out string errorMessage);
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

        private enum SoftKeyboardFunctionKey
        {
            None = 0,
            CapsLock = 1,
            LeftShift = 2,
            RightShift = 3,
            Enter = 4,
            Backspace = 5,
        }

        private enum SoftKeyboardTabMode
        {
            Numeric = 0,
            Lowercase = 1,
            Uppercase = 2,
        }

        private static readonly char[] LowercaseKeyCharacters = "1234567890qwertyuiopasdfghjklzxcvbnm-".ToCharArray();
        private static readonly char[] UppercaseKeyCharacters = "1234567890QWERTYUIOPASDFGHJKLZXCVBNM_".ToCharArray();
        private static readonly string[] T9AlphabeticGroups =
        {
            "abc",
            "def",
            "ghi",
            "jkl",
            "mno",
            "pqr",
            "stu",
            "vwx",
            "yz",
        };
        private static readonly int[] RowStarts = { 0, 10, 20, 29 };
        private static readonly int[] RowLengths = { 10, 10, 9, 8 };
        private static readonly Random DigitRandom = new();
        private static readonly Rectangle[] TabBounds =
        {
            new(8, 11, 68, 19),
            new(77, 11, 68, 19),
            new(146, 11, 68, 19),
        };
        private const int ExpandedAlphabeticGroupCount = 9;
        private const int ExpandedAlphabeticCommitKeyIndex = 9;

        private const int KeyOriginXRow0 = 24;
        private const int KeyOriginXRow1 = 47;
        private const int KeyOriginXRow2 = 58;
        private const int KeyOriginXRow3 = 74;
        private const int KeyOriginY = 20;
        private const int KeyPitch = 23;
        private const int BackspaceX = 255;
        private const int CapsLockX = 12;
        private const int LeftShiftX = 10;
        private const int RightShiftX = 238;
        private const int EnterX = 244;
        private const int MinButtonX = 260;
        private const int MaxButtonX = 246;
        private const int CloseButtonX = 274;
        private const int WindowButtonY = 4;
        private const int DefaultWindowButtonSize = 12;
        private const int ExpandedHeaderOffsetY = 17;

        private readonly Texture2D _compactBackgroundTexture;
        private readonly Texture2D _expandedBackgroundTexture;
        private readonly Texture2D[][] _keyTexturesByIndex;
        private readonly Texture2D[] _capsLockTextures;
        private readonly Texture2D[] _leftShiftTextures;
        private readonly Texture2D[] _rightShiftTextures;
        private readonly Texture2D[] _backspaceTextures;
        private readonly Texture2D[] _enterTextures;
        private readonly Texture2D[] _minButtonTextures;
        private readonly Texture2D[] _maxButtonTextures;
        private readonly Texture2D[] _closeButtonTextures;
        private readonly Texture2D _pixelTexture;
        private readonly int _screenWidth;
        private readonly int _screenHeight;

        private SpriteFont _font;
        private ISoftKeyboardHost _host;
        private bool _isExpandedLayout;
        private bool _capsLockEnabled;
        private bool _shiftEnabled;
        private int _hoveredKeyIndex = -1;
        private int _pressedKeyIndex = -1;
        private int _hoveredTabIndex = -1;
        private int _pressedTabIndex = -1;
        private SoftKeyboardFunctionKey _hoveredFunctionKey = SoftKeyboardFunctionKey.None;
        private SoftKeyboardFunctionKey _pressedFunctionKey = SoftKeyboardFunctionKey.None;
        private bool _hoveredCloseButton;
        private bool _hoveredMinButton;
        private bool _hoveredMaxButton;
        private bool _pressedCloseButton;
        private bool _pressedMinButton;
        private bool _pressedMaxButton;
        private SoftKeyboardTabMode _tabMode = SoftKeyboardTabMode.Numeric;
        private readonly int[] _digitOrder = new int[10];
        private int _switchingKeyIndex = -1;
        private int _switchingCharacterOffset = -1;
        private string _statusMessage = string.Empty;

        public SoftKeyboardUI(
            IDXObject frame,
            Texture2D compactBackgroundTexture,
            Texture2D expandedBackgroundTexture,
            Texture2D[][] keyTexturesByIndex,
            Texture2D[] capsLockTextures,
            Texture2D[] leftShiftTextures,
            Texture2D[] rightShiftTextures,
            Texture2D[] backspaceTextures,
            Texture2D[] enterTextures,
            Texture2D[] minButtonTextures,
            Texture2D[] maxButtonTextures,
            Texture2D[] closeButtonTextures,
            GraphicsDevice device,
            int screenWidth,
            int screenHeight)
            : base(frame)
        {
            _compactBackgroundTexture = compactBackgroundTexture ?? throw new ArgumentNullException(nameof(compactBackgroundTexture));
            _expandedBackgroundTexture = expandedBackgroundTexture ?? compactBackgroundTexture;
            _keyTexturesByIndex = ValidateKeyTextures(keyTexturesByIndex, nameof(keyTexturesByIndex));
            _capsLockTextures = ValidateStateTextures(capsLockTextures, nameof(capsLockTextures));
            _leftShiftTextures = ValidateStateTextures(leftShiftTextures, nameof(leftShiftTextures));
            _rightShiftTextures = ValidateStateTextures(rightShiftTextures, nameof(rightShiftTextures));
            _backspaceTextures = ValidateStateTextures(backspaceTextures, nameof(backspaceTextures));
            _enterTextures = ValidateStateTextures(enterTextures, nameof(enterTextures));
            _minButtonTextures = ValidateStateTextures(minButtonTextures, nameof(minButtonTextures));
            _maxButtonTextures = ValidateStateTextures(maxButtonTextures, nameof(maxButtonTextures));
            _closeButtonTextures = ValidateStateTextures(closeButtonTextures, nameof(closeButtonTextures));
            _screenWidth = Math.Max(0, screenWidth);
            _screenHeight = Math.Max(0, screenHeight);

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            ResetDigitOrder();
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
            bool hostChanged = !ReferenceEquals(_host, host);
            if (hostChanged)
            {
                _host = host;
                _statusMessage = string.Empty;
                ResetVisualState();
                _capsLockEnabled = false;
                _shiftEnabled = false;
                _tabMode = ResolveDefaultTabMode(host);
                ClearSwitchingCharacter();
                ResetDigitOrder();
                _isExpandedLayout = false;
            }

            IsVisible = host != null;
            if (!IsVisible)
            {
                return;
            }

            EnsureValidTabMode();

            Rectangle anchor = host.GetSoftKeyboardAnchorBounds();
            int desiredX = anchor.X;
            int desiredY = anchor.Bottom + 4;
            Texture2D backgroundTexture = GetBackgroundTexture();
            int width = backgroundTexture?.Width ?? CurrentFrame?.Width ?? 290;
            int height = backgroundTexture?.Height ?? CurrentFrame?.Height ?? 119;

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
            _isExpandedLayout = false;
            _capsLockEnabled = false;
            _shiftEnabled = false;
            _tabMode = SoftKeyboardTabMode.Numeric;
            ClearSwitchingCharacter();
            ResetDigitOrder();
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

            Texture2D backgroundTexture = GetBackgroundTexture();
            if (backgroundTexture != null)
            {
                sprite.Draw(backgroundTexture, new Vector2(Position.X, Position.Y), Color.White);
            }

            if (_isExpandedLayout)
            {
                DrawTabs(sprite);
            }

            DrawWindowButton(sprite, GetMaxButtonBounds(), _maxButtonTextures, GetWindowButtonState(_hoveredMaxButton, _pressedMaxButton), "+");
            DrawWindowButton(sprite, GetMinButtonBounds(), _minButtonTextures, GetWindowButtonState(_hoveredMinButton, _pressedMinButton), "-");
            DrawWindowButton(sprite, GetCloseButtonBounds(), _closeButtonTextures, GetWindowButtonState(_hoveredCloseButton, _pressedCloseButton), "X");

            DrawFunctionKey(sprite, SoftKeyboardFunctionKey.CapsLock, "CAPS");
            DrawFunctionKey(sprite, SoftKeyboardFunctionKey.LeftShift, "SHIFT");
            DrawFunctionKey(sprite, SoftKeyboardFunctionKey.RightShift, "SHIFT");
            DrawFunctionKey(sprite, SoftKeyboardFunctionKey.Enter, "ENTER");
            DrawFunctionKey(sprite, SoftKeyboardFunctionKey.Backspace, "BS");

            for (int keyIndex = 0; keyIndex < LowercaseKeyCharacters.Length; keyIndex++)
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

            if (!GetSoftKeyboardBounds().Contains(mousePoint))
            {
                if (mouseState.LeftButton == ButtonState.Released)
                {
                    ResetPressedState();
                }

                return false;
            }

            mouseCursor?.SetMouseCursorMovedToClickableItem();

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;
            bool leftReleased = mouseState.LeftButton == ButtonState.Released;

            if (leftPressed)
            {
                _pressedCloseButton = _hoveredCloseButton;
                _pressedMinButton = _hoveredMinButton;
                _pressedMaxButton = _hoveredMaxButton;
                _pressedTabIndex = _hoveredTabIndex;
                _pressedFunctionKey = _hoveredFunctionKey;
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

                if (_pressedMinButton && _hoveredMinButton)
                {
                    _isExpandedLayout = false;
                    SyncHost(_host);
                    ResetPressedState();
                    return true;
                }

                if (_pressedMaxButton && _hoveredMaxButton)
                {
                    _isExpandedLayout = true;
                    SyncHost(_host);
                    ResetPressedState();
                    return true;
                }

                if (_pressedTabIndex >= 0 && _pressedTabIndex == _hoveredTabIndex)
                {
                    SoftKeyboardTabMode selectedTab = (SoftKeyboardTabMode)_pressedTabIndex;
                    if (!IsTabSupported(selectedTab))
                    {
                        _statusMessage = "That recovered soft-keyboard tab is disabled for the current owner state.";
                        ResetPressedState();
                        return true;
                    }

                    _tabMode = selectedTab;
                    _capsLockEnabled = false;
                    _shiftEnabled = false;
                    ClearSwitchingCharacter();
                    _statusMessage = _tabMode switch
                    {
                        SoftKeyboardTabMode.Numeric => "Numeric tab enabled with a fresh randomized keypad.",
                        SoftKeyboardTabMode.Uppercase => "Uppercase switching-character tab enabled for the current owner.",
                        _ => "Lowercase switching-character tab enabled for the current owner.",
                    };
                    if (_tabMode == SoftKeyboardTabMode.Numeric)
                    {
                        ResetDigitOrder();
                    }
                    ResetPressedState();
                    return true;
                }

                if (_pressedFunctionKey != SoftKeyboardFunctionKey.None && _pressedFunctionKey == _hoveredFunctionKey)
                {
                    HandleFunctionKeyRelease(_pressedFunctionKey);
                    ResetPressedState();
                    return true;
                }

                if (_pressedKeyIndex >= 0 && _pressedKeyIndex == _hoveredKeyIndex && IsKeyEnabled(_pressedKeyIndex))
                {
                    if (HandleKeyRelease(_pressedKeyIndex, out string errorMessage))
                    {
                        _statusMessage = string.Empty;
                        if (!_isExpandedLayout && _shiftEnabled)
                        {
                            _shiftEnabled = false;
                        }
                    }
                    else
                    {
                        _statusMessage = errorMessage ?? string.Empty;
                    }
                }

                ResetPressedState();
            }

            return true;
        }

        private void DrawTabs(SpriteBatch sprite)
        {
            string[] labels = { "123", "abc", "ABC" };
            for (int i = 0; i < TabBounds.Length; i++)
            {
                Rectangle bounds = TranslateBounds(TabBounds[i]);
                SoftKeyboardTabMode tab = (SoftKeyboardTabMode)i;
                bool enabled = IsTabSupported(tab);
                bool active = enabled && _tabMode == tab;
                bool hovered = _hoveredTabIndex == i;
                bool pressed = _pressedTabIndex == i;
                Color fill = !enabled
                    ? new Color(76, 76, 76, 160)
                    : active
                    ? new Color(248, 224, 153, 215)
                    : pressed
                        ? new Color(163, 132, 78, 210)
                        : hovered
                            ? new Color(196, 168, 108, 210)
                            : new Color(80, 88, 102, 180);
                sprite.Draw(_pixelTexture, bounds, fill);
                DrawSimpleBorder(sprite, bounds, new Color(44, 34, 20));

                if (_font != null)
                {
                    Vector2 size = _font.MeasureString(labels[i]) * 0.5f;
                    sprite.DrawString(
                        _font,
                        labels[i],
                        new Vector2(bounds.Center.X - (size.X / 2f), bounds.Center.Y - (size.Y / 2f)),
                        !enabled
                            ? new Color(144, 144, 144)
                            : active
                                ? new Color(58, 36, 12)
                                : Color.White,
                        0f,
                        Vector2.Zero,
                        0.5f,
                        SpriteEffects.None,
                        0f);
                }
            }
        }

        private void DrawFunctionKey(SpriteBatch sprite, SoftKeyboardFunctionKey key, string fallbackLabel)
        {
            Rectangle bounds = GetFunctionKeyBounds(key);
            Texture2D[] textures = GetFunctionKeyTextures(key);
            SoftKeyboardVisualState state = GetFunctionKeyState(key);
            Texture2D texture = textures[(int)state];
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            }

            if (_font == null)
            {
                return;
            }

            Vector2 size = _font.MeasureString(fallbackLabel) * 0.4f;
            sprite.DrawString(
                _font,
                fallbackLabel,
                new Vector2(bounds.Center.X - (size.X / 2f), bounds.Center.Y - (size.Y / 2f)),
                state == SoftKeyboardVisualState.Disabled ? new Color(110, 110, 110) : new Color(60, 45, 25),
                0f,
                Vector2.Zero,
                0.4f,
                SpriteEffects.None,
                0f);
        }

        private void DrawKey(SpriteBatch sprite, int keyIndex)
        {
            if (_isExpandedLayout && keyIndex >= 10)
            {
                return;
            }

            Rectangle bounds = GetKeyBounds(keyIndex);
            SoftKeyboardVisualState state = GetKeyVisualState(keyIndex);
            Texture2D texture = _keyTexturesByIndex[keyIndex][(int)state];
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            }

            if (_font == null)
            {
                return;
            }

            string label = GetKeyLabel(keyIndex);
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

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

        private void DrawWindowButton(SpriteBatch sprite, Rectangle bounds, Texture2D[] textures, SoftKeyboardVisualState state, string fallbackLabel)
        {
            Texture2D texture = textures[(int)state];
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
                return;
            }

            Color fill = state switch
            {
                SoftKeyboardVisualState.Pressed => new Color(136, 44, 44),
                SoftKeyboardVisualState.MouseOver => new Color(208, 104, 104),
                SoftKeyboardVisualState.Disabled => new Color(88, 88, 88, 170),
                _ => new Color(176, 76, 76),
            };

            sprite.Draw(_pixelTexture, bounds, fill);
            DrawSimpleBorder(sprite, bounds, new Color(52, 20, 20));

            if (_font != null)
            {
                sprite.DrawString(
                    _font,
                    fallbackLabel,
                    new Vector2(bounds.X + 3, bounds.Y + 1),
                    Color.White,
                    0f,
                    Vector2.Zero,
                    0.45f,
                    SpriteEffects.None,
                    0f);
            }
        }

        private void DrawSimpleBorder(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
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

        private SoftKeyboardVisualState GetFunctionKeyState(SoftKeyboardFunctionKey key)
        {
            if (!CanUseFunctionKey(key))
            {
                return SoftKeyboardVisualState.Disabled;
            }

            if (IsFunctionKeyLatched(key) || _pressedFunctionKey == key)
            {
                return SoftKeyboardVisualState.Pressed;
            }

            if (_hoveredFunctionKey == key)
            {
                return SoftKeyboardVisualState.MouseOver;
            }

            return SoftKeyboardVisualState.Normal;
        }

        private static SoftKeyboardVisualState GetWindowButtonState(bool hovered, bool pressed)
        {
            if (pressed)
            {
                return SoftKeyboardVisualState.Pressed;
            }

            if (hovered)
            {
                return SoftKeyboardVisualState.MouseOver;
            }

            return SoftKeyboardVisualState.Normal;
        }

        private bool IsKeyEnabled(int keyIndex)
        {
            if (_host == null || keyIndex < 0 || keyIndex >= LowercaseKeyCharacters.Length)
            {
                return false;
            }

            SoftKeyboardKeyMode keyMode = ResolveKeyMode();
            if (keyMode == SoftKeyboardKeyMode.Disabled)
            {
                return false;
            }

            if (_isExpandedLayout)
            {
                if (keyIndex >= 10)
                {
                    return false;
                }

                return _tabMode switch
                {
                    SoftKeyboardTabMode.Numeric => IsNumericFamilyEnabled(keyMode),
                    SoftKeyboardTabMode.Lowercase or SoftKeyboardTabMode.Uppercase => keyIndex <= ExpandedAlphabeticCommitKeyIndex && IsAlphabeticFamilyEnabled(keyMode),
                    _ => false,
                };
            }

            bool isNumeric = IsNumericKey(keyIndex);
            bool isAlphabetic = !isNumeric;
            return isNumeric
                ? IsNumericFamilyEnabled(keyMode)
                : IsAlphabeticFamilyEnabled(keyMode);
        }

        private char GetCharacterForKey(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= LowercaseKeyCharacters.Length)
            {
                return '\0';
            }

            if (IsNumericKey(keyIndex))
            {
                return (char)('0' + _digitOrder[keyIndex]);
            }

            return IsUppercaseActive()
                ? UppercaseKeyCharacters[keyIndex]
                : LowercaseKeyCharacters[keyIndex];
        }

        private void HandleFunctionKeyRelease(SoftKeyboardFunctionKey key)
        {
            if (key is SoftKeyboardFunctionKey.CapsLock or SoftKeyboardFunctionKey.LeftShift or SoftKeyboardFunctionKey.RightShift)
            {
                ClearSwitchingCharacter();
            }

            switch (key)
            {
                case SoftKeyboardFunctionKey.CapsLock:
                    if (CanUseFunctionKey(key))
                    {
                        _capsLockEnabled = !_capsLockEnabled;
                    }
                    break;
                case SoftKeyboardFunctionKey.LeftShift:
                case SoftKeyboardFunctionKey.RightShift:
                    if (CanUseFunctionKey(key))
                    {
                        _shiftEnabled = !_shiftEnabled;
                    }
                    break;
                case SoftKeyboardFunctionKey.Enter:
                    ClearSwitchingCharacter();
                    if (_host.TrySubmitSoftKeyboard(out string submitMessage))
                    {
                        _statusMessage = string.Empty;
                    }
                    else
                    {
                        _statusMessage = submitMessage ?? string.Empty;
                    }
                    break;
                case SoftKeyboardFunctionKey.Backspace:
                    ClearSwitchingCharacter();
                    if (_host.TryBackspaceSoftKeyboard(out string backspaceMessage))
                    {
                        _statusMessage = string.Empty;
                    }
                    else
                    {
                        _statusMessage = backspaceMessage ?? string.Empty;
                    }
                    break;
            }
        }

        private bool CanUseFunctionKey(SoftKeyboardFunctionKey key)
        {
            SoftKeyboardKeyMode mode = ResolveKeyMode();
            return key switch
            {
                SoftKeyboardFunctionKey.CapsLock => !_isExpandedLayout && IsAlphabeticFamilyEnabled(mode),
                SoftKeyboardFunctionKey.LeftShift => !_isExpandedLayout && IsAlphabeticFamilyEnabled(mode),
                SoftKeyboardFunctionKey.RightShift => !_isExpandedLayout && IsAlphabeticFamilyEnabled(mode),
                SoftKeyboardFunctionKey.Enter => CanSubmit(),
                SoftKeyboardFunctionKey.Backspace => _host?.SoftKeyboardTextLength > 0,
                _ => false,
            };
        }

        private bool IsFunctionKeyLatched(SoftKeyboardFunctionKey key)
        {
            return key switch
            {
                SoftKeyboardFunctionKey.CapsLock => _capsLockEnabled,
                SoftKeyboardFunctionKey.LeftShift => _shiftEnabled,
                SoftKeyboardFunctionKey.RightShift => _shiftEnabled,
                _ => false,
            };
        }

        private bool IsUppercaseActive()
        {
            return _capsLockEnabled ^ _shiftEnabled;
        }

        private void UpdateHoverState(Point mousePoint)
        {
            _hoveredCloseButton = GetCloseButtonBounds().Contains(mousePoint);
            _hoveredMinButton = GetMinButtonBounds().Contains(mousePoint);
            _hoveredMaxButton = GetMaxButtonBounds().Contains(mousePoint);
            _hoveredTabIndex = ResolveTabIndex(mousePoint);
            _hoveredFunctionKey = ResolveFunctionKey(mousePoint);
            _hoveredKeyIndex = ResolveKeyIndex(mousePoint);
        }

        private int ResolveTabIndex(Point mousePoint)
        {
            if (!_isExpandedLayout)
            {
                return -1;
            }

            for (int i = 0; i < TabBounds.Length; i++)
            {
                if (TranslateBounds(TabBounds[i]).Contains(mousePoint))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsTabSupported(SoftKeyboardTabMode mode)
        {
            SoftKeyboardKeyMode keyMode = ResolveKeyMode();
            return mode switch
            {
                SoftKeyboardTabMode.Numeric => IsNumericFamilyEnabled(keyMode),
                SoftKeyboardTabMode.Lowercase or SoftKeyboardTabMode.Uppercase => IsAlphabeticFamilyEnabled(keyMode),
                _ => false,
            };
        }

        private void EnsureValidTabMode()
        {
            if (!_isExpandedLayout || _host == null)
            {
                return;
            }

            if (IsTabSupported(_tabMode))
            {
                return;
            }

            SoftKeyboardTabMode fallbackMode = IsTabSupported(SoftKeyboardTabMode.Numeric)
                ? SoftKeyboardTabMode.Numeric
                : SoftKeyboardTabMode.Lowercase;
            _tabMode = fallbackMode;
            ClearSwitchingCharacter();
            if (_tabMode == SoftKeyboardTabMode.Numeric)
            {
                ResetDigitOrder();
            }
        }

        private SoftKeyboardFunctionKey ResolveFunctionKey(Point mousePoint)
        {
            foreach (SoftKeyboardFunctionKey key in Enum.GetValues(typeof(SoftKeyboardFunctionKey)))
            {
                if (key == SoftKeyboardFunctionKey.None)
                {
                    continue;
                }

                if (GetFunctionKeyBounds(key).Contains(mousePoint))
                {
                    return key;
                }
            }

            return SoftKeyboardFunctionKey.None;
        }

        private int ResolveKeyIndex(Point mousePoint)
        {
            int rx = mousePoint.X - Position.X;
            int ry = mousePoint.Y - Position.Y;
            int keyboardOriginY = GetKeyboardOriginY();
            if (ry - keyboardOriginY < 0)
            {
                return -1;
            }

            return ((ry - keyboardOriginY) / KeyPitch) switch
            {
                0 => ResolveRowIndex(rx - KeyOriginXRow0, 0),
                1 => ResolveRowIndex(rx - KeyOriginXRow1, 10),
                2 => ResolveRowIndex(rx - KeyOriginXRow2, 20),
                3 => ResolveRowIndex(rx - KeyOriginXRow3, 29),
                _ => -1,
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
                _ => -1,
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
                        _ => KeyOriginXRow3,
                    };
                    break;
                }
            }

            int column = keyIndex - rowStart;
            Texture2D normalTexture = _keyTexturesByIndex[keyIndex][0];
            return new Rectangle(
                Position.X + xBase + (column * KeyPitch),
                Position.Y + GetKeyboardOriginY() + (row * KeyPitch),
                normalTexture?.Width ?? 21,
                normalTexture?.Height ?? 22);
        }

        private Rectangle GetFunctionKeyBounds(SoftKeyboardFunctionKey key)
        {
            return key switch
            {
                SoftKeyboardFunctionKey.CapsLock => new Rectangle(Position.X + CapsLockX, Position.Y + GetKeyboardOriginY() + KeyPitch, _capsLockTextures[0]?.Width ?? 32, _capsLockTextures[0]?.Height ?? 22),
                SoftKeyboardFunctionKey.LeftShift => new Rectangle(Position.X + LeftShiftX, Position.Y + GetKeyboardOriginY() + (KeyPitch * 2), _leftShiftTextures[0]?.Width ?? 48, _leftShiftTextures[0]?.Height ?? 22),
                SoftKeyboardFunctionKey.RightShift => new Rectangle(Position.X + RightShiftX, Position.Y + GetKeyboardOriginY() + (KeyPitch * 2), _rightShiftTextures[0]?.Width ?? 40, _rightShiftTextures[0]?.Height ?? 22),
                SoftKeyboardFunctionKey.Enter => new Rectangle(Position.X + EnterX, Position.Y + GetKeyboardOriginY() + KeyPitch, _enterTextures[0]?.Width ?? 33, _enterTextures[0]?.Height ?? 22),
                SoftKeyboardFunctionKey.Backspace => new Rectangle(Position.X + BackspaceX, Position.Y + GetKeyboardOriginY(), _backspaceTextures[0]?.Width ?? 21, _backspaceTextures[0]?.Height ?? 45),
                _ => Rectangle.Empty,
            };
        }

        private Texture2D[] GetFunctionKeyTextures(SoftKeyboardFunctionKey key)
        {
            return key switch
            {
                SoftKeyboardFunctionKey.CapsLock => _capsLockTextures,
                SoftKeyboardFunctionKey.LeftShift => _leftShiftTextures,
                SoftKeyboardFunctionKey.RightShift => _rightShiftTextures,
                SoftKeyboardFunctionKey.Enter => _enterTextures,
                SoftKeyboardFunctionKey.Backspace => _backspaceTextures,
                _ => _backspaceTextures,
            };
        }

        private Rectangle GetMinButtonBounds() => new(Position.X + MinButtonX, Position.Y + WindowButtonY, _minButtonTextures[0]?.Width ?? DefaultWindowButtonSize, _minButtonTextures[0]?.Height ?? DefaultWindowButtonSize);
        private Rectangle GetMaxButtonBounds() => new(Position.X + MaxButtonX, Position.Y + WindowButtonY, _maxButtonTextures[0]?.Width ?? DefaultWindowButtonSize, _maxButtonTextures[0]?.Height ?? DefaultWindowButtonSize);
        private Rectangle GetCloseButtonBounds() => new(Position.X + CloseButtonX, Position.Y + WindowButtonY, _closeButtonTextures[0]?.Width ?? DefaultWindowButtonSize, _closeButtonTextures[0]?.Height ?? DefaultWindowButtonSize);

        private SoftKeyboardKeyMode ResolveKeyMode()
        {
            if (_host == null)
            {
                return SoftKeyboardKeyMode.Disabled;
            }

            return ResolveKeyMode(_host.SoftKeyboardKeyboardType, _host.SoftKeyboardTextLength, _host.SoftKeyboardMaxLength);
        }

        internal static bool CanAcceptCharacter(
            SoftKeyboardKeyboardType keyboardType,
            int textLength,
            int maxLength,
            char character)
        {
            if (!char.IsLetterOrDigit(character))
            {
                return false;
            }

            SoftKeyboardKeyMode keyMode = ResolveKeyMode(keyboardType, textLength, maxLength);
            return char.IsDigit(character)
                ? IsNumericFamilyEnabled(keyMode)
                : IsAlphabeticFamilyEnabled(keyMode);
        }

        internal static bool CanBackspace(int textLength)
        {
            return textLength > 0;
        }

        internal static SoftKeyboardKeyMode ResolveKeyMode(
            SoftKeyboardKeyboardType keyboardType,
            int textLength,
            int maxLength)
        {
            textLength = Math.Max(0, textLength);
            if (maxLength < 0)
            {
                return SoftKeyboardKeyMode.Disabled;
            }

            return keyboardType switch
            {
                SoftKeyboardKeyboardType.AlphaNumeric => textLength < maxLength
                    ? SoftKeyboardKeyMode.AlphaNumeric
                    : SoftKeyboardKeyMode.Disabled,
                SoftKeyboardKeyboardType.AlphaNumericWithAlphaEdges => ResolveAlphaEdgeKeyMode(textLength, maxLength),
                SoftKeyboardKeyboardType.NumericOnly or SoftKeyboardKeyboardType.NumericOnlyAlt => textLength < maxLength
                    ? SoftKeyboardKeyMode.NumericOnly
                    : SoftKeyboardKeyMode.Disabled,
                _ => SoftKeyboardKeyMode.Disabled,
            };
        }

        private static SoftKeyboardKeyMode ResolveAlphaEdgeKeyMode(int textLength, int maxLength)
        {
            if (textLength <= 0)
            {
                return SoftKeyboardKeyMode.AlphabeticOnly;
            }

            int finalAlphabeticIndex = maxLength - 1;
            if (textLength < finalAlphabeticIndex)
            {
                return SoftKeyboardKeyMode.NumericOnly;
            }

            return textLength == finalAlphabeticIndex
                ? SoftKeyboardKeyMode.AlphabeticOnly
                : SoftKeyboardKeyMode.Disabled;
        }

        private static bool IsAlphabeticFamilyEnabled(SoftKeyboardKeyMode keyMode)
        {
            return keyMode is SoftKeyboardKeyMode.AlphaNumeric or SoftKeyboardKeyMode.AlphabeticOnly;
        }

        private static bool IsNumericFamilyEnabled(SoftKeyboardKeyMode keyMode)
        {
            return keyMode is SoftKeyboardKeyMode.AlphaNumeric or SoftKeyboardKeyMode.NumericOnly;
        }

        private static bool IsNumericKey(int keyIndex)
        {
            return keyIndex >= 0 && keyIndex <= 9;
        }

        private void ResetPressedState()
        {
            _pressedKeyIndex = -1;
            _pressedTabIndex = -1;
            _pressedFunctionKey = SoftKeyboardFunctionKey.None;
            _pressedCloseButton = false;
            _pressedMinButton = false;
            _pressedMaxButton = false;
        }

        private void ResetVisualState()
        {
            _hoveredKeyIndex = -1;
            _hoveredTabIndex = -1;
            _hoveredFunctionKey = SoftKeyboardFunctionKey.None;
            _hoveredCloseButton = false;
            _hoveredMinButton = false;
            _hoveredMaxButton = false;
            ResetPressedState();
        }

        private bool CanSubmit()
        {
            return _host?.CanSubmitSoftKeyboard == true;
        }

        private Texture2D GetBackgroundTexture()
        {
            return _isExpandedLayout ? _expandedBackgroundTexture : _compactBackgroundTexture;
        }

        private Rectangle GetSoftKeyboardBounds()
        {
            Texture2D backgroundTexture = GetBackgroundTexture();
            return new Rectangle(
                Position.X,
                Position.Y,
                backgroundTexture?.Width ?? CurrentFrame?.Width ?? 290,
                backgroundTexture?.Height ?? CurrentFrame?.Height ?? 119);
        }

        private int GetKeyboardOriginY()
        {
            return KeyOriginY + (_isExpandedLayout ? ExpandedHeaderOffsetY : 0);
        }

        private Rectangle TranslateBounds(Rectangle bounds)
        {
            return new Rectangle(Position.X + bounds.X, Position.Y + bounds.Y, bounds.Width, bounds.Height);
        }

        private static SoftKeyboardTabMode ResolveDefaultTabMode(ISoftKeyboardHost host)
        {
            return host?.SoftKeyboardKeyboardType switch
            {
                SoftKeyboardKeyboardType.NumericOnly => SoftKeyboardTabMode.Numeric,
                SoftKeyboardKeyboardType.NumericOnlyAlt => SoftKeyboardTabMode.Numeric,
                _ => SoftKeyboardTabMode.Lowercase,
            };
        }

        internal static string GetT9GroupLabel(bool uppercase, int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= T9AlphabeticGroups.Length)
            {
                return string.Empty;
            }

            return uppercase
                ? T9AlphabeticGroups[keyIndex].ToUpperInvariant()
                : T9AlphabeticGroups[keyIndex];
        }

        internal static char GetNextT9Character(bool uppercase, int keyIndex, int cycleOffset)
        {
            if (keyIndex < 0 || keyIndex >= T9AlphabeticGroups.Length)
            {
                return '\0';
            }

            string group = GetT9GroupLabel(uppercase, keyIndex);
            if (string.IsNullOrEmpty(group))
            {
                return '\0';
            }

            int offset = cycleOffset % group.Length;
            if (offset < 0)
            {
                offset += group.Length;
            }

            return group[offset];
        }

        internal static int GetDefaultDialogTabIndex(SoftKeyboardKeyboardType keyboardType)
        {
            return (int)(keyboardType switch
            {
                SoftKeyboardKeyboardType.NumericOnly => SoftKeyboardTabMode.Numeric,
                SoftKeyboardKeyboardType.NumericOnlyAlt => SoftKeyboardTabMode.Numeric,
                _ => SoftKeyboardTabMode.Lowercase,
            });
        }

        private string GetKeyLabel(int keyIndex)
        {
            if (_isExpandedLayout)
            {
                return _tabMode switch
                {
                    SoftKeyboardTabMode.Numeric => keyIndex < 10 ? ((char)('0' + _digitOrder[keyIndex])).ToString() : string.Empty,
                    SoftKeyboardTabMode.Uppercase when keyIndex == ExpandedAlphabeticCommitKeyIndex => "END",
                    SoftKeyboardTabMode.Lowercase when keyIndex == ExpandedAlphabeticCommitKeyIndex => "END",
                    SoftKeyboardTabMode.Uppercase => GetT9GroupLabel(uppercase: true, keyIndex),
                    _ => GetT9GroupLabel(uppercase: false, keyIndex),
                };
            }

            return GetCharacterForKey(keyIndex).ToString();
        }

        private bool HandleKeyRelease(int keyIndex, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!_isExpandedLayout)
            {
                ClearSwitchingCharacter();
                char value = GetCharacterForKey(keyIndex);
                return _host.TryInsertSoftKeyboardCharacter(value, out errorMessage);
            }

            if (_tabMode == SoftKeyboardTabMode.Numeric)
            {
                ClearSwitchingCharacter();
                char digit = (char)('0' + _digitOrder[keyIndex]);
                return _host.TryInsertSoftKeyboardCharacter(digit, out errorMessage);
            }

            if (keyIndex == ExpandedAlphabeticCommitKeyIndex)
            {
                ClearSwitchingCharacter();
                return true;
            }

            if (keyIndex < 0 || keyIndex >= ExpandedAlphabeticGroupCount)
            {
                errorMessage = "This recovered owner tab only exposes nine alphabetic switching groups.";
                return false;
            }

            bool uppercase = _tabMode == SoftKeyboardTabMode.Uppercase;
            bool canCycleExistingCharacter = _switchingKeyIndex == keyIndex && _host.SoftKeyboardTextLength > 0;
            int nextOffset = canCycleExistingCharacter
                ? (_switchingCharacterOffset + 1) % T9AlphabeticGroups[keyIndex].Length
                : 0;

            char nextCharacter = GetNextT9Character(uppercase, keyIndex, nextOffset);
            bool applied = canCycleExistingCharacter
                ? _host.TryReplaceLastSoftKeyboardCharacter(nextCharacter, out errorMessage)
                : _host.TryInsertSoftKeyboardCharacter(nextCharacter, out errorMessage);
            if (!applied)
            {
                ClearSwitchingCharacter();
                return false;
            }

            _switchingKeyIndex = keyIndex;
            _switchingCharacterOffset = nextOffset;
            return true;
        }

        private void ClearSwitchingCharacter()
        {
            _switchingKeyIndex = -1;
            _switchingCharacterOffset = -1;
        }

        private void ResetDigitOrder()
        {
            for (int i = 0; i < _digitOrder.Length; i++)
            {
                _digitOrder[i] = i;
            }

            for (int i = _digitOrder.Length - 1; i > 0; i--)
            {
                int swapIndex = DigitRandom.Next(i + 1);
                (_digitOrder[i], _digitOrder[swapIndex]) = (_digitOrder[swapIndex], _digitOrder[i]);
            }
        }

        private static Texture2D[][] ValidateKeyTextures(Texture2D[][] textures, string parameterName)
        {
            if (textures == null || textures.Length != LowercaseKeyCharacters.Length)
            {
                throw new ArgumentException("Expected per-key texture families for the full soft-keyboard layout.", parameterName);
            }

            for (int i = 0; i < textures.Length; i++)
            {
                ValidateStateTextures(textures[i], $"{parameterName}[{i}]");
            }

            return textures;
        }

        private static Texture2D[] ValidateStateTextures(Texture2D[] textures, string parameterName)
        {
            if (textures == null || textures.Length != 4)
            {
                throw new ArgumentException("Expected four visual-state textures.", parameterName);
            }

            return textures;
        }
    }
}
