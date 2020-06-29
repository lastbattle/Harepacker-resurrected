using System.Text;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements abstract multi-render font
    /// </summary>
    public abstract class FontBase
    {
        /// <summary>
        /// Gets the line spacing.
        /// </summary>
        /// <value>
        /// The line spacing.
        /// </value>
        public abstract int LineSpacing { get; }

        /// <summary>
        /// Gets the default character.
        /// </summary>
        /// <value>
        /// The default character.
        /// </value>
        public abstract char? DefaultCharacter { get; }

        /// <summary>
        /// Gets or sets the spacing.
        /// </summary>
        /// <value>
        /// The spacing.
        /// </value>
        public abstract float Spacing { get; set; }

        /// <summary>
        /// Gets or sets the type of the effect.
        /// </summary>
        /// <value>
        /// The type of the effect.
        /// </value>
        public abstract FontEffectType EffectType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FontBase"/> class.
        /// </summary>
        /// <param name="nativeFont">The native font.</param>
        public FontBase(object nativeFont)
        {
        }

        /// <summary>
        /// Gets the native font.
        /// </summary>
        /// <returns></returns>
        public abstract object GetNativeFont();

        /// <summary>
        /// Measures the string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="dpiScaleX">The dpi scale x.</param>
        /// <param name="dpiScaleY">The dpi scale y.</param>
        /// <returns></returns>
        public abstract Size MeasureString(string text, float dpiScaleX, float dpiScaleY);

        /// <summary>
        /// Measures the string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="dpiScaleX">The dpi scale x.</param>
        /// <param name="dpiScaleY">The dpi scale y.</param>
        /// <returns></returns>
        public abstract Size MeasureString(StringBuilder text, float dpiScaleX, float dpiScaleY);
    }
}
