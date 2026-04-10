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
        internal sealed class FollowAnimationOptions
        {
            public IReadOnlyList<Vector2> GenerationPoints { get; init; }
            public int ThetaDegrees { get; init; }
            public float Radius { get; init; }
            public bool RandomizeStartupAngle { get; init; }
            public int UpdateIntervalMs { get; init; }
            public IReadOnlyList<List<IDXObject>> SpawnFrameVariants { get; init; }
            public bool SpawnRelativeToTarget { get; init; } = true;
            public bool SpawnUsesEmissionBox { get; init; }
            public bool SpawnAppliesEmissionBias { get; init; }
            public int SpawnDurationMs { get; init; }
            public float SpawnTravelDistanceMin { get; init; }
            public float SpawnTravelDistanceMax { get; init; }
            public float SpawnVerticalEmissionBias { get; init; }
            public Point SpawnOffsetMin { get; init; }
            public Point SpawnOffsetMax { get; init; }
            public Rectangle SpawnArea { get; init; }
        }

        private readonly List<OneTimeAnimation> _oneTimeAnimations = new();
        private readonly List<OneTimeCanvasLayerAnimation> _oneTimeCanvasLayers = new();
        private readonly List<RepeatAnimation> _repeatAnimations = new();
        private readonly List<ChainLightning> _chainLightnings = new();
        private readonly List<FallingAnimation> _fallingAnimations = new();
        private readonly List<FollowAnimation> _followAnimations = new();
        private readonly List<FollowParticleAnimation> _followParticleAnimations = new();
        private readonly List<AreaAnimationRegistration> _areaAnimations = new();
        private readonly List<UserStateAnimation> _userStateAnimations = new();

        private readonly Random _random = new();

        // Object pools for reduced allocations
        private readonly Queue<OneTimeAnimation> _oneTimePool = new();
        private readonly Queue<OneTimeCanvasLayerAnimation> _oneTimeCanvasLayerPool = new();
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

        internal void AddOneTimeAttached(
            List<IDXObject> frames,
            Func<Vector2> getPosition,
            Func<bool> getFlip,
            float fallbackX,
            float fallbackY,
            bool fallbackFlip,
            int currentTimeMs,
            int zOrder = 0)
        {
            if (frames == null || frames.Count == 0) return;

            OneTimeAnimation anim = _oneTimePool.Count > 0 ? _oneTimePool.Dequeue() : new OneTimeAnimation();
            anim.Initialize(frames, fallbackX, fallbackY, fallbackFlip, currentTimeMs, zOrder, getPosition, getFlip);
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

        internal void RegisterOneTimeCanvasLayer(
            Texture2D canvasTexture,
            float left,
            float top,
            int holdDurationMs,
            int fadeDurationMs,
            int riseDistancePx,
            int currentTimeMs,
            Texture2D overlayTexture = null,
            Point? overlayOffset = null,
            int overlayDelayMs = 0,
            bool ownsCanvasTexture = false,
            AnimationCanvasLayerOwner owner = AnimationCanvasLayerOwner.Generic,
            CanvasLayerRecoveredLayerSettings? recoveredLayerSettings = null)
        {
            if (canvasTexture == null)
            {
                return;
            }

            CanvasLayerRegistration registration = OneTimeCanvasLayerAnimation.BuildRegistration(
                holdDurationMs,
                fadeDurationMs,
                riseDistancePx,
                overlayTexture != null,
                overlayOffset ?? Point.Zero,
                overlayDelayMs,
                recoveredLayerSettings);

            OneTimeCanvasLayerAnimation anim = _oneTimeCanvasLayerPool.Count > 0
                ? _oneTimeCanvasLayerPool.Dequeue()
                : new OneTimeCanvasLayerAnimation();
            anim.Initialize(
                canvasTexture,
                overlayTexture,
                left,
                top,
                currentTimeMs,
                registration.InsertDescriptors,
                ownsCanvasTexture,
                owner,
                registration.RecoveredLayerSettings);
            _oneTimeCanvasLayers.Add(anim);
        }

        public void ClearCanvasLayers(AnimationCanvasLayerOwner owner)
        {
            for (int i = _oneTimeCanvasLayers.Count - 1; i >= 0; i--)
            {
                OneTimeCanvasLayerAnimation animation = _oneTimeCanvasLayers[i];
                if (animation.Owner != owner)
                {
                    continue;
                }

                animation.Reset();
                _oneTimeCanvasLayerPool.Enqueue(animation);
                _oneTimeCanvasLayers.RemoveAt(i);
            }
        }

        public void ClearDamageNumberLayers()
        {
            ClearCanvasLayers(AnimationCanvasLayerOwner.DamageNumber);
        }

        public int DamageNumberLayerCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _oneTimeCanvasLayers.Count; i++)
                {
                    if (_oneTimeCanvasLayers[i].Owner == AnimationCanvasLayerOwner.DamageNumber)
                    {
                        count++;
                    }
                }

                return count;
            }
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
            return AddFollow(frames, getTargetPosition, offsetX, offsetY, durationMs, currentTimeMs, options: null);
        }

        internal int AddFollow(
            List<IDXObject> frames,
            Func<Vector2> getTargetPosition,
            float offsetX,
            float offsetY,
            int durationMs,
            int currentTimeMs,
            FollowAnimationOptions options)
        {
            bool hasFollowFrames = HasFrames(frames);
            bool hasSpawnVariants = HasFrameVariants(options?.SpawnFrameVariants);
            if (!hasFollowFrames && !hasSpawnVariants) return -1;

            FollowAnimation anim = new FollowAnimation();
            anim.Initialize(frames, getTargetPosition, offsetX, offsetY, durationMs, currentTimeMs, options, _random);
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
                    RemoveFollowParticles(id);
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region User State Animation

        public int RegisterUserState(
            int ownerId,
            List<IDXObject> startFrames,
            List<IDXObject> repeatFrames,
            List<IDXObject> endFrames,
            Func<Vector2> getTargetPosition,
            float offsetX,
            float offsetY,
            int currentTimeMs)
        {
            return RegisterUserState(
                registrationKey: ownerId,
                ownerId,
                startFrames,
                repeatFrames,
                endFrames,
                getTargetPosition,
                offsetX,
                offsetY,
                currentTimeMs);
        }

        public int RegisterUserState(
            int registrationKey,
            int ownerId,
            List<IDXObject> startFrames,
            List<IDXObject> repeatFrames,
            List<IDXObject> endFrames,
            Func<Vector2> getTargetPosition,
            float offsetX,
            float offsetY,
            int currentTimeMs)
        {
            if (ownerId <= 0 || getTargetPosition == null)
            {
                return -1;
            }

            if (!HasFrames(startFrames) && !HasFrames(repeatFrames) && !HasFrames(endFrames))
            {
                return -1;
            }

            for (int i = _userStateAnimations.Count - 1; i >= 0; i--)
            {
                if (_userStateAnimations[i].RegistrationKey == registrationKey)
                {
                    _userStateAnimations.RemoveAt(i);
                }
            }

            var animation = new UserStateAnimation();
            animation.Initialize(registrationKey, ownerId, startFrames, repeatFrames, endFrames, getTargetPosition, offsetX, offsetY, currentTimeMs);
            _userStateAnimations.Add(animation);
            return registrationKey;
        }

        public bool RemoveUserState(int ownerId, int currentTimeMs)
        {
            return RemoveUserStateByRegistrationKey(ownerId, currentTimeMs);
        }

        public bool RemoveUserStateByRegistrationKey(int registrationKey, int currentTimeMs)
        {
            for (int i = _userStateAnimations.Count - 1; i >= 0; i--)
            {
                if (_userStateAnimations[i].RegistrationKey == registrationKey)
                {
                    if (_userStateAnimations[i].BeginEndPhase(currentTimeMs))
                    {
                        return true;
                    }

                    _userStateAnimations.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool HasUserState(int ownerId)
        {
            return HasUserStateByRegistrationKey(ownerId);
        }

        public bool HasUserStateByRegistrationKey(int registrationKey)
        {
            for (int i = 0; i < _userStateAnimations.Count; i++)
            {
                if (_userStateAnimations[i].RegistrationKey == registrationKey)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Area Animation

        public int RegisterAreaAnimation(
            List<IDXObject> frames,
            Rectangle area,
            int updateIntervalMs,
            int updateCount,
            int updateNextMs,
            int durationMs,
            int currentTimeMs,
            Action onSpawn = null)
        {
            if (!HasFrames(frames) || area.Width <= 0 || area.Height <= 0)
            {
                return -1;
            }

            var registration = new AreaAnimationRegistration();
            registration.Initialize(frames, area, updateIntervalMs, updateCount, updateNextMs, durationMs, currentTimeMs, onSpawn);
            _areaAnimations.Add(registration);
            return registration.Id;
        }

        public bool RemoveAreaAnimation(int id)
        {
            for (int i = _areaAnimations.Count - 1; i >= 0; i--)
            {
                if (_areaAnimations[i].Id == id)
                {
                    _areaAnimations.RemoveAt(i);
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

            for (int i = _oneTimeCanvasLayers.Count - 1; i >= 0; i--)
            {
                if (!_oneTimeCanvasLayers[i].Update(currentTimeMs))
                {
                    _oneTimeCanvasLayerPool.Enqueue(_oneTimeCanvasLayers[i]);
                    _oneTimeCanvasLayers.RemoveAt(i);
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
                if (!_followAnimations[i].Update(this, currentTimeMs, _random))
                {
                    RemoveFollowParticles(_followAnimations[i].Id);
                    _followAnimations.RemoveAt(i);
                }
            }

            for (int i = _followParticleAnimations.Count - 1; i >= 0; i--)
            {
                if (!_followParticleAnimations[i].Update(currentTimeMs))
                {
                    _followParticleAnimations.RemoveAt(i);
                }
            }

            for (int i = _areaAnimations.Count - 1; i >= 0; i--)
            {
                if (!_areaAnimations[i].Update(this, currentTimeMs, _random))
                {
                    _areaAnimations.RemoveAt(i);
                }
            }

            for (int i = _userStateAnimations.Count - 1; i >= 0; i--)
            {
                if (!_userStateAnimations[i].Update(currentTimeMs))
                {
                    _userStateAnimations.RemoveAt(i);
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

            foreach (var anim in _oneTimeCanvasLayers)
            {
                anim.Draw(spriteBatch, mapShiftX, mapShiftY);
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

            foreach (var anim in _followParticleAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY, currentTimeMs);
            }

            foreach (var anim in _userStateAnimations)
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
            for (int i = 0; i < _oneTimeCanvasLayers.Count; i++)
            {
                _oneTimeCanvasLayers[i].Reset();
            }
            _oneTimeCanvasLayers.Clear();
            _repeatAnimations.Clear();
            _chainLightnings.Clear();
            _fallingAnimations.Clear();
            _followAnimations.Clear();
            _areaAnimations.Clear();
            _userStateAnimations.Clear();
        }

        /// <summary>
        /// Get count of active animations
        /// </summary>
        public int ActiveCount =>
            _oneTimeAnimations.Count + _oneTimeCanvasLayers.Count + _repeatAnimations.Count +
            _chainLightnings.Count + _fallingAnimations.Count +
            _followAnimations.Count + _areaAnimations.Count +
            _userStateAnimations.Count;

        internal static bool HasFrames(List<IDXObject> frames)
        {
            return frames != null && frames.Count > 0;
        }

        internal static bool HasFrameVariants(IReadOnlyList<List<IDXObject>> frameVariants)
        {
            if (frameVariants == null)
            {
                return false;
            }

            for (int i = 0; i < frameVariants.Count; i++)
            {
                if (HasFrames(frameVariants[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public void ClearUserStates()
        {
            _userStateAnimations.Clear();
        }

        public void ClearAreaAnimations()
        {
            _areaAnimations.Clear();
        }

        private void RemoveFollowParticles(int followRegistrationId)
        {
            for (int i = _followParticleAnimations.Count - 1; i >= 0; i--)
            {
                if (_followParticleAnimations[i].FollowRegistrationId == followRegistrationId)
                {
                    _followParticleAnimations.RemoveAt(i);
                }
            }
        }

        internal void AddFollowParticle(
            int followRegistrationId,
            List<IDXObject> frames,
            Func<Vector2> getTargetPosition,
            Vector2 capturedTargetPosition,
            bool relativeToTarget,
            float offsetX,
            float offsetY,
            Vector2 startOffset,
            Vector2 endOffset,
            int durationMs,
            int currentTimeMs)
        {
            if (!HasFrames(frames) || getTargetPosition == null)
            {
                return;
            }

            var particle = new FollowParticleAnimation();
            particle.Initialize(
                followRegistrationId,
                frames,
                getTargetPosition,
                capturedTargetPosition,
                relativeToTarget,
                offsetX,
                offsetY,
                startOffset,
                endOffset,
                durationMs,
                currentTimeMs);
            _followParticleAnimations.Add(particle);
        }

        internal static Vector2 ResolveFollowGenerationPointOffset(
            IReadOnlyList<Vector2> generationPoints,
            int generationPointIndex,
            float radius,
            int currentAngleDegrees,
            out int nextGenerationPointIndex,
            out int nextAngleDegrees)
        {
            if (generationPoints != null && generationPoints.Count > 0)
            {
                int resolvedIndex = Math.Max(0, generationPointIndex) % generationPoints.Count;
                nextGenerationPointIndex = (resolvedIndex + 1) % generationPoints.Count;
                nextAngleDegrees = NormalizeFollowAngleDegrees(currentAngleDegrees);
                return generationPoints[resolvedIndex];
            }

            int normalizedAngle = NormalizeFollowAngleDegrees(currentAngleDegrees);
            nextGenerationPointIndex = 0;
            nextAngleDegrees = normalizedAngle;
            return ResolvePolarFollowOffset(radius, normalizedAngle);
        }

        internal static Vector2 ResolvePolarFollowOffset(float radius, int angleDegrees)
        {
            float radians = MathHelper.ToRadians(NormalizeFollowAngleDegrees(angleDegrees));
            return new Vector2(
                (float)Math.Cos(radians) * radius,
                (float)Math.Sin(radians) * radius);
        }

        internal static int NormalizeFollowAngleDegrees(int angleDegrees)
        {
            int normalized = angleDegrees % 360;
            return normalized < 0 ? normalized + 360 : normalized;
        }

        internal static int ResolveFollowSpawnDurationMs(IReadOnlyList<IDXObject> frames, int explicitDurationMs)
        {
            if (explicitDurationMs > 0)
            {
                return explicitDurationMs;
            }

            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int totalDurationMs = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                totalDurationMs += Math.Max(1, frames[i]?.Delay ?? 0);
            }

            return Math.Max(1, totalDurationMs);
        }

        internal static int ResolveFollowParticleAngleDegrees(Vector2 emissionOffset, int fallbackAngleDegrees)
        {
            if (emissionOffset.LengthSquared() <= float.Epsilon)
            {
                return NormalizeFollowAngleDegrees(fallbackAngleDegrees);
            }

            int angleDegrees = (int)Math.Round(MathHelper.ToDegrees((float)Math.Atan2(emissionOffset.Y, emissionOffset.X)));
            return NormalizeFollowAngleDegrees(angleDegrees);
        }

        internal static int ResolveFollowSpawnAngleDegrees(
            bool hasGenerationPoints,
            int currentAngleDegrees,
            int thetaDegrees,
            Random random)
        {
            if (hasGenerationPoints || thetaDegrees != 0)
            {
                return NormalizeFollowAngleDegrees(currentAngleDegrees);
            }

            return random?.Next(0, 360) ?? 0;
        }

        internal static int ResolveFollowRandomOffsetComponent(int startInclusive, int endExclusive, Random random)
        {
            int resolvedMin = Math.Min(startInclusive, endExclusive);
            int resolvedMax = Math.Max(startInclusive, endExclusive);
            int delta = resolvedMax - resolvedMin;
            if (delta <= 0)
            {
                return resolvedMin;
            }

            return resolvedMin + (random?.Next(delta) ?? 0);
        }

        internal static Vector2 ResolveFollowRandomOffset(Point startInclusive, Point endExclusive, Random random)
        {
            return new Vector2(
                ResolveFollowRandomOffsetComponent(startInclusive.X, endExclusive.X, random),
                ResolveFollowRandomOffsetComponent(startInclusive.Y, endExclusive.Y, random));
        }

        internal static Vector2 ResolveFollowEmissionStartOffset(Rectangle area, Random random)
        {
            int x = ResolveFollowRandomOffsetComponent(area.Left, area.Right, random);
            int y = ResolveFollowRandomOffsetComponent(area.Top, area.Bottom, random);
            return new Vector2(x, y);
        }

        internal static Vector2 ResolveFollowParticleStartOffset(
            Vector2 generationPointOffset,
            bool hasGenerationPoints,
            int angleDegrees,
            Vector2 randomOffset,
            Rectangle emissionArea,
            bool useEmissionBox,
            bool applyEmissionBias,
            float verticalEmissionBias,
            Random random)
        {
            Vector2 startOffset;
            if (useEmissionBox)
            {
                Vector2 emissionStart = ResolveFollowEmissionStartOffset(emissionArea, random);
                startOffset = emissionStart;
            }
            else if (hasGenerationPoints)
            {
                startOffset = generationPointOffset;
            }
            else
            {
                float radialDistance = Math.Abs(randomOffset.X);
                startOffset = ResolvePolarFollowOffset(radialDistance, angleDegrees);
            }

            return applyEmissionBias
                ? new Vector2(startOffset.X, startOffset.Y + verticalEmissionBias)
                : startOffset;
        }

        internal static Vector2 ResolveFollowParticleEndOffset(
            Vector2 emissionOffset,
            int angleDegrees,
            Vector2 randomOffset,
            bool useEmissionBox)
        {
            if (!useEmissionBox)
            {
                float travelDistance = Math.Abs(randomOffset.Y);
                if (travelDistance <= float.Epsilon)
                {
                    return emissionOffset;
                }

                return emissionOffset + ResolvePolarFollowOffset(travelDistance, angleDegrees);
            }

            float boxedTravelDistance = Math.Abs(randomOffset.Y);
            if (boxedTravelDistance <= float.Epsilon)
            {
                return emissionOffset;
            }

            return emissionOffset + ResolvePolarFollowOffset(boxedTravelDistance, angleDegrees);
        }

        internal static Vector2 ResolveFollowParticleEndOffset(
            Vector2 emissionOffset,
            int angleDegrees,
            float radialDistance)
        {
            if (radialDistance <= float.Epsilon)
            {
                return emissionOffset;
            }

            return emissionOffset + ResolvePolarFollowOffset(radialDistance, angleDegrees);
        }

        internal static float ResolveFollowParticleTravelDistance(float minDistance, float maxDistance, Random random)
        {
            float resolvedMin = Math.Max(0f, Math.Min(minDistance, maxDistance));
            float resolvedMax = Math.Max(0f, Math.Max(minDistance, maxDistance));
            if (resolvedMax <= resolvedMin + float.Epsilon)
            {
                return resolvedMin;
            }

            return resolvedMin + ((float)(random?.NextDouble() ?? 0d) * (resolvedMax - resolvedMin));
        }

        internal static Vector2 ResolveFollowSpawnAnchorPosition(
            Vector2 targetPosition,
            Vector2 capturedTargetPosition,
            bool relativeToTarget)
        {
            return relativeToTarget ? targetPosition : capturedTargetPosition;
        }

        public int UserStateCount => _userStateAnimations.Count;
        public int AreaAnimationCount => _areaAnimations.Count;
        public int FollowAnimationCount => _followAnimations.Count;
        public int FollowParticleCount => _followParticleAnimations.Count;

        #endregion
    }

    #region Animation Classes

    /// <summary>
    /// Shared one-time canvas layer owners used by animation-displayer parity paths.
    /// </summary>
    public enum AnimationCanvasLayerOwner
    {
        Generic = 0,
        DamageNumber = 1
    }

    internal enum AnimationCanvasLayerContent
    {
        PrimaryCanvas = 0,
        OverlayCanvas = 1
    }

    internal enum AnimationCanvasLayerBlendMode
    {
        AlphaBlend = 0
    }

    internal readonly record struct CanvasLayerInsertDescriptor(
        AnimationCanvasLayerContent Content,
        Point Offset,
        int StartDelayMs,
        int HoldDurationMs,
        int FadeDurationMs,
        float StartAlpha,
        float EndAlpha,
        int RiseDistancePx,
        AnimationCanvasLayerBlendMode BlendMode,
        CanvasLayerRecoveredInsertCanvasSettings RecoveredInsertCanvasSettings,
        CanvasLayerRecoveredMoveSettings RecoveredMoveSettings);

    /// <summary>
    /// Native InsertCanvas parameters recovered from CAnimationDisplayer::Effect_HP.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredInsertCanvasSettings(
        int DurationMs,
        int StartAlphaValue,
        int EndAlphaValue);

    /// <summary>
    /// Native layer movement target recovered from CAnimationDisplayer::Effect_HP.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredMoveSettings(
        Point StartOffset,
        Point EndOffset);

    /// <summary>
    /// Native layer position recovered from CAnimationDisplayer::Effect_HP.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredPositionSettings(
        int Left,
        int Top);

    /// <summary>
    /// Native InsertCanvas call shape recovered from CAnimationDisplayer::Effect_HP.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredInsertCommand(
        AnimationCanvasLayerContent Content,
        Point Offset,
        int StartDelayMs,
        CanvasLayerRecoveredInsertCanvasSettings InsertCanvasSettings,
        CanvasLayerRecoveredMoveSettings MoveSettings);

    /// <summary>
    /// Temporary canvas dimensions recovered from CAnimationDisplayer::Effect_HP CreateCanvas.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredCanvasSettings(
        int Width,
        int Height);

    /// <summary>
    /// Recovered native layer values from CAnimationDisplayer::Effect_HP.
    /// The simulator preserves these as parity metadata even though it does not instantiate the client COM graph.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredLayerSettings(
        int CreateLayerCanvasValue,
        int InitialLayerOptionValue,
        int LayerPriorityValue,
        int FinalizeLayerOptionValue);

    internal readonly record struct CanvasLayerRegistration(
        CanvasLayerInsertDescriptor[] InsertDescriptors,
        CanvasLayerRecoveredLayerSettings RecoveredLayerSettings);

    /// <summary>
    /// Full recovered registration trace for the managed canvas-layer analogue.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredRegistrationTrace(
        CanvasLayerRecoveredCanvasSettings CanvasSettings,
        CanvasLayerRecoveredLayerSettings LayerSettings,
        CanvasLayerRecoveredPositionSettings Position,
        CanvasLayerRecoveredInsertCommand[] InsertCommands,
        bool RegistersOneTimeAnimation);

    /// <summary>
    /// One-shot canvas-backed animation that mirrors RegisterOneTimeAnimation ownership.
    /// </summary>
    internal class OneTimeCanvasLayerAnimation
    {
        private sealed class CanvasLayerInsertOperation
        {
            public Texture2D Texture { get; init; }
            public CanvasLayerInsertDescriptor Descriptor { get; init; }
        }

        private Texture2D _canvasTexture;
        private Texture2D _overlayTexture;
        private float _left;
        private float _top;
        private int _startTimeMs;
        private int _elapsedMs;
        private bool _ownsCanvasTexture;
        private CanvasLayerInsertOperation[] _insertOperations = Array.Empty<CanvasLayerInsertOperation>();

        public AnimationCanvasLayerOwner Owner { get; private set; } = AnimationCanvasLayerOwner.Generic;
        internal IReadOnlyList<CanvasLayerInsertDescriptor> InsertDescriptors => Array.ConvertAll(
            _insertOperations,
            static operation => operation.Descriptor);
        internal CanvasLayerRecoveredLayerSettings RecoveredLayerSettings { get; private set; }
        internal CanvasLayerRecoveredRegistrationTrace RecoveredRegistrationTrace { get; private set; }

        internal static CanvasLayerInsertDescriptor[] BuildInsertDescriptors(
            int holdDurationMs,
            int fadeDurationMs,
            int riseDistancePx,
            bool hasOverlay,
            Point overlayOffset,
            int overlayDelayMs)
        {
            holdDurationMs = Math.Max(0, holdDurationMs);
            fadeDurationMs = Math.Max(0, fadeDurationMs);
            overlayDelayMs = Math.Max(0, overlayDelayMs);

            var descriptors = new List<CanvasLayerInsertDescriptor>(hasOverlay ? 3 : 2)
            {
                new(
                    AnimationCanvasLayerContent.PrimaryCanvas,
                    Point.Zero,
                    0,
                holdDurationMs,
                0,
                1f,
                1f,
                0,
                AnimationCanvasLayerBlendMode.AlphaBlend,
                new CanvasLayerRecoveredInsertCanvasSettings(holdDurationMs, 255, 255),
                new CanvasLayerRecoveredMoveSettings(Point.Zero, Point.Zero)),
                new(
                    AnimationCanvasLayerContent.PrimaryCanvas,
                    Point.Zero,
                    holdDurationMs,
                    0,
                    fadeDurationMs,
                    1f,
                    0f,
                    riseDistancePx,
                    AnimationCanvasLayerBlendMode.AlphaBlend,
                    new CanvasLayerRecoveredInsertCanvasSettings(fadeDurationMs, 255, 0),
                    new CanvasLayerRecoveredMoveSettings(Point.Zero, new Point(0, -riseDistancePx))),
            };

            if (hasOverlay)
            {
                int overlayHoldDurationMs = Math.Max(0, holdDurationMs - overlayDelayMs);
                descriptors.Add(new CanvasLayerInsertDescriptor(
                    AnimationCanvasLayerContent.OverlayCanvas,
                    overlayOffset,
                    overlayDelayMs,
                    overlayHoldDurationMs,
                    fadeDurationMs,
                    1f,
                    0f,
                    riseDistancePx,
                    AnimationCanvasLayerBlendMode.AlphaBlend,
                    new CanvasLayerRecoveredInsertCanvasSettings(
                        overlayHoldDurationMs + fadeDurationMs,
                        255,
                        0),
                    new CanvasLayerRecoveredMoveSettings(
                        overlayOffset,
                        new Point(overlayOffset.X, overlayOffset.Y - riseDistancePx))));
            }

            return descriptors.ToArray();
        }

        internal static CanvasLayerRegistration BuildRegistration(
            int holdDurationMs,
            int fadeDurationMs,
            int riseDistancePx,
            bool hasOverlay,
            Point overlayOffset,
            int overlayDelayMs,
            CanvasLayerRecoveredLayerSettings? recoveredLayerSettings = null)
        {
            return new CanvasLayerRegistration(
                BuildInsertDescriptors(
                    holdDurationMs,
                    fadeDurationMs,
                    riseDistancePx,
                    hasOverlay,
                    overlayOffset,
                    overlayDelayMs),
                recoveredLayerSettings ?? default);
        }

        public void Initialize(
            Texture2D canvasTexture,
            Texture2D overlayTexture,
            float left,
            float top,
            int currentTimeMs,
            IReadOnlyList<CanvasLayerInsertDescriptor> insertDescriptors,
            bool ownsCanvasTexture,
            AnimationCanvasLayerOwner owner,
            CanvasLayerRecoveredLayerSettings recoveredLayerSettings)
        {
            _canvasTexture = canvasTexture;
            _overlayTexture = overlayTexture;
            _left = left;
            _top = top;
            _startTimeMs = currentTimeMs;
            _elapsedMs = 0;
            _ownsCanvasTexture = ownsCanvasTexture;
            _insertOperations = BuildInsertOperations(insertDescriptors);
            Owner = owner;
            RecoveredLayerSettings = recoveredLayerSettings;
            RecoveredRegistrationTrace = BuildRecoveredRegistrationTrace(
                left,
                top,
                canvasTexture.Width,
                canvasTexture.Height,
                insertDescriptors,
                recoveredLayerSettings,
                registersOneTimeAnimation: true);
        }

        public bool Update(int currentTimeMs)
        {
            _elapsedMs = Math.Max(0, currentTimeMs - _startTimeMs);
            return HasActiveInsertOperation();
        }

        public void Draw(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY)
        {
            if (_canvasTexture == null)
            {
                return;
            }

            for (int i = 0; i < _insertOperations.Length; i++)
            {
                CanvasLayerInsertOperation operation = _insertOperations[i];
                if (operation?.Texture == null
                    || !TryResolveOperationDrawState(operation.Descriptor, out float alpha, out float riseOffset))
                {
                    continue;
                }

                Vector2 position = new(
                    _left + mapShiftX + operation.Descriptor.Offset.X,
                    _top + mapShiftY + operation.Descriptor.Offset.Y + riseOffset);

                spriteBatch.Draw(operation.Texture, position, Color.White * alpha);
            }
        }

        public void Reset()
        {
            if (_ownsCanvasTexture)
            {
                _canvasTexture?.Dispose();
            }

            _canvasTexture = null;
            _overlayTexture = null;
            _left = 0f;
            _top = 0f;
            _startTimeMs = 0;
            _elapsedMs = 0;
            _ownsCanvasTexture = false;
            _insertOperations = Array.Empty<CanvasLayerInsertOperation>();
            Owner = AnimationCanvasLayerOwner.Generic;
            RecoveredLayerSettings = default;
            RecoveredRegistrationTrace = default;
        }

        internal static CanvasLayerRecoveredRegistrationTrace BuildRecoveredRegistrationTrace(
            float left,
            float top,
            int canvasWidth,
            int canvasHeight,
            IReadOnlyList<CanvasLayerInsertDescriptor> insertDescriptors,
            CanvasLayerRecoveredLayerSettings recoveredLayerSettings,
            bool registersOneTimeAnimation)
        {
            CanvasLayerRecoveredInsertCommand[] recoveredInsertCommands;
            if (insertDescriptors == null || insertDescriptors.Count == 0)
            {
                recoveredInsertCommands = Array.Empty<CanvasLayerRecoveredInsertCommand>();
            }
            else
            {
                recoveredInsertCommands = new CanvasLayerRecoveredInsertCommand[insertDescriptors.Count];
                for (int i = 0; i < insertDescriptors.Count; i++)
                {
                    CanvasLayerInsertDescriptor descriptor = insertDescriptors[i];
                    recoveredInsertCommands[i] = new CanvasLayerRecoveredInsertCommand(
                        descriptor.Content,
                        descriptor.Offset,
                        descriptor.StartDelayMs,
                        descriptor.RecoveredInsertCanvasSettings,
                        descriptor.RecoveredMoveSettings);
                }
            }

            return new CanvasLayerRecoveredRegistrationTrace(
                new CanvasLayerRecoveredCanvasSettings(
                    Math.Max(0, canvasWidth),
                    Math.Max(0, canvasHeight)),
                recoveredLayerSettings,
                new CanvasLayerRecoveredPositionSettings(
                    (int)Math.Round(left, MidpointRounding.AwayFromZero),
                    (int)Math.Round(top, MidpointRounding.AwayFromZero)),
                recoveredInsertCommands,
                registersOneTimeAnimation);
        }

        private CanvasLayerInsertOperation[] BuildInsertOperations(IReadOnlyList<CanvasLayerInsertDescriptor> insertDescriptors)
        {
            if (insertDescriptors == null || insertDescriptors.Count == 0)
            {
                return Array.Empty<CanvasLayerInsertOperation>();
            }

            var operations = new CanvasLayerInsertOperation[insertDescriptors.Count];
            for (int i = 0; i < insertDescriptors.Count; i++)
            {
                CanvasLayerInsertDescriptor descriptor = insertDescriptors[i];
                Texture2D texture = descriptor.Content switch
                {
                    AnimationCanvasLayerContent.PrimaryCanvas => _canvasTexture,
                    AnimationCanvasLayerContent.OverlayCanvas => _overlayTexture,
                    _ => null,
                };

                operations[i] = new CanvasLayerInsertOperation
                {
                    Texture = texture,
                    Descriptor = descriptor,
                };
            }

            return operations;
        }

        private bool HasActiveInsertOperation()
        {
            for (int i = 0; i < _insertOperations.Length; i++)
            {
                CanvasLayerInsertOperation operation = _insertOperations[i];
                if (operation?.Texture != null
                    && TryResolveOperationDrawState(operation.Descriptor, out _, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveOperationDrawState(
            CanvasLayerInsertDescriptor descriptor,
            out float alpha,
            out float riseOffset)
        {
            alpha = 0f;
            riseOffset = 0f;

            int localElapsedMs = _elapsedMs - descriptor.StartDelayMs;
            int lifetimeMs = descriptor.HoldDurationMs + descriptor.FadeDurationMs;
            if (localElapsedMs < 0 || localElapsedMs >= lifetimeMs)
            {
                return false;
            }

            float transitionProgress = 0f;
            if (descriptor.FadeDurationMs > 0)
            {
                int fadeElapsedMs = Math.Max(0, localElapsedMs - descriptor.HoldDurationMs);
                transitionProgress = Math.Clamp((float)fadeElapsedMs / descriptor.FadeDurationMs, 0f, 1f);
                riseOffset = -descriptor.RiseDistancePx * transitionProgress;
            }

            alpha = MathHelper.Lerp(descriptor.StartAlpha, descriptor.EndAlpha, transitionProgress);
            return alpha > 0f;
        }
    }

    /// <summary>
    /// One-shot animation that plays once and removes itself
    /// </summary>
    internal class OneTimeAnimation
    {
        private List<IDXObject> _frames;
        private float _x, _y;
        private bool _flip;
        private Func<Vector2> _positionResolver;
        private Func<bool> _flipResolver;
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
            Initialize(frames, x, y, flip, currentTimeMs, zOrder, getPosition: null, getFlip: null);
        }

        public void Initialize(
            List<IDXObject> frames,
            float x,
            float y,
            bool flip,
            int currentTimeMs,
            int zOrder,
            Func<Vector2> getPosition,
            Func<bool> getFlip)
        {
            _frames = frames;
            _x = x;
            _y = y;
            _flip = flip;
            _positionResolver = getPosition;
            _flipResolver = getFlip;
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
            Vector2 position = _positionResolver?.Invoke() ?? new Vector2(_x, _y);
            bool flip = _flipResolver?.Invoke() ?? _flip;

            // Use the object's built-in DrawObject method
            // Note: DrawObject takes negative shift values because it subtracts them internally
            int drawShiftX = -(int)position.X - mapShiftX;
            int drawShiftY = -(int)position.Y - mapShiftY;

            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, flip, null);
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
        private int _lastFollowUpdateTime;
        private IReadOnlyList<Vector2> _generationPoints;
        private int _currentGenerationPointIndex;
        private int _currentAngleDegrees;
        private int _thetaDegrees;
        private int _updateIntervalMs;
        private float _radius;
        private Vector2 _followOffset;
        private IReadOnlyList<List<IDXObject>> _spawnFrameVariants;
        private bool _spawnRelativeToTarget;
        private int _spawnDurationMs;
        private int _nextSpawnTime;
        private float _spawnTravelDistanceMin;
        private float _spawnTravelDistanceMax;
        private bool _spawnUsesEmissionBox;
        private bool _spawnAppliesEmissionBias;
        private float _spawnVerticalEmissionBias;
        private Point _spawnOffsetMin;
        private Point _spawnOffsetMax;
        private Rectangle _spawnArea;

        public int Id { get; private set; }

        public void Initialize(List<IDXObject> frames, Func<Vector2> getTargetPosition,
            float offsetX, float offsetY, int durationMs, int currentTimeMs, AnimationEffects.FollowAnimationOptions options, Random random)
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
            _lastFollowUpdateTime = currentTimeMs;
            _generationPoints = options?.GenerationPoints ?? Array.Empty<Vector2>();
            _currentGenerationPointIndex = 0;
            _thetaDegrees = options?.ThetaDegrees ?? 0;
            _updateIntervalMs = Math.Max(1, options?.UpdateIntervalMs ?? 100);
            _radius = Math.Max(0f, options?.Radius ?? 0f);
            _spawnFrameVariants = options?.SpawnFrameVariants;
            _spawnRelativeToTarget = options?.SpawnRelativeToTarget ?? true;
            _spawnDurationMs = Math.Max(0, options?.SpawnDurationMs ?? 0);
            _spawnTravelDistanceMin = Math.Max(0f, options?.SpawnTravelDistanceMin ?? 0f);
            _spawnTravelDistanceMax = Math.Max(_spawnTravelDistanceMin, options?.SpawnTravelDistanceMax ?? _spawnTravelDistanceMin);
            _spawnUsesEmissionBox = options?.SpawnUsesEmissionBox ?? false;
            _spawnAppliesEmissionBias = options?.SpawnAppliesEmissionBias ?? false;
            _spawnVerticalEmissionBias = options?.SpawnVerticalEmissionBias ?? 0f;
            _spawnOffsetMin = options?.SpawnOffsetMin ?? Point.Zero;
            _spawnOffsetMax = options?.SpawnOffsetMax ?? Point.Zero;
            _spawnArea = options?.SpawnArea ?? Rectangle.Empty;
            _currentAngleDegrees = options?.RandomizeStartupAngle == true
                ? random?.Next(0, 360) ?? 0
                : 0;
            _followOffset = AnimationEffects.ResolveFollowGenerationPointOffset(
                _generationPoints,
                _currentGenerationPointIndex,
                _radius,
                _currentAngleDegrees,
                out _currentGenerationPointIndex,
                out _currentAngleDegrees);
            _nextSpawnTime = currentTimeMs;
        }

        public bool Update(AnimationEffects effects, int currentTimeMs, Random random)
        {
            // Check duration
            if (_duration > 0 && currentTimeMs - _startTime > _duration)
                return false;

            // Update animation frame
            if (AnimationEffects.HasFrames(_frames))
            {
                IDXObject frame = _frames[_currentFrame];
                if (currentTimeMs - _lastFrameTime > frame.Delay)
                {
                    _currentFrame = (_currentFrame + 1) % _frames.Count;
                    _lastFrameTime = currentTimeMs;
                }
            }

            while (_nextSpawnTime <= currentTimeMs)
            {
                bool hasGenerationPoints = _generationPoints != null && _generationPoints.Count > 0;
                int spawnAngleDegrees = AnimationEffects.ResolveFollowSpawnAngleDegrees(
                    hasGenerationPoints,
                    _currentAngleDegrees,
                    _thetaDegrees,
                    random);
                _followOffset = AnimationEffects.ResolveFollowGenerationPointOffset(
                    _generationPoints,
                    _currentGenerationPointIndex,
                    _radius,
                    spawnAngleDegrees,
                    out _currentGenerationPointIndex,
                    out _currentAngleDegrees);

                if (!hasGenerationPoints)
                {
                    _currentAngleDegrees = _thetaDegrees != 0
                        ? AnimationEffects.NormalizeFollowAngleDegrees(spawnAngleDegrees + _thetaDegrees)
                        : AnimationEffects.NormalizeFollowAngleDegrees(spawnAngleDegrees);
                }

                if (effects != null && AnimationEffects.HasFrameVariants(_spawnFrameVariants))
                {
                    int variantIndex = random?.Next(0, _spawnFrameVariants.Count) ?? 0;
                    List<IDXObject> variantFrames = _spawnFrameVariants[variantIndex];
                    if (AnimationEffects.HasFrames(variantFrames))
                    {
                        Vector2 targetPosition = _getTargetPosition();
                        int resolvedDurationMs = AnimationEffects.ResolveFollowSpawnDurationMs(variantFrames, _spawnDurationMs);
                        if (resolvedDurationMs > 0)
                        {
                            int particleAngleDegrees = AnimationEffects.ResolveFollowParticleAngleDegrees(_followOffset, spawnAngleDegrees);
                            Vector2 randomOffset = AnimationEffects.ResolveFollowRandomOffset(
                                _spawnOffsetMin,
                                _spawnOffsetMax,
                                random);
                            bool useClientOffsetPath = _spawnUsesEmissionBox
                                || _spawnAppliesEmissionBias
                                || _spawnOffsetMin != Point.Zero
                                || _spawnOffsetMax != Point.Zero;
                            Vector2 particleStartOffset = useClientOffsetPath
                                ? AnimationEffects.ResolveFollowParticleStartOffset(
                                    _followOffset,
                                    hasGenerationPoints,
                                    particleAngleDegrees,
                                     randomOffset,
                                     _spawnArea,
                                     _spawnUsesEmissionBox,
                                     _spawnAppliesEmissionBias,
                                     _spawnVerticalEmissionBias,
                                     random)
                                : _followOffset;
                            Vector2 particleEndOffset;
                            if (useClientOffsetPath)
                            {
                                particleEndOffset = AnimationEffects.ResolveFollowParticleEndOffset(
                                    particleStartOffset,
                                    particleAngleDegrees,
                                    randomOffset,
                                    _spawnUsesEmissionBox);
                            }
                            else
                            {
                                float particleTravelDistance = AnimationEffects.ResolveFollowParticleTravelDistance(
                                    _spawnTravelDistanceMin,
                                    _spawnTravelDistanceMax,
                                    random);
                                particleEndOffset = AnimationEffects.ResolveFollowParticleEndOffset(
                                    particleStartOffset,
                                    particleAngleDegrees,
                                    particleTravelDistance);
                            }
                            effects.AddFollowParticle(
                                Id,
                                variantFrames,
                                _getTargetPosition,
                                targetPosition,
                                _spawnRelativeToTarget,
                                _offsetX,
                                _offsetY,
                                startOffset: particleStartOffset,
                                endOffset: particleEndOffset,
                                resolvedDurationMs,
                                _nextSpawnTime);
                        }
                    }
                }

                _lastFollowUpdateTime = _nextSpawnTime;
                _nextSpawnTime += _updateIntervalMs;
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            if (!AnimationEffects.HasFrames(_frames))
            {
                return;
            }

            Vector2 targetPos = _getTargetPosition();
            IDXObject frame = _frames[_currentFrame];

            // Use the object's built-in DrawObject method
            float drawX = targetPos.X + _offsetX + _followOffset.X;
            float drawY = targetPos.Y + _offsetY + _followOffset.Y;
            int drawShiftX = -(int)drawX - mapShiftX;
            int drawShiftY = -(int)drawY - mapShiftY;

            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, false, null);
        }
    }

    internal sealed class FollowParticleAnimation
    {
        private List<IDXObject> _frames;
        private Func<Vector2> _getTargetPosition;
        private Vector2 _capturedTargetPosition;
        private bool _relativeToTarget;
        private float _offsetX;
        private float _offsetY;
        private Vector2 _startOffset;
        private Vector2 _endOffset;
        private int _startTime;
        private int _duration;
        private int _currentFrame;
        private int _lastFrameTime;

        public int FollowRegistrationId { get; private set; }

        public void Initialize(
            int followRegistrationId,
            List<IDXObject> frames,
            Func<Vector2> getTargetPosition,
            Vector2 capturedTargetPosition,
            bool relativeToTarget,
            float offsetX,
            float offsetY,
            Vector2 startOffset,
            Vector2 endOffset,
            int durationMs,
            int currentTimeMs)
        {
            FollowRegistrationId = followRegistrationId;
            _frames = frames;
            _getTargetPosition = getTargetPosition;
            _capturedTargetPosition = capturedTargetPosition;
            _relativeToTarget = relativeToTarget;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _startOffset = startOffset;
            _endOffset = endOffset;
            _startTime = currentTimeMs;
            _duration = Math.Max(1, durationMs);
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
        }

        public bool Update(int currentTimeMs)
        {
            if (_duration > 0 && currentTimeMs - _startTime >= _duration)
            {
                return false;
            }

            if (!AnimationEffects.HasFrames(_frames))
            {
                return false;
            }

            IDXObject frame = _frames[_currentFrame];
            if (currentTimeMs - _lastFrameTime > frame.Delay)
            {
                _currentFrame = (_currentFrame + 1) % _frames.Count;
                _lastFrameTime = currentTimeMs;
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY, int currentTimeMs)
        {
            if (!AnimationEffects.HasFrames(_frames))
            {
                return;
            }

            float progress = _duration <= 0
                ? 1f
                : MathHelper.Clamp((float)(currentTimeMs - _startTime) / _duration, 0f, 1f);
            Vector2 anchorPosition = AnimationEffects.ResolveFollowSpawnAnchorPosition(
                _getTargetPosition(),
                _capturedTargetPosition,
                _relativeToTarget);
            Vector2 animatedOffset = Vector2.Lerp(_startOffset, _endOffset, progress);
            IDXObject frame = _frames[_currentFrame];
            float drawX = anchorPosition.X + _offsetX + animatedOffset.X;
            float drawY = anchorPosition.Y + _offsetY + animatedOffset.Y;
            int drawShiftX = -(int)drawX - mapShiftX;
            int drawShiftY = -(int)drawY - mapShiftY;
            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, false, null);
        }
    }

    internal sealed class UserStateAnimation
    {
        private enum UserStatePhase
        {
            Start,
            Repeat,
            End
        }

        private List<IDXObject> _startFrames;
        private List<IDXObject> _repeatFrames;
        private List<IDXObject> _endFrames;
        private Func<Vector2> _getTargetPosition;
        private float _offsetX;
        private float _offsetY;
        private int _currentFrame;
        private int _lastFrameTime;
        private bool _finished;
        private UserStatePhase _phase;

        public int RegistrationKey { get; private set; }
        public int OwnerId { get; private set; }

        public void Initialize(
            int registrationKey,
            int ownerId,
            List<IDXObject> startFrames,
            List<IDXObject> repeatFrames,
            List<IDXObject> endFrames,
            Func<Vector2> getTargetPosition,
            float offsetX,
            float offsetY,
            int currentTimeMs)
        {
            RegistrationKey = registrationKey;
            OwnerId = ownerId;
            _startFrames = startFrames;
            _repeatFrames = repeatFrames;
            _endFrames = endFrames;
            _getTargetPosition = getTargetPosition;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
            _finished = false;
            _phase = AnimationEffects.HasFrames(startFrames)
                ? UserStatePhase.Start
                : AnimationEffects.HasFrames(repeatFrames)
                    ? UserStatePhase.Repeat
                    : UserStatePhase.End;
        }

        public bool BeginEndPhase(int currentTimeMs)
        {
            if (_finished)
            {
                return false;
            }

            if (!AnimationEffects.HasFrames(_endFrames))
            {
                _finished = true;
                return false;
            }

            _phase = UserStatePhase.End;
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
            return true;
        }

        public bool Update(int currentTimeMs)
        {
            if (_finished)
            {
                return false;
            }

            List<IDXObject> frames = GetCurrentFrames();
            if (!AnimationEffects.HasFrames(frames))
            {
                return AdvancePhase(currentTimeMs);
            }

            IDXObject frame = frames[_currentFrame];
            if (currentTimeMs - _lastFrameTime > frame.Delay)
            {
                _currentFrame++;
                _lastFrameTime = currentTimeMs;
                if (_currentFrame >= frames.Count)
                {
                    return AdvancePhase(currentTimeMs);
                }
            }

            return true;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            if (_finished)
            {
                return;
            }

            List<IDXObject> frames = GetCurrentFrames();
            if (!AnimationEffects.HasFrames(frames) || _currentFrame < 0 || _currentFrame >= frames.Count)
            {
                return;
            }

            Vector2 targetPosition = _getTargetPosition();
            IDXObject frame = frames[_currentFrame];
            int drawShiftX = -(int)(targetPosition.X + _offsetX) - mapShiftX;
            int drawShiftY = -(int)(targetPosition.Y + _offsetY) - mapShiftY;
            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, false, null);
        }

        private bool AdvancePhase(int currentTimeMs)
        {
            switch (_phase)
            {
                case UserStatePhase.Start:
                    if (AnimationEffects.HasFrames(_repeatFrames))
                    {
                        _phase = UserStatePhase.Repeat;
                        _currentFrame = 0;
                        _lastFrameTime = currentTimeMs;
                        return true;
                    }

                    if (AnimationEffects.HasFrames(_endFrames))
                    {
                        _phase = UserStatePhase.End;
                        _currentFrame = 0;
                        _lastFrameTime = currentTimeMs;
                        return true;
                    }

                    _finished = true;
                    return false;

                case UserStatePhase.Repeat:
                    if (!AnimationEffects.HasFrames(_repeatFrames))
                    {
                        return AdvanceEndOrFinish(currentTimeMs);
                    }

                    _currentFrame = 0;
                    _lastFrameTime = currentTimeMs;
                    return true;

                case UserStatePhase.End:
                default:
                    _finished = true;
                    return false;
            }
        }

        private bool AdvanceEndOrFinish(int currentTimeMs)
        {
            if (AnimationEffects.HasFrames(_endFrames))
            {
                _phase = UserStatePhase.End;
                _currentFrame = 0;
                _lastFrameTime = currentTimeMs;
                return true;
            }

            _finished = true;
            return false;
        }

        private List<IDXObject> GetCurrentFrames()
        {
            return _phase switch
            {
                UserStatePhase.Start => _startFrames,
                UserStatePhase.Repeat => _repeatFrames,
                UserStatePhase.End => _endFrames,
                _ => null
            };
        }
    }

    internal sealed class AreaAnimationRegistration
    {
        private static int _nextId;

        private List<IDXObject> _frames;
        private Rectangle _area;
        private int _effectiveWidth;
        private int _effectiveHeight;
        private int _updateIntervalMs;
        private int _remainingUpdates;
        private int _nextUpdateAt;
        private int _expiresAt;
        private Action _onSpawn;

        public int Id { get; private set; }

        public void Initialize(
            List<IDXObject> frames,
            Rectangle area,
            int updateIntervalMs,
            int updateCount,
            int updateNextMs,
            int durationMs,
            int currentTimeMs,
            Action onSpawn)
        {
            Id = ++_nextId;
            _frames = frames;
            _area = area;
            _effectiveWidth = Math.Max(1, area.Width * 5 / 6);
            _effectiveHeight = Math.Max(1, area.Height / 3);
            _updateIntervalMs = Math.Max(1, updateIntervalMs);
            _remainingUpdates = Math.Max(0, updateCount);
            _nextUpdateAt = currentTimeMs + Math.Max(0, updateNextMs);
            _expiresAt = durationMs > 0 ? currentTimeMs + durationMs : int.MaxValue;
            _onSpawn = onSpawn;
        }

        public bool Update(AnimationEffects effects, int currentTimeMs, Random random)
        {
            if (effects == null || !AnimationEffects.HasFrames(_frames))
            {
                return false;
            }

            if (_remainingUpdates == 0 || currentTimeMs > _expiresAt)
            {
                return false;
            }

            while (_remainingUpdates > 0 && _nextUpdateAt <= currentTimeMs && _nextUpdateAt <= _expiresAt)
            {
                int scheduledUpdateTime = _nextUpdateAt;
                float x = _area.Left + random.Next(_effectiveWidth);
                float y = _area.Top + random.Next(_effectiveHeight);
                effects.AddOneTime(_frames, x, y, flip: false, scheduledUpdateTime, zOrder: 1);
                _onSpawn?.Invoke();
                _remainingUpdates--;
                _nextUpdateAt += _updateIntervalMs;
            }

            return _remainingUpdates > 0 && _nextUpdateAt <= _expiresAt;
        }
    }

    #endregion
}
