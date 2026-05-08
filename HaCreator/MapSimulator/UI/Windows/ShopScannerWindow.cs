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
        private const int SearchTextX = 56;
        private const int SearchTextY = 31;
        private const int SearchTextWidth = 130;
        private const int SearchFieldHeight = 16;
        private const int ListX = 14;
        private const int ListY = 62;
        private const int ListWidth = 218;
        private const int RowHeight = 20;
        private const int VisibleRows = 8;
        private const int FooterY = 239;
        private const int InitialRequestOpcode = 72;
        private const int InitialRequestSubtype = 5;
        private static readonly object ScannerIndexLock = new();
        private static IReadOnlyList<ScannerIndexEntry> _scannerIndex;

        private readonly Texture2D _searchBackgroundTexture;
        private readonly Texture2D _resultBackgroundTexture;
        private readonly Texture2D _iconPlaceholderTexture;
        private readonly Texture2D _pixelTexture;
        private readonly UIObject _searchButton;
        private readonly UIObject _retryButton;
        private readonly UIObject _closeButton;
        private readonly UIObject _backButton;
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

        public ShopScannerWindow(
            IDXObject frame,
            Texture2D searchBackgroundTexture,
            Texture2D resultBackgroundTexture,
            Texture2D iconPlaceholderTexture,
            UIObject searchButton,
            UIObject retryButton,
            UIObject closeButton,
            UIObject backButton,
            GraphicsDevice device)
            : base(frame)
        {
            _searchBackgroundTexture = searchBackgroundTexture;
            _resultBackgroundTexture = resultBackgroundTexture;
            _iconPlaceholderTexture = iconPlaceholderTexture;
            _searchButton = searchButton;
            _retryButton = retryButton;
            _closeButton = closeButton;
            _backButton = backButton;
            _device = device;

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            RegisterButton(_searchButton, 194, 30, ExecuteSearch);
            RegisterButton(_retryButton, 96, 241, ExecuteSearch);
            RegisterButton(_backButton, 30, 241, ClearSearch);
            RegisterButton(_closeButton, 162, 241, Hide);

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
            string normalizedQuery = NormalizeSearchText(query);
            string collapsedQuery = CollapseSpaces(normalizedQuery);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return Array.Empty<ScannerResult>();
            }

            return GetScannerIndex()
                .Where(entry => !entry.IsBlockedByScanBlock && !entry.IsScannerItem)
                .Select(entry => new
                {
                    Entry = entry,
                    Score = ScoreEntry(entry, normalizedQuery, collapsedQuery)
                })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Entry.ClientListOrder)
                .ThenBy(candidate => candidate.Entry.ItemId)
                .Take(Math.Max(1, maxResults))
                .Select(candidate => new ScannerResult
                {
                    ItemId = candidate.Entry.ItemId,
                    Name = candidate.Entry.Name,
                    NoSpaceName = candidate.Entry.NoSpaceName,
                    InventoryType = candidate.Entry.InventoryType,
                    ClientListOrder = candidate.Entry.ClientListOrder,
                    IsBlockedByScanBlock = candidate.Entry.IsBlockedByScanBlock,
                    IsScannerItem = candidate.Entry.IsScannerItem
                })
                .ToList();
        }

        public string GetStatusSummary()
        {
            return $"{LastInitialRequestSummary} Index rows: {GetScannerIndex().Count.ToString(CultureInfo.InvariantCulture)}.";
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

            if (_resultBackgroundTexture != null && _results.Count > 0)
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
                ? "No scanner rows staged."
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
            LastInitialRequestSummary = $"CUIShopScanner::OnCreate mirrored COutPacket opcode {InitialRequestOpcode} subtype {InitialRequestSubtype}.";
            _statusMessage = $"{LastInitialRequestSummary} Search edit focused.";
        }

        private void ExecuteSearch()
        {
            string query = _searchQuery?.Trim() ?? string.Empty;
            _results = Search(query, 200).ToList();
            _selectedIndex = _results.Count > 0 ? 0 : -1;
            _scrollOffset = 0;
            _statusMessage = _results.Count > 0
                ? $"Scanner index matched {_results.Count.ToString(CultureInfo.InvariantCulture)} item-name row(s)."
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
            _searchFieldFocused = true;
            _statusMessage = "Scanner search reset; edit focus restored.";
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
        }

        private void SelectVisibleRow(int visibleRow)
        {
            int index = _scrollOffset + visibleRow;
            if (index < 0 || index >= _results.Count)
            {
                return;
            }

            _selectedIndex = index;
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

            _statusMessage = $"Selected {_results[_selectedIndex].Name}.";
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

        private static int ScoreEntry(ScannerIndexEntry entry, string normalizedQuery, string collapsedQuery)
        {
            if (entry == null || string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return 0;
            }

            string normalizedName = NormalizeSearchText(entry.Name);
            if (string.Equals(normalizedName, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 500;
            }

            if (normalizedName.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 420;
            }

            if (normalizedName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 320;
            }

            if (!string.IsNullOrWhiteSpace(collapsedQuery)
                && entry.NoSpaceName.Contains(collapsedQuery, StringComparison.OrdinalIgnoreCase))
            {
                return 300;
            }

            return 0;
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
                        NoSpaceName = CollapseSpaces(itemName),
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

        private static HashSet<int> LoadScanBlockedItemIds()
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

        private static string NormalizeSearchText(string text)
        {
            return text?.Trim() ?? string.Empty;
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
