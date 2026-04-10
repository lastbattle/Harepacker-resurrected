using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class QuestDeliveryWindow : UIWindowBase
    {
        internal readonly struct IconFrame
        {
            public IconFrame(Texture2D texture, Point origin, int delayMs)
            {
                Texture = texture;
                Origin = origin;
                DelayMs = Math.Max(1, delayMs);
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
            public int DelayMs { get; }
        }

        internal sealed class DeliveryEntry
        {
            public int QuestId { get; init; }
            public int DisplayQuestId { get; init; }
            public int TargetNpcId { get; init; }
            public string Title { get; init; } = string.Empty;
            public string NpcName { get; init; } = string.Empty;
            public string StatusText { get; init; } = string.Empty;
            public string DetailText { get; init; } = string.Empty;
            public string DeliveryCashItemName { get; init; } = string.Empty;
            public bool CompletionPhase { get; init; }
            public bool CanConfirm { get; init; }
            public bool IsBlocked { get; init; }
            public bool IsSeriesRepresentative { get; init; }
            public InventoryType DeliveryCashInventoryType { get; init; }
            public int DeliveryCashItemRuntimeSlotIndex { get; init; } = -1;
            public int DeliveryCashItemClientSlotIndex { get; init; }
        }

        private const int VisibleRowCount = 4;
        private const int RowHeight = 17;
        private static readonly Point IconAnchor = new(41, 50);
        private const int HeaderTop = 12;
        private const int ListLeft = 14;
        private const int ListTop = 68;
        private const int ListWidth = 136;
        private const int ScrollLeft = 154;
        private const int ScrollTop = 69;
        private const int ScrollHeight = 61;
        private const int DetailLeft = 170;
        private const int DetailTop = 14;

        private readonly IconFrame[] _iconFrames;
        private readonly Texture2D _rowTexture;
        private readonly Texture2D _selectedRowTexture;
        private readonly Texture2D _scrollThumbTexture;
        private readonly Texture2D _dividerTexture;
        private readonly Texture2D _pixel;

        private readonly List<DeliveryEntry> _entries = new();

        private UIObject _okButton;
        private UIObject _cancelButton;
        private MouseState _previousMouseState;
        private int _questId;
        private int _itemId;
        private int _updatedAtTick = int.MinValue;
        private string _itemName = string.Empty;
        private int _selectedIndex = -1;
        private int _scrollOffset;

        public QuestDeliveryWindow(
            IDXObject frame,
            IconFrame[] iconFrames,
            Texture2D rowTexture,
            Texture2D selectedRowTexture,
            Texture2D scrollThumbTexture,
            Texture2D dividerTexture,
            GraphicsDevice device)
            : base(frame)
        {
            _iconFrames = iconFrames ?? Array.Empty<IconFrame>();
            _rowTexture = rowTexture;
            _selectedRowTexture = selectedRowTexture;
            _scrollThumbTexture = scrollThumbTexture;
            _dividerTexture = dividerTexture;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.QuestDelivery;
        public override bool SupportsDragging => true;

        internal event Action<DeliveryEntry> DeliveryRequested;

        public void Configure(int questId, int itemId, IReadOnlyList<DeliveryEntry> entries, int updatedAtTick)
        {
            _questId = Math.Max(0, questId);
            _itemId = Math.Max(0, itemId);
            _updatedAtTick = updatedAtTick;
            _itemName = InventoryItemMetadataResolver.TryResolveItemName(_itemId, out string resolvedItemName)
                ? resolvedItemName
                : _itemId > 0
                    ? $"Item {_itemId}"
                    : "Unknown delivery item";

            DeliveryEntry previousEntry = GetSelectedEntry();
            int previousSelectionQuestId = GetSelectionQuestId(previousEntry) ?? _questId;
            int previousQuestId = previousEntry?.QuestId ?? _questId;
            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries.Where(entry => entry != null));
            }

            _selectedIndex = ResolveSelectedIndex(previousSelectionQuestId, previousQuestId);
            _scrollOffset = ClampScrollOffset(_scrollOffset);
            EnsureSelectedVisible();
            UpdateButtonStates();
        }

        public void InitializeButtons(UIObject okButton, UIObject cancelButton)
        {
            _okButton = okButton;
            _cancelButton = cancelButton;

            ConfigureButton(_okButton, ConfirmSelection);
            ConfigureButton(_cancelButton, Hide);
            PositionButtons();
            UpdateButtonStates();
        }

        public override void SetFont(SpriteFont font)
        {
            base.SetFont(font);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            PositionButtons();
            UpdateButtonStates();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            Rectangle rowArea = GetRowAreaBounds();
            if (rowArea.Contains(mouseState.X, mouseState.Y))
            {
                int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
                if (wheelDelta != 0 && _entries.Count > VisibleRowCount)
                {
                    _scrollOffset = ClampScrollOffset(_scrollOffset - Math.Sign(wheelDelta));
                    _previousMouseState = mouseState;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }

                bool released = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
                if (released)
                {
                    int rowIndex = (mouseState.Y - rowArea.Y) / RowHeight;
                    int absoluteIndex = _scrollOffset + rowIndex;
                    if (absoluteIndex >= 0 && absoluteIndex < _entries.Count)
                    {
                        _selectedIndex = absoluteIndex;
                        EnsureSelectedVisible();
                        UpdateButtonStates();
                        _previousMouseState = mouseState;
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        return true;
                    }
                }
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
            int TickCount)
        {
            if (!CanDrawWindowText)
            {
                return;
            }

            DrawIcon(sprite, TickCount);
            DrawHeader(sprite);
            DrawList(sprite);
            DrawSelectedEntryDetails(sprite);
            DrawFooter(sprite);
        }

        private void DrawIcon(SpriteBatch sprite, int tickCount)
        {
            if (_iconFrames.Length == 0)
            {
                return;
            }

            IconFrame frame = ResolveIconFrame(tickCount);
            if (frame.Texture != null)
            {
                Vector2 drawPosition = new(
                    Position.X + IconAnchor.X - frame.Origin.X,
                    Position.Y + IconAnchor.Y - frame.Origin.Y);
                sprite.Draw(frame.Texture, drawPosition, Color.White);
            }
        }

        private IconFrame ResolveIconFrame(int tickCount)
        {
            if (_iconFrames.Length == 1)
            {
                return _iconFrames[0];
            }

            int totalDelay = 0;
            for (int i = 0; i < _iconFrames.Length; i++)
            {
                totalDelay += _iconFrames[i].DelayMs;
            }

            if (totalDelay <= 0)
            {
                return _iconFrames[0];
            }

            int time = Math.Abs(tickCount % totalDelay);
            for (int i = 0; i < _iconFrames.Length; i++)
            {
                if (time < _iconFrames[i].DelayMs)
                {
                    return _iconFrames[i];
                }

                time -= _iconFrames[i].DelayMs;
            }

            return _iconFrames[_iconFrames.Length - 1];
        }

        private void DrawHeader(SpriteBatch sprite)
        {
            float headerX = Position.X + 56f;
            float headerY = Position.Y + HeaderTop;
            DrawLine(sprite, "QUEST DELIVERY", new Vector2(headerX, headerY), Color.White, 0.56f);
            DrawLine(sprite, $"Quest #{_questId}", new Vector2(headerX, headerY + 20f), new Color(238, 219, 170), 0.42f);
            DrawLine(sprite, _itemName, new Vector2(headerX, headerY + 36f), new Color(66, 44, 26), 0.52f);
            DrawLine(sprite, $"Item ID: {_itemId}", new Vector2(headerX, headerY + 53f), new Color(93, 77, 61), 0.36f);
        }

        private void DrawList(SpriteBatch sprite)
        {
            Rectangle listBounds = GetRowAreaBounds();
            for (int row = 0; row < VisibleRowCount; row++)
            {
                int index = _scrollOffset + row;
                Rectangle rowBounds = new(listBounds.X, listBounds.Y + (row * RowHeight), listBounds.Width, RowHeight - 1);

                DrawRowBackground(sprite, rowBounds, index == _selectedIndex);
                if (_dividerTexture != null)
                {
                    sprite.Draw(_dividerTexture, new Vector2(rowBounds.X, rowBounds.Bottom - 1), Color.White);
                }
                else
                {
                    sprite.Draw(_pixel, new Rectangle(rowBounds.X, rowBounds.Bottom - 1, rowBounds.Width, 1), new Color(92, 78, 62, 80));
                }

                if (index >= _entries.Count)
                {
                    continue;
                }

                DeliveryEntry entry = _entries[index];
                Color titleColor = entry.CanConfirm
                    ? new Color(77, 54, 32)
                    : new Color(120, 110, 100);
                Color statusColor = entry.IsBlocked
                    ? new Color(161, 79, 67)
                    : entry.CompletionPhase
                        ? new Color(52, 101, 61)
                        : new Color(76, 95, 132);
                string titleText = entry.IsSeriesRepresentative && entry.DisplayQuestId > 0 && entry.DisplayQuestId != entry.QuestId
                    ? $"#{entry.DisplayQuestId} -> {entry.Title}"
                    : entry.Title;

                DrawLine(sprite, Truncate(titleText, 22), new Vector2(rowBounds.X + 5, rowBounds.Y + 1), titleColor, 0.36f);
                DrawLine(sprite, Truncate(entry.StatusText, 24), new Vector2(rowBounds.X + 5, rowBounds.Y + 8), statusColor, 0.31f);
            }

            DrawScrollbar(sprite);
        }

        private void DrawSelectedEntryDetails(SpriteBatch sprite)
        {
            DeliveryEntry entry = GetSelectedEntry();
            string title = entry?.Title ?? "No delivery quests matched this packet payload.";
            string status = entry == null
                ? "The packet did not resolve to a worthy accept or complete quest in the simulator quest runtime."
                : entry.StatusText;
            string detail = entry == null
                ? "The client keeps this owner alive only when a usable item-backed delivery target survives the packet and unique-modeless gating."
                : entry.DetailText;
            string seriesText = entry?.IsSeriesRepresentative == true && entry.DisplayQuestId > 0 && entry.DisplayQuestId != entry.QuestId
                ? $"Showing series representative quest #{entry.DisplayQuestId} for actual quest #{entry.QuestId}."
                : $"Quest #{entry?.QuestId ?? _questId}";
            string deliveryItemText = !string.IsNullOrWhiteSpace(entry?.DeliveryCashItemName)
                ? entry.DeliveryCashItemName
                : _itemName;
            string slotText = entry?.DeliveryCashItemClientSlotIndex > 0
                ? $"Item slot: {entry.DeliveryCashInventoryType} #{entry.DeliveryCashItemClientSlotIndex}"
                : "Item slot: unresolved";

            float left = Position.X + DetailLeft;
            float y = Position.Y + DetailTop;
            DrawLine(sprite, title, new Vector2(left, y), new Color(66, 44, 26), 0.46f);
            y += 18f;
            DrawLine(sprite, status, new Vector2(left, y), entry?.IsBlocked == true ? new Color(161, 79, 67) : new Color(84, 92, 104), 0.34f);
            y += 16f;
            DrawLine(sprite, Truncate(seriesText, 42), new Vector2(left, y), new Color(120, 108, 97), 0.31f);
            y += 14f;
            DrawLine(sprite, Truncate($"Delivery item: {deliveryItemText}", 42), new Vector2(left, y), new Color(120, 108, 97), 0.31f);
            y += 14f;
            DrawLine(sprite, Truncate(slotText, 42), new Vector2(left, y), new Color(120, 108, 97), 0.31f);
            y += 14f;

            foreach (string line in WrapText(detail, Math.Max(120f, (CurrentFrame?.Width ?? 312) - 36f), 0.34f).Take(3))
            {
                DrawLine(sprite, line, new Vector2(left, y), new Color(96, 84, 70), 0.34f);
                y += 13f;
            }
        }

        private void DrawFooter(SpriteBatch sprite)
        {
            string footer = _updatedAtTick == int.MinValue
                ? "Packet-owned delivery request idle."
                : $"Request stamp: {_updatedAtTick.ToString(CultureInfo.InvariantCulture)}";
            DrawLine(sprite, footer, new Vector2(Position.X + 166, Position.Y + 110), new Color(120, 108, 97), 0.28f);
        }

        private void DrawRowBackground(SpriteBatch sprite, Rectangle bounds, bool selected)
        {
            Texture2D texture = selected ? _selectedRowTexture ?? _rowTexture : _rowTexture;
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
                return;
            }

            Color fill = selected
                ? new Color(255, 240, 214, 210)
                : new Color(255, 255, 255, 120);
            sprite.Draw(_pixel, bounds, fill);
        }

        private void DrawScrollbar(SpriteBatch sprite)
        {
            Rectangle trackBounds = new(Position.X + ScrollLeft, Position.Y + ScrollTop, 10, ScrollHeight);
            sprite.Draw(_pixel, trackBounds, new Color(116, 101, 84, 48));

            if (_entries.Count <= VisibleRowCount)
            {
                return;
            }

            int maxOffset = Math.Max(1, _entries.Count - VisibleRowCount);
            float visibleRatio = VisibleRowCount / (float)_entries.Count;
            int thumbHeight = Math.Max(12, (int)Math.Round(trackBounds.Height * visibleRatio));
            int thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbY = trackBounds.Y + (int)Math.Round((_scrollOffset / (float)maxOffset) * thumbTravel);
            Rectangle thumbBounds = new(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);

            if (_scrollThumbTexture != null)
            {
                sprite.Draw(_scrollThumbTexture, new Vector2(thumbBounds.X, thumbBounds.Y), Color.White);
            }
            else
            {
                sprite.Draw(_pixel, thumbBounds, new Color(137, 114, 82));
            }
        }

        private void ConfirmSelection()
        {
            DeliveryEntry entry = GetSelectedEntry();
            if (entry == null || !entry.CanConfirm)
            {
                return;
            }

            DeliveryRequested?.Invoke(entry);
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

        private void PositionButtons()
        {
            if (_okButton != null)
            {
                _okButton.X = Math.Max(12, (CurrentFrame?.Width ?? 312) - _okButton.CanvasSnapshotWidth - 76);
                _okButton.Y = Math.Max(8, (CurrentFrame?.Height ?? 132) - _okButton.CanvasSnapshotHeight - 10);
            }

            if (_cancelButton != null)
            {
                _cancelButton.X = Math.Max(12, (CurrentFrame?.Width ?? 312) - _cancelButton.CanvasSnapshotWidth - 12);
                _cancelButton.Y = Math.Max(8, (CurrentFrame?.Height ?? 132) - _cancelButton.CanvasSnapshotHeight - 10);
            }
        }

        private void UpdateButtonStates()
        {
            DeliveryEntry entry = GetSelectedEntry();
            _okButton?.SetEnabled(entry?.CanConfirm == true);
            _cancelButton?.SetEnabled(true);
        }

        private Rectangle GetRowAreaBounds()
        {
            return new Rectangle(Position.X + ListLeft, Position.Y + ListTop, ListWidth, VisibleRowCount * RowHeight);
        }

        private DeliveryEntry GetSelectedEntry()
        {
            return _selectedIndex >= 0 && _selectedIndex < _entries.Count
                ? _entries[_selectedIndex]
                : null;
        }

        private int ResolveSelectedIndex(int preferredSelectionQuestId, int preferredQuestId)
        {
            if (_entries.Count == 0)
            {
                return -1;
            }

            if (preferredSelectionQuestId > 0)
            {
                int preferredIndex = _entries.FindIndex(entry => GetSelectionQuestId(entry) == preferredSelectionQuestId);
                if (preferredIndex >= 0)
                {
                    return preferredIndex;
                }
            }

            if (preferredQuestId > 0)
            {
                int preferredIndex = _entries.FindIndex(entry => entry.QuestId == preferredQuestId);
                if (preferredIndex >= 0)
                {
                    return preferredIndex;
                }
            }

            int actionableIndex = _entries.FindIndex(entry => entry.CanConfirm);
            return actionableIndex >= 0 ? actionableIndex : 0;
        }

        private static int? GetSelectionQuestId(DeliveryEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return entry.IsSeriesRepresentative && entry.DisplayQuestId > 0
                ? entry.DisplayQuestId
                : entry.QuestId > 0
                    ? entry.QuestId
                    : null;
        }

        private void EnsureSelectedVisible()
        {
            if (_selectedIndex < 0)
            {
                _scrollOffset = 0;
                return;
            }

            if (_selectedIndex < _scrollOffset)
            {
                _scrollOffset = _selectedIndex;
            }
            else if (_selectedIndex >= (_scrollOffset + VisibleRowCount))
            {
                _scrollOffset = _selectedIndex - (VisibleRowCount - 1);
            }

            _scrollOffset = ClampScrollOffset(_scrollOffset);
        }

        private int ClampScrollOffset(int offset)
        {
            return Math.Clamp(offset, 0, Math.Max(0, _entries.Count - VisibleRowCount));
        }

        private void DrawLine(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            DrawWindowText(sprite, text, position + new Vector2(1f, 1f), new Color(24, 24, 24, 180), scale);
            DrawWindowText(sprite, text, position, color, scale);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Replace("\r", " ").Replace("\n", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentLine = string.Empty;

            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                if (!string.IsNullOrEmpty(currentLine) && MeasureWindowText(null, candidate, scale).X > maxWidth)
                {
                    yield return currentLine;
                    currentLine = words[i];
                    continue;
                }

                currentLine = candidate;
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                yield return currentLine;
            }
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return $"{text[..Math.Max(0, maxLength - 3)]}...";
        }
    }
}
