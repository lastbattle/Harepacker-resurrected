using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Managers;
using HaSharedLibrary.Render.DX;
using HaSharedLibrary.Util;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.Xna.Framework.Graphics;
using Point = Microsoft.Xna.Framework.Point;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace HaCreator.MapSimulator.Character.Skills
{
    /// <summary>
    /// Loads skill data from Skill.wz
    /// </summary>
    public class SkillLoader
    {
        private static readonly string[] PreferredSummonAnimationBranches =
        {
            "stand",
            "fly",
            "move",
            "walk",
            "attack1",
            "attack",
            "die"
        };

        private static readonly string[] PreferredSummonSpawnBranches =
        {
            "summoned",
            "create",
            "summon"
        };

        private static readonly string[] PreferredSummonAttackBranches =
        {
            "attack1",
            "attack",
            "attack0"
        };

        private static readonly string[] PersistentAvatarEffectBranches =
        {
            "special",
            "special0",
            "finish",
            "finish0",
            "back",
            "back_finish"
        };

        private readonly WzFile _skillWz;
        private readonly GraphicsDevice _device;
        private readonly TexturePool _texturePool;

        // Caches
        private readonly Dictionary<int, SkillData> _skillCache = new();
        private readonly Dictionary<int, JobSkillBook> _jobCache = new();
        private readonly HashSet<int> _skillsWithoutCastSound = new();
        private readonly HashSet<int> _skillsWithoutRepeatSound = new();
        private WzImage _skillSoundImage;

        public SkillLoader(WzFile skillWz, GraphicsDevice device, TexturePool texturePool)
        {
            _skillWz = skillWz;
            _device = device;
            _texturePool = texturePool;
        }

        #region Skill Loading

        /// <summary>
        /// Load a skill by ID
        /// </summary>
        public SkillData LoadSkill(int skillId)
        {
            if (_skillCache.TryGetValue(skillId, out var cached))
                return cached;

            var skill = LoadSkillInternal(skillId);
            if (skill != null)
            {
                _skillCache[skillId] = skill;
            }
            return skill;
        }

        public string EnsureCastSoundRegistered(SkillData skill, SoundManager soundManager)
        {
            if (skill == null || soundManager == null)
                return null;

            if (!string.IsNullOrEmpty(skill.CastSoundKey))
                return skill.CastSoundKey;

            if (_skillsWithoutCastSound.Contains(skill.SkillId))
                return null;

            var skillSoundNode = GetSkillSoundNode(skill.SkillId);
            if (skillSoundNode == null)
            {
                _skillsWithoutCastSound.Add(skill.SkillId);
                return null;
            }

            foreach (string preferredName in new[] { "Use", "Attack1" })
            {
                if (TryRegisterSkillSound(skill, soundManager, skillSoundNode[preferredName], preferredName))
                    return skill.CastSoundKey;
            }

            foreach (var child in skillSoundNode.WzProperties)
            {
                if (TryRegisterSkillSound(skill, soundManager, child, child.Name))
                    return skill.CastSoundKey;
            }

            _skillsWithoutCastSound.Add(skill.SkillId);
            return null;
        }

        public string EnsureRepeatSoundRegistered(SkillData skill, SoundManager soundManager)
        {
            if (skill == null || soundManager == null)
                return null;

            if (!string.IsNullOrEmpty(skill.RepeatSoundKey))
                return skill.RepeatSoundKey;

            if (_skillsWithoutRepeatSound.Contains(skill.SkillId))
                return null;

            var skillSoundNode = GetSkillSoundNode(skill.SkillId);
            if (skillSoundNode == null)
            {
                _skillsWithoutRepeatSound.Add(skill.SkillId);
                return null;
            }

            foreach (string preferredName in new[] { "Hit", "Attack1", "Loop" })
            {
                if (TryRegisterSkillSound(soundManager, skillSoundNode[preferredName], skill.SkillId, preferredName, out string soundKey))
                {
                    skill.RepeatSoundKey = soundKey;
                    return soundKey;
                }
            }

            _skillsWithoutRepeatSound.Add(skill.SkillId);
            return null;
        }

        /// <summary>
        /// Load every skill book available in Skill.wz and merge the results into a single catalog.
        /// </summary>
        public List<SkillData> LoadAllSkills()
        {
            var skills = new List<SkillData>();

            foreach (int jobId in EnumerateAvailableSkillBookJobIds())
            {
                var book = LoadJobSkills(jobId);
                if (book == null || book.Skills.Count == 0)
                    continue;

                skills.AddRange(book.Skills.Values);
            }

            if (skills.Count == 0)
                return skills;

            var seen = new HashSet<int>();
            var result = new List<SkillData>(skills.Count);
            foreach (var skill in skills)
            {
                if (skill == null)
                    continue;

                if (seen.Add(skill.SkillId))
                    result.Add(skill);
            }

            return result;
        }

        private SkillData LoadSkillInternal(int skillId)
        {
            // Skill.wz structure: Skill.wz/[jobId].img/skill/[skillId]
            // Job ID is skillId / 10000
            int jobId = skillId / 10000;
            string imgName = $"{jobId}.img";

            var jobImg = GetSkillImage(imgName);
            if (jobImg == null)
                return null;

            jobImg.ParseImage();

            var skillNode = jobImg["skill"]?[skillId.ToString()];
            if (skillNode == null)
                return null;

            var skill = new SkillData
            {
                SkillId = skillId,
                Job = jobId
            };

            // Parse basic info
            ParseSkillInfo(skill, skillNode);

            // Parse level data
            ParseSkillLevels(skill, skillNode);

            // Parse animations
            ParseSkillAnimations(skill, skillNode);

            // Load icon
            LoadSkillIcon(skill, skillNode);

            return skill;
        }

        private void ParseSkillInfo(SkillData skill, WzImageProperty skillNode)
        {
            skill.ActionName = GetPrimaryActionName(skillNode);

            // Basic properties from info node
            var infoNode = skillNode["info"];
            if (infoNode != null)
            {
                // Hidden skill
                skill.Invisible = GetInt(infoNode, "invisible") == 1;
                skill.MasterOnly = GetInt(infoNode, "masterOnly") == 1;
                skill.IsRapidAttack = GetInt(infoNode, "rapidAttack") == 1;
            }

            // Check common nodes
            var commonNode = skillNode["common"];
            if (commonNode != null)
            {
                skill.MaxLevel = GetInt(commonNode, "maxLevel", 1);
            }

            // Parse level nodes to find max level
            var levelNode = skillNode["level"];
            if (levelNode != null)
            {
                foreach (var child in levelNode.WzProperties)
                {
                    if (int.TryParse(child.Name, out int level))
                    {
                        skill.MaxLevel = Math.Max(skill.MaxLevel, level);
                    }
                }
            }

            // Determine skill type from properties
            DetermineSkillType(skill, skillNode);
        }

        private void DetermineSkillType(SkillData skill, WzImageProperty skillNode)
        {
            // Check for various type indicators
            var infoNode = skillNode["info"];
            bool hasBall = skillNode["ball"] != null;
            bool hasHit = skillNode["hit"] != null;
            bool hasAffected = skillNode["affected"] != null;
            bool hasSummon = skillNode["summon"] != null;
            bool hasPrepare = skillNode["prepare"] != null || skillNode["keydown"] != null || skillNode["keyDown"] != null;
            bool hasAction = !string.IsNullOrWhiteSpace(skill.ActionName);
            bool looksLikeMovement = MatchesAction(skill.ActionName, "teleport", "rush", "dash", "flash", "jump", "step", "fly");
            bool looksLikePrepare = hasPrepare || MatchesAction(skill.ActionName, "prepare", "charge", "keydown", "keyDown");
            bool isSuddenDeathSkill = GetInt(infoNode, "suddenDeath") == 1;
            bool hasPersistentAvatarEffect = HasPersistentAvatarEffectBranches(
                skillNode.WzProperties.Select(child => child.Name),
                isSuddenDeathSkill);

            var commonNode = skillNode["common"];
            int commonDamage = GetInt(commonNode, "damage");
            int commonTime = GetInt(commonNode, "time");
            int commonMobCount = GetInt(commonNode, "mobCount");
            int commonHp = GetInt(commonNode, "hp") + GetInt(commonNode, "hpR");
            int commonPad = GetInt(commonNode, "pad");
            int commonMad = GetInt(commonNode, "mad");

            var levelNode = skillNode["level"]?["1"];
            int levelDamage = GetInt(levelNode, "damage");
            int levelMobCount = GetInt(levelNode, "mobCount");
            int levelHp = GetInt(levelNode, "hp") + GetInt(levelNode, "hpR");
            int levelTime = GetInt(levelNode, "time");
            int levelPad = GetInt(levelNode, "pad");
            int levelMad = GetInt(levelNode, "mad");

            bool hasDamage = commonDamage > 0 || levelDamage > 0;
            bool hasTime = commonTime > 0 || levelTime > 0;
            bool hasMobCount = commonMobCount > 0 || levelMobCount > 0;
            bool hasHp = commonHp > 0 || levelHp > 0;
            bool hasPad = commonPad > 0 || levelPad > 0;
            bool hasMad = commonMad > 0 || levelMad > 0;

            skill.IsMovement = looksLikeMovement;
            skill.IsPrepareSkill = looksLikePrepare;

            if (commonNode != null)
            {
                if (hasSummon)
                {
                    skill.Type = SkillType.Summon;
                    skill.IsSummon = true;
                    skill.IsAttack = hasDamage || hasMobCount;
                }
                else if (hasHp && !hasDamage)
                {
                    skill.Type = SkillType.Heal;
                    skill.IsHeal = true;
                }
                else if (hasTime && (hasPad || hasMad || hasAffected || skill.IsMovement))
                {
                    skill.Type = SkillType.Buff;
                    skill.IsBuff = true;
                }
                else if (hasDamage || hasMobCount || skill.IsPrepareSkill || skill.IsMovement)
                {
                    skill.Type = hasBall ? SkillType.Magic : (skill.IsMovement ? SkillType.Movement : SkillType.Attack);
                    skill.IsAttack = hasDamage || hasMobCount || skill.IsPrepareSkill;
                }
                else if (!hasDamage && !hasTime && !hasAction)
                {
                    skill.Type = SkillType.Passive;
                    skill.IsPassive = true;
                }
            }
            else if (hasSummon || hasDamage || hasMobCount || skill.IsMovement || skill.IsPrepareSkill)
            {
                skill.Type = hasSummon
                    ? SkillType.Summon
                    : hasBall
                        ? SkillType.Magic
                        : skill.IsMovement
                            ? SkillType.Movement
                            : SkillType.Attack;
                skill.IsSummon = hasSummon;
                skill.IsAttack = hasDamage || hasMobCount || skill.IsPrepareSkill;
            }
            else if (hasTime || hasAffected)
            {
                skill.Type = SkillType.Buff;
                skill.IsBuff = true;
            }
            else
            {
                skill.Type = SkillType.Passive;
                skill.IsPassive = true;
            }

            // Determine attack type
            if (skill.IsSummon)
            {
                skill.AttackType = SkillAttackType.Summon;
            }
            else if (skill.IsMovement || skill.IsPrepareSkill)
            {
                skill.AttackType = SkillAttackType.Special;
            }
            else if (hasBall)
            {
                skill.AttackType = SkillAttackType.Ranged;
            }
            else if (skill.Type == SkillType.Magic)
            {
                skill.AttackType = SkillAttackType.Magic;
            }
            else
            {
                skill.AttackType = SkillAttackType.Melee;
            }

            // Determine target type
            var levelOneNode = skillNode["level"]?["1"];
            if (levelOneNode != null)
            {
                int mobCount = GetInt(levelOneNode, "mobCount", 1);
                if (skill.IsBuff)
                {
                    skill.Target = SkillTarget.Self;
                }
                else if (mobCount > 1)
                {
                    skill.Target = SkillTarget.MultipleEnemy;
                }
                else
                {
                    skill.Target = SkillTarget.SingleEnemy;
                }
            }

            skill.IsBuff |= hasTime && (hasPad || hasMad || hasAffected || hasPersistentAvatarEffect);
            skill.IsHeal |= hasHp && !hasDamage;
        }

        private void ParseSkillLevels(SkillData skill, WzImageProperty skillNode)
        {
            int eventTamingMobId = GetInt(skillNode, "eventTamingMob");
            var levelNode = skillNode["level"];
            if (levelNode != null)
            {
                foreach (var child in levelNode.WzProperties)
                {
                    if (!int.TryParse(child.Name, out int level))
                        continue;

                    var levelData = CreateLevelData(child, level);
                    if (eventTamingMobId > 0 && levelData.ItemConNo <= 0)
                    {
                        levelData.ItemConNo = eventTamingMobId;
                    }

                    skill.Levels[level] = levelData;
                }
            }

            var commonNode = skillNode["common"];
            if (commonNode != null && skill.Levels.Count == 0)
            {
                int maxLevel = Math.Max(1, GetInt(commonNode, "maxLevel", skill.MaxLevel > 0 ? skill.MaxLevel : 1));
                skill.MaxLevel = Math.Max(skill.MaxLevel, maxLevel);

                for (int level = 1; level <= maxLevel; level++)
                {
                    var levelData = CreateLevelData(commonNode, level);
                    if (eventTamingMobId > 0 && levelData.ItemConNo <= 0)
                    {
                        levelData.ItemConNo = eventTamingMobId;
                    }

                    skill.Levels[level] = levelData;
                }
            }
        }

        #endregion

        #region Animation Loading

        private void ParseSkillAnimations(SkillData skill, WzImageProperty skillNode)
        {
            var prepareNode = skillNode["prepare"];
            if (prepareNode != null)
            {
                skill.PrepareActionName = GetString(prepareNode, "action");
                skill.PrepareDurationMs = ResolveAnimationDuration(prepareNode);

                var prepareAnimation = LoadSkillAnimation(prepareNode, "prepare");
                if (prepareAnimation.Frames.Count > 0)
                {
                    skill.PrepareEffect = prepareAnimation;
                }
            }

            var keydownNode = skillNode["keydown"] ?? skillNode["keyDown"];
            if (keydownNode != null)
            {
                skill.KeydownActionName = GetString(keydownNode, "action");
                skill.KeydownDurationMs = ResolveAnimationDuration(keydownNode);
                skill.KeydownRepeatIntervalMs = ResolveKeydownRepeatInterval(keydownNode);

                var keydownAnimation = LoadSkillAnimation(keydownNode, "keydown");
                if (keydownAnimation.Frames.Count > 0)
                {
                    keydownAnimation.Loop = true;
                    skill.KeydownEffect = keydownAnimation;
                }
            }

            var keydownEndNode = skillNode["keydownend"] ?? skillNode["keyDownEnd"];
            if (keydownEndNode != null)
            {
                skill.KeydownEndActionName = GetString(keydownEndNode, "action");
                skill.KeydownEndDurationMs = ResolveAnimationDuration(keydownEndNode);

                var keydownEndAnimation = LoadSkillAnimation(keydownEndNode, "keydownend");
                if (keydownEndAnimation.Frames.Count > 0)
                {
                    skill.KeydownEndEffect = keydownEndAnimation;
                }
            }

            skill.IsKeydownSkill = keydownNode != null || keydownEndNode != null || skill.IsRapidAttack;

            // Load effect animation
            var effectNode = skillNode["effect"];
            if (effectNode != null)
            {
                skill.Effect = LoadSkillAnimation(effectNode, "effect");
            }

            // Load hit effect
            var hitNode = skillNode["hit"];
            if (hitNode != null)
            {
                skill.HitEffect = LoadSkillAnimation(hitNode, "hit");
            }

            // Load affected (buff visual)
            var affectedNode = skillNode["affected"];
            if (affectedNode != null)
            {
                skill.AffectedEffect = LoadSkillAnimation(affectedNode, "affected");
            }

            LoadPersistentAvatarEffects(skill, skillNode);

            var summonNode = skillNode["summon"];
            if (summonNode != null)
            {
                LoadSummonAnimations(skill, summonNode);
            }

            // Load projectile/ball
            var ballNode = skillNode["ball"];
            if (ballNode != null)
            {
                skill.Projectile = LoadProjectile(skill.SkillId, ballNode, skillNode);
            }
        }

        private void LoadPersistentAvatarEffects(SkillData skill, WzImageProperty skillNode)
        {
            if (skill == null || skillNode == null)
            {
                return;
            }

            skill.AvatarOverlayEffect = LoadAvatarEffectAnimation(skillNode, "special")
                                        ?? LoadSuddenDeathRepeatAnimation(skill, skillNode);
            skill.AvatarUnderFaceEffect = LoadAvatarEffectAnimation(skillNode, "special0");
            skill.AvatarLadderEffect = LoadAvatarEffectAnimation(skillNode, "back");
            skill.AvatarOverlayFinishEffect = LoadAvatarEffectAnimation(skillNode, "finish");
            skill.AvatarUnderFaceFinishEffect = LoadAvatarEffectAnimation(skillNode, "finish0");
            skill.AvatarLadderFinishEffect = LoadAvatarEffectAnimation(skillNode, "back_finish");
        }

        private SkillAnimation LoadAvatarEffectAnimation(WzImageProperty skillNode, string branchName)
        {
            if (skillNode == null || string.IsNullOrWhiteSpace(branchName))
            {
                return null;
            }

            WzImageProperty branch = skillNode[branchName];
            if (branch == null)
            {
                return null;
            }

            SkillAnimation animation = LoadSkillAnimation(branch, branchName);
            if (animation.Frames.Count == 0)
            {
                return null;
            }

            if (!branchName.Contains("finish", StringComparison.OrdinalIgnoreCase))
            {
                animation.Loop = true;
            }

            return animation;
        }

        private SkillAnimation LoadSuddenDeathRepeatAnimation(SkillData skill, WzImageProperty skillNode)
        {
            if (skill == null || skillNode == null)
            {
                return null;
            }

            if (GetInt(skillNode["info"], "suddenDeath") != 1)
            {
                return null;
            }

            WzImageProperty repeatNode = skillNode["repeat"];
            if (repeatNode == null)
            {
                return null;
            }

            SkillAnimation animation = LoadSkillAnimation(repeatNode, "repeat");
            if (animation.Frames.Count == 0)
            {
                return null;
            }

            animation.Loop = true;
            return animation;
        }

        private SkillAnimation LoadSkillAnimation(WzImageProperty node, string name)
        {
            var animation = new SkillAnimation { Name = name };

            // Try numbered frames (0, 1, 2, ...)
            int frameIndex = 0;
            while (true)
            {
                var frameNode = node[frameIndex.ToString()];
                if (frameNode == null)
                    break;

                var frame = LoadSkillFrame(frameNode);
                if (frame != null)
                {
                    animation.Frames.Add(frame);
                }
                frameIndex++;
            }

            // If no numbered frames, check for direct canvas
            if (animation.Frames.Count == 0 && node is WzCanvasProperty canvas)
            {
                var frame = LoadSkillFrame(node);
                if (frame != null)
                {
                    animation.Frames.Add(frame);
                }
            }

            // Check for subeffects (effect0, effect1, etc.)
            if (animation.Frames.Count == 0)
            {
                foreach (var child in node.WzProperties)
                {
                    if (child.Name.StartsWith("effect") || child.Name.All(char.IsDigit))
                    {
                        var subAnim = LoadSkillAnimation(child, child.Name);
                        animation.Frames.AddRange(subAnim.Frames);
                    }
                }
            }

            animation.CalculateDuration();

            // Check for loop property
            animation.Loop = GetInt(node, "loop") == 1;

            // Get origin/position
            var originX = GetInt(node, "x");
            var originY = GetInt(node, "y");
            animation.Origin = new Point(originX, originY);

            // Get z-order
            animation.ZOrder = GetInt(node, "z");

            return animation;
        }

        private static int ResolveAnimationDuration(WzImageProperty node)
        {
            if (node == null)
                return 0;

            int explicitDuration = GetInt(node, "time");
            if (explicitDuration > 0)
                return explicitDuration;

            int duration = 0;
            foreach (var child in node.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                    continue;

                int frameDelay = GetInt(child, "delay");
                duration += frameDelay > 0 ? frameDelay : 100;
            }

            return duration;
        }

        private static int ResolveKeydownRepeatInterval(WzImageProperty node)
        {
            if (node == null)
                return 90;

            int bestDelay = int.MaxValue;
            int frameCount = 0;

            foreach (var child in node.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                    continue;

                frameCount++;
                int frameDelay = GetInt(child, "delay");
                if (frameDelay > 0)
                {
                    bestDelay = Math.Min(bestDelay, frameDelay);
                }
            }

            if (bestDelay != int.MaxValue)
                return bestDelay;

            int duration = ResolveAnimationDuration(node);
            if (duration > 0 && frameCount > 0)
                return Math.Max(1, duration / frameCount);

            return 90;
        }

        private void LoadSummonAnimations(SkillData skill, WzImageProperty summonNode)
        {
            var branchNames = summonNode.WzProperties
                .Select(child => child.Name)
                .ToArray();
            SummonMovementProfile movementProfile = SummonMovementResolver.Resolve(skill.SkillId, branchNames);
            skill.SummonMoveAbility = movementProfile.MoveAbility;
            skill.SummonMovementStyle = movementProfile.Style;
            skill.SummonSpawnDistanceX = movementProfile.SpawnDistanceX;

            var directAnimation = LoadSkillAnimation(summonNode, "summon");

            string spawnBranchName = SelectPreferredSummonSpawnBranch(branchNames);
            if (spawnBranchName != null)
            {
                var spawnBranch = summonNode[spawnBranchName];
                if (spawnBranch != null)
                {
                    var spawnAnimation = LoadSkillAnimation(spawnBranch, spawnBranchName);
                    if (spawnAnimation.Frames.Count > 0)
                    {
                        skill.SummonSpawnAnimation = spawnAnimation;
                    }
                }
            }

            string preferredBranchName = SelectPreferredSummonIdleBranch(branchNames);
            if (preferredBranchName == null)
            {
                if (directAnimation.Frames.Count > 0)
                {
                    skill.SummonAnimation = directAnimation;
                    if (skill.SummonSpawnAnimation == null)
                    {
                        skill.SummonSpawnAnimation = directAnimation;
                    }
                }

                return;
            }

            var preferredBranch = summonNode[preferredBranchName];
            if (preferredBranch == null)
            {
                if (directAnimation.Frames.Count > 0)
                {
                    skill.SummonAnimation = directAnimation;
                }

                return;
            }

            var summonAnimation = LoadSkillAnimation(preferredBranch, "summon");
            if (summonAnimation.Frames.Count == 0)
            {
                if (directAnimation.Frames.Count > 0)
                {
                    skill.SummonAnimation = directAnimation;
                }

                return;
            }

            if (!summonAnimation.Loop
                && (preferredBranchName.Equals("stand", StringComparison.OrdinalIgnoreCase)
                    || preferredBranchName.Equals("fly", StringComparison.OrdinalIgnoreCase)
                    || preferredBranchName.Equals("move", StringComparison.OrdinalIgnoreCase)
                    || preferredBranchName.Equals("walk", StringComparison.OrdinalIgnoreCase)))
            {
                summonAnimation.Loop = true;
            }

            skill.SummonAnimation = summonAnimation;
            if (skill.SummonSpawnAnimation == null)
            {
                skill.SummonSpawnAnimation = summonAnimation;
            }

            string attackBranchName = SelectPreferredSummonAttackBranch(branchNames);
            if (attackBranchName == null)
            {
                return;
            }

            var attackBranch = summonNode[attackBranchName];
            if (attackBranch == null)
            {
                return;
            }

            var attackAnimation = LoadSkillAnimation(attackBranch, attackBranchName);
            if (attackAnimation.Frames.Count > 0)
            {
                skill.SummonAttackAnimation = attackAnimation;
            }
        }

        public static string SelectPreferredSummonSpawnBranch(IEnumerable<string> branchNames)
        {
            return SelectPreferredSummonBranch(branchNames, PreferredSummonSpawnBranches);
        }

        public static string SelectPreferredSummonIdleBranch(IEnumerable<string> branchNames)
        {
            return SelectPreferredSummonBranch(branchNames, PreferredSummonAnimationBranches);
        }

        public static string SelectPreferredSummonAttackBranch(IEnumerable<string> branchNames)
        {
            if (branchNames == null)
            {
                return null;
            }

            var availableBranches = branchNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);

            foreach (string preferredBranch in PreferredSummonAttackBranches)
            {
                if (availableBranches.TryGetValue(preferredBranch, out string actualBranchName))
                {
                    return actualBranchName;
                }
            }

            return null;
        }

        public static bool HasPersistentAvatarEffectBranches(IEnumerable<string> branchNames, bool suddenDeath)
        {
            if (branchNames == null)
            {
                return false;
            }

            var availableBranches = new HashSet<string>(
                branchNames.Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);

            foreach (string branchName in PersistentAvatarEffectBranches)
            {
                if (availableBranches.Contains(branchName))
                {
                    return true;
                }
            }

            return suddenDeath && availableBranches.Contains("repeat");
        }

        private static string SelectPreferredSummonBranch(IEnumerable<string> branchNames, IEnumerable<string> preferredBranches)
        {
            if (branchNames == null)
            {
                return null;
            }

            var availableBranches = branchNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);

            foreach (string preferredBranch in preferredBranches)
            {
                if (availableBranches.TryGetValue(preferredBranch, out string actualBranchName))
                {
                    return actualBranchName;
                }
            }

            return availableBranches.Keys.FirstOrDefault();
        }

        private SkillFrame LoadSkillFrame(WzImageProperty frameNode)
        {
            // Handle canvas directly
            WzCanvasProperty canvas = null;

            if (frameNode is WzCanvasProperty directCanvas)
            {
                canvas = directCanvas;
            }
            else
            {
                // Try to get from _inlink or _outlink
                var linked = frameNode.GetLinkedWzImageProperty();
                canvas = linked as WzCanvasProperty;
            }

            if (canvas == null)
                return null;

            var frame = new SkillFrame();

            // Get bitmap and create texture
            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap != null)
            {
                var texture = bitmap.ToTexture2DAndDispose(_device);
                if (texture != null)
                {
                    var origin = canvas.GetCanvasOriginPosition();
                    frame.Texture = new DXObject(0, 0, texture)
                    {
                        Tag = canvas.FullPath
                    };
                    frame.Origin = new Point((int)origin.X, (int)origin.Y);
                    frame.Bounds = new Rectangle(0, 0, texture.Width, texture.Height);
                }
            }

            // Get delay
            frame.Delay = GetInt(frameNode, "delay", 100);

            // Get flip
            frame.Flip = GetInt(frameNode, "flip") == 1;

            return frame;
        }

        private ProjectileData LoadProjectile(int skillId, WzImageProperty ballNode, WzImageProperty skillNode)
        {
            var projectile = new ProjectileData
            {
                SkillId = skillId
            };

            // Load ball animation
            projectile.Animation = LoadSkillAnimation(ballNode, "ball");

            // Load hit animation
            var hitNode = ballNode["hit"] ?? skillNode["hit"];
            if (hitNode != null)
            {
                projectile.HitAnimation = LoadSkillAnimation(hitNode, "hit");
            }

            // Parse ball properties
            projectile.Speed = GetInt(ballNode, "speed", 400);
            projectile.Gravity = GetInt(ballNode, "gravity", 0);

            // Check for special behaviors
            if (GetInt(ballNode, "pierce") == 1)
            {
                projectile.Piercing = true;
                projectile.Behavior = ProjectileBehavior.Piercing;
            }

            if (GetInt(ballNode, "homing") == 1)
            {
                projectile.Behavior = ProjectileBehavior.Homing;
            }

            if (GetInt(ballNode, "explode") == 1)
            {
                projectile.Behavior = ProjectileBehavior.Exploding;
                projectile.ExplosionRadius = GetInt(ballNode, "explodeRadius", 100);

                var explodeNode = ballNode["explode"];
                if (explodeNode != null)
                {
                    projectile.ExplosionAnimation = LoadSkillAnimation(explodeNode, "explode");
                }
            }

            projectile.LifeTime = GetInt(ballNode, "life", 2000);
            projectile.MaxHits = GetInt(ballNode, "mobCount", 1);

            return projectile;
        }

        private void LoadSkillIcon(SkillData skill, WzImageProperty skillNode)
        {
            // Icons are in skill node
            var iconNode = skillNode["icon"];
            if (iconNode is WzCanvasProperty iconCanvas)
            {
                var bitmap = iconCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap != null)
                {
                    var texture = bitmap.ToTexture2DAndDispose(_device);
                    skill.Icon = new DXObject(0, 0, texture);
                    skill.IconTexture = texture;
                }
            }

            // Disabled icon
            var iconDisabledNode = skillNode["iconDisabled"];
            if (iconDisabledNode is WzCanvasProperty iconDisabledCanvas)
            {
                var bitmap = iconDisabledCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap != null)
                {
                    var texture = bitmap.ToTexture2DAndDispose(_device);
                    skill.IconDisabled = new DXObject(0, 0, texture);
                }
            }

            // Mouse over icon
            var iconMouseOverNode = skillNode["iconMouseOver"];
            if (iconMouseOverNode is WzCanvasProperty iconMouseOverCanvas)
            {
                var bitmap = iconMouseOverCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap != null)
                {
                    var texture = bitmap.ToTexture2DAndDispose(_device);
                    skill.IconMouseOver = new DXObject(0, 0, texture);
                }
            }
        }

        #endregion

        #region Job Skill Book

        /// <summary>
        /// Load all skills for a job
        /// </summary>
        public JobSkillBook LoadJobSkills(int jobId)
        {
            if (_jobCache.TryGetValue(jobId, out var cached))
                return cached;

            var book = LoadJobSkillsInternal(jobId);
            if (book != null)
            {
                _jobCache[jobId] = book;
            }
            return book;
        }

        private JobSkillBook LoadJobSkillsInternal(int jobId)
        {
            string imgName = $"{jobId}.img";
            var jobImg = GetSkillImage(imgName);
            if (jobImg == null)
                return null;

            jobImg.ParseImage();

            var book = new JobSkillBook
            {
                JobId = jobId,
                JobName = GetJobName(jobId)
            };

            var skillNode = jobImg["skill"];
            if (skillNode == null)
                return book;

            foreach (var child in skillNode.WzProperties)
            {
                if (!int.TryParse(child.Name, out int skillId))
                    continue;

                var skill = LoadSkill(skillId);
                if (skill != null)
                {
                    book.Skills[skillId] = skill;
                }
            }

            return book;
        }

        /// <summary>
        /// Load skills for exactly one job (no "job path" / advancements).
        /// </summary>
        public List<SkillData> LoadSkillsForJob(int jobId)
        {
            // Some jobs are "wrappers" over another skill book (e.g. SuperGM 910 uses GM 900 skills too).
            // Keep this narrow to avoid returning to "load everything" behavior.
            var bookJobIds = GetSkillBookJobIdsForJob(jobId);

            var skills = new List<SkillData>();
            foreach (int bookJobId in bookJobIds)
            {
                var book = LoadJobSkills(bookJobId);
                if (book != null && book.Skills.Count > 0)
                {
                    skills.AddRange(book.Skills.Values);
                }
            }

            if (skills.Count == 0)
                return skills;

            // De-dupe by skillId while preserving order.
            var seen = new HashSet<int>();
            var result = new List<SkillData>(skills.Count);
            foreach (var s in skills)
            {
                if (s == null) continue;
                if (seen.Add(s.SkillId))
                    result.Add(s);
            }

            return result;
        }

        private static IReadOnlyList<int> GetSkillBookJobIdsForJob(int jobId)
        {
            return jobId switch
            {
                // SuperGM shares/extends GM skills in many data sets.
                910 => new[] { 900, 910 },
                _ => new[] { jobId }
            };
        }

        public static IReadOnlyList<int> EnumerateSkillBookJobIds(WzFile skillWz)
        {
            if (skillWz?.WzDirectory == null)
                return Array.Empty<int>();

            var result = new SortedSet<int>();
            CollectSkillBookJobIds(skillWz.WzDirectory, result);
            return result.ToList();
        }

        private IReadOnlyList<int> EnumerateAvailableSkillBookJobIds()
        {
            var fromFile = EnumerateSkillBookJobIds(_skillWz);
            if (fromFile.Count > 0)
                return fromFile;

            var result = new SortedSet<int>();
            CollectSkillBookJobIds(Program.FindWzObject("Skill", string.Empty), result);

            if (result.Count == 0)
            {
                foreach (var directory in Program.GetDirectories("Skill"))
                {
                    CollectSkillBookJobIds(directory, result);
                }
            }

            return result.ToList();
        }

        private static void CollectSkillBookJobIds(WzDirectory directory, ISet<int> result)
        {
            if (directory == null)
                return;

            foreach (var image in directory.WzImages)
            {
                if (image == null)
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(image.Name);
                if (int.TryParse(fileName, out int jobId))
                {
                    result.Add(jobId);
                }
            }

            foreach (var subDirectory in directory.WzDirectories)
            {
                CollectSkillBookJobIds(subDirectory, result);
            }
        }

        private static void CollectSkillBookJobIds(WzObject node, ISet<int> result)
        {
            switch (node)
            {
                case WzDirectory directory:
                    CollectSkillBookJobIds(directory, result);
                    break;
                case WzImage image:
                    string fileName = Path.GetFileNameWithoutExtension(image.Name);
                    if (int.TryParse(fileName, out int jobId))
                    {
                        result.Add(jobId);
                    }
                    break;
            }
        }

        private WzImage GetSkillImage(string imgName)
        {
            return (_skillWz?.WzDirectory?[imgName] as WzImage)
                   ?? Program.FindImage("Skill", imgName);
        }

        private WzImageProperty GetSkillSoundNode(int skillId)
        {
            _skillSoundImage ??= Program.FindImage("Sound", "Skill.img");
            _skillSoundImage?.ParseImage();
            return _skillSoundImage?[skillId.ToString("D7", CultureInfo.InvariantCulture)];
        }

        private bool TryRegisterSkillSound(SkillData skill, SoundManager soundManager, WzImageProperty soundProperty, string soundName)
        {
            if (!TryRegisterSkillSound(soundManager, soundProperty, skill.SkillId, soundName, out string soundKey))
                return false;

            skill.CastSoundKey = soundKey;
            return true;
        }

        private static bool TryRegisterSkillSound(SoundManager soundManager, WzImageProperty soundProperty, int skillId, string soundName, out string soundKey)
        {
            soundKey = null;
            WzBinaryProperty soundBinary = soundProperty as WzBinaryProperty
                                          ?? (soundProperty as WzUOLProperty)?.LinkValue as WzBinaryProperty;
            if (soundBinary == null)
                return false;

            soundKey = $"Skill:{skillId}:{soundName}";
            soundManager.RegisterSound(soundKey, soundBinary);
            return true;
        }

        private static string GetPrimaryActionName(WzImageProperty skillNode)
        {
            var actionNode = skillNode["action"];
            if (actionNode == null)
                return string.Empty;

            return GetString(actionNode, "0")
                   ?? GetString(actionNode, "action")
                   ?? actionNode.WzProperties.Select(prop => GetPropertyStringValue(prop)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                   ?? string.Empty;
        }

        private static string GetPropertyStringValue(WzImageProperty property)
        {
            return property switch
            {
                WzStringProperty stringProperty => stringProperty.Value,
                _ => null
            };
        }

        private static bool MatchesAction(string actionName, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return false;

            string lowered = actionName.ToLowerInvariant();
            foreach (string keyword in keywords)
            {
                if (lowered.Contains(keyword.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        private string GetJobName(int jobId)
        {
            return jobId switch
            {
                0 => "Beginner",
                100 => "Warrior",
                110 => "Fighter",
                111 => "Crusader",
                112 => "Hero",
                120 => "Page",
                121 => "White Knight",
                122 => "Paladin",
                130 => "Spearman",
                131 => "Dragon Knight",
                132 => "Dark Knight",
                200 => "Magician",
                210 => "Fire/Poison Wizard",
                211 => "Fire/Poison Mage",
                212 => "Fire/Poison Archmage",
                220 => "Ice/Lightning Wizard",
                221 => "Ice/Lightning Mage",
                222 => "Ice/Lightning Archmage",
                230 => "Cleric",
                231 => "Priest",
                232 => "Bishop",
                300 => "Bowman",
                310 => "Hunter",
                311 => "Ranger",
                312 => "Bowmaster",
                320 => "Crossbowman",
                321 => "Sniper",
                322 => "Marksman",
                400 => "Thief",
                410 => "Assassin",
                411 => "Hermit",
                412 => "Night Lord",
                420 => "Bandit",
                421 => "Chief Bandit",
                422 => "Shadower",
                500 => "Pirate",
                510 => "Brawler",
                511 => "Marauder",
                512 => "Buccaneer",
                520 => "Gunslinger",
                521 => "Outlaw",
                522 => "Corsair",
                900 => "GM",
                910 => "SuperGM",
                _ => $"Job {jobId}"
            };
        }

        /// <summary>
        /// Load skills for a character's job path
        /// </summary>
        public List<SkillData> LoadSkillsForJobPath(int currentJob)
        {
            var skills = new List<SkillData>();

            // Get all jobs in the path
            var jobPath = GetJobPath(currentJob);
            foreach (int jobId in jobPath)
            {
                var book = LoadJobSkills(jobId);
                if (book != null)
                {
                    skills.AddRange(book.Skills.Values);
                }
            }

            return skills;
        }

        private List<int> GetJobPath(int job)
        {
            var path = new List<int> { 0 }; // Always include beginner

            if (job == 0)
                return path;

            // Add first job
            int firstJob = (job / 100) * 100;
            if (firstJob > 0)
                path.Add(firstJob);

            // Add second job
            int secondJob = (job / 10) * 10;
            if (secondJob > firstJob)
                path.Add(secondJob);

            // Add third job
            int thirdJob = secondJob + (job % 10 > 0 ? 1 : 0);
            if (thirdJob > secondJob && thirdJob < job)
                path.Add(thirdJob);

            // Add current job
            if (!path.Contains(job))
                path.Add(job);

            return path;
        }

        #endregion

        #region Utility

        private static SkillLevelData CreateLevelData(WzImageProperty node, int level)
        {
            var levelData = new SkillLevelData { Level = level };

            levelData.Damage = GetInt(node, "damage", 0, level);
            levelData.AttackCount = GetInt(node, "attackCount", 1, level);
            levelData.MobCount = GetInt(node, "mobCount", 1, level);

            levelData.MpCon = GetInt(node, "mpCon", 0, level);
            levelData.HpCon = GetInt(node, "hpCon", 0, level);
            levelData.ItemCon = GetInt(node, "itemCon", 0, level);
            levelData.ItemConNo = GetInt(node, "itemConNo", 0, level);

            levelData.Cooldown = GetInt(node, "cooltime", 0, level) * 1000;
            levelData.Time = GetInt(node, "time", 0, level);

            levelData.Range = GetInt(node, "range", 0, level);
            var rb = GetVector(node, "rb");
            var lt = GetVector(node, "lt");
            levelData.RangeR = rb?.X ?? levelData.Range;
            levelData.RangeL = Math.Abs(lt?.X ?? levelData.Range);
            levelData.RangeTop = lt?.Y ?? 0;
            levelData.RangeBottom = rb?.Y ?? 0;
            levelData.RangeY = Math.Abs(levelData.RangeTop) + Math.Abs(levelData.RangeBottom);

            levelData.PAD = GetInt(node, "pad", 0, level);
            levelData.MAD = GetInt(node, "mad", 0, level);
            levelData.PDD = GetInt(node, "pdd", 0, level);
            levelData.MDD = GetInt(node, "mdd", 0, level);
            levelData.ACC = GetInt(node, "acc", 0, level);
            levelData.EVA = GetInt(node, "eva", 0, level);
            levelData.Speed = GetInt(node, "speed", 0, level);
            levelData.Jump = GetInt(node, "jump", 0, level);

            levelData.HP = GetInt(node, "hp", 0, level);
            levelData.MP = GetInt(node, "mp", 0, level);

            levelData.Prop = GetInt(node, "prop", 0, level);
            levelData.X = GetInt(node, "x", 0, level);
            levelData.Y = GetInt(node, "y", 0, level);
            levelData.Z = GetInt(node, "z", 0, level);

            levelData.BulletCount = GetInt(node, "bulletCount", 1, level);
            levelData.BulletSpeed = GetInt(node, "bulletSpeed", 0, level);

            levelData.Mastery = GetInt(node, "mastery", 0, level);
            levelData.CriticalRate = GetInt(node, "cr", 0, level);

            levelData.RequiredLevel = GetInt(node, "reqLevel", 0, level);

            return levelData;
        }

        private static int GetInt(WzImageProperty node, string name, int defaultValue = 0, int formulaX = 1)
        {
            var child = node?[name];
            if (child is WzIntProperty intProp)
                return intProp.Value;
            if (child is WzShortProperty shortProp)
                return shortProp.Value;
            if (child is WzLongProperty longProp)
                return (int)longProp.Value;
            if (child is WzStringProperty stringProp && TryEvaluateFormula(stringProp.Value, formulaX, out int value))
                return value;
            return defaultValue;
        }

        private static bool TryEvaluateFormula(string expression, int xValue, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            try
            {
                var parser = new FormulaParser(expression, xValue);
                double result = parser.Parse();
                value = (int)Math.Round(result, MidpointRounding.AwayFromZero);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Point? GetVector(WzImageProperty node, string name)
        {
            var child = node?[name];
            if (child is WzVectorProperty vectorProp)
                return new Point(vectorProp.X.Value, vectorProp.Y.Value);
            return null;
        }

        private static string GetString(WzImageProperty node, string name, string defaultValue = "")
        {
            var child = node?[name];
            if (child is WzStringProperty stringProp)
                return stringProp.Value;
            return defaultValue;
        }

        public void ClearCache()
        {
            _skillCache.Clear();
            _jobCache.Clear();
        }

        private sealed class FormulaParser
        {
            private readonly string _expression;
            private readonly int _xValue;
            private int _index;

            public FormulaParser(string expression, int xValue)
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

                while (_index < _expression.Length &&
                       (char.IsDigit(_expression[_index]) || _expression[_index] == '.'))
                {
                    _index++;
                }

                if (start == _index)
                    throw new FormatException($"Expected number at index {_index} in '{_expression}'.");

                string token = _expression[start.._index];
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                    throw new FormatException($"Invalid number '{token}' in '{_expression}'.");

                return value;
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
                    identifier = null;
                    return false;
                }

                identifier = _expression[start.._index];
                return true;
            }

            private bool Match(char ch)
            {
                SkipWhitespace();
                if (_index >= _expression.Length || _expression[_index] != ch)
                    return false;

                _index++;
                return true;
            }

            private void Expect(char ch)
            {
                if (!Match(ch))
                    throw new FormatException($"Expected '{ch}' at index {_index} in '{_expression}'.");
            }

            private void SkipWhitespace()
            {
                while (_index < _expression.Length && char.IsWhiteSpace(_expression[_index]))
                {
                    _index++;
                }
            }
        }

        #endregion
    }
}
