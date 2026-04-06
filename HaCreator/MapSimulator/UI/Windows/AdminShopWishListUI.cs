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
    public sealed class AdminShopWishListUI : UIWindowBase
    {
        private sealed class CategoryDisplayRow
        {
            public AdminShopDialogUI.WishlistCategoryNode Node { get; init; }
            public int Depth { get; init; }
            public bool IsExpanded { get; init; }
            public bool HasChildren => Node?.Children?.Count > 0;
        }

        private enum PopupMode
        {
            None,
            Category,
            Results
        }

        private const int SearchTextX = 82;
        private const int SearchTextY = 24;
        private const int SearchTextWidth = 160;
        private const int CategoryTextX = 82;
        private const int CategoryTextY = 54;
        private const int PriceTextX = 82;
        private const int PriceTextY = 72;
        private const int StatusTextX = 18;
        private const int StatusTextY = 92;
        private const int SearchMaxLength = 40;
        private const int MainTitleBandHeight = 16;
        private const int CategoryPopupOffsetY = 18;
        private const int PopupHeaderY = 12;
        private const int CategoryListX = 18;
        private const int CategoryListY = 24;
        private const int CategoryListWidth = 206;
        private const int CategoryRowHeight = 21;
        private const int CategoryVisibleRows = 7;
        private const int CategoryPopupFooterY = 236;
        private const int ResultListX = 18;
        private const int ResultListY = 32;
        private const int ResultListWidth = 206;
        private const int ResultRowHeight = 22;
        private const int ResultVisibleRows = 7;
        private const int ResultDetailY = 196;
        private const int ResultFooterY = 234;
        private const int ScrollBarX = 233;
        private const int ScrollBarY = 23;
        private readonly Texture2D _categoryPopupTexture;
        private readonly Texture2D _searchFieldTexture;
        private readonly Texture2D _pixelTexture;
        private readonly Texture2D _scrollBaseTexture;
        private readonly Texture2D _scrollThumbTexture;
        private readonly Texture2D _scrollPrevTexture;
        private readonly Texture2D _scrollNextTexture;
        private readonly UIObject _toggleAddOnButton;
        private readonly UIObject _priceRangeButton;
        private readonly UIObject _searchButton;
        private readonly UIObject _resultConfirmButton;
        private readonly UIObject _resultCancelButton;
        private readonly UIObject _closeButton;
        private readonly List<UIObject> _categoryRowButtons = new();
        private readonly List<UIObject> _resultRowButtons = new();

        private SpriteFont _font;
        private AdminShopDialogUI _sourceDialog;
        private IReadOnlyList<AdminShopDialogUI.WishlistPriceRange> _priceRanges = Array.Empty<AdminShopDialogUI.WishlistPriceRange>();
        private IReadOnlyList<AdminShopDialogUI.WishlistCategoryNode> _categoryTree = Array.Empty<AdminShopDialogUI.WishlistCategoryNode>();
        private List<AdminShopDialogUI.WishlistSearchResult> _searchResults = new();
        private readonly List<CategoryDisplayRow> _categoryRows = new();
        private readonly HashSet<string> _expandedCategoryKeys = new(StringComparer.OrdinalIgnoreCase);
        private string _searchQuery = string.Empty;
        private string _compositionText = string.Empty;
        private string _statusMessage = "Type an item name, then submit through SearchItemName.";
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Point? _dragStartOffset;
        private string _selectedCategoryKey = "all";
        private int _selectedPriceRangeIndex;
        private int _categoryScrollOffset;
        private int _resultScrollOffset;
        private int _selectedResultIndex;
        private PopupMode _popupMode;

        public AdminShopWishListUI(
            IDXObject frame,
            Texture2D categoryPopupTexture,
            Texture2D searchFieldTexture,
            UIObject toggleAddOnButton,
            UIObject priceRangeButton,
            UIObject searchButton,
            UIObject resultConfirmButton,
            UIObject resultCancelButton,
            UIObject closeButton,
            Texture2D scrollBaseTexture,
            Texture2D scrollThumbTexture,
            Texture2D scrollPrevTexture,
            Texture2D scrollNextTexture,
            GraphicsDevice device)
            : base(frame)
        {
            _categoryPopupTexture = categoryPopupTexture;
            _searchFieldTexture = searchFieldTexture;
            _toggleAddOnButton = toggleAddOnButton;
            _priceRangeButton = priceRangeButton;
            _searchButton = searchButton;
            _resultConfirmButton = resultConfirmButton;
            _resultCancelButton = resultCancelButton;
            _closeButton = closeButton;
            _scrollBaseTexture = scrollBaseTexture;
            _scrollThumbTexture = scrollThumbTexture;
            _scrollPrevTexture = scrollPrevTexture;
            _scrollNextTexture = scrollNextTexture;

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            if (_toggleAddOnButton != null)
            {
                AddButton(_toggleAddOnButton);
                _toggleAddOnButton.ButtonClickReleased += _ => ToggleAddOn();
            }

            if (_priceRangeButton != null)
            {
                AddButton(_priceRangeButton);
                _priceRangeButton.ButtonClickReleased += _ => CyclePriceRange(1);
            }

            if (_searchButton != null)
            {
                AddButton(_searchButton);
                _searchButton.ButtonClickReleased += _ => ExecuteSearch();
            }

            if (_resultConfirmButton != null)
            {
                AddButton(_resultConfirmButton);
                _resultConfirmButton.ButtonClickReleased += _ => ApplySelectedResult();
                _resultConfirmButton.SetVisible(false);
                _resultConfirmButton.SetEnabled(false);
            }

            if (_resultCancelButton != null)
            {
                AddButton(_resultCancelButton);
                _resultCancelButton.ButtonClickReleased += _ => ClosePopup("Wish-list results closed.");
                _resultCancelButton.SetVisible(false);
                _resultCancelButton.SetEnabled(false);
            }

            if (_closeButton != null)
            {
                AddButton(_closeButton);
                _closeButton.ButtonClickReleased += _ => CloseOwner();
            }

            for (int i = 0; i < CategoryVisibleRows; i++)
            {
                UIObject rowButton = CreateTransparentButton(device, CategoryListWidth, CategoryRowHeight);
                rowButton.X = CategoryListX;
                rowButton.Y = CategoryPopupOffsetY + CategoryListY + (i * CategoryRowHeight);
                int capturedRow = i;
                rowButton.ButtonClickReleased += _ => SelectVisibleCategory(capturedRow);
                rowButton.SetVisible(false);
                rowButton.SetEnabled(false);
                AddButton(rowButton);
                _categoryRowButtons.Add(rowButton);
            }

            for (int i = 0; i < ResultVisibleRows; i++)
            {
                UIObject rowButton = CreateTransparentButton(device, ResultListWidth, ResultRowHeight);
                rowButton.X = ResultListX;
                rowButton.Y = CategoryPopupOffsetY + ResultListY + (i * ResultRowHeight);
                int capturedRow = i;
                rowButton.ButtonClickReleased += _ => SelectVisibleResult(capturedRow, true);
                rowButton.SetVisible(false);
                rowButton.SetEnabled(false);
                AddButton(rowButton);
                _resultRowButtons.Add(rowButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.AdminShopWishList;
        public override bool CapturesKeyboardInput => IsVisible;
        public Action<AdminShopWishListUI> ShowCategoryAddOnRequested { get; set; }
        public Action HideCategoryAddOnRequested { get; set; }
        public Func<bool> IsCategoryAddOnVisible { get; set; }

        public IReadOnlyList<AdminShopDialogUI.WishlistCategoryNode> GetWishlistCategoryTree()
        {
            return _categoryTree;
        }

        public string GetSelectedWishlistCategoryKey()
        {
            return _selectedCategoryKey;
        }

        public IReadOnlyCollection<string> GetExpandedWishlistCategoryKeys()
        {
            return _expandedCategoryKeys;
        }

        public void OnCategoryAddOnClosed(string selectedCategoryKey, IEnumerable<string> expandedCategoryKeys, string message)
        {
            SyncCategoryAddOnState(selectedCategoryKey, expandedCategoryKeys, message);
            UpdatePopupButtons();
        }

        public void SyncCategoryAddOnState(string selectedCategoryKey, IEnumerable<string> expandedCategoryKeys, string message)
        {
            _selectedCategoryKey = string.IsNullOrWhiteSpace(selectedCategoryKey) ? "all" : selectedCategoryKey;
            _expandedCategoryKeys.Clear();
            if (expandedCategoryKeys != null)
            {
                foreach (string expandedKey in expandedCategoryKeys)
                {
                    if (!string.IsNullOrWhiteSpace(expandedKey))
                    {
                        _expandedCategoryKeys.Add(expandedKey);
                    }
                }
            }

            EnsureCategoryPathExpanded(_selectedCategoryKey);
            RefreshCategoryRows();
            EnsureSelectedCategoryVisible();
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
        }

        public string ResolveWishlistCategoryLabel(string categoryKey)
        {
            return _sourceDialog?.GetWishlistCategoryLabel(categoryKey) ?? "All";
        }

        public void ShowFor(AdminShopDialogUI sourceDialog)
        {
            _sourceDialog = sourceDialog;
            _priceRanges = sourceDialog?.GetWishlistPriceRanges() ?? Array.Empty<AdminShopDialogUI.WishlistPriceRange>();
            _categoryTree = sourceDialog?.GetWishlistCategoryTree() ?? Array.Empty<AdminShopDialogUI.WishlistCategoryNode>();
            _searchResults = new List<AdminShopDialogUI.WishlistSearchResult>();
            _searchQuery = sourceDialog?.GetWishlistSuggestedQuery() ?? string.Empty;
            _selectedCategoryKey = sourceDialog?.GetWishlistSuggestedCategoryKey() ?? "all";
            _selectedPriceRangeIndex = NormalizePriceRangeIndex(sourceDialog?.GetWishlistSuggestedPriceRangeIndex() ?? 0);
            _expandedCategoryKeys.Clear();
            EnsureCategoryPathExpanded(_selectedCategoryKey);
            RefreshCategoryRows();
            EnsureSelectedCategoryVisible();
            _resultScrollOffset = 0;
            _selectedResultIndex = 0;
            _compositionText = string.Empty;
            _popupMode = PopupMode.None;
            _statusMessage = $"CUIAdminShopWishList::OnCreate focused the search edit for {sourceDialog?.GetWishlistServiceName() ?? "cash-service"} browsing.";
            HideCategoryAddOnRequested?.Invoke();
            PositionRelativeToSource(sourceDialog);
            Show();
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _dragStartOffset = null;
            UpdatePopupButtons();
        }

        public override void Hide()
        {
            HideCategoryAddOnRequested?.Invoke();
            base.Hide();
            _compositionText = string.Empty;
            _popupMode = PopupMode.None;
            _dragStartOffset = null;
            _searchResults.Clear();
            _categoryRows.Clear();
            _expandedCategoryKeys.Clear();
            UpdatePopupButtons();
        }

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

            _compositionText = string.Empty;
            foreach (char ch in text)
            {
                if (char.IsControl(ch) || _searchQuery.Length >= SearchMaxLength)
                {
                    continue;
                }

                _searchQuery += ch;
            }
        }

        public override void HandleCompositionText(string text)
        {
            _compositionText = string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Length > SearchMaxLength
                    ? text[..SearchMaxLength]
                    : text;
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

            if (_sourceDialog == null || !_sourceDialog.IsVisible)
            {
                Hide();
                return;
            }

            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            HandleKeyboardInput(keyboardState);
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                return;
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (_popupMode == PopupMode.Results && leftJustPressed)
            {
                if (GetScrollPrevBounds().Contains(mouseState.Position))
                {
                    ScrollActivePopup(-1);
                }
                else if (GetScrollNextBounds().Contains(mouseState.Position))
                {
                    ScrollActivePopup(1);
                }
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (_popupMode == PopupMode.Results && wheelDelta != 0 && GetPopupBounds().Contains(mouseState.Position))
            {
                ScrollActivePopup(wheelDelta > 0 ? -1 : 1);
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        public override bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!IsVisible)
            {
                return false;
            }

            foreach (UIObject button in uiButtons)
            {
                if (!button.ButtonVisible)
                {
                    continue;
                }

                if (button.CheckMouseEvent(shiftCenteredX, shiftCenteredY, Position.X, Position.Y, mouseState))
                {
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }

            Rectangle dragBounds = new Rectangle(Position.X, Position.Y - MainTitleBandHeight, CurrentFrame?.Width ?? 265, (CurrentFrame?.Height ?? 135) + MainTitleBandHeight);
            if (_popupMode == PopupMode.Results)
            {
                dragBounds = Rectangle.Union(dragBounds, GetPopupBounds());
            }

            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                if (_dragStartOffset == null)
                {
                    if (!dragBounds.Contains(mouseState.Position))
                    {
                        return false;
                    }

                    _dragStartOffset = new Point(mouseState.X - Position.X, mouseState.Y - Position.Y);
                }

                int maxWidth = _popupMode == PopupMode.Results ? Math.Max(CurrentFrame?.Width ?? 265, _categoryPopupTexture?.Width ?? 264) : CurrentFrame?.Width ?? 265;
                int maxHeight = _popupMode == PopupMode.Results ? Math.Max(CurrentFrame?.Height ?? 135, CategoryPopupOffsetY + (_categoryPopupTexture?.Height ?? 274)) : CurrentFrame?.Height ?? 135;
                Position = new Point(
                    Math.Clamp(mouseState.X - _dragStartOffset.Value.X, 0, Math.Max(0, renderWidth - maxWidth)),
                    Math.Clamp(mouseState.Y - _dragStartOffset.Value.Y, MainTitleBandHeight, Math.Max(MainTitleBandHeight, renderHeight - maxHeight)));
                return true;
            }

            _dragStartOffset = null;
            return dragBounds.Contains(mouseState.Position);
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
            if (_font == null)
            {
                return;
            }

            if (_searchFieldTexture != null)
            {
                sprite.Draw(_searchFieldTexture, new Vector2(Position.X + SearchTextX - 2, Position.Y + SearchTextY - 1), Color.White);
            }

            sprite.DrawString(_font, TrimToWidth(BuildRenderedQuery(), SearchTextWidth), new Vector2(Position.X + SearchTextX, Position.Y + SearchTextY), new Color(50, 56, 94));
            sprite.DrawString(_font, TrimToWidth(GetSelectedCategoryLabel(), 172f), new Vector2(Position.X + CategoryTextX, Position.Y + CategoryTextY), new Color(50, 56, 94));
            sprite.DrawString(_font, TrimToWidth(GetSelectedPriceRangeLabel(), 172f), new Vector2(Position.X + PriceTextX, Position.Y + PriceTextY), new Color(50, 56, 94));
            sprite.DrawString(_font, TrimToWidth(_statusMessage, 224f), new Vector2(Position.X + StatusTextX, Position.Y + StatusTextY), new Color(255, 233, 160));

            if (_popupMode == PopupMode.Results)
            {
                DrawResultsPopup(sprite);
            }
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            yield return new Rectangle(Position.X, Position.Y - MainTitleBandHeight, CurrentFrame?.Width ?? 265, MainTitleBandHeight);
            if (_popupMode == PopupMode.Results)
            {
                yield return GetPopupBounds();
            }
        }

        private void DrawResultsPopup(SpriteBatch sprite)
        {
            Rectangle popupBounds = GetPopupBounds();
            if (_categoryPopupTexture != null)
            {
                sprite.Draw(_categoryPopupTexture, popupBounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, popupBounds, new Color(74, 134, 186));
            }

            string header = $"SearchItemName staged {_searchResults.Count} result(s)";
            sprite.DrawString(_font, TrimToWidth(header, 184f), new Vector2(popupBounds.X + ResultListX, popupBounds.Y + PopupHeaderY), Color.White);
            for (int row = 0; row < ResultVisibleRows; row++)
            {
                int resultIndex = _resultScrollOffset + row;
                if (resultIndex >= _searchResults.Count)
                {
                    break;
                }

                AdminShopDialogUI.WishlistSearchResult result = _searchResults[resultIndex];
                Rectangle rowBounds = GetResultRowBounds(row);
                bool selected = resultIndex == _selectedResultIndex;
                sprite.Draw(_pixelTexture, rowBounds, selected ? new Color(255, 255, 255, 120) : new Color(255, 255, 255, 32));
                sprite.DrawString(_font, TrimToWidth(result.Title, 132f), new Vector2(rowBounds.X + 6, rowBounds.Y + 3), selected ? new Color(40, 55, 96) : Color.White);
                sprite.DrawString(_font, TrimToWidth(result.PriceLabel, 60f), new Vector2(rowBounds.Right - 62, rowBounds.Y + 3), selected ? new Color(40, 55, 96) : new Color(255, 233, 160));
            }

            DrawPopupScrollBar(sprite);
            if (_searchResults.Count > 0 && _selectedResultIndex >= 0 && _selectedResultIndex < _searchResults.Count)
            {
                AdminShopDialogUI.WishlistSearchResult selectedResult = _searchResults[_selectedResultIndex];
                sprite.DrawString(_font, TrimToWidth(selectedResult.CategoryLabel, 90f), new Vector2(popupBounds.X + ResultListX, popupBounds.Y + ResultDetailY), new Color(255, 233, 160));
                sprite.DrawString(_font, TrimToWidth(selectedResult.Seller, 112f), new Vector2(popupBounds.X + ResultListX + 94, popupBounds.Y + ResultDetailY), Color.White);
                sprite.DrawString(_font, TrimToWidth(selectedResult.Detail, 220f), new Vector2(popupBounds.X + ResultListX, popupBounds.Y + ResultDetailY + 18), Color.White);
                string footer = selectedResult.AlreadyWishlisted
                    ? "Entry already exists in the local wish-list cache."
                    : "BtBuy focuses the selected result in the admin-shop dialog.";
                sprite.DrawString(_font, TrimToWidth(footer, 220f), new Vector2(popupBounds.X + ResultListX, popupBounds.Y + ResultFooterY), new Color(255, 233, 160));
            }
        }

        private void DrawPopupScrollBar(SpriteBatch sprite)
        {
            Rectangle baseBounds = GetScrollBaseBounds();
            Rectangle thumbBounds = GetScrollThumbBounds();

            if (_scrollBaseTexture != null)
            {
                sprite.Draw(_scrollBaseTexture, baseBounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, baseBounds, new Color(122, 160, 202));
            }

            if (_scrollThumbTexture != null)
            {
                sprite.Draw(_scrollThumbTexture, thumbBounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, thumbBounds, new Color(215, 224, 235));
            }

            if (_scrollPrevTexture != null)
            {
                sprite.Draw(_scrollPrevTexture, GetScrollPrevBounds().Location.ToVector2(), Color.White);
            }

            if (_scrollNextTexture != null)
            {
                sprite.Draw(_scrollNextTexture, GetScrollNextBounds().Location.ToVector2(), Color.White);
            }
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (IsCategoryAddOnOpen())
            {
                return;
            }

            if (WasPressed(keyboardState, Keys.Escape))
            {
                if (_popupMode == PopupMode.Results)
                {
                    ClosePopup("Wish-list results closed.");
                    return;
                }

                CloseOwner();
                return;
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
                if (_popupMode == PopupMode.Results)
                {
                    ApplySelectedResult();
                    return;
                }

                ExecuteSearch();
                return;
            }

            if (WasPressed(keyboardState, Keys.Back) && _searchQuery.Length > 0)
            {
                _searchQuery = _searchQuery[..^1];
            }
            else if (WasPressed(keyboardState, Keys.Delete))
            {
                _searchQuery = string.Empty;
            }
            else if (WasPressed(keyboardState, Keys.Space))
            {
                ToggleAddOn();
                return;
            }

            if (_popupMode == PopupMode.Results)
            {
                if (WasPressed(keyboardState, Keys.Up))
                {
                    SelectResult(_selectedResultIndex - 1, true);
                }
                else if (WasPressed(keyboardState, Keys.Down))
                {
                    SelectResult(_selectedResultIndex + 1, true);
                }
                else if (WasPressed(keyboardState, Keys.PageUp))
                {
                    SelectResult(_selectedResultIndex - ResultVisibleRows, true);
                }
                else if (WasPressed(keyboardState, Keys.PageDown))
                {
                    SelectResult(_selectedResultIndex + ResultVisibleRows, true);
                }
                else if (WasPressed(keyboardState, Keys.Home))
                {
                    SelectResult(0, true);
                }
                else if (WasPressed(keyboardState, Keys.End))
                {
                    SelectResult(_searchResults.Count - 1, true);
                }

                return;
            }

            if (WasPressed(keyboardState, Keys.Left))
            {
                CyclePriceRange(-1);
            }
            else if (WasPressed(keyboardState, Keys.Right))
            {
                CyclePriceRange(1);
            }
        }

        private void ExecuteSearch()
        {
            if (_sourceDialog == null)
            {
                _statusMessage = "Wish-list owner has no active cash-service dialog to search.";
                return;
            }

            if (IsCategoryAddOnOpen())
            {
                CloseCategoryAddOn();
            }

            IReadOnlyList<AdminShopDialogUI.WishlistSearchResult> results = _sourceDialog.SearchWishlistEntries(_searchQuery, _selectedCategoryKey, _selectedPriceRangeIndex, out _statusMessage);
            _searchResults = results.ToList();
            _selectedResultIndex = 0;
            _resultScrollOffset = 0;
            _compositionText = string.Empty;
            _popupMode = _searchResults.Count > 0 ? PopupMode.Results : PopupMode.None;
            UpdatePopupButtons();
        }

        private void ToggleAddOn()
        {
            if (IsCategoryAddOnOpen())
            {
                CloseCategoryAddOn();
                return;
            }

            if (_popupMode == PopupMode.Results)
            {
                ClosePopup("Wish-list results closed.");
            }

            EnsureCategoryPathExpanded(_selectedCategoryKey);
            RefreshCategoryRows();
            EnsureSelectedCategoryVisible();
            if (ShowCategoryAddOnRequested != null)
            {
                ShowCategoryAddOnRequested(this);
                _statusMessage = "ToggleAddOn opened the wish-list category add-on.";
                UpdatePopupButtons();
                return;
            }

            _popupMode = PopupMode.Category;
            _statusMessage = "ToggleAddOn opened the wish-list category add-on.";
            UpdatePopupButtons();
        }

        private void CloseOwner()
        {
            Hide();
        }

        private void SelectVisibleCategory(int rowIndex)
        {
            SelectCategoryByRowIndex(_categoryScrollOffset + rowIndex, true);
        }

        private void SelectCategory(string categoryKey, bool keepPopupOpen)
        {
            _selectedCategoryKey = string.IsNullOrWhiteSpace(categoryKey) ? "all" : categoryKey;
            EnsureCategoryPathExpanded(_selectedCategoryKey);
            RefreshCategoryRows();
            EnsureSelectedCategoryVisible();
            _popupMode = keepPopupOpen ? PopupMode.Category : PopupMode.None;
            _statusMessage = $"Wish-list category set to {GetSelectedCategoryLabel()}.";
            UpdatePopupButtons();
        }

        private void ScrollCategories(int delta)
        {
            _categoryScrollOffset = Math.Clamp(_categoryScrollOffset + delta, 0, Math.Max(0, _categoryRows.Count - CategoryVisibleRows));
            UpdatePopupButtons();
        }

        private void UpdatePopupButtons()
        {
            foreach (UIObject rowButton in _categoryRowButtons)
            {
                bool active = _popupMode == PopupMode.Category;
                rowButton.SetVisible(active);
                rowButton.SetEnabled(active);
            }

            foreach (UIObject rowButton in _resultRowButtons)
            {
                bool active = _popupMode == PopupMode.Results;
                rowButton.SetVisible(active);
                rowButton.SetEnabled(active);
            }

            if (_resultConfirmButton != null)
            {
                bool active = _popupMode == PopupMode.Results && _searchResults.Count > 0;
                _resultConfirmButton.SetVisible(active);
                _resultConfirmButton.SetEnabled(active);
            }

            if (_resultCancelButton != null)
            {
                bool active = _popupMode == PopupMode.Results;
                _resultCancelButton.SetVisible(active);
                _resultCancelButton.SetEnabled(active);
            }
        }

        private void PositionRelativeToSource(AdminShopDialogUI sourceDialog)
        {
            if (sourceDialog != null)
            {
                Position = new Point(sourceDialog.Position.X + 100, sourceDialog.Position.Y + 28);
            }
        }

        private Rectangle GetPopupBounds()
        {
            return new Rectangle(Position.X, Position.Y + CategoryPopupOffsetY, _categoryPopupTexture?.Width ?? 264, _categoryPopupTexture?.Height ?? 274);
        }

        private Rectangle GetCategoryRowBounds(int visibleRowIndex)
        {
            Rectangle popupBounds = GetPopupBounds();
            return new Rectangle(popupBounds.X + CategoryListX, popupBounds.Y + CategoryListY + (visibleRowIndex * CategoryRowHeight), CategoryListWidth, CategoryRowHeight - 2);
        }

        private Rectangle GetResultRowBounds(int visibleRowIndex)
        {
            Rectangle popupBounds = GetPopupBounds();
            return new Rectangle(popupBounds.X + ResultListX, popupBounds.Y + ResultListY + (visibleRowIndex * ResultRowHeight), ResultListWidth, ResultRowHeight - 2);
        }

        private Rectangle GetScrollBaseBounds()
        {
            Rectangle popupBounds = GetPopupBounds();
            return new Rectangle(popupBounds.X + ScrollBarX, popupBounds.Y + ScrollBarY, _scrollBaseTexture?.Width ?? 15, _scrollBaseTexture?.Height ?? 167);
        }

        private Rectangle GetScrollPrevBounds()
        {
            Rectangle popupBounds = GetPopupBounds();
            return new Rectangle(popupBounds.X + ScrollBarX + 1, popupBounds.Y + ScrollBarY, _scrollPrevTexture?.Width ?? 13, _scrollPrevTexture?.Height ?? 14);
        }

        private Rectangle GetScrollNextBounds()
        {
            Rectangle baseBounds = GetScrollBaseBounds();
            return new Rectangle(baseBounds.X + 1, baseBounds.Bottom - (_scrollNextTexture?.Height ?? 14), _scrollNextTexture?.Width ?? 13, _scrollNextTexture?.Height ?? 14);
        }

        private Rectangle GetScrollThumbBounds()
        {
            Rectangle baseBounds = GetScrollBaseBounds();
            Rectangle prevBounds = GetScrollPrevBounds();
            Rectangle nextBounds = GetScrollNextBounds();
            Rectangle trackBounds = new Rectangle(baseBounds.X, prevBounds.Bottom, baseBounds.Width, nextBounds.Y - prevBounds.Bottom);
            int maxOffset = GetActivePopupMaxOffset();
            if (maxOffset <= 0)
            {
                return trackBounds;
            }

            int thumbHeight = _scrollThumbTexture?.Height ?? 25;
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int popupOffset = GetActivePopupScrollOffset();
            int thumbY = trackBounds.Y + (int)Math.Round((popupOffset / (float)maxOffset) * travel);
            return new Rectangle(baseBounds.X + 1, thumbY, _scrollThumbTexture?.Width ?? 13, thumbHeight);
        }

        private void ScrollActivePopup(int delta)
        {
            if (_popupMode == PopupMode.Category)
            {
                ScrollCategories(delta);
                return;
            }

            if (_popupMode != PopupMode.Results)
            {
                return;
            }

            _resultScrollOffset = Math.Clamp(_resultScrollOffset + delta, 0, GetActivePopupMaxOffset());
            if (_selectedResultIndex < _resultScrollOffset)
            {
                _selectedResultIndex = _resultScrollOffset;
            }
            else if (_selectedResultIndex >= _resultScrollOffset + ResultVisibleRows)
            {
                _selectedResultIndex = _resultScrollOffset + ResultVisibleRows - 1;
            }

            _selectedResultIndex = Math.Clamp(_selectedResultIndex, 0, Math.Max(0, _searchResults.Count - 1));
            UpdatePopupButtons();
        }

        private void SelectVisibleResult(int rowIndex, bool keepPopupOpen)
        {
            SelectResult(_resultScrollOffset + rowIndex, keepPopupOpen);
        }

        private void SelectResult(int resultIndex, bool keepPopupOpen)
        {
            if (_searchResults.Count == 0)
            {
                return;
            }

            _selectedResultIndex = Math.Clamp(resultIndex, 0, _searchResults.Count - 1);
            if (_selectedResultIndex < _resultScrollOffset)
            {
                _resultScrollOffset = _selectedResultIndex;
            }
            else if (_selectedResultIndex >= _resultScrollOffset + ResultVisibleRows)
            {
                _resultScrollOffset = _selectedResultIndex - ResultVisibleRows + 1;
            }

            _resultScrollOffset = Math.Clamp(_resultScrollOffset, 0, GetActivePopupMaxOffset());
            _popupMode = keepPopupOpen ? PopupMode.Results : PopupMode.None;
            _statusMessage = $"Wish-list result selected: {_searchResults[_selectedResultIndex].Title}.";
            UpdatePopupButtons();
        }

        private void ApplySelectedResult()
        {
            if (_sourceDialog == null || _searchResults.Count == 0 || _selectedResultIndex < 0 || _selectedResultIndex >= _searchResults.Count)
            {
                _statusMessage = "Wish-list result selection is unavailable.";
                return;
            }

            AdminShopDialogUI.WishlistSearchResult selectedResult = _searchResults[_selectedResultIndex];
            if (_sourceDialog.TryApplyWishlistSearchResult(selectedResult.EntryKey, _selectedCategoryKey, out _statusMessage))
            {
                Hide();
            }
        }

        private void ClosePopup(string message)
        {
            _popupMode = PopupMode.None;
            if (!string.IsNullOrWhiteSpace(message))
            {
                _statusMessage = message;
            }
            UpdatePopupButtons();
        }

        private bool IsCategoryAddOnOpen()
        {
            if (IsCategoryAddOnVisible != null)
            {
                return IsCategoryAddOnVisible();
            }

            return _popupMode == PopupMode.Category;
        }

        private void CloseCategoryAddOn()
        {
            if (HideCategoryAddOnRequested != null)
            {
                HideCategoryAddOnRequested();
                _statusMessage = "ToggleAddOn closed the wish-list category add-on.";
                UpdatePopupButtons();
                return;
            }

            if (_popupMode == PopupMode.Category)
            {
                _popupMode = PopupMode.None;
                _statusMessage = "ToggleAddOn closed the wish-list category add-on.";
                UpdatePopupButtons();
            }
        }

        private void CyclePriceRange(int delta)
        {
            if (_priceRanges.Count == 0)
            {
                _selectedPriceRangeIndex = 0;
                return;
            }

            _selectedPriceRangeIndex += delta;
            if (_selectedPriceRangeIndex < 0)
            {
                _selectedPriceRangeIndex = _priceRanges.Count - 1;
            }
            else if (_selectedPriceRangeIndex >= _priceRanges.Count)
            {
                _selectedPriceRangeIndex = 0;
            }

            _statusMessage = $"Wish-list price range set to {GetSelectedPriceRangeLabel()}.";
        }

        private int NormalizePriceRangeIndex(int priceRangeIndex)
        {
            if (_priceRanges.Count == 0)
            {
                return 0;
            }

            return Math.Clamp(priceRangeIndex, 0, _priceRanges.Count - 1);
        }

        private string GetSelectedPriceRangeLabel()
        {
            if (_priceRanges.Count == 0)
            {
                return "All prices";
            }

            return _priceRanges[NormalizePriceRangeIndex(_selectedPriceRangeIndex)].Label;
        }

        private int GetActivePopupMaxOffset()
        {
            return _popupMode switch
            {
                PopupMode.Category => Math.Max(0, _categoryRows.Count - CategoryVisibleRows),
                PopupMode.Results => Math.Max(0, _searchResults.Count - ResultVisibleRows),
                _ => 0
            };
        }

        private int GetActivePopupScrollOffset()
        {
            return _popupMode switch
            {
                PopupMode.Category => _categoryScrollOffset,
                PopupMode.Results => _resultScrollOffset,
                _ => 0
            };
        }

        private void MoveCategorySelection(int delta)
        {
            if (_categoryRows.Count == 0)
            {
                return;
            }

            int selectedIndex = GetSelectedCategoryRowIndex();
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            SelectCategoryByRowIndex(selectedIndex + delta, true);
        }

        private void SelectCategoryByRowIndex(int rowIndex, bool keepPopupOpen)
        {
            if (_categoryRows.Count == 0)
            {
                return;
            }

            int normalizedIndex = Math.Clamp(rowIndex, 0, _categoryRows.Count - 1);
            CategoryDisplayRow row = _categoryRows[normalizedIndex];
            SelectCategory(row.Node?.Key, keepPopupOpen);
        }

        private int GetSelectedCategoryRowIndex()
        {
            for (int i = 0; i < _categoryRows.Count; i++)
            {
                if (string.Equals(_categoryRows[i].Node?.Key, _selectedCategoryKey, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureSelectedCategoryVisible()
        {
            int selectedIndex = GetSelectedCategoryRowIndex();
            if (selectedIndex < 0)
            {
                _categoryScrollOffset = 0;
                return;
            }

            if (selectedIndex < _categoryScrollOffset)
            {
                _categoryScrollOffset = selectedIndex;
            }
            else if (selectedIndex >= _categoryScrollOffset + CategoryVisibleRows)
            {
                _categoryScrollOffset = selectedIndex - CategoryVisibleRows + 1;
            }

            _categoryScrollOffset = Math.Clamp(_categoryScrollOffset, 0, Math.Max(0, _categoryRows.Count - CategoryVisibleRows));
        }

        private void RefreshCategoryRows()
        {
            _categoryRows.Clear();
            foreach (AdminShopDialogUI.WishlistCategoryNode node in _categoryTree)
            {
                AppendCategoryRows(node, 0);
            }
        }

        private void AppendCategoryRows(AdminShopDialogUI.WishlistCategoryNode node, int depth)
        {
            if (node == null)
            {
                return;
            }

            bool isExpanded = _expandedCategoryKeys.Contains(node.Key);
            _categoryRows.Add(new CategoryDisplayRow
            {
                Node = node,
                Depth = depth,
                IsExpanded = isExpanded
            });

            if (!isExpanded || node.Children == null)
            {
                return;
            }

            foreach (AdminShopDialogUI.WishlistCategoryNode child in node.Children)
            {
                AppendCategoryRows(child, depth + 1);
            }
        }

        private void EnsureCategoryPathExpanded(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
            {
                categoryKey = "all";
            }

            if (TryFindCategoryPath(_categoryTree, categoryKey, out List<AdminShopDialogUI.WishlistCategoryNode> path))
            {
                foreach (AdminShopDialogUI.WishlistCategoryNode pathNode in path)
                {
                    if (pathNode.Children?.Count > 0)
                    {
                        _expandedCategoryKeys.Add(pathNode.Key);
                    }
                }
            }
            else
            {
                _expandedCategoryKeys.Add("all");
            }
        }

        private void CollapseSelectedCategoryOrMoveToParent()
        {
            if (!TryFindCategoryPath(_categoryTree, _selectedCategoryKey, out List<AdminShopDialogUI.WishlistCategoryNode> path) || path.Count == 0)
            {
                return;
            }

            AdminShopDialogUI.WishlistCategoryNode selectedNode = path[^1];
            if (selectedNode.Children?.Count > 0 && _expandedCategoryKeys.Contains(selectedNode.Key))
            {
                _expandedCategoryKeys.Remove(selectedNode.Key);
                RefreshCategoryRows();
                EnsureSelectedCategoryVisible();
                UpdatePopupButtons();
                return;
            }

            if (path.Count > 1)
            {
                SelectCategory(path[^2].Key, true);
            }
        }

        private void ExpandSelectedCategoryOrMoveToFirstChild()
        {
            if (!TryFindCategoryPath(_categoryTree, _selectedCategoryKey, out List<AdminShopDialogUI.WishlistCategoryNode> path) || path.Count == 0)
            {
                return;
            }

            AdminShopDialogUI.WishlistCategoryNode selectedNode = path[^1];
            if (selectedNode.Children?.Count > 0)
            {
                _expandedCategoryKeys.Add(selectedNode.Key);
                RefreshCategoryRows();
                SelectCategory(selectedNode.Children[0].Key, true);
            }
        }

        private string GetSelectedCategoryLabel()
        {
            return _sourceDialog?.GetWishlistCategoryLabel(_selectedCategoryKey) ?? "All";
        }

        private static bool TryFindCategoryPath(
            IReadOnlyList<AdminShopDialogUI.WishlistCategoryNode> roots,
            string categoryKey,
            out List<AdminShopDialogUI.WishlistCategoryNode> path)
        {
            List<AdminShopDialogUI.WishlistCategoryNode> resolvedPath = new();
            if (roots == null || roots.Count == 0)
            {
                path = resolvedPath;
                return false;
            }

            bool Search(IReadOnlyList<AdminShopDialogUI.WishlistCategoryNode> nodes)
            {
                foreach (AdminShopDialogUI.WishlistCategoryNode node in nodes)
                {
                    resolvedPath.Add(node);
                    if (string.Equals(node.Key, categoryKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (node.Children != null && Search(node.Children))
                    {
                        return true;
                    }

                    resolvedPath.RemoveAt(resolvedPath.Count - 1);
                }

                return false;
            }

            bool found = Search(roots);
            path = resolvedPath;
            return found;
        }

        private string BuildRenderedQuery()
        {
            string rendered = _searchQuery;
            if (!string.IsNullOrEmpty(_compositionText))
            {
                rendered += _compositionText;
            }

            return string.IsNullOrEmpty(rendered) ? "Search item name" : rendered;
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private static string TrimToWidth(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length > 34 ? text[..31] + "..." : text;
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
