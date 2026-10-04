using Microsoft.Xna.Framework.Graphics;
using Spine41;
using System.Runtime.CompilerServices;

namespace HaSharedLibrary.Render.DX
{
    internal static class Spine41RendererCache
    {
        private sealed class RendererHolder
        {
            public SkeletonRenderer Renderer;
        }

        private static readonly ConditionalWeakTable<GraphicsDevice, RendererHolder> Renderers = new();

        public static SkeletonRenderer Get(GraphicsDevice graphicsDevice)
        {
            RendererHolder holder = Renderers.GetOrCreateValue(graphicsDevice);
            holder.Renderer ??= new SkeletonRenderer(graphicsDevice);
            return holder.Renderer;
        }
    }
}
