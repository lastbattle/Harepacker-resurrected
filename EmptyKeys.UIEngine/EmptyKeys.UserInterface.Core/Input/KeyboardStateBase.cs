
namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements abstract keyboard state
    /// </summary>
    public abstract class KeyboardStateBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeyboardStateBase"/> class.
        /// </summary>
        public KeyboardStateBase()
        {
        }

        /// <summary>
        /// Determines whether [is key pressed] [the specified key code].
        /// </summary>
        /// <param name="keyCode">The key code.</param>
        /// <returns></returns>
        public abstract bool IsKeyPressed(KeyCode keyCode);

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public abstract void Update();
    }
}
