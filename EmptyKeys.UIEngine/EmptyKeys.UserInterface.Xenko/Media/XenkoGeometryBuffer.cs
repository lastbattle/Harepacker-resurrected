using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmptyKeys.UserInterface.Renderers;
using Xenko.Core;
using Xenko.Core.Mathematics;
using Xenko.Graphics;
using Xenko.Rendering;
using VertexBuffer = Xenko.Graphics.Buffer;

namespace EmptyKeys.UserInterface.Media
{
    public class XenkoGeometryBuffer : GeometryBuffer
    {
        private readonly EffectInstance effect;                  

        /// <summary>
        /// Gets the effect.
        /// </summary>
        /// <value>
        /// The effect.
        /// </value>
        public EffectInstance EffectInstance
        {
            get
            {
                return effect;
            }
        }

        /// <summary>
        /// Gets the vertex buffer.
        /// </summary>
        /// <value>
        /// The vertex buffer.
        /// </value>
        public VertexBuffer VertexBuffer { get; private set; }

        /// <summary>
        /// Gets the vertex buffer binding.
        /// </summary>
        /// <value>
        /// The vertex buffer binding.
        /// </value>
        public VertexBufferBinding VertexBufferBinding { get; private set; }

        /// <summary>
        /// Gets the input element descriptions.
        /// </summary>
        /// <value>
        /// The input element descriptions.
        /// </value>
        public InputElementDescription[] InputElementDescriptions { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="XenkoGeometryBuffer"/> class.
        /// </summary>
        public XenkoGeometryBuffer()
            : base()
        {
            effect = new EffectInstance(new Effect(XenkoRenderer.GraphicsDevice, SpriteEffect.Bytecode));
            effect.UpdateEffect(XenkoRenderer.GraphicsDevice);
        }

        /// <summary>
        /// Fills the color type buffer (VertexPositionColor)
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="primitiveType">Type of the primitive.</param>
        public override void FillColor(List<PointF> points, GeometryPrimitiveType primitiveType)
        {
            SetPrimitiveCount(primitiveType, points.Count);

            VertexPositionNormalTexture[] vertex = new VertexPositionNormalTexture[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                vertex[i] = new VertexPositionNormalTexture(new Vector3(points[i].X, points[i].Y, 0), new Vector3(0, 0, 1), Vector2.Zero);
            }

            VertexBuffer = VertexBuffer.Vertex.New(XenkoRenderer.GraphicsDevice, vertex);
            VertexBuffer.Reload = (graphicsResource) => ((VertexBuffer)graphicsResource).Recreate(vertex);
            VertexBufferBinding = new VertexBufferBinding(VertexBuffer, VertexPositionNormalTexture.Layout, vertex.Length, VertexPositionNormalTexture.Size);
            InputElementDescriptions = VertexBufferBinding.Declaration.CreateInputElements();
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
                    PrimitiveCount = pointCount;
                    break;
                case GeometryPrimitiveType.LineList:
                    PrimitiveCount = pointCount / 2;
                    break;
                case GeometryPrimitiveType.LineStrip:
                    PrimitiveCount = pointCount;
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

            VertexPositionNormalTexture[] vertex = new VertexPositionNormalTexture[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 uv = new Vector2(sourceRect.X + (points[i].X / destinationSize.Width) * sourceRect.Width,
                                         sourceRect.Y + (points[i].Y / destinationSize.Height) * sourceRect.Height);
                vertex[i] = new VertexPositionNormalTexture(new Vector3(points[i].X, points[i].Y, 0), new Vector3(0,0,1), uv);
            }

            VertexBuffer = VertexBuffer.Vertex.New(XenkoRenderer.GraphicsDevice, vertex);
            VertexBuffer.Reload = (graphicsResource) => ((VertexBuffer)graphicsResource).Recreate(vertex);
            VertexBufferBinding = new VertexBufferBinding(VertexBuffer, VertexPositionNormalTexture.Layout, vertex.Length, VertexPositionNormalTexture.Size);
            InputElementDescriptions = VertexBufferBinding.Declaration.CreateInputElements();
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

            if (effect != null && !effect.IsDisposed)
            {
                effect.Dispose();
            }
        }
    }
}
