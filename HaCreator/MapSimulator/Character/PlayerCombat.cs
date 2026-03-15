using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.Physics;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Animation;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character
{
    /// <summary>
    /// Damage result from an attack
    /// </summary>
    public class DamageResult
    {
        public MobItem Target { get; set; }
        public int Damage { get; set; }
        public bool IsCritical { get; set; }
        public bool IsMiss { get; set; }
        public float HitX { get; set; }
        public float HitY { get; set; }
        public int KnockbackDirection { get; set; } // -1 left, 1 right
    }

    /// <summary>
    /// Player Combat System - Handles damage calculation and mob interaction
    /// </summary>
    public class PlayerCombat
    {
        private readonly PlayerCharacter _player;
        private readonly Random _random = new();

        // Combat stats
        private const float BASE_CRITICAL_CHANCE = 0.05f;
        private const float BASE_MISS_CHANCE = 0.05f;
        private const float CRITICAL_MULTIPLIER = 1.5f;
        private const float KNOCKBACK_FORCE = 250f; // Player knockback velocity when hit by mobs (px/s)
        private const float KNOCKBACK_FORCE_Y = -150f; // Vertical knockback velocity (px/s, negative = up)
        private const float MOB_HIT_KNOCKBACK_FORCE = 8f; // Grounded mob hit reaction in simulator movement units
        private const float SWIM_KNOCKBACK_FORCE_SCALE_X = 0.6f;
        private const float SWIM_KNOCKBACK_FORCE_SCALE_Y = 0.45f;
        private const int INVINCIBILITY_DURATION = 2000; // 2 seconds after hit

        // State
        private int _lastHitTime;
        private readonly List<int> _recentlyHitMobs = new(); // Prevent multi-hit exploits
        private Func<int, bool> _damageBlockedEvaluator;

        // Callbacks
        public Action<DamageResult> OnDamageDealt;
        public Action<PlayerCharacter, int, MobItem> OnDamageReceived;
        public Action<MobItem> OnMobKilled;
        public Action<float, float, int> OnMobAttackMissPlayer;
        public Action<DropPickupAttemptResult> OnPickupAttemptFailed;
        public Func<DropItem, DropPickupFailureReason> EvaluatePickupAvailability;
        /// <summary>
        /// Callback when player is hit by a mob skill attack.
        /// Parameters: playerX, playerY, mobSkillId, skillLevel
        /// Used to trigger the "affected" animation from MobSkill.img on the player.
        /// </summary>
        public Action<float, float, int, int> OnMobSkillHitPlayer;

        /// <summary>
        /// Callback when player is hit by a mob's regular attack (attack1, attack2, etc.)
        /// Parameters: playerX, playerY, hitEffectFrames
        /// Used to trigger the hit effect animation from the mob's attack/info/hit on the player.
        /// </summary>
        public Action<float, float, List<IDXObject>> OnAttackHitPlayer;

        public PlayerCombat(PlayerCharacter player)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void SetDamageBlockedEvaluator(Func<int, bool> damageBlockedEvaluator)
        {
            _damageBlockedEvaluator = damageBlockedEvaluator;
        }

        #region Player Attack

        /// <summary>
        /// Process attack against mobs in range
        /// </summary>
        public List<DamageResult> ProcessAttack(MobPool mobPool, Rectangle attackHitbox, int maxTargets = 6)
        {
            var results = new List<DamageResult>();
            _recentlyHitMobs.Clear();

            if (mobPool == null || !_player.IsAlive)
                return results;

            // Get mobs in attack range
            int hitCount = 0;
            foreach (var mob in mobPool.ActiveMobs)
            {
                if (hitCount >= maxTargets)
                    break;

                if (!IsMobInHitbox(mob, attackHitbox))
                    continue;

                var result = CalculateDamage(mob);
                results.Add(result);
                _recentlyHitMobs.Add(mob.PoolId);
                hitCount++;

                // Apply damage to mob
                ApplyDamageToMob(mob, result);

                OnDamageDealt?.Invoke(result);

                // Check if mob died
                if (mob.AI != null && mob.AI.State == MobAIState.Death)
                {
                    OnMobKilled?.Invoke(mob);
                }
            }

            return results;
        }

        private bool IsMobInHitbox(MobItem mob, Rectangle hitbox)
        {
            if (mob?.MovementInfo == null || mob.IsProtectedFromPlayerDamage)
                return false;

            // Get mob hitbox
            float mobX = mob.MovementInfo.X;
            float mobY = mob.MovementInfo.Y;

            // Simple rectangular collision
            var mobBounds = new Rectangle(
                (int)mobX - 20,
                (int)mobY - 50,
                40,
                50);

            // Adjust hitbox to world coordinates
            var worldHitbox = new Rectangle(
                (int)_player.X + hitbox.X,
                (int)_player.Y + hitbox.Y,
                hitbox.Width,
                hitbox.Height);

            return worldHitbox.Intersects(mobBounds);
        }

        private DamageResult CalculateDamage(MobItem mob)
        {
            Vector2 damageAnchor = mob?.GetDamageNumberAnchor() ?? Vector2.Zero;

            var result = new DamageResult
            {
                Target = mob,
                HitX = damageAnchor.X,
                HitY = damageAnchor.Y,
                KnockbackDirection = _player.FacingRight ? 1 : -1
            };

            // Check for miss
            if (_random.NextDouble() < BASE_MISS_CHANCE)
            {
                result.IsMiss = true;
                result.Damage = 0;
                return result;
            }

            // Base damage calculation
            int baseAttack = _player.Build.Attack;
            var weapon = _player.Build.GetWeapon();
            if (weapon != null)
            {
                baseAttack += weapon.Attack;
            }

            // Damage variance (90-110%)
            float variance = 0.9f + (float)_random.NextDouble() * 0.2f;
            result.Damage = (int)(baseAttack * variance);

            // Critical hit check
            if (_random.NextDouble() < BASE_CRITICAL_CHANCE)
            {
                result.IsCritical = true;
                result.Damage = (int)(result.Damage * CRITICAL_MULTIPLIER);
            }

            // Apply mob defense (simplified)
            // In real MapleStory, this is more complex with level differences
            result.Damage = Math.Max(1, result.Damage);

            return result;
        }

        private void ApplyDamageToMob(MobItem mob, DamageResult result)
        {
            if (mob?.AI == null)
                return;

            // Apply damage with sound effects and aggro (pass player position for chase)
            mob.ApplyDamage(result.Damage, Environment.TickCount, result.IsCritical, _player.X, _player.Y);

            // Apply knockback
            if (mob.MovementInfo != null && !result.IsMiss)
            {
                mob.MovementInfo.ApplyKnockback(MOB_HIT_KNOCKBACK_FORCE, result.KnockbackDirection > 0);
            }
        }

        #endregion

        #region Player Defense

        /// <summary>
        /// Check for mob attacks hitting player (including skill attacks)
        /// </summary>
        public void CheckMobAttacks(MobPool mobPool, int currentTime)
        {
            if (mobPool == null || !_player.IsAlive)
                return;

            if (_damageBlockedEvaluator?.Invoke(currentTime) == true)
                return;

            // Check invincibility frames
            if (currentTime - _lastHitTime < INVINCIBILITY_DURATION)
                return;

            var playerHitbox = _player.GetHitbox();

            foreach (var mob in mobPool.ActiveMobs)
            {
                if (mob?.AI == null)
                    continue;

                bool isUsingSkill = mob.AI.State == MobAIState.Skill;
                if (!isUsingSkill)
                    continue;

                MobSkillEntry currentSkill = mob.AI.GetCurrentSkill();
                bool skillTriggered = isUsingSkill && mob.AI.ShouldApplySkillEffect(currentTime);
                if (!skillTriggered)
                    continue;

                Rectangle mobAttackHitbox = GetMobAttackHitbox(mob, null, currentSkill);
                if (playerHitbox.Intersects(mobAttackHitbox))
                {
                    ProcessPlayerHit(mob, currentTime, null, currentSkill);
                    return; // Only take one hit per frame
                }
            }
        }

        public bool TryApplyMobHit(MobItem mob, Rectangle hitbox, int currentTime, MobAttackEntry attackOverride = null, MobSkillEntry skillOverride = null)
        {
            if (mob?.AI == null || !_player.IsAlive)
                return false;

            if (_damageBlockedEvaluator?.Invoke(currentTime) == true)
                return false;

            if (currentTime - _lastHitTime < INVINCIBILITY_DURATION)
                return false;

            if (hitbox.IsEmpty || !_player.GetHitbox().Intersects(hitbox))
                return false;

            ProcessPlayerHit(mob, currentTime, attackOverride, skillOverride);
            return true;
        }

        private Rectangle GetMobAttackHitbox(MobItem mob, MobAttackEntry attackOverride = null, MobSkillEntry skillOverride = null)
        {
            if (mob?.MovementInfo == null)
                return Rectangle.Empty;

            float mobX = mob.MovementInfo.X;
            float mobY = mob.MovementInfo.Y;
            bool facingRight = mob.MovementInfo.FlipX; // FlipX = true means facing right
            var attack = attackOverride ?? mob.AI?.GetCurrentAttack();

            if (skillOverride == null && attack?.HasRangeBounds == true)
            {
                int left = attack.RangeLeft;
                int right = attack.RangeRight;
                if (facingRight)
                {
                    left = -attack.RangeRight;
                    right = -attack.RangeLeft;
                }

                int top = attack.RangeTop;
                int bottom = attack.RangeBottom;
                int rangeWidth = Math.Max(1, right - left);
                int rangeHeight = Math.Max(1, bottom - top);

                return new Rectangle(
                    (int)mobX + left,
                    (int)mobY + top,
                    rangeWidth,
                    rangeHeight);
            }

            int range = skillOverride?.Range ?? attack?.Range ?? 50;
            int width = Math.Max(50, range);
            int height = skillOverride != null
                ? 90
                : Math.Max(60, attack?.AreaHeight ?? 60);

            if (skillOverride != null && skillOverride.Range >= 180)
            {
                return new Rectangle(
                    (int)mobX - width / 2,
                    (int)mobY - height,
                    width,
                    height);
            }

            return new Rectangle(
                (int)mobX + (facingRight ? 0 : -width),
                (int)mobY - height + 20,
                width,
                height);
        }

        private void ProcessPlayerHit(MobItem mob, int currentTime, MobAttackEntry attackOverride = null, MobSkillEntry skillOverride = null)
        {
            bool isSkillAttack = skillOverride != null;

            if (ShouldMobAttackMiss(mob))
            {
                OnMobAttackMissPlayer?.Invoke(_player.X, _player.Y, currentTime);
                return;
            }

            _lastHitTime = currentTime;

            // Calculate damage from mob
            var currentAttack = attackOverride ?? mob.AI?.GetCurrentAttack();
            int baseMobAttack = currentAttack?.Damage ?? GetFallbackMobSkillDamage(mob);
            int mobAttack = mob.AI?.CalculateOutgoingDamage(
                baseMobAttack,
                isSkillAttack ? MobDamageType.Magical : MobDamageType.Physical) ?? baseMobAttack;
            int playerDefense = _player.Build?.Defense ?? 0;

            int damage = Math.Max(1, mobAttack - playerDefense / 2);

            // Apply damage variance
            float variance = 0.9f + (float)_random.NextDouble() * 0.2f;
            damage = (int)(damage * variance);

            // Calculate knockback direction (away from mob)
            Vector2 knockback = GetPlayerKnockbackVelocity(mob.MovementInfo.X);

            // Play character damage sound from mob (CharDam1/CharDam2)
            int attackNum = currentAttack?.AttackId ?? 1;
            mob.PlayCharDamSound(attackNum);

            // Apply damage and knockback (KNOCKBACK_FORCE_Y is negative for upward motion)
            _player.TakeDamage(damage, knockback.X, knockback.Y);

            OnDamageReceived?.Invoke(_player, damage, mob);

            // Trigger mob skill hit effect if this was a skill attack
            if (isSkillAttack)
            {
                var currentSkill = skillOverride ?? mob.AI?.GetCurrentSkill();
                if (currentSkill != null)
                {
                    // Trigger the "affected" animation from MobSkill.img on the player
                    // The animation plays at the player's position
                    OnMobSkillHitPlayer?.Invoke(_player.X, _player.Y, currentSkill.SkillId, currentSkill.Level);
                    System.Diagnostics.Debug.WriteLine($"[PlayerCombat] Player hit by mob skill {currentSkill.SkillId} level {currentSkill.Level}");
                }
            }
            else
            {
                // Trigger regular attack hit effect (from attack/info/hit in mob data)
                // Reuse currentAttack from above (already retrieved at line 266)
                if (currentAttack != null)
                {
                    // Get the hit effect frames for this attack action
                    var hitFrames = mob.GetAttackHitFrames(currentAttack.AnimationName);
                    if (hitFrames != null && hitFrames.Count > 0)
                    {
                        OnAttackHitPlayer?.Invoke(_player.X, _player.Y, hitFrames);
                        System.Diagnostics.Debug.WriteLine($"[PlayerCombat] Player hit by mob attack {currentAttack.AnimationName}, displaying {hitFrames.Count} hit effect frames");
                    }
                }
            }
        }

        private bool ShouldMobAttackMiss(MobItem mob)
        {
            if (mob?.AI == null)
                return false;

            if (mob.AI.HasStatusEffect(MobStatusEffect.Blind))
                return true;

            float hitChance = GetMobHitChance(mob.AI);
            return _random.NextDouble() > hitChance;
        }

        private float GetMobHitChance(MobAI mobAI)
        {
            if (mobAI == null)
                return 0.95f;

            float hitChance = 0.95f;
            hitChance += Math.Clamp(mobAI.GetStatusEffectValue(MobStatusEffect.ACC) / 100f, -0.35f, 0.35f);

            if (mobAI.HasStatusEffect(MobStatusEffect.Darkness))
            {
                int darknessPenalty = mobAI.GetStatusEffectValue(MobStatusEffect.Darkness);
                if (darknessPenalty <= 0)
                {
                    darknessPenalty = 20;
                }

                hitChance -= Math.Min(0.85f, darknessPenalty / 100f);
            }

            int playerAvoidability = Math.Max(0, _player.Build?.Avoidability ?? 0);
            hitChance -= Math.Min(0.45f, playerAvoidability / 400f);

            return Math.Clamp(hitChance, 0.05f, 0.99f);
        }

        #endregion

        #region Touch Damage

        /// <summary>
        /// Check for touch damage from mobs (contact damage)
        /// </summary>
        public void CheckTouchDamage(MobPool mobPool, int currentTime)
        {
            if (mobPool == null || !_player.IsAlive)
                return;

            if (_damageBlockedEvaluator?.Invoke(currentTime) == true)
                return;

            // Check invincibility frames
            if (currentTime - _lastHitTime < INVINCIBILITY_DURATION)
                return;

            var playerHitbox = _player.GetHitbox();

            foreach (var mob in mobPool.ActiveMobs)
            {
                if (mob?.AI == null || mob.AI.State == MobAIState.Death)
                    continue;

                // Get mob body hitbox
                var mobHitbox = GetMobBodyHitbox(mob, currentTime);
                if (playerHitbox.Intersects(mobHitbox))
                {
                    ProcessTouchDamage(mob, currentTime);
                    return;
                }
            }
        }

        private static int GetFallbackMobSkillDamage(MobItem mob)
        {
            int physical = mob?.MobData?.PADamage ?? 0;
            int magical = mob?.MobData?.MADamage ?? 0;
            return Math.Max(10, Math.Max(physical, magical));
        }

        private Rectangle GetMobBodyHitbox(MobItem mob, int currentTime)
        {
            if (mob == null)
                return Rectangle.Empty;

            return mob.GetBodyHitbox(currentTime);
        }

        private void ProcessTouchDamage(MobItem mob, int currentTime)
        {
            _lastHitTime = currentTime;

            // Touch damage is typically lower than attack damage (body collision)
            // Use a portion of the mob's attack damage or a default
            var attack = mob.AI?.GetCurrentAttack();
            int baseTouchDamage = (attack?.Damage ?? 10) / 2; // Half of attack damage
            int touchDamage = mob.AI?.CalculateOutgoingDamage(baseTouchDamage, MobDamageType.Physical) ?? baseTouchDamage;
            touchDamage = Math.Max(5, touchDamage); // Minimum 5 damage
            int playerDefense = _player.Build?.Defense ?? 0;

            int damage = Math.Max(1, touchDamage - playerDefense / 2);

            // Calculate knockback direction (away from mob)
            Vector2 knockback = GetPlayerKnockbackVelocity(mob.MovementInfo.X);

            // Play character damage sound from mob (CharDam1 for touch damage)
            mob.PlayCharDamSound(1);

            // Apply damage and knockback (touch damage uses same knockback as attacks)
            _player.TakeDamage(damage, knockback.X, knockback.Y);

            OnDamageReceived?.Invoke(_player, damage, mob);

            // Trigger attack hit effect for touch damage (use attack1's hit effect if available)
            var hitFrames = mob.GetAttackHitFrames("attack1");
            if (hitFrames != null && hitFrames.Count > 0)
            {
                OnAttackHitPlayer?.Invoke(_player.X, _player.Y, hitFrames);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Check if player is invincible
        /// </summary>
        public bool IsInvincible(int currentTime)
        {
            return currentTime - _lastHitTime < INVINCIBILITY_DURATION;
        }

        /// <summary>
        /// Get remaining invincibility time
        /// </summary>
        public int GetInvincibilityRemaining(int currentTime)
        {
            int remaining = INVINCIBILITY_DURATION - (currentTime - _lastHitTime);
            return Math.Max(0, remaining);
        }

        /// <summary>
        /// Force invincibility (e.g., after respawn)
        /// </summary>
        public void SetInvincible(int currentTime)
        {
            _lastHitTime = currentTime;
        }

        private Vector2 GetPlayerKnockbackVelocity(float sourceX)
        {
            float horizontal = sourceX < _player.X ? KNOCKBACK_FORCE : -KNOCKBACK_FORCE;
            float vertical = KNOCKBACK_FORCE_Y;

            if (_player.Physics.IsSwimming())
            {
                horizontal *= SWIM_KNOCKBACK_FORCE_SCALE_X;
                vertical *= SWIM_KNOCKBACK_FORCE_SCALE_Y;
            }

            return new Vector2(horizontal, vertical);
        }

        /// <summary>
        /// Get total attack range
        /// </summary>
        public int GetAttackRange()
        {
            var weapon = _player.Build.GetWeapon();
            return weapon?.Range ?? 50;
        }

        /// <summary>
        /// Calculate DPS estimate
        /// </summary>
        public float CalculateDPS()
        {
            int baseAttack = _player.Build.Attack;
            var weapon = _player.Build.GetWeapon();
            if (weapon != null)
            {
                baseAttack += weapon.Attack;
            }

            // Average damage with crit
            float avgDamage = baseAttack * (1f + BASE_CRITICAL_CHANCE * (CRITICAL_MULTIPLIER - 1f));
            avgDamage *= (1f - BASE_MISS_CHANCE);

            // Attacks per second based on weapon speed
            int speed = weapon?.AttackSpeed ?? 6;
            float attacksPerSecond = 1000f / (300 + speed * 50);

            return avgDamage * attacksPerSecond;
        }

        #endregion

        #region Drop Pickup

        /// <summary>
        /// Try to pick up nearby drops
        /// </summary>
        public DropItem TryPickupDrop(DropPool dropPool, int currentTime, float pickupRange = 40f)
        {
            if (dropPool == null || !_player.IsAlive)
                return null;

            DropPickupAttemptResult result = dropPool.TryPickupClosestDetailed(
                _player.X,
                _player.Y,
                0,
                currentTime,
                pickupRange,
                EvaluatePickupAvailability);

            if (result.Drop != null)
            {
                // Apply drop effects
                if (result.Drop.Type == DropType.Meso)
                {
                    // Add mesos to player (would need inventory system)
                    // For now, just consume the drop
                }
                else
                {
                    // Add item to inventory (would need inventory system)
                }
            }
            else if (result.FailureReason != DropPickupFailureReason.None
                     && result.FailureReason != DropPickupFailureReason.NoDropInRange)
            {
                OnPickupAttemptFailed?.Invoke(result);
            }

            return result.Drop;
        }

        /// <summary>
        /// Get closest drop in range
        /// </summary>
        public DropItem GetClosestDrop(DropPool dropPool, float range = 40f)
        {
            if (dropPool == null)
                return null;

            return dropPool.GetClosestDrop(_player.X, _player.Y, range, 0);
        }

        #endregion
    }
}
