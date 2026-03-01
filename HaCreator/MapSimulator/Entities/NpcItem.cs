using HaCreator.MapEditor.Instance;
using HaCreator.MapSimulator.Animation;
using HaCreator.MapSimulator.Core;
using HaSharedLibrary.Render;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Entities
{
    public class NpcItem : BaseDXDrawableItem, IInteractiveEntity
    {
        private readonly NpcInstance _npcInstance;
        public NpcInstance NpcInstance
        {
            get { return _npcInstance; }
            private set { }
        }

        private NameTooltipItem _nameTooltip = null;
        private NameTooltipItem _npcDescTooltip = null;

        // Animation system - using AnimationController for unified frame management
        private readonly NpcAnimationSet _animationSet;
        private readonly AnimationController _animationController;

        // Action cycling (based on MapleNecrocer) - only when standing
        private int _actionCycleCounter = 0;
        private const int ACTION_CYCLE_INTERVAL = 1000; // Cycle to random action every ~1000 frames
        private static readonly Random _random = new Random();

        // Movement system
        public NpcMovementInfo MovementInfo { get; private set; }
        public bool MovementEnabled { get; set; } = true;

        // Cached mirror boundary (optimization - avoid recalculating every frame)
        private readonly CachedBoundaryChecker _boundaryChecker = new CachedBoundaryChecker();

        /// <summary>
        /// Constructor with animation set
        /// </summary>
        /// <param name="_npcInstance"></param>
        /// <param name="animationSet"></param>
        /// <param name="_nameTooltip"></param>
        /// <param name="_npcDescTooltip"></param>
        public NpcItem(NpcInstance _npcInstance, NpcAnimationSet animationSet, NameTooltipItem _nameTooltip, NameTooltipItem _npcDescTooltip)
            : base(animationSet.GetFrames(AnimationKeys.Stand) ?? animationSet.GetFrames(null), _npcInstance.Flip)
        {
            this._npcInstance = _npcInstance;
            this._nameTooltip = _nameTooltip;
            this._npcDescTooltip = _npcDescTooltip;
            this._animationSet = animationSet;

            // Initialize animation controller
            _animationController = new AnimationController(animationSet, AnimationKeys.Stand);

            // Randomize initial counter so NPCs don't all change action at once
            _actionCycleCounter = _random.Next(ACTION_CYCLE_INTERVAL);

            // Initialize movement
            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a multi frame NPC (legacy support)
        /// </summary>
        /// <param name="_npcInstance"></param>
        /// <param name="frames"></param>
        /// <param name="_nameTooltip"></param>
        /// <param name="_npcDescTooltip"></param>
        public NpcItem(NpcInstance _npcInstance, List<IDXObject> frames, NameTooltipItem _nameTooltip, NameTooltipItem _npcDescTooltip)
            : base(frames, _npcInstance.Flip)
        {
            this._npcInstance = _npcInstance;
            this._nameTooltip = _nameTooltip;
            this._npcDescTooltip = _npcDescTooltip;

            // Create a simple animation set with all frames as "stand"
            _animationSet = new NpcAnimationSet();
            _animationSet.AddAnimation(AnimationKeys.Stand, frames);

            // Initialize animation controller
            _animationController = new AnimationController(_animationSet, AnimationKeys.Stand);

            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a single frame NPC (legacy support)
        /// </summary>
        /// <param name="_npcInstance"></param>
        /// <param name="frame0"></param>
        /// <param name="_nameTooltip"></param>
        /// <param name="_npcDescTooltip"></param>
        public NpcItem(NpcInstance _npcInstance, IDXObject frame0, NameTooltipItem _nameTooltip, NameTooltipItem _npcDescTooltip)
            : base(frame0, _npcInstance.Flip)
        {
            this._npcInstance = _npcInstance;
            this._nameTooltip = _nameTooltip;
            this._npcDescTooltip = _npcDescTooltip;

            // Create a simple animation set
            _animationSet = new NpcAnimationSet();
            _animationSet.AddAnimation(AnimationKeys.Stand, new List<IDXObject> { frame0 });

            // Initialize animation controller
            _animationController = new AnimationController(_animationSet, AnimationKeys.Stand);

            InitializeMovement();
        }

        /// <summary>
        /// Initialize movement info from NPC instance data
        /// </summary>
        private void InitializeMovement()
        {
            MovementInfo = new NpcMovementInfo();
            MovementInfo.Initialize(
                _npcInstance.X,
                _npcInstance.Y,
                _npcInstance.rx0Shift,
                _npcInstance.rx1Shift,
                _animationSet?.CanWalk ?? false
            );
        }

        /// <summary>
        /// Set the current animation action
        /// </summary>
        /// <param name="action">Action name (stand, speak, blink, etc.)</param>
        public void SetAction(string action)
        {
            _animationController?.SetAction(action);
        }

        /// <summary>
        /// Gets the current animation action
        /// </summary>
        public string CurrentAction => _animationController?.CurrentAction ?? AnimationKeys.Stand;

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
                    string walkAction = _animationSet.HasAnimation(AnimationKeys.Move) ? AnimationKeys.Move : AnimationKeys.Walk;
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
            if (CurrentAction == AnimationKeys.Move || CurrentAction == AnimationKeys.Walk)
            {
                SetAction(AnimationKeys.Stand);
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
                } while ((newAction == AnimationKeys.Move || newAction == AnimationKeys.Walk) && attempts < 5);

                if (newAction != AnimationKeys.Move && newAction != AnimationKeys.Walk)
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
            if (_animationController == null)
                return null;

            // Update the animation controller's frame
            _animationController.UpdateFrame(tickCount);

            return _animationController.GetCurrentFrame();
        }

        /// <summary>
        /// Gets the current frame for external use (e.g., size calculations)
        /// </summary>
        public IDXObject GetCurrentFrame()
        {
            return _animationController?.GetCurrentFrame();
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
                positionOffsetX = (int)(MovementInfo.X - _npcInstance.X);
                positionOffsetY = (int)(MovementInfo.Y - _npcInstance.Y);
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
            if (_nameTooltip != null)
            {
                _nameTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
                    adjustedMapShiftX, adjustedMapShiftY, centerX, centerY,
                    null,
                    renderParameters,
                    TickCount);
            }

            // Draw description tooltip
            if (_npcDescTooltip != null)
            {
                _npcDescTooltip.Draw(sprite, skeletonMeshRenderer, gameTime,
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
            : _npcInstance.X;

        /// <summary>
        /// Gets the current Y position of the NPC (considering movement)
        /// </summary>
        public int CurrentY => MovementEnabled && MovementInfo != null && MovementInfo.CanMove
            ? (int)MovementInfo.Y
            : _npcInstance.Y;

        /// <summary>
        /// Gets the cached mirror boundary for this NPC
        /// </summary>
        public ReflectionDrawableBoundary CachedMirrorBoundary => _boundaryChecker.CachedBoundary;

        /// <summary>
        /// Check if a map point is within the NPC's bounds
        /// Uses the same calculation as the debug overlay for consistency
        /// </summary>
        /// <param name="mapX">Map X coordinate (mouse screen X converted to map coords)</param>
        /// <param name="mapY">Map Y coordinate (mouse screen Y converted to map coords)</param>
        /// <returns>True if the point is within the NPC's bounds</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsMapPoint(int mapX, int mapY)
        {
            int npcX = CurrentX;
            int npcY = CurrentY;

            // Use NpcInstance Width/Height like the debug overlay does
            // NPC position is at their feet, hitbox extends upward
            int width = Math.Max(100, _npcInstance.Width + 40);
            int height = Math.Max(120, _npcInstance.Height);

            // Left edge calculation matches debug overlay: instance.X - (instance.Width - 20)
            int left = npcX - (_npcInstance.Width - 20);
            int right = left + width;
            int top = npcY - _npcInstance.Height;
            int bottom = npcY;

            // Check if point is within bounds
            return mapX >= left && mapX <= right && mapY >= top && mapY <= bottom;
        }

        /// <summary>
        /// Updates the cached mirror boundary if the NPC has moved significantly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMirrorBoundary(Rectangle mirrorBottomRect, ReflectionDrawableBoundary mirrorBottomReflection,
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData)
        {
            _boundaryChecker.UpdateBoundary(CurrentX, CurrentY, mirrorBottomRect, mirrorBottomReflection, checkMirrorFieldData);
        }
    }
}
