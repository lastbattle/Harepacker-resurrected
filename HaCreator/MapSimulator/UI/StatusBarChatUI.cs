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
        private const int ChatScrollRecentThresholdMs = 5000;
        private static readonly Point PointNotificationAnchor = new Point(512, 60);
        private static readonly Vector2 ChatTargetLabelPos = new Vector2(17, 7);
        private static readonly Vector2 ChatInputPos = new Vector2(74, 5);
        private static readonly Vector2 ChatWhisperPromptPos = new Vector2(74, -13);

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
                this.Position.X + PointNotificationAnchor.X - origin.X,
                this.Position.Y + PointNotificationAnchor.Y - origin.Y);
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
                        this.Position.X + ChatTargetLabelPos.X + originDelta.X,
                        this.Position.Y + ChatTargetLabelPos.Y + originDelta.Y),
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
                Vector2 textSize = _font.MeasureString(line.Text);
                if (_pixelTexture != null)
                {
                    sprite.Draw(
                        _pixelTexture,
                        new Rectangle(this.Position.X + 2, (int)lineY - 1, (int)textSize.X + 6, (int)textSize.Y + 2),
                        new Color((byte)0, (byte)0, (byte)0, (byte)(150 * alpha)));
                }

                DrawTextWithShadow(sprite, line.Text, new Vector2(this.Position.X + 4, lineY), lineColor);
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
                    new Rectangle(this.Position.X + 4, this.Position.Y + 2, _chatEnterTexture.Width, _chatEnterTexture.Height),
                    Color.White);
            }

            string whisperPrompt = string.IsNullOrWhiteSpace(chatState.WhisperTarget)
                ? string.Empty
                : $"> {chatState.WhisperTarget}";
            if (!string.IsNullOrEmpty(whisperPrompt))
            {
                DrawTextWithShadow(sprite,
                    whisperPrompt,
                    new Vector2(this.Position.X + ChatWhisperPromptPos.X, this.Position.Y + ChatWhisperPromptPos.Y),
                    new Color(255, 170, 255));
            }

            Vector2 inputPos = new Vector2(this.Position.X + ChatInputPos.X, this.Position.Y + ChatInputPos.Y);
            DrawTextWithShadow(sprite, chatState.InputText, inputPos, Color.White);

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
                    ShouldIndentWrappedContinuation(message.ChatLogType)))
                {
                    wrappedLines.Add(new WrappedChatLine
                    {
                        Text = lineText,
                        Color = message.Color,
                        Timestamp = message.Timestamp
                    });
                }
            }

            return wrappedLines;
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, bool indentWrappedContinuation)
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
                string candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (_font.MeasureString(candidate).X <= maxWidth)
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

                if (_font.MeasureString(word).X <= maxWidth)
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
                    string fragmentCandidate = fragment + c.ToString();
                    if (_font.MeasureString(fragmentCandidate).X > maxWidth && fragment.Length > 0)
                    {
                        yield return fragment.ToString();
                        fragment.Clear();
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

        private void DrawTextWithShadow(SpriteBatch sprite, string text, Vector2 position, Color color)
        {
            sprite.DrawString(_font, text, position + new Vector2(1, 1), Color.Black);
            sprite.DrawString(_font, text, position, color);
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
