using EmptyKeys.UserInterface.Media;

namespace EmptyKeys.UserInterface.Renderers
{
    /// <summary>
    /// Implements abstract Renderer
    /// </summary>
    public abstract class Renderer
    {
        /// <summary>
        /// Gets or sets the width of the native screen.
        /// </summary>
        /// <value>
        /// The width of the native screen.
        /// </value>
        public float NativeScreenWidth
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets or sets the height of the native screen.
        /// </summary>
        /// <value>
        /// The height of the native screen.
        /// </value>
        public float NativeScreenHeight
        {
            get;
            protected set;
        }

        /// <summary>
        /// Gets a value indicating whether is full screen.
        /// </summary>
        /// <value>
        /// <c>true</c> if is full screen; otherwise, <c>false</c>.
        /// </value>
        public abstract bool IsFullScreen
        {
            get;
        }

        /// <summary>
        /// Begins the rendering
        /// </summary>
        public abstract void Begin();

        /// <summary>
        /// Begins the rendering with custom effect
        /// </summary>
        /// <param name="effect">The effect.</param>
        public abstract void Begin(EffectBase effect);

        /// <summary>
        /// Ends rendering
        /// </summary>
        public abstract void End(bool endEffect = false);

        /// <summary>
        /// Begins the clipped rendering
        /// </summary>
        /// <param name="clipRect">The clip rect.</param>
        public abstract void BeginClipped(Rect clipRect);

        /// <summary>
        /// Begins the clipped rendering with custom effect
        /// </summary>
        /// <param name="clipRect">The clip rect.</param>
        /// <param name="effect">The effect.</param>
        public abstract void BeginClipped(Rect clipRect, EffectBase effect);

        /// <summary>
        /// Ends the clipped rendering
        /// </summary>
        public abstract void EndClipped(bool endEffect = false);

        /// <summary>
        /// Determines whether the specified rectangle is outside of clip bounds
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="renderSize">Size of the render.</param>
        /// <returns></returns>
        public abstract bool IsClipped(PointF position, Size renderSize);

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
        public abstract void DrawText(FontBase font, string text, PointF position, Size renderSize, ColorW color, PointF scale, float depth);

        /// <summary>
        /// Draws the specified texture.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="position">The position.</param>
        /// <param name="renderSize">Size of the render.</param>
        /// <param name="color">The color.</param>
        /// <param name="centerOrigin">if set to <c>true</c> [center origin].</param>
        public abstract void Draw(TextureBase texture, PointF position, Size renderSize, ColorW color, bool centerOrigin);

        /// <summary>
        /// Draws the specified texture.
        /// </summary>
        /// <param name="texture">The texture.</param>
        /// <param name="position">The position.</param>
        /// <param name="renderSize">Size of the render.</param>
        /// <param name="color">The color.</param>
        /// <param name="source">The source.</param>
        /// <param name="centerOrigin">if set to <c>true</c> [center origin].</param>
        public abstract void Draw(TextureBase texture, PointF position, Size renderSize, ColorW color, Rect source, bool centerOrigin);

        /// <summary>
        /// Gets the viewport.
        /// </summary>
        /// <returns></returns>
        public abstract Rect GetViewport();

        /// <summary>
        /// Creates the texture.
        /// </summary>
        /// <param name="nativeTexture">The native texture.</param>
        /// <returns></returns>
        public abstract TextureBase CreateTexture(object nativeTexture);

        /// <summary>
        /// Creates the texture.
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="mipmap">if set to <c>true</c> [mipmap].</param>
        /// <param name="dynamic">if set to <c>true</c> [dynamic].</param>
        /// <returns></returns>
        public abstract TextureBase CreateTexture(int width, int height, bool mipmap, bool dynamic);

        /// <summary>
        /// Creates the geometry buffer.
        /// </summary>
        /// <returns></returns>
        public abstract GeometryBuffer CreateGeometryBuffer();

        /// <summary>
        /// Draws the color of the geometry.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="position">The position.</param>
        /// <param name="color">The color.</param>
        /// <param name="opacity">The opacity.</param>
        /// <param name="depth">The depth.</param>
        public abstract void DrawGeometryColor(GeometryBuffer buffer, PointF position, ColorW color, float opacity, float depth);

        /// <summary>
        /// Draws the geometry texture.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="position">The position.</param>
        /// <param name="texture">The texture.</param>
        /// <param name="opacity">The opacity.</param>
        /// <param name="depth">The depth.</param>
        public abstract void DrawGeometryTexture(GeometryBuffer buffer, PointF position, TextureBase texture, float opacity, float depth);

        /// <summary>
        /// Creates the font.
        /// </summary>
        /// <param name="nativeFont">The native font.</param>
        /// <returns></returns>
        public abstract FontBase CreateFont(object nativeFont);

        /// <summary>
        /// Resets the size of the native. Sets NativeScreenWidth and NativeScreenHeight based on active back buffer
        /// </summary>
        public abstract void ResetNativeSize();

        /// <summary>
        /// Creates the effect.
        /// </summary>
        /// <param name="nativeEffect">The native effect.</param>
        /// <returns></returns>
        public abstract EffectBase CreateEffect(object nativeEffect);

        /// <summary>
        /// Gets the SDF font effect.
        /// </summary>
        /// <returns></returns>
        public abstract EffectBase GetSDFFontEffect();
    }
}
