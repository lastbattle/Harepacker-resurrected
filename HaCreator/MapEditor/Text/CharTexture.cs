using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapEditor.Text
{
    internal class CharTexture
    {
        internal Texture2D texture;
        internal int w;
        internal int h;

        internal CharTexture(Texture2D texture, int w, int h)
        {
            this.texture = texture;
            this.w = w;
            this.h = h;
        }
    }
}
