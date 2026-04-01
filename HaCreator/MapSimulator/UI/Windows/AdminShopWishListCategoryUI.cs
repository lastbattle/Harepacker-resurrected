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
            public int Depth { get; init; }
            public bool IsExpanded { get; init; }
            public bool HasChildren => Node?.Children?.Count > 0;
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

        private readonly Texture2D _backgroundTexture;
        private readonly Texture2D _pixelTexture;
        private readonly Texture2D _scrollBaseTexture;
        private readonly Texture2D _scrollThumbTexture;
        private readonly Texture2D _scrollPrevTexture;
        private readonly Texture2D _scrollNextTexture;
        private readonly UIObject _closeButton;
        private readonly List<UIObject> _rowButtons = new();
        private readonly List<CategoryDisplayRow> _categoryRows = new();
        private readonly HashSet<string> _expandedCategoryKeys = new(StringComparer.OrdinalIgnoreCase);

        private SpriteFont _font;
        private AdminShopWishListUI _owner;
        private IReadOnlyList<AdminShopDialogUI.WishlistCategoryNode> _categoryTree = Array.Empty<AdminShopDialogUI.WishlistCategoryNode>();
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;
        private Point? _dragStartOffset;
        private string _selectedCategoryKey = "all";
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
            _expandedCategoryKeys.Clear();
            if (owner?.GetExpandedWishlistCategoryKeys() is IReadOnlyCollection<string> expandedKeys)
            {
                foreach (string expandedKey in expandedKeys)
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
            PositionRelativeToOwner(owner);
            Show();
        }

        public void HideForOwner(string message)
        {
            if (IsVisible)
            {
                _owner?.OnCategoryAddOnClosed(_selectedCategoryKey, _expandedCategoryKeys, message);
            }

            Hide();
        }

        public override void Show()
        {
            base.Show();
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
            _dragStartOffset = null;
            SetRowButtonsActive(true);
        }

        public override void Hide()
        {
            base.Hide();
            _dragStartOffset = null;
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

            Rectangle dragBounds = new Rectangle(Position.X, Position.Y - MainTitleBandHeight, CurrentFrame?.Width ?? 264, (CurrentFrame?.Height ?? 274) + MainTitleBandHeight);
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

                Position = new Point(
                    Math.Clamp(mouseState.X - _dragStartOffset.Value.X, 0, Math.Max(0, renderWidth - dragBounds.Width)),
                    Math.Clamp(mouseState.Y - _dragStartOffset.Value.Y, MainTitleBandHeight, Math.Max(MainTitleBandHeight, renderHeight - dragBounds.Height)));
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

            Rectangle windowBounds = GetWindowBounds();
            if (_backgroundTexture != null)
            {
                sprite.Draw(_backgroundTexture, windowBounds.Location.ToVector2(), Color.White);
            }
            else
            {
                sprite.Draw(_pixelTexture, windowBounds, new Color(74, 134, 186));
            }

            sprite.DrawString(_font, TrimToWidth("CUIAdminShopWishListCategory", 184f), new Vector2(windowBounds.X + CategoryListX, windowBounds.Y + PopupHeaderY), Color.White);
            for (int row = 0; row < CategoryVisibleRows; row++)
            {
                int categoryIndex = _scrollOffset + row;
                if (categoryIndex >= _categoryRows.Count)
                {
                    break;
                }

                CategoryDisplayRow categoryRow = _categoryRows[categoryIndex];
                Rectangle rowBounds = GetCategoryRowBounds(row);
                bool selected = string.Equals(categoryRow.Node?.Key, _selectedCategoryKey, StringComparison.OrdinalIgnoreCase);
                sprite.Draw(_pixelTexture, rowBounds, selected ? new Color(255, 255, 255, 120) : new Color(255, 255, 255, 40));
                string prefix = categoryRow.HasChildren ? (categoryRow.IsExpanded ? "- " : "+ ") : "  ";
                string label = prefix + new string(' ', categoryRow.Depth * 2) + categoryRow.Node.Label;
                sprite.DrawString(_font, TrimToWidth(label, 182f), new Vector2(rowBounds.X + 6, rowBounds.Y + 2), selected ? new Color(40, 55, 96) : Color.White);
            }

            DrawScrollBar(sprite);
            sprite.DrawString(_font, $"Selected: {TrimToWidth(GetSelectedCategoryLabel(), 172f)}", new Vector2(windowBounds.X + 18, windowBounds.Y + CategoryPopupFooterY - 14), new Color(255, 233, 160));
            sprite.DrawString(_font, "Left/Right collapse or expand. Enter confirms.", new Vector2(windowBounds.X + 18, windowBounds.Y + CategoryPopupFooterY), new Color(255, 233, 160));
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            yield return new Rectangle(Position.X, Position.Y - MainTitleBandHeight, CurrentFrame?.Width ?? 264, (CurrentFrame?.Height ?? 274) + MainTitleBandHeight);
        }

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
                SelectCategoryByRowIndex(0);
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                SelectCategoryByRowIndex(_categoryRows.Count - 1);
            }
            else if (WasPressed(keyboardState, Keys.Left))
            {
                CollapseSelectedCategoryOrMoveToParent();
            }
            else if (WasPressed(keyboardState, Keys.Right))
            {
                ExpandSelectedCategoryOrMoveToFirstChild();
            }
            else if (WasPressed(keyboardState, Keys.Enter))
            {
                HideForOwner($"Wish-list category set to {GetSelectedCategoryLabel()}.");
            }
        }

        private void SetSelectedCategory(string categoryKey)
        {
            _selectedCategoryKey = string.IsNullOrWhiteSpace(categoryKey) ? "all" : categoryKey;
            EnsureCategoryPathExpanded(_selectedCategoryKey);
            RefreshCategoryRows();
            EnsureSelectedCategoryVisible();
            _owner?.SyncCategoryAddOnState(_selectedCategoryKey, _expandedCategoryKeys, $"Wish-list category set to {GetSelectedCategoryLabel()}.");
        }

        private void SelectVisibleCategory(int rowIndex)
        {
            SelectCategoryByRowIndex(_scrollOffset + rowIndex);
        }

        private void SelectCategoryByRowIndex(int rowIndex)
        {
            if (_categoryRows.Count == 0)
            {
                return;
            }

            int normalizedIndex = Math.Clamp(rowIndex, 0, _categoryRows.Count - 1);
            SetSelectedCategory(_categoryRows[normalizedIndex].Node?.Key);
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

            SelectCategoryByRowIndex(selectedIndex + delta);
        }

        private void ScrollCategories(int delta)
        {
            _scrollOffset = Math.Clamp(_scrollOffset + delta, 0, Math.Max(0, _categoryRows.Count - CategoryVisibleRows));
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
                _owner?.SyncCategoryAddOnState(_selectedCategoryKey, _expandedCategoryKeys, $"Wish-list category set to {GetSelectedCategoryLabel()}.");
                return;
            }

            if (path.Count > 1)
            {
                SetSelectedCategory(path[^2].Key);
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
                SetSelectedCategory(selectedNode.Children[0].Key);
            }
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

        private string GetSelectedCategoryLabel()
        {
            return _owner?.ResolveWishlistCategoryLabel(_selectedCategoryKey) ?? "All";
        }

        private void PositionRelativeToOwner(AdminShopWishListUI owner)
        {
            if (owner != null)
            {
                Position = new Point(owner.Position.X, owner.Position.Y + 18);
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

        private Rectangle GetWindowBounds()
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

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
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
