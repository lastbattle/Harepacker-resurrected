using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using Spine41;

namespace HaSharedLibrary.Render.DX
{
    internal sealed class Spine41TextureLoader : TextureLoader
    {
        private readonly WzObject parentNode;
        private readonly GraphicsDevice graphicsDevice;

        public Spine41TextureLoader(WzObject parentNode, GraphicsDevice graphicsDevice)
        {
            this.parentNode = parentNode;
            this.graphicsDevice = graphicsDevice;
        }

        public void Load(AtlasPage page, string path)
        {
            if (parentNode == null || graphicsDevice == null)
                return;

            string pageName = System.IO.Path.GetFileName(path);
            WzImageProperty imageChild = parentNode[pageName] as WzImageProperty;
            if (imageChild is WzUOLProperty uolProperty)
                imageChild = uolProperty.LinkValue as WzImageProperty;

            if (imageChild is not WzCanvasProperty canvasProperty)
                return;

            WzCanvasProperty linkedCanvas = canvasProperty.GetLinkedWzImageProperty() as WzCanvasProperty ?? canvasProperty;
            WzPngProperty pngProperty = linkedCanvas.PngProperty;

            Texture2D texture = new Texture2D(
                graphicsDevice,
                pngProperty.Width,
                pngProperty.Height,
                false,
                WzPngFormatExtensions.GetXNASurfaceFormat(pngProperty.Format));

            pngProperty.ParsePng(true, texture);

            page.rendererObject = texture;
            page.width = pngProperty.Width;
            page.height = pngProperty.Height;
        }

        public void Unload(object texture)
        {
            (texture as Texture2D)?.Dispose();
        }
    }
}
