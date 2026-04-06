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
    public sealed class AdminShopWishListCategoryUI : UIWindowBase
    {
        private sealed class CategoryDisplayRow
        {
            public AdminShopDialogUI.WishlistCategoryNode Node { get; init; }
        }

        private const int MainTitleBandHeight = 16;
        private const int PopupHeaderY = 12;
        private const int CategoryListX = 18;
        private const int CategoryListY = 24;
        private const int CategoryListWidth = 206;
        private const int CategoryRowHeight = 21;
        private const int CategoryVisibleRows = 7;
        private const int CategoryPopupFooterY = 236;
        private const int ScrollBarX = 233;
        private const int ScrollBarY = 23;
        private const int CloseButtonX = 180;
        private const int CloseButtonY = 7;
        private const int OwnerOffsetY = 18;

        private readonly Texture2D _backgroundTexture;
        private readonly Texture2D _pixelTexture;
        private readonly Texture2D _scrollBaseTexture;
        private readonly Texture2D _scrollThumbTexture;
        private readonly Texture2D _scrollPrevTexture;
        private readonly Texture2D _scrollNextTexture;
        private readonly UIObject _closeButton;
        private readonly List<UIObject> _rowButtons = new();
        private readonly List<CategoryDisplayRow> _categoryRows = new();

        private SpriteFont _font;
        private AdminShopWishListUI _owner;
        private IReadOnlyList<AdminShopDialogUI.WishlistCategoryNode> _categoryTree = Array.Empty<AdminShopDialogUI.WishlistCategoryNode>();
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private string _selectedCategoryKey = "all";
        private string _activeParentCategoryKey;
        private int _scrollOffset;

        public AdminShopWishListCategoryUI(
            IDXObject frame,
            Texture2D backgroundTexture,
            UIObject closeButton,
            Texture2D scrollBaseTexture,
            Texture2D scrollThumbTexture,
            Texture2D scrollPrevTexture,
            Texture2D scrollNextTexture,
            GraphicsDevice device)
            : base(frame)
        {
            _backgroundTexture = backgroundTexture;
            _closeButton = closeButton;
            _scrollBaseTexture = scrollBaseTexture;
            _scrollThumbTexture = scrollThumbTexture;
            _scrollPrevTexture = scrollPrevTexture;
            _scrollNextTexture = scrollNextTexture;

            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            if (_closeButton != null)
            {
                _closeButton.X = CloseButtonX;
                _closeButton.Y = CloseButtonY;
                AddButton(_closeButton);
                _closeButton.ButtonClickReleased += _ => HideForOwner("ToggleAddOn closed the wish-list category add-on.");
            }

            for (int i = 0; i < CategoryVisibleRows; i++)
            {
                UIObject rowButton = CreateTransparentButton(device, CategoryListWidth, CategoryRowHeight);
                rowButton.X = CategoryListX;
                rowButton.Y = CategoryListY + (i * CategoryRowHeight);
                int capturedRow = i;
                rowButton.ButtonClickReleased += _ => SelectVisibleCategory(capturedRow);
                AddButton(rowButton);
                _rowButtons.Add(rowButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.AdminShopWishListCategory;
        public override bool CapturesKeyboardInput => IsVisible;

        public void ShowFor(AdminShopWishListUI owner)
        {
            _owner = owner;
            _categoryTree = owner?.GetWishlistCategoryTree() ?? Array.Empty<AdminShopDialogUI.WishlistCategoryNode>();
            _selectedCategoryKey = owner?.GetSelectedWishlistCategoryKey() ?? "all";
            _activeParentCategoryKey = ResolveParentCategoryKey(_selectedCategoryKey);
            RefreshCategoryRows();
            EnsureSelectedCategoryVisible();
            PositionRelativeToOwner(owner);
            Show();
        }

        public void HideForOwner(string message)
        {
            if (IsVisible)
            {
                _owner?.OnCategoryAddOnClosed(_selectedCategoryKey, Array.Empty<string>(), message);
            }

            Hide();
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            PositionRelativeToOwner(_owner);
            SetRowButtonsActive(true);
        }

        public override void Hide()
        {
            base.Hide();
            SetRowButtonsActive(false);
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
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                return;
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (leftJustPressed)
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
            if (wheelDelta != 0 && GetWindowBounds().Contains(mouseState.Position))
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

            Rectangle windowBounds = GetWindowBounds();
            if (_backgroundTexture != null)
            {
                sprite.Draw(_backgroundTexture, windowBounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, windowBounds, new Color(74, 134, 186));
            }

            sprite.DrawString(_font, TrimToWidth(GetHeaderLabel(), 184f), new Vector2(windowBounds.X + CategoryListX, windowBounds.Y + PopupHeaderY), Color.White);
            for (int row = 0; row < CategoryVisibleRows; row++)
            {
                int categoryIndex = _scrollOffset + row;
                if (categoryIndex >= _categoryRows.Count)
                {
                    break;
                }

                AdminShopDialogUI.WishlistCategoryNode categoryNode = _categoryRows[categoryIndex].Node;
                Rectangle rowBounds = GetCategoryRowBounds(row);
                bool selected = string.Equals(categoryNode?.Key, _selectedCategoryKey, StringComparison.OrdinalIgnoreCase);
                sprite.Draw(_pixelTexture, rowBounds, selected ? new Color(255, 255, 255, 120) : new Color(255, 255, 255, 40));
                string label = categoryNode?.Children?.Count > 0 && !IsShowingChildStage
                    ? $"> {categoryNode.Label}"
                    : categoryNode?.Label ?? string.Empty;
                sprite.DrawString(_font, TrimToWidth(label, 182f), new Vector2(rowBounds.X + 6, rowBounds.Y + 2), selected ? new Color(40, 55, 96) : Color.White);
            }

            DrawScrollBar(sprite);
            sprite.DrawString(_font, $"Selected: {TrimToWidth(GetSelectedCategoryLabel(), 172f)}", new Vector2(windowBounds.X + 18, windowBounds.Y + CategoryPopupFooterY - 14), new Color(255, 233, 160));
            sprite.DrawString(_font, TrimToWidth(GetFooterHint(), 212f), new Vector2(windowBounds.X + 18, windowBounds.Y + CategoryPopupFooterY), new Color(255, 233, 160));
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            yield return GetWindowBounds();
        }

        private bool IsShowingChildStage => TryGetActiveParentNode(out _);

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (WasPressed(keyboardState, Keys.Escape))
            {
                HideForOwner("Wish-list category add-on closed.");
                return;
            }

            if (WasPressed(keyboardState, Keys.Up))
            {
                MoveCategorySelection(-1);
            }
            else if (WasPressed(keyboardState, Keys.Down))
            {
                MoveCategorySelection(1);
            }
            else if (WasPressed(keyboardState, Keys.PageUp))
            {
                MoveCategorySelection(-CategoryVisibleRows);
            }
            else if (WasPressed(keyboardState, Keys.PageDown))
            {
                MoveCategorySelection(CategoryVisibleRows);
            }
            else if (WasPressed(keyboardState, Keys.Home))
            {
                SelectCategoryByRowIndex(0, activateCategory: false, closeOnLeaf: false);
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                SelectCategoryByRowIndex(_categoryRows.Count - 1, activateCategory: false, closeOnLeaf: false);
            }
            else if (WasPressed(keyboardState, Keys.Left))
            {
                ReturnToTopLevel();
            }
            else if (WasPressed(keyboardState, Keys.Right) || WasPressed(keyboardState, Keys.Enter))
            {
                ActivateSelectedCategory(closeOnLeaf: WasPressed(keyboardState, Keys.Enter), preferredChildCategoryKey: _selectedCategoryKey);
            }
        }

        private void SelectVisibleCategory(int rowIndex)
        {
            SelectCategoryByRowIndex(_scrollOffset + rowIndex, activateCategory: true, closeOnLeaf: true);
        }

        private void SelectCategoryByRowIndex(int rowIndex, bool activateCategory, bool closeOnLeaf)
        {
            if (_categoryRows.Count == 0)
            {
                return;
            }

            int normalizedIndex = Math.Clamp(rowIndex, 0, _categoryRows.Count - 1);
            AdminShopDialogUI.WishlistCategoryNode categoryNode = _categoryRows[normalizedIndex].Node;
            if (categoryNode == null)
            {
                return;
            }

            string previousCategoryKey = _selectedCategoryKey;
            _selectedCategoryKey = string.IsNullOrWhiteSpace(categoryNode.Key) ? "all" : categoryNode.Key;
            EnsureSelectedCategoryVisible();
            _owner?.SyncCategoryAddOnState(_selectedCategoryKey, Array.Empty<string>(), $"Wish-list category staged {GetSelectedCategoryLabel()}.");

            if (activateCategory)
            {
                ActivateSelectedCategory(closeOnLeaf, previousCategoryKey);
            }
        }

        private void ActivateSelectedCategory(bool closeOnLeaf, string preferredChildCategoryKey)
        {
            AdminShopDialogUI.WishlistCategoryNode selectedNode = FindNode(_categoryTree, _selectedCategoryKey);
            if (selectedNode?.Children?.Count > 0 && !IsShowingChildStage)
            {
                _activeParentCategoryKey = selectedNode.Key;
                RefreshCategoryRows();
                _selectedCategoryKey = ResolveInitialChildSelection(selectedNode, preferredChildCategoryKey);
                EnsureSelectedCategoryVisible();
                _owner?.SyncCategoryAddOnState(_selectedCategoryKey, Array.Empty<string>(), $"ToggleAddOn opened {selectedNode.Label} rows.");
                return;
            }

            if (closeOnLeaf)
            {
                HideForOwner($"Wish-list category set to {GetSelectedCategoryLabel()}.");
            }
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

            SelectCategoryByRowIndex(selectedIndex + delta, activateCategory: false, closeOnLeaf: false);
        }

        private void ScrollCategories(int delta)
        {
            _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, Math.Max(0, _categoryRows.Count - CategoryVisibleRows));
        }

        private void ReturnToTopLevel()
        {
            if (!TryGetActiveParentNode(out AdminShopDialogUI.WishlistCategoryNode parentNode))
            {
                return;
            }

            _activeParentCategoryKey = null;
            RefreshCategoryRows();
            _selectedCategoryKey = parentNode.Key;
            EnsureSelectedCategoryVisible();
            _owner?.SyncCategoryAddOnState(_selectedCategoryKey, Array.Empty<string>(), $"ToggleAddOn returned to {parentNode.Label}.");
        }

        private void RefreshCategoryRows()
        {
            _categoryRows.Clear();
            IEnumerable<AdminShopDialogUI.WishlistCategoryNode> nodes = TryGetActiveParentNode(out AdminShopDialogUI.WishlistCategoryNode parentNode)
                ? parentNode.Children
                : _categoryTree;

            foreach (AdminShopDialogUI.WishlistCategoryNode node in nodes ?? Array.Empty<AdminShopDialogUI.WishlistCategoryNode>())
            {
                if (node != null)
                {
                    _categoryRows.Add(new CategoryDisplayRow { Node = node });
                }
            }

            _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _categoryRows.Count - CategoryVisibleRows));
        }

        private bool TryGetActiveParentNode(out AdminShopDialogUI.WishlistCategoryNode parentNode)
        {
            parentNode = FindNode(_categoryTree, _activeParentCategoryKey);
            return parentNode?.Children?.Count > 0;
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
                _scrollOffset = 0;
                return;
            }

            if (selectedIndex < _scrollOffset)
            {
                _scrollOffset = selectedIndex;
            }
            else if (selectedIndex >= _scrollOffset + CategoryVisibleRows)
            {
                _scrollOffset = selectedIndex - CategoryVisibleRows + 1;
            }

            _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, _categoryRows.Count - CategoryVisibleRows));
        }

        private string ResolveParentCategoryKey(string categoryKey)
        {
            if (string.IsNullOrWhiteSpace(categoryKey) || string.Equals(categoryKey, "all", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!TryFindCategoryPath(_categoryTree, categoryKey, out List<AdminShopDialogUI.WishlistCategoryNode> path) || path.Count < 2)
            {
                return null;
            }

            AdminShopDialogUI.WishlistCategoryNode parentNode = path[^2];
            return parentNode.Children?.Count > 0 ? parentNode.Key : null;
        }

        private string GetHeaderLabel()
        {
            if (TryGetActiveParentNode(out AdminShopDialogUI.WishlistCategoryNode parentNode))
            {
                return TrimToWidth(parentNode.Label, 184f);
            }

            return "CUIAdminShopWishListCategory";
        }

        private string GetFooterHint()
        {
            return IsShowingChildStage
                ? "Left returns to category rows. Enter confirms."
                : "Right opens child rows. Enter confirms.";
        }

        private string GetSelectedCategoryLabel()
        {
            return _owner?.ResolveWishlistCategoryLabel(_selectedCategoryKey) ?? "All";
        }

        private string ResolveInitialChildSelection(AdminShopDialogUI.WishlistCategoryNode parentNode, string preferredChildCategoryKey)
        {
            if (parentNode?.Children == null || parentNode.Children.Count == 0)
            {
                return "all";
            }

            if (TryFindCategoryPath(parentNode.Children, preferredChildCategoryKey, out List<AdminShopDialogUI.WishlistCategoryNode> existingPath)
                && existingPath.Count > 0
                && !string.IsNullOrWhiteSpace(existingPath[0].Key))
            {
                return existingPath[0].Key;
            }

            return FindFirstLeafKey(parentNode) ?? parentNode.Children[0].Key;
        }

        private static string FindFirstLeafKey(AdminShopDialogUI.WishlistCategoryNode node)
        {
            if (node == null)
            {
                return null;
            }

            if (node.Children == null || node.Children.Count == 0)
            {
                return node.Key;
            }

            foreach (AdminShopDialogUI.WishlistCategoryNode child in node.Children)
            {
                string leafKey = FindFirstLeafKey(child);
                if (!string.IsNullOrWhiteSpace(leafKey))
                {
                    return leafKey;
                }
            }

            return null;
        }

        private void PositionRelativeToOwner(AdminShopWishListUI owner)
        {
            if (owner != null)
            {
                Position = new Point(owner.Position.X, owner.Position.Y + OwnerOffsetY);
            }
        }

        private void SetRowButtonsActive(bool active)
        {
            foreach (UIObject rowButton in _rowButtons)
            {
                rowButton.SetVisible(active);
                rowButton.SetEnabled(active);
            }
        }

        private new Rectangle GetWindowBounds()
        {
            return new Rectangle(Position.X, Position.Y, _backgroundTexture?.Width ?? 264, _backgroundTexture?.Height ?? 274);
        }

        private Rectangle GetCategoryRowBounds(int visibleRowIndex)
        {
            Rectangle windowBounds = GetWindowBounds();
            return new Rectangle(windowBounds.X + CategoryListX, windowBounds.Y + CategoryListY + (visibleRowIndex * CategoryRowHeight), CategoryListWidth, CategoryRowHeight - 2);
        }

        private Rectangle GetScrollBaseBounds()
        {
            Rectangle windowBounds = GetWindowBounds();
            return new Rectangle(windowBounds.X + ScrollBarX, windowBounds.Y + ScrollBarY, _scrollBaseTexture?.Width ?? 15, _scrollBaseTexture?.Height ?? 167);
        }

        private Rectangle GetScrollPrevBounds()
        {
            Rectangle windowBounds = GetWindowBounds();
            return new Rectangle(windowBounds.X + ScrollBarX + 1, windowBounds.Y + ScrollBarY, _scrollPrevTexture?.Width ?? 13, _scrollPrevTexture?.Height ?? 14);
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
            int maxOffset = Math.Max(0, _categoryRows.Count - CategoryVisibleRows);
            if (maxOffset <= 0)
            {
                return trackBounds;
            }

            int thumbHeight = _scrollThumbTexture?.Height ?? 25;
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbY = trackBounds.Y + (int)Math.Round((_scrollOffset / (float)maxOffset) * travel);
            return new Rectangle(baseBounds.X + 1, thumbY, _scrollThumbTexture?.Width ?? 13, thumbHeight);
        }

        private void DrawScrollBar(SpriteBatch sprite)
        {
            Rectangle baseBounds = GetScrollBaseBounds();
            if (_scrollBaseTexture != null)
            {
                sprite.Draw(_scrollBaseTexture, baseBounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, baseBounds, new Color(159, 181, 207));
            }

            Rectangle thumbBounds = GetScrollThumbBounds();
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

        private static AdminShopDialogUI.WishlistCategoryNode FindNode(IEnumerable<AdminShopDialogUI.WishlistCategoryNode> nodes, string categoryKey)
        {
            if (nodes == null || string.IsNullOrWhiteSpace(categoryKey))
            {
                return null;
            }

            foreach (AdminShopDialogUI.WishlistCategoryNode node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (string.Equals(node.Key, categoryKey, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }

                AdminShopDialogUI.WishlistCategoryNode childMatch = FindNode(node.Children, categoryKey);
                if (childMatch != null)
                {
                    return childMatch;
                }
            }

            return null;
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
