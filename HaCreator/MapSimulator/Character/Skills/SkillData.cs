using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.UI;
using HaSharedLibrary.Render.DX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
        Strength,
        Dexterity,
        Intelligence,
        Luck,
        Craft,
        Attack,
        AttackPercent,
        MagicAttack,
        MagicAttackPercent,
        Defense,
        MagicDefense,
        DefensePercent,
        MagicDefensePercent,
        Accuracy,
        AccuracyPercent,
        Avoidability,
        AvoidabilityPercent,
        Speed,
        SpeedPercent,
        SpeedMax,
        Jump,
        MaxHP,
        MaxMP,
        MaxHPPercent,
        MaxMPPercent,
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

    public enum SummonMovementStyle
    {
        Stationary,
        GroundFollow,
        HoverFollow,
        DriftAroundOwner,
        HoverAroundAnchor
    }

    public enum SummonAssistType
    {
        PeriodicAttack = 1,
        Support = 2,
        TargetedAttack = 3,
        SummonAction = 4,
        ManualAttack = 5,
        OwnerAttackTargeted = 6
    }

    public enum SummonActorState
    {
        Spawn,
        Idle,
        Prepare,
        Attack,
        Hit,
        Die
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
        public int DotDamage { get; set; }           // Damage-over-time % for aura / mist style skills
        public int DotInterval { get; set; }         // Damage-over-time tick interval in seconds
        public int DotTime { get; set; }             // Damage-over-time duration in seconds
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
        public int RangeTop { get; set; }            // Raw top bound from WZ lt.y
        public int RangeBottom { get; set; }         // Raw bottom bound from WZ rb.y

        // Buff stats
        public int PAD { get; set; }                 // Physical Attack boost
        public int MAD { get; set; }                 // Magic Attack boost
        public int PDD { get; set; }                 // Physical Defense boost
        public int MDD { get; set; }                 // Magic Defense boost
        public int STR { get; set; }                 // Strength boost
        public int DEX { get; set; }                 // Dexterity boost
        public int INT { get; set; }                 // Intelligence boost
        public int LUK { get; set; }                 // Luck boost
        public int ACC { get; set; }                 // Accuracy boost
        public int EVA { get; set; }                 // Avoidability boost
        public int Speed { get; set; }               // Speed boost
        public int Jump { get; set; }                // Jump boost

        // Heal
        public int HP { get; set; }                  // HP recovery
        public int MP { get; set; }                  // MP recovery

        // Special
        public int Prop { get; set; }                // Probability % for effect
        public int SubProp { get; set; }             // Secondary probability % for affectedSkillEffect branches
        public int X { get; set; }                   // Generic value X
        public int Y { get; set; }                   // Generic value Y
        public int Z { get; set; }                   // Generic value Z
        public int U { get; set; }                   // Generic value U
        public int V { get; set; }                   // Generic value V
        public int W { get; set; }                   // Generic value W

        // Projectile
        public int BulletCount { get; set; } = 1;    // Projectiles per attack
        public int BulletConsume { get; set; }       // Ammo consumed per cast
        public int ProjectileItemConsume { get; set; } // WZ `itemConsume` ammo/item requirement
        public int BulletSpeed { get; set; }         // Projectile speed
        public List<int> ProjectileSpawnDelaysMs { get; set; } = new();

        // Mastery
        public int Mastery { get; set; }             // Mastery %
        public int CriticalRate { get; set; }        // Critical rate boost
        public int CriticalDamageMin { get; set; }   // Minimum critical damage boost
        public int CriticalDamageMax { get; set; }   // Maximum critical damage boost
        public int DamageReductionRate { get; set; } // damR / indieDamR incoming damage reduction
        public int EnhancedPAD { get; set; }         // Mechanic-only weapon attack boost
        public int EnhancedMAD { get; set; }         // Alias-backed magic attack boost
        public int EnhancedPDD { get; set; }         // Mechanic-only defense boost
        public int EnhancedMDD { get; set; }         // Mechanic-only magic defense boost
        public int EnhancedMaxHP { get; set; }       // Mechanic-only max HP boost
        public int EnhancedMaxMP { get; set; }       // Mechanic-only max MP boost
        public int IndieMaxHP { get; set; }          // Big Bang indie max HP boost
        public int IndieMaxMP { get; set; }          // Big Bang indie max MP boost
        public int MaxHPPercent { get; set; }        // Percentage max HP boost
        public int MaxMPPercent { get; set; }        // Percentage max MP boost
        public int AttackPercent { get; set; }       // padRate percentage attack boost
        public int MagicAttackPercent { get; set; }  // madRate percentage magic attack boost
        public int DefensePercent { get; set; }      // pddR percentage defense boost
        public int MagicDefensePercent { get; set; } // mddR percentage magic defense boost
        public int AccuracyPercent { get; set; }     // accR percentage accuracy boost
        public int AvoidabilityPercent { get; set; } // evaR percentage avoidability boost
        public int SpeedPercent { get; set; }        // speedRate percentage speed boost
        public int SpeedMax { get; set; }            // speedMax max-speed cap boost
        public int AllStat { get; set; }             // Big Bang indie all-stat boost
        public int Craft { get; set; }               // incCraft / hands boost
        public int AbnormalStatusResistance { get; set; } // asrR / indieAsrR
        public int ElementalResistance { get; set; } // terR / indieTerR
        public int ExperienceRate { get; set; }      // expR bonus rate
        public int DropRate { get; set; }            // dropR bonus rate
        public int MesoRate { get; set; }            // mesoR bonus rate
        public int BossDamageRate { get; set; }      // bdR boss damage bonus rate
        public int IgnoreDefenseRate { get; set; }   // ignoreMobpdpR / ignoreMobDamR monster defense or reduction ignore rate
        public List<string> AuthoredPropertyOrder { get; set; } = new();

        // Requirements
        public int RequiredLevel { get; set; }       // Level required
        public int RequiredSkill { get; set; }       // Prerequisite skill ID
        public int RequiredSkillLevel { get; set; }  // Required level of prerequisite

        public SkillLevelData ShallowClone()
        {
            return (SkillLevelData)MemberwiseClone();
        }
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
        public int Z { get; set; }
        public int AlphaStart { get; set; } = 255;
        public int AlphaEnd { get; set; } = 255;
        public bool HasAlphaStart { get; set; }
        public bool HasAlphaEnd { get; set; }
        public int ZoomStart { get; set; }
        public int ZoomEnd { get; set; }
        public bool HasZoomStart { get; set; }
        public bool HasZoomEnd { get; set; }
        public int RotationDegrees { get; set; }
    }

    public readonly record struct AfterimageRenderableLayer(SkillFrame Frame, float Alpha, float Zoom = 1f);

    /// <summary>
    /// Skill effect animation
    /// </summary>
    public class SkillAnimation
    {
        public string Name { get; set; }
        public List<SkillFrame> Frames { get; set; } = new();
        public List<SkillAnimation> VariantAnimations { get; set; } = new();
        public int TotalDuration { get; private set; }
        public bool Loop { get; set; } = false;
        public Point Origin { get; set; }           // Animation origin relative to caster
        public int ZOrder { get; set; } = 0;        // Draw order
        public int? PositionCode { get; set; }      // Optional WZ `pos` anchor selection
        public int? ClientEventDelayMs { get; set; }

        public List<IDXObject> ToTextureFrames()
        {
            if (Frames == null || Frames.Count == 0)
            {
                return null;
            }

            var textures = new List<IDXObject>(Frames.Count);
            for (int i = 0; i < Frames.Count; i++)
            {
                IDXObject texture = Frames[i]?.Texture;
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            return textures.Count > 0 ? textures : null;
        }

        public bool TryGetTextureFrames(out List<IDXObject> textures)
        {
            textures = ToTextureFrames();
            return textures != null && textures.Count > 0;
        }

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
            TryGetFrameAtTime(timeMs, out SkillFrame frame, out _);
            return frame;
        }

        public bool TryGetFrameAtTime(int timeMs, out SkillFrame frame, out int frameElapsedMs)
        {
            frame = null;
            frameElapsedMs = 0;

            if (Frames.Count == 0) return false;

            if (TotalDuration == 0) CalculateDuration();
            if (TotalDuration == 0)
            {
                frame = Frames[0];
                return true;
            }

            int time = Loop ? (timeMs % TotalDuration) : Math.Min(timeMs, TotalDuration - 1);
            int elapsed = 0;

            foreach (var currentFrame in Frames)
            {
                int frameDelay = Math.Max(1, currentFrame.Delay);
                elapsed += frameDelay;
                if (time < elapsed)
                {
                    frameElapsedMs = time - (elapsed - frameDelay);
                    frame = currentFrame;
                    return true;
                }
            }

            frame = Frames[^1];
            frameElapsedMs = Math.Max(0, Math.Min(time, TotalDuration - 1));
            return true;
        }

        public bool IsComplete(int timeMs)
        {
            return !Loop && timeMs >= TotalDuration;
        }
    }

    public sealed class SummonImpactPresentation
    {
        public SkillAnimation Animation { get; set; }
        public int HitAfterMs { get; set; }
        public int? PositionCode { get; set; }
    }

    #endregion

    #region Melee Afterimage

    public class MeleeAfterImageFrameSet
    {
        public List<SkillFrame> Frames { get; set; } = new();
    }

    public class MeleeAfterImageAction
    {
        public Rectangle Range { get; set; } = Rectangle.Empty;
        public Dictionary<int, MeleeAfterImageFrameSet> FrameSets { get; set; } = new();

        public bool HasRange => Range.Width > 0 && Range.Height > 0;
    }

    public class MeleeAfterImageCatalog
    {
        public Dictionary<string, MeleeAfterImageAction> Actions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGetAction(string actionName, out MeleeAfterImageAction action)
        {
            action = null;
            return !string.IsNullOrWhiteSpace(actionName)
                && Actions != null
                && Actions.TryGetValue(actionName, out action)
                && action != null;
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
        public SkillAnimation FlipAnimation { get; set; }
        public string AnimationPath { get; set; }
        public string BallUolPath { get; set; }
        public string FlipBallUolPath { get; set; }
        public SkillAnimation HitAnimation { get; set; }
        public List<SkillAnimation> VariantAnimations { get; set; } = new();
        public List<string> VariantAnimationPaths { get; set; } = new();
        public SortedDictionary<int, List<SkillAnimation>> CharacterLevelVariantAnimations { get; set; } = new();
        public SortedDictionary<int, List<string>> CharacterLevelVariantAnimationPaths { get; set; } = new();
        public SortedDictionary<int, string> CharacterLevelBallUolPaths { get; set; } = new();
        public SortedDictionary<int, string> CharacterLevelFlipBallUolPaths { get; set; } = new();
        public Dictionary<int, List<SkillAnimation>> LevelVariantAnimations { get; set; } = new();
        public Dictionary<int, List<string>> LevelVariantAnimationPaths { get; set; } = new();
        public Dictionary<int, string> LevelBallUolPaths { get; set; } = new();
        public Dictionary<int, string> LevelFlipBallUolPaths { get; set; } = new();

        public ProjectileBehavior Behavior { get; set; } = ProjectileBehavior.Straight;
        public float Speed { get; set; } = 400f;
        public float Gravity { get; set; } = 0f;
        public float LifeTime { get; set; } = 2000f;  // Max life in ms
        public bool Homing { get; set; } = false;
        public bool Piercing { get; set; } = false;
        public int MaxHits { get; set; } = 1;

        // Explosion
        public float ExplosionRadius { get; set; } = 0;
        public SkillAnimation ExplosionAnimation { get; set; }

        public SkillAnimation ResolveAnimationVariant(int level, int maxLevel = 0)
        {
            return ResolveAnimationVariant(VariantAnimations, level, maxLevel, Animation);
        }

        public SkillAnimation ResolveGetBallLikeAnimation(int skillLevel, int characterLevel, int maxLevel = 0)
        {
            return ResolveGetBallLikeAnimation(skillLevel, characterLevel, flip: false, maxLevel: maxLevel);
        }

        public SkillAnimation ResolveGetBallLikeAnimation(int skillLevel, int characterLevel, bool flip, int maxLevel = 0)
        {
            if (CharacterLevelVariantAnimations != null && CharacterLevelVariantAnimations.Count > 0)
            {
                List<SkillAnimation> characterLevelVariants = null;
                foreach ((int requiredLevel, List<SkillAnimation> variants) in CharacterLevelVariantAnimations)
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    characterLevelVariants = variants;
                }

                SkillAnimation characterLevelAnimation = ResolveAnimationVariant(
                    characterLevelVariants,
                    skillLevel,
                    maxLevel);
                if (characterLevelAnimation?.Frames.Count > 0)
                {
                    return characterLevelAnimation;
                }
            }

            if (skillLevel > 0
                && LevelVariantAnimations != null
                && LevelVariantAnimations.TryGetValue(skillLevel, out List<SkillAnimation> levelVariants))
            {
                SkillAnimation levelAnimation = ResolveAnimationVariant(
                    levelVariants,
                    skillLevel,
                    maxLevel);
                if (levelAnimation?.Frames.Count > 0)
                {
                    return levelAnimation;
                }
            }

            if (flip && FlipAnimation?.Frames.Count > 0)
            {
                return FlipAnimation;
            }

            return ResolveAnimationVariant(skillLevel, maxLevel);
        }

        public string ResolveGetBallLikeAnimationPath(int skillLevel, int characterLevel, int maxLevel = 0)
        {
            if (CharacterLevelVariantAnimations != null && CharacterLevelVariantAnimations.Count > 0)
            {
                List<SkillAnimation> characterLevelVariants = null;
                List<string> characterLevelVariantPaths = null;
                foreach ((int requiredLevel, List<SkillAnimation> variants) in CharacterLevelVariantAnimations)
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    characterLevelVariants = variants;
                    if (CharacterLevelVariantAnimationPaths != null)
                    {
                        CharacterLevelVariantAnimationPaths.TryGetValue(requiredLevel, out characterLevelVariantPaths);
                    }
                }

                string characterLevelAnimationPath = ResolveAnimationVariantPath(
                    characterLevelVariants,
                    characterLevelVariantPaths,
                    skillLevel,
                    maxLevel);
                if (!string.IsNullOrWhiteSpace(characterLevelAnimationPath))
                {
                    return characterLevelAnimationPath;
                }
            }

            if (skillLevel > 0
                && LevelVariantAnimations != null
                && LevelVariantAnimations.TryGetValue(skillLevel, out List<SkillAnimation> levelVariants))
            {
                List<string> levelVariantPaths = null;
                if (LevelVariantAnimationPaths != null)
                {
                    LevelVariantAnimationPaths.TryGetValue(skillLevel, out levelVariantPaths);
                }
                string levelAnimationPath = ResolveAnimationVariantPath(
                    levelVariants,
                    levelVariantPaths,
                    skillLevel,
                    maxLevel);
                if (!string.IsNullOrWhiteSpace(levelAnimationPath))
                {
                    return levelAnimationPath;
                }
            }

            return ResolveAnimationVariantPath(VariantAnimations, VariantAnimationPaths, skillLevel, maxLevel, AnimationPath);
        }

        public string ResolveGetBallLikeUolPath(int skillLevel, int characterLevel, bool flip, int maxLevel = 0)
        {
            if (CharacterLevelBallUolPaths != null || CharacterLevelFlipBallUolPaths != null)
            {
                string characterLevelBallUolPath = null;
                string characterLevelFlipBallUolPath = null;
                foreach (int requiredLevel in EnumerateSortedKeys(CharacterLevelBallUolPaths?.Keys, CharacterLevelFlipBallUolPaths?.Keys))
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    if (CharacterLevelBallUolPaths != null)
                    {
                        CharacterLevelBallUolPaths.TryGetValue(requiredLevel, out characterLevelBallUolPath);
                    }

                    if (CharacterLevelFlipBallUolPaths != null)
                    {
                        CharacterLevelFlipBallUolPaths.TryGetValue(requiredLevel, out characterLevelFlipBallUolPath);
                    }
                }

                string characterLevelResolvedUolPath = SelectPreferredUolPath(
                    flip,
                    characterLevelFlipBallUolPath,
                    characterLevelBallUolPath);
                if (!string.IsNullOrWhiteSpace(characterLevelResolvedUolPath))
                {
                    return characterLevelResolvedUolPath;
                }
            }

            if (skillLevel > 0)
            {
                string levelBallUolPath = null;
                string levelFlipBallUolPath = null;
                if (LevelBallUolPaths != null)
                {
                    LevelBallUolPaths.TryGetValue(skillLevel, out levelBallUolPath);
                }

                if (LevelFlipBallUolPaths != null)
                {
                    LevelFlipBallUolPaths.TryGetValue(skillLevel, out levelFlipBallUolPath);
                }

                string levelResolvedUolPath = SelectPreferredUolPath(
                    flip,
                    levelFlipBallUolPath,
                    levelBallUolPath);
                if (!string.IsNullOrWhiteSpace(levelResolvedUolPath))
                {
                    return levelResolvedUolPath;
                }
            }

            return SelectPreferredUolPath(flip, FlipBallUolPath, BallUolPath)
                   ?? BallUolPath
                   ?? AnimationPath;
        }

        private static SkillAnimation ResolveAnimationVariant(
            IReadOnlyList<SkillAnimation> variants,
            int level,
            int maxLevel = 0,
            SkillAnimation fallback = null)
        {
            if (variants == null || variants.Count == 0)
            {
                return fallback;
            }

            List<SkillAnimation> renderableVariants = variants
                .Where(static animation => animation?.Frames.Count > 0)
                .ToList();
            if (renderableVariants.Count == 0)
            {
                return fallback;
            }

            if (renderableVariants.Count == 1)
            {
                return renderableVariants[0];
            }

            int resolvedLevel = Math.Max(1, level);
            int resolvedMaxLevel = maxLevel > 0
                ? maxLevel
                : Math.Max(resolvedLevel, renderableVariants.Count);
            int variantIndex = Math.Clamp(
                ((resolvedLevel - 1) * renderableVariants.Count) / Math.Max(1, resolvedMaxLevel),
                0,
                renderableVariants.Count - 1);
            return renderableVariants[variantIndex];
        }

        private static string ResolveAnimationVariantPath(
            IReadOnlyList<SkillAnimation> variants,
            IReadOnlyList<string> paths,
            int level,
            int maxLevel = 0,
            string fallback = null)
        {
            int? resolvedIndex = ResolveRenderableVariantIndex(variants, level, maxLevel);
            if (!resolvedIndex.HasValue
                || paths == null
                || resolvedIndex.Value < 0
                || resolvedIndex.Value >= paths.Count)
            {
                return fallback;
            }

            string resolvedPath = paths[resolvedIndex.Value];
            return string.IsNullOrWhiteSpace(resolvedPath) ? fallback : resolvedPath;
        }

        private static string ResolveVariantUolPath(
            IReadOnlyList<SkillAnimation> variants,
            string uolPath)
        {
            return variants?.Any(static animation => animation?.Frames.Count > 0) == true
                   && !string.IsNullOrWhiteSpace(uolPath)
                ? uolPath
                : null;
        }

        private static string SelectPreferredUolPath(bool flip, string flipUolPath, string uolPath)
        {
            return flip && !string.IsNullOrWhiteSpace(flipUolPath)
                ? flipUolPath
                : uolPath;
        }

        private static IEnumerable<int> EnumerateSortedKeys(params IEnumerable<int>[] keySets)
        {
            SortedSet<int> mergedKeys = new();
            foreach (IEnumerable<int> keySet in keySets)
            {
                if (keySet == null)
                {
                    continue;
                }

                foreach (int key in keySet)
                {
                    mergedKeys.Add(key);
                }
            }

            return mergedKeys;
        }

        private static int? ResolveRenderableVariantIndex(
            IReadOnlyList<SkillAnimation> variants,
            int level,
            int maxLevel = 0)
        {
            if (variants == null || variants.Count == 0)
            {
                return null;
            }

            List<int> renderableIndexes = variants
                .Select((animation, index) => new { animation, index })
                .Where(static entry => entry.animation?.Frames.Count > 0)
                .Select(static entry => entry.index)
                .ToList();
            if (renderableIndexes.Count == 0)
            {
                return null;
            }

            if (renderableIndexes.Count == 1)
            {
                return renderableIndexes[0];
            }

            int resolvedLevel = Math.Max(1, level);
            int resolvedMaxLevel = maxLevel > 0
                ? maxLevel
                : Math.Max(resolvedLevel, renderableIndexes.Count);
            int renderableIndex = Math.Clamp(
                ((resolvedLevel - 1) * renderableIndexes.Count) / Math.Max(1, resolvedMaxLevel),
                0,
                renderableIndexes.Count - 1);
            return renderableIndexes[renderableIndex];
        }
    }

    /// <summary>
    /// Zone/tile effect data with client-shaped variant selection.
    /// </summary>
    public class ZoneEffectData
    {
        public SkillAnimation Animation { get; set; }
        public string AnimationPath { get; set; }
        public string TileUolPath { get; set; }
        public List<SkillAnimation> VariantAnimations { get; set; } = new();
        public List<string> VariantAnimationPaths { get; set; } = new();
        public SortedDictionary<int, List<SkillAnimation>> CharacterLevelVariantAnimations { get; set; } = new();
        public SortedDictionary<int, List<string>> CharacterLevelVariantAnimationPaths { get; set; } = new();
        public SortedDictionary<int, string> CharacterLevelTileUolPaths { get; set; } = new();
        public Dictionary<int, List<SkillAnimation>> LevelVariantAnimations { get; set; } = new();
        public Dictionary<int, List<string>> LevelVariantAnimationPaths { get; set; } = new();
        public Dictionary<int, string> LevelTileUolPaths { get; set; } = new();
        public SortedDictionary<int, int> CharacterLevelEffectDistances { get; set; } = new();
        public Dictionary<int, int> LevelEffectDistances { get; set; } = new();
        public int EffectDistance { get; set; }

        public SkillAnimation ResolveAnimationVariant(int skillLevel, int characterLevel, int maxLevel = 0)
        {
            if (CharacterLevelVariantAnimations != null && CharacterLevelVariantAnimations.Count > 0)
            {
                List<SkillAnimation> characterLevelVariants = null;
                foreach ((int requiredLevel, List<SkillAnimation> variants) in CharacterLevelVariantAnimations)
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    characterLevelVariants = variants;
                }

                SkillAnimation characterLevelAnimation = ResolveAnimationVariant(
                    characterLevelVariants,
                    skillLevel,
                    maxLevel);
                if (characterLevelAnimation?.Frames.Count > 0)
                {
                    return characterLevelAnimation;
                }
            }

            if (skillLevel > 0
                && LevelVariantAnimations != null
                && LevelVariantAnimations.TryGetValue(skillLevel, out List<SkillAnimation> levelVariants))
            {
                SkillAnimation levelAnimation = ResolveAnimationVariant(
                    levelVariants,
                    skillLevel,
                    maxLevel);
                if (levelAnimation?.Frames.Count > 0)
                {
                    return levelAnimation;
                }
            }

            return ResolveAnimationVariant(VariantAnimations, skillLevel, maxLevel, Animation);
        }

        public int ResolveEffectDistanceVariant(int skillLevel, int characterLevel)
        {
            if (CharacterLevelVariantAnimations != null && CharacterLevelVariantAnimations.Count > 0)
            {
                int? selectedRequiredLevel = null;
                foreach ((int requiredLevel, List<SkillAnimation> variants) in CharacterLevelVariantAnimations)
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    if (variants?.Any(static animation => animation?.Frames.Count > 0) == true)
                    {
                        selectedRequiredLevel = requiredLevel;
                    }
                }

                if (selectedRequiredLevel.HasValue)
                {
                    if (CharacterLevelEffectDistances != null
                        && CharacterLevelEffectDistances.TryGetValue(selectedRequiredLevel.Value, out int characterLevelEffectDistance)
                        && characterLevelEffectDistance > 0)
                    {
                        return characterLevelEffectDistance;
                    }

                    return EffectDistance;
                }
            }

            if (skillLevel > 0
                && LevelVariantAnimations != null
                && LevelVariantAnimations.TryGetValue(skillLevel, out List<SkillAnimation> levelVariants)
                && levelVariants?.Any(static animation => animation?.Frames.Count > 0) == true)
            {
                if (LevelEffectDistances != null
                    && LevelEffectDistances.TryGetValue(skillLevel, out int levelEffectDistance)
                    && levelEffectDistance > 0)
                {
                    return levelEffectDistance;
                }

                return EffectDistance;
            }

            return EffectDistance;
        }

        public string ResolveAnimationVariantPath(int skillLevel, int characterLevel, int maxLevel = 0)
        {
            if (CharacterLevelVariantAnimations != null && CharacterLevelVariantAnimations.Count > 0)
            {
                List<SkillAnimation> characterLevelVariants = null;
                List<string> characterLevelVariantPaths = null;
                foreach ((int requiredLevel, List<SkillAnimation> variants) in CharacterLevelVariantAnimations)
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    characterLevelVariants = variants;
                    if (CharacterLevelVariantAnimationPaths != null)
                    {
                        CharacterLevelVariantAnimationPaths.TryGetValue(requiredLevel, out characterLevelVariantPaths);
                    }
                }

                string characterLevelAnimationPath = ResolveAnimationVariantPath(
                    characterLevelVariants,
                    characterLevelVariantPaths,
                    skillLevel,
                    maxLevel);
                if (!string.IsNullOrWhiteSpace(characterLevelAnimationPath))
                {
                    return characterLevelAnimationPath;
                }
            }

            if (skillLevel > 0
                && LevelVariantAnimations != null
                && LevelVariantAnimations.TryGetValue(skillLevel, out List<SkillAnimation> levelVariants))
            {
                List<string> levelVariantPaths = null;
                if (LevelVariantAnimationPaths != null)
                {
                    LevelVariantAnimationPaths.TryGetValue(skillLevel, out levelVariantPaths);
                }
                string levelAnimationPath = ResolveAnimationVariantPath(
                    levelVariants,
                    levelVariantPaths,
                    skillLevel,
                    maxLevel);
                if (!string.IsNullOrWhiteSpace(levelAnimationPath))
                {
                    return levelAnimationPath;
                }
            }

            return ResolveAnimationVariantPath(VariantAnimations, VariantAnimationPaths, skillLevel, maxLevel, AnimationPath);
        }

        public string ResolveTileUolPath(int skillLevel, int characterLevel)
        {
            if (CharacterLevelTileUolPaths != null && CharacterLevelTileUolPaths.Count > 0)
            {
                string characterLevelTileUolPath = null;
                foreach (int requiredLevel in CharacterLevelTileUolPaths.Keys)
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    CharacterLevelTileUolPaths.TryGetValue(requiredLevel, out characterLevelTileUolPath);
                }

                if (!string.IsNullOrWhiteSpace(characterLevelTileUolPath))
                {
                    return characterLevelTileUolPath;
                }
            }

            if (skillLevel > 0)
            {
                string levelTileUolPath = null;
                if (LevelTileUolPaths != null)
                {
                    LevelTileUolPaths.TryGetValue(skillLevel, out levelTileUolPath);
                }

                if (!string.IsNullOrWhiteSpace(levelTileUolPath))
                {
                    return levelTileUolPath;
                }
            }

            return TileUolPath ?? AnimationPath;
        }

        private static SkillAnimation ResolveAnimationVariant(
            IReadOnlyList<SkillAnimation> variants,
            int level,
            int maxLevel = 0,
            SkillAnimation fallback = null)
        {
            if (variants == null || variants.Count == 0)
            {
                return fallback;
            }

            List<SkillAnimation> renderableVariants = variants
                .Where(static animation => animation?.Frames.Count > 0)
                .ToList();
            if (renderableVariants.Count == 0)
            {
                return fallback;
            }

            if (renderableVariants.Count == 1)
            {
                return renderableVariants[0];
            }

            int resolvedLevel = Math.Max(1, level);
            int resolvedMaxLevel = maxLevel > 0
                ? maxLevel
                : Math.Max(resolvedLevel, renderableVariants.Count);
            int variantIndex = Math.Clamp(
                ((resolvedLevel - 1) * renderableVariants.Count) / Math.Max(1, resolvedMaxLevel),
                0,
                renderableVariants.Count - 1);
            return renderableVariants[variantIndex];
        }

        private static string ResolveAnimationVariantPath(
            IReadOnlyList<SkillAnimation> variants,
            IReadOnlyList<string> paths,
            int level,
            int maxLevel = 0,
            string fallback = null)
        {
            int? resolvedIndex = ResolveRenderableVariantIndex(variants, level, maxLevel);
            if (!resolvedIndex.HasValue
                || paths == null
                || resolvedIndex.Value < 0
                || resolvedIndex.Value >= paths.Count)
            {
                return fallback;
            }

            string resolvedPath = paths[resolvedIndex.Value];
            return string.IsNullOrWhiteSpace(resolvedPath) ? fallback : resolvedPath;
        }

        private static string ResolveVariantUolPath(
            IReadOnlyList<SkillAnimation> variants,
            string uolPath)
        {
            return variants?.Any(static animation => animation?.Frames.Count > 0) == true
                   && !string.IsNullOrWhiteSpace(uolPath)
                ? uolPath
                : null;
        }

        private static int? ResolveRenderableVariantIndex(
            IReadOnlyList<SkillAnimation> variants,
            int level,
            int maxLevel = 0)
        {
            if (variants == null || variants.Count == 0)
            {
                return null;
            }

            List<int> renderableIndexes = variants
                .Select((animation, index) => new { animation, index })
                .Where(static entry => entry.animation?.Frames.Count > 0)
                .Select(static entry => entry.index)
                .ToList();
            if (renderableIndexes.Count == 0)
            {
                return null;
            }

            if (renderableIndexes.Count == 1)
            {
                return renderableIndexes[0];
            }

            int resolvedLevel = Math.Max(1, level);
            int resolvedMaxLevel = maxLevel > 0
                ? maxLevel
                : Math.Max(resolvedLevel, renderableIndexes.Count);
            int renderableIndex = Math.Clamp(
                ((resolvedLevel - 1) * renderableIndexes.Count) / Math.Max(1, resolvedMaxLevel),
                0,
                renderableIndexes.Count - 1);
            return renderableIndexes[renderableIndex];
        }
    }

    #endregion

    #region Skill Definition

    /// <summary>
    /// Complete skill definition
    /// </summary>
    public class SkillData
    {
        public const int MirrorImageSkillId = 4331002;

        public int SkillId { get; set; }
        internal bool PresentationDataLoaded { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DescriptionHints { get; set; }
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
        public bool IsMovement { get; set; }
        public bool IsPrepareSkill { get; set; }
        public bool IsKeydownSkill { get; set; }
        public bool IsMesoExplosion { get; set; }
        public bool IsRapidAttack { get; set; }
        public bool IsMovingAttack { get; set; }
        public bool Invisible { get; set; }          // Hidden skill
        public bool MasterOnly { get; set; }         // Only usable at max level
        public bool FixedState { get; set; }
        public bool CanNotMoveInState { get; set; }
        public bool OnlyNormalAttackInState { get; set; }
        public bool SpecialNormalAttackInState { get; set; }
        public bool HasMorphMetadata { get; set; }
        public int MorphId { get; set; }
        public bool UsesTamingMobMount { get; set; }
        public bool ReflectsIncomingDamage { get; set; }
        public bool RedirectsDamageToMp { get; set; }
        public bool HasMagicStealMetadata { get; set; }
        public bool HasInvincibleMetadata { get; set; }
        public bool HasDispelMetadata { get; set; }
        public bool HasBlessingArmorMetadata { get; set; }
        public bool UsesEnergyChargeRuntime { get; set; }
        public bool HasChargingSkillMetadata { get; set; }
        public string FullChargeEffectName { get; set; }
        public string EnergyChargeThresholdFormula { get; set; }

        // Level data
        public Dictionary<int, SkillLevelData> Levels { get; set; } = new();
        public Dictionary<int, SkillLevelData> PvpLevels { get; set; } = new();

        // Animations
        public IDXObject Icon { get; set; }
        public Texture2D IconTexture { get; set; }
        public IDXObject IconDisabled { get; set; }
        public Texture2D IconDisabledTexture { get; set; }
        public IDXObject IconMouseOver { get; set; }
        public SkillAnimation Effect { get; set; }           // Effect on caster
        public SkillAnimation EffectSecondary { get; set; }  // Secondary caster effect branch (e.g. effect0)
        public SkillAnimation PrepareEffect { get; set; }    // Startup effect for prepare/keydown skills
        public SkillAnimation PrepareSecondaryEffect { get; set; } // Secondary startup branch (e.g. prepare0) drawn alongside PrepareEffect
        public SkillAnimation KeydownEffect { get; set; }    // Looping effect while keydown skill is held
        public SkillAnimation KeydownSecondaryEffect { get; set; } // Secondary hold branch (e.g. keydown0) drawn alongside KeydownEffect
        public SkillAnimation RepeatEffect { get; set; }     // Dedicated repeated hold-loop effect for charge/keydown skills
        public SkillAnimation RepeatSecondaryEffect { get; set; } // Secondary repeated hold-loop branch (e.g. repeat0)
        public SkillAnimation KeydownEndEffect { get; set; } // Exit effect when keydown skill ends
        public SkillAnimation KeydownEndSecondaryEffect { get; set; } // Secondary exit branch (e.g. keydownend0)
        public SkillAnimation StopEffect { get; set; }       // Dedicated transient cleanup branch (e.g. stopEffect)
        public SkillAnimation StopSecondaryEffect { get; set; } // Secondary transient cleanup branch (e.g. stopEffect0)
        public SkillAnimation HitEffect { get; set; }        // Effect on target
        public SkillAnimation MobAnimation { get; set; }     // Target-bound branch used by client-owned secondary managers (e.g. Monster Magnet hook/chain)
        public string MobAnimationPath { get; set; }
        public List<SkillAnimation> TargetHitEffects { get; set; } = new(); // Indexed client hit UOL branches (hit/0, hit/1, ...)
        public List<SkillAnimation> MultipleLayerTargetHitEffects { get; set; } = new(); // Indexed client multiple-layer hit roots (hit0, hit1, ...)
        public SkillAnimation AffectedEffect { get; set; }   // Effect while buff active
        public SkillAnimation AffectedSecondaryEffect { get; set; } // Secondary buff/affected branch (e.g. affected0)
        public SkillAnimation SpecialAffectedEffect { get; set; } // One-time buff/heal start branch (e.g. specialAffected)
        public SkillAnimation SummonSpawnAnimation { get; set; } // Initial summon spawn sequence
        public SkillAnimation SummonAnimation { get; set; }  // Summon body/effect
        public SkillAnimation SummonAttackPrepareAnimation { get; set; } // Optional summon windup before the main attack branch
        public SkillAnimation SummonAttackAnimation { get; set; } // Summon attack sequence
        public SkillAnimation SummonHitAnimation { get; set; } // Summon hit reaction sequence
        public SkillAnimation SummonRemovalAnimation { get; set; } // Optional self-destruct / removal branch
        public string ClientSummonedUolPath { get; set; }
        public List<string> ClientSummonedUolCandidatePaths { get; set; } = new();
        public string ClientTileUolPath { get; set; }
        public string ClientBallUolPath { get; set; }
        public string ClientFlipBallUolPath { get; set; }
        public string ResolvedSummonAssetPath { get; set; }
        public List<SkillAnimation> SummonProjectileAnimations { get; set; } = new();
        public List<string> SummonProjectileAnimationPaths { get; set; } = new();
        public List<SkillAnimation> SummonTargetHitAnimations { get; set; } = new();
        public List<SummonImpactPresentation> SummonTargetHitPresentations { get; set; } = new();
        public Dictionary<string, SummonRangeMetadata> SummonNamedRangeMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Point> SummonNamedAttackCenterOffsets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> SummonNamedAttackRadii { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SkillAnimation> SummonNamedAnimations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SkillAnimation> SummonActionAnimations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<SkillAnimation>> SummonProjectileAnimationsByBranch { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> SummonProjectileAnimationPathsByBranch { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<SummonImpactPresentation>> SummonTargetHitPresentationsByBranch { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> SummonAttackAfterMsByBranch { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> SummonMobCountOverridesByBranch { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> SummonAttackCountOverridesByBranch { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> SummonAttackProjectileSpeedByBranch { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string SummonSpawnBranchName { get; set; }
        public string SummonIdleBranchName { get; set; }
        public string SummonPrepareBranchName { get; set; }
        public string SummonRemovalBranchName { get; set; }
        public string SummonAttackBranchName { get; set; }
        public string SummonHitBranchName { get; set; }
        public SkillAnimation AvatarOverlayEffect { get; set; } // Avatar-bound looping overlay
        public SkillAnimation AvatarOverlaySecondaryEffect { get; set; } // Optional second overlay on the same avatar-owned plane
        public SkillAnimation AvatarUnderFaceEffect { get; set; } // Avatar-bound layer below face/hair
        public SkillAnimation AvatarUnderFaceSecondaryEffect { get; set; } // Optional second under-face layer on the same avatar-owned plane
        public SkillAnimation AvatarLadderEffect { get; set; } // Ladder/rope override for avatar overlay
        public SkillAnimation AvatarOverlayFinishEffect { get; set; } // One-shot cleanup overlay
        public SkillAnimation AvatarUnderFaceFinishEffect { get; set; } // One-shot cleanup below face
        public SkillAnimation AvatarLadderFinishEffect { get; set; } // Ladder/rope cleanup override
        public Dictionary<string, SkillAnimation> ShadowPartnerActionAnimations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ShadowPartnerSupportedRawActionNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int ShadowPartnerHorizontalOffsetPx { get; set; }
        public bool HideAvatarEffectOnLadderOrRope { get; set; }
        public int SummonMoveAbility { get; set; }
        public SummonMovementStyle SummonMovementStyle { get; set; } = SummonMovementStyle.Stationary;
        public float SummonSpawnDistanceX { get; set; } = 50f;
        public int SummonAttackIntervalMs { get; set; }
        public int SummonAttackCountOverride { get; set; }
        public int SummonMobCountOverride { get; set; }
        public int SummonAttackHitDelayMs { get; set; }
        public int SummonAttackProjectileSpeed { get; set; }
        public Point? SummonAttackCenterOffset { get; set; }
        public int SummonAttackRadius { get; set; }
        public int SummonAttackRangeLeft { get; set; }
        public int SummonAttackRangeRight { get; set; }
        public int SummonAttackRangeTop { get; set; }
        public int SummonAttackRangeBottom { get; set; }
        public string MinionAbility { get; set; }
        public string MinionAttack { get; set; }
        public string SummonCondition { get; set; }
        public bool SelfDestructMinion { get; set; }
        public string SummonSelfDestructionFormula { get; set; }
        public string SummonSubTimeFormula { get; set; }
        public string SummonTimeFormula { get; set; }
        public string TriggerCondition { get; set; }
        public bool IsSwallowSkill { get; set; }
        public int[] DummySkillParents { get; set; } = Array.Empty<int>();
        public ProjectileData Projectile { get; set; }       // Ball/projectile
        public string CastSoundKey { get; set; }             // Registered simulator sound key for cast SFX
        public string RepeatSoundKey { get; set; }           // Registered simulator sound key for repeated hits/shots
        public string ZoneType { get; set; }
        public bool IsMassSpell { get; set; }
        public string DebuffMessageToken { get; set; }
        public SkillAnimation ZoneAnimation { get; set; }
        public ZoneEffectData ZoneEffect { get; set; }
        public int ClientInfoType { get; set; }
        public string ElementAttributeToken { get; set; }
        public bool AvailableInJumpingState { get; set; }
        public bool RequireHighestJump { get; set; }
        public int[] RequiredSkillIds { get; set; } = Array.Empty<int>();
        public bool IsPassiveSkillData { get; set; }
        public int AffectedSkillId { get; set; }
        public int[] AffectedSkillIds { get; set; } = Array.Empty<int>();
        public int[] PassiveLinkedSkillIds { get; set; } = Array.Empty<int>();
        public string AffectedSkillEffect { get; set; }
        public string DotType { get; set; }
        public bool IsMagicDamageSkill { get; set; }
        public Dictionary<string, MeleeAfterImageCatalog> AfterImageCatalogsByWeaponType { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public SortedDictionary<int, Dictionary<string, MeleeAfterImageCatalog>> CharacterLevelAfterImageCatalogsByWeaponType { get; set; } = new();

        // Action
        public string ActionName { get; set; }       // Animation action to play
        public IReadOnlyList<string> ActionNames { get; set; } = Array.Empty<string>();
        public string PrepareActionName { get; set; }
        public string KeydownActionName { get; set; }
        public string KeydownEndActionName { get; set; }
        public int PrepareDurationMs { get; set; }
        public int KeydownDurationMs { get; set; }
        public int RepeatDurationMs { get; set; }
        public int KeydownEndDurationMs { get; set; }
        public int KeydownRepeatIntervalMs { get; set; }
        public int ClientDelayMs { get; set; }
        public bool CasterMove { get; set; }
        public bool AreaAttack { get; set; }
        public bool PullTarget { get; set; }
        public bool RectBasedOnTarget { get; set; }
        public bool MultiTargeting { get; set; }
        public bool ChainAttack { get; set; }
        public bool ChainAttackPenalty { get; set; }
        public string LandingEffectName { get; set; }
        public Dictionary<int, HashSet<int>> FinalAttackTriggers { get; set; } = new();

        public bool HasPersistentAvatarEffect =>
            AvatarOverlayEffect != null
            || AvatarOverlaySecondaryEffect != null
            || AvatarUnderFaceEffect != null
            || AvatarUnderFaceSecondaryEffect != null
            || AvatarLadderEffect != null
            || AvatarOverlayFinishEffect != null
            || AvatarUnderFaceFinishEffect != null
            || AvatarLadderFinishEffect != null;

        public bool IsSwallowFamilySkill { get; set; }

        public bool UsesAffectedSkillPassiveData =>
            GetAffectedSkillIds().Length > 0
            && !string.IsNullOrWhiteSpace(AffectedSkillEffect);

        public bool HasStandaloneActiveCastSurface =>
            !string.IsNullOrWhiteSpace(ActionName)
            || (ActionNames?.Count ?? 0) > 0
            || !string.IsNullOrWhiteSpace(PrepareActionName)
            || !string.IsNullOrWhiteSpace(KeydownActionName)
            || !string.IsNullOrWhiteSpace(KeydownEndActionName)
            || HasAnimationFrames(Effect)
            || HasAnimationFrames(EffectSecondary)
            || HasAnimationFrames(PrepareEffect)
            || HasAnimationFrames(PrepareSecondaryEffect)
            || HasAnimationFrames(KeydownEffect)
            || HasAnimationFrames(KeydownSecondaryEffect)
            || HasAnimationFrames(RepeatEffect)
            || HasAnimationFrames(RepeatSecondaryEffect)
            || HasAnimationFrames(KeydownEndEffect)
            || HasAnimationFrames(KeydownEndSecondaryEffect)
            || HasAnimationFrames(StopEffect)
            || HasAnimationFrames(StopSecondaryEffect)
            || HasAnimationFrames(HitEffect)
            || HasAnimationFrames(AffectedEffect)
            || HasAnimationFrames(AffectedSecondaryEffect)
            || HasAnimationFrames(SpecialAffectedEffect)
            || HasPersistentAvatarEffect
            || HasAnimationFrames(SummonSpawnAnimation)
            || HasAnimationFrames(SummonAnimation)
            || HasAnimationFrames(SummonAttackPrepareAnimation)
            || HasAnimationFrames(SummonAttackAnimation)
            || HasAnimationFrames(SummonHitAnimation)
            || HasAnimationFrames(SummonRemovalAnimation)
            || Projectile != null
            || HasAnimationFrames(ZoneAnimation)
            || SummonActionAnimations.Count > 0
            || SummonNamedAnimations.Count > 0;

        public bool UsesHelperOnlyAffectedSkillPassiveData =>
            UsesAffectedSkillPassiveData
            && !HasStandaloneActiveCastSurface;

        public bool UsesAffectedSkillBodyAttack =>
            UsesHelperOnlyAffectedSkillPassiveData
            && AffectedSkillEffect.IndexOf("bodyAttack", StringComparison.OrdinalIgnoreCase) >= 0;

        public bool SuppressesStandaloneActiveCast => UsesHelperOnlyAffectedSkillPassiveData;

        public bool HasShadowPartnerActionAnimations => ShadowPartnerActionAnimations != null && ShadowPartnerActionAnimations.Count > 0;

        public bool UsesMirrorHelperActor => SkillId == MirrorImageSkillId;

        public bool HideAvatarEffectOnRotateAction { get; set; }

        public bool LinksDummySkill(int skillId)
        {
            return skillId > 0
                   && DummySkillParents != null
                   && Array.IndexOf(DummySkillParents, skillId) >= 0;
        }

        public bool LinksAffectedSkill(int skillId)
        {
            return skillId > 0
                   && Array.IndexOf(GetAffectedSkillIds(), skillId) >= 0;
        }

        private static bool HasAnimationFrames(SkillAnimation animation)
        {
            return animation?.Frames?.Count > 0;
        }

        public int[] GetAffectedSkillIds()
        {
            if (AffectedSkillIds?.Length > 0)
            {
                return AffectedSkillIds;
            }

            return AffectedSkillId > 0
                ? new[] { AffectedSkillId }
                : Array.Empty<int>();
        }

        public MeleeAfterImageCatalog GetAfterImageCatalogForCharacterLevel(string weaponTypeKey, int characterLevel)
        {
            MeleeAfterImageCatalog resolvedCatalog = null;
            if (!string.IsNullOrWhiteSpace(weaponTypeKey) && CharacterLevelAfterImageCatalogsByWeaponType != null)
            {
                foreach ((int requiredLevel, Dictionary<string, MeleeAfterImageCatalog> catalogMap) in CharacterLevelAfterImageCatalogsByWeaponType)
                {
                    if (requiredLevel > characterLevel)
                    {
                        break;
                    }

                    if (catalogMap != null
                        && catalogMap.TryGetValue(weaponTypeKey, out MeleeAfterImageCatalog catalog)
                        && catalog != null)
                    {
                        resolvedCatalog = catalog;
                    }
                }
            }

            if (resolvedCatalog != null)
            {
                return resolvedCatalog;
            }

            return !string.IsNullOrWhiteSpace(weaponTypeKey)
                && AfterImageCatalogsByWeaponType != null
                && AfterImageCatalogsByWeaponType.TryGetValue(weaponTypeKey, out MeleeAfterImageCatalog baseCatalog)
                ? baseCatalog
                : null;
        }

        /// <summary>
        /// Get data for a specific level
        /// </summary>
        public SkillLevelData GetLevel(int level)
        {
            level = Math.Clamp(level, 1, MaxLevel);
            return Levels.TryGetValue(level, out var data) ? data : null;
        }

        public SkillLevelData GetPvpLevel(int level)
        {
            level = Math.Clamp(level, 1, MaxLevel);
            return PvpLevels.TryGetValue(level, out var data) ? data : null;
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

            if (TryGetExplicitAttackRange(level, facingRight, out Rectangle explicitRange))
            {
                return explicitRange;
            }

            int rangeX = facingRight ? levelData.RangeR : levelData.RangeL;
            if (rangeX == 0) rangeX = levelData.Range;
            int height = levelData.RangeY > 0 ? levelData.RangeY : 60;

            return new Rectangle(
                facingRight ? 0 : -rangeX,
                -height / 2,
                Math.Max(1, rangeX),
                Math.Max(1, height));
        }

        public bool TryGetExplicitAttackRange(int level, bool facingRight, out Rectangle range)
        {
            range = Rectangle.Empty;

            var levelData = GetLevel(level);
            if (levelData == null)
            {
                return false;
            }

            bool hasExplicitRangeBox = levelData.RangeL > 0
                || levelData.RangeR > 0
                || levelData.RangeTop != 0
                || levelData.RangeBottom != 0;
            if (!hasExplicitRangeBox)
            {
                return false;
            }

            int left = -levelData.RangeL;
            int right = levelData.RangeR;
            if (!facingRight)
            {
                (left, right) = (-right, -left);
            }

            int top = levelData.RangeTop;
            int bottom = levelData.RangeBottom;
            if (bottom <= top)
            {
                int fallbackHeight = levelData.RangeY > 0 ? levelData.RangeY : 60;
                top = -fallbackHeight / 2;
                bottom = top + fallbackHeight;
            }

            range = new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
            return true;
        }

        public Rectangle GetSummonAttackRange(bool facingRight)
        {
            if (TryGetSummonAttackRange(facingRight, branchName: null, out Rectangle range))
            {
                return range;
            }

            return Rectangle.Empty;
        }

        public bool TryGetSummonAttackRange(bool facingRight, string branchName, out Rectangle range)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonNamedRangeMetadata != null
                && SummonNamedRangeMetadata.TryGetValue(branchName, out SummonRangeMetadata metadata)
                && metadata.HasExplicitRectangle)
            {
                range = metadata.ToRectangle(facingRight);
                return true;
            }

            bool hasExplicitRect = SummonAttackRangeLeft > 0
                || SummonAttackRangeRight > 0
                || SummonAttackRangeTop != 0
                || SummonAttackRangeBottom != 0;
            if (hasExplicitRect)
            {
                range = new SummonRangeMetadata(
                    SummonAttackRangeLeft,
                    SummonAttackRangeRight,
                    SummonAttackRangeTop,
                    SummonAttackRangeBottom).ToRectangle(facingRight);
                return true;
            }

            range = Rectangle.Empty;
            return false;
        }

        public Point GetSummonAttackCircleCenterOffset(bool facingRight, string branchName = null)
        {
            Point? center = null;
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonNamedAttackCenterOffsets != null
                && SummonNamedAttackCenterOffsets.TryGetValue(branchName, out Point branchCenter))
            {
                center = branchCenter;
            }
            else if (SummonAttackCenterOffset.HasValue)
            {
                center = SummonAttackCenterOffset.Value;
            }

            if (!center.HasValue)
            {
                return Point.Zero;
            }

            Point resolvedCenter = center.Value;
            return facingRight
                ? resolvedCenter
                : new Point(-resolvedCenter.X, resolvedCenter.Y);
        }

        public int ResolveSummonAttackRadius(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonNamedAttackRadii != null
                && SummonNamedAttackRadii.TryGetValue(branchName, out int branchRadius)
                && branchRadius > 0)
            {
                return branchRadius;
            }

            return SummonAttackRadius;
        }

        public int ResolveSummonAttackAfterMs(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonAttackAfterMsByBranch != null
                && SummonAttackAfterMsByBranch.TryGetValue(branchName, out int branchDelay)
                && branchDelay > 0)
            {
                return branchDelay;
            }

            return SummonAttackIntervalMs;
        }

        public int ResolveExplicitSummonAttackAfterMs(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonAttackAfterMsByBranch != null
                && SummonAttackAfterMsByBranch.TryGetValue(branchName, out int branchDelay)
                && branchDelay > 0)
            {
                return branchDelay;
            }

            if (string.IsNullOrWhiteSpace(branchName)
                && !string.IsNullOrWhiteSpace(SummonAttackBranchName)
                && SummonAttackAfterMsByBranch != null
                && SummonAttackAfterMsByBranch.TryGetValue(SummonAttackBranchName, out int defaultBranchDelay)
                && defaultBranchDelay > 0)
            {
                return defaultBranchDelay;
            }

            return 0;
        }

        public int ResolveSummonAttackProjectileSpeed(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonAttackProjectileSpeedByBranch != null
                && SummonAttackProjectileSpeedByBranch.TryGetValue(branchName, out int branchSpeed)
                && branchSpeed > 0)
            {
                return branchSpeed;
            }

            return SummonAttackProjectileSpeed;
        }

        public int ResolveSummonAttackCountOverride(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonAttackCountOverridesByBranch != null
                && SummonAttackCountOverridesByBranch.TryGetValue(branchName, out int branchAttackCount)
                && branchAttackCount > 0)
            {
                return branchAttackCount;
            }

            return SummonAttackCountOverride;
        }

        public IReadOnlyList<SkillAnimation> GetSummonProjectileAnimations(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonProjectileAnimationsByBranch != null
                && SummonProjectileAnimationsByBranch.TryGetValue(branchName, out List<SkillAnimation> branchAnimations)
                && branchAnimations?.Count > 0)
            {
                return branchAnimations;
            }

            return SummonProjectileAnimations;
        }

        public IReadOnlyList<string> GetSummonProjectileAnimationPaths(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonProjectileAnimationPathsByBranch != null
                && SummonProjectileAnimationPathsByBranch.TryGetValue(branchName, out List<string> branchPaths)
                && branchPaths?.Count > 0)
            {
                return branchPaths;
            }

            return SummonProjectileAnimationPaths;
        }

        public string ResolveSummonProjectileAnimationPath(string branchName = null, int variantIndex = 0)
        {
            IReadOnlyList<string> paths = GetSummonProjectileAnimationPaths(branchName);
            if (paths == null || paths.Count == 0)
            {
                return null;
            }

            return paths[Math.Abs(variantIndex) % paths.Count];
        }

        public int? ResolveSummonProjectilePositionCode(string branchName = null, int variantIndex = 0)
        {
            IReadOnlyList<SkillAnimation> animations = GetSummonProjectileAnimations(branchName);
            if (animations == null || animations.Count == 0)
            {
                return null;
            }

            SkillAnimation animation = animations[Math.Abs(variantIndex) % animations.Count];
            return animation?.PositionCode;
        }

        public int ResolveSummonAttackIntervalMs(int level)
        {
            if (SummonAttackIntervalMs > 0)
            {
                return SummonAttackIntervalMs;
            }

            if (TryEvaluateSkillFormula(SummonSubTimeFormula, level, out int subTimeSeconds) && subTimeSeconds > 0)
            {
                return subTimeSeconds * 1000;
            }

            return 1000;
        }

        public readonly record struct SummonRangeMetadata(
            int Left,
            int Right,
            int Top,
            int Bottom)
        {
            public bool HasExplicitRectangle =>
                Left > 0
                || Right > 0
                || Top != 0
                || Bottom != 0;

            public Rectangle ToRectangle(bool facingRight)
            {
                int left = -Left;
                int right = Right;
                if (!facingRight)
                {
                    (left, right) = (-right, -left);
                }

                int bottom = Bottom;
                if (bottom <= Top)
                {
                    bottom = Top + 60;
                }

                return new Rectangle(
                    left,
                    Top,
                    Math.Max(1, right - left),
                    Math.Max(1, bottom - Top));
            }
        }

        public int ResolveSummonDurationSeconds(int level)
        {
            return TryEvaluateSkillFormula(SummonTimeFormula, level, out int durationSeconds)
                ? Math.Max(0, durationSeconds)
                : 0;
        }

        public int ResolveSummonSelfDestructionDamagePercent(int level)
        {
            return TryEvaluateSkillFormula(SummonSelfDestructionFormula, level, out int damagePercent)
                ? Math.Max(0, damagePercent)
                : 0;
        }

        public int ResolveEnergyChargeThreshold(int level)
        {
            int resolvedLevel = level > 0 ? level : 1;
            if (TryEvaluateSkillFormula(EnergyChargeThresholdFormula, resolvedLevel, out int threshold)
                && threshold > 0)
            {
                return threshold;
            }

            SkillLevelData levelData = GetLevel(resolvedLevel);
            if (levelData?.X > 0)
            {
                return levelData.X;
            }

            return 100;
        }

        private static bool TryEvaluateSkillFormula(string expression, int xValue, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            try
            {
                var parser = new SkillFormulaParser(expression, xValue);
                double result = parser.Parse();
                value = (int)Math.Round(result, MidpointRounding.AwayFromZero);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private sealed class SkillFormulaParser
        {
            private readonly string _expression;
            private readonly int _xValue;
            private int _index;

            public SkillFormulaParser(string expression, int xValue)
            {
                _expression = expression ?? string.Empty;
                _xValue = xValue;
            }

            public double Parse()
            {
                double value = ParseExpression();
                SkipWhitespace();
                if (_index < _expression.Length)
                    throw new FormatException($"Unexpected token '{_expression[_index]}' in '{_expression}'.");

                return value;
            }

            private double ParseExpression()
            {
                double value = ParseTerm();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('+'))
                    {
                        value += ParseTerm();
                    }
                    else if (Match('-'))
                    {
                        value -= ParseTerm();
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private double ParseTerm()
            {
                double value = ParseFactor();
                while (true)
                {
                    SkipWhitespace();
                    if (Match('*'))
                    {
                        value *= ParseFactor();
                    }
                    else if (Match('/'))
                    {
                        double divisor = ParseFactor();
                        value = Math.Abs(divisor) < double.Epsilon ? 0 : value / divisor;
                    }
                    else
                    {
                        return value;
                    }
                }
            }

            private double ParseFactor()
            {
                SkipWhitespace();

                if (Match('+'))
                    return ParseFactor();

                if (Match('-'))
                    return -ParseFactor();

                if (Match('('))
                {
                    double value = ParseExpression();
                    Expect(')');
                    return value;
                }

                if (TryParseIdentifier(out string identifier))
                {
                    if (string.Equals(identifier, "x", StringComparison.OrdinalIgnoreCase))
                        return _xValue;

                    if (identifier.Equals("u", StringComparison.OrdinalIgnoreCase) ||
                        identifier.Equals("d", StringComparison.OrdinalIgnoreCase))
                    {
                        Expect('(');
                        double inner = ParseExpression();
                        Expect(')');
                        return identifier.Equals("u", StringComparison.OrdinalIgnoreCase)
                            ? Math.Ceiling(inner)
                            : Math.Floor(inner);
                    }

                    throw new FormatException($"Unsupported identifier '{identifier}' in '{_expression}'.");
                }

                return ParseNumber();
            }

            private double ParseNumber()
            {
                SkipWhitespace();
                int start = _index;
                while (_index < _expression.Length
                       && (char.IsDigit(_expression[_index]) || _expression[_index] == '.'))
                {
                    _index++;
                }

                if (start == _index)
                    throw new FormatException($"Expected number at index {_index} in '{_expression}'.");

                string token = _expression.Substring(start, _index - start);
                return double.Parse(token, System.Globalization.CultureInfo.InvariantCulture);
            }

            private bool TryParseIdentifier(out string identifier)
            {
                SkipWhitespace();
                int start = _index;
                while (_index < _expression.Length && char.IsLetter(_expression[_index]))
                {
                    _index++;
                }

                if (start == _index)
                {
                    identifier = string.Empty;
                    return false;
                }

                identifier = _expression.Substring(start, _index - start);
                return true;
            }

            private bool Match(char expected)
            {
                SkipWhitespace();
                if (_index >= _expression.Length || _expression[_index] != expected)
                    return false;

                _index++;
                return true;
            }

            private void Expect(char expected)
            {
                if (!Match(expected))
                    throw new FormatException($"Expected '{expected}' at index {_index} in '{_expression}'.");
            }

            private void SkipWhitespace()
            {
                while (_index < _expression.Length && char.IsWhiteSpace(_expression[_index]))
                {
                    _index++;
                }
            }
        }

        public SummonImpactPresentation GetSummonTargetHitPresentation(int targetOrder, string branchName = null)
        {
            IReadOnlyList<SummonImpactPresentation> presentations = SummonTargetHitPresentations;
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonTargetHitPresentationsByBranch != null
                && SummonTargetHitPresentationsByBranch.TryGetValue(branchName, out List<SummonImpactPresentation> branchPresentations)
                && branchPresentations?.Count > 0)
            {
                presentations = branchPresentations;
            }

            if (presentations == null || presentations.Count == 0)
            {
                return null;
            }

            return presentations[Math.Abs(targetOrder) % presentations.Count];
        }

        public SkillAnimation GetSummonTargetHitAnimation(int targetOrder, string branchName = null)
        {
            return GetSummonTargetHitPresentation(targetOrder, branchName)?.Animation;
        }

        public int ResolveSummonMobCountOverride(string branchName = null)
        {
            if (!string.IsNullOrWhiteSpace(branchName)
                && SummonMobCountOverridesByBranch != null
                && SummonMobCountOverridesByBranch.TryGetValue(branchName, out int branchMobCount)
                && branchMobCount > 0)
            {
                return branchMobCount;
            }

            return SummonMobCountOverride;
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

    #region Active Skill Zone

    public class ActiveSkillZone
    {
        public int SkillId { get; set; }
        public int Level { get; set; }
        public int StartTime { get; set; }
        public int Duration { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public SkillData SkillData { get; set; }
        public SkillLevelData LevelData { get; set; }
        public SkillAnimation Animation { get; set; }
        public Rectangle WorldBounds { get; set; }

        public bool IsExpired(int currentTime)
        {
            return Duration > 0 && currentTime - StartTime >= Duration;
        }

        public int AnimationTime(int currentTime)
        {
            return Math.Max(0, currentTime - StartTime);
        }

        public bool Contains(float worldX, float worldY)
        {
            return WorldBounds.Contains((int)worldX, (int)worldY);
        }
    }

    #endregion

    #region Active Projectile

    public enum BulletAnimationOwnerKind
    {
        Normal,
        Magic
    }

    public enum QueuedProjectileVisualLane
    {
        None,
        Use,
        Cash
    }

    public sealed class BulletAnimationPresentation
    {
        public BulletAnimationOwnerKind Kind { get; init; }
        public QueuedProjectileVisualLane VisualLane { get; init; }
        public int StartTime { get; init; }
        public int EndTime { get; init; }
        public Vector2 SourcePoint { get; init; }
        public Vector2 DestinationPoint { get; init; }
        public Vector2 TargetPoint { get; init; }
        public int Z { get; init; }
        public string EffectUol { get; init; }
        public int WeaponCode { get; init; }
        public int WeaponItemId { get; init; }
        public int BulletItemId { get; init; }
        public int BulletUseItemId { get; init; }
        public int BulletCashItemId { get; init; }
        public int BulletUseSlotIndex { get; init; } = -1;
        public int BulletCashSlotIndex { get; init; } = -1;
        public int QueuedBulletUseSlotPosition { get; init; }
        public int QueuedBulletCashSlotPosition { get; init; }
        public bool HasAfterimage { get; init; }
        public int AfterimageIntervalMs { get; init; }
        public int AfterimageStartAlpha { get; init; }
        public SkillAnimation Animation { get; init; }
        public SkillAnimation EffectAnimation { get; init; }
    }

    public sealed class RegisteredBulletPresentationContext
    {
        public QueuedProjectileVisualLane VisualLane { get; init; }
        public int BulletItemId { get; init; }
        public int BulletUseItemId { get; init; }
        public int BulletCashItemId { get; init; }
        public int WeaponCode { get; init; }
        public int WeaponItemId { get; init; }
    }

    public sealed class ActiveBulletAnimationOwner
    {
        public int Id { get; set; }
        public int ProjectileId { get; set; }
        public bool IsDetachedFromProjectile { get; set; }
        public BulletAnimationPresentation Presentation { get; init; }
        public Vector2 CurrentPosition { get; set; }
        public Vector2 PreviousPosition { get; set; }
        public bool FacingRight { get; set; }
        public int? StopTime { get; set; }
        public Vector2? StopPosition { get; set; }
        public int LastAfterimageUpdateTime { get; set; } = int.MinValue;
        public int NextAfterimageRepeatLayerId { get; set; } = 1;
        public List<ProjectileAfterimageLayer> AfterimageLayers { get; } = new();

        public bool CanDrawMainAnimation(int currentTime)
        {
            return Presentation != null
                   && currentTime >= Presentation.StartTime
                   && currentTime < Presentation.EndTime;
        }
    }

    public sealed class ProjectileAfterimageLayer
    {
        public int RepeatLayerObjectId { get; init; }
        public int ParentRepeatLayerObjectId { get; init; }
        public SkillFrame Frame { get; init; }
        public Vector2 Position { get; init; }
        public Rectangle WorldBounds { get; init; }
        public Point LayerOrigin { get; init; }
        public Point LayerLeftTop { get; init; }
        public Point LayerRightBottom { get; init; }
        public Vector2 OriginShiftStart { get; init; }
        public Vector2 OriginShiftEnd { get; init; }
        public bool ClockwiseBounds { get; init; }
        public bool Flip { get; init; }
        public int StartTime { get; init; }
        public int Duration { get; init; }
        public int AlphaStart { get; init; }
        public int AlphaEnd { get; init; }
        public int AlphaVectorStart { get; init; }
        public int AlphaVectorEnd { get; init; }
        public bool HasAlphaVectorStart { get; init; }
        public bool HasAlphaVectorEnd { get; init; }
        public float FrameAlphaMultiplier { get; init; } = 1f;

        public bool IsExpired(int currentTime)
        {
            return currentTime - StartTime > Duration;
        }

        public float ResolveAlpha(int currentTime)
        {
            float progress = ResolveProgress(currentTime);
            int alphaStart = ResolveAlphaVectorStart();
            int alphaEnd = ResolveAlphaVectorEnd();

            return MathHelper.Lerp(
                Math.Clamp(alphaStart, 0, 255),
                Math.Clamp(alphaEnd, 0, 255),
                progress) / 255f * MathHelper.Clamp(FrameAlphaMultiplier, 0f, 1f);
        }

        public Vector2 ResolveOrigin(int currentTime)
        {
            float progress = ResolveProgress(currentTime);
            Vector2 origin = new(LayerOrigin.X, LayerOrigin.Y);
            Vector2 shift = Vector2.Lerp(OriginShiftStart, OriginShiftEnd, progress);
            return origin + shift;
        }

        public Vector2 ResolveOriginShift(int currentTime)
        {
            float progress = ResolveProgress(currentTime);
            return Vector2.Lerp(OriginShiftStart, OriginShiftEnd, progress);
        }

        public int ResolveAlphaVector(int currentTime)
        {
            float progress = ResolveProgress(currentTime);
            return (int)MathF.Round(MathHelper.Lerp(
                ResolveAlphaVectorStart(),
                ResolveAlphaVectorEnd(),
                progress));
        }

        private float ResolveProgress(int currentTime)
        {
            return MathHelper.Clamp(
                (currentTime - StartTime) / (float)Math.Max(1, Duration),
                0f,
                1f);
        }

        private int ResolveAlphaVectorStart()
        {
            return HasAlphaVectorStart || AlphaVectorStart != 0
                ? AlphaVectorStart
                : AlphaStart;
        }

        private int ResolveAlphaVectorEnd()
        {
            return HasAlphaVectorEnd || AlphaVectorEnd != 0
                ? AlphaVectorEnd
                : AlphaEnd;
        }
    }

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
        public float PreviousX { get; set; }
        public float PreviousY { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public bool FacingRight { get; set; }
        public bool? QueuedFacingRight { get; set; }

        // State
        public int SpawnTime { get; set; }
        public int HitCount { get; set; }
        public List<int> HitMobIds { get; set; } = new();
        public bool IsExpired { get; set; }
        public bool IsExploding { get; set; }
        public int ExplodeTime { get; set; }
        public bool VisualOnly { get; set; }

        // Owner
        public int OwnerId { get; set; }
        public float OwnerX { get; set; }            // For homing reference
        public float OwnerY { get; set; }
        public int? PreferredTargetMobId { get; set; }
        public Vector2? PreferredTargetPosition { get; set; }
        public bool PreferStoredTargetPosition { get; set; }
        public bool UsesAuthoredShootPoint { get; set; }
        public float FallbackShootPointYOffset { get; set; } = ClientShootAttackFamilyResolver.DefaultShootAttackPointYOffset;
        public bool HasExplicitAttackOriginOverride { get; set; }
        public ShootAmmoSelection ResolvedShootAmmoSelection { get; set; }
        public int ResolvedShootWeaponCode { get; set; }
        public int ResolvedShootWeaponItemId { get; set; }
        public bool AllowFollowUpQueue { get; set; } = true;
        public bool ForceCritical { get; set; }
        public bool IsQueuedFinalAttack { get; set; }
        public bool IsQueuedSparkAttack { get; set; }
        public Vector2? QueuedFollowUpSourceMicroOffsetSnapshot { get; set; }
        public int PresentationBulletIndex { get; set; }
        public int PresentationBulletCount { get; set; } = 1;
        public int ClientActionSpeedDegree { get; set; }
        public int BulletAnimationOwnerId { get; set; }
        public BulletAnimationPresentation BulletAnimation { get; set; }
        public RegisteredBulletPresentationContext RegisteredBulletPresentation { get; set; }
        public int ImpactPresentationBaseTime { get; set; } = int.MinValue;
        public List<ProjectileAfterimageLayer> AfterimageLayers { get; } = new();
        public int LastAfterimageUpdateTime { get; set; } = int.MinValue;

        public void Update(float deltaTime, int currentTime)
        {
            if (IsExpired) return;

            PreviousX = X;
            PreviousY = Y;

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

        public bool CanHitMob(int mobId, int maxHits)
        {
            if (HitMobIds.Contains(mobId)) return false;
            if (HitCount >= maxHits) return false;
            return true;
        }

        public void RegisterHit(int mobId, int currentTime, int maxHits)
        {
            HitMobIds.Add(mobId);
            HitCount++;

            if (HitCount < maxHits)
            {
                return;
            }

            if (Data.Piercing)
            {
                IsExpired = true;
            }
            else
            {
                Explode(currentTime);
            }
        }
    }

    #endregion

    #region Skill Cast Info

    /// <summary>
    /// Explicit request for the shared skill-use animation owner.
    /// </summary>
    public class SkillUseEffectRequest
    {
        public int EffectSkillId { get; set; }
        public int SourceSkillId { get; set; }
        public int RequestTime { get; set; }
        public IReadOnlyList<string> BranchNames { get; set; }
        public int? EffectBranchLastIndex { get; set; }
        public Vector2? WorldOrigin { get; set; }
        public Point OriginOffset { get; set; }
        public bool FollowOwnerPosition { get; set; } = true;
        public bool FollowOwnerFacing { get; set; } = true;
        public bool? FacingRightOverride { get; set; }
        public int? DelayRateOverride { get; set; }
    }

    /// <summary>
    /// Information about a skill being cast
    /// </summary>
    public class SkillCastInfo
    {
        public int SkillId { get; set; }
        public int Level { get; set; }
        public SkillData SkillData { get; set; }
        public SkillLevelData LevelData { get; set; }
        public SkillAnimation EffectAnimation { get; set; }
        public SkillAnimation SecondaryEffectAnimation { get; set; }
        public bool SuppressEffectAnimation { get; set; }

        public int CastTime { get; set; }
        public int CasterId { get; set; }
        public float CasterX { get; set; }
        public float CasterY { get; set; }
        public bool FacingRight { get; set; }
        public IReadOnlyList<string> RequestedBranchNames { get; set; }
        public int? EffectBranchLastIndex { get; set; }
        public Point OriginOffset { get; set; }
        public bool FollowOwnerPosition { get; set; } = true;
        public bool FollowOwnerFacing { get; set; } = true;
        public bool? FacingRightOverride { get; set; }
        public int? DelayRateOverride { get; set; }

        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public List<int> TargetMobIds { get; set; } = new();

        public bool IsComplete { get; set; }
        public int AnimationTime => Environment.TickCount - CastTime;
    }

    #endregion

    #region Prepared Skill

    /// <summary>
    /// Active prepare / charge skill state
    /// </summary>
    public class PreparedSkill
    {
        public int SkillId { get; set; }
        public int Level { get; set; }
        public int OwnerHotkeySlot { get; set; } = -1;
        public int OwnerInputToken { get; set; }
        public int ReservedTargetMobId { get; set; }
        public int StartTime { get; set; }
        public int Duration { get; set; }
        public int MaxHoldDurationMs { get; set; }
        public int HudGaugeDurationMs { get; set; }
        public string HudSkinKey { get; set; } = "KeyDownBar";
        public PreparedSkillHudTextVariant HudTextVariant { get; set; } = PreparedSkillHudTextVariant.Default;
        public bool ShowHudBar { get; set; } = true;
        public bool ShowHudText { get; set; } = true;
        public PreparedSkillHudSurface HudSurface { get; set; } = PreparedSkillHudSurface.StatusBar;
        public SkillData SkillData { get; set; }
        public SkillLevelData LevelData { get; set; }
        public bool IsKeydownSkill { get; set; }
        public bool IsHolding { get; set; }
        public int HoldStartTime { get; set; }
        public int LastRepeatTime { get; set; }
        public bool PlayHoldWeaponSfx { get; set; }
        public int HoldWeaponSfxRepeatIntervalMs { get; set; }
        public int LastHoldWeaponSfxTime { get; set; } = int.MinValue;

        public int Elapsed(int currentTime) => Math.Max(0, currentTime - StartTime);
        public int HoldElapsed(int currentTime) => IsHolding ? Math.Max(0, currentTime - HoldStartTime) : 0;

        public float Progress(int currentTime)
        {
            if (Duration <= 0)
                return 1f;

            return Math.Clamp(Elapsed(currentTime) / (float)Duration, 0f, 1f);
        }
    }

    #endregion

    #region Active Summon

    /// <summary>
    /// Generic summon state used by the simulator for summon-family skills
    /// </summary>
    public class ActiveSummon
    {
        public int ObjectId { get; set; }
        public int SummonSlotIndex { get; set; } = -1;
        public int SkillId { get; set; }
        public int Level { get; set; }
        public int StartTime { get; set; }
        public int Duration { get; set; }
        public int LastAttackTime { get; set; }
        public int LastAttackAnimationStartTime { get; set; } = int.MinValue;
        public int RemovalAnimationStartTime { get; set; } = int.MinValue;
        public int MoveAbility { get; set; }
        public SummonMovementStyle MovementStyle { get; set; }
        public float SpawnDistanceX { get; set; }
        public float AnchorX { get; set; }
        public float AnchorY { get; set; }
        public float PreviousPositionX { get; set; }
        public float PreviousPositionY { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public bool NeedsAnchorReset { get; set; }
        public SkillData SkillData { get; set; }
        public SkillLevelData LevelData { get; set; }
        public bool FacingRight { get; set; }
        public SummonAssistType AssistType { get; set; } = SummonAssistType.PeriodicAttack;
        public bool ManualAssistEnabled { get; set; } = true;
        public bool PendingManualAttackRequest { get; set; }
        public int PendingManualAttackRequestedAt { get; set; } = int.MinValue;
        public int PendingManualAttackPrimaryTargetMobId { get; set; }
        public int[] PendingManualAttackTargetMobIds { get; set; } = Array.Empty<int>();
        public int[] PendingManualAttackConfirmedTargetMobIds { get; set; } = Array.Empty<int>();
        public int PendingManualAttackFollowUpAt { get; set; } = int.MinValue;
        public int LastManualAttackResolvedTime { get; set; } = int.MinValue;
        public bool PendingTeslaAttackRequest { get; set; }
        public int PendingTeslaAttackRequestedAt { get; set; } = int.MinValue;
        public int[] PendingTeslaAttackTargetMobIds { get; set; } = Array.Empty<int>();
        public int[] PendingTeslaAttackConfirmedTargetMobIds { get; set; } = Array.Empty<int>();
        public int LastTeslaAttackResolvedTime { get; set; } = int.MinValue;
        public int NextSupportTime { get; set; }
        public int SupportSuspendUntilTime { get; set; } = int.MinValue;
        public int NextHealTime { get; set; } = int.MinValue;
        public int NextBuffTime { get; set; } = int.MinValue;
        public int PendingRemovalTime { get; set; } = int.MaxValue;
        public int LastBodyContactTime { get; set; } = int.MinValue;
        public int LastBodyContactRelativeMotionX { get; set; }
        public bool? LastBodyContactHitFacingRight { get; set; }
        public int LastHitAnimationStartTime { get; set; } = int.MinValue;
        public int HitPeriodRemainingMs { get; set; }
        public int LastHitPeriodUpdateTime { get; set; } = int.MinValue;
        public uint HitFlashCounter { get; set; }
        public int LastPassiveEffectTime { get; set; } = int.MinValue;
        public int LastStateChangeTime { get; set; }
        public SummonActorState ActorState { get; set; } = SummonActorState.Spawn;
        public string CurrentAnimationBranchName { get; set; }
        public SkillAnimation OneTimeActionFallbackAnimation { get; set; }
        public int OneTimeActionFallbackActionCode { get; set; }
        public int OneTimeActionFallbackStartTime { get; set; } = int.MinValue;
        public int OneTimeActionFallbackAnimationTime { get; set; } = int.MinValue;
        public int OneTimeActionFallbackEndTime { get; set; } = int.MinValue;
        public bool ExpiryActionTriggered { get; set; }
        public int MaxHealth { get; set; } = 1;
        public int CurrentHealth { get; set; } = 1;
        public byte TeslaCoilState { get; set; }
        public Point[] TeslaTrianglePoints { get; set; } = Array.Empty<Point>();

        public bool IsPendingRemoval => PendingRemovalTime != int.MaxValue;

        public bool HasReachedNaturalExpiry(int currentTime)
        {
            return Duration > 0 && currentTime - StartTime >= Duration;
        }

        public bool IsExpired(int currentTime)
        {
            return IsPendingRemoval && currentTime >= PendingRemovalTime;
        }

        public int GetRemainingTime(int currentTime)
        {
            if (IsPendingRemoval)
            {
                return Math.Max(0, PendingRemovalTime - currentTime);
            }

            if (Duration <= 0)
            {
                return 0;
            }

            return Math.Max(0, (StartTime + Duration) - currentTime);
        }
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
                if (!skill.IsPassive && !skill.Invisible && !skill.SuppressesStandaloneActiveCast)
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
