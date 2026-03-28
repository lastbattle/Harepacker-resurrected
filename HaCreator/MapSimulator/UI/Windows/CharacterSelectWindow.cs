using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Text;
using SD = System.Drawing;
using SDText = System.Drawing.Text;

namespace HaCreator.MapSimulator.UI
{
    public sealed class CharacterSelectWindow : UIWindowBase
    {
        private static readonly Point ClientStatusScrollPosition = new(201, 112);
        private static readonly Point ClientStatusSparkleAnchor = new(285, -26);
        private static readonly Point ClientStatusBeamAnchor = new(274, -18);
        private static readonly Point ClientStatusAccentPosition = new(281, 128);
        private static readonly Point ClientEnterFocusAnchor = new(148, 246);
        private static readonly Point ClientEventBannerTopLeft = new(66, 8);
        private static readonly Point BalloonDefaultAnchor = new(309, 94);
        private const int EntriesPerPage = 3;
        private const int OwnerWidth = 618;
        private const int CardWidth = 183;
        private const int CardStartX = 18;
        private const int CardGap = 14;
        private const int BalloonWidth = 220;
        private const int BalloonHeight = 40;
        private const int BalloonTextInsetX = 10;
        private const int BalloonSingleLineTextInsetY = 15;
        private const int BalloonMultiLineTextInsetY = 9;
        private const int StatusTextWrapWidth = 170;
        private const float StatusTextScale = 0.50f;
        private const int BasicBlackFontHeight = 12;
        private const string DefaultInstructionMessage = "Double-click to enter.";
        private const string DefaultStatusMessage = "Select a character.";

        private readonly UIObject _enterButton;
        private readonly UIObject _newButton;
        private readonly UIObject _deleteButton;
        private readonly IReadOnlyList<AnimationFrame> _statusScrollFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusSparkleFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusBeamFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusAccentFrames;
        private readonly IReadOnlyList<AnimationFrame> _enterFocusFrames;
        private readonly BalloonStyle _instructionBalloonStyle;
        private readonly OwnerCanvasFrame _eventBanner;
        private readonly Dictionary<TextRenderCacheKey, Texture2D> _textTextureCache = new();
        private readonly SD.Bitmap _measureBitmap;
        private readonly SD.Graphics _measureGraphics;
        private readonly SD.Font _basicBlackFont;
        private readonly GraphicsDevice _graphicsDevice;

        private SpriteFont _font;
        private string _statusMessage = "Select a character.";
        private bool _canEnter;
        private int _showTick = -1;
        private int _selectedIndex = -1;
        private int _pageIndex;

