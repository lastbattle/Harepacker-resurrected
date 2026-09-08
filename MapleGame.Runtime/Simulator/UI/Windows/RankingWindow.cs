using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class RankingWindow : UIWindowBase
    {
        internal const int EntriesPerPage = 4;
        private static readonly PlayerIndex[] NavigationGamePadIndices =
        {
            PlayerIndex.One,
            PlayerIndex.Two,
            PlayerIndex.Three,
            PlayerIndex.Four,
        };

        private readonly struct PageLayer
        {
            public PageLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        internal readonly struct LoadingFrame
        {
            public LoadingFrame(Texture2D texture, int delayMs)
            {
                Texture = texture;
                DelayMs = Math.Max(1, delayMs);
            }

            public Texture2D Texture { get; }
            public int DelayMs { get; }
        }

        private readonly List<PageLayer> _layers = new();
        private readonly Texture2D _highlightTexture;
        private readonly string _windowName;
        private IReadOnlyList<LoadingFrame> _loadingFrames = Array.Empty<LoadingFrame>();
        private Point _loadingLayerOffset;
        private Func<RankingWindowSnapshot> _snapshotProvider;
        private RankingWindowSnapshot _currentSnapshot = new();
        private readonly List<string> _wrappedTextBuffer = new();
        private KeyboardState _previousKeyboardState;
        private readonly GamePadState[] _previousNavigationGamePadStates = new GamePadState[NavigationGamePadIndices.Length];
        private int _selectedEntryIndex;
        private int _pageIndex;

        public RankingWindow(IDXObject frame, string windowName, Texture2D highlightTexture)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _highlightTexture = highlightTexture ?? throw new ArgumentNullException(nameof(highlightTexture));
        }

        public override string WindowName => _windowName;

        public override void Show()
        {
            _selectedEntryIndex = 0;
            _pageIndex = 0;
            _previousKeyboardState = Keyboard.GetState();
            CaptureNavigationGamePadStates(_previousNavigationGamePadStates);
            base.Show();
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new PageLayer(layer, offset));
            }
        }

        public void SetSnapshotProvider(Func<RankingWindowSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _currentSnapshot = RefreshSnapshot();
        }

        public void SetLoadingFrames(IReadOnlyList<LoadingFrame> loadingFrames, Point offset)
        {
            _loadingFrames = loadingFrames ?? Array.Empty<LoadingFrame>();
            _loadingLayerOffset = offset;
        }

        public override void SetFont(SpriteFont font)
        {
            base.SetFont(font);
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

            RankingWindowSnapshot snapshot = RefreshSnapshot();
            SyncEntrySelection(snapshot.Entries.Count);
            int pageEntryCount = GetVisibleEntryCount(snapshot.Entries.Count, _pageIndex);
            for (int i = 0; i < pageEntryCount; i++)
            {
                if (!GetEntryBounds(i).Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                _selectedEntryIndex = (_pageIndex * EntriesPerPage) + i;
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                return true;
            }

            return false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (!IsVisible)
            {
                return;
            }

            HandleOwnerInput();
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
            foreach (PageLayer layer in _layers)
            {
                layer.Layer.DrawBackground(
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    Position.X + layer.Offset.X,
                    Position.Y + layer.Offset.Y,
                    Color.White,
                    false,
                    drawReflectionInfo);
            }

            if (!CanDrawWindowText)
            {
                return;
            }

            RankingWindowSnapshot snapshot = RefreshSnapshot();
            SyncEntrySelection(snapshot.Entries.Count);
            DrawWindowText(sprite, snapshot.Title, new Vector2(Position.X + 18, Position.Y + 16), Color.White);
            float contentWidth = Math.Max(220f, (CurrentFrame?.Width ?? 303) - 36f);
            DrawWrappedText(sprite, snapshot.Subtitle, Position.X + 18, Position.Y + 38, contentWidth, new Color(220, 220, 220), maxLines: 2);

            Rectangle navigationBounds = GetNavigationBounds();
            DrawNavigationState(sprite, snapshot, navigationBounds);
            DrawLoadingLayer(sprite, snapshot, TickCount);

            if (snapshot.Entries.Count == 0)
            {
                DrawWrappedText(
                    sprite,
                    "Ranking owner art is loaded, but no simulator-side ranking data is currently available.",
                    Position.X + 18,
                    navigationBounds.Bottom + 8,
                    contentWidth,
                    new Color(224, 224, 224),
                    maxLines: 2);
            }
            else
            {
                int pageStart = _pageIndex * EntriesPerPage;
                int maxVisibleEntries = GetVisibleEntryCount(snapshot.Entries.Count, _pageIndex);
                for (int i = 0; i < maxVisibleEntries; i++)
                {
                    Rectangle bounds = GetEntryBounds(i);
                    int entryIndex = pageStart + i;
                    Color fillColor = entryIndex == _selectedEntryIndex
                        ? new Color(94, 123, 188, 196)
                        : new Color(34, 42, 60, 196);
                    sprite.Draw(_highlightTexture, bounds, fillColor);

                    RankingEntrySnapshot entry = snapshot.Entries[entryIndex];
                    DrawTrimmedText(sprite, entry.Label, bounds.X + 10, bounds.Y + 6, bounds.Width - 100f, new Color(255, 228, 151));

                    string valueText = TrimTextToWidth(entry.Value, 92f);
                    Vector2 valueSize = MeasureWindowText(sprite, valueText);
                    DrawWindowText(sprite, valueText, new Vector2(bounds.Right - valueSize.X - 10, bounds.Y + 6), Color.White);

                    DrawWrappedText(sprite, entry.Detail, bounds.X + 10, bounds.Y + 21, bounds.Width - 18f, new Color(215, 215, 215), maxLines: 1);
                }

                if (ResolvePageCount(snapshot.Entries.Count) > 1)
                {
                    int pageCount = ResolvePageCount(snapshot.Entries.Count);
                    string pageText = $"Page {_pageIndex + 1}/{pageCount}  Row {_selectedEntryIndex + 1}/{snapshot.Entries.Count}";
                    DrawWrappedText(
                        sprite,
                        pageText,
                        Position.X + 18,
                        GetEntryBounds(EntriesPerPage - 1).Bottom + 6,
                        contentWidth,
                        new Color(220, 220, 220),
                        maxLines: 1);
                }
            }

            if (!string.IsNullOrWhiteSpace(snapshot.StatusText))
            {
                DrawWrappedText(
                    sprite,
                    snapshot.StatusText,
                    Position.X + 18,
                    Position.Y + (int)Math.Max(0f, (CurrentFrame?.Height ?? 298) - (WindowLineSpacing * 2f) - 10f),
                    contentWidth,
                    new Color(255, 228, 151),
                    maxLines: 2);
            }
        }

        private void DrawNavigationState(SpriteBatch sprite, RankingWindowSnapshot snapshot, Rectangle bounds)
        {
            sprite.Draw(_highlightTexture, bounds, snapshot.IsLoading
                ? new Color(50, 72, 126, 214)
                : new Color(28, 34, 52, 214));

            DrawTrimmedText(sprite, snapshot.NavigationCaption, bounds.X + 10, bounds.Y + 5, bounds.Width - 80f, new Color(255, 228, 151));
            if (snapshot.IsLoading)
            {
                DrawTrimmedText(sprite, "Loading", bounds.Right - 60, bounds.Y + 5, 50f, Color.White);
            }

            DrawWrappedText(sprite, snapshot.NavigationSeedText, bounds.X + 10, bounds.Y + 18, bounds.Width - 20f, Color.White, maxLines: 1);
            DrawWrappedText(sprite, snapshot.NavigationHostText, bounds.X + 10, bounds.Y + 31, bounds.Width - 20f, new Color(215, 215, 215), maxLines: 1);
            DrawWrappedText(sprite, snapshot.NavigationRequestText, bounds.X + 10, bounds.Y + 44, bounds.Width - 20f, new Color(215, 215, 215), maxLines: 1);
            DrawWrappedText(sprite, snapshot.NavigationStateText, bounds.X + 10, bounds.Y + 57, bounds.Width - 20f, new Color(215, 215, 215), maxLines: 1);
        }

        private void DrawLoadingLayer(SpriteBatch sprite, RankingWindowSnapshot snapshot, int tickCount)
        {
            if (!snapshot.IsLoading || _loadingFrames.Count == 0)
            {
                return;
            }

            List<int> frameDelays = new(_loadingFrames.Count);
            for (int i = 0; i < _loadingFrames.Count; i++)
            {
                frameDelays.Add(_loadingFrames[i].DelayMs);
            }

            int frameIndex = ResolveLoadingFrameIndex(tickCount, snapshot.LoadingStartTick, frameDelays);
            if ((uint)frameIndex >= (uint)_loadingFrames.Count)
            {
                return;
            }

            Texture2D loadingFrame = _loadingFrames[frameIndex].Texture;
            if (loadingFrame == null)
            {
                return;
            }

            sprite.Draw(
                loadingFrame,
                new Vector2(Position.X + _loadingLayerOffset.X, Position.Y + _loadingLayerOffset.Y),
                Color.White);
        }

        internal static int ResolveLoadingFrameIndex(int tickCount, int loadingStartTick, IReadOnlyList<int> frameDelays)
        {
            if (frameDelays == null || frameDelays.Count == 0)
            {
                return 0;
            }

            int totalDelay = 0;
            for (int i = 0; i < frameDelays.Count; i++)
            {
                totalDelay += Math.Max(1, frameDelays[i]);
            }

            if (totalDelay <= 0)
            {
                return 0;
            }

            int animationOriginTick = loadingStartTick != int.MinValue ? loadingStartTick : 0;
            int animationTime = tickCount - animationOriginTick;
            if (animationTime < 0)
            {
                animationTime = 0;
            }

            animationTime %= totalDelay;
            for (int i = 0; i < frameDelays.Count; i++)
            {
                int frameDelay = Math.Max(1, frameDelays[i]);
                if (animationTime < frameDelay)
                {
                    return i;
                }

                animationTime -= frameDelay;
            }

            return frameDelays.Count - 1;
        }

        private Rectangle GetNavigationBounds()
        {
            int width = Math.Max(240, (CurrentFrame?.Width ?? 303) - 24);
            return new Rectangle(Position.X + 12, Position.Y + 74, width, 74);
        }

        private Rectangle GetEntryBounds(int index)
        {
            int width = Math.Max(240, (CurrentFrame?.Width ?? 303) - 24);
            return new Rectangle(Position.X + 12, Position.Y + 154 + (index * 34), width, 32);
        }

        internal static int ResolvePageCount(int entryCount)
        {
            if (entryCount <= 0)
            {
                return 0;
            }

            return ((entryCount - 1) / EntriesPerPage) + 1;
        }

        internal static int GetVisibleEntryCount(int entryCount, int pageIndex)
        {
            if (entryCount <= 0)
            {
                return 0;
            }

            int clampedPage = Math.Clamp(pageIndex, 0, Math.Max(0, ResolvePageCount(entryCount) - 1));
            int remaining = entryCount - (clampedPage * EntriesPerPage);
            return Math.Clamp(remaining, 0, EntriesPerPage);
        }

        internal static (int PageIndex, int SelectedEntryIndex) ResolveSelectionState(int entryCount, int currentPageIndex, int currentSelectedEntryIndex)
        {
            if (entryCount <= 0)
            {
                return (0, 0);
            }

            int selectedEntryIndex = Math.Clamp(currentSelectedEntryIndex, 0, entryCount - 1);
            int pageIndex = Math.Clamp(currentPageIndex, 0, Math.Max(0, ResolvePageCount(entryCount) - 1));
            int selectedPage = selectedEntryIndex / EntriesPerPage;
            if (selectedPage != pageIndex)
            {
                pageIndex = selectedPage;
            }

            return (pageIndex, selectedEntryIndex);
        }

        private void DrawTrimmedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            DrawWindowText(sprite, TrimTextToWidth(text, maxWidth), new Vector2(x, y), color);
        }

        private string TrimTextToWidth(string text, float maxWidth)
        {
            if (!CanDrawWindowText || string.IsNullOrEmpty(text) || MeasureWindowText(null, text).X <= maxWidth)
            {
                return text ?? string.Empty;
            }

            const string ellipsis = "...";
            string trimmed = text;
            while (trimmed.Length > 1 && MeasureWindowText(null, trimmed + ellipsis).X > maxWidth)
            {
                trimmed = trimmed[..^1];
            }

            return trimmed + ellipsis;
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, int x, int y, float maxWidth, Color color, int maxLines = int.MaxValue)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float drawY = y;
            foreach (string line in WrapText(text, maxWidth).Take(Math.Max(1, maxLines)))
            {
                DrawWindowText(sprite, line, new Vector2(x, drawY), color);
                drawY += WindowLineSpacing;
            }
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            _wrappedTextBuffer.Clear();
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;
            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate).X > maxWidth)
                {
                    _wrappedTextBuffer.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = candidate;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                _wrappedTextBuffer.Add(currentLine);
            }

            for (int i = 0; i < _wrappedTextBuffer.Count; i++)
            {
                yield return _wrappedTextBuffer[i];
            }
        }

        private RankingWindowSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new RankingWindowSnapshot();
            return _currentSnapshot;
        }

        private void HandleOwnerInput()
        {
            KeyboardState keyboard = Keyboard.GetState();
            GamePadState[] gamePads = CaptureNavigationGamePadStates();
            RankingWindowSnapshot snapshot = RefreshSnapshot();
            SyncEntrySelection(snapshot.Entries.Count);

            bool close = IsNewKeyPress(keyboard, Keys.Escape)
                || IsNewKeyPress(keyboard, Keys.Back)
                || IsNewButtonPress(gamePads, Buttons.B)
                || IsNewButtonPress(gamePads, Buttons.Back);
            bool moveUp = IsNewKeyPress(keyboard, Keys.Up)
                || IsNewButtonPress(gamePads, Buttons.DPadUp)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickUp);
            bool moveDown = IsNewKeyPress(keyboard, Keys.Down)
                || IsNewButtonPress(gamePads, Buttons.DPadDown)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickDown);
            bool pageLeft = IsNewKeyPress(keyboard, Keys.Left)
                || IsNewKeyPress(keyboard, Keys.PageUp)
                || IsNewButtonPress(gamePads, Buttons.DPadLeft)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickLeft)
                || IsNewButtonPress(gamePads, Buttons.LeftShoulder);
            bool pageRight = IsNewKeyPress(keyboard, Keys.Right)
                || IsNewKeyPress(keyboard, Keys.PageDown)
                || IsNewButtonPress(gamePads, Buttons.DPadRight)
                || IsNewButtonPress(gamePads, Buttons.LeftThumbstickRight)
                || IsNewButtonPress(gamePads, Buttons.RightShoulder);
            bool jumpFirst = IsNewKeyPress(keyboard, Keys.Home);
            bool jumpLast = IsNewKeyPress(keyboard, Keys.End);

            if (close)
            {
                Hide();
            }
            else if (jumpFirst)
            {
                _selectedEntryIndex = 0;
                _pageIndex = 0;
            }
            else if (jumpLast && snapshot.Entries.Count > 0)
            {
                _selectedEntryIndex = snapshot.Entries.Count - 1;
                _pageIndex = _selectedEntryIndex / EntriesPerPage;
            }
            else if (moveUp)
            {
                MoveSelection(-1, snapshot.Entries.Count);
            }
            else if (moveDown)
            {
                MoveSelection(1, snapshot.Entries.Count);
            }
            else if (pageLeft)
            {
                MovePage(-1, snapshot.Entries.Count);
            }
            else if (pageRight)
            {
                MovePage(1, snapshot.Entries.Count);
            }

            _previousKeyboardState = keyboard;
            CopyNavigationGamePadStates(gamePads, _previousNavigationGamePadStates);
        }

        private void MoveSelection(int direction, int entryCount)
        {
            if (entryCount <= 0)
            {
                return;
            }

            _selectedEntryIndex = (_selectedEntryIndex + direction) % entryCount;
            if (_selectedEntryIndex < 0)
            {
                _selectedEntryIndex += entryCount;
            }

            _pageIndex = _selectedEntryIndex / EntriesPerPage;
        }

        private void MovePage(int direction, int entryCount)
        {
            int pageCount = ResolvePageCount(entryCount);
            if (pageCount <= 1)
            {
                return;
            }

            _pageIndex += direction;
            if (_pageIndex < 0)
            {
                _pageIndex = pageCount - 1;
            }
            else if (_pageIndex >= pageCount)
            {
                _pageIndex = 0;
            }

            int pageStart = _pageIndex * EntriesPerPage;
            int pageEntryCount = GetVisibleEntryCount(entryCount, _pageIndex);
            int rowOffset = _selectedEntryIndex % EntriesPerPage;
            _selectedEntryIndex = pageStart + Math.Min(rowOffset, Math.Max(0, pageEntryCount - 1));
        }

        private void SyncEntrySelection(int entryCount)
        {
            (int pageIndex, int selectedEntryIndex) = ResolveSelectionState(entryCount, _pageIndex, _selectedEntryIndex);
            _pageIndex = pageIndex;
            _selectedEntryIndex = selectedEntryIndex;
        }

        private bool IsNewKeyPress(KeyboardState current, Keys key)
        {
            return current.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private static GamePadState[] CaptureNavigationGamePadStates()
        {
            GamePadState[] states = new GamePadState[NavigationGamePadIndices.Length];
            CaptureNavigationGamePadStates(states);
            return states;
        }

        private static void CaptureNavigationGamePadStates(GamePadState[] states)
        {
            if (states == null)
            {
                return;
            }

            for (int i = 0; i < states.Length && i < NavigationGamePadIndices.Length; i++)
            {
                states[i] = GamePad.GetState(NavigationGamePadIndices[i]);
            }
        }

        private static void CopyNavigationGamePadStates(GamePadState[] source, GamePadState[] destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            int count = Math.Min(source.Length, destination.Length);
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
        }

        private bool IsNewButtonPress(GamePadState[] currentStates, Buttons button)
        {
            if (currentStates == null)
            {
                return false;
            }

            int count = Math.Min(currentStates.Length, _previousNavigationGamePadStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (currentStates[i].IsButtonDown(button)
                    && !_previousNavigationGamePadStates[i].IsButtonDown(button))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
