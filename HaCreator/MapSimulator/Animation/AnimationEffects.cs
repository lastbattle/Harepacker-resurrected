using HaSharedLibrary.Render.DX;
using HaCreator.MapSimulator.Interaction;
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
        internal sealed class SecondaryMotionBlurLayerStackEntryTag
        {
            public SecondaryMotionBlurLayerStackEntryTag(
                int drawOrder,
                int sourceLayerCode = -1,
                int sourceLayerCaptureOrder = -1,
                int simulatedLayerHandleId = 0)
            {
                DrawOrder = Math.Max(0, drawOrder);
                SourceLayerCode = sourceLayerCode;
                SourceLayerCaptureOrder = sourceLayerCaptureOrder;
                SimulatedLayerHandleId = Math.Max(0, simulatedLayerHandleId);
            }

            public int DrawOrder { get; }
            public int SourceLayerCode { get; }
            public int SourceLayerCaptureOrder { get; }
            public int SimulatedLayerHandleId { get; }
        }

        internal static bool IsSecondaryMotionBlurLayerStack(IReadOnlyList<IDXObject> frames)
        {
            return frames != null
                && frames.Count > 0
                && frames[0]?.Tag is SecondaryMotionBlurLayerStackEntryTag;
        }

        internal static int ResolveSecondaryMotionBlurLayerStackDrawOrder(IDXObject frame, int fallbackOrder)
        {
            return frame?.Tag is SecondaryMotionBlurLayerStackEntryTag metadata
                ? metadata.DrawOrder
                : fallbackOrder;
        }

        internal sealed class FollowAnimationOptions
        {
            public IReadOnlyList<Vector2> GenerationPoints { get; init; }
            public int ThetaDegrees { get; init; }
            public float Radius { get; init; }
            public bool RandomizeStartupAngle { get; init; }
            public Func<bool> GetTargetFlip { get; init; }
            public Func<bool> IsTargetMoveAction { get; init; }
            public bool SuppressTargetFlip { get; init; }
            public bool SpawnOnlyOnTargetMove { get; init; }
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
            public int SpawnZOrder { get; init; }
        }

        internal sealed class SecondaryMotionBlurAnimationState
        {
            public bool TerminateRequested { get; set; }
            public bool IsTerminated { get; internal set; }
            public int SimulatedAnimationStateId { get; internal set; }
            public IReadOnlyDictionary<int, int> SimulatedLayerHandleIdsByLayerCode { get; internal set; }
                = new Dictionary<int, int>();

            internal SecondaryMotionBlurAnimationStateTrace CaptureTrace()
            {
                return new SecondaryMotionBlurAnimationStateTrace(
                    SimulatedAnimationStateId,
                    TerminateRequested,
                    IsTerminated,
                    SimulatedLayerHandleIdsByLayerCode);
            }
        }

        internal readonly record struct SecondaryMotionBlurAnimationStateTrace(
            int SimulatedAnimationStateId,
            bool TerminateRequested,
            bool IsTerminated,
            IReadOnlyDictionary<int, int> SimulatedLayerHandleIdsByLayerCode);

        private readonly List<OneTimeAnimation> _oneTimeAnimations = new();
        private readonly List<OneTimeCanvasLayerAnimation> _oneTimeCanvasLayers = new();
        private readonly List<RepeatAnimation> _repeatAnimations = new();
        private readonly List<ChainLightning> _chainLightnings = new();
        private readonly List<FallingAnimation> _fallingAnimations = new();
        private readonly List<FollowAnimation> _followAnimations = new();
        private readonly List<FollowParticleAnimation> _followParticleAnimations = new();
        private readonly List<AreaAnimationRegistration> _areaAnimations = new();
        private readonly List<UserStateAnimation> _userStateAnimations = new();
        private readonly Dictionary<int, SecondaryPrepareAnimation> _secondaryPrepareAnimations = new();
        private readonly List<SecondaryFootholdAnimation> _secondaryFootholdAnimations = new();
        private readonly List<SecondaryHookChainAnimation> _secondaryHookChainAnimations = new();
        private readonly List<SecondaryMotionBlurAnimation> _secondaryMotionBlurAnimations = new();
        private readonly List<SecondaryChainSegmentAnimation> _secondaryChainSegmentAnimations = new();

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
            InsertOneTimeAnimation(anim);
        }

        internal void AddOneTimeAttached(
            List<IDXObject> frames,
            Func<Vector2> getPosition,
            Func<bool> getFlip,
            float fallbackX,
            float fallbackY,
            bool fallbackFlip,
            int currentTimeMs,
            int zOrder = 0,
            int initialElapsedMs = 0)
        {
            if (frames == null || frames.Count == 0) return;

            OneTimeAnimation anim = _oneTimePool.Count > 0 ? _oneTimePool.Dequeue() : new OneTimeAnimation();
            anim.Initialize(
                frames,
                fallbackX,
                fallbackY,
                fallbackFlip,
                currentTimeMs,
                zOrder,
                getPosition,
                getFlip,
                AnimationOneTimeOwner.Generic,
                AnimationOneTimePlaybackMode.Default,
                sourceUol: null,
                usesOverlayParent: false,
                recoveredRegistrationTrace: null,
                initialElapsedMs: initialElapsedMs);
            InsertOneTimeAnimation(anim);
        }

        internal void AddFullChargedAngerGauge(
            List<IDXObject> frames,
            string sourceUol,
            Func<Vector2> getOrigin,
            float fallbackX,
            float fallbackY,
            int currentTimeMs,
            int zOrder = 1)
        {
            if (frames == null || frames.Count == 0 || string.IsNullOrWhiteSpace(sourceUol)) return;

            OneTimeAnimation anim = _oneTimePool.Count > 0 ? _oneTimePool.Dequeue() : new OneTimeAnimation();
            OneTimeAnimationRecoveredRegistrationTrace registrationTrace =
                OneTimeAnimationRecoveredRegistrationTrace.CreateFullChargedAngerGauge(sourceUol);
            anim.Initialize(
                frames,
                fallbackX,
                fallbackY,
                registrationTrace.LoadLayerFlip,
                currentTimeMs,
                zOrder,
                getOrigin,
                getFlip: null,
                AnimationOneTimeOwner.FullChargedAngerGauge,
                AnimationOneTimePlaybackMode.GA_STOP,
                sourceUol,
                usesOverlayParent: true,
                registrationTrace);
            InsertOneTimeAnimation(anim);
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
            InsertOneTimeAnimation(anim);
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
            InsertOneTimeAnimation(anim);
        }

        private void InsertOneTimeAnimation(OneTimeAnimation animation)
        {
            if (animation == null)
            {
                return;
            }

            int insertIndex = _oneTimeAnimations.Count;
            while (insertIndex > 0 && _oneTimeAnimations[insertIndex - 1].ZOrder > animation.ZOrder)
            {
                insertIndex--;
            }

            _oneTimeAnimations.Insert(insertIndex, animation);
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
            CanvasLayerRecoveredLayerSettings? recoveredLayerSettings = null,
            CanvasLayerRecoveredRegistrationTrace? recoveredRegistrationTrace = null,
            CanvasLayerRecoveredOwnerTrace? recoveredOwnerTrace = null)
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
                registration.RecoveredLayerSettings,
                recoveredRegistrationTrace,
                recoveredOwnerTrace);
            InsertOneTimeCanvasLayer(anim);
        }

        internal void RegisterOneTimeCanvasLayer(
            Texture2D canvasTexture,
            int currentTimeMs,
            PreparedOneTimeCanvasLayerRegistration registration,
            Texture2D overlayTexture = null,
            bool ownsCanvasTexture = false,
            AnimationCanvasLayerOwner owner = AnimationCanvasLayerOwner.Generic)
        {
            if (canvasTexture == null)
            {
                return;
            }

            OneTimeCanvasLayerAnimation anim = _oneTimeCanvasLayerPool.Count > 0
                ? _oneTimeCanvasLayerPool.Dequeue()
                : new OneTimeCanvasLayerAnimation();
            anim.Initialize(
                canvasTexture,
                overlayTexture,
                registration.Left,
                registration.Top,
                currentTimeMs,
                registration.InsertDescriptors,
                ownsCanvasTexture,
                owner,
                registration.RecoveredLayerSettings,
                registration.RecoveredRegistrationTrace,
                registration.RecoveredOwnerTrace);
            InsertOneTimeCanvasLayer(anim);
        }

        internal IReadOnlyList<OneTimeCanvasLayerAnimation> OneTimeCanvasLayers => _oneTimeCanvasLayers;

        private void InsertOneTimeCanvasLayer(OneTimeCanvasLayerAnimation animation)
        {
            if (animation == null)
            {
                return;
            }

            int insertIndex = _oneTimeCanvasLayers.Count;
            while (insertIndex > 0
                && _oneTimeCanvasLayers[insertIndex - 1].RecoveredLayerSettings.LayerPriorityValue > animation.RecoveredLayerSettings.LayerPriorityValue)
            {
                insertIndex--;
            }

            _oneTimeCanvasLayers.Insert(insertIndex, animation);
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

        #region Secondary Skill Animation Owners

        public int RegisterPrepareAnimation(
            int ownerId,
            List<IDXObject> primaryFrames,
            List<IDXObject> secondaryFrames,
            Func<Vector2> getOwnerPosition,
            Func<bool> getOwnerFlip,
            Vector2 fallbackPosition,
            bool fallbackFlip,
            int currentTimeMs,
            int durationMs)
        {
            if (ownerId <= 0 || (!HasFrames(primaryFrames) && !HasFrames(secondaryFrames)))
            {
                return -1;
            }

            var animation = new SecondaryPrepareAnimation();
            animation.Initialize(ownerId, primaryFrames, secondaryFrames, getOwnerPosition, getOwnerFlip, fallbackPosition, fallbackFlip, currentTimeMs, durationMs);
            _secondaryPrepareAnimations[ownerId] = animation;
            return ownerId;
        }

        public bool RemovePrepareAnimation(int ownerId)
        {
            return ownerId > 0 && _secondaryPrepareAnimations.Remove(ownerId);
        }

        public int RegisterFootholdAnimation(
            List<IDXObject> frames,
            Rectangle area,
            int tStartDelay,
            int tDuration,
            int updateIntervalMs,
            int currentTimeMs,
            bool randomPosition)
        {
            if (!HasFrames(frames) || area.Width <= 0 || area.Height <= 0)
            {
                return -1;
            }

            var animation = new SecondaryFootholdAnimation();
            animation.Initialize(frames, area, tStartDelay, tDuration, updateIntervalMs, currentTimeMs, randomPosition, _random);
            _secondaryFootholdAnimations.Add(animation);
            return animation.Id;
        }

        public int RegisterHookingChainAnimation(
            List<IDXObject> hookFrames,
            List<IDXObject> chainFrames,
            int ownerId,
            Func<Vector2> getOwnerPosition,
            Vector2 fallbackOwnerPosition,
            Vector2? targetPosition,
            bool left,
            int attackTimeMs,
            int currentTimeMs,
            int zOrder,
            int initialElapsedMs = 0)
        {
            if (!HasFrames(hookFrames) && !HasFrames(chainFrames))
            {
                return -1;
            }

            var animation = new SecondaryHookChainAnimation();
            animation.Initialize(hookFrames, chainFrames, ownerId, getOwnerPosition, fallbackOwnerPosition, targetPosition, left, attackTimeMs, currentTimeMs, zOrder, initialElapsedMs);
            _secondaryHookChainAnimations.Add(animation);
            return animation.Id;
        }

        internal int RegisterMotionBlurAnimation(
            List<IDXObject> frames,
            Func<Vector2> getOwnerPosition,
            Func<bool> getOwnerFlip,
            Vector2 fallbackPosition,
            bool fallbackFlip,
            int delayMs,
            int intervalMs,
            byte alpha,
            int currentTimeMs,
            int durationMs,
            bool follow,
            int snapshotRetentionMs = 0,
            bool ownsFrameTextures = false,
            Func<int, List<IDXObject>> snapshotFrameFactory = null,
            Func<int, Vector2?> snapshotPositionFactory = null,
            Func<int, bool?> snapshotFlipFactory = null,
            SecondaryMotionBlurAnimationState animationState = null)
        {
            if (!HasFrames(frames))
            {
                return -1;
            }

            var animation = new SecondaryMotionBlurAnimation();
            animation.Initialize(
                frames,
                getOwnerPosition,
                getOwnerFlip,
                fallbackPosition,
                fallbackFlip,
                delayMs,
                intervalMs,
                alpha,
                currentTimeMs,
                durationMs,
                follow,
                snapshotRetentionMs,
                ownsFrameTextures,
                snapshotFrameFactory,
                snapshotPositionFactory,
                snapshotFlipFactory,
                animationState);
            _secondaryMotionBlurAnimations.Add(animation);
            return animation.Id;
        }

        public bool RemoveMotionBlurAnimation(int id)
        {
            for (int i = _secondaryMotionBlurAnimations.Count - 1; i >= 0; i--)
            {
                if (_secondaryMotionBlurAnimations[i].Id != id)
                {
                    continue;
                }

                _secondaryMotionBlurAnimations[i].MarkTerminated();
                _secondaryMotionBlurAnimations[i].Dispose();
                _secondaryMotionBlurAnimations.RemoveAt(i);
                return true;
            }

            return false;
        }

        public bool TerminateMotionBlurAnimation(int id, int currentTimeMs)
        {
            for (int i = _secondaryMotionBlurAnimations.Count - 1; i >= 0; i--)
            {
                if (_secondaryMotionBlurAnimations[i].Id != id)
                {
                    continue;
                }

                _secondaryMotionBlurAnimations[i].RequestTermination(currentTimeMs);
                return true;
            }

            return false;
        }

        public int RegisterSecondaryChainLightningAnimation(
            IReadOnlyList<List<IDXObject>> frameVariants,
            Vector2 start,
            Vector2 end,
            int tStart,
            int tEnd,
            int zOrder,
            bool ordered,
            bool tesla,
            int registrationKey = 0)
        {
            if (start == end || tEnd <= tStart)
            {
                return -1;
            }

            var animation = new SecondaryChainSegmentAnimation();
            animation.Initialize(frameVariants, start, end, tStart, tEnd, zOrder, ordered, tesla, registrationKey, _random);
            _secondaryChainSegmentAnimations.Add(animation);
            AddLightningBolt(
                start,
                end,
                tesla ? new Color(130, 220, 255) : new Color(100, 150, 255),
                Math.Max(1, tEnd - tStart),
                tStart,
                tesla ? 4f : 3f,
                Math.Max(4, animation.SegmentCount));
            return animation.Id;
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
            int currentTimeMs,
            int initialElapsedMs = 0)
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
                currentTimeMs,
                initialElapsedMs);
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
            int currentTimeMs,
            int initialElapsedMs = 0)
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
            animation.Initialize(
                registrationKey,
                ownerId,
                startFrames,
                repeatFrames,
                endFrames,
                getTargetPosition,
                offsetX,
                offsetY,
                currentTimeMs,
                initialElapsedMs);
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
                    _oneTimeCanvasLayers[i].Reset();
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

            foreach (int key in new List<int>(_secondaryPrepareAnimations.Keys))
            {
                if (!_secondaryPrepareAnimations[key].Update(currentTimeMs))
                {
                    _secondaryPrepareAnimations.Remove(key);
                }
            }

            for (int i = _secondaryFootholdAnimations.Count - 1; i >= 0; i--)
            {
                if (!_secondaryFootholdAnimations[i].Update(currentTimeMs, _random))
                {
                    _secondaryFootholdAnimations.RemoveAt(i);
                }
            }

            for (int i = _secondaryHookChainAnimations.Count - 1; i >= 0; i--)
            {
                if (!_secondaryHookChainAnimations[i].Update(currentTimeMs))
                {
                    _secondaryHookChainAnimations.RemoveAt(i);
                }
            }

            for (int i = _secondaryMotionBlurAnimations.Count - 1; i >= 0; i--)
            {
                if (!_secondaryMotionBlurAnimations[i].Update(currentTimeMs))
                {
                    _secondaryMotionBlurAnimations[i].MarkTerminated();
                    _secondaryMotionBlurAnimations[i].Dispose();
                    _secondaryMotionBlurAnimations.RemoveAt(i);
                }
            }

            for (int i = _secondaryChainSegmentAnimations.Count - 1; i >= 0; i--)
            {
                if (!_secondaryChainSegmentAnimations[i].Update(currentTimeMs))
                {
                    _secondaryChainSegmentAnimations.RemoveAt(i);
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

            foreach (var anim in _secondaryPrepareAnimations.Values)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY);
            }

            foreach (var anim in _secondaryFootholdAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY);
            }

            foreach (var anim in _secondaryHookChainAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, pixelTexture, mapShiftX, mapShiftY);
            }

            foreach (var anim in _secondaryMotionBlurAnimations)
            {
                anim.Draw(spriteBatch, skeletonRenderer, gameTime, mapShiftX, mapShiftY);
            }

            foreach (var anim in _secondaryChainSegmentAnimations)
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
            _followParticleAnimations.Clear();
            _areaAnimations.Clear();
            _userStateAnimations.Clear();
            _secondaryPrepareAnimations.Clear();
            _secondaryFootholdAnimations.Clear();
            _secondaryHookChainAnimations.Clear();
            for (int i = 0; i < _secondaryMotionBlurAnimations.Count; i++)
            {
                _secondaryMotionBlurAnimations[i].MarkTerminated();
                _secondaryMotionBlurAnimations[i].Dispose();
            }
            _secondaryMotionBlurAnimations.Clear();
            _secondaryChainSegmentAnimations.Clear();
        }

        /// <summary>
        /// Get count of active animations
        /// </summary>
        public int ActiveCount =>
            _oneTimeAnimations.Count + _oneTimeCanvasLayers.Count + _repeatAnimations.Count +
            _chainLightnings.Count + _fallingAnimations.Count +
            _followAnimations.Count + _followParticleAnimations.Count + _areaAnimations.Count +
            _userStateAnimations.Count + SecondarySkillAnimationOwnerCount;

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

        public void ClearSecondarySkillAnimationOwners()
        {
            _secondaryPrepareAnimations.Clear();
            _secondaryFootholdAnimations.Clear();
            _secondaryHookChainAnimations.Clear();
            for (int i = 0; i < _secondaryMotionBlurAnimations.Count; i++)
            {
                _secondaryMotionBlurAnimations[i].MarkTerminated();
                _secondaryMotionBlurAnimations[i].Dispose();
            }
            _secondaryMotionBlurAnimations.Clear();
            _secondaryChainSegmentAnimations.Clear();
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
            Func<bool> getTargetFlip,
            Vector2 capturedTargetPosition,
            bool relativeToTarget,
            bool suppressTargetFlip,
            float offsetX,
            float offsetY,
            Vector2 startOffset,
            Vector2 endOffset,
            int zOrder,
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
                getTargetFlip,
                capturedTargetPosition,
                relativeToTarget,
                suppressTargetFlip,
                offsetX,
                offsetY,
                startOffset,
                endOffset,
                zOrder,
                durationMs,
                currentTimeMs);
            InsertFollowParticleAnimation(particle);
        }

        private void InsertFollowParticleAnimation(FollowParticleAnimation particle)
        {
            if (particle == null)
            {
                return;
            }

            int insertIndex = _followParticleAnimations.Count;
            while (insertIndex > 0 && _followParticleAnimations[insertIndex - 1].ZOrder > particle.ZOrder)
            {
                insertIndex--;
            }

            _followParticleAnimations.Insert(insertIndex, particle);
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
            bool useEmissionBox,
            bool mirrorHorizontal)
        {
            Vector2 travelOffset = ResolveFollowParticleTravelOffset(randomOffset, angleDegrees, useEmissionBox, mirrorHorizontal);
            if (travelOffset.LengthSquared() <= float.Epsilon)
            {
                return emissionOffset;
            }

            return emissionOffset + travelOffset;
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

        internal static float ResolveFollowParticleTravelDistance(Vector2 randomOffset, bool useEmissionBox)
        {
            if (useEmissionBox)
            {
                return randomOffset.Length();
            }

            float distance = randomOffset.Length();
            if (distance > float.Epsilon)
            {
                return distance;
            }

            return Math.Max(Math.Abs(randomOffset.X), Math.Abs(randomOffset.Y));
        }

        internal static Vector2 ResolveFollowParticleTravelOffset(
            Vector2 randomOffset,
            int angleDegrees,
            bool useEmissionBox,
            bool mirrorHorizontal)
        {
            if (useEmissionBox)
            {
                return new Vector2(
                    mirrorHorizontal ? -randomOffset.X : randomOffset.X,
                    randomOffset.Y);
            }

            float travelDistance = ResolveFollowParticleTravelDistance(randomOffset, useEmissionBox: false);
            return travelDistance <= float.Epsilon
                ? Vector2.Zero
                : ResolvePolarFollowOffset(travelDistance, angleDegrees);
        }

        internal static Vector2 ResolveFollowSpawnAnchorPosition(
            Vector2 targetPosition,
            Vector2 capturedTargetPosition,
            bool relativeToTarget)
        {
            return relativeToTarget ? targetPosition : capturedTargetPosition;
        }

        internal static byte ResolveFollowParticleAlpha(int elapsedMs, int durationMs)
        {
            if (durationMs <= 0)
            {
                return byte.MinValue;
            }

            float progress = MathHelper.Clamp((float)elapsedMs / durationMs, 0f, 1f);
            return (byte)Math.Clamp((int)MathF.Round(byte.MaxValue * (1f - progress)), byte.MinValue, byte.MaxValue);
        }

        public int UserStateCount => _userStateAnimations.Count;
        public int AreaAnimationCount => _areaAnimations.Count;
        public int FallingAnimationCount => _fallingAnimations.Count;
        public int FollowAnimationCount => _followAnimations.Count;
        public int FollowParticleCount => _followParticleAnimations.Count;
        public int SecondarySkillAnimationOwnerCount =>
            _secondaryPrepareAnimations.Count
            + _secondaryFootholdAnimations.Count
            + _secondaryHookChainAnimations.Count
            + _secondaryMotionBlurAnimations.Count
            + _secondaryChainSegmentAnimations.Count;
        internal IReadOnlyList<FollowParticleAnimation> FollowParticles => _followParticleAnimations;
        internal IReadOnlyList<OneTimeAnimation> OneTimeAnimations => _oneTimeAnimations;

        #endregion
    }

    #region Animation Classes

    internal static class SecondarySkillAnimationIdSource
    {
        private static int _nextId = 1;

        public static int NextId()
        {
            return _nextId++;
        }
    }

    internal abstract class SecondarySkillFrameAnimation
    {
        protected static int ResolveFrameDelay(IDXObject frame)
        {
            return Math.Max(10, frame?.Delay ?? 100);
        }

        protected static int ResolveFrameIndex(IReadOnlyList<IDXObject> frames, int elapsedMs, bool loop)
        {
            if (frames == null || frames.Count == 0)
            {
                return -1;
            }

            int totalDuration = ResolveFramesDuration(frames);
            if (loop && totalDuration > 0)
            {
                elapsedMs %= totalDuration;
            }

            int cursor = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                cursor += ResolveFrameDelay(frames[i]);
                if (elapsedMs < cursor)
                {
                    return i;
                }
            }

            return loop ? 0 : frames.Count - 1;
        }

        protected static int ResolveFramesDuration(IReadOnlyList<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return 0;
            }

            int duration = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                duration += ResolveFrameDelay(frames[i]);
            }

            return duration;
        }

        protected static void DrawFrame(
            IReadOnlyList<IDXObject> frames,
            int frameIndex,
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            GameTime gameTime,
            Vector2 position,
            bool flip,
            int mapShiftX,
            int mapShiftY)
        {
            if (frames == null || frameIndex < 0 || frameIndex >= frames.Count)
            {
                return;
            }

            int drawShiftX = -(int)MathF.Round(position.X) - mapShiftX;
            int drawShiftY = -(int)MathF.Round(position.Y) - mapShiftY;
            frames[frameIndex]?.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, flip, null);
        }
    }

    internal sealed class SecondaryPrepareAnimation : SecondarySkillFrameAnimation
    {
        private int _ownerId;
        private List<IDXObject> _primaryFrames;
        private List<IDXObject> _secondaryFrames;
        private Func<Vector2> _positionResolver;
        private Func<bool> _flipResolver;
        private Vector2 _fallbackPosition;
        private bool _fallbackFlip;
        private int _startTime;
        private int _durationMs;
        private int _lastUpdateTime;

        public void Initialize(
            int ownerId,
            List<IDXObject> primaryFrames,
            List<IDXObject> secondaryFrames,
            Func<Vector2> getOwnerPosition,
            Func<bool> getOwnerFlip,
            Vector2 fallbackPosition,
            bool fallbackFlip,
            int currentTimeMs,
            int durationMs)
        {
            _ownerId = ownerId;
            _primaryFrames = primaryFrames;
            _secondaryFrames = secondaryFrames;
            _positionResolver = getOwnerPosition;
            _flipResolver = getOwnerFlip;
            _fallbackPosition = fallbackPosition;
            _fallbackFlip = fallbackFlip;
            _startTime = currentTimeMs;
            _lastUpdateTime = currentTimeMs;
            _durationMs = durationMs > 0
                ? durationMs
                : Math.Max(ResolveFramesDuration(primaryFrames), ResolveFramesDuration(secondaryFrames));
        }

        public bool Update(int currentTimeMs)
        {
            _lastUpdateTime = currentTimeMs;
            return _ownerId > 0 && (_durationMs <= 0 || currentTimeMs - _startTime < _durationMs);
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            Vector2 position = _positionResolver?.Invoke() ?? _fallbackPosition;
            bool flip = _flipResolver?.Invoke() ?? _fallbackFlip;
            int elapsed = Math.Max(0, _lastUpdateTime - _startTime);
            DrawFrame(_primaryFrames, ResolveFrameIndex(_primaryFrames, elapsed, loop: true), spriteBatch, skeletonRenderer, gameTime, position, flip, mapShiftX, mapShiftY);
            DrawFrame(_secondaryFrames, ResolveFrameIndex(_secondaryFrames, elapsed, loop: true), spriteBatch, skeletonRenderer, gameTime, position, flip, mapShiftX, mapShiftY);
        }
    }

    internal sealed class SecondaryFootholdAnimation : SecondarySkillFrameAnimation
    {
        private readonly List<(Vector2 Position, int StartTime)> _layers = new();
        private List<IDXObject> _frames;
        private Rectangle _area;
        private int _startTime;
        private int _endTime;
        private int _nextSpawnTime;
        private int _updateIntervalMs;
        private bool _randomPosition;
        private int _lastUpdateTime;

        public int Id { get; } = SecondarySkillAnimationIdSource.NextId();

        public void Initialize(List<IDXObject> frames, Rectangle area, int tStartDelay, int tDuration, int updateIntervalMs, int currentTimeMs, bool randomPosition, Random random)
        {
            _frames = frames;
            _area = area;
            _startTime = currentTimeMs + Math.Max(0, tStartDelay);
            _endTime = _startTime + Math.Max(1, tDuration);
            _nextSpawnTime = _startTime;
            _lastUpdateTime = currentTimeMs;
            _updateIntervalMs = Math.Max(30, updateIntervalMs);
            _randomPosition = randomPosition;
            TrySpawn(_startTime, random);
        }

        public bool Update(int currentTimeMs, Random random)
        {
            _lastUpdateTime = currentTimeMs;
            while (currentTimeMs >= _nextSpawnTime && _nextSpawnTime < _endTime)
            {
                TrySpawn(_nextSpawnTime, random);
                _nextSpawnTime += _updateIntervalMs;
            }

            int frameDuration = ResolveFramesDuration(_frames);
            _layers.RemoveAll(layer => currentTimeMs - layer.StartTime >= frameDuration);
            return currentTimeMs < _endTime || _layers.Count > 0;
        }

        private void TrySpawn(int startTime, Random random)
        {
            if (_area.Width <= 0 || _area.Height <= 0)
            {
                return;
            }

            Vector2 position = _randomPosition
                ? new Vector2(_area.Left + (random?.Next(_area.Width) ?? 0), _area.Top + (random?.Next(_area.Height) ?? 0))
                : new Vector2(_area.Left + (_area.Width * 0.5f), _area.Top + (_area.Height * 0.5f));
            _layers.Add((position, startTime));
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            int currentTime = _lastUpdateTime;
            foreach ((Vector2 position, int startTime) in _layers)
            {
                if (currentTime < startTime)
                {
                    continue;
                }

                int frameIndex = ResolveFrameIndex(_frames, Math.Max(0, currentTime - startTime), loop: false);
                DrawFrame(_frames, frameIndex, spriteBatch, skeletonRenderer, gameTime, position, flip: false, mapShiftX, mapShiftY);
            }
        }
    }

    internal sealed class SecondaryHookChainAnimation : SecondarySkillFrameAnimation
    {
        private List<IDXObject> _hookFrames;
        private List<IDXObject> _chainFrames;
        private Func<Vector2> _ownerPositionResolver;
        private Vector2 _fallbackOwnerPosition;
        private Vector2 _targetPosition;
        private bool _left;
        private int _startTime;
        private int _stretchEndTime;
        private int _endTime;
        private int _lastUpdateTime;

        public int Id { get; } = SecondarySkillAnimationIdSource.NextId();

        public void Initialize(List<IDXObject> hookFrames, List<IDXObject> chainFrames, int ownerId, Func<Vector2> getOwnerPosition, Vector2 fallbackOwnerPosition, Vector2? targetPosition, bool left, int attackTimeMs, int currentTimeMs, int zOrder, int initialElapsedMs = 0)
        {
            _hookFrames = hookFrames;
            _chainFrames = chainFrames;
            _ownerPositionResolver = getOwnerPosition;
            _fallbackOwnerPosition = fallbackOwnerPosition;
            _left = left;
            int elapsedMs = Math.Max(0, initialElapsedMs);
            _startTime = unchecked(currentTimeMs - elapsedMs);
            _lastUpdateTime = currentTimeMs;
            _stretchEndTime = attackTimeMs - 100;
            _endTime = _stretchEndTime + 1000;
            Vector2 userPoint = ResolveUserPoint();
            _targetPosition = targetPosition ?? new Vector2(userPoint.X + (left ? -300f : 300f), userPoint.Y);
        }

        public bool Update(int currentTimeMs)
        {
            _lastUpdateTime = currentTimeMs;
            return currentTimeMs < _endTime;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, Texture2D pixelTexture, int mapShiftX, int mapShiftY)
        {
            int currentTime = _lastUpdateTime;
            Vector2 userPoint = ResolveUserPoint();
            float progress = _stretchEndTime <= _startTime
                ? 1f
                : MathHelper.Clamp((float)(currentTime - _startTime) / (_stretchEndTime - _startTime), 0f, 1f);
            Vector2 tip = Vector2.Lerp(userPoint, _targetPosition, progress);
            if (pixelTexture != null)
            {
                DrawLine(spriteBatch, pixelTexture, userPoint + new Vector2(mapShiftX, mapShiftY), tip + new Vector2(mapShiftX, mapShiftY), 3f, Color.White * 0.8f);
            }

            int elapsed = Math.Max(0, currentTime - _startTime);
            DrawFrame(_chainFrames, ResolveFrameIndex(_chainFrames, elapsed, loop: true), spriteBatch, skeletonRenderer, gameTime, (userPoint + tip) * 0.5f, _left, mapShiftX, mapShiftY);
            DrawFrame(_hookFrames, ResolveFrameIndex(_hookFrames, elapsed, loop: true), spriteBatch, skeletonRenderer, gameTime, tip, _left, mapShiftX, mapShiftY);
        }

        private Vector2 ResolveUserPoint()
        {
            Vector2 owner = _ownerPositionResolver?.Invoke() ?? _fallbackOwnerPosition;
            return new Vector2(owner.X + (_left ? -25f : 25f), owner.Y - 20f);
        }

        private static void DrawLine(SpriteBatch spriteBatch, Texture2D texture, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length < 0.01f)
            {
                return;
            }

            spriteBatch.Draw(texture, start, null, color, (float)Math.Atan2(delta.Y, delta.X), new Vector2(0, 0.5f), new Vector2(length, width), SpriteEffects.None, 0);
        }
    }

    internal sealed class SecondaryMotionBlurAnimation : SecondarySkillFrameAnimation
    {
        private readonly List<MotionBlurSnapshot> _snapshots = new();
        private List<IDXObject> _fallbackFrames;
        private Func<Vector2> _positionResolver;
        private Func<bool> _flipResolver;
        private Func<int, List<IDXObject>> _snapshotFrameFactory;
        private Vector2 _fallbackPosition;
        private bool _fallbackFlip;
        private int _nextUpdateTime;
        private int _endTime;
        private int _intervalMs;
        private byte _alpha;
        private int _snapshotRetentionMs;
        private int _lastUpdateTime;
        private bool _follow;
        private bool _ownsFrameTextures;
        private bool _terminationRequested;
        private AnimationEffects.SecondaryMotionBlurAnimationState _state;
        private Func<int, Vector2?> _snapshotPositionFactory;
        private Func<int, bool?> _snapshotFlipFactory;

        public int Id { get; } = SecondarySkillAnimationIdSource.NextId();

        private readonly struct MotionBlurSnapshot
        {
            public MotionBlurSnapshot(Vector2 position, bool flip, int startTime, List<IDXObject> frames)
            {
                Position = position;
                Flip = flip;
                StartTime = startTime;
                Frames = frames;
            }

            public Vector2 Position { get; }
            public bool Flip { get; }
            public int StartTime { get; }
            public List<IDXObject> Frames { get; }
        }

        public void Initialize(
            List<IDXObject> frames,
            Func<Vector2> getOwnerPosition,
            Func<bool> getOwnerFlip,
            Vector2 fallbackPosition,
            bool fallbackFlip,
            int delayMs,
            int intervalMs,
            byte alpha,
            int currentTimeMs,
            int durationMs,
            bool follow,
            int snapshotRetentionMs,
            bool ownsFrameTextures,
            Func<int, List<IDXObject>> snapshotFrameFactory,
            Func<int, Vector2?> snapshotPositionFactory,
            Func<int, bool?> snapshotFlipFactory,
            AnimationEffects.SecondaryMotionBlurAnimationState state)
        {
            _fallbackFrames = frames;
            _positionResolver = getOwnerPosition;
            _flipResolver = getOwnerFlip;
            _snapshotFrameFactory = snapshotFrameFactory;
            _snapshotPositionFactory = snapshotPositionFactory;
            _snapshotFlipFactory = snapshotFlipFactory;
            _fallbackPosition = fallbackPosition;
            _fallbackFlip = fallbackFlip;
            _follow = follow;
            _ownsFrameTextures = ownsFrameTextures;
            _nextUpdateTime = currentTimeMs + Math.Max(0, delayMs);
            _lastUpdateTime = currentTimeMs;
            _endTime = currentTimeMs + Math.Max(1, durationMs);
            _intervalMs = NormalizeSnapshotIntervalMs(intervalMs);
            _alpha = alpha;
            _snapshotRetentionMs = Math.Max(0, snapshotRetentionMs);
            _terminationRequested = false;
            _state = state ?? new AnimationEffects.SecondaryMotionBlurAnimationState();
            _state.TerminateRequested = false;
            _state.IsTerminated = false;
        }

        public bool Update(int currentTimeMs)
        {
            if (_state?.TerminateRequested == true)
            {
                RequestTermination(currentTimeMs);
            }

            _lastUpdateTime = currentTimeMs;
            while (currentTimeMs >= _nextUpdateTime && _nextUpdateTime < _endTime)
            {
                List<IDXObject> snapshotFrames = _snapshotFrameFactory?.Invoke(_nextUpdateTime);
                if (!AnimationEffects.HasFrames(snapshotFrames))
                {
                    snapshotFrames = _fallbackFrames;
                }

                if (AnimationEffects.HasFrames(snapshotFrames))
                {
                    Vector2 snapshotPosition = _positionResolver?.Invoke() ?? _fallbackPosition;
                    bool snapshotFlip = _flipResolver?.Invoke() ?? _fallbackFlip;
                    Vector2? samplePosition = _snapshotPositionFactory?.Invoke(_nextUpdateTime);
                    bool? sampleFlip = _snapshotFlipFactory?.Invoke(_nextUpdateTime);
                    if (samplePosition.HasValue)
                    {
                        snapshotPosition = samplePosition.Value;
                    }

                    if (sampleFlip.HasValue)
                    {
                        snapshotFlip = sampleFlip.Value;
                    }

                    _snapshots.Add(new MotionBlurSnapshot(
                        snapshotPosition,
                        snapshotFlip,
                        _nextUpdateTime,
                        snapshotFrames));
                }

                _nextUpdateTime += _intervalMs;
            }

            for (int i = _snapshots.Count - 1; i >= 0; i--)
            {
                MotionBlurSnapshot snapshot = _snapshots[i];
                int retentionMs = ResolveSnapshotRetentionMs(snapshot);
                if (retentionMs <= 0 || currentTimeMs - snapshot.StartTime >= retentionMs)
                {
                    DisposeFrameListIfOwned(snapshot.Frames);
                    _snapshots.RemoveAt(i);
                }
            }

            bool isActive = currentTimeMs < _endTime || _snapshots.Count > 0;
            if (!isActive && _state != null)
            {
                _state.IsTerminated = true;
            }

            return isActive;
        }

        public void RequestTermination(int currentTimeMs)
        {
            if (_terminationRequested)
            {
                return;
            }

            _terminationRequested = true;
            if (_state != null)
            {
                _state.TerminateRequested = true;
            }

            _endTime = Math.Min(_endTime, currentTimeMs);
            _nextUpdateTime = Math.Max(_nextUpdateTime, _endTime);
        }

        public void MarkTerminated()
        {
            if (_state != null)
            {
                _state.IsTerminated = true;
                _state.TerminateRequested = true;
            }
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            int currentTime = _lastUpdateTime;
            Vector2 ownerPosition = _positionResolver?.Invoke() ?? _fallbackPosition;
            bool ownerFlip = _flipResolver?.Invoke() ?? _fallbackFlip;
            foreach (MotionBlurSnapshot snapshot in _snapshots)
            {
                byte snapshotAlpha = ResolveSnapshotAlpha(
                    ageMs: Math.Max(0, currentTime - snapshot.StartTime),
                    retentionMs: ResolveSnapshotRetentionMs(snapshot),
                    baseAlpha: _alpha);
                if (snapshotAlpha == 0)
                {
                    continue;
                }

                List<IDXObject> frames = snapshot.Frames;
                int frameIndex = ResolveFrameIndex(frames, Math.Max(0, currentTime - snapshot.StartTime), loop: false);
                if (frames == null || frameIndex < 0 || frameIndex >= frames.Count)
                {
                    continue;
                }

                IDXObject frame = frames[frameIndex];
                if (frame == null)
                {
                    continue;
                }

                Vector2 drawPosition = _follow ? ownerPosition : snapshot.Position;
                bool drawFlip = _follow ? ownerFlip : snapshot.Flip;
                int drawShiftX = -(int)MathF.Round(drawPosition.X) - mapShiftX;
                int drawShiftY = -(int)MathF.Round(drawPosition.Y) - mapShiftY;
                Color tint = new Color(byte.MaxValue, byte.MaxValue, byte.MaxValue, snapshotAlpha);
                if (AnimationEffects.IsSecondaryMotionBlurLayerStack(frames))
                {
                    DrawLayerStack(
                        frames,
                        spriteBatch,
                        skeletonRenderer,
                        gameTime,
                        drawShiftX,
                        drawShiftY,
                        drawFlip,
                        tint);
                    continue;
                }

                frame.DrawBackground(
                    spriteBatch,
                    skeletonRenderer,
                    gameTime,
                    frame.X - drawShiftX,
                    frame.Y - drawShiftY,
                    tint,
                    drawFlip,
                    null);
            }
        }

        private static void DrawLayerStack(
            IReadOnlyList<IDXObject> frames,
            SpriteBatch spriteBatch,
            SkeletonMeshRenderer skeletonRenderer,
            GameTime gameTime,
            int drawShiftX,
            int drawShiftY,
            bool drawFlip,
            Color tint)
        {
            if (frames == null || frames.Count == 0)
            {
                return;
            }

            int maxTaggedOrder = -1;
            for (int i = 0; i < frames.Count; i++)
            {
                maxTaggedOrder = Math.Max(
                    maxTaggedOrder,
                    AnimationEffects.ResolveSecondaryMotionBlurLayerStackDrawOrder(frames[i], fallbackOrder: -1));
            }

            for (int drawOrder = 0; drawOrder <= maxTaggedOrder; drawOrder++)
            {
                for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                {
                    IDXObject layer = frames[frameIndex];
                    if (layer == null
                        || AnimationEffects.ResolveSecondaryMotionBlurLayerStackDrawOrder(layer, fallbackOrder: -1) != drawOrder)
                    {
                        continue;
                    }

                    layer.DrawBackground(
                        spriteBatch,
                        skeletonRenderer,
                        gameTime,
                        layer.X - drawShiftX,
                        layer.Y - drawShiftY,
                        tint,
                        drawFlip,
                        null);
                }
            }

            for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
            {
                IDXObject layer = frames[frameIndex];
                if (layer == null
                    || layer.Tag is AnimationEffects.SecondaryMotionBlurLayerStackEntryTag)
                {
                    continue;
                }

                layer.DrawBackground(
                    spriteBatch,
                    skeletonRenderer,
                    gameTime,
                    layer.X - drawShiftX,
                    layer.Y - drawShiftY,
                    tint,
                    drawFlip,
                    null);
            }
        }

        internal static byte ResolveSnapshotAlpha(int ageMs, int retentionMs, byte baseAlpha)
        {
            if (baseAlpha == 0)
            {
                return 0;
            }

            if (retentionMs <= 0)
            {
                return baseAlpha;
            }

            if (ageMs >= retentionMs)
            {
                return 0;
            }

            float progress = MathHelper.Clamp((float)ageMs / retentionMs, 0f, 1f);
            return (byte)Math.Clamp((int)MathF.Round(baseAlpha * (1f - progress)), 0, byte.MaxValue);
        }

        internal static int NormalizeSnapshotIntervalMs(int intervalMs)
        {
            return Math.Max(1, intervalMs);
        }

        public void Dispose()
        {
            if (!_ownsFrameTextures)
            {
                return;
            }

            for (int i = _snapshots.Count - 1; i >= 0; i--)
            {
                DisposeFrameListIfOwned(_snapshots[i].Frames);
            }

            _snapshots.Clear();
            DisposeFallbackFramesIfOwned();
        }

        private void DisposeFrameListIfOwned(List<IDXObject> frames)
        {
            if (!_ownsFrameTextures
                || frames == null
                || ReferenceEquals(frames, _fallbackFrames))
            {
                return;
            }

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] is DXObject dxObject
                    && dxObject.Texture != null
                    && !dxObject.Texture.IsDisposed)
                {
                    dxObject.Texture.Dispose();
                }
            }
        }

        private int ResolveSnapshotRetentionMs(MotionBlurSnapshot snapshot)
        {
            if (_snapshotRetentionMs > 0)
            {
                return _snapshotRetentionMs;
            }

            return ResolveFramesDuration(snapshot.Frames);
        }

        private void DisposeFallbackFramesIfOwned()
        {
            if (!_ownsFrameTextures || _fallbackFrames == null)
            {
                return;
            }

            for (int i = 0; i < _fallbackFrames.Count; i++)
            {
                if (_fallbackFrames[i] is DXObject dxObject
                    && dxObject.Texture != null
                    && !dxObject.Texture.IsDisposed)
                {
                    dxObject.Texture.Dispose();
                }
            }
        }
    }

    internal sealed class SecondaryChainSegmentAnimation : SecondarySkillFrameAnimation
    {
        private readonly List<(Vector2 Position, int Variant)> _segments = new();
        private IReadOnlyList<List<IDXObject>> _frameVariants;
        private int _startTime;
        private int _endTime;
        private int _lastUpdateTime;

        public int Id { get; } = SecondarySkillAnimationIdSource.NextId();
        public int SegmentCount => _segments.Count;

        public void Initialize(IReadOnlyList<List<IDXObject>> frameVariants, Vector2 start, Vector2 end, int tStart, int tEnd, int zOrder, bool ordered, bool tesla, int registrationKey, Random random)
        {
            _frameVariants = frameVariants ?? Array.Empty<List<IDXObject>>();
            _startTime = tStart;
            _endTime = tEnd;
            _lastUpdateTime = tStart;
            Vector2 delta = end - start;
            float distance = delta.Length();
            int count = Math.Max(1, (int)(distance / 48f) + 1);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                int variant = ResolveVariantIndex(i, count, ordered, tesla, random);
                _segments.Add((start + (delta * t), variant));
            }
        }

        public bool Update(int currentTimeMs)
        {
            _lastUpdateTime = currentTimeMs;
            return currentTimeMs < _endTime;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            int elapsed = Math.Max(0, _lastUpdateTime - _startTime);
            for (int i = 0; i < _segments.Count; i++)
            {
                (Vector2 position, int variant) = _segments[i];
                List<IDXObject> frames = variant >= 0 && variant < _frameVariants.Count ? _frameVariants[variant] : null;
                int frameIndex = ResolveFrameIndex(frames, elapsed, loop: true);
                DrawFrame(frames, frameIndex, spriteBatch, skeletonRenderer, gameTime, position, flip: false, mapShiftX, mapShiftY);
            }
        }

        private static int ResolveVariantIndex(int index, int count, bool ordered, bool tesla, Random random)
        {
            if (tesla)
            {
                if (index == 0)
                {
                    return 0;
                }

                if (index == count - 1)
                {
                    return 4;
                }

                return (random?.Next(3) ?? 0) + 1;
            }

            return ordered ? random?.Next(2) ?? 0 : random?.Next(3) ?? 0;
        }
    }

    /// <summary>
    /// Shared one-time canvas layer owners used by animation-displayer parity paths.
    /// </summary>
    public enum AnimationCanvasLayerOwner
    {
        Generic = 0,
        DamageNumber = 1
    }

    internal enum AnimationOneTimeOwner
    {
        Generic = 0,
        FullChargedAngerGauge = 1
    }

    internal enum AnimationOneTimePlaybackMode
    {
        Default = 0,
        GA_STOP = 1
    }

    internal enum AnimationOneTimeOverlayParentKind
    {
        None = 0,
        MobActionLayer = 1
    }

    internal enum FollowParticleRecoveredNativeOperationKind
    {
        LoadLayer = 0,
        PutFlip = 1,
        RelOffset = 2,
        AlphaRelMove = 3,
        AnimateRepeat = 4,
        RegisterRepeatAnimation = 5
    }

    internal readonly record struct FollowParticleRecoveredNativeOperation(
        FollowParticleRecoveredNativeOperationKind Kind,
        bool RelativeToTarget,
        bool AppliesOwnerFlip,
        Vector2 StartOffset,
        Vector2 EndOffset,
        int ZOrder,
        int DurationMs,
        int AlphaStart,
        int AlphaEnd);

    internal readonly record struct FollowParticleRecoveredNativeLayerState(
        bool RelativeToTarget,
        bool AppliesOwnerFlip,
        Vector2 StartOffset,
        Vector2 EndOffset,
        int ZOrder,
        int DurationMs,
        int AlphaStart,
        int AlphaEnd,
        bool RegistersRepeatLayer);

    /// <summary>
    /// Recovered native call shape for one-time animation-displayer owners that are still drawn through managed DX frames.
    /// </summary>
    internal readonly record struct OneTimeAnimationRecoveredRegistrationTrace(
        string SourceUol,
        int MobTemplatePathStringPoolId,
        int EffectNameStringPoolId,
        int LoadLayerCanvasValue,
        bool UsesOriginVector,
        int LoadLayerOriginOffsetX,
        int LoadLayerOriginOffsetY,
        bool UsesOverlayLayer,
        AnimationOneTimeOverlayParentKind OverlayParentKind,
        int LoadLayerOptionValue,
        int LoadLayerAlphaValue,
        int LoadLayerReservedValue,
        bool LoadLayerFlip,
        AnimationOneTimePlaybackMode AnimatePlaybackMode,
        bool AnimateUsesMissingStartTime,
        bool AnimateUsesMissingRepeatCount,
        bool RegistersOneTimeAnimation,
        int RegisterOneTimeAnimationDelayMs,
        bool RegisterOneTimeAnimationUsesFlipOrigin,
        bool RegisterOneTimeAnimationHasCallback)
    {
        public static OneTimeAnimationRecoveredRegistrationTrace CreateFullChargedAngerGauge(string sourceUol)
        {
            return new OneTimeAnimationRecoveredRegistrationTrace(
                sourceUol,
                MapleStoryStringPool.MobAngerGaugeBurstTemplatePathStringPoolId,
                MapleStoryStringPool.MobAngerGaugeBurstEffectNameStringPoolId,
                LoadLayerCanvasValue: 0,
                UsesOriginVector: true,
                LoadLayerOriginOffsetX: 0,
                LoadLayerOriginOffsetY: 0,
                UsesOverlayLayer: true,
                OverlayParentKind: AnimationOneTimeOverlayParentKind.MobActionLayer,
                LoadLayerOptionValue: unchecked((int)0xC00614A4),
                LoadLayerAlphaValue: 255,
                LoadLayerReservedValue: 0,
                LoadLayerFlip: false,
                AnimatePlaybackMode: AnimationOneTimePlaybackMode.GA_STOP,
                AnimateUsesMissingStartTime: true,
                AnimateUsesMissingRepeatCount: true,
                RegistersOneTimeAnimation: true,
                RegisterOneTimeAnimationDelayMs: 0,
                RegisterOneTimeAnimationUsesFlipOrigin: false,
                RegisterOneTimeAnimationHasCallback: false);
        }
    }

    internal enum OneTimeAnimationRecoveredNativeOperationKind
    {
        LoadLayer = 0,
        Animate = 1,
        RegisterOneTimeAnimation = 2,
        RetainOverlayParent = 3,
        RetainOriginVector = 4,
        RetainLoadedLayerForRegistration = 5,
        ReleaseLoadedLayer = 6,
        ReleaseOriginVector = 7,
        ReleaseOverlayParent = 8,
        ReleaseSourceUol = 9
    }

    internal readonly record struct OneTimeAnimationRecoveredNativeOperation(
        OneTimeAnimationRecoveredNativeOperationKind Kind,
        string SourceUol,
        AnimationOneTimePlaybackMode PlaybackMode,
        bool UsesOriginVector,
        int OriginOffsetX,
        int OriginOffsetY,
        bool UsesOverlayLayer,
        AnimationOneTimeOverlayParentKind OverlayParentKind,
        int Value,
        int LoadLayerCanvasValue = 0,
        int LoadLayerAlphaValue = 0,
        bool LoadLayerFlip = false,
        int LoadLayerReservedValue = 0,
        bool AnimateUsesMissingStartTime = false,
        bool AnimateUsesMissingRepeatCount = false,
        bool RegisterOneTimeAnimationHasCallback = false);

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
    /// Prepared managed registration payload handed off from owner seams such as Effect_HP.
    /// Carries the recovered position write, insert-descriptor shape, and native trace verbatim.
    /// </summary>
    internal readonly record struct PreparedOneTimeCanvasLayerRegistration(
        float Left,
        float Top,
        CanvasLayerInsertDescriptor[] InsertDescriptors,
        CanvasLayerRecoveredLayerSettings RecoveredLayerSettings,
        CanvasLayerRecoveredRegistrationTrace RecoveredRegistrationTrace,
        CanvasLayerRecoveredOwnerTrace? RecoveredOwnerTrace);

    /// <summary>
    /// Full recovered registration trace for the managed canvas-layer analogue.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredRegistrationTrace(
        CanvasLayerRecoveredCanvasSettings CanvasSettings,
        CanvasLayerRecoveredLayerSettings LayerSettings,
        CanvasLayerRecoveredPositionSettings Position,
        CanvasLayerRecoveredInsertCommand[] InsertCommands,
        bool RegistersOneTimeAnimation);

    internal enum CanvasLayerRecoveredNativeOperationKind
    {
        CreateTemporaryCanvas,
        InsertTemporaryCanvas,
        CreateLayer,
        SetLayerOption,
        SetLayerPriority,
        InsertCanvas,
        SetLayerPosition,
        RetainLayerForOneTimeRegistration,
        RegisterOneTimeAnimation,
        ReleaseLayerAfterOneTimeRegistration
    }

    /// <summary>
    /// Managed replay of the native Gr2D calls recovered from CAnimationDisplayer::Effect_HP.
    /// This keeps the live one-time layer tied to the client call sequence even though the
    /// simulator renders through XNA textures instead of IWzGr2DLayer COM objects.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredNativeOperation(
        CanvasLayerRecoveredNativeOperationKind Kind,
        AnimationCanvasLayerContent? Content,
        Point Offset,
        int StartDelayMs,
        CanvasLayerRecoveredInsertCanvasSettings InsertCanvasSettings,
        CanvasLayerRecoveredMoveSettings MoveSettings,
        CanvasLayerRecoveredPositionSettings Position,
        int Value,
        CanvasLayerRecoveredCanvasSettings CanvasSettings);

    /// <summary>
    /// Snapshot of the recovered layer state after the native Effect_HP call sequence has run.
    /// This is the managed analogue for the IWzGr2DLayer writes the simulator does not instantiate.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredNativeLayerState(
        CanvasLayerRecoveredCanvasSettings CanvasSettings,
        CanvasLayerRecoveredLayerSettings LayerSettings,
        CanvasLayerRecoveredPositionSettings PrimaryPosition,
        bool HasOverlayPosition,
        CanvasLayerRecoveredPositionSettings OverlayPosition,
        int ActiveLayerOptionValue,
        bool RegistersOneTimeAnimation);

    /// <summary>
    /// Owner-prepared source trace that stays attached to managed one-time canvas layers.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredPreparedSourceTrace(
        string SourceSetName,
        string SpriteName,
        string SourceCanvasPath,
        bool UseLargeDigitSet,
        Point SourceOrigin,
        int SourceWidth,
        int SourceHeight,
        Point CanvasOffset);

    internal enum CanvasLayerRecoveredTemporaryCanvasOperationKind
    {
        CreateCanvas,
        InsertCanvas
    }

    /// <summary>
    /// Owner-side temporary canvas operation recovered from CAnimationDisplayer::Effect_HP.
    /// This preserves the CreateCanvas and source InsertCanvas sequence before the
    /// prepared canvas is handed to the managed one-time layer analogue.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredTemporaryCanvasOperation(
        CanvasLayerRecoveredTemporaryCanvasOperationKind Kind,
        CanvasLayerRecoveredCanvasSettings CanvasSettings,
        CanvasLayerRecoveredPreparedSourceTrace Source);

    /// <summary>
    /// Owner-side prepared canvas provenance preserved alongside the managed registration payload.
    /// </summary>
    internal readonly record struct CanvasLayerRecoveredOwnerTrace(
        int FormatStringPoolId,
        string FormattedText,
        CanvasLayerRecoveredCanvasSettings CanvasSettings,
        CanvasLayerRecoveredPreparedSourceTrace[] PreparedSources,
        CanvasLayerRecoveredTemporaryCanvasOperation[] TemporaryCanvasOperations,
        bool KeepsOverlayOnSeparateLayer,
        string OverlayCanvasPath,
        string OverlaySpriteName,
        Point OverlayOffset,
        int OverlayLayerPositionOffsetY);

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
        internal CanvasLayerRecoveredOwnerTrace? RecoveredOwnerTrace { get; private set; }
        internal IReadOnlyList<CanvasLayerRecoveredNativeOperation> RecoveredNativeExecutionTrace { get; private set; }
            = Array.Empty<CanvasLayerRecoveredNativeOperation>();
        internal CanvasLayerRecoveredNativeLayerState RecoveredNativeLayerState { get; private set; }

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
            CanvasLayerRecoveredLayerSettings recoveredLayerSettings,
            CanvasLayerRecoveredRegistrationTrace? recoveredRegistrationTrace = null,
            CanvasLayerRecoveredOwnerTrace? recoveredOwnerTrace = null)
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
            RecoveredRegistrationTrace = recoveredRegistrationTrace ?? BuildRecoveredRegistrationTrace(
                left,
                top,
                canvasTexture.Width,
                canvasTexture.Height,
                insertDescriptors,
                recoveredLayerSettings,
                registersOneTimeAnimation: true);
            RecoveredOwnerTrace = recoveredOwnerTrace;
            RecoveredNativeExecutionTrace = BuildRecoveredNativeExecutionTrace(
                RecoveredRegistrationTrace,
                recoveredOwnerTrace);
            RecoveredNativeLayerState = BuildRecoveredNativeLayerState(
                RecoveredRegistrationTrace,
                recoveredOwnerTrace);
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
                    || !TryResolveOperationDrawState(operation.Descriptor, out float alpha, out Point animatedOffset))
                {
                    continue;
                }

                CanvasLayerRecoveredPositionSettings basePosition = ResolveContentBasePosition(operation.Descriptor.Content);
                Vector2 position = new(
                    basePosition.Left + mapShiftX + animatedOffset.X,
                    basePosition.Top + mapShiftY + animatedOffset.Y);

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
            RecoveredOwnerTrace = null;
            RecoveredNativeExecutionTrace = Array.Empty<CanvasLayerRecoveredNativeOperation>();
            RecoveredNativeLayerState = default;
        }

        internal static CanvasLayerRecoveredNativeLayerState BuildRecoveredNativeLayerState(
            CanvasLayerRecoveredRegistrationTrace registrationTrace,
            CanvasLayerRecoveredOwnerTrace? ownerTrace)
        {
            bool hasOverlayPosition = ownerTrace?.KeepsOverlayOnSeparateLayer == true;
            int overlayLayerPositionOffsetY = ownerTrace?.OverlayLayerPositionOffsetY ?? 0;
            CanvasLayerRecoveredPositionSettings overlayPosition = hasOverlayPosition
                ? new CanvasLayerRecoveredPositionSettings(
                    registrationTrace.Position.Left,
                    registrationTrace.Position.Top + overlayLayerPositionOffsetY)
                : default;

            return new CanvasLayerRecoveredNativeLayerState(
                registrationTrace.CanvasSettings,
                registrationTrace.LayerSettings,
                registrationTrace.Position,
                hasOverlayPosition,
                overlayPosition,
                registrationTrace.LayerSettings.FinalizeLayerOptionValue,
                registrationTrace.RegistersOneTimeAnimation);
        }

        internal static CanvasLayerRecoveredNativeOperation[] BuildRecoveredNativeExecutionTrace(
            CanvasLayerRecoveredRegistrationTrace registrationTrace,
            CanvasLayerRecoveredOwnerTrace? ownerTrace)
        {
            CanvasLayerRecoveredTemporaryCanvasOperation[] ownerTemporaryCanvasOperations =
                ownerTrace?.TemporaryCanvasOperations ?? Array.Empty<CanvasLayerRecoveredTemporaryCanvasOperation>();
            CanvasLayerRecoveredInsertCommand[] insertCommands =
                registrationTrace.InsertCommands ?? Array.Empty<CanvasLayerRecoveredInsertCommand>();
            bool hasOverlayPositionWrite = ownerTrace?.KeepsOverlayOnSeparateLayer == true;
            int capacity = ownerTemporaryCanvasOperations.Length + 3 + insertCommands.Length + 1 + (hasOverlayPositionWrite ? 1 : 0)
                + (registrationTrace.RegistersOneTimeAnimation ? 3 : 0);
            var operations = new List<CanvasLayerRecoveredNativeOperation>(capacity);

            for (int i = 0; i < ownerTemporaryCanvasOperations.Length; i++)
            {
                CanvasLayerRecoveredTemporaryCanvasOperation operation = ownerTemporaryCanvasOperations[i];
                if (operation.Kind == CanvasLayerRecoveredTemporaryCanvasOperationKind.CreateCanvas)
                {
                    operations.Add(new CanvasLayerRecoveredNativeOperation(
                        CanvasLayerRecoveredNativeOperationKind.CreateTemporaryCanvas,
                        null,
                        Point.Zero,
                        0,
                        default,
                        default,
                        default,
                        0,
                        operation.CanvasSettings));
                    continue;
                }

                operations.Add(new CanvasLayerRecoveredNativeOperation(
                    CanvasLayerRecoveredNativeOperationKind.InsertTemporaryCanvas,
                    null,
                    operation.Source.CanvasOffset,
                    0,
                    new CanvasLayerRecoveredInsertCanvasSettings(
                        DurationMs: 0,
                        StartAlphaValue: 255,
                        EndAlphaValue: 255),
                    new CanvasLayerRecoveredMoveSettings(
                        operation.Source.CanvasOffset,
                        operation.Source.CanvasOffset),
                    default,
                    0,
                    operation.CanvasSettings));
            }

            operations.AddRange(new CanvasLayerRecoveredNativeOperation[]
            {
                new(
                    CanvasLayerRecoveredNativeOperationKind.CreateLayer,
                    null,
                    Point.Zero,
                    0,
                    default,
                    default,
                    default,
                    registrationTrace.LayerSettings.CreateLayerCanvasValue,
                    registrationTrace.CanvasSettings),
                new(
                    CanvasLayerRecoveredNativeOperationKind.SetLayerOption,
                    null,
                    Point.Zero,
                    0,
                    default,
                    default,
                    default,
                    registrationTrace.LayerSettings.InitialLayerOptionValue,
                    registrationTrace.CanvasSettings),
                new(
                    CanvasLayerRecoveredNativeOperationKind.SetLayerPriority,
                    null,
                    Point.Zero,
                    0,
                    default,
                    default,
                    default,
                    registrationTrace.LayerSettings.LayerPriorityValue,
                    registrationTrace.CanvasSettings)
            });

            for (int i = 0; i < insertCommands.Length; i++)
            {
                CanvasLayerRecoveredInsertCommand command = insertCommands[i];
                operations.Add(new CanvasLayerRecoveredNativeOperation(
                    CanvasLayerRecoveredNativeOperationKind.InsertCanvas,
                    command.Content,
                    command.Offset,
                    command.StartDelayMs,
                    command.InsertCanvasSettings,
                    command.MoveSettings,
                    default,
                    0,
                    registrationTrace.CanvasSettings));
            }

            operations.Add(new CanvasLayerRecoveredNativeOperation(
                CanvasLayerRecoveredNativeOperationKind.SetLayerPosition,
                AnimationCanvasLayerContent.PrimaryCanvas,
                Point.Zero,
                0,
                default,
                default,
                registrationTrace.Position,
                0,
                registrationTrace.CanvasSettings));

            if (hasOverlayPositionWrite)
            {
                Point overlayOffset = ResolveOverlayInsertOffset(insertCommands);
                int overlayLayerPositionOffsetY = ownerTrace?.OverlayLayerPositionOffsetY ?? 0;
                operations.Add(new CanvasLayerRecoveredNativeOperation(
                    CanvasLayerRecoveredNativeOperationKind.SetLayerPosition,
                    AnimationCanvasLayerContent.OverlayCanvas,
                    overlayOffset,
                    0,
                    default,
                    default,
                    new CanvasLayerRecoveredPositionSettings(
                        registrationTrace.Position.Left,
                        registrationTrace.Position.Top + overlayLayerPositionOffsetY),
                    0,
                    registrationTrace.CanvasSettings));
            }

            operations.Add(new CanvasLayerRecoveredNativeOperation(
                CanvasLayerRecoveredNativeOperationKind.SetLayerOption,
                null,
                Point.Zero,
                0,
                default,
                default,
                default,
                registrationTrace.LayerSettings.FinalizeLayerOptionValue,
                registrationTrace.CanvasSettings));

            if (registrationTrace.RegistersOneTimeAnimation)
            {
                operations.Add(new CanvasLayerRecoveredNativeOperation(
                    CanvasLayerRecoveredNativeOperationKind.RetainLayerForOneTimeRegistration,
                    null,
                    Point.Zero,
                    0,
                    default,
                    default,
                    default,
                    1,
                    registrationTrace.CanvasSettings));
                operations.Add(new CanvasLayerRecoveredNativeOperation(
                    CanvasLayerRecoveredNativeOperationKind.RegisterOneTimeAnimation,
                    null,
                    Point.Zero,
                    0,
                    default,
                    default,
                    default,
                    0,
                    registrationTrace.CanvasSettings));
                operations.Add(new CanvasLayerRecoveredNativeOperation(
                    CanvasLayerRecoveredNativeOperationKind.ReleaseLayerAfterOneTimeRegistration,
                    null,
                    Point.Zero,
                    0,
                    default,
                    default,
                    default,
                    -1,
                    registrationTrace.CanvasSettings));
            }

            return operations.ToArray();
        }

        private static Point ResolveOverlayInsertOffset(IReadOnlyList<CanvasLayerRecoveredInsertCommand> insertCommands)
        {
            if (insertCommands == null)
            {
                return Point.Zero;
            }

            for (int i = 0; i < insertCommands.Count; i++)
            {
                if (insertCommands[i].Content == AnimationCanvasLayerContent.OverlayCanvas)
                {
                    return insertCommands[i].Offset;
                }
            }

            return Point.Zero;
        }

        private CanvasLayerRecoveredPositionSettings ResolveContentBasePosition(AnimationCanvasLayerContent content)
        {
            if (content == AnimationCanvasLayerContent.OverlayCanvas
                && RecoveredNativeLayerState.HasOverlayPosition)
            {
                return RecoveredNativeLayerState.OverlayPosition;
            }

            return new CanvasLayerRecoveredPositionSettings(
                (int)Math.Round(_left, MidpointRounding.AwayFromZero),
                (int)Math.Round(_top, MidpointRounding.AwayFromZero));
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
            out Point animatedOffset)
        {
            return TryResolveRecoveredInsertState(
                descriptor,
                _elapsedMs,
                out alpha,
                out animatedOffset);
        }

        internal static bool TryResolveRecoveredInsertState(
            CanvasLayerInsertDescriptor descriptor,
            int elapsedMs,
            out float alpha,
            out Point animatedOffset)
        {
            alpha = 0f;
            animatedOffset = descriptor.Offset;

            int localElapsedMs = elapsedMs - descriptor.StartDelayMs;
            int recoveredDurationMs = descriptor.RecoveredInsertCanvasSettings.DurationMs;
            int fallbackDurationMs = descriptor.HoldDurationMs + descriptor.FadeDurationMs;
            int lifetimeMs = recoveredDurationMs > 0
                ? recoveredDurationMs
                : Math.Max(0, fallbackDurationMs);
            if (localElapsedMs < 0 || localElapsedMs >= lifetimeMs)
            {
                return false;
            }

            float transitionProgress = lifetimeMs > 0
                ? Math.Clamp((float)localElapsedMs / lifetimeMs, 0f, 1f)
                : 1f;
            float startAlpha = ResolveRecoveredAlphaValue(
                descriptor.StartAlpha,
                descriptor.RecoveredInsertCanvasSettings.StartAlphaValue);
            float endAlpha = ResolveRecoveredAlphaValue(
                descriptor.EndAlpha,
                descriptor.RecoveredInsertCanvasSettings.EndAlphaValue);
            alpha = MathHelper.Lerp(startAlpha, endAlpha, transitionProgress);

            Point startOffset = descriptor.RecoveredMoveSettings.StartOffset;
            Point endOffset = descriptor.RecoveredMoveSettings.EndOffset;
            if (startOffset == Point.Zero
                && endOffset == Point.Zero
                && descriptor.Offset != Point.Zero)
            {
                startOffset = descriptor.Offset;
                endOffset = descriptor.Offset;
            }

            animatedOffset = new Point(
                startOffset.X + (int)Math.Round(
                    (endOffset.X - startOffset.X) * transitionProgress,
                    MidpointRounding.AwayFromZero),
                startOffset.Y + (int)Math.Round(
                    (endOffset.Y - startOffset.Y) * transitionProgress,
                    MidpointRounding.AwayFromZero));
            return alpha > 0f;
        }

        private static float ResolveRecoveredAlphaValue(float fallbackAlpha, int recoveredAlphaValue)
        {
            return recoveredAlphaValue >= 0 && recoveredAlphaValue <= 255
                ? recoveredAlphaValue / 255f
                : Math.Clamp(fallbackAlpha, 0f, 1f);
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
        public int ZOrder => _zOrder;
        public AnimationOneTimeOwner Owner { get; private set; } = AnimationOneTimeOwner.Generic;
        public AnimationOneTimePlaybackMode PlaybackMode { get; private set; } = AnimationOneTimePlaybackMode.Default;
        public string SourceUol { get; private set; }
        public bool UsesOverlayParent { get; private set; }
        public AnimationOneTimeOverlayParentKind OverlayParentKind { get; private set; } = AnimationOneTimeOverlayParentKind.None;
        public OneTimeAnimationRecoveredRegistrationTrace? RecoveredRegistrationTrace { get; private set; }
        internal IReadOnlyList<OneTimeAnimationRecoveredNativeOperation> RecoveredNativeExecutionTrace { get; private set; }
            = Array.Empty<OneTimeAnimationRecoveredNativeOperation>();
        internal int CurrentFrameIndex => _currentFrame;

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
            Initialize(
                frames,
                x,
                y,
                flip,
                currentTimeMs,
                zOrder,
                getPosition,
                getFlip,
                AnimationOneTimeOwner.Generic,
                AnimationOneTimePlaybackMode.Default,
                sourceUol: null,
                usesOverlayParent: false,
                recoveredRegistrationTrace: null);
        }

        public void Initialize(
            List<IDXObject> frames,
            float x,
            float y,
            bool flip,
            int currentTimeMs,
            int zOrder,
            Func<Vector2> getPosition,
            Func<bool> getFlip,
            AnimationOneTimeOwner owner,
            AnimationOneTimePlaybackMode playbackMode,
            string sourceUol,
            bool usesOverlayParent,
            OneTimeAnimationRecoveredRegistrationTrace? recoveredRegistrationTrace = null,
            int initialElapsedMs = 0)
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
            Owner = owner;
            PlaybackMode = playbackMode;
            SourceUol = sourceUol;
            UsesOverlayParent = usesOverlayParent;
            OverlayParentKind = recoveredRegistrationTrace?.OverlayParentKind
                ?? (usesOverlayParent ? AnimationOneTimeOverlayParentKind.MobActionLayer : AnimationOneTimeOverlayParentKind.None);
            RecoveredRegistrationTrace = recoveredRegistrationTrace;
            RecoveredNativeExecutionTrace = BuildRecoveredNativeExecutionTrace(recoveredRegistrationTrace);

            if (initialElapsedMs > 0)
            {
                SeekToElapsed(initialElapsedMs, currentTimeMs);
            }
        }

        public bool Update(int currentTimeMs)
        {
            if (_finished) return false;

            if (PlaybackMode == AnimationOneTimePlaybackMode.GA_STOP)
            {
                return UpdateStoppedPlayback(currentTimeMs);
            }

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

        private bool UpdateStoppedPlayback(int currentTimeMs)
        {
            if (_frames == null || _frames.Count == 0)
            {
                _finished = true;
                return false;
            }

            int elapsed = Math.Max(0, unchecked(currentTimeMs - _startTime));
            int cursor = 0;
            for (int i = 0; i < _frames.Count; i++)
            {
                cursor += ResolveFrameDelay(_frames[i]);
                if (elapsed < cursor)
                {
                    _currentFrame = i;
                    _lastFrameTime = currentTimeMs;
                    return true;
                }
            }

            _finished = true;
            _currentFrame = _frames.Count;
            return false;
        }

        private void SeekToElapsed(int initialElapsedMs, int currentTimeMs)
        {
            if (_frames == null || _frames.Count == 0)
            {
                _finished = true;
                return;
            }

            int elapsed = Math.Max(0, initialElapsedMs);
            _startTime = unchecked(currentTimeMs - elapsed);
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;

            while (_currentFrame < _frames.Count)
            {
                int frameDelay = Math.Max(0, _frames[_currentFrame]?.Delay ?? 0);
                if (elapsed <= frameDelay)
                {
                    _lastFrameTime = unchecked(currentTimeMs - elapsed);
                    return;
                }

                elapsed -= frameDelay + 1;
                _currentFrame++;
            }

            _finished = true;
        }

        private static int ResolveFrameDelay(IDXObject frame)
        {
            return Math.Max(10, frame?.Delay ?? 100);
        }

        private static OneTimeAnimationRecoveredNativeOperation[] BuildRecoveredNativeExecutionTrace(
            OneTimeAnimationRecoveredRegistrationTrace? registrationTrace)
        {
            if (registrationTrace == null)
            {
                return Array.Empty<OneTimeAnimationRecoveredNativeOperation>();
            }

            OneTimeAnimationRecoveredRegistrationTrace trace = registrationTrace.GetValueOrDefault();
            int capacity = 2
                + (trace.UsesOverlayLayer ? 2 : 0)
                + (trace.UsesOriginVector ? 2 : 0)
                + (trace.RegistersOneTimeAnimation ? 2 : 1);
            var operations = new List<OneTimeAnimationRecoveredNativeOperation>(capacity);

            if (trace.UsesOverlayLayer)
            {
                operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                    OneTimeAnimationRecoveredNativeOperationKind.RetainOverlayParent,
                    trace.SourceUol,
                    AnimationOneTimePlaybackMode.Default,
                    false,
                    0,
                    0,
                    true,
                    trace.OverlayParentKind,
                    Value: 1));
            }

            if (trace.UsesOriginVector)
            {
                operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                    OneTimeAnimationRecoveredNativeOperationKind.RetainOriginVector,
                    trace.SourceUol,
                    AnimationOneTimePlaybackMode.Default,
                    true,
                    trace.LoadLayerOriginOffsetX,
                    trace.LoadLayerOriginOffsetY,
                    false,
                    AnimationOneTimeOverlayParentKind.None,
                    Value: 1));
            }

            operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                OneTimeAnimationRecoveredNativeOperationKind.LoadLayer,
                trace.SourceUol,
                AnimationOneTimePlaybackMode.Default,
                trace.UsesOriginVector,
                trace.LoadLayerOriginOffsetX,
                trace.LoadLayerOriginOffsetY,
                trace.UsesOverlayLayer,
                trace.OverlayParentKind,
                trace.LoadLayerOptionValue,
                trace.LoadLayerCanvasValue,
                trace.LoadLayerAlphaValue,
                trace.LoadLayerFlip,
                trace.LoadLayerReservedValue));
            operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                OneTimeAnimationRecoveredNativeOperationKind.Animate,
                trace.SourceUol,
                trace.AnimatePlaybackMode,
                false,
                0,
                0,
                false,
                AnimationOneTimeOverlayParentKind.None,
                (int)trace.AnimatePlaybackMode,
                AnimateUsesMissingStartTime: trace.AnimateUsesMissingStartTime,
                AnimateUsesMissingRepeatCount: trace.AnimateUsesMissingRepeatCount));

            if (trace.RegistersOneTimeAnimation)
            {
                operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                    OneTimeAnimationRecoveredNativeOperationKind.RetainLoadedLayerForRegistration,
                    trace.SourceUol,
                    AnimationOneTimePlaybackMode.Default,
                    false,
                    0,
                    0,
                    false,
                    AnimationOneTimeOverlayParentKind.None,
                    Value: 1));
                operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                    OneTimeAnimationRecoveredNativeOperationKind.RegisterOneTimeAnimation,
                    trace.SourceUol,
                    AnimationOneTimePlaybackMode.Default,
                    trace.RegisterOneTimeAnimationUsesFlipOrigin,
                    0,
                    0,
                    false,
                    AnimationOneTimeOverlayParentKind.None,
                    trace.RegisterOneTimeAnimationDelayMs,
                    RegisterOneTimeAnimationHasCallback: trace.RegisterOneTimeAnimationHasCallback));
            }

            operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                OneTimeAnimationRecoveredNativeOperationKind.ReleaseLoadedLayer,
                trace.SourceUol,
                AnimationOneTimePlaybackMode.Default,
                false,
                0,
                0,
                false,
                AnimationOneTimeOverlayParentKind.None,
                Value: 1));
            operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                OneTimeAnimationRecoveredNativeOperationKind.ReleaseSourceUol,
                trace.SourceUol,
                AnimationOneTimePlaybackMode.Default,
                false,
                0,
                0,
                false,
                AnimationOneTimeOverlayParentKind.None,
                Value: 1));

            if (trace.UsesOriginVector)
            {
                operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                    OneTimeAnimationRecoveredNativeOperationKind.ReleaseOriginVector,
                    trace.SourceUol,
                    AnimationOneTimePlaybackMode.Default,
                    true,
                    trace.LoadLayerOriginOffsetX,
                    trace.LoadLayerOriginOffsetY,
                    false,
                    AnimationOneTimeOverlayParentKind.None,
                    Value: 1));
            }

            if (trace.UsesOverlayLayer)
            {
                operations.Add(new OneTimeAnimationRecoveredNativeOperation(
                    OneTimeAnimationRecoveredNativeOperationKind.ReleaseOverlayParent,
                    trace.SourceUol,
                    AnimationOneTimePlaybackMode.Default,
                    false,
                    0,
                    0,
                    true,
                    trace.OverlayParentKind,
                    Value: 1));
            }

            return operations.ToArray();
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

        internal bool ResolveDrawFlipForTesting()
        {
            return _flipResolver?.Invoke() ?? _flip;
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
        private Func<bool> _getTargetFlip;
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
        private bool _spawnOnlyOnTargetMove;
        private Func<bool> _isTargetMoveAction;
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
        private int _spawnZOrder;
        private bool _suppressTargetFlip;
        private Vector2 _lastObservedTargetPosition;

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
            _getTargetFlip = options?.GetTargetFlip;
            _currentGenerationPointIndex = 0;
            _thetaDegrees = options?.ThetaDegrees ?? 0;
            _updateIntervalMs = Math.Max(1, options?.UpdateIntervalMs ?? 100);
            _radius = Math.Max(0f, options?.Radius ?? 0f);
            _spawnFrameVariants = options?.SpawnFrameVariants;
            _spawnRelativeToTarget = options?.SpawnRelativeToTarget ?? true;
            _suppressTargetFlip = options?.SuppressTargetFlip ?? false;
            _spawnOnlyOnTargetMove = options?.SpawnOnlyOnTargetMove ?? false;
            _isTargetMoveAction = options?.IsTargetMoveAction;
            _spawnDurationMs = Math.Max(0, options?.SpawnDurationMs ?? 0);
            _spawnTravelDistanceMin = Math.Max(0f, options?.SpawnTravelDistanceMin ?? 0f);
            _spawnTravelDistanceMax = Math.Max(_spawnTravelDistanceMin, options?.SpawnTravelDistanceMax ?? _spawnTravelDistanceMin);
            _spawnUsesEmissionBox = options?.SpawnUsesEmissionBox ?? false;
            _spawnAppliesEmissionBias = options?.SpawnAppliesEmissionBias ?? false;
            _spawnVerticalEmissionBias = options?.SpawnVerticalEmissionBias ?? 0f;
            _spawnOffsetMin = options?.SpawnOffsetMin ?? Point.Zero;
            _spawnOffsetMax = options?.SpawnOffsetMax ?? Point.Zero;
            _spawnArea = options?.SpawnArea ?? Rectangle.Empty;
            _spawnZOrder = options?.SpawnZOrder ?? 0;
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
            _lastObservedTargetPosition = _getTargetPosition?.Invoke() ?? Vector2.Zero;
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

            if (_nextSpawnTime <= currentTimeMs)
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

                Vector2 targetPosition = _getTargetPosition();
                bool targetMoved = ResolveTargetMoveActionState(targetPosition);

                if (effects != null && targetMoved && AnimationEffects.HasFrameVariants(_spawnFrameVariants))
                {
                    int variantIndex = random?.Next(0, _spawnFrameVariants.Count) ?? 0;
                    List<IDXObject> variantFrames = _spawnFrameVariants[variantIndex];
                    if (AnimationEffects.HasFrames(variantFrames))
                    {
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
                                bool mirrorHorizontalTravel = _spawnUsesEmissionBox
                                    && !_suppressTargetFlip
                                    && (_getTargetFlip?.Invoke() ?? false);
                                particleEndOffset = AnimationEffects.ResolveFollowParticleEndOffset(
                                    particleStartOffset,
                                    particleAngleDegrees,
                                    randomOffset,
                                    _spawnUsesEmissionBox,
                                    mirrorHorizontalTravel);
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
                                _getTargetFlip,
                                targetPosition,
                                _spawnRelativeToTarget,
                                _suppressTargetFlip,
                                _offsetX,
                                _offsetY,
                                startOffset: particleStartOffset,
                                endOffset: particleEndOffset,
                                zOrder: _spawnZOrder,
                                durationMs: resolvedDurationMs,
                                currentTimeMs: currentTimeMs);
                        }
                    }
                }

                _lastFollowUpdateTime = _nextSpawnTime;
                _nextSpawnTime += _updateIntervalMs;
                _lastObservedTargetPosition = targetPosition;
            }

            return true;
        }

        private bool ResolveTargetMoveActionState(Vector2 targetPosition)
        {
            if (!_spawnOnlyOnTargetMove)
            {
                return true;
            }

            if (_isTargetMoveAction != null)
            {
                return _isTargetMoveAction();
            }

            return Vector2.DistanceSquared(targetPosition, _lastObservedTargetPosition) > float.Epsilon;
        }

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonRenderer, GameTime gameTime, int mapShiftX, int mapShiftY)
        {
            if (!AnimationEffects.HasFrames(_frames))
            {
                return;
            }

            Vector2 targetPos = _getTargetPosition();
            IDXObject frame = _frames[_currentFrame];
            bool flip = !_suppressTargetFlip && (_getTargetFlip?.Invoke() ?? false);

            // Use the object's built-in DrawObject method
            float drawX = targetPos.X + _offsetX + _followOffset.X;
            float drawY = targetPos.Y + _offsetY + _followOffset.Y;
            int drawShiftX = -(int)drawX - mapShiftX;
            int drawShiftY = -(int)drawY - mapShiftY;

            frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, flip, null);
        }
    }

    internal sealed class FollowParticleAnimation
    {
        private List<IDXObject> _frames;
        private Func<Vector2> _getTargetPosition;
        private Func<bool> _getTargetFlip;
        private Vector2 _capturedTargetPosition;
        private bool _relativeToTarget;
        private bool _suppressTargetFlip;
        private float _offsetX;
        private float _offsetY;
        private Vector2 _startOffset;
        private Vector2 _endOffset;
        private int _zOrder;
        private int _startTime;
        private int _duration;
        private int _currentFrame;
        private int _lastFrameTime;

        public int FollowRegistrationId { get; private set; }
        public int ZOrder => _zOrder;
        internal IReadOnlyList<FollowParticleRecoveredNativeOperation> RecoveredNativeExecutionTrace { get; private set; }
            = Array.Empty<FollowParticleRecoveredNativeOperation>();
        internal FollowParticleRecoveredNativeLayerState RecoveredNativeLayerState { get; private set; }

        public void Initialize(
            int followRegistrationId,
            List<IDXObject> frames,
            Func<Vector2> getTargetPosition,
            Func<bool> getTargetFlip,
            Vector2 capturedTargetPosition,
            bool relativeToTarget,
            bool suppressTargetFlip,
            float offsetX,
            float offsetY,
            Vector2 startOffset,
            Vector2 endOffset,
            int zOrder,
            int durationMs,
            int currentTimeMs)
        {
            FollowRegistrationId = followRegistrationId;
            _frames = frames;
            _getTargetPosition = getTargetPosition;
            _getTargetFlip = getTargetFlip;
            _capturedTargetPosition = capturedTargetPosition;
            _relativeToTarget = relativeToTarget;
            _suppressTargetFlip = suppressTargetFlip;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _startOffset = startOffset;
            _endOffset = endOffset;
            _zOrder = zOrder;
            _startTime = currentTimeMs;
            _duration = Math.Max(1, durationMs);
            _currentFrame = 0;
            _lastFrameTime = currentTimeMs;
            RecoveredNativeLayerState = BuildRecoveredNativeLayerState(
                _relativeToTarget,
                !_suppressTargetFlip,
                _startOffset,
                _endOffset,
                _zOrder,
                _duration);
            RecoveredNativeExecutionTrace = BuildRecoveredNativeExecutionTrace(RecoveredNativeLayerState);
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
            bool flip = !_suppressTargetFlip && (_getTargetFlip?.Invoke() ?? false);
            float drawX = anchorPosition.X + _offsetX + animatedOffset.X;
            float drawY = anchorPosition.Y + _offsetY + animatedOffset.Y;
            int drawShiftX = -(int)drawX - mapShiftX;
            int drawShiftY = -(int)drawY - mapShiftY;
            byte alpha = AnimationEffects.ResolveFollowParticleAlpha(currentTimeMs - _startTime, _duration);
            if (alpha == byte.MaxValue)
            {
                frame.DrawObject(spriteBatch, skeletonRenderer, gameTime, drawShiftX, drawShiftY, flip, null);
                return;
            }

            frame.DrawBackground(
                spriteBatch,
                skeletonRenderer,
                gameTime,
                frame.X - drawShiftX,
                frame.Y - drawShiftY,
                new Color(byte.MaxValue, byte.MaxValue, byte.MaxValue, alpha),
                flip,
                null);
        }

        internal static FollowParticleRecoveredNativeLayerState BuildRecoveredNativeLayerState(
            bool relativeToTarget,
            bool appliesOwnerFlip,
            Vector2 startOffset,
            Vector2 endOffset,
            int zOrder,
            int durationMs)
        {
            return new FollowParticleRecoveredNativeLayerState(
                relativeToTarget,
                appliesOwnerFlip,
                startOffset,
                endOffset,
                zOrder,
                Math.Max(1, durationMs),
                AlphaStart: byte.MaxValue,
                AlphaEnd: byte.MinValue,
                RegistersRepeatLayer: true);
        }

        internal static FollowParticleRecoveredNativeOperation[] BuildRecoveredNativeExecutionTrace(
            FollowParticleRecoveredNativeLayerState layerState)
        {
            var operations = new List<FollowParticleRecoveredNativeOperation>(6)
            {
                BuildRecoveredNativeOperation(FollowParticleRecoveredNativeOperationKind.LoadLayer, layerState)
            };

            if (layerState.AppliesOwnerFlip)
            {
                operations.Add(BuildRecoveredNativeOperation(FollowParticleRecoveredNativeOperationKind.PutFlip, layerState));
            }

            operations.Add(BuildRecoveredNativeOperation(FollowParticleRecoveredNativeOperationKind.RelOffset, layerState));
            operations.Add(BuildRecoveredNativeOperation(FollowParticleRecoveredNativeOperationKind.AlphaRelMove, layerState));
            operations.Add(BuildRecoveredNativeOperation(FollowParticleRecoveredNativeOperationKind.AnimateRepeat, layerState));
            operations.Add(BuildRecoveredNativeOperation(FollowParticleRecoveredNativeOperationKind.RegisterRepeatAnimation, layerState));
            return operations.ToArray();
        }

        private static FollowParticleRecoveredNativeOperation BuildRecoveredNativeOperation(
            FollowParticleRecoveredNativeOperationKind kind,
            FollowParticleRecoveredNativeLayerState layerState)
        {
            return new FollowParticleRecoveredNativeOperation(
                kind,
                layerState.RelativeToTarget,
                layerState.AppliesOwnerFlip,
                layerState.StartOffset,
                layerState.EndOffset,
                layerState.ZOrder,
                layerState.DurationMs,
                layerState.AlphaStart,
                layerState.AlphaEnd);
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
            int currentTimeMs,
            int initialElapsedMs = 0)
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

            if (initialElapsedMs > 0)
            {
                SeekElapsed(initialElapsedMs, currentTimeMs);
            }
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

        private void SeekElapsed(int elapsedMs, int currentTimeMs)
        {
            int remainingMs = Math.Max(0, elapsedMs);
            int guard = 0;
            while (remainingMs > 0 && !_finished && guard++ < 4096)
            {
                List<IDXObject> frames = GetCurrentFrames();
                if (!AnimationEffects.HasFrames(frames))
                {
                    if (!AdvancePhase(currentTimeMs))
                    {
                        break;
                    }

                    continue;
                }

                if (_currentFrame < 0 || _currentFrame >= frames.Count)
                {
                    _currentFrame = 0;
                }

                int frameDelay = Math.Max(1, frames[_currentFrame]?.Delay ?? 1);
                if (remainingMs < frameDelay)
                {
                    _lastFrameTime = currentTimeMs - remainingMs;
                    return;
                }

                remainingMs -= frameDelay;
                _currentFrame++;
                if (_currentFrame < frames.Count)
                {
                    continue;
                }

                if (!AdvancePhase(currentTimeMs))
                {
                    break;
                }
            }

            _lastFrameTime = currentTimeMs;
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