        public CharacterSelectWindow(
            IDXObject frame,
            UIObject enterButton,
            UIObject newButton,
            UIObject deleteButton,
            IReadOnlyList<AnimationFrame> statusScrollFrames,
            IReadOnlyList<AnimationFrame> statusSparkleFrames,
            IReadOnlyList<AnimationFrame> statusBeamFrames,
            IReadOnlyList<AnimationFrame> statusAccentFrames,
            IReadOnlyList<AnimationFrame> enterFocusFrames,
            BalloonStyle instructionBalloonStyle,
            OwnerCanvasFrame eventBanner)
            : base(frame)
        {
            _enterButton = enterButton;
            _newButton = newButton;
            _deleteButton = deleteButton;
            _statusScrollFrames = statusScrollFrames ?? Array.Empty<AnimationFrame>();
            _statusSparkleFrames = statusSparkleFrames ?? Array.Empty<AnimationFrame>();
            _statusBeamFrames = statusBeamFrames ?? Array.Empty<AnimationFrame>();
            _statusAccentFrames = statusAccentFrames ?? Array.Empty<AnimationFrame>();
            _enterFocusFrames = enterFocusFrames ?? Array.Empty<AnimationFrame>();
            _instructionBalloonStyle = instructionBalloonStyle;
            _eventBanner = eventBanner;
            _graphicsDevice = frame?.Texture?.GraphicsDevice
                ?? _instructionBalloonStyle.Center?.GraphicsDevice
                ?? _eventBanner.Texture?.GraphicsDevice;
            _measureBitmap = new SD.Bitmap(1, 1);
            _measureGraphics = SD.Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            _basicBlackFont = new SD.Font("Tahoma", BasicBlackFontHeight, SD.FontStyle.Regular, SD.GraphicsUnit.Pixel);

            if (_enterButton != null)
            {
                _enterButton.ButtonClickReleased += _ => EnterRequested?.Invoke();
                AddButton(_enterButton);
            }

            if (_newButton != null)
            {
                _newButton.ButtonClickReleased += _ => NewCharacterRequested?.Invoke();
                AddButton(_newButton);
            }

            if (_deleteButton != null)
            {
                _deleteButton.ButtonClickReleased += _ => DeleteRequested?.Invoke();
                AddButton(_deleteButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterSelect;

        public event Action<int> CharacterSelected;
        public event Action EnterRequested;
        public event Action NewCharacterRequested;
        public event Action DeleteRequested;

        public void NotifyCharacterSelected(int rowIndex)
        {
            CharacterSelected?.Invoke(rowIndex);
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override bool SupportsDragging => false;

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                _showTick = -1;
            }
        }

        public void SetRoster(
            IReadOnlyList<LoginCharacterRosterEntry> entries,
            int selectedIndex,
            string statusMessage,
            int slotCount,
            int buyCharacterCount,
            int pageIndex,
            int pageCount,
            bool canEnter,
            bool canDelete)
        {
            _statusMessage = statusMessage ?? string.Empty;
            _canEnter = canEnter;
            _selectedIndex = selectedIndex;
            _pageIndex = Math.Max(0, pageIndex);
            _enterButton?.SetEnabled(canEnter);
            _deleteButton?.SetEnabled(canDelete);
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
            if (_showTick < 0)
            {
                _showTick = TickCount;
            }

            DrawClientShell(sprite, TickCount);
            DrawEventBanner(sprite);
            DrawInstructionBalloon(sprite);
            DrawStatusText(sprite);
        }

        private void DrawClientShell(SpriteBatch sprite, int tickCount)
        {
            DrawAnimation(sprite, _statusBeamFrames, tickCount, true, ClientStatusBeamAnchor, false);
            DrawAnimation(sprite, _statusSparkleFrames, tickCount, true, ClientStatusSparkleAnchor, false);
            DrawAnimation(sprite, _statusScrollFrames, tickCount, false, ClientStatusScrollPosition, true);
            DrawAnimation(sprite, _statusAccentFrames, tickCount, true, ClientStatusAccentPosition, false);

            if (_canEnter)
            {
                DrawAnimation(sprite, _enterFocusFrames, tickCount, true, ClientEnterFocusAnchor, false);
            }
        }

        private void DrawStatusText(SpriteBatch sprite)
        {
            if (ShouldDrawStatusScrollText())
            {
                Point statusPosition = new(Position.X + ClientStatusScrollPosition.X + 21, Position.Y + ClientStatusScrollPosition.Y + 79);
                string wrappedMessage = WrapText(_statusMessage, StatusTextWrapWidth, StatusTextScale, 2);
                if (!DrawWrappedRasterText(sprite, wrappedMessage, statusPosition.X, statusPosition.Y, new Color(92, 63, 44)))
                {
                    DrawShadowedText(sprite, wrappedMessage, statusPosition.ToVector2(), new Color(92, 63, 44), StatusTextScale);
                }
            }
        }

        private void DrawEventBanner(SpriteBatch sprite)
        {
            if (_eventBanner.Texture == null)
            {
                return;
            }

            sprite.Draw(
                _eventBanner.Texture,
                new Vector2(
                    Position.X + ClientEventBannerTopLeft.X - _eventBanner.Origin.X,
                    Position.Y + ClientEventBannerTopLeft.Y - _eventBanner.Origin.Y),
                Color.White);
        }

        private void DrawInstructionBalloon(SpriteBatch sprite)
        {
            if (!_instructionBalloonStyle.IsReady)
            {
                return;
            }

            string balloonMessage = ResolveBalloonMessage();
            if (string.IsNullOrWhiteSpace(balloonMessage))
            {
                return;
            }

            Rectangle bodyBounds = ResolveBalloonBounds();
            DrawBalloonNineSlice(sprite, bodyBounds);

            if (_instructionBalloonStyle.SelectionArrow != null)
            {
                float arrowX = ResolveBalloonAnchorX() - (_instructionBalloonStyle.SelectionArrow.Width / 2f);
                float arrowY = bodyBounds.Bottom - 1f;
                sprite.Draw(_instructionBalloonStyle.SelectionArrow, new Vector2(arrowX, arrowY), Color.White);
            }

            string wrappedMessage = WrapText(balloonMessage, BalloonWidth - 20, StatusTextScale, 2);
            int textY = bodyBounds.Y + (wrappedMessage.Contains(Environment.NewLine, StringComparison.Ordinal)
                ? BalloonMultiLineTextInsetY
                : BalloonSingleLineTextInsetY);
            // CUIAvatar::OnCreate draws the balloon copy into a 220x40 canvas at (10, 15).
            DrawWrappedRasterText(sprite, wrappedMessage, bodyBounds.X + BalloonTextInsetX, textY, _instructionBalloonStyle.TextColor);
        }

        private void DrawBalloonNineSlice(SpriteBatch sprite, Rectangle bodyBounds)
        {
            Texture2D center = _instructionBalloonStyle.Center;
            Texture2D north = _instructionBalloonStyle.North;
            Texture2D south = _instructionBalloonStyle.South;
            Texture2D west = _instructionBalloonStyle.West;
            Texture2D east = _instructionBalloonStyle.East;
            Texture2D northWest = _instructionBalloonStyle.NorthWest;
            Texture2D northEast = _instructionBalloonStyle.NorthEast;
            Texture2D southWest = _instructionBalloonStyle.SouthWest;
            Texture2D southEast = _instructionBalloonStyle.SouthEast;

            int leftWidth = northWest.Width;
            int rightWidth = northEast.Width;
            int topHeight = northWest.Height;
            int bottomHeight = southWest.Height;
            int innerWidth = Math.Max(0, bodyBounds.Width - leftWidth - rightWidth);
            int innerHeight = Math.Max(0, bodyBounds.Height - topHeight - bottomHeight);

            sprite.Draw(center, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y + topHeight, innerWidth, innerHeight), Color.White);
            sprite.Draw(north, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Y, innerWidth, topHeight), Color.White);
            sprite.Draw(south, new Rectangle(bodyBounds.X + leftWidth, bodyBounds.Bottom - bottomHeight, innerWidth, bottomHeight), Color.White);
            sprite.Draw(west, new Rectangle(bodyBounds.X, bodyBounds.Y + topHeight, leftWidth, innerHeight), Color.White);
            sprite.Draw(east, new Rectangle(bodyBounds.Right - rightWidth, bodyBounds.Y + topHeight, rightWidth, innerHeight), Color.White);
            sprite.Draw(northWest, new Vector2(bodyBounds.X, bodyBounds.Y), Color.White);
            sprite.Draw(northEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Y), Color.White);
            sprite.Draw(southWest, new Vector2(bodyBounds.X, bodyBounds.Bottom - bottomHeight), Color.White);
            sprite.Draw(southEast, new Vector2(bodyBounds.Right - rightWidth, bodyBounds.Bottom - bottomHeight), Color.White);
        }

