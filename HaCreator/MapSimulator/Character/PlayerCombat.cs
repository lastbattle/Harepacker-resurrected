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
        private const float KNOCKBACK_FORCE = 250f; // Horizontal knockback velocity (px/s) - matches official client feel
        private const float KNOCKBACK_FORCE_Y = -150f; // Vertical knockback velocity (px/s, negative = up)
        private const int INVINCIBILITY_DURATION = 2000; // 2 seconds after hit

        // State
        private int _lastHitTime;
        private readonly List<int> _recentlyHitMobs = new(); // Prevent multi-hit exploits

        // Callbacks
        public Action<DamageResult> OnDamageDealt;
        public Action<PlayerCharacter, int, MobItem> OnDamageReceived;
        public Action<MobItem> OnMobKilled;
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
            if (mob?.MovementInfo == null)
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
            var result = new DamageResult
            {
                Target = mob,
                HitX = mob.MovementInfo?.X ?? 0,
                HitY = mob.MovementInfo?.Y ?? 0,
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
                float knockback = KNOCKBACK_FORCE * result.KnockbackDirection;
                mob.MovementInfo.ApplyKnockback(knockback, -2f);
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

            // Check invincibility frames
            if (currentTime - _lastHitTime < INVINCIBILITY_DURATION)
                return;

            var playerHitbox = _player.GetHitbox();

            foreach (var mob in mobPool.ActiveMobs)
            {
                if (mob?.AI == null)
                    continue;

                // Check for regular attack or skill attack
                bool isAttacking = mob.AI.State == MobAIState.Attack;
                bool isUsingSkill = mob.AI.State == MobAIState.Skill;

                if (!isAttacking && !isUsingSkill)
                    continue;

                // Check if mob's attack hitbox overlaps player
                var mobAttackHitbox = GetMobAttackHitbox(mob);
                if (playerHitbox.Intersects(mobAttackHitbox))
                {
                    ProcessPlayerHit(mob, currentTime, isUsingSkill);
                    return; // Only take one hit per frame
                }
            }
        }

        private Rectangle GetMobAttackHitbox(MobItem mob)
        {
            if (mob?.MovementInfo == null)
                return Rectangle.Empty;

            float mobX = mob.MovementInfo.X;
            float mobY = mob.MovementInfo.Y;
            bool facingRight = mob.MovementInfo.FlipX; // FlipX = true means facing right

            // Attack extends in front of mob
            var currentAttack = mob.AI?.GetCurrentAttack();
            int range = currentAttack?.Range ?? 50;
            int width = range;
            int height = 60;

            return new Rectangle(
                (int)mobX + (facingRight ? 0 : -width),
                (int)mobY - 40,
                width,
                height);
        }

        private void ProcessPlayerHit(MobItem mob, int currentTime, bool isSkillAttack = false)
        {
            _lastHitTime = currentTime;

            // Calculate damage from mob
            var currentAttack = mob.AI?.GetCurrentAttack();
            int mobAttack = currentAttack?.Damage ?? 10;
            int playerDefense = _player.Build?.Defense ?? 0;

            int damage = Math.Max(1, mobAttack - playerDefense / 2);

            // Apply damage variance
            float variance = 0.9f + (float)_random.NextDouble() * 0.2f;
            damage = (int)(damage * variance);

            // Calculate knockback direction (away from mob)
            float knockbackX = mob.MovementInfo.X < _player.X ? KNOCKBACK_FORCE : -KNOCKBACK_FORCE;

            // Play character damage sound from mob (CharDam1/CharDam2)
            int attackNum = currentAttack?.AttackId ?? 1;
            mob.PlayCharDamSound(attackNum);

            // Apply damage and knockback (KNOCKBACK_FORCE_Y is negative for upward motion)
            _player.TakeDamage(damage, knockbackX, KNOCKBACK_FORCE_Y);

            OnDamageReceived?.Invoke(_player, damage, mob);

            // Trigger mob skill hit effect if this was a skill attack
            if (isSkillAttack && mob.AI != null)
            {
                var currentSkill = mob.AI.GetCurrentSkill();
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

        #endregion

        #region Touch Damage

        /// <summary>
        /// Check for touch damage from mobs (contact damage)
        /// </summary>
        public void CheckTouchDamage(MobPool mobPool, int currentTime)
        {
            if (mobPool == null || !_player.IsAlive)
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
                var mobHitbox = GetMobBodyHitbox(mob);
                if (playerHitbox.Intersects(mobHitbox))
                {
                    ProcessTouchDamage(mob, currentTime);
                    return;
                }
            }
        }

        private Rectangle GetMobBodyHitbox(MobItem mob)
        {
            if (mob?.MovementInfo == null)
                return Rectangle.Empty;

            float mobX = mob.MovementInfo.X;
            float mobY = mob.MovementInfo.Y;

            // Body hitbox based on mob size
            int width = 40;
            int height = 50;

            // TODO: Get actual mob bounds from MobInfo

            return new Rectangle(
                (int)mobX - width / 2,
                (int)mobY - height,
                width,
                height);
        }

        private void ProcessTouchDamage(MobItem mob, int currentTime)
        {
            _lastHitTime = currentTime;

            // Touch damage is typically lower than attack damage (body collision)
            // Use a portion of the mob's attack damage or a default
            var attack = mob.AI?.GetCurrentAttack();
            int touchDamage = (attack?.Damage ?? 10) / 2; // Half of attack damage
            touchDamage = Math.Max(5, touchDamage); // Minimum 5 damage
            int playerDefense = _player.Build?.Defense ?? 0;

            int damage = Math.Max(1, touchDamage - playerDefense / 2);

            // Calculate knockback direction (away from mob)
            float knockbackX = mob.MovementInfo.X < _player.X ? KNOCKBACK_FORCE : -KNOCKBACK_FORCE;

            // Play character damage sound from mob (CharDam1 for touch damage)
            mob.PlayCharDamSound(1);

            // Apply damage and knockback (touch damage uses same knockback as attacks)
            _player.TakeDamage(damage, knockbackX, KNOCKBACK_FORCE_Y);

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

            var drop = dropPool.TryPickupClosest(_player.X, _player.Y, 0, currentTime, pickupRange);
            if (drop != null)
            {
                // Apply drop effects
                if (drop.Type == DropType.Meso)
                {
                    // Add mesos to player (would need inventory system)
                    // For now, just consume the drop
                }
                else
                {
                    // Add item to inventory (would need inventory system)
                }
            }

            return drop;
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
