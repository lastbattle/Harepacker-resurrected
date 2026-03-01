using System;
using System.Collections.Generic;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;

namespace HaCreator.MapSimulator.Character.Skills
{
    #region Enums

    /// <summary>
    /// Skill type classification
    /// </summary>
    public enum SkillType
    {
        Attack,         // Direct damage skill
        Magic,          // Magic attack
        Summon,         // Summons a creature
        Buff,           // Applies buff to self
        PartyBuff,      // Applies buff to party
        Heal,           // Heals HP
        Passive,        // Always active, no casting
        Movement,       // Teleport, flash jump, etc.
        Debuff          // Applies negative effect to enemy
    }

    /// <summary>
    /// Element type for skills
    /// </summary>
    public enum SkillElement
    {
        Physical,
        Fire,
        Ice,
        Lightning,
        Poison,
        Holy,
        Dark
    }

    /// <summary>
    /// Skill target type
    /// </summary>
    public enum SkillTarget
    {
        SingleEnemy,
        MultipleEnemy,
        Self,
        Party,
        Ground,         // Ground-targeted AOE
        Direction       // Directional skill
    }

    /// <summary>
    /// Attack type for animation selection
    /// </summary>
    public enum SkillAttackType
    {
        Melee,          // Close range
        Ranged,         // Projectile
        Magic,          // Magic casting
        Summon,         // Summon skill
        Special         // Unique animation
    }

    /// <summary>
    /// Buff stat type
    /// </summary>
    public enum BuffStatType
    {
        Attack,
        MagicAttack,
        Defense,
        MagicDefense,
        Accuracy,
        Avoidability,
        Speed,
        Jump,
        MaxHP,
        MaxMP,
        CriticalRate,
        DamageReduction,
        Invincible,
        Stance,         // Knockback resistance
        Booster         // Attack speed boost
    }

    /// <summary>
    /// Projectile behavior
    /// </summary>
    public enum ProjectileBehavior
    {
        Straight,       // Travels straight
        Homing,         // Homes in on target
        Falling,        // Falls with gravity
        Bouncing,       // Bounces off surfaces
        Piercing,       // Goes through enemies
        Exploding       // Explodes on impact
    }

    #endregion

    #region Skill Level Data

    /// <summary>
    /// Data for a specific skill level
    /// </summary>
    public class SkillLevelData
    {
        public int Level { get; set; }

        // Damage
        public int Damage { get; set; }              // Damage % (e.g., 150 = 150%)
        public int AttackCount { get; set; } = 1;    // Number of hits
        public int MobCount { get; set; } = 1;       // Max targets

        // Costs
        public int MpCon { get; set; }               // MP consumption
        public int HpCon { get; set; }               // HP consumption (rare)
        public int ItemCon { get; set; }             // Item consumption count
        public int ItemConNo { get; set; }           // Item ID consumed

        // Timing
        public int Cooldown { get; set; }            // Cooldown in ms
        public int Time { get; set; }                // Buff duration in seconds

        // Range
        public int Range { get; set; }               // Attack range
        public int RangeR { get; set; }              // Range right
        public int RangeL { get; set; }              // Range left (usually same as RangeR)
        public int RangeY { get; set; }              // Vertical range

        // Buff stats
        public int PAD { get; set; }                 // Physical Attack boost
        public int MAD { get; set; }                 // Magic Attack boost
        public int PDD { get; set; }                 // Physical Defense boost
        public int MDD { get; set; }                 // Magic Defense boost
        public int ACC { get; set; }                 // Accuracy boost
        public int EVA { get; set; }                 // Avoidability boost
        public int Speed { get; set; }               // Speed boost
        public int Jump { get; set; }                // Jump boost

        // Heal
        public int HP { get; set; }                  // HP recovery
        public int MP { get; set; }                  // MP recovery

        // Special
        public int Prop { get; set; }                // Probability % for effect
        public int X { get; set; }                   // Generic value X
        public int Y { get; set; }                   // Generic value Y
        public int Z { get; set; }                   // Generic value Z

        // Projectile
        public int BulletCount { get; set; } = 1;    // Projectiles per attack
        public int BulletSpeed { get; set; }         // Projectile speed

        // Mastery
        public int Mastery { get; set; }             // Mastery %
        public int CriticalRate { get; set; }        // Critical rate boost

        // Requirements
        public int RequiredLevel { get; set; }       // Level required
        public int RequiredSkill { get; set; }       // Prerequisite skill ID
        public int RequiredSkillLevel { get; set; }  // Required level of prerequisite
    }

    #endregion

    #region Skill Animation

    /// <summary>
    /// Single frame of skill effect animation
    /// </summary>
    public class SkillFrame
    {
        public IDXObject Texture { get; set; }
        public Point Origin { get; set; }
        public int Delay { get; set; } = 100;
        public Rectangle Bounds { get; set; }
        public bool Flip { get; set; }
    }

    /// <summary>
    /// Skill effect animation
    /// </summary>
    public class SkillAnimation
    {
        public string Name { get; set; }
        public List<SkillFrame> Frames { get; set; } = new();
        public int TotalDuration { get; private set; }
        public bool Loop { get; set; } = false;
        public Point Origin { get; set; }           // Animation origin relative to caster
        public int ZOrder { get; set; } = 0;        // Draw order

