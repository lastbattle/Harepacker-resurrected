using System;
using System.Collections.Generic;
using HaCreator.MapSimulator.Entities;
using HaCreator.MapSimulator.AI;
using HaCreator.MapSimulator.Pools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HaCreator.MapSimulator.Effects;

namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Manages active skills, projectiles, buffs, and cooldowns
    /// </summary>
    public class SkillManager
    {
        #region Properties

        private readonly SkillLoader _loader;
        private readonly PlayerCharacter _player;

        // Active state
        private readonly List<ActiveProjectile> _projectiles = new();
        private readonly List<ActiveBuff> _buffs = new();
        private readonly List<ActiveHitEffect> _hitEffects = new();
        private readonly Dictionary<int, int> _cooldowns = new(); // skillId -> lastCastTime
        private SkillCastInfo _currentCast;

        // Skill book
        private readonly Dictionary<int, int> _skillLevels = new(); // skillId -> level
        private List<SkillData> _availableSkills = new();

        // Hotkeys
        private readonly Dictionary<int, int> _skillHotkeys = new(); // keyIndex -> skillId

        // Counters
        private int _nextProjectileId = 1;

        // Callbacks
        public Action<SkillCastInfo> OnSkillCast;
        public Action<ActiveProjectile, MobItem> OnProjectileHit;
        public Action<ActiveBuff> OnBuffApplied;
        public Action<ActiveBuff> OnBuffExpired;

        // References
        private MobPool _mobPool;
        private CombatEffects _combatEffects;

        #endregion

        #region Initialization

        public SkillManager(SkillLoader loader, PlayerCharacter player)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public void SetMobPool(MobPool mobPool) => _mobPool = mobPool;
        public void SetCombatEffects(CombatEffects effects) => _combatEffects = effects;

        /// <summary>
        /// Load skills for player's job
        /// </summary>
        public void LoadSkillsForJob(int jobId)
        {
            _availableSkills = _loader.LoadSkillsForJobPath(jobId);

            // Initialize skill levels to 0 (unlearned)
            foreach (var skill in _availableSkills)
            {
                if (!_skillLevels.ContainsKey(skill.SkillId))
                {
                    _skillLevels[skill.SkillId] = 0;
                }
            }
        }

        /// <summary>
        /// Set skill level
        /// </summary>
        public void SetSkillLevel(int skillId, int level)
        {
            _skillLevels[skillId] = level;
        }

        /// <summary>
        /// Get skill level
        /// </summary>
        public int GetSkillLevel(int skillId)
        {
            return _skillLevels.TryGetValue(skillId, out int level) ? level : 0;
        }

        /// <summary>
        /// Set skill hotkey
        /// </summary>
        public void SetHotkey(int keyIndex, int skillId)
        {
            _skillHotkeys[keyIndex] = skillId;
        }

        /// <summary>
        /// Get skill on hotkey
        /// </summary>
        public int GetHotkeySkill(int keyIndex)
        {
            return _skillHotkeys.TryGetValue(keyIndex, out int skillId) ? skillId : 0;
        }

        #endregion

        #region Skill Casting

        /// <summary>
        /// Try to cast a skill
        /// </summary>
        public bool TryCastSkill(int skillId, int currentTime)
        {
            int level = GetSkillLevel(skillId);
            if (level <= 0)
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            // Check if can cast
            if (!CanCastSkill(skillId, currentTime))
                return false;

            // Start casting
            StartCast(skill, level, currentTime);
            return true;
        }

        /// <summary>
        /// Try to cast skill on hotkey
        /// </summary>
        public bool TryCastHotkey(int keyIndex, int currentTime)
        {
            int skillId = GetHotkeySkill(keyIndex);
            if (skillId <= 0)
                return false;

            return TryCastSkill(skillId, currentTime);
        }

        /// <summary>
        /// Check if skill can be cast
        /// </summary>
        public bool CanCastSkill(int skillId, int currentTime)
        {
            int level = GetSkillLevel(skillId);
            if (level <= 0)
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            // Check passive
            if (skill.IsPassive)
                return false;

            // Check cooldown
            if (IsOnCooldown(skillId, currentTime))
                return false;

            // Check MP
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return false;

            if (_player.MP < levelData.MpCon)
                return false;

            // Check HP (some skills consume HP)
            if (_player.HP <= levelData.HpCon)
                return false;

            // Check if already casting
            if (_currentCast != null && !_currentCast.IsComplete)
                return false;

            // Attack skills cannot be cast while on ladder/rope/swimming (buffs and heals are allowed)
            if (skill.IsAttack && !_player.CanAttack)
                return false;

            return true;
        }

        /// <summary>
        /// Check cooldown
        /// </summary>
        public bool IsOnCooldown(int skillId, int currentTime)
        {
            if (!_cooldowns.TryGetValue(skillId, out int lastCast))
                return false;

            var skill = GetSkillData(skillId);
            if (skill == null)
                return false;

            int level = GetSkillLevel(skillId);
            var levelData = skill.GetLevel(level);
            if (levelData == null || levelData.Cooldown <= 0)
                return false;

            return currentTime - lastCast < levelData.Cooldown;
        }

        /// <summary>
        /// Get remaining cooldown
        /// </summary>
        public int GetCooldownRemaining(int skillId, int currentTime)
        {
            if (!_cooldowns.TryGetValue(skillId, out int lastCast))
                return 0;

            var skill = GetSkillData(skillId);
            int level = GetSkillLevel(skillId);
            var levelData = skill?.GetLevel(level);

            if (levelData == null || levelData.Cooldown <= 0)
                return 0;

            int remaining = levelData.Cooldown - (currentTime - lastCast);
            return Math.Max(0, remaining);
        }

        private void StartCast(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);

            _currentCast = new SkillCastInfo
            {
                SkillId = skill.SkillId,
                Level = level,
                SkillData = skill,
                LevelData = levelData,
                CastTime = currentTime,
                CasterId = 0, // Player ID
                CasterX = _player.X,
                CasterY = _player.Y,
                FacingRight = _player.FacingRight
            };

            // Consume MP
            _player.MP = Math.Max(0, _player.MP - levelData.MpCon);

            // Consume HP if needed
            if (levelData.HpCon > 0)
            {
                _player.HP = Math.Max(1, _player.HP - levelData.HpCon);
            }

            // Set cooldown
            if (levelData.Cooldown > 0)
            {
                _cooldowns[skill.SkillId] = currentTime;
            }

            // Trigger player attack animation
            _player.TriggerSkillAnimation(skill.ActionName ?? "attack1");

            OnSkillCast?.Invoke(_currentCast);

            // Handle different skill types
            if (skill.IsBuff)
            {
                ApplyBuff(skill, level, currentTime);
            }
            else if (skill.IsHeal)
            {
                ApplyHeal(skill, level);
            }
            else if (skill.IsAttack)
            {
                if (skill.Projectile != null)
                {
                    SpawnProjectile(skill, level, currentTime);
                }
                else
                {
                    ProcessMeleeAttack(skill, level, currentTime);
                }
            }
        }

        #endregion

        #region Basic Attack Methods (TryDoingMeleeAttack, TryDoingShoot, TryDoingMagicAttack)

        /// <summary>
        /// TryDoingMeleeAttack - Close range attack (matching CUserLocal::TryDoingMeleeAttack from client)
        /// </summary>
        public bool TryDoingMeleeAttack(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            if (_mobPool == null)
                return false;

            // Trigger attack animation
            _player.TriggerSkillAnimation("swingO1");

            // Define melee hitbox (relative to player facing)
            int hitWidth = 80;
            int hitHeight = 60;
            int offsetX = _player.FacingRight ? 10 : -(hitWidth + 10);

            var worldHitbox = new Rectangle(
                (int)_player.X + offsetX,
                (int)_player.Y - hitHeight - 10,
                hitWidth,
                hitHeight);

            int hitCount = 0;
            int maxTargets = 3; // Can hit up to 3 mobs
            var mobsToKill = new List<MobItem>();

            foreach (var mob in _mobPool.ActiveMobs)
            {
                if (hitCount >= maxTargets)
                    break;

                // Skip dead or removed mobs
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!worldHitbox.Intersects(mobHitbox))
                    continue;

                // Calculate basic melee damage
                int damage = CalculateBasicDamage();

                bool died = mob.ApplyDamage(damage, currentTime, damage > 100, _player.X, _player.Y);

                // Apply knockback effect if mob didn't die
                if (!died && mob.MovementInfo != null)
                {
                    float knockbackForce = 6f + (damage / 50f); // Scale knockback with damage
                    knockbackForce = Math.Min(knockbackForce, 12f); // Cap at 12
                    bool knockRight = _player.FacingRight; // Knock away from player
                    mob.MovementInfo.ApplyKnockback(knockbackForce, knockRight);
                }

                if (_combatEffects != null)
                {
                    // Notify HP bar system
                    _combatEffects.OnMobDamaged(mob, currentTime);

                    _combatEffects.AddDamageNumber(
                        damage,
                        mob.MovementInfo?.X ?? 0,
                        (mob.MovementInfo?.Y ?? 0) - 30,
                        damage > 100,
                        false,
                        currentTime,
                        hitCount);
                }

                // Queue mob for death (can't modify collection during iteration)
                if (died)
                {
                    mobsToKill.Add(mob);
                }

                hitCount++;
            }

            // Kill mobs after iteration is complete
            foreach (var mob in mobsToKill)
            {
                HandleMobDeath(mob, currentTime);
            }

            return hitCount > 0;
        }

        /// <summary>
        /// TryDoingShoot - Ranged projectile attack (matching CUserLocal::TryDoingShoot from client)
        /// </summary>
        public bool TryDoingShoot(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            // Trigger shooting animation
            _player.TriggerSkillAnimation("shoot1");

            // Create a basic projectile
            var proj = new ActiveProjectile
            {
                Id = _nextProjectileId++,
                SkillId = 0, // Basic attack
                SkillLevel = 1,
                Data = CreateBasicProjectileData(),
                LevelData = null,
                X = _player.X,
                Y = _player.Y - 25, // Hand height
                FacingRight = _player.FacingRight,
                SpawnTime = currentTime,
                OwnerId = 0,
                OwnerX = _player.X,
                OwnerY = _player.Y
            };

            // Set velocity
            float speed = 8.0f;
            proj.VelocityX = _player.FacingRight ? speed : -speed;
            proj.VelocityY = 0;

            _projectiles.Add(proj);

            return true;
        }

        /// <summary>
        /// TryDoingMagicAttack - Magic attack with effect (matching CUserLocal::TryDoingMagicAttack from client)
        /// </summary>
        public bool TryDoingMagicAttack(int currentTime)
        {
            // Cannot attack while on ladder/rope/swimming
            if (!_player.CanAttack)
                return false;

            if (_mobPool == null)
                return false;

            // Trigger magic attack animation
            _player.TriggerSkillAnimation("swingO1");

            // Consume MP for magic attack
            int mpCost = 10;
            if (_player.MP < mpCost)
                return false;

            _player.MP -= mpCost;

            // Magic attack has larger range but single target
            int hitWidth = 120;
            int hitHeight = 80;
            int offsetX = _player.FacingRight ? 20 : -(hitWidth + 20);

            var worldHitbox = new Rectangle(
                (int)_player.X + offsetX,
                (int)_player.Y - hitHeight - 10,
                hitWidth,
                hitHeight);

            MobItem closestMob = null;
            float closestDist = float.MaxValue;

            foreach (var mob in _mobPool.ActiveMobs)
            {
                // Skip dead or removed mobs
                if (mob?.AI == null || mob.AI.IsDead)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!worldHitbox.Intersects(mobHitbox))
                    continue;

                float dist = Math.Abs((mob.MovementInfo?.X ?? 0) - _player.X);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestMob = mob;
                }
            }

            if (closestMob == null)
                return false;

            // Calculate magic damage (higher than melee but single target)
            int damage = CalculateBasicDamage() + 50;
            bool isCritical = new Random().Next(100) < 20; // 20% crit chance
            if (isCritical)
                damage = (int)(damage * 1.5f);

            bool died = closestMob.ApplyDamage(damage, currentTime, isCritical, _player.X, _player.Y);

            // Apply knockback effect if mob didn't die
            if (!died && closestMob.MovementInfo != null)
            {
                float knockbackForce = 4f + (damage / 80f); // Magic has less knockback
                knockbackForce = Math.Min(knockbackForce, 8f); // Cap at 8
                bool knockRight = _player.FacingRight;
                closestMob.MovementInfo.ApplyKnockback(knockbackForce, knockRight);
            }

            if (_combatEffects != null)
            {
                // Notify HP bar system
                _combatEffects.OnMobDamaged(closestMob, currentTime);

                _combatEffects.AddDamageNumber(
                    damage,
                    closestMob.MovementInfo?.X ?? 0,
                    (closestMob.MovementInfo?.Y ?? 0) - 30,
                    isCritical,
                    false,
                    currentTime,
                    0);
            }

            // If mob died, notify mob pool
            if (died && _mobPool != null)
            {
                HandleMobDeath(closestMob, currentTime);
            }

            return true;
        }

        /// <summary>
        /// Handles mob death effects, sounds, and pool removal
        /// </summary>
        private void HandleMobDeath(MobItem mob, int currentTime)
        {
            if (mob == null)
                return;

            // Play death sound FIRST, before any cleanup
            mob.PlayDieSound();

            // Trigger death effects
            if (_combatEffects != null)
            {
                _combatEffects.AddDeathEffectForMob(mob, currentTime);
                _combatEffects.RemoveMobHPBar(mob.PoolId);
            }

            // Remove from mob pool LAST
            _mobPool?.KillMob(mob, MobDeathType.Killed);
        }

        /// <summary>
        /// TryDoingRandomAttack - Randomly selects and performs one of the three attack types
        /// </summary>
        public bool TryDoingRandomAttack(int currentTime)
        {
            int attackType = new Random().Next(3);

            return attackType switch
            {
                0 => TryDoingMeleeAttack(currentTime),
                1 => TryDoingShoot(currentTime),
                2 => TryDoingMagicAttack(currentTime),
                _ => TryDoingMeleeAttack(currentTime)
            };
        }

        /// <summary>
        /// Calculate basic attack damage without skill
        /// </summary>
        private int CalculateBasicDamage()
        {
            int baseAttack = _player.Build?.Attack ?? 10;
            var weapon = _player.Build?.GetWeapon();
            if (weapon != null)
            {
                baseAttack += weapon.Attack;
            }

            // Variance 0.9 - 1.1
            var random = new Random();
            float variance = 0.9f + (float)random.NextDouble() * 0.2f;

            return Math.Max(1, (int)(baseAttack * variance));
        }

        /// <summary>
        /// Create basic projectile data for shooting
        /// </summary>
        private ProjectileData CreateBasicProjectileData()
        {
            return new ProjectileData
            {
                Speed = 8.0f,
                Piercing = false,
                MaxHits = 1,
                LifeTime = 2000 // 2 seconds
            };
        }

        #endregion

        #region Attack Processing

        private void ProcessMeleeAttack(SkillData skill, int level, int currentTime)
        {
            if (_mobPool == null)
                return;

            var levelData = skill.GetLevel(level);
            var hitbox = skill.GetAttackRange(level, _player.FacingRight);

            // Adjust hitbox to world position
            var worldHitbox = new Rectangle(
                (int)_player.X + hitbox.X,
                (int)_player.Y + hitbox.Y,
                hitbox.Width,
                hitbox.Height);

            int hitCount = 0;
            int maxTargets = levelData?.MobCount ?? 1;
            int attackCount = levelData?.AttackCount ?? 1;

            foreach (var mob in _mobPool.ActiveMobs)
            {
                if (hitCount >= maxTargets)
                    break;

                if (mob?.AI == null || mob.AI.State == MobAIState.Death)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!worldHitbox.Intersects(mobHitbox))
                    continue;

                // Calculate damage for each hit
                for (int i = 0; i < attackCount; i++)
                {
                    int damage = CalculateSkillDamage(skill, level);

                    // Apply damage with sound effects and aggro
                    mob.ApplyDamage(damage, currentTime, damage > levelData.Damage, _player.X, _player.Y);

                    // Show damage number
                    if (_combatEffects != null)
                    {
                        _combatEffects.AddDamageNumber(
                            damage,
                            mob.MovementInfo?.X ?? 0,
                            (mob.MovementInfo?.Y ?? 0) - 30,
                            damage > levelData.Damage,
                            false,
                            currentTime,
                            i);
                    }

                    // Show hit effect
                    if (skill.HitEffect != null)
                    {
                        SpawnHitEffect(skill, mob.MovementInfo?.X ?? 0, (mob.MovementInfo?.Y ?? 0) - 20, currentTime);
                    }

                    // Check death
                    if (mob.AI.State == MobAIState.Death)
                        break;
                }

                hitCount++;
            }
        }

        private Rectangle GetMobHitbox(MobItem mob)
        {
            if (mob?.MovementInfo == null)
                return Rectangle.Empty;

            return new Rectangle(
                (int)mob.MovementInfo.X - 20,
                (int)mob.MovementInfo.Y - 50,
                40,
                50);
        }

        private int CalculateSkillDamage(SkillData skill, int level)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return 1;

            // Base attack calculation
            int baseAttack = _player.Build?.Attack ?? 10;
            var weapon = _player.Build?.GetWeapon();
            if (weapon != null)
            {
                baseAttack += weapon.Attack;
            }

            // Apply skill damage multiplier
            float multiplier = levelData.Damage / 100f;

            // Variance
            var random = new Random();
            float variance = 0.9f + (float)random.NextDouble() * 0.2f;

            int damage = (int)(baseAttack * multiplier * variance);

            return Math.Max(1, damage);
        }

        /// <summary>
        /// Spawn a hit effect at the specified position
        /// </summary>
        private void SpawnHitEffect(SkillData skill, float x, float y, int currentTime)
        {
            if (skill.HitEffect == null)
                return;

            var hitEffect = new ActiveHitEffect
            {
                SkillId = skill.SkillId,
                X = x,
                Y = y,
                StartTime = currentTime,
                Animation = skill.HitEffect,
                FacingRight = _player.FacingRight
            };

            _hitEffects.Add(hitEffect);
        }

        #endregion

        #region Projectile System

        private void SpawnProjectile(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            int bulletCount = levelData?.BulletCount ?? 1;

            for (int i = 0; i < bulletCount; i++)
            {
                var proj = new ActiveProjectile
                {
                    Id = _nextProjectileId++,
                    SkillId = skill.SkillId,
                    SkillLevel = level,
                    Data = skill.Projectile,
                    LevelData = levelData,
                    X = _player.X,
                    Y = _player.Y - 20, // Adjust to hand height
                    FacingRight = _player.FacingRight,
                    SpawnTime = currentTime,
                    OwnerId = 0,
                    OwnerX = _player.X,
                    OwnerY = _player.Y
                };

                // Set velocity
                float speed = skill.Projectile.Speed;
                proj.VelocityX = _player.FacingRight ? speed : -speed;
                proj.VelocityY = 0;

                // Spread for multiple projectiles
                if (bulletCount > 1)
                {
                    float spreadAngle = (i - (bulletCount - 1) / 2f) * 10f * MathF.PI / 180f;
                    proj.VelocityX = speed * MathF.Cos(spreadAngle) * (_player.FacingRight ? 1 : -1);
                    proj.VelocityY = speed * MathF.Sin(spreadAngle);
                }

                _projectiles.Add(proj);
            }
        }

        private void UpdateProjectiles(int currentTime, float deltaTime)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var proj = _projectiles[i];
                proj.Update(deltaTime, currentTime);

                // Check for expired
                if (proj.IsExpired)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                // Skip if exploding (just playing animation)
                if (proj.IsExploding)
                    continue;

                // Check mob collisions
                if (_mobPool != null)
                {
                    CheckProjectileCollisions(proj, currentTime);
                }
            }
        }

        private void CheckProjectileCollisions(ActiveProjectile proj, int currentTime)
        {
            var projHitbox = proj.GetHitbox();
            int maxTargets = proj.LevelData?.MobCount ?? 1;

            foreach (var mob in _mobPool.ActiveMobs)
            {
                if (!proj.CanHitMob(mob.PoolId))
                    continue;

                if (mob?.AI == null || mob.AI.State == MobAIState.Death)
                    continue;

                var mobHitbox = GetMobHitbox(mob);
                if (!projHitbox.Intersects(mobHitbox))
                    continue;

                // Hit!
                proj.RegisterHit(mob.PoolId);

                // Calculate damage
                int attackCount = proj.LevelData?.AttackCount ?? 1;
                for (int i = 0; i < attackCount; i++)
                {
                    var skill = GetSkillData(proj.SkillId);
                    int damage = CalculateSkillDamage(skill, proj.SkillLevel);

                    mob.ApplyDamage(damage, currentTime, false, _player.X, _player.Y);

                    if (_combatEffects != null)
                    {
                        _combatEffects.AddDamageNumber(
                            damage,
                            mob.MovementInfo?.X ?? 0,
                            (mob.MovementInfo?.Y ?? 0) - 30,
                            false,
                            false,
                            currentTime,
                            i);
                    }

                    // Spawn hit effect at mob position
                    if (skill?.HitEffect != null)
                    {
                        SpawnHitEffect(skill, mob.MovementInfo?.X ?? 0, (mob.MovementInfo?.Y ?? 0) - 20, currentTime);
                    }
                }

                OnProjectileHit?.Invoke(proj, mob);

                // Check if should stop (non-piercing already handled in RegisterHit)
                if (proj.IsExploding || proj.IsExpired)
                    break;

                if (proj.HitCount >= maxTargets)
                    break;
            }
        }

        public IReadOnlyList<ActiveProjectile> ActiveProjectiles => _projectiles;

        #endregion

        #region Buff System

        private void ApplyBuff(SkillData skill, int level, int currentTime)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null || levelData.Time <= 0)
                return;

            // Check for existing buff of same skill
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                if (_buffs[i].SkillId == skill.SkillId)
                {
                    OnBuffExpired?.Invoke(_buffs[i]);
                    _buffs.RemoveAt(i);
                }
            }

            var buff = new ActiveBuff
            {
                SkillId = skill.SkillId,
                Level = level,
                StartTime = currentTime,
                Duration = levelData.Time * 1000, // Convert seconds to ms
                SkillData = skill,
                LevelData = levelData
            };

            _buffs.Add(buff);
            OnBuffApplied?.Invoke(buff);

            // Apply buff effects to player stats
            ApplyBuffStats(buff, true);
        }

        private void ApplyBuffStats(ActiveBuff buff, bool apply)
        {
            var levelData = buff.LevelData;
            if (levelData == null || _player.Build == null)
                return;

            int modifier = apply ? 1 : -1;

            // Apply stat modifiers
            _player.Build.Attack += levelData.PAD * modifier;
            _player.Build.Defense += levelData.PDD * modifier;
            // Would also apply: MAD, MDD, ACC, EVA, Speed, Jump
        }

        private void UpdateBuffs(int currentTime)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                var buff = _buffs[i];
                if (buff.IsExpired(currentTime))
                {
                    // Remove buff effects
                    ApplyBuffStats(buff, false);
                    _buffs.RemoveAt(i);
                    OnBuffExpired?.Invoke(buff);
                }
            }
        }

        public IReadOnlyList<ActiveBuff> ActiveBuffs => _buffs;

        /// <summary>
        /// Get total buff stat bonus
        /// </summary>
        public int GetBuffStat(BuffStatType stat)
        {
            int total = 0;
            foreach (var buff in _buffs)
            {
                var data = buff.LevelData;
                if (data == null) continue;

                total += stat switch
                {
                    BuffStatType.Attack => data.PAD,
                    BuffStatType.MagicAttack => data.MAD,
                    BuffStatType.Defense => data.PDD,
                    BuffStatType.MagicDefense => data.MDD,
                    BuffStatType.Accuracy => data.ACC,
                    BuffStatType.Avoidability => data.EVA,
                    BuffStatType.Speed => data.Speed,
                    BuffStatType.Jump => data.Jump,
                    _ => 0
                };
            }
            return total;
        }

        /// <summary>
        /// Check if buff type is active
        /// </summary>
        public bool HasBuff(int skillId)
        {
            foreach (var buff in _buffs)
            {
                if (buff.SkillId == skillId)
                    return true;
            }
            return false;
        }

        #endregion

        #region Heal

        private void ApplyHeal(SkillData skill, int level)
        {
            var levelData = skill.GetLevel(level);
            if (levelData == null)
                return;

            // Calculate heal amount
            int hpHeal = levelData.HP;
            int mpHeal = levelData.MP;

            // Some heals are percentage based
            if (levelData.X > 0)
            {
                hpHeal = _player.MaxHP * levelData.X / 100;
            }

            // Apply heal
            if (hpHeal > 0)
            {
                _player.HP = Math.Min(_player.MaxHP, _player.HP + hpHeal);
            }

            if (mpHeal > 0)
            {
                _player.MP = Math.Min(_player.MaxMP, _player.MP + mpHeal);
            }
        }

        #endregion

        #region Passive Skills

        /// <summary>
        /// Get passive skill bonus
        /// </summary>
        public int GetPassiveBonus(BuffStatType stat)
        {
            int total = 0;

            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive)
                    continue;

                int level = GetSkillLevel(skill.SkillId);
                if (level <= 0)
                    continue;

                var levelData = skill.GetLevel(level);
                if (levelData == null)
                    continue;

                total += stat switch
                {
                    BuffStatType.Attack => levelData.PAD,
                    BuffStatType.MagicAttack => levelData.MAD,
                    BuffStatType.Defense => levelData.PDD,
                    BuffStatType.MagicDefense => levelData.MDD,
                    BuffStatType.Accuracy => levelData.ACC,
                    BuffStatType.Avoidability => levelData.EVA,
                    BuffStatType.CriticalRate => levelData.CriticalRate,
                    BuffStatType.Booster => levelData.X, // Usually attack speed
                    _ => 0
                };
            }

            return total;
        }

        /// <summary>
        /// Get mastery from passive skills
        /// </summary>
        public int GetMastery()
        {
            int mastery = 10; // Base mastery

            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive)
                    continue;

                int level = GetSkillLevel(skill.SkillId);
                if (level <= 0)
                    continue;

                var levelData = skill.GetLevel(level);
                if (levelData?.Mastery > mastery)
                {
                    mastery = levelData.Mastery;
                }
            }

            return mastery;
        }

        #endregion

        #region Update

        public void Update(int currentTime, float deltaTime)
        {
            // Update current cast
            if (_currentCast != null)
            {
                // Check if cast animation is complete
                var skill = _currentCast.SkillData;
                if (skill?.Effect != null)
                {
                    if (skill.Effect.IsComplete(_currentCast.AnimationTime))
                    {
                        _currentCast.IsComplete = true;
                    }
                }
                else
                {
                    // No effect animation, complete after delay
                    if (_currentCast.AnimationTime > 500)
                    {
                        _currentCast.IsComplete = true;
                    }
                }
            }

            // Update projectiles
            UpdateProjectiles(currentTime, deltaTime);

            // Update buffs
            UpdateBuffs(currentTime);

            // Update hit effects (remove expired)
            UpdateHitEffects(currentTime);
        }

        private void UpdateHitEffects(int currentTime)
        {
            for (int i = _hitEffects.Count - 1; i >= 0; i--)
            {
                if (_hitEffects[i].IsExpired(currentTime))
                {
                    _hitEffects.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Draw

        public void DrawProjectiles(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY,
            int centerX, int centerY, int currentTime)
        {
            foreach (var proj in _projectiles)
            {
                DrawProjectile(spriteBatch, proj, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        private void DrawProjectile(SpriteBatch spriteBatch, ActiveProjectile proj,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            SkillAnimation anim;
            int animTime;

            if (proj.IsExploding)
            {
                anim = proj.Data.ExplosionAnimation;
                animTime = currentTime - proj.ExplodeTime;
            }
            else
            {
                anim = proj.Data.Animation;
                animTime = currentTime - proj.SpawnTime;
            }

            if (anim == null)
                return;

            var frame = anim.GetFrameAtTime(animTime);
            if (frame?.Texture == null)
                return;

            int screenX = (int)proj.X - mapShiftX + centerX;
            int screenY = (int)proj.Y - mapShiftY + centerY;

            bool shouldFlip = (!proj.FacingRight) ^ frame.Flip;

            // Use DrawBackground which handles the texture internally
            frame.Texture.DrawBackground(spriteBatch, null, null,
                screenX - frame.Origin.X, screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        public void DrawEffects(SpriteBatch spriteBatch, int mapShiftX, int mapShiftY,
            int centerX, int centerY, int currentTime)
        {
            // Draw current cast effect
            if (_currentCast != null && !_currentCast.IsComplete)
            {
                DrawCastEffect(spriteBatch, _currentCast, mapShiftX, mapShiftY, centerX, centerY);
            }

            // Draw affected effects for active buffs (looping on character)
            foreach (var buff in _buffs)
            {
                DrawAffectedEffect(spriteBatch, buff, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }

            // Draw hit effects
            foreach (var hitEffect in _hitEffects)
            {
                DrawHitEffect(spriteBatch, hitEffect, mapShiftX, mapShiftY, centerX, centerY, currentTime);
            }
        }

        /// <summary>
        /// Draw a hit effect at its position
        /// </summary>
        private void DrawHitEffect(SpriteBatch spriteBatch, ActiveHitEffect hitEffect,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            if (hitEffect.Animation == null)
                return;

            var frame = hitEffect.Animation.GetFrameAtTime(hitEffect.AnimationTime(currentTime));
            if (frame?.Texture == null)
                return;

            int screenX = (int)hitEffect.X - mapShiftX + centerX;
            int screenY = (int)hitEffect.Y - mapShiftY + centerY;

            bool shouldFlip = (!hitEffect.FacingRight) ^ frame.Flip;

            frame.Texture.DrawBackground(spriteBatch, null, null,
                screenX - frame.Origin.X, screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        private void DrawCastEffect(SpriteBatch spriteBatch, SkillCastInfo cast,
            int mapShiftX, int mapShiftY, int centerX, int centerY)
        {
            var effect = cast.SkillData?.Effect;
            if (effect == null)
                return;

            var frame = effect.GetFrameAtTime(cast.AnimationTime);
            if (frame?.Texture == null)
                return;

            int screenX = (int)cast.CasterX - mapShiftX + centerX;
            int screenY = (int)cast.CasterY - mapShiftY + centerY;

            bool shouldFlip = (!cast.FacingRight) ^ frame.Flip;

            // Use DrawBackground which handles the texture internally
            frame.Texture.DrawBackground(spriteBatch, null, null,
                screenX - frame.Origin.X, screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        /// <summary>
        /// Draw affected effect for active buff (loops while buff is active)
        /// </summary>
        private void DrawAffectedEffect(SpriteBatch spriteBatch, ActiveBuff buff,
            int mapShiftX, int mapShiftY, int centerX, int centerY, int currentTime)
        {
            var affected = buff.SkillData?.AffectedEffect;
            if (affected == null)
                return;

            // Calculate animation time - loop continuously
            int animTime = currentTime - buff.StartTime;
            var frame = affected.GetFrameAtTime(animTime);
            if (frame?.Texture == null)
                return;

            // Position at player
            int screenX = (int)_player.X - mapShiftX + centerX;
            int screenY = (int)_player.Y - mapShiftY + centerY;

            bool shouldFlip = (!_player.FacingRight) ^ frame.Flip;

            // Use DrawBackground which handles the texture internally
            frame.Texture.DrawBackground(spriteBatch, null, null,
                screenX - frame.Origin.X, screenY - frame.Origin.Y,
                Color.White, shouldFlip, null);
        }

        #endregion

        #region Utility

        private SkillData GetSkillData(int skillId)
        {
            return _loader.LoadSkill(skillId);
        }

        public IEnumerable<SkillData> GetLearnedSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (GetSkillLevel(skill.SkillId) > 0)
                {
                    yield return skill;
                }
            }
        }

        public IEnumerable<SkillData> GetActiveSkills()
        {
            foreach (var skill in _availableSkills)
            {
                if (!skill.IsPassive && !skill.Invisible && GetSkillLevel(skill.SkillId) > 0)
                {
                    yield return skill;
                }
            }
        }

        /// <summary>
        /// Full clear - clears everything including skill levels and hotkeys.
        /// Use when completely disposing the skill system.
        /// </summary>
        public void Clear()
        {
            _projectiles.Clear();
            _hitEffects.Clear();

            // Remove all buff effects
            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
            }
            _buffs.Clear();

            _cooldowns.Clear();
            _currentCast = null;
            _skillLevels.Clear();
            _skillHotkeys.Clear();
            _availableSkills.Clear();
        }

        /// <summary>
        /// Clear map-specific state but preserve persistent data.
        /// Preserves: skill levels, hotkeys, available skills, cooldowns.
        /// Clears: active projectiles, hit effects, current cast, buffs.
        /// </summary>
        public void ClearMapState()
        {
            // Clear active combat state
            _projectiles.Clear();
            _hitEffects.Clear();
            _currentCast = null;

            // Remove all buff effects (buffs don't persist across maps in MapleStory)
            foreach (var buff in _buffs)
            {
                ApplyBuffStats(buff, false);
            }
            _buffs.Clear();

            // Clear map-specific references
            _mobPool = null;
            _combatEffects = null;

            // Note: We intentionally do NOT clear:
            // - _skillLevels (learned skills persist)
            // - _skillHotkeys (hotkey bindings persist)
            // - _availableSkills (job skills persist)
            // - _cooldowns (debatable - could reset or persist)
        }

        #endregion
    }
}
