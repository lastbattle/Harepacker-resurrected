using HaCreator.MapSimulator.Interaction;
using HaCreator.MapSimulator.UI.Controls;
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
    internal sealed class GuildSkillWindow : UIWindowBase
    {
        private readonly IDXObject _overlay;
        private readonly Point _overlayOffset;
        private readonly IDXObject _headerLayer;
        private readonly Point _headerOffset;
        private readonly Texture2D _row0Texture;
        private readonly Texture2D _row1Texture;
        private readonly Texture2D _recommendTexture;
        private readonly Texture2D[] _summaryStripTextures;
        private readonly UIObject _renewButton;
        private readonly UIObject _upButton;
        private readonly Texture2D _pixel;
        private readonly List<Rectangle> _rowBounds = new();
        private Texture2D _scrollPrevNormal;
        private Texture2D _scrollPrevPressed;
        private Texture2D _scrollNextNormal;
        private Texture2D _scrollNextPressed;
        private Texture2D _scrollTrackEnabled;
        private Texture2D _scrollThumbNormal;
        private Texture2D _scrollThumbPressed;
        private Texture2D _scrollPrevDisabled;
        private Texture2D _scrollNextDisabled;
        private Texture2D _scrollTrackDisabled;

        private Func<GuildSkillSnapshot> _snapshotProvider;
        private Action<int> _entrySelectionHandler;
        private Func<string> _renewHandler;
        private Func<string> _levelUpHandler;
        private Action<string> _feedbackHandler;
        private SpriteFont _font;
        private MouseState _previousMouseState;
        private KeyboardState _previousKeyboardState;
        private int _hoveredIndex = -1;
        private int _firstVisibleIndex;
        private GuildSkillSnapshot _currentSnapshot = new();
        private bool _isDraggingScrollThumb;
        private int _scrollThumbDragOffsetY;

        private const int ListX = 15;
        private const int ListY = 57;
        private const int RowWidth = 233;
        private const int RowHeight = 39;
        private const int VisibleRows = 7;
        private const int ScrollBarX = 249;
        private const int ScrollBarY = ListY;
        private const int ScrollBarWidth = 12;
        private const int ScrollBarHeight = RowHeight * VisibleRows;
        private const int ScrollButtonHeight = 12;
        private const int TooltipWidth = 244;
        private const int TooltipPadding = 10;
        private const int TooltipGapX = 8;
        private const int TooltipTopPadding = 10;
        private const int TooltipBottomPadding = 10;
        private const int TooltipBlankLineHeight = 6;
        private const int TooltipTitleGap = 8;
        private const int TooltipIconSize = 32;
        private const int TooltipIconGap = 8;
        private const int TooltipBodyX = TooltipPadding + TooltipIconSize + TooltipIconGap;
        private const int SummaryStripX = 15;
        private const int SummaryStripY = 329;
        private const int SummaryStripStepY = 16;
        private const int SummaryTextPaddingX = 6;
        private const int SummaryTextPaddingY = 3;
        private const float SummaryTextScale = 0.3f;
        private static readonly Color TooltipBackgroundColor = new(28, 28, 28, 228);
        private static readonly Color TooltipBorderColor = new(112, 112, 112, 235);
        private static readonly Color TooltipTitleColor = new(255, 220, 120);
        private static readonly Color TooltipBodyColor = new(224, 229, 236);

        private readonly record struct TooltipRenderLine(string Text, bool IsTitle)
        {
            internal bool IsBlank => string.IsNullOrEmpty(Text);
        }

        public GuildSkillWindow(
            IDXObject frame,
            IDXObject overlay,
            Point overlayOffset,
            IDXObject headerLayer,
            Point headerOffset,
            Texture2D row0Texture,
            Texture2D row1Texture,
            Texture2D recommendTexture,
            Texture2D[] summaryStripTextures,
            UIObject renewButton,
            UIObject upButton,
            GraphicsDevice device)
            : base(frame)
        {
            _overlay = overlay;
            _overlayOffset = overlayOffset;
            _headerLayer = headerLayer;
            _headerOffset = headerOffset;
            _row0Texture = row0Texture;
            _row1Texture = row1Texture;
            _recommendTexture = recommendTexture;
            _summaryStripTextures = summaryStripTextures ?? Array.Empty<Texture2D>();
            _renewButton = renewButton;
            _upButton = upButton;
            _pixel = new Texture2D(device ?? throw new ArgumentNullException(nameof(device)), 1, 1);
            _pixel.SetData(new[] { Color.White });

            if (_renewButton != null)
            {
                AddButton(_renewButton);
                _renewButton.ButtonClickReleased += _ => EmitFeedback(_renewHandler);
            }

            if (_upButton != null)
            {
                AddButton(_upButton);
                _upButton.ButtonClickReleased += _ => EmitFeedback(_levelUpHandler);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.GuildSkill;

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        internal void SetScrollBarTextures(
            Texture2D prevNormal,
            Texture2D prevPressed,
            Texture2D nextNormal,
            Texture2D nextPressed,
            Texture2D trackEnabled,
            Texture2D thumbNormal,
            Texture2D thumbPressed,
            Texture2D prevDisabled,
            Texture2D nextDisabled,
            Texture2D trackDisabled)
        {
            _scrollPrevNormal = prevNormal;
            _scrollPrevPressed = prevPressed;
            _scrollNextNormal = nextNormal;
            _scrollNextPressed = nextPressed;
            _scrollTrackEnabled = trackEnabled;
            _scrollThumbNormal = thumbNormal;
            _scrollThumbPressed = thumbPressed;
            _scrollPrevDisabled = prevDisabled;
            _scrollNextDisabled = nextDisabled;
            _scrollTrackDisabled = trackDisabled;
        }

        internal void SetSnapshotProvider(Func<GuildSkillSnapshot> snapshotProvider)
        {
            _snapshotProvider = snapshotProvider;
            UpdateButtonLayout(RefreshSnapshot());
        }

        internal void SetHandlers(
            Action<int> entrySelectionHandler,
            Func<string> renewHandler,
            Func<string> levelUpHandler,
            Action<string> feedbackHandler)
        {
            _entrySelectionHandler = entrySelectionHandler;
            _renewHandler = renewHandler;
            _levelUpHandler = levelUpHandler;
            _feedbackHandler = feedbackHandler;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            GuildSkillSnapshot snapshot = RefreshSnapshot();
            EnsureSelectionVisible(snapshot);
            EnsureRowBounds();
            UpdateButtonLayout(snapshot);

            MouseState mouseState = Mouse.GetState();
            if (_isDraggingScrollThumb)
            {
                if (mouseState.LeftButton == ButtonState.Pressed)
                {
                    SetScrollOffsetFromThumb(mouseState.Y, snapshot);
                }
                else
                {
                    _isDraggingScrollThumb = false;
                }
            }

            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed &&
                               _previousMouseState.LeftButton == ButtonState.Released;
            if (leftPressed && TryHandleScrollBarMouseDown(mouseState, snapshot))
            {
                _hoveredIndex = ResolveHoveredIndex(mouseState, snapshot);
                _previousMouseState = mouseState;
                _previousKeyboardState = Keyboard.GetState();
                return;
            }

            bool leftReleased = mouseState.LeftButton == ButtonState.Released &&
                                _previousMouseState.LeftButton == ButtonState.Pressed;
            if (leftReleased && ContainsPoint(mouseState.X, mouseState.Y) && !GetScrollBarBounds().Contains(mouseState.Position))
            {
                for (int i = 0; i < _rowBounds.Count; i++)
                {
                    if (i < snapshot.Entries.Count && _rowBounds[i].Contains(mouseState.Position))
                    {
                        int absoluteIndex = _firstVisibleIndex + i;
                        if (absoluteIndex < snapshot.Entries.Count)
                        {
                            _entrySelectionHandler?.Invoke(absoluteIndex);
                        }
                        break;
                    }
                }
            }

            HandleScrollWheel(mouseState, snapshot);

            _hoveredIndex = ResolveHoveredIndex(mouseState, snapshot);
            HandleKeyboard(snapshot);

            _previousMouseState = mouseState;
            _previousKeyboardState = Keyboard.GetState();
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
            DrawLayer(sprite, _overlay, _overlayOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);
            DrawLayer(sprite, _headerLayer, _headerOffset, drawReflectionInfo, skeletonMeshRenderer, gameTime);

            if (_font == null)
            {
                return;
            }

            GuildSkillSnapshot snapshot = _currentSnapshot ?? RefreshSnapshot();
            DrawEntries(sprite, snapshot);
            DrawScrollBar(sprite, snapshot);
            DrawSummary(sprite, snapshot);
            DrawTooltip(sprite, snapshot, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        private GuildSkillSnapshot RefreshSnapshot()
        {
            _currentSnapshot = _snapshotProvider?.Invoke() ?? new GuildSkillSnapshot();
            return _currentSnapshot;
        }

        private void UpdateButtonLayout(GuildSkillSnapshot snapshot)
        {
            if (_renewButton != null)
            {
                _renewButton.X = 188;
                _renewButton.Y = 351;
                _renewButton.ButtonVisible = snapshot.InGuild;
                _renewButton.SetEnabled(snapshot.InGuild && snapshot.CanRenew);
            }

            if (_upButton == null)
            {
                return;
            }

            bool showUp = snapshot.InGuild &&
                          snapshot.SelectedIndex >= 0 &&
                          snapshot.SelectedIndex >= _firstVisibleIndex &&
                          snapshot.SelectedIndex < Math.Min(snapshot.Entries.Count, _firstVisibleIndex + VisibleRows);
            _upButton.ButtonVisible = showUp;
            _upButton.SetEnabled(showUp && snapshot.CanLevelUpSelected);
            if (!showUp)
            {
                return;
            }

            Rectangle rowBounds = GetRowBounds(snapshot.SelectedIndex - _firstVisibleIndex);
            _upButton.X = rowBounds.Right - _upButton.CanvasSnapshotWidth - 8 - Position.X;
            _upButton.Y = rowBounds.Y + ((RowHeight - _upButton.CanvasSnapshotHeight) / 2) - Position.Y;
        }

        private void DrawEntries(SpriteBatch sprite, GuildSkillSnapshot snapshot)
        {
            _rowBounds.Clear();

            for (int i = 0; i < VisibleRows; i++)
            {
                Rectangle rowBounds = GetRowBounds(i);
                _rowBounds.Add(rowBounds);

                Texture2D rowTexture = i % 2 == 0 ? _row0Texture : _row1Texture;
                if (rowTexture != null)
                {
                    sprite.Draw(rowTexture, new Vector2(rowBounds.X, rowBounds.Y), Color.White);
                }
                else
                {
                    sprite.Draw(_pixel, rowBounds, new Color(21, 31, 46, i % 2 == 0 ? 150 : 120));
                }

                int absoluteIndex = _firstVisibleIndex + i;
                bool selected = absoluteIndex == snapshot.SelectedIndex;
                if (selected)
                {
                    sprite.Draw(_pixel, rowBounds, new Color(112, 173, 228, 48));
                }

                if (absoluteIndex >= snapshot.Entries.Count)
                {
                    continue;
                }

                GuildSkillEntrySnapshot entry = snapshot.Entries[absoluteIndex];
                if (entry.IsRecommended && _recommendTexture != null)
                {
                    sprite.Draw(_recommendTexture, new Vector2(rowBounds.X + 40, rowBounds.Y), Color.White);
                }

                Texture2D icon = entry.CanLevelUp || entry.CurrentLevel > 0
                    ? entry.IconTexture
                    : entry.DisabledIconTexture ?? entry.IconTexture;
                if (icon != null)
                {
                    sprite.Draw(icon, new Rectangle(rowBounds.X + 4, rowBounds.Y + 3, 32, 32), Color.White);
                }

                DrawText(sprite, entry.SkillName, rowBounds.X + 42, rowBounds.Y + 3, new Color(245, 246, 248), 0.38f);
                DrawText(sprite, $"Lv. {entry.CurrentLevel}/{entry.MaxLevel}", rowBounds.X + 42, rowBounds.Y + 18, new Color(255, 222, 142), 0.32f);
                string requirementText = entry.RequiredGuildLevel > 0 && entry.CurrentLevel < entry.MaxLevel
                    ? $"Guild {entry.RequiredGuildLevel}"
                    : string.Empty;
                DrawRightAlignedText(sprite, requirementText, rowBounds.Right - 26, rowBounds.Y + 18, new Color(181, 191, 204), 0.3f);
            }
        }

        private void DrawSummary(SpriteBatch sprite, GuildSkillSnapshot snapshot)
        {
            Color[] palette =
            {
                new Color(245, 246, 248),
                new Color(255, 222, 142),
                new Color(185, 193, 203)
            };

            for (int i = 0; i < snapshot.SummaryLines.Count && i < 3; i++)
            {
                Rectangle stripBounds = GetSummaryStripBounds(i);
                Texture2D stripTexture = i < _summaryStripTextures.Length ? _summaryStripTextures[i] : null;
                if (stripTexture != null)
                {
                    sprite.Draw(stripTexture, new Vector2(stripBounds.X, stripBounds.Y), Color.White);
                }
                else
                {
                    sprite.Draw(_pixel, stripBounds, new Color(5, 11, 18, 76));
                }

                int maxTextWidth = Math.Max(16, stripBounds.Width - (SummaryTextPaddingX * 2));
                IReadOnlyList<string> wrappedLines = WrapText(snapshot.SummaryLines[i], maxTextWidth, SummaryTextScale, preserveBlankLines: false, sprite.GraphicsDevice).ToArray();
                if (wrappedLines.Count == 0)
                {
                    continue;
                }

                int lineHeight = Math.Max(1, (int)Math.Ceiling(_font.LineSpacing * SummaryTextScale));
                int maxVisibleLines = Math.Max(1, Math.Min(2, Math.Max(1, stripBounds.Height / lineHeight)));
                int visibleLineCount = Math.Min(maxVisibleLines, wrappedLines.Count);
                int totalTextHeight = visibleLineCount * lineHeight;
                int textY = stripBounds.Y + Math.Max(0, (stripBounds.Height - totalTextHeight) / 2) + SummaryTextPaddingY;

                for (int lineIndex = 0; lineIndex < visibleLineCount; lineIndex++)
                {
                    DrawText(
                        sprite,
                        wrappedLines[lineIndex],
                        stripBounds.X + SummaryTextPaddingX,
                        textY,
                        palette[Math.Min(i, palette.Length - 1)],
                        SummaryTextScale,
                        maxTextWidth);
                    textY += lineHeight;
                }
            }
        }

        private void DrawScrollBar(SpriteBatch sprite, GuildSkillSnapshot snapshot)
        {
            Rectangle upButtonBounds = GetScrollUpButtonBounds();
            Rectangle downButtonBounds = GetScrollDownButtonBounds();
            Rectangle trackBounds = GetScrollTrackBounds();
            bool canScroll = CanScroll(snapshot);
            MouseState mouseState = Mouse.GetState();
            bool leftPressed = mouseState.LeftButton == ButtonState.Pressed;

            DrawScrollTexture(
                sprite,
                canScroll
                    ? ((leftPressed && upButtonBounds.Contains(mouseState.Position) && !_isDraggingScrollThumb)
                        ? _scrollPrevPressed ?? _scrollPrevNormal
                        : _scrollPrevNormal)
                    : _scrollPrevDisabled ?? _scrollPrevNormal,
                upButtonBounds);

            DrawScrollTexture(sprite, canScroll ? _scrollTrackEnabled : _scrollTrackDisabled ?? _scrollTrackEnabled, trackBounds);

            if (canScroll)
            {
                Rectangle thumbBounds = GetScrollThumbBounds(snapshot);
                bool thumbPressed = _isDraggingScrollThumb || (leftPressed && thumbBounds.Contains(mouseState.Position));
                DrawScrollTexture(sprite, thumbPressed ? _scrollThumbPressed ?? _scrollThumbNormal : _scrollThumbNormal, thumbBounds);
            }

            DrawScrollTexture(
                sprite,
                canScroll
                    ? ((leftPressed && downButtonBounds.Contains(mouseState.Position) && !_isDraggingScrollThumb)
                        ? _scrollNextPressed ?? _scrollNextNormal
                        : _scrollNextNormal)
                    : _scrollNextDisabled ?? _scrollNextNormal,
                downButtonBounds);
        }

        private Rectangle GetRowBounds(int index)
        {
            return new Rectangle(Position.X + ListX, Position.Y + ListY + (index * RowHeight), RowWidth, RowHeight);
        }

        private void EnsureRowBounds()
        {
            if (_rowBounds.Count == VisibleRows)
            {
                return;
            }

            _rowBounds.Clear();
            for (int i = 0; i < VisibleRows; i++)
            {
                _rowBounds.Add(GetRowBounds(i));
            }
        }

        private void EmitFeedback(Func<string> handler)
        {
            string message = handler?.Invoke();
            if (!string.IsNullOrWhiteSpace(message))
            {
                _feedbackHandler?.Invoke(message);
            }
        }

        private int ResolveHoveredIndex(MouseState mouseState, GuildSkillSnapshot snapshot)
        {
            if (!ContainsPoint(mouseState.X, mouseState.Y))
            {
                return -1;
            }

            for (int i = 0; i < _rowBounds.Count && (_firstVisibleIndex + i) < snapshot.Entries.Count; i++)
            {
                if (_rowBounds[i].Contains(mouseState.Position))
                {
                    return _firstVisibleIndex + i;
                }
            }

            return -1;
        }

        private void HandleScrollWheel(MouseState mouseState, GuildSkillSnapshot snapshot)
        {
            int wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (wheelDelta == 0 || snapshot.Entries.Count <= VisibleRows)
            {
                return;
            }

            Rectangle listBounds = GetListBounds();
            if (!listBounds.Contains(mouseState.Position))
            {
                return;
            }

            ScrollBy(wheelDelta > 0 ? -1 : 1, snapshot);
        }

        private void HandleKeyboard(GuildSkillSnapshot snapshot)
        {
            KeyboardState keyboardState = Keyboard.GetState();
            int entryCount = snapshot.Entries.Count;
            if (entryCount <= 0)
            {
                return;
            }

            int selection = snapshot.SelectedIndex >= 0
                ? Math.Clamp(snapshot.SelectedIndex, 0, entryCount - 1)
                : 0;
            int nextSelection = selection;

            if (WasKeyPressed(keyboardState, Keys.Up))
                nextSelection = Math.Max(0, selection - 1);
            else if (WasKeyPressed(keyboardState, Keys.Down))
                nextSelection = Math.Min(entryCount - 1, selection + 1);
            else if (WasKeyPressed(keyboardState, Keys.PageUp))
                nextSelection = Math.Max(0, selection - VisibleRows);
            else if (WasKeyPressed(keyboardState, Keys.PageDown))
                nextSelection = Math.Min(entryCount - 1, selection + VisibleRows);
            else if (WasKeyPressed(keyboardState, Keys.Home))
                nextSelection = 0;
            else if (WasKeyPressed(keyboardState, Keys.End))
                nextSelection = entryCount - 1;

            if (nextSelection != selection || snapshot.SelectedIndex < 0)
            {
                _entrySelectionHandler?.Invoke(nextSelection);
            }
        }

        private void EnsureSelectionVisible(GuildSkillSnapshot snapshot)
        {
            int entryCount = snapshot.Entries.Count;
            int maxFirstVisibleIndex = Math.Max(0, entryCount - VisibleRows);
            if (entryCount <= 0)
            {
                _firstVisibleIndex = 0;
                return;
            }

            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex, 0, maxFirstVisibleIndex);
            if (snapshot.SelectedIndex < 0)
            {
                return;
            }

            if (snapshot.SelectedIndex < _firstVisibleIndex)
            {
                _firstVisibleIndex = snapshot.SelectedIndex;
            }
            else if (snapshot.SelectedIndex >= _firstVisibleIndex + VisibleRows)
            {
                _firstVisibleIndex = snapshot.SelectedIndex - VisibleRows + 1;
            }

            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex, 0, maxFirstVisibleIndex);
        }

        private void ScrollBy(int delta, GuildSkillSnapshot snapshot)
        {
            int maxFirstVisibleIndex = Math.Max(0, snapshot.Entries.Count - VisibleRows);
            _firstVisibleIndex = Math.Clamp(_firstVisibleIndex + delta, 0, maxFirstVisibleIndex);
        }

        private bool CanScroll(GuildSkillSnapshot snapshot)
        {
            return snapshot != null && snapshot.Entries.Count > VisibleRows;
        }

        private Rectangle GetScrollBarBounds()
        {
            return new Rectangle(Position.X + ScrollBarX, Position.Y + ScrollBarY, ScrollBarWidth, ScrollBarHeight);
        }

        private Rectangle GetScrollUpButtonBounds()
        {
            return new Rectangle(Position.X + ScrollBarX, Position.Y + ScrollBarY, ScrollBarWidth, ScrollButtonHeight);
        }

        private Rectangle GetScrollDownButtonBounds()
        {
            return new Rectangle(
                Position.X + ScrollBarX,
                Position.Y + ScrollBarY + ScrollBarHeight - ScrollButtonHeight,
                ScrollBarWidth,
                ScrollButtonHeight);
        }

        private Rectangle GetScrollTrackBounds()
        {
            return new Rectangle(
                Position.X + ScrollBarX,
                Position.Y + ScrollBarY + ScrollButtonHeight,
                ScrollBarWidth,
                ScrollBarHeight - (ScrollButtonHeight * 2));
        }

        private Rectangle GetScrollThumbBounds(GuildSkillSnapshot snapshot)
        {
            Rectangle trackBounds = GetScrollTrackBounds();
            int thumbHeight = Math.Min(trackBounds.Height, Math.Max(1, _scrollThumbNormal?.Height ?? ScrollBarWidth));
            int maxScroll = Math.Max(0, snapshot.Entries.Count - VisibleRows);
            if (maxScroll <= 0)
            {
                return new Rectangle(trackBounds.X, trackBounds.Y, trackBounds.Width, thumbHeight);
            }

            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int thumbY = trackBounds.Y;
            if (travel > 0)
            {
                float ratio = _firstVisibleIndex / (float)maxScroll;
                thumbY += (int)Math.Round(travel * ratio);
            }

            return new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        }

        private void SetScrollOffsetFromThumb(int mouseY, GuildSkillSnapshot snapshot)
        {
            Rectangle trackBounds = GetScrollTrackBounds();
            int thumbHeight = Math.Min(trackBounds.Height, Math.Max(1, _scrollThumbNormal?.Height ?? ScrollBarWidth));
            int travel = Math.Max(0, trackBounds.Height - thumbHeight);
            int maxScroll = Math.Max(0, snapshot.Entries.Count - VisibleRows);
            if (travel <= 0 || maxScroll <= 0)
            {
                _firstVisibleIndex = 0;
                return;
            }

            int thumbTop = Math.Clamp(mouseY - _scrollThumbDragOffsetY, trackBounds.Y, trackBounds.Y + travel);
            float ratio = (thumbTop - trackBounds.Y) / (float)travel;
            _firstVisibleIndex = (int)Math.Round(ratio * maxScroll);
        }

        private bool TryHandleScrollBarMouseDown(MouseState mouseState, GuildSkillSnapshot snapshot)
        {
            if (!GetScrollBarBounds().Contains(mouseState.Position))
            {
                return false;
            }

            if (!CanScroll(snapshot))
            {
                return true;
            }

            Rectangle upButtonBounds = GetScrollUpButtonBounds();
            if (upButtonBounds.Contains(mouseState.Position))
            {
                ScrollBy(-1, snapshot);
                return true;
            }

            Rectangle downButtonBounds = GetScrollDownButtonBounds();
            if (downButtonBounds.Contains(mouseState.Position))
            {
                ScrollBy(1, snapshot);
                return true;
            }

            Rectangle thumbBounds = GetScrollThumbBounds(snapshot);
            if (thumbBounds.Contains(mouseState.Position))
            {
                _isDraggingScrollThumb = true;
                _scrollThumbDragOffsetY = mouseState.Y - thumbBounds.Y;
                return true;
            }

            Rectangle trackBounds = GetScrollTrackBounds();
            if (trackBounds.Contains(mouseState.Position))
            {
                ScrollBy(mouseState.Y < thumbBounds.Y ? -VisibleRows : VisibleRows, snapshot);
                return true;
            }

            return false;
        }

        private bool WasKeyPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
        }

        private void DrawTooltip(SpriteBatch sprite, GuildSkillSnapshot snapshot, int renderWidth, int renderHeight)
        {
            int tooltipIndex = _hoveredIndex >= 0 ? _hoveredIndex : snapshot.SelectedIndex;
            if (tooltipIndex < 0 || tooltipIndex >= snapshot.Entries.Count)
            {
                return;
            }

            GuildSkillEntrySnapshot entry = snapshot.Entries[tooltipIndex];
            List<TooltipRenderLine> titleLines = BuildTooltipLines(entry, titleOnly: true);
            List<TooltipRenderLine> bodyLines = BuildTooltipLines(entry, titleOnly: false);
            if (titleLines.Count == 0 && bodyLines.Count == 0)
            {
                return;
            }

            Texture2D tooltipIcon = entry.IconTexture ?? entry.DisabledIconTexture;
            float titleScale = 0.36f;
            float bodyScale = 0.31f;
            int lineSpacing = 12;
            int titleHeight = MeasureTooltipLineHeight(titleLines, titleScale, lineSpacing);
            int bodyHeight = MeasureTooltipLineHeight(bodyLines, bodyScale, lineSpacing);
            int iconHeight = tooltipIcon != null ? TooltipIconSize : 0;
            int contentHeight = TooltipTopPadding + titleHeight;
            if (bodyLines.Count > 0 || tooltipIcon != null)
            {
                contentHeight += TooltipTitleGap + Math.Max(iconHeight, bodyHeight);
            }
            contentHeight += TooltipBottomPadding;

            int visibleIndex = Math.Clamp(tooltipIndex - _firstVisibleIndex, 0, VisibleRows - 1);
            Rectangle rowBounds = GetRowBounds(visibleIndex);
            int tooltipX = rowBounds.Right + TooltipGapX;
            if (tooltipX + TooltipWidth > renderWidth - TooltipPadding)
            {
                tooltipX = rowBounds.X - TooltipGapX - TooltipWidth;
            }

            tooltipX = Math.Clamp(tooltipX, TooltipPadding, Math.Max(TooltipPadding, renderWidth - TooltipPadding - TooltipWidth));
            int maxTooltipY = Math.Max(TooltipPadding, renderHeight - TooltipPadding - contentHeight);
            int tooltipY = Math.Clamp(rowBounds.Y, TooltipPadding, maxTooltipY);
            Rectangle tooltipBounds = new Rectangle(tooltipX, tooltipY, TooltipWidth, contentHeight);

            sprite.Draw(_pixel, tooltipBounds, TooltipBackgroundColor);
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X - 1, tooltipBounds.Y - 1, tooltipBounds.Width + 2, 1), TooltipBorderColor);
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X - 1, tooltipBounds.Bottom, tooltipBounds.Width + 2, 1), TooltipBorderColor);
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.X - 1, tooltipBounds.Y, 1, tooltipBounds.Height), TooltipBorderColor);
            sprite.Draw(_pixel, new Rectangle(tooltipBounds.Right, tooltipBounds.Y, 1, tooltipBounds.Height), TooltipBorderColor);

            int y = tooltipBounds.Y + TooltipTopPadding;
            for (int i = 0; i < titleLines.Count; i++)
            {
                if (titleLines[i].IsBlank)
                {
                    y += TooltipBlankLineHeight;
                    continue;
                }

                ClientTextDrawing.Draw(sprite, titleLines[i].Text, new Vector2(tooltipBounds.X + TooltipPadding, y), TooltipTitleColor, titleScale, _font);
                y += Math.Max(lineSpacing, (int)Math.Ceiling(_font.LineSpacing * titleScale));
            }

            if (bodyLines.Count == 0 && tooltipIcon == null)
            {
                return;
            }

            y += TooltipTitleGap;
            int bodyStartY = y;
            if (tooltipIcon != null)
            {
                sprite.Draw(
                    tooltipIcon,
                    new Rectangle(tooltipBounds.X + TooltipPadding, bodyStartY, TooltipIconSize, TooltipIconSize),
                    Color.White);
            }

            int bodyY = bodyStartY;
            for (int i = 0; i < bodyLines.Count; i++)
            {
                if (bodyLines[i].IsBlank)
                {
                    bodyY += TooltipBlankLineHeight;
                    continue;
                }

                ClientTextDrawing.Draw(
                    sprite,
                    bodyLines[i].Text,
                    new Vector2(tooltipBounds.X + TooltipBodyX, bodyY),
                    TooltipBodyColor,
                    bodyScale,
                    _font);
                bodyY += Math.Max(lineSpacing, (int)Math.Ceiling(_font.LineSpacing * bodyScale));
            }
        }

        private List<TooltipRenderLine> BuildTooltipLines(GuildSkillEntrySnapshot entry, bool titleOnly)
        {
            List<TooltipRenderLine> lines = new();
            IReadOnlyList<string> rawLines = GuildSkillTooltipContentBuilder.BuildLines(entry);
            if (rawLines.Count == 0)
            {
                return lines;
            }

            if (titleOnly)
            {
                AddWrappedLine(lines, rawLines[0], true);
                return lines;
            }

            for (int i = 1; i < rawLines.Count; i++)
            {
                AddWrappedLine(lines, rawLines[i], false);
            }

            return lines;
        }

        private void AddWrappedLine(List<TooltipRenderLine> lines, string text, bool isTitle)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float scale = isTitle ? 0.36f : 0.31f;
            int maxWidth = isTitle
                ? TooltipWidth - (TooltipPadding * 2)
                : TooltipWidth - TooltipBodyX - TooltipPadding;
            foreach (string wrappedLine in WrapText(text, maxWidth, scale, preserveBlankLines: true))
            {
                lines.Add(new TooltipRenderLine(wrappedLine, isTitle));
            }
        }

        private int MeasureTooltipLineHeight(IReadOnlyList<TooltipRenderLine> lines, float scale, int lineSpacing)
        {
            int height = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].IsBlank)
                {
                    height += TooltipBlankLineHeight;
                    continue;
                }

                height += Math.Max(lineSpacing, (int)Math.Ceiling(_font.LineSpacing * scale));
            }

            return height;
        }

        private IEnumerable<string> WrapText(string text, int maxWidth, float scale, bool preserveBlankLines, GraphicsDevice graphicsDevice = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            string normalized = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');

            string[] paragraphs = normalized.Split('\n');
            for (int paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
            {
                string paragraph = paragraphs[paragraphIndex].Trim();
                if (paragraph.Length == 0)
                {
                    if (preserveBlankLines)
                    {
                        yield return string.Empty;
                    }

                    continue;
                }

                string[] words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    continue;
                }

                string line = words[0];
                if (ClientTextDrawing.Measure(graphicsDevice, line, scale, _font).X > maxWidth)
                {
                    foreach (string fragment in SplitOverlongToken(line, maxWidth, scale, graphicsDevice))
                    {
                        yield return fragment;
                    }

                    line = string.Empty;
                }

                for (int i = 1; i < words.Length; i++)
                {
                    string word = words[i];
                    if (ClientTextDrawing.Measure(graphicsDevice, word, scale, _font).X > maxWidth)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            yield return line;
                            line = string.Empty;
                        }

                        foreach (string fragment in SplitOverlongToken(word, maxWidth, scale, graphicsDevice))
                        {
                            yield return fragment;
                        }

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        line = word;
                        continue;
                    }

                    string candidate = line + " " + word;
                    if (ClientTextDrawing.Measure(graphicsDevice, candidate, scale, _font).X <= maxWidth)
                    {
                        line = candidate;
                        continue;
                    }

                    yield return line;
                    line = word;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;
                }
            }
        }

        private IEnumerable<string> SplitOverlongToken(string text, int maxWidth, float scale, GraphicsDevice graphicsDevice)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            int start = 0;
            while (start < text.Length)
            {
                int length = 1;
                int bestLength = 1;
                while (start + length <= text.Length)
                {
                    string candidate = text.Substring(start, length);
                    if (ClientTextDrawing.Measure(graphicsDevice, candidate, scale, _font).X > maxWidth)
                    {
                        break;
                    }

                    bestLength = length;
                    length++;
                }

                yield return text.Substring(start, bestLength);
                start += bestLength;
            }
        }

        private void DrawScrollTexture(SpriteBatch sprite, Texture2D texture, Rectangle bounds)
        {
            if (texture != null)
            {
                sprite.Draw(texture, new Vector2(bounds.X, bounds.Y), Color.White);
                return;
            }

            sprite.Draw(_pixel, bounds, new Color(54, 70, 94, 200));
        }

        private Rectangle GetListBounds()
        {
            return new Rectangle(Position.X + ListX, Position.Y + ListY, RowWidth + ScrollBarWidth + 1, RowHeight * VisibleRows);
        }

        private void DrawLayer(
            SpriteBatch sprite,
            IDXObject layer,
            Point offset,
            ReflectionDrawableBoundary drawReflectionInfo,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime)
        {
            layer?.DrawBackground(sprite, skeletonMeshRenderer, gameTime, Position.X + offset.X, Position.Y + offset.Y, Color.White, false, drawReflectionInfo);
        }

        private void DrawText(SpriteBatch sprite, string text, int x, int y, Color color, float scale, float? maxWidth = null)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                ClientTextDrawing.Draw(sprite, text, new Vector2(x, y), color, scale, _font, maxWidth);
            }
        }

        private void DrawRightAlignedText(SpriteBatch sprite, string text, int rightX, int y, Color color, float scale)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Vector2 size = ClientTextDrawing.Measure((GraphicsDevice)null, text, scale, _font);
            DrawText(sprite, text, (int)Math.Round(rightX - size.X), y, color, scale);
        }

        private Rectangle GetSummaryStripBounds(int index)
        {
            Texture2D stripTexture = index < _summaryStripTextures.Length ? _summaryStripTextures[index] : null;
            int width = Math.Max(170, stripTexture?.Width ?? 233);
            int height = Math.Max(12, stripTexture?.Height ?? 21);
            return new Rectangle(
                Position.X + SummaryStripX,
                Position.Y + SummaryStripY + (index * SummaryStripStepY),
                width,
                height);
        }

    }
}
