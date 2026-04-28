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
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    internal sealed class QuestRewardRaiseWindow : UIWindowBase
    {
        private const int DefaultWidth = 332;
        private const int DefaultHeight = 272;
        private const int TitleTop = 16;
        private const int SubtitleTop = 36;
        private const int PromptTop = 58;
        private const int PreviewLeft = 18;
        private const int PreviewTop = 92;
        private const int PreviewSize = 74;
        private const int ListLeft = 104;
        private const int ListTop = 92;
        private const int ListWidth = 208;
        private const int ListHeight = 106;
        private const int RowHeight = 24;
        private const int FooterTop = 210;
        private const int DragBandHeight = 20;
        private const int PiecePreviewCaptionTop = 172;
        private const int PiecePreviewDetailTop = 188;

        private readonly Texture2D _pixel;
        private readonly Texture2D _backgroundTopLeft;
        private readonly Texture2D _backgroundTopCenter;
        private readonly Texture2D _backgroundTopRight;
        private readonly Texture2D _backgroundMiddleLeft;
        private readonly Texture2D _backgroundMiddleCenter;
        private readonly Texture2D _backgroundMiddleRight;
        private readonly Texture2D _backgroundBottomLeft;
        private readonly Texture2D _backgroundBottomCenter;
        private readonly Texture2D _backgroundBottomRight;
        private readonly Texture2D _previewBackdrop;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();

        private Func<int, Texture2D> _itemIconProvider;
        private UIObject _confirmButton;
        private UIObject _cancelButton;
        private MouseState _previousMouseState;
        private QuestRewardChoicePrompt _prompt;
        private QuestRewardChoiceGroup _group;
        private QuestRewardRaiseState _state;
        private int _groupIndex;
        private int _selectedIndex;
        private bool _suppressCancelEvent;

        public QuestRewardRaiseWindow(
            IDXObject frame,
            Texture2D backgroundTopLeft,
            Texture2D backgroundTopCenter,
            Texture2D backgroundTopRight,
            Texture2D backgroundMiddleLeft,
            Texture2D backgroundMiddleCenter,
            Texture2D backgroundMiddleRight,
            Texture2D backgroundBottomLeft,
            Texture2D backgroundBottomCenter,
            Texture2D backgroundBottomRight,
            Texture2D previewBackdrop,
            GraphicsDevice device)
            : base(frame)
        {
            _backgroundTopLeft = backgroundTopLeft;
            _backgroundTopCenter = backgroundTopCenter;
            _backgroundTopRight = backgroundTopRight;
            _backgroundMiddleLeft = backgroundMiddleLeft;
            _backgroundMiddleCenter = backgroundMiddleCenter;
            _backgroundMiddleRight = backgroundMiddleRight;
            _backgroundBottomLeft = backgroundBottomLeft;
            _backgroundBottomCenter = backgroundBottomCenter;
            _backgroundBottomRight = backgroundBottomRight;
            _previewBackdrop = previewBackdrop;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.QuestRewardRaise;

        internal event Action<int> SelectionConfirmed;
        internal event Action PlacementConfirmed;
        internal event Action CancelRequested;
        internal event Action<QuestRewardRaisePieceDropRequest> PieceDropRequested;
        internal event Action<int> PieceRemovalRequested;

        internal void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
        }

        internal void Configure(QuestRewardRaiseState state)
        {
            int previousSelectedItemId = GetSelectedOption()?.ItemId ?? 0;
            _state = state;
            _prompt = state?.Prompt;
            _groupIndex = state?.GroupIndex ?? 0;
            _group = _prompt?.Groups != null && _groupIndex >= 0 && _groupIndex < _prompt.Groups.Count
                ? _prompt.Groups[_groupIndex]
                : null;
            _selectedIndex = ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement
                ? Math.Clamp(_selectedIndex, 0, Math.Max(0, (_state?.PlacedPieces?.Count ?? 1) - 1))
                : ResolveSelectedOptionIndex(previousSelectedItemId);
            if (state != null && state.WindowPosition != Point.Zero)
            {
                Position = state.WindowPosition;
            }

            UpdateButtonStates();
        }

        internal void InitializeButtons(UIObject confirmButton, UIObject cancelButton)
        {
            _confirmButton = confirmButton;
            _cancelButton = cancelButton;

            ConfigureButton(_confirmButton, ConfirmSelection);
            ConfigureButton(_cancelButton, CancelSelection);
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
            if (_state != null)
            {
                _state.WindowPosition = Position;
            }

            PositionButtons();
            UpdateButtonStates();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            Rectangle listBounds = GetListBounds();
            if (listBounds.Contains(mouseState.X, mouseState.Y))
            {
                bool released = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
                bool rightReleased = mouseState.RightButton == ButtonState.Released && _previousMouseState.RightButton == ButtonState.Pressed;
                int hoverIndex = (mouseState.Y - listBounds.Y) / RowHeight;
                if (ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement)
                {
                    int placedCount = _state?.PlacedPieces?.Count ?? 0;
                    if (hoverIndex >= 0 && hoverIndex < placedCount)
                    {
                        _selectedIndex = hoverIndex;
                        UpdateButtonStates();
                    }

                    if (rightReleased && hoverIndex >= 0 && hoverIndex < placedCount)
                    {
                        PieceRemovalRequested?.Invoke(_state.PlacedPieces[hoverIndex].RequestId);
                        _previousMouseState = mouseState;
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        return true;
                    }

                    if (released && hoverIndex >= 0 && hoverIndex < placedCount)
                    {
                        _selectedIndex = hoverIndex;
                        UpdateButtonStates();
                        _previousMouseState = mouseState;
                        mouseCursor?.SetMouseCursorMovedToClickableItem();
                        return true;
                    }
                }
                else
                {
                    if (_group?.Options != null && hoverIndex >= 0 && hoverIndex < _group.Options.Count)
                    {
                        _selectedIndex = hoverIndex;
                        UpdateButtonStates();
                    }

                    if (released && _group?.Options != null)
                    {
                        int rowIndex = (mouseState.Y - listBounds.Y) / RowHeight;
                        if (rowIndex >= 0 && rowIndex < _group.Options.Count)
                        {
                            _selectedIndex = rowIndex;
                            UpdateButtonStates();
                            _previousMouseState = mouseState;
                            mouseCursor?.SetMouseCursorMovedToClickableItem();
                            return true;
                        }
                    }
                }
            }

            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            _previousMouseState = mouseState;
            return handled;
        }

        public override void Hide()
        {
            bool wasVisible = IsVisible;
            base.Hide();
            if (wasVisible && !_suppressCancelEvent)
            {
                CancelRequested?.Invoke();
            }

            _suppressCancelEvent = false;
        }

        protected override void OnCloseButtonClicked(UIObject sender)
        {
            CancelSelection();
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
            DrawBackground(sprite);
            if (!CanDrawWindowText)
            {
                return;
            }

            DrawHeader(sprite);
            DrawPreview(sprite);
            DrawOptions(sprite);
            DrawFooter(sprite);
        }

        protected override void DrawOverlay(
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
            DrawButtonLabel(sprite, _confirmButton, string.IsNullOrWhiteSpace(_prompt?.ActionLabel) ? "OK" : _prompt.ActionLabel);
            DrawButtonLabel(sprite, _cancelButton, "Cancel");
        }

        public override bool CanStartDragAt(int x, int y)
        {
            if (!base.CanStartDragAt(x, y))
            {
                return false;
            }

            Rectangle bounds = GetWindowBounds();
            return y >= bounds.Y && y < bounds.Y + DragBandHeight;
        }

        internal bool TryHandleInventoryDrop(
            int mouseX,
            int mouseY,
            InventoryType sourceInventoryType,
            int sourceSlotIndex,
            InventorySlotData draggedSlotData)
        {
            if (!IsVisible
                || ResolveWindowMode() != QuestRewardRaiseWindowMode.PiecePlacement
                || draggedSlotData == null
                || draggedSlotData.IsDisabled)
            {
                return false;
            }

            Rectangle previewBounds = new(Position.X + PreviewLeft, Position.Y + PreviewTop, PreviewSize, PreviewSize);
            Rectangle listBounds = GetListBounds();
            if (!previewBounds.Contains(mouseX, mouseY) && !listBounds.Contains(mouseX, mouseY))
            {
                return false;
            }

            PieceDropRequested?.Invoke(new QuestRewardRaisePieceDropRequest(
                sourceInventoryType,
                sourceSlotIndex,
                draggedSlotData.Clone()));
            return true;
        }

        private void ConfigureButton(UIObject button, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => action();
        }

        private void PositionButtons()
        {
            if (_confirmButton != null)
            {
                _confirmButton.X = (CurrentFrame?.Width ?? DefaultWidth) - 154;
                _confirmButton.Y = (CurrentFrame?.Height ?? DefaultHeight) - 38;
            }

            if (_cancelButton != null)
            {
                _cancelButton.X = (CurrentFrame?.Width ?? DefaultWidth) - 78;
                _cancelButton.Y = (CurrentFrame?.Height ?? DefaultHeight) - 38;
            }
        }

        private void UpdateButtonStates()
        {
            if (_confirmButton != null)
            {
                bool enabled = ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement
                    ? (_state?.PlacedPieces?.Count ?? 0) > 0
                    : _selectedIndex >= 0 && _group?.Options?.Count > 0;
                _confirmButton.SetEnabled(enabled);
            }
        }

        private void ConfirmSelection()
        {
            if (ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement)
            {
                if ((_state?.PlacedPieces?.Count ?? 0) > 0)
                {
                    PlacementConfirmed?.Invoke();
                }

                return;
            }

            if (_group?.Options == null || _selectedIndex < 0 || _selectedIndex >= _group.Options.Count)
            {
                return;
            }

            SelectionConfirmed?.Invoke(_group.Options[_selectedIndex].ItemId);
        }

        private void CancelSelection()
        {
            _suppressCancelEvent = false;
            Hide();
        }

        internal void DismissWithoutCancelling()
        {
            _suppressCancelEvent = true;
            Hide();
        }

        private void DrawBackground(SpriteBatch sprite)
        {
            Rectangle bounds = GetWindowBounds();
            int leftWidth = Math.Max(_backgroundTopLeft?.Width ?? 5, _backgroundMiddleLeft?.Width ?? 5);
            int rightWidth = Math.Max(_backgroundTopRight?.Width ?? 5, _backgroundMiddleRight?.Width ?? 5);
            int topHeight = Math.Max(_backgroundTopLeft?.Height ?? 20, _backgroundTopCenter?.Height ?? 20);
            int bottomHeight = Math.Max(_backgroundBottomLeft?.Height ?? 5, _backgroundBottomCenter?.Height ?? 5);
            int middleHeight = Math.Max(1, bounds.Height - topHeight - bottomHeight);
            int middleWidth = Math.Max(1, bounds.Width - leftWidth - rightWidth);

            DrawStretched(sprite, _backgroundTopLeft, new Rectangle(bounds.X, bounds.Y, leftWidth, topHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundTopCenter, new Rectangle(bounds.X + leftWidth, bounds.Y, middleWidth, topHeight), new Color(255, 255, 255, 248));
            DrawStretched(sprite, _backgroundTopRight, new Rectangle(bounds.Right - rightWidth, bounds.Y, rightWidth, topHeight), new Color(255, 255, 255, 248));

            DrawTiled(sprite, _backgroundMiddleLeft, new Rectangle(bounds.X, bounds.Y + topHeight, leftWidth, middleHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundMiddleCenter, new Rectangle(bounds.X + leftWidth, bounds.Y + topHeight, middleWidth, middleHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundMiddleRight, new Rectangle(bounds.Right - rightWidth, bounds.Y + topHeight, rightWidth, middleHeight), new Color(255, 255, 255, 248));

            DrawStretched(sprite, _backgroundBottomLeft, new Rectangle(bounds.X, bounds.Bottom - bottomHeight, leftWidth, bottomHeight), new Color(255, 255, 255, 248));
            DrawTiled(sprite, _backgroundBottomCenter, new Rectangle(bounds.X + leftWidth, bounds.Bottom - bottomHeight, middleWidth, bottomHeight), new Color(255, 255, 255, 248));
            DrawStretched(sprite, _backgroundBottomRight, new Rectangle(bounds.Right - rightWidth, bounds.Bottom - bottomHeight, rightWidth, bottomHeight), new Color(255, 255, 255, 248));

            sprite.Draw(_pixel, new Rectangle(bounds.X + 12, bounds.Y + 72, bounds.Width - 24, 1), new Color(84, 69, 48, 84));
            sprite.Draw(_pixel, new Rectangle(bounds.X + 12, bounds.Y + FooterTop - 10, bounds.Width - 24, 1), new Color(84, 69, 48, 84));
        }

        private void DrawHeader(SpriteBatch sprite)
        {
            string title = string.IsNullOrWhiteSpace(_prompt?.QuestName) ? "Quest Reward" : _prompt.QuestName;
            string subtitle = ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement
                ? BuildPiecePlacementSubtitle()
                : _prompt == null
                    ? "Raise window unavailable."
                    : $"{(_prompt.CompletionPhase ? "Completion" : "Acceptance")} choice {_groupIndex + 1}/{Math.Max(1, _prompt.Groups?.Count ?? 1)}";
            string promptText = ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement
                ? BuildPiecePlacementPromptText()
                : _group?.PromptText ?? "No reward choice is active.";

            DrawText(sprite, title, new Vector2(Position.X + 16, Position.Y + TitleTop), new Color(62, 39, 22), 0.58f);
            DrawText(sprite, subtitle, new Vector2(Position.X + 16, Position.Y + SubtitleTop), new Color(132, 104, 78), 0.38f);

            float promptY = Position.Y + PromptTop;
            foreach (string line in WrapText(promptText, (CurrentFrame?.Width ?? DefaultWidth) - 32f, 0.38f).Take(2))
            {
                DrawText(sprite, line, new Vector2(Position.X + 16, promptY), new Color(86, 68, 48), 0.38f);
                promptY += 14f;
            }
        }

        private void DrawPreview(SpriteBatch sprite)
        {
            Rectangle previewBounds = new(Position.X + PreviewLeft, Position.Y + PreviewTop, PreviewSize, PreviewSize);
            if (_previewBackdrop != null)
            {
                float backdropScale = Math.Min(previewBounds.Width / (float)_previewBackdrop.Width, previewBounds.Height / (float)_previewBackdrop.Height);
                Vector2 backdropPosition = new(
                    previewBounds.X + ((previewBounds.Width - (_previewBackdrop.Width * backdropScale)) / 2f),
                    previewBounds.Y + ((previewBounds.Height - (_previewBackdrop.Height * backdropScale)) / 2f));
                sprite.Draw(_previewBackdrop, backdropPosition, null, new Color(255, 255, 255, 216), 0f, Vector2.Zero, backdropScale, SpriteEffects.None, 0f);
            }
            else
            {
                sprite.Draw(_pixel, previewBounds, new Color(255, 250, 241, 230));
            }

            sprite.Draw(_pixel, new Rectangle(previewBounds.X, previewBounds.Y, previewBounds.Width, 1), new Color(124, 102, 74));
            sprite.Draw(_pixel, new Rectangle(previewBounds.X, previewBounds.Bottom - 1, previewBounds.Width, 1), new Color(124, 102, 74));
            sprite.Draw(_pixel, new Rectangle(previewBounds.X, previewBounds.Y, 1, previewBounds.Height), new Color(124, 102, 74));
            sprite.Draw(_pixel, new Rectangle(previewBounds.Right - 1, previewBounds.Y, 1, previewBounds.Height), new Color(124, 102, 74));

            QuestRewardChoiceOption selectedOption = GetSelectedOption();
            QuestRewardRaisePlacedPiece selectedPiece = GetSelectedPlacedPiece();
            Texture2D icon = ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement
                ? ResolveItemIcon(selectedPiece?.ItemId ?? 0)
                : selectedOption != null ? ResolveItemIcon(selectedOption.ItemId) : null;
            if (icon != null)
            {
                float iconScale = Math.Min(1f, Math.Min((PreviewSize - 24f) / icon.Width, (PreviewSize - 24f) / icon.Height));
                Vector2 iconPosition = new(
                    previewBounds.X + ((previewBounds.Width - (icon.Width * iconScale)) / 2f),
                    previewBounds.Y + ((previewBounds.Height - (icon.Height * iconScale)) / 2f));
                sprite.Draw(icon, iconPosition, null, Color.White, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
            }

            if (!CanDrawWindowText)
            {
                return;
            }

            if (ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement)
            {
                DrawText(sprite, selectedPiece?.Label ?? "Drop a piece", new Vector2(Position.X + 18, Position.Y + PiecePreviewCaptionTop), new Color(72, 50, 31), 0.34f);
                DrawText(sprite, BuildPiecePreviewDetail(selectedPiece), new Vector2(Position.X + 18, Position.Y + PiecePreviewDetailTop), new Color(132, 104, 78), 0.31f);
                return;
            }

            DrawText(sprite, selectedOption?.Label ?? "Select a reward", new Vector2(Position.X + 18, Position.Y + PiecePreviewCaptionTop), new Color(72, 50, 31), 0.34f);
            DrawText(sprite, $"Quest #{_prompt?.QuestId ?? 0}", new Vector2(Position.X + 18, Position.Y + PiecePreviewDetailTop), new Color(132, 104, 78), 0.31f);
        }

        private void DrawOptions(SpriteBatch sprite)
        {
            Rectangle listBounds = GetListBounds();
            if (ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement)
            {
                DrawPlacedPieces(sprite, listBounds);
                return;
            }

            IReadOnlyList<QuestRewardChoiceOption> options = _group?.Options ?? Array.Empty<QuestRewardChoiceOption>();
            int visibleRows = Math.Max(1, listBounds.Height / RowHeight);
            for (int i = 0; i < visibleRows; i++)
            {
                Rectangle rowBounds = new(listBounds.X, listBounds.Y + (i * RowHeight), listBounds.Width, RowHeight - 2);
                bool selected = i == _selectedIndex && i < options.Count;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(216, 196, 165, 205) : new Color(255, 252, 246, 172));
                sprite.Draw(_pixel, new Rectangle(rowBounds.X, rowBounds.Bottom, rowBounds.Width, 1), new Color(120, 103, 84, 70));

                if (i >= options.Count)
                {
                    continue;
                }

                QuestRewardChoiceOption option = options[i];
                Texture2D icon = ResolveItemIcon(option.ItemId);
                if (icon != null)
                {
                    float iconScale = Math.Min(1f, Math.Min(18f / icon.Width, 18f / icon.Height));
                    sprite.Draw(icon, new Vector2(rowBounds.X + 4, rowBounds.Y + 3), null, Color.White, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
                }

                DrawText(sprite, Truncate(option.Label, 26), new Vector2(rowBounds.X + 26, rowBounds.Y + 2), new Color(66, 44, 26), 0.34f);
                DrawText(sprite, Truncate(option.DetailText, 40), new Vector2(rowBounds.X + 26, rowBounds.Y + 11), new Color(120, 96, 74), 0.28f);
            }
        }

        private void DrawFooter(SpriteBatch sprite)
        {
            string footer;
            if (ResolveWindowMode() == QuestRewardRaiseWindowMode.PiecePlacement)
            {
                footer = BuildPiecePlacementFooter();
            }
            else
            {
                string actionLabel = string.IsNullOrWhiteSpace(_prompt?.ActionLabel) ? "Confirm" : _prompt.ActionLabel;
                QuestRewardChoiceOption selectedOption = GetSelectedOption();
                footer = _group?.Options == null || _group.Options.Count == 0
                    ? "No valid reward choices remain."
                    : string.IsNullOrWhiteSpace(selectedOption?.DetailText)
                        ? $"Use {actionLabel} to lock in the highlighted reward."
                        : selectedOption.DetailText;
            }

            DrawText(sprite, footer, new Vector2(Position.X + 16, Position.Y + FooterTop), new Color(104, 86, 65), 0.32f);
        }

        private Rectangle GetListBounds()
        {
            return new Rectangle(Position.X + ListLeft, Position.Y + ListTop, ListWidth, ListHeight);
        }

        private QuestRewardChoiceOption GetSelectedOption()
        {
            if (_group?.Options == null || _selectedIndex < 0 || _selectedIndex >= _group.Options.Count)
            {
                return null;
            }

            return _group.Options[_selectedIndex];
        }

        private QuestRewardRaisePlacedPiece GetSelectedPlacedPiece()
        {
            if (_state?.PlacedPieces == null || _selectedIndex < 0 || _selectedIndex >= _state.PlacedPieces.Count)
            {
                return null;
            }

            return _state.PlacedPieces[_selectedIndex];
        }

        private int ResolveSelectedOptionIndex(int previousSelectedItemId)
        {
            if (_group?.Options == null || _group.Options.Count == 0)
            {
                return -1;
            }

            if (_group.GroupKey > 0
                && _state?.SelectedItemsByGroup != null
                && _state.SelectedItemsByGroup.TryGetValue(_group.GroupKey, out int selectedItemId))
            {
                int restoredIndex = _group.Options
                    .Select((option, index) => new { option.ItemId, index })
                    .FirstOrDefault(entry => entry.ItemId == selectedItemId)?.index ?? -1;
                if (restoredIndex >= 0)
                {
                    return restoredIndex;
                }
            }

            if (previousSelectedItemId > 0)
            {
                int preservedIndex = _group.Options
                    .Select((option, index) => new { option.ItemId, index })
                    .FirstOrDefault(entry => entry.ItemId == previousSelectedItemId)?.index ?? -1;
                if (preservedIndex >= 0)
                {
                    return preservedIndex;
                }
            }

            return _selectedIndex >= 0 && _selectedIndex < _group.Options.Count
                ? _selectedIndex
                : 0;
        }

        private Texture2D ResolveItemIcon(int itemId)
        {
            if (itemId <= 0 || _itemIconProvider == null)
            {
                return null;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cached))
            {
                return cached;
            }

            Texture2D icon = _itemIconProvider(itemId);
            _itemIconCache[itemId] = icon;
            return icon;
        }

        private void DrawButtonLabel(SpriteBatch sprite, UIObject button, string label)
        {
            if (!CanDrawWindowText || button == null || !button.ButtonVisible || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            Vector2 size = MeasureWindowText(null, label, 0.34f);
            float x = Position.X + button.X + Math.Max(0f, (button.CanvasSnapshotWidth - size.X) / 2f);
            float y = Position.Y + button.Y + Math.Max(0f, (button.CanvasSnapshotHeight - size.Y) / 2f) - 1f;
            DrawWindowText(sprite, label, new Vector2(x, y), new Color(70, 49, 31), 0.34f);
        }

        private void DrawStretched(SpriteBatch sprite, Texture2D texture, Rectangle destination, Color color)
        {
            if (texture != null)
            {
                sprite.Draw(texture, destination, color);
                return;
            }

            sprite.Draw(_pixel, destination, color);
        }

        private void DrawTiled(SpriteBatch sprite, Texture2D texture, Rectangle destination, Color color)
        {
            if (destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            if (texture == null)
            {
                sprite.Draw(_pixel, destination, new Color((byte)247, (byte)240, (byte)229, color.A));
                return;
            }

            for (int y = destination.Y; y < destination.Bottom; y += texture.Height)
            {
                int tileHeight = Math.Min(texture.Height, destination.Bottom - y);
                for (int x = destination.X; x < destination.Right; x += texture.Width)
                {
                    int tileWidth = Math.Min(texture.Width, destination.Right - x);
                    sprite.Draw(
                        texture,
                        new Rectangle(x, y, tileWidth, tileHeight),
                        new Rectangle(0, 0, tileWidth, tileHeight),
                        color);
                }
            }
        }

        private void DrawText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            DrawWindowText(sprite, text, position, color, scale);
        }

        private IEnumerable<string> WrapText(string text, float width, float scale)
        {
            if (!CanDrawWindowText || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            string[] words = text.Replace("\r", string.Empty).Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> lines = new();
            string current = string.Empty;

            foreach (string word in words)
            {
                string candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (MeasureWindowText(null, candidate, scale).X <= width)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                }

                current = word;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            return lines;
        }

        private QuestRewardRaiseWindowMode ResolveWindowMode()
        {
            return _state?.DisplayMode
                ?? _state?.WindowMode
                ?? _prompt?.OwnerContext?.WindowMode
                ?? QuestRewardRaiseWindowMode.Selection;
        }

        private string BuildPiecePlacementSubtitle()
        {
            int placedCount = _state?.PlacedPieces?.Count ?? 0;
            int maxDropCount = Math.Max(1, _state?.MaxDropCount ?? _prompt?.OwnerContext?.MaxDropCount ?? 1);
            int ownerItemId = Math.Max(0, _state?.OwnerItemId ?? _prompt?.OwnerContext?.OwnerItemId ?? 0);
            int grade = Math.Max(0, _state?.Grade ?? _prompt?.OwnerContext?.Grade ?? 0);
            int expUnit = Math.Max(0, _state?.IncrementExpUnit ?? _prompt?.OwnerContext?.IncrementExpUnit ?? 0);
            string clientMetadata = grade > 0 || expUnit > 0
                ? $"  grade {grade} exp {expUnit}"
                : string.Empty;
            string clientKind = (_state?.ClientWindowKind ?? QuestRewardRaiseClientWindowKind.Selection) switch
            {
                QuestRewardRaiseClientWindowKind.RaiseWnd => "CUIRaiseWnd",
                QuestRewardRaiseClientWindowKind.RaisePieceWnd => "CUIRaisePieceWnd",
                _ => "Piece window"
            };
            return $"{clientKind}  {placedCount}/{maxDropCount}  owner #{ownerItemId}{clientMetadata}";
        }

        private string BuildPiecePlacementPromptText()
        {
            if (!string.IsNullOrWhiteSpace(_group?.PromptText))
            {
                return _group.PromptText;
            }

            return "Drag qualifying items from the inventory into the raise surface. Right-click a placed row to release that local PutItem request.";
        }

        private void DrawPlacedPieces(SpriteBatch sprite, Rectangle listBounds)
        {
            IReadOnlyList<QuestRewardRaisePlacedPiece> pieces = _state?.PlacedPieces != null
                ? _state.PlacedPieces
                : Array.Empty<QuestRewardRaisePlacedPiece>();
            int maxDropCount = Math.Max(1, _state?.MaxDropCount ?? _prompt?.OwnerContext?.MaxDropCount ?? 1);
            int visibleRows = Math.Max(1, listBounds.Height / RowHeight);
            int totalRows = Math.Min(visibleRows, Math.Max(maxDropCount, pieces.Count == 0 ? 1 : pieces.Count));
            for (int i = 0; i < totalRows; i++)
            {
                Rectangle rowBounds = new(listBounds.X, listBounds.Y + (i * RowHeight), listBounds.Width, RowHeight - 2);
                bool selected = i == _selectedIndex && i < pieces.Count;
                bool occupied = i < pieces.Count;
                sprite.Draw(_pixel, rowBounds, selected ? new Color(216, 196, 165, 205) : new Color(255, 252, 246, 172));
                sprite.Draw(_pixel, new Rectangle(rowBounds.X, rowBounds.Bottom, rowBounds.Width, 1), new Color(120, 103, 84, 70));

                if (!occupied)
                {
                    DrawText(sprite, $"Drop piece {i + 1}", new Vector2(rowBounds.X + 8, rowBounds.Y + 4), new Color(120, 96, 74), 0.34f);
                    DrawText(sprite, "Awaiting PutItem request", new Vector2(rowBounds.X + 8, rowBounds.Y + 13), new Color(146, 122, 98), 0.28f);
                    continue;
                }

                QuestRewardRaisePlacedPiece piece = pieces[i];
                Texture2D icon = ResolveItemIcon(piece.ItemId);
                if (icon != null)
                {
                    float iconScale = Math.Min(1f, Math.Min(18f / icon.Width, 18f / icon.Height));
                    sprite.Draw(icon, new Vector2(rowBounds.X + 4, rowBounds.Y + 3), null, Color.White, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
                }

                DrawText(sprite, Truncate(piece.Label, 26), new Vector2(rowBounds.X + 26, rowBounds.Y + 2), new Color(66, 44, 26), 0.34f);
                string detailText = BuildPieceRowDetail(piece);
                DrawText(sprite, Truncate(detailText, 40), new Vector2(rowBounds.X + 26, rowBounds.Y + 11), new Color(120, 96, 74), 0.28f);
            }
        }

        private string BuildPiecePreviewDetail(QuestRewardRaisePlacedPiece piece)
        {
            return piece == null
                ? $"QR {_state?.QrData ?? _prompt?.OwnerContext?.InitialQrData ?? 0}"
                : piece.PacketOpcode > 0
                    ? $"Op {piece.PacketOpcode}  {ResolveLifecycleLabel(piece.LifecycleState)}  req #{piece.RequestId}"
                    : $"{piece.InventoryType} slot {piece.SlotIndex + 1}  {ResolveLifecycleLabel(piece.LifecycleState)}";
        }

        private string BuildPiecePlacementFooter()
        {
            int placedCount = _state?.PlacedPieces?.Count ?? 0;
            int maxDropCount = Math.Max(1, _state?.MaxDropCount ?? _prompt?.OwnerContext?.MaxDropCount ?? 1);
            int qrData = _state?.QrData ?? _prompt?.OwnerContext?.InitialQrData ?? 0;
            int ownerItemId = Math.Max(0, _state?.OwnerItemId ?? _prompt?.OwnerContext?.OwnerItemId ?? 0);
            if (!string.IsNullOrWhiteSpace(_state?.LastInboundSummary))
            {
                return Truncate(_state.LastInboundSummary, 118);
            }

            if (!string.IsNullOrWhiteSpace(_state?.OpenDispatchSummary))
            {
                return Truncate(_state.OpenDispatchSummary, 118);
            }

            return placedCount == 0
                ? $"Drop inventory items into the raise surface. QR {qrData}  owner #{ownerItemId}."
                : $"Ready to confirm {placedCount}/{maxDropCount} placed piece{(placedCount == 1 ? string.Empty : "s")}. QR {qrData}.";
        }

        private static string BuildPieceRowDetail(QuestRewardRaisePlacedPiece piece)
        {
            if (piece == null)
            {
                return string.Empty;
            }

            string lifecycle = ResolveLifecycleLabel(piece.LifecycleState);
            return piece.PacketOpcode > 0
                ? $"Req #{piece.RequestId}  op {piece.PacketOpcode}  {lifecycle}"
                : $"Req #{piece.RequestId}  {piece.InventoryType} {piece.SlotIndex + 1}  {lifecycle}";
        }

        private static string ResolveLifecycleLabel(QuestRewardRaisePieceLifecycleState lifecycleState)
        {
            return lifecycleState switch
            {
                QuestRewardRaisePieceLifecycleState.PendingAddAck => "pending add",
                QuestRewardRaisePieceLifecycleState.Active => "active",
                QuestRewardRaisePieceLifecycleState.PendingReleaseAck => "pending release",
                QuestRewardRaisePieceLifecycleState.PendingConfirmAck => "pending confirm",
                QuestRewardRaisePieceLifecycleState.Confirmed => "confirmed",
                _ => "unknown"
            };
        }

        private string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return $"{text[..Math.Max(0, maxLength - 1)]}…";
        }
    }

    internal readonly record struct QuestRewardRaisePieceDropRequest(
        InventoryType SourceInventoryType,
        int SourceSlotIndex,
        InventorySlotData SlotData);
}
