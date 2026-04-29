using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SD = System.Drawing;
using HaSharedLibrary.Util;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Limited View Field System - Fog of war / viewport restriction.
    /// Creates a circular or rectangular visibility area around the player position,
    /// with darkness/fog covering the rest of the map.
    ///
    /// Used in maps like Kerning Square PQ, certain event maps, and exploration maps.
    /// </summary>
    public class LimitedViewField
    {
        internal const int ClientOwnedDarkCanvasWidth = 1024;
        internal const int ClientOwnedDarkCanvasHeight = 768;
        internal const int ClientOwnedDarkLayerOffsetX = -512;
        internal const int ClientOwnedDarkLayerOffsetY = -468;
        internal const int ClientOwnedSmallDarkCanvasWidth = 316;
        internal const int ClientOwnedSmallDarkCanvasHeight = 316;
        internal const int ClientOwnedCanvasClassStringPoolId = 0x3D0;
        internal const int ClientOwnedViewrangePathStringPoolId = 0xE6E;
        internal const int ClientOwnedDarkLayerColorArgb = unchecked((int)0xC006060A);

        internal enum ClientOwnedInitOperationKind
        {
            ReleaseExistingDarkCanvas,
            ClearPreviousMaskHistory,
            CreateDarkCanvas,
            FillDarkCanvasBlack,
            CreateDarkLayer,
            InsertDarkCanvasIntoLayer,
            BindDarkLayerToGraphicsCenter,
            MoveDarkLayer,
            SetDarkLayerColor,
            SetDarkLayerZ,
            CreateSmallDarkCanvas,
            FillSmallDarkCanvasBlack,
            LoadViewrangeCanvas,
            DrawInitialViewrange
        }

        internal readonly struct ClientOwnedInitOperation
        {
            public ClientOwnedInitOperation(
                ClientOwnedInitOperationKind kind,
                int width = 0,
                int height = 0,
                int x = 0,
                int y = 0,
                string source = null)
            {
                Kind = kind;
                Width = Math.Max(0, width);
                Height = Math.Max(0, height);
                X = x;
                Y = y;
                Source = source ?? string.Empty;
            }

            public ClientOwnedInitOperationKind Kind { get; }
            public int Width { get; }
            public int Height { get; }
            public int X { get; }
            public int Y { get; }
            public string Source { get; }
        }

        internal enum ClientOwnedDrawViewrangeOperationKind
        {
            AcquireDarkLayerCanvas,
            AcquireSmallDarkCanvas,
            AcquireViewrangeCanvas,
            ResolvePreviousSmallDarkPatchRectangle,
            RestorePreviousSmallDarkPatch,
            ClearPreviousMaskHistory,
            ResolveLocalUserPosition,
            ResolveGraphicsCenter,
            QueryViewrangeCanvasDimensions,
            DrawDarkLayerFallback,
            ResolveViewrangeCopyRectangles,
            ResolveDarkLayerBounds,
            ClipViewrangeCopyRectangles,
            SkipViewrangeCopyOutsideDarkLayer,
            PrepareRemoveAlphaViewrangeCopy,
            CopyLocalViewrange,
            EvaluateShareViewRemoteLoop,
            SkipRemoteViewrangeBecauseShareViewDisabled,
            SkipRemoteViewrangeBecauseLocalUserMissing,
            ResolveRemoteUserPosition,
            CopyRemoteViewrange,
            AppendPreviousMaskHistory
        }

        internal readonly struct ClientOwnedDrawViewrangeOperation
        {
            public ClientOwnedDrawViewrangeOperation(
                ClientOwnedDrawViewrangeOperationKind kind,
                Vector2 topLeft,
                int maskIndex,
                int sourceX = 0,
                int sourceY = 0,
                int sourceWidth = 0,
                int sourceHeight = 0,
                int destinationX = 0,
                int destinationY = 0,
                int destinationWidth = 0,
                int destinationHeight = 0,
                bool usesRemoveAlphaCopy = false)
            {
                Kind = kind;
                TopLeft = topLeft;
                MaskIndex = maskIndex;
                SourceX = Math.Max(0, sourceX);
                SourceY = Math.Max(0, sourceY);
                SourceWidth = Math.Max(0, sourceWidth);
                SourceHeight = Math.Max(0, sourceHeight);
                DestinationX = destinationX;
                DestinationY = destinationY;
                DestinationWidth = Math.Max(0, destinationWidth);
                DestinationHeight = Math.Max(0, destinationHeight);
                UsesRemoveAlphaCopy = usesRemoveAlphaCopy;
            }

            public ClientOwnedDrawViewrangeOperationKind Kind { get; }
            public Vector2 TopLeft { get; }
            public int MaskIndex { get; }
            public int SourceX { get; }
            public int SourceY { get; }
            public int SourceWidth { get; }
            public int SourceHeight { get; }
            public int DestinationX { get; }
            public int DestinationY { get; }
            public int DestinationWidth { get; }
            public int DestinationHeight { get; }
            public bool UsesRemoveAlphaCopy { get; }
        }

        #region View Mode
        public enum ViewMode
        {
            None,           // No fog of war
            Circle,         // Circular visibility area
            Rectangle,      // Rectangular visibility area
            Spotlight,      // Soft-edged spotlight effect
            Flashlight      // Directional flashlight effect
        }
        #endregion

        #region Configuration
        private ViewMode _mode = ViewMode.None;
        private float _viewRadius = 300f;           // Radius for circle/spotlight
        private float _viewWidth = 400f;            // Width for rectangle
        private float _viewHeight = 300f;           // Height for rectangle
        private float _edgeSoftness = 50f;          // Soft edge transition width
        private float _flashlightAngle = 0f;        // Angle for flashlight mode (radians)
        private float _flashlightSpread = 45f;      // Spread angle in degrees
        private Color _fogColor = new Color(0, 0, 0, 230); // Semi-transparent black
        private bool _followPlayer = true;          // Follow player position
        private float _centerX, _centerY;           // Fixed center if not following
        private bool _clientOwnedImmediateMode;
        private bool _clientOwnedUpdateParityMode;
        private bool _clientOwnedShareView;
        private float _clientOwnedMaskWidth;
        private float _clientOwnedMaskHeight;
        private float _clientOwnedMaskOriginX;
        private float _clientOwnedMaskOriginY;
        private int _clientOwnedSmallDarkPatchWidth;
        private int _clientOwnedSmallDarkPatchHeight;
        private int _clientOwnedDarkLayerWidth;
        private int _clientOwnedDarkLayerHeight;
        private int _clientOwnedDarkLayerOffsetX;
        private int _clientOwnedDarkLayerOffsetY;
        private Vector2 _clientOwnedScreenMaskCenter;
        private bool _clientOwnedFocusWorldPositionValid;
        private Vector2 _clientOwnedFocusWorldPosition;
        private readonly List<Vector2> _clientOwnedRemoteFocusWorldPositions = new();
        private readonly List<Vector2> _clientOwnedScreenMaskCentersBuffer = new();
        private readonly List<Vector2> _clientOwnedMaskTopLeftsBuffer = new();
        private readonly List<Vector2> _clientOwnedPreviousMaskTopLefts = new();
        #endregion

        #region Runtime State
        private bool _enabled = false;
        private float _currentRadius;
        private float _targetRadius;
        private float _radiusTransitionSpeed = 200f; // pixels per second
        private float _currentAlpha = 1f;
        private float _targetAlpha = 1f;
        private float _alphaTransitionSpeed = 2f;   // per second

        // Pulse effect for spotlight
        private bool _pulseEnabled = false;
        private float _pulseAmplitude = 20f;
        private float _pulseFrequency = 2f;
        private float _pulsePhase = 0f;
        #endregion

        #region Textures
        private Texture2D _gradientCircle;          // Radial gradient for soft edges
        private Texture2D _pixelTexture;            // 1x1 white pixel for drawing
        private Texture2D _clientOwnedViewrangeTexture;
        private GraphicsDevice _device;
        private int _screenWidth, _screenHeight;
        #endregion

        #region Public Properties
        public bool Enabled => _enabled;
        public ViewMode Mode => _mode;
        public float ViewRadius => _currentRadius;
        public Color FogColor => _fogColor;
        internal bool UsesClientOwnedUpdateParity => _clientOwnedUpdateParityMode;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            _device = device;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;
            _clientOwnedScreenMaskCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);

            // Create 1x1 white pixel texture
            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            // Create radial gradient for soft-edged circles
            CreateGradientCircle(device, 256);
        }

        private void CreateGradientCircle(GraphicsDevice device, int size)
        {
            _gradientCircle = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];

            float center = size / 2f;
            float maxDist = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    // Normalized distance from center (0 = center, 1 = edge)
                    float t = Math.Min(dist / maxDist, 1f);

                    // Smooth step for soft edges (transparent in center, opaque at edge)
                    // Inner 70% is fully transparent, outer 30% fades to opaque
                    float alpha;
                    if (t < 0.7f)
                        alpha = 0f;
                    else
                        alpha = SmoothStep(0f, 1f, (t - 0.7f) / 0.3f);

                    data[y * size + x] = new Color(255, 255, 255, (int)(alpha * 255));
                }
            }

            _gradientCircle.SetData(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep(float edge0, float edge1, float x)
        {
            x = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return x * x * (3f - 2f * x);
        }
        #endregion

        #region Enable/Disable
        public void Enable(ViewMode mode, float radius = 300f)
        {
            _mode = mode;
            _enabled = true;
            _targetRadius = radius;
            _currentRadius = radius;
            _viewRadius = radius;
            _targetAlpha = 1f;
        }

        public void EnableCircle(float radius = 300f)
        {
            Enable(ViewMode.Circle, radius);
        }

        public void EnableSpotlight(float radius = 300f, bool pulse = true)
        {
            Enable(ViewMode.Spotlight, radius);
            _pulseEnabled = pulse;
        }

        public void EnableRectangle(float width = 400f, float height = 300f)
        {
            _mode = ViewMode.Rectangle;
            _enabled = true;
            _viewWidth = width;
            _viewHeight = height;
            _targetAlpha = 1f;
        }

        public void EnableFlashlight(float radius = 400f, float spreadDegrees = 45f)
        {
            Enable(ViewMode.Flashlight, radius);
            _flashlightSpread = spreadDegrees;
        }

        public void Disable()
        {
            _targetAlpha = 0f;
            // Will fully disable when alpha reaches 0
        }

        public void DisableImmediate()
        {
            _enabled = false;
            _mode = ViewMode.None;
            _currentAlpha = 0f;
            _clientOwnedImmediateMode = false;
        }
        #endregion

        #region Configuration
        public void SetViewRadius(float radius, bool immediate = false)
        {
            _targetRadius = radius;
            if (immediate)
                _currentRadius = radius;
        }

        public void SetFogColor(Color color)
        {
            _fogColor = color;
        }

        public void SetEdgeSoftness(float softness)
        {
            _edgeSoftness = Math.Max(0f, softness);
        }

        public void SetFollowPlayer(bool follow, float fixedX = 0, float fixedY = 0)
        {
            _followPlayer = follow;
            if (!follow)
            {
                _centerX = fixedX;
                _centerY = fixedY;
            }
        }

        public void SetFlashlightAngle(float radians)
        {
            _flashlightAngle = radians;
        }

        public void SetPulse(bool enabled, float amplitude = 20f, float frequency = 2f)
        {
            _pulseEnabled = enabled;
            _pulseAmplitude = amplitude;
            _pulseFrequency = frequency;
        }

        public void ConfigureClientOwnedMask(float width, float height, float originX, float originY, bool immediateMode = true)
        {
            _clientOwnedMaskWidth = Math.Max(1f, width);
            _clientOwnedMaskHeight = Math.Max(1f, height);
            _clientOwnedMaskOriginX = Math.Clamp(originX, 0f, _clientOwnedMaskWidth);
            _clientOwnedMaskOriginY = Math.Clamp(originY, 0f, _clientOwnedMaskHeight);
            _clientOwnedImmediateMode = immediateMode;
        }

        public void ConfigureClientOwnedDarkLayer(int width, int height, int offsetX, int offsetY)
        {
            _clientOwnedDarkLayerWidth = Math.Max(0, width);
            _clientOwnedDarkLayerHeight = Math.Max(0, height);
            _clientOwnedDarkLayerOffsetX = offsetX;
            _clientOwnedDarkLayerOffsetY = offsetY;
        }

        public void ConfigureClientOwnedSmallDarkPatch(int width, int height)
        {
            _clientOwnedSmallDarkPatchWidth = Math.Max(0, width);
            _clientOwnedSmallDarkPatchHeight = Math.Max(0, height);
        }

        public void EnableClientOwnedCircleMask(float radius, float width, float height, float originX, float originY, int smallDarkPatchWidth = 0, int smallDarkPatchHeight = 0)
        {
            ConfigureClientOwnedMask(width, height, originX, originY, immediateMode: true);
            ConfigureClientOwnedSmallDarkPatch(smallDarkPatchWidth, smallDarkPatchHeight);
            _clientOwnedUpdateParityMode = true;
            _pulseEnabled = false;
            _edgeSoftness = 0f;
            EnableCircle(radius);
        }

        public void SetClientOwnedShareView(bool shareView)
        {
            _clientOwnedShareView = shareView;
        }

        public void SetClientOwnedViewrangeTexture(SD.Bitmap bitmap)
        {
            _clientOwnedViewrangeTexture?.Dispose();
            _clientOwnedViewrangeTexture = bitmap.ToTexture2DAndDispose(_device);
        }

        public void SetClientOwnedFocusWorldPosition(float worldX, float worldY)
        {
            _clientOwnedFocusWorldPosition = new Vector2(worldX, worldY);
            _clientOwnedFocusWorldPositionValid = true;
        }

        public void ClearClientOwnedFocusWorldPosition()
        {
            _clientOwnedFocusWorldPositionValid = false;
            _clientOwnedRemoteFocusWorldPositions.Clear();
        }

        public void SetClientOwnedRemoteFocusWorldPositions(IEnumerable<Vector2> worldPositions)
        {
            _clientOwnedRemoteFocusWorldPositions.Clear();
            if (worldPositions == null)
            {
                return;
            }

            foreach (Vector2 worldPosition in worldPositions)
            {
                _clientOwnedRemoteFocusWorldPositions.Add(worldPosition);
            }
        }

        public void ClearClientOwnedMask()
        {
            _clientOwnedMaskWidth = 0f;
            _clientOwnedMaskHeight = 0f;
            _clientOwnedMaskOriginX = 0f;
            _clientOwnedMaskOriginY = 0f;
            _clientOwnedSmallDarkPatchWidth = 0;
            _clientOwnedSmallDarkPatchHeight = 0;
            _clientOwnedDarkLayerWidth = 0;
            _clientOwnedDarkLayerHeight = 0;
            _clientOwnedDarkLayerOffsetX = 0;
            _clientOwnedDarkLayerOffsetY = 0;
            _clientOwnedImmediateMode = false;
            _clientOwnedUpdateParityMode = false;
            _clientOwnedShareView = false;
            _clientOwnedFocusWorldPositionValid = false;
            _clientOwnedRemoteFocusWorldPositions.Clear();
            _clientOwnedScreenMaskCentersBuffer.Clear();
            _clientOwnedMaskTopLeftsBuffer.Clear();
            _clientOwnedPreviousMaskTopLefts.Clear();
        }
        #endregion

        #region Update
        public void Update(GameTime gameTime, float playerX, float playerY)
        {
            if (!_enabled && _currentAlpha <= 0f)
                return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update center position
            if (_followPlayer)
            {
                _centerX = playerX;
                _centerY = playerY;
            }

            if (_clientOwnedImmediateMode)
            {
                _clientOwnedScreenMaskCenter = GetScreenMaskCenter();
                _currentRadius = _targetRadius;
                _currentAlpha = _targetAlpha;

                if (_targetAlpha <= 0f)
                {
                    _enabled = false;
                    _mode = ViewMode.None;
                }

                if (_clientOwnedUpdateParityMode)
                {
                    return;
                }

                return;
            }

            // Transition radius
            if (Math.Abs(_currentRadius - _targetRadius) > 0.1f)
            {
                float diff = _targetRadius - _currentRadius;
                float maxChange = _radiusTransitionSpeed * deltaTime;
                _currentRadius += Math.Sign(diff) * Math.Min(Math.Abs(diff), maxChange);
            }

            // Transition alpha
            if (Math.Abs(_currentAlpha - _targetAlpha) > 0.01f)
            {
                float diff = _targetAlpha - _currentAlpha;
                float maxChange = _alphaTransitionSpeed * deltaTime;
                _currentAlpha += Math.Sign(diff) * Math.Min(Math.Abs(diff), maxChange);
            }
            else if (_targetAlpha <= 0f && _currentAlpha <= 0.01f)
            {
                _enabled = false;
                _mode = ViewMode.None;
            }

            // Update pulse
            if (_pulseEnabled)
            {
                _pulsePhase += deltaTime * _pulseFrequency * MathF.PI * 2f;
                if (_pulsePhase > MathF.PI * 2f)
                    _pulsePhase -= MathF.PI * 2f;
            }
        }
        #endregion

        #region Draw
        public void Draw(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (!_enabled || _currentAlpha <= 0f || _pixelTexture == null)
                return;

            // The fog of war is always centered on the SCREEN (where the player would be)
            // Not on map coordinates - the viewport center IS the player position
            Vector2 maskCenter = GetScreenMaskCenter();
            if (_clientOwnedUpdateParityMode)
            {
                maskCenter = GetClientOwnedUpdateParityScreenMaskCenter(mapShiftX, mapShiftY, centerX, centerY);
            }

            int screenCenterX = (int)MathF.Round(maskCenter.X);
            int screenCenterY = (int)MathF.Round(maskCenter.Y);

            // Apply pulse effect
            float effectiveRadius = _currentRadius;
            if (_pulseEnabled)
            {
                effectiveRadius += MathF.Sin(_pulsePhase) * _pulseAmplitude;
            }

            // Apply alpha to fog color
            Color fogColorWithAlpha = new Color(
                _fogColor.R, _fogColor.G, _fogColor.B,
                (int)(_fogColor.A * _currentAlpha));

            switch (_mode)
            {
                case ViewMode.Circle:
                case ViewMode.Spotlight:
                    if (_clientOwnedUpdateParityMode && _clientOwnedViewrangeTexture != null)
                    {
                        Vector2? localMaskTopLeft = TryGetClientOwnedUpdateParityLocalMaskTopLeft(mapShiftX, mapShiftY, centerX, centerY, out Vector2 resolvedLocalMaskTopLeft)
                            ? resolvedLocalMaskTopLeft
                            : null;
                        IReadOnlyList<Vector2> remoteMaskTopLefts = GetClientOwnedUpdateParityRemoteMaskTopLefts(mapShiftX, mapShiftY, centerX, centerY);
                        int viewrangeWidth = Math.Max(1, _clientOwnedViewrangeTexture.Width);
                        int viewrangeHeight = Math.Max(1, _clientOwnedViewrangeTexture.Height);
                        ExecuteClientOwnedDrawViewrangeOperationPlan(
                            spriteBatch,
                            fogColorWithAlpha,
                            BuildClientOwnedDrawViewrangeOperationPlan(
                                _clientOwnedPreviousMaskTopLefts.ToArray(),
                                localMaskTopLeft,
                                remoteMaskTopLefts,
                                _clientOwnedShareView,
                                viewrangeWidth,
                                viewrangeHeight,
                                GetClientOwnedDarkLayerBounds()));
                        break;
                    }

                    if (_clientOwnedUpdateParityMode)
                    {
                        IReadOnlyList<Vector2> maskCenters = GetClientOwnedUpdateParityScreenMaskCenters(mapShiftX, mapShiftY, centerX, centerY);
                        for (int i = 0; i < maskCenters.Count; i++)
                        {
                            Vector2 clientMaskCenter = maskCenters[i];
                            DrawCircularFog(
                                spriteBatch,
                                (int)MathF.Round(clientMaskCenter.X),
                                (int)MathF.Round(clientMaskCenter.Y),
                                effectiveRadius,
                                fogColorWithAlpha,
                                drawClientOwnedDarkLayer: i == 0);
                        }

                        break;
                    }

                    DrawCircularFog(spriteBatch, screenCenterX, screenCenterY, effectiveRadius, fogColorWithAlpha);
                    break;

                case ViewMode.Rectangle:
                    DrawRectangularFog(spriteBatch, screenCenterX, screenCenterY, fogColorWithAlpha);
                    break;

                case ViewMode.Flashlight:
                    DrawFlashlightFog(spriteBatch, screenCenterX, screenCenterY, effectiveRadius, fogColorWithAlpha);
                    break;
            }
        }

        private void DrawCircularFog(
            SpriteBatch spriteBatch,
            int centerX,
            int centerY,
            float radius,
            Color fogColor,
            bool drawClientOwnedDarkLayer = true)
        {
            if (_clientOwnedUpdateParityMode && _clientOwnedViewrangeTexture != null)
            {
                DrawClientOwnedViewrangeFog(spriteBatch, centerX, centerY, fogColor, drawClientOwnedDarkLayer);
                return;
            }

            if (_gradientCircle == null)
                return;

            // The gradient circle has: transparent center (alpha=0), opaque edge (alpha=255)
            // We draw it with fogColor to create the soft-edge fog ring
            float softRadius = radius + _edgeSoftness;
            int gradientSize = (int)(softRadius * 2);

            int gradientLeft = centerX - (int)softRadius;
            int gradientTop = centerY - (int)softRadius;
            int gradientRight = centerX + (int)softRadius;
            int gradientBottom = centerY + (int)softRadius;

            // Draw the gradient circle (creates the soft-edge circular "hole")
            spriteBatch.Draw(_gradientCircle,
                new Rectangle(gradientLeft, gradientTop, gradientSize, gradientSize),
                fogColor);

            // Draw 4 corner rectangles to fill areas OUTSIDE the gradient's bounding box
            // Top strip (full width, above gradient)
            if (gradientTop > 0)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(0, 0, _screenWidth, gradientTop),
                    fogColor);
            }

            // Bottom strip (full width, below gradient)
            if (gradientBottom < _screenHeight)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(0, gradientBottom, _screenWidth, _screenHeight - gradientBottom),
                    fogColor);
            }

            // Left strip (between top and bottom strips)
            if (gradientLeft > 0)
            {
                int stripTop = Math.Max(0, gradientTop);
                int stripBottom = Math.Min(_screenHeight, gradientBottom);
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(0, stripTop, gradientLeft, stripBottom - stripTop),
                    fogColor);
            }

            // Right strip (between top and bottom strips)
            if (gradientRight < _screenWidth)
            {
                int stripTop = Math.Max(0, gradientTop);
                int stripBottom = Math.Min(_screenHeight, gradientBottom);
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(gradientRight, stripTop, _screenWidth - gradientRight, stripBottom - stripTop),
                    fogColor);
            }
        }

        private void DrawClientOwnedViewrangeFog(
            SpriteBatch spriteBatch,
            int centerX,
            int centerY,
            Color fogColor,
            bool drawDarkLayer)
        {
            int width = _clientOwnedSmallDarkPatchWidth > 0
                ? _clientOwnedSmallDarkPatchWidth
                : _clientOwnedViewrangeTexture.Width;
            int height = _clientOwnedSmallDarkPatchHeight > 0
                ? _clientOwnedSmallDarkPatchHeight
                : _clientOwnedViewrangeTexture.Height;
            int left = centerX - (width / 2);
            int top = centerY - (height / 2);
            int right = left + width;
            int bottom = top + height;

            DrawClientOwnedViewrangeFogAtTopLeft(
                spriteBatch,
                left,
                top,
                fogColor,
                drawDarkLayer,
                restorePreviousClientOwnedViewranges: false,
                resetPreviousClientOwnedViewrangesForCurrentFrame: false,
                sourceX: 0,
                sourceY: 0,
                sourceWidth: width,
                sourceHeight: height,
                usesRemoveAlphaCopy: true);
        }

        private bool DrawClientOwnedViewrangeFogAtTopLeft(
            SpriteBatch spriteBatch,
            int left,
            int top,
            Color fogColor,
            bool drawClientOwnedDarkLayer,
            bool restorePreviousClientOwnedViewranges,
            bool resetPreviousClientOwnedViewrangesForCurrentFrame,
            int sourceX,
            int sourceY,
            int sourceWidth,
            int sourceHeight,
            bool usesRemoveAlphaCopy)
        {
            int width = _clientOwnedViewrangeTexture.Width;
            int height = _clientOwnedViewrangeTexture.Height;

            if (restorePreviousClientOwnedViewranges)
            {
                RestorePreviousClientOwnedViewrangePatches(spriteBatch, fogColor);
            }

            if (resetPreviousClientOwnedViewrangesForCurrentFrame)
            {
                ResetClientOwnedPreviousMaskTopLeftsForCurrentFrame();
            }

            if (!TryResolveClientOwnedViewrangeCopyRectangles(
                left,
                top,
                width,
                height,
                sourceX,
                sourceY,
                sourceWidth,
                sourceHeight,
                GetClientOwnedDarkLayerBounds(),
                out Rectangle destinationRect,
                out Rectangle sourceRect))
            {
                return false;
            }

            // CField_LimitedView::DrawViewrange restores prior m_lpPrev entries
            // with the small-dark canvas, clears m_lpPrev, then copies
            // Viewrange/0 over the current user positions while appending each
            // current top-left back into m_lpPrev.
            if (drawClientOwnedDarkLayer)
            {
                DrawClientOwnedDarkLayerAroundCurrentViewrange(
                    spriteBatch,
                    destinationRect.Left,
                    destinationRect.Top,
                    destinationRect.Right,
                    destinationRect.Bottom,
                    fogColor);
            }

            Color tint = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, fogColor.A);
            if (usesRemoveAlphaCopy)
            {
                // SpriteBatch alpha blending approximates the COM remove-alpha copy
                // used by CField_LimitedView for the viewrange mask blit.
                spriteBatch.Draw(_clientOwnedViewrangeTexture, destinationRect, sourceRect, tint);
                return true;
            }

            spriteBatch.Draw(_clientOwnedViewrangeTexture, destinationRect, sourceRect, tint);
            return true;
        }

        private void DrawClientOwnedDarkLayerAroundCurrentViewrange(SpriteBatch spriteBatch, int left, int top, int right, int bottom, Color fogColor)
        {
            Rectangle darkBounds = GetClientOwnedDarkLayerBounds();
            int darkLeft = darkBounds.Left;
            int darkTop = darkBounds.Top;
            int darkRight = darkBounds.Right;
            int darkBottom = darkBounds.Bottom;

            if (darkTop < top)
            {
                spriteBatch.Draw(_pixelTexture, new Rectangle(darkLeft, darkTop, darkBounds.Width, top - darkTop), fogColor);
            }

            if (bottom < darkBottom)
            {
                spriteBatch.Draw(_pixelTexture, new Rectangle(darkLeft, bottom, darkBounds.Width, darkBottom - bottom), fogColor);
            }

            if (darkLeft < left)
            {
                int stripTop = Math.Max(darkTop, top);
                int stripBottom = Math.Min(darkBottom, bottom);
                if (stripBottom > stripTop)
                {
                    spriteBatch.Draw(_pixelTexture, new Rectangle(darkLeft, stripTop, left - darkLeft, stripBottom - stripTop), fogColor);
                }
            }

            if (right < darkRight)
            {
                int stripTop = Math.Max(darkTop, top);
                int stripBottom = Math.Min(darkBottom, bottom);
                if (stripBottom > stripTop)
                {
                    spriteBatch.Draw(_pixelTexture, new Rectangle(right, stripTop, darkRight - right, stripBottom - stripTop), fogColor);
                }
            }
        }

        private void RestorePreviousClientOwnedViewrangePatches(SpriteBatch spriteBatch, Color fogColor)
        {
            if (_clientOwnedPreviousMaskTopLefts.Count == 0)
            {
                return;
            }

            int width = _clientOwnedSmallDarkPatchWidth > 0
                ? _clientOwnedSmallDarkPatchWidth
                : _clientOwnedViewrangeTexture.Width;
            int height = _clientOwnedSmallDarkPatchHeight > 0
                ? _clientOwnedSmallDarkPatchHeight
                : _clientOwnedViewrangeTexture.Height;
            Color smallDarkColor = new((byte)0, (byte)0, (byte)0, fogColor.A);
            for (int i = 0; i < _clientOwnedPreviousMaskTopLefts.Count; i++)
            {
                Vector2 topLeft = _clientOwnedPreviousMaskTopLefts[i];
                if (!TryResolveClientOwnedSmallDarkPatchRectangle(
                    (int)MathF.Round(topLeft.X),
                    (int)MathF.Round(topLeft.Y),
                    width,
                    height,
                    GetClientOwnedDarkLayerBounds(),
                    out Rectangle destinationRect))
                {
                    continue;
                }

                spriteBatch.Draw(_pixelTexture, destinationRect, smallDarkColor);
            }
        }

        private void DrawRectangularFog(SpriteBatch spriteBatch, int centerX, int centerY, Color fogColor)
        {
            int halfWidth = (int)(_viewWidth / 2);
            int halfHeight = (int)(_viewHeight / 2);

            // Calculate the clear rectangle bounds
            int clearLeft = centerX - halfWidth;
            int clearTop = centerY - halfHeight;
            int clearRight = centerX + halfWidth;
            int clearBottom = centerY + halfHeight;

            // Draw fog in 4 regions around the clear area
            // Top
            if (clearTop > 0)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(0, 0, _screenWidth, clearTop),
                    fogColor);
            }

            // Bottom
            if (clearBottom < _screenHeight)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(0, clearBottom, _screenWidth, _screenHeight - clearBottom),
                    fogColor);
            }

            // Left (between top and bottom)
            if (clearLeft > 0)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(0, Math.Max(0, clearTop), clearLeft, Math.Min(clearBottom, _screenHeight) - Math.Max(0, clearTop)),
                    fogColor);
            }

            // Right (between top and bottom)
            if (clearRight < _screenWidth)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(clearRight, Math.Max(0, clearTop), _screenWidth - clearRight, Math.Min(clearBottom, _screenHeight) - Math.Max(0, clearTop)),
                    fogColor);
            }

            // Draw soft edges if enabled
            if (_edgeSoftness > 0)
            {
                DrawRectangularSoftEdges(spriteBatch, clearLeft, clearTop, clearRight, clearBottom, fogColor);
            }
        }

        private void DrawRectangularSoftEdges(SpriteBatch spriteBatch, int left, int top, int right, int bottom, Color fogColor)
        {
            int edgeSize = (int)_edgeSoftness;

            // Create gradient effect using multiple semi-transparent rectangles
            for (int i = 0; i < edgeSize; i++)
            {
                float t = (float)i / edgeSize;
                int alpha = (int)(fogColor.A * t * t); // Quadratic falloff
                Color edgeColor = new Color(fogColor.R, fogColor.G, fogColor.B, alpha);

                // Inner edge rectangles
                // Left edge
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(left + i, Math.Max(0, top), 1, Math.Min(bottom, _screenHeight) - Math.Max(0, top)),
                    edgeColor);
                // Right edge
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(right - i - 1, Math.Max(0, top), 1, Math.Min(bottom, _screenHeight) - Math.Max(0, top)),
                    edgeColor);
                // Top edge
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(Math.Max(0, left), top + i, Math.Min(right, _screenWidth) - Math.Max(0, left), 1),
                    edgeColor);
                // Bottom edge
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(Math.Max(0, left), bottom - i - 1, Math.Min(right, _screenWidth) - Math.Max(0, left), 1),
                    edgeColor);
            }
        }

        private void DrawFlashlightFog(SpriteBatch spriteBatch, int centerX, int centerY, float radius, Color fogColor)
        {
            // Flashlight mode is complex (would need shaders for proper cone effect)
            // For now, just use the same approach as circle mode
            DrawCircularFog(spriteBatch, centerX, centerY, radius, fogColor);
        }

        private Vector2 GetScreenMaskCenter()
        {
            float screenCenterX = _screenWidth * 0.5f;
            float screenCenterY = _screenHeight * 0.5f;
            if (_clientOwnedMaskWidth <= 0f || _clientOwnedMaskHeight <= 0f)
            {
                return new Vector2(screenCenterX, screenCenterY);
            }

            float offsetX = (_clientOwnedMaskWidth * 0.5f) - _clientOwnedMaskOriginX;
            float offsetY = (_clientOwnedMaskHeight * 0.5f) - _clientOwnedMaskOriginY;
            return new Vector2(screenCenterX + offsetX, screenCenterY + offsetY);
        }

        internal Vector2 GetClientOwnedUpdateParityScreenMaskCenter(int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (!_clientOwnedFocusWorldPositionValid)
            {
                return _clientOwnedScreenMaskCenter;
            }

            Vector2 topLeft = ResolveClientOwnedMaskTopLeft(
                _clientOwnedFocusWorldPosition.X,
                _clientOwnedFocusWorldPosition.Y,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                _clientOwnedMaskOriginX,
                _clientOwnedMaskOriginY);
            return new Vector2(
                topLeft.X + (_clientOwnedMaskWidth * 0.5f),
                topLeft.Y + (_clientOwnedMaskHeight * 0.5f));
        }

        internal IReadOnlyList<Vector2> GetClientOwnedUpdateParityScreenMaskCenters(int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            _clientOwnedScreenMaskCentersBuffer.Clear();
            if (!_clientOwnedFocusWorldPositionValid)
            {
                return _clientOwnedScreenMaskCentersBuffer;
            }

            _clientOwnedScreenMaskCentersBuffer.Add(GetClientOwnedUpdateParityScreenMaskCenter(mapShiftX, mapShiftY, centerX, centerY));

            if (_clientOwnedShareView)
            {
                foreach (Vector2 worldPosition in _clientOwnedRemoteFocusWorldPositions)
                {
                    _clientOwnedScreenMaskCentersBuffer.Add(GetClientOwnedUpdateParityScreenPosition(worldPosition, mapShiftX, mapShiftY, centerX, centerY));
                }
            }

            return _clientOwnedScreenMaskCentersBuffer;
        }

        internal IReadOnlyList<Vector2> GetClientOwnedUpdateParityMaskTopLefts(int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            _clientOwnedMaskTopLeftsBuffer.Clear();
            if (!TryGetClientOwnedUpdateParityLocalMaskTopLeft(mapShiftX, mapShiftY, centerX, centerY, out Vector2 localMaskTopLeft))
            {
                return _clientOwnedMaskTopLeftsBuffer;
            }

            _clientOwnedMaskTopLeftsBuffer.Add(localMaskTopLeft);

            if (_clientOwnedShareView)
            {
                _clientOwnedMaskTopLeftsBuffer.AddRange(GetClientOwnedUpdateParityRemoteMaskTopLefts(mapShiftX, mapShiftY, centerX, centerY));
            }

            return _clientOwnedMaskTopLeftsBuffer;
        }

        internal bool TryGetClientOwnedUpdateParityLocalMaskTopLeft(int mapShiftX, int mapShiftY, int centerX, int centerY, out Vector2 maskTopLeft)
        {
            if (!_clientOwnedFocusWorldPositionValid)
            {
                maskTopLeft = Vector2.Zero;
                return false;
            }

            maskTopLeft = ResolveClientOwnedMaskTopLeft(
                _clientOwnedFocusWorldPosition.X,
                _clientOwnedFocusWorldPosition.Y,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                _clientOwnedMaskOriginX,
                _clientOwnedMaskOriginY);
            return true;
        }

        internal IReadOnlyList<Vector2> GetClientOwnedUpdateParityRemoteMaskTopLefts(int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            _clientOwnedMaskTopLeftsBuffer.Clear();
            foreach (Vector2 worldPosition in _clientOwnedRemoteFocusWorldPositions)
            {
                _clientOwnedMaskTopLeftsBuffer.Add(ResolveClientOwnedMaskTopLeft(
                    worldPosition.X,
                    worldPosition.Y,
                    mapShiftX,
                    mapShiftY,
                    centerX,
                    centerY,
                    _clientOwnedMaskOriginX,
                    _clientOwnedMaskOriginY));
            }

            return _clientOwnedMaskTopLeftsBuffer;
        }

        internal static IReadOnlyList<ClientOwnedDrawViewrangeOperation> BuildClientOwnedDrawViewrangeOperationPlan(
            IReadOnlyList<Vector2> previousMaskTopLefts,
            IReadOnlyList<Vector2> currentMaskTopLefts)
        {
            return BuildClientOwnedDrawViewrangeOperationPlan(
                previousMaskTopLefts,
                currentMaskTopLefts,
                ClientOwnedSmallDarkCanvasWidth,
                ClientOwnedSmallDarkCanvasHeight);
        }

        internal static IReadOnlyList<ClientOwnedDrawViewrangeOperation> BuildClientOwnedDrawViewrangeOperationPlan(
            IReadOnlyList<Vector2> previousMaskTopLefts,
            Vector2? localMaskTopLeft,
            IReadOnlyList<Vector2> remoteMaskTopLefts,
            bool shareView,
            int viewrangeWidth,
            int viewrangeHeight)
        {
            return BuildClientOwnedDrawViewrangeOperationPlan(
                previousMaskTopLefts,
                localMaskTopLeft,
                remoteMaskTopLefts,
                shareView,
                viewrangeWidth,
                viewrangeHeight,
                darkLayerBounds: null);
        }

        internal static IReadOnlyList<ClientOwnedDrawViewrangeOperation> BuildClientOwnedDrawViewrangeOperationPlan(
            IReadOnlyList<Vector2> previousMaskTopLefts,
            Vector2? localMaskTopLeft,
            IReadOnlyList<Vector2> remoteMaskTopLefts,
            bool shareView,
            int viewrangeWidth,
            int viewrangeHeight,
            Rectangle? darkLayerBounds)
        {
            List<Vector2> currentMaskTopLefts = new();
            if (localMaskTopLeft.HasValue)
            {
                currentMaskTopLefts.Add(localMaskTopLeft.Value);
            }

            if (shareView && localMaskTopLeft.HasValue && remoteMaskTopLefts != null)
            {
                for (int i = 0; i < remoteMaskTopLefts.Count; i++)
                {
                    currentMaskTopLefts.Add(remoteMaskTopLefts[i]);
                }
            }

            List<ClientOwnedDrawViewrangeOperation> operations = new(BuildClientOwnedDrawViewrangeOperationPlan(
                previousMaskTopLefts,
                currentMaskTopLefts,
                viewrangeWidth,
                viewrangeHeight,
                darkLayerBounds));

            if (!shareView && localMaskTopLeft.HasValue && remoteMaskTopLefts != null && remoteMaskTopLefts.Count > 0)
            {
                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.EvaluateShareViewRemoteLoop,
                    Vector2.Zero,
                    -1));
                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.SkipRemoteViewrangeBecauseShareViewDisabled,
                    Vector2.Zero,
                    -1));
            }
            else if (shareView && !localMaskTopLeft.HasValue && remoteMaskTopLefts != null && remoteMaskTopLefts.Count > 0)
            {
                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.SkipRemoteViewrangeBecauseLocalUserMissing,
                    Vector2.Zero,
                    -1));
            }

            return operations;
        }

        internal static IReadOnlyList<ClientOwnedDrawViewrangeOperation> BuildClientOwnedDrawViewrangeOperationPlan(
            IReadOnlyList<Vector2> previousMaskTopLefts,
            IReadOnlyList<Vector2> currentMaskTopLefts,
            int viewrangeWidth,
            int viewrangeHeight)
        {
            return BuildClientOwnedDrawViewrangeOperationPlan(
                previousMaskTopLefts,
                currentMaskTopLefts,
                viewrangeWidth,
                viewrangeHeight,
                darkLayerBounds: null);
        }

        internal static IReadOnlyList<ClientOwnedDrawViewrangeOperation> BuildClientOwnedDrawViewrangeOperationPlan(
            IReadOnlyList<Vector2> previousMaskTopLefts,
            IReadOnlyList<Vector2> currentMaskTopLefts,
            int viewrangeWidth,
            int viewrangeHeight,
            Rectangle? darkLayerBounds)
        {
            List<ClientOwnedDrawViewrangeOperation> operations = new();
            int sourceWidth = Math.Max(0, viewrangeWidth);
            int sourceHeight = Math.Max(0, viewrangeHeight);

            operations.Add(new ClientOwnedDrawViewrangeOperation(
                ClientOwnedDrawViewrangeOperationKind.AcquireDarkLayerCanvas,
                Vector2.Zero,
                -1,
                sourceWidth: sourceWidth,
                sourceHeight: sourceHeight));

            if (previousMaskTopLefts != null)
            {
                if (previousMaskTopLefts.Count > 0)
                {
                    operations.Add(new ClientOwnedDrawViewrangeOperation(
                        ClientOwnedDrawViewrangeOperationKind.AcquireSmallDarkCanvas,
                        Vector2.Zero,
                        -1,
                        sourceWidth: sourceWidth,
                        sourceHeight: sourceHeight));
                }

                for (int i = 0; i < previousMaskTopLefts.Count; i++)
                {
                    operations.Add(new ClientOwnedDrawViewrangeOperation(
                        ClientOwnedDrawViewrangeOperationKind.ResolvePreviousSmallDarkPatchRectangle,
                        NormalizeClientOwnedMaskTopLeft(previousMaskTopLefts[i]),
                        i,
                        sourceWidth: sourceWidth,
                        sourceHeight: sourceHeight));
                    operations.Add(new ClientOwnedDrawViewrangeOperation(
                        ClientOwnedDrawViewrangeOperationKind.RestorePreviousSmallDarkPatch,
                        NormalizeClientOwnedMaskTopLeft(previousMaskTopLefts[i]),
                        i));
                }
            }

            operations.Add(new ClientOwnedDrawViewrangeOperation(
                ClientOwnedDrawViewrangeOperationKind.ClearPreviousMaskHistory,
                Vector2.Zero,
                -1));

            if (currentMaskTopLefts == null || currentMaskTopLefts.Count == 0)
            {
                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.DrawDarkLayerFallback,
                    Vector2.Zero,
                    -1));
                return operations;
            }

            operations.Add(new ClientOwnedDrawViewrangeOperation(
                ClientOwnedDrawViewrangeOperationKind.ResolveLocalUserPosition,
                NormalizeClientOwnedMaskTopLeft(currentMaskTopLefts[0]),
                0));
            operations.Add(new ClientOwnedDrawViewrangeOperation(
                ClientOwnedDrawViewrangeOperationKind.ResolveGraphicsCenter,
                Vector2.Zero,
                -1));
            operations.Add(new ClientOwnedDrawViewrangeOperation(
                ClientOwnedDrawViewrangeOperationKind.QueryViewrangeCanvasDimensions,
                Vector2.Zero,
                -1,
                sourceWidth: sourceWidth,
                sourceHeight: sourceHeight));
            operations.Add(new ClientOwnedDrawViewrangeOperation(
                ClientOwnedDrawViewrangeOperationKind.AcquireViewrangeCanvas,
                Vector2.Zero,
                -1,
                sourceWidth: sourceWidth,
                sourceHeight: sourceHeight));

            if (darkLayerBounds.HasValue)
            {
                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.ResolveDarkLayerBounds,
                    Vector2.Zero,
                    -1,
                    destinationX: darkLayerBounds.Value.X,
                    destinationY: darkLayerBounds.Value.Y,
                    destinationWidth: darkLayerBounds.Value.Width,
                    destinationHeight: darkLayerBounds.Value.Height));
            }

            for (int i = 0; i < currentMaskTopLefts.Count; i++)
            {
                Vector2 normalizedTopLeft = NormalizeClientOwnedMaskTopLeft(currentMaskTopLefts[i]);
                ClientOwnedDrawViewrangeOperationKind copyKind = i == 0
                    ? ClientOwnedDrawViewrangeOperationKind.CopyLocalViewrange
                    : ClientOwnedDrawViewrangeOperationKind.CopyRemoteViewrange;
                if (i > 0)
                {
                    operations.Add(new ClientOwnedDrawViewrangeOperation(
                        ClientOwnedDrawViewrangeOperationKind.ResolveRemoteUserPosition,
                        normalizedTopLeft,
                        i));
                }

                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.ResolveViewrangeCopyRectangles,
                    normalizedTopLeft,
                    i,
                    sourceWidth: sourceWidth,
                    sourceHeight: sourceHeight,
                    usesRemoveAlphaCopy: true));
                if (darkLayerBounds.HasValue)
                {
                    bool hasClippedCopy = TryResolveClientOwnedViewrangeCopyRectangles(
                        (int)MathF.Round(normalizedTopLeft.X),
                        (int)MathF.Round(normalizedTopLeft.Y),
                        sourceWidth,
                        sourceHeight,
                        sourceX: 0,
                        sourceY: 0,
                        sourceWidth,
                        sourceHeight,
                        darkLayerBounds.Value,
                        out Rectangle destinationRect,
                        out Rectangle sourceRect);
                    operations.Add(new ClientOwnedDrawViewrangeOperation(
                        hasClippedCopy
                            ? ClientOwnedDrawViewrangeOperationKind.ClipViewrangeCopyRectangles
                            : ClientOwnedDrawViewrangeOperationKind.SkipViewrangeCopyOutsideDarkLayer,
                        normalizedTopLeft,
                        i,
                        sourceX: sourceRect.X,
                        sourceY: sourceRect.Y,
                        sourceWidth: sourceRect.Width,
                        sourceHeight: sourceRect.Height,
                        destinationX: destinationRect.X,
                        destinationY: destinationRect.Y,
                        destinationWidth: destinationRect.Width,
                        destinationHeight: destinationRect.Height,
                        usesRemoveAlphaCopy: true));

                    if (!hasClippedCopy)
                    {
                        continue;
                    }
                }

                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.PrepareRemoveAlphaViewrangeCopy,
                    normalizedTopLeft,
                    i,
                    sourceWidth: sourceWidth,
                    sourceHeight: sourceHeight,
                    usesRemoveAlphaCopy: true));
                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    copyKind,
                    normalizedTopLeft,
                    i,
                    sourceWidth: sourceWidth,
                    sourceHeight: sourceHeight,
                    usesRemoveAlphaCopy: true));
                operations.Add(new ClientOwnedDrawViewrangeOperation(
                    ClientOwnedDrawViewrangeOperationKind.AppendPreviousMaskHistory,
                    normalizedTopLeft,
                    i));

                if (i == 0 && currentMaskTopLefts.Count > 1)
                {
                    operations.Add(new ClientOwnedDrawViewrangeOperation(
                        ClientOwnedDrawViewrangeOperationKind.EvaluateShareViewRemoteLoop,
                        Vector2.Zero,
                        -1));
                }
            }

            return operations;
        }

        internal static Vector2 NormalizeClientOwnedMaskTopLeft(Vector2 topLeft)
        {
            return new Vector2(
                MathF.Round(topLeft.X),
                MathF.Round(topLeft.Y));
        }

        internal static IReadOnlyList<ClientOwnedInitOperation> BuildClientOwnedInitOperationPlan(
            int viewrangeWidth,
            int viewrangeHeight,
            string viewrangeSource)
        {
            int smallDarkWidth = Math.Max(1, viewrangeWidth);
            int smallDarkHeight = Math.Max(1, viewrangeHeight);
            string source = string.IsNullOrWhiteSpace(viewrangeSource)
                ? $"StringPool[0x{ClientOwnedViewrangePathStringPoolId:X}]"
                : viewrangeSource.Trim();

            return new[]
            {
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.ReleaseExistingDarkCanvas),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.ClearPreviousMaskHistory),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.CreateDarkCanvas, ClientOwnedDarkCanvasWidth, ClientOwnedDarkCanvasHeight, source: $"StringPool[0x{ClientOwnedCanvasClassStringPoolId:X}]"),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.FillDarkCanvasBlack, ClientOwnedDarkCanvasWidth, ClientOwnedDarkCanvasHeight),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.CreateDarkLayer),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.InsertDarkCanvasIntoLayer, ClientOwnedDarkCanvasWidth, ClientOwnedDarkCanvasHeight),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.BindDarkLayerToGraphicsCenter),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.MoveDarkLayer, x: ClientOwnedDarkLayerOffsetX, y: ClientOwnedDarkLayerOffsetY),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.SetDarkLayerColor, source: $"0x{unchecked((uint)ClientOwnedDarkLayerColorArgb):X8}"),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.SetDarkLayerZ, source: "-1"),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.CreateSmallDarkCanvas, smallDarkWidth, smallDarkHeight, source: $"StringPool[0x{ClientOwnedCanvasClassStringPoolId:X}]"),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.FillSmallDarkCanvasBlack, smallDarkWidth, smallDarkHeight),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.LoadViewrangeCanvas, smallDarkWidth, smallDarkHeight, source: source),
                new ClientOwnedInitOperation(ClientOwnedInitOperationKind.DrawInitialViewrange, smallDarkWidth, smallDarkHeight)
            };
        }

        internal static Color ResolveClientOwnedDarkLayerColor()
        {
            uint argb = unchecked((uint)ClientOwnedDarkLayerColorArgb);
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);
            return new Color(r, g, b, a);
        }

        internal void CommitClientOwnedUpdateParityMaskTopLefts(IReadOnlyList<Vector2> maskTopLefts)
        {
            _clientOwnedPreviousMaskTopLefts.Clear();
            if (maskTopLefts == null)
            {
                return;
            }

            foreach (Vector2 topLeft in maskTopLefts)
            {
                _clientOwnedPreviousMaskTopLefts.Add(NormalizeClientOwnedMaskTopLeft(topLeft));
            }
        }

        internal void ResetClientOwnedPreviousMaskTopLeftsForCurrentFrame()
        {
            _clientOwnedPreviousMaskTopLefts.Clear();
        }

        internal void TrackClientOwnedCurrentMaskTopLeft(Vector2 topLeft)
        {
            _clientOwnedPreviousMaskTopLefts.Add(NormalizeClientOwnedMaskTopLeft(topLeft));
        }

        internal static bool TryResolveClientOwnedViewrangeCopyRectangles(
            int left,
            int top,
            int textureWidth,
            int textureHeight,
            int sourceX,
            int sourceY,
            int sourceWidth,
            int sourceHeight,
            Rectangle darkLayerBounds,
            out Rectangle destinationRect,
            out Rectangle sourceRect)
        {
            destinationRect = Rectangle.Empty;
            sourceRect = Rectangle.Empty;

            if (textureWidth <= 0 || textureHeight <= 0 || darkLayerBounds.Width <= 0 || darkLayerBounds.Height <= 0)
            {
                return false;
            }

            int normalizedSourceX = Math.Clamp(sourceX, 0, textureWidth);
            int normalizedSourceY = Math.Clamp(sourceY, 0, textureHeight);
            int normalizedSourceWidth = sourceWidth > 0 ? sourceWidth : textureWidth - normalizedSourceX;
            int normalizedSourceHeight = sourceHeight > 0 ? sourceHeight : textureHeight - normalizedSourceY;
            normalizedSourceWidth = Math.Clamp(normalizedSourceWidth, 0, textureWidth - normalizedSourceX);
            normalizedSourceHeight = Math.Clamp(normalizedSourceHeight, 0, textureHeight - normalizedSourceY);
            if (normalizedSourceWidth <= 0 || normalizedSourceHeight <= 0)
            {
                return false;
            }

            sourceRect = new Rectangle(normalizedSourceX, normalizedSourceY, normalizedSourceWidth, normalizedSourceHeight);

            Rectangle unclippedDestination = new(
                left + normalizedSourceX,
                top + normalizedSourceY,
                normalizedSourceWidth,
                normalizedSourceHeight);

            if (!TryIntersectRectangles(unclippedDestination, darkLayerBounds, out destinationRect))
            {
                return false;
            }

            int clippedOffsetX = destinationRect.Left - unclippedDestination.Left;
            int clippedOffsetY = destinationRect.Top - unclippedDestination.Top;
            sourceRect = new Rectangle(
                sourceRect.X + clippedOffsetX,
                sourceRect.Y + clippedOffsetY,
                destinationRect.Width,
                destinationRect.Height);
            return sourceRect.Width > 0 && sourceRect.Height > 0;
        }

        internal static bool TryResolveClientOwnedSmallDarkPatchRectangle(
            int left,
            int top,
            int width,
            int height,
            Rectangle darkLayerBounds,
            out Rectangle destinationRect)
        {
            destinationRect = Rectangle.Empty;

            if (width <= 0 || height <= 0 || darkLayerBounds.Width <= 0 || darkLayerBounds.Height <= 0)
            {
                return false;
            }

            Rectangle unclippedDestination = new(left, top, width, height);
            return TryIntersectRectangles(unclippedDestination, darkLayerBounds, out destinationRect);
        }

        private static bool TryIntersectRectangles(Rectangle a, Rectangle b, out Rectangle intersection)
        {
            int left = Math.Max(a.Left, b.Left);
            int top = Math.Max(a.Top, b.Top);
            int right = Math.Min(a.Right, b.Right);
            int bottom = Math.Min(a.Bottom, b.Bottom);
            if (right <= left || bottom <= top)
            {
                intersection = Rectangle.Empty;
                return false;
            }

            intersection = new Rectangle(left, top, right - left, bottom - top);
            return true;
        }

        private void ExecuteClientOwnedDrawViewrangeOperationPlan(
            SpriteBatch spriteBatch,
            Color fogColor,
            IReadOnlyList<ClientOwnedDrawViewrangeOperation> operations)
        {
            if (operations == null)
            {
                return;
            }

            int successfulCopyCount = 0;
            HashSet<int> successfulCopyMaskIndices = new();
            foreach (ClientOwnedDrawViewrangeOperation operation in operations)
            {
                switch (operation.Kind)
                {
                    case ClientOwnedDrawViewrangeOperationKind.AcquireDarkLayerCanvas:
                    case ClientOwnedDrawViewrangeOperationKind.AcquireSmallDarkCanvas:
                    case ClientOwnedDrawViewrangeOperationKind.AcquireViewrangeCanvas:
                    case ClientOwnedDrawViewrangeOperationKind.ResolvePreviousSmallDarkPatchRectangle:
                    case ClientOwnedDrawViewrangeOperationKind.ResolveLocalUserPosition:
                    case ClientOwnedDrawViewrangeOperationKind.ResolveGraphicsCenter:
                    case ClientOwnedDrawViewrangeOperationKind.QueryViewrangeCanvasDimensions:
                    case ClientOwnedDrawViewrangeOperationKind.ResolveViewrangeCopyRectangles:
                    case ClientOwnedDrawViewrangeOperationKind.ResolveDarkLayerBounds:
                    case ClientOwnedDrawViewrangeOperationKind.ClipViewrangeCopyRectangles:
                    case ClientOwnedDrawViewrangeOperationKind.SkipViewrangeCopyOutsideDarkLayer:
                    case ClientOwnedDrawViewrangeOperationKind.PrepareRemoveAlphaViewrangeCopy:
                    case ClientOwnedDrawViewrangeOperationKind.EvaluateShareViewRemoteLoop:
                    case ClientOwnedDrawViewrangeOperationKind.SkipRemoteViewrangeBecauseShareViewDisabled:
                    case ClientOwnedDrawViewrangeOperationKind.SkipRemoteViewrangeBecauseLocalUserMissing:
                    case ClientOwnedDrawViewrangeOperationKind.ResolveRemoteUserPosition:
                        break;
                    case ClientOwnedDrawViewrangeOperationKind.RestorePreviousSmallDarkPatch:
                        DrawClientOwnedSmallDarkPatch(spriteBatch, operation.TopLeft, fogColor);
                        break;
                    case ClientOwnedDrawViewrangeOperationKind.ClearPreviousMaskHistory:
                        ResetClientOwnedPreviousMaskTopLeftsForCurrentFrame();
                        break;
                    case ClientOwnedDrawViewrangeOperationKind.DrawDarkLayerFallback:
                        DrawClientOwnedDarkLayerFallback(spriteBatch, fogColor);
                        break;
                    case ClientOwnedDrawViewrangeOperationKind.CopyLocalViewrange:
                    case ClientOwnedDrawViewrangeOperationKind.CopyRemoteViewrange:
                        if (DrawClientOwnedViewrangeFogAtTopLeft(
                            spriteBatch,
                            (int)MathF.Round(operation.TopLeft.X),
                            (int)MathF.Round(operation.TopLeft.Y),
                            fogColor,
                            drawClientOwnedDarkLayer: operation.Kind == ClientOwnedDrawViewrangeOperationKind.CopyLocalViewrange,
                            restorePreviousClientOwnedViewranges: false,
                            resetPreviousClientOwnedViewrangesForCurrentFrame: false,
                            operation.SourceX,
                            operation.SourceY,
                            operation.SourceWidth,
                            operation.SourceHeight,
                            operation.UsesRemoveAlphaCopy))
                        {
                            successfulCopyCount++;
                            successfulCopyMaskIndices.Add(operation.MaskIndex);
                        }
                        break;
                    case ClientOwnedDrawViewrangeOperationKind.AppendPreviousMaskHistory:
                        if (ShouldAppendClientOwnedPreviousMaskTopLeft(operation, successfulCopyMaskIndices))
                        {
                            TrackClientOwnedCurrentMaskTopLeft(operation.TopLeft);
                        }
                        break;
                }
            }

            if (ShouldDrawClientOwnedDarkLayerFallbackAfterCopyOperations(operations, successfulCopyCount))
            {
                DrawClientOwnedDarkLayerFallback(spriteBatch, fogColor);
            }
        }

        internal static bool ShouldAppendClientOwnedPreviousMaskTopLeft(
            ClientOwnedDrawViewrangeOperation operation,
            ISet<int> successfulCopyMaskIndices)
        {
            return operation.Kind == ClientOwnedDrawViewrangeOperationKind.AppendPreviousMaskHistory
                && successfulCopyMaskIndices != null
                && successfulCopyMaskIndices.Contains(operation.MaskIndex);
        }

        internal static bool ShouldDrawClientOwnedDarkLayerFallbackAfterCopyOperations(
            IReadOnlyList<ClientOwnedDrawViewrangeOperation> operations,
            int successfulCopyCount)
        {
            if (operations == null || successfulCopyCount > 0)
            {
                return false;
            }

            bool hasCopyOperation = false;
            bool hasExplicitFallbackOperation = false;
            foreach (ClientOwnedDrawViewrangeOperation operation in operations)
            {
                if (operation.Kind == ClientOwnedDrawViewrangeOperationKind.DrawDarkLayerFallback)
                {
                    hasExplicitFallbackOperation = true;
                    continue;
                }

                if (operation.Kind == ClientOwnedDrawViewrangeOperationKind.CopyLocalViewrange
                    || operation.Kind == ClientOwnedDrawViewrangeOperationKind.CopyRemoteViewrange)
                {
                    hasCopyOperation = true;
                }
            }

            return hasCopyOperation && !hasExplicitFallbackOperation;
        }

        private void DrawClientOwnedDarkLayerFallback(SpriteBatch spriteBatch, Color fogColor)
        {
            if (_pixelTexture == null)
            {
                return;
            }

            Rectangle darkBounds = GetClientOwnedDarkLayerBounds();
            if (darkBounds.Width <= 0 || darkBounds.Height <= 0)
            {
                return;
            }

            spriteBatch.Draw(_pixelTexture, darkBounds, fogColor);
        }

        private void DrawClientOwnedSmallDarkPatch(SpriteBatch spriteBatch, Vector2 topLeft, Color fogColor)
        {
            if (_pixelTexture == null)
            {
                return;
            }

            int width = _clientOwnedSmallDarkPatchWidth > 0
                ? _clientOwnedSmallDarkPatchWidth
                : _clientOwnedViewrangeTexture?.Width ?? 0;
            int height = _clientOwnedSmallDarkPatchHeight > 0
                ? _clientOwnedSmallDarkPatchHeight
                : _clientOwnedViewrangeTexture?.Height ?? 0;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (!TryResolveClientOwnedSmallDarkPatchRectangle(
                (int)MathF.Round(topLeft.X),
                (int)MathF.Round(topLeft.Y),
                width,
                height,
                GetClientOwnedDarkLayerBounds(),
                out Rectangle destinationRect))
            {
                return;
            }

            Color smallDarkColor = new((byte)0, (byte)0, (byte)0, fogColor.A);
            spriteBatch.Draw(_pixelTexture, destinationRect, smallDarkColor);
        }

        internal IReadOnlyList<Vector2> ClientOwnedPreviousMaskTopLefts => _clientOwnedPreviousMaskTopLefts;
        internal Point ClientOwnedSmallDarkPatchSize => new(_clientOwnedSmallDarkPatchWidth, _clientOwnedSmallDarkPatchHeight);

        private Vector2 GetClientOwnedUpdateParityScreenPosition(Vector2 worldPosition, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            Vector2 topLeft = ResolveClientOwnedMaskTopLeft(
                worldPosition.X,
                worldPosition.Y,
                mapShiftX,
                mapShiftY,
                centerX,
                centerY,
                _clientOwnedMaskOriginX,
                _clientOwnedMaskOriginY);
            return new Vector2(
                topLeft.X + (_clientOwnedMaskWidth * 0.5f),
                topLeft.Y + (_clientOwnedMaskHeight * 0.5f));
        }

        internal static Vector2 ResolveClientOwnedMaskTopLeft(
            float worldX,
            float worldY,
            int mapShiftX,
            int mapShiftY,
            int centerX,
            int centerY,
            float originX,
            float originY)
        {
            // Mirrors CField_LimitedView::DrawViewrange:
            // x = userX - centerX - viewrange.cx + 512
            // y = userY - centerY - viewrange.cy + 468
            return new Vector2(
                worldX - mapShiftX + centerX - originX,
                worldY - mapShiftY + centerY - originY);
        }

        private Rectangle GetClientOwnedDarkLayerBounds()
        {
            if (_clientOwnedDarkLayerWidth <= 0 || _clientOwnedDarkLayerHeight <= 0)
            {
                return new Rectangle(0, 0, _screenWidth, _screenHeight);
            }

            int screenCenterX = (int)MathF.Round(_screenWidth * 0.5f);
            int screenCenterY = (int)MathF.Round(_screenHeight * 0.5f);
            int left = screenCenterX + _clientOwnedDarkLayerOffsetX;
            int top = screenCenterY + _clientOwnedDarkLayerOffsetY;
            return new Rectangle(left, top, _clientOwnedDarkLayerWidth, _clientOwnedDarkLayerHeight);
        }
        #endregion

        #region Debug
        public void DrawDebug(SpriteBatch spriteBatch, SpriteFont font, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            if (!_enabled || font == null)
                return;

            int screenCenterX = (int)_centerX - mapShiftX + centerX;
            int screenCenterY = (int)_centerY - mapShiftY + centerY;

            // Draw center marker
            if (_pixelTexture != null)
            {
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(screenCenterX - 5, screenCenterY - 1, 10, 2),
                    Color.Yellow);
                spriteBatch.Draw(_pixelTexture,
                    new Rectangle(screenCenterX - 1, screenCenterY - 5, 2, 10),
                    Color.Yellow);
            }

            // Draw info text
            string info = $"LimitedView: {_mode}\n" +
                         $"Radius: {_currentRadius:F0} (target: {_targetRadius:F0})\n" +
                         $"Alpha: {_currentAlpha:F2}\n" +
                         $"Center: ({_centerX:F0}, {_centerY:F0})";

            spriteBatch.DrawString(font, info, new Vector2(10, _screenHeight - 100), Color.Yellow);
        }
        #endregion

        #region Cleanup
        public void Dispose()
        {
            _gradientCircle?.Dispose();
            _pixelTexture?.Dispose();
            _clientOwnedViewrangeTexture?.Dispose();
        }
        #endregion
    }
}
