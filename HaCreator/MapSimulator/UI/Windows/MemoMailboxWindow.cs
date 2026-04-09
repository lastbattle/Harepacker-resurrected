using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Text;

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
        private const int RecipientMaxLength = 13;
        private const int MesoMaxDigits = 10;
        private const int KeyRepeatDelayMs = 400;
        private const int KeyRepeatRateMs = 35;

        private readonly string _windowName;
        private readonly Texture2D _unreadIconTexture;
        private readonly Texture2D _readIconTexture;
        private readonly Texture2D[] _enabledTabs;
        private readonly Texture2D[] _disabledTabs;
        private readonly IDXObject _tabReceiveBase;
        private readonly Point _tabReceiveBaseOffset;
        private readonly IDXObject _tabReceiveInfoText;
        private readonly Point _tabReceiveInfoTextOffset;
        private readonly IDXObject _tabSendBase;
        private readonly Point _tabSendBaseOffset;
        private readonly IDXObject _tabQuickBase;
        private readonly Point _tabQuickBaseOffset;
        private readonly IDXObject _tabQuickHint;
        private readonly Point _tabQuickHintOffset;
        private readonly VerticalScrollbarSkin _receiveScrollbarSkin;
        private readonly Texture2D _pixel;
        private readonly List<RowLayout> _rowLayouts = new();
        private readonly List<ParcelDialogTab> _tabOrder = new();
        private readonly List<Rectangle> _tabBounds = new();
        private readonly StringBuilder _inputBuffer = new();

        private SpriteFont _font;
        private Func<MemoMailboxSnapshot> _snapshotProvider;
        private Func<MemoMailboxDraftSnapshot> _draftSnapshotProvider;
        private Action<ParcelDialogTab> _tabSelected;
        private Action<int> _openMemoRequested;
        private Action<int> _deleteMemoRequested;
        private Action<int> _attachmentRequested;
        private Func<string> _dispatchRequested;
        private Action<bool> _taxInfoRequested;
        private Action<ParcelDialogTab> _mesoRequested;
        private Action<ParcelDialogTab> _draftAttachmentRequested;
        private Action<string> _recipientChanged;
        private Action<string> _bodyChanged;
        private Action<int> _mesoChanged;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private int _selectedMemoId = -1;
        private int _openedMemoId = -1;
        private int _scrollOffset;
        private int _cursorPosition;
        private int _cursorBlinkTimer;
        private int _keyHoldStartTime;
        private int _lastKeyRepeatTime;
        private Keys _lastHeldKey = Keys.None;
        private string _compositionText = string.Empty;
        private bool _isDraggingReceiveScrollThumb;
        private int _receiveScrollThumbDragOffsetY;
        private MemoMailboxSnapshot _currentSnapshot = new();
        private MemoMailboxDraftSnapshot _currentDraftSnapshot = new();
        private UIObject _receiveGetButton;
        private UIObject _receiveDeleteButton;
        private UIObject _sendSendButton;
        private UIObject _sendMesoButton;
        private UIObject _sendTaxOpenButton;
        private UIObject _sendTaxCloseButton;
        private UIObject _quickSendButton;
        private UIObject _quickMesoButton;
        private UIObject _quickTaxOpenButton;
        private UIObject _quickTaxCloseButton;
        private ComposeInputField _activeInputField;

        internal Func<InventoryType, int, InventorySlotData, bool> InventoryDropRequested { private get; set; }
        internal Func<InventoryType, int, InventorySlotData, bool> InventoryPickRequested { private get; set; }

        private enum ComposeInputField
        {
            None,
            SendRecipient,
            SendBody,
            SendMeso,
            QuickRecipient,
            QuickBody,
            QuickMeso
        }

        private sealed class RowLayout
        {
            public int MemoId { get; init; }
            public Rectangle Bounds { get; init; }
        }

        private sealed class TextLineLayout
        {
            public int StartIndex { get; init; }
            public int EndIndex { get; init; }
            public string Text { get; init; } = string.Empty;
        }

        public MemoMailboxWindow(
            IDXObject frame,
            string windowName,
            GraphicsDevice device,
            Texture2D unreadIconTexture,
            Texture2D readIconTexture,
            Texture2D[] enabledTabs,
            Texture2D[] disabledTabs,
            IDXObject tabReceiveBase,
            Point tabReceiveBaseOffset,
            IDXObject tabReceiveInfoText,
            Point tabReceiveInfoTextOffset,
            IDXObject tabSendBase,
            Point tabSendBaseOffset,
            IDXObject tabQuickBase,
            Point tabQuickBaseOffset,
            IDXObject tabQuickHint,
            Point tabQuickHintOffset,
            VerticalScrollbarSkin receiveScrollbarSkin)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _unreadIconTexture = unreadIconTexture;
            _readIconTexture = readIconTexture;
            _enabledTabs = enabledTabs ?? Array.Empty<Texture2D>();
            _disabledTabs = disabledTabs ?? Array.Empty<Texture2D>();
            _tabReceiveBase = tabReceiveBase;
            _tabReceiveBaseOffset = tabReceiveBaseOffset;
            _tabReceiveInfoText = tabReceiveInfoText;
            _tabReceiveInfoTextOffset = tabReceiveInfoTextOffset;
            _tabSendBase = tabSendBase;
            _tabSendBaseOffset = tabSendBaseOffset;
            _tabQuickBase = tabQuickBase;
            _tabQuickBaseOffset = tabQuickBaseOffset;
            _tabQuickHint = tabQuickHint;
            _tabQuickHintOffset = tabQuickHintOffset;
            _receiveScrollbarSkin = receiveScrollbarSkin;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => _windowName;
        public override bool CapturesKeyboardInput => IsVisible && _activeInputField != ComposeInputField.None;

        public override void Hide()
        {
            base.Hide();
            DeactivateInput();
            _isDraggingReceiveScrollThumb = false;
        }

        internal bool TryHandleInventoryDrop(
            int mouseX,
            int mouseY,
            InventoryType sourceInventoryType,
            int sourceSlotIndex,
            InventorySlotData draggedSlotData)
        {
            if (!IsVisible
                || draggedSlotData == null
                || draggedSlotData.IsDisabled)
            {
                return false;
            }

            MemoMailboxSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            if (snapshot.ActiveTab == ParcelDialogTab.Receive)
            {
                return false;
            }

            Rectangle contentBounds = GetContentBounds();
            bool quickMode = snapshot.ActiveTab == ParcelDialogTab.QuickSend;
            Rectangle iconBounds = GetComposeItemIconBounds(contentBounds, quickMode);
            Rectangle textBounds = GetComposeItemTextBounds(contentBounds, quickMode);
            if (!iconBounds.Contains(mouseX, mouseY) && !textBounds.Contains(mouseX, mouseY))
            {
                return false;
            }

            return InventoryDropRequested?.Invoke(sourceInventoryType, sourceSlotIndex, draggedSlotData.Clone()) == true;
        }

        internal bool TryHandleInventoryPick(
            InventoryType sourceInventoryType,
            int sourceSlotIndex,
            InventorySlotData pickedSlotData)
        {
            if (!IsVisible || pickedSlotData == null || pickedSlotData.IsDisabled)
            {
                return false;
            }

            MemoMailboxDraftSnapshot draftSnapshot = _currentDraftSnapshot ?? RefreshDraftSnapshot();
            if (draftSnapshot.ActiveTab != ParcelDialogTab.Send || !draftSnapshot.AwaitingItemSelection)
            {
                return false;
            }

            return InventoryPickRequested?.Invoke(sourceInventoryType, sourceSlotIndex, pickedSlotData.Clone()) == true;
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        internal void SetSnapshotProvider(Func<MemoMailboxSnapshot> snapshotProvider, Func<MemoMailboxDraftSnapshot> draftSnapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            _draftSnapshotProvider = draftSnapshotProvider;
            _currentSnapshot = RefreshSnapshot();
            _currentDraftSnapshot = RefreshDraftSnapshot();
        }

        internal void SetActions(
            Action<ParcelDialogTab> tabSelected,
            Action<int> openMemoRequested,
            Action<int> deleteMemoRequested,
            Action<int> attachmentRequested,
            Func<string> dispatchRequested,
            Action<bool> taxInfoRequested,
            Action<ParcelDialogTab> mesoRequested,
            Action<ParcelDialogTab> draftAttachmentRequested,
            Action<string> recipientChanged,
            Action<string> bodyChanged,
            Action<int> mesoChanged)
        {
            _tabSelected = tabSelected;
            _openMemoRequested = openMemoRequested;
            _deleteMemoRequested = deleteMemoRequested;
            _attachmentRequested = attachmentRequested;
            _dispatchRequested = dispatchRequested;
            _taxInfoRequested = taxInfoRequested;
            _mesoRequested = mesoRequested;
            _draftAttachmentRequested = draftAttachmentRequested;
            _recipientChanged = recipientChanged;
            _bodyChanged = bodyChanged;
            _mesoChanged = mesoChanged;
        }

        internal void InitializeReceiveButtons(UIObject getButton, UIObject deleteButton)
        {
            _receiveGetButton = getButton;
            _receiveDeleteButton = deleteButton;
            ConfigureButton(_receiveGetButton, HandleReceiveAttachment);
            ConfigureButton(_receiveDeleteButton, DeleteOpenedMemo);
            UpdateButtonVisibility();
        }

        internal void InitializeSendButtons(UIObject sendButton, UIObject mesoButton, UIObject taxOpenButton, UIObject taxCloseButton)
        {
            _sendSendButton = sendButton;
            _sendMesoButton = mesoButton;
            _sendTaxOpenButton = taxOpenButton;
            _sendTaxCloseButton = taxCloseButton;
            ConfigureButton(_sendSendButton, HandleDispatch);
            ConfigureButton(_sendMesoButton, () => HandleMesoButton(ParcelDialogTab.Send));
            ConfigureButton(_sendTaxOpenButton, () => _taxInfoRequested?.Invoke(true));
            ConfigureButton(_sendTaxCloseButton, () => _taxInfoRequested?.Invoke(false));
            UpdateButtonVisibility();
        }

        internal void InitializeQuickButtons(UIObject sendButton, UIObject mesoButton, UIObject taxOpenButton, UIObject taxCloseButton)
        {
            _quickSendButton = sendButton;
            _quickMesoButton = mesoButton;
            _quickTaxOpenButton = taxOpenButton;
            _quickTaxCloseButton = taxCloseButton;
            ConfigureButton(_quickSendButton, HandleDispatch);
            ConfigureButton(_quickMesoButton, () => HandleMesoButton(ParcelDialogTab.QuickSend));
            ConfigureButton(_quickTaxOpenButton, () => _taxInfoRequested?.Invoke(true));
            ConfigureButton(_quickTaxCloseButton, () => _taxInfoRequested?.Invoke(false));
            UpdateButtonVisibility();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            MemoMailboxSnapshot snapshot = RefreshSnapshot();
            MemoMailboxDraftSnapshot draftSnapshot = RefreshDraftSnapshot();
            EnsureSelection(snapshot);
            ClampScroll(snapshot);

            KeyboardState keyboardState = Keyboard.GetState();
            HandleKeyboardInput(snapshot, draftSnapshot, keyboardState, Environment.TickCount);
            UpdateButtonVisibility();

            MouseState mouseState = Mouse.GetState();
            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            bool leftReleased = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;

            if (snapshot.ActiveTab == ParcelDialogTab.Receive)
            {
                if (leftPressed && ContainsPoint(mouseState.X, mouseState.Y))
                {
                    TryBeginReceiveScrollbarInteraction(snapshot, mouseState.Position);
                }

                UpdateReceiveScrollbarDrag(snapshot, mouseState);

                int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                if (wheelDelta != 0 && ContainsPoint(mouseState.X, mouseState.Y))
                {
                    _scrollOffset -= Math.Sign(wheelDelta);
                    ClampScroll(snapshot);
                }
            }
            else
            {
                _isDraggingReceiveScrollThumb = false;
            }

            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                HandleLeftClick(snapshot, draftSnapshot, mouseState.Position);
            }

            if (mouseState.LeftButton == ButtonState.Released)
            {
                _isDraggingReceiveScrollThumb = false;
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
            _currentDraftSnapshot = draftSnapshot;
        }

        public override void HandleCommittedText(string text)
        {
            if (_activeInputField == ComposeInputField.None || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                if (!TryInsertCharacter(character))
                {
                    continue;
                }

                SyncInputBuffer();
            }
        }

        public override void HandleCompositionText(string text)
        {
            if (_activeInputField == ComposeInputField.None)
            {
                _compositionText = string.Empty;
                return;
            }

            string sanitized = SanitizeCommittedText(text ?? string.Empty);
            if (IsMesoField(_activeInputField))
            {
                sanitized = StripNonDigits(sanitized);
                int availableDigits = Math.Max(0, MesoMaxDigits - _inputBuffer.Length);
                _compositionText = sanitized.Length > availableDigits
                    ? sanitized[..availableDigits]
                    : sanitized;
                return;
            }

            if (IsRecipientField(_activeInputField))
            {
                int availableCharacters = Math.Max(0, RecipientMaxLength - _inputBuffer.Length);
                _compositionText = sanitized.Length > availableCharacters
                    ? sanitized[..availableCharacters]
                    : sanitized;
                return;
            }

            _compositionText = sanitized;
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
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
            MemoMailboxDraftSnapshot draftSnapshot = _currentDraftSnapshot ?? RefreshDraftSnapshot();
            EnsureSelection(snapshot);
            ClampScroll(snapshot);

            DrawLayer(sprite, GetTabBackground(snapshot.ActiveTab), GetTabBackgroundOffset(snapshot.ActiveTab), drawReflectionInfo, skeletonMeshRenderer, gameTime);
            if (snapshot.ActiveTab == ParcelDialogTab.Receive)
            {
                DrawLayer(sprite, _tabReceiveInfoText, _tabReceiveInfoTextOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }
            else if (snapshot.ActiveTab == ParcelDialogTab.QuickSend)
            {
                DrawLayer(sprite, _tabQuickHint, _tabQuickHintOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            }

            DrawTabs(sprite, snapshot.ActiveTab, snapshot.AvailableTabs);
            Rectangle contentBounds = GetContentBounds();

            string title = snapshot.ActiveTab switch
            {
                ParcelDialogTab.Send => "PARCEL DELIVERY",
                ParcelDialogTab.QuickSend => "QUICK DELIVERY",
                _ => $"PARCEL RECEIVE ({snapshot.UnreadCount} unread / {snapshot.ClaimableCount} ready)"
            };
            sprite.DrawString(_font, title, new Vector2(Position.X + 24, Position.Y + 9), Color.White, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);

            switch (snapshot.ActiveTab)
            {
                case ParcelDialogTab.Receive:
                    DrawReceiveTab(sprite, snapshot, contentBounds);
                    break;
                case ParcelDialogTab.QuickSend:
                    DrawComposeTab(sprite, draftSnapshot, contentBounds, true, TickCount);
                    break;
                default:
                    DrawComposeTab(sprite, draftSnapshot, contentBounds, false, TickCount);
                    break;
            }
        }

        private IDXObject GetTabBackground(ParcelDialogTab tab) => tab switch
        {
            ParcelDialogTab.Send => _tabSendBase,
            ParcelDialogTab.QuickSend => _tabQuickBase,
            _ => _tabReceiveBase
        };

        private Point GetTabBackgroundOffset(ParcelDialogTab tab) => tab switch
        {
            ParcelDialogTab.Send => _tabSendBaseOffset,
            ParcelDialogTab.QuickSend => _tabQuickBaseOffset,
            _ => _tabReceiveBaseOffset
        };

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action?.Invoke();
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

        private void DrawTabs(SpriteBatch sprite, ParcelDialogTab activeTab, ParcelDialogTabAvailability availableTabs)
        {
            _tabBounds.Clear();
            _tabOrder.Clear();
            int x = Position.X + 8;
            int y = Position.Y + 9;
            for (int i = 0; i < 3; i++)
            {
                ParcelDialogTab tab = (ParcelDialogTab)i;
                if (!IsTabAvailable(availableTabs, tab))
                {
                    continue;
                }

                Texture2D texture = activeTab == (ParcelDialogTab)i
                    ? GetTabTexture(_enabledTabs, i)
                    : GetTabTexture(_disabledTabs, i);
                if (texture == null)
                {
                    continue;
                }

                Rectangle bounds = new Rectangle(x, y, texture.Width, texture.Height);
                sprite.Draw(texture, bounds, Color.White);
                _tabBounds.Add(bounds);
                _tabOrder.Add(tab);
                x += texture.Width + 1;
            }
        }

        private static Texture2D GetTabTexture(Texture2D[] textures, int index)
        {
            return textures != null && index >= 0 && index < textures.Length
                ? textures[index]
                : null;
        }

        private void DrawReceiveTab(SpriteBatch sprite, MemoMailboxSnapshot snapshot, Rectangle contentBounds)
        {
            _rowLayouts.Clear();

            if (snapshot.Entries.Count == 0)
            {
                sprite.DrawString(_font, "No parcels are waiting in the receive backlog.", new Vector2(contentBounds.X, contentBounds.Y + 4), new Color(59, 70, 84), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
                sprite.DrawString(_font, "Use /memo packet deliver ... to seed packet-shaped parcel rows.", new Vector2(contentBounds.X, contentBounds.Y + 22), new Color(105, 114, 127), 0f, Vector2.Zero, 0.39f, SpriteEffects.None, 0f);
                return;
            }

            int startIndex = Math.Min(_scrollOffset, Math.Max(0, snapshot.Entries.Count - MaxVisibleEntries));
            int visibleCount = Math.Min(MaxVisibleEntries, snapshot.Entries.Count - startIndex);
            for (int index = 0; index < visibleCount; index++)
            {
                MemoMailboxEntrySnapshot memo = snapshot.Entries[startIndex + index];
                Rectangle rowBounds = new Rectangle(contentBounds.X, contentBounds.Y + (index * RowHeight), contentBounds.Width - 18, RowHeight - 2);
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
                if (!string.IsNullOrWhiteSpace(memo.StatusText))
                {
                    sprite.DrawString(_font, Truncate(memo.StatusText, 14), new Vector2(rowBounds.X + 104, rowBounds.Y + 14), new Color(113, 120, 132), 0f, Vector2.Zero, 0.33f, SpriteEffects.None, 0f);
                }

                Vector2 timeSize = _font.MeasureString(memo.DeliveredAtText) * 0.34f;
                sprite.DrawString(_font, memo.DeliveredAtText, new Vector2(rowBounds.Right - timeSize.X - 4, rowBounds.Y + 3), new Color(123, 129, 141), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                sprite.DrawString(_font, Truncate(memo.Preview, 36), new Vector2(rowBounds.Right - 120, rowBounds.Y + 15), new Color(108, 115, 126), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                if (memo.HasAttachment)
                {
                    Color attachmentColor = memo.CanClaimAttachment ? new Color(67, 137, 76) : new Color(129, 136, 145);
                    sprite.DrawString(_font, "PKG", new Vector2(rowBounds.Right - 28, rowBounds.Y + 14), attachmentColor, 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                }
            }

            DrawReceiveScrollbar(sprite, snapshot);

            if (_openedMemoId > 0 && TryGetEntry(snapshot, _openedMemoId, out MemoMailboxEntrySnapshot openedMemo))
            {
                DrawOpenedMemo(sprite, openedMemo, contentBounds);
            }
            else
            {
                sprite.DrawString(_font, snapshot.LastActionSummary ?? "Select a parcel row to inspect it.", new Vector2(contentBounds.X, contentBounds.Bottom - 18), new Color(97, 105, 117), 0f, Vector2.Zero, 0.37f, SpriteEffects.None, 0f);
            }
        }

        private void DrawReceiveScrollbar(SpriteBatch sprite, MemoMailboxSnapshot snapshot)
        {
            if (_receiveScrollbarSkin == null || !_receiveScrollbarSkin.IsReady)
            {
                DrawFallbackScrollbar(sprite, snapshot);
                return;
            }

            Rectangle prevBounds = GetReceiveScrollbarPrevBounds();
            Rectangle nextBounds = GetReceiveScrollbarNextBounds();
            Rectangle trackBounds = GetReceiveScrollbarTrackBounds();
            Rectangle thumbBounds = GetReceiveScrollbarThumbBounds(snapshot);

            DrawScrollbarTrack(sprite, trackBounds);
            DrawScrollbarArrow(sprite, prevBounds, _receiveScrollbarSkin.PrevStates, _receiveScrollbarSkin.PrevDisabled, _scrollOffset > 0, pressed: false);
            DrawScrollbarArrow(sprite, nextBounds, _receiveScrollbarSkin.NextStates, _receiveScrollbarSkin.NextDisabled, _scrollOffset < GetMaxScrollOffset(snapshot), pressed: false);

            Texture2D thumbTexture = ResolveScrollbarStateTexture(
                _receiveScrollbarSkin.ThumbStates,
                thumbBounds.Contains(Mouse.GetState().Position),
                _isDraggingReceiveScrollThumb);
            if (thumbTexture != null)
            {
                sprite.Draw(thumbTexture, new Vector2(thumbBounds.X, thumbBounds.Y), Color.White);
            }
        }

        private void DrawFallbackScrollbar(SpriteBatch sprite, MemoMailboxSnapshot snapshot)
        {
            Rectangle contentBounds = GetContentBounds();
            Rectangle track = new Rectangle(contentBounds.Right - 8, contentBounds.Y, 4, MaxVisibleEntries * RowHeight);
            sprite.Draw(_pixel, track, new Color(201, 208, 217));

            int visibleCount = Math.Min(MaxVisibleEntries, snapshot.Entries.Count);
            int thumbHeight = snapshot.Entries.Count <= visibleCount
                ? track.Height
                : Math.Max(18, (int)Math.Round(track.Height * (visibleCount / (float)snapshot.Entries.Count)));
            int thumbY = snapshot.Entries.Count <= visibleCount
                ? track.Y
                : track.Y + (int)Math.Round((track.Height - thumbHeight) * (_scrollOffset / (float)Math.Max(1, snapshot.Entries.Count - visibleCount)));
            sprite.Draw(_pixel, new Rectangle(track.X, thumbY, track.Width, thumbHeight), new Color(108, 138, 171));
        }

        private void DrawOpenedMemo(SpriteBatch sprite, MemoMailboxEntrySnapshot memo, Rectangle contentBounds)
        {
            Rectangle panel = new Rectangle(contentBounds.X, contentBounds.Bottom - 84, contentBounds.Width - 12, 80);
            sprite.Draw(_pixel, panel, new Color(255, 255, 255, 205));
            sprite.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 1), new Color(196, 205, 214));

            Color headingColor = new Color(56, 66, 80);
            Color bodyColor = new Color(75, 82, 93);
            sprite.DrawString(_font, Truncate(memo.Subject, 30), new Vector2(panel.X + 4, panel.Y + 4), headingColor, 0f, Vector2.Zero, 0.43f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, $"From {memo.Sender}", new Vector2(panel.X + 4, panel.Y + 20), new Color(112, 119, 131), 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, memo.DeliveredAtText, new Vector2(panel.Right - 76, panel.Y + 20), new Color(112, 119, 131), 0f, Vector2.Zero, 0.32f, SpriteEffects.None, 0f);
            if (!string.IsNullOrWhiteSpace(memo.StatusText))
            {
                sprite.DrawString(_font, Truncate(memo.StatusText, 18), new Vector2(panel.X + 4, panel.Y + 32), new Color(103, 111, 123), 0f, Vector2.Zero, 0.33f, SpriteEffects.None, 0f);
            }

            float drawY = panel.Y + 44;
            foreach (string line in WrapText(memo.Body, panel.Width - 8, 0.36f))
            {
                sprite.DrawString(_font, line, new Vector2(panel.X + 4, drawY), bodyColor, 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
                drawY += 12f;
                if (drawY > panel.Bottom - 16)
                {
                    break;
                }
            }

            string footer = memo.CanClaimAttachment
                ? $"GET opens the package view for {memo.AttachmentSummary}."
                : memo.HasAttachment
                    ? $"Claimed package: {memo.AttachmentSummary}."
                    : "No package is attached to this parcel row.";
            sprite.DrawString(_font, footer, new Vector2(panel.X + 4, panel.Bottom - 14), memo.CanClaimAttachment ? new Color(74, 134, 80) : new Color(123, 129, 141), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
        }

        private void DrawComposeTab(SpriteBatch sprite, MemoMailboxDraftSnapshot snapshot, Rectangle bounds, bool quickMode, int tickCount)
        {
            Rectangle bodyBounds = GetComposeBodyBounds(bounds, quickMode);
            Rectangle recipientBounds = GetComposeRecipientBounds(bounds, quickMode);
            Rectangle mesoBounds = GetComposeMesoBounds(bounds, quickMode);
            Rectangle itemIconBounds = GetComposeItemIconBounds(bounds, quickMode);
            Rectangle itemTextBounds = GetComposeItemTextBounds(bounds, quickMode);

            DrawFieldHighlight(sprite, bodyBounds, IsBodyField(_activeInputField));
            DrawFieldHighlight(sprite, recipientBounds, IsRecipientField(_activeInputField));
            DrawFieldHighlight(sprite, mesoBounds, IsMesoField(_activeInputField));

            DrawMultilineEditor(sprite, bodyBounds, GetDisplayValue(snapshot, quickMode ? ComposeInputField.QuickBody : ComposeInputField.SendBody), 0.37f, new Color(67, 72, 82), quickMode ? ComposeInputField.QuickBody : ComposeInputField.SendBody, tickCount, 6);
            DrawSingleLineEditor(sprite, recipientBounds, GetDisplayValue(snapshot, quickMode ? ComposeInputField.QuickRecipient : ComposeInputField.SendRecipient), 0.38f, new Color(63, 66, 74), quickMode ? ComposeInputField.QuickRecipient : ComposeInputField.SendRecipient, tickCount);
            DrawSingleLineEditor(sprite, mesoBounds, GetDisplayValue(snapshot, quickMode ? ComposeInputField.QuickMeso : ComposeInputField.SendMeso), 0.38f, snapshot.AttachedMeso > 0 ? new Color(172, 126, 18) : new Color(110, 115, 123), quickMode ? ComposeInputField.QuickMeso : ComposeInputField.SendMeso, tickCount);

            Color itemColor = quickMode
                ? new Color(140, 81, 59)
                : snapshot.AttachmentKind == MemoDraftAttachmentKind.Item
                    ? new Color(70, 104, 136)
                    : new Color(124, 129, 136);
            string itemSummary = quickMode
                ? "Quick Send does not allow item parcels."
                : snapshot.AwaitingItemSelection
                    ? "Select an inventory slot to stage it into the parcel."
                : string.IsNullOrWhiteSpace(snapshot.ItemAttachmentSummary)
                    ? "Click the package lane to stage an inventory item."
                    : snapshot.ItemAttachmentSummary;
            sprite.DrawString(_font, Truncate(itemSummary, 24), new Vector2(itemTextBounds.X + 4, itemTextBounds.Y + 7), itemColor, 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);

            if (snapshot.AttachmentKind == MemoDraftAttachmentKind.Item)
            {
                sprite.Draw(_pixel, itemIconBounds, new Color(86, 117, 150, 38));
                sprite.Draw(_pixel, new Rectangle(itemIconBounds.X, itemIconBounds.Y, itemIconBounds.Width, 1), new Color(88, 111, 134));
                sprite.DrawString(_font, "PKG", new Vector2(itemIconBounds.X + 4, itemIconBounds.Y + 8), new Color(67, 96, 127), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
            }
            else if (quickMode)
            {
                sprite.Draw(_pixel, itemIconBounds, new Color(160, 132, 132, 28));
                sprite.DrawString(_font, "X", new Vector2(itemIconBounds.X + 11, itemIconBounds.Y + 8), new Color(127, 86, 86), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
            }

            if (snapshot.ShowTaxInfo)
            {
                Rectangle taxPanel = new Rectangle(bounds.X + 18, bounds.Bottom - 59, bounds.Width - 36, 40);
                sprite.Draw(_pixel, taxPanel, new Color(247, 244, 229, 232));
                sprite.Draw(_pixel, new Rectangle(taxPanel.X, taxPanel.Y, taxPanel.Width, 1), new Color(205, 191, 148));

                float taxY = taxPanel.Y + 5;
                foreach (string line in WrapText(snapshot.TaxSummary, taxPanel.Width - 8, 0.35f))
                {
                    sprite.DrawString(_font, line, new Vector2(taxPanel.X + 4, taxY), new Color(114, 94, 55), 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
                    taxY += 12f;
                    if (taxY > taxPanel.Bottom - 10)
                    {
                        break;
                    }
                }
            }
            else
            {
                Color hintColor = !quickMode && snapshot.AttachmentKind == MemoDraftAttachmentKind.Item
                    ? new Color(75, 109, 141)
                    : quickMode && !snapshot.CanQuickSend
                        ? new Color(146, 77, 57)
                        : new Color(89, 122, 158);
                string hint = quickMode
                    ? snapshot.CanQuickSend
                        ? "Quick Send mirrors the client split: recipient, note, and meso edit live in the owner."
                        : "Quick Send is blocked until the staged parcel no longer carries an item attachment."
                    : snapshot.AwaitingItemSelection
                        ? "Parcel picker is waiting on an inventory click or drag-drop into the package lane."
                    : "Send now keeps editable recipient, note, and meso fields in the parcel owner.";
                sprite.DrawString(_font, hint, new Vector2(bounds.X + 1, bounds.Bottom - 31), hintColor, 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
            }

            sprite.DrawString(_font, Truncate(snapshot.LastActionSummary ?? string.Empty, 72), new Vector2(bounds.X + 1, bounds.Bottom - 16), new Color(96, 105, 119), 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
        }

        private void HandleLeftClick(MemoMailboxSnapshot snapshot, MemoMailboxDraftSnapshot draftSnapshot, Point mousePosition)
        {
            for (int i = 0; i < _tabBounds.Count; i++)
            {
                if (_tabBounds[i].Contains(mousePosition))
                {
                    DeactivateInput();
                    if (i < _tabOrder.Count)
                    {
                        _tabSelected?.Invoke(_tabOrder[i]);
                    }

                    return;
                }
            }

            if (snapshot.ActiveTab == ParcelDialogTab.Receive)
            {
                foreach (RowLayout rowLayout in _rowLayouts)
                {
                    if (!rowLayout.Bounds.Contains(mousePosition))
                    {
                        continue;
                    }

                    _selectedMemoId = rowLayout.MemoId;
                    OpenSelectedMemo(snapshot);
                    return;
                }

                return;
            }

            Rectangle contentBounds = GetContentBounds();
            bool quickMode = snapshot.ActiveTab == ParcelDialogTab.QuickSend;
            if (GetComposeRecipientBounds(contentBounds, quickMode).Contains(mousePosition))
            {
                ActivateInput(quickMode ? ComposeInputField.QuickRecipient : ComposeInputField.SendRecipient, draftSnapshot);
                SetSingleLineCursorFromPoint(GetComposeRecipientBounds(contentBounds, quickMode));
                return;
            }

            if (GetComposeBodyBounds(contentBounds, quickMode).Contains(mousePosition))
            {
                ActivateInput(quickMode ? ComposeInputField.QuickBody : ComposeInputField.SendBody, draftSnapshot);
                _cursorPosition = _inputBuffer.Length;
                return;
            }

            if (GetComposeMesoBounds(contentBounds, quickMode).Contains(mousePosition))
            {
                ActivateInput(quickMode ? ComposeInputField.QuickMeso : ComposeInputField.SendMeso, draftSnapshot);
                SetSingleLineCursorFromPoint(GetComposeMesoBounds(contentBounds, quickMode));
                return;
            }

            if (GetComposeItemIconBounds(contentBounds, quickMode).Contains(mousePosition)
                || GetComposeItemTextBounds(contentBounds, quickMode).Contains(mousePosition))
            {
                DeactivateInput();
                _draftAttachmentRequested?.Invoke(snapshot.ActiveTab);
                return;
            }

            DeactivateInput();
        }

        private void OpenSelectedMemo(MemoMailboxSnapshot snapshot)
        {
            if (_selectedMemoId <= 0 || !TryGetEntry(snapshot, _selectedMemoId, out _))
            {
                return;
            }

            _openMemoRequested?.Invoke(_selectedMemoId);
            _openedMemoId = _selectedMemoId;
            UpdateButtonVisibility();
        }

        private void HandleReceiveAttachment()
        {
            if (_openedMemoId <= 0)
            {
                return;
            }

            _attachmentRequested?.Invoke(_openedMemoId);
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

            ClampScroll(snapshot);
            UpdateButtonVisibility();
        }

        private void HandleDispatch()
        {
            ClearCompositionText();
            _dispatchRequested?.Invoke();
        }

        private void HandleMesoButton(ParcelDialogTab tab)
        {
            _mesoRequested?.Invoke(tab);
            ActivateInput(
                tab == ParcelDialogTab.QuickSend ? ComposeInputField.QuickMeso : ComposeInputField.SendMeso,
                _currentDraftSnapshot ?? RefreshDraftSnapshot());
        }

        private void EnsureSelection(MemoMailboxSnapshot snapshot)
        {
            if (_selectedMemoId > 0 && TryGetEntry(snapshot, _selectedMemoId, out _))
            {
                return;
            }

            _selectedMemoId = GetFirstEntryId(snapshot);
            if (_openedMemoId > 0 && !TryGetEntry(snapshot, _openedMemoId, out _))
            {
                _openedMemoId = -1;
            }
        }

        private void ClampScroll(MemoMailboxSnapshot snapshot)
        {
            int maxOffset = GetMaxScrollOffset(snapshot);
            if (_scrollOffset < 0)
            {
                _scrollOffset = 0;
            }
            else if (_scrollOffset > maxOffset)
            {
                _scrollOffset = maxOffset;
            }
        }

        private void UpdateButtonVisibility()
        {
            ParcelDialogTab activeTab = _currentSnapshot?.ActiveTab ?? ParcelDialogTab.Receive;
            MemoMailboxDraftSnapshot draftSnapshot = _currentDraftSnapshot ?? new MemoMailboxDraftSnapshot();
            bool receiveVisible = activeTab == ParcelDialogTab.Receive;
            bool sendVisible = activeTab == ParcelDialogTab.Send;
            bool quickVisible = activeTab == ParcelDialogTab.QuickSend;
            bool canClaimAttachment = _openedMemoId > 0
                && _currentSnapshot != null
                && TryGetEntry(_currentSnapshot, _openedMemoId, out MemoMailboxEntrySnapshot openedMemo)
                && openedMemo.CanClaimAttachment;

            if (_receiveGetButton != null)
            {
                _receiveGetButton.ButtonVisible = receiveVisible && _openedMemoId > 0;
                _receiveGetButton.SetButtonState(canClaimAttachment ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_receiveDeleteButton != null)
            {
                _receiveDeleteButton.ButtonVisible = receiveVisible && _openedMemoId > 0;
                _receiveDeleteButton.SetButtonState(_openedMemoId > 0 ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_sendSendButton != null)
            {
                _sendSendButton.ButtonVisible = sendVisible;
                _sendSendButton.SetButtonState(draftSnapshot.CanSend ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_sendMesoButton != null)
            {
                _sendMesoButton.ButtonVisible = sendVisible;
                _sendMesoButton.SetButtonState(UIObjectState.Normal);
            }

            if (_sendTaxOpenButton != null)
            {
                _sendTaxOpenButton.ButtonVisible = sendVisible && !draftSnapshot.ShowTaxInfo;
            }

            if (_sendTaxCloseButton != null)
            {
                _sendTaxCloseButton.ButtonVisible = sendVisible && draftSnapshot.ShowTaxInfo;
            }

            if (_quickSendButton != null)
            {
                _quickSendButton.ButtonVisible = quickVisible;
                _quickSendButton.SetButtonState(draftSnapshot.CanQuickSend ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_quickMesoButton != null)
            {
                _quickMesoButton.ButtonVisible = quickVisible;
                _quickMesoButton.SetButtonState(UIObjectState.Normal);
            }

            if (_quickTaxOpenButton != null)
            {
                _quickTaxOpenButton.ButtonVisible = quickVisible && !draftSnapshot.ShowTaxInfo;
            }

            if (_quickTaxCloseButton != null)
            {
                _quickTaxCloseButton.ButtonVisible = quickVisible && draftSnapshot.ShowTaxInfo;
            }
        }

        private MemoMailboxSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new MemoMailboxSnapshot();
            return _currentSnapshot;
        }

        private MemoMailboxDraftSnapshot RefreshDraftSnapshot()
        {
            _currentDraftSnapshot = _draftSnapshotProvider?.Invoke() ?? new MemoMailboxDraftSnapshot();
            return _currentDraftSnapshot;
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
            int width = (CurrentFrame?.Width ?? 289) - ContentMarginLeft - ContentMarginRight;
            int height = (CurrentFrame?.Height ?? 310) - ContentMarginTop - ContentMarginBottom;
            return new Rectangle(Position.X + ContentMarginLeft, Position.Y + ContentMarginTop, Math.Max(0, width), Math.Max(0, height));
        }

        private Rectangle GetComposeBodyBounds(Rectangle contentBounds, bool quickMode)
        {
            int height = quickMode ? 80 : 92;
            return new Rectangle(contentBounds.X + 62, contentBounds.Y + 5, 185, height);
        }

        private Rectangle GetComposeRecipientBounds(Rectangle contentBounds, bool quickMode)
        {
            int y = quickMode ? contentBounds.Y + 108 : contentBounds.Y + 108;
            return new Rectangle(contentBounds.X + 91, y, 158, 15);
        }

        private Rectangle GetComposeMesoBounds(Rectangle contentBounds, bool quickMode)
        {
            int y = quickMode ? contentBounds.Y + 131 : contentBounds.Y + 131;
            return new Rectangle(contentBounds.X + 112, y, 137, 15);
        }

        private Rectangle GetComposeItemIconBounds(Rectangle contentBounds, bool quickMode)
        {
            int y = quickMode ? contentBounds.Y + 154 : contentBounds.Y + 154;
            return new Rectangle(contentBounds.X + 92, y, 33, 29);
        }

        private Rectangle GetComposeItemTextBounds(Rectangle contentBounds, bool quickMode)
        {
            int y = quickMode ? contentBounds.Y + 154 : contentBounds.Y + 154;
            return new Rectangle(contentBounds.X + 128, y, 121, 29);
        }

        private Rectangle GetReceiveScrollbarBounds()
        {
            Rectangle contentBounds = GetContentBounds();
            int width = _receiveScrollbarSkin?.Width ?? 11;
            return new Rectangle(contentBounds.Right - width - 4, contentBounds.Y, width, MaxVisibleEntries * RowHeight);
        }

        private Rectangle GetReceiveScrollbarPrevBounds()
        {
            Rectangle bounds = GetReceiveScrollbarBounds();
            int height = _receiveScrollbarSkin?.PrevHeight ?? 12;
            return new Rectangle(bounds.X, bounds.Y, bounds.Width, height);
        }

        private Rectangle GetReceiveScrollbarNextBounds()
        {
            Rectangle bounds = GetReceiveScrollbarBounds();
            int height = _receiveScrollbarSkin?.NextHeight ?? 12;
            return new Rectangle(bounds.X, bounds.Bottom - height, bounds.Width, height);
        }

        private Rectangle GetReceiveScrollbarTrackBounds()
        {
            Rectangle bounds = GetReceiveScrollbarBounds();
            Rectangle prevBounds = GetReceiveScrollbarPrevBounds();
            Rectangle nextBounds = GetReceiveScrollbarNextBounds();
            return new Rectangle(bounds.X, prevBounds.Bottom, bounds.Width, Math.Max(0, nextBounds.Y - prevBounds.Bottom));
        }

        private Rectangle GetReceiveScrollbarThumbBounds(MemoMailboxSnapshot snapshot)
        {
            Rectangle trackBounds = GetReceiveScrollbarTrackBounds();
            if (_receiveScrollbarSkin == null || !_receiveScrollbarSkin.IsReady || snapshot.Entries.Count <= MaxVisibleEntries)
            {
                return new Rectangle(trackBounds.X, trackBounds.Y, trackBounds.Width, Math.Min(trackBounds.Height, Math.Max(1, _receiveScrollbarSkin?.ThumbHeight ?? 26)));
            }

            int thumbHeight = Math.Min(trackBounds.Height, Math.Max(1, _receiveScrollbarSkin.ThumbHeight));
            int thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
            int maxOffset = GetMaxScrollOffset(snapshot);
            int thumbY = maxOffset <= 0
                ? trackBounds.Y
                : trackBounds.Y + (int)Math.Round((_scrollOffset / (float)maxOffset) * thumbTravel);
            return new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        }

        private void DrawScrollbarTrack(SpriteBatch sprite, Rectangle trackBounds)
        {
            if (_receiveScrollbarSkin?.Base == null)
            {
                return;
            }

            int tileY = trackBounds.Y;
            while (tileY < trackBounds.Bottom)
            {
                int tileHeight = Math.Min(_receiveScrollbarSkin.Base.Height, trackBounds.Bottom - tileY);
                Rectangle destination = new Rectangle(trackBounds.X, tileY, _receiveScrollbarSkin.Base.Width, tileHeight);
                Rectangle? source = tileHeight == _receiveScrollbarSkin.Base.Height
                    ? null
                    : new Rectangle(0, 0, _receiveScrollbarSkin.Base.Width, tileHeight);
                sprite.Draw(_receiveScrollbarSkin.Base, destination, source, Color.White);
                tileY += tileHeight;
            }
        }

        private void DrawScrollbarArrow(SpriteBatch sprite, Rectangle bounds, Texture2D[] states, Texture2D disabledTexture, bool enabled, bool pressed)
        {
            Texture2D texture = enabled
                ? ResolveScrollbarStateTexture(states, bounds.Contains(Mouse.GetState().Position), pressed)
                : disabledTexture ?? states?[0];
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
            }
        }

        private static Texture2D ResolveScrollbarStateTexture(Texture2D[] states, bool hovered, bool pressed)
        {
            if (states == null || states.Length == 0)
            {
                return null;
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

        private void HandleKeyboardInput(MemoMailboxSnapshot snapshot, MemoMailboxDraftSnapshot draftSnapshot, KeyboardState keyboardState, int tickCount)
        {
            if (_activeInputField == ComposeInputField.None || snapshot.ActiveTab == ParcelDialogTab.Receive)
            {
                ResetKeyRepeat();
                return;
            }

            if (HandleNavigationKey(keyboardState, Keys.Back, tickCount, removeBackward: true))
            {
                return;
            }

            if (HandleNavigationKey(keyboardState, Keys.Delete, tickCount, removeBackward: false))
            {
                return;
            }

            if (HandleCursorKey(keyboardState, Keys.Left, tickCount, -1)
                || HandleCursorKey(keyboardState, Keys.Right, tickCount, 1))
            {
                return;
            }

            if (Pressed(keyboardState, Keys.Home))
            {
                _cursorPosition = 0;
                _cursorBlinkTimer = tickCount;
                ClearCompositionText();
                return;
            }

            if (Pressed(keyboardState, Keys.End))
            {
                _cursorPosition = _inputBuffer.Length;
                _cursorBlinkTimer = tickCount;
                ClearCompositionText();
                return;
            }

            if (Pressed(keyboardState, Keys.Tab))
            {
                CycleInputTarget(snapshot.ActiveTab, keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift), draftSnapshot);
                return;
            }

            if (Pressed(keyboardState, Keys.Enter))
            {
                if (IsBodyField(_activeInputField) && TryInsertCharacter('\n'))
                {
                    SyncInputBuffer();
                }

                return;
            }

            if (_lastHeldKey != Keys.None && !keyboardState.IsKeyDown(_lastHeldKey))
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
                SyncInputBuffer();
                return true;
            }

            if (ShouldRepeatKey(key, tickCount))
            {
                ApplyDelete(removeBackward);
                _lastKeyRepeatTime = tickCount;
                SyncInputBuffer();
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
                _cursorBlinkTimer = tickCount;
                ClearCompositionText();
                _lastHeldKey = key;
                _keyHoldStartTime = tickCount;
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            if (ShouldRepeatKey(key, tickCount))
            {
                _cursorPosition = Math.Clamp(_cursorPosition + delta, 0, _inputBuffer.Length);
                _cursorBlinkTimer = tickCount;
                ClearCompositionText();
                _lastKeyRepeatTime = tickCount;
                return true;
            }

            return true;
        }

        private void ApplyDelete(bool removeBackward)
        {
            ClearCompositionText();
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

        private void ActivateInput(ComposeInputField field, MemoMailboxDraftSnapshot snapshot)
        {
            if (field == ComposeInputField.None)
            {
                DeactivateInput();
                return;
            }

            _activeInputField = field;
            _inputBuffer.Clear();
            _inputBuffer.Append(GetDraftFieldValue(snapshot, field));
            _cursorPosition = _inputBuffer.Length;
            _cursorBlinkTimer = Environment.TickCount;
            ClearCompositionText();
            ResetKeyRepeat();
        }

        private void DeactivateInput()
        {
            _activeInputField = ComposeInputField.None;
            _inputBuffer.Clear();
            _cursorPosition = 0;
            ClearCompositionText();
            ResetKeyRepeat();
        }

        private void CycleInputTarget(ParcelDialogTab activeTab, bool backwards, MemoMailboxDraftSnapshot snapshot)
        {
            ComposeInputField[] order = activeTab == ParcelDialogTab.QuickSend
                ? new[] { ComposeInputField.QuickRecipient, ComposeInputField.QuickBody, ComposeInputField.QuickMeso }
                : new[] { ComposeInputField.SendRecipient, ComposeInputField.SendBody, ComposeInputField.SendMeso };

            int currentIndex = Array.IndexOf(order, _activeInputField);
            if (currentIndex < 0)
            {
                ActivateInput(order[0], snapshot);
                return;
            }

            int nextIndex = backwards
                ? (currentIndex + order.Length - 1) % order.Length
                : (currentIndex + 1) % order.Length;
            ActivateInput(order[nextIndex], snapshot);
        }

        private bool TryInsertCharacter(char character)
        {
            string sanitized = SanitizeCommittedText(character.ToString());
            if (string.IsNullOrEmpty(sanitized))
            {
                return false;
            }

            string insertText = sanitized;
            if (IsMesoField(_activeInputField))
            {
                insertText = StripNonDigits(insertText);
                if (string.IsNullOrEmpty(insertText) || _inputBuffer.Length + insertText.Length > MesoMaxDigits)
                {
                    return false;
                }
            }
            else if (IsRecipientField(_activeInputField) && _inputBuffer.Length + insertText.Length > RecipientMaxLength)
            {
                insertText = insertText[..Math.Max(0, RecipientMaxLength - _inputBuffer.Length)];
            }

            if (string.IsNullOrEmpty(insertText))
            {
                return false;
            }

            _inputBuffer.Insert(_cursorPosition, insertText);
            _cursorPosition += insertText.Length;
            _cursorBlinkTimer = Environment.TickCount;
            return true;
        }

        private void SyncInputBuffer()
        {
            string value = _inputBuffer.ToString();
            switch (_activeInputField)
            {
                case ComposeInputField.SendRecipient:
                case ComposeInputField.QuickRecipient:
                    _recipientChanged?.Invoke(value);
                    break;
                case ComposeInputField.SendBody:
                case ComposeInputField.QuickBody:
                    _bodyChanged?.Invoke(value);
                    break;
                case ComposeInputField.SendMeso:
                case ComposeInputField.QuickMeso:
                    _mesoChanged?.Invoke(TryParseDraftMeso(value));
                    break;
            }

            _currentDraftSnapshot = RefreshDraftSnapshot();
            _cursorBlinkTimer = Environment.TickCount;
        }

        private void SetSingleLineCursorFromPoint(Rectangle bounds)
        {
            string text = GetDisplayValue(_currentDraftSnapshot ?? RefreshDraftSnapshot(), _activeInputField);
            float scale = 0.38f;
            _cursorPosition = 0;
            for (int i = 0; i < text.Length; i++)
            {
                string segment = text[..(i + 1)];
                float width = _font.MeasureString(segment).X * scale;
                if (bounds.X + 3 + width >= Mouse.GetState().X)
                {
                    _cursorPosition = i;
                    return;
                }

                _cursorPosition = i + 1;
            }
        }

        private bool TryBeginReceiveScrollbarInteraction(MemoMailboxSnapshot snapshot, Point mousePosition)
        {
            if (_receiveScrollbarSkin == null || !_receiveScrollbarSkin.IsReady || snapshot.Entries.Count <= MaxVisibleEntries)
            {
                return false;
            }

            Rectangle prevBounds = GetReceiveScrollbarPrevBounds();
            Rectangle nextBounds = GetReceiveScrollbarNextBounds();
            Rectangle thumbBounds = GetReceiveScrollbarThumbBounds(snapshot);
            Rectangle trackBounds = GetReceiveScrollbarTrackBounds();

            if (prevBounds.Contains(mousePosition))
            {
                _scrollOffset = Math.Max(0, _scrollOffset - 1);
                return true;
            }

            if (nextBounds.Contains(mousePosition))
            {
                _scrollOffset = Math.Min(GetMaxScrollOffset(snapshot), _scrollOffset + 1);
                return true;
            }

            if (thumbBounds.Contains(mousePosition))
            {
                _isDraggingReceiveScrollThumb = true;
                _receiveScrollThumbDragOffsetY = mousePosition.Y - thumbBounds.Y;
                return true;
            }

            if (trackBounds.Contains(mousePosition))
            {
                _scrollOffset = ResolveScrollOffsetFromThumbPosition(snapshot, mousePosition.Y - (_receiveScrollbarSkin.ThumbHeight / 2));
                return true;
            }

            return false;
        }

        private void UpdateReceiveScrollbarDrag(MemoMailboxSnapshot snapshot, MouseState mouseState)
        {
            if (!_isDraggingReceiveScrollThumb || mouseState.LeftButton != ButtonState.Pressed)
            {
                return;
            }

            _scrollOffset = ResolveScrollOffsetFromThumbPosition(snapshot, mouseState.Y - _receiveScrollThumbDragOffsetY);
        }

        private int ResolveScrollOffsetFromThumbPosition(MemoMailboxSnapshot snapshot, int thumbTop)
        {
            Rectangle trackBounds = GetReceiveScrollbarTrackBounds();
            int thumbTravel = Math.Max(1, trackBounds.Height - (_receiveScrollbarSkin?.ThumbHeight ?? 26));
            int clampedTop = Math.Clamp(thumbTop, trackBounds.Y, trackBounds.Bottom - (_receiveScrollbarSkin?.ThumbHeight ?? 26));
            float ratio = (clampedTop - trackBounds.Y) / (float)thumbTravel;
            return (int)Math.Round(ratio * GetMaxScrollOffset(snapshot));
        }

        private int GetMaxScrollOffset(MemoMailboxSnapshot snapshot)
        {
            return Math.Max(0, snapshot.Entries.Count - MaxVisibleEntries);
        }

        private void DrawFieldHighlight(SpriteBatch sprite, Rectangle bounds, bool active)
        {
            if (!active)
            {
                return;
            }

            sprite.Draw(_pixel, bounds, new Color(255, 255, 255, 18));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(81, 124, 170, 170));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(81, 124, 170, 170));
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(81, 124, 170, 170));
            sprite.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(81, 124, 170, 170));
        }

        private void DrawSingleLineEditor(SpriteBatch sprite, Rectangle bounds, string text, float scale, Color color, ComposeInputField field, int tickCount)
        {
            string displayText = text ?? string.Empty;
            string clipped = TruncateToWidth(displayText, bounds.Width - 6, scale);
            sprite.DrawString(_font, clipped, new Vector2(bounds.X + 3, bounds.Y + 1), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (_activeInputField != field || ((tickCount - _cursorBlinkTimer) / 500) % 2 != 0)
            {
                return;
            }

            string cursorText = displayText[..Math.Clamp(_cursorPosition, 0, displayText.Length)];
            float cursorX = bounds.X + 3 + (_font.MeasureString(TruncateToWidth(cursorText, bounds.Width - 6, scale)).X * scale);
            sprite.Draw(_pixel, new Rectangle((int)cursorX, bounds.Y + 1, 1, bounds.Height - 2), new Color(66, 76, 94));
        }

        private void DrawMultilineEditor(SpriteBatch sprite, Rectangle bounds, string text, float scale, Color color, ComposeInputField field, int tickCount, int maxLines)
        {
            IReadOnlyList<TextLineLayout> lines = BuildTextLines(text ?? string.Empty, bounds.Width - 6, scale);
            if (lines.Count == 0)
            {
                lines = new[] { new TextLineLayout { StartIndex = 0, EndIndex = 0, Text = string.Empty } };
            }

            float y = bounds.Y + 2;
            int visibleCount = Math.Min(maxLines, lines.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                sprite.DrawString(_font, lines[i].Text, new Vector2(bounds.X + 2, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                y += 12f;
            }

            if (_activeInputField != field || ((tickCount - _cursorBlinkTimer) / 500) % 2 != 0)
            {
                return;
            }

            int lineIndex = ResolveCursorLineIndex(lines, _cursorPosition);
            if (lineIndex < 0 || lineIndex >= visibleCount)
            {
                return;
            }

            TextLineLayout line = lines[lineIndex];
            int relativeIndex = Math.Clamp(_cursorPosition - line.StartIndex, 0, line.Text.Length);
            string cursorText = line.Text[..relativeIndex];
            float cursorX = bounds.X + 2 + (_font.MeasureString(cursorText).X * scale);
            int cursorY = bounds.Y + 2 + (lineIndex * 12);
            sprite.Draw(_pixel, new Rectangle((int)cursorX, cursorY, 1, 11), new Color(66, 76, 94));
        }

        private string GetDisplayValue(MemoMailboxDraftSnapshot snapshot, ComposeInputField field)
        {
            if (_activeInputField == field)
            {
                string value = _inputBuffer.ToString();
                if (!string.IsNullOrEmpty(_compositionText))
                {
                    value = value.Insert(_cursorPosition, _compositionText);
                }

                return value;
            }

            return GetDraftFieldValue(snapshot, field);
        }

        private static string GetDraftFieldValue(MemoMailboxDraftSnapshot snapshot, ComposeInputField field)
        {
            return field switch
            {
                ComposeInputField.SendRecipient or ComposeInputField.QuickRecipient => snapshot?.Recipient ?? string.Empty,
                ComposeInputField.SendBody or ComposeInputField.QuickBody => snapshot?.Body ?? string.Empty,
                ComposeInputField.SendMeso or ComposeInputField.QuickMeso => snapshot?.AttachedMeso > 0 ? snapshot.AttachedMeso.ToString() : string.Empty,
                _ => string.Empty
            };
        }

        private static bool IsRecipientField(ComposeInputField field)
        {
            return field == ComposeInputField.SendRecipient || field == ComposeInputField.QuickRecipient;
        }

        private static bool IsBodyField(ComposeInputField field)
        {
            return field == ComposeInputField.SendBody || field == ComposeInputField.QuickBody;
        }

        private static bool IsMesoField(ComposeInputField field)
        {
            return field == ComposeInputField.SendMeso || field == ComposeInputField.QuickMeso;
        }

        private static bool IsTabAvailable(ParcelDialogTabAvailability availableTabs, ParcelDialogTab tab)
        {
            ParcelDialogTabAvailability flag = tab switch
            {
                ParcelDialogTab.Receive => ParcelDialogTabAvailability.Receive,
                ParcelDialogTab.Send => ParcelDialogTabAvailability.Send,
                ParcelDialogTab.QuickSend => ParcelDialogTabAvailability.QuickSend,
                _ => ParcelDialogTabAvailability.None
            };

            return (availableTabs & flag) != 0;
        }

        private static string SanitizeCommittedText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r", string.Empty);
        }

        private static string StripNonDigits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (char character in text)
            {
                if (char.IsDigit(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString();
        }

        private int TryParseDraftMeso(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            return int.TryParse(StripNonDigits(value), out int meso)
                ? Math.Max(0, meso)
                : 0;
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private bool ShouldRepeatKey(Keys key, int tickCount)
        {
            return _lastHeldKey == key
                && tickCount - _keyHoldStartTime >= KeyRepeatDelayMs
                && tickCount - _lastKeyRepeatTime >= KeyRepeatRateMs;
        }

        private void ResetKeyRepeat()
        {
            _lastHeldKey = Keys.None;
            _keyHoldStartTime = 0;
            _lastKeyRepeatTime = 0;
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

        private string TruncateToWidth(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string value = text;
            while (value.Length > 0 && (_font.MeasureString(value).X * scale) > maxWidth)
            {
                value = value[1..];
            }

            return value;
        }

        private IReadOnlyList<TextLineLayout> BuildTextLines(string text, float maxWidth, float scale)
        {
            var lines = new List<TextLineLayout>();
            if (_font == null)
            {
                return lines;
            }

            string value = text ?? string.Empty;
            if (value.Length == 0)
            {
                lines.Add(new TextLineLayout { StartIndex = 0, EndIndex = 0, Text = string.Empty });
                return lines;
            }

            int startIndex = 0;
            int index = 0;
            var current = new StringBuilder();
            while (index < value.Length)
            {
                char character = value[index];
                if (character == '\n')
                {
                    lines.Add(new TextLineLayout
                    {
                        StartIndex = startIndex,
                        EndIndex = index,
                        Text = current.ToString()
                    });
                    current.Clear();
                    index++;
                    startIndex = index;
                    continue;
                }

                current.Append(character);
                if ((_font.MeasureString(current).X * scale) > maxWidth && current.Length > 1)
                {
                    current.Length--;
                    lines.Add(new TextLineLayout
                    {
                        StartIndex = startIndex,
                        EndIndex = index,
                        Text = current.ToString()
                    });
                    current.Clear();
                    startIndex = index;
                    continue;
                }

                index++;
            }

            lines.Add(new TextLineLayout
            {
                StartIndex = startIndex,
                EndIndex = value.Length,
                Text = current.ToString()
            });
            return lines;
        }

        private static int ResolveCursorLineIndex(IReadOnlyList<TextLineLayout> lines, int cursorPosition)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (cursorPosition >= lines[i].StartIndex && cursorPosition <= lines[i].EndIndex)
                {
                    return i;
                }
            }

            return lines.Count - 1;
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield return string.Empty;
                yield break;
            }

            foreach (TextLineLayout line in BuildTextLines(text, maxWidth, scale))
            {
                yield return line.Text;
            }
        }
    }
}
