using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.DX
{
    public class MapItem
    {
        private List<IDXObject> frames;
        private int currFrame = 0;
        private int lastFrameSwitchTime = 0;

        protected bool flip;
        protected bool notAnimated;
        private IDXObject frame0;

        public MapItem(List<IDXObject> frames, bool flip)
        {
            this.frames = frames;
            notAnimated = false;
            this.flip = flip;
        }

        public MapItem(IDXObject frame0, bool flip)
        {
            this.frame0 = frame0;
            notAnimated = true;
            this.flip = flip;
        }

        protected IDXObject GetCurrFrame(int TickCount)
        {
            if (notAnimated) 
                return frame0;
            else
            {
                if (TickCount - lastFrameSwitchTime > frames[currFrame].Delay)
                { //advance frame
                    currFrame++;
                    if (currFrame == frames.Count) currFrame = 0;
                    lastFrameSwitchTime = TickCount;
                }
                return frames[currFrame];
            }
        }

        /// <summary>
        /// Draw
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="mapShiftX"></param>
        /// <param name="mapShiftY"></param>
        /// <param name="centerX"></param>
        /// <param name="centerY"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="TickCount">Ticks since system startup</param>
        public virtual void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, 
            int width, int height, float RenderObjectScaling, MapRenderResolution mapRenderResolution,
            int TickCount)
        {
            if (notAnimated)
            {
                if (frame0.X - mapShiftX + frame0.Width > 0 && frame0.Y - mapShiftY + frame0.Height > 0 && frame0.X - mapShiftX < width && frame0.Y - mapShiftY < height)
                    frame0.DrawObject(sprite, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, flip);
            }
            else
                GetCurrFrame(TickCount).DrawObject(sprite, skeletonMeshRenderer, gameTime, mapShiftX, mapShiftY, flip);
        }
    }
}
