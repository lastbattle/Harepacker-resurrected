using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public enum AdminShopServiceMode
    {
        CashShop,
        Mts
    }

    /// <summary>
    /// WZ-backed cash-service dialog owner that keeps Cash Shop and MTS access in a
    /// dedicated surface instead of a generic placeholder.
    /// </summary>
    public sealed class AdminShopDialogUI : UIWindowBase
    {
        private sealed class AdminShopCategory
        {
            public AdminShopCategory(string title, string detail)
            {
                Title = title ?? string.Empty;
                Detail = detail ?? string.Empty;
            }

            public string Title { get; }
            public string Detail { get; }
        }

        private static readonly AdminShopCategory[] CashShopCategories =
        {
            new AdminShopCategory("Featured", "Preview the featured NX rotation and highlighted avatar bundles."),
            new AdminShopCategory("Style", "Browse outfits, hair coupons, and cosmetic utility items."),
            new AdminShopCategory("Pets", "Review pet bundles, pet equip, and support consumables."),
            new AdminShopCategory("Convenience", "Check storage, slot, and quality-of-life cash services."),
            new AdminShopCategory("Events", "Inspect limited promotions and event-only cash services.")
        };

        private static readonly AdminShopCategory[] MtsCategories =
        {
            new AdminShopCategory("Search", "Scan posted listings before entering the full trading surface."),
            new AdminShopCategory("Equipment", "Filter equipment listings and compare recent sale routes."),
            new AdminShopCategory("Use", "Browse consumable and utility listings in the preview flow."),
            new AdminShopCategory("Etc", "Inspect ETC and crafting-material listing categories."),
            new AdminShopCategory("My Page", "Review sale status and your personal MTS actions.")
        };

        private const int LeftPaneX = 17;
        private const int RightPaneX = 242;
        private const int PaneTopY = 101;
        private const int PaneRowHeight = 35;
        private const int RowTextX = 14;
        private const int RowTextY = 9;
        private const int HeaderX = 18;
        private const int HeaderY = 72;
        private const int DetailX = 18;
        private const int DetailY = 278;
        private const int MoneyIconX = 335;
        private const int MoneyIconY = 299;
        private const int MoneyTextX = 353;
        private const int MoneyTextY = 296;

        private readonly string _windowName;
        private readonly AdminShopServiceMode _defaultMode;
        private readonly IDXObject _frameOverlay;
        private readonly Point _frameOverlayOffset;
        private readonly IDXObject _contentOverlay;
        private readonly Point _contentOverlayOffset;
        private readonly Texture2D _selectionTexture;
        private readonly Texture2D _mesoTexture;
        private readonly UIObject _buyButton;
        private readonly UIObject _sellButton;
        private readonly UIObject _exitButton;

        private SpriteFont _font;
        private AdminShopServiceMode _currentMode;
        private int _cashShopCategoryIndex;
        private int _mtsCategoryIndex;
        private string _footerMessage;

        public AdminShopDialogUI(
            IDXObject frame,
            string windowName,
            AdminShopServiceMode defaultMode,
            IDXObject frameOverlay,
            Point frameOverlayOffset,
            IDXObject contentOverlay,
            Point contentOverlayOffset,
            Texture2D selectionTexture,
            Texture2D mesoTexture,
            UIObject buyButton,
            UIObject sellButton,
            UIObject exitButton)
            : base(frame)
        {
            _windowName = windowName ?? throw new ArgumentNullException(nameof(windowName));
            _defaultMode = defaultMode;
            _currentMode = defaultMode;
            _frameOverlay = frameOverlay;
            _frameOverlayOffset = frameOverlayOffset;
            _contentOverlay = contentOverlay;
            _contentOverlayOffset = contentOverlayOffset;
            _selectionTexture = selectionTexture;
            _mesoTexture = mesoTexture;
            _buyButton = buyButton;
            _sellButton = sellButton;
            _exitButton = exitButton;
            _footerMessage = BuildFooterMessage(_defaultMode, GetSelectedCategory(_defaultMode));

            if (_buyButton != null)
            {
                AddButton(_buyButton);
                _buyButton.ButtonClickReleased += OnBuyButtonClicked;
            }

            if (_sellButton != null)
            {
                AddButton(_sellButton);
                _sellButton.ButtonClickReleased += OnSellButtonClicked;
            }

            if (_exitButton != null)
            {
                AddButton(_exitButton);
                _exitButton.ButtonClickReleased += _ => Hide();
            }
        }

        public override string WindowName => _windowName;

        public long Money { get; set; }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
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
            int windowX = Position.X;
            int windowY = Position.Y;

            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _frameOverlay, _frameOverlayOffset, windowX, windowY, drawReflectionInfo);
            DrawLayer(sprite, skeletonMeshRenderer, gameTime, _contentOverlay, _contentOverlayOffset, windowX, windowY, drawReflectionInfo);

            if (_font == null)
            {
                return;
            }

            AdminShopCategory[] activeCategories = GetActiveCategories();
            DrawHeader(sprite, windowX, windowY);
            DrawPane(sprite, windowX, windowY, LeftPaneX, activeCategories, GetSelectedIndex(), true);
            DrawPane(sprite, windowX, windowY, RightPaneX, GetInactiveCategories(), -1, false);
            DrawFooter(sprite, windowX, windowY, activeCategories[GetSelectedIndex()]);
            DrawMoney(sprite, windowX, windowY);
        }

        private void DrawLayer(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            IDXObject layer,
            Point offset,
            int windowX,
            int windowY,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            layer?.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                windowX + offset.X,
                windowY + offset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawHeader(SpriteBatch sprite, int windowX, int windowY)
        {
            string primaryHeader = _currentMode == AdminShopServiceMode.CashShop ? "Cash Shop request" : "MTS request";
            string secondaryHeader = _currentMode == AdminShopServiceMode.CashShop
                ? "BtBuy cycles routes, BtSell switches to MTS."
                : "BtSell cycles routes, BtBuy switches to Cash Shop.";

            sprite.DrawString(_font, primaryHeader, new Vector2(windowX + HeaderX, windowY + HeaderY), Color.White);
            sprite.DrawString(_font, secondaryHeader, new Vector2(windowX + HeaderX, windowY + HeaderY + 18), new Color(215, 215, 215));
        }

        private void DrawPane(SpriteBatch sprite, int windowX, int windowY, int paneX, IReadOnlyList<AdminShopCategory> categories, int selectedIndex, bool activePane)
        {
            for (int i = 0; i < categories.Count; i++)
            {
                int rowX = windowX + paneX;
                int rowY = windowY + PaneTopY + (i * PaneRowHeight);

                if (_selectionTexture != null && i == selectedIndex)
                {
                    sprite.Draw(_selectionTexture, new Vector2(rowX, rowY), Color.White);
                }

                Color titleColor = i == selectedIndex ? Color.Black : Color.White;
                Color detailColor = i == selectedIndex
                    ? new Color(36, 36, 36)
                    : (activePane ? new Color(225, 225, 225) : new Color(185, 185, 185));

                sprite.DrawString(_font, categories[i].Title, new Vector2(rowX + RowTextX, rowY + RowTextY), titleColor);
                sprite.DrawString(_font, Truncate(categories[i].Detail, 26), new Vector2(rowX + RowTextX, rowY + RowTextY + 15), detailColor);
            }
        }

        private void DrawFooter(SpriteBatch sprite, int windowX, int windowY, AdminShopCategory selectedCategory)
        {
            sprite.DrawString(_font, selectedCategory.Title, new Vector2(windowX + DetailX, windowY + DetailY), Color.White);

            float detailY = windowY + DetailY + 18;
            foreach (string line in WrapText(selectedCategory.Detail, 400f))
            {
                sprite.DrawString(_font, line, new Vector2(windowX + DetailX, detailY), new Color(218, 218, 218));
                detailY += 16f;
            }

            if (!string.IsNullOrWhiteSpace(_footerMessage))
            {
                sprite.DrawString(_font, _footerMessage, new Vector2(windowX + DetailX, windowY + DetailY + 34), new Color(255, 221, 143));
            }
        }

        private void DrawMoney(SpriteBatch sprite, int windowX, int windowY)
        {
            if (_mesoTexture != null)
            {
                sprite.Draw(_mesoTexture, new Vector2(windowX + MoneyIconX, windowY + MoneyIconY), Color.White);
            }

            sprite.DrawString(_font, Money.ToString("N0"), new Vector2(windowX + MoneyTextX, windowY + MoneyTextY), Color.White);
        }

        private void OnBuyButtonClicked(UIObject sender)
        {
            if (_currentMode == AdminShopServiceMode.CashShop)
            {
                _cashShopCategoryIndex = (_cashShopCategoryIndex + 1) % CashShopCategories.Length;
            }
            else
            {
                _currentMode = AdminShopServiceMode.CashShop;
            }

            _footerMessage = BuildFooterMessage(_currentMode, GetSelectedCategory(_currentMode));
        }

        private void OnSellButtonClicked(UIObject sender)
        {
            if (_currentMode == AdminShopServiceMode.Mts)
            {
                _mtsCategoryIndex = (_mtsCategoryIndex + 1) % MtsCategories.Length;
            }
            else
            {
                _currentMode = AdminShopServiceMode.Mts;
            }

            _footerMessage = BuildFooterMessage(_currentMode, GetSelectedCategory(_currentMode));
        }

        private AdminShopCategory[] GetActiveCategories()
        {
            return _currentMode == AdminShopServiceMode.CashShop ? CashShopCategories : MtsCategories;
        }

        private AdminShopCategory[] GetInactiveCategories()
        {
            return _currentMode == AdminShopServiceMode.CashShop ? MtsCategories : CashShopCategories;
        }

        private int GetSelectedIndex()
        {
            return _currentMode == AdminShopServiceMode.CashShop ? _cashShopCategoryIndex : _mtsCategoryIndex;
        }

        private AdminShopCategory GetSelectedCategory(AdminShopServiceMode mode)
        {
            return mode == AdminShopServiceMode.CashShop
                ? CashShopCategories[_cashShopCategoryIndex]
                : MtsCategories[_mtsCategoryIndex];
        }

        private static string BuildFooterMessage(AdminShopServiceMode mode, AdminShopCategory category)
        {
            string modeLabel = mode == AdminShopServiceMode.CashShop ? "Cash Shop" : "MTS";
            return $"{modeLabel} preview armed for {category.Title}.";
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
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

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }
    }
}
