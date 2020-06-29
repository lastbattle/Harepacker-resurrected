using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements MonoGame specific mouse state
    /// </summary>
    public class MonoGameMouseState : MouseStateBase
    {
        private MouseState state;

        /// <summary>
        /// Gets a value indicating whether this instance is left button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is left button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsLeftButtonPressed
        {
            get { return state.LeftButton == ButtonState.Pressed; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is middle button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is middle button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsMiddleButtonPressed
        {
            get { return state.MiddleButton == ButtonState.Pressed; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is right button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is right button pressed; otherwise, <c>false</c>.
        /// </value>
        public override bool IsRightButtonPressed
        {
            get { return state.RightButton == ButtonState.Pressed; }
        }

        /// <summary>
        /// Gets the normalized x.
        /// </summary>
        /// <value>
        /// The normalized x.
        /// </value>
        public override float NormalizedX
        {
            get
            {
                float width = Engine.Instance.Renderer.NativeScreenWidth;
                return state.Position.X / width;
            }
        }

        /// <summary>
        /// Gets the normalized y.
        /// </summary>
        /// <value>
        /// The normalized y.
        /// </value>
        public override float NormalizedY
        {
            get
            {
                float height = Engine.Instance.Renderer.NativeScreenHeight;
                return state.Position.Y / height;
            }
        }

        /// <summary>
        /// Gets the scroll wheel value.
        /// </summary>
        /// <value>
        /// The scroll wheel value.
        /// </value>
        public override int ScrollWheelValue
        {
            get { return state.ScrollWheelValue; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether mouse is visible.
        /// </summary>
        /// <value>
        ///   <c>true</c> if mouse is visible; otherwise, <c>false</c>.
        /// </value>
        public override bool IsVisible
        {
            get
            {
                return false;
            }
            set
            {                
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameMouseState"/> class.
        /// </summary>
        public MonoGameMouseState()
            : base()
        {
        }

        /// <summary>
        /// Sets the position.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public override void SetPosition(int x, int y)
        {            
            Microsoft.Xna.Framework.Input.Mouse.SetPosition(x, y);
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public override void Update()
        {
            state = Microsoft.Xna.Framework.Input.Mouse.GetState();
        }
    }
}
