using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Effects
{
    /// <summary>
    /// Particle System - Emitter-based particle effects for MapSimulator.
    /// Supports weather effects (rain, snow, leaves), explosions, sparkles, and custom particles.
    ///
    /// Based on MapleStory's particle rendering system with Effect.wz support.
    /// </summary>
    public class ParticleSystem
    {
        private readonly List<ParticleEmitter> _emitters = new();
        private readonly List<Particle> _particles = new();
        private readonly Queue<Particle> _particlePool = new();
        private readonly Random _random = new();

        private const int MAX_PARTICLES = 2000;
        private const int POOL_SIZE = 500;

        /// <summary>
        /// Total active particles
        /// </summary>
        public int ActiveParticleCount => _particles.Count;

        /// <summary>
        /// Total active emitters
        /// </summary>
        public int EmitterCount => _emitters.Count;

        public ParticleSystem()
        {
            // Pre-populate particle pool
            for (int i = 0; i < POOL_SIZE; i++)
            {
                _particlePool.Enqueue(new Particle());
            }
        }

        #region Emitter Management

        /// <summary>
        /// Add a particle emitter
        /// </summary>
        public int AddEmitter(ParticleEmitter emitter)
        {
            emitter.Id = _emitters.Count;
            _emitters.Add(emitter);
            return emitter.Id;
        }

        /// <summary>
        /// Remove an emitter by ID
        /// </summary>
        public bool RemoveEmitter(int id)
        {
            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                if (_emitters[i].Id == id)
                {
                    _emitters.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Create a rain weather emitter
        /// </summary>
        public int CreateRainEmitter(int screenWidth, int screenHeight, float intensity = 1f)
        {
            var emitter = new ParticleEmitter
            {
                EmitterType = EmitterType.Rain,
                X = screenWidth / 2f,
                Y = -50,
                Width = screenWidth + 200,
                Height = 10,
                ParticlesPerSecond = (int)(200 * intensity),
                ParticleLifetime = 2000,
                ParticleSpeed = 800f * intensity,
                ParticleSpeedVariance = 100f,
                ParticleAngle = MathHelper.PiOver2 + 0.2f, // Slightly angled
                ParticleAngleVariance = 0.1f,
                ParticleColor = new Color(180, 200, 255, 180),
                ParticleWidth = 2,
                ParticleHeight = 15,
                Gravity = 500f,
                IsWorldSpace = false,
                Duration = -1 // Infinite
            };
            return AddEmitter(emitter);
        }

        /// <summary>
        /// Create a snow weather emitter
        /// </summary>
        public int CreateSnowEmitter(int screenWidth, int screenHeight, float intensity = 1f)
        {
            var emitter = new ParticleEmitter
            {
                EmitterType = EmitterType.Snow,
                X = screenWidth / 2f,
                Y = -20,
                Width = screenWidth + 100,
                Height = 10,
                ParticlesPerSecond = (int)(50 * intensity),
                ParticleLifetime = 8000,
                ParticleSpeed = 50f * intensity,
                ParticleSpeedVariance = 30f,
                ParticleAngle = MathHelper.PiOver2,
                ParticleAngleVariance = 0.5f,
                ParticleColor = Color.White,
                ParticleWidth = 4,
                ParticleHeight = 4,
                Gravity = 20f,
                WindStrength = 30f,
                WindVariance = 20f,
                RotationSpeed = 2f,
                IsWorldSpace = false,
                Duration = -1
            };
            return AddEmitter(emitter);
        }

        /// <summary>
        /// Create a falling leaves emitter
        /// </summary>
        public int CreateLeavesEmitter(int screenWidth, int screenHeight, float intensity = 1f)
        {
            var emitter = new ParticleEmitter
            {
                EmitterType = EmitterType.Leaves,
                X = screenWidth / 2f,
                Y = -30,
                Width = screenWidth + 100,
                Height = 10,
                ParticlesPerSecond = (int)(10 * intensity),
                ParticleLifetime = 10000,
                ParticleSpeed = 40f,
                ParticleSpeedVariance = 20f,
                ParticleAngle = MathHelper.PiOver2,
                ParticleAngleVariance = 0.3f,
                ParticleColor = new Color(180, 140, 60),
                ParticleColorVariance = new Color(40, 40, 20),
                ParticleWidth = 8,
                ParticleHeight = 6,
                Gravity = 15f,
                WindStrength = 50f,
                WindVariance = 40f,
                RotationSpeed = 3f,
                SwayAmplitude = 30f,
                SwayFrequency = 2f,
                IsWorldSpace = false,
                Duration = -1
            };
            return AddEmitter(emitter);
        }

        /// <summary>
        /// Create a sparkle/firework burst emitter
        /// </summary>
        public int CreateSparkleBurst(float x, float y, int count, Color color, int duration = 1000)
        {
            var emitter = new ParticleEmitter
            {
                EmitterType = EmitterType.Burst,
                X = x,
                Y = y,
                Width = 1,
                Height = 1,
                ParticlesPerSecond = count * 10, // Emit quickly
                ParticleLifetime = duration,
                ParticleSpeed = 150f,
                ParticleSpeedVariance = 100f,
                ParticleAngle = 0,
                ParticleAngleVariance = MathHelper.TwoPi, // Full circle
                ParticleColor = color,
                ParticleWidth = 3,
                ParticleHeight = 3,
                Gravity = 100f,
                FadeOut = true,
                ScaleOverLife = true,
                EndScale = 0.2f,
                IsWorldSpace = true,
                Duration = 100, // Short emission burst
                MaxParticles = count
            };
            return AddEmitter(emitter);
        }

        /// <summary>
        /// Create a smoke/cloud emitter
        /// </summary>
        public int CreateSmokeEmitter(float x, float y, float width, int duration = -1)
        {
            var emitter = new ParticleEmitter
            {
                EmitterType = EmitterType.Smoke,
                X = x,
                Y = y,
                Width = width,
                Height = 10,
                ParticlesPerSecond = 15,
                ParticleLifetime = 3000,
                ParticleSpeed = 30f,
                ParticleSpeedVariance = 15f,
                ParticleAngle = -MathHelper.PiOver2, // Upward
                ParticleAngleVariance = 0.3f,
                ParticleColor = new Color(100, 100, 100, 150),
                ParticleWidth = 20,
                ParticleHeight = 20,
                Gravity = -20f, // Rise
                FadeOut = true,
                ScaleOverLife = true,
                StartScale = 0.5f,
                EndScale = 2f,
                IsWorldSpace = true,
                Duration = duration
            };
            return AddEmitter(emitter);
        }

        #endregion

        #region Update

        /// <summary>
        /// Update all emitters and particles
        /// </summary>
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            // Update emitters and spawn new particles
            for (int i = _emitters.Count - 1; i >= 0; i--)
            {
                var emitter = _emitters[i];

                // Check if emitter has expired
                if (emitter.Duration > 0 && currentTimeMs - emitter.StartTime > emitter.Duration)
                {
                    _emitters.RemoveAt(i);
                    continue;
                }

                // Spawn particles
                emitter.AccumulatedTime += deltaSeconds;
                float spawnInterval = 1f / emitter.ParticlesPerSecond;

                while (emitter.AccumulatedTime >= spawnInterval && _particles.Count < MAX_PARTICLES)
                {
                    if (emitter.MaxParticles > 0 && emitter.SpawnedCount >= emitter.MaxParticles)
                        break;

                    SpawnParticle(emitter, currentTimeMs);
                    emitter.AccumulatedTime -= spawnInterval;
                    emitter.SpawnedCount++;
                }
            }

            // Update particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var particle = _particles[i];

                // Check lifetime
                int age = currentTimeMs - particle.SpawnTime;
                if (age >= particle.Lifetime)
                {
                    _particlePool.Enqueue(particle);
                    _particles.RemoveAt(i);
                    continue;
                }

                // Update position
                particle.VelocityY += particle.Gravity * deltaSeconds;
                particle.VelocityX += particle.Wind * deltaSeconds;

                // Apply sway for leaves
                if (particle.SwayAmplitude > 0)
                {
                    float swayOffset = (float)Math.Sin(age * 0.001f * particle.SwayFrequency) * particle.SwayAmplitude;
                    particle.X += swayOffset * deltaSeconds;
                }

                particle.X += particle.VelocityX * deltaSeconds;
                particle.Y += particle.VelocityY * deltaSeconds;

                // Update rotation
                particle.Rotation += particle.RotationSpeed * deltaSeconds;

                // Update alpha and scale based on lifetime
                float lifeProgress = (float)age / particle.Lifetime;

                if (particle.FadeOut)
                {
                    particle.Alpha = 1f - lifeProgress;
                }

                if (particle.ScaleOverLife)
                {
                    particle.Scale = MathHelper.Lerp(particle.StartScale, particle.EndScale, lifeProgress);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SpawnParticle(ParticleEmitter emitter, int currentTimeMs)
        {
            Particle particle = _particlePool.Count > 0 ? _particlePool.Dequeue() : new Particle();

            // Random position within emitter bounds
            float spawnX = emitter.X + (float)(_random.NextDouble() - 0.5) * emitter.Width;
            float spawnY = emitter.Y + (float)(_random.NextDouble() - 0.5) * emitter.Height;

            // Random angle
            float angle = emitter.ParticleAngle + (float)(_random.NextDouble() - 0.5) * 2 * emitter.ParticleAngleVariance;

            // Random speed
            float speed = emitter.ParticleSpeed + (float)(_random.NextDouble() - 0.5) * 2 * emitter.ParticleSpeedVariance;

            // Calculate velocity
            float vx = (float)Math.Cos(angle) * speed;
            float vy = (float)Math.Sin(angle) * speed;

            // Random color variation
            Color color = emitter.ParticleColor;
            if (emitter.ParticleColorVariance != Color.Transparent)
            {
                int r = MathHelper.Clamp(color.R + _random.Next(-emitter.ParticleColorVariance.R, emitter.ParticleColorVariance.R + 1), 0, 255);
                int g = MathHelper.Clamp(color.G + _random.Next(-emitter.ParticleColorVariance.G, emitter.ParticleColorVariance.G + 1), 0, 255);
                int b = MathHelper.Clamp(color.B + _random.Next(-emitter.ParticleColorVariance.B, emitter.ParticleColorVariance.B + 1), 0, 255);
                color = new Color(r, g, b, color.A);
            }

            // Initialize particle
            particle.X = spawnX;
            particle.Y = spawnY;
            particle.VelocityX = vx;
            particle.VelocityY = vy;
            particle.Width = emitter.ParticleWidth;
            particle.Height = emitter.ParticleHeight;
            particle.Color = color;
            particle.Alpha = 1f;
            particle.Rotation = (float)(_random.NextDouble() * MathHelper.TwoPi);
            particle.RotationSpeed = emitter.RotationSpeed * (float)(_random.NextDouble() * 2 - 1);
            particle.Scale = emitter.StartScale;
            particle.StartScale = emitter.StartScale;
            particle.EndScale = emitter.EndScale;
            particle.Gravity = emitter.Gravity;
            particle.Wind = emitter.WindStrength + (float)(_random.NextDouble() - 0.5) * 2 * emitter.WindVariance;
            particle.SwayAmplitude = emitter.SwayAmplitude;
            particle.SwayFrequency = emitter.SwayFrequency;
            particle.FadeOut = emitter.FadeOut;
            particle.ScaleOverLife = emitter.ScaleOverLife;
            particle.Lifetime = emitter.ParticleLifetime;
            particle.SpawnTime = currentTimeMs;
            particle.IsWorldSpace = emitter.IsWorldSpace;
            particle.Texture = emitter.Texture;

            _particles.Add(particle);
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw all particles
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Texture2D pixelTexture, int mapShiftX, int mapShiftY)
        {
            foreach (var particle in _particles)
            {
                float drawX = particle.X;
                float drawY = particle.Y;

                // Apply map shift for world-space particles
                if (particle.IsWorldSpace)
                {
                    drawX += mapShiftX;
                    drawY += mapShiftY;
                }

                Color drawColor = particle.Color * particle.Alpha;
                Texture2D texture = particle.Texture ?? pixelTexture;

                Rectangle destRect = new Rectangle(
                    (int)(drawX - particle.Width * particle.Scale / 2),
                    (int)(drawY - particle.Height * particle.Scale / 2),
                    (int)(particle.Width * particle.Scale),
                    (int)(particle.Height * particle.Scale));

                spriteBatch.Draw(
                    texture,
                    destRect,
                    null,
                    drawColor,
                    particle.Rotation,
                    new Vector2(texture.Width / 2f, texture.Height / 2f),
                    SpriteEffects.None,
                    0f);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Clear all particles and emitters
        /// </summary>
        public void Clear()
        {
            foreach (var particle in _particles)
            {
                _particlePool.Enqueue(particle);
            }
            _particles.Clear();
            _emitters.Clear();
        }

        /// <summary>
        /// Stop all emitters (particles will finish their lifetime)
        /// </summary>
        public void StopEmitters()
        {
            _emitters.Clear();
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Emitter type for preset behaviors
    /// </summary>
    public enum EmitterType
    {
        Generic,
        Rain,
        Snow,
        Leaves,
        Burst,
        Smoke,
        Fire,
        Sparkle
    }

    /// <summary>
    /// Particle emitter configuration
    /// </summary>
    public class ParticleEmitter
    {
        public int Id;
        public EmitterType EmitterType = EmitterType.Generic;

        // Position and size
        public float X, Y;
        public float Width = 100, Height = 10;

        // Emission
        public int ParticlesPerSecond = 50;
        public int MaxParticles = 0; // 0 = unlimited
        public int Duration = -1; // -1 = infinite
        public int StartTime;
        public float AccumulatedTime;
        public int SpawnedCount;

        // Particle properties
        public int ParticleLifetime = 1000;
        public float ParticleSpeed = 100f;
        public float ParticleSpeedVariance = 20f;
        public float ParticleAngle = MathHelper.PiOver2; // Down
        public float ParticleAngleVariance = 0.1f;
        public Color ParticleColor = Color.White;
        public Color ParticleColorVariance = Color.Transparent;
        public int ParticleWidth = 4;
        public int ParticleHeight = 4;

        // Physics
        public float Gravity = 0f;
        public float WindStrength = 0f;
        public float WindVariance = 0f;

        // Visual effects
        public float RotationSpeed = 0f;
        public float SwayAmplitude = 0f;
        public float SwayFrequency = 1f;
        public bool FadeOut = false;
        public bool ScaleOverLife = false;
        public float StartScale = 1f;
        public float EndScale = 1f;

        // Other
        public bool IsWorldSpace = false;
        public Texture2D Texture = null;
    }

    /// <summary>
    /// Individual particle
    /// </summary>
    internal class Particle
    {
        public float X, Y;
        public float VelocityX, VelocityY;
        public int Width, Height;
        public Color Color;
        public float Alpha;
        public float Rotation;
        public float RotationSpeed;
        public float Scale;
        public float StartScale, EndScale;
        public float Gravity;
        public float Wind;
        public float SwayAmplitude;
        public float SwayFrequency;
        public bool FadeOut;
        public bool ScaleOverLife;
        public int Lifetime;
        public int SpawnTime;
        public bool IsWorldSpace;
        public Texture2D Texture;
    }

    #endregion
}
