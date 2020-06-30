using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmptyKeys.UserInterface.Media;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;
using Xenko.Rendering;
using Texture2D = Xenko.Graphics.Texture;

namespace EmptyKeys.UserInterface.Renderers
{
    public class XenkoRenderer : Renderer
    {
        private static GraphicsDeviceManager manager;
        private static GraphicsContext graphicsContext;

        /// <summary>
        /// The graphics device
        /// </summary>
        /// <value>
        /// The graphics device.
        /// </value>
        public static GraphicsDevice GraphicsDevice
        {
            get
            {
                return manager.GraphicsDevice;
            }
        }

        /// <summary>
        /// Gets or sets the graphics context.
        /// </summary>
        /// <value>
        /// The graphics context.
        /// </value>
        public static GraphicsContext GraphicsContext
        {
            get
            {
                return graphicsContext;
            }

            set
            {
                graphicsContext = value;
            }
        }

        private Matrix view = Matrix.LookAtRH(new Vector3(0.0f, 0.0f, 1.0f), Vector3.Zero, Vector3.UnitY);
        private Matrix projection;
        private Size activeViewportSize;

        private SpriteBatch spriteBatch;
        private Vector2 vecPosition;
        private Vector2 vecScale;
        private Vector2 origin;        
        private Color vecColor;
        private Rectangle testRectangle;
        private Rectangle sourceRect;
        private Rectangle currentScissorRectangle;
        private Rectangle[] clipArray;
        private Stack<Rectangle> clipRectanges;
        private Stack<EffectInstance> activeEffects;
        private EffectInstance currentActiveEffect;
        private EffectSystem effectSystem;
        private XenkoEffect sdfFontEffect;

        private bool isSpriteRenderInProgress;
        private bool isClipped;
        private Rectangle clipRectangle;        
        private MutablePipelineState geometryPipelineState;        
        private RasterizerStateDescription scissorRasterizerStateDescription;
        private RasterizerStateDescription geometryRasterizerStateDescription;

