
namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Implements abstract Touch state
    /// </summary>
    public abstract class TouchStateBase
    {
        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        public abstract int Id { get; }

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
        /// Gets the gesture.
        /// </summary>
        /// <value>
        /// The gesture.
        /// </value>
        public abstract TouchGestures Gesture { get; }

        /// <summary>
        /// Gets the move x.
        /// </summary>
        /// <value>
        /// The move x.
        /// </value>
        public abstract float MoveX { get; }

        /// <summary>
        /// Gets the move y.
        /// </summary>
        /// <value>
        /// The move y.
        /// </value>
        public abstract float MoveY { get; }

        /// <summary>
        /// Gets a value indicating whether is touched.
        /// </summary>
        /// <value>
        /// <c>true</c> if is touched; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsTouched { get; }

        /// <summary>
        /// Gets the action.
        /// </summary>
        /// <value>
        /// The action.
        /// </value>
        public abstract TouchAction Action { get; }

        /// <summary>
        /// Gets a value indicating whether this instance has gesture.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has gesture; otherwise, <c>false</c>.
        /// </value>
        public abstract bool HasGesture { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TouchStateBase"/> class.
        /// </summary>
        public TouchStateBase()
        {
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        public abstract void Update();
    }
}