        public void CalculateDuration()
        {
            TotalDuration = 0;
            foreach (var frame in Frames)
            {
                TotalDuration += frame.Delay;
            }
        }

        public SkillFrame GetFrameAtTime(int timeMs)
        {
            if (Frames.Count == 0) return null;

            if (TotalDuration == 0) CalculateDuration();
            if (TotalDuration == 0) return Frames[0];

            int time = Loop ? (timeMs % TotalDuration) : Math.Min(timeMs, TotalDuration - 1);
            int elapsed = 0;

            foreach (var frame in Frames)
            {
                elapsed += frame.Delay;
                if (time < elapsed)
                    return frame;
            }

            return Frames[^1];
        }

        public bool IsComplete(int timeMs)
        {
            return !Loop && timeMs >= TotalDuration;
        }
    }

    #endregion

    #region Projectile Data

    /// <summary>
    /// Projectile/Ball data
    /// </summary>
    public class ProjectileData
    {
        public int SkillId { get; set; }
        public SkillAnimation Animation { get; set; }
        public SkillAnimation HitAnimation { get; set; }

        public ProjectileBehavior Behavior { get; set; } = ProjectileBehavior.Straight;
        public float Speed { get; set; } = 400f;
        public float Gravity { get; set; } = 0f;
        public float LifeTime { get; set; } = 2000f;  // Max life in ms
        public bool Piercing { get; set; } = false;
        public int MaxHits { get; set; } = 1;

        // Explosion
        public float ExplosionRadius { get; set; } = 0;
        public SkillAnimation ExplosionAnimation { get; set; }
    }

    #endregion

    #region Skill Definition

    /// <summary>
    /// Complete skill definition
    /// </summary>
    public class SkillData
    {
        public int SkillId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int MaxLevel { get; set; }

        // Classification
        public SkillType Type { get; set; }
        public SkillElement Element { get; set; } = SkillElement.Physical;
        public SkillTarget Target { get; set; }
        public SkillAttackType AttackType { get; set; }

        // Job info
        public int Job { get; set; }                 // Job ID (e.g., 100 = Warrior)
        public bool IsFourthJob { get; set; }

        // Flags
        public bool IsPassive { get; set; }
        public bool IsBuff { get; set; }
        public bool IsAttack { get; set; }
        public bool IsHeal { get; set; }
        public bool IsSummon { get; set; }
        public bool Invisible { get; set; }          // Hidden skill
        public bool MasterOnly { get; set; }         // Only usable at max level

        // Level data
        public Dictionary<int, SkillLevelData> Levels { get; set; } = new();

        // Animations
        public IDXObject Icon { get; set; }
        public IDXObject IconDisabled { get; set; }
        public IDXObject IconMouseOver { get; set; }
        public SkillAnimation Effect { get; set; }           // Effect on caster
        public SkillAnimation HitEffect { get; set; }        // Effect on target
        public SkillAnimation AffectedEffect { get; set; }   // Effect while buff active
        public ProjectileData Projectile { get; set; }       // Ball/projectile

        // Action
        public string ActionName { get; set; }       // Animation action to play

        /// <summary>
        /// Get data for a specific level
        /// </summary>
        public SkillLevelData GetLevel(int level)
        {
            level = Math.Clamp(level, 1, MaxLevel);
            return Levels.TryGetValue(level, out var data) ? data : null;
        }

