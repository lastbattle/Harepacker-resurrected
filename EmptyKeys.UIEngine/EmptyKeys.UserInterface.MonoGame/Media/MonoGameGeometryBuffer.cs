using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmptyKeys.UserInterface.Renderers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EmptyKeys.UserInterface.Media
{
    /// <summary>
    /// Implements Mono Game specific geometry buffer
    /// </summary>
    public class MonoGameGeometryBuffer : GeometryBuffer
    {
        /// <summary>
        /// Gets or sets the vertex buffer.
        /// </summary>
        /// <value>
        /// The vertex buffer.
        /// </value>
        public VertexBuffer VertexBuffer { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonoGameGeometryBuffer"/> class.
        /// </summary>
        public MonoGameGeometryBuffer()
            : base()
        {
        }

        /// <summary>
        /// Fills the color type buffer (VertexPositionColor)
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="primitiveType">Type of the primitive.</param>
        public override void FillColor(List<PointF> points, GeometryPrimitiveType primitiveType)
        {
            SetPrimitiveCount(primitiveType, points.Count);

            VertexPositionColor[] vertex = new VertexPositionColor[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                vertex[i] = new VertexPositionColor(new Vector3(points[i].X, points[i].Y, 0), Color.White);
            }

            VertexBuffer = new VertexBuffer(MonoGameRenderer.GraphicsDevice, VertexPositionColor.VertexDeclaration, vertex.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData<VertexPositionColor>(vertex);
        }

        private void SetPrimitiveCount(GeometryPrimitiveType primitiveType, int pointCount)
        {
            PrimitiveType = primitiveType;
            switch (primitiveType)
            {
                case GeometryPrimitiveType.TriangleList:
                    PrimitiveCount = pointCount / 3;
                    break;
                case GeometryPrimitiveType.TriangleStrip:
                    PrimitiveCount = pointCount - 2;
                    break;
                case GeometryPrimitiveType.LineList:
                    PrimitiveCount = pointCount / 2;
                    break;
                case GeometryPrimitiveType.LineStrip:
                    PrimitiveCount = pointCount - 1;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Fills the texture type buffer (VertexPositionTexture)
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="destinationSize">Size of the destination.</param>
        /// <param name="sourceRect">The source rect.</param>
        /// <param name="primitiveType">Type of the primitive.</param>
        public override void FillTexture(List<PointF> points, Size destinationSize, Rect sourceRect, GeometryPrimitiveType primitiveType)
        {
            SetPrimitiveCount(primitiveType, points.Count);

            VertexPositionTexture[] vertex = new VertexPositionTexture[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 uv = new Vector2(sourceRect.X + (points[i].X / destinationSize.Width) * sourceRect.Width,
                                         sourceRect.Y + (points[i].Y / destinationSize.Height) * sourceRect.Height);
                vertex[i] = new VertexPositionTexture(new Vector3(points[i].X, points[i].Y, 0), uv);
            }

            VertexBuffer = new VertexBuffer(MonoGameRenderer.GraphicsDevice, VertexPositionTexture.VertexDeclaration, vertex.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData<VertexPositionTexture>(vertex);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            if (VertexBuffer != null && !VertexBuffer.IsDisposed)
            {
                VertexBuffer.Dispose();
            }            
        }
    }
}
