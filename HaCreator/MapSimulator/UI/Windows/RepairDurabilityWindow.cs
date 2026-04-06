using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Entities;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WeaponPart = HaCreator.MapSimulator.Character.WeaponPart;

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

        private readonly struct TooltipValueSegment
        {
            public TooltipValueSegment(Texture2D texture, string text = null)
            {
                Texture = texture;
                Text = text;
            }

            public Texture2D Texture { get; }
            public string Text { get; }
        }

        private readonly struct TooltipInfoRow
        {
            public TooltipInfoRow(
                Texture2D labelTexture,
                string labelText,
                string valueText,
                Color valueColor,
                IReadOnlyList<TooltipValueSegment> valueSegments = null)
            {
                LabelTexture = labelTexture;
                LabelText = labelText ?? string.Empty;
                ValueText = valueText ?? string.Empty;
                ValueColor = valueColor;
                ValueSegments = valueSegments;
            }

            public Texture2D LabelTexture { get; }
            public string LabelText { get; }
            public string ValueText { get; }
            public Color ValueColor { get; }
            public IReadOnlyList<TooltipValueSegment> ValueSegments { get; }
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
        private const int NpcPreviewX = 39;
        private const int NpcPreviewY = 92;
        private const int StatusTextX = 14;
        private const int StatusTextY = 303;
        private const int ScrollBarX = 212;
        private const int ScrollBarY = 109;
        private const int ScrollBarHeight = 203;
        private const int ScrollBarThumbWidth = 8;
        private const int ScrollBarThumbMinHeight = 18;
        private const int HoverIconHitWidth = 46;
        private const float TextScale = 0.68f;
        private const float SecondaryTextScale = 0.58f;
        private const int TooltipPadding = 10;
        private const int TooltipIconSize = 32;
        private const int TooltipIconGap = 8;
        private const int TooltipSectionGap = 6;
        private const int TooltipFallbackWidth = 214;

        private readonly string _feeLabelText;
        private readonly List<WindowLayer> _layers = new();
        private readonly List<RepairEntry> _entries = new();
        private readonly Texture2D _normalRowTexture;
        private readonly Texture2D _selectedRowTexture;
        private readonly Texture2D _pixel;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Point[] _tooltipFrameOrigins = new Point[3];

        private SpriteFont _font;
        private NpcItem _npcPreview;
        private UIObject _repairAllButton;
        private UIObject _repairButton;
        private IInventoryRuntime _inventory;
        private MouseState _previousMouseState;
        private Point _lastMousePosition;
        private int _selectedIndex = -1;
        private int _scrollOffset;
        private int _hoveredTooltipIndex = -1;
        private int _lastNpcTemplateId;
        private string _npcName = string.Empty;
        private string _statusMessage = "Select an item to preview repair cost.";
        private bool _awaitingRepairResponse;
        private Texture2D _cashLabelTexture;
        private EquipUIBigBang.EquipTooltipAssets _equipTooltipAssets;

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
            base.SetFont(font);
        }

        public void SetTooltipTextures(Texture2D[] tooltipFrames)
        {
            if (tooltipFrames == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_tooltipFrames.Length, tooltipFrames.Length); i++)
            {
                _tooltipFrames[i] = tooltipFrames[i];
            }
        }

        public void SetTooltipOrigins(Point[] tooltipOrigins)
        {
            if (tooltipOrigins == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_tooltipFrameOrigins.Length, tooltipOrigins.Length); i++)
            {
                _tooltipFrameOrigins[i] = tooltipOrigins[i];
            }
        }

        public void SetCashTooltipTexture(Texture2D cashLabelTexture)
        {
            _cashLabelTexture = cashLabelTexture;
        }

        public void SetEquipTooltipAssets(EquipUIBigBang.EquipTooltipAssets assets)
        {
            _equipTooltipAssets = assets;
            _cashLabelTexture ??= assets?.CashLabel;
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
            InventorySlotData previousInventorySlot = previousSelection?.InventorySlot;
            int previousEncodedSlotPosition = previousSelection?.EncodedSlotPosition ?? int.MinValue;
            bool previousIsInventorySlot = previousSelection?.IsInventorySlot ?? false;

            _entries.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries.Where(entry => entry != null && (entry.Part != null || entry.InventorySlot != null)));
            }

            if (_entries.Count == 0)
            {
                _selectedIndex = -1;
                _scrollOffset = 0;
                _hoveredTooltipIndex = -1;
            }
            else
            {
                _selectedIndex = ResolveSelectionIndex(previousPart, previousInventorySlot, previousEncodedSlotPosition, previousIsInventorySlot, preferredItemId);
                _scrollOffset = ClampScrollOffset(_scrollOffset);
                EnsureSelectionVisible();
                _hoveredTooltipIndex = ResolveHoveredTooltipIndex(_lastMousePosition);
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

            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            Rectangle rowArea = GetRowAreaBounds();
            _hoveredTooltipIndex = ResolveHoveredTooltipIndex(_lastMousePosition);
            if (rowArea.Contains(mouseState.X, mouseState.Y))
            {
                if (wheelDelta != 0 && _entries.Count > VisibleRowCount)
                {
                    _scrollOffset = ClampScrollOffset(_scrollOffset - Math.Sign(wheelDelta));
                    _hoveredTooltipIndex = ResolveHoveredTooltipIndex(_lastMousePosition);
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
            base.DrawOverlay(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);
            DrawHoveredItemTooltip(sprite);
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
            Vector2 size = ClientTextDrawing.Measure((GraphicsDevice)null, feeText, TextScale, _font);
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

        private void DrawHoveredItemTooltip(SpriteBatch sprite)
        {
            if (_font == null)
            {
                return;
            }

            RepairEntry entry = GetHoveredTooltipEntry();
            if (entry == null)
            {
                return;
            }

            int itemId = entry.Part?.ItemId ?? entry.InventorySlot?.ItemId ?? 0;
            if (itemId <= 0)
            {
                return;
            }

            CharacterPart tooltipPart = entry.Part ?? entry.InventorySlot?.TooltipPart;
            if (tooltipPart != null)
            {
                DrawHoveredEquipTooltip(sprite, entry, tooltipPart, itemId);
                return;
            }

            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(itemId, InventoryType.EQUIP);
            string title = ResolveDisplayText(entry.ItemName, metadata.ItemName, $"Equip {itemId}");
            string typeLine = ResolveDisplayText(metadata.TypeName, "Equip");
            string description = ResolveDisplayText(entry.Part?.Description, metadata.Description);

            List<string> lines = new();
            if (!string.IsNullOrWhiteSpace(typeLine))
            {
                lines.Add(typeLine);
            }

            lines.Add($"Durability: {entry.CurrentDurability}/{Math.Max(1, entry.MaxDurability)} ({ResolveDurabilityPercent(entry)}%)");

            if (!string.IsNullOrWhiteSpace(entry.SlotLabel))
            {
                lines.Add($"Slot: {entry.SlotLabel}");
            }

            lines.Add($"Repair Fee: {entry.RepairCost.ToString("N0", CultureInfo.InvariantCulture)} mesos");

            if (!string.IsNullOrWhiteSpace(entry.AvailabilityText))
            {
                lines.Add(entry.AvailabilityText);
            }

            foreach (string metadataLine in metadata.MetadataLines)
            {
                if (!string.IsNullOrWhiteSpace(metadataLine))
                {
                    lines.Add(metadataLine);
                }
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                lines.Add(description);
            }

            int tooltipWidth = ResolveTooltipWidth();
            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            int textWidth = tooltipWidth - (TooltipPadding * 2) - TooltipIconSize - TooltipIconGap;

            string[] wrappedTitle = WrapTooltipText(title, tooltipWidth - (TooltipPadding * 2));
            List<string[]> wrappedSections = WrapTooltipSections(lines, textWidth);

            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float cashLabelHeight = metadata.IsCashItem ? _cashLabelTexture?.Height ?? 0f : 0f;
            float textBlockHeight = MeasureWrappedSectionHeight(wrappedSections);
            if (cashLabelHeight > 0f)
            {
                textBlockHeight += cashLabelHeight + (textBlockHeight > 0f ? 2f : 0f);
            }

            float topBlockHeight = Math.Max(TooltipIconSize, textBlockHeight);
            int tooltipHeight = (int)Math.Ceiling((TooltipPadding * 2) + titleHeight + TooltipSectionGap + topBlockHeight);

            Point tooltipAnchor = new(_lastMousePosition.X, _lastMousePosition.Y + 20);
            Rectangle backgroundRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                viewportWidth,
                viewportHeight,
                stackalloc int[] { 1, 0, 2 },
                out int tooltipFrameIndex);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int tooltipX = backgroundRect.X;
            int tooltipY = backgroundRect.Y;
            int titleX = tooltipX + TooltipPadding;
            int titleY = tooltipY + TooltipPadding;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = tooltipY + TooltipPadding + (int)Math.Ceiling(titleHeight) + TooltipSectionGap;
            if (entry.Icon != null)
            {
                entry.Icon.DrawBackground(sprite, null, null, tooltipX + TooltipPadding, contentY, Color.White, false, null);
            }

            int textX = tooltipX + TooltipPadding + TooltipIconSize + TooltipIconGap;
            float sectionY = contentY;
            if (cashLabelHeight > 0f && _cashLabelTexture != null)
            {
                sprite.Draw(_cashLabelTexture, new Vector2(textX, sectionY), Color.White);
                sectionY += cashLabelHeight;
                if (wrappedSections.Count > 0)
                {
                    sectionY += 2f;
                }
            }

            DrawWrappedSections(sprite, textX, sectionY, wrappedSections);
        }

        private void DrawHoveredEquipTooltip(SpriteBatch sprite, RepairEntry entry, CharacterPart part, int itemId)
        {
            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(itemId, InventoryType.EQUIP);
            string title = ResolveDisplayText(entry.ItemName, ResolveDisplayText(part.Name, metadata.ItemName), $"Equip {itemId}");
            string description = ResolveDisplayText(part.Description, metadata.Description);
            Texture2D categoryTexture = ResolveCategoryTexture(part);
            string categoryText = categoryTexture == null
                ? ResolveDisplayText(ResolveCategoryFallbackText(part), metadata.TypeName)
                : string.Empty;
            string[] wrappedTitle = WrapTooltipText(title, ResolveTooltipWidth() - (TooltipPadding * 2));

            string[] wrappedCategory = Array.Empty<string>();
            if (categoryTexture == null && !string.IsNullOrWhiteSpace(categoryText))
            {
                wrappedCategory = WrapTooltipText(categoryText, ResolveTooltipWidth() - (TooltipPadding * 2) - TooltipIconSize - TooltipIconGap);
            }

            string[] wrappedDescription = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(description))
            {
                wrappedDescription = WrapTooltipText(description, ResolveTooltipWidth() - (TooltipPadding * 2) - TooltipIconSize - TooltipIconGap);
            }

            float titleHeight = MeasureLinesHeight(wrappedTitle);
            float categoryHeight = categoryTexture?.Height ?? MeasureLinesHeight(wrappedCategory);
            float cashLabelHeight = part.IsCash ? _cashLabelTexture?.Height ?? 0f : 0f;
            float descriptionHeight = MeasureLinesHeight(wrappedDescription);
            float topTextHeight = categoryHeight;
            if (cashLabelHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? 2f : 0f) + cashLabelHeight;
            }

            if (descriptionHeight > 0f)
            {
                topTextHeight += (topTextHeight > 0f ? TooltipSectionGap : 0f) + descriptionHeight;
            }

            float topBlockHeight = Math.Max(TooltipIconSize, topTextHeight);

            List<TooltipInfoRow> statRows = BuildEquipTooltipStatRows(part);
            List<TooltipInfoRow> requirementRows = BuildEquipTooltipRequirementRows(part);
            float statRowsHeight = MeasureTooltipInfoRowsHeight(statRows);
            float requirementRowsHeight = MeasureTooltipInfoRowsHeight(requirementRows);
            List<Texture2D> jobBadges = BuildTooltipJobBadges(part.RequiredJobMask);
            float jobBadgeHeight = jobBadges.Count > 0 ? 13f : 0f;

            List<string> footerLines = BuildEquipTooltipFooterLines(entry, part, metadata);
            List<string[]> wrappedFooterSections = WrapTooltipSections(
                footerLines,
                ResolveTooltipWidth() - (TooltipPadding * 2));
            float footerHeight = MeasureWrappedSectionHeight(wrappedFooterSections);

            float contentHeight = topBlockHeight;
            if (statRowsHeight > 0f)
            {
                contentHeight += TooltipSectionGap + statRowsHeight;
            }

            if (requirementRowsHeight > 0f)
            {
                contentHeight += TooltipSectionGap + requirementRowsHeight;
            }

            if (jobBadgeHeight > 0f)
            {
                contentHeight += TooltipSectionGap + jobBadgeHeight;
            }

            if (footerHeight > 0f)
            {
                contentHeight += TooltipSectionGap + footerHeight;
            }

            int tooltipWidth = ResolveTooltipWidth();
            int tooltipHeight = (int)Math.Ceiling((TooltipPadding * 2) + titleHeight + TooltipSectionGap + contentHeight);
            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            Point tooltipAnchor = new(_lastMousePosition.X, _lastMousePosition.Y + 20);
            Rectangle backgroundRect = ResolveTooltipRect(
                tooltipAnchor,
                tooltipWidth,
                tooltipHeight,
                viewportWidth,
                viewportHeight,
                stackalloc int[] { 1, 0, 2 },
                out int tooltipFrameIndex);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            int tooltipX = backgroundRect.X;
            int tooltipY = backgroundRect.Y;
            int titleX = tooltipX + TooltipPadding;
            int titleY = tooltipY + TooltipPadding;
            DrawTooltipLines(sprite, wrappedTitle, titleX, titleY, new Color(255, 220, 120));

            int contentY = tooltipY + TooltipPadding + (int)Math.Ceiling(titleHeight) + TooltipSectionGap;
            if (entry.Icon != null)
            {
                entry.Icon.DrawBackground(sprite, null, null, tooltipX + TooltipPadding, contentY, Color.White, false, null);
            }

            int textX = tooltipX + TooltipPadding + TooltipIconSize + TooltipIconGap;
            float topY = contentY;
            if (categoryTexture != null)
            {
                sprite.Draw(categoryTexture, new Vector2(textX, topY), Color.White);
                topY += categoryTexture.Height;
            }
            else if (wrappedCategory.Length > 0)
            {
                DrawTooltipLines(sprite, wrappedCategory, textX, topY, new Color(181, 224, 255));
                topY += categoryHeight;
            }

            if (cashLabelHeight > 0f && _cashLabelTexture != null)
            {
                if (topY > contentY)
                {
                    topY += 2f;
                }

                sprite.Draw(_cashLabelTexture, new Vector2(textX, topY), Color.White);
                topY += cashLabelHeight;
            }

            if (wrappedDescription.Length > 0)
            {
                if (topY > contentY)
                {
                    topY += TooltipSectionGap;
                }

                DrawTooltipLines(sprite, wrappedDescription, textX, topY, new Color(216, 216, 216));
            }

            float sectionY = contentY + topBlockHeight;
            if (statRowsHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                sectionY = DrawTooltipInfoRows(sprite, tooltipX + TooltipPadding, sectionY, tooltipWidth - (TooltipPadding * 2), statRows);
            }

            if (requirementRowsHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                sectionY = DrawTooltipInfoRows(sprite, tooltipX + TooltipPadding, sectionY, tooltipWidth - (TooltipPadding * 2), requirementRows);
            }

            if (jobBadgeHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                sectionY = DrawJobBadgeRow(sprite, tooltipX + TooltipPadding, sectionY, jobBadges);
            }

            if (footerHeight > 0f)
            {
                sectionY += TooltipSectionGap;
                DrawWrappedSections(sprite, tooltipX + TooltipPadding, sectionY, wrappedFooterSections);
            }
        }

        private List<TooltipInfoRow> BuildEquipTooltipStatRows(CharacterPart part)
        {
            List<TooltipInfoRow> rows = new();
            AppendTooltipInfoRow(rows, "STR:", null, part.BonusSTR, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "DEX:", null, part.BonusDEX, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "INT:", null, part.BonusINT, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "LUK:", null, part.BonusLUK, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "HP:", null, part.BonusHP, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "MP:", null, part.BonusMP, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Weapon ATT:", ResolvePropertyLabel("6"), part.BonusWeaponAttack, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Magic ATT:", ResolvePropertyLabel("7"), part.BonusMagicAttack, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Weapon DEF:", ResolvePropertyLabel("8"), part.BonusWeaponDefense, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Magic DEF:", ResolvePropertyLabel("9"), part.BonusMagicDefense, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Accuracy:", ResolvePropertyLabel("10"), part.BonusAccuracy, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Avoidability:", ResolvePropertyLabel("11"), part.BonusAvoidability, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Hands:", ResolvePropertyLabel("12"), part.BonusHands, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Speed:", ResolvePropertyLabel("13"), part.BonusSpeed, new Color(176, 255, 176), prefixPlus: true);
            AppendTooltipInfoRow(rows, "Jump:", ResolvePropertyLabel("14"), part.BonusJump, new Color(176, 255, 176), prefixPlus: true);

            if (part.EnhancementStarCount > 0)
            {
                string valueText = part.EnhancementStarCount.ToString(CultureInfo.InvariantCulture);
                rows.Add(new TooltipInfoRow(
                    _equipTooltipAssets?.StarLabel,
                    "Stars:",
                    valueText,
                    new Color(255, 232, 176),
                    BuildTooltipValueSegments(valueText, enabled: true, preferGrowthDigits: false)));
            }

            int upgradeSlots = ResolveTooltipUpgradeSlotCount(part);
            if (upgradeSlots > 0)
            {
                string upgradeValue = part.TotalUpgradeSlotCount.HasValue && part.TotalUpgradeSlotCount.Value > 0
                    ? $"{upgradeSlots}/{part.TotalUpgradeSlotCount.Value}"
                    : upgradeSlots.ToString(CultureInfo.InvariantCulture);
                rows.Add(new TooltipInfoRow(
                    ResolvePropertyLabel("16"),
                    "Upgrades Available:",
                    upgradeValue,
                    new Color(255, 232, 176),
                    BuildTooltipValueSegments(upgradeValue, enabled: true, preferGrowthDigits: false)));
            }

            if (part.SellPrice > 0)
            {
                string valueText = part.SellPrice.ToString("N0", CultureInfo.InvariantCulture);
                rows.Add(new TooltipInfoRow(
                    _equipTooltipAssets?.MesosLabel,
                    "Mesos:",
                    valueText,
                    new Color(255, 244, 186),
                    BuildTooltipValueSegments(valueText, enabled: true, preferGrowthDigits: false)));
            }

            if (part is WeaponPart weapon && weapon.AttackSpeed >= 0)
            {
                Texture2D speedTexture = ResolveSpeedTexture(weapon.AttackSpeed);
                rows.Add(new TooltipInfoRow(speedTexture ?? ResolvePropertyLabel("4"), "Attack Speed:", ResolveAttackSpeedText(weapon.AttackSpeed), new Color(181, 224, 255)));
            }

            AppendGrowthRows(rows, part);

            return rows;
        }

        private List<TooltipInfoRow> BuildEquipTooltipRequirementRows(CharacterPart part)
        {
            List<TooltipInfoRow> rows = new();
            AppendRequirementRow(rows, "reqLEV", "Required Level:", part.RequiredLevel, CharacterBuild?.Level ?? int.MaxValue);
            AppendRequirementRow(rows, "reqSTR", "Required STR:", part.RequiredSTR, CharacterBuild?.TotalSTR ?? int.MaxValue);
            AppendRequirementRow(rows, "reqDEX", "Required DEX:", part.RequiredDEX, CharacterBuild?.TotalDEX ?? int.MaxValue);
            AppendRequirementRow(rows, "reqINT", "Required INT:", part.RequiredINT, CharacterBuild?.TotalINT ?? int.MaxValue);
            AppendRequirementRow(rows, "reqLUK", "Required LUK:", part.RequiredLUK, CharacterBuild?.TotalLUK ?? int.MaxValue);
            AppendRequirementRow(rows, "reqPOP", "Required Fame:", part.RequiredFame, CharacterBuild?.Fame ?? int.MaxValue);

            if (part.Durability.HasValue)
            {
                bool canUse = !part.MaxDurability.HasValue || part.Durability.Value > 0;
                string value = part.MaxDurability.HasValue && part.MaxDurability.Value > 0
                    ? $"{Math.Max(0, part.Durability.Value)}/{part.MaxDurability.Value}"
                    : Math.Max(0, part.Durability.Value).ToString(CultureInfo.InvariantCulture);
                rows.Add(new TooltipInfoRow(
                    ResolveRequirementLabel(canUse, "durability"),
                    "Durability:",
                    value,
                    canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                    BuildTooltipValueSegments(value, enabled: canUse, preferGrowthDigits: false)));
            }

            return rows;
        }

        private List<string> BuildEquipTooltipFooterLines(RepairEntry entry, CharacterPart part, InventoryItemTooltipMetadata metadata)
        {
            List<string> lines = new();
            if (!string.IsNullOrWhiteSpace(entry.SlotLabel))
            {
                lines.Add($"Slot: {entry.SlotLabel}");
            }

            lines.Add($"Repair Fee: {entry.RepairCost.ToString("N0", CultureInfo.InvariantCulture)} mesos");

            if (!string.IsNullOrWhiteSpace(entry.AvailabilityText))
            {
                lines.Add(entry.AvailabilityText);
            }

            if (part.IsTradeBlocked || metadata.IsTradeBlocked)
            {
                lines.Add("Untradeable");
            }

            if (part.IsOneOfAKind || metadata.IsOneOfAKind)
            {
                lines.Add("One of a kind");
            }

            if (part.IsNotForSale || metadata.IsNotForSale)
            {
                lines.Add("Not for sale");
            }

            if (part.ExpirationDateUtc.HasValue)
            {
                lines.Add($"Expires {part.ExpirationDateUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}");
            }

            if (!string.IsNullOrWhiteSpace(part.PotentialTierText))
            {
                lines.Add(part.PotentialTierText);
            }

            if (part.PotentialLines != null)
            {
                for (int i = 0; i < part.PotentialLines.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(part.PotentialLines[i]))
                    {
                        lines.Add(part.PotentialLines[i]);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(part.Description))
            {
                lines.Add(part.Description);
            }

            return lines;
        }

        private void AppendTooltipInfoRow(List<TooltipInfoRow> rows, string fallbackLabel, Texture2D labelTexture, int value, Color valueColor, bool prefixPlus)
        {
            if (value <= 0)
            {
                return;
            }

            string valueText = prefixPlus ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipInfoRow(
                labelTexture,
                fallbackLabel,
                valueText,
                valueColor,
                BuildTooltipValueSegments(valueText, enabled: true, preferGrowthDigits: true)));
        }

        private void AppendRequirementRow(List<TooltipInfoRow> rows, string labelKey, string fallbackLabel, int requiredValue, int actualValue)
        {
            if (requiredValue <= 0)
            {
                return;
            }

            bool canUse = actualValue >= requiredValue;
            rows.Add(new TooltipInfoRow(
                ResolveRequirementLabel(canUse, labelKey),
                fallbackLabel,
                requiredValue.ToString(CultureInfo.InvariantCulture),
                canUse ? new Color(181, 224, 255) : new Color(255, 186, 186),
                BuildTooltipValueSegments(requiredValue.ToString(CultureInfo.InvariantCulture), enabled: canUse, preferGrowthDigits: false)));
        }

        private static int ResolveTooltipUpgradeSlotCount(CharacterPart part)
        {
            if (part?.RemainingUpgradeSlotCount.HasValue == true)
            {
                return Math.Max(0, part.RemainingUpgradeSlotCount.Value);
            }

            return Math.Max(0, part?.UpgradeSlots ?? 0);
        }

        private float MeasureTooltipInfoRowsHeight(IReadOnlyList<TooltipInfoRow> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                height += MeasureTooltipInfoRowHeight(rows[i]);
                if (i < rows.Count - 1)
                {
                    height += 2f;
                }
            }

            return height;
        }

        private float MeasureTooltipInfoRowHeight(TooltipInfoRow row)
        {
            float height = MeasureTooltipValueSegmentsHeight(row.ValueSegments);
            if (height <= 0f)
            {
                height = ClientTextDrawing.Measure((GraphicsDevice)null, row.ValueText ?? string.Empty, SecondaryTextScale, _font).Y;
            }

            if (row.LabelTexture != null)
            {
                height = Math.Max(height, row.LabelTexture.Height);
            }
            else if (!string.IsNullOrWhiteSpace(row.LabelText))
            {
                height = Math.Max(height, ClientTextDrawing.Measure((GraphicsDevice)null, row.LabelText, SecondaryTextScale, _font).Y);
            }

            return Math.Max(height, 12f);
        }

        private float DrawTooltipInfoRows(SpriteBatch sprite, int x, float y, int width, IReadOnlyList<TooltipInfoRow> rows)
        {
            if (rows == null)
            {
                return y;
            }

            float drawY = y;
            for (int i = 0; i < rows.Count; i++)
            {
                TooltipInfoRow row = rows[i];
                float rowHeight = MeasureTooltipInfoRowHeight(row);
                float labelWidth = 0f;
                if (row.LabelTexture != null)
                {
                    sprite.Draw(row.LabelTexture, new Vector2(x, drawY), Color.White);
                    labelWidth = row.LabelTexture.Width;
                }
                else if (!string.IsNullOrWhiteSpace(row.LabelText))
                {
                    DrawOutlinedText(sprite, row.LabelText, new Vector2(x, drawY), new Color(214, 222, 238), SecondaryTextScale);
                    labelWidth = ClientTextDrawing.Measure((GraphicsDevice)null, row.LabelText, SecondaryTextScale, _font).X;
                }

                float valueWidth = MeasureTooltipValueSegmentsWidth(row.ValueSegments);
                if (valueWidth <= 0f)
                {
                    valueWidth = ClientTextDrawing.Measure((GraphicsDevice)null, row.ValueText ?? string.Empty, SecondaryTextScale, _font).X;
                }

                float minValueX = x + labelWidth + 10f;
                float preferredValueX = x + width - valueWidth;
                float valueX = Math.Max(minValueX, preferredValueX);
                if (row.ValueSegments != null && row.ValueSegments.Count > 0)
                {
                    DrawTooltipValueSegments(sprite, row.ValueSegments, valueX, drawY, row.ValueColor);
                }
                else
                {
                    DrawOutlinedText(sprite, row.ValueText, new Vector2(valueX, drawY), row.ValueColor, SecondaryTextScale);
                }

                drawY += rowHeight + 2f;
            }

            return drawY;
        }

        private Texture2D ResolvePropertyLabel(string key)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.PropertyLabels, key);
        }

        private Texture2D ResolveRequirementLabel(bool canUse, string key)
        {
            return TryResolveTooltipAsset(canUse ? _equipTooltipAssets?.CanLabels : _equipTooltipAssets?.CannotLabels, key);
        }

        private Texture2D ResolveSpeedTexture(int attackSpeed)
        {
            return TryResolveTooltipAsset(_equipTooltipAssets?.SpeedLabels, Math.Clamp(attackSpeed, 0, 6).ToString(CultureInfo.InvariantCulture));
        }

        private Texture2D ResolveGrowthLabel(bool enabled, string key)
        {
            return TryResolveTooltipAsset(
                enabled ? _equipTooltipAssets?.GrowthEnabledLabels : _equipTooltipAssets?.GrowthDisabledLabels,
                key);
        }

        private Texture2D ResolveCategoryTexture(CharacterPart part)
        {
            if (_equipTooltipAssets == null || part == null || part.ItemId <= 0)
            {
                return null;
            }

            int itemCategory = part.ItemId / 10000;
            if (part is WeaponPart)
            {
                Texture2D weaponTexture = TryResolveTooltipAsset(
                    _equipTooltipAssets.WeaponCategoryLabels,
                    (itemCategory - 100).ToString(CultureInfo.InvariantCulture));
                if (weaponTexture != null)
                {
                    return weaponTexture;
                }
            }

            string categoryKey = itemCategory switch
            {
                100 => "1",
                101 => "2",
                102 => "3",
                103 => "4",
                104 => "5",
                105 => "21",
                106 => "6",
                107 => "7",
                108 => "8",
                109 => "10",
                110 => "9",
                111 => "12",
                _ => null
            };

            return categoryKey == null
                ? null
                : TryResolveTooltipAsset(_equipTooltipAssets.ItemCategoryLabels, categoryKey);
        }

        private static Texture2D TryResolveTooltipAsset(IReadOnlyDictionary<string, Texture2D> assets, string key)
        {
            if (assets == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return assets.TryGetValue(key, out Texture2D texture) ? texture : null;
        }

        private static string ResolveCategoryFallbackText(CharacterPart part)
        {
            if (part is WeaponPart weapon && !string.IsNullOrWhiteSpace(weapon.WeaponType))
            {
                return weapon.WeaponType;
            }

            return part?.ItemCategory ?? string.Empty;
        }

        private static string ResolveAttackSpeedText(int attackSpeed)
        {
            return Math.Clamp(attackSpeed, 0, 6) switch
            {
                0 => "Fastest",
                1 => "Faster",
                2 => "Fast",
                3 => "Normal",
                4 => "Slow",
                5 => "Slower",
                6 => "Slowest",
                _ => string.Empty
            };
        }

        private void AppendGrowthRows(List<TooltipInfoRow> rows, CharacterPart part)
        {
            if (rows == null || part == null || !part.HasGrowthInfo)
            {
                return;
            }

            int currentLevel = Math.Max(1, part.GrowthLevel);
            int maxLevel = Math.Max(currentLevel, part.GrowthMaxLevel);
            bool growthEnabled = currentLevel < maxLevel;

            string levelText = currentLevel.ToString(CultureInfo.InvariantCulture);
            rows.Add(new TooltipInfoRow(
                ResolveGrowthLabel(growthEnabled, "itemLEV"),
                "Item Level:",
                levelText,
                growthEnabled ? new Color(181, 224, 255) : new Color(192, 192, 192),
                BuildTooltipValueSegments(levelText, enabled: growthEnabled, preferGrowthDigits: true)));

            string expText = growthEnabled
                ? $"{Math.Clamp(part.GrowthExpPercent, 0, 99)}%"
                : "MAX";
            rows.Add(new TooltipInfoRow(
                ResolveGrowthLabel(growthEnabled, "itemEXP"),
                "Item EXP:",
                expText,
                growthEnabled ? new Color(181, 224, 255) : new Color(192, 192, 192),
                BuildTooltipValueSegments(expText, enabled: growthEnabled, preferGrowthDigits: true)));
        }

        private IReadOnlyList<TooltipValueSegment> BuildTooltipValueSegments(string valueText, bool enabled, bool preferGrowthDigits)
        {
            if (string.IsNullOrWhiteSpace(valueText) || _equipTooltipAssets == null)
            {
                return null;
            }

            IReadOnlyDictionary<string, Texture2D> source = preferGrowthDigits
                ? (enabled ? _equipTooltipAssets.GrowthEnabledLabels : _equipTooltipAssets.GrowthDisabledLabels)
                : (enabled ? _equipTooltipAssets.CanLabels : _equipTooltipAssets.CannotLabels);
            if (source == null)
            {
                return null;
            }

            if (string.Equals(valueText, "MAX", StringComparison.OrdinalIgnoreCase))
            {
                Texture2D maxTexture = TryResolveTooltipAsset(source, "max");
                return maxTexture == null ? null : new[] { new TooltipValueSegment(maxTexture) };
            }

            List<TooltipValueSegment> segments = new(valueText.Length);
            for (int i = 0; i < valueText.Length; i++)
            {
                char character = valueText[i];
                if (character == '+')
                {
                    continue;
                }

                string key = character switch
                {
                    '%' => "percent",
                    _ => char.IsDigit(character) ? character.ToString() : null
                };

                if (key == null)
                {
                    if (character == '/' || character == '-' || character == '.' || character == ',')
                    {
                        segments.Add(new TooltipValueSegment(null, character.ToString()));
                        continue;
                    }

                    return null;
                }

                Texture2D texture = TryResolveTooltipAsset(source, key);
                if (texture == null)
                {
                    return null;
                }

                segments.Add(new TooltipValueSegment(texture));
            }

            return segments.Count > 0 ? segments : null;
        }

        private float MeasureTooltipValueSegmentsHeight(IReadOnlyList<TooltipValueSegment> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0f;
            }

            int height = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                TooltipValueSegment segment = segments[i];
                if (segment.Texture != null)
                {
                    height = Math.Max(height, segment.Texture.Height);
                }
                else if (!string.IsNullOrEmpty(segment.Text))
                {
                    height = Math.Max(
                        height,
                        (int)Math.Ceiling(ClientTextDrawing.Measure((GraphicsDevice)null, segment.Text, SecondaryTextScale, _font).Y));
                }
            }

            return height;
        }

        private float MeasureTooltipValueSegmentsWidth(IReadOnlyList<TooltipValueSegment> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return 0f;
            }

            float width = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                TooltipValueSegment segment = segments[i];
                if (segment.Texture != null)
                {
                    width += segment.Texture.Width;
                }
                else if (!string.IsNullOrEmpty(segment.Text))
                {
                    width += ClientTextDrawing.Measure((GraphicsDevice)null, segment.Text, SecondaryTextScale, _font).X;
                }

                if (i < segments.Count - 1)
                {
                    width += 1f;
                }
            }

            return width;
        }

        private void DrawTooltipValueSegments(SpriteBatch sprite, IReadOnlyList<TooltipValueSegment> segments, float x, float y, Color color)
        {
            if (segments == null || segments.Count == 0)
            {
                return;
            }

            float drawX = x;
            for (int i = 0; i < segments.Count; i++)
            {
                TooltipValueSegment segment = segments[i];
                if (segment.Texture != null)
                {
                    sprite.Draw(segment.Texture, new Vector2(drawX, y), Color.White);
                    drawX += segment.Texture.Width;
                }
                else if (!string.IsNullOrEmpty(segment.Text))
                {
                    DrawOutlinedText(sprite, segment.Text, new Vector2(drawX, y), color, SecondaryTextScale);
                    drawX += ClientTextDrawing.Measure((GraphicsDevice)null, segment.Text, SecondaryTextScale, _font).X;
                }

                if (i < segments.Count - 1)
                {
                    drawX += 1f;
                }
            }
        }

        private List<Texture2D> BuildTooltipJobBadges(int requiredJobMask)
        {
            List<Texture2D> textures = new(6);
            AppendJobBadgeTexture(textures, requiredJobMask, 1, "beginner");
            AppendJobBadgeTexture(textures, requiredJobMask, 2, "warrior");
            AppendJobBadgeTexture(textures, requiredJobMask, 4, "magician");
            AppendJobBadgeTexture(textures, requiredJobMask, 8, "bowman");
            AppendJobBadgeTexture(textures, requiredJobMask, 16, "thief");
            AppendJobBadgeTexture(textures, requiredJobMask, 32, "pirate");
            return textures;
        }

        private void AppendJobBadgeTexture(List<Texture2D> textures, int requiredJobMask, int maskBit, string key)
        {
            if (textures == null || (requiredJobMask & maskBit) == 0)
            {
                return;
            }

            Texture2D texture = ResolveRequirementLabel(true, key);
            if (texture != null)
            {
                textures.Add(texture);
            }
        }

        private static float DrawJobBadgeRow(SpriteBatch sprite, int x, float y, IReadOnlyList<Texture2D> textures)
        {
            int drawX = x;
            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D texture = textures[i];
                if (texture == null)
                {
                    continue;
                }

                sprite.Draw(texture, new Vector2(drawX, y), Color.White);
                drawX += texture.Width + 4;
            }

            return y + 13f;
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

        private RepairEntry GetHoveredTooltipEntry()
        {
            return _hoveredTooltipIndex >= 0 && _hoveredTooltipIndex < _entries.Count
                ? _entries[_hoveredTooltipIndex]
                : null;
        }

        private Rectangle GetRowAreaBounds()
        {
            return new Rectangle(Position.X + RowLeft, Position.Y + RowTop, RowWidth, (VisibleRowCount * RowPitch) - (RowPitch - RowHeight));
        }

        private int ResolveHoveredTooltipIndex(Point mousePosition)
        {
            Rectangle rowArea = GetRowAreaBounds();
            if (!rowArea.Contains(mousePosition) || mousePosition.X >= rowArea.X + HoverIconHitWidth)
            {
                return -1;
            }

            int row = (mousePosition.Y - rowArea.Y) / RowPitch;
            if (row < 0 || row >= VisibleRowCount)
            {
                return -1;
            }

            int index = _scrollOffset + row;
            return index >= 0 && index < _entries.Count ? index : -1;
        }

        private int ResolveSelectionIndex(CharacterPart previousPart, InventorySlotData previousInventorySlot, int previousEncodedSlotPosition, bool previousIsInventorySlot, int preferredItemId)
        {
            if (preferredItemId > 0)
            {
                int preferredIndex = _entries.FindIndex(entry =>
                    (entry.Part?.ItemId ?? entry.InventorySlot?.ItemId ?? 0) == preferredItemId);
                if (preferredIndex >= 0)
                {
                    return preferredIndex;
                }
            }

            if (previousEncodedSlotPosition != int.MinValue)
            {
                int previousPositionIndex = _entries.FindIndex(entry =>
                    entry.EncodedSlotPosition == previousEncodedSlotPosition
                    && entry.IsInventorySlot == previousIsInventorySlot);
                if (previousPositionIndex >= 0)
                {
                    return previousPositionIndex;
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

            if (previousInventorySlot != null)
            {
                int previousInventoryIndex = _entries.FindIndex(entry => ReferenceEquals(entry.InventorySlot, previousInventorySlot));
                if (previousInventoryIndex >= 0)
                {
                    return previousInventoryIndex;
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

            ClientTextDrawing.DrawShadowed(sprite, text, position, color, _font, scale);
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }

            return $"{text[..Math.Max(0, maxLength - 3)]}...";
        }

        private int ResolveTooltipWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TooltipFallbackWidth;
        }

        private Rectangle CreateTooltipRectFromAnchor(Point anchorPoint, int tooltipWidth, int tooltipHeight, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;
            Point origin = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrameOrigins.Length
                ? _tooltipFrameOrigins[tooltipFrameIndex]
                : Point.Zero;

            if (tooltipFrame != null && origin != Point.Zero)
            {
                float scaleX = tooltipFrame.Width > 0 ? tooltipWidth / (float)tooltipFrame.Width : 1f;
                float scaleY = tooltipFrame.Height > 0 ? tooltipHeight / (float)tooltipFrame.Height : 1f;
                return new Rectangle(
                    anchorPoint.X - (int)Math.Round(origin.X * scaleX),
                    anchorPoint.Y - (int)Math.Round(origin.Y * scaleY),
                    tooltipWidth,
                    tooltipHeight);
            }

            return tooltipFrameIndex switch
            {
                0 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight),
                2 => new Rectangle(anchorPoint.X - tooltipWidth + 1, anchorPoint.Y, tooltipWidth, tooltipHeight),
                _ => new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight)
            };
        }

        private static int ComputeTooltipOverflow(Rectangle rect, int renderWidth, int renderHeight)
        {
            int overflow = 0;

            if (rect.Left < TooltipPadding)
            {
                overflow += TooltipPadding - rect.Left;
            }

            if (rect.Top < TooltipPadding)
            {
                overflow += TooltipPadding - rect.Top;
            }

            if (rect.Right > renderWidth - TooltipPadding)
            {
                overflow += rect.Right - (renderWidth - TooltipPadding);
            }

            if (rect.Bottom > renderHeight - TooltipPadding)
            {
                overflow += rect.Bottom - (renderHeight - TooltipPadding);
            }

            return overflow;
        }

        private static Rectangle ClampTooltipRect(Rectangle rect, int renderWidth, int renderHeight)
        {
            int minX = TooltipPadding;
            int minY = TooltipPadding;
            int maxX = Math.Max(minX, renderWidth - TooltipPadding - rect.Width);
            int maxY = Math.Max(minY, renderHeight - TooltipPadding - rect.Height);

            return new Rectangle(
                Math.Clamp(rect.X, minX, maxX),
                Math.Clamp(rect.Y, minY, maxY),
                rect.Width,
                rect.Height);
        }

        private Rectangle ResolveTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            ReadOnlySpan<int> framePreference,
            out int tooltipFrameIndex)
        {
            Rectangle bestRect = Rectangle.Empty;
            int bestFrame = framePreference.Length > 0 ? framePreference[0] : 1;
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < framePreference.Length; i++)
            {
                int frameIndex = framePreference[i];
                Rectangle candidate = CreateTooltipRectFromAnchor(anchorPoint, tooltipWidth, tooltipHeight, frameIndex);
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight);
                if (overflow == 0)
                {
                    tooltipFrameIndex = frameIndex;
                    return candidate;
                }

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    bestFrame = frameIndex;
                    bestRect = candidate;
                }
            }

            tooltipFrameIndex = bestFrame;
            return ClampTooltipRect(bestRect, renderWidth, renderHeight);
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;

            if (tooltipFrame != null)
            {
                sprite.Draw(tooltipFrame, rect, Color.White);
                return;
            }

            sprite.Draw(_pixel, rect, new Color(24, 30, 44, 235));
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            Color borderColor = new(87, 100, 128);
            sprite.Draw(_pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_pixel, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_pixel, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_pixel, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
        }

        private void DrawTooltipLines(SpriteBatch sprite, string[] lines, int x, float y, Color color)
        {
            if (lines == null)
            {
                return;
            }

            for (int i = 0; i < lines.Length; i++)
            {
                DrawTooltipText(sprite, lines[i], new Vector2(x, y + (i * _font.LineSpacing)), color);
            }
        }

        private void DrawTooltipText(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, text, position + Vector2.One, Color.Black);
            sprite.DrawString(_font, text, position, color);
        }

        private float MeasureLinesHeight(string[] lines)
        {
            if (_font == null || lines == null || lines.Length == 0)
            {
                return 0f;
            }

            int nonEmptyLineCount = lines.Count(line => !string.IsNullOrWhiteSpace(line));
            return nonEmptyLineCount <= 0
                ? 0f
                : (nonEmptyLineCount * _font.LineSpacing) - 2f;
        }

        private List<string[]> WrapTooltipSections(IEnumerable<string> lines, int width)
        {
            var wrappedSections = new List<string[]>();
            if (lines == null)
            {
                return wrappedSections;
            }

            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    wrappedSections.Add(WrapTooltipText(line, width));
                }
            }

            return wrappedSections;
        }

        private float MeasureWrappedSectionHeight(IReadOnlyList<string[]> wrappedSections)
        {
            if (wrappedSections == null || wrappedSections.Count == 0)
            {
                return 0f;
            }

            float height = 0f;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                float sectionHeight = MeasureLinesHeight(wrappedSections[i]);
                if (sectionHeight <= 0f)
                {
                    continue;
                }

                if (height > 0f)
                {
                    height += TooltipSectionGap;
                }

                height += sectionHeight;
            }

            return height;
        }

        private void DrawWrappedSections(SpriteBatch sprite, int x, float y, IReadOnlyList<string[]> wrappedSections)
        {
            if (wrappedSections == null)
            {
                return;
            }

            float currentY = y;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                string[] section = wrappedSections[i];
                float sectionHeight = MeasureLinesHeight(section);
                if (sectionHeight <= 0f)
                {
                    continue;
                }

                DrawTooltipLines(sprite, section, x, currentY, i == 0 ? new Color(181, 224, 255) : Color.White);
                currentY += sectionHeight + TooltipSectionGap;
            }
        }

        private string[] WrapTooltipText(string text, float width)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            if (width <= 0f || _font.MeasureString(text).X <= width)
            {
                return new[] { text };
            }

            List<string> lines = new();
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return new[] { text };
            }

            string currentLine = string.Empty;
            for (int i = 0; i < words.Length; i++)
            {
                string candidate = string.IsNullOrWhiteSpace(currentLine)
                    ? words[i]
                    : $"{currentLine} {words[i]}";
                if (_font.MeasureString(candidate).X <= width)
                {
                    currentLine = candidate;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentLine))
                {
                    lines.Add(currentLine);
                }

                currentLine = words[i];
            }

            if (!string.IsNullOrWhiteSpace(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines.Count > 0 ? lines.ToArray() : new[] { text };
        }

        private static string ResolveDisplayText(params string[] candidates)
        {
            if (candidates == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return string.Empty;
        }

        private static int ResolveDurabilityPercent(RepairEntry entry)
        {
            return entry == null
                ? 0
                : Math.Clamp((int)Math.Round((double)entry.CurrentDurability * 100d / Math.Max(1, entry.MaxDurability)), 0, 999);
        }
    }
}
