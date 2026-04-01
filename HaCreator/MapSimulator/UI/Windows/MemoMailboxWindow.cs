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
        private readonly Texture2D _pixel;
        private readonly List<RowLayout> _rowLayouts = new();
        private readonly List<Rectangle> _tabBounds = new();

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
        private MouseState _previousMouseState;
        private int _selectedMemoId = -1;
        private int _openedMemoId = -1;
        private int _scrollOffset;
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
            Point tabQuickHintOffset)
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
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => _windowName;

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
            Action<ParcelDialogTab> mesoRequested)
        {
            _tabSelected = tabSelected;
            _openMemoRequested = openMemoRequested;
            _deleteMemoRequested = deleteMemoRequested;
            _attachmentRequested = attachmentRequested;
            _dispatchRequested = dispatchRequested;
            _taxInfoRequested = taxInfoRequested;
            _mesoRequested = mesoRequested;
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
            ConfigureButton(_sendMesoButton, () => _mesoRequested?.Invoke(ParcelDialogTab.Send));
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
            ConfigureButton(_quickMesoButton, () => _mesoRequested?.Invoke(ParcelDialogTab.QuickSend));
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
            UpdateButtonVisibility();

            MouseState mouseState = Mouse.GetState();
            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y))
            {
                HandleLeftClick(snapshot, mouseState.Position);
            }

            if (snapshot.ActiveTab == ParcelDialogTab.Receive)
            {
                int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                if (wheelDelta != 0 && ContainsPoint(mouseState.X, mouseState.Y))
                {
                    _scrollOffset -= Math.Sign(wheelDelta);
                    ClampScroll(snapshot);
                }
            }

            _previousMouseState = mouseState;
            _currentDraftSnapshot = draftSnapshot;
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

            DrawTabs(sprite, snapshot.ActiveTab);
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
                    DrawComposeTab(sprite, draftSnapshot, contentBounds, true);
                    break;
                default:
                    DrawComposeTab(sprite, draftSnapshot, contentBounds, false);
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

        private void DrawTabs(SpriteBatch sprite, ParcelDialogTab activeTab)
        {
            _tabBounds.Clear();
            int x = Position.X + 8;
            int y = Position.Y + 9;
            for (int i = 0; i < 3; i++)
            {
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
                Rectangle rowBounds = new Rectangle(contentBounds.X, contentBounds.Y + (index * RowHeight), contentBounds.Width - 12, RowHeight - 2);
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
                sprite.DrawString(_font, memo.DeliveredAtText, new Vector2(rowBounds.Right - timeSize.X - 4, rowBounds.Y + 3), new Color(123, 129, 141), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                sprite.DrawString(_font, Truncate(memo.Preview, 36), new Vector2(rowBounds.Right - 120, rowBounds.Y + 15), new Color(108, 115, 126), 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                if (memo.HasAttachment)
                {
                    Color attachmentColor = memo.CanClaimAttachment ? new Color(67, 137, 76) : new Color(129, 136, 145);
                    sprite.DrawString(_font, "PKG", new Vector2(rowBounds.Right - 28, rowBounds.Y + 14), attachmentColor, 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
                }
            }

            DrawScrollbar(sprite, contentBounds, snapshot.Entries.Count, startIndex, visibleCount);

            if (_openedMemoId > 0 && TryGetEntry(snapshot, _openedMemoId, out MemoMailboxEntrySnapshot openedMemo))
            {
                DrawOpenedMemo(sprite, openedMemo, contentBounds);
            }
            else
            {
                sprite.DrawString(_font, snapshot.LastActionSummary ?? "Select a parcel row to inspect it.", new Vector2(contentBounds.X, contentBounds.Bottom - 18), new Color(97, 105, 117), 0f, Vector2.Zero, 0.37f, SpriteEffects.None, 0f);
            }
        }

        private void DrawScrollbar(SpriteBatch sprite, Rectangle contentBounds, int totalEntries, int startIndex, int visibleCount)
        {
            Rectangle track = new Rectangle(contentBounds.Right - 8, contentBounds.Y, 4, MaxVisibleEntries * RowHeight);
            sprite.Draw(_pixel, track, new Color(201, 208, 217));

            int thumbHeight = totalEntries <= visibleCount
                ? track.Height
                : Math.Max(18, (int)Math.Round(track.Height * (visibleCount / (float)totalEntries)));
            int thumbY = totalEntries <= visibleCount
                ? track.Y
                : track.Y + (int)Math.Round((track.Height - thumbHeight) * (startIndex / (float)Math.Max(1, totalEntries - visibleCount)));
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

            float drawY = panel.Y + 34;
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

        private void DrawComposeTab(SpriteBatch sprite, MemoMailboxDraftSnapshot snapshot, Rectangle bounds, bool quickMode)
        {
            Color labelColor = new(96, 105, 119);
            Color valueColor = new(55, 64, 77);

            float y = bounds.Y + 4;
            DrawField(sprite, "To", snapshot.Recipient, bounds.X, ref y, labelColor, valueColor);
            if (!quickMode)
            {
                DrawField(sprite, "Subject", snapshot.Subject, bounds.X, ref y, labelColor, valueColor);
            }
            DrawMultilineField(sprite, "Body", snapshot.Body, bounds.X, bounds.Width - 8, ref y, labelColor, valueColor, quickMode ? 4 : 5);
            DrawField(sprite, "Package", snapshot.AttachmentSummary, bounds.X, ref y, labelColor, valueColor);
            DrawField(sprite, "Mode", snapshot.ModeSummary, bounds.X, ref y, labelColor, new Color(84, 111, 138));

            if (snapshot.ShowTaxInfo)
            {
                Rectangle taxPanel = new Rectangle(bounds.X, bounds.Bottom - 54, bounds.Width - 8, 40);
                sprite.Draw(_pixel, taxPanel, new Color(247, 244, 229));
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
                Color hintColor = quickMode ? new Color(74, 134, 80) : new Color(89, 122, 158);
                string hint = quickMode
                    ? "Quick Send blocks item parcels; use the meso button or /memo draft meso."
                    : "Send keeps the full parcel draft; use the meso button or /memo draft item.";
                sprite.DrawString(_font, hint, new Vector2(bounds.X, bounds.Bottom - 30), hintColor, 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);
            }

            sprite.DrawString(_font, snapshot.LastActionSummary ?? string.Empty, new Vector2(bounds.X, bounds.Bottom - 16), new Color(96, 105, 119), 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
        }

        private void HandleLeftClick(MemoMailboxSnapshot snapshot, Point mousePosition)
        {
            for (int i = 0; i < _tabBounds.Count; i++)
            {
                if (_tabBounds[i].Contains(mousePosition))
                {
                    _tabSelected?.Invoke((ParcelDialogTab)i);
                    return;
                }
            }

            if (snapshot.ActiveTab != ParcelDialogTab.Receive)
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
            _dispatchRequested?.Invoke();
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
            int maxOffset = Math.Max(0, snapshot.Entries.Count - MaxVisibleEntries);
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

            if (_receiveGetButton != null)
            {
                _receiveGetButton.ButtonVisible = receiveVisible && _openedMemoId > 0;
            }

            if (_receiveDeleteButton != null)
            {
                _receiveDeleteButton.ButtonVisible = receiveVisible && _openedMemoId > 0;
            }

            if (_sendSendButton != null)
            {
                _sendSendButton.ButtonVisible = sendVisible;
            }

            if (_sendMesoButton != null)
            {
                _sendMesoButton.ButtonVisible = sendVisible;
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
            }

            if (_quickMesoButton != null)
            {
                _quickMesoButton.ButtonVisible = quickVisible;
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

        private void DrawField(SpriteBatch sprite, string label, string value, int x, ref float y, Color labelColor, Color valueColor)
        {
            sprite.DrawString(_font, label, new Vector2(x, y), labelColor, 0f, Vector2.Zero, 0.39f, SpriteEffects.None, 0f);
            sprite.DrawString(_font, Truncate(value, 48), new Vector2(x + 44, y), valueColor, 0f, Vector2.Zero, 0.41f, SpriteEffects.None, 0f);
            y += 17f;
        }

        private void DrawMultilineField(
            SpriteBatch sprite,
            string label,
            string value,
            int x,
            int width,
            ref float y,
            Color labelColor,
            Color valueColor,
            int maxLines)
        {
            sprite.DrawString(_font, label, new Vector2(x, y), labelColor, 0f, Vector2.Zero, 0.39f, SpriteEffects.None, 0f);
            y += 14f;

            int lineCount = 0;
            foreach (string line in WrapText(value, width, 0.39f))
            {
                sprite.DrawString(_font, line, new Vector2(x + 4, y), valueColor, 0f, Vector2.Zero, 0.39f, SpriteEffects.None, 0f);
                y += 12f;
                lineCount++;
                if (lineCount >= maxLines)
                {
                    break;
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

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield return string.Empty;
                yield break;
            }

            string[] words = text.Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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
