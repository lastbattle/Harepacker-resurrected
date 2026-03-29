using HaCreator.MapSimulator.Character;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
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
    public sealed class BookCollectionWindow : UIWindowBase
    {
        private readonly struct SearchMatch
        {
            public SearchMatch(int gradeIndex, int pageIndex, int slotIndex) { GradeIndex = gradeIndex; PageIndex = pageIndex; SlotIndex = slotIndex; }
            public int GradeIndex { get; }
            public int PageIndex { get; }
            public int SlotIndex { get; }
        }

        private readonly struct TabVisual
        {
            public TabVisual(Texture2D normal, Texture2D selected, Texture2D hover, Texture2D disabled, Texture2D icon) { Normal = normal; Selected = selected; Hover = hover; Disabled = disabled; Icon = icon; }
            public Texture2D Normal { get; }
            public Texture2D Selected { get; }
            public Texture2D Hover { get; }
            public Texture2D Disabled { get; }
            public Texture2D Icon { get; }
        }

        private const int PrevButtonId = 1000;
        private const int NextButtonId = 1001;
        private const int SearchButtonId = 1002;
        private const int CardsPerPage = 25;
        private const int MaxSearchLength = 32;

        private static readonly Point CardSlotOrigin = new(24, 22);
        private static readonly Point InfoPageOrigin = new(278, 36);
        private static readonly Point CardCellPadding = new(5, 14);
        private static readonly Point CardCellStride = new(33, 45);
        private static readonly Point LeftTabInfoOrigin = new(2, 18);
        private static readonly Point LeftTabOrigin = new(5, 56);
        private static readonly Point RightTabOrigin = new(414, 38);
        private static readonly Point SummaryValueOrigin = new(98, 90);
        private static readonly Point PageMarkerAnchor = new(236, 296);
        private static readonly Rectangle SelectedCardIconBounds = new(69, 3, 32, 32);
        private static readonly Rectangle SelectedCardNameBounds = new(10, 38, 151, 16);
        private static readonly Rectangle SelectedCardDetailBounds = new(10, 54, 151, 20);
        private static readonly Rectangle DetailBodyBounds = new(11, 76, 147, 108);
        private static readonly Rectangle SearchBoxBounds = new(12, 262, 150, 17);
        private static readonly Rectangle SearchStatusBounds = new(14, 242, 144, 14);
        private static readonly Rectangle LeftPageIndexBounds = new(143, 286, 84, 18);
        private static readonly Rectangle RightPageIndexBounds = new(249, 286, 84, 18);
        private static readonly Rectangle StatusBounds = new(16, 286, 184, 18);
        private static readonly Color TitleColor = new(82, 59, 29);
        private static readonly Color ValueColor = new(56, 45, 33);
        private static readonly Color AccentColor = new(173, 120, 48);
        private static readonly Color MutedColor = new(128, 118, 103);
        private static readonly Color HiddenTint = new(255, 255, 255, 66);

        private readonly Texture2D _pixel;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Action _closeRequested;
        private readonly Dictionary<int, Action> _buttonActions = new();
        private readonly Dictionary<int, Texture2D> _cardIconCache = new();
        private readonly List<SearchMatch> _searchMatches = new();

        private Texture2D _cardSlotTexture;
        private Texture2D _infoPageTexture;
        private Texture2D _coveredSlotTexture;
        private Texture2D _selectedSlotTexture;
        private Texture2D _fullMarkTexture;
        private Texture2D _inactivePageMarkerTexture;
        private Texture2D _activePageMarkerTexture;
        private Texture2D _contextMenuTopTexture;
        private Texture2D _contextMenuCenterTexture;
        private Texture2D _contextMenuBottomTexture;
        private TabVisual _leftTabInfoVisual;
        private IReadOnlyList<TabVisual> _leftTabs = Array.Empty<TabVisual>();
        private IReadOnlyList<TabVisual> _rightTabs = Array.Empty<TabVisual>();
        private IReadOnlyList<MonsterBookDetailTab> _rightTabOrder = new[] { MonsterBookDetailTab.BasicInfo, MonsterBookDetailTab.Episode, MonsterBookDetailTab.Rewards, MonsterBookDetailTab.Habitat };
        private SpriteFont _font;
        private Func<MonsterBookSnapshot> _snapshotProvider;
        private Func<int, bool, MonsterBookSnapshot> _registrationHandler;
        private MonsterBookSnapshot _snapshot;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private UIObject _prevButton;
        private UIObject _nextButton;
        private UIObject _searchButton;
        private UIObject _registerButton;
        private UIObject _releaseButton;
        private int _selectedLeftTabIndex;
        private int _currentGradeIndex;
        private int _currentPageIndex;
        private int _selectedSlotIndex;
        private int _hoveredLeftTab = -1;
        private int _hoveredRightTab = -1;
        private int _selectedSearchMatchIndex = -1;
        private int _contextMenuMobId;
        private MonsterBookDetailTab _detailTab;
        private bool _searchMode;
        private bool _contextMenuVisible;
        private string _searchQuery = string.Empty;
        private string _compositionText = string.Empty;
        private Point _contextMenuPosition;

        public BookCollectionWindow(IDXObject frame, Texture2D pixel, GraphicsDevice graphicsDevice, Action closeRequested = null) : base(frame)
        {
            _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _closeRequested = closeRequested;
        }

        public override string WindowName => MapSimulatorWindowNames.BookCollection;
        public override bool CapturesKeyboardInput => IsVisible && _searchMode;
        public override void SetFont(SpriteFont font) => _font = font;
        public override void HandleCompositionText(string text) => _compositionText = CapturesKeyboardInput ? text ?? string.Empty : string.Empty;
        public override void ClearCompositionText() => _compositionText = string.Empty;

        public override void HandleCommittedText(string text)
        {
            if (!CapturesKeyboardInput || string.IsNullOrEmpty(text)) return;
            ClearCompositionText();
            _searchQuery = (_searchQuery + text).TrimStart();
            if (_searchQuery.Length > MaxSearchLength) _searchQuery = _searchQuery[..MaxSearchLength];
            RefreshSearchMatches(true, false);
        }

        public void SetMonsterBookSnapshotProvider(Func<MonsterBookSnapshot> snapshotProvider) => _snapshotProvider = snapshotProvider;
        public void SetMonsterBookRegistrationHandler(Func<int, bool, MonsterBookSnapshot> registrationHandler) => _registrationHandler = registrationHandler;

        public void SetMonsterBookArt(Texture2D cardSlotTexture, Texture2D infoPageTexture, Texture2D coveredSlotTexture, Texture2D selectedSlotTexture, Texture2D fullMarkTexture)
        {
            _cardSlotTexture = cardSlotTexture; _infoPageTexture = infoPageTexture; _coveredSlotTexture = coveredSlotTexture; _selectedSlotTexture = selectedSlotTexture; _fullMarkTexture = fullMarkTexture;
        }

        public void SetMonsterBookTabArt(Texture2D leftTabInfoNormal, Texture2D leftTabInfoSelected, IReadOnlyList<Texture2D> leftNormals, IReadOnlyList<Texture2D> leftSelected, IReadOnlyList<Texture2D> leftHover, IReadOnlyList<Texture2D> leftIcons, IReadOnlyList<Texture2D> rightNormals, IReadOnlyList<Texture2D> rightSelected, IReadOnlyList<Texture2D> rightHover, IReadOnlyList<Texture2D> rightDisabled, IReadOnlyList<MonsterBookDetailTab> rightTabOrder)
        {
            _leftTabInfoVisual = new TabVisual(leftTabInfoNormal, leftTabInfoSelected, leftTabInfoNormal, null, null);
            _leftTabs = BuildTabs(leftNormals, leftSelected, leftHover, null, leftIcons);
            _rightTabs = BuildTabs(rightNormals, rightSelected, rightHover, rightDisabled, null);
            if (rightTabOrder?.Count > 0)
            {
                _rightTabOrder = rightTabOrder.ToArray();
            }
        }

        public void SetMonsterBookContextMenuArt(Texture2D topTexture, Texture2D centerTexture, Texture2D bottomTexture) { _contextMenuTopTexture = topTexture; _contextMenuCenterTexture = centerTexture; _contextMenuBottomTexture = bottomTexture; }
        public void SetPageMarkerTextures(Texture2D inactiveMarkerTexture, Texture2D activeMarkerTexture) { _inactivePageMarkerTexture = inactiveMarkerTexture; _activePageMarkerTexture = activeMarkerTexture; }

        public void InitializeButtons(UIObject prevButton, UIObject nextButton, UIObject closeButton, UIObject searchButton = null)
        {
            _prevButton = prevButton; _nextButton = nextButton; _searchButton = searchButton;
            RegisterButton(prevButton, PrevButtonId, () => MoveSpread(-1));
            RegisterButton(nextButton, NextButtonId, () => MoveSpread(1));
            RegisterButton(searchButton, SearchButtonId, ToggleSearchMode);
            InitializeCloseButton(closeButton);
        }

        public void InitializeContextMenuButtons(UIObject registerButton, UIObject releaseButton)
        {
            _registerButton = registerButton; _releaseButton = releaseButton;
            if (_registerButton != null) { AddButton(_registerButton); _registerButton.ButtonVisible = false; _registerButton.ButtonClickReleased += _ => ApplyRegistration(true); }
            if (_releaseButton != null) { AddButton(_releaseButton); _releaseButton.ButtonVisible = false; _releaseButton.ButtonClickReleased += _ => ApplyRegistration(false); }
        }

        public override void Show()
        {
            RefreshSnapshot();
            SelectCardOnCurrentPage();
            _previousMouseState = Mouse.GetState();
            _previousKeyboardState = Keyboard.GetState();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible) return;
            RefreshSnapshot();
            UpdateHoverState();
            HandleKeyboardInput();
            HandleMouseInput();
            UpdateContextButtons();
            UpdateButtonStates();
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, int centerX, int centerY, ReflectionDrawableBoundary drawReflectionInfo, RenderParameters renderParameters, int TickCount)
        {
            if (_font == null) return;
            DrawLeftTabs(sprite);
            DrawRightTabs(sprite);
            DrawCardSlotPanel(sprite);
            DrawInfoPanel(sprite);
            DrawSearchBox(sprite);
            DrawPageIndex(sprite);
            DrawPageMarkers(sprite);
            DrawStatus(sprite);
            DrawContextMenu(sprite);
        }

        protected override void OnCloseButtonClicked(UIObject sender) { CloseBook(); _closeRequested?.Invoke(); }
        public void CloseBook() { ResetBookState(); base.Hide(); }
        public override void Hide() { ResetBookState(); base.Hide(); }

        private static IReadOnlyList<TabVisual> BuildTabs(IReadOnlyList<Texture2D> normals, IReadOnlyList<Texture2D> selected, IReadOnlyList<Texture2D> hover, IReadOnlyList<Texture2D> disabled, IReadOnlyList<Texture2D> icons)
        {
            int count = new[] { normals?.Count ?? 0, selected?.Count ?? 0, hover?.Count ?? 0, disabled?.Count ?? 0, icons?.Count ?? 0 }.Max();
            List<TabVisual> tabs = new(count);
            for (int i = 0; i < count; i++) tabs.Add(new TabVisual(i < (normals?.Count ?? 0) ? normals[i] : null, i < (selected?.Count ?? 0) ? selected[i] : null, i < (hover?.Count ?? 0) ? hover[i] : null, i < (disabled?.Count ?? 0) ? disabled[i] : null, i < (icons?.Count ?? 0) ? icons[i] : null));
            return tabs;
        }

        private void RegisterButton(UIObject button, int buttonId, Action action)
        {
            if (button == null) return;
            AddButton(button);
            _buttonActions[buttonId] = action;
            button.ButtonClickReleased += _ => { if (_buttonActions.TryGetValue(buttonId, out Action handler)) handler?.Invoke(); };
        }

        private void RefreshSnapshot()
        {
            int mobId = GetSelectedCard()?.MobId ?? _contextMenuMobId;
            _snapshot = _snapshotProvider?.Invoke() ?? new MonsterBookSnapshot();
            _selectedLeftTabIndex = Math.Clamp(_selectedLeftTabIndex, 0, _leftTabs.Count);
            _currentGradeIndex = Math.Clamp(_currentGradeIndex, 0, Math.Max(0, (_snapshot.Grades?.Count ?? 1) - 1));
            _currentPageIndex = Math.Clamp(_currentPageIndex, 0, Math.Max(0, (GetCurrentGrade()?.Pages?.Count ?? 1) - 1));
            RestoreSelection(mobId);
            RefreshSearchMatches(false, true);
        }

        private void RestoreSelection(int mobId)
        {
            IReadOnlyList<MonsterBookCardSnapshot> cards = GetCurrentPageCards();
            if (cards.Count == 0) { _selectedSlotIndex = 0; return; }
            int index = mobId > 0 ? cards.ToList().FindIndex(card => card?.MobId == mobId) : -1;
            _selectedSlotIndex = index >= 0 ? index : Math.Clamp(_selectedSlotIndex, 0, cards.Count - 1);
        }

        private bool IsOverviewTabSelected => _selectedLeftTabIndex <= 0;

        private MonsterBookGradeSnapshot GetCurrentGrade()
        {
            if (IsOverviewTabSelected || _snapshot?.Grades == null)
            {
                return null;
            }

            _currentGradeIndex = Math.Clamp(_selectedLeftTabIndex - 1, 0, Math.Max(0, _snapshot.Grades.Count - 1));
            return _snapshot.Grades[_currentGradeIndex];
        }

        private IReadOnlyList<MonsterBookCardSnapshot> GetCurrentPageCards() => GetCurrentGrade()?.Pages != null && _currentPageIndex >= 0 && _currentPageIndex < GetCurrentGrade().Pages.Count ? GetCurrentGrade().Pages[_currentPageIndex].Cards ?? Array.Empty<MonsterBookCardSnapshot>() : Array.Empty<MonsterBookCardSnapshot>();
        private MonsterBookCardSnapshot GetSelectedCard() => _selectedSlotIndex >= 0 && _selectedSlotIndex < GetCurrentPageCards().Count ? GetCurrentPageCards()[_selectedSlotIndex] : null;

        private void SelectCardOnCurrentPage()
        {
            IReadOnlyList<MonsterBookCardSnapshot> cards = GetCurrentPageCards();
            _selectedSlotIndex = cards.Count == 0 ? 0 : Math.Max(0, cards.ToList().FindIndex(card => card.IsDiscovered));
        }

        private void MoveSpread(int direction)
        {
            if (IsOverviewTabSelected)
            {
                return;
            }

            int totalPages = GetTotalPageCount();
            if (totalPages <= 0)
            {
                return;
            }

            int spreadStart = GetSpreadStartAbsolutePageIndex(GetAbsolutePageIndex());
            int maxSpreadStart = GetSpreadStartAbsolutePageIndex(totalPages - 1);
            int targetSpreadStart = Math.Clamp(spreadStart + (direction * 2), 0, maxSpreadStart);
            if (TrySetAbsolutePageIndex(targetSpreadStart))
            {
                SelectCardOnCurrentPage();
            }
        }

        private void ToggleSearchMode()
        {
            _searchMode = !_searchMode;
            if (!_searchMode) { _searchQuery = string.Empty; _selectedSearchMatchIndex = -1; _searchMatches.Clear(); ClearCompositionText(); }
            else RefreshSearchMatches(false, false);
        }

        private void ApplyRegistration(bool registered)
        {
            MonsterBookCardSnapshot card = GetSelectedCard();
            if (card?.IsDiscovered != true || _registrationHandler == null) return;
            _snapshot = _registrationHandler.Invoke(card.MobId, registered) ?? _snapshot;
            _contextMenuVisible = false;
            RefreshSnapshot();
        }

        private void RefreshSearchMatches(bool selectFirst, bool preserveSelection)
        {
            int previousIndex = _selectedSearchMatchIndex;
            _searchMatches.Clear();
            if (string.IsNullOrWhiteSpace(_searchQuery)) { _selectedSearchMatchIndex = -1; return; }

            string query = _searchQuery.Trim();
            for (int g = 0; g < (_snapshot?.Grades?.Count ?? 0); g++)
            for (int p = 0; p < (_snapshot.Grades[g]?.Pages?.Count ?? 0); p++)
            for (int s = 0; s < (_snapshot.Grades[g].Pages[p]?.Cards?.Count ?? 0); s++)
            {
                MonsterBookCardSnapshot card = _snapshot.Grades[g].Pages[p].Cards[s];
                if (card == null) continue;
                if (card.Name?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || card.SearchText?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(card.MobId.ToString(CultureInfo.InvariantCulture), query, StringComparison.OrdinalIgnoreCase))
                {
                    _searchMatches.Add(new SearchMatch(g, p, s));
                }
            }

            if (_searchMatches.Count == 0) { _selectedSearchMatchIndex = -1; return; }
            _selectedSearchMatchIndex = preserveSelection && previousIndex >= 0
                ? Math.Clamp(previousIndex, 0, _searchMatches.Count - 1)
                : selectFirst || _selectedSearchMatchIndex < 0
                    ? 0
                    : Math.Clamp(_selectedSearchMatchIndex, 0, _searchMatches.Count - 1);
            ApplySearchMatch(_selectedSearchMatchIndex);
        }

        private void ApplySearchMatch(int index)
        {
            if (index < 0 || index >= _searchMatches.Count) return;
            SearchMatch match = _searchMatches[index];
            _selectedLeftTabIndex = match.GradeIndex + 1;
            _currentGradeIndex = match.GradeIndex;
            _currentPageIndex = match.PageIndex;
            _selectedSlotIndex = match.SlotIndex;
            _selectedSearchMatchIndex = index;
            _contextMenuVisible = false;
        }

        private void UpdateHoverState()
        {
            Point mouse = Mouse.GetState().Position;
            _hoveredLeftTab = -1;
            _hoveredRightTab = -1;
            for (int i = 0; i <= _leftTabs.Count; i++) if (GetLeftTabBounds(i).Contains(mouse)) { _hoveredLeftTab = i; break; }
            for (int i = 0; i < Math.Min(_rightTabs.Count, _rightTabOrder.Count); i++) if (GetRightTabBounds(i).Contains(mouse)) { _hoveredRightTab = i; break; }
        }

        private void HandleKeyboardInput()
        {
            KeyboardState keyboard = Keyboard.GetState();
            if (_searchMode)
            {
                if (WasPressed(keyboard, Keys.Escape)) ToggleSearchMode();
                else
                {
                    if (WasPressed(keyboard, Keys.Back) && _searchQuery.Length > 0) { _searchQuery = _searchQuery[..^1]; RefreshSearchMatches(true, false); }
                    if (WasPressed(keyboard, Keys.Delete) && _searchQuery.Length > 0) { _searchQuery = string.Empty; RefreshSearchMatches(false, false); }
                    if ((WasPressed(keyboard, Keys.Enter) || WasPressed(keyboard, Keys.Down)) && _searchMatches.Count > 0) ApplySearchMatch((_selectedSearchMatchIndex + 1 + _searchMatches.Count) % _searchMatches.Count);
                    if (WasPressed(keyboard, Keys.Up) && _searchMatches.Count > 0) ApplySearchMatch((_selectedSearchMatchIndex - 1 + _searchMatches.Count) % _searchMatches.Count);
                }
            }
            else if (_contextMenuVisible && WasPressed(keyboard, Keys.Escape))
            {
                _contextMenuVisible = false;
            }
            _previousKeyboardState = keyboard;
        }

        private void HandleMouseInput()
        {
            MouseState mouse = Mouse.GetState();
            bool leftReleased = mouse.LeftButton == ButtonState.Released && _previousMouseState.LeftButton == ButtonState.Pressed;
            bool rightReleased = mouse.RightButton == ButtonState.Released && _previousMouseState.RightButton == ButtonState.Pressed;
            Point point = mouse.Position;

            if (leftReleased)
            {
                if (_contextMenuVisible && !GetContextMenuBounds().Contains(point)) _contextMenuVisible = false;
                for (int i = 0; i <= _leftTabs.Count; i++) if (GetLeftTabBounds(i).Contains(point)) { _selectedLeftTabIndex = i; _currentGradeIndex = Math.Max(0, i - 1); _currentPageIndex = 0; SelectCardOnCurrentPage(); _contextMenuVisible = false; _previousMouseState = mouse; return; }
                for (int i = 0; i < Math.Min(_rightTabs.Count, _rightTabOrder.Count); i++) if (GetRightTabBounds(i).Contains(point) && !IsOverviewTabSelected) { _detailTab = _rightTabOrder[i]; _previousMouseState = mouse; return; }
                for (int i = 0; i < GetCurrentPageCards().Count; i++) if (GetCardBounds(i).Contains(point)) { _selectedSlotIndex = i; _contextMenuVisible = false; break; }
            }

            if (rightReleased)
            {
                for (int i = 0; i < GetCurrentPageCards().Count; i++) if (GetCardBounds(i).Contains(point))
                {
                    MonsterBookCardSnapshot card = GetCurrentPageCards()[i];
                    if (card?.IsDiscovered != true) break;
                    Rectangle bounds = GetCardBounds(i);
                    _selectedSlotIndex = i;
                    _contextMenuVisible = true;
                    _contextMenuMobId = card.MobId;
                    _contextMenuPosition = new Point(Math.Min(bounds.Right + 4, Position.X + 366), Math.Min(bounds.Bottom - 8, Position.Y + 276));
                    break;
                }
            }

            _previousMouseState = mouse;
        }

        private void UpdateContextButtons()
        {
            if (_registerButton != null) { _registerButton.X = _contextMenuPosition.X - Position.X + 8; _registerButton.Y = _contextMenuPosition.Y - Position.Y + 7; }
            if (_releaseButton != null) { _releaseButton.X = _contextMenuPosition.X - Position.X + 8; _releaseButton.Y = _contextMenuPosition.Y - Position.Y + 25; }
        }

        private void UpdateButtonStates()
        {
            int totalPages = GetTotalPageCount();
            int currentSpreadStart = GetSpreadStartAbsolutePageIndex(GetAbsolutePageIndex());
            int maxSpreadStart = GetSpreadStartAbsolutePageIndex(Math.Max(0, totalPages - 1));
            _prevButton?.SetEnabled(currentSpreadStart > 0);
            _nextButton?.SetEnabled(totalPages > 0 && currentSpreadStart < maxSpreadStart);
            if (_searchButton != null) _searchButton.SetButtonState(_searchMode ? UIObjectState.Pressed : UIObjectState.Normal);
            MonsterBookCardSnapshot card = GetSelectedCard();
            if (_registerButton != null) { _registerButton.ButtonVisible = _contextMenuVisible && card?.IsDiscovered == true && !card.IsRegistered; _registerButton.SetEnabled(_registerButton.ButtonVisible); }
            if (_releaseButton != null) { _releaseButton.ButtonVisible = _contextMenuVisible && card?.IsDiscovered == true && card.IsRegistered; _releaseButton.SetEnabled(_releaseButton.ButtonVisible); }
        }

        private void DrawLeftTabs(SpriteBatch sprite)
        {
            for (int i = 0; i <= _leftTabs.Count; i++)
            {
                Rectangle bounds = GetLeftTabBounds(i);
                DrawTab(sprite, bounds, i == 0 ? _leftTabInfoVisual : i - 1 < _leftTabs.Count ? _leftTabs[i - 1] : default, i == _selectedLeftTabIndex, i == _hoveredLeftTab, false);
                if (i > 0 && i - 1 < _leftTabs.Count && _leftTabs[i - 1].Icon != null)
                {
                    Texture2D icon = _leftTabs[i - 1].Icon;
                    sprite.Draw(icon, new Vector2(bounds.X + (bounds.Width - icon.Width) / 2, bounds.Y + (bounds.Height - icon.Height) / 2), Color.White);
                }
            }
        }

        private void DrawRightTabs(SpriteBatch sprite)
        {
            for (int i = 0; i < Math.Min(_rightTabs.Count, _rightTabOrder.Count); i++) DrawTab(sprite, GetRightTabBounds(i), _rightTabs[i], _rightTabOrder[i] == _detailTab, i == _hoveredRightTab, IsOverviewTabSelected);
        }

        private static void DrawTab(SpriteBatch sprite, Rectangle bounds, TabVisual tab, bool selected, bool hovered, bool disabled)
        {
            Texture2D texture = disabled ? tab.Disabled ?? tab.Normal ?? tab.Selected : selected ? tab.Selected ?? tab.Normal : hovered ? tab.Hover ?? tab.Normal : tab.Normal ?? tab.Selected;
            if (texture != null) sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
        }

        private void DrawCardSlotPanel(SpriteBatch sprite)
        {
            if (_cardSlotTexture != null) sprite.Draw(_cardSlotTexture, new Vector2(Position.X + CardSlotOrigin.X, Position.Y + CardSlotOrigin.Y), Color.White);
            if (IsOverviewTabSelected)
            {
                DrawOverviewSlots(sprite);
                return;
            }

            IReadOnlyList<MonsterBookCardSnapshot> cards = GetCurrentPageCards();
            for (int i = 0; i < CardsPerPage; i++) DrawCardSlot(sprite, GetCardBounds(i), i < cards.Count ? cards[i] : null, i == _selectedSlotIndex);
        }

        private void DrawCardSlot(SpriteBatch sprite, Rectangle bounds, MonsterBookCardSnapshot card, bool selected)
        {
            if (card?.IsDiscovered == true)
            {
                Texture2D icon = ResolveCardIcon(card.CardItemId);
                if (icon != null) DrawCardIcon(sprite, icon, bounds, card.IsCompleted ? Color.White : new Color(255, 255, 255, 230));
                Texture2D border = selected ? _selectedSlotTexture : _coveredSlotTexture;
                if (border != null) sprite.Draw(border, new Vector2(bounds.X, bounds.Y), Color.White);
                DrawCenteredString(sprite, $"{card.OwnedCopies}/{Math.Max(1, card.MaxCopies)}", new Rectangle(bounds.X - 1, bounds.Bottom - 13, bounds.Width + 2, 10), ValueColor, 0.42f);
                if (card.IsCompleted && _fullMarkTexture != null) sprite.Draw(_fullMarkTexture, new Vector2(bounds.Right - _fullMarkTexture.Width - 2, bounds.Y + 1), Color.White);
            }
            else
            {
                sprite.Draw(_pixel, bounds, new Color(25, 25, 25, 52));
                if (selected && _selectedSlotTexture != null) sprite.Draw(_selectedSlotTexture, new Vector2(bounds.X, bounds.Y), HiddenTint);
                DrawCenteredString(sprite, "?", bounds, MutedColor, 0.62f);
            }
        }

        private void DrawInfoPanel(SpriteBatch sprite)
        {
            if (_infoPageTexture != null) sprite.Draw(_infoPageTexture, new Vector2(Position.X + InfoPageOrigin.X, Position.Y + InfoPageOrigin.Y), Color.White);
            if (IsOverviewTabSelected)
            {
                DrawOverviewInfoPanel(sprite);
                return;
            }

            MonsterBookCardSnapshot card = GetSelectedCard();
            Texture2D icon = ResolveCardIcon(card?.CardItemId ?? 0);
            if (icon != null) sprite.Draw(icon, OffsetBounds(SelectedCardIconBounds, InfoPageOrigin), Color.White);
            DrawCenteredString(sprite, card?.IsDiscovered == true ? card.Name : "Unknown Card", OffsetBounds(SelectedCardNameBounds, InfoPageOrigin), TitleColor, 0.6f);
            DrawCenteredString(sprite, card == null ? "No card selected." : card.IsDiscovered ? BuildDetailText(card) : "Collect this card to reveal its detail entry.", OffsetBounds(SelectedCardDetailBounds, InfoPageOrigin), MutedColor, 0.46f);
            DrawDetailLines(sprite, card);
            DrawSummaryValue(sprite, 0, $"{GetCurrentGrade()?.Label ?? $"Chapter {_currentGradeIndex + 1}"} {_currentPageIndex + 1}/{Math.Max(1, GetCurrentGrade()?.Pages?.Count ?? 1)}");
            DrawSummaryValue(sprite, 1, $"{_snapshot?.OwnedCardTypes ?? 0}");
            DrawSummaryValue(sprite, 2, $"{_snapshot?.OwnedBossCardTypes ?? 0}");
            DrawSummaryValue(sprite, 3, $"{_snapshot?.OwnedNormalCardTypes ?? 0}");
            DrawSummaryValue(sprite, 4, $"{_snapshot?.CompletedCardTypes ?? 0}");
        }

        private void DrawDetailLines(SpriteBatch sprite, MonsterBookCardSnapshot card)
        {
            IEnumerable<string> lines = _detailTab switch
            {
                MonsterBookDetailTab.BasicInfo => card?.IsDiscovered == true ? new[] { $"Lv. {card.Level}", $"HP {card.MaxHp}", $"EXP {card.Exp}", card.IsBoss ? "Boss classification" : "Normal classification", card.IsRegistered ? "Registered in book" : "Not registered", $"Cards {card.OwnedCopies}/{Math.Max(1, card.MaxCopies)}" } : new[] { "Collect this card to reveal", "the client detail entry." },
                MonsterBookDetailTab.Episode => new[] { TrimToWidth(card?.EpisodeText ?? "No episode entry.", DetailBodyBounds.Width, 0.42f) },
                MonsterBookDetailTab.Rewards => card?.RewardLines?.Count > 0 == true ? card.RewardLines.Take(6) : new[] { "No reward entries." },
                MonsterBookDetailTab.Habitat => card?.HabitatLines?.Count > 0 == true ? card.HabitatLines.Take(6) : new[] { "No habitat entries." },
                _ => Array.Empty<string>()
            };
            int row = 0;
            foreach (string line in lines.Take(6))
            {
                DrawTrimmedString(sprite, line, new Vector2(Position.X + InfoPageOrigin.X + DetailBodyBounds.X, Position.Y + InfoPageOrigin.Y + DetailBodyBounds.Y + (row * 17)), ValueColor, 0.42f, DetailBodyBounds.Width);
                row++;
            }
        }

        private void DrawSearchBox(SpriteBatch sprite)
        {
            Rectangle bounds = OffsetBounds(SearchBoxBounds, InfoPageOrigin);
            sprite.Draw(_pixel, bounds, new Color(255, 252, 239, 232));
            DrawOutline(sprite, bounds, new Color(115, 87, 42));
            DrawTrimmedString(sprite, string.IsNullOrWhiteSpace(_searchQuery) && !_searchMode ? "Search" : _searchQuery + _compositionText, new Vector2(bounds.X + 3, bounds.Y + 2), ValueColor, 0.42f, bounds.Width - 6);
            DrawTrimmedString(sprite, _searchMatches.Count == 0 ? (_searchMode ? "No local matches" : "Search catalog") : $"Match {_selectedSearchMatchIndex + 1}/{_searchMatches.Count}", new Vector2(Position.X + InfoPageOrigin.X + SearchStatusBounds.X, Position.Y + InfoPageOrigin.Y + SearchStatusBounds.Y), MutedColor, 0.4f, SearchStatusBounds.Width);
        }

        private void DrawPageIndex(SpriteBatch sprite)
        {
            if (IsOverviewTabSelected)
            {
                DrawCenteredString(
                    sprite,
                    "Overview",
                    new Rectangle(Position.X + LeftPageIndexBounds.X, Position.Y + LeftPageIndexBounds.Y, RightPageIndexBounds.Right - LeftPageIndexBounds.X, LeftPageIndexBounds.Height),
                    TitleColor,
                    0.52f);
                return;
            }

            int totalPages = GetTotalPageCount();
            int spreadStart = GetSpreadStartAbsolutePageIndex(GetAbsolutePageIndex());

            DrawCenteredString(
                sprite,
                BuildPageIndexText(spreadStart, totalPages),
                new Rectangle(Position.X + LeftPageIndexBounds.X, Position.Y + LeftPageIndexBounds.Y, LeftPageIndexBounds.Width, LeftPageIndexBounds.Height),
                TitleColor,
                0.52f);

            if (spreadStart + 1 < totalPages)
            {
                DrawCenteredString(
                    sprite,
                    BuildPageIndexText(spreadStart + 1, totalPages),
                    new Rectangle(Position.X + RightPageIndexBounds.X, Position.Y + RightPageIndexBounds.Y, RightPageIndexBounds.Width, RightPageIndexBounds.Height),
                    TitleColor,
                    0.52f);
            }
        }

        private void DrawPageMarkers(SpriteBatch sprite)
        {
            if (IsOverviewTabSelected)
            {
                DrawPageMarker(sprite, Position.X + PageMarkerAnchor.X - 16, Position.Y + PageMarkerAnchor.Y, false);
                DrawPageMarker(sprite, Position.X + PageMarkerAnchor.X + 16, Position.Y + PageMarkerAnchor.Y, false);
                return;
            }

            int totalPages = GetTotalPageCount();
            int currentAbsolutePage = GetAbsolutePageIndex();
            int spreadStart = GetSpreadStartAbsolutePageIndex(currentAbsolutePage);
            DrawPageMarker(sprite, Position.X + PageMarkerAnchor.X - 16, Position.Y + PageMarkerAnchor.Y, currentAbsolutePage == spreadStart);
            DrawPageMarker(sprite, Position.X + PageMarkerAnchor.X + 16, Position.Y + PageMarkerAnchor.Y, spreadStart + 1 < totalPages && currentAbsolutePage == spreadStart + 1);
        }
        private void DrawPageMarker(SpriteBatch sprite, int x, int y, bool active) { Texture2D marker = active ? _activePageMarkerTexture ?? _fullMarkTexture : _inactivePageMarkerTexture ?? _coveredSlotTexture; if (marker != null) sprite.Draw(marker, new Rectangle(x - marker.Width / 2, y - marker.Height / 2, marker.Width, marker.Height), Color.White); }
        private void DrawStatus(SpriteBatch sprite) => DrawTrimmedString(sprite, _contextMenuVisible ? (GetSelectedCard()?.IsRegistered == true ? "Release this card from the local register list." : "Register this card in the local book list.") : _searchMode ? (_searchMatches.Count > 0 ? "Type to search. Enter jumps to the next match." : "Type to search the local catalog.") : IsOverviewTabSelected ? "Overview tab selected. Choose a chapter tab to browse cards or search across the catalog." : _snapshot?.StatusText, new Vector2(Position.X + StatusBounds.X, Position.Y + StatusBounds.Y), MutedColor, 0.4f, StatusBounds.Width);

        private void DrawContextMenu(SpriteBatch sprite)
        {
            if (!_contextMenuVisible) return;
            Rectangle bounds = GetContextMenuBounds();
            if (_contextMenuTopTexture != null) sprite.Draw(_contextMenuTopTexture, new Vector2(bounds.X, bounds.Y), Color.White);
            if (_contextMenuCenterTexture != null) for (int y = bounds.Y + (_contextMenuTopTexture?.Height ?? 0); y < bounds.Bottom - (_contextMenuBottomTexture?.Height ?? 0); y += _contextMenuCenterTexture.Height) sprite.Draw(_contextMenuCenterTexture, new Vector2(bounds.X, y), Color.White);
            if (_contextMenuBottomTexture != null) sprite.Draw(_contextMenuBottomTexture, new Vector2(bounds.X, bounds.Bottom - _contextMenuBottomTexture.Height), Color.White);
        }

        private void DrawOverviewSlots(SpriteBatch sprite)
        {
            IReadOnlyList<MonsterBookGradeSnapshot> grades = _snapshot?.Grades ?? Array.Empty<MonsterBookGradeSnapshot>();
            for (int i = 0; i < CardsPerPage; i++)
            {
                Rectangle bounds = GetCardBounds(i);
                if (i >= grades.Count)
                {
                    sprite.Draw(_pixel, bounds, new Color(25, 25, 25, 24));
                    continue;
                }

                MonsterBookGradeSnapshot grade = grades[i];
                sprite.Draw(_pixel, bounds, new Color(247, 239, 220, 224));
                DrawOutline(sprite, bounds, new Color(130, 100, 58));
                DrawCenteredString(sprite, $"{grade.GradeIndex + 1}", new Rectangle(bounds.X, bounds.Y + 2, bounds.Width, 10), TitleColor, 0.48f);
                DrawCenteredString(sprite, $"{grade.OwnedCardTypes}/{grade.CardTypeCount}", new Rectangle(bounds.X - 1, bounds.Y + 15, bounds.Width + 2, 10), ValueColor, 0.38f);
                DrawCenteredString(sprite, $"{grade.CompletedCardTypes}", new Rectangle(bounds.X - 1, bounds.Y + 27, bounds.Width + 2, 10), AccentColor, 0.38f);
            }
        }

        private void DrawOverviewInfoPanel(SpriteBatch sprite)
        {
            DrawCenteredString(sprite, _snapshot?.Title ?? "Monster Book", OffsetBounds(SelectedCardNameBounds, InfoPageOrigin), TitleColor, 0.58f);
            DrawCenteredString(sprite, "Overview", OffsetBounds(SelectedCardDetailBounds, InfoPageOrigin), MutedColor, 0.46f);

            string[] lines =
            {
                $"Owned cards {_snapshot?.OwnedCardTypes ?? 0}/{_snapshot?.TotalCardTypes ?? 0}",
                $"Completed {_snapshot?.CompletedCardTypes ?? 0}",
                $"Boss {_snapshot?.OwnedBossCardTypes ?? 0}",
                $"Normal {_snapshot?.OwnedNormalCardTypes ?? 0}",
                $"Copies {_snapshot?.TotalOwnedCopies ?? 0}",
                "Select a chapter tab or search."
            };

            for (int row = 0; row < lines.Length; row++)
            {
                DrawTrimmedString(sprite, lines[row], new Vector2(Position.X + InfoPageOrigin.X + DetailBodyBounds.X, Position.Y + InfoPageOrigin.Y + DetailBodyBounds.Y + (row * 17)), ValueColor, 0.42f, DetailBodyBounds.Width);
            }

            DrawSummaryValue(sprite, 0, "All");
            DrawSummaryValue(sprite, 1, $"{_snapshot?.OwnedCardTypes ?? 0}");
            DrawSummaryValue(sprite, 2, $"{_snapshot?.OwnedBossCardTypes ?? 0}");
            DrawSummaryValue(sprite, 3, $"{_snapshot?.OwnedNormalCardTypes ?? 0}");
            DrawSummaryValue(sprite, 4, $"{_snapshot?.CompletedCardTypes ?? 0}");
        }

        private void ResetBookState()
        {
            _contextMenuVisible = false;
            _searchMode = false;
            _searchQuery = string.Empty;
            _compositionText = string.Empty;
            _searchMatches.Clear();
            _selectedSearchMatchIndex = -1;
        }

        private bool WasPressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);

        private int GetTotalPageCount() => IsOverviewTabSelected ? 0 : GetCurrentGrade()?.Pages?.Count ?? 0;

        private int GetAbsolutePageIndex()
        {
            return Math.Clamp(_currentPageIndex, 0, Math.Max(0, GetTotalPageCount() - 1));
        }

        private bool TrySetAbsolutePageIndex(int absolutePageIndex)
        {
            int totalPages = GetTotalPageCount();
            if (totalPages <= 0)
            {
                return false;
            }

            _currentPageIndex = Math.Clamp(absolutePageIndex, 0, totalPages - 1);
            return true;
        }

        private static int GetSpreadStartAbsolutePageIndex(int absolutePageIndex) => Math.Max(0, absolutePageIndex) & ~1;

        private static string BuildPageIndexText(int absolutePageIndex, int totalPages)
        {
            if (totalPages <= 0)
            {
                return "0/0";
            }

            return $"{absolutePageIndex + 1}/{totalPages}";
        }

        private Rectangle GetLeftTabBounds(int index)
        {
            if (index <= 0)
                return new Rectangle(Position.X + LeftTabInfoOrigin.X, Position.Y + LeftTabInfoOrigin.Y, _leftTabInfoVisual.Normal?.Width ?? 30, _leftTabInfoVisual.Normal?.Height ?? 28);

            Texture2D texture = (index - 1) < _leftTabs.Count ? _leftTabs[index - 1].Normal ?? _leftTabs[index - 1].Selected : null;
            int y = Position.Y + LeftTabOrigin.Y + ((index - 1) * ((texture?.Height ?? 26) + 2));
            return new Rectangle(Position.X + LeftTabOrigin.X, y, texture?.Width ?? 28, texture?.Height ?? 26);
        }

        private Rectangle GetRightTabBounds(int index)
        {
            Texture2D texture = index >= 0 && index < _rightTabs.Count ? _rightTabs[index].Normal ?? _rightTabs[index].Selected : null;
            int x = Position.X + RightTabOrigin.X + (index * ((texture?.Width ?? 34) + 2));
            return new Rectangle(x, Position.Y + RightTabOrigin.Y, texture?.Width ?? 34, texture?.Height ?? 22);
        }

        private Rectangle GetCardBounds(int index)
        {
            int row = index / 5;
            int column = index % 5;
            int x = Position.X + CardSlotOrigin.X + CardCellPadding.X + (column * CardCellStride.X);
            int y = Position.Y + CardSlotOrigin.Y + CardCellPadding.Y + (row * CardCellStride.Y);
            return new Rectangle(x, y, 28, 40);
        }

        private Rectangle GetContextMenuBounds()
        {
            int width = Math.Max(_contextMenuTopTexture?.Width ?? 70, Math.Max(_contextMenuCenterTexture?.Width ?? 70, _contextMenuBottomTexture?.Width ?? 70));
            int height = (_contextMenuTopTexture?.Height ?? 16) + (_contextMenuCenterTexture?.Height ?? 16) + (_contextMenuBottomTexture?.Height ?? 16);
            return new Rectangle(_contextMenuPosition.X, _contextMenuPosition.Y, width, height);
        }

        private Rectangle OffsetBounds(Rectangle bounds, Point offset) => new(Position.X + offset.X + bounds.X, Position.Y + offset.Y + bounds.Y, bounds.Width, bounds.Height);

        private void DrawSummaryValue(SpriteBatch sprite, int rowIndex, string value)
        {
            Vector2 position = new(Position.X + InfoPageOrigin.X + SummaryValueOrigin.X, Position.Y + InfoPageOrigin.Y + SummaryValueOrigin.Y + (rowIndex * 18));
            DrawTrimmedString(sprite, value, position, ValueColor, 0.44f, 64);
        }

        private string BuildDetailText(MonsterBookCardSnapshot card)
        {
            if (card == null)
                return "No card selected.";

            return card.IsBoss
                ? $"Boss card Lv. {card.Level}"
                : $"Monster card Lv. {card.Level}";
        }

        private Texture2D ResolveCardIcon(int itemId)
        {
            if (itemId <= 0 || _graphicsDevice == null)
                return null;

            if (_cardIconCache.TryGetValue(itemId, out Texture2D cached) && cached != null && !cached.IsDisposed)
                return cached;

            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
                return null;

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
                return null;

            itemImage.ParseImage();
            string itemText = string.Equals(category, "Character", StringComparison.OrdinalIgnoreCase)
                ? itemId.ToString("D8", CultureInfo.InvariantCulture)
                : itemId.ToString("D7", CultureInfo.InvariantCulture);
            WzSubProperty infoProperty = (itemImage[itemText] as WzSubProperty)?["info"] as WzSubProperty;
            Texture2D icon = (infoProperty?["iconRaw"] as WzCanvasProperty ?? infoProperty?["icon"] as WzCanvasProperty)?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_graphicsDevice);
            if (icon != null)
                _cardIconCache[itemId] = icon;
            return icon;
        }

        private static void DrawCardIcon(SpriteBatch sprite, Texture2D icon, Rectangle bounds, Color color)
        {
            Rectangle destination = new(bounds.X + ((bounds.Width - 28) / 2), bounds.Y + 2, 28, 28);
            sprite.Draw(icon, destination, color);
        }

        private string TrimToWidth(string text, int maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
                return text ?? string.Empty;

            string candidate = text;
            while (candidate.Length > 1 && _font.MeasureString(candidate).X * scale > maxWidth)
                candidate = candidate[..^1];

            return candidate.Length < text.Length && candidate.Length > 3 ? candidate[..^3] + "..." : candidate;
        }

        private void DrawTrimmedString(SpriteBatch sprite, string text, Vector2 position, Color color, float scale, int maxWidth)
        {
            string trimmed = TrimToWidth(text, maxWidth, scale);
            if (string.IsNullOrEmpty(trimmed) || _font == null)
                return;

            sprite.DrawString(_font, trimmed, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawOutline(SpriteBatch sprite, Rectangle bounds, Color color)
        {
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), color);
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), color);
            sprite.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), color);
            sprite.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), color);
        }

        private void DrawCenteredString(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text) || _font == null)
                return;

            Vector2 size = _font.MeasureString(text) * scale;
            Vector2 position = new(bounds.X + ((bounds.Width - size.X) / 2f), bounds.Y + ((bounds.Height - size.Y) / 2f));
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
