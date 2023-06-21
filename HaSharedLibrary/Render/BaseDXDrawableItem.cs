using HaSharedLibrary.Render.DX;
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
    /// <summary>
    /// The Base class for image or animated objects
    /// </summary>
    public class BaseDXDrawableItem : IBaseDXDrawableItem
    {
        // multiple frame
        private readonly List<IDXObject> frames;
        private int currFrame = 0;
        private int lastFrameSwitchTime = 0;

        // 1 frame
        protected bool flip;
        protected readonly bool notAnimated;
        private readonly IDXObject frame0;

        // Debug
        private string _DebugText = null;
        private int _lastDebugSwitchTime = 0;
        /// <summary>
        /// Indexed debug text for developers. 
        /// </summary>
        public string DebugText {
            get { return _DebugText; }
            set { this._DebugText = value; }
        }
        /// <summary>
        /// Returns true if the debug text is ready to be updated 
        /// </summary>
        /// <param name="TickCount"></param>
        /// <returns></returns>
        public bool CanUpdateDebugText(int TickCount, int updateTimeMillis) {
            // Animated
            if (TickCount - _lastDebugSwitchTime > updateTimeMillis) {
                this._lastDebugSwitchTime = TickCount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The last frame drawn
        /// </summary>
        private IDXObject _LastFrameDrawn = null;
        public IDXObject LastFrameDrawn
        {
            get { return this._LastFrameDrawn; }
            private set { }
        }


        private Point _Position;
        /// <summary>
        /// The additional relative position of the image (used primarily for UI overlay) 
        /// </summary>
        public Point Position
        {
            get { return this._Position; }
            set { this._Position = value; }
        }

        /// <summary>
        /// Creates an instance 
        /// </summary>
        /// <param name="frames"></param>
        /// <param name="flip"></param>
        public BaseDXDrawableItem(List<IDXObject> frames, bool flip)
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
            this._Position = new Point(0, 0);
        }

        /// <summary>
        /// Creates an instance of non-animated map item
        /// </summary>
        /// <param name="frame0"></param>
        /// <param name="flip"></param>
        public BaseDXDrawableItem(IDXObject frame0, bool flip)
        {
            this.frame0 = frame0;
            notAnimated = true;
            this.flip = flip;

            this._Position = new Point(0, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected IDXObject GetCurrentFrame(int TickCount)
        {
            if (notAnimated)
                return frame0;

            // Animated
            if (TickCount - lastFrameSwitchTime > frames[currFrame].Delay)
            {
                currFrame++;  //advance frame
                if (currFrame == frames.Count)
                    currFrame = 0;
                lastFrameSwitchTime = TickCount;
            }
            return frames[currFrame];
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
        /// <param name="drawReflectionInfo">The reflection info to draw for this object. Null if none.</param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="TickCount">Ticks since system startup</param>
        public virtual void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            int width, int height, float RenderObjectScaling, RenderResolution mapRenderResolution,
            int TickCount)
        {
            int shiftCenteredX = mapShiftX - centerX;
            int shiftCenteredY = mapShiftY - centerY;

            IDXObject drawFrame;
            if (notAnimated)
                drawFrame = frame0;
            else
                drawFrame = GetCurrentFrame(TickCount);

            if (IsFrameWithinView(drawFrame, shiftCenteredX, shiftCenteredY, width, height))
            {
                drawFrame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                    shiftCenteredX - _Position.X, shiftCenteredY - _Position.Y,
                    flip,
                    drawReflectionInfo // for map objects that are able to cast a reflection on items that are reflectable
                    );

                this._LastFrameDrawn = drawFrame; // set the last frame drawn
            }
            else
                this._LastFrameDrawn = null;
        }

        /// <summary>
        /// Checks if the animation frame's position is within the player's viewing box.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="shiftCenteredX"></param>
        /// <param name="shiftCenteredY"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFrameWithinView(IDXObject frame, int shiftCenteredX, int shiftCenteredY, int width, int height)
        {
            return (frame.X - shiftCenteredX + frame.Width > 0 &&
                frame.Y - shiftCenteredY + frame.Height > 0 &&
                frame.X - shiftCenteredX < width &&
                frame.Y - shiftCenteredY < height);
        }

        /// <summary>
        /// Copies the X and Y position from copySrc
        /// </summary>
        /// <param name="copySrc"></param>
        public void CopyObjectPosition(IBaseDXDrawableItem copySrc) {
            this.Position = new Point(copySrc.Position.X, copySrc.Position.Y);
        }
    }
}
