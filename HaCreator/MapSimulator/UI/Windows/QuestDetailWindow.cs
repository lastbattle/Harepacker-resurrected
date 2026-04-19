using HaCreator.MapSimulator.Interaction;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using Spine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SD = System.Drawing;

namespace HaCreator.MapSimulator.UI
{
    public sealed class QuestDetailWindow : UIWindowBase
    {
        private const float ClientContentX = 18f;
        // CUIQuestInfoDetail::Draw clips body CT rows at x=18 but draws each entry at ct.x + 17.
        private const float ClientTextArrayX = 17f;
        private const float ClientContentWidth = 253f;
        private const int ClientLogBaseY = 128;
        // Log CT rows are drawn one pixel above the clip top; the clip still starts at ClientLogBaseY.
        private const int ClientLogTextArrayBaseY = 127;
        private const int ClientSummaryClipY = 252;
        private const int ClientSummaryClipHeight = 111;
        private const int ClientScrLogLenWithSummary = 120;
        private const int ClientScrLogLenWithoutSummary = 238;
        private const float ClientTitleX = 35f;
        private const float ClientTitleY = 42f;
        private const float ClientHeaderNoteX = 35f;
        private const float ClientHeaderNoteY = 56f;
        private const float ClientMateHeaderNoteY = 45f;
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
        // CUIQuestInfoDetail accumulates body rows by CT height; keep section tail spacing neutral.
        private const float ClientSectionTailGap = 0f;
        private const int ConditionIconSize = 18;
        private const float ConditionSectionBodyGap = 6f;
        private const float ClientTitleScale = 0.74f;
        private const float ClientHeaderScale = 0.58f;
        private const float ClientDetailScale = 0.58f;
        private const float ClientNavigationScale = 0.52f;
        private const int TOOLTIP_PADDING = 8;
        private const int TOOLTIP_ICON_SIZE = 28;
        private const int TOOLTIP_GAP = 8;
        private const int TOOLTIP_OFFSET_X = 18;
        private const int TOOLTIP_OFFSET_Y = 18;
        private const int TOOLTIP_SECTION_GAP = 4;
        private const int TOOLTIP_FALLBACK_WIDTH = 220;
        private static readonly Regex RichTextTokenRegex = new(
            @"(\{\{ITEMICON:\d+\}\}|\{\{UICANVAS:[^}]+\}\}|\{\{QUESTSURFACE:[^}]+\}\}|\{\{QUESTSTYLE:[^}]+\}\}|\{\{QUESTREF:[^}]+\}\}|\{\{QUESTFONT:[^}]+\}\}|\{\{QUESTFONTSIZE:-?\d+(?:\.\d+)?\}\}|\r?\n|\s+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string _windowName;
        private readonly List<ButtonLabel> _buttonLabels = new();
        private readonly Dictionary<QuestWindowActionKind, ActionButtonBinding> _actionButtons = new();
        private readonly Dictionary<QuestDetailNpcButtonStyle, ActionButtonBinding> _npcButtons = new();
        private readonly Dictionary<string, TimeLimitIndicatorStyle> _timeLimitIndicatorStyles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> _questSurfaceTextures = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Texture2D> _inlineUiCanvasTextures = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Point> _inlineUiCanvasOrigins = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ClientTextRasterizer> _customDetailTextRasterizers = new(StringComparer.Ordinal);
        private readonly Texture2D[] _tooltipFrames = new Texture2D[3];
        private readonly Point[] _tooltipFrameOrigins = new Point[3];

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
        private QuestDetailInlineReference? _hoveredInlineReference;
        private MouseState _previousInlineReferenceMouseState;
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
        private ClientTextRasterizer _clientTitleTextRasterizer;
        private ClientTextRasterizer _clientHeaderTextRasterizer;
        private ClientTextRasterizer _clientDetailTextRasterizer;
        private ClientTextRasterizer _clientDetailBoldTextRasterizer;
        private ClientTextRasterizer _clientNavigationTextRasterizer;
        private ClientTextRasterizer _clientButtonTextRasterizer;

        public QuestDetailWindow(IDXObject frame, string windowName)
            : base(frame)
        {
            _windowName = windowName;
        }

        public override string WindowName => _windowName;

        internal event Action PreviousRequested;
        internal event Action NextRequested;
        internal event Action<QuestWindowActionKind> ActionRequested;
        internal event Action<QuestDetailInlineReference> InlineReferenceRequested;

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
            _questSurfaceTextures.Clear();
            RegisterQuestSurfaceTexture("summary", summaryHeaderTexture);
            RegisterQuestSurfaceTexture("basic", requirementHeaderTexture);
            RegisterQuestSurfaceTexture("reward", rewardHeaderTexture);
            RegisterQuestSurfaceTexture("select", selectionBarTexture);
            RegisterQuestSurfaceTexture("prob", incompleteSelectionBarTexture ?? selectionBarTexture);
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

        public void SetTooltipTextures(Texture2D[] tooltipFrames)
        {
            if (tooltipFrames == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_tooltipFrames.Length, tooltipFrames.Length); i++)
            {
                _tooltipFrames[i] = tooltipFrames[i];
            }
        }

        public void SetTooltipOrigins(Point[] tooltipOrigins)
        {
            if (tooltipOrigins == null)
            {
                return;
            }

            for (int i = 0; i < Math.Min(_tooltipFrameOrigins.Length, tooltipOrigins.Length); i++)
            {
                _tooltipFrameOrigins[i] = tooltipOrigins[i];
            }
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
                primaryBinding.Button.SetButtonState(ResolveButtonState(state.PrimaryActionEnabled, state.PrimaryActionSelected));
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
                _hoveredInlineReference = null;
                _previousScrollWheelValue = mouseState.ScrollWheelValue;
                _previousInlineReferenceMouseState = mouseState;
                return;
            }

            int wheelDelta = mouseState.ScrollWheelValue - _previousScrollWheelValue.Value;
            _previousScrollWheelValue = mouseState.ScrollWheelValue;
            if (wheelDelta != 0)
            {
                HandleMouseWheel(mouseState, wheelDelta);
            }

            _hoveredQuestItem = ResolveHoveredQuestItem(mouseState.X, mouseState.Y);
            _hoveredInlineReference = ResolveHoveredInlineReference(mouseState.X, mouseState.Y);
            QuestDetailInlineReference? clickedReference = ResolveClickedQuestDetailReference();
            if (clickedReference.HasValue &&
                _previousInlineReferenceMouseState.LeftButton == ButtonState.Pressed &&
                mouseState.LeftButton == ButtonState.Released)
            {
                InlineReferenceRequested?.Invoke(clickedReference.Value);
            }

            _previousInlineReferenceMouseState = mouseState;
        }

        private QuestDetailInlineReference? ResolveClickedQuestDetailReference()
        {
            if (_hoveredInlineReference.HasValue)
            {
                return _hoveredInlineReference.Value;
            }

            if (_hoveredQuestItem != null && _hoveredQuestItem.ItemId > 0)
            {
                return new QuestDetailInlineReference(
                    QuestDetailInlineReferenceKind.Item,
                    _hoveredQuestItem.ItemId,
                    _hoveredQuestItem.Title);
            }

            return null;
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

            DrawTextLine(
                sprite,
                _state.Title,
                new Vector2(Position.X + ClientTitleX, Position.Y + ClientTitleY),
                Color.White,
                ClientTitleScale,
                lane: QuestDetailTextLane.Title);

            DrawHeaderNote(sprite, drawMateNameHeader: false);

            if (!string.IsNullOrWhiteSpace(_state.NpcText))
            {
                DrawTextLine(
                    sprite,
                    _state.NpcText,
                    new Vector2(Position.X + ClientNpcX, Position.Y + ClientNpcY),
                    new Color(214, 214, 171),
                    ClientHeaderScale,
                    lane: QuestDetailTextLane.Header);
            }

            DrawDetailInset(sprite, TickCount);
            DrawNoticeSurface(sprite, TickCount);
            DrawLogPane(sprite);
            DrawSummaryPanel(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo);
            DrawSummaryPane(sprite);
            DrawHeaderNote(sprite, drawMateNameHeader: true);
        }

        private void DrawSummaryPanel(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo)
        {
            if (_summaryPanel == null || !HasSummaryPaneContent())
            {
                return;
            }

            _summaryPanel.DrawBackground(
                sprite,
                skeletonMeshRenderer,
                gameTime,
                Position.X + _summaryPanelOffset.X,
                Position.Y + _summaryPanelOffset.Y,
                Color.White,
                false,
                drawReflectionInfo);
        }

