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
            public int ChannelId { get; set; }
            public bool IsFirstWrappedLine { get; set; }
            public string WhisperTargetCandidate { get; set; }
            public string WhisperTargetDisplayText { get; set; }
            public bool CanBeginWhisper { get; set; }
        }

        private sealed class WhisperTargetHitRegion
        {
            public Rectangle Bounds { get; set; }
            public string WhisperTarget { get; set; }
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
        private UIObject _chatOpenButton;
        private UIObject _chatCloseButton;
        private Func<StatusBarPointNotificationState> _pointNotificationStateProvider;
        private SpriteFont _font;
        private Texture2D _pixelTexture;
        private ClientTextRasterizer _clientTextRasterizer;
        private Texture2D _chatEnterTexture;
        private StatusBarPointNotificationAnimation _abilityPointNotificationAnimation = new StatusBarPointNotificationAnimation();
        private StatusBarPointNotificationAnimation _skillPointNotificationAnimation = new StatusBarPointNotificationAnimation();
        private int _scrollOffset;
        private int _lastWrappedLineCount;
        private int _lastManualScrollTick = int.MinValue;
        private int? _previousScrollWheelValue;
        private ButtonState _previousLeftButtonState = ButtonState.Released;
        private string _pressedWhisperTarget;
        private readonly List<WhisperTargetHitRegion> _whisperTargetHitRegions = new List<WhisperTargetHitRegion>();

        private const int ChatMessageDisplayTime = 10000;
        private const int ChatMessageFadeTime = 2000;
        private const int ChatMaxVisibleLines = 8;
        private const int DefaultChatLogLineHeight = 14;
        private const int DefaultChatCursorHeight = 13;
        private const int DefaultChatWhisperPromptGap = 15;
        private const int DefaultChatLogToEnterGap = 4;
        private const int DefaultChatInputRightPadding = 3;
        private const int ChatWrapIndentSpaces = 5;
        private const int ChatSpecialFirstLineWidthReduction = 38;
        private const int ChatScrollRecentThresholdMs = 5000;
        private Point _pointNotificationAnchor = new Point(512, 60);
        private Vector2 _chatTargetLabelPos = new Vector2(17, 7);
        private Vector2 _chatEnterPos = new Vector2(4, 2);
        private Vector2 _chatInputBasePos = new Vector2(74, 5);
        private Vector2 _chatInputPos = new Vector2(74, 5);
        private Vector2 _chatWhisperPromptPos = new Vector2(74, -13);
        private Vector2 _chatLogTextBasePos = new Vector2(4, -16);
        private Vector2 _chatLogTextPos = new Vector2(4, -16);
        private int _chatLogWidth = 452;
        private int _chatInputWidth = 380;
        private int _chatLogLineHeight = DefaultChatLogLineHeight;
        private int _chatCursorHeight = DefaultChatCursorHeight;
        private Rectangle? _chatInteractionBounds;

        public Action ToggleChatRequested { get; set; }
        public Action<int> CycleChatTargetRequested { get; set; }
        public Action CharacterInfoRequested { get; set; }
        public Action MemoMailboxRequested { get; set; }
        public Action<string> WhisperTargetRequested { get; set; }

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
            RefreshTextMetrics();
        }

        public void SetPixelTexture(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture == null && graphicsDevice != null)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            if (_clientTextRasterizer == null && graphicsDevice != null)
            {
                _clientTextRasterizer = new ClientTextRasterizer(graphicsDevice);
            }
        }

        public void SetChatEnterTexture(Texture2D chatEnterTexture)
        {
            _chatEnterTexture = chatEnterTexture;
            RefreshTextMetrics();
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

        public void SetLayoutMetrics(
            Point frameAnchor,
            Vector2 chatTargetLabelPos,
            Vector2 chatEnterPos,
            Vector2 chatInputPos,
            Vector2 chatLogTextPos,
            int chatLogWidth,
            Rectangle? chatInteractionBounds = null)
        {
            _pointNotificationAnchor = frameAnchor;
            _chatTargetLabelPos = chatTargetLabelPos;
            _chatEnterPos = chatEnterPos;
            _chatInputBasePos = chatInputPos;
            _chatLogTextBasePos = chatLogTextPos;
            _chatLogWidth = Math.Max(1, chatLogWidth);
            _chatInteractionBounds = chatInteractionBounds;
            RefreshTextMetrics();
        }

        public void BindControls(UIObject chatTargetButton, UIObject chatToggleButton, UIObject scrollUpButton, UIObject scrollDownButton, UIObject characterInfoButton = null, UIObject memoButton = null)
        {
            BindControls(chatTargetButton, chatToggleButton, null, scrollUpButton, scrollDownButton, characterInfoButton, memoButton);
        }

        public void BindControls(
            UIObject chatTargetButton,
            UIObject chatOpenButton,
            UIObject chatCloseButton,
            UIObject scrollUpButton,
            UIObject scrollDownButton,
            UIObject characterInfoButton = null,
            UIObject memoButton = null)
        {
            _chatOpenButton = chatOpenButton;
            _chatCloseButton = chatCloseButton;

            if (chatTargetButton != null)
            {
                chatTargetButton.ButtonClickReleased += _ => CycleChatTargetRequested?.Invoke(1);
            }

            if (chatOpenButton != null)
            {
                chatOpenButton.ButtonClickReleased += _ => ToggleChatRequested?.Invoke();
            }

            if (chatCloseButton != null)
            {
                chatCloseButton.ButtonClickReleased += _ => ToggleChatRequested?.Invoke();
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
            MapSimulatorChatRenderState chatState = _chatStateProvider?.Invoke();
            SyncChatToggleButtons(chatState?.IsActive == true);

            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                this.Position.X, this.Position.Y, centerX, centerY,
                drawReflectionInfo,
                renderParameters,
                TickCount);

            foreach (UIObject uiBtn in uiButtons)
            {
                if (uiBtn == null || !uiBtn.ButtonVisible)
                {
                    continue;
                }

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

            DrawChatOverlay(sprite, TickCount, chatState);
            DrawPointNotifications(sprite, TickCount);
        }

        private void DrawChatOverlay(SpriteBatch sprite, int tickCount, MapSimulatorChatRenderState chatState)
        {
            if (_font == null)
            {
                return;
            }

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

        private void SyncChatToggleButtons(bool isChatActive)
        {
            _chatOpenButton?.SetVisible(!isChatActive);
            _chatCloseButton?.SetVisible(isChatActive);
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

            Vector2 drawPosition = SnapToPixel(new Vector2(
                this.Position.X + _pointNotificationAnchor.X - origin.X,
                this.Position.Y + _pointNotificationAnchor.Y - origin.Y));
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
                    SnapToPixel(new Vector2(
                        this.Position.X + _chatTargetLabelPos.X + originDelta.X,
                        this.Position.Y + _chatTargetLabelPos.Y + originDelta.Y)),
                    Color.White);
            }
        }

        private void DrawChatMessages(SpriteBatch sprite, MapSimulatorChatRenderState chatState, int tickCount)
        {
            List<WrappedChatLine> wrappedLines = BuildWrappedLines(chatState.Messages, chatState.LocalPlayerName);
            AdjustScrollForNewLines(wrappedLines.Count, tickCount);
            _whisperTargetHitRegions.Clear();

            int maxScrollOffset = Math.Max(0, wrappedLines.Count - ChatMaxVisibleLines);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScrollOffset);

            int lineIndex = wrappedLines.Count - 1 - _scrollOffset;
            int drawnLines = 0;
            float lineY = this.Position.Y + _chatLogTextPos.Y;

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

                Color lineColor = ResolveRenderedLineColor(line) * alpha;
                Color backgroundColor = GetClientChatLineBackgroundColor(line.ChatLogType, line.ChannelId) * alpha;
                Color shadowColor = GetClientChatLineShadowColor(line.ChatLogType, line.ChannelId) * alpha;
                Vector2 textSize = MeasureChatText(line.Text);
                if (_pixelTexture != null)
                {
                    sprite.Draw(
                        _pixelTexture,
                    new Rectangle(this.Position.X + (int)_chatLogTextPos.X - 2, (int)lineY - 1, (int)textSize.X + 6, (int)textSize.Y + 2),
                        backgroundColor);
                }

                DrawTextWithShadow(sprite, line.Text, new Vector2(this.Position.X + _chatLogTextPos.X, lineY), lineColor, shadowColor);
                TryRegisterWhisperTargetHitRegion(line, lineY);
                lineY -= _chatLogLineHeight;
                drawnLines++;
                lineIndex--;
            }

            _lastWrappedLineCount = wrappedLines.Count;
        }

        private static Color ResolveRenderedLineColor(WrappedChatLine line)
        {
            if (line == null)
            {
                return Color.White;
            }

            Color mappedColor = MapSimulatorChat.ResolveRenderedClientChatLogColor(line.ChatLogType, line.ChannelId);
            return mappedColor != Color.White || line.ChatLogType == 0
                ? mappedColor
                : line.Color;
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

            Vector2 inputPos = SnapToPixel(new Vector2(this.Position.X + _chatInputPos.X, this.Position.Y + _chatInputPos.Y));
            string inputText = chatState.InputText ?? string.Empty;
            int cursorPosition = Math.Clamp(chatState.CursorPosition, 0, inputText.Length);
            string visibleText = ResolveVisibleInputText(inputText, cursorPosition, out int visibleCursorOffset);
            DrawTextWithShadow(sprite, visibleText, inputPos, Color.White, Color.Black);

            if (_pixelTexture == null || ((tickCount / 500) % 2) != 0)
            {
                return;
            }

            string textBeforeCursor = visibleCursorOffset <= 0
                ? string.Empty
                : visibleText.Substring(0, Math.Clamp(visibleCursorOffset, 0, visibleText.Length));
            float cursorX = (float)Math.Round(inputPos.X + MeasureChatText(textBeforeCursor).X);
            sprite.Draw(_pixelTexture,
                new Rectangle(
                    (int)cursorX,
                    (int)Math.Floor(inputPos.Y + ResolveCursorTopOffset()),
                    1,
                    _chatCursorHeight),
                Color.White);
        }

        private void RefreshTextMetrics()
        {
            float measuredHeight = ResolveMeasuredTextHeight();
            int fontLineSpacing = ResolveFontLineSpacing();
            _chatLogLineHeight = Math.Max(DefaultChatLogLineHeight, fontLineSpacing + 1);
            _chatCursorHeight = Math.Clamp(
                fontLineSpacing - 1,
                1,
                Math.Max(1, (_chatEnterTexture?.Height ?? DefaultChatCursorHeight + 2) - 2));

            float inputYOffset = ResolveInputTextYOffset(measuredHeight);
            _chatInputPos = new Vector2(_chatInputBasePos.X, _chatEnterPos.Y + inputYOffset);
            _chatWhisperPromptPos = new Vector2(
                _chatInputPos.X,
                _chatInputPos.Y - Math.Max(DefaultChatWhisperPromptGap, _chatLogLineHeight + 3));
            _chatLogTextPos = new Vector2(
                _chatLogTextBasePos.X,
                _chatEnterPos.Y - (_chatLogLineHeight + DefaultChatLogToEnterGap));
            _chatInputWidth = ResolveChatInputWidth();
        }

        private float ResolveMeasuredTextHeight()
        {
            if (_font == null)
            {
                return DefaultChatCursorHeight;
            }

            return Math.Max(1f, MeasureChatText("Ag").Y);
        }

        private float ResolveInputTextYOffset(float measuredTextHeight)
        {
            int enterHeight = _chatEnterTexture?.Height ?? 21;
            return MathF.Floor(Math.Max(0f, (enterHeight - measuredTextHeight) * 0.5f));
        }

        private float ResolveCursorTopOffset()
        {
            float measuredHeight = ResolveMeasuredTextHeight();
            return MathF.Floor(Math.Max(0f, (measuredHeight - _chatCursorHeight) * 0.5f));
        }

        private int ResolveChatInputWidth()
        {
            float enterRight = _chatEnterPos.X + (_chatEnterTexture?.Width ?? 457);
            float availableWidth = enterRight - _chatInputPos.X - DefaultChatInputRightPadding;
            return Math.Max(1, (int)MathF.Floor(availableWidth));
        }

        private string ResolveVisibleInputText(string inputText, int cursorPosition, out int visibleCursorOffset)
        {
            visibleCursorOffset = 0;
            if (_font == null || string.IsNullOrEmpty(inputText))
            {
                return inputText ?? string.Empty;
            }

            float maxWidth = Math.Max(1, _chatInputWidth);
            if (MeasureChatText(inputText).X <= maxWidth)
            {
                visibleCursorOffset = cursorPosition;
                return inputText;
            }

            int startIndex = cursorPosition;
            while (startIndex > 0)
            {
                string candidate = inputText.Substring(startIndex - 1, cursorPosition - startIndex + 1);
                if (MeasureChatText(candidate).X > maxWidth)
                {
                    break;
                }

                startIndex--;
            }

            int endIndex = cursorPosition;
            while (endIndex < inputText.Length)
            {
                string candidate = inputText.Substring(startIndex, endIndex - startIndex + 1);
                if (MeasureChatText(candidate).X > maxWidth)
                {
                    break;
                }

                endIndex++;
            }

            visibleCursorOffset = cursorPosition - startIndex;
            return inputText.Substring(startIndex, Math.Max(0, endIndex - startIndex));
        }

        private List<WrappedChatLine> BuildWrappedLines(IReadOnlyList<ChatMessage> messages, string localPlayerName)
        {
            var wrappedLines = new List<WrappedChatLine>();
            if (messages == null)
            {
                return wrappedLines;
            }

            string normalizedLocalPlayerName = MapSimulatorChat.NormalizeChatSpeakerCandidate(localPlayerName);
            foreach (ChatMessage message in messages)
            {
                string whisperTargetCandidate = ResolveWhisperTargetCandidate(message);
                bool canBeginWhisper = CanBeginWhisperFromCandidate(whisperTargetCandidate, normalizedLocalPlayerName);
                string whisperTargetDisplayText = ResolveWhisperTargetDisplayText(message, whisperTargetCandidate);
                bool isFirstWrappedLine = true;
                foreach (string lineText in WrapText(
                    message.Text ?? string.Empty,
                    _chatLogWidth,
                    message.ChatLogType,
                    ShouldIndentWrappedContinuation(message.ChatLogType)))
                {
                    wrappedLines.Add(new WrappedChatLine
                    {
                        Text = lineText,
                        Color = message.Color,
                        Timestamp = message.Timestamp,
                        ChatLogType = message.ChatLogType,
                        ChannelId = message.ChannelId,
                        IsFirstWrappedLine = isFirstWrappedLine,
                        WhisperTargetCandidate = canBeginWhisper ? whisperTargetCandidate : string.Empty,
                        WhisperTargetDisplayText = canBeginWhisper ? whisperTargetDisplayText : string.Empty,
                        CanBeginWhisper = canBeginWhisper
                    });
                    isFirstWrappedLine = false;
                }
            }

            if (wrappedLines.Count > MapSimulatorChat.ClientChatLogEntryLimit)
            {
                wrappedLines.RemoveRange(0, wrappedLines.Count - MapSimulatorChat.ClientChatLogEntryLimit);
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
                if (MeasureChatText(candidate).X <= currentMaxWidth)
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
                if (MeasureChatText(word).X <= currentMaxWidth)
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
                    if (MeasureChatText(fragmentCandidate).X > currentMaxWidth && fragment.Length > 0)
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
            Vector2 snappedPosition = SnapToPixel(position);
            if (_clientTextRasterizer != null)
            {
                _clientTextRasterizer.DrawString(sprite, text, snappedPosition + new Vector2(1, 1), shadowColor);
                _clientTextRasterizer.DrawString(sprite, text, snappedPosition, color);
                return;
            }

            sprite.DrawString(_font, text, snappedPosition + new Vector2(1, 1), shadowColor);
            sprite.DrawString(_font, text, snappedPosition, color);
        }

        private void TryRegisterWhisperTargetHitRegion(WrappedChatLine line, float lineY)
        {
            if (line == null
                || !line.IsFirstWrappedLine
                || !line.CanBeginWhisper
                || string.IsNullOrWhiteSpace(line.WhisperTargetCandidate)
                || string.IsNullOrWhiteSpace(line.WhisperTargetDisplayText)
                || _font == null)
            {
                return;
            }

            float textWidth = MeasureChatText(line.WhisperTargetDisplayText).X;
            if (textWidth <= 0f)
            {
                return;
            }

            _whisperTargetHitRegions.Add(new WhisperTargetHitRegion
            {
                Bounds = new Rectangle(
                    (int)Math.Round(this.Position.X + _chatLogTextPos.X),
                    (int)Math.Round(lineY) - 1,
                    (int)Math.Ceiling(textWidth) + 2,
                    Math.Max(1, ResolveFontLineSpacing())),
                WhisperTarget = line.WhisperTargetCandidate
            });
        }

        private Vector2 MeasureChatText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            if (_clientTextRasterizer != null)
            {
                return _clientTextRasterizer.MeasureString(text);
            }

            return _font?.MeasureString(text) ?? Vector2.Zero;
        }

        private int ResolveFontLineSpacing()
        {
            if (_clientTextRasterizer != null)
            {
                return Math.Max(1, (int)Math.Ceiling(_clientTextRasterizer.MeasureString("Ag").Y));
            }

            return _font?.LineSpacing ?? DefaultChatCursorHeight + 1;
        }

        private static Vector2 SnapToPixel(Vector2 position)
        {
            return new Vector2(
                (float)Math.Round(position.X),
                (float)Math.Round(position.Y));
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

        private static Color GetClientChatLineBackgroundColor(int chatLogType, int channelId)
        {
            return chatLogType switch
            {
                11 => new Color(0, 0, 0, 176),
                13 => new Color(202, 231, 255, 176),
                14 => new Color(255, 191, 221, 204),
                15 => new Color(247, 75, 75, 255),
                16 or 21 => new Color(255, 198, 0, 221),
                18 => new Color(77, 26, 173, 44),
                19 => channelId != -1 ? new Color(153, 204, 51, 255) : new Color(255, 92, 89, 128),
                20 => new Color(255, 92, 89, 128),
                22 or 23 => new Color(153, 204, 51, 255),
                _ => new Color(0, 0, 0, 150)
            };
        }

        private static Color GetClientChatLineShadowColor(int chatLogType, int channelId)
        {
            return chatLogType switch
            {
                11 => new Color(255, 255, 255, 176),
                13 => new Color(202, 231, 255, 176),
                14 => new Color(255, 191, 221, 204),
                15 => new Color(247, 75, 75, 255),
                16 or 21 => new Color(255, 198, 0, 221),
                18 => new Color(77, 26, 173, 44),
                19 => channelId != -1 ? new Color(153, 204, 51, 255) : new Color(255, 92, 89, 128),
                20 => new Color(255, 92, 89, 128),
                22 or 23 => new Color(153, 204, 51, 255),
                _ => Color.Black
            };
        }

        #region IClickableUIObject
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            HandleMouseWheel(mouseState);
            bool whisperHandled = HandleWhisperTargetClick(mouseState);
            bool buttonHandled = !whisperHandled && UIMouseEventHandler.CheckMouseEvent(
                shiftCenteredX,
                shiftCenteredY,
                this.Position.X,
                this.Position.Y,
                mouseState,
                mouseCursor,
                uiButtons,
                false);
            _previousLeftButtonState = mouseState.LeftButton;
            return whisperHandled || buttonHandled;
        }

        private bool HandleWhisperTargetClick(MouseState mouseState)
        {
            WhisperTargetHitRegion hoveredRegion = FindWhisperTargetHitRegion(mouseState.X, mouseState.Y);
            bool isPressStarted = mouseState.LeftButton == ButtonState.Pressed
                && _previousLeftButtonState == ButtonState.Released;
            bool isRelease = mouseState.LeftButton == ButtonState.Released
                && _previousLeftButtonState == ButtonState.Pressed;

            if (isPressStarted)
            {
                _pressedWhisperTarget = hoveredRegion?.WhisperTarget;
                return hoveredRegion != null;
            }

            if (!isRelease)
            {
                return !string.IsNullOrWhiteSpace(_pressedWhisperTarget) && hoveredRegion != null;
            }

            string pressedWhisperTarget = _pressedWhisperTarget;
            _pressedWhisperTarget = null;
            if (string.IsNullOrWhiteSpace(pressedWhisperTarget)
                || hoveredRegion == null
                || !string.Equals(pressedWhisperTarget, hoveredRegion.WhisperTarget, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            WhisperTargetRequested?.Invoke(hoveredRegion.WhisperTarget);
            return true;
        }

        private WhisperTargetHitRegion FindWhisperTargetHitRegion(int mouseX, int mouseY)
        {
            for (int i = 0; i < _whisperTargetHitRegions.Count; i++)
            {
                WhisperTargetHitRegion region = _whisperTargetHitRegions[i];
                if (region.Bounds.Contains(mouseX, mouseY))
                {
                    return region;
                }
            }

            return null;
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
            if (_chatInteractionBounds.HasValue)
            {
                Rectangle bounds = _chatInteractionBounds.Value;
                return new Rectangle(
                    this.Position.X + bounds.X,
                    this.Position.Y + bounds.Y,
                    bounds.Width,
                    bounds.Height);
            }

            return new Rectangle(
                this.Position.X + (int)_chatLogTextPos.X - 4,
                this.Position.Y + (int)_chatLogTextPos.Y - (ChatMaxVisibleLines * _chatLogLineHeight) + 2,
                _chatLogWidth + 18,
                (ChatMaxVisibleLines * _chatLogLineHeight) + 42);
        }

        private static string ResolveWhisperTargetCandidate(ChatMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.WhisperTargetCandidate))
            {
                return MapSimulatorChat.NormalizeChatSpeakerCandidate(message.WhisperTargetCandidate);
            }

            if (!CanBeginWhisperFromChatLogType(message.ChatLogType))
            {
                return string.Empty;
            }

            if (TryExtractDirectedWhisperTarget(message.Text, out string directedWhisperTarget))
            {
                return directedWhisperTarget;
            }

            string speakerToken = ExtractSpeakerToken(message.Text);
            return string.IsNullOrWhiteSpace(speakerToken)
                ? string.Empty
                : MapSimulatorChat.NormalizeChatSpeakerCandidate(speakerToken);
        }

        private static string ResolveWhisperTargetDisplayText(ChatMessage message, string whisperTargetCandidate)
        {
            if (string.IsNullOrWhiteSpace(whisperTargetCandidate))
            {
                return string.Empty;
            }

            int separatorIndex = (message.Text ?? string.Empty).IndexOf(':');
            if (separatorIndex < 0)
            {
                return string.Empty;
            }

            string prefix = message.Text.Substring(0, separatorIndex + 1).TrimStart();
            return string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix;
        }

        private static bool CanBeginWhisperFromChatLogType(int chatLogType)
        {
            return chatLogType == 14
                || chatLogType == 15
                || chatLogType == 16
                || chatLogType == 18
                || chatLogType == 19
                || chatLogType == 20
                || chatLogType == 21
                || chatLogType == 22;
        }

        private static string ExtractSpeakerToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int separatorIndex = text.IndexOf(':');
            if (separatorIndex < 0)
            {
                return string.Empty;
            }

            string prefix = text.Substring(0, separatorIndex).Trim();
            if (prefix.StartsWith(">", StringComparison.Ordinal))
            {
                prefix = prefix.Substring(1).TrimStart();
            }

            int closingBracketIndex = prefix.LastIndexOf(']');
            if (closingBracketIndex >= 0 && closingBracketIndex + 1 < prefix.Length)
            {
                prefix = prefix.Substring(closingBracketIndex + 1).TrimStart();
            }

            int channelSuffixIndex = prefix.LastIndexOf(" (", StringComparison.Ordinal);
            if (channelSuffixIndex > 0 && prefix.EndsWith(")", StringComparison.Ordinal))
            {
                prefix = prefix.Substring(0, channelSuffixIndex).TrimEnd();
            }

            return MapSimulatorChat.NormalizeChatSpeakerCandidate(prefix);
        }

        private static bool TryExtractDirectedWhisperTarget(string text, out string whisperTarget)
        {
            whisperTarget = string.Empty;
            if (string.IsNullOrWhiteSpace(text)
                || (!text.StartsWith("[Whisper]", StringComparison.OrdinalIgnoreCase)
                    && !text.StartsWith("[GM Whisper]", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            int separatorIndex = text.IndexOf(':');
            if (separatorIndex < 0)
            {
                return false;
            }

            string prefix = text.Substring(0, separatorIndex).Trim();
            int arrowIndex = prefix.LastIndexOf("->", StringComparison.Ordinal);
            if (arrowIndex < 0 || arrowIndex + 2 >= prefix.Length)
            {
                return false;
            }

            whisperTarget = MapSimulatorChat.NormalizeChatSpeakerCandidate(prefix[(arrowIndex + 2)..]);
            return !string.IsNullOrWhiteSpace(whisperTarget);
        }

        private static bool CanBeginWhisperFromCandidate(string whisperTargetCandidate, string localPlayerName)
        {
            return MapSimulatorChat.ValidateWhisperTargetCandidate(
                whisperTargetCandidate,
                localPlayerName,
                out _) == MapSimulatorChat.WhisperTargetValidationResult.Valid;
        }
        #endregion
    }
}
