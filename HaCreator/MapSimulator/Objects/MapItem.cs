using HaCreator.MapSimulator.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.Objects
{
    public class MapItem
    {
        private List<IDXObject> frames;
        private int currFrame = 0;
        private int lastFrameSwitchTime = 0;

        protected bool flip;
        protected bool notAnimated;
        private IDXObject frame0;

        /// <summary>
        /// Creates an instance of MapItem
        /// </summary>
        /// <param name="frames"></param>
        /// <param name="flip"></param>
        public MapItem(List<IDXObject> frames, bool flip)
        {
            if (frames.Count == 1) // not animated if its just 1 frame
            {
                this.frame0 = frames[0];
                notAnimated = true;
                this.flip = flip;
            }
            else
            {
                this.frames = frames;
                notAnimated = false;
                this.flip = flip;
            }
        }

        /// <summary>
        /// Creates an instance of non-animated map item
        /// </summary>
        /// <param name="frame0"></param>
        /// <param name="flip"></param>
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
        /// Draw as object
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
            int shiftCenteredX = mapShiftX - centerX;
            int shiftCenteredY = mapShiftY - centerY;

            if (notAnimated)
            {
                if (frame0.X - shiftCenteredX + frame0.Width > 0 && frame0.Y - shiftCenteredY + frame0.Height > 0 && frame0.X - shiftCenteredX < width && frame0.Y - shiftCenteredY < height)
                {
                    frame0.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                        shiftCenteredX, shiftCenteredY, 
                        flip);
                }
            }
            else
            {
                IDXObject frame = GetCurrFrame(TickCount);
                frame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                    shiftCenteredX, shiftCenteredY,
                    flip);
            }
        }

    }
}
