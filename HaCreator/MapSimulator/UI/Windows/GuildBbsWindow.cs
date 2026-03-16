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

namespace HaCreator.MapSimulator.UI
{
    internal sealed class GuildBbsWindow : UIWindowBase
    {
        private const int ThreadListLeft = 395;
        private const int ThreadListTop = 55;
        private const int ThreadListWidth = 314;
        private const int ThreadRowHeight = 31;
        private const int MaxVisibleThreads = 8;
        private const int DetailLeft = 20;
        private const int DetailTop = 62;
        private const int DetailWidth = 352;
        private const int DetailHeight = 430;

        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly IDXObject _contentOverlay;
        private readonly Point _contentOverlayOffset;
        private readonly Texture2D _pixel;
        private readonly List<RowLayout> _rowLayouts = new();

        private SpriteFont _font;
        private MouseState _previousMouseState;
        private Func<GuildBbsSnapshot> _snapshotProvider;
        private Action<int> _selectThreadHandler;
        private Func<string> _writeHandler;
        private Func<string> _editHandler;
        private Func<string> _deleteHandler;
        private Func<string> _submitHandler;
        private Func<string> _cancelHandler;
        private Func<string> _toggleNoticeHandler;
        private Func<string> _replyHandler;
        private Func<string> _replyDeleteHandler;
        private Action<string> _feedbackHandler;
        private UIObject _registerButton;
        private UIObject _cancelButton;
        private UIObject _noticeButton;
        private UIObject _writeButton;
        private UIObject _retouchButton;
        private UIObject _deleteButton;
        private UIObject _quitButton;
        private UIObject _replyButton;
        private UIObject _replyDeleteButton;

        private sealed class RowLayout
        {
            public int ThreadId { get; init; }
            public Rectangle Bounds { get; init; }
        }

        public GuildBbsWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _contentOverlay = contentOverlay;
            _contentOverlayOffset = contentOverlayOffset;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.GuildBbs;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<GuildBbsSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        internal void SetActionHandlers(
            Action<int> selectThreadHandler,
            Func<string> writeHandler,
            Func<string> editHandler,
            Func<string> deleteHandler,
            Func<string> submitHandler,
            Func<string> cancelHandler,
            Func<string> toggleNoticeHandler,
            Func<string> replyHandler,
            Func<string> replyDeleteHandler,
            Action<string> feedbackHandler)
        {
            _selectThreadHandler = selectThreadHandler;
            _writeHandler = writeHandler;
            _editHandler = editHandler;
            _deleteHandler = deleteHandler;
            _submitHandler = submitHandler;
            _cancelHandler = cancelHandler;
            _toggleNoticeHandler = toggleNoticeHandler;
            _replyHandler = replyHandler;
            _replyDeleteHandler = replyDeleteHandler;
            _feedbackHandler = feedbackHandler;
        }

