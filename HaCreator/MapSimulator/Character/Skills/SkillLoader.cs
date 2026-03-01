using System;
using System.Collections.Generic;
using System.Linq;
using HaCreator.MapSimulator.Pools;
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
        private readonly WzFile _skillWz;
        private readonly GraphicsDevice _device;
        private readonly TexturePool _texturePool;

        // Caches
        private readonly Dictionary<int, SkillData> _skillCache = new();
        private readonly Dictionary<int, JobSkillBook> _jobCache = new();

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

        private SkillData LoadSkillInternal(int skillId)
        {
            // Skill.wz structure: Skill.wz/[jobId].img/skill/[skillId]
            // Job ID is skillId / 10000
            int jobId = skillId / 10000;
            string imgName = $"{jobId}.img";

            var jobImg = _skillWz?.WzDirectory?[imgName] as WzImage;
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
            // Basic properties from info node
            var infoNode = skillNode["info"];
            if (infoNode != null)
            {
                // Hidden skill
                skill.Invisible = GetInt(infoNode, "invisible") == 1;
                skill.MasterOnly = GetInt(infoNode, "masterOnly") == 1;
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
            bool hasBall = skillNode["ball"] != null;
            bool hasHit = skillNode["hit"] != null;
            bool hasAffected = skillNode["affected"] != null;
            bool hasSummon = skillNode["summon"] != null;

            var commonNode = skillNode["common"];
            if (commonNode != null)
            {
                bool hasDamage = GetInt(commonNode, "damage") > 0;
                bool hasTime = GetInt(commonNode, "time") > 0;
                bool hasMobCount = GetInt(commonNode, "mobCount") > 0;
                bool hasHp = GetInt(commonNode, "hp") > 0 || GetInt(commonNode, "hpR") > 0;
                bool hasPad = GetInt(commonNode, "pad") > 0;
                bool hasMad = GetInt(commonNode, "mad") > 0;

                // Determine type
                if (hasSummon)
                {
                    skill.Type = SkillType.Summon;
                    skill.IsSummon = true;
                }
                else if (hasHp && !hasDamage)
                {
                    skill.Type = SkillType.Heal;
                    skill.IsHeal = true;
                }
                else if (hasTime && (hasPad || hasMad || hasAffected))
                {
                    skill.Type = SkillType.Buff;
                    skill.IsBuff = true;
                }
                else if (hasDamage || hasMobCount)
                {
                    skill.Type = hasBall ? SkillType.Magic : SkillType.Attack;
                    skill.IsAttack = true;
                }
                else if (!hasDamage && !hasTime)
                {
                    skill.Type = SkillType.Passive;
                    skill.IsPassive = true;
                }
            }

            // Determine attack type
            if (hasBall)
            {
                skill.AttackType = SkillAttackType.Ranged;
            }
            else if (skill.Type == SkillType.Magic)
            {
                skill.AttackType = SkillAttackType.Magic;
            }
            else if (skill.Type == SkillType.Summon)
            {
                skill.AttackType = SkillAttackType.Summon;
            }
            else
            {
                skill.AttackType = SkillAttackType.Melee;
            }

            // Determine target type
            var levelNode = skillNode["level"]?["1"];
            if (levelNode != null)
            {
                int mobCount = GetInt(levelNode, "mobCount", 1);
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

            // Check action name
            var actionNode = skillNode["action"];
            if (actionNode != null)
            {
                skill.ActionName = GetString(actionNode, "0");
            }
        }

        private void ParseSkillLevels(SkillData skill, WzImageProperty skillNode)
        {
            var levelNode = skillNode["level"];
            if (levelNode == null)
                return;

            foreach (var child in levelNode.WzProperties)
            {
                if (!int.TryParse(child.Name, out int level))
                    continue;

                var levelData = new SkillLevelData { Level = level };

                // Damage
                levelData.Damage = GetInt(child, "damage");
                levelData.AttackCount = GetInt(child, "attackCount", 1);
                levelData.MobCount = GetInt(child, "mobCount", 1);

                // Costs
                levelData.MpCon = GetInt(child, "mpCon");
                levelData.HpCon = GetInt(child, "hpCon");
                levelData.ItemCon = GetInt(child, "itemCon");
                levelData.ItemConNo = GetInt(child, "itemConNo");

                // Timing
                levelData.Cooldown = GetInt(child, "cooltime") * 1000; // Convert to ms
                levelData.Time = GetInt(child, "time");

                // Range
                levelData.Range = GetInt(child, "range");
                var rb = GetVector(child, "rb");
                var lt = GetVector(child, "lt");
                levelData.RangeR = rb?.X ?? levelData.Range;
                levelData.RangeL = Math.Abs(lt?.X ?? levelData.Range);
                levelData.RangeY = Math.Abs(lt?.Y ?? 0) + Math.Abs(rb?.Y ?? 0);

                // Buff stats
                levelData.PAD = GetInt(child, "pad");
                levelData.MAD = GetInt(child, "mad");
                levelData.PDD = GetInt(child, "pdd");
                levelData.MDD = GetInt(child, "mdd");
                levelData.ACC = GetInt(child, "acc");
                levelData.EVA = GetInt(child, "eva");
                levelData.Speed = GetInt(child, "speed");
                levelData.Jump = GetInt(child, "jump");

                // Heal
                levelData.HP = GetInt(child, "hp");
                levelData.MP = GetInt(child, "mp");

                // Special
                levelData.Prop = GetInt(child, "prop");
                levelData.X = GetInt(child, "x");
                levelData.Y = GetInt(child, "y");
                levelData.Z = GetInt(child, "z");

                // Projectile
                levelData.BulletCount = GetInt(child, "bulletCount", 1);
                levelData.BulletSpeed = GetInt(child, "bulletSpeed");

                // Mastery
                levelData.Mastery = GetInt(child, "mastery");
                levelData.CriticalRate = GetInt(child, "cr");

                // Requirements
                levelData.RequiredLevel = GetInt(child, "reqLevel");

                skill.Levels[level] = levelData;
            }

            // Also try common node for simplified skill data
            var commonNode = skillNode["common"];
            if (commonNode != null && skill.Levels.Count == 0)
            {
                // Some skills use common node with formulas
                // For now, just create level 1 from common
                var levelData = new SkillLevelData { Level = 1 };
                levelData.Damage = GetInt(commonNode, "damage");
                levelData.MobCount = GetInt(commonNode, "mobCount", 1);
                levelData.AttackCount = GetInt(commonNode, "attackCount", 1);
                levelData.MpCon = GetInt(commonNode, "mpCon");
                levelData.Time = GetInt(commonNode, "time");
                skill.Levels[1] = levelData;
            }
        }

        #endregion

        #region Animation Loading

        private void ParseSkillAnimations(SkillData skill, WzImageProperty skillNode)
        {
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

            // Load projectile/ball
            var ballNode = skillNode["ball"];
            if (ballNode != null)
            {
                skill.Projectile = LoadProjectile(skill.SkillId, ballNode, skillNode);
            }
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
                var texture = bitmap.ToTexture2D(_device);
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
                    var texture = bitmap.ToTexture2D(_device);
                    skill.Icon = new DXObject(0, 0, texture);
                }
            }

            // Disabled icon
            var iconDisabledNode = skillNode["iconDisabled"];
            if (iconDisabledNode is WzCanvasProperty iconDisabledCanvas)
            {
                var bitmap = iconDisabledCanvas.GetLinkedWzCanvasBitmap();
                if (bitmap != null)
                {
                    var texture = bitmap.ToTexture2D(_device);
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
                    var texture = bitmap.ToTexture2D(_device);
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
            var jobImg = _skillWz?.WzDirectory?[imgName] as WzImage;
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

        private static int GetInt(WzImageProperty node, string name, int defaultValue = 0)
        {
            var child = node?[name];
            if (child is WzIntProperty intProp)
                return intProp.Value;
            if (child is WzShortProperty shortProp)
                return shortProp.Value;
            if (child is WzLongProperty longProp)
                return (int)longProp.Value;
            return defaultValue;
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

        #endregion
    }
}
