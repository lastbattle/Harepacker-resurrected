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

        private sealed class WhisperPickerHitRegion
        {
            public Rectangle Bounds { get; set; }
            public string WhisperTarget { get; set; }
        }

        private sealed class WhisperPickerButtonHitRegion
        {
            public Rectangle Bounds { get; set; }
            public WhisperPickerButtonAction Action { get; set; }
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
        private WhisperPickerButtonAction? _pressedWhisperPickerButtonAction;
        private readonly List<WhisperTargetHitRegion> _whisperTargetHitRegions = new List<WhisperTargetHitRegion>();
        private readonly List<WhisperPickerHitRegion> _whisperPickerHitRegions = new List<WhisperPickerHitRegion>();
        private Rectangle? _whisperPromptBounds;
        private Rectangle? _whisperPickerBounds;
        private Texture2D _whisperPickerSelectedTexture;
        private Texture2D _whisperPickerRowTexture;
        private Texture2D _whisperPickerDialogTopTexture;
        private Texture2D _whisperPickerDialogCenterTexture;
        private Texture2D _whisperPickerDialogBottomTexture;
        private Texture2D _whisperPickerDialogBarTexture;
        private Texture2D _whisperPickerPrevButtonTexture;
        private Texture2D _whisperPickerNextButtonTexture;
        private Texture2D _whisperPickerOkButtonTexture;
        private Texture2D _whisperPickerCloseButtonTexture;
        private readonly List<WhisperPickerButtonHitRegion> _whisperPickerButtonHitRegions = new List<WhisperPickerButtonHitRegion>();

        private const int ChatMessageDisplayTime = 10000;
        private const int ChatMessageFadeTime = 2000;
        private const int ChatMaxVisibleLines = 8;
        private const int DefaultChatLogLineHeight = 14;
        private const int DefaultChatCursorHeight = 13;
        private const int DefaultChatWhisperPromptGap = 15;
        private const int DefaultChatLogToEnterGap = 4;
        private const int DefaultChatInputRightPadding = 3;
        private const int ChatSpecialFirstLineWidthReduction = 38;
        private const int ChatScrollRecentThresholdMs = 5000;
        private const int WhisperPickerVisibleRows = 6;
        private const int WhisperPickerRowPadding = 4;
        private const int WhisperPickerFramePadding = 3;
        private const int WhisperPickerModalContentPadding = 16;
        private const int WhisperPickerModalButtonGap = 6;
        private const int WhisperPickerModalHeaderGap = 10;
        private const int WhisperPickerModalFooterGap = 12;
        private const int WhisperPickerModalMinimumWidth = 360;
        private const int WhisperPickerModalMinimumHeight = 170;
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
        public Action WhisperTargetPickerRequested { get; set; }
        public Action<string> WhisperTargetPickerCandidateRequested { get; set; }
        public Action<int> WhisperTargetPickerSelectionDeltaRequested { get; set; }
        public Action WhisperTargetPickerConfirmRequested { get; set; }
        public Action WhisperTargetPickerCancelRequested { get; set; }

        private enum WhisperPickerButtonAction
        {
            Previous = 0,
            Next = 1,
            Confirm = 2,
            Close = 3
        }

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
                _clientTextRasterizer = new ClientTextRasterizer(
                    graphicsDevice,
                    preferEmbeddedPrivateFontSources: true);
            }
        }

        public void SetChatEnterTexture(Texture2D chatEnterTexture)
        {
            _chatEnterTexture = chatEnterTexture;
            RefreshTextMetrics();
        }

        public void SetWhisperPickerTextures(Texture2D selectedTexture, Texture2D rowTexture)
        {
            _whisperPickerSelectedTexture = selectedTexture;
            _whisperPickerRowTexture = rowTexture;
        }

        public void SetWhisperPickerDialogTextures(
            Texture2D topTexture,
            Texture2D centerTexture,
            Texture2D bottomTexture,
            Texture2D barTexture,
            Texture2D prevButtonTexture,
            Texture2D nextButtonTexture,
            Texture2D okButtonTexture,
            Texture2D closeButtonTexture)
        {
            _whisperPickerDialogTopTexture = topTexture;
            _whisperPickerDialogCenterTexture = centerTexture;
            _whisperPickerDialogBottomTexture = bottomTexture;
            _whisperPickerDialogBarTexture = barTexture;
            _whisperPickerPrevButtonTexture = prevButtonTexture;
            _whisperPickerNextButtonTexture = nextButtonTexture;
            _whisperPickerOkButtonTexture = okButtonTexture;
            _whisperPickerCloseButtonTexture = closeButtonTexture;
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

        public void SetPointNotificationAnchor(Point pointNotificationAnchor)
        {
            _pointNotificationAnchor = pointNotificationAnchor;
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
            _whisperPickerHitRegions.Clear();
            _whisperPickerButtonHitRegions.Clear();
            _whisperPromptBounds = null;
            _whisperPickerBounds = null;

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
                DrawWhisperTargetPicker(sprite, chatState);
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
                Vector2 promptPos = new Vector2(this.Position.X + _chatWhisperPromptPos.X, this.Position.Y + _chatWhisperPromptPos.Y);
                DrawTextWithShadow(sprite,
                    whisperPrompt,
                    promptPos,
                    new Color(255, 170, 255),
                    Color.Black);
                Vector2 promptSize = MeasureChatText(whisperPrompt);
                _whisperPromptBounds = new Rectangle(
                    (int)Math.Round(promptPos.X),
                    (int)Math.Round(promptPos.Y) - 1,
                    Math.Max(1, (int)Math.Ceiling(promptSize.X) + 2),
                    Math.Max(1, ResolveFontLineSpacing()));
            }
            else
            {
                _whisperPromptBounds = null;
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

        private void DrawWhisperTargetPicker(SpriteBatch sprite, MapSimulatorChatRenderState chatState)
        {
            if (chatState == null || !chatState.IsWhisperTargetPickerActive || _font == null)
            {
                return;
            }

            if (chatState.WhisperTargetPickerPresentation == MapSimulatorChat.WhisperTargetPickerPresentation.Modal)
            {
                DrawModalWhisperTargetPicker(sprite, chatState);
                return;
            }

            DrawInlineWhisperTargetPicker(sprite, chatState);
        }

        private void DrawInlineWhisperTargetPicker(SpriteBatch sprite, MapSimulatorChatRenderState chatState)
        {
            IReadOnlyList<string> candidates = chatState.WhisperCandidates ?? Array.Empty<string>();
            int visibleCount = Math.Min(WhisperPickerVisibleRows, candidates.Count);
            int firstVisibleIndex = MapSimulatorChat.ResolveWhisperTargetPickerFirstVisibleIndex(
                chatState.WhisperTargetPickerSelectionIndex,
                candidates.Count,
                WhisperPickerVisibleRows);
            float maxCandidateWidth = ResolveWhisperPickerMaxCandidateWidth(chatState, firstVisibleIndex, visibleCount);
            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = firstVisibleIndex + i;
                if (candidateIndex >= 0 && candidateIndex < candidates.Count)
                {
                    maxCandidateWidth = Math.Max(maxCandidateWidth, MeasureChatText(candidates[candidateIndex]).X);
                }
            }

            int rowHeight = Math.Max(
                Math.Max(
                    _whisperPickerSelectedTexture?.Height ?? 0,
                    _whisperPickerRowTexture?.Height ?? 0),
                ResolveFontLineSpacing() + WhisperPickerRowPadding);
            int popupWidth = Math.Max(96, (int)Math.Ceiling(maxCandidateWidth) + (WhisperPickerFramePadding * 2) + 8);
            int popupHeight = rowHeight * (visibleCount + 1);
            int popupX = (int)Math.Round(this.Position.X + _chatInputPos.X);
            int popupY = (int)Math.Round(this.Position.Y + _chatWhisperPromptPos.Y) - popupHeight - 4;
            _whisperPickerBounds = new Rectangle(popupX, popupY, popupWidth, popupHeight);

            if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture,
                    new Rectangle(popupX - 1, popupY - 1, popupWidth + 2, popupHeight + 2),
                    new Color(0, 0, 0, 216));
            }

            DrawWhisperPickerRow(
                sprite,
                popupX,
                popupY,
                popupWidth,
                rowHeight,
                chatState.InputText ?? string.Empty,
                isSelected: chatState.WhisperTargetPickerSelectionIndex < 0,
                registerHitRegion: false);

            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = firstVisibleIndex + i;
                if (candidateIndex < 0 || candidateIndex >= candidates.Count)
                {
                    continue;
                }

                int rowY = popupY + ((i + 1) * rowHeight);
                DrawWhisperPickerRow(
                    sprite,
                    popupX,
                    rowY,
                    popupWidth,
                    rowHeight,
                    candidates[candidateIndex],
                    isSelected: candidateIndex == chatState.WhisperTargetPickerSelectionIndex,
                    registerHitRegion: true);
            }
        }

        private void DrawModalWhisperTargetPicker(SpriteBatch sprite, MapSimulatorChatRenderState chatState)
        {
            IReadOnlyList<string> candidates = chatState.WhisperCandidates ?? Array.Empty<string>();
            int visibleCount = Math.Min(WhisperPickerVisibleRows, candidates.Count);
            int firstVisibleIndex = MapSimulatorChat.ResolveWhisperTargetPickerFirstVisibleIndex(
                chatState.WhisperTargetPickerSelectionIndex,
                candidates.Count,
                WhisperPickerVisibleRows);
            int rowHeight = Math.Max(
                Math.Max(
                    _whisperPickerSelectedTexture?.Height ?? 0,
                    _whisperPickerRowTexture?.Height ?? 0),
                ResolveFontLineSpacing() + WhisperPickerRowPadding);
            int buttonRowHeight = Math.Max(
                Math.Max(
                    Math.Max(_whisperPickerPrevButtonTexture?.Height ?? 0, _whisperPickerNextButtonTexture?.Height ?? 0),
                    Math.Max(_whisperPickerOkButtonTexture?.Height ?? 0, _whisperPickerCloseButtonTexture?.Height ?? 0)),
                18);
            int contentWidth = Math.Max(
                WhisperPickerModalMinimumWidth - (WhisperPickerModalContentPadding * 2),
                (int)Math.Ceiling(ResolveWhisperPickerMaxCandidateWidth(chatState, firstVisibleIndex, visibleCount)) + (WhisperPickerFramePadding * 2) + 24);
            int modalWidth = Math.Max(
                WhisperPickerModalMinimumWidth,
                Math.Max(
                    _whisperPickerDialogTopTexture?.Width ?? 0,
                    Math.Max(_whisperPickerDialogBottomTexture?.Width ?? 0, contentWidth + (WhisperPickerModalContentPadding * 2))));
            int titleBarHeight = _whisperPickerDialogBarTexture?.Height ?? 0;
            int topHeight = _whisperPickerDialogTopTexture?.Height ?? 28;
            int bottomHeight = _whisperPickerDialogBottomTexture?.Height ?? 44;
            int contentHeight = (rowHeight * (visibleCount + 1))
                + WhisperPickerModalHeaderGap
                + titleBarHeight
                + WhisperPickerModalFooterGap
                + buttonRowHeight;
            int modalHeight = Math.Max(WhisperPickerModalMinimumHeight, topHeight + contentHeight + bottomHeight);
            Viewport viewport = sprite.GraphicsDevice.Viewport;
            int modalX = viewport.X + Math.Max(0, (viewport.Width - modalWidth) / 2);
            int modalY = viewport.Y + Math.Max(0, (viewport.Height - modalHeight) / 2);
            _whisperPickerBounds = new Rectangle(modalX, modalY, modalWidth, modalHeight);

            if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, viewport.Bounds, new Color(0, 0, 0, 132));
            }

            DrawModalWhisperPickerFrame(sprite, _whisperPickerBounds.Value);

            int contentX = modalX + WhisperPickerModalContentPadding;
            int contentY = modalY + topHeight + WhisperPickerModalHeaderGap;
            if (_whisperPickerDialogBarTexture != null)
            {
                int barX = modalX + Math.Max(0, (modalWidth - _whisperPickerDialogBarTexture.Width) / 2);
                sprite.Draw(_whisperPickerDialogBarTexture, new Vector2(barX, modalY + 6), Color.White);
            }

            Vector2 titlePos = new Vector2(contentX, modalY + 8f);
            DrawTextWithShadow(sprite, "Whisper Target", titlePos, new Color(244, 240, 227), Color.Black);

            DrawWhisperPickerRow(
                sprite,
                contentX,
                contentY,
                modalWidth - (WhisperPickerModalContentPadding * 2),
                rowHeight,
                chatState.InputText ?? string.Empty,
                isSelected: chatState.WhisperTargetPickerSelectionIndex < 0,
                registerHitRegion: false);

            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = firstVisibleIndex + i;
                if (candidateIndex < 0 || candidateIndex >= candidates.Count)
                {
                    continue;
                }

                DrawWhisperPickerRow(
                    sprite,
                    contentX,
                    contentY + ((i + 1) * rowHeight),
                    modalWidth - (WhisperPickerModalContentPadding * 2),
                    rowHeight,
                    candidates[candidateIndex],
                    isSelected: candidateIndex == chatState.WhisperTargetPickerSelectionIndex,
                    registerHitRegion: true);
            }

            int buttonY = modalY + modalHeight - bottomHeight - buttonRowHeight - 6;
            DrawWhisperPickerButtonRow(sprite, modalX, buttonY, modalWidth, buttonRowHeight);
        }

        private void DrawWhisperPickerRow(
            SpriteBatch sprite,
            int x,
            int y,
            int width,
            int height,
            string text,
            bool isSelected,
            bool registerHitRegion)
        {
            Texture2D rowTexture = isSelected ? _whisperPickerSelectedTexture : _whisperPickerRowTexture;
            if (rowTexture != null)
            {
                sprite.Draw(rowTexture, new Rectangle(x, y, width, height), Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture,
                    new Rectangle(x, y, width, height),
                    isSelected ? new Color(83, 116, 168, 235) : new Color(28, 33, 45, 235));
            }

            DrawTextWithShadow(
                sprite,
                text ?? string.Empty,
                new Vector2(x + WhisperPickerFramePadding + 1, y + Math.Max(0f, (height - MeasureChatText("Ag").Y) * 0.5f)),
                isSelected ? Color.White : new Color(244, 240, 227),
                Color.Black);

            if (registerHitRegion && !string.IsNullOrWhiteSpace(text))
            {
                _whisperPickerHitRegions.Add(new WhisperPickerHitRegion
                {
                    Bounds = new Rectangle(x, y, width, height),
                    WhisperTarget = text
                });
            }
        }

        private float ResolveWhisperPickerMaxCandidateWidth(
            MapSimulatorChatRenderState chatState,
            int firstVisibleIndex,
            int visibleCount)
        {
            IReadOnlyList<string> candidates = chatState?.WhisperCandidates ?? Array.Empty<string>();
            float maxCandidateWidth = MeasureChatText(chatState?.InputText ?? string.Empty).X;
            for (int i = 0; i < visibleCount; i++)
            {
                int candidateIndex = firstVisibleIndex + i;
                if (candidateIndex >= 0 && candidateIndex < candidates.Count)
                {
                    maxCandidateWidth = Math.Max(maxCandidateWidth, MeasureChatText(candidates[candidateIndex]).X);
                }
            }

            return maxCandidateWidth;
        }

        private void DrawModalWhisperPickerFrame(SpriteBatch sprite, Rectangle bounds)
        {
            int topHeight = _whisperPickerDialogTopTexture?.Height ?? 28;
            int bottomHeight = _whisperPickerDialogBottomTexture?.Height ?? 44;
            Rectangle topBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, topHeight);
            Rectangle centerBounds = new Rectangle(bounds.X, bounds.Y + topHeight, bounds.Width, Math.Max(1, bounds.Height - topHeight - bottomHeight));
            Rectangle bottomBounds = new Rectangle(bounds.X, bounds.Bottom - bottomHeight, bounds.Width, bottomHeight);

            if (_whisperPickerDialogTopTexture != null)
            {
                sprite.Draw(_whisperPickerDialogTopTexture, topBounds, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, topBounds, new Color(57, 69, 84, 240));
            }

            if (_whisperPickerDialogCenterTexture != null)
            {
                sprite.Draw(_whisperPickerDialogCenterTexture, centerBounds, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, centerBounds, new Color(235, 228, 213, 244));
            }

            if (_whisperPickerDialogBottomTexture != null)
            {
                sprite.Draw(_whisperPickerDialogBottomTexture, bottomBounds, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, bottomBounds, new Color(217, 206, 187, 244));
            }
        }

        private void DrawWhisperPickerButtonRow(SpriteBatch sprite, int modalX, int buttonY, int modalWidth, int buttonRowHeight)
        {
            Texture2D[] textures =
            {
                _whisperPickerPrevButtonTexture,
                _whisperPickerNextButtonTexture,
                _whisperPickerOkButtonTexture,
                _whisperPickerCloseButtonTexture
            };
            WhisperPickerButtonAction[] actions =
            {
                WhisperPickerButtonAction.Previous,
                WhisperPickerButtonAction.Next,
                WhisperPickerButtonAction.Confirm,
                WhisperPickerButtonAction.Close
            };

            int totalButtonWidth = 0;
            for (int i = 0; i < textures.Length; i++)
            {
                totalButtonWidth += Math.Max(50, textures[i]?.Width ?? 0);
            }

            totalButtonWidth += WhisperPickerModalButtonGap * (textures.Length - 1);
            int buttonX = modalX + Math.Max(0, (modalWidth - totalButtonWidth) / 2);
            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D texture = textures[i];
                int buttonWidth = Math.Max(50, texture?.Width ?? 0);
                Rectangle buttonBounds = new Rectangle(buttonX, buttonY, buttonWidth, buttonRowHeight);
                if (texture != null)
                {
                    sprite.Draw(texture, new Rectangle(buttonX, buttonY, buttonWidth, Math.Max(buttonRowHeight, texture.Height)), Color.White);
                    buttonBounds = new Rectangle(buttonX, buttonY, buttonWidth, Math.Max(buttonRowHeight, texture.Height));
                }
                else if (_pixelTexture != null)
                {
                    sprite.Draw(_pixelTexture, buttonBounds, new Color(154, 120, 68, 240));
                }

                _whisperPickerButtonHitRegions.Add(new WhisperPickerButtonHitRegion
                {
                    Bounds = buttonBounds,
                    Action = actions[i]
                });
                buttonX += buttonBounds.Width + WhisperPickerModalButtonGap;
            }
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
            foreach (string line in StatusBarChatLayoutRules.WrapClientChatText(
                text,
                maxWidth,
                chatLogType,
                value => MeasureChatText(value).X))
            {
                yield return line;
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

        private int ResolveLongestFittingPrefixLength(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            float clampedMaxWidth = Math.Max(1f, maxWidth);
            if (MeasureChatText(text).X <= clampedMaxWidth)
            {
                return text.Length;
            }

            int low = 1;
            int high = text.Length;
            int best = 0;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (MeasureChatText(text.Substring(0, mid)).X <= clampedMaxWidth)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return Math.Max(1, best);
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
            WhisperPickerHitRegion hoveredPickerRegion = FindWhisperPickerHitRegion(mouseState.X, mouseState.Y);
            WhisperPickerButtonHitRegion hoveredButtonRegion = FindWhisperPickerButtonHitRegion(mouseState.X, mouseState.Y);
            bool promptHovered = _whisperPromptBounds?.Contains(mouseState.X, mouseState.Y) == true;
            bool isPressStarted = mouseState.LeftButton == ButtonState.Pressed
                && _previousLeftButtonState == ButtonState.Released;
            bool isRelease = mouseState.LeftButton == ButtonState.Released
                && _previousLeftButtonState == ButtonState.Pressed;

            if (isPressStarted)
            {
                _pressedWhisperTarget = hoveredRegion?.WhisperTarget ?? hoveredPickerRegion?.WhisperTarget;
                _pressedWhisperPickerButtonAction = hoveredButtonRegion?.Action;
                return hoveredRegion != null || hoveredPickerRegion != null || hoveredButtonRegion != null || promptHovered;
            }

            if (!isRelease)
            {
                return !string.IsNullOrWhiteSpace(_pressedWhisperTarget)
                    && (hoveredRegion != null || hoveredPickerRegion != null);
            }

            if (promptHovered && string.IsNullOrWhiteSpace(_pressedWhisperTarget))
            {
                _pressedWhisperPickerButtonAction = null;
                WhisperTargetPickerRequested?.Invoke();
                return true;
            }

            if (hoveredButtonRegion != null
                && _pressedWhisperPickerButtonAction.HasValue
                && hoveredButtonRegion.Action == _pressedWhisperPickerButtonAction.Value)
            {
                _pressedWhisperTarget = null;
                _pressedWhisperPickerButtonAction = null;
                InvokeWhisperPickerButtonAction(hoveredButtonRegion.Action);
                return true;
            }

            if (hoveredPickerRegion != null
                && !string.IsNullOrWhiteSpace(_pressedWhisperTarget)
                && string.Equals(_pressedWhisperTarget, hoveredPickerRegion.WhisperTarget, StringComparison.OrdinalIgnoreCase))
            {
                _pressedWhisperTarget = null;
                WhisperTargetPickerCandidateRequested?.Invoke(hoveredPickerRegion.WhisperTarget);
                return true;
            }

            string pressedWhisperTarget = _pressedWhisperTarget;
            _pressedWhisperTarget = null;
            _pressedWhisperPickerButtonAction = null;
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

        private WhisperPickerHitRegion FindWhisperPickerHitRegion(int mouseX, int mouseY)
        {
            for (int i = 0; i < _whisperPickerHitRegions.Count; i++)
            {
                WhisperPickerHitRegion region = _whisperPickerHitRegions[i];
                if (region.Bounds.Contains(mouseX, mouseY))
                {
                    return region;
                }
            }

            return null;
        }

        private WhisperPickerButtonHitRegion FindWhisperPickerButtonHitRegion(int mouseX, int mouseY)
        {
            for (int i = 0; i < _whisperPickerButtonHitRegions.Count; i++)
            {
                WhisperPickerButtonHitRegion region = _whisperPickerButtonHitRegions[i];
                if (region.Bounds.Contains(mouseX, mouseY))
                {
                    return region;
                }
            }

            return null;
        }

        private void InvokeWhisperPickerButtonAction(WhisperPickerButtonAction action)
        {
            switch (action)
            {
                case WhisperPickerButtonAction.Previous:
                    WhisperTargetPickerSelectionDeltaRequested?.Invoke(-1);
                    break;
                case WhisperPickerButtonAction.Next:
                    WhisperTargetPickerSelectionDeltaRequested?.Invoke(1);
                    break;
                case WhisperPickerButtonAction.Confirm:
                    WhisperTargetPickerConfirmRequested?.Invoke();
                    break;
                case WhisperPickerButtonAction.Close:
                    WhisperTargetPickerCancelRequested?.Invoke();
                    break;
            }
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

            int steps = Math.Max(1, Math.Abs(scrollDelta) / 120);
            if (_whisperPickerBounds?.Contains(mouseState.X, mouseState.Y) == true)
            {
                WhisperTargetPickerSelectionDeltaRequested?.Invoke(scrollDelta > 0 ? -steps : steps);
                return;
            }

            if (!GetChatInteractionBounds().Contains(mouseState.X, mouseState.Y))
            {
                return;
            }

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
