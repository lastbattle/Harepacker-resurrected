using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
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

namespace HaCreator.MapSimulator.UI
{
    internal sealed class RepairDurabilityWindow : UIWindowBase
    {
        internal sealed class RepairEntry
        {
            public CharacterPart Part { get; init; }
            public InventorySlotData InventorySlot { get; init; }
            public EquipSlot Slot { get; init; }
            public bool IsHiddenSlot { get; init; }
            public bool IsInventorySlot { get; init; }
            public int EncodedSlotPosition { get; init; }
            public string SlotLabel { get; init; } = string.Empty;
            public string ItemName { get; init; } = string.Empty;
            public int CurrentDurability { get; init; }
            public int MaxDurability { get; init; }
            public int RepairCost { get; init; }
            public bool IsCashItem { get; init; }
            public string AvailabilityText { get; init; } = string.Empty;
            public IDXObject Icon { get; init; }
        }

        private readonly struct WindowLayer
        {
            public WindowLayer(IDXObject layer, Point offset)
            {
                Layer = layer;
                Offset = offset;
            }

            public IDXObject Layer { get; }
            public Point Offset { get; }
        }

        private const int RowLeft = 11;
        private const int RowTop = 109;
        private const int RowWidth = 199;
        private const int RowHeight = 35;
        private const int RowPitch = 42;
        private const int VisibleRowCount = 5;
        private const int ItemNameX = 54;
        private const int ItemNameY = 3;
        private const int ItemPercentX = 54;
        private const int ItemPercentY = 18;
        private const int ItemIconX = 14;
        private const int ItemIconY = 2;
        private const int RepairFeeLabelX = 52;
        private const int RepairFeeLabelY = 83;
        private const int RepairFeeValueRight = 200;
        private const int RepairFeeValueY = 83;
        private const int NpcNameX = 72;
        private const int NpcNameY = 53;
        private const int NpcPreviewX = 41;
        private const int NpcPreviewY = 104;
        private const int StatusTextX = 14;
        private const int StatusTextY = 303;
        private const int ScrollBarX = 212;
        private const int ScrollBarY = 109;
        private const int ScrollBarHeight = 203;
        private const int ScrollBarThumbWidth = 8;
        private const int ScrollBarThumbMinHeight = 18;
        private const float TextScale = 0.68f;
        private const float SecondaryTextScale = 0.58f;

        private readonly string _feeLabelText;
        private readonly List<WindowLayer> _layers = new();
        private readonly List<RepairEntry> _entries = new();
        private readonly Texture2D _normalRowTexture;
        private readonly Texture2D _selectedRowTexture;
        private readonly Texture2D _pixel;

        private SpriteFont _font;
        private NpcItem _npcPreview;
        private UIObject _repairAllButton;
        private UIObject _repairButton;
        private IInventoryRuntime _inventory;
        private MouseState _previousMouseState;
        private int _selectedIndex = -1;
        private int _scrollOffset;
        private int _lastNpcTemplateId;
        private string _npcName = string.Empty;
        private string _statusMessage = "Select an item to preview repair cost.";
        private bool _awaitingRepairResponse;

        internal event Action<RepairEntry> RepairRequested;
        internal event Action<IReadOnlyList<RepairEntry>> RepairAllRequested;

