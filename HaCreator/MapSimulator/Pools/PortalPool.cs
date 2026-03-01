using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapEditor.Instance;
using MapleLib.WzLib.WzStructure.Data;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Pools
{
    /// <summary>
    /// Hidden portal state tracking
    /// </summary>
    public class HiddenPortalState
    {
        public int PortalIndex { get; set; }
        public string PortalName { get; set; }
        public bool IsRevealed { get; set; }
        public int RevealedTime { get; set; }
        public float Alpha { get; set; }
        public float TargetAlpha { get; set; }
    }

    /// <summary>
    /// Portal properties for collision and effects
    /// </summary>
    public struct PortalProperties
    {
        /// <summary>Height property (PH) - typically the vertical detection range</summary>
        public int Height;
        /// <summary>Special height property (PSH) - for special portal effects</summary>
        public int SpecialHeight;
        /// <summary>Vertical property (PV) - vertical impact/velocity</summary>
        public int Vertical;
        /// <summary>Horizontal range for detection</summary>
        public int HorizontalRange;
        /// <summary>Vertical range for detection</summary>
        public int VerticalRange;
        /// <summary>Horizontal impact/velocity</summary>
        public int HorizontalImpact;
        /// <summary>Vertical impact/velocity</summary>
        public int VerticalImpact;
    }

    /// <summary>
    /// Portal Pool System - Manages portals including hidden portal discovery
    /// Based on CPortalList from MapleStory client
    /// </summary>
    public class PortalPool
    {
        #region Constants
        private const float HIDDEN_PORTAL_REVEAL_RANGE = 50f;    // Range at which hidden portals reveal
        private const float HIDDEN_PORTAL_FADE_SPEED = 3f;       // Alpha fade per second
        private const int HIDDEN_PORTAL_REVEAL_DURATION = 2000;  // How long portal stays revealed after leaving range
        private const float PORTAL_DEFAULT_DETECTION_RANGE = 40f; // Default collision detection range
        #endregion

        #region Collections
        private PortalItem[] _portals;
        private readonly Dictionary<int, HiddenPortalState> _hiddenPortalStates = new Dictionary<int, HiddenPortalState>();
        private readonly Dictionary<string, int> _portalNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region State
        private int _lastUpdateTick = 0;
        private Action<PortalItem, int> _onHiddenPortalRevealed;
        private Action<PortalItem, int> _onHiddenPortalHidden;
        private Action<PortalItem> _onPortalTriggered;
        #endregion

        #region Public Properties
        public int PortalCount => _portals?.Length ?? 0;
        public IReadOnlyList<PortalItem> Portals => _portals;
        #endregion

        #region Events
        public void SetOnHiddenPortalRevealed(Action<PortalItem, int> callback) => _onHiddenPortalRevealed = callback;
        public void SetOnHiddenPortalHidden(Action<PortalItem, int> callback) => _onHiddenPortalHidden = callback;
        public void SetOnPortalTriggered(Action<PortalItem> callback) => _onPortalTriggered = callback;
        #endregion

        #region Initialization
        /// <summary>
        /// Initialize the portal pool with portals from map load
        /// </summary>
        public void Initialize(PortalItem[] portals)
        {
            Clear();

            if (portals == null || portals.Length == 0)
            {
                _portals = Array.Empty<PortalItem>();
                return;
            }

            _portals = portals;

            // Build name lookup and identify hidden portals
            for (int i = 0; i < _portals.Length; i++)
            {
                var portal = _portals[i];
                if (portal?.PortalInstance == null)
                    continue;

                var instance = portal.PortalInstance;

                // Add to name lookup
                if (!string.IsNullOrEmpty(instance.pn))
                {
                    _portalNameToIndex[instance.pn] = i;
                }

                // Initialize hidden portal state
                if (IsHiddenPortalType(instance.pt))
                {
                    _hiddenPortalStates[i] = new HiddenPortalState
                    {
                        PortalIndex = i,
                        PortalName = instance.pn,
                        IsRevealed = false,
                        RevealedTime = 0,
                        Alpha = 0f,
                        TargetAlpha = 0f
                    };
                }
            }
        }

        /// <summary>
        /// Check if a portal type is a hidden type
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHiddenPortalType(PortalType pt)
        {
            return pt == PortalType.Hidden ||
                   pt == PortalType.ScriptHidden ||
                   pt == PortalType.ScriptHiddenUng;
        }

        public void Clear()
        {
            _portals = null;
            _hiddenPortalStates.Clear();
            _portalNameToIndex.Clear();
        }
        #endregion

        #region Portal Lookup
        /// <summary>
        /// Get portal by index
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PortalItem GetPortal(int index)
        {
            if (_portals == null || index < 0 || index >= _portals.Length)
                return null;
            return _portals[index];
        }

        /// <summary>
        /// Get portal by name (pn)
        /// </summary>
        public PortalItem GetPortalByName(string name)
        {
            if (string.IsNullOrEmpty(name) || !_portalNameToIndex.TryGetValue(name, out int index))
                return null;
            return GetPortal(index);
        }

        /// <summary>
        /// Get portal index by name
        /// </summary>
        public int GetPortalIndexByName(string name)
        {
            if (string.IsNullOrEmpty(name) || !_portalNameToIndex.TryGetValue(name, out int index))
                return -1;
            return index;
        }

        /// <summary>
        /// Find start point portal (spawn portal)
        /// </summary>
        public PortalItem FindStartPoint()
        {
            if (_portals == null)
                return null;

            foreach (var portal in _portals)
            {
                if (portal?.PortalInstance?.pt == PortalType.StartPoint)
                    return portal;
            }

            // Fallback to portal named "sp" or first portal
            var spPortal = GetPortalByName("sp");
            if (spPortal != null)
                return spPortal;

            return _portals.Length > 0 ? _portals[0] : null;
        }

        /// <summary>
        /// Find portal at position
        /// </summary>
        public PortalItem FindPortalAtPosition(float x, float y, float range = 40f)
        {
            if (_portals == null)
                return null;

            float rangeSq = range * range;

            foreach (var portal in _portals)
            {
                if (portal?.PortalInstance == null)
                    continue;

                float dx = portal.PortalInstance.X - x;
                float dy = portal.PortalInstance.Y - y;

                if (dx * dx + dy * dy <= rangeSq)
                    return portal;
            }

            return null;
        }

        /// <summary>
        /// Get all portals within a range
        /// </summary>
        public IEnumerable<PortalItem> GetPortalsInRange(float x, float y, float range)
        {
            if (_portals == null)
                yield break;

            float rangeSq = range * range;

            foreach (var portal in _portals)
            {
                if (portal?.PortalInstance == null)
                    continue;

                float dx = portal.PortalInstance.X - x;
                float dy = portal.PortalInstance.Y - y;

                if (dx * dx + dy * dy <= rangeSq)
                    yield return portal;
            }
        }
        #endregion

        #region Hidden Portal System (CPortalList functions)
        /// <summary>
        /// Find hidden portals near player position.
        /// Based on CPortalList::FindPortal_Hidden from MapleStory client.
        /// </summary>
        /// <param name="playerX">Player X position</param>
        /// <param name="playerY">Player Y position</param>
        /// <param name="range">Detection range (default: HIDDEN_PORTAL_REVEAL_RANGE)</param>
        /// <returns>List of hidden portals in range that should be revealed</returns>
        public List<(PortalItem portal, int index)> FindPortal_Hidden(float playerX, float playerY, float range = 0)
        {
            if (range <= 0)
                range = HIDDEN_PORTAL_REVEAL_RANGE;

            var results = new List<(PortalItem portal, int index)>();
            float rangeSq = range * range;

            foreach (var kvp in _hiddenPortalStates)
            {
                int index = kvp.Key;
                var portal = GetPortal(index);

                if (portal?.PortalInstance == null)
                    continue;

                float dx = portal.PortalInstance.X - playerX;
                float dy = portal.PortalInstance.Y - playerY;

                if (dx * dx + dy * dy <= rangeSq)
                {
                    results.Add((portal, index));
                }
            }

            return results;
        }

        /// <summary>
        /// Update hidden portal visibility state based on player proximity.
        /// Based on CPortalList::UpdateHiddenPortal from MapleStory client.
        /// </summary>
        /// <param name="playerX">Player X position</param>
        /// <param name="playerY">Player Y position</param>
        /// <param name="currentTick">Current game tick</param>
        /// <param name="deltaTime">Delta time in seconds</param>
        public void UpdateHiddenPortal(float playerX, float playerY, int currentTick, float deltaTime)
        {
            _lastUpdateTick = currentTick;
            float rangeSq = HIDDEN_PORTAL_REVEAL_RANGE * HIDDEN_PORTAL_REVEAL_RANGE;

            foreach (var kvp in _hiddenPortalStates)
            {
                int index = kvp.Key;
                var state = kvp.Value;
                var portal = GetPortal(index);

                if (portal?.PortalInstance == null)
                    continue;

                // Check if player is in range
                float dx = portal.PortalInstance.X - playerX;
                float dy = portal.PortalInstance.Y - playerY;
                bool inRange = (dx * dx + dy * dy) <= rangeSq;

                if (inRange)
                {
                    // Player is near - reveal portal
                    if (!state.IsRevealed)
                    {
                        state.IsRevealed = true;
                        _onHiddenPortalRevealed?.Invoke(portal, index);
                    }
                    state.RevealedTime = currentTick;
                    state.TargetAlpha = 1f;
                }
                else
                {
                    // Player moved away - check if should start hiding
                    if (state.IsRevealed)
                    {
                        int timeSinceInRange = currentTick - state.RevealedTime;
                        if (timeSinceInRange > HIDDEN_PORTAL_REVEAL_DURATION)
                        {
                            state.TargetAlpha = 0f;
                        }
                    }
                }

                // Animate alpha towards target
                if (state.Alpha < state.TargetAlpha)
                {
                    state.Alpha = Math.Min(state.Alpha + HIDDEN_PORTAL_FADE_SPEED * deltaTime, state.TargetAlpha);
                }
                else if (state.Alpha > state.TargetAlpha)
                {
                    state.Alpha = Math.Max(state.Alpha - HIDDEN_PORTAL_FADE_SPEED * deltaTime, state.TargetAlpha);

                    // Check if fully hidden
                    if (state.Alpha <= 0 && state.IsRevealed)
                    {
                        state.IsRevealed = false;
                        _onHiddenPortalHidden?.Invoke(portal, index);
                    }
                }
            }
        }

        /// <summary>
        /// Set a portal as hidden or revealed.
        /// Based on CPortalList::SetHiddenPortal from MapleStory client.
        /// </summary>
        /// <param name="portalIndex">Portal index</param>
        /// <param name="hidden">True to hide, false to reveal</param>
        /// <param name="currentTick">Current game tick</param>
        public void SetHiddenPortal(int portalIndex, bool hidden, int currentTick)
        {
            if (!_hiddenPortalStates.TryGetValue(portalIndex, out var state))
                return;

            var portal = GetPortal(portalIndex);

            if (hidden)
            {
                state.TargetAlpha = 0f;
                // If forcing hidden, do it immediately
                if (state.IsRevealed)
                {
                    state.IsRevealed = false;
                    state.Alpha = 0f;
                    _onHiddenPortalHidden?.Invoke(portal, portalIndex);
                }
            }
            else
            {
                state.TargetAlpha = 1f;
                state.RevealedTime = currentTick;
                if (!state.IsRevealed)
                {
                    state.IsRevealed = true;
                    _onHiddenPortalRevealed?.Invoke(portal, portalIndex);
                }
            }
        }

        /// <summary>
        /// Set hidden portal state by name
        /// </summary>
        public void SetHiddenPortal(string portalName, bool hidden, int currentTick)
        {
            int index = GetPortalIndexByName(portalName);
            if (index >= 0)
            {
                SetHiddenPortal(index, hidden, currentTick);
            }
        }

        /// <summary>
        /// Get hidden portal alpha for rendering
        /// </summary>
        public float GetHiddenPortalAlpha(int portalIndex)
        {
            if (_hiddenPortalStates.TryGetValue(portalIndex, out var state))
                return state.Alpha;
            return 1f; // Non-hidden portals are fully visible
        }

        /// <summary>
        /// Check if a portal is currently revealed
        /// </summary>
        public bool IsHiddenPortalRevealed(int portalIndex)
        {
            if (_hiddenPortalStates.TryGetValue(portalIndex, out var state))
                return state.IsRevealed;
            return true; // Non-hidden portals are always "revealed"
        }

        /// <summary>
        /// Check if a portal is a hidden type
        /// </summary>
        public bool IsHiddenPortal(int portalIndex)
        {
            return _hiddenPortalStates.ContainsKey(portalIndex);
        }
        #endregion

        #region Portal Properties (CPortalList functions)
        /// <summary>
        /// Get portal height property (PH).
        /// Based on CPortalList::GetPropPH from MapleStory client.
        /// Height typically refers to the vertical detection range or portal height.
        /// </summary>
        /// <param name="portalIndex">Portal index</param>
        /// <returns>Height property value, or default if not set</returns>
        public int GetPropPH(int portalIndex)
        {
            var portal = GetPortal(portalIndex);
            if (portal?.PortalInstance == null)
                return 0;

            // In MapleStory, PH is typically the vRange (vertical range)
            return portal.PortalInstance.vRange ?? 30; // Default portal height
        }

        /// <summary>
        /// Get portal special height property (PSH).
        /// Based on CPortalList::GetPropPSH from MapleStory client.
        /// Special height is used for certain portal effects like spring jumps.
        /// </summary>
        /// <param name="portalIndex">Portal index</param>
        /// <returns>Special height property value</returns>
        public int GetPropPSH(int portalIndex)
        {
            var portal = GetPortal(portalIndex);
            if (portal?.PortalInstance == null)
                return 0;

            // PSH is typically related to vertical impact for jump portals
            // Used in PORTALTYPE_COLLISION_CUSTOM_IMPACT portals
            return portal.PortalInstance.verticalImpact ?? 0;
        }

        /// <summary>
        /// Get portal vertical property (PV).
        /// Based on CPortalList::GetPropPV from MapleStory client.
        /// Vertical property is the vertical impact/velocity applied when using portal.
        /// </summary>
        /// <param name="portalIndex">Portal index</param>
        /// <returns>Vertical property value</returns>
        public int GetPropPV(int portalIndex)
        {
            var portal = GetPortal(portalIndex);
            if (portal?.PortalInstance == null)
                return 0;

            // PV is vertical impact - negative means upward velocity (jump)
            return portal.PortalInstance.verticalImpact ?? 0;
        }

        /// <summary>
        /// Get all portal properties at once
        /// </summary>
        public PortalProperties GetPortalProperties(int portalIndex)
        {
            var portal = GetPortal(portalIndex);
            if (portal?.PortalInstance == null)
                return default;

            var instance = portal.PortalInstance;
            return new PortalProperties
            {
                Height = instance.vRange ?? 30,
                SpecialHeight = instance.verticalImpact ?? 0,
                Vertical = instance.verticalImpact ?? 0,
                HorizontalRange = instance.hRange ?? 40,
                VerticalRange = instance.vRange ?? 30,
                HorizontalImpact = instance.horizontalImpact ?? 0,
                VerticalImpact = instance.verticalImpact ?? 0
            };
        }

        /// <summary>
        /// Get portal properties by name
        /// </summary>
        public PortalProperties GetPortalProperties(string portalName)
        {
            int index = GetPortalIndexByName(portalName);
            if (index < 0)
                return default;
            return GetPortalProperties(index);
        }
        #endregion

        #region Portal Collision Detection
        /// <summary>
        /// Check if player is colliding with a portal
        /// </summary>
        /// <param name="playerX">Player X position</param>
        /// <param name="playerY">Player Y position</param>
        /// <param name="playerHeight">Player height (default 60)</param>
        /// <returns>Colliding portal, or null</returns>
        public PortalItem CheckPortalCollision(float playerX, float playerY, int playerHeight = 60)
        {
            if (_portals == null)
                return null;

            for (int i = 0; i < _portals.Length; i++)
            {
                var portal = _portals[i];
                if (portal?.PortalInstance == null)
                    continue;

                var instance = portal.PortalInstance;
                var pt = instance.pt;

                // Skip non-collision portals for automatic triggering
                if (pt == PortalType.StartPoint ||
                    pt == PortalType.TownPortalPoint ||
                    pt == PortalType.Invisible ||
                    pt == PortalType.ScriptInvisible)
                    continue;

                // For hidden portals, check if revealed
                if (IsHiddenPortalType(pt) && !IsHiddenPortalRevealed(i))
                    continue;

                // Get detection range
                int hRange = instance.hRange ?? 40;
                int vRange = instance.vRange ?? 30;

                // Check collision with player hitbox
                float portalX = instance.X;
                float portalY = instance.Y;

                // Player hitbox is typically centered on X, with Y at feet
                bool xCollide = Math.Abs(playerX - portalX) <= hRange;
                bool yCollide = (playerY - playerHeight) <= (portalY + vRange) && playerY >= (portalY - vRange);

                if (xCollide && yCollide)
                {
                    return portal;
                }
            }

            return null;
        }

        /// <summary>
        /// Check collision with specific portal types (like collision jump portals)
        /// </summary>
        public PortalItem CheckCollisionPortal(float playerX, float playerY, int playerHeight = 60)
        {
            if (_portals == null)
                return null;

            for (int i = 0; i < _portals.Length; i++)
            {
                var portal = _portals[i];
                if (portal?.PortalInstance == null)
                    continue;

                var pt = portal.PortalInstance.pt;

                // Only check collision-type portals
                if (pt != PortalType.Collision &&
                    pt != PortalType.CollisionScript &&
                    pt != PortalType.CollisionVerticalJump &&
                    pt != PortalType.CollisionCustomImpact &&
                    pt != PortalType.CollisionCustomImpact2 &&
                    pt != PortalType.CollisionUnknownPcig)
                    continue;

                var instance = portal.PortalInstance;
                int hRange = instance.hRange ?? 30;
                int vRange = instance.vRange ?? 20;

                float dx = Math.Abs(playerX - instance.X);
                float dy = Math.Abs(playerY - instance.Y);

                if (dx <= hRange && dy <= vRange)
                    return portal;
            }

            return null;
        }

        /// <summary>
        /// Trigger a portal (invoke callback)
        /// </summary>
        public void TriggerPortal(PortalItem portal)
        {
            if (portal == null)
                return;
            _onPortalTriggered?.Invoke(portal);
        }
        #endregion

        #region Update
        /// <summary>
        /// Update portal pool
        /// </summary>
        public void Update(float playerX, float playerY, int currentTick, float deltaTime)
        {
            // Update hidden portal visibility
            UpdateHiddenPortal(playerX, playerY, currentTick, deltaTime);
        }
        #endregion

        #region Statistics
        public PortalPoolStats GetStats()
        {
            int visibleCount = 0;
            int hiddenCount = _hiddenPortalStates.Count;
            int revealedCount = 0;

            if (_portals != null)
            {
                foreach (var portal in _portals)
                {
                    if (portal?.PortalInstance?.pt == PortalType.Visible ||
                        portal?.PortalInstance?.pt == PortalType.Default)
                        visibleCount++;
                }
            }

            foreach (var state in _hiddenPortalStates.Values)
            {
                if (state.IsRevealed)
                    revealedCount++;
            }

            return new PortalPoolStats
            {
                TotalPortals = _portals?.Length ?? 0,
                VisiblePortals = visibleCount,
                HiddenPortals = hiddenCount,
                RevealedHiddenPortals = revealedCount
            };
        }
        #endregion
    }

    /// <summary>
    /// Statistics about the portal pool
    /// </summary>
    public struct PortalPoolStats
    {
        public int TotalPortals;
        public int VisiblePortals;
        public int HiddenPortals;
        public int RevealedHiddenPortals;
    }
}