        private void DrawHeaderNote(SpriteBatch sprite, bool drawMateNameHeader)
        {
            if (string.IsNullOrWhiteSpace(_state?.HeaderNoteText))
            {
                return;
            }

            bool isMateNameHeader = _state.QuestId == QuestWindowDetailState.MateNameHeaderQuestId;
            if (isMateNameHeader != drawMateNameHeader)
            {
                return;
            }

            float headerNoteY = isMateNameHeader
                ? ClientMateHeaderNoteY
                : ClientHeaderNoteY;
            DrawTextLine(
                sprite,
                _state.HeaderNoteText,
                new Vector2(Position.X + ClientHeaderNoteX, Position.Y + headerNoteY),
                new Color(244, 232, 192),
                ClientHeaderScale,
                lane: QuestDetailTextLane.Header);
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
                DrawTextLine(
                    sprite,
                    navigationText,
                    new Vector2(Position.X + 126, Position.Y + Math.Max(16, (CurrentFrame?.Height ?? 396) - 27)),
                    new Color(220, 220, 220),
                    ClientNavigationScale,
                    lane: QuestDetailTextLane.Navigation);
            }

            DrawHoveredItemTooltip(sprite);
        }

        private void DrawLogPane(SpriteBatch sprite)
        {
            Rectangle clipRect = GetLogClipRectangle();
            float y = Position.Y + GetLogTextArrayBaseY() - _logScrollOffset;
            y = DrawRequirementSection(sprite, clipRect, y, Position.X + ClientTextArrayX, ClientContentWidth);
            y = DrawRewardSection(sprite, clipRect, y, Position.X + ClientTextArrayX, ClientContentWidth);

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                DrawRichTextClipped(
                    sprite,
                    _state.HintText,
                    new Vector2(Position.X + ClientTextArrayX, y),
                    ClientContentWidth,
                    new Color(243, 227, 168),
                    clipRect,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
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
            DrawSectionHeaderClipped(sprite, clipRect, _summaryHeaderTexture, "Summary", Position.X + ClientTextArrayX, ref y, ClientDetailScale);
            y = DrawRichTextClipped(
                sprite,
                _state.SummaryText,
                new Vector2(Position.X + ClientTextArrayX, y),
                ClientContentWidth,
                new Color(228, 228, 228),
                clipRect,
                ClientDetailScale,
                QuestDetailTextLane.Detail);
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
            DrawTextLine(sprite, timerText, timerPosition, new Color(255, 244, 199), ClientDetailScale, lane: QuestDetailTextLane.Detail);

            DrawTimeLimitGauge(sprite, insetBounds);
        }

        private float DrawRequirementSection(SpriteBatch sprite, Rectangle clipRect, float y, float x, float maxWidth)
        {
            if (!HasRequirementContent())
            {
                return y;
            }

            DrawSectionHeaderClipped(sprite, clipRect, _requirementHeaderTexture, "Requirements", x, ref y, ClientDetailScale);
            if (!string.IsNullOrWhiteSpace(_state.RequirementText))
            {
                y = DrawRichTextClipped(
                    sprite,
                    _state.RequirementText,
                    new Vector2(x, y),
                    maxWidth,
                    new Color(215, 228, 215),
                    clipRect,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
                if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
                {
                    y += ConditionSectionBodyGap;
                }
            }

            if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
            {
                y = DrawConditionLines(sprite, clipRect, _state.RequirementLines, x, y, maxWidth, false);
            }

            return y + ClientSectionTailGap;
        }

        private float DrawRewardSection(SpriteBatch sprite, Rectangle clipRect, float y, float x, float maxWidth)
        {
            if (!HasRewardContent())
            {
                return y;
            }

            DrawSectionHeaderClipped(sprite, clipRect, _rewardHeaderTexture, "Rewards", x, ref y, ClientDetailScale);
            if (!string.IsNullOrWhiteSpace(_state.RewardText))
            {
                y = DrawRichTextClipped(
                    sprite,
                    _state.RewardText,
                    new Vector2(x, y),
                    maxWidth,
                    new Color(232, 220, 176),
                    clipRect,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
                if (_state.RewardLines != null && _state.RewardLines.Count > 0)
                {
                    y += ConditionSectionBodyGap;
                }
            }

            if (_state.RewardLines != null && _state.RewardLines.Count > 0)
            {
                y = DrawConditionLines(sprite, clipRect, _state.RewardLines, x, y, maxWidth, true);
            }

            return y + ClientSectionTailGap;
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

        private void DrawSectionHeaderClipped(SpriteBatch sprite, Rectangle clipRect, Texture2D texture, string fallbackText, float x, ref float y, float scale)
        {
            if (texture != null)
            {
                DrawTextureClipped(sprite, texture, new Rectangle((int)x, (int)y, texture.Width, texture.Height), clipRect, Color.White);
                y += texture.Height + 4;
                return;
            }

            DrawTextLineClipped(sprite, fallbackText, new Vector2(x, y), new Color(255, 232, 166), clipRect, scale, lane: QuestDetailTextLane.Header);
            y += GetLineHeight(scale, QuestDetailTextLane.Header);
        }

        private void DrawProgressClipped(SpriteBatch sprite, Rectangle clipRect, float x, ref float y)
        {
            if (_state.TotalProgress <= 0)
            {
                return;
            }

            string progressText = $"Progress: {Math.Min(_state.CurrentProgress, _state.TotalProgress)}/{_state.TotalProgress}";
            DrawTextLineClipped(sprite, progressText, new Vector2(x, y), new Color(196, 218, 255), clipRect, ClientDetailScale, lane: QuestDetailTextLane.DetailStrong);
            y += GetLineHeight(ClientDetailScale) + 3;

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

                DrawTextLineClipped(sprite, line.Label ?? string.Empty, layout.LabelPosition, labelColor, clipRect, ClientDetailScale, lane: QuestDetailTextLane.DetailStrong);
                if (layout.IconTexture != null)
                {
                    DrawTextureClipped(sprite, layout.IconTexture, layout.IconBounds, clipRect, Color.White);
                }

                y = DrawRichTextClipped(
                    sprite,
                    line.Text,
                    layout.BodyPosition,
                    layout.BodyMaxWidth,
                    textColor,
                    clipRect,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
                if (!string.IsNullOrWhiteSpace(line.ValueText))
                {
                    DrawTextLineClipped(sprite, line.ValueText, layout.ValuePosition, textColor, clipRect, ClientDetailScale, lane: QuestDetailTextLane.DetailStrong);
                }

                y = layout.NextY;
            }

            return y;
        }

        private float DrawWrappedTextClipped(
            SpriteBatch sprite,
            string text,
            Vector2 position,
            float maxWidth,
            Color color,
            Rectangle clipRect,
            float scale,
            QuestDetailTextLane lane)
        {
            float y = position.Y;
            foreach (string line in WrapText(text, maxWidth, scale, lane))
            {
                DrawTextLineClipped(sprite, line, new Vector2(position.X, y), color, clipRect, scale, lane: lane);
                y += GetLineHeight(scale);
            }

            return y;
        }

        private float DrawRichTextClipped(
            SpriteBatch sprite,
            string text,
            Vector2 position,
            float maxWidth,
            Color color,
            Rectangle clipRect,
            float scale,
            QuestDetailTextLane lane)
        {
            return LayoutRichText(
                text,
                position,
                maxWidth,
                scale,
                lane,
                (token, drawPosition, drawStyle) =>
                {
                    if (token.Texture != null)
                    {
                        DrawTextureClipped(
                            sprite,
                            token.Texture,
                            new Rectangle(
                                (int)Math.Round(drawPosition.X + token.DrawOffsetX),
                                (int)Math.Round(drawPosition.Y + token.DrawOffsetY),
                                token.Width,
                                token.Height),
                            clipRect,
                            Color.White);
                    }
                    else if (!string.IsNullOrEmpty(token.Text))
                    {
                        Color tokenColor = token.Kind == RichTextTokenKind.Reference
                            ? new Color(164, 238, 255)
                            : drawStyle.Color;
                        DrawTextLineClipped(
                            sprite,
                            token.Text,
                            drawPosition,
                            tokenColor,
                            clipRect,
                            scale,
                            drawStyle.Emphasized,
                            lane,
                            drawStyle.FontFamily,
                            drawStyle.FontPixelSize);
                    }
                },
                color);
        }

        private float AdvanceRichText(string text, float maxWidth, float scale, QuestDetailTextLane lane)
        {
            return LayoutRichText(text, Vector2.Zero, maxWidth, scale, lane, null, Color.White);
        }

        private IEnumerable<string> WrapText(string text, float maxWidth, float scale, QuestDetailTextLane lane = QuestDetailTextLane.Detail)
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
                    if (!string.IsNullOrEmpty(currentLine) && MeasureText(candidate, scale, false, lane).X > maxWidth)
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

        private float LayoutRichText(
            string text,
            Vector2 position,
            float maxWidth,
            float scale,
            QuestDetailTextLane lane,
            Action<RichTextToken, Vector2, RichTextStyleState> drawToken,
            Color defaultColor)
        {
            float baselineHeight = Math.Max(1f, GetLineHeight(scale, lane));
            float currentX = position.X;
            float currentY = position.Y;
            float lineStartX = position.X;
            float lineHeight = baselineHeight;
            RichTextStyleState currentStyle = new(defaultColor, false, null, null);
            bool lineHasContent = false;

            foreach (RichTextToken token in EnumerateRichTextTokens(text, scale))
            {
                if (token.Kind == RichTextTokenKind.NewLine)
                {
                    currentY += lineHeight;
                    currentX = lineStartX;
                    lineHeight = baselineHeight;
                    lineHasContent = false;
                    continue;
                }

                if (token.Kind == RichTextTokenKind.Style)
                {
                    currentStyle = ApplyQuestDetailStyle(token.StyleTag, currentStyle, defaultColor);
                    continue;
                }

                if (token.Kind == RichTextTokenKind.Font)
                {
                    currentStyle = ApplyQuestDetailFontName(token.FontName, currentStyle);
                    continue;
                }

                if (token.Kind == RichTextTokenKind.FontSize)
                {
                    currentStyle = ApplyQuestDetailFontSize(token.FontSize, currentStyle);
                    continue;
                }

                Vector2 measuredToken = MeasureRichTextToken(token, scale, currentStyle, lane);
                if (token.Kind == RichTextTokenKind.Space)
                {
                    if (!lineHasContent)
                    {
                        continue;
                    }

                    if ((currentX - lineStartX) + measuredToken.X > maxWidth)
                    {
                        currentY += lineHeight;
                        currentX = lineStartX;
                        lineHeight = baselineHeight;
                        lineHasContent = false;
                        continue;
                    }
                }
                else if (lineHasContent && (currentX - lineStartX) + measuredToken.X > maxWidth)
                {
                    currentY += lineHeight;
                    currentX = lineStartX;
                    lineHeight = baselineHeight;
                    lineHasContent = false;
                }

                if (measuredToken.X <= 0f && measuredToken.Y <= 0f)
                {
                    continue;
                }

                drawToken?.Invoke(token, new Vector2(currentX, currentY), currentStyle);
                currentX += measuredToken.X;
                lineHeight = Math.Max(lineHeight, measuredToken.Y > 0f ? measuredToken.Y : baselineHeight);
                if (token.Kind != RichTextTokenKind.Space)
                {
                    lineHasContent = true;
                }
            }

            return lineHasContent ? currentY + lineHeight : currentY;
        }

        private Vector2 MeasureRichTextToken(RichTextToken token, float scale, RichTextStyleState style, QuestDetailTextLane lane)
        {
            if (token.Kind == RichTextTokenKind.Text || token.Kind == RichTextTokenKind.Space)
            {
                return MeasureText(token.Text, scale, style.Emphasized, lane, style.FontFamily, style.FontPixelSize);
            }

            if (token.Kind == RichTextTokenKind.Icon ||
                token.Kind == RichTextTokenKind.Surface ||
                token.Kind == RichTextTokenKind.Reference)
            {
                return new Vector2(token.Width, token.AdvanceHeight);
            }

            return new Vector2(token.Width, token.Height);
        }

        private IEnumerable<RichTextToken> EnumerateRichTextTokens(string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            int index = 0;
            foreach (Match match in RichTextTokenRegex.Matches(text))
            {
                if (match.Index > index)
                {
                    foreach (RichTextToken token in EnumerateTextWordTokens(text.Substring(index, match.Index - index), scale))
                    {
                        yield return token;
                    }
                }

                string value = match.Value;
                if (value.IndexOf('\n') >= 0)
                {
                    yield return RichTextToken.NewLineToken;
                }
                else if (TryCreateMarkerToken(value, out RichTextToken markerToken))
                {
                    yield return markerToken;
                }
                else
                {
                    float width = MeasureText(value, scale, false, QuestDetailTextLane.Detail).X;
                    if (width > 0f)
                    {
                        yield return new RichTextToken(
                            RichTextTokenKind.Space,
                            value,
                            null,
                            null,
                            (int)Math.Ceiling(width),
                            (int)Math.Ceiling(GetLineHeight(scale, QuestDetailTextLane.Detail)));
                    }
                }

                index = match.Index + match.Length;
            }

            if (index < text.Length)
            {
                foreach (RichTextToken token in EnumerateTextWordTokens(text.Substring(index), scale))
                {
                    yield return token;
                }
            }
        }

        private IEnumerable<RichTextToken> EnumerateTextWordTokens(string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            float width = MeasureText(text, scale).X;
            if (width > 0f)
            {
                yield return new RichTextToken(
                    RichTextTokenKind.Text,
                    text,
                    null,
                    null,
                    (int)Math.Ceiling(width),
                    (int)Math.Ceiling(GetLineHeight(scale, QuestDetailTextLane.Detail)));
            }
        }

        private bool TryCreateMarkerToken(string value, out RichTextToken token)
        {
            token = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            const string itemPrefix = "{{ITEMICON:";
            const string uiCanvasPrefix = "{{UICANVAS:";
            const string questSurfacePrefix = "{{QUESTSURFACE:";
            const string questReferencePrefix = "{{QUESTREF:";
            if (value.StartsWith(itemPrefix, StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith("}}", StringComparison.Ordinal) &&
                int.TryParse(
                    value.Substring(itemPrefix.Length, value.Length - itemPrefix.Length - 2),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int itemId))
            {
            Texture2D iconTexture = ResolveItemIcon(itemId);
            if (iconTexture != null)
            {
                token = new RichTextToken(RichTextTokenKind.Icon, null, iconTexture, null, iconTexture.Width, iconTexture.Height, itemId);
            }

            return true;
        }

            if (value.StartsWith(uiCanvasPrefix, StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith("}}", StringComparison.Ordinal))
            {
                string uiCanvasPath = value.Substring(uiCanvasPrefix.Length, value.Length - uiCanvasPrefix.Length - 2).Trim();
                Texture2D uiCanvasTexture = ResolveInlineUiCanvasTexture(uiCanvasPath, out Point uiCanvasOrigin);
                if (uiCanvasTexture != null)
                {
                    token = new RichTextToken(
                        RichTextTokenKind.Surface,
                        null,
                        uiCanvasTexture,
                        null,
                        uiCanvasTexture.Width,
                        uiCanvasTexture.Height,
                        drawOffsetX: -uiCanvasOrigin.X,
                        drawOffsetY: -uiCanvasOrigin.Y);
                }

                return true;
            }

            if (value.StartsWith(questSurfacePrefix, StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith("}}", StringComparison.Ordinal))
            {
                string surfaceKey = value.Substring(questSurfacePrefix.Length, value.Length - questSurfacePrefix.Length - 2).Trim();
                Texture2D surfaceTexture = ResolveQuestSurfaceTexture(surfaceKey);
                if (surfaceTexture != null)
                {
                    token = new RichTextToken(RichTextTokenKind.Surface, null, surfaceTexture, null, surfaceTexture.Width, surfaceTexture.Height);
                }

                return true;
            }

            if (value.StartsWith(questReferencePrefix, StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith("}}", StringComparison.Ordinal))
            {
                string referencePayload = value.Substring(questReferencePrefix.Length, value.Length - questReferencePrefix.Length - 2);
                if (TryParseQuestInlineReferenceToken(referencePayload, out QuestDetailInlineReference reference))
                {
                    string label = string.IsNullOrWhiteSpace(reference.Label)
                        ? $"{reference.Kind} #{reference.TargetId}"
                        : reference.Label;
                    int width = (int)Math.Ceiling(MeasureText(label, ClientDetailScale, false, QuestDetailTextLane.Detail).X);
                    int height = (int)Math.Ceiling(GetLineHeight(ClientDetailScale, QuestDetailTextLane.Detail));
                    token = RichTextToken.ReferenceToken(reference, label, width, height);
                }

                return true;
            }

            const string questStylePrefix = "{{QUESTSTYLE:";
            if (value.StartsWith(questStylePrefix, StringComparison.OrdinalIgnoreCase) &&
                value.EndsWith("}}", StringComparison.Ordinal))
            {
                string styleTag = value.Substring(questStylePrefix.Length, value.Length - questStylePrefix.Length - 2).Trim();
                if (!string.IsNullOrWhiteSpace(styleTag))
                {
                    token = RichTextToken.StyleToken(styleTag);
                }

                return true;
            }

            if (TryParseQuestFontMarker(value, out string fontName))
            {
                token = RichTextToken.FontToken(fontName);
                return true;
            }

            if (TryParseQuestFontSizeMarker(value, out float fontPixelSize))
            {
                token = RichTextToken.FontSizeToken(fontPixelSize);
                return true;
            }

            return false;
        }

        private static bool TryParseQuestFontMarker(string value, out string fontName)
        {
            fontName = string.Empty;
            const string questFontPrefix = "{{QUESTFONT:";
            if (string.IsNullOrWhiteSpace(value) ||
                !value.StartsWith(questFontPrefix, StringComparison.OrdinalIgnoreCase) ||
                !value.EndsWith("}}", StringComparison.Ordinal))
            {
                return false;
            }

            string parsedFontName = value.Substring(questFontPrefix.Length, value.Length - questFontPrefix.Length - 2).Trim();
            if (string.IsNullOrWhiteSpace(parsedFontName))
            {
                return false;
            }

            fontName = parsedFontName;
            return true;
        }

        private static bool TryParseQuestFontSizeMarker(string value, out float fontPixelSize)
        {
            fontPixelSize = 0f;
            const string questFontSizePrefix = "{{QUESTFONTSIZE:";
            if (string.IsNullOrWhiteSpace(value) ||
                !value.StartsWith(questFontSizePrefix, StringComparison.OrdinalIgnoreCase) ||
                !value.EndsWith("}}", StringComparison.Ordinal))
            {
                return false;
            }

            if (!float.TryParse(
                    value.Substring(questFontSizePrefix.Length, value.Length - questFontSizePrefix.Length - 2),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedFontSize) ||
                parsedFontSize <= 0f)
            {
                return false;
            }

            fontPixelSize = parsedFontSize;
            return true;
        }

        internal static bool TryParseQuestFontMarkerForTesting(string value, out string fontName)
        {
            return TryParseQuestFontMarker(value, out fontName);
        }

        internal static bool TryParseQuestFontSizeMarkerForTesting(string value, out float fontPixelSize)
        {
            return TryParseQuestFontSizeMarker(value, out fontPixelSize);
        }

        internal static bool TryParseQuestInlineReferenceTokenForTesting(string payload, out QuestDetailInlineReference reference)
        {
            return TryParseQuestInlineReferenceToken(payload, out reference);
        }

        private static bool TryParseQuestInlineReferenceToken(string payload, out QuestDetailInlineReference reference)
        {
            reference = default;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            string[] parts = payload.Split(new[] { ':' }, 3);
            if (parts.Length < 2 ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int targetId) ||
                targetId <= 0)
            {
                return false;
            }

            QuestDetailInlineReferenceKind kind = parts[0].Trim().ToLowerInvariant() switch
            {
                "npc" => QuestDetailInlineReferenceKind.Npc,
                "map" => QuestDetailInlineReferenceKind.Map,
                "mob" => QuestDetailInlineReferenceKind.Mob,
                "item" => QuestDetailInlineReferenceKind.Item,
                _ => QuestDetailInlineReferenceKind.None
            };

            if (kind == QuestDetailInlineReferenceKind.None)
            {
                return false;
            }

            string label = parts.Length >= 3 ? parts[2].Trim() : string.Empty;
            reference = new QuestDetailInlineReference(kind, targetId, label);
            return true;
        }

        private static RichTextStyleState ApplyQuestDetailStyle(string styleTag, RichTextStyleState currentStyle, Color defaultColor)
        {
            string normalizedTag = styleTag?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedTag))
            {
                return currentStyle;
            }

            return normalizedTag switch
            {
                "e" => currentStyle with { Emphasized = true },
                "n" => currentStyle with { Emphasized = false },
                _ => currentStyle with { Color = ResolveQuestDetailStyleColor(normalizedTag, defaultColor) }
            };
        }

        private static RichTextStyleState ApplyQuestDetailFontName(string fontName, RichTextStyleState currentStyle)
        {
            string normalizedFontName = fontName?.Trim();
            return string.IsNullOrWhiteSpace(normalizedFontName)
                ? currentStyle
                : currentStyle with { FontFamily = normalizedFontName };
        }

        private static RichTextStyleState ApplyQuestDetailFontSize(float fontPixelSize, RichTextStyleState currentStyle)
        {
            if (fontPixelSize <= 0f)
            {
                return currentStyle;
            }

            return currentStyle with
            {
                FontPixelSize = MathHelper.Clamp(fontPixelSize, 6f, 48f)
            };
        }

        private static Color ResolveQuestDetailStyleColor(string styleTag, Color defaultColor)
        {
            return styleTag?.Trim().ToLowerInvariant() switch
            {
                "b" => new Color(167, 214, 255),
                "r" => new Color(255, 166, 154),
                "g" => new Color(166, 224, 166),
                "d" => new Color(196, 196, 196),
                "m" => new Color(244, 182, 255),
                "c" => new Color(164, 238, 255),
                "k" => defaultColor,
                _ => defaultColor
            };
        }

        private HoveredQuestItemInfo ResolveHoveredQuestItem(int mouseX, int mouseY)
        {
            if (_state == null || !ContainsPoint(mouseX, mouseY))
            {
                return null;
            }

            Rectangle logClipRect = GetLogClipRectangle();
            float y = Position.Y + GetLogTextArrayBaseY() - _logScrollOffset;
            if (HasRequirementContent())
            {
                y = AdvanceSectionHeader(_requirementHeaderTexture, y);
                if (!string.IsNullOrWhiteSpace(_state.RequirementText))
                {
                    HoveredQuestItemInfo hoveredInlineItem = TryResolveHoveredRichTextItem(
                        mouseX,
                        mouseY,
                        logClipRect,
                        _state.RequirementText,
                        new Vector2(Position.X + ClientTextArrayX, y),
                        ClientContentWidth,
                        ClientDetailScale,
                        QuestDetailTextLane.Detail);
                    if (hoveredInlineItem != null)
                    {
                        return hoveredInlineItem;
                    }

                    y += AdvanceRichText(_state.RequirementText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
                    if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
                    {
                        y += ConditionSectionBodyGap;
                    }
                }

                HoveredQuestItemInfo hovered = TryResolveHoveredConditionItem(mouseX, mouseY, logClipRect, _state.RequirementLines, Position.X + ClientTextArrayX, ref y, ClientContentWidth, false);
                if (hovered != null)
                {
                    return hovered;
                }

                y += ClientSectionTailGap;
            }

            if (HasRewardContent())
            {
                y = AdvanceSectionHeader(_rewardHeaderTexture, y);
                if (!string.IsNullOrWhiteSpace(_state.RewardText))
                {
                    HoveredQuestItemInfo hoveredInlineItem = TryResolveHoveredRichTextItem(
                        mouseX,
                        mouseY,
                        logClipRect,
                        _state.RewardText,
                        new Vector2(Position.X + ClientTextArrayX, y),
                        ClientContentWidth,
                        ClientDetailScale,
                        QuestDetailTextLane.Detail);
                    if (hoveredInlineItem != null)
                    {
                        return hoveredInlineItem;
                    }

                    y += AdvanceRichText(_state.RewardText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
                    if (_state.RewardLines != null && _state.RewardLines.Count > 0)
                    {
                        y += ConditionSectionBodyGap;
                    }
                }

                HoveredQuestItemInfo hovered = TryResolveHoveredConditionItem(mouseX, mouseY, logClipRect, _state.RewardLines, Position.X + ClientTextArrayX, ref y, ClientContentWidth, true);
                if (hovered != null)
                {
                    return hovered;
                }

                y += ClientSectionTailGap;
            }

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                HoveredQuestItemInfo hoveredInlineItem = TryResolveHoveredRichTextItem(
                    mouseX,
                    mouseY,
                    logClipRect,
                    _state.HintText,
                    new Vector2(Position.X + ClientTextArrayX, y),
                    ClientContentWidth,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
                if (hoveredInlineItem != null)
                {
                    return hoveredInlineItem;
                }
            }

            Rectangle summaryClipRect = GetSummaryClipRectangle();
            if (summaryClipRect.Contains(mouseX, mouseY) && HasSummaryPaneContent())
            {
                float summaryY = Position.Y + ClientSummaryY - _summaryScrollOffset;
                summaryY = AdvanceSectionHeader(_summaryHeaderTexture, summaryY);
                return TryResolveHoveredRichTextItem(
                    mouseX,
                    mouseY,
                    summaryClipRect,
                    _state.SummaryText,
                    new Vector2(Position.X + ClientTextArrayX, summaryY),
                    ClientContentWidth,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
            }

            return null;
        }

        private QuestDetailInlineReference? ResolveHoveredInlineReference(int mouseX, int mouseY)
        {
            if (_state == null || !ContainsPoint(mouseX, mouseY))
            {
                return null;
            }

            Rectangle logClipRect = GetLogClipRectangle();
            float y = Position.Y + GetLogTextArrayBaseY() - _logScrollOffset;
            if (HasRequirementContent())
            {
                y = AdvanceSectionHeader(_requirementHeaderTexture, y);
                if (!string.IsNullOrWhiteSpace(_state.RequirementText))
                {
                    QuestDetailInlineReference? reference = TryResolveHoveredRichTextReference(
                        mouseX,
                        mouseY,
                        logClipRect,
                        _state.RequirementText,
                        new Vector2(Position.X + ClientTextArrayX, y),
                        ClientContentWidth,
                        ClientDetailScale,
                        QuestDetailTextLane.Detail);
                    if (reference.HasValue)
                    {
                        return reference;
                    }

                    y += AdvanceRichText(_state.RequirementText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
                    if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
                    {
                        y += ConditionSectionBodyGap;
                    }
                }

                if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
                {
                    y = AdvanceConditionLines(_state.RequirementLines, Position.X + ClientTextArrayX, y, ClientContentWidth, false);
                }

                y += ClientSectionTailGap;
            }

            if (HasRewardContent())
            {
                y = AdvanceSectionHeader(_rewardHeaderTexture, y);
                if (!string.IsNullOrWhiteSpace(_state.RewardText))
                {
                    QuestDetailInlineReference? reference = TryResolveHoveredRichTextReference(
                        mouseX,
                        mouseY,
                        logClipRect,
                        _state.RewardText,
                        new Vector2(Position.X + ClientTextArrayX, y),
                        ClientContentWidth,
                        ClientDetailScale,
                        QuestDetailTextLane.Detail);
                    if (reference.HasValue)
                    {
                        return reference;
                    }

                    y += AdvanceRichText(_state.RewardText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
                    if (_state.RewardLines != null && _state.RewardLines.Count > 0)
                    {
                        y += ConditionSectionBodyGap;
                    }
                }

                if (_state.RewardLines != null && _state.RewardLines.Count > 0)
                {
                    y = AdvanceConditionLines(_state.RewardLines, Position.X + ClientTextArrayX, y, ClientContentWidth, true);
                }

                y += ClientSectionTailGap;
            }

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                QuestDetailInlineReference? reference = TryResolveHoveredRichTextReference(
                    mouseX,
                    mouseY,
                    logClipRect,
                    _state.HintText,
                    new Vector2(Position.X + ClientTextArrayX, y),
                    ClientContentWidth,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
                if (reference.HasValue)
                {
                    return reference;
                }
            }

            Rectangle summaryClipRect = GetSummaryClipRectangle();
            if (summaryClipRect.Contains(mouseX, mouseY) && HasSummaryPaneContent())
            {
                float summaryY = Position.Y + ClientSummaryY - _summaryScrollOffset;
                summaryY = AdvanceSectionHeader(_summaryHeaderTexture, summaryY);
                return TryResolveHoveredRichTextReference(
                    mouseX,
                    mouseY,
                    summaryClipRect,
                    _state.SummaryText,
                    new Vector2(Position.X + ClientTextArrayX, summaryY),
                    ClientContentWidth,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
            }

            return null;
        }

        private HoveredQuestItemInfo TryResolveHoveredRichTextItem(
            int mouseX,
            int mouseY,
            Rectangle clipRect,
            string text,
            Vector2 position,
            float maxWidth,
            float scale,
            QuestDetailTextLane lane)
        {
            if (string.IsNullOrWhiteSpace(text) || !clipRect.Contains(mouseX, mouseY))
            {
                return null;
            }

            HoveredQuestItemInfo hoveredItem = null;
            LayoutRichText(
                text,
                position,
                maxWidth,
                scale,
                lane,
                (token, drawPosition, _) =>
                {
                    if (hoveredItem != null || token.Kind != RichTextTokenKind.Icon || !token.ItemId.HasValue)
                    {
                        return;
                    }

                    Rectangle bounds = new(
                        (int)Math.Round(drawPosition.X),
                        (int)Math.Round(drawPosition.Y),
                        Math.Max(1, token.Width),
                        Math.Max(1, token.Height));
                    if (bounds.Contains(mouseX, mouseY) && bounds.Intersects(clipRect))
                    {
                        hoveredItem = CreateHoveredQuestItem(token.ItemId.Value, "Quest detail item", null);
                    }
                },
                Color.White);

            return hoveredItem;
        }

        private QuestDetailInlineReference? TryResolveHoveredRichTextReference(
            int mouseX,
            int mouseY,
            Rectangle clipRect,
            string text,
            Vector2 position,
            float maxWidth,
            float scale,
            QuestDetailTextLane lane)
        {
            if (string.IsNullOrWhiteSpace(text) || !clipRect.Contains(mouseX, mouseY))
            {
                return null;
            }

            QuestDetailInlineReference? hoveredReference = null;
            LayoutRichText(
                text,
                position,
                maxWidth,
                scale,
                lane,
                (token, drawPosition, _) =>
                {
                    if (hoveredReference.HasValue ||
                        token.Kind != RichTextTokenKind.Reference ||
                        !token.InlineReference.HasValue)
                    {
                        return;
                    }

                    Rectangle bounds = new(
                        (int)Math.Round(drawPosition.X),
                        (int)Math.Round(drawPosition.Y),
                        Math.Max(1, token.Width),
                        Math.Max(1, token.Height));
                    if (bounds.Contains(mouseX, mouseY) && bounds.Intersects(clipRect))
                    {
                        hoveredReference = token.InlineReference.Value;
                    }
                },
                Color.White);

            return hoveredReference;
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

            foreach (QuestLogLineSnapshot line in lines.Where(line => line != null))
            {
                ConditionRowLayout layout = BuildConditionRowLayout(line, x, y, maxWidth, rewardSection);
                if (line.ItemId.HasValue && clipRect.Contains(mouseX, mouseY) && layout.IconBounds.Contains(mouseX, mouseY))
                {
                    return CreateHoveredQuestItem(line.ItemId.Value, line.Text, line.ItemQuantity);
                }

                HoveredQuestItemInfo inlineItem = TryResolveHoveredRichTextItem(
                    mouseX,
                    mouseY,
                    clipRect,
                    line.Text,
                    layout.BodyPosition,
                    layout.BodyMaxWidth,
                    ClientDetailScale,
                    QuestDetailTextLane.Detail);
                if (inlineItem != null)
                {
                    return inlineItem;
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
                : MeasureText(line.ValueText, ClientDetailScale, true, QuestDetailTextLane.DetailStrong).X;
            float textRight = detailX + stripWidth - (rowTexture != null ? ConditionTextInset : 0f);
            float bodyMaxWidth = Math.Max(36f, textRight - textLeft - (valueWidth > 0f ? valueWidth + ConditionValueGap : 0f));
            float bodyHeight = Math.Max(
                GetLineHeight(ClientDetailScale),
                AdvanceRichText(line.Text, bodyMaxWidth, ClientDetailScale, QuestDetailTextLane.Detail));
            float rowHeight = Math.Max(Math.Max(bodyHeight, stripHeight), iconWidth);
            float textureY = y;
            float labelY = y + 1f;
            float iconY = y + Math.Max(0f, (stripHeight > 0f ? stripHeight : rowHeight) - ConditionIconSize) / 2f;
            float bodyY = y + 1f;
            float valueY = y + 1f;

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
            return y + ((texture?.Height ?? GetLineHeight(ClientDetailScale)) + 4f);
        }

        private float AdvanceWrappedText(string text, float maxWidth, float y)
        {
            int lineCount = Math.Max(1, WrapText(text, maxWidth, ClientDetailScale).Count());
            return y + (lineCount * GetLineHeight(ClientDetailScale));
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

        private int GetLogTextArrayBaseY()
        {
            return ClientLogTextArrayBaseY + GetDetailTipHeight();
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
                if (!string.IsNullOrWhiteSpace(_state.RequirementText))
                {
                    y += AdvanceRichText(_state.RequirementText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
                    if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
                    {
                        y += ConditionSectionBodyGap;
                    }
                }

                if (_state.RequirementLines != null && _state.RequirementLines.Count > 0)
                {
                    y = AdvanceConditionLines(_state.RequirementLines, Position.X + ClientContentX, y, ClientContentWidth, false);
                }

                y += ClientSectionTailGap;
            }

            if (HasRewardContent())
            {
                y = AdvanceSectionHeader(_rewardHeaderTexture, y);
                if (!string.IsNullOrWhiteSpace(_state.RewardText))
                {
                    y += AdvanceRichText(_state.RewardText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
                    if (_state.RewardLines != null && _state.RewardLines.Count > 0)
                    {
                        y += ConditionSectionBodyGap;
                    }
                }

                if (_state.RewardLines != null && _state.RewardLines.Count > 0)
                {
                    y = AdvanceConditionLines(_state.RewardLines, Position.X + ClientContentX, y, ClientContentWidth, true);
                }

                y += ClientSectionTailGap;
            }

            if (!string.IsNullOrWhiteSpace(_state.HintText))
            {
                y += AdvanceRichText(_state.HintText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
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
            y += AdvanceRichText(_state.SummaryText, ClientContentWidth, ClientDetailScale, QuestDetailTextLane.Detail);
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

        private void DrawTextLineClipped(
            SpriteBatch sprite,
            string text,
            Vector2 position,
            Color color,
            Rectangle clipRect,
            float scale,
            bool emphasized = false,
            QuestDetailTextLane lane = QuestDetailTextLane.Detail,
            string fontFamilyOverride = null,
            float? fontPixelSizeOverride = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Vector2 textSize = MeasureText(text, scale, emphasized, lane, fontFamilyOverride, fontPixelSizeOverride);
            Rectangle lineRect = new(
                (int)position.X,
                (int)position.Y,
                Math.Max(1, (int)Math.Ceiling(textSize.X)),
                Math.Max(
                    1,
                    (int)Math.Ceiling(Math.Max(
                        textSize.Y,
                        GetLineHeight(scale, lane, emphasized, fontFamilyOverride, fontPixelSizeOverride)))));
            if (!lineRect.Intersects(clipRect))
            {
                return;
            }

            DrawTextLine(sprite, text, position, color, scale, emphasized, lane, fontFamilyOverride, fontPixelSizeOverride);
        }

        private void DrawTextLine(
            SpriteBatch sprite,
            string text,
            Vector2 position,
            Color color,
            float scale,
            bool emphasized = false,
            QuestDetailTextLane lane = QuestDetailTextLane.Detail,
            string fontFamilyOverride = null,
            float? fontPixelSizeOverride = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            ClientTextRasterizer rasterizer = ResolveQuestDetailTextRasterizer(
                lane,
                emphasized,
                fontFamilyOverride,
                fontPixelSizeOverride);
            if (rasterizer != null)
            {
                rasterizer.DrawString(sprite, text, position, color, scale);
                return;
            }

            if (emphasized)
            {
                ClientTextDrawing.Draw(sprite, text, position + new Vector2(1f, 0f), color, scale, _font);
            }

            ClientTextDrawing.Draw(sprite, text, position, color, scale, _font);
        }

        private Vector2 MeasureText(
            string text,
            float scale,
            bool emphasized = false,
            QuestDetailTextLane lane = QuestDetailTextLane.Detail,
            string fontFamilyOverride = null,
            float? fontPixelSizeOverride = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Vector2.Zero;
            }

            ClientTextRasterizer rasterizer = ResolveQuestDetailTextRasterizer(
                lane,
                emphasized,
                fontFamilyOverride,
                fontPixelSizeOverride);
            if (rasterizer != null)
            {
                return rasterizer.MeasureString(text, scale);
            }

            return ClientTextDrawing.Measure(GetTextGraphicsDevice(), text, scale, _font);
        }

        private float GetLineHeight(
            float scale,
            QuestDetailTextLane lane = QuestDetailTextLane.Detail,
            bool emphasized = false,
            string fontFamilyOverride = null,
            float? fontPixelSizeOverride = null)
        {
            ClientTextRasterizer rasterizer = ResolveQuestDetailTextRasterizer(
                lane,
                emphasized,
                fontFamilyOverride,
                fontPixelSizeOverride);
            Vector2 measured = rasterizer?.MeasureString("Ag", scale)
                ?? ClientTextDrawing.Measure(GetTextGraphicsDevice(), "Ag", scale, _font);
            if (measured.Y > 0f)
            {
                return measured.Y;
            }

            return (_font?.LineSpacing ?? 0) * scale;
        }

        private GraphicsDevice GetTextGraphicsDevice()
        {
            return _pixel?.GraphicsDevice
                ?? CurrentFrame?.Texture?.GraphicsDevice
                ?? _foreground?.Texture?.GraphicsDevice
                ?? _bottomPanel?.Texture?.GraphicsDevice
                ?? _summaryPanel?.Texture?.GraphicsDevice
                ?? _detailTip?.Texture?.GraphicsDevice;
        }

        private ClientTextRasterizer ResolveQuestDetailTextRasterizer(
            QuestDetailTextLane lane,
            bool emphasized,
            string fontFamilyOverride = null,
            float? fontPixelSizeOverride = null)
        {
            GraphicsDevice graphicsDevice = GetTextGraphicsDevice();
            if (graphicsDevice == null)
            {
                return null;
            }

            bool useBold = emphasized || lane == QuestDetailTextLane.DetailStrong;
            string normalizedFontFamilyOverride = fontFamilyOverride?.Trim();
            bool hasFontFamilyOverride = !string.IsNullOrWhiteSpace(normalizedFontFamilyOverride);
            bool hasFontSizeOverride = fontPixelSizeOverride.HasValue && fontPixelSizeOverride.Value > 0f;
            if (hasFontFamilyOverride || hasFontSizeOverride)
            {
                float basePointSize = hasFontSizeOverride
                    ? MathHelper.Clamp(fontPixelSizeOverride.Value, 6f, 48f)
                    : GetLaneBasePointSize(lane);
                string customKey = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{(int)lane}|{(useBold ? 1 : 0)}|{basePointSize:F2}|{normalizedFontFamilyOverride ?? string.Empty}");
                if (_customDetailTextRasterizers.TryGetValue(customKey, out ClientTextRasterizer customRasterizer))
                {
                    return customRasterizer;
                }

                customRasterizer = new ClientTextRasterizer(
                    graphicsDevice,
                    fontFamily: hasFontFamilyOverride ? normalizedFontFamilyOverride : null,
                    basePointSize: basePointSize,
                    fontStyle: useBold ? SD.FontStyle.Bold : SD.FontStyle.Regular);
                _customDetailTextRasterizers[customKey] = customRasterizer;
                return customRasterizer;
            }

            if (useBold)
            {
                _clientDetailBoldTextRasterizer ??= new ClientTextRasterizer(graphicsDevice, basePointSize: 11f, fontStyle: SD.FontStyle.Bold);
                return _clientDetailBoldTextRasterizer;
            }

            switch (lane)
            {
                case QuestDetailTextLane.Title:
                    _clientTitleTextRasterizer ??= new ClientTextRasterizer(graphicsDevice, basePointSize: 13f);
                    return _clientTitleTextRasterizer;
                case QuestDetailTextLane.Header:
                    _clientHeaderTextRasterizer ??= new ClientTextRasterizer(graphicsDevice, basePointSize: 11f);
                    return _clientHeaderTextRasterizer;
                case QuestDetailTextLane.Navigation:
                    _clientNavigationTextRasterizer ??= new ClientTextRasterizer(graphicsDevice, basePointSize: 10f);
                    return _clientNavigationTextRasterizer;
                case QuestDetailTextLane.Button:
                    _clientButtonTextRasterizer ??= new ClientTextRasterizer(graphicsDevice, basePointSize: 11f);
                    return _clientButtonTextRasterizer;
                default:
                    _clientDetailTextRasterizer ??= new ClientTextRasterizer(graphicsDevice, basePointSize: 11f);
                    return _clientDetailTextRasterizer;
            }
        }

        private static float GetLaneBasePointSize(QuestDetailTextLane lane)
        {
            return lane switch
            {
                QuestDetailTextLane.Title => 13f,
                QuestDetailTextLane.Navigation => 10f,
                _ => 11f
            };
        }

        private void RegisterQuestSurfaceTexture(string surfaceKey, Texture2D texture)
        {
            if (string.IsNullOrWhiteSpace(surfaceKey) || texture == null)
            {
                return;
            }

            _questSurfaceTextures[surfaceKey.Trim()] = texture;
        }

        private Texture2D ResolveQuestSurfaceTexture(string surfaceKey)
        {
            if (string.IsNullOrWhiteSpace(surfaceKey))
            {
                return null;
            }

            _questSurfaceTextures.TryGetValue(surfaceKey.Trim(), out Texture2D texture);
            return texture;
        }

        private Texture2D ResolveInlineUiCanvasTexture(string uiCanvasPath, out Point canvasOrigin)
        {
            canvasOrigin = Point.Zero;
            if (string.IsNullOrWhiteSpace(uiCanvasPath))
            {
                return null;
            }

            string normalizedPath = uiCanvasPath.Trim().Replace('\\', '/');
            if (_inlineUiCanvasTextures.TryGetValue(normalizedPath, out Texture2D cachedTexture) &&
                cachedTexture != null &&
                !cachedTexture.IsDisposed)
            {
                if (!_inlineUiCanvasOrigins.TryGetValue(normalizedPath, out canvasOrigin))
                {
                    canvasOrigin = Point.Zero;
                }

                return cachedTexture;
            }

            if (!TryResolveInlineUiCanvasProperty(normalizedPath, out WzCanvasProperty canvasProperty))
            {
                return null;
            }

            Texture2D texture = canvasProperty.GetLinkedWzCanvasBitmap()?.ToTexture2DAndDispose(GetTextGraphicsDevice());
            if (texture == null)
            {
                return null;
            }

            canvasOrigin = ResolveCanvasOrigin(canvasProperty);
            _inlineUiCanvasTextures[normalizedPath] = texture;
            _inlineUiCanvasOrigins[normalizedPath] = canvasOrigin;
            return texture;
        }

        private static Point ResolveCanvasOrigin(WzCanvasProperty canvasProperty)
        {
            if (canvasProperty?["origin"] is WzVectorProperty originProperty)
            {
                return new Point(originProperty.X?.Value ?? 0, originProperty.Y?.Value ?? 0);
            }

            return Point.Zero;
        }

        private static bool TryResolveInlineUiCanvasProperty(string uiCanvasPath, out WzCanvasProperty canvasProperty)
        {
            canvasProperty = null;
            if (string.IsNullOrWhiteSpace(uiCanvasPath))
            {
                return false;
            }

            string[] pathSegments = uiCanvasPath
                .Trim()
                .Replace('\\', '/')
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length < 3)
            {
                return false;
            }

            WzObject current = Program.FindWzObject(pathSegments[0], pathSegments[1]);
            if (current == null)
            {
                return false;
            }

            for (int i = 2; i < pathSegments.Length && current != null; i++)
            {
                current = current[pathSegments[i]];
            }

            canvasProperty = current as WzCanvasProperty;
            return canvasProperty != null;
        }

        private HoveredQuestItemInfo CreateHoveredQuestItem(int itemId, string lineText, int? quantity)
        {
            return new HoveredQuestItemInfo
            {
                ItemId = itemId,
                Quantity = quantity,
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
            int tooltipWidth = ResolveTooltipWidth();
            float titleWidth = tooltipWidth - (TOOLTIP_PADDING * 2);
            float bodyWidth = tooltipWidth - ((TOOLTIP_PADDING * 2) + TOOLTIP_ICON_SIZE + TOOLTIP_GAP);
            InventoryItemTooltipMetadata metadata = InventoryItemMetadataResolver.ResolveTooltipMetadata(
                _hoveredQuestItem.ItemId,
                InventoryItemMetadataResolver.ResolveInventoryType(_hoveredQuestItem.ItemId));
            string quantityLine = _hoveredQuestItem.Quantity.GetValueOrDefault(0) > 0
                ? $"Quantity: {_hoveredQuestItem.Quantity.Value}"
                : string.Empty;
            int? maxStackSize = InventoryItemMetadataResolver.TryResolveMaxStackForItem(_hoveredQuestItem.ItemId, out int resolvedMaxStackSize)
                ? resolvedMaxStackSize
                : null;
            string stackLine = InventoryItemMetadataResolver.BuildRuntimeFallbackStackLimitMetadataLine(
                maxStackSize,
                metadata.MetadataLines);

            string[] wrappedTitle = WrapTooltipText(title, titleWidth);
            float titleHeight = wrappedTitle.Length * _font.LineSpacing;
            var wrappedSections = new List<(string[] Lines, Color Color, float Height)>();

            void AddSection(string text, Color color)
            {
                string[] wrapped = WrapTooltipText(text, bodyWidth);
                float height = wrapped.Length * _font.LineSpacing;
                if (height > 0f)
                {
                    wrappedSections.Add((wrapped, color, height));
                }
            }

            AddSection(_hoveredQuestItem.Subtitle, new Color(228, 233, 242));
            AddSection(metadata.TypeName, new Color(180, 220, 255));
            for (int i = 0; i < metadata.EffectLines.Count; i++)
            {
                AddSection(metadata.EffectLines[i], new Color(180, 255, 210));
            }

            AddSection(quantityLine, Color.White);
            AddSection(stackLine, new Color(180, 255, 210));

            for (int i = 0; i < metadata.MetadataLines.Count; i++)
            {
                AddSection(metadata.MetadataLines[i], new Color(255, 214, 156));
            }

            AddSection(_hoveredQuestItem.Description, new Color(199, 206, 218));

            float bodyHeight = 0f;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                if (bodyHeight > 0f)
                {
                    bodyHeight += TOOLTIP_SECTION_GAP;
                }

                bodyHeight += wrappedSections[i].Height;
            }

            int tooltipHeight = (int)Math.Ceiling((TOOLTIP_PADDING * 2) + titleHeight + 6f + Math.Max(TOOLTIP_ICON_SIZE, bodyHeight));

            int viewportWidth = sprite.GraphicsDevice.Viewport.Width;
            int viewportHeight = sprite.GraphicsDevice.Viewport.Height;
            Rectangle backgroundRect = ResolveTooltipRect(
                new Point(_lastMousePosition.X + TOOLTIP_OFFSET_X, _lastMousePosition.Y + TOOLTIP_OFFSET_Y),
                tooltipWidth,
                tooltipHeight,
                viewportWidth,
                viewportHeight,
                stackalloc[] { 1, 0, 2 },
                out int tooltipFrameIndex);
            DrawTooltipBackground(sprite, backgroundRect, tooltipFrameIndex);

            float textY = backgroundRect.Y + TOOLTIP_PADDING;
            DrawTooltipLines(sprite, wrappedTitle, new Vector2(backgroundRect.X + TOOLTIP_PADDING, textY), new Color(255, 220, 120));
            textY += titleHeight + 6f;

            if (_hoveredQuestItem.Icon != null)
            {
                sprite.Draw(_hoveredQuestItem.Icon, new Rectangle(backgroundRect.X + TOOLTIP_PADDING, (int)textY, TOOLTIP_ICON_SIZE, TOOLTIP_ICON_SIZE), Color.White);
            }

            float bodyX = backgroundRect.X + TOOLTIP_PADDING + TOOLTIP_ICON_SIZE + TOOLTIP_GAP;
            float sectionY = textY;
            for (int i = 0; i < wrappedSections.Count; i++)
            {
                if (i > 0)
                {
                    sectionY += TOOLTIP_SECTION_GAP;
                }

                DrawTooltipLines(sprite, wrappedSections[i].Lines, new Vector2(bodyX, sectionY), wrappedSections[i].Color);
                sectionY += wrappedSections[i].Height;
            }
        }

        private int ResolveTooltipWidth()
        {
            int textureWidth = _tooltipFrames[1]?.Width ?? 0;
            return textureWidth > 0 ? textureWidth : TOOLTIP_FALLBACK_WIDTH;
        }

        private Rectangle CreateTooltipRectFromAnchor(Point anchorPoint, int tooltipWidth, int tooltipHeight, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;
            Point origin = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrameOrigins.Length
                ? _tooltipFrameOrigins[tooltipFrameIndex]
                : Point.Zero;

            if (tooltipFrame != null && origin != Point.Zero)
            {
                float scaleX = tooltipFrame.Width > 0 ? tooltipWidth / (float)tooltipFrame.Width : 1f;
                float scaleY = tooltipFrame.Height > 0 ? tooltipHeight / (float)tooltipFrame.Height : 1f;
                return new Rectangle(
                    anchorPoint.X - (int)Math.Round(origin.X * scaleX),
                    anchorPoint.Y - (int)Math.Round(origin.Y * scaleY),
                    tooltipWidth,
                    tooltipHeight);
            }

            return tooltipFrameIndex switch
            {
                0 => new Rectangle(anchorPoint.X - tooltipWidth - TOOLTIP_OFFSET_X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight),
                2 => new Rectangle(anchorPoint.X, anchorPoint.Y + TOOLTIP_OFFSET_Y, tooltipWidth, tooltipHeight),
                _ => new Rectangle(anchorPoint.X, anchorPoint.Y - tooltipHeight + 1, tooltipWidth, tooltipHeight)
            };
        }

        private static int ComputeTooltipOverflow(Rectangle rect, int renderWidth, int renderHeight)
        {
            int overflow = 0;
            if (rect.Left < TOOLTIP_PADDING)
            {
                overflow += TOOLTIP_PADDING - rect.Left;
            }

            if (rect.Top < TOOLTIP_PADDING)
            {
                overflow += TOOLTIP_PADDING - rect.Top;
            }

            if (rect.Right > renderWidth - TOOLTIP_PADDING)
            {
                overflow += rect.Right - (renderWidth - TOOLTIP_PADDING);
            }

            if (rect.Bottom > renderHeight - TOOLTIP_PADDING)
            {
                overflow += rect.Bottom - (renderHeight - TOOLTIP_PADDING);
            }

            return overflow;
        }

        private static Rectangle ClampTooltipRect(Rectangle rect, int renderWidth, int renderHeight)
        {
            int minX = TOOLTIP_PADDING;
            int minY = TOOLTIP_PADDING;
            int maxX = Math.Max(minX, renderWidth - TOOLTIP_PADDING - rect.Width);
            int maxY = Math.Max(minY, renderHeight - TOOLTIP_PADDING - rect.Height);
            return new Rectangle(
                Math.Clamp(rect.X, minX, maxX),
                Math.Clamp(rect.Y, minY, maxY),
                rect.Width,
                rect.Height);
        }

        private Rectangle ResolveTooltipRect(
            Point anchorPoint,
            int tooltipWidth,
            int tooltipHeight,
            int renderWidth,
            int renderHeight,
            ReadOnlySpan<int> framePreference,
            out int tooltipFrameIndex)
        {
            Rectangle bestRect = Rectangle.Empty;
            int bestFrame = framePreference.Length > 0 ? framePreference[0] : 1;
            int bestOverflow = int.MaxValue;

            for (int i = 0; i < framePreference.Length; i++)
            {
                int frameIndex = framePreference[i];
                Rectangle candidate = CreateTooltipRectFromAnchor(anchorPoint, tooltipWidth, tooltipHeight, frameIndex);
                int overflow = ComputeTooltipOverflow(candidate, renderWidth, renderHeight);
                if (overflow == 0)
                {
                    tooltipFrameIndex = frameIndex;
                    return candidate;
                }

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    bestFrame = frameIndex;
                    bestRect = candidate;
                }
            }

            tooltipFrameIndex = bestFrame;
            return ClampTooltipRect(bestRect, renderWidth, renderHeight);
        }

        private void DrawTooltipBackground(SpriteBatch sprite, Rectangle rect, int tooltipFrameIndex)
        {
            Texture2D tooltipFrame = tooltipFrameIndex >= 0 && tooltipFrameIndex < _tooltipFrames.Length
                ? _tooltipFrames[tooltipFrameIndex]
                : null;
            if (tooltipFrame != null)
            {
                sprite.Draw(tooltipFrame, rect, Color.White);
                return;
            }

            sprite.Draw(_pixel, rect, new Color(18, 24, 37, 235));
            DrawTooltipBorder(sprite, rect);
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Color(112, 146, 201));
            sprite.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Color(112, 146, 201));
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

            return WrapText(text, (int)Math.Ceiling(maxWidth), 1f).ToArray();
        }

        private void DrawCenteredButtonLabel(SpriteBatch sprite, UIObject button, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            int width = Math.Max(1, button.CanvasSnapshotWidth);
            int height = Math.Max(1, button.CanvasSnapshotHeight);
            Vector2 textSize = MeasureText(text, 1f, false, QuestDetailTextLane.Button);
            float x = Position.X + button.X + ((width - textSize.X) / 2f);
            float y = Position.Y + button.Y + ((height - textSize.Y) / 2f) - 1f;
            DrawTextLine(sprite, text, new Vector2(x, y), Color.White, 1f, lane: QuestDetailTextLane.Button);
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

        private static UIObjectState ResolveButtonState(bool enabled, bool selected)
        {
            if (!enabled)
            {
                return UIObjectState.Disabled;
            }

            return selected
                ? UIObjectState.Pressed
                : UIObjectState.Normal;
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

        private enum QuestDetailTextLane
        {
            Title,
            Header,
            Detail,
            DetailStrong,
            Navigation,
            Button
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

        private enum RichTextTokenKind
        {
            Text,
            Space,
            Icon,
            Surface,
            Style,
            Font,
            FontSize,
            Reference,
            NewLine
        }

        private readonly struct RichTextToken
        {
            public static readonly RichTextToken NewLineToken = new(RichTextTokenKind.NewLine, null, null, null, 0, 0);

            public RichTextToken(
                RichTextTokenKind kind,
                string text,
                Texture2D texture,
                string styleTag,
                int width,
                int height,
                int? itemId = null,
                QuestDetailInlineReference? inlineReference = null,
                string fontName = null,
                float fontSize = 0f,
                int drawOffsetX = 0,
                int drawOffsetY = 0)
            {
                Kind = kind;
                Text = text;
                Texture = texture;
                StyleTag = styleTag;
                Width = Math.Max(0, width);
                Height = Math.Max(0, height);
                ItemId = itemId;
                InlineReference = inlineReference;
                FontName = fontName;
                FontSize = fontSize;
                DrawOffsetX = drawOffsetX;
                DrawOffsetY = drawOffsetY;
            }

            public RichTextTokenKind Kind { get; }
            public string Text { get; }
            public Texture2D Texture { get; }
            public string StyleTag { get; }
            public int Width { get; }
            public int Height { get; }
            public int? ItemId { get; }
            public QuestDetailInlineReference? InlineReference { get; }
            public string FontName { get; }
            public float FontSize { get; }
            public int DrawOffsetX { get; }
            public int DrawOffsetY { get; }
            public int AdvanceHeight => Math.Max(0, Height + Math.Max(0, DrawOffsetY));

            public static RichTextToken StyleToken(string styleTag)
            {
                return new RichTextToken(RichTextTokenKind.Style, null, null, styleTag, 0, 0);
            }

            public static RichTextToken ReferenceToken(QuestDetailInlineReference inlineReference, string label, int width, int height)
            {
                return new RichTextToken(RichTextTokenKind.Reference, label, null, null, width, height, inlineReference: inlineReference);
            }

            public static RichTextToken FontToken(string fontName)
            {
                return new RichTextToken(RichTextTokenKind.Font, null, null, null, 0, 0, fontName: fontName);
            }

            public static RichTextToken FontSizeToken(float fontSize)
            {
                return new RichTextToken(RichTextTokenKind.FontSize, null, null, null, 0, 0, fontSize: fontSize);
            }
        }

        private readonly record struct RichTextStyleState(Color Color, bool Emphasized, string FontFamily, float? FontPixelSize);
    }
}
