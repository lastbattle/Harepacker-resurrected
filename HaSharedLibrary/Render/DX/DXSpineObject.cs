using MapleLib.WzLib.Spine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace HaSharedLibrary.Render.DX
{
    public class DXSpineObject : IDXObject
    {
        private readonly WzSpineObject spineObject;
        private readonly int _x;
        private readonly int _y;
        private System.Drawing.PointF _origin;

        private readonly int delay;

        private object _Tag;

        public DXSpineObject(WzSpineObject spineObject, int x, int y, System.Drawing.PointF _origin, int delay = 0)
        {
            this.spineObject = spineObject;
            this._x = x;
            this._y = y;
            this._origin = _origin;
            this.delay = delay;

            spineObject.bounds.Update(spineObject.skeleton, true);
        }

        /// <summary>
        /// Draw spine objects
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="flip"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawObject(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, bool flip)
        {
            spineObject.state.Update(gameTime.ElapsedGameTime.Milliseconds / 1000f);
            spineObject.state.Apply(spineObject.skeleton);

            spineObject.skeleton.FlipX = flip;
            spineObject.skeleton.X = X - mapShiftX;
            spineObject.skeleton.Y = Y - mapShiftY;
            spineObject.skeleton.UpdateWorldTransform();

            skeletonMeshRenderer.PremultipliedAlpha = spineObject.spineAnimationItem.PremultipliedAlpha;

            skeletonMeshRenderer.Begin();
            skeletonMeshRenderer.Draw(spineObject.skeleton);
            skeletonMeshRenderer.End();
        }

        /// <summary>
        /// Draw spine background 
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="color"></param>
        /// <param name="flip"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawBackground(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int x, int y, Color color, bool flip)
        {
            spineObject.state.Update(gameTime.ElapsedGameTime.Milliseconds / 1000f);
            spineObject.state.Apply(spineObject.skeleton);

            if (spineObject.skeleton.FlipX != flip || spineObject.skeleton.X != x || spineObject.skeleton.Y != y) // reduce the number of updates
            {
                spineObject.skeleton.FlipX = flip;
                spineObject.skeleton.X = x; //x + (Width);
                spineObject.skeleton.Y = y;//y + (Height / 2);
                spineObject.skeleton.UpdateWorldTransform();
            }

            skeletonMeshRenderer.PremultipliedAlpha = spineObject.spineAnimationItem.PremultipliedAlpha;

            skeletonMeshRenderer.Begin();
            skeletonMeshRenderer.Draw(spineObject.skeleton);
            skeletonMeshRenderer.End();
        }

        public int Delay
        {
            get { return delay; }
        }

        public int X { get { return _x; } }
        public int Y { get { return _y; } }

        public int Width { get { return (int)spineObject.skeleton.Data.Width; } }
        public int Height { get { return (int)spineObject.skeleton.Data.Height; } }
        public object Tag { get { return _Tag; } set { this._Tag = value; } }
    }
}