        internal void InitializeButtons(
            UIObject registerButton,
            UIObject cancelButton,
            UIObject noticeButton,
            UIObject writeButton,
            UIObject retouchButton,
            UIObject deleteButton,
            UIObject quitButton,
            UIObject replyButton,
            UIObject replyDeleteButton)
        {
            _registerButton = registerButton;
            _cancelButton = cancelButton;
            _noticeButton = noticeButton;
            _writeButton = writeButton;
            _retouchButton = retouchButton;
            _deleteButton = deleteButton;
            _quitButton = quitButton;
            _replyButton = replyButton;
            _replyDeleteButton = replyDeleteButton;

            ConfigureButton(_registerButton, () => ShowFeedback(_submitHandler?.Invoke()));
            ConfigureButton(_cancelButton, () => ShowFeedback(_cancelHandler?.Invoke()));
            ConfigureButton(_noticeButton, () => ShowFeedback(_toggleNoticeHandler?.Invoke()));
            ConfigureButton(_writeButton, () => ShowFeedback(_writeHandler?.Invoke()));
            ConfigureButton(_retouchButton, () => ShowFeedback(_editHandler?.Invoke()));
            ConfigureButton(_deleteButton, () => ShowFeedback(_deleteHandler?.Invoke()));
            ConfigureButton(_quitButton, Hide);
            ConfigureButton(_replyButton, () => ShowFeedback(_replyHandler?.Invoke()));
            ConfigureButton(_replyDeleteButton, () => ShowFeedback(_replyDeleteHandler?.Invoke()));

            UpdateButtonStates(GetSnapshot());
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GuildBbsSnapshot snapshot = GetSnapshot();
            UpdateButtonStates(snapshot);

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                HandleThreadSelection(mouseState.Position);
            }

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
            DrawLayer(sprite, _contentOverlay, _contentOverlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            GuildBbsSnapshot snapshot = GetSnapshot();

            sprite.DrawString(_font, "Guild BBS", new Vector2(Position.X + 54, Position.Y + 7), Color.White, 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, snapshot.GuildName, new Vector2(Position.X + 58, Position.Y + 22), new Color(59, 58, 54), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
            DrawGuildMark(sprite, snapshot.GuildName);
            DrawThreadList(sprite, snapshot);

            if (snapshot.IsWriteMode)
            {
                DrawComposePane(sprite, snapshot.Compose);
            }
            else
            {
                DrawDetailPane(sprite, snapshot.SelectedThread);
            }
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            if (action != null)
            {
                button.ButtonClickReleased += _ => action();
            }
        }

        private void HandleThreadSelection(Point mousePosition)
        {
            foreach (RowLayout row in _rowLayouts)
            {
                if (!row.Bounds.Contains(mousePosition))
                {
                    continue;
                }

                _selectThreadHandler?.Invoke(row.ThreadId);
                break;
            }
        }

        private void UpdateButtonStates(GuildBbsSnapshot snapshot)
        {
            bool hasSelectedThread = snapshot.SelectedThread != null;
            bool writeMode = snapshot.IsWriteMode;

            _registerButton?.SetVisible(writeMode);
            _cancelButton?.SetVisible(writeMode);
            _writeButton?.SetVisible(!writeMode);
            _retouchButton?.SetVisible(!writeMode);
            _deleteButton?.SetVisible(!writeMode);
            _replyButton?.SetVisible(!writeMode);
            _replyDeleteButton?.SetVisible(!writeMode);
            _noticeButton?.SetVisible(true);
            _quitButton?.SetVisible(true);

            if (_registerButton != null)
            {
                _registerButton.SetButtonState(writeMode ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_cancelButton != null)
            {
                _cancelButton.SetButtonState(writeMode ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_writeButton != null)
            {
                _writeButton.SetButtonState(!writeMode ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_retouchButton != null)
            {
                _retouchButton.SetButtonState(!writeMode && hasSelectedThread ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_deleteButton != null)
            {
                _deleteButton.SetButtonState(!writeMode && hasSelectedThread ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_replyButton != null)
            {
                _replyButton.SetButtonState(!writeMode && hasSelectedThread ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_replyDeleteButton != null)
            {
                _replyDeleteButton.SetButtonState(!writeMode && hasSelectedThread ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_noticeButton != null)
            {
                _noticeButton.SetButtonState(writeMode || hasSelectedThread ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        private GuildBbsSnapshot GetSnapshot()
        {
            return _snapshotProvider?.Invoke() ?? new GuildBbsSnapshot();
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            layer?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawGuildMark(SpriteBatch sprite, string guildName)
        {
            Rectangle markBounds = new Rectangle(Position.X + 20, Position.Y + 24, 20, 20);
            sprite.Draw(_pixel, markBounds, ResolveGuildMarkColor(guildName));
            sprite.Draw(_pixel, new Rectangle(markBounds.X + 3, markBounds.Y + 3, 14, 14), new Color(255, 255, 255, 180));
        }

        private void DrawThreadList(SpriteBatch sprite, GuildBbsSnapshot snapshot)
        {
            _rowLayouts.Clear();

            Rectangle bounds = new Rectangle(Position.X + ThreadListLeft, Position.Y + ThreadListTop, ThreadListWidth, ThreadRowHeight * MaxVisibleThreads);
            sprite.Draw(_pixel, bounds, new Color(8, 10, 14, 35));

            IReadOnlyList<GuildBbsThreadEntrySnapshot> threads = snapshot.Threads ?? Array.Empty<GuildBbsThreadEntrySnapshot>();
            int visibleCount = Math.Min(MaxVisibleThreads, threads.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                GuildBbsThreadEntrySnapshot thread = threads[i];
                Rectangle rowBounds = new Rectangle(bounds.X, bounds.Y + (i * ThreadRowHeight), bounds.Width, ThreadRowHeight - 1);
                _rowLayouts.Add(new RowLayout
                {
                    ThreadId = thread.ThreadId,
                    Bounds = rowBounds
                });

                Color background = thread.ThreadId == snapshot.SelectedThreadId
                    ? new Color(93, 117, 154, 120)
                    : new Color(255, 255, 255, i % 2 == 0 ? 25 : 12);
                sprite.Draw(_pixel, rowBounds, background);

                int titleX = rowBounds.X + 6;
                if (thread.IsNotice)
                {
                    Rectangle noticeBounds = new Rectangle(titleX, rowBounds.Y + 8, 18, 14);
                    sprite.Draw(_pixel, noticeBounds, new Color(218, 143, 57, 210));
                    DrawString(sprite, "N", noticeBounds.X + 5, noticeBounds.Y - 1, new Color(46, 28, 7), 0.34f);
                    titleX += 24;
                }

                DrawString(sprite, Truncate(thread.Title, 20), titleX, rowBounds.Y + 4, new Color(61, 53, 44), 0.41f);
                DrawString(sprite, Truncate(thread.Author, 10), rowBounds.X + 81, rowBounds.Y + 16, new Color(90, 92, 99), 0.34f);
                DrawString(sprite, thread.DateText, rowBounds.X + 258, rowBounds.Y + 16, new Color(102, 104, 111), 0.32f);

                if (thread.CommentCount > 0)
                {
                    DrawString(sprite, $"{thread.CommentCount}", rowBounds.X + 278, rowBounds.Y + 4, new Color(74, 106, 146), 0.34f);
                }
            }

            if (threads.Count == 0)
            {
                DrawString(sprite, "No guild board threads are available.", bounds.X + 8, bounds.Y + 8, new Color(74, 77, 82), 0.42f);
            }
        }

        private void DrawComposePane(SpriteBatch sprite, GuildBbsComposeSnapshot compose)
        {
            Rectangle detailBounds = new Rectangle(Position.X + DetailLeft, Position.Y + DetailTop, DetailWidth, DetailHeight);
            DrawString(sprite, compose.ModeText, detailBounds.X, detailBounds.Y - 22, new Color(83, 86, 92), 0.42f);
            DrawString(sprite, compose.IsNotice ? "[Notice]" : "[Thread]", detailBounds.X + 232, detailBounds.Y - 22, compose.IsNotice ? new Color(212, 143, 61) : new Color(85, 94, 110), 0.42f);

            DrawString(sprite, compose.Title, detailBounds.X, detailBounds.Y, new Color(58, 49, 39), 0.52f);
            DrawString(sprite, "Register saves the current draft using simulator-owned text.", detailBounds.X, detailBounds.Y + 22, new Color(104, 111, 121), 0.38f);
            sprite.Draw(_pixel, new Rectangle(detailBounds.X, detailBounds.Y + 40, detailBounds.Width, 1), new Color(204, 208, 216, 200));

            float y = detailBounds.Y + 50;
            foreach (string line in WrapText(compose.Body, detailBounds.Width - 8, 0.43f))
            {
                if (y > detailBounds.Bottom - 34)
                {
                    break;
                }

                sprite.DrawString(_font, line, new Vector2(detailBounds.X + 2, y), new Color(78, 82, 91), 0f, Vector2.Zero, 0.43f, SpriteEffects.None, 0f);
                y += 15f;
            }

            DrawString(sprite, "NOTICE toggles sticky guild-board placement. CANCEL returns to read mode.", detailBounds.X, detailBounds.Bottom - 20, new Color(111, 117, 127), 0.37f);
        }

        private void DrawDetailPane(SpriteBatch sprite, GuildBbsThreadSnapshot thread)
        {
            Rectangle detailBounds = new Rectangle(Position.X + DetailLeft, Position.Y + DetailTop, DetailWidth, DetailHeight);
            if (thread == null)
            {
                DrawString(sprite, "Select a guild thread to inspect its detail surface.", detailBounds.X, detailBounds.Y, new Color(93, 96, 103), 0.44f);
                return;
            }

            DrawString(sprite, thread.IsNotice ? "[Notice]" : "[Thread]", detailBounds.X, detailBounds.Y - 20, thread.IsNotice ? new Color(211, 142, 63) : new Color(84, 91, 108), 0.41f);
            DrawString(sprite, thread.Title, detailBounds.X, detailBounds.Y, new Color(58, 49, 39), 0.52f);
            DrawString(sprite, $"By {thread.Author}", detailBounds.X, detailBounds.Y + 20, new Color(101, 106, 115), 0.39f);
            DrawString(sprite, thread.DateText, detailBounds.X + 258, detailBounds.Y + 20, new Color(101, 106, 115), 0.36f);
            sprite.Draw(_pixel, new Rectangle(detailBounds.X, detailBounds.Y + 38, detailBounds.Width, 1), new Color(204, 208, 216, 200));

            float y = detailBounds.Y + 48;
            foreach (string line in WrapText(thread.Body, detailBounds.Width - 8, 0.43f))
            {
                if (y > detailBounds.Y + 154)
                {
                    break;
                }

                sprite.DrawString(_font, line, new Vector2(detailBounds.X + 2, y), new Color(78, 82, 91), 0f, Vector2.Zero, 0.43f, SpriteEffects.None, 0f);
                y += 15f;
            }

            DrawString(sprite, $"Replies ({thread.Comments.Count})", detailBounds.X, detailBounds.Y + 182, new Color(69, 74, 83), 0.42f);
            int drawY = detailBounds.Y + 206;
            foreach (GuildBbsCommentSnapshot comment in thread.Comments.Take(6))
            {
                Rectangle commentBounds = new Rectangle(detailBounds.X, drawY, detailBounds.Width, 34);
                sprite.Draw(_pixel, commentBounds, new Color(255, 255, 255, 28));
                DrawString(sprite, Truncate(comment.Author, 14), commentBounds.X + 4, commentBounds.Y + 3, new Color(61, 70, 87), 0.38f);
                DrawString(sprite, comment.DateText, commentBounds.X + 270, commentBounds.Y + 3, new Color(111, 116, 126), 0.31f);
                DrawString(sprite, Truncate(comment.Body, 44), commentBounds.X + 4, commentBounds.Y + 16, new Color(87, 91, 99), 0.36f);
                drawY += 38;
                if (drawY > detailBounds.Bottom - 28)
                {
                    break;
                }
            }

            DrawString(sprite, "WRITE opens a simulator draft. REPLY appends a location-tagged comment.", detailBounds.X, detailBounds.Bottom - 18, new Color(111, 117, 127), 0.37f);
        }

        private void DrawString(SpriteBatch sprite, string text, float x, float y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            foreach (string rawLine in lines)
            {
                string[] words = rawLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    yield return string.Empty;
                    continue;
                }

                string currentLine = string.Empty;
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (!string.IsNullOrEmpty(currentLine) && (_font.MeasureString(candidate).X * scale) > maxWidth)
                    {
                        yield return currentLine;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                }
            }
        }

        private static string Truncate(string text, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length <= maxCharacters
                ? text
                : text.Substring(0, Math.Max(0, maxCharacters - 3)) + "...";
        }

        private static Color ResolveGuildMarkColor(string guildName)
        {
            int seed = string.IsNullOrWhiteSpace(guildName) ? 0 : guildName.Aggregate(17, (current, ch) => (current * 31) + ch);
            byte red = (byte)(70 + Math.Abs(seed % 120));
            byte green = (byte)(70 + Math.Abs((seed / 7) % 120));
            byte blue = (byte)(90 + Math.Abs((seed / 13) % 120));
            return new Color(red, green, blue);
        }
    }
}
