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

namespace HaCreator.MapSimulator.UI
{
    public sealed class TrunkUI : UIWindowBase
    {
        private const int MaxVisibleRows = 6;
        private const int RowHeight = 35;
        private const int TabCount = 5;
        private const float MoneyTextScale = 0.72f;

        private const int StorageRowX = 12;
        private const int StorageRowY = 93;
        private const int StorageRowWidth = 165;
        private const int InventoryRowX = 243;
        private const int InventoryRowY = 93;
        private const int InventoryRowWidth = 202;
        private const int RowIconX = 6;
        private const int RowIconY = 2;
        private const int RowQuantityRightPadding = 8;
        private const int RowTextX = 42;
        private const int RowTextY = 10;
        private const int MoneyStorageRightX = 193;
        private const int MoneyInventoryRightX = 424;
        private const int MoneyTextY = 279;
        private const int MesoPromptTextY = 291;
        private const int StatusTextX = 14;
        private const int StatusTextY = 308;
        private const int StatusTextWidth = 430;
        private const int MaxMesoDigits = 10;

        private readonly IDXObject _foreground;
        private readonly Point _foregroundOffset;
        private readonly IDXObject _contentOverlay;
        private readonly Point _contentOverlayOffset;
        private readonly Texture2D _selectionTexture;
        private readonly Dictionary<InventoryType, List<InventorySlotData>> _storageItems = new()
        {
            { InventoryType.EQUIP, new List<InventorySlotData>() },
            { InventoryType.USE, new List<InventorySlotData>() },
            { InventoryType.SETUP, new List<InventorySlotData>() },
            { InventoryType.ETC, new List<InventorySlotData>() },
            { InventoryType.CASH, new List<InventorySlotData>() }
        };
        private readonly Dictionary<InventoryType, int> _storageSlotLimits = new()
        {
            { InventoryType.EQUIP, 24 },
            { InventoryType.USE, 24 },
            { InventoryType.SETUP, 24 },
            { InventoryType.ETC, 24 },
            { InventoryType.CASH, 24 }
        };
        private readonly List<UIObject> _storageRowButtons = new();
        private readonly List<UIObject> _inventoryRowButtons = new();
        private readonly UIObject _withdrawButton;
        private readonly UIObject _depositButton;
        private readonly UIObject _sortButton;
        private readonly UIObject _withdrawMesoButton;
        private readonly UIObject _depositMesoButton;

        private InventoryUI _inventory;
        private IStorageRuntime _storageRuntime;
        private SpriteFont _font;
        private UIObject _tabEquip;
        private UIObject _tabUse;
        private UIObject _tabSetup;
        private UIObject _tabEtc;
        private UIObject _tabCash;
        private int _currentTab;
        private int _storageScrollOffset;
        private int _inventoryScrollOffset;
        private int _storageSelectedIndex = -1;
        private int _inventorySelectedIndex = -1;
        private int _previousScrollWheelValue;
        private KeyboardState _previousKeyboardState;
        private long _storageMeso;
        private string _statusMessage = "Select an item to deposit or withdraw.";
        private MesoEntryMode _mesoEntryMode = MesoEntryMode.None;
        private string _mesoEntryText = string.Empty;
        private long _mesoEntryMaxValue;
        private string _mesoEntryPrompt = string.Empty;
        private bool _mesoEntryReplaceOnDigit;

        private enum TrunkPane
        {
            None,
            Storage,
            Inventory
        }

        private enum MesoEntryMode
        {
            None,
            Withdraw,
            Deposit
        }

        public TrunkUI(
            IDXObject frame,
            IDXObject foreground,
            Point foregroundOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            Texture2D selectionTexture,
            UIObject withdrawButton,
            UIObject depositButton,
            UIObject sortButton,
            UIObject exitButton,
            UIObject withdrawMesoButton,
            UIObject depositMesoButton,
            GraphicsDevice device)
            : base(frame)
        {
            _foreground = foreground;
            _foregroundOffset = foregroundOffset;
            _contentOverlay = contentOverlay;
            _contentOverlayOffset = contentOverlayOffset;
            _selectionTexture = selectionTexture;
            _withdrawButton = withdrawButton;
            _depositButton = depositButton;
            _sortButton = sortButton;
            _withdrawMesoButton = withdrawMesoButton;
            _depositMesoButton = depositMesoButton;

            InitializeActionButtons(withdrawButton, depositButton, sortButton, exitButton, withdrawMesoButton, depositMesoButton);
            InitializeRowButtons(device);
            UpdateButtonStates();
        }

        public override string WindowName => MapSimulatorWindowNames.Trunk;
        public override bool CapturesKeyboardInput => IsVisible && _mesoEntryMode != MesoEntryMode.None;

