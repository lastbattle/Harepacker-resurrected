using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Camera controller that provides smooth scrolling with easing,
    /// zoom controls, and focus tracking for the MapSimulator.
    /// Based on common 2D platformer camera techniques.
    /// </summary>
    public class CameraController
    {
        #region Constants
        // Smoothing factors (higher = faster tracking)
        private const float DEFAULT_HORIZONTAL_SMOOTHING = 8.0f;   // Horizontal follow speed
        private const float DEFAULT_VERTICAL_SMOOTHING = 6.0f;     // Vertical follow speed (slightly slower for platformers)
        private const float JUMP_VERTICAL_SMOOTHING = 2.5f;        // Slower vertical follow during jumps
        private const float LAND_VERTICAL_SMOOTHING = 10.0f;       // Faster vertical catch-up on landing
        private const float FALL_THRESHOLD_Y = 50.0f;              // Y distance before camera starts following down
        private const float JUMP_THRESHOLD_Y = 30.0f;              // Y distance before camera starts following up during jump

        // Zoom limits
        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 2.0f;
        private const float ZOOM_SPEED = 0.5f;                     // Zoom per scroll wheel tick
        private const float ZOOM_SMOOTHING = 5.0f;                 // Zoom interpolation speed

        // Focus transitions
        private const float FOCUS_TRANSITION_SPEED = 3.0f;         // Speed of transitioning to new focus target

        // Dead zones (camera won't move unless target is outside this zone)
        private const float DEAD_ZONE_X = 100.0f;                  // Horizontal dead zone
        private const float DEAD_ZONE_Y = 60.0f;                   // Vertical dead zone

        // Look-ahead (camera leads in direction of movement)
        private const float LOOK_AHEAD_X = 80.0f;                  // Horizontal look-ahead distance
        private const float LOOK_AHEAD_SMOOTHING = 4.0f;           // Look-ahead interpolation speed
        #endregion

        #region Fields
        // Current camera position (center of viewport in world coords)
        private float _positionX;
        private float _positionY;

        // Target position for smooth interpolation
        private float _targetX;
        private float _targetY;

        // Zoom
        private float _currentZoom = 1.0f;
        private float _targetZoom = 1.0f;

        // Map boundaries (in world coordinates)
        private float _boundaryLeft;
        private float _boundaryRight;
        private float _boundaryTop;
        private float _boundaryBottom;

        // Viewport dimensions
        private int _viewportWidth;
        private int _viewportHeight;

        // Map center offset
        private float _mapCenterX;
        private float _mapCenterY;

        // Object scaling
        private float _objectScaling = 1.0f;

        // State tracking
        private bool _isPlayerJumping;
        private bool _isPlayerFalling;
        private float _lastGroundY;
        private float _playerVelocityY;
        private float _previousPlayerY;

        // Look-ahead
        private float _currentLookAheadX;
        private bool _playerFacingRight = true;

        // Focus target (for mob/NPC focus feature)
        private Vector2? _focusTarget;
        private float _focusBlendFactor;

        // Smoothing settings (can be adjusted)
        private float _horizontalSmoothing = DEFAULT_HORIZONTAL_SMOOTHING;
        private float _verticalSmoothing = DEFAULT_VERTICAL_SMOOTHING;

        // Enabled features
        private bool _smoothScrollingEnabled = true;
        private bool _deadZoneEnabled = true;
        private bool _lookAheadEnabled = true;
        private bool _jumpSmoothingEnabled = true;
        #endregion

        #region Properties
        /// <summary>
        /// Current camera X position in world coordinates (center of viewport)
        /// </summary>
        public float PositionX => _positionX;

        /// <summary>
        /// Current camera Y position in world coordinates (center of viewport)
        /// </summary>
        public float PositionY => _positionY;

        /// <summary>
        /// Camera X offset for rendering (mapShiftX equivalent)
        /// </summary>
        public int MapShiftX => (int)(_positionX + _mapCenterX - _viewportWidth / 2f);

        /// <summary>
        /// Camera Y offset for rendering (mapShiftY equivalent)
        /// </summary>
        public int MapShiftY => (int)(_positionY + _mapCenterY - _viewportHeight / 2f);

        /// <summary>
        /// Current zoom level (1.0 = normal)
        /// </summary>
        public float Zoom => _currentZoom;

        /// <summary>
        /// Target zoom level for smooth interpolation
        /// </summary>
        public float TargetZoom
        {
            get => _targetZoom;
            set => _targetZoom = MathHelper.Clamp(value, MIN_ZOOM, MAX_ZOOM);
        }

        /// <summary>
        /// Enable/disable smooth scrolling (if disabled, camera snaps to target)
        /// </summary>
        public bool SmoothScrollingEnabled
        {
            get => _smoothScrollingEnabled;
            set => _smoothScrollingEnabled = value;
        }

        /// <summary>
        /// Enable/disable dead zone (area where player can move without camera moving)
        /// </summary>
        public bool DeadZoneEnabled
        {
            get => _deadZoneEnabled;
            set => _deadZoneEnabled = value;
        }

        /// <summary>
        /// Enable/disable look-ahead (camera leads in movement direction)
        /// </summary>
        public bool LookAheadEnabled
        {
            get => _lookAheadEnabled;
            set => _lookAheadEnabled = value;
        }

        /// <summary>
        /// Enable/disable special handling for jumping (smoother vertical tracking)
        /// </summary>
        public bool JumpSmoothingEnabled
        {
            get => _jumpSmoothingEnabled;
            set => _jumpSmoothingEnabled = value;
        }

        /// <summary>
        /// Horizontal smoothing factor (higher = faster tracking)
        /// </summary>
        public float HorizontalSmoothing
        {
            get => _horizontalSmoothing;
            set => _horizontalSmoothing = Math.Max(0.1f, value);
        }

        /// <summary>
        /// Vertical smoothing factor (higher = faster tracking)
        /// </summary>
        public float VerticalSmoothing
        {
            get => _verticalSmoothing;
            set => _verticalSmoothing = Math.Max(0.1f, value);
        }

        /// <summary>
        /// Whether camera is currently focused on a specific target (mob/NPC)
        /// </summary>
        public bool HasFocusTarget => _focusTarget.HasValue;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the camera controller with map and viewport settings
        /// </summary>
        public void Initialize(
            Rectangle fieldBoundary,
            int viewportWidth,
            int viewportHeight,
            float mapCenterX,
            float mapCenterY,
            float objectScaling = 1.0f)
        {
            _boundaryLeft = fieldBoundary.Left;
            _boundaryRight = fieldBoundary.Right;
            _boundaryTop = fieldBoundary.Top;
            _boundaryBottom = fieldBoundary.Bottom;

            _viewportWidth = viewportWidth;
            _viewportHeight = viewportHeight;
            _mapCenterX = mapCenterX;
            _mapCenterY = mapCenterY;
            _objectScaling = objectScaling;

            // Reset state
            _focusTarget = null;
            _focusBlendFactor = 0;
            _currentLookAheadX = 0;
            _isPlayerJumping = false;
            _isPlayerFalling = false;
        }

        /// <summary>
        /// Set the initial camera position (snaps immediately, no smoothing)
        /// </summary>
        public void SetPosition(float x, float y)
        {
            _positionX = x;
            _positionY = y;
            _targetX = x;
            _targetY = y;
            _lastGroundY = y;
            _previousPlayerY = y;

            // Apply boundary clamping
            ClampToBoundaries();
        }

        /// <summary>
        /// Teleport camera to position (snaps immediately)
        /// </summary>
        public void TeleportTo(float x, float y)
        {
            SetPosition(x, y);
        }
        #endregion

        #region Update
        /// <summary>
        /// Update camera position based on player position and state
        /// </summary>
        /// <param name="playerX">Player world X position</param>
        /// <param name="playerY">Player world Y position</param>
        /// <param name="playerFacingRight">Is player facing right?</param>
        /// <param name="isOnGround">Is player on ground?</param>
        /// <param name="deltaTime">Time since last frame in seconds</param>
        public void Update(float playerX, float playerY, bool playerFacingRight, bool isOnGround, float deltaTime)
        {
            // Clamp deltaTime to prevent huge jumps
            deltaTime = Math.Min(deltaTime, 0.1f);

            // Horizontal: always follow player smoothly
            _targetX = playerX;

            // Vertical: MapleStory-style camera behavior
            // - Camera stays stable during jumps (doesn't follow every pixel)
            // - Camera updates Y target only when:
            //   1. Player lands on ground (update to new platform height)
            //   2. Player goes significantly above/below camera (threshold)
            const float VERTICAL_THRESHOLD = 120f; // Pixels before camera follows during jump/fall

            if (isOnGround)
            {
                // Player is on ground - update camera Y target to player position
                // This handles landing on new platforms
                if (!_wasOnGround)
                {
                    // Just landed - start landing delay timer
                    _targetY = playerY;
                    _landingDelayTimer = LANDING_DELAY_DURATION;
                }
                else
                {
                    // Walking on ground - follow player Y (for slopes, etc.)
                    _targetY = playerY;
                    // Decrement landing delay timer
                    if (_landingDelayTimer > 0)
                    {
                        _landingDelayTimer -= deltaTime;
                    }
                }
                _lastGroundY = playerY;
                _wasOnGround = true;
            }
            else
            {
                // Player is in the air (jumping or falling)
                // Only update camera Y if player goes significantly above/below current camera position
                float distanceFromCamera = playerY - _positionY;

                if (Math.Abs(distanceFromCamera) > VERTICAL_THRESHOLD)
                {
                    // Player has moved significantly - camera should follow
                    // But only move enough to keep player within threshold
                    if (distanceFromCamera > VERTICAL_THRESHOLD)
                    {
                        // Player is below camera threshold
                        _targetY = playerY - VERTICAL_THRESHOLD;
                    }
                    else if (distanceFromCamera < -VERTICAL_THRESHOLD)
                    {
                        // Player is above camera threshold
                        _targetY = playerY + VERTICAL_THRESHOLD;
                    }
                }
                // Otherwise keep camera Y stable (don't update _targetY)

                _wasOnGround = false;
            }

            // Handle focus target blending
            if (_focusTarget.HasValue)
            {
                _focusBlendFactor = Math.Min(_focusBlendFactor + FOCUS_TRANSITION_SPEED * deltaTime, 1.0f);
                _targetX = MathHelper.Lerp(_targetX, _focusTarget.Value.X, _focusBlendFactor);
                _targetY = MathHelper.Lerp(_targetY, _focusTarget.Value.Y, _focusBlendFactor);
            }
            else if (_focusBlendFactor > 0)
            {
                _focusBlendFactor = Math.Max(_focusBlendFactor - FOCUS_TRANSITION_SPEED * deltaTime, 0);
            }

            // Smooth camera movement
            if (_smoothScrollingEnabled)
            {
                // Horizontal: normal smooth follow
                float hSmoothFactor = 0.15f;
                _positionX = MathHelper.Lerp(_positionX, _targetX, hSmoothFactor);

                // Vertical: speed based on distance and state
                // The further the camera is from target, the slower it moves (prevents fast snap)
                float yDistance = Math.Abs(_targetY - _positionY);
                float vSmoothFactor;

                // Check if we're in landing delay period (just landed, camera should stay slow)
                bool inLandingDelay = isOnGround && _landingDelayTimer > 0;

                if (inLandingDelay)
                {
                    // During landing delay - use very slow catch-up similar to falling
                    // This prevents the jarring fast catch-up right after landing
                    if (yDistance < 10)
                        vSmoothFactor = 0.06f;
                    else if (yDistance < 50)
                        vSmoothFactor = 0.03f;
                    else
                        vSmoothFactor = 0.02f;
                }
                else if (yDistance < 10)
                {
                    // Very close - normal speed
                    vSmoothFactor = 0.12f;
                }
                else if (yDistance < 50)
                {
                    // Close - medium speed
                    vSmoothFactor = 0.08f;
                }
                else if (yDistance < 150)
                {
                    // Medium distance - slow
                    vSmoothFactor = 0.04f;
                }
                else
                {
                    // Far distance - very slow (MapleStory style gradual catch-up)
                    vSmoothFactor = 0.025f;
                }

                // Even slower when actively falling
                if (!isOnGround && playerY > _positionY)
                {
                    vSmoothFactor *= 0.5f;
                }

                _positionY = MathHelper.Lerp(_positionY, _targetY, vSmoothFactor);
            }
            else
            {
                // No smoothing - snap to target
                _positionX = _targetX;
                _positionY = _targetY;
            }

            // Smooth zoom
            if (Math.Abs(_currentZoom - _targetZoom) > 0.001f)
            {
                float zoomFactor = 0.1f;
                _currentZoom = MathHelper.Lerp(_currentZoom, _targetZoom, zoomFactor);
            }
            else
            {
                _currentZoom = _targetZoom;
            }

            // Boundary clamping handled by MapSimulator
        }

        // Track previous ground state
        private bool _wasOnGround = true;

        // Landing delay timer - keeps camera slow after landing
        private float _landingDelayTimer = 0f;
        private const float LANDING_DELAY_DURATION = 0.6f; // Seconds to wait after landing before speeding up

        /// <summary>
        /// Update camera for free camera mode (not following player)
        /// </summary>
        public void UpdateFreeCamera(bool left, bool right, bool up, bool down, int moveSpeed, float deltaTime)
        {
            float dx = 0, dy = 0;
            if (left) dx -= moveSpeed;
            if (right) dx += moveSpeed;
            if (up) dy -= moveSpeed;
            if (down) dy += moveSpeed;

            _targetX += dx * deltaTime;
            _targetY += dy * deltaTime;

            if (_smoothScrollingEnabled)
            {
                float factor = 1.0f - (float)Math.Exp(-10.0f * deltaTime); // Fast follow in free camera
                _positionX = MathHelper.Lerp(_positionX, _targetX, factor);
                _positionY = MathHelper.Lerp(_positionY, _targetY, factor);
            }
            else
            {
                _positionX = _targetX;
                _positionY = _targetY;
            }

            ClampToBoundaries();
        }
        #endregion

        #region Private Methods
        private void UpdatePlayerState(float playerY, bool isOnGround)
        {
            // Calculate player vertical velocity
            _playerVelocityY = playerY - _previousPlayerY;

            // Track jumping/falling state
            if (isOnGround)
            {
                if (_isPlayerJumping || _isPlayerFalling)
                {
                    // Just landed
                    _isPlayerJumping = false;
                    _isPlayerFalling = false;
                }
                _lastGroundY = playerY;
            }
            else
            {
                if (_playerVelocityY < -0.1f) // Moving up
                {
                    _isPlayerJumping = true;
                    _isPlayerFalling = false;
                }
                else if (_playerVelocityY > 0.1f) // Moving down
                {
                    _isPlayerJumping = false;
                    _isPlayerFalling = true;
                }
            }
        }

        private void CalculateTarget(float playerX, float playerY, bool playerFacingRight, float deltaTime)
        {
            float targetX = playerX;
            float targetY = playerY;

            // Apply look-ahead
            if (_lookAheadEnabled)
            {
                float desiredLookAhead = playerFacingRight ? LOOK_AHEAD_X : -LOOK_AHEAD_X;
                float lookAheadFactor = 1.0f - (float)Math.Exp(-LOOK_AHEAD_SMOOTHING * deltaTime);
                _currentLookAheadX = MathHelper.Lerp(_currentLookAheadX, desiredLookAhead, lookAheadFactor);
                targetX += _currentLookAheadX;
            }

            // Apply dead zone
            if (_deadZoneEnabled)
            {
                // Horizontal dead zone
                float dx = targetX - _positionX;
                if (Math.Abs(dx) < DEAD_ZONE_X)
                {
                    targetX = _positionX + Math.Sign(dx) * Math.Max(0, Math.Abs(dx) - DEAD_ZONE_X);
                    if (Math.Abs(dx) < DEAD_ZONE_X * 0.5f)
                        targetX = _positionX; // Full dead zone - don't move
                }

                // Vertical dead zone with jump handling
                if (_jumpSmoothingEnabled && (_isPlayerJumping || _isPlayerFalling))
                {
                    // During jump: only follow if player goes significantly above/below
                    float threshold = _isPlayerJumping ? JUMP_THRESHOLD_Y : FALL_THRESHOLD_Y;
                    float dy = targetY - _positionY;
                    if (Math.Abs(dy) < threshold)
                    {
                        targetY = _positionY;
                    }
                }
                else
                {
                    // On ground: normal dead zone
                    float dy = targetY - _positionY;
                    if (Math.Abs(dy) < DEAD_ZONE_Y)
                    {
                        targetY = _positionY;
                    }
                }
            }

            _targetX = targetX;
            _targetY = targetY;
            _playerFacingRight = playerFacingRight;
        }

        private float GetVerticalSmoothing()
        {
            if (!_jumpSmoothingEnabled)
                return _verticalSmoothing;

            if (_isPlayerJumping)
            {
                // Slow vertical follow during jumps (camera stays more stable)
                return JUMP_VERTICAL_SMOOTHING;
            }
            else if (_isPlayerFalling)
            {
                // Medium follow during falls
                return _verticalSmoothing * 0.6f;
            }
            else
            {
                // Just landed or on ground - catch up quickly
                float distanceToGround = Math.Abs(_positionY - _lastGroundY);
                if (distanceToGround > 20)
                {
                    return LAND_VERTICAL_SMOOTHING;
                }
                return _verticalSmoothing;
            }
        }

        private void ClampToBoundaries()
        {
            // NOTE: Boundary clamping is handled by MapSimulator.SetCameraMoveX/Y
            // after getting MapShiftX/MapShiftY from this controller.
            // This method is intentionally empty to avoid double-clamping issues.
            // The MapSimulator's boundary logic accounts for scaling and map size
            // correctly, so we defer to it.
        }
        #endregion

        #region Focus Control
        /// <summary>
        /// Focus camera on a specific world position (mob, NPC, etc.)
        /// </summary>
        public void FocusOn(float worldX, float worldY)
        {
            _focusTarget = new Vector2(worldX, worldY);
            _focusBlendFactor = 0; // Start transition
        }

        /// <summary>
        /// Clear the focus target, returning camera to player tracking
        /// </summary>
        public void ClearFocus()
        {
            _focusTarget = null;
            // focusBlendFactor will naturally decrease in Update()
        }
        #endregion

        #region Zoom Control
        /// <summary>
        /// Zoom in by one step
        /// </summary>
        public void ZoomIn()
        {
            TargetZoom = _targetZoom + ZOOM_SPEED * 0.1f;
        }

        /// <summary>
        /// Zoom out by one step
        /// </summary>
        public void ZoomOut()
        {
            TargetZoom = _targetZoom - ZOOM_SPEED * 0.1f;
        }

        /// <summary>
        /// Reset zoom to 1.0
        /// </summary>
        public void ResetZoom()
        {
            _targetZoom = 1.0f;
        }

        /// <summary>
        /// Set zoom level directly (with smoothing)
        /// </summary>
        public void SetZoom(float zoom)
        {
            TargetZoom = zoom;
        }

        /// <summary>
        /// Set zoom level immediately (no smoothing)
        /// </summary>
        public void SetZoomImmediate(float zoom)
        {
            _currentZoom = MathHelper.Clamp(zoom, MIN_ZOOM, MAX_ZOOM);
            _targetZoom = _currentZoom;
        }
        #endregion

        #region Screen Shake Effect
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeElapsed;
        private Random _shakeRandom = new Random();

        /// <summary>
        /// Offset X from shake effect (add to final render position)
        /// </summary>
        public float ShakeOffsetX { get; private set; }

        /// <summary>
        /// Offset Y from shake effect (add to final render position)
        /// </summary>
        public float ShakeOffsetY { get; private set; }

        /// <summary>
        /// Start a screen shake effect
        /// </summary>
        /// <param name="intensity">Maximum shake offset in pixels</param>
        /// <param name="duration">Duration in seconds</param>
        public void StartShake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeElapsed = 0;
        }

        /// <summary>
        /// Update shake effect (call from main Update after camera position update)
        /// </summary>
        public void UpdateShake(float deltaTime)
        {
            if (_shakeDuration <= 0 || _shakeElapsed >= _shakeDuration)
            {
                ShakeOffsetX = 0;
                ShakeOffsetY = 0;
                return;
            }

            _shakeElapsed += deltaTime;

            // Calculate remaining shake intensity (decreases over time)
            float progress = _shakeElapsed / _shakeDuration;
            float currentIntensity = _shakeIntensity * (1.0f - progress);

            // Random shake offset
            ShakeOffsetX = (float)(_shakeRandom.NextDouble() * 2 - 1) * currentIntensity;
            ShakeOffsetY = (float)(_shakeRandom.NextDouble() * 2 - 1) * currentIntensity;
        }
        #endregion

        #region Debug
        /// <summary>
        /// Get debug info string
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Camera: Pos({_positionX:F1}, {_positionY:F1}) Target({_targetX:F1}, {_targetY:F1}) " +
                   $"Shift({MapShiftX}, {MapShiftY}) Zoom:{_currentZoom:F2} " +
                   $"Jump:{_isPlayerJumping} Fall:{_isPlayerFalling} Focus:{HasFocusTarget}";
        }
        #endregion
    }
}
