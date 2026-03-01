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
            int mapShiftX, int mapShiftY, bool flip, ReflectionDrawableBoundary drawReflectionInfo);

        void DrawBackground(Microsoft.Xna.Framework.Graphics.SpriteBatch sprite, SkeletonMeshRenderer meshRenderer, GameTime gameTime, 
            int x, int y, Color color, bool flip, ReflectionDrawableBoundary drawReflectionInfo);

        /// <summary>
        /// The delay of the current frame
        /// </summary>
        int Delay { get; }

        /// <summary>
        /// The origin X
        /// </summary>
        int X { get; }

        /// <summary>
        /// The origin Y
        /// </summary>
        int Y { get; }

        /// <summary>
        /// The Width of the Texture2D
        /// </summary>
        int Width { get; }

        /// <summary>
        /// The height of the Texture2D
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Tag - For storing anything
        /// </summary>
        object Tag { get; set; }
    }
}
