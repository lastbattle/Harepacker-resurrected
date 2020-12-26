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
        public void AddUIButtons(MapObjects.UIObject.UIObject baseClickableUIObject)
        {
            uiButtons.Add(baseClickableUIObject);
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
    }
}
