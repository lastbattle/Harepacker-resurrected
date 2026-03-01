using HaSharedLibrary.Render.DX;
using System.Collections.Generic;

namespace HaCreator.MapSimulator.Animation
{
    /// <summary>
    /// Stores animation frames for different mob actions (stand, move, fly, etc.)
    /// Used by MobItem to play appropriate animations based on movement state.
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
    public class MobAnimationSet : AnimationSetBase
    {
        // Hit effect frames for each attack (attack1/info/hit, attack2/info/hit, etc.)
        private readonly Dictionary<string, List<IDXObject>> _attackHitEffects = new();

        /// <summary>
        /// Add hit effect frames for a specific attack action.
        /// These are the animations that play on the player when hit by this attack.
        /// </summary>
        /// <param name="attackAction">Attack action name (e.g., "attack1", "attack2")</param>
        /// <param name="hitFrames">Hit effect frames from attack/info/hit</param>
        public void AddAttackHitEffect(string attackAction, List<IDXObject> hitFrames)
        {
            if (hitFrames == null || hitFrames.Count == 0)
                return;

            string key = attackAction.ToLower();
            _attackHitEffects[key] = hitFrames;
        }

        /// <summary>
        /// Get hit effect frames for a specific attack action.
        /// </summary>
        /// <param name="attackAction">Attack action name (e.g., "attack1", "attack2")</param>
        /// <returns>Hit effect frames, or null if not available</returns>
        public List<IDXObject> GetAttackHitEffect(string attackAction)
        {
            string key = attackAction?.ToLower() ?? "";
            if (_attackHitEffects.TryGetValue(key, out var frames))
                return frames;

            // Fallback to attack1's hit effect if specific attack hit not found
            if (key != "attack1" && _attackHitEffects.TryGetValue("attack1", out frames))
                return frames;

            return null;
        }

        /// <summary>
        /// Check if a specific attack has hit effect frames
        /// </summary>
        public bool HasAttackHitEffect(string attackAction)
        {
            return _attackHitEffects.ContainsKey(attackAction?.ToLower() ?? "");
        }
        /// <summary>
        /// Provides mob-specific fallback logic for movement animations.
        /// </summary>
        protected override bool TryGetFallbackFrames(string requestedAction, out List<IDXObject> frames)
        {
            frames = null;

            // Try move/walk fallback
            if (requestedAction == "move" || requestedAction == "walk")
            {
                if (_animations.TryGetValue("move", out frames))
                    return true;
                if (_animations.TryGetValue("walk", out frames))
                    return true;
            }

            return false;
        }

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
        public bool CanMove => CanWalk;
    }
}