        private Rectangle ResolveBalloonBounds()
        {
            int anchorX = ResolveBalloonAnchorX();
            int x = anchorX - (BalloonWidth / 2);
            int minX = Position.X + 4;
            int maxX = Position.X + OwnerWidth - BalloonWidth - 4;
            x = Math.Clamp(x, minX, Math.Max(minX, maxX));
            return new Rectangle(x, Position.Y + 4, BalloonWidth, BalloonHeight);
        }

        private int ResolveBalloonAnchorX()
        {
            int visibleSlotIndex = _selectedIndex >= 0
                ? _selectedIndex - (_pageIndex * EntriesPerPage)
                : -1;
            if (visibleSlotIndex < 0 || visibleSlotIndex >= EntriesPerPage)
            {
                return Position.X + BalloonDefaultAnchor.X;
            }

            return Position.X + CardStartX + (visibleSlotIndex * (CardWidth + CardGap)) + (CardWidth / 2);
        }

        private string ResolveBalloonMessage()
        {
            if (_canEnter)
            {
                return DefaultInstructionMessage;
            }

            return string.IsNullOrWhiteSpace(_statusMessage)
                ? DefaultStatusMessage
                : _statusMessage;
        }

        private bool ShouldDrawStatusScrollText()
        {
            return !string.IsNullOrWhiteSpace(_statusMessage) &&
                   !_canEnter &&
                   !string.Equals(_statusMessage, DefaultStatusMessage, StringComparison.Ordinal);
        }

