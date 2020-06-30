
namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Contains information bout area of linear gradient
    /// </summary>
    public class GradientStop
    {
        /// <summary>
        /// Gets or sets the color.
        /// </summary>
        /// <value>
        /// The color.
        /// </value>
        public ColorW Color
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the offset.
        /// </summary>
        /// <value>
        /// The offset.
        /// </value>
        public float Offset
        {
            get;
            set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GradientStop"/> class.
        /// </summary>
        public GradientStop()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GradientStop"/> class.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <param name="offset">The offset.</param>
        public GradientStop(ColorW color, float offset)
        {
            Color = color;
            Offset = offset;
        }
    }
}
