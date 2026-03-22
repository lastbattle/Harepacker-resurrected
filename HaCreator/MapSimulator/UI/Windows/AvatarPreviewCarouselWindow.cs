using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    public sealed class AvatarPreviewCarouselWindow : UIWindowBase
    {
        private const int EntriesPerPage = 3;
        private const int CardWidth = 183;
        private const int CardHeight = 151;
        private const int CardStartX = 18;
        private const int CardStartY = 46;
        private const int CardGap = 14;
        private const int AvatarFeetOffsetY = 106;
        private const int DoubleClickThresholdMs = 400;

        private readonly List<UIObject> _cardButtons = new();
        private readonly List<LoginCharacterRosterEntry> _entries = new();
        private readonly UIObject _prevPageButton;
        private readonly UIObject _nextPageButton;
        private readonly Dictionary<LoginCharacterRosterEntry, CharacterAssembler> _previewAssemblers = new();
        private readonly Texture2D _normalCardTexture;
        private readonly Texture2D _selectedCardTexture;

        private SpriteFont _font;
        private int _selectedIndex = -1;
        private int _pageIndex;
        private int _lastActivatedEntryIndex = -1;
        private int _lastActivationTick = int.MinValue;

        public AvatarPreviewCarouselWindow(
            IDXObject frame,
            Texture2D normalCardTexture,
            Texture2D selectedCardTexture,
            IEnumerable<UIObject> cardButtons,
            UIObject prevPageButton,
            UIObject nextPageButton)
            : base(frame)
        {
            _normalCardTexture = normalCardTexture;
            _selectedCardTexture = selectedCardTexture ?? normalCardTexture;
            _prevPageButton = prevPageButton;
            _nextPageButton = nextPageButton;

            int slotIndex = 0;
            foreach (UIObject cardButton in cardButtons ?? Array.Empty<UIObject>())
            {
                if (cardButton == null)
                {
                    continue;
                }

                int capturedIndex = slotIndex;
                cardButton.ButtonClickReleased += _ => ActivateCard(capturedIndex);
                AddButton(cardButton);
                _cardButtons.Add(cardButton);
                slotIndex++;
            }

            if (_prevPageButton != null)
            {
                _prevPageButton.ButtonClickReleased += _ => SelectPreviousPage();
                AddButton(_prevPageButton);
            }

            if (_nextPageButton != null)
            {
                _nextPageButton.ButtonClickReleased += _ => SelectNextPage();
                AddButton(_nextPageButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.AvatarPreviewCarousel;

        public event Action<int> CharacterSelected;
        public event Action EnterRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override bool SupportsDragging => false;

        public void SetRoster(IReadOnlyList<LoginCharacterRosterEntry> entries, int selectedIndex)
        {
            _entries.Clear();
            _previewAssemblers.Clear();
            if (entries != null)
            {
                _entries.AddRange(entries);
                foreach (LoginCharacterRosterEntry entry in _entries)
                {
                    if (entry?.Build == null)
                    {
                        continue;
                    }

                    _previewAssemblers[entry] = new CharacterAssembler(entry.Build);
                }
            }

            _selectedIndex = selectedIndex;
            _pageIndex = GetPageIndexForSelection(selectedIndex);

            for (int i = 0; i < _cardButtons.Count; i++)
            {
                int entryIndex = GetEntryIndexForVisibleSlot(i);
                bool visible = entryIndex >= 0 && entryIndex < _entries.Count;
                _cardButtons[i].SetVisible(visible);
                _cardButtons[i].SetEnabled(visible);
                _cardButtons[i].SetButtonState(entryIndex == _selectedIndex ? UIObjectState.Pressed : UIObjectState.Normal);
            }

            bool hasMultiplePages = GetPageCount() > 1;
            _prevPageButton?.SetVisible(hasMultiplePages);
            _prevPageButton?.SetEnabled(hasMultiplePages);
            _nextPageButton?.SetVisible(hasMultiplePages);
            _nextPageButton?.SetEnabled(hasMultiplePages);
        }

        protected override void DrawOverlay(
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
            DrawPreviewCards(sprite, skeletonMeshRenderer, TickCount);
            if (_font != null)
            {
                DrawPageSummary(sprite);
            }
        }

        private void DrawPreviewCards(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, int tickCount)
        {
            if (_font == null)
            {
                return;
            }

            for (int slotIndex = 0; slotIndex < _cardButtons.Count; slotIndex++)
            {
                int entryIndex = GetEntryIndexForVisibleSlot(slotIndex);
                if (entryIndex < 0 || entryIndex >= _entries.Count)
                {
                    continue;
                }

                LoginCharacterRosterEntry entry = _entries[entryIndex];
                CharacterBuild build = entry.Build;
                if (build == null)
                {
                    continue;
                }

                Rectangle cardBounds = GetCardBounds(slotIndex);
                bool isSelected = entryIndex == _selectedIndex;
                Texture2D cardTexture = isSelected ? _selectedCardTexture : _normalCardTexture;
                if (cardTexture != null)
                {
                    sprite.Draw(cardTexture, new Vector2(cardBounds.X, cardBounds.Y), Color.White);
                }

                if (_previewAssemblers.TryGetValue(entry, out CharacterAssembler assembler))
                {
                    AssembledFrame previewFrame = assembler.GetFrameAtTime("stand1", tickCount);
                    if (previewFrame != null)
                    {
                        previewFrame.Draw(sprite, skeletonMeshRenderer, cardBounds.Center.X, cardBounds.Y + AvatarFeetOffsetY, false, Color.White);
                    }
                }

                Color headerColor = isSelected ? new Color(103, 53, 40) : new Color(89, 60, 42);
                Color detailColor = isSelected ? new Color(122, 72, 50) : new Color(108, 79, 63);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    build.Name,
                    new Vector2(cardBounds.X + 10, cardBounds.Bottom - 48),
                    headerColor);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    $"Lv. {build.Level}",
                    new Vector2(cardBounds.X + 10, cardBounds.Bottom - 32),
                    detailColor);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    build.JobName,
                    new Vector2(cardBounds.X + 10, cardBounds.Bottom - 18),
                    detailColor);
                SelectorWindowDrawing.DrawShadowedText(
                    sprite,
                    _font,
                    build.GuildDisplayText,
                    new Vector2(cardBounds.X + 10, cardBounds.Bottom - 4),
                    detailColor);
            }
        }

        private void DrawPageSummary(SpriteBatch sprite)
        {
            if (_font == null)
            {
                return;
            }

            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            string pageText = $"{_pageIndex + 1}/{pageCount}";
            Vector2 size = _font.MeasureString(pageText);
            float x = Position.X + 311f - (size.X / 2f);
            float y = Position.Y + 212f;
            SelectorWindowDrawing.DrawShadowedText(sprite, _font, pageText, new Vector2(x, y), new Color(233, 226, 205));
        }

        private void ActivateCard(int visibleSlotIndex)
        {
            int entryIndex = GetEntryIndexForVisibleSlot(visibleSlotIndex);
            if (entryIndex < 0 || entryIndex >= _entries.Count)
            {
                return;
            }

            int currentTick = Environment.TickCount;
            bool isDoubleClick = entryIndex == _lastActivatedEntryIndex &&
                                 unchecked(currentTick - _lastActivationTick) <= DoubleClickThresholdMs;
            _lastActivatedEntryIndex = entryIndex;
            _lastActivationTick = currentTick;

            CharacterSelected?.Invoke(entryIndex);
            if (isDoubleClick)
            {
                EnterRequested?.Invoke();
            }
        }

        private void SelectPreviousPage()
        {
            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            int targetPage = (_pageIndex - 1 + pageCount) % pageCount;
            int pageStartIndex = targetPage * EntriesPerPage;
            int pageEndIndex = Math.Min(_entries.Count - 1, pageStartIndex + EntriesPerPage - 1);
            if (pageEndIndex >= 0)
            {
                CharacterSelected?.Invoke(pageEndIndex);
            }
        }

        private void SelectNextPage()
        {
            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            int targetPage = (_pageIndex + 1) % pageCount;
            int pageStartIndex = targetPage * EntriesPerPage;
            if (pageStartIndex < _entries.Count)
            {
                CharacterSelected?.Invoke(pageStartIndex);
            }
        }

        private int GetEntryIndexForVisibleSlot(int visibleSlotIndex)
        {
            return (_pageIndex * EntriesPerPage) + visibleSlotIndex;
        }

        private int GetPageCount()
        {
            return _entries.Count == 0 ? 0 : ((_entries.Count - 1) / EntriesPerPage) + 1;
        }

        private int GetPageIndexForSelection(int selectedIndex)
        {
            if (selectedIndex < 0 || _entries.Count == 0)
            {
                return 0;
            }

            return Math.Clamp(selectedIndex / EntriesPerPage, 0, Math.Max(0, GetPageCount() - 1));
        }

        private Rectangle GetCardBounds(int visibleSlotIndex)
        {
            int x = Position.X + CardStartX + (visibleSlotIndex * (CardWidth + CardGap));
            int y = Position.Y + CardStartY;
            return new Rectangle(x, y, CardWidth, CardHeight);
        }
    }
}
