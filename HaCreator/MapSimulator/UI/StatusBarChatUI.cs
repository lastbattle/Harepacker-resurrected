using HaCreator.MapSimulator;
using HaCreator.MapSimulator.Interaction;
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
            public int ClientComboSelectIndex { get; set; } = -1;
            public int ClientComboDeleteIndex { get; set; } = -1;
        }

        private sealed class WhisperPickerButtonHitRegion
        {
            public Rectangle Bounds { get; set; }
            public WhisperPickerButtonAction Action { get; set; }
        }

        private sealed class WhisperPickerButtonVisuals
        {
            public Texture2D Normal { get; set; }
            public Texture2D Hover { get; set; }
            public Texture2D Pressed { get; set; }
            public Texture2D Disabled { get; set; }
            public Texture2D KeyFocused { get; set; }
            public Point NormalOrigin { get; set; }
            public Point HoverOrigin { get; set; }
            public Point PressedOrigin { get; set; }
            public Point DisabledOrigin { get; set; }
            public Point KeyFocusedOrigin { get; set; }

            public Texture2D ResolveTexture(
                PacketScriptOwnerButtonVisualState state,
                bool keyFocused)
            {
                if (state == PacketScriptOwnerButtonVisualState.Disabled)
                {
                    return Disabled ?? Normal ?? Hover ?? Pressed ?? KeyFocused;
                }

                if (state == PacketScriptOwnerButtonVisualState.Pressed)
                {
                    return Pressed ?? Hover ?? KeyFocused ?? Normal ?? Disabled;
                }

                if (state == PacketScriptOwnerButtonVisualState.Hover)
                {
                    return Hover ?? KeyFocused ?? Normal ?? Pressed ?? Disabled;
                }

                if (keyFocused)
                {
                    return KeyFocused ?? Hover ?? Normal ?? Pressed ?? Disabled;
                }

                return Normal ?? Hover ?? KeyFocused ?? Pressed ?? Disabled;
            }

            public Point ResolveOrigin(Texture2D texture)
            {
                if (texture == Disabled && Disabled != null)
                {
                    return DisabledOrigin;
                }

                if (texture == Pressed && Pressed != null)
                {
                    return PressedOrigin;
                }

                if (texture == Hover && Hover != null)
                {
                    return HoverOrigin;
                }

                if (texture == KeyFocused && KeyFocused != null)
                {
                    return KeyFocusedOrigin;
                }

                return NormalOrigin;
            }

            public int ResolveSlotWidth(int minimumWidth)
            {
                return StatusBarChatLayoutRules.ResolveWhisperPickerButtonSlotWidth(
                    minimumWidth,
                    Normal?.Width ?? 0,
                    Hover?.Width ?? 0,
                    Pressed?.Width ?? 0,
                    Disabled?.Width ?? 0,
                    KeyFocused?.Width ?? 0);
            }

            public int ResolveSlotHeight(int minimumHeight)
            {
                return StatusBarChatLayoutRules.ResolveWhisperPickerButtonSlotHeight(
                    minimumHeight,
                    Normal?.Height ?? 0,
                    Hover?.Height ?? 0,
                    Pressed?.Height ?? 0,
                    Disabled?.Height ?? 0,
                    KeyFocused?.Height ?? 0);
            }
        }

        private sealed class WhisperPickerComboDropdownStateVisuals
        {
            public Texture2D Left { get; set; }
            public Texture2D Center { get; set; }
            public Texture2D Right { get; set; }

            public bool HasAnyTexture => Left != null || Center != null || Right != null;

            public int Height => Math.Max(
                Math.Max(Left?.Height ?? 0, Center?.Height ?? 0),
                Right?.Height ?? 0);

            public int Width => StatusBarChatLayoutRules.ResolveWhisperPickerModalComboDropdownMinimumWidth(
                Left?.Width ?? 0,
                Center?.Width ?? 0,
                Right?.Width ?? 0);
        }

        private sealed class WhisperPickerComboDropdownVisuals
        {
            public WhisperPickerComboDropdownStateVisuals Normal { get; set; } = new WhisperPickerComboDropdownStateVisuals();
            public WhisperPickerComboDropdownStateVisuals Hover { get; set; } = new WhisperPickerComboDropdownStateVisuals();
            public WhisperPickerComboDropdownStateVisuals Pressed { get; set; } = new WhisperPickerComboDropdownStateVisuals();
            public WhisperPickerComboDropdownStateVisuals Disabled { get; set; } = new WhisperPickerComboDropdownStateVisuals();
            public WhisperPickerComboDropdownStateVisuals Selected { get; set; } = new WhisperPickerComboDropdownStateVisuals();

            public WhisperPickerComboDropdownStateVisuals ResolveState(
                PacketScriptOwnerButtonVisualState state,
                bool selected)
            {
                if (state == PacketScriptOwnerButtonVisualState.Disabled)
                {
                    return Disabled.HasAnyTexture ? Disabled : Normal;
                }

                if (state == PacketScriptOwnerButtonVisualState.Pressed)
                {
                    return Pressed.HasAnyTexture ? Pressed : (Hover.HasAnyTexture ? Hover : Normal);
                }

                if (selected && Selected.HasAnyTexture)
                {
                    return Selected;
                }

                if (state == PacketScriptOwnerButtonVisualState.Hover)
                {
                    return Hover.HasAnyTexture ? Hover : Normal;
                }

                return Normal;
            }

            public int ResolveRowHeight()
            {
                return StatusBarChatLayoutRules.ResolveWhisperPickerModalComboDropdownRowHeight(
                    Normal.Height,
                    Hover.Height,
                    Pressed.Height,
                    Disabled.Height,
                    Selected.Height);
            }

            public int ResolveMinimumWidth()
            {
                return Math.Max(
                    StatusBarChatLayoutRules.ResolveWhisperPickerModalComboDropdownMinimumWidth(
                        StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownLeftSliceWidth,
                        StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownCenterSliceWidth,
                        StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRightSliceWidth),
                    Math.Max(
                        Math.Max(Normal.Width, Hover.Width),
                        Math.Max(Math.Max(Pressed.Width, Disabled.Width), Selected.Width)));
            }
        }

        public sealed class ChatTargetLabelPlacement
        {
            public Texture2D Texture { get; set; }
            public Point Origin { get; set; }
        }

        private readonly List<UIObject> uiButtons = new List<UIObject>();
        private readonly Dictionary<MapSimulatorChatTargetType, ChatTargetLabelPlacement> _chatTargetLabels =
            new Dictionary<MapSimulatorChatTargetType, ChatTargetLabelPlacement>();
        private readonly Dictionary<string, UIObject> _shortcutTooltipButtons = new Dictionary<string, UIObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _shortcutTooltips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        private ButtonState _previousRightButtonState = ButtonState.Released;
        private string _pressedWhisperTarget;
        private bool _pressedWhisperPickerCandidate;
        private string _pressedWhisperPickerCandidateTarget;
        private string _pressedRightWhisperPickerCandidateTarget;
        private WhisperPickerButtonAction? _pressedWhisperPickerButtonAction;
        private bool _pressedWhisperPickerComboControl;
        private bool _pressedWhisperPickerComboToggle;
        private readonly List<WhisperTargetHitRegion> _whisperTargetHitRegions = new List<WhisperTargetHitRegion>();
        private readonly List<WhisperPickerHitRegion> _whisperPickerHitRegions = new List<WhisperPickerHitRegion>();
        private Rectangle? _whisperPromptBounds;
        private Rectangle? _whisperPickerBounds;
        private Rectangle? _whisperPickerComboBounds;
        private Rectangle? _whisperPickerComboToggleBounds;
        private Rectangle? _whisperPickerDropdownBounds;
        private Rectangle? _whisperPickerDropdownRowContentBounds;
        private Rectangle? _whisperPickerDropdownScrollBarBounds;
        private Rectangle? _whisperPickerDropdownScrollPrevBounds;
        private Rectangle? _whisperPickerDropdownScrollNextBounds;
        private Rectangle? _whisperPickerDropdownScrollTrackBounds;
        private Rectangle? _whisperPickerDropdownScrollThumbBounds;
        private Texture2D _whisperPickerSelectedTexture;
        private Texture2D _whisperPickerRowTexture;
        private Point _whisperPickerSelectedOrigin;
        private Point _whisperPickerRowOrigin;
        private Texture2D _whisperPickerDialogTopTexture;
        private Texture2D _whisperPickerDialogCenterTexture;
        private Texture2D _whisperPickerDialogBottomTexture;
        private Texture2D _whisperPickerDialogBarTexture;
        private Texture2D _whisperPickerDialogLineTexture;
        private WhisperPickerButtonVisuals _whisperPickerComboVisuals = new WhisperPickerButtonVisuals();
        private WhisperPickerButtonVisuals _whisperPickerComboToggleVisuals = new WhisperPickerButtonVisuals();
        private WhisperPickerComboDropdownVisuals _whisperPickerComboDropdownVisuals = new WhisperPickerComboDropdownVisuals();
        private WhisperPickerButtonVisuals _whisperPickerPrevButtonVisuals = new WhisperPickerButtonVisuals();
        private WhisperPickerButtonVisuals _whisperPickerNextButtonVisuals = new WhisperPickerButtonVisuals();
        private WhisperPickerButtonVisuals _whisperPickerOkButtonVisuals = new WhisperPickerButtonVisuals();
        private WhisperPickerButtonVisuals _whisperPickerCloseButtonVisuals = new WhisperPickerButtonVisuals();
        private VerticalScrollbarSkin _whisperPickerDropdownScrollbarSkin;
        private readonly List<WhisperPickerButtonHitRegion> _whisperPickerButtonHitRegions = new List<WhisperPickerButtonHitRegion>();
        private bool _isDraggingWhisperPickerDropdownScrollThumb;
        private int _whisperPickerDropdownScrollThumbDragOffsetY;
        private WhisperPickerDropdownScrollRepeatAction _whisperPickerDropdownScrollRepeatAction = WhisperPickerDropdownScrollRepeatAction.None;
        private int _whisperPickerDropdownScrollRepeatStartTick = int.MinValue;
        private int _whisperPickerDropdownScrollRepeatLastTick = int.MinValue;

        internal const float ClientChatTextFontPixelSize = 11f;
        internal const int ClientChatTextFontFaceStringPoolId = 0x1A25;
        internal const string ClientChatTextFontFallbackFamily = "Arial";
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
        private const int WhisperPickerModalHeaderGap = 10;
        private const int WhisperPickerDropdownScrollRepeatInitialDelayMs = 400;
        private const int WhisperPickerDropdownScrollRepeatIntervalMs = 60;
        private Point _pointNotificationAnchor = new Point(512, 60);
        private Vector2 _chatTargetLabelPos = new Vector2(17, 7);
        private Vector2 _chatEnterPos = new Vector2(4, 2);
        private Vector2 _chatInputBasePos = new Vector2(74, 5);
        private Vector2 _chatInputPos = new Vector2(74, 5);
        private Vector2 _chatWhisperPromptPos = new Vector2(74, -13);
        private Vector2 _chatLogTextBasePos = new Vector2(StatusBarChatLayoutRules.ClientChatLogTextLeftInset, -16);
        private Vector2 _chatLogTextPos = new Vector2(StatusBarChatLayoutRules.ClientChatLogTextLeftInset, -16);
        private int _chatLogWidth = 452;
        private int _chatInputWidth = 380;
        private int _chatLogLineHeight = DefaultChatLogLineHeight;
        private int _chatCursorHeight = DefaultChatCursorHeight;
        private Rectangle _chatEnterBounds;
        private Rectangle _chatSpace2Bounds;

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
        public Action WhisperTargetPickerModalButtonFocusRequested { get; set; }
        public Action WhisperTargetPickerModalComboFocusRequested { get; set; }
        public Action WhisperTargetPickerModalComboDropdownCloseRequested { get; set; }
        public Action WhisperTargetPickerModalComboDropdownToggleRequested { get; set; }
        public Action<string> WhisperTargetPickerModalComboDropdownHoverRequested { get; set; }
        public Action<int> WhisperTargetPickerModalComboDropdownHoverIndexRequested { get; set; }
        public Action<int> WhisperTargetPickerModalComboDropdownSelectIndexRequested { get; set; }
        public Action<string> WhisperTargetPickerModalComboDropdownDeleteRequested { get; set; }
        public Action<int> WhisperTargetPickerModalComboDropdownDeleteIndexRequested { get; set; }
        public Action<int> WhisperTargetPickerModalComboDropdownScrollRequested { get; set; }
        public Action<int> WhisperTargetPickerModalComboDropdownPageRequested { get; set; }
        public Action<int> WhisperTargetPickerModalComboDropdownScrollPositionRequested { get; set; }

        private enum WhisperPickerButtonAction
        {
            Previous = 0,
            Next = 1,
            Confirm = 2,
            Close = 3
        }

        private enum WhisperPickerDropdownScrollRepeatAction
        {
            None = 0,
            StepPrevious = 1,
            StepNext = 2,
            PagePrevious = 3,
            PageNext = 4
        }

        internal static bool ShouldCloseWhisperPickerDropdownOnOutsidePress(
            bool isDropdownOpen,
            bool hoveredInteractiveElement,
            bool dropdownHovered = false)
        {
            // CCtrlComboBoxSelect owns pointer input while the select window is open.
            // A click inside the open dropdown chrome should not be treated as an outside press.
            return isDropdownOpen && !(hoveredInteractiveElement || dropdownHovered);
        }

        internal static bool ShouldToggleWhisperPickerComboDropdownOnPress(
            bool comboHovered,
            bool comboToggleHovered)
        {
            // Client CCtrlComboBox::OnMouseButton routes combo left-click through BtClicked.
            return comboHovered || comboToggleHovered;
        }

        internal static bool ShouldToggleWhisperPickerComboDropdownOnMouseDown(
            bool comboHovered,
            bool comboToggleHovered,
            bool primaryButtonDownMessage,
            bool secondaryButtonDownMessage)
        {
            // Client CCtrlComboBox::OnMouseButton routes both WM_LBUTTONDOWN (513)
            // and WM_RBUTTONDOWN (515) through BtClicked while pointer is on combo chrome.
            return (primaryButtonDownMessage || secondaryButtonDownMessage)
                && ShouldToggleWhisperPickerComboDropdownOnPress(comboHovered, comboToggleHovered);
        }

        internal static bool ShouldDrawWhisperPickerModalComboCaret(
            bool isWhisperTargetPickerActive,
            MapSimulatorChat.WhisperTargetPickerPresentation whisperTargetPickerPresentation,
            MapSimulatorChat.WhisperTargetPickerModalFocusTarget modalFocusTarget,
            bool isComboDropdownOpen,
            int tickCount,
            int blinkIntervalMs = 500)
        {
            if (!isWhisperTargetPickerActive
                || whisperTargetPickerPresentation != MapSimulatorChat.WhisperTargetPickerPresentation.Modal
                || modalFocusTarget != MapSimulatorChat.WhisperTargetPickerModalFocusTarget.ComboBox
                || isComboDropdownOpen)
            {
                return false;
            }

            return ((tickCount / Math.Max(1, blinkIntervalMs)) % 2) == 0;
        }

        internal static bool ShouldCommitHoveredWhisperPickerCandidateOnRelease(
            bool pressedWhisperPickerCandidate,
            string pressedWhisperTarget,
            string hoveredWhisperTarget,
            bool dropdownHovered,
            int hoveredPickerClientRowIndex)
        {
            // Client CCtrlComboBoxSelect::OnMouseButton commits on left-release row hit
            // (msg 514) without requiring a prior pressed-row capture.
            return (dropdownHovered && hoveredPickerClientRowIndex >= 0)
                || !string.IsNullOrWhiteSpace(hoveredWhisperTarget);
        }

        internal static bool ShouldDeleteHoveredWhisperPickerCandidateOnRightRelease(
            string pressedWhisperTarget,
            string hoveredWhisperTarget,
            bool dropdownHovered,
            int hoveredPickerClientRowIndex)
        {
            // Client CCtrlComboBoxSelect::OnMouseButton deletes on right-release row hit
            // (msg 517) using the release row index directly.
            return (dropdownHovered && hoveredPickerClientRowIndex >= 0)
                || !string.IsNullOrWhiteSpace(hoveredWhisperTarget);
        }

        internal static bool ShouldConsumeWhisperPickerPointerCapture(
            string pressedWhisperTarget,
            bool pressedWhisperPickerCandidate,
            bool hasPressedWhisperPickerButtonAction,
            bool pressedWhisperPickerComboControl,
            bool pressedWhisperPickerComboToggle)
        {
            return !string.IsNullOrWhiteSpace(pressedWhisperTarget)
                || pressedWhisperPickerCandidate
                || hasPressedWhisperPickerButtonAction
                || pressedWhisperPickerComboControl
                || pressedWhisperPickerComboToggle;
        }

        internal static bool ShouldConsumeWhisperPickerDropdownChromePointerEvent(
            bool dropdownHovered,
            bool hasHoveredCandidate,
            bool hasHoveredButton,
            bool promptHovered,
            bool comboHovered,
            bool comboToggleHovered)
        {
            // Client combo/select ownership keeps pointer interaction inside the open
            // dropdown chrome; blank in-window clicks are owner-internal and should
            // not fall through to unrelated HUD buttons.
            return dropdownHovered
                && !hasHoveredCandidate
                && !hasHoveredButton
                && !promptHovered
                && !comboHovered
                && !comboToggleHovered;
        }

        internal static int ResolveWhisperPickerClientComboSelectIndex(int firstVisibleIndex, int visibleRowIndex)
        {
            // Client CCtrlComboBoxSelect::OnMouseButton resolves select row index
            // as (ry / 16 + scrollbar curPos).
            return firstVisibleIndex + visibleRowIndex;
        }

        internal static int ResolveWhisperPickerClientComboDeleteIndex(int visibleRowIndex)
        {
            // Client CCtrlComboBoxSelect::OnMouseButton handles right-release delete
            // from visible-row (ry / 16) without adding scrollbar curPos.
            return Math.Max(0, visibleRowIndex);
        }

        internal static int ResolveWhisperPickerClientComboRowIndexFromReleaseY(
            int releaseY,
            Rectangle dropdownBounds,
            int firstVisibleIndex,
            int candidateCount,
            int rowHeight = StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRowHeight)
        {
            if (candidateCount <= 0 || rowHeight <= 0)
            {
                return -1;
            }

            int relativeY = releaseY - dropdownBounds.Y;
            if (relativeY < 0)
            {
                return -1;
            }

            int visibleRowIndex = relativeY / rowHeight;
            int clientRowIndex = firstVisibleIndex + visibleRowIndex;
            if (clientRowIndex < 0 || clientRowIndex >= candidateCount)
            {
                return -1;
            }

            return clientRowIndex;
        }

        internal static int ResolveWhisperPickerClientComboRowIndexFromReleasePoint(
            int releaseX,
            int releaseY,
            Rectangle rowContentBounds,
            Rectangle dropdownBounds,
            int firstVisibleIndex,
            int candidateCount,
            int rowHeight = StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRowHeight)
        {
            // Client select-row mapping is owned by CCtrlComboBoxSelect::OnMouseButton.
            // Scrollbar child ownership belongs to CCtrlScrollBar::OnMouseButton, so row
            // mapping should not run while pointer is in the scrollbar lane.
            if (!rowContentBounds.Contains(releaseX, releaseY))
            {
                return -1;
            }

            return ResolveWhisperPickerClientComboRowIndexFromReleaseY(
                releaseY,
                dropdownBounds,
                firstVisibleIndex,
                candidateCount,
                rowHeight);
        }

        internal static int ResolveWhisperPickerClientComboDeleteIndexFromReleaseY(
            int releaseY,
            Rectangle dropdownBounds,
            int candidateCount,
            int rowHeight = StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRowHeight)
        {
            if (candidateCount <= 0 || rowHeight <= 0)
            {
                return -1;
            }

            int relativeY = releaseY - dropdownBounds.Y;
            if (relativeY < 0)
            {
                return -1;
            }

            int visibleRowIndex = relativeY / rowHeight;
            return visibleRowIndex >= 0 && visibleRowIndex < candidateCount
                ? visibleRowIndex
                : -1;
        }

        internal static int ResolveWhisperPickerClientComboDeleteIndexFromReleasePoint(
            int releaseX,
            int releaseY,
            Rectangle rowContentBounds,
            Rectangle dropdownBounds,
            int candidateCount,
            int rowHeight = StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRowHeight)
        {
            // Client delete-row mapping is owned by CCtrlComboBoxSelect::OnMouseButton.
            // Scrollbar child ownership belongs to CCtrlScrollBar::OnMouseButton, so row
            // mapping should not run while pointer is in the scrollbar lane.
            if (!rowContentBounds.Contains(releaseX, releaseY))
            {
                return -1;
            }

            return ResolveWhisperPickerClientComboDeleteIndexFromReleaseY(
                releaseY,
                dropdownBounds,
                candidateCount,
                rowHeight);
        }

        internal static bool ShouldKeepWhisperPickerDropdownScrollThumbCapture(
            bool wasDraggingScrollThumb,
            ButtonState leftButtonState)
        {
            return wasDraggingScrollThumb && leftButtonState == ButtonState.Pressed;
        }

        internal static bool ShouldClearWhisperPickerDropdownScrollThumbCapture(MapSimulatorChatRenderState chatState)
        {
            return chatState == null
                || !chatState.IsWhisperTargetPickerActive
                || chatState.WhisperTargetPickerPresentation != MapSimulatorChat.WhisperTargetPickerPresentation.Modal
                || !chatState.IsWhisperTargetPickerComboDropdownOpen;
        }

        internal static bool ShouldTriggerWhisperPickerDropdownScrollAutoRepeat(
            int heldElapsedMs,
            int sinceLastRepeatMs,
            int initialDelayMs = WhisperPickerDropdownScrollRepeatInitialDelayMs,
            int repeatIntervalMs = WhisperPickerDropdownScrollRepeatIntervalMs)
        {
            if (heldElapsedMs < Math.Max(0, initialDelayMs))
            {
                return false;
            }

            return sinceLastRepeatMs >= Math.Max(1, repeatIntervalMs);
        }

        internal static bool ShouldContinueWhisperPickerDropdownTrackRepeat(
            bool repeatBackward,
            int mouseY,
            int thumbTop,
            int thumbBottom)
        {
            return repeatBackward ? mouseY < thumbTop : mouseY >= thumbBottom;
        }

        private void ResetWhisperPickerPointerCaptureState()
        {
            _pressedWhisperTarget = null;
            _pressedWhisperPickerCandidate = false;
            _pressedWhisperPickerCandidateTarget = null;
            _pressedWhisperPickerButtonAction = null;
            _pressedWhisperPickerComboControl = false;
            _pressedWhisperPickerComboToggle = false;
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
                    fontFamily: ResolveClientChatFontFamily(),
                    basePointSize: ClientChatTextFontPixelSize,
                    preferEmbeddedPrivateFontSources: false);
            }
        }

        internal static string ResolveClientChatFontFamily()
        {
            string resolvedFamily = MapleStoryStringPool.GetOrFallback(
                ClientChatTextFontFaceStringPoolId,
                ClientChatTextFontFallbackFamily);
            return string.IsNullOrWhiteSpace(resolvedFamily)
                ? ClientChatTextFontFallbackFamily
                : resolvedFamily.Trim();
        }

        public void SetChatEnterTexture(Texture2D chatEnterTexture)
        {
            _chatEnterTexture = chatEnterTexture;
            RefreshTextMetrics();
        }

        public void SetWhisperPickerTextures(
            Texture2D selectedTexture,
            Texture2D rowTexture,
            Point selectedOrigin,
            Point rowOrigin)
        {
            _whisperPickerSelectedTexture = selectedTexture;
            _whisperPickerRowTexture = rowTexture;
            _whisperPickerSelectedOrigin = selectedOrigin;
            _whisperPickerRowOrigin = rowOrigin;
        }

        public void SetWhisperPickerDialogTextures(
            Texture2D topTexture,
            Texture2D centerTexture,
            Texture2D bottomTexture,
            Texture2D barTexture,
            Texture2D lineTexture,
            Texture2D prevButtonNormalTexture,
            Texture2D prevButtonHoverTexture,
            Texture2D prevButtonPressedTexture,
            Texture2D prevButtonDisabledTexture,
            Texture2D prevButtonKeyFocusedTexture,
            Texture2D nextButtonNormalTexture,
            Texture2D nextButtonHoverTexture,
            Texture2D nextButtonPressedTexture,
            Texture2D nextButtonDisabledTexture,
            Texture2D nextButtonKeyFocusedTexture,
            Texture2D okButtonNormalTexture,
            Texture2D okButtonHoverTexture,
            Texture2D okButtonPressedTexture,
            Texture2D okButtonKeyFocusedTexture,
            Texture2D closeButtonNormalTexture,
            Texture2D closeButtonHoverTexture,
            Texture2D closeButtonPressedTexture,
            Texture2D closeButtonKeyFocusedTexture)
        {
            _whisperPickerDialogTopTexture = topTexture;
            _whisperPickerDialogCenterTexture = centerTexture;
            _whisperPickerDialogBottomTexture = bottomTexture;
            _whisperPickerDialogBarTexture = barTexture;
            _whisperPickerDialogLineTexture = lineTexture;
            _whisperPickerPrevButtonVisuals = new WhisperPickerButtonVisuals
            {
                Normal = prevButtonNormalTexture,
                Hover = prevButtonHoverTexture,
                Pressed = prevButtonPressedTexture,
                Disabled = prevButtonDisabledTexture,
                KeyFocused = prevButtonKeyFocusedTexture
            };
            _whisperPickerNextButtonVisuals = new WhisperPickerButtonVisuals
            {
                Normal = nextButtonNormalTexture,
                Hover = nextButtonHoverTexture,
                Pressed = nextButtonPressedTexture,
                Disabled = nextButtonDisabledTexture,
                KeyFocused = nextButtonKeyFocusedTexture
            };
            _whisperPickerOkButtonVisuals = new WhisperPickerButtonVisuals
            {
                Normal = okButtonNormalTexture,
                Hover = okButtonHoverTexture,
                Pressed = okButtonPressedTexture,
                KeyFocused = okButtonKeyFocusedTexture
            };
            _whisperPickerCloseButtonVisuals = new WhisperPickerButtonVisuals
            {
                Normal = closeButtonNormalTexture,
                Hover = closeButtonHoverTexture,
                Pressed = closeButtonPressedTexture,
                KeyFocused = closeButtonKeyFocusedTexture
            };
        }

        public void SetWhisperPickerComboTextures(
            Texture2D comboNormalTexture,
            Texture2D comboHoverTexture,
            Texture2D comboPressedTexture,
            Texture2D comboDisabledTexture,
            Texture2D comboToggleNormalTexture,
            Texture2D comboToggleHoverTexture,
            Texture2D comboTogglePressedTexture,
            Texture2D comboToggleDisabledTexture)
        {
            _whisperPickerComboVisuals = new WhisperPickerButtonVisuals
            {
                Normal = comboNormalTexture,
                Hover = comboHoverTexture,
                Pressed = comboPressedTexture,
                Disabled = comboDisabledTexture
            };
            _whisperPickerComboToggleVisuals = new WhisperPickerButtonVisuals
            {
                Normal = comboToggleNormalTexture,
                Hover = comboToggleHoverTexture,
                Pressed = comboTogglePressedTexture,
                Disabled = comboToggleDisabledTexture
            };
        }

        public void SetWhisperPickerComboDropdownTextures(
            Texture2D normalLeftTexture,
            Texture2D normalCenterTexture,
            Texture2D normalRightTexture,
            Texture2D hoverLeftTexture,
            Texture2D hoverCenterTexture,
            Texture2D hoverRightTexture,
            Texture2D pressedLeftTexture,
            Texture2D pressedCenterTexture,
            Texture2D pressedRightTexture,
            Texture2D disabledLeftTexture,
            Texture2D disabledCenterTexture,
            Texture2D disabledRightTexture,
            Texture2D selectedLeftTexture,
            Texture2D selectedCenterTexture,
            Texture2D selectedRightTexture)
        {
            _whisperPickerComboDropdownVisuals = new WhisperPickerComboDropdownVisuals
            {
                Normal = new WhisperPickerComboDropdownStateVisuals
                {
                    Left = normalLeftTexture,
                    Center = normalCenterTexture,
                    Right = normalRightTexture
                },
                Hover = new WhisperPickerComboDropdownStateVisuals
                {
                    Left = hoverLeftTexture,
                    Center = hoverCenterTexture,
                    Right = hoverRightTexture
                },
                Pressed = new WhisperPickerComboDropdownStateVisuals
                {
                    Left = pressedLeftTexture,
                    Center = pressedCenterTexture,
                    Right = pressedRightTexture
                },
                Disabled = new WhisperPickerComboDropdownStateVisuals
                {
                    Left = disabledLeftTexture,
                    Center = disabledCenterTexture,
                    Right = disabledRightTexture
                },
                Selected = new WhisperPickerComboDropdownStateVisuals
                {
                    Left = selectedLeftTexture,
                    Center = selectedCenterTexture,
                    Right = selectedRightTexture
                }
            };
        }

        internal void SetWhisperPickerDropdownScrollbarSkin(VerticalScrollbarSkin scrollbarSkin)
        {
            _whisperPickerDropdownScrollbarSkin = scrollbarSkin;
        }

        public void SetWhisperPickerDialogButtonOrigins(
            Point okButtonNormalOrigin,
            Point okButtonHoverOrigin,
            Point okButtonPressedOrigin,
            Point okButtonKeyFocusedOrigin,
            Point closeButtonNormalOrigin,
            Point closeButtonHoverOrigin,
            Point closeButtonPressedOrigin,
            Point closeButtonKeyFocusedOrigin)
        {
            _whisperPickerOkButtonVisuals.NormalOrigin = okButtonNormalOrigin;
            _whisperPickerOkButtonVisuals.HoverOrigin = okButtonHoverOrigin;
            _whisperPickerOkButtonVisuals.PressedOrigin = okButtonPressedOrigin;
            _whisperPickerOkButtonVisuals.KeyFocusedOrigin = okButtonKeyFocusedOrigin;
            _whisperPickerCloseButtonVisuals.NormalOrigin = closeButtonNormalOrigin;
            _whisperPickerCloseButtonVisuals.HoverOrigin = closeButtonHoverOrigin;
            _whisperPickerCloseButtonVisuals.PressedOrigin = closeButtonPressedOrigin;
            _whisperPickerCloseButtonVisuals.KeyFocusedOrigin = closeButtonKeyFocusedOrigin;
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
            Rectangle chatEnterBounds,
            Rectangle chatSpace2Bounds)
        {
            _pointNotificationAnchor = frameAnchor;
            _chatTargetLabelPos = chatTargetLabelPos;
            _chatEnterPos = chatEnterPos;
            _chatInputBasePos = chatInputPos;
            _chatLogTextBasePos = chatLogTextPos;
            _chatLogWidth = Math.Max(1, chatLogWidth);
            _chatEnterBounds = chatEnterBounds;
            _chatSpace2Bounds = chatSpace2Bounds;
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

        public void RegisterShortcutTooltipButton(string entryName, UIObject button)
        {
            if (string.IsNullOrWhiteSpace(entryName) || button == null)
            {
                return;
            }

            _shortcutTooltipButtons[entryName] = button;
        }

        public void SetShortcutTooltip(string entryName, string tooltipText)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(tooltipText))
            {
                _shortcutTooltips.Remove(entryName);
                return;
            }

            _shortcutTooltips[entryName] = tooltipText.Trim();
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
                Point buttonDrawPosition = uiBtn.GetDrawPositionByState();
                int drawRelativeX = -(this.Position.X) - buttonDrawPosition.X;
                int drawRelativeY = -(this.Position.Y) - buttonDrawPosition.Y;

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
            DrawHoveredShortcutTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
        }

        private void DrawChatOverlay(SpriteBatch sprite, int tickCount, MapSimulatorChatRenderState chatState)
        {
            _whisperPickerHitRegions.Clear();
            _whisperPickerButtonHitRegions.Clear();
            _whisperPromptBounds = null;
            _whisperPickerBounds = null;
            _whisperPickerComboBounds = null;
            _whisperPickerDropdownBounds = null;
            _whisperPickerDropdownRowContentBounds = null;
            _whisperPickerDropdownScrollBarBounds = null;
            _whisperPickerDropdownScrollPrevBounds = null;
            _whisperPickerDropdownScrollNextBounds = null;
            _whisperPickerDropdownScrollTrackBounds = null;
            _whisperPickerDropdownScrollThumbBounds = null;

            if (!HasTextRenderer())
            {
                _isDraggingWhisperPickerDropdownScrollThumb = false;
                ResetWhisperPickerDropdownScrollRepeatCapture();
                return;
            }

            if (chatState == null)
            {
                _isDraggingWhisperPickerDropdownScrollThumb = false;
                ResetWhisperPickerDropdownScrollRepeatCapture();
                return;
            }

            if (ShouldClearWhisperPickerDropdownScrollThumbCapture(chatState))
            {
                _isDraggingWhisperPickerDropdownScrollThumb = false;
                ResetWhisperPickerDropdownScrollRepeatCapture();
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
            if (chatState == null || !chatState.IsWhisperTargetPickerActive || !HasTextRenderer())
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
            int firstVisibleIndex = MapSimulatorChat.ClampWhisperTargetPickerFirstVisibleIndex(
                chatState.WhisperTargetPickerFirstVisibleIndex,
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
            int popupWidth = Math.Max(
                ResolveWhisperPickerMinimumRowWidth(),
                (int)Math.Ceiling(maxCandidateWidth) + (WhisperPickerFramePadding * 2) + 8);
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
            int firstVisibleIndex = MapSimulatorChat.ClampWhisperTargetPickerFirstVisibleIndex(
                chatState.WhisperTargetPickerFirstVisibleIndex,
                candidates.Count,
                WhisperPickerVisibleRows);
            int rowHeight = ResolveWhisperPickerModalComboDropdownRowHeight();
            int buttonRowHeight = ResolveWhisperPickerButtonRowHeight();
            int modalWidth = StatusBarChatLayoutRules.ClientWhisperPickerModalWidth;
            string titleText = MapleStoryStringPool.GetOrFallback(0x031E, "Whisper Target");
            int topHeight = _whisperPickerDialogTopTexture?.Height ?? 28;
            int bottomHeight = _whisperPickerDialogBottomTexture?.Height ?? 44;
            int modalHeight = Math.Max(
                topHeight + bottomHeight + 1,
                StatusBarChatLayoutRules.ResolveWhisperPickerModalClientHeight(0));
            Viewport viewport = sprite.GraphicsDevice.Viewport;
            int modalX = viewport.X + Math.Max(0, (viewport.Width - modalWidth) / 2);
            int modalY = viewport.Y + Math.Max(0, (viewport.Height - modalHeight) / 2);
            _whisperPickerBounds = new Rectangle(modalX, modalY, modalWidth, modalHeight);

            if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, viewport.Bounds, new Color(0, 0, 0, 132));
            }

            DrawModalWhisperPickerFrame(sprite, _whisperPickerBounds.Value);

            int contentY = modalY + topHeight + WhisperPickerModalHeaderGap;
            if (_whisperPickerDialogBarTexture != null)
            {
                int barX = modalX + Math.Max(0, (modalWidth - _whisperPickerDialogBarTexture.Width) / 2);
                sprite.Draw(_whisperPickerDialogBarTexture, new Vector2(barX, modalY + 6), Color.White);
            }

            Rectangle comboBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalComboBounds(
                _whisperPickerBounds.Value,
                contentY,
                StatusBarChatLayoutRules.ClientWhisperPickerModalComboHeight,
                ResolveWhisperPickerModalDividerWidth(modalWidth));
            _whisperPickerComboBounds = comboBounds;
            _whisperPickerComboToggleBounds = null;
            Vector2 titlePos = new Vector2(comboBounds.X, modalY + 8f);
            DrawTextWithShadow(
                sprite,
                titleText,
                titlePos,
                new Color(244, 240, 227),
                Color.Black);
            if (_whisperPickerDialogLineTexture != null)
            {
                int dividerWidth = ResolveWhisperPickerModalDividerWidth(modalWidth);
                int dividerX = modalX + Math.Max(0, (modalWidth - dividerWidth) / 2);
                DrawModalWhisperPickerFrameSlice(
                    sprite,
                    _whisperPickerDialogLineTexture,
                    new Rectangle(dividerX, contentY - 4, dividerWidth, _whisperPickerDialogLineTexture.Height));
            }

            DrawWhisperPickerComboBox(sprite, comboBounds, chatState);

            if (chatState.IsWhisperTargetPickerComboDropdownOpen && visibleCount > 0)
            {
                Rectangle listBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownBounds(
                    comboBounds,
                    rowHeight,
                    visibleCount,
                    candidates.Count,
                    ResolveWhisperPickerMinimumRowWidth(),
                    ResolveWhisperPickerMaxCandidateWidth(chatState, firstVisibleIndex, visibleCount));
                _whisperPickerDropdownBounds = listBounds;
                _whisperPickerBounds = Rectangle.Union(_whisperPickerBounds.Value, listBounds);
                MouseState mouseState = Mouse.GetState();
                bool showScrollbar = candidates.Count > visibleCount;
                Rectangle rowContentBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownRowContentBounds(
                    listBounds,
                    showScrollbar,
                    ResolveWhisperPickerDropdownScrollbarWidth());
                _whisperPickerDropdownRowContentBounds = rowContentBounds;
                int dropdownTextTopInset = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownTextTopInset(showScrollbar);

                for (int i = 0; i < visibleCount; i++)
                {
                    int candidateIndex = firstVisibleIndex + i;
                    string candidateText = candidateIndex >= 0 && candidateIndex < candidates.Count
                        ? candidates[candidateIndex]
                        : string.Empty;

                    Rectangle rowBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalVisibleRowBounds(
                        rowContentBounds,
                        rowHeight,
                        i,
                        visibleCount);
                    DrawWhisperPickerModalComboDropdownRow(
                        sprite,
                        rowBounds,
                        candidateText,
                        ResolveWhisperPickerClientComboSelectIndex(firstVisibleIndex, i),
                        ResolveWhisperPickerClientComboDeleteIndex(i),
                        dropdownTextTopInset,
                        isSelected: candidateIndex == chatState.WhisperTargetPickerSelectionIndex,
                        registerHitRegion: true,
                        mouseState: mouseState);
                }

                if (showScrollbar)
                {
                    DrawWhisperPickerDropdownScrollbar(
                        sprite,
                        listBounds,
                        firstVisibleIndex,
                        candidates.Count,
                        visibleCount,
                        mouseState);
                }
            }

            int buttonY = modalY + modalHeight - StatusBarChatLayoutRules.ClientWhisperPickerModalButtonBottomOffset;
            DrawWhisperPickerButtonRow(
                sprite,
                modalX,
                buttonY,
                modalWidth,
                buttonRowHeight,
                chatState.WhisperTargetPickerModalButtonFocus,
                chatState.WhisperTargetPickerModalFocusTarget);
        }

        private void DrawWhisperPickerComboBox(
            SpriteBatch sprite,
            Rectangle comboBounds,
            MapSimulatorChatRenderState chatState)
        {
            MouseState mouseState = Mouse.GetState();
            bool comboHovered = comboBounds.Contains(mouseState.X, mouseState.Y);
            Rectangle comboChromeBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalComboChromeBounds(
                comboBounds,
                Math.Max(
                    StatusBarChatLayoutRules.ClientWhisperPickerModalComboChromeHeight,
                    _whisperPickerComboVisuals.Normal?.Height ?? 0));
            Rectangle comboToggleBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalComboToggleBounds(
                comboBounds,
                _whisperPickerComboToggleVisuals.Normal?.Width ?? StatusBarChatLayoutRules.ClientWhisperPickerModalComboButtonWidth,
                _whisperPickerComboToggleVisuals.Normal?.Height ?? comboBounds.Height);
            _whisperPickerComboToggleBounds = comboToggleBounds;
            bool toggleHovered = comboToggleBounds.Contains(mouseState.X, mouseState.Y);
            bool comboPressed = comboHovered && mouseState.LeftButton == ButtonState.Pressed && !_pressedWhisperPickerComboToggle;
            bool togglePressed = (_pressedWhisperPickerComboToggle && mouseState.LeftButton == ButtonState.Pressed)
                || (chatState.IsWhisperTargetPickerComboDropdownOpen && !toggleHovered);
            Texture2D comboTexture = _whisperPickerComboVisuals.ResolveTexture(
                PacketScriptOwnerVisualStateResolver.ResolveButtonState(
                    true,
                    comboHovered,
                    comboPressed),
                keyFocused: false);
            if (comboTexture != null)
            {
                sprite.Draw(comboTexture, comboChromeBounds, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, comboChromeBounds, new Color(245, 245, 245, 240));
            }

            Texture2D comboToggleTexture = _whisperPickerComboToggleVisuals.ResolveTexture(
                PacketScriptOwnerVisualStateResolver.ResolveButtonState(
                    true,
                    toggleHovered,
                    togglePressed),
                keyFocused: false);
            if (comboToggleTexture != null)
            {
                sprite.Draw(comboToggleTexture, comboToggleBounds, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, comboToggleBounds, new Color(186, 186, 186, 240));
            }

            float comboTextX = comboBounds.X + StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextLeftInset;
            int comboTextMaxWidth = StatusBarChatLayoutRules.ResolveWhisperPickerModalComboTextMaxWidth(
                comboBounds,
                comboToggleBounds,
                StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextLeftInset);
            string inputText = chatState?.InputText ?? string.Empty;
            int cursorPosition = Math.Clamp(chatState?.CursorPosition ?? 0, 0, inputText.Length);
            string visibleComboText = ResolveWhisperPickerModalComboVisibleText(
                inputText,
                cursorPosition,
                comboTextMaxWidth,
                value => MeasureChatText(value).X,
                out int visibleComboCursorOffset);
            int comboTextTopInset = StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextTopInset;
            DrawTextWithShadow(
                sprite,
                visibleComboText,
                new Vector2(
                    comboTextX,
                    comboBounds.Y + comboTextTopInset),
                new Color(24, 24, 24),
                new Color(255, 255, 255, 160));

            if (_pixelTexture == null
                || !ShouldDrawWhisperPickerModalComboCaret(
                    chatState?.IsWhisperTargetPickerActive == true,
                    chatState?.WhisperTargetPickerPresentation ?? MapSimulatorChat.WhisperTargetPickerPresentation.Inline,
                    chatState?.WhisperTargetPickerModalFocusTarget ?? MapSimulatorChat.WhisperTargetPickerModalFocusTarget.ComboBox,
                    chatState?.IsWhisperTargetPickerComboDropdownOpen == true,
                    Environment.TickCount))
            {
                return;
            }

            string textBeforeCursor = visibleComboCursorOffset <= 0
                ? string.Empty
                : visibleComboText.Substring(0, Math.Clamp(visibleComboCursorOffset, 0, visibleComboText.Length));
            int caretX = (int)Math.Round(comboTextX + MeasureChatText(textBeforeCursor).X);
            int caretHeight = Math.Clamp(Math.Max(1, ResolveFontLineSpacing() - 1), 1, Math.Max(1, comboBounds.Height - 4));
            int caretY = comboBounds.Y + Math.Max(0, comboTextTopInset - 1);
            sprite.Draw(
                _pixelTexture,
                new Rectangle(
                    caretX,
                    caretY,
                    1,
                    caretHeight),
                Color.White);
        }

        private void DrawWhisperPickerModalComboDropdownRow(
            SpriteBatch sprite,
            Rectangle rowBounds,
            string text,
            int clientComboSelectIndex,
            int clientComboDeleteIndex,
            int textTopInset,
            bool isSelected,
            bool registerHitRegion,
            MouseState mouseState)
        {
            bool hovered = rowBounds.Contains(mouseState.X, mouseState.Y);
            bool pressed = hovered && mouseState.LeftButton == ButtonState.Pressed;
            PacketScriptOwnerButtonVisualState state = PacketScriptOwnerVisualStateResolver.ResolveButtonState(
                true,
                hovered,
                pressed);
            WhisperPickerComboDropdownStateVisuals rowVisuals = _whisperPickerComboDropdownVisuals.ResolveState(
                state,
                isSelected);

            if (rowVisuals.HasAnyTexture)
            {
                DrawWhisperPickerComboDropdownRowBackground(sprite, rowBounds, rowVisuals);
            }
            else
            {
                DrawWhisperPickerRow(
                    sprite,
                    rowBounds.X,
                    rowBounds.Y,
                    rowBounds.Width,
                    rowBounds.Height,
                    text,
                    isSelected,
                    registerHitRegion);
                return;
            }

            Color textColor = isSelected || pressed
                ? Color.White
                : new Color(24, 24, 24);
            string clippedRowText = ResolveWhisperPickerModalDropdownDisplayText(
                text,
                rowBounds.Width,
                StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextLeftInset,
                rightPadding: 0,
                value => MeasureChatText(value).X);
            DrawTextWithShadow(
                sprite,
                clippedRowText,
                new Vector2(
                    rowBounds.X + StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextLeftInset,
                    rowBounds.Y + Math.Max(0, textTopInset)),
                textColor,
                isSelected || pressed ? Color.Black : new Color(255, 255, 255, 160));

            if (registerHitRegion && !string.IsNullOrWhiteSpace(text))
            {
                _whisperPickerHitRegions.Add(new WhisperPickerHitRegion
                {
                    Bounds = rowBounds,
                    WhisperTarget = text,
                    ClientComboSelectIndex = clientComboSelectIndex,
                    ClientComboDeleteIndex = clientComboDeleteIndex
                });
            }
        }

        private void DrawWhisperPickerComboDropdownRowBackground(
            SpriteBatch sprite,
            Rectangle rowBounds,
            WhisperPickerComboDropdownStateVisuals rowVisuals)
        {
            Texture2D leftTexture = rowVisuals.Left;
            Texture2D centerTexture = rowVisuals.Center;
            Texture2D rightTexture = rowVisuals.Right;
            int leftWidth = leftTexture?.Width ?? StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownLeftSliceWidth;
            int rightWidth = rightTexture?.Width ?? StatusBarChatLayoutRules.ClientWhisperPickerModalComboDropdownRightSliceWidth;
            Rectangle centerBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalComboDropdownCenterSliceBounds(
                rowBounds,
                leftWidth,
                rightWidth);

            DrawComboDropdownSlice(sprite, leftTexture, new Rectangle(rowBounds.X, rowBounds.Y, leftWidth, rowBounds.Height));
            DrawComboDropdownSlice(sprite, centerTexture, centerBounds);
            DrawComboDropdownSlice(sprite, rightTexture, new Rectangle(rowBounds.Right - rightWidth, rowBounds.Y, rightWidth, rowBounds.Height));
        }

        private void DrawComboDropdownSlice(SpriteBatch sprite, Texture2D texture, Rectangle destination)
        {
            if (destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            if (texture != null)
            {
                sprite.Draw(texture, destination, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, destination, new Color(238, 238, 238, 255));
            }
        }

        private void DrawWhisperPickerDropdownScrollbar(
            SpriteBatch sprite,
            Rectangle listBounds,
            int firstVisibleIndex,
            int candidateCount,
            int visibleCount,
            MouseState mouseState)
        {
            int scrollbarWidth = ResolveWhisperPickerDropdownScrollbarWidth();
            int prevHeight = _whisperPickerDropdownScrollbarSkin?.PrevHeight ?? 12;
            int nextHeight = _whisperPickerDropdownScrollbarSkin?.NextHeight ?? 12;
            int thumbHeight = _whisperPickerDropdownScrollbarSkin?.ThumbHeight ?? 26;
            int maxScrollOffset = MapSimulatorChat.ResolveWhisperTargetPickerMaxScrollOffset(candidateCount, visibleCount);
            Rectangle scrollbarBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownScrollBarBounds(
                listBounds,
                scrollbarWidth);
            Rectangle prevBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownScrollPrevBounds(scrollbarBounds, prevHeight);
            Rectangle nextBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownScrollNextBounds(scrollbarBounds, nextHeight);
            Rectangle trackBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownScrollTrackBounds(
                scrollbarBounds,
                prevHeight,
                nextHeight);
            Rectangle thumbBounds = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownScrollThumbBounds(
                trackBounds,
                thumbHeight,
                firstVisibleIndex,
                maxScrollOffset);
            _whisperPickerDropdownScrollBarBounds = scrollbarBounds;
            _whisperPickerDropdownScrollPrevBounds = prevBounds;
            _whisperPickerDropdownScrollNextBounds = nextBounds;
            _whisperPickerDropdownScrollTrackBounds = trackBounds;
            _whisperPickerDropdownScrollThumbBounds = thumbBounds;

            DrawWhisperPickerDropdownScrollbarTrack(sprite, trackBounds);

            bool prevEnabled = firstVisibleIndex > 0;
            bool nextEnabled = firstVisibleIndex < maxScrollOffset;
            bool prevPressed = mouseState.LeftButton == ButtonState.Pressed
                && _whisperPickerDropdownScrollRepeatAction == WhisperPickerDropdownScrollRepeatAction.StepPrevious;
            bool nextPressed = mouseState.LeftButton == ButtonState.Pressed
                && _whisperPickerDropdownScrollRepeatAction == WhisperPickerDropdownScrollRepeatAction.StepNext;
            DrawWhisperPickerDropdownScrollbarArrow(
                sprite,
                prevBounds,
                _whisperPickerDropdownScrollbarSkin?.PrevStates,
                _whisperPickerDropdownScrollbarSkin?.PrevDisabled,
                prevEnabled,
                mouseState,
                pressed: prevPressed);
            DrawWhisperPickerDropdownScrollbarArrow(
                sprite,
                nextBounds,
                _whisperPickerDropdownScrollbarSkin?.NextStates,
                _whisperPickerDropdownScrollbarSkin?.NextDisabled,
                nextEnabled,
                mouseState,
                pressed: nextPressed);

            Texture2D thumbTexture = ResolveWhisperPickerDropdownScrollbarStateTexture(
                _whisperPickerDropdownScrollbarSkin?.ThumbStates,
                enabled: true,
                hovered: thumbBounds.Contains(mouseState.Position),
                pressed: _isDraggingWhisperPickerDropdownScrollThumb,
                disabledTexture: null);
            if (thumbTexture != null)
            {
                sprite.Draw(thumbTexture, thumbBounds, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, thumbBounds, new Color(173, 173, 173, 255));
            }
        }

        private void DrawWhisperPickerDropdownScrollbarTrack(SpriteBatch sprite, Rectangle trackBounds)
        {
            Texture2D baseTexture = _whisperPickerDropdownScrollbarSkin?.Base;
            if (baseTexture == null)
            {
                if (_pixelTexture != null)
                {
                    sprite.Draw(_pixelTexture, trackBounds, new Color(221, 221, 221, 255));
                }

                return;
            }

            for (int tileY = trackBounds.Y; tileY < trackBounds.Bottom; tileY += baseTexture.Height)
            {
                int tileHeight = Math.Min(baseTexture.Height, trackBounds.Bottom - tileY);
                Rectangle destination = new Rectangle(trackBounds.X, tileY, trackBounds.Width, tileHeight);
                Rectangle? source = tileHeight == baseTexture.Height
                    ? null
                    : new Rectangle(0, 0, baseTexture.Width, tileHeight);
                sprite.Draw(baseTexture, destination, source, Color.White);
            }
        }

        private void DrawWhisperPickerDropdownScrollbarArrow(
            SpriteBatch sprite,
            Rectangle bounds,
            Texture2D[] states,
            Texture2D disabledTexture,
            bool enabled,
            MouseState mouseState,
            bool pressed)
        {
            Texture2D texture = ResolveWhisperPickerDropdownScrollbarStateTexture(
                states,
                enabled,
                bounds.Contains(mouseState.Position),
                pressed,
                disabledTexture);
            if (texture != null)
            {
                sprite.Draw(texture, bounds, Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, bounds, enabled ? new Color(201, 201, 201, 255) : new Color(160, 160, 160, 255));
            }
        }

        private static Texture2D ResolveWhisperPickerDropdownScrollbarStateTexture(
            Texture2D[] states,
            bool enabled,
            bool hovered,
            bool pressed,
            Texture2D disabledTexture)
        {
            if (!enabled)
            {
                return disabledTexture ?? states?[0];
            }

            if (states == null || states.Length == 0)
            {
                return null;
            }

            if (pressed && states.Length > 2 && states[2] != null)
            {
                return states[2];
            }

            if (hovered && states.Length > 1 && states[1] != null)
            {
                return states[1];
            }

            return states[0];
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
            Point rowOrigin = isSelected ? _whisperPickerSelectedOrigin : _whisperPickerRowOrigin;
            Point rowOriginDelta = StatusBarChatLayoutRules.ResolveWhisperPickerRowOriginDelta(
                _whisperPickerRowOrigin,
                rowOrigin);
            if (rowTexture != null)
            {
                sprite.Draw(
                    rowTexture,
                    new Rectangle(
                        x + rowOriginDelta.X,
                        y + rowOriginDelta.Y,
                        width,
                        height),
                    Color.White);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture,
                    new Rectangle(
                        x + rowOriginDelta.X,
                        y + rowOriginDelta.Y,
                        width,
                        height),
                    isSelected ? new Color(83, 116, 168, 235) : new Color(28, 33, 45, 235));
            }

            DrawTextWithShadow(
                sprite,
                ResolveWhisperPickerModalDropdownDisplayText(
                    text,
                    Math.Max(1, width - Math.Max(0, rowOriginDelta.X)),
                    StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextLeftInset,
                    rightPadding: 0,
                    value => MeasureChatText(value).X),
                new Vector2(
                    x + StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextLeftInset + rowOriginDelta.X,
                    y + rowOriginDelta.Y + StatusBarChatLayoutRules.ClientWhisperPickerModalComboTextTopInset),
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

        private int ResolveWhisperPickerMinimumRowWidth()
        {
            return Math.Max(
                Math.Max(96, _whisperPickerComboDropdownVisuals.ResolveMinimumWidth()),
                Math.Max(
                    _whisperPickerSelectedTexture?.Width ?? 0,
                    _whisperPickerRowTexture?.Width ?? 0));
        }

        private int ResolveWhisperPickerModalComboDropdownRowHeight()
        {
            return _whisperPickerComboDropdownVisuals.ResolveRowHeight();
        }

        private int ResolveWhisperPickerDropdownScrollbarWidth()
        {
            return _whisperPickerDropdownScrollbarSkin?.Width ?? 11;
        }

        private int ResolveWhisperPickerModalDividerWidth(int modalWidth)
        {
            int dividerWidth = _whisperPickerDialogLineTexture?.Width ?? 0;
            return dividerWidth > 0 ? Math.Min(dividerWidth, modalWidth) : 0;
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
                DrawModalWhisperPickerFrameSlice(sprite, _whisperPickerDialogTopTexture, topBounds);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, topBounds, new Color(57, 69, 84, 240));
            }

            if (_whisperPickerDialogCenterTexture != null)
            {
                DrawModalWhisperPickerFrameTiledSlice(sprite, _whisperPickerDialogCenterTexture, centerBounds);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, centerBounds, new Color(235, 228, 213, 244));
            }

            if (_whisperPickerDialogBottomTexture != null)
            {
                DrawModalWhisperPickerFrameSlice(sprite, _whisperPickerDialogBottomTexture, bottomBounds);
            }
            else if (_pixelTexture != null)
            {
                sprite.Draw(_pixelTexture, bottomBounds, new Color(217, 206, 187, 244));
            }
        }

        private static void DrawModalWhisperPickerFrameSlice(
            SpriteBatch sprite,
            Texture2D texture,
            Rectangle destination)
        {
            if (sprite == null || texture == null || destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            Rectangle source = new Rectangle(
                0,
                0,
                Math.Min(texture.Width, destination.Width),
                Math.Min(texture.Height, destination.Height));
            sprite.Draw(
                texture,
                new Rectangle(destination.X, destination.Y, source.Width, source.Height),
                source,
                Color.White);
        }

        private static void DrawModalWhisperPickerFrameTiledSlice(
            SpriteBatch sprite,
            Texture2D texture,
            Rectangle destination)
        {
            if (sprite == null || texture == null || destination.Width <= 0 || destination.Height <= 0)
            {
                return;
            }

            int sourceWidth = Math.Min(texture.Width, destination.Width);
            for (int y = destination.Y; y < destination.Bottom; y += texture.Height)
            {
                int height = Math.Min(texture.Height, destination.Bottom - y);
                sprite.Draw(
                    texture,
                    new Rectangle(destination.X, y, sourceWidth, height),
                    new Rectangle(0, 0, sourceWidth, height),
                    Color.White);
            }
        }

        private void DrawWhisperPickerButtonRow(
            SpriteBatch sprite,
            int modalX,
            int buttonY,
            int modalWidth,
            int buttonRowHeight,
            MapSimulatorChat.WhisperTargetPickerModalButtonFocus focusedButton,
            MapSimulatorChat.WhisperTargetPickerModalFocusTarget focusTarget)
        {
            WhisperPickerButtonVisuals[] visuals =
            {
                _whisperPickerOkButtonVisuals,
                _whisperPickerCloseButtonVisuals
            };
            WhisperPickerButtonAction[] actions =
            {
                WhisperPickerButtonAction.Confirm,
                WhisperPickerButtonAction.Close
            };
            int[] clientLefts =
            {
                StatusBarChatLayoutRules.ClientWhisperPickerModalOkButtonLeft,
                StatusBarChatLayoutRules.ClientWhisperPickerModalCloseButtonLeft
            };

            MouseState mouseState = Mouse.GetState();
            for (int i = 0; i < visuals.Length; i++)
            {
                int normalWidth = visuals[i].Normal?.Width ?? 0;
                int slotWidth = visuals[i].ResolveSlotWidth(Math.Max(1, normalWidth));
                int slotLeft = StatusBarChatLayoutRules.ResolveWhisperPickerButtonSlotLeft(
                    modalX + clientLefts[i],
                    normalWidth,
                    slotWidth);
                Rectangle slotBounds = new Rectangle(slotLeft, buttonY, slotWidth, buttonRowHeight);
                bool hovered = slotBounds.Contains(mouseState.X, mouseState.Y);
                bool pressed = hovered && mouseState.LeftButton == ButtonState.Pressed;
                PacketScriptOwnerButtonVisualState state = PacketScriptOwnerVisualStateResolver.ResolveButtonState(
                    true,
                    hovered,
                    pressed);
                bool keyFocused = !hovered
                    && !pressed
                    && focusTarget == MapSimulatorChat.WhisperTargetPickerModalFocusTarget.FooterButtons
                    && IsWhisperPickerModalButtonFocused(actions[i], focusedButton);
                Texture2D texture = visuals[i].ResolveTexture(state, keyFocused);
                Rectangle buttonBounds = slotBounds;
                if (texture != null)
                {
                    buttonBounds = StatusBarChatLayoutRules.ResolveWhisperPickerButtonVisualBounds(
                        slotBounds,
                        texture.Width,
                        texture.Height,
                        visuals[i].NormalOrigin,
                        visuals[i].ResolveOrigin(texture));
                    sprite.Draw(texture, buttonBounds, Color.White);
                }
                else if (_pixelTexture != null)
                {
                    sprite.Draw(_pixelTexture, slotBounds, new Color(154, 120, 68, 240));
                }

                _whisperPickerButtonHitRegions.Add(new WhisperPickerButtonHitRegion
                {
                    Bounds = slotBounds,
                    Action = actions[i]
                });
            }
        }

        private static bool IsWhisperPickerModalButtonFocused(
            WhisperPickerButtonAction action,
            MapSimulatorChat.WhisperTargetPickerModalButtonFocus focusedButton)
        {
            return (action == WhisperPickerButtonAction.Confirm
                    && focusedButton == MapSimulatorChat.WhisperTargetPickerModalButtonFocus.Confirm)
                || (action == WhisperPickerButtonAction.Close
                    && focusedButton == MapSimulatorChat.WhisperTargetPickerModalButtonFocus.Close);
        }

        private int ResolveWhisperPickerButtonRowHeight()
        {
            return Math.Max(
                18,
                Math.Max(
                    _whisperPickerOkButtonVisuals.ResolveSlotHeight(0),
                    _whisperPickerCloseButtonVisuals.ResolveSlotHeight(0)));
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
            if (!HasTextRenderer())
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
            if (!HasTextRenderer() || string.IsNullOrEmpty(inputText))
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

            if (_font == null)
            {
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
                || !HasTextRenderer())
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

        internal static string ResolveWhisperPickerModalComboDisplayText(
            string inputText,
            float maxWidth,
            Func<string, float> measureWidth)
        {
            if (measureWidth == null)
            {
                throw new ArgumentNullException(nameof(measureWidth));
            }

            string text = inputText ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            float safeMaxWidth = Math.Max(1f, maxWidth);
            if (measureWidth(text) <= safeMaxWidth)
            {
                return text;
            }

            int low = 0;
            int high = text.Length;
            int best = 0;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (measureWidth(text.Substring(0, mid)) <= safeMaxWidth)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best > 0 ? text.Substring(0, best) : string.Empty;
        }

        internal static string ResolveWhisperPickerModalComboVisibleText(
            string inputText,
            int cursorPosition,
            float maxWidth,
            Func<string, float> measureWidth,
            out int visibleCursorOffset)
        {
            if (measureWidth == null)
            {
                throw new ArgumentNullException(nameof(measureWidth));
            }

            visibleCursorOffset = 0;
            string text = inputText ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            int safeCursorPosition = Math.Clamp(cursorPosition, 0, text.Length);
            float safeMaxWidth = Math.Max(1f, maxWidth);
            if (measureWidth(text) <= safeMaxWidth)
            {
                visibleCursorOffset = safeCursorPosition;
                return text;
            }

            int startIndex = safeCursorPosition;
            while (startIndex > 0)
            {
                string candidate = text.Substring(startIndex - 1, safeCursorPosition - startIndex + 1);
                if (measureWidth(candidate) > safeMaxWidth)
                {
                    break;
                }

                startIndex--;
            }

            int endIndex = safeCursorPosition;
            while (endIndex < text.Length)
            {
                string candidate = text.Substring(startIndex, endIndex - startIndex + 1);
                if (measureWidth(candidate) > safeMaxWidth)
                {
                    break;
                }

                endIndex++;
            }

            visibleCursorOffset = safeCursorPosition - startIndex;
            return text.Substring(startIndex, Math.Max(0, endIndex - startIndex));
        }

        internal static string ResolveWhisperPickerModalDropdownDisplayText(
            string text,
            int rowWidth,
            int leftPadding,
            int rightPadding,
            Func<string, float> measureWidth)
        {
            int safeRowWidth = Math.Max(1, rowWidth);
            int safeLeftPadding = Math.Max(0, leftPadding);
            int safeRightPadding = Math.Max(0, rightPadding);
            float maxWidth = Math.Max(1f, safeRowWidth - safeLeftPadding - safeRightPadding);
            return ResolveWhisperPickerModalComboDisplayText(
                text,
                maxWidth,
                measureWidth);
        }

        private int ResolveFontLineSpacing()
        {
            if (_clientTextRasterizer != null)
            {
                return Math.Max(1, (int)Math.Ceiling(_clientTextRasterizer.MeasureString("Ag").Y));
            }

            return _font?.LineSpacing ?? DefaultChatCursorHeight + 1;
        }

        private bool HasTextRenderer()
        {
            return _clientTextRasterizer != null || _font != null;
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
            _previousRightButtonState = mouseState.RightButton;
            return whisperHandled || buttonHandled;
        }

        private bool HandleWhisperTargetClick(MouseState mouseState)
        {
            if (HandleWhisperPickerDropdownScrollbarInteraction(mouseState))
            {
                return true;
            }

            WhisperTargetHitRegion hoveredRegion = FindWhisperTargetHitRegion(mouseState.X, mouseState.Y);
            WhisperPickerHitRegion hoveredPickerRegion = FindWhisperPickerHitRegion(mouseState.X, mouseState.Y);
            WhisperPickerButtonHitRegion hoveredButtonRegion = FindWhisperPickerButtonHitRegion(mouseState.X, mouseState.Y);
            int hoveredPickerClientRowIndex = ResolveHoveredWhisperPickerClientComboRowIndex(mouseState.X, mouseState.Y, hoveredPickerRegion);
            int hoveredPickerClientDeleteIndex = ResolveHoveredWhisperPickerClientComboDeleteIndex(mouseState.X, mouseState.Y, hoveredPickerRegion);
            bool promptHovered = _whisperPromptBounds?.Contains(mouseState.X, mouseState.Y) == true;
            bool comboHovered = _whisperPickerComboBounds?.Contains(mouseState.X, mouseState.Y) == true;
            bool comboToggleHovered = _whisperPickerComboToggleBounds?.Contains(mouseState.X, mouseState.Y) == true;
            bool dropdownHovered = _whisperPickerDropdownBounds?.Contains(mouseState.X, mouseState.Y) == true;
            MapSimulatorChatRenderState chatState = _chatStateProvider?.Invoke();
            bool modalWhisperPickerActive = chatState != null
                && chatState.IsWhisperTargetPickerActive
                && chatState.WhisperTargetPickerPresentation == MapSimulatorChat.WhisperTargetPickerPresentation.Modal;
            bool isPressStarted = mouseState.LeftButton == ButtonState.Pressed
                && _previousLeftButtonState == ButtonState.Released;
            bool isRelease = mouseState.LeftButton == ButtonState.Released
                && _previousLeftButtonState == ButtonState.Pressed;
            bool isRightPressStarted = mouseState.RightButton == ButtonState.Pressed
                && _previousRightButtonState == ButtonState.Released;
            bool isRightRelease = mouseState.RightButton == ButtonState.Released
                && _previousRightButtonState == ButtonState.Pressed;
            bool hoveredInteractiveElement = hoveredRegion != null
                || hoveredPickerRegion != null
                || hoveredButtonRegion != null
                || promptHovered
                || comboHovered
                || comboToggleHovered
                || dropdownHovered;
            bool consumeDropdownChromePointer = ShouldConsumeWhisperPickerDropdownChromePointerEvent(
                dropdownHovered,
                hasHoveredCandidate: hoveredPickerRegion != null,
                hasHoveredButton: hoveredButtonRegion != null,
                promptHovered,
                comboHovered,
                comboToggleHovered);

            if (dropdownHovered)
            {
                if (hoveredPickerClientRowIndex >= 0)
                {
                    WhisperTargetPickerModalComboDropdownHoverIndexRequested?.Invoke(hoveredPickerClientRowIndex);
                }
                else if (hoveredPickerRegion != null)
                {
                    WhisperTargetPickerModalComboDropdownHoverRequested?.Invoke(hoveredPickerRegion.WhisperTarget);
                }
            }

            if (isRightPressStarted)
            {
                if (ShouldToggleWhisperPickerComboDropdownOnMouseDown(
                        comboHovered,
                        comboToggleHovered,
                        primaryButtonDownMessage: false,
                        secondaryButtonDownMessage: true))
                {
                    WhisperTargetPickerModalComboFocusRequested?.Invoke();
                    WhisperTargetPickerModalComboDropdownToggleRequested?.Invoke();
                    _pressedRightWhisperPickerCandidateTarget = null;
                    return true;
                }

                _pressedRightWhisperPickerCandidateTarget = dropdownHovered
                    ? hoveredPickerRegion?.WhisperTarget
                    : null;
                return dropdownHovered && hoveredPickerRegion != null;
            }

            if (isRightRelease
                && dropdownHovered
                && hoveredPickerClientDeleteIndex >= 0
                && ShouldDeleteHoveredWhisperPickerCandidateOnRightRelease(
                    _pressedRightWhisperPickerCandidateTarget,
                    hoveredPickerRegion?.WhisperTarget,
                    dropdownHovered,
                    hoveredPickerClientDeleteIndex))
            {
                ResetWhisperPickerPointerCaptureState();
                _pressedRightWhisperPickerCandidateTarget = null;
                WhisperTargetPickerModalComboFocusRequested?.Invoke();
                WhisperTargetPickerModalComboDropdownDeleteIndexRequested?.Invoke(hoveredPickerClientDeleteIndex);
                return true;
            }

            if (isRightRelease)
            {
                bool consumedRightPress = !string.IsNullOrWhiteSpace(_pressedRightWhisperPickerCandidateTarget);
                _pressedRightWhisperPickerCandidateTarget = null;
                if (consumedRightPress)
                {
                    return true;
                }
            }

            if (isPressStarted)
            {
                if (ShouldCloseWhisperPickerDropdownOnOutsidePress(
                        _whisperPickerDropdownBounds.HasValue,
                        hoveredInteractiveElement,
                        dropdownHovered))
                {
                    ResetWhisperPickerPointerCaptureState();
                    WhisperTargetPickerModalComboDropdownCloseRequested?.Invoke();
                    return true;
                }

                _pressedWhisperTarget = hoveredRegion?.WhisperTarget ?? hoveredPickerRegion?.WhisperTarget;
                _pressedWhisperPickerCandidate = hoveredPickerRegion != null;
                _pressedWhisperPickerCandidateTarget = hoveredPickerRegion?.WhisperTarget;
                _pressedWhisperPickerButtonAction = hoveredButtonRegion?.Action;
                _pressedWhisperPickerComboControl = comboHovered || comboToggleHovered;
                _pressedWhisperPickerComboToggle = comboToggleHovered;
                if (ShouldToggleWhisperPickerComboDropdownOnMouseDown(
                        comboHovered,
                        comboToggleHovered,
                        primaryButtonDownMessage: true,
                        secondaryButtonDownMessage: false))
                {
                    WhisperTargetPickerModalComboFocusRequested?.Invoke();
                    WhisperTargetPickerModalComboDropdownToggleRequested?.Invoke();
                }
                else if (hoveredButtonRegion != null)
                {
                    WhisperTargetPickerModalButtonFocusRequested?.Invoke();
                }
                else if (hoveredRegion != null || hoveredPickerRegion != null || promptHovered || comboHovered)
                {
                    WhisperTargetPickerModalComboFocusRequested?.Invoke();
                }
                else if (consumeDropdownChromePointer)
                {
                    WhisperTargetPickerModalComboFocusRequested?.Invoke();
                }

                return hoveredRegion != null
                    || hoveredPickerRegion != null
                    || hoveredButtonRegion != null
                    || promptHovered
                    || comboHovered
                    || dropdownHovered;
            }

            if (!isRelease)
            {
                return ShouldConsumeWhisperPickerPointerCapture(
                    _pressedWhisperTarget,
                    _pressedWhisperPickerCandidate,
                    _pressedWhisperPickerButtonAction.HasValue,
                    _pressedWhisperPickerComboControl,
                    _pressedWhisperPickerComboToggle);
            }

            if (promptHovered && string.IsNullOrWhiteSpace(_pressedWhisperTarget))
            {
                ResetWhisperPickerPointerCaptureState();
                WhisperTargetPickerModalComboFocusRequested?.Invoke();
                if (!modalWhisperPickerActive)
                {
                    WhisperTargetPickerRequested?.Invoke();
                }
                return true;
            }

            if (comboToggleHovered
                && _pressedWhisperPickerComboToggle
                && string.IsNullOrWhiteSpace(_pressedWhisperTarget))
            {
                ResetWhisperPickerPointerCaptureState();
                return true;
            }

            if (comboHovered && string.IsNullOrWhiteSpace(_pressedWhisperTarget))
            {
                ResetWhisperPickerPointerCaptureState();
                WhisperTargetPickerModalComboFocusRequested?.Invoke();
                return true;
            }

            if (hoveredButtonRegion != null
                && _pressedWhisperPickerButtonAction.HasValue
                && hoveredButtonRegion.Action == _pressedWhisperPickerButtonAction.Value)
            {
                ResetWhisperPickerPointerCaptureState();
                WhisperTargetPickerModalButtonFocusRequested?.Invoke();
                InvokeWhisperPickerButtonAction(hoveredButtonRegion.Action);
                return true;
            }

            if (ShouldCommitHoveredWhisperPickerCandidateOnRelease(
                    _pressedWhisperPickerCandidate,
                    _pressedWhisperPickerCandidateTarget,
                    hoveredPickerRegion?.WhisperTarget,
                    dropdownHovered,
                    hoveredPickerClientRowIndex))
            {
                ResetWhisperPickerPointerCaptureState();
                WhisperTargetPickerModalComboFocusRequested?.Invoke();
                if (hoveredPickerClientRowIndex >= 0)
                {
                    WhisperTargetPickerModalComboDropdownSelectIndexRequested?.Invoke(hoveredPickerClientRowIndex);
                }
                else
                {
                    WhisperTargetPickerCandidateRequested?.Invoke(hoveredPickerRegion.WhisperTarget);
                }
                return true;
            }

            if (consumeDropdownChromePointer)
            {
                ResetWhisperPickerPointerCaptureState();
                WhisperTargetPickerModalComboFocusRequested?.Invoke();
                return true;
            }

            string pressedWhisperTarget = _pressedWhisperTarget;
            bool pressedWhisperPickerCandidate = _pressedWhisperPickerCandidate;
            bool pressedWhisperPickerComboControl = _pressedWhisperPickerComboControl;
            bool pressedWhisperPickerComboToggle = _pressedWhisperPickerComboToggle;
            bool hadPressedWhisperPickerButtonAction = _pressedWhisperPickerButtonAction.HasValue;
            _pressedWhisperTarget = null;
            _pressedWhisperPickerCandidate = false;
            _pressedWhisperPickerCandidateTarget = null;
            _pressedWhisperPickerButtonAction = null;
            _pressedWhisperPickerComboControl = false;
            _pressedWhisperPickerComboToggle = false;
            if (string.IsNullOrWhiteSpace(pressedWhisperTarget)
                || hoveredRegion == null
                || !string.Equals(pressedWhisperTarget, hoveredRegion.WhisperTarget, StringComparison.OrdinalIgnoreCase))
            {
                return ShouldConsumeWhisperPickerPointerCapture(
                    pressedWhisperTarget,
                    pressedWhisperPickerCandidate,
                    hadPressedWhisperPickerButtonAction,
                    pressedWhisperPickerComboControl,
                    pressedWhisperPickerComboToggle);
            }

            WhisperTargetRequested?.Invoke(hoveredRegion.WhisperTarget);
            return true;
        }

        private int ResolveHoveredWhisperPickerClientComboRowIndex(
            int mouseX,
            int mouseY,
            WhisperPickerHitRegion hoveredPickerRegion)
        {
            if (hoveredPickerRegion != null && hoveredPickerRegion.ClientComboSelectIndex >= 0)
            {
                return hoveredPickerRegion.ClientComboSelectIndex;
            }

            if (!_whisperPickerDropdownBounds.HasValue || !_whisperPickerDropdownRowContentBounds.HasValue)
            {
                return -1;
            }

            MapSimulatorChatRenderState chatState = _chatStateProvider?.Invoke();
            IReadOnlyList<string> candidates = chatState?.WhisperCandidates;
            int candidateCount = candidates?.Count ?? 0;
            if (chatState == null
                || !chatState.IsWhisperTargetPickerActive
                || chatState.WhisperTargetPickerPresentation != MapSimulatorChat.WhisperTargetPickerPresentation.Modal
                || !chatState.IsWhisperTargetPickerComboDropdownOpen
                || candidateCount <= 0)
            {
                return -1;
            }

            int firstVisibleIndex = MapSimulatorChat.ClampWhisperTargetPickerFirstVisibleIndex(
                chatState.WhisperTargetPickerFirstVisibleIndex,
                candidateCount,
                WhisperPickerVisibleRows);
            return ResolveWhisperPickerClientComboRowIndexFromReleasePoint(
                mouseX,
                mouseY,
                _whisperPickerDropdownRowContentBounds.Value,
                _whisperPickerDropdownBounds.Value,
                firstVisibleIndex,
                candidateCount,
                ResolveWhisperPickerModalComboDropdownRowHeight());
        }

        private int ResolveHoveredWhisperPickerClientComboDeleteIndex(
            int mouseX,
            int mouseY,
            WhisperPickerHitRegion hoveredPickerRegion)
        {
            if (hoveredPickerRegion != null && hoveredPickerRegion.ClientComboDeleteIndex >= 0)
            {
                return hoveredPickerRegion.ClientComboDeleteIndex;
            }

            if (!_whisperPickerDropdownBounds.HasValue || !_whisperPickerDropdownRowContentBounds.HasValue)
            {
                return -1;
            }

            MapSimulatorChatRenderState chatState = _chatStateProvider?.Invoke();
            int candidateCount = chatState?.WhisperCandidates?.Count ?? 0;
            if (chatState == null
                || !chatState.IsWhisperTargetPickerActive
                || chatState.WhisperTargetPickerPresentation != MapSimulatorChat.WhisperTargetPickerPresentation.Modal
                || !chatState.IsWhisperTargetPickerComboDropdownOpen
                || candidateCount <= 0)
            {
                return -1;
            }

            return ResolveWhisperPickerClientComboDeleteIndexFromReleasePoint(
                mouseX,
                mouseY,
                _whisperPickerDropdownRowContentBounds.Value,
                _whisperPickerDropdownBounds.Value,
                candidateCount,
                ResolveWhisperPickerModalComboDropdownRowHeight());
        }

        private bool HandleWhisperPickerDropdownScrollbarInteraction(MouseState mouseState)
        {
            bool isPressStarted = mouseState.LeftButton == ButtonState.Pressed
                && _previousLeftButtonState == ButtonState.Released;
            bool isRelease = mouseState.LeftButton == ButtonState.Released
                && _previousLeftButtonState == ButtonState.Pressed;

            if (!_whisperPickerDropdownScrollBarBounds.HasValue)
            {
                if (isRelease)
                {
                    ResetWhisperPickerDropdownScrollRepeatCapture();
                }

                return false;
            }

            if (_isDraggingWhisperPickerDropdownScrollThumb)
            {
                if (ShouldKeepWhisperPickerDropdownScrollThumbCapture(
                        _isDraggingWhisperPickerDropdownScrollThumb,
                        mouseState.LeftButton))
                {
                    if (_whisperPickerDropdownScrollTrackBounds.HasValue && _whisperPickerDropdownScrollThumbBounds.HasValue)
                    {
                        WhisperTargetPickerModalComboFocusRequested?.Invoke();
                        int maxScrollOffset = MapSimulatorChat.ResolveWhisperTargetPickerMaxScrollOffset(
                            _chatStateProvider?.Invoke()?.WhisperCandidates?.Count ?? 0,
                            WhisperPickerVisibleRows);
                        int thumbTop = mouseState.Y - _whisperPickerDropdownScrollThumbDragOffsetY;
                        int firstVisibleIndex = StatusBarChatLayoutRules.ResolveWhisperPickerModalDropdownScrollOffsetFromThumbTop(
                            _whisperPickerDropdownScrollTrackBounds.Value,
                            _whisperPickerDropdownScrollThumbBounds.Value.Height,
                            thumbTop,
                            maxScrollOffset);
                        WhisperTargetPickerModalComboDropdownScrollPositionRequested?.Invoke(firstVisibleIndex);
                    }

                    return true;
                }

                _isDraggingWhisperPickerDropdownScrollThumb = false;
                return true;
            }

            if (TryHandleWhisperPickerDropdownScrollbarAutoRepeat(mouseState, isRelease))
            {
                return true;
            }

            if (!isPressStarted)
            {
                return false;
            }

            Point mousePosition = mouseState.Position;
            if (!_whisperPickerDropdownScrollBarBounds.Value.Contains(mousePosition))
            {
                return false;
            }

            ResetWhisperPickerDropdownScrollRepeatCapture();
            WhisperTargetPickerModalComboFocusRequested?.Invoke();
            if (_whisperPickerDropdownScrollThumbBounds?.Contains(mousePosition) == true)
            {
                _isDraggingWhisperPickerDropdownScrollThumb = true;
                _whisperPickerDropdownScrollThumbDragOffsetY = mouseState.Y - _whisperPickerDropdownScrollThumbBounds.Value.Y;
                return true;
            }

            if (_whisperPickerDropdownScrollPrevBounds?.Contains(mousePosition) == true)
            {
                StartWhisperPickerDropdownScrollRepeatCapture(
                    WhisperPickerDropdownScrollRepeatAction.StepPrevious);
                PerformWhisperPickerDropdownScrollRepeatAction(
                    _whisperPickerDropdownScrollRepeatAction,
                    mousePosition.Y);
                return true;
            }

            if (_whisperPickerDropdownScrollNextBounds?.Contains(mousePosition) == true)
            {
                StartWhisperPickerDropdownScrollRepeatCapture(
                    WhisperPickerDropdownScrollRepeatAction.StepNext);
                PerformWhisperPickerDropdownScrollRepeatAction(
                    _whisperPickerDropdownScrollRepeatAction,
                    mousePosition.Y);
                return true;
            }

            if (_whisperPickerDropdownScrollTrackBounds?.Contains(mousePosition) == true && _whisperPickerDropdownScrollThumbBounds.HasValue)
            {
                if (mouseState.Y < _whisperPickerDropdownScrollThumbBounds.Value.Y)
                {
                    StartWhisperPickerDropdownScrollRepeatCapture(
                        WhisperPickerDropdownScrollRepeatAction.PagePrevious);
                    PerformWhisperPickerDropdownScrollRepeatAction(
                        _whisperPickerDropdownScrollRepeatAction,
                        mousePosition.Y);
                }
                else if (mouseState.Y >= _whisperPickerDropdownScrollThumbBounds.Value.Bottom)
                {
                    StartWhisperPickerDropdownScrollRepeatCapture(
                        WhisperPickerDropdownScrollRepeatAction.PageNext);
                    PerformWhisperPickerDropdownScrollRepeatAction(
                        _whisperPickerDropdownScrollRepeatAction,
                        mousePosition.Y);
                }

                return true;
            }

            return isRelease;
        }

        private bool TryHandleWhisperPickerDropdownScrollbarAutoRepeat(MouseState mouseState, bool isRelease)
        {
            if (_whisperPickerDropdownScrollRepeatAction == WhisperPickerDropdownScrollRepeatAction.None)
            {
                if (isRelease)
                {
                    ResetWhisperPickerDropdownScrollRepeatCapture();
                }

                return false;
            }

            if (isRelease || mouseState.LeftButton != ButtonState.Pressed)
            {
                ResetWhisperPickerDropdownScrollRepeatCapture();
                return true;
            }

            if (!ShouldContinueWhisperPickerDropdownScrollRepeatAction(mouseState))
            {
                ResetWhisperPickerDropdownScrollRepeatCapture();
                return true;
            }

            int currentTick = Environment.TickCount;
            int heldElapsedMs = unchecked(currentTick - _whisperPickerDropdownScrollRepeatStartTick);
            int sinceLastRepeatMs = unchecked(currentTick - _whisperPickerDropdownScrollRepeatLastTick);
            if (ShouldTriggerWhisperPickerDropdownScrollAutoRepeat(
                    heldElapsedMs,
                    sinceLastRepeatMs))
            {
                PerformWhisperPickerDropdownScrollRepeatAction(
                    _whisperPickerDropdownScrollRepeatAction,
                    mouseState.Y);
                _whisperPickerDropdownScrollRepeatLastTick = currentTick;
            }

            return true;
        }

        private bool ShouldContinueWhisperPickerDropdownScrollRepeatAction(MouseState mouseState)
        {
            if (_whisperPickerDropdownScrollRepeatAction == WhisperPickerDropdownScrollRepeatAction.PagePrevious
                || _whisperPickerDropdownScrollRepeatAction == WhisperPickerDropdownScrollRepeatAction.PageNext)
            {
                if (!_whisperPickerDropdownScrollThumbBounds.HasValue)
                {
                    return false;
                }

                return ShouldContinueWhisperPickerDropdownTrackRepeat(
                    repeatBackward: _whisperPickerDropdownScrollRepeatAction == WhisperPickerDropdownScrollRepeatAction.PagePrevious,
                    mouseState.Y,
                    _whisperPickerDropdownScrollThumbBounds.Value.Y,
                    _whisperPickerDropdownScrollThumbBounds.Value.Bottom);
            }

            return true;
        }

        private void StartWhisperPickerDropdownScrollRepeatCapture(
            WhisperPickerDropdownScrollRepeatAction action)
        {
            _whisperPickerDropdownScrollRepeatAction = action;
            int currentTick = Environment.TickCount;
            _whisperPickerDropdownScrollRepeatStartTick = currentTick;
            _whisperPickerDropdownScrollRepeatLastTick = currentTick;
        }

        private void ResetWhisperPickerDropdownScrollRepeatCapture()
        {
            _whisperPickerDropdownScrollRepeatAction = WhisperPickerDropdownScrollRepeatAction.None;
            _whisperPickerDropdownScrollRepeatStartTick = int.MinValue;
            _whisperPickerDropdownScrollRepeatLastTick = int.MinValue;
        }

        private void PerformWhisperPickerDropdownScrollRepeatAction(
            WhisperPickerDropdownScrollRepeatAction action,
            int mouseY)
        {
            WhisperTargetPickerModalComboFocusRequested?.Invoke();
            switch (action)
            {
                case WhisperPickerDropdownScrollRepeatAction.StepPrevious:
                    WhisperTargetPickerModalComboDropdownScrollRequested?.Invoke(-1);
                    break;
                case WhisperPickerDropdownScrollRepeatAction.StepNext:
                    WhisperTargetPickerModalComboDropdownScrollRequested?.Invoke(1);
                    break;
                case WhisperPickerDropdownScrollRepeatAction.PagePrevious:
                    if (_whisperPickerDropdownScrollThumbBounds.HasValue
                        && mouseY < _whisperPickerDropdownScrollThumbBounds.Value.Y)
                    {
                        WhisperTargetPickerModalComboDropdownPageRequested?.Invoke(-1);
                    }
                    else
                    {
                        ResetWhisperPickerDropdownScrollRepeatCapture();
                    }

                    break;
                case WhisperPickerDropdownScrollRepeatAction.PageNext:
                    if (_whisperPickerDropdownScrollThumbBounds.HasValue
                        && mouseY >= _whisperPickerDropdownScrollThumbBounds.Value.Bottom)
                    {
                        WhisperTargetPickerModalComboDropdownPageRequested?.Invoke(1);
                    }
                    else
                    {
                        ResetWhisperPickerDropdownScrollRepeatCapture();
                    }

                    break;
            }
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
                    WhisperTargetPickerSelectionDeltaRequested?.Invoke(-WhisperPickerVisibleRows);
                    break;
                case WhisperPickerButtonAction.Next:
                    WhisperTargetPickerSelectionDeltaRequested?.Invoke(WhisperPickerVisibleRows);
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
            bool dropdownHovered = _whisperPickerDropdownBounds?.Contains(mouseState.X, mouseState.Y) == true;
            bool comboHovered = _whisperPickerComboBounds?.Contains(mouseState.X, mouseState.Y) == true;
            if (dropdownHovered || comboHovered)
            {
                WhisperTargetPickerModalComboFocusRequested?.Invoke();
                if (dropdownHovered)
                {
                    int candidateCount = _chatStateProvider?.Invoke()?.WhisperCandidates?.Count ?? 0;
                    if (candidateCount > WhisperPickerVisibleRows)
                    {
                        WhisperTargetPickerModalComboDropdownScrollRequested?.Invoke(scrollDelta > 0 ? -steps : steps);
                    }
                    else
                    {
                        WhisperTargetPickerSelectionDeltaRequested?.Invoke(scrollDelta > 0 ? -steps : steps);
                    }
                }
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
            Rectangle bounds = StatusBarChatLayoutRules.ResolveChatInteractionBounds(
                _chatLogTextPos,
                _chatLogWidth,
                _chatEnterBounds,
                _chatSpace2Bounds,
                _chatEnterTexture?.Height ?? 21,
                ChatMaxVisibleLines,
                _chatLogLineHeight);
            return new Rectangle(
                this.Position.X + bounds.X,
                this.Position.Y + bounds.Y,
                bounds.Width,
                bounds.Height);
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
            return MapSimulatorChat.ValidateExplicitWhisperTargetCandidate(
                whisperTargetCandidate,
                localPlayerName,
                out _) == MapSimulatorChat.WhisperTargetValidationResult.Valid;
        }
        #endregion

        private void DrawHoveredShortcutTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (!HasTextRenderer() || _shortcutTooltips.Count == 0)
            {
                return;
            }

            string hoveredEntryName = TryResolveHoveredShortcutTooltipEntryName(Mouse.GetState().X, Mouse.GetState().Y);
            if (string.IsNullOrWhiteSpace(hoveredEntryName)
                || !_shortcutTooltips.TryGetValue(hoveredEntryName, out string tooltipText)
                || string.IsNullOrWhiteSpace(tooltipText)
                || !TryGetShortcutTooltipBounds(hoveredEntryName, out Rectangle anchorBounds))
            {
                return;
            }

            const float textScale = 0.75f;
            const int paddingX = 6;
            const int paddingY = 4;
            Vector2 textSize = ClientTextDrawing.Measure((GraphicsDevice)null, tooltipText, textScale, _font);
            int width = Math.Max(22, (int)Math.Ceiling(textSize.X) + (paddingX * 2));
            int height = Math.Max(16, (int)Math.Ceiling(textSize.Y) + (paddingY * 2));
            int x = anchorBounds.Right + 8;
            int y = anchorBounds.Center.Y - (height / 2);

            if (x + width > renderWidth)
            {
                x = anchorBounds.Left - width - 8;
            }

            if (x < 8)
            {
                x = Math.Max(8, Math.Min(anchorBounds.Left, renderWidth - width - 8));
                y = anchorBounds.Top - height - 6;
            }

            if (y + height > renderHeight - 8)
            {
                y = renderHeight - height - 8;
            }

            if (y < 8)
            {
                y = Math.Min(renderHeight - height - 8, anchorBounds.Bottom + 6);
            }

            Texture2D pixel = EnsureTooltipPixel(sprite.GraphicsDevice);
            Rectangle background = new Rectangle(x, y, width, height);
            Color border = new Color(255, 238, 155, 210);
            sprite.Draw(pixel, background, new Color(24, 28, 37, 220));
            sprite.Draw(pixel, new Rectangle(background.X, background.Y, background.Width, 1), border);
            sprite.Draw(pixel, new Rectangle(background.X, background.Bottom - 1, background.Width, 1), border);
            sprite.Draw(pixel, new Rectangle(background.X, background.Y, 1, background.Height), border);
            sprite.Draw(pixel, new Rectangle(background.Right - 1, background.Y, 1, background.Height), border);
            ClientTextDrawing.Draw(sprite, tooltipText, new Vector2(background.X + paddingX + 1, background.Y + paddingY + 1), Color.Black, textScale, _font);
            ClientTextDrawing.Draw(sprite, tooltipText, new Vector2(background.X + paddingX, background.Y + paddingY), new Color(255, 238, 155), textScale, _font);
        }

        private string TryResolveHoveredShortcutTooltipEntryName(int mouseX, int mouseY)
        {
            foreach (KeyValuePair<string, UIObject> entry in _shortcutTooltipButtons)
            {
                if (TryGetShortcutTooltipBounds(entry.Key, out Rectangle bounds)
                    && bounds.Contains(mouseX, mouseY))
                {
                    return entry.Key;
                }
            }

            return string.Empty;
        }

        private bool TryGetShortcutTooltipBounds(string entryName, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (string.IsNullOrWhiteSpace(entryName)
                || !_shortcutTooltipButtons.TryGetValue(entryName, out UIObject button)
                || button == null
                || !button.ButtonVisible)
            {
                return false;
            }

            int width = button.CanvasSnapshotWidth;
            int height = button.CanvasSnapshotHeight;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            bounds = new Rectangle(Position.X + button.X, Position.Y + button.Y, width, height);
            return true;
        }

        private Texture2D EnsureTooltipPixel(GraphicsDevice graphicsDevice)
        {
            if (_pixelTexture == null && graphicsDevice != null)
            {
                _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            return _pixelTexture;
        }
    }
}