        public override void Show()
        {
            base.Show();
            CancelMesoEntry();
            UpdateAccessStatusMessage();
            _previousScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            _previousKeyboardState = Keyboard.GetState();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void InitializeTabs(UIObject equipTab, UIObject useTab, UIObject setupTab, UIObject etcTab, UIObject cashTab)
        {
            _tabEquip = equipTab;
            _tabUse = useTab;
            _tabSetup = setupTab;
            _tabEtc = etcTab;
            _tabCash = cashTab;

            AttachTabButton(_tabEquip, 0);
            AttachTabButton(_tabUse, 1);
            AttachTabButton(_tabSetup, 2);
            AttachTabButton(_tabEtc, 3);
            AttachTabButton(_tabCash, 4);
            UpdateTabStates();
        }

        public void SetInventory(InventoryUI inventory)
        {
            _inventory = inventory;
            ClampSelection();
            UpdateButtonStates();
        }

        public void SetStorageRuntime(IStorageRuntime storageRuntime)
        {
            if (ReferenceEquals(_storageRuntime, storageRuntime))
            {
                return;
            }

            _storageRuntime = storageRuntime;
            SyncLocalStorageIntoRuntime();
            ClampSelection();
            UpdateAccessStatusMessage();
            UpdateButtonStates();
        }

        public void ConfigureStorageAccess(string accountLabel, string currentCharacterName, IEnumerable<string> sharedCharacterNames)
        {
            _storageRuntime?.ConfigureAccess(accountLabel, currentCharacterName, sharedCharacterNames);
            ClampSelection();
            UpdateAccessStatusMessage();
            UpdateButtonStates();
        }

        public void SetStorageMeso(long meso)
        {
            if (_storageRuntime != null)
            {
                _storageRuntime.SetMeso(meso);
            }
            else
            {
                _storageMeso = Math.Max(0, meso);
            }

            UpdateButtonStates();
        }

        public void SetSlotLimit(InventoryType type, int slotLimit)
        {
            if (type == InventoryType.NONE)
            {
                return;
            }

            if (_storageRuntime != null)
            {
                _storageRuntime.SetSlotLimit(slotLimit);
            }
            else
            {
                _storageSlotLimits[type] = Math.Max(MaxVisibleRows, slotLimit);
            }

            ClampSelection();
            UpdateButtonStates();
        }

        public void AddStoredItem(InventoryType type, InventorySlotData slotData)
        {
            if (_storageRuntime != null)
            {
                _storageRuntime.AddItem(type, slotData);
                ClampSelection();
                UpdateButtonStates();
                return;
            }

            if (type == InventoryType.NONE || slotData == null || !_storageItems.TryGetValue(type, out List<InventorySlotData> rows))
            {
                return;
            }

            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, slotData.MaxStackSize);
            int remainingQuantity = Math.Max(1, slotData.Quantity);
            if (IsStackable(type, maxStackSize))
            {
                remainingQuantity = FillExistingStacks(rows, slotData, remainingQuantity, maxStackSize);
            }

            while (remainingQuantity > 0 && rows.Count < GetStorageSlotLimit(type))
            {
                InventorySlotData stack = slotData.Clone();
                stack.Quantity = IsStackable(type, maxStackSize)
                    ? Math.Min(remainingQuantity, maxStackSize)
                    : 1;
                stack.MaxStackSize = maxStackSize;
                rows.Add(stack);
                remainingQuantity -= stack.Quantity;
            }

            ClampSelection();
            UpdateButtonStates();
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            UpdateTabStates();
            HandleMesoEntryInput();

            MouseState mouseState = Mouse.GetState();
            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            if (wheelDelta == 0)
            {
                return;
            }

            TrunkPane hoveredPane = ResolvePane(mouseState.X, mouseState.Y);
            if (hoveredPane == TrunkPane.Storage)
            {
                ScrollStorage(wheelDelta);
            }
            else if (hoveredPane == TrunkPane.Inventory)
            {
                ScrollInventory(wheelDelta);
            }
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
            DrawFrameLayer(sprite, skeletonMeshRenderer, gameTime, _foreground, _foregroundOffset, drawReflectionInfo);
            DrawFrameLayer(sprite, skeletonMeshRenderer, gameTime, _contentOverlay, _contentOverlayOffset, drawReflectionInfo);

            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            IReadOnlyList<InventorySlotData> storageRows = GetStorageRows(inventoryType);
            IReadOnlyList<InventorySlotData> inventoryRows = _inventory?.GetSlots(inventoryType) ?? Array.Empty<InventorySlotData>();

            DrawSelection(sprite, StorageRowX, StorageRowY, _storageSelectedIndex, _storageScrollOffset, StorageRowWidth);
            DrawSelection(sprite, InventoryRowX, InventoryRowY, _inventorySelectedIndex, _inventoryScrollOffset, InventoryRowWidth);

            DrawRows(sprite, storageRows, _storageScrollOffset, StorageRowX, StorageRowY, StorageRowWidth);
            DrawRows(sprite, inventoryRows, _inventoryScrollOffset, InventoryRowX, InventoryRowY, InventoryRowWidth);

            DrawMoneyValues(sprite);
            DrawMesoPrompt(sprite);
            DrawStatusText(sprite);
        }

