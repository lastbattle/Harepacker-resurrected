
namespace EmptyKeys.UserInterface.Input
{    
    /// <summary>
    /// Implements abstract Game Pad State
    /// </summary>
    public abstract class GamePadStateBase
    {
        /// <summary>
        /// Gets a value indicating whether this instance is a button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is a button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsAButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is b button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is b button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsBButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is c button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is c button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsCButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is d button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is d button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsDButtonPressed { get; }

        /// <summary>
        /// Gets the d pad.
        /// </summary>
        /// <value>
        /// The d pad.
        /// </value>
        public abstract PointF DPad { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is left shoulder button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is left shoulder button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsLeftShoulderButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is left stick button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is left stick button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsLeftStickButtonPressed { get; }

        /// <summary>
        /// Gets the left thumb stick.
        /// </summary>
        /// <value>
        /// The left thumb stick.
        /// </value>
        public abstract PointF LeftThumbStick { get; }

        /// <summary>
        /// Gets the left trigger.
        /// </summary>
        /// <value>
        /// The left trigger.
        /// </value>
        public abstract float LeftTrigger { get; }

        /// <summary>
        /// Gets the player number.
        /// </summary>
        /// <value>
        /// The player number.
        /// </value>
        public abstract int PlayerNumber { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is right shoulder button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is right shoulder button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsRightShoulderButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is right stick button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is right stick button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsRightStickButtonPressed { get; }

        /// <summary>
        /// Gets the right thumb stick.
        /// </summary>
        /// <value>
        /// The right thumb stick.
        /// </value>
        public abstract PointF RightThumbStick { get; }

        /// <summary>
        /// Gets the right trigger.
        /// </summary>
        /// <value>
        /// The right trigger.
        /// </value>
        public abstract float RightTrigger { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is select button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is select button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsSelectButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is start button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is start button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsStartButtonPressed { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GamePadStateBase"/> class.
        /// </summary>
        public GamePadStateBase()
        {
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        /// <param name="gamePadIndex">Index of the game pad.</param>
        public abstract void Update(int gamePadIndex);
    }
}
