using HaSharedLibrary.Render.DX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Base class for animation frame storage used by entities (Mobs, NPCs, etc.).
    /// Provides common functionality for storing and retrieving animation frames by action name.
    ///
    /// <para><b>Animation Lookup System:</b></para>
    /// <para>
    /// When GetFrames(action) is called, the lookup follows this priority:
    /// <list type="number">
    ///   <item>Exact match for the requested action</item>
    ///   <item>Subclass-specific fallback logic (via TryGetFallbackFrames)</item>
    ///   <item>Default "stand" animation</item>
    ///   <item>Any available animation (last resort)</item>
    /// </list>
    /// </para>
    /// </summary>
    public abstract class AnimationSetBase
    {
        protected readonly Dictionary<string, List<IDXObject>> _animations = new();
        protected readonly List<string> _actionList = new();
        protected string _defaultAction = "stand";

        #region Core Methods

        /// <summary>
        /// Add frames for a specific action
        /// </summary>
        /// <param name="action">Action name (e.g., "stand", "move", "attack1")</param>
        /// <param name="frames">Animation frames for this action</param>
        public virtual void AddAnimation(string action, List<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
                return;

            string key = action.ToLower();
            _animations[key] = frames;

            if (!_actionList.Contains(key))
            {
                _actionList.Add(key);
            }
        }

        /// <summary>
        /// Get frames for a specific action with fallback logic
        /// </summary>
        /// <param name="action">Action name</param>
        /// <returns>List of frames, or null if no animation available</returns>
        public List<IDXObject> GetFrames(string action)
        {
            string key = action?.ToLower() ?? _defaultAction;

            // Try exact match
            if (_animations.TryGetValue(key, out var frames))
                return frames;

            // Try subclass-specific fallback
            if (TryGetFallbackFrames(key, out frames))
                return frames;

            // Fall back to default action
            if (key != _defaultAction && _animations.TryGetValue(_defaultAction, out frames))
                return frames;

            // Last resort - return any available animation
            return _animations.Values.FirstOrDefault();
        }

        /// <summary>
        /// Override in derived classes to provide action-specific fallback logic.
        /// For example, Mob might try "walk" when "move" is requested.
        /// </summary>
        /// <param name="requestedAction">The action that was not found</param>
        /// <param name="frames">Output frames if fallback found</param>
        /// <returns>True if a fallback was found</returns>
        protected virtual bool TryGetFallbackFrames(string requestedAction, out List<IDXObject> frames)
        {
            frames = null;
            return false;
        }

        /// <summary>
        /// Check if an action animation exists
        /// </summary>
        public bool HasAnimation(string action)
        {
            return _animations.ContainsKey(action?.ToLower() ?? "");
        }

        /// <summary>
        /// Get all available action names
        /// </summary>
        public IEnumerable<string> GetAvailableActions()
        {
            return _actionList;
        }

        /// <summary>
        /// Get all available action names as a list (for indexed access)
        /// </summary>
        public IReadOnlyList<string> GetAvailableActionsList()
        {
            return _actionList;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Get or set the default action (usually "stand")
        /// </summary>
        public string DefaultAction
        {
            get => _defaultAction;
            set => _defaultAction = value?.ToLower() ?? "stand";
        }

        /// <summary>
        /// Total number of actions available
        /// </summary>
        public int ActionCount => _animations.Count;

        /// <summary>
        /// Get total frame count across all animations
        /// </summary>
        public int TotalFrameCount => _animations.Values.Sum(frames => frames.Count);

        /// <summary>
        /// Whether this entity can walk/move based on available animations
        /// </summary>
        public bool CanWalk => _animations.ContainsKey("move") || _animations.ContainsKey("walk");

        #endregion

        #region Random Action Support

        /// <summary>
        /// Get a random action name from available actions
        /// </summary>
        /// <param name="random">Random instance to use</param>
        /// <returns>Random action name, or default if none available</returns>
        public string GetRandomAction(Random random)
        {
            if (_actionList.Count == 0)
                return _defaultAction;

            int index = random.Next(_actionList.Count);
            return _actionList[index];
        }

        #endregion
    }
}
