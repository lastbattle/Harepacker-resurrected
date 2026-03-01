using HaSharedLibrary.Render;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Render context containing common parameters passed to all render operations.
    /// Avoids passing many parameters to each render method.
    /// </summary>
    public readonly struct RenderContext
    {
        public readonly SpriteBatch SpriteBatch;
        public readonly SkeletonMeshRenderer SkeletonMeshRenderer;
        public readonly GameTime GameTime;
        public readonly int MapShiftX;
        public readonly int MapShiftY;
        public readonly int MapCenterX;
        public readonly int MapCenterY;
        public readonly Vector2 ShiftCenter;
        public readonly RenderParameters RenderParams;
        public readonly int TickCount;
        public readonly Texture2D DebugTexture;

        public RenderContext(
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonMeshRenderer,
            GameTime gameTime,
            int mapShiftX,
            int mapShiftY,
            int mapCenterX,
            int mapCenterY,
            RenderParameters renderParams,
            int tickCount,
            Texture2D debugTexture = null)
        {
            SpriteBatch = spriteBatch;
            SkeletonMeshRenderer = skeletonMeshRenderer;
            GameTime = gameTime;
            MapShiftX = mapShiftX;
            MapShiftY = mapShiftY;
            MapCenterX = mapCenterX;
            MapCenterY = mapCenterY;
            ShiftCenter = new Vector2(mapShiftX - mapCenterX, mapShiftY - mapCenterY);
            RenderParams = renderParams;
            TickCount = tickCount;
            DebugTexture = debugTexture;
        }

        /// <summary>
        /// The width of the render area
        /// </summary>
        public int RenderWidth => RenderParams.RenderWidth;

        /// <summary>
        /// The height of the render area
        /// </summary>
        public int RenderHeight => RenderParams.RenderHeight;
    }

    /// <summary>
    /// Interface for renderable layers in the MapSimulator.
    /// Provides consistent rendering contract for all visual elements.
    /// </summary>
    public interface IRenderLayer
    {
        /// <summary>
        /// The priority/order of this layer (lower = drawn first/behind)
        /// </summary>
        int RenderOrder { get; }

        /// <summary>
        /// Whether this layer is currently visible
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Draw this layer
        /// </summary>
        /// <param name="context">The render context containing all common render parameters</param>
        void Draw(in RenderContext context);
    }

    /// <summary>
    /// Interface for layers that support debug overlay rendering
    /// </summary>
    public interface IDebugRenderable
    {
        /// <summary>
        /// Draw debug information for this layer
        /// </summary>
        /// <param name="context">The render context</param>
        /// <param name="font">Font for debug text</param>
        void DrawDebug(in RenderContext context, SpriteFont font);
    }

    /// <summary>
    /// Standard render layer ordering constants
    /// </summary>
    public static class RenderLayerOrder
    {
        public const int BackgroundBack = 0;
        public const int MapObjects = 100;
        public const int Mobs = 200;
        public const int Player = 300;
        public const int Drops = 400;
        public const int Portals = 500;
        public const int Reactors = 600;
        public const int NPCs = 700;
        public const int Transportation = 800;
        public const int BackgroundFront = 900;
        public const int Borders = 1000;
        public const int DebugOverlays = 1100;
        public const int ScreenEffects = 1200;
        public const int LimitedView = 1300;
        public const int Tooltips = 1400;
        public const int UI = 1500;
        public const int Chat = 1600;
        public const int FadeOverlay = 1700;
        public const int Cursor = 1800;
    }
}
