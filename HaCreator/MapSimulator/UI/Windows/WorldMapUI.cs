using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class WorldMapUI : UIWindowBase
    {
        public enum SearchResultKind
        {
            Field,
            Npc,
            Mob,
            Item
        }

        public sealed class MapEntry
        {
            public int MapId { get; init; }
            public string StreetName { get; init; } = string.Empty;
            public string MapName { get; init; } = string.Empty;
            public string CategoryName { get; init; } = string.Empty;
            public string RegionCode { get; init; } = string.Empty;

            public string DisplayName =>
                !string.IsNullOrWhiteSpace(StreetName) &&
                !string.IsNullOrWhiteSpace(MapName) &&
                !string.Equals(StreetName, MapName, StringComparison.OrdinalIgnoreCase)
                    ? $"{StreetName} : {MapName}"
                    : !string.IsNullOrWhiteSpace(MapName)
                        ? MapName
                        : !string.IsNullOrWhiteSpace(StreetName)
                            ? StreetName
                            : MapId.ToString();
        }

        public sealed class SearchResultEntry
        {
            public SearchResultKind Kind { get; init; }
            public int MapId { get; init; }
            public string Label { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
        }

        private sealed class RegionButtonEntry
        {
            public RegionButtonEntry(string regionCode, UIObject button)
            {
                RegionCode = regionCode ?? string.Empty;
                Button = button;
            }

            public string RegionCode { get; }
            public UIObject Button { get; }
        }

        public readonly struct SearchResultVisualStyle
        {
            public SearchResultVisualStyle(Texture2D hoverTexture, Point hoverOffset, Texture2D iconTexture, Point iconOffset)
            {
                HoverTexture = hoverTexture;
                HoverOffset = hoverOffset;
                IconTexture = iconTexture;
                IconOffset = iconOffset;
            }

            public Texture2D HoverTexture { get; }
            public Point HoverOffset { get; }
            public Texture2D IconTexture { get; }
            public Point IconOffset { get; }
        }

        private enum SearchFilterMode
        {
            All,
            MobOnly
        }

        private const int MaxVisibleRows = 20;
        private const int ListStartX = 523;
        private const int ListStartY = 70;
        private const int ListWidth = 118;
        private const int RowHeight = 18;
        private const int SummaryX = 19;
        private const int SummaryY = 64;
        private const int SummaryWidth = 460;
        private const int SearchInputBoxX = SummaryX;
        private const int SearchInputBoxY = 60;
        private const int SearchInputBoxWidth = 454;
        private const int SearchInputBoxHeight = 21;
        private const int SearchTextInsetX = 6;
        private const int SearchTextInsetY = 3;
        private const int MaxSearchQueryLength = 48;
        private const int KeyRepeatInitialDelayMs = 360;
        private const int KeyRepeatIntervalMs = 42;

        private readonly Texture2D _titleTexture;
        private readonly Texture2D _sidePanelTexture;
        private readonly Point _sidePanelOffset;
        private readonly Texture2D _searchNoticeTexture;
        private readonly Point _searchNoticeOffset;
        private readonly Texture2D _selectionTexture;
        private readonly Texture2D _searchInputBackgroundTexture;
        private readonly Texture2D _searchInputOutlineTexture;
        private readonly Texture2D _caretTexture;
        private readonly UIObject _allButton;
        private readonly UIObject _anotherButton;
        private readonly UIObject _searchButton;
        private readonly UIObject _allSearchButton;
        private readonly UIObject _levelMobButton;
        private readonly UIObject _prevButton;
        private readonly UIObject _nextButton;
        private readonly IReadOnlyDictionary<SearchResultKind, SearchResultVisualStyle> _resultStyles;
        private readonly List<RegionButtonEntry> _regionButtons = new List<RegionButtonEntry>();
        private readonly List<UIObject> _rowButtons = new List<UIObject>();
        private readonly List<MapEntry> _allEntries = new List<MapEntry>();
        private readonly List<SearchResultEntry> _searchResults = new List<SearchResultEntry>();
        private SpriteFont _font;
        private bool _showAnotherWorld;
        private bool _searchMode;
        private string _selectedRegionCode = string.Empty;
        private string _searchQuery = string.Empty;
        private int _searchCursorPosition;
        private KeyboardState _previousSearchKeyboardState;
        private Keys _lastHeldSearchKey = Keys.None;
        private int _keyHoldStartTime = int.MinValue;
        private int _lastKeyRepeatTime = int.MinValue;
        private int _caretBlinkTick;
        private int _currentMapId;
        private int _selectedMapId;
        private string _selectedSearchResultKey = string.Empty;
        private int _pageIndex;
        private SearchFilterMode _searchFilterMode;
        private MouseState _previousMouseState;

        public WorldMapUI(
            IDXObject frame,
            Texture2D titleTexture,
            Texture2D sidePanelTexture,
            Point sidePanelOffset,
            Texture2D searchNoticeTexture,
            Point searchNoticeOffset,
            Texture2D selectionTexture,
            UIObject allButton,
            UIObject anotherButton,
            UIObject searchButton,
            UIObject allSearchButton,
            UIObject levelMobButton,
            UIObject prevButton,
            UIObject nextButton,
            IReadOnlyDictionary<SearchResultKind, SearchResultVisualStyle> resultStyles,
            IEnumerable<(string regionCode, UIObject button)> regionButtons,
            GraphicsDevice device)
            : base(frame)
        {
            _titleTexture = titleTexture;
            _sidePanelTexture = sidePanelTexture;
            _sidePanelOffset = sidePanelOffset;
            _searchNoticeTexture = searchNoticeTexture;
            _searchNoticeOffset = searchNoticeOffset;
            _selectionTexture = selectionTexture;
            _searchInputBackgroundTexture = new Texture2D(device, 1, 1);
            _searchInputBackgroundTexture.SetData(new[] { Color.White });
            _searchInputOutlineTexture = new Texture2D(device, 1, 1);
            _searchInputOutlineTexture.SetData(new[] { Color.White });
            _caretTexture = new Texture2D(device, 1, 1);
            _caretTexture.SetData(new[] { Color.White });
            _allButton = allButton;
            _anotherButton = anotherButton;
            _searchButton = searchButton;
            _allSearchButton = allSearchButton;
            _levelMobButton = levelMobButton;
            _prevButton = prevButton;
            _nextButton = nextButton;
            _resultStyles = resultStyles ?? new Dictionary<SearchResultKind, SearchResultVisualStyle>();

            if (_allButton != null)
            {
                _allButton.ButtonClickReleased += _ => SetViewMode(false);
                AddButton(_allButton);
            }

            if (_anotherButton != null)
            {
                _anotherButton.ButtonClickReleased += _ => SetViewMode(true);
                AddButton(_anotherButton);
            }

            if (_searchButton != null)
            {
                _searchButton.ButtonClickReleased += _ => ToggleSearchMode();
                AddButton(_searchButton);
            }

            if (_allSearchButton != null)
            {
                _allSearchButton.ButtonClickReleased += _ => ExitSearchMode();
                AddButton(_allSearchButton);
            }

            if (_levelMobButton != null)
            {
                _levelMobButton.ButtonClickReleased += _ => ToggleMobSearchFilter();
                AddButton(_levelMobButton);
            }

            if (_prevButton != null)
            {
                _prevButton.ButtonClickReleased += _ =>
                {
                    if (_pageIndex > 0)
                    {
                        _pageIndex--;
                    }
                    UpdateButtonStates();
                };
                AddButton(_prevButton);
            }

            if (_nextButton != null)
            {
                _nextButton.ButtonClickReleased += _ =>
                {
                    if (_pageIndex < GetMaxPageIndexForCurrentMode())
                    {
                        _pageIndex++;
                    }
                    UpdateButtonStates();
                };
                AddButton(_nextButton);
            }

            foreach ((string regionCode, UIObject button) in regionButtons ?? Enumerable.Empty<(string, UIObject)>())
            {
                if (button == null)
                {
                    continue;
                }

                string capturedRegionCode = regionCode ?? string.Empty;
                button.ButtonClickReleased += _ => SelectRegion(capturedRegionCode);
                AddButton(button);
                _regionButtons.Add(new RegionButtonEntry(capturedRegionCode, button));
            }

            for (int row = 0; row < MaxVisibleRows; row++)
            {
                UIObject rowButton = CreateRowButton(device);
                rowButton.X = ListStartX;
                rowButton.Y = ListStartY + (row * RowHeight);
                int capturedRow = row;
                rowButton.ButtonClickReleased += _ => ActivateRow(capturedRow);
                AddButton(rowButton);
                _rowButtons.Add(rowButton);
            }

            UpdateButtonStates();
        }

        public WorldMapUI(
            IDXObject frame,
            Texture2D titleTexture,
            Texture2D sidePanelTexture,
            Point sidePanelOffset,
            Texture2D searchNoticeTexture,
            Point searchNoticeOffset,
            Texture2D selectionTexture,
            UIObject allButton,
            UIObject anotherButton,
            UIObject searchButton,
            UIObject allSearchButton,
            UIObject levelMobButton,
            UIObject prevButton,
            UIObject nextButton,
            Texture2D resultFieldHoverTexture,
            Texture2D resultFieldIconTexture,
            Texture2D resultNpcHoverTexture,
            Texture2D resultNpcIconTexture,
            Texture2D resultMobHoverTexture,
            Texture2D resultMobIconTexture,
            IEnumerable<(string regionCode, UIObject button)> regionButtons,
            GraphicsDevice device)
            : this(
                frame,
                titleTexture,
                sidePanelTexture,
                sidePanelOffset,
                searchNoticeTexture,
                searchNoticeOffset,
                selectionTexture,
                allButton,
                anotherButton,
                searchButton,
                allSearchButton,
                levelMobButton,
                prevButton,
                nextButton,
                BuildLegacyResultStyles(
                    resultFieldHoverTexture,
                    resultFieldIconTexture,
                    resultNpcHoverTexture,
                    resultNpcIconTexture,
                    resultMobHoverTexture,
                    resultMobIconTexture),
                regionButtons,
                device)
        {
        }

        public override string WindowName => MapSimulatorWindowNames.WorldMap;
        public override bool CapturesKeyboardInput => IsVisible && _searchMode;

        public event Action<MapEntry> MapRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetEntries(IReadOnlyList<MapEntry> entries, int currentMapId, int? focusedMapId = null)
        {
            _allEntries.Clear();
            if (entries != null)
            {
                _allEntries.AddRange(entries
                    .Where(entry => entry != null)
                    .OrderBy(entry => entry.MapId));
            }

            _currentMapId = currentMapId;
            _selectedMapId = focusedMapId.GetValueOrDefault(currentMapId);

            string currentRegionCode = GetRegionCodeForMapId(currentMapId);
            string selectedRegionCode = GetRegionCodeForMapId(_selectedMapId);
            _selectedRegionCode = selectedRegionCode;
            if (!_regionButtons.Any(entry => entry.RegionCode == _selectedRegionCode))
            {
                _selectedRegionCode = _regionButtons.Any(entry => entry.RegionCode == currentRegionCode)
                    ? currentRegionCode
                    : _regionButtons.FirstOrDefault()?.RegionCode ?? string.Empty;
            }

            _showAnotherWorld = focusedMapId.HasValue
                && !string.IsNullOrWhiteSpace(selectedRegionCode)
                && !string.Equals(selectedRegionCode, currentRegionCode, StringComparison.Ordinal);
            _selectedSearchResultKey = string.Empty;
            EnsureSelectedEntryVisible();
            UpdateButtonStates();
        }

        public void SetSearchResults(IReadOnlyList<SearchResultEntry> searchResults)
        {
            string previousSelectedKey = _selectedSearchResultKey;
            _searchResults.Clear();
            if (searchResults != null)
            {
                _searchResults.AddRange(searchResults.Where(entry => entry != null));
            }

            if (_searchResults.Count == 0 && _allEntries.Count == 0)
            {
                _searchMode = false;
                _searchFilterMode = SearchFilterMode.All;
                ClearSearchQuery();
            }

            IReadOnlyList<SearchResultEntry> resolvedResults = GetFilteredSearchResults();
            _selectedSearchResultKey = resolvedResults.Any(entry => string.Equals(BuildSearchResultKey(entry), previousSelectedKey, StringComparison.OrdinalIgnoreCase))
                ? previousSelectedKey
                : resolvedResults.FirstOrDefault() is SearchResultEntry firstEntry
                    ? BuildSearchResultKey(firstEntry)
                    : string.Empty;
            _pageIndex = 0;
            UpdateButtonStates();
        }

        public bool FocusSearchResult(SearchResultKind kind, string label, int mapId, bool enterSearchMode = true)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            _searchFilterMode = SearchFilterMode.All;
            if (enterSearchMode)
            {
                _searchMode = true;
            }

            _searchQuery = label.Trim();
            _searchCursorPosition = _searchQuery.Length;
            _selectedMapId = mapId;

            IReadOnlyList<SearchResultEntry> results = GetFilteredSearchResults();
            SearchResultEntry selectedEntry = results.FirstOrDefault(entry =>
                entry.Kind == kind &&
                entry.MapId == mapId &&
                string.Equals(entry.Label, _searchQuery, StringComparison.OrdinalIgnoreCase));
            if (selectedEntry == null)
            {
                selectedEntry = results.FirstOrDefault(entry =>
                    entry.Kind == kind &&
                    string.Equals(entry.Label, _searchQuery, StringComparison.OrdinalIgnoreCase));
            }

            _selectedSearchResultKey = selectedEntry != null
                ? BuildSearchResultKey(selectedEntry)
                : string.Empty;
            EnsureSelectedSearchResultVisible();
            UpdateButtonStates();
            return selectedEntry != null;
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible || !_searchMode)
            {
                _previousSearchKeyboardState = Keyboard.GetState();
                _previousMouseState = Mouse.GetState();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            int tickCount = Environment.TickCount;

            HandleSearchMouseInput(mouseState);
            HandleSearchKeyboardInput(keyboardState, tickCount);
            _previousSearchKeyboardState = keyboardState;
            _previousMouseState = mouseState;
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
            if (_sidePanelTexture != null)
            {
                sprite.Draw(_sidePanelTexture, new Vector2(Position.X + _sidePanelOffset.X, Position.Y + _sidePanelOffset.Y), Color.White);
            }

            if (_font == null)
            {
                return;
            }

            if (_searchMode)
            {
                DrawSearchContents(sprite);
                return;
            }

            IReadOnlyList<MapEntry> visibleEntries = GetVisibleEntries();
            int selectedIndex = GetSelectedIndex(visibleEntries);
            if (_selectionTexture != null && selectedIndex >= 0)
            {
                sprite.Draw(
                    _selectionTexture,
                    new Rectangle(Position.X + ListStartX, Position.Y + ListStartY + (selectedIndex * RowHeight), ListWidth, RowHeight),
                    new Color(86, 120, 186, 165));
            }

            DrawTitle(sprite, _showAnotherWorld ? "World Map - Another World" : "World Map - All Regions");

            string currentMapText = ResolveCurrentMapText();
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                TrimToWidth($"Current: {currentMapText}", SummaryWidth),
                new Vector2(Position.X + SummaryX, Position.Y + 38),
                new Color(220, 220, 220));

            float textY = Position.Y + SummaryY;
            foreach (string line in WrapText(BuildSummaryText(), SummaryWidth))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(Position.X + SummaryX, textY),
                    new Color(226, 226, 226));
                textY += _font.LineSpacing;
            }

            for (int row = 0; row < visibleEntries.Count; row++)
            {
                MapEntry entry = visibleEntries[row];
                Color textColor = entry.MapId == _selectedMapId ? Color.White : new Color(228, 228, 228);
                string label = TrimToWidth($"{entry.MapId} {entry.DisplayName}", ListWidth - 4);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    label,
                    new Vector2(Position.X + ListStartX + 2, Position.Y + ListStartY + 1 + (row * RowHeight)),
                    textColor);
            }

            string pageText = $"{_pageIndex + 1}/{Math.Max(1, GetMaxPageIndex() + 1)}";
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                pageText,
                new Vector2(Position.X + 586, Position.Y + 511),
                new Color(214, 214, 214));
        }

        private void SetViewMode(bool anotherWorld)
        {
            _showAnotherWorld = anotherWorld;
            _searchMode = false;
            _pageIndex = 0;
            EnsureSelectedEntryVisible();
            UpdateButtonStates();
        }

        private void SelectRegion(string regionCode)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return;
            }

            _showAnotherWorld = true;
            _searchMode = false;
            _selectedRegionCode = regionCode;
            _pageIndex = 0;
            EnsureSelectedEntryVisible();
            UpdateButtonStates();
        }

        private void FocusCurrentMap()
        {
            if (_currentMapId <= 0)
            {
                return;
            }

            _selectedMapId = _currentMapId;
            _selectedRegionCode = GetRegionCodeForMapId(_currentMapId);
            _showAnotherWorld = !string.IsNullOrWhiteSpace(_selectedRegionCode);
            _searchMode = false;
            EnsureSelectedEntryVisible();
            UpdateButtonStates();
        }

        private void ActivateRow(int rowIndex)
        {
            if (_searchMode)
            {
                ActivateSearchRow(rowIndex);
                return;
            }

            IReadOnlyList<MapEntry> visibleEntries = GetVisibleEntries();
            if (rowIndex < 0 || rowIndex >= visibleEntries.Count)
            {
                return;
            }

            MapEntry entry = visibleEntries[rowIndex];
            if (entry.MapId == _selectedMapId)
            {
                MapRequested?.Invoke(entry);
                return;
            }

            _selectedMapId = entry.MapId;
            UpdateButtonStates();
        }

        private void ActivateSearchRow(int rowIndex)
        {
            IReadOnlyList<SearchResultEntry> visibleResults = GetVisibleSearchResults();
            if (rowIndex < 0 || rowIndex >= visibleResults.Count)
            {
                return;
            }

            SearchResultEntry entry = visibleResults[rowIndex];
            if (entry.MapId <= 0)
            {
                return;
            }

            string entryKey = BuildSearchResultKey(entry);
            if (string.Equals(_selectedSearchResultKey, entryKey, StringComparison.OrdinalIgnoreCase))
            {
                MapEntry mapEntry = _allEntries.FirstOrDefault(candidate => candidate.MapId == entry.MapId);
                if (mapEntry != null)
                {
                    MapRequested?.Invoke(mapEntry);
                }

                return;
            }

            _selectedMapId = entry.MapId;
            _selectedSearchResultKey = entryKey;
            _selectedRegionCode = GetRegionCodeForMapId(entry.MapId);
            UpdateButtonStates();
        }

        private void EnsureSelectedEntryVisible()
        {
            List<MapEntry> filteredEntries = GetFilteredEntries();
            if (filteredEntries.Count == 0)
            {
                _pageIndex = 0;
                return;
            }

            int selectedIndex = filteredEntries.FindIndex(entry => entry.MapId == _selectedMapId);
            if (selectedIndex < 0)
            {
                MapEntry currentEntry = filteredEntries.FirstOrDefault(entry => entry.MapId == _currentMapId);
                if (currentEntry != null)
                {
                    _selectedMapId = currentEntry.MapId;
                    selectedIndex = filteredEntries.FindIndex(entry => entry.MapId == _selectedMapId);
                }
                else
                {
                    _selectedMapId = filteredEntries[0].MapId;
                    selectedIndex = 0;
                }
            }

            _pageIndex = Math.Clamp(selectedIndex / MaxVisibleRows, 0, GetMaxPageIndex(filteredEntries.Count));
        }

        private IReadOnlyList<MapEntry> GetVisibleEntries()
        {
            List<MapEntry> filteredEntries = GetFilteredEntries();
            return filteredEntries
                .Skip(_pageIndex * MaxVisibleRows)
                .Take(MaxVisibleRows)
                .ToArray();
        }

        private List<MapEntry> GetFilteredEntries()
        {
            if (!_showAnotherWorld || string.IsNullOrWhiteSpace(_selectedRegionCode))
            {
                return _allEntries;
            }

            return _allEntries
                .Where(entry => string.Equals(entry.RegionCode, _selectedRegionCode, StringComparison.Ordinal))
                .ToList();
        }

        private IReadOnlyList<SearchResultEntry> GetVisibleSearchResults()
        {
            IReadOnlyList<SearchResultEntry> filteredResults = GetFilteredSearchResults();
            return filteredResults
                .Skip(_pageIndex * MaxVisibleRows)
                .Take(MaxVisibleRows)
                .ToArray();
        }

        private IReadOnlyList<SearchResultEntry> GetFilteredSearchResults()
        {
            IEnumerable<SearchResultEntry> results = BuildResolvedSearchResults();
            if (_searchFilterMode == SearchFilterMode.MobOnly)
            {
                results = results.Where(entry => entry.Kind == SearchResultKind.Mob);
            }

            return results
                .ToArray();
        }

        private int GetSelectedIndex(IReadOnlyList<MapEntry> visibleEntries)
        {
            for (int i = 0; i < visibleEntries.Count; i++)
            {
                if (visibleEntries[i].MapId == _selectedMapId)
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateButtonStates()
        {
            if (_allButton != null)
            {
                _allButton.SetButtonState(_showAnotherWorld ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_anotherButton != null)
            {
                _anotherButton.SetButtonState(_showAnotherWorld ? UIObjectState.Disabled : UIObjectState.Normal);
            }

            if (_searchButton != null)
            {
                bool hasSearchSurface = _searchResults.Count > 0 || _allEntries.Count > 0;
                _searchButton.SetEnabled(hasSearchSurface);
                if (hasSearchSurface)
                {
                    _searchButton.SetButtonState(_searchMode ? UIObjectState.Disabled : UIObjectState.Normal);
                }
            }

            if (_allSearchButton != null)
            {
                _allSearchButton.SetVisible(_searchMode);
                _allSearchButton.SetEnabled(_searchMode);
            }

            if (_levelMobButton != null)
            {
                bool hasMobResults = _searchResults.Any(entry => entry.Kind == SearchResultKind.Mob);
                _levelMobButton.SetVisible(_searchMode && hasMobResults);
                _levelMobButton.SetEnabled(hasMobResults);
                if (hasMobResults)
                {
                    _levelMobButton.SetButtonState(
                        _searchMode && _searchFilterMode == SearchFilterMode.MobOnly
                            ? UIObjectState.Disabled
                            : UIObjectState.Normal);
                }
            }

            int maxPageIndex = GetMaxPageIndexForCurrentMode();
            if (_prevButton != null)
            {
                _prevButton.SetEnabled(_pageIndex > 0);
            }

            if (_nextButton != null)
            {
                _nextButton.SetEnabled(_pageIndex < maxPageIndex);
            }

            HashSet<string> activeRegions = _allEntries
                .Select(entry => entry.RegionCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.Ordinal);

            foreach (RegionButtonEntry regionButton in _regionButtons)
            {
                bool regionVisible = _showAnotherWorld && !_searchMode;
                regionButton.Button.SetVisible(regionVisible);
                if (!regionVisible)
                {
                    continue;
                }

                bool hasEntries = activeRegions.Contains(regionButton.RegionCode);
                regionButton.Button.SetEnabled(hasEntries);
                regionButton.Button.SetButtonState(
                    hasEntries && string.Equals(regionButton.RegionCode, _selectedRegionCode, StringComparison.Ordinal)
                        ? UIObjectState.Disabled
                        : UIObjectState.Normal);
            }

            int visibleCount = _searchMode ? GetVisibleSearchResults().Count : GetVisibleEntries().Count;
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool visible = i < visibleCount;
                _rowButtons[i].SetVisible(visible);
            }
        }

        private string ResolveCurrentMapText()
        {
            MapEntry entry = _allEntries.FirstOrDefault(candidate => candidate.MapId == _currentMapId);
            return entry?.DisplayName ?? (_currentMapId > 0 ? _currentMapId.ToString() : "Unknown");
        }

        private string BuildSummaryText()
        {
            MapEntry selectedEntry = _allEntries.FirstOrDefault(candidate => candidate.MapId == _selectedMapId);
            if (selectedEntry == null)
            {
                return "Select a region or a map entry from the list at the right.";
            }

            string regionText = string.IsNullOrWhiteSpace(selectedEntry.RegionCode)
                ? "Unknown region"
                : $"Region code {selectedEntry.RegionCode}";
            return $"Selected: {selectedEntry.DisplayName}\nCategory: {selectedEntry.CategoryName}\n{regionText}\nClick the selected row again to transfer through the world-map flow.";
        }

        private string BuildSearchSummaryText()
        {
            IReadOnlyList<SearchResultEntry> resolvedResults = BuildResolvedSearchResults();
            int fieldCount = resolvedResults.Count(entry => entry.Kind == SearchResultKind.Field);
            int npcCount = resolvedResults.Count(entry => entry.Kind == SearchResultKind.Npc);
            int mobCount = resolvedResults.Count(entry => entry.Kind == SearchResultKind.Mob);
            int itemCount = resolvedResults.Count(entry => entry.Kind == SearchResultKind.Item);
            string filterText = _searchFilterMode == SearchFilterMode.MobOnly ? "Mob-only filter active." : "All live result families are visible.";
            string queryText = string.IsNullOrWhiteSpace(_searchQuery)
                ? "Type a field, NPC, mob, or map id query."
                : $"Query: {_searchQuery}";
            return $"Current field search surface.\nFields: {fieldCount}  NPCs: {npcCount}  Mobs: {mobCount}  Quest items: {itemCount}\n{filterText}\n{queryText}\nClick a result row to focus its map, then click again to queue transfer.";
        }

        private int GetMaxPageIndex()
        {
            return GetMaxPageIndex(GetFilteredEntries().Count);
        }

        private int GetMaxPageIndexForCurrentMode()
        {
            return _searchMode
                ? GetMaxPageIndex(GetFilteredSearchResults().Count)
                : GetMaxPageIndex(GetFilteredEntries().Count);
        }

        private static int GetMaxPageIndex(int count)
        {
            return Math.Max(0, (int)Math.Ceiling(count / (double)MaxVisibleRows) - 1);
        }

        private static UIObject CreateRowButton(GraphicsDevice device)
        {
            Texture2D texture = new Texture2D(device, ListWidth, RowHeight);
            Color[] pixels = new Color[ListWidth * RowHeight];
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

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            foreach (string rawLine in text.Replace("\r", string.Empty).Split('\n'))
            {
                string[] words = rawLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    yield return string.Empty;
                    continue;
                }

                string currentLine = string.Empty;
                foreach (string word in words)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
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

        public static string GetRegionCodeForMapId(int mapId)
        {
            return mapId <= 0
                ? string.Empty
                : (mapId / 10000000).ToString("D3");
        }

        private void ToggleSearchMode()
        {
            if (_searchResults.Count == 0 && _allEntries.Count == 0)
            {
                return;
            }

            _searchMode = true;
            EnsureSelectedSearchResultVisible();
            UpdateButtonStates();
        }

        private void ExitSearchMode()
        {
            _searchMode = false;
            _searchFilterMode = SearchFilterMode.All;
            _pageIndex = 0;
            UpdateButtonStates();
        }

        private void ToggleMobSearchFilter()
        {
            if (_searchResults.All(entry => entry.Kind != SearchResultKind.Mob))
            {
                return;
            }

            _searchMode = true;
            _searchFilterMode = _searchFilterMode == SearchFilterMode.MobOnly
                ? SearchFilterMode.All
                : SearchFilterMode.MobOnly;
            EnsureSelectedSearchResultVisible();
            UpdateButtonStates();
        }

        private void DrawSearchContents(SpriteBatch sprite)
        {
            if (_searchNoticeTexture != null)
            {
                sprite.Draw(_searchNoticeTexture, new Vector2(Position.X + _searchNoticeOffset.X, Position.Y + _searchNoticeOffset.Y), Color.White);
            }

            DrawTitle(sprite, "World Map Search");

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                TrimToWidth($"Current: {ResolveCurrentMapText()}", SummaryWidth),
                new Vector2(Position.X + SummaryX, Position.Y + 38),
                new Color(220, 220, 220));

            DrawSearchInput(sprite, Environment.TickCount);

            float textY = Position.Y + SummaryY + 26;
            foreach (string line in WrapText(BuildSearchSummaryText(), SummaryWidth))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    line,
                    new Vector2(Position.X + SummaryX, textY),
                    new Color(226, 226, 226));
                textY += _font.LineSpacing;
            }

            IReadOnlyList<SearchResultEntry> visibleResults = GetVisibleSearchResults();
            if (visibleResults.Count == 0)
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    "No matching results.",
                    new Vector2(Position.X + ListStartX + 2, Position.Y + ListStartY + 1),
                    new Color(206, 206, 206));
            }

            for (int row = 0; row < visibleResults.Count; row++)
            {
                DrawSearchRow(sprite, visibleResults[row], row);
            }

            string pageText = $"{_pageIndex + 1}/{Math.Max(1, GetMaxPageIndexForCurrentMode() + 1)}";
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                pageText,
                new Vector2(Position.X + 586, Position.Y + 511),
                new Color(214, 214, 214));
        }

        private void DrawSearchRow(SpriteBatch sprite, SearchResultEntry entry, int row)
        {
            Texture2D hoverTexture = GetHoverTexture(entry.Kind);
            Rectangle rowBounds = new Rectangle(Position.X + ListStartX, Position.Y + ListStartY + (row * RowHeight), ListWidth, RowHeight);
            if (IsSearchResultSelected(entry))
            {
                if (hoverTexture != null)
                {
                    sprite.Draw(hoverTexture, rowBounds, Color.White);
                }
                else if (_selectionTexture != null)
                {
                    sprite.Draw(_selectionTexture, rowBounds, new Color(86, 120, 186, 165));
                }
            }

            Texture2D iconTexture = GetIconTexture(entry.Kind);
            int textStartX = rowBounds.X + 2;
            if (iconTexture != null)
            {
                sprite.Draw(iconTexture, new Vector2(rowBounds.X + 2, rowBounds.Y + 1), Color.White);
                textStartX += 15;
            }

            string label = TrimToWidth(entry.Label, ListWidth - (textStartX - rowBounds.X) - 2);
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                label,
                new Vector2(textStartX, rowBounds.Y + 1),
                IsSearchResultSelected(entry) ? Color.White : new Color(228, 228, 228));
        }

        private Texture2D GetHoverTexture(SearchResultKind kind)
        {
            if (_resultStyles.TryGetValue(kind, out SearchResultVisualStyle style))
            {
                return style.HoverTexture;
            }

            return kind == SearchResultKind.Item
                ? GetHoverTexture(SearchResultKind.Field) ?? GetHoverTexture(SearchResultKind.Npc)
                : null;
        }

        private Texture2D GetIconTexture(SearchResultKind kind)
        {
            if (_resultStyles.TryGetValue(kind, out SearchResultVisualStyle style))
            {
                return style.IconTexture;
            }

            return kind == SearchResultKind.Item
                ? GetIconTexture(SearchResultKind.Npc) ?? GetIconTexture(SearchResultKind.Field)
                : null;
        }

        private static IReadOnlyDictionary<SearchResultKind, SearchResultVisualStyle> BuildLegacyResultStyles(
            Texture2D resultFieldHoverTexture,
            Texture2D resultFieldIconTexture,
            Texture2D resultNpcHoverTexture,
            Texture2D resultNpcIconTexture,
            Texture2D resultMobHoverTexture,
            Texture2D resultMobIconTexture)
        {
            var styles = new Dictionary<SearchResultKind, SearchResultVisualStyle>
            {
                [SearchResultKind.Field] = new(resultFieldHoverTexture, Point.Zero, resultFieldIconTexture, Point.Zero),
                [SearchResultKind.Npc] = new(resultNpcHoverTexture, Point.Zero, resultNpcIconTexture, Point.Zero),
                [SearchResultKind.Mob] = new(resultMobHoverTexture, Point.Zero, resultMobIconTexture, Point.Zero)
            };
            return styles;
        }

        private void DrawTitle(SpriteBatch sprite, string fallbackText)
        {
            if (_titleTexture != null)
            {
                sprite.Draw(_titleTexture, new Vector2(Position.X + SummaryX, Position.Y + 18), Color.White);
                return;
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                fallbackText,
                new Vector2(Position.X + SummaryX, Position.Y + 18),
                Color.White);
        }

        private Rectangle GetSearchInputBounds()
        {
            return new Rectangle(
                Position.X + SearchInputBoxX,
                Position.Y + SearchInputBoxY,
                SearchInputBoxWidth,
                SearchInputBoxHeight);
        }

        private void DrawSearchInput(SpriteBatch sprite, int tickCount)
        {
            Rectangle bounds = GetSearchInputBounds();

            sprite.Draw(_searchInputBackgroundTexture, bounds, new Color(16, 27, 43, 215));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(117, 155, 220));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(117, 155, 220));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(117, 155, 220));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(117, 155, 220));

            if (string.IsNullOrEmpty(_searchQuery))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    "Type to search field, NPC, or mob names",
                    new Vector2(bounds.X + SearchTextInsetX, bounds.Y + SearchTextInsetY),
                    new Color(166, 184, 206));
                return;
            }

            (int visibleStart, string visibleText) = GetVisibleSearchText();
            Vector2 textPosition = new Vector2(bounds.X + SearchTextInsetX, bounds.Y + SearchTextInsetY);
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, visibleText, textPosition, new Color(236, 236, 236));

            if (((tickCount - _caretBlinkTick) / 500) % 2 != 0)
            {
                return;
            }

            int visibleCaretIndex = Math.Clamp(_searchCursorPosition - visibleStart, 0, visibleText.Length);
            string caretPrefix = visibleCaretIndex <= 0
                ? string.Empty
                : visibleText[..visibleCaretIndex];
            int caretX = bounds.X + SearchTextInsetX + (int)_font.MeasureString(caretPrefix).X;
            int caretY = bounds.Y + 3;
            sprite.Draw(_caretTexture, new Rectangle(caretX, caretY, 1, Math.Max(12, _font.LineSpacing - 2)), new Color(255, 255, 255, 220));
        }

        private void HandleSearchKeyboardInput(KeyboardState keyboardState, int tickCount)
        {
            if (WasPressed(keyboardState, Keys.Escape))
            {
                if (!string.IsNullOrEmpty(_searchQuery))
                {
                    ClearSearchQuery();
                    EnsureSelectedSearchResultVisible();
                    UpdateButtonStates();
                }
                else
                {
                    ExitSearchMode();
                }

                return;
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
                ActivateSearchRow(GetSelectedSearchRowIndex());
                return;
            }

            if (WasPressed(keyboardState, Keys.Up))
            {
                MoveSearchSelection(-1);
                return;
            }

            if (WasPressed(keyboardState, Keys.Down))
            {
                MoveSearchSelection(1);
                return;
            }

            if (WasPressed(keyboardState, Keys.PageUp))
            {
                if (_pageIndex > 0)
                {
                    _pageIndex--;
                    EnsureSelectedSearchResultVisible();
                    UpdateButtonStates();
                }

                return;
            }

            if (WasPressed(keyboardState, Keys.PageDown))
            {
                int maxPageIndex = GetMaxPageIndexForCurrentMode();
                if (_pageIndex < maxPageIndex)
                {
                    _pageIndex++;
                    EnsureSelectedSearchResultVisible();
                    UpdateButtonStates();
                }

                return;
            }

            if (HandleSearchEditingKeys(keyboardState, tickCount))
            {
                return;
            }

            bool ctrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            if (ctrl && WasPressed(keyboardState, Keys.V))
            {
                HandleSearchClipboardPaste();
                return;
            }

            bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
            foreach (Keys key in keyboardState.GetPressedKeys())
            {
                if (ctrl)
                {
                    continue;
                }

                if (_previousSearchKeyboardState.IsKeyDown(key) || !TryInsertSearchCharacter(key, shift, tickCount))
                {
                    continue;
                }

                return;
            }

            if (_lastHeldSearchKey != Keys.None
                && keyboardState.IsKeyDown(_lastHeldSearchKey)
                && ShouldRepeatHeldKey(tickCount))
            {
                if (_lastHeldSearchKey == Keys.Back)
                {
                    if (_searchCursorPosition > 0)
                    {
                        _searchQuery = _searchQuery.Remove(_searchCursorPosition - 1, 1);
                        _searchCursorPosition--;
                        OnSearchQueryChanged();
                    }
                }
                else if (_lastHeldSearchKey == Keys.Delete)
                {
                    if (_searchCursorPosition < _searchQuery.Length)
                    {
                        _searchQuery = _searchQuery.Remove(_searchCursorPosition, 1);
                        OnSearchQueryChanged();
                    }
                }
                else if (_lastHeldSearchKey == Keys.Left)
                {
                    _searchCursorPosition = Math.Max(0, _searchCursorPosition - 1);
                }
                else if (_lastHeldSearchKey == Keys.Right)
                {
                    _searchCursorPosition = Math.Min(_searchQuery.Length, _searchCursorPosition + 1);
                }
                else
                {
                    TryInsertSearchCharacter(_lastHeldSearchKey, shift, tickCount);
                }

                _lastKeyRepeatTime = tickCount;
            }
            else if (_lastHeldSearchKey != Keys.None && !keyboardState.IsKeyDown(_lastHeldSearchKey))
            {
                ResetSearchKeyRepeat();
            }
        }

        private bool HandleSearchEditingKeys(KeyboardState keyboardState, int tickCount)
        {
            bool ctrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);

            if (keyboardState.IsKeyDown(Keys.Back))
            {
                if (_previousSearchKeyboardState.IsKeyUp(Keys.Back))
                {
                    if (ctrl)
                    {
                        DeletePreviousSearchWord();
                    }
                    else if (_searchCursorPosition > 0)
                    {
                        _searchQuery = _searchQuery.Remove(_searchCursorPosition - 1, 1);
                        _searchCursorPosition--;
                        OnSearchQueryChanged();
                    }

                    BeginHeldKey(Keys.Back, tickCount);
                }

                return true;
            }

            if (keyboardState.IsKeyDown(Keys.Delete))
            {
                if (_previousSearchKeyboardState.IsKeyUp(Keys.Delete))
                {
                    if (ctrl)
                    {
                        DeleteNextSearchWord();
                    }
                    else if (_searchCursorPosition < _searchQuery.Length)
                    {
                        _searchQuery = _searchQuery.Remove(_searchCursorPosition, 1);
                        OnSearchQueryChanged();
                    }

                    BeginHeldKey(Keys.Delete, tickCount);
                }

                return true;
            }

            if (keyboardState.IsKeyDown(Keys.Left))
            {
                if (_previousSearchKeyboardState.IsKeyUp(Keys.Left))
                {
                    _searchCursorPosition = Math.Max(0, _searchCursorPosition - 1);
                    BeginHeldKey(Keys.Left, tickCount);
                }

                return true;
            }

            if (keyboardState.IsKeyDown(Keys.Right))
            {
                if (_previousSearchKeyboardState.IsKeyUp(Keys.Right))
                {
                    _searchCursorPosition = Math.Min(_searchQuery.Length, _searchCursorPosition + 1);
                    BeginHeldKey(Keys.Right, tickCount);
                }

                return true;
            }

            if (WasPressed(keyboardState, Keys.Home))
            {
                _searchCursorPosition = 0;
                return true;
            }

            if (WasPressed(keyboardState, Keys.End))
            {
                _searchCursorPosition = _searchQuery.Length;
                return true;
            }

            return false;
        }

        private void DeletePreviousSearchWord()
        {
            if (_searchCursorPosition <= 0 || string.IsNullOrEmpty(_searchQuery))
            {
                return;
            }

            int removalStart = _searchCursorPosition;
            while (removalStart > 0 && char.IsWhiteSpace(_searchQuery[removalStart - 1]))
            {
                removalStart--;
            }

            while (removalStart > 0 && !char.IsWhiteSpace(_searchQuery[removalStart - 1]))
            {
                removalStart--;
            }

            int removalLength = _searchCursorPosition - removalStart;
            if (removalLength <= 0)
            {
                return;
            }

            _searchQuery = _searchQuery.Remove(removalStart, removalLength);
            _searchCursorPosition = removalStart;
            OnSearchQueryChanged();
        }

        private void DeleteNextSearchWord()
        {
            if (_searchCursorPosition >= _searchQuery.Length || string.IsNullOrEmpty(_searchQuery))
            {
                return;
            }

            int removalEnd = _searchCursorPosition;
            while (removalEnd < _searchQuery.Length && char.IsWhiteSpace(_searchQuery[removalEnd]))
            {
                removalEnd++;
            }

            while (removalEnd < _searchQuery.Length && !char.IsWhiteSpace(_searchQuery[removalEnd]))
            {
                removalEnd++;
            }

            int removalLength = removalEnd - _searchCursorPosition;
            if (removalLength <= 0)
            {
                return;
            }

            _searchQuery = _searchQuery.Remove(_searchCursorPosition, removalLength);
            OnSearchQueryChanged();
        }

        private bool TryInsertSearchCharacter(Keys key, bool shift, int tickCount)
        {
            char? character = KeyToChar(key, shift);
            if (!character.HasValue || _searchQuery.Length >= MaxSearchQueryLength)
            {
                return false;
            }

            _searchQuery = _searchQuery.Insert(_searchCursorPosition, character.Value.ToString());
            _searchCursorPosition++;
            BeginHeldKey(key, tickCount);
            OnSearchQueryChanged();
            return true;
        }

        private void OnSearchQueryChanged()
        {
            _caretBlinkTick = Environment.TickCount;
            EnsureSelectedSearchResultVisible();
            UpdateButtonStates();
        }

        private void MoveSearchSelection(int direction)
        {
            IReadOnlyList<SearchResultEntry> results = GetFilteredSearchResults();
            if (results.Count == 0)
            {
                return;
            }

            int currentIndex = GetSelectedSearchIndex(results);
            int nextIndex = currentIndex < 0
                ? 0
                : Math.Clamp(currentIndex + direction, 0, results.Count - 1);
            SearchResultEntry selectedEntry = results[nextIndex];
            _selectedMapId = selectedEntry.MapId;
            _selectedSearchResultKey = BuildSearchResultKey(selectedEntry);
            _pageIndex = nextIndex / MaxVisibleRows;
            UpdateButtonStates();
        }

        private int GetSelectedSearchRowIndex()
        {
            IReadOnlyList<SearchResultEntry> visibleResults = GetVisibleSearchResults();
            for (int i = 0; i < visibleResults.Count; i++)
            {
                if (IsSearchResultSelected(visibleResults[i]))
                {
                    return i;
                }
            }

            return visibleResults.Count > 0 ? 0 : -1;
        }

        private int GetSelectedSearchIndex(IReadOnlyList<SearchResultEntry> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (IsSearchResultSelected(results[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureSelectedSearchResultVisible()
        {
            IReadOnlyList<SearchResultEntry> filteredResults = GetFilteredSearchResults();
            if (filteredResults.Count == 0)
            {
                _pageIndex = 0;
                return;
            }

            int selectedIndex = GetSelectedSearchIndex(filteredResults);
            if (selectedIndex < 0)
            {
                SearchResultEntry firstResult = filteredResults[0];
                _selectedMapId = firstResult.MapId;
                _selectedSearchResultKey = BuildSearchResultKey(firstResult);
                selectedIndex = 0;
            }

            _pageIndex = Math.Clamp(selectedIndex / MaxVisibleRows, 0, GetMaxPageIndex(filteredResults.Count));
        }

        private IReadOnlyList<SearchResultEntry> BuildResolvedSearchResults()
        {
            List<SearchResultEntry> results = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            string query = _searchQuery.Trim();

            foreach (SearchResultEntry result in _searchResults)
            {
                if (result == null || !MatchesSearchQuery(result, query))
                {
                    continue;
                }

                if (seen.Add(BuildSearchResultKey(result)))
                {
                    results.Add(result);
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            foreach (MapEntry entry in _allEntries
                .Where(entry => entry != null && MatchesSearchQuery(entry, query))
                .OrderByDescending(entry => ScoreMapEntryMatch(entry, query))
                .ThenBy(entry => entry.MapId))
            {
                SearchResultEntry fieldResult = new SearchResultEntry
                {
                    Kind = SearchResultKind.Field,
                    MapId = entry.MapId,
                    Label = entry.DisplayName,
                    Description = string.IsNullOrWhiteSpace(entry.CategoryName)
                        ? $"Field {entry.MapId}"
                        : $"{entry.CategoryName} ({entry.MapId})"
                };

                if (seen.Add(BuildSearchResultKey(fieldResult)))
                {
                    results.Add(fieldResult);
                }
            }

            return results;
        }

        private static string BuildSearchResultKey(SearchResultEntry entry)
        {
            return $"{entry.Kind}:{entry.MapId}:{entry.Label}";
        }

        private bool IsSearchResultSelected(SearchResultEntry entry)
        {
            return entry != null
                && string.Equals(BuildSearchResultKey(entry), _selectedSearchResultKey, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesSearchQuery(SearchResultEntry entry, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return ContainsToken(entry.Label, query)
                || ContainsToken(entry.Description, query)
                || entry.MapId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesSearchQuery(MapEntry entry, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return ContainsToken(entry.DisplayName, query)
                || ContainsToken(entry.MapName, query)
                || ContainsToken(entry.StreetName, query)
                || ContainsToken(entry.CategoryName, query)
                || entry.MapId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsToken(string value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private static int ScoreMapEntryMatch(MapEntry entry, string query)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.MapId.ToString().Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                return 400;
            }

            if (!string.IsNullOrWhiteSpace(entry.DisplayName) && entry.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 300;
            }

            if (!string.IsNullOrWhiteSpace(entry.MapName) && entry.MapName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 260;
            }

            if (!string.IsNullOrWhiteSpace(entry.StreetName) && entry.StreetName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 220;
            }

            if (ContainsToken(entry.DisplayName, query))
            {
                return 180;
            }

            if (ContainsToken(entry.CategoryName, query))
            {
                return 140;
            }

            return 100;
        }

        private void ClearSearchQuery()
        {
            _searchQuery = string.Empty;
            _searchCursorPosition = 0;
            _caretBlinkTick = Environment.TickCount;
            ResetSearchKeyRepeat();
        }

        private void HandleSearchClipboardPaste()
        {
            try
            {
                if (!System.Windows.Forms.Clipboard.ContainsText())
                {
                    return;
                }

                string clipboardText = System.Windows.Forms.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    return;
                }

                string normalized = clipboardText.Replace("\r", " ").Replace("\n", " ");
                int remainingLength = MaxSearchQueryLength - _searchQuery.Length;
                if (remainingLength <= 0)
                {
                    return;
                }

                if (normalized.Length > remainingLength)
                {
                    normalized = normalized[..remainingLength];
                }

                _searchQuery = _searchQuery.Insert(_searchCursorPosition, normalized);
                _searchCursorPosition += normalized.Length;
                OnSearchQueryChanged();
            }
            catch
            {
                // Clipboard access is optional for the search shell.
            }
        }

        private void BeginHeldKey(Keys key, int tickCount)
        {
            _lastHeldSearchKey = key;
            _keyHoldStartTime = tickCount;
            _lastKeyRepeatTime = tickCount;
            _caretBlinkTick = tickCount;
        }

        private void ResetSearchKeyRepeat()
        {
            _lastHeldSearchKey = Keys.None;
            _keyHoldStartTime = int.MinValue;
            _lastKeyRepeatTime = int.MinValue;
        }

        private bool ShouldRepeatHeldKey(int tickCount)
        {
            return _lastHeldSearchKey != Keys.None
                && tickCount - _keyHoldStartTime >= KeyRepeatInitialDelayMs
                && tickCount - _lastKeyRepeatTime >= KeyRepeatIntervalMs;
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousSearchKeyboardState.IsKeyUp(key);
        }

        private static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char value = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpperInvariant(value) : value;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }

            return key switch
            {
                Keys.Space => ' ',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.Subtract => '-',
                Keys.OemPeriod or Keys.Decimal => '.',
                Keys.OemComma => ',',
                Keys.OemSemicolon => shift ? ':' : ';',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                Keys.OemPlus => shift ? '+' : '=',
                _ => null
            };
        }

        private void HandleSearchMouseInput(MouseState mouseState)
        {
            bool leftClicked = mouseState.LeftButton == ButtonState.Pressed
                && _previousMouseState.LeftButton == ButtonState.Released;
            if (!leftClicked)
            {
                return;
            }

            if (!GetSearchInputBounds().Contains(mouseState.X, mouseState.Y))
            {
                return;
            }

            _caretBlinkTick = Environment.TickCount;
            _searchCursorPosition = ResolveSearchCursorFromMouse(mouseState.X);
            ResetSearchKeyRepeat();
        }

        private int ResolveSearchCursorFromMouse(int mouseX)
        {
            if (_font == null || string.IsNullOrEmpty(_searchQuery))
            {
                return 0;
            }

            Rectangle bounds = GetSearchInputBounds();
            (int visibleStart, string visibleText) = GetVisibleSearchText();
            float localX = Math.Max(0, mouseX - bounds.X - SearchTextInsetX);
            float bestDistance = float.MaxValue;
            int bestCursor = visibleStart;

            for (int i = 0; i <= visibleText.Length; i++)
            {
                string prefix = i == 0 ? string.Empty : visibleText[..i];
                float prefixWidth = _font.MeasureString(prefix).X;
                float distance = Math.Abs(prefixWidth - localX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCursor = visibleStart + i;
                }
            }

            return Math.Clamp(bestCursor, 0, _searchQuery.Length);
        }

        private (int visibleStart, string visibleText) GetVisibleSearchText()
        {
            if (_font == null || string.IsNullOrEmpty(_searchQuery))
            {
                return (0, string.Empty);
            }

            float maxWidth = SearchInputBoxWidth - (SearchTextInsetX * 2) - 2;
            int caretIndex = Math.Clamp(_searchCursorPosition, 0, _searchQuery.Length);
            int visibleStart = 0;

            while (visibleStart < caretIndex)
            {
                if (_font.MeasureString(_searchQuery[visibleStart..caretIndex]).X <= maxWidth)
                {
                    break;
                }

                visibleStart++;
            }

            int visibleLength = Math.Max(0, Math.Min(_searchQuery.Length - visibleStart, Math.Max(1, caretIndex - visibleStart)));
            while (visibleStart + visibleLength < _searchQuery.Length)
            {
                string candidate = _searchQuery.Substring(visibleStart, visibleLength + 1);
                if (_font.MeasureString(candidate).X > maxWidth)
                {
                    break;
                }

                visibleLength++;
            }

            return (visibleStart, _searchQuery.Substring(visibleStart, visibleLength));
        }
    }
}