        private void InitializeActionButtons(
            UIObject withdrawButton,
            UIObject depositButton,
            UIObject sortButton,
            UIObject exitButton,
            UIObject withdrawMesoButton,
            UIObject depositMesoButton)
        {
            if (withdrawButton != null)
            {
                AddButton(withdrawButton);
                withdrawButton.ButtonClickReleased += _ => WithdrawSelectedItem();
            }

            if (depositButton != null)
            {
                AddButton(depositButton);
                depositButton.ButtonClickReleased += _ => DepositSelectedItem();
            }

            if (sortButton != null)
            {
                AddButton(sortButton);
                sortButton.ButtonClickReleased += _ => SortCurrentTab();
            }

            if (exitButton != null)
            {
                InitializeCloseButton(exitButton);
            }

            if (withdrawMesoButton != null)
            {
                AddButton(withdrawMesoButton);
                withdrawMesoButton.ButtonClickReleased += _ => BeginMesoEntry(MesoEntryMode.Withdraw);
            }

            if (depositMesoButton != null)
            {
                AddButton(depositMesoButton);
                depositMesoButton.ButtonClickReleased += _ => BeginMesoEntry(MesoEntryMode.Deposit);
            }
        }

        private void InitializeRowButtons(GraphicsDevice device)
        {
            for (int row = 0; row < MaxVisibleRows; row++)
            {
                UIObject storageButton = CreateTransparentButton(device, StorageRowWidth, RowHeight);
                storageButton.X = StorageRowX;
                storageButton.Y = StorageRowY + (row * RowHeight);
                int storageRow = row;
                storageButton.ButtonClickReleased += _ => SelectStorageRow(storageRow);
                AddButton(storageButton);
                _storageRowButtons.Add(storageButton);

                UIObject inventoryButton = CreateTransparentButton(device, InventoryRowWidth, RowHeight);
                inventoryButton.X = InventoryRowX;
                inventoryButton.Y = InventoryRowY + (row * RowHeight);
                int inventoryRow = row;
                inventoryButton.ButtonClickReleased += _ => SelectInventoryRow(inventoryRow);
                AddButton(inventoryButton);
                _inventoryRowButtons.Add(inventoryButton);
            }
        }

        private void AttachTabButton(UIObject button, int tabIndex)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            button.ButtonClickReleased += _ => SetCurrentTab(tabIndex);
        }

