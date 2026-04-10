using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AntiMacroChallengeWindow : UIWindowBase, ISoftKeyboardHost
    {
        private const int FallbackWindowWidth = 265;
        private const int FallbackWindowHeight = 250;
        private const int ChallengeWidth = 178;
        private const int ChallengeHeight = 53;
        private const int InputWidth = 194;
        private const int InputHeight = 16;
        private const int InputMaxLength = 12;
        private const int ManagedDoubleClickDistancePadding = 2;

        private sealed class LayoutProfile
        {
            public LayoutProfile(
                Point challengeOrigin,
                Point inputOrigin,
                Point attemptMessageOrigin,
                IReadOnlyList<Point> countdownDigitOrigins,
                Point countdownCommaOrigin)
            {
                ChallengeOrigin = challengeOrigin;
                InputOrigin = inputOrigin;
                AttemptMessageOrigin = attemptMessageOrigin;
                CountdownDigitOrigins = countdownDigitOrigins ?? throw new ArgumentNullException(nameof(countdownDigitOrigins));
                CountdownCommaOrigin = countdownCommaOrigin;
            }

            public Point ChallengeOrigin { get; }
            public Point InputOrigin { get; }
            public Point AttemptMessageOrigin { get; }
            public IReadOnlyList<Point> CountdownDigitOrigins { get; }
            public Point CountdownCommaOrigin { get; }
        }

        private static readonly LayoutProfile NormalLayout = new(
            new Point(40, 122),
            new Point(42, 189),
            new Point(222, 206),
            new[]
            {
                new Point(95, 39),
                new Point(115, 39),
                new Point(147, 39),
                new Point(167, 39)
            },
            new Point(137, 39));

        private static readonly LayoutProfile AdminLayout = new(
            new Point(81, 153),
            new Point(83, 206),
            new Point(170, 225),
            new[]
            {
                new Point(110, 39),
                new Point(130, 39),
                new Point(162, 39),
                new Point(182, 39)
            },
            new Point(150, 39));

        private readonly string _windowName;
        private readonly bool _adminVariant;
        private readonly Texture2D[] _countdownDigitTextures = new Texture2D[10];
        private readonly Point[] _countdownDigitOriginsByValue = new Point[10];
        private readonly AntiMacroEditControl _editControl;
        private readonly NativeAntiMacroEditHost _nativeEditHost;

        private SpriteFont _font;
        private Texture2D _challengeTexture;
        private Texture2D _frameTexture;
        private Texture2D _countdownCommaTexture;
        private Point _countdownCommaTextureOrigin;
        private KeyboardState _previousKeyboardState;
        private string _statusText = string.Empty;
        private string _attemptMessageFormat = AntiMacroOwnerStringPoolText.GetAttemptMessageFormat(appendFallbackSuffix: false);
        private UIObject _submitButton;
        private LayoutProfile _layout;
        private MouseState _previousMouseState;
        private int _answerCount;
        private int _expiresAt = int.MinValue;
        private bool _softKeyboardActive;
        private int _lastManagedInputClickTick = int.MinValue;
        private Point _lastManagedInputClickPosition = new(int.MinValue, int.MinValue);

        public AntiMacroChallengeWindow(string windowName, bool adminVariant, GraphicsDevice graphicsDevice)
            : base(new DXObject(0, 0, CreateFallbackFrameTexture(graphicsDevice), 0))
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _adminVariant = adminVariant;
            _layout = adminVariant ? AdminLayout : NormalLayout;

            Texture2D pixelTexture = new(graphicsDevice, 1, 1);
            pixelTexture.SetData(new[] { Color.White });
            _editControl = new AntiMacroEditControl(pixelTexture, _layout.InputOrigin, InputWidth, InputHeight, InputMaxLength);
            _editControl.UseClientAntiMacroVisualStyle();
            _nativeEditHost = new NativeAntiMacroEditHost(InputMaxLength);
            _nativeEditHost.TextChanged += OnNativeEditHostTextChanged;
            _nativeEditHost.SubmitRequested += OnNativeEditHostSubmitRequested;
            _nativeEditHost.FocusChanged += OnNativeEditHostFocusChanged;
        }

        public override string WindowName => _windowName;
        public override bool SupportsDragging => false;
        public override bool CapturesKeyboardInput => IsVisible && (UsingNativeEditHost ? _nativeEditHost.HasFocus : _editControl.HasFocus);
        bool ISoftKeyboardHost.WantsSoftKeyboard => !UsingNativeEditHost && IsVisible && _editControl.HasFocus && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.AlphaNumeric;
        int ISoftKeyboardHost.SoftKeyboardTextLength => CurrentInput?.Length ?? 0;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => InputMaxLength;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => CanSubmitAnswer();

        public event Action<string> SubmitRequested;

        public string CurrentInput => UsingNativeEditHost ? _nativeEditHost.Text : _editControl.Text;
        public int ExpiresAt => _expiresAt;
        public bool IsAdminVariant => _adminVariant;
        public Point ActiveFrameSize => new(
            _frameTexture?.Width ?? FallbackWindowWidth,
            _frameTexture?.Height ?? FallbackWindowHeight);

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            _editControl.SetFont(font);
        }

        public void TryAttachNativeEditHost(IntPtr parentWindowHandle)
        {
            if (_nativeEditHost.TryAttach(parentWindowHandle, GetNativeInputBounds()))
            {
                _nativeEditHost.SetVisible(IsVisible);
            }
        }

        public override void Hide()
        {
            base.Hide();
            _editControl.SetFocus(false);
            _nativeEditHost.SetVisible(false);
            _nativeEditHost.Blur();
            _softKeyboardActive = false;
        }

        public override void Show()
        {
            base.Show();
            if (UsingNativeEditHost)
            {
                _nativeEditHost.UpdateBounds(GetNativeInputBounds());
                _nativeEditHost.SetVisible(true);
                _nativeEditHost.Focus();
            }
            else
            {
                _editControl.ActivateByOwner();
            }

            _softKeyboardActive = false;
        }

        public void ConfigureVisualAssets(
            Texture2D frameTexture,
            IReadOnlyList<Texture2D> digitTextures,
            IReadOnlyList<Point> digitOrigins,
            Texture2D commaTexture,
            Point commaOrigin,
            UIObject submitButton,
            IReadOnlyList<Point> countdownDrawOrigins = null,
            Point? countdownCommaDrawOrigin = null,
            Point? challengeOrigin = null,
            Point? inputOrigin = null,
            Point? attemptMessageOrigin = null,
            string attemptMessageFormat = null)
        {
            _frameTexture = frameTexture;
            if (_frameTexture != null)
            {
                Frame = new DXObject(0, 0, _frameTexture, 0);
            }

            for (int i = 0; i < _countdownDigitTextures.Length; i++)
            {
                _countdownDigitTextures[i] = digitTextures != null && i < digitTextures.Count ? digitTextures[i] : null;
                _countdownDigitOriginsByValue[i] = digitOrigins != null && i < digitOrigins.Count ? digitOrigins[i] : Point.Zero;
            }

            _countdownCommaTexture = commaTexture;
            _countdownCommaTextureOrigin = commaOrigin;
            IReadOnlyList<Point> digitDrawOrigins = countdownDrawOrigins != null && countdownDrawOrigins.Count == 4
                ? countdownDrawOrigins
                : _layout.CountdownDigitOrigins;
            _layout = new LayoutProfile(
                challengeOrigin ?? _layout.ChallengeOrigin,
                inputOrigin ?? _layout.InputOrigin,
                attemptMessageOrigin ?? _layout.AttemptMessageOrigin,
                digitDrawOrigins,
                countdownCommaDrawOrigin ?? _layout.CountdownCommaOrigin);
            _editControl.UpdateLayout(_layout.InputOrigin);
            if (UsingNativeEditHost)
            {
                _nativeEditHost.UpdateBounds(GetNativeInputBounds());
            }

            _attemptMessageFormat = string.IsNullOrWhiteSpace(attemptMessageFormat)
                ? AntiMacroOwnerStringPoolText.GetAttemptMessageFormat(appendFallbackSuffix: false)
                : attemptMessageFormat;

            if (_submitButton != null)
            {
                _submitButton.ButtonClickReleased -= OnSubmitButtonReleased;
                uiButtons.Remove(_submitButton);
            }

            _submitButton = submitButton;
            if (_submitButton != null)
            {
                _submitButton.ButtonClickReleased += OnSubmitButtonReleased;
                AddButton(_submitButton);
            }
        }

        public void Configure(Texture2D challengeTexture, int expiresAt, int answerCount, string statusText)
        {
            if (!ReferenceEquals(_challengeTexture, challengeTexture))
            {
                _challengeTexture?.Dispose();
                _challengeTexture = challengeTexture;
            }

            _expiresAt = expiresAt;
            _answerCount = Math.Max(0, answerCount);
            _statusText = statusText ?? string.Empty;
            _editControl.Reset();
            _nativeEditHost.Reset();
            _softKeyboardActive = false;
            _previousMouseState = Mouse.GetState();
            _submitButton?.SetEnabled(false);
        }

        public void ClearChallenge()
        {
            _challengeTexture?.Dispose();
            _challengeTexture = null;
            _expiresAt = int.MinValue;
            _answerCount = 0;
            _statusText = string.Empty;
            _editControl.Clear();
            _nativeEditHost.Reset();
            _nativeEditHost.SetVisible(false);
            _submitButton?.SetEnabled(false);
            Hide();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            if (UsingNativeEditHost)
            {
                _nativeEditHost.UpdateBounds(GetNativeInputBounds());
                _nativeEditHost.SynchronizeState();
            }
            else
            {
                _editControl.HandleKeyboardInput(keyboardState, _previousKeyboardState);
                if (Pressed(keyboardState, Microsoft.Xna.Framework.Input.Keys.Enter) && CanSubmitAnswer())
                {
                    SubmitRequested?.Invoke(_editControl.Text);
                }
            }

            _submitButton?.SetEnabled(CanSubmitAnswer());
            _previousKeyboardState = keyboardState;
            _previousMouseState = Mouse.GetState();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return false;
            }

            bool leftJustPressed = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed && _previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released;
            bool leftHeld = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            bool leftJustReleased = mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
            Rectangle ownerBounds = GetWindowBounds();
            if (leftJustPressed)
            {
                Rectangle inputBounds = UsingNativeEditHost ? GetNativeInputBounds() : _editControl.GetBounds(ownerBounds);
                if (inputBounds.Contains(mouseState.Position))
                {
                    if (UsingNativeEditHost)
                    {
                        _nativeEditHost.BeginSelectionAtPoint(mouseState.Position);
                        _softKeyboardActive = false;
                    }
                    else
                    {
                        if (IsManagedInputDoubleClick(mouseState.Position, inputBounds))
                        {
                            _editControl.DoubleClickSelectAtMouseX(mouseState.X, ownerBounds);
                        }
                        else
                        {
                            _editControl.BeginSelectionAtMouseX(mouseState.X, ownerBounds);
                        }

                        _lastManagedInputClickTick = Environment.TickCount;
                        _lastManagedInputClickPosition = mouseState.Position;
                        _softKeyboardActive = true;
                    }

                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                }
                else if (!ContainsPoint(mouseState.X, mouseState.Y))
                {
                    if (UsingNativeEditHost)
                    {
                        _nativeEditHost.Blur();
                    }
                    else
                    {
                        _editControl.SetFocus(false);
                    }

                    _softKeyboardActive = false;
                }
            }
            else if (UsingNativeEditHost && leftHeld && _nativeEditHost.IsSelectingWithMouse)
            {
                _nativeEditHost.UpdateSelectionAtPoint(mouseState.Position);
            }
            else if (!UsingNativeEditHost && leftHeld && _editControl.IsSelectingWithMouse)
            {
                _editControl.UpdateSelectionAtMouseX(mouseState.X, ownerBounds);
            }

            if (UsingNativeEditHost && leftJustReleased)
            {
                _nativeEditHost.EndMouseSelection();
            }

            if (!UsingNativeEditHost && leftJustReleased)
            {
                _editControl.EndMouseSelection();
            }

            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousMouseState = mouseState;
            return handled;
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
            Rectangle bounds = GetWindowBounds();
            Rectangle challengeBounds = new(
                bounds.X + _layout.ChallengeOrigin.X,
                bounds.Y + _layout.ChallengeOrigin.Y,
                ChallengeWidth,
                ChallengeHeight);

            DrawChallengeTexture(sprite, challengeBounds);
            DrawCountdown(sprite, bounds, tickCount);
            DrawAttemptMessage(sprite, bounds);
            if (!UsingNativeEditHost)
            {
                _editControl.Draw(sprite, bounds, drawChrome: true);
                _editControl.DrawImeCandidateWindow(sprite, bounds);
            }
        }

        public override void HandleCommittedText(string text)
        {
            if (UsingNativeEditHost)
            {
                return;
            }

            _editControl.HandleCommittedText(text, CapturesKeyboardInput);
        }

        public override void HandleCompositionText(string text)
        {
            if (UsingNativeEditHost)
            {
                return;
            }

            _editControl.HandleCompositionText(text, CapturesKeyboardInput);
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (UsingNativeEditHost)
            {
                return;
            }

            _editControl.HandleCompositionState(state, CapturesKeyboardInput);
        }

        public override void ClearCompositionText()
        {
            if (UsingNativeEditHost)
            {
                return;
            }

            _editControl.ClearCompositionText();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            if (UsingNativeEditHost)
            {
                return;
            }

            _editControl.HandleImeCandidateList(state, CapturesKeyboardInput);
        }

        public override void ClearImeCandidateList()
        {
            if (UsingNativeEditHost)
            {
                return;
            }

            _editControl.ClearImeCandidateList();
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => UsingNativeEditHost ? GetNativeInputBounds() : _editControl.GetBounds(GetWindowBounds());

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (UsingNativeEditHost)
            {
                if (!_nativeEditHost.HasFocus)
                {
                    errorMessage = "The anti-macro answer field is not focused.";
                    return false;
                }

                if (!_nativeEditHost.TryInsertCharacter(character))
                {
                    errorMessage = "The anti-macro answer field is full.";
                    return false;
                }

                return true;
            }

            if (!_editControl.HasFocus)
            {
                errorMessage = "The anti-macro answer field is not focused.";
                return false;
            }

            if (!_editControl.TryInsertCharacter(character))
            {
                errorMessage = "The anti-macro answer field is full.";
                return false;
            }

            return true;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (UsingNativeEditHost)
            {
                if (!_nativeEditHost.HasFocus)
                {
                    errorMessage = "The anti-macro answer field is not focused.";
                    return false;
                }

                if (!_nativeEditHost.TryReplaceCharacterBeforeCaret(character))
                {
                    errorMessage = "That key cannot replace the current anti-macro answer character.";
                    return false;
                }

                return true;
            }

            if (!_editControl.HasFocus)
            {
                errorMessage = "The anti-macro answer field is not focused.";
                return false;
            }

            if (!_editControl.TryReplaceCharacterBeforeCaret(character))
            {
                errorMessage = "That key cannot replace the current anti-macro answer character.";
                return false;
            }

            return true;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (UsingNativeEditHost)
            {
                if (!_nativeEditHost.HasFocus)
                {
                    errorMessage = "The anti-macro answer field is not focused.";
                    return false;
                }

                if (!_nativeEditHost.TryBackspace())
                {
                    errorMessage = "The anti-macro answer field is already empty.";
                    return false;
                }

                return true;
            }

            if (!_editControl.HasFocus)
            {
                errorMessage = "The anti-macro answer field is not focused.";
                return false;
            }

            if (!_editControl.TryBackspace())
            {
                errorMessage = "The anti-macro answer field is already empty.";
                return false;
            }

            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!CanSubmitAnswer())
            {
                errorMessage = "The anti-macro answer cannot be submitted yet.";
                return false;
            }

            SubmitRequested?.Invoke(CurrentInput);
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

        private void DrawChallengeTexture(SpriteBatch sprite, Rectangle bounds)
        {
            if (_challengeTexture != null)
            {
                Rectangle sourceBounds = new(
                    0,
                    0,
                    Math.Min(bounds.Width, _challengeTexture.Width),
                    Math.Min(bounds.Height, _challengeTexture.Height));
                if (sourceBounds.Width > 0 && sourceBounds.Height > 0)
                {
                    sprite.Draw(
                        _challengeTexture,
                        new Vector2(bounds.X, bounds.Y),
                        sourceBounds,
                        Color.White);
                }

                return;
            }

            if (_font != null && !string.IsNullOrWhiteSpace(_statusText))
            {
                DrawFallbackMessage(sprite, _statusText, bounds, new Color(216, 216, 216));
            }
        }

        private void DrawCountdown(SpriteBatch sprite, Rectangle bounds, int tickCount)
        {
            int remainingSeconds = GetRemainingSeconds(tickCount);
            int[] digits =
            {
                Math.Clamp(remainingSeconds / 60 / 10, 0, 9),
                Math.Clamp(remainingSeconds / 60 % 10, 0, 9),
                Math.Clamp(remainingSeconds % 60 / 10, 0, 9),
                Math.Clamp(remainingSeconds % 60 % 10, 0, 9)
            };

            bool hasDigitArt = _countdownCommaTexture != null;
            for (int i = 0; i < digits.Length && hasDigitArt; i++)
            {
                hasDigitArt = _countdownDigitTextures[digits[i]] != null;
            }

            if (hasDigitArt)
            {
                for (int i = 0; i < digits.Length; i++)
                {
                    int digit = digits[i];
                    Point origin = _countdownDigitOriginsByValue[digit];
                    Point drawOrigin = _layout.CountdownDigitOrigins[i];
                    sprite.Draw(
                        _countdownDigitTextures[digit],
                        new Vector2(bounds.X + drawOrigin.X - origin.X, bounds.Y + drawOrigin.Y - origin.Y),
                        Color.White);
                }

                sprite.Draw(
                    _countdownCommaTexture,
                    new Vector2(bounds.X + _layout.CountdownCommaOrigin.X - _countdownCommaTextureOrigin.X, bounds.Y + _layout.CountdownCommaOrigin.Y - _countdownCommaTextureOrigin.Y),
                    Color.White);
                return;
            }

            if (_font == null)
            {
                return;
            }

            string timerText = $"{remainingSeconds / 60:00}:{remainingSeconds % 60:00}";
            Point digitOrigin = _layout.CountdownDigitOrigins[0];
            DrawShadowedText(sprite, timerText, new Vector2(bounds.X + digitOrigin.X, bounds.Y + digitOrigin.Y + 1), new Color(255, 236, 163));
        }

        private void DrawAttemptMessage(SpriteBatch sprite, Rectangle bounds)
        {
            string attemptMessage = AntiMacroOwnerStringPoolText.FormatAttemptMessageFromClientCounter(_answerCount, _attemptMessageFormat);
            ClientTextDrawing.Draw(
                sprite,
                attemptMessage,
                new Vector2(bounds.X + _layout.AttemptMessageOrigin.X, bounds.Y + _layout.AttemptMessageOrigin.Y),
                new Color(197, 42, 26),
                fallbackFont: _font);
        }

        private bool Pressed(KeyboardState keyboardState, Microsoft.Xna.Framework.Input.Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private bool IsManagedInputDoubleClick(Point mousePosition, Rectangle inputBounds)
        {
            if (_lastManagedInputClickTick == int.MinValue)
            {
                return false;
            }

            int elapsed = unchecked(Environment.TickCount - _lastManagedInputClickTick);
            if (elapsed < 0 || elapsed > SystemInformation.DoubleClickTime)
            {
                return false;
            }

            System.Drawing.Size doubleClickSize = SystemInformation.DoubleClickSize;
            Rectangle toleranceBounds = new(
                _lastManagedInputClickPosition.X - (doubleClickSize.Width / 2) - ManagedDoubleClickDistancePadding,
                _lastManagedInputClickPosition.Y - (doubleClickSize.Height / 2) - ManagedDoubleClickDistancePadding,
                doubleClickSize.Width + (ManagedDoubleClickDistancePadding * 2),
                doubleClickSize.Height + (ManagedDoubleClickDistancePadding * 2));
            return inputBounds.Contains(mousePosition) && toleranceBounds.Contains(mousePosition);
        }

        private bool CanSubmitAnswer()
        {
            return CanSubmitAnswer(CurrentInput, GetRemainingMilliseconds(Environment.TickCount));
        }

        internal static bool CanSubmitAnswer(string currentInput, int remainingMilliseconds)
        {
            return !string.IsNullOrWhiteSpace(currentInput) && remainingMilliseconds > 0;
        }

        private int GetRemainingSeconds(int tickCount)
        {
            return GetRemainingMilliseconds(tickCount) / 1000;
        }

        private int GetRemainingMilliseconds(int tickCount)
        {
            if (_expiresAt == int.MinValue)
            {
                return 0;
            }

            return Math.Max(0, _expiresAt - tickCount);
        }

        private bool UsingNativeEditHost => _nativeEditHost.IsAttached;

        private Rectangle GetNativeInputBounds()
        {
            return _editControl.GetBounds(GetWindowBounds());
        }

        private new Rectangle GetWindowBounds()
        {
            Point size = ActiveFrameSize;
            return new Rectangle(Position.X, Position.Y, size.X, size.Y);
        }

        private static Texture2D CreateFallbackFrameTexture(GraphicsDevice graphicsDevice)
        {
            Texture2D texture = new(graphicsDevice, FallbackWindowWidth, FallbackWindowHeight);
            Color[] data = new Color[FallbackWindowWidth * FallbackWindowHeight];
            for (int y = 0; y < FallbackWindowHeight; y++)
            {
                for (int x = 0; x < FallbackWindowWidth; x++)
                {
                    bool border = x == 0 || y == 0 || x == FallbackWindowWidth - 1 || y == FallbackWindowHeight - 1;
                    data[(y * FallbackWindowWidth) + x] = border
                        ? new Color(84, 62, 41)
                        : new Color(236, 223, 201);
                }
            }

            texture.SetData(data);
            return texture;
        }

        private void DrawFallbackMessage(SpriteBatch sprite, string message, Rectangle bounds, Color color)
        {
            if (_font == null)
            {
                return;
            }

            Vector2 size = _font.MeasureString(message);
            Vector2 position = new(
                bounds.X + Math.Max(0, (bounds.Width - size.X) / 2f),
                bounds.Y + Math.Max(0, (bounds.Height - size.Y) / 2f));
            DrawShadowedText(sprite, message, position, color);
        }

        private void DrawShadowedText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + new Vector2(1f, 1f), Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private void OnSubmitButtonReleased(UIObject sender)
        {
            if (CanSubmitAnswer())
            {
                SubmitRequested?.Invoke(CurrentInput);
            }
        }

        private void OnNativeEditHostTextChanged(string text)
        {
            _submitButton?.SetEnabled(CanSubmitAnswer());
        }

        private void OnNativeEditHostSubmitRequested()
        {
            if (CanSubmitAnswer())
            {
                SubmitRequested?.Invoke(CurrentInput);
            }
        }

        private void OnNativeEditHostFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                _softKeyboardActive = false;
            }

            _submitButton?.SetEnabled(CanSubmitAnswer());
        }
    }
}
