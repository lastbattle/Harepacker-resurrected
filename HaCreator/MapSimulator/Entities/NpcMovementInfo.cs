using System;

namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Simple movement info for NPC walking behavior.
    /// NPCs walk back and forth within their rx0/rx1 range.
    /// </summary>
    public class NpcMovementInfo
    {
        // Current position
        public float X { get; private set; }
        public float Y { get; private set; }

        // Spawn position
        private float _spawnX;
        private float _spawnY;

        // Movement boundaries (rx0 = left bound, rx1 = right bound)
        public int RX0 { get; private set; }
        public int RX1 { get; private set; }

        // Movement state
        public bool IsMoving { get; private set; }
        public bool FlipX { get; private set; }  // true = facing left
        public float MoveSpeed { get; set; } = 1.5f;  // Default NPC walk speed

        // Movement timing
        private int _standTimer = 0;
        private int _moveTimer = 0;
        private const int MIN_STAND_TIME = 2000;  // Stand for at least 2 seconds
        private const int MAX_STAND_TIME = 5000;  // Stand for up to 5 seconds
        private const int MIN_MOVE_TIME = 1000;   // Move for at least 1 second
        private const int MAX_MOVE_TIME = 3000;   // Move for up to 3 seconds

        private static readonly Random _random = new Random();
        private int _currentStandDuration;
        private int _currentMoveDuration;

        // Whether this NPC can move at all
        public bool CanMove { get; private set; }

        /// <summary>
        /// Initialize the NPC movement info
        /// </summary>
        public void Initialize(int x, int y, int rx0Shift, int rx1Shift, bool hasWalkAnimation)
        {
            _spawnX = x;
            _spawnY = y;
            X = x;
            Y = y;

            // Calculate movement boundaries
            RX0 = x - rx0Shift;
            RX1 = x + rx1Shift;

            // NPC can move if it has walk animation and has a movement range
            CanMove = hasWalkAnimation && (rx0Shift > 0 || rx1Shift > 0);

            // Start standing
            IsMoving = false;
            _currentStandDuration = _random.Next(MIN_STAND_TIME, MAX_STAND_TIME);
            _standTimer = 0;

            // Random initial facing direction
            FlipX = _random.Next(2) == 0;
        }

        /// <summary>
        /// Update NPC movement
        /// </summary>
        /// <param name="deltaTimeMs">Time elapsed since last update in milliseconds</param>
        public void UpdateMovement(int deltaTimeMs)
        {
            if (!CanMove)
                return;

            if (IsMoving)
            {
                UpdateWalking(deltaTimeMs);
            }
            else
            {
                UpdateStanding(deltaTimeMs);
            }
        }

        /// <summary>
        /// Update while standing still
        /// </summary>
        private void UpdateStanding(int deltaTimeMs)
        {
            _standTimer += deltaTimeMs;

            if (_standTimer >= _currentStandDuration)
            {
                // Start moving
                IsMoving = true;
                _moveTimer = 0;
                _currentMoveDuration = _random.Next(MIN_MOVE_TIME, MAX_MOVE_TIME);

                // Choose direction based on position within range
                if (X <= RX0 + 10)
                {
                    FlipX = false;  // Move right
                }
                else if (X >= RX1 - 10)
                {
                    FlipX = true;  // Move left
                }
                else
                {
                    // Random direction
                    FlipX = _random.Next(2) == 0;
                }
            }
        }

        /// <summary>
        /// Update while walking
        /// </summary>
        private void UpdateWalking(int deltaTimeMs)
        {
            _moveTimer += deltaTimeMs;

            // Calculate movement
            float moveAmount = MoveSpeed * deltaTimeMs / 16.67f;  // Normalize to ~60fps

            if (FlipX)
            {
                // Moving left
                X -= moveAmount;
                if (X <= RX0)
                {
                    X = RX0;
                    FlipX = false;  // Turn around
                }
            }
            else
            {
                // Moving right
                X += moveAmount;
                if (X >= RX1)
                {
                    X = RX1;
                    FlipX = true;  // Turn around
                }
            }

            // Check if move duration expired
            if (_moveTimer >= _currentMoveDuration)
            {
                // Stop and stand
                IsMoving = false;
                _standTimer = 0;
                _currentStandDuration = _random.Next(MIN_STAND_TIME, MAX_STAND_TIME);
            }
        }
    }
}