        private void SetCurrentTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= TabCount)
            {
                return;
            }

            _currentTab = tabIndex;
            _storageScrollOffset = 0;
            _inventoryScrollOffset = 0;
            _storageSelectedIndex = -1;
            _inventorySelectedIndex = -1;
            CancelMesoEntry();
            _statusMessage = "Select an item to deposit or withdraw.";
            UpdateTabStates();
            UpdateButtonStates();
        }

        private void UpdateTabStates()
        {
            _tabEquip?.SetButtonState(_currentTab == 0 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabUse?.SetButtonState(_currentTab == 1 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabSetup?.SetButtonState(_currentTab == 2 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabEtc?.SetButtonState(_currentTab == 3 ? UIObjectState.Pressed : UIObjectState.Normal);
            _tabCash?.SetButtonState(_currentTab == 4 ? UIObjectState.Pressed : UIObjectState.Normal);
        }

        private void SelectStorageRow(int rowIndex)
        {
            int actualIndex = _storageScrollOffset + rowIndex;
            if (actualIndex < 0 || actualIndex >= GetStorageRows(GetInventoryTypeFromTab(_currentTab)).Count)
            {
                return;
            }

            _storageSelectedIndex = actualIndex;
            _inventorySelectedIndex = -1;
            UpdateButtonStates();
        }

        private void SelectInventoryRow(int rowIndex)
        {
            IReadOnlyList<InventorySlotData> inventoryRows = _inventory?.GetSlots(GetInventoryTypeFromTab(_currentTab)) ?? Array.Empty<InventorySlotData>();
            int actualIndex = _inventoryScrollOffset + rowIndex;
            if (actualIndex < 0 || actualIndex >= inventoryRows.Count)
            {
                return;
            }

            _inventorySelectedIndex = actualIndex;
            _storageSelectedIndex = -1;
            UpdateButtonStates();
        }

        private void WithdrawSelectedItem()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            IReadOnlyList<InventorySlotData> storageRows = GetStorageRows(inventoryType);
            if (!HasStorageAccess())
            {
                _statusMessage = BuildAccessDeniedMessage();
                UpdateButtonStates();
                return;
            }

            if (_inventory == null ||
                _storageSelectedIndex < 0 ||
                _storageSelectedIndex >= storageRows.Count)
            {
                _statusMessage = "Select a stored item to withdraw.";
                UpdateButtonStates();
                return;
            }

            InventorySlotData selected = storageRows[_storageSelectedIndex];
            if (selected == null || !_inventory.CanAcceptItem(inventoryType, selected.ItemId, Math.Max(1, selected.Quantity), selected.MaxStackSize))
            {
                _statusMessage = "Inventory cannot accept the selected item.";
                UpdateButtonStates();
                return;
            }

            InventorySlotData removed = RemoveStorageSlot(inventoryType, _storageSelectedIndex);
            if (removed == null)
            {
                _statusMessage = "Unable to withdraw the selected item.";
                UpdateButtonStates();
                return;
            }

            _inventory.AddItem(inventoryType, removed);
            _statusMessage = $"Withdrew {FormatItemLabel(removed)}.";
            ClampSelection();
            UpdateButtonStates();
        }

        private void DepositSelectedItem()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            IReadOnlyList<InventorySlotData> inventoryRows = _inventory?.GetSlots(inventoryType) ?? Array.Empty<InventorySlotData>();
            if (!HasStorageAccess())
            {
                _statusMessage = BuildAccessDeniedMessage();
                UpdateButtonStates();
                return;
            }

            if (_inventory == null ||
                _inventorySelectedIndex < 0 ||
                _inventorySelectedIndex >= inventoryRows.Count)
            {
                _statusMessage = "Select an inventory item to deposit.";
                UpdateButtonStates();
                return;
            }

            InventorySlotData selected = inventoryRows[_inventorySelectedIndex];
            if (selected == null || !CanAcceptStorageItem(inventoryType, selected))
            {
                _statusMessage = "Storage cannot accept the selected item.";
                UpdateButtonStates();
                return;
            }

            if (!_inventory.TryRemoveSlotAt(inventoryType, _inventorySelectedIndex, out InventorySlotData removed))
            {
                _statusMessage = "Unable to deposit the selected item.";
                UpdateButtonStates();
                return;
            }

            AddStoredItem(inventoryType, removed);
            _statusMessage = $"Deposited {FormatItemLabel(removed)}.";
            ClampSelection();
            UpdateButtonStates();
        }

        private void SortCurrentTab()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            if (!HasStorageAccess())
            {
                _statusMessage = BuildAccessDeniedMessage();
                UpdateButtonStates();
                return;
            }

            if (_storageRuntime != null)
            {
                _storageRuntime.SortSlots(inventoryType);
            }
            else if (_storageItems.TryGetValue(inventoryType, out List<InventorySlotData> storageRows))
            {
                storageRows.Sort(CompareSlots);
            }

            _inventory?.SortSlots(inventoryType);
            _statusMessage = $"{inventoryType} items sorted.";
            ClampSelection();
            UpdateButtonStates();
        }

        private void BeginMesoEntry(MesoEntryMode mode)
        {
            if (!HasStorageAccess())
            {
                _statusMessage = BuildAccessDeniedMessage();
                CancelMesoEntry();
                UpdateButtonStates();
                return;
            }

            long maxValue = mode == MesoEntryMode.Withdraw
                ? GetStorageMesoCount()
                : _inventory?.GetMesoCount() ?? 0;
            if (_inventory == null || maxValue <= 0)
            {
                _statusMessage = mode == MesoEntryMode.Withdraw
                    ? "No meso is stored."
                    : "No meso is available to deposit.";
                CancelMesoEntry();
                UpdateButtonStates();
                return;
            }

            _mesoEntryMode = mode;
            _mesoEntryMaxValue = maxValue;
            _mesoEntryText = maxValue.ToString(CultureInfo.InvariantCulture);
            _mesoEntryPrompt = mode == MesoEntryMode.Withdraw
                ? "Withdraw meso amount"
                : "Deposit meso amount";
            _mesoEntryReplaceOnDigit = true;
            UpdateButtonStates();
        }

        private void ConfirmMesoEntry()
        {
            if (!TryParseMesoEntry(out long amount))
            {
                _statusMessage = $"Enter a meso amount between 1 and {_mesoEntryMaxValue.ToString("N0", CultureInfo.InvariantCulture)}.";
                UpdateButtonStates();
                return;
            }

            if (_mesoEntryMode == MesoEntryMode.Withdraw)
            {
                _inventory.AddMeso(amount);
                SetStorageMeso(GetStorageMesoCount() - amount);
                _statusMessage = $"Withdrew {amount.ToString("N0", CultureInfo.InvariantCulture)} meso.";
            }
            else
            {
                if (_inventory.TryConsumeMeso(amount))
                {
                    SetStorageMeso(GetStorageMesoCount() + amount);
                    _statusMessage = $"Deposited {amount.ToString("N0", CultureInfo.InvariantCulture)} meso.";
                }
                else
                {
                    _statusMessage = "Unable to move meso into storage.";
                    UpdateButtonStates();
                    return;
                }
            }

            CancelMesoEntry();
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            IReadOnlyList<InventorySlotData> storageRows = GetStorageRows(inventoryType);
            IReadOnlyList<InventorySlotData> inventoryRows = _inventory?.GetSlots(inventoryType) ?? Array.Empty<InventorySlotData>();
            bool hasStorageAccess = HasStorageAccess();

            _withdrawButton?.SetEnabled(
                hasStorageAccess &&
                _inventory != null &&
                _storageSelectedIndex >= 0 &&
                _storageSelectedIndex < storageRows.Count &&
                storageRows[_storageSelectedIndex] != null &&
                _inventory.CanAcceptItem(
                    inventoryType,
                    storageRows[_storageSelectedIndex].ItemId,
                    Math.Max(1, storageRows[_storageSelectedIndex].Quantity),
                    storageRows[_storageSelectedIndex].MaxStackSize));

            _depositButton?.SetEnabled(
                hasStorageAccess &&
                _inventory != null &&
                _inventorySelectedIndex >= 0 &&
                _inventorySelectedIndex < inventoryRows.Count &&
                inventoryRows[_inventorySelectedIndex] != null &&
                CanAcceptStorageItem(inventoryType, inventoryRows[_inventorySelectedIndex]));

            _sortButton?.SetEnabled(hasStorageAccess && (storageRows.Count > 1 || inventoryRows.Count > 1));
            bool mesoEntryActive = _mesoEntryMode != MesoEntryMode.None;
            _withdrawMesoButton?.SetEnabled(!mesoEntryActive && hasStorageAccess && _inventory != null && GetStorageMesoCount() > 0);
            _depositMesoButton?.SetEnabled(!mesoEntryActive && hasStorageAccess && _inventory != null && _inventory.GetMesoCount() > 0);

            for (int row = 0; row < MaxVisibleRows; row++)
            {
                _storageRowButtons[row].SetVisible(_storageScrollOffset + row < storageRows.Count);
                _inventoryRowButtons[row].SetVisible(_inventoryScrollOffset + row < inventoryRows.Count);
            }
        }

        private void ScrollStorage(int wheelDelta)
        {
            int maxScroll = Math.Max(0, GetStorageRows(GetInventoryTypeFromTab(_currentTab)).Count - MaxVisibleRows);
            _storageScrollOffset = wheelDelta > 0
                ? Math.Max(0, _storageScrollOffset - 1)
                : Math.Min(maxScroll, _storageScrollOffset + 1);
            UpdateButtonStates();
        }

        private void ScrollInventory(int wheelDelta)
        {
            IReadOnlyList<InventorySlotData> inventoryRows = _inventory?.GetSlots(GetInventoryTypeFromTab(_currentTab)) ?? Array.Empty<InventorySlotData>();
            int maxScroll = Math.Max(0, inventoryRows.Count - MaxVisibleRows);
            _inventoryScrollOffset = wheelDelta > 0
                ? Math.Max(0, _inventoryScrollOffset - 1)
                : Math.Min(maxScroll, _inventoryScrollOffset + 1);
            UpdateButtonStates();
        }

        private void ClampSelection()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            IReadOnlyList<InventorySlotData> storageRows = GetStorageRows(inventoryType);
            IReadOnlyList<InventorySlotData> inventoryRows = _inventory?.GetSlots(inventoryType) ?? Array.Empty<InventorySlotData>();

            _storageSelectedIndex = storageRows.Count == 0 ? -1 : Math.Clamp(_storageSelectedIndex, 0, storageRows.Count - 1);
            _inventorySelectedIndex = inventoryRows.Count == 0 ? -1 : Math.Clamp(_inventorySelectedIndex, 0, inventoryRows.Count - 1);
            _storageScrollOffset = Math.Clamp(_storageScrollOffset, 0, Math.Max(0, storageRows.Count - MaxVisibleRows));
            _inventoryScrollOffset = Math.Clamp(_inventoryScrollOffset, 0, Math.Max(0, inventoryRows.Count - MaxVisibleRows));
        }

        private void DrawRows(SpriteBatch sprite, IReadOnlyList<InventorySlotData> rows, int scrollOffset, int originX, int originY, int rowWidth)
        {
            if (_font == null)
            {
                return;
            }

            int visibleCount = Math.Min(MaxVisibleRows, rows.Count - scrollOffset);
            for (int row = 0; row < visibleCount; row++)
            {
                InventorySlotData slotData = rows[scrollOffset + row];
                if (slotData == null)
                {
                    continue;
                }

                int drawX = Position.X + originX;
                int drawY = Position.Y + originY + (row * RowHeight);
                if (slotData.ItemTexture != null)
                {
                    sprite.Draw(slotData.ItemTexture, new Rectangle(drawX + RowIconX, drawY + RowIconY, 32, 32), Color.White);
                }

                string label = TrimToWidth(GetDisplayName(slotData), rowWidth - 82f);
                InventoryRenderUtil.DrawOutlinedText(sprite, _font, label, new Vector2(drawX + RowTextX, drawY + RowTextY), Color.White, 0.62f);

                if (slotData.Quantity > 1)
                {
                    string quantity = slotData.Quantity.ToString(CultureInfo.InvariantCulture);
                    Vector2 quantitySize = _font.MeasureString(quantity) * 0.62f;
                    Vector2 quantityPosition = new Vector2(drawX + rowWidth - quantitySize.X - RowQuantityRightPadding, drawY + RowTextY);
                    InventoryRenderUtil.DrawOutlinedText(sprite, _font, quantity, quantityPosition, new Color(255, 228, 151), 0.62f);
                }
            }
        }

        private void DrawSelection(SpriteBatch sprite, int rowX, int rowY, int selectedIndex, int scrollOffset, int width)
        {
            if (_selectionTexture == null || selectedIndex < scrollOffset || selectedIndex >= scrollOffset + MaxVisibleRows)
            {
                return;
            }

            int row = selectedIndex - scrollOffset;
            Rectangle destination = new Rectangle(Position.X + rowX, Position.Y + rowY + (row * RowHeight), width, RowHeight);
            sprite.Draw(_selectionTexture, destination, Color.White);
        }

        private void DrawMoneyValues(SpriteBatch sprite)
        {
            if (_font == null)
            {
                return;
            }

            DrawMoneyValue(sprite, GetStorageMesoCount(), MoneyStorageRightX);
            DrawMoneyValue(sprite, _inventory?.GetMesoCount() ?? 0, MoneyInventoryRightX);
        }

        private void DrawMoneyValue(SpriteBatch sprite, long amount, int rightAnchorX)
        {
            string text = amount.ToString("N0", CultureInfo.InvariantCulture);
            Vector2 size = _font.MeasureString(text) * MoneyTextScale;
            Vector2 position = new Vector2(Position.X + rightAnchorX - size.X, Position.Y + MoneyTextY);
            InventoryRenderUtil.DrawOutlinedText(sprite, _font, text, position, Color.White, MoneyTextScale);
        }

        private void DrawStatusText(SpriteBatch sprite)
        {
            if (_font == null || string.IsNullOrWhiteSpace(_statusMessage))
            {
                return;
            }

            InventoryRenderUtil.DrawOutlinedText(
                sprite,
                _font,
                TrimToWidth(_statusMessage, StatusTextWidth),
                new Vector2(Position.X + StatusTextX, Position.Y + StatusTextY),
                new Color(225, 225, 225),
                0.62f);
        }

        private void DrawMesoPrompt(SpriteBatch sprite)
        {
            if (_font == null || _mesoEntryMode == MesoEntryMode.None)
            {
                return;
            }

            string amountText = string.IsNullOrEmpty(_mesoEntryText) ? "0" : _mesoEntryText;
            string prompt = $"{_mesoEntryPrompt}: {amountText} / {_mesoEntryMaxValue.ToString("N0", CultureInfo.InvariantCulture)}";
            InventoryRenderUtil.DrawOutlinedText(
                sprite,
                _font,
                TrimToWidth(prompt, StatusTextWidth),
                new Vector2(Position.X + StatusTextX, Position.Y + MesoPromptTextY),
                new Color(255, 228, 151),
                0.62f);
        }

        private static UIObject CreateTransparentButton(GraphicsDevice device, int width, int height)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Transparent;
            }

            texture.SetData(pixels);
            BaseDXDrawableItem normal = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            BaseDXDrawableItem disabled = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            BaseDXDrawableItem pressed = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            BaseDXDrawableItem mouseOver = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            return new UIObject(normal, disabled, pressed, mouseOver);
        }

        private void DrawFrameLayer(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, IDXObject layer, Point offset, ReflectionDrawableBoundary drawReflectionInfo)
        {
            if (layer == null)
            {
                return;
            }

            layer.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + offset.X,
                Position.Y + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private IReadOnlyList<InventorySlotData> GetStorageRows(InventoryType type)
        {
            if (_storageRuntime != null)
            {
                return _storageRuntime.GetSlots(type);
            }

            return _storageItems.TryGetValue(type, out List<InventorySlotData> rows)
                ? rows
                : Array.Empty<InventorySlotData>();
        }

        private InventorySlotData RemoveStorageSlot(InventoryType type, int slotIndex)
        {
            if (_storageRuntime != null)
            {
                return _storageRuntime.TryRemoveSlotAt(type, slotIndex, out InventorySlotData removedFromRuntime)
                    ? removedFromRuntime
                    : null;
            }

            if (!_storageItems.TryGetValue(type, out List<InventorySlotData> rows) ||
                slotIndex < 0 ||
                slotIndex >= rows.Count)
            {
                return null;
            }

            InventorySlotData removed = rows[slotIndex]?.Clone();
            rows.RemoveAt(slotIndex);
            return removed;
        }

        private int GetStorageSlotLimit(InventoryType type)
        {
            if (_storageRuntime != null)
            {
                return Math.Max(MaxVisibleRows, _storageRuntime.GetSlotLimit());
            }

            return _storageSlotLimits.TryGetValue(type, out int value)
                ? Math.Max(MaxVisibleRows, value)
                : MaxVisibleRows;
        }

        private bool CanAcceptStorageItem(InventoryType type, InventorySlotData slotData)
        {
            IReadOnlyList<InventorySlotData> rows = GetStorageRows(type);
            if (slotData == null || type == InventoryType.NONE)
            {
                return false;
            }

            int remainingQuantity = Math.Max(1, slotData.Quantity);
            int maxStackSize = InventoryItemMetadataResolver.ResolveMaxStack(type, slotData.MaxStackSize);
            if (IsStackable(type, maxStackSize))
            {
                foreach (InventorySlotData row in rows)
                {
                    if (row == null || row.ItemId != slotData.ItemId || row.IsDisabled)
                    {
                        continue;
                    }

                    int rowMax = InventoryItemMetadataResolver.ResolveMaxStack(type, row.MaxStackSize);
                    int capacity = rowMax - Math.Max(1, row.Quantity);
                    if (capacity > 0)
                    {
                        remainingQuantity -= capacity;
                        if (remainingQuantity <= 0)
                        {
                            return true;
                        }
                    }
                }
            }

            if (!IsStackable(type, maxStackSize))
            {
                return rows.Count + remainingQuantity <= GetStorageSlotLimit(type);
            }

            int freeSlots = GetStorageSlotLimit(type) - rows.Count;
            int neededStacks = (remainingQuantity + maxStackSize - 1) / maxStackSize;
            return freeSlots >= neededStacks;
        }

        private static int FillExistingStacks(List<InventorySlotData> rows, InventorySlotData slotData, int remainingQuantity, int maxStackSize)
        {
            for (int i = 0; i < rows.Count && remainingQuantity > 0; i++)
            {
                InventorySlotData existing = rows[i];
                if (existing == null || existing.ItemId != slotData.ItemId || existing.IsDisabled)
                {
                    continue;
                }

                int rowMax = InventoryItemMetadataResolver.ResolveMaxStack(
                    InventoryItemMetadataResolver.ResolveInventoryType(existing.ItemId),
                    existing.MaxStackSize);
                int capacity = rowMax - Math.Max(1, existing.Quantity);
                if (capacity <= 0)
                {
                    continue;
                }

                int quantityToMerge = Math.Min(capacity, remainingQuantity);
                existing.Quantity += quantityToMerge;
                existing.MaxStackSize = maxStackSize;
                existing.ItemTexture ??= slotData.ItemTexture;
                existing.ItemName ??= slotData.ItemName;
                existing.GradeFrameIndex ??= slotData.GradeFrameIndex;
                existing.IsActiveBullet |= slotData.IsActiveBullet;
                remainingQuantity -= quantityToMerge;
            }

            return remainingQuantity;
        }

        private static int CompareSlots(InventorySlotData left, InventorySlotData right)
        {
            int leftId = left?.ItemId ?? int.MaxValue;
            int rightId = right?.ItemId ?? int.MaxValue;
            int idComparison = leftId.CompareTo(rightId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            int rightQuantity = right?.Quantity ?? 0;
            int leftQuantity = left?.Quantity ?? 0;
            return rightQuantity.CompareTo(leftQuantity);
        }

        private TrunkPane ResolvePane(int mouseX, int mouseY)
        {
            Rectangle storageRect = new Rectangle(Position.X + StorageRowX, Position.Y + StorageRowY, StorageRowWidth, MaxVisibleRows * RowHeight);
            if (storageRect.Contains(mouseX, mouseY))
            {
                return TrunkPane.Storage;
            }

            Rectangle inventoryRect = new Rectangle(Position.X + InventoryRowX, Position.Y + InventoryRowY, InventoryRowWidth, MaxVisibleRows * RowHeight);
            if (inventoryRect.Contains(mouseX, mouseY))
            {
                return TrunkPane.Inventory;
            }

            return TrunkPane.None;
        }

        private static InventoryType GetInventoryTypeFromTab(int tabIndex)
        {
            return tabIndex switch
            {
                0 => InventoryType.EQUIP,
                1 => InventoryType.USE,
                2 => InventoryType.SETUP,
                3 => InventoryType.ETC,
                4 => InventoryType.CASH,
                _ => InventoryType.NONE
            };
        }

        private static bool IsStackable(InventoryType type, int maxStackSize)
        {
            return type != InventoryType.EQUIP && maxStackSize > 1;
        }

        private static string GetDisplayName(InventorySlotData slotData)
        {
            if (slotData == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(slotData.ItemName)
                ? slotData.ItemName
                : $"{slotData.ItemId:D8}";
        }

        private static string FormatItemLabel(InventorySlotData slotData)
        {
            if (slotData == null)
            {
                return "item";
            }

            string name = GetDisplayName(slotData);
            return slotData.Quantity > 1
                ? $"{name} x{slotData.Quantity.ToString(CultureInfo.InvariantCulture)}"
                : name;
        }

        private string TrimToWidth(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (_font.MeasureString(text).X <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            string value = text;
            while (value.Length > 0 && _font.MeasureString(value + ellipsis).X > maxWidth)
            {
                value = value[..^1];
            }

            return string.IsNullOrEmpty(value) ? ellipsis : value + ellipsis;
        }

        private void HandleMesoEntryInput()
        {
            KeyboardState keyboardState = Keyboard.GetState();
            try
            {
                if (_mesoEntryMode == MesoEntryMode.None)
                {
                    return;
                }

                if (IsPressed(keyboardState, Keys.Escape))
                {
                    _statusMessage = "Meso transfer cancelled.";
                    CancelMesoEntry();
                    UpdateButtonStates();
                    return;
                }

                if (IsPressed(keyboardState, Keys.Enter))
                {
                    ConfirmMesoEntry();
                    return;
                }

                if (IsPressed(keyboardState, Keys.Back) && _mesoEntryText.Length > 0)
                {
                    _mesoEntryText = _mesoEntryReplaceOnDigit ? string.Empty : _mesoEntryText[..^1];
                    _mesoEntryReplaceOnDigit = false;
                }

                foreach (Keys key in keyboardState.GetPressedKeys())
                {
                    if (_previousKeyboardState.IsKeyDown(key))
                    {
                        continue;
                    }

                    char? digit = KeyToDigit(key);
                    if (!digit.HasValue)
                    {
                        continue;
                    }

                    string source = _mesoEntryReplaceOnDigit ? digit.Value.ToString() : _mesoEntryText + digit.Value;
                    string rawValue = source.TrimStart('0');
                    string candidate = string.IsNullOrEmpty(rawValue) ? "0" : rawValue;
                    if (candidate.Length > MaxMesoDigits)
                    {
                        continue;
                    }

                    if (long.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) &&
                        parsed <= int.MaxValue)
                    {
                        _mesoEntryText = candidate;
                        _mesoEntryReplaceOnDigit = false;
                    }
                }
            }
            finally
            {
                _previousKeyboardState = keyboardState;
            }
        }

        private void CancelMesoEntry()
        {
            _mesoEntryMode = MesoEntryMode.None;
            _mesoEntryText = string.Empty;
            _mesoEntryMaxValue = 0;
            _mesoEntryPrompt = string.Empty;
            _mesoEntryReplaceOnDigit = false;
        }

        private bool TryParseMesoEntry(out long amount)
        {
            amount = 0;
            if (_mesoEntryMode == MesoEntryMode.None ||
                string.IsNullOrWhiteSpace(_mesoEntryText) ||
                !long.TryParse(_mesoEntryText, NumberStyles.None, CultureInfo.InvariantCulture, out amount))
            {
                return false;
            }

            return amount > 0 && amount <= _mesoEntryMaxValue;
        }

        private bool IsPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private long GetStorageMesoCount()
        {
            return _storageRuntime?.GetMesoCount() ?? _storageMeso;
        }

        private bool HasStorageAccess()
        {
            return _storageRuntime?.CanCurrentCharacterAccess ?? true;
        }

        private void SyncLocalStorageIntoRuntime()
        {
            if (_storageRuntime == null)
            {
                return;
            }

            bool hasLocalItems = false;
            foreach (List<InventorySlotData> rows in _storageItems.Values)
            {
                if (rows.Count > 0)
                {
                    hasLocalItems = true;
                    break;
                }
            }

            bool hasNonDefaultSlotLimit = false;
            foreach (int value in _storageSlotLimits.Values)
            {
                if (value != 24)
                {
                    hasNonDefaultSlotLimit = true;
                    break;
                }
            }

            if (!hasLocalItems && _storageMeso <= 0 && !hasNonDefaultSlotLimit)
            {
                return;
            }

            int slotLimit = MaxVisibleRows;
            foreach (int value in _storageSlotLimits.Values)
            {
                slotLimit = Math.Max(slotLimit, value);
            }

            _storageRuntime.SetSlotLimit(slotLimit);
            _storageRuntime.SetMeso(_storageMeso);
            foreach ((InventoryType type, List<InventorySlotData> rows) in _storageItems)
            {
                foreach (InventorySlotData row in rows)
                {
                    _storageRuntime.AddItem(type, row);
                }

                rows.Clear();
            }

            _storageMeso = 0;
        }

        private void UpdateAccessStatusMessage()
        {
            if (_mesoEntryMode != MesoEntryMode.None)
            {
                return;
            }

            if (!HasStorageAccess())
            {
                _statusMessage = BuildAccessDeniedMessage();
                return;
            }

            if (_storageRuntime == null)
            {
                if (string.IsNullOrWhiteSpace(_statusMessage))
                {
                    _statusMessage = "Select an item to deposit or withdraw.";
                }

                return;
            }

            string accountLabel = string.IsNullOrWhiteSpace(_storageRuntime.AccountLabel)
                ? "storage"
                : _storageRuntime.AccountLabel;
            string currentCharacterName = string.IsNullOrWhiteSpace(_storageRuntime.CurrentCharacterName)
                ? "current character"
                : _storageRuntime.CurrentCharacterName;
            int sharedCount = _storageRuntime.SharedCharacterNames?.Count ?? 0;
            _statusMessage = sharedCount > 0
                ? $"{currentCharacterName} is using {accountLabel}. Shared roster: {sharedCount} character(s)."
                : $"{currentCharacterName} is using {accountLabel}.";
        }

        private string BuildAccessDeniedMessage()
        {
            if (_storageRuntime == null)
            {
                return "Storage access is unavailable.";
            }

            string accountLabel = string.IsNullOrWhiteSpace(_storageRuntime.AccountLabel)
                ? "this storage"
                : _storageRuntime.AccountLabel;
            string currentCharacterName = string.IsNullOrWhiteSpace(_storageRuntime.CurrentCharacterName)
                ? "This character"
                : _storageRuntime.CurrentCharacterName;
            return $"{currentCharacterName} is not authorized to access {accountLabel}.";
        }

        private static char? KeyToDigit(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return null;
        }
    }
}
