using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.CompilerServices;

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
        private Texture2D _fogTexture;              // Full-screen fog overlay
        private Texture2D _gradientCircle;          // Radial gradient for soft edges
        private Texture2D _pixelTexture;            // 1x1 white pixel for drawing
        private GraphicsDevice _device;
        private int _screenWidth, _screenHeight;
        #endregion

        #region Public Properties
        public bool Enabled => _enabled;
        public ViewMode Mode => _mode;
        public float ViewRadius => _currentRadius;
        public Color FogColor => _fogColor;
        #endregion

        #region Initialization
        public void Initialize(GraphicsDevice device, int screenWidth, int screenHeight)
        {
            _device = device;
            _screenWidth = screenWidth;
            _screenHeight = screenHeight;

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
            int screenCenterX = _screenWidth / 2;
            int screenCenterY = _screenHeight / 2;

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

        private void DrawCircularFog(SpriteBatch spriteBatch, int centerX, int centerY, float radius, Color fogColor)
        {
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
            _fogTexture?.Dispose();
            _gradientCircle?.Dispose();
            _pixelTexture?.Dispose();
        }
        #endregion
    }
}
