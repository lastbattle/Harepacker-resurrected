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
        private const int NameTagBaselineOffsetY = 120;
        private const float NameTagTextScale = 0.5f;
        private const int DoubleClickThresholdMs = 400;

        private readonly List<UIObject> _cardButtons = new();
        private readonly List<LoginCharacterRosterEntry> _entries = new();
        private readonly UIObject _prevPageButton;
        private readonly UIObject _nextPageButton;
        private readonly Dictionary<LoginCharacterRosterEntry, CharacterAssembler> _previewAssemblers = new();
        private readonly Texture2D _normalCardTexture;
        private readonly Texture2D _selectedCardTexture;
        private readonly PreviewNameTagStyle _normalNameTagStyle;
        private readonly PreviewNameTagStyle _selectedNameTagStyle;
        private readonly IReadOnlyDictionary<LoginJobDecorationStyle, PreviewCanvasFrame> _jobDecorations;

        private SpriteFont _font;
        private int _selectedIndex = -1;
        private int _pageIndex;
        private int _lastActivatedEntryIndex = -1;
        private int _lastActivationTick = int.MinValue;

        public AvatarPreviewCarouselWindow(
            IDXObject frame,
            Texture2D normalCardTexture,
            Texture2D selectedCardTexture,
            PreviewNameTagStyle normalNameTagStyle,
            PreviewNameTagStyle selectedNameTagStyle,
            IReadOnlyDictionary<LoginJobDecorationStyle, PreviewCanvasFrame> jobDecorations,
            IEnumerable<UIObject> cardButtons,
            UIObject prevPageButton,
            UIObject nextPageButton)
            : base(frame)
        {
            _normalCardTexture = normalCardTexture;
            _selectedCardTexture = selectedCardTexture ?? normalCardTexture;
            _normalNameTagStyle = normalNameTagStyle;
            _selectedNameTagStyle = selectedNameTagStyle;
            _jobDecorations = jobDecorations ?? new Dictionary<LoginJobDecorationStyle, PreviewCanvasFrame>();
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

                int avatarAnchorX = cardBounds.Center.X;
                int avatarAnchorY = cardBounds.Y + AvatarFeetOffsetY;
                DrawJobDecoration(sprite, build, avatarAnchorX, avatarAnchorY);

                if (_previewAssemblers.TryGetValue(entry, out CharacterAssembler assembler))
                {
                    AssembledFrame previewFrame = assembler.GetFrameAtTime("stand1", tickCount);
                    if (previewFrame != null)
                    {
                        previewFrame.Draw(sprite, skeletonMeshRenderer, avatarAnchorX, avatarAnchorY, false, Color.White);
                    }
                }

                if (_font != null)
                {
                    DrawNameTag(sprite, build.Name, isSelected, cardBounds.X + 91, cardBounds.Y + NameTagBaselineOffsetY);
                }
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

        private void DrawJobDecoration(SpriteBatch sprite, CharacterBuild build, int anchorX, int anchorY)
        {
            if (build == null || !_jobDecorations.TryGetValue(ResolveDecorationStyle(build.Job), out PreviewCanvasFrame frame))
            {
                return;
            }

            if (frame.Texture == null)
            {
                return;
            }

            sprite.Draw(
                frame.Texture,
                new Vector2(anchorX - frame.Origin.X, anchorY - frame.Origin.Y),
                Color.White);
        }

        private void DrawNameTag(SpriteBatch sprite, string name, bool isSelected, int centerX, int y)
        {
            if (_font == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            PreviewNameTagStyle style = isSelected ? _selectedNameTagStyle : _normalNameTagStyle;
            if (!style.IsReady)
            {
                Color fallbackColor = isSelected ? Color.White : new Color(153, 153, 153);
                Vector2 fallbackSize = _font.MeasureString(name) * NameTagTextScale;
                sprite.DrawString(
                    _font,
                    name,
                    new Vector2(centerX - (fallbackSize.X * 0.5f), y + 4),
                    fallbackColor,
                    0f,
                    Vector2.Zero,
                    NameTagTextScale,
                    SpriteEffects.None,
                    0f);
                return;
            }

            Vector2 scaledTextSize = _font.MeasureString(name) * NameTagTextScale;
            int totalWidth = Math.Max(58, (int)Math.Ceiling(scaledTextSize.X) + 18);
            int leftWidth = style.Left.Width;
            int middleWidth = style.Middle.Width;
            int rightWidth = style.Right.Width;
            int stretchWidth = Math.Max(0, totalWidth - leftWidth - rightWidth);
            int middleX = centerX - (totalWidth / 2) + leftWidth;
            int leftX = middleX - leftWidth;
            int rightX = middleX + stretchWidth;

            sprite.Draw(style.Left, new Vector2(leftX, y), Color.White);
            for (int offsetX = 0; offsetX < stretchWidth; offsetX += middleWidth)
            {
                int tileWidth = Math.Min(middleWidth, stretchWidth - offsetX);
                sprite.Draw(
                    style.Middle,
                    new Rectangle(middleX + offsetX, y, tileWidth, style.Middle.Height),
                    new Rectangle(0, 0, tileWidth, style.Middle.Height),
                    Color.White);
            }

            sprite.Draw(style.Right, new Vector2(rightX, y), Color.White);

            sprite.DrawString(
                _font,
                name,
                new Vector2(centerX - (scaledTextSize.X * 0.5f) - 1f, y + 2f),
                style.TextColor,
                0f,
                Vector2.Zero,
                NameTagTextScale,
                SpriteEffects.None,
                0f);
        }

        private static LoginJobDecorationStyle ResolveDecorationStyle(int jobId)
        {
            if (jobId / 1000 == 1)
            {
                return LoginJobDecorationStyle.Knight;
            }

            if (jobId == 2000 || jobId / 100 == 21)
            {
                return LoginJobDecorationStyle.Aran;
            }

            if (IsEvanJob(jobId))
            {
                return LoginJobDecorationStyle.Evan;
            }

            if (jobId / 1000 == 3)
            {
                return LoginJobDecorationStyle.Resistance;
            }

            return LoginJobDecorationStyle.Adventure;
        }

        private static bool IsEvanJob(int jobId)
        {
            return jobId == 2001 || (jobId >= 2200 && jobId <= 2218);
        }

        public readonly struct PreviewCanvasFrame
        {
            public PreviewCanvasFrame(Texture2D texture, Point origin)
            {
                Texture = texture;
                Origin = origin;
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
        }

        public readonly struct PreviewNameTagStyle
        {
            public PreviewNameTagStyle(Texture2D left, Texture2D middle, Texture2D right, Color textColor)
            {
                Left = left;
                Middle = middle;
                Right = right;
                TextColor = textColor;
            }

            public Texture2D Left { get; }
            public Texture2D Middle { get; }
            public Texture2D Right { get; }
            public Color TextColor { get; }
            public bool IsReady => Left != null && Middle != null && Right != null;
        }

        public enum LoginJobDecorationStyle
        {
            Adventure,
            Knight,
            Aran,
            Evan,
            Resistance
        }
    }
}
