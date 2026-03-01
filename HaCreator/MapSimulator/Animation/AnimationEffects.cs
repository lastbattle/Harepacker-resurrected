using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Advanced animation effects system based on MapleStory's CAnimationDisplayer.
    /// Provides one-shot, repeating, chain lightning, falling, and following animations.
    ///
    /// Structures:
    /// - ONETIMEINFO: Single-play animations
    /// - REPEATINFO: Looping animations
    /// - CHAINLIGHTNINGINFO: Lightning chains between targets
    /// - FALLINGINFO: Falling object animations
    /// - FOLLOWINFO: Target-following animations
    /// </summary>
    public class AnimationEffects
    {
        private readonly List<OneTimeAnimation> _oneTimeAnimations = new();
        private readonly List<RepeatAnimation> _repeatAnimations = new();
        private readonly List<ChainLightning> _chainLightnings = new();
        private readonly List<FallingAnimation> _fallingAnimations = new();
        private readonly List<FollowAnimation> _followAnimations = new();

        private readonly Random _random = new();

        // Object pools for reduced allocations
        private readonly Queue<OneTimeAnimation> _oneTimePool = new();
        private readonly Queue<FallingAnimation> _fallingPool = new();

        #region One-Time Animation (ONETIMEINFO)

        /// <summary>
        /// Add a one-shot animation that plays once and disappears
        /// </summary>
        /// <param name="frames">Animation frames</param>
        /// <param name="x">World X position</param>
        /// <param name="y">World Y position</param>
        /// <param name="flip">Whether to flip horizontally</param>
        /// <param name="currentTimeMs">Current game time</param>
        /// <param name="zOrder">Draw order (higher = on top)</param>
        public void AddOneTime(List<IDXObject> frames, float x, float y, bool flip, int currentTimeMs, int zOrder = 0)
        {
            if (frames == null || frames.Count == 0) return;

            OneTimeAnimation anim = _oneTimePool.Count > 0 ? _oneTimePool.Dequeue() : new OneTimeAnimation();
            anim.Initialize(frames, x, y, flip, currentTimeMs, zOrder);
            _oneTimeAnimations.Add(anim);
        }

        /// <summary>
        /// Add a one-shot animation with color tint
        /// </summary>
        public void AddOneTimeTinted(List<IDXObject> frames, float x, float y, bool flip, Color tint, int currentTimeMs, int zOrder = 0)
        {
            if (frames == null || frames.Count == 0) return;

            OneTimeAnimation anim = _oneTimePool.Count > 0 ? _oneTimePool.Dequeue() : new OneTimeAnimation();
            anim.Initialize(frames, x, y, flip, currentTimeMs, zOrder);
            anim.Tint = tint;
            _oneTimeAnimations.Add(anim);
        }

        /// <summary>
        /// Add a one-shot animation with fade out
        /// </summary>
        public void AddOneTimeFading(List<IDXObject> frames, float x, float y, bool flip, int currentTimeMs, int zOrder = 0)
        {
            if (frames == null || frames.Count == 0) return;

            OneTimeAnimation anim = _oneTimePool.Count > 0 ? _oneTimePool.Dequeue() : new OneTimeAnimation();
            anim.Initialize(frames, x, y, flip, currentTimeMs, zOrder);
            anim.FadeOut = true;
            _oneTimeAnimations.Add(anim);
        }

        #endregion

        #region Repeat Animation (REPEATINFO)

        /// <summary>
        /// Add a looping animation at a fixed position
        /// </summary>
        /// <param name="frames">Animation frames</param>
        /// <param name="x">World X position</param>
        /// <param name="y">World Y position</param>
        /// <param name="flip">Whether to flip horizontally</param>
        /// <param name="durationMs">How long to loop (-1 for infinite)</param>
        /// <param name="currentTimeMs">Current game time</param>
        /// <returns>Animation ID for removal</returns>
        public int AddRepeat(List<IDXObject> frames, float x, float y, bool flip, int durationMs, int currentTimeMs)
        {
            if (frames == null || frames.Count == 0) return -1;

            RepeatAnimation anim = new RepeatAnimation();
            anim.Initialize(frames, x, y, flip, durationMs, currentTimeMs);
            _repeatAnimations.Add(anim);
            return anim.Id;
        }

        /// <summary>
        /// Remove a repeating animation by ID
        /// </summary>
        public bool RemoveRepeat(int id)
        {
            for (int i = _repeatAnimations.Count - 1; i >= 0; i--)
            {
                if (_repeatAnimations[i].Id == id)
                {
                    _repeatAnimations.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Chain Lightning (CHAINLIGHTNINGINFO)

        /// <summary>
        /// Create a chain lightning effect between multiple points
        /// </summary>
        /// <param name="points">List of points the lightning chains through</param>
        /// <param name="color">Lightning color</param>
        /// <param name="durationMs">Duration in milliseconds</param>
        /// <param name="currentTimeMs">Current game time</param>
        /// <param name="boltWidth">Width of lightning bolts</param>
        /// <param name="segments">Number of segments per bolt (more = more jagged)</param>
        public void AddChainLightning(List<Vector2> points, Color color, int durationMs, int currentTimeMs,
            float boltWidth = 3f, int segments = 8)
        {
            if (points == null || points.Count < 2) return;

            ChainLightning lightning = new ChainLightning();
            lightning.Initialize(points, color, durationMs, currentTimeMs, boltWidth, segments, _random);
            _chainLightnings.Add(lightning);
        }

        /// <summary>
        /// Create lightning between two points
        /// </summary>
        public void AddLightningBolt(Vector2 start, Vector2 end, Color color, int durationMs, int currentTimeMs,
            float boltWidth = 3f, int segments = 8)
        {
            AddChainLightning(new List<Vector2> { start, end }, color, durationMs, currentTimeMs, boltWidth, segments);
        }

        /// <summary>
        /// Create a blue lightning effect (typical skill effect)
        /// </summary>
        public void AddBlueLightning(Vector2 start, Vector2 end, int durationMs, int currentTimeMs)
        {
            AddLightningBolt(start, end, new Color(100, 150, 255), durationMs, currentTimeMs, 4f, 10);
        }

        /// <summary>
        /// Create a yellow lightning effect (typical thunder skill)
        /// </summary>
        public void AddYellowLightning(Vector2 start, Vector2 end, int durationMs, int currentTimeMs)
        {
            AddLightningBolt(start, end, new Color(255, 255, 100), durationMs, currentTimeMs, 3f, 12);
        }

        #endregion

        #region Falling Animation (FALLINGINFO)

        /// <summary>
        /// Add a falling object animation (like drops, leaves, debris)
        /// </summary>
        /// <param name="frames">Animation frames (can be single frame)</param>
        /// <param name="startX">Starting X position</param>
        /// <param name="startY">Starting Y position</param>
        /// <param name="endY">Target Y position (ground level)</param>
        /// <param name="fallSpeed">Fall speed in pixels per second</param>
        /// <param name="horizontalDrift">Horizontal drift (-1 to 1)</param>
        /// <param name="rotation">Whether to rotate while falling</param>
        /// <param name="currentTimeMs">Current game time</param>
        public void AddFalling(List<IDXObject> frames, float startX, float startY, float endY,
            float fallSpeed, float horizontalDrift, bool rotation, int currentTimeMs)
        {
            if (frames == null || frames.Count == 0) return;

            FallingAnimation anim = _fallingPool.Count > 0 ? _fallingPool.Dequeue() : new FallingAnimation();
            anim.Initialize(frames, startX, startY, endY, fallSpeed, horizontalDrift, rotation, currentTimeMs, _random);
            _fallingAnimations.Add(anim);
        }

        /// <summary>
        /// Add multiple falling objects in an area (like rain of items)
        /// </summary>
        public void AddFallingBurst(List<IDXObject> frames, float centerX, float startY, float endY,
            float spreadX, int count, float fallSpeed, int currentTimeMs)
        {
            for (int i = 0; i < count; i++)
            {
                float x = centerX + (float)(_random.NextDouble() * 2 - 1) * spreadX;
                float drift = (float)(_random.NextDouble() * 0.4 - 0.2);
                int delay = _random.Next(0, 300);

                // Stagger the start times
                AddFalling(frames, x, startY, endY, fallSpeed, drift, true, currentTimeMs + delay);
            }
        }

        #endregion

        #region Follow Animation (FOLLOWINFO)

        /// <summary>
        /// Add an animation that follows a target
        /// </summary>
        /// <param name="frames">Animation frames</param>
        /// <param name="getTargetPosition">Function that returns target position</param>
        /// <param name="offsetX">X offset from target</param>
        /// <param name="offsetY">Y offset from target</param>
        /// <param name="durationMs">Duration (-1 for infinite)</param>
        /// <param name="currentTimeMs">Current game time</param>
        /// <returns>Animation ID for removal</returns>
        public int AddFollow(List<IDXObject> frames, Func<Vector2> getTargetPosition,
            float offsetX, float offsetY, int durationMs, int currentTimeMs)
        {
            if (frames == null || frames.Count == 0) return -1;

            FollowAnimation anim = new FollowAnimation();
            anim.Initialize(frames, getTargetPosition, offsetX, offsetY, durationMs, currentTimeMs);
            _followAnimations.Add(anim);
            return anim.Id;
        }

        /// <summary>
        /// Remove a following animation by ID
        /// </summary>
        public bool RemoveFollow(int id)
        {
            for (int i = _followAnimations.Count - 1; i >= 0; i--)
            {
                if (_followAnimations[i].Id == id)
                {
                    _followAnimations.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Update

        /// <summary>
        /// Update all animation effects with frame-rate independent timing
        /// </summary>
        /// <param name="currentTimeMs">Current time in milliseconds</param>
        /// <param name="deltaSeconds">Time since last frame in seconds (defaults to 0.016f for backwards compatibility)</param>
        public void Update(int currentTimeMs, float deltaSeconds = 0.016f)
        {
            // Update one-time animations
            for (int i = _oneTimeAnimations.Count - 1; i >= 0; i--)
            {
                if (!_oneTimeAnimations[i].Update(currentTimeMs))
                {
                    _oneTimePool.Enqueue(_oneTimeAnimations[i]);
                    _oneTimeAnimations.RemoveAt(i);
                }
            }

            // Update repeat animations
            for (int i = _repeatAnimations.Count - 1; i >= 0; i--)
            {
                if (!_repeatAnimations[i].Update(currentTimeMs))
                {
                    _repeatAnimations.RemoveAt(i);
                }
            }

            // Update chain lightning
            for (int i = _chainLightnings.Count - 1; i >= 0; i--)
            {
                if (!_chainLightnings[i].Update(currentTimeMs))
                {
                    _chainLightnings.RemoveAt(i);
                }
            }

            // Update falling animations with frame-rate independent timing
            for (int i = _fallingAnimations.Count - 1; i >= 0; i--)
            {
                if (!_fallingAnimations[i].Update(currentTimeMs, deltaSeconds))
                {
                    _fallingPool.Enqueue(_fallingAnimations[i]);
                    _fallingAnimations.RemoveAt(i);
                }
            }

            // Update follow animations
            for (int i = _followAnimations.Count - 1; i >= 0; i--)
            {
                if (!_followAnimations[i].Update(currentTimeMs))
                {
                    _followAnimations.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw all animation effects
        /// </summary>
        /// <param name="spriteBatch">SpriteBatch for drawing</param>
        /// <param name="skeletonRenderer">Skeleton renderer for Spine animations</param>
        /// <param name="gameTime">Current game time</param>
        /// <param name="pixelTexture">1x1 white pixel texture for primitives</param>
        /// <param name="mapShiftX">Map shift X</param>
        /// <param name="mapShiftY">Map shift Y</param>
        /// <param name="currentTimeMs">Current time in milliseconds</param>
        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime,
            Texture2D pixelTexture, int mapShiftX, int mapShiftY, int currentTimeMs)
        {
            // Draw one-time animations
            foreach (var anim in _oneTimeAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY);
            }

            // Draw repeat animations
            foreach (var anim in _repeatAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY);
            }

            // Draw chain lightning
            foreach (var lightning in _chainLightnings)
            {
                lightning.Draw(spriteBatch, pixelTexture, mapShiftX, mapShiftY);
            }

            // Draw falling animations
            foreach (var anim in _fallingAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY);
            }

            // Draw follow animations
            foreach (var anim in _followAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Clear all animations
        /// </summary>
        public void Clear()
        {
            _oneTimeAnimations.Clear();
            _repeatAnimations.Clear();
            _chainLightnings.Clear();
            _fallingAnimations.Clear();
            _followAnimations.Clear();
        }

        /// <summary>
        /// Get count of active animations
        /// </summary>
        public int ActiveCount =>
            _oneTimeAnimations.Count + _repeatAnimations.Count +
            _chainLightnings.Count + _fallingAnimations.Count +
            _followAnimations.Count;

        #endregion
    }

    #region Animation Classes

    /// <summary>
    /// One-shot animation that plays once and removes itself
    /// </summary>
    internal class OneTimeAnimation
    {
        private List<IDXObject> _frames;
        private float _x, _y;
        private bool _flip;
        private int _startTime;
        private int _currentFrame;
        private int _lastFrameTime;
        private int _zOrder;
        private bool _finished;

        public Color Tint { get; set; } = Color.White;
        public bool FadeOut { get; set; } = false;
        public float Alpha { get; private set; } = 1f;

        public void Initialize(List<IDXObject> frames, float x, float y, bool flip, int currentTimeMs, int zOrder)
        {
            _frames = frames;
            _x = x;
            _y = y;
            _flip = flip;
            _startTime = currentTimeMs;
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
            _zOrder = zOrder;
            _finished = false;
            Tint = Color.White;
            FadeOut = false;
            Alpha = 1f;
        }

        public bool Update(int currentTimeMs)
        {
            if (_finished) return false;

            IDXObject frame = _frames[_currentFrame];
            if (currentTimeMs - _lastFrameTime > frame.Delay)
            {
                _currentFrame++;
                _lastFrameTime = currentTimeMs;

                if (_currentFrame >= _frames.Count)
                {
                    _finished = true;
                    return false;
                }
            }

            // Update fade
            if (FadeOut)
            {
                float progress = (float)_currentFrame / _frames.Count;
                Alpha = 1f - progress * 0.5f; // Fade to 50% by end
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            if (_finished || _currentFrame >= _frames.Count) return;

            IDXObject frame = _frames[_currentFrame];

            // Use the object's built-in DrawObject method
            // Note: DrawObject takes negative shift values because it subtracts them internally
            int drawShiftX = -(int)_x - mapShiftX;
            int drawShiftY = -(int)_y - mapShiftY;

            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, _flip, null);
        }
    }

    /// <summary>
    /// Looping animation at a fixed position
    /// </summary>
    internal class RepeatAnimation
    {
        private static int _nextId = 0;

        private List<IDXObject> _frames;
        private float _x, _y;
        private bool _flip;
        private int _startTime;
        private int _duration;
        private int _currentFrame;
        private int _lastFrameTime;

        public int Id { get; private set; }

        public void Initialize(List<IDXObject> frames, float x, float y, bool flip, int durationMs, int currentTimeMs)
        {
            Id = _nextId++;
            _frames = frames;
            _x = x;
            _y = y;
            _flip = flip;
            _startTime = currentTimeMs;
            _duration = durationMs;
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
        }

        public bool Update(int currentTimeMs)
        {
            // Check duration
            if (_duration > 0 && currentTimeMs - _startTime > _duration)
                return false;

            IDXObject frame = _frames[_currentFrame];
            if (currentTimeMs - _lastFrameTime > frame.Delay)
            {
                _currentFrame = (_currentFrame + 1) % _frames.Count;
                _lastFrameTime = currentTimeMs;
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            IDXObject frame = _frames[_currentFrame];

            // Use the object's built-in DrawObject method
            int drawShiftX = -(int)_x - mapShiftX;
            int drawShiftY = -(int)_y - mapShiftY;

            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, _flip, null);
        }
    }

    /// <summary>
    /// Chain lightning effect between multiple points
    /// </summary>
    internal class ChainLightning
    {
        private List<Vector2> _points;
        private List<List<Vector2>> _boltSegments; // Pre-calculated jagged segments
        private Color _color;
        private int _startTime;
        private int _duration;
        private float _boltWidth;
        private float _alpha = 1f;

        public void Initialize(List<Vector2> points, Color color, int durationMs, int currentTimeMs,
            float boltWidth, int segments, Random random)
        {
            _points = points;
            _color = color;
            _startTime = currentTimeMs;
            _duration = durationMs;
            _boltWidth = boltWidth;
            _alpha = 1f;

            // Pre-calculate jagged bolt segments
            _boltSegments = new List<List<Vector2>>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                _boltSegments.Add(GenerateBoltSegments(points[i], points[i + 1], segments, random));
            }
        }

        private List<Vector2> GenerateBoltSegments(Vector2 start, Vector2 end, int segments, Random random)
        {
            List<Vector2> result = new List<Vector2> { start };

            Vector2 delta = end - start;
            float length = delta.Length();
            Vector2 direction = delta / length;
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            float displacement = length * 0.15f; // Max displacement

            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                Vector2 point = start + delta * t;

                // Add random perpendicular offset
                float offset = (float)(random.NextDouble() * 2 - 1) * displacement;
                point += perpendicular * offset;

                result.Add(point);
            }

            result.Add(end);
            return result;
        }

        public bool Update(int currentTimeMs)
        {
            int elapsed = currentTimeMs - _startTime;
            if (elapsed >= _duration)
                return false;

            // Fade out in the last 30%
            float progress = (float)elapsed / _duration;
            if (progress > 0.7f)
            {
                _alpha = 1f - ((progress - 0.7f) / 0.3f);
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, int mapShiftX, int mapShiftY)
        {
            Color drawColor = _color * _alpha;

            foreach (var segments in _boltSegments)
            {
                for (int i = 0; i < segments.Count - 1; i++)
                {
                    Vector2 start = segments[i] + new Vector2(mapShiftX, mapShiftY);
                    Vector2 end = segments[i + 1] + new Vector2(mapShiftX, mapShiftY);

                    DrawLine(spriteBatch, pixelTexture, start, end, _boltWidth, drawColor);

                    // Draw glow effect (wider, more transparent)
                    DrawLine(spriteBatch, pixelTexture, start, end, _boltWidth * 2.5f, drawColor * 0.3f);
                }
            }
        }

        private void DrawLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length < 0.01f) return;

            float rotation = (float)Math.Atan2(delta.Y, delta.X);

            spriteBatch.Draw(
                texture,
                start,
                null,
                color,
                rotation,
                new Vector2(0, 0.5f),
                new Vector2(length, width),
                SpriteEffects.None,
                0);
        }
    }

    /// <summary>
    /// Falling object animation
    /// </summary>
    internal class FallingAnimation
    {
        private List<IDXObject> _frames;
        private float _x, _y;
        private float _startY, _endY;
        private float _fallSpeed;
        private float _horizontalDrift;
        private bool _rotation;
        private float _rotationAngle;
        private float _rotationSpeed;
        private int _startTime;
        private int _currentFrame;
        private int _lastFrameTime;
        private bool _finished;

        public void Initialize(List<IDXObject> frames, float startX, float startY, float endY,
            float fallSpeed, float horizontalDrift, bool rotation, int currentTimeMs, Random random)
        {
            _frames = frames;
            _x = startX;
            _y = startY;
            _startY = startY;
            _endY = endY;
            _fallSpeed = fallSpeed;
            _horizontalDrift = horizontalDrift;
            _rotation = rotation;
            _rotationAngle = 0;
            _rotationSpeed = rotation ? (float)(random.NextDouble() * 4 - 2) : 0; // Random rotation speed
            _startTime = currentTimeMs;
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
            _finished = false;
        }

        public bool Update(int currentTimeMs, float deltaSeconds)
        {
            if (_finished) return false;

            // Update position with frame-rate independent timing
            _y += _fallSpeed * deltaSeconds;
            _x += _horizontalDrift * _fallSpeed * deltaSeconds * 0.5f;

            // Update rotation
            if (_rotation)
            {
                _rotationAngle += _rotationSpeed * deltaSeconds;
            }

            // Check if reached ground
            if (_y >= _endY)
            {
                _finished = true;
                return false;
            }

            // Update animation frame
            if (_frames.Count > 1)
            {
                IDXObject frame = _frames[_currentFrame];
                if (currentTimeMs - _lastFrameTime > frame.Delay)
                {
                    _currentFrame = (_currentFrame + 1) % _frames.Count;
                    _lastFrameTime = currentTimeMs;
                }
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            if (_finished) return;

            IDXObject frame = _frames[_currentFrame];

            // Use the object's built-in DrawObject method
            // Note: DrawObject doesn't support rotation, but that's acceptable for most falling effects
            int drawShiftX = -(int)_x - mapShiftX;
            int drawShiftY = -(int)_y - mapShiftY;

            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, false, null);
        }
    }

    /// <summary>
    /// Animation that follows a target
    /// </summary>
    internal class FollowAnimation
    {
        private static int _nextId = 0;

        private List<IDXObject> _frames;
        private Func<Vector2> _getTargetPosition;
        private float _offsetX, _offsetY;
        private int _startTime;
        private int _duration;
        private int _currentFrame;
        private int _lastFrameTime;

        public int Id { get; private set; }

        public void Initialize(List<IDXObject> frames, Func<Vector2> getTargetPosition,
            float offsetX, float offsetY, int durationMs, int currentTimeMs)
        {
            Id = _nextId++;
            _frames = frames;
            _getTargetPosition = getTargetPosition;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _startTime = currentTimeMs;
            _duration = durationMs;
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
        }

        public bool Update(int currentTimeMs)
        {
            // Check duration
            if (_duration > 0 && currentTimeMs - _startTime > _duration)
                return false;

            // Update animation frame
            IDXObject frame = _frames[_currentFrame];
            if (currentTimeMs - _lastFrameTime > frame.Delay)
            {
                _currentFrame = (_currentFrame + 1) % _frames.Count;
                _lastFrameTime = currentTimeMs;
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            Vector2 targetPos = _getTargetPosition();
            IDXObject frame = _frames[_currentFrame];

            // Use the object's built-in DrawObject method
            float drawX = targetPos.X + _offsetX;
            float drawY = targetPos.Y + _offsetY;
            int drawShiftX = -(int)drawX - mapShiftX;
            int drawShiftY = -(int)drawY - mapShiftY;

            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, false, null);
        }
    }

    #endregion
}
