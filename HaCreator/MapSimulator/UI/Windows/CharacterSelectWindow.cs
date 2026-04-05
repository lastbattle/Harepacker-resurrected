using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SD = System.Drawing;
using SDText = System.Drawing.Text;
using SWF = System.Windows.Forms;

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
        private const int StatusScrollCanvasWidth = 217;
        private const int StatusScrollCanvasHeight = 161;
        private const int StatusScrollTextInsetX = 21;
        private const int StatusScrollTextInsetY = 79;
        private const int StatusTextWrapWidth = 170;
        private const float StatusTextScale = 0.50f;
        private const int BasicBlackFontHeight = 12;
        private const byte KoreanGdiCharset = 129;
        private const string BasicBlackFontPathEnvironmentVariable = "MAPSIM_FONT_BASIC_BLACK_PATH";
        private const string BasicBlackFontFaceEnvironmentVariable = "MAPSIM_FONT_BASIC_BLACK_FACE";
        private const string DefaultInstructionMessage = "Double-click to enter.";
        private const string DefaultStatusMessage = "Select a character.";
        private static readonly SWF.TextFormatFlags BasicBlackTextFormatFlags =
            SWF.TextFormatFlags.NoPadding |
            SWF.TextFormatFlags.NoPrefix |
            SWF.TextFormatFlags.PreserveGraphicsClipping |
            SWF.TextFormatFlags.PreserveGraphicsTranslateTransform;
        private static readonly string[] BasicBlackFontFamilyCandidates =
        {
            "DotumChe",
            "Dotum",
            "・駆它・ｴ",
            "・駆它",
            "GulimChe",
            "Gulim",
            "・ｴ・ｼ・ｴ",
            "・ｴ・ｼ",
            "Tahoma",
            SD.SystemFonts.MessageBoxFont?.FontFamily?.Name,
            SD.FontFamily.GenericSansSerif.Name
        };

        private readonly UIObject _enterButton;
        private readonly UIObject _newButton;
        private readonly UIObject _deleteButton;
        private readonly IReadOnlyList<IReadOnlyList<AnimationFrame>> _statusScrollFrameSets;
        private readonly IReadOnlyList<AnimationFrame> _statusSparkleFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusBeamFrames;
        private readonly IReadOnlyList<AnimationFrame> _statusAccentFrames;
        private readonly IReadOnlyList<AnimationFrame> _selectFocusFrames;
        private readonly IReadOnlyList<AnimationFrame> _newFocusFrames;
        private readonly IReadOnlyList<AnimationFrame> _deleteFocusFrames;
        private readonly BalloonStyle _instructionBalloonStyle;
        private readonly OwnerCanvasFrame _eventBanner;
        private readonly OwnerCanvasFrame _statusCharacterFrame;
        private readonly Dictionary<TextRenderCacheKey, Texture2D> _textTextureCache = new();
        private readonly SD.Bitmap _measureBitmap;
        private readonly SD.Graphics _measureGraphics;
        private readonly SD.Font _basicBlackFont;
        private readonly string _basicBlackFontFamilyName;
        private readonly GraphicsDevice _graphicsDevice;
        private SpriteFont _font;

        private string _statusMessage = "Select a character.";
        private bool _canEnter;
        private bool _hasKeyboardFocus;
        private int _showTick = -1;
        private int _selectedIndex = -1;
        private int _pageIndex;
        private KeyboardState _previousKeyboardState;
        private FocusedButton _focusedButton = FocusedButton.Select;

        public CharacterSelectWindow(
            IDXObject frame,
            UIObject enterButton,
            UIObject newButton,
            UIObject deleteButton,
            IReadOnlyList<IReadOnlyList<AnimationFrame>> statusScrollFrameSets,
            IReadOnlyList<AnimationFrame> statusSparkleFrames,
            IReadOnlyList<AnimationFrame> statusBeamFrames,
            IReadOnlyList<AnimationFrame> statusAccentFrames,
            IReadOnlyList<AnimationFrame> selectFocusFrames,
            IReadOnlyList<AnimationFrame> newFocusFrames,
            IReadOnlyList<AnimationFrame> deleteFocusFrames,
            BalloonStyle instructionBalloonStyle,
            OwnerCanvasFrame eventBanner,
            OwnerCanvasFrame statusCharacterFrame)
            : base(frame)
        {
            _enterButton = enterButton;
            _newButton = newButton;
            _deleteButton = deleteButton;
            _statusScrollFrameSets = statusScrollFrameSets ?? Array.Empty<IReadOnlyList<AnimationFrame>>();
            _statusSparkleFrames = statusSparkleFrames ?? Array.Empty<AnimationFrame>();
            _statusBeamFrames = statusBeamFrames ?? Array.Empty<AnimationFrame>();
            _statusAccentFrames = statusAccentFrames ?? Array.Empty<AnimationFrame>();
            _selectFocusFrames = selectFocusFrames ?? Array.Empty<AnimationFrame>();
            _newFocusFrames = newFocusFrames ?? Array.Empty<AnimationFrame>();
            _deleteFocusFrames = deleteFocusFrames ?? Array.Empty<AnimationFrame>();
            _instructionBalloonStyle = instructionBalloonStyle;
            _eventBanner = eventBanner;
            _statusCharacterFrame = statusCharacterFrame;
            _graphicsDevice = frame?.Texture?.GraphicsDevice
                ?? _instructionBalloonStyle.Center?.GraphicsDevice
                ?? _eventBanner.Texture?.GraphicsDevice;
            _measureBitmap = new SD.Bitmap(1, 1);
            _measureGraphics = SD.Graphics.FromImage(_measureBitmap);
            _measureGraphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            _basicBlackFont = CreateBasicBlackFont(out _basicBlackFontFamilyName);

            if (_enterButton != null)
            {
                _enterButton.ButtonClickReleased += _ =>
                {
                    RequestKeyboardFocus(FocusedButton.Select);
                    EnterRequested?.Invoke();
                };
                AddButton(_enterButton);
            }

            if (_newButton != null)
            {
                _newButton.ButtonClickReleased += _ =>
                {
                    RequestKeyboardFocus(FocusedButton.New);
                    NewCharacterRequested?.Invoke();
                };
                AddButton(_newButton);
            }

            if (_deleteButton != null)
            {
                _deleteButton.ButtonClickReleased += _ =>
                {
                    RequestKeyboardFocus(FocusedButton.Delete);
                    DeleteRequested?.Invoke();
                };
                AddButton(_deleteButton);
            }
        }

        public override string WindowName => MapSimulatorWindowNames.CharacterSelect;
        public override bool CapturesKeyboardInput => IsVisible && _hasKeyboardFocus;

        public event Action<int> CharacterSelected;
        public event Action EnterRequested;
        public event Action NewCharacterRequested;
        public event Action DeleteRequested;
        public event Action AvatarPreviewFocusRequested;
        public event Action CancelRequested;

        public void NotifyCharacterSelected(int rowIndex)
        {
            CharacterSelected?.Invoke(rowIndex);
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
            base.SetFont(font);
        }

        public override bool SupportsDragging => false;

        public override void Show()
        {
            bool wasVisible = IsVisible;
            base.Show();
            if (!wasVisible)
            {
                _showTick = -1;
                _previousKeyboardState = Keyboard.GetState();
            }
        }

        public void SetKeyboardFocus(bool hasKeyboardFocus)
        {
            _hasKeyboardFocus = hasKeyboardFocus;
            _previousKeyboardState = Keyboard.GetState();
            if (_focusedButton == FocusedButton.Select && !_canEnter)
            {
                _focusedButton = ResolveFirstAvailableButton();
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
            if (_focusedButton == FocusedButton.Select && !canEnter)
            {
                _focusedButton = ResolveFirstAvailableButton();
            }
            else if (_focusedButton == FocusedButton.Delete && !canDelete)
            {
                _focusedButton = ResolveFirstAvailableButton();
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            KeyboardState keyboardState = Keyboard.GetState();
            if (!IsVisible || !_hasKeyboardFocus)
            {
                _previousKeyboardState = keyboardState;
                return;
            }

            if (Pressed(keyboardState, Keys.Tab))
            {
                _hasKeyboardFocus = false;
                AvatarPreviewFocusRequested?.Invoke();
            }
            else if (Pressed(keyboardState, Keys.Up))
            {
                MoveFocusedButton(-1);
            }
            else if (Pressed(keyboardState, Keys.Down))
            {
                MoveFocusedButton(1);
            }
            else if (Pressed(keyboardState, Keys.Enter))
            {
                ActivateFocusedButton();
            }
            else if (Pressed(keyboardState, Keys.Escape))
            {
                CancelRequested?.Invoke();
            }

            _previousKeyboardState = keyboardState;
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
            DrawAnimation(sprite, ResolveStatusScrollFrames(), tickCount, false, ClientStatusScrollPosition, true);
            DrawOwnerCanvasFrame(sprite, _statusCharacterFrame, ClientStatusAccentPosition);
            DrawAnimation(sprite, _statusAccentFrames, tickCount, true, ClientStatusAccentPosition, false);

            DrawFocusedButtonAnimation(sprite, tickCount);
        }

        private IReadOnlyList<AnimationFrame> ResolveStatusScrollFrames()
        {
            if (_statusScrollFrameSets == null || _statusScrollFrameSets.Count == 0)
            {
                return Array.Empty<AnimationFrame>();
            }

            int frameSetCount = _statusScrollFrameSets.Count;
            if (frameSetCount == 1)
            {
                return _statusScrollFrameSets[0] ?? Array.Empty<AnimationFrame>();
            }

            int requestedIndex = Math.Abs(_pageIndex) % frameSetCount;
            IReadOnlyList<AnimationFrame> selectedFrames = _statusScrollFrameSets[requestedIndex];
            if (selectedFrames != null && selectedFrames.Count > 0)
            {
                return selectedFrames;
            }

            for (int i = 0; i < frameSetCount; i++)
            {
                IReadOnlyList<AnimationFrame> fallback = _statusScrollFrameSets[i];
                if (fallback != null && fallback.Count > 0)
                {
                    return fallback;
                }
            }

            return Array.Empty<AnimationFrame>();
        }

        private void DrawOwnerCanvasFrame(SpriteBatch sprite, OwnerCanvasFrame frame, Point anchor)
        {
            if (frame.Texture == null)
            {
                return;
            }

            sprite.Draw(
                frame.Texture,
                new Vector2(
                    Position.X + anchor.X - frame.Origin.X,
                    Position.Y + anchor.Y - frame.Origin.Y),
                Color.White);
        }

        private void DrawStatusText(SpriteBatch sprite)
        {
            if (ShouldDrawStatusScrollText())
            {
                Rectangle statusBounds = new(
                    Position.X + ClientStatusScrollPosition.X,
                    Position.Y + ClientStatusScrollPosition.Y,
                    StatusScrollCanvasWidth,
                    StatusScrollCanvasHeight);
                string wrappedMessage = WrapText(_statusMessage, StatusTextWrapWidth, StatusTextScale, 2);
                DrawCanvasText(
                    sprite,
                    wrappedMessage,
                    statusBounds,
                    StatusScrollTextInsetX,
                    StatusScrollTextInsetY,
                    new Color(92, 63, 44));
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

            if (_instructionBalloonStyle.SelectionArrow.Texture != null)
            {
                Vector2 arrowAnchor = new(
                    Position.X + BalloonDefaultAnchor.X,
                    bodyBounds.Bottom - 1f);
                sprite.Draw(
                    _instructionBalloonStyle.SelectionArrow.Texture,
                    new Vector2(
                        arrowAnchor.X - _instructionBalloonStyle.SelectionArrow.Origin.X,
                        arrowAnchor.Y - _instructionBalloonStyle.SelectionArrow.Origin.Y),
                    Color.White);
            }

            string wrappedMessage = WrapText(balloonMessage, BalloonWidth - 20, StatusTextScale, 2);
            int textY = bodyBounds.Y + (wrappedMessage.Contains(Environment.NewLine, StringComparison.Ordinal)
                ? BalloonMultiLineTextInsetY
                : BalloonSingleLineTextInsetY);
            // CUIAvatar::OnCreate draws the balloon copy into a dedicated 220x40 canvas before presenting the layer.
            DrawCanvasText(
                sprite,
                wrappedMessage,
                bodyBounds,
                BalloonTextInsetX,
                textY - bodyBounds.Y,
                _instructionBalloonStyle.TextColor);
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
            int x = Position.X + BalloonDefaultAnchor.X - (BalloonWidth / 2);
            return new Rectangle(x, Position.Y + 4, BalloonWidth, BalloonHeight);
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

        private void DrawFocusedButtonAnimation(SpriteBatch sprite, int tickCount)
        {
            if (!_hasKeyboardFocus)
            {
                return;
            }

            switch (_focusedButton)
            {
                case FocusedButton.Select when _canEnter:
                    DrawAnimation(sprite, _selectFocusFrames, tickCount, true, ClientEnterFocusAnchor, false);
                    break;
                case FocusedButton.New:
                    DrawAnimation(sprite, _newFocusFrames, tickCount, true, new Point(_newButton?.X ?? 0, _newButton?.Y ?? 0), false);
                    break;
                case FocusedButton.Delete when _deleteButton?.Enabled != false:
                    DrawAnimation(sprite, _deleteFocusFrames, tickCount, true, new Point(_deleteButton?.X ?? 0, _deleteButton?.Y ?? 0), false);
                    break;
            }
        }

        private void RequestKeyboardFocus(FocusedButton focusedButton)
        {
            _hasKeyboardFocus = true;
            _focusedButton = focusedButton;
            _previousKeyboardState = Keyboard.GetState();
        }

        private void MoveFocusedButton(int direction)
        {
            FocusedButton[] order = { FocusedButton.Select, FocusedButton.New, FocusedButton.Delete };
            int currentIndex = Array.IndexOf(order, _focusedButton);
            if (currentIndex < 0)
            {
                _focusedButton = ResolveFirstAvailableButton();
                return;
            }

            for (int offset = 1; offset <= order.Length; offset++)
            {
                int candidateIndex = (currentIndex + (direction * offset) + order.Length * 2) % order.Length;
                FocusedButton candidate = order[candidateIndex];
                if (IsButtonAvailable(candidate))
                {
                    _focusedButton = candidate;
                    return;
                }
            }
        }

        private void ActivateFocusedButton()
        {
            switch (_focusedButton)
            {
                case FocusedButton.Select when _canEnter:
                    EnterRequested?.Invoke();
                    break;
                case FocusedButton.New:
                    NewCharacterRequested?.Invoke();
                    break;
                case FocusedButton.Delete when _deleteButton?.Enabled != false:
                    DeleteRequested?.Invoke();
                    break;
            }
        }

        private FocusedButton ResolveFirstAvailableButton()
        {
            if (_canEnter)
            {
                return FocusedButton.Select;
            }

            if (IsButtonAvailable(FocusedButton.New))
            {
                return FocusedButton.New;
            }

            if (IsButtonAvailable(FocusedButton.Delete))
            {
                return FocusedButton.Delete;
            }

            return FocusedButton.Select;
        }

        private bool IsButtonAvailable(FocusedButton button)
        {
            return button switch
            {
                FocusedButton.Select => _canEnter,
                FocusedButton.New => _newButton?.Enabled != false,
                FocusedButton.Delete => _deleteButton?.Enabled != false,
                _ => false
            };
        }

        private bool Pressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
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

            ClientTextDrawing.DrawShadowed(sprite, text, position, color, _font, scale);
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
                SWF.TextFormatFlags formatFlags = BasicBlackTextFormatFlags;
                if (!text.Contains(Environment.NewLine, StringComparison.Ordinal))
                {
                    formatFlags |= SWF.TextFormatFlags.SingleLine;
                }

                SD.Size size = SWF.TextRenderer.MeasureText(
                    _measureGraphics,
                    text,
                    _basicBlackFont,
                    new SD.Size(int.MaxValue, int.MaxValue),
                    formatFlags);
                if (size.Width > 0 && size.Height > 0)
                {
                    return new Vector2(size.Width, size.Height);
                }
            }

            return new Vector2(0f, Math.Max(1f, BasicBlackFontHeight * fallbackScale));
        }

        private bool DrawCanvasText(SpriteBatch sprite, string text, Rectangle canvasBounds, int insetX, int insetY, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            Texture2D texture = GetOrCreateCanvasTextTexture(text, color, canvasBounds.Width, canvasBounds.Height, insetX, insetY);
            if (texture == null)
            {
                return false;
            }

            sprite.Draw(texture, new Vector2(canvasBounds.X, canvasBounds.Y), Color.White);
            return true;
        }

        private bool DrawRasterText(SpriteBatch sprite, string text, int x, int y, Color color)
        {
            return false;
        }

        private Texture2D GetOrCreateCanvasTextTexture(string text, Color color, int canvasWidth, int canvasHeight, int insetX, int insetY)
        {
            if (_basicBlackFont == null ||
                _graphicsDevice == null ||
                string.IsNullOrEmpty(text) ||
                canvasWidth <= 0 ||
                canvasHeight <= 0)
            {
                return null;
            }

            TextRenderCacheKey cacheKey = new(text, color, canvasWidth, canvasHeight, insetX, insetY);
            if (_textTextureCache.TryGetValue(cacheKey, out Texture2D cachedTexture) &&
                cachedTexture != null &&
                !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            using var bitmap = new SD.Bitmap(canvasWidth, canvasHeight);
            using SD.Graphics graphics = SD.Graphics.FromImage(bitmap);
            graphics.Clear(SD.Color.Transparent);
            graphics.TextRenderingHint = SDText.TextRenderingHint.SingleBitPerPixelGridFit;
            SWF.TextFormatFlags formatFlags = BasicBlackTextFormatFlags;
            if (!text.Contains(Environment.NewLine, StringComparison.Ordinal))
            {
                formatFlags |= SWF.TextFormatFlags.SingleLine;
            }

            SWF.TextRenderer.DrawText(
                graphics,
                text,
                _basicBlackFont,
                new SD.Rectangle(insetX, insetY, Math.Max(1, canvasWidth - insetX), Math.Max(1, canvasHeight - insetY)),
                SD.Color.FromArgb(color.A, color.R, color.G, color.B),
                SD.Color.Transparent,
                formatFlags);

            Texture2D texture = bitmap.ToTexture2D(_graphicsDevice);
            _textTextureCache[cacheKey] = texture;
            return texture;
        }

        private static SD.Font CreateBasicBlackFont(out string fontFamilyName)
        {
            if (TryCreateConfiguredBasicBlackFont(out SD.Font configuredFont, out string configuredFamilyName))
            {
                fontFamilyName = configuredFamilyName;
                return configuredFont;
            }

            string requestedFamily = ResolveInstalledFontFamilyName(BasicBlackFontFamilyCandidates);
            string resolvedFamily = ClientTextRasterizer.ResolvePreferredFontFamily(
                requestedFamily,
                BasicBlackFontPathEnvironmentVariable,
                BasicBlackFontFaceEnvironmentVariable,
                BasicBlackFontFamilyCandidates);
            SD.Font font = ClientTextRasterizer.CreateClientFont(
                BasicBlackFontHeight,
                SD.FontStyle.Regular,
                resolvedFamily,
                BasicBlackFontPathEnvironmentVariable,
                BasicBlackFontFaceEnvironmentVariable,
                BasicBlackFontFamilyCandidates);
            fontFamilyName = font.FontFamily?.Name ?? resolvedFamily;
            return font;
        }

        private static bool TryCreateConfiguredBasicBlackFont(out SD.Font font, out string fontFamilyName)
        {
            font = null;
            fontFamilyName = null;

            string configuredFontPath = Environment.GetEnvironmentVariable(BasicBlackFontPathEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredFontPath))
            {
                return false;
            }

            string resolvedFontPath = Path.GetFullPath(configuredFontPath.Trim());
            if (!File.Exists(resolvedFontPath))
            {
                return false;
            }

            string configuredFontFace = Environment.GetEnvironmentVariable(BasicBlackFontFaceEnvironmentVariable);
            if (!BasicBlackPrivateFontRegistry.TryRegister(resolvedFontPath, configuredFontFace, out string resolvedFamilyName))
            {
                return false;
            }

            fontFamilyName = resolvedFamilyName;

            try
            {
                font = new SD.Font(
                    resolvedFamilyName,
                    BasicBlackFontHeight,
                    SD.FontStyle.Regular,
                    SD.GraphicsUnit.Pixel,
                    KoreanGdiCharset);
                return true;
            }
            catch (ArgumentException)
            {
                font = new SD.Font(resolvedFamilyName, BasicBlackFontHeight, SD.FontStyle.Regular, SD.GraphicsUnit.Pixel);
                return true;
            }
        }

        private static string ResolveInstalledFontFamilyName(params string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
            {
                return SD.FontFamily.GenericSansSerif.Name;
            }

            HashSet<string> installedFamilies = new(
                SD.FontFamily.Families.Select(static family => family.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && installedFamilies.Contains(candidate))
                {
                    return candidate;
                }
            }

            return SD.FontFamily.GenericSansSerif.Name;
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
                OwnerCanvasFrame selectionArrow,
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
            public OwnerCanvasFrame SelectionArrow { get; }
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

        private enum FocusedButton
        {
            Select,
            New,
            Delete
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
            public TextRenderCacheKey(string text, Color color, int width, int height, int insetX, int insetY)
            {
                Text = text ?? string.Empty;
                Color = color.PackedValue;
                Width = width;
                Height = height;
                InsetX = insetX;
                InsetY = insetY;
            }

            public string Text { get; }
            public uint Color { get; }
            public int Width { get; }
            public int Height { get; }
            public int InsetX { get; }
            public int InsetY { get; }

            public bool Equals(TextRenderCacheKey other)
            {
                return Color == other.Color &&
                    Width == other.Width &&
                    Height == other.Height &&
                    InsetX == other.InsetX &&
                    InsetY == other.InsetY &&
                    string.Equals(Text, other.Text, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TextRenderCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Text, Color, Width, Height, InsetX, InsetY);
            }
        }

        private static class BasicBlackPrivateFontRegistry
        {
            private static readonly object Sync = new();
            private static readonly Dictionary<string, RegisteredPrivateFont> RegisteredFonts = new(StringComparer.OrdinalIgnoreCase);

            public static bool TryRegister(string fontPath, string preferredFamilyName, out string resolvedFamilyName)
            {
                lock (Sync)
                {
                    if (RegisteredFonts.TryGetValue(fontPath, out RegisteredPrivateFont cachedFont))
                    {
                        resolvedFamilyName = cachedFont.ResolveFamilyName(preferredFamilyName);
                        return !string.IsNullOrWhiteSpace(resolvedFamilyName);
                    }

                    try
                    {
                        byte[] fontBytes = File.ReadAllBytes(fontPath);
                        if (fontBytes.Length == 0)
                        {
                            resolvedFamilyName = null;
                            return false;
                        }

                        IntPtr fontData = Marshal.AllocCoTaskMem(fontBytes.Length);
                        Marshal.Copy(fontBytes, 0, fontData, fontBytes.Length);

                        var privateFonts = new SDText.PrivateFontCollection();
                        privateFonts.AddMemoryFont(fontData, fontBytes.Length);

                        uint fontsAdded = 0;
                        IntPtr gdiHandle = AddFontMemResourceEx(fontData, (uint)fontBytes.Length, IntPtr.Zero, ref fontsAdded);
                        RegisteredPrivateFont registeredFont = new(fontData, privateFonts, gdiHandle);
                        RegisteredFonts[fontPath] = registeredFont;

                        resolvedFamilyName = registeredFont.ResolveFamilyName(preferredFamilyName);
                        return !string.IsNullOrWhiteSpace(resolvedFamilyName);
                    }
                    catch
                    {
                        resolvedFamilyName = null;
                        return false;
                    }
                }
            }

            [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);
        }

        private sealed class RegisteredPrivateFont
        {
            public RegisteredPrivateFont(IntPtr fontData, SDText.PrivateFontCollection collection, IntPtr gdiHandle)
            {
                FontData = fontData;
                Collection = collection;
                GdiHandle = gdiHandle;
            }

            private IntPtr FontData { get; }
            private SDText.PrivateFontCollection Collection { get; }
            private IntPtr GdiHandle { get; }

            public string ResolveFamilyName(string preferredFamilyName)
            {
                if (!string.IsNullOrWhiteSpace(preferredFamilyName))
                {
                    SD.FontFamily preferredFamily = Collection.Families.FirstOrDefault(
                        family => string.Equals(family.Name, preferredFamilyName, StringComparison.OrdinalIgnoreCase));
                    if (preferredFamily != null)
                    {
                        return preferredFamily.Name;
                    }
                }

                return Collection.Families.FirstOrDefault()?.Name;
            }
        }
    }
}
