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
    /// <summary>
    /// Dedicated Monster Book owner. The window keeps the existing BookCollection seam
    /// but now renders the client MonsterBook art and card-driven runtime data.
    /// </summary>
    public sealed class BookCollectionWindow : UIWindowBase
    {
        private const int PrevButtonId = 1000;
        private const int NextButtonId = 1001;
        private const int CardColumns = 5;
        private const int CardRows = 5;
        private const int CardsPerPage = CardColumns * CardRows;
        private const int MaxCardCopies = 5;

        private static readonly Point CardSlotOrigin = new(24, 22);
        private static readonly Point InfoPageOrigin = new(278, 36);
        private static readonly Point CardCellPadding = new(5, 14);
        private static readonly Point CardCellStride = new(33, 45);
        private static readonly Point CardCellSize = new(31, 42);
        private static readonly Rectangle SelectedCardIconBounds = new(69, 3, 32, 32);
        private static readonly Rectangle SelectedCardNameBounds = new(10, 38, 151, 16);
        private static readonly Rectangle SelectedCardDetailBounds = new(10, 54, 151, 20);
        private static readonly Point SummaryValueOrigin = new(98, 90);
        private static readonly int SummaryValueRowHeight = 31;
        private static readonly Rectangle PageIndexBounds = new(158, 286, 156, 18);
        private static readonly Rectangle StatusBounds = new(18, 286, 180, 18);
        private static readonly Point PageMarkerAnchor = new(236, 296);
        private const int PageMarkerSpacing = 16;
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

        private Texture2D _cardSlotTexture;
        private Texture2D _infoPageTexture;
        private Texture2D _coveredSlotTexture;
        private Texture2D _selectedSlotTexture;
        private Texture2D _fullMarkTexture;
        private Texture2D _inactivePageMarkerTexture;
        private Texture2D _activePageMarkerTexture;
        private SpriteFont _font;
        private Func<MonsterBookSnapshot> _snapshotProvider;
        private MonsterBookSnapshot _snapshot;
        private MouseState _previousMouseState;
        private int _currentPageIndex;
        private int _selectedSlotIndex;
        private UIObject _prevButton;
        private UIObject _nextButton;

        public BookCollectionWindow(IDXObject frame, Texture2D pixel, GraphicsDevice graphicsDevice, Action closeRequested = null)
            : base(frame)
        {
            _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _closeRequested = closeRequested;
        }

        public override string WindowName => MapSimulatorWindowNames.BookCollection;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetMonsterBookSnapshotProvider(Func<MonsterBookSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
        }

        public void SetMonsterBookArt(
            Texture2D cardSlotTexture,
            Texture2D infoPageTexture,
            Texture2D coveredSlotTexture,
            Texture2D selectedSlotTexture,
            Texture2D fullMarkTexture)
        {
            _cardSlotTexture = cardSlotTexture;
            _infoPageTexture = infoPageTexture;
            _coveredSlotTexture = coveredSlotTexture;
            _selectedSlotTexture = selectedSlotTexture;
            _fullMarkTexture = fullMarkTexture;
        }

        public void SetPageMarkerTextures(Texture2D inactiveMarkerTexture, Texture2D activeMarkerTexture)
        {
            _inactivePageMarkerTexture = inactiveMarkerTexture;
            _activePageMarkerTexture = activeMarkerTexture;
        }

        public void InitializeButtons(UIObject prevButton, UIObject nextButton, UIObject closeButton)
        {
            _prevButton = prevButton;
            _nextButton = nextButton;

            RegisterButton(prevButton, PrevButtonId, MovePreviousPage);
            RegisterButton(nextButton, NextButtonId, MoveNextPage);
            InitializeCloseButton(closeButton);
        }

        public override void Show()
        {
            RefreshSnapshot();
            SelectCardOnCurrentPage();
            UpdateButtonStates();
            base.Show();
        }

        public override void Update(GameTime gameTime)
        {
            RefreshSnapshot();
            ClampPageIndex();
            UpdateButtonStates();
            HandleMouseSelection();
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

            DrawCardSlotPanel(sprite);
            DrawInfoPanel(sprite);
            DrawPageIndex(sprite);
            DrawPageMarkers(sprite);
            DrawStatus(sprite);
        }

        protected override void OnCloseButtonClicked(UIObject sender)
        {
            CloseBook();
            _closeRequested?.Invoke();
        }

        public void CloseBook()
        {
            ResetBookState();
            base.Hide();
        }

        public override void Hide()
        {
            ResetBookState();
            base.Hide();
        }

        private void RegisterButton(UIObject button, int buttonId, Action action)
        {
            if (button == null)
            {
                return;
            }

            AddButton(button);
            _buttonActions[buttonId] = action;
            button.ButtonClickReleased += _ =>
            {
                if (_buttonActions.TryGetValue(buttonId, out Action handler))
                {
                    handler?.Invoke();
                }
            };
        }

        private void MovePreviousPage()
        {
            if (_currentPageIndex <= 0)
            {
                return;
            }

            _currentPageIndex--;
            SelectCardOnCurrentPage();
            UpdateButtonStates();
        }

        private void MoveNextPage()
        {
            int maxPageIndex = Math.Max(0, (_snapshot?.Pages.Count ?? 1) - 1);
            if (_currentPageIndex >= maxPageIndex)
            {
                return;
            }

            _currentPageIndex++;
            SelectCardOnCurrentPage();
            UpdateButtonStates();
        }

        private void RefreshSnapshot()
        {
            _snapshot = _snapshotProvider?.Invoke() ?? new MonsterBookSnapshot();
            ClampPageIndex();
            ClampSelectedSlot();
        }

        private void ClampPageIndex()
        {
            int maxPageIndex = Math.Max(0, (_snapshot?.Pages.Count ?? 1) - 1);
            _currentPageIndex = Math.Clamp(_currentPageIndex, 0, maxPageIndex);
        }

        private void ClampSelectedSlot()
        {
            int visibleCount = GetCurrentPageCards().Count;
            if (visibleCount <= 0)
            {
                _selectedSlotIndex = 0;
                return;
            }

            _selectedSlotIndex = Math.Clamp(_selectedSlotIndex, 0, visibleCount - 1);
        }

        private void SelectCardOnCurrentPage()
        {
            IReadOnlyList<MonsterBookCardSnapshot> cards = GetCurrentPageCards();
            if (cards.Count == 0)
            {
                _selectedSlotIndex = 0;
                return;
            }

            int discoveredIndex = cards.ToList().FindIndex(card => card.IsDiscovered);
            _selectedSlotIndex = discoveredIndex >= 0 ? discoveredIndex : 0;
        }

        private void UpdateButtonStates()
        {
            bool hasPrevious = _currentPageIndex > 0;
            bool hasNext = _currentPageIndex + 1 < (_snapshot?.Pages.Count ?? 1);

            _prevButton?.SetEnabled(hasPrevious);
            _nextButton?.SetEnabled(hasNext);
        }

        private void HandleMouseSelection()
        {
            MouseState currentMouseState = Mouse.GetState();
            bool leftReleased = currentMouseState.LeftButton == ButtonState.Released
                && _previousMouseState.LeftButton == ButtonState.Pressed;

            if (leftReleased)
            {
                Point mousePosition = currentMouseState.Position;
                IReadOnlyList<MonsterBookCardSnapshot> cards = GetCurrentPageCards();
                for (int i = 0; i < cards.Count; i++)
                {
                    if (GetCardBounds(i).Contains(mousePosition))
                    {
                        _selectedSlotIndex = i;
                        break;
                    }
                }
            }

            _previousMouseState = currentMouseState;
        }

        private void DrawCardSlotPanel(SpriteBatch sprite)
        {
            Vector2 panelPosition = new(Position.X + CardSlotOrigin.X, Position.Y + CardSlotOrigin.Y);
            if (_cardSlotTexture != null)
            {
                sprite.Draw(_cardSlotTexture, panelPosition, Color.White);
            }

            IReadOnlyList<MonsterBookCardSnapshot> cards = GetCurrentPageCards();
            for (int i = 0; i < CardsPerPage; i++)
            {
                Rectangle slotBounds = GetCardBounds(i);
                MonsterBookCardSnapshot card = i < cards.Count ? cards[i] : null;
                DrawCardSlot(sprite, slotBounds, card, i == _selectedSlotIndex);
            }
        }

        private void DrawCardSlot(SpriteBatch sprite, Rectangle bounds, MonsterBookCardSnapshot card, bool selected)
        {
            if (card?.IsDiscovered == true)
            {
                Texture2D icon = ResolveCardIcon(card.CardItemId);
                if (icon != null)
                {
                    DrawCardIcon(sprite, icon, bounds, card.IsCompleted ? Color.White : new Color(255, 255, 255, 230));
                }

                Texture2D borderTexture = selected ? _selectedSlotTexture : _coveredSlotTexture;
                if (borderTexture != null)
                {
                    sprite.Draw(borderTexture, new Vector2(bounds.X, bounds.Y), Color.White);
                }

                DrawCenteredString(sprite, $"{card.OwnedCopies}/{card.MaxCopies}", new Rectangle(bounds.X - 1, bounds.Bottom - 13, bounds.Width + 2, 10), ValueColor, 0.42f);
                if (card.IsCompleted && _fullMarkTexture != null)
                {
                    sprite.Draw(_fullMarkTexture, new Vector2(bounds.Right - _fullMarkTexture.Width - 2, bounds.Y + 1), Color.White);
                }
            }
            else
            {
                sprite.Draw(_pixel, bounds, new Color(25, 25, 25, 52));
                if (selected && _selectedSlotTexture != null)
                {
                    sprite.Draw(_selectedSlotTexture, new Vector2(bounds.X, bounds.Y), HiddenTint);
                }

                DrawCenteredString(sprite, "?", bounds, MutedColor, 0.62f);
            }
        }

        private void DrawCardIcon(SpriteBatch sprite, Texture2D icon, Rectangle bounds, Color tint)
        {
            float scale = Math.Min(24f / Math.Max(1, icon.Width), 24f / Math.Max(1, icon.Height));
            int drawWidth = Math.Max(1, (int)Math.Round(icon.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(icon.Height * scale));
            Rectangle destination = new(
                bounds.X + ((bounds.Width - drawWidth) / 2),
                bounds.Y + 4 + ((24 - drawHeight) / 2),
                drawWidth,
                drawHeight);
            sprite.Draw(icon, destination, tint);
        }

        private void DrawInfoPanel(SpriteBatch sprite)
        {
            Vector2 panelPosition = new(Position.X + InfoPageOrigin.X, Position.Y + InfoPageOrigin.Y);
            if (_infoPageTexture != null)
            {
                sprite.Draw(_infoPageTexture, panelPosition, Color.White);
            }

            MonsterBookCardSnapshot selectedCard = GetSelectedCard();
            if (selectedCard != null)
            {
                Texture2D icon = ResolveCardIcon(selectedCard.CardItemId);
                if (icon != null)
                {
                    Rectangle iconBounds = OffsetBounds(SelectedCardIconBounds, InfoPageOrigin);
                    sprite.Draw(icon, iconBounds, Color.White);
                }
            }

            string selectedName = selectedCard?.IsDiscovered == true ? selectedCard.Name : "Unknown Card";
            DrawCenteredString(sprite, selectedName, OffsetBounds(SelectedCardNameBounds, InfoPageOrigin), TitleColor, 0.6f);

            string detailText = selectedCard == null
                ? "No card selected."
                : selectedCard.IsDiscovered
                    ? BuildDetailText(selectedCard)
                    : "Collect this card to reveal its detail entry.";
            DrawCenteredString(sprite, detailText, OffsetBounds(SelectedCardDetailBounds, InfoPageOrigin), MutedColor, 0.46f);

            DrawSummaryValue(sprite, 0, $"{Math.Max(1, _currentPageIndex + 1)}/{Math.Max(1, _snapshot?.Pages.Count ?? 1)}");
            DrawSummaryValue(sprite, 1, $"{_snapshot?.OwnedCardTypes ?? 0}");
            DrawSummaryValue(sprite, 2, $"{_snapshot?.OwnedBossCardTypes ?? 0}");
            DrawSummaryValue(sprite, 3, $"{_snapshot?.OwnedNormalCardTypes ?? 0}");
            DrawSummaryValue(sprite, 4, $"{_snapshot?.CompletedCardTypes ?? 0}");
        }

        private void DrawSummaryValue(SpriteBatch sprite, int row, string text)
        {
            Vector2 position = new(
                Position.X + InfoPageOrigin.X + SummaryValueOrigin.X,
                Position.Y + InfoPageOrigin.Y + SummaryValueOrigin.Y + (row * SummaryValueRowHeight));
            sprite.DrawString(_font, string.IsNullOrWhiteSpace(text) ? "-" : text, position, AccentColor, 0f, Vector2.Zero, 0.56f, SpriteEffects.None, 0f);
        }

        private void DrawPageIndex(SpriteBatch sprite)
        {
            Rectangle bounds = new(
                Position.X + PageIndexBounds.X,
                Position.Y + PageIndexBounds.Y,
                PageIndexBounds.Width,
                PageIndexBounds.Height);

            DrawCenteredString(
                sprite,
                $"{Math.Max(1, _currentPageIndex + 1)}/{Math.Max(1, _snapshot?.Pages.Count ?? 1)}",
                bounds,
                AccentColor,
                0.62f);
        }

        private void DrawPageMarkers(SpriteBatch sprite)
        {
            Texture2D activeMarker = _activePageMarkerTexture ?? _fullMarkTexture;
            Texture2D inactiveMarker = _inactivePageMarkerTexture ?? _coveredSlotTexture;
            if (activeMarker == null && inactiveMarker == null)
            {
                return;
            }

            bool hasCurrentPage = (_snapshot?.Pages?.Count ?? 0) > 0;
            bool hasNextPage = _currentPageIndex + 1 < (_snapshot?.Pages?.Count ?? 0);

            DrawPageMarker(
                sprite,
                Position.X + PageMarkerAnchor.X - PageMarkerSpacing,
                Position.Y + PageMarkerAnchor.Y,
                hasCurrentPage,
                inactiveMarker,
                activeMarker);
            DrawPageMarker(
                sprite,
                Position.X + PageMarkerAnchor.X + PageMarkerSpacing,
                Position.Y + PageMarkerAnchor.Y,
                hasNextPage,
                inactiveMarker,
                activeMarker);
        }

        private void DrawStatus(SpriteBatch sprite)
        {
            string text = _snapshot?.StatusText;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            DrawTrimmedString(
                sprite,
                text,
                new Vector2(Position.X + StatusBounds.X, Position.Y + StatusBounds.Y),
                MutedColor,
                0.4f,
                StatusBounds.Width);
        }

        private IReadOnlyList<MonsterBookCardSnapshot> GetCurrentPageCards()
        {
            if (_snapshot?.Pages == null || _snapshot.Pages.Count == 0)
            {
                return Array.Empty<MonsterBookCardSnapshot>();
            }

            return _snapshot.Pages[_currentPageIndex].Cards ?? Array.Empty<MonsterBookCardSnapshot>();
        }

        private MonsterBookCardSnapshot GetSelectedCard()
        {
            IReadOnlyList<MonsterBookCardSnapshot> cards = GetCurrentPageCards();
            return _selectedSlotIndex >= 0 && _selectedSlotIndex < cards.Count
                ? cards[_selectedSlotIndex]
                : null;
        }

        private Rectangle GetCardBounds(int slotIndex)
        {
            int column = slotIndex % CardColumns;
            int row = slotIndex / CardColumns;
            return new Rectangle(
                Position.X + CardSlotOrigin.X + CardCellPadding.X + (column * CardCellStride.X),
                Position.Y + CardSlotOrigin.Y + CardCellPadding.Y + (row * CardCellStride.Y),
                CardCellSize.X,
                CardCellSize.Y);
        }

        private Rectangle OffsetBounds(Rectangle bounds, Point offset)
        {
            return new Rectangle(Position.X + offset.X + bounds.X, Position.Y + offset.Y + bounds.Y, bounds.Width, bounds.Height);
        }

        private string BuildDetailText(MonsterBookCardSnapshot card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Lv {0}  HP {1}  EXP {2}  {3}/{4}",
                Math.Max(0, card.Level),
                Math.Max(0, card.MaxHp),
                Math.Max(0, card.Exp),
                card.OwnedCopies,
                card.MaxCopies);
        }

        private Texture2D ResolveCardIcon(int cardItemId)
        {
            if (cardItemId <= 0)
            {
                return null;
            }

            if (_cardIconCache.TryGetValue(cardItemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            Texture2D texture = LoadCardIconTexture(cardItemId);
            if (texture != null)
            {
                _cardIconCache[cardItemId] = texture;
            }

            return texture;
        }

        private Texture2D LoadCardIconTexture(int cardItemId)
        {
            try
            {
                WzImage itemImage = global::HaCreator.Program.FindImage("Item", "Consume/0238.img");
                if (itemImage == null)
                {
                    return null;
                }

                if (!itemImage.Parsed)
                {
                    itemImage.ParseImage();
                }

                WzSubProperty cardProperty = itemImage[cardItemId.ToString(CultureInfo.InvariantCulture)] as WzSubProperty;
                WzCanvasProperty iconProperty = cardProperty?["info"]?["icon"] as WzCanvasProperty;
                return iconProperty?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_graphicsDevice);
            }
            catch
            {
                return null;
            }
        }

        private void DrawCenteredString(SpriteBatch sprite, string text, Rectangle bounds, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = _font.MeasureString(text) * scale;
            Vector2 position = new(
                bounds.X + Math.Max(0f, (bounds.Width - size.X) / 2f),
                bounds.Y + Math.Max(0f, (bounds.Height - size.Y) / 2f));
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawTrimmedString(SpriteBatch sprite, string text, Vector2 position, Color color, float scale, float maxWidth)
        {
            string trimmed = TrimToWidth(text, maxWidth, scale);
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                sprite.DrawString(_font, trimmed, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private string TrimToWidth(string text, float maxWidth, float scale)
        {
            string safeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(safeText) || Measure(safeText, scale) <= maxWidth)
            {
                return safeText;
            }

            const string ellipsis = "...";
            for (int length = safeText.Length - 1; length > 0; length--)
            {
                string candidate = safeText.Substring(0, length) + ellipsis;
                if (Measure(candidate, scale) <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private float Measure(string text, float scale)
        {
            return _font.MeasureString(text ?? string.Empty).X * scale;
        }

        private void DrawPageMarker(SpriteBatch sprite, int centerX, int centerY, bool active, Texture2D inactiveMarker, Texture2D activeMarker)
        {
            Texture2D marker = active ? activeMarker ?? inactiveMarker : inactiveMarker ?? activeMarker;
            if (marker == null)
            {
                return;
            }

            Rectangle destination = new(
                centerX - (marker.Width / 2),
                centerY - (marker.Height / 2),
                marker.Width,
                marker.Height);
            sprite.Draw(marker, destination, Color.White);
        }

        private void ResetBookState()
        {
            _currentPageIndex = 0;
            _selectedSlotIndex = 0;
            _previousMouseState = default;
        }
    }
}
