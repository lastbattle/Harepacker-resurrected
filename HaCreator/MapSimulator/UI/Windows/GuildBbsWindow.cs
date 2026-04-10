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
    internal sealed class GuildBbsWindow : UIWindowBase
    {
        private const int NoticeLeft = 22;
        private const int NoticeTop = 103;
        private const int NoticeWidth = 344;
        private const int NoticeHeight = 31;
        private const int ThreadPageSelectorLeft = 450;
        private const int ThreadPageSelectorTop = 320;
        private const int ThreadPageSelectorWidth = 40;
        private const int ThreadPageSelectorHeight = 14;
        private const int ThreadPageSelectorVisibleCount = 4;
        private const int ThreadListLeft = 22;
        private const int ThreadListTop = 134;
        private const int ThreadListWidth = 344;
        private const int ThreadRowHeight = 31;
        private const int DetailLeft = 395;
        private const int DetailTop = 30;
        private const int DetailWidth = 314;
        private const int DetailHeight = 430;
        private const int DetailBodyVisibleLineCount = 15;
        private const int DetailBodyLineHeight = 12;
        private const int DetailBodyClientMaxLineWidth = 240;
        private const int TitleInputHeight = 16;
        private const int ReplyInputHeight = 22;
        private const int BasicEmoticonSize = 18;
        private const int CashEmoticonSize = 18;
        private const int EmoticonSelectionSize = 22;
        private const int CashEmoticonSpacing = 23;
        private const int BasicEmoticonSpacing = 23;
        private const int VisibleCashEmoticonCount = 7;
        private const int ComposeBodyLineHeight = 12;
        private const int ComposeBodyVisibleLineCount = 15;
        private const int ComposeBodyClientMaxLineWidth = 240;
        private const int ComposeScrollBarWidth = 12;
        private const int CommentScrollBarWidth = 10;
        private const int PromptBoxWidth = 250;
        private const int PromptBoxHeight = 98;
        private const int PromptButtonWidth = 64;
        private const int PromptButtonHeight = 22;
        private const int KeyRepeatDelayMs = 400;
        private const int KeyRepeatRateMs = 35;

        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly IDXObject _contentOverlay;
        private readonly Point _contentOverlayOffset;
        private readonly Texture2D _pixel;
        private readonly Texture2D _emoticonSelectTexture;
        private readonly Texture2D[] _basicEmoticonTextures;
        private readonly Texture2D[] _cashEmoticonTextures;
        private readonly VerticalScrollbarSkin _scrollbarSkin;
        private readonly List<RowLayout> _rowLayouts = new();

        private SpriteFont _font;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private int _previousScrollValue;
        private int _keyHoldStartTime;
        private int _lastKeyRepeatTime;
        private Keys _lastHeldKey = Keys.None;
        private readonly StringBuilder _inputBuffer = new();
        private int _cursorPosition;
        private int _cursorBlinkTimer;
        private int _composeBodyScrollLine;
        private int _detailBodyScrollLine;
        private bool _isDraggingComposeScrollBar;
        private int _composeScrollDragOffsetY;
        private bool _isDraggingDetailBodyScrollBar;
        private int _detailBodyScrollDragOffsetY;
        private bool _isDraggingCommentScrollBar;
        private int _commentScrollDragOffsetY;
        private int _lastDetailThreadId = -1;
        private PendingPrompt _pendingPrompt;
        private int _pendingPromptVisibleReplyIndex = -1;
        private bool _ignorePromptMouseRelease;

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
        private Func<int, string> _deleteReplyAtVisibleIndexHandler;
        private Action<string> _setComposeTitleHandler;
        private Action<string> _setComposeBodyHandler;
        private Action<string> _setReplyDraftHandler;
        private Func<int, string> _moveThreadPageHandler;
        private Func<int, string> _setThreadPageHandler;
        private Func<int, string> _moveCommentPageHandler;
        private Func<int, string> _setCommentPageHandler;
        private Func<int, string> _moveComposeCashPageHandler;
        private Func<int, string> _moveReplyCashPageHandler;
        private Func<GuildBbsEmoticonKind, int, int, string> _selectComposeEmoticonHandler;
        private Func<GuildBbsEmoticonKind, int, int, string> _selectReplyEmoticonHandler;
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
        private readonly UIObject[] _commentDeleteButtons = new UIObject[4];
        private UIObject _emoticonLeftButton;
        private UIObject _emoticonRightButton;
        private GuildBbsSnapshot _currentSnapshot = new();

        private enum InputTarget
        {
            None,
            ComposeTitle,
            ComposeBody,
            ReplyBody
        }

        private enum PendingPrompt
        {
            None,
            DeleteThread,
            DeleteLatestReply,
            DeleteReply
        }

        private sealed class RowLayout
        {
            public int ThreadId { get; init; }
            public Rectangle Bounds { get; init; }
        }

        private sealed class ScrollMetrics
        {
            public int TotalLines { get; init; }
            public int VisibleLines { get; init; }
            public int MaxScrollLine { get; init; }
            public Rectangle TrackBounds { get; init; }
            public Rectangle ThumbBounds { get; init; }
        }

        private sealed class TextLineLayout
        {
            public int StartIndex { get; init; }
            public int EndIndex { get; init; }
            public string Text { get; init; } = string.Empty;
        }

        private InputTarget _activeInputTarget;

        public GuildBbsWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            Texture2D emoticonSelectTexture,
            Texture2D[] basicEmoticonTextures,
            Texture2D[] cashEmoticonTextures,
            VerticalScrollbarSkin scrollbarSkin,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _contentOverlay = contentOverlay;
            _contentOverlayOffset = contentOverlayOffset;
            _emoticonSelectTexture = emoticonSelectTexture;
            _basicEmoticonTextures = basicEmoticonTextures ?? Array.Empty<Texture2D>();
            _cashEmoticonTextures = cashEmoticonTextures ?? Array.Empty<Texture2D>();
            _scrollbarSkin = scrollbarSkin;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public GuildBbsWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            Texture2D emoticonSelectTexture,
            Texture2D[] basicEmoticonTextures,
            Texture2D[] cashEmoticonTextures,
            GraphicsDevice device)
            : this(
                frame,
                overlay,
                overlayOffset,
                contentOverlay,
                contentOverlayOffset,
                emoticonSelectTexture,
                basicEmoticonTextures,
                cashEmoticonTextures,
                null,
                device)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.GuildBbs;
        public override bool CapturesKeyboardInput => IsVisible && _activeInputTarget != InputTarget.None;
        internal int BasicEmoticonSlotCount => _basicEmoticonTextures.Length;
        internal int CashEmoticonSlotCount => _cashEmoticonTextures.Length;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<GuildBbsSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _currentSnapshot = RefreshSnapshot();
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
            Func<int, string> deleteReplyAtVisibleIndexHandler,
            Action<string> setComposeTitleHandler,
            Action<string> setComposeBodyHandler,
            Action<string> setReplyDraftHandler,
            Func<int, string> moveThreadPageHandler,
            Func<int, string> setThreadPageHandler,
            Func<int, string> moveCommentPageHandler,
            Func<int, string> setCommentPageHandler,
            Func<int, string> moveComposeCashPageHandler,
            Func<int, string> moveReplyCashPageHandler,
            Func<GuildBbsEmoticonKind, int, int, string> selectComposeEmoticonHandler,
            Func<GuildBbsEmoticonKind, int, int, string> selectReplyEmoticonHandler,
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
            _deleteReplyAtVisibleIndexHandler = deleteReplyAtVisibleIndexHandler;
            _setComposeTitleHandler = setComposeTitleHandler;
            _setComposeBodyHandler = setComposeBodyHandler;
            _setReplyDraftHandler = setReplyDraftHandler;
            _moveThreadPageHandler = moveThreadPageHandler;
            _setThreadPageHandler = setThreadPageHandler;
            _moveCommentPageHandler = moveCommentPageHandler;
            _setCommentPageHandler = setCommentPageHandler;
            _moveComposeCashPageHandler = moveComposeCashPageHandler;
            _moveReplyCashPageHandler = moveReplyCashPageHandler;
            _selectComposeEmoticonHandler = selectComposeEmoticonHandler;
            _selectReplyEmoticonHandler = selectReplyEmoticonHandler;
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
            UIObject replyDeleteButton,
            UIObject[] commentDeleteButtons,
            UIObject emoticonLeftButton,
            UIObject emoticonRightButton)
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
            if (commentDeleteButtons != null)
            {
                for (int index = 0; index < Math.Min(_commentDeleteButtons.Length, commentDeleteButtons.Length); index++)
                {
                    _commentDeleteButtons[index] = commentDeleteButtons[index];
                }
            }
            _emoticonLeftButton = emoticonLeftButton;
            _emoticonRightButton = emoticonRightButton;

            ConfigureButton(_registerButton, HandleSubmitCompose);
            ConfigureButton(_cancelButton, HandleCancelCompose);
            ConfigureButton(_noticeButton, () => ShowFeedback(_toggleNoticeHandler?.Invoke()));
            ConfigureButton(_writeButton, HandleBeginWrite);
            ConfigureButton(_retouchButton, HandleEditSelected);
            ConfigureButton(_deleteButton, OpenDeleteThreadPrompt);
            ConfigureButton(_quitButton, Hide);
            ConfigureButton(_replyButton, HandleSubmitReply);
            ConfigureButton(_replyDeleteButton, OpenDeleteLatestReplyPrompt);
            for (int index = 0; index < _commentDeleteButtons.Length; index++)
            {
                int visibleIndex = index;
                ConfigureButton(_commentDeleteButtons[index], () => OpenDeleteReplyPrompt(visibleIndex));
            }
            ConfigureButton(_emoticonLeftButton, () => MoveCashEmoticonPage(-1));
            ConfigureButton(_emoticonRightButton, () => MoveCashEmoticonPage(1));

            UpdateButtonStates(_currentSnapshot ?? RefreshSnapshot());
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GuildBbsSnapshot snapshot = RefreshSnapshot();
            UpdateButtonStates(snapshot);
            UpdateDynamicButtonLayout(snapshot);

            KeyboardState keyboardState = Keyboard.GetState();
            HandleKeyboardInput(keyboardState, snapshot, Environment.TickCount);

            MouseState mouseState = Mouse.GetState();
            HandleScroll(snapshot, mouseState);
            HandleComposeScrollDrag(snapshot, mouseState);
            HandleDetailBodyScrollDrag(snapshot, mouseState);
            HandleCommentScrollDrag(snapshot, mouseState);

            bool leftReleased = mouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool releasedComposeDrag = _isDraggingComposeScrollBar && leftReleased;
            bool releasedDetailBodyDrag = _isDraggingDetailBodyScrollBar && leftReleased;
            bool releasedCommentDrag = _isDraggingCommentScrollBar && leftReleased;
            if (leftReleased && _pendingPrompt != PendingPrompt.None)
            {
                if (_ignorePromptMouseRelease)
                {
                    _ignorePromptMouseRelease = false;
                }
                else
                {
                    HandlePromptMouseClick(mouseState.Position);
                }
            }
            else if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                if (!releasedComposeDrag && !releasedDetailBodyDrag && !releasedCommentDrag)
                {
                    HandleMouseClick(snapshot, mouseState.Position);
                }
            }

            if (leftReleased)
            {
                _isDraggingComposeScrollBar = false;
                _isDraggingDetailBodyScrollBar = false;
                _isDraggingCommentScrollBar = false;
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
            _previousScrollValue = mouseState.ScrollWheelValue;
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
            GuildBbsSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            if (snapshot.IsWriteMode)
            {
                DrawLayer(sprite, _contentOverlay, _contentOverlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }
            else
            {
                DrawLayer(sprite, _overlay, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }

            if (_font == null)
            {
                return;
            }

            sprite.DrawString(_font, "Guild BBS", new Vector2(Position.X + 54, Position.Y + 7), Color.White, 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, snapshot.GuildName, new Vector2(Position.X + 58, Position.Y + 22), new Color(59, 58, 54), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
            DrawString(
                sprite,
                $"{snapshot.GuildRoleLabel}  Auth {snapshot.Permission?.AuthoritySourceLabel ?? "Guild role"}  Cash {snapshot.Permission?.OwnedCashEmoticonCount ?? 0} ({snapshot.Permission?.CashOwnershipSourceLabel ?? "Inventory"})",
                Position.X + 58,
                Position.Y + 34,
                new Color(92, 95, 102),
                0.33f);
            DrawGuildMark(sprite, snapshot.GuildName);
            DrawNoticeRow(sprite, snapshot);
            DrawThreadList(sprite, snapshot);

            if (snapshot.IsWriteMode)
            {
                DrawComposePane(sprite, snapshot.Compose, TickCount);
            }
            else
            {
                DrawDetailPane(sprite, snapshot, TickCount);
            }

            DrawPromptOverlay(sprite);
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

        private GuildBbsSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new GuildBbsSnapshot();
            int selectedThreadId = _currentSnapshot.SelectedThread?.ThreadId ?? -1;
            if (_lastDetailThreadId != selectedThreadId)
            {
                _detailBodyScrollLine = 0;
                _lastDetailThreadId = selectedThreadId;
            }

            ScrollMetrics detailBodyMetrics = BuildDetailBodyScrollMetrics(_currentSnapshot.SelectedThread?.Body);
            _detailBodyScrollLine = Math.Clamp(_detailBodyScrollLine, 0, detailBodyMetrics.MaxScrollLine);
            return _currentSnapshot;
        }

        private void HandleBeginWrite()
        {
            string message = _writeHandler?.Invoke();
            ShowFeedback(message);
            GuildBbsSnapshot snapshot = RefreshSnapshot();
            if (snapshot.IsWriteMode)
            {
                ActivateInput(InputTarget.ComposeTitle, snapshot, clearExisting: false);
            }
        }

        private void HandleEditSelected()
        {
            string message = _editHandler?.Invoke();
            ShowFeedback(message);
            GuildBbsSnapshot snapshot = RefreshSnapshot();
            if (snapshot.IsWriteMode)
            {
                ActivateInput(InputTarget.ComposeTitle, snapshot, clearExisting: false);
            }
        }

        private void HandleSubmitCompose()
        {
            string message = _submitHandler?.Invoke();
            ShowFeedback(message);
            if (!RefreshSnapshot().IsWriteMode)
            {
                DeactivateInput(clearText: true);
            }
        }

        private void HandleCancelCompose()
        {
            ShowFeedback(_cancelHandler?.Invoke());
            DeactivateInput(clearText: true);
        }

        private void HandleSubmitReply()
        {
            string message = _replyHandler?.Invoke();
            ShowFeedback(message);
            if (string.IsNullOrEmpty(RefreshSnapshot().ReplyDraft?.Body))
            {
                DeactivateInput(clearText: true);
            }
        }

        private void OpenDeleteThreadPrompt()
        {
            GuildBbsSnapshot snapshot = RefreshSnapshot();
            if (snapshot.SelectedThread == null || snapshot.Permission?.CanDeleteSelectedThread != true)
            {
                ShowFeedback(_deleteHandler?.Invoke());
                return;
            }

            OpenPrompt(PendingPrompt.DeleteThread, -1);
        }

        private void OpenDeleteReplyPrompt(int visibleIndex)
        {
            GuildBbsSnapshot snapshot = RefreshSnapshot();
            GuildBbsThreadSnapshot selectedThread = snapshot.SelectedThread;
            bool canDelete = selectedThread?.Comments != null
                && visibleIndex >= 0
                && visibleIndex < selectedThread.Comments.Count
                && selectedThread.Comments[visibleIndex].CanDelete;
            if (!canDelete)
            {
                ShowFeedback(_deleteReplyAtVisibleIndexHandler?.Invoke(visibleIndex));
                return;
            }

            OpenPrompt(PendingPrompt.DeleteReply, visibleIndex);
        }

        private void OpenDeleteLatestReplyPrompt()
        {
            GuildBbsSnapshot snapshot = RefreshSnapshot();
            bool canDeleteReply = snapshot.Permission?.CanDeleteReply == true;
            if (!canDeleteReply)
            {
                ShowFeedback(_replyDeleteHandler?.Invoke());
                return;
            }

            OpenPrompt(PendingPrompt.DeleteLatestReply, -1);
        }

        private void OpenPrompt(PendingPrompt prompt, int visibleReplyIndex)
        {
            _pendingPrompt = prompt;
            _pendingPromptVisibleReplyIndex = visibleReplyIndex;
            _ignorePromptMouseRelease = true;
            DeactivateInput(clearText: false);
            ShowFeedback($"{GuildBbsRuntime.GetDeletePostPrompt()} [StringPool 0xEB2]");
        }

        private void ConfirmPrompt()
        {
            PendingPrompt prompt = _pendingPrompt;
            int visibleReplyIndex = _pendingPromptVisibleReplyIndex;
            ClearPrompt();

            string message = prompt switch
            {
                PendingPrompt.DeleteThread => _deleteHandler?.Invoke(),
                PendingPrompt.DeleteLatestReply => _replyDeleteHandler?.Invoke(),
                PendingPrompt.DeleteReply => _deleteReplyAtVisibleIndexHandler?.Invoke(visibleReplyIndex),
                _ => null
            };
            ShowFeedback(message);
        }

        private void CancelPrompt()
        {
            ClearPrompt();
            ShowFeedback("Guild BBS delete request canceled.");
        }

        private void ClearPrompt()
        {
            _pendingPrompt = PendingPrompt.None;
            _pendingPromptVisibleReplyIndex = -1;
            _ignorePromptMouseRelease = false;
        }

        private void HandleMouseClick(GuildBbsSnapshot snapshot, Point mousePosition)
        {
            if (TryHandleNoticeSelection(snapshot, mousePosition))
            {
                return;
            }

            if (TryHandleThreadPageSelectorClick(snapshot, mousePosition))
            {
                return;
            }

            if (snapshot.IsWriteMode)
            {
                if (GetComposeTitleBounds().Contains(mousePosition))
                {
                    ActivateInput(InputTarget.ComposeTitle, snapshot, clearExisting: false);
                    SetCursorFromPoint(InputTarget.ComposeTitle, snapshot, mousePosition);
                    return;
                }

                if (GetComposeBodyBounds().Contains(mousePosition))
                {
                    ActivateInput(InputTarget.ComposeBody, snapshot, clearExisting: false);
                    SetCursorFromPoint(InputTarget.ComposeBody, snapshot, mousePosition);
                    return;
                }

                if (TryHandleComposeScrollClick(snapshot, mousePosition))
                {
                    return;
                }

                if (TryHandleEmoticonClick(snapshot.Compose, mousePosition, composing: true))
                {
                    return;
                }
            }
            else
            {
                if (snapshot.SelectedThread != null && TryHandleDetailBodyScrollClick(snapshot, mousePosition))
                {
                    return;
                }

                if (snapshot.SelectedThread != null && TryHandleCommentScrollClick(snapshot, mousePosition))
                {
                    return;
                }

                if (snapshot.SelectedThread != null && GetReplyInputBounds().Contains(mousePosition))
                {
                    ActivateInput(InputTarget.ReplyBody, snapshot, clearExisting: false);
                    SetCursorFromPoint(InputTarget.ReplyBody, snapshot, mousePosition);
                    return;
                }

                if (snapshot.SelectedThread != null && TryHandleEmoticonClick(snapshot.ReplyDraft, mousePosition, composing: false))
                {
                    return;
                }
            }

            HandleThreadSelection(mousePosition);
        }

        private bool TryHandleEmoticonClick(GuildBbsComposeSnapshot compose, Point mousePosition, bool composing)
        {
            if (compose == null)
            {
                return false;
            }

            for (int i = 0; i < _basicEmoticonTextures.Length; i++)
            {
                if (!GetBasicEmoticonBounds(i).Contains(mousePosition))
                {
                    continue;
                }

                ShowFeedback(composing
                    ? _selectComposeEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Basic, i, compose.CashEmoticonPageIndex)
                    : _selectReplyEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Basic, i, compose.CashEmoticonPageIndex));
                return true;
            }

            IReadOnlyList<int> visibleCashSlots = BuildVisibleCashEmoticonSlots(
                compose.CashEmoticonOwnership,
                compose.CashEmoticonPageIndex,
                VisibleCashEmoticonCount);
            for (int i = 0; i < VisibleCashEmoticonCount; i++)
            {
                if (!GetCashEmoticonBounds(i).Contains(mousePosition))
                {
                    continue;
                }

                if (i >= visibleCashSlots.Count)
                {
                    return false;
                }

                int globalSlotIndex = visibleCashSlots[i];
                ShowFeedback(composing
                    ? _selectComposeEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Cash, i, compose.CashEmoticonPageIndex)
                    : _selectReplyEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Cash, i, compose.CashEmoticonPageIndex));
                return true;
            }

            return false;
        }

        private bool TryHandleEmoticonClick(GuildBbsReplyDraftSnapshot replyDraft, Point mousePosition, bool composing)
        {
            if (replyDraft == null)
            {
                return false;
            }

            for (int i = 0; i < _basicEmoticonTextures.Length; i++)
            {
                if (!GetBasicEmoticonBounds(i).Contains(mousePosition))
                {
                    continue;
                }

                ShowFeedback(composing
                    ? _selectComposeEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Basic, i, replyDraft.CashEmoticonPageIndex)
                    : _selectReplyEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Basic, i, replyDraft.CashEmoticonPageIndex));
                return true;
            }

            IReadOnlyList<int> visibleCashSlots = BuildVisibleCashEmoticonSlots(
                replyDraft.CashEmoticonOwnership,
                replyDraft.CashEmoticonPageIndex,
                VisibleCashEmoticonCount);
            for (int i = 0; i < VisibleCashEmoticonCount; i++)
            {
                if (!GetCashEmoticonBounds(i).Contains(mousePosition))
                {
                    continue;
                }

                if (i >= visibleCashSlots.Count)
                {
                    return false;
                }

                int globalSlotIndex = visibleCashSlots[i];
                ShowFeedback(composing
                    ? _selectComposeEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Cash, i, replyDraft.CashEmoticonPageIndex)
                    : _selectReplyEmoticonHandler?.Invoke(GuildBbsEmoticonKind.Cash, i, replyDraft.CashEmoticonPageIndex));
                return true;
            }

            return false;
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
                DeactivateInput(clearText: true);
                break;
            }
        }

        private void HandleScroll(GuildBbsSnapshot snapshot, MouseState mouseState)
        {
            int scrollDelta = mouseState.ScrollWheelValue - _previousScrollValue;
            if (scrollDelta == 0 || !ContainsPoint(mouseState.X, mouseState.Y))
            {
                return;
            }

            int direction = scrollDelta > 0 ? -1 : 1;
            if (GetThreadListBounds().Contains(mouseState.Position))
            {
                _moveThreadPageHandler?.Invoke(direction);
                return;
            }

            if (snapshot.IsWriteMode && GetComposeBodyBounds().Contains(mouseState.Position))
            {
                ScrollComposeBody(direction);
                return;
            }

            if (!snapshot.IsWriteMode && snapshot.SelectedThread != null && GetDetailBodyBounds().Contains(mouseState.Position))
            {
                ScrollDetailBody(direction);
                return;
            }

            if (!snapshot.IsWriteMode && snapshot.SelectedThread != null && GetCommentPaneBounds().Contains(mouseState.Position))
            {
                _moveCommentPageHandler?.Invoke(direction);
            }
        }

        private void HandleKeyboardInput(KeyboardState keyboardState, GuildBbsSnapshot snapshot, int tickCount)
        {
            if (_pendingPrompt != PendingPrompt.None)
            {
                if ((keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
                    || (keyboardState.IsKeyDown(Keys.Y) && _previousKeyboardState.IsKeyUp(Keys.Y)))
                {
                    ConfirmPrompt();
                }
                else if ((keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
                    || (keyboardState.IsKeyDown(Keys.N) && _previousKeyboardState.IsKeyUp(Keys.N)))
                {
                    CancelPrompt();
                }

                ResetKeyRepeat();
                return;
            }

            if (_activeInputTarget == InputTarget.None)
            {
                ResetKeyRepeat();
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                DeactivateInput(clearText: false);
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Tab) && _previousKeyboardState.IsKeyUp(Keys.Tab) && snapshot.IsWriteMode)
            {
                ActivateInput(_activeInputTarget == InputTarget.ComposeTitle ? InputTarget.ComposeBody : InputTarget.ComposeTitle, snapshot, clearExisting: false);
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter))
            {
                if (_activeInputTarget == InputTarget.ComposeTitle)
                {
                    ActivateInput(InputTarget.ComposeBody, snapshot, clearExisting: false);
                }
                else if (_activeInputTarget == InputTarget.ComposeBody)
                {
                    TryInsertInputText("\n");
                }
                else if (_activeInputTarget == InputTarget.ReplyBody)
                {
                    HandleSubmitReply();
                }

                return;
            }

            if (HandleNavigationKey(keyboardState, Keys.Back, tickCount, removeBackward: true)
                || HandleNavigationKey(keyboardState, Keys.Delete, tickCount, removeBackward: false)
                || HandleCursorKey(keyboardState, Keys.Left, tickCount, -1)
                || HandleCursorKey(keyboardState, Keys.Right, tickCount, 1)
                || HandleVerticalCursorKey(keyboardState, Keys.Up, tickCount, -1)
                || HandleVerticalCursorKey(keyboardState, Keys.Down, tickCount, 1))
            {
                SyncInputBuffer();
                return;
            }

            if (keyboardState.IsKeyDown(Keys.Home) && _previousKeyboardState.IsKeyUp(Keys.Home))
            {
                _cursorPosition = ResolveHomeCursorPosition();
                EnsureComposeCursorVisible();
                return;
            }

            if (keyboardState.IsKeyDown(Keys.End) && _previousKeyboardState.IsKeyUp(Keys.End))
            {
                _cursorPosition = ResolveEndCursorPosition();
                EnsureComposeCursorVisible();
                return;
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (_previousKeyboardState.IsKeyDown(key) || IsControlKey(key))
                {
                    continue;
                }

                char? character = KeyToChar(key, shift);
                if (!character.HasValue)
                {
                    continue;
                }

                if (TryInsertInputText(character.Value.ToString()))
                {
                    _lastHeldKey = key;
                    _keyHoldStartTime = tickCount;
                    _lastKeyRepeatTime = tickCount;
                }
            }

            if (_lastHeldKey != Keys.None
                && !IsControlKey(_lastHeldKey)
                && keyboardState.IsKeyDown(_lastHeldKey)
                && ShouldRepeatKey(_lastHeldKey, tickCount))
            {
                char? repeatedCharacter = KeyToChar(_lastHeldKey, shift);
                if (repeatedCharacter.HasValue)
                {
                    if (TryInsertInputText(repeatedCharacter.Value.ToString()))
                    {
                        _lastKeyRepeatTime = tickCount;
                    }
                }
            }
            else if (_lastHeldKey != Keys.None && !keyboardState.IsKeyDown(_lastHeldKey))
            {
                ResetKeyRepeat();
            }
        }

        private bool HandleNavigationKey(KeyboardState keyboardState, Keys key, int tickCount, bool removeBackward)
        {
            if (!keyboardState.IsKeyDown(key))
            {
                return false;
            }

            if (_previousKeyboardState.IsKeyUp(key))
            {
                ApplyDelete(removeBackward);
                _lastHeldKey = key;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            if (ShouldRepeatKey(key, tickCount))
            {
                ApplyDelete(removeBackward);
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            return true;
        }

        private bool HandleCursorKey(KeyboardState keyboardState, Keys key, int tickCount, int delta)
        {
            if (!keyboardState.IsKeyDown(key))
            {
                return false;
            }

            if (_previousKeyboardState.IsKeyUp(key))
            {
                _cursorPosition = Math.Clamp(_cursorPosition + delta, 0, _inputBuffer.Length);
                EnsureComposeCursorVisible();
                _lastHeldKey = key;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            if (ShouldRepeatKey(key, tickCount))
            {
                _cursorPosition = Math.Clamp(_cursorPosition + delta, 0, _inputBuffer.Length);
                EnsureComposeCursorVisible();
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            return true;
        }

        private bool HandleVerticalCursorKey(KeyboardState keyboardState, Keys key, int tickCount, int delta)
        {
            if (_activeInputTarget != InputTarget.ComposeBody || !keyboardState.IsKeyDown(key))
            {
                return false;
            }

            if (_previousKeyboardState.IsKeyUp(key))
            {
                MoveComposeBodyCursorVertical(delta);
                _lastHeldKey = key;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            if (ShouldRepeatKey(key, tickCount))
            {
                MoveComposeBodyCursorVertical(delta);
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            return true;
        }

        private void ApplyDelete(bool removeBackward)
        {
            if (removeBackward)
            {
                if (_cursorPosition <= 0)
                {
                    return;
                }

                _inputBuffer.Remove(_cursorPosition - 1, 1);
                _cursorPosition--;
                return;
            }

            if (_cursorPosition < _inputBuffer.Length)
            {
                _inputBuffer.Remove(_cursorPosition, 1);
            }
        }

        private void ActivateInput(InputTarget target, GuildBbsSnapshot snapshot, bool clearExisting)
        {
            _activeInputTarget = target;
            string source = target switch
            {
                InputTarget.ComposeTitle => snapshot.Compose?.Title ?? string.Empty,
                InputTarget.ComposeBody => snapshot.Compose?.Body ?? string.Empty,
                InputTarget.ReplyBody => snapshot.ReplyDraft?.Body ?? string.Empty,
                _ => string.Empty
            };

            _inputBuffer.Clear();
            if (!clearExisting)
            {
                _inputBuffer.Append(source);
            }

            _cursorPosition = _inputBuffer.Length;
            _cursorBlinkTimer = Environment.TickCount;
            if (target == InputTarget.ComposeBody)
            {
                EnsureComposeCursorVisible();
            }

            ResetKeyRepeat();
        }

        private void DeactivateInput(bool clearText)
        {
            if (clearText)
            {
                _inputBuffer.Clear();
                _cursorPosition = 0;
            }

            _activeInputTarget = InputTarget.None;
            ResetKeyRepeat();
        }

        private void SyncInputBuffer()
        {
            string value = _inputBuffer.ToString();
            switch (_activeInputTarget)
            {
                case InputTarget.ComposeTitle:
                    _setComposeTitleHandler?.Invoke(value);
                    break;
                case InputTarget.ComposeBody:
                    _setComposeBodyHandler?.Invoke(value);
                    break;
                case InputTarget.ReplyBody:
                    _setReplyDraftHandler?.Invoke(value);
                    break;
            }

            ReconcileInputBufferWithSnapshot(RefreshSnapshot());
            _cursorBlinkTimer = Environment.TickCount;
            EnsureComposeCursorVisible();
        }

        private void ReconcileInputBufferWithSnapshot(GuildBbsSnapshot snapshot)
        {
            string resolvedValue = _activeInputTarget switch
            {
                InputTarget.ComposeTitle => snapshot.Compose?.Title ?? string.Empty,
                InputTarget.ComposeBody => snapshot.Compose?.Body ?? string.Empty,
                InputTarget.ReplyBody => snapshot.ReplyDraft?.Body ?? string.Empty,
                _ => null
            };

            if (resolvedValue == null || string.Equals(_inputBuffer.ToString(), resolvedValue, StringComparison.Ordinal))
            {
                return;
            }

            _inputBuffer.Clear();
            _inputBuffer.Append(resolvedValue);
            _cursorPosition = Math.Clamp(_cursorPosition, 0, _inputBuffer.Length);
        }

        private void UpdateButtonStates(GuildBbsSnapshot snapshot)
        {
            bool hasSelectedThread = snapshot.SelectedThread != null;
            bool writeMode = snapshot.IsWriteMode;
            GuildBbsPermissionSnapshot permission = snapshot.Permission ?? new GuildBbsPermissionSnapshot();

            _registerButton?.SetVisible(writeMode);
            _cancelButton?.SetVisible(writeMode);
            _writeButton?.SetVisible(!writeMode);
            _retouchButton?.SetVisible(!writeMode);
            _deleteButton?.SetVisible(!writeMode);
            _replyButton?.SetVisible(!writeMode);
            _replyDeleteButton?.SetVisible(false);
            _noticeButton?.SetVisible(!writeMode);
            _quitButton?.SetVisible(true);
            _emoticonLeftButton?.SetVisible(writeMode || hasSelectedThread);
            _emoticonRightButton?.SetVisible(writeMode || hasSelectedThread);
            for (int index = 0; index < _commentDeleteButtons.Length; index++)
            {
                UIObject button = _commentDeleteButtons[index];
                bool hasCommentSlot = !writeMode
                    && hasSelectedThread
                    && snapshot.SelectedThread?.Comments != null
                    && index < snapshot.SelectedThread.Comments.Count;
                bool canDeleteComment = hasCommentSlot && snapshot.SelectedThread.Comments[index].CanDelete;
                button?.SetVisible(canDeleteComment);
                button?.SetButtonState(canDeleteComment ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            _registerButton?.SetButtonState(writeMode && permission.CanWrite ? UIObjectState.Normal : UIObjectState.Disabled);
            _cancelButton?.SetButtonState(writeMode ? UIObjectState.Normal : UIObjectState.Disabled);
            _writeButton?.SetButtonState(!writeMode && permission.CanWrite ? UIObjectState.Normal : UIObjectState.Disabled);
            _retouchButton?.SetButtonState(!writeMode && hasSelectedThread && permission.CanEditSelectedThread ? UIObjectState.Normal : UIObjectState.Disabled);
            _deleteButton?.SetButtonState(!writeMode && hasSelectedThread && permission.CanDeleteSelectedThread ? UIObjectState.Normal : UIObjectState.Disabled);
            _replyButton?.SetButtonState(!writeMode && hasSelectedThread && permission.CanReply ? UIObjectState.Normal : UIObjectState.Disabled);
            _replyDeleteButton?.SetButtonState(!writeMode && hasSelectedThread && permission.CanDeleteReply ? UIObjectState.Normal : UIObjectState.Disabled);
            _noticeButton?.SetButtonState(permission.CanWriteNotice ? UIObjectState.Normal : UIObjectState.Disabled);

            bool canPageCash = writeMode
                ? (snapshot.Compose?.CashEmoticonPageCount ?? 0) > 1
                : (snapshot.ReplyDraft?.CashEmoticonPageCount ?? 0) > 1;
            _emoticonLeftButton?.SetButtonState(canPageCash ? UIObjectState.Normal : UIObjectState.Disabled);
            _emoticonRightButton?.SetButtonState(canPageCash ? UIObjectState.Normal : UIObjectState.Disabled);
        }

        private static string DescribePermissions(GuildBbsPermissionSnapshot permission, GuildBbsThreadSnapshot selectedThread)
        {
            if (permission == null)
            {
                return string.Empty;
            }

            string threadAccess = selectedThread == null
                ? "No thread selected"
                : $"Edit {(permission.CanEditSelectedThread ? "Y" : "N")}  Delete {(permission.CanDeleteSelectedThread ? "Y" : "N")}  Reply {(permission.CanReply ? "Y" : "N")}";
            return $"{permission.PermissionLabel} {permission.AuthoritySourceLabel} [{permission.PermissionMaskText}]  Notice {(permission.CanWriteNotice ? "Y" : "N")}  {threadAccess}";
        }

        private void UpdateDynamicButtonLayout(GuildBbsSnapshot snapshot)
        {
            bool writeMode = snapshot?.IsWriteMode == true;

            PositionButton(_quitButton, 680, 501);
            PositionButton(_registerButton, 602, 275);
            PositionButton(_cancelButton, 664, 275);
            PositionButton(_noticeButton, 404, 275);
            PositionButton(_writeButton, 540, 275);
            PositionButton(_retouchButton, 602, 275);
            PositionButton(_deleteButton, 664, 275);
            PositionButton(_replyButton, 704, 457);

            if (!writeMode)
            {
                PositionButton(_replyDeleteButton, 690, 331);
            }

            if (_emoticonLeftButton != null)
            {
                _emoticonLeftButton.X = Position.X + 404;
                _emoticonLeftButton.Y = Position.Y + 243;
            }

            if (_emoticonRightButton != null)
            {
                _emoticonRightButton.X = Position.X + 704;
                _emoticonRightButton.Y = Position.Y + 243;
            }

            int[] commentDeleteYs = { 331, 362, 393, 424 };
            for (int index = 0; index < _commentDeleteButtons.Length; index++)
            {
                UIObject button = _commentDeleteButtons[index];
                if (button == null)
                {
                    continue;
                }

                button.X = Position.X + 690;
                button.Y = Position.Y + commentDeleteYs[index];
            }
        }

        private void PositionButton(UIObject button, int x, int y)
        {
            if (button == null)
            {
                return;
            }

            button.X = Position.X + x;
            button.Y = Position.Y + y;
        }

        private void MoveCashEmoticonPage(int delta)
        {
            GuildBbsSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            string message = snapshot.IsWriteMode
                ? _moveComposeCashPageHandler?.Invoke(delta)
                : _moveReplyCashPageHandler?.Invoke(delta);
            ShowFeedback(message);
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

        private void DrawNoticeRow(SpriteBatch sprite, GuildBbsSnapshot snapshot)
        {
            Rectangle bounds = GetNoticeBounds();
            sprite.Draw(_pixel, bounds, new Color(255, 255, 255, 28));

            GuildBbsThreadEntrySnapshot noticeThread = snapshot.NoticeThread;
            if (noticeThread == null)
            {
                DrawString(sprite, "No guild notice is registered.", bounds.X + 6, bounds.Y + 9, new Color(104, 109, 117), 0.36f);
                return;
            }

            bool selected = noticeThread.ThreadId == snapshot.SelectedThreadId;
            if (selected)
            {
                sprite.Draw(_pixel, bounds, new Color(211, 146, 66, 60));
            }

            Rectangle badgeBounds = new Rectangle(bounds.X + 6, bounds.Y + 8, 24, 14);
            sprite.Draw(_pixel, badgeBounds, new Color(218, 143, 57, 210));
            DrawString(sprite, "NOTICE", badgeBounds.X + 2, badgeBounds.Y - 1, new Color(46, 28, 7), 0.26f);

            int titleX = badgeBounds.Right + 8;
            DrawInlineEmoticon(sprite, noticeThread.Emoticon, new Point(titleX, bounds.Y + 6), isSmall: true);
            if (noticeThread.Emoticon != null)
            {
                titleX += 20;
            }

            DrawString(sprite, Truncate(noticeThread.Title, 30), titleX, bounds.Y + 4, new Color(61, 53, 44), 0.41f);
            DrawString(sprite, Truncate(noticeThread.Author, 10), titleX, bounds.Y + 16, new Color(90, 92, 99), 0.32f);
            DrawString(sprite, noticeThread.DateText, bounds.Right - 72, bounds.Y + 16, new Color(102, 104, 111), 0.30f);
        }

        private void DrawThreadList(SpriteBatch sprite, GuildBbsSnapshot snapshot)
        {
            _rowLayouts.Clear();

            Rectangle bounds = GetThreadListBounds();
            sprite.Draw(_pixel, bounds, new Color(8, 10, 14, 35));

            IReadOnlyList<GuildBbsThreadEntrySnapshot> threads = snapshot.Threads ?? Array.Empty<GuildBbsThreadEntrySnapshot>();
            for (int i = 0; i < threads.Count; i++)
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

                DrawInlineEmoticon(sprite, thread.Emoticon, new Point(titleX, rowBounds.Y + 6), isSmall: true);
                if (thread.Emoticon != null)
                {
                    titleX += 20;
                }

                DrawString(sprite, Truncate(thread.Title, 18), titleX, rowBounds.Y + 4, new Color(61, 53, 44), 0.41f);
                DrawString(sprite, Truncate(thread.Author, 10), rowBounds.X + 81, rowBounds.Y + 16, new Color(90, 92, 99), 0.34f);
                DrawString(sprite, thread.DateText, rowBounds.X + 258, rowBounds.Y + 16, new Color(102, 104, 111), 0.32f);

                if (thread.CommentCount > 0)
                {
                    DrawString(sprite, $"{thread.CommentCount}", rowBounds.X + 278, rowBounds.Y + 4, new Color(74, 106, 146), 0.34f);
                }
            }

            DrawString(
                sprite,
                $"Scroll to page threads",
                bounds.X + 6,
                bounds.Bottom + 4,
                new Color(96, 103, 114),
                0.32f);
            DrawThreadPageSelector(sprite, snapshot);
        }

        private void DrawComposePane(SpriteBatch sprite, GuildBbsComposeSnapshot compose, int tickCount)
        {
            Rectangle detailBounds = GetDetailBounds();
            Rectangle titleBounds = GetComposeTitleBounds();
            Rectangle bodyBounds = GetComposeBodyBounds();
            DrawString(sprite, compose.ModeText, detailBounds.X, detailBounds.Y - 22, new Color(83, 86, 92), 0.42f);
            DrawString(sprite, compose.IsNotice ? "[Notice]" : "[Thread]", detailBounds.X + 219, detailBounds.Y - 22, compose.IsNotice ? new Color(212, 143, 61) : new Color(85, 94, 110), 0.42f);

            DrawEditableText(sprite, compose.Title, titleBounds, 0.46f, new Color(58, 49, 39), InputTarget.ComposeTitle, tickCount);
            DrawEditableMultilineText(sprite, compose.Body, bodyBounds, 0.37f, new Color(78, 82, 91), InputTarget.ComposeBody, tickCount, ComposeBodyVisibleLineCount);
            DrawComposeScrollBar(sprite, compose.Body, bodyBounds);

            DrawString(sprite, "Title", titleBounds.X, titleBounds.Y - 16, new Color(101, 106, 115), 0.35f);
            DrawString(sprite, "Body", bodyBounds.X, bodyBounds.Y - 16, new Color(101, 106, 115), 0.35f);
            DrawEmoticonStrip(sprite, compose.SelectedEmoticon, compose.CashEmoticonPageIndex, compose.CashEmoticonPageCount, compose.CashEmoticonOwnership);
            DrawString(sprite, "TAB switches fields. Mouse wheel scrolls the body editor.", detailBounds.X, detailBounds.Bottom - 16, new Color(111, 117, 127), 0.34f);
        }

        private void DrawDetailPane(SpriteBatch sprite, GuildBbsSnapshot snapshot, int tickCount)
        {
            Rectangle detailBounds = GetDetailBounds();
            GuildBbsThreadSnapshot thread = snapshot.SelectedThread;
            if (thread == null)
            {
                DrawString(sprite, "Select a guild thread to inspect its detail surface.", detailBounds.X, detailBounds.Y, new Color(93, 96, 103), 0.44f);
                return;
            }

            DrawString(sprite, thread.IsNotice ? "[Notice]" : "[Thread]", detailBounds.X, detailBounds.Y - 20, thread.IsNotice ? new Color(211, 142, 63) : new Color(84, 91, 108), 0.41f);
            DrawInlineEmoticon(sprite, thread.Emoticon, new Point(detailBounds.X, detailBounds.Y + 2), isSmall: false);
            int titleOffset = thread.Emoticon == null ? 0 : 22;
            DrawString(sprite, thread.Title, detailBounds.X + titleOffset, detailBounds.Y, new Color(58, 49, 39), 0.52f);
            DrawString(sprite, $"By {thread.Author}", detailBounds.X, detailBounds.Y + 20, new Color(101, 106, 115), 0.39f);
            DrawString(sprite, thread.DateText, detailBounds.X + 242, detailBounds.Y + 20, new Color(101, 106, 115), 0.36f);
            sprite.Draw(_pixel, new Rectangle(detailBounds.X, detailBounds.Y + 38, detailBounds.Width, 1), new Color(204, 208, 216, 200));

            Rectangle bodyBounds = GetDetailBodyBounds();
            DrawReadOnlyMultilineText(sprite, thread.Body, bodyBounds, 0.37f, new Color(78, 82, 91), DetailBodyVisibleLineCount);
            DrawDetailBodyScrollBar(sprite, thread.Body);

            DrawString(sprite, $"Replies ({thread.TotalCommentCount})", detailBounds.X, detailBounds.Y + 164, new Color(69, 74, 83), 0.42f);
            Rectangle commentPaneBounds = GetCommentPaneBounds();
            int drawY = commentPaneBounds.Y;
            foreach (GuildBbsCommentSnapshot comment in thread.Comments)
            {
                Rectangle commentBounds = new Rectangle(commentPaneBounds.X, drawY, commentPaneBounds.Width, 31);
                sprite.Draw(_pixel, commentBounds, new Color(255, 255, 255, 28));
                DrawString(sprite, Truncate(comment.Author, 14), commentBounds.X + 4, commentBounds.Y + 2, new Color(61, 70, 87), 0.36f);
                DrawString(sprite, comment.DateText, commentBounds.X + 146, commentBounds.Y + 2, new Color(111, 116, 126), 0.29f);
                DrawInlineEmoticon(sprite, comment.Emoticon, new Point(commentBounds.X + 4, commentBounds.Y + 14), isSmall: true);
                int bodyX = comment.Emoticon == null ? commentBounds.X + 4 : commentBounds.X + 24;
                DrawString(sprite, Truncate(comment.Body, 32), bodyX, commentBounds.Y + 14, new Color(87, 91, 99), 0.34f);
                drawY += 31;
            }

            DrawString(
                sprite,
                $"Page {thread.CommentPageIndex + 1}/{Math.Max(1, thread.CommentPageCount)}  Scroll here to page comments",
                commentPaneBounds.X,
                commentPaneBounds.Bottom + 2,
                new Color(96, 103, 114),
                0.31f);
            DrawCommentScrollBar(sprite, thread);

            Rectangle replyBounds = GetReplyInputBounds();
            DrawEditableText(sprite, snapshot.ReplyDraft.Body, replyBounds, 0.39f, new Color(78, 82, 91), InputTarget.ReplyBody, tickCount);
            DrawString(sprite, "Reply draft", replyBounds.X, replyBounds.Y - 15, new Color(101, 106, 115), 0.34f);
            DrawEmoticonStrip(sprite, snapshot.ReplyDraft.SelectedEmoticon, snapshot.ReplyDraft.CashEmoticonPageIndex, snapshot.ReplyDraft.CashEmoticonPageCount, snapshot.ReplyDraft.CashEmoticonOwnership);
            if (snapshot.Permission != null)
            {
                DrawString(sprite, DescribePermissions(snapshot.Permission, snapshot.SelectedThread), detailBounds.X, detailBounds.Bottom - 10, new Color(111, 117, 127), 0.33f);
            }
        }

        private void DrawDetailBodyScrollBar(SpriteBatch sprite, string text)
        {
            ScrollMetrics metrics = BuildDetailBodyScrollMetrics(text);
            if (metrics.MaxScrollLine <= 0)
            {
                return;
            }

            DrawScrollbar(
                sprite,
                GetDetailBodyScrollBarPrevBounds(),
                GetDetailBodyScrollBarNextBounds(),
                metrics.TrackBounds,
                metrics.ThumbBounds,
                canMovePrev: _detailBodyScrollLine > 0,
                canMoveNext: _detailBodyScrollLine < metrics.MaxScrollLine,
                draggingThumb: _isDraggingDetailBodyScrollBar);
        }

        private void DrawComposeScrollBar(SpriteBatch sprite, string text, Rectangle bodyBounds)
        {
            ScrollMetrics metrics = BuildComposeScrollMetrics(text, bodyBounds);
            if (metrics.MaxScrollLine <= 0)
            {
                return;
            }

            DrawScrollbar(
                sprite,
                GetComposeScrollBarPrevBounds(),
                GetComposeScrollBarNextBounds(),
                metrics.TrackBounds,
                metrics.ThumbBounds,
                canMovePrev: _composeBodyScrollLine > 0,
                canMoveNext: _composeBodyScrollLine < metrics.MaxScrollLine,
                draggingThumb: _isDraggingComposeScrollBar);
        }

        private void DrawCommentScrollBar(SpriteBatch sprite, GuildBbsThreadSnapshot thread)
        {
            ScrollMetrics metrics = BuildCommentScrollMetrics(thread);
            if (metrics.MaxScrollLine <= 0)
            {
                return;
            }

            DrawScrollbar(
                sprite,
                GetCommentScrollBarPrevBounds(),
                GetCommentScrollBarNextBounds(),
                metrics.TrackBounds,
                metrics.ThumbBounds,
                canMovePrev: (thread?.CommentPageIndex ?? 0) > 0,
                canMoveNext: thread != null && thread.CommentPageIndex < Math.Max(0, thread.CommentPageCount - 1),
                draggingThumb: _isDraggingCommentScrollBar);
        }

        private void DrawThreadPageSelector(SpriteBatch sprite, GuildBbsSnapshot snapshot)
        {
            Rectangle bounds = GetThreadPageSelectorBounds();
            sprite.Draw(_pixel, bounds, new Color(255, 255, 255, 28));

            int visiblePageCount = Math.Min(ThreadPageSelectorVisibleCount, Math.Max(1, snapshot.ThreadPageCount));
            int startPageIndex = ResolveThreadPageSelectorStart(snapshot.ThreadPageIndex, snapshot.ThreadPageCount, visiblePageCount);
            int slotWidth = Math.Max(1, bounds.Width / ThreadPageSelectorVisibleCount);
            for (int slotIndex = 0; slotIndex < visiblePageCount; slotIndex++)
            {
                int pageIndex = startPageIndex + slotIndex;
                Rectangle slotBounds = new Rectangle(bounds.X + (slotIndex * slotWidth), bounds.Y, slotWidth, bounds.Height);
                bool selected = pageIndex == snapshot.ThreadPageIndex;
                sprite.Draw(_pixel, slotBounds, selected ? new Color(93, 117, 154, 120) : new Color(255, 255, 255, 10));
                DrawString(
                    sprite,
                    (pageIndex + 1).ToString(),
                    slotBounds.X + 2,
                    slotBounds.Y + 1,
                    selected ? new Color(52, 57, 67) : new Color(96, 103, 114),
                    0.29f);
            }
        }

        private void DrawScrollbar(
            SpriteBatch sprite,
            Rectangle prevBounds,
            Rectangle nextBounds,
            Rectangle trackBounds,
            Rectangle thumbBounds,
            bool canMovePrev,
            bool canMoveNext,
            bool draggingThumb)
        {
            if (_scrollbarSkin?.IsReady != true)
            {
                sprite.Draw(_pixel, trackBounds, new Color(41, 46, 56, 45));
                sprite.Draw(_pixel, thumbBounds, new Color(101, 112, 131, 155));
                return;
            }

            DrawScrollbarTrack(sprite, trackBounds);
            DrawScrollbarArrow(sprite, prevBounds, _scrollbarSkin.PrevStates, _scrollbarSkin.PrevDisabled, canMovePrev);
            DrawScrollbarArrow(sprite, nextBounds, _scrollbarSkin.NextStates, _scrollbarSkin.NextDisabled, canMoveNext);

            Texture2D thumbTexture = ResolveScrollbarStateTexture(_scrollbarSkin.ThumbStates, true, thumbBounds.Contains(Mouse.GetState().Position), draggingThumb);
            if (thumbTexture != null)
            {
                sprite.Draw(thumbTexture, new Vector2(thumbBounds.X, thumbBounds.Y), Color.White);
            }
        }

        private bool TryInsertInputText(string insertedText)
        {
            if (string.IsNullOrEmpty(insertedText))
            {
                return false;
            }

            string candidate = _inputBuffer.ToString().Insert(_cursorPosition, insertedText);
            if (_activeInputTarget == InputTarget.ComposeBody && !CanAcceptComposeBodyText(candidate))
            {
                return false;
            }

            _inputBuffer.Insert(_cursorPosition, insertedText);
            _cursorPosition += insertedText.Length;
            SyncInputBuffer();
            return true;
        }

        private void DrawEmoticonStrip(SpriteBatch sprite, GuildBbsEmoticonSnapshot selectedEmoticon, int cashPageIndex, int cashPageCount, IReadOnlyList<bool> cashOwnership)
        {
            for (int i = 0; i < _basicEmoticonTextures.Length; i++)
            {
                DrawEmoticonSlot(sprite, _basicEmoticonTextures[i], GetBasicEmoticonBounds(i), selectedEmoticon?.Kind == GuildBbsEmoticonKind.Basic && selectedEmoticon.SlotIndex == i);
            }

            IReadOnlyList<int> visibleCashSlots = BuildVisibleCashEmoticonSlots(cashOwnership, cashPageIndex, VisibleCashEmoticonCount);
            for (int i = 0; i < VisibleCashEmoticonCount; i++)
            {
                int globalSlotIndex = i < visibleCashSlots.Count ? visibleCashSlots[i] : -1;
                Texture2D texture = globalSlotIndex >= 0 && globalSlotIndex < _cashEmoticonTextures.Length
                    ? _cashEmoticonTextures[globalSlotIndex]
                    : null;
                DrawEmoticonSlot(
                    sprite,
                    texture,
                    GetCashEmoticonBounds(i),
                    selectedEmoticon?.Kind == GuildBbsEmoticonKind.Cash && selectedEmoticon.SlotIndex == globalSlotIndex && selectedEmoticon.CashPageIndex == cashPageIndex,
                    globalSlotIndex >= 0);
            }

            Rectangle cashRow = GetCashEmoticonRowBounds();
            DrawString(sprite, $"Cash {cashPageIndex + 1}/{Math.Max(1, cashPageCount)}", cashRow.X + 45, cashRow.Y - 16, new Color(101, 106, 115), 0.33f);
        }

        private void DrawEmoticonSlot(SpriteBatch sprite, Texture2D texture, Rectangle bounds, bool selected, bool isOwned = true)
        {
            if (texture == null)
            {
                sprite.Draw(_pixel, bounds, new Color(180, 184, 191, 80));
            }
            else
            {
                sprite.Draw(texture, bounds, isOwned ? Color.White : new Color(255, 255, 255, 72));
            }

            if (!isOwned)
            {
                sprite.Draw(_pixel, bounds, new Color(11, 13, 18, 140));
                DrawString(sprite, "X", bounds.X + 6, bounds.Y + 1, new Color(229, 232, 237), 0.30f);
            }

            if (selected && _emoticonSelectTexture != null)
            {
                sprite.Draw(_emoticonSelectTexture, new Rectangle(bounds.X - 2, bounds.Y - 2, EmoticonSelectionSize, EmoticonSelectionSize), Color.White);
            }
        }

        private void DrawEditableText(SpriteBatch sprite, string text, Rectangle bounds, float scale, Color color, InputTarget target, int tickCount)
        {
            string value = text ?? string.Empty;
            string clipped = TruncateToWidth(value, bounds.Width - 8, scale);
            sprite.DrawString(_font, clipped, new Vector2(bounds.X + 4, bounds.Y + 3), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (_activeInputTarget != target || ((tickCount - _cursorBlinkTimer) / 500) % 2 != 0)
            {
                return;
            }

            string cursorSource = _inputBuffer.Length > 0 ? _inputBuffer.ToString()[..Math.Clamp(_cursorPosition, 0, _inputBuffer.Length)] : string.Empty;
            float cursorX = bounds.X + 4 + (_font.MeasureString(TruncateToWidth(cursorSource, bounds.Width - 8, scale)).X * scale);
            sprite.Draw(_pixel, new Rectangle((int)cursorX, bounds.Y + 3, 1, bounds.Height - 6), new Color(66, 76, 94));
        }

        private void DrawEditableMultilineText(SpriteBatch sprite, string text, Rectangle bounds, float scale, Color color, InputTarget target, int tickCount, int maxLines)
        {
            IReadOnlyList<TextLineLayout> lines = BuildTextLines(text ?? string.Empty, bounds.Width - 8, scale);
            if (lines.Count == 0)
            {
                lines = new[] { new TextLineLayout { StartIndex = 0, EndIndex = 0, Text = string.Empty } };
            }

            float y = bounds.Y + 2;
            int startLine = target == InputTarget.ComposeBody ? Math.Clamp(_composeBodyScrollLine, 0, Math.Max(0, lines.Count - maxLines)) : 0;
            int visibleLineCount = Math.Min(maxLines, lines.Count - startLine);
            for (int i = 0; i < visibleLineCount; i++)
            {
                sprite.DrawString(_font, lines[startLine + i].Text, new Vector2(bounds.X + 2, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += ComposeBodyLineHeight;
            }

            if (_activeInputTarget != target || ((tickCount - _cursorBlinkTimer) / 500) % 2 != 0)
            {
                return;
            }

            int lineIndex = ResolveCursorLineIndex(lines, _cursorPosition);
            if (lineIndex < startLine || lineIndex >= startLine + maxLines)
            {
                return;
            }

            TextLineLayout line = lines[lineIndex];
            int relativeIndex = Math.Clamp(_cursorPosition - line.StartIndex, 0, line.Text.Length);
            string cursorText = line.Text[..relativeIndex];
            float cursorX = bounds.X + 2 + (_font.MeasureString(cursorText).X * scale);
            int cursorY = bounds.Y + 2 + ((lineIndex - startLine) * ComposeBodyLineHeight);
            sprite.Draw(_pixel, new Rectangle((int)cursorX, cursorY, 1, 12), new Color(66, 76, 94));
        }

        private void DrawReadOnlyMultilineText(SpriteBatch sprite, string text, Rectangle bounds, float scale, Color color, int maxLines)
        {
            IReadOnlyList<TextLineLayout> lines = BuildTextLines(text ?? string.Empty, DetailBodyClientMaxLineWidth, scale);
            if (lines.Count == 0)
            {
                lines = new[] { new TextLineLayout { StartIndex = 0, EndIndex = 0, Text = string.Empty } };
            }

            int startLine = Math.Clamp(_detailBodyScrollLine, 0, Math.Max(0, lines.Count - maxLines));
            int visibleLineCount = Math.Min(maxLines, lines.Count - startLine);
            float y = bounds.Y + 2;
            for (int index = 0; index < visibleLineCount; index++)
            {
                sprite.DrawString(_font, lines[startLine + index].Text, new Vector2(bounds.X + 2, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += DetailBodyLineHeight;
            }
        }

        private void DrawWrappedText(SpriteBatch sprite, string text, Rectangle bounds, float scale, Color color, int maxLines)
        {
            float y = bounds.Y;
            int lineCount = 0;
            foreach (string line in WrapText(text, bounds.Width - 8, scale))
            {
                if (lineCount >= maxLines)
                {
                    break;
                }

                sprite.DrawString(_font, line, new Vector2(bounds.X + 2, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += 15f;
                lineCount++;
            }
        }

        private void DrawPromptOverlay(SpriteBatch sprite)
        {
            if (_pendingPrompt == PendingPrompt.None || _font == null)
            {
                return;
            }

            GetPromptLayout(out Rectangle promptBox, out Rectangle yesRect, out Rectangle noRect);
            sprite.Draw(_pixel, new Rectangle(Position.X, Position.Y, 734, 526), new Color(0, 0, 0, 74));
            sprite.Draw(_pixel, promptBox, new Color(247, 241, 224, 245));
            sprite.Draw(_pixel, new Rectangle(promptBox.X, promptBox.Y, promptBox.Width, 1), new Color(122, 98, 64));
            sprite.Draw(_pixel, new Rectangle(promptBox.X, promptBox.Bottom - 1, promptBox.Width, 1), new Color(122, 98, 64));
            sprite.Draw(_pixel, new Rectangle(promptBox.X, promptBox.Y, 1, promptBox.Height), new Color(122, 98, 64));
            sprite.Draw(_pixel, new Rectangle(promptBox.Right - 1, promptBox.Y, 1, promptBox.Height), new Color(122, 98, 64));

            DrawString(sprite, "Confirm", promptBox.X + 10, promptBox.Y + 8, new Color(75, 52, 29), 0.44f);
            DrawWrappedText(
                sprite,
                GuildBbsRuntime.GetDeletePostPrompt(),
                new Rectangle(promptBox.X + 10, promptBox.Y + 30, promptBox.Width - 20, 28),
                0.37f,
                new Color(51, 55, 63),
                2);
            DrawPromptButton(sprite, yesRect, "Yes", true);
            DrawPromptButton(sprite, noRect, "No", true);
        }

        private void DrawPromptButton(SpriteBatch sprite, Rectangle bounds, string label, bool enabled)
        {
            sprite.Draw(_pixel, bounds, enabled ? new Color(91, 112, 143, 230) : new Color(141, 146, 154, 160));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(47, 61, 82));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(47, 61, 82));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(47, 61, 82));
            sprite.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(47, 61, 82));
            Vector2 labelSize = _font.MeasureString(label) * 0.38f;
            sprite.DrawString(
                _font,
                label,
                new Vector2(bounds.X + ((bounds.Width - labelSize.X) / 2f), bounds.Y + 3),
                Color.White,
                0f,
                Vector2.Zero,
                0.38f,
                SpriteEffects.None,
                0f);
        }

        private void DrawInlineEmoticon(SpriteBatch sprite, GuildBbsEmoticonSnapshot emoticon, Point position, bool isSmall)
        {
            if (emoticon == null)
            {
                return;
            }

            Texture2D texture = ResolveEmoticonTexture(emoticon);
            if (texture == null)
            {
                return;
            }

            int size = isSmall ? 14 : 18;
            sprite.Draw(texture, new Rectangle(position.X, position.Y, size, size), Color.White);
        }

        private Texture2D ResolveEmoticonTexture(GuildBbsEmoticonSnapshot emoticon)
        {
            if (emoticon == null)
            {
                return null;
            }

            return emoticon.Kind switch
            {
                GuildBbsEmoticonKind.Basic when emoticon.SlotIndex >= 0 && emoticon.SlotIndex < _basicEmoticonTextures.Length => _basicEmoticonTextures[emoticon.SlotIndex],
                GuildBbsEmoticonKind.Cash when emoticon.SlotIndex >= 0 && emoticon.SlotIndex < _cashEmoticonTextures.Length => _cashEmoticonTextures[emoticon.SlotIndex],
                _ => null
            };
        }

        private void ShowFeedback(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private Rectangle GetNoticeBounds() => new(Position.X + NoticeLeft, Position.Y + NoticeTop, NoticeWidth, NoticeHeight);
        private Rectangle GetThreadListBounds()
        {
            int visibleRowCount = Math.Max(0, _currentSnapshot?.Threads?.Count ?? 0);
            return new Rectangle(Position.X + ThreadListLeft, Position.Y + ThreadListTop, ThreadListWidth, ThreadRowHeight * visibleRowCount);
        }
        private Rectangle GetThreadPageSelectorBounds() => new(Position.X + ThreadPageSelectorLeft, Position.Y + ThreadPageSelectorTop, ThreadPageSelectorWidth, ThreadPageSelectorHeight);
        private Rectangle GetDetailBounds() => new(Position.X + DetailLeft, Position.Y + DetailTop, DetailWidth, DetailHeight);
        private Rectangle GetComposeTitleBounds() => new(Position.X + 449, Position.Y + 30, 256, TitleInputHeight);
        private Rectangle GetComposeBodyBounds() => new(Position.X + 449, Position.Y + 56, 250, 180);
        private Rectangle GetComposeScrollBarBounds() => new(Position.X + 706, Position.Y + 53, _scrollbarSkin?.Width ?? ComposeScrollBarWidth, 187);
        private Rectangle GetDetailBodyBounds() => new(Position.X + 424, Position.Y + 83, 250, 180);
        private Rectangle GetDetailBodyScrollBarBounds() => new(Position.X + 704, Position.Y + 78, _scrollbarSkin?.Width ?? ComposeScrollBarWidth, 190);
        private Rectangle GetCommentPaneBounds() => new(Position.X + 424, Position.Y + 326, 258, 124);
        private Rectangle GetCommentScrollBarBounds() => new(Position.X + 710, Position.Y + 326, _scrollbarSkin?.Width ?? CommentScrollBarWidth, 125);
        private Rectangle GetReplyInputBounds() => new(Position.X + 424, Position.Y + 459, 256, 16);
        private Rectangle GetBasicEmoticonBounds(int index) => new(Position.X + 426 + (index * BasicEmoticonSpacing), Position.Y + 246, 20, 20);
        private Rectangle GetCashEmoticonBounds(int index) => new(Position.X + 495 + (index * CashEmoticonSpacing), Position.Y + 246, 20, 20);
        private Rectangle GetCashEmoticonRowBounds() => new(Position.X + 495, Position.Y + 246, (VisibleCashEmoticonCount * CashEmoticonSpacing), CashEmoticonSize);
        private Rectangle GetComposeScrollBarPrevBounds() => BuildScrollBarPrevBounds(GetComposeScrollBarBounds());
        private Rectangle GetComposeScrollBarNextBounds() => BuildScrollBarNextBounds(GetComposeScrollBarBounds());
        private Rectangle GetDetailBodyScrollBarPrevBounds() => BuildScrollBarPrevBounds(GetDetailBodyScrollBarBounds());
        private Rectangle GetDetailBodyScrollBarNextBounds() => BuildScrollBarNextBounds(GetDetailBodyScrollBarBounds());
        private Rectangle GetCommentScrollBarPrevBounds() => BuildScrollBarPrevBounds(GetCommentScrollBarBounds());
        private Rectangle GetCommentScrollBarNextBounds() => BuildScrollBarNextBounds(GetCommentScrollBarBounds());

        private void HandlePromptMouseClick(Point mousePosition)
        {
            GetPromptLayout(out Rectangle promptBox, out Rectangle yesRect, out Rectangle noRect);
            if (yesRect.Contains(mousePosition))
            {
                ConfirmPrompt();
            }
            else if (noRect.Contains(mousePosition) || !promptBox.Contains(mousePosition))
            {
                CancelPrompt();
            }
        }

        private void GetPromptLayout(out Rectangle promptBox, out Rectangle yesRect, out Rectangle noRect)
        {
            int promptX = Position.X + ((734 - PromptBoxWidth) / 2);
            int promptY = Position.Y + ((526 - PromptBoxHeight) / 2);
            promptBox = new Rectangle(promptX, promptY, PromptBoxWidth, PromptBoxHeight);
            yesRect = new Rectangle(promptBox.X + 26, promptBox.Bottom - 30, PromptButtonWidth, PromptButtonHeight);
            noRect = new Rectangle(promptBox.Right - 26 - PromptButtonWidth, promptBox.Bottom - 30, PromptButtonWidth, PromptButtonHeight);
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

        private string TruncateToWidth(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string value = text;
            while (value.Length > 0 && (_font.MeasureString(value).X * scale) > maxWidth)
            {
                value = value[..^1];
            }

            return value;
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

        private static bool IsControlKey(Keys key)
        {
            return key == Keys.Enter
                || key == Keys.Escape
                || key == Keys.Back
                || key == Keys.Delete
                || key == Keys.Left
                || key == Keys.Right
                || key == Keys.Up
                || key == Keys.Down
                || key == Keys.Home
                || key == Keys.End
                || key == Keys.Tab
                || key == Keys.LeftShift
                || key == Keys.RightShift
                || key == Keys.LeftControl
                || key == Keys.RightControl
                || key == Keys.LeftAlt
                || key == Keys.RightAlt;
        }

        private bool ShouldRepeatKey(Keys key, int tickCount)
        {
            if (_lastHeldKey != key)
            {
                return false;
            }

            return tickCount - _keyHoldStartTime >= KeyRepeatDelayMs
                && tickCount - _lastKeyRepeatTime >= KeyRepeatRateMs;
        }

        private void ResetKeyRepeat()
        {
            _lastHeldKey = Keys.None;
            _keyHoldStartTime = 0;
            _lastKeyRepeatTime = 0;
        }

        private static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpperInvariant(c) : c;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                const string normal = "0123456789";
                const string shifted = ")!@#$%^&*(";
                int index = key - Keys.D0;
                return shift ? shifted[index] : normal[index];
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return key switch
            {
                Keys.Space => ' ',
                Keys.OemComma => shift ? '<' : ',',
                Keys.OemPeriod => shift ? '>' : '.',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.OemTilde => shift ? '~' : '`',
                _ => null
            };
        }

        private static Color ResolveGuildMarkColor(string guildName)
        {
            int seed = string.IsNullOrWhiteSpace(guildName) ? 0 : guildName.Aggregate(17, (current, ch) => (current * 31) + ch);
            byte red = (byte)(70 + Math.Abs(seed % 120));
            byte green = (byte)(70 + Math.Abs((seed / 7) % 120));
            byte blue = (byte)(90 + Math.Abs((seed / 13) % 120));
            return new Color(red, green, blue);
        }

        private bool TryHandleNoticeSelection(GuildBbsSnapshot snapshot, Point mousePosition)
        {
            GuildBbsThreadEntrySnapshot noticeThread = snapshot.NoticeThread;
            if (noticeThread == null || !GetNoticeBounds().Contains(mousePosition))
            {
                return false;
            }

            _selectThreadHandler?.Invoke(noticeThread.ThreadId);
            DeactivateInput(clearText: true);
            return true;
        }

        private bool TryHandleThreadPageSelectorClick(GuildBbsSnapshot snapshot, Point mousePosition)
        {
            Rectangle bounds = GetThreadPageSelectorBounds();
            if (!bounds.Contains(mousePosition) || snapshot.ThreadPageCount <= 1)
            {
                return false;
            }

            int visiblePageCount = Math.Min(ThreadPageSelectorVisibleCount, snapshot.ThreadPageCount);
            int startPageIndex = ResolveThreadPageSelectorStart(snapshot.ThreadPageIndex, snapshot.ThreadPageCount, visiblePageCount);
            int slotWidth = Math.Max(1, bounds.Width / ThreadPageSelectorVisibleCount);
            int clickedSlot = Math.Clamp((mousePosition.X - bounds.X) / slotWidth, 0, ThreadPageSelectorVisibleCount - 1);
            int pageIndex = startPageIndex + clickedSlot;
            if (pageIndex >= snapshot.ThreadPageCount)
            {
                return false;
            }

            ShowFeedback(_setThreadPageHandler?.Invoke(pageIndex));
            return true;
        }

        private bool TryHandleComposeScrollClick(GuildBbsSnapshot snapshot, Point mousePosition)
        {
            Rectangle scrollBounds = GetComposeScrollBarBounds();
            if (!scrollBounds.Contains(mousePosition))
            {
                return false;
            }

            ScrollMetrics metrics = BuildComposeScrollMetrics(snapshot.Compose?.Body, GetComposeBodyBounds());
            if (metrics.MaxScrollLine <= 0)
            {
                return true;
            }

            if (GetComposeScrollBarPrevBounds().Contains(mousePosition))
            {
                ScrollComposeBody(-1);
                return true;
            }

            if (GetComposeScrollBarNextBounds().Contains(mousePosition))
            {
                ScrollComposeBody(1);
                return true;
            }

            if (metrics.ThumbBounds.Contains(mousePosition))
            {
                _isDraggingComposeScrollBar = true;
                _composeScrollDragOffsetY = mousePosition.Y - metrics.ThumbBounds.Y;
                return true;
            }

            if (mousePosition.Y < metrics.ThumbBounds.Top)
            {
                ScrollComposeBody(-ComposeBodyVisibleLineCount);
                return true;
            }

            if (mousePosition.Y > metrics.ThumbBounds.Bottom)
            {
                ScrollComposeBody(ComposeBodyVisibleLineCount);
                return true;
            }

            float relative = (mousePosition.Y - scrollBounds.Y) / (float)Math.Max(1, scrollBounds.Height - metrics.ThumbBounds.Height);
            _composeBodyScrollLine = Math.Clamp((int)Math.Round(relative * metrics.MaxScrollLine), 0, metrics.MaxScrollLine);
            return true;
        }

        private bool TryHandleDetailBodyScrollClick(GuildBbsSnapshot snapshot, Point mousePosition)
        {
            if (snapshot.SelectedThread == null)
            {
                return false;
            }

            Rectangle scrollBounds = GetDetailBodyScrollBarBounds();
            if (!scrollBounds.Contains(mousePosition))
            {
                return false;
            }

            ScrollMetrics metrics = BuildDetailBodyScrollMetrics(snapshot.SelectedThread.Body);
            if (metrics.MaxScrollLine <= 0)
            {
                return true;
            }

            if (GetDetailBodyScrollBarPrevBounds().Contains(mousePosition))
            {
                ScrollDetailBody(-1);
                return true;
            }

            if (GetDetailBodyScrollBarNextBounds().Contains(mousePosition))
            {
                ScrollDetailBody(1);
                return true;
            }

            if (metrics.ThumbBounds.Contains(mousePosition))
            {
                _isDraggingDetailBodyScrollBar = true;
                _detailBodyScrollDragOffsetY = mousePosition.Y - metrics.ThumbBounds.Y;
                return true;
            }

            if (mousePosition.Y < metrics.ThumbBounds.Top)
            {
                ScrollDetailBody(-DetailBodyVisibleLineCount);
                return true;
            }

            if (mousePosition.Y > metrics.ThumbBounds.Bottom)
            {
                ScrollDetailBody(DetailBodyVisibleLineCount);
                return true;
            }

            float relative = (mousePosition.Y - scrollBounds.Y) / (float)Math.Max(1, scrollBounds.Height - metrics.ThumbBounds.Height);
            _detailBodyScrollLine = Math.Clamp((int)Math.Round(relative * metrics.MaxScrollLine), 0, metrics.MaxScrollLine);
            return true;
        }

        private bool TryHandleCommentScrollClick(GuildBbsSnapshot snapshot, Point mousePosition)
        {
            if (snapshot.SelectedThread == null)
            {
                return false;
            }

            Rectangle scrollBounds = GetCommentScrollBarBounds();
            if (!scrollBounds.Contains(mousePosition))
            {
                return false;
            }

            ScrollMetrics metrics = BuildCommentScrollMetrics(snapshot.SelectedThread);
            if (metrics.MaxScrollLine <= 0)
            {
                return true;
            }

            if (GetCommentScrollBarPrevBounds().Contains(mousePosition))
            {
                ShowFeedback(_moveCommentPageHandler?.Invoke(-1));
                return true;
            }

            if (GetCommentScrollBarNextBounds().Contains(mousePosition))
            {
                ShowFeedback(_moveCommentPageHandler?.Invoke(1));
                return true;
            }

            if (metrics.ThumbBounds.Contains(mousePosition))
            {
                _isDraggingCommentScrollBar = true;
                _commentScrollDragOffsetY = mousePosition.Y - metrics.ThumbBounds.Y;
                return true;
            }

            if (mousePosition.Y < metrics.ThumbBounds.Top)
            {
                ShowFeedback(_moveCommentPageHandler?.Invoke(-1));
                return true;
            }

            if (mousePosition.Y > metrics.ThumbBounds.Bottom)
            {
                ShowFeedback(_moveCommentPageHandler?.Invoke(1));
                return true;
            }

            return true;
        }

        private void ScrollComposeBody(int deltaLines)
        {
            ScrollMetrics metrics = BuildComposeScrollMetrics(_inputBuffer.ToString(), GetComposeBodyBounds());
            _composeBodyScrollLine = Math.Clamp(_composeBodyScrollLine + deltaLines, 0, metrics.MaxScrollLine);
        }

        private void ScrollDetailBody(int deltaLines)
        {
            ScrollMetrics metrics = BuildDetailBodyScrollMetrics(_currentSnapshot?.SelectedThread?.Body);
            _detailBodyScrollLine = Math.Clamp(_detailBodyScrollLine + deltaLines, 0, metrics.MaxScrollLine);
        }

        private void HandleComposeScrollDrag(GuildBbsSnapshot snapshot, MouseState mouseState)
        {
            if (!snapshot.IsWriteMode || snapshot.Compose == null)
            {
                _isDraggingComposeScrollBar = false;
                return;
            }

            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TryHandleComposeScrollClick(snapshot, mouseState.Position);
            }

            if (!_isDraggingComposeScrollBar || mouseState.LeftButton != ButtonState.Pressed)
            {
                return;
            }

            ScrollMetrics metrics = BuildComposeScrollMetrics(snapshot.Compose.Body, GetComposeBodyBounds());
            if (metrics.MaxScrollLine <= 0)
            {
                _isDraggingComposeScrollBar = false;
                return;
            }

            Rectangle trackBounds = metrics.TrackBounds;
            int thumbHeight = metrics.ThumbBounds.Height;
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int thumbTop = Math.Clamp(mouseState.Y - _composeScrollDragOffsetY, trackBounds.Y, trackBounds.Bottom - thumbHeight);
            float relative = (thumbTop - trackBounds.Y) / (float)travel;
            _composeBodyScrollLine = Math.Clamp((int)Math.Round(relative * metrics.MaxScrollLine), 0, metrics.MaxScrollLine);
        }

        private void HandleDetailBodyScrollDrag(GuildBbsSnapshot snapshot, MouseState mouseState)
        {
            if (snapshot.IsWriteMode || snapshot.SelectedThread == null)
            {
                _isDraggingDetailBodyScrollBar = false;
                return;
            }

            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TryHandleDetailBodyScrollClick(snapshot, mouseState.Position);
            }

            if (!_isDraggingDetailBodyScrollBar || mouseState.LeftButton != ButtonState.Pressed)
            {
                return;
            }

            ScrollMetrics metrics = BuildDetailBodyScrollMetrics(snapshot.SelectedThread.Body);
            if (metrics.MaxScrollLine <= 0)
            {
                _isDraggingDetailBodyScrollBar = false;
                return;
            }

            Rectangle trackBounds = metrics.TrackBounds;
            int thumbHeight = metrics.ThumbBounds.Height;
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int thumbTop = Math.Clamp(mouseState.Y - _detailBodyScrollDragOffsetY, trackBounds.Y, trackBounds.Bottom - thumbHeight);
            float relative = (thumbTop - trackBounds.Y) / (float)travel;
            _detailBodyScrollLine = Math.Clamp((int)Math.Round(relative * metrics.MaxScrollLine), 0, metrics.MaxScrollLine);
        }

        private void HandleCommentScrollDrag(GuildBbsSnapshot snapshot, MouseState mouseState)
        {
            if (snapshot.IsWriteMode || snapshot.SelectedThread == null)
            {
                _isDraggingCommentScrollBar = false;
                return;
            }

            if (mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released)
            {
                TryHandleCommentScrollClick(snapshot, mouseState.Position);
            }

            if (!_isDraggingCommentScrollBar || mouseState.LeftButton != ButtonState.Pressed)
            {
                return;
            }

            ScrollMetrics metrics = BuildCommentScrollMetrics(snapshot.SelectedThread);
            if (metrics.MaxScrollLine <= 0)
            {
                _isDraggingCommentScrollBar = false;
                return;
            }

            Rectangle trackBounds = metrics.TrackBounds;
            int thumbHeight = metrics.ThumbBounds.Height;
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int thumbTop = Math.Clamp(mouseState.Y - _commentScrollDragOffsetY, trackBounds.Y, trackBounds.Bottom - thumbHeight);
            float relative = (thumbTop - trackBounds.Y) / (float)travel;
            int targetPageIndex = Math.Clamp((int)Math.Round(relative * metrics.MaxScrollLine), 0, metrics.MaxScrollLine);
            ShowFeedback(_setCommentPageHandler?.Invoke(targetPageIndex));
        }

        private void EnsureComposeCursorVisible()
        {
            if (_activeInputTarget != InputTarget.ComposeBody)
            {
                return;
            }

            IReadOnlyList<TextLineLayout> lines = BuildComposeBodyTextLines(_inputBuffer.ToString());
            int lineIndex = ResolveCursorLineIndex(lines, _cursorPosition);
            if (lineIndex < 0)
            {
                _composeBodyScrollLine = 0;
                return;
            }

            if (lineIndex < _composeBodyScrollLine)
            {
                _composeBodyScrollLine = lineIndex;
                return;
            }

            int maxVisibleLine = _composeBodyScrollLine + ComposeBodyVisibleLineCount - 1;
            if (lineIndex > maxVisibleLine)
            {
                _composeBodyScrollLine = lineIndex - ComposeBodyVisibleLineCount + 1;
            }
        }

        private ScrollMetrics BuildComposeScrollMetrics(string text, Rectangle bodyBounds)
        {
            IReadOnlyList<TextLineLayout> lines = BuildComposeBodyTextLines(text ?? string.Empty);
            int totalLines = Math.Max(1, lines.Count);
            int visibleLines = Math.Min(ComposeBodyVisibleLineCount, totalLines);
            int maxScrollLine = Math.Max(0, totalLines - visibleLines);
            Rectangle trackBounds = BuildScrollBarTrackBounds(GetComposeScrollBarBounds());
            if (maxScrollLine <= 0)
            {
                return new ScrollMetrics
                {
                    TotalLines = totalLines,
                    VisibleLines = visibleLines,
                    MaxScrollLine = 0,
                    TrackBounds = trackBounds,
                    ThumbBounds = trackBounds
                };
            }

            int thumbHeight = Math.Max(18, (int)Math.Round(trackBounds.Height * (visibleLines / (float)totalLines)));
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int thumbTop = trackBounds.Y + (int)Math.Round((_composeBodyScrollLine / (float)maxScrollLine) * travel);
            return new ScrollMetrics
            {
                TotalLines = totalLines,
                VisibleLines = visibleLines,
                MaxScrollLine = maxScrollLine,
                TrackBounds = trackBounds,
                ThumbBounds = new Rectangle(trackBounds.X, thumbTop, trackBounds.Width, thumbHeight)
            };
        }

        private ScrollMetrics BuildDetailBodyScrollMetrics(string text)
        {
            IReadOnlyList<TextLineLayout> lines = BuildTextLines(text ?? string.Empty, DetailBodyClientMaxLineWidth, 0.37f);
            int totalLines = Math.Max(1, lines.Count);
            int visibleLines = Math.Min(DetailBodyVisibleLineCount, totalLines);
            int maxScrollLine = Math.Max(0, totalLines - visibleLines);
            Rectangle trackBounds = BuildScrollBarTrackBounds(GetDetailBodyScrollBarBounds());
            if (maxScrollLine <= 0)
            {
                return new ScrollMetrics
                {
                    TotalLines = totalLines,
                    VisibleLines = visibleLines,
                    MaxScrollLine = 0,
                    TrackBounds = trackBounds,
                    ThumbBounds = trackBounds
                };
            }

            int thumbHeight = Math.Max(18, (int)Math.Round(trackBounds.Height * (visibleLines / (float)totalLines)));
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int thumbTop = trackBounds.Y + (int)Math.Round((_detailBodyScrollLine / (float)maxScrollLine) * travel);
            return new ScrollMetrics
            {
                TotalLines = totalLines,
                VisibleLines = visibleLines,
                MaxScrollLine = maxScrollLine,
                TrackBounds = trackBounds,
                ThumbBounds = new Rectangle(trackBounds.X, thumbTop, trackBounds.Width, thumbHeight)
            };
        }

        private ScrollMetrics BuildCommentScrollMetrics(GuildBbsThreadSnapshot thread)
        {
            int totalPages = Math.Max(1, thread?.CommentPageCount ?? 1);
            Rectangle trackBounds = BuildScrollBarTrackBounds(GetCommentScrollBarBounds());
            if (totalPages <= 1)
            {
                return new ScrollMetrics
                {
                    TotalLines = totalPages,
                    VisibleLines = 1,
                    MaxScrollLine = 0,
                    TrackBounds = trackBounds,
                    ThumbBounds = trackBounds
                };
            }

            int thumbHeight = Math.Max(18, (int)Math.Round(trackBounds.Height / (float)totalPages));
            int travel = Math.Max(1, trackBounds.Height - thumbHeight);
            int pageIndex = Math.Clamp(thread?.CommentPageIndex ?? 0, 0, totalPages - 1);
            int thumbTop = trackBounds.Y + (int)Math.Round((pageIndex / (float)(totalPages - 1)) * travel);
            return new ScrollMetrics
            {
                TotalLines = totalPages,
                VisibleLines = 1,
                MaxScrollLine = totalPages - 1,
                TrackBounds = trackBounds,
                ThumbBounds = new Rectangle(trackBounds.X, thumbTop, trackBounds.Width, thumbHeight)
            };
        }

        private static int GetCashEmoticonGlobalIndex(int cashPageIndex, int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= VisibleCashEmoticonCount)
            {
                return -1;
            }

            return (Math.Max(0, cashPageIndex) * VisibleCashEmoticonCount) + displayIndex;
        }

        private static int ResolveThreadPageSelectorStart(int currentPageIndex, int totalPageCount, int visiblePageCount)
        {
            if (totalPageCount <= 0 || visiblePageCount <= 0)
            {
                return 0;
            }

            int maxStart = Math.Max(0, totalPageCount - visiblePageCount);
            int centeredStart = currentPageIndex - (visiblePageCount / 2);
            return Math.Clamp(centeredStart, 0, maxStart);
        }

        private Rectangle BuildScrollBarPrevBounds(Rectangle scrollBounds)
        {
            int height = _scrollbarSkin?.PrevHeight ?? 12;
            return new Rectangle(scrollBounds.X, scrollBounds.Y, scrollBounds.Width, Math.Min(scrollBounds.Height, height));
        }

        private Rectangle BuildScrollBarNextBounds(Rectangle scrollBounds)
        {
            int height = _scrollbarSkin?.NextHeight ?? 12;
            return new Rectangle(scrollBounds.X, scrollBounds.Bottom - Math.Min(scrollBounds.Height, height), scrollBounds.Width, Math.Min(scrollBounds.Height, height));
        }

        private Rectangle BuildScrollBarTrackBounds(Rectangle scrollBounds)
        {
            Rectangle prevBounds = BuildScrollBarPrevBounds(scrollBounds);
            Rectangle nextBounds = BuildScrollBarNextBounds(scrollBounds);
            return new Rectangle(scrollBounds.X, prevBounds.Bottom, scrollBounds.Width, Math.Max(0, nextBounds.Y - prevBounds.Bottom));
        }

        private void DrawScrollbarTrack(SpriteBatch sprite, Rectangle trackBounds)
        {
            if (_scrollbarSkin?.Base == null)
            {
                return;
            }

            int tileY = trackBounds.Y;
            while (tileY < trackBounds.Bottom)
            {
                int tileHeight = Math.Min(_scrollbarSkin.Base.Height, trackBounds.Bottom - tileY);
                Rectangle destination = new Rectangle(trackBounds.X, tileY, _scrollbarSkin.Base.Width, tileHeight);
                Rectangle? source = tileHeight == _scrollbarSkin.Base.Height
                    ? null
                    : new Rectangle(0, 0, _scrollbarSkin.Base.Width, tileHeight);
                sprite.Draw(_scrollbarSkin.Base, destination, source, Color.White);
                tileY += tileHeight;
            }
        }

        private void DrawScrollbarArrow(SpriteBatch sprite, Rectangle bounds, Texture2D[] states, Texture2D disabledTexture, bool enabled)
        {
            Texture2D texture = enabled
                ? ResolveScrollbarStateTexture(states, true, bounds.Contains(Mouse.GetState().Position), false)
                : disabledTexture ?? states?.FirstOrDefault();
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            }
        }

        private static Texture2D ResolveScrollbarStateTexture(Texture2D[] states, bool enabled, bool hovered, bool pressed)
        {
            if (!enabled || states == null || states.Length == 0)
            {
                return states?.FirstOrDefault();
            }

            if (pressed && states.Length > 2 && states[2] != null)
            {
                return states[2];
            }

            if (hovered && states.Length > 1 && states[1] != null)
            {
                return states[1];
            }

            return states[0];
        }

        private void SetCursorFromPoint(InputTarget target, GuildBbsSnapshot snapshot, Point mousePosition)
        {
            string source = target switch
            {
                InputTarget.ComposeTitle => snapshot.Compose?.Title ?? string.Empty,
                InputTarget.ComposeBody => snapshot.Compose?.Body ?? string.Empty,
                InputTarget.ReplyBody => snapshot.ReplyDraft?.Body ?? string.Empty,
                _ => string.Empty
            };

            _cursorPosition = target == InputTarget.ComposeBody
                ? ResolveMultilineCursorPosition(source, GetComposeBodyBounds(), mousePosition, 0.37f, ComposeBodyClientMaxLineWidth)
                : ResolveSingleLineCursorPosition(
                    source,
                    target == InputTarget.ComposeTitle ? GetComposeTitleBounds() : GetReplyInputBounds(),
                    mousePosition,
                    target == InputTarget.ComposeTitle ? 0.46f : 0.39f);
            _cursorBlinkTimer = Environment.TickCount;
        }

        private int ResolveSingleLineCursorPosition(string text, Rectangle bounds, Point mousePosition, float scale)
        {
            string source = text ?? string.Empty;
            float relativeX = Math.Max(0f, mousePosition.X - (bounds.X + 4));
            int bestIndex = 0;
            float bestDistance = float.MaxValue;

            for (int index = 0; index <= source.Length; index++)
            {
                float width = _font.MeasureString(source[..index]).X * scale;
                float distance = Math.Abs(width - relativeX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private int ResolveMultilineCursorPosition(string text, Rectangle bounds, Point mousePosition, float scale, float? maxWidthOverride = null)
        {
            IReadOnlyList<TextLineLayout> lines = BuildTextLines(text ?? string.Empty, maxWidthOverride ?? (bounds.Width - 8), scale);
            if (lines.Count == 0)
            {
                return 0;
            }

            int lineIndex = Math.Clamp(_composeBodyScrollLine + ((mousePosition.Y - (bounds.Y + 2)) / ComposeBodyLineHeight), 0, lines.Count - 1);
            TextLineLayout line = lines[lineIndex];
            float relativeX = Math.Max(0f, mousePosition.X - (bounds.X + 2));
            int bestIndex = line.StartIndex;
            float bestDistance = float.MaxValue;

            for (int index = 0; index <= line.Text.Length; index++)
            {
                float width = _font.MeasureString(line.Text[..index]).X * scale;
                float distance = Math.Abs(width - relativeX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = line.StartIndex + index;
                }
            }

            return bestIndex;
        }

        private int ResolveHomeCursorPosition()
        {
            if (_activeInputTarget != InputTarget.ComposeBody)
            {
                return 0;
            }

            IReadOnlyList<TextLineLayout> lines = BuildComposeBodyTextLines(_inputBuffer.ToString());
            int lineIndex = ResolveCursorLineIndex(lines, _cursorPosition);
            return lineIndex >= 0 ? lines[lineIndex].StartIndex : 0;
        }

        private int ResolveEndCursorPosition()
        {
            if (_activeInputTarget != InputTarget.ComposeBody)
            {
                return _inputBuffer.Length;
            }

            IReadOnlyList<TextLineLayout> lines = BuildComposeBodyTextLines(_inputBuffer.ToString());
            int lineIndex = ResolveCursorLineIndex(lines, _cursorPosition);
            return lineIndex >= 0 ? lines[lineIndex].EndIndex : _inputBuffer.Length;
        }

        private void MoveComposeBodyCursorVertical(int delta)
        {
            IReadOnlyList<TextLineLayout> lines = BuildComposeBodyTextLines(_inputBuffer.ToString());
            if (lines.Count == 0)
            {
                _cursorPosition = 0;
                return;
            }

            int currentLineIndex = ResolveCursorLineIndex(lines, _cursorPosition);
            if (currentLineIndex < 0)
            {
                currentLineIndex = 0;
            }

            int targetLineIndex = Math.Clamp(currentLineIndex + delta, 0, lines.Count - 1);
            if (targetLineIndex == currentLineIndex)
            {
                return;
            }

            TextLineLayout currentLine = lines[currentLineIndex];
            TextLineLayout targetLine = lines[targetLineIndex];
            int column = Math.Clamp(_cursorPosition - currentLine.StartIndex, 0, currentLine.Text.Length);
            _cursorPosition = Math.Clamp(targetLine.StartIndex + column, targetLine.StartIndex, targetLine.EndIndex);
            EnsureComposeCursorVisible();
        }

        private IReadOnlyList<TextLineLayout> BuildComposeBodyTextLines(string text)
        {
            return BuildTextLines(text, ComposeBodyClientMaxLineWidth, 0.37f);
        }

        private bool CanAcceptComposeBodyText(string candidate)
        {
            return candidate != null;
        }

        private IReadOnlyList<TextLineLayout> BuildTextLines(string text, float maxWidth, float scale)
        {
            string source = text?.Replace("\r", string.Empty) ?? string.Empty;
            var lines = new List<TextLineLayout>();

            if (source.Length == 0)
            {
                lines.Add(new TextLineLayout());
                return lines;
            }

            int index = 0;
            while (index < source.Length)
            {
                if (source[index] == '\n')
                {
                    lines.Add(new TextLineLayout
                    {
                        StartIndex = index,
                        EndIndex = index,
                        Text = string.Empty
                    });
                    index++;
                    continue;
                }

                int startIndex = index;
                string currentText = string.Empty;
                while (index < source.Length && source[index] != '\n')
                {
                    string candidate = currentText + source[index];
                    if (currentText.Length > 0 && (_font.MeasureString(candidate).X * scale) > maxWidth)
                    {
                        break;
                    }

                    currentText = candidate;
                    index++;
                }

                if (currentText.Length == 0 && index < source.Length)
                {
                    currentText = source[index].ToString();
                    index++;
                }

                lines.Add(new TextLineLayout
                {
                    StartIndex = startIndex,
                    EndIndex = startIndex + currentText.Length,
                    Text = currentText
                });
            }

            if (source[^1] == '\n')
            {
                lines.Add(new TextLineLayout
                {
                    StartIndex = source.Length,
                    EndIndex = source.Length,
                    Text = string.Empty
                });
            }

            return lines;
        }

        private static int ResolveCursorLineIndex(IReadOnlyList<TextLineLayout> lines, int cursorPosition)
        {
            if (lines == null || lines.Count == 0)
            {
                return -1;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                TextLineLayout line = lines[i];
                if (cursorPosition >= line.StartIndex && cursorPosition <= line.EndIndex)
                {
                    return i;
                }
            }

            return cursorPosition <= lines[0].StartIndex ? 0 : lines.Count - 1;
        }

        internal static IReadOnlyList<int> BuildVisibleCashEmoticonSlots(IReadOnlyList<bool> cashOwnership, int cashPageIndex, int visibleSlotCount)
        {
            if (cashOwnership == null || cashOwnership.Count == 0 || visibleSlotCount <= 0)
            {
                return Array.Empty<int>();
            }

            List<int> ownedSlots = new();
            for (int slotIndex = 0; slotIndex < cashOwnership.Count; slotIndex++)
            {
                if (cashOwnership[slotIndex])
                {
                    ownedSlots.Add(slotIndex);
                }
            }

            if (ownedSlots.Count == 0)
            {
                return Array.Empty<int>();
            }

            int maxPageIndex = Math.Max(0, (ownedSlots.Count - 1) / visibleSlotCount);
            int resolvedPageIndex = Math.Clamp(cashPageIndex, 0, maxPageIndex);
            return ownedSlots
                .Skip(resolvedPageIndex * visibleSlotCount)
                .Take(visibleSlotCount)
                .ToArray();
        }
    }
}
