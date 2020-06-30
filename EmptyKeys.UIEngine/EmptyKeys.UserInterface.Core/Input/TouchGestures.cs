using System;

namespace EmptyKeys.UserInterface.Input
{
    /// <summary>
    /// Describes touch gesture types
    /// </summary>
    [Flags]
    public enum TouchGestures
    {
        /// <summary>
        /// The none
        /// </summary>
        None = 0,
        /// <summary>
        /// The tap
        /// </summary>
        Tap = 1,
        /// <summary>
        /// The drag complete
        /// </summary>
        DragComplete = 2,
        /// <summary>
        /// The flick
        /// </summary>
        Flick = 4,
        /// <summary>
        /// The free drag
        /// </summary>
        FreeDrag = 8,
        /// <summary>
        /// The hold
        /// </summary>
        Hold = 16,
        /// <summary>
        /// The horizontal drag
        /// </summary>
        HorizontalDrag = 32,
        /// <summary>
        /// The pinch
        /// </summary>
        Pinch = 64,
        /// <summary>
        /// The pinch complete
        /// </summary>
        PinchComplete = 128,
        /// <summary>
        /// The double tap
        /// </summary>
        DoubleTap = 256,
        /// <summary>
        /// The vertical drag
        /// </summary>
        VerticalDrag = 512,
        /// <summary>
        /// The move rotate and scale
        /// </summary>
        MoveRotateAndScale = 1024
    }
}
