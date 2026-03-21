using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
            Mob
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

        private readonly Texture2D _sidePanelTexture;
        private readonly Point _sidePanelOffset;
        private readonly Texture2D _searchNoticeTexture;
        private readonly Point _searchNoticeOffset;
        private readonly Texture2D _selectionTexture;
        private readonly UIObject _allButton;
        private readonly UIObject _anotherButton;
        private readonly UIObject _searchButton;
        private readonly UIObject _allSearchButton;
        private readonly UIObject _levelMobButton;
        private readonly UIObject _prevButton;
        private readonly UIObject _nextButton;
        private readonly Texture2D _resultFieldHoverTexture;
        private readonly Texture2D _resultFieldIconTexture;
        private readonly Texture2D _resultNpcHoverTexture;
        private readonly Texture2D _resultNpcIconTexture;
        private readonly Texture2D _resultMobHoverTexture;
        private readonly Texture2D _resultMobIconTexture;
        private readonly List<RegionButtonEntry> _regionButtons = new List<RegionButtonEntry>();
        private readonly List<UIObject> _rowButtons = new List<UIObject>();
        private readonly List<MapEntry> _allEntries = new List<MapEntry>();
        private readonly List<SearchResultEntry> _searchResults = new List<SearchResultEntry>();
        private SpriteFont _font;
        private bool _showAnotherWorld;
        private bool _searchMode;
        private string _selectedRegionCode = string.Empty;
        private int _currentMapId;
        private int _selectedMapId;
        private int _pageIndex;
        private SearchFilterMode _searchFilterMode;

        public WorldMapUI(
            IDXObject frame,
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
            : base(frame)
        {
            _sidePanelTexture = sidePanelTexture;
            _sidePanelOffset = sidePanelOffset;
            _searchNoticeTexture = searchNoticeTexture;
            _searchNoticeOffset = searchNoticeOffset;
            _selectionTexture = selectionTexture;
            _allButton = allButton;
            _anotherButton = anotherButton;
            _searchButton = searchButton;
            _allSearchButton = allSearchButton;
            _levelMobButton = levelMobButton;
            _prevButton = prevButton;
            _nextButton = nextButton;
            _resultFieldHoverTexture = resultFieldHoverTexture;
            _resultFieldIconTexture = resultFieldIconTexture;
            _resultNpcHoverTexture = resultNpcHoverTexture;
            _resultNpcIconTexture = resultNpcIconTexture;
            _resultMobHoverTexture = resultMobHoverTexture;
            _resultMobIconTexture = resultMobIconTexture;

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

        public override string WindowName => MapSimulatorWindowNames.WorldMap;

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
            EnsureSelectedEntryVisible();
            UpdateButtonStates();
        }

        public void SetSearchResults(IReadOnlyList<SearchResultEntry> searchResults)
        {
            _searchResults.Clear();
            if (searchResults != null)
            {
                _searchResults.AddRange(searchResults.Where(entry => entry != null));
            }

            if (_searchResults.Count == 0)
            {
                _searchMode = false;
                _searchFilterMode = SearchFilterMode.All;
            }

            _pageIndex = 0;
            UpdateButtonStates();
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

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                _showAnotherWorld ? "World Map - Another World" : "World Map - All Regions",
                new Vector2(Position.X + SummaryX, Position.Y + 18),
                Color.White);

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

            if (_selectedMapId == entry.MapId)
            {
                MapEntry mapEntry = _allEntries.FirstOrDefault(candidate => candidate.MapId == entry.MapId);
                if (mapEntry != null)
                {
                    MapRequested?.Invoke(mapEntry);
                }

                return;
            }

            _selectedMapId = entry.MapId;
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
            if (_searchFilterMode != SearchFilterMode.MobOnly)
            {
                return _searchResults;
            }

            return _searchResults
                .Where(entry => entry.Kind == SearchResultKind.Mob)
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
                bool hasSearchResults = _searchResults.Count > 0;
                _searchButton.SetEnabled(hasSearchResults);
                if (hasSearchResults)
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
            int fieldCount = _searchResults.Count(entry => entry.Kind == SearchResultKind.Field);
            int npcCount = _searchResults.Count(entry => entry.Kind == SearchResultKind.Npc);
            int mobCount = _searchResults.Count(entry => entry.Kind == SearchResultKind.Mob);
            string filterText = _searchFilterMode == SearchFilterMode.MobOnly ? "Mob-only filter active." : "All live result families are visible.";
            return $"Current field search surface.\nFields: {fieldCount}  NPCs: {npcCount}  Mobs: {mobCount}\n{filterText}\nClick a result row to focus its map, then click again to queue transfer.";
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
            if (_searchResults.Count == 0)
            {
                return;
            }

            _searchMode = true;
            _pageIndex = 0;
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
            _pageIndex = 0;
            UpdateButtonStates();
        }

        private void DrawSearchContents(SpriteBatch sprite)
        {
            if (_searchNoticeTexture != null)
            {
                sprite.Draw(_searchNoticeTexture, new Vector2(Position.X + _searchNoticeOffset.X, Position.Y + _searchNoticeOffset.Y), Color.White);
            }

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                "World Map Search",
                new Vector2(Position.X + SummaryX, Position.Y + 18),
                Color.White);

            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                TrimToWidth($"Current: {ResolveCurrentMapText()}", SummaryWidth),
                new Vector2(Position.X + SummaryX, Position.Y + 38),
                new Color(220, 220, 220));

            float textY = Position.Y + SummaryY;
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
            if (entry.MapId == _selectedMapId)
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
                entry.MapId == _selectedMapId ? Color.White : new Color(228, 228, 228));
        }

        private Texture2D GetHoverTexture(SearchResultKind kind)
        {
            return kind switch
            {
                SearchResultKind.Field => _resultFieldHoverTexture,
                SearchResultKind.Npc => _resultNpcHoverTexture,
                SearchResultKind.Mob => _resultMobHoverTexture,
                _ => null
            };
        }

        private Texture2D GetIconTexture(SearchResultKind kind)
        {
            return kind switch
            {
                SearchResultKind.Field => _resultFieldIconTexture,
                SearchResultKind.Npc => _resultNpcIconTexture,
                SearchResultKind.Mob => _resultMobIconTexture,
                _ => null
            };
        }
    }
}
