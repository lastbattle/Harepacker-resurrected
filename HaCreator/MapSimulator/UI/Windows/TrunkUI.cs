using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
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
    public sealed partial class TrunkUI : UIWindowBase, ISoftKeyboardHost
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
        private const int StorageScrollBarX = 214;
        private const int StorageScrollBarY = 92;
        private const int StorageScrollBarHeight = 204;
        private const int InventoryScrollBarX = 445;
        private const int InventoryScrollBarY = 134;
        private const int InventoryScrollBarHeight = 162;
        private const int ScrollButtonHeight = 12;
        private const int MoneyStorageRightX = 193;
        private const int MoneyInventoryRightX = 424;
        private const int MoneyTextY = 279;
        private const int MesoPromptTextY = 291;
        private const int StatusTextX = 14;
        private const int StatusTextY = 308;
        private const int StatusTextWidth = 430;
        private const int MaxMesoDigits = 10;
        private const int MaxAccountSecurityLength = 16;
        private const int ClientPicEditMaxLength = 8;
        private const int MinSecondaryPasswordDigits = 4;
        private const int MaxSecondaryPasswordDigits = 8;
        private const int TooltipPadding = 10;
        private const int TooltipIconSize = 32;
        private const int TooltipIconGap = 8;
        private const int TooltipOffsetX = 18;
        private const int TooltipOffsetY = 14;
        private const int TooltipSectionGap = 6;
        private const int TooltipFallbackWidth = 214;
        private const int TooltipBitmapGap = 1;

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
        private readonly Texture2D _scrollBaseEnabledTexture;
        private readonly Texture2D _scrollBaseDisabledTexture;
        private readonly Texture2D _scrollThumbNormalTexture;
        private readonly Texture2D _scrollThumbHoverTexture;
        private readonly Texture2D _scrollThumbPressedTexture;
        private readonly Texture2D _scrollPrevNormalTexture;
        private readonly Texture2D _scrollPrevHoverTexture;
        private readonly Texture2D _scrollPrevPressedTexture;
        private readonly Texture2D _scrollPrevDisabledTexture;
        private readonly Texture2D _scrollNextNormalTexture;
        private readonly Texture2D _scrollNextHoverTexture;
        private readonly Texture2D _scrollNextPressedTexture;
        private readonly Texture2D _scrollNextDisabledTexture;
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Point[] _tooltipFrameOrigins = new Point[3];
        private readonly Texture2D _debugTooltipTexture;

        private InventoryUI _inventory;
        private IStorageRuntime _storageRuntime;
        private SpriteFont _font;
        private EquipUIBigBang.EquipTooltipAssets _equipTooltipAssets;
        private CharacterLoader _characterLoader;
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
        private readonly int[] _storageScrollOffsetsByTab = new int[TabCount];
        private readonly int[] _inventoryScrollOffsetsByTab = new int[TabCount];
        private readonly int[] _storageSelectedIndicesByTab = new int[TabCount];
        private readonly int[] _inventorySelectedIndicesByTab = new int[TabCount];
        private int _previousScrollWheelValue;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private long _storageMeso;
        private string _statusMessage = "Select an item to deposit or withdraw.";
        private MesoEntryMode _mesoEntryMode = MesoEntryMode.None;
        private string _mesoEntryText = string.Empty;
        private long _mesoEntryMaxValue;
        private string _mesoEntryPrompt = string.Empty;
        private bool _mesoEntryReplaceOnDigit;
        private string _secondaryPasswordConfirmationText = string.Empty;
        private bool _softKeyboardActive;
        private string _compositionText = string.Empty;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;
        private Point _lastMousePosition;
        private TrunkPane _hoveredPane = TrunkPane.None;
        private int _hoveredStorageIndex = -1;
        private int _hoveredInventoryIndex = -1;
        private ScrollbarPane? _draggingScrollbarPane;
        private int _scrollbarThumbDragOffsetY;

        internal Func<TrunkAccountSecurityPromptKind, bool> AccountSecurityPromptRequested { get; set; }
        internal Func<bool> CloseRequested { get; set; }
        internal Action<TrunkUI> WindowHidden { get; set; }

        private readonly struct TooltipSection
        {
            public TooltipSection(string text, Color color)
            {
                Text = text;
                Color = color;
            }

            public string Text { get; }
            public Color Color { get; }
        }

        private readonly struct TooltipLabeledValueRow
        {
            public TooltipLabeledValueRow(
                Texture2D labelTexture,
                string fallbackLabel,
                string valueText,
                Color valueColor,
                IReadOnlyList<TooltipValueSegment> valueSegments = null)
            {
                LabelTexture = labelTexture;
                FallbackLabel = fallbackLabel;
                ValueText = valueText;
                ValueColor = valueColor;
                ValueSegments = valueSegments;
            }

            public Texture2D LabelTexture { get; }
            public string FallbackLabel { get; }
            public string ValueText { get; }
            public Color ValueColor { get; }
            public IReadOnlyList<TooltipValueSegment> ValueSegments { get; }
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
            Deposit,
            VerifyAccountPic,
            VerifyAccountSecondaryPassword,
            SetupSecondaryPassword,
            ConfirmSecondaryPassword,
            VerifySecondaryPassword
        }

        private enum ScrollbarPane
        {
            Storage,
            Inventory
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
            Texture2D scrollPrevNormalTexture,
            Texture2D scrollPrevHoverTexture,
            Texture2D scrollPrevPressedTexture,
            Texture2D scrollPrevDisabledTexture,
            Texture2D scrollNextNormalTexture,
            Texture2D scrollNextHoverTexture,
            Texture2D scrollNextPressedTexture,
            Texture2D scrollNextDisabledTexture,
            Texture2D scrollBaseEnabledTexture,
            Texture2D scrollBaseDisabledTexture,
            Texture2D scrollThumbNormalTexture,
            Texture2D scrollThumbHoverTexture,
            Texture2D scrollThumbPressedTexture,
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
            _scrollPrevNormalTexture = scrollPrevNormalTexture;
            _scrollPrevHoverTexture = scrollPrevHoverTexture;
            _scrollPrevPressedTexture = scrollPrevPressedTexture;
            _scrollPrevDisabledTexture = scrollPrevDisabledTexture;
            _scrollNextNormalTexture = scrollNextNormalTexture;
            _scrollNextHoverTexture = scrollNextHoverTexture;
            _scrollNextPressedTexture = scrollNextPressedTexture;
            _scrollNextDisabledTexture = scrollNextDisabledTexture;
            _scrollBaseEnabledTexture = scrollBaseEnabledTexture;
            _scrollBaseDisabledTexture = scrollBaseDisabledTexture;
            _scrollThumbNormalTexture = scrollThumbNormalTexture;
            _scrollThumbHoverTexture = scrollThumbHoverTexture;
            _scrollThumbPressedTexture = scrollThumbPressedTexture;

            if (device != null)
            {
                _debugTooltipTexture = new Texture2D(device, 1, 1);
                _debugTooltipTexture.SetData(new[] { Color.White });
            }

            Array.Fill(_storageSelectedIndicesByTab, -1);
            Array.Fill(_inventorySelectedIndicesByTab, -1);

            InitializeActionButtons(withdrawButton, depositButton, sortButton, exitButton, withdrawMesoButton, depositMesoButton);
            InitializeRowButtons(device);
            UpdateButtonStates();
        }

        public override string WindowName => MapSimulatorWindowNames.Trunk;
        public override bool CapturesKeyboardInput => IsVisible && _mesoEntryMode != MesoEntryMode.None;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _mesoEntryMode != MesoEntryMode.None && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => GetSoftKeyboardType(_mesoEntryMode);
        int ISoftKeyboardHost.SoftKeyboardTextLength => GetEffectiveEntryValue().Length;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => GetEntryMaxLength(_mesoEntryMode);
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => _mesoEntryMode != MesoEntryMode.None;

        public override void Show()
        {
            base.Show();
            _storageRuntime?.BeginAccessSession();
            CancelMesoEntry();
            UpdateAccessStatusMessage();
            TryRequestExternalAccountSecurityPrompt();
            _previousScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _draggingScrollbarPane = null;
            UpdateButtonStates();
        }

        public override void Hide()
        {
            base.Hide();
            _storageRuntime?.EndAccessSession();
            CancelMesoEntry();
            UpdateAccessStatusMessage();
            _previousMouseState = Mouse.GetState();
            _draggingScrollbarPane = null;
            UpdateButtonStates();
            WindowHidden?.Invoke(this);
        }

        protected override void OnCloseButtonClicked(UIObject sender)
        {
            if (CloseRequested?.Invoke() == false)
            {
                return;
            }

            base.OnCloseButtonClicked(sender);
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
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

        public void SetEquipTooltipAssets(EquipUIBigBang.EquipTooltipAssets assets)
        {
            _equipTooltipAssets = assets;
        }

        public void SetCharacterLoader(CharacterLoader characterLoader)
        {
            _characterLoader = characterLoader;
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

        public IStorageRuntime StorageRuntime => _storageRuntime;

        public void ConfigureStorageAccess(string accountLabel, string accountKey, string currentCharacterName, IEnumerable<string> sharedCharacterNames)
        {
            _storageRuntime?.ConfigureAccess(accountLabel, accountKey, currentCharacterName, sharedCharacterNames);
            ClampSelection();
            UpdateAccessStatusMessage();
            UpdateButtonStates();
        }

        public void ConfigureStorageLoginSecurity(string picCode, bool secondaryPasswordEnabled, string secondaryPassword)
        {
            _storageRuntime?.ConfigureLoginAccountSecurity(picCode, secondaryPasswordEnabled, secondaryPassword);
            UpdateAccessStatusMessage();
            UpdateButtonStates();
        }

        public void ResumeSecurityUnlockFlow()
        {
            CancelMesoEntry();
            UpdateAccessStatusMessage();
            if (!IsVisible || _storageRuntime == null || !_storageRuntime.CanCurrentCharacterAccess)
            {
                UpdateButtonStates();
                return;
            }

            if (!_storageRuntime.IsClientAccountAuthorityVerified || !_storageRuntime.IsSecondaryPasswordVerified)
            {
                BeginSecondaryPasswordEntry();
            }
            else
            {
                UpdateButtonStates();
            }
        }

        public void RefreshSecurityStatus()
        {
            CancelMesoEntry();
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
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            _hoveredPane = ResolvePane(mouseState.X, mouseState.Y);
            _hoveredStorageIndex = _hoveredPane == TrunkPane.Storage ? ResolveRowIndex(mouseState.X, mouseState.Y, true) : -1;
            _hoveredInventoryIndex = _hoveredPane == TrunkPane.Inventory ? ResolveRowIndex(mouseState.X, mouseState.Y, false) : -1;
            UpdateScrollbarDrag(mouseState);
            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            if (wheelDelta == 0 || _mesoEntryMode != MesoEntryMode.None)
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

            DrawScrollBar(sprite, ScrollbarPane.Storage);
            DrawScrollBar(sprite, ScrollbarPane.Inventory);
            DrawMoneyValues(sprite);
            DrawMesoPrompt(sprite);
            DrawImeCandidateWindow(sprite);
            DrawStatusText(sprite);
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
            DrawHoveredSlotTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            bool handled = base.CheckMouseEvent(shiftCenteredX, shiftCenteredY, mouseState, mouseCursor, renderWidth, renderHeight);
            if (!IsVisible)
            {
                _previousMouseState = mouseState;
                return handled;
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (!handled && leftJustPressed && _mesoEntryMode != MesoEntryMode.None && GetEntryBounds().Contains(mouseState.Position))
            {
                _softKeyboardActive = true;
                ClearCompositionText();
                UpdateButtonStates();
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                handled = true;
            }
            else if (!handled && leftJustPressed && _mesoEntryMode == MesoEntryMode.None && TryHandleScrollBarMouseDown(mouseState))
            {
                mouseCursor?.SetMouseCursorMovedToClickableItem();
                handled = true;
            }

            _previousMouseState = mouseState;
            return handled;
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

            SaveCurrentTabState();
            _currentTab = tabIndex;
            LoadCurrentTabState();
            CancelMesoEntry();
            UpdateAccessStatusMessage();
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
            SyncCurrentTabState();
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
            SyncCurrentTabState();
            UpdateButtonStates();
        }

        private void WithdrawSelectedItem()
        {
            InventoryType inventoryType = GetInventoryTypeFromTab(_currentTab);
            IReadOnlyList<InventorySlotData> storageRows = GetStorageRows(inventoryType);
            if (!EnsureStorageAccess())
            {
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
            if (!EnsureStorageAccess())
            {
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
            if (!EnsureStorageAccess())
            {
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

            _statusMessage = $"{inventoryType} items sorted.";
            ClampSelection();
            UpdateButtonStates();
        }

        private void BeginMesoEntry(MesoEntryMode mode)
        {
            bool isMesoTransfer = mode == MesoEntryMode.Withdraw || mode == MesoEntryMode.Deposit;
            if (isMesoTransfer && !EnsureStorageAccess())
            {
                CancelMesoEntry();
                UpdateButtonStates();
                return;
            }

            long maxValue = mode == MesoEntryMode.Withdraw
                ? GetStorageMesoCount()
                : mode == MesoEntryMode.Deposit
                    ? _inventory?.GetMesoCount() ?? 0
                    : 0;
            if (isMesoTransfer && (_inventory == null || maxValue <= 0))
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
            _mesoEntryText = isMesoTransfer
                ? maxValue.ToString(CultureInfo.InvariantCulture)
                : IsNumericEntryMode(mode)
                    ? "0"
                    : string.Empty;
            _mesoEntryPrompt = mode switch
            {
                MesoEntryMode.Withdraw => "Withdraw meso amount",
                MesoEntryMode.Deposit => "Deposit meso amount",
                MesoEntryMode.VerifyAccountPic => "Enter account PIC",
                MesoEntryMode.VerifyAccountSecondaryPassword => "Enter account secondary password",
                MesoEntryMode.SetupSecondaryPassword => "Create storage passcode",
                MesoEntryMode.ConfirmSecondaryPassword => "Confirm storage passcode",
                MesoEntryMode.VerifySecondaryPassword => "Enter storage passcode",
                _ => string.Empty
            };
            _mesoEntryReplaceOnDigit = true;
            _secondaryPasswordConfirmationText ??= string.Empty;
            _softKeyboardActive = true;
            ClearCompositionText();
            _previousMouseState = Mouse.GetState();
            UpdateButtonStates();
        }

        private void ConfirmMesoEntry()
        {
            if (_mesoEntryMode == MesoEntryMode.VerifyAccountPic ||
                _mesoEntryMode == MesoEntryMode.VerifyAccountSecondaryPassword ||
                _mesoEntryMode == MesoEntryMode.SetupSecondaryPassword ||
                _mesoEntryMode == MesoEntryMode.ConfirmSecondaryPassword ||
                _mesoEntryMode == MesoEntryMode.VerifySecondaryPassword)
            {
                ConfirmSecurityEntry();
                return;
            }

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
            bool promptActive = _mesoEntryMode != MesoEntryMode.None;
            bool canSelectStorageRows = !promptActive && hasStorageAccess;
            bool canSelectInventoryRows = !promptActive;

            _withdrawButton?.SetEnabled(
                !promptActive &&
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
                !promptActive &&
                hasStorageAccess &&
                _inventory != null &&
                _inventorySelectedIndex >= 0 &&
                _inventorySelectedIndex < inventoryRows.Count &&
                inventoryRows[_inventorySelectedIndex] != null &&
                CanAcceptStorageItem(inventoryType, inventoryRows[_inventorySelectedIndex]));

            _sortButton?.SetEnabled(!promptActive && hasStorageAccess && storageRows.Count > 1);
            _withdrawMesoButton?.SetEnabled(!promptActive && hasStorageAccess && _inventory != null && GetStorageMesoCount() > 0);
            _depositMesoButton?.SetEnabled(!promptActive && hasStorageAccess && _inventory != null && _inventory.GetMesoCount() > 0);

            for (int row = 0; row < MaxVisibleRows; row++)
            {
                bool storageVisible = _storageScrollOffset + row < storageRows.Count;
                bool inventoryVisible = _inventoryScrollOffset + row < inventoryRows.Count;
                _storageRowButtons[row].SetVisible(storageVisible);
                _inventoryRowButtons[row].SetVisible(inventoryVisible);
                _storageRowButtons[row].SetEnabled(storageVisible && canSelectStorageRows);
                _inventoryRowButtons[row].SetEnabled(inventoryVisible && canSelectInventoryRows);
            }

            SetTabEnabledState(!promptActive);
            if (!promptActive)
            {
                UpdateTabStates();
            }
        }

        private void ScrollStorage(int wheelDelta)
        {
            int maxScroll = Math.Max(0, GetStorageRows(GetInventoryTypeFromTab(_currentTab)).Count - MaxVisibleRows);
            _storageScrollOffset = wheelDelta > 0
                ? Math.Max(0, _storageScrollOffset - 1)
                : Math.Min(maxScroll, _storageScrollOffset + 1);
            SyncCurrentTabState();
            UpdateButtonStates();
        }

        private void ScrollInventory(int wheelDelta)
        {
            IReadOnlyList<InventorySlotData> inventoryRows = _inventory?.GetSlots(GetInventoryTypeFromTab(_currentTab)) ?? Array.Empty<InventorySlotData>();
            int maxScroll = Math.Max(0, inventoryRows.Count - MaxVisibleRows);
            _inventoryScrollOffset = wheelDelta > 0
                ? Math.Max(0, _inventoryScrollOffset - 1)
                : Math.Min(maxScroll, _inventoryScrollOffset + 1);
            SyncCurrentTabState();
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
            SyncCurrentTabState();
        }

        private void SaveCurrentTabState()
        {
            _storageScrollOffsetsByTab[_currentTab] = _storageScrollOffset;
            _inventoryScrollOffsetsByTab[_currentTab] = _inventoryScrollOffset;
            _storageSelectedIndicesByTab[_currentTab] = _storageSelectedIndex;
            _inventorySelectedIndicesByTab[_currentTab] = _inventorySelectedIndex;
        }

        private void LoadCurrentTabState()
        {
            _storageScrollOffset = _storageScrollOffsetsByTab[_currentTab];
            _inventoryScrollOffset = _inventoryScrollOffsetsByTab[_currentTab];
            _storageSelectedIndex = _storageSelectedIndicesByTab[_currentTab];
            _inventorySelectedIndex = _inventorySelectedIndicesByTab[_currentTab];
            ClampSelection();
        }

        private void SyncCurrentTabState()
        {
            SaveCurrentTabState();
        }

        private void SetTabEnabledState(bool enabled)
        {
            _tabEquip?.SetEnabled(enabled);
            _tabUse?.SetEnabled(enabled);
            _tabSetup?.SetEnabled(enabled);
            _tabEtc?.SetEnabled(enabled);
            _tabCash?.SetEnabled(enabled);
        }

        private void DrawScrollBar(SpriteBatch sprite, ScrollbarPane pane)
        {
            Rectangle baseBounds = GetScrollBaseBounds(pane);
            Rectangle prevBounds = GetScrollPrevBounds(pane);
            Rectangle nextBounds = GetScrollNextBounds(pane);
            Rectangle thumbBounds = GetScrollThumbBounds(pane);
            bool canScroll = CanInteractWithScrollbar(pane) && GetMaxScrollOffset(pane) > 0;
            bool pressed = _draggingScrollbarPane == pane;
            bool hoverPrev = prevBounds.Contains(_lastMousePosition);
            bool hoverNext = nextBounds.Contains(_lastMousePosition);
            bool hoverThumb = thumbBounds.Contains(_lastMousePosition);

            DrawTexture(sprite, canScroll ? _scrollBaseEnabledTexture : _scrollBaseDisabledTexture, baseBounds, canScroll ? Color.White : new Color(255, 255, 255, 200));
            DrawTexture(sprite, ResolveScrollPrevTexture(canScroll, hoverPrev), prevBounds, Color.White);
            DrawTexture(sprite, ResolveScrollNextTexture(canScroll, hoverNext), nextBounds, Color.White);
            DrawTexture(sprite, ResolveScrollThumbTexture(canScroll, hoverThumb, pressed), thumbBounds, Color.White);
        }

        private void DrawTexture(SpriteBatch sprite, Texture2D texture, Rectangle bounds, Color color)
        {
            if (texture != null)
            {
                sprite.Draw(texture, bounds.Location.ToVector2(), color);
                return;
            }

            if (_debugTooltipTexture != null)
            {
                sprite.Draw(_debugTooltipTexture, bounds, color);
            }
        }

        private Texture2D ResolveScrollPrevTexture(bool canScroll, bool hover)
        {
            if (!canScroll)
            {
                return _scrollPrevDisabledTexture ?? _scrollPrevNormalTexture;
            }

            return hover ? _scrollPrevHoverTexture ?? _scrollPrevPressedTexture ?? _scrollPrevNormalTexture : _scrollPrevNormalTexture;
        }

        private Texture2D ResolveScrollNextTexture(bool canScroll, bool hover)
        {
            if (!canScroll)
            {
                return _scrollNextDisabledTexture ?? _scrollNextNormalTexture;
            }

            return hover ? _scrollNextHoverTexture ?? _scrollNextPressedTexture ?? _scrollNextNormalTexture : _scrollNextNormalTexture;
        }

        private Texture2D ResolveScrollThumbTexture(bool canScroll, bool hover, bool pressed)
        {
            if (!canScroll)
            {
                return _scrollThumbPressedTexture ?? _scrollThumbNormalTexture;
            }

            if (pressed)
            {
                return _scrollThumbPressedTexture ?? _scrollThumbHoverTexture ?? _scrollThumbNormalTexture;
            }

            return hover ? _scrollThumbHoverTexture ?? _scrollThumbNormalTexture : _scrollThumbNormalTexture;
        }

        private bool TryHandleScrollBarMouseDown(MouseState mouseState)
        {
            foreach (ScrollbarPane pane in Enum.GetValues(typeof(ScrollbarPane)))
            {
                Rectangle barBounds = GetScrollBarBounds(pane);
                if (!barBounds.Contains(mouseState.Position) || !CanInteractWithScrollbar(pane))
                {
                    continue;
                }

                int maxScroll = GetMaxScrollOffset(pane);
                if (maxScroll <= 0)
                {
                    return true;
                }

                if (GetScrollPrevBounds(pane).Contains(mouseState.Position))
                {
                    AdjustScrollOffset(pane, -1);
                    return true;
                }

                if (GetScrollNextBounds(pane).Contains(mouseState.Position))
                {
                    AdjustScrollOffset(pane, 1);
                    return true;
                }

                Rectangle thumbBounds = GetScrollThumbBounds(pane);
                if (thumbBounds.Contains(mouseState.Position))
                {
                    _draggingScrollbarPane = pane;
                    _scrollbarThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
                    return true;
                }

                Rectangle trackBounds = GetScrollTrackBounds(pane);
                if (trackBounds.Contains(mouseState.Position))
                {
                    AdjustScrollOffset(pane, mouseState.Y < thumbBounds.Y ? -MaxVisibleRows : MaxVisibleRows);
                    return true;
                }
            }

            return false;
        }

        private void UpdateScrollbarDrag(MouseState mouseState)
        {
            if (!_draggingScrollbarPane.HasValue)
            {
                return;
            }

            if (mouseState.LeftButton != ButtonState.Pressed)
            {
                _draggingScrollbarPane = null;
                return;
            }

            SetScrollOffsetFromThumb(_draggingScrollbarPane.Value, mouseState.Y);
        }

        private bool CanInteractWithScrollbar(ScrollbarPane pane)
        {
            return _mesoEntryMode == MesoEntryMode.None
                && (pane != ScrollbarPane.Storage || HasStorageAccess())
                && GetMaxScrollOffset(pane) > 0;
        }

        private Rectangle GetScrollBarBounds(ScrollbarPane pane)
        {
            return pane switch
            {
                ScrollbarPane.Storage => new Rectangle(Position.X + StorageScrollBarX, Position.Y + StorageScrollBarY, _scrollBaseEnabledTexture?.Width ?? 11, StorageScrollBarHeight),
                _ => new Rectangle(Position.X + InventoryScrollBarX, Position.Y + InventoryScrollBarY, _scrollBaseEnabledTexture?.Width ?? 11, InventoryScrollBarHeight)
            };
        }

        private Rectangle GetScrollBaseBounds(ScrollbarPane pane)
        {
            return GetScrollBarBounds(pane);
        }

        private Rectangle GetScrollPrevBounds(ScrollbarPane pane)
        {
            Rectangle bounds = GetScrollBarBounds(pane);
            return new Rectangle(bounds.X, bounds.Y, _scrollPrevNormalTexture?.Width ?? bounds.Width, _scrollPrevNormalTexture?.Height ?? ScrollButtonHeight);
        }

        private Rectangle GetScrollNextBounds(ScrollbarPane pane)
        {
            Rectangle bounds = GetScrollBarBounds(pane);
            int height = _scrollNextNormalTexture?.Height ?? ScrollButtonHeight;
            return new Rectangle(bounds.X, bounds.Bottom - height, _scrollNextNormalTexture?.Width ?? bounds.Width, height);
        }

        private Rectangle GetScrollTrackBounds(ScrollbarPane pane)
        {
            Rectangle bounds = GetScrollBarBounds(pane);
            Rectangle prevBounds = GetScrollPrevBounds(pane);
            Rectangle nextBounds = GetScrollNextBounds(pane);
            return new Rectangle(bounds.X, prevBounds.Bottom, bounds.Width, Math.Max(0, nextBounds.Y - prevBounds.Bottom));
        }

        private Rectangle GetScrollThumbBounds(ScrollbarPane pane)
        {
            Rectangle trackBounds = GetScrollTrackBounds(pane);
            int maxScroll = GetMaxScrollOffset(pane);
            int thumbWidth = _scrollThumbNormalTexture?.Width ?? trackBounds.Width;
            int thumbHeight = _scrollThumbNormalTexture?.Height ?? trackBounds.Height;
            if (maxScroll <= 0)
            {
                return new Rectangle(trackBounds.X, trackBounds.Y, thumbWidth, Math.Max(0, trackBounds.Height));
            }

            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int scrollOffset = GetScrollOffset(pane);
            int thumbY = trackBounds.Y + (travel == 0 ? 0 : (int)Math.Round((scrollOffset / (float)maxScroll) * travel));
            return new Rectangle(trackBounds.X, thumbY, thumbWidth, thumbHeight);
        }

        private int GetScrollOffset(ScrollbarPane pane)
        {
            return pane == ScrollbarPane.Storage ? _storageScrollOffset : _inventoryScrollOffset;
        }

        private void SetScrollOffset(ScrollbarPane pane, int value)
        {
            int clampedValue = Math.Clamp(value, 0, GetMaxScrollOffset(pane));
            if (pane == ScrollbarPane.Storage)
            {
                _storageScrollOffset = clampedValue;
            }
            else
            {
                _inventoryScrollOffset = clampedValue;
            }

            SyncCurrentTabState();
            UpdateButtonStates();
        }

        private void AdjustScrollOffset(ScrollbarPane pane, int delta)
        {
            SetScrollOffset(pane, GetScrollOffset(pane) + delta);
        }

        private void SetScrollOffsetFromThumb(ScrollbarPane pane, int mouseY)
        {
            Rectangle trackBounds = GetScrollTrackBounds(pane);
            Rectangle thumbBounds = GetScrollThumbBounds(pane);
            int maxScroll = GetMaxScrollOffset(pane);
            int travel = Math.Max(0, trackBounds.Height - thumbBounds.Height);
            if (travel <= 0 || maxScroll <= 0)
            {
                SetScrollOffset(pane, 0);
                return;
            }

            int thumbTop = Math.Clamp(mouseY - _scrollbarThumbDragOffsetY, trackBounds.Y, trackBounds.Bottom - thumbBounds.Height);
            float ratio = (thumbTop - trackBounds.Y) / (float)travel;
            SetScrollOffset(pane, (int)Math.Round(ratio * maxScroll));
        }

        private int GetMaxScrollOffset(ScrollbarPane pane)
        {
            return pane == ScrollbarPane.Storage
                ? Math.Max(0, GetStorageRows(GetInventoryTypeFromTab(_currentTab)).Count - MaxVisibleRows)
                : Math.Max(0, (_inventory?.GetSlots(GetInventoryTypeFromTab(_currentTab)).Count ?? 0) - MaxVisibleRows);
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

            string amountText = BuildVisibleEntryValue();
            string prompt = IsMesoTransferMode(_mesoEntryMode)
                ? $"{_mesoEntryPrompt}: {amountText} / {_mesoEntryMaxValue.ToString("N0", CultureInfo.InvariantCulture)}"
                : $"{_mesoEntryPrompt}: {amountText}";
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
                existing.TooltipPart ??= slotData.TooltipPart?.Clone();
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
                    _statusMessage = IsMesoTransferMode(_mesoEntryMode)
                        ? "Meso transfer cancelled."
                        : "Trunk security entry cancelled.";
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
                    ClearCompositionText();
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
            _secondaryPasswordConfirmationText = string.Empty;
            _softKeyboardActive = false;
            ClearCompositionText();
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
            return (_storageRuntime?.CanCurrentCharacterAccess ?? true) &&
                   (_storageRuntime?.IsClientAccountAuthorityVerified ?? true) &&
                   (_storageRuntime?.IsSecondaryPasswordVerified ?? true);
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

            if (_storageRuntime != null && !_storageRuntime.CanCurrentCharacterAccess)
            {
                _statusMessage = BuildAccessDeniedMessage();
                return;
            }

            if (_storageRuntime != null && !_storageRuntime.IsClientAccountAuthorityVerified)
            {
                if (_storageRuntime.HasAccountPic && !_storageRuntime.IsAccountPicVerified)
                {
                    _statusMessage = "Enter the simulator account PIC to unlock this trunk session.";
                    return;
                }

                if (_storageRuntime.HasAccountSecondaryPassword && !_storageRuntime.IsAccountSecondaryPasswordVerified)
                {
                    _statusMessage = "Enter the simulator account secondary password to unlock this trunk session.";
                    return;
                }
            }

            if (_storageRuntime != null && !_storageRuntime.IsSecondaryPasswordVerified)
            {
                _statusMessage = _storageRuntime.HasSecondaryPassword
                    ? "Enter the storage passcode to unlock this trunk session."
                    : "Create a storage passcode to secure this shared trunk.";
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

        private bool EnsureStorageAccess()
        {
            if (_storageRuntime == null)
            {
                return true;
            }

            if (!_storageRuntime.CanCurrentCharacterAccess)
            {
                _statusMessage = BuildAccessDeniedMessage();
                return false;
            }

            if (_storageRuntime.IsSecondaryPasswordVerified)
            {
                return true;
            }

            BeginSecondaryPasswordEntry();
            return false;
        }

        private bool TryRequestExternalAccountSecurityPrompt()
        {
            if (_storageRuntime == null || !_storageRuntime.CanCurrentCharacterAccess)
            {
                return false;
            }

            if (_storageRuntime.HasAccountPic &&
                !_storageRuntime.IsAccountPicVerified &&
                AccountSecurityPromptRequested?.Invoke(TrunkAccountSecurityPromptKind.VerifyPic) == true)
            {
                _statusMessage = "Waiting for simulator account PIC verification.";
                return true;
            }

            if (_storageRuntime.HasAccountSecondaryPassword &&
                !_storageRuntime.IsAccountSecondaryPasswordVerified &&
                AccountSecurityPromptRequested?.Invoke(TrunkAccountSecurityPromptKind.VerifySecondaryPassword) == true)
            {
                _statusMessage = "Waiting for simulator account secondary-password verification.";
                return true;
            }

            return false;
        }

        private void BeginSecondaryPasswordEntry()
        {
            if (_storageRuntime == null || !_storageRuntime.CanCurrentCharacterAccess)
            {
                _statusMessage = BuildAccessDeniedMessage();
                return;
            }

            if (_storageRuntime.HasAccountPic && !_storageRuntime.IsAccountPicVerified)
            {
                if (TryRequestExternalAccountSecurityPrompt())
                {
                    CancelMesoEntry();
                    UpdateButtonStates();
                    return;
                }

                BeginMesoEntry(MesoEntryMode.VerifyAccountPic);
                _statusMessage = "Storage access is account-locked. Enter the configured PIC first.";
                return;
            }

            if (_storageRuntime.HasAccountSecondaryPassword && !_storageRuntime.IsAccountSecondaryPasswordVerified)
            {
                if (TryRequestExternalAccountSecurityPrompt())
                {
                    CancelMesoEntry();
                    UpdateButtonStates();
                    return;
                }

                BeginMesoEntry(MesoEntryMode.VerifyAccountSecondaryPassword);
                _statusMessage = "Storage access is account-locked. Enter the account secondary password.";
                return;
            }

            BeginMesoEntry(_storageRuntime.HasSecondaryPassword
                ? MesoEntryMode.VerifySecondaryPassword
                : MesoEntryMode.SetupSecondaryPassword);
            _statusMessage = _storageRuntime.HasSecondaryPassword
                ? "Storage access is locked. Enter the 4-8 digit passcode."
                : "Set a 4-8 digit storage passcode for this shared trunk.";
        }

        private void ConfirmSecurityEntry()
        {
            string password = _mesoEntryText == "0" ? string.Empty : _mesoEntryText;

            if (_storageRuntime == null)
            {
                CancelMesoEntry();
                UpdateButtonStates();
                return;
            }

            if (_mesoEntryMode == MesoEntryMode.VerifyAccountPic)
            {
                _statusMessage = _storageRuntime.TryVerifyAccountPic(password)
                    ? "Simulator account PIC accepted."
                    : "Simulator account PIC rejected.";
                if (_storageRuntime.IsAccountPicVerified)
                {
                    CancelMesoEntry();
                    if ((_storageRuntime.HasAccountSecondaryPassword && !_storageRuntime.IsAccountSecondaryPasswordVerified) ||
                        (_storageRuntime.HasSecondaryPassword && !_storageRuntime.IsSecondaryPasswordVerified))
                    {
                        BeginSecondaryPasswordEntry();
                    }
                }
                else
                {
                    _mesoEntryText = string.Empty;
                    _mesoEntryReplaceOnDigit = true;
                }

                UpdateButtonStates();
                return;
            }

            if (_mesoEntryMode == MesoEntryMode.VerifyAccountSecondaryPassword)
            {
                _statusMessage = _storageRuntime.TryVerifyAccountSecondaryPassword(password)
                    ? "Simulator account secondary password accepted."
                    : "Simulator account secondary password rejected.";
                if (_storageRuntime.IsAccountSecondaryPasswordVerified)
                {
                    CancelMesoEntry();
                    if (_storageRuntime.HasSecondaryPassword && !_storageRuntime.IsSecondaryPasswordVerified)
                    {
                        BeginSecondaryPasswordEntry();
                    }
                }
                else
                {
                    _mesoEntryText = string.Empty;
                    _mesoEntryReplaceOnDigit = true;
                }

                UpdateButtonStates();
                return;
            }

            if (password.Length < MinSecondaryPasswordDigits || password.Length > MaxSecondaryPasswordDigits)
            {
                _statusMessage = $"Storage passcodes must be {MinSecondaryPasswordDigits}-{MaxSecondaryPasswordDigits} digits.";
                UpdateButtonStates();
                return;
            }

            if (_mesoEntryMode == MesoEntryMode.SetupSecondaryPassword)
            {
                _secondaryPasswordConfirmationText = password;
                _mesoEntryMode = MesoEntryMode.ConfirmSecondaryPassword;
                _mesoEntryText = "0";
                _mesoEntryPrompt = "Confirm storage passcode";
                _mesoEntryReplaceOnDigit = true;
                _statusMessage = "Re-enter the storage passcode to confirm it.";
                UpdateButtonStates();
                return;
            }

            if (_mesoEntryMode == MesoEntryMode.ConfirmSecondaryPassword)
            {
                if (!string.Equals(_secondaryPasswordConfirmationText, password, StringComparison.Ordinal))
                {
                    _secondaryPasswordConfirmationText = string.Empty;
                    _mesoEntryMode = MesoEntryMode.SetupSecondaryPassword;
                    _mesoEntryText = "0";
                    _mesoEntryPrompt = "Create storage passcode";
                    _mesoEntryReplaceOnDigit = true;
                    _statusMessage = "Storage passcode confirmation did not match. Try again.";
                    UpdateButtonStates();
                    return;
                }

                _statusMessage = _storageRuntime.TrySetSecondaryPassword(password)
                    ? "Storage passcode saved. Trunk access is now unlocked for this session."
                    : "Unable to save the storage passcode.";
                if (_storageRuntime.IsSecondaryPasswordVerified)
                {
                    CancelMesoEntry();
                }

                UpdateButtonStates();
                return;
            }

            _statusMessage = _storageRuntime.TryVerifySecondaryPassword(password)
                ? "Storage passcode accepted."
                : "Storage passcode rejected.";
            if (_storageRuntime.IsSecondaryPasswordVerified)
            {
                CancelMesoEntry();
            }
            else
            {
                _mesoEntryText = "0";
                _mesoEntryReplaceOnDigit = true;
            }

            UpdateButtonStates();
        }

        private static bool IsMesoTransferMode(MesoEntryMode mode)
        {
            return mode is MesoEntryMode.Withdraw or MesoEntryMode.Deposit;
        }

        private static bool IsNumericEntryMode(MesoEntryMode mode)
        {
            return mode is MesoEntryMode.Withdraw
                or MesoEntryMode.Deposit
                or MesoEntryMode.VerifyAccountPic
                or MesoEntryMode.SetupSecondaryPassword
                or MesoEntryMode.ConfirmSecondaryPassword
                or MesoEntryMode.VerifySecondaryPassword;
        }

        private static int GetEntryMaxLength(MesoEntryMode mode)
        {
            return mode switch
            {
                MesoEntryMode.Withdraw or MesoEntryMode.Deposit => MaxMesoDigits,
                MesoEntryMode.VerifyAccountPic => ClientPicEditMaxLength,
                MesoEntryMode.SetupSecondaryPassword or MesoEntryMode.ConfirmSecondaryPassword or MesoEntryMode.VerifySecondaryPassword => MaxSecondaryPasswordDigits,
                MesoEntryMode.VerifyAccountSecondaryPassword => MaxAccountSecurityLength,
                _ => MaxMesoDigits
            };
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

        public override void HandleCommittedText(string text)
        {
            if (!CapturesKeyboardInput || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            foreach (char character in text)
            {
                TryAcceptEntryCharacter(character, out _);
            }
        }

        public override void HandleCompositionText(string text)
        {
            HandleCompositionState(new ImeCompositionState(text ?? string.Empty, Array.Empty<int>(), -1));
        }

        public override void HandleCompositionState(ImeCompositionState state)
        {
            if (!CapturesKeyboardInput)
            {
                ClearCompositionText();
                return;
            }

            string sanitized = SanitizeCompositionText(state?.Text);
            _compositionText = sanitized;
            if (sanitized.Length == 0)
            {
                ClearImeCandidateList();
            }
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            ClearImeCandidateList();
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = CapturesKeyboardInput && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => GetEntryBounds();

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            if (TryAcceptEntryCharacter(character, out errorMessage))
            {
                ClearCompositionText();
                return true;
            }

            return false;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            string currentValue = GetEffectiveEntryValue();
            if (!SoftKeyboardUI.CanBackspace(currentValue.Length))
            {
                errorMessage = "Nothing to replace.";
                return false;
            }

            string previousValue = _mesoEntryText;
            bool previousReplaceOnDigit = _mesoEntryReplaceOnDigit;
            _mesoEntryText = previousReplaceOnDigit && previousValue == "0"
                ? string.Empty
                : previousValue[..^1];
            _mesoEntryReplaceOnDigit = false;
            if (TryAcceptEntryCharacter(character, out errorMessage))
            {
                ClearCompositionText();
                return true;
            }

            _mesoEntryText = previousValue;
            _mesoEntryReplaceOnDigit = previousReplaceOnDigit;
            return false;
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!SoftKeyboardUI.CanBackspace(GetEffectiveEntryValue().Length))
            {
                errorMessage = "Nothing to remove.";
                return false;
            }

            _mesoEntryText = _mesoEntryReplaceOnDigit ? string.Empty : _mesoEntryText[..^1];
            _mesoEntryReplaceOnDigit = false;
            ClearCompositionText();
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_mesoEntryMode == MesoEntryMode.None)
            {
                errorMessage = "This trunk prompt is not active.";
                return false;
            }

            ConfirmMesoEntry();
            return true;
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
        }

        void ISoftKeyboardHost.SetSoftKeyboardCompositionText(string text)
        {
            HandleCompositionText(text);
        }

        private static SoftKeyboardKeyboardType GetSoftKeyboardType(MesoEntryMode mode)
        {
            return mode switch
            {
                MesoEntryMode.VerifyAccountPic => SoftKeyboardKeyboardType.NumericOnlyAlt,
                MesoEntryMode.VerifyAccountSecondaryPassword => SoftKeyboardKeyboardType.AlphaNumeric,
                MesoEntryMode.Withdraw or
                MesoEntryMode.Deposit or
                MesoEntryMode.SetupSecondaryPassword or
                MesoEntryMode.ConfirmSecondaryPassword or
                MesoEntryMode.VerifySecondaryPassword => SoftKeyboardKeyboardType.NumericOnly,
                _ => SoftKeyboardKeyboardType.AlphaNumeric
            };
        }

        private bool TryAcceptEntryCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (_mesoEntryMode == MesoEntryMode.None)
            {
                errorMessage = "This trunk prompt is not active.";
                return false;
            }

            if (!CanAcceptEntryCharacter(character))
            {
                errorMessage = "That key is disabled for this trunk prompt.";
                return false;
            }

            string source = _mesoEntryReplaceOnDigit ? character.ToString() : _mesoEntryText + character;
            if (IsNumericEntryMode(_mesoEntryMode))
            {
                string rawValue = source.TrimStart('0');
                string candidate = string.IsNullOrEmpty(rawValue) ? "0" : rawValue;
                if (candidate.Length > GetEntryMaxLength(_mesoEntryMode))
                {
                    errorMessage = "The input field is full.";
                    return false;
                }

                if (!long.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed) ||
                    parsed > int.MaxValue)
                {
                    errorMessage = "That value is out of range.";
                    return false;
                }

                _mesoEntryText = candidate;
                _mesoEntryReplaceOnDigit = false;
                return true;
            }

            if (source.Length > GetEntryMaxLength(_mesoEntryMode))
            {
                errorMessage = "The input field is full.";
                return false;
            }

            _mesoEntryText = source;
            _mesoEntryReplaceOnDigit = false;
            return true;
        }

        private bool CanAcceptEntryCharacter(char character)
        {
            if (_mesoEntryMode == MesoEntryMode.None)
            {
                return false;
            }

            return SoftKeyboardUI.CanAcceptCharacter(
                GetSoftKeyboardType(_mesoEntryMode),
                GetEffectiveEntryValue().Length,
                GetEntryMaxLength(_mesoEntryMode),
                character);
        }

        private string GetEffectiveEntryValue()
        {
            if (_mesoEntryMode == MesoEntryMode.None)
            {
                return string.Empty;
            }

            return _mesoEntryReplaceOnDigit && _mesoEntryText == "0"
                ? string.Empty
                : _mesoEntryText ?? string.Empty;
        }

        private string BuildVisibleEntryValue()
        {
            string value = GetEffectiveEntryValue();
            if (_mesoEntryMode == MesoEntryMode.None)
            {
                return string.Empty;
            }

            if (IsMesoTransferMode(_mesoEntryMode))
            {
                string visible = string.IsNullOrEmpty(value) ? "0" : value;
                return string.IsNullOrEmpty(_compositionText) ? visible : visible + _compositionText;
            }

            return new string('*', value.Length + _compositionText.Length);
        }

        private string SanitizeCompositionText(string text)
        {
            if (string.IsNullOrEmpty(text) || _mesoEntryMode == MesoEntryMode.None)
            {
                return string.Empty;
            }

            List<char> accepted = new(text.Length);
            int textLength = GetEffectiveEntryValue().Length;
            foreach (char character in text)
            {
                if (!SoftKeyboardUI.CanAcceptCharacter(
                        GetSoftKeyboardType(_mesoEntryMode),
                        textLength + accepted.Count,
                        GetEntryMaxLength(_mesoEntryMode),
                        character))
                {
                    continue;
                }

                if (IsNumericEntryMode(_mesoEntryMode) && !char.IsDigit(character))
                {
                    continue;
                }

                accepted.Add(character);
            }

            return new string(accepted.ToArray());
        }

        private Rectangle GetEntryBounds()
        {
            int lineHeight = Math.Max(_font?.LineSpacing ?? 16, 16);
            return new Rectangle(
                Position.X + StatusTextX,
                Position.Y + MesoPromptTextY - 1,
                StatusTextWidth,
                lineHeight + 4);
        }

        private void DrawImeCandidateWindow(SpriteBatch sprite)
        {
            if (_font == null || !_candidateListState.HasCandidates || _debugTooltipTexture == null)
            {
                return;
            }

            Rectangle bounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            sprite.Draw(_debugTooltipTexture, bounds, new Color(33, 33, 41, 235));
            sprite.Draw(_debugTooltipTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_debugTooltipTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(214, 214, 214, 220));
            sprite.Draw(_debugTooltipTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));
            sprite.Draw(_debugTooltipTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(214, 214, 214, 220));

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            int rowHeight = Math.Max(_font.LineSpacing + 1, 16);
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = start + i;
                string numberText = $"{i + 1}.";
                Rectangle rowBounds = new(bounds.X + 2, bounds.Y + 2 + (i * rowHeight), bounds.Width - 4, rowHeight);
                bool selected = candidateIndex == _candidateListState.Selection;
                if (selected)
                {
                    sprite.Draw(_debugTooltipTexture, rowBounds, new Color(89, 108, 147, 220));
                }

                ClientTextDrawing.Draw(sprite, numberText, new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222), 1.0f, _font);
                ClientTextDrawing.Draw(
                    sprite,
                    _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                    new Vector2(rowBounds.X + 8 + (int)Math.Ceiling(ClientTextDrawing.Measure((GraphicsDevice)null, $"{count}.", 1.0f, _font).X), rowBounds.Y),
                    selected ? Color.White : new Color(240, 235, 200),
                    1.0f,
                    _font);
            }
        }

        private Rectangle GetImeCandidateWindowBounds(Viewport viewport)
        {
            if (ImeCandidateWindowRendering.ShouldPreferNativeWindow(_candidateListState))
            {
                return Rectangle.Empty;
            }

            int visibleCount = GetVisibleCandidateCount();
            if (visibleCount <= 0 || _font == null)
            {
                return Rectangle.Empty;
            }

            Rectangle entryBounds = GetEntryBounds();
            int widestEntryWidth = 0;
            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = Math.Clamp(_candidateListState.PageStart + i, 0, _candidateListState.Candidates.Count - 1);
                string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                int entryWidth = (int)Math.Ceiling(
                    ClientTextDrawing.Measure((GraphicsDevice)null, $"{i + 1}.", 1.0f, _font).X +
                    ClientTextDrawing.Measure((GraphicsDevice)null, candidateText, 1.0f, _font).X) + 16;
                widestEntryWidth = Math.Max(widestEntryWidth, entryWidth);
            }

            int width = Math.Max(96, widestEntryWidth + 14);
            int height = (visibleCount * Math.Max(_font.LineSpacing + 1, 16)) + 4;
            int x = Math.Clamp(entryBounds.X, 0, Math.Max(0, viewport.Width - width));
            int y = entryBounds.Bottom + 2;
            if (y + height > viewport.Height)
            {
                y = Math.Max(0, entryBounds.Y - height - 2);
            }

            return new Rectangle(x, y, width, height);
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int pageSize = _candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count;
            return Math.Max(0, Math.Min(pageSize, _candidateListState.Candidates.Count - start));
        }
    }
}
