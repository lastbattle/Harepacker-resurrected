using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    public sealed class LoginTitleSubmission
    {
        public LoginTitleSubmission(string accountName, string password, bool rememberId)
        {
            AccountName = accountName ?? string.Empty;
            Password = password ?? string.Empty;
            RememberId = rememberId;
        }

        public string AccountName { get; }
        public string Password { get; }
        public bool RememberId { get; }
    }

    public sealed class LoginTitleWindow : UIWindowBase, ISoftKeyboardHost
    {
        private enum LoginFieldFocus
        {
            None = 0,
            Account = 1,
            Password = 2,
        }

        private readonly IDXObject _titleLayer;
        private readonly Point _titleOffset;
        private readonly IDXObject _dollsLayer;
        private readonly Point _dollsOffset;
        private readonly IDXObject _signboardLayer;
        private readonly Point _signboardOffset;
        private readonly IDXObject _idFieldLayer;
        private readonly Point _idFieldOffset;
        private readonly IDXObject _passwordFieldLayer;
        private readonly Point _passwordFieldOffset;
        private readonly UIObject _loginButton;
        private readonly UIObject _guestLoginButton;
        private readonly UIObject _newButton;
        private readonly UIObject _homePageButton;
        private readonly UIObject _quitButton;
        private readonly UIObject _saveIdButton;
        private readonly UIObject _idLostButton;
        private readonly UIObject _passwordLostButton;
        private readonly UIObject _softKeyboardButton;
        private readonly Texture2D _saveIdUncheckedTexture;
        private readonly Texture2D _saveIdCheckedTexture;

        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private LoginFieldFocus _focusedField = LoginFieldFocus.Account;
        private string _accountName = string.Empty;
        private string _password = string.Empty;
        private string _statusMessage = "Enter credentials or feed login packets through the inbox.";
        private string _socketStatusMessage = string.Empty;
        private bool _rememberId;
        private bool _busy;
        private bool _softKeyboardActive;

        private static readonly Keys[] AlphaKeys = Enumerable.Range((int)Keys.A, 26).Select(value => (Keys)value).ToArray();
        private static readonly Keys[] NumberKeys = Enumerable.Range((int)Keys.D0, 10).Select(value => (Keys)value).ToArray();
        private static readonly Keys[] NumPadKeys = Enumerable.Range((int)Keys.NumPad0, 10).Select(value => (Keys)value).ToArray();

        public LoginTitleWindow(
            IDXObject frame,
            IDXObject titleLayer,
            Point titleOffset,
            IDXObject dollsLayer,
            Point dollsOffset,
            IDXObject signboardLayer,
            Point signboardOffset,
            IDXObject idFieldLayer,
            Point idFieldOffset,
            IDXObject passwordFieldLayer,
            Point passwordFieldOffset,
            UIObject loginButton,
            UIObject guestLoginButton,
            UIObject newButton,
            UIObject homePageButton,
            UIObject quitButton,
            UIObject saveIdButton,
            UIObject idLostButton,
            UIObject passwordLostButton,
            UIObject softKeyboardButton,
            Texture2D saveIdUncheckedTexture,
            Texture2D saveIdCheckedTexture)
            : base(frame)
        {
            _titleLayer = titleLayer;
            _titleOffset = titleOffset;
            _dollsLayer = dollsLayer;
            _dollsOffset = dollsOffset;
            _signboardLayer = signboardLayer;
            _signboardOffset = signboardOffset;
            _idFieldLayer = idFieldLayer;
            _idFieldOffset = idFieldOffset;
            _passwordFieldLayer = passwordFieldLayer;
            _passwordFieldOffset = passwordFieldOffset;
            _loginButton = loginButton;
            _guestLoginButton = guestLoginButton;
            _newButton = newButton;
            _homePageButton = homePageButton;
            _quitButton = quitButton;
            _saveIdButton = saveIdButton;
            _idLostButton = idLostButton;
            _passwordLostButton = passwordLostButton;
            _softKeyboardButton = softKeyboardButton;
            _saveIdUncheckedTexture = saveIdUncheckedTexture;
            _saveIdCheckedTexture = saveIdCheckedTexture;

            if (_loginButton != null)
            {
                _loginButton.ButtonClickReleased += _ => SubmitRequested?.Invoke(new LoginTitleSubmission(_accountName, _password, _rememberId));
                AddButton(_loginButton);
            }

            if (_guestLoginButton != null)
            {
                _guestLoginButton.ButtonClickReleased += _ => GuestLoginRequested?.Invoke();
                AddButton(_guestLoginButton);
            }

            if (_newButton != null)
            {
                _newButton.ButtonClickReleased += _ => NewAccountRequested?.Invoke();
                AddButton(_newButton);
            }

            if (_quitButton != null)
            {
                _quitButton.ButtonClickReleased += _ => QuitRequested?.Invoke();
                AddButton(_quitButton);
            }

            if (_homePageButton != null)
            {
                _homePageButton.ButtonClickReleased += _ => HomePageRequested?.Invoke();
                AddButton(_homePageButton);
            }

            if (_saveIdButton != null)
            {
                _saveIdButton.ButtonClickReleased += _ => ToggleRememberId();
                AddButton(_saveIdButton);
            }

            if (_idLostButton != null)
            {
                _idLostButton.ButtonClickReleased += _ => RecoverIdRequested?.Invoke();
                AddButton(_idLostButton);
            }

            if (_passwordLostButton != null)
            {
                _passwordLostButton.ButtonClickReleased += _ => RecoverPasswordRequested?.Invoke();
                AddButton(_passwordLostButton);
            }

            if (_softKeyboardButton != null)
            {
                _softKeyboardButton.ButtonClickReleased += _ => OpenSoftKeyboard();
                AddButton(_softKeyboardButton);
            }
        }

        public LoginTitleWindow(
            IDXObject frame,
            IDXObject titleLayer,
            Point titleOffset,
            IDXObject dollsLayer,
            Point dollsOffset,
            IDXObject signboardLayer,
            Point signboardOffset,
            IDXObject idFieldLayer,
            Point idFieldOffset,
            IDXObject passwordFieldLayer,
            Point passwordFieldOffset,
            UIObject loginButton,
            UIObject guestLoginButton,
            UIObject newButton,
            UIObject homePageButton,
            UIObject quitButton,
            UIObject saveIdButton,
            UIObject idLostButton,
            UIObject passwordLostButton)
            : this(
                frame,
                titleLayer,
                titleOffset,
                dollsLayer,
                dollsOffset,
                signboardLayer,
                signboardOffset,
                idFieldLayer,
                idFieldOffset,
                passwordFieldLayer,
                passwordFieldOffset,
                loginButton,
                guestLoginButton,
                newButton,
                homePageButton,
                quitButton,
                saveIdButton,
                idLostButton,
                passwordLostButton,
                null,
                null,
                null)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.LoginTitle;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => true;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _softKeyboardActive && !_busy && _focusedField != LoginFieldFocus.None;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.AlphaNumeric;
        int ISoftKeyboardHost.SoftKeyboardTextLength => GetFocusedFieldValue().Length;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => 16;
        public string AccountName => _accountName;
        public string Password => _password;
        public bool RememberId => _rememberId;

        public event Action<LoginTitleSubmission> SubmitRequested;
        public event Action GuestLoginRequested;
        public event Action NewAccountRequested;
        public event Action HomePageRequested;
        public event Action QuitRequested;
        public event Action RecoverIdRequested;
        public event Action RecoverPasswordRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void Configure(
            string accountName,
            string password,
            bool rememberId,
            string statusMessage,
            string socketStatusMessage,
            bool busy)
        {
            _accountName = accountName ?? string.Empty;
            _password = password ?? string.Empty;
            _rememberId = rememberId;
            _statusMessage = string.IsNullOrWhiteSpace(statusMessage)
                ? "Enter credentials or feed login packets through the inbox."
                : statusMessage;
            _socketStatusMessage = socketStatusMessage ?? string.Empty;
            _busy = busy;

            _loginButton?.SetEnabled(!busy);
            _guestLoginButton?.SetEnabled(!busy);
            _softKeyboardButton?.SetEnabled(!busy);
            _saveIdButton?.SetButtonState(_rememberId ? UIObjectState.Pressed : UIObjectState.Normal);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsVisible)
            {
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();

            if (Pressed(keyboardState, Keys.Tab))
            {
                _focusedField = _focusedField == LoginFieldFocus.Account ? LoginFieldFocus.Password : LoginFieldFocus.Account;
                _softKeyboardActive = _focusedField != LoginFieldFocus.None;
            }

            if (Pressed(keyboardState, Keys.Enter))
            {
                SubmitRequested?.Invoke(new LoginTitleSubmission(_accountName, _password, _rememberId));
            }

            if (Pressed(keyboardState, Keys.Back))
            {
                RemoveLastCharacter();
            }

            if (Pressed(keyboardState, Keys.Space))
            {
                AppendCharacter(' ');
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in AlphaKeys)
            {
                if (Pressed(keyboardState, key))
                {
                    char c = (char)('a' + (key - Keys.A));
                    AppendCharacter(shift ? char.ToUpperInvariant(c) : c);
                }
            }

            foreach (Keys key in NumberKeys)
            {
                if (Pressed(keyboardState, key))
                {
                    AppendCharacter((char)('0' + (key - Keys.D0)));
                }
            }

            foreach (Keys key in NumPadKeys)
            {
                if (Pressed(keyboardState, key))
                {
                    AppendCharacter((char)('0' + (key - Keys.NumPad0)));
                }
            }

            if (Pressed(keyboardState, Keys.OemPeriod) || Pressed(keyboardState, Keys.Decimal))
            {
                AppendCharacter('.');
            }

            if (Pressed(keyboardState, Keys.OemMinus) || Pressed(keyboardState, Keys.Subtract))
            {
                AppendCharacter(shift ? '_' : '-');
            }

            _previousKeyboardState = keyboardState;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight))
            {
                return true;
            }

            if (!IsVisible || mouseState.LeftButton != ButtonState.Pressed)
            {
                return false;
            }

            Point mousePoint = new Point(mouseState.X, mouseState.Y);
            if (GetAccountFieldBounds().Contains(mousePoint))
            {
                _focusedField = LoginFieldFocus.Account;
                _softKeyboardActive = !_busy;
                return true;
            }

            if (GetPasswordFieldBounds().Contains(mousePoint))
            {
                _focusedField = LoginFieldFocus.Password;
                _softKeyboardActive = !_busy;
                return true;
            }

            return false;
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
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _dollsLayer, _dollsOffset, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _titleLayer, _titleOffset, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _signboardLayer, _signboardOffset, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _idFieldLayer, _idFieldOffset, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _passwordFieldLayer, _passwordFieldOffset, centerX, centerY, drawReflectionInfo, renderParameters, TickCount);

            if (_font == null)
            {
                return;
            }

            Rectangle accountBounds = GetAccountFieldBounds();
            Rectangle passwordBounds = GetPasswordFieldBounds();

            DrawFieldValue(sprite, accountBounds, _accountName, _focusedField == LoginFieldFocus.Account, TickCount);
            DrawFieldValue(sprite, passwordBounds, new string('*', _password.Length), _focusedField == LoginFieldFocus.Password, TickCount);

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "ID",
                new Vector2(accountBounds.X - 28, accountBounds.Y + 4),
                new Color(255, 248, 223));
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "PW",
                new Vector2(passwordBounds.X - 28, passwordBounds.Y + 4),
                new Color(255, 248, 223));

            DrawWrappedText(sprite, _statusMessage, new Rectangle(Position.X + 303, Position.Y + 88, 210, 56), new Color(76, 49, 21));
            DrawWrappedText(sprite, _socketStatusMessage, new Rectangle(Position.X + 228, Position.Y + 356, 332, 34), new Color(235, 224, 189));
            DrawRememberIdLabel(sprite);
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

            DrawButtonLabel(sprite, _loginButton, "Login");
            DrawButtonLabel(sprite, _guestLoginButton, "Guest");
            DrawButtonLabel(sprite, _newButton, "New");
            DrawButtonLabel(sprite, _homePageButton, "Home");
            DrawButtonLabel(sprite, _quitButton, "Quit");
            DrawButtonLabel(sprite, _idLostButton, "Find ID");
            DrawButtonLabel(sprite, _passwordLostButton, "Find PW");
        }

        private void DrawLayer(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            IDXObject layer,
            Point offset,
            int centerX,
            int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
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

        private void DrawFieldValue(SpriteBatch sprite, Rectangle bounds, string text, bool focused, int tickCount)
        {
            string drawText = text ?? string.Empty;
            if (focused && ((tickCount / 350) % 2 == 0))
            {
                drawText += "|";
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                drawText,
                new Vector2(bounds.X + 8, bounds.Y + 5),
                _busy ? new Color(200, 200, 200) : new Color(55, 45, 38));
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, Rectangle bounds, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float y = bounds.Y;
            foreach (string line in WrapText(text, bounds.Width))
            {
                SelectorWindowDrawing.DrawShadowedText(sprite, _font, line, new Vector2(bounds.X, y), color);
                y += _font.LineSpacing;
                if (y > bounds.Bottom - _font.LineSpacing)
                {
                    break;
                }
            }
        }

        private void DrawRememberIdLabel(SpriteBatch sprite)
        {
            if (_font == null)
            {
                return;
            }

            Texture2D indicatorTexture = _rememberId ? _saveIdCheckedTexture : _saveIdUncheckedTexture;
            Vector2 labelPosition;
            if (indicatorTexture != null && _saveIdButton != null)
            {
                int indicatorY = Position.Y + _saveIdButton.Y + Math.Max(0, (_saveIdButton.CanvasSnapshotHeight - indicatorTexture.Height) / 2);
                int indicatorX = Position.X + _saveIdButton.X;
                sprite.Draw(indicatorTexture, new Vector2(indicatorX, indicatorY), Color.White);
                labelPosition = new Vector2(indicatorX + indicatorTexture.Width + 4, indicatorY - 2);
            }
            else
            {
                labelPosition = new Vector2(Position.X + 388, Position.Y + 295);
            }

            Color color = _rememberId ? new Color(255, 245, 208) : new Color(226, 226, 226);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "Save ID",
                labelPosition,
                color);
        }

        private Rectangle GetAccountFieldBounds()
        {
            return new Rectangle(Position.X + 348, Position.Y + 240, 160, 24);
        }

        private Rectangle GetPasswordFieldBounds()
        {
            return new Rectangle(Position.X + 348, Position.Y + 266, 160, 23);
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds()
        {
            return _focusedField == LoginFieldFocus.Password
                ? GetPasswordFieldBounds()
                : GetAccountFieldBounds();
        }

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_busy || _focusedField == LoginFieldFocus.None)
            {
                errorMessage = "This field is not editable right now.";
                return false;
            }

            string value = GetFocusedFieldValue();
            if (!SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, value.Length, 16, character))
            {
                errorMessage = "That key is disabled for this field.";
                return false;
            }

            AppendCharacter(character);
            return true;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!SoftKeyboardUI.CanBackspace(GetFocusedFieldValue().Length))
            {
                errorMessage = "Nothing to remove.";
                return false;
            }

            RemoveLastCharacter();
            return true;
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
        }

        private void ToggleRememberId()
        {
            _rememberId = !_rememberId;
            _saveIdButton?.SetButtonState(_rememberId ? UIObjectState.Pressed : UIObjectState.Normal);
        }

        private void OpenSoftKeyboard()
        {
            if (_busy)
            {
                return;
            }

            if (_focusedField == LoginFieldFocus.None)
            {
                _focusedField = LoginFieldFocus.Account;
            }

            _softKeyboardActive = true;
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private void AppendCharacter(char value)
        {
            if (_busy)
            {
                return;
            }

            switch (_focusedField)
            {
                case LoginFieldFocus.Account:
                    if (_accountName.Length < 16)
                    {
                        _accountName += value;
                    }
                    break;
                case LoginFieldFocus.Password:
                    if (_password.Length < 16)
                    {
                        _password += value;
                    }
                    break;
            }
        }

        private void RemoveLastCharacter()
        {
            if (_busy)
            {
                return;
            }

            switch (_focusedField)
            {
                case LoginFieldFocus.Account:
                    if (_accountName.Length > 0)
                    {
                        _accountName = _accountName[..^1];
                    }
                    break;
                case LoginFieldFocus.Password:
                    if (_password.Length > 0)
                    {
                        _password = _password[..^1];
                    }
                    break;
            }
        }

        private string GetFocusedFieldValue()
        {
            return _focusedField switch
            {
                LoginFieldFocus.Password => _password ?? string.Empty,
                LoginFieldFocus.Account => _accountName ?? string.Empty,
                _ => string.Empty,
            };
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder currentLine = new StringBuilder();
            foreach (string word in words)
            {
                string candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (currentLine.Length > 0 && _font.MeasureString(candidate).X > maxWidth)
                {
                    yield return currentLine.ToString();
                    currentLine.Clear();
                    currentLine.Append(word);
                }
                else
                {
                    if (currentLine.Length > 0)
                    {
                        currentLine.Append(' ');
                    }

                    currentLine.Append(word);
                }
            }

            if (currentLine.Length > 0)
            {
                yield return currentLine.ToString();
            }
        }
    }
}
