using HaCreator.MapEditor.Instance;
using MapleLib.WzLib.WzStructure.Data.MobStructure;
using HaCreator.MapSimulator.MapObjects.FieldObject;
using HaSharedLibrary.Render;
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

namespace HaCreator.MapSimulator.Objects.FieldObject
{
    public class MobItem : BaseDXDrawableItem
    {
        private readonly MobInstance mobInstance;
        private NameTooltipItem nameTooltip = null;

        // Animation system
        private readonly MobAnimationSet _animationSet;
        private string _currentAction = "stand";
        private string _previousAction = "";
        private List<IDXObject> _currentFrames;
        private int _currentFrameIndex = 0;
        private int _lastFrameSwitchTime = 0;
        private bool _isJumpingAnimation = false;     // Currently playing jump animation
        private bool _jumpAnimationCompleted = false; // Jump animation has played through once (hold last frame)

        // Cached mirror boundary (optimization - avoid recalculating every frame)
        private ReflectionDrawableBoundary _cachedMirrorBoundary = null;
        private int _lastMirrorCheckX = int.MinValue;
        private int _lastMirrorCheckY = int.MinValue;
        private const int MIRROR_CHECK_THRESHOLD = 50; // Only recheck if moved more than this

        /// <summary>
        /// Movement information for this mob (position, direction, speed, foothold)
        /// </summary>
        public MobMovementInfo MovementInfo { get; private set; }

        /// <summary>
        /// Whether movement is enabled for this mob in the simulator
        /// </summary>
        public bool MovementEnabled { get; set; } = true;

        /// <summary>
        /// Current animation action being played
        /// </summary>
        public string CurrentAction => _currentAction;