        /// <summary>
        /// Gets a value indicating whether is full screen.
        /// </summary>
        /// <value>
        /// <c>true</c> if is full screen; otherwise, <c>false</c>.
        /// </value>
        public override bool IsFullScreen
        {
            get { return manager.IsFullScreen; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XenkoRenderer"/> class.
        /// </summary>
        /// <param name="graphicsDeviceManager">The graphics device manager.</param>
        public XenkoRenderer(GraphicsDeviceManager graphicsDeviceManager, EffectSystem effectSystem)
            : base()
        {
            manager = graphicsDeviceManager;
            this.effectSystem = effectSystem;
            spriteBatch = new SpriteBatch(manager.GraphicsDevice);
            clipRectanges = new Stack<Rectangle>();
            activeEffects = new Stack<EffectInstance>();

            scissorRasterizerStateDescription = RasterizerStates.CullNone;
            scissorRasterizerStateDescription.ScissorTestEnable = true; // enables the scissor test            

            geometryRasterizerStateDescription = RasterizerStates.CullNone;            
            //geometryRasterizerStateDescription.FillMode = FillMode.Wireframe;            
            geometryPipelineState = new MutablePipelineState(manager.GraphicsDevice);
            geometryPipelineState.State.DepthStencilState = DepthStencilStates.None;

            clipArray = new Rectangle[1];
        }

        /// <summary>
        /// Begins the rendering
        /// </summary>
        public override void Begin()
        {
            Begin(null);
        }

        /// <summary>
        /// Begins the rendering with custom effect
        /// </summary>
        /// <param name="effect"></param>
        public override void Begin(EffectBase effect)
        {
            isClipped = false;
            isSpriteRenderInProgress = true;
            UpdateCurrentEffect(effect);

            if (clipRectanges.Count == 0)
            {
                spriteBatch.Begin(graphicsContext, SpriteSortMode.Deferred, depthStencilState: DepthStencilStates.None, effect: currentActiveEffect);
            }
            else
            {
                Rectangle previousClip = clipRectanges.Pop();
                BeginClipped(previousClip);
            }
        }

        private void UpdateCurrentEffect(EffectBase effect)
        {
            EffectInstance effectInstance = effect != null ? effect.GetNativeEffect() as EffectInstance : null;
            if (effectInstance != null)
            {
                if (currentActiveEffect != null)
                {
                    activeEffects.Push(currentActiveEffect);
                }

                currentActiveEffect = effectInstance;
            }                       

            if (currentActiveEffect == null && activeEffects.Count > 0)
            {
                currentActiveEffect = activeEffects.Pop();
            }            
        }

        /// <summary>
        /// Ends rendering
        /// </summary>
        public override void End(bool endEffect = false)
        {
            isClipped = false;
            if (endEffect)
            {
                currentActiveEffect = null;
            }
            else
            {
                activeEffects.Push(currentActiveEffect);
                currentActiveEffect = null;
            }
                        
            spriteBatch.End();
            isSpriteRenderInProgress = false;
        }

        /// <summary>
        /// Begins the clipped rendering
        /// </summary>
        /// <param name="clipRect">The clip rect.</param>
        public override void BeginClipped(Rect clipRect)
        {            
            BeginClipped(clipRect, null);
        }

        /// <summary>
        /// Begins the clipped rendering with custom effect
        /// </summary>
        /// <param name="clipRect">The clip rect.</param>
        /// <param name="effect">The effect.</param>
        public override void BeginClipped(Rect clipRect, EffectBase effect)
        {
            clipRectangle.X = (int)clipRect.X;
            clipRectangle.Y = (int)clipRect.Y;
            clipRectangle.Width = (int)clipRect.Width;
            clipRectangle.Height = (int)clipRect.Height;

            UpdateCurrentEffect(effect);

            BeginClipped(clipRectangle);
        }

        private void BeginClipped(Rectangle clipRect)
        {
            isClipped = true;
            isSpriteRenderInProgress = true;

            if (clipRectanges.Count > 0)
            {
                Rectangle previousClip = clipRectanges.Pop();
                if (previousClip.Intersects(clipRect))
                {
                    clipRect = Rectangle.Intersect(previousClip, clipRect);
                }
                else
                {
                    clipRect = previousClip;
                }

                clipRectanges.Push(previousClip);
            }

            clipArray[0] = clipRect;
            graphicsContext.CommandList.SetScissorRectangles(1, clipArray);            
            
            currentScissorRectangle = clipRect;
            spriteBatch.Begin(graphicsContext, SpriteSortMode.Deferred, depthStencilState: DepthStencilStates.None, rasterizerState: scissorRasterizerStateDescription, effect: currentActiveEffect);
            clipRectanges.Push(clipRect);
        }

        /// <summary>
        /// Ends the clipped rendering
        /// </summary>
        public override void EndClipped(bool endEffect = false)
        {
            isClipped = false;
            isSpriteRenderInProgress = false;
            if (endEffect)
            {
                currentActiveEffect = null;
            }
            else
            {
                activeEffects.Push(currentActiveEffect);
                currentActiveEffect = null;
            }

            spriteBatch.End();
            clipRectanges.Pop();
        }

        /// <summary>
        /// Draws the text.
        /// </summary>
        /// <param name="font">The font.</param>
        /// <param name="text">The text.</param>
        /// <param name="position">The position.</param>
        /// <param name="renderSize">Size of the render.</param>
        /// <param name="color">The color.</param>
        /// <param name="scale">The scale.</param>
        /// <param name="depth">The depth.</param>
        public override void DrawText(FontBase font, string text, PointF position, Size renderSize, ColorW color, PointF scale, float depth)
        {
            if (isClipped)
            {
                testRectangle.X = (int)position.X;
                testRectangle.Y = (int)position.Y;
                testRectangle.Width = (int)renderSize.Width;
                testRectangle.Height = (int)renderSize.Height;

                if (!currentScissorRectangle.Intersects(testRectangle))
                {
                    return;
                }
            }

            vecPosition.X = position.X;
            vecPosition.Y = position.Y;
            vecScale.X = scale.X;
            vecScale.Y = scale.Y;
            vecColor.A = color.A;
            vecColor.R = color.R;
            vecColor.G = color.G;
            vecColor.B = color.B;
            SpriteFont native = font.GetNativeFont() as SpriteFont;
            spriteBatch.DrawString(native, text, vecPosition, vecColor);
        }

        /// <summary>
        /// Draws the specified texture.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="position">The position.</param>
        /// <param name="renderSize">Size of the render.</param>
        /// <param name="color">The color.</param>
        /// <param name="centerOrigin">if set to <c>true</c> [center origin].</param>
        public override void Draw(TextureBase texture, PointF position, Size renderSize, ColorW color, bool centerOrigin)
        {
            testRectangle.X = (int)position.X;
            testRectangle.Y = (int)position.Y;
            testRectangle.Width = (int)renderSize.Width;
            testRectangle.Height = (int)renderSize.Height;
            if (centerOrigin)
            {
                testRectangle.X -= testRectangle.Width / 2;
                testRectangle.Y -= testRectangle.Height / 2;
            }

            if (isClipped && !currentScissorRectangle.Intersects(testRectangle))
            {
                return;
            }

            vecColor.A = color.A;
            vecColor.R = color.R;
            vecColor.G = color.G;
            vecColor.B = color.B;
            Texture2D native = texture.GetNativeTexture() as Texture2D;
            spriteBatch.Draw(native, testRectangle, vecColor);
        }

        /// <summary>
        /// Draws the specified texture.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="position">The position.</param>
        /// <param name="renderSize">Size of the render.</param>
        /// <param name="color">The color.</param>
        /// <param name="source">The source.</param>
        /// <param name="centerOrigin">if set to <c>true</c> [center origin].</param>
        public override void Draw(TextureBase texture, PointF position, Size renderSize, ColorW color, Rect source, bool centerOrigin)
        {
            testRectangle.X = (int)position.X;
            testRectangle.Y = (int)position.Y;
            testRectangle.Width = (int)renderSize.Width;
            testRectangle.Height = (int)renderSize.Height;
            if (isClipped && !currentScissorRectangle.Intersects(testRectangle))
            {
                return;
            }

            sourceRect.X = (int)source.X;
            sourceRect.Y = (int)source.Y;
            sourceRect.Width = (int)source.Width;
            sourceRect.Height = (int)source.Height;
            vecColor.A = color.A;
            vecColor.R = color.R;
            vecColor.G = color.G;
            vecColor.B = color.B;
            if (centerOrigin)
            {
                origin.X = testRectangle.Width / 2f;
                origin.Y = testRectangle.Height / 2f;
            }

            Texture2D native = texture.GetNativeTexture() as Texture2D;
            spriteBatch.Draw(native, testRectangle, sourceRect, vecColor, 0, origin);
        }

        /// <summary>
        /// Gets the viewport.
        /// </summary>
        /// <returns></returns>
        public override Rect GetViewport()
        {
            Viewport viewport = graphicsContext.CommandList.Viewport;
            return new Rect(viewport.X, viewport.Y, viewport.Width, viewport.Height);
        }

        /// <summary>
        /// Creates the texture.
        /// </summary>
        /// <param name="nativeTexture">The native texture.</param>
        /// <returns></returns>
        public override TextureBase CreateTexture(object nativeTexture)
        {
            if (nativeTexture == null)
            {
                return null;
            }

            return new XenkoTexture(nativeTexture);
        }

        /// <summary>
        /// Creates the texture.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="mipmap">if set to <c>true</c> [mipmap].</param>
        /// <param name="dynamic">if set to <c>true</c> [dynamic].</param>
        /// <returns></returns>
        public override TextureBase CreateTexture(int width, int height, bool mipmap, bool dynamic)
        {
            if (width == 0 || height == 0)
            {
                return null;
            }

            Texture2D native = null;
            if (dynamic)
            {
                native = Texture2D.New2D(GraphicsDevice, width, height, PixelFormat.R8G8B8A8_UNorm, usage: GraphicsResourceUsage.Dynamic);
            }
            else
            {
                native = Texture2D.New2D(GraphicsDevice, width, height, PixelFormat.R8G8B8A8_UNorm);
            }

            XenkoTexture texture = new XenkoTexture(native);
            return texture;
        }

        /// <summary>
        /// Creates the geometry buffer.
        /// </summary>
        /// <returns></returns>
        public override GeometryBuffer CreateGeometryBuffer()
        {
            return new XenkoGeometryBuffer();
        }

        /// <summary>
        /// Draws the color of the geometry.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="position">The position.</param>
        /// <param name="color">The color.</param>
        /// <param name="opacity">The opacity.</param>
        /// <param name="depth">The depth.</param>
        public override void DrawGeometryColor(GeometryBuffer buffer, PointF position, ColorW color, float opacity, float depth)
        {
            XenkoGeometryBuffer xenkoBuffer = buffer as XenkoGeometryBuffer;

            Color4 nativeColor = new Color4(color.PackedValue) * opacity;
            xenkoBuffer.EffectInstance.Parameters.Set(SpriteEffectKeys.Color, nativeColor);
            xenkoBuffer.EffectInstance.Parameters.Set(TexturingKeys.Texture0, GraphicsDevice.GetSharedWhiteTexture());
            DrawGeometry(buffer, position, depth);
        }

        /// <summary>
        /// Draws the geometry texture.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="position">The position.</param>
        /// <param name="texture">The texture.</param>
        /// <param name="opacity">The opacity.</param>
        /// <param name="depth">The depth.</param>
        public override void DrawGeometryTexture(GeometryBuffer buffer, PointF position, TextureBase texture, float opacity, float depth)
        {
            XenkoGeometryBuffer paradoxBuffer = buffer as XenkoGeometryBuffer;
            Texture2D nativeTexture = texture.GetNativeTexture() as Texture2D;
            paradoxBuffer.EffectInstance.Parameters.Set(SpriteEffectKeys.Color, Color.White * opacity);
            paradoxBuffer.EffectInstance.Parameters.Set(TexturingKeys.Texture0, nativeTexture);
            DrawGeometry(buffer, position, depth);
        }

        private void UpdateProjection(CommandList commandList)
        {
            bool sameViewport = activeViewportSize.Width == commandList.Viewport.Width && activeViewportSize.Height == commandList.Viewport.Height;
            if (!sameViewport)
            {                
                activeViewportSize = new Size(commandList.Viewport.Width, commandList.Viewport.Height);
                projection = Matrix.OrthoOffCenterRH(0, commandList.Viewport.Width, commandList.Viewport.Height, 0, 1.0f, 1000.0f);
            }
        }

        private void DrawGeometry(GeometryBuffer buffer, PointF position, float depth)
        {
            if (isSpriteRenderInProgress)
            {
                spriteBatch.End();
            }

            Matrix world = Matrix.Translation(position.X, position.Y, 0);            

            Matrix worldView;
            Matrix.MultiplyTo(ref world, ref view, out worldView);
            
            Matrix worldViewProjection;
            UpdateProjection(graphicsContext.CommandList);
            Matrix.MultiplyTo(ref worldView, ref projection, out worldViewProjection);            

            XenkoGeometryBuffer paradoxBuffer = buffer as XenkoGeometryBuffer;            
            paradoxBuffer.EffectInstance.Parameters.Set(SpriteBaseKeys.MatrixTransform, worldViewProjection);            
            
            if (isClipped)
            {
                geometryPipelineState.State.RasterizerState = scissorRasterizerStateDescription;
            }
            else
            {
                geometryPipelineState.State.RasterizerState = geometryRasterizerStateDescription;
            }

            switch (buffer.PrimitiveType)
            {
                case GeometryPrimitiveType.TriangleList:
                    geometryPipelineState.State.PrimitiveType = PrimitiveType.TriangleList;
                    break;
                case GeometryPrimitiveType.TriangleStrip:
                    geometryPipelineState.State.PrimitiveType = PrimitiveType.TriangleStrip;
                    break;
                case GeometryPrimitiveType.LineList:
                    geometryPipelineState.State.PrimitiveType = PrimitiveType.LineList;
                    break;
                case GeometryPrimitiveType.LineStrip:
                    geometryPipelineState.State.PrimitiveType = PrimitiveType.LineStrip;
                    break;
                default:
                    break;
            }

            geometryPipelineState.State.RootSignature = paradoxBuffer.EffectInstance.RootSignature;
            geometryPipelineState.State.EffectBytecode = paradoxBuffer.EffectInstance.Effect.Bytecode;
            geometryPipelineState.State.InputElements = paradoxBuffer.InputElementDescriptions;            
            geometryPipelineState.State.Output.CaptureState(graphicsContext.CommandList);            
            geometryPipelineState.Update();
            graphicsContext.CommandList.SetPipelineState(geometryPipelineState.CurrentState);
            paradoxBuffer.EffectInstance.Apply(graphicsContext);

            graphicsContext.CommandList.SetVertexBuffer(0, paradoxBuffer.VertexBufferBinding.Buffer, 0, paradoxBuffer.VertexBufferBinding.Stride);            
            graphicsContext.CommandList.Draw(paradoxBuffer.PrimitiveCount);

            if (isSpriteRenderInProgress)
            {
                if (isClipped)
                {
                    spriteBatch.Begin(graphicsContext, SpriteSortMode.Deferred, 
                        depthStencilState: DepthStencilStates.None, rasterizerState: scissorRasterizerStateDescription, effect: currentActiveEffect);                    
                }
                else
                {
                    spriteBatch.Begin(graphicsContext, SpriteSortMode.Deferred, depthStencilState: DepthStencilStates.None, effect: currentActiveEffect);
                }
            }
        }

        /// <summary>
        /// Creates the font.
        /// </summary>
        /// <param name="nativeFont">The native font.</param>
        /// <returns></returns>
        public override FontBase CreateFont(object nativeFont)
        {
            return new XenkoFont(nativeFont);
        }

        /// <summary>
        /// Resets the size of the native. Sets NativeScreenWidth and NativeScreenHeight based on active back buffer
        /// </summary>
        public override void ResetNativeSize()
        {
        }

        /// <summary>
        /// Determines whether the specified rectangle is outside of clip bounds
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="renderSize">Size of the render.</param>
        /// <returns></returns>
        public override bool IsClipped(PointF position, Size renderSize)
        {
            if (isClipped)
            {
                testRectangle.X = (int)position.X;
                testRectangle.Y = (int)position.Y;
                testRectangle.Width = (int)renderSize.Width;
                testRectangle.Height = (int)renderSize.Height;

                if (!currentScissorRectangle.Intersects(testRectangle))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates the effect.
        /// </summary>
        /// <param name="nativeEffect">The native effect.</param>
        /// <returns></returns>
        public override EffectBase CreateEffect(object nativeEffect)
        {
            return new XenkoEffect(nativeEffect, null);
        }

        /// <summary>
        /// Gets the SDF font effect.
        /// </summary>
        /// <returns></returns>
        public override EffectBase GetSDFFontEffect()
        {
            if (sdfFontEffect == null)
            {                
                Effect effect = effectSystem.LoadEffect("SDFFontShader").WaitForResult();
                if (effect != null)
                {
                    ParameterCollection parameters = new ParameterCollection();
                    parameters.Set<Color4>(SDFFontShaderKeys.TintColor, Color4.White);
                    parameters.Set<Color4>(SDFFontShaderKeys.BorderColor, Color4.Black);
                    parameters.Set<float>(SDFFontShaderKeys.BorderThickness, 0f);

                    sdfFontEffect = new XenkoEffect(effect, parameters);
                }
            }

            return sdfFontEffect;
        }
    }
}
