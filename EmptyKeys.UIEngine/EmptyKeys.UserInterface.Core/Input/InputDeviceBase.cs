
namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements abstract input device
    /// </summary>
    public abstract class InputDeviceBase
    {
        /// <summary>
        /// Gets the state of the mouse.
        /// </summary>
        /// <value>
        /// The state of the mouse.
        /// </value>
        public abstract MouseStateBase MouseState { get; }

        /// <summary>
        /// Gets the state of the game pad.
        /// </summary>
        /// <value>
        /// The state of the game pad.
        /// </value>
        public abstract GamePadStateBase GamePadState { get; }

        /// <summary>
        /// Gets the state of the keyboard.
        /// </summary>
        /// <value>
        /// The state of the keyboard.
        /// </value>
        public abstract KeyboardStateBase KeyboardState { get; }

        /// <summary>
        /// Gets or sets the state of the touch.
        /// </summary>
        /// <value>
        /// The state of the touch.
        /// </value>
        public abstract TouchStateBase TouchState { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputDeviceBase"/> class.
        /// </summary>
        public InputDeviceBase()
        {
        }
    }
}
