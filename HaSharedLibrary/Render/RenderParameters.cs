using HaSharedLibrary.Render.DX;

namespace HaSharedLibrary.Render
{
    public struct RenderParameters
    {
        public int RenderWidth { get; }
        public int RenderHeight { get; }
        public float RenderObjectScaling { get; }
        public RenderResolution Resolution { get; }

        public RenderParameters(int width, int height, float renderObjectScaling, RenderResolution resolution)
        {
            RenderWidth = width;
            RenderHeight = height;
            RenderObjectScaling = renderObjectScaling;
            Resolution = resolution;
        }
    }
}
