using HaSharedLibrary.Render.DX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HaCreator.MapSimulator.Objects.FieldObject
{
    /// <summary>
    /// Stores animation frames for different NPC actions (stand, speak, blink, etc.)
    /// Similar to MobAnimationSet but for NPCs.
    /// </summary>
    public class NpcAnimationSet
    {
        private readonly Dictionary<string, List<IDXObject>> _animations = new();
        private string _defaultAction = "stand";
        private List<string> _actionList = new();

        /// <summary>
        /// Add frames for a specific action
        /// </summary>
        /// <param name="action">Action name (e.g., "stand", "speak", "blink")</param>
        /// <param name="frames">Animation frames for this action</param>
        public void AddAnimation(string action, List<IDXObject> frames)
        {
            if (frames != null && frames.Count > 0)
            {
                string key = action.ToLower();
                _animations[key] = frames;
                if (!_actionList.Contains(key))
                {
                    _actionList.Add(key);
                }
            }
        }

        /// <summary>
        /// Get frames for a specific action
        /// </summary>
        /// <param name="action">Action name</param>
        /// <returns>List of frames, or default action frames if not found</returns>
        public List<IDXObject> GetFrames(string action)
        {
            string key = action?.ToLower() ?? _defaultAction;

            if (_animations.TryGetValue(key, out var frames))
                return frames;

            // Fall back to stand
            if (_animations.TryGetValue("stand", out frames))
                return frames;

            // Last resort - return any available animation
            foreach (var anim in _animations.Values)
            {
                return anim;
            }

            return null;
        }

        /// <summary>
        /// Check if an action animation exists
        /// </summary>
        public bool HasAnimation(string action)
        {
            return _animations.ContainsKey(action?.ToLower() ?? "");
        }

        /// <summary>
        /// Get all available action names as a list (for indexed access)
        /// </summary>
        public IReadOnlyList<string> GetAvailableActionsList()
        {
            return _actionList;
        }

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

        /// <summary>
        /// Get the default action (usually "stand")
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
        /// Determine if this NPC can walk based on available animations
        /// </summary>
        public bool CanWalk => _animations.ContainsKey("move") || _animations.ContainsKey("walk");
    }
}
