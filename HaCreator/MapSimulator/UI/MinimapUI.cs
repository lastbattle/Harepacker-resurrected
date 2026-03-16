using HaCreator.MapSimulator.Entities;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// Mini map window item
    /// </summary>
    public class MinimapUI : BaseDXDrawableItem, IUIObjectEvents
    {
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

        private readonly BaseDXDrawableItem _pixelDot;
        private readonly BaseDXDrawableItem _collapsedFrame;
        private readonly BaseDXDrawableItem _userMarker;
        private readonly BaseDXDrawableItem _npcMarker;
        private readonly BaseDXDrawableItem _questStartNpcMarker;
        private readonly BaseDXDrawableItem _questEndNpcMarker;
        private readonly BaseDXDrawableItem _portalMarker;
        private readonly BaseDXDrawableItem _npcListPanel;
        private readonly IReadOnlyDictionary<DirectionArrow, BaseDXDrawableItem> _directionMarkers;
        private readonly List<UIObject> uiButtons = new List<UIObject>();

        private UIObject _btnMin;
        private UIObject _btnMax;
        private UIObject _btnBig;
        private UIObject _btnMap;
        private UIObject _btnNpc;

        private bool _bIsCollapsedState = false; // minimised minimap state
        private readonly int _minimapImageWidth;
        private readonly int _minimapImageHeight;
        private IReadOnlyList<NpcItem> _npcMarkers = Array.Empty<NpcItem>();
        private IReadOnlyList<PortalItem> _portalMarkers = Array.Empty<PortalItem>();
        private bool _showNpcMarkers;

        private int _lastMinimapToggleTime = 0;
        private const int MINIMAP_TOGGLE_COOLDOWN_MS = 200; // Cooldown in milliseconds

        // Player position on minimap (in minimap coordinates, not world coordinates)
        private int _playerMinimapX = 0;
        private int _playerMinimapY = 0;
        private int _minimapOriginX = 0;
        private int _minimapOriginY = 0;

        public Action FullMapRequested { get; set; }
        public Action MapTransferRequested { get; set; }
        public Func<NpcItem, NpcMarkerType> ResolveNpcMarkerType { get; set; }

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
            BaseDXDrawableItem _collapsedFrame,
            int minimapImageWidth,
            int minimapImageHeight,
            BaseDXDrawableItem userMarker = null,
            BaseDXDrawableItem npcMarker = null,
            BaseDXDrawableItem questStartNpcMarker = null,
            BaseDXDrawableItem questEndNpcMarker = null,
            BaseDXDrawableItem npcListPanel = null,
            BaseDXDrawableItem portalMarker = null,
            IReadOnlyDictionary<DirectionArrow, BaseDXDrawableItem> directionMarkers = null)
            : base(frame, false)
        {
            this._pixelDot = _pixelDot;
            this._collapsedFrame = _collapsedFrame;
            _minimapImageWidth = minimapImageWidth;
            _minimapImageHeight = minimapImageHeight;
            _userMarker = userMarker;
            _npcMarker = npcMarker;
            _questStartNpcMarker = questStartNpcMarker;
            _questEndNpcMarker = questEndNpcMarker;
            _npcListPanel = npcListPanel;
            _portalMarker = portalMarker;
            _directionMarkers = directionMarkers ?? new Dictionary<DirectionArrow, BaseDXDrawableItem>();
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        public void InitializeMinimapButtons(
            UIObject _btnMin,
            UIObject _btnMax,
            UIObject _btnBig,
            UIObject _btnMap,
            UIObject _btnNpc = null)
        {
            this._btnMin = _btnMin;
            this._btnMax = _btnMax;
            if (_btnBig != null)
                this._btnBig = _btnBig;
            this._btnMap = _btnMap;
            this._btnNpc = _btnNpc;

            uiButtons.Add(_btnMin);
            uiButtons.Add(_btnMax);
            if (_btnBig != null)
                uiButtons.Add(_btnBig);
            if (_btnNpc != null)
                uiButtons.Add(_btnNpc);
            uiButtons.Add(_btnMap);

            _btnMax.SetButtonState(UIObjectState.Disabled); // start maximised

            _btnMin.ButtonClickReleased += ObjUIBtMin_ButtonClickReleased;
            _btnMax.ButtonClickReleased += ObjUIBtMax_ButtonClickReleased;
            if (_btnBig != null)
                _btnBig.ButtonClickReleased += ObjUIBtBig_ButtonClickReleased;
            if (_btnNpc != null)
                _btnNpc.ButtonClickReleased += ObjUIBtNpc_ButtonClickReleased;
            _btnMap.ButtonClickReleased += ObjUIBtMap_ButtonClickReleased;
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
        }

        public void SetPortalMarkers(IReadOnlyList<PortalItem> portalMarkers)
        {
            _portalMarkers = portalMarkers ?? Array.Empty<PortalItem>();
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
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
                base.Draw(sprite, skeletonMeshRenderer, gameTime,
                   0, 0, centerX, centerY,
                   drawReflectionInfo,
                   renderParameters,
                   TickCount);

                // Use stored player position (set via SetPlayerPosition) for accurate dot placement
                // This ensures the dot follows the actual character position, not the viewport center
                int minimapPosX = _playerMinimapX;
                int minimapPosY = _playerMinimapY;

                if (_userMarker != null)
                {
                    _userMarker.Draw(sprite, skeletonMeshRenderer, gameTime,
                        -Position.X, -Position.Y, minimapPosX, minimapPosY,
                        drawReflectionInfo,
                        renderParameters,
                        TickCount);
                }

                // Draw the minimap pixel dot
                _pixelDot.Draw(sprite, skeletonMeshRenderer, gameTime,
                    -Position.X, -Position.Y, minimapPosX, minimapPosY,
                    drawReflectionInfo,
                    renderParameters,
                    TickCount);

                DrawPortalMarkers(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
                DrawNpcMarkers(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
                DrawNpcListPanel(sprite, skeletonMeshRenderer, gameTime, centerX, centerY, renderParameters, TickCount);
                DrawDirectionOverlays(sprite, skeletonMeshRenderer, gameTime, drawReflectionInfo, renderParameters, TickCount);
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

                if (!this._bIsCollapsedState)
                {
                    ObjUIBtMin_ButtonClickReleased(null);
                }
                else
                {
                    ObjUIBtMax_ButtonClickReleased(null);
                }
            }
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
            int frameWidth = _bIsCollapsedState ? _collapsedFrame.LastFrameDrawn?.Width ?? 100 : this.LastFrameDrawn?.Width ?? 100;
            int frameHeight = _bIsCollapsedState ? _collapsedFrame.LastFrameDrawn?.Height ?? 100 : this.LastFrameDrawn?.Height ?? 100;

            Rectangle rect = new Rectangle(
                this.Position.X,
                this.Position.Y,
                frameWidth,
                frameHeight);

            return rect.Contains(x, y);
        }

        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight)
        {
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

            // handle UI movement
            if (mouseState.LeftButton == ButtonState.Pressed)
            {
                // if drag has not started, initialize the offset
                if (mouseOffsetOnDragStart == null)
                {
                    Rectangle rect = new Rectangle(
                        this.Position.X,
                        this.Position.Y,
                        this.LastFrameDrawn.Width, this.LastFrameDrawn.Height);
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
                int frameWidth = _bIsCollapsedState ? _collapsedFrame.LastFrameDrawn.Width : this.LastFrameDrawn.Width;
                int frameHeight = _bIsCollapsedState ? _collapsedFrame.LastFrameDrawn.Height : this.LastFrameDrawn.Height;

                // Enforce screen boundary constraints
                newX = Math.Max(0, Math.Min(newX, renderWidth - frameWidth));
                newY = Math.Max(0, Math.Min(newY, renderHeight - frameHeight));

                this.Position = new Point(newX, newY);
                if (_bIsCollapsedState)
                {
                    this._collapsedFrame.Position = new Point(newX, newY);
                }
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
            _btnMin.SetButtonState(UIObjectState.Disabled);
            _btnMax.SetButtonState(UIObjectState.Normal);

            _btnMap.X = this._collapsedFrame.Frame0.Width - _btnMap.CanvasSnapshotWidth - 8;
            if (_btnBig != null)
            {
                _btnBig.X = _btnMap.X - _btnBig.CanvasSnapshotWidth;
                if (_btnNpc != null)
                {
                    _btnNpc.X = _btnBig.X - _btnNpc.CanvasSnapshotWidth;
                    _btnMax.X = _btnNpc.X - _btnMax.CanvasSnapshotWidth;
                }
                else
                {
                    _btnMax.X = _btnBig.X - _btnMax.CanvasSnapshotWidth;
                }
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth;
            }
            else
            {
                _btnMax.X = _btnMap.X - _btnMax.CanvasSnapshotWidth;
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth;
            }

            BaseDXDrawableItem baseItem = (BaseDXDrawableItem)this;
            _collapsedFrame.CopyObjectPosition(baseItem);

            this._bIsCollapsedState = true;
        }

        /// <summary>
        /// On 'BtMax' clicked.
        /// Map maximised mode
        /// </summary>
        private void ObjUIBtMax_ButtonClickReleased(UIObject sender)
        {
            _btnMin.SetButtonState(UIObjectState.Normal);
            _btnMax.SetButtonState(UIObjectState.Disabled);

            if (_btnBig != null)
            {
                _btnMap.X = this.Frame0.Width - _btnMap.CanvasSnapshotWidth - 8;
                _btnBig.X = _btnMap.X - _btnBig.CanvasSnapshotWidth;
                if (_btnNpc != null)
                {
                    _btnNpc.X = _btnBig.X - _btnNpc.CanvasSnapshotWidth;
                    _btnMax.X = _btnNpc.X - _btnMax.CanvasSnapshotWidth;
                }
                else
                {
                    _btnMax.X = _btnBig.X - _btnMax.CanvasSnapshotWidth;
                }
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth;
            }
            else
            {
                _btnMap.X = this.Frame0.Width - _btnMap.CanvasSnapshotWidth - 8;
                _btnMax.X = _btnMap.X - _btnMax.CanvasSnapshotWidth;
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth;
            }
            this.CopyObjectPosition(_collapsedFrame);

            this._bIsCollapsedState = false;
        }

        /// <summary>
        /// On 'BtBig' clicked
        /// </summary>
        private void ObjUIBtBig_ButtonClickReleased(UIObject sender)
        {
            FullMapRequested?.Invoke();
        }

        /// <summary>
        /// On 'BtNpc' clicked
        /// Toggle NPC marker visibility on the minimap.
        /// </summary>
        private void ObjUIBtNpc_ButtonClickReleased(UIObject sender)
        {
            if (_btnNpc == null || _npcMarker == null || _npcMarkers.Count == 0)
                return;

            _showNpcMarkers = !_showNpcMarkers;
            _btnNpc.SetButtonState(_showNpcMarkers ? UIObjectState.Disabled : UIObjectState.Normal);
        }

        /// <summary>
        /// On 'BtMap' clicked
        /// </summary>
        private void ObjUIBtMap_ButtonClickReleased(UIObject sender)
        {
            MapTransferRequested?.Invoke();
        }

        private void DrawNpcMarkers(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_bIsCollapsedState || !_showNpcMarkers || _npcMarker == null || _npcMarkers.Count == 0)
                return;

            foreach (NpcItem npc in _npcMarkers)
            {
                if (npc?.NpcInstance == null || !npc.IsVisible)
                    continue;

                Point minimapPoint = WorldToMinimap(npc.CurrentX, npc.CurrentY);
                if (!IsWithinMinimapImage(minimapPoint))
                    continue;

                BaseDXDrawableItem marker = ResolveNpcMarker(npc);
                if (marker == null)
                    continue;

                marker.Draw(sprite, skeletonMeshRenderer, gameTime,
                    -Position.X, -Position.Y, minimapPoint.X, minimapPoint.Y,
                    drawReflectionInfo,
                    renderParameters,
                    tickCount);
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

            foreach (PortalItem portal in _portalMarkers)
            {
                if (portal?.PortalInstance == null || !portal.IsVisible)
                    continue;

                Point minimapPoint = WorldToMinimap(portal.PortalInstance.X, portal.PortalInstance.Y);
                if (!IsWithinMinimapImage(minimapPoint))
                    continue;

                _portalMarker.Draw(sprite, skeletonMeshRenderer, gameTime,
                    -Position.X, -Position.Y, minimapPoint.X, minimapPoint.Y,
                    drawReflectionInfo,
                    renderParameters,
                    tickCount);
            }
        }

        private void DrawDirectionOverlays(
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (_bIsCollapsedState || _directionMarkers.Count == 0)
                return;

            foreach (PortalItem portal in _portalMarkers)
            {
                if (portal?.PortalInstance == null || !portal.IsVisible)
                    continue;

                DrawDirectionOverlayForPoint(
                    WorldToMinimap(portal.PortalInstance.X, portal.PortalInstance.Y),
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    drawReflectionInfo,
                    renderParameters,
                    tickCount);
            }

            if (!_showNpcMarkers)
                return;

            foreach (NpcItem npc in _npcMarkers)
            {
                if (npc?.NpcInstance == null || !npc.IsVisible)
                    continue;

                DrawDirectionOverlayForPoint(
                    WorldToMinimap(npc.CurrentX, npc.CurrentY),
                    sprite,
                    skeletonMeshRenderer,
                    gameTime,
                    drawReflectionInfo,
                    renderParameters,
                    tickCount);
            }
        }

        private void DrawDirectionOverlayForPoint(
            Point minimapPoint,
            SpriteBatch sprite,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int tickCount)
        {
            if (IsWithinMinimapImage(minimapPoint))
                return;

            DirectionArrow direction = ResolveDirectionArrow(minimapPoint);
            if (!_directionMarkers.TryGetValue(direction, out BaseDXDrawableItem arrow) || arrow == null)
                return;

            Point drawPoint = ClampDirectionArrowPoint(minimapPoint);
            arrow.Draw(sprite, skeletonMeshRenderer, gameTime,
                -Position.X, -Position.Y, drawPoint.X, drawPoint.Y,
                drawReflectionInfo,
                renderParameters,
                tickCount);
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
        #endregion
    }
}