        public RepairDurabilityWindow(
            IDXObject frame,
            Texture2D normalRowTexture,
            Texture2D selectedRowTexture,
            string feeLabelText,
            GraphicsDevice device)
            : base(frame)
        {
            _normalRowTexture = normalRowTexture;
            _selectedRowTexture = selectedRowTexture;
            _feeLabelText = string.IsNullOrWhiteSpace(feeLabelText) ? "Repair Fee" : feeLabelText;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public override string WindowName => MapSimulatorWindowNames.RepairDurability;
        public override CharacterBuild CharacterBuild { get; set; }
        public int NpcTemplateId => _lastNpcTemplateId;
        public bool HasNpcPreview => _npcPreview != null;

        public override void Show()
        {
            base.Show();
            _previousMouseState = Mouse.GetState();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void AddLayer(IDXObject layer, Point offset)
        {
            if (layer != null)
            {
                _layers.Add(new WindowLayer(layer, offset));
            }
        }

        public void InitializeButtons(UIObject repairAllButton, UIObject repairButton, UIObject closeButton)
        {
            _repairAllButton = repairAllButton;
            _repairButton = repairButton;

            if (_repairAllButton != null)
            {
                AddButton(_repairAllButton);
                _repairAllButton.ButtonClickReleased += _ => RepairAllRequested?.Invoke(_entries.ToArray());
            }

            if (_repairButton != null)
            {
                AddButton(_repairButton);
                _repairButton.ButtonClickReleased += _ =>
                {
                    RepairEntry entry = GetSelectedEntry();
                    if (entry != null)
                    {
                        RepairRequested?.Invoke(entry);
                    }
                };
            }

            InitializeCloseButton(closeButton);
            UpdateButtonStates();
        }

        public void SetInventory(IInventoryRuntime inventory)
        {
            _inventory = inventory;
            UpdateButtonStates();
        }

        public void ConfigureNpc(int npcTemplateId, string npcName)
        {
            _lastNpcTemplateId = Math.Max(0, npcTemplateId);
            _npcName = npcName ?? string.Empty;
        }

        public void SetNpcPreview(NpcItem npcPreview)
        {
            _npcPreview = npcPreview;
            if (_npcPreview != null)
            {
                _npcPreview.MovementEnabled = false;
                _npcPreview.SetRenderPositionOverride(Position.X + NpcPreviewX, Position.Y + NpcPreviewY);
            }
        }

        public void SetStatusMessage(string statusMessage)
        {
            _statusMessage = statusMessage ?? string.Empty;
        }

        public void SetAwaitingRepairResponse(bool awaitingRepairResponse)
        {
            _awaitingRepairResponse = awaitingRepairResponse;
            UpdateButtonStates();
        }

        public void SetEntries(IReadOnlyList<RepairEntry> entries, int preferredItemId = 0)
        {
            RepairEntry previousSelection = GetSelectedEntry();
            CharacterPart previousPart = previousSelection?.Part;

            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries.Where(entry => entry?.Part != null));
            }

            if (_entries.Count == 0)
            {
                _selectedIndex = -1;
                _scrollOffset = 0;
            }
            else
            {
                _selectedIndex = ResolveSelectionIndex(previousPart, preferredItemId);
                _scrollOffset = ClampScrollOffset(_scrollOffset);
                EnsureSelectionVisible();
            }

            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _npcPreview?.SetRenderPositionOverride(Position.X + NpcPreviewX, Position.Y + NpcPreviewY);
            _npcPreview?.Update((int)Math.Round(gameTime.ElapsedGameTime.TotalMilliseconds));
            UpdateButtonStates();
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            Rectangle rowArea = GetRowAreaBounds();
            if (rowArea.Contains(mouseState.X, mouseState.Y))
            {
                if (wheelDelta != 0 && _entries.Count > VisibleRowCount)
                {
                    _scrollOffset = ClampScrollOffset(_scrollOffset - Math.Sign(wheelDelta));
                    UpdateButtonStates();
                    _previousMouseState = mouseState;
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }

                bool clicked = mouseState.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
                if (clicked)
                {
                    int row = (mouseState.Y - rowArea.Y) / RowPitch;
                    int index = _scrollOffset + row;
                    if (index >= 0 && index < _entries.Count)
                    {
                        _selectedIndex = index;
                        EnsureSelectionVisible();
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
            foreach (WindowLayer layer in _layers)
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

            if (_font == null)
            {
                return;
            }

            DrawNpcName(sprite);
            DrawNpcPreview(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
            DrawRepairFee(sprite);
            DrawRows(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawStatus(sprite);
            DrawScrollBar(sprite);
        }

        private void DrawNpcName(SpriteBatch sprite)
        {
            string npcLabel = string.IsNullOrWhiteSpace(_npcName)
                ? _lastNpcTemplateId > 0 ? $"NPC #{_lastNpcTemplateId}" : "Repair NPC unavailable"
                : _npcName;
            DrawOutlinedText(sprite, npcLabel, new Vector2(Position.X + NpcNameX, Position.Y + NpcNameY), new Color(89, 66, 32), SecondaryTextScale);
        }

        private void DrawNpcPreview(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            _npcPreview?.Draw(sprite, skeletonMeshRenderer, gameTime, 0, 0, 0, 0, drawReflectionInfo, renderParameters, tickCount);
        }

        private void DrawRepairFee(SpriteBatch sprite)
        {
            DrawOutlinedText(sprite, _feeLabelText, new Vector2(Position.X + RepairFeeLabelX, Position.Y + RepairFeeLabelY), new Color(255, 234, 188), SecondaryTextScale);

            RepairEntry selectedEntry = GetSelectedEntry();
            string feeText = selectedEntry == null
                ? "0"
                : selectedEntry.RepairCost.ToString("N0", CultureInfo.InvariantCulture);
            Vector2 size = _font.MeasureString(feeText) * TextScale;
            Vector2 position = new(Position.X + RepairFeeValueRight - size.X, Position.Y + RepairFeeValueY);
            DrawOutlinedText(sprite, feeText, position, Color.White, TextScale);
        }

        private void DrawRows(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, ReflectionDrawableBoundary drawReflectionInfo)
        {
            for (int row = 0; row < VisibleRowCount; row++)
            {
                int index = _scrollOffset + row;
                int rowY = Position.Y + RowTop + (row * RowPitch);
                Rectangle rowBounds = new(Position.X + RowLeft, rowY, RowWidth, RowHeight);
                bool selected = index == _selectedIndex;

                Texture2D rowTexture = selected ? _selectedRowTexture ?? _normalRowTexture : _normalRowTexture;
                if (rowTexture != null)
                {
                    sprite.Draw(rowTexture, new Vector2(rowBounds.X, rowBounds.Y), Color.White);
                }
                else
                {
                    sprite.Draw(_pixel, rowBounds, selected ? new Color(124, 205, 255, 200) : new Color(224, 224, 224, 180));
                }

                if (index < 0 || index >= _entries.Count)
                {
                    continue;
                }

                RepairEntry entry = _entries[index];
                if (entry.Icon != null)
                {
                    entry.Icon.DrawBackground(
                        sprite,
                        skeletonMeshRenderer,
                        gameTime,
                        rowBounds.X + ItemIconX,
                        rowBounds.Y + ItemIconY,
                        Color.White,
                        false,
                        drawReflectionInfo);
                }

                Color nameColor = selected ? new Color(64, 52, 33) : new Color(73, 56, 31);
                Color percentColor = entry.CurrentDurability > 0 ? new Color(64, 112, 64) : new Color(160, 62, 62);
                string title = $"{entry.ItemName}";
                if (!string.IsNullOrWhiteSpace(entry.SlotLabel))
                {
                    title = $"{title} ({entry.SlotLabel})";
                }

                string percent = $"{Math.Clamp((int)Math.Round((double)entry.CurrentDurability * 100d / Math.Max(1, entry.MaxDurability)), 0, 999)}%";
                DrawOutlinedText(sprite, Truncate(title, 22), new Vector2(rowBounds.X + ItemNameX, rowBounds.Y + ItemNameY), nameColor, SecondaryTextScale);
                DrawOutlinedText(sprite, $"{entry.CurrentDurability}/{entry.MaxDurability}  {percent}", new Vector2(rowBounds.X + ItemPercentX, rowBounds.Y + ItemPercentY), percentColor, SecondaryTextScale);
            }
        }

        private void DrawStatus(SpriteBatch sprite)
        {
            string status = string.IsNullOrWhiteSpace(_statusMessage)
                ? "Select an item to preview repair cost."
                : _statusMessage;
            DrawOutlinedText(sprite, Truncate(status, 48), new Vector2(Position.X + StatusTextX, Position.Y + StatusTextY), new Color(89, 66, 32), SecondaryTextScale);
        }

        private void DrawScrollBar(SpriteBatch sprite)
        {
            Rectangle track = new(Position.X + ScrollBarX, Position.Y + ScrollBarY, ScrollBarThumbWidth, ScrollBarHeight);
            sprite.Draw(_pixel, track, new Color(30, 30, 30, 60));

            int hiddenCount = Math.Max(0, _entries.Count - VisibleRowCount);
            if (hiddenCount <= 0)
            {
                sprite.Draw(_pixel, track, new Color(255, 255, 255, 45));
                return;
            }

            float visibleRatio = Math.Clamp((float)VisibleRowCount / _entries.Count, 0f, 1f);
            int thumbHeight = Math.Max(ScrollBarThumbMinHeight, (int)Math.Round(track.Height * visibleRatio));
            int thumbTravel = Math.Max(0, track.Height - thumbHeight);
            int thumbY = track.Y + (int)Math.Round(thumbTravel * (_scrollOffset / (float)hiddenCount));
            sprite.Draw(_pixel, new Rectangle(track.X, thumbY, track.Width, thumbHeight), new Color(164, 206, 255, 220));
        }

        private void UpdateButtonStates()
        {
            RepairEntry selectedEntry = GetSelectedEntry();
            long mesoCount = _inventory?.GetMesoCount() ?? 0;
            bool canRepairSelected = !_awaitingRepairResponse
                && selectedEntry != null
                && selectedEntry.RepairCost > 0
                && mesoCount >= selectedEntry.RepairCost;
            int repairAllCost = _entries.Sum(entry => Math.Max(0, entry.RepairCost));
            bool canRepairAll = !_awaitingRepairResponse
                && _entries.Count > 0
                && repairAllCost > 0
                && mesoCount >= repairAllCost;

            _repairButton?.SetEnabled(canRepairSelected);
            _repairAllButton?.SetEnabled(canRepairAll);
        }

        private RepairEntry GetSelectedEntry()
        {
            return _selectedIndex >= 0 && _selectedIndex < _entries.Count
                ? _entries[_selectedIndex]
                : null;
        }

        private Rectangle GetRowAreaBounds()
        {
            return new Rectangle(Position.X + RowLeft, Position.Y + RowTop, RowWidth, (VisibleRowCount * RowPitch) - (RowPitch - RowHeight));
        }

        private int ResolveSelectionIndex(CharacterPart previousPart, int preferredItemId)
        {
            if (preferredItemId > 0)
            {
                int preferredIndex = _entries.FindIndex(entry => entry.Part?.ItemId == preferredItemId);
                if (preferredIndex >= 0)
                {
                    return preferredIndex;
                }
            }

            if (previousPart != null)
            {
                int previousIndex = _entries.FindIndex(entry => ReferenceEquals(entry.Part, previousPart));
                if (previousIndex >= 0)
                {
                    return previousIndex;
                }
            }

            return _entries.Count > 0 ? 0 : -1;
        }

        private int ClampScrollOffset(int scrollOffset)
        {
            return Math.Clamp(scrollOffset, 0, Math.Max(0, _entries.Count - VisibleRowCount));
        }

        private void EnsureSelectionVisible()
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
            else if (_selectedIndex >= _scrollOffset + VisibleRowCount)
            {
                _scrollOffset = _selectedIndex - VisibleRowCount + 1;
            }

            _scrollOffset = ClampScrollOffset(_scrollOffset);
        }

        private void DrawOutlinedText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 shadowOffset = new(1f, 1f);
            sprite.DrawString(_font, text, position + shadowOffset, Color.Black * 0.55f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
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
