using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Transportation Field System - Ship/vehicle movement between maps.
    ///
    /// Packet handling (CField_ContiMove::OnContiMove):
    /// - Case 8 (OnStartShipMoveField): If value == 2, LeaveShipMove (ship departs)
    /// - Case 10 (OnMoveField): If value == 4, AppearShip; If value == 5, DisappearShip (Balrog)
    /// - Case 12 (OnEndShipMoveField): If value == 6, EnterShipMove (ship arrives)
    ///
    /// Ship kinds (from CShip::Init):
    /// - m_nShipKind == 0: Regular ship (moves from x0 to x, e.g. Orbis-Ellinia)
    /// - m_nShipKind == 1: Balrog type (appears/disappears with alpha fade)
    /// </summary>
    public class TransportationField
    {
        #region Constants
        // Animation timing in milliseconds
        private const int ALPHA_FADE_DURATION = 1000; // Alpha fade for Balrog appear/disappear
        #endregion

        #region Ship Properties (matching CShip class structure)
        // Ship kind: 0 = regular ship, 1 = Balrog type
        private int _shipKind = 0;

        // Ship positions from WZ (Map.wz/Map/Map*/mapId/info/ship)
        private int _x;      // Target X position (docked position)
        private int _y;      // Y position (constant during movement)
        private int _x0;     // Start X position (away from dock)
        private int _f;      // Flip direction (0 = right, 1 = left)
        private int _tMove;  // Movement duration in seconds

        // Balrog type limits
        private int _limitX0, _limitX, _limitY0, _limitY;

        // Ship path for animation
        private string _shipPath;
        #endregion

        #region Runtime State
        private ShipState _state = ShipState.Idle;
        private float _currentX, _currentY;
        private float _currentAlpha = 255f;
        private int _moveStartTime;
        private float _startMoveX, _endMoveX;
        private float _startAlpha, _endAlpha;
        #endregion

        #region Balrog Attack State
        private BalrogState _balrogState = BalrogState.Hidden;
        private float _balrogX, _balrogY;
        private float _balrogAlpha = 0f;
        private int _balrogMoveStartTime;
        private float _balrogStartX, _balrogEndX;
        private float _balrogStartY, _balrogEndY;
        private float _balrogStartAlpha, _balrogEndAlpha;
        private int _balrogMoveDuration = 1000;
        #endregion

        #region Visual Properties
        private int _shipWidth = 400;
        private int _shipHeight = 200;
        private List<IDXObject> _shipFrames;
        private int _shipFrameIndex = 0;
        private int _lastShipFrameTime = 0;

        private List<IDXObject> _balrogFrames;
        private int _balrogFrameIndex = 0;
        private int _lastBalrogFrameTime = 0;
        #endregion

        #region Background Scrolling
        private float _bgScrollX;
        private float _bgScrollSpeed = 50f;
        private bool _enableBgScroll = true;
        #endregion

        #region Events and Announcements
        public event Action OnDeparture;
        public event Action OnArrival;
        public event Action OnBalrogAppear;
        public event Action OnBalrogDisappear;

        private readonly Queue<TransportAnnouncement> _announcements = new();
        private TransportAnnouncement _currentAnnouncement;
        #endregion

        #region Public Properties
        public ShipState State => _state;
        public BalrogState BalrogStatus => _balrogState;
        public float ShipX => _currentX;
        public float ShipY => _currentY;
        public float ShipAlpha => _currentAlpha;
        public float BalrogX => _balrogX;
        public float BalrogY => _balrogY;
        public float BalrogAlpha => _balrogAlpha;
        public float BackgroundScrollX => _bgScrollX;
        public bool HasShipTextures => _shipFrames != null && _shipFrames.Count > 0;
        public bool HasBalrogTextures => _balrogFrames != null && _balrogFrames.Count > 0;
        public bool IsActive => _state != ShipState.Idle;
        public bool IsBalrogVisible => _balrogState != BalrogState.Hidden && _balrogAlpha > 0;

        public float VoyageProgress
        {
            get
            {
                if (_state != ShipState.Moving)
                    return _state == ShipState.Docked ? 1f : 0f;
                int elapsed = Environment.TickCount - _moveStartTime;
                return Math.Clamp((float)elapsed / (_tMove * 1000), 0f, 1f);
            }
        }
        #endregion

        #region Initialization

        /// <summary>
        /// Initialize ship from WZ configuration (matching CShip::Init)
        /// </summary>
        /// <param name="shipKind">0 = regular ship, 1 = Balrog type</param>
        /// <param name="x">Docked X position</param>
        /// <param name="y">Y position</param>
        /// <param name="x0">Away X position (only for shipKind 0)</param>
        /// <param name="f">Flip direction (0 = right, 1 = left)</param>
        /// <param name="tMove">Movement duration in seconds</param>
        /// <param name="shipPath">Path to ship animation in WZ</param>
        public void Initialize(int shipKind, int x, int y, int x0, int f, int tMove, string shipPath = null)
        {
            _shipKind = shipKind;
            _x = x;
            _y = y;
            _x0 = x0;
            _f = f;
            _tMove = tMove > 0 ? tMove : 3; // Default 3 seconds
            _shipPath = shipPath;

            // Set current position based on ship kind
            if (_shipKind == 1) // Balrog type
            {
                // Balrog limits (matching client: m_x +/- 50, m_y +/- 100)
                _limitX0 = _x - 50;
                _limitX = _x + 50;
                _limitY0 = _y - 100;
                _limitY = _y + 100;
                _currentX = _x;
                _currentY = _y;
                _currentAlpha = 0f; // Hidden until AppearShip
            }
            else // Regular ship
            {
                _currentX = _x0; // Start at away position
                _currentY = _y;
                _currentAlpha = 255f;
            }

            _state = ShipState.Idle;
            _balrogState = BalrogState.Hidden;

            System.Diagnostics.Debug.WriteLine($"[TransportField] Initialized: kind={shipKind}, x={x}, y={y}, x0={x0}, f={f}, tMove={tMove}s");
        }

        /// <summary>
        /// Initialize with simple route (legacy compatibility)
        /// </summary>
        public void InitializeRoute(float startX, float endX, float y, int voyageDurationMs, float speed = 100f)
        {
            Initialize(
                shipKind: 0,
                x: (int)endX,
                y: (int)y,
                x0: (int)startX,
                f: startX < endX ? 0 : 1,
                tMove: voyageDurationMs / 1000
            );
        }

        public void SetShipVisual(int width, int height, bool flip = false)
        {
            _shipWidth = width;
            _shipHeight = height;
            _f = flip ? 1 : 0;
        }

        public void SetBackgroundScroll(bool enabled, float speed = 50f)
        {
            _enableBgScroll = enabled;
            _bgScrollSpeed = speed;
        }

        public void SetShipFrames(List<IDXObject> frames)
        {
            _shipFrames = frames;
            _shipFrameIndex = 0;
            if (frames != null && frames.Count > 0)
            {
                _shipWidth = frames[0].Width;
                _shipHeight = frames[0].Height;
            }
        }

        public void SetBalrogFrames(List<IDXObject> frames)
        {
            _balrogFrames = frames;
            _balrogFrameIndex = 0;
        }

        #endregion

        #region Ship Control (matching CField_ContiMove packet handling)

        /// <summary>
        /// OnStartShipMoveField - Case 8 with value 2
        /// Ship leaves the dock (LeaveShipMove)
        /// For regular ships: moves from docked (x) to away (x0) position
        /// </summary>
        public void LeaveShipMove()
        {
            if (_shipKind != 0) return; // Only for regular ships

            System.Diagnostics.Debug.WriteLine("[TransportField] LeaveShipMove - Ship departing");

            _state = ShipState.Moving;
            _moveStartTime = Environment.TickCount;
            _startMoveX = _x;  // Start at dock
            _endMoveX = _x0;   // End at away position
            _currentX = _startMoveX;

            OnDeparture?.Invoke();
            QueueAnnouncement("The ship is now departing.", 2000);
        }

        /// <summary>
        /// OnEndShipMoveField - Case 12 with value 6
        /// Ship arrives at the dock (EnterShipMove)
        /// For regular ships: moves from away (x0) to docked (x) position
        /// </summary>
        public void EnterShipMove()
        {
            if (_shipKind != 0) return; // Only for regular ships

            System.Diagnostics.Debug.WriteLine("[TransportField] EnterShipMove - Ship arriving");

            _state = ShipState.Moving;
            _moveStartTime = Environment.TickCount;
            _startMoveX = _x0; // Start at away position
            _endMoveX = _x;    // End at dock
            _currentX = _startMoveX;

            QueueAnnouncement("The ship is arriving.", 2000);
        }

        /// <summary>
        /// OnMoveField - Case 10 with value 4
        /// Balrog appears (AppearShip)
        /// For Balrog type: fades in and moves from offset position to center
        /// </summary>
        public void AppearShip()
        {
            if (_shipKind != 1) return; // Only for Balrog type

            System.Diagnostics.Debug.WriteLine("[TransportField] AppearShip - Balrog appearing");

            _state = ShipState.Appearing;
            _moveStartTime = Environment.TickCount;

            // Start position: offset based on flip (matching client logic)
            // m_x + 100 * (2 * (m_f == 0) - 1), m_y - 100
            int offsetX = 100 * (2 * (_f == 0 ? 1 : 0) - 1); // +100 if facing right, -100 if facing left
            _startMoveX = _x + offsetX;
            _endMoveX = _x;
            _currentX = _startMoveX;
            _currentY = _y - 100; // Start 100 pixels higher

            _startAlpha = 0f;
            _endAlpha = 255f;
            _currentAlpha = 0f;

            OnBalrogAppear?.Invoke();
            QueueAnnouncement("Balrog has appeared!", 2000);
        }

        /// <summary>
        /// OnMoveField - Case 10 with value 5
        /// Balrog disappears (DisappearShip)
        /// For Balrog type: fades out
        /// </summary>
        public void DisappearShip()
        {
            if (_shipKind != 1) return; // Only for Balrog type

            System.Diagnostics.Debug.WriteLine("[TransportField] DisappearShip - Balrog disappearing");

            _state = ShipState.Disappearing;
            _moveStartTime = Environment.TickCount;

            _startAlpha = 255f;
            _endAlpha = 0f;
            _currentAlpha = 255f;

            OnBalrogDisappear?.Invoke();
        }

        /// <summary>
        /// Trigger Balrog attack during voyage (separate from ship movement)
        /// This is for the Crimson Balrog attack event on regular ships
        /// </summary>
        public void TriggerBalrogAttack(int durationMs = 5000)
        {
            if (_balrogState != BalrogState.Hidden) return;

            System.Diagnostics.Debug.WriteLine("[TransportField] TriggerBalrogAttack");

            _balrogState = BalrogState.Appearing;
            _balrogMoveStartTime = Environment.TickCount;
            _balrogMoveDuration = durationMs;

            // Balrog appears from the right side of the ship
            _balrogStartX = _currentX + _shipWidth + 200;
            _balrogEndX = _currentX + _shipWidth / 2 + 100;
            _balrogStartY = _currentY - 50;
            _balrogEndY = _currentY - _shipHeight / 2;
            _balrogX = _balrogStartX;
            _balrogY = _balrogStartY;

            _balrogStartAlpha = 0f;
            _balrogEndAlpha = 255f;
            _balrogAlpha = 0f;

            OnBalrogAppear?.Invoke();
            QueueAnnouncement("Balrog has appeared!", 2000);
        }

        /// <summary>
        /// Force start voyage (for testing)
        /// </summary>
        public void ForceDeparture()
        {
            if (_shipKind == 0)
            {
                LeaveShipMove();
            }
            else
            {
                AppearShip();
            }
        }

        /// <summary>
        /// Skip to arrival (for testing)
        /// </summary>
        public void ForceArrival()
        {
            if (_shipKind == 0)
            {
                EnterShipMove();
            }
            else
            {
                DisappearShip();
            }
        }

        /// <summary>
        /// Legacy compatibility
        /// </summary>
        public void OnStartShipMoveField(int departureDelayMs = 10000)
        {
            // Wait then depart
            _state = ShipState.WaitingDeparture;
            _moveStartTime = Environment.TickCount;
            _startMoveX = departureDelayMs; // Store delay in startMoveX temporarily

            QueueAnnouncement("The ship will depart shortly.", 3000);
        }

        public void OnEndShipMoveField()
        {
            _state = ShipState.Docked;
            _currentX = _x;
            OnArrival?.Invoke();
            QueueAnnouncement("We have arrived at our destination.", 3000);
        }

        #endregion

        #region Update

        public void Update(int currentTimeMs, float deltaSeconds)
        {
            switch (_state)
            {
                case ShipState.WaitingDeparture:
                    UpdateWaitingDeparture(currentTimeMs);
                    break;

                case ShipState.Moving:
                    UpdateMoving(currentTimeMs, deltaSeconds);
                    break;

                case ShipState.Appearing:
                    UpdateAppearing(currentTimeMs);
                    break;

                case ShipState.Disappearing:
                    UpdateDisappearing(currentTimeMs);
                    break;
            }

            // Update Balrog attack (separate from ship state)
            UpdateBalrogAttack(currentTimeMs);

            // Update announcements
            UpdateAnnouncements(currentTimeMs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateWaitingDeparture(int currentTimeMs)
        {
            int elapsed = currentTimeMs - _moveStartTime;
            int delay = (int)_startMoveX; // Delay stored here

            if (elapsed > delay - 5000 && elapsed < delay - 4900)
            {
                QueueAnnouncement("5 seconds until departure!", 1000);
            }

            if (elapsed >= delay)
            {
                LeaveShipMove();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateMoving(int currentTimeMs, float deltaSeconds)
        {
            int elapsed = currentTimeMs - _moveStartTime;
            int duration = _tMove * 1000; // Convert to milliseconds

            float progress = Math.Clamp((float)elapsed / duration, 0f, 1f);
            float eased = SmoothStep(progress);

            _currentX = MathHelper.Lerp(_startMoveX, _endMoveX, eased);

            // Update background scroll during voyage
            if (_enableBgScroll)
            {
                _bgScrollX += _bgScrollSpeed * deltaSeconds;
            }

            if (progress >= 1f)
            {
                _currentX = _endMoveX;

                // Determine next state based on direction
                if (Math.Abs(_endMoveX - _x0) < 1f)
                {
                    // Arrived at away position (departed)
                    _state = ShipState.InTransit;
                    System.Diagnostics.Debug.WriteLine("[TransportField] Ship departed, now in transit");
                }
                else
                {
                    // Arrived at dock
                    _state = ShipState.Docked;
                    OnArrival?.Invoke();
                    System.Diagnostics.Debug.WriteLine("[TransportField] Ship docked");
                    QueueAnnouncement("We have arrived at our destination.", 3000);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateAppearing(int currentTimeMs)
        {
            int elapsed = currentTimeMs - _moveStartTime;
            int duration = _tMove * 1000;

            float progress = Math.Clamp((float)elapsed / duration, 0f, 1f);
            float eased = SmoothStep(progress);

            _currentX = MathHelper.Lerp(_startMoveX, _endMoveX, eased);
            _currentY = MathHelper.Lerp(_y - 100, (float)_y, eased);
            _currentAlpha = MathHelper.Lerp(_startAlpha, _endAlpha, eased);

            if (progress >= 1f)
            {
                _currentX = _endMoveX;
                _currentY = _y;
                _currentAlpha = 255f;
                _state = ShipState.Visible;
                System.Diagnostics.Debug.WriteLine("[TransportField] Balrog fully appeared");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateDisappearing(int currentTimeMs)
        {
            int elapsed = currentTimeMs - _moveStartTime;
            int duration = _tMove * 1000;

            float progress = Math.Clamp((float)elapsed / duration, 0f, 1f);
            _currentAlpha = MathHelper.Lerp(_startAlpha, _endAlpha, progress);

            if (progress >= 1f)
            {
                _currentAlpha = 0f;
                _state = ShipState.Idle;
                System.Diagnostics.Debug.WriteLine("[TransportField] Balrog disappeared");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBalrogAttack(int currentTimeMs)
        {
            if (_balrogState == BalrogState.Hidden) return;

            int elapsed = currentTimeMs - _balrogMoveStartTime;

            switch (_balrogState)
            {
                case BalrogState.Appearing:
                    {
                        float progress = Math.Clamp((float)elapsed / 1000f, 0f, 1f);
                        float eased = SmoothStep(progress);

                        _balrogX = MathHelper.Lerp(_balrogStartX, _balrogEndX, eased);
                        _balrogY = MathHelper.Lerp(_balrogStartY, _balrogEndY, eased);
                        _balrogAlpha = MathHelper.Lerp(0f, 255f, eased);

                        if (progress >= 1f)
                        {
                            _balrogState = BalrogState.Attacking;
                            _balrogMoveStartTime = currentTimeMs;
                        }
                    }
                    break;

                case BalrogState.Attacking:
                    {
                        // Hover/attack animation
                        float hover = (float)Math.Sin(elapsed * 0.003) * 20f;
                        _balrogY = _balrogEndY + hover;

                        // Track ship position
                        _balrogX = _currentX + _shipWidth / 2 + 100;

                        if (elapsed >= _balrogMoveDuration)
                        {
                            _balrogState = BalrogState.Disappearing;
                            _balrogMoveStartTime = currentTimeMs;
                            _balrogStartX = _balrogX;
                            _balrogStartY = _balrogY;
                        }
                    }
                    break;

                case BalrogState.Disappearing:
                    {
                        float progress = Math.Clamp((float)elapsed / 1000f, 0f, 1f);

                        _balrogX = MathHelper.Lerp(_balrogStartX, _balrogStartX + 200, progress);
                        _balrogAlpha = MathHelper.Lerp(255f, 0f, progress);

                        if (progress >= 1f)
                        {
                            _balrogState = BalrogState.Hidden;
                            _balrogAlpha = 0f;
                            OnBalrogDisappear?.Invoke();
                        }
                    }
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private void UpdateAnnouncements(int currentTimeMs)
        {
            if (_currentAnnouncement != null)
            {
                if (currentTimeMs - _currentAnnouncement.StartTime > _currentAnnouncement.Duration)
                {
                    _currentAnnouncement = null;
                }
            }

            if (_currentAnnouncement == null && _announcements.Count > 0)
            {
                _currentAnnouncement = _announcements.Dequeue();
                _currentAnnouncement.StartTime = currentTimeMs;
            }
        }

        #endregion

        #region Announcements

        public void QueueAnnouncement(string message, int durationMs)
        {
            _announcements.Enqueue(new TransportAnnouncement
            {
                Message = message,
                Duration = durationMs
            });
        }

        public TransportAnnouncement CurrentAnnouncement => _currentAnnouncement;

        #endregion

        #region Entity Sync

        public Vector2 GetShipOffset()
        {
            return new Vector2(_currentX - _x, 0);
        }

        public bool IsOnShipDeck(float x, float y, float deckY, float deckWidth)
        {
            float shipLeft = _currentX - deckWidth / 2;
            float shipRight = _currentX + deckWidth / 2;
            float tolerance = 10f;

            return x >= shipLeft && x <= shipRight &&
                   y >= deckY - tolerance && y <= deckY + tolerance;
        }

        #endregion

        #region Draw

        public void Draw(SpriteBatch spriteBatch, SkeletonMeshRenderer skeletonMeshRenderer, GameTime gameTime,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int tickCount,
            Texture2D fallbackTexture = null, SpriteFont debugFont = null)
        {
            if (_state == ShipState.Idle && _currentAlpha <= 0)
                return;

            // If no textures, use debug drawing with fallback texture
            if (!HasShipTextures)
            {
                if (fallbackTexture != null)
                {
                    DrawDebug(spriteBatch, fallbackTexture, mapShiftX, mapShiftY, centerX, centerY, debugFont);
                }
                return;
            }

            // Calculate screen position from map coordinates
            // Map position → Screen position: screenX = mapX - mapShiftX + centerX
            int shiftCenterX = mapShiftX - centerX;
            int shiftCenterY = mapShiftY - centerY;

            // Draw ship - all frames/layers are drawn together (not animated sequentially)
            // Ship objects are composed of multiple parts that render simultaneously
            if (_shipFrames != null && _shipFrames.Count > 0 && _currentAlpha > 0)
            {
                Color tint = new Color(255, 255, 255, (int)_currentAlpha);

                // Draw all ship frames/layers at the same position
                foreach (var shipFrame in _shipFrames)
                {
                    // LoadFrames stores X = -originX, Y = -originY (offset from anchor to top-left)
                    // Convert map coords to screen coords, then add frame offset
                    int screenX = (int)_currentX - shiftCenterX + shipFrame.X;
                    int screenY = (int)_currentY - shiftCenterY + shipFrame.Y;

                    // Use DrawBackground which takes screen x,y directly (not map shift)
                    shipFrame.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime,
                        screenX, screenY, tint, _f != 0, null);
                }
            }

            // Draw Balrog if visible (attack event on regular ships)
            if (_balrogState != BalrogState.Hidden && HasBalrogTextures && _balrogAlpha > 0)
            {
                IDXObject balrogFrame = GetCurrentBalrogFrame(tickCount);
                if (balrogFrame != null)
                {
                    // LoadFrames stores X = -originX, Y = -originY (offset from anchor to top-left)
                    int balrogScreenX = (int)_balrogX - shiftCenterX + balrogFrame.X;
                    int balrogScreenY = (int)_balrogY - shiftCenterY + balrogFrame.Y;

                    Color tint = new Color(255, 255, 255, (int)_balrogAlpha);
                    balrogFrame.DrawBackground(spriteBatch, skeletonMeshRenderer, gameTime,
                        balrogScreenX, balrogScreenY, tint, true, null); // Flip to face ship
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDXObject GetCurrentShipFrame(int tickCount)
        {
            if (_shipFrames == null || _shipFrames.Count == 0)
                return null;

            if (_shipFrames.Count == 1)
                return _shipFrames[0];

            IDXObject currentFrame = _shipFrames[_shipFrameIndex];
            int delay = currentFrame.Delay > 0 ? currentFrame.Delay : 100;

            if (tickCount - _lastShipFrameTime > delay)
            {
                _shipFrameIndex = (_shipFrameIndex + 1) % _shipFrames.Count;
                _lastShipFrameTime = tickCount;
                currentFrame = _shipFrames[_shipFrameIndex];
            }

            return currentFrame;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IDXObject GetCurrentBalrogFrame(int tickCount)
        {
            if (_balrogFrames == null || _balrogFrames.Count == 0)
                return null;

            if (_balrogFrames.Count == 1)
                return _balrogFrames[0];

            IDXObject currentFrame = _balrogFrames[_balrogFrameIndex];
            int delay = currentFrame.Delay > 0 ? currentFrame.Delay : 100;

            if (tickCount - _lastBalrogFrameTime > delay)
            {
                _balrogFrameIndex = (_balrogFrameIndex + 1) % _balrogFrames.Count;
                _lastBalrogFrameTime = tickCount;
                currentFrame = _balrogFrames[_balrogFrameIndex];
            }

            return currentFrame;
        }

        public void DrawDebug(SpriteBatch spriteBatch, Texture2D pixelTexture,
            int mapShiftX, int mapShiftY, int centerX, int centerY, SpriteFont font = null)
        {
            if (_state == ShipState.Idle && _currentAlpha <= 0)
                return;

            // Skip if textures are loaded - Draw() handles that case
            // This prevents double-drawing when called from debug rendering section
            if (HasShipTextures)
                return;

            int shiftCenterX = mapShiftX - centerX;
            int shiftCenterY = mapShiftY - centerY;

            int screenX = (int)(_currentX - shiftCenterX - _shipWidth / 2);
            int screenY = (int)(_currentY - shiftCenterY - _shipHeight);

            // Draw ship announcements
            if (font != null && _currentAnnouncement != null)
            {
                Vector2 textSize = font.MeasureString(_currentAnnouncement.Message);
                spriteBatch.DrawString(font, _currentAnnouncement.Message,
                    new Vector2(screenX + (_shipWidth - textSize.X) / 2, screenY - 30),
                    Color.Yellow * _currentAnnouncement.Alpha);
            }
        }

        public float GetBackgroundScrollOffset(float parallaxFactor = 1f)
        {
            return _bgScrollX * parallaxFactor;
        }

        #endregion

        #region Utility

        public void Reset()
        {
            _state = ShipState.Idle;
            _balrogState = BalrogState.Hidden;
            _currentX = _shipKind == 0 ? _x0 : _x;
            _currentY = _y;
            _currentAlpha = _shipKind == 0 ? 255f : 0f;
            _balrogAlpha = 0f;
            _bgScrollX = 0;
            _announcements.Clear();
            _currentAnnouncement = null;
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Ship movement state (matching client CShip behavior)
    /// </summary>
    public enum ShipState
    {
        Idle,               // Not active
        WaitingDeparture,   // Waiting at dock before departure
        Moving,             // Moving between positions (LeaveShipMove/EnterShipMove)
        InTransit,          // In transit (after leaving, before arriving)
        Docked,             // Docked at destination
        Appearing,          // Balrog type: appearing with alpha fade
        Visible,            // Balrog type: fully visible
        Disappearing        // Balrog type: disappearing with alpha fade
    }

    /// <summary>
    /// Balrog attack state (for Crimson Balrog attack event)
    /// </summary>
    public enum BalrogState
    {
        Hidden,
        Appearing,
        Attacking,
        Disappearing
    }

    /// <summary>
    /// Transport state (legacy compatibility)
    /// </summary>
    public enum TransportState
    {
        Idle = 0,
        WaitingDeparture = 1,
        InTransit = 2,
        Arrived = 3
    }

    /// <summary>
    /// Transport announcement message
    /// </summary>
    public class TransportAnnouncement
    {
        public string Message;
        public int Duration;
        public int StartTime;

        public float Alpha
        {
            get
            {
                int elapsed = Environment.TickCount - StartTime;
                if (elapsed < 500)
                    return elapsed / 500f;
                if (elapsed > Duration - 500)
                    return (Duration - elapsed) / 500f;
                return 1f;
            }
        }
    }

    #endregion
}
