using HaSharedLibrary.Render.DX;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Objects.FieldObject
{
    /// <summary>
    /// Stores animation frames for different mob actions (stand, move, fly, etc.)
    /// Used by MobItem to play appropriate animations based on movement state.
    ///
    /// <para><b>Animation Lookup System:</b></para>
    /// <para>
    /// When GetFrames(action) is called, the lookup follows this priority:
    /// <list type="number">
    ///   <item>Exact match for the requested action (e.g., "move" → "move")</item>
    ///   <item>Fallback aliases (e.g., "move" → "walk", or "walk" → "move")</item>
    ///   <item>Default "stand" animation</item>
    ///   <item>Any available animation (last resort)</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Common Mob Actions:</b></para>
    /// <list type="bullet">
    ///   <item><b>stand</b>: Idle animation when stationary</item>
    ///   <item><b>move/walk</b>: Walking animation for ground movement</item>
    ///   <item><b>fly</b>: Flying animation for airborne mobs</item>
    ///   <item><b>jump</b>: Jump animation (used during jump physics)</item>
    ///   <item><b>hit1</b>: Damage taken animation</item>
    ///   <item><b>die1</b>: Death animation</item>
    ///   <item><b>attack1</b>: Attack animation</item>
    /// </list>
    ///
    /// <para><b>Movement Type Detection:</b></para>
    /// <para>
    /// MobMovementInfo uses the CanFly, CanMove, and CanJump properties to determine
    /// what movement type to use:
    /// <list type="bullet">
    ///   <item>CanFly=true → MobMoveType.Fly (floating movement, vertical bobbing)</item>
    ///   <item>CanJump=true → MobMoveType.Jump (ground + periodic jumps)</item>
    ///   <item>CanMove=true → MobMoveType.Move (ground walking along footholds)</item>
    ///   <item>None → MobMoveType.Stand (stationary)</item>
    /// </list>
    /// </para>
    /// </summary>
    public class MobAnimationSet
    {
        private readonly Dictionary<string, List<IDXObject>> _animations = new();
        private string _defaultAction = "stand";

        /// <summary>
        /// Add frames for a specific action
        /// </summary>
        /// <param name="action">Action name (e.g., "stand", "move", "fly")</param>
        /// <param name="frames">Animation frames for this action</param>
        public void AddAnimation(string action, List<IDXObject> frames)
        {
            if (frames != null && frames.Count > 0)
            {
                _animations[action.ToLower()] = frames;
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

            // Try fallback actions
            if (key == "move" || key == "walk")
            {
                // Try "move" first, then "walk"
                if (_animations.TryGetValue("move", out frames))
                    return frames;
                if (_animations.TryGetValue("walk", out frames))
                    return frames;
            }

            if (key == "fly")
            {
                if (_animations.TryGetValue("fly", out frames))
                    return frames;
            }

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
        /// Get all available action names
        /// </summary>
        public IEnumerable<string> GetAvailableActions()
        {
            return _animations.Keys;
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
        /// Determine if this mob can fly based on available animations
        /// </summary>
        public bool CanFly => _animations.ContainsKey("fly");

        /// <summary>
        /// Determine if this mob can jump based on available animations
        /// </summary>
        public bool CanJump => _animations.ContainsKey("jump");

        /// <summary>
        /// Determine if this mob can move based on available animations
        /// </summary>
        public bool CanMove => _animations.ContainsKey("move") || _animations.ContainsKey("walk");
    }
}
