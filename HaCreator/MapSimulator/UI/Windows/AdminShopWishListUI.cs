using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class AdminShopWishListUI : UIWindowBase
    {
        private const int SearchTextX = 82;
        private const int SearchTextY = 24;
        private const int SearchTextWidth = 160;
        private const int CategoryTextX = 82;
        private const int CategoryTextY = 54;
        private const int StatusTextX = 18;
        private const int StatusTextY = 92;
        private const int SearchMaxLength = 40;
        private const int MainTitleBandHeight = 16;
        private const int CategoryPopupOffsetY = 18;
        private const int CategoryListX = 56;
        private const int CategoryListY = 24;
        private const int CategoryListWidth = 170;
        private const int CategoryRowHeight = 24;
        private const int CategoryVisibleRows = 7;
        private const int CategoryPopupFooterY = 222;
        private const int ScrollBarX = 233;
        private const int ScrollBarY = 23;
        private static readonly string[] CategoryLabels =
        {
            "All",
            "Equip",
            "Use",
            "Etc",
            "Cash",
            "Recipe",
            "Scroll",
            "Special",
            "Package",
            "Button"
        };

        private readonly Texture2D _categoryPopupTexture;
        private readonly Texture2D _searchFieldTexture;
        private readonly Texture2D _pixelTexture;
        private readonly Texture2D _scrollBaseTexture;
        private readonly Texture2D _scrollThumbTexture;
        private readonly Texture2D _scrollPrevTexture;
        private readonly Texture2D _scrollNextTexture;
        private readonly UIObject _toggleAddOnButton;
        private readonly UIObject _searchButton;
        private readonly UIObject _closeButton;
        private readonly List<UIObject> _categoryRowButtons = new();

        private SpriteFont _font;
        private AdminShopDialogUI _sourceDialog;
        private string _searchQuery = string.Empty;
        private string _compositionText = string.Empty;
        private string _statusMessage = "Type an item name, then submit through SearchItemName.";
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Point? _dragStartOffset;
        private int _selectedCategoryIndex;
        private int _categoryScrollOffset;
        private bool _categoryPopupVisible;

        public AdminShopWishListUI(
            IDXObject frame,
            Texture2D categoryPopupTexture,
            Texture2D searchFieldTexture,
            UIObject toggleAddOnButton,
            UIObject searchButton,
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
            _searchButton = searchButton;
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

            if (_searchButton != null)
            {
                AddButton(_searchButton);
                _searchButton.ButtonClickReleased += _ => ExecuteSearch();
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
        }

        public override string WindowName => MapSimulatorWindowNames.AdminShopWishList;
        public override bool CapturesKeyboardInput => IsVisible;

        public void ShowFor(AdminShopDialogUI sourceDialog)
        {
            _sourceDialog = sourceDialog;
            _searchQuery = sourceDialog?.GetWishlistSuggestedQuery() ?? string.Empty;
            _selectedCategoryIndex = Math.Clamp(sourceDialog?.GetWishlistSuggestedCategoryIndex() ?? 0, 0, CategoryLabels.Length - 1);
            _categoryScrollOffset = Math.Clamp(_selectedCategoryIndex - 2, 0, Math.Max(0, CategoryLabels.Length - CategoryVisibleRows));
            _compositionText = string.Empty;
            _categoryPopupVisible = false;
            _statusMessage = $"CUIAdminShopWishList::OnCreate focused the search edit for {sourceDialog?.GetWishlistServiceName() ?? "cash-service"} browsing.";
            PositionRelativeToSource(sourceDialog);
            Show();
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _dragStartOffset = null;
            UpdateCategoryPopupButtons();
        }

        public override void Hide()
        {
            base.Hide();
            _compositionText = string.Empty;
            _categoryPopupVisible = false;
            _dragStartOffset = null;
            UpdateCategoryPopupButtons();
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
            if (_categoryPopupVisible && leftJustPressed)
            {
                if (GetScrollPrevBounds().Contains(mouseState.Position))
                {
                    ScrollCategories(-1);
                }
                else if (GetScrollNextBounds().Contains(mouseState.Position))
                {
                    ScrollCategories(1);
                }
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (_categoryPopupVisible && wheelDelta != 0 && GetCategoryPopupBounds().Contains(mouseState.Position))
            {
                ScrollCategories(wheelDelta > 0 ? -1 : 1);
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
            if (_categoryPopupVisible)
            {
                dragBounds = Rectangle.Union(dragBounds, GetCategoryPopupBounds());
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

                int maxWidth = _categoryPopupVisible ? Math.Max(CurrentFrame?.Width ?? 265, _categoryPopupTexture?.Width ?? 264) : CurrentFrame?.Width ?? 265;
                int maxHeight = _categoryPopupVisible ? Math.Max(CurrentFrame?.Height ?? 135, CategoryPopupOffsetY + (_categoryPopupTexture?.Height ?? 274)) : CurrentFrame?.Height ?? 135;
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
            sprite.DrawString(_font, CategoryLabels[_selectedCategoryIndex], new Vector2(Position.X + CategoryTextX, Position.Y + CategoryTextY), new Color(50, 56, 94));
            sprite.DrawString(_font, TrimToWidth(_statusMessage, 224f), new Vector2(Position.X + StatusTextX, Position.Y + StatusTextY), new Color(255, 233, 160));

            if (_categoryPopupVisible)
            {
                DrawCategoryPopup(sprite);
            }
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            yield return new Rectangle(Position.X, Position.Y - MainTitleBandHeight, CurrentFrame?.Width ?? 265, MainTitleBandHeight);
            if (_categoryPopupVisible)
            {
                yield return GetCategoryPopupBounds();
            }
        }

        private void DrawCategoryPopup(SpriteBatch sprite)
        {
            Rectangle popupBounds = GetCategoryPopupBounds();
            if (_categoryPopupTexture != null)
            {
                sprite.Draw(_categoryPopupTexture, popupBounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, popupBounds, new Color(74, 134, 186));
            }

            for (int row = 0; row < CategoryVisibleRows; row++)
            {
                int categoryIndex = _categoryScrollOffset + row;
                if (categoryIndex >= CategoryLabels.Length)
                {
                    break;
                }

                Rectangle rowBounds = GetCategoryRowBounds(row);
                bool selected = categoryIndex == _selectedCategoryIndex;
                sprite.Draw(_pixelTexture, rowBounds, selected ? new Color(255, 255, 255, 120) : new Color(255, 255, 255, 40));
                sprite.DrawString(_font, CategoryLabels[categoryIndex], new Vector2(rowBounds.X + 8, rowBounds.Y + 4), selected ? new Color(40, 55, 96) : Color.White);
            }

            DrawPopupScrollBar(sprite);
            sprite.DrawString(_font, "ToggleAddOn switched the category add-on.", new Vector2(popupBounds.X + 18, popupBounds.Y + CategoryPopupFooterY), new Color(255, 233, 160));
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
            if (WasPressed(keyboardState, Keys.Escape))
            {
                CloseOwner();
                return;
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
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

            if (!_categoryPopupVisible)
            {
                return;
            }

            if (WasPressed(keyboardState, Keys.Up))
            {
                SelectCategory(_selectedCategoryIndex - 1, true);
            }
            else if (WasPressed(keyboardState, Keys.Down))
            {
                SelectCategory(_selectedCategoryIndex + 1, true);
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                SelectCategory(_selectedCategoryIndex - CategoryVisibleRows, true);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                SelectCategory(_selectedCategoryIndex + CategoryVisibleRows, true);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                SelectCategory(0, true);
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                SelectCategory(CategoryLabels.Length - 1, true);
            }
        }

        private void ExecuteSearch()
        {
            if (_sourceDialog == null)
            {
                _statusMessage = "Wish-list owner has no active cash-service dialog to search.";
                return;
            }

            _sourceDialog.TrySubmitWishlistSearch(_searchQuery, _selectedCategoryIndex, out _statusMessage);
            _compositionText = string.Empty;
        }

        private void ToggleAddOn()
        {
            _categoryPopupVisible = !_categoryPopupVisible;
            _statusMessage = _categoryPopupVisible
                ? "ToggleAddOn opened the wish-list category add-on."
                : "ToggleAddOn closed the wish-list category add-on.";
            UpdateCategoryPopupButtons();
        }

        private void CloseOwner()
        {
            Hide();
        }

        private void SelectVisibleCategory(int rowIndex)
        {
            SelectCategory(_categoryScrollOffset + rowIndex, false);
        }

        private void SelectCategory(int categoryIndex, bool keepPopupOpen)
        {
            _selectedCategoryIndex = Math.Clamp(categoryIndex, 0, CategoryLabels.Length - 1);
            if (_selectedCategoryIndex < _categoryScrollOffset)
            {
                _categoryScrollOffset = _selectedCategoryIndex;
            }
            else if (_selectedCategoryIndex >= _categoryScrollOffset + CategoryVisibleRows)
            {
                _categoryScrollOffset = _selectedCategoryIndex - CategoryVisibleRows + 1;
            }

            _categoryScrollOffset = Math.Clamp(_categoryScrollOffset, 0, Math.Max(0, CategoryLabels.Length - CategoryVisibleRows));
            _categoryPopupVisible = keepPopupOpen;
            _statusMessage = $"Wish-list category set to {CategoryLabels[_selectedCategoryIndex]}.";
            UpdateCategoryPopupButtons();
        }

        private void ScrollCategories(int delta)
        {
            _categoryScrollOffset = Math.Clamp(_categoryScrollOffset + delta, 0, Math.Max(0, CategoryLabels.Length - CategoryVisibleRows));
            UpdateCategoryPopupButtons();
        }

        private void UpdateCategoryPopupButtons()
        {
            foreach (UIObject rowButton in _categoryRowButtons)
            {
                rowButton.SetVisible(_categoryPopupVisible);
                rowButton.SetEnabled(_categoryPopupVisible);
            }
        }

        private void PositionRelativeToSource(AdminShopDialogUI sourceDialog)
        {
            if (sourceDialog != null)
            {
                Position = new Point(sourceDialog.Position.X + 100, sourceDialog.Position.Y + 28);
            }
        }

        private Rectangle GetCategoryPopupBounds()
        {
            return new Rectangle(Position.X, Position.Y + CategoryPopupOffsetY, _categoryPopupTexture?.Width ?? 264, _categoryPopupTexture?.Height ?? 274);
        }

        private Rectangle GetCategoryRowBounds(int visibleRowIndex)
        {
            Rectangle popupBounds = GetCategoryPopupBounds();
            return new Rectangle(popupBounds.X + CategoryListX, popupBounds.Y + CategoryListY + (visibleRowIndex * CategoryRowHeight), CategoryListWidth, CategoryRowHeight - 2);
        }

        private Rectangle GetScrollBaseBounds()
        {
            Rectangle popupBounds = GetCategoryPopupBounds();
            return new Rectangle(popupBounds.X + ScrollBarX, popupBounds.Y + ScrollBarY, _scrollBaseTexture?.Width ?? 15, _scrollBaseTexture?.Height ?? 167);
        }

        private Rectangle GetScrollPrevBounds()
        {
            Rectangle popupBounds = GetCategoryPopupBounds();
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
            int maxOffset = Math.Max(0, CategoryLabels.Length - CategoryVisibleRows);
            if (maxOffset <= 0)
            {
                return trackBounds;
            }

            int thumbHeight = _scrollThumbTexture?.Height ?? 25;
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbY = trackBounds.Y + (int)Math.Round((_categoryScrollOffset / (float)maxOffset) * travel);
            return new Rectangle(baseBounds.X + 1, thumbY, _scrollThumbTexture?.Width ?? 13, thumbHeight);
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
