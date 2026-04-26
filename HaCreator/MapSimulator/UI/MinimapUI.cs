using HaCreator.MapSimulator.Entities;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
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
    /// Mini map window item
    /// </summary>
    public class MinimapUI : BaseDXDrawableItem, IUIObjectEvents
    {
        internal readonly record struct ClientStateTransition(int CurrentOption, int PreviousExpandedOption, bool IsCollapsed);
        internal readonly record struct ClientButtonVisibility(bool MinVisible, bool MaxVisible, bool BigVisible, bool SmallVisible, bool NpcVisible);
        internal readonly record struct ClientButtonPlacement(int X, int Y, bool Visible);

        public enum HelperMarkerType
        {
            Another = 0,
            Friend = 1,
            Guild = 2,
            GuildMaster = 3,
            Match = 4,
            Party = 5,
            PartyMaster = 6,
            UserTrader = 7,
            AnotherTrader = 8,
            User = 9
        }

        public enum NpcMarkerType
        {
            Default,
            QuestStart,
            QuestEnd
        }

        public enum DirectionArrow
        {
            NorthWest,
            North,
            NorthEast,
            West,
            East,
            SouthWest,
            South,
            SouthEast
        }

        internal enum ClientHoverTargetKind
        {
            RemoteDirection = 0,
            Npc = 1,
            Employee = 2,
            Portal = 3,
            TrackedUser = 4
        }

        private readonly BaseDXDrawableItem _pixelDot;
        private readonly BaseDXDrawableItem _expandedFrame;
        private readonly BaseDXDrawableItem _collapsedFrame;
        private readonly BaseDXDrawableItem _userMarker;
        private readonly BaseDXDrawableItem _npcMarker;
        private readonly BaseDXDrawableItem _questStartNpcMarker;
        private readonly BaseDXDrawableItem _questEndNpcMarker;
        private readonly BaseDXDrawableItem _portalMarker;
        private readonly BaseDXDrawableItem _npcListPanel;
        private readonly IReadOnlyDictionary<DirectionArrow, BaseDXDrawableItem> _directionMarkers;
        private readonly HashSet<BaseDXDrawableItem> _directionMarkerSet;
        private readonly IReadOnlyDictionary<HelperMarkerType, BaseDXDrawableItem> _helperMarkers;
        private readonly List<UIObject> uiButtons = new List<UIObject>();
        private readonly List<HoverTargetEntry> _hoverTargets = new();
        private int _hoverTargetCount;

        private UIObject _btnMin;
        private UIObject _btnMax;
        private UIObject _btnBig;
        private UIObject _btnSmall;
        private UIObject _btnMap;
        private UIObject _btnNpc;

        private bool _bIsCollapsedState = false; // minimised minimap state
        private bool _isMiniMapVisible = true;
        private int _currentOption = ClientOptionExpanded;
        private int _previousExpandedOption = ClientOptionExpanded;
        private readonly int _minimapImageWidth;
        private readonly int _minimapImageHeight;
        private readonly Point _compactMarkerOffset;
        private readonly Point _expandedMarkerOffset;
        private IReadOnlyList<NpcItem> _npcMarkers = Array.Empty<NpcItem>();
        private IReadOnlyList<PortalItem> _portalMarkers = Array.Empty<PortalItem>();
        private IReadOnlyList<TrackedUserMarker> _trackedUserMarkers = Array.Empty<TrackedUserMarker>();
        private int _trackedUserMarkerCount;
        private IReadOnlyList<EmployeeMarker> _employeeMarkers = Array.Empty<EmployeeMarker>();
        private int _employeeMarkerCount;
        private bool _showNpcMarkers;
        private SpriteFont _tooltipFont;
        private Texture2D _tooltipPixelTexture;
        private Point? _hoverTooltipAnchorPoint;
        private string _hoverTooltipText;
        private readonly List<string> _hoverTooltipLines = new();
        private float _hoverTooltipMaxWidth;
        private int _hoverTooltipVisibleLineCount;

        private int _lastMinimapToggleTime = 0;
        private const int MINIMAP_TOGGLE_COOLDOWN_MS = 200; // Cooldown in milliseconds
        private const int TOOLTIP_PADDING = 8;
        private const int TOOLTIP_MARGIN = 10;
        private const int TOOLTIP_LINE_GAP = 2;
        private const int ClientTooltipMouseOffset = 20;
        private const int ClientMarkerHoverBoundsWidth = 5;
        private const int ClientMarkerHoverBoundsHeight = 7;
        private const int ClientMarkerHoverBoundsXOffset = -5;
        private const int ClientMarkerHoverBoundsYOffset = -2;
        private const int ClientButtonIdMinimapState = 1000;
        private const int ClientButtonIdMinimapRestore = 1001;
        private const int ClientButtonIdMap = 1002;
        private const int ClientButtonIdOption = 1003;
        private const int ClientOptionExpanded = 0;
        private const int ClientOptionCompact = 1;
        private const int ClientOptionCollapsed = 2;
        private const int ClientTopRowButtonTop = 4;
        private const int ClientTopRowButtonRightPadding = 6;
        private const int ClientOptionButtonRightPadding = 17;
        private const int ClientOptionButtonBottomPadding = 4;
        private bool _useLegacyOptionButtonCycle;

        // Player position on minimap (in minimap coordinates, not world coordinates)
        private int _playerMinimapX = 0;
        private int _playerMinimapY = 0;
        private int _minimapOriginX = 0;
        private int _minimapOriginY = 0;
        private HelperMarkerType? _localPlayerHelperMarkerType;

        public Action WorldMapRequested { get; set; }
        public Func<NpcItem, NpcMarkerType> ResolveNpcMarkerType { get; set; }
        public Func<NpcItem, string> ResolveNpcTooltipText { get; set; }
        public Func<PortalItem, string> ResolvePortalTooltipText { get; set; }
        public Action<Point> WindowPositionChanged { get; set; }

        public sealed class TrackedUserMarker
        {
            public float WorldX { get; set; }
            public float WorldY { get; set; }
            public HelperMarkerType MarkerType { get; set; }
            public bool ShowDirectionOverlay { get; set; } = true;
            public string TooltipText { get; set; }
        }

        public sealed class EmployeeMarker
        {
            public float WorldX { get; set; }
            public float WorldY { get; set; }
            public bool ShowDirectionOverlay { get; set; } = true;
            public HelperMarkerType? PreferredMarkerType { get; set; }
            public string TooltipText { get; set; }
        }

        private sealed class HoverTargetEntry
        {
            public Rectangle Bounds { get; set; }
            public string TooltipText { get; set; }
            public NpcItem Npc { get; set; }
            public PortalItem Portal { get; set; }
            public ClientHoverTargetKind Kind { get; set; }
        }

        /// <summary>
        /// Sets the player's position on the minimap.
        /// Call this before Draw() to update the yellow dot position.
        /// </summary>
        /// <param name="playerWorldX">Player X position in world coordinates</param>
        /// <param name="playerWorldY">Player Y position in world coordinates</param>
        /// <param name="minimapOriginX">World X coordinate that corresponds to minimap position 0</param>
        /// <param name="minimapOriginY">World Y coordinate that corresponds to minimap position 0</param>
        public void SetPlayerPosition(float playerWorldX, float playerWorldY, int minimapOriginX, int minimapOriginY)
        {
            _minimapOriginX = minimapOriginX;
            _minimapOriginY = minimapOriginY;

            // Convert world coordinates to minimap coordinates
            // Minimap is scaled 1:16 from world coordinates
            _playerMinimapX = (int)(playerWorldX - minimapOriginX) / 16;
            _playerMinimapY = (int)(playerWorldY - minimapOriginY) / 16;
        }

        /// <summary>
        /// Constructor for the minimap window
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="_pixelDot"></param>
        public MinimapUI(
            IDXObject frame,
            BaseDXDrawableItem _pixelDot,
            BaseDXDrawableItem expandedFrame,
            BaseDXDrawableItem _collapsedFrame,
            int minimapImageWidth,
            int minimapImageHeight,
            Point compactMarkerOffset,
            Point expandedMarkerOffset,
            BaseDXDrawableItem userMarker = null,
            BaseDXDrawableItem npcMarker = null,
            BaseDXDrawableItem questStartNpcMarker = null,
            BaseDXDrawableItem questEndNpcMarker = null,
            BaseDXDrawableItem npcListPanel = null,
            BaseDXDrawableItem portalMarker = null,
            IReadOnlyDictionary<DirectionArrow, BaseDXDrawableItem> directionMarkers = null,
            IReadOnlyDictionary<HelperMarkerType, BaseDXDrawableItem> helperMarkers = null)
            : base(frame, false)
        {
            this._pixelDot = _pixelDot;
            _expandedFrame = expandedFrame;
            this._collapsedFrame = _collapsedFrame;
            _minimapImageWidth = minimapImageWidth;
            _minimapImageHeight = minimapImageHeight;
            _compactMarkerOffset = compactMarkerOffset;
            _expandedMarkerOffset = expandedMarkerOffset;
            _userMarker = userMarker;
            _npcMarker = npcMarker;
            _questStartNpcMarker = questStartNpcMarker;
            _questEndNpcMarker = questEndNpcMarker;
            _npcListPanel = npcListPanel;
            _portalMarker = portalMarker;
            _directionMarkers = directionMarkers ?? new Dictionary<DirectionArrow, BaseDXDrawableItem>();
            _directionMarkerSet = new HashSet<BaseDXDrawableItem>(_directionMarkers.Values);
            _helperMarkers = helperMarkers ?? new Dictionary<HelperMarkerType, BaseDXDrawableItem>();
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        public void InitializeMinimapButtons(
            UIObject _btnMin,
            UIObject _btnMax,
            UIObject _btnBig,
            UIObject _btnSmall,
            UIObject _btnMap,
            UIObject _btnNpc = null)
        {
            this._btnMin = _btnMin;
            this._btnMax = _btnMax;
            if (_btnBig != null)
                this._btnBig = _btnBig;
            if (_btnSmall != null)
                this._btnSmall = _btnSmall;
            this._btnMap = _btnMap;
            this._btnNpc = _btnNpc;

            uiButtons.Add(_btnMin);
            uiButtons.Add(_btnMax);
            if (_btnBig != null)
                uiButtons.Add(_btnBig);
            if (_btnSmall != null)
                uiButtons.Add(_btnSmall);
            if (_btnNpc != null)
                uiButtons.Add(_btnNpc);
            uiButtons.Add(_btnMap);

            _useLegacyOptionButtonCycle = _btnSmall == null || _expandedFrame == null;
            _btnMax.SetButtonState(_useLegacyOptionButtonCycle ? UIObjectState.Normal : UIObjectState.Disabled); // start maximised
            _btnMin.SetButtonState(UIObjectState.Normal);
            if (_btnSmall != null)
                _btnSmall.SetVisible(false);

            _btnMin.ButtonClickReleased += ObjUIBtMin_ButtonClickReleased;
            _btnMax.ButtonClickReleased += ObjUIBtMax_ButtonClickReleased;
            if (_btnBig != null)
                _btnBig.ButtonClickReleased += ObjUIBtBig_ButtonClickReleased;
            if (_btnSmall != null)
                _btnSmall.ButtonClickReleased += ObjUIBtSmall_ButtonClickReleased;
            if (_btnNpc != null)
                _btnNpc.ButtonClickReleased += ObjUIBtNpc_ButtonClickReleased;
            _btnMap.ButtonClickReleased += ObjUIBtMap_ButtonClickReleased;
            UpdateButtonLayout();
        }

        public void SetNpcMarkers(IReadOnlyList<NpcItem> npcMarkers)
        {
            _npcMarkers = npcMarkers ?? Array.Empty<NpcItem>();

            if (_btnNpc == null)
                return;

            bool hasNpcMarkers = (_npcMarker != null || _questStartNpcMarker != null || _questEndNpcMarker != null) && _npcMarkers.Count > 0;
            _btnNpc.SetVisible(hasNpcMarkers);
            _showNpcMarkers = hasNpcMarkers && _showNpcMarkers;
            _btnNpc.SetButtonState(_showNpcMarkers ? UIObjectState.Disabled : UIObjectState.Normal);
            UpdateButtonLayout();
        }

        public void SetPortalMarkers(IReadOnlyList<PortalItem> portalMarkers)
        {
            _portalMarkers = portalMarkers ?? Array.Empty<PortalItem>();
        }

        public void SetTrackedUserMarkers(IReadOnlyList<TrackedUserMarker> trackedUserMarkers)
        {
            _trackedUserMarkers = trackedUserMarkers ?? Array.Empty<TrackedUserMarker>();
            _trackedUserMarkerCount = _trackedUserMarkers.Count;
        }

        public void SetTrackedUserMarkers(IReadOnlyList<TrackedUserMarker> trackedUserMarkers, int count)
        {
            _trackedUserMarkers = trackedUserMarkers ?? Array.Empty<TrackedUserMarker>();
            _trackedUserMarkerCount = Math.Clamp(count, 0, _trackedUserMarkers.Count);
        }

        public void SetEmployeeMarkers(IReadOnlyList<EmployeeMarker> employeeMarkers)
        {
            _employeeMarkers = employeeMarkers ?? Array.Empty<EmployeeMarker>();
            _employeeMarkerCount = _employeeMarkers.Count;
        }

        public void SetEmployeeMarkers(IReadOnlyList<EmployeeMarker> employeeMarkers, int count)
        {
            _employeeMarkers = employeeMarkers ?? Array.Empty<EmployeeMarker>();
            _employeeMarkerCount = Math.Clamp(count, 0, _employeeMarkers.Count);
        }

        public void SetLocalPlayerHelperMarker(HelperMarkerType? helperMarkerType)
        {
            _localPlayerHelperMarkerType = helperMarkerType;
        }

        public void SetTooltipResources(SpriteFont tooltipFont, Texture2D tooltipPixelTexture)
        {
            _tooltipFont = tooltipFont;
            _tooltipPixelTexture = tooltipPixelTexture;
        }

        public void ResetTransientHoverState()
        {
            _hoverTooltipAnchorPoint = null;
            _hoverTooltipText = null;
            _hoverTooltipLines.Clear();
            _hoverTooltipMaxWidth = 0f;
            _hoverTooltipVisibleLineCount = 0;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            if (!_isMiniMapVisible)
            {
                return;
            }

            // control minimap render UI position via
            //  Position.X, Position.Y

            // Draw the main frame
            if (_bIsCollapsedState)
            {
                _collapsedFrame.Draw(sprite, skeletonMeshRenderer, gameTime,
                   0, 0, centerX, centerY,
                   drawReflectionInfo,
                   renderParameters,
                   TickCount);
            }
            else
            {
                ResetHoverTargets();
                ApplyMarkerOffsetForCurrentState();

                BaseDXDrawableItem expandedFrame = GetActiveExpandedFrame();
                if (ReferenceEquals(expandedFrame, this))
                {
                    base.Draw(sprite, skeletonMeshRenderer, gameTime,
                        0, 0, centerX, centerY,
                        drawReflectionInfo,
                        renderParameters,
                        TickCount);
                }
                else
                {
                    expandedFrame.Draw(sprite, skeletonMeshRenderer, gameTime,
                        0, 0, centerX, centerY,
                        drawReflectionInfo,
                        renderParameters,
                        TickCount);
                }

                // Use stored player position (set via SetPlayerPosition) for accurate dot placement
                // This ensures the dot follows the actual character position, not the viewport center
                int minimapPosX = _playerMinimapX;
                int minimapPosY = _playerMinimapY;

                BaseDXDrawableItem playerMarker = ResolveLocalPlayerMarker();
                bool suppressLegacyPixelDot = ShouldSuppressLegacyPixelDot(playerMarker);
                if (playerMarker != null)
                {
                    if (_localPlayerHelperMarkerType.HasValue)
                    {
                        DrawMarkerWithDirectionOverlay(
                            playerMarker,
                            new Point(minimapPosX, minimapPosY),
                            true,
                            sprite,
                            skeletonMeshRenderer,
                            gameTime,
                            drawReflectionInfo,
                            renderParameters,
                            TickCount);
                    }
                    else
                    {
                        playerMarker.Draw(sprite, skeletonMeshRenderer, gameTime,
                            -Position.X, -Position.Y, minimapPosX, minimapPosY,
                            drawReflectionInfo,
                            renderParameters,
                            TickCount);
                    }
                }

                if (!suppressLegacyPixelDot)
                {
                    _pixelDot.Draw(sprite, skeletonMeshRenderer, gameTime,
                        -Position.X, -Position.Y, minimapPosX, minimapPosY,
                        drawReflectionInfo,
                        renderParameters,
                        TickCount);
                }

                DrawPortalMarkers(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
                DrawTrackedUserMarkers(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
                DrawEmployeeMarkers(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
                DrawNpcMarkers(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
                DrawNpcListPanel(sprite, skeletonMeshRenderer, gameTime, centerX, centerY, renderParameters, TickCount);
                DrawHoveredTooltip(sprite, renderParameters.RenderWidth, renderParameters.RenderHeight);
            }

            // draw minimap buttons
            foreach (UIObject uiBtn in uiButtons)
            {
                if (uiBtn == null || !uiBtn.ButtonVisible)
                    continue;

                BaseDXDrawableItem buttonToDraw = uiBtn.GetBaseDXDrawableItemByState();

                // Position drawn is relative to the MinimapItem
                int drawRelativeX = -(this.Position.X) - uiBtn.X; // Left to right
                int drawRelativeY = -(this.Position.Y) - uiBtn.Y; // Top to bottom

                buttonToDraw.Draw(sprite, skeletonMeshRenderer,
                    gameTime,
                    drawRelativeX,
                    drawRelativeY,
                    centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
        }

        public void MinimiseOrMaximiseMinimap(int currTickCount)
        {
            if (currTickCount - _lastMinimapToggleTime > MINIMAP_TOGGLE_COOLDOWN_MS)
            {
                _lastMinimapToggleTime = currTickCount;
                ToggleMinimapState();
            }
        }

        public bool IsMiniMapVisible => _isMiniMapVisible;

        public bool IsCollapsed => _bIsCollapsedState;

        public bool IsExpandedOptionActive => _isMiniMapVisible && !_bIsCollapsedState && NormalizeExpandedOption(_currentOption) == ClientOptionExpanded;

        public void ReloadMiniMap(bool isVisible)
        {
            _isMiniMapVisible = isVisible;
            if (isVisible)
            {
                return;
            }

            ResetDragState();
            ResetTransientHoverState();
        }

        public void EnsureExpanded()
        {
            if (_bIsCollapsedState)
            {
                ApplyClientStateTransition(ResolveEnsureExpandedTransitionForTesting(
                    _currentOption,
                    _previousExpandedOption,
                    _btnSmall != null && _expandedFrame != null));
            }
        }

        public void EnsureCollapsed()
        {
            if (!_bIsCollapsedState)
            {
                ApplyClientStateTransition(ResolveEnsureCollapsedTransitionForTesting(
                    _currentOption,
                    _previousExpandedOption,
                    _btnSmall != null && _expandedFrame != null));
            }
        }

        public void SetWindowPosition(Point position)
        {
            Position = position;
            _expandedFrame?.CopyObjectPosition(this);
            _collapsedFrame?.CopyObjectPosition(this);
            WindowPositionChanged?.Invoke(position);
        }

        #region IClickableUIObject
        private Point? mouseOffsetOnDragStart = null;

        /// <summary>
        /// Whether the minimap is currently being dragged
        /// </summary>
        public bool IsDragging => mouseOffsetOnDragStart != null;

        /// <summary>
        /// Reset drag state (called when another UI element takes priority)
        /// </summary>
        public void ResetDragState()
        {
            mouseOffsetOnDragStart = null;
        }

        /// <summary>
        /// Check if a point is within the minimap bounds
        /// </summary>
        public bool ContainsPoint(int x, int y)
        {
            if (!_isMiniMapVisible)
            {
                return false;
            }

            BaseDXDrawableItem activeFrame = GetVisibleFrame();
            IDXObject activeFrameDrawn = activeFrame?.LastFrameDrawn ?? activeFrame?.Frame0;
            int frameWidth = activeFrameDrawn?.Width ?? 100;
            int frameHeight = activeFrameDrawn?.Height ?? 100;

            Rectangle rect = new Rectangle(
                this.Position.X,
                this.Position.Y,
                frameWidth,
                frameHeight);

            return rect.Contains(x, y);
        }

        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
            if (!_isMiniMapVisible)
            {
                ResetDragState();
                ResetTransientHoverState();
                return false;
            }

            ResetTransientHoverState();

            foreach (UIObject uiBtn in uiButtons)
            {
                if (uiBtn == null || !uiBtn.ButtonVisible)
                    continue;

                bool bHandled = uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, this.Position.X, this.Position.Y, mouseState);
                if (bHandled)
                {
                    mouseCursor.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }

            if (TrySetHoveredMarkerTooltip(mouseState))
            {
                mouseCursor.SetMouseCursorMovedToClickableItem();
            }

            // handle UI movement
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                // if drag has not started, initialize the offset
                if (mouseOffsetOnDragStart == null)
                {
                    BaseDXDrawableItem activeFrame = GetVisibleFrame();
                    IDXObject activeFrameDrawn = activeFrame?.LastFrameDrawn ?? activeFrame?.Frame0;
                    Rectangle rect = new Rectangle(
                        this.Position.X,
                        this.Position.Y,
                        activeFrameDrawn?.Width ?? 0,
                        activeFrameDrawn?.Height ?? 0);
                    if (!rect.Contains(mouseState.X, mouseState.Y))
                    {
                        return false;
                    }
                    mouseOffsetOnDragStart = new Point(mouseState.X - this.Position.X, mouseState.Y - this.Position.Y);
                }

                // Calculate the mouse position relative to the minimap
                // and move the minimap Position
                int newX = mouseState.X - mouseOffsetOnDragStart.Value.X;
                int newY = mouseState.Y - mouseOffsetOnDragStart.Value.Y;

                // Get the current frame dimensions for boundary checking
                BaseDXDrawableItem dragFrame = GetVisibleFrame();
                IDXObject dragFrameDrawn = dragFrame?.LastFrameDrawn ?? dragFrame?.Frame0;
                int frameWidth = dragFrameDrawn?.Width ?? 0;
                int frameHeight = dragFrameDrawn?.Height ?? 0;

                // Enforce screen boundary constraints
                newX = Math.Max(0, Math.Min(newX, renderWidth - frameWidth));
                newY = Math.Max(0, Math.Min(newY, renderHeight - frameHeight));

                SetWindowPosition(new Point(newX, newY));
            }
            else
            {
                mouseOffsetOnDragStart = null;
            }
            return false;
        }
        #endregion

        #region Events
        /// <summary>
        /// On 'BtMin' clicked
        /// Map minimised mode
        /// </summary>
        private void ObjUIBtMin_ButtonClickReleased(UIObject sender)
        {
            HandleClientButtonClick(ClientButtonIdMinimapState);
        }

        /// <summary>
        /// On 'BtMax' clicked.
        /// Map maximised mode
        /// </summary>
        private void ObjUIBtMax_ButtonClickReleased(UIObject sender)
        {
            HandleClientButtonClick(ClientButtonIdMinimapRestore);
        }

        /// <summary>
        /// On 'BtBig' clicked
        /// </summary>
        private void ObjUIBtBig_ButtonClickReleased(UIObject sender)
        {
            HandleClientButtonClick(ClientButtonIdOption);
        }

        /// <summary>
        /// On 'BtSmall' clicked.
        /// </summary>
        private void ObjUIBtSmall_ButtonClickReleased(UIObject sender)
        {
            HandleClientButtonClick(ClientButtonIdOption);
        }

        /// <summary>
        /// On 'BtNpc' clicked
        /// Toggle NPC marker visibility on the minimap.
        /// </summary>
        private void ObjUIBtNpc_ButtonClickReleased(UIObject sender)
        {
            if (_btnNpc == null || ResolveAnyNpcMarker() == null || _npcMarkers.Count == 0)
                return;

            _showNpcMarkers = !_showNpcMarkers;
            _btnNpc.SetButtonState(_showNpcMarkers ? UIObjectState.Disabled : UIObjectState.Normal);
        }

        /// <summary>
        /// On 'BtMap' clicked
        /// </summary>
        private void ObjUIBtMap_ButtonClickReleased(UIObject sender)
        {
            HandleClientButtonClick(ClientButtonIdMap);
        }

        private void HandleClientButtonClick(int buttonId)
        {
            switch (buttonId)
            {
                case ClientButtonIdMinimapState:
                case ClientButtonIdMinimapRestore:
                case ClientButtonIdOption:
                    ApplyClientStateTransition(ResolveClientStateTransitionForTesting(
                        buttonId,
                        _currentOption,
                        _previousExpandedOption,
                        _btnSmall != null && _expandedFrame != null,
                        _useLegacyOptionButtonCycle));
                    break;
                case ClientButtonIdMap:
                    WorldMapRequested?.Invoke();
                    break;
            }
        }

        private int NormalizeExpandedOption(int option)
        {
            if (_expandedFrame == null || _btnSmall == null)
            {
                return ClientOptionCompact;
            }

            return option == ClientOptionExpanded ? ClientOptionExpanded : ClientOptionCompact;
        }

        private static int NormalizeRememberedExpandedOption(int option)
        {
            return option == ClientOptionExpanded || option == ClientOptionCompact
                ? option
                : ClientOptionCompact;
        }

        private BaseDXDrawableItem GetActiveExpandedFrame()
        {
            return _currentOption == ClientOptionExpanded && _expandedFrame != null ? _expandedFrame : this;
        }

        private BaseDXDrawableItem GetVisibleFrame()
        {
            return _bIsCollapsedState ? _collapsedFrame : GetActiveExpandedFrame();
        }

        private Point GetCurrentMarkerOffset()
        {
            return NormalizeExpandedOption(_currentOption) == ClientOptionExpanded
                ? _expandedMarkerOffset
                : _compactMarkerOffset;
        }

        private void ApplyMarkerOffsetForCurrentState()
        {
            Point markerOffset = GetCurrentMarkerOffset();
            _pixelDot.Position = markerOffset;

            if (_userMarker != null)
            {
                _userMarker.Position = markerOffset;
            }

            if (_npcMarker != null)
            {
                _npcMarker.Position = markerOffset;
            }

            if (_questStartNpcMarker != null)
            {
                _questStartNpcMarker.Position = markerOffset;
            }

            if (_questEndNpcMarker != null)
            {
                _questEndNpcMarker.Position = markerOffset;
            }

            if (_portalMarker != null)
            {
                _portalMarker.Position = markerOffset;
            }

            foreach (BaseDXDrawableItem marker in _helperMarkers.Values)
            {
                if (marker != null)
                {
                    marker.Position = markerOffset;
                }
            }

            foreach (BaseDXDrawableItem marker in _directionMarkers.Values)
            {
                if (marker != null)
                {
                    marker.Position = markerOffset;
                }
            }
        }

        private void SyncFramePositionsFrom(BaseDXDrawableItem source)
        {
            if (source == null)
            {
                return;
            }

            this.CopyObjectPosition(source);
            _expandedFrame?.CopyObjectPosition(source);
            _collapsedFrame?.CopyObjectPosition(source);
        }

        private void UpdateButtonLayout()
        {
            if (_btnMap == null || _btnMax == null || _btnMin == null)
            {
                return;
            }

            ClientButtonVisibility buttonVisibility = ResolveButtonVisibilityForTesting(
                _currentOption,
                _bIsCollapsedState,
                _btnSmall != null && _expandedFrame != null,
                _btnNpc != null && ResolveAnyNpcMarker() != null && _npcMarkers.Count > 0,
                _useLegacyOptionButtonCycle);

            _btnMin.SetVisible(buttonVisibility.MinVisible);
            _btnMax.SetVisible(buttonVisibility.MaxVisible);
            _btnBig?.SetVisible(buttonVisibility.BigVisible);
            _btnSmall?.SetVisible(buttonVisibility.SmallVisible);
            _btnNpc?.SetVisible(buttonVisibility.NpcVisible);

            int frameWidth = GetVisibleFrame()?.Frame0?.Width ?? Frame0?.Width ?? 0;
            int frameHeight = GetVisibleFrame()?.Frame0?.Height ?? Frame0?.Height ?? 0;
            bool supportsExpandedOption = _btnSmall != null && _expandedFrame != null;
            int mapButtonX = ResolveTopRowButtonX(frameWidth, _btnMap.CanvasSnapshotWidth);

            _btnMap.X = mapButtonX;
            _btnMap.Y = ClientTopRowButtonTop;

            int stateButtonX = ResolveAdjacentLeftButtonX(mapButtonX, _btnMax.CanvasSnapshotWidth);
            if (_useLegacyOptionButtonCycle)
            {
                _btnMax.X = stateButtonX;
                _btnMax.Y = ClientTopRowButtonTop;
                _btnMin.X = ResolveAdjacentLeftButtonX(stateButtonX, _btnMin.CanvasSnapshotWidth);
                _btnMin.Y = ClientTopRowButtonTop;
                stateButtonX = _btnMin.X;
            }
            else
            {
                UIObject visibleStateButton = _btnMax.ButtonVisible ? _btnMax : _btnMin;
                UIObject hiddenStateButton = ReferenceEquals(visibleStateButton, _btnMax) ? _btnMin : _btnMax;
                stateButtonX = ResolveAdjacentLeftButtonX(mapButtonX, visibleStateButton?.CanvasSnapshotWidth ?? 0);
                if (visibleStateButton != null)
                {
                    visibleStateButton.X = stateButtonX;
                    visibleStateButton.Y = ClientTopRowButtonTop;
                }

                if (hiddenStateButton != null)
                {
                    hiddenStateButton.X = stateButtonX;
                    hiddenStateButton.Y = ClientTopRowButtonTop;
                }
            }

            int nextTopRowX = stateButtonX;
            if (_btnNpc?.ButtonVisible == true)
            {
                _btnNpc.X = ResolveAdjacentLeftButtonX(nextTopRowX, _btnNpc.CanvasSnapshotWidth);
                _btnNpc.Y = ClientTopRowButtonTop;
                nextTopRowX = _btnNpc.X;
            }

            UIObject sizeToggleButton = _btnSmall?.ButtonVisible == true
                ? _btnSmall
                : _btnBig?.ButtonVisible == true
                    ? _btnBig
                    : null;
            if (sizeToggleButton != null)
            {
                ClientButtonPlacement optionPlacement = ResolveOptionButtonPlacementForTesting(
                    frameWidth,
                    frameHeight,
                    sizeToggleButton.CanvasSnapshotWidth,
                    sizeToggleButton.CanvasSnapshotHeight,
                    supportsExpandedOption);
                sizeToggleButton.X = optionPlacement.X;
                sizeToggleButton.Y = optionPlacement.Y;
            }
        }

        private static int ResolveTopRowButtonX(int frameWidth, int buttonWidth)
        {
            return Math.Max(0, frameWidth - buttonWidth - ClientTopRowButtonRightPadding);
        }

        private static int ResolveAdjacentLeftButtonX(int anchorX, int buttonWidth)
        {
            return Math.Max(0, anchorX - buttonWidth);
        }

        private void ToggleMinimapState()
        {
            ClientStateTransition transition = ResolveToggleMiniMapStateTransitionForTesting(
                _currentOption,
                _previousExpandedOption,
                _btnSmall != null && _expandedFrame != null,
                _bIsCollapsedState,
                _useLegacyOptionButtonCycle);

            ApplyClientStateTransition(transition);
        }

        private void ApplyClientStateTransition(ClientStateTransition transition)
        {
            BaseDXDrawableItem previousFrame = GetVisibleFrame();

            _currentOption = transition.CurrentOption;
            _previousExpandedOption = transition.PreviousExpandedOption;
            _bIsCollapsedState = transition.IsCollapsed;

            SyncFramePositionsFrom(previousFrame);
            if (_useLegacyOptionButtonCycle)
            {
                _btnMin.SetButtonState(UIObjectState.Normal);
                _btnMax.SetButtonState(UIObjectState.Normal);
            }
            else
            {
                _btnMin.SetButtonState(_bIsCollapsedState ? UIObjectState.Disabled : UIObjectState.Normal);
                _btnMax.SetButtonState(_bIsCollapsedState ? UIObjectState.Normal : UIObjectState.Disabled);
            }

            UpdateButtonLayout();
        }

        internal static ClientStateTransition ResolveClientStateTransitionForTesting(
            int buttonId,
            int currentOption,
            int previousExpandedOption,
            bool supportsExpandedOption,
            bool useLegacyOptionButtonCycle = false)
        {
            if (useLegacyOptionButtonCycle)
            {
                int legacyNormalizedCurrentOption = NormalizeClientOptionForLegacyCycle(currentOption);
                int legacyNormalizedPreviousExpandedOption = NormalizeRememberedExpandedOption(previousExpandedOption);
                return buttonId switch
                {
                    ClientButtonIdMinimapState => new ClientStateTransition(
                        NormalizeClientOptionForLegacyCycle(legacyNormalizedCurrentOption + 1),
                        legacyNormalizedPreviousExpandedOption,
                        NormalizeClientOptionForLegacyCycle(legacyNormalizedCurrentOption + 1) == ClientOptionCollapsed),
                    ClientButtonIdMinimapRestore => new ClientStateTransition(
                        NormalizeClientOptionForLegacyCycle(legacyNormalizedCurrentOption - 1),
                        legacyNormalizedPreviousExpandedOption,
                        NormalizeClientOptionForLegacyCycle(legacyNormalizedCurrentOption - 1) == ClientOptionCollapsed),
                    _ => new ClientStateTransition(
                        legacyNormalizedCurrentOption,
                        legacyNormalizedPreviousExpandedOption,
                        legacyNormalizedCurrentOption == ClientOptionCollapsed)
                };
            }

            static int NormalizeClientOptionForState(int option, bool supportsExpanded)
            {
                if (!supportsExpanded)
                {
                    return ClientOptionCompact;
                }

                return option switch
                {
                    ClientOptionExpanded => ClientOptionExpanded,
                    ClientOptionCompact => ClientOptionCompact,
                    ClientOptionCollapsed => ClientOptionCollapsed,
                    _ => ClientOptionCompact
                };
            }

            int normalizedCurrentOption = NormalizeClientOptionForState(currentOption, supportsExpandedOption);
            int normalizedPreviousExpandedOption = NormalizeRememberedExpandedOption(previousExpandedOption);

            return buttonId switch
            {
                ClientButtonIdMinimapState => ResolveMinimapStateButtonTransition(
                    normalizedCurrentOption,
                    normalizedPreviousExpandedOption),
                ClientButtonIdMinimapRestore => normalizedCurrentOption == ClientOptionCollapsed
                    ? new ClientStateTransition(
                        NormalizeRememberedExpandedOption(normalizedPreviousExpandedOption),
                        normalizedPreviousExpandedOption,
                        false)
                    : new ClientStateTransition(
                        normalizedCurrentOption,
                        normalizedPreviousExpandedOption,
                        false),
                ClientButtonIdOption when supportsExpandedOption && normalizedCurrentOption != ClientOptionCollapsed => new ClientStateTransition(
                    normalizedCurrentOption == ClientOptionExpanded
                        ? ClientOptionCompact
                        : ClientOptionExpanded,
                    normalizedPreviousExpandedOption,
                    false),
                _ => new ClientStateTransition(
                    normalizedCurrentOption,
                    normalizedPreviousExpandedOption,
                    normalizedCurrentOption == ClientOptionCollapsed)
            };
        }

        private static int NormalizeClientOptionForLegacyCycle(int option)
        {
            int normalized = option % 3;
            return normalized < 0 ? normalized + 3 : normalized;
        }

        private static ClientStateTransition ResolveMinimapStateButtonTransition(
            int normalizedCurrentOption,
            int normalizedPreviousExpandedOption)
        {
            if (normalizedCurrentOption == ClientOptionCollapsed)
            {
                return new ClientStateTransition(
                    ClientOptionCollapsed,
                    normalizedPreviousExpandedOption,
                    true);
            }

            return new ClientStateTransition(
                ClientOptionCollapsed,
                NormalizeRememberedExpandedOption(normalizedCurrentOption),
                true);
        }

        internal static ClientStateTransition ResolveToggleMiniMapStateTransitionForTesting(
            int currentOption,
            int previousExpandedOption,
            bool supportsExpandedOption,
            bool isCollapsed,
            bool useLegacyOptionButtonCycle = false)
        {
            int buttonId = isCollapsed || currentOption == ClientOptionCollapsed
                ? ClientButtonIdMinimapRestore
                : ClientButtonIdMinimapState;

            return ResolveClientStateTransitionForTesting(
                buttonId,
                currentOption,
                previousExpandedOption,
                supportsExpandedOption,
                useLegacyOptionButtonCycle);
        }

        internal static ClientStateTransition ResolveEnsureExpandedTransitionForTesting(
            int currentOption,
            int previousExpandedOption,
            bool supportsExpandedOption)
        {
            static int NormalizeExpandedOptionForState(int option, bool supportsExpanded)
            {
                if (!supportsExpanded)
                {
                    return ClientOptionCompact;
                }

                return option == ClientOptionExpanded ? ClientOptionExpanded : ClientOptionCompact;
            }

            int normalizedCurrentOption = currentOption == ClientOptionCollapsed
                ? ClientOptionCollapsed
                : NormalizeExpandedOptionForState(currentOption, supportsExpandedOption);
            int normalizedPreviousExpandedOption = NormalizeRememberedExpandedOption(previousExpandedOption);

            if (normalizedCurrentOption == ClientOptionCollapsed)
            {
                return new ClientStateTransition(
                    NormalizeRememberedExpandedOption(normalizedPreviousExpandedOption),
                    normalizedPreviousExpandedOption,
                    false);
            }

            return new ClientStateTransition(
                normalizedCurrentOption,
                normalizedPreviousExpandedOption,
                false);
        }

        internal static ClientStateTransition ResolveEnsureCollapsedTransitionForTesting(
            int currentOption,
            int previousExpandedOption,
            bool supportsExpandedOption)
        {
            static int NormalizeExpandedOptionForState(int option, bool supportsExpanded)
            {
                if (!supportsExpanded)
                {
                    return ClientOptionCompact;
                }

                return option == ClientOptionExpanded ? ClientOptionExpanded : ClientOptionCompact;
            }

            int normalizedCurrentOption = currentOption == ClientOptionCollapsed
                ? ClientOptionCollapsed
                : NormalizeExpandedOptionForState(currentOption, supportsExpandedOption);
            int normalizedPreviousExpandedOption = NormalizeRememberedExpandedOption(previousExpandedOption);
            int rememberedExpandedOption = normalizedCurrentOption != ClientOptionCollapsed
                ? normalizedCurrentOption
                : normalizedPreviousExpandedOption;

            return new ClientStateTransition(
                ClientOptionCollapsed,
                rememberedExpandedOption,
                true);
        }

        internal static ClientButtonVisibility ResolveButtonVisibilityForTesting(
            int currentOption,
            bool isCollapsed,
            bool supportsExpandedOption,
            bool supportsNpcButton,
            bool useLegacyOptionButtonCycle = false)
        {
            if (useLegacyOptionButtonCycle)
            {
                return new ClientButtonVisibility(
                    MinVisible: true,
                    MaxVisible: true,
                    BigVisible: false,
                    SmallVisible: false,
                    NpcVisible: false);
            }

            int normalizedCurrentOption = currentOption switch
            {
                ClientOptionExpanded when supportsExpandedOption => ClientOptionExpanded,
                ClientOptionCompact => ClientOptionCompact,
                ClientOptionCollapsed => ClientOptionCollapsed,
                _ => ClientOptionCompact
            };

            if (isCollapsed || normalizedCurrentOption == ClientOptionCollapsed)
            {
                return new ClientButtonVisibility(
                    MinVisible: false,
                    MaxVisible: true,
                    BigVisible: false,
                    SmallVisible: false,
                    NpcVisible: false);
            }

            return new ClientButtonVisibility(
                MinVisible: true,
                MaxVisible: false,
                BigVisible: supportsExpandedOption && normalizedCurrentOption == ClientOptionCompact,
                SmallVisible: supportsExpandedOption && normalizedCurrentOption == ClientOptionExpanded,
                NpcVisible: supportsNpcButton);
        }

        internal static ClientButtonPlacement ResolveMapButtonPlacementForTesting(int frameWidth, int buttonWidth)
        {
            return new ClientButtonPlacement(
                ResolveTopRowButtonX(frameWidth, buttonWidth),
                ClientTopRowButtonTop,
                Visible: true);
        }

        internal static ClientButtonPlacement ResolveStateButtonPlacementForTesting(
            int frameWidth,
            int mapButtonWidth,
            int stateButtonWidth)
        {
            int mapButtonX = ResolveTopRowButtonX(frameWidth, mapButtonWidth);
            return new ClientButtonPlacement(
                ResolveAdjacentLeftButtonX(mapButtonX, stateButtonWidth),
                ClientTopRowButtonTop,
                Visible: true);
        }

        internal static ClientButtonPlacement ResolveOptionButtonPlacementForTesting(
            int frameWidth,
            int frameHeight,
            int optionButtonWidth,
            int optionButtonHeight,
            bool supportsExpandedOption)
        {
            if (!supportsExpandedOption || optionButtonWidth <= 0 || optionButtonHeight <= 0)
            {
                return new ClientButtonPlacement(0, 0, Visible: false);
            }

            return new ClientButtonPlacement(
                Math.Max(0, frameWidth - optionButtonWidth - ClientOptionButtonRightPadding),
                Math.Max(0, frameHeight - optionButtonHeight - ClientOptionButtonBottomPadding),
                Visible: true);
        }

        internal static Rectangle ResolveCollapsedHoverBoundsForTesting(Rectangle bounds, bool isCollapsed)
        {
            if (!isCollapsed || bounds.IsEmpty)
            {
                return bounds;
            }

            int horizontalInflation = bounds.Width / 2;
            int verticalInflation = bounds.Height / 2;
            return new Rectangle(
                bounds.X - horizontalInflation,
                bounds.Y - verticalInflation,
                bounds.Width + (horizontalInflation * 2),
                bounds.Height + (verticalInflation * 2));
        }

        private void DrawNpcMarkers(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_bIsCollapsedState || !_showNpcMarkers || ResolveAnyNpcMarker() == null || _npcMarkers.Count == 0)
                return;

            foreach (NpcItem npc in _npcMarkers)
            {
                if (npc?.NpcInstance == null || !npc.IsVisible)
                    continue;

                if (!IsClientNpcHoverCandidate(npc))
                    continue;

                Point minimapPoint = WorldToMinimap(npc.CurrentX, npc.CurrentY);
                BaseDXDrawableItem marker = ResolveNpcMarker(npc);
                if (marker == null)
                    continue;

                Rectangle hoverBounds = DrawMarkerWithDirectionOverlay(marker, minimapPoint, true, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, tickCount);
                Rectangle clientHoverBounds = GetClientMarkerHoverBounds(marker, minimapPoint);
                if (!clientHoverBounds.IsEmpty)
                {
                    AddHoverTarget(npc, clientHoverBounds);
                }
                else if (!hoverBounds.IsEmpty)
                {
                    AddHoverTarget(npc, hoverBounds);
                }
            }
        }

        private void DrawPortalMarkers(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_bIsCollapsedState || _portalMarker == null || _portalMarkers.Count == 0)
                return;

            for (int i = _portalMarkers.Count - 1; i >= 0; i--)
            {
                PortalItem portal = _portalMarkers[i];
                if (portal?.PortalInstance == null || !portal.IsVisible)
                    continue;

                Point minimapPoint = WorldToMinimap(portal.PortalInstance.X, portal.PortalInstance.Y);
                Rectangle hoverBounds = DrawMarkerWithDirectionOverlay(_portalMarker, minimapPoint, true, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, tickCount);
                if (!IsClientPortalHoverCandidate(portal))
                    continue;

                Rectangle clientHoverBounds = GetClientMarkerHoverBounds(_portalMarker, minimapPoint);
                if (!clientHoverBounds.IsEmpty)
                {
                    AddHoverTarget(portal, clientHoverBounds);
                }
            }
        }

        private Rectangle DrawDirectionOverlayForPoint(
            Point minimapPoint,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (IsWithinMinimapImage(minimapPoint))
                return Rectangle.Empty;

            DirectionArrow direction = ResolveDirectionArrow(minimapPoint);
            if (!_directionMarkers.TryGetValue(direction, out BaseDXDrawableItem arrow) || arrow == null)
                return Rectangle.Empty;

            Point drawPoint = ClampDirectionArrowPoint(minimapPoint);
            arrow.Draw(sprite, skeletonMeshRenderer, gameTime,
                -Position.X, -Position.Y, drawPoint.X, drawPoint.Y,
                drawReflectionInfo,
                renderParameters,
                tickCount);

            return GetMarkerScreenBounds(arrow, drawPoint);
        }

        private Point WorldToMinimap(int worldX, int worldY)
        {
            return new Point(
                (worldX - _minimapOriginX) / 16,
                (worldY - _minimapOriginY) / 16);
        }

        private BaseDXDrawableItem ResolveNpcMarker(NpcItem npc)
        {
            NpcMarkerType markerType = ResolveNpcMarkerType?.Invoke(npc) ?? NpcMarkerType.Default;
            return markerType switch
            {
                NpcMarkerType.QuestStart when _questStartNpcMarker != null => _questStartNpcMarker,
                NpcMarkerType.QuestEnd when _questEndNpcMarker != null => _questEndNpcMarker,
                _ => _npcMarker ?? _questStartNpcMarker ?? _questEndNpcMarker
            };
        }

        private BaseDXDrawableItem ResolveAnyNpcMarker()
        {
            return _npcMarker ?? _questStartNpcMarker ?? _questEndNpcMarker;
        }

        private BaseDXDrawableItem ResolveEmployeeMarker(EmployeeMarker employee)
        {
            if (employee?.PreferredMarkerType is HelperMarkerType preferredMarkerType)
            {
                BaseDXDrawableItem helperMarker = ResolveHelperMarker(preferredMarkerType);
                if (helperMarker != null)
                {
                    return helperMarker;
                }
            }

            return ResolveAnyNpcMarker();
        }

        private BaseDXDrawableItem ResolveLocalPlayerMarker()
        {
            if (_localPlayerHelperMarkerType.HasValue
                && ResolveHelperMarker(_localPlayerHelperMarkerType.Value) is BaseDXDrawableItem helperMarker
                && helperMarker != null)
            {
                return helperMarker;
            }

            return _userMarker;
        }

        private bool ShouldSuppressLegacyPixelDot(BaseDXDrawableItem playerMarker)
        {
            return playerMarker != null && _localPlayerHelperMarkerType.HasValue;
        }

        private void DrawTrackedUserMarkers(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_bIsCollapsedState || _helperMarkers.Count == 0 || _trackedUserMarkerCount == 0)
                return;

            for (int i = 0; i < _trackedUserMarkerCount; i++)
            {
                TrackedUserMarker trackedUser = _trackedUserMarkers[i];
                if (trackedUser == null)
                    continue;

                Point minimapPoint = WorldToMinimap((int)trackedUser.WorldX, (int)trackedUser.WorldY);
                BaseDXDrawableItem marker = ResolveHelperMarker(trackedUser.MarkerType);
                if (marker == null)
                    continue;

                Rectangle hoverBounds = DrawMarkerWithDirectionOverlay(marker, minimapPoint, trackedUser.ShowDirectionOverlay, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, tickCount);
                bool isWithinMinimapImage = IsWithinMinimapImage(minimapPoint);
                if (ShouldRegisterTrackedUserHoverTargetForTesting(
                        isWithinMinimapImage,
                        trackedUser.ShowDirectionOverlay,
                        trackedUser.TooltipText,
                        hoverBounds))
                {
                    AddHoverTarget(trackedUser.TooltipText, hoverBounds, ClientHoverTargetKind.RemoteDirection);
                }
            }
        }

        internal static bool ShouldRegisterTrackedUserHoverTargetForTesting(
            bool isWithinMinimapImage,
            bool showDirectionOverlay,
            string tooltipText,
            Rectangle hoverBounds)
        {
            return (isWithinMinimapImage || showDirectionOverlay)
                && !hoverBounds.IsEmpty
                && !string.IsNullOrWhiteSpace(tooltipText);
        }

        private BaseDXDrawableItem ResolveHelperMarker(HelperMarkerType markerType)
        {
            if (_helperMarkers.TryGetValue(markerType, out BaseDXDrawableItem helperMarker) && helperMarker != null)
            {
                return helperMarker;
            }

            return markerType switch
            {
                HelperMarkerType.User => ResolveHelperMarker(HelperMarkerType.Another),
                HelperMarkerType.GuildMaster => ResolveHelperMarker(HelperMarkerType.Guild),
                HelperMarkerType.PartyMaster => ResolveHelperMarker(HelperMarkerType.Party),
                HelperMarkerType.UserTrader => ResolveHelperMarker(HelperMarkerType.AnotherTrader) ?? ResolveHelperMarker(HelperMarkerType.Another),
                HelperMarkerType.AnotherTrader => ResolveHelperMarker(HelperMarkerType.Another),
                HelperMarkerType.Match => ResolveHelperMarker(HelperMarkerType.Another),
                HelperMarkerType.Friend => ResolveHelperMarker(HelperMarkerType.Another),
                HelperMarkerType.Guild => ResolveHelperMarker(HelperMarkerType.Another),
                HelperMarkerType.Party => ResolveHelperMarker(HelperMarkerType.Another),
                _ => null
            };
        }

        private void DrawEmployeeMarkers(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_bIsCollapsedState || _employeeMarkerCount == 0)
                return;

            for (int i = 0; i < _employeeMarkerCount; i++)
            {
                EmployeeMarker employee = _employeeMarkers[i];
                if (employee == null)
                    continue;

                BaseDXDrawableItem marker = ResolveEmployeeMarker(employee);
                if (marker == null)
                    continue;

                Point minimapPoint = WorldToMinimap((int)employee.WorldX, (int)employee.WorldY);
                Rectangle hoverBounds = DrawMarkerWithDirectionOverlay(marker, minimapPoint, employee.ShowDirectionOverlay, sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, tickCount);
                Rectangle clientHoverBounds = GetClientMarkerHoverBounds(marker, minimapPoint);
                if (!clientHoverBounds.IsEmpty && !string.IsNullOrWhiteSpace(employee.TooltipText))
                {
                    AddHoverTarget(employee.TooltipText, clientHoverBounds, ClientHoverTargetKind.Employee);
                }
                else if (!hoverBounds.IsEmpty && !string.IsNullOrWhiteSpace(employee.TooltipText))
                {
                    AddHoverTarget(employee.TooltipText, hoverBounds, ClientHoverTargetKind.Employee);
                }
            }
        }

        private Rectangle DrawMarkerWithDirectionOverlay(
            BaseDXDrawableItem marker,
            Point minimapPoint,
            bool showDirectionOverlay,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (marker == null)
            {
                return Rectangle.Empty;
            }

            if (IsWithinMinimapImage(minimapPoint))
            {
                marker.Draw(sprite, skeletonMeshRenderer, gameTime,
                    -Position.X, -Position.Y, minimapPoint.X, minimapPoint.Y,
                    drawReflectionInfo,
                    renderParameters,
                    tickCount);
                return GetMarkerScreenBounds(marker, minimapPoint);
            }

            if (showDirectionOverlay)
            {
                return DrawDirectionOverlayForPoint(
                    minimapPoint,
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    drawReflectionInfo,
                    renderParameters,
                    tickCount);
            }

            return Rectangle.Empty;
        }

        private void DrawNpcListPanel(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int centerX,
            int centerY,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_bIsCollapsedState || !_showNpcMarkers || _npcListPanel == null)
                return;

            int drawRelativeX = -Position.X - _npcListPanel.Position.X;
            int drawRelativeY = -Position.Y - _npcListPanel.Position.Y;

            _npcListPanel.Draw(sprite, skeletonMeshRenderer, gameTime,
                drawRelativeX,
                drawRelativeY,
                centerX,
                centerY,
                null,
                renderParameters,
                tickCount);
        }

        private bool IsWithinMinimapImage(Point minimapPoint)
        {
            return minimapPoint.X >= 0 &&
                   minimapPoint.Y >= 0 &&
                   minimapPoint.X < _minimapImageWidth &&
                   minimapPoint.Y < _minimapImageHeight;
        }

        private DirectionArrow ResolveDirectionArrow(Point minimapPoint)
        {
            bool isLeft = minimapPoint.X < 0;
            bool isRight = minimapPoint.X >= _minimapImageWidth;
            bool isAbove = minimapPoint.Y < 0;
            bool isBelow = minimapPoint.Y >= _minimapImageHeight;

            if (isAbove && isLeft)
                return DirectionArrow.NorthWest;
            if (isAbove && isRight)
                return DirectionArrow.NorthEast;
            if (isBelow && isLeft)
                return DirectionArrow.SouthWest;
            if (isBelow && isRight)
                return DirectionArrow.SouthEast;
            if (isAbove)
                return DirectionArrow.North;
            if (isBelow)
                return DirectionArrow.South;
            if (isLeft)
                return DirectionArrow.West;
            return DirectionArrow.East;
        }

        private Point ClampDirectionArrowPoint(Point minimapPoint)
        {
            const int padding = 4;
            int x = Math.Clamp(minimapPoint.X, padding, Math.Max(padding, _minimapImageWidth - padding));
            int y = Math.Clamp(minimapPoint.Y, padding, Math.Max(padding, _minimapImageHeight - padding));
            return new Point(x, y);
        }

        private bool TrySetHoveredMarkerTooltip(MouseState mouseState)
        {
            if (_bIsCollapsedState || !ContainsPoint(mouseState.X, mouseState.Y))
            {
                return false;
            }

            HoverTargetEntry selectedHoverTarget = null;
            string selectedTooltipText = null;
            for (int i = 0; i < _hoverTargetCount; i++)
            {
                HoverTargetEntry hoverTarget = _hoverTargets[i];
                if (hoverTarget.Bounds.IsEmpty || !hoverTarget.Bounds.Contains(mouseState.X, mouseState.Y))
                {
                    continue;
                }

                string tooltipText = ResolveHoverTargetTooltipText(hoverTarget);
                if (string.IsNullOrWhiteSpace(tooltipText))
                {
                    continue;
                }

                if (selectedHoverTarget == null
                    || IsClientHoverTargetKindPreferredForTesting(hoverTarget.Kind, selectedHoverTarget.Kind))
                {
                    selectedHoverTarget = hoverTarget;
                    selectedTooltipText = tooltipText;
                }
            }

            if (selectedHoverTarget != null)
            {
                int relativeMouseX = mouseState.X - Position.X;
                int relativeMouseY = mouseState.Y - Position.Y;
                SetHoveredTooltip(
                    selectedTooltipText,
                    ResolveTooltipAnchorPointForTesting(Position.X, Position.Y, relativeMouseX, relativeMouseY),
                    selectedHoverTarget.Kind);
                return true;
            }

            return false;
        }

        private Rectangle GetMarkerScreenBounds(BaseDXDrawableItem marker, Point minimapPoint)
        {
            if (marker == null)
            {
                return Rectangle.Empty;
            }

            bool withinMinimap = IsWithinMinimapImage(minimapPoint);
            if (!withinMinimap && !_directionMarkerSet.Contains(marker))
            {
                return Rectangle.Empty;
            }

            Point drawPoint = withinMinimap ? minimapPoint : ClampDirectionArrowPoint(minimapPoint);
            IDXObject frame = marker.LastFrameDrawn ?? marker.Frame0;
            if (frame == null)
            {
                return Rectangle.Empty;
            }

            Rectangle rect = new Rectangle(
                Position.X + marker.Position.X + drawPoint.X + frame.X,
                Position.Y + marker.Position.Y + drawPoint.Y + frame.Y,
                frame.Width,
                frame.Height);

            return ResolveCollapsedHoverBoundsForTesting(rect, _bIsCollapsedState);
        }

        private Rectangle GetClientMarkerHoverBounds(BaseDXDrawableItem marker, Point minimapPoint)
        {
            if (marker == null)
            {
                return Rectangle.Empty;
            }

            return ResolveClientVisibleMarkerHoverBoundsForTesting(
                IsWithinMinimapImage(minimapPoint),
                Position.X + marker.Position.X + minimapPoint.X,
                Position.Y + marker.Position.Y + minimapPoint.Y);
        }

        internal static Rectangle ResolveClientVisibleMarkerHoverBoundsForTesting(
            bool isWithinMinimapImage,
            int markerScreenX,
            int markerScreenY)
        {
            return isWithinMinimapImage
                ? ResolveClientMarkerHoverBoundsForTesting(markerScreenX, markerScreenY)
                : Rectangle.Empty;
        }

        private static bool IsClientNpcHoverCandidate(NpcItem npc)
        {
            return npc?.NpcInstance != null
                && IsClientNpcHoverCandidateForTesting(npc.NpcInstance.NpcInfo?.HideName == true);
        }

        private void ResetHoverTargets()
        {
            _hoverTargetCount = 0;
        }

        private void AddHoverTarget(string tooltipText, Rectangle bounds, ClientHoverTargetKind kind)
        {
            if (string.IsNullOrWhiteSpace(tooltipText) || bounds.IsEmpty)
            {
                return;
            }

            HoverTargetEntry hoverTarget = GetOrCreateHoverTarget(_hoverTargetCount++);
            hoverTarget.Bounds = bounds;
            hoverTarget.TooltipText = tooltipText;
            hoverTarget.Npc = null;
            hoverTarget.Portal = null;
            hoverTarget.Kind = kind;
        }

        private void AddHoverTarget(NpcItem npc, Rectangle bounds)
        {
            if (npc == null || bounds.IsEmpty)
            {
                return;
            }

            HoverTargetEntry hoverTarget = GetOrCreateHoverTarget(_hoverTargetCount++);
            hoverTarget.Bounds = bounds;
            hoverTarget.TooltipText = null;
            hoverTarget.Npc = npc;
            hoverTarget.Portal = null;
            hoverTarget.Kind = ClientHoverTargetKind.Npc;
        }

        private void AddHoverTarget(PortalItem portal, Rectangle bounds)
        {
            if (portal == null || bounds.IsEmpty)
            {
                return;
            }

            HoverTargetEntry hoverTarget = GetOrCreateHoverTarget(_hoverTargetCount++);
            hoverTarget.Bounds = bounds;
            hoverTarget.TooltipText = null;
            hoverTarget.Npc = null;
            hoverTarget.Portal = portal;
            hoverTarget.Kind = ClientHoverTargetKind.Portal;
        }

        private HoverTargetEntry GetOrCreateHoverTarget(int index)
        {
            while (_hoverTargets.Count <= index)
            {
                _hoverTargets.Add(new HoverTargetEntry());
            }

            return _hoverTargets[index];
        }

        private string ResolveHoverTargetTooltipText(HoverTargetEntry hoverTarget)
        {
            if (!string.IsNullOrWhiteSpace(hoverTarget.TooltipText))
            {
                return hoverTarget.TooltipText;
            }

            if (hoverTarget.Npc != null)
            {
                return ResolveNpcTooltipText?.Invoke(hoverTarget.Npc);
            }

            if (hoverTarget.Portal != null)
            {
                return ResolvePortalTooltipText?.Invoke(hoverTarget.Portal);
            }

            return null;
        }

        internal static Rectangle ResolveClientMarkerHoverBoundsForTesting(int markerScreenX, int markerScreenY)
        {
            return new Rectangle(
                markerScreenX + ClientMarkerHoverBoundsXOffset,
                markerScreenY + ClientMarkerHoverBoundsYOffset,
                ClientMarkerHoverBoundsWidth,
                ClientMarkerHoverBoundsHeight);
        }

        internal static bool IsClientNpcHoverCandidateForTesting(bool templateHidesName)
        {
            return !templateHidesName;
        }

        private static bool IsClientPortalHoverCandidate(PortalItem portal)
        {
            if (portal?.PortalInstance == null)
            {
                return false;
            }

            return IsClientPortalHoverCandidateForTesting(
                portal.PortalInstance.pt,
                portal.PortalInstance.hideTooltip,
                portal.PortalInstance.tm);
        }

        internal static bool IsClientPortalHoverCandidateForTesting(
            PortalType portalType,
            MapleBool hideTooltip,
            int targetMapId)
        {
            return IsClientMinimapTooltipPortalType(portalType)
                && hideTooltip != MapleBool.True
                && targetMapId != -1;
        }

        internal static bool IsClientMinimapTooltipPortalType(PortalType portalType)
        {
            return portalType == PortalType.Visible
                || portalType == PortalType.TownPortalPoint;
        }

        internal static Point ResolveTooltipAnchorPointForTesting(int windowX, int windowY, int relativeMouseX, int relativeMouseY)
        {
            return new Point(
                windowX + relativeMouseX + ClientTooltipMouseOffset,
                windowY + relativeMouseY + ClientTooltipMouseOffset);
        }

        internal static Point ResolveTooltipAnchorPointForTesting(int mouseX, int mouseY)
        {
            return ResolveTooltipAnchorPointForTesting(0, 0, mouseX, mouseY);
        }

        internal static bool IsClientHoverTargetKindPreferredForTesting(
            ClientHoverTargetKind candidate,
            ClientHoverTargetKind current)
        {
            return (int)candidate < (int)current;
        }

        internal static string NormalizeTooltipTextForDisplayForTesting(string tooltipText, ClientHoverTargetKind kind)
        {
            if (string.IsNullOrWhiteSpace(tooltipText))
            {
                return null;
            }

            string trimmed = tooltipText.Trim();
            if (kind is ClientHoverTargetKind.RemoteDirection or ClientHoverTargetKind.TrackedUser)
            {
                return trimmed;
            }

            // Client OnMouseMove routes NPC/employee/portal through SetToolTip_String (single-line path).
            StringBuilder singleLine = new(trimmed.Length);
            bool previousWasWhitespace = false;
            for (int i = 0; i < trimmed.Length; i++)
            {
                char current = trimmed[i];
                bool normalizeWhitespace = current == '\r' || current == '\n' || current == '\t' || current == ' ';
                if (normalizeWhitespace)
                {
                    if (!previousWasWhitespace)
                    {
                        singleLine.Append(' ');
                        previousWasWhitespace = true;
                    }

                    continue;
                }

                singleLine.Append(current);
                previousWasWhitespace = false;
            }

            return singleLine.ToString().Trim();
        }

        private void SetHoveredTooltip(string tooltipText, Point anchorPoint, ClientHoverTargetKind kind)
        {
            string normalizedText = NormalizeTooltipTextForDisplayForTesting(tooltipText, kind);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return;
            }

            if (!string.Equals(_hoverTooltipText, normalizedText, StringComparison.Ordinal))
            {
                _hoverTooltipText = normalizedText;
                RefreshHoveredTooltipLayout();
            }

            _hoverTooltipAnchorPoint = anchorPoint;
        }

        private void RefreshHoveredTooltipLayout()
        {
            _hoverTooltipLines.Clear();
            _hoverTooltipMaxWidth = 0f;
            _hoverTooltipVisibleLineCount = 0;

            if (_tooltipFont == null || string.IsNullOrWhiteSpace(_hoverTooltipText))
            {
                return;
            }

            string[] lines = _hoverTooltipText
                .Replace("\r\n", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                _hoverTooltipLines.Add(line);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _hoverTooltipVisibleLineCount++;
                }
            }
        }

        private void DrawHoveredTooltip(SpriteBatch sprite, int renderWidth, int renderHeight)
        {
            if (_tooltipFont == null
                || _tooltipPixelTexture == null
                || string.IsNullOrWhiteSpace(_hoverTooltipText)
                || !_hoverTooltipAnchorPoint.HasValue
                || _hoverTooltipVisibleLineCount == 0)
            {
                return;
            }

            Point tooltipAnchorPoint = _hoverTooltipAnchorPoint.Value;
            int lineHeight = ResolveTooltipLineHeight(sprite?.GraphicsDevice);
            _hoverTooltipMaxWidth = ResolveTooltipMaxWidth(sprite?.GraphicsDevice);
            int tooltipWidth = (int)Math.Ceiling(_hoverTooltipMaxWidth) + (TOOLTIP_PADDING * 2);
            int tooltipHeight = (_hoverTooltipVisibleLineCount * lineHeight) + ((_hoverTooltipVisibleLineCount - 1) * TOOLTIP_LINE_GAP) + (TOOLTIP_PADDING * 2);
            int tooltipX = tooltipAnchorPoint.X;
            int tooltipY = tooltipAnchorPoint.Y;

            if (tooltipX + tooltipWidth > renderWidth - TOOLTIP_MARGIN)
            {
                tooltipX = Math.Max(TOOLTIP_MARGIN, renderWidth - tooltipWidth - TOOLTIP_MARGIN);
            }

            if (tooltipX < TOOLTIP_MARGIN)
            {
                tooltipX = TOOLTIP_MARGIN;
            }

            tooltipY = Math.Clamp(tooltipY, TOOLTIP_MARGIN, Math.Max(TOOLTIP_MARGIN, renderHeight - tooltipHeight - TOOLTIP_MARGIN));
            Rectangle tooltipRect = new Rectangle(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

            sprite.Draw(_tooltipPixelTexture, tooltipRect, new Color(18, 18, 26, 235));
            DrawTooltipBorder(sprite, tooltipRect);

            float drawY = tooltipRect.Y + TOOLTIP_PADDING;
            for (int i = 0; i < _hoverTooltipLines.Count; i++)
            {
                string line = _hoverTooltipLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                Vector2 textPosition = new Vector2(tooltipRect.X + TOOLTIP_PADDING, drawY);
                ClientTextDrawing.DrawShadowed(sprite, line, textPosition, Color.White, _tooltipFont);
                drawY += lineHeight + TOOLTIP_LINE_GAP;
            }
        }

        private int ResolveTooltipLineHeight(GraphicsDevice graphicsDevice)
        {
            Vector2 measured = ClientTextDrawing.Measure(graphicsDevice, "Ag", fallbackFont: _tooltipFont);
            int measuredHeight = (int)Math.Ceiling(measured.Y);
            return Math.Max(1, measuredHeight > 0 ? measuredHeight : _tooltipFont.LineSpacing);
        }

        private float ResolveTooltipMaxWidth(GraphicsDevice graphicsDevice)
        {
            float maxWidth = 0f;
            for (int i = 0; i < _hoverTooltipLines.Count; i++)
            {
                string line = _hoverTooltipLines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                maxWidth = Math.Max(
                    maxWidth,
                    ClientTextDrawing.Measure(graphicsDevice, line, fallbackFont: _tooltipFont).X);
            }

            return maxWidth;
        }

        private void DrawTooltipBorder(SpriteBatch sprite, Rectangle rect)
        {
            Color borderColor = new Color(214, 174, 82);
            sprite.Draw(_tooltipPixelTexture, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 1), borderColor);
            sprite.Draw(_tooltipPixelTexture, new Rectangle(rect.X - 1, rect.Bottom, rect.Width + 2, 1), borderColor);
            sprite.Draw(_tooltipPixelTexture, new Rectangle(rect.X - 1, rect.Y, 1, rect.Height), borderColor);
            sprite.Draw(_tooltipPixelTexture, new Rectangle(rect.Right, rect.Y, 1, rect.Height), borderColor);
        }
        #endregion
    }
}
