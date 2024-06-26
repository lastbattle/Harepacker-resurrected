using HaCreator.MapSimulator.MapObjects.UIObject;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Objects.UIObject
{
    /// <summary>
    /// Mini map window item
    /// </summary>
    public class MinimapUI : BaseDXDrawableItem, IUIObjectEvents
    {
        private readonly BaseDXDrawableItem item_pixelDot;
        private readonly List<MapObjects.UIObject.UIObject> uiButtons = new List<MapObjects.UIObject.UIObject>();

        private MapObjects.UIObject.UIObject objUIBtMin;
        private MapObjects.UIObject.UIObject objUIBtMax;
        private MapObjects.UIObject.UIObject objUIBtBig;
        private MapObjects.UIObject.UIObject objUIBtMap;

        private bool _bIsCollapsedState = false; // minimised minimap state
        private readonly BaseDXDrawableItem frame_collapsedState;

        private int mapWidth, mapHeight;

        private int lastMinimapToggleTime = 0;
        private const int MINIMAP_TOGGLE_COOLDOWN_MS = 200; // Cooldown in milliseconds

        /// <summary>
        /// Constructor for the minimap window
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="item_pixelDot"></param>
        public MinimapUI(IDXObject frame, BaseDXDrawableItem item_pixelDot, BaseDXDrawableItem frame_collapsedState, int mapWidth, int mapHeight)
            : base(frame, false)
        {
            this.item_pixelDot = item_pixelDot;
            this.frame_collapsedState = frame_collapsedState;
            this.mapWidth = mapWidth;
            this.mapHeight = mapHeight;
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        /// <param name="baseClickableUIObject"></param>
        public void InitializeMinimapButtons(
            MapObjects.UIObject.UIObject objUIBtMin,
            MapObjects.UIObject.UIObject objUIBtMax,
            MapObjects.UIObject.UIObject objUIBtBig, MapObjects.UIObject.UIObject objUIBtMap)
        {
            this.objUIBtMin = objUIBtMin;
            this.objUIBtMax = objUIBtMax;
            if (objUIBtBig != null)
                this.objUIBtBig = objUIBtBig;
            this.objUIBtMap = objUIBtMap;

            uiButtons.Add(objUIBtMin);
            uiButtons.Add(objUIBtMax);
            if (objUIBtBig != null)
                uiButtons.Add(objUIBtBig);
            uiButtons.Add(objUIBtMap);

            objUIBtMax.SetButtonState(UIObjectState.Disabled); // start maximised

            objUIBtMin.ButtonClickReleased += ObjUIBtMin_ButtonClickReleased;
            objUIBtMax.ButtonClickReleased += ObjUIBtMax_ButtonClickReleased;
            if (objUIBtBig != null)
                objUIBtBig.ButtonClickReleased += ObjUIBtBig_ButtonClickReleased;
            objUIBtMap.ButtonClickReleased += ObjUIBtMap_ButtonClickReleased;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            int RenderWidth, int RenderHeight, float RenderObjectScaling, RenderResolution mapRenderResolution,
            int TickCount)
        {
            // control minimap render UI position via
            //  Position.X, Position.Y

            // Draw the main frame
            if (_bIsCollapsedState) {
                frame_collapsedState.Draw(sprite, skeletonMeshRenderer, gameTime,
                   0, 0, centerX, centerY,
                   drawReflectionInfo,
                   RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                   TickCount);
            } else {
                base.Draw(sprite, skeletonMeshRenderer, gameTime,
                   0, 0, centerX, centerY,
                   drawReflectionInfo,
                   RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                   TickCount);

                int minimapPosX = (mapShiftX + (RenderWidth / 2)) / 16;
                int minimapPosY = (mapShiftY + (RenderHeight / 2)) / 16;

                // Draw the minimap pixel dor
                item_pixelDot.Draw(sprite, skeletonMeshRenderer, gameTime,
                    -Position.X, -Position.Y, minimapPosX, minimapPosY,
                    drawReflectionInfo,
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                    TickCount);
            }

            //IDXObject lastFrameDrawn = base.LastFrameDrawn;
            //int minimapMainFrameWidth = lastFrameDrawn.Width;
            //int minimapMainFrameHeight = lastFrameDrawn.Height;

            // draw minimap buttons
            foreach (MapObjects.UIObject.UIObject uiBtn in uiButtons)
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
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution, TickCount);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currTickCount"></param>
        public void MinimiseOrMaximiseMinimap(int currTickCount) {
            if (currTickCount - lastMinimapToggleTime > MINIMAP_TOGGLE_COOLDOWN_MS) {
                lastMinimapToggleTime = currTickCount;

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
        public bool CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState, MouseCursorItem mouseCursor) {
            foreach (MapObjects.UIObject.UIObject uiBtn in uiButtons) {
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
                this.Position = new Point(mouseState.X - mouseOffsetOnDragStart.Value.X, mouseState.Y - mouseOffsetOnDragStart.Value.Y);
                if (_bIsCollapsedState) {
                    this.frame_collapsedState.Position = new Point(mouseState.X - mouseOffsetOnDragStart.Value.X, mouseState.Y - mouseOffsetOnDragStart.Value.Y);
                }
                //System.Diagnostics.Debug.WriteLine("Button rect: " + rect.ToString());
                //System.Diagnostics.Debug.WriteLine("Mouse X: " + mouseState.X + ", Y: " + mouseState.Y);
            }
            else {
                // if the mouse button is not pressed, reset the initial drag offset
                mouseOffsetOnDragStart = null;

                // If the window is outside at the end of mouse click + move
                // move it slightly back to the nearest X and Y coordinate
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
        private void ObjUIBtMin_ButtonClickReleased(MapObjects.UIObject.UIObject sender)
        {
            objUIBtMin.SetButtonState(UIObjectState.Disabled);
            objUIBtMax.SetButtonState(UIObjectState.Normal);

            objUIBtMap.X = this.frame_collapsedState.Frame0.Width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)
            objUIBtBig.X = objUIBtMap.X - objUIBtBig.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            objUIBtMax.X = objUIBtBig.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

            BaseDXDrawableItem baseItem = (BaseDXDrawableItem)this;
            frame_collapsedState.CopyObjectPosition(baseItem);

            this._bIsCollapsedState = true;
        }

        /// <summary>
        /// On 'BtMax' clicked.
        /// Map maximised mode
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtMax_ButtonClickReleased(MapObjects.UIObject.UIObject sender)
        {
            objUIBtMin.SetButtonState(UIObjectState.Normal);
            objUIBtMax.SetButtonState(UIObjectState.Disabled);

            objUIBtMap.X = this.Frame0.Width - objUIBtMap.CanvasSnapshotWidth - 8; // render at the (width of minimap - obj width)
            objUIBtBig.X = objUIBtMap.X - objUIBtBig.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            objUIBtMax.X = objUIBtBig.X - objUIBtMax.CanvasSnapshotWidth; // render at the (width of minimap - obj width)
            objUIBtMin.X = objUIBtMax.X - objUIBtMin.CanvasSnapshotWidth; // render at the (width of minimap - obj width)

            this.CopyObjectPosition(frame_collapsedState);

            this._bIsCollapsedState = false;
        }

        /// <summary>
        /// On 'BtBig' clicked
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtBig_ButtonClickReleased(MapObjects.UIObject.UIObject sender)
        {
        }

        /// <summary>
        /// On 'BtMap' clicked
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtMap_ButtonClickReleased(MapObjects.UIObject.UIObject sender)
        {
        }

        #endregion
    }
}
