using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Field Effects System - Manages field-level effects based on MapleStory's CField.
    ///
    /// Based on client analysis:
    /// - OnFieldEffect(): Handle field effect packets
    /// - OnBlowWeather(): Weather effects with messages
    /// - Fear effect: Darkness overlay with eye tracking
    /// - Field obstacles: Platform/obstacle on/off states
    /// </summary>
    public class FieldEffects
    {
        // Weather system
        private readonly List<WeatherMessageInfo> _weatherMessages = new();
        private WeatherEffectType _currentWeatherEffect = WeatherEffectType.None;
        private float _weatherIntensity = 1f;
        private int _weatherDuration = -1; // -1 = infinite
        private int _weatherStartTime;
        private string _weatherItemId = null;

        // Fear effect (darkness with watching eyes)
        private bool _fearEffectActive = false;
        private float _fearAlpha = 0f;
        private float _fearTargetAlpha = 0f;
        private int _fearStartTime;
        private int _fearDuration = 5000; // 5 seconds default
        private List<FearEye> _fearEyes = new();
        private Random _fearRandom = new();

        // Field obstacles
        private readonly Dictionary<string, FieldObstacle> _obstacles = new();

        // Screen flash for field effects
        private bool _screenFlashActive = false;
        private Color _screenFlashColor = Color.White;
        private float _screenFlashAlpha = 0f;
        private int _screenFlashStartTime;
        private int _screenFlashDuration;

        // Object effects (spawned at positions)
        private readonly List<FieldObjectEffect> _objectEffects = new();

        // Grey screen effect (for certain field states)
        private bool _greyScreenActive = false;
        private float _greyScreenAlpha = 0f;

        // Damage fog/mist effect
        private bool _damageMistActive = false;
        private float _damageMistAlpha = 0f;
        private int _damageMistDuration;
        private int _damageMistStartTime;

        #region Field Effect Handling (OnFieldEffect equivalent)

        /// <summary>
        /// Handle a field effect - based on client's OnFieldEffect handler
        /// </summary>
        /// <param name="effectType">Effect type from server</param>
        /// <param name="delay">Delay before effect starts</param>
        public void OnFieldEffect(FieldEffectType effectType, int delay, int currentTimeMs)
        {
            switch (effectType)
            {
                case FieldEffectType.Tremble:
                    // Handled by ScreenEffects.TriggerTremble
                    break;

                case FieldEffectType.MapEffect:
                    // Map-wide visual effect (usually played on all layers)
                    break;

                case FieldEffectType.ScreenFlash:
                    TriggerScreenFlash(Color.White, 1f, 500, currentTimeMs);
                    break;

                case FieldEffectType.Sound:
                    // Play field sound effect
                    break;

                case FieldEffectType.MobHpTag:
                    // Show HP bars for all mobs
                    break;

                case FieldEffectType.ObjectState:
                    // Change object state
                    break;

                case FieldEffectType.BlindEffect:
                    TriggerGreyScreen(true);
                    break;

                case FieldEffectType.StageChange:
                    // Boss stage transition effects
                    TriggerScreenFlash(Color.Black, 1f, 1000, currentTimeMs);
                    break;

                case FieldEffectType.TopScreen:
                    // Top screen message/banner
                    break;

                case FieldEffectType.MobTierGauge:
                    // Show mob tier gauge UI
                    break;

                case FieldEffectType.ResetScreen:
                    ResetAllEffects();
                    break;
            }
        }

        /// <summary>
        /// Handle object-specific field effect
        /// </summary>
        public void OnFieldObjectEffect(string objectName, float x, float y, string effectPath, int currentTimeMs)
        {
            var effect = new FieldObjectEffect
            {
                Name = objectName,
                X = x,
                Y = y,
                EffectPath = effectPath,
                StartTime = currentTimeMs,
                IsActive = true
            };
            _objectEffects.Add(effect);
        }

        #endregion

        #region Weather System (OnBlowWeather equivalent)

        /// <summary>
        /// Trigger weather effect - based on client's OnBlowWeather
        /// </summary>
        /// <param name="weatherType">Type of weather</param>
        /// <param name="itemId">Item ID for custom weather (null for default)</param>
        /// <param name="message">Weather message to display</param>
        /// <param name="intensity">Weather intensity (0-1)</param>
        /// <param name="duration">Duration in ms (-1 for infinite)</param>
        public void OnBlowWeather(WeatherEffectType weatherType, string itemId, string message,
            float intensity, int duration, int currentTimeMs)
        {
            _currentWeatherEffect = weatherType;
            _weatherItemId = itemId;
            _weatherIntensity = MathHelper.Clamp(intensity, 0f, 1f);
            _weatherDuration = duration;
            _weatherStartTime = currentTimeMs;

            // Add weather message if provided
            if (!string.IsNullOrEmpty(message))
            {
                AddWeatherMessage(message, weatherType, currentTimeMs);
            }
        }

        /// <summary>
        /// Add a weather message (WEATHERMSGINFO)
        /// </summary>
        public void AddWeatherMessage(string message, WeatherEffectType weatherType, int currentTimeMs)
        {
            var msgInfo = new WeatherMessageInfo
            {
                Message = message,
                WeatherType = weatherType,
                StartTime = currentTimeMs,
                Duration = 5000, // 5 seconds display
                FadeIn = true,
                Alpha = 0f
            };
            _weatherMessages.Add(msgInfo);
        }

        /// <summary>
        /// Stop current weather effect
        /// </summary>
        public void StopWeather()
        {
            _currentWeatherEffect = WeatherEffectType.None;
            _weatherItemId = null;
        }

        /// <summary>
        /// Get current weather type for particle system integration
        /// </summary>
        public WeatherEffectType CurrentWeather => _currentWeatherEffect;

        /// <summary>
        /// Get current weather intensity
        /// </summary>
        public float WeatherIntensity => _weatherIntensity;

        #endregion

        #region Fear Effect System

        /// <summary>
        /// Initialize fear effect (darkness with eyes) - based on client's InitFearEffect
        /// </summary>
        public void InitFearEffect(float targetAlpha, int duration, int eyeCount, int currentTimeMs)
        {
            _fearEffectActive = true;
            _fearTargetAlpha = MathHelper.Clamp(targetAlpha, 0f, 0.95f);
            _fearAlpha = 0f;
            _fearStartTime = currentTimeMs;
            _fearDuration = duration;

            // Create watching eyes
            _fearEyes.Clear();
            for (int i = 0; i < eyeCount; i++)
            {
                _fearEyes.Add(new FearEye
                {
                    X = (float)_fearRandom.NextDouble(),
                    Y = (float)_fearRandom.NextDouble(),
                    Scale = 0.5f + (float)_fearRandom.NextDouble() * 0.5f,
                    BlinkTimer = _fearRandom.Next(2000, 5000),
                    IsBlinking = false,
                    PupilOffsetX = 0,
                    PupilOffsetY = 0,
                    AppearDelay = i * 200 // Staggered appearance
                });
            }
        }

        /// <summary>
        /// Update fear effect - based on client's OnFearEffect
        /// </summary>
        /// <param name="mouseX">Mouse X position</param>
        /// <param name="mouseY">Mouse Y position</param>
        /// <param name="screenWidth">Screen width</param>
        /// <param name="screenHeight">Screen height</param>
        /// <param name="currentTimeMs">Current time in milliseconds</param>
        /// <param name="deltaTimeMs">Time since last frame in milliseconds</param>
        public void OnFearEffect(float mouseX, float mouseY, int screenWidth, int screenHeight, int currentTimeMs, float deltaTimeMs)
        {
            if (!_fearEffectActive) return;

            int elapsed = currentTimeMs - _fearStartTime;

            // Fade in during first 500ms
            if (elapsed < 500)
            {
                _fearAlpha = _fearTargetAlpha * (elapsed / 500f);
            }
            // Fade out during last 500ms
            else if (_fearDuration > 0 && elapsed > _fearDuration - 500)
            {
                float fadeOutProgress = (elapsed - (_fearDuration - 500)) / 500f;
                _fearAlpha = _fearTargetAlpha * (1f - fadeOutProgress);
            }
            else
            {
                _fearAlpha = _fearTargetAlpha;
            }

            // Check if fear effect has ended
            if (_fearDuration > 0 && elapsed >= _fearDuration)
            {
                _fearEffectActive = false;
                _fearAlpha = 0f;
                return;
            }

            // Update eyes - they track the mouse/player position
            float normalizedMouseX = mouseX / screenWidth;
            float normalizedMouseY = mouseY / screenHeight;

            foreach (var eye in _fearEyes)
            {
                // Skip if still in appear delay
                if (elapsed < eye.AppearDelay) continue;

                // Update blink timer with actual delta time for frame-rate independence
                eye.BlinkTimer -= (int)deltaTimeMs;
                if (eye.BlinkTimer <= 0)
                {
                    eye.IsBlinking = !eye.IsBlinking;
                    eye.BlinkTimer = eye.IsBlinking ? 150 : _fearRandom.Next(2000, 5000);
                }

                // Track mouse with pupils (limited range)
                float dx = normalizedMouseX - eye.X;
                float dy = normalizedMouseY - eye.Y;
                float maxOffset = 0.02f * eye.Scale;
                eye.PupilOffsetX = MathHelper.Clamp(dx * 0.1f, -maxOffset, maxOffset);
                eye.PupilOffsetY = MathHelper.Clamp(dy * 0.1f, -maxOffset, maxOffset);
            }
        }

        /// <summary>
        /// Check if fear effect is active
        /// </summary>
        public bool IsFearActive => _fearEffectActive;

        /// <summary>
        /// Get fear darkness alpha
        /// </summary>
        public float FearAlpha => _fearAlpha;

        /// <summary>
        /// Get fear eyes for rendering
        /// </summary>
        public IReadOnlyList<FearEye> FearEyes => _fearEyes;

        /// <summary>
        /// Stop fear effect
        /// </summary>
        public void StopFearEffect()
        {
            _fearEffectActive = false;
            _fearAlpha = 0f;
            _fearEyes.Clear();
        }

        #endregion

        #region Field Obstacles

        /// <summary>
        /// Toggle field obstacle on/off - based on client's OnFieldObstacleOnOff
        /// </summary>
        public void OnFieldObstacleOnOff(string obstacleName, bool isOn, int transitionTimeMs, int currentTimeMs)
        {
            if (!_obstacles.TryGetValue(obstacleName, out var obstacle))
            {
                obstacle = new FieldObstacle { Name = obstacleName };
                _obstacles[obstacleName] = obstacle;
            }

            obstacle.TargetState = isOn;
            obstacle.TransitionDuration = transitionTimeMs;
            obstacle.TransitionStartTime = currentTimeMs;
            obstacle.IsTransitioning = true;
        }

        /// <summary>
        /// Get obstacle state (0 = off, 1 = on, between = transitioning)
        /// </summary>
        public float GetObstacleState(string obstacleName, int currentTimeMs)
        {
            if (!_obstacles.TryGetValue(obstacleName, out var obstacle))
                return 0f;

            if (!obstacle.IsTransitioning)
                return obstacle.TargetState ? 1f : 0f;

            int elapsed = currentTimeMs - obstacle.TransitionStartTime;
            if (elapsed >= obstacle.TransitionDuration)
            {
                obstacle.IsTransitioning = false;
                return obstacle.TargetState ? 1f : 0f;
            }

            float progress = (float)elapsed / obstacle.TransitionDuration;
            return obstacle.TargetState ? progress : (1f - progress);
        }

        /// <summary>
        /// Check if obstacle is currently on (or transitioning to on)
        /// </summary>
        public bool IsObstacleOn(string obstacleName)
        {
            return _obstacles.TryGetValue(obstacleName, out var obstacle) && obstacle.TargetState;
        }

        #endregion

        #region Screen Effects

        /// <summary>
        /// Trigger screen flash effect
        /// </summary>
        public void TriggerScreenFlash(Color color, float intensity, int durationMs, int currentTimeMs)
        {
            _screenFlashActive = true;
            _screenFlashColor = color;
            _screenFlashAlpha = intensity;
            _screenFlashStartTime = currentTimeMs;
            _screenFlashDuration = durationMs;
        }

        /// <summary>
        /// Trigger grey screen effect (colorblind/blind effect)
        /// </summary>
        public void TriggerGreyScreen(bool active)
        {
            _greyScreenActive = active;
            _greyScreenAlpha = active ? 1f : 0f;
        }

        /// <summary>
        /// Trigger damage mist effect
        /// </summary>
        public void TriggerDamageMist(float intensity, int durationMs, int currentTimeMs)
        {
            _damageMistActive = true;
            _damageMistAlpha = intensity;
            _damageMistDuration = durationMs;
            _damageMistStartTime = currentTimeMs;
        }

        #endregion

        #region Update

        /// <summary>
        /// Update all field effects with frame-rate independent timing
        /// </summary>
        /// <param name="currentTimeMs">Current tick count in milliseconds</param>
        /// <param name="screenWidth">Screen width</param>
        /// <param name="screenHeight">Screen height</param>
        /// <param name="mouseX">Mouse X position</param>
        /// <param name="mouseY">Mouse Y position</param>
        /// <param name="deltaTimeMs">Time since last frame in milliseconds (defaults to 16.67ms for backwards compatibility)</param>
        public void Update(int currentTimeMs, int screenWidth, int screenHeight, float mouseX, float mouseY, float deltaTimeMs = 16.67f)
        {
            // Update weather messages
            UpdateWeatherMessages(currentTimeMs);

            // Update weather duration
            if (_currentWeatherEffect != WeatherEffectType.None && _weatherDuration > 0)
            {
                if (currentTimeMs - _weatherStartTime >= _weatherDuration)
                {
                    StopWeather();
                }
            }

            // Update fear effect
            if (_fearEffectActive)
            {
                OnFearEffect(mouseX, mouseY, screenWidth, screenHeight, currentTimeMs, deltaTimeMs);
            }

            // Update screen flash
            if (_screenFlashActive)
            {
                int elapsed = currentTimeMs - _screenFlashStartTime;
                if (elapsed >= _screenFlashDuration)
                {
                    _screenFlashActive = false;
                    _screenFlashAlpha = 0f;
                }
                else
                {
                    // Fade out over duration
                    _screenFlashAlpha = 1f - ((float)elapsed / _screenFlashDuration);
                }
            }

            // Update damage mist
            if (_damageMistActive)
            {
                int elapsed = currentTimeMs - _damageMistStartTime;
                if (elapsed >= _damageMistDuration)
                {
                    _damageMistActive = false;
                    _damageMistAlpha = 0f;
                }
                else
                {
                    // Pulse effect
                    float progress = (float)elapsed / _damageMistDuration;
                    _damageMistAlpha = (float)(0.3f + 0.2f * Math.Sin(progress * Math.PI * 4));
                }
            }

            // Update object effects
            for (int i = _objectEffects.Count - 1; i >= 0; i--)
            {
                var effect = _objectEffects[i];
                // Object effects typically last 1-2 seconds
                if (currentTimeMs - effect.StartTime > 2000)
                {
                    _objectEffects.RemoveAt(i);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateWeatherMessages(int currentTimeMs)
        {
            for (int i = _weatherMessages.Count - 1; i >= 0; i--)
            {
                var msg = _weatherMessages[i];
                int elapsed = currentTimeMs - msg.StartTime;

                if (elapsed >= msg.Duration)
                {
                    _weatherMessages.RemoveAt(i);
                    continue;
                }

                // Fade in first 500ms, fade out last 500ms
                if (elapsed < 500)
                {
                    msg.Alpha = elapsed / 500f;
                }
                else if (elapsed > msg.Duration - 500)
                {
                    msg.Alpha = (msg.Duration - elapsed) / 500f;
                }
                else
                {
                    msg.Alpha = 1f;
                }
            }
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw field effects overlay
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, int screenWidth, int screenHeight,
            int mapShiftX, int mapShiftY, SpriteFont font = null)
        {
            // Draw grey screen effect
            if (_greyScreenActive && _greyScreenAlpha > 0)
            {
                // This would require a shader for proper desaturation
                // For now, draw a semi-transparent grey overlay
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(0, 0, screenWidth, screenHeight),
                    new Color(128, 128, 128, (int)(_greyScreenAlpha * 100)));
            }

            // Draw damage mist
            if (_damageMistActive && _damageMistAlpha > 0)
            {
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(0, 0, screenWidth, screenHeight),
                    new Color(80, 0, 0, (int)(_damageMistAlpha * 255)));
            }

            // Draw fear effect (darkness with eyes)
            if (_fearEffectActive && _fearAlpha > 0)
            {
                DrawFearEffect(spriteBatch, pixelTexture, screenWidth, screenHeight);
            }

            // Draw screen flash
            if (_screenFlashActive && _screenFlashAlpha > 0)
            {
                Color flashColor = _screenFlashColor * _screenFlashAlpha;
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(0, 0, screenWidth, screenHeight),
                    flashColor);
            }

            // Draw weather messages
            if (font != null && _weatherMessages.Count > 0)
            {
                DrawWeatherMessages(spriteBatch, font, screenWidth, screenHeight);
            }
        }

        /// <summary>
        /// Draw fear effect overlay - based on client's DrawFearEffect
        /// </summary>
        private void DrawFearEffect(SpriteBatch spriteBatch, Texture2D pixelTexture, int screenWidth, int screenHeight)
        {
            // Draw darkness overlay
            spriteBatch.Draw(pixelTexture,
                new Rectangle(0, 0, screenWidth, screenHeight),
                new Color(0, 0, 0, (int)(_fearAlpha * 255)));

            // Draw watching eyes
            int elapsed = Environment.TickCount;
            foreach (var eye in _fearEyes)
            {
                if (elapsed - _fearStartTime < eye.AppearDelay) continue;
                if (eye.IsBlinking) continue;

                int eyeX = (int)(eye.X * screenWidth);
                int eyeY = (int)(eye.Y * screenHeight);
                int eyeSize = (int)(40 * eye.Scale);

                // Draw eye white (oval shape approximated with rectangle)
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(eyeX - eyeSize, eyeY - eyeSize / 2, eyeSize * 2, eyeSize),
                    new Color(200, 200, 180, (int)(200 * _fearAlpha)));

                // Draw pupil (follows mouse)
                int pupilSize = (int)(eyeSize * 0.4f);
                int pupilX = eyeX + (int)(eye.PupilOffsetX * screenWidth) - pupilSize / 2;
                int pupilY = eyeY + (int)(eye.PupilOffsetY * screenHeight) - pupilSize / 2;

                spriteBatch.Draw(pixelTexture,
                    new Rectangle(pupilX, pupilY, pupilSize, pupilSize),
                    new Color(20, 0, 0, (int)(255 * _fearAlpha)));

                // Draw red glow in pupil
                int glowSize = (int)(pupilSize * 0.3f);
                spriteBatch.Draw(pixelTexture,
                    new Rectangle(pupilX + pupilSize / 3, pupilY + pupilSize / 3, glowSize, glowSize),
                    new Color(180, 0, 0, (int)(200 * _fearAlpha)));
            }
        }

        /// <summary>
        /// Draw weather messages
        /// </summary>
        private void DrawWeatherMessages(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight)
        {
            int yOffset = 100;
            foreach (var msg in _weatherMessages)
            {
                if (msg.Alpha <= 0) continue;

                Vector2 textSize = font.MeasureString(msg.Message);
                Vector2 position = new Vector2(
                    (screenWidth - textSize.X) / 2,
                    yOffset);

                // Draw shadow
                spriteBatch.DrawString(font, msg.Message, position + new Vector2(2, 2),
                    new Color(0, 0, 0, (int)(msg.Alpha * 180)));

                // Draw text with weather-appropriate color
                Color textColor = GetWeatherMessageColor(msg.WeatherType);
                spriteBatch.DrawString(font, msg.Message, position,
                    new Color(textColor.R, textColor.G, textColor.B, (int)(msg.Alpha * 255)));

                yOffset += (int)textSize.Y + 10;
            }
        }

        private Color GetWeatherMessageColor(WeatherEffectType weatherType)
        {
            return weatherType switch
            {
                WeatherEffectType.Rain => new Color(150, 180, 255),
                WeatherEffectType.Snow => Color.White,
                WeatherEffectType.Leaves => new Color(180, 140, 60),
                WeatherEffectType.Cherry => new Color(255, 180, 200),
                WeatherEffectType.Stars => new Color(255, 255, 150),
                _ => Color.White
            };
        }

        #endregion

        #region Utility

        /// <summary>
        /// Reset all field effects
        /// </summary>
        public void ResetAllEffects()
        {
            StopWeather();
            StopFearEffect();
            _weatherMessages.Clear();
            _screenFlashActive = false;
            _greyScreenActive = false;
            _damageMistActive = false;
            _objectEffects.Clear();
            _obstacles.Clear();
        }

        /// <summary>
        /// Get active weather messages for UI display
        /// </summary>
        public IReadOnlyList<WeatherMessageInfo> WeatherMessages => _weatherMessages;

        #endregion
    }

    #region Supporting Enums and Classes

    /// <summary>
    /// Field effect types from client packets
    /// </summary>
    public enum FieldEffectType
    {
        None = 0,
        Tremble = 1,
        MapEffect = 2,
        ScreenFlash = 3,
        Sound = 4,
        MobHpTag = 5,
        ObjectState = 6,
        BlindEffect = 7,
        StageChange = 8,
        TopScreen = 9,
        MobTierGauge = 10,
        ResetScreen = 99
    }

    /// <summary>
    /// Weather effect types
    /// </summary>
    public enum WeatherEffectType
    {
        None = 0,
        Rain = 1,
        Snow = 2,
        Leaves = 3,
        Cherry = 4,     // Cherry blossom petals
        Stars = 5,      // Shooting stars
        Fireworks = 6,
        Custom = 99     // Custom item-based weather
    }

    /// <summary>
    /// Weather message info (WEATHERMSGINFO)
    /// </summary>
    public class WeatherMessageInfo
    {
        public string Message;
        public WeatherEffectType WeatherType;
        public int StartTime;
        public int Duration;
        public bool FadeIn;
        public float Alpha;
    }

    /// <summary>
    /// Fear effect eye
    /// </summary>
    public class FearEye
    {
        public float X, Y;          // Normalized screen position (0-1)
        public float Scale;
        public int BlinkTimer;
        public bool IsBlinking;
        public float PupilOffsetX;
        public float PupilOffsetY;
        public int AppearDelay;
    }

    /// <summary>
    /// Field obstacle state
    /// </summary>
    public class FieldObstacle
    {
        public string Name;
        public bool TargetState;
        public bool IsTransitioning;
        public int TransitionStartTime;
        public int TransitionDuration;
    }

    /// <summary>
    /// Field object effect (spawned effects at positions)
    /// </summary>
    public class FieldObjectEffect
    {
        public string Name;
        public float X, Y;
        public string EffectPath;
        public int StartTime;
        public bool IsActive;
    }

    #endregion
}
