using HaCreator.MapSimulator.Character;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using SD = System.Drawing;
using SDText = System.Drawing.Text;

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
        private readonly IReadOnlyList<PreviewCanvasFrame> _buyCharacterFrames;
        private readonly Texture2D _emptySlotTexture;
        private readonly Dictionary<PreviewNameTagCacheKey, Texture2D> _nameTagTextureCache = new();
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SD.Bitmap _measureBitmap;
        private readonly SD.Graphics _measureGraphics;
        private readonly SD.Font _nameTagFont;

        private SpriteFont _font;
        private int _selectedIndex = -1;
        private int _pageIndex;
        private int _slotCount;
        private int _buyCharacterCount;
        private int _lastActivatedEntryIndex = -1;
        private int _lastActivationTick = int.MinValue;

        public AvatarPreviewCarouselWindow(
            IDXObject frame,
            Texture2D normalCardTexture,
            Texture2D selectedCardTexture,
            PreviewNameTagStyle normalNameTagStyle,
            PreviewNameTagStyle selectedNameTagStyle,
            IReadOnlyDictionary<LoginJobDecorationStyle, PreviewCanvasFrame> jobDecorations,
            Texture2D emptySlotTexture,
            IReadOnlyList<PreviewCanvasFrame> buyCharacterFrames,
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
            _emptySlotTexture = emptySlotTexture;
            _buyCharacterFrames = buyCharacterFrames ?? Array.Empty<PreviewCanvasFrame>();
            _prevPageButton = prevPageButton;
            _nextPageButton = nextPageButton;
            _graphicsDevice = frame?.Texture?.GraphicsDevice;
            _measureBitmap = new SD.Bitmap(1, 1);
            _measureGraphics = SD.Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            _nameTagFont = new SD.Font("Tahoma", 12f, SD.FontStyle.Regular, SD.GraphicsUnit.Pixel);

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
        public event Action<int> PageRequested;
        public event Action EnterRequested;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override bool SupportsDragging => false;

        public void SetRoster(
            IReadOnlyList<LoginCharacterRosterEntry> entries,
            int selectedIndex,
            int slotCount,
            int buyCharacterCount,
            int pageIndex)
        {
            ClearNameTagTextureCache();
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
            _slotCount = Math.Max(0, slotCount);
            _buyCharacterCount = Math.Max(0, buyCharacterCount);
            _pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, GetPageCount() - 1));

            for (int i = 0; i < _cardButtons.Count; i++)
            {
                int displaySlotIndex = GetDisplaySlotIndexForVisibleSlot(i);
                LoginCharacterRosterSlotKind slotKind = GetSlotKind(displaySlotIndex);
                bool visible = slotKind != LoginCharacterRosterSlotKind.Hidden;
                _cardButtons[i].SetVisible(visible);
                _cardButtons[i].SetEnabled(slotKind == LoginCharacterRosterSlotKind.Character);
                _cardButtons[i].SetButtonState(displaySlotIndex == _selectedIndex ? UIObjectState.Pressed : UIObjectState.Normal);
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
        }

        private void DrawPreviewCards(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, int tickCount)
        {
            for (int slotIndex = 0; slotIndex < _cardButtons.Count; slotIndex++)
            {
                int displaySlotIndex = GetDisplaySlotIndexForVisibleSlot(slotIndex);
                LoginCharacterRosterSlotKind slotKind = GetSlotKind(displaySlotIndex);
                if (slotKind == LoginCharacterRosterSlotKind.Hidden)
                {
                    continue;
                }

                Rectangle cardBounds = GetCardBounds(slotIndex);
                bool isSelected = displaySlotIndex == _selectedIndex;
                Texture2D cardTexture = isSelected ? _selectedCardTexture : _normalCardTexture;
                if (cardTexture != null)
                {
                    sprite.Draw(cardTexture, new Vector2(cardBounds.X, cardBounds.Y), Color.White);
                }

                if (slotKind == LoginCharacterRosterSlotKind.Empty)
                {
                    DrawEmptySlot(sprite, cardBounds);
                    continue;
                }

                if (slotKind == LoginCharacterRosterSlotKind.BuyCharacter)
                {
                    DrawBuyCharacterCard(sprite, cardBounds, tickCount);
                    continue;
                }

                if (displaySlotIndex < 0 || displaySlotIndex >= _entries.Count)
                {
                    continue;
                }

                LoginCharacterRosterEntry entry = _entries[displaySlotIndex];
                CharacterBuild build = entry.Build;
                if (build == null)
                {
                    continue;
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

        private void ActivateCard(int visibleSlotIndex)
        {
            int entryIndex = GetDisplaySlotIndexForVisibleSlot(visibleSlotIndex);
            if (GetSlotKind(entryIndex) != LoginCharacterRosterSlotKind.Character ||
                entryIndex < 0 ||
                entryIndex >= _entries.Count)
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
            PageRequested?.Invoke(targetPage);
        }

        private void SelectNextPage()
        {
            int pageCount = GetPageCount();
            if (pageCount <= 1)
            {
                return;
            }

            int targetPage = (_pageIndex + 1) % pageCount;
            PageRequested?.Invoke(targetPage);
        }

        private int GetDisplaySlotIndexForVisibleSlot(int visibleSlotIndex)
        {
            return (_pageIndex * EntriesPerPage) + visibleSlotIndex;
        }

        private int GetPageCount()
        {
            int displaySlotCount = GetDisplaySlotCount();
            return displaySlotCount == 0 ? 0 : ((displaySlotCount - 1) / EntriesPerPage) + 1;
        }

        private int GetPageIndexForSelection(int selectedIndex)
        {
            if (selectedIndex < 0 || GetDisplaySlotCount() == 0)
            {
                return 0;
            }

            return Math.Clamp(selectedIndex / EntriesPerPage, 0, Math.Max(0, GetPageCount() - 1));
        }

        private int GetDisplaySlotCount()
        {
            return Math.Max(_entries.Count, _slotCount + _buyCharacterCount);
        }

        private LoginCharacterRosterSlotKind GetSlotKind(int displaySlotIndex)
        {
            if (displaySlotIndex < 0 || displaySlotIndex >= GetDisplaySlotCount())
            {
                return LoginCharacterRosterSlotKind.Hidden;
            }

            if (displaySlotIndex < _entries.Count)
            {
                return LoginCharacterRosterSlotKind.Character;
            }

            if (displaySlotIndex < _slotCount)
            {
                return LoginCharacterRosterSlotKind.Empty;
            }

            if (displaySlotIndex < _slotCount + _buyCharacterCount)
            {
                return LoginCharacterRosterSlotKind.BuyCharacter;
            }

            return LoginCharacterRosterSlotKind.Hidden;
        }

        private Rectangle GetCardBounds(int visibleSlotIndex)
        {
            int x = Position.X + CardStartX + (visibleSlotIndex * (CardWidth + CardGap));
            int y = Position.Y + CardStartY;
            return new Rectangle(x, y, CardWidth, CardHeight);
        }

        private void DrawEmptySlot(SpriteBatch sprite, Rectangle cardBounds)
        {
            if (_emptySlotTexture == null)
            {
                return;
            }

            Vector2 position = new(
                cardBounds.Center.X - (_emptySlotTexture.Width / 2f),
                cardBounds.Y + 34f);
            sprite.Draw(_emptySlotTexture, position, Color.White);
        }

        private void DrawBuyCharacterCard(SpriteBatch sprite, Rectangle cardBounds, int tickCount)
        {
            PreviewCanvasFrame frame = ResolveBuyCharacterFrame(tickCount);
            if (frame.Texture == null)
            {
                return;
            }

            Vector2 position = new(
                cardBounds.Center.X - frame.Origin.X,
                cardBounds.Center.Y - frame.Origin.Y + 6f);
            sprite.Draw(frame.Texture, position, Color.White);
        }

        private PreviewCanvasFrame ResolveBuyCharacterFrame(int tickCount)
        {
            if (_buyCharacterFrames == null || _buyCharacterFrames.Count == 0)
            {
                return default;
            }

            int totalDuration = 0;
            foreach (PreviewCanvasFrame frame in _buyCharacterFrames)
            {
                totalDuration += Math.Max(1, frame.DelayMs);
            }

            if (totalDuration <= 0)
            {
                return _buyCharacterFrames[0];
            }

            int animationTick = Math.Abs(tickCount % totalDuration);
            int elapsed = 0;
            foreach (PreviewCanvasFrame frame in _buyCharacterFrames)
            {
                elapsed += Math.Max(1, frame.DelayMs);
                if (animationTick < elapsed)
                {
                    return frame;
                }
            }

            return _buyCharacterFrames[_buyCharacterFrames.Count - 1];
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
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            Texture2D nameTagTexture = GetOrCreateNameTagTexture(name, isSelected);
            if (nameTagTexture == null)
            {
                DrawNameTagFallback(sprite, name, isSelected, centerX, y);
                return;
            }

            sprite.Draw(
                nameTagTexture,
                new Vector2(centerX - (nameTagTexture.Width / 2f), y),
                Color.White);
        }

        private Texture2D GetOrCreateNameTagTexture(string name, bool isSelected)
        {
            PreviewNameTagStyle style = isSelected ? _selectedNameTagStyle : _normalNameTagStyle;
            if (!style.IsReady || _graphicsDevice == null || _nameTagFont == null)
            {
                return null;
            }

            PreviewNameTagCacheKey cacheKey = new(name, isSelected);
            if (_nameTagTextureCache.TryGetValue(cacheKey, out Texture2D cachedTexture) &&
                cachedTexture != null &&
                !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            SD.SizeF measured = _measureGraphics.MeasureString(name, _nameTagFont, SD.PointF.Empty, SD.StringFormat.GenericTypographic);
            int textWidth = Math.Max(1, (int)Math.Ceiling(measured.Width));
            int totalWidth = Math.Max(style.Left.Width + style.Right.Width, textWidth + 10);
            int totalHeight = Math.Max(Math.Max(style.Left.Height, style.Middle.Height), style.Right.Height);

            using var bitmap = new SD.Bitmap(totalWidth, totalHeight);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);
            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;

            using (SD.Bitmap leftBitmap = style.Left.ToBitmap())
            using (SD.Bitmap middleBitmap = style.Middle.ToBitmap())
            using (SD.Bitmap rightBitmap = style.Right.ToBitmap())
            using (var brush = new SD.SolidBrush(SD.Color.FromArgb(style.TextColor.A, style.TextColor.R, style.TextColor.G, style.TextColor.B)))
            {
                graphics.DrawImageUnscaled(leftBitmap, 0, 0);

                int middleStartX = style.Left.Width;
                int middleEndX = Math.Max(middleStartX, totalWidth - style.Right.Width);
                for (int offsetX = middleStartX; offsetX < middleEndX; offsetX += middleBitmap.Width)
                {
                    int remainingWidth = middleEndX - offsetX;
                    if (remainingWidth <= 0)
                    {
                        break;
                    }

                    int drawWidth = Math.Min(middleBitmap.Width, remainingWidth);
                    graphics.DrawImage(
                        middleBitmap,
                        new SD.Rectangle(offsetX, 0, drawWidth, middleBitmap.Height),
                        new SD.Rectangle(0, 0, drawWidth, middleBitmap.Height),
                        SD.GraphicsUnit.Pixel);
                }

                graphics.DrawImageUnscaled(rightBitmap, totalWidth - style.Right.Width, 0);
                graphics.DrawString(
                    name,
                    _nameTagFont,
                    brush,
                    (float)(((totalWidth - textWidth) / 2) - 1),
                    2f,
                    SD.StringFormat.GenericTypographic);
            }

            Texture2D texture = bitmap.ToTexture2D(_graphicsDevice);
            _nameTagTextureCache[cacheKey] = texture;
            return texture;
        }

        private void DrawNameTagFallback(SpriteBatch sprite, string name, bool isSelected, int centerX, int y)
        {
            if (_font == null)
            {
                return;
            }

            Color fallbackColor = isSelected ? Color.White : new Color(153, 153, 153);
            Vector2 fallbackSize = _font.MeasureString(name) * 0.5f;
            sprite.DrawString(
                _font,
                name,
                new Vector2(centerX - (fallbackSize.X * 0.5f), y + 4),
                fallbackColor,
                0f,
                Vector2.Zero,
                0.5f,
                SpriteEffects.None,
                0f);
        }

        private void ClearNameTagTextureCache()
        {
            foreach (Texture2D texture in _nameTagTextureCache.Values)
            {
                texture?.Dispose();
            }

            _nameTagTextureCache.Clear();
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
            public PreviewCanvasFrame(Texture2D texture, Point origin, int delayMs = 0)
            {
                Texture = texture;
                Origin = origin;
                DelayMs = delayMs;
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
            public int DelayMs { get; }
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

        private readonly struct PreviewNameTagCacheKey : IEquatable<PreviewNameTagCacheKey>
        {
            public PreviewNameTagCacheKey(string name, bool isSelected)
            {
                Name = name ?? string.Empty;
                IsSelected = isSelected;
            }

            public string Name { get; }
            public bool IsSelected { get; }

            public bool Equals(PreviewNameTagCacheKey other)
            {
                return IsSelected == other.IsSelected &&
                       string.Equals(Name, other.Name, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PreviewNameTagCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, IsSelected);
            }
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
