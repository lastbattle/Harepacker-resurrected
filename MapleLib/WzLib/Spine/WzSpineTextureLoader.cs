/*
 * Copyright (c) 2018~2020, LastBattle https://github.com/lastbattle
 * Copyright (c) 2010~2013, haha01haha http://forum.ragezone.com/f701/release-universal-harepacker-version-892005/

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MapleLib.WzLib.Spine
{
    public class WzSpineTextureLoader : TextureLoader
    {
        public WzObject ParentNode { get; private set; }
        private readonly GraphicsDevice graphicsDevice;


        public WzSpineTextureLoader(WzObject ParentNode, GraphicsDevice graphicsDevice)
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
            if (imageChild is WzUOLProperty uolProperty)
            {
                WzObject uolLink = uolProperty.LinkValue;

                if (uolLink is WzCanvasProperty uolPropertyLink)
                {
                    canvasProperty = uolPropertyLink;
                }
                else
                {
                    // other unimplemented prop?
                }
            }
            else if (imageChild is WzCanvasProperty property)
            {
                canvasProperty = property;
            }

            if (canvasProperty != null && graphicsDevice != null)
            {
                WzCanvasProperty linkImgProperty = (WzCanvasProperty)canvasProperty.GetLinkedWzImageProperty();
                WzPngProperty pngProperty = linkImgProperty.PngProperty;

                Texture2D tex = new Texture2D(graphicsDevice, pngProperty.Width, pngProperty.Height, false, linkImgProperty.PngProperty.GetXNASurfaceFormat());

                pngProperty.ParsePng(true, tex);

                page.rendererObject = tex;
                page.width = pngProperty.Width;
                page.height = pngProperty.Height;
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
