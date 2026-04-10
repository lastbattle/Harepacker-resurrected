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
    public sealed class WorldMapUI : UIWindowBase, ISoftKeyboardHost
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

        public sealed class QuestOverlayEntry
        {
            public SearchResultKind Kind { get; init; }
            public int MapId { get; init; }
            public string Label { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public bool IsPriorityTarget { get; init; }
            public int StableOrder { get; init; }
        }

        public sealed class WorldMapSurfaceDefinition
        {
            public string SurfaceName { get; init; } = string.Empty;
            public string ParentSurfaceName { get; init; } = string.Empty;
            public Texture2D BaseTexture { get; init; }
            public Point BaseOrigin { get; init; }
            public IReadOnlyDictionary<int, Point> MapSpots { get; init; } = new Dictionary<int, Point>();
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

        private readonly struct OverlayMarkerFrame
        {
            public OverlayMarkerFrame(Texture2D texture, Point origin, int delay)
            {
                Texture = texture;
                Origin = origin;
                Delay = Math.Max(1, delay);
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
            public int Delay { get; }
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
        private const int CandidateWindowPadding = 4;
        private const uint CandidateWindowStyleRect = 0x0001;
        private const uint CandidateWindowStylePoint = 0x0002;
        private const uint CandidateWindowStyleForcePosition = 0x0020;
        private const uint CandidateWindowStyleCandidatePosition = 0x0040;
        private const uint CandidateWindowStyleExclude = 0x0080;

        private readonly Texture2D _titleTexture;
        private readonly Texture2D _sidePanelTexture;
        private readonly Point _sidePanelOffset;
        private readonly Texture2D _searchNoticeTexture;
        private readonly Point _searchNoticeOffset;
        private readonly Texture2D _selectionTexture;
        private readonly Texture2D _searchInputBackgroundTexture;
        private readonly Texture2D _searchInputOutlineTexture;
        private readonly Texture2D _caretTexture;
        private readonly OverlayMarkerFrame[] _overlayMarkerFrames;
        private readonly UIObject _allButton;
        private readonly UIObject _anotherButton;
        private readonly UIObject _searchButton;
        private readonly UIObject _allSearchButton;
        private readonly UIObject _levelMobButton;
        private readonly UIObject _prevButton;
        private readonly UIObject _nextButton;
        private UIObject _locationButton;
        private UIObject _questToggleButton;
        private readonly IReadOnlyDictionary<SearchResultKind, SearchResultVisualStyle> _resultStyles;
        private readonly List<RegionButtonEntry> _regionButtons = new List<RegionButtonEntry>();
        private readonly List<UIObject> _rowButtons = new List<UIObject>();
        private readonly List<MapEntry> _allEntries = new List<MapEntry>();
        private readonly List<SearchResultEntry> _searchResults = new List<SearchResultEntry>();
        private readonly List<QuestOverlayEntry> _questOverlays = new List<QuestOverlayEntry>();
        private readonly Dictionary<string, WorldMapSurfaceDefinition> _worldMapSurfacesByName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _worldMapSurfaceNameByMapId = new();
        private SpriteFont _font;
        private bool _showAnotherWorld;
        private bool _searchMode;
        private bool _softKeyboardActive;
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
        private string _hoveredSearchResultKey = string.Empty;
        private int _pageIndex;
        private SearchFilterMode _searchFilterMode;
        private bool _questOverlayMarkersVisible = true;
        private MouseState _previousMouseState;
        private string _compositionText = string.Empty;
        private int _compositionInsertionIndex = -1;
        private IReadOnlyList<int> _compositionClauseOffsets = Array.Empty<int>();
        private int _compositionCursorPosition = -1;
        private ImeCandidateListState _candidateListState = ImeCandidateListState.Empty;
        internal Func<int, int, bool> OnImeCandidateSelected;
        internal Func<IntPtr> ResolveImeWindowHandle;

        public WorldMapUI(
            IDXObject frame,
            Texture2D titleTexture,
            Texture2D sidePanelTexture,
            Point sidePanelOffset,
            Texture2D searchNoticeTexture,
            Point searchNoticeOffset,
            Texture2D selectionTexture,
            Texture2D[] overlayMarkerTextures,
            Point[] overlayMarkerOrigins,
            int[] overlayMarkerDelays,
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
            _overlayMarkerFrames = BuildOverlayMarkerFrames(overlayMarkerTextures, overlayMarkerOrigins, overlayMarkerDelays);
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
            IReadOnlyDictionary<SearchResultKind, SearchResultVisualStyle> resultStyles,
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
                Array.Empty<Texture2D>(),
                Array.Empty<Point>(),
                Array.Empty<int>(),
                allButton,
                anotherButton,
                searchButton,
                allSearchButton,
                levelMobButton,
                prevButton,
                nextButton,
                resultStyles,
                regionButtons,
                device)
        {
        }

        public WorldMapUI(
            IDXObject frame,
            Texture2D titleTexture,
            Texture2D sidePanelTexture,
            Point sidePanelOffset,
            Texture2D searchNoticeTexture,
            Point searchNoticeOffset,
            Texture2D selectionTexture,
            Texture2D[] overlayMarkerTextures,
            Point[] overlayMarkerOrigins,
            int[] overlayMarkerDelays,
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
                overlayMarkerTextures,
                overlayMarkerOrigins,
                overlayMarkerDelays,
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
        bool ISoftKeyboardHost.WantsSoftKeyboard => IsVisible && _searchMode && _softKeyboardActive;
        SoftKeyboardKeyboardType ISoftKeyboardHost.SoftKeyboardKeyboardType => SoftKeyboardKeyboardType.AlphaNumeric;
        int ISoftKeyboardHost.SoftKeyboardTextLength => _searchQuery?.Length ?? 0;
        int ISoftKeyboardHost.SoftKeyboardMaxLength => MaxSearchQueryLength;
        bool ISoftKeyboardHost.CanSubmitSoftKeyboard => _searchMode && GetFilteredSearchResults().Count > 0;

        public event Action<MapEntry> MapRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void HandleCommittedText(string text)
        {
            if (!CapturesKeyboardInput || string.IsNullOrEmpty(text))
            {
                return;
            }

            ClearCompositionText();
            InsertSearchText(text);
            UpdateImePresentationPlacement();
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

            ImeCompositionState effectiveState = state ?? ImeCompositionState.Empty;
            string sanitized = SanitizeSearchText(effectiveState.Text);
            if (string.IsNullOrEmpty(sanitized))
            {
                ClearCompositionText();
                return;
            }

            if (_compositionText.Length == 0)
            {
                _compositionInsertionIndex = Math.Clamp(_searchCursorPosition, 0, _searchQuery.Length);
            }

            int availableLength = Math.Max(0, MaxSearchQueryLength - _searchQuery.Length);
            if (availableLength <= 0)
            {
                ClearCompositionText();
                return;
            }

            _compositionText = sanitized.Length > availableLength
                ? sanitized[..availableLength]
                : sanitized;
            _compositionClauseOffsets = ClampClauseOffsets(effectiveState.ClauseOffsets, _compositionText.Length);
            _compositionCursorPosition = Math.Clamp(effectiveState.CursorPosition, -1, _compositionText.Length);
            _caretBlinkTick = Environment.TickCount;
            UpdateImePresentationPlacement();
        }

        public override void ClearCompositionText()
        {
            _compositionText = string.Empty;
            _compositionInsertionIndex = -1;
            _compositionClauseOffsets = Array.Empty<int>();
            _compositionCursorPosition = -1;
        }

        public override void HandleImeCandidateList(ImeCandidateListState state)
        {
            _candidateListState = CapturesKeyboardInput && state != null && state.HasCandidates
                ? state
                : ImeCandidateListState.Empty;
            UpdateImePresentationPlacement();
        }

        public override void ClearImeCandidateList()
        {
            _candidateListState = ImeCandidateListState.Empty;
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
            _hoveredSearchResultKey = string.Empty;
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
            _hoveredSearchResultKey = string.Empty;
            _pageIndex = 0;
            UpdateButtonStates();
        }

        public void SetQuestOverlays(IReadOnlyList<QuestOverlayEntry> overlays)
        {
            _questOverlays.Clear();
            if (overlays != null)
            {
                _questOverlays.AddRange(overlays.Where(entry => entry != null));
            }

            UpdateButtonStates();
        }

        public void InitializeQuestGuideButtons(UIObject locationButton, UIObject questToggleButton)
        {
            if (locationButton != null && !uiButtons.Contains(locationButton))
            {
                _locationButton = locationButton;
                _locationButton.ButtonClickReleased += _ => FocusCurrentMap();
                AddButton(_locationButton);
            }

            if (questToggleButton != null && !uiButtons.Contains(questToggleButton))
            {
                _questToggleButton = questToggleButton;
                _questToggleButton.ButtonClickReleased += _ => ToggleQuestOverlayMarkers();
                AddButton(_questToggleButton);
            }

            UpdateButtonStates();
        }

        public void ConfigureWorldMapSurfaces(IEnumerable<WorldMapSurfaceDefinition> surfaces)
        {
            _worldMapSurfacesByName.Clear();
            _worldMapSurfaceNameByMapId.Clear();

            foreach (WorldMapSurfaceDefinition surface in surfaces ?? Enumerable.Empty<WorldMapSurfaceDefinition>())
            {
                if (surface?.BaseTexture == null || string.IsNullOrWhiteSpace(surface.SurfaceName))
                {
                    continue;
                }

                _worldMapSurfacesByName[surface.SurfaceName] = surface;
                foreach ((int mapId, _) in surface.MapSpots ?? Enumerable.Empty<KeyValuePair<int, Point>>())
                {
                    if (mapId > 0)
                    {
                        _worldMapSurfaceNameByMapId[mapId] = surface.SurfaceName;
                    }
                }
            }
        }

        public bool HasEntry(int mapId)
        {
            return mapId > 0 && _allEntries.Any(entry => entry.MapId == mapId);
        }

        public bool CanPresentMapId(int mapId)
        {
            return mapId > 0
                && HasEntry(mapId)
                && TryResolveSurfaceForMapId(mapId, out WorldMapSurfaceDefinition surface)
                && TryResolveMapSpot(surface, mapId, out _);
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
            _hoveredSearchResultKey = string.Empty;
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

            UpdateHoveredSearchResult(mouseState);
            HandleSearchMouseInput(mouseState);
            HandleSearchKeyboardInput(keyboardState, tickCount);
            UpdateImePresentationPlacement();
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
            DrawWorldMapSurface(sprite, TickCount);

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
                DrawSearchContents(sprite, TickCount);
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
                Rectangle rowBounds = new(Position.X + ListStartX, Position.Y + ListStartY + (row * RowHeight), ListWidth, RowHeight);
                Color textColor = entry.MapId == _selectedMapId ? Color.White : new Color(228, 228, 228);
                int overlayCount = GetOverlayCountForMap(entry.MapId);
                int markerInset = DrawOverlayMarker(sprite, rowBounds, overlayCount, TickCount);
                string label = TrimToWidth($"{entry.MapId} {entry.DisplayName}", ListWidth - markerInset - 4);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    label,
                    new Vector2(rowBounds.X + 2 + markerInset, rowBounds.Y + 1),
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

            if (_locationButton != null)
            {
                _locationButton.SetVisible(true);
                _locationButton.SetEnabled(_currentMapId > 0);
                if (_currentMapId > 0)
                {
                    _locationButton.SetButtonState(
                        _selectedMapId == _currentMapId
                            ? UIObjectState.Pressed
                            : UIObjectState.Normal);
                }
            }

            if (_questToggleButton != null)
            {
                bool hasQuestOverlays = _questOverlays.Count > 0;
                _questToggleButton.SetVisible(hasQuestOverlays);
                _questToggleButton.SetEnabled(hasQuestOverlays);
                if (hasQuestOverlays)
                {
                    _questToggleButton.SetButtonState(
                        _questOverlayMarkersVisible
                            ? UIObjectState.Pressed
                            : UIObjectState.Normal);
                }
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
            string overlayText = BuildQuestOverlaySummary(selectedEntry.MapId, includeCurrentMapFallback: true);
            return string.IsNullOrWhiteSpace(overlayText)
                ? $"Selected: {selectedEntry.DisplayName}\nCategory: {selectedEntry.CategoryName}\n{regionText}\nClick the selected row again to transfer through the world-map flow."
                : $"Selected: {selectedEntry.DisplayName}\nCategory: {selectedEntry.CategoryName}\n{regionText}\n{overlayText}";
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
            string overlayText = BuildQuestOverlaySummary(_selectedMapId > 0 ? _selectedMapId : _currentMapId, includeCurrentMapFallback: false);
            return string.IsNullOrWhiteSpace(overlayText)
                ? $"Current field search surface.\nFields: {fieldCount}  NPCs: {npcCount}  Mobs: {mobCount}  Quest items: {itemCount}\n{filterText}\n{queryText}\nClick a result row to focus its map, then click again to queue transfer."
                : $"Current field search surface.\nFields: {fieldCount}  NPCs: {npcCount}  Mobs: {mobCount}  Quest items: {itemCount}\n{filterText}\n{queryText}\n{overlayText}";
        }

        private string BuildQuestOverlaySummary(int mapId, bool includeCurrentMapFallback)
        {
            if (!_questOverlayMarkersVisible)
            {
                return _questOverlays.Count > 0
                    ? "Quest overlays are hidden. Use the world-map quest toggle to re-enable markers."
                    : string.Empty;
            }

            IReadOnlyList<QuestOverlayEntry> overlays = _questOverlays
                .Where(entry => entry.MapId == mapId)
                .ToArray();
            if (overlays.Count == 0 && includeCurrentMapFallback)
            {
                overlays = _questOverlays
                    .Where(entry => entry.MapId == _currentMapId)
                    .ToArray();
            }

            if (overlays.Count == 0)
            {
                return string.Empty;
            }

            IEnumerable<string> lines = overlays
                .Take(3)
                .Select(entry => $"{ResolveOverlayPrefix(entry.Kind)} {entry.Label}: {entry.Description}");
            string overflowLine = overlays.Count > 3
                ? $"\n{overlays.Count - 3} more quest overlay target(s) are attached to this map."
                : string.Empty;
            return $"Quest overlays:\n{string.Join("\n", lines)}{overflowLine}";
        }

        private int GetOverlayCountForMap(int mapId)
        {
            if (mapId <= 0)
            {
                return 0;
            }

            if (!_questOverlayMarkersVisible)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _questOverlays.Count; i++)
            {
                if (_questOverlays[i].MapId == mapId)
                {
                    count++;
                }
            }

            return count;
        }

        private static string ResolveOverlayPrefix(SearchResultKind kind)
        {
            return kind switch
            {
                SearchResultKind.Npc => "NPC",
                SearchResultKind.Mob => "Mob",
                SearchResultKind.Item => "Item",
                _ => "Field"
            };
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

        private void DrawWorldMapSurface(SpriteBatch sprite, int tickCount)
        {
            if (sprite == null || !TryResolveActiveSurface(out WorldMapSurfaceDefinition surface))
            {
                return;
            }

            sprite.Draw(surface.BaseTexture, new Vector2(Position.X, Position.Y), Color.White);

            if (!_questOverlayMarkersVisible)
            {
                return;
            }

            OverlayMarkerFrame? markerFrame = GetActiveOverlayMarkerFrame(tickCount);
            if (!markerFrame.HasValue)
            {
                return;
            }

            foreach (IGrouping<int, QuestOverlayEntry> overlayGroup in _questOverlays
                .Where(entry => entry != null && entry.MapId > 0)
                .GroupBy(entry => entry.MapId))
            {
                if (!TryResolveMapSpot(surface, overlayGroup.Key, out Point spotAnchor))
                {
                    continue;
                }

                QuestOverlayEntry[] orderedOverlays = overlayGroup
                    .OrderByDescending(entry => entry.IsPriorityTarget)
                    .ThenBy(entry => entry.StableOrder)
                    .ThenBy(entry => entry.Kind)
                    .ThenBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                for (int i = 0; i < orderedOverlays.Length; i++)
                {
                    DrawSurfaceMarker(sprite, markerFrame.Value, spotAnchor, i);
                }
            }
        }

        private bool TryResolveActiveSurface(out WorldMapSurfaceDefinition surface)
        {
            if (TryResolveSurfaceForMapId(_selectedMapId, out surface))
            {
                return true;
            }

            if (TryResolveSurfaceForMapId(_currentMapId, out surface))
            {
                return true;
            }

            foreach (QuestOverlayEntry overlay in _questOverlays)
            {
                if (overlay != null && TryResolveSurfaceForMapId(overlay.MapId, out surface))
                {
                    return true;
                }
            }

            surface = null;
            return false;
        }

        private bool TryResolveSurfaceForMapId(int mapId, out WorldMapSurfaceDefinition surface)
        {
            if (mapId > 0 &&
                _worldMapSurfaceNameByMapId.TryGetValue(mapId, out string surfaceName) &&
                !string.IsNullOrWhiteSpace(surfaceName) &&
                _worldMapSurfacesByName.TryGetValue(surfaceName, out surface))
            {
                return true;
            }

            surface = null;
            return false;
        }

        private bool TryResolveMapSpot(WorldMapSurfaceDefinition surface, int mapId, out Point anchor)
        {
            if (surface?.MapSpots != null && surface.MapSpots.TryGetValue(mapId, out Point surfaceSpot))
            {
                anchor = new Point(Position.X + surface.BaseOrigin.X + surfaceSpot.X, Position.Y + surface.BaseOrigin.Y + surfaceSpot.Y);
                return true;
            }

            anchor = Point.Zero;
            return false;
        }

        private static void DrawSurfaceMarker(SpriteBatch sprite, OverlayMarkerFrame frame, Point anchor, int overlayIndex)
        {
            if (frame.Texture == null)
            {
                return;
            }

            Point overlayOffset = ResolveOverlaySurfaceOffset(overlayIndex);
            Vector2 drawPosition = new(
                anchor.X + overlayOffset.X - frame.Origin.X,
                anchor.Y + overlayOffset.Y - frame.Origin.Y);
            sprite.Draw(frame.Texture, drawPosition, Color.White);
        }

        private static Point ResolveOverlaySurfaceOffset(int overlayIndex)
        {
            return overlayIndex switch
            {
                <= 0 => Point.Zero,
                1 => new Point(-16, -6),
                2 => new Point(16, -6),
                3 => new Point(-24, 8),
                4 => new Point(24, 8),
                5 => new Point(0, -16),
                _ => ResolveOverlayRingOffset(overlayIndex)
            };
        }

        private static Point ResolveOverlayRingOffset(int overlayIndex)
        {
            int ringIndex = Math.Max(0, overlayIndex - 6);
            int slot = ringIndex % 6;
            int radius = 24 + ((ringIndex / 6) * 10);
            double angle = ((Math.PI * 2d) / 6d) * slot;
            return new Point(
                (int)Math.Round(Math.Cos(angle) * radius),
                (int)Math.Round(Math.Sin(angle) * radius * 0.6d) - 8);
        }

        private static int GetMaxPageIndex(int count)
        {
            return Math.Max(0, (int)Math.Ceiling(count / (double)MaxVisibleRows) - 1);
        }

        private void ToggleQuestOverlayMarkers()
        {
            if (_questOverlays.Count == 0)
            {
                return;
            }

            _questOverlayMarkersVisible = !_questOverlayMarkersVisible;
            UpdateButtonStates();
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
            _softKeyboardActive = false;
            _searchFilterMode = SearchFilterMode.All;
            _hoveredSearchResultKey = string.Empty;
            ClearCompositionText();
            ClearImeCandidateList();
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

        private void DrawSearchContents(SpriteBatch sprite, int tickCount)
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
            DrawImeCandidateWindow(sprite);

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
                DrawSearchRow(sprite, visibleResults[row], row, tickCount);
            }

            string pageText = $"{_pageIndex + 1}/{Math.Max(1, GetMaxPageIndexForCurrentMode() + 1)}";
            SelectorWindowDrawing.DrawShadowedText(
                sprite,
                _font,
                pageText,
                new Vector2(Position.X + 586, Position.Y + 511),
                new Color(214, 214, 214));
        }

        private void DrawSearchRow(SpriteBatch sprite, SearchResultEntry entry, int row, int tickCount)
        {
            SearchResultVisualStyle? style = GetResultStyle(entry.Kind);
            Texture2D hoverTexture = style?.HoverTexture;
            Rectangle rowBounds = new Rectangle(Position.X + ListStartX, Position.Y + ListStartY + (row * RowHeight), ListWidth, RowHeight);
            if (IsSearchResultHovered(entry))
            {
                if (hoverTexture != null)
                {
                    Point hoverOffset = style?.HoverOffset ?? Point.Zero;
                    sprite.Draw(hoverTexture, new Vector2(rowBounds.X + hoverOffset.X, rowBounds.Y + hoverOffset.Y), Color.White);
                }
                else if (_selectionTexture != null)
                {
                    sprite.Draw(_selectionTexture, rowBounds, new Color(72, 98, 145, 120));
                }
            }

            if (IsSearchResultSelected(entry) && _selectionTexture != null)
            {
                sprite.Draw(_selectionTexture, rowBounds, new Color(86, 120, 186, 165));
            }

            Texture2D iconTexture = style?.IconTexture;
            int textStartX = rowBounds.X + 2;
            textStartX += DrawOverlayMarker(sprite, rowBounds, GetOverlayCountForMap(entry.MapId), tickCount);
            if (iconTexture != null)
            {
                Point iconOffset = style?.IconOffset ?? Point.Zero;
                sprite.Draw(iconTexture, new Vector2(rowBounds.X + 2 + iconOffset.X, rowBounds.Y + 1 + iconOffset.Y), Color.White);
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

        private int DrawOverlayMarker(SpriteBatch sprite, Rectangle rowBounds, int overlayCount, int tickCount)
        {
            if (overlayCount <= 0)
            {
                return 0;
            }

            OverlayMarkerFrame? markerFrame = GetActiveOverlayMarkerFrame(tickCount);
            if (!markerFrame.HasValue)
            {
                return 12;
            }

            OverlayMarkerFrame frame = markerFrame.Value;
            Texture2D texture = frame.Texture;
            if (texture == null)
            {
                return 12;
            }

            int maxHeight = Math.Max(1, rowBounds.Height - 2);
            float scale = Math.Min(1f, maxHeight / (float)Math.Max(1, texture.Height));
            int width = Math.Max(1, (int)Math.Round(texture.Width * scale));
            int height = Math.Max(1, (int)Math.Round(texture.Height * scale));
            int anchorX = rowBounds.X + 7;
            int anchorY = rowBounds.Bottom - 1;
            Rectangle destination = new(
                anchorX - (int)Math.Round(frame.Origin.X * scale),
                anchorY - (int)Math.Round(frame.Origin.Y * scale),
                width,
                height);
            sprite.Draw(frame.Texture, destination, Color.White);
            return Math.Max(12, destination.Width) + 2;
        }

        private OverlayMarkerFrame? GetActiveOverlayMarkerFrame(int tickCount)
        {
            if (_overlayMarkerFrames.Length == 0)
            {
                return null;
            }

            int totalDelay = 0;
            for (int i = 0; i < _overlayMarkerFrames.Length; i++)
            {
                totalDelay += _overlayMarkerFrames[i].Delay;
            }

            if (totalDelay <= 0)
            {
                return _overlayMarkerFrames[0];
            }

            int time = Math.Abs(tickCount % totalDelay);
            for (int i = 0; i < _overlayMarkerFrames.Length; i++)
            {
                if (time < _overlayMarkerFrames[i].Delay)
                {
                    return _overlayMarkerFrames[i];
                }

                time -= _overlayMarkerFrames[i].Delay;
            }

            return _overlayMarkerFrames[^1];
        }

        private static OverlayMarkerFrame[] BuildOverlayMarkerFrames(Texture2D[] textures, Point[] origins, int[] delays)
        {
            if (textures == null || origins == null || delays == null)
            {
                return Array.Empty<OverlayMarkerFrame>();
            }

            int count = Math.Min(textures.Length, Math.Min(origins.Length, delays.Length));
            if (count <= 0)
            {
                return Array.Empty<OverlayMarkerFrame>();
            }

            var frames = new List<OverlayMarkerFrame>(count);
            for (int i = 0; i < count; i++)
            {
                if (textures[i] == null)
                {
                    continue;
                }

                frames.Add(new OverlayMarkerFrame(textures[i], origins[i], delays[i]));
            }

            return frames.ToArray();
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

        private Rectangle GetSearchResultListBounds(int visibleRowCount)
        {
            if (visibleRowCount <= 0)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(
                Position.X + ListStartX,
                Position.Y + ListStartY,
                ListWidth,
                visibleRowCount * RowHeight);
        }

        private Viewport BuildOwnerViewport()
        {
            Rectangle ownerBounds = GetOwnerBounds();
            int viewportWidth = Math.Max(1, ownerBounds.Right);
            int viewportHeight = Math.Max(1, ownerBounds.Bottom);
            return new Viewport(0, 0, viewportWidth, viewportHeight);
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

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(visibleCount, _candidateListState.Candidates.Count - start);
            int viewportWidth = Math.Max(1, viewport.Width);
            int viewportHeight = Math.Max(1, viewport.Height);
            int width;
            int height;
            if (_candidateListState.Vertical)
            {
                int widestEntryWidth = 0;
                for (int i = 0; i < count; i++)
                {
                    int candidateIndex = start + i;
                    string numberText = $"{i + 1}.";
                    string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                    int entryWidth = (int)Math.Ceiling(_font.MeasureString(numberText).X + _font.MeasureString(candidateText).X) + 2;
                    widestEntryWidth = Math.Max(widestEntryWidth, entryWidth);
                }

                width = widestEntryWidth + _font.LineSpacing + 7;
                height = (GetCandidatePageSize() * GetCandidateRowHeight()) + 3;
            }
            else
            {
                width = GetHorizontalCandidateWindowWidth();
                height = _font.LineSpacing + 10;
            }

            Rectangle ownerBounds = GetOwnerBounds();
            int availableWidth = ownerBounds.Width > 0 ? ownerBounds.Width : viewportWidth;
            int availableHeight = ownerBounds.Height > 0 ? ownerBounds.Height : viewportHeight;

            width = Math.Max(64, Math.Min(Math.Min(viewportWidth, availableWidth), width));
            height = Math.Max(4, Math.Min(Math.Min(viewportHeight, availableHeight), height));
            Point origin = ResolveCandidateWindowOrigin(viewport, width, height);

            int minX = Math.Max(0, ownerBounds.X);
            int maxX = Math.Min(viewportWidth, ownerBounds.Right) - width;
            if (maxX < minX)
            {
                minX = 0;
                maxX = Math.Max(0, viewportWidth - width);
            }

            int x = Math.Clamp(origin.X, minX, Math.Max(minX, maxX));
            int y = origin.Y;
            int ownerBottom = ownerBounds.Height > 0 ? Math.Min(viewportHeight, ownerBounds.Bottom) : viewportHeight;
            if (y + height > ownerBottom)
            {
                y = origin.Y - height - (_font.LineSpacing + 2);
            }

            int minY = Math.Max(0, ownerBounds.Y);
            int maxY = ownerBounds.Height > 0
                ? Math.Min(viewportHeight, ownerBounds.Bottom) - height
                : viewportHeight - height;
            y = Math.Clamp(y, minY, Math.Max(minY, maxY));
            return new Rectangle(x, y, width, height);
        }

        private Rectangle GetOwnerBounds()
        {
            IDXObject frame = CurrentFrame;
            if (frame == null)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(Position.X, Position.Y, Math.Max(0, frame.Width), Math.Max(0, frame.Height));
        }

        private void DrawSearchInput(SpriteBatch sprite, int tickCount)
        {
            Rectangle bounds = GetSearchInputBounds();

            sprite.Draw(_searchInputBackgroundTexture, bounds, new Color(16, 27, 43, 215));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(117, 155, 220));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(117, 155, 220));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), new Color(117, 155, 220));
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), new Color(117, 155, 220));

            SearchInputVisualState searchVisual = BuildSearchInputVisualState();
            if (string.IsNullOrEmpty(searchVisual.VisibleText))
            {
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    "Type to search field, NPC, or mob names",
                    new Vector2(bounds.X + SearchTextInsetX, bounds.Y + SearchTextInsetY),
                    new Color(166, 184, 206));
                return;
            }

            Vector2 textPosition = new Vector2(bounds.X + SearchTextInsetX, bounds.Y + SearchTextInsetY);
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, searchVisual.VisibleText, textPosition, new Color(236, 236, 236));

            if (searchVisual.VisibleCompositionLength > 0)
            {
                string compositionPrefix = searchVisual.VisibleCompositionStart <= 0
                    ? string.Empty
                    : searchVisual.VisibleText[..searchVisual.VisibleCompositionStart];
                string compositionText = searchVisual.VisibleText.Substring(searchVisual.VisibleCompositionStart, searchVisual.VisibleCompositionLength);
                int underlineX = bounds.X + SearchTextInsetX + (int)_font.MeasureString(compositionPrefix).X;
                int underlineWidth = Math.Max(1, (int)Math.Ceiling(_font.MeasureString(compositionText).X));
                int underlineY = bounds.Bottom - 4;
                sprite.Draw(_caretTexture, new Rectangle(underlineX, underlineY, underlineWidth, 1), new Color(164, 214, 255, 220));
            }

            if (((tickCount - _caretBlinkTick) / 500) % 2 != 0)
            {
                return;
            }

            int visibleCaretIndex = Math.Clamp(searchVisual.VisibleCaretIndex, 0, searchVisual.VisibleText.Length);
            string caretPrefix = visibleCaretIndex <= 0
                ? string.Empty
                : searchVisual.VisibleText[..visibleCaretIndex];
            int caretX = bounds.X + SearchTextInsetX + (int)_font.MeasureString(caretPrefix).X;
            int caretY = bounds.Y + 3;
            sprite.Draw(_caretTexture, new Rectangle(caretX, caretY, 1, Math.Max(12, _font.LineSpacing - 2)), new Color(255, 255, 255, 220));
        }

        private void DrawImeCandidateWindow(SpriteBatch sprite)
        {
            if (_font == null || !_candidateListState.HasCandidates)
            {
                return;
            }

            Rectangle candidateBounds = GetImeCandidateWindowBounds(sprite.GraphicsDevice.Viewport);
            if (candidateBounds.Width <= 0 || candidateBounds.Height <= 0)
            {
                return;
            }

            sprite.Draw(_searchInputBackgroundTexture, candidateBounds, new Color(21, 28, 40, 240));

            Color borderColor = new(117, 155, 220, 220);
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(candidateBounds.X, candidateBounds.Y, candidateBounds.Width, 1), borderColor);
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(candidateBounds.X, candidateBounds.Bottom - 1, candidateBounds.Width, 1), borderColor);
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(candidateBounds.X, candidateBounds.Y, 1, candidateBounds.Height), borderColor);
            sprite.Draw(_searchInputOutlineTexture, new Rectangle(candidateBounds.Right - 1, candidateBounds.Y, 1, candidateBounds.Height), borderColor);

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            if (count <= 0)
            {
                return;
            }

            if (_candidateListState.Vertical)
            {
                int rowHeight = GetCandidateRowHeight();
                int numberWidth = GetCandidateNumberWidth();
                for (int i = 0; i < count; i++)
                {
                    int candidateIndex = start + i;
                    Rectangle rowBounds = new(candidateBounds.X + 2, candidateBounds.Y + 2 + (i * rowHeight), candidateBounds.Width - 4, rowHeight);
                    bool selected = candidateIndex == _candidateListState.Selection;
                    if (selected)
                    {
                        sprite.Draw(_searchInputOutlineTexture, rowBounds, new Color(86, 120, 186, 215));
                    }

                    sprite.DrawString(_font, $"{i + 1}.", new Vector2(rowBounds.X + 4, rowBounds.Y), selected ? Color.White : new Color(222, 222, 222));
                    sprite.DrawString(
                        _font,
                        _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                        new Vector2(rowBounds.X + 8 + numberWidth, rowBounds.Y),
                        selected ? Color.White : new Color(240, 235, 200));
                }
            }
            else
            {
                int cellWidth = GetHorizontalCandidateCellWidth();
                int textY = candidateBounds.Y + 3;
                for (int i = 0; i < count; i++)
                {
                    int candidateIndex = start + i;
                    int cellX = candidateBounds.X + 3 + (i * cellWidth);
                    int numberWidth = (int)Math.Ceiling(_font.MeasureString($"{i + 1}.").X);
                    Rectangle cellBounds = new(cellX - 1, candidateBounds.Y + 1, cellWidth, Math.Max(1, candidateBounds.Height - 2));
                    bool selected = candidateIndex == _candidateListState.Selection;
                    if (selected)
                    {
                        sprite.Draw(_searchInputOutlineTexture, cellBounds, new Color(86, 120, 186, 215));
                    }

                    sprite.DrawString(_font, $"{i + 1}.", new Vector2(cellX, textY), selected ? Color.White : new Color(222, 222, 222));
                    sprite.DrawString(
                        _font,
                        _candidateListState.Candidates[candidateIndex] ?? string.Empty,
                        new Vector2(cellX + numberWidth + 3, textY),
                        selected ? Color.White : new Color(240, 235, 200));
                }
            }
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

            if (_lastHeldSearchKey != Keys.None
                && keyboardState.IsKeyDown(_lastHeldSearchKey)
                && ShouldRepeatHeldKey(tickCount))
            {
                bool repeatCtrl = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
                if (_lastHeldSearchKey == Keys.Back)
                {
                    if (_searchCursorPosition > 0)
                    {
                        if (repeatCtrl)
                        {
                            DeletePreviousSearchWord();
                        }
                        else
                        {
                            _searchQuery = _searchQuery.Remove(_searchCursorPosition - 1, 1);
                            _searchCursorPosition--;
                            OnSearchQueryChanged();
                        }
                    }
                }
                else if (_lastHeldSearchKey == Keys.Delete)
                {
                    if (_searchCursorPosition < _searchQuery.Length)
                    {
                        if (repeatCtrl)
                        {
                            DeleteNextSearchWord();
                        }
                        else
                        {
                            _searchQuery = _searchQuery.Remove(_searchCursorPosition, 1);
                            OnSearchQueryChanged();
                        }
                    }
                }
                else if (_lastHeldSearchKey == Keys.Left)
                {
                    _searchCursorPosition = repeatCtrl
                        ? FindPreviousSearchWordBoundary()
                        : Math.Max(0, _searchCursorPosition - 1);
                }
                else if (_lastHeldSearchKey == Keys.Right)
                {
                    _searchCursorPosition = repeatCtrl
                        ? FindNextSearchWordBoundary()
                        : Math.Min(_searchQuery.Length, _searchCursorPosition + 1);
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
                    _searchCursorPosition = ctrl
                        ? FindPreviousSearchWordBoundary()
                        : Math.Max(0, _searchCursorPosition - 1);
                    BeginHeldKey(Keys.Left, tickCount);
                }

                return true;
            }

            if (keyboardState.IsKeyDown(Keys.Right))
            {
                if (_previousSearchKeyboardState.IsKeyUp(Keys.Right))
                {
                    _searchCursorPosition = ctrl
                        ? FindNextSearchWordBoundary()
                        : Math.Min(_searchQuery.Length, _searchCursorPosition + 1);
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

        private int FindPreviousSearchWordBoundary()
        {
            if (_searchCursorPosition <= 0 || string.IsNullOrEmpty(_searchQuery))
            {
                return 0;
            }

            int cursor = _searchCursorPosition;
            while (cursor > 0 && char.IsWhiteSpace(_searchQuery[cursor - 1]))
            {
                cursor--;
            }

            while (cursor > 0 && !char.IsWhiteSpace(_searchQuery[cursor - 1]))
            {
                cursor--;
            }

            return cursor;
        }

        private int FindNextSearchWordBoundary()
        {
            if (_searchCursorPosition >= _searchQuery.Length || string.IsNullOrEmpty(_searchQuery))
            {
                return _searchQuery.Length;
            }

            int cursor = _searchCursorPosition;
            while (cursor < _searchQuery.Length && char.IsWhiteSpace(_searchQuery[cursor]))
            {
                cursor++;
            }

            while (cursor < _searchQuery.Length && !char.IsWhiteSpace(_searchQuery[cursor]))
            {
                cursor++;
            }

            return cursor;
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
            if (entry == null)
            {
                return string.Empty;
            }

            return $"{entry.Kind}:{entry.MapId}:{entry.Label}:{entry.Description}";
        }

        public override bool CanStartDragAt(int x, int y)
        {
            if (!base.CanStartDragAt(x, y))
            {
                return false;
            }

            if (!_searchMode)
            {
                return true;
            }

            if (GetSearchInputBounds().Contains(x, y))
            {
                return false;
            }

            Rectangle candidateBounds = GetImeCandidateWindowBounds(BuildOwnerViewport());
            if (!candidateBounds.IsEmpty && candidateBounds.Contains(x, y))
            {
                return false;
            }

            Rectangle searchResultBounds = GetSearchResultListBounds(GetVisibleSearchResults().Count);
            return searchResultBounds.IsEmpty || !searchResultBounds.Contains(x, y);
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            foreach (Rectangle bounds in base.GetAdditionalInteractiveBounds())
            {
                yield return bounds;
            }

            if (!_searchMode)
            {
                yield break;
            }

            Rectangle candidateBounds = GetImeCandidateWindowBounds(BuildOwnerViewport());
            if (!candidateBounds.IsEmpty)
            {
                yield return candidateBounds;
            }
        }

        private bool IsSearchResultSelected(SearchResultEntry entry)
        {
            return entry != null
                && string.Equals(BuildSearchResultKey(entry), _selectedSearchResultKey, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSearchResultHovered(SearchResultEntry entry)
        {
            return entry != null
                && string.Equals(BuildSearchResultKey(entry), _hoveredSearchResultKey, StringComparison.OrdinalIgnoreCase);
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
            ClearCompositionText();
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

                ClearCompositionText();
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

        private void InsertSearchText(string text)
        {
            string sanitized = SanitizeSearchText(text);
            if (string.IsNullOrEmpty(sanitized))
            {
                return;
            }

            int availableLength = MaxSearchQueryLength - _searchQuery.Length;
            if (availableLength <= 0)
            {
                return;
            }

            if (sanitized.Length > availableLength)
            {
                sanitized = sanitized[..availableLength];
            }

            _searchQuery = _searchQuery.Insert(_searchCursorPosition, sanitized);
            _searchCursorPosition += sanitized.Length;
            OnSearchQueryChanged();
        }

        private static string SanitizeSearchText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return string.Concat(text
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Where(static character => !char.IsControl(character)));
        }

        private SearchInputVisualState BuildSearchInputVisualState()
        {
            (string displayText, int caretIndex, int compositionStart, int compositionLength) = BuildDisplayedSearchText();
            if (_font == null || string.IsNullOrEmpty(displayText))
            {
                return new SearchInputVisualState(string.Empty, 0, 0, 0, 0);
            }

            float maxWidth = SearchInputBoxWidth - (SearchTextInsetX * 2) - 2;
            int clampedCaretIndex = Math.Clamp(caretIndex, 0, displayText.Length);
            int visibleStart = 0;

            while (visibleStart < clampedCaretIndex)
            {
                if (_font.MeasureString(displayText[visibleStart..clampedCaretIndex]).X <= maxWidth)
                {
                    break;
                }

                visibleStart++;
            }

            int visibleLength = Math.Max(0, Math.Min(displayText.Length - visibleStart, Math.Max(1, clampedCaretIndex - visibleStart)));
            while (visibleStart + visibleLength < displayText.Length)
            {
                string candidate = displayText.Substring(visibleStart, visibleLength + 1);
                if (_font.MeasureString(candidate).X > maxWidth)
                {
                    break;
                }

                visibleLength++;
            }

            string visibleText = displayText.Substring(visibleStart, Math.Max(0, visibleLength));
            int visibleCaretIndex = Math.Clamp(clampedCaretIndex - visibleStart, 0, visibleText.Length);
            int visibleCompositionStart = 0;
            int visibleCompositionLength = 0;
            if (compositionLength > 0)
            {
                int compositionEnd = compositionStart + compositionLength;
                int visibleEnd = visibleStart + visibleText.Length;
                int overlapStart = Math.Max(compositionStart, visibleStart);
                int overlapEnd = Math.Min(compositionEnd, visibleEnd);
                if (overlapEnd > overlapStart)
                {
                    visibleCompositionStart = overlapStart - visibleStart;
                    visibleCompositionLength = overlapEnd - overlapStart;
                }
            }

            return new SearchInputVisualState(visibleText, visibleStart, visibleCaretIndex, visibleCompositionStart, visibleCompositionLength);
        }

        private void UpdateImePresentationPlacement()
        {
            if (!CapturesKeyboardInput
                || !_searchMode
                || _font == null
                || ResolveImeWindowHandle == null
                || (_compositionText.Length == 0 && !_candidateListState.HasCandidates))
            {
                return;
            }

            IntPtr windowHandle = ResolveImeWindowHandle();
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            Rectangle searchInputBounds = GetSearchInputBounds();
            SearchInputVisualState searchVisual = BuildSearchInputVisualState();
            int visibleCaretIndex = Math.Clamp(searchVisual.VisibleCaretIndex, 0, searchVisual.VisibleText.Length);
            string caretPrefix = visibleCaretIndex <= 0
                ? string.Empty
                : searchVisual.VisibleText[..visibleCaretIndex];
            int compositionCaretWidth = MeasureImePlacementWidth(caretPrefix);

            bool useClauseAnchor = searchVisual.VisibleCompositionLength > 0;
            int clauseAnchorIndex = useClauseAnchor
                ? Math.Clamp(searchVisual.VisibleCompositionStart + ResolveCompositionAnchorIndex(), 0, searchVisual.VisibleText.Length)
                : visibleCaretIndex;
            string clausePrefix = clauseAnchorIndex <= 0
                ? string.Empty
                : searchVisual.VisibleText[..clauseAnchorIndex];
            int clauseAnchorWidth = MeasureImePlacementWidth(clausePrefix);
            int clauseWidth = useClauseAnchor
                ? MeasureImePlacementWidth(ResolveActiveCompositionClauseText())
                : 1;

            SkillMacroImeWindowPlacement placement = SkillMacroImeWindowPlacementLayout.Resolve(
                searchInputBounds,
                SearchTextInsetX,
                _font.LineSpacing,
                compositionCaretWidth,
                useClauseAnchor,
                clauseAnchorWidth,
                clauseWidth);
            WindowsImePresentationBridge.TryUpdatePlacement(windowHandle, placement);
        }

        private int MeasureImePlacementWidth(string text)
        {
            if (_font == null || string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return (int)Math.Round(_font.MeasureString(text).X);
        }

        private string ResolveActiveCompositionClauseText()
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return string.Empty;
            }

            if (_compositionClauseOffsets.Count >= 2)
            {
                int cursor = Math.Clamp(_compositionCursorPosition, 0, _compositionText.Length);
                for (int i = 0; i < _compositionClauseOffsets.Count - 1; i++)
                {
                    int start = Math.Clamp(_compositionClauseOffsets[i], 0, _compositionText.Length);
                    int end = Math.Clamp(_compositionClauseOffsets[i + 1], start, _compositionText.Length);
                    if (cursor >= start && cursor <= end)
                    {
                        return _compositionText[start..end];
                    }
                }
            }

            return _compositionText;
        }

        private Point ResolveCandidateWindowOrigin(Viewport viewport, int width, int height)
        {
            if (TryResolveCandidateWindowOriginFromWindowForm(viewport, width, height, out Point windowFormOrigin))
            {
                return windowFormOrigin;
            }

            Rectangle bounds = GetSearchInputBounds();
            SearchInputVisualState searchVisual = BuildSearchInputVisualState();
            int anchorIndex = searchVisual.VisibleCompositionLength > 0
                ? Math.Clamp(searchVisual.VisibleCompositionStart + ResolveCompositionAnchorIndex(), 0, searchVisual.VisibleText.Length)
                : Math.Clamp(searchVisual.VisibleCaretIndex, 0, searchVisual.VisibleText.Length);
            string prefix = anchorIndex <= 0
                ? string.Empty
                : searchVisual.VisibleText[..anchorIndex];
            int x = bounds.X + SearchTextInsetX + (int)Math.Round(_font.MeasureString(prefix).X);
            if (_candidateListState.Vertical)
            {
                x -= _font.LineSpacing + 4;
            }

            return new Point(x, bounds.Bottom + 1);
        }

        private bool TryResolveCandidateWindowOriginFromWindowForm(Viewport viewport, int width, int height, out Point origin)
        {
            ImeCandidateWindowForm windowForm = _candidateListState?.WindowForm;
            if (windowForm == null || !windowForm.HasPlacementData)
            {
                origin = Point.Zero;
                return false;
            }

            int viewportWidth = Math.Max(1, viewport.Width);
            int viewportHeight = Math.Max(1, viewport.Height);
            uint style = windowForm.Style;

            int x = windowForm.CurrentX;
            int y = windowForm.CurrentY;

            if ((style & CandidateWindowStyleExclude) != 0 && windowForm.AreaWidth > 0 && windowForm.AreaHeight > 0)
            {
                x = windowForm.CurrentX;
                y = windowForm.AreaY + windowForm.AreaHeight + 1;
                if (y + height > viewportHeight)
                {
                    y = windowForm.AreaY - height - 1;
                }
            }
            else if ((style & (CandidateWindowStyleForcePosition | CandidateWindowStyleCandidatePosition | CandidateWindowStylePoint)) != 0)
            {
                y = windowForm.CurrentY + 1;
            }
            else if ((style & CandidateWindowStyleRect) != 0 && windowForm.AreaWidth > 0 && windowForm.AreaHeight > 0)
            {
                x = windowForm.AreaX;
                y = windowForm.AreaY + windowForm.AreaHeight + 1;
                if (y + height > viewportHeight)
                {
                    y = windowForm.AreaY - height - 1;
                }
            }
            else
            {
                origin = Point.Zero;
                return false;
            }

            Rectangle ownerBounds = GetOwnerBounds();
            int minX = Math.Max(0, ownerBounds.X);
            int maxX = ownerBounds.Width > 0
                ? Math.Min(viewportWidth, ownerBounds.Right) - width
                : viewportWidth - width;
            if (maxX < minX)
            {
                minX = 0;
                maxX = Math.Max(0, viewportWidth - width);
            }

            int minY = Math.Max(0, ownerBounds.Y);
            int maxY = ownerBounds.Height > 0
                ? Math.Min(viewportHeight, ownerBounds.Bottom) - height
                : viewportHeight - height;
            if (maxY < minY)
            {
                minY = 0;
                maxY = Math.Max(0, viewportHeight - height);
            }

            origin = new Point(
                Math.Clamp(x, minX, Math.Max(minX, maxX)),
                Math.Clamp(y, minY, Math.Max(minY, maxY)));
            return true;
        }

        private int ResolveCompositionAnchorIndex()
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return 0;
            }

            if (_compositionClauseOffsets.Count >= 2)
            {
                int cursor = Math.Clamp(_compositionCursorPosition, 0, _compositionText.Length);
                for (int i = 0; i < _compositionClauseOffsets.Count - 1; i++)
                {
                    int start = Math.Clamp(_compositionClauseOffsets[i], 0, _compositionText.Length);
                    int end = Math.Clamp(_compositionClauseOffsets[i + 1], start, _compositionText.Length);
                    if (cursor >= start && cursor <= end)
                    {
                        return start;
                    }
                }
            }

            return _compositionCursorPosition >= 0
                ? Math.Clamp(_compositionCursorPosition, 0, _compositionText.Length)
                : _compositionText.Length;
        }

        private (string DisplayText, int CaretIndex, int CompositionStart, int CompositionLength) BuildDisplayedSearchText()
        {
            if (string.IsNullOrEmpty(_compositionText))
            {
                return (_searchQuery, Math.Clamp(_searchCursorPosition, 0, _searchQuery.Length), -1, 0);
            }

            int insertionIndex = Math.Clamp(_compositionInsertionIndex >= 0 ? _compositionInsertionIndex : _searchCursorPosition, 0, _searchQuery.Length);
            string displayText = _searchQuery.Insert(insertionIndex, _compositionText);
            return (displayText, insertionIndex + _compositionText.Length, insertionIndex, _compositionText.Length);
        }

        private int GetVisibleCandidateCount()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            int pageStart = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int pageSize = _candidateListState.PageSize > 0 ? _candidateListState.PageSize : _candidateListState.Candidates.Count;
            return Math.Max(0, Math.Min(pageSize, _candidateListState.Candidates.Count - pageStart));
        }

        private int GetCandidatePageSize()
        {
            if (!_candidateListState.HasCandidates)
            {
                return 0;
            }

            return Math.Max(1, _candidateListState.PageSize > 0 ? _candidateListState.PageSize : GetVisibleCandidateCount());
        }

        private int GetCandidateRowHeight()
        {
            return Math.Max(_font.LineSpacing + 1, 16);
        }

        private int GetHorizontalCandidateCellWidth()
        {
            if (_font == null || !_candidateListState.HasCandidates)
            {
                return 28;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            int widestCell = 0;
            for (int i = 0; i < count; i++)
            {
                int candidateIndex = start + i;
                string numberText = $"{i + 1}.";
                string candidateText = _candidateListState.Candidates[candidateIndex] ?? string.Empty;
                int cellWidth = (int)Math.Ceiling(_font.MeasureString(numberText).X + _font.MeasureString(candidateText).X) + CandidateWindowPadding + 6;
                widestCell = Math.Max(widestCell, cellWidth);
            }

            return Math.Max(2 * (_font.LineSpacing + 4), widestCell);
        }

        private int GetHorizontalCandidateWindowWidth()
        {
            int pageSize = GetCandidatePageSize();
            if (pageSize <= 0)
            {
                return 64;
            }

            return (GetHorizontalCandidateCellWidth() * pageSize) + CandidateWindowPadding;
        }

        private int GetCandidateNumberWidth()
        {
            int visibleCount = Math.Max(1, GetVisibleCandidateCount());
            return (int)Math.Ceiling(_font.MeasureString($"{visibleCount}.").X);
        }

        private bool IsPointInImeCandidateWindow(int mouseX, int mouseY)
        {
            Rectangle candidateBounds = GetImeCandidateWindowBounds(BuildOwnerViewport());
            return !candidateBounds.IsEmpty && candidateBounds.Contains(mouseX, mouseY);
        }

        private int ResolveImeCandidateIndexFromPoint(int mouseX, int mouseY)
        {
            if (!_candidateListState.HasCandidates)
            {
                return -1;
            }

            int start = Math.Clamp(_candidateListState.PageStart, 0, _candidateListState.Candidates.Count);
            int count = Math.Min(GetVisibleCandidateCount(), _candidateListState.Candidates.Count - start);
            if (count <= 0)
            {
                return -1;
            }

            Rectangle candidateBounds = GetImeCandidateWindowBounds(BuildOwnerViewport());
            int localIndex = SkillMacroImeCandidateWindowLayout.HitTestCandidate(
                candidateBounds,
                new Point(mouseX, mouseY),
                _candidateListState.Vertical,
                count,
                GetCandidateRowHeight(),
                GetHorizontalCandidateCellWidth());
            return localIndex >= 0
                ? start + localIndex
                : -1;
        }

        private static IReadOnlyList<int> ClampClauseOffsets(IReadOnlyList<int> offsets, int maxLength)
        {
            if (offsets == null || offsets.Count == 0)
            {
                return Array.Empty<int>();
            }

            List<int> clamped = new(offsets.Count);
            foreach (int offset in offsets)
            {
                int safeOffset = Math.Clamp(offset, 0, maxLength);
                if (clamped.Count == 0 || safeOffset >= clamped[^1])
                {
                    clamped.Add(safeOffset);
                }
            }

            if (clamped.Count == 0)
            {
                return Array.Empty<int>();
            }

            if (clamped[^1] != maxLength)
            {
                clamped.Add(maxLength);
            }

            return clamped;
        }

        private SearchResultVisualStyle? GetResultStyle(SearchResultKind kind)
        {
            if (_resultStyles.TryGetValue(kind, out SearchResultVisualStyle style))
            {
                return style;
            }

            return kind == SearchResultKind.Item
                ? (_resultStyles.TryGetValue(SearchResultKind.Npc, out style)
                    ? style
                    : _resultStyles.TryGetValue(SearchResultKind.Field, out style)
                        ? style
                        : null)
                : null;
        }

        private void HandleSearchMouseInput(MouseState mouseState)
        {
            bool leftClicked = mouseState.LeftButton == ButtonState.Pressed
                && _previousMouseState.LeftButton == ButtonState.Released;
            if (!leftClicked)
            {
                return;
            }

            if (IsPointInImeCandidateWindow(mouseState.X, mouseState.Y))
            {
                int candidateIndex = ResolveImeCandidateIndexFromPoint(mouseState.X, mouseState.Y);
                if (candidateIndex >= 0)
                {
                    OnImeCandidateSelected?.Invoke(_candidateListState.ListIndex, candidateIndex);
                    _caretBlinkTick = Environment.TickCount;
                }

                return;
            }

            if (!GetSearchInputBounds().Contains(mouseState.X, mouseState.Y))
            {
                return;
            }

            _softKeyboardActive = true;
            _caretBlinkTick = Environment.TickCount;
            ClearCompositionText();
            _searchCursorPosition = ResolveSearchCursorFromMouse(mouseState.X);
            ResetSearchKeyRepeat();
        }

        Rectangle ISoftKeyboardHost.GetSoftKeyboardAnchorBounds() => GetSearchInputBounds();

        bool ISoftKeyboardHost.TryInsertSoftKeyboardCharacter(char character, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!_searchMode
                || char.IsControl(character)
                || !SoftKeyboardUI.CanAcceptCharacter(SoftKeyboardKeyboardType.AlphaNumeric, _searchQuery.Length, MaxSearchQueryLength, character))
            {
                errorMessage = "The world-map search field cannot accept that character.";
                return false;
            }

            string previousQuery = _searchQuery;
            HandleCommittedText(character.ToString());
            if (string.Equals(previousQuery, _searchQuery, StringComparison.Ordinal))
            {
                errorMessage = "The world-map search field cannot accept that character.";
                return false;
            }

            return true;
        }

        bool ISoftKeyboardHost.TryReplaceLastSoftKeyboardCharacter(char character, out string errorMessage)
        {
            if (!((ISoftKeyboardHost)this).TryBackspaceSoftKeyboard(out errorMessage))
            {
                return false;
            }

            return ((ISoftKeyboardHost)this).TryInsertSoftKeyboardCharacter(character, out errorMessage);
        }

        bool ISoftKeyboardHost.TryBackspaceSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!_searchMode)
            {
                errorMessage = "World-map search is not active.";
                return false;
            }

            if (!SkillMacroNameRules.TryRemoveTextElementBeforeCaret(_searchQuery, _searchCursorPosition, out string updatedText, out int updatedCaretIndex))
            {
                errorMessage = "The world-map search field is already empty.";
                return false;
            }

            ClearCompositionText();
            _searchQuery = updatedText;
            _searchCursorPosition = updatedCaretIndex;
            OnSearchQueryChanged();
            return true;
        }

        bool ISoftKeyboardHost.TrySubmitSoftKeyboard(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!_searchMode)
            {
                errorMessage = "World-map search is not active.";
                return false;
            }

            if (GetFilteredSearchResults().Count <= 0)
            {
                errorMessage = "No world-map search results are available.";
                return false;
            }

            ActivateSearchRow(GetSelectedSearchRowIndex());
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

        private void UpdateHoveredSearchResult(MouseState mouseState)
        {
            _hoveredSearchResultKey = string.Empty;

            IReadOnlyList<SearchResultEntry> visibleResults = GetVisibleSearchResults();
            for (int row = 0; row < visibleResults.Count; row++)
            {
                Rectangle rowBounds = new Rectangle(
                    Position.X + ListStartX,
                    Position.Y + ListStartY + (row * RowHeight),
                    ListWidth,
                    RowHeight);
                if (!rowBounds.Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                _hoveredSearchResultKey = BuildSearchResultKey(visibleResults[row]);
                break;
            }
        }

        private int ResolveSearchCursorFromMouse(int mouseX)
        {
            if (_font == null || string.IsNullOrEmpty(_searchQuery))
            {
                return 0;
            }

            Rectangle bounds = GetSearchInputBounds();
            SearchInputVisualState searchVisual = BuildSearchInputVisualState();
            int visibleStart = searchVisual.VisibleStart;
            string visibleText = searchVisual.VisibleText;
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

        private sealed class SearchInputVisualState
        {
            public SearchInputVisualState(string visibleText, int visibleStart, int visibleCaretIndex, int visibleCompositionStart, int visibleCompositionLength)
            {
                VisibleText = visibleText;
                VisibleStart = visibleStart;
                VisibleCaretIndex = visibleCaretIndex;
                VisibleCompositionStart = visibleCompositionStart;
                VisibleCompositionLength = visibleCompositionLength;
            }

            public string VisibleText { get; }
            public int VisibleStart { get; }
            public int VisibleCaretIndex { get; }
            public int VisibleCompositionStart { get; }
            public int VisibleCompositionLength { get; }
        }
    }
}
