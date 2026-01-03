using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.MapObjects.FieldObject;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Objects.FieldObject
{
    public class NpcItem : BaseDXDrawableItem
    {
        private readonly NpcInstance npcInstance;
        public NpcInstance NpcInstance
        {
            get { return npcInstance; }
            private set { }
        }

        private NameTooltipItem nameTooltip = null;
        private NameTooltipItem npcDescTooltip = null;

        // Animation system (similar to MobItem)
        private readonly NpcAnimationSet _animationSet;
        private string _currentAction = "stand";
        private List<IDXObject> _currentFrames;
        private int _currentFrameIndex = 0;
        private int _lastFrameSwitchTime = 0;

        // Action cycling (based on MapleNecrocer) - only when standing
        private int _actionCycleCounter = 0;
        private const int ACTION_CYCLE_INTERVAL = 1000; // Cycle to random action every ~1000 frames
        private static readonly Random _random = new Random();

        // Movement system
        public NpcMovementInfo MovementInfo { get; private set; }
        public bool MovementEnabled { get; set; } = true;

        // Cached mirror boundary (optimization - avoid recalculating every frame)
        private ReflectionDrawableBoundary _cachedMirrorBoundary = null;
        private int _lastMirrorCheckX = int.MinValue;
        private int _lastMirrorCheckY = int.MinValue;
        private const int MIRROR_CHECK_THRESHOLD = 50;

        /// <summary>
        /// Constructor with animation set
        /// </summary>
        /// <param name="npcInstance"></param>
        /// <param name="animationSet"></param>
        /// <param name="nameTooltip"></param>
        /// <param name="npcDescTooltip"></param>
        public NpcItem(NpcInstance npcInstance, NpcAnimationSet animationSet, NameTooltipItem nameTooltip, NameTooltipItem npcDescTooltip)
            : base(animationSet.GetFrames("stand") ?? animationSet.GetFrames(null), npcInstance.Flip)
        {
            this.npcInstance = npcInstance;
            this.nameTooltip = nameTooltip;
            this.npcDescTooltip = npcDescTooltip;
            this._animationSet = animationSet;

            // Set initial animation
            _currentFrames = animationSet.GetFrames("stand") ?? animationSet.GetFrames(null);
            _currentAction = "stand";

            // Randomize initial counter so NPCs don't all change action at once
            _actionCycleCounter = _random.Next(ACTION_CYCLE_INTERVAL);

            // Initialize movement
            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a multi frame NPC (legacy support)
        /// </summary>
        /// <param name="npcInstance"></param>
        /// <param name="frames"></param>
        /// <param name="nameTooltip"></param>
        /// <param name="npcDescTooltip"></param>
        public NpcItem(NpcInstance npcInstance, List<IDXObject> frames, NameTooltipItem nameTooltip, NameTooltipItem npcDescTooltip)
            : base(frames, npcInstance.Flip)
        {
            this.npcInstance = npcInstance;
            this.nameTooltip = nameTooltip;
            this.npcDescTooltip = npcDescTooltip;

            // Create a simple animation set with all frames as "stand"
            _animationSet = new NpcAnimationSet();
            _animationSet.AddAnimation("stand", frames);
            _currentFrames = frames;

            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a single frame NPC (legacy support)
        /// </summary>
        /// <param name="npcInstance"></param>
        /// <param name="frame0"></param>
        /// <param name="nameTooltip"></param>
        /// <param name="npcDescTooltip"></param>
        public NpcItem(NpcInstance npcInstance, IDXObject frame0, NameTooltipItem nameTooltip, NameTooltipItem npcDescTooltip)
            : base(frame0, npcInstance.Flip)
        {
            this.npcInstance = npcInstance;
            this.nameTooltip = nameTooltip;
            this.npcDescTooltip = npcDescTooltip;

            // Create a simple animation set
            _animationSet = new NpcAnimationSet();
            _animationSet.AddAnimation("stand", new List<IDXObject> { frame0 });
            _currentFrames = new List<IDXObject> { frame0 };

            InitializeMovement();
        }

        /// <summary>
        /// Initialize movement info from NPC instance data
        /// </summary>
        private void InitializeMovement()
        {
            MovementInfo = new NpcMovementInfo();
            MovementInfo.Initialize(
                npcInstance.X,
                npcInstance.Y,
                npcInstance.rx0Shift,
                npcInstance.rx1Shift,
                _animationSet?.CanWalk ?? false
            );
        }

        /// <summary>
        /// Set the current animation action
        /// </summary>
        /// <param name="action">Action name (stand, speak, blink, etc.)</param>
        public void SetAction(string action)
        {
            if (action == _currentAction)
                return;

            var newFrames = _animationSet?.GetFrames(action);
            if (newFrames != null && newFrames.Count > 0)
            {
                _currentAction = action;
                _currentFrames = newFrames;
                _currentFrameIndex = 0;  // Reset to first frame
            }
        }

        /// <summary>
        /// Update NPC movement and action cycling
        /// </summary>
        /// <param name="deltaTimeMs">Time elapsed since last update in milliseconds</param>
        public void Update(int deltaTimeMs)
        {
            // Update movement
            if (MovementEnabled && MovementInfo != null && MovementInfo.CanMove)
            {
                MovementInfo.UpdateMovement(deltaTimeMs);

                // Update flip based on movement direction
                // NPC sprites typically face left by default, so invert the flip
                this.flip = !MovementInfo.FlipX;

                // Update animation based on movement state
                if (MovementInfo.IsMoving)
                {
                    // Use move/walk animation
                    string walkAction = _animationSet.HasAnimation("move") ? "move" : "walk";
                    SetAction(walkAction);
                }
                else
                {
                    // Standing - cycle through idle actions
                    UpdateActionCycle();
                }
            }
            else
            {
                // No movement - just cycle through actions
                UpdateActionCycle();
            }
        }

        /// <summary>
        /// Update action cycling - only when standing still
        /// Based on MapleNecrocer's NPC action cycling
        /// </summary>
        private void UpdateActionCycle()
        {
            if (_animationSet == null || _animationSet.ActionCount <= 1)
                return;

            // Don't cycle if we're in move/walk action
            if (_currentAction == "move" || _currentAction == "walk")
            {
                SetAction("stand");
                return;
            }

            _actionCycleCounter++;
            if (_actionCycleCounter >= ACTION_CYCLE_INTERVAL)
            {
                _actionCycleCounter = 0;
                // Switch to a random action (excluding move/walk)
                string newAction;
                int attempts = 0;
                do
                {
                    newAction = _animationSet.GetRandomAction(_random);
                    attempts++;
                } while ((newAction == "move" || newAction == "walk") && attempts < 5);

                if (newAction != "move" && newAction != "walk")
                {
                    SetAction(newAction);
                }
            }
        }

        /// <summary>
        /// Get the current animation frame based on time
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDXObject GetCurrentAnimationFrame(int tickCount)
        {
            if (_currentFrames == null || _currentFrames.Count == 0)
                return null;

            if (_currentFrames.Count == 1)
                return _currentFrames[0];

            // Check if it's time to switch frames
            IDXObject currentFrame = _currentFrames[_currentFrameIndex];
            int delay = currentFrame.Delay > 0 ? currentFrame.Delay : 100;

            if (tickCount - _lastFrameSwitchTime > delay)
            {
                _currentFrameIndex = (_currentFrameIndex + 1) % _currentFrames.Count;
                _lastFrameSwitchTime = tickCount;
                currentFrame = _currentFrames[_currentFrameIndex];
            }

            return currentFrame;
        }

        public override void Draw(SpriteBatch sprite, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY,
            ReflectionDrawableBoundary drawReflectionInfo,
            RenderParameters renderParameters,
            int TickCount)
        {
            // Calculate position offset from movement
            int positionOffsetX = 0;
            int positionOffsetY = 0;

            if (MovementEnabled && MovementInfo != null && MovementInfo.CanMove)
            {
                positionOffsetX = (int)(MovementInfo.X - npcInstance.X);
                positionOffsetY = (int)(MovementInfo.Y - npcInstance.Y);
            }

            int adjustedMapShiftX = mapShiftX - positionOffsetX;
            int adjustedMapShiftY = mapShiftY - positionOffsetY;

            // Get current frame from animation
            IDXObject drawFrame = GetCurrentAnimationFrame(TickCount);

            if (drawFrame != null)
            {
                int shiftCenteredX = adjustedMapShiftX - centerX;
                int shiftCenteredY = adjustedMapShiftY - centerY;

                if (IsFrameWithinView(drawFrame, shiftCenteredX, shiftCenteredY,
                    renderParameters.RenderWidth, renderParameters.RenderHeight))
                {
                    drawFrame.DrawObject(sprite, skeletonMeshRenderer, gameTime,
                        shiftCenteredX, shiftCenteredY,
                        flip,
                        drawReflectionInfo);
                }
            }

            // Draw name tooltip
            if (nameTooltip != null)
            {
                nameTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    adjustedMapShiftX, adjustedMapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }

            // Draw description tooltip
            if (npcDescTooltip != null)
            {
                npcDescTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    adjustedMapShiftX, adjustedMapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }
        }

        /// <summary>
        /// Gets the current X position of the NPC (considering movement)
        /// </summary>
        public int CurrentX => MovementEnabled && MovementInfo != null && MovementInfo.CanMove
            ? (int)MovementInfo.X
            : npcInstance.X;

        /// <summary>
        /// Gets the current Y position of the NPC (considering movement)
        /// </summary>
        public int CurrentY => MovementEnabled && MovementInfo != null && MovementInfo.CanMove
            ? (int)MovementInfo.Y
            : npcInstance.Y;

        /// <summary>
        /// Gets the cached mirror boundary for this NPC
        /// </summary>
        public ReflectionDrawableBoundary CachedMirrorBoundary => _cachedMirrorBoundary;

        /// <summary>
        /// Updates the cached mirror boundary if the NPC has moved significantly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMirrorBoundary(Rectangle mirrorBottomRect, ReflectionDrawableBoundary mirrorBottomReflection,
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData)
        {
            int npcX = CurrentX;
            int npcY = CurrentY;

            // Skip threshold check on first call to avoid int.MinValue overflow
            if (_lastMirrorCheckX != int.MinValue)
            {
                int dx = Math.Abs(npcX - _lastMirrorCheckX);
                int dy = Math.Abs(npcY - _lastMirrorCheckY);
                if (dx < MIRROR_CHECK_THRESHOLD && dy < MIRROR_CHECK_THRESHOLD)
                    return;
            }

            _lastMirrorCheckX = npcX;
            _lastMirrorCheckY = npcY;

            _cachedMirrorBoundary = null;
            if (mirrorBottomReflection != null && mirrorBottomRect.Contains(new Point(npcX, npcY)))
            {
                _cachedMirrorBoundary = mirrorBottomReflection;
            }
            else if (checkMirrorFieldData != null)
            {
                _cachedMirrorBoundary = checkMirrorFieldData(npcX, npcY);
            }
        }
    }
}
