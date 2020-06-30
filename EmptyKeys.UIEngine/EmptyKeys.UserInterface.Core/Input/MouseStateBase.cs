
namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements abstract Mouse State
    /// </summary>
    public abstract class MouseStateBase
    {
        /// <summary>
        /// Gets a value indicating whether this instance is left button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is left button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsLeftButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is middle button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is middle button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsMiddleButtonPressed { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is right button pressed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is right button pressed; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsRightButtonPressed { get; }

        /// <summary>
        /// Gets the normalized x.
        /// </summary>
        /// <value>
        /// The normalized x.
        /// </value>
        public abstract float NormalizedX { get; }

        /// <summary>
        /// Gets the normalized y.
        /// </summary>
        /// <value>
        /// The normalized y.
        /// </value>
        public abstract float NormalizedY { get; }

        /// <summary>
        /// Gets the scroll wheel value.
        /// </summary>
        /// <value>
        /// The scroll wheel value.
        /// </value>
        public abstract int ScrollWheelValue { get; }

        /// <summary>
        /// Gets or sets a value indicating whether mouse is visible.
        /// </summary>
        /// <value>
        /// <c>true</c> if mouse is visible; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsVisible { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MouseStateBase"/> class.
        /// </summary>
        public MouseStateBase()
        {
        }
        
        /// <summary>
        /// Sets the position.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public abstract void SetPosition(int x, int y);

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public abstract void Update();
    }
}
