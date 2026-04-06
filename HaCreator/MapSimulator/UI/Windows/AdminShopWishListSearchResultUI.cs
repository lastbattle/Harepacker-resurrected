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
    public sealed class AdminShopWishListSearchResultUI : UIWindowBase
    {
        private const int MainTitleBandHeight = 16;
        private const int CloseButtonX = 184;
        private const int CloseButtonY = 6;
        private const int PreviousButtonX = 167;
        private const int PreviousButtonY = 21;
        private const int NextButtonX = 184;
        private const int NextButtonY = 21;
        private const int RegisterButtonX = 145;
        private const int RegisterButtonY = 248;
        private const int ListX = 16;
        private const int ListY = 52;
        private const int ListWidth = 246;
        private const int RowHeight = 24;
        private const int ResultsPerPage = 10;
        private const int HeaderX = 16;
        private const int HeaderY = 14;
        private const int DetailX = 280;
        private const int DetailY = 54;
        private const int DetailWidth = 142;
        private const int DetailLineHeight = 18;
        private const int FooterY = 392;
        private const int IconX = 375;
        private const int IconY = 296;
        private const int IconSize = 32;

        private readonly Texture2D _backgroundTexture;
        private readonly Texture2D _iconPlaceholderTexture;
        private readonly Texture2D _pixelTexture;
        private readonly UIObject _closeButton;
        private readonly UIObject _previousButton;
        private readonly UIObject _nextButton;
        private readonly UIObject _registerButton;
        private readonly List<UIObject> _rowButtons = new();

        private SpriteFont _font;
        private AdminShopWishListUI _owner;
        private IReadOnlyList<AdminShopDialogUI.WishlistSearchResult> _results = Array.Empty<AdminShopDialogUI.WishlistSearchResult>();
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private int _selectedIndex;
        private int _pageIndex;

        public AdminShopWishListSearchResultUI(
            IDXObject frame,
            Texture2D backgroundTexture,
            Texture2D iconPlaceholderTexture,
            UIObject closeButton,
            UIObject previousButton,
            UIObject nextButton,
            UIObject registerButton,
            GraphicsDevice device)
            : base(frame)
        {
            _backgroundTexture = backgroundTexture;
            _iconPlaceholderTexture = iconPlaceholderTexture;
            _closeButton = closeButton;
            _previousButton = previousButton;
            _nextButton = nextButton;
            _registerButton = registerButton;

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            if (_closeButton != null)
            {
                _closeButton.X = CloseButtonX;
                _closeButton.Y = CloseButtonY;
                AddButton(_closeButton);
                _closeButton.ButtonClickReleased += _ => HideForOwner("Wish-list results closed.");
            }

            if (_previousButton != null)
            {
                _previousButton.X = PreviousButtonX;
                _previousButton.Y = PreviousButtonY;
                AddButton(_previousButton);
                _previousButton.ButtonClickReleased += _ => ChangePage(-1);
            }

            if (_nextButton != null)
            {
                _nextButton.X = NextButtonX;
                _nextButton.Y = NextButtonY;
                AddButton(_nextButton);
                _nextButton.ButtonClickReleased += _ => ChangePage(1);
            }

            if (_registerButton != null)
            {
                _registerButton.X = RegisterButtonX;
                _registerButton.Y = RegisterButtonY;
                AddButton(_registerButton);
                _registerButton.ButtonClickReleased += _ => ApplySelectedResult();
            }

            for (int i = 0; i < ResultsPerPage; i++)
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

        public override string WindowName => MapSimulatorWindowNames.AdminShopWishListSearchResult;
        public override bool CapturesKeyboardInput => IsVisible;

        public void ShowFor(AdminShopWishListUI owner, IReadOnlyList<AdminShopDialogUI.WishlistSearchResult> results)
        {
            _owner = owner;
            _results = results ?? Array.Empty<AdminShopDialogUI.WishlistSearchResult>();
            _selectedIndex = 0;
            _pageIndex = 0;
            PositionRelativeToOwner(owner);
            Show();
        }

        public void HideForOwner(string message)
        {
            if (IsVisible)
            {
                _owner?.OnSearchResultAddOnClosed(message);
            }

            Hide();
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            PositionRelativeToOwner(_owner);
            UpdateButtons();
        }

        public override void Hide()
        {
            base.Hide();
            _results = Array.Empty<AdminShopDialogUI.WishlistSearchResult>();
            UpdateButtons();
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsVisible)
            {
                return;
            }

            if (_owner == null || !_owner.IsVisible)
            {
                Hide();
                return;
            }

            PositionRelativeToOwner(_owner);
            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            HandleKeyboardInput(keyboardState);

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (wheelDelta != 0 && GetWindowBounds().Contains(mouseState.Position))
            {
                SelectIndex(_selectedIndex + (wheelDelta > 0 ? -1 : 1));
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

            return GetWindowBounds().Contains(mouseState.Position);
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

            Rectangle bounds = GetWindowBounds();
            if (_backgroundTexture != null)
            {
                sprite.Draw(_backgroundTexture, bounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, bounds, new Color(74, 134, 186));
            }

            string searchText = _owner?.GetWishlistSearchQuery() ?? string.Empty;
            string categoryText = _owner?.ResolveWishlistCategoryLabel(_owner?.GetSelectedWishlistCategoryKey()) ?? "All";
            string priceText = _owner?.GetSelectedWishlistPriceRangeLabel() ?? "All prices";
            sprite.DrawString(_font, TrimToWidth($"SearchItemName: {searchText}", 232f), new Vector2(bounds.X + HeaderX, bounds.Y + HeaderY), Color.White);
            sprite.DrawString(_font, TrimToWidth($"{categoryText} / {priceText}", 204f), new Vector2(bounds.X + HeaderX, bounds.Y + HeaderY + 18), new Color(255, 233, 160));

            int pageStart = _pageIndex * ResultsPerPage;
            int pageEnd = Math.Min(_results.Count, pageStart + ResultsPerPage);
            for (int visibleRow = 0; visibleRow < ResultsPerPage; visibleRow++)
            {
                int resultIndex = pageStart + visibleRow;
                if (resultIndex >= pageEnd)
                {
                    break;
                }

                AdminShopDialogUI.WishlistSearchResult result = _results[resultIndex];
                Rectangle rowBounds = GetRowBounds(visibleRow);
                bool selected = resultIndex == _selectedIndex;
                sprite.Draw(_pixelTexture, rowBounds, selected ? new Color(255, 255, 255, 120) : new Color(255, 255, 255, 32));
                sprite.DrawString(_font, TrimToWidth(result.Title, 176f), new Vector2(rowBounds.X + 6, rowBounds.Y + 3), selected ? new Color(40, 55, 96) : Color.White);
                sprite.DrawString(_font, TrimToWidth(result.PriceLabel, 54f), new Vector2(rowBounds.Right - 58, rowBounds.Y + 3), selected ? new Color(40, 55, 96) : new Color(255, 233, 160));
                sprite.DrawString(_font, TrimToWidth(result.Seller, 140f), new Vector2(rowBounds.X + 6, rowBounds.Y + 13), selected ? new Color(40, 55, 96) : new Color(214, 223, 236));
            }

            string pageLabel = _results.Count == 0
                ? "0 / 0"
                : $"{_pageIndex + 1} / {GetPageCount()}";
            sprite.DrawString(_font, pageLabel, new Vector2(bounds.X + 208, bounds.Y + 26), new Color(255, 233, 160));

            if (_results.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _results.Count)
            {
                AdminShopDialogUI.WishlistSearchResult selected = _results[_selectedIndex];
                Rectangle iconBounds = new(bounds.X + IconX, bounds.Y + IconY, IconSize, IconSize);
                if (_iconPlaceholderTexture != null)
                {
                    sprite.Draw(_iconPlaceholderTexture, iconBounds.Location.ToVector2(), Color.White);
                }

                if (selected.IconTexture != null)
                {
                    sprite.Draw(selected.IconTexture, iconBounds, Color.White);
                }

                DrawDetailLine(sprite, bounds, 0, selected.Title, Color.White);
                DrawDetailLine(sprite, bounds, 1, selected.CategoryLabel, new Color(255, 233, 160));
                DrawDetailLine(sprite, bounds, 2, selected.Seller, Color.White);
                DrawDetailLine(sprite, bounds, 3, selected.PriceLabel, new Color(255, 233, 160));
                DrawDetailBlock(sprite, bounds, 5, selected.Detail, new Color(214, 223, 236));
                string footer = selected.AlreadyWishlisted
                    ? "This row is already staged in the local wish list."
                    : "BtRegist focuses the selected row in the admin-shop dialog.";
                DrawDetailBlock(sprite, bounds, 10, footer, new Color(255, 233, 160));
            }

            string status = _owner?.GetStatusMessage() ?? string.Empty;
            sprite.DrawString(_font, TrimToWidth(status, 404f), new Vector2(bounds.X + HeaderX, bounds.Y + FooterY), new Color(255, 233, 160));
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            yield return GetWindowBounds();
            yield return new Rectangle(Position.X, Position.Y - MainTitleBandHeight, CurrentFrame?.Width ?? 438, MainTitleBandHeight);
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (WasPressed(keyboardState, Keys.Escape))
            {
                HideForOwner("Wish-list results closed.");
                return;
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
                ApplySelectedResult();
                return;
            }

            if (WasPressed(keyboardState, Keys.Up))
            {
                SelectIndex(_selectedIndex - 1);
            }
            else if (WasPressed(keyboardState, Keys.Down))
            {
                SelectIndex(_selectedIndex + 1);
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                ChangePage(-1);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                ChangePage(1);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                SelectIndex(0);
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                SelectIndex(_results.Count - 1);
            }
            else if (WasPressed(keyboardState, Keys.Left))
            {
                ChangePage(-1);
            }
            else if (WasPressed(keyboardState, Keys.Right))
            {
                ChangePage(1);
            }
        }

        private void ChangePage(int delta)
        {
            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            _pageIndex = Math.Clamp(_pageIndex + delta, 0, pageCount - 1);
            int pageStart = _pageIndex * ResultsPerPage;
            if (_selectedIndex < pageStart || _selectedIndex >= pageStart + ResultsPerPage)
            {
                _selectedIndex = pageStart;
            }

            UpdateButtons();
        }

        private void SelectVisibleRow(int visibleRow)
        {
            SelectIndex((_pageIndex * ResultsPerPage) + visibleRow);
        }

        private void SelectIndex(int index)
        {
            if (_results.Count == 0)
            {
                return;
            }

            _selectedIndex = Math.Clamp(index, 0, _results.Count - 1);
            _pageIndex = _selectedIndex / ResultsPerPage;
            UpdateButtons();
        }

        private void ApplySelectedResult()
        {
            if (_owner == null || _results.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _results.Count)
            {
                HideForOwner("Wish-list result selection is unavailable.");
                return;
            }

            AdminShopDialogUI.WishlistSearchResult result = _results[_selectedIndex];
            if (_owner.TryApplySearchResult(result, out string message))
            {
                _owner.Hide();
                Hide();
                return;
            }

            _owner.OnSearchResultAddOnClosed(message);
        }

        private void UpdateButtons()
        {
            int pageStart = _pageIndex * ResultsPerPage;
            int visibleCount = Math.Max(0, Math.Min(ResultsPerPage, _results.Count - pageStart));
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool active = IsVisible && i < visibleCount;
                _rowButtons[i].SetVisible(active);
                _rowButtons[i].SetEnabled(active);
            }

            if (_previousButton != null)
            {
                bool canMove = IsVisible && _pageIndex > 0;
                _previousButton.SetVisible(IsVisible);
                _previousButton.SetEnabled(canMove);
            }

            if (_nextButton != null)
            {
                bool canMove = IsVisible && _pageIndex + 1 < GetPageCount();
                _nextButton.SetVisible(IsVisible);
                _nextButton.SetEnabled(canMove);
            }

            if (_registerButton != null)
            {
                bool canRegister = IsVisible && _results.Count > 0;
                _registerButton.SetVisible(IsVisible);
                _registerButton.SetEnabled(canRegister);
            }

            if (_closeButton != null)
            {
                _closeButton.SetVisible(IsVisible);
                _closeButton.SetEnabled(IsVisible);
            }
        }

        private new Rectangle GetWindowBounds()
        {
            return new Rectangle(Position.X, Position.Y, CurrentFrame?.Width ?? 438, CurrentFrame?.Height ?? 430);
        }

        private Rectangle GetRowBounds(int visibleRow)
        {
            Rectangle bounds = GetWindowBounds();
            return new Rectangle(bounds.X + ListX, bounds.Y + ListY + (visibleRow * RowHeight), ListWidth, RowHeight - 2);
        }

        private int GetPageCount()
        {
            return _results.Count == 0 ? 0 : (int)Math.Ceiling(_results.Count / (float)ResultsPerPage);
        }

        private void PositionRelativeToOwner(AdminShopWishListUI owner)
        {
            if (owner != null)
            {
                Position = owner.Position;
            }
        }

        private void DrawDetailLine(SpriteBatch sprite, Rectangle bounds, int lineIndex, string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, TrimToWidth(text, DetailWidth), new Vector2(bounds.X + DetailX, bounds.Y + DetailY + (lineIndex * DetailLineHeight)), color);
        }

        private void DrawDetailBlock(SpriteBatch sprite, Rectangle bounds, int lineIndex, string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sprite.DrawString(_font, TrimToWidth(text, DetailWidth), new Vector2(bounds.X + DetailX, bounds.Y + DetailY + (lineIndex * DetailLineHeight)), color);
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
