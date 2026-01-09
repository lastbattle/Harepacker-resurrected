using System;

namespace HaCreator.MapSimulator.Managers
{
    /// <summary>
    /// Manages game state flags and transitions for the MapSimulator.
    /// Centralizes state management that was previously scattered across MapSimulator.cs.
    /// </summary>
    public class GameStateManager
    {
        #region Map Transition State

        /// <summary>
        /// Flag to trigger map change in Update loop
        /// </summary>
        public bool PendingMapChange { get; set; } = false;

        /// <summary>
        /// Target map ID for pending change (-1 = none)
        /// </summary>
        public int PendingMapId { get; set; } = -1;

        /// <summary>
        /// Target portal name for pending change
        /// </summary>
        public string PendingPortalName { get; set; } = null;

        /// <summary>
        /// Tick count when portal interaction cooldown expires.
        /// Prevents immediate re-entry after fast map changes.
        /// </summary>
        public int PortalCooldownUntil { get; set; } = 0;

        /// <summary>
        /// Portal cooldown duration in milliseconds after map change
        /// </summary>
        private const int PORTAL_COOLDOWN_MS = 500;

        #endregion

        #region Map Type Flags

        /// <summary>
        /// True if the simulated map is the Login map
        /// </summary>
        public bool IsLoginMap { get; set; } = false;

        /// <summary>
        /// True if the simulated map is the Cash Shop preview
        /// </summary>
        public bool IsCashShopMap { get; set; } = false;

        /// <summary>
        /// Big-Bang update version flag (different rendering for pre/post-BB)
        /// </summary>
        public bool IsBigBangUpdate { get; set; } = true;

        /// <summary>
        /// Chaos update version flag
        /// </summary>
        public bool IsBigBang2Update { get; set; } = true;

        #endregion

        #region Camera/Control State

        /// <summary>
        /// True = player control mode, False = free camera mode
        /// </summary>
        public bool PlayerControlEnabled { get; set; } = true;

        /// <summary>
        /// Enable smooth camera scrolling
        /// </summary>
        public bool UseSmoothCamera { get; set; } = true;

        #endregion

        #region UI/Debug State

        /// <summary>
        /// Show debug overlays (F5 toggle)
        /// </summary>
        public bool ShowDebugMode { get; set; } = false;

        /// <summary>
        /// Hide UI elements (H toggle)
        /// </summary>
        public bool HideUIMode { get; set; } = false;

        #endregion

        #region Gameplay State

        /// <summary>
        /// Enable mob movement and AI (F6 toggle)
        /// </summary>
        public bool MobMovementEnabled { get; set; } = true;

        /// <summary>
        /// Current weather type
        /// </summary>
        public WeatherType CurrentWeather { get; set; } = WeatherType.None;

        /// <summary>
        /// Active weather particle emitter ID (-1 = none)
        /// </summary>
        public int ActiveWeatherEmitter { get; set; } = -1;

        #endregion

        #region Methods

        /// <summary>
        /// Request a map transition. The actual transition happens in MapSimulator.Update().
        /// </summary>
        /// <param name="mapId">Target map ID</param>
        /// <param name="portalName">Target portal name to spawn at</param>
        public void RequestMapChange(int mapId, string portalName = null)
        {
            PendingMapChange = true;
            PendingMapId = mapId;
            PendingPortalName = portalName;
        }

        /// <summary>
        /// Clear pending map change after it's been processed
        /// </summary>
        public void ClearPendingMapChange()
        {
            PendingMapChange = false;
            PendingMapId = -1;
            PendingPortalName = null;
        }

        /// <summary>
        /// Set portal interaction cooldown after map change.
        /// Prevents immediate re-entry when player is still holding Up key.
        /// </summary>
        /// <param name="currentTickCount">Current Environment.TickCount</param>
        public void SetPortalCooldown(int currentTickCount)
        {
            PortalCooldownUntil = currentTickCount + PORTAL_COOLDOWN_MS;
        }

        /// <summary>
        /// Check if portal interaction is on cooldown
        /// </summary>
        /// <param name="currentTickCount">Current Environment.TickCount</param>
        /// <returns>True if portal interaction should be blocked</returns>
        public bool IsPortalOnCooldown(int currentTickCount)
        {
            return currentTickCount < PortalCooldownUntil;
        }

        /// <summary>
        /// Reset all state to defaults (for map reload)
        /// </summary>
        public void Reset()
        {
            // Don't reset map type flags - those are set during map load
            // Don't reset version flags - those are determined by WZ data

            // Reset transition state
            ClearPendingMapChange();

            // Reset camera/control state
            PlayerControlEnabled = true;
            UseSmoothCamera = true;

            // Reset UI/debug state
            ShowDebugMode = false;
            HideUIMode = false;

            // Reset gameplay state
            MobMovementEnabled = true;
            CurrentWeather = WeatherType.None;
            ActiveWeatherEmitter = -1;
        }

        /// <summary>
        /// Toggle debug mode (F5)
        /// </summary>
        public void ToggleDebugMode()
        {
            ShowDebugMode = !ShowDebugMode;
        }

        /// <summary>
        /// Toggle UI visibility (H)
        /// </summary>
        public void ToggleHideUI()
        {
            HideUIMode = !HideUIMode;
        }

        /// <summary>
        /// Toggle mob movement (F6)
        /// </summary>
        public void ToggleMobMovement()
        {
            MobMovementEnabled = !MobMovementEnabled;
        }

        /// <summary>
        /// Toggle player control mode (Tab)
        /// </summary>
        public void TogglePlayerControl()
        {
            PlayerControlEnabled = !PlayerControlEnabled;
        }

        /// <summary>
        /// Toggle smooth camera (C)
        /// </summary>
        public void ToggleSmoothCamera()
        {
            UseSmoothCamera = !UseSmoothCamera;
        }

        #endregion
    }
}
