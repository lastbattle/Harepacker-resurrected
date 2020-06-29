using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Input.Touch;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements MonoGame specific input device
    /// </summary>
    public class MonoGameInputDevice : InputDeviceBase
    {
        private MouseStateBase mouseState = new MonoGameMouseState();
        private GamePadStateBase gamePadState = new MonoGameGamePadState();
        private KeyboardStateBase keyboardState = new MonoGameKeyboardState();
        private TouchStateBase touchState = new MonoGameTouchState();

        /// <summary>
        /// Gets the state of the mouse.
        /// </summary>
        /// <value>
        /// The state of the mouse.
        /// </value>
        public override MouseStateBase MouseState
        {
            get { return mouseState; }
        }

        /// <summary>
        /// Gets the state of the game pad.
        /// </summary>
        /// <value>
        /// The state of the game pad.
        /// </value>
        public override GamePadStateBase GamePadState
        {
            get { return gamePadState; }
        }

        /// <summary>
        /// Gets the state of the keyboard.
        /// </summary>
        /// <value>
        /// The state of the keyboard.
        /// </value>
        public override KeyboardStateBase KeyboardState
        {
            get { return keyboardState; }
        }

        /// <summary>
        /// Gets or sets the state of the touch.
        /// </summary>
        /// <value>
        /// The state of the touch.
        /// </value>
        public override TouchStateBase TouchState
        {
            get { return touchState; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameInputDevice"/> class.
        /// </summary>
        public MonoGameInputDevice()
            : base()
        {
            TouchPanel.EnabledGestures = GestureType.Tap | GestureType.HorizontalDrag |
                GestureType.VerticalDrag;
        }        
    }
}
