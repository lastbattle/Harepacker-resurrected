using System;
using System.Collections.Generic;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements abstract multi-render texture
    /// </summary>
    public abstract class TextureBase : IDisposable
    {
        /// <summary>
        /// Gets the width.
        /// </summary>
        /// <value>
        /// The width.
        /// </value>
        public abstract int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        /// <value>
        /// The height.
        /// </value>
        public abstract int Height { get; }

        /// <summary>
        /// Gets the format.
        /// </summary>
        /// <value>
        /// The format.
        /// </value>
        public abstract TextureSurfaceFormat Format { get; }        

        /// <summary>
        /// Initializes a new instance of the <see cref="TextureBase"/> class.
        /// </summary>
        /// <param name="nativeTexture">The native texture.</param>
        public TextureBase(object nativeTexture)
        {
        }

        /// <summary>
        /// Gets the native texture.
        /// </summary>
        /// <returns></returns>
        public abstract object GetNativeTexture();

        /// <summary>
        /// Generates the one to one white texture content
        /// </summary>
        public abstract void GenerateOneToOne();

        /// <summary>
        /// Generates the solid color texture content
        /// </summary>
        /// <param name="borderThickness">The border thickness.</param>
        /// <param name="isBorder">if set to <c>true</c> [is border].</param>
        public abstract void GenerateSolidColor(Thickness borderThickness, bool isBorder);

        /// <summary>
        /// Generates the linear gradient texture content
        /// </summary>
        /// <param name="lineStart">The line start.</param>
        /// <param name="lineEnd">The line end.</param>
        /// <param name="borderThickness">The border thickness.</param>
        /// <param name="sortedStops">The sorted stops.</param>
        /// <param name="spread">The spread.</param>
        /// <param name="isBorder">if set to <c>true</c> [is border].</param>
        public abstract void GenerateLinearGradient(PointF lineStart, PointF lineEnd, Thickness borderThickness, List<GradientStop> sortedStops,
            GradientSpreadMethod spread, bool isBorder);

        /// <summary>
        /// Generates the check box texture content
        /// </summary>
        public abstract void GenerateCheckbox();

        /// <summary>
        /// Generates the arrow.
        /// </summary>
        /// <param name="direction">The direction.</param>
        /// <param name="startX">The start x point</param>
        /// <param name="lineSize">Size of the line.</param>
        public abstract void GenerateArrow(ArrowDirection direction, int startX, int lineSize);

        /// <summary>
        /// Sets the color data.
        /// </summary>
        /// <param name="data">The data.</param>
        public abstract void SetColorData(uint[] data);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();
    }
}
