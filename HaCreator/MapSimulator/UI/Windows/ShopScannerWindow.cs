using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaCreator.MapSimulator.Interaction;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.ItemStructure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    public sealed class ShopScannerWindow : UIWindowBase, ISoftKeyboardHost
    {
        public sealed class ScannerResult
        {
            public int ItemId { get; init; }
            public string Name { get; init; } = string.Empty;
            public string NoSpaceName { get; init; } = string.Empty;
            public InventoryType InventoryType { get; init; } = InventoryType.NONE;
            public int ClientListOrder { get; init; }
            public bool IsBlockedByScanBlock { get; init; }
            public bool IsScannerItem { get; init; }
            public string ShopOwnerName { get; init; } = string.Empty;
            public string ShopTitle { get; init; } = string.Empty;
            public int FieldId { get; init; }
            public int MiniRoomSn { get; init; }
            public int ChannelId { get; init; } = -1;
            public int Quantity { get; init; }
            public int BundleCount { get; init; }
            public int Price { get; init; }
            public bool IsNpcShop { get; init; }
            public bool IsPacketFedShopRow { get; init; }
        }

        internal sealed class ScannerPacketShopRow
        {
            public string ShopOwnerName { get; init; } = string.Empty;
            public string ShopTitle { get; init; } = string.Empty;
            public int FieldId { get; init; }
            public int MiniRoomSn { get; init; }
            public int ChannelId { get; init; } = -1;
            public int Quantity { get; init; }
            public int BundleCount { get; init; }
            public int Price { get; init; }
            public int InventoryTypeCode { get; init; }
            public bool HasItemSlotPayload { get; init; }
            public int ItemSlotPayloadLength { get; init; }
            public int DecodedItemSlotType { get; init; }
            public int DecodedItemSlotItemId { get; init; }
            public bool IsNpcShop { get; init; }
        }

        internal sealed class ScannerPacketResult
        {
            public int Subtype { get; init; }
            public int NpcShopPrice { get; init; }
            public int ItemId { get; init; }
            public int ServerRowCount { get; init; }
            public IReadOnlyList<ScannerPacketShopRow> ShopRows { get; init; } = Array.Empty<ScannerPacketShopRow>();
            public IReadOnlyList<int> ItemIds { get; init; } = Array.Empty<int>();
            public bool HasNoResultNotice { get; init; }
            public int TrailingByteCount { get; init; }
        }

        private sealed class ScannerIndexEntry
        {
            public int ItemId { get; init; }
            public string Name { get; init; } = string.Empty;
            public string NoSpaceName { get; init; } = string.Empty;
            public InventoryType InventoryType { get; init; } = InventoryType.NONE;
            public int ClientListOrder { get; init; }
            public bool IsBlockedByScanBlock { get; init; }
            public bool IsScannerItem { get; init; }
        }

        private const int SearchMaxLength = 40;
        private const int SearchTextX = 23;
        private const int SearchTextY = 79;
        private const int SearchTextWidth = 130;
        private const int SearchFieldHeight = 16;
        private const int ListX = 14;
        private const int ListY = 108;
        private const int ListWidth = 190;
        private const int RowHeight = 20;
        private const int VisibleRows = 6;
        private const int FooterY = 221;
        internal const int InitialRequestOpcode = 72;
        internal const int ShopLinkRequestOpcode = 73;
        internal const int InitialRequestSubtype = 5;
        internal const int ScannerResultPacketType = 73;
        internal const int ShopLinkResultPacketType = 74;
        internal const int ScannerResultShopRowsSubtype = 6;
        internal const int ScannerResultItemIdListSubtype = 7;
        internal const int ShopResultRowFirstButtonId = 3000;
        internal const int ShopResultRowsPerPage = 8;
        internal const int ChildCloseButtonId = 1000;
        internal const int ChildPreviousButtonId = 1001;
        internal const int ChildNextButtonId = 1002;
        internal const int ChildOkButtonId = 1003;
        internal const int SearchResultDescendingCheckBoxId = 1004;
        internal const int ShopResultOkButtonId = 1;
        internal const int ShopResultCancelButtonId = 2;
        internal const int ShopResultCloseButtonId = 8;
        internal const int ShopResultNextButtonId = 4000;
        internal const int ShopResultPreviousButtonId = 4001;
        internal const int SearchResultPageSize = 10;
        internal const int ScanRequestThrottleMilliseconds = 500;
        internal const int ScanConfirmationFormatStringPoolId = 0x0E55;
        internal const int ScanConfirmationAscendingStringPoolId = 0x0E56;
        internal const int ScanConfirmationDescendingStringPoolId = 0x0E57;
        internal const int SearchResultChildCheckBoxX = 35;
        internal const int SearchResultChildCheckBoxY = 10;
        internal const int SearchResultChildCheckBoxWidth = 105;
        internal const int SearchResultChildCheckBoxHeight = 11;
        internal const int ShopLinkSuccessResultCode = 0;
        internal const int ShopLinkNoShopResultCode = 1;
        internal const int ShopLinkAlreadyMovedResultCode = 2;
        internal const int ShopLinkUnknownItemResultCode = 3;
        internal const int ShopLinkCannotEnterResultCode = 4;
        internal const int ShopLinkNoMesoResultCode = 7;
        internal const int ShopLinkNoShopChannelResultCode = 17;
        internal const int ShopLinkExpiredResultCode = 18;
        internal const int ShopLinkTooFarResultCode = 23;
        private static readonly object ScannerIndexLock = new();
        private static IReadOnlyList<ScannerIndexEntry> _scannerIndex;

        private readonly Texture2D _searchBackgroundTexture;
        private readonly Texture2D _resultBackgroundTexture;
        private readonly Texture2D _iconPlaceholderTexture;
        private readonly Texture2D _pixelTexture;
        private readonly UIObject _top10Button;
        private readonly UIObject _categoryButton;
        private readonly UIObject _searchButton;
        private readonly UIObject _closeButton;
        private readonly List<UIObject> _rowButtons = new();
        private readonly GraphicsDevice _device;
        private readonly bool _searchResultChildAssetAvailable;
        private readonly int _searchResultChildAssetWidth;
        private readonly int _searchResultChildAssetHeight;

        private SpriteFont _font;
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private string _searchQuery = string.Empty;
        private string _compositionText = string.Empty;
        private List<ScannerResult> _results = new();
        private int _selectedIndex = -1;
        private int _scrollOffset;
        private bool _searchFieldFocused;
        private bool _softKeyboardActive;
        private string _statusMessage = "CUIShopScanner::OnCreate waits on the scanner item-name feed.";
        private int _initialRequestCount;
        private ScannerAddOnMode _activeAddOnMode;
        private int _searchResultPageIndex;
        private int _searchResultTotalPages;
        private int _lastRequestedScanItemId;
        private int _lastRequestedMiniRoomSn;
        private int _lastRequestedFieldId;
        private int _lastRequestedScanTick;
        private bool _lastRequestedScanDescending;
        private bool _hotListChildCreated;
        private bool _categoryChildCreated;
        private bool _searchResultChildCreated;
        private bool _hotListChildShown;
        private bool _categoryChildShown;
        private bool _searchResultChildShown;
        private bool _exclusiveScannerRequestPending;
        private bool _descendingOrderChecked;
        private bool _scanConfirmationPending;
        private int _pendingScanConfirmationItemId;
        private string _pendingScanConfirmationSummary = string.Empty;
        private int? _lastShopLinkResultCode;
        private string _lastShopLinkResultSummary = "No scanner shop-link result packet has been applied.";
        private int _lastScannerResultSubtype;
        private int _lastScannerResultItemId;
        private int _lastScannerResultPacketRowCount;
        private int _lastScannerResultTrailingByteCount;
        private string _lastScannerResultSummary = "No packet-fed shop-scanner result has been applied.";

        public ShopScannerWindow(
            IDXObject frame,
            Texture2D searchBackgroundTexture,
            Texture2D resultBackgroundTexture,
            Texture2D iconPlaceholderTexture,
            UIObject top10Button,
            UIObject categoryButton,
            UIObject searchButton,
            UIObject closeButton,
            GraphicsDevice device,
            bool searchResultChildAssetAvailable = false,
            int searchResultChildAssetWidth = 0,
            int searchResultChildAssetHeight = 0)
            : base(frame)
        {
            _searchBackgroundTexture = searchBackgroundTexture;
            _resultBackgroundTexture = resultBackgroundTexture;
            _iconPlaceholderTexture = iconPlaceholderTexture;
            _top10Button = top10Button;
            _categoryButton = categoryButton;
            _searchButton = searchButton;
            _closeButton = closeButton;
            _device = device;
            _searchResultChildAssetAvailable = searchResultChildAssetAvailable;
            _searchResultChildAssetWidth = searchResultChildAssetWidth;
            _searchResultChildAssetHeight = searchResultChildAssetHeight;

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            RegisterButton(_top10Button, 13, 51, ToggleHotList);
            RegisterButton(_categoryButton, 75, 51, ToggleCategory);
            RegisterButton(_searchButton, 160, 78, ExecuteSearch);
            RegisterButton(_closeButton, 196, 6, Hide);

            for (int i = 0; i < VisibleRows; i++)
            {
                UIObject rowButton = CreateTransparentButton(device, ListWidth, RowHeight);
                rowButton.X = ListX;
                rowButton.Y = ListY + (i * RowHeight);
                int capturedRow = i;
                rowButton.ButtonClickReleased += _ => SelectVisibleRow(capturedRow);
                AddButton(rowButton);
                _rowButtons.Add(rowButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.ShopScanner;
        public override bool CapturesKeyboardInput => IsVisible && _searchFieldFocused;
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _searchFieldFocused && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.AlphaNumeric;
        int ISoftKeyboardHost.SoftKeyboardTextLength => _searchQuery?.Length ?? 0;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => SearchMaxLength;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => !string.IsNullOrWhiteSpace(_searchQuery);
        string ISoftKeyboardHost.GetSoftKeyboardText() => _searchQuery ?? string.Empty;

        public int InitialRequestCount => _initialRequestCount;
        public string LastInitialRequestSummary { get; private set; } = string.Empty;
        public Func<int, IReadOnlyList<byte>, string> InitialScannerRequestDispatcher { get; set; }
        public Func<int, IReadOnlyList<byte>, string> ScanItemRequestDispatcher { get; set; }
        public Func<int, IReadOnlyList<byte>, string> ShopLinkRequestDispatcher { get; set; }

        private enum ScannerAddOnMode
        {
            None,
            HotList,
            Category,
            SearchResult
        }

        public sealed class ScannerChildOwnerSnapshot
        {
            public string ActiveOwner { get; init; } = "none";
            public int ResultCount { get; init; }
            public int SelectedIndex { get; init; }
            public int SelectedItemId { get; init; }
            public int PageIndex { get; init; }
            public int TotalPages { get; init; }
            public int LastRequestedScanItemId { get; init; }
            public int LastRequestedMiniRoomSn { get; init; }
            public int LastRequestedFieldId { get; init; }
            public int LastRequestedScanTick { get; init; }
            public bool LastRequestedScanDescending { get; init; }
            public bool HotListChildCreated { get; init; }
            public bool CategoryChildCreated { get; init; }
            public bool SearchResultChildCreated { get; init; }
            public bool HotListChildShown { get; init; }
            public bool CategoryChildShown { get; init; }
            public bool SearchResultChildShown { get; init; }
            public bool ExclusiveScannerRequestPending { get; init; }
            public bool DescendingOrderChecked { get; init; }
            public bool SearchResultChildAssetAvailable { get; init; }
            public int SearchResultChildAssetWidth { get; init; }
            public int SearchResultChildAssetHeight { get; init; }
            public int SearchResultChildRowsPerPage { get; init; }
            public bool ChildPreviousButtonEnabled { get; init; }
            public bool ChildNextButtonEnabled { get; init; }
            public IReadOnlyList<int> PreviousPageItemIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> CurrentPageItemIds { get; init; } = Array.Empty<int>();
            public IReadOnlyList<int> NextPageItemIds { get; init; } = Array.Empty<int>();
            public int DescendingCheckBoxX { get; init; }
            public int DescendingCheckBoxY { get; init; }
            public int DescendingCheckBoxWidth { get; init; }
            public int DescendingCheckBoxHeight { get; init; }
            public bool ScanConfirmationPending { get; init; }
            public int PendingScanConfirmationItemId { get; init; }
            public string PendingScanConfirmationSummary { get; init; } = string.Empty;
            public int? LastShopLinkResultCode { get; init; }
            public string LastShopLinkResultSummary { get; init; } = string.Empty;
            public int LastScannerResultSubtype { get; init; }
            public int LastScannerResultItemId { get; init; }
            public int LastScannerResultPacketRowCount { get; init; }
            public int LastScannerResultTrailingByteCount { get; init; }
            public string LastScannerResultSummary { get; init; } = string.Empty;
        }

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            _searchFieldFocused = true;
            _softKeyboardActive = false;
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            if (!wasVisible)
            {
                DispatchInitialScannerRequest();
            }

            RefreshRows();
        }

        public override void Hide()
        {
            base.Hide();
            _searchFieldFocused = false;
            _softKeyboardActive = false;
            ClearCompositionText();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        public override void HandleCommittedText(string text)
        {
            if (!CapturesKeyboardInput || string.IsNullOrEmpty(text))
            {
                return;
            }

            for (int i = 0; i < text.Length; i++)
            {
                TryAppendSearchCharacter(text[i]);
            }
        }

        public override void HandleCompositionText(string text)
        {
            _compositionText = CapturesKeyboardInput ? text ?? string.Empty : string.Empty;
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            HandleKeyboardInput(keyboardState);

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (wheelDelta != 0 && GetWindowBounds().Contains(mouseState.Position))
            {
                MoveSelection(wheelDelta > 0 ? -1 : 1);
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (leftJustPressed)
            {
                _searchFieldFocused = GetSearchFieldBounds().Contains(mouseState.Position);
                _softKeyboardActive = _searchFieldFocused;
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
            RefreshRows();
        }

        public IReadOnlyList<ScannerResult> Search(string query, int maxResults = 100)
        {
            string collapsedQuery = NormalizeScannerQuery(query);
            if (string.IsNullOrWhiteSpace(collapsedQuery))
            {
                return Array.Empty<ScannerResult>();
            }

            return GetScannerIndex()
                .Where(entry => !entry.IsBlockedByScanBlock && !entry.IsScannerItem)
                .Where(entry => entry.NoSpaceName.Contains(collapsedQuery, StringComparison.Ordinal))
                .OrderBy(entry => entry.ClientListOrder)
                .Take(Math.Max(1, maxResults))
                .Select(ToScannerResult)
                .ToList();
        }

        public string GetStatusSummary()
        {
            return $"{LastInitialRequestSummary} Index rows: {GetScannerIndex().Count.ToString(CultureInfo.InvariantCulture)}.";
        }

        public ScannerChildOwnerSnapshot GetChildOwnerSnapshot()
        {
            ScannerResult selected = _selectedIndex >= 0 && _selectedIndex < _results.Count
                ? _results[_selectedIndex]
                : null;
            return new ScannerChildOwnerSnapshot
            {
                ActiveOwner = _activeAddOnMode.ToString(),
                ResultCount = _results.Count,
                SelectedIndex = _selectedIndex,
                SelectedItemId = selected?.ItemId ?? 0,
                PageIndex = _searchResultPageIndex,
                TotalPages = _searchResultTotalPages,
                LastRequestedScanItemId = _lastRequestedScanItemId,
                LastRequestedMiniRoomSn = _lastRequestedMiniRoomSn,
                LastRequestedFieldId = _lastRequestedFieldId,
                LastRequestedScanTick = _lastRequestedScanTick,
                LastRequestedScanDescending = _lastRequestedScanDescending,
                HotListChildCreated = _hotListChildCreated,
                CategoryChildCreated = _categoryChildCreated,
                SearchResultChildCreated = _searchResultChildCreated,
                HotListChildShown = _hotListChildShown,
                CategoryChildShown = _categoryChildShown,
                SearchResultChildShown = _searchResultChildShown,
                ExclusiveScannerRequestPending = _exclusiveScannerRequestPending,
                DescendingOrderChecked = _descendingOrderChecked,
                SearchResultChildAssetAvailable = _searchResultChildAssetAvailable,
                SearchResultChildAssetWidth = _searchResultChildAssetWidth,
                SearchResultChildAssetHeight = _searchResultChildAssetHeight,
                SearchResultChildRowsPerPage = GetCurrentChildPageSize(),
                ChildPreviousButtonEnabled = IsChildPreviousButtonEnabled(),
                ChildNextButtonEnabled = IsChildNextButtonEnabled(),
                PreviousPageItemIds = GetChildPageItemIds(_searchResultPageIndex - 1),
                CurrentPageItemIds = GetChildPageItemIds(_searchResultPageIndex),
                NextPageItemIds = GetChildPageItemIds(_searchResultPageIndex + 1),
                DescendingCheckBoxX = SearchResultChildCheckBoxX,
                DescendingCheckBoxY = SearchResultChildCheckBoxY,
                DescendingCheckBoxWidth = SearchResultChildCheckBoxWidth,
                DescendingCheckBoxHeight = SearchResultChildCheckBoxHeight,
                ScanConfirmationPending = _scanConfirmationPending,
                PendingScanConfirmationItemId = _pendingScanConfirmationItemId,
                PendingScanConfirmationSummary = _pendingScanConfirmationSummary,
                LastShopLinkResultCode = _lastShopLinkResultCode,
                LastShopLinkResultSummary = _lastShopLinkResultSummary,
                LastScannerResultSubtype = _lastScannerResultSubtype,
                LastScannerResultItemId = _lastScannerResultItemId,
                LastScannerResultPacketRowCount = _lastScannerResultPacketRowCount,
                LastScannerResultTrailingByteCount = _lastScannerResultTrailingByteCount,
                LastScannerResultSummary = _lastScannerResultSummary
            };
        }

        public bool ApplyScannerResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (!TryDecodeScannerResultPayload(payload, out ScannerPacketResult packetResult, out string decodeError))
            {
                message = decodeError;
                return false;
            }

            _lastScannerResultSubtype = packetResult.Subtype;
            _lastScannerResultItemId = packetResult.ItemId;
            _lastScannerResultPacketRowCount = packetResult.ShopRows.Count;
            _lastScannerResultTrailingByteCount = packetResult.TrailingByteCount;

            if (packetResult.Subtype == ScannerResultShopRowsSubtype)
            {
                if (packetResult.HasNoResultNotice)
                {
                    _exclusiveScannerRequestPending = false;
                    ClearPendingScanConfirmation();
                    _results.Clear();
                    _selectedIndex = -1;
                    SetActiveAddOnMode(ScannerAddOnMode.SearchResult);
                    ResetSearchResultPaging();
                    _lastScannerResultSummary = "CWvsContext::OnShopScannerResult subtype 6 decoded zero shop rows and zero NPC-shop price; client would show the no-result notice and clear the exclusive request latch.";
                    _statusMessage = _lastScannerResultSummary;
                    message = _lastScannerResultSummary;
                    RefreshRows();
                    return true;
                }

                string itemName = ResolveScannerItemName(packetResult.ItemId);
                _results = packetResult.ShopRows
                    .Select((row, index) => new ScannerResult
                    {
                        ItemId = packetResult.ItemId,
                        Name = string.IsNullOrWhiteSpace(itemName) ? $"Item {packetResult.ItemId.ToString(CultureInfo.InvariantCulture)}" : itemName,
                        NoSpaceName = NormalizeScannerQuery(itemName),
                        InventoryType = InventoryItemMetadataResolver.ResolveInventoryType(packetResult.ItemId),
                        ClientListOrder = index,
                        ShopOwnerName = row.ShopOwnerName,
                        ShopTitle = row.ShopTitle,
                        FieldId = row.FieldId,
                        MiniRoomSn = row.MiniRoomSn,
                        ChannelId = row.ChannelId,
                        Quantity = row.Quantity,
                        BundleCount = row.BundleCount,
                        Price = row.Price,
                        IsNpcShop = row.IsNpcShop,
                        IsPacketFedShopRow = true
                    })
                    .ToList();
                _selectedIndex = _results.Count > 0 ? 0 : -1;
                SetActiveAddOnMode(ScannerAddOnMode.SearchResult);
                ResetSearchResultPaging();
                _exclusiveScannerRequestPending = false;
                ClearPendingScanConfirmation();

                _lastScannerResultSummary =
                    $"CWvsContext::OnShopScannerResult subtype 6 decoded {_results.Count.ToString(CultureInfo.InvariantCulture)} packet-fed shop row(s) for item {packetResult.ItemId.ToString(CultureInfo.InvariantCulture)}; CUIShopScanResult child owner is now packet-backed and the exclusive scanner request latch is clear.";
                _statusMessage = _lastScannerResultSummary;
                message = _lastScannerResultSummary;
                RefreshRows();
                return true;
            }

            _results = packetResult.ItemIds
                .Select((itemId, index) =>
                {
                    string itemName = ResolveScannerItemName(itemId);
                    return new ScannerResult
                    {
                        ItemId = itemId,
                        Name = string.IsNullOrWhiteSpace(itemName) ? $"Item {itemId.ToString(CultureInfo.InvariantCulture)}" : itemName,
                        NoSpaceName = NormalizeScannerQuery(itemName),
                        InventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId),
                        ClientListOrder = index
                    };
                })
                .ToList();
            _selectedIndex = _results.Count > 0 ? 0 : -1;
            SetActiveAddOnMode(ScannerAddOnMode.HotList);
            ResetSearchResultPaging();
            ClearPendingScanConfirmation();
            _lastScannerResultSummary =
                $"CWvsContext::OnShopScannerResult subtype 7 refreshed {_results.Count.ToString(CultureInfo.InvariantCulture)} packet-fed scanner item id(s) on the CUIShopScanner owner.";
            _statusMessage = _lastScannerResultSummary;
            message = _lastScannerResultSummary;
            RefreshRows();
            return true;
        }

        public bool ApplyShopLinkResultPayload(byte[] payload, out string message)
        {
            message = null;
            if (payload == null || payload.Length < 1)
            {
                message = "Shop-scanner link-result payload must contain the client result code byte.";
                return false;
            }

            int resultCode = payload[0];
            _lastShopLinkResultCode = resultCode;
            _lastShopLinkResultSummary = GetShopLinkResultSummary(resultCode, _lastRequestedScanItemId);
            _statusMessage = _lastShopLinkResultSummary;
            if (resultCode == ShopLinkSuccessResultCode)
            {
                _exclusiveScannerRequestPending = false;
                Hide();
            }
            else if (_lastRequestedScanItemId > 0)
            {
                ScannerResult result = _results.FirstOrDefault(row => row.ItemId == _lastRequestedScanItemId);
                if (result != null)
                {
                    _selectedIndex = _results.IndexOf(result);
                    SyncSearchResultPageToSelection();
                }
            }

            message = _lastShopLinkResultSummary;
            RefreshRows();
            return true;
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
            Rectangle bounds = GetWindowBounds();
            if (_searchBackgroundTexture != null)
            {
                sprite.Draw(_searchBackgroundTexture, bounds.Location.ToVector2(), Color.White);
            }

            if (_resultBackgroundTexture != null && _activeAddOnMode != ScannerAddOnMode.None)
            {
                sprite.Draw(_resultBackgroundTexture, new Vector2(bounds.X, bounds.Y), Color.White * 0.18f);
            }

            if (_font == null)
            {
                return;
            }

            Rectangle searchBounds = GetSearchFieldBounds();
            sprite.Draw(_pixelTexture, searchBounds, _searchFieldFocused ? new Color(255, 255, 255, 72) : new Color(255, 255, 255, 36));
            string searchText = _searchQuery + (_compositionText ?? string.Empty);
            sprite.DrawString(_font, TrimToWidth(searchText, 20), new Vector2(searchBounds.X + 4, searchBounds.Y + 1), Color.White);

            IReadOnlyList<ScannerResult> visibleResults = GetVisibleResults();
            for (int i = 0; i < visibleResults.Count; i++)
            {
                ScannerResult result = visibleResults[i];
                Rectangle rowBounds = GetRowBounds(i);
                bool selected = _selectedIndex == _scrollOffset + i;
                sprite.Draw(_pixelTexture, rowBounds, selected ? new Color(255, 255, 255, 92) : new Color(0, 0, 0, 60));
                if (_iconPlaceholderTexture != null)
                {
                    sprite.Draw(_iconPlaceholderTexture, new Rectangle(rowBounds.X + 3, rowBounds.Y + 5, 8, 8), Color.White);
                }

                sprite.DrawString(_font, TrimToWidth(result.Name, 22), new Vector2(rowBounds.X + 16, rowBounds.Y + 2), selected ? new Color(30, 45, 80) : Color.White);
                string rightText = result.IsPacketFedShopRow
                    ? result.Price.ToString("N0", CultureInfo.InvariantCulture)
                    : result.ItemId.ToString(CultureInfo.InvariantCulture);
                sprite.DrawString(_font, rightText, new Vector2(rowBounds.Right - 62, rowBounds.Y + 2), selected ? new Color(30, 45, 80) : new Color(255, 233, 160));
                if (result.IsPacketFedShopRow)
                {
                    string ownerText = result.IsNpcShop
                        ? "NPC Shop"
                        : $"{result.ShopOwnerName} ch{result.ChannelId.ToString(CultureInfo.InvariantCulture)}";
                    sprite.DrawString(_font, TrimToWidth(ownerText, 24), new Vector2(rowBounds.X + 16, rowBounds.Y + 10), selected ? new Color(30, 45, 80) : new Color(190, 211, 233));
                }
            }

            string countLabel = _results.Count == 0
                ? GetEmptyListLabel()
                : $"{Math.Min(_selectedIndex + 1, _results.Count).ToString(CultureInfo.InvariantCulture)} / {_results.Count.ToString(CultureInfo.InvariantCulture)}";
            sprite.DrawString(_font, TrimToWidth(countLabel, 32), new Vector2(bounds.X + 14, bounds.Y + FooterY - 17), new Color(255, 233, 160));
            sprite.DrawString(_font, TrimToWidth(_statusMessage, 38), new Vector2(bounds.X + 14, bounds.Y + FooterY), new Color(214, 223, 236));
        }

        private void RegisterButton(UIObject button, int x, int y, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.X = x;
            button.Y = y;
            button.ButtonClickReleased += _ => action?.Invoke();
            AddButton(button);
        }

        private void DispatchInitialScannerRequest()
        {
            _initialRequestCount++;
            byte[] payload = BuildInitialScannerRequestPayload();
            string dispatchSummary = InitialScannerRequestDispatcher?.Invoke(InitialRequestOpcode, Array.AsReadOnly(payload));
            LastInitialRequestSummary = string.IsNullOrWhiteSpace(dispatchSummary)
                ? $"CUIShopScanner::OnCreate mirrored COutPacket opcode {InitialRequestOpcode} subtype {InitialRequestSubtype} simulator-local."
                : dispatchSummary;
            _statusMessage = $"{LastInitialRequestSummary} Search edit focused.";
        }

        private void ExecuteSearch()
        {
            string query = _searchQuery?.Trim() ?? string.Empty;
            _results = Search(query, 200).ToList();
            _selectedIndex = _results.Count > 0 ? 0 : -1;
            SetActiveAddOnMode(ScannerAddOnMode.SearchResult);
            ResetSearchResultPaging();
            _statusMessage = _results.Count > 0
                ? $"CUIShopScannerSearchResult staged {_results.Count.ToString(CultureInfo.InvariantCulture)} item-name row(s) for \"{NormalizeScannerQuery(query)}\"."
                : "Scanner index found no item-name rows for that query.";
            RefreshRows();
        }

        private void ClearSearch()
        {
            _searchQuery = string.Empty;
            _compositionText = string.Empty;
            _results.Clear();
            _selectedIndex = -1;
            _scrollOffset = 0;
            _searchResultPageIndex = 0;
            _searchResultTotalPages = 0;
            ClearPendingScanConfirmation();
            SetActiveAddOnMode(ScannerAddOnMode.None);
            _searchFieldFocused = true;
            _statusMessage = "Scanner search reset; edit focus restored.";
            RefreshRows();
        }

        private void ToggleHotList()
        {
            if (_activeAddOnMode == ScannerAddOnMode.HotList)
            {
                ClearSearch();
                return;
            }

            _results = GetScannerIndex()
                .Where(entry => !entry.IsBlockedByScanBlock && !entry.IsScannerItem)
                .Take(20)
                .Select(ToScannerResult)
                .ToList();
            _selectedIndex = _results.Count > 0 ? 0 : -1;
            SetActiveAddOnMode(ScannerAddOnMode.HotList);
            ResetSearchResultPaging();
            _statusMessage = "CUIShopScannerHotList child owner toggled from button 2000; child paging uses buttons 1001/1002.";
            RefreshRows();
        }

        private void ToggleCategory()
        {
            if (_activeAddOnMode == ScannerAddOnMode.Category)
            {
                ClearSearch();
                return;
            }

            _results = GetScannerIndex()
                .Where(entry => !entry.IsBlockedByScanBlock && !entry.IsScannerItem)
                .GroupBy(entry => entry.InventoryType)
                .OrderBy(group => group.Key)
                .Select((group, index) => new ScannerResult
                {
                    ItemId = group.Count(),
                    Name = $"{group.Key} item-name rows",
                    NoSpaceName = CollapseSpaces(group.Key.ToString()),
                    InventoryType = group.Key,
                    ClientListOrder = index
                })
                .ToList();
            _selectedIndex = _results.Count > 0 ? 0 : -1;
            SetActiveAddOnMode(ScannerAddOnMode.Category);
            ResetSearchResultPaging();
            _statusMessage = "CUIShopScannerCategory child owner toggled from button 2001; category selections feed the result child.";
            RefreshRows();
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (WasPressed(keyboardState, Keys.Escape))
            {
                Hide();
                return;
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
                ExecuteSearch();
                return;
            }

            if (WasPressed(keyboardState, Keys.Back) && _searchFieldFocused && _searchQuery.Length > 0)
            {
                _searchQuery = _searchQuery[..^1];
                ClearCompositionText();
                return;
            }

            if (WasPressed(keyboardState, Keys.Up))
            {
                MoveSelection(-1);
            }
            else if (WasPressed(keyboardState, Keys.Down))
            {
                MoveSelection(1);
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                MoveChildPage(-1);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                MoveChildPage(1);
            }
        }

        private void SelectVisibleRow(int visibleRow)
        {
            int index = _scrollOffset + visibleRow;
            if (index < 0 || index >= _results.Count)
            {
                return;
            }

            _selectedIndex = index;
            SyncSearchResultPageToSelection();
            _statusMessage = $"Selected {_results[index].Name} ({_results[index].ItemId.ToString(CultureInfo.InvariantCulture)}).";
        }

        private void MoveSelection(int delta)
        {
            if (_results.Count == 0)
            {
                return;
            }

            _selectedIndex = Math.Clamp(_selectedIndex < 0 ? 0 : _selectedIndex + delta, 0, _results.Count - 1);
            if (_selectedIndex < _scrollOffset)
            {
                _scrollOffset = _selectedIndex;
            }
            else if (_selectedIndex >= _scrollOffset + VisibleRows)
            {
                _scrollOffset = _selectedIndex - VisibleRows + 1;
            }

            SyncSearchResultPageToSelection();
            _statusMessage = $"Selected {_results[_selectedIndex].Name}.";
        }

        private void MoveChildPage(int delta)
        {
            if (_results.Count == 0 || _searchResultTotalPages <= 1)
            {
                return;
            }

            int nextPage = Math.Clamp(_searchResultPageIndex + delta, 0, _searchResultTotalPages - 1);
            if (nextPage == _searchResultPageIndex)
            {
                return;
            }

            _searchResultPageIndex = nextPage;
            int pageSize = GetCurrentChildPageSize();
            _selectedIndex = Math.Clamp(_searchResultPageIndex * pageSize, 0, _results.Count - 1);
            _scrollOffset = Math.Min(_selectedIndex, Math.Max(0, _results.Count - VisibleRows));
            _statusMessage = $"CUIShopScanner child owner moved to page {(_searchResultPageIndex + 1).ToString(CultureInfo.InvariantCulture)} / {_searchResultTotalPages.ToString(CultureInfo.InvariantCulture)}.";
        }

        public bool HandleChildButton(int buttonId)
        {
            switch (buttonId)
            {
                case ChildCloseButtonId:
                    ClearSearch();
                    return true;
                case ChildPreviousButtonId:
                case ShopResultPreviousButtonId:
                    MoveChildPage(-1);
                    return true;
                case ChildNextButtonId:
                case ShopResultNextButtonId:
                    MoveChildPage(1);
                    return true;
                case ChildOkButtonId:
                    return TryStageSelectedScanConfirmation(out _);
                case SearchResultDescendingCheckBoxId:
                    ToggleDescendingOrder();
                    return true;
                case ShopResultOkButtonId:
                case ShopResultCancelButtonId:
                case ShopResultCloseButtonId:
                    ClearSearch();
                    return true;
                default:
                    if (buttonId >= ShopResultRowFirstButtonId
                        && buttonId < ShopResultRowFirstButtonId + ShopResultRowsPerPage)
                    {
                        return TrySendShopResultRowLinkRequest(buttonId - ShopResultRowFirstButtonId, Environment.TickCount, out _);
                    }

                    return false;
            }
        }

        public bool TrySendSelectedChildOkRequest(int currentTick, out string message)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _results.Count)
            {
                message = "CUIShopScannerSearchResult has no selected item row for button 1003.";
                return false;
            }

            return _results[_selectedIndex].IsPacketFedShopRow
                ? TrySendSelectedShopLinkRequest(currentTick, out message)
                : TryStageSelectedScanConfirmation(out message);
        }

        public bool TryStageSelectedScanConfirmation(out string message)
        {
            message = null;
            if (_selectedIndex < 0 || _selectedIndex >= _results.Count)
            {
                message = "CUIShopScannerSearchResult has no selected item row for button 1003.";
                return false;
            }

            ScannerResult selected = _results[_selectedIndex];
            if (selected.IsPacketFedShopRow)
            {
                message = "CUIShopScannerSearchResult button 1003 is only used for item-name result rows; packet-fed shop rows use CUIShopScanResult link buttons 3000..3007.";
                _statusMessage = message;
                return false;
            }

            if (selected.ItemId <= 0)
            {
                message = "CUIShopScannerSearchResult cannot open the scan confirmation without a concrete item id.";
                _statusMessage = message;
                return false;
            }

            _scanConfirmationPending = true;
            _pendingScanConfirmationItemId = selected.ItemId;
            _pendingScanConfirmationSummary = BuildScanConfirmationSummary(
                string.IsNullOrWhiteSpace(selected.Name) ? $"Item {selected.ItemId.ToString(CultureInfo.InvariantCulture)}" : selected.Name,
                _descendingOrderChecked);
            message = $"CUIShopScannerSearchResult button 1003 opened CUtilDlg::YesNo for item {selected.ItemId.ToString(CultureInfo.InvariantCulture)}. {_pendingScanConfirmationSummary}";
            _statusMessage = message;
            return true;
        }

        public bool ConfirmPendingScanRequest(int currentTick, out string message)
        {
            if (!_scanConfirmationPending || _pendingScanConfirmationItemId <= 0)
            {
                message = "CUIShopScannerSearchResult has no pending CUtilDlg::YesNo scan confirmation.";
                return false;
            }

            int selectedIndex = _results.FindIndex(result => result.ItemId == _pendingScanConfirmationItemId && !result.IsPacketFedShopRow);
            if (selectedIndex < 0)
            {
                message = $"CUIShopScannerSearchResult pending confirmation item {_pendingScanConfirmationItemId.ToString(CultureInfo.InvariantCulture)} is no longer present.";
                ClearPendingScanConfirmation();
                _statusMessage = message;
                return false;
            }

            _selectedIndex = selectedIndex;
            SyncSearchResultPageToSelection();
            ClearPendingScanConfirmation();
            return TrySendSelectedScanRequest(currentTick, out message);
        }

        public bool CancelPendingScanConfirmation(out string message)
        {
            if (!_scanConfirmationPending)
            {
                message = "CUIShopScannerSearchResult has no pending CUtilDlg::YesNo scan confirmation.";
                return false;
            }

            int itemId = _pendingScanConfirmationItemId;
            ClearPendingScanConfirmation();
            message = $"CUIShopScannerSearchResult cancelled CUtilDlg::YesNo scan confirmation for item {itemId.ToString(CultureInfo.InvariantCulture)}.";
            _statusMessage = message;
            return true;
        }

        public bool TrySendSelectedScanRequest(int currentTick, out string message)
        {
            message = null;
            if (_selectedIndex < 0 || _selectedIndex >= _results.Count)
            {
                message = "CUIShopScannerSearchResult has no selected item row for button 1003.";
                return false;
            }

            ScannerResult selected = _results[_selectedIndex];
            if (selected.ItemId <= 0)
            {
                message = "CUIShopScannerSearchResult cannot send a scan request without a concrete item id.";
                _statusMessage = message;
                return false;
            }

            if (!IsScannerExclusiveRequestGateOpen(_exclusiveScannerRequestPending, _lastRequestedScanTick, currentTick))
            {
                message = $"CUIShopScanner::SendScanPacket blocked item {selected.ItemId.ToString(CultureInfo.InvariantCulture)} because the client exclusive-request latch is still active or the 500 ms request throttle has not elapsed.";
                _statusMessage = message;
                return false;
            }

            _lastRequestedScanDescending = _descendingOrderChecked;
            byte[] payload = BuildShopScanRequestPayload(selected.ItemId, _lastRequestedScanDescending, currentTick);
            string dispatchSummary = ScanItemRequestDispatcher?.Invoke(InitialRequestOpcode, Array.AsReadOnly(payload));
            _lastRequestedScanItemId = selected.ItemId;
            _lastRequestedMiniRoomSn = 0;
            _lastRequestedFieldId = 0;
            _lastRequestedScanTick = currentTick;
            _exclusiveScannerRequestPending = true;
            message = string.IsNullOrWhiteSpace(dispatchSummary)
                ? $"CUIShopScanner::SendScanPacket staged opcode {InitialRequestOpcode} item {selected.ItemId.ToString(CultureInfo.InvariantCulture)} descending {(_lastRequestedScanDescending ? 1 : 0).ToString(CultureInfo.InvariantCulture)} tick {currentTick.ToString(CultureInfo.InvariantCulture)} simulator-local."
                : dispatchSummary;
            _statusMessage = message;
            Hide();
            return true;
        }

        private void ToggleDescendingOrder()
        {
            _descendingOrderChecked = !_descendingOrderChecked;
            _lastRequestedScanDescending = _descendingOrderChecked;
            if (_scanConfirmationPending && _pendingScanConfirmationItemId > 0)
            {
                ScannerResult pending = _results.FirstOrDefault(result => result.ItemId == _pendingScanConfirmationItemId);
                string itemName = pending == null || string.IsNullOrWhiteSpace(pending.Name)
                    ? $"Item {_pendingScanConfirmationItemId.ToString(CultureInfo.InvariantCulture)}"
                    : pending.Name;
                _pendingScanConfirmationSummary = BuildScanConfirmationSummary(itemName, _descendingOrderChecked);
            }

            _statusMessage = _descendingOrderChecked
                ? "CUIShopScannerSearchResult checkbox 1004 checked: scan result order is descending."
                : "CUIShopScannerSearchResult checkbox 1004 cleared: scan result order is ascending.";
        }

        private void ClearPendingScanConfirmation()
        {
            _scanConfirmationPending = false;
            _pendingScanConfirmationItemId = 0;
            _pendingScanConfirmationSummary = string.Empty;
        }

        private bool TrySendShopResultRowLinkRequest(int visibleRow, int currentTick, out string message)
        {
            int index = (_searchResultPageIndex * GetCurrentChildPageSize()) + visibleRow;
            if (index < 0 || index >= _results.Count)
            {
                message = $"CUIShopScanResult row button {(ShopResultRowFirstButtonId + visibleRow).ToString(CultureInfo.InvariantCulture)} has no item on the current page.";
                _statusMessage = message;
                return false;
            }

            _selectedIndex = index;
            _scrollOffset = Math.Min(_selectedIndex, Math.Max(0, _results.Count - VisibleRows));
            return TrySendSelectedShopLinkRequest(currentTick, out message);
        }

        public bool TrySendSelectedShopLinkRequest(int currentTick, out string message)
        {
            message = null;
            if (_selectedIndex < 0 || _selectedIndex >= _results.Count)
            {
                message = "CUIShopScannerSearchResult has no selected item row for button 1003.";
                return false;
            }

            ScannerResult selected = _results[_selectedIndex];
            if (!selected.IsPacketFedShopRow)
            {
                message = "CUIShopScanResult shop-link request waits for packet-fed subtype 6 rows before sending opcode 73.";
                _statusMessage = message;
                return false;
            }

            if (selected.IsNpcShop)
            {
                message = "CUIShopScanResult selected the NPC-shop sentinel row; the client does not send a shop-link packet for NPC-shop rows.";
                _statusMessage = message;
                return false;
            }

            byte[] payload = BuildShopLinkRequestPayload(selected.MiniRoomSn, selected.FieldId);
            string dispatchSummary = ShopLinkRequestDispatcher?.Invoke(ShopLinkRequestOpcode, Array.AsReadOnly(payload));
            _lastRequestedScanItemId = selected.ItemId;
            _lastRequestedMiniRoomSn = selected.MiniRoomSn;
            _lastRequestedFieldId = selected.FieldId;
            _lastRequestedScanTick = currentTick;
            _lastRequestedScanDescending = false;
            message = string.IsNullOrWhiteSpace(dispatchSummary)
                ? $"CUIShopScanResult::OnButtonClicked staged opcode {ShopLinkRequestOpcode} miniRoomSN {selected.MiniRoomSn.ToString(CultureInfo.InvariantCulture)} field {selected.FieldId.ToString(CultureInfo.InvariantCulture)} simulator-local."
                : dispatchSummary;
            _statusMessage = message;
            return true;
        }

        private void ResetSearchResultPaging()
        {
            _searchResultPageIndex = 0;
            _searchResultTotalPages = _results.Count == 0
                ? 0
                : (int)Math.Ceiling(_results.Count / (double)GetCurrentChildPageSize());
            _scrollOffset = 0;
        }

        private void SyncSearchResultPageToSelection()
        {
            if (_selectedIndex < 0 || _results.Count == 0)
            {
                _searchResultPageIndex = 0;
                return;
            }

            _searchResultPageIndex = Math.Clamp(_selectedIndex / GetCurrentChildPageSize(), 0, Math.Max(0, _searchResultTotalPages - 1));
        }

        private int GetCurrentChildPageSize()
        {
            return _activeAddOnMode == ScannerAddOnMode.SearchResult && _results.Any(result => result.IsPacketFedShopRow)
                ? ShopResultRowsPerPage
                : SearchResultPageSize;
        }

        private bool IsChildPreviousButtonEnabled()
        {
            return _results.Count > 0 && _searchResultPageIndex > 0;
        }

        private bool IsChildNextButtonEnabled()
        {
            return _results.Count > 0 && _searchResultPageIndex < _searchResultTotalPages - 1;
        }

        private IReadOnlyList<int> GetChildPageItemIds(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _searchResultTotalPages || _results.Count == 0)
            {
                return Array.Empty<int>();
            }

            int pageSize = GetCurrentChildPageSize();
            return _results
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(result => result.ItemId)
                .ToList();
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool visible = IsVisible && _scrollOffset + i < _results.Count;
                _rowButtons[i].SetVisible(visible);
                _rowButtons[i].SetEnabled(visible);
            }
        }

        private IReadOnlyList<ScannerResult> GetVisibleResults()
        {
            if (_results.Count == 0)
            {
                return Array.Empty<ScannerResult>();
            }

            return _results.Skip(_scrollOffset).Take(VisibleRows).ToList();
        }

        private string GetEmptyListLabel()
        {
            return _activeAddOnMode switch
            {
                ScannerAddOnMode.HotList => "No hot-list rows staged.",
                ScannerAddOnMode.Category => "No category rows staged.",
                ScannerAddOnMode.SearchResult => "No result rows staged.",
                _ => "No scanner rows staged."
            };
        }

        private Rectangle GetSearchFieldBounds()
        {
            return new Rectangle(Position.X + SearchTextX, Position.Y + SearchTextY, SearchTextWidth, SearchFieldHeight);
        }

        private Rectangle GetRowBounds(int visibleRow)
        {
            return new Rectangle(Position.X + ListX, Position.Y + ListY + (visibleRow * RowHeight), ListWidth, RowHeight - 2);
        }

        private bool TryAppendSearchCharacter(char character)
        {
            if (!_searchFieldFocused
                || char.IsControl(character)
                || !SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, _searchQuery.Length, SearchMaxLength, character))
            {
                return false;
            }

            ClearCompositionText();
            _searchQuery += character;
            return true;
        }

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (TryAppendSearchCharacter(character))
            {
                return true;
            }

            errorMessage = "The shop-scanner search edit cannot accept that character.";
            return false;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                _searchQuery = _searchQuery[..^1];
            }

            return ((ISoftKeyboardHost)this).TryInsertSoftKeyboardCharacter(character, out errorMessage);
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrEmpty(_searchQuery))
            {
                errorMessage = "The shop-scanner search edit is already empty.";
                return false;
            }

            _searchQuery = _searchQuery[..^1];
            ClearCompositionText();
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            ExecuteSearch();
            return true;
        }

        void ISoftKeyboardHost.SetSoftKeyboardCompositionText(string text)
        {
            HandleCompositionText(text);
        }

        void ISoftKeyboardHost.OnSoftKeyboardClosed()
        {
            _softKeyboardActive = false;
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds()
        {
            return GetSearchFieldBounds();
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        internal static byte[] BuildInitialScannerRequestPayload()
        {
            return new[] { unchecked((byte)InitialRequestSubtype) };
        }

        internal static bool IsScannerExclusiveRequestGateOpen(bool exclusiveRequestPending, int lastRequestTick, int currentTick)
        {
            return !exclusiveRequestPending
                && unchecked(currentTick - lastRequestTick) >= ScanRequestThrottleMilliseconds;
        }

        internal static byte[] BuildShopLinkRequestPayload(int miniRoomSn, int fieldId)
        {
            byte[] payload = new byte[sizeof(int) + sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), miniRoomSn);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int), sizeof(int)), fieldId);
            return payload;
        }

        internal static byte[] BuildShopScanRequestPayload(int itemId, bool descendingOrder, int updateTick)
        {
            byte[] payload = new byte[sizeof(int) + sizeof(byte) + sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), itemId);
            payload[sizeof(int)] = descendingOrder ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) + sizeof(byte), sizeof(int)), updateTick);
            return payload;
        }

        internal static string BuildScanConfirmationSummary(string itemName, bool descendingOrder)
        {
            string displayName = string.IsNullOrWhiteSpace(itemName) ? "this item" : itemName.Trim();
            string orderText = MapleStoryStringPool.GetOrFallback(
                descendingOrder ? ScanConfirmationDescendingStringPoolId : ScanConfirmationAscendingStringPoolId,
                descendingOrder ? "high price order" : "low price order");
            string format = MapleStoryStringPool.GetOrFallback(
                ScanConfirmationFormatStringPoolId,
                "Do you want to search for %s in %s?");

            return FormatClientString(format, displayName, orderText);
        }

        private static string FormatClientString(string format, params string[] args)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Join(" ", args.Where(arg => !string.IsNullOrWhiteSpace(arg)));
            }

            string normalized = format;
            for (int i = 0; i < args.Length; i++)
            {
                int tokenIndex = normalized.IndexOf("%s", StringComparison.Ordinal);
                if (tokenIndex < 0)
                {
                    break;
                }

                normalized = normalized.Remove(tokenIndex, 2).Insert(tokenIndex, args[i] ?? string.Empty);
            }

            return normalized;
        }

        internal static bool TryDecodeScannerResultPayload(byte[] payload, out ScannerPacketResult result, out string error)
        {
            result = null;
            error = null;
            payload ??= Array.Empty<byte>();
            if (payload.Length < 1)
            {
                error = "Shop-scanner result payload must start with subtype 6 or 7.";
                return false;
            }

            int offset = 0;
            int subtype = payload[offset++];
            if (subtype == ScannerResultShopRowsSubtype)
            {
                if (!TryReadInt32(payload, ref offset, out int npcShopPrice)
                    || !TryReadInt32(payload, ref offset, out int itemId)
                    || !TryReadInt32(payload, ref offset, out int rowCount))
                {
                    error = "Shop-scanner subtype 6 payload must include npcShopPrice, itemId, and row count.";
                    return false;
                }

                if (rowCount < 0 || rowCount > 512)
                {
                    error = $"Shop-scanner subtype 6 row count {rowCount.ToString(CultureInfo.InvariantCulture)} is outside the supported range.";
                    return false;
                }

                List<ScannerPacketShopRow> rows = new();
                if (npcShopPrice > 0)
                {
                    rows.Add(new ScannerPacketShopRow
                    {
                        ShopOwnerName = "NPC Shop",
                        FieldId = -1,
                        ShopTitle = string.Empty,
                        Quantity = 0,
                        BundleCount = 1,
                        Price = npcShopPrice,
                        MiniRoomSn = -1,
                        ChannelId = -1,
                        InventoryTypeCode = ResolveClientInventoryTypeCode(itemId),
                        IsNpcShop = true
                    });
                }

                for (int i = 0; i < rowCount; i++)
                {
                    if (!TryReadMapleString(payload, ref offset, out string ownerName)
                        || !TryReadInt32(payload, ref offset, out int fieldId)
                        || !TryReadMapleString(payload, ref offset, out string title)
                        || !TryReadInt32(payload, ref offset, out int quantity)
                        || !TryReadInt32(payload, ref offset, out int bundleCount)
                        || !TryReadInt32(payload, ref offset, out int price)
                        || !TryReadInt32(payload, ref offset, out int miniRoomSn))
                    {
                        error = $"Shop-scanner subtype 6 row {i.ToString(CultureInfo.InvariantCulture)} is truncated before channel/type/item-slot data.";
                        return false;
                    }

                    if (offset >= payload.Length)
                    {
                        error = $"Shop-scanner subtype 6 row {i.ToString(CultureInfo.InvariantCulture)} is missing channel id.";
                        return false;
                    }

                    int channelId = payload[offset++];
                    if (offset >= payload.Length)
                    {
                        error = $"Shop-scanner subtype 6 row {i.ToString(CultureInfo.InvariantCulture)} is missing inventory type.";
                        return false;
                    }

                    int inventoryTypeCode = payload[offset++];
                    int itemSlotPayloadStart = offset;
                    bool hasItemSlotPayload = TrySkipItemSlotPayload(
                        payload,
                        ref offset,
                        itemId,
                        out int decodedItemSlotType,
                        out int decodedItemSlotItemId);
                    rows.Add(new ScannerPacketShopRow
                    {
                        ShopOwnerName = ownerName,
                        FieldId = fieldId,
                        ShopTitle = title,
                        Quantity = quantity,
                        BundleCount = bundleCount,
                        Price = price,
                        MiniRoomSn = miniRoomSn,
                        ChannelId = channelId,
                        InventoryTypeCode = inventoryTypeCode,
                        HasItemSlotPayload = hasItemSlotPayload,
                        ItemSlotPayloadLength = Math.Max(0, offset - itemSlotPayloadStart),
                        DecodedItemSlotType = decodedItemSlotType,
                        DecodedItemSlotItemId = decodedItemSlotItemId,
                        IsNpcShop = false
                    });
                }

                result = new ScannerPacketResult
                {
                    Subtype = subtype,
                    NpcShopPrice = npcShopPrice,
                    ItemId = itemId,
                    ServerRowCount = rowCount,
                    ShopRows = rows,
                    HasNoResultNotice = npcShopPrice == 0 && rowCount == 0,
                    TrailingByteCount = Math.Max(0, payload.Length - offset)
                };
                return true;
            }

            if (subtype == ScannerResultItemIdListSubtype)
            {
                if (offset >= payload.Length)
                {
                    error = "Shop-scanner subtype 7 payload must include an item-id count byte.";
                    return false;
                }

                int count = payload[offset++];
                if (payload.Length - offset < count * sizeof(int))
                {
                    error = "Shop-scanner subtype 7 item-id list is truncated.";
                    return false;
                }

                List<int> itemIds = new();
                for (int i = 0; i < count; i++)
                {
                    itemIds.Add(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int))));
                    offset += sizeof(int);
                }

                result = new ScannerPacketResult
                {
                    Subtype = subtype,
                    ItemIds = itemIds,
                    TrailingByteCount = Math.Max(0, payload.Length - offset)
                };
                return true;
            }

            error = $"Unsupported shop-scanner result subtype {subtype.ToString(CultureInfo.InvariantCulture)}.";
            return false;
        }

        private static bool TryReadInt32(byte[] payload, ref int offset, out int value)
        {
            value = 0;
            if (payload == null || payload.Length - offset < sizeof(int))
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset, sizeof(int)));
            offset += sizeof(int);
            return true;
        }

        private static bool TryReadInt16(byte[] payload, ref int offset, out short value)
        {
            value = 0;
            if (payload == null || payload.Length - offset < sizeof(short))
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(offset, sizeof(short)));
            offset += sizeof(short);
            return true;
        }

        private static bool TryReadUInt16(byte[] payload, ref int offset, out ushort value)
        {
            value = 0;
            if (payload == null || payload.Length - offset < sizeof(ushort))
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            return true;
        }

        private static bool TryReadInt64(byte[] payload, ref int offset, out long value)
        {
            value = 0;
            if (payload == null || payload.Length - offset < sizeof(long))
            {
                return false;
            }

            value = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(offset, sizeof(long)));
            offset += sizeof(long);
            return true;
        }

        private static bool TryReadByte(byte[] payload, ref int offset, out byte value)
        {
            value = 0;
            if (payload == null || offset >= payload.Length)
            {
                return false;
            }

            value = payload[offset++];
            return true;
        }

        private static bool TrySkipBytes(byte[] payload, ref int offset, int count)
        {
            if (payload == null || count < 0 || payload.Length - offset < count)
            {
                return false;
            }

            offset += count;
            return true;
        }

        private static bool TryReadMapleString(byte[] payload, ref int offset, out string value)
        {
            value = string.Empty;
            if (payload == null || payload.Length - offset < sizeof(ushort))
            {
                return false;
            }

            int length = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (length < 0 || payload.Length - offset < length)
            {
                return false;
            }

            value = length == 0 ? string.Empty : Encoding.ASCII.GetString(payload, offset, length);
            offset += length;
            return true;
        }

        private static bool TrySkipMapleString(byte[] payload, ref int offset)
        {
            return TryReadMapleString(payload, ref offset, out _);
        }

        private static bool TrySkipItemSlotPayload(
            byte[] payload,
            ref int offset,
            int expectedItemId,
            out int decodedItemSlotType,
            out int decodedItemSlotItemId)
        {
            decodedItemSlotType = 0;
            decodedItemSlotItemId = 0;
            int startOffset = offset;

            if (!TryReadByte(payload, ref offset, out byte itemSlotType)
                || itemSlotType is < 1 or > 3
                || !TryReadInt32(payload, ref offset, out int itemSlotItemId)
                || itemSlotItemId != expectedItemId
                || !TryReadByte(payload, ref offset, out byte cashSerialFlag))
            {
                offset = startOffset;
                return false;
            }

            if (cashSerialFlag != 0 && !TryReadInt64(payload, ref offset, out _))
            {
                offset = startOffset;
                return false;
            }

            if (!TryReadInt64(payload, ref offset, out _))
            {
                offset = startOffset;
                return false;
            }

            bool decoded = itemSlotType switch
            {
                1 => TrySkipEquipItemSlotBody(payload, ref offset, cashSerialFlag != 0),
                2 => TrySkipBundleItemSlotBody(payload, ref offset, itemSlotItemId),
                3 => TrySkipPetItemSlotBody(payload, ref offset),
                _ => false
            };

            if (!decoded)
            {
                offset = startOffset;
                return false;
            }

            decodedItemSlotType = itemSlotType;
            decodedItemSlotItemId = itemSlotItemId;
            return true;
        }

        private static bool TrySkipEquipItemSlotBody(byte[] payload, ref int offset, bool hasCashItemSerialNumber)
        {
            if (!TryReadByte(payload, ref offset, out _)
                || !TryReadByte(payload, ref offset, out _))
            {
                return false;
            }

            for (int i = 0; i < 15; i++)
            {
                if (!TryReadInt16(payload, ref offset, out _))
                {
                    return false;
                }
            }

            if (!TrySkipMapleString(payload, ref offset)
                || !TryReadInt16(payload, ref offset, out _)
                || !TryReadByte(payload, ref offset, out _)
                || !TryReadByte(payload, ref offset, out _)
                || !TryReadInt32(payload, ref offset, out _)
                || !TryReadInt32(payload, ref offset, out _)
                || !TryReadInt32(payload, ref offset, out _)
                || !TryReadByte(payload, ref offset, out _)
                || !TryReadByte(payload, ref offset, out _))
            {
                return false;
            }

            for (int i = 0; i < 5; i++)
            {
                if (!TryReadInt16(payload, ref offset, out _))
                {
                    return false;
                }
            }

            if (!hasCashItemSerialNumber && !TryReadInt64(payload, ref offset, out _))
            {
                return false;
            }

            return TryReadInt64(payload, ref offset, out _);
        }

        private static bool TrySkipBundleItemSlotBody(byte[] payload, ref int offset, int itemId)
        {
            if (!TryReadUInt16(payload, ref offset, out _)
                || !TrySkipMapleString(payload, ref offset)
                || !TryReadInt16(payload, ref offset, out _))
            {
                return false;
            }

            return (itemId / 10000) is 207 or 233
                ? TryReadInt64(payload, ref offset, out _)
                : true;
        }

        private static bool TrySkipPetItemSlotBody(byte[] payload, ref int offset)
        {
            return TrySkipBytes(payload, ref offset, 13)
                && TryReadByte(payload, ref offset, out _)
                && TryReadInt16(payload, ref offset, out _)
                && TryReadByte(payload, ref offset, out _)
                && TryReadInt64(payload, ref offset, out _)
                && TryReadInt16(payload, ref offset, out _)
                && TryReadUInt16(payload, ref offset, out _)
                && TryReadInt32(payload, ref offset, out _)
                && TryReadInt16(payload, ref offset, out _);
        }

        private static int ResolveClientInventoryTypeCode(int itemId)
        {
            return InventoryItemMetadataResolver.ResolveInventoryType(itemId) switch
            {
                InventoryType.EQUIP => 1,
                InventoryType.USE => 2,
                InventoryType.SETUP => 3,
                InventoryType.ETC => 4,
                InventoryType.CASH => 5,
                _ => 0
            };
        }

        private void SetActiveAddOnMode(ScannerAddOnMode mode)
        {
            _activeAddOnMode = mode;
            _hotListChildShown = mode == ScannerAddOnMode.HotList;
            _categoryChildShown = mode == ScannerAddOnMode.Category;
            _searchResultChildShown = mode == ScannerAddOnMode.SearchResult;

            _hotListChildCreated |= _hotListChildShown;
            _categoryChildCreated |= _categoryChildShown;
            _searchResultChildCreated |= _searchResultChildShown;
        }

        private static string ResolveScannerItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            Dictionary<int, Tuple<string, string, string>> cache = global::HaCreator.Program.InfoManager?.ItemNameCache;
            return cache != null && cache.TryGetValue(itemId, out Tuple<string, string, string> value)
                ? value?.Item2?.Trim() ?? string.Empty
                : string.Empty;
        }

        private static ScannerResult ToScannerResult(ScannerIndexEntry entry)
        {
            return new ScannerResult
            {
                ItemId = entry.ItemId,
                Name = entry.Name,
                NoSpaceName = entry.NoSpaceName,
                InventoryType = entry.InventoryType,
                ClientListOrder = entry.ClientListOrder,
                IsBlockedByScanBlock = entry.IsBlockedByScanBlock,
                IsScannerItem = entry.IsScannerItem
            };
        }

        private static IReadOnlyList<ScannerIndexEntry> GetScannerIndex()
        {
            if (_scannerIndex != null)
            {
                return _scannerIndex;
            }

            lock (ScannerIndexLock)
            {
                if (_scannerIndex != null)
                {
                    return _scannerIndex;
                }

                HashSet<int> scanBlockedItemIds = LoadScanBlockedItemIds();
                List<ScannerIndexEntry> entries = new();
                int clientListOrder = 0;
                foreach (KeyValuePair<int, Tuple<string, string, string>> itemInfo in EnumerateItemNameCache())
                {
                    int itemId = itemInfo.Key;
                    string itemName = itemInfo.Value?.Item2?.Trim();
                    if (itemId <= 0 || string.IsNullOrWhiteSpace(itemName))
                    {
                        continue;
                    }

                    bool isScannerItem = IsShopScannerItem(itemId);
                    entries.Add(new ScannerIndexEntry
                    {
                        ItemId = itemId,
                        Name = itemName,
                        NoSpaceName = NormalizeScannerQuery(itemName),
                        InventoryType = InventoryItemMetadataResolver.ResolveInventoryType(itemId),
                        ClientListOrder = clientListOrder++,
                        IsBlockedByScanBlock = scanBlockedItemIds.Contains(itemId),
                        IsScannerItem = isScannerItem
                    });
                }

                _scannerIndex = entries;
                return _scannerIndex;
            }
        }

        private static IEnumerable<KeyValuePair<int, Tuple<string, string, string>>> EnumerateItemNameCache()
        {
            Dictionary<int, Tuple<string, string, string>> cache = global::HaCreator.Program.InfoManager?.ItemNameCache;
            if (cache == null || cache.Count == 0)
            {
                yield break;
            }

            foreach (KeyValuePair<int, Tuple<string, string, string>> pair in cache.OrderBy(pair => pair.Key))
            {
                yield return pair;
            }
        }

        internal static HashSet<int> LoadScanBlockedItemIds()
        {
            HashSet<int> blocked = new();
            WzImage scanBlockImage = global::HaCreator.Program.FindImage("Etc", "ScanBlock.img")
                ?? global::HaCreator.Program.FindImage("etc", "ScanBlock.img");
            foreach (WzImageProperty property in scanBlockImage?.WzProperties ?? Enumerable.Empty<WzImageProperty>())
            {
                if (int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)
                    && itemId > 0)
                {
                    blocked.Add(itemId);
                }
            }

            return blocked;
        }

        internal static bool IsShopScannerItem(int itemId)
        {
            return itemId > 0 && itemId / 10000 == 231;
        }

        internal static string CollapseSpaces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return string.Concat(text.Where(character => !char.IsWhiteSpace(character)));
        }

        internal static string NormalizeScannerQuery(string text)
        {
            return CollapseSpaces(text).ToUpperInvariant();
        }

        internal static string GetShopLinkResultSummary(int resultCode, int itemId)
        {
            string itemSuffix = itemId > 0 ? $" for item {itemId.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
            return resultCode switch
            {
                ShopLinkSuccessResultCode => $"CUIShopScanResult::OnShopLinkResult accepted shop link{itemSuffix} and closed the scanner.",
                ShopLinkNoShopResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: target shop is unavailable.",
                ShopLinkAlreadyMovedResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: target shop owner already moved.",
                ShopLinkUnknownItemResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: target item is no longer listed.",
                ShopLinkCannotEnterResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: current field cannot enter that shop.",
                ShopLinkNoMesoResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: not enough meso for the scan.",
                ShopLinkNoShopChannelResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: no shop was found on this channel.",
                ShopLinkExpiredResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: scanner data expired.",
                ShopLinkTooFarResultCode => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: shop link target is too far away.",
                _ => $"CUIShopScanResult::OnShopLinkResult rejected shop link{itemSuffix}: unrecovered result code {resultCode.ToString(CultureInfo.InvariantCulture)}."
            };
        }

        private static string TrimToWidth(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length > maxChars ? text[..Math.Max(0, maxChars - 3)] + "..." : text;
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
            BaseDXDrawableItem drawable = new BaseDXDrawableItem(new DXObject(0, 0, texture, 0), false);
            return new UIObject(drawable, drawable, drawable, drawable);
        }
    }
}
