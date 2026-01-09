namespace HaCreator.MapSimulator.Core
{
    /// <summary>
    /// Centralized game constants for the MapSimulator.
    /// Contains timing, rendering, and gameplay constants that were previously scattered across files.
    /// </summary>
    public static class GameConstants
    {
        #region Spatial Partitioning

        /// <summary>
        /// Cell size for spatial grid partitioning (pixels)
        /// </summary>
        public const int SPATIAL_GRID_CELL_SIZE = 512;

        /// <summary>
        /// Minimum object count before enabling spatial partitioning
        /// </summary>
        public const int SPATIAL_PARTITIONING_THRESHOLD = 100;

        #endregion

        #region Map Boundaries

        /// <summary>
        /// Width/height of the VR (Visual Range) border
        /// </summary>
        public const int VR_BORDER_SIZE = 600;

        /// <summary>
        /// Additional width/height of LB (Logical Boundary) outside VR
        /// </summary>
        public const int LB_BORDER_SIZE = 300;

        /// <summary>
        /// Height of the UI menu bar at the bottom of the screen
        /// </summary>
        public const int LB_BORDER_UI_HEIGHT = 62;

        /// <summary>
        /// Offset from map edge for LB border
        /// </summary>
        public const int LB_BORDER_OFFSET_X = 150;

        #endregion

        #region Input Timing

        /// <summary>
        /// Time window for double-click detection (ms)
        /// </summary>
        public const int DOUBLE_CLICK_TIME_MS = 500;

        /// <summary>
        /// Initial delay before key repeat starts (ms)
        /// </summary>
        public const int KEY_REPEAT_DELAY = 500;

        /// <summary>
        /// Rate of key repeat after initial delay (ms)
        /// </summary>
        public const int KEY_REPEAT_RATE = 50;

        /// <summary>
        /// Cooldown for UI toggle buttons (ms)
        /// </summary>
        public const int UI_TOGGLE_COOLDOWN_MS = 200;

        #endregion

        #region Entity Dimensions

        /// <summary>
        /// Default player hitbox width (pixels)
        /// </summary>
        public const int PLAYER_HITBOX_WIDTH = 30;

        /// <summary>
        /// Default player hitbox height (pixels)
        /// </summary>
        public const int PLAYER_HITBOX_HEIGHT = 60;

        /// <summary>
        /// Player hitbox Y offset from feet (negative = above feet)
        /// </summary>
        public const int PLAYER_HITBOX_OFFSET_Y = -60;

        /// <summary>
        /// Threshold distance before recalculating mirror boundaries (pixels)
        /// </summary>
        public const int MIRROR_CHECK_THRESHOLD = 50;

        #endregion

        #region Combat Timing

        /// <summary>
        /// Minimum delay between player attacks (ms)
        /// </summary>
        public const int MIN_ATTACK_DELAY = 300;

        /// <summary>
        /// Duration of invincibility after being hit (ms)
        /// </summary>
        public const int INVINCIBILITY_DURATION = 2000;

        /// <summary>
        /// Duration of stun when mob is hit (ms)
        /// </summary>
        public const int HIT_STUN_DURATION = 300;

        #endregion

        #region Respawn/Death Timing

        /// <summary>
        /// Default mob respawn time (ms)
        /// </summary>
        public const int DEFAULT_RESPAWN_TIME = 7000;

        /// <summary>
        /// Death animation display time (ms)
        /// </summary>
        public const int DEATH_ANIMATION_TIME = 2000;

        /// <summary>
        /// Death effect animation duration (ms)
        /// </summary>
        public const int DEATH_DURATION = 1000;

        #endregion

        #region AI Constants

        /// <summary>
        /// Default aggro range for mobs (pixels)
        /// </summary>
        public const int DEFAULT_AGGRO_RANGE = 200;

        /// <summary>
        /// Default melee attack range (pixels)
        /// </summary>
        public const int DEFAULT_ATTACK_RANGE = 50;

        /// <summary>
        /// Time in alert state before chasing (ms)
        /// </summary>
        public const int ALERT_DURATION = 500;

        /// <summary>
        /// Default attack cooldown (ms)
        /// </summary>
        public const int ATTACK_COOLDOWN = 1500;

        /// <summary>
        /// Time to lose aggro if no line of sight (ms)
        /// </summary>
        public const int LOSE_AGGRO_TIME = 5000;

        /// <summary>
        /// Time after which boss monsters stop using aggro if player remains in map (ms).
        /// Boss mobs will become passive after this duration, even if the player is dead.
        /// </summary>
        public const int BOSS_AGGRO_TIMEOUT = 3600000; // 1 hour

        #endregion

        #region Drop Constants

        /// <summary>
        /// Default drop lifetime before expiration (ms)
        /// </summary>
        public const int DROP_LIFETIME = 120000; // 2 minutes

        /// <summary>
        /// Duration of owner priority on drops (ms)
        /// </summary>
        public const int DROP_OWNER_PRIORITY = 15000; // 15 seconds

        /// <summary>
        /// Horizontal spread of multiple drops (pixels)
        /// </summary>
        public const float DROP_SPREAD = 30f;

        /// <summary>
        /// Initial upward velocity for drops
        /// </summary>
        public const float DROP_INITIAL_VELOCITY = -200f;

        /// <summary>
        /// Maximum active drops
        /// </summary>
        public const int MAX_DROPS = 200;

        #endregion

        #region UI Dimensions

        /// <summary>
        /// Standard slot size for inventory/equipment UI (pixels)
        /// </summary>
        public const int UI_SLOT_SIZE = 32;

        /// <summary>
        /// Standard slot padding (pixels)
        /// </summary>
        public const int UI_SLOT_PADDING = 4;

        /// <summary>
        /// Skill icon size (pixels)
        /// </summary>
        public const int SKILL_ICON_SIZE = 32;

        #endregion

        #region Particle/Effect Limits

        /// <summary>
        /// Maximum particles in the system
        /// </summary>
        public const int MAX_PARTICLES = 2000;

        /// <summary>
        /// Maximum damage number displays
        /// </summary>
        public const int MAX_DAMAGE_DISPLAYS = 50;

        /// <summary>
        /// Maximum mob HP bars visible
        /// </summary>
        public const int MAX_MOB_HP_BARS = 100;

        #endregion

        #region Chat Constants

        /// <summary>
        /// Maximum chat input length
        /// </summary>
        public const int CHAT_MAX_INPUT_LENGTH = 100;

        /// <summary>
        /// Maximum stored chat messages
        /// </summary>
        public const int CHAT_MAX_MESSAGES = 50;

        /// <summary>
        /// Chat message display time before fade (ms)
        /// </summary>
        public const int CHAT_MESSAGE_DISPLAY_TIME = 10000;

        #endregion

        #region Texture Pool

        /// <summary>
        /// Default texture TTL before cleanup (seconds)
        /// </summary>
        public const int TEXTURE_TTL_SECONDS = 300;

        /// <summary>
        /// Texture cleanup interval (seconds)
        /// </summary>
        public const int TEXTURE_CLEANUP_INTERVAL = 30;

        /// <summary>
        /// Maximum textures before forced cleanup
        /// </summary>
        public const int MAX_TEXTURES_BEFORE_CLEANUP = 500;

        #endregion

        #region Movement Constants

        /// <summary>
        /// GM fly speed for map exploration (pixels/second)
        /// </summary>
        public const float GM_FLY_SPEED = 400f;

        /// <summary>
        /// Speed multiplier when chasing player
        /// </summary>
        public const int CHASE_SPEED_MULTIPLIER = 2;

        #endregion
    }
}
