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

namespace HaCreator.MapSimulator.UI
{
    /// <summary>
    /// For cursor
    /// </summary>
    public class MouseCursorItem : BaseDXDrawableItem
    {
        private MouseState _previousMouseState;
        public MouseState MouseState
        {
            get { return this._previousMouseState; }
            private set { }
        }

        private int _mouseCursorItemStates; // enum
        private bool _isHoveringToClickableButton = false;
        private bool _isHoveringToNpc = false;

        private readonly BaseDXDrawableItem _cursorPressedState; // default state of the cursor = this instance
        private readonly BaseDXDrawableItem _cursorClickableState;
        private readonly BaseDXDrawableItem _cursorNpcHoverState; // NPC hover cursor 

        /// <summary>
        /// Mouse cursor constructor
        /// </summary>
        /// <param name="frames"></param>
        /// <param name="_cursorPressedState"></param>
        /// <param name="_cursorClickableState"></param>
        /// <param name="_cursorNpcHoverState"></param>
        public MouseCursorItem(List<IDXObject> frames, BaseDXDrawableItem _cursorPressedState,
            BaseDXDrawableItem _cursorClickableState, BaseDXDrawableItem _cursorNpcHoverState = null)
            : base(frames, false)
        {
            _previousMouseState = Mouse.GetState();

            this._mouseCursorItemStates = (int)MouseCursorItemStates.Normal;
            this._cursorPressedState = _cursorPressedState;
            this._cursorClickableState = _cursorClickableState;
            this._cursorNpcHoverState = _cursorNpcHoverState;
        }

        /// <summary>
        /// Sets the mouse hovering to a clickable button state to true
        /// </summary>
        public void SetMouseCursorMovedToClickableItem() {
            this._isHoveringToClickableButton = true;
        }

        /// <summary>
        /// Sets the mouse hovering to NPC state to true
        /// </summary>
        public void SetMouseCursorMovedToNpc() {
            this._isHoveringToNpc = true;
        }

        /// <summary>
        /// Updates the cursor state
        /// </summary>
        public void UpdateCursorState()
        {
            // reset
            this._isHoveringToClickableButton = false;
            this._isHoveringToNpc = false;

            int newSetState = (int)MouseCursorItemStates.Normal; // 0

            // Left buttpn
            if (_previousMouseState.LeftButton == ButtonState.Released && Mouse.GetState().LeftButton == ButtonState.Pressed) // Left click
            {

            }
            else if (Mouse.GetState().LeftButton == ButtonState.Pressed)
            {
                newSetState |= (int)MouseCursorItemStates.LeftPress;
            }

            // Right button
            if (_previousMouseState.RightButton == ButtonState.Released && Mouse.GetState().RightButton == ButtonState.Pressed) // Right click
            {

            }
            else if (Mouse.GetState().RightButton == ButtonState.Pressed)
            {
                newSetState |= (int)MouseCursorItemStates.RightPress;
            }

            this._mouseCursorItemStates = newSetState;
            this._previousMouseState = Mouse.GetState(); // save
        }



        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="centerX"></param>
        /// <param name="centerY"></param>
        /// <param name="drawReflectionInfo"></param>
        /// <param name="renderParameters"></param>
        /// <param name="TickCount"></param>
        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            Point MousePos = Mouse.GetState().Position; // relative to the window already

            if ((_mouseCursorItemStates & (int)MouseCursorItemStates.LeftPress) != (int) MouseCursorItemStates.LeftPress
                 && (_mouseCursorItemStates & (int)MouseCursorItemStates.RightPress) != (int)MouseCursorItemStates.RightPress)  // default
            {
                if (_isHoveringToNpc && _cursorNpcHoverState != null) {
                    _cursorNpcHoverState.Draw(sprite, skeletonMeshRenderer, gameTime,
                        -MousePos.X, -MousePos.Y, centerX, centerY,
                        drawReflectionInfo,
                        renderParameters,
                        TickCount);
                }
                else if (_isHoveringToClickableButton) {
                    _cursorClickableState.Draw(sprite, skeletonMeshRenderer, gameTime,
                        -MousePos.X, -MousePos.Y, centerX, centerY,
                        drawReflectionInfo,
                        renderParameters,
                        TickCount);
                }
                else {
                    base.Draw(sprite, skeletonMeshRenderer, gameTime,
                        -MousePos.X, -MousePos.Y, centerX, centerY,
                        drawReflectionInfo,
                        renderParameters,
                        TickCount);
                }
            }
            else // if left or right press is active, draw pressed state
            {
                _cursorPressedState.Draw(sprite, skeletonMeshRenderer, gameTime,
                    -MousePos.X, -MousePos.Y, centerX, centerY,
                    drawReflectionInfo,
                    renderParameters,
                    TickCount);
            }
        }
    }

    public enum MouseCursorItemStates
    {
        Normal = 0x0,
        LeftPress = 0x1,
        RightPress = 0x2,
    }
}
