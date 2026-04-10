using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using HaCreator.MapSimulator.Pools;
using HaCreator.MapSimulator.Managers;
using HaCreator.MapSimulator.Interaction;
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
        private readonly record struct SummonActionCacheKey(int SkillId, int SkillLevel, string ActionKey);

        private static readonly string[] PreferredSummonAnimationBranches =
        {
            "stand",
            "fly",
            "move",
            "walk",
            "repeat"
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
            "attack0",
            "attackTriangle"
        };

        private static readonly string[] SupplementalSummonAnimationBranches =
        {
            "attack2",
            "attack3",
            "attack4",
            "attack5",
            "attack6",
            "attack7",
            "attack8",
            "attackF",
            "heal",
            "support",
            "subsummon",
            "attackTriangle",
            "repeat",
            "repeat0",
            "effect",
            "effect0",
            "skill1",
            "skill2",
            "skill3",
            "skill4",
            "skill5",
            "skill6"
        };

        private static readonly HashSet<string> NonActionSummonBranchNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "info",
            "ball",
            "mob"
        };

        private const int ClientSummonedFrameDelayFallbackMs = 120;
        private const int ClientSummonReversePlaybackStringPoolId = 0x049F;
        private const string ClientSummonReversePlaybackFallbackName = "zigzag";

        private static readonly string[] PersistentAvatarEffectBranches =
        {
            "special",
            "special0",
            "finish",
            "finish0",
            "back",
            "back_finish"
        };

        private static readonly string[] MoreWildPersistentAvatarEffectBranches =
        {
            "back",
            "back_finish"
        };

        private static readonly HashSet<int> ShadowPartnerSkillIds = new()
        {
            4111002,
            4211008,
            14111000
        };

        private static readonly string[] PreferredShadowPartnerOffsetActions =
        {
            "stand1",
            "alert",
            "stand2",
            "walk1"
        };

        private static readonly HashSet<string> ShadowPartnerReplayTailActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            // `LoadShadowPartnerAction` gates mirrored interior-frame replay through the
            // client action-table flag at `C68E70h[action * 0x18 + 4]`; the targeted
            // v95 scan confirms raw actions 2/3/4 (`stand1`, `stand2`, `alert`) only.
            "stand1",
            "stand2",
            "alert"
        };

        private readonly WzFile _skillWz;
        private readonly GraphicsDevice _device;
        private readonly TexturePool _texturePool;

        // Caches
        private readonly Dictionary<int, SkillData> _skillCache = new();
        private readonly Dictionary<int, JobSkillBook> _jobCache = new();
        private readonly HashSet<int> _skillsWithoutCastSound = new();
        private readonly HashSet<int> _skillsWithoutRepeatSound = new();
        private readonly Dictionary<string, MeleeAfterImageCatalog> _characterAfterImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MeleeAfterImageCatalog> _characterChargeAfterImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingCharacterAfterImageKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingCharacterChargeAfterImageKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, SkillAnimation> _itemBulletAnimationCache = new();
        private readonly HashSet<int> _itemsWithoutBulletAnimation = new();
        private readonly Dictionary<SummonActionCacheKey, SkillAnimation> _summonActionCache = new();
        private WzImage _skillSoundImage;
        private WzImage _skillStringImage;

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

        public SkillAnimation LoadItemBulletAnimation(int itemId)
        {
            if (itemId <= 0)
            {
                return null;
            }

            if (_itemBulletAnimationCache.TryGetValue(itemId, out SkillAnimation cached))
            {
                return cached;
            }

            if (_itemsWithoutBulletAnimation.Contains(itemId))
            {
                return null;
            }

            SkillAnimation animation = LoadItemBulletAnimationInternal(itemId);
            if (animation?.Frames?.Count > 0)
            {
                _itemBulletAnimationCache[itemId] = animation;
                return animation;
            }

            _itemsWithoutBulletAnimation.Add(itemId);
            return null;
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

        public SkillAnimation ResolveSummonActionAnimation(
            SkillData skill,
            int skillLevel,
            string actionKey,
            SkillAnimation fallback = null)
        {
            if (skill == null || string.IsNullOrWhiteSpace(actionKey))
            {
                return fallback;
            }

            string normalizedKey = actionKey.Trim();
            if (TryGetCachedSummonActionAnimation(skill.SkillId, skillLevel, normalizedKey, out SkillAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            if (skill.SummonActionAnimations != null
                && skill.SummonActionAnimations.TryGetValue(normalizedKey, out cachedAnimation)
                && cachedAnimation?.Frames.Count > 0)
            {
                CacheSummonActionAnimation(
                    skill,
                    normalizedKey,
                    cachedAnimation,
                    skillLevel > 0 ? new[] { skillLevel } : null);
                return cachedAnimation;
            }

            return fallback;
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

            MarkSwallowFamilySkills(result);
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

            LoadSkillStrings(skill);

            // Parse basic info
            ParseSkillInfo(skill, skillNode);

            // Parse level data
            ParseSkillLevels(skill, skillNode);

            // Parse animations
            ParseSkillAnimations(skill, skillNode);
            FinalizeMovementClassification(skill);

            // Load icon
            LoadSkillIcon(skill, skillNode);

            return skill;
        }

        private void LoadSkillStrings(SkillData skill)
        {
            if (skill == null)
                return;

            _skillStringImage ??= Program.FindImage("String", "Skill.img");

            WzImageProperty stringNode = _skillStringImage?[skill.SkillId.ToString()];
            if (stringNode == null)
                return;

            skill.Name = GetString(stringNode, "name");
            skill.Description = GetString(stringNode, "desc");
            skill.DescriptionHints = BuildDescriptionHints(stringNode, skill.Description);
        }

        private static string BuildDescriptionHints(WzImageProperty stringNode, string description)
        {
            if (stringNode == null)
            {
                return string.Empty;
            }

            string[] authoredHintKeys =
            {
                "pdesc",
                "h",
                "h1",
                "ph"
            };

            List<string> segments = new();
            foreach (string hintKey in authoredHintKeys)
            {
                string value = GetString(stringNode, hintKey);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(description)
                    && string.Equals(
                        NormalizeAuthoredDescriptionText(value),
                        NormalizeAuthoredDescriptionText(description),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (segments.Any(existing => string.Equals(
                        NormalizeAuthoredDescriptionText(existing),
                        NormalizeAuthoredDescriptionText(value),
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                segments.Add(value.Trim());
            }

            return segments.Count == 0
                ? string.Empty
                : string.Join(" ", segments);
        }

        private static string NormalizeAuthoredDescriptionText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                value.Replace("\\r", " ", StringComparison.Ordinal)
                    .Replace("\\n", " ", StringComparison.Ordinal)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void ParseSkillInfo(SkillData skill, WzImageProperty skillNode)
        {
            skill.ActionNames = GetActionNames(skillNode);
            skill.ActionName = skill.ActionNames.FirstOrDefault() ?? string.Empty;
            skill.IsPassiveSkillData = GetInt(skillNode, "psd") == 1;
            ParseFinalAttackTriggers(skill, skillNode);

            // Basic properties from info node
            var infoNode = skillNode["info"];
            if (infoNode != null)
            {
                // Hidden skill
                skill.Invisible = GetInt(infoNode, "invisible") == 1;
                skill.MasterOnly = GetInt(infoNode, "masterOnly") == 1;
                skill.IsRapidAttack = GetInt(infoNode, "rapidAttack") == 1;
                skill.IsMesoExplosion = GetInt(infoNode, "mesoExplosion") == 1;
                skill.CasterMove = GetInt(infoNode, "casterMove") == 1;
                skill.AreaAttack = GetInt(infoNode, "areaAttack") == 1;
                skill.RectBasedOnTarget = GetInt(infoNode, "rectBasedOnTarget") == 1;
                skill.MultiTargeting = GetInt(infoNode, "multiTargeting") == 1;
                skill.ChainAttack = GetInt(infoNode, "chainAttack") == 1;
                skill.ChainAttackPenalty = GetInt(infoNode, "chainattackPenalty") == 1;
                skill.LandingEffectName = GetString(infoNode, "landingEffect");
                skill.MinionAbility = GetString(infoNode, "minionAbility");
                skill.MinionAttack = GetString(infoNode, "minionAttack");
                skill.ClientInfoType = GetInt(infoNode, "type");
                skill.ClientDelayMs = GetInt(infoNode, "delay");
                skill.AvailableInJumpingState = GetInt(infoNode, "avaliableInJumpingState") == 1;
                string condition = GetString(infoNode, "condition");
                skill.SummonCondition = condition;
                skill.TriggerCondition = condition;
                skill.IsSwallowSkill = GetInt(infoNode, "swallow") == 1;
                skill.DummySkillParents = ParseDummySkillParents(GetString(infoNode, "dummyOf"));
                skill.SelfDestructMinion = GetInt(infoNode, "selfDestructMinion") == 1;
                skill.ZoneType = GetString(infoNode, "zoneType");
                skill.IsMassSpell = GetInt(infoNode, "massSpell") == 1;
                skill.DebuffMessageToken = GetString(infoNode, "mes");
                skill.AffectedSkillIds = ParseLinkedSkillIds(GetString(infoNode, "affectedSkill"));
                skill.AffectedSkillId = skill.AffectedSkillIds.FirstOrDefault();
                skill.PassiveLinkedSkillIds = ParseLinkedSkillIds(skillNode["psdSkill"]);
                skill.AffectedSkillEffect = GetString(infoNode, "affectedSkillEffect");
                skill.DotType = GetString(infoNode, "dotType");
                skill.IsMagicDamageSkill = GetInt(infoNode, "magicDamage") == 1;
                skill.RequireHighestJump = GetInt(infoNode, "requireHighestJump") == 1;
                skill.RequiredSkillIds = ResolveRequiredSkillIds(skillNode, infoNode);
                skill.FixedState = GetInt(infoNode, "fixedState") == 1;
                skill.CanNotMoveInState = GetInt(infoNode, "canNotMoveInState") == 1;
                skill.OnlyNormalAttackInState = GetInt(infoNode, "onlyNormalAttack") == 1;
                skill.SpecialNormalAttackInState = GetInt(infoNode, "specialNormalAttack") == 1;
                skill.RedirectsDamageToMp = GetInt(infoNode, "switchDamtoMP") == 1;
                skill.HasMagicStealMetadata = GetInt(infoNode, "magicSteal") == 1;
                skill.HasInvincibleMetadata = GetInt(infoNode, "invincible") == 1;
                skill.HasDispelMetadata = GetInt(infoNode, "dispell") == 1;
                skill.HasBlessingArmorMetadata = GetInt(infoNode, "blessingArmor") == 1;
                skill.UsesEnergyChargeRuntime = GetInt(infoNode, "energyCharge") == 1;
                skill.HasChargingSkillMetadata = GetInt(infoNode, "chargingSkill") == 1;
                skill.FullChargeEffectName = GetString(infoNode, "fullChargeEffect");
                skill.ReflectsIncomingDamage = GetInt(infoNode, "PADReflect") == 1
                                               || GetInt(infoNode, "MADReflect") == 1;
            }

            // Check common nodes
            var commonNode = skillNode["common"];
            var pvpCommonNode = skillNode["PVPcommon"];
            skill.HasMorphMetadata = HasProperty(commonNode, "morph")
                || HasProperty(pvpCommonNode, "morph")
                || HasProperty(infoNode, "morph");
            if (commonNode != null)
            {
                skill.MaxLevel = GetInt(commonNode, "maxLevel", 1);
                skill.EnergyChargeThresholdFormula = GetString(commonNode, "x");
                skill.SummonSubTimeFormula = GetString(commonNode, "subTime");
                skill.SummonTimeFormula = GetString(commonNode, "time");
                skill.SummonSelfDestructionFormula = GetString(commonNode, "selfDestruction");
            }

            skill.MorphId = ResolveMorphTemplateId(skill.SkillId, commonNode, pvpCommonNode, infoNode);

            if (!skill.Invisible)
            {
                skill.Invisible = GetInt(skillNode, "invisible") == 1;
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

        private static void FinalizeMovementClassification(SkillData skill)
        {
            if (skill == null || skill.IsMovement || skill.IsKeydownSkill)
            {
                return;
            }

            if (EnumerateMovementActionCandidates(skill).Any(ActionImpliesMovement))
            {
                skill.IsMovement = true;
            }
        }

        private static IEnumerable<string> EnumerateMovementActionCandidates(SkillData skill)
        {
            if (skill == null)
            {
                yield break;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(skill.PrepareActionName) && seen.Add(skill.PrepareActionName))
            {
                yield return skill.PrepareActionName;
            }

            if (skill.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill.ActionName) && seen.Add(skill.ActionName))
            {
                yield return skill.ActionName;
            }
        }

        private static bool ActionImpliesMovement(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            string[] movementTokens = { "teleport", "rush", "dash", "flash", "jump", "step", "fly" };
            foreach (string token in movementTokens)
            {
                if (actionName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void DetermineSkillType(SkillData skill, WzImageProperty skillNode)
        {
            if (skill.UsesAffectedSkillBodyAttack)
            {
                skill.Type = SkillType.Passive;
                skill.IsPassive = true;
                skill.IsAttack = true;
                skill.AttackType = skill.IsMagicDamageSkill ? SkillAttackType.Magic : SkillAttackType.Melee;
                return;
            }

            if (skill.UsesAffectedSkillPassiveData)
            {
                skill.Type = SkillType.Passive;
                skill.IsPassive = true;
                return;
            }

            // Check for various type indicators
            var infoNode = skillNode["info"];
            SkillAttackType? clientAttackType = ResolveClientAttackType(skill.SkillId, infoNode);
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
            skill.HideAvatarEffectOnRotateAction = ShouldHidePersistentAvatarEffectOnRotateAction(
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
                    skill.Type = ResolveAttackSkillType(clientAttackType, hasBall, skill.IsMovement);
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
                    : clientAttackType == SkillAttackType.Magic
                        ? SkillType.Magic
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
            else if (clientAttackType.HasValue)
            {
                skill.AttackType = clientAttackType.Value;
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

        private static SkillType ResolveAttackSkillType(SkillAttackType? clientAttackType, bool hasBall, bool isMovement)
        {
            if (isMovement)
            {
                return SkillType.Movement;
            }

            if (clientAttackType == SkillAttackType.Magic || hasBall)
            {
                return SkillType.Magic;
            }

            return SkillType.Attack;
        }

        private static SkillAttackType? ResolveClientAttackType(int skillId, WzImageProperty infoNode)
        {
            if (ClientShootAttackFamilyResolver.UsesClientShootAttackLane(skillId))
            {
                return SkillAttackType.Ranged;
            }

            int infoType = GetInt(infoNode, "type");
            return infoType switch
            {
                1 => SkillAttackType.Melee,
                2 => SkillAttackType.Ranged,
                // Aran combo/command attack families publish `info/type=52` even without `ball`,
                // but the client keeps them on the shoot-attack path instead of falling back to melee.
                52 => SkillAttackType.Ranged,
                10 => SkillAttackType.Magic,
                // WZ `massSpell=1` type-32 families are spell-owned area resolution, not melee.
                32 when GetInt(infoNode, "massSpell") == 1 => SkillAttackType.Magic,
                _ => null
            };
        }

        private void ParseSkillLevels(SkillData skill, WzImageProperty skillNode)
        {
            int eventTamingMobId = GetInt(skillNode, "eventTamingMob");
            if (eventTamingMobId > 0)
            {
                skill.UsesTamingMobMount = true;
            }

            var levelNode = skillNode["level"];
            if (levelNode != null)
            {
                foreach (var child in levelNode.WzProperties)
                {
                    if (!int.TryParse(child.Name, out int level))
                        continue;

                    var levelData = CreateLevelData(skill, child, level);
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
                    var levelData = CreateLevelData(skill, commonNode, level);
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

            skill.PrepareSecondaryEffect = LoadOptionalSkillAnimation(skillNode, "prepare0", loop: false);

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

            skill.KeydownSecondaryEffect =
                LoadOptionalSkillAnimation(skillNode, "keydown0", loop: true)
                ?? LoadOptionalSkillAnimation(skillNode, "keyDown0", loop: true);

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

            skill.KeydownEndSecondaryEffect =
                LoadOptionalSkillAnimation(skillNode, "keydownend0", loop: false)
                ?? LoadOptionalSkillAnimation(skillNode, "keyDownEnd0", loop: false);

            var repeatNode = skillNode["repeat"];
            if (repeatNode != null)
            {
                skill.RepeatDurationMs = ResolveAnimationDuration(repeatNode);

                var repeatAnimation = LoadSkillAnimation(repeatNode, "repeat");
                if (repeatAnimation.Frames.Count > 0)
                {
                    repeatAnimation.Loop = true;
                    skill.RepeatEffect = repeatAnimation;
                }
            }

            skill.RepeatSecondaryEffect = LoadOptionalSkillAnimation(skillNode, "repeat0", loop: true);

            skill.IsKeydownSkill = keydownNode != null || keydownEndNode != null || skill.IsRapidAttack;

            // Load effect animation
            var effectNode = skillNode["effect"];
            if (effectNode != null)
            {
                skill.Effect = LoadSkillAnimation(effectNode, "effect");
            }

            skill.EffectSecondary = LoadOptionalSkillAnimation(skillNode, "effect0", loop: false);
            skill.StopEffect = LoadOptionalSkillAnimation(skillNode, "stopEffect", loop: false);
            skill.StopSecondaryEffect = LoadOptionalSkillAnimation(skillNode, "stopEffect0", loop: false);

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

            skill.AffectedSecondaryEffect = LoadOptionalSkillAnimation(skillNode, "affected0", loop: false);

            LoadPersistentAvatarEffects(skill, skillNode);
            LoadShadowPartnerActionAnimations(skill, skillNode);
            LoadAfterImages(skill, skillNode);

            var summonNode = ResolveSummonSourceProperty(skill, skillNode);
            if (summonNode != null)
            {
                LoadSummonAnimations(skill, summonNode);
            }

            var tileNode = skillNode["tile"];
            if (tileNode != null)
            {
                skill.ZoneEffect = LoadZoneEffect(tileNode, skillNode);
                skill.ZoneAnimation = skill.ZoneEffect?.Animation;
            }

            // Load projectile/ball
            var ballNode = skillNode["ball"];
            if (ballNode != null)
            {
                skill.Projectile = LoadProjectile(skill.SkillId, ballNode, skillNode);
            }
        }

        public bool TryResolveMeleeAfterImageAction(
            SkillData skill,
            WeaponPart weapon,
            string actionName,
            int characterLevel,
            int masteryPercent,
            int chargeElement,
            out MeleeAfterImageAction afterImageAction)
        {
            return TryResolveMeleeAfterImageAction(
                skill,
                weapon,
                string.IsNullOrWhiteSpace(actionName) ? null : new[] { actionName },
                characterLevel,
                masteryPercent,
                chargeElement,
                out afterImageAction,
                out _);
        }

        public bool TryResolveMeleeAfterImageAction(
            SkillData skill,
            WeaponPart weapon,
            string actionName,
            int characterLevel,
            int masteryPercent,
            int chargeElement,
            out MeleeAfterImageAction afterImageAction,
            out string matchedActionName)
        {
            return TryResolveMeleeAfterImageAction(
                skill,
                weapon,
                string.IsNullOrWhiteSpace(actionName) ? null : new[] { actionName },
                characterLevel,
                masteryPercent,
                chargeElement,
                out afterImageAction,
                out matchedActionName);
        }

        public bool TryResolveMeleeAfterImageAction(
            SkillData skill,
            WeaponPart weapon,
            IEnumerable<string> actionNames,
            int characterLevel,
            int masteryPercent,
            int chargeElement,
            out MeleeAfterImageAction afterImageAction)
        {
            return TryResolveMeleeAfterImageAction(
                skill,
                weapon,
                actionNames,
                characterLevel,
                masteryPercent,
                chargeElement,
                out afterImageAction,
                out _);
        }

        public bool TryResolveMeleeAfterImageAction(
            SkillData skill,
            WeaponPart weapon,
            IEnumerable<string> actionNames,
            int characterLevel,
            int masteryPercent,
            int chargeElement,
            out MeleeAfterImageAction afterImageAction,
            out string matchedActionName)
        {
            afterImageAction = null;
            matchedActionName = null;
            if (actionNames == null)
            {
                return false;
            }

            foreach (string actionName in EnumerateDistinctActionNames(actionNames))
            {
                if (chargeElement > 0)
                {
                    foreach (string weaponTypeKey in ResolveAfterImageWeaponTypeKeys(weapon))
                    {
                        MeleeAfterImageCatalog chargeCatalog = GetOrLoadCharacterChargeAfterImageCatalog(weaponTypeKey, chargeElement);
                        if (TryResolveMeleeAfterImageCatalogAction(
                                chargeCatalog,
                                skill?.SkillId ?? 0,
                                actionName,
                                out afterImageAction,
                                out matchedActionName))
                        {
                            return true;
                        }
                    }

                    continue;
                }

                foreach (string weaponTypeKey in ResolveAfterImageWeaponTypeKeys(weapon))
                {
                    MeleeAfterImageCatalog skillCatalog = skill?.GetAfterImageCatalogForCharacterLevel(weaponTypeKey, characterLevel);
                    if (TryResolveMeleeAfterImageCatalogAction(
                            skillCatalog,
                            skill?.SkillId ?? 0,
                            actionName,
                            out afterImageAction,
                            out matchedActionName))
                    {
                        return true;
                    }

                    MeleeAfterImageCatalog weaponCatalog = GetOrLoadCharacterAfterImageCatalog(
                        weaponTypeKey,
                        GetWeaponAfterImageMasteryIndex(masteryPercent));
                    if (TryResolveMeleeAfterImageCatalogAction(
                            weaponCatalog,
                            skill?.SkillId ?? 0,
                            actionName,
                            out afterImageAction,
                            out matchedActionName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateDistinctActionNames(IEnumerable<string> actionNames)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string actionName in actionNames)
            {
                if (!string.IsNullOrWhiteSpace(actionName) && yielded.Add(actionName.Trim()))
                {
                    yield return actionName.Trim();
                }
            }
        }

        public MeleeAfterImageAction ApplyClientMeleeRangeOverride(
            MeleeAfterImageAction action,
            int skillId,
            int? rawActionCode,
            bool facingRight)
        {
            return ClientMeleeAfterimageRangeResolver.ApplyRangeOverride(action, skillId, rawActionCode, facingRight);
        }

        public bool TryResolveUniqueMeleeAfterImageActionName(
            SkillData skill,
            WeaponPart weapon,
            IEnumerable<string> actionNames,
            int characterLevel,
            int masteryPercent,
            int chargeElement,
            out string actionName)
        {
            actionName = null;

            var matchedActionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MeleeAfterImageCatalog catalog in EnumerateRelevantMeleeAfterImageCatalogs(
                         skill,
                         weapon,
                         characterLevel,
                         masteryPercent,
                         chargeElement))
            {
                if (catalog?.Actions == null || catalog.Actions.Count == 0)
                {
                    continue;
                }

                foreach (string requestedActionName in EnumerateDistinctActionNames(actionNames ?? Array.Empty<string>()))
                {
                    foreach (string candidate in EnumerateMeleeAfterImageActionCandidates(skill?.SkillId ?? 0, requestedActionName))
                    {
                        if (catalog.Actions.ContainsKey(candidate))
                        {
                            matchedActionNames.Add(candidate);
                        }
                    }
                }
            }

            if (matchedActionNames.Count == 1)
            {
                foreach (string matchedActionName in matchedActionNames)
                {
                    actionName = matchedActionName;
                    return true;
                }
            }

            if (matchedActionNames.Count > 1)
            {
                return false;
            }

            var catalogActionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MeleeAfterImageCatalog catalog in EnumerateRelevantMeleeAfterImageCatalogs(
                         skill,
                         weapon,
                         characterLevel,
                         masteryPercent,
                         chargeElement))
            {
                if (catalog?.Actions == null || catalog.Actions.Count == 0)
                {
                    continue;
                }

                foreach (string catalogActionName in catalog.Actions.Keys)
                {
                    catalogActionNames.Add(catalogActionName);
                }
            }

            if (catalogActionNames.Count != 1)
            {
                return false;
            }

            foreach (string catalogActionName in catalogActionNames)
            {
                actionName = catalogActionName;
                return true;
            }

            return false;
        }

        internal bool TryResolveRenderableMeleeAfterImageActionName(
            SkillData skill,
            WeaponPart weapon,
            IEnumerable<string> actionNames,
            int characterLevel,
            int masteryPercent,
            int chargeElement,
            Func<string, bool> canRenderAction,
            out string actionName)
        {
            actionName = null;
            if (canRenderAction == null)
            {
                return false;
            }

            HashSet<string> matchedActionNames = CollectRelevantMeleeAfterImageActionNames(
                skill,
                weapon,
                actionNames,
                characterLevel,
                masteryPercent,
                chargeElement,
                matchedOnly: true);
            if (TrySelectSingleRenderableActionName(matchedActionNames, canRenderAction, out actionName))
            {
                return true;
            }

            HashSet<string> catalogActionNames = matchedActionNames.Count > 0
                ? matchedActionNames
                : CollectRelevantMeleeAfterImageActionNames(
                    skill,
                    weapon,
                    actionNames,
                    characterLevel,
                    masteryPercent,
                    chargeElement,
                    matchedOnly: false);
            return TrySelectSingleRenderableActionName(catalogActionNames, canRenderAction, out actionName);
        }

        private HashSet<string> CollectRelevantMeleeAfterImageActionNames(
            SkillData skill,
            WeaponPart weapon,
            IEnumerable<string> actionNames,
            int characterLevel,
            int masteryPercent,
            int chargeElement,
            bool matchedOnly)
        {
            var collectedActionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] distinctActionNames = EnumerateDistinctActionNames(actionNames ?? Array.Empty<string>()).ToArray();

            foreach (MeleeAfterImageCatalog catalog in EnumerateRelevantMeleeAfterImageCatalogs(
                         skill,
                         weapon,
                         characterLevel,
                         masteryPercent,
                         chargeElement))
            {
                if (catalog?.Actions == null || catalog.Actions.Count == 0)
                {
                    continue;
                }

                if (!matchedOnly)
                {
                    foreach (string catalogActionName in catalog.Actions.Keys)
                    {
                        collectedActionNames.Add(catalogActionName);
                    }

                    continue;
                }

                foreach (string requestedActionName in distinctActionNames)
                {
                    foreach (string candidate in EnumerateMeleeAfterImageActionCandidates(skill?.SkillId ?? 0, requestedActionName))
                    {
                        if (catalog.Actions.ContainsKey(candidate))
                        {
                            collectedActionNames.Add(candidate);
                        }
                    }
                }
            }

            return collectedActionNames;
        }

        internal static bool TrySelectSingleRenderableActionName(
            IEnumerable<string> actionNames,
            Func<string, bool> canRenderAction,
            out string actionName)
        {
            actionName = null;
            if (actionNames == null)
            {
                return false;
            }

            string renderableActionName = null;
            int renderableCount = 0;
            foreach (string candidate in actionNames)
            {
                if (string.IsNullOrWhiteSpace(candidate) || !canRenderAction(candidate))
                {
                    continue;
                }

                renderableActionName = candidate;
                renderableCount++;
                if (renderableCount > 1)
                {
                    return false;
                }
            }

            if (renderableCount != 1)
            {
                return false;
            }

            actionName = renderableActionName;
            return true;
        }

        private static bool TryResolveMeleeAfterImageCatalogAction(
            MeleeAfterImageCatalog catalog,
            int skillId,
            string actionName,
            out MeleeAfterImageAction action,
            out string matchedActionName)
        {
            action = null;
            matchedActionName = null;
            if (catalog?.Actions == null || string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            foreach (string candidate in EnumerateMeleeAfterImageActionCandidates(skillId, actionName))
            {
                if (catalog.TryGetAction(candidate, out action))
                {
                    matchedActionName = candidate;
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<MeleeAfterImageCatalog> EnumerateRelevantMeleeAfterImageCatalogs(
            SkillData skill,
            WeaponPart weapon,
            int characterLevel,
            int masteryPercent,
            int chargeElement)
        {
            if (chargeElement > 0)
            {
                foreach (string weaponTypeKey in ResolveAfterImageWeaponTypeKeys(weapon))
                {
                    MeleeAfterImageCatalog chargeCatalog = GetOrLoadCharacterChargeAfterImageCatalog(weaponTypeKey, chargeElement);
                    if (chargeCatalog?.Actions?.Count > 0)
                    {
                        yield return chargeCatalog;
                    }
                }

                yield break;
            }

            foreach (string weaponTypeKey in ResolveAfterImageWeaponTypeKeys(weapon))
            {
                MeleeAfterImageCatalog skillCatalog = skill?.GetAfterImageCatalogForCharacterLevel(weaponTypeKey, characterLevel);
                if (skillCatalog?.Actions?.Count > 0)
                {
                    yield return skillCatalog;
                }

                MeleeAfterImageCatalog weaponCatalog = GetOrLoadCharacterAfterImageCatalog(
                    weaponTypeKey,
                    GetWeaponAfterImageMasteryIndex(masteryPercent));
                if (weaponCatalog?.Actions?.Count > 0)
                {
                    yield return weaponCatalog;
                }
            }
        }

        private static IEnumerable<string> EnumerateMeleeAfterImageActionCandidates(int skillId, string actionName)
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in CharacterPart.GetActionLookupStrings(actionName))
            {
                if (yielded.Add(candidate))
                {
                    yield return candidate;
                }

                foreach (string alias in EnumerateMeleeAfterImageFamilyAliases(candidate))
                {
                    if (yielded.Add(alias))
                    {
                        yield return alias;
                    }
                }

                foreach (string alias in EnumerateClientMeleeAfterImageSkillAliases(skillId, candidate))
                {
                    if (yielded.Add(alias))
                    {
                        yield return alias;
                    }
                }
            }
        }

        private static IEnumerable<string> EnumerateClientMeleeAfterImageSkillAliases(int skillId, string actionName)
        {
            if (skillId == 1221009
                && string.Equals(actionName, "blast", StringComparison.OrdinalIgnoreCase))
            {
                // WZ: Skill/122.img/1221009/action/0 = blast, but afterimage/* publishes stabO2.
                // Client: CUserRemote::OnMeleeAttack forces raw action 17 before GetMeleeAttackRange.
                yield return "stabO2";
            }
        }

        private static IEnumerable<string> EnumerateMeleeAfterImageFamilyAliases(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                yield break;
            }

            if (string.Equals(actionName, "attack1", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[]
                {
                    "stabO1", "stabO2", "stabOF",
                    "stabT1", "stabT2", "stabTF",
                    "proneStab"
                })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (string.Equals(actionName, "attack2", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[]
                {
                    "swingO1", "swingO2", "swingO3", "swingOF",
                    "swingT1", "swingT2", "swingT3", "swingTF",
                    "swingP1", "swingP2", "swingPF"
                })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("stabO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "stabD1", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "stabO1", "stabO2", "stabOF", "stabD1" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("stabT", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "stabT1", "stabT2", "stabTF" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("swingO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "swingD1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "swingD2", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "swingO1", "swingO2", "swingO3", "swingOF", "swingD1", "swingD2" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("swingT", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "swingT1", "swingT2", "swingT3", "swingTF", "swingT2PoleArm" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("swingP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "doubleSwing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "tripleSwing", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[]
                {
                    "swingP1", "swingP2", "swingPF",
                    "swingP1PoleArm", "swingP2PoleArm",
                    "doubleSwing", "tripleSwing"
                })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "shotC1", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "shoot1", "shoot2", "shootF", "shotC1" })
                {
                    yield return candidate;
                }

                yield break;
            }

            if (actionName.StartsWith("swingC", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string candidate in new[] { "swingC1", "swingC2" })
                {
                    yield return candidate;
                }
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

        private void LoadAfterImages(SkillData skill, WzImageProperty skillNode)
        {
            if (skill == null || skillNode == null)
            {
                return;
            }

            PopulateAfterImageCatalogMap(skill.AfterImageCatalogsByWeaponType, skillNode["afterimage"]);

            WzImageProperty charLevelNode = skillNode["CharLevel"];
            if (charLevelNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in charLevelNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int requiredLevel))
                {
                    continue;
                }

                var catalogMap = new Dictionary<string, MeleeAfterImageCatalog>(StringComparer.OrdinalIgnoreCase);
                PopulateAfterImageCatalogMap(catalogMap, child["afterimage"]);
                if (catalogMap.Count > 0)
                {
                    skill.CharacterLevelAfterImageCatalogsByWeaponType[requiredLevel] = catalogMap;
                }
            }
        }

        private void PopulateAfterImageCatalogMap(
            Dictionary<string, MeleeAfterImageCatalog> destination,
            WzImageProperty afterImageNode)
        {
            if (destination == null || afterImageNode == null)
            {
                return;
            }

            foreach (WzImageProperty weaponNode in afterImageNode.WzProperties)
            {
                if (weaponNode == null || string.IsNullOrWhiteSpace(weaponNode.Name))
                {
                    continue;
                }

                MeleeAfterImageCatalog catalog = LoadAfterImageCatalog(weaponNode);
                if (catalog.Actions.Count > 0)
                {
                    destination[weaponNode.Name] = catalog;
                }
            }
        }

        private MeleeAfterImageCatalog GetOrLoadCharacterAfterImageCatalog(string weaponTypeKey, int masteryIndex)
        {
            string cacheKey = $"{weaponTypeKey}/{masteryIndex}";
            if (_characterAfterImageCache.TryGetValue(cacheKey, out MeleeAfterImageCatalog cachedCatalog))
            {
                return cachedCatalog;
            }

            if (_missingCharacterAfterImageKeys.Contains(cacheKey))
            {
                return null;
            }

            WzImage image = Program.FindImage("Character", $"Afterimage/{weaponTypeKey}");
            if (image == null)
            {
                _missingCharacterAfterImageKeys.Add(cacheKey);
                return null;
            }

            image.ParseImage();
            WzImageProperty masteryNode = image[masteryIndex.ToString(CultureInfo.InvariantCulture)];
            if (masteryNode == null)
            {
                _missingCharacterAfterImageKeys.Add(cacheKey);
                return null;
            }

            MeleeAfterImageCatalog catalog = LoadAfterImageCatalog(masteryNode);
            if (catalog.Actions.Count == 0)
            {
                _missingCharacterAfterImageKeys.Add(cacheKey);
                return null;
            }

            _characterAfterImageCache[cacheKey] = catalog;
            return catalog;
        }

        private MeleeAfterImageCatalog GetOrLoadCharacterChargeAfterImageCatalog(string weaponTypeKey, int chargeElement)
        {
            string cacheKey = $"{weaponTypeKey}/charge/{chargeElement}";
            if (_characterChargeAfterImageCache.TryGetValue(cacheKey, out MeleeAfterImageCatalog cachedCatalog))
            {
                return cachedCatalog;
            }

            if (_missingCharacterChargeAfterImageKeys.Contains(cacheKey))
            {
                return null;
            }

            WzImage image = Program.FindImage("Character", $"Afterimage/{weaponTypeKey}");
            if (image == null)
            {
                _missingCharacterChargeAfterImageKeys.Add(cacheKey);
                return null;
            }

            image.ParseImage();
            WzImageProperty chargeNode = image["charge"]?[chargeElement.ToString(CultureInfo.InvariantCulture)];
            if (chargeNode == null)
            {
                _missingCharacterChargeAfterImageKeys.Add(cacheKey);
                return null;
            }

            MeleeAfterImageCatalog catalog = LoadAfterImageCatalog(chargeNode);
            if (catalog.Actions.Count == 0)
            {
                _missingCharacterChargeAfterImageKeys.Add(cacheKey);
                return null;
            }

            _characterChargeAfterImageCache[cacheKey] = catalog;
            return catalog;
        }

        private MeleeAfterImageCatalog LoadAfterImageCatalog(WzImageProperty rootNode)
        {
            var catalog = new MeleeAfterImageCatalog();
            if (rootNode == null)
            {
                return catalog;
            }

            foreach (WzImageProperty child in rootNode.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                MeleeAfterImageAction action = LoadAfterImageAction(child);
                if (action.HasRange || action.FrameSets.Count > 0)
                {
                    catalog.Actions[child.Name] = action;
                }
            }

            return catalog;
        }

        private MeleeAfterImageAction LoadAfterImageAction(WzImageProperty actionNode)
        {
            var action = new MeleeAfterImageAction();
            if (actionNode == null)
            {
                return action;
            }

            Point? lt = GetVector(actionNode, "lt");
            Point? rb = GetVector(actionNode, "rb");
            if (lt.HasValue || rb.HasValue)
            {
                int left = lt?.X ?? 0;
                int top = lt?.Y ?? 0;
                int right = rb?.X ?? left;
                int bottom = rb?.Y ?? top;
                action.Range = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            }

            foreach (WzImageProperty child in actionNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int frameIndex))
                {
                    continue;
                }

                MeleeAfterImageFrameSet frameSet = LoadAfterImageFrameSet(child);
                if (frameSet.Frames.Count > 0)
                {
                    action.FrameSets[frameIndex] = frameSet;
                }
            }

            return action;
        }

        private MeleeAfterImageFrameSet LoadAfterImageFrameSet(WzImageProperty frameSetNode)
        {
            var frameSet = new MeleeAfterImageFrameSet();
            if (frameSetNode == null)
            {
                return frameSet;
            }

            if (frameSetNode is WzCanvasProperty)
            {
                SkillFrame directFrame = LoadSkillFrame(frameSetNode);
                if (directFrame != null)
                {
                    frameSet.Frames.Add(directFrame);
                }

                return frameSet;
            }

            foreach (WzImageProperty child in EnumerateAfterImageFrameChildrenInClientOrder(frameSetNode))
            {
                if (child == null)
                {
                    continue;
                }

                SkillFrame frame = LoadSkillFrame(child);
                if (frame != null)
                {
                    frameSet.Frames.Add(frame);
                }
            }

            return frameSet;
        }

        internal static IReadOnlyList<WzImageProperty> EnumerateAfterImageFrameChildrenInClientOrder(WzImageProperty frameSetNode)
        {
            if (frameSetNode == null)
            {
                return Array.Empty<WzImageProperty>();
            }

            if (frameSetNode is WzCanvasProperty)
            {
                return new[] { frameSetNode };
            }

            if (frameSetNode.WzProperties == null || frameSetNode.WzProperties.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            int childCount = frameSetNode.WzProperties.Count;
            List<WzImageProperty> orderedFrames = new(childCount);
            for (int i = 0; i < childCount; i++)
            {
                WzImageProperty child = frameSetNode[i.ToString(CultureInfo.InvariantCulture)];
                if (child != null)
                {
                    orderedFrames.Add(child);
                }
            }

            if (orderedFrames.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            return orderedFrames;
        }

        private static int GetWeaponAfterImageMasteryIndex(int masteryPercent)
        {
            return Math.Max(0, (Math.Max(10, masteryPercent) - 10) / 5);
        }

        private static IEnumerable<string> ResolveAfterImageWeaponTypeKeys(WeaponPart weapon)
        {
            if (weapon == null)
            {
                yield return "barehands";
                yield break;
            }

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in EnumerateAfterImageWeaponTypeCandidates(weapon))
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IEnumerable<string> EnumerateAfterImageWeaponTypeCandidates(WeaponPart weapon)
        {
            if (!string.IsNullOrWhiteSpace(weapon?.AfterImageType))
            {
                yield return weapon.AfterImageType;

                switch (weapon.AfterImageType)
                {
                    case "swordOL":
                        yield return "swordOS";
                        break;
                    case "swordOS":
                        yield return "swordOL";
                        break;
                    case "swordTL":
                        yield return "swordTS";
                        break;
                    case "swordTS":
                        yield return "swordTL";
                        break;
                }

                yield break;
            }

            int weaponCode = Math.Abs(weapon.ItemId / 10000) % 100;
            string fallback = weaponCode switch
            {
                30 => "swordOL",
                31 or 41 => "axe",
                32 or 42 => "mace",
                33 or 34 or 36 => null,
                37 or 38 => null,
                40 => "swordTL",
                43 => "spear",
                44 => "poleArm",
                45 => "bow",
                46 => "crossBow",
                47 => null,
                48 => "knuckle",
                49 => "gun",
                52 => "dualBow",
                53 => "cannon",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                yield return fallback;
            }
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

        private SkillAnimation LoadOptionalSkillAnimation(WzImageProperty skillNode, string branchName, bool loop)
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

            animation.Loop = loop || animation.Loop;
            return animation;
        }

        private void LoadShadowPartnerActionAnimations(SkillData skill, WzImageProperty skillNode)
        {
            if (!IsShadowPartnerSkill(skill, skillNode))
            {
                return;
            }

            WzImageProperty specialNode = ResolveShadowPartnerSourcePropertyFromSkillNode(skillNode);
            if (specialNode == null)
            {
                return;
            }

            IReadOnlyDictionary<string, string> storagePlan = BuildShadowPartnerActionStoragePlan(
                specialNode.WzProperties.Select(static child => child?.Name));

            foreach (WzImageProperty child in specialNode.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                string actionKey = ResolveShadowPartnerActionStorageKey(child.Name);
                if (string.IsNullOrWhiteSpace(actionKey)
                    || !storagePlan.TryGetValue(actionKey, out string preferredSourceKey)
                    || !string.Equals(preferredSourceKey, child.Name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                SkillAnimation animation = LoadShadowPartnerActionAnimation(child, actionKey);
                if (animation.Frames.Count == 0)
                {
                    continue;
                }

                skill.ShadowPartnerActionAnimations[actionKey] = animation;
            }

            SynthesizePiecedShadowPartnerActionAnimations(skill.ShadowPartnerActionAnimations);
            skill.ShadowPartnerHorizontalOffsetPx = ResolveShadowPartnerHorizontalOffsetPx(skill.ShadowPartnerActionAnimations);
        }

        private SkillAnimation LoadShadowPartnerActionAnimation(WzImageProperty actionNode, string actionKey)
        {
            var animation = new SkillAnimation { Name = actionKey };
            foreach (WzImageProperty frameNode in EnumerateShadowPartnerActionFrameChildrenInClientOrder(actionNode))
            {
                SkillFrame frame = LoadShadowPartnerActionFrame(frameNode);
                if (frame != null)
                {
                    animation.Frames.Add(frame);
                }
            }

            animation.Loop = ShouldLoopShadowPartnerAction(actionKey);
            if (ShouldAppendReversedShadowPartnerFrames(actionKey))
            {
                AppendReversedInteriorShadowPartnerFrames(animation);
            }

            return animation;
        }

        internal static IReadOnlyDictionary<string, string> BuildShadowPartnerActionStoragePlan(IEnumerable<string> sourceActionKeys)
        {
            var storagePlan = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (sourceActionKeys == null)
            {
                return storagePlan;
            }

            foreach (string sourceActionKey in sourceActionKeys)
            {
                string storageKey = ResolveShadowPartnerActionStorageKey(sourceActionKey);
                if (string.IsNullOrWhiteSpace(storageKey))
                {
                    continue;
                }

                if (!storagePlan.TryGetValue(storageKey, out string existingSourceKey)
                    || ShouldReplaceShadowPartnerActionSource(existingSourceKey, sourceActionKey))
                {
                    storagePlan[storageKey] = sourceActionKey;
                }
            }

            return storagePlan;
        }

        internal static WzImageProperty ResolveShadowPartnerSourcePropertyFromSkillNode(WzImageProperty skillNode)
        {
            return ResolveLinkedProperty(skillNode?["special"]);
        }

        internal static IReadOnlyList<WzImageProperty> EnumerateShadowPartnerActionFrameChildrenInClientOrder(WzImageProperty actionNode)
        {
            if (actionNode == null)
            {
                return Array.Empty<WzImageProperty>();
            }

            if (actionNode is WzCanvasProperty)
            {
                return new[] { actionNode };
            }

            if (actionNode.WzProperties == null || actionNode.WzProperties.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            List<WzImageProperty> orderedFrames = new();
            foreach (WzImageProperty child in actionNode.WzProperties)
            {
                if (child == null)
                {
                    continue;
                }

                if (child is not WzCanvasProperty && child.GetLinkedWzImageProperty() is not WzCanvasProperty)
                {
                    continue;
                }

                orderedFrames.Add(child);
            }

            if (orderedFrames.Count == 0)
            {
                return Array.Empty<WzImageProperty>();
            }

            return orderedFrames;
        }

        internal static string ResolveShadowPartnerActionStorageKey(string sourceActionKey)
        {
            if (string.IsNullOrWhiteSpace(sourceActionKey))
            {
                return null;
            }

            if (!sourceActionKey.All(char.IsDigit))
            {
                return sourceActionKey;
            }

            if (int.TryParse(sourceActionKey, out int rawActionCode)
                && CharacterPart.TryGetActionStringFromCode(rawActionCode, out string resolvedActionName)
                && !string.IsNullOrWhiteSpace(resolvedActionName))
            {
                return resolvedActionName;
            }

            return sourceActionKey;
        }

        internal static bool ShouldReplaceShadowPartnerActionSource(string existingSourceKey, string incomingSourceKey)
        {
            if (string.IsNullOrWhiteSpace(incomingSourceKey))
            {
                return false;
            }

            bool existingIsNumeric = !string.IsNullOrWhiteSpace(existingSourceKey) && existingSourceKey.All(char.IsDigit);
            bool incomingIsNumeric = incomingSourceKey.All(char.IsDigit);
            return existingIsNumeric && !incomingIsNumeric;
        }

        internal static bool ShouldAppendReversedShadowPartnerFrames(string actionKey)
        {
            return !string.IsNullOrWhiteSpace(actionKey)
                   && ShadowPartnerReplayTailActionNames.Contains(actionKey);
        }

        internal static void SynthesizePiecedShadowPartnerActionAnimations(
            IDictionary<string, SkillAnimation> actionAnimations)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return;
            }

            IReadOnlyDictionary<string, SkillAnimation> readOnlyActionAnimations =
                actionAnimations as IReadOnlyDictionary<string, SkillAnimation>
                ?? new Dictionary<string, SkillAnimation>(actionAnimations, StringComparer.OrdinalIgnoreCase);

            foreach (string actionName in ShadowPartnerClientActionResolver.EnumeratePiecedShadowPartnerActionNames())
            {
                if (string.IsNullOrWhiteSpace(actionName) || actionAnimations.ContainsKey(actionName))
                {
                    continue;
                }

                SkillAnimation piecedAnimation = ShadowPartnerClientActionResolver.TryBuildPiecedShadowPartnerActionAnimation(
                    readOnlyActionAnimations,
                    actionName);
                if (piecedAnimation?.Frames.Count > 0)
                {
                    actionAnimations[actionName] = piecedAnimation;
                }
            }
        }

        internal static void AppendReversedInteriorShadowPartnerFrames(SkillAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count < 3)
            {
                return;
            }

            SkillFrame[] originalFrames = animation.Frames.ToArray();
            for (int i = originalFrames.Length - 2; i >= 1; i--)
            {
                animation.Frames.Add(originalFrames[i]);
            }

            animation.CalculateDuration();
        }

        private static int ResolveShadowPartnerHorizontalOffsetPx(IReadOnlyDictionary<string, SkillAnimation> actionAnimations)
        {
            if (actionAnimations == null || actionAnimations.Count == 0)
            {
                return 26;
            }

            foreach (string actionName in PreferredShadowPartnerOffsetActions)
            {
                if (!actionAnimations.TryGetValue(actionName, out SkillAnimation animation)
                    || animation?.Frames == null
                    || animation.Frames.Count == 0)
                {
                    continue;
                }

                SkillFrame firstFrame = animation.Frames[0];
                if (firstFrame == null)
                {
                    continue;
                }

                // Shadow Partner `special/*` idle origins resolve to a 26px separation;
                // use the authored origin as the baseline instead of a second hardcoded render offset.
                return Math.Max(0, firstFrame.Origin.X - 2);
            }

            return 26;
        }

        private static bool IsShadowPartnerSkill(SkillData skill, WzImageProperty skillNode)
        {
            if (skill == null || skillNode?["special"] == null)
            {
                return false;
            }

            if (ShadowPartnerSkillIds.Contains(skill.SkillId))
            {
                return true;
            }

            return skill.IsBuff
                   && !string.IsNullOrWhiteSpace(skill.Name)
                   && skill.Name.IndexOf("shadow partner", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private SkillFrame LoadShadowPartnerActionFrame(WzImageProperty frameNode)
        {
            return LoadSkillFrame(frameNode, 100, true);
        }

        private static bool ShouldLoopShadowPartnerAction(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            if (actionName.StartsWith("create", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "dead", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("swing", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("stab", StringComparison.OrdinalIgnoreCase)
                || actionName.StartsWith("shoot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "proneStab", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
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
            return LoadSkillAnimation(node, name, LoadSkillFrame);
        }

        private SkillAnimation LoadSkillAnimation(
            WzImageProperty node,
            string name,
            Func<WzImageProperty, SkillFrame> frameLoader)
        {
            var animation = new SkillAnimation { Name = name };
            if (node == null)
                return animation;

            frameLoader ??= LoadSkillFrame;

            // Try numbered frames (0, 1, 2, ...)
            int frameIndex = 0;
            while (true)
            {
                var frameNode = node[frameIndex.ToString()];
                if (frameNode == null)
                    break;

                var frame = frameLoader(frameNode);
                if (frame != null)
                {
                    animation.Frames.Add(frame);
                }
                frameIndex++;
            }

            // If no numbered frames, check for direct canvas
            if (animation.Frames.Count == 0 && node is WzCanvasProperty canvas)
            {
                var frame = frameLoader(node);
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
                        var subAnim = LoadSkillAnimation(child, child.Name, frameLoader);
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
            animation.PositionCode = node["pos"] != null ? GetInt(node, "pos") : null;

            return animation;
        }

        private ZoneEffectData LoadZoneEffect(WzImageProperty tileNode, WzImageProperty skillNode)
        {
            if (tileNode == null)
            {
                return null;
            }

            var zoneEffect = new ZoneEffectData
            {
                Animation = LoadZoneAnimation(tileNode),
                AnimationPath = LoadZoneAnimationPath(tileNode),
                VariantAnimations = LoadSummonIndexedAnimations(tileNode, "tile"),
                VariantAnimationPaths = LoadSummonIndexedAnimationPaths(tileNode),
                EffectDistance = GetInt(tileNode, "effectDistance")
            };

            if (zoneEffect.Animation?.Frames.Count <= 0)
            {
                zoneEffect.Animation = zoneEffect.ResolveAnimationVariant(1, 1);
            }

            PopulateZoneCharacterLevelVariants(zoneEffect, skillNode?["CharLevel"]);
            PopulateZoneLevelVariants(zoneEffect, skillNode?["level"]);

            bool hasRenderableAnimation = zoneEffect.Animation?.Frames.Count > 0
                || zoneEffect.VariantAnimations.Count > 0
                || zoneEffect.CharacterLevelVariantAnimations.Count > 0
                || zoneEffect.LevelVariantAnimations.Count > 0;
            return hasRenderableAnimation ? zoneEffect : null;
        }

        private SkillAnimation LoadZoneAnimation(WzImageProperty tileNode)
        {
            if (tileNode == null)
            {
                return null;
            }

            foreach (WzImageProperty child in tileNode.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                {
                    continue;
                }

                SkillAnimation animation = LoadSkillAnimation(child, "tile");
                if (animation.Frames.Count > 0)
                {
                    animation.Loop = true;
                    return animation;
                }
            }

            SkillAnimation fallbackAnimation = LoadSkillAnimation(tileNode, "tile");
            if (fallbackAnimation.Frames.Count > 0)
            {
                fallbackAnimation.Loop = true;
                return fallbackAnimation;
            }

            return null;
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
            skill.ResolvedSummonAssetPath = summonNode.FullPath;

            var branchNames = summonNode.WzProperties
                .Select(child => child.Name)
                .ToArray();
            SummonMovementProfile movementProfile = SummonMovementResolver.Resolve(skill.SkillId, branchNames);
            skill.SummonMoveAbility = movementProfile.MoveAbility;
            skill.SummonMovementStyle = movementProfile.Style;
            skill.SummonSpawnDistanceX = movementProfile.SpawnDistanceX;

            bool standaloneSummonRoot = LooksLikeStandaloneSummonSourceProperty(summonNode);

            SkillAnimation directAnimation = GetOrLoadSummonActionAnimation(skill, summonNode, "summon");
            RegisterSummonActionAnimation(skill, "summon", directAnimation);

            string spawnBranchName = SelectPreferredSummonSpawnBranch(branchNames);
            if (spawnBranchName != null)
            {
                var spawnBranch = summonNode[spawnBranchName];
                if (spawnBranch != null)
                {
                    SkillAnimation spawnAnimation = GetOrLoadSummonActionAnimation(skill, spawnBranch, spawnBranchName);
                    if (spawnAnimation.Frames.Count > 0)
                    {
                        skill.SummonSpawnAnimation = spawnAnimation;
                        skill.SummonSpawnBranchName = spawnBranchName;
                        RegisterSummonActionAnimation(skill, spawnBranchName, spawnAnimation);
                    }
                }
            }

            string preferredBranchName = SelectPreferredSummonIdleBranch(branchNames);
            if (preferredBranchName == null && directAnimation.Frames.Count == 0)
            {
                preferredBranchName = SelectFallbackSummonIdleBranch(summonNode, branchNames, standaloneSummonRoot);
            }

            if (preferredBranchName == null)
            {
                if (directAnimation.Frames.Count > 0)
                {
                    skill.SummonAnimation = directAnimation;
                    skill.SummonIdleBranchName = "summon";
                    if (skill.SummonSpawnAnimation == null)
                    {
                        skill.SummonSpawnAnimation = directAnimation;
                        skill.SummonSpawnBranchName = "summon";
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
                    skill.SummonIdleBranchName = "summon";
                }

                return;
            }

            SkillAnimation summonAnimation = GetOrLoadSummonActionAnimation(skill, preferredBranch, preferredBranchName, "summon");
            if (summonAnimation.Frames.Count == 0)
            {
                if (directAnimation.Frames.Count > 0)
                {
                    skill.SummonAnimation = directAnimation;
                    skill.SummonIdleBranchName = "summon";
                }

                return;
            }

            if (!summonAnimation.Loop
                && (preferredBranchName.Equals("stand", StringComparison.OrdinalIgnoreCase)
                    || preferredBranchName.Equals("fly", StringComparison.OrdinalIgnoreCase)
                    || preferredBranchName.Equals("move", StringComparison.OrdinalIgnoreCase)
                    || preferredBranchName.Equals("walk", StringComparison.OrdinalIgnoreCase)
                    || IsRepeatStyleSummonBranchName(preferredBranchName)))
            {
                summonAnimation.Loop = true;
            }

            skill.SummonAnimation = summonAnimation;
            skill.SummonIdleBranchName = preferredBranchName;
            RegisterSummonActionAnimation(skill, preferredBranchName, summonAnimation);
            if (skill.SummonSpawnAnimation == null)
            {
                skill.SummonSpawnAnimation = summonAnimation;
                skill.SummonSpawnBranchName = preferredBranchName;
            }

            string supportBranchName = SelectPreferredSummonSupportBranch(branchNames);
            if (supportBranchName != null)
            {
                PopulateSummonSupportMetadata(skill, supportBranchName, summonNode[supportBranchName]);
            }

            WzImageProperty prepareBranch = summonNode["prepare"];
            if (prepareBranch != null)
            {
                SkillAnimation prepareAnimation = GetOrLoadSummonActionAnimation(skill, prepareBranch, "prepare");
                if (prepareAnimation.Frames.Count > 0)
                {
                    skill.SummonAttackPrepareAnimation = prepareAnimation;
                    skill.SummonPrepareBranchName = "prepare";
                    RegisterSummonActionAnimation(skill, "prepare", prepareAnimation);
                }
            }

            string removalBranchName = skill.SelfDestructMinion
                ? SelectPreferredSummonBranch(branchNames, new[] { "die", "die1" })
                : null;
            WzImageProperty removalBranch = !string.IsNullOrWhiteSpace(removalBranchName)
                ? summonNode[removalBranchName]
                : null;
            string attackBranchName = SelectPreferredSummonAttackBranch(branchNames);
            if (attackBranchName == null && skill.SelfDestructMinion)
            {
                attackBranchName = SelectPreferredSummonBranch(branchNames, new[] { "die", "die1" });
            }

            WzImageProperty attackBranch = !string.IsNullOrWhiteSpace(attackBranchName)
                ? summonNode[attackBranchName]
                : null;

            if (attackBranch != null)
            {
                PopulateSummonAttackMetadata(skill, attackBranch, attackBranchName);
                if (skill.SelfDestructMinion
                    && removalBranch != null
                    && !string.Equals(removalBranchName, attackBranchName, StringComparison.OrdinalIgnoreCase))
                {
                    PopulateSummonAttackMetadata(skill, removalBranch, removalBranchName);
                }

                skill.SummonAttackBranchName = attackBranchName;

                SkillAnimation attackAnimation = GetOrLoadSummonActionAnimation(skill, attackBranch, attackBranchName);
                if (attackAnimation.Frames.Count > 0)
                {
                    skill.SummonAttackAnimation = attackAnimation;
                    RegisterSummonActionAnimation(skill, attackBranchName, attackAnimation);
                }
            }

            if (removalBranch != null)
            {
                SkillAnimation removalAnimation = GetOrLoadSummonActionAnimation(skill, removalBranch, removalBranchName);
                if (removalAnimation.Frames.Count > 0)
                {
                    skill.SummonRemovalAnimation = removalAnimation;
                    skill.SummonRemovalBranchName = removalBranchName;
                    RegisterSummonActionAnimation(skill, removalBranchName, removalAnimation);
                }
            }

            skill.SummonProjectileAnimations = LoadSummonIndexedAnimations(summonNode["ball"], "ball");
            skill.SummonTargetHitPresentations = LoadSummonImpactPresentations(summonNode["mob"], "mob");

            if (attackBranch != null)
            {
                List<SkillAnimation> attackBranchProjectileAnimations = BuildSummonBranchProjectileAnimations(
                    skill.SummonProjectileAnimations,
                    LoadSummonIndexedAnimations(attackBranch["info"]?["ball"], $"{attackBranchName}/info/ball"));
                List<SummonImpactPresentation> attackBranchImpactPresentations = BuildSummonBranchImpactPresentations(
                    skill.SummonTargetHitPresentations,
                    LoadSummonImpactPresentations(attackBranch["info"]?["mob"], $"{attackBranchName}/info/mob"));
                skill.SummonProjectileAnimations = attackBranchProjectileAnimations;
                skill.SummonTargetHitPresentations = attackBranchImpactPresentations;
                RegisterSummonBranchImpactMetadata(
                    skill,
                    attackBranchName,
                    attackBranchProjectileAnimations,
                    attackBranchImpactPresentations);
            }

            if (removalBranch != null)
            {
                AppendSummonIndexedAnimations(
                    skill.SummonProjectileAnimations,
                    LoadSummonIndexedAnimations(removalBranch["info"]?["ball"], $"{removalBranchName}/info/ball"));
                AppendSummonImpactPresentations(
                    skill.SummonTargetHitPresentations,
                    LoadSummonImpactPresentations(removalBranch["info"]?["mob"], $"{removalBranchName}/info/mob"));
            }

            WzImageProperty hitNode = summonNode["hit"] ?? (summonNode.Parent as WzImageProperty)?["hit"];
            AppendSummonImpactPresentations(
                skill.SummonTargetHitPresentations,
                LoadSummonHitTargetAnimations(hitNode, "hit"));
            SkillAnimation hitAnimation = LoadSummonHitAnimation(skill, hitNode, out string hitBranchName);
            if (hitAnimation?.Frames.Count > 0)
            {
                skill.SummonHitAnimation = hitAnimation;
                skill.SummonHitBranchName = hitBranchName;
                RegisterSummonActionAnimation(skill, hitBranchName, hitAnimation);
            }

            skill.SummonTargetHitAnimations = skill.SummonTargetHitPresentations
                .Select(static presentation => presentation?.Animation)
                .Where(static animation => animation?.Frames.Count > 0)
                .ToList();

            LoadSupplementalSummonAnimations(skill, summonNode, branchNames, standaloneSummonRoot);
            LoadRemainingSummonActionAnimations(skill, summonNode, branchNames, standaloneSummonRoot);
            if (!string.IsNullOrWhiteSpace(attackBranchName))
            {
                PopulateSummonHitTimingMetadata(skill, attackBranchName, hitNode);
            }
        }

        internal WzImageProperty ResolveSummonSourceProperty(SkillData skill, WzImageProperty skillNode)
        {
            if (TryResolveSummonSourceProperty(skill, skillNode, out WzImageProperty summonNode))
            {
                return summonNode;
            }

            return null;
        }

        private string LoadZoneAnimationPath(WzImageProperty tileNode)
        {
            if (tileNode == null)
            {
                return null;
            }

            foreach (WzImageProperty child in tileNode.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                {
                    continue;
                }

                SkillAnimation animation = LoadSkillAnimation(child, $"tile/{child.Name}");
                if (animation?.Frames.Count > 0)
                {
                    return NormalizeSkillAssetPath(child);
                }
            }

            SkillAnimation fallbackAnimation = LoadSkillAnimation(tileNode, "tile");
            return fallbackAnimation?.Frames.Count > 0
                ? NormalizeSkillAssetPath(tileNode)
                : null;
        }

        private void PopulateZoneCharacterLevelVariants(ZoneEffectData zoneEffect, WzImageProperty charLevelNode)
        {
            if (zoneEffect == null || charLevelNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in charLevelNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int requiredLevel))
                {
                    continue;
                }

                WzImageProperty tileVariantNode = child["tile"];
                if (tileVariantNode == null)
                {
                    continue;
                }

                List<SkillAnimation> variants = LoadSummonIndexedAnimations(
                    tileVariantNode,
                    $"tile/CharLevel/{requiredLevel}");
                if (variants.Count > 0)
                {
                    zoneEffect.CharacterLevelVariantAnimations[requiredLevel] = variants;
                    zoneEffect.CharacterLevelVariantAnimationPaths[requiredLevel] = LoadSummonIndexedAnimationPaths(tileVariantNode);
                    zoneEffect.CharacterLevelEffectDistances[requiredLevel] = GetInt(tileVariantNode, "effectDistance");
                }
            }
        }

        private void PopulateZoneLevelVariants(ZoneEffectData zoneEffect, WzImageProperty levelNode)
        {
            if (zoneEffect == null || levelNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in levelNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int skillLevel))
                {
                    continue;
                }

                WzImageProperty tileVariantNode = child["tile"];
                if (tileVariantNode == null)
                {
                    continue;
                }

                List<SkillAnimation> variants = LoadSummonIndexedAnimations(
                    tileVariantNode,
                    $"tile/level/{skillLevel}");
                if (variants.Count > 0)
                {
                    zoneEffect.LevelVariantAnimations[skillLevel] = variants;
                    zoneEffect.LevelVariantAnimationPaths[skillLevel] = LoadSummonIndexedAnimationPaths(tileVariantNode);
                    zoneEffect.LevelEffectDistances[skillLevel] = GetInt(tileVariantNode, "effectDistance");
                }
            }
        }

        internal bool TryResolveSummonSourceProperty(
            SkillData skill,
            WzImageProperty skillNode,
            out WzImageProperty summonNode)
        {
            summonNode = null;
            foreach (WzImageProperty candidateSkillNode in EnumerateSummonSourceSkillNodes(skill, skillNode))
            {
                if (TryResolveSummonSourcePropertyFromSkillNode(candidateSkillNode, out summonNode))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<WzImageProperty> EnumerateSummonSourceSkillNodes(SkillData skill, WzImageProperty skillNode)
        {
            var yieldedSkillIds = new HashSet<int>();
            if (skillNode != null)
            {
                yield return skillNode;
                if (skill?.SkillId > 0)
                {
                    yieldedSkillIds.Add(skill.SkillId);
                }
            }

            foreach (int linkedSkillId in EnumerateSummonSourceCandidateSkillIds(skill, LoadSummonSourceCandidateSkillMetadata))
            {
                if (linkedSkillId <= 0 || !yieldedSkillIds.Add(linkedSkillId))
                {
                    continue;
                }

                if (TryGetSkillNode(linkedSkillId, out WzImageProperty linkedSkillNode))
                {
                    yield return linkedSkillNode;
                }
            }
        }

        private SkillData LoadSummonSourceCandidateSkillMetadata(int skillId)
        {
            if (skillId <= 0)
            {
                return null;
            }

            if (_skillCache.TryGetValue(skillId, out SkillData cachedSkill))
            {
                return cachedSkill;
            }

            if (!TryGetSkillNode(skillId, out WzImageProperty skillNode))
            {
                return null;
            }

            var metadata = new SkillData
            {
                SkillId = skillId,
                Job = skillId / 10000
            };

            ParseSkillInfo(metadata, skillNode);
            return metadata;
        }

        internal static IEnumerable<int> EnumerateSummonSourceCandidateSkillIds(
            SkillData skill,
            Func<int, SkillData> linkedSkillResolver = null)
        {
            if (skill == null)
            {
                yield break;
            }

            var visitedSkillIds = new HashSet<int>();
            if (skill.SkillId > 0)
            {
                visitedSkillIds.Add(skill.SkillId);
            }

            var pendingSkillIds = new Queue<int>();
            foreach (int linkedSkillId in EnumerateDirectSummonSourceCandidateSkillIds(skill))
            {
                if (linkedSkillId > 0)
                {
                    pendingSkillIds.Enqueue(linkedSkillId);
                }
            }

            while (pendingSkillIds.Count > 0)
            {
                int linkedSkillId = pendingSkillIds.Dequeue();
                if (linkedSkillId <= 0 || !visitedSkillIds.Add(linkedSkillId))
                {
                    continue;
                }

                yield return linkedSkillId;

                SkillData linkedSkill = linkedSkillResolver?.Invoke(linkedSkillId);
                if (linkedSkill == null)
                {
                    continue;
                }

                foreach (int nestedSkillId in EnumerateDirectSummonSourceCandidateSkillIds(linkedSkill))
                {
                    if (nestedSkillId > 0 && !visitedSkillIds.Contains(nestedSkillId))
                    {
                        pendingSkillIds.Enqueue(nestedSkillId);
                    }
                }
            }
        }

        private static IEnumerable<int> EnumerateDirectSummonSourceCandidateSkillIds(SkillData skill)
        {
            if (skill == null)
            {
                yield break;
            }

            foreach (int linkedSkillId in skill.GetAffectedSkillIds())
            {
                if (linkedSkillId > 0)
                {
                    yield return linkedSkillId;
                }
            }

            if (skill.DummySkillParents != null)
            {
                foreach (int linkedSkillId in skill.DummySkillParents)
                {
                    if (linkedSkillId > 0)
                    {
                        yield return linkedSkillId;
                    }
                }
            }

            if (skill.RequiredSkillIds != null)
            {
                foreach (int linkedSkillId in skill.RequiredSkillIds)
                {
                    if (linkedSkillId > 0)
                    {
                        yield return linkedSkillId;
                    }
                }
            }
        }

        private bool TryGetSkillNode(int skillId, out WzImageProperty skillNode)
        {
            skillNode = null;
            if (skillId <= 0)
            {
                return false;
            }

            int jobId = skillId / 10000;
            WzImage jobImage = GetSkillImage($"{jobId}.img");
            if (jobImage == null)
            {
                return false;
            }

            jobImage.ParseImage();
            skillNode = jobImage["skill"]?[skillId.ToString(CultureInfo.InvariantCulture)];
            return skillNode != null;
        }

        private void LoadSupplementalSummonAnimations(
            SkillData skill,
            WzImageProperty summonNode,
            IEnumerable<string> branchNames,
            bool standaloneSummonRoot)
        {
            if (skill == null || summonNode == null || branchNames == null)
            {
                return;
            }

            foreach (string branchName in branchNames)
            {
                if (string.IsNullOrWhiteSpace(branchName)
                    || Array.IndexOf(SupplementalSummonAnimationBranches, branchName) < 0
                    || (standaloneSummonRoot && ShouldSkipStandaloneSummonWrapperBranchName(branchName))
                    || skill.SummonNamedAnimations.ContainsKey(branchName))
                {
                    continue;
                }

                SkillAnimation animation = GetOrLoadSummonActionAnimation(skill, summonNode[branchName], branchName);
                if (animation.Frames.Count > 0)
                {
                    RegisterSummonActionAnimation(skill, branchName, animation);
                    RegisterSupplementalSummonAttackMetadata(skill, summonNode, branchName);
                }
            }
        }

        private void LoadRemainingSummonActionAnimations(
            SkillData skill,
            WzImageProperty summonNode,
            IEnumerable<string> branchNames,
            bool standaloneSummonRoot)
        {
            if (skill == null || summonNode == null || branchNames == null)
            {
                return;
            }

            foreach (string branchName in branchNames)
            {
                if (string.IsNullOrWhiteSpace(branchName)
                    || NonActionSummonBranchNames.Contains(branchName)
                    || (standaloneSummonRoot && ShouldSkipStandaloneSummonWrapperBranchName(branchName))
                    || string.Equals(branchName, "hit", StringComparison.OrdinalIgnoreCase)
                    || skill.SummonActionAnimations.ContainsKey(branchName))
                {
                    continue;
                }

                WzImageProperty branchNode = summonNode[branchName];
                if (!IsSummonActionBranchProperty(branchNode))
                {
                    continue;
                }

                SkillAnimation animation = GetOrLoadSummonActionAnimation(skill, branchNode, branchName);
                if (animation.Frames.Count > 0)
                {
                    RegisterSummonActionAnimation(skill, branchName, animation);
                }
            }
        }

        private SkillAnimation GetOrLoadSummonActionAnimation(
            SkillData skill,
            WzImageProperty node,
            string actionKey,
            string animationName = null)
        {
            string normalizedKey = string.IsNullOrWhiteSpace(actionKey) ? animationName : actionKey;
            if (TryGetCachedSummonActionAnimation(skill, normalizedKey, out SkillAnimation cachedAnimation))
            {
                return cachedAnimation;
            }

            if (skill?.SummonActionAnimations != null
                && !string.IsNullOrWhiteSpace(normalizedKey)
                && skill.SummonActionAnimations.TryGetValue(normalizedKey, out cachedAnimation))
            {
                return cachedAnimation;
            }

            SkillAnimation animation = LoadSummonActionAnimation(node, animationName ?? normalizedKey ?? "summon");
            CacheSummonActionAnimation(skill, normalizedKey, animation);

            return animation;
        }

        private bool TryGetCachedSummonActionAnimation(
            int skillId,
            int skillLevel,
            string actionKey,
            out SkillAnimation animation)
        {
            animation = null;
            if (skillId <= 0 || skillLevel <= 0 || string.IsNullOrWhiteSpace(actionKey))
            {
                return false;
            }

            return _summonActionCache.TryGetValue(
                new SummonActionCacheKey(skillId, skillLevel, actionKey),
                out animation);
        }

        private bool TryGetCachedSummonActionAnimation(SkillData skill, string actionKey, out SkillAnimation animation)
        {
            animation = null;
            if (skill == null || string.IsNullOrWhiteSpace(actionKey))
            {
                return false;
            }

            foreach (int level in EnumerateSummonActionCacheLevels(skill))
            {
                if (_summonActionCache.TryGetValue(new SummonActionCacheKey(skill.SkillId, level, actionKey), out animation))
                {
                    skill.SummonActionAnimations[actionKey] = animation;
                    return true;
                }
            }

            return false;
        }

        private void CacheSummonActionAnimation(
            SkillData skill,
            string actionKey,
            SkillAnimation animation,
            IEnumerable<int> explicitLevels = null)
        {
            if (skill == null || string.IsNullOrWhiteSpace(actionKey) || animation == null)
            {
                return;
            }

            skill.SummonActionAnimations[actionKey] = animation;
            IEnumerable<int> cacheLevels = explicitLevels ?? EnumerateSummonActionCacheLevels(skill);
            foreach (int level in cacheLevels
                         .Where(level => level > 0)
                         .Distinct())
            {
                _summonActionCache[new SummonActionCacheKey(skill.SkillId, level, actionKey)] = animation;
            }
        }

        private void RegisterSummonActionAnimation(SkillData skill, string actionKey, SkillAnimation animation)
        {
            if (skill == null || string.IsNullOrWhiteSpace(actionKey) || animation?.Frames.Count <= 0)
            {
                return;
            }

            CacheSummonActionAnimation(skill, actionKey, animation);
            if (ShouldTrackSummonNamedActionKey(actionKey))
            {
                skill.SummonNamedAnimations[actionKey] = animation;
            }
        }

        internal static bool ShouldTrackSummonNamedActionKey(string actionKey)
        {
            return !string.IsNullOrWhiteSpace(actionKey)
                && actionKey.IndexOf('/') < 0;
        }

        private static IEnumerable<int> EnumerateSummonActionCacheLevels(SkillData skill)
        {
            if (skill?.Levels?.Count > 0)
            {
                return skill.Levels.Keys
                    .Where(level => level > 0)
                    .Distinct()
                    .OrderBy(level => level);
            }

            int maxLevel = skill?.MaxLevel ?? 0;
            if (maxLevel > 0)
            {
                return Enumerable.Range(1, maxLevel);
            }

            return new[] { 1 };
        }

        private SkillAnimation LoadSummonActionAnimation(WzImageProperty node, string name)
        {
            SkillAnimation animation = LoadSkillAnimation(node, name, LoadSummonActionFrame);
            if (animation.Frames.Count > 0 && ShouldAppendReversedSummonFrames(node, name))
            {
                AppendReversedSummonFrames(animation);
            }

            return animation;
        }

        private static void PopulateSummonAttackMetadata(SkillData skill, WzImageProperty attackBranch, string branchName = null)
        {
            if (skill == null || attackBranch == null)
            {
                return;
            }

            WzImageProperty infoNode = attackBranch["info"];
            if (infoNode == null)
            {
                return;
            }

            int attackAfter = GetInt(infoNode, "attackAfter");
            if (attackAfter > 0)
            {
                if (skill.SummonAttackIntervalMs <= 0)
                {
                    skill.SummonAttackIntervalMs = attackAfter;
                }

                if (!string.IsNullOrWhiteSpace(branchName))
                {
                    skill.SummonAttackAfterMsByBranch[branchName] = attackAfter;
                }
            }

            int attackCount = GetInt(infoNode, "attackCount");
            if (attackCount > 0 && skill.SummonAttackCountOverride <= 0)
            {
                skill.SummonAttackCountOverride = attackCount;
            }

            int mobCount = GetInt(infoNode, "mobCount");
            if (mobCount > 0 && skill.SummonMobCountOverride <= 0)
            {
                skill.SummonMobCountOverride = mobCount;
            }

            int bulletSpeed = GetInt(infoNode, "bulletSpeed");
            if (bulletSpeed > 0)
            {
                if (skill.SummonAttackProjectileSpeed <= 0)
                {
                    skill.SummonAttackProjectileSpeed = bulletSpeed;
                }

                if (!string.IsNullOrWhiteSpace(branchName))
                {
                    skill.SummonAttackProjectileSpeedByBranch[branchName] = bulletSpeed;
                }
            }

            WzImageProperty rangeNode = infoNode["range"];
            if (rangeNode == null)
            {
                return;
            }

            Point? lt = GetVector(rangeNode, "lt");
            Point? rb = GetVector(rangeNode, "rb");
            if (lt.HasValue || rb.HasValue)
            {
                if (skill.SummonAttackRangeLeft <= 0
                    && skill.SummonAttackRangeRight <= 0
                    && skill.SummonAttackRangeTop == 0
                    && skill.SummonAttackRangeBottom == 0)
                {
                    skill.SummonAttackRangeLeft = Math.Abs(lt?.X ?? 0);
                    skill.SummonAttackRangeRight = rb?.X ?? 0;
                    skill.SummonAttackRangeTop = lt?.Y ?? 0;
                    skill.SummonAttackRangeBottom = rb?.Y ?? 0;
                }

                if (!string.IsNullOrWhiteSpace(branchName))
                {
                    skill.SummonNamedRangeMetadata[branchName] = new SkillData.SummonRangeMetadata(
                        Math.Abs(lt?.X ?? 0),
                        rb?.X ?? 0,
                        lt?.Y ?? 0,
                        rb?.Y ?? 0);
                }
            }

            Point? center = GetVector(rangeNode, "sp");
            int radius = GetInt(rangeNode, "r");
            if (center.HasValue && radius > 0)
            {
                if (!skill.SummonAttackCenterOffset.HasValue && skill.SummonAttackRadius <= 0)
                {
                    skill.SummonAttackCenterOffset = center.Value;
                    skill.SummonAttackRadius = radius;
                }

                if (!string.IsNullOrWhiteSpace(branchName))
                {
                    skill.SummonNamedAttackCenterOffsets[branchName] = center.Value;
                    skill.SummonNamedAttackRadii[branchName] = radius;
                }
            }
        }

        private void RegisterSupplementalSummonAttackMetadata(SkillData skill, WzImageProperty summonNode, string branchName)
        {
            if (skill == null
                || summonNode == null
                || string.IsNullOrWhiteSpace(branchName)
                || !branchName.StartsWith("attack", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            WzImageProperty attackBranch = summonNode[branchName];
            if (attackBranch == null)
            {
                return;
            }

            PopulateSummonAttackMetadata(skill, attackBranch, branchName);
            RegisterSummonBranchImpactMetadata(
                skill,
                branchName,
                BuildSummonBranchProjectileAnimations(
                    skill.SummonProjectileAnimations,
                    LoadSummonIndexedAnimations(attackBranch["info"]?["ball"], $"{branchName}/info/ball")),
                BuildSummonBranchImpactPresentations(
                    skill.SummonTargetHitPresentations,
                    LoadSummonImpactPresentations(attackBranch["info"]?["mob"], $"{branchName}/info/mob")));
        }

        private static void PopulateSummonHitTimingMetadata(SkillData skill, string attackBranchName, WzImageProperty hitNode)
        {
            if (skill == null || hitNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in hitNode.WzProperties)
            {
                if (!int.TryParse(child.Name, out _))
                {
                    continue;
                }

                int hitAfter = GetInt(child, "hitAfter");
                if (hitAfter > 0)
                {
                    skill.SummonAttackHitDelayMs = hitAfter;
                    return;
                }
            }

            if (attackBranchName.StartsWith("die", StringComparison.OrdinalIgnoreCase)
                && skill.SummonAttackAnimation?.TotalDuration > 0)
            {
                skill.SummonAttackHitDelayMs = skill.SummonAttackAnimation.TotalDuration;
            }
        }

        private SkillAnimation LoadSummonHitAnimation(SkillData skill, WzImageProperty hitNode, out string actionKey)
        {
            actionKey = null;
            if (hitNode == null)
            {
                return null;
            }

            if (hitNode.WzProperties.OfType<WzCanvasProperty>().Any())
            {
                SkillAnimation directAnimation = GetOrLoadSummonActionAnimation(skill, hitNode, "hit");
                actionKey = directAnimation.Frames.Count > 0 ? "hit" : null;
                return directAnimation.Frames.Count > 0 ? directAnimation : null;
            }

            foreach (WzImageProperty child in hitNode.WzProperties)
            {
                if (child == null)
                {
                    continue;
                }

                if (int.TryParse(child.Name, out _))
                {
                    if (child.WzProperties.OfType<WzCanvasProperty>().Any())
                    {
                        SkillAnimation indexedAnimation = GetOrLoadSummonActionAnimation(
                            skill,
                            child,
                            $"hit/{child.Name}",
                            $"hit/{child.Name}");
                        if (indexedAnimation.Frames.Count > 0)
                        {
                            actionKey = $"hit/{child.Name}";
                            return indexedAnimation;
                        }
                    }
                    else
                    {
                        foreach (WzImageProperty nestedChild in child.WzProperties)
                        {
                            if (nestedChild == null || !int.TryParse(nestedChild.Name, out _))
                            {
                                continue;
                            }

                            SkillAnimation nestedAnimation = GetOrLoadSummonActionAnimation(
                                skill,
                                nestedChild,
                                $"hit/{child.Name}/{nestedChild.Name}",
                                $"hit/{child.Name}/{nestedChild.Name}");
                            if (nestedAnimation.Frames.Count > 0)
                            {
                                actionKey = $"hit/{child.Name}/{nestedChild.Name}";
                                return nestedAnimation;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private List<SummonImpactPresentation> LoadSummonHitTargetAnimations(WzImageProperty hitNode, string baseName)
        {
            var animations = new List<SummonImpactPresentation>();
            if (hitNode == null)
            {
                return animations;
            }

            if (hitNode.WzProperties.OfType<WzCanvasProperty>().Any()
                && HasSummonHitTargetPresentationMetadata(hitNode))
            {
                TryAddSummonImpactPresentation(animations, hitNode, baseName);
                return animations;
            }

            foreach (WzImageProperty child in hitNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out _))
                {
                    continue;
                }

                bool includeChild = HasSummonHitTargetPresentationMetadata(child);
                if (child.WzProperties.OfType<WzCanvasProperty>().Any())
                {
                    if (includeChild)
                    {
                        TryAddSummonImpactPresentation(animations, child, $"{baseName}/{child.Name}");
                    }

                    continue;
                }

                foreach (WzImageProperty nestedChild in child.WzProperties)
                {
                    if (nestedChild == null
                        || !int.TryParse(nestedChild.Name, out _)
                        || !nestedChild.WzProperties.OfType<WzCanvasProperty>().Any())
                    {
                        continue;
                    }

                    if (includeChild || HasSummonHitTargetPresentationMetadata(nestedChild))
                    {
                        TryAddSummonImpactPresentation(
                            animations,
                            nestedChild,
                            $"{baseName}/{child.Name}/{nestedChild.Name}");
                    }
                }
            }

            return animations;
        }

        private static bool HasSummonHitTargetPresentationMetadata(WzImageProperty node)
        {
            return node != null
                && (GetInt(node, "hitAfter") > 0
                    || node["pos"] != null);
        }

        private void TryAddSummonImpactPresentation(List<SummonImpactPresentation> animations, WzImageProperty node, string animationName)
        {
            if (animations == null || node == null)
            {
                return;
            }

            SkillAnimation animation = LoadSkillAnimation(node, animationName);
            if (animation.Frames.Count <= 0)
            {
                return;
            }

            if (animation.Frames.Count > 1)
            {
                animation.Loop = true;
            }

            animations.Add(new SummonImpactPresentation
            {
                Animation = animation,
                HitAfterMs = Math.Max(0, GetInt(node, "hitAfter")),
                PositionCode = node["pos"] != null ? (int?)GetInt(node, "pos") : null
            });
        }

        private List<SummonImpactPresentation> LoadSummonImpactPresentations(WzImageProperty rootNode, string baseName)
        {
            var animations = new List<SummonImpactPresentation>();
            if (rootNode == null)
            {
                return animations;
            }

            if (rootNode.WzProperties.OfType<WzCanvasProperty>().Any())
            {
                TryAddSummonImpactPresentation(animations, rootNode, baseName);
                return animations;
            }

            foreach (WzImageProperty child in rootNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out _))
                {
                    continue;
                }

                TryAddSummonImpactPresentation(animations, child, $"{baseName}/{child.Name}");
            }

            return animations;
        }

        private List<SkillAnimation> LoadSummonIndexedAnimations(WzImageProperty rootNode, string baseName)
        {
            var animations = new List<SkillAnimation>();
            if (rootNode == null)
            {
                return animations;
            }

            if (rootNode.WzProperties.OfType<WzCanvasProperty>().Any())
            {
                SkillAnimation directAnimation = LoadSkillAnimation(rootNode, baseName);
                if (directAnimation.Frames.Count > 0)
                {
                    if (directAnimation.Frames.Count > 1)
                    {
                        directAnimation.Loop = true;
                    }

                    animations.Add(directAnimation);
                }

                return animations;
            }

            foreach (WzImageProperty child in rootNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out _))
                {
                    continue;
                }

                SkillAnimation animation = LoadSkillAnimation(child, $"{baseName}/{child.Name}");
                if (animation.Frames.Count <= 0)
                {
                    continue;
                }

                if (animation.Frames.Count > 1)
                {
                    animation.Loop = true;
                }

                animations.Add(animation);
            }

            return animations;
        }

        private List<string> LoadSummonIndexedAnimationPaths(WzImageProperty rootNode)
        {
            var paths = new List<string>();
            if (rootNode == null)
            {
                return paths;
            }

            if (rootNode.WzProperties.OfType<WzCanvasProperty>().Any())
            {
                SkillAnimation directAnimation = LoadSkillAnimation(rootNode, rootNode.Name);
                if (directAnimation?.Frames.Count > 0)
                {
                    paths.Add(NormalizeSkillAssetPath(rootNode));
                }

                return paths;
            }

            foreach (WzImageProperty child in rootNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out _))
                {
                    continue;
                }

                SkillAnimation animation = LoadSkillAnimation(child, child.Name);
                if (animation?.Frames.Count <= 0)
                {
                    continue;
                }

                paths.Add(NormalizeSkillAssetPath(child));
            }

            return paths;
        }

        private static void AppendSummonImpactPresentations(List<SummonImpactPresentation> destination, IEnumerable<SummonImpactPresentation> source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            foreach (SummonImpactPresentation presentation in source)
            {
                if (presentation?.Animation?.Frames.Count <= 0)
                {
                    continue;
                }

                bool alreadyLoaded = destination.Any(existing =>
                    existing?.Animation != null
                    && string.Equals(existing.Animation.Name, presentation.Animation.Name, StringComparison.OrdinalIgnoreCase));
                if (!alreadyLoaded)
                {
                    destination.Add(presentation);
                }
            }
        }

        private static List<SkillAnimation> BuildSummonBranchProjectileAnimations(
            IEnumerable<SkillAnimation> baseAnimations,
            IEnumerable<SkillAnimation> overrideAnimations)
        {
            var resolved = new List<SkillAnimation>();
            AppendSummonIndexedAnimations(resolved, baseAnimations);
            AppendSummonIndexedAnimations(resolved, overrideAnimations);
            return resolved;
        }

        private static List<SummonImpactPresentation> BuildSummonBranchImpactPresentations(
            IEnumerable<SummonImpactPresentation> basePresentations,
            IEnumerable<SummonImpactPresentation> overridePresentations)
        {
            var resolved = new List<SummonImpactPresentation>();
            AppendSummonImpactPresentations(resolved, basePresentations);
            AppendSummonImpactPresentations(resolved, overridePresentations);
            return resolved;
        }

        private static void RegisterSummonBranchImpactMetadata(
            SkillData skill,
            string branchName,
            List<SkillAnimation> projectileAnimations,
            List<SummonImpactPresentation> impactPresentations)
        {
            if (skill == null || string.IsNullOrWhiteSpace(branchName))
            {
                return;
            }

            if (projectileAnimations?.Count > 0)
            {
                skill.SummonProjectileAnimationsByBranch[branchName] = projectileAnimations;
            }

            if (impactPresentations?.Count > 0)
            {
                skill.SummonTargetHitPresentationsByBranch[branchName] = impactPresentations;
            }
        }

        private static void AppendSummonIndexedAnimations(List<SkillAnimation> destination, IEnumerable<SkillAnimation> source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            foreach (SkillAnimation animation in source)
            {
                if (animation?.Frames.Count <= 0)
                {
                    continue;
                }

                bool alreadyLoaded = destination.Any(existing =>
                    existing != null
                    && string.Equals(existing.Name, animation.Name, StringComparison.OrdinalIgnoreCase));
                if (!alreadyLoaded)
                {
                    destination.Add(animation);
                }
            }
        }

        private static void PopulateSummonSupportMetadata(SkillData skill, string supportBranchName, WzImageProperty supportBranch)
        {
            if (skill == null || supportBranch == null)
            {
                return;
            }

            WzImageProperty infoNode = supportBranch["info"];
            if (infoNode == null)
            {
                return;
            }

            WzImageProperty rangeNode = infoNode["range"];
            if (rangeNode == null)
            {
                return;
            }

            Point? lt = GetVector(rangeNode, "lt");
            Point? rb = GetVector(rangeNode, "rb");
            if (!lt.HasValue && !rb.HasValue)
            {
                return;
            }

            skill.SummonAttackRangeLeft = Math.Abs(lt?.X ?? 0);
            skill.SummonAttackRangeRight = rb?.X ?? 0;
            skill.SummonAttackRangeTop = lt?.Y ?? 0;
            skill.SummonAttackRangeBottom = rb?.Y ?? 0;
            if (!string.IsNullOrWhiteSpace(supportBranchName))
            {
                skill.SummonNamedRangeMetadata[supportBranchName] = new SkillData.SummonRangeMetadata(
                    Math.Abs(lt?.X ?? 0),
                    rb?.X ?? 0,
                    lt?.Y ?? 0,
                    rb?.Y ?? 0);
            }
        }

        public static string SelectPreferredSummonSpawnBranch(IEnumerable<string> branchNames)
        {
            return SelectExactSummonBranch(branchNames, PreferredSummonSpawnBranches);
        }

        public static string SelectPreferredSummonIdleBranch(IEnumerable<string> branchNames)
        {
            return SelectExactSummonBranch(branchNames, PreferredSummonAnimationBranches);
        }

        public static string SelectPreferredSummonSupportBranch(IEnumerable<string> branchNames)
        {
            return SelectExactSummonBranch(branchNames, new[] { "heal", "support", "stand" });
        }

        private static int[] ParseDummySkillParents(string dummyOf)
        {
            return ParseLinkedSkillIds(dummyOf);
        }

        internal static int[] ParseLinkedSkillIds(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<int>();
            }

            return value
                .Split(new[] { '&', '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => int.TryParse(token, out _))
                .Select(int.Parse)
                .Distinct()
                .ToArray();
        }

        internal static int[] ParseLinkedSkillIds(WzImageProperty property)
        {
            if (property == null)
            {
                return Array.Empty<int>();
            }

            HashSet<int> linkedSkillIds = new();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child != null && int.TryParse(child.Name, out int linkedSkillId) && linkedSkillId > 0)
                {
                    linkedSkillIds.Add(linkedSkillId);
                }
            }

            return linkedSkillIds.Count == 0
                ? Array.Empty<int>()
                : linkedSkillIds.OrderBy(id => id).ToArray();
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

            foreach (string availableBranch in availableBranches.Keys)
            {
                if (availableBranch.StartsWith("attack", StringComparison.OrdinalIgnoreCase))
                {
                    return availableBranch;
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

        public static bool ShouldHidePersistentAvatarEffectOnRotateAction(IEnumerable<string> branchNames, bool suddenDeath)
        {
            if (!HasPersistentAvatarEffectBranches(branchNames, suddenDeath))
            {
                return false;
            }

            if (suddenDeath)
            {
                return true;
            }

            if (branchNames == null)
            {
                return false;
            }

            var availableBranches = new HashSet<string>(
                branchNames.Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);

            foreach (string excludedBranch in MoreWildPersistentAvatarEffectBranches)
            {
                if (availableBranches.Contains(excludedBranch))
                {
                    return false;
                }
            }

            return true;
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

        private static string SelectExactSummonBranch(IEnumerable<string> branchNames, IEnumerable<string> preferredBranches)
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

            return null;
        }

        private static string SelectFallbackSummonIdleBranch(
            WzImageProperty summonNode,
            IEnumerable<string> branchNames,
            bool standaloneSummonRoot)
        {
            string preferredFallback = SelectPreferredSummonBranch(branchNames, new[] { "attack1", "attack", "die" });
            if (!string.IsNullOrWhiteSpace(preferredFallback))
            {
                return preferredFallback;
            }

            if (summonNode == null)
            {
                return null;
            }

            foreach (WzImageProperty child in summonNode.WzProperties)
            {
                if (child == null
                    || string.IsNullOrWhiteSpace(child.Name)
                    || NonActionSummonBranchNames.Contains(child.Name)
                    || (standaloneSummonRoot && ShouldSkipStandaloneSummonWrapperBranchName(child.Name))
                    || string.Equals(child.Name, "hit", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsSummonActionBranchProperty(child))
                {
                    return child.Name;
                }
            }

            return null;
        }

        internal static bool TryResolveSummonSourcePropertyFromSkillNode(
            WzImageProperty skillNode,
            out WzImageProperty summonNode)
        {
            summonNode = ResolveLinkedProperty(skillNode?["summon"]);
            if (LooksLikeSummonSourceProperty(summonNode))
            {
                return true;
            }

            if (LooksLikeStandaloneSummonSourceProperty(skillNode))
            {
                summonNode = skillNode;
                return true;
            }

            summonNode = null;
            return false;
        }

        internal static bool LooksLikeStandaloneSummonSourceProperty(WzImageProperty skillNode)
        {
            if (!HasPotentialSummonOwnerMetadata(skillNode)
                || !LooksLikeSummonSourceProperty(skillNode))
            {
                return false;
            }

            return skillNode.WzProperties.Any(IsStandaloneSummonSignatureBranch);
        }

        private static bool IsStandaloneSummonSignatureBranch(WzImageProperty child)
        {
            if (child == null || string.IsNullOrWhiteSpace(child.Name))
            {
                return false;
            }

            if (IsStandaloneSummonSignatureBranchName(child.Name))
            {
                return true;
            }

            if (ShouldSkipStandaloneSummonWrapperBranchName(child.Name))
            {
                return false;
            }

            return IsSummonActionBranchProperty(child);
        }

        internal static bool HasPotentialSummonOwnerMetadata(WzImageProperty skillNode)
        {
            if (skillNode == null)
            {
                return false;
            }

            WzImageProperty infoNode = skillNode["info"];
            return GetInt(infoNode, "type") == 33
                   || HasProperty(infoNode, "minionAbility")
                   || HasProperty(infoNode, "condition")
                   || HasProperty(infoNode, "affectedSkill")
                   || HasProperty(infoNode, "selfDestructMinion")
                   || HasProperty(skillNode["common"], "time")
                   || HasProperty(skillNode["common"], "subTime");
        }

        internal static bool LooksLikeSummonSourceProperty(WzImageProperty node)
        {
            if (node == null)
            {
                return false;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null || string.IsNullOrWhiteSpace(child.Name))
                {
                    continue;
                }

                if (NonActionSummonBranchNames.Contains(child.Name))
                {
                    continue;
                }

                if (IsRecognizedSummonActionBranchName(child.Name)
                    || IsSummonActionBranchProperty(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRecognizedSummonActionBranchName(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return false;
            }

            return string.Equals(branchName, "prepare", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "hit", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "summon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "create", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "summoned", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "stand", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "fly", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "move", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "walk", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "heal", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "support", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "die", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "die1", StringComparison.OrdinalIgnoreCase)
                   || IsRepeatStyleSummonBranchName(branchName)
                   || branchName.StartsWith("attack", StringComparison.OrdinalIgnoreCase)
                   || branchName.StartsWith("skill", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStandaloneSummonSignatureBranchName(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return false;
            }

            return string.Equals(branchName, "prepare", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "hit", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "summon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "create", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "summoned", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "stand", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "fly", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "move", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "walk", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "heal", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "support", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "die", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "die1", StringComparison.OrdinalIgnoreCase)
                   || IsRepeatStyleSummonBranchName(branchName);
        }

        internal static bool ShouldSkipStandaloneSummonWrapperBranchName(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
            {
                return true;
            }

            return string.Equals(branchName, "action", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "effect", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "effect0", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "icon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "iconMouseOver", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "iconDisabled", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "common", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "PVPcommon", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "info", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "invisible", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(branchName, "weapon", StringComparison.OrdinalIgnoreCase)
                   || NonActionSummonBranchNames.Contains(branchName);
        }

        private SkillFrame LoadSkillFrame(WzImageProperty frameNode)
        {
            return LoadSkillFrame(frameNode, 100, false);
        }

        private SkillAnimation LoadItemBulletAnimationInternal(int itemId)
        {
            if (!UI.InventoryItemMetadataResolver.TryResolveImageSource(itemId, out string category, out string imagePath))
            {
                return null;
            }

            WzImage itemImage = global::HaCreator.Program.FindImage(category, imagePath);
            if (itemImage == null)
            {
                return null;
            }

            itemImage.ParseImage();
            string itemText = category == "Character"
                ? itemId.ToString("D8", CultureInfo.InvariantCulture)
                : itemId.ToString("D7", CultureInfo.InvariantCulture);
            if (itemImage[itemText] is not WzSubProperty itemProperty
                || itemProperty["bullet"] is not WzImageProperty bulletProperty)
            {
                return null;
            }

            var animation = new SkillAnimation();
            if (bulletProperty is WzCanvasProperty bulletCanvas)
            {
                SkillFrame frame = LoadSkillFrame(bulletCanvas, 60, false);
                if (frame != null)
                {
                    animation.Frames.Add(frame);
                }

                return animation.Frames.Count > 0 ? animation : null;
            }

            IEnumerable<WzImageProperty> orderedFrames = bulletProperty.WzProperties
                .OrderBy(static property =>
                    int.TryParse(property?.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                        ? index
                        : int.MaxValue)
                .ThenBy(static property => property?.Name, StringComparer.OrdinalIgnoreCase);
            foreach (WzImageProperty frameNode in orderedFrames)
            {
                SkillFrame frame = LoadSkillFrame(frameNode, 60, false);
                if (frame != null)
                {
                    animation.Frames.Add(frame);
                }
            }

            return animation.Frames.Count > 0 ? animation : null;
        }

        private SkillFrame LoadSummonActionFrame(WzImageProperty frameNode)
        {
            return LoadSkillFrame(frameNode, ClientSummonedFrameDelayFallbackMs, true);
        }

        private SkillFrame LoadSkillFrame(WzImageProperty frameNode, int defaultDelay, bool useMetadataBounds)
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
            WzImageProperty metadataNode = ResolveFrameMetadataProperty(frameNode, canvas);
            Point origin = ResolveFrameOrigin(metadataNode, canvas);

            // Get bitmap and create texture
            var bitmap = canvas.GetLinkedWzCanvasBitmap();
            if (bitmap != null)
            {
                var texture = bitmap.ToTexture2DAndDispose(_device);
                if (texture != null)
                {
                    frame.Texture = new DXObject(0, 0, texture)
                    {
                        Tag = canvas.FullPath
                    };
                    frame.Origin = origin;
                    frame.Bounds = useMetadataBounds
                        ? ResolveFrameBounds(metadataNode, canvas, frame.Texture, origin)
                        : new Rectangle(0, 0, texture.Width, texture.Height);
                }
            }

            // Get delay
            frame.Delay = ResolveFrameInt(metadataNode, canvas, "delay", defaultDelay);

            // Get flip
            frame.Flip = ResolveFrameInt(metadataNode, canvas, "flip") == 1;
            frame.Z = ResolveFrameInt(metadataNode, canvas, "z");

            // Shadow-partner and companion layers can carry authored alpha ramps per frame.
            frame.AlphaStart = Math.Clamp(ResolveFrameInt(metadataNode, canvas, "a0", 255), 0, 255);
            frame.AlphaEnd = Math.Clamp(ResolveFrameInt(metadataNode, canvas, "a1", 255), 0, 255);

            return frame;
        }

        private static WzImageProperty ResolveLinkedProperty(WzImageProperty property)
        {
            return property?.GetLinkedWzImageProperty() ?? property;
        }

        private static WzImageProperty ResolveFrameMetadataProperty(WzImageProperty frameNode, WzCanvasProperty canvas)
        {
            if (frameNode != null
                && (frameNode is WzCanvasProperty
                    || frameNode["origin"] != null
                    || frameNode["delay"] != null
                    || frameNode["lt"] != null
                    || frameNode["rb"] != null
                    || frameNode["z"] != null))
            {
                return frameNode;
            }

            return canvas;
        }

        private static int ResolveFrameInt(WzImageProperty metadataNode, WzCanvasProperty canvas, string propertyName, int defaultValue = 0)
        {
            if (metadataNode != null && metadataNode[propertyName] != null)
            {
                return GetInt(metadataNode, propertyName, defaultValue);
            }

            return canvas != null ? GetInt(canvas, propertyName, defaultValue) : defaultValue;
        }

        private static Point ResolveFrameOrigin(WzImageProperty metadataNode, WzCanvasProperty canvas)
        {
            Point? metadataOrigin = GetVector(metadataNode, "origin");
            if (metadataOrigin.HasValue)
            {
                return metadataOrigin.Value;
            }

            if (canvas?.GetCanvasOriginPosition() is System.Drawing.PointF canvasOrigin)
            {
                return new Point((int)canvasOrigin.X, (int)canvasOrigin.Y);
            }

            return Point.Zero;
        }

        private static Rectangle ResolveFrameBounds(WzImageProperty metadataNode, WzCanvasProperty canvas, IDXObject texture, Point origin)
        {
            Point? lt = GetVector(metadataNode, "lt") ?? GetVector(canvas, "lt");
            Point? rb = GetVector(metadataNode, "rb") ?? GetVector(canvas, "rb");
            if (lt.HasValue && rb.HasValue)
            {
                int left = lt.Value.X;
                int top = lt.Value.Y;
                int width = Math.Max(1, rb.Value.X - left);
                int height = Math.Max(1, rb.Value.Y - top);
                return new Rectangle(left, top, width, height);
            }

            return new Rectangle(-origin.X, -origin.Y, texture?.Width ?? 0, texture?.Height ?? 0);
        }

        internal static bool IsRepeatStyleSummonBranchName(string actionKey)
        {
            return !string.IsNullOrWhiteSpace(actionKey)
                   && actionKey.StartsWith("repeat", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSummonActionBranchProperty(WzImageProperty node)
        {
            if (node == null)
            {
                return false;
            }

            if (node is WzCanvasProperty || node is WzUOLProperty)
            {
                return true;
            }

            foreach (WzImageProperty child in node.WzProperties)
            {
                if (child == null)
                {
                    continue;
                }

                if (child is WzCanvasProperty || child is WzUOLProperty)
                {
                    return true;
                }

                if (!int.TryParse(child.Name, out _))
                {
                    continue;
                }

                if (child.WzProperties.OfType<WzCanvasProperty>().Any()
                    || child.WzProperties.OfType<WzUOLProperty>().Any())
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ShouldAppendReversedSummonFrames(WzImageProperty actionNode, string actionKey)
        {
            if (actionNode == null)
            {
                return IsRepeatStyleSummonBranchName(actionKey);
            }

            string reverseFlagName = MapleStoryStringPool.GetOrFallback(
                ClientSummonReversePlaybackStringPoolId,
                ClientSummonReversePlaybackFallbackName);
            if (HasProperty(actionNode, reverseFlagName))
            {
                return GetInt(actionNode, reverseFlagName) != 0;
            }

            if (IsRepeatStyleSummonBranchName(actionKey))
            {
                return true;
            }

            return GetInt(actionNode, "reverse") != 0
                || GetInt(actionNode, "repeat") != 0
                || GetInt(actionNode, "r") != 0;
        }

        private static void AppendReversedSummonFrames(SkillAnimation animation)
        {
            if (animation?.Frames == null || animation.Frames.Count == 0)
            {
                return;
            }

            SkillFrame[] reversedFrames = animation.Frames.ToArray();
            for (int i = reversedFrames.Length - 1; i >= 0; i--)
            {
                animation.Frames.Add(reversedFrames[i]);
            }

            animation.CalculateDuration();
        }

        private ProjectileData LoadProjectile(int skillId, WzImageProperty ballNode, WzImageProperty skillNode)
        {
            var projectile = new ProjectileData
            {
                SkillId = skillId
            };

            // Load ball animation
            projectile.Animation = LoadSkillAnimation(ballNode, "ball");
            projectile.AnimationPath = NormalizeSkillAssetPath(ballNode);
            projectile.VariantAnimations = LoadSummonIndexedAnimations(ballNode, "ball");
            projectile.VariantAnimationPaths = LoadSummonIndexedAnimationPaths(ballNode);
            if (projectile.Animation?.Frames.Count <= 0)
            {
                projectile.Animation = projectile.ResolveAnimationVariant(level: 1);
                projectile.AnimationPath = projectile.VariantAnimationPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                    ?? projectile.AnimationPath;
            }

            PopulateProjectileCharacterLevelVariants(projectile, skillNode?["CharLevel"]);
            PopulateProjectileLevelVariants(projectile, skillNode?["level"]);

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
                projectile.Homing = true;
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

        private void PopulateProjectileCharacterLevelVariants(ProjectileData projectile, WzImageProperty charLevelNode)
        {
            if (projectile == null || charLevelNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in charLevelNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int requiredLevel))
                {
                    continue;
                }

                WzImageProperty ballVariantNode = child["ball"];
                if (ballVariantNode == null)
                {
                    continue;
                }

                List<SkillAnimation> variants = LoadSummonIndexedAnimations(
                    ballVariantNode,
                    $"ball/CharLevel/{requiredLevel}");
                if (variants.Count > 0)
                {
                    projectile.CharacterLevelVariantAnimations[requiredLevel] = variants;
                    projectile.CharacterLevelVariantAnimationPaths[requiredLevel] = LoadSummonIndexedAnimationPaths(ballVariantNode);
                }
            }
        }

        private void PopulateProjectileLevelVariants(ProjectileData projectile, WzImageProperty levelNode)
        {
            if (projectile == null || levelNode == null)
            {
                return;
            }

            foreach (WzImageProperty child in levelNode.WzProperties)
            {
                if (child == null || !int.TryParse(child.Name, out int skillLevel))
                {
                    continue;
                }

                WzImageProperty ballVariantNode = child["ball"];
                if (ballVariantNode == null)
                {
                    continue;
                }

                List<SkillAnimation> variants = LoadSummonIndexedAnimations(
                    ballVariantNode,
                    $"ball/level/{skillLevel}");
                if (variants.Count > 0)
                {
                    projectile.LevelVariantAnimations[skillLevel] = variants;
                    projectile.LevelVariantAnimationPaths[skillLevel] = LoadSummonIndexedAnimationPaths(ballVariantNode);
                }
            }
        }

        private static string NormalizeSkillAssetPath(WzImageProperty property)
        {
            string fullPath = property?.FullPath;
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                return null;
            }

            string normalizedPath = fullPath.Replace('\\', '/').TrimStart('/');
            return normalizedPath.StartsWith("Skill/", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath
                : $"Skill/{normalizedPath}";
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

            MarkSwallowFamilySkills(result);
            return result;
        }

        internal static void MarkSwallowFamilySkills(IList<SkillData> skills)
        {
            if (skills == null || skills.Count == 0)
            {
                return;
            }

            Dictionary<int, SkillData> skillsById = skills
                .Where(static skill => skill != null)
                .GroupBy(static skill => skill.SkillId)
                .ToDictionary(static group => group.Key, static group => group.First());
            SkillData[] swallowRoots = skillsById.Values
                .Where(IsExplicitSwallowFamilyRoot)
                .ToArray();
            HashSet<int> swallowSkillIds = new(swallowRoots.Select(static skill => skill.SkillId));

            foreach (SkillData root in swallowRoots)
            {
                int[] linkedDummySkillIds = root.DummySkillParents;
                if (linkedDummySkillIds == null || linkedDummySkillIds.Length == 0)
                {
                    continue;
                }

                foreach (int linkedSkillId in linkedDummySkillIds)
                {
                    if (linkedSkillId > 0 && skillsById.ContainsKey(linkedSkillId))
                    {
                        swallowSkillIds.Add(linkedSkillId);
                    }
                }
            }

            foreach (SkillData skill in skillsById.Values)
            {
                skill.IsSwallowFamilySkill = swallowSkillIds.Contains(skill.SkillId);
            }
        }

        private static bool IsExplicitSwallowFamilyRoot(SkillData skill)
        {
            if (skill == null
                || skill.DummySkillParents == null
                || skill.DummySkillParents.Length == 0)
            {
                return false;
            }

            foreach (string actionName in EnumerateSwallowFamilyActionNames(skill))
            {
                if (IsSwallowFamilyActionName(actionName))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateSwallowFamilyActionNames(SkillData skill)
        {
            if (!string.IsNullOrWhiteSpace(skill?.PrepareActionName))
            {
                yield return skill.PrepareActionName;
            }

            if (skill?.ActionNames != null)
            {
                foreach (string actionName in skill.ActionNames)
                {
                    if (!string.IsNullOrWhiteSpace(actionName))
                    {
                        yield return actionName;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(skill?.ActionName))
            {
                yield return skill.ActionName;
            }

            if (!string.IsNullOrWhiteSpace(skill?.KeydownActionName))
            {
                yield return skill.KeydownActionName;
            }
        }

        private static bool IsSwallowFamilyActionName(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName)
                   && (string.Equals(actionName, "swallow", StringComparison.OrdinalIgnoreCase)
                       || actionName.StartsWith("swallow_", StringComparison.OrdinalIgnoreCase));
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

        private static IReadOnlyList<string> GetActionNames(WzImageProperty skillNode)
        {
            var actionNode = skillNode["action"];
            if (actionNode == null)
            {
                return Array.Empty<string>();
            }

            var actionNames = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string preferredName in new[]
            {
                GetString(actionNode, "0"),
                GetString(actionNode, "action")
            })
            {
                if (!string.IsNullOrWhiteSpace(preferredName) && seen.Add(preferredName.Trim()))
                {
                    actionNames.Add(preferredName.Trim());
                }
            }

            foreach (WzImageProperty property in actionNode.WzProperties)
            {
                string actionName = GetPropertyStringValue(property);
                if (!string.IsNullOrWhiteSpace(actionName) && seen.Add(actionName.Trim()))
                {
                    actionNames.Add(actionName.Trim());
                }
            }

            return actionNames;
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
            int normalizedJob = Math.Abs(job);
            if (TryBuildSpecialJobPath(normalizedJob, out List<int> specialPath))
            {
                return specialPath;
            }

            var path = new List<int> { 0 };

            if (normalizedJob == 0)
            {
                return path;
            }

            int firstJob = (normalizedJob / 100) * 100;
            if (firstJob > 0)
            {
                path.Add(firstJob);
            }

            int secondJob = (normalizedJob / 10) * 10;
            if (secondJob > firstJob)
            {
                path.Add(secondJob);
            }

            int thirdJob = secondJob + (normalizedJob % 10 > 0 ? 1 : 0);
            if (thirdJob > secondJob && thirdJob < normalizedJob)
            {
                path.Add(thirdJob);
            }

            if (!path.Contains(normalizedJob))
            {
                path.Add(normalizedJob);
            }

            return path;
        }

        private static bool TryBuildSpecialJobPath(int job, out List<int> path)
        {
            path = job switch
            {
                2000 or >= 2100 and <= 2112 => BuildLineagePath(job, 2000, 2100, 2110, 2111, 2112),
                2001 or >= 2200 and <= 2218 => BuildLineagePath(job, 2001, 2200, 2210, 2211, 2212, 2213, 2214, 2215, 2216, 2217, 2218),
                2002 or >= 2300 and <= 2312 => BuildLineagePath(job, 2002, 2300, 2310, 2311, 2312),
                2003 or >= 2400 and <= 2412 => BuildLineagePath(job, 2003, 2400, 2410, 2411, 2412),
                2004 or >= 2700 and <= 2712 => BuildLineagePath(job, 2004, 2700, 2710, 2711, 2712),
                2005 or >= 2500 and <= 2512 => BuildLineagePath(job, 2005, 2500, 2510, 2511, 2512),
                1000 or >= 1100 and <= 1112 => BuildLineagePath(job, 1000, 1100, 1110, 1111, 1112),
                >= 1200 and <= 1212 => BuildLineagePath(job, 1000, 1200, 1210, 1211, 1212),
                >= 1300 and <= 1312 => BuildLineagePath(job, 1000, 1300, 1310, 1311, 1312),
                >= 1400 and <= 1412 => BuildLineagePath(job, 1000, 1400, 1410, 1411, 1412),
                >= 1500 and <= 1512 => BuildLineagePath(job, 1000, 1500, 1510, 1511, 1512),
                3000 or >= 3200 and <= 3212 => BuildLineagePath(job, 3000, 3200, 3210, 3211, 3212),
                >= 3300 and <= 3312 => BuildLineagePath(job, 3000, 3300, 3310, 3311, 3312),
                >= 3500 and <= 3512 => BuildLineagePath(job, 3000, 3500, 3510, 3511, 3512),
                3001 or >= 3100 and <= 3112 => BuildLineagePath(job, 3001, 3100, 3110, 3111, 3112),
                3002 or >= 3600 and <= 3612 => BuildLineagePath(job, 3000, 3002, 3600, 3610, 3611, 3612),
                4001 or >= 4100 and <= 4112 => BuildLineagePath(job, 4001, 4100, 4110, 4111, 4112),
                4002 or >= 4200 and <= 4212 => BuildLineagePath(job, 4002, 4200, 4210, 4211, 4212),
                5000 or >= 5100 and <= 5112 => BuildLineagePath(job, 5000, 5100, 5110, 5111, 5112),
                6000 or >= 6100 and <= 6112 => BuildLineagePath(job, 6000, 6100, 6110, 6111, 6112),
                6001 or >= 6500 and <= 6512 => BuildLineagePath(job, 6001, 6500, 6510, 6511, 6512),
                >= 430 and <= 434 => BuildLineagePath(job, 0, 400, 430, 431, 432, 433, 434),
                _ => null
            };

            return path != null;
        }

        private static List<int> BuildLineagePath(int currentJob, params int[] lineage)
        {
            var path = new List<int>(lineage.Length);
            foreach (int jobId in lineage)
            {
                path.Add(jobId);
                if (jobId == currentJob)
                {
                    break;
                }
            }

            if (!path.Contains(currentJob))
            {
                path.Add(currentJob);
            }

            return path;
        }

        #endregion

        #region Utility

        private static SkillLevelData CreateLevelData(SkillData skill, WzImageProperty node, int level)
        {
            var levelData = new SkillLevelData { Level = level };
            levelData.AuthoredPropertyOrder = GetDirectPropertyOrder(node);

            levelData.Damage = GetInt(node, "damage", 0, level);
            levelData.DotDamage = GetInt(node, "dot", 0, level);
            levelData.DotInterval = GetInt(node, "dotInterval", 0, level);
            levelData.DotTime = GetInt(node, "dotTime", 0, level);
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
            levelData.STR = GetInt(node, "str", 0, level);
            levelData.DEX = GetInt(node, "dex", 0, level);
            levelData.INT = GetInt(node, "int", 0, level);
            levelData.LUK = GetInt(node, "luk", 0, level);
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
            levelData.BulletConsume = GetInt(node, "bulletConsume", 0, level);
            levelData.ProjectileItemConsume = GetInt(node, "itemConsume", 0, level);
            levelData.BulletSpeed = GetInt(node, "bulletSpeed", 0, level);
            levelData.ProjectileSpawnDelaysMs = ParseProjectileSpawnDelays(node, level);

            levelData.Mastery = GetInt(node, "mastery", 0, level);
            levelData.CriticalRate = GetInt(node, "cr", 0, level);
            levelData.CriticalDamageMin = GetInt(node, "criticaldamageMin", 0, level);
            levelData.CriticalDamageMax = GetInt(node, "criticaldamageMax", 0, level);
            levelData.DamageReductionRate = GetInt(node, "damR", 0, level);
            levelData.BossDamageRate = GetInt(node, "bdR", 0, level);
            levelData.IgnoreDefenseRate = PreferPrimaryStat(
                GetInt(node, "ignoreMobpdpR", 0, level),
                GetInt(node, "ignoreMobDamR", 0, level));
            levelData.DefensePercent = GetInt(node, "pddR", 0, level);
            levelData.MagicDefensePercent = GetInt(node, "mddR", 0, level);
            levelData.AccuracyPercent = GetInt(node, "accR", 0, level);
            levelData.AvoidabilityPercent = GetInt(node, "evaR", 0, level);

            levelData.RequiredLevel = GetInt(node, "reqLevel", 0, level);
            NormalizePassiveStatAliases(skill, node, level, levelData);
            PopulateSkillLevelRequirements(levelData, node);

            return levelData;
        }

        private static List<string> GetDirectPropertyOrder(WzImageProperty node)
        {
            if (node?.WzProperties == null || node.WzProperties.Count == 0)
            {
                return new List<string>();
            }

            var propertyOrder = new List<string>(node.WzProperties.Count);
            foreach (WzImageProperty child in node.WzProperties)
            {
                if (!string.IsNullOrWhiteSpace(child?.Name))
                {
                    propertyOrder.Add(child.Name);
                }
            }

            return propertyOrder;
        }

        private static void NormalizePassiveStatAliases(SkillData skill, WzImageProperty node, int level, SkillLevelData levelData)
        {
            if (levelData == null || node == null)
                return;

            levelData.ACC = PreferPrimaryStat(levelData.ACC, GetInt(node, "ar", 0, level));
            levelData.ACC = PreferPrimaryStat(levelData.ACC, GetInt(node, "accX", 0, level));
            levelData.PAD = PreferPrimaryStat(levelData.PAD, GetInt(node, "padX", 0, level));
            levelData.MAD = PreferPrimaryStat(levelData.MAD, GetInt(node, "madX", 0, level));
            levelData.PDD = PreferPrimaryStat(levelData.PDD, GetInt(node, "pddX", 0, level));
            levelData.MDD = PreferPrimaryStat(levelData.MDD, GetInt(node, "mddX", 0, level));
            levelData.STR = PreferPrimaryStat(levelData.STR, GetInt(node, "strX", 0, level));
            levelData.DEX = PreferPrimaryStat(levelData.DEX, GetInt(node, "dexX", 0, level));
            levelData.INT = PreferPrimaryStat(levelData.INT, GetInt(node, "intX", 0, level));
            levelData.LUK = PreferPrimaryStat(levelData.LUK, GetInt(node, "lukX", 0, level));
            levelData.EVA = PreferPrimaryStat(levelData.EVA, GetInt(node, "er", 0, level));
            levelData.EVA = PreferPrimaryStat(levelData.EVA, GetInt(node, "evaX", 0, level));

            if (levelData.ACC == 0 && UsesAccuracyXAlias(skill, node))
            {
                levelData.ACC = GetInt(node, "x", 0, level);
            }

            if (levelData.PAD == 0 && UsesWeaponAttackXAlias(skill, node))
            {
                levelData.PAD = GetInt(node, "x", 0, level);
            }

            levelData.PAD = PreferPrimaryStat(levelData.PAD, GetInt(node, "indiePad", 0, level));
            levelData.MAD = PreferPrimaryStat(levelData.MAD, GetInt(node, "indieMad", 0, level));
            levelData.ACC = PreferPrimaryStat(levelData.ACC, GetInt(node, "indieAcc", 0, level));
            levelData.EVA = PreferPrimaryStat(levelData.EVA, GetInt(node, "indieEva", 0, level));
            levelData.Speed = PreferPrimaryStat(levelData.Speed, GetInt(node, "indieSpeed", 0, level));
            levelData.Jump = PreferPrimaryStat(levelData.Jump, GetInt(node, "indieJump", 0, level));
            levelData.EnhancedPAD = GetInt(node, "epad", 0, level);
            levelData.EnhancedMAD = GetInt(node, "emad", 0, level);
            levelData.EnhancedPDD = GetInt(node, "epdd", 0, level);
            levelData.EnhancedMDD = GetInt(node, "emdd", 0, level);
            levelData.EnhancedMaxHP = GetInt(node, "emhp", 0, level);
            levelData.EnhancedMaxMP = GetInt(node, "emmp", 0, level);
            levelData.IndieMaxHP = GetInt(node, "indieMhp", 0, level);
            levelData.IndieMaxMP = GetInt(node, "indieMmp", 0, level);
            levelData.IndieMaxHP = PreferPrimaryStat(levelData.IndieMaxHP, GetInt(node, "mhpX", 0, level));
            levelData.IndieMaxMP = PreferPrimaryStat(levelData.IndieMaxMP, GetInt(node, "mmpX", 0, level));
            levelData.MaxHPPercent = PreferPrimaryStat(GetInt(node, "mhpR", 0, level), GetInt(node, "indieMhpR", 0, level));
            levelData.MaxMPPercent = PreferPrimaryStat(GetInt(node, "mmpR", 0, level), GetInt(node, "indieMmpR", 0, level));
            levelData.DefensePercent = GetInt(node, "pddR", 0, level);
            levelData.MagicDefensePercent = GetInt(node, "mddR", 0, level);
            levelData.AccuracyPercent = GetInt(node, "accR", 0, level);
            levelData.AvoidabilityPercent = GetInt(node, "evaR", 0, level);
            levelData.AllStat = GetInt(node, "indieAllStat", 0, level);
            levelData.AbnormalStatusResistance = PreferPrimaryStat(GetInt(node, "asrR", 0, level), GetInt(node, "indieAsrR", 0, level));
            levelData.ElementalResistance = PreferPrimaryStat(GetInt(node, "terR", 0, level), GetInt(node, "indieTerR", 0, level));
            levelData.CriticalDamageMin = PreferPrimaryStat(levelData.CriticalDamageMin, GetInt(node, "criticalDamageMin", 0, level));
            levelData.CriticalDamageMax = PreferPrimaryStat(levelData.CriticalDamageMax, GetInt(node, "criticalDamageMax", 0, level));
            levelData.DamageReductionRate = PreferPrimaryStat(levelData.DamageReductionRate, GetInt(node, "indieDamR", 0, level));
            levelData.ExperienceRate = GetInt(node, "expR", 0, level);
            levelData.DropRate = GetInt(node, "dropR", 0, level);
            levelData.MesoRate = GetInt(node, "mesoR", 0, level);
            levelData.BossDamageRate = GetInt(node, "bdR", levelData.BossDamageRate, level);
            levelData.IgnoreDefenseRate = PreferPrimaryStat(
                GetInt(node, "ignoreMobpdpR", levelData.IgnoreDefenseRate, level),
                GetInt(node, "ignoreMobDamR", 0, level));
            ApplyDescriptionBackedGenericStatAliases(
                skill,
                levelData,
                GetInt(node, "x", 0, level),
                GetInt(node, "y", 0, level),
                GetInt(node, "z", 0, level),
                GetInt(node, "u", 0, level),
                GetInt(node, "v", 0, level),
                GetInt(node, "w", 0, level));
            levelData.AttackPercent = PreferPrimaryStat(levelData.AttackPercent, ResolveDescriptionBackedAttackPercentAlias(skill, node, level));
            levelData.MagicAttackPercent = PreferPrimaryStat(levelData.MagicAttackPercent, ResolveDescriptionBackedMagicAttackPercentAlias(skill, node, level));
        }

        private static int PreferPrimaryStat(int currentValue, int aliasValue)
        {
            return currentValue != 0 ? currentValue : aliasValue;
        }

        private static bool UsesAccuracyXAlias(SkillData skill, WzImageProperty node)
        {
            if (node?["x"] == null || node["acc"] != null || node["accX"] != null)
                return false;

            if (SkillDataTextSurface.GetDescriptionSurface(skill).Contains("#x", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string description = SkillDataTextSurface.GetDescriptionSurface(skill).ToLowerInvariant();
            string name = (skill?.Name ?? string.Empty).ToLowerInvariant();
            return description.Contains("accuracy")
                   || description.Contains("accurary")
                   || name.Contains("mastery");
        }

        private static bool UsesWeaponAttackXAlias(SkillData skill, WzImageProperty node)
        {
            if (node?["x"] == null || node["pad"] != null || node["padX"] != null)
                return false;

            if (SkillDataTextSurface.GetDescriptionSurface(skill).Contains("#x", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string description = SkillDataTextSurface.GetDescriptionSurface(skill).ToLowerInvariant();
            return description.Contains("weapon attack")
                   || description.Contains("att,")
                   || description.Contains("att ")
                   || description.Contains("attack power");
        }

        internal static void ApplyDescriptionBackedGenericStatAliases(
            SkillData skill,
            SkillLevelData levelData,
            int xValue,
            int yValue,
            int zValue,
            int uValue,
            int vValue,
            int wValue)
        {
            if (skill == null || levelData == null)
            {
                return;
            }

            string normalizedSurface = NormalizeDescriptionBackedAliasSurface(SkillDataTextSurface.GetDescriptionSurface(skill));
            if (string.IsNullOrWhiteSpace(normalizedSurface))
            {
                return;
            }

            levelData.PAD = ApplyDescriptionBackedAliasValue(
                levelData.PAD,
                xValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#x", "weapon att", "weapon attack"));
            levelData.MAD = ApplyDescriptionBackedAliasValue(
                levelData.MAD,
                yValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#y", "magic att", "magic attack"));
            levelData.PDD = ApplyDescriptionBackedAliasValue(
                levelData.PDD,
                zValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#z", "weapon def", "weapon defense"));
            levelData.MDD = ApplyDescriptionBackedAliasValue(
                levelData.MDD,
                uValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#u", "magic def", "magic defense"));
            levelData.ACC = ApplyDescriptionBackedAliasValue(
                levelData.ACC,
                vValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#v", "accuracy"));
            levelData.ACC = ApplyDescriptionBackedAliasValue(
                levelData.ACC,
                xValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#x", "accuracy"));
            levelData.ACC = ApplyDescriptionBackedAliasValue(
                levelData.ACC,
                yValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#y", "accuracy"));
            levelData.ACC = ApplyDescriptionBackedAliasValue(
                levelData.ACC,
                zValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#z", "accuracy"));
            levelData.EVA = ApplyDescriptionBackedAliasValue(
                levelData.EVA,
                wValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#w", "avoidability"));
            levelData.EVA = ApplyDescriptionBackedAliasValue(
                levelData.EVA,
                xValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#x", "avoidability"));
            levelData.EVA = ApplyDescriptionBackedAliasValue(
                levelData.EVA,
                yValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#y", "avoidability"));
            levelData.EVA = ApplyDescriptionBackedAliasValue(
                levelData.EVA,
                zValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#z", "avoidability"));
            levelData.Speed = ApplyDescriptionBackedAliasValue(
                levelData.Speed,
                xValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#x", "movement speed", "speed"));
            levelData.Jump = ApplyDescriptionBackedAliasValue(
                levelData.Jump,
                yValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#y", "jump"));
            levelData.CriticalRate = ApplyDescriptionBackedAliasValue(
                levelData.CriticalRate,
                xValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#x", "critical rate"));
            levelData.MaxHPPercent = ApplyDescriptionBackedAliasValue(
                levelData.MaxHPPercent,
                xValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#x", "max hp"));
            levelData.MaxMPPercent = ApplyDescriptionBackedAliasValue(
                levelData.MaxMPPercent,
                xValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#x", "max mp"));
            levelData.MaxHPPercent = ApplyDescriptionBackedAliasValue(
                levelData.MaxHPPercent,
                yValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#y", "max hp"));
            levelData.MaxMPPercent = ApplyDescriptionBackedAliasValue(
                levelData.MaxMPPercent,
                yValue,
                PlaceholderMatchesHintLabel(normalizedSurface, "#y", "max mp"));
        }

        private static int ApplyDescriptionBackedAliasValue(int target, int aliasValue, bool shouldApply)
        {
            if (!shouldApply || aliasValue == 0)
            {
                return target;
            }

            return PreferPrimaryStat(target, aliasValue);
        }

        private static string NormalizeDescriptionBackedAliasSurface(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(
                " ",
                value.Replace("\\r", " ", StringComparison.Ordinal)
                    .Replace("\\n", " ", StringComparison.Ordinal)
                    .ToLowerInvariant()
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool PlaceholderMatchesHintLabel(string normalizedSurface, string placeholderToken, params string[] labelTokens)
        {
            if (string.IsNullOrWhiteSpace(normalizedSurface)
                || string.IsNullOrWhiteSpace(placeholderToken)
                || labelTokens == null
                || labelTokens.Length == 0)
            {
                return false;
            }

            int searchIndex = 0;
            while (searchIndex < normalizedSurface.Length)
            {
                int placeholderIndex = normalizedSurface.IndexOf(
                    placeholderToken,
                    searchIndex,
                    StringComparison.Ordinal);
                if (placeholderIndex < 0)
                {
                    break;
                }

                int contextStart = Math.Max(0, placeholderIndex - 48);
                int contextLength = placeholderIndex - contextStart + placeholderToken.Length;
                string context = normalizedSurface.Substring(contextStart, contextLength);
                foreach (string labelToken in labelTokens)
                {
                    if (!string.IsNullOrWhiteSpace(labelToken)
                        && context.Contains(labelToken, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                searchIndex = placeholderIndex + placeholderToken.Length;
            }

            return false;
        }

        internal static int ResolveDescriptionBackedAttackPercentAlias(SkillData skill, WzImageProperty node, int level)
        {
            return UsesEchoOfHeroAttackPercentAlias(skill, node)
                ? Math.Max(0, GetInt(node, "x", 0, level))
                : 0;
        }

        internal static int ResolveDescriptionBackedMagicAttackPercentAlias(SkillData skill, WzImageProperty node, int level)
        {
            return UsesEchoOfHeroAttackPercentAlias(skill, node)
                ? Math.Max(0, GetInt(node, "x", 0, level))
                : 0;
        }

        internal static bool UsesEchoOfHeroAttackPercentAlias(SkillData skill, WzImageProperty node)
        {
            if (node?["x"] == null)
            {
                return false;
            }

            string name = (skill?.Name ?? string.Empty).Trim();
            string description = SkillDataTextSurface.GetDescriptionSurface(skill).Trim();
            return name.Equals("Echo of Hero", StringComparison.OrdinalIgnoreCase)
                   || (description.Contains("weapon attack", StringComparison.OrdinalIgnoreCase)
                       && description.Contains("magic attack", StringComparison.OrdinalIgnoreCase));
        }

        private static void PopulateSkillLevelRequirements(SkillLevelData levelData, WzImageProperty node)
        {
            if (levelData == null || node == null)
                return;

            WzImageProperty reqNode = node["req"];
            if (reqNode?.WzProperties == null)
                return;

            foreach (WzImageProperty requirementNode in reqNode.WzProperties)
            {
                if (!int.TryParse(requirementNode.Name, out int requiredSkillId) || requiredSkillId <= 0)
                    continue;

                int requiredSkillLevel = 0;
                switch (requirementNode)
                {
                    case WzIntProperty intProperty:
                        requiredSkillLevel = intProperty.Value;
                        break;
                    case WzShortProperty shortProperty:
                        requiredSkillLevel = shortProperty.Value;
                        break;
                    case WzLongProperty longProperty:
                        requiredSkillLevel = (int)longProperty.Value;
                        break;
                    default:
                        requiredSkillLevel = GetInt(reqNode, requirementNode.Name, 0);
                        break;
                }

                if (requiredSkillLevel <= 0)
                    continue;

                levelData.RequiredSkill = requiredSkillId;
                levelData.RequiredSkillLevel = requiredSkillLevel;
                break;
            }
        }

        private static List<int> ParseProjectileSpawnDelays(WzImageProperty node, int level)
        {
            var delays = new List<int>();
            if (node == null)
                return delays;

            for (int index = 0; ; index++)
            {
                string propertyName = index == 0 ? "ballDelay" : $"ballDelay{index}";
                if (node[propertyName] == null)
                    break;

                delays.Add(Math.Max(0, GetInt(node, propertyName, 0, level)));
            }

            return delays;
        }

        private static void ParseFinalAttackTriggers(SkillData skill, WzImageProperty skillNode)
        {
            if (skill == null || skillNode == null)
                return;

            WzImageProperty finalAttackNode = skillNode["finalAttack"] ?? skillNode["info"]?["finalAttack"];
            if (finalAttackNode == null || finalAttackNode.WzProperties == null)
                return;

            foreach (WzImageProperty followUpNode in finalAttackNode.WzProperties)
            {
                if (!int.TryParse(followUpNode.Name, out int followUpSkillId))
                    continue;

                HashSet<int> allowedWeaponCodes = new();
                foreach (WzImageProperty weaponNode in followUpNode.WzProperties)
                {
                    int weaponCode = weaponNode switch
                    {
                        WzIntProperty intProp => intProp.Value,
                        WzShortProperty shortProp => shortProp.Value,
                        WzLongProperty longProp => (int)longProp.Value,
                        _ => 0
                    };

                    if (weaponCode > 0)
                    {
                        allowedWeaponCodes.Add(weaponCode);
                    }
                }

                if (allowedWeaponCodes.Count > 0)
                {
                    skill.FinalAttackTriggers[followUpSkillId] = allowedWeaponCodes;
                }
            }
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

        private static bool HasProperty(WzImageProperty node, string name)
        {
            return node?[name] != null;
        }

        private static int ResolveMorphTemplateId(int skillId, WzImageProperty commonNode, WzImageProperty pvpCommonNode, WzImageProperty infoNode)
        {
            int morphTemplateId = TryGetConcreteMorphTemplateId(commonNode, "morph");
            if (morphTemplateId > 0)
            {
                return morphTemplateId;
            }

            morphTemplateId = TryGetConcreteMorphTemplateId(pvpCommonNode, "morph");
            if (morphTemplateId > 0)
            {
                return morphTemplateId;
            }

            morphTemplateId = TryGetConcreteMorphTemplateId(infoNode, "morph");
            if (morphTemplateId > 0)
            {
                return morphTemplateId;
            }

            return ResolveFlagOnlyMorphAliasTemplateId(skillId, commonNode, pvpCommonNode, infoNode);
        }

        internal static int ResolveMorphTemplateIdForTesting(
            int skillId,
            WzImageProperty commonNode,
            WzImageProperty pvpCommonNode,
            WzImageProperty infoNode)
        {
            return ResolveMorphTemplateId(skillId, commonNode, pvpCommonNode, infoNode);
        }

        private static int TryGetConcreteMorphTemplateId(WzImageProperty node, string name)
        {
            WzImageProperty child = node?[name];
            if (child == null)
            {
                return 0;
            }

            int morphTemplateId = GetInt(node, name);
            return morphTemplateId > 1 ? morphTemplateId : 0;
        }

        private static int ResolveFlagOnlyMorphAliasTemplateId(int skillId, WzImageProperty commonNode, WzImageProperty pvpCommonNode, WzImageProperty infoNode)
        {
            if (!HasFlagOnlyMorphMetadata(commonNode, "morph")
                && !HasFlagOnlyMorphMetadata(pvpCommonNode, "morph")
                && !HasFlagOnlyMorphMetadata(infoNode, "morph"))
            {
                return 0;
            }

            foreach (int morphTemplateId in EnumerateFlagOnlyMorphTemplateCandidates(skillId))
            {
                if (CharacterLoader.CanResolveMorphTemplate(morphTemplateId))
                {
                    return morphTemplateId;
                }
            }

            return 0;
        }

        private static IEnumerable<int> EnumerateFlagOnlyMorphTemplateCandidates(int skillId)
        {
            if (skillId <= 0)
            {
                yield break;
            }

            var seen = new HashSet<int>();
            int suffixTemplateId = skillId % 10000;
            if (suffixTemplateId > 0 && seen.Add(suffixTemplateId))
            {
                yield return suffixTemplateId;
            }
        }

        internal static IReadOnlyList<int> EnumerateFlagOnlyMorphTemplateCandidatesForTesting(int skillId)
        {
            return EnumerateFlagOnlyMorphTemplateCandidates(skillId).ToArray();
        }

        private static bool HasFlagOnlyMorphMetadata(WzImageProperty node, string name)
        {
            if (node?[name] == null)
            {
                return false;
            }

            return GetInt(node, name) == 1;
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

        internal static int[] ResolveRequiredSkillIds(WzImageProperty skillNode, WzImageProperty infoNode)
        {
            var requiredSkillIds = new HashSet<int>();

            foreach (int infoRequiredSkillId in ParseLinkedSkillIds(GetString(infoNode, "requireSkill")))
            {
                requiredSkillIds.Add(infoRequiredSkillId);
            }

            WzImageProperty reqNode = skillNode?["req"];
            if (reqNode != null)
            {
                foreach (WzImageProperty child in reqNode.WzProperties)
                {
                    if (child != null && TryParseRequiredSkillId(child.Name, out int reqRequiredSkillId))
                    {
                        requiredSkillIds.Add(reqRequiredSkillId);
                    }
                }
            }

            return requiredSkillIds.Count > 0
                ? requiredSkillIds.OrderBy(id => id).ToArray()
                : Array.Empty<int>();
        }

        private static bool TryParseRequiredSkillId(string value, out int skillId)
        {
            return int.TryParse(value, out skillId) && skillId > 0;
        }

        #endregion
    }
}
