using HaCreator.MapSimulator.Interaction;
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
    internal sealed class AllianceEditorWindow : UIWindowBase
    {
        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly IDXObject _headerLayer;
        private readonly Point _headerOffset;
        private readonly IDXObject _contentLayer;
        private readonly Point _contentOffset;
        private readonly UIObject _editButton;
        private readonly UIObject _saveButton;
        private readonly Texture2D _pixel;
        private readonly List<Rectangle> _rankBounds = new();
        private readonly StringBuilder _inputBuffer = new();

        private Func<AllianceEditorSnapshot> _snapshotProvider;
        private Action<int> _rankSelectionHandler;
        private Action _noticeFocusHandler;
        private Func<string> _beginEditHandler;
        private Func<string> _saveHandler;
        private Func<string> _cancelHandler;
        private Action<string> _draftChangeHandler;
        private Action<string> _feedbackHandler;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private Keys _lastHeldKey = Keys.None;
        private int _keyHoldStartTime;
        private int _lastKeyRepeatTime;

        public AllianceEditorWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject headerLayer,
            Point headerOffset,
            IDXObject contentLayer,
            Point contentOffset,
            UIObject editButton,
            UIObject saveButton,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _headerLayer = headerLayer;
            _headerOffset = headerOffset;
            _contentLayer = contentLayer;
            _contentOffset = contentOffset;
            _editButton = editButton;
            _saveButton = saveButton;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            if (_editButton != null)
            {
                AddButton(_editButton);
                _editButton.ButtonClickReleased += _ => ShowFeedback(_beginEditHandler?.Invoke());
            }

            if (_saveButton != null)
            {
                AddButton(_saveButton);
                _saveButton.ButtonClickReleased += _ => ShowFeedback(_saveHandler?.Invoke());
            }
        }

        public override string WindowName => MapSimulatorWindowNames.AllianceEditor;

        public override bool CapturesKeyboardInput => GetSnapshot().IsEditing;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<AllianceEditorSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            SyncInputBuffer(GetSnapshot());
            UpdateButtonStates(GetSnapshot());
        }

        internal void SetHandlers(
            Action<int> rankSelectionHandler,
            Action noticeFocusHandler,
            Func<string> beginEditHandler,
            Func<string> saveHandler,
            Func<string> cancelHandler,
            Action<string> draftChangeHandler,
            Action<string> feedbackHandler)
        {
            _rankSelectionHandler = rankSelectionHandler;
            _noticeFocusHandler = noticeFocusHandler;
            _beginEditHandler = beginEditHandler;
            _saveHandler = saveHandler;
            _cancelHandler = cancelHandler;
            _draftChangeHandler = draftChangeHandler;
            _feedbackHandler = feedbackHandler;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            AllianceEditorSnapshot snapshot = GetSnapshot();
            SyncInputBuffer(snapshot);
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                TryHandleClick(mouseState.Position);
            }

            HandleKeyboardInput(Keyboard.GetState(), snapshot, Environment.TickCount);
            _previousMouseState = mouseState;
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
            DrawLayer(sprite, _overlay, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _headerLayer, _headerOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _contentLayer, _contentOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            AllianceEditorSnapshot snapshot = GetSnapshot();
            DrawRankTitles(sprite, snapshot);
            DrawNoticeEditor(sprite, snapshot, TickCount);
            DrawSummary(sprite, snapshot);
        }

        private AllianceEditorSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new AllianceEditorSnapshot();
        }

        private void UpdateButtonStates(AllianceEditorSnapshot snapshot)
        {
            _editButton?.SetEnabled(snapshot.CanEdit && !snapshot.IsEditing);
            _saveButton?.SetEnabled(snapshot.CanEdit && snapshot.IsEditing);
        }

        private void TryHandleClick(Point mousePosition)
        {
            for (int i = 0; i < _rankBounds.Count; i++)
            {
                if (_rankBounds[i].Contains(mousePosition))
                {
                    _rankSelectionHandler?.Invoke(i);
                    return;
                }
            }

            if (GetNoticeBounds().Contains(mousePosition))
            {
                _noticeFocusHandler?.Invoke();
            }
        }

        private void DrawRankTitles(SpriteBatch sprite, AllianceEditorSnapshot snapshot)
        {
            _rankBounds.Clear();
            Rectangle bounds = new Rectangle(Position.X + 20, Position.Y + 92, 224, 118);
            sprite.Draw(_pixel, bounds, new Color(18, 25, 38, 44));

            for (int i = 0; i < snapshot.RankTitles.Count; i++)
            {
                Rectangle rowBounds = new Rectangle(bounds.X + 6, bounds.Y + 6 + (i * 20), bounds.Width - 12, 18);
                _rankBounds.Add(rowBounds);
                bool selected = snapshot.Focus == AllianceEditorFocus.RankTitle && i == snapshot.SelectedRankIndex;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(111, 163, 219, 92) : new Color(255, 255, 255, 14));
                DrawText(sprite, $"Rank {i + 1}", rowBounds.X + 4, rowBounds.Y + 1, new Color(103, 112, 124), 0.34f);
                DrawText(sprite, snapshot.RankTitles[i], rowBounds.X + 64, rowBounds.Y + 1, new Color(242, 245, 249), 0.38f);
            }
        }

        private void DrawNoticeEditor(SpriteBatch sprite, AllianceEditorSnapshot snapshot, int tickCount)
        {
            Rectangle noticeBounds = GetNoticeBounds();
            bool active = snapshot.Focus == AllianceEditorFocus.Notice || snapshot.IsEditing;
            sprite.Draw(_pixel, noticeBounds, active ? new Color(245, 248, 255, 46) : new Color(14, 21, 32, 64));

            string drawText = snapshot.IsEditing ? _inputBuffer.ToString() : snapshot.EditableText;
            DrawWrappedText(sprite, drawText, noticeBounds, new Color(232, 236, 242), 0.36f, 12);
            DrawText(sprite, "Alliance notice / focused editor field", noticeBounds.X, noticeBounds.Y - 15, new Color(104, 109, 119), 0.33f);

            if (snapshot.IsEditing && ((tickCount / 500) % 2 == 0))
            {
                int cursorX = noticeBounds.X + 8 + (int)Math.Round(_font.MeasureString(drawText).X * 0.36f);
                sprite.Draw(_pixel, new Rectangle(Math.Min(noticeBounds.Right - 3, cursorX), noticeBounds.Bottom - 16, 1, 12), new Color(238, 242, 248));
            }
        }

        private Rectangle GetNoticeBounds()
        {
            return new Rectangle(Position.X + 20, Position.Y + 224, 224, 78);
        }

        private void DrawSummary(SpriteBatch sprite, AllianceEditorSnapshot snapshot)
        {
            Rectangle summaryBounds = new Rectangle(Position.X + 16, Position.Y + 312, 232, 46);
            sprite.Draw(_pixel, summaryBounds, new Color(5, 10, 18, 88));

            int y = summaryBounds.Y + 5;
            foreach (string line in snapshot.SummaryLines.Take(3))
            {
                DrawText(sprite, line, summaryBounds.X + 6, y, new Color(228, 232, 239), 0.32f);
                y += 13;
            }
        }

        private void HandleKeyboardInput(KeyboardState keyboardState, AllianceEditorSnapshot snapshot, int tickCount)
        {
            if (!snapshot.IsEditing)
            {
                _previousKeyboardState = keyboardState;
                _lastHeldKey = Keys.None;
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                ShowFeedback(_cancelHandler?.Invoke());
                _previousKeyboardState = keyboardState;
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                ShowFeedback(_saveHandler?.Invoke());
                _previousKeyboardState = keyboardState;
                return;
            }

            if (HandleBackspace(keyboardState, tickCount))
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (_previousKeyboardState.IsKeyDown(key) || KeyboardTextInputHelper.IsControlKey(key))
                {
                    continue;
                }

                char? character = KeyboardTextInputHelper.KeyToChar(key, shift);
                if (!character.HasValue)
                {
                    continue;
                }

                _inputBuffer.Append(character.Value);
                _lastHeldKey = key;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
                _draftChangeHandler?.Invoke(_inputBuffer.ToString());
            }

            if (_lastHeldKey != Keys.None
                && !KeyboardTextInputHelper.IsControlKey(_lastHeldKey)
                && KeyboardTextInputHelper.ShouldRepeatKey(_lastHeldKey, keyboardState, _keyHoldStartTime, _lastKeyRepeatTime, tickCount))
            {
                char? repeatedCharacter = KeyboardTextInputHelper.KeyToChar(_lastHeldKey, shift);
                if (repeatedCharacter.HasValue)
                {
                    _inputBuffer.Append(repeatedCharacter.Value);
                    _lastKeyRepeatTime = tickCount;
                    _draftChangeHandler?.Invoke(_inputBuffer.ToString());
                }
            }

            _previousKeyboardState = keyboardState;
        }

        private bool HandleBackspace(KeyboardState keyboardState, int tickCount)
        {
            if (!keyboardState.IsKeyDown(Keys.Back))
            {
                return false;
            }

            if (_previousKeyboardState.IsKeyUp(Keys.Back)
                || KeyboardTextInputHelper.ShouldRepeatKey(Keys.Back, keyboardState, _keyHoldStartTime, _lastKeyRepeatTime, tickCount))
            {
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
                    _draftChangeHandler?.Invoke(_inputBuffer.ToString());
                }

                _lastHeldKey = Keys.Back;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
            }

            return true;
        }

        private void SyncInputBuffer(AllianceEditorSnapshot snapshot)
        {
            string source = snapshot.IsEditing ? snapshot.EditableText : string.Empty;
            if (source == _inputBuffer.ToString())
            {
                return;
            }

            _inputBuffer.Clear();
            _inputBuffer.Append(source);
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            layer?.DrawBackground(sprite, skeletonMeshRenderer, gameTime, Position.X + offset.X, Position.Y + offset.Y, Color.White, false, drawReflectionInfo);
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color, float scale)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                sprite.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale, int lineHeight)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.None);
            string currentLine = string.Empty;
            int drawY = bounds.Y + 6;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (_font.MeasureString(candidate).X * scale <= bounds.Width - 12)
                {
                    currentLine = candidate;
                    continue;
                }

                DrawText(sprite, currentLine, bounds.X + 6, drawY, color, scale);
                currentLine = word;
                drawY += lineHeight;
                if (drawY > bounds.Bottom - lineHeight)
                {
                    return;
                }
            }

            DrawText(sprite, currentLine, bounds.X + 6, drawY, color, scale);
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }
    }
}