        private void DrawAnimation(
            SpriteBatch sprite,
            IReadOnlyList<AnimationFrame> frames,
            int tickCount,
            bool loop,
            Point anchor,
            bool useShowTime)
        {
            AnimationFrame frame = ResolveAnimationFrame(frames, tickCount, loop, useShowTime);
            if (frame.Texture == null)
            {
                return;
            }

            Vector2 drawPosition = new(Position.X + anchor.X + frame.Offset.X, Position.Y + anchor.Y + frame.Offset.Y);
            sprite.Draw(frame.Texture, drawPosition, Color.White);
        }

        private AnimationFrame ResolveAnimationFrame(
            IReadOnlyList<AnimationFrame> frames,
            int tickCount,
            bool loop,
            bool useShowTime)
        {
            if (frames == null || frames.Count == 0)
            {
                return default;
            }

            int elapsed = useShowTime && _showTick >= 0
                ? Math.Max(0, tickCount - _showTick)
                : Math.Max(0, tickCount);
            int totalDuration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDuration += Math.Max(1, frames[i].Delay);
            }

            if (totalDuration <= 0)
            {
                return frames[^1];
            }

            int animationTick = loop ? elapsed % totalDuration : Math.Min(elapsed, totalDuration - 1);
            for (int i = 0; i < frames.Count; i++)
            {
                int frameDuration = Math.Max(1, frames[i].Delay);
                if (animationTick < frameDuration)
                {
                    return frames[i];
                }

                animationTick -= frameDuration;
            }

