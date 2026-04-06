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
            public int AttackType { get; set; } = -1;
            public bool HitAttach { get; set; }
            public bool FacingAttach { get; set; }
            public bool EffectFacingAttach { get; set; }
            public bool HasRangeBounds { get; set; }
            public Rectangle RangeBounds { get; set; }
            public bool HasRangeOrigin { get; set; }
            public Point RangeOrigin { get; set; }
            public int RangeRadius { get; set; }
            public int EffectAfter { get; set; }
            public int AttackAfter { get; set; }
            public int RandDelayAttack { get; set; }
            public int AreaCount { get; set; }
            public int AttackCount { get; set; }
            public int StartOffset { get; set; }
            public bool HasPrimaryEffect { get; set; }
            public bool HasAreaWarning { get; set; }
            public bool IsRushAttack { get; set; }
            public bool IsJumpAttack { get; set; }
            public bool Tremble { get; set; }
            public bool IsAngerAttack { get; set; }
        }

        public sealed class AttackEffectNode
        {
            public string Name { get; set; }
            public int EffectType { get; set; }
            public int EffectDistance { get; set; }
            public bool RandomPos { get; set; }
            public int Delay { get; set; }
            public int Start { get; set; }
            public int Interval { get; set; }
            public int Count { get; set; }
            public int Duration { get; set; }
            public int Fall { get; set; }
            public int OffsetX { get; set; }
            public int OffsetY { get; set; }
            public bool HasRangeBounds { get; set; }
            public Rectangle RangeBounds { get; set; }
            public bool UseRangeGroupPlacement { get; set; }
            public int RangeGroupIndex { get; set; }
            public int RangeGroupCount { get; set; }
            public List<List<IDXObject>> Sequences { get; } = new List<List<IDXObject>>();
        }

        // Hit effect frames for each attack (attack1/info/hit, attack2/info/hit, etc.)
        private readonly Dictionary<string, List<IDXObject>> _attackHitEffects = new();
        private readonly Dictionary<string, List<IDXObject>> _attackProjectileEffects = new();
        private readonly Dictionary<string, List<IDXObject>> _attackEffects = new();
        private readonly Dictionary<string, List<IDXObject>> _attackWarningEffects = new();
        private readonly Dictionary<string, List<AttackEffectNode>> _attackExtraEffects = new();
        private readonly Dictionary<string, AttackInfoMetadata> _attackMetadata = new();
        private readonly Dictionary<int, List<IDXObject>> _angerGaugeAnimations = new();
        private List<IDXObject> _angerGaugeEffect;

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
            foreach (string key in EnumerateCompatibleAttackKeys(attackAction))
            {
                if (_attackHitEffects.TryGetValue(key, out var frames))
                    return frames;
            }

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
            foreach (string key in EnumerateCompatibleAttackKeys(attackAction))
            {
                if (_attackProjectileEffects.TryGetValue(key, out var frames))
                    return frames;
            }

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
            foreach (string key in EnumerateCompatibleAttackKeys(attackAction))
            {
                if (_attackEffects.TryGetValue(key, out var frames))
                    return frames;
            }

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
            foreach (string key in EnumerateCompatibleAttackKeys(attackAction))
            {
                if (_attackWarningEffects.TryGetValue(key, out var frames))
                    return frames;
            }

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
            foreach (string key in EnumerateCompatibleAttackKeys(attackAction))
            {
                if (_attackExtraEffects.TryGetValue(key, out var effects))
                    return effects;
            }

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
            foreach (string key in EnumerateCompatibleAttackKeys(attackAction))
            {
                if (_attackMetadata.TryGetValue(key, out var metadata))
                    return metadata;
            }

            return null;
        }

        private static IEnumerable<string> EnumerateCompatibleAttackKeys(string attackAction)
        {
            string key = attackAction?.ToLowerInvariant() ?? string.Empty;
            if (key.Length == 0)
            {
                yield break;
            }

            yield return key;

            if (TryResolveAlternateAttackKey(key, out string alternateKey))
            {
                yield return alternateKey;
            }

            if (!string.Equals(key, "attack1", System.StringComparison.Ordinal))
            {
                yield return "attack1";
            }

            if (!string.Equals(key, "skill1", System.StringComparison.Ordinal))
            {
                yield return "skill1";
            }
        }

        private static bool TryResolveAlternateAttackKey(string key, out string alternateKey)
        {
            alternateKey = null;
            if (key.StartsWith("attack", System.StringComparison.Ordinal))
            {
                string suffix = key["attack".Length..];
                if (suffix.Length > 0)
                {
                    alternateKey = $"skill{suffix}";
                    return true;
                }
            }
            else if (key.StartsWith("skill", System.StringComparison.Ordinal))
            {
                string suffix = key["skill".Length..];
                if (suffix.Length > 0)
                {
                    alternateKey = $"attack{suffix}";
                    return true;
                }
            }

            return false;
        }

        public void SetAngerGaugeAnimation(int stage, List<IDXObject> frames)
        {
            if (stage < 0 || frames == null || frames.Count == 0)
                return;

            _angerGaugeAnimations[stage] = frames;
        }

        public List<IDXObject> GetAngerGaugeAnimation(int stage)
        {
            return stage >= 0 && _angerGaugeAnimations.TryGetValue(stage, out var frames)
                ? frames
                : null;
        }

        public void SetAngerGaugeEffect(List<IDXObject> frames)
        {
            if (frames == null || frames.Count == 0)
                return;

            _angerGaugeEffect = frames;
        }

        public List<IDXObject> GetAngerGaugeEffect()
        {
            return _angerGaugeEffect;
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
