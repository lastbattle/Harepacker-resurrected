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
    internal sealed class GuildManageWindow : UIWindowBase
    {
        private static readonly GuildManageTab[] TabOrder =
        {
            GuildManageTab.Position,
            GuildManageTab.Admission,
            GuildManageTab.Change
        };

        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly Dictionary<GuildManageTab, IDXObject> _baseLayers = new();
        private readonly Dictionary<GuildManageTab, Point> _baseOffsets = new();
        private readonly Dictionary<GuildManageTab, IDXObject> _contentLayers = new();
        private readonly Dictionary<GuildManageTab, Point> _contentOffsets = new();
        private readonly Texture2D[] _enabledTabs;
        private readonly Texture2D[] _disabledTabs;
        private readonly Texture2D _pixel;
        private readonly UIObject _pagePrevButton;
        private readonly UIObject _pageNextButton;
        private readonly UIObject _editButton;
        private readonly UIObject _saveButton;
        private readonly UIObject _admitYesButton;
        private readonly UIObject _admitNoButton;
        private readonly UIObject _changeButton;
        private readonly List<Rectangle> _rankBounds = new();
        private readonly StringBuilder _inputBuffer = new();

        private Func<GuildManageSnapshot> _snapshotProvider;
        private Action<GuildManageTab> _tabHandler;
        private Action<int> _rankSelectionHandler;
        private Action<int> _pageMoveHandler;
        private Func<bool, string> _admissionHandler;
        private Func<string> _beginEditHandler;
        private Func<string> _saveHandler;
        private Func<string> _cancelEditHandler;
        private Action<string> _draftChangeHandler;
        private Action<string> _feedbackHandler;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private Keys _lastHeldKey = Keys.None;
        private int _keyHoldStartTime;
        private int _lastKeyRepeatTime;

        public GuildManageWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            Texture2D[] enabledTabs,
            Texture2D[] disabledTabs,
            UIObject pagePrevButton,
            UIObject pageNextButton,
            UIObject editButton,
            UIObject saveButton,
            UIObject admitYesButton,
            UIObject admitNoButton,
            UIObject changeButton,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _enabledTabs = enabledTabs ?? Array.Empty<Texture2D>();
            _disabledTabs = disabledTabs ?? Array.Empty<Texture2D>();
            _pagePrevButton = pagePrevButton;
            _pageNextButton = pageNextButton;
            _editButton = editButton;
            _saveButton = saveButton;
            _admitYesButton = admitYesButton;
            _admitNoButton = admitNoButton;
            _changeButton = changeButton;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            WireButton(_pagePrevButton, () => _pageMoveHandler?.Invoke(-1));
            WireButton(_pageNextButton, () => _pageMoveHandler?.Invoke(1));
            WireButton(_editButton, () => ShowFeedback(_beginEditHandler?.Invoke()));
            WireButton(_saveButton, () => ShowFeedback(_saveHandler?.Invoke()));
            WireButton(_admitYesButton, () => ShowFeedback(_admissionHandler?.Invoke(true)));
            WireButton(_admitNoButton, () => ShowFeedback(_admissionHandler?.Invoke(false)));
            WireButton(_changeButton, () => ShowFeedback(_saveHandler?.Invoke()));
        }

        public override string WindowName => MapSimulatorWindowNames.GuildManage;

        public override bool CapturesKeyboardInput => GetSnapshot().IsEditing;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<GuildManageSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            SyncInputBuffer(GetSnapshot());
            UpdateButtonStates(GetSnapshot());
        }

        internal void SetHandlers(
            Action<GuildManageTab> tabHandler,
            Action<int> rankSelectionHandler,
            Action<int> pageMoveHandler,
            Func<bool, string> admissionHandler,
            Func<string> beginEditHandler,
            Func<string> saveHandler,
            Func<string> cancelEditHandler,
            Action<string> draftChangeHandler,
            Action<string> feedbackHandler)
        {
            _tabHandler = tabHandler;
            _rankSelectionHandler = rankSelectionHandler;
            _pageMoveHandler = pageMoveHandler;
            _admissionHandler = admissionHandler;
            _beginEditHandler = beginEditHandler;
            _saveHandler = saveHandler;
            _cancelEditHandler = cancelEditHandler;
            _draftChangeHandler = draftChangeHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void RegisterTabLayer(GuildManageTab tab, IDXObject baseLayer, Point baseOffset, IDXObject contentLayer, Point contentOffset)
        {
            if (baseLayer != null)
            {
                _baseLayers[tab] = baseLayer;
                _baseOffsets[tab] = baseOffset;
            }

            if (contentLayer != null)
            {
                _contentLayers[tab] = contentLayer;
                _contentOffsets[tab] = contentOffset;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GuildManageSnapshot snapshot = GetSnapshot();
            SyncInputBuffer(snapshot);
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                if (!TryHandleTabClick(mouseState.Position))
                {
                    TryHandleRankClick(mouseState.Position, snapshot);
                }
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

            GuildManageSnapshot snapshot = GetSnapshot();
            DrawTabStrip(sprite, snapshot);

            if (_baseLayers.TryGetValue(snapshot.CurrentTab, out IDXObject baseLayer))
            {
                DrawLayer(sprite, baseLayer, _baseOffsets[snapshot.CurrentTab], drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }

            if (_contentLayers.TryGetValue(snapshot.CurrentTab, out IDXObject contentLayer))
            {
                DrawLayer(sprite, contentLayer, _contentOffsets[snapshot.CurrentTab], drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }

            if (_font == null)
            {
                return;
            }

            switch (snapshot.CurrentTab)
            {
                case GuildManageTab.Position:
                    DrawRankTitles(sprite, snapshot);
                    break;
                case GuildManageTab.Admission:
                    DrawAdmissionPanel(sprite, snapshot);
                    break;
                case GuildManageTab.Change:
                    DrawChangePanel(sprite, snapshot, TickCount);
                    break;
            }

            DrawSummary(sprite, snapshot);
        }

        private void WireButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private GuildManageSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new GuildManageSnapshot();
        }

        private void UpdateButtonStates(GuildManageSnapshot snapshot)
        {
            if (_pagePrevButton != null)
            {
                _pagePrevButton.ButtonVisible = snapshot.CurrentTab == GuildManageTab.Position;
                _pagePrevButton.SetEnabled(snapshot.CanPageBackward);
            }

            if (_pageNextButton != null)
            {
                _pageNextButton.ButtonVisible = snapshot.CurrentTab == GuildManageTab.Position;
                _pageNextButton.SetEnabled(snapshot.CanPageForward);
            }

            if (_editButton != null)
            {
                _editButton.ButtonVisible = snapshot.CurrentTab != GuildManageTab.Admission;
                _editButton.SetEnabled(snapshot.CanEdit && !snapshot.IsEditing);
            }

            if (_saveButton != null)
            {
                _saveButton.ButtonVisible = snapshot.CurrentTab == GuildManageTab.Position;
                _saveButton.SetEnabled(snapshot.CanEdit && snapshot.IsEditing);
            }

            if (_admitYesButton != null)
            {
                _admitYesButton.ButtonVisible = snapshot.CurrentTab == GuildManageTab.Admission;
                _admitYesButton.SetEnabled(snapshot.CanEdit && snapshot.RequiresApproval);
            }

            if (_admitNoButton != null)
            {
                _admitNoButton.ButtonVisible = snapshot.CurrentTab == GuildManageTab.Admission;
                _admitNoButton.SetEnabled(snapshot.CanEdit && !snapshot.RequiresApproval);
            }

            if (_changeButton != null)
            {
                _changeButton.ButtonVisible = snapshot.CurrentTab == GuildManageTab.Change;
                _changeButton.SetEnabled(snapshot.CanEdit && snapshot.IsEditing);
            }
        }

        private void DrawTabStrip(SpriteBatch sprite, GuildManageSnapshot snapshot)
        {
            int x = Position.X + 14;
            for (int i = 0; i < TabOrder.Length; i++)
            {
                Texture2D texture = TabOrder[i] == snapshot.CurrentTab
                    ? (i < _enabledTabs.Length ? _enabledTabs[i] : null)
                    : (i < _disabledTabs.Length ? _disabledTabs[i] : null);
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(x, Position.Y + 28), Color.White);
                x += texture.Width + 1;
            }
        }

        private bool TryHandleTabClick(Point mousePosition)
        {
            int x = Position.X + 14;
            for (int i = 0; i < TabOrder.Length; i++)
            {
                Texture2D texture = (i < _enabledTabs.Length ? _enabledTabs[i] : null) ?? (i < _disabledTabs.Length ? _disabledTabs[i] : null);
                Rectangle bounds = new Rectangle(x, Position.Y + 28, texture?.Width ?? 60, texture?.Height ?? 20);
                if (bounds.Contains(mousePosition))
                {
                    _tabHandler?.Invoke(TabOrder[i]);
                    return true;
                }

                x += bounds.Width + 1;
            }

            return false;
        }

        private void DrawRankTitles(SpriteBatch sprite, GuildManageSnapshot snapshot)
        {
            _rankBounds.Clear();

            Rectangle listBounds = new Rectangle(Position.X + 20, Position.Y + 95, 224, 124);
            sprite.Draw(_pixel, listBounds, new Color(18, 25, 38, 44));
            for (int i = 0; i < snapshot.RankTitles.Count; i++)
            {
                Rectangle rowBounds = new Rectangle(listBounds.X + 6, listBounds.Y + 8 + (i * 22), listBounds.Width - 12, 20);
                _rankBounds.Add(rowBounds);
                bool selected = i == snapshot.SelectedRankIndex;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(111, 163, 219, 92) : new Color(255, 255, 255, 16));
                DrawText(sprite, $"Rank {i + 1}", rowBounds.X + 5, rowBounds.Y + 2, new Color(103, 112, 124), 0.35f);
                DrawText(sprite, snapshot.RankTitles[i], rowBounds.X + 66, rowBounds.Y + 2, new Color(242, 245, 249), 0.4f);
            }

            Rectangle editorBounds = new Rectangle(Position.X + 20, Position.Y + 232, 224, 46);
            DrawEditorBox(sprite, editorBounds, snapshot.IsEditing, snapshot.EditableText, Environment.TickCount);
            DrawText(sprite, "Selected rank title", editorBounds.X, editorBounds.Y - 15, new Color(104, 109, 119), 0.34f);
        }

        private void TryHandleRankClick(Point mousePosition, GuildManageSnapshot snapshot)
        {
            if (snapshot.CurrentTab != GuildManageTab.Position)
            {
                return;
            }

            for (int i = 0; i < _rankBounds.Count; i++)
            {
                if (_rankBounds[i].Contains(mousePosition))
                {
                    _rankSelectionHandler?.Invoke(i);
                    break;
                }
            }
        }

        private void DrawAdmissionPanel(SpriteBatch sprite, GuildManageSnapshot snapshot)
        {
            Rectangle infoBounds = new Rectangle(Position.X + 20, Position.Y + 95, 224, 92);
            sprite.Draw(_pixel, infoBounds, new Color(18, 25, 38, 44));
            DrawText(sprite, "Guild admission mode", infoBounds.X + 8, infoBounds.Y + 8, new Color(244, 247, 250), 0.42f);
            DrawText(
                sprite,
                snapshot.RequiresApproval ? "Applicants wait for approval." : "Applicants may join immediately.",
                infoBounds.X + 8,
                infoBounds.Y + 34,
                snapshot.RequiresApproval ? new Color(255, 225, 151) : new Color(154, 208, 169),
                0.37f);
            DrawText(sprite, "Use OK or NO to mirror the client toggle.", infoBounds.X + 8, infoBounds.Y + 58, new Color(167, 175, 186), 0.33f);
        }

        private void DrawChangePanel(SpriteBatch sprite, GuildManageSnapshot snapshot, int tickCount)
        {
            Rectangle noticeBounds = new Rectangle(Position.X + 20, Position.Y + 90, 224, 122);
            DrawEditorBox(sprite, noticeBounds, snapshot.IsEditing, snapshot.EditableText, tickCount);
            DrawText(sprite, "Guild notice", noticeBounds.X, noticeBounds.Y - 15, new Color(104, 109, 119), 0.34f);
            DrawText(sprite, "EDIT arms the notice draft. CHANGE saves it.", noticeBounds.X, noticeBounds.Bottom + 8, new Color(166, 173, 183), 0.33f);
        }

        private void DrawEditorBox(SpriteBatch sprite, Rectangle bounds, bool active, string text, int tickCount)
        {
            sprite.Draw(_pixel, bounds, active ? new Color(245, 248, 255, 46) : new Color(14, 21, 32, 64));
            string drawText = active ? _inputBuffer.ToString() : text;
            DrawWrappedText(sprite, drawText, bounds, new Color(232, 236, 242), 0.38f, 12);

            if (active && ((tickCount / 500) % 2 == 0))
            {
                int cursorX = bounds.X + 8 + (int)Math.Round(_font.MeasureString(drawText).X * 0.38f);
                sprite.Draw(_pixel, new Rectangle(Math.Min(bounds.Right - 3, cursorX), bounds.Bottom - 16, 1, 12), new Color(238, 242, 248));
            }
        }

        private void DrawSummary(SpriteBatch sprite, GuildManageSnapshot snapshot)
        {
            Rectangle summaryBounds = new Rectangle(Position.X + 16, Position.Y + 304, 232, 48);
            sprite.Draw(_pixel, summaryBounds, new Color(5, 10, 18, 88));

            int y = summaryBounds.Y + 5;
            foreach (string line in snapshot.SummaryLines.Take(3))
            {
                DrawText(sprite, line, summaryBounds.X + 6, y, new Color(228, 232, 239), 0.33f);
                y += 13;
            }
        }

        private void HandleKeyboardInput(KeyboardState keyboardState, GuildManageSnapshot snapshot, int tickCount)
        {
            if (!snapshot.IsEditing)
            {
                _previousKeyboardState = keyboardState;
                _lastHeldKey = Keys.None;
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                ShowFeedback(_cancelEditHandler?.Invoke());
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

        private void SyncInputBuffer(GuildManageSnapshot snapshot)
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
