using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


namespace HaSharedLibrary.Render.DX
{
    /// <summary>
    /// The Base class for image or animated objects
    /// </summary>
    public class BaseDXDrawableItem : IBaseDXDrawableItem
    {
        // multiple frame
        private readonly IDXObject[]? frames;
        private readonly int frameCount;

        private int currFrame = 0;
        private int lastFrameSwitchTime = 0;

        // 1 frame
        protected bool flip;
        protected readonly bool notAnimated;
        private readonly IDXObject? frame0;

        // Visibility culling (calculated once per frame in Update, used in Draw)
        private bool _isVisible = true;
        private int _lastVisibilityCheckFrame = -1;

        /// <summary>
        /// Whether this object is currently visible (within view frustum).
        /// Pre-calculated during Update phase for performance.
        /// </summary>
        public bool IsVisible => _isVisible;

        // Debug
        private string? _debugText;
        private int _lastDebugSwitchTime;
        /// <summary>
        /// Indexed debug text for developers. 
        /// </summary>
        public string? DebugText
        {
            get => _debugText;
            set => _debugText = value;
        }

        /// <summary>
        /// Returns true if the debug text is ready to be updated 
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanUpdateDebugText(int tickCount, int updateTimeMillis) =>
            tickCount - _lastDebugSwitchTime > updateTimeMillis &&
            (_lastDebugSwitchTime = tickCount) >= 0;

        /// <summary>
        /// The last frame drawn. Returns the default frame if none
        /// </summary>
        private IDXObject? _lastFrameDrawn;
        public IDXObject? LastFrameDrawn
        {
            get => _lastFrameDrawn ?? frame0 ?? frames?[0];
            private set => _lastFrameDrawn = value;
        }

        public IDXObject? Frame0 => frame0;

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
            }
            else
            {
                if (frames.Count == 0)
                    throw new System.Exception("frame count is zero.");

                this.frames = frames.ToArray(); // Convert to array for better performance
                this.frameCount = frames.Count;
                this.notAnimated = false;
            }
            this.flip = flip;
            this._Position = Point.Zero; // Use static Point.Zero instead of new
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
                currFrame = (currFrame + 1) % frameCount; // Use modulo instead of if check
                lastFrameSwitchTime = TickCount;
            }
            return frames[currFrame];
        }

        /// <summary>
        /// Draw as object
        /// </summary>
        /// <param name="sprite"></param>
        /// <param name="skeletonMeshRenderer"></param>
        /// <param name="gameTime"></param>
        /// <param name="mapShiftX">The relative x position</param>
        /// <param name="mapShiftY">The relative y position</param>
        /// <param name="centerX"></param>
        /// <param name="centerY"></param>
        /// <param name="drawReflectionInfo">The reflection info to draw for this object. Null if none.</param>
        /// <param name="renderParameters"></param>
        /// <param name="TickCount">Ticks since system startup</param>
        public virtual void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            int shiftCenteredX = mapShiftX - centerX;
            int shiftCenteredY = mapShiftY - centerY;

            IDXObject drawFrame;
            if (notAnimated)
                drawFrame = frame0;
            else
                drawFrame = GetCurrentFrame(TickCount);

            if (IsFrameWithinView(drawFrame, shiftCenteredX, shiftCenteredY, renderParameters.RenderWidth, renderParameters.RenderHeight))
            {
                drawFrame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                    shiftCenteredX - _Position.X, shiftCenteredY - _Position.Y,
                    flip,
                    drawReflectionInfo // for map objects that are able to cast a reflection on items that are reflectable
                    );

                this._lastFrameDrawn = drawFrame; // set the last frame drawn
            }
            else
                this._lastFrameDrawn = null;
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
            int adjustedX = frame.X - shiftCenteredX;
            int adjustedY = frame.Y - shiftCenteredY;

            return adjustedX + frame.Width >= 0 &&
                   adjustedY + frame.Height >= 0 &&
                   adjustedX <= width &&
                   adjustedY <= height;
        }

        /// <summary>
        /// Pre-calculates visibility for this frame. Call once per frame in Update phase.
        /// </summary>
        /// <param name="mapShiftX">Map shift X position</param>
        /// <param name="mapShiftY">Map shift Y position</param>
        /// <param name="centerX">Center X offset</param>
        /// <param name="centerY">Center Y offset</param>
        /// <param name="viewWidth">View width</param>
        /// <param name="viewHeight">View height</param>
        /// <param name="frameNumber">Current frame number to avoid redundant calculations</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateVisibility(int mapShiftX, int mapShiftY, int centerX, int centerY,
            int viewWidth, int viewHeight, int frameNumber)
        {
            if (_lastVisibilityCheckFrame == frameNumber)
                return; // Already calculated this frame

            _lastVisibilityCheckFrame = frameNumber;

            IDXObject frame = notAnimated ? frame0 : (frames != null && frames.Length > 0 ? frames[0] : null);
            if (frame == null)
            {
                _isVisible = false;
                return;
            }

            int shiftCenteredX = mapShiftX - centerX;
            int shiftCenteredY = mapShiftY - centerY;

            _isVisible = IsFrameWithinView(frame, shiftCenteredX, shiftCenteredY, viewWidth, viewHeight);
        }

        /// <summary>
        /// Force sets visibility state (for objects that handle their own culling)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVisible(bool visible) => _isVisible = visible;

        /// <summary>
        /// Copies the X and Y position from copySrc
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyObjectPosition(IBaseDXDrawableItem copySrc) =>
            this.Position = copySrc.Position;
    }
}