            return frames[^1];
        }

        private void DrawShadowedText(SpriteBatch sprite, string text, Vector2 position, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (DrawRasterText(sprite, text, (int)position.X + 1, (int)position.Y + 1, new Color(32, 24, 16, 180)) &&
                DrawRasterText(sprite, text, (int)position.X, (int)position.Y, color))
            {
                return;
            }

            if (_font == null)
            {
                return;
            }

            Vector2 shadowOffset = new(1f, 1f);
            sprite.DrawString(_font, text, position + shadowOffset, new Color(32, 24, 16, 180), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sprite.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private string WrapText(string text, int maxWidth, float scale, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return text;
            }

            List<string> lines = new();
            StringBuilder currentLine = new();
            for (int i = 0; i < words.Length; i++)
            {
                string candidate = currentLine.Length == 0
                    ? words[i]
                    : $"{currentLine} {words[i]}";
                if (MeasureText(candidate, scale).X <= maxWidth)
                {
                    currentLine.Clear();
                    currentLine.Append(candidate);
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString());
                    currentLine.Clear();
                    currentLine.Append(words[i]);
                }
                else
                {
                    lines.Add(words[i]);
                }

                if (lines.Count >= maxLines)
                {
                    return string.Join(Environment.NewLine, lines);
                }
            }

            if (currentLine.Length > 0 && lines.Count < maxLines)
            {
                lines.Add(currentLine.ToString());
            }

            return string.Join(Environment.NewLine, lines);
        }

        private Vector2 MeasureText(string text, float fallbackScale)
        {
            if (_basicBlackFont != null && !string.IsNullOrEmpty(text))
            {
                SD.SizeF size = _measureGraphics.MeasureString(text, _basicBlackFont, SD.PointF.Empty, SD.StringFormat.GenericTypographic);
                if (size.Width > 0f && size.Height > 0f)
                {
                    return new Vector2((float)Math.Ceiling(size.Width), (float)Math.Ceiling(size.Height));
                }
            }

            return _font == null ? Vector2.Zero : _font.MeasureString(text) * fallbackScale;
        }

        private bool DrawRasterText(SpriteBatch sprite, string text, int x, int y, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            Texture2D texture = GetOrCreateTextTexture(text, color);
            if (texture == null)
            {
                return false;
            }

            sprite.Draw(texture, new Vector2(x, y), Color.White);
            return true;
        }

        private bool DrawWrappedRasterText(SpriteBatch sprite, string text, int x, int y, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            int lineHeight = Math.Max(1, (int)Math.Ceiling(MeasureText("Ay", StatusTextScale).Y));
            bool drewAny = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (DrawRasterText(sprite, lines[i], x, y + (i * lineHeight), color))
                {
                    drewAny = true;
                }
            }

            return drewAny;
        }

        private Texture2D GetOrCreateTextTexture(string text, Color color)
        {
            if (_basicBlackFont == null || _graphicsDevice == null || string.IsNullOrEmpty(text))
            {
                return null;
            }

            TextRenderCacheKey cacheKey = new(text, color);
            if (_textTextureCache.TryGetValue(cacheKey, out Texture2D cachedTexture) &&
                cachedTexture != null &&
                !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            Vector2 size = MeasureText(text, StatusTextScale);
            int width = Math.Max(1, (int)size.X);
            int height = Math.Max(1, (int)size.Y);

            using var bitmap = new SD.Bitmap(width, height);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);
            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            using var brush = new SD.SolidBrush(SD.Color.FromArgb(color.A, color.R, color.G, color.B));
            graphics.DrawString(text, _basicBlackFont, brush, 0f, 0f, SD.StringFormat.GenericTypographic);

            Texture2D texture = bitmap.ToTexture2D(_graphicsDevice);
            _textTextureCache[cacheKey] = texture;
            return texture;
        }

        public readonly struct AnimationFrame
        {
            public AnimationFrame(Texture2D texture, Point offset, int delay)
            {
                Texture = texture;
                Offset = offset;
                Delay = delay;
            }

            public Texture2D Texture { get; }
            public Point Offset { get; }
            public int Delay { get; }
        }

        public readonly struct BalloonStyle
        {
            public BalloonStyle(
                Texture2D northWest,
                Texture2D north,
                Texture2D northEast,
                Texture2D west,
                Texture2D center,
                Texture2D east,
                Texture2D southWest,
                Texture2D south,
                Texture2D southEast,
                Texture2D selectionArrow,
                Color textColor)
            {
                NorthWest = northWest;
                North = north;
                NorthEast = northEast;
                West = west;
                Center = center;
                East = east;
                SouthWest = southWest;
                South = south;
                SouthEast = southEast;
                SelectionArrow = selectionArrow;
                TextColor = textColor;
            }

            public Texture2D NorthWest { get; }
            public Texture2D North { get; }
            public Texture2D NorthEast { get; }
            public Texture2D West { get; }
            public Texture2D Center { get; }
            public Texture2D East { get; }
            public Texture2D SouthWest { get; }
            public Texture2D South { get; }
            public Texture2D SouthEast { get; }
            public Texture2D SelectionArrow { get; }
            public Color TextColor { get; }

            public bool IsReady =>
                NorthWest != null &&
                North != null &&
                NorthEast != null &&
                West != null &&
                Center != null &&
                East != null &&
                SouthWest != null &&
                South != null &&
                SouthEast != null;
        }

        public readonly struct OwnerCanvasFrame
        {
            public OwnerCanvasFrame(Texture2D texture, Point origin)
            {
                Texture = texture;
                Origin = origin;
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
        }

        private readonly struct TextRenderCacheKey : IEquatable<TextRenderCacheKey>
        {
            public TextRenderCacheKey(string text, Color color)
            {
                Text = text ?? string.Empty;
                Color = color.PackedValue;
            }

            public string Text { get; }
            public uint Color { get; }

            public bool Equals(TextRenderCacheKey other)
            {
                return Color == other.Color && string.Equals(Text, other.Text, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TextRenderCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Text, Color);
            }
        }
    }
}
