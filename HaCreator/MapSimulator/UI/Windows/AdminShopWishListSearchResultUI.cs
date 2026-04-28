using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaCreator.MapSimulator.Interaction;
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
        internal const int FooterInfoLineCapacity = 3;
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
        private const int FooterLineHeight = 16;
        private const int IconX = 375;
        private const int IconY = 296;
        private const int IconSize = 32;
        private const int ConfirmWidth = 226;
        private const int ConfirmHeight = 78;
        private const int ConfirmButtonWidth = 74;
        private const int ConfirmButtonHeight = 20;
        private const int ConfirmButtonY = 46;
        private const int ConfirmOkButtonX = 36;
        private const int ConfirmCancelButtonX = 116;

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
        private int _ownerSessionToken;
        private bool _isRegisterConfirmationOpen;
        private bool _confirmAcceptFocused = true;
        private string _statusMessage = string.Empty;

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
                _closeButton.ButtonClickReleased += _ => HideForOwner("CUIAdminShopWishListSearchResult::OnButtonClicked(1000) closed through the parent ToggleAddOn(0) path.");
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

        private bool UseOwnerSession => _owner?.HasWishlistSearchResultSession() == true;

        public void PrepareForShow(AdminShopWishListUI owner, IReadOnlyList<AdminShopDialogUI.WishlistSearchResult> results)
        {
            _owner = owner;
            _results = results ?? Array.Empty<AdminShopDialogUI.WishlistSearchResult>();
            _selectedIndex = -1;
            _pageIndex = UseOwnerSession ? _owner.GetWishlistSearchResultSessionPageIndex() : 0;
            _ownerSessionToken = owner?.GetWishlistSearchResultSessionToken() ?? 0;
            _isRegisterConfirmationOpen = false;
            _confirmAcceptFocused = true;
            _statusMessage = owner?.GetStatusMessage() ?? string.Empty;
            PositionRelativeToOwner(owner);
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
            _selectedIndex = -1;
            _pageIndex = 0;
            _ownerSessionToken = 0;
            _isRegisterConfirmationOpen = false;
            _confirmAcceptFocused = true;
            _statusMessage = string.Empty;
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
            SyncOwnerSessionState();
            KeyboardState keyboardState = Keyboard.GetState();
            MouseState mouseState = Mouse.GetState();
            HandleKeyboardInput(keyboardState);
            if (!IsVisible)
            {
                _previousKeyboardState = keyboardState;
                _previousMouseState = mouseState;
                return;
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (!_isRegisterConfirmationOpen && wheelDelta != 0 && GetWindowBounds().Contains(mouseState.Position))
            {
                if (UseOwnerSession)
                {
                    if (_owner.TryMoveWishlistSearchResultSessionSelection(wheelDelta > 0 ? -1 : 1, out string message))
                    {
                        _statusMessage = message;
                    }
                    else
                    {
                        _statusMessage = _owner.GetStatusMessage();
                    }
                }
                else
                {
                    if (_selectedIndex < 0)
                    {
                        SelectIndex(wheelDelta > 0 ? 0 : GetPageStartIndex());
                    }
                    else
                    {
                        SelectIndex(_selectedIndex + (wheelDelta > 0 ? -1 : 1));
                    }
                }
            }

            bool leftJustPressed = mouseState.LeftButton == ButtonState.Pressed && _previousMouseState.LeftButton == ButtonState.Released;
            if (_isRegisterConfirmationOpen && leftJustPressed)
            {
                if (GetConfirmationButtonBounds(confirm: true).Contains(mouseState.Position))
                {
                    _confirmAcceptFocused = true;
                    ConfirmSelectedResult();
                }
                else if (GetConfirmationButtonBounds(confirm: false).Contains(mouseState.Position))
                {
                    _confirmAcceptFocused = false;
                    CancelSelectedResultConfirmation();
                }
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

            if (_isRegisterConfirmationOpen)
            {
                Rectangle confirmBounds = GetConfirmationBounds();
                if (confirmBounds.Contains(mouseState.Position)
                    || GetConfirmationButtonBounds(confirm: true).Contains(mouseState.Position)
                    || GetConfirmationButtonBounds(confirm: false).Contains(mouseState.Position))
                {
                    mouseCursor?.SetMouseCursorMovedToClickableItem();
                    return true;
                }
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
            string serviceState = _owner?.GetWishlistSearchServiceStateSummary() ?? string.Empty;
            sprite.DrawString(_font, TrimToWidth($"SearchItemName: {searchText}", 232f), new Vector2(bounds.X + HeaderX, bounds.Y + HeaderY), Color.White);
            sprite.DrawString(_font, TrimToWidth($"{categoryText} / {priceText}", 204f), new Vector2(bounds.X + HeaderX, bounds.Y + HeaderY + 18), new Color(255, 233, 160));

            IReadOnlyList<AdminShopDialogUI.WishlistSearchResult> visibleResults = GetVisibleResults();
            for (int visibleRow = 0; visibleRow < visibleResults.Count; visibleRow++)
            {
                AdminShopDialogUI.WishlistSearchResult result = visibleResults[visibleRow];
                Rectangle rowBounds = GetRowBounds(visibleRow);
                bool selected = string.Equals(result.EntryKey, GetSelectedResult()?.EntryKey, StringComparison.Ordinal);
                sprite.Draw(_pixelTexture, rowBounds, selected ? new Color(255, 255, 255, 120) : new Color(255, 255, 255, 32));
                sprite.DrawString(_font, TrimToWidth(result.Title, 176f), new Vector2(rowBounds.X + 6, rowBounds.Y + 3), selected ? new Color(40, 55, 96) : Color.White);
                sprite.DrawString(_font, TrimToWidth(result.PriceLabel, 54f), new Vector2(rowBounds.Right - 58, rowBounds.Y + 3), selected ? new Color(40, 55, 96) : new Color(255, 233, 160));
                sprite.DrawString(_font, TrimToWidth(result.Seller, 140f), new Vector2(rowBounds.X + 6, rowBounds.Y + 13), selected ? new Color(40, 55, 96) : new Color(214, 223, 236));
            }

            string pageLabel = GetResultCount() == 0
                ? "0 / 0"
                : $"{GetPageIndex() + 1} / {GetPageCount()}";
            sprite.DrawString(_font, pageLabel, new Vector2(bounds.X + 208, bounds.Y + 26), new Color(255, 233, 160));

            AdminShopDialogUI.WishlistSearchResult selectedResult = GetSelectedResult();
            if (GetResultCount() > 0 && selectedResult != null)
            {
                Rectangle iconBounds = new(bounds.X + IconX, bounds.Y + IconY, IconSize, IconSize);
                if (_iconPlaceholderTexture != null)
                {
                    sprite.Draw(_iconPlaceholderTexture, iconBounds.Location.ToVector2(), Color.White);
                }

                if (selectedResult.IconTexture != null)
                {
                    sprite.Draw(selectedResult.IconTexture, iconBounds, Color.White);
                }

                DrawDetailLine(sprite, bounds, 0, selectedResult.Title, Color.White);
                DrawDetailLine(sprite, bounds, 1, selectedResult.CategoryLabel, new Color(255, 233, 160));
                DrawDetailLine(sprite, bounds, 2, selectedResult.Seller, Color.White);
                DrawDetailLine(sprite, bounds, 3, selectedResult.PriceLabel, new Color(255, 233, 160));
                DrawDetailBlock(sprite, bounds, 5, selectedResult.Detail, new Color(214, 223, 236));
                string footer = selectedResult.AlreadyWishlisted
                    ? "This row is already staged in the local wish-list cache."
                    : "BtRegist focuses the selected row in the admin-shop dialog.";
                DrawDetailBlock(sprite, bounds, 10, footer, new Color(255, 233, 160));
                if (selectedResult.IsPacketAuthored)
                {
                    string sessionLabel = BuildPacketSessionLabel(selectedResult);
                    DrawDetailBlock(
                        sprite,
                        bounds,
                        12,
                        $"Packet row session: {sessionLabel}.",
                        selectedResult.CanRegister ? new Color(198, 208, 224) : new Color(255, 218, 172));
                    DrawDetailBlock(
                        sprite,
                        bounds,
                        13,
                        BuildPacketBindingDetail(selectedResult),
                        ResolvePacketBindingDetailColor(selectedResult));
                }
            }
            else if (GetResultCount() > 0)
            {
                DrawDetailBlock(sprite, bounds, 0, "Select a staged result to review or register it.", Color.White);
                DrawDetailBlock(sprite, bounds, 2, "CUIAdminShopWishListSearchResult starts with no row selected.", new Color(255, 233, 160));
            }
            else
            {
                DrawDetailBlock(sprite, bounds, 0, "No staged result rows are currently available.", Color.White);
                DrawDetailBlock(sprite, bounds, 2, "The child owner now follows packet-owned session invalidation instead of showing a detached local copy.", new Color(255, 233, 160));
            }

            string status = string.IsNullOrWhiteSpace(_statusMessage)
                ? _owner?.GetStatusMessage() ?? string.Empty
                : _statusMessage;
            IReadOnlyList<string> serviceStateDetails = _owner?.GetWishlistSearchServiceStateDetailLines() ?? Array.Empty<string>();
            IReadOnlyList<(string Text, Color Color)> footerInfoLines = BuildFooterInfoLines(serviceState, serviceStateDetails);
            int footerInfoStartY = bounds.Y + FooterY - (footerInfoLines.Count * FooterLineHeight);
            for (int i = 0; i < footerInfoLines.Count; i++)
            {
                (string footerLine, Color footerColor) = footerInfoLines[i];
                sprite.DrawString(
                    _font,
                    TrimToWidth(footerLine, 404f),
                    new Vector2(bounds.X + HeaderX, footerInfoStartY + (i * FooterLineHeight)),
                    footerColor);
            }

            sprite.DrawString(_font, TrimToWidth(status, 404f), new Vector2(bounds.X + HeaderX, bounds.Y + FooterY), new Color(255, 233, 160));

            if (_isRegisterConfirmationOpen)
            {
                DrawRegisterConfirmation(sprite, bounds);
            }
        }

        protected override IEnumerable<Rectangle> GetAdditionalInteractiveBounds()
        {
            yield return GetWindowBounds();
            yield return new Rectangle(Position.X, Position.Y - MainTitleBandHeight, CurrentFrame?.Width ?? 438, MainTitleBandHeight);
        }

        private void HandleKeyboardInput(KeyboardState keyboardState)
        {
            if (_isRegisterConfirmationOpen)
            {
                HandleConfirmationKeyboardInput(keyboardState);
                return;
            }

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
                MoveSelection(-1);
            }
            else if (WasPressed(keyboardState, Keys.Down))
            {
                MoveSelection(1);
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
                SelectVisibleRow(0);
            }
            else if (WasPressed(keyboardState, Keys.End))
            {
                SelectVisibleRow(Math.Max(0, GetVisibleResults().Count - 1));
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
            if (UseOwnerSession)
            {
                if (_owner.TryChangeWishlistSearchResultSessionPage(delta, out string message))
                {
                    _statusMessage = message;
                }
                else
                {
                    _statusMessage = _owner.GetStatusMessage();
                }

                UpdateButtons();
                return;
            }

            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            _pageIndex = Math.Clamp(_pageIndex + delta, 0, pageCount - 1);
            _selectedIndex = -1;
            _statusMessage = $"SearchItemName moved to result page {_pageIndex + 1} / {pageCount}.";
            UpdateButtons();
        }

        private void SelectVisibleRow(int visibleRow)
        {
            if (UseOwnerSession)
            {
                if (_owner.TrySelectWishlistSearchResultSessionVisibleRow(visibleRow, out string message))
                {
                    _statusMessage = message;
                }
                else
                {
                    _statusMessage = _owner.GetStatusMessage();
                }

                UpdateButtons();
                return;
            }

            SelectIndex((_pageIndex * ResultsPerPage) + visibleRow);
        }

        private void MoveSelection(int delta)
        {
            if (UseOwnerSession)
            {
                if (_owner.TryMoveWishlistSearchResultSessionSelection(delta, out string message))
                {
                    _statusMessage = message;
                }
                else
                {
                    _statusMessage = _owner.GetStatusMessage();
                }

                UpdateButtons();
                return;
            }

            SelectIndex(_selectedIndex < 0 ? GetPageStartIndex() : _selectedIndex + delta);
        }

        private void SelectIndex(int index)
        {
            if (_results.Count == 0)
            {
                return;
            }

            _selectedIndex = Math.Clamp(index, 0, _results.Count - 1);
            _pageIndex = _selectedIndex / ResultsPerPage;
            _isRegisterConfirmationOpen = false;
            _confirmAcceptFocused = true;
            _statusMessage = $"Wish-list result selected: {_results[_selectedIndex].Title}.";
            UpdateButtons();
        }

        private void ApplySelectedResult()
        {
            AdminShopDialogUI.WishlistSearchResult result = GetSelectedResult();
            if (_owner == null || result == null)
            {
                _statusMessage = "Select a wish-list result before using BtRegist.";
                return;
            }

            if (!result.CanRegister)
            {
                _statusMessage = BuildResultNotRegisterableMessage(result);
                UpdateButtons();
                return;
            }

            if (result.AlreadyWishlisted)
            {
                _statusMessage = $"{result.Title} is already saved in the in-session wish-list cache.";
                UpdateButtons();
                return;
            }

            _isRegisterConfirmationOpen = true;
            _confirmAcceptFocused = true;
            _statusMessage = $"BtRegist opened CUtilDlg::YesNo with StringPool 0x{AdminShopWishListClientParityText.RegisterConfirmationFormatStringPoolId:X} for {result.Title}.";
            UpdateButtons();
        }

        private void ConfirmSelectedResult()
        {
            if (_owner == null)
            {
                _isRegisterConfirmationOpen = false;
                _statusMessage = "Wish-list result selection is unavailable.";
                UpdateButtons();
                return;
            }

            bool applied;
            string message;
            if (UseOwnerSession)
            {
                applied = _owner.TryApplySelectedWishlistSearchResultSession(out message);
            }
            else
            {
                applied = _owner.TryApplySearchResult(_results[_selectedIndex], out message);
            }

            if (applied)
            {
                _owner.Hide();
                Hide();
                return;
            }

            _isRegisterConfirmationOpen = false;
            _confirmAcceptFocused = true;
            _statusMessage = message;
            _owner.OnSearchResultAddOnClosed(message);
            UpdateButtons();
        }

        private void CancelSelectedResultConfirmation()
        {
            _isRegisterConfirmationOpen = false;
            _confirmAcceptFocused = true;
            AdminShopDialogUI.WishlistSearchResult selectedResult = GetSelectedResult();
            _statusMessage = selectedResult != null
                ? $"BtRegist confirmation cancelled for {selectedResult.Title}."
                : "Wish-list registration confirmation cancelled.";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            int visibleCount = GetVisibleResults().Count;
            for (int i = 0; i < _rowButtons.Count; i++)
            {
                bool active = IsVisible && !_isRegisterConfirmationOpen && i < visibleCount;
                _rowButtons[i].SetVisible(active);
                _rowButtons[i].SetEnabled(active);
            }

            if (_previousButton != null)
            {
                bool canMove = IsVisible && !_isRegisterConfirmationOpen && CanMovePage(-1);
                _previousButton.SetVisible(IsVisible);
                _previousButton.SetEnabled(canMove);
            }

            if (_nextButton != null)
            {
                bool canMove = IsVisible && !_isRegisterConfirmationOpen && CanMovePage(1);
                _nextButton.SetVisible(IsVisible);
                _nextButton.SetEnabled(canMove);
            }

            if (_registerButton != null)
            {
                AdminShopDialogUI.WishlistSearchResult selectedResult = GetSelectedResult();
                bool canRegister = IsVisible
                    && !_isRegisterConfirmationOpen
                    && selectedResult != null
                    && selectedResult.CanRegister
                    && !selectedResult.AlreadyWishlisted;
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

        private Rectangle GetConfirmationBounds()
        {
            Rectangle bounds = GetWindowBounds();
            return new Rectangle(
                bounds.X + (bounds.Width - ConfirmWidth) / 2,
                bounds.Y + (bounds.Height - ConfirmHeight) / 2,
                ConfirmWidth,
                ConfirmHeight);
        }

        private Rectangle GetConfirmationButtonBounds(bool confirm)
        {
            Rectangle confirmBounds = GetConfirmationBounds();
            int buttonX = confirm ? ConfirmOkButtonX : ConfirmCancelButtonX;
            return new Rectangle(confirmBounds.X + buttonX, confirmBounds.Y + ConfirmButtonY, ConfirmButtonWidth, ConfirmButtonHeight);
        }

        private int GetPageCount()
        {
            return UseOwnerSession
                ? _owner.GetWishlistSearchResultSessionPageCount()
                : _results.Count == 0 ? 0 : (int)Math.Ceiling(_results.Count / (float)ResultsPerPage);
        }

        private int GetPageStartIndex()
        {
            return Math.Clamp(_pageIndex * ResultsPerPage, 0, Math.Max(0, _results.Count - 1));
        }

        private int GetPageEndIndex()
        {
            if (_results.Count == 0)
            {
                return -1;
            }

            return Math.Min(_results.Count - 1, GetPageStartIndex() + ResultsPerPage - 1);
        }

        private void PositionRelativeToOwner(AdminShopWishListUI owner)
        {
            if (owner != null)
            {
                Position = owner.Position;
            }
        }

        private void SyncOwnerSessionState()
        {
            if (_owner == null)
            {
                return;
            }

            int liveSessionToken = _owner.GetWishlistSearchResultSessionToken();
            if (liveSessionToken == _ownerSessionToken)
            {
                return;
            }

            _ownerSessionToken = liveSessionToken;
            _isRegisterConfirmationOpen = false;
            _confirmAcceptFocused = true;
            _statusMessage = _owner.GetStatusMessage();
            if (_owner.HasWishlistSearchResultSession())
            {
                _pageIndex = _owner.GetWishlistSearchResultSessionPageIndex();
                _selectedIndex = -1;
            }
            else
            {
                _results = Array.Empty<AdminShopDialogUI.WishlistSearchResult>();
                _pageIndex = 0;
                _selectedIndex = -1;
            }

            UpdateButtons();
        }

        internal static IReadOnlyList<(string Text, Color Color)> BuildFooterInfoLines(
            string serviceState,
            IReadOnlyList<string> serviceStateDetails)
        {
            List<(string Text, Color Color)> lines = new(FooterInfoLineCapacity);
            if (!string.IsNullOrWhiteSpace(serviceState))
            {
                lines.Add((serviceState, new Color(214, 223, 236)));
            }

            if (serviceStateDetails != null)
            {
                List<string> detailLines = serviceStateDetails
                    .Where(detailLine => !string.IsNullOrWhiteSpace(detailLine))
                    .ToList();
                int remainingCapacity = FooterInfoLineCapacity - lines.Count;
                if (remainingCapacity <= 0 || detailLines.Count <= 0)
                {
                    return lines;
                }

                if (detailLines.Count <= remainingCapacity)
                {
                    foreach (string detailLine in detailLines)
                    {
                        lines.Add((detailLine, new Color(198, 208, 224)));
                    }

                    return lines;
                }

                if (remainingCapacity == 1)
                {
                    lines.Add((detailLines[^1], new Color(198, 208, 224)));
                    return lines;
                }

                for (int i = 0; i < remainingCapacity - 1; i++)
                {
                    lines.Add((detailLines[i], new Color(198, 208, 224)));
                }

                lines.Add((detailLines[^1], new Color(198, 208, 224)));
            }

            return lines;
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

        private void DrawRegisterConfirmation(SpriteBatch sprite, Rectangle bounds)
        {
            Rectangle confirmBounds = GetConfirmationBounds();
            sprite.Draw(_pixelTexture, confirmBounds, new Color(30, 43, 66, 236));

            Rectangle confirmButtonBounds = GetConfirmationButtonBounds(confirm: true);
            Rectangle cancelButtonBounds = GetConfirmationButtonBounds(confirm: false);
            sprite.Draw(_pixelTexture, confirmButtonBounds, _confirmAcceptFocused ? new Color(255, 233, 160) : new Color(214, 223, 236));
            sprite.Draw(_pixelTexture, cancelButtonBounds, !_confirmAcceptFocused ? new Color(255, 233, 160) : new Color(214, 223, 236));

            AdminShopDialogUI.WishlistSearchResult selectedResult = GetSelectedResult();
            DrawRegisterConfirmationPrompt(sprite, confirmBounds, selectedResult);
            sprite.DrawString(_font, "Enter = Yes / Esc = No", new Vector2(confirmBounds.X + 44, confirmBounds.Y + 30), new Color(255, 233, 160));
            sprite.DrawString(_font, "Yes", new Vector2(confirmButtonBounds.X + 25, confirmButtonBounds.Y + 3), new Color(40, 55, 96));
            sprite.DrawString(_font, "No", new Vector2(cancelButtonBounds.X + 28, cancelButtonBounds.Y + 3), new Color(40, 55, 96));
        }

        private void DrawRegisterConfirmationPrompt(
            SpriteBatch sprite,
            Rectangle confirmBounds,
            AdminShopDialogUI.WishlistSearchResult selectedResult)
        {
            string prompt = selectedResult != null
                ? AdminShopWishListClientParityText.GetRegisterConfirmationText(selectedResult.Title)
                : AdminShopWishListClientParityText.GetRegisterConfirmationText(null);
            string[] promptLines = prompt.Replace("\r\n", "\n").Split('\n');
            int maxLines = Math.Min(2, promptLines.Length);
            for (int i = 0; i < maxLines; i++)
            {
                if (string.IsNullOrWhiteSpace(promptLines[i]))
                {
                    continue;
                }

                sprite.DrawString(
                    _font,
                    TrimToWidth(promptLines[i], 206f),
                    new Vector2(confirmBounds.X + 10, confirmBounds.Y + 9 + (i * 12)),
                    Color.White);
            }
        }

        private void HandleConfirmationKeyboardInput(KeyboardState keyboardState)
        {
            if (WasPressed(keyboardState, Keys.Escape))
            {
                CancelSelectedResultConfirmation();
                return;
            }

            if (WasPressed(keyboardState, Keys.Enter))
            {
                if (_confirmAcceptFocused)
                {
                    ConfirmSelectedResult();
                }
                else
                {
                    CancelSelectedResultConfirmation();
                }

                return;
            }

            if (WasPressed(keyboardState, Keys.Left)
                || WasPressed(keyboardState, Keys.Right)
                || WasPressed(keyboardState, Keys.Tab))
            {
                _confirmAcceptFocused = !_confirmAcceptFocused;
            }
        }

        private bool WasPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        private int GetPageIndex()
        {
            return UseOwnerSession ? _owner.GetWishlistSearchResultSessionPageIndex() : _pageIndex;
        }

        private int GetResultCount()
        {
            return UseOwnerSession ? _owner.GetWishlistSearchResultSessionResultCount() : _results.Count;
        }

        private IReadOnlyList<AdminShopDialogUI.WishlistSearchResult> GetVisibleResults()
        {
            return UseOwnerSession ? _owner.GetWishlistSearchResultSessionPage() : _results.Skip(_pageIndex * ResultsPerPage).Take(ResultsPerPage).ToList();
        }

        private AdminShopDialogUI.WishlistSearchResult GetSelectedResult()
        {
            return UseOwnerSession ? _owner.GetWishlistSearchResultSessionSelectedResult() : _selectedIndex >= 0 && _selectedIndex < _results.Count ? _results[_selectedIndex] : null;
        }

        private bool CanMovePage(int delta)
        {
            return UseOwnerSession
                ? _owner.CanMoveWishlistSearchResultSessionPage(delta)
                : delta < 0 ? _pageIndex > 0 : _pageIndex + 1 < GetPageCount();
        }

        private static string BuildPacketSessionLabel(AdminShopDialogUI.WishlistSearchResult result)
        {
            if (result == null)
            {
                return "-1/-1";
            }

            return $"{Math.Max(-1, result.PacketServiceSessionId)}/{Math.Max(-1, result.PacketSearchSessionId)}";
        }

        private static string BuildResultNotRegisterableMessage(AdminShopDialogUI.WishlistSearchResult result)
        {
            if (result == null)
            {
                return "This result row cannot be registered.";
            }

            if (!result.IsPacketAuthored)
            {
                return "This result row cannot be registered.";
            }

            string sessionLabel = BuildPacketSessionLabel(result);
            if (!result.HasLiveCatalogBinding && result.RewardItemId <= 0)
            {
                return $"Packet-authored row session {sessionLabel} has no live NPC catalog binding or registerable item id; BtRegist stays disabled.";
            }

            return $"Packet-authored row session {sessionLabel} is no longer active in the current admin-shop search session.";
        }

        private static string BuildPacketBindingDetail(AdminShopDialogUI.WishlistSearchResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (result.HasLiveCatalogBinding)
            {
                return "Live catalog binding resolved.";
            }

            if (result.RewardItemId > 0 && result.CanRegister && !result.AlreadyWishlisted)
            {
                return "Packet row has no live catalog binding; BtRegist sends the packet item id.";
            }

            if (result.RewardItemId > 0 && result.AlreadyWishlisted)
            {
                return "Packet row has no live catalog binding; item is already in the wish list.";
            }

            return "No live catalog binding or registerable item id; BtRegist stays disabled.";
        }

        private static Color ResolvePacketBindingDetailColor(AdminShopDialogUI.WishlistSearchResult result)
        {
            if (result?.HasLiveCatalogBinding == true || result?.CanRegister == true)
            {
                return new Color(198, 208, 224);
            }

            return new Color(255, 233, 160);
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