        /// <summary>
        /// Check if skill can be cast
        /// </summary>
        public bool CanCast(int currentMp, int currentHp, int currentLevel, int lastCastTime, int currentTime)
        {
            var levelData = GetLevel(currentLevel);
            if (levelData == null) return false;

            // Check MP
            if (currentMp < levelData.MpCon) return false;

            // Check HP
            if (currentHp < levelData.HpCon) return false;

            // Check cooldown
            if (levelData.Cooldown > 0)
            {
                if (currentTime - lastCastTime < levelData.Cooldown)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get damage multiplier
        /// </summary>
        public float GetDamageMultiplier(int level)
        {
            var levelData = GetLevel(level);
            return levelData != null ? levelData.Damage / 100f : 1f;
        }

        /// <summary>
        /// Get attack range
        /// </summary>
        public Rectangle GetAttackRange(int level, bool facingRight)
        {
            var levelData = GetLevel(level);
            if (levelData == null) return Rectangle.Empty;

            int rangeX = facingRight ? levelData.RangeR : levelData.RangeL;
            if (rangeX == 0) rangeX = levelData.Range;

            return new Rectangle(
                facingRight ? 0 : -rangeX,
                -levelData.RangeY / 2,
                rangeX,
                levelData.RangeY > 0 ? levelData.RangeY : 60);
        }
    }

    #endregion

    #region Active Buff

    /// <summary>
    /// Currently active buff on a character
    /// </summary>
    public class ActiveBuff
    {
        public int SkillId { get; set; }
        public int Level { get; set; }
        public int StartTime { get; set; }
        public int Duration { get; set; }           // Duration in ms
        public SkillData SkillData { get; set; }
        public SkillLevelData LevelData { get; set; }

        public bool IsExpired(int currentTime)
        {
            return currentTime - StartTime >= Duration;
        }

        public int GetRemainingTime(int currentTime)
        {
            return Math.Max(0, Duration - (currentTime - StartTime));
        }

        public float GetRemainingPercent(int currentTime)
        {
            if (Duration <= 0) return 0;
            return (float)GetRemainingTime(currentTime) / Duration;
        }
    }

    #endregion

    #region Active Projectile

    /// <summary>
    /// Active projectile in the world
    /// </summary>
    public class ActiveProjectile
    {
        public int Id { get; set; }
        public int SkillId { get; set; }
        public int SkillLevel { get; set; }
        public ProjectileData Data { get; set; }
        public SkillLevelData LevelData { get; set; }

        // Position and movement
        public float X { get; set; }
        public float Y { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public bool FacingRight { get; set; }

        // State
        public int SpawnTime { get; set; }
        public int HitCount { get; set; }
        public List<int> HitMobIds { get; set; } = new();
        public bool IsExpired { get; set; }
        public bool IsExploding { get; set; }
        public int ExplodeTime { get; set; }

        // Owner
        public int OwnerId { get; set; }
        public float OwnerX { get; set; }            // For homing reference
        public float OwnerY { get; set; }

        public void Update(float deltaTime, int currentTime)
        {
            if (IsExpired) return;

            // Check lifetime
            if (currentTime - SpawnTime >= Data.LifeTime)
            {
                IsExpired = true;
                return;
            }

            // Update explosion animation
            if (IsExploding)
            {
                if (Data.ExplosionAnimation?.IsComplete(currentTime - ExplodeTime) == true)
                {
                    IsExpired = true;
                }
                return;
            }

            // Apply gravity
            VelocityY += Data.Gravity * deltaTime;

            // Update position
            X += VelocityX * deltaTime;
            Y += VelocityY * deltaTime;
        }

        public Rectangle GetHitbox()
        {
            // Simple hitbox based on projectile
            return new Rectangle((int)X - 10, (int)Y - 10, 20, 20);
        }

        public void Explode(int currentTime)
        {
            if (Data.ExplosionRadius <= 0)
            {
                IsExpired = true;
                return;
            }

            IsExploding = true;
            ExplodeTime = currentTime;
            VelocityX = 0;
            VelocityY = 0;
        }

        public bool CanHitMob(int mobId)
        {
            if (HitMobIds.Contains(mobId)) return false;
            if (!Data.Piercing && HitCount >= Data.MaxHits) return false;
            return true;
        }

        public void RegisterHit(int mobId)
        {
            HitMobIds.Add(mobId);
            HitCount++;

            if (!Data.Piercing && HitCount >= Data.MaxHits)
            {
                // Non-piercing projectile hit something
                Explode(Environment.TickCount);
            }
        }
    }

    #endregion

    #region Skill Cast Info

    /// <summary>
    /// Information about a skill being cast
    /// </summary>
    public class SkillCastInfo
    {
        public int SkillId { get; set; }
        public int Level { get; set; }
        public SkillData SkillData { get; set; }
        public SkillLevelData LevelData { get; set; }

        public int CastTime { get; set; }
        public int CasterId { get; set; }
        public float CasterX { get; set; }
        public float CasterY { get; set; }
        public bool FacingRight { get; set; }

        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public List<int> TargetMobIds { get; set; } = new();

        public bool IsComplete { get; set; }
        public int AnimationTime => Environment.TickCount - CastTime;
    }

    #endregion

    #region Hit Effect

    /// <summary>
    /// Active hit effect displayed when a skill hits a mob
    /// </summary>
    public class ActiveHitEffect
    {
        public int SkillId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int StartTime { get; set; }
        public SkillAnimation Animation { get; set; }
        public bool FacingRight { get; set; }

        public int AnimationTime(int currentTime) => currentTime - StartTime;

        public bool IsExpired(int currentTime)
        {
            if (Animation == null) return true;
            return Animation.IsComplete(AnimationTime(currentTime));
        }
    }

    #endregion

    #region Job Skill Book

    /// <summary>
    /// Collection of skills for a job
    /// </summary>
    public class JobSkillBook
    {
        public int JobId { get; set; }
        public string JobName { get; set; }
        public Dictionary<int, SkillData> Skills { get; set; } = new();

        public IEnumerable<SkillData> GetPassiveSkills()
        {
            foreach (var skill in Skills.Values)
            {
                if (skill.IsPassive)
                    yield return skill;
            }
        }

        public IEnumerable<SkillData> GetActiveSkills()
        {
            foreach (var skill in Skills.Values)
            {
                if (!skill.IsPassive && !skill.Invisible)
                    yield return skill;
            }
        }

        public IEnumerable<SkillData> GetBuffSkills()
        {
            foreach (var skill in Skills.Values)
            {
                if (skill.IsBuff)
                    yield return skill;
            }
        }
    }

    #endregion
}
