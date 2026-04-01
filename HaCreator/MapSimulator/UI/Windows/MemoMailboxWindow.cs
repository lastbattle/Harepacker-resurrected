using HaCreator.MapSimulator.Interaction;
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
    internal sealed class MemoMailboxWindow : UIWindowBase
    {
        private const int ContentMarginLeft = 16;
        private const int ContentMarginTop = 33;
        private const int ContentMarginRight = 15;
        private const int ContentMarginBottom = 29;
        private const int RowHeight = 29;
        private const int MaxVisibleEntries = 6;

        private readonly string _windowName;
        private readonly Texture2D _unreadIconTexture;
        private readonly Texture2D _readIconTexture;
        private readonly Texture2D _pixel;
        private readonly List<RowLayout> _rowLayouts = new();

        private SpriteFont _font;
        private Func<MemoMailboxSnapshot> _snapshotProvider;
        private Action<int> _openMemoRequested;
        private Action<int> _keepMemoRequested;
        private Action<int> _deleteMemoRequested;
        private Action<int> _attachmentRequested;
        private MouseState _previousMouseState;
        private int _selectedMemoId = -1;
        private int _openedMemoId = -1;
        private UIObject _keepButton;
        private UIObject _deleteButton;
        private UIObject _actionButton;
        private MemoMailboxSnapshot _currentSnapshot = new();

        private sealed class RowLayout
        {
            public int MemoId { get; init; }
            public Rectangle Bounds { get; init; }
        }

        public MemoMailboxWindow(
            IDXObject frame,
            string windowName,
            GraphicsDevice device,
            Texture2D unreadIconTexture,
            Texture2D readIconTexture)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _unreadIconTexture = unreadIconTexture;
            _readIconTexture = readIconTexture;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => _windowName;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<MemoMailboxSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _currentSnapshot = RefreshSnapshot();
        }

        internal void SetActions(
            Action<int> openMemoRequested,
            Action<int> keepMemoRequested,
            Action<int> deleteMemoRequested,
            Action<int> attachmentRequested)
        {
            _openMemoRequested = openMemoRequested;
            _keepMemoRequested = keepMemoRequested;
            _deleteMemoRequested = deleteMemoRequested;
            _attachmentRequested = attachmentRequested;
        }

        internal void InitializeButtons(UIObject keepButton, UIObject deleteButton, UIObject actionButton)
        {
            _keepButton = keepButton;
            _deleteButton = deleteButton;
            _actionButton = actionButton;

            ConfigureButton(_keepButton, 16, 215, KeepOpenedMemo);
            ConfigureButton(_deleteButton, 60, 215, DeleteOpenedMemo);
            ConfigureButton(_actionButton, 214, 215, HandleActionButton);
            UpdateButtonVisibility();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            MemoMailboxSnapshot snapshot = RefreshSnapshot();
            EnsureSelection(snapshot);
            UpdateButtonVisibility();

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                HandleLeftClick(snapshot, mouseState.Position);
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
            if (_font == null)
            {
                return;
            }

            MemoMailboxSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            EnsureSelection(snapshot);

            Rectangle contentBounds = GetContentBounds();
            string title = _openedMemoId > 0
                ? "READ MEMO"
                : $"INBOX ({snapshot.UnreadCount} unread / {snapshot.ClaimableCount} package)";
            sprite.DrawString(_font, title, new Vector2(Position.X + 28, Position.Y + 10), Color.White, 0f, Vector2.Zero, 0.46f, SpriteEffects.None, 0f);

            if (_openedMemoId > 0)
            {
                DrawOpenedMemo(sprite, snapshot, contentBounds);
                return;
            }

            DrawInbox(sprite, snapshot, contentBounds);
        }

        private void ConfigureButton(UIObject button, int x, int y, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
        }

        private void DrawInbox(SpriteBatch sprite, MemoMailboxSnapshot snapshot, Rectangle contentBounds)
        {
            _rowLayouts.Clear();

            if (snapshot.Entries.Count == 0)
            {
                sprite.DrawString(_font, "No memos are waiting in the inbox.", new Vector2(contentBounds.X, contentBounds.Y + 6), new Color(53, 65, 79), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
                sprite.DrawString(_font, "Delivery is modeled separately from whisper and messenger surfaces.", new Vector2(contentBounds.X, contentBounds.Y + 24), new Color(104, 112, 125), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
                return;
            }

            int visibleCount = Math.Min(MaxVisibleEntries, snapshot.Entries.Count);
            for (int index = 0; index < visibleCount; index++)
            {
                MemoMailboxEntrySnapshot memo = snapshot.Entries[index];
                Rectangle rowBounds = new Rectangle(contentBounds.X, contentBounds.Y + (index * RowHeight), contentBounds.Width, RowHeight - 2);
                _rowLayouts.Add(new RowLayout
                {
                    MemoId = memo.MemoId,
                    Bounds = rowBounds
                });

                Color fillColor = memo.MemoId == _selectedMemoId
                    ? new Color(130, 185, 224, 120)
                    : new Color(255, 255, 255, index % 2 == 0 ? 72 : 48);
                sprite.Draw(_pixel, rowBounds, fillColor);

                Texture2D stateIcon = memo.IsRead ? _readIconTexture : _unreadIconTexture;
                if (stateIcon != null)
                {
                    sprite.Draw(stateIcon, new Vector2(rowBounds.X + 3, rowBounds.Y + 8), Color.White);
                }

                Color subjectColor = memo.IsRead ? new Color(58, 68, 81) : new Color(190, 77, 35);
                sprite.DrawString(_font, Truncate(memo.Subject, 23), new Vector2(rowBounds.X + 20, rowBounds.Y + 2), subjectColor, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
                sprite.DrawString(_font, Truncate(memo.Sender, 14), new Vector2(rowBounds.X + 20, rowBounds.Y + 14), new Color(91, 99, 113), 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);

                Vector2 timeSize = _font.MeasureString(memo.DeliveredAtText) * 0.34f;
                sprite.DrawString(_font, memo.DeliveredAtText, new Vector2(rowBounds.Right - timeSize.X - 2, rowBounds.Y + 3), new Color(123, 129, 141), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                sprite.DrawString(_font, Truncate(memo.Preview, 36), new Vector2(rowBounds.Right - 120, rowBounds.Y + 15), new Color(108, 115, 126), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                if (memo.HasAttachment)
                {
                    Color attachmentColor = memo.CanClaimAttachment ? new Color(67, 137, 76) : new Color(129, 136, 145);
                    sprite.DrawString(_font, "PKG", new Vector2(rowBounds.Right - 28, rowBounds.Y + 14), attachmentColor, 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                }
            }

            sprite.DrawString(_font, snapshot.LastActionSummary ?? "Click a memo entry to read it.", new Vector2(contentBounds.X, contentBounds.Bottom - 18), new Color(97, 105, 117), 0f, Vector2.Zero, 0.37f, SpriteEffects.None, 0f);

            if (snapshot.Entries.Count > visibleCount)
            {
                sprite.DrawString(_font, $"+{snapshot.Entries.Count - visibleCount} more memo(s) in backlog", new Vector2(contentBounds.Right - 116, contentBounds.Bottom - 18), new Color(97, 105, 117), 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
            }
        }

        private void DrawOpenedMemo(SpriteBatch sprite, MemoMailboxSnapshot snapshot, Rectangle contentBounds)
        {
            if (!TryGetEntry(snapshot, _openedMemoId, out MemoMailboxEntrySnapshot memo))
            {
                ReturnToInbox();
                return;
            }

            Color headingColor = new Color(56, 66, 80);
            Color bodyColor = new Color(75, 82, 93);
            Color accentColor = memo.IsKept ? new Color(80, 141, 82) : new Color(103, 110, 122);

            sprite.DrawString(_font, Truncate(memo.Subject, 30), new Vector2(contentBounds.X, contentBounds.Y), headingColor, 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, $"From {memo.Sender}", new Vector2(contentBounds.X, contentBounds.Y + 18), new Color(112, 119, 131), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, memo.DeliveredAtText, new Vector2(contentBounds.Right - 76, contentBounds.Y + 18), new Color(112, 119, 131), 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
            sprite.Draw(_pixel, new Rectangle(contentBounds.X, contentBounds.Y + 34, contentBounds.Width, 1), new Color(195, 205, 214));

            if (memo.HasAttachment)
            {
                Color attachmentColor = memo.CanClaimAttachment ? new Color(74, 134, 80) : new Color(123, 129, 141);
                sprite.DrawString(
                    _font,
                    memo.CanClaimAttachment
                        ? $"Package ready: {memo.AttachmentSummary}"
                        : $"Package claimed: {memo.AttachmentSummary}",
                    new Vector2(contentBounds.X, contentBounds.Y + 34),
                    attachmentColor,
                    0f,
                    Vector2.Zero,
                    0.39f,
                    SpriteEffects.None,
                    0f);
            }

            float drawY = contentBounds.Y + (memo.HasAttachment ? 52 : 42);
            foreach (string line in WrapText(memo.Body, contentBounds.Width - 4, 0.43f))
            {
                sprite.DrawString(_font, line, new Vector2(contentBounds.X + 2, drawY), bodyColor, 0f, Vector2.Zero, 0.43f, SpriteEffects.None, 0f);
                drawY += 14f;
                if (drawY > contentBounds.Bottom - 28)
                {
                    break;
                }
            }

            string footer = memo.IsKept
                ? "Kept in the mailbox backlog."
                : memo.CanClaimAttachment
                    ? "Use OPEN to inspect and claim the attached package."
                : "Use KEEP to preserve this memo in the inbox.";
            sprite.DrawString(_font, footer, new Vector2(contentBounds.X, contentBounds.Bottom - 18), accentColor, 0f, Vector2.Zero, 0.39f, SpriteEffects.None, 0f);
        }

        private void HandleLeftClick(MemoMailboxSnapshot snapshot, Point mousePosition)
        {
            if (_openedMemoId > 0)
            {
                return;
            }

            foreach (RowLayout rowLayout in _rowLayouts)
            {
                if (!rowLayout.Bounds.Contains(mousePosition))
                {
                    continue;
                }

                _selectedMemoId = rowLayout.MemoId;
                OpenSelectedMemo(snapshot);
                break;
            }
        }

        private void OpenSelectedMemo(MemoMailboxSnapshot snapshot)
        {
            if (_selectedMemoId <= 0 || !ContainsEntry(snapshot, _selectedMemoId))
            {
                return;
            }

            _openMemoRequested?.Invoke(_selectedMemoId);
            _openedMemoId = _selectedMemoId;
            UpdateButtonVisibility();
        }

        private void KeepOpenedMemo()
        {
            if (_openedMemoId <= 0)
            {
                return;
            }

            _keepMemoRequested?.Invoke(_openedMemoId);
        }

        private void DeleteOpenedMemo()
        {
            if (_openedMemoId <= 0)
            {
                return;
            }

            int deletedMemoId = _openedMemoId;
            _deleteMemoRequested?.Invoke(deletedMemoId);
            _openedMemoId = -1;

            MemoMailboxSnapshot snapshot = RefreshSnapshot();
            if (_selectedMemoId == deletedMemoId)
            {
                _selectedMemoId = GetFirstEntryId(snapshot);
            }

            UpdateButtonVisibility();
        }

        private void ReturnToInbox()
        {
            _openedMemoId = -1;
            UpdateButtonVisibility();
        }

        private void HandleActionButton()
        {
            if (_openedMemoId <= 0)
            {
                return;
            }

            MemoMailboxSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            if (!TryGetEntry(snapshot, _openedMemoId, out MemoMailboxEntrySnapshot memo))
            {
                ReturnToInbox();
                return;
            }

            if (memo?.CanClaimAttachment == true)
            {
                _attachmentRequested?.Invoke(_openedMemoId);
                return;
            }

            ReturnToInbox();
        }

        private void EnsureSelection(MemoMailboxSnapshot snapshot)
        {
            if (_selectedMemoId > 0 && ContainsEntry(snapshot, _selectedMemoId))
            {
                return;
            }

            _selectedMemoId = GetFirstEntryId(snapshot);
            if (_openedMemoId > 0 && !ContainsEntry(snapshot, _openedMemoId))
            {
                _openedMemoId = -1;
            }
        }

        private void UpdateButtonVisibility()
        {
            bool readerVisible = _openedMemoId > 0;
            if (_keepButton != null)
            {
                _keepButton.ButtonVisible = readerVisible;
            }

            if (_deleteButton != null)
            {
                _deleteButton.ButtonVisible = readerVisible;
            }

            if (_actionButton != null)
            {
                _actionButton.ButtonVisible = readerVisible;
            }
        }

        private MemoMailboxSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new MemoMailboxSnapshot();
            return _currentSnapshot;
        }

        private static bool TryGetEntry(MemoMailboxSnapshot snapshot, int memoId, out MemoMailboxEntrySnapshot entry)
        {
            foreach (MemoMailboxEntrySnapshot candidate in snapshot.Entries)
            {
                if (candidate.MemoId != memoId)
                {
                    continue;
                }

                entry = candidate;
                return true;
            }

            entry = null;
            return false;
        }

        private static bool ContainsEntry(MemoMailboxSnapshot snapshot, int memoId)
        {
            return TryGetEntry(snapshot, memoId, out _);
        }

        private static int GetFirstEntryId(MemoMailboxSnapshot snapshot)
        {
            foreach (MemoMailboxEntrySnapshot entry in snapshot.Entries)
            {
                return entry.MemoId;
            }

            return -1;
        }

        private Rectangle GetContentBounds()
        {
            int width = (CurrentFrame?.Width ?? 295) - ContentMarginLeft - ContentMarginRight;
            int height = (CurrentFrame?.Height ?? 240) - ContentMarginTop - ContentMarginBottom;
            return new Rectangle(Position.X + ContentMarginLeft, Position.Y + ContentMarginTop, Math.Max(0, width), Math.Max(0, height));
        }

        private string Truncate(string text, int maxCharacters)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length <= maxCharacters
                ? text
                : text.Substring(0, Math.Max(0, maxCharacters - 3)) + "...";
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
    }
}
