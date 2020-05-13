using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Texture2D = Microsoft.Xna.Framework.Graphics.Texture2D;

namespace MapleLib.WzLib.Spine
{
    public class SpineTextureLoader : TextureLoader
    {
        public WzObject ParentNode { get; private set; }
        private GraphicsDevice graphicsDevice;

        public SpineTextureLoader(WzObject ParentNode, GraphicsDevice graphicsDevice)
        {
            this.ParentNode = ParentNode;
            this.graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Loads spine texture from the specified WZ path
        /// </summary>
        /// <param name="page"></param>
        /// <param name="path"></param>
        public void Load(AtlasPage page, string path)
        {
            WzObject frameNode = this.ParentNode[path];
            if (frameNode == null)
                return;


            WzCanvasProperty canvasProperty = null;

            WzImageProperty imageChild = (WzImageProperty)ParentNode[path];
            if (imageChild is WzUOLProperty)
            {
                WzObject uolLink = ((WzUOLProperty)imageChild).LinkValue;

                if (uolLink is WzCanvasProperty)
                {
                    canvasProperty = (WzCanvasProperty)uolLink;
                }
                else
                {
                    // other unimplemented prop?
                }
            }
            else if (imageChild is WzCanvasProperty)
            {
                canvasProperty = (WzCanvasProperty)imageChild;
            }

            if (canvasProperty != null)
            {
                Bitmap bitmap = canvasProperty.GetLinkedWzCanvasBitmap();

                if (bitmap != null)
                {
                    Texture2D tex = new Texture2D(graphicsDevice, bitmap.Width, bitmap.Height, true, SurfaceFormat.Color);
                    BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

                    int bufferSize = data.Height * data.Stride;

                    //create data buffer 
                    byte[] bytes = new byte[bufferSize];

                    // copy bitmap data into buffer
                    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                    // copy our buffer to the texture
                    tex.SetData(bytes);

                    // unlock the bitmap data
                    bitmap.UnlockBits(data);

                    page.rendererObject = tex;
                    page.width = bitmap.Width;
                    page.height = bitmap.Height;
                }
            }
        }

        /// <summary>
        /// Unload texture
        /// </summary>
        /// <param name="texture"></param>
        public void Unload(object texture)
        {
            (texture as Texture2D)?.Dispose();
        }
    }
}
