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
        private readonly IReadOnlyList<IReadOnlyList<CharacterSelectWindow.AnimationFrame>> _effectFrameSets;
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
        private readonly Texture2D _pixelTexture;

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
        private string _compositionText = string.Empty;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;

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
            IReadOnlyList<IReadOnlyList<CharacterSelectWindow.AnimationFrame>> effectFrameSets,
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
            _effectFrameSets = effectFrameSets ?? Array.Empty<IReadOnlyList<CharacterSelectWindow.AnimationFrame>>();
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
            if (frame?.Texture?.GraphicsDevice != null)
            {
                _pixelTexture = new Texture2D(frame.Texture.GraphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

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
            IReadOnlyList<IReadOnlyList<CharacterSelectWindow.AnimationFrame>> effectFrameSets,
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
                effectFrameSets,
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
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => !_busy && _focusedField != LoginFieldFocus.None;
        string ISoftKeyboardHost.GetSoftKeyboardText() => GetFocusedFieldValue();
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
            base.SetFont(font);
        }

        public override void Hide()
        {
            base.Hide();
            ClearCompositionText();
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
                ClearCompositionText();
                _focusedField = _focusedField == LoginFieldFocus.Account ? LoginFieldFocus.Password : LoginFieldFocus.Account;
                _softKeyboardActive = _focusedField != LoginFieldFocus.None;
            }

            if (Pressed(keyboardState, Keys.Enter))
            {
                SubmitRequested?.Invoke(new LoginTitleSubmission(_accountName, _password, _rememberId));
            }

            if (Pressed(keyboardState, Keys.Back))
            {
                ClearCompositionText();
                RemoveLastCharacter();
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
            DrawEffectLayers(sprite, TickCount);
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
            DrawImeCandidateWindow(sprite);
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

            Vector2 size = ClientTextDrawing.Measure((GraphicsDevice)null, text, 1.0f, _font);
            float x = Position.X + button.X + ((button.CanvasSnapshotWidth - size.X) / 2f);
            float y = Position.Y + button.Y + ((button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, text, new Vector2(x, y), Color.White);
        }

        private void DrawEffectLayers(SpriteBatch sprite, int tickCount)
        {
            if (_effectFrameSets == null || _effectFrameSets.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _effectFrameSets.Count; i++)
            {
                CharacterSelectWindow.AnimationFrame frame = ResolveAnimationFrame(_effectFrameSets[i], tickCount);
                if (frame.Texture == null)
                {
                    continue;
                }

                Vector2 drawPosition = new(Position.X + frame.Offset.X, Position.Y + frame.Offset.Y);
                sprite.Draw(frame.Texture, drawPosition, Color.White);
            }
        }

        private static CharacterSelectWindow.AnimationFrame ResolveAnimationFrame(
            IReadOnlyList<CharacterSelectWindow.AnimationFrame> frames,
            int tickCount)
        {
            if (frames == null || frames.Count == 0)
            {
                return default;
            }

            int totalDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDuration += Math.Max(1, frames[i].Delay);
            }

            if (totalDuration <= 0)
            {
                return frames[^1];
            }

            int animationTick = Math.Max(0, tickCount) % totalDuration;
            for (int i = 0; i < frames.Count; i++)
            {
                int frameDuration = Math.Max(1, frames[i].Delay);
                if (animationTick < frameDuration)
                {
                    return frames[i];
                }

                animationTick -= frameDuration;
            }

            return frames[^1];
        }

        private void DrawFieldValue(SpriteBatch sprite, Rectangle bounds, string text, bool focused, int tickCount)
        {
            string drawText = text ?? string.Empty;
            if (focused && !string.IsNullOrEmpty(_compositionText))
            {
                drawText += _focusedField == LoginFieldFocus.Password
                    ? new string('*', _compositionText.Length)
                    : _compositionText;
            }

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

        public override void HandleCommittedText(string text)
        {
            if (!IsVisible || _busy || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                AppendCharacter(character);
            }
        }

        public override void HandleCompositionText(string text)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1));
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (!IsVisible || _busy || _focusedField == LoginFieldFocus.None)
            {
                ClearCompositionText();
                return;
            }

            string sanitized = SanitizeCompositionText(state?.Text);
            if (sanitized.Length == 0)
            {
                ClearCompositionText();
                return;
            }

            int availableLength = Math.Max(0, 16 - GetFocusedFieldValue().Length);
            if (availableLength <= 0)
            {
                ClearCompositionText();
                return;
            }

            _compositionText = sanitized.Length > availableLength
                ? sanitized[..availableLength]
                : sanitized;
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            ClearImeCandidateList();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = IsVisible && !_busy && _focusedField != LoginFieldFocus.None && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
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

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_busy || _focusedField == LoginFieldFocus.None)
            {
                errorMessage = "This field is not editable right now.";
                return false;
            }

            string value = GetFocusedFieldValue();
            if (string.IsNullOrEmpty(value))
            {
                errorMessage = "Nothing to replace.";
                return false;
            }

            if (!SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, value.Length - 1, 16, character))
            {
                errorMessage = "That key is disabled for this field.";
                return false;
            }

            switch (_focusedField)
            {
                case LoginFieldFocus.Account:
                    _accountName = value[..^1] + character;
                    break;
                case LoginFieldFocus.Password:
                    _password = value[..^1] + character;
                    break;
            }

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

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_busy || _focusedField == LoginFieldFocus.None)
            {
                errorMessage = "Login is unavailable right now.";
                return false;
            }

            SubmitRequested?.Invoke(new LoginTitleSubmission(_accountName, _password, _rememberId));
            return true;
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
        }

        void ISoftKeyboardHost.SetSoftKeyboardCompositionText(string text)
        {
            HandleCompositionText(text);
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
                    if (_accountName.Length < 16 && SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, _accountName.Length, 16, value))
                    {
                        _accountName += value;
                    }
                    break;
                case LoginFieldFocus.Password:
                    if (_password.Length < 16 && SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, _password.Length, 16, value))
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

        private string SanitizeCompositionText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string currentValue = GetFocusedFieldValue();
            List<char> accepted = new(text.Length);
            foreach (char character in text)
            {
                if (!SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, currentValue.Length + accepted.Count, 16, character))
                {
                    continue;
                }

                accepted.Add(character);
                if (currentValue.Length + accepted.Count >= 16)
                {
                    break;
                }
            }

            return accepted.Count == 0
                ? string.Empty
                : new string(accepted.ToArray());
        }

        private void DrawImeCandidateWindow(SpriteBatch sprite)
        {
            if (_font == null || _pixelTexture == null || !_candidateListState.HasCandidates || _focusedField == LoginFieldFocus.None)
            {
                return;
            }

            Rectangle bounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            sprite.Draw(_pixelTexture, bounds, new Color(33, 33, 41, 235));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));
            sprite.Draw(_pixelTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            int rowHeight = Math.Max(_font.LineSpacing + 1, 16);
            int numberWidth = (int)Math.Ceiling(ClientTextDrawing.Measure((GraphicsDevice)null, $"{Math.Max(1, count)}.", 1.0f, _font).X);
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = start + i;
                string numberText = $"{i + 1}.";
                Rectangle rowBounds = new(bounds.X + 2, bounds.Y + 2 + (i * rowHeight), bounds.Width - 4, rowHeight);
                bool selected = candidateIndex == _candidateListState.Selection;
                if (selected)
                {
                    sprite.Draw(_pixelTexture, rowBounds, new Color(89, 108, 147, 220));
                }

                ClientTextDrawing.Draw(sprite, numberText, new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222), 1.0f, _font);
                ClientTextDrawing.Draw(
                    sprite,
                    _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                    new Vector2(rowBounds.X + 8 + numberWidth, rowBounds.Y),
                    selected ? Color.White : new Color(240, 235, 200),
                    1.0f,
                    _font);
            }
        }

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport)
        {
            if (ImeCandidateWindowRendering.ShouldPreferNativeWindow(_candidateListState))
            {
                return Rectangle.Empty;
            }

            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0 || _font == null)
            {
                return Rectangle.Empty;
            }

            Rectangle ownerBounds = _focusedField == LoginFieldFocus.Password
                ? GetPasswordFieldBounds()
                : GetAccountFieldBounds();
            int widestEntryWidth = 0;
            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = Math.Clamp(_candidateListState.PageStart + i, 0, _candidateListState.Candidates.Count - 1);
                string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                int entryWidth = (int)Math.Ceiling(
                    ClientTextDrawing.Measure((GraphicsDevice)null, $"{i + 1}.", 1.0f, _font).X +
                    ClientTextDrawing.Measure((GraphicsDevice)null, candidateText, 1.0f, _font).X) + 16;
                widestEntryWidth = Math.Max(widestEntryWidth, entryWidth);
            }

            int width = Math.Max(96, widestEntryWidth + 14);
            int height = (visibleCount * Math.Max(_font.LineSpacing + 1, 16)) + 4;
            int x = Math.Clamp(ownerBounds.X, 0, Math.Max(0, viewport.Width - width));
            int y = ownerBounds.Bottom + 2;
            if (y + height > viewport.Height)
            {
                y = Math.Max(0, ownerBounds.Y - height - 2);
            }

            return new Rectangle(x, y, width, height);
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int pageSize = _candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count;
            return Math.Max(0, Math.Min(pageSize, _candidateListState.Candidates.Count - start));
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
                if (currentLine.Length > 0 && ClientTextDrawing.Measure((GraphicsDevice)null, candidate, 1.0f, _font).X > maxWidth)
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
