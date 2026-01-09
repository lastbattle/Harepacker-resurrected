using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HaCreator.MapSimulator.Fields
{
    /// <summary>
    /// Dynamic Foothold System - Moving platforms based on MapleStory's CField_DynamicFoothold.
    ///
    /// Supports:
    /// - Moving platforms with waypoint paths
    /// - Platform spawn/despawn with timers
    /// - Horizontal and vertical movement patterns
    /// - Entity synchronization (mobs/players on platforms move with them)
    /// </summary>
    public class DynamicFootholdSystem
    {
        private readonly List<DynamicPlatform> _platforms = new();
        private readonly List<int> _entityOnPlatform = new(); // Entity IDs currently on each platform

        /// <summary>
        /// Total number of active platforms
        /// </summary>
        public int PlatformCount => _platforms.Count;

        #region Platform Management

        /// <summary>
        /// Add a dynamic platform
        /// </summary>
        public int AddPlatform(DynamicPlatform platform)
        {
            platform.Id = _platforms.Count;
            _platforms.Add(platform);
            return platform.Id;
        }

        /// <summary>
        /// Get a platform by ID
        /// </summary>
        public DynamicPlatform GetPlatform(int id)
        {
            if (id >= 0 && id < _platforms.Count)
                return _platforms[id];
            return null;
        }

        /// <summary>
        /// Create a horizontal moving platform
        /// </summary>
        public int CreateHorizontalPlatform(float startX, float y, float width, float height,
            float leftBound, float rightBound, float speed, int delay = 0)
        {
            var platform = new DynamicPlatform
            {
                X = startX,
                Y = y,
                Width = width,
                Height = height,
                MovementType = PlatformMovementType.Horizontal,
                Speed = speed,
                LeftBound = leftBound,
                RightBound = rightBound,
                MovingRight = true,
                PauseDelay = delay,
                IsActive = true,
                IsVisible = true
            };
            return AddPlatform(platform);
        }

        /// <summary>
        /// Create a vertical moving platform
        /// </summary>
        public int CreateVerticalPlatform(float x, float startY, float width, float height,
            float topBound, float bottomBound, float speed, int delay = 0)
        {
            var platform = new DynamicPlatform
            {
                X = x,
                Y = startY,
                Width = width,
                Height = height,
                MovementType = PlatformMovementType.Vertical,
                Speed = speed,
                TopBound = topBound,
                BottomBound = bottomBound,
                MovingDown = true,
                PauseDelay = delay,
                IsActive = true,
                IsVisible = true
            };
            return AddPlatform(platform);
        }

        /// <summary>
        /// Create a platform that follows a waypoint path
        /// </summary>
        public int CreateWaypointPlatform(float width, float height, List<Vector2> waypoints,
            float speed, bool loop = true, int pauseAtWaypoint = 0)
        {
            if (waypoints == null || waypoints.Count < 2)
                return -1;

            var platform = new DynamicPlatform
            {
                X = waypoints[0].X,
                Y = waypoints[0].Y,
                Width = width,
                Height = height,
                MovementType = PlatformMovementType.Waypoint,
                Speed = speed,
                Waypoints = new List<Vector2>(waypoints),
                CurrentWaypointIndex = 0,
                LoopWaypoints = loop,
                PauseDelay = pauseAtWaypoint,
                IsActive = true,
                IsVisible = true
            };
            return AddPlatform(platform);
        }

        /// <summary>
        /// Create a platform that spawns/despawns on a timer
        /// </summary>
        public int CreateTimedPlatform(float x, float y, float width, float height,
            int visibleDuration, int hiddenDuration, int initialDelay = 0)
        {
            var platform = new DynamicPlatform
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                MovementType = PlatformMovementType.Static,
                SpawnDespawn = true,
                VisibleDuration = visibleDuration,
                HiddenDuration = hiddenDuration,
                SpawnTimer = initialDelay,
                IsActive = true,
                IsVisible = initialDelay == 0
            };
            return AddPlatform(platform);
        }

        #endregion

        #region Update

        /// <summary>
        /// Update all dynamic platforms
        /// </summary>
        public void Update(int currentTimeMs, float deltaSeconds)
        {
            for (int i = 0; i < _platforms.Count; i++)
            {
                var platform = _platforms[i];
                if (!platform.IsActive)
                    continue;

                // Store previous position for entity sync
                float prevX = platform.X;
                float prevY = platform.Y;

                // Handle spawn/despawn timing
                if (platform.SpawnDespawn)
                {
                    UpdateSpawnDespawn(platform, currentTimeMs);
                }

                // Handle pause at waypoints/bounds
                if (platform.IsPaused)
                {
                    if (currentTimeMs - platform.PauseStartTime >= platform.PauseDelay)
                    {
                        platform.IsPaused = false;
                    }
                    continue;
                }

                // Update position based on movement type
                switch (platform.MovementType)
                {
                    case PlatformMovementType.Horizontal:
                        UpdateHorizontalMovement(platform, deltaSeconds);
                        break;
                    case PlatformMovementType.Vertical:
                        UpdateVerticalMovement(platform, deltaSeconds);
                        break;
                    case PlatformMovementType.Waypoint:
                        UpdateWaypointMovement(platform, deltaSeconds, currentTimeMs);
                        break;
                }

                // Calculate movement delta for entity sync
                platform.DeltaX = platform.X - prevX;
                platform.DeltaY = platform.Y - prevY;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateSpawnDespawn(DynamicPlatform platform, int currentTimeMs)
        {
            platform.SpawnTimer += 16; // Approximate frame time

            if (platform.IsVisible)
            {
                if (platform.SpawnTimer >= platform.VisibleDuration)
                {
                    platform.IsVisible = false;
                    platform.SpawnTimer = 0;
                    platform.FadeAlpha = 1f;
                    platform.IsFading = true;
                }
            }
            else
            {
                if (platform.SpawnTimer >= platform.HiddenDuration)
                {
                    platform.IsVisible = true;
                    platform.SpawnTimer = 0;
                    platform.FadeAlpha = 0f;
                    platform.IsFading = true;
                }
            }

            // Handle fade animation
            if (platform.IsFading)
            {
                float fadeSpeed = 0.05f;
                if (platform.IsVisible)
                {
                    platform.FadeAlpha = Math.Min(1f, platform.FadeAlpha + fadeSpeed);
                    if (platform.FadeAlpha >= 1f)
                        platform.IsFading = false;
                }
                else
                {
                    platform.FadeAlpha = Math.Max(0f, platform.FadeAlpha - fadeSpeed);
                    if (platform.FadeAlpha <= 0f)
                        platform.IsFading = false;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateHorizontalMovement(DynamicPlatform platform, float deltaSeconds)
        {
            float movement = platform.Speed * deltaSeconds;

            if (platform.MovingRight)
            {
                platform.X += movement;
                if (platform.X >= platform.RightBound)
                {
                    platform.X = platform.RightBound;
                    platform.MovingRight = false;
                    if (platform.PauseDelay > 0)
                    {
                        platform.IsPaused = true;
                        platform.PauseStartTime = Environment.TickCount;
                    }
                }
            }
            else
            {
                platform.X -= movement;
                if (platform.X <= platform.LeftBound)
                {
                    platform.X = platform.LeftBound;
                    platform.MovingRight = true;
                    if (platform.PauseDelay > 0)
                    {
                        platform.IsPaused = true;
                        platform.PauseStartTime = Environment.TickCount;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateVerticalMovement(DynamicPlatform platform, float deltaSeconds)
        {
            float movement = platform.Speed * deltaSeconds;

            if (platform.MovingDown)
            {
                platform.Y += movement;
                if (platform.Y >= platform.BottomBound)
                {
                    platform.Y = platform.BottomBound;
                    platform.MovingDown = false;
                    if (platform.PauseDelay > 0)
                    {
                        platform.IsPaused = true;
                        platform.PauseStartTime = Environment.TickCount;
                    }
                }
            }
            else
            {
                platform.Y -= movement;
                if (platform.Y <= platform.TopBound)
                {
                    platform.Y = platform.TopBound;
                    platform.MovingDown = true;
                    if (platform.PauseDelay > 0)
                    {
                        platform.IsPaused = true;
                        platform.PauseStartTime = Environment.TickCount;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateWaypointMovement(DynamicPlatform platform, float deltaSeconds, int currentTimeMs)
        {
            if (platform.Waypoints == null || platform.Waypoints.Count < 2)
                return;

            int nextIndex = (platform.CurrentWaypointIndex + 1) % platform.Waypoints.Count;
            Vector2 target = platform.Waypoints[nextIndex];
            Vector2 current = new Vector2(platform.X, platform.Y);

            Vector2 direction = target - current;
            float distance = direction.Length();

            if (distance < 1f) // Reached waypoint
            {
                platform.X = target.X;
                platform.Y = target.Y;
                platform.CurrentWaypointIndex = nextIndex;

                // Check if we've completed the path
                if (!platform.LoopWaypoints && nextIndex == platform.Waypoints.Count - 1)
                {
                    platform.IsActive = false;
                    return;
                }

                // Pause at waypoint
                if (platform.PauseDelay > 0)
                {
                    platform.IsPaused = true;
                    platform.PauseStartTime = currentTimeMs;
                }
            }
            else
            {
                // Move towards target
                direction.Normalize();
                float movement = Math.Min(platform.Speed * deltaSeconds, distance);
                platform.X += direction.X * movement;
                platform.Y += direction.Y * movement;
            }
        }

        #endregion

        #region Collision Detection

        /// <summary>
        /// Check if a point is on any active platform
        /// Returns the platform ID or -1 if not on any platform
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetPlatformAtPoint(float x, float y, float tolerance = 5f)
        {
            for (int i = 0; i < _platforms.Count; i++)
            {
                var platform = _platforms[i];
                if (!platform.IsActive || !platform.IsVisible)
                    continue;

                // Check if point is on top of platform (within tolerance)
                float platformTop = platform.Y;
                float platformLeft = platform.X;
                float platformRight = platform.X + platform.Width;

                if (x >= platformLeft && x <= platformRight &&
                    y >= platformTop - tolerance && y <= platformTop + tolerance)
                {
                    return platform.Id;
                }
            }
            return -1;
        }

        /// <summary>
        /// Get the movement delta for an entity on a platform
        /// </summary>
        public Vector2 GetPlatformDelta(int platformId)
        {
            if (platformId >= 0 && platformId < _platforms.Count)
            {
                var platform = _platforms[platformId];
                return new Vector2(platform.DeltaX, platform.DeltaY);
            }
            return Vector2.Zero;
        }

        /// <summary>
        /// Get the Y position of a platform's surface at a given X
        /// </summary>
        public float GetPlatformSurfaceY(int platformId)
        {
            if (platformId >= 0 && platformId < _platforms.Count)
            {
                return _platforms[platformId].Y;
            }
            return float.NaN;
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw all platforms (debug visualization)
        /// </summary>
        public void DrawDebug(SpriteBatch spriteBatch, Texture2D pixelTexture, int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            // Calculate shift center (same as other debug overlays)
            int shiftCenterX = mapShiftX - centerX;
            int shiftCenterY = mapShiftY - centerY;

            for (int i = 0; i < _platforms.Count; i++)
            {
                var platform = _platforms[i];
                if (!platform.IsActive)
                    continue;

                float alpha = platform.IsVisible ? platform.FadeAlpha : 0f;
                if (alpha <= 0 && !platform.SpawnDespawn)
                    alpha = 1f;

                // Convert map coordinates to screen coordinates
                int screenX = (int)(platform.X - shiftCenterX);
                int screenY = (int)(platform.Y - shiftCenterY);

                // Draw platform rectangle
                Color platformColor = platform.MovementType switch
                {
                    PlatformMovementType.Horizontal => new Color(100, 200, 100, (int)(180 * alpha)),
                    PlatformMovementType.Vertical => new Color(100, 100, 200, (int)(180 * alpha)),
                    PlatformMovementType.Waypoint => new Color(200, 100, 200, (int)(180 * alpha)),
                    _ => new Color(150, 150, 150, (int)(180 * alpha))
                };

                Rectangle rect = new Rectangle(screenX, screenY, (int)platform.Width, (int)platform.Height);
                spriteBatch.Draw(pixelTexture, rect, platformColor);

                // Draw border
                Color borderColor = new Color(255, 255, 255, (int)(255 * alpha));
                spriteBatch.Draw(pixelTexture, new Rectangle(screenX, screenY, (int)platform.Width, 2), borderColor);
                spriteBatch.Draw(pixelTexture, new Rectangle(screenX, screenY + (int)platform.Height - 2, (int)platform.Width, 2), borderColor);
                spriteBatch.Draw(pixelTexture, new Rectangle(screenX, screenY, 2, (int)platform.Height), borderColor);
                spriteBatch.Draw(pixelTexture, new Rectangle(screenX + (int)platform.Width - 2, screenY, 2, (int)platform.Height), borderColor);

                // Draw waypoints for waypoint platforms
                if (platform.MovementType == PlatformMovementType.Waypoint && platform.Waypoints != null)
                {
                    foreach (var waypoint in platform.Waypoints)
                    {
                        int wpX = (int)(waypoint.X - shiftCenterX);
                        int wpY = (int)(waypoint.Y - shiftCenterY);
                        spriteBatch.Draw(pixelTexture, new Rectangle(wpX - 3, wpY - 3, 6, 6), Color.Yellow);
                    }
                }
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Clear all platforms
        /// </summary>
        public void Clear()
        {
            _platforms.Clear();
        }

        /// <summary>
        /// Set platform active state
        /// </summary>
        public void SetPlatformActive(int id, bool active)
        {
            if (id >= 0 && id < _platforms.Count)
            {
                _platforms[id].IsActive = active;
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Platform movement type
    /// </summary>
    public enum PlatformMovementType
    {
        Static,
        Horizontal,
        Vertical,
        Waypoint,
        Circular
    }

    /// <summary>
    /// Dynamic platform data
    /// </summary>
    public class DynamicPlatform
    {
        public int Id;
        public float X, Y;
        public float Width, Height;

        // Movement
        public PlatformMovementType MovementType = PlatformMovementType.Static;
        public float Speed = 50f;

        // Horizontal bounds
        public float LeftBound, RightBound;
        public bool MovingRight = true;

        // Vertical bounds
        public float TopBound, BottomBound;
        public bool MovingDown = true;

        // Waypoint path
        public List<Vector2> Waypoints;
        public int CurrentWaypointIndex;
        public bool LoopWaypoints = true;

        // Pause at endpoints
        public int PauseDelay;
        public bool IsPaused;
        public int PauseStartTime;

        // Spawn/Despawn
        public bool SpawnDespawn;
        public int VisibleDuration = 3000;
        public int HiddenDuration = 2000;
        public int SpawnTimer;
        public bool IsFading;
        public float FadeAlpha = 1f;

        // State
        public bool IsActive = true;
        public bool IsVisible = true;

        // Movement delta (for entity sync)
        public float DeltaX, DeltaY;

        // Optional texture
        public Texture2D Texture;
    }

    #endregion
}