        /// <summary>
        /// Constructor with animation set
        /// </summary>
        /// <param name="mobInstance"></param>
        /// <param name="animationSet"></param>
        /// <param name="nameTooltip"></param>
        public MobItem(MobInstance mobInstance, MobAnimationSet animationSet, NameTooltipItem nameTooltip)
            : base(animationSet.GetFrames("stand") ?? animationSet.GetFrames(null), mobInstance.Flip)
        {
            this.mobInstance = mobInstance;
            this.nameTooltip = nameTooltip;
            this._animationSet = animationSet;

            // Set initial animation
            _currentFrames = animationSet.GetFrames("stand") ?? animationSet.GetFrames(null);
            _currentAction = "stand";
            _previousAction = "stand";

            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a multi frame mob image (legacy support)
        /// </summary>
        /// <param name="mobInstance"></param>
        /// <param name="frames"></param>
        /// <param name="nameTooltip"></param>
        public MobItem(MobInstance mobInstance, List<IDXObject> frames, NameTooltipItem nameTooltip)
            : base(frames, mobInstance.Flip)
        {
            this.mobInstance = mobInstance;
            this.nameTooltip = nameTooltip;

            // Create a simple animation set with all frames as "stand"
            _animationSet = new MobAnimationSet();
            _animationSet.AddAnimation("stand", frames);
            _currentFrames = frames;

            InitializeMovement();
        }

        /// <summary>
        /// Constructor for a single frame mob (legacy support)
        /// </summary>
        /// <param name="mobInstance"></param>
        /// <param name="frame0"></param>
        /// <param name="nameTooltip"></param>
        public MobItem(MobInstance mobInstance, IDXObject frame0, NameTooltipItem nameTooltip)
            : base(frame0, mobInstance.Flip)
        {
            this.mobInstance = mobInstance;
            this.nameTooltip = nameTooltip;

            // Create a simple animation set
            _animationSet = new MobAnimationSet();
            _animationSet.AddAnimation("stand", new List<IDXObject> { frame0 });
            _currentFrames = new List<IDXObject> { frame0 };

            InitializeMovement();
        }

        /// <summary>
        /// Initialize movement info from mob instance data
        /// </summary>
        private void InitializeMovement()
        {
            MovementInfo = new MobMovementInfo();

            // Get parsed mob data (cached per mob ID)
            var mobData = mobInstance.MobInfo?.MobData;

            // Use MobData if available, fallback to animation set
            bool isFlyingMob = mobData?.CanFly ?? _animationSet?.CanFly ?? false;
            bool isMobile = mobData?.IsMobile ?? false;
            bool isJumpingMob = mobData?.CanJump ?? _animationSet?.CanJump ?? false;
            bool noFlip = mobData?.NoFlip ?? false;

            // Also check animation set for movement capabilities (fallback)
            if (!isFlyingMob && _animationSet?.CanFly == true)
                isFlyingMob = true;
            if (!isJumpingMob && _animationSet?.CanJump == true)
                isJumpingMob = true;

            MovementInfo.Initialize(
                mobInstance.X,
                mobInstance.Y,
                mobInstance.rx0Shift,
                mobInstance.rx1Shift,
                mobInstance.yShift,  // Pass yShift for correct foothold positioning
                isFlyingMob,
                isJumpingMob,
                noFlip
            );

            // For noFlip mobs, set initial flip based on mob instance flip property
            if (noFlip)
            {
                MovementInfo.FlipX = mobInstance.Flip;
            }

            // If mob is not mobile (no fly/move/jump animations), set to Stand type
            if (!isMobile && !isFlyingMob && !isJumpingMob)
            {
                MovementInfo.MoveType = MobMoveType.Stand;
            }

            // Set default action based on movement type
            if (isFlyingMob)
            {
                MovementInfo.CurrentAction = MobAction.Fly;
                SetAction("fly");
            }
            else if (isJumpingMob)
            {
                MovementInfo.CurrentAction = MobAction.Stand;
                SetAction("stand");
            }
            else
            {
                MovementInfo.CurrentAction = MobAction.Stand;
                SetAction("stand");
            }

            // Get movement speed from mob data (formula from MapleNecrocer)
            // MoveSpeed = (1 + speed/100) * 2, default 2
            if (mobData != null)
            {
                if (mobData.Speed != 0)
                {
                    MovementInfo.MoveSpeed = (1 + (float)mobData.Speed / 100) * 2;
                }

                if (mobData.FlySpeed != 0)
                {
                    MovementInfo.FlySpeed = (1 + (float)mobData.FlySpeed / 100) * 2;
                }
            }
        }

        /// <summary>
        /// Set the current animation action
        /// </summary>
        /// <param name="action">Action name (stand, move, fly, etc.)</param>
        public void SetAction(string action)
        {
            if (action == _currentAction)
                return;

            // If currently in jump animation and mob is still in the air, don't allow change
            if (_isJumpingAnimation && MovementInfo != null && MovementInfo.JumpState != MobJumpState.None)
            {
                return;  // Wait for mob to land before changing animation
            }

            var newFrames = _animationSet?.GetFrames(action);
            if (newFrames != null && newFrames.Count > 0)
            {
                _previousAction = _currentAction;
                _currentAction = action;
                _currentFrames = newFrames;
                _currentFrameIndex = 0;  // Reset to first frame

                // Track if this is a jump animation
                _isJumpingAnimation = (action == "jump");
                _jumpAnimationCompleted = false;
            }
        }

        #region Custom Members
        public MobInstance MobInstance
        {
            get { return this.mobInstance; }
            private set { }
        }
        #endregion

        /// <summary>
        /// Set map boundaries for mob movement (VR rectangle)
        /// </summary>
        public void SetMapBoundaries(int left, int right, int top, int bottom)
        {
            if (MovementInfo != null)
            {
                MovementInfo.MapLeft = left;
                MovementInfo.MapRight = right;
                MovementInfo.MapTop = top;
                MovementInfo.MapBottom = bottom;
                // Flying mob Y adjustment is now handled in UpdateFlyingMovement
            }
        }

        /// <summary>
        /// Update mob movement and animation. Call this from MapSimulator.Update()
        /// </summary>
        /// <param name="deltaTimeMs">Time elapsed since last update in milliseconds</param>
        public void UpdateMovement(int deltaTimeMs)
        {
            if (!MovementEnabled || MovementInfo == null)
                return;

            MovementInfo.UpdateMovement(deltaTimeMs);

            // Update flip state based on movement direction
            this.flip = MovementInfo.FlipX;

            // Reset jump animation state when mob has landed
            if (_isJumpingAnimation && MovementInfo.JumpState == MobJumpState.None)
            {
                _isJumpingAnimation = false;
                _jumpAnimationCompleted = false;
            }

            // Update animation action based on movement state
            UpdateAnimationAction();
        }

        /// <summary>
        /// Update the animation action based on current movement state
        /// </summary>
        private void UpdateAnimationAction()
        {
            if (MovementInfo == null)
                return;

            string targetAction;

            switch (MovementInfo.MoveType)
            {
                case MobMoveType.Fly:
                    targetAction = "fly";
                    break;

                case MobMoveType.Jump:
                    // Use current action from movement info (jump when in air, move/stand on ground)
                    if (MovementInfo.CurrentAction == MobAction.Jump)
                    {
                        targetAction = _animationSet.HasAnimation("jump") ? "jump" : "stand";
                    }
                    else if (MovementInfo.CurrentAction == MobAction.Move)
                    {
                        targetAction = _animationSet.HasAnimation("move") ? "move" :
                                       _animationSet.HasAnimation("walk") ? "walk" : "stand";
                    }
                    else
                    {
                        targetAction = "stand";
                    }
                    break;

                case MobMoveType.Move:
                    // Check if mob is currently moving or standing
                    if (MovementInfo.CurrentAction == MobAction.Move)
                    {
                        // Use "move" or "walk" whichever is available
                        targetAction = _animationSet.HasAnimation("move") ? "move" :
                                       _animationSet.HasAnimation("walk") ? "walk" : "stand";
                    }
                    else
                    {
                        targetAction = "stand";
                    }
                    break;

                case MobMoveType.Stand:
                default:
                    targetAction = "stand";
                    break;
            }

            SetAction(targetAction);
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

            // If jump animation completed, hold the last frame while still in the air
            if (_isJumpingAnimation && _jumpAnimationCompleted)
            {
                return _currentFrames[_currentFrames.Count - 1];  // Return last frame
            }

            // Check if it's time to switch frames
            IDXObject currentFrame = _currentFrames[_currentFrameIndex];
            int delay = currentFrame.Delay > 0 ? currentFrame.Delay : 100;

            if (tickCount - _lastFrameSwitchTime > delay)
            {
                int nextIndex = _currentFrameIndex + 1;

                // For jump animation, don't loop - hold last frame
                if (_isJumpingAnimation && nextIndex >= _currentFrames.Count)
                {
                    _jumpAnimationCompleted = true;
                    _currentFrameIndex = _currentFrames.Count - 1;  // Stay on last frame
                }
                else
                {
                    _currentFrameIndex = nextIndex % _currentFrames.Count;
                }

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

            if (MovementEnabled && MovementInfo != null)
            {
                positionOffsetX = (int)(MovementInfo.X - mobInstance.X);
                positionOffsetY = (int)(MovementInfo.Y - mobInstance.Y);
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
        }

        /// <summary>
        /// Gets the current X position of the mob (considering movement)
        /// </summary>
        public int CurrentX => MovementEnabled && MovementInfo != null
            ? (int)MovementInfo.X
            : mobInstance.X;

        /// <summary>
        /// Gets the current Y position of the mob (considering movement)
        /// </summary>
        public int CurrentY => MovementEnabled && MovementInfo != null
            ? (int)MovementInfo.Y
            : mobInstance.Y;

        /// <summary>
        /// Gets the cached mirror boundary for this mob
        /// </summary>
        public ReflectionDrawableBoundary CachedMirrorBoundary => _cachedMirrorBoundary;

        /// <summary>
        /// Updates the cached mirror boundary if the mob has moved significantly.
        /// Call this once per frame to avoid redundant boundary checks.
        /// </summary>
        /// <param name="mirrorBottomRect">Mirror bottom rectangle</param>
        /// <param name="mirrorBottomReflection">Mirror bottom reflection info</param>
        /// <param name="checkMirrorFieldData">Function to check mirror field data boundaries</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateMirrorBoundary(Rectangle mirrorBottomRect, ReflectionDrawableBoundary mirrorBottomReflection,
            Func<int, int, ReflectionDrawableBoundary> checkMirrorFieldData)
        {
            int mobX = CurrentX;
            int mobY = CurrentY;

            // Only recalculate if mob has moved significantly (skip check on first call to avoid int.MinValue overflow)
            if (_lastMirrorCheckX != int.MinValue)
            {
                int dx = Math.Abs(mobX - _lastMirrorCheckX);
                int dy = Math.Abs(mobY - _lastMirrorCheckY);
                if (dx < MIRROR_CHECK_THRESHOLD && dy < MIRROR_CHECK_THRESHOLD)
                    return;
            }

            _lastMirrorCheckX = mobX;
            _lastMirrorCheckY = mobY;

            // Check mirror boundaries
            _cachedMirrorBoundary = null;
            if (mirrorBottomReflection != null && mirrorBottomRect.Contains(new Point(mobX, mobY)))
            {
                _cachedMirrorBoundary = mirrorBottomReflection;
            }
            else if (checkMirrorFieldData != null)
            {
                _cachedMirrorBoundary = checkMirrorFieldData(mobX, mobY);
            }
        }
    }
}
