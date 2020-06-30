using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xenko.Input;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements Xenko specific input device
    /// </summary>
    public class XenkoInputDevice : InputDeviceBase
    {
        public static Xenko.Input.InputManager NativeInputManager
        {
            get;
            set;
        }

        private MouseStateBase mouseState = new XenkoMouseState();
        private GamePadStateBase gamePadState = new XenkoGamePadState();
        private KeyboardStateBase keyboardState = new XenkoKeyboardState();
        private TouchStateBase touchState = new XenkoTouchState();

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
        /// Initializes a new instance of the <see cref="XenkoInputDevice"/> class.
        /// </summary>
        public XenkoInputDevice()
            : base()
        {
            NativeInputManager.Gestures.Add(new GestureConfigDrag());
            NativeInputManager.Gestures.Add(new GestureConfigFlick());            
            //NativeInputManager.Gestures.Add(new GestureConfigTap());
            NativeInputManager.Gestures.Add(new GestureConfigComposite());
        }        
    }
}
