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
        /// Target portal index for pending change (-1 = none)
        /// </summary>
        public int PendingPortalIndex { get; set; } = -1;

        /// <summary>
        /// Target portal name for pending change
        /// </summary>
        public string PendingPortalName { get; set; } = null;

        /// <summary>
        /// Alternate target portal names for pending change when the primary handoff
        /// comes from WZ reciprocal or map-return fallback metadata.
        /// </summary>
        public string[] PendingPortalNameCandidates { get; set; } = Array.Empty<string>();

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
        /// True while locally inferred scripted UI or field owners hold direction mode.
        /// </summary>
        public bool ScriptedDirectionModeActive { get; private set; } = false;

        /// <summary>
        /// Tick count when a delayed scripted direction-mode release should occur.
        /// int.MinValue means no delayed release is pending.
        /// </summary>
        public int ScriptedDirectionModeReleaseAt { get; private set; } = int.MinValue;

        /// <summary>
        /// True while the packet-owned SetDirectionMode seam is holding direction mode directly.
        /// </summary>
        public bool PacketDirectionModeActive { get; private set; } = false;

        /// <summary>
        /// Tick count when a delayed packet-authored direction-mode release should occur.
        /// int.MinValue means no delayed release is pending.
        /// </summary>
        public int PacketDirectionModeReleaseAt { get; private set; } = int.MinValue;

        /// <summary>
        /// Combined direction-mode state visible to the rest of the simulator.
        /// </summary>
        public bool DirectionModeActive => ScriptedDirectionModeActive || PacketDirectionModeActive;

        /// <summary>
        /// Backward-compatible alias for the locally inferred release timer.
        /// </summary>
        public int DirectionModeReleaseAt => ScriptedDirectionModeReleaseAt;

        /// <summary>
        /// Mirrors the packet-authored CWvsContext stand-alone flag.
        /// </summary>
        public bool StandAloneModeActive { get; private set; } = false;

        /// <summary>
        /// True when local gameplay input should reach the player character.
        /// </summary>
        public bool IsPlayerInputEnabled => PlayerControlEnabled && !DirectionModeActive;

        /// <summary>
        /// True when later modeless owners should inherit direction-mode ownership from an
        /// active scripted owner, packet-owned direction mode, or the packet-authored
        /// CWvsContext stand-alone flag.
        /// </summary>
        public bool ShouldInheritDirectionModeOwner(
            bool npcInteractionVisible,
            bool scriptedOwnerActive,
            bool weddingDialogVisible,
            bool memoryGameVisible,
            bool tournamentMatchTableVisible,
            bool rockPaperScissorsVisible)
        {
            return npcInteractionVisible
                   || DirectionModeActive
                   || StandAloneModeActive
                   || scriptedOwnerActive
                   || weddingDialogVisible
                   || memoryGameVisible
                   || tournamentMatchTableVisible
                   || rockPaperScissorsVisible;
        }

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
        public void RequestMapChange(int mapId, string portalName = null, int portalIndex = -1, string[] portalNameCandidates = null)
        {
            PendingMapChange = true;
            PendingMapId = mapId;
            PendingPortalName = portalName;
            PendingPortalIndex = portalIndex;
            PendingPortalNameCandidates = portalNameCandidates ?? Array.Empty<string>();
        }

        /// <summary>
        /// Clear pending map change after it's been processed
        /// </summary>
        public void ClearPendingMapChange()
        {
            PendingMapChange = false;
            PendingMapId = -1;
            PendingPortalName = null;
            PendingPortalIndex = -1;
            PendingPortalNameCandidates = Array.Empty<string>();
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
            ScriptedDirectionModeActive = false;
            ScriptedDirectionModeReleaseAt = int.MinValue;
            PacketDirectionModeActive = false;
            PacketDirectionModeReleaseAt = int.MinValue;
            StandAloneModeActive = false;
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
        /// Enter direction mode immediately and cancel any pending delayed release.
        /// </summary>
        public void EnterDirectionMode()
        {
            ScriptedDirectionModeActive = true;
            ScriptedDirectionModeReleaseAt = int.MinValue;
        }

        /// <summary>
        /// Schedule direction mode to end after a delay, mirroring the client's deferred release.
        /// </summary>
        public void RequestLeaveDirectionMode(int currentTickCount, int delayMs)
        {
            if (!ScriptedDirectionModeActive)
            {
                ScriptedDirectionModeReleaseAt = int.MinValue;
                return;
            }

            ScriptedDirectionModeReleaseAt = currentTickCount + Math.Max(0, delayMs);
        }

        /// <summary>
        /// Immediately clear direction mode and any pending delayed release.
        /// </summary>
        public void ExitDirectionModeImmediate()
        {
            ScriptedDirectionModeActive = false;
            ScriptedDirectionModeReleaseAt = int.MinValue;
        }

        /// <summary>
        /// Apply the client-shaped packet-owned direction-mode write.
        /// </summary>
        public void SetPacketDirectionMode(bool enabled, int currentTickCount, int delayMs)
        {
            PacketDirectionModeReleaseAt = int.MinValue;

            if (enabled || delayMs <= 0)
            {
                PacketDirectionModeActive = enabled;
                return;
            }

            PacketDirectionModeReleaseAt = currentTickCount + Math.Max(0, delayMs);
        }

        /// <summary>
        /// Apply the packet-authored CWvsContext stand-alone flag.
        /// </summary>
        public void SetStandAloneMode(bool enabled)
        {
            StandAloneModeActive = enabled;
        }

        /// <summary>
        /// Advance delayed direction-mode release timers.
        /// </summary>
        public void UpdateDirectionMode(int currentTickCount)
        {
            if (PacketDirectionModeReleaseAt != int.MinValue
                && unchecked(currentTickCount - PacketDirectionModeReleaseAt) >= 0)
            {
                PacketDirectionModeActive = false;
                PacketDirectionModeReleaseAt = int.MinValue;
            }

            if (!ScriptedDirectionModeActive || ScriptedDirectionModeReleaseAt == int.MinValue)
            {
                return;
            }

            if (unchecked(currentTickCount - ScriptedDirectionModeReleaseAt) >= 0)
            {
                ExitDirectionModeImmediate();
            }
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
