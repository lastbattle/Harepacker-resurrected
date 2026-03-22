using HaCreator.MapSimulator;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Text;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Chat UI
    /// </summary>
    public class StatusBarChatUI : BaseDXDrawableItem, IUIObjectEvents
    {
        public sealed class StatusBarPointNotificationAnimation
        {
            public Texture2D[] Frames { get; set; } = Array.Empty<Texture2D>();
            public Point[] Origins { get; set; } = Array.Empty<Point>();
            public int[] FrameDelaysMs { get; set; } = Array.Empty<int>();
        }

        public sealed class StatusBarPointNotificationState
        {
            public bool ShowAbilityPointNotification { get; set; }
            public bool ShowSkillPointNotification { get; set; }
        }

        private sealed class WrappedChatLine
        {
            public string Text { get; set; }
            public Color Color { get; set; }
            public int Timestamp { get; set; }
            public int ChatLogType { get; set; }
        }

        public sealed class ChatTargetLabelPlacement
        {
            public Texture2D Texture { get; set; }
            public Point Origin { get; set; }
        }

        private readonly List<UIObject> uiButtons = new List<UIObject>();
        private readonly Dictionary<MapSimulatorChatTargetType, ChatTargetLabelPlacement> _chatTargetLabels =
            new Dictionary<MapSimulatorChatTargetType, ChatTargetLabelPlacement>();

        private Func<MapSimulatorChatRenderState> _chatStateProvider;
        private Func<StatusBarPointNotificationState> _pointNotificationStateProvider;
        private SpriteFont _font;
        private Texture2D _pixelTexture;
        private Texture2D _chatEnterTexture;
        private StatusBarPointNotificationAnimation _abilityPointNotificationAnimation = new StatusBarPointNotificationAnimation();
        private StatusBarPointNotificationAnimation _skillPointNotificationAnimation = new StatusBarPointNotificationAnimation();
        private int _scrollOffset;
        private int _lastWrappedLineCount;
        private int _lastManualScrollTick = int.MinValue;
        private int? _previousScrollWheelValue;

        private const int ChatMessageDisplayTime = 10000;
        private const int ChatMessageFadeTime = 2000;
        private const int ChatMaxVisibleLines = 8;
        private const int ChatLogLineHeight = 14;
        private const int ChatLogWidth = 452;
        private const int ChatWrapIndentSpaces = 5;
        private const int ChatSpecialFirstLineWidthReduction = 38;
        private const int ChatScrollRecentThresholdMs = 5000;
        private Point _pointNotificationAnchor = new Point(512, 60);
        private Vector2 _chatTargetLabelPos = new Vector2(17, 7);
        private Vector2 _chatEnterPos = new Vector2(4, 2);
        private Vector2 _chatInputPos = new Vector2(74, 5);
        private Vector2 _chatWhisperPromptPos = new Vector2(74, -13);

        public Action ToggleChatRequested { get; set; }
        public Action<int> CycleChatTargetRequested { get; set; }
        public Action CharacterInfoRequested { get; set; }
        public Action MemoMailboxRequested { get; set; }

        /// <summary>
        /// Constructor for the status bar chat window
        /// </summary>
        /// <param name="frame"></param>
        public StatusBarChatUI(IDXObject frame, Point setPosition, List<UIObject> otherUI)
            : base(frame, false)
        {
            uiButtons.AddRange(otherUI);
            this.Position = setPosition;
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        public void InitializeButtons()
        {
        }

        public void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public void SetPixelTexture(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture == null && graphicsDevice != null)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }
        }

        public void SetChatEnterTexture(Texture2D chatEnterTexture)
        {
            _chatEnterTexture = chatEnterTexture;
        }

        public void SetChatTargetTextures(
            Dictionary<MapSimulatorChatTargetType, Texture2D> chatTargetTextures,
            Dictionary<MapSimulatorChatTargetType, Point> chatTargetOrigins = null)
        {
            _chatTargetLabels.Clear();
            if (chatTargetTextures == null)
            {
                return;
            }

            foreach (KeyValuePair<MapSimulatorChatTargetType, Texture2D> entry in chatTargetTextures)
            {
                if (entry.Value != null)
                {
                    _chatTargetLabels[entry.Key] = new ChatTargetLabelPlacement
                    {
                        Texture = entry.Value,
                        Origin = chatTargetOrigins != null && chatTargetOrigins.TryGetValue(entry.Key, out Point origin)
                            ? origin
                            : Point.Zero
                    };
                }
            }
        }

        public void SetChatRenderProvider(Func<MapSimulatorChatRenderState> chatStateProvider)
        {
            _chatStateProvider = chatStateProvider;
        }

        public void SetPointNotificationRenderProvider(Func<StatusBarPointNotificationState> pointNotificationStateProvider)
        {
            _pointNotificationStateProvider = pointNotificationStateProvider;
        }

        public void SetPointNotificationAnimations(
            StatusBarPointNotificationAnimation abilityPointAnimation,
            StatusBarPointNotificationAnimation skillPointAnimation)
        {
            _abilityPointNotificationAnimation = abilityPointAnimation ?? new StatusBarPointNotificationAnimation();
            _skillPointNotificationAnimation = skillPointAnimation ?? new StatusBarPointNotificationAnimation();
        }

        public void SetLayoutMetrics(Point frameAnchor, Vector2 chatTargetLabelPos, Vector2 chatEnterPos)
        {
            _pointNotificationAnchor = frameAnchor;
            _chatTargetLabelPos = chatTargetLabelPos;
            _chatEnterPos = chatEnterPos;

            Vector2 inputDelta = new Vector2(70, 3);
            _chatInputPos = _chatEnterPos + inputDelta;
            _chatWhisperPromptPos = _chatEnterPos + new Vector2(inputDelta.X, -15);
        }

        public void BindControls(UIObject chatTargetButton, UIObject chatToggleButton, UIObject scrollUpButton, UIObject scrollDownButton, UIObject characterInfoButton = null, UIObject memoButton = null)
        {
            if (chatTargetButton != null)
            {
                chatTargetButton.ButtonClickReleased += _ => CycleChatTargetRequested?.Invoke(1);
            }

            if (chatToggleButton != null)
            {
                chatToggleButton.ButtonClickReleased += _ => ToggleChatRequested?.Invoke();
            }

            if (scrollUpButton != null)
            {
                scrollUpButton.ButtonClickReleased += _ =>
                {
                    _scrollOffset++;
                    RecordManualScroll();
                };
            }

            if (scrollDownButton != null)
            {
                scrollDownButton.ButtonClickReleased += _ =>
                {
                    _scrollOffset = Math.Max(0, _scrollOffset - 1);
                    RecordManualScroll();
                };
            }

            if (characterInfoButton != null)
            {
                characterInfoButton.ButtonClickReleased += _ => CharacterInfoRequested?.Invoke();
            }

            if (memoButton != null)
            {
                memoButton.ButtonClickReleased += _ => MemoMailboxRequested?.Invoke();
            }
        }

        /// <summary>
        /// Draw
        /// </summary>
        public override void Draw(
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
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                this.Position.X, this.Position.Y, centerX, centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);

            foreach (UIObject uiBtn in uiButtons)
            {
                BaseDXDrawableItem buttonToDraw = uiBtn.GetBaseDXDrawableItemByState();
                int drawRelativeX = -(this.Position.X) - uiBtn.X;
                int drawRelativeY = -(this.Position.Y) - uiBtn.Y;

                buttonToDraw.Draw(sprite, skeletonMeshRenderer,
                    gameTime,
                    drawRelativeX,
                    drawRelativeY,
                    centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }

            DrawChatOverlay(sprite, TickCount);
            DrawPointNotifications(sprite, TickCount);
        }

        private void DrawChatOverlay(SpriteBatch sprite, int tickCount)
        {
            if (_font == null || _chatStateProvider == null)
            {
                return;
            }

            MapSimulatorChatRenderState chatState = _chatStateProvider();
            if (chatState == null)
            {
                return;
            }

            DrawChatTargetLabel(sprite, chatState.TargetType);
            DrawChatMessages(sprite, chatState, tickCount);
            if (chatState.IsActive)
            {
                DrawChatInput(sprite, chatState, tickCount);
            }
        }

        private void DrawPointNotifications(SpriteBatch sprite, int tickCount)
        {
            if (_pointNotificationStateProvider == null)
            {
                return;
            }

            StatusBarPointNotificationState notificationState = _pointNotificationStateProvider();
            if (notificationState == null)
            {
                return;
            }

            if (notificationState.ShowAbilityPointNotification)
            {
                DrawPointNotification(sprite, _abilityPointNotificationAnimation, tickCount);
            }

            if (notificationState.ShowSkillPointNotification)
            {
                DrawPointNotification(sprite, _skillPointNotificationAnimation, tickCount);
            }
        }

        private void DrawPointNotification(
            SpriteBatch sprite,
            StatusBarPointNotificationAnimation animation,
            int tickCount)
        {
            Texture2D frame = ResolvePointNotificationFrame(animation, tickCount, out Point origin);
            if (frame == null)
            {
                return;
            }

            Vector2 drawPosition = new Vector2(
                this.Position.X + _pointNotificationAnchor.X - origin.X,
                this.Position.Y + _pointNotificationAnchor.Y - origin.Y);
            sprite.Draw(frame, drawPosition, Color.White);
        }

        private static Texture2D ResolvePointNotificationFrame(
            StatusBarPointNotificationAnimation animation,
            int tickCount,
            out Point origin)
        {
            origin = Point.Zero;
            if (animation?.Frames == null || animation.Frames.Length == 0)
            {
                return null;
            }

            int totalDuration = 0;
            for (int i = 0; i < animation.Frames.Length; i++)
            {
                totalDuration += ResolveFrameDelay(animation, i);
            }

            if (totalDuration <= 0)
            {
                origin = ResolveAnimationOrigin(animation, 0);
                return animation.Frames[0];
            }

            int animationTick = Math.Abs(tickCount % totalDuration);
            int elapsed = 0;
            for (int i = 0; i < animation.Frames.Length; i++)
            {
                int frameDelay = ResolveFrameDelay(animation, i);
                elapsed += frameDelay;
                if (animationTick < elapsed)
                {
                    origin = ResolveAnimationOrigin(animation, i);
                    return animation.Frames[i];
                }
            }

            int lastFrameIndex = animation.Frames.Length - 1;
            origin = ResolveAnimationOrigin(animation, lastFrameIndex);
            return animation.Frames[lastFrameIndex];
        }

        private static Point ResolveAnimationOrigin(StatusBarPointNotificationAnimation animation, int frameIndex)
        {
            if (animation?.Origins == null || frameIndex < 0 || frameIndex >= animation.Origins.Length)
            {
                return Point.Zero;
            }

            return animation.Origins[frameIndex];
        }

        private static int ResolveFrameDelay(StatusBarPointNotificationAnimation animation, int frameIndex)
        {
            if (animation?.FrameDelaysMs == null || frameIndex < 0 || frameIndex >= animation.FrameDelaysMs.Length)
            {
                return 120;
            }

            return Math.Max(1, animation.FrameDelaysMs[frameIndex]);
        }

        private void DrawChatTargetLabel(SpriteBatch sprite, MapSimulatorChatTargetType targetType)
        {
            if (_chatTargetLabels.TryGetValue(targetType, out ChatTargetLabelPlacement labelPlacement)
                && labelPlacement?.Texture != null)
            {
                Point originDelta = ResolveChatTargetOriginDelta(targetType, labelPlacement.Origin);
                sprite.Draw(labelPlacement.Texture,
                    new Vector2(
                        this.Position.X + _chatTargetLabelPos.X + originDelta.X,
                        this.Position.Y + _chatTargetLabelPos.Y + originDelta.Y),
                    Color.White);
            }
        }

        private void DrawChatMessages(SpriteBatch sprite, MapSimulatorChatRenderState chatState, int tickCount)
        {
            List<WrappedChatLine> wrappedLines = BuildWrappedLines(chatState.Messages);
            AdjustScrollForNewLines(wrappedLines.Count, tickCount);

            int maxScrollOffset = Math.Max(0, wrappedLines.Count - ChatMaxVisibleLines);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScrollOffset);

            int lineIndex = wrappedLines.Count - 1 - _scrollOffset;
            int drawnLines = 0;
            float lineY = this.Position.Y - 16;

            while (lineIndex >= 0 && drawnLines < ChatMaxVisibleLines)
            {
                WrappedChatLine line = wrappedLines[lineIndex];
                int age = tickCount - line.Timestamp;
                float alpha = 1.0f;
                if (!chatState.IsActive && age > ChatMessageDisplayTime - ChatMessageFadeTime)
                {
                    alpha = Math.Max(0f,
                        1.0f - (age - (ChatMessageDisplayTime - ChatMessageFadeTime)) / (float)ChatMessageFadeTime);
                }

                if (alpha <= 0f && !chatState.IsActive)
                {
                    lineIndex--;
                    continue;
                }

                Color lineColor = line.Color * alpha;
                Color backgroundColor = GetClientChatLineBackgroundColor(line.ChatLogType) * alpha;
                Color shadowColor = GetClientChatLineShadowColor(line.ChatLogType) * alpha;
                Vector2 textSize = _font.MeasureString(line.Text);
                if (_pixelTexture != null)
                {
                    sprite.Draw(
                        _pixelTexture,
                        new Rectangle(this.Position.X + 2, (int)lineY - 1, (int)textSize.X + 6, (int)textSize.Y + 2),
                        backgroundColor);
                }

                DrawTextWithShadow(sprite, line.Text, new Vector2(this.Position.X + 4, lineY), lineColor, shadowColor);
                lineY -= ChatLogLineHeight;
                drawnLines++;
                lineIndex--;
            }

            _lastWrappedLineCount = wrappedLines.Count;
        }

        private void AdjustScrollForNewLines(int wrappedLineCount, int tickCount)
        {
            if (wrappedLineCount <= _lastWrappedLineCount)
            {
                return;
            }

            int addedLineCount = wrappedLineCount - _lastWrappedLineCount;
            int maxScrollOffset = Math.Max(0, wrappedLineCount - ChatMaxVisibleLines);
            bool shouldSnapToBottom = maxScrollOffset <= 2
                || _scrollOffset == 0
                || tickCount - _lastManualScrollTick > ChatScrollRecentThresholdMs;

            if (shouldSnapToBottom)
            {
                _scrollOffset = 0;
                return;
            }

            _scrollOffset = Math.Min(maxScrollOffset, _scrollOffset + addedLineCount);
        }

        private void DrawChatInput(SpriteBatch sprite, MapSimulatorChatRenderState chatState, int tickCount)
        {
            if (_chatEnterTexture != null)
            {
                sprite.Draw(_chatEnterTexture,
                    new Rectangle(this.Position.X + (int)_chatEnterPos.X, this.Position.Y + (int)_chatEnterPos.Y, _chatEnterTexture.Width, _chatEnterTexture.Height),
                    Color.White);
            }

            string whisperPrompt = string.IsNullOrWhiteSpace(chatState.WhisperTarget)
                ? string.Empty
                : $"> {chatState.WhisperTarget}";
            if (!string.IsNullOrEmpty(whisperPrompt))
            {
                DrawTextWithShadow(sprite,
                    whisperPrompt,
                    new Vector2(this.Position.X + _chatWhisperPromptPos.X, this.Position.Y + _chatWhisperPromptPos.Y),
                    new Color(255, 170, 255),
                    Color.Black);
            }

            Vector2 inputPos = new Vector2(this.Position.X + _chatInputPos.X, this.Position.Y + _chatInputPos.Y);
            DrawTextWithShadow(sprite, chatState.InputText, inputPos, Color.White, Color.Black);

            if (_pixelTexture == null || ((tickCount / 500) % 2) != 0)
            {
                return;
            }

            string textBeforeCursor = chatState.InputText.Substring(
                0,
                Math.Clamp(chatState.CursorPosition, 0, chatState.InputText.Length));
            float cursorX = inputPos.X + _font.MeasureString(textBeforeCursor).X;
            sprite.Draw(_pixelTexture,
                new Rectangle((int)cursorX, (int)inputPos.Y, 1, _font.LineSpacing - 1),
                Color.White);
        }

        private List<WrappedChatLine> BuildWrappedLines(IReadOnlyList<ChatMessage> messages)
        {
            var wrappedLines = new List<WrappedChatLine>();
            if (messages == null)
            {
                return wrappedLines;
            }

            foreach (ChatMessage message in messages)
            {
                foreach (string lineText in WrapText(
                    message.Text ?? string.Empty,
                    ChatLogWidth,
                    message.ChatLogType,
                    ShouldIndentWrappedContinuation(message.ChatLogType)))
                {
                    wrappedLines.Add(new WrappedChatLine
                    {
                        Text = lineText,
                        Color = message.Color,
                        Timestamp = message.Timestamp,
                        ChatLogType = message.ChatLogType
                    });
                }
            }

            return wrappedLines;
        }

        private IEnumerable<string> WrapText(
            string text,
            float maxWidth,
            int chatLogType,
            bool indentWrappedContinuation)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield return string.Empty;
                yield break;
            }

            string[] words = text.Split(' ');
            StringBuilder currentLine = new StringBuilder();
            string continuationIndent = indentWrappedContinuation ? new string(' ', ChatWrapIndentSpaces) : string.Empty;
            bool isFirstLine = true;
            foreach (string word in words)
            {
                float currentMaxWidth = ResolveLineMaxWidth(maxWidth, chatLogType, isFirstLine);
                string candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (_font.MeasureString(candidate).X <= currentMaxWidth)
                {
                    currentLine.Clear();
                    currentLine.Append(candidate);
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    yield return currentLine.ToString();
                    currentLine.Clear();
                    isFirstLine = false;
                }

                currentMaxWidth = ResolveLineMaxWidth(maxWidth, chatLogType, isFirstLine);
                if (_font.MeasureString(word).X <= currentMaxWidth)
                {
                    if (!isFirstLine)
                    {
                        currentLine.Append(continuationIndent);
                    }
                    currentLine.Append(word);
                    continue;
                }

                StringBuilder fragment = new StringBuilder();
                foreach (char c in word)
                {
                    currentMaxWidth = ResolveLineMaxWidth(maxWidth, chatLogType, isFirstLine);
                    string fragmentCandidate = fragment + c.ToString();
                    if (_font.MeasureString(fragmentCandidate).X > currentMaxWidth && fragment.Length > 0)
                    {
                        yield return fragment.ToString();
                        fragment.Clear();
                        isFirstLine = false;
                    }

                    fragment.Append(c);
                }

                if (fragment.Length > 0)
                {
                    if (!isFirstLine)
                    {
                        currentLine.Append(continuationIndent);
                    }
                    currentLine.Append(fragment);
                }
            }

            if (currentLine.Length > 0)
            {
                yield return currentLine.ToString();
            }
        }

        private void DrawTextWithShadow(SpriteBatch sprite, string text, Vector2 position, Color color, Color shadowColor)
        {
            sprite.DrawString(_font, text, position + new Vector2(1, 1), shadowColor);
            sprite.DrawString(_font, text, position, color);
        }

        private static float ResolveLineMaxWidth(float maxWidth, int chatLogType, bool isFirstLine)
        {
            if (isFirstLine && RequiresReducedFirstLineWidth(chatLogType))
            {
                return Math.Max(1f, maxWidth - ChatSpecialFirstLineWidthReduction);
            }

            return maxWidth;
        }

        private Point ResolveChatTargetOriginDelta(MapSimulatorChatTargetType targetType, Point targetOrigin)
        {
            if (!_chatTargetLabels.TryGetValue(MapSimulatorChatTargetType.All, out ChatTargetLabelPlacement allLabel)
                || allLabel == null)
            {
                return Point.Zero;
            }

            if (targetType == MapSimulatorChatTargetType.All)
            {
                return Point.Zero;
            }

            return new Point(
                targetOrigin.X - allLabel.Origin.X,
                targetOrigin.Y - allLabel.Origin.Y);
        }

        private static bool ShouldIndentWrappedContinuation(int chatLogType)
        {
            return chatLogType < 7 || chatLogType > 12;
        }

        private static bool RequiresReducedFirstLineWidth(int chatLogType)
        {
            return chatLogType == 14
                || chatLogType == 16
                || chatLogType == 19
                || chatLogType == 20;
        }

        private static Color GetClientChatLineBackgroundColor(int chatLogType)
        {
            return chatLogType switch
            {
                11 => new Color(0, 0, 0, 176),
                13 => new Color(202, 231, 255, 176),
                14 => new Color(255, 191, 221, 204),
                15 => new Color(247, 75, 75, 255),
                16 or 21 => new Color(255, 198, 0, 221),
                18 => new Color(77, 26, 173, 44),
                19 => new Color(255, 92, 89, 128),
                20 => new Color(255, 92, 89, 128),
                22 or 23 => new Color(153, 204, 51, 255),
                _ => new Color(0, 0, 0, 150)
            };
        }

        private static Color GetClientChatLineShadowColor(int chatLogType)
        {
            return chatLogType switch
            {
                11 => new Color(255, 255, 255, 176),
                13 => new Color(202, 231, 255, 176),
                14 => new Color(255, 191, 221, 204),
                15 => new Color(247, 75, 75, 255),
                16 or 21 => new Color(255, 198, 0, 221),
                18 => new Color(77, 26, 173, 44),
                19 or 20 => new Color(255, 92, 89, 128),
                22 or 23 => new Color(153, 204, 51, 255),
                _ => Color.Black
            };
        }

        #region IClickableUIObject
        private Point? mouseOffsetOnDragStart = null;

        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            HandleMouseWheel(mouseState);

            return UIMouseEventHandler.CheckMouseEvent(
                shiftCenteredX,
                shiftCenteredY,
                this.Position.X,
                this.Position.Y,
                mouseState,
                mouseCursor,
                uiButtons,
                false);
        }

        private void HandleMouseWheel(MouseState mouseState)
        {
            if (_previousScrollWheelValue == null)
            {
                _previousScrollWheelValue = mouseState.ScrollWheelValue;
                return;
            }

            int scrollDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue.Value;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            if (scrollDelta == 0)
            {
                return;
            }

            if (!GetChatInteractionBounds().Contains(mouseState.X, mouseState.Y))
            {
                return;
            }

            int steps = Math.Max(1, Math.Abs(scrollDelta) / 120);
            if (scrollDelta > 0)
            {
                _scrollOffset += steps;
            }
            else
            {
                _scrollOffset = Math.Max(0, _scrollOffset - steps);
            }

            RecordManualScroll();
        }

        private void RecordManualScroll()
        {
            _lastManualScrollTick = Environment.TickCount;
        }

        private Rectangle GetChatInteractionBounds()
        {
            return new Rectangle(
                this.Position.X,
                this.Position.Y - (ChatMaxVisibleLines * ChatLogLineHeight) - 18,
                ChatLogWidth + 18,
                (ChatMaxVisibleLines * ChatLogLineHeight) + 42);
        }
        #endregion
    }
}
