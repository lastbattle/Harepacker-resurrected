using HaSharedLibrary.Render;
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

namespace HaCreator.MapSimulator.MapObjects.UIObject {

    public class StatusBarUI : BaseDXDrawableItem, IUIObjectEvents {
        private readonly List<UIObject> uiButtons = new List<UIObject>();

        /// <summary>
        /// Constructor for the minimap window
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="obj_Ui_BtCashShop">The BtCashShop button</param>
        /// <param name="obj_Ui_BtMTS">The MTS button</param>
        /// <param name="obj_Ui_BtMenu"></param>
        /// <param name="obj_Ui_BtSystem">The System button</param>
        /// <param name="obj_Ui_BtChannel">The Channel button</param>
        public StatusBarUI(IDXObject frame, UIObject obj_Ui_BtCashShop, UIObject obj_Ui_BtMTS, UIObject obj_Ui_BtMenu, UIObject obj_Ui_BtSystem, UIObject obj_Ui_BtChannel, Point setPosition,
            List<UIObject> otherUI)
            : base(frame, false) {

            uiButtons.Add(obj_Ui_BtCashShop);
            uiButtons.Add(obj_Ui_BtMTS);
            uiButtons.Add(obj_Ui_BtMenu);
            uiButtons.Add(obj_Ui_BtSystem);
            uiButtons.Add(obj_Ui_BtChannel);

            uiButtons.AddRange(otherUI);

            this.Position = setPosition;
        }

        /// <summary>
        /// Add UI buttons to be rendered
        /// </summary>
        /// <param name="baseClickableUIObject"></param>
        public void InitializeButtons() {
            //objUIBtMax.SetButtonState(UIObjectState.Disabled); // start maximised
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            int RenderWidth, int RenderHeight, float RenderObjectScaling, RenderResolution mapRenderResolution,
            int TickCount) {
            // control minimap render UI position via
            //  Position.X, Position.Y

            // Draw the main frame
            base.Draw(sprite, skeletonMeshRenderer, gameTime,
                   this.Position.X, this.Position.Y, centerX, centerY,
                   drawReflectionInfo,
                   RenderWidth, RenderHeight, RenderObjectScaling, mapRenderResolution,
                   TickCount);

            // draw other buttons
            foreach (UIObject uiBtn in uiButtons) {
                BaseDXDrawableItem buttonToDraw = uiBtn.GetBaseDXDrawableItemByState();

                // Position drawn is relative to this UI
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

        #region IClickableUIObject
        private Point? mouseOffsetOnDragStart = null;
        public void CheckMouseEvent(int shiftCenteredX, int shiftCenteredY, MouseState mouseState) {
            foreach (MapObjects.UIObject.UIObject uiBtn in uiButtons) {
                // Position drawn is relative to this UI
                //int drawRelativeX = -(this.Position.X) - uiBtn.X; // Left to right
                //int drawRelativeY = -(this.Position.Y) - uiBtn.Y; // Top to bottom

                bool bHandled = uiBtn.CheckMouseEvent(shiftCenteredX, shiftCenteredY, this.Position.X, this.Position.Y, mouseState);
                if (bHandled)
                    return;
            }

            // handle UI movement
          /*  if (mouseState.LeftButton == ButtonState.Pressed) {
                // The rectangle of the MinimapItem UI object

                // if drag has not started, initialize the offset
                if (mouseOffsetOnDragStart == null) {
                    Rectangle rect = new Rectangle(
                        this.Position.X,
                        this.Position.Y,
                        this.LastFrameDrawn.Width, this.LastFrameDrawn.Height);
                    if (!rect.Contains(mouseState.X, mouseState.Y)) {
                        return;
                    }
                    mouseOffsetOnDragStart = new Point(mouseState.X - this.Position.X, mouseState.Y - this.Position.Y);
                }

                // Calculate the mouse position relative to the minimap
                // and move the minimap Position
                this.Position = new Point(mouseState.X - mouseOffsetOnDragStart.Value.X, mouseState.Y - mouseOffsetOnDragStart.Value.Y);
                //System.Diagnostics.Debug.WriteLine("Button rect: " + rect.ToString());
                //System.Diagnostics.Debug.WriteLine("Mouse X: " + mouseState.X + ", Y: " + mouseState.Y);
            }
            else {
                // if the mouse button is not pressed, reset the initial drag offset
                mouseOffsetOnDragStart = null;

                // If the window is outside at the end of mouse click + move
                // move it slightly back to the nearest X and Y coordinate
            }*/
        }
        #endregion
    }
}
