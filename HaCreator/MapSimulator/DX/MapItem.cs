using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaCreator.MapSimulator.DX
{
    public class MapItem
    {
        private List<DXObject> frames;
        private int currFrame = 0;
        private int lastFrameSwitchTime = 0;

        protected bool flip;
        protected bool notAnimated;
        private DXObject frame0;

        public MapItem(List<DXObject> frames, bool flip)
        {
            this.frames = frames;
            notAnimated = false;
            this.flip = flip;
        }

        public MapItem(DXObject frame0, bool flip)
        {
            this.frame0 = frame0;
            notAnimated = true;
            this.flip = flip;
        }

        protected DXObject GetCurrFrame()
        {
            if (notAnimated) 
                return frame0;
            else
            {
                int tc = Environment.TickCount;
                if (tc - lastFrameSwitchTime > frames[currFrame].Delay)
                { //advance frame
                    currFrame++;
                    if (currFrame == frames.Count) currFrame = 0;
                    lastFrameSwitchTime = tc;
                }
                return frames[currFrame];
            }
        }

        public virtual void Draw(SpriteBatch sprite, int mapShiftX, int mapShiftY, int centerX, int centerY, int width, int height)
        {
            if (notAnimated)
            {
                if (frame0.X - mapShiftX + frame0.Width > 0 && frame0.Y - mapShiftY + frame0.Height > 0 && frame0.X - mapShiftX < width && frame0.Y - mapShiftY < height)
                    frame0.Draw(sprite, mapShiftX, mapShiftY, flip);
            }
            else
                GetCurrFrame().Draw(sprite, mapShiftX, mapShiftY, flip);
        }
    }
}
