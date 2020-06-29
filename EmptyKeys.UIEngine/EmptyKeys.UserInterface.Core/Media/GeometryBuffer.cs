using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements abstract Geometry Buffer
    /// </summary>
    public abstract class GeometryBuffer : IDisposable
    {
        /// <summary>
        /// Gets or sets the primitive count.
        /// </summary>
        /// <value>
        /// The primitive count.
        /// </value>
        public int PrimitiveCount { get; set; }

        /// <summary>
        /// Gets or sets the type of the primitive.
        /// </summary>
        /// <value>
        /// The type of the primitive.
        /// </value>
        public GeometryPrimitiveType PrimitiveType { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeometryBuffer"/> class.
        /// </summary>
        public GeometryBuffer()
        {
        }

        /// <summary>
        /// Fills the color type buffer (VertexPositionColor)
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="primitiveType">Type of the primitive.</param>
        public abstract void FillColor(List<PointF> points, GeometryPrimitiveType primitiveType);

        /// <summary>
        /// Fills the texture type buffer (VertexPositionTexture)
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="destinationSize">Size of the destination.</param>
        /// <param name="sourceRect">The source rect.</param>
        /// <param name="primitiveType">Type of the primitive.</param>
        public abstract void FillTexture(List<PointF> points, Size destinationSize, Rect sourceRect, GeometryPrimitiveType primitiveType);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public abstract void Dispose();        
    }
}
