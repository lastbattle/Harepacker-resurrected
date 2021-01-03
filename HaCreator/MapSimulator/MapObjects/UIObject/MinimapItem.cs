using HaCreator.MapSimulator.MapObjects.UIObject;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Objects.UIObject
{
    /// <summary>
    /// Mini map window item
    /// </summary>
    public class MinimapItem : BaseDXDrawableItem, IUIObjectEvents
    {
        private readonly BaseDXDrawableItem item_pixelDot;
        private readonly List<MapObjects.UIObject.UIObject> uiButtons = new List<MapObjects.UIObject.UIObject>();

        private MapObjects.UIObject.UIObject objUIBtMin;
        private MapObjects.UIObject.UIObject objUIBtMax;
        private MapObjects.UIObject.UIObject objUIBtBig;
        private MapObjects.UIObject.UIObject objUIBtMap;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="frames"></param>
        /// <param name="item_pixelDot"></param>
        public MinimapItem(IDXObject frames, BaseDXDrawableItem item_pixelDot)
            : base(frames, false)
        {
            this.item_pixelDot = item_pixelDot;

            this.Position = new Point(10, 10); // starting position
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
            this.objUIBtBig = objUIBtBig;
            this.objUIBtMap = objUIBtMap;

            uiButtons.Add(objUIBtMin);
            uiButtons.Add(objUIBtMax);
            uiButtons.Add(objUIBtBig);
            uiButtons.Add(objUIBtMap);

            objUIBtMax.SetButtonState(UIObjectState.Disabled); // start maximised

            objUIBtMin.ButtonClickReleased += ObjUIBtMin_ButtonClickReleased;
            objUIBtMax.ButtonClickReleased += ObjUIBtMax_ButtonClickReleased;
            objUIBtBig.ButtonClickReleased += ObjUIBtBig_ButtonClickReleased;
            objUIBtMap.ButtonClickReleased += ObjUIBtMap_ButtonClickReleased;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            int RenderWidth, int RenderHeight, float RenderObjectScaling, RenderResolution mapRenderResolution,
            int TickCount)
        {
            // control minimap render UI position via
            //  Position.X, Position.Y

            // Draw the main drame
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                0, 0, centerX, centerY,
                RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);

            int minimapPosX = (mapShiftX + (RenderWidth / 2)) / 16;
            int minimapPosY = (mapShiftY + (RenderHeight / 2)) / 16;

            item_pixelDot.Draw(sprite, skeletonMeshRenderer, gameTime,
                -Position.X, -Position.Y, minimapPosX, minimapPosY,
                RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                TickCount);

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
                    RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution, TickCount);
            }
        }

        #region IClickableUIObject
        public void CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState)
        {
            foreach (MapObjects.UIObject.UIObject uiBtn in uiButtons)
            {
                uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, this.Position.X, this.Position.Y, mouseState);
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// On 'BtMin' clicked
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtMin_ButtonClickReleased(MapObjects.UIObject.UIObject sender)
        {
            objUIBtMin.SetButtonState(UIObjectState.Disabled);
            objUIBtMax.SetButtonState(UIObjectState.Normal);
        }

        /// <summary>
        /// On 'BtMax' clicked
        /// </summary>
        /// <param name="sender"></param>
        private void ObjUIBtMax_ButtonClickReleased(MapObjects.UIObject.UIObject sender)
        {
            objUIBtMin.SetButtonState(UIObjectState.Normal);
            objUIBtMax.SetButtonState(UIObjectState.Disabled);
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
