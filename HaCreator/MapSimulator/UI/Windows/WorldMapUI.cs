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
        private readonly Texture2D _selectionTexture;
        private readonly UIObject _allButton;
        private readonly UIObject _anotherButton;
        private readonly UIObject _searchButton;
        private readonly UIObject _prevButton;
        private readonly UIObject _nextButton;
        private readonly List<RegionButtonEntry> _regionButtons = new List<RegionButtonEntry>();
        private readonly List<UIObject> _rowButtons = new List<UIObject>();
        private readonly List<MapEntry> _allEntries = new List<MapEntry>();
        private SpriteFont _font;
        private bool _showAnotherWorld;
        private string _selectedRegionCode = string.Empty;
        private int _currentMapId;
        private int _selectedMapId;
        private int _pageIndex;

        public WorldMapUI(
            IDXObject frame,
            Texture2D sidePanelTexture,
            Point sidePanelOffset,
            Texture2D selectionTexture,
            UIObject allButton,
            UIObject anotherButton,
            UIObject searchButton,
            UIObject prevButton,
            UIObject nextButton,
            IEnumerable<(string regionCode, UIObject button)> regionButtons,
            GraphicsDevice device)
            : base(frame)
        {
            _sidePanelTexture = sidePanelTexture;
            _sidePanelOffset = sidePanelOffset;
            _selectionTexture = selectionTexture;
            _allButton = allButton;
            _anotherButton = anotherButton;
            _searchButton = searchButton;
            _prevButton = prevButton;
            _nextButton = nextButton;

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
                _searchButton.ButtonClickReleased += _ => FocusCurrentMap();
                AddButton(_searchButton);
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
                    if (_pageIndex < GetMaxPageIndex())
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
            EnsureSelectedEntryVisible();
            UpdateButtonStates();
        }

        private void ActivateRow(int rowIndex)
        {
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
                _searchButton.SetEnabled(_currentMapId > 0);
            }

            int maxPageIndex = GetMaxPageIndex();
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
                bool regionVisible = _showAnotherWorld;
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

            IReadOnlyList<MapEntry> visibleEntries = GetVisibleEntries();
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool visible = i < visibleEntries.Count;
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

        private int GetMaxPageIndex()
        {
            return GetMaxPageIndex(GetFilteredEntries().Count);
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
    }
}
