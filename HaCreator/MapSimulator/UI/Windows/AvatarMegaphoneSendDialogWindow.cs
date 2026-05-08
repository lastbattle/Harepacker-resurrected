using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class AvatarMegaphoneSendDialogWindow : UIWindowBase
    {
        private const int EditX = 48;
        private const int EditY = 81;
        private const int EditWidth = 107;
        private const int EditHeight = 60;
        private const int WhisperCheckX = 10;
        private const int WhisperCheckY = 147;
        private const int WhisperCheckSize = 14;
        private const int MaxInputCharacters = 240;

        private readonly Texture2D _pixel;
        private readonly Texture2D _checkBoxNormal;
        private readonly Texture2D _checkBoxChecked;
        private Func<AvatarMegaphoneSendDialogSnapshot> _snapshotProvider;
        private Func<string, bool, string> _okHandler;
        private Action _cancelHandler;
        private KeyboardState _previousKeyboardState;
        private string _editText = string.Empty;
        private bool _whisperChecked = true;
        private bool _isEditing = true;

        public AvatarMegaphoneSendDialogWindow(
            IDXObject frame,
            Texture2D pixel,
            Texture2D checkBoxNormal,
            Texture2D checkBoxChecked,
            Point position)
            : base(frame, position)
        {
            _pixel = pixel;
            _checkBoxNormal = checkBoxNormal;
            _checkBoxChecked = checkBoxChecked;
            SupportsDragging = false;
        }

        public override string WindowName => MapSimulatorWindowNames.AvatarMegaphoneSendDialog;
        public override bool CapturesKeyboardInput => IsVisible && _isEditing;
        public override bool IsModalDialogOwner => IsVisible;

        internal void SetSnapshotProvider(Func<AvatarMegaphoneSendDialogSnapshot> provider)
        {
            _snapshotProvider = provider;
        }

        internal void SetActions(Func<string, bool, string> okHandler, Action cancelHandler)
        {
            _okHandler = okHandler;
            _cancelHandler = cancelHandler;
        }

        internal void InitializeControls(UIObject okButton, UIObject cancelButton)
        {
            ConfigureButton(okButton, HandleOk);
            ConfigureButton(cancelButton, HandleCancel);
        }

        public override void Show()
        {
            AvatarMegaphoneSendDialogSnapshot snapshot = _snapshotProvider?.Invoke()
                ?? new AvatarMegaphoneSendDialogSnapshot(5390000, string.Empty, true, Array.Empty<string>(), string.Empty);
            _editText = AvatarMegaphoneRuntime.JoinFragmentsForDialogEdit(snapshot.MessageFragments);
            _whisperChecked = snapshot.WhisperChecked;
            _isEditing = true;
            _previousKeyboardState = Keyboard.GetState();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            HandleKeyboardInput(keyboardState);
            _previousKeyboardState = keyboardState;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            Rectangle checkBounds = new(Position.X + WhisperCheckX, Position.Y + WhisperCheckY, WhisperCheckSize, WhisperCheckSize);
            if (checkBounds.Contains(mouseState.X, mouseState.Y))
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    _whisperChecked = !_whisperChecked;
                }

                return true;
            }

            return base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
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
            DrawCheckBox(sprite);

            if (!CanDrawWindowText)
            {
                return;
            }

            AvatarMegaphoneSendDialogSnapshot snapshot = _snapshotProvider?.Invoke()
                ?? new AvatarMegaphoneSendDialogSnapshot(5390000, string.Empty, _whisperChecked, Array.Empty<string>(), string.Empty);

            Color textColor = new(48, 40, 35);
            Color muted = new(102, 93, 83);
            DrawWindowText(sprite, $"Item {snapshot.ItemId}", new Vector2(Position.X + 15, Position.Y + 19), muted, 0.34f);
            DrawWindowText(sprite, "Whisper", new Vector2(Position.X + 28, Position.Y + 146), muted, 0.36f);
            DrawWindowText(sprite, Truncate(snapshot.LastStatus, 30), new Vector2(Position.X + 14, Position.Y + 160), muted, 0.3f, 168);

            float y = Position.Y + EditY + 2;
            foreach (string line in AvatarMegaphoneRuntime.SplitDialogTextIntoFragments(_editText))
            {
                DrawWindowText(sprite, line, new Vector2(Position.X + EditX + 2, y), textColor, 0.38f, EditWidth - 4);
                y += 14f;
            }

            if (_isEditing && (TickCount / 450) % 2 == 0)
            {
                Vector2 caret = ResolveCaretPosition(sprite);
                DrawPixel(sprite, new Rectangle((int)caret.X, (int)caret.Y, 1, 12), textColor);
            }
        }

        private void DrawCheckBox(SpriteBatch sprite)
        {
            Texture2D texture = _whisperChecked ? _checkBoxChecked : _checkBoxNormal;
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(Position.X + WhisperCheckX, Position.Y + WhisperCheckY), Color.White);
                return;
            }

            DrawPixel(sprite, new Rectangle(Position.X + WhisperCheckX, Position.Y + WhisperCheckY, WhisperCheckSize, WhisperCheckSize), new Color(238, 231, 218));
            DrawPixel(sprite, new Rectangle(Position.X + WhisperCheckX, Position.Y + WhisperCheckY, WhisperCheckSize, 1), Color.Black);
            DrawPixel(sprite, new Rectangle(Position.X + WhisperCheckX, Position.Y + WhisperCheckY, 1, WhisperCheckSize), Color.Black);
            DrawPixel(sprite, new Rectangle(Position.X + WhisperCheckX + WhisperCheckSize - 1, Position.Y + WhisperCheckY, 1, WhisperCheckSize), Color.Black);
            DrawPixel(sprite, new Rectangle(Position.X + WhisperCheckX, Position.Y + WhisperCheckY + WhisperCheckSize - 1, WhisperCheckSize, 1), Color.Black);
            if (_whisperChecked)
            {
                DrawWindowText(sprite, "v", new Vector2(Position.X + WhisperCheckX + 2, Position.Y + WhisperCheckY - 2), Color.Black, 0.38f);
            }
        }

        private void DrawPixel(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            if (_pixel != null)
            {
                sprite.Draw(_pixel, bounds, color);
            }
        }

        private Vector2 ResolveCaretPosition(SpriteBatch sprite)
        {
            string[] fragments = AvatarMegaphoneRuntime.SplitDialogTextIntoFragments(_editText);
            int lineIndex = Math.Clamp(fragments.Length - 1, 0, 3);
            string line = fragments.ElementAtOrDefault(lineIndex) ?? string.Empty;
            float width = MeasureWindowText(sprite, line, 0.38f).X;
            return new Vector2(Position.X + EditX + 3 + Math.Min(width, EditWidth - 6), Position.Y + EditY + 4 + (lineIndex * 14));
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (WasPressed(keyboardState, Keys.Escape))
            {
                HandleCancel();
                return;
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
                AppendText(Environment.NewLine);
                return;
            }

            if (WasPressed(keyboardState, Keys.Back) && _editText.Length > 0)
            {
                _editText = _editText[..^1];
                return;
            }

            if (WasPressed(keyboardState, Keys.Tab) || WasPressed(keyboardState, Keys.Space) && keyboardState.IsKeyDown(Keys.LeftControl))
            {
                _whisperChecked = !_whisperChecked;
                return;
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (!WasPressed(keyboardState, key) || KeyboardTextInputHelper.IsControlKey(key))
                {
                    continue;
                }

                char? value = KeyboardTextInputHelper.KeyToChar(key, shift);
                if (value.HasValue)
                {
                    AppendText(value.Value.ToString());
                }
            }
        }

        public override void HandleCommittedText(string text)
        {
            AppendText(text);
        }

        private void AppendText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            StringBuilder builder = new(_editText ?? string.Empty);
            builder.Append(text.Replace("\r\n", "\n").Replace('\r', '\n'));
            _editText = builder.ToString();
            if (_editText.Length > MaxInputCharacters)
            {
                _editText = _editText[..MaxInputCharacters];
            }
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private void HandleOk()
        {
            string message = _okHandler?.Invoke(_editText, _whisperChecked);
            if (!string.IsNullOrWhiteSpace(message))
            {
                Hide();
            }
        }

        private void HandleCancel()
        {
            Hide();
            _cancelHandler?.Invoke();
        }

        private static string Truncate(string text, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length <= maxCharacters
                ? text
                : text[..Math.Max(0, maxCharacters - 3)] + "...";
        }
    }
}
