using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
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
        public sealed class AttackInfoMetadata
        {
            public bool HasRangeBounds { get; set; }
            public Rectangle RangeBounds { get; set; }
            public int EffectAfter { get; set; }
            public int AttackAfter { get; set; }
            public int AreaCount { get; set; }
            public int AttackCount { get; set; }
            public int StartOffset { get; set; }
            public bool HasPrimaryEffect { get; set; }
            public bool HasAreaWarning { get; set; }
            public bool IsRushAttack { get; set; }
            public bool IsJumpAttack { get; set; }
            public bool Tremble { get; set; }
        }

        public sealed class AttackEffectNode
        {
            public string Name { get; set; }
            public int EffectType { get; set; }
            public int EffectDistance { get; set; }
            public bool RandomPos { get; set; }
            public int Delay { get; set; }
            public bool HasRangeBounds { get; set; }
            public Rectangle RangeBounds { get; set; }
            public List<List<IDXObject>> Sequences { get; } = new List<List<IDXObject>>();
        }

        // Hit effect frames for each attack (attack1/info/hit, attack2/info/hit, etc.)
        private readonly Dictionary<string, List<IDXObject>> _attackHitEffects = new();
        private readonly Dictionary<string, List<IDXObject>> _attackProjectileEffects = new();
        private readonly Dictionary<string, List<IDXObject>> _attackEffects = new();
        private readonly Dictionary<string, List<IDXObject>> _attackWarningEffects = new();
        private readonly Dictionary<string, List<AttackEffectNode>> _attackExtraEffects = new();
        private readonly Dictionary<string, AttackInfoMetadata> _attackMetadata = new();

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
        /// Add projectile frames for a specific attack action.
        /// These come from attack/info/ball and are rendered while the projectile is in flight.
        /// </summary>
        public void AddAttackProjectileEffect(string attackAction, List<IDXObject> projectileFrames)
        {
            if (projectileFrames == null || projectileFrames.Count == 0)
                return;

            string key = attackAction.ToLower();
            _attackProjectileEffects[key] = projectileFrames;
        }

        /// <summary>
        /// Get projectile frames for a specific attack action.
        /// </summary>
        public List<IDXObject> GetAttackProjectileEffect(string attackAction)
        {
            string key = attackAction?.ToLower() ?? "";
            if (_attackProjectileEffects.TryGetValue(key, out var frames))
                return frames;

            if (key != "attack1" && _attackProjectileEffects.TryGetValue("attack1", out frames))
                return frames;

            return null;
        }

        public void AddAttackEffect(string attackAction, List<IDXObject> effectFrames)
        {
            if (effectFrames == null || effectFrames.Count == 0)
                return;

            string key = attackAction.ToLower();
            _attackEffects[key] = effectFrames;
        }

        public List<IDXObject> GetAttackEffect(string attackAction)
        {
            string key = attackAction?.ToLower() ?? "";
            if (_attackEffects.TryGetValue(key, out var frames))
                return frames;

            if (key != "attack1" && _attackEffects.TryGetValue("attack1", out frames))
                return frames;

            return null;
        }

        public void AddAttackWarningEffect(string attackAction, List<IDXObject> warningFrames)
        {
            if (warningFrames == null || warningFrames.Count == 0)
                return;

            string key = attackAction.ToLower();
            _attackWarningEffects[key] = warningFrames;
        }

        public List<IDXObject> GetAttackWarningEffect(string attackAction)
        {
            string key = attackAction?.ToLower() ?? "";
            if (_attackWarningEffects.TryGetValue(key, out var frames))
                return frames;

            if (key != "attack1" && _attackWarningEffects.TryGetValue("attack1", out frames))
                return frames;

            return null;
        }

        public void AddAttackExtraEffect(string attackAction, AttackEffectNode effectNode)
        {
            if (effectNode == null || effectNode.Sequences.Count == 0)
                return;

            string key = attackAction.ToLower();
            if (!_attackExtraEffects.TryGetValue(key, out var effects))
            {
                effects = new List<AttackEffectNode>();
                _attackExtraEffects[key] = effects;
            }

            effects.Add(effectNode);
        }

        public IReadOnlyList<AttackEffectNode> GetAttackExtraEffects(string attackAction)
        {
            string key = attackAction?.ToLower() ?? "";
            if (_attackExtraEffects.TryGetValue(key, out var effects))
                return effects;

            if (key != "attack1" && _attackExtraEffects.TryGetValue("attack1", out effects))
                return effects;

            return null;
        }

        public void SetAttackInfoMetadata(string attackAction, AttackInfoMetadata metadata)
        {
            if (metadata == null)
                return;

            _attackMetadata[attackAction.ToLower()] = metadata;
        }

        public AttackInfoMetadata GetAttackInfoMetadata(string attackAction)
        {
            string key = attackAction?.ToLower() ?? "";
            if (_attackMetadata.TryGetValue(key, out var metadata))
                return metadata;

            if (key != "attack1" && _attackMetadata.TryGetValue("attack1", out metadata))
                return metadata;

            return null;
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
