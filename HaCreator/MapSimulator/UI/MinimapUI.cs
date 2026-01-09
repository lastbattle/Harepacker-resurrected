using HaCreator.MapSimulator.UI;
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
        private readonly BaseDXDrawableItem _pixelDot;
        private readonly List<UIObject> uiButtons = new List<UIObject>();

        private UIObject _btnMin;
        private UIObject _btnMax;
        private UIObject _btnBig;
        private UIObject _btnMap;

        private bool _bIsCollapsedState = false; // minimised minimap state
        private readonly BaseDXDrawableItem _collapsedFrame;

        private int _mapWidth, _mapHeight;

        private int _lastMinimapToggleTime = 0;
        private const int MINIMAP_TOGGLE_COOLDOWN_MS = 200; // Cooldown in milliseconds

        /// <summary>
        /// Constructor for the minimap window
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="_pixelDot"></param>
        public MinimapUI(IDXObject frame, BaseDXDrawableItem _pixelDot, BaseDXDrawableItem _collapsedFrame, int _mapWidth, int _mapHeight)
            : base(frame, false)
        {
            this._pixelDot = _pixelDot;
            this._collapsedFrame = _collapsedFrame;
            this._mapWidth = _mapWidth;
            this._mapHeight = _mapHeight;
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        /// <param name="baseClickableUIObject"></param>
        public void InitializeMinimapButtons(
            UIObject _btnMin,
            UIObject _btnMax,
            UIObject _btnBig, UIObject _btnMap)
        {
            this._btnMin = _btnMin;
            this._btnMax = _btnMax;
            if (_btnBig != null)
                this._btnBig = _btnBig;
            this._btnMap = _btnMap;

            uiButtons.Add(_btnMin);
            uiButtons.Add(_btnMax);
            if (_btnBig != null)
                uiButtons.Add(_btnBig);
            uiButtons.Add(_btnMap);

            _btnMax.SetButtonState(UIObjectState.Disabled); // start maximised

            _btnMin.ButtonClickReleased += ObjUIBtMin_ButtonClickReleased;
            _btnMax.ButtonClickReleased += ObjUIBtMax_ButtonClickReleased;
            if (_btnBig != null)
                _btnBig.ButtonClickReleased += ObjUIBtBig_ButtonClickReleased;
            _btnMap.ButtonClickReleased += ObjUIBtMap_ButtonClickReleased;
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
            if (_bIsCollapsedState) {
                _collapsedFrame.Draw(sprite, skeletonMeshRenderer, gameTime,
                   0, 0, centerX, centerY,
                   drawReflectionInfo,
                   renderParameters,
                   TickCount);
            } else {
                base.Draw(sprite, skeletonMeshRenderer, gameTime,
                   0, 0, centerX, centerY,
                   drawReflectionInfo,
                   renderParameters,
                   TickCount);

                int minimapPosX = (mapShiftX + (renderParameters.RenderWidth / 2)) / 16;
                int minimapPosY = (mapShiftY + (renderParameters.RenderHeight / 2)) / 16;

                // Draw the minimap pixel dor
                _pixelDot.Draw(sprite, skeletonMeshRenderer, gameTime,
                    -Position.X, -Position.Y, minimapPosX, minimapPosY,
                    drawReflectionInfo,
                    renderParameters,
                    TickCount);
            }

            //IDXObject lastFrameDrawn = base.LastFrameDrawn;
            //int minimapMainFrameWidth = lastFrameDrawn.Width;
            //int minimapMainFrameHeight = lastFrameDrawn.Height;

            // draw minimap buttons
            foreach (UIObject uiBtn in uiButtons)
            {
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currTickCount"></param>
        public void MinimiseOrMaximiseMinimap(int currTickCount) {
            if (currTickCount - _lastMinimapToggleTime > MINIMAP_TOGGLE_COOLDOWN_MS) {
                _lastMinimapToggleTime = currTickCount;

                if (!this._bIsCollapsedState) {
                    ObjUIBtMin_ButtonClickReleased(null);
                }
                else {
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

        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor, int renderWidth, int renderHeight) {
            foreach (UIObject uiBtn in uiButtons) {
                bool bHandled = uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, this.Position.X, this.Position.Y, mouseState);
                if (bHandled) {
                    mouseCursor.SetMouseCursorMovedToClickableItem();
                    return true;
                }
            }

            // handle UI movement
            if (mouseState.LeftButton == ButtonState.Pressed) {
                // The rectangle of the MinimapItem UI object

                // if drag has not started, initialize the offset
                if (mouseOffsetOnDragStart == null) {
                    Rectangle rect = new Rectangle(
                        this.Position.X,
                        this.Position.Y,
                        this.LastFrameDrawn.Width, this.LastFrameDrawn.Height);
                    if (!rect.Contains(mouseState.X, mouseState.Y)) {
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
                // Ensure the UI doesn't move outside the window
                newX = Math.Max(0, Math.Min(newX, renderWidth - frameWidth));
                newY = Math.Max(0, Math.Min(newY, renderHeight - frameHeight));

                this.Position = new Point(newX, newY);
                if (_bIsCollapsedState) {
                    this._collapsedFrame.Position = new Point(newX, newY);
                }
                //System.Diagnostics.Debug.WriteLine("Button rect: " + rect.ToString());
                //System.Diagnostics.Debug.WriteLine("Mouse X: " + mouseState.X + ", Y: " + mouseState.Y);
            }
            else {
                // if the mouse button is not pressed, reset the initial drag offset
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
        /// <param name="sender"></param>
        private void ObjUIBtMin_ButtonClickReleased(UIObject sender)
        {
            _btnMin.SetButtonState(UIObjectState.Disabled);
            _btnMax.SetButtonState(UIObjectState.Normal);

            _btnMap.X = this._collapsedFrame.Frame0.Width - _btnMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)
            if (_btnBig != null) {
                _btnBig.X = _btnMap.X - _btnBig.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
                _btnMax.X = _btnBig.X - _btnMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            } else { // beta maplestory
                _btnMax.X = _btnMap.X - _btnMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            }

            BaseDXDrawableItem baseItem = (BaseDXDrawableItem)this;
            _collapsedFrame.CopyObjectPosition(baseItem);

            this._bIsCollapsedState = true;
        }

        /// <summary>
        /// On 'BtMax' clicked.
        /// Map maximised mode
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtMax_ButtonClickReleased(UIObject sender)
        {
            _btnMin.SetButtonState(UIObjectState.Normal);
            _btnMax.SetButtonState(UIObjectState.Disabled);

            if (_btnBig != null) {
                _btnMap.X = this.Frame0.Width - _btnMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)
                _btnBig.X = _btnMap.X - _btnBig.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
                _btnMax.X = _btnBig.X - _btnMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            } else { // beta maplestory
                _btnMap.X = this.Frame0.Width - _btnMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)
                _btnMax.X = _btnMap.X - _btnMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
                _btnMin.X = _btnMax.X - _btnMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            }
            this.CopyObjectPosition(_collapsedFrame);

            this._bIsCollapsedState = false;
        }

        /// <summary>
        /// On 'BtBig' clicked
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtBig_ButtonClickReleased(UIObject sender)
        {
        }

        /// <summary>
        /// On 'BtMap' clicked
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtMap_ButtonClickReleased(UIObject sender)
        {
        }

        #endregion
    }
}
