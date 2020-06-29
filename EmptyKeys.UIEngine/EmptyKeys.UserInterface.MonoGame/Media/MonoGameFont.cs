using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements MonoGame specific font
    /// </summary>
    public class MonoGameFont : FontBase
    {
        private SpriteFont font;

        /// <summary>
        /// Gets the line spacing.
        /// </summary>
        /// <value>
        /// The line spacing.
        /// </value>
        public override int LineSpacing
        {
            get { return font.LineSpacing; }
        }

        /// <summary>
        /// Gets the default character.
        /// </summary>
        /// <value>
        /// The default character.
        /// </value>
        public override char? DefaultCharacter
        {
            get { return font.DefaultCharacter; }
        }

        /// <summary>
        /// Gets or sets the spacing.
        /// </summary>
        /// <value>
        /// The spacing.
        /// </value>
        public override float Spacing
        {
            get
            {
                return font.Spacing;
            }
            set
            {
                font.Spacing = value;
            }
        }

        /// <summary>
        /// Gets or sets the type of the effect.
        /// </summary>
        /// <value>
        /// The type of the effect.
        /// </value>
        public override FontEffectType EffectType
        {
            get
            {
                return FontEffectType.None;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameFont" /> class.
        /// </summary>
        /// <param name="nativeFont">The native font.</param>
        public MonoGameFont(object nativeFont)
            : base(nativeFont)
        {
            font = nativeFont as SpriteFont;
        }

        /// <summary>
        /// Gets the native font.
        /// </summary>
        /// <returns></returns>
        public override object GetNativeFont()
        {
            return font;
        }

        /// <summary>
        /// Measures the string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="dpiScaleX">The dpi scale x.</param>
        /// <param name="dpiScaleY">The dpi scale y.</param>
        /// <returns></returns>
        public override Size MeasureString(string text, float dpiScaleX, float dpiScaleY)
        {
            Vector2 result = font.MeasureString(text);
            result.X /= dpiScaleX;
            result.Y /= dpiScaleY;
            return new Size(result.X, result.Y);
        }

        /// <summary>
        /// Measures the string.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="dpiScaleX">The dpi scale x.</param>
        /// <param name="dpiScaleY">The dpi scale y.</param>
        /// <returns></returns>
        public override Size MeasureString(StringBuilder text, float dpiScaleX, float dpiScaleY)
        {
            Vector2 result = font.MeasureString(text);
            result.X /= dpiScaleX;
            result.Y /= dpiScaleY;
            return new Size(result.X, result.Y);
        }              
    }
}