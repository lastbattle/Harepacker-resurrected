using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
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
        internal const int InitialRequestSubtype = 5;
        internal const int ChildCloseButtonId = 1000;
        internal const int ChildPreviousButtonId = 1001;
        internal const int ChildNextButtonId = 1002;
        internal const int ChildOkButtonId = 1003;
        internal const int SearchResultPageSize = 10;
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
        private bool _lastRequestedScanDescending;
        private int _lastRequestedScanTick;
        private int? _lastShopLinkResultCode;
        private string _lastShopLinkResultSummary = "No scanner shop-link result packet has been applied.";

        public ShopScannerWindow(
            IDXObject frame,
            Texture2D searchBackgroundTexture,
            Texture2D resultBackgroundTexture,
            Texture2D iconPlaceholderTexture,
            UIObject top10Button,
            UIObject categoryButton,
            UIObject searchButton,
            UIObject closeButton,
            GraphicsDevice device)
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
            public bool LastRequestedScanDescending { get; init; }
            public int LastRequestedScanTick { get; init; }
            public int? LastShopLinkResultCode { get; init; }
            public string LastShopLinkResultSummary { get; init; } = string.Empty;
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
                LastRequestedScanDescending = _lastRequestedScanDescending,
                LastRequestedScanTick = _lastRequestedScanTick,
                LastShopLinkResultCode = _lastShopLinkResultCode,
                LastShopLinkResultSummary = _lastShopLinkResultSummary
            };
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
                sprite.DrawString(_font, result.ItemId.ToString(CultureInfo.InvariantCulture), new Vector2(rowBounds.Right - 62, rowBounds.Y + 2), selected ? new Color(30, 45, 80) : new Color(255, 233, 160));
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
            _activeAddOnMode = ScannerAddOnMode.SearchResult;
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
            _activeAddOnMode = ScannerAddOnMode.None;
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
            _activeAddOnMode = ScannerAddOnMode.HotList;
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
            _activeAddOnMode = ScannerAddOnMode.Category;
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
            _selectedIndex = Math.Clamp(_searchResultPageIndex * SearchResultPageSize, 0, _results.Count - 1);
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
                    MoveChildPage(-1);
                    return true;
                case ChildNextButtonId:
                    MoveChildPage(1);
                    return true;
                case ChildOkButtonId:
                    return TrySendSelectedShopLinkRequest(descendingOrder: false, Environment.TickCount, out _);
                default:
                    return false;
            }
        }

        public bool TrySendSelectedShopLinkRequest(bool descendingOrder, int currentTick, out string message)
        {
            message = null;
            if (_selectedIndex < 0 || _selectedIndex >= _results.Count)
            {
                message = "CUIShopScannerSearchResult has no selected item row for button 1003.";
                return false;
            }

            ScannerResult selected = _results[_selectedIndex];
            byte[] payload = BuildShopLinkRequestPayload(selected.ItemId, descendingOrder, currentTick);
            string dispatchSummary = ShopLinkRequestDispatcher?.Invoke(InitialRequestOpcode, Array.AsReadOnly(payload));
            _lastRequestedScanItemId = selected.ItemId;
            _lastRequestedScanDescending = descendingOrder;
            _lastRequestedScanTick = currentTick;
            message = string.IsNullOrWhiteSpace(dispatchSummary)
                ? $"CUIShopScanner::SendScanPacket staged opcode {InitialRequestOpcode} for {selected.Name} ({selected.ItemId.ToString(CultureInfo.InvariantCulture)}) simulator-local."
                : dispatchSummary;
            _statusMessage = message;
            return true;
        }

        private void ResetSearchResultPaging()
        {
            _searchResultPageIndex = 0;
            _searchResultTotalPages = _results.Count == 0
                ? 0
                : (int)Math.Ceiling(_results.Count / (double)SearchResultPageSize);
            _scrollOffset = 0;
        }

        private void SyncSearchResultPageToSelection()
        {
            if (_selectedIndex < 0 || _results.Count == 0)
            {
                _searchResultPageIndex = 0;
                return;
            }

            _searchResultPageIndex = Math.Clamp(_selectedIndex / SearchResultPageSize, 0, Math.Max(0, _searchResultTotalPages - 1));
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

        internal static byte[] BuildShopLinkRequestPayload(int itemId, bool descendingOrder, int currentTick)
        {
            byte[] payload = new byte[sizeof(int) + sizeof(byte) + sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, sizeof(int)), itemId);
            payload[sizeof(int)] = descendingOrder ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(sizeof(int) + sizeof(byte), sizeof(int)), currentTick);
            return payload;
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
