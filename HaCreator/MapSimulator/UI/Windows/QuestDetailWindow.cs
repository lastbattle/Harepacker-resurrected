using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HaCreator.MapSimulator.UI
{
    public sealed class QuestDetailWindow : UIWindowBase
    {
        private const float ClientContentX = 18f;
        private const float ClientContentWidth = 253f;
        private const int ClientLogBaseY = 128;
        private const int ClientSummaryClipY = 252;
        private const int ClientSummaryClipHeight = 111;
        private const int ClientScrLogLenWithSummary = 120;
        private const int ClientScrLogLenWithoutSummary = 238;
        private const float ClientTitleX = 35f;
        private const float ClientTitleY = 42f;
        private const float ClientNpcX = 23f;
        private const float ClientNpcY = 75f;
        private const float ClientLogY = 128f;
        private const float ClientSummaryY = 257f;
        private const int ClientSummaryHeight = 111;
        private const int ScrollStep = 18;
        private const float ConditionLabelWidth = 38f;
        private const float ConditionTextInset = 6f;
        private const float ConditionValueGap = 8f;
        private const float ConditionRowGap = 4f;
        private const int ConditionIconSize = 18;

        private readonly string _windowName;
        private readonly List<ButtonLabel> _buttonLabels = new();
        private readonly Dictionary<QuestWindowActionKind, ActionButtonBinding> _actionButtons = new();
        private readonly Dictionary<QuestDetailNpcButtonStyle, ActionButtonBinding> _npcButtons = new();
        private readonly Dictionary<string, TimeLimitIndicatorStyle> _timeLimitIndicatorStyles = new(StringComparer.OrdinalIgnoreCase);

        private SpriteFont _font;
        private IDXObject _foreground;
        private Point _foregroundOffset;
        private IDXObject _bottomPanel;
        private Point _bottomPanelOffset;
        private IDXObject _summaryPanel;
        private Point _summaryPanelOffset;
        private IDXObject _detailTip;
        private Point _detailTipOffset;
        private UIObject _previousButton;
        private UIObject _nextButton;
        private QuestWindowDetailState _state;
        private int _navigationIndex = -1;
        private int _navigationCount;
        private UIObject _activePrimaryButton;
        private UIObject _activeSecondaryButton;
        private UIObject _activeTertiaryButton;
        private UIObject _activeQuaternaryButton;
        private UIObject _activeDeliveryButton;
        private bool _drawPrimaryLabel = true;
        private bool _drawSecondaryLabel = true;
        private bool _drawTertiaryLabel = true;
        private bool _drawQuaternaryLabel = true;
        private bool _drawDeliveryLabel = true;
        private Texture2D _summaryHeaderTexture;
        private Texture2D _requirementHeaderTexture;
        private Texture2D _rewardHeaderTexture;
        private Texture2D _selectionBarTexture;
        private Texture2D _incompleteSelectionBarTexture;
        private Texture2D _progressFrameTexture;
        private Texture2D _progressGaugeTexture;
        private Texture2D _progressSpotTexture;
        private Point _progressFrameOffset;
        private Func<int, Texture2D> _itemIconProvider;
        private readonly Dictionary<int, Texture2D> _itemIconCache = new();
        private Texture2D _pixel;
        private HoveredQuestItemInfo _hoveredQuestItem;
        private Point _lastMousePosition;
        private NoticeSurface[] _noticeSurfaces = Array.Empty<NoticeSurface>();
        private NoticeAnimationFrame[] _noticeAnimationFrames = Array.Empty<NoticeAnimationFrame>();
        private Point _noticeAnimationOffset;
        private Texture2D _timeLimitBarBackgroundTexture;
        private Texture2D _timeLimitGaugeLeftTexture;
        private Texture2D _timeLimitGaugeMiddleTexture;
        private Texture2D _timeLimitGaugeRightTexture;
        private int? _previousScrollWheelValue;
        private int _logScrollOffset;
        private int _summaryScrollOffset;

        public QuestDetailWindow(IDXObject frame, string windowName)
            : base(frame)
        {
            _windowName = windowName;
        }

        public override string WindowName => _windowName;

        internal event Action PreviousRequested;
        internal event Action NextRequested;
        internal event Action<QuestWindowActionKind> ActionRequested;

        public void SetForeground(IDXObject foreground, Point offset)
        {
            _foreground = foreground;
            _foregroundOffset = offset;
        }

        public void SetBottomPanel(IDXObject panel, Point offset)
        {
            _bottomPanel = panel;
            _bottomPanelOffset = offset;
        }

        public void SetSummaryPanel(IDXObject panel, Point offset)
        {
            _summaryPanel = panel;
            _summaryPanelOffset = offset;
        }

        public void SetDetailTip(IDXObject tip, Point offset)
        {
            _detailTip = tip;
            _detailTipOffset = offset;
        }

        public void SetSectionTextures(
            Texture2D summaryHeaderTexture,
            Texture2D requirementHeaderTexture,
            Texture2D rewardHeaderTexture,
            Texture2D selectionBarTexture,
            Texture2D incompleteSelectionBarTexture)
        {
            _summaryHeaderTexture = summaryHeaderTexture;
            _requirementHeaderTexture = requirementHeaderTexture;
            _rewardHeaderTexture = rewardHeaderTexture;
            _selectionBarTexture = selectionBarTexture;
            _incompleteSelectionBarTexture = incompleteSelectionBarTexture;
        }

        public void SetProgressTextures(Texture2D frameTexture, Texture2D gaugeTexture, Texture2D spotTexture, Point frameOffset)
        {
            _progressFrameTexture = frameTexture;
            _progressGaugeTexture = gaugeTexture;
            _progressSpotTexture = spotTexture;
            _progressFrameOffset = frameOffset;
        }

        public void SetNoticeTextures(Texture2D[] surfaces, Point[] surfaceOffsets, Texture2D[] animationFrames, int[] animationDelays, Point animationOffset)
        {
            if (surfaces != null && surfaceOffsets != null && surfaces.Length == surfaceOffsets.Length)
            {
                _noticeSurfaces = new NoticeSurface[surfaces.Length];
                for (int i = 0; i < surfaces.Length; i++)
                {
                    _noticeSurfaces[i] = new NoticeSurface(surfaces[i], surfaceOffsets[i]);
                }
            }
            else
            {
                _noticeSurfaces = Array.Empty<NoticeSurface>();
            }

            if (animationFrames != null && animationDelays != null && animationFrames.Length == animationDelays.Length)
            {
                _noticeAnimationFrames = new NoticeAnimationFrame[animationFrames.Length];
                for (int i = 0; i < animationFrames.Length; i++)
                {
                    _noticeAnimationFrames[i] = new NoticeAnimationFrame(animationFrames[i], Math.Max(1, animationDelays[i]));
                }
            }
            else
            {
                _noticeAnimationFrames = Array.Empty<NoticeAnimationFrame>();
            }

            _noticeAnimationOffset = animationOffset;
        }

        public void SetTimeLimitBarTextures(Texture2D barBackgroundTexture, Texture2D gaugeLeftTexture, Texture2D gaugeMiddleTexture, Texture2D gaugeRightTexture)
        {
            _timeLimitBarBackgroundTexture = barBackgroundTexture;
            _timeLimitGaugeLeftTexture = gaugeLeftTexture;
            _timeLimitGaugeMiddleTexture = gaugeMiddleTexture;
            _timeLimitGaugeRightTexture = gaugeRightTexture;
        }

        public void SetTimeLimitIndicatorStyle(string styleKey, Texture2D[] frames, Point[] origins, int[] delays)
        {
            if (string.IsNullOrWhiteSpace(styleKey) || frames == null || origins == null || delays == null)
            {
                return;
            }

            int count = Math.Min(frames.Length, Math.Min(origins.Length, delays.Length));
            if (count <= 0)
            {
                return;
            }

            List<TimeLimitAnimationFrame> validFrames = new(count);
            int totalDurationMs = 0;
            for (int i = 0; i < count; i++)
            {
                if (frames[i] == null)
                {
                    continue;
                }

                int delayMs = Math.Max(1, delays[i]);
                validFrames.Add(new TimeLimitAnimationFrame(frames[i], origins[i], delayMs));
                totalDurationMs += delayMs;
            }

            if (validFrames.Count == 0)
            {
                return;
            }

            _timeLimitIndicatorStyles[styleKey.Trim()] = new TimeLimitIndicatorStyle(validFrames.ToArray(), Math.Max(1, totalDurationMs));
        }

        public void SetItemIconProvider(Func<int, Texture2D> itemIconProvider)
        {
            _itemIconProvider = itemIconProvider;
        }

        internal void RegisterActionButton(QuestWindowActionKind action, UIObject button, bool drawLabel = false)
        {
            if (action == QuestWindowActionKind.None || button == null)
            {
                return;
            }

            button.SetVisible(false);
            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                if (_state == null)
                {
                    return;
                }

                if (_state.PrimaryAction == action ||
                    _state.SecondaryAction == action ||
                    _state.QuaternaryAction == action ||
                    ResolveDeliveryAction(_state) == action)
                {
                    ActionRequested?.Invoke(action);
                }
            };

            _actionButtons[action] = new ActionButtonBinding(button, drawLabel);
        }

        internal void RegisterNpcButton(QuestDetailNpcButtonStyle style, UIObject button, bool drawLabel = false)
        {
            if (style == QuestDetailNpcButtonStyle.None || button == null)
            {
                return;
            }

            button.SetVisible(false);
            AddButton(button);
            button.ButtonClickReleased += _ =>
            {
                if (_state?.TertiaryAction == QuestWindowActionKind.LocateNpc && _state.TertiaryActionEnabled)
                {
                    ActionRequested?.Invoke(QuestWindowActionKind.LocateNpc);
                }
            };

            _npcButtons[style] = new ActionButtonBinding(button, drawLabel);
        }

        public void InitializeNavigationButtons(GraphicsDevice device)
        {
            _pixel ??= new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _previousButton = UiButtonFactory.CreateSolidButton(
                device, 48, 18,
                new Color(48, 61, 77, 220),
                new Color(74, 96, 118, 240),
                new Color(63, 80, 98, 235),
                new Color(28, 28, 28, 170));
            _previousButton.X = 16;
            _previousButton.Y = Math.Max(16, (CurrentFrame?.Height ?? 396) - 28);
            _previousButton.ButtonClickReleased += _ => PreviousRequested?.Invoke();
            AddButton(_previousButton);
            _buttonLabels.Add(new ButtonLabel(_previousButton, "Prev"));

            _nextButton = UiButtonFactory.CreateSolidButton(
                device, 48, 18,
                new Color(48, 61, 77, 220),
                new Color(74, 96, 118, 240),
                new Color(63, 80, 98, 235),
                new Color(28, 28, 28, 170));
            _nextButton.X = 70;
            _nextButton.Y = _previousButton.Y;
            _nextButton.ButtonClickReleased += _ => NextRequested?.Invoke();
            AddButton(_nextButton);
            _buttonLabels.Add(new ButtonLabel(_nextButton, "Next"));
        }

        internal void SetDetailState(QuestWindowDetailState state, int navigationIndex, int navigationCount)
        {
            int previousQuestId = _state?.QuestId ?? 0;
            _state = state;
            _navigationIndex = navigationIndex;
            _navigationCount = navigationCount;
            _activePrimaryButton = null;
            _activeSecondaryButton = null;
            _activeTertiaryButton = null;
            _activeQuaternaryButton = null;
            _activeDeliveryButton = null;
            _drawPrimaryLabel = true;
            _drawSecondaryLabel = true;
            _drawTertiaryLabel = true;
            _drawQuaternaryLabel = true;
            _drawDeliveryLabel = true;

            foreach (ActionButtonBinding binding in _actionButtons.Values)
            {
                binding.Button.SetVisible(false);
            }

            foreach (ActionButtonBinding binding in _npcButtons.Values)
            {
                binding.Button.SetVisible(false);
            }

            if (state != null && state.PrimaryAction != QuestWindowActionKind.None &&
                _actionButtons.TryGetValue(state.PrimaryAction, out ActionButtonBinding primaryBinding))
            {
                primaryBinding.Button.SetVisible(true);
                primaryBinding.Button.SetButtonState(state.PrimaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
                _activePrimaryButton = primaryBinding.Button;
                _drawPrimaryLabel = primaryBinding.DrawLabel;
            }

            if (state != null && state.SecondaryAction != QuestWindowActionKind.None &&
                _actionButtons.TryGetValue(state.SecondaryAction, out ActionButtonBinding secondaryBinding))
            {
                secondaryBinding.Button.SetVisible(true);
                secondaryBinding.Button.SetButtonState(state.SecondaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
                _activeSecondaryButton = secondaryBinding.Button;
                _drawSecondaryLabel = secondaryBinding.DrawLabel;
            }

            if (state != null)
            {
                BindDeliveryActionButton(state);
                BindNpcButton(state);
                BindQuaternaryActionButton(state);
                LayoutActionButtons();
            }

            if (state == null || previousQuestId != state.QuestId)
            {
                _logScrollOffset = 0;
                _summaryScrollOffset = 0;
            }

            ClampScrollOffsets();

            if (_previousButton != null)
            {
                bool enabled = navigationCount > 1 && navigationIndex > 0;
                _previousButton.SetVisible(navigationCount > 1);
                _previousButton.SetButtonState(enabled ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            if (_nextButton != null)
            {
                bool enabled = navigationCount > 1 && navigationIndex >= 0 && navigationIndex < navigationCount - 1;
                _nextButton.SetVisible(navigationCount > 1);
                _nextButton.SetButtonState(enabled ? UIObjectState.Normal : UIObjectState.Disabled);
            }
        }

        public override void SetFont(SpriteFont font)
        {
            _font = font;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            Microsoft.Xna.Framework.Input.MouseState mouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();
            _lastMousePosition = new Point(mouseState.X, mouseState.Y);
            _previousScrollWheelValue ??= mouseState.ScrollWheelValue;

            if (_font == null || _state == null || !IsVisible)
            {
                _hoveredQuestItem = null;
                _previousScrollWheelValue = mouseState.ScrollWheelValue;
                return;
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue.Value;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            if (wheelDelta != 0)
            {
                HandleMouseWheel(mouseState, wheelDelta);
            }

            _hoveredQuestItem = ResolveHoveredQuestItem(mouseState.X, mouseState.Y);
        }

        protected override void DrawContents(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_foreground != null)
            {
                _foreground.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _foregroundOffset.X, Position.Y + _foregroundOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_bottomPanel != null)
            {
                _bottomPanel.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _bottomPanelOffset.X, Position.Y + _bottomPanelOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_summaryPanel != null && HasSummaryPaneContent())
            {
                _summaryPanel.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _summaryPanelOffset.X, Position.Y + _summaryPanelOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_detailTip != null && ShouldDrawDetailTip())
            {
                _detailTip.DrawBackground(sprite, skeletonMeshRenderer, gameTime,
                    Position.X + _detailTipOffset.X, Position.Y + _detailTipOffset.Y,
                    Color.White, false, drawReflectionInfo);
            }

            if (_font == null)
            {
                return;
            }

            if (_state == null)
            {
                sprite.DrawString(_font, "Select a quest to inspect its details.", new Vector2(Position.X + ClientContentX, Position.Y + 22), new Color(220, 220, 220));
                return;
            }

            sprite.DrawString(_font, _state.Title, new Vector2(Position.X + ClientTitleX, Position.Y + ClientTitleY), Color.White);

            if (!string.IsNullOrWhiteSpace(_state.NpcText))
            {
                sprite.DrawString(_font, _state.NpcText, new Vector2(Position.X + ClientNpcX, Position.Y + ClientNpcY), new Color(214, 214, 171));
            }

            DrawDetailInset(sprite, TickCount);
            DrawNoticeSurface(sprite, TickCount);
            DrawLogPane(sprite);
            DrawSummaryPane(sprite);
        }

        protected override void DrawOverlay(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (_font == null)
            {
                return;
            }

            foreach (ButtonLabel label in _buttonLabels)
            {
                if (!label.Button.ButtonVisible)
                {
                    continue;
                }

                DrawCenteredButtonLabel(sprite, label.Button, label.Text);
            }

            if (_state == null)
            {
                return;
            }

            if (_activePrimaryButton?.ButtonVisible == true && _drawPrimaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activePrimaryButton, _state.PrimaryActionLabel);
            }

            if (_activeSecondaryButton?.ButtonVisible == true && _drawSecondaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeSecondaryButton, _state.SecondaryActionLabel);
            }

            if (_activeTertiaryButton?.ButtonVisible == true && _drawTertiaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeTertiaryButton, _state.TertiaryActionLabel);
            }

            if (_activeQuaternaryButton?.ButtonVisible == true && _drawQuaternaryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeQuaternaryButton, _state.QuaternaryActionLabel);
            }

            if (_activeDeliveryButton?.ButtonVisible == true && _drawDeliveryLabel)
            {
                DrawCenteredButtonLabel(sprite, _activeDeliveryButton, GetDeliveryActionLabel(_state));
            }

            if (_navigationCount > 1)
            {
                string navigationText = $"{_navigationIndex + 1}/{_navigationCount}";
                sprite.DrawString(_font, navigationText, new Vector2(Position.X + 126, Position.Y + Math.Max(16, (CurrentFrame?.Height ?? 396) - 27)), new Color(220, 220, 220));
            }

            DrawHoveredItemTooltip(sprite);
        }

        private void DrawLogPane(SpriteBatch sprite)
        {
            Rectangle clipRect = GetLogClipRectangle();
            float y = Position.Y + GetLogContentBaseY() - _logScrollOffset;
            y = DrawRequirementSection(sprite, clipRect, y, Position.X + ClientContentX, ClientContentWidth);
            y = DrawRewardSection(sprite, clipRect, y, Position.X + ClientContentX, ClientContentWidth);

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                DrawWrappedTextClipped(sprite, _state.HintText, new Vector2(Position.X + ClientContentX, y), ClientContentWidth, new Color(243, 227, 168), clipRect);
            }
        }

        private void DrawSummaryPane(SpriteBatch sprite)
        {
            if (!HasSummaryPaneContent())
            {
                return;
            }

            Rectangle clipRect = GetSummaryClipRectangle();
            float y = Position.Y + ClientSummaryY - _summaryScrollOffset;
            DrawSectionHeaderClipped(sprite, clipRect, _summaryHeaderTexture, "Summary", Position.X + ClientContentX, ref y);
            y = DrawWrappedTextClipped(sprite, _state.SummaryText, new Vector2(Position.X + ClientContentX, y), ClientContentWidth, new Color(228, 228, 228), clipRect);
            y += 8;
            DrawProgressClipped(sprite, clipRect, Position.X + ClientContentX, ref y);
        }

        private void DrawDetailInset(SpriteBatch sprite, int tickCount)
        {
            if (_state == null || !ShouldDrawDetailTip() || _state.TimeLimitSeconds <= 0)
            {
                return;
            }

            Rectangle insetBounds = GetDetailInsetBounds();
            if (insetBounds.Width <= 0 || insetBounds.Height <= 0)
            {
                return;
            }

            int iconInset = 0;
            TimeLimitAnimationFrame? frame = GetActiveTimeLimitIndicatorFrame(tickCount);
            if (frame.HasValue && frame.Value.Texture != null)
            {
                Point iconPosition = new(
                    insetBounds.X + 4 - frame.Value.Origin.X,
                    insetBounds.Y + Math.Max(0, (insetBounds.Height - frame.Value.Texture.Height) / 2) - frame.Value.Origin.Y);
                sprite.Draw(frame.Value.Texture, new Vector2(iconPosition.X, iconPosition.Y), Color.White);
                iconInset = Math.Max(0, (iconPosition.X + frame.Value.Texture.Width) - insetBounds.X) + 6;
            }

            string timerText = FormatRemainingTime(_state.RemainingTimeSeconds);
            Vector2 timerPosition = new(insetBounds.X + Math.Max(10, iconInset), insetBounds.Y + 7);
            sprite.DrawString(_font, timerText, timerPosition, new Color(255, 244, 199));

            DrawTimeLimitGauge(sprite, insetBounds);
        }

        private float DrawRequirementSection(SpriteBatch sprite, Rectangle clipRect, float y, float x, float maxWidth)
        {
            if (!HasRequirementContent())
            {
                return y;
            }

            DrawSectionHeaderClipped(sprite, clipRect, _requirementHeaderTexture, "Requirements", x, ref y);
            if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
            {
                y = DrawConditionLines(sprite, clipRect, _state.RequirementLines, x, y, maxWidth, false);
            }
            else
            {
                y = DrawWrappedTextClipped(sprite, _state.RequirementText, new Vector2(x, y), maxWidth, new Color(215, 228, 215), clipRect);
            }

            return y + 8f;
        }

        private float DrawRewardSection(SpriteBatch sprite, Rectangle clipRect, float y, float x, float maxWidth)
        {
            if (!HasRewardContent())
            {
                return y;
            }

            DrawSectionHeaderClipped(sprite, clipRect, _rewardHeaderTexture, "Rewards", x, ref y);
            if (_state.RewardLines != null && _state.RewardLines.Count > 0)
            {
                y = DrawConditionLines(sprite, clipRect, _state.RewardLines, x, y, maxWidth, true);
            }
            else
            {
                y = DrawWrappedTextClipped(sprite, _state.RewardText, new Vector2(x, y), maxWidth, new Color(232, 220, 176), clipRect);
            }

            return y + 8f;
        }

        private bool HasRequirementContent()
        {
            return !string.IsNullOrWhiteSpace(_state.RequirementText) ||
                   (_state.RequirementLines != null && _state.RequirementLines.Count > 0);
        }

        private bool HasRewardContent()
        {
            return !string.IsNullOrWhiteSpace(_state.RewardText) ||
                   (_state.RewardLines != null && _state.RewardLines.Count > 0);
        }

        private bool HasSummaryPaneContent()
        {
            return !string.IsNullOrWhiteSpace(_state?.SummaryText) || _state?.TotalProgress > 0;
        }

        private void DrawSectionHeaderClipped(SpriteBatch sprite, Rectangle clipRect, Texture2D texture, string fallbackText, float x, ref float y)
        {
            if (texture != null)
            {
                DrawTextureClipped(sprite, texture, new Rectangle((int)x, (int)y, texture.Width, texture.Height), clipRect, Color.White);
                y += texture.Height + 4;
                return;
            }

            DrawTextLineClipped(sprite, fallbackText, new Vector2(x, y), new Color(255, 232, 166), clipRect);
            y += _font.LineSpacing;
        }

        private void DrawProgressClipped(SpriteBatch sprite, Rectangle clipRect, float x, ref float y)
        {
            if (_state.TotalProgress <= 0)
            {
                return;
            }

            string progressText = $"Progress: {Math.Min(_state.CurrentProgress, _state.TotalProgress)}/{_state.TotalProgress}";
            DrawTextLineClipped(sprite, progressText, new Vector2(x, y), new Color(196, 218, 255), clipRect);
            y += _font.LineSpacing + 3;

            if (_progressFrameTexture == null || _progressGaugeTexture == null)
            {
                return;
            }

            Vector2 framePosition = new(Position.X + _progressFrameOffset.X, y);
            DrawTextureClipped(
                sprite,
                _progressFrameTexture,
                new Rectangle((int)framePosition.X, (int)framePosition.Y, _progressFrameTexture.Width, _progressFrameTexture.Height),
                clipRect,
                Color.White);

            float ratio = MathHelper.Clamp(_state.TotalProgress > 0
                ? (float)_state.CurrentProgress / _state.TotalProgress
                : 0f, 0f, 1f);
            int fillWidth = Math.Max(0, (int)Math.Round(ratio * (_progressFrameTexture.Width - 2)));
            if (fillWidth > 0)
            {
                Rectangle destination = new(
                    (int)framePosition.X + 1,
                    (int)framePosition.Y + 1,
                    fillWidth,
                    Math.Max(1, _progressFrameTexture.Height - 2));
                DrawTextureClipped(sprite, _progressGaugeTexture, destination, clipRect, Color.White);

                if (_progressSpotTexture != null)
                {
                    DrawTextureClipped(
                        sprite,
                        _progressSpotTexture,
                        new Rectangle(
                            destination.X + Math.Max(0, destination.Width - _progressSpotTexture.Width),
                            destination.Y,
                            _progressSpotTexture.Width,
                            _progressSpotTexture.Height),
                        clipRect,
                        Color.White);
                }
            }

            y += _progressFrameTexture.Height + 8;
        }

        private float DrawConditionLines(SpriteBatch sprite, Rectangle clipRect, IReadOnlyList<QuestLogLineSnapshot> lines, float x, float y, float maxWidth, bool rewardSection)
        {
            if (lines == null || lines.Count == 0)
            {
                return y;
            }

            foreach (QuestLogLineSnapshot line in lines.Where(line => line != null))
            {
                ConditionRowLayout layout = BuildConditionRowLayout(line, x, y, maxWidth, rewardSection);
                if (layout.RowTexture != null)
                {
                    DrawTextureClipped(sprite, layout.RowTexture, layout.TextureBounds, clipRect, Color.White);
                }

                Color labelColor = rewardSection
                    ? new Color(255, 226, 157)
                    : (line.IsComplete ? new Color(168, 224, 173) : new Color(255, 190, 137));
                Color textColor = rewardSection
                    ? new Color(244, 234, 198)
                    : (line.IsComplete ? new Color(219, 239, 219) : new Color(255, 218, 189));

                DrawTextLineClipped(sprite, line.Label ?? string.Empty, layout.LabelPosition, labelColor, clipRect);
                if (layout.IconTexture != null)
                {
                    DrawTextureClipped(sprite, layout.IconTexture, layout.IconBounds, clipRect, Color.White);
                }

                y = DrawWrappedTextClipped(sprite, line.Text, layout.BodyPosition, layout.BodyMaxWidth, textColor, clipRect);
                if (!string.IsNullOrWhiteSpace(line.ValueText))
                {
                    DrawTextLineClipped(sprite, line.ValueText, layout.ValuePosition, textColor, clipRect);
                }

                y = layout.NextY;
            }

            return y;
        }

        private float DrawWrappedTextClipped(SpriteBatch sprite, string text, Vector2 position, float maxWidth, Color color, Rectangle clipRect)
        {
            float y = position.Y;
            foreach (string line in WrapText(text, maxWidth))
            {
                DrawTextLineClipped(sprite, line, new Vector2(position.X, y), color, clipRect);
                y += _font.LineSpacing;
            }

            return y;
        }

        private IEnumerable<string> WrapText(string text, float maxWidth)
        {
            if (_font == null || string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            foreach (string block in text.Replace("\r", string.Empty).Split('\n'))
            {
                string[] words = block.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    yield return string.Empty;
                    continue;
                }

                string currentLine = string.Empty;
                for (int i = 0; i < words.Length; i++)
                {
                    string candidate = string.IsNullOrEmpty(currentLine) ? words[i] : $"{currentLine} {words[i]}";
                    if (!string.IsNullOrEmpty(currentLine) && _font.MeasureString(candidate).X > maxWidth)
                    {
                        yield return currentLine;
                        currentLine = words[i];
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                }
            }
        }

        private HoveredQuestItemInfo ResolveHoveredQuestItem(int mouseX, int mouseY)
        {
            if (_state == null || !ContainsPoint(mouseX, mouseY))
            {
                return null;
            }

            Rectangle logClipRect = GetLogClipRectangle();
            float y = Position.Y + GetLogContentBaseY() - _logScrollOffset;
            HoveredQuestItemInfo hovered = TryResolveHoveredConditionItem(mouseX, mouseY, logClipRect, _state.RequirementLines, Position.X + ClientContentX, ref y, ClientContentWidth, false);
            if (hovered != null)
            {
                return hovered;
            }

            return TryResolveHoveredConditionItem(mouseX, mouseY, logClipRect, _state.RewardLines, Position.X + ClientContentX, ref y, ClientContentWidth, true);
        }

        private void DrawNoticeSurface(SpriteBatch sprite, int tickCount)
        {
            NoticeSurface? surface = GetActiveNoticeSurface();
            if (!surface.HasValue || surface.Value.Texture == null)
            {
                return;
            }

            Vector2 surfacePosition = new(Position.X + surface.Value.Offset.X, Position.Y + surface.Value.Offset.Y);
            sprite.Draw(surface.Value.Texture, surfacePosition, Color.White);

            NoticeAnimationFrame? animationFrame = GetActiveNoticeAnimationFrame(tickCount);
            if (animationFrame.HasValue && animationFrame.Value.Texture != null)
            {
                sprite.Draw(
                    animationFrame.Value.Texture,
                    new Vector2(Position.X + _noticeAnimationOffset.X, Position.Y + _noticeAnimationOffset.Y),
                    Color.White);
            }
        }

        private HoveredQuestItemInfo TryResolveHoveredConditionItem(int mouseX, int mouseY, Rectangle clipRect, IReadOnlyList<QuestLogLineSnapshot> lines, float x, ref float y, float maxWidth, bool rewardSection)
        {
            if (lines == null || lines.Count == 0)
            {
                return null;
            }

            y = AdvanceSectionHeader(rewardSection ? _rewardHeaderTexture : _requirementHeaderTexture, y);

            foreach (QuestLogLineSnapshot line in lines.Where(line => line != null))
            {
                ConditionRowLayout layout = BuildConditionRowLayout(line, x, y, maxWidth, rewardSection);
                if (line.ItemId.HasValue && clipRect.Contains(mouseX, mouseY) && layout.IconBounds.Contains(mouseX, mouseY))
                {
                    return CreateHoveredQuestItem(line.ItemId.Value, line.Text);
                }

                y = layout.NextY;
            }

            return null;
        }

        private ConditionRowLayout BuildConditionRowLayout(QuestLogLineSnapshot line, float x, float y, float maxWidth, bool rewardSection)
        {
            Texture2D rowTexture = !rewardSection && !line.IsComplete
                ? _incompleteSelectionBarTexture ?? _selectionBarTexture
                : _selectionBarTexture;
            Texture2D iconTexture = line.ItemId.HasValue
                ? ResolveItemIcon(line.ItemId.Value)
                : null;

            float detailX = x + ConditionLabelWidth + ConditionTextInset;
            float detailWidth = Math.Max(48f, maxWidth - (detailX - x));
            float stripWidth = rowTexture != null
                ? Math.Min(rowTexture.Width, detailWidth)
                : detailWidth;
            float stripHeight = rowTexture?.Height ?? 0f;
            float iconWidth = iconTexture != null ? ConditionIconSize : 0f;
            float textLeft = detailX + (rowTexture != null ? ConditionTextInset : 0f);
            if (iconTexture != null)
            {
                textLeft += iconWidth + 4f;
            }

            float valueWidth = string.IsNullOrWhiteSpace(line.ValueText)
                ? 0f
                : _font.MeasureString(line.ValueText).X;
            float textRight = detailX + stripWidth - (rowTexture != null ? ConditionTextInset : 0f);
            float bodyMaxWidth = Math.Max(36f, textRight - textLeft - (valueWidth > 0f ? valueWidth + ConditionValueGap : 0f));
            int lineCount = Math.Max(1, WrapText(line.Text, bodyMaxWidth).Count());
            float bodyHeight = lineCount * _font.LineSpacing;
            float rowHeight = Math.Max(Math.Max(bodyHeight, stripHeight), iconWidth);
            float textureY = y + Math.Max(0f, (rowHeight - stripHeight) / 2f);
            float labelY = y + Math.Max(0f, (rowHeight - _font.LineSpacing) / 2f);
            float iconY = y + Math.Max(0f, (rowHeight - ConditionIconSize) / 2f);
            float bodyY = y + Math.Max(0f, (rowHeight - bodyHeight) / 2f);
            float valueY = y + Math.Max(0f, (rowHeight - _font.LineSpacing) / 2f);

            return new ConditionRowLayout(
                rowTexture,
                new Rectangle((int)detailX, (int)textureY, Math.Max(1, (int)Math.Round(stripWidth)), Math.Max(1, (int)Math.Round(stripHeight))),
                new Vector2(x, labelY),
                iconTexture,
                new Rectangle((int)(detailX + (rowTexture != null ? ConditionTextInset : 0f)), (int)iconY, ConditionIconSize, ConditionIconSize),
                new Vector2(textLeft, bodyY),
                bodyMaxWidth,
                new Vector2(Math.Max(textLeft, textRight - valueWidth), valueY),
                y + rowHeight + ConditionRowGap);
        }

        private float AdvanceSectionHeader(Texture2D texture, float y)
        {
            return y + ((texture?.Height ?? _font.LineSpacing) + 4f);
        }

        private float AdvanceWrappedText(string text, float maxWidth, float y)
        {
            int lineCount = Math.Max(1, WrapText(text, maxWidth).Count());
            return y + (lineCount * _font.LineSpacing);
        }

        private void HandleMouseWheel(Microsoft.Xna.Framework.Input.MouseState mouseState, int wheelDelta)
        {
            if (!ContainsPoint(mouseState.X, mouseState.Y))
            {
                return;
            }

            int direction = wheelDelta > 0 ? -1 : 1;
            int nextScrollOffset = ScrollStep * direction;
            Rectangle summaryRect = GetSummaryClipRectangle();
            Rectangle logRect = GetLogClipRectangle();
            if (summaryRect.Contains(mouseState.X, mouseState.Y) && GetMaxSummaryScrollOffset() > 0)
            {
                _summaryScrollOffset = Math.Clamp(_summaryScrollOffset + nextScrollOffset, 0, GetMaxSummaryScrollOffset());
                return;
            }

            if (logRect.Contains(mouseState.X, mouseState.Y) && GetMaxLogScrollOffset() > 0)
            {
                _logScrollOffset = Math.Clamp(_logScrollOffset + nextScrollOffset, 0, GetMaxLogScrollOffset());
            }
        }

        private Rectangle GetLogClipRectangle()
        {
            return new Rectangle(
                Position.X + (int)ClientContentX,
                Position.Y + GetLogContentBaseY(),
                (int)ClientContentWidth,
                GetClientLogClipHeight());
        }

        private Rectangle GetSummaryClipRectangle()
        {
            return new Rectangle(
                Position.X + (int)ClientContentX,
                Position.Y + ClientSummaryClipY,
                (int)ClientContentWidth,
                ClientSummaryClipHeight);
        }

        private int GetLogContentBaseY()
        {
            return ClientLogBaseY + GetDetailTipHeight();
        }

        private int GetClientScrLogLength()
        {
            int clipHeight = HasSummaryPaneContent()
                ? ClientScrLogLenWithSummary
                : ClientScrLogLenWithoutSummary;
            clipHeight -= GetDetailTipHeight();
            return Math.Max(32, clipHeight);
        }

        private int GetClientLogClipHeight()
        {
            return Math.Max(32, GetClientScrLogLength() - 2);
        }

        private int GetDetailTipHeight()
        {
            return ShouldDrawDetailTip() ? 15 : 0;
        }

        private Rectangle GetDetailInsetBounds()
        {
            return new Rectangle(
                Position.X + (_detailTip != null ? _detailTipOffset.X : (int)ClientContentX),
                Position.Y + (_detailTip != null ? _detailTipOffset.Y : (ClientLogBaseY - 18)),
                _detailTip?.Width ?? Math.Min(154, (int)ClientContentWidth),
                _detailTip?.Height ?? 32);
        }

        private bool ShouldDrawDetailTip()
        {
            return _state?.HasDetailInset == true;
        }

        private void DrawTimeLimitGauge(SpriteBatch sprite, Rectangle insetBounds)
        {
            if (_timeLimitBarBackgroundTexture == null)
            {
                return;
            }

            int barWidth = _timeLimitBarBackgroundTexture.Width;
            int barHeight = _timeLimitBarBackgroundTexture.Height;
            Rectangle backgroundBounds = new(
                insetBounds.Right - barWidth - 8,
                insetBounds.Y + Math.Max(0, (insetBounds.Height - barHeight) / 2),
                barWidth,
                barHeight);
            sprite.Draw(_timeLimitBarBackgroundTexture, new Vector2(backgroundBounds.X, backgroundBounds.Y), Color.White);

            float ratio = _state.TimeLimitSeconds > 0
                ? MathHelper.Clamp(_state.RemainingTimeSeconds / (float)_state.TimeLimitSeconds, 0f, 1f)
                : 0f;
            DrawHorizontalGauge(sprite, backgroundBounds, ratio);
        }

        private void DrawHorizontalGauge(SpriteBatch sprite, Rectangle bounds, float ratio)
        {
            Texture2D leftTexture = _timeLimitGaugeLeftTexture ?? _timeLimitGaugeMiddleTexture;
            Texture2D middleTexture = _timeLimitGaugeMiddleTexture ?? leftTexture;
            Texture2D rightTexture = _timeLimitGaugeRightTexture ?? middleTexture;
            if (leftTexture == null || middleTexture == null || rightTexture == null)
            {
                return;
            }

            int interiorWidth = Math.Max(0, bounds.Width - leftTexture.Width - rightTexture.Width);
            int fillWidth = (int)Math.Round(MathHelper.Clamp(ratio, 0f, 1f) * (leftTexture.Width + interiorWidth + rightTexture.Width));
            if (fillWidth <= 0)
            {
                return;
            }

            int drawX = bounds.X;
            int drawY = bounds.Y + Math.Max(0, (bounds.Height - leftTexture.Height) / 2);
            int leftWidth = Math.Min(fillWidth, leftTexture.Width);
            if (leftWidth > 0)
            {
                sprite.Draw(leftTexture, new Rectangle(drawX, drawY, leftWidth, leftTexture.Height), new Rectangle(0, 0, leftWidth, leftTexture.Height), Color.White);
                drawX += leftWidth;
                fillWidth -= leftWidth;
            }

            int middleWidth = Math.Min(fillWidth, interiorWidth);
            if (middleWidth > 0)
            {
                sprite.Draw(middleTexture, new Rectangle(drawX, drawY, middleWidth, middleTexture.Height), new Rectangle(0, 0, Math.Max(1, Math.Min(middleWidth, middleTexture.Width)), middleTexture.Height), Color.White);
                drawX += middleWidth;
                fillWidth -= middleWidth;
            }

            int rightWidth = Math.Min(fillWidth, rightTexture.Width);
            if (rightWidth > 0)
            {
                sprite.Draw(rightTexture, new Rectangle(drawX, drawY, rightWidth, rightTexture.Height), new Rectangle(0, 0, rightWidth, rightTexture.Height), Color.White);
            }
        }

        private int GetMaxLogScrollOffset()
        {
            return Math.Max(0, (int)Math.Ceiling(GetLogContentHeight() - GetClientLogClipHeight()));
        }

        private int GetMaxSummaryScrollOffset()
        {
            return Math.Max(0, (int)Math.Ceiling(GetSummaryContentHeight() - ClientSummaryHeight));
        }

        private void ClampScrollOffsets()
        {
            _logScrollOffset = Math.Clamp(_logScrollOffset, 0, GetMaxLogScrollOffset());
            _summaryScrollOffset = Math.Clamp(_summaryScrollOffset, 0, GetMaxSummaryScrollOffset());
        }

        private float GetLogContentHeight()
        {
            float y = 0f;
            if (HasRequirementContent())
            {
                y = AdvanceSectionHeader(_requirementHeaderTexture, y);
                y = _state.RequirementLines != null && _state.RequirementLines.Count > 0
                    ? AdvanceConditionLines(_state.RequirementLines, Position.X + ClientContentX, y, ClientContentWidth, false)
                    : AdvanceWrappedText(_state.RequirementText, ClientContentWidth, y);
                y += 8f;
            }

            if (HasRewardContent())
            {
                y = AdvanceSectionHeader(_rewardHeaderTexture, y);
                y = _state.RewardLines != null && _state.RewardLines.Count > 0
                    ? AdvanceConditionLines(_state.RewardLines, Position.X + ClientContentX, y, ClientContentWidth, true)
                    : AdvanceWrappedText(_state.RewardText, ClientContentWidth, y);
                y += 8f;
            }

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                y = AdvanceWrappedText(_state.HintText, ClientContentWidth, y);
            }

            return y;
        }

        private float GetSummaryContentHeight()
        {
            if (!HasSummaryPaneContent())
            {
                return 0f;
            }

            float y = AdvanceSectionHeader(_summaryHeaderTexture, 0f);
            y = AdvanceWrappedText(_state.SummaryText, ClientContentWidth, y);
            y += 8f;
            if (_state.TotalProgress > 0)
            {
                y += _font.LineSpacing + 3f;
                if (_progressFrameTexture != null)
                {
                    y += _progressFrameTexture.Height + 8f;
                }
            }

            return y;
        }

        private float AdvanceConditionLines(IReadOnlyList<QuestLogLineSnapshot> lines, float x, float y, float maxWidth, bool rewardSection)
        {
            foreach (QuestLogLineSnapshot line in lines.Where(line => line != null))
            {
                y = BuildConditionRowLayout(line, x, y, maxWidth, rewardSection).NextY;
            }

            return y;
        }

        private void DrawTextureClipped(SpriteBatch sprite, Texture2D texture, Rectangle destination, Rectangle clipRect, Color color)
        {
            if (texture == null || destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            Rectangle intersected = Rectangle.Intersect(destination, clipRect);
            if (intersected.Width <= 0 || intersected.Height <= 0)
            {
                return;
            }

            float sourceScaleX = texture.Width / (float)Math.Max(1, destination.Width);
            float sourceScaleY = texture.Height / (float)Math.Max(1, destination.Height);
            Rectangle sourceRect = new(
                (int)Math.Round((intersected.X - destination.X) * sourceScaleX),
                (int)Math.Round((intersected.Y - destination.Y) * sourceScaleY),
                Math.Max(1, (int)Math.Round(intersected.Width * sourceScaleX)),
                Math.Max(1, (int)Math.Round(intersected.Height * sourceScaleY)));
            sourceRect.Width = Math.Min(sourceRect.Width, texture.Width - sourceRect.X);
            sourceRect.Height = Math.Min(sourceRect.Height, texture.Height - sourceRect.Y);
            sprite.Draw(texture, intersected, sourceRect, color);
        }

        private void DrawTextLineClipped(SpriteBatch sprite, string text, Vector2 position, Color color, Rectangle clipRect)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Rectangle lineRect = new((int)position.X, (int)position.Y, Math.Max(1, (int)Math.Ceiling(_font.MeasureString(text).X)), _font.LineSpacing);
            if (!lineRect.Intersects(clipRect))
            {
                return;
            }

            sprite.DrawString(_font, text, position, color);
        }

        private HoveredQuestItemInfo CreateHoveredQuestItem(int itemId, string lineText)
        {
            return new HoveredQuestItemInfo
            {
                ItemId = itemId,
                Title = ResolveItemName(itemId),
                Subtitle = lineText,
                Description = ResolveItemDescription(itemId),
                Icon = ResolveItemIcon(itemId)
            };
        }

        private Texture2D ResolveItemIcon(int itemId)
        {
            if (itemId <= 0)
            {
                return null;
            }

            Texture2D providerTexture = _itemIconProvider?.Invoke(itemId);
            if (providerTexture != null)
            {
                return providerTexture;
            }

            if (_itemIconCache.TryGetValue(itemId, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            if (!InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                _itemIconCache[itemId] = null;
                return null;
            }

            MapleLib.WzLib.WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            itemImage?.ParseImage();
            string itemText = category == "Character" ? itemId.ToString("D8") : itemId.ToString("D7");
            MapleLib.WzLib.WzProperties.WzSubProperty itemProperty = itemImage?[itemText] as MapleLib.WzLib.WzProperties.WzSubProperty;
            MapleLib.WzLib.WzProperties.WzSubProperty infoProperty = itemProperty?["info"] as MapleLib.WzLib.WzProperties.WzSubProperty;
            MapleLib.WzLib.WzProperties.WzCanvasProperty iconCanvas = infoProperty?["iconRaw"] as MapleLib.WzLib.WzProperties.WzCanvasProperty
                                                                      ?? infoProperty?["icon"] as MapleLib.WzLib.WzProperties.WzCanvasProperty;
            Texture2D texture = iconCanvas?.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(_pixel?.GraphicsDevice);
            _itemIconCache[itemId] = texture;
            return texture;
        }

        private static string ResolveItemName(int itemId)
        {
            if (itemId <= 0)
            {
                return string.Empty;
            }

            return InventoryItemMetadataResolver.TryResolveItemName(itemId, out string itemName)
                ? itemName
                : $"Item #{itemId}";
        }

        private static string ResolveItemDescription(int itemId)
        {
            return itemId > 0 && InventoryItemMetadataResolver.TryResolveItemDescription(itemId, out string description)
                ? description
                : string.Empty;
        }

        private void DrawHoveredItemTooltip(SpriteBatch sprite)
        {
            if (_hoveredQuestItem == null || _font == null || _pixel == null)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(_hoveredQuestItem.Title) ? $"Item #{_hoveredQuestItem.ItemId}" : _hoveredQuestItem.Title;
            const int tooltipWidth = 220;
            const int padding = 8;
            const int iconSize = 28;
            const int gap = 8;
            float titleWidth = tooltipWidth - (padding * 2);
            float bodyWidth = tooltipWidth - ((padding * 2) + iconSize + gap);

            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            string[] wrappedSubtitle = WrapTooltipText(_hoveredQuestItem.Subtitle, bodyWidth);
            string[] wrappedDescription = WrapTooltipText(_hoveredQuestItem.Description, bodyWidth);

            float titleHeight = wrappedTitle.Length * _font.LineSpacing;
            float subtitleHeight = wrappedSubtitle.Length * _font.LineSpacing;
            float descriptionHeight = wrappedDescription.Length * _font.LineSpacing;
            float bodyHeight = subtitleHeight + (descriptionHeight > 0f ? 4f + descriptionHeight : 0f);
            int tooltipHeight = (int)Math.Ceiling((padding * 2) + titleHeight + 6f + Math.Max(iconSize, bodyHeight));

            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            int tooltipX = _lastMousePosition.X + 18;
            int tooltipY = _lastMousePosition.Y + 18;
            if (tooltipX + tooltipWidth > viewportWidth - 4)
            {
                tooltipX = Math.Max(4, _lastMousePosition.X - tooltipWidth - 18);
            }

            if (tooltipY + tooltipHeight > viewportHeight - 4)
            {
                tooltipY = Math.Max(4, _lastMousePosition.Y - tooltipHeight - 18);
            }

            Rectangle backgroundRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);
            sprite.Draw(_pixel, backgroundRect, new Color(18, 24, 37, 235));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Y, backgroundRect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Bottom - 1, backgroundRect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.X, backgroundRect.Y, 1, backgroundRect.Height), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(backgroundRect.Right - 1, backgroundRect.Y, 1, backgroundRect.Height), new Color(112, 146, 201));

            float textY = tooltipY + padding;
            DrawTooltipLines(sprite, wrappedTitle, new Vector2(tooltipX + padding, textY), new Color(255, 220, 120));
            textY += titleHeight + 6f;

            if (_hoveredQuestItem.Icon != null)
            {
                sprite.Draw(_hoveredQuestItem.Icon, new Rectangle(tooltipX + padding, (int)textY, iconSize, iconSize), Color.White);
            }

            float bodyX = tooltipX + padding + iconSize + gap;
            DrawTooltipLines(sprite, wrappedSubtitle, new Vector2(bodyX, textY), new Color(228, 233, 242));
            if (descriptionHeight > 0f)
            {
                DrawTooltipLines(sprite, wrappedDescription, new Vector2(bodyX, textY + subtitleHeight + 4f), new Color(199, 206, 218));
            }
        }

        private void DrawTooltipLines(SpriteBatch sprite, IReadOnlyList<string> lines, Vector2 position, Color color)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                sprite.DrawString(_font, lines[i], new Vector2(position.X, position.Y + (i * _font.LineSpacing)), color);
            }
        }

        private TimeLimitAnimationFrame? GetActiveTimeLimitIndicatorFrame(int tickCount)
        {
            string styleKey = string.IsNullOrWhiteSpace(_state?.TimerUiKey) ? "default" : _state.TimerUiKey;
            if (!_timeLimitIndicatorStyles.TryGetValue(styleKey, out TimeLimitIndicatorStyle style) &&
                !_timeLimitIndicatorStyles.TryGetValue("default", out style))
            {
                return null;
            }

            if (style.Frames.Length == 0)
            {
                return null;
            }

            int normalizedTick = ((tickCount % style.TotalDurationMs) + style.TotalDurationMs) % style.TotalDurationMs;
            int elapsed = 0;
            for (int i = 0; i < style.Frames.Length; i++)
            {
                elapsed += style.Frames[i].DelayMs;
                if (normalizedTick < elapsed)
                {
                    return style.Frames[i];
                }
            }

            return style.Frames[^1];
        }

        private static string FormatRemainingTime(int totalSeconds)
        {
            int seconds = Math.Max(0, totalSeconds);
            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            return duration.TotalHours >= 1d
                ? string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", (int)duration.TotalHours, duration.Minutes, duration.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", duration.Minutes, duration.Seconds);
        }

        private string[] WrapTooltipText(string text, float maxWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return WrapText(text, maxWidth).ToArray();
        }

        private void DrawCenteredButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            int width = Math.Max(1, button.CanvasSnapshotWidth);
            int height = Math.Max(1, button.CanvasSnapshotHeight);
            Vector2 textSize = _font.MeasureString(text);
            float x = Position.X + button.X + ((width - textSize.X) / 2f);
            float y = Position.Y + button.Y + ((height - textSize.Y) / 2f) - 1f;
            sprite.DrawString(_font, text, new Vector2(x, y), Color.White);
        }

        private void BindNpcButton(QuestWindowDetailState state)
        {
            if (state == null || state.TertiaryAction != QuestWindowActionKind.LocateNpc)
            {
                return;
            }

            ActionButtonBinding? npcBinding = null;
            if (state.NpcButtonStyle != QuestDetailNpcButtonStyle.None &&
                _npcButtons.TryGetValue(state.NpcButtonStyle, out ActionButtonBinding styledBinding))
            {
                npcBinding = styledBinding;
            }
            else if (_npcButtons.TryGetValue(QuestDetailNpcButtonStyle.GenericNpc, out ActionButtonBinding genericBinding))
            {
                npcBinding = genericBinding;
            }
            else if (_npcButtons.Count > 0)
            {
                npcBinding = _npcButtons.Values.First();
            }

            if (!npcBinding.HasValue)
            {
                return;
            }

            npcBinding.Value.Button.SetVisible(true);
            npcBinding.Value.Button.SetButtonState(state.TertiaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
            _activeTertiaryButton = npcBinding.Value.Button;
            _drawTertiaryLabel = npcBinding.Value.DrawLabel;
        }

        private void BindQuaternaryActionButton(QuestWindowDetailState state)
        {
            if (state == null || state.QuaternaryAction == QuestWindowActionKind.None)
            {
                return;
            }

            if (!_actionButtons.TryGetValue(state.QuaternaryAction, out ActionButtonBinding binding))
            {
                return;
            }

            binding.Button.SetVisible(true);
            binding.Button.SetButtonState(state.QuaternaryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
            _activeQuaternaryButton = binding.Button;
            _drawQuaternaryLabel = binding.DrawLabel;
        }

        private void LayoutActionButtons()
        {
            List<UIObject> orderedButtons = new();
            AppendDistinctVisibleButton(orderedButtons, _activeDeliveryButton);
            AppendDistinctVisibleButton(orderedButtons, _activeQuaternaryButton);
            AppendDistinctVisibleButton(orderedButtons, _activeTertiaryButton);
            AppendDistinctVisibleButton(orderedButtons, _activeSecondaryButton);
            AppendDistinctVisibleButton(orderedButtons, _activePrimaryButton);

            if (orderedButtons.Count == 0)
            {
                return;
            }

            int frameWidth = CurrentFrame?.Width ?? 296;
            int frameHeight = CurrentFrame?.Height ?? 396;
            int cursorX = frameWidth - 12;

            for (int i = orderedButtons.Count - 1; i >= 0; i--)
            {
                UIObject button = orderedButtons[i];
                int buttonWidth = Math.Max(1, button.CanvasSnapshotWidth);
                int buttonHeight = Math.Max(1, button.CanvasSnapshotHeight);
                button.X = Math.Max(12, cursorX - buttonWidth);
                button.Y = Math.Max(16, frameHeight - buttonHeight - 10);
                cursorX = button.X - 8;
            }
        }

        private void BindDeliveryActionButton(QuestWindowDetailState state)
        {
            QuestWindowActionKind action = ResolveDeliveryAction(state);
            if (action == QuestWindowActionKind.None || !_actionButtons.TryGetValue(action, out ActionButtonBinding binding))
            {
                return;
            }

            binding.Button.SetVisible(true);
            binding.Button.SetButtonState(state.DeliveryActionEnabled ? UIObjectState.Normal : UIObjectState.Disabled);
            _activeDeliveryButton = binding.Button;
            _drawDeliveryLabel = binding.DrawLabel;
        }

        private static QuestWindowActionKind ResolveDeliveryAction(QuestWindowDetailState state)
        {
            return state?.DeliveryType switch
            {
                QuestDetailDeliveryType.Accept => QuestWindowActionKind.QuestDeliveryAccept,
                QuestDetailDeliveryType.Complete => QuestWindowActionKind.QuestDeliveryComplete,
                _ => QuestWindowActionKind.None
            };
        }

        private static string GetDeliveryActionLabel(QuestWindowDetailState state)
        {
            if (!string.IsNullOrWhiteSpace(state?.DeliveryCashItemName))
            {
                return state.DeliveryCashItemName;
            }

            return ResolveDeliveryAction(state) switch
            {
                QuestWindowActionKind.QuestDeliveryAccept => "Delivery",
                QuestWindowActionKind.QuestDeliveryComplete => "Delivery",
                _ => string.Empty
            };
        }

        private static void AppendDistinctVisibleButton(ICollection<UIObject> buttons, UIObject button)
        {
            if (button == null || !button.ButtonVisible || buttons.Contains(button))
            {
                return;
            }

            buttons.Add(button);
        }

        private NoticeSurface? GetActiveNoticeSurface()
        {
            if (_state == null || _noticeSurfaces.Length == 0)
            {
                return null;
            }

            int surfaceIndex = _state.State switch
            {
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Completed => 3,
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Started when _state.PrimaryAction == QuestWindowActionKind.Complete && _state.PrimaryActionEnabled => 2,
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Started => 1,
                MapleLib.WzLib.WzStructure.Data.QuestStructure.QuestStateType.Not_Started => 0,
                _ => -1
            };

            if (surfaceIndex < 0 || surfaceIndex >= _noticeSurfaces.Length)
            {
                return null;
            }

            return _noticeSurfaces[surfaceIndex];
        }

        private NoticeAnimationFrame? GetActiveNoticeAnimationFrame(int tickCount)
        {
            if (_noticeAnimationFrames.Length == 0)
            {
                return null;
            }

            int totalDuration = 0;
            for (int i = 0; i < _noticeAnimationFrames.Length; i++)
            {
                totalDuration += _noticeAnimationFrames[i].DelayMs;
            }

            if (totalDuration <= 0)
            {
                return _noticeAnimationFrames[0];
            }

            int normalizedTick = ((tickCount % totalDuration) + totalDuration) % totalDuration;
            int elapsed = 0;
            for (int i = 0; i < _noticeAnimationFrames.Length; i++)
            {
                elapsed += _noticeAnimationFrames[i].DelayMs;
                if (normalizedTick < elapsed)
                {
                    return _noticeAnimationFrames[i];
                }
            }

            return _noticeAnimationFrames[_noticeAnimationFrames.Length - 1];
        }

        private readonly struct ActionButtonBinding
        {
            public ActionButtonBinding(UIObject button, bool drawLabel)
            {
                Button = button;
                DrawLabel = drawLabel;
            }

            public UIObject Button { get; }
            public bool DrawLabel { get; }
        }

        private readonly struct ConditionRowLayout
        {
            public ConditionRowLayout(
                Texture2D rowTexture,
                Rectangle textureBounds,
                Vector2 labelPosition,
                Texture2D iconTexture,
                Rectangle iconBounds,
                Vector2 bodyPosition,
                float bodyMaxWidth,
                Vector2 valuePosition,
                float nextY)
            {
                RowTexture = rowTexture;
                TextureBounds = textureBounds;
                LabelPosition = labelPosition;
                IconTexture = iconTexture;
                IconBounds = iconBounds;
                BodyPosition = bodyPosition;
                BodyMaxWidth = bodyMaxWidth;
                ValuePosition = valuePosition;
                NextY = nextY;
            }

            public Texture2D RowTexture { get; }
            public Rectangle TextureBounds { get; }
            public Vector2 LabelPosition { get; }
            public Texture2D IconTexture { get; }
            public Rectangle IconBounds { get; }
            public Vector2 BodyPosition { get; }
            public float BodyMaxWidth { get; }
            public Vector2 ValuePosition { get; }
            public float NextY { get; }
        }

        private readonly struct ButtonLabel
        {
            public ButtonLabel(UIObject button, string text)
            {
                Button = button;
                Text = text;
            }

            public UIObject Button { get; }
            public string Text { get; }
        }

        private readonly struct NoticeSurface
        {
            public NoticeSurface(Texture2D texture, Point offset)
            {
                Texture = texture;
                Offset = offset;
            }

            public Texture2D Texture { get; }
            public Point Offset { get; }
        }

        private readonly struct NoticeAnimationFrame
        {
            public NoticeAnimationFrame(Texture2D texture, int delayMs)
            {
                Texture = texture;
                DelayMs = delayMs;
            }

            public Texture2D Texture { get; }
            public int DelayMs { get; }
        }

        private readonly struct TimeLimitIndicatorStyle
        {
            public TimeLimitIndicatorStyle(TimeLimitAnimationFrame[] frames, int totalDurationMs)
            {
                Frames = frames ?? Array.Empty<TimeLimitAnimationFrame>();
                TotalDurationMs = Math.Max(1, totalDurationMs);
            }

            public TimeLimitAnimationFrame[] Frames { get; }
            public int TotalDurationMs { get; }
        }

        private readonly struct TimeLimitAnimationFrame
        {
            public TimeLimitAnimationFrame(Texture2D texture, Point origin, int delayMs)
            {
                Texture = texture;
                Origin = origin;
                DelayMs = delayMs;
            }

            public Texture2D Texture { get; }
            public Point Origin { get; }
            public int DelayMs { get; }
        }
    }
}
