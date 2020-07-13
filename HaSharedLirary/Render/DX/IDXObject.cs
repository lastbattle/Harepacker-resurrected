using Microsoft.Xna.Framework;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaSharedLibrary.Render.DX
{
    public interface IDXObject
    {
        void DrawObject(Microsoft.Xna.Framework.Graphics.SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, bool flip);

        void DrawBackground(Microsoft.Xna.Framework.Graphics.SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, 
            int x, int y, Color color, bool flip);

        int Delay { get; }

        int X { get; }

        int Y { get; }

        int Width { get; }
        int Height { get; }
    }
}
